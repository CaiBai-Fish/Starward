using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using Starward.Core.GameRecord;
using Starward.Core.GameRecord.BH3.DailyNote;
using Starward.Core.GameRecord.Genshin.DailyNote;
using Starward.Core.GameRecord.StarRail.DailyNote;
using Starward.Core.GameRecord.ZZZ.DailyNote;
using Starward.Core.HoYoPlay;
using Starward.Features.Gacha.Endfield;
using System;
using System.Threading.Tasks;


namespace Starward.Features.GameRecord.DailyNote;

[INotifyPropertyChanged]
public sealed partial class DailyNoteButton : UserControl
{

    private readonly ILogger<DailyNoteButton> _logger = AppConfig.GetLogger<DailyNoteButton>();


    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();

    private readonly EndfieldGachaService _endfieldGachaService = AppConfig.GetService<EndfieldGachaService>();


    public DailyNoteButton()
    {
        this.InitializeComponent();
        this.Visibility = Visibility.Collapsed;
    }



    public GameId CurrentGameId
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsBH3Enabled));
                OnPropertyChanged(nameof(IsGenshinEnabled));
                OnPropertyChanged(nameof(IsStarRailEnabled));
                OnPropertyChanged(nameof(IsZZZEnabled));
                OnPropertyChanged(nameof(IsEndfieldEnabled));
            }
        }
    }



    public bool IsBH3Enabled => CurrentGameId?.GameBiz.Game is GameBiz.bh3;

    public bool IsGenshinEnabled => CurrentGameId?.GameBiz.Game is GameBiz.hk4e;

    public bool IsStarRailEnabled => CurrentGameId?.GameBiz.Game is GameBiz.hkrpg;

    public bool IsZZZEnabled => CurrentGameId?.GameBiz.Game is GameBiz.nap;

    public bool IsEndfieldEnabled => CurrentGameId?.GameBiz.Game is GameBiz.endfield;



    private GameRecordRole? GameRecordRole { get; set => SetProperty(ref field, value); }


    public BH3DailyNote BH3DailyNote { get; set => SetProperty(ref field, value); }

    public GenshinDailyNote GenshinDailyNote { get; set => SetProperty(ref field, value); }

    public StarRailDailyNote StarRailDailyNote { get; set => SetProperty(ref field, value); }

    public ZZZDailyNote ZZZDailyNote { get; set => SetProperty(ref field, value); }

    public EndfieldDailyNote? EndfieldDailyNote { get; set => SetProperty(ref field, value); }


    public string? ErrorMessage { get; set => SetProperty(ref field, value); }



    private void Button_DailyNote_Loaded(object sender, RoutedEventArgs e)
    {
        if (CurrentGameId is null)
        {
            return;
        }
        if (!GameFeatureConfig.FromGameId(CurrentGameId).SupportDailyNote)
        {
            return;
        }
        RefreshDailyNoteCommand.Execute(false);
    }



    private void Button_DailyNote_Unloaded(object sender, RoutedEventArgs e)
    {
        GameRecordRole = null;
        BH3DailyNote = null!;
        GenshinDailyNote = null!;
        StarRailDailyNote = null!;
        ZZZDailyNote = null!;
        EndfieldDailyNote = null;
    }



    [RelayCommand]
    private async Task RefreshDailyNoteAsync(bool? forceUpdate)
    {
        try
        {
            if (!forceUpdate.HasValue)
            {
                forceUpdate = true;
            }
            GameBiz gameBiz = CurrentGameId.GameBiz;
            if (gameBiz.Server is "bilibili")
            {
                gameBiz = $"{gameBiz.Game}_cn";
            }
            if (gameBiz.Game is GameBiz.endfield)
            {
                EndfieldGachaAccount? endfieldAccount = _endfieldGachaService.GetCurrentAccount();
                if (endfieldAccount is null)
                {
                    this.Visibility = Visibility.Visible;
                    ErrorMessage = "请先在寻访记录中登录鹰角账号。";
                    return;
                }
                this.Visibility = Visibility.Visible;
                ErrorMessage = null;
                EndfieldDailyNote = await _endfieldGachaService.GetDailyNoteAsync(endfieldAccount, forceUpdate.Value);
                return;
            }
            _gameRecordService.IsHoyolab = gameBiz.Server is "global";
            GameRecordRole = _gameRecordService.GetLastSelectGameRecordRoleOrTheFirstOne(gameBiz);
            if (GameRecordRole is not null)
            {
                this.Visibility = Visibility.Visible;
                ErrorMessage = null;
                await _gameRecordService.UpdateDeviceFpAsync();
                if (IsBH3Enabled)
                {
                    BH3DailyNote = await _gameRecordService.GetBH3DailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
                if (IsGenshinEnabled)
                {
                    GenshinDailyNote = await _gameRecordService.GetGenshinDailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
                if (IsStarRailEnabled)
                {
                    StarRailDailyNote = await _gameRecordService.GetStarRailDailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
                if (IsZZZEnabled)
                {
                    ZZZDailyNote = await _gameRecordService.GetZZZDailyNoteAsync(GameRecordRole, forceUpdate.Value);
                }
            }
        }
        catch (Exception ex)
        {
            if (ex is miHoYoApiException miHoYoEx)
            {
                ErrorMessage = $"Error Code: {ex.Message}";
                if (GameRecordPage.IsAccountErrorCode(miHoYoEx.ReturnCode) && ShouldShowAccountErrorToast())
                {
                    GameRecordPage.HandleMiHoYoApiException(miHoYoEx, GameRecordRole);
                }
            }
            else if (CurrentGameId?.GameBiz.Game is GameBiz.endfield)
            {
                ErrorMessage = ex.Message;
            }
            _logger.LogError(ex, "Refresh daily note failed (Biz: {GameBiz}, Server: {GameServer}, Uid: {Uid})", CurrentGameId?.GameBiz, GameRecordRole?.Region, GameRecordRole?.Uid);
        }
    }





    private static DateTime _lastAccountErrorToastTime;


    /// <summary>
    /// 账号异常提示节流，避免页面切换时反复弹出
    /// </summary>
    private static bool ShouldShowAccountErrorToast()
    {
        DateTime now = DateTime.Now;
        if (now - _lastAccountErrorToastTime < TimeSpan.FromMinutes(2))
        {
            return false;
        }
        _lastAccountErrorToastTime = now;
        return true;
    }



    public static string ZZZMemberCardRemainingDaysToString(int remainingDays)
    {
        return string.Format(Lang.DailyNoteButton_0DaySRemaining, remainingDays);
    }





}
