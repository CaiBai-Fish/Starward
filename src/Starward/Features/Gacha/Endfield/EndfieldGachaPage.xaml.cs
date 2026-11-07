using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Gacha.Endfield;

public sealed partial class EndfieldGachaPage : PageBase
{
    private static readonly Dictionary<string, string> CharacterPoolNames = new()
    {
        ["E_CharacterGachaPoolType_Special"] = "特许寻访",
        ["E_CharacterGachaPoolType_Joint"] = "辉光庆典",
        ["E_CharacterGachaPoolType_Standard"] = "基础寻访",
        ["E_CharacterGachaPoolType_Beginner"] = "启程寻访",
    };

    private static readonly string[] ReissueKeywords = ["复刻", "再演", "重临"];

    private readonly ILogger<EndfieldGachaPage> _logger = AppConfig.GetLogger<EndfieldGachaPage>();
    private readonly EndfieldGachaService _service = AppConfig.GetService<EndfieldGachaService>();
    private List<EndfieldGachaItem> _allItems = [];
    private CancellationTokenSource? _syncCancellationTokenSource;
    private CancellationTokenSource? _gachaInfoCancellationTokenSource;

    public EndfieldGachaPage()
    {
        InitializeComponent();
    }

    public ObservableCollection<EndfieldGachaAccount> Accounts { get; } = [];

    public ObservableCollection<EndfieldGachaPoolStats> CharacterStats { get; } = [];

    public ObservableCollection<EndfieldGachaPoolStats> WeaponStats { get; } = [];

    public ObservableCollection<EndfieldGachaPoolStats> BasicStats { get; } = [];

    public EndfieldGachaAccount? SelectedAccount
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                LoadSelectedAccount();
            }
        }
    }

    public bool IsSyncing { get; set => SetProperty(ref field, value); }

    public bool IsCharacterEmpty { get; set => SetProperty(ref field, value); } = true;

    public bool IsWeaponEmpty { get; set => SetProperty(ref field, value); } = true;

    public bool IsBasicEmpty { get; set => SetProperty(ref field, value); } = true;

    public bool IsStatusOpen { get; set => SetProperty(ref field, value); }

    public string StatusText { get; set => SetProperty(ref field, value); } = "";

    public InfoBarSeverity StatusSeverity { get; set => SetProperty(ref field, value); } = InfoBarSeverity.Informational;

    public string CharacterSummaryText { get; set => SetProperty(ref field, value); } = "";

    public string WeaponSummaryText { get; set => SetProperty(ref field, value); } = "";

    public string BasicSummaryText { get; set => SetProperty(ref field, value); } = "";

    public string BasicColumnTitle { get; set => SetProperty(ref field, value); } = "基础寻访";

    public EndfieldGachaColumnStats CharacterColumnStats { get; set => SetProperty(ref field, value); } = new();

    public EndfieldGachaColumnStats WeaponColumnStats { get; set => SetProperty(ref field, value); } = new();

    public EndfieldGachaColumnStats BasicColumnStats { get; set => SetProperty(ref field, value); } = new();

    protected override void OnLoaded()
    {
        LoadAccounts();
        _gachaInfoCancellationTokenSource?.Cancel();
        _gachaInfoCancellationTokenSource?.Dispose();
        _gachaInfoCancellationTokenSource = new CancellationTokenSource();
        if (Accounts.Count > 0)
        {
            _ = UpdateGachaInfoAsync(_gachaInfoCancellationTokenSource.Token);
        }
    }

    protected override void OnUnloaded()
    {
        _syncCancellationTokenSource?.Cancel();
        _syncCancellationTokenSource?.Dispose();
        _syncCancellationTokenSource = null;
        _gachaInfoCancellationTokenSource?.Cancel();
        _gachaInfoCancellationTokenSource?.Dispose();
        _gachaInfoCancellationTokenSource = null;
    }

    private async Task UpdateGachaInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            bool updated = await _service.UpdateGachaInfoAsync(cancellationToken);
            if (updated && !cancellationToken.IsCancellationRequested)
            {
                LoadSelectedAccount();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to update Endfield gacha icons: {ErrorType}", ex.GetType().Name);
        }
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
            _allItems.Clear();
            ClearGachaStats();
        }
    }

    private void LoadSelectedAccount()
    {
        if (SelectedAccount is null)
        {
            return;
        }
        _allItems = _service.GetItems(SelectedAccount.AccountKey, "char")
            .Concat(_service.GetItems(SelectedAccount.AccountKey, "weapon"))
            .ToList();
        BuildGachaStats();
    }

    private void ClearGachaStats()
    {
        CharacterStats.Clear();
        WeaponStats.Clear();
        BasicStats.Clear();
        IsCharacterEmpty = true;
        IsWeaponEmpty = true;
        IsBasicEmpty = true;
        CharacterSummaryText = "";
        WeaponSummaryText = "";
        BasicSummaryText = "";
        BasicColumnTitle = "基础寻访";
        CharacterColumnStats = new();
        WeaponColumnStats = new();
        BasicColumnStats = new();
    }

    private void BuildGachaStats()
    {
        CharacterStats.Clear();
        WeaponStats.Clear();
        BasicStats.Clear();

        var characterPools = new List<EndfieldGachaPoolStats>();
        var reissuePools = new List<EndfieldGachaPoolStats>();
        BuildCharacterPools(characterPools, reissuePools);

        var weaponPools = new List<EndfieldGachaPoolStats>();
        BuildWeaponPools(weaponPools);

        var basicPools = new List<EndfieldGachaPoolStats>();
        BuildBasicPools(basicPools, reissuePools);

        foreach (EndfieldGachaPoolStats item in characterPools)
        {
            CharacterStats.Add(item);
        }
        foreach (EndfieldGachaPoolStats item in weaponPools)
        {
            WeaponStats.Add(item);
        }
        // 第三列：复刻记录在前，基础寻访在后
        foreach (EndfieldGachaPoolStats item in reissuePools)
        {
            BasicStats.Add(item);
        }
        foreach (EndfieldGachaPoolStats item in basicPools)
        {
            BasicStats.Add(item);
        }

        IsCharacterEmpty = CharacterStats.Count == 0;
        IsWeaponEmpty = WeaponStats.Count == 0;
        IsBasicEmpty = BasicStats.Count == 0;
        CharacterSummaryText = Summarize(CharacterStats);
        WeaponSummaryText = Summarize(WeaponStats);
        BasicSummaryText = Summarize(BasicStats);
        BasicColumnTitle = reissuePools.Count > 0 ? "基础寻访 / 复刻" : "基础寻访";
        CharacterColumnStats = BuildColumnStats(CharacterStats);
        WeaponColumnStats = BuildColumnStats(WeaponStats);
        BasicColumnStats = BuildColumnStats(BasicStats);
    }

    private static EndfieldGachaColumnStats BuildColumnStats(IEnumerable<EndfieldGachaPoolStats> stats)
    {
        var list = stats.ToList();
        var sixList = list.SelectMany(x => x.List6).Where(x => !x.IsPityPlaceholder && !x.IsFree).ToList();
        return new EndfieldGachaColumnStats
        {
            Pools = list.Count,
            Total = list.Sum(x => x.Count),
            Count6 = list.Sum(x => x.Count6),
            Count5 = list.Sum(x => x.Count5),
            Count4 = list.Sum(x => x.Count4),
            Average6 = sixList.Count == 0 ? 0 : sixList.Average(x => x.Pity),
        };
    }

    private static string Summarize(IEnumerable<EndfieldGachaPoolStats> stats)
    {
        var list = stats.ToList();
        return list.Count == 0 ? "" : $"{list.Count} 池 · {list.Sum(x => x.Count)} 抽";
    }

    private void BuildCharacterPools(List<EndfieldGachaPoolStats> characterPools, List<EndfieldGachaPoolStats> reissuePools)
    {
        List<EndfieldGachaItem> specialItems = SortAscending(_allItems
            .Where(x => x.PoolType == "E_CharacterGachaPoolType_Special"));
        if (specialItems.Count > 0)
        {
            var batches = new List<List<EndfieldGachaItem>>();
            foreach (EndfieldGachaItem item in specialItems)
            {
                if (batches.Count == 0 || batches[^1][0].PoolId != item.PoolId)
                {
                    batches.Add([]);
                }
                batches[^1].Add(item);
            }
            int pity6 = 0;
            int pity5 = 0;
            var cards = new List<(EndfieldGachaPoolStats Stats, bool IsReissue)>();
            foreach (List<EndfieldGachaItem> batch in batches)
            {
                string name = GetPoolName(batch, CharacterPoolNames["E_CharacterGachaPoolType_Special"]);
                EndfieldGachaPoolStats stats = CreatePoolStats($"special:{batch[0].PoolId}", name, batch,
                    ref pity6, ref pity5, true);
                cards.Add((stats, IsReissuePool(name, batch[0].PoolId)));
            }
            cards.Reverse();
            foreach ((EndfieldGachaPoolStats stats, bool isReissue) in cards)
            {
                if (isReissue)
                {
                    reissuePools.Add(stats);
                }
                else
                {
                    characterPools.Add(stats);
                }
            }
        }

        IEnumerable<IGrouping<string, EndfieldGachaItem>> jointPools = _allItems
            .Where(x => x.PoolType == "E_CharacterGachaPoolType_Joint")
            .GroupBy(x => x.PoolId)
            .OrderByDescending(x => x.Max(item => ParseTimestamp(item.GachaTime)));
        foreach (IGrouping<string, EndfieldGachaItem> pool in jointPools)
        {
            int pity6 = 0;
            int pity5 = 0;
            List<EndfieldGachaItem> items = pool.ToList();
            reissuePools.Add(CreatePoolStats($"joint:{pool.Key}",
                GetPoolName(items, CharacterPoolNames["E_CharacterGachaPoolType_Joint"]),
                items, ref pity6, ref pity5, true));
        }
    }

    private void BuildBasicPools(List<EndfieldGachaPoolStats> basicPools, List<EndfieldGachaPoolStats> reissuePools)
    {
        foreach (string poolType in CharacterPoolNames.Keys
            .Where(x => x is not "E_CharacterGachaPoolType_Special" and not "E_CharacterGachaPoolType_Joint")
            .OrderBy(GetCharacterPoolOrder))
        {
            List<EndfieldGachaItem> items = _allItems.Where(x => x.PoolType == poolType).ToList();
            if (items.Count == 0)
            {
                continue;
            }
            int pity6 = 0;
            int pity5 = 0;
            basicPools.Add(CreatePoolStats($"type:{poolType}", CharacterPoolNames[poolType], items,
                ref pity6, ref pity5, false));
        }

        // 未知类型（例如后续版本新增的复刻卡池）归入第三列
        IEnumerable<IGrouping<string, EndfieldGachaItem>> unknownPools = _allItems
            .Where(x => x.RecordType == "char" &&
                        !string.IsNullOrWhiteSpace(x.PoolType) &&
                        !CharacterPoolNames.ContainsKey(x.PoolType))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.PoolId) ? x.PoolName : x.PoolId)
            .OrderByDescending(x => x.Max(item => ParseTimestamp(item.GachaTime)));
        foreach (IGrouping<string, EndfieldGachaItem> pool in unknownPools)
        {
            int pity6 = 0;
            int pity5 = 0;
            List<EndfieldGachaItem> items = pool.ToList();
            reissuePools.Add(CreatePoolStats($"unknown:{pool.Key}", GetPoolName(items, pool.Key), items,
                ref pity6, ref pity5, false));
        }
    }

    private void BuildWeaponPools(List<EndfieldGachaPoolStats> weaponPools)
    {
        IEnumerable<IGrouping<string, EndfieldGachaItem>> pools = _allItems
            .Where(x => x.RecordType == "weapon")
            .GroupBy(x => string.IsNullOrWhiteSpace(x.PoolId) ? x.PoolName : x.PoolId)
            .OrderByDescending(x => x.Max(item => ParseTimestamp(item.GachaTime)));
        foreach (IGrouping<string, EndfieldGachaItem> pool in pools)
        {
            int pity6 = 0;
            int pity5 = 0;
            List<EndfieldGachaItem> items = pool.ToList();
            weaponPools.Add(CreatePoolStats($"weapon:{pool.Key}", GetPoolName(items, pool.Key), items,
                ref pity6, ref pity5, false));
        }
    }

    private static bool IsReissuePool(string name, string poolId)
    {
        if (ReissueKeywords.Any(keyword => name.Contains(keyword, StringComparison.Ordinal)))
        {
            return true;
        }
        return poolId.Contains("reissue", StringComparison.OrdinalIgnoreCase) ||
               poolId.Contains("rerun", StringComparison.OrdinalIgnoreCase) ||
               poolId.Contains("repeat", StringComparison.OrdinalIgnoreCase);
    }

    private static EndfieldGachaPoolStats CreatePoolStats(string key, string name,
        IEnumerable<EndfieldGachaItem> source, ref int pity6, ref int pity5, bool excludeFreePulls)
    {
        List<EndfieldGachaItem> items = SortAscending(source);
        foreach (EndfieldGachaItem item in items)
        {
            bool countsForPity = !excludeFreePulls || !item.IsFree;
            if (countsForPity)
            {
                pity6++;
                pity5++;
            }
            if (item.Rarity == 6)
            {
                item.Pity = countsForPity ? pity6 : 0;
                if (countsForPity)
                {
                    pity6 = 0;
                }
            }
            else if (item.Rarity == 5)
            {
                item.Pity = countsForPity ? pity5 : 0;
                if (countsForPity)
                {
                    pity5 = 0;
                }
            }
        }

        List<EndfieldGachaItem> list6 = items.Where(x => x.Rarity == 6).Reverse().ToList();
        List<EndfieldGachaItem> list5 = items.Where(x => x.Rarity == 5).Reverse().ToList();
        var stats = new EndfieldGachaPoolStats
        {
            Key = key,
            Name = name,
            Count = items.Count,
            StartTimeText = items[0].TimeText,
            EndTimeText = items[^1].TimeText,
            Count6 = list6.Count,
            Count5 = list5.Count,
            Count4 = items.Count(x => x.Rarity == 4),
            Pity6 = pity6,
            Pity5 = pity5,
            Average6 = list6.Where(x => !x.IsFree).Select(x => x.Pity).DefaultIfEmpty().Average(),
            List6 = list6,
            List5 = list5,
        };
        stats.List6.Insert(0, CreatePityItem(6, pity6, items[^1].GachaTime));
        stats.List5.Insert(0, CreatePityItem(5, pity5, items[^1].GachaTime));
        return stats;
    }

    private static EndfieldGachaItem CreatePityItem(int rarity, int pity, string time)
    {
        return new EndfieldGachaItem
        {
            ItemName = "保底",
            Rarity = rarity,
            Pity = pity,
            GachaTime = time,
            IsPityPlaceholder = true,
        };
    }

    private static List<EndfieldGachaItem> SortAscending(IEnumerable<EndfieldGachaItem> items)
    {
        return items.OrderBy(x => ParseTimestamp(x.GachaTime))
            .ThenBy(x => x.SeqId.Length)
            .ThenBy(x => x.SeqId, StringComparer.Ordinal)
            .ToList();
    }

    private static string GetPoolName(List<EndfieldGachaItem> items, string fallback)
    {
        return items.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.PoolName))?.PoolName ?? fallback;
    }

    private static long ParseTimestamp(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : 0;
    }

    private static int GetCharacterPoolOrder(string poolType) => poolType switch
    {
        "E_CharacterGachaPoolType_Special" => 0,
        "E_CharacterGachaPoolType_Joint" => 1,
        "E_CharacterGachaPoolType_Standard" => 2,
        "E_CharacterGachaPoolType_Beginner" => 3,
        _ => 4,
    };

    [RelayCommand]
    private async Task SyncGachaAsync()
    {
        if (IsSyncing)
        {
            return;
        }
        if (SelectedAccount is not null && _service.HasSavedLoginToken(SelectedAccount.AccountKey))
        {
            EndfieldGachaAccount account = SelectedAccount;
            await ExecuteSyncAsync((progress, token) => _service.SyncAccountAsync(account, progress, token));
            return;
        }
        await LoginAccountInternalAsync();
    }

    [RelayCommand]
    private async Task LoginAccountAsync()
    {
        if (!IsSyncing)
        {
            await LoginAccountInternalAsync();
        }
    }

    private async Task LoginAccountInternalAsync()
    {
        try
        {
            var dialog = new EndfieldLoginDialog { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
            if (!string.IsNullOrWhiteSpace(dialog.LoginToken))
            {
                await AddAccountAndSyncAsync(dialog.LoginToken);
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

    private async Task AddAccountAndSyncAsync(string loginToken)
    {
        PrepareSync();
        try
        {
            var progress = new Progress<string>(text => StatusText = text);
            List<EndfieldGachaAccount> accounts = await _service.AddAccountsFromLoginTokenAsync(
                loginToken, progress, _syncCancellationTokenSource!.Token);
            EndfieldGachaAccount account = accounts[0];
            LoadAccounts(account.AccountKey);
            EndfieldGachaSyncResult result = await _service.SyncAccountAsync(
                account, progress, _syncCancellationTokenSource.Token);
            LoadAccounts(result.Account.AccountKey);
            ShowSyncResult(result);
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

    [RelayCommand]
    private async Task SyncFromGameLogAsync()
    {
        if (!IsSyncing)
        {
            await ExecuteSyncAsync((progress, token) => _service.SyncFromGameLogAsync(progress, token));
        }
    }

    private async Task ExecuteSyncAsync(
        Func<IProgress<string>, CancellationToken, Task<EndfieldGachaSyncResult>> syncOperation)
    {
        PrepareSync();
        try
        {
            var progress = new Progress<string>(text => StatusText = text);
            EndfieldGachaSyncResult result = await syncOperation(progress, _syncCancellationTokenSource!.Token);
            LoadAccounts(result.Account.AccountKey);
            ShowSyncResult(result);
        }
        catch (OperationCanceledException)
        {
            StatusSeverity = InfoBarSeverity.Informational;
            StatusText = "同步已取消。";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to sync Endfield gacha records: {ErrorType}", ex.GetType().Name);
            StatusSeverity = InfoBarSeverity.Error;
            StatusText = ex.Message;
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

    private void ShowSyncResult(EndfieldGachaSyncResult result)
    {
        if (result.FailedPools.Length == 0)
        {
            StatusSeverity = InfoBarSeverity.Success;
            StatusText = $"同步完成：新增 {result.NewCount} 条，共 {result.TotalCount} 条记录。";
        }
        else
        {
            StatusSeverity = InfoBarSeverity.Warning;
            StatusText = $"同步完成，但以下卡池获取失败：{string.Join("、", result.FailedPools)}。请稍后重试。";
        }
    }

    [RelayCommand]
    private void CancelSync()
    {
        _syncCancellationTokenSource?.Cancel();
    }
}
