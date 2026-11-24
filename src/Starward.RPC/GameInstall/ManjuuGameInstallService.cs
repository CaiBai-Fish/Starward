using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Starward.Core;
using Starward.Core.Manjuu;
using System.Text.Json;

namespace Starward.RPC.GameInstall;

/// <summary>
/// 蛮啾网络游戏（蓝色星原：旅谣）的安装、更新与修复。
/// 下载模式为文件级索引（REALITY）：下载加密索引，AES 解密后逐文件按大小与 MD5 校验、下载。
/// </summary>
internal sealed class ManjuuGameInstallService
{

    private const int DownloadConcurrency = 8;

    private const int DownloadRetryCount = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };


    private readonly ILogger<ManjuuGameInstallService> _logger;

    private readonly ManjuuLauncherClient _launcherClient;

    private readonly GameInstallHelper _gameInstallHelper;


    public ManjuuGameInstallService(
        ILogger<ManjuuGameInstallService> logger,
        ManjuuLauncherClient launcherClient,
        GameInstallHelper gameInstallHelper)
    {
        _logger = logger;
        _launcherClient = launcherClient;
        _gameInstallHelper = gameInstallHelper;
    }



    public async Task ExecuteAsync(GameInstallContext context, CancellationToken cancellationToken)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(context.GameId.GameBiz);
        Directory.CreateDirectory(context.InstallPath);
        if (context.Operation is not GameInstallOperation.Predownload)
        {
            foreach (string file in Directory.EnumerateFiles(context.InstallPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        ManjuuGameVersion latest = await _launcherClient.GetLatestVersionAsync(context.GameId.GameBiz, cancellationToken);
        string localVersion = ReadLocalVersion(context.InstallPath);
        context.LocalGameVersion = localVersion;
        context.LatestGameVersion = latest.VersionCode;
        context.PredownloadVersion = null;
        _logger.LogInformation("Manjuu game version check: local {localVersion}, latest {latestVersion}", localVersion, latest.VersionCode);

        switch (context.Operation)
        {
            case GameInstallOperation.Install:
            case GameInstallOperation.Update:
            case GameInstallOperation.Repair:
                await InstallOrUpdateAsync(context, profile, latest, localVersion, cancellationToken);
                break;
            case GameInstallOperation.Predownload:
                throw new NotSupportedException("蛮啾游戏暂不支持预下载。");
            default:
                throw new NotSupportedException($"不支持的安装操作：{context.Operation}。");
        }
    }



    private async Task InstallOrUpdateAsync(
        GameInstallContext context,
        ManjuuGameProfile profile,
        ManjuuGameVersion latest,
        string localVersion,
        CancellationToken cancellationToken)
    {
        context.State = GameInstallState.Verifying;
        ManjuuPackageConfig packageConfig = await _launcherClient.GetPackageConfigAsync(context.GameId.GameBiz, null, cancellationToken);
        ManjuuGameVersion version = packageConfig.Released ?? latest;
        string versionCode = version.VersionCode ?? latest.VersionCode ?? "";
        string versionName = version.VersionName ?? latest.VersionName ?? "";

        ManjuuGamePackage? indexPackage = version.GamePackageList?.FirstOrDefault(x => x.PackageType == 2);
        if (indexPackage is null)
        {
            throw new NotSupportedException("当前渠道下发的不是文件级索引包（packageType=2），暂不支持分片整包模式。");
        }
        string? cdnBaseUrl = version.DomainList?.FirstOrDefault() ?? packageConfig.Preload?.DomainList?.FirstOrDefault() ?? profile.CdnBaseUrl;
        if (!string.IsNullOrWhiteSpace(cdnBaseUrl) && !cdnBaseUrl.EndsWith('/'))
        {
            cdnBaseUrl += "/";
        }

        string indexDirectory = Path.Combine(context.InstallPath, "_index");
        Directory.CreateDirectory(indexDirectory);
        string indexFile = Path.Combine(indexDirectory, versionCode);
        byte[] indexBytes = await ReadOrDownloadIndexAsync(context, profile, indexPackage, indexFile, cdnBaseUrl, cancellationToken);
        ManjuuGameIndex index = ManjuuGameIndexParser.Parse(indexBytes);
        _logger.LogInformation("Manjuu index parsed: {fileCount} files, {directoryCount} directories, package size {packageSize}", index.Files.Count, index.Directories.Count, index.PackageSize);

        ManjuuGameIndex? oldIndex = null;
        if (context.Operation is GameInstallOperation.Update && !string.IsNullOrWhiteSpace(localVersion) && localVersion != versionCode)
        {
            string oldIndexFile = Path.Combine(indexDirectory, localVersion);
            if (File.Exists(oldIndexFile))
            {
                try
                {
                    oldIndex = ManjuuGameIndexParser.Parse(await File.ReadAllBytesAsync(oldIndexFile, cancellationToken));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse local Manjuu index {version}", localVersion);
                }
            }
        }

        List<ManjuuIndexEntry> files = index.Files.Where(x => !IsExcluded(profile, x.Name)).ToList();
        long totalSize = files.Sum(x => x.Size);
        long finishedSize = 0;
        List<(ManjuuIndexEntry Entry, string Path, string Url)> pending = [];
        foreach (ManjuuIndexEntry entry in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(context.InstallPath, entry.Name);
            bool exists = File.Exists(path);
            if (exists && new FileInfo(path).Length == entry.Size)
            {
                // 安装时按大小判断，修复时进一步校验 MD5
                if (context.Operation is not GameInstallOperation.Repair)
                {
                    finishedSize += entry.Size;
                    continue;
                }
                if (await CheckFileMD5Async(path, entry.MD5, cancellationToken))
                {
                    finishedSize += entry.Size;
                    continue;
                }
            }
            pending.Add((entry, path, cdnBaseUrl + entry.RemotePath.TrimStart('/')));
        }

        context.Progress_DownloadTotalBytes = totalSize;
        context.Progress_DownloadFinishBytes = finishedSize;
        context.Progress_WriteTotalBytes = totalSize;
        context.Progress_WriteFinishBytes = finishedSize;
        context.Progress_Percent = totalSize == 0 ? 1 : (double)finishedSize / totalSize;
        _logger.LogInformation("Manjuu install plan: {pendingCount} files to download, {pendingSize} bytes, total {totalSize}", pending.Count, pending.Sum(x => x.Entry.Size), totalSize);

        if (pending.Count > 0)
        {
            context.State = GameInstallState.Downloading;
            await Parallel.ForEachAsync(pending, new ParallelOptions
            {
                MaxDegreeOfParallelism = DownloadConcurrency,
                CancellationToken = cancellationToken,
            }, async (item, token) =>
            {
                await DownloadWithRetryAsync(context, item.Path, item.Url, item.Entry.Size, item.Entry.MD5, token);
            });
        }

        if (oldIndex is not null)
        {
            await CleanDeprecatedFilesAsync(context, profile, index, oldIndex, cancellationToken);
        }

        context.Progress_Percent = 1;
        WriteInstallMetadata(context.InstallPath, versionCode, versionName, version.LaunchEntry ?? profile.ExeName);
    }



    private async Task<byte[]> ReadOrDownloadIndexAsync(
        GameInstallContext context,
        ManjuuGameProfile profile,
        ManjuuGamePackage indexPackage,
        string indexFile,
        string? cdnBaseUrl,
        CancellationToken cancellationToken)
    {
        if (File.Exists(indexFile) && new FileInfo(indexFile).Length == indexPackage.FileSize)
        {
            try
            {
                byte[] cached = await File.ReadAllBytesAsync(indexFile, cancellationToken);
                _ = ManjuuGameIndexParser.Parse(cached);
                return cached;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cached Manjuu index is invalid, re-downloading.");
            }
        }
        byte[] bytes = await _launcherClient.DownloadIndexAsync(context.GameId.GameBiz, indexPackage, cdnBaseUrl, cancellationToken);
        string temporary = indexFile + "_tmp";
        await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
        File.Move(temporary, indexFile, true);
        return bytes;
    }



    private async Task DownloadWithRetryAsync(GameInstallContext context, string path, string url, long size, string md5, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                await _gameInstallHelper.DownloadToFileAsync(context, path, url, size, md5, cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < DownloadRetryCount)
            {
                _logger.LogWarning(ex, "Download failed ({attempt}/{retry}), retrying: {url}", attempt + 1, DownloadRetryCount, url);
                await Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), cancellationToken);
            }
        }
    }



    private async Task CleanDeprecatedFilesAsync(GameInstallContext context, ManjuuGameProfile profile, ManjuuGameIndex newIndex, ManjuuGameIndex oldIndex, CancellationToken cancellationToken)
    {
        HashSet<string> newFiles = new(newIndex.Files.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
        context.State = GameInstallState.Verifying;
        int deleted = 0;
        foreach (ManjuuIndexEntry entry in oldIndex.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (newFiles.Contains(entry.Name) || IsExcluded(profile, entry.Name))
            {
                continue;
            }
            string path = Path.Combine(context.InstallPath, entry.Name);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete deprecated file {path}", path);
            }
        }
        _logger.LogInformation("Manjuu install cleanup: deleted {count} deprecated files", deleted);
    }



    private async Task<bool> CheckFileMD5Async(string path, string md5, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = File.OpenRead(path);
            byte[] hash = await System.Security.Cryptography.MD5.HashDataAsync(stream, cancellationToken);
            return string.Equals(Convert.ToHexStringLower(hash), md5, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check file MD5: {path}", path);
            return false;
        }
    }



    private static bool IsExcluded(ManjuuGameProfile profile, string name)
    {
        string normalized = name.Replace('/', Path.DirectorySeparatorChar);
        foreach (ManjuuExcludedFile item in profile.ExcludedFiles)
        {
            string path = (item.FilePath ?? "").Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }
            if (item.FileType == 1)
            {
                if (string.Equals(normalized, path, StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(normalized, path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }



    /// <summary>
    /// 读取本地版本：优先读取官方启动器写入的 _index/version，其次读注册表。
    /// </summary>
    private string ReadLocalVersion(string installPath)
    {
        try
        {
            string versionFile = Path.Combine(installPath, "_index", "version");
            if (File.Exists(versionFile))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(versionFile));
                if (document.RootElement.TryGetProperty("version", out JsonElement version) && version.ValueKind is JsonValueKind.String)
                {
                    return version.GetString() ?? "";
                }
            }
        }
        catch
        {
        }
        return "";
    }



    private void WriteInstallMetadata(string installPath, string versionCode, string versionName, string launchEntry)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(GameBiz.azurpromilia_cn);
        try
        {
            string versionFile = Path.Combine(installPath, "_index", "version");
            Directory.CreateDirectory(Path.GetDirectoryName(versionFile)!);
            var payload = new { version = versionCode, versionName };
            File.WriteAllText(versionFile, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Manjuu version file.");
        }
        try
        {
            string subKey = string.Join(Path.DirectorySeparatorChar, "SOFTWARE", "AzurPromilia", $"{profile.GameId}_{profile.ChannelCode}");
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey, true);
            key.SetValue("Path", installPath, RegistryValueKind.String);
            key.SetValue("Version", versionCode, RegistryValueKind.String);
            key.SetValue("VersionName", versionName, RegistryValueKind.String);
            key.SetValue("Exe", launchEntry, RegistryValueKind.String);
            _logger.LogInformation("Manjuu install metadata written: {version} at {path}", versionCode, installPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write Manjuu install registry.");
        }
    }

}
