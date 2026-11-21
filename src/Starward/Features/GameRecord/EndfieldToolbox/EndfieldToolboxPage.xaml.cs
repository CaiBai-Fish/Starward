using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Features.Gacha.Endfield;
using Starward.Features.GameRecord.ArknightsRecord;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace Starward.Features.GameRecord.EndfieldToolbox;

public sealed partial class EndfieldToolboxPage : Starward.Frameworks.PageBase
{
    private readonly EndfieldGachaService _gachaService = AppConfig.GetService<EndfieldGachaService>();

    private EndfieldAccountRecordPage? _accountPage;

    private string _currentCategory = "account";

    private bool _updatingAccounts;

    private EndfieldGachaAccount? _selectedAccount;


    public ObservableCollection<EndfieldGachaAccount> Accounts { get; } = [];


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
    }


    private void UpdateHeader(EndfieldGachaAccount? account)
    {
        if (account is null)
        {
            TextBlock_RoleName.Text = "未登录";
            TextBlock_RoleInfo.Text = "终末地 · 鹰角账号";
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
        if (_currentCategory is "crisis" or "war" or "monument")
        {
            ShowRecordPage(_currentCategory);
        }
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
            ShowRecordPage(key);
        }
    }


    private void ShowAccount()
    {
        Frame_Content.Visibility = Visibility.Visible;
        if (Frame_Content.SourcePageType?.Name != nameof(EndfieldAccountRecordPage))
        {
            Frame_Content.Navigate(typeof(EndfieldAccountRecordPage));
        }
    }


    private void ShowRecordPage(string key)
    {
        Frame_Content.Visibility = Visibility.Visible;
        Type pageType = key switch
        {
            "crisis" => typeof(CrisisContractPage),
            "war" => typeof(WarEchoPage),
            _ => typeof(UmbralMonumentPage),
        };
        Frame_Content.Navigate(pageType, _selectedAccount);
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
        else
        {
            _accountPage = null;
        }
    }


    private async void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        switch (Frame_Content.Content)
        {
            case EndfieldAccountRecordPage page:
                await page.RefreshAsync();
                break;
            case CrisisContractPage crisis:
                await crisis.RefreshAsync();
                break;
            case WarEchoPage war:
                await war.RefreshAsync();
                break;
            case UmbralMonumentPage monument:
                await monument.RefreshAsync();
                break;
            default:
                LoadAccounts();
                break;
        }
    }


    private async void Button_WebLogin_Click(object sender, RoutedEventArgs e)
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
            else if (_currentCategory is "crisis" or "war" or "monument")
            {
                ShowRecordPage(_currentCategory);
            }
        }
        catch (Exception ex)
        {
            TextBlock_RoleInfo.Text = ex.Message;
        }
    }


    private async void Button_CookieLogin_Click(object sender, RoutedEventArgs e)
    {
        TextBox textBox = new()
        {
            PlaceholderText = "粘贴包含 loginToken 的 Cookie",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            MaxHeight = 220,
        };
        ContentDialog dialog = new()
        {
            Title = "Cookies 登录",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "从森空岛网页版复制完整 Cookie，需包含 loginToken。", TextWrapping = TextWrapping.Wrap },
                    textBox,
                },
            },
            PrimaryButtonText = "登录",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }
        try
        {
            await _gachaService.AddAccountsFromCookieAsync(textBox.Text);
            LoadAccounts();
            if (_currentCategory is "crisis" or "war" or "monument")
            {
                ShowRecordPage(_currentCategory);
            }
        }
        catch (Exception ex)
        {
            ContentDialog errorDialog = new()
            {
                Title = "Cookies 登录失败",
                Content = ex.Message,
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };
            await errorDialog.ShowAsync();
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
}
