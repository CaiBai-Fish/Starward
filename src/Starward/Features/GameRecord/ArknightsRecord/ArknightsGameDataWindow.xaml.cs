using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Starward.Frameworks;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Graphics;

namespace Starward.Features.GameRecord.ArknightsRecord;

public sealed partial class ArknightsGameDataWindow : WindowEx
{
    private const string GameDataUrl = "https://game.skland.com/arknights/game-data?header=0&hg_media=skland";

    private const string CacheKey = "/arknights/game-data@arknights_game_data";

    private static ArknightsGameDataWindow? _instance;

    private CoreWebView2? _core;

    private bool _pendingUpdateFingerprint;


    public ArknightsGameDataWindow()
    {
        InitializeComponent();
        Title = "明日方舟战绩";
        CenterInScreen();
        _instance = this;
        Closed += (_, _) =>
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        };
    }


    public static void ShowWindow()
    {
        if (_instance?.AppWindow is null)
        {
            _instance = new ArknightsGameDataWindow();
        }
        _instance.Activate();
    }


    public static async void UpdateDeviceFingerprint()
    {
        if (_instance?.AppWindow is null)
        {
            _instance = new ArknightsGameDataWindow();
        }
        _instance._pendingUpdateFingerprint = true;
        _instance.Activate();
        if (_instance._core is not null)
        {
            await _instance.ApplyDeviceFingerprintAsync();
        }
    }


    private void CenterInScreen()
    {
        RectInt32 workArea = DisplayArea.GetFromWindowId(MainWindowId, DisplayAreaFallback.Nearest).WorkArea;
        int h = (int)(workArea.Height * 0.95);
        int w = (int)(h / 16.0 * 9.0);
        if (w > workArea.Width)
        {
            w = (int)(workArea.Width * 0.95);
            h = (int)(w * 16.0 / 9.0);
        }
        int x = workArea.X + (workArea.Width - w) / 2;
        int y = workArea.Y + (workArea.Height - h) / 2;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }


    private async void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView_GameData.EnsureCoreWebView2Async();
            CoreWebView2 core = WebView_GameData.CoreWebView2;
            _core = core;
            core.Settings.UserAgent = "Mozilla/5.0 (Linux; Android 14; Redmi 7 Build/AP2A.240905.003; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/138.0.7204.63 Mobile Safari/537.36; SKLand/2.3.0";
            string folder = AppConfig.UserDataFolder ?? AppContext.BaseDirectory;
            string snapshot = Path.Combine(folder, "ArknightsGameData.html");
            if (File.Exists(snapshot))
            {
                WebView_GameData.Visibility = Visibility.Visible;
                core.Navigate(new Uri(snapshot).AbsoluteUri);
                if (_pendingUpdateFingerprint)
                {
                    await ApplyDeviceFingerprintAsync();
                }
                return;
            }
            string? cacheScript = BuildCacheScript();
            if (cacheScript is not null)
            {
                await core.AddScriptToExecuteOnDocumentCreatedAsync(cacheScript);
            }
            WebView_GameData.Visibility = Visibility.Visible;
            core.Navigate(GameDataUrl);
        }
        catch
        {
        }
    }


    private async Task ApplyDeviceFingerprintAsync()
    {
        if (_core is null)
        {
            return;
        }
        _pendingUpdateFingerprint = false;
        const string script = """
            (() => {
              const uuid = () => crypto.randomUUID();
              const id = uuid().replace(/-/g, '');
              localStorage.setItem('#deviceIDS', id);
              localStorage.setItem('hg_sdk_device_id', id + Math.random().toString(16).slice(2));
              localStorage.setItem('#eventLogDeviceId', id);
              document.cookie = 'hg_sdk_track_id=' + uuid() + '; path=/; domain=.skland.com';
              return true;
            })()
            """;
        await _core.ExecuteScriptAsync(script);
        _core.Reload();
    }


    private static string? BuildCacheScript()
    {
        try
        {
            string folder = AppConfig.UserDataFolder ?? AppContext.BaseDirectory;
            string path = Path.Combine(folder, "ArknightsGameData.json");
            if (!File.Exists(path))
            {
                return null;
            }
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            string literal = JsonSerializer.Serialize(json);
            string hgId = "";
            string metaPath = Path.Combine(folder, "ArknightsGameDataMeta.json");
            if (File.Exists(metaPath))
            {
                using JsonDocument meta = JsonDocument.Parse(File.ReadAllText(metaPath));
                if (meta.RootElement.TryGetProperty("hgId", out JsonElement hgIdElement))
                {
                    hgId = hgIdElement.GetString() ?? "";
                }
            }
            string hgInfo = hgId.Length > 0 ? $"localStorage.setItem('HG_INFO_KEY', {JsonSerializer.Serialize($"{{\"hgId\":\"{hgId}\"}}")});document.cookie='HG_INFO_KEY='+encodeURIComponent({JsonSerializer.Serialize($"{{\"hgId\":\"{hgId}\"}}")})+'; path=/; domain=.skland.com';" : "";
            return $"try{{localStorage.setItem('{CacheKey}', {literal});{hgInfo}}}catch(e){{}}";
        }
        catch
        {
            return null;
        }
    }

}
