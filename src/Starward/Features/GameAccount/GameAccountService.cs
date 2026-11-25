using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Starward.Core;
using Starward.Core.Hypergryph;
using Starward.Core.Manjuu;
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
                    GameUid = x.Uid,
                    RoleId = x.RoleId,
                    Name = x.RoleName ?? "",
                    Value = Encoding.UTF8.GetBytes(x.AccountKey),
                    Time = DateTime.Now,
                    IsSaved = true,
                }).ToList();
        }
        if (ManjuuGameConstants.IsManjuuGame(biz))
        {
            return GetManjuuGameAccounts(biz);
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



    /// <summary>
    /// 蓝色星原：账号列表来自客户端 history.dat（解密后含 uid / phone / utoken）。
    /// </summary>
    private List<GameAccount> GetManjuuGameAccounts(GameBiz biz)
    {
        var cache = AppConfig.GetService<ManjuuAccountCacheService>();
        ManjuuAccountHistory? history = cache.ReadAccountHistory();
        List<GameAccount> list = [];
        if (history?.Items is { Count: > 0 } items)
        {
            foreach (ManjuuAccountInfo item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Uid))
                {
                    continue;
                }
                list.Add(new GameAccount
                {
                    SHA256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(item.Uid))),
                    GameBiz = biz,
                    GameUid = item.Uid,
                    Name = string.IsNullOrWhiteSpace(item.Phone) ? item.Uid : item.Phone,
                    Uid = 0,
                    Value = Encoding.UTF8.GetBytes(item.Uid),
                    Time = DateTime.Now,
                    IsSaved = true,
                });
            }
            string? currentUid = history.LastUser?.Uid;
            if (!string.IsNullOrWhiteSpace(currentUid))
            {
                GameAccount? current = list.FirstOrDefault(x => x.GameUid == currentUid);
                if (current is not null)
                {
                    list.Remove(current);
                    list.Insert(0, current);
                }
            }
            return list;
        }
        // 回退：没有 history.dat 时按登录缓存哈希显示未保存账号
        list = GetGameAccountsFromDatabase(biz).ToList();
        string? hash = cache.GetCurrentCacheHash();
        if (!string.IsNullOrWhiteSpace(hash))
        {
            GameAccount? current = list.FirstOrDefault(x => x.SHA256 == hash);
            if (current is not null)
            {
                current.Time = DateTime.Now;
                list.Remove(current);
                list.Insert(0, current);
            }
            else
            {
                list.Insert(0, new GameAccount
                {
                    SHA256 = hash,
                    GameBiz = biz,
                    Value = Encoding.UTF8.GetBytes(hash),
                    Name = "未保存账号",
                    Time = DateTime.Now,
                    IsSaved = false,
                });
            }
        }
        return list;
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
        if (HypergryphGameConstants.IsHypergryphGame(account.GameBiz))
        {
            SaveEndfieldAccountCache(account);
        }
        if (ManjuuGameConstants.IsManjuuGame(account.GameBiz))
        {
            AppConfig.GetService<ManjuuAccountCacheService>().Save(account.SHA256);
        }
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("INSERT OR REPLACE INTO GameAccount (SHA256, GameBiz, Uid, Name, Value, Time) VALUES (@SHA256, @GameBiz, @Uid, @Name, @Value, @Time);", account);
        _logger.LogInformation("Save account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }



    public void DeleteGameAccount(GameAccount account)
    {
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("DELETE FROM GameAccount WHERE SHA256=@SHA256;", account);
        if (HypergryphGameConstants.IsHypergryphGame(account.GameBiz))
        {
            AppConfig.GetService<EndfieldAccountCacheService>().Delete(GetEndfieldGameUid(account, fallbackToCurrent: false));
        }
        if (ManjuuGameConstants.IsManjuuGame(account.GameBiz))
        {
            AppConfig.GetService<ManjuuAccountCacheService>().Delete(account.SHA256);
        }
        _logger.LogInformation("Delete account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }


    private static string GetEndfieldGameUid(GameAccount account, bool fallbackToCurrent)
    {
        if (!string.IsNullOrWhiteSpace(account.GameUid))
        {
            return account.GameUid;
        }
        return fallbackToCurrent ? AppConfig.GetService<EndfieldAccountCacheService>().GetCurrentGameUid() ?? "" : "";
    }


    private void SaveEndfieldAccountCache(GameAccount account)
    {
        try
        {
            var cache = AppConfig.GetService<EndfieldAccountCacheService>();
            string gameUid = GetEndfieldGameUid(account, fallbackToCurrent: true);
            if (string.IsNullOrWhiteSpace(gameUid))
            {
                _logger.LogWarning("Save Endfield account cache skipped: game uid is unknown.");
                return;
            }
            string? currentUid = cache.GetCurrentGameUid();
            if (!string.IsNullOrWhiteSpace(currentUid) && !string.Equals(currentUid, gameUid, StringComparison.Ordinal))
            {
                _logger.LogWarning("Save Endfield account cache skipped: current game uid {CurrentUid} does not match account uid {GameUid}.", currentUid, gameUid);
                return;
            }
            cache.Save(gameUid, account.RoleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save Endfield account cache failed.");
        }
    }


    private void RestoreEndfieldAccountCache(GameAccount account)
    {
        try
        {
            string gameUid = GetEndfieldGameUid(account, fallbackToCurrent: false);
            if (string.IsNullOrWhiteSpace(gameUid))
            {
                return;
            }
            AppConfig.GetService<EndfieldAccountCacheService>().Restore(gameUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore Endfield account cache failed.");
        }
    }



    public void ChangeGameAccount(GameAccount account)
    {
        if (ManjuuGameConstants.IsManjuuGame(account.GameBiz))
        {
            var manjuuCache = AppConfig.GetService<ManjuuAccountCacheService>();
            string uid = account.GameUid ?? (account.Value is { Length: > 0 } ? Encoding.UTF8.GetString(account.Value) : "");
            if (!string.IsNullOrWhiteSpace(uid) && manjuuCache.SwitchAccount(uid))
            {
                return;
            }
            manjuuCache.Restore(account.SHA256);
            return;
        }
        if (HypergryphGameConstants.IsHypergryphGame(account.GameBiz))
        {
            AppConfig.EndfieldCurrentAccountKey = account.Value is { Length: > 0 } ? Encoding.UTF8.GetString(account.Value) : null;
            RestoreEndfieldAccountCache(account);
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
