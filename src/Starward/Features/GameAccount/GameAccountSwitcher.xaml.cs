using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Core.Manjuu;
using Starward.Features.Gacha.Endfield;
using Starward.Features.GameLauncher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Starward.Features.GameAccount;

[INotifyPropertyChanged]
public sealed partial class GameAccountSwitcher : UserControl
{

    private readonly ILogger<GameAccountService> _logger = AppConfig.GetLogger<GameAccountService>();


    private readonly GameAccountService _gameAccountService = AppConfig.GetService<GameAccountService>();

    private readonly EndfieldAccountCacheService _endfieldAccountCacheService = AppConfig.GetService<EndfieldAccountCacheService>();

    private readonly ManjuuAccountCacheService _manjuuAccountCacheService = AppConfig.GetService<ManjuuAccountCacheService>();

    private readonly EndfieldGachaService _endfieldGachaService = AppConfig.GetService<EndfieldGachaService>();

    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();

    private string? _newAccountBaselineUid;

    private const string NewAccountPlaceholderKey = "__new_account__";


    public GameAccountSwitcher()
    {
        this.InitializeComponent();
        this.Loaded += GameAccountSwitcher_Loaded;
        this.Unloaded += GameAccountSwitcher_Unloaded;
    }



    private void GameAccountSwitcher_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateGameAccount();
    }


    private void GameAccountSwitcher_Unloaded(object sender, RoutedEventArgs e)
    {
        SelectGameAccount = null;
        GameAccountList = null!;
        suggestionUids = null!;
    }



    public GameBiz CurrentGameBiz { get; set; }


    public GameId? CurrentGameId { get; set; }


    /// <summary>仅终末地支持客户端账号缓存的新建/切换。</summary>
    public bool IsEndfield => CurrentGameBiz.Game == GameBiz.endfield;


    /// <summary>蓝色星原支持客户端登录缓存（pcsdk.ini）的新建/切换。</summary>
    public bool IsManjuu => CurrentGameBiz.Game == GameBiz.azurpromilia;


    /// <summary>游戏是否支持客户端缓存形式的账号新建/切换。</summary>
    public bool CanCacheSwitch => IsEndfield || IsManjuu;


    /// <summary>是否处于“新建账号信息”流程中（已清空登录缓存并启动游戏，等待用户登录后完成）。</summary>
    public bool IsNewAccountFlow { get; set => SetProperty(ref field, value); }



    public List<GameAccount> GameAccountList { get; set => SetProperty(ref field, value); }


    public GameAccount? SelectGameAccount
    {
        get; set
        {
            if (SetProperty(ref field, value) && value is not null)
            {
                if (value.SHA256 == NewAccountPlaceholderKey)
                {
                    // 选中“新建账号”占位项时启动新建流程，随后把选中项恢复到真实账号。
                    CanChangeGameAccount = false;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        NewGameAccountCommand.Execute(null);
                        SelectGameAccount = GameAccountList?.FirstOrDefault(x => x.SHA256 != NewAccountPlaceholderKey);
                        CanChangeGameAccount = false;
                    });
                    return;
                }
                CanChangeGameAccount = true;
            }
        }
    }


    public bool CanChangeGameAccount { get; set => SetProperty(ref field, value); }


    public void UpdateGameAccount()
    {
        try
        {
            List<GameAccount> list = _gameAccountService.GetGameAccounts(CurrentGameBiz).ToList();
            if (CanCacheSwitch)
            {
                list.Add(new GameAccount
                {
                    SHA256 = NewAccountPlaceholderKey,
                    GameBiz = CurrentGameBiz,
                    Name = "新建账号",
                    IsSaved = false,
                });
            }
            GameAccountList = list;
            SelectGameAccount = GameAccountList.FirstOrDefault(x => x.SHA256 != NewAccountPlaceholderKey);
            CanChangeGameAccount = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot get game account ({biz})", CurrentGameBiz);
        }
    }


    [RelayCommand]
    private void ChangeGameAccount()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                _gameAccountService.ChangeGameAccount(SelectGameAccount);
                CanChangeGameAccount = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot change game {biz} account to {name}", CurrentGameBiz, SelectGameAccount?.Name);
        }
    }


    [RelayCommand]
    private async Task SaveGameAccountAsync()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                var acc = SelectGameAccount;
                if (GameAccountList.FirstOrDefault(x => x.SHA256 != acc.SHA256 && x.Uid == acc.Uid) is GameAccount lacc)
                {
                    var dialog = new ContentDialog
                    {
                        Title = Lang.Common_Attention,
                        Content = string.Format(Lang.LauncherPage_AccountSaveNew, acc.Uid),
                        PrimaryButtonText = Lang.LauncherPage_Replace,
                        SecondaryButtonText = Lang.LauncherPage_SaveNew,
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot,
                    };
                    if (await dialog.ShowAsync() is ContentDialogResult.Primary)
                    {
                        GameAccountList.Remove(lacc);
                        _gameAccountService.DeleteGameAccount(lacc);
                    }
                }
                SelectGameAccount.Time = DateTime.Now;
                _gameAccountService.SaveGameAccount(SelectGameAccount);
                FontIcon_SaveGameAccount.Glyph = "\uE8FB";
                await Task.Delay(3000);
                FontIcon_SaveGameAccount.Glyph = "\uE74E";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save game account");
        }
    }


    [RelayCommand]
    private void DeleteGameAccount()
    {
        try
        {
            if (SelectGameAccount is not null)
            {
                _gameAccountService.DeleteGameAccount(SelectGameAccount);
                UpdateGameAccount();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete game account");
        }
    }


    [RelayCommand]
    private async Task NewGameAccountAsync()
    {
        try
        {
            var confirm = new ContentDialog
            {
                Title = "新建账号信息",
                Content = "将备份当前账号的客户端缓存，并清除游戏登录状态，然后启动游戏。\n\n请在游戏内登录新账号；登录成功后关闭游戏，回到 Starward 点击「完成新建账号」。",
                PrimaryButtonText = "备份并启动游戏",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };
            if (await confirm.ShowAsync() is not ContentDialogResult.Primary)
            {
                return;
            }

            if (IsManjuu)
            {
                _newAccountBaselineUid = _manjuuAccountCacheService.GetCurrentCacheHash();
                if (!string.IsNullOrWhiteSpace(_newAccountBaselineUid))
                {
                    _manjuuAccountCacheService.Save(_newAccountBaselineUid);
                }
                _manjuuAccountCacheService.ClearLoginCache();
            }
            else
            {
                var cache = _endfieldAccountCacheService;
                _newAccountBaselineUid = cache.GetCurrentGameUid();
                if (!string.IsNullOrWhiteSpace(_newAccountBaselineUid))
                {
                    cache.Save(_newAccountBaselineUid);
                }
                cache.ClearLoginCache();
            }

            GameId? gameId = CurrentGameId ?? GameId.FromGameBiz(CurrentGameBiz);
            if (gameId is null)
            {
                throw new InvalidOperationException("无法确定当前游戏。");
            }
            var process = await _gameLauncherService.StartGameAsync(gameId);
            if (process is null)
            {
                throw new InvalidOperationException("游戏启动失败。");
            }
            IsNewAccountFlow = true;

            var tip = new ContentDialog
            {
                Title = "请在游戏内登录新账号",
                Content = "游戏已启动。请使用新账号完成登录，然后关闭游戏；回到 Starward 后点击「完成新建账号」。",
                CloseButtonText = "知道了",
                XamlRoot = this.XamlRoot,
            };
            await tip.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start new Endfield account flow");
            await ShowMessageAsync("新建账号失败", ex.Message);
        }
    }


    [RelayCommand]
    private async Task CompleteNewGameAccountAsync()
    {
        try
        {
            if (IsManjuu)
            {
                string? currentHash = _manjuuAccountCacheService.GetCurrentCacheHash();
                if (string.IsNullOrWhiteSpace(currentHash) || string.Equals(currentHash, _newAccountBaselineUid, StringComparison.Ordinal))
                {
                    await ShowMessageAsync("未检测到新账号", "请先在游戏内登录新账号并关闭游戏，再点击「完成新建账号」。");
                    return;
                }
                if (_manjuuAccountCacheService.IsGameRunning())
                {
                    await ShowMessageAsync("请先退出游戏", "完成新建前请关闭游戏。");
                    return;
                }
                _manjuuAccountCacheService.Save(currentHash);
                List<GameAccount> existing = _gameAccountService.GetGameAccounts(CurrentGameBiz);
                int index = existing.Count(x => x.IsSaved) + 1;
                var manjuuAccount = new GameAccount
                {
                    SHA256 = currentHash,
                    GameBiz = CurrentGameBiz,
                    Uid = 0,
                    Name = $"账号 {index}",
                    Value = Encoding.UTF8.GetBytes(currentHash),
                    Time = DateTime.Now,
                    IsSaved = true,
                };
                _gameAccountService.SaveGameAccount(manjuuAccount);
                IsNewAccountFlow = false;
                _newAccountBaselineUid = null;
                UpdateGameAccount();
                await ShowMessageAsync("新建账号成功", $"已保存账号：{manjuuAccount.Name}");
                return;
            }
            var cache = _endfieldAccountCacheService;
            string? currentUid = cache.GetCurrentGameUid();
            string? currentRoleId = cache.GetCurrentRoleId();
            if (string.IsNullOrWhiteSpace(currentUid) || string.Equals(currentUid, _newAccountBaselineUid, StringComparison.Ordinal))
            {
                await ShowMessageAsync("未检测到新账号", "请先在游戏内登录新账号并关闭游戏，再点击「完成新建账号」。");
                return;
            }
            if (cache.IsGameRunning())
            {
                await ShowMessageAsync("请先退出游戏", "完成新建前请关闭游戏。");
                return;
            }

            var login = new EndfieldLoginDialog { XamlRoot = this.XamlRoot };
            await login.ShowAsync();
            string? token = login.LoginToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                await ShowMessageAsync("未获取登录凭证", "请使用与游戏内新账号相同的鹰角账号登录。");
                return;
            }

            List<EndfieldGachaAccount> accounts = await _endfieldGachaService.AddAccountsFromLoginTokenAsync(token);
            EndfieldGachaAccount? account = accounts.FirstOrDefault(x => x.Uid == currentUid)
                ?? accounts.FirstOrDefault(x => x.RoleId == currentRoleId)
                ?? accounts.FirstOrDefault();
            if (account is null)
            {
                await ShowMessageAsync("未找到角色", "鹰角账号下没有匹配的终末地角色信息。");
                return;
            }

            cache.Save(currentUid, account.RoleId);
            IsNewAccountFlow = false;
            _newAccountBaselineUid = null;
            UpdateGameAccount();
            SelectGameAccount = GameAccountList.FirstOrDefault(x => x.SHA256 == account.AccountKey) ?? GameAccountList.FirstOrDefault();
            await ShowMessageAsync("新建账号成功", $"已保存账号：{account.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Complete new Endfield account flow");
            await ShowMessageAsync("完成新建失败", ex.Message);
        }
    }


    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "确定",
            XamlRoot = this.XamlRoot,
        };
        await dialog.ShowAsync();
    }



    private TextBox TextBox_AccountUid;


    private List<string> suggestionUids;


    private void AutoSuggestBox_Uid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (TextBox_AccountUid is null)
            {
                var ele1 = VisualTreeHelper.GetChild(AutoSuggestBox_Uid, 0);
                var ele = VisualTreeHelper.GetChild(ele1, 0);
                if (ele is TextBox textBox)
                {
                    TextBox_AccountUid = textBox;
                    TextBox_AccountUid.InputScope = new InputScope { Names = { new InputScopeName { NameValue = InputScopeNameValue.Number } } };
                    TextBox_AccountUid.BeforeTextChanging += TextBox_AccountUid_BeforeTextChanging;
                }
            }
        }
        catch { }
    }


    private void AutoSuggestBox_Uid_GotFocus(object sender, RoutedEventArgs e)
    {
        try
        {
            suggestionUids = _gameAccountService.GetSuggestionUids(CurrentGameBiz).Select(x => x.ToString()).ToList();
            UpdateSuggestionUids(AutoSuggestBox_Uid.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get suggestion uids");
        }
    }


    private void AutoSuggestBox_Uid_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        try
        {
            if (args.Reason is AutoSuggestionBoxTextChangeReason.UserInput)
            {
                UpdateSuggestionUids(sender.Text);
            }
        }
        catch { }
    }


    private void TextBox_AccountUid_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        args.Cancel = !args.NewText.All(x => char.IsDigit(x));
    }


    private void UpdateSuggestionUids(string text)
    {
        try
        {
            if (suggestionUids != null && suggestionUids.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    AutoSuggestBox_Uid.ItemsSource = suggestionUids;
                    AutoSuggestBox_Uid.IsSuggestionListOpen = true;
                }
                else
                {
                    var list = suggestionUids.Where(x => x != text && x.StartsWith(text)).ToList();
                    if (list.Count == 0)
                    {
                        AutoSuggestBox_Uid.IsSuggestionListOpen = false;
                    }
                    else
                    {
                        if (!(AutoSuggestBox_Uid.ItemsSource is List<string> source && source.SequenceEqual(list)))
                        {
                            AutoSuggestBox_Uid.ItemsSource = list;
                        }
                        AutoSuggestBox_Uid.IsSuggestionListOpen = true;
                    }
                }
            }
        }
        catch { }
    }


}
