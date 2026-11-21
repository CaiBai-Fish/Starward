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
using Starward.Features.GameRecord.EndfieldToolbox;
using System;
using System.Threading.Tasks;


namespace Starward.Features.GameRecord.DailyNote;

[INotifyPropertyChanged]
public sealed partial class DailyNoteButton : UserControl
{

    private readonly ILogger<DailyNoteButton> _logger = AppConfig.GetLogger<DailyNoteButton>();


    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();

    private readonly EndfieldRecordService _endfieldRecordService = AppConfig.GetService<EndfieldRecordService>();

    private readonly EndfieldGachaService _endfieldGachaService = AppConfig.GetService<EndfieldGachaService>();

    private readonly ArknightsDailyNoteService _arknightsDailyNoteService = AppConfig.GetService<ArknightsDailyNoteService>();


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
                OnPropertyChanged(nameof(IsArknightsEnabled));
                if (value is not null && this.IsLoaded)
                {
                    RefreshDailyNoteCommand.Execute(false);
                }
            }
        }
    }



    public bool IsBH3Enabled => CurrentGameId?.GameBiz.Game is GameBiz.bh3;

    public bool IsGenshinEnabled => CurrentGameId?.GameBiz.Game is GameBiz.hk4e;

    public bool IsStarRailEnabled => CurrentGameId?.GameBiz.Game is GameBiz.hkrpg;

    public bool IsZZZEnabled => CurrentGameId?.GameBiz.Game is GameBiz.nap;

    public bool IsEndfieldEnabled => CurrentGameId?.GameBiz.Game is GameBiz.endfield;

    public bool IsArknightsEnabled => CurrentGameId?.GameBiz.Game is GameBiz.arknights;



    private GameRecordRole? GameRecordRole { get; set => SetProperty(ref field, value); }


    public BH3DailyNote BH3DailyNote { get; set => SetProperty(ref field, value); }

    public GenshinDailyNote GenshinDailyNote { get; set => SetProperty(ref field, value); }

    public StarRailDailyNote StarRailDailyNote { get; set => SetProperty(ref field, value); }

    public ZZZDailyNote ZZZDailyNote { get; set => SetProperty(ref field, value); }

    public EndfieldDailyNote? EndfieldDailyNote { get; set => SetProperty(ref field, value); }

    public ArknightsDailyNote? ArknightsDailyNote { get; set => SetProperty(ref field, value); }


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
        ArknightsDailyNote = null;
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
                EndfieldDailyNote getNote = await _endfieldGachaService.GetDailyNoteAsync(endfieldAccount, forceUpdate.Value);
                try
                {
                    using System.Text.Json.JsonDocument creditDocument = await _endfieldRecordService.GetCardDetailAsync(endfieldAccount);
                    EndfieldRecordParser.ApplyCredit(getNote, creditDocument.RootElement);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Endfield credit failed: {ex.Message}");
                }
                EndfieldDailyNote = getNote;
                return;
            }
            if (gameBiz.Game is GameBiz.arknights)
            {
                this.Visibility = Visibility.Visible;
                ArknightsAccount? arknightsAccount = _arknightsDailyNoteService.GetAccount();
                if (arknightsAccount is null)
                {
                    ArknightsDailyNote = null;
                    ErrorMessage = "请先登录明日方舟的鹰角账号。";
                    Button_LoginArknights.Visibility = Visibility.Visible;
                    return;
                }
                ErrorMessage = null;
                Button_LoginArknights.Visibility = Visibility.Collapsed;
                ArknightsDailyNote = await _arknightsDailyNoteService.GetDailyNoteAsync(arknightsAccount, forceUpdate.Value);
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
            else if (CurrentGameId?.GameBiz.Game is GameBiz.endfield or GameBiz.arknights)
            {
                ErrorMessage = ex.Message;
            }
            _logger.LogError(ex, "Refresh daily note failed (Biz: {GameBiz}, Server: {GameServer}, Uid: {Uid})", CurrentGameId?.GameBiz, GameRecordRole?.Region, GameRecordRole?.Uid);
        }
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
            ErrorMessage = "正在读取该鹰角账号的明日方舟角色…";
            await _arknightsDailyNoteService.AddAccountAsync(dialog.LoginToken);
            ErrorMessage = null;
            Button_LoginArknights.Visibility = Visibility.Collapsed;
            RefreshDailyNoteCommand.Execute(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Login Arknights account failed.");
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
