using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Gacha.Endfield;

public sealed partial class EndfieldAccountRecordPage : PageBase
{

    private readonly ILogger<EndfieldAccountRecordPage> _logger = AppConfig.GetLogger<EndfieldAccountRecordPage>();

    private readonly EndfieldGachaService _service = AppConfig.GetService<EndfieldGachaService>();

    private List<EndfieldAccountRecordItem> _allAccountRecords = [];

    private string _accountRecordsAccountKey = "";

    private CancellationTokenSource? _syncCancellationTokenSource;



    public EndfieldAccountRecordPage()
    {
        InitializeComponent();
        InitializeAccountRecordFilters();
    }



    public ObservableCollection<EndfieldGachaAccount> Accounts { get; } = [];

    public ObservableCollection<EndfieldAccountRecordItem> AccountRecords { get; } = [];

    public ObservableCollection<EndfieldAccountRecordSummary> AccountRecordSummaries { get; } = [];

    public ObservableCollection<EndfieldAccountRecordFilterOption> AccountRecordFilters { get; } = [];



    public EndfieldGachaAccount? SelectedAccount
    {
        get;
        set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                LoadLocalAccountRecords(value);
            }
        }
    }



    public int SelectedAccountRecordCategoryIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AccountRecordListTitle = value >= 0 && value < AccountRecordFilters.Count
                    ? AccountRecordFilters[value].Name
                    : "账号记录";
                FilterAccountRecords();
            }
        }
    }



    public bool IsSyncing { get; set => SetProperty(ref field, value); }

    public bool IsAccountRecordEmpty { get; set => SetProperty(ref field, value); } = true;

    public bool IsStatusOpen { get; set => SetProperty(ref field, value); }

    public string StatusText { get; set => SetProperty(ref field, value); } = "";

    public InfoBarSeverity StatusSeverity { get; set => SetProperty(ref field, value); } = InfoBarSeverity.Informational;

    public string AccountRecordUpdatedText { get; set => SetProperty(ref field, value); } = "尚未同步";

    public string AccountRecordListTitle { get; set => SetProperty(ref field, value); } = "理智";



    protected override void OnLoaded()
    {
        LoadAccounts();
    }



    protected override void OnUnloaded()
    {
        _syncCancellationTokenSource?.Cancel();
        _syncCancellationTokenSource?.Dispose();
        _syncCancellationTokenSource = null;
    }



    private void LoadAccounts(string? selectedAccountKey = null)
    {
        selectedAccountKey ??= SelectedAccount?.AccountKey;
        List<EndfieldGachaAccount> accounts = _service.GetAccounts();
        Accounts.Clear();
        foreach (EndfieldGachaAccount account in accounts)
        {
            Accounts.Add(account);
        }
        SelectedAccount = Accounts.FirstOrDefault(x => x.AccountKey == selectedAccountKey) ?? Accounts.FirstOrDefault();
        if (SelectedAccount is null)
        {
            ClearAccountRecords();
        }
    }



    private void InitializeAccountRecordFilters()
    {
        AddFilter("stamina", "理智", "\uE946");
        AddFilter("currency:1", "源石", "\uE8C7");
        AddFilter("currency:2", "嵌晶玉", "\uE8C7");
        AddFilter("currency:3", "武库配额", "\uE8C7");
        AddFilter("battle_pass", "协议通行证", "\uE8D7");
        AddFilter("monthly_card", "月卡", "\uE8C7");
        AddFilter("mail", "邮件", "\uE715");
        AddFilter("login", "登录", "\uE77B");
        SelectedAccountRecordCategoryIndex = -1;
        SelectedAccountRecordCategoryIndex = 0;

        void AddFilter(string key, string name, string glyph)
        {
            AccountRecordFilters.Add(new EndfieldAccountRecordFilterOption
            {
                Key = key,
                Name = name,
                Glyph = glyph,
                Icon = EndfieldGachaService.GetAccountRecordIcon(key),
            });
        }
    }



    private void ClearAccountRecords()
    {
        _allAccountRecords.Clear();
        _accountRecordsAccountKey = "";
        AccountRecords.Clear();
        AccountRecordSummaries.Clear();
        AccountRecordUpdatedText = "尚未同步";
        IsAccountRecordEmpty = true;
        UpdateAccountRecordFilterCounts();
    }



    private void LoadLocalAccountRecords(EndfieldGachaAccount account)
    {
        try
        {
            ApplyAccountRecords(account, _service.GetAccountRecords(account.AccountKey));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load local Endfield account records: {ErrorType}", ex.GetType().Name);
            ClearAccountRecords();
        }
    }



    private void ApplyAccountRecords(EndfieldGachaAccount account, EndfieldAccountRecordSyncResult result)
    {
        _allAccountRecords = result.Items.ToList();
        _accountRecordsAccountKey = account.AccountKey;
        AccountRecordSummaries.Clear();
        foreach (EndfieldAccountRecordSummary summary in result.Summaries)
        {
            AccountRecordSummaries.Add(summary);
        }
        AccountRecordUpdatedText = result.SyncTime == DateTimeOffset.MinValue
            ? "尚未同步"
            : $"更新于 {result.SyncTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
        UpdateAccountRecordFilterCounts();
        FilterAccountRecords();
    }



    private void UpdateAccountRecordFilterCounts()
    {
        foreach (EndfieldAccountRecordFilterOption option in AccountRecordFilters)
        {
            option.Count = _allAccountRecords.Count(x => x.RecordType == option.Key);
        }
    }



    private void FilterAccountRecords()
    {
        string recordType = SelectedAccountRecordCategoryIndex >= 0 &&
            SelectedAccountRecordCategoryIndex < AccountRecordFilters.Count
            ? AccountRecordFilters[SelectedAccountRecordCategoryIndex].Key
            : "stamina";
        AccountRecords.Clear();
        foreach (EndfieldAccountRecordItem item in _allAccountRecords.Where(x => x.RecordType == recordType))
        {
            AccountRecords.Add(item);
        }
        IsAccountRecordEmpty = AccountRecords.Count == 0;
    }



    [RelayCommand]
    private async Task SyncAccountRecordsAsync()
    {
        if (IsSyncing)
        {
            return;
        }
        if (SelectedAccount is null || !_service.HasSavedLoginToken(SelectedAccount.AccountKey))
        {
            await LoginAccountInternalAsync(syncAccountRecords: true);
            return;
        }
        await ExecuteAccountRecordSyncAsync(SelectedAccount);
    }



    [RelayCommand]
    private async Task LoginAccountAsync()
    {
        if (!IsSyncing)
        {
            await LoginAccountInternalAsync(syncAccountRecords: true);
        }
    }



    [RelayCommand]
    private async Task InputLoginTokenAsync()
    {
        if (IsSyncing)
        {
            return;
        }
        var passwordBox = new PasswordBox
        {
            MinWidth = 360,
            PlaceholderText = "登录 Token",
            PasswordRevealMode = PasswordRevealMode.Peek,
        };
        var dialog = new ContentDialog
        {
            Title = "输入鹰角账号登录 Token",
            Content = passwordBox,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(passwordBox.Password))
        {
            await AddAccountAndSyncAsync(passwordBox.Password);
        }
    }



    private async Task LoginAccountInternalAsync(bool syncAccountRecords = false)
    {
        try
        {
            var dialog = new EndfieldLoginDialog { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (!string.IsNullOrWhiteSpace(dialog.LoginToken))
            {
                await AddAccountAndSyncAsync(dialog.LoginToken, syncAccountRecords);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to login Hypergryph account: {ErrorType}", ex.GetType().Name);
            StatusSeverity = InfoBarSeverity.Error;
            StatusText = ex.Message;
            IsStatusOpen = true;
        }
    }



    private async Task AddAccountAndSyncAsync(string loginToken, bool syncAccountRecords = false)
    {
        PrepareSync();
        try
        {
            var progress = new Progress<string>(text => StatusText = text);
            List<EndfieldGachaAccount> accounts = await _service.AddAccountsFromLoginTokenAsync(
                loginToken, progress, _syncCancellationTokenSource!.Token);
            EndfieldGachaAccount account = accounts[0];
            LoadAccounts(account.AccountKey);
            if (syncAccountRecords)
            {
                EndfieldAccountRecordSyncResult result = await _service.SyncAccountRecordsAsync(
                    account, progress, _syncCancellationTokenSource.Token);
                ApplyAccountRecords(account, result);
                StatusSeverity = result.FailedCategories.Length == 0
                    ? InfoBarSeverity.Success
                    : InfoBarSeverity.Warning;
                StatusText = result.FailedCategories.Length == 0
                    ? $"账号记录同步完成，共读取 {result.Items.Count} 条。"
                    : $"账号记录已部分更新，以下分类读取失败：{string.Join("、", result.FailedCategories)}。";
            }
        }
        catch (OperationCanceledException)
        {
            StatusSeverity = InfoBarSeverity.Informational;
            StatusText = "同步已取消。";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to add Endfield account: {ErrorType}", ex.GetType().Name);
            StatusSeverity = InfoBarSeverity.Error;
            StatusText = ex.Message;
        }
        finally
        {
            IsSyncing = false;
        }
    }



    private async Task ExecuteAccountRecordSyncAsync(EndfieldGachaAccount account)
    {
        PrepareSync();
        try
        {
            var progress = new Progress<string>(text => StatusText = text);
            EndfieldAccountRecordSyncResult result = await _service.SyncAccountRecordsAsync(
                account, progress, _syncCancellationTokenSource!.Token);
            if (SelectedAccount?.AccountKey == account.AccountKey)
            {
                ApplyAccountRecords(account, result);
            }
            StatusSeverity = result.FailedCategories.Length == 0
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Warning;
            StatusText = result.FailedCategories.Length == 0
                ? $"账号记录同步完成，共读取 {result.Items.Count} 条。"
                : $"账号记录已部分更新，以下分类读取失败：{string.Join("、", result.FailedCategories)}。";
            IsStatusOpen = true;
        }
        catch (OperationCanceledException)
        {
            StatusSeverity = InfoBarSeverity.Informational;
            StatusText = "同步已取消。";
            IsStatusOpen = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to sync Endfield account records: {ErrorType}", ex.GetType().Name);
            StatusSeverity = InfoBarSeverity.Error;
            StatusText = ex.Message;
            IsStatusOpen = true;
        }
        finally
        {
            IsSyncing = false;
        }
    }



    private void PrepareSync()
    {
        _syncCancellationTokenSource?.Dispose();
        _syncCancellationTokenSource = new CancellationTokenSource();
        IsSyncing = true;
        IsStatusOpen = true;
        StatusSeverity = InfoBarSeverity.Informational;
        StatusText = "正在准备同步";
    }



    [RelayCommand]
    private void CancelSync()
    {
        _syncCancellationTokenSource?.Cancel();
    }


}
