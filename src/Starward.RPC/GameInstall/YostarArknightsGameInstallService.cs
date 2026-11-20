using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.Yostar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.RPC.GameInstall;

/// <summary>
/// 明日方舟国际服（Yostar）安装/更新：按 manifest 逐文件下载，CRC-64/XZ 校验，Range 续传。
/// </summary>
internal sealed class YostarArknightsGameInstallService
{
    private const int DownloadConcurrency = 3;

    private const int MaxAttempts = 5;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<YostarArknightsGameInstallService> _logger;

    private readonly YostarGameClient _client;


    public YostarArknightsGameInstallService(ILogger<YostarArknightsGameInstallService> logger, YostarGameClient client)
    {
        _logger = logger;
        _client = client;
    }


    public async Task ExecuteAsync(GameInstallContext context, CancellationToken cancellationToken)
    {
        if (context.Operation is GameInstallOperation.Predownload)
        {
            throw new NotSupportedException("Arknights EN does not support predownload.");
        }

        string installPath = context.InstallPath;
        Directory.CreateDirectory(installPath);
        foreach (string file in Directory.EnumerateFiles(installPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        YostarGameConfig config = await _client.GetGameConfigAsync(cancellationToken);
        if (config.IsEmpty)
        {
            throw new InvalidDataException("The Yostar launcher returned an empty game config.");
        }
        YostarCdnInfo cdn = await _client.GetCdnAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(cdn.PrimaryCdn) && string.IsNullOrWhiteSpace(cdn.BackupCdn))
        {
            throw new InvalidDataException("The Yostar launcher returned no CDN.");
        }
        string manifestUrl = await _client.GetManifestUrlAsync(config.LatestVersion, config.LatestFilePath, cancellationToken);
        YostarManifest manifest = await _client.GetManifestAsync(manifestUrl, cancellationToken);
        if (manifest.Files.Count == 0)
        {
            throw new InvalidDataException("The Yostar launcher returned an empty manifest.");
        }

        LocalManifest local = ReadLocalManifest(installPath);
        context.LocalGameVersion = local.Version;
        context.LatestGameVersion = config.LatestVersion;

        Dictionary<string, YostarManifestFile> localFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (YostarManifestFile item in local.Files)
        {
            localFiles[Normalize(item.Path)] = item;
        }

        List<YostarManifestFile> downloadFiles = [];
        bool noLocalManifest = local.Files.Count == 0;
        foreach (YostarManifestFile file in manifest.Files)
        {
            string key = Normalize(file.Path);
            bool upToDate = localFiles.TryGetValue(key, out YostarManifestFile? localFile)
                && string.Equals(localFile.Hash, file.Hash, StringComparison.Ordinal)
                && localFile.Size == file.Size
                && File.Exists(GetTargetPath(installPath, file.Path));
            if (!upToDate && noLocalManifest)
            {
                // 没有本地 manifest（首次安装或中断后）时，按大小 + CRC 复用已下载的文件。
                string targetPath = GetTargetPath(installPath, file.Path);
                if (File.Exists(targetPath) && new FileInfo(targetPath).Length == file.Size)
                {
                    string crc = await Crc64Xz.ComputeFileAsync(targetPath, cancellationToken);
                    upToDate = string.Equals(crc, file.Hash, StringComparison.Ordinal);
                    if (!upToDate)
                    {
                        File.Delete(targetPath);
                    }
                }
            }
            if (!upToDate)
            {
                downloadFiles.Add(file);
            }
        }

        List<string> deleteFiles = localFiles
            .Where(x => !manifest.Files.Any(f => string.Equals(Normalize(f.Path), x.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Value.Path)
            .ToList();

        context.Progress_DownloadTotalBytes = downloadFiles.Sum(x => x.Size);
        context.Progress_DownloadFinishBytes = 0;
        context.State = GameInstallState.Downloading;

        _logger.LogInformation("Arknights EN install: version {version}, download {count} files ({bytes} bytes), delete {delete} files.",
            config.LatestVersion, downloadFiles.Count, context.Progress_DownloadTotalBytes, deleteFiles.Count);

        if (downloadFiles.Count > 0)
        {
            await DownloadFilesAsync(installPath, manifest.Source, cdn, downloadFiles, context, cancellationToken);
        }

        context.State = GameInstallState.Merging;
        foreach (string relative in deleteFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string path = GetTargetPath(installPath, relative);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete obsolete file {path}", relative);
            }
        }

        WriteManifest(installPath, config, manifest);
        WriteGameConfig(installPath, config);

        context.Progress_DownloadFinishBytes = context.Progress_DownloadTotalBytes;
        context.Progress_Percent = 1;
        context.State = GameInstallState.Finish;
    }


    private async Task DownloadFilesAsync(string installPath, string source, YostarCdnInfo cdn, List<YostarManifestFile> files, GameInstallContext context, CancellationToken cancellationToken)
    {
        using SemaphoreSlim semaphore = new(DownloadConcurrency);
        List<Task> tasks = [];
        foreach (YostarManifestFile file in files)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await DownloadFileWithRetryAsync(installPath, source, cdn, file, context, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }


    private async Task DownloadFileWithRetryAsync(string installPath, string source, YostarCdnInfo cdn, YostarManifestFile file, GameInstallContext context, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long added = 0;
            try
            {
                await DownloadFileCoreAsync(installPath, source, cdn, file, bytes =>
                {
                    added += bytes;
                    Interlocked.Add(ref context._progress_DownloadFinishBytes, bytes);
                    Interlocked.Add(ref context.networkDownloadBytes, bytes);
                }, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Interlocked.Add(ref context._progress_DownloadFinishBytes, -added);
                _logger.LogWarning(ex, "Failed to download {path} (attempt {attempt}/{max})", file.Path, attempt, MaxAttempts);
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                }
            }
        }
        throw new InvalidDataException($"Failed to download {file.Path}: {lastError?.Message}", lastError);
    }


    private async Task DownloadFileCoreAsync(string installPath, string source, YostarCdnInfo cdn, YostarManifestFile file, Action<long> onBytes, CancellationToken cancellationToken)
    {
        string targetPath = GetTargetPath(installPath, file.Path);
        string tempPath = targetPath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        long existing = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;
        if (existing > file.Size)
        {
            File.Delete(tempPath);
            existing = 0;
        }
        if (existing > 0)
        {
            onBytes(existing);
        }

        List<string> domains = [cdn.PrimaryCdn, cdn.BackupCdn];
        Exception? lastError = null;
        foreach (string domain in domains.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            try
            {
                long currentLength = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;
                if (currentLength > file.Size)
                {
                    File.Delete(tempPath);
                    onBytes(-currentLength);
                    currentLength = 0;
                }
                string url = YostarGameClient.BuildFileUrl(domain, source, file.Path);
                using HttpRequestMessage request = new(HttpMethod.Get, url)
                {
                    Version = HttpVersion.Version11,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                };
                if (currentLength > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(currentLength, null);
                }
                using HttpResponseMessage response = await _client.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode} for {url}");
                }
                bool partial = response.StatusCode is HttpStatusCode.PartialContent;
                long appendFrom = partial ? currentLength : 0;
                if (!partial && currentLength > 0)
                {
                    // 服务端忽略了 Range，需要从头写入，并回退已计入的进度。
                    File.Delete(tempPath);
                    onBytes(-currentLength);
                    currentLength = 0;
                }
                await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using FileStream output = new(tempPath, appendFrom > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
                byte[] buffer = new byte[1 << 20];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    onBytes(read);
                }
                await output.FlushAsync(cancellationToken);
                lastError = null;
                break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Download {path} from {domain} failed", file.Path, domain);
            }
        }
        if (lastError is not null)
        {
            throw lastError;
        }

        FileInfo info = new(tempPath);
        if (info.Length != file.Size)
        {
            File.Delete(tempPath);
            throw new InvalidDataException($"Size mismatch for {file.Path}: expected {file.Size}, actual {info.Length}.");
        }
        string crc = await Crc64Xz.ComputeFileAsync(tempPath, cancellationToken);
        if (!string.Equals(crc, file.Hash, StringComparison.Ordinal))
        {
            File.Delete(tempPath);
            throw new InvalidDataException($"CRC mismatch for {file.Path}: expected {file.Hash}, actual {crc}.");
        }
        File.Move(tempPath, targetPath, overwrite: true);
    }


    private sealed class LocalManifest
    {
        public string Version { get; set; } = "";

        public List<YostarManifestFile> Files { get; set; } = [];
    }


    private static LocalManifest ReadLocalManifest(string installPath)
    {
        LocalManifest result = new();
        string manifestPath = Path.Combine(installPath, YostarGameConstants.ManifestFileName);
        if (File.Exists(manifestPath))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("version", out JsonElement version))
                {
                    result.Version = version.GetString() ?? "";
                }
                if (root.TryGetProperty("files", out JsonElement files) && files.ValueKind is JsonValueKind.Array)
                {
                    foreach (JsonElement item in files.EnumerateArray())
                    {
                        result.Files.Add(new YostarManifestFile
                        {
                            Path = item.TryGetProperty("path", out JsonElement path) ? path.GetString() ?? "" : "",
                            Hash = item.TryGetProperty("hash", out JsonElement hash) ? hash.GetString() ?? "" : "",
                            Size = item.TryGetProperty("size", out JsonElement size) && long.TryParse(size.GetString(), out long parsed) ? parsed : 0,
                        });
                    }
                }
            }
            catch
            {
                result = new LocalManifest();
            }
        }
        if (string.IsNullOrWhiteSpace(result.Version))
        {
            string configPath = Path.Combine(installPath, YostarGameConstants.ConfigFileName);
            if (File.Exists(configPath))
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
                    if (document.RootElement.TryGetProperty("version", out JsonElement version))
                    {
                        result.Version = version.GetString() ?? "";
                    }
                }
                catch { }
            }
        }
        return result;
    }


    private static void WriteManifest(string installPath, YostarGameConfig config, YostarManifest manifest)
    {
        string name = YostarGameConstants.GameTag;
        string vc = ObjectHash(name, config.LatestVersion, config.LatestFilePath);
        var files = manifest.Files.Select(file => new
        {
            path = file.Path,
            hash = file.Hash,
            size = file.Size.ToString(),
            vc = ObjectHash(file.Path, file.Hash, file.Size.ToString()),
        }).ToArray();
        var data = new
        {
            name,
            version = config.LatestVersion,
            basis = config.LatestFilePath,
            vc,
            files,
        };
        WriteJson(Path.Combine(installPath, YostarGameConstants.ManifestFileName), data);
    }


    private static void WriteGameConfig(string installPath, YostarGameConfig config)
    {
        string[] parameters = config.StartParams.ToArray();
        string vc = ObjectHash(YostarGameConstants.GameTag, "Arknights", string.Join(",", parameters), config.LatestVersion, config.UninstallScript);
        var data = new
        {
            tag = YostarGameConstants.GameTag,
            name = "Arknights",
            @params = parameters,
            version = config.LatestVersion,
            gameUninstallScript = config.UninstallScript,
            vc,
        };
        WriteJson(Path.Combine(installPath, YostarGameConstants.ConfigFileName), data);
    }


    private static void WriteJson(string path, object data)
    {
        string json = JsonSerializer.Serialize(data, JsonOptions);
        string temp = path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }


    private static string ObjectHash(params string[] values)
    {
        return Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes(string.Join(";", values))));
    }


    private static string Normalize(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }


    private static string GetTargetPath(string installPath, string relativePath)
    {
        string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar, '/');
        string root = Path.GetFullPath(installPath);
        string full = Path.GetFullPath(Path.Combine(root, normalized));
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Invalid manifest path: {relativePath}");
        }
        return full;
    }
}
