using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using Starward.Features.Database;
using Starward.Features.GameLauncher;
using Starward.Features.ViewHost;
using System;
using System.Threading;
using System.Threading.Tasks;


namespace Starward.Features.PlayTime;

[INotifyPropertyChanged]
public sealed partial class PlayTimeButton : UserControl
{


    private GameBiz _currentGameBiz;

    private bool _isLoaded;

    /// <summary>
    /// 当前游戏。切换游戏时立即刷新已记录的游玩时长。
    /// </summary>
    public GameBiz CurrentGameBiz
    {
        get => _currentGameBiz;
        set
        {
            if (_currentGameBiz == value)
            {
                return;
            }
            _currentGameBiz = value;
            if (_isLoaded)
            {
                InitializePlayTime();
            }
        }
    }


    private readonly ILogger<PlayTimeButton> _logger = AppConfig.GetLogger<PlayTimeButton>();


    private long _lastActivatedRefreshTick;



    public PlayTimeButton()
    {
        this.InitializeComponent();
    }



    public TimeSpan PlayTimeTotal { get; set => SetProperty(ref field, value); }



    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        InitializePlayTime();
        WeakReferenceMessenger.Default.Register<GameExitedMessage>(this, OnGameExited);
        if (MainWindow.Current is MainWindow mainWindow)
        {
            mainWindow.Activated += MainWindow_Activated;
        }
    }



    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        if (MainWindow.Current is MainWindow mainWindow)
        {
            mainWindow.Activated -= MainWindow_Activated;
        }
    }



    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        try
        {
            if (args.WindowActivationState is WindowActivationState.Deactivated)
            {
                return;
            }
            // 窗口重新获得焦点时刷新一次，避免每次切换窗口都查数据库
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastActivatedRefreshTick);
            if (now - last < 2000)
            {
                return;
            }
            Interlocked.Exchange(ref _lastActivatedRefreshTick, now);
            InitializePlayTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh play time when window activated");
        }
    }



    /// <summary>
    /// 游戏进程退出后刷新已记录的游玩时长。
    /// 记录进程需要 1 秒左右才会把统计数据写回数据库，因此这里轮询等待。
    /// </summary>
    private void OnGameExited(object recipient, GameExitedMessage message)
    {
        _ = RefreshPlayTimeAfterGameExitedAsync(message.GameBiz);
    }



    /// <summary>
    /// 游戏退出后刷新游玩时长（可由调用方在检测到游戏进程退出时主动触发）。
    /// </summary>
    public void RefreshPlayTime()
    {
        _ = RefreshPlayTimeAfterGameExitedAsync(CurrentGameBiz);
    }



    private async Task RefreshPlayTimeAfterGameExitedAsync(GameBiz gameBiz)
    {
        try
        {
            if (NormalizeGameBiz(gameBiz) != NormalizeGameBiz(CurrentGameBiz))
            {
                return;
            }
            TimeSpan previous = PlayTimeTotal;
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(500);
                TimeSpan total = ReadPlayTimeTotal(CurrentGameBiz);
                if (total > previous)
                {
                    SetPlayTimeTotal(total);
                    _logger.LogInformation("Play time refreshed after game exited ({GameBiz}): {previous} -> {total}", gameBiz, previous, total);
                    return;
                }
            }
            // 记录进程没有及时写回时，直接用数据库中的记录重新统计
            TimeSpan computed = AppConfig.GetService<PlayTimeStatsService>().GetPlayTimeTotal(NormalizeGameBiz(CurrentGameBiz));
            if (computed > previous)
            {
                SetPlayTimeTotal(computed);
                _logger.LogInformation("Play time recomputed after game exited ({GameBiz}): {previous} -> {total}", gameBiz, previous, computed);
            }
            else
            {
                _logger.LogInformation("Play time not changed after game exited ({GameBiz}): {total}", gameBiz, previous);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh play time after game exited ({GameBiz})", gameBiz);
        }
    }



    private void SetPlayTimeTotal(TimeSpan total)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            PlayTimeTotal = total;
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => PlayTimeTotal = total);
        }
    }



    private void InitializePlayTime()
    {
        try
        {
            PlayTimeTotal = ReadPlayTimeTotal(CurrentGameBiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize play time");
        }
    }



    private static TimeSpan ReadPlayTimeTotal(GameBiz gameBiz)
    {
        return DatabaseService.GetValue<TimeSpan>($"playtime_total_{NormalizeGameBiz(gameBiz)}", out _);
    }



    private static GameBiz NormalizeGameBiz(GameBiz gameBiz)
    {
        return gameBiz.IsBilibili() ? $"{gameBiz.Game}_cn" : gameBiz;
    }



    public static string TimeSpanToString(TimeSpan timeSpan)
    {
        int totalMinutes = (int)Math.Round(timeSpan.TotalMinutes);
        int hours = totalMinutes / 60, minutes = totalMinutes % 60;
        return $"{hours}h {minutes}m";
    }



    [RelayCommand]
    private async Task OpenStatsDialogAsync()
    {
        await new PlayTimeStatsDialog
        {
            CurrentGameBiz = CurrentGameBiz,
            XamlRoot = this.XamlRoot,
        }.ShowAsync();
        InitializePlayTime();
    }


}
