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
        if (key == "account")
        {
            ShowAccount();
        }
        else
        {
            ShowGuide(key);
        }
    }


    private void ShowAccount()
    {
        Panel_Guide.Visibility = Visibility.Collapsed;
        Frame_Content.Visibility = Visibility.Visible;
        if (Frame_Content.SourcePageType?.Name != nameof(EndfieldAccountRecordPage))
        {
            Frame_Content.Navigate(typeof(EndfieldAccountRecordPage));
        }
    }


    private void ShowGuide(string key)
    {
        Frame_Content.Visibility = Visibility.Collapsed;
        Panel_Guide.Visibility = Visibility.Visible;
        (TextBlock_GuideTitle.Text, TextBlock_GuideDescription.Text) = key switch
        {
            "crisis" => ("危机合约", "危机合约的赛季记录、通关等级与奖励进度由森空岛终末地战绩页提供。"),
            "war" => ("战争回响", "战争回响的关卡进度与评分由森空岛终末地战绩页提供。"),
            _ => ("影拓丰碑", "影拓丰碑的通关记录与收集进度由森空岛终末地战绩页提供。"),
        };
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
