namespace Starward.Core.Manjuu;

/// <summary>
/// 蛮啾网络（Manjuu）游戏渠道配置。
/// 数据来自官方启动器 res/app_config.dat 与游戏版本接口。
/// </summary>
public sealed record ManjuuGameProfile(
    GameBiz GameBiz,
    string GameId,
    string AppId,
    string ChannelCode,
    string SubChannelCode,
    string LauncherId,
    string SecretKey,
    string ApiBaseUrl,
    string ApiBackupBaseUrl,
    string CdnBaseUrl,
    string CdnBackupBaseUrl,
    string GameFolderName,
    string ExeName,
    string InstallationDirectory,
    string PublisherDirectory,
    string Icon,
    string Background,
    IReadOnlyList<string> RelatedProcesses,
    IReadOnlyList<ManjuuExcludedFile> ExcludedFiles);


/// <summary>
/// 安装/校验时排除的文件或目录（与官方启动器 gameExcludedFileList 一致）。
/// </summary>
public sealed record ManjuuExcludedFile(int FileType, string FilePath);


public static class ManjuuGameConstants
{
    /// <summary>
    /// 启动器接口主域名。
    /// </summary>
    public const string ApiBaseUrl = "https://api-grp.manjuu.com";

    /// <summary>
    /// 启动器接口备用域名。
    /// </summary>
    public const string ApiBackupBaseUrl = "https://api-grp-l2.manjuu.com";

    /// <summary>
    /// 文件 CDN 主域名。
    /// </summary>
    public const string CdnBaseUrl = "https://syncstation.manjuu.com/";

    /// <summary>
    /// 文件 CDN 备用域名。
    /// </summary>
    public const string CdnBackupBaseUrl = "https://l2-syncstation.manjuu.com/";

    /// <summary>
    /// 启动器 launcherId（C000 官服渠道）。
    /// </summary>
    public const string LauncherId = "93cdd43e096046138053253dc56d3562";

    /// <summary>
    /// 启动器 appId（与游戏内 SDK 的 353 不同）。
    /// </summary>
    public const string LauncherAppId = "354";

    /// <summary>
    /// 渠道与子渠道。
    /// </summary>
    public const string ChannelCode = "C000";

    public const string SubChannelCode = "SC000";

    /// <summary>
    /// 接口签名密钥，来自官方启动器 res/app_config.dat。
    /// </summary>
    public const string SecretKey = "20bccb31-fb57-4683-b6d8-6314d43811f2";

    /// <summary>
    /// 版本索引解密密钥（AES-192-CBC，24 字节 ASCII）。
    /// </summary>
    public const string IndexAesKey = "mJ9dH2bR7fT1zW4eQ6cY8a==";

    /// <summary>
    /// 版本索引解密 IV（16 字节 ASCII）。
    /// </summary>
    public const string IndexAesIV = "aP3sD8fG0hJ6kL9z";

    /// <summary>
    /// 游戏 ID，注册表键与接口参数使用。
    /// </summary>
    public const string GameId = "35";

    /// <summary>
    /// 官方启动器安装目录名。
    /// </summary>
    public const string GameFolderName = "AzurPromilia_Game";

    /// <summary>
    /// 游戏主程序。
    /// </summary>
    public const string ExeName = "AzurPromilia.exe";

    /// <summary>
    /// 用于展示的启动器版本号（请求头字段，非关键）。
    /// </summary>
    public const string LauncherVersion = "1.0.1.1";

    /// <summary>
    /// 游戏启动参数，缺少时游戏提示“请使用启动器启动游戏”。
    /// </summary>
    public const string LaunchArgument = "-start=azurpromilia_launcher";

    public const string AzurPromiliaIcon = "ms-appx:///Assets/Image/icon_azurpromilia.png";

    public const string AzurPromiliaBackground = "ms-appx:///Assets/Image/background_azurpromilia.jpg";

    public const string AzurPromiliaListLogo = "ms-appx:///Assets/Image/icon_azurpromilia_logo.png";

    /// <summary>游戏页顶部显示的游戏 Logo（官方 game_logo_zh）。</summary>
    public const string AzurPromiliaGameLogo = "ms-appx:///Assets/Image/game_logo_azurpromilia.png";


    public static ManjuuGameProfile AzurPromiliaProfile { get; } = new(
        GameBiz.azurpromilia_cn,
        GameId,
        LauncherAppId,
        ChannelCode,
        SubChannelCode,
        LauncherId,
        SecretKey,
        ApiBaseUrl,
        ApiBackupBaseUrl,
        CdnBaseUrl,
        CdnBackupBaseUrl,
        GameFolderName,
        ExeName,
        GameFolderName,
        "Manjuu",
        AzurPromiliaIcon,
        AzurPromiliaBackground,
        [ExeName, "ACE-Service64.exe"],
        [
            new ManjuuExcludedFile(1, "CrashSightLog"),
            new ManjuuExcludedFile(1, "PersistentDataPath"),
            new ManjuuExcludedFile(2, "pcsdk_log.txt"),
            new ManjuuExcludedFile(2, "pcsdklni.ini"),
        ]);


    public static IReadOnlyList<ManjuuGameProfile> GameProfiles { get; } = [AzurPromiliaProfile];


    public static bool IsManjuuGame(GameBiz gameBiz) => gameBiz.Value is GameBiz.azurpromilia_cn;


    public static ManjuuGameProfile GetGameProfile(GameBiz gameBiz) => gameBiz.Value switch
    {
        GameBiz.azurpromilia_cn => AzurPromiliaProfile,
        _ => throw new ArgumentOutOfRangeException(nameof(gameBiz), gameBiz.Value, "Unsupported Manjuu game."),
    };


    /// <summary>
    /// 本地元数据与官方启动器共用的注册表路径。
    /// </summary>
    public static string GetRegistryPath(GameBiz gameBiz) => $@"HKEY_CURRENT_USER\SOFTWARE\AzurPromilia\{GetGameProfile(gameBiz).GameId}_{GetGameProfile(gameBiz).ChannelCode}";


    /// <summary>
    /// 接口签名：MD5(timestamp + queryString + utoken + appId + channelCode + secretKey)。
    /// </summary>
    public static string Sign(string timestamp, string queryString, string? utoken, string appId, string channelCode, string secretKey)
    {
        string source = string.Concat(timestamp, queryString, utoken ?? "", appId, channelCode, secretKey);
        return Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(source)));
    }
}
