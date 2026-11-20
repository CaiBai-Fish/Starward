using System;
using System.Collections.Generic;

namespace Starward.Core.Yostar;

public sealed class YostarGameConfig
{
    public string LowestVersion { get; set; } = "";

    public string LatestVersion { get; set; } = "";

    public string LatestFilePath { get; set; } = "";

    public string StartExeName { get; set; } = "";

    public List<string> StartParams { get; set; } = [];

    public string DecompressionSize { get; set; } = "";

    public string UninstallScript { get; set; } = "";


    public bool IsEmpty => string.IsNullOrWhiteSpace(LatestVersion);
}


public sealed class YostarCdnInfo
{
    public string PrimaryCdn { get; set; } = "";

    public string BackupCdn { get; set; } = "";
}


public sealed class YostarManifest
{
    public string Source { get; set; } = "";

    public List<YostarManifestFile> Files { get; set; } = [];
}


public sealed class YostarManifestFile
{
    public string Path { get; set; } = "";

    public string Hash { get; set; } = "";

    public long Size { get; set; }
}


public sealed class YostarNewsItem
{
    public string Title { get; set; } = "";

    public string Link { get; set; } = "";

    public string Thumbnail { get; set; } = "";

    public string TypeLabel { get; set; } = "";

    public DateTimeOffset PublishTime { get; set; }
}
