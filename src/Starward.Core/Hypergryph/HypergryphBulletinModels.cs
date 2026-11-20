using System.Text.Json.Serialization;

namespace Starward.Core.Hypergryph;

/// <summary>
/// 游戏内公告（bulletin）聚合接口响应。
/// </summary>
public sealed class HypergryphBulletinAggregate
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public HypergryphBulletinData? Data { get; set; }
}


public sealed class HypergryphBulletinData
{
    [JsonPropertyName("topicCid")]
    public string TopicCid { get; set; } = "";

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public long UpdatedAt { get; set; }

    [JsonPropertyName("list")]
    public List<HypergryphBulletinItem> List { get; set; } = [];
}


public sealed class HypergryphBulletinItem
{
    [JsonPropertyName("cid")]
    public string Cid { get; set; } = "";

    [JsonPropertyName("type")]
    public int Type { get; set; }

    /// <summary>
    /// news / updates / events
    /// </summary>
    [JsonPropertyName("tab")]
    public string Tab { get; set; } = "";

    [JsonPropertyName("displayType")]
    public string DisplayType { get; set; } = "";

    [JsonPropertyName("startAt")]
    public long StartAt { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("header")]
    public string Header { get; set; } = "";

    [JsonPropertyName("focus")]
    public int Focus { get; set; }

    [JsonPropertyName("orderType")]
    public int OrderType { get; set; }

    [JsonPropertyName("orderWeight")]
    public int OrderWeight { get; set; }

    [JsonPropertyName("data")]
    public HypergryphBulletinContent? Data { get; set; }

    /// <summary>
    /// 列表显示用的标题，优先 header
    /// </summary>
    public string DisplayHeader => string.IsNullOrWhiteSpace(Header) ? Title.Replace("\n", " ") : Header.Replace("\n", " ");
}


public sealed class HypergryphBulletinContent
{
    /// <summary>
    /// 富文本正文（displayType = rich_text）
    /// </summary>
    [JsonPropertyName("html")]
    public string Html { get; set; } = "";

    /// <summary>
    /// 图片地址（displayType = picture）
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("link")]
    public string Link { get; set; } = "";

    [JsonPropertyName("linkType")]
    public int LinkType { get; set; }
}
