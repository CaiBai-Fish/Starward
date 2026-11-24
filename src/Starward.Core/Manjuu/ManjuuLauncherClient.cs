using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Starward.Core.Manjuu;

/// <summary>
/// 蛮啾启动器接口客户端。
/// 接口签名与官方启动器一致：MD5(timestamp + query + utoken + appId + channelCode + secretKey)。
/// </summary>
public sealed class ManjuuLauncherClient
{

    private readonly HttpClient _httpClient;


    public ManjuuLauncherClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }


    /// <summary>
    /// 获取最新版本信息（gameversion/config）。
    /// </summary>
    public async Task<ManjuuGameVersion> GetLatestVersionAsync(GameBiz gameBiz, CancellationToken cancellationToken = default)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
        Dictionary<string, string> query = new()
        {
            ["launcherId"] = profile.LauncherId,
        };
        ManjuuGameInfo? info = await GetAsync<ManjuuGameInfo>(gameBiz, "/pc-launcher/client/gameversion/config", query, cancellationToken);
        return info?.Released ?? throw new InvalidDataException($"蛮啾接口未返回 {profile.GameBiz} 的版本信息。");
    }


    /// <summary>
    /// 获取下载包配置（gameversion/packageconfig），包含索引地址与 CDN 域名。
    /// </summary>
    public async Task<ManjuuPackageConfig> GetPackageConfigAsync(GameBiz gameBiz, string? uid = null, CancellationToken cancellationToken = default)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
        Dictionary<string, string> query = new()
        {
            ["launcherId"] = profile.LauncherId,
            ["uid"] = uid ?? "",
        };
        ManjuuPackageConfig? config = await GetAsync<ManjuuPackageConfig>(gameBiz, "/pc-launcher/client/gameversion/packageconfig", query, cancellationToken);
        return config ?? throw new InvalidDataException($"蛮啾接口未返回 {profile.GameBiz} 的下载配置。");
    }


    /// <summary>
    /// 获取启动器内容配置（背景图、轮播、资讯与社交）。
    /// </summary>
    public async Task<ManjuuLauncherContent> GetContentAsync(GameBiz gameBiz, CancellationToken cancellationToken = default)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
        Dictionary<string, string> query = new()
        {
            ["launcherId"] = profile.LauncherId,
        };
        ManjuuLauncherContent? content = await GetAsync<ManjuuLauncherContent>(gameBiz, "/pc-launcher/client/launcher/contentconfig", query, cancellationToken);
        return content ?? new ManjuuLauncherContent();
    }


    /// <summary>
    /// 下载版本索引文件并校验 MD5。
    /// </summary>
    public async Task<byte[]> DownloadIndexAsync(GameBiz gameBiz, ManjuuGamePackage package, string? preferredCdnBaseUrl, CancellationToken cancellationToken = default)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
        if (string.IsNullOrWhiteSpace(package.DownloadUrl))
        {
            throw new InvalidDataException("蛮啾版本索引地址为空。");
        }
        List<string> bases = [];
        if (!string.IsNullOrWhiteSpace(preferredCdnBaseUrl))
        {
            bases.Add(preferredCdnBaseUrl);
        }
        if (package.CdnBaseUrls is { Count: > 0 })
        {
            foreach (string item in package.CdnBaseUrls)
            {
                if (!bases.Contains(item, StringComparer.OrdinalIgnoreCase))
                {
                    bases.Add(item);
                }
            }
        }
        if (bases.Count == 0)
        {
            bases.Add(profile.CdnBaseUrl);
            bases.Add(profile.CdnBackupBaseUrl);
        }
        Exception? lastError = null;
        foreach (string baseUrl in bases)
        {
            try
            {
                string url = baseUrl.TrimEnd('/') + "/" + package.DownloadUrl.TrimStart('/');
                using HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (package.FileSize > 0 && bytes.LongLength != package.FileSize)
                {
                    throw new InvalidDataException($"版本索引大小不符：期望 {package.FileSize}，实际 {bytes.LongLength}。");
                }
                if (!string.IsNullOrWhiteSpace(package.FileMd5))
                {
                    string md5 = Convert.ToHexStringLower(MD5.HashData(bytes));
                    if (!string.Equals(md5, package.FileMd5, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("版本索引 MD5 校验失败。");
                    }
                }
                return bytes;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }
        throw new InvalidDataException("下载蛮啾版本索引失败。", lastError);
    }


    private async Task<T?> GetAsync<T>(GameBiz gameBiz, string path, Dictionary<string, string> query, CancellationToken cancellationToken)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
        string queryString = string.Join('&', query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string sign = ManjuuGameConstants.Sign(timestamp, queryString, "", profile.AppId, profile.ChannelCode, profile.SecretKey);
        Exception? lastError = null;
        foreach (string baseUrl in new[] { profile.ApiBaseUrl, profile.ApiBackupBaseUrl })
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}{path}?{queryString}");
                request.Headers.TryAddWithoutValidation("timestamp", timestamp);
                request.Headers.TryAddWithoutValidation("sign", sign);
                request.Headers.TryAddWithoutValidation("device-id", Guid.NewGuid().ToString());
                request.Headers.TryAddWithoutValidation("grp-app-id", profile.AppId);
                request.Headers.TryAddWithoutValidation("grp-platform", "Launcher");
                request.Headers.TryAddWithoutValidation("grp-channel-code", profile.ChannelCode);
                request.Headers.TryAddWithoutValidation("grp-sub-channel-code", profile.SubChannelCode);
                request.Headers.TryAddWithoutValidation("sdk-version", ManjuuGameConstants.LauncherVersion);
                request.Headers.TryAddWithoutValidation("channel-sdk-version", ManjuuGameConstants.LauncherVersion);
                request.Headers.TryAddWithoutValidation("grp-launcher-version-code", ManjuuGameConstants.LauncherVersion);
                request.Headers.TryAddWithoutValidation("grp-trace-id", Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));
                request.Headers.TryAddWithoutValidation("lang", "zh-Hans");
                using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                ManjuuResponse<T>? result = await response.Content.ReadFromJsonAsync<ManjuuResponse<T>>(cancellationToken);
                if (result is null)
                {
                    throw new InvalidDataException("蛮啾接口返回内容为空。");
                }
                if (result.Code != 0)
                {
                    throw new miHoYoApiException(result.Code, result.Message ?? "蛮啾接口返回错误。");
                }
                return result.Data;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }
        throw new InvalidDataException("请求蛮啾接口失败。", lastError);
    }

}
