using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Gacha.Endfield;
using Starward.Features.GameRecord.DailyNote;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    public ArknightsRecordPage()
    {
        InitializeComponent();
        Loaded += ArknightsRecordPage_Loaded;
    }


    private Dictionary<string, List<ArknightsRecordItemView>> _sections = [];

    private async void ArknightsRecordPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRecordAsync();
    }


    private async Task LoadRecordAsync()
    {
        try
        {
            ProgressRing_Loading.IsActive = true;
            TextBlock_Error.Text = "";
            if (CurrentGameId?.GameBiz.Game is GameBiz.endfield)
            {
                foreach (NavigationViewItem item in NavigationView_Records.MenuItems.OfType<NavigationViewItem>())
                {
                    if (item.Tag is string tag && tag is "garrison" or "rogue" or "activity")
                    {
                        item.Visibility = Visibility.Collapsed;
                    }
                }
                NavigationView_Records.SelectedItem = NavigationView_Records.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (x.Tag as string) == "h5");
                Items_Records.ItemsSource = new List<ArknightsRecordItemView>
                {
                    new() { Title = "终末地游戏数据", Subtitle = "点击左侧「战绩」打开森空岛 H5" },
                };
                TextBlock_RoleName.Text = "森空岛工具箱";
                TextBlock_RoleInfo.Text = "终末地";
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
            if (NavigationView_Records.SelectedItem is not NavigationViewItem)
            {
                NavigationView_Records.SelectedItem = NavigationViewItem_Garrison;
            }
            ShowSection((NavigationView_Records.SelectedItem as NavigationViewItem)?.Tag as string ?? "garrison");
            ArknightsAccount? account = _dailyNoteService.GetAccount();
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
        ShowSection(key);
    }


    private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
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
