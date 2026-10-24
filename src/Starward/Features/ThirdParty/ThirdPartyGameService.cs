using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Core.Hypergryph;
using Starward.Features.GameLauncher;
using Starward.Features.Hypergryph;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Starward.Features.ThirdParty;

/// <summary>
/// 第三方启动器游戏（鹰角、库洛）的检测与展示。
/// 目前只负责游戏识别、安装路径自动发现和启动，版本更新由对应官方启动器管理。
/// </summary>
internal static class ThirdPartyGameService
{

    private const string TransparentImage = "ms-appx:///Assets/Image/Transparent.png";


    internal sealed record Profile(GameBiz GameBiz, string ExeName, string[] InstallFolderNames);


    public static IReadOnlyList<Profile> Profiles { get; } =
    [
        new(GameBiz.endfield_cn, "Endfield.exe", ["Endfield Game", "Endfield", "Hypergryph Launcher/games/Endfield Game"]),
        new(GameBiz.arknights_cn, "Arknights.exe", ["Arknights", "Hypergryph Launcher/games/Arknights"]),
        new(GameBiz.kirara_cn, "Wuthering Waves.exe", ["Wuthering Waves Game", "Wuthering Waves", "Kuro Games/Wuthering Waves", "Kuro Games/Wuthering Waves Game"]),
        new(GameBiz.kirara_global, "Wuthering Waves.exe", ["Wuthering Waves Game", "Wuthering Waves", "Kuro Games/Wuthering Waves", "Kuro Games/Wuthering Waves Game"]),
    ];



    public static bool IsThirdPartyGame(GameBiz gameBiz) => HypergryphGameConstants.IsHypergryphGame(gameBiz) || Profiles.Any(x => x.GameBiz == gameBiz);


    public static Profile? GetProfile(GameBiz gameBiz) => Profiles.FirstOrDefault(x => x.GameBiz == gameBiz);


    public static string? GetExeName(GameBiz gameBiz)
    {
        if (HypergryphGameConstants.IsHypergryphGame(gameBiz))
        {
            return HypergryphGameConstants.GetGameProfile(gameBiz).ExeName;
        }
        return GetProfile(gameBiz)?.ExeName;
    }


    public static IEnumerable<GameInfo> CreateGameInfos()
    {
        foreach (Profile profile in Profiles)
        {
            GameInfo? info = CreateGameInfo(profile.GameBiz);
            if (info is not null)
            {
                yield return info;
            }
        }
    }


    public static GameInfo? CreateGameInfo(GameBiz gameBiz)
    {
        if (HypergryphGameConstants.IsHypergryphGame(gameBiz))
        {
            return HypergryphGameMetadata.CreateGameInfo(gameBiz);
        }
        Profile? profile = GetProfile(gameBiz);
        GameId? gameId = GameId.FromGameBiz(gameBiz);
        if (profile is null || gameId is null)
        {
            return null;
        }
        string name = gameBiz.ToGameName();
        return new GameInfo
        {
            Id = gameId.Id,
            GameBiz = gameId.GameBiz,
            DisplayStatus = GameInfoDisplayStatus.LAUNCHER_GAME_DISPLAY_STATUS_AVAILABLE,
            GameServerConfigs = [],
            Display = new GameInfoDisplay
            {
                Language = CultureInfo.CurrentUICulture.Name,
                Name = name,
                Title = name,
                Subtitle = "",
                Icon = new GameImage { Url = TransparentImage, Link = "" },
                Logo = new GameImage { Url = TransparentImage, Link = "" },
                Thumbnail = new GameImage { Url = TransparentImage, Link = "" },
                Background = new GameImage { Url = TransparentImage, Link = "" },
            },
        };
    }



    /// <summary>
    /// 扫描常见安装位置，把检测到的第三方游戏路径写入设置。
    /// </summary>
    public static void EnsureInstallPathsDetected()
    {
        foreach (Profile profile in Profiles)
        {
            try
            {
                if (GameLauncherService.GetGameInstallPath(profile.GameBiz) is not null)
                {
                    continue;
                }
                string? path = FindInstallPath(profile) ?? GetRegistryInstallPath(profile.GameBiz);
                if (path is not null)
                {
                    GameLauncherService.ChangeGameInstallPath(profile.GameBiz, path);
                }
            }
            catch { }
        }
    }



    public static string? GetLauncherPath(GameBiz gameBiz)
    {
        string[] relativePaths;
        if (HypergryphGameConstants.IsHypergryphGame(gameBiz))
        {
            relativePaths = ["Hypergryph Launcher/Launcher.exe", "Hypergryph Launcher/1.6.0/Launcher.exe"];
        }
        else if (Profiles.Any(x => x.GameBiz == gameBiz))
        {
            relativePaths = ["Kuro Games Launcher/KuroLauncher.exe", "Kuro Games Launcher/Launcher.exe", "Kuro Games/Launcher/KuroLauncher.exe"];
        }
        else
        {
            return null;
        }
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }
                foreach (string relativePath in relativePaths)
                {
                    string path = Path.Combine(drive.RootDirectory.FullName, relativePath);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }
        catch { }
        return null;
    }



    private static string? GetRegistryInstallPath(GameBiz gameBiz)
    {
        try
        {
            string key = gameBiz.GetGameRegistryKey();
            return Microsoft.Win32.Registry.GetValue(key, "GameInstallPath", null) as string;
        }
        catch
        {
            return null;
        }
    }



    private static string? FindInstallPath(Profile profile)
    {
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }
                foreach (string folder in profile.InstallFolderNames)
                {
                    string path = Path.Combine(drive.RootDirectory.FullName, folder);
                    if (File.Exists(Path.Combine(path, profile.ExeName)))
                    {
                        return path;
                    }
                }
            }
        }
        catch { }
        return null;
    }


}
