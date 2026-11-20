using CommunityToolkit.Mvvm.ComponentModel;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.ThirdParty;
using System;


namespace Starward.Features.GameSelector;

public partial class GameBizIcon : ObservableObject, IEquatable<GameBizIcon>
{


    private const double GB = 1 << 30;


    public GameId GameId { get; set; }

    public GameBiz GameBiz { get; set; }


    public string GameIcon { get; set => SetProperty(ref field, value); }

    public string GameName { get; set => SetProperty(ref field, value); }

    public string ServerIcon { get; set => SetProperty(ref field, value); }

    public string ServerName { get; set => SetProperty(ref field, value); }

    public double MaskOpacity { get; set => SetProperty(ref field, value); } = 1.0;

    public bool IsPinned { get; set => SetProperty(ref field, value); }

    public string? InstallPath { get; set => SetProperty(ref field, value); }

    public long TotalSize { get; set { field = value; OnPropertyChanged(nameof(TotalSizeText)); } }

    public string? TotalSizeText => TotalSize == 0 ? null : $"{TotalSize / GB:F2}GB";


    public bool IsSelected
    {
        get;
        set
        {
            field = value;
            MaskOpacity = value ? 0 : 1;
        }
    }



    public GameBizIcon(GameBiz gameBiz)
    {
        GameBiz = gameBiz;
        GameId = GameId.FromGameBiz(gameBiz)!;
        GameIcon = GameBizToIcon(gameBiz);
        GameName = gameBiz.ToGameName();
        ServerIcon = GameBizToServerIcon(gameBiz);
        ServerName = gameBiz.ToGameServerName();
    }



    public GameBizIcon(GameInfo gameInfo)
    {
        GameId = gameInfo;
        GameBiz = gameInfo.GameBiz;
        GameIcon = gameInfo.Display.Icon.Url;
        GameName = gameInfo.Display.Name;
        ServerIcon = GameBizToServerIcon(gameInfo.GameBiz);
        ServerName = gameInfo.GameBiz.ToGameServerName();
    }



    /// <summary>
    /// 使用 HoYoPlay 接口返回的图标替换当前图标。
    /// 只更新图标，游戏名与服务器名沿用本地映射。
    /// </summary>
    public void UpdateIcon(GameInfo gameInfo)
    {
        if (!string.IsNullOrWhiteSpace(gameInfo?.Display?.Icon?.Url))
        {
            GameIcon = gameInfo.Display.Icon.Url;
        }
    }



    private static string GameBizToIcon(GameBiz gameBiz)
    {
        return gameBiz.Game switch
        {
            GameBiz.bh3 => "ms-appx:///Assets/Image/icon_bh3.jpg",
            GameBiz.hk4e => "ms-appx:///Assets/Image/icon_ys.jpg",
            GameBiz.hkrpg => "ms-appx:///Assets/Image/icon_sr.jpg",
            GameBiz.nap => "ms-appx:///Assets/Image/icon_zzz.jpg",
            GameBiz.endfield => "ms-appx:///Assets/Image/icon_endfield.png",
            GameBiz.arknights => "ms-appx:///Assets/Image/icon_arknights.png",
            _ => "ms-appx:///Assets/Image/Transparent.png",
        };
    }


    private static string GameBizToServerIcon(GameBiz gameBiz)
    {
        // 鹰角游戏图标本身不区分渠道，用社区平台 Logo 作为服务器角标。
        if (gameBiz.Game is GameBiz.endfield or GameBiz.arknights)
        {
            return gameBiz.IsGlobalServer()
                ? "ms-appx:///Assets/Image/icon_skport.png"
                : "ms-appx:///Assets/Image/icon_skland.png";
        }
        // 库洛等第三方游戏不属于米哈游渠道体系，不显示角标。
        if (ThirdPartyGameService.IsThirdPartyGame(gameBiz))
        {
            return "ms-appx:///Assets/Image/Transparent.png";
        }
        return gameBiz.Server switch
        {
            "cn" => "ms-appx:///Assets/Image/icon_badge_miyoushe.png",
            "global" => "ms-appx:///Assets/Image/icon_badge_hoyolab.png",
            "bilibili" => "ms-appx:///Assets/Image/icon_badge_bilibili.png",
            _ => "ms-appx:///Assets/Image/Transparent.png",
        };
    }


    public bool Equals(GameBizIcon? other)
    {
        return ReferenceEquals(this, other) || GameBiz == other?.GameBiz;
    }

}
