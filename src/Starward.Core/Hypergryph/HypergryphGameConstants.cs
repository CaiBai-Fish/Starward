namespace Starward.Core.Hypergryph;

public sealed record HypergryphGameProfile(
    GameBiz GameBiz,
    string LauncherAppCode,
    string GameAppCode,
    string Channel,
    string SubChannel,
    string BatchProxyUrl,
    string WebBatchProxyUrl,
    string ExeName,
    string InstallationDirectory,
    string PublisherDirectory,
    string Icon,
    string Background,
    IReadOnlyList<string> RelatedProcesses);

public static class HypergryphGameConstants
{
    public const string ChinaBatchProxyUrl = "https://launcher.hypergryph.com/api/proxy/batch_proxy";

    public const string ChinaWebBatchProxyUrl = "https://launcher.hypergryph.com/api/proxy/web/batch_proxy";

    public const string GlobalBatchProxyUrl = "https://launcher.gryphline.com/api/proxy/batch_proxy";

    public const string GlobalWebBatchProxyUrl = "https://launcher.gryphline.com/api/proxy/web/batch_proxy";

    public const string LauncherAppCode = "abYeZZ16BPluCFyT";

    public const string EndfieldAppCode = "6LL0KJuqHBVz33WK";

    public const string ArknightsAppCode = "GzD1CpaWgmSq1wew";

    public const string GlobalLauncherAppCode = "TiaytKBUIEdoEwRT";

    public const string GlobalEndfieldAppCode = "YDUTE5gscDZ229CW";

    public const string EndfieldTargetApp = "EndField";

    public const string Channel = "1";

    public const string SubChannel = "1";

    public const string EndfieldExeName = "Endfield.exe";

    public const string ArknightsExeName = "Arknights.exe";

    public const string EndfieldInstallationDirectory = "Endfield Game";

    public const string ArknightsInstallationDirectory = "Arknights";

    public const string GlobalEndfieldInstallationDirectory = "Arknights Endfield";

    // 游戏内公告（bulletin）接口
    public const string ChinaGameHubUrl = "https://game-hub.hypergryph.com";

    public const string GlobalGameHubUrl = "https://game-hub.gryphline.com";

    public const string EndfieldBulletinCodeCN = "endfield_5SD9TN";

    public const string EndfieldBulletinCodeOS = "endfield_U35PW8";

    public const int EndfieldBulletinChannelCN = 1;

    public const int EndfieldBulletinChannelOS = 6;

    public const int EndfieldBulletinServerOS = 3;

    /// <summary>
    /// 明日方舟的游戏内公告页面（ak-webview 的 H5，公开可访问）。
    /// </summary>
    public const string ArknightsBulletinPageUrl = "https://ak-webview.hypergryph.com/?target=Windows";

    public const string EndfieldBackground = "ms-appx:///Assets/Image/background_endfield.jpg";

    public const string EndfieldIcon = "ms-appx:///Assets/Image/icon_endfield.png";

    public const string ArknightsBackground = "ms-appx:///Assets/Image/background_arknights.jpg";

    public const string ArknightsIcon = "ms-appx:///Assets/Image/icon_arknights.png";

    /// <summary>
    /// 鹰角国服（森空岛）渠道角标。
    /// </summary>
    public const string SklandIcon = "ms-appx:///Assets/Image/icon_skland.png";

    /// <summary>
    /// 鹰角国际服（SKPORT）渠道角标。
    /// </summary>
    public const string SkportIcon = "ms-appx:///Assets/Image/icon_skport.png";

    public const string EndfieldListLogo = "ms-appx:///Assets/Image/icon_endfield_logo.png";

    public const string ArknightsListLogo = "ms-appx:///Assets/Image/icon_arknights_logo.png";

    public static HypergryphGameProfile ChinaArknightsProfile { get; } = new(
        GameBiz.arknights_cn,
        LauncherAppCode,
        ArknightsAppCode,
        Channel,
        SubChannel,
        ChinaBatchProxyUrl,
        ChinaWebBatchProxyUrl,
        ArknightsExeName,
        ArknightsInstallationDirectory,
        "Hypergryph",
        ArknightsIcon,
        ArknightsBackground,
        [ArknightsExeName, "PlatformProcess.exe"]);

    public static HypergryphGameProfile ChinaEndfieldProfile { get; } = new(
        GameBiz.endfield_cn,
        LauncherAppCode,
        EndfieldAppCode,
        Channel,
        SubChannel,
        ChinaBatchProxyUrl,
        ChinaWebBatchProxyUrl,
        EndfieldExeName,
        EndfieldInstallationDirectory,
        "Hypergryph",
        EndfieldIcon,
        EndfieldBackground,
        [EndfieldExeName, "PlatformProcess.exe"]);

    public static HypergryphGameProfile GlobalEndfieldProfile { get; } = new(
        GameBiz.endfield_global,
        GlobalLauncherAppCode,
        GlobalEndfieldAppCode,
        "6",
        "6",
        GlobalBatchProxyUrl,
        GlobalWebBatchProxyUrl,
        EndfieldExeName,
        GlobalEndfieldInstallationDirectory,
        "GRYPHLINK",
        EndfieldIcon,
        EndfieldBackground,
        [EndfieldExeName, "PlatformProcess.exe"]);

    public static IReadOnlyList<HypergryphGameProfile> GameProfiles { get; } =
    [
        ChinaArknightsProfile,
        ChinaEndfieldProfile,
        GlobalEndfieldProfile,
    ];

    public static bool IsHypergryphGame(GameBiz gameBiz) =>
        GameProfiles.Any(x => x.GameBiz == gameBiz);

    public static bool IsEndfield(GameBiz gameBiz) =>
        gameBiz.Value is GameBiz.endfield_cn or GameBiz.endfield_global;

    public static bool IsArknights(GameBiz gameBiz) => gameBiz.Value is GameBiz.arknights_cn;

    public static HypergryphGameProfile GetGameProfile(GameBiz gameBiz) => gameBiz.Value switch
    {
        GameBiz.arknights_cn => ChinaArknightsProfile,
        GameBiz.endfield_cn => ChinaEndfieldProfile,
        GameBiz.endfield_global => GlobalEndfieldProfile,
        _ => throw new ArgumentOutOfRangeException(nameof(gameBiz), gameBiz.Value, "Unsupported Hypergryph game."),
    };
}
