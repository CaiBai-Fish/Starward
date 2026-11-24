using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Core.Manjuu;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Starward.Features.Manjuu;

internal static class ManjuuGameMetadata
{

    public static GameInfo CreateGameInfo(GameBiz gameBiz)
    {
        GameId gameId = GameId.FromGameBiz(gameBiz)!;
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
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
                Logo = new GameImage { Url = ManjuuGameConstants.AzurPromiliaListLogo },
                Thumbnail = new GameImage { Url = profile.Background },
                Background = new GameImage { Url = profile.Background },
            },
        };
    }


    public static GameConfig CreateGameConfig(GameBiz gameBiz)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
        return new GameConfig
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            ExeFileName = profile.ExeName,
            InstallationDir = profile.InstallationDirectory,
            DefaultDownloadMode = DownloadMode.DOWNLOAD_MODE_FILE,
            RelatedProcesses = [.. profile.RelatedProcesses],
            RedundantFileCleanupPaths = [],
            AudioPackageScanDir = "",
        };
    }


    public static GameBackgroundInfo CreateBackgroundInfo(GameBiz gameBiz, ManjuuLauncherContent content)
    {
        List<GameBackground> backgrounds = [];
        if (content.MediaContentMap.TryGetValue("1", out List<ManjuuMediaItem>? staticItems)
            && staticItems.OrderBy(x => x.Sort).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Image)) is ManjuuMediaItem item)
        {
            backgrounds.Add(new GameBackground
            {
                Id = "manjuu_static_" + item.Sort,
                Background = new GameImage { Url = item.Image!, Link = item.LinkUrl ?? "" },
                Type = GameBackground.BACKGROUND_TYPE_UNSPECIFIED,
            });
        }
        if (content.MediaContentMap.TryGetValue("12", out List<ManjuuMediaItem>? videoItems)
            && videoItems.OrderBy(x => x.Sort).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Video)) is ManjuuMediaItem video)
        {
            backgrounds.Add(new GameBackground
            {
                Id = "manjuu_video_" + video.Sort,
                Background = new GameImage { Url = video.Icon ?? "" },
                Video = new GameImage { Url = video.Video! },
                Theme = new GameImage { Url = "" },
                Type = GameBackground.BACKGROUND_TYPE_VIDEO,
            });
        }
        return new GameBackgroundInfo
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            Backgrounds = backgrounds,
        };
    }


    public static GameContent CreateGameContent(GameBiz gameBiz, ManjuuLauncherContent content)
    {
        List<GameBanner> banners = [];
        if (content.MediaContentMap.TryGetValue("2", out List<ManjuuMediaItem>? bannerItems))
        {
            foreach (ManjuuMediaItem item in bannerItems.OrderBy(x => x.Sort))
            {
                if (string.IsNullOrWhiteSpace(item.Image))
                {
                    continue;
                }
                banners.Add(new GameBanner
                {
                    Id = item.Sort.ToString(),
                    Image = new GameImage { Url = item.Image, Link = item.LinkUrl ?? "" },
                });
            }
        }
        List<GamePost> posts = [];
        if (content.TxtContentMap.TryGetValue("1", out List<ManjuuTextItem>? textItems))
        {
            foreach (ManjuuTextItem item in textItems.OrderBy(x => x.Sort))
            {
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    continue;
                }
                posts.Add(new GamePost
                {
                    Id = item.Sort.ToString(),
                    Type = GamePostType.POST_TYPE_ANNOUNCE,
                    Title = item.Title,
                    Link = item.LinkUrl ?? "",
                    Date = item.PublishDate ?? "",
                });
            }
        }
        List<GameSocialMedia> socialMediaList = [];
        if (content.MediaContentMap.TryGetValue("11", out List<ManjuuMediaItem>? socialItems))
        {
            foreach (ManjuuMediaItem item in socialItems.OrderBy(x => x.Sort))
            {
                List<GameSocialMediaLink> links = [];
                if (!string.IsNullOrWhiteSpace(item.LinkUrl))
                {
                    links.Add(new GameSocialMediaLink { Title = item.Title ?? "", Link = item.LinkUrl });
                }
                socialMediaList.Add(new GameSocialMedia
                {
                    Id = item.Sort.ToString(),
                    Icon = new GameImage { Url = item.Icon ?? "" },
                    QrImage = new GameImage { Url = item.Image ?? "" },
                    QrDesc = item.Title ?? "",
                    Links = links,
                });
            }
        }
        return new GameContent
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            Language = CultureInfo.CurrentUICulture.Name,
            Banners = banners,
            Posts = posts,
            SocialMediaList = socialMediaList,
        };
    }


    public static GamePackage CreateGamePackage(GameBiz gameBiz, ManjuuGameVersion version, ManjuuPackageConfig packageConfig)
    {
        ManjuuGameProfile profile = ManjuuGameConstants.GetGameProfile(gameBiz);
        string cdnBaseUrl = version.DomainList?.FirstOrDefault()
            ?? packageConfig.Released?.DomainList?.FirstOrDefault()
            ?? profile.CdnBaseUrl;
        if (!string.IsNullOrWhiteSpace(cdnBaseUrl) && !cdnBaseUrl.EndsWith('/'))
        {
            cdnBaseUrl += "/";
        }
        List<GamePackageFile> files = (version.GamePackageList ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.DownloadUrl))
            .Select(x => new GamePackageFile
            {
                Url = cdnBaseUrl + x.DownloadUrl!.TrimStart('/'),
                MD5 = x.FileMd5 ?? "",
                Size = x.FileSize,
                DecompressedSize = 0,
            })
            .ToList();
        return new GamePackage
        {
            GameId = GameId.FromGameBiz(gameBiz)!,
            Main = new GamePackageVersion
            {
                Major = new GamePackageResource
                {
                    Version = version.VersionCode ?? "",
                    GamePackages = files,
                    AudioPackages = [],
                    ResListUrl = "",
                },
                Patches = [],
            },
            PreDownload = new GamePackageVersion { Patches = [] },
        };
    }

}
