using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Core.Hypergryph;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.Graphics;
using Windows.System;

namespace Starward.Features.GameLauncher;

/// <summary>
/// 鹰角游戏的游戏内公告窗口。直接加载游戏内公告的 H5 页面（ef-webview 的 game_bulletin），
/// 渲染出来与游戏内公告界面一致。
/// </summary>
public sealed partial class HypergryphGameNoticeWindow : WindowEx
{


    /// <summary>
    /// 游戏内公告页面。终末地用 ef-webview 的 game_bulletin，明日方舟用 ak-webview 的根页面。
    /// 实测都无需 u8_token，公告列表和正文都是匿名接口。
    /// </summary>
    private string BulletinPageUrl => HypergryphGameConstants.IsArknights(CurrentGameId.GameBiz)
        ? HypergryphGameConstants.ArknightsBulletinPageUrl
        : "https://ef-webview.hypergryph.com/page/game_bulletin?platform=Windows&lang=zh-cn";


    /// <summary>
    /// 游戏内公告页面依赖 Qt WebChannel（qt.webChannelTransport + QWebChannel）与 HGWebPC 的 UA。
    /// 这里用 stub 模拟 Qt 桥，把页面的 close 消息转发到 WebView2 宿主。
    /// </summary>
    private const string NoticeScript = """
        (function () {
            var stub = "window.QWebChannel = function (transport, callback) { var bridge = { postMessage: function (data) { try { chrome.webview.postMessage(data); } catch (e) { } } }; setTimeout(function () { callback({ objects: { __JSBridge__: bridge } }); }, 0); };";
            var dataUrl = 'data:text/javascript,' + encodeURIComponent(stub);
            var origAppend = Node.prototype.appendChild;
            Node.prototype.appendChild = function (node) {
                try {
                    if (node && node.tagName === 'SCRIPT' && typeof node.src === 'string' && node.src.indexOf('qrc:///qtwebchannel/qwebchannel.js') === 0) {
                        node.src = dataUrl;
                    }
                } catch (e) { }
                return origAppend.call(this, node);
            };
            window.qt = window.qt || { webChannelTransport: {} };
            var style = document.createElement('style');
            style.textContent = 'html,body{background:#000 !important;margin:0 !important;padding:0 !important;}#root{margin:0 !important;padding:0 !important;background:transparent !important;}#root > div{margin:0 !important;padding:0 !important;border-radius:0 !important;}.ziE87l .HW7qtE{width:100% !important;height:100% !important;margin:0 !important;border-radius:0 !important;}';
            var applyStyle = function () { try { (document.head || document.documentElement).appendChild(style); } catch (e) { } };
            if (document.head) { applyStyle(); } else { document.addEventListener('DOMContentLoaded', applyStyle); }
            window.close = function () { try { chrome.webview.postMessage({ type: 'close' }); } catch (e) { } };
            document.addEventListener('keydown', function (e) {
                if (e.key === 'Escape') { try { chrome.webview.postMessage({ type: 'close' }); } catch (e2) { } }
            });
            document.addEventListener('click', function (e) {
                var el = e.target;
                while (el && el !== document.body) {
                    if (el.classList && el.classList.contains('NDOlBp')) {
                        try { chrome.webview.postMessage({ type: 'close' }); } catch (e2) { }
                        return;
                    }
                    el = el.parentElement;
                }
            }, true);
        })();
        """;

    private readonly ILogger<HypergryphGameNoticeWindow> _logger = AppConfig.GetLogger<HypergryphGameNoticeWindow>();


    public GameId CurrentGameId { get; set; }


    public nint ParentWindowHandle { get; set; }



    public HypergryphGameNoticeWindow()
    {
        this.InitializeComponent();
        SystemBackdrop = new TransparentBackdrop(fillBlack: false);
        InitializeWindow();
    }



    private void InitializeWindow()
    {
        try
        {
            Title = "游戏公告";
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.TitleBar.SetDragRectangles([new RectInt32(0, 0, 0, 0)]);
            AdaptTitleBarButtonColorToActuallTheme();
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }
            nint style = User32.GetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE);
            style &= ~(nint)(User32.WindowStyles.WS_CAPTION | User32.WindowStyles.WS_SYSMENU | User32.WindowStyles.WS_THICKFRAME | User32.WindowStyles.WS_DLGFRAME);
            User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_STYLE, style);
            User32.SetWindowPos(WindowHandle, nint.Zero, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOZORDER | User32.SetWindowPosFlags.SWP_FRAMECHANGED);

        }
        catch { }
    }



    public new void Activate()
    {
        try
        {
            AppWindow? appWindow = AppWindow.GetFromWindowId(new WindowId((ulong)ParentWindowHandle));
            if (appWindow is null)
            {
                Close();
                return;
            }
            var pos = appWindow.Position;
            var size = appWindow.Size;
            int edge = (int)(8 * UIScale);
            AppWindow.MoveAndResize(new RectInt32(pos.X + edge, pos.Y, size.Width - 2 * edge, size.Height - edge));
            User32.SetWindowLong(WindowHandle, User32.WindowLongFlags.GWL_HWNDPARENT, ParentWindowHandle);
        }
        catch { }
        base.Activate();
        // 窗口一创建就隐藏，等页面加载完成后再显示，避免出现黑色过渡窗口。
        AppWindow.Hide();
    }



    protected override nint InputSiteSubclassProc(HWND hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData)
    {
        if (uMsg == (uint)User32.WindowMessage.WM_KEYDOWN)
        {
            if ((VirtualKey)wParam == VirtualKey.Escape)
            {
                Close();
                return 0;
            }
        }
        return base.InputSiteSubclassProc(hWnd, uMsg, wParam, lParam, uIdSubclass, dwRefData);
    }



    private async void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        if (ParentWindowHandle is 0)
        {
            Close();
            return;
        }
        await LoadBulletinsAsync();
    }



    private async Task LoadBulletinsAsync()
    {
        try
        {
            await WebView_Notice.EnsureCoreWebView2Async();
            WebView_Notice.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 6.2; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) QtWebEngine/5.15.8 Chrome/87.0.4280.144 Safari/537.36 PC/WIN/HGSDK HGWebPC/1.30.1";
            WebView_Notice.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            await WebView_Notice.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(NoticeScript);
            WebView_Notice.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    ShowEmpty();
                    Show();
                    return;
                }
                Border_Loading.Visibility = Visibility.Collapsed;
                ProgressRing_Loading.IsActive = false;
                WebView_Notice.Visibility = Visibility.Visible;
                await Task.Delay(300);
                Show();
            };
            WebView_Notice.Source = new Uri(BulletinPageUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load Hypergryph bulletin page");
            ShowEmpty();
        }
    }



    private void ShowEmpty()
    {
        Border_Loading.Visibility = Visibility.Collapsed;
        ProgressRing_Loading.IsActive = false;
        TextBlock_Empty.Visibility = Visibility.Visible;
    }



    private void CoreWebView2_WebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            string json = args.WebMessageAsJson;
            _logger.LogInformation("WebMessage: {Json}", json);
            // 页面通过 Qt 桥发送 {"type":"close"}；"close_button" 是点击事件，不算关闭。
            if (json.Contains("close", StringComparison.OrdinalIgnoreCase) && !json.Contains("close_button", StringComparison.OrdinalIgnoreCase))
            {
                Close();
            }
        }
        catch { }
    }



    private void Button_Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }


}