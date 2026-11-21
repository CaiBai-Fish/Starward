using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Starward.Features.Gacha.Endfield;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.EndfieldToolbox;

public sealed partial class UmbralMonumentPage : PageBase
{

    private readonly EndfieldRecordService _recordService = AppConfig.GetService<EndfieldRecordService>();

    private readonly EndfieldGachaService _gachaService = AppConfig.GetService<EndfieldGachaService>();

    private CancellationTokenSource? _cancellationTokenSource;

    private bool _initialized;

    private EndfieldGachaAccount? _account;

    private int _selectedIndex;

    private bool _showHard;


    public List<MonumentActivityView> Activities { get; set => SetProperty(ref field, value); } = [];


    public MonumentActivityView? SelectedActivity { get; set => SetProperty(ref field, value); }


    public ObservableCollection<MonumentStageView> Stages { get; } = [];


    public bool HasStageRecords { get; set => SetProperty(ref field, value); }


    public UmbralMonumentPage()
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
                ShowMessage("请先在「森空岛工具箱」登录终末地账号，再查看影拓丰碑战绩。", InfoBarSeverity.Warning);
                return;
            }
            using JsonDocument document = await _recordService.GetUmbralMonumentAsync(account, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            Activities = EndfieldRecordParser.ParseUmbralMonument(document.RootElement);
            if (Activities.Count == 0)
            {
                Panel_Content.Visibility = Visibility.Collapsed;
                ShowMessage("当前账号还没有影拓丰碑战绩。", InfoBarSeverity.Informational);
                return;
            }
            Panel_Content.Visibility = Visibility.Visible;
            SelectActivity(Math.Clamp(_selectedIndex, 0, Activities.Count - 1));
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


    private void Activity_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out int index))
        {
            SelectActivity(index);
        }
    }


    private void SelectActivity(int index)
    {
        _selectedIndex = index;
        Button[] buttons =
        [
            Button_Activity1, Button_Activity2, Button_Activity3, Button_Activity4,
            Button_Activity5, Button_Activity6, Button_Activity7, Button_Activity8,
        ];
        Style selected = (Style)Application.Current.Resources["EndfieldTabButtonSelectedStyle"];
        Style normal = (Style)Application.Current.Resources["EndfieldTabButtonStyle"];
        for (int i = 0; i < buttons.Length; i++)
        {
            MonumentActivityView? activity = Activities.ElementAtOrDefault(i);
            buttons[i].Visibility = activity is null ? Visibility.Collapsed : Visibility.Visible;
            buttons[i].Content = activity?.Name ?? "";
            buttons[i].Style = i == index ? selected : normal;
        }
        SelectedActivity = Activities.ElementAtOrDefault(index);
        RefreshStages();
    }


    private void ToggleSwitch_ShowHard_Toggled(object sender, RoutedEventArgs e)
    {
        _showHard = ToggleSwitch_ShowHard.IsOn;
        RefreshStages();
    }


    private void RefreshStages()
    {
        Stages.Clear();
        if (SelectedActivity is not null)
        {
            foreach (MonumentStageView stage in SelectedActivity.Stages)
            {
                if (!_showHard && stage.IsHard)
                {
                    continue;
                }
                Stages.Add(stage);
            }
        }
        HasStageRecords = Stages.Count > 0;
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
