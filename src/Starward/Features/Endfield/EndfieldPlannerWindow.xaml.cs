using Microsoft.Extensions.Logging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Starward.Frameworks;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.System;

namespace Starward.Features.Endfield;

/// <summary>
/// 终末地规划器（CEP, https://end.canmoe.com）内嵌窗口。
/// CEP 为纯静态站点，页面内的账号与云同步均由站点自身的后端完成，
/// 启动器只负责承载 WebView2 与传递界面语言。
/// </summary>
public sealed partial class EndfieldPlannerWindow : WindowEx
{

    private const string PlannerHost = "https://end.canmoe.com";

    private readonly ILogger<EndfieldPlannerWindow> _logger = AppConfig.GetLogger<EndfieldPlannerWindow>();


    public EndfieldPlannerWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
    }



    private void InitializeWindow()
    {
        Title = Lang.EndfieldPlannerWindow_Title;
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        RootGrid.RequestedTheme = ShouldAppsUseDarkMode() ? ElementTheme.Dark : ElementTheme.Light;
        SystemBackdrop = new MicaBackdrop();
        AdaptTitleBarButtonColorToActuallTheme();
        SetDragRectangles(new RectInt32(0, 0, 100000, (int)(48 * UIScale)));
        CenterInScreen(1280, 800);
        SetIcon();
    }




    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }




    private async Task InitializeWebViewAsync()
    {
        try
        {
            ShowLoading();
            await webview.EnsureCoreWebView2Async();
            CoreWebView2 core = webview.CoreWebView2;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += Core_NewWindowRequested;
            core.NavigationCompleted += Core_NavigationCompleted;
            core.ProcessFailed += Core_ProcessFailed;
            webview.Source = new Uri(GetPlannerUrl());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize Endfield planner web view");
            ShowError(ex.Message);
        }
    }




    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        try
        {
            args.Handled = true;
            _ = Launcher.LaunchUriAsync(new Uri(args.Uri));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open external url {Uri}", args.Uri);
        }
    }




    private void Core_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            ShowWebView();
        }
        else
        {
            ShowError(args.WebErrorStatus.ToString());
        }
    }




    private void Core_ProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        ShowError(args.ProcessFailedKind.ToString());
    }




    private void Button_Retry_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (webview.CoreWebView2 is null)
            {
                _ = InitializeWebViewAsync();
            }
            else
            {
                ShowLoading();
                webview.CoreWebView2.Navigate(GetPlannerUrl());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reload Endfield planner");
            ShowError(ex.Message);
        }
    }




    private async void Button_OpenInBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Launcher.LaunchUriAsync(new Uri(GetPlannerUrl()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open Endfield planner in browser");
        }
    }




    private void ShowLoading()
    {
        StackPanel_Loading.Visibility = Visibility.Visible;
        StackPanel_Error.Visibility = Visibility.Collapsed;
        webview.Visibility = Visibility.Collapsed;
    }




    private void ShowWebView()
    {
        StackPanel_Loading.Visibility = Visibility.Collapsed;
        StackPanel_Error.Visibility = Visibility.Collapsed;
        webview.Visibility = Visibility.Visible;
    }




    private void ShowError(string? message)
    {
        StackPanel_Loading.Visibility = Visibility.Collapsed;
        webview.Visibility = Visibility.Collapsed;
        StackPanel_Error.Visibility = Visibility.Visible;
        TextBlock_ErrorMessage.Text = message ?? string.Empty;
    }




    /// <summary>
    /// CEP 支持的界面语言为 zh-CN / zh-TW / ja / en。
    /// </summary>
    private static string GetPlannerUrl()
    {
        string language = CultureInfo.CurrentUICulture.Name;
        string locale = language switch
        {
            _ when language.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)
                || language.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase) => "zh-TW",
            _ when language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) => "ja",
            _ when language.StartsWith("en", StringComparison.OrdinalIgnoreCase) => "en",
            _ => "zh-CN",
        };
        return $"{PlannerHost}/{locale}";
    }




}
