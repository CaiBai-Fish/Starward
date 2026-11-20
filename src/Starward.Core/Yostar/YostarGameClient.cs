using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Core.Yostar;

/// <summary>
/// 明日方舟国际服（Yostar）启动器 API 客户端。
/// 所有接口都需要 Authorization 签名头：
/// { head: { game_tag, time, version }, sign: md5(head + body + salt) }
/// </summary>
public sealed class YostarGameClient
{
    private readonly HttpClient _httpClient;


    public YostarGameClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }


    public HttpClient HttpClient => _httpClient;


    private static string CreateAuthorization(string data = "")
    {
        long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string head = $"{{\"game_tag\":\"{YostarGameConstants.GameTag}\",\"time\":{time},\"version\":\"{YostarGameConstants.LauncherVersion}\"}}";
        string sign = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(head + data + YostarGameConstants.AuthSalt)));
        return $"{{\"head\":{head},\"sign\":\"{sign}\"}}";
    }


    private async Task<JsonElement> GetDataAsync(string path, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, YostarGameConstants.ApiBaseUrl + path)
                {
                    Version = HttpVersion.Version11,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                };
                request.Headers.TryAddWithoutValidation("Authorization", CreateAuthorization());
                request.Headers.TryAddWithoutValidation("Content-Type", "application/json;charset=UTF-8");
                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                JsonElement root = document.RootElement;
                int code = root.TryGetProperty("code", out JsonElement codeElement) ? codeElement.GetInt32() : -1;
                if (code != 200)
                {
                    string message = root.TryGetProperty("msg", out JsonElement msgElement) ? msgElement.GetString() ?? "" : "";
                    throw new InvalidDataException($"Yostar launcher API returned {code}: {message}");
                }
                if (!root.TryGetProperty("data", out JsonElement data))
                {
                    throw new InvalidDataException("Yostar launcher API returned no data.");
                }
                return data.Clone();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (attempt < 3)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
                }
            }
        }
        throw lastError ?? new InvalidDataException("Failed to request Yostar launcher API.");
    }


    public async Task<YostarGameConfig> GetGameConfigAsync(CancellationToken cancellationToken = default)
    {
        JsonElement data = await GetDataAsync("/api/launcher/game/config", cancellationToken);
        YostarGameConfig config = new()
        {
            LowestVersion = data.TryGetProperty("game_lowest_version", out JsonElement lowest) ? lowest.GetString() ?? "" : "",
            LatestVersion = data.TryGetProperty("game_latest_version", out JsonElement latest) ? latest.GetString() ?? "" : "",
            LatestFilePath = data.TryGetProperty("game_latest_file_path", out JsonElement filePath) ? filePath.GetString() ?? "" : "",
            StartExeName = data.TryGetProperty("game_start_exe_name", out JsonElement exeName) ? exeName.GetString() ?? "" : "",
            DecompressionSize = data.TryGetProperty("decompression_size", out JsonElement decompression) ? decompression.GetString() ?? "" : "",
            UninstallScript = data.TryGetProperty("game_uninstall_script", out JsonElement uninstall) ? uninstall.GetString() ?? "" : "",
        };
        if (data.TryGetProperty("game_start_params", out JsonElement parameters) && parameters.ValueKind is JsonValueKind.Array)
        {
            config.StartParams.AddRange(parameters.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x))!);
        }
        return config;
    }


    public async Task<YostarCdnInfo> GetCdnAsync(CancellationToken cancellationToken = default)
    {
        JsonElement data = await GetDataAsync("/api/launcher/advanced/game/download/cdn", cancellationToken);
        return new YostarCdnInfo
        {
            PrimaryCdn = data.TryGetProperty("primary_cdn", out JsonElement primary) ? primary.GetString() ?? "" : "",
            BackupCdn = data.TryGetProperty("back_up_cdn", out JsonElement backup) ? backup.GetString() ?? "" : "",
        };
    }


    public async Task<string> GetManifestUrlAsync(string version, string filePath, CancellationToken cancellationToken = default)
    {
        string url = $"/api/launcher/game/config/json?version={Uri.EscapeDataString(version)}&file_path={Uri.EscapeDataString(filePath)}";
        JsonElement data = await GetDataAsync(url, cancellationToken);
        return data.TryGetProperty("url", out JsonElement manifestUrl) ? manifestUrl.GetString() ?? "" : "";
    }


    /// <summary>
    /// 拉取 manifest（静态 CDN 文件，无需签名头）。
    /// </summary>
    public async Task<YostarManifest> GetManifestAsync(string url, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;
        YostarManifest manifest = new()
        {
            Source = root.TryGetProperty("source", out JsonElement source) ? source.GetString() ?? "" : "",
        };
        if (root.TryGetProperty("file", out JsonElement files) && files.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement item in files.EnumerateArray())
            {
                manifest.Files.Add(new YostarManifestFile
                {
                    Path = item.TryGetProperty("path", out JsonElement path) ? path.GetString() ?? "" : "",
                    Hash = item.TryGetProperty("hash", out JsonElement hash) ? hash.GetString() ?? "" : "",
                    Size = item.TryGetProperty("size", out JsonElement size) && long.TryParse(size.GetString(), out long parsed) ? parsed : 0,
                });
            }
        }
        return manifest;
    }


    public async Task<YostarManifest> GetLatestManifestAsync(CancellationToken cancellationToken = default)
    {
        YostarGameConfig config = await GetGameConfigAsync(cancellationToken);
        string url = await GetManifestUrlAsync(config.LatestVersion, config.LatestFilePath, cancellationToken);
        return await GetManifestAsync(url, cancellationToken);
    }


    public static string BuildFileUrl(string cdn, string source, string path)
    {
        // manifest 中存在包含 '#' 等字符的资源文件名，必须编码最后一段，否则会被当作 fragment。
        // 目录部分保持原样，与官方启动器一致（例如 [[en]] 目录）。
        string relative = $"{source.Trim('/')}/{path.TrimStart('/')}";
        int index = relative.LastIndexOf('/');
        if (index < 0)
        {
            return $"{cdn.TrimEnd('/')}/{Uri.EscapeDataString(relative)}";
        }
        string directory = relative[..index];
        string fileName = relative[(index + 1)..];
        return $"{cdn.TrimEnd('/')}/{directory}/{Uri.EscapeDataString(fileName)}";
    }
}
