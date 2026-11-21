using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Features.Gacha.Endfield;
using Starward.Features.GameRecord.ArknightsRecord;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Starward.Features.GameRecord.EndfieldToolbox;

public sealed class EndfieldRecordCard
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Value { get; set; } = "";
}


public sealed partial class EndfieldToolboxPage : Starward.Frameworks.PageBase
{
    private readonly EndfieldGachaService _gachaService = AppConfig.GetService<EndfieldGachaService>();

    private readonly EndfieldRecordService _recordService = AppConfig.GetService<EndfieldRecordService>();

    private CancellationTokenSource? _recordCancellationTokenSource;

    private EndfieldAccountRecordPage? _accountPage;

    private string _currentCategory = "account";

    private bool _updatingAccounts;

    private EndfieldGachaAccount? _selectedAccount;


    public ObservableCollection<EndfieldGachaAccount> Accounts { get; } = [];

    public ObservableCollection<EndfieldRecordCard> NativeRecords { get; } = [];


    public EndfieldToolboxPage()
    {
        InitializeComponent();
        Frame_Content.Navigated += Frame_Content_Navigated;
    }


    protected override void OnLoaded()
    {
        LoadAccounts();
        if (NavigationView_Toolbox.MenuItems.OfType<NavigationViewItem>().FirstOrDefault() is NavigationViewItem first)
        {
            NavigationView_Toolbox.SelectedItem = first;
        }
        ShowAccount();
    }


    private void LoadAccounts()
    {
        List<EndfieldGachaAccount> accounts = _gachaService.GetAccounts();
        _updatingAccounts = true;
        Accounts.Clear();
        foreach (EndfieldGachaAccount account in accounts)
        {
            Accounts.Add(account);
        }
        _selectedAccount = accounts.FirstOrDefault();
        ListView_Accounts.SelectedItem = _selectedAccount;
        _updatingAccounts = false;
        UpdateHeader(_selectedAccount);
        Button_Login.Visibility = _selectedAccount is null ? Visibility.Visible : Visibility.Collapsed;
        Button_Accounts.Visibility = _selectedAccount is null ? Visibility.Collapsed : Visibility.Visible;
    }


    private void UpdateHeader(EndfieldGachaAccount? account)
    {
        if (account is null)
        {
            TextBlock_RoleName.Text = "终末地";
            TextBlock_RoleInfo.Text = "未登录鹰角账号";
        }
        else
        {
            TextBlock_RoleName.Text = string.IsNullOrWhiteSpace(account.RoleName) ? account.Uid : account.RoleName;
            TextBlock_RoleInfo.Text = string.IsNullOrWhiteSpace(account.ServerName)
                ? $"UID {account.Uid}"
                : $"{account.ServerName} · UID {account.Uid}";
        }
    }


    private void ListView_Accounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingAccounts || ListView_Accounts.SelectedItem is not EndfieldGachaAccount account)
        {
            return;
        }
        _selectedAccount = account;
        UpdateHeader(account);
        _accountPage?.SelectAccount(account.AccountKey);
    }


    private void NavigationView_Toolbox_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is not string key)
        {
            return;
        }
        _currentCategory = key;
        if (key == "h5")
        {
            ArknightsGameDataWindow.ShowWindow("endfield");
        }
        else if (key == "account")
        {
            ShowAccount();
        }
        else
        {
            _ = ShowCategoryAsync(key);
        }
    }


    private void ShowAccount()
    {
        Panel_Guide.Visibility = Visibility.Collapsed;
        Panel_Records.Visibility = Visibility.Collapsed;
        Frame_Content.Visibility = Visibility.Visible;
        if (Frame_Content.SourcePageType?.Name != nameof(EndfieldAccountRecordPage))
        {
            Frame_Content.Navigate(typeof(EndfieldAccountRecordPage));
        }
    }


    private void ShowGuide(string key)
    {
        Frame_Content.Visibility = Visibility.Collapsed;
        Panel_Records.Visibility = Visibility.Collapsed;
        Panel_Guide.Visibility = Visibility.Visible;
        (TextBlock_GuideTitle.Text, TextBlock_GuideDescription.Text) = key switch
        {
            "crisis" => ("危机合约", "危机合约的赛季记录、通关等级与奖励进度由森空岛终末地战绩页提供。"),
            "war" => ("战争回响", "战争回响的关卡进度与评分由森空岛终末地战绩页提供。"),
            _ => ("影拓丰碑", "影拓丰碑的通关记录与收集进度由森空岛终末地战绩页提供。"),
        };
    }


    private async Task ShowCategoryAsync(string key)
    {
        _recordCancellationTokenSource?.Cancel();
        _recordCancellationTokenSource?.Dispose();
        _recordCancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _recordCancellationTokenSource.Token;
        Frame_Content.Visibility = Visibility.Collapsed;
        Panel_Guide.Visibility = Visibility.Collapsed;
        Panel_Records.Visibility = Visibility.Visible;
        NativeRecords.Clear();
        EndfieldGachaAccount? account = _selectedAccount;
        if (account is null)
        {
            ShowGuide(key);
            TextBlock_GuideTitle.Text = "未登录终末地账号";
            TextBlock_GuideDescription.Text = "请先在角色下拉中登录终末地账号。";
            return;
        }
        NativeRecords.Add(new EndfieldRecordCard { Title = "正在读取记录..." });
        try
        {
            using JsonDocument document = key switch
            {
                "crisis" => await _recordService.GetCrisisContractAsync(account, cancellationToken),
                "war" => await _recordService.GetWarEchoesAsync(account, cancellationToken),
                _ => await _recordService.GetUmbralMonumentAsync(account, cancellationToken),
            };
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            NativeRecords.Clear();
            switch (key)
            {
                case "crisis":
                    ParseCrisisContract(document.RootElement);
                    break;
                case "war":
                    ParseWarEchoes(document.RootElement);
                    break;
                default:
                    ParseUmbralMonument(document.RootElement);
                    break;
            }
            if (NativeRecords.Count == 0)
            {
                NativeRecords.Add(new EndfieldRecordCard { Title = "暂无记录", Subtitle = "当前账号没有可显示的终末地战绩。" });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            NativeRecords.Clear();
            ShowGuide(key);
            TextBlock_GuideTitle.Text = "读取失败";
            TextBlock_GuideDescription.Text = ex.Message;
        }
    }


    private void ParseWarEchoes(JsonElement root)
    {
        if (!TryGet(root, "data", out JsonElement data) || !TryGet(data, "warEchoes", out JsonElement warEchoes))
        {
            return;
        }
        if (TryGet(warEchoes, "seasons", out JsonElement seasons) && seasons.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement season in seasons.EnumerateArray())
            {
                string name = JsString(season, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = $"赛季 {JsString(season, "id")}";
                }
                string start = FormatTime(JsString(season, "startAtTs"));
                string end = FormatTime(JsString(season, "endAtTs"));
                int weeks = TryGet(season, "weeks", out JsonElement weekArray) && weekArray.ValueKind is JsonValueKind.Array ? weekArray.GetArrayLength() : 0;
                NativeRecords.Add(new EndfieldRecordCard
                {
                    Title = name,
                    Subtitle = string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end) ? $"轮换 {weeks} 期" : $"{start} - {end} · {weeks} 期",
                    Value = JsInt(season, "highest") is int highest && highest > 0 ? $"最高 {highest}" : "",
                });
            }
        }
        if (TryGet(warEchoes, "achieves", out JsonElement achieves) && achieves.ValueKind is JsonValueKind.Array && achieves.GetArrayLength() > 0)
        {
            NativeRecords.Add(new EndfieldRecordCard
            {
                Title = "成就",
                Subtitle = "战争回响成就进度",
                Value = achieves.GetArrayLength().ToString(),
            });
        }
    }


    private void ParseCrisisContract(JsonElement root)
    {
        if (!TryGet(root, "data", out JsonElement data) || !TryGet(data, "crisisContract", out JsonElement contract))
        {
            return;
        }
        if (TryGet(contract, "status", out JsonElement status))
        {
            string name = JsString(status, "name");
            NativeRecords.Add(new EndfieldRecordCard
            {
                Title = string.IsNullOrWhiteSpace(name) ? "危机合约" : name,
                Subtitle = $"{FormatTime(JsString(status, "startAtTs"))} - {FormatTime(JsString(status, "endAtTs"))}",
                Value = JsInt(status, "highest") is int highest && highest > 0 ? $"最高 {highest}" : "",
            });
        }
        if (TryGet(contract, "history", out JsonElement history) && TryGet(history, "records", out JsonElement records)
            && records.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement record in records.EnumerateArray())
            {
                bool isBest = TryGet(record, "isBest", out JsonElement best) && best.ValueKind is JsonValueKind.True;
                bool passed = TryGet(record, "isPass", out JsonElement pass) && pass.ValueKind is JsonValueKind.True;
                int passWave = JsInt(record, "passWave");
                string time = JsString(record, "passTs");
                if (string.IsNullOrWhiteSpace(time))
                {
                    time = JsString(record, "ts");
                }
                NativeRecords.Add(new EndfieldRecordCard
                {
                    Title = isBest ? "最佳记录" : "历史记录",
                    Subtitle = $"{FormatTime(time)}{(passWave > 0 ? $" · 波次 {passWave}" : "")}",
                    Value = passed ? "已通关" : "",
                });
            }
        }
    }


    private void ParseUmbralMonument(JsonElement root)
    {
        if (!TryGet(root, "data", out JsonElement data)
            || !TryGet(data, "indieHard", out JsonElement indieHard)
            || !TryGet(indieHard, "indieHardGroups", out JsonElement groups)
            || groups.ValueKind is not JsonValueKind.Array)
        {
            return;
        }
        foreach (JsonElement group in groups.EnumerateArray())
        {
            string name = JsString(group, "showName");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = JsString(group, "name");
            }
            string activity = JsString(group, "activityName");
            bool achieved = TryGet(group, "achieve", out JsonElement achieve)
                            && TryGet(achieve, "isAchieved", out JsonElement isAchieved)
                            && isAchieved.ValueKind is JsonValueKind.True;
            NativeRecords.Add(new EndfieldRecordCard
            {
                Title = string.IsNullOrWhiteSpace(name) ? "影拓丰碑" : name,
                Subtitle = string.IsNullOrWhiteSpace(activity) ? "影拓丰碑" : activity,
                Value = achieved ? "已达成" : "",
            });

        }
    }


    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }
        value = default;
        return false;
    }


    private static string JsString(JsonElement element, string name)
    {
        if (!TryGet(element, name, out JsonElement value))
        {
            return "";
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => "",
        };
    }


    private static int JsInt(JsonElement element, string name)
    {
        if (!TryGet(element, name, out JsonElement value))
        {
            return 0;
        }
        if (value.ValueKind is JsonValueKind.Number && value.TryGetInt32(out int number))
        {
            return number;
        }
        return int.TryParse(value.ToString(), out number) ? number : 0;
    }


    private static string FormatTime(string value)
    {
        if (long.TryParse(value, out long timestamp) && timestamp > 0)
        {
            if (timestamp > 100000000000)
            {
                timestamp /= 1000;
            }
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("yyyy-MM-dd");
        }
        return value;
    }


    private void Frame_Content_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is EndfieldAccountRecordPage page)
        {
            _accountPage = page;
            page.SetEmbedded(true);
            if (!string.IsNullOrWhiteSpace(_selectedAccount?.AccountKey))
            {
                page.SelectAccount(_selectedAccount.AccountKey);
            }
        }
    }


    private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (Frame_Content.Visibility == Visibility.Visible && _accountPage is not null)
        {
            await _accountPage.RefreshAsync();
        }
        else
        {
            LoadAccounts();
        }
    }


    private async void Button_Login_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            EndfieldLoginDialog dialog = new() { XamlRoot = this.XamlRoot };
            await dialog.ShowAsync();
            if (string.IsNullOrWhiteSpace(dialog.LoginToken))
            {
                return;
            }
            await _gachaService.AddAccountsFromLoginTokenAsync(dialog.LoginToken);
            LoadAccounts();
            if (Frame_Content.Visibility == Visibility.Visible && _accountPage is not null)
            {
                if (!string.IsNullOrWhiteSpace(_selectedAccount?.AccountKey))
                {
                    _accountPage.SelectAccount(_selectedAccount.AccountKey);
                }
                await _accountPage.RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            TextBlock_RoleInfo.Text = ex.Message;
        }
    }


    private void Button_OpenGuide_Click(object sender, RoutedEventArgs e)
    {
        ArknightsGameDataWindow.ShowWindow("endfield");
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
}
