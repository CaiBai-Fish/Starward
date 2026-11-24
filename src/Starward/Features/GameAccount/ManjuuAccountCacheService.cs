using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Starward.Features.GameAccount;

/// <summary>
/// 保存/恢复蓝色星原客户端的账号登录缓存（pcsdk.ini 与协议状态），用于账号切换。
/// 设备 uuid 与 device_id 不参与切换，避免触发设备验证。
/// </summary>
public sealed class ManjuuAccountCacheService
{

    private const string BackupFolderName = "manjuu_account_cache";

    /// <summary>参与切换的客户端文件，pcsdk.ini 保存登录凭据，agreement.json 保存协议同意状态。</summary>
    private static readonly string[] LoginFiles = ["pcsdk.ini", "agreement.json"];

    private static readonly string[] ExeNames = ["AzurPromilia"];

    private readonly ILogger<ManjuuAccountCacheService> _logger;


    public ManjuuAccountCacheService(ILogger<ManjuuAccountCacheService> logger)
    {
        _logger = logger;
    }


    /// <summary>客户端账号数据目录（%APPDATA%\AzurPromilia\Game_35_C000_SC000）。</summary>
    public static string GameDataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AzurPromilia", "Game_35_C000_SC000");


    private static string BackupRoot => Path.Combine(AppConfig.CacheFolder, BackupFolderName);


    public string GetBackupFolder(string key) => Path.Combine(BackupRoot, key);


    /// <summary>当前登录缓存的哈希；未登录或没有 pcsdk.ini 时返回 null。</summary>
    public string? GetCurrentCacheHash()
    {
        try
        {
            string file = Path.Combine(GameDataFolder, "pcsdk.ini");
            if (!File.Exists(file))
            {
                return null;
            }
            using FileStream fs = File.OpenRead(file);
            return Convert.ToHexStringLower(SHA256.HashData(fs));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read Manjuu login cache hash failed.");
            return null;
        }
    }


    public bool HasCache(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && File.Exists(Path.Combine(GetBackupFolder(key), "pcsdk.ini"));
    }


    /// <summary>备份当前登录缓存到 Starward 缓存目录。</summary>
    public void Save(string key)
    {
        try
        {
            string folder = GetBackupFolder(key);
            Directory.CreateDirectory(folder);
            int count = 0;
            foreach (string name in LoginFiles)
            {
                string source = Path.Combine(GameDataFolder, name);
                if (File.Exists(source))
                {
                    File.Copy(source, Path.Combine(folder, name), true);
                    count++;
                }
            }
            _logger.LogInformation("Save Manjuu account cache {key}: {count} files.", key, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save Manjuu account cache failed.");
        }
    }


    /// <summary>从 Starward 缓存恢复登录缓存（切换账号）。</summary>
    public void Restore(string key)
    {
        try
        {
            string folder = GetBackupFolder(key);
            if (!Directory.Exists(folder))
            {
                _logger.LogWarning("Manjuu account cache {key} not found.", key);
                return;
            }
            Directory.CreateDirectory(GameDataFolder);
            foreach (string name in LoginFiles)
            {
                string source = Path.Combine(folder, name);
                if (File.Exists(source))
                {
                    File.Copy(source, Path.Combine(GameDataFolder, name), true);
                }
            }
            _logger.LogInformation("Restore Manjuu account cache {key}.", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore Manjuu account cache failed.");
        }
    }


    public void Delete(string key)
    {
        try
        {
            string folder = GetBackupFolder(key);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delete Manjuu account cache failed.");
        }
    }


    /// <summary>清除登录缓存，用于新建账号前退出当前账号。</summary>
    public void ClearLoginCache()
    {
        try
        {
            File.Delete(Path.Combine(GameDataFolder, "pcsdk.ini"));
            string cef = Path.Combine(GameDataFolder, "cef_cache");
            if (Directory.Exists(cef))
            {
                Directory.Delete(cef, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clear Manjuu login cache failed.");
        }
    }


    public bool IsGameRunning()
    {
        try
        {
            return ExeNames.Any(name => Process.GetProcessesByName(name).Length > 0);
        }
        catch
        {
            return false;
        }
    }

}
