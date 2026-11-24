using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Starward.Features.GameAccount;

/// <summary>
/// 保存/恢复终末地客户端的鹰角账号缓存（MMKV login_cache 与相关注册表值），用于账号切换。
/// 设备标识 sdk_hgsdk_data_1 不参与切换，避免触发设备验证。
/// </summary>
public sealed class EndfieldAccountCacheService
{

    private const string SdkRegistryPath = @"Software\Hypergryph\sdk_data";
    private const string PrefsRegistryPath = @"Software\Hypergryph\Endfield";
    private const string BackupFolderName = "endfield_account_cache";
    private const string ManifestFileName = "manifest.json";

    /// <summary>PlayerPrefs 中与当前账号相关的键名前缀（实际键名带 Unity 哈希后缀）。</summary>
    private static readonly string[] PrefsKeyPrefixes =
    [
        "u8sdk_cached_uid",
        "PLDK_cachedUserId",
        "PLDK_cachedRoleId",
    ];

    /// <summary>参与切换的 MMKV 文件，login_cache 与 .crc 必须成对出现。</summary>
    private static readonly string[] CacheFileNames =
    [
        "login_cache",
        "login_cache.crc",
        "gf_login_cache",
        "gf_login_cache.crc",
    ];

    private readonly ILogger<EndfieldAccountCacheService> _logger;


    public EndfieldAccountCacheService(ILogger<EndfieldAccountCacheService> logger)
    {
        _logger = logger;
    }


    /// <summary>LocalLow 下终末地客户端数据目录。</summary>
    public static string LocalLowEndfieldFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Hypergryph", "Endfield");


    /// <summary>Starward 缓存的账号快照根目录。</summary>
    private static string BackupRoot => Path.Combine(AppConfig.CacheFolder, BackupFolderName);


    public static string GetBackupFolder(string gameUid) => Path.Combine(BackupRoot, gameUid);


    /// <summary>读取 PlayerPrefs 中当前登录的游戏 UID；未登录或读不到时返回 null。</summary>
    public string? GetCurrentGameUid()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PrefsRegistryPath);
            if (key is null)
            {
                return null;
            }
            foreach (string valueName in key.GetValueNames())
            {
                if (!valueName.StartsWith(PrefsKeyPrefixes[0], StringComparison.Ordinal))
                {
                    continue;
                }
                string? uid = ReadPrefsValueAsString(key, valueName);
                if (!string.IsNullOrWhiteSpace(uid))
                {
                    return uid;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read Endfield current game uid failed.");
        }
        return null;
    }


    public bool HasCache(string gameUid)
    {
        return !string.IsNullOrWhiteSpace(gameUid)
            && File.Exists(Path.Combine(GetBackupFolder(gameUid), ManifestFileName));
    }


    /// <summary>读取 PlayerPrefs 中当前登录的游戏角色 ID；未登录或读不到时返回 null。</summary>
    public string? GetCurrentRoleId()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PrefsRegistryPath);
            if (key is null)
            {
                return null;
            }
            foreach (string valueName in key.GetValueNames())
            {
                if (!valueName.StartsWith(PrefsKeyPrefixes[2], StringComparison.Ordinal))
                {
                    continue;
                }
                string? roleId = ReadPrefsValueAsString(key, valueName);
                if (!string.IsNullOrWhiteSpace(roleId))
                {
                    return roleId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read Endfield current role id failed.");
        }
        return null;
    }


    /// <summary>
    /// 删除客户端登录缓存，使游戏下次启动时进入登录界面。
    /// 调用前应先用 <see cref="Save"/> 备份当前账号缓存。
    /// </summary>
    public int ClearLoginCache()
    {
        int count = 0;
        if (Directory.Exists(LocalLowEndfieldFolder))
        {
            foreach (string sdkFolder in Directory.EnumerateDirectories(LocalLowEndfieldFolder, "sdk_data_*"))
            {
                foreach (string fileName in new[] { "login_cache", "login_cache.crc" })
                {
                    string path = Path.Combine(sdkFolder, fileName);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        count++;
                    }
                }
            }
        }
        _logger.LogInformation("Cleared Endfield login cache files: {Count}.", count);
        return count;
    }


    /// <summary>游戏进程是否在运行；切换缓存前必须关闭游戏。</summary>
    public bool IsGameRunning()
    {
        try
        {
            return Process.GetProcessesByName("Endfield").Length > 0;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// 保存当前登录账号的客户端缓存。调用前应确认游戏已经关闭或至少已完成登录。
    /// </summary>
    public void Save(string gameUid, string? roleId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameUid);
        string folder = GetBackupFolder(gameUid);
        string filesFolder = Path.Combine(folder, "files");
        Directory.CreateDirectory(filesFolder);

        var manifest = new CacheManifest
        {
            GameUid = gameUid,
            RoleId = roleId,
            SavedAt = DateTimeOffset.Now,
        };

        int fileCount = 0;
        if (Directory.Exists(LocalLowEndfieldFolder))
        {
            foreach (string sdkFolder in Directory.EnumerateDirectories(LocalLowEndfieldFolder, "sdk_data_*"))
            {
                string folderName = Path.GetFileName(sdkFolder);
                foreach (string fileName in CacheFileNames)
                {
                    string source = Path.Combine(sdkFolder, fileName);
                    if (!File.Exists(source))
                    {
                        continue;
                    }
                    string relative = Path.Combine("files", folderName, fileName);
                    string target = Path.Combine(folder, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target, overwrite: true);
                    manifest.Files.Add(relative);
                    fileCount++;
                }
            }
        }

        manifest.SdkRegistry = ReadSdkRegistryValues();
        manifest.Prefs = ReadPrefsValues();

        string manifestPath = Path.Combine(folder, ManifestFileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, AppConfig.JsonSerializerOptions), Encoding.UTF8);
        _logger.LogInformation("Saved Endfield account cache for uid {Uid}: {FileCount} files, {RegistryCount} registry values.", gameUid, fileCount, manifest.SdkRegistry.Count + manifest.Prefs.Count);
    }


    /// <summary>
    /// 恢复指定账号的客户端缓存。返回 false 表示没有可恢复的快照。
    /// </summary>
    public bool Restore(string gameUid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameUid);
        string manifestPath = Path.Combine(GetBackupFolder(gameUid), ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            _logger.LogInformation("No Endfield account cache snapshot for uid {Uid}.", gameUid);
            return false;
        }
        if (IsGameRunning())
        {
            throw new InvalidOperationException("切换终末地账号缓存前请先退出游戏。");
        }

        CacheManifest? manifest = JsonSerializer.Deserialize<CacheManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
        if (manifest is null)
        {
            return false;
        }

        string folder = GetBackupFolder(gameUid);
        foreach (string relative in manifest.Files)
        {
            string source = Path.Combine(folder, relative);
            if (!File.Exists(source))
            {
                continue;
            }
            // files/<sdk_data_dir>/<file> -> LocalLow/<sdk_data_dir>/<file>
            string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length != 3 || !string.Equals(parts[0], "files", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string targetFolder = Path.Combine(LocalLowEndfieldFolder, parts[1]);
            Directory.CreateDirectory(targetFolder);
            string target = Path.Combine(targetFolder, parts[2]);
            if (File.Exists(target))
            {
                string backup = target + ".before_switch";
                File.Copy(target, backup, overwrite: true);
            }
            File.Copy(source, target, overwrite: true);
        }

        WriteRegistryValues(manifest.SdkRegistry);
        WritePrefsValues(manifest.Prefs);
        _logger.LogInformation("Restored Endfield account cache for uid {Uid}.", gameUid);
        return true;
    }


    public void Delete(string gameUid)
    {
        if (string.IsNullOrWhiteSpace(gameUid))
        {
            return;
        }
        string folder = GetBackupFolder(gameUid);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
            _logger.LogInformation("Deleted Endfield account cache snapshot for uid {Uid}.", gameUid);
        }
    }


    private static List<RegistryValueEntry> ReadSdkRegistryValues()
    {
        var result = new List<RegistryValueEntry>();
        try
        {
            using RegistryKey? sdkKey = Registry.CurrentUser.OpenSubKey(SdkRegistryPath);
            if (sdkKey is null)
            {
                return result;
            }
            foreach (string subKeyName in sdkKey.GetSubKeyNames())
            {
                using RegistryKey? subKey = sdkKey.OpenSubKey(subKeyName);
                if (subKey is null)
                {
                    continue;
                }
                foreach (string valueName in new[] { "REG_GUID", "REG_GUID_V3_Encrypt" })
                {
                    object? value = subKey.GetValue(valueName);
                    if (value is null)
                    {
                        continue;
                    }
                    RegistryValueKind kind = subKey.GetValueKind(valueName);
                    result.Add(new RegistryValueEntry
                    {
                        Path = $@"{SdkRegistryPath}\{subKeyName}",
                        Name = valueName,
                        Kind = kind.ToString(),
                        StringValue = value as string,
                        BinaryBase64 = value is byte[] bytes ? Convert.ToBase64String(bytes) : null,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // 读取失败不应阻止文件缓存保存
            result.Add(new RegistryValueEntry { Path = "", Name = "", Error = ex.Message });
        }
        return result;
    }


    private static List<RegistryValueEntry> ReadPrefsValues()
    {
        var result = new List<RegistryValueEntry>();
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PrefsRegistryPath);
            if (key is null)
            {
                return result;
            }
            foreach (string valueName in key.GetValueNames())
            {
                if (!PrefsKeyPrefixes.Any(x => valueName.StartsWith(x, StringComparison.Ordinal)))
                {
                    continue;
                }
                object? value = key.GetValue(valueName);
                if (value is null)
                {
                    continue;
                }
                result.Add(new RegistryValueEntry
                {
                    Path = PrefsRegistryPath,
                    Name = valueName,
                    Kind = key.GetValueKind(valueName).ToString(),
                    StringValue = value switch
                    {
                        string text => text,
                        int number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        _ => null,
                    },
                    BinaryBase64 = value is byte[] bytes ? Convert.ToBase64String(bytes) : null,
                });
            }
        }
        catch (Exception ex)
        {
            result.Add(new RegistryValueEntry { Path = "", Name = "", Error = ex.Message });
        }
        return result;
    }


    private static void WriteRegistryValues(IEnumerable<RegistryValueEntry> entries)
    {
        foreach (RegistryValueEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Path) || string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(entry.Path);
            if (entry.BinaryBase64 is not null)
            {
                key.SetValue(entry.Name, Convert.FromBase64String(entry.BinaryBase64), RegistryValueKind.Binary);
            }
            else if (entry.Kind == RegistryValueKind.DWord.ToString() && long.TryParse(entry.StringValue, out long dword))
            {
                key.SetValue(entry.Name, (int)dword, RegistryValueKind.DWord);
            }
            else if (entry.StringValue is not null)
            {
                key.SetValue(entry.Name, entry.StringValue, RegistryValueKind.String);
            }
        }
    }


    private static void WritePrefsValues(IEnumerable<RegistryValueEntry> entries)
    {
        WriteRegistryValues(entries);
    }


    private static string? ReadPrefsValueAsString(RegistryKey key, string valueName)
    {
        object? value = key.GetValue(valueName);
        return value switch
        {
            string text => text.TrimEnd('\0').Trim(),
            byte[] bytes => Encoding.UTF8.GetString(bytes).TrimEnd('\0').Trim(),
            _ => null,
        };
    }


    private sealed class CacheManifest
    {
        public string GameUid { get; set; } = "";
        public string? RoleId { get; set; }
        public DateTimeOffset SavedAt { get; set; }
        public List<string> Files { get; set; } = [];
        public List<RegistryValueEntry> SdkRegistry { get; set; } = [];
        public List<RegistryValueEntry> Prefs { get; set; } = [];
    }


    private sealed class RegistryValueEntry
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? StringValue { get; set; }
        public string? BinaryBase64 { get; set; }
        public string? Error { get; set; }
    }

}
