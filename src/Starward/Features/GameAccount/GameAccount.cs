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

    public bool IsSaved { get; set => SetProperty(ref field, value); }

}