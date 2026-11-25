using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Starward.Features.GameAccount;

/// <summary>
/// 保存/恢复蓝色星原客户端的账号数据（history.dat 中的账号历史与登录态），用于账号切换。
/// history.dat 使用 AES-256-CBC 加密：key = deviceId 的 UTF-8 前 32 字节，iv = deviceId 十六进制解码，
/// deviceId 从游戏目录 pcsdk_log.txt 的 "device id: " 日志行获取。
/// </summary>
public sealed class ManjuuAccountCacheService
{

    private const string BackupFolderName = "manjuu_account_cache";

    private const string GameRegistryPath = @"SOFTWARE\AzurPromilia\35_C000";

    private const string MagicPrefix = "MjAES|";

    /// <summary>参与备份的客户端文件，history.dat 保存账号历史与登录态。</summary>
    private static readonly string[] LoginFiles = ["history.dat", "pcsdk.ini", "agreement.json"];

    private static readonly string[] ExeNames = ["AzurPromilia"];

    private static readonly Regex DeviceIdRegex = new(@"device id: ([0-9a-f]{32})", RegexOptions.Compiled);

    private readonly ILogger<ManjuuAccountCacheService> _logger;


    public ManjuuAccountCacheService(ILogger<ManjuuAccountCacheService> logger)
    {
        _logger = logger;
    }


    /// <summary>客户端账号数据目录（%APPDATA%\AzurPromilia\Game_35_C000_SC000）。</summary>
    public static string GameDataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AzurPromilia", "Game_35_C000_SC000");


    private static string BackupRoot => Path.Combine(AppConfig.CacheFolder, BackupFolderName);


    public string GetBackupFolder(string key) => Path.Combine(BackupRoot, key);


    #region 文件备份


    /// <summary>当前登录缓存的哈希（history.dat 不存在时回退 pcsdk.ini）。</summary>
    public string? GetCurrentCacheHash()
    {
        try
        {
            foreach (string name in new[] { "history.dat", "pcsdk.ini" })
            {
                string file = Path.Combine(GameDataFolder, name);
                if (File.Exists(file))
                {
                    using FileStream fs = File.OpenRead(file);
                    return Convert.ToHexStringLower(SHA256.HashData(fs));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read Manjuu login cache hash failed.");
        }
        return null;
    }


    public bool HasCache(string? key)
    {
        return !string.IsNullOrWhiteSpace(key) && Directory.Exists(GetBackupFolder(key));
    }


    /// <summary>备份当前账号数据到 Starward 缓存目录。</summary>
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


    /// <summary>从 Starward 缓存恢复账号数据（切换账号）。</summary>
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
            File.Delete(Path.Combine(GameDataFolder, "history.dat"));
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


    #endregion


    #region 账号解析


    /// <summary>游戏安装目录（从启动器注册表读取）。</summary>
    private static string? GetGameFolder()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(GameRegistryPath);
            if (key?.GetValue("Path") is string path && !string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        catch
        {
        }
        return null;
    }


    /// <summary>SDK 设备 ID，来自 pcsdk_log.txt 的 "device id: " 日志行（取最后一条）。</summary>
    public string? GetDeviceId()
    {
        try
        {
            string? folder = GetGameFolder();
            if (folder is null)
            {
                return null;
            }
            string log = Path.Combine(folder, "pcsdk_log.txt");
            if (!File.Exists(log))
            {
                return null;
            }
            using FileStream fs = File.OpenRead(log);
            long start = Math.Max(0, fs.Length - 1024 * 1024);
            fs.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            string content = reader.ReadToEnd();
            MatchCollection matches = DeviceIdRegex.Matches(content);
            if (matches.Count > 0)
            {
                return matches[^1].Groups[1].Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read Manjuu device id failed.");
        }
        return null;
    }


    private static byte[] GetKey(string deviceId) => Encoding.UTF8.GetBytes(deviceId).Take(32).ToArray();

    private static byte[] GetIv(string deviceId) => Convert.FromHexString(new string(deviceId.Where(c => Uri.IsHexDigit(c)).ToArray()).Substring(0, 32));


    private static byte[] DecryptMjAes(string base64, string deviceId)
    {
        using Aes aes = Aes.Create();
        aes.Key = GetKey(deviceId);
        aes.IV = GetIv(deviceId);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] cipher = Convert.FromBase64String(base64);
        byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        if (plain.Length < MagicPrefix.Length || Encoding.ASCII.GetString(plain, 0, MagicPrefix.Length) != MagicPrefix)
        {
            throw new InvalidDataException("MjAES prefix mismatch.");
        }
        return plain[MagicPrefix.Length..];
    }


    private static string EncryptMjAes(byte[] data, string deviceId)
    {
        using Aes aes = Aes.Create();
        aes.Key = GetKey(deviceId);
        aes.IV = GetIv(deviceId);
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] plain = Encoding.ASCII.GetBytes(MagicPrefix).Concat(data).ToArray();
        byte[] cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        return Convert.ToBase64String(cipher);
    }


    /// <summary>读取账号历史（history.dat）。读不到或解密失败时返回 null。</summary>
    public ManjuuAccountHistory? ReadAccountHistory()
    {
        try
        {
            string file = Path.Combine(GameDataFolder, "history.dat");
            if (!File.Exists(file))
            {
                return null;
            }
            string? deviceId = GetDeviceId();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }
            string base64 = File.ReadAllText(file).Trim();
            byte[] plain = DecryptMjAes(base64, deviceId);
            return JsonSerializer.Deserialize<ManjuuAccountHistory>(plain);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read Manjuu account history failed.");
            return null;
        }
    }


    /// <summary>当前登录账号（history.dat 的 last_user）。</summary>
    public ManjuuAccountInfo? GetCurrentAccount()
    {
        return ReadAccountHistory()?.LastUser;
    }


    /// <summary>把指定 UID 设为 history.dat 的 last_user（账号切换），保留账号列表。</summary>
    public bool SwitchAccount(string uid)
    {
        try
        {
            string file = Path.Combine(GameDataFolder, "history.dat");
            if (!File.Exists(file))
            {
                return false;
            }
            string? deviceId = GetDeviceId();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }
            ManjuuAccountHistory? history = ReadAccountHistory();
            if (history is null)
            {
                return false;
            }
            ManjuuAccountInfo? target = history.Items.FirstOrDefault(x => x.Uid == uid);
            if (target is null)
            {
                return false;
            }
            history.LastUser = target;
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(history);
            File.WriteAllText(file, EncryptMjAes(data, deviceId));
            _logger.LogInformation("Switch Manjuu account to {uid}.", uid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Switch Manjuu account failed.");
            return false;
        }
    }


    #endregion

}


public sealed class ManjuuAccountHistory
{
    [JsonPropertyName("items")]
    public List<ManjuuAccountInfo> Items { get; set; } = [];

    [JsonPropertyName("last_user")]
    public ManjuuAccountInfo? LastUser { get; set; }
}


public sealed class ManjuuAccountInfo
{
    [JsonPropertyName("account_type")]
    public int AccountType { get; set; }

    [JsonPropertyName("mail")]
    public string? Mail { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("third_uid")]
    public string? ThirdUid { get; set; }

    [JsonPropertyName("third_user_name")]
    public string? ThirdUserName { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("utoken")]
    public string? UToken { get; set; }
}
