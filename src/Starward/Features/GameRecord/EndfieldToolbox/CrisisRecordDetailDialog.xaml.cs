using Microsoft.UI.Xaml.Controls;
using Starward.Features.Gacha.Endfield;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.EndfieldToolbox;

public sealed partial class CrisisRecordDetailDialog : ContentDialog
{

    private readonly EndfieldRecordService _recordService = AppConfig.GetService<EndfieldRecordService>();


    public CrisisContractRecordView Record { get; private set; } = new();


    public List<EndfieldCharView> Chars { get; private set; } = [];


    public List<CrisisContractIndicatorView> Indicators { get; private set; } = [];


    public string IndicatorCaption { get; private set; } = "赛季可选指标";


    public string StatusText { get; private set; } = "";

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);


    public CrisisRecordDetailDialog()
    {
        InitializeComponent();
    }


    public async Task LoadAsync(CrisisContractRecordView record, EndfieldGachaAccount? account,
        IReadOnlyList<CrisisContractIndicatorView> seasonIndicators)
    {
        Record = record;
        Chars = record.Chars.ToList();
        Indicators = seasonIndicators.ToList();
        if (account is not null && !string.IsNullOrWhiteSpace(record.Id))
        {
            try
            {
                using JsonDocument document = await _recordService.GetCrisisContractRecordAsync(account, record.Id, "recordId");
                CrisisContractRecordDetail detail = EndfieldRecordParser.ParseCrisisRecordDetail(document.RootElement);
                if (detail.HasChars && Chars.Count == 0)
                {
                    Chars = detail.Chars;
                }
                if (detail.HasIndicators)
                {
                    Indicators = detail.Indicators;
                    IndicatorCaption = "本次挑战启用";
                    StatusText = "";
                }
                else
                {
                    StatusText = "森空岛没有返回本次挑战的指标明细，下方展示本赛季可选指标。";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"挑战详情读取失败：{ex.Message}";
            }
        }
        else
        {
            StatusText = "未登录终末地账号，无法读取挑战详情，下方展示本赛季可选指标。";
        }

        if (Chars.Count == 0)
        {
            StatusText = $"{StatusText} 该记录没有干员信息。".Trim();
        }
        if (Indicators.Count == 0)
        {
            StatusText = $"{StatusText} 接口没有返回可显示的指标。".Trim();
        }
        Bindings.Update();
    }

}
