using System;

namespace Starward.Features.Gacha.Endfield;

/// <summary>
/// 终末地的实时数据（启动器看板接口 /api/game/get_kanban 的 beyond_kanban）。
/// </summary>
public sealed class EndfieldDailyNote
{

    public string RoleName { get; set; } = "";

    public string Uid { get; set; } = "";

    public string RoleId { get; set; } = "";

    public string ServerName { get; set; } = "";

    public int CurrentSanity { get; set; }

    public int MaxSanity { get; set; }

    public int Level { get; set; }

    public int MaxLevel { get; set; }

    public int BattlePassLevel { get; set; }

    public int MaxBattlePassLevel { get; set; }

    public int DailyActivity { get; set; }

    public int DailyActivityMax { get; set; }

    public int WeeklyTask { get; set; }

    public int WeeklyTaskMax { get; set; }

    /// <summary>
    /// 距离理智回满的剩余时间。
    /// </summary>
    public TimeSpan RecoverTime { get; set; }

    public bool IsSanityFull => MaxSanity > 0 && CurrentSanity >= MaxSanity;

    public int RecoverHours => (int)RecoverTime.TotalHours;

    public int RecoverMinutes => RecoverTime.Minutes;

}
