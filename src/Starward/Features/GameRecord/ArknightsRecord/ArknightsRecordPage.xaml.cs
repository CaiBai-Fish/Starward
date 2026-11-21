using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Gacha.Endfield;
using Starward.Features.GameRecord.DailyNote;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Starward.Features.GameRecord.ArknightsRecord;

public sealed class ArknightsRecordItemView
{
    public string Title { get; set; } = "";

    public string Subtitle { get; set; } = "";

    public string Value { get; set; } = "";

    public bool HasProgress { get; set; }

    public double ProgressValue { get; set; }

    public double ProgressMaximum { get; set; } = 1;
}


public sealed partial class ArknightsRecordPage : Page
{
    private readonly ArknightsRecordService _service = AppConfig.GetService<ArknightsRecordService>();

    private readonly ArknightsDailyNoteService _dailyNoteService = AppConfig.GetService<ArknightsDailyNoteService>();

    public GameId? CurrentGameId { get; set; }

    public ObservableCollection<ArknightsAccount> Accounts { get; } = [];

    public ArknightsRecordPage()
    {
        InitializeComponent();
    }


    private Dictionary<string, List<ArknightsRecordItemView>> _sections = [];


    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is GameId id)
        {
            CurrentGameId = id;
        }
        else if (e.Parameter is GameBiz biz)
        {
            CurrentGameId = GameId.FromGameBiz(biz);
        }
        _ = LoadRecordAsync();
    }


    private async Task LoadRecordAsync()
    {
        try
        {
            ProgressRing_Loading.IsActive = true;
            TextBlock_Error.Text = "";
            NavigationViewItem_EndfieldAccount.Visibility = Visibility.Collapsed;
            NavigationViewItem_EndfieldCrisis.Visibility = Visibility.Collapsed;
            NavigationViewItem_EndfieldWar.Visibility = Visibility.Collapsed;
            NavigationViewItem_EndfieldMonument.Visibility = Visibility.Collapsed;
            Frame_EndfieldAccount.Visibility = Visibility.Collapsed;
            ScrollViewer_Records.Visibility = Visibility.Visible;
            Border_EndfieldGuide.Visibility = Visibility.Collapsed;
            Items_Records.ItemsSource = null;
            Button_LoginArknights.Visibility = Visibility.Collapsed;
            Button_AccountList.Visibility = Visibility.Collapsed;
            foreach (NavigationViewItem item in NavigationView_Records.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string tag && tag is "garrison" or "rogue" or "activity" or "h5")
                {
                    item.Visibility = Visibility.Visible;
                }
            }
            if (CurrentGameId?.GameBiz.Game is GameBiz.endfield)
            {
                foreach (NavigationViewItem item in NavigationView_Records.MenuItems.OfType<NavigationViewItem>())
                {
                    if (item.Tag is string tag && tag is "garrison" or "rogue" or "activity" or "h5")
                    {
                        item.Visibility = Visibility.Collapsed;
                    }
                }
                NavigationViewItem_EndfieldAccount.Visibility = Visibility.Visible;
                NavigationViewItem_EndfieldCrisis.Visibility = Visibility.Visible;
                NavigationViewItem_EndfieldWar.Visibility = Visibility.Visible;
                NavigationViewItem_EndfieldMonument.Visibility = Visibility.Visible;
                NavigationView_Records.SelectedItem = NavigationViewItem_EndfieldAccount;
                TextBlock_RoleName.Text = "森空岛工具箱";
                TextBlock_RoleInfo.Text = "终末地";
                Image_Avatar.Source = new BitmapImage(new Uri("ms-appx:///Assets/Image/icon_endfield.png"));
                ShowEndfieldAccount();
                ProgressRing_Loading.IsActive = false;
                return;
            }
            ArknightsRecord record = await _service.GetRecordAsync();
            TextBlock_RoleName.Text = record.RoleName;
            TextBlock_RoleInfo.Text = $"UID {record.Uid} · Lv.{record.Level}";
            if (!string.IsNullOrWhiteSpace(record.AvatarUrl))
            {
                try
                {
                    Image_Avatar.Source = new BitmapImage(new Uri(record.AvatarUrl));
                }
                catch { }
            }
            _sections = new()
            {
                ["garrison"] = BuildGarrison(record),
                ["rogue"] = BuildRogues(record),
                ["activity"] = BuildActivityRecords(record),
            };
            if (NavigationView_Records.SelectedItem is not NavigationViewItem selected || selected.Tag is "endfield_account" or "endfield_crisis" or "endfield_war" or "endfield_monument")
            {
                NavigationView_Records.SelectedItem = NavigationViewItem_Garrison;
            }
            ShowSection((NavigationView_Records.SelectedItem as NavigationViewItem)?.Tag as string ?? "garrison");
            ArknightsAccount? account = _dailyNoteService.GetAccount();
            Accounts.Clear();
            if (account is not null)
            {
                Accounts.Add(account);
                ListView_Accounts.SelectedItem = account;
            }
            Button_LoginArknights.Visibility = account is null ? Visibility.Visible : Visibility.Collapsed;
            Button_AccountList.Visibility = account is null ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            TextBlock_Error.Text = ex.Message;
        }
        finally
        {
            ProgressRing_Loading.IsActive = false;
        }
    }


    private void ShowSection(string key)
    {
        Frame_EndfieldAccount.Visibility = Visibility.Collapsed;
        ScrollViewer_Records.Visibility = Visibility.Visible;
        Border_EndfieldGuide.Visibility = Visibility.Collapsed;
        Items_Records.ItemsSource = _sections.TryGetValue(key, out List<ArknightsRecordItemView>? list) ? list : [];
    }


    private void NavigationView_Records_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is not string key)
        {
            return;
        }
        if (key == "h5")
        {
            ArknightsGameDataWindow.ShowWindow(CurrentGameId?.GameBiz.Game is GameBiz.endfield ? "endfield" : "arknights");
            return;
        }
        if (key == "endfield_account")
        {
            ShowEndfieldAccount();
            return;
        }
        if (key is "endfield_crisis" or "endfield_war" or "endfield_monument")
        {
            ShowEndfieldGuide(key);
            return;
        }
        ShowSection(key);
    }


    private void ShowEndfieldGuide(string key)
    {
        (string title, string text) = key switch
        {
            "endfield_war" => ("战争回响", "战争回响的关卡进度与评分由森空岛终末地战绩页提供，客户端暂未接入本地解析。"),
            "endfield_monument" => ("影拓丰碑", "影拓丰碑的通关记录与收藏进度需在森空岛终末地战绩页查看。"),
            _ => ("危机合约", "危机合约的赛季记录、通关等级与奖励进度需在森空岛终末地战绩页查看。"),
        };
        Frame_EndfieldAccount.Visibility = Visibility.Collapsed;
        ScrollViewer_Records.Visibility = Visibility.Visible;
        Items_Records.ItemsSource = null;
        TextBlock_EndfieldGuideTitle.Text = title;
        TextBlock_EndfieldGuideText.Text = text;
        Border_EndfieldGuide.Visibility = Visibility.Visible;
    }


    private void Button_EndfieldGuideOpen_Click(object sender, RoutedEventArgs e)
    {
        ArknightsGameDataWindow.ShowWindow("endfield");
    }


    private void ShowEndfieldAccount()
    {
        ScrollViewer_Records.Visibility = Visibility.Collapsed;
        Frame_EndfieldAccount.Visibility = Visibility.Visible;
        if (Frame_EndfieldAccount.Content is not EndfieldAccountRecordPage)
        {
            Frame_EndfieldAccount.Navigate(typeof(EndfieldAccountRecordPage));
        }
        if (Frame_EndfieldAccount.Content is EndfieldAccountRecordPage page)
        {
            page.SetEmbedded(true);
        }
    }


    private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentGameId?.GameBiz.Game is GameBiz.endfield)
        {
            if (Frame_EndfieldAccount.Content is EndfieldAccountRecordPage page)
            {
                await page.RefreshAsync();
            }
            return;
        }
        await LoadRecordAsync();
    }


    private async void Button_LoginArknights_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EndfieldLoginDialog dialog = new() { XamlRoot = this.XamlRoot };
            await dialog.ShowAsync();
            if (string.IsNullOrWhiteSpace(dialog.LoginToken))
            {
                return;
            }
            await _dailyNoteService.AddAccountAsync(dialog.LoginToken);
            await LoadRecordAsync();
        }
        catch (Exception ex)
        {
            TextBlock_Error.Text = ex.Message;
        }
    }


    private void MenuFlyoutItem_CopyUid_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string uid } && !string.IsNullOrWhiteSpace(uid))
        {
            DataPackage package = new();
            package.SetText(uid);
            Clipboard.SetContent(package);
        }
    }


    private void NavigationViewItem_UpdateDeviceInfo_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        ArknightsGameDataWindow.UpdateDeviceFingerprint();
    }


    private static List<ArknightsRecordItemView> BuildActivityRecords(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        foreach (ArknightsActivityRecord activity in record.Activities.OrderByDescending(x => x.StartTime))
        {
            string time = activity.StartTime == DateTimeOffset.MinValue
                ? ""
                : $"{activity.StartTime.ToLocalTime():yyyy-MM-dd} - {activity.EndTime.ToLocalTime():yyyy-MM-dd}";
            bool completed = activity.TotalStages > 0 && activity.ClearedStages >= activity.TotalStages;
            list.Add(new ArknightsRecordItemView
            {
                Title = activity.Name,
                Subtitle = string.IsNullOrWhiteSpace(time) ? "SideStory" : $"SideStory · {time}",
                Value = completed ? "已完成" : "进行中",
                HasProgress = activity.TotalStages > 0,
                ProgressValue = activity.ClearedStages,
                ProgressMaximum = Math.Max(1, activity.TotalStages),
            });
        }
        foreach (ArknightsSandboxRecord sandbox in record.Sandboxes)
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = sandbox.Name,
                Subtitle = $"生息演算 · 基地等级 {sandbox.BaseLv} · 击杀 {sandbox.EnemyKill}",
                Value = $"生存 {sandbox.MaxDay} 天",
                HasProgress = sandbox.SubQuestTotal > 0,
                ProgressValue = sandbox.SubQuestDone,
                ProgressMaximum = Math.Max(1, sandbox.SubQuestTotal),
            });
        }
        foreach (ArknightsTowerRecord tower in record.Towers.OrderByDescending(x => x.Best))
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = tower.Name,
                Subtitle = $"保全派驻 · {tower.SubName}",
                Value = $"最高进度 {tower.Best}",
            });
        }
        foreach (ArknightsCampaignRecord campaign in record.Campaigns.OrderByDescending(x => x.MaxKills))
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = campaign.Name,
                Subtitle = $"剿灭作战 · {campaign.ZoneName}",
                Value = $"最高剿灭 {campaign.MaxKills}",
                HasProgress = campaign.MaxKills > 0,
                ProgressValue = campaign.MaxKills,
                ProgressMaximum = 400,
            });
        }
        foreach (ArknightsBossRushRecord bossRush in record.BossRushes)
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = bossRush.Name,
                Subtitle = bossRush.Played ? $"引航者试炼 · {bossRush.StageName} · {bossRush.Difficulty}" : "引航者试炼 · 暂无记录",
                Value = bossRush.Played ? "已挑战" : "未挑战",
            });
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无活动战绩" });
        }
        return list;
    }


    private static List<ArknightsRecordItemView> BuildGarrison(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        if (record.Garrison is ArknightsGarrisonRecord garrison)
        {
            foreach (ArknightsGarrisonMatch match in garrison.Matches.OrderByDescending(x => x.Timestamp).Take(30))
            {
                string time = match.Timestamp > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(match.Timestamp).ToLocalTime().ToString("MM-dd HH:mm")
                    : "";
                list.Add(new ArknightsRecordItemView
                {
                    Title = $"{garrison.SeasonName} · {time}",
                    Subtitle = $"击杀 {match.KillCount} · 奖杯 {match.Trophy}",
                    Value = match.Completed ? "完成" : "未完成",
                });
            }
            if (list.Count == 0)
            {
                list.Add(new ArknightsRecordItemView { Title = garrison.SeasonName, Subtitle = "当前活动暂无战绩" });
            }
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无卫戍协议战绩" });
        }
        return list;
    }


    private static List<ArknightsRecordItemView> BuildActivities(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        foreach (ArknightsActivityRecord activity in record.Activities.OrderByDescending(x => x.StartTime))
        {
            string time = activity.StartTime == DateTimeOffset.MinValue
                ? ""
                : $"{activity.StartTime.ToLocalTime():yyyy-MM-dd} - {activity.EndTime.ToLocalTime():yyyy-MM-dd}";
            bool completed = activity.TotalStages > 0 && activity.ClearedStages >= activity.TotalStages;
            list.Add(new ArknightsRecordItemView
            {
                Title = activity.Name,
                Subtitle = string.IsNullOrWhiteSpace(time) ? "SideStory" : time,
                Value = completed ? "已完成" : "进行中",
                HasProgress = activity.TotalStages > 0,
                ProgressValue = activity.ClearedStages,
                ProgressMaximum = Math.Max(1, activity.TotalStages),
            });
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无 SideStory 记录" });
        }
        return list;
    }


    private static List<ArknightsRecordItemView> BuildRogues(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        foreach (ArknightsRogueRecord rogue in record.Rogues)
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = rogue.Name,
                Subtitle = $"通关 {rogue.ClearTime} 次 · 等级 {rogue.BpLevel}",
                Value = $"奖章 {rogue.MedalCurrent}/{rogue.MedalTotal}",
                HasProgress = rogue.MedalTotal > 0,
                ProgressValue = rogue.MedalCurrent,
                ProgressMaximum = Math.Max(1, rogue.MedalTotal),
            });
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无集成战略记录" });
        }
        return list;
    }


    private static List<ArknightsRecordItemView> BuildSandboxes(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        foreach (ArknightsSandboxRecord sandbox in record.Sandboxes)
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = sandbox.Name,
                Subtitle = $"基地等级 {sandbox.BaseLv} · 击杀 {sandbox.EnemyKill}",
                Value = $"生存 {sandbox.MaxDay} 天",
                HasProgress = sandbox.SubQuestTotal > 0,
                ProgressValue = sandbox.SubQuestDone,
                ProgressMaximum = Math.Max(1, sandbox.SubQuestTotal),
            });
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无生息演算记录" });
        }
        return list;
    }


    private static List<ArknightsRecordItemView> BuildTowers(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        foreach (ArknightsTowerRecord tower in record.Towers.OrderByDescending(x => x.Best))
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = tower.Name,
                Subtitle = tower.SubName,
                Value = $"最高进度 {tower.Best}",
            });
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无保全派驻记录" });
        }
        return list;
    }


    private static List<ArknightsRecordItemView> BuildCampaigns(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        foreach (ArknightsCampaignRecord campaign in record.Campaigns.OrderByDescending(x => x.MaxKills))
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = campaign.Name,
                Subtitle = campaign.ZoneName,
                Value = $"最高剿灭 {campaign.MaxKills}",
                HasProgress = campaign.MaxKills > 0,
                ProgressValue = campaign.MaxKills,
                ProgressMaximum = 400,
            });
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无剿灭作战记录" });
        }
        return list;
    }


    private static List<ArknightsRecordItemView> BuildBossRushes(ArknightsRecord record)
    {
        List<ArknightsRecordItemView> list = [];
        foreach (ArknightsBossRushRecord bossRush in record.BossRushes)
        {
            list.Add(new ArknightsRecordItemView
            {
                Title = bossRush.Name,
                Subtitle = bossRush.Played ? $"{bossRush.StageName} · {bossRush.Difficulty}" : "暂无记录",
                Value = bossRush.Played ? "已挑战" : "未挑战",
            });
        }
        if (list.Count == 0)
        {
            list.Add(new ArknightsRecordItemView { Title = "暂无引航者试炼记录" });
        }
        return list;
    }

}
