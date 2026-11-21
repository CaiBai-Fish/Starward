using Microsoft.Extensions.Logging;
using Starward.Features.Gacha.Endfield;
using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.EndfieldToolbox;

internal sealed class EndfieldRecordService
{
    private const string SklandAppCode = "4ca99fa6b56cc2ba";

    private const string GrantApi = "https://as.hypergryph.com/user/oauth2/v2/grant";

    private const string CredApi = "https://zonai.skland.com/api/v1/user/auth/generate_cred_by_code";

    private const string SklandApi = "https://zonai.skland.com/web/v1/game/endfield";

    private const string SklandUserAgent = "Skland/1.32.1 (com.hypergryph.skland; build:103201004; Android 33; ) Okhttp/4.11.0";

    private readonly ILogger<EndfieldRecordService> _logger;

    private readonly HttpClient _httpClient;

    private readonly EndfieldGachaService _gachaService;


    public EndfieldRecordService(ILogger<EndfieldRecordService> logger, HttpClient httpClient,
        EndfieldGachaService gachaService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _gachaService = gachaService;
    }


    public async Task<JsonDocument> GetWarEchoesAsync(EndfieldGachaAccount account, CancellationToken cancellationToken = default)
    {
        return await GetRecordAsync(account, "war-echoes", cancellationToken);
    }


    public async Task<JsonDocument> GetCrisisContractAsync(EndfieldGachaAccount account, CancellationToken cancellationToken = default)
    {
        return await GetRecordAsync(account, "crisis-contract", cancellationToken);
    }


    public async Task<JsonDocument> GetUmbralMonumentAsync(EndfieldGachaAccount account, CancellationToken cancellationToken = default)
    {
        return await GetRecordAsync(account, "indie-hard", cancellationToken);
    }


    private async Task<JsonDocument> GetRecordAsync(EndfieldGachaAccount account, string endpoint,
        CancellationToken cancellationToken)
    {
        string loginToken = _gachaService.GetSavedLoginToken(account.AccountKey);
        if (string.IsNullOrWhiteSpace(loginToken))
        {
            throw new InvalidOperationException("终末地账号登录已失效，请重新登录。");
        }
        SklandCredential credential = await GetCredentialAsync(loginToken, cancellationToken);
        string url = $"{SklandApi}/card/{endpoint}" +
                     $"?roleId={Uri.EscapeDataString(account.RoleId)}" +
                     $"&serverId={Uri.EscapeDataString(account.ServerId)}" +
                     $"&userId={Uri.EscapeDataString(credential.UserId)}";
        JsonDocument document = await GetSignedAsync(url, credential,
            $"3_{account.RoleId}_{account.ServerId}", cancellationToken);
        _logger.LogInformation("Endfield {Endpoint} response: {Json}", endpoint, document.RootElement.GetRawText());
        return document;
    }


    private async Task<SklandCredential> GetCredentialAsync(string loginToken, CancellationToken cancellationToken)
    {
        using HttpRequestMessage grantRequest = new(HttpMethod.Post, GrantApi);
        grantRequest.Headers.TryAddWithoutValidation("User-Agent", SklandUserAgent);
        grantRequest.Content = JsonContent.Create(new { token = loginToken, appCode = SklandAppCode, type = 0 });
        using HttpResponseMessage grantResponse = await _httpClient.SendAsync(grantRequest, cancellationToken);
        using JsonDocument grant = await JsonDocument.ParseAsync(await grantResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!grantResponse.IsSuccessStatusCode || GetInt32(grant.RootElement, "status") != 0
            || !grant.RootElement.TryGetProperty("data", out JsonElement grantData)
            || !grantData.TryGetProperty("code", out JsonElement codeElement))
        {
            throw new InvalidOperationException("获取森空岛授权失败，请重新登录终末地账号。");
        }
        string code = codeElement.GetString() ?? "";
        using HttpRequestMessage credRequest = new(HttpMethod.Post, CredApi);
        credRequest.Headers.TryAddWithoutValidation("User-Agent", SklandUserAgent);
        credRequest.Content = JsonContent.Create(new { kind = 1, code });
        using HttpResponseMessage credResponse = await _httpClient.SendAsync(credRequest, cancellationToken);
        using JsonDocument cred = await JsonDocument.ParseAsync(await credResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!credResponse.IsSuccessStatusCode
            || cred.RootElement.TryGetProperty("code", out JsonElement credCode) && GetInt32(cred.RootElement, "code") != 0
            || !cred.RootElement.TryGetProperty("data", out JsonElement credData))
        {
            throw new InvalidOperationException("获取森空岛凭证失败，请重新登录终末地账号。");
        }
        return new SklandCredential
        {
            Cred = GetString(credData, "cred"),
            Token = GetString(credData, "token"),
            UserId = GetString(credData, "userId"),
        };
    }


    private async Task<JsonDocument> GetSignedAsync(string url, SklandCredential credential, string gameRole,
        CancellationToken cancellationToken)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
        Uri uri = new(url);
        string query = uri.Query.TrimStart('?');
        string headerJson = $"{{\"platform\":\"\",\"timestamp\":\"{timestamp}\",\"dId\":\"\",\"vName\":\"\"}}";
        string secret = $"{uri.AbsolutePath}{query}{timestamp}{headerJson}";
        byte[] hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(credential.Token), Encoding.UTF8.GetBytes(secret));
        string hexHash = Convert.ToHexString(hash).ToLowerInvariant();
        string sign = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(hexHash))).ToLowerInvariant();
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", SklandUserAgent);
        request.Headers.TryAddWithoutValidation("cred", credential.Cred);
        request.Headers.TryAddWithoutValidation("sign", sign);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("platform", "");
        request.Headers.TryAddWithoutValidation("dId", "");
        request.Headers.TryAddWithoutValidation("vName", "");
        request.Headers.TryAddWithoutValidation("sk-game-role", gameRole);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"森空岛接口请求失败（HTTP {(int)response.StatusCode}）。");
        }
        JsonDocument document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("code", out JsonElement code) && GetInt32(document.RootElement, "code") != 0)
        {
            string message = GetString(document.RootElement, "message");
            document.Dispose();
            throw new InvalidOperationException($"森空岛接口返回错误：{message}");
        }
        return document;
    }


    private static string GetString(JsonElement element, string name)
    {
        return element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind is JsonValueKind.String ? value.GetString() ?? "" : value.ToString()
            : "";
    }


    private static int GetInt32(JsonElement element, string name)
    {
        return element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt32(out int result) ? result : 0;
    }


    private sealed class SklandCredential
    {
        public string Cred { get; set; } = "";
        public string Token { get; set; } = "";
        public string UserId { get; set; } = "";
    }
}
