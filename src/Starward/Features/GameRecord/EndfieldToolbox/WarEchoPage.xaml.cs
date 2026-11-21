using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Features.Gacha.Endfield;
using Starward.Frameworks;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.EndfieldToolbox;

public sealed partial class WarEchoPage : PageBase
{

    private readonly EndfieldRecordService _recordService = AppConfig.GetService<EndfieldRecordService>();

    private readonly EndfieldGachaService _gachaService = AppConfig.GetService<EndfieldGachaService>();

    private CancellationTokenSource? _cancellationTokenSource;

    private bool _initialized;

    private EndfieldGachaAccount? _account;

    private int _selectedWeekIndex;


    public WarEchoView? Record { get; set => SetProperty(ref field, value); }


    public WarEchoWeekView? SelectedWeek { get; set => SetProperty(ref field, value); }


    public WarEchoPage()
    {
        InitializeComponent();
    }


    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is EndfieldGachaAccount account)
        {
            _account = account;
        }
    }


    protected override void OnLoaded()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        Grid_Root.SizeChanged += Grid_Root_SizeChanged;
        UpdateLayoutWidth();
        _ = LoadAsync();
    }


    protected override void OnUnloaded()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }


    public async Task RefreshAsync()
    {
        await LoadAsync();
    }


    private async Task LoadAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _cancellationTokenSource.Token;

        InfoBar_Message.IsOpen = false;
        ProgressRing_Loading.Visibility = Visibility.Visible;
        ProgressRing_Loading.IsActive = true;
        try
        {
            EndfieldGachaAccount? account = _account ?? _gachaService.GetAccounts().FirstOrDefault();
            if (account is null)
            {
                Panel_Content.Visibility = Visibility.Collapsed;
                ShowMessage("请先在「森空岛工具箱」登录终末地账号，再查看战争回响战绩。", InfoBarSeverity.Warning);
                return;
            }
            using JsonDocument document = await _recordService.GetWarEchoesAsync(account, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            Record = EndfieldRecordParser.ParseWarEchoes(document.RootElement).FirstOrDefault();
            if (Record is null)
            {
                Panel_Content.Visibility = Visibility.Collapsed;
                ShowMessage("当前赛季还没有战争回响数据。", InfoBarSeverity.Informational);
                return;
            }
            ApplyBanner(Record.HeaderImage);
            Panel_Content.Visibility = Visibility.Visible;
            int currentWeek = Record.Weeks.FindIndex(x => x.IsCurrent);
            SelectWeek(currentWeek >= 0 ? currentWeek : Math.Clamp(_selectedWeekIndex, 0, Math.Max(0, Record.Weeks.Count - 1)));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            ProgressRing_Loading.IsActive = false;
            ProgressRing_Loading.Visibility = Visibility.Collapsed;
        }
    }


    private void ApplyBanner(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Image_Banner.Source = null;
            return;
        }
        try
        {
            Image_Banner.Source = new BitmapImage(new Uri(url));
        }
        catch (UriFormatException)
        {
            Image_Banner.Source = null;
        }
    }


    private void Week_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out int index))
        {
            SelectWeek(index);
        }
    }


    private void SelectWeek(int index)
    {
        _selectedWeekIndex = index;
        Button[] buttons = [Button_Week1, Button_Week2, Button_Week3];
        Style selected = (Style)Application.Current.Resources["EndfieldTabButtonSelectedStyle"];
        Style normal = (Style)Application.Current.Resources["EndfieldTabButtonStyle"];
        for (int i = 0; i < buttons.Length; i++)
        {
            WarEchoWeekView? week = Record?.Weeks.ElementAtOrDefault(i);
            buttons[i].Visibility = week is null ? Visibility.Collapsed : Visibility.Visible;
            buttons[i].Content = week?.Name ?? "";
            buttons[i].Style = i == index ? selected : normal;
        }
        SelectedWeek = Record?.Weeks.ElementAtOrDefault(index);
    }


    private void Grid_Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyLayoutWidth(e.NewSize.Width);
    }


    private void UpdateLayoutWidth()
    {
        if (Grid_Root.ActualWidth > 0)
        {
            ApplyLayoutWidth(Grid_Root.ActualWidth);
        }
    }


    private void ApplyLayoutWidth(double width)
    {
        double contentWidth = Math.Min(width, 1280);
        Panel_Layout.Width = contentWidth;
        Panel_Layout.Margin = new Thickness(Math.Max(0, (width - contentWidth) / 2), 0, 0, 0);
    }

    private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }


    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        InfoBar_Message.Severity = severity;
        InfoBar_Message.Message = message;
        InfoBar_Message.IsOpen = true;
    }

}
