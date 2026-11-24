using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Core.Hypergryph;
using Starward.Core.Yostar;
using Starward.Features.HoYoPlay;
using Starward.Features.ViewHost;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.System;


namespace Starward.Features.GameLauncher;

[INotifyPropertyChanged]
public sealed partial class GameBannerAndPost : UserControl
{


    private Microsoft.UI.Dispatching.DispatcherQueueTimer _bannerTimer;


    private bool _updatingBannerItems;


    private readonly ILogger<GameBannerAndPost> _logger = AppConfig.GetLogger<GameBannerAndPost>();


    private readonly HoYoPlayService _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();


    private readonly GameNoticeService _gameNoticeService = AppConfig.GetService<GameNoticeService>();

    private readonly YostarGameClient _yostarGameClient = AppConfig.GetService<YostarGameClient>();


    public GameId CurrentGameId { get; set; }



    public GameBannerAndPost()
    {
        this.InitializeComponent();
        this.Loaded += GameBannerAndPost_Loaded;
        this.Unloaded += GameBannerAndPost_Unloaded;
        _bannerTimer = DispatcherQueue.CreateTimer();
        _bannerTimer.Interval = TimeSpan.FromSeconds(5);
        _bannerTimer.IsRepeating = true;
        _bannerTimer.Tick += _bannerTimer_Tick;
    }






    private async void GameBannerAndPost_Loaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<GameNoticeWindowClosedMessage>(this, OnGameNoticeWindowClosed);
        WeakReferenceMessenger.Default.Register<GameAnnouncementSettingChangedMessage>(this, OnGameAnnouncementSettingChanged);
        await UpdateGameContentAsync();
        await UpdateGameNoticeAlertAsync();
    }


    private void GameBannerAndPost_Unloaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _bannerTimer.Stop();
        _bannerTimer.Tick -= _bannerTimer_Tick;
        Banners = null;
        PostGroups = null;
    }



    private void OnMainWindowStateChanged(object _, MainWindowStateChangedMessage message)
    {
        try
        {
            if (message.Activate)
            {
                _bannerTimer.Start();
            }
            else if (message.Hide || message.SessionLock)
            {
                _bannerTimer.Stop();
            }
        }
        catch { }
    }



    private async void OnGameNoticeWindowClosed(object _, GameNoticeWindowClosedMessage message)
    {
        User32.SetForegroundWindow(XamlRoot.GetWindowHandle());
        await UpdateGameNoticeAlertAsync();
    }



    private async void OnGameAnnouncementSettingChanged(object _, GameAnnouncementSettingChangedMessage message)
    {
        // 没有设置取消，网络不好时可能会造成状态异常，懒得写了
        if (AppConfig.EnableBannerAndPost)
        {
            ShowBannerAndPost = true;
            await UpdateGameContentAsync();
            if (AppConfig.DisableGameNoticeRedHot)
            {
                IsGameNoticesAlert = false;
            }
            else
            {
                await UpdateGameNoticeAlertAsync();
            }
        }
        else
        {
            ShowBannerAndPost = false;
        }
    }



    public List<GameBanner>? Banners
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateBannerItems();
            }
        }
    }


    /// <summary>
    /// 展示给 FlipView 的公告列表。数据量大于 1 时，在首尾各插入一个哨兵项
    /// （最后一张、第一张），让手势滑动可以越过首尾，再由 SelectionChanged
    /// 无动画跳回真实项，效果与自动轮换的循环一致。
    /// </summary>
    public List<GameBanner>? BannerItems { get; set => SetProperty(ref field, value); }





    public List<GamePostGroup>? PostGroups { get; set => SetProperty(ref field, value); }





    public bool IsGameNoticesAlert { get; set => SetProperty(ref field, value); }




    public bool ShowBannerAndPost
    {
        get => this.Opacity == 1;
        set
        {
            if (value && Banners?.Count > 0 && PostGroups?.Count > 0)
            {
                _bannerTimer.Start();
                this.Opacity = 1;
                this.IsHitTestVisible = true;
            }
            else
            {
                _bannerTimer.Stop();
                this.Opacity = 0;
                this.IsHitTestVisible = false;
            }
        }
    }




    private async Task UpdateGameContentAsync()
    {
        try
        {
            GameContent? content = CurrentGameId.GameBiz == GameBiz.arknights_global
                ? await GetYostarContentAsync()
                : await _hoYoPlayService.GetGameContentAsync(CurrentGameId);
            if (content is null || !AppConfig.EnableBannerAndPost)
            {
                ShowBannerAndPost = false;
                return;
            }
            Banners = content.Banners;
            PostGroups = GamePostGroup.FromGameContent(content);
            ShowBannerAndPost = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game launcher content ({CurrentGameId})", CurrentGameId);
        }
    }


    /// <summary>
    /// 明日方舟国际服：把 Yostar 运营新闻转换成背景轮播与公告/资讯列表。
    /// </summary>
    private async Task<GameContent?> GetYostarContentAsync()
    {
        List<YostarNewsItem> news = await _yostarGameClient.GetNewsAsync();
        if (news.Count == 0)
        {
            return null;
        }
        // 同一新闻可能同时出现在 LATEST 与 event 分组，按 (分组, 链接) 去重，
        // 保留官方接口的“活动 / 公告 / 资讯”分组。
        news = news.GroupBy(x => (x.TypeLabel, x.Link)).Select(x => x.First()).ToList();
        string? backgroundUrl = null;
        try
        {
            backgroundUrl = await _yostarGameClient.GetLauncherBackgroundUrlAsync();
        }
        catch { }
        List<GameBanner> banners = news.Where(x => !string.IsNullOrWhiteSpace(x.Thumbnail))
                                       .GroupBy(x => x.Link).Select(x => x.First())
                                       .Take(5)
                                       .Select((x, i) => new GameBanner
                                       {
                                           Id = i.ToString(),
                                           Image = new GameImage { Url = x.Thumbnail, Link = x.Link },
                                       }).ToList();
        if (banners.Count == 0 && !string.IsNullOrWhiteSpace(backgroundUrl))
        {
            banners.Add(new GameBanner
            {
                Id = "0",
                Image = new GameImage { Url = backgroundUrl, Link = news[0].Link },
            });
        }
        List<GamePost> posts = news.Select((x, i) => new GamePost
        {
            Id = i.ToString(),
            Type = x.TypeLabel.Equals("event", StringComparison.OrdinalIgnoreCase)
                ? GamePostType.POST_TYPE_ACTIVITY
                : x.TypeLabel.Equals("LATEST", StringComparison.OrdinalIgnoreCase)
                    ? GamePostType.POST_TYPE_ANNOUNCE
                    : GamePostType.POST_TYPE_INFO,
            Title = x.Title,
            Link = x.Link,
            Date = x.PublishTime == default ? "" : x.PublishTime.ToLocalTime().ToString("MM/dd"),
        }).ToList();
        return new GameContent
        {
            GameId = CurrentGameId,
            Language = "",
            Banners = banners,
            Posts = posts,
            SocialMediaList = [],
        };
    }




    private async Task UpdateGameNoticeAlertAsync()
    {
        try
        {
            if (GameFeatureConfig.FromGameId(CurrentGameId).InGameNoticesWindow)
            {
                Button_InGameNotices.Visibility = Visibility.Visible;
            }
            else
            {
                Button_InGameNotices.Visibility = Visibility.Collapsed;
                return;
            }
            if (AppConfig.DisableGameNoticeRedHot || HypergryphGameConstants.IsHypergryphGame(CurrentGameId.GameBiz))
            {
                IsGameNoticesAlert = false;
            }
            else
            {
                IsGameNoticesAlert = await _gameNoticeService.IsNoticeAlertAsync(CurrentGameId.GameBiz);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game launcher content ({CurrentGameId})", CurrentGameId);
        }
    }




    private void _bannerTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        try
        {
            int count = Banners?.Count ?? 0;
            if (count < 2)
            {
                return;
            }
            // 走到底部的哨兵项后，SelectionChanged 会跳回第一张
            int index = FlipView_Banner.SelectedIndex + 1;
            FlipView_Banner.SelectedIndex = index > count + 1 ? 1 : index;
        }
        catch { }
    }



    private async void Image_Banner_Tapped(object sender, TappedRoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.DataContext is GameBanner banner)
            {
                await Launcher.LaunchUriAsync(new Uri(banner.Image.Link));
            }
        }
        catch { }
    }



    /// <summary>
    /// 隐藏 FilpView 中自动出现的翻页按键
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FlipView_Banner_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var grid = VisualTreeHelper.GetChild(FlipView_Banner, 0);
            if (grid != null)
            {
                var count = VisualTreeHelper.GetChildrenCount(grid);
                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(grid, i);
                        if (child is Button button)
                        {

                            button.IsHitTestVisible = false;
                            button.Opacity = 0;
                        }
                    }
                }
            }
        }
        catch { }
    }




    private void Grid_BannerContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _bannerTimer.Stop();
        Border_PipsPager.Visibility = Visibility.Visible;
    }



    private void Grid_BannerContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _bannerTimer.Start();
        Border_PipsPager.Visibility = Visibility.Collapsed;
    }






    [RelayCommand]
    private void OpenGameNoticeWindow()
    {
        try
        {
            nint parentWindowHandle = (nint)this.XamlRoot.ContentIslandEnvironment.AppWindowId.Value;
            if (HypergryphGameConstants.IsHypergryphGame(CurrentGameId.GameBiz))
            {
                new HypergryphGameNoticeWindow
                {
                    CurrentGameId = CurrentGameId,
                    ParentWindowHandle = parentWindowHandle,
                }.Activate();
            }
            else
            {
                new GameNoticeWindow
                {
                    CurrentGameBiz = CurrentGameId.GameBiz,
                    ParentWindowHandle = parentWindowHandle,
                }.Activate();
            }
        }
        catch { }
    }





    private void FlipView_Banner_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_updatingBannerItems)
            {
                return;
            }
            int count = Banners?.Count ?? 0;
            if (count < 2)
            {
                return;
            }
            int index = FlipView_Banner.SelectedIndex;
            if (index == 0)
            {
                // 表头哨兵是最后一张，跳到真正的最后一张（内容相同）
                JumpToBanner(count);
            }
            else if (index == count + 1)
            {
                // 表尾哨兵是第一张，跳到真正的第一张（内容相同）
                JumpToBanner(1);
            }
        }
        catch { }
    }


    /// <summary>
    /// 无动画跳到指定公告，避免跨越多页时出现明显的滑动过程。
    /// </summary>
    private void JumpToBanner(int index)
    {
        // 只设置选中项，由 FlipView 按目标项的真实位置滚动（保留过渡动画）
        FlipView_Banner.SelectedIndex = index;
    }





    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                return result;
            }
            if (FindChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }
        return null;
    }


    private void UpdateBannerItems()
    {
        _updatingBannerItems = true;
        try
        {
            List<GameBanner>? banners = Banners;
            if (banners is null || banners.Count == 0)
            {
                BannerItems = null;
                return;
            }
            if (banners.Count == 1)
            {
                // 只有一张公告时不需要哨兵，否则会重复出现同一张
                BannerItems = [banners[0]];
                return;
            }
            List<GameBanner> items = new(banners.Count + 2)
            {
                banners[^1],
            };
            items.AddRange(banners);
            items.Add(banners[0]);
            BannerItems = items;
            // 初始停在第一张真实公告，而不是表头哨兵（最后一张）
            FlipView_Banner.SelectedIndex = 1;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                try
                {
                    // 首尾哨兵跳转跨度大，扩大内部面板缓存，避免容器回收造成的闪屏
                    if (FindChild<ItemsStackPanel>(FlipView_Banner) is { } panel)
                    {
                        panel.CacheLength = 24;
                    }
                    if (FlipView_Banner.SelectedIndex != 1 && BannerItems is { Count: > 2 })
                    {
                        FlipView_Banner.SelectedIndex = 1;
                    }
                }
                catch { }
            });
        }
        catch { }
        finally
        {
            _updatingBannerItems = false;
        }
    }


    /// <summary>
    /// 把展示列表索引换算成公告列表中的序号（从 1 开始）。
    /// </summary>
    public static int GetBannerIndexNumber(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        int number = ((index - 1) % count + count) % count;
        return number + 1;
    }




}
