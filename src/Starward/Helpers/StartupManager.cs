using Microsoft.Win32;
using System;
using System.IO;

namespace Starward.Helpers;

/// <summary>
/// 开机自启管理，写入当前用户的 Run 注册表项。
/// </summary>
internal static class StartupManager
{

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "Starward";


    /// <summary>
    /// 注册表中是否已存在有效的自启项。
    /// </summary>
    public static bool IsEnabled()
    {
        try
        {
            string? value = GetValue();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            string expected = GetCommand();
            return string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }



    /// <summary>
    /// 写入或删除自启项，返回操作是否成功。
    /// </summary>
    public static bool TrySetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key is null)
            {
                return false;
            }
            if (enabled)
            {
                key.SetValue(ValueName, GetCommand(), RegistryValueKind.String);
                AppConfig.RunAtStartup = true;
            }
            else
            {
                key.DeleteValue(ValueName, false);
                AppConfig.RunAtStartup = false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }



    /// <summary>
    /// 启动时同步注册表，处理程序被移动或版本更新后路径变化的情况。
    /// </summary>
    public static void Synchronize()
    {
        try
        {
            if (AppConfig.RunAtStartup)
            {
                if (!IsEnabled())
                {
                    TrySetEnabled(true);
                }
            }
            else if (!string.IsNullOrWhiteSpace(GetValue()))
            {
                TrySetEnabled(false);
            }
        }
        catch { }
    }



    private static string? GetValue()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) as string;
        }
        catch
        {
            return null;
        }
    }



    private static string GetCommand()
    {
        string exe = AppConfig.StarwardExecutePath;
        if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            exe = Path.Combine(AppContext.BaseDirectory, "Starward.exe");
        }
        string arguments = AppConfig.RunAtStartupSilently ? " --hide" : "";
        return $"\"{exe}\"{arguments}";
    }


}
