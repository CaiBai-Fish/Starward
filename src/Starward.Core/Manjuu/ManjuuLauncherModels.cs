using System.Text.Json.Serialization;

namespace Starward.Core.Manjuu;

internal sealed class ManjuuResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }
}


/// <summary>
/// /pc-launcher/client/gameversion/config 返回的游戏版本信息。
/// </summary>
public sealed class ManjuuGameInfo
{
    [JsonPropertyName("released")]
    public ManjuuGameVersion? Released { get; set; }

    [JsonPropertyName("preload")]
    public ManjuuGameVersion? Preload { get; set; }

    [JsonPropertyName("downloadMode")]
    public int DownloadMode { get; set; }
}


public sealed class ManjuuGameVersion
{
    [JsonPropertyName("gameId")]
    public int GameId { get; set; }

    [JsonPropertyName("gameName")]
    public string? GameName { get; set; }

    [JsonPropertyName("gameFolderName")]
    public string? GameFolderName { get; set; }

    [JsonPropertyName("versionCode")]
    public string? VersionCode { get; set; }

    [JsonPropertyName("versionName")]
    public string? VersionName { get; set; }

    [JsonPropertyName("launchEntry")]
    public string? LaunchEntry { get; set; }

    [JsonPropertyName("packageSize")]
    public long PackageSize { get; set; }

    [JsonPropertyName("packageInstallSize")]
    public long PackageInstallSize { get; set; }

    [JsonPropertyName("gamePackageList")]
    public List<ManjuuGamePackage>? GamePackageList { get; set; }

    [JsonPropertyName("gameExcludedFileList")]
    public List<ManjuuGameExcludedFile>? GameExcludedFileList { get; set; }

    [JsonPropertyName("domainList")]
    public List<string>? DomainList { get; set; }
}


/// <summary>
/// /pc-launcher/client/gameversion/packageconfig 返回的下载配置。
/// </summary>
public sealed class ManjuuPackageConfig
{
    [JsonPropertyName("downloadMode")]
    public int DownloadMode { get; set; }

    [JsonPropertyName("released")]
    public ManjuuGameVersion? Released { get; set; }

    [JsonPropertyName("preload")]
    public ManjuuGameVersion? Preload { get; set; }
}


public sealed class ManjuuGamePackage
{
    /// <summary>
    /// 1 = 分片整包（SLICE），2 = 文件级索引（REALITY）。
    /// </summary>
    [JsonPropertyName("packageType")]
    public int PackageType { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("fileMd5")]
    public string? FileMd5 { get; set; }

    [JsonPropertyName("cdnBaseUrls")]
    public List<string>? CdnBaseUrls { get; set; }
}


public sealed class ManjuuGameExcludedFile
{
    /// <summary>
    /// 1 = 目录，2 = 文件。
    /// </summary>
    [JsonPropertyName("fileType")]
    public int FileType { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}


/// <summary>
/// 解密后的版本索引条目。
/// </summary>
public sealed class ManjuuIndexEntry
{
    public string Name { get; set; } = "";

    public string MD5 { get; set; } = "";

    public long Size { get; set; }

    /// <summary>
    /// 相对 CDN 根地址的下载路径。
    /// </summary>
    public string RemotePath { get; set; } = "";
}


/// <summary>
/// 解密后的完整索引。
/// </summary>
public sealed class ManjuuGameIndex
{
    public List<ManjuuIndexEntry> Files { get; } = [];

    public List<string> Directories { get; } = [];

    /// <summary>
    /// 分片整包（SLICE 模式）的合并大小，0 表示不是整包索引。
    /// </summary>
    public long PackageSize { get; set; }

    /// <summary>
    /// 分片整包（SLICE 模式）的整包 MD5。
    /// </summary>
    public string? PackageMD5 { get; set; }
}

/// <summary>
/// /pc-launcher/client/launcher/contentconfig 返回的背景、轮播、资讯与社交配置。
/// </summary>
public sealed class ManjuuLauncherContent
{
    [JsonPropertyName("mediaContentMap")]
    public Dictionary<string, List<ManjuuMediaItem>> MediaContentMap { get; set; } = [];

    [JsonPropertyName("txtContentMap")]
    public Dictionary<string, List<ManjuuTextItem>> TxtContentMap { get; set; } = [];
}


public sealed class ManjuuMediaItem
{
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("video")]
    public string? Video { get; set; }

    [JsonPropertyName("linkUrl")]
    public string? LinkUrl { get; set; }

    [JsonPropertyName("showButton")]
    public int ShowButton { get; set; }

    [JsonPropertyName("buttonColor")]
    public int ButtonColor { get; set; }

    [JsonPropertyName("buttonDesc")]
    public string? ButtonDesc { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("sort")]
    public int Sort { get; set; }
}


public sealed class ManjuuTextItem
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("linkUrl")]
    public string? LinkUrl { get; set; }

    [JsonPropertyName("publishDate")]
    public string? PublishDate { get; set; }

    [JsonPropertyName("sort")]
    public int Sort { get; set; }
}
