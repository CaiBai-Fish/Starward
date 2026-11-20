namespace Starward.Core.Yostar;

/// <summary>
/// 明日方舟国际服（Yostar 启动器）常量。
/// </summary>
public static class YostarGameConstants
{
    public const string ApiBaseUrl = "https://api-launcher-en.yo-star.com";

    /// <summary>
    /// 启动器标识，参与请求签名。
    /// </summary>
    public const string GameTag = "Arknights_EN";

    /// <summary>
    /// 签名头中声明的启动器版本。
    /// </summary>
    public const string LauncherVersion = "1.8.1";

    /// <summary>
    /// 签名盐值，硬编码在启动器内。
    /// </summary>
    public const string AuthSalt = "DE7108E9B2842FD460F4777702727869";

    public const string RootDirectoryName = "YostarGames";

    public const string GameDirectoryName = "Arknights_EN";

    public const string StartExeName = "Arknights";

    public const string ManifestFileName = "manifest.json";

    public const string ConfigFileName = "game-launcher-config.json";

    public const string Icon = "ms-appx:///Assets/Image/icon_arknights.png";

    public const string Background = "ms-appx:///Assets/Image/background_arknights.jpg";

    public const string ListLogo = "ms-appx:///Assets/Image/icon_arknights_logo.png";
}
