using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Features.Gacha.Endfield;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.EndfieldToolbox;

public sealed partial class CrisisContractPage : PageBase
{

    private readonly Microsoft.Extensions.Logging.ILogger<CrisisContractPage> _logger = AppConfig.GetLogger<CrisisContractPage>();

    private readonly EndfieldRecordService _recordService = AppConfig.GetService<EndfieldRecordService>();

    private readonly EndfieldGachaService _gachaService = AppConfig.GetService<EndfieldGachaService>();

    private CancellationTokenSource? _cancellationTokenSource;

    private bool _initialized;

    private static readonly Dictionary<string, EndfieldCharView> EquipmentCache = [];

    private EndfieldGachaAccount? _account;


    public CrisisContractView? Record { get; set => SetProperty(ref field, value); }


    public CrisisContractPage()
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
                ShowMessage("请先在「森空岛工具箱」登录终末地账号，再查看危机合约战绩。", InfoBarSeverity.Warning);
                return;
            }
            using JsonDocument document = await _recordService.GetCrisisContractAsync(account, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            CrisisContractView parsed = EndfieldRecordParser.ParseCrisisContract(document.RootElement);
            await ApplyCharacterEquipmentAsync(parsed, cancellationToken);
            Record = parsed;
            ApplyBanner(parsed.BannerImage);
            Panel_Content.Visibility = Visibility.Visible;
            SelectTab(0);
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


    private async Task ApplyCharacterEquipmentAsync(CrisisContractView view, CancellationToken cancellationToken)
    {
        try
        {
            EndfieldGachaAccount? account = _account ?? _gachaService.GetAccounts().FirstOrDefault();
            if (account is null)
            {
                return;
            }

            List<EndfieldCharView> characters = view.Records
                .SelectMany(record => record.Chars)
                .Concat(view.BestRecord?.Chars ?? [])
                .Where(character => !string.IsNullOrWhiteSpace(character.CharId))
                .ToList();
            if (characters.Count == 0)
            {
                return;
            }

            List<Task> tasks = [];
            foreach (IGrouping<string, EndfieldCharView> group in characters.GroupBy(character => character.CharId))
            {
                string cacheKey = $"{account.AccountKey}:{group.Key}";
                if (EquipmentCache.TryGetValue(cacheKey, out EndfieldCharView? cached))
                {
                    foreach (EndfieldCharView character in group)
                    {
                        ApplyEquipment(character, cached);
                    }
                    continue;
                }
                tasks.Add(LoadEquipmentAsync(account, group.Key, cacheKey, group.ToList(), cancellationToken));
            }
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Endfield character equipment failed: {Message}", ex.Message);
        }
    }


    private async Task LoadEquipmentAsync(EndfieldGachaAccount account, string charId, string cacheKey,
        List<EndfieldCharView> targets, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument document = await _recordService.GetCharacterDetailAsync(account, charId, "charId", cancellationToken);
            EndfieldCharView? detail = EndfieldRecordParser.ParseCharacterEquipment(document.RootElement);
            if (detail is null)
            {
                return;
            }
            EquipmentCache[cacheKey] = detail;
            foreach (EndfieldCharView target in targets)
            {
                ApplyEquipment(target, detail);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Endfield character {CharId} equipment failed: {Message}", charId, ex.Message);
        }
    }


    private static void ApplyEquipment(EndfieldCharView target, EndfieldCharView source)
    {
        target.WeaponName = source.WeaponName;
        target.WeaponIcon = source.WeaponIcon;
        target.WeaponLevelText = source.WeaponLevelText;
        target.EquipSlots = source.EquipSlots;
        if (source.PotentialLevel > 0)
        {
            target.PotentialLevel = source.PotentialLevel;
            target.PotentialText = $"命座 {source.PotentialLevel}";
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


    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out int index))
        {
            SelectTab(index);
        }
    }


    private void SelectTab(int index)
    {
        Panel_History.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        Panel_Indicators.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        Panel_Overview.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

        Style selected = (Style)Application.Current.Resources["EndfieldTabButtonSelectedStyle"];
        Style normal = (Style)Application.Current.Resources["EndfieldTabButtonStyle"];
        Button_TabHistory.Style = index == 0 ? selected : normal;
        Button_TabIndicators.Style = index == 1 ? selected : normal;
        Button_TabOverview.Style = index == 2 ? selected : normal;
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

    private async void RecordCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CrisisContractRecordView record } || Record is null)
        {
            return;
        }
        CrisisRecordDetailDialog dialog = new() { XamlRoot = XamlRoot };
        await dialog.LoadAsync(record, _account, Record.Indicators);
        await dialog.ShowAsync();
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
