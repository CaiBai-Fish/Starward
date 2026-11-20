using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.ArknightsRecord;

public sealed partial class ArknightsRecordPage : Page
{
    private readonly ArknightsRecordService _service = AppConfig.GetService<ArknightsRecordService>();

    public GameId? CurrentGameId { get; set; }

    public ArknightsRecordPage()
    {
        InitializeComponent();
        Loaded += ArknightsRecordPage_Loaded;
    }


    private async void ArknightsRecordPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ProgressRing_Loading.IsActive = true;
            TextBlock_Error.Text = "";
            ArknightsRecord record = await _service.GetRecordAsync();
            TextBlock_RoleName.Text = record.RoleName;
            TextBlock_RoleInfo.Text = $"UID {record.Uid} · 等级 {record.Level} · 主线 {record.MainStage} · 干员 {record.CharCount} · 时装 {record.SkinCount}";
            if (record.Garrison is ArknightsGarrisonRecord garrison)
            {
                string time = garrison.StartTime == DateTimeOffset.MinValue
                    ? ""
                    : $"{garrison.StartTime.ToLocalTime():yyyy-MM-dd} - {garrison.EndTime.ToLocalTime():yyyy-MM-dd}";
                TextBlock_Garrison.Text = $"{garrison.SeasonName}  {time}  共 {garrison.Matches.Count} 场";
                ItemsControl_Garrison.ItemsSource = garrison.Matches
                                                           .OrderByDescending(x => x.Timestamp)
                                                           .Take(10)
                                                           .Select(x => $"{DateTimeOffset.FromUnixTimeSeconds(x.Timestamp).ToLocalTime():MM-dd HH:mm}  击杀 {x.KillCount}  奖杯 {x.Trophy}  {(x.Completed ? "完成" : "未完成")}")
                                                           .ToList();
            }
            else
            {
                TextBlock_Garrison.Text = "暂无卫戍协议战绩";
            }
            ItemsControl_Activities.ItemsSource = record.Activities
                                                      .OrderByDescending(x => x.ClearedStages)
                                                      .Take(10)
                                                      .Select(x => $"{x.ActId}  关卡 {x.ClearedStages}/{x.TotalStages}")
                                                      .ToList();
            ItemsControl_Campaigns.ItemsSource = record.Campaigns
                                                     .OrderByDescending(x => x.MaxKills)
                                                     .Take(10)
                                                     .Select(x => $"{x.Id}  最高击杀 {x.MaxKills}")
                                                     .ToList();
            ItemsControl_Towers.ItemsSource = record.Towers
                                                  .OrderByDescending(x => x.Best)
                                                  .Select(x => $"{x.Id}  最佳记录 {x.Best}")
                                                  .ToList();
            ItemsControl_Rogues.ItemsSource = record.Rogues
                                                  .Select(x => $"{x.Id}  通关 {x.ClearTime} 次 · 等级 {x.BpLevel} · 奖章 {x.MedalCurrent}/{x.MedalTotal}")
                                                  .ToList();
            Panel_Content.Visibility = Visibility.Visible;
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

}
