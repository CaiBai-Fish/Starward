using CommunityToolkit.Mvvm.ComponentModel;
using Starward.Core;
using System;

namespace Starward.Features.GameAccount;

public class GameAccount : ObservableObject
{

    public string SHA256 { get; set; }

    public GameBiz GameBiz { get; set; }

    public long Uid { get; set => SetProperty(ref field, value); }

    /// <summary>终末地：鹰角账号下的游戏 UID（对应 PlayerPrefs u8sdk_cached_uid），用于定位客户端账号缓存。</summary>
    public string? GameUid { get; set; }

    /// <summary>终末地：游戏角色 ID（对应 PlayerPrefs PLDK_cachedRoleId）。</summary>
    public string? RoleId { get; set; }

    public string Name { get; set => SetProperty(ref field, value); }

    public byte[] Value { get; set; }

    public DateTime Time { get; set; } = DateTime.Now;


    /// <summary>界面显示的账号标识：优先 GameUid（蓝色星原的 UID 是字符串），否则显示数字 Uid。</summary>
    public string DisplayUid
    {
        get => !string.IsNullOrWhiteSpace(GameUid) ? GameUid : (Uid > 0 ? Uid.ToString() : "");
        set
        {
            if (!string.IsNullOrWhiteSpace(GameUid))
            {
                GameUid = string.IsNullOrWhiteSpace(value) ? null : value;
            }
            else if (long.TryParse(value, out long uid))
            {
                Uid = uid;
            }
            OnPropertyChanged();
        }
    }

    public bool IsSaved { get; set => SetProperty(ref field, value); }

}