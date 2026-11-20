using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Core.Hypergryph;

/// <summary>
/// 游戏内公告（bulletin）客户端。数据与游戏内公告界面一致：标题、日期、富文本正文。
/// </summary>
public sealed class HypergryphBulletinClient
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 6.2; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) QtWebEngine/5.15.8 Chrome/87.0.4280.144 Safari/537.36 PC/WIN/HGSDK HGWebPC/1.30.1";

    private static readonly HttpClient _httpClient = new();

    static HypergryphBulletinClient()
    {
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }


    public async Task<List<HypergryphBulletinItem>> GetBulletinsAsync(GameBiz gameBiz, string? language = null, CancellationToken cancellationToken = default)
    {
        bool isChina = gameBiz.Value.EndsWith("_cn", StringComparison.Ordinal);
        string baseUrl = isChina ? HypergryphGameConstants.ChinaGameHubUrl : HypergryphGameConstants.GlobalGameHubUrl;
        string code = isChina ? HypergryphGameConstants.EndfieldBulletinCodeCN : HypergryphGameConstants.EndfieldBulletinCodeOS;
        int channel = isChina ? HypergryphGameConstants.EndfieldBulletinChannelCN : HypergryphGameConstants.EndfieldBulletinChannelOS;
        string lang = string.IsNullOrWhiteSpace(language) ? "zh-cn" : LanguageUtil.FilterLanguage(language);
        string url = $"{baseUrl}/bulletin/v2/aggregate?lang={Uri.EscapeDataString(lang)}&platform=Windows&channel={channel}&type=0&code={code}&hideDetail=0";
        if (!isChina)
        {
            url += $"&server={HypergryphGameConstants.EndfieldBulletinServerOS}";
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        HypergryphBulletinAggregate? aggregate = await response.Content.ReadFromJsonAsync<HypergryphBulletinAggregate>(cancellationToken: cancellationToken);
        if (aggregate is null || aggregate.Code != 0)
        {
            throw new InvalidDataException($"The Endfield bulletin request failed: {aggregate?.Code} {aggregate?.Message}");
        }
        return aggregate.Data?.List ?? [];
    }
}
