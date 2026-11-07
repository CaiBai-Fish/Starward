using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Core.Hypergryph;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Starward.Features.Hypergryph;

internal static class HypergryphGameMetadata
{
    public static GameInfo CreateGameInfo(GameBiz gameBiz)
    {
        GameId gameId = GameId.FromGameBiz(gameBiz)!;
        HypergryphGameProfile profile = HypergryphGameConstants.GetGameProfile(gameBiz);
        // 游戏列表卡片显示官方 Logo（透明底），鼠标悬停的小图标仍使用游戏图标
        string listLogo = gameBiz.Value switch
        {
            GameBiz.endfield_cn or GameBiz.endfield_global => HypergryphGameConstants.EndfieldListLogo,
            GameBiz.arknights_cn => HypergryphGameConstants.ArknightsListLogo,
            _ => profile.Icon,
        };
        return new GameInfo
        {
            Id = gameId.Id,
            GameBiz = gameId.GameBiz,
            DisplayStatus = GameInfoDisplayStatus.LAUNCHER_GAME_DISPLAY_STATUS_AVAILABLE,
            GameServerConfigs = [],
            Display = new GameInfoDisplay
            {
                Language = CultureInfo.CurrentUICulture.Name,
                Name = gameId.GameBiz.ToGameName(),
                Title = gameId.GameBiz.ToGameName(),
                Subtitle = "",
                Icon = new GameImage { Url = profile.Icon },
                Logo = new GameImage { Url = listLogo },
                Thumbnail = new GameImage { Url = profile.Background },
                Background = new GameImage { Url = profile.Background },
            },
        };
    }

    public static GameConfig CreateGameConfig(GameBiz gameBiz)
    {
        HypergryphGameProfile profile = HypergryphGameConstants.GetGameProfile(gameBiz);
        return new GameConfig
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            ExeFileName = profile.ExeName,
            InstallationDir = profile.InstallationDirectory,
            DefaultDownloadMode = DownloadMode.DOWNLOAD_MODE_FILE,
            RelatedProcesses = [.. profile.RelatedProcesses],
            RedundantFileCleanupPaths = [],
        };
    }

    public static GameBackgroundInfo CreateBackgroundInfo(GameBiz gameBiz, HypergryphLauncherContent launcherContent)
    {
        List<GameBackground> backgrounds = [];
        HypergryphMainBackground? mainBackground = launcherContent.MainBackground;
        if (!string.IsNullOrWhiteSpace(mainBackground?.Url))
        {
            bool isVideo = !string.IsNullOrWhiteSpace(mainBackground.VideoUrl);
            string id = string.IsNullOrWhiteSpace(mainBackground.MD5) ? mainBackground.Url : mainBackground.MD5;
            backgrounds.Add(new GameBackground
            {
                Id = id,
                Background = new GameImage { Url = mainBackground.Url },
                Theme = new GameImage { Url = "" },
                Video = null!,
                Type = GameBackground.BACKGROUND_TYPE_UNSPECIFIED,
            });
            if (isVideo)
            {
                backgrounds.Add(new GameBackground
                {
                    Id = id + "_video",
                    Background = new GameImage { Url = mainBackground.Url },
                    Theme = new GameImage { Url = "" },
                    Video = new GameImage { Url = mainBackground.VideoUrl },
                    Type = GameBackground.BACKGROUND_TYPE_VIDEO,
                });
            }
        }
        return new GameBackgroundInfo
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            Backgrounds = backgrounds,
        };
    }

    public static GameContent CreateGameContent(GameBiz gameBiz, HypergryphLauncherContent launcherContent)
    {
        return new GameContent
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            Language = CultureInfo.CurrentUICulture.Name,
            Banners = launcherContent.Banners
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .Select(x => new GameBanner
                {
                    Id = x.Id,
                    Image = new GameImage
                    {
                        Url = x.Url,
                        Link = x.JumpUrl,
                        LoginStateInLink = x.NeedToken,
                    },
                }).ToList(),
            Posts = launcherContent.AnnouncementTabs
                .SelectMany(tab => tab.Announcements
                    .Where(x => !string.IsNullOrWhiteSpace(x.Content))
                    .Select(x => new GamePost
                    {
                        Id = x.Id,
                        Type = tab.TabName,
                        Title = x.Content,
                        Link = x.JumpUrl,
                        Date = FormatAnnouncementDate(x.StartTimestamp),
                    }))
                .ToList(),
            SocialMediaList = [],
        };
    }

    private static string FormatAnnouncementDate(string timestamp)
    {
        if (long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out long milliseconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
        return "";
    }

    public static GamePackage CreateGamePackage(GameBiz gameBiz, HypergryphLatestGame latest)
    {
        HypergryphGamePackage package = latest.Package ?? new();
        List<HypergryphPackagePart> packs = package.Packs ?? [];
        HypergryphPackagePart? firstPart = packs.FirstOrDefault();
        string url = !string.IsNullOrWhiteSpace(firstPart?.Url) ? firstPart.Url : package.Url;
        string md5 = !string.IsNullOrWhiteSpace(firstPart?.MD5) ? firstPart.MD5 : package.MD5;
        long packedSize = packs.Count > 0 ? packs.Sum(x => x.PackageSize) : package.PackageSize;
        long decompressedSize = package.TotalSize + (packs.Count > 0 ? packs.Sum(x => x.PackageSize) : 0);
        return new GamePackage
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            Main = new GamePackageVersion
            {
                Major = new GamePackageResource
                {
                    Version = latest.Version,
                    GamePackages =
                    [
                        new GamePackageFile
                        {
                            Url = url,
                            MD5 = md5,
                            Size = packedSize,
                            DecompressedSize = decompressedSize,
                        },
                    ],
                    AudioPackages = [],
                    ResListUrl = "",
                },
                Patches = [],
            },
            PreDownload = new GamePackageVersion { Patches = [] },
        };
    }
}
