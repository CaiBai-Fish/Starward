using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Starward.Core;
using Starward.Core.Hypergryph;
using Starward.Features.Database;
using Starward.Features.Gacha.Endfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Starward.Features.GameAccount;

public class GameAccountService
{

    private readonly ILogger<GameAccountService> _logger;



    public GameAccountService(ILogger<GameAccountService> logger)
    {
        _logger = logger;
    }



    public GameAccount? GetGameAccountFromRegistry(GameBiz biz)
    {
        string key = biz.GetGameRegistryKey();
        string keyName = biz.Server switch
        {
            "cn" => GameRegistry.MIHOYOSDK_ADL_PROD_CN_h3123967166,
            "global" => GameRegistry.MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810,
            _ => throw new ArgumentOutOfRangeException($"Unknown region {biz}"),
        };
        byte[]? adl = Registry.GetValue(key, keyName, null) as byte[];
        if (adl != null)
        {
            var account = new GameAccount
            {
                SHA256 = Convert.ToHexString(SHA256.HashData(adl)),
                GameBiz = biz,
                Value = adl,
                Name = "-",
            };
            if (biz.Game is GameBiz.bh3)
            {
                account.Uid = (int)(Registry.GetValue(key, GameRegistry.GENERAL_DATA_V2_LastLoginUserId_h47158221, 0) ?? 0);
            }
            if (biz.Game is GameBiz.hk4e)
            {
                byte[]? uidBytes = Registry.GetValue(key, GameRegistry.__LastUid___h2153286551, 0) as byte[];
                if (uidBytes is not null)
                {
                    // 该键写入的是 "uid\0"，Trim 不会移除 \0，需要显式截断
                    string uidStr = Encoding.UTF8.GetString(uidBytes).TrimEnd('\0').Trim();
                    if (long.TryParse(uidStr, out long uid))
                    {
                        account.Uid = uid;
                    }
                }
            }
            if (biz.Game is GameBiz.hkrpg)
            {
                account.Uid = (int)(Registry.GetValue(key, GameRegistry.App_LastUserID_h2841727341, 0) ?? 0);
            }
            return account;
        }
        return null;
    }



    public IEnumerable<GameAccount> GetGameAccountsFromDatabase(GameBiz biz)
    {
        using var dapper = DatabaseService.CreateConnection();
        var list = dapper.Query<GameAccount>("SELECT * FROM GameAccount WHERE GameBiz = @biz;", new { biz });
        foreach (var item in list)
        {
            item.IsSaved = true;
            yield return item;
        }
    }



    public List<GameAccount> GetGameAccounts(GameBiz biz)
    {
        if (HypergryphGameConstants.IsHypergryphGame(biz))
        {
            return AppConfig.GetService<EndfieldGachaService>().GetAccounts()
                .Select(x => new GameAccount
                {
                    SHA256 = x.AccountKey,
                    GameBiz = biz,
                    Uid = long.TryParse(x.RoleId, out long uid) ? uid : 0,
                    Name = x.RoleName ?? "",
                    Value = Encoding.UTF8.GetBytes(x.AccountKey),
                    Time = DateTime.Now,
                    IsSaved = true,
                }).ToList();
        }
        var databaseAccounts = GetGameAccountsFromDatabase(biz).ToList();
        var regAccount = GetGameAccountFromRegistry(biz);
        if (regAccount != null)
        {
            GameAccount? current = databaseAccounts.FirstOrDefault(x => x.SHA256 == regAccount.SHA256);
            if (current is null && regAccount.Uid > 0)
            {
                // 登录凭据会随每次登录刷新，SHA256 随之变化；按 UID 认领已保存的账号，
                // 避免当前账号变成未保存条目、或保存时多出同 UID 的重复账号
                current = databaseAccounts.FirstOrDefault(x => x.Uid == regAccount.Uid);
            }
            if (current is null && regAccount.Uid == 0 && databaseAccounts.Count == 1)
            {
                // 绝区零等游戏不在注册表写入登录 UID，单账号时认领唯一记录
                current = databaseAccounts[0];
            }
            if (current is not null)
            {
                current.Value = regAccount.Value;
                current.Time = regAccount.Time;
            }
            if (current is not null)
            {
                databaseAccounts.Remove(current);
                databaseAccounts.Insert(0, current);
            }
            else
            {
                databaseAccounts.Insert(0, regAccount);
            }
        }
        return databaseAccounts;
    }



    public IEnumerable<int> GetSuggestionUids(GameBiz biz)
    {
        using var dapper = DatabaseService.CreateConnection();
        List<int> uids = dapper.Query<int>("SELECT DISTINCT Uid FROM GameAccount WHERE GameBiz = @biz AND Uid > 0;", new { biz }).ToList();
        return uids.Distinct().Order();
    }



    public void SaveGameAccount(GameAccount account)
    {
        account.Name ??= "";
        account.IsSaved = true;
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("INSERT OR REPLACE INTO GameAccount (SHA256, GameBiz, Uid, Name, Value, Time) VALUES (@SHA256, @GameBiz, @Uid, @Name, @Value, @Time);", account);
        _logger.LogInformation("Save account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }



    public void DeleteGameAccount(GameAccount account)
    {
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("DELETE FROM GameAccount WHERE SHA256=@SHA256;", account);
        _logger.LogInformation("Delete account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }



    public void ChangeGameAccount(GameAccount account)
    {
        if (HypergryphGameConstants.IsHypergryphGame(account.GameBiz))
        {
            AppConfig.EndfieldCurrentAccountKey = account.Value is { Length: > 0 } ? Encoding.UTF8.GetString(account.Value) : null;
            return;
        }
        string key = account.GameBiz.GetGameRegistryKey();
        string keyName = account.GameBiz.Server switch
        {
            "cn" => GameRegistry.MIHOYOSDK_ADL_PROD_CN_h3123967166,
            "global" => GameRegistry.MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810,
            _ => throw new ArgumentOutOfRangeException($"Unknown region {account.GameBiz}"),
        };
        Registry.SetValue(key, keyName, account.Value);
        if (account.GameBiz.Game is GameBiz.hkrpg && account.Uid > 0)
        {
            Registry.SetValue(key, GameRegistry.App_LastUserID_h2841727341, (int)account.Uid, RegistryValueKind.DWord);
        }
        if (account.GameBiz.Game is GameBiz.bh3 && account.Uid > 0)
        {
            Registry.SetValue(key, GameRegistry.GENERAL_DATA_V2_LastLoginUserId_h47158221, (int)account.Uid, RegistryValueKind.DWord);
        }
        if (account.GameBiz.Game is GameBiz.hk4e && account.Uid > 0)
        {
            Registry.SetValue(key, GameRegistry.__LastUid___h2153286551, Encoding.UTF8.GetBytes($"{account.Uid}\0"));
        }
        _logger.LogInformation("Change account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }


}
