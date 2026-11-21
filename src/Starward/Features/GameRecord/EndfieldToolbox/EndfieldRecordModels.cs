using Microsoft.UI.Xaml.Media;
using Starward.Features.Gacha.Endfield;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.UI;

namespace Starward.Features.GameRecord.EndfieldToolbox;


/// <summary>
/// 战绩详情中展示的干员头像与等级。
/// </summary>
public sealed class EndfieldCharView
{
    public string CharId { get; set; } = "";

    public string AvatarUrl { get; set; } = "";

    public string LevelText { get; set; } = "";

    public string LevelNumberText { get; set; } = "";

    public string RarityText { get; set; } = "";

    public string PotentialText { get; set; } = "";

    public int PotentialLevel { get; set; }

    public string PotentialIconUrl { get; set; } = "";

    public bool HasPotentialIcon => !string.IsNullOrWhiteSpace(PotentialIconUrl);

    public string PotentialIconSource => !string.IsNullOrWhiteSpace(PotentialIconUrl)
        ? PotentialIconUrl
        : PotentialLevel > 0
            ? $"ms-appx:///Assets/Image/Endfield/potential/potential_endfield_{Math.Min(PotentialLevel, 5)}.png"
            : "";

    public Brush PotentialEdge1Brush => PotentialLevel >= 1 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public Brush PotentialEdge2Brush => PotentialLevel >= 2 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public Brush PotentialEdge3Brush => PotentialLevel >= 3 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public Brush PotentialEdge4Brush => PotentialLevel >= 4 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public Brush PotentialEdge5Brush => PotentialLevel >= 5 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public string PropertyText { get; set; } = "";

    public Brush? AccentBrush { get; set; }

    public string WeaponName { get; set; } = "";

    public string WeaponIcon { get; set; } = "";

    public string WeaponLevelText { get; set; } = "";

    public bool HasWeapon => !string.IsNullOrWhiteSpace(WeaponIcon) || !string.IsNullOrWhiteSpace(WeaponName);

    public List<EndfieldEquipSlot> EquipSlots { get; set; } = EndfieldEquipSlot.CreateEmpty(4);

    public EndfieldEquipSlot EquipSlot1 => EquipSlots.Count > 0 ? EquipSlots[0] : new EndfieldEquipSlot();

    public EndfieldEquipSlot EquipSlot2 => EquipSlots.Count > 1 ? EquipSlots[1] : new EndfieldEquipSlot();

    public EndfieldEquipSlot EquipSlot3 => EquipSlots.Count > 2 ? EquipSlots[2] : new EndfieldEquipSlot();

    public EndfieldEquipSlot EquipSlot4 => EquipSlots.Count > 3 ? EquipSlots[3] : new EndfieldEquipSlot();

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

    public bool HasRarity => !string.IsNullOrWhiteSpace(RarityText);

    public bool HasPotential => !string.IsNullOrWhiteSpace(PotentialText);
}


public sealed class EndfieldEquipSlot
{
    public string Name { get; set; } = "";

    public string Icon { get; set; } = "";

    public string LevelText { get; set; } = "";

    public bool HasItem => !string.IsNullOrWhiteSpace(Icon) || !string.IsNullOrWhiteSpace(Name);

    public static List<EndfieldEquipSlot> CreateEmpty(int count)
    {
        List<EndfieldEquipSlot> slots = [];
        for (int i = 0; i < count; i++)
        {
            slots.Add(new EndfieldEquipSlot());
        }
        return slots;
    }
}


public sealed class EndfieldEnemyView
{
    public string Name { get; set; } = "";

    public string LevelText { get; set; } = "";

    public string ImageUrl { get; set; } = "";

    public string Description { get; set; } = "";

    public string Ability { get; set; } = "";

    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
}


public sealed class EndfieldStatView
{
    public string Glyph { get; set; } = "\uE7BA";

    public string Label { get; set; } = "";

    public string Value { get; set; } = "";
}


public sealed class CrisisContractRecordDetail
{
    public List<EndfieldCharView> Chars { get; set; } = [];

    public List<CrisisContractIndicatorView> Indicators { get; set; } = [];

    public bool HasChars => Chars.Count > 0;

    public bool HasIndicators => Indicators.Count > 0;
}


public sealed class CrisisContractIndicatorView
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string Icon { get; set; } = "";

    public string ScoreText { get; set; } = "";

    public string TypeText { get; set; } = "";

    public bool IsUnlock { get; set; }

    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);

    public string StateText => IsUnlock ? "已启用" : "未解锁";
}


public sealed class CrisisContractRecordView
{
    public string Id { get; set; } = "";

    public List<CrisisContractIndicatorView> Indicators { get; set; } = [];

    public bool IsPass { get; set; }

    public bool IsBest { get; set; }

    public int IndicatorCount { get; set; }

    public int PassWave { get; set; }

    public string TimeAgo { get; set; } = "";

    public string TimestampText { get; set; } = "";

    public string DurationText { get; set; } = "";

    public bool ShowNewBadge { get; set; }

    public List<EndfieldCharView> Chars { get; set; } = [];

    public bool HasChars => Chars.Count > 0;

    public bool HasWave1 => PassWave >= 1;

    public bool HasWave2 => PassWave >= 2;

    public bool HasWave3 => PassWave >= 3;

    public string StatusText => IsPass ? "行动成功" : "行动失败";

    public string IndicatorCountText => IndicatorCount.ToString(CultureInfo.CurrentCulture);

    public string WaveText => PassWave > 0 ? $"通过 {PassWave} 波" : "未通过";
}


public sealed class CrisisContractView
{
    public string SeasonName { get; set; } = "危机合约";

    public string SeasonType { get; set; } = "危机合约";

    public string DateRange { get; set; } = "";

    public string BannerImage { get; set; } = "";

    public string BadgeImage { get; set; } = "";

    public string BadgeName { get; set; } = "";

    public string BadgeLevelText { get; set; } = "";

    public int Highest { get; set; }

    public string HighestText => Highest.ToString(CultureInfo.CurrentCulture);

    public double WeeklyProgress { get; set; }

    public double IndicatorProgress { get; set; }

    public double StageProgress { get; set; }

    public List<EndfieldStatView> Stats { get; set; } = [];

    public CrisisContractRecordView? BestRecord { get; set; }

    public bool HasBestRecord => BestRecord is not null;

    public List<CrisisContractRecordView> Records { get; set; } = [];

    public bool HasRecords => Records.Count > 0;

    public List<CrisisContractIndicatorView> Indicators { get; set; } = [];

    public bool HasIndicators => Indicators.Count > 0;

    public string DungeonName { get; set; } = "";

    public string DungeonDescription { get; set; } = "";

    public bool HasDungeonDescription => !string.IsNullOrWhiteSpace(DungeonDescription);

    public List<EndfieldEnemyView> Enemies { get; set; } = [];

    public bool HasEnemies => Enemies.Count > 0;

    public string AchievementName { get; set; } = "";

    public string AchievementDescription { get; set; } = "";

    public string AchievementIcon { get; set; } = "";

    public bool HasAchievement => !string.IsNullOrWhiteSpace(AchievementName);
}


public sealed class WarEchoDungeonView
{
    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string FeatureText { get; set; } = "";

    public string RecommendLevelText { get; set; } = "";

    public bool IsPass { get; set; }

    public bool HasRecord { get; set; }

    public string DurationText { get; set; } = "";

    public string DateText { get; set; } = "";

    public int Star { get; set; }

    public bool HasStar1 => Star >= 1;

    public bool HasStar2 => Star >= 2;

    public bool HasStar3 => Star >= 3;

    public Brush Star1Brush => Star >= 1 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public Brush Star2Brush => Star >= 2 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public Brush Star3Brush => Star >= 3 ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.EmptyStar;

    public List<EndfieldCharView> Chars { get; set; } = [];

    public bool HasChars => Chars.Count > 0;

    public List<EndfieldEnemyView> Enemies { get; set; } = [];

    public bool HasEnemies => Enemies.Count > 0;

    public bool HasFeature => !string.IsNullOrWhiteSpace(FeatureText);

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}


public sealed class WarEchoWeekView
{
    public string Name { get; set; } = "";

    public string DateRange { get; set; } = "";

    public int Stars { get; set; }

    public string StarsText => Stars > 0 ? new string('★', Math.Min(Stars, 3)) : "未评级";

    public bool IsCurrent { get; set; }

    public List<WarEchoDungeonView> Dungeons { get; set; } = [];
}


public sealed class WarEchoHonorView
{
    public string CountText { get; set; } = "0";

    public string Label { get; set; } = "";

    public Brush AccentBrush { get; set; } = EndfieldRecordPalette.Muted;
}


public sealed class WarEchoAchieveView
{
    public string Name { get; set; } = "";

    public int Star { get; set; }

    public string StarText { get; set; } = "";

    public string DateText { get; set; } = "";

    public bool HasStar1 => Star >= 1;

    public bool HasStar2 => Star >= 2;

    public bool HasStar3 => Star >= 3;
}


public sealed class WarEchoView
{
    public string SeasonName { get; set; } = "战争回响";

    public string DateRange { get; set; } = "";

    public string HeaderImage { get; set; } = "";

    public string KvImage { get; set; } = "";

    public int Stars { get; set; }

    public string StarsText => Stars > 0 ? Stars.ToString(CultureInfo.CurrentCulture) : "—";

    public List<WarEchoHonorView> Honors { get; set; } = [];

    public List<WarEchoWeekView> Weeks { get; set; } = [];

    public bool HasWeeks => Weeks.Count > 0;

    public List<WarEchoAchieveView> Achieves { get; set; } = [];

    public bool HasAchieves => Achieves.Count > 0;

    public string AchieveCountText => $"共 {Achieves.Count} 项";
}


public sealed class MonumentProgressSegment
{
    public bool IsFilled { get; set; }

    public Brush Fill { get; set; } = EndfieldRecordPalette.SegmentEmpty;
}


public sealed class MonumentStageView
{
    public string Name { get; set; } = "";

    public bool IsHard { get; set; }

    public bool IsPass { get; set; }

    public string DurationText { get; set; } = "";

    public string TimestampText { get; set; } = "";

    public string DateText { get; set; } = "";

    public string RecommendLevelText { get; set; } = "";

    public string Description { get; set; } = "";

    public string FeatureText { get; set; } = "";

    public bool HasFeature => !string.IsNullOrWhiteSpace(FeatureText);

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public List<EndfieldEnemyView> Enemies { get; set; } = [];

    public bool HasEnemies => Enemies.Count > 0;

    public List<EndfieldCharView> Chars { get; set; } = [];

    public bool HasChars => Chars.Count > 0;

    public string StatusText => IsPass ? "已通关" : "未通关";

    public string DifficultyText => IsHard ? "苦难" : "普通";
}


public sealed class MonumentActivityView
{
    public string Name { get; set; } = "";

    public string ActivityName { get; set; } = "";

    public string Pic { get; set; } = "";

    public string DateRange { get; set; } = "";

    public string AchievementName { get; set; } = "";

    public string AchievementIcon { get; set; } = "";

    public bool HasAchievementIcon => !string.IsNullOrWhiteSpace(AchievementIcon);

    public bool IsAchieved { get; set; }

    public bool IsPlated { get; set; }

    public string AchievementStateText => IsAchieved ? (IsPlated ? "蚀刻章已镀层" : "蚀刻章已获得") : "蚀刻章未获得";

    public int ProgressValue { get; set; }

    public int ProgressMax { get; set; } = 4;

    public string ProgressText => $"{ProgressValue} / {ProgressMax}";

    public List<MonumentProgressSegment> ProgressSegments { get; set; } = [];

    public List<MonumentStageView> Stages { get; set; } = [];

    public bool HasStages => Stages.Count > 0;

    public string StageCountText => $"共 {Stages.Count} 个关卡";
}


/// <summary>
/// 终末地三类赛季战绩的解析入口，布局只消费解析后的视图模型。
/// </summary>
internal static class EndfieldRecordParser
{
    public static CrisisContractView ParseCrisisContract(JsonElement root)
    {
        if (!TryGet(root, "data", out JsonElement data) || !TryGet(data, "crisisContract", out JsonElement contract))
        {
            return new CrisisContractView();
        }

        string seasonName = "危机合约";
        string dateRange = "";
        string bannerImage = "";
        string badgeImage = "";
        string badgeName = "";
        string badgeLevel = "";
        string achievementCategory = "";
        string weeklyText = "—";
        string indicatorMissionText = "—";
        string stageText = "—";
        int highest = 0;
        int challengeCount = 0;
        JsonElement statusElement = default;

        if (TryGet(contract, "status", out JsonElement status))
        {
            statusElement = status;
            seasonName = Str(status, "name").Trim();
            if (string.IsNullOrWhiteSpace(seasonName))
            {
                seasonName = "危机合约";
            }
            dateRange = Long(status, "gameplayEndAtTs") > 0
                ? FormatDateRange(status, "startAtTs", "gameplayEndAtTs")
                : FormatDateRange(status, "startAtTs", "endAtTs");
            bannerImage = Str(status, "kvImage");
            if (string.IsNullOrWhiteSpace(bannerImage))
            {
                bannerImage = Str(status, "headerImage");
            }
            highest = Int(status, "highest");
            challengeCount = Int(status, "challengeCount");
            weeklyText = MissionText(status, "weeklyMission");
            indicatorMissionText = MissionText(status, "indicatorMission");
            stageText = MissionText(status, "stageMission");
            if (TryGet(status, "achieve", out JsonElement achieve))
            {
                if (TryGet(achieve, "achievementData", out JsonElement achievement))
                {
                    badgeName = Str(achievement, "name").Trim('"', '“', '”');
                    badgeImage = Str(achievement, "initIcon");
                    if (string.IsNullOrWhiteSpace(badgeImage))
                    {
                        badgeImage = Str(achievement, "platedIcon");
                    }
                    achievementCategory = Str(achievement, "cateName");
                }
                int level = Int(achieve, "level");
                bool plated = Bool(achieve, "isPlated");
                badgeLevel = level > 0 ? $"Lv.{level}{(plated ? " · 镀层" : "")}" : "未获得";
            }
        }

        string dungeonName = "";
        string dungeonDescription = "";
        List<EndfieldEnemyView> enemies = [];
        if (TryGet(contract, "dungeon", out JsonElement dungeon))
        {
            dungeonName = Str(dungeon, "name");
            dungeonDescription = CleanMarkup(Str(dungeon, "desc"));
            enemies = ParseEnemies(dungeon);
        }

        CrisisContractRecordView? bestRecord = null;
        List<CrisisContractRecordView> records = [];
        if (TryGet(contract, "history", out JsonElement history))
        {
            if (TryGet(history, "bestRecord", out JsonElement bestElement) && bestElement.ValueKind is JsonValueKind.Object)
            {
                bestRecord = ParseCrisisRecord(bestElement, isBest: true);
            }
            if (TryGet(history, "records", out JsonElement recordArray) && recordArray.ValueKind is JsonValueKind.Array)
            {
                foreach (JsonElement record in recordArray.EnumerateArray())
                {
                    records.Add(ParseCrisisRecord(record, Bool(record, "isBest")));
                }
            }
        }

        List<CrisisContractIndicatorView> indicators = [];
        if (TryGet(contract, "indicators", out JsonElement indicatorArray) && indicatorArray.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement indicator in indicatorArray.EnumerateArray())
            {
                int type = Int(indicator, "type");
                indicators.Add(new CrisisContractIndicatorView
                {
                    Id = Str(indicator, "id"),
                    Name = Str(indicator, "name"),
                    Description = CleanMarkup(Str(indicator, "desc"), indicator),
                    Icon = Str(indicator, "icon"),
                    ScoreText = $"积分 {Int(indicator, "score")}",
                    TypeText = type switch { 1 => "合约指标", 2 => "环境条件", _ => "附加条件" },
                    IsUnlock = Bool(indicator, "isUnlock"),
                });
            }
        }

        return new CrisisContractView
        {
            SeasonName = seasonName,
            SeasonType = "危机合约",
            DateRange = dateRange,
            BannerImage = bannerImage,
            BadgeImage = badgeImage,
            BadgeName = badgeName,
            BadgeLevelText = badgeLevel,
            Highest = highest,
            AchievementName = badgeName,
            AchievementDescription = achievementCategory,
            AchievementIcon = badgeImage,
            Stats =
            [
                new EndfieldStatView { Glyph = "\uE9D9", Label = "挑战次数", Value = challengeCount.ToString(CultureInfo.CurrentCulture) },
                new EndfieldStatView { Glyph = "\uE823", Label = "周期任务", Value = weeklyText },
                new EndfieldStatView { Glyph = "\uE8A5", Label = "指标任务", Value = indicatorMissionText },
                new EndfieldStatView { Glyph = "\uE73E", Label = "阶段任务", Value = stageText },
            ],
            DungeonName = dungeonName,
            DungeonDescription = dungeonDescription,
            Enemies = enemies,
            BestRecord = bestRecord,
            Records = records,
            Indicators = indicators,
        };
    }


    public static List<WarEchoView> ParseWarEchoes(JsonElement root)
    {
        List<WarEchoView> views = [];
        if (!TryGet(root, "data", out JsonElement data) || !TryGet(data, "warEchoes", out JsonElement warEchoes))
        {
            return views;
        }

        List<WarEchoAchieveView> achieves = [];
        if (TryGet(warEchoes, "achieves", out JsonElement achieveArray) && achieveArray.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement achieve in achieveArray.EnumerateArray())
            {
                int star = Int(achieve, "star");
                achieves.Add(new WarEchoAchieveView
                {
                    Name = Str(achieve, "name"),
                    Star = star,
                    StarText = star > 0 ? new string('★', Math.Min(star, 3)) : "未评级",
                    DateText = FormatTimestamp(Str(achieve, "firstPassTs"), "yyyy-MM-dd"),
                });
            }
        }

        int honor3 = achieves.Count(x => x.Star >= 3);
        int honor2 = achieves.Count(x => x.Star == 2);
        int honor1 = achieves.Count(x => x.Star <= 1);

        if (!TryGet(warEchoes, "seasons", out JsonElement seasons) || seasons.ValueKind is not JsonValueKind.Array)
        {
            return views;
        }
        foreach (JsonElement season in seasons.EnumerateArray())
        {
            List<WarEchoWeekView> weeks = [];
            if (TryGet(season, "weeks", out JsonElement weekArray) && weekArray.ValueKind is JsonValueKind.Array)
            {
                foreach (JsonElement week in weekArray.EnumerateArray())
                {
                    List<WarEchoDungeonView> dungeons = [];
                    if (TryGet(week, "dungeonGroups", out JsonElement groups) && groups.ValueKind is JsonValueKind.Array)
                    {
                        foreach (JsonElement group in groups.EnumerateArray())
                        {
                            dungeons.Add(ParseWarEchoDungeon(group));
                        }
                    }
                    long weekStart = Long(week, "startTs");
                    long weekEnd = Long(week, "endTs");
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    weeks.Add(new WarEchoWeekView
                    {
                        Name = Str(week, "name"),
                        DateRange = FormatDateRange(week, "startTs", "endTs"),
                        Stars = Int(week, "stars"),
                        IsCurrent = weekStart > 0 && weekEnd > 0 && weekStart <= now && now < weekEnd,
                        Dungeons = dungeons,
                    });
                }
            }
            views.Add(new WarEchoView
            {
                SeasonName = Str(season, "name"),
                DateRange = FormatDateRange(season, "startTs", "endTs"),
                HeaderImage = Str(season, "headerImage"),
                KvImage = Str(season, "kvImage"),
                Stars = Int(season, "stars"),
                Honors =
                [
                    new WarEchoHonorView { CountText = honor3.ToString(CultureInfo.CurrentCulture), Label = "三阶荣誉", AccentBrush = EndfieldRecordPalette.Gold },
                    new WarEchoHonorView { CountText = honor2.ToString(CultureInfo.CurrentCulture), Label = "二阶荣誉", AccentBrush = EndfieldRecordPalette.Silver },
                    new WarEchoHonorView { CountText = honor1.ToString(CultureInfo.CurrentCulture), Label = "一阶荣誉", AccentBrush = EndfieldRecordPalette.Bronze },
                ],
                Weeks = weeks,
                Achieves = achieves,
            });
        }
        return views;
    }


    public static List<MonumentActivityView> ParseUmbralMonument(JsonElement root)
    {
        List<MonumentActivityView> views = [];
        if (!TryGet(root, "data", out JsonElement data)
            || !TryGet(data, "indieHard", out JsonElement indieHard)
            || !TryGet(indieHard, "indieHardGroups", out JsonElement groups)
            || groups.ValueKind is not JsonValueKind.Array)
        {
            return views;
        }

        foreach (JsonElement group in groups.EnumerateArray())
        {
            string achievementName = "";
            string achievementIcon = "";
            bool achieved = false;
            bool plated = false;
            int level = 0;
            if (TryGet(group, "achieve", out JsonElement achieve))
            {
                level = Int(achieve, "level");
                achieved = level > 0 || Int(achieve, "obtainTs") > 0;
                plated = Bool(achieve, "isPlated");
                if (TryGet(achieve, "achievementData", out JsonElement achievement))
                {
                    achievementName = Str(achievement, "name").Trim('"', '“', '”');
                    achievementIcon = plated ? Str(achievement, "platedIcon") : Str(achievement, "initIcon");
                    if (string.IsNullOrWhiteSpace(achievementIcon))
                    {
                        achievementIcon = Str(achievement, "initIcon");
                    }
                }
            }

            List<MonumentStageView> stages = [];
            if (TryGet(group, "dungeonGroups", out JsonElement stageGroups) && stageGroups.ValueKind is JsonValueKind.Array)
            {
                foreach (JsonElement stageGroup in stageGroups.EnumerateArray())
                {
                    if (TryGet(stageGroup, "normalDungeon", out JsonElement normal) && normal.ValueKind is JsonValueKind.Object)
                    {
                        stages.Add(ParseMonumentStage(normal, isHard: false));
                    }
                    if (TryGet(stageGroup, "hardDungeon", out JsonElement hard) && hard.ValueKind is JsonValueKind.Object)
                    {
                        stages.Add(ParseMonumentStage(hard, isHard: true));
                    }
                }
            }

            int progressMax = 4;
            int progressValue = Math.Clamp(level, 0, progressMax);
            List<MonumentProgressSegment> segments = [];
            for (int i = 0; i < progressMax; i++)
            {
                segments.Add(new MonumentProgressSegment
                {
                    IsFilled = i < progressValue,
                    Fill = i < progressValue ? EndfieldRecordPalette.Gold : EndfieldRecordPalette.SegmentEmpty,
                });
            }

            string start = FormatTimestamp(Str(group, "activityStartTs"), "yyyy/MM/dd");
            string end = FormatTimestamp(Str(group, "activityEndTs"), "yyyy/MM/dd");
            bool isInActivity = Bool(group, "isInActivity");
            views.Add(new MonumentActivityView
            {
                Name = Str(group, "name"),
                ActivityName = Str(group, "activityName"),
                Pic = Str(group, "pic"),
                DateRange = string.IsNullOrWhiteSpace(end) || end == "—"
                    ? (isInActivity ? $"{start} - 进行中" : start)
                    : $"{start} - {end}",
                AchievementName = achievementName,
                AchievementIcon = achievementIcon,
                IsAchieved = achieved,
                IsPlated = plated,
                ProgressValue = progressValue,
                ProgressMax = progressMax,
                ProgressSegments = segments,
                Stages = stages,
            });
        }
        return views;
    }


    private static CrisisContractRecordView ParseCrisisRecord(JsonElement record, bool isBest)
    {
        long timestamp = Long(record, "ts");
        int passTs = Int(record, "passTs");
        return new CrisisContractRecordView
        {
            Id = Str(record, "id"),
            IsPass = Bool(record, "isPass"),
            IsBest = isBest,
            IndicatorCount = Int(record, "indicatorCount"),
            PassWave = Int(record, "passWave"),
            TimeAgo = RelativeTime(timestamp),
            TimestampText = FormatTimestamp(Str(record, "ts"), "yyyy-MM-dd HH:mm"),
            DurationText = FormatDuration(passTs),
            ShowNewBadge = isBest,
            Chars = ParseChars(record),
        };
    }


    private static WarEchoDungeonView ParseWarEchoDungeon(JsonElement group)
    {
        string name = Str(group, "name");
        int star = Int(group, "star");
        JsonElement dungeon = default;
        foreach (string key in new[] { "cruelDungeon", "hardDungeon", "normalDungeon" })
        {
            if (TryGet(group, key, out JsonElement candidate) && candidate.ValueKind is JsonValueKind.Object)
            {
                dungeon = candidate;
                break;
            }
        }
        if (dungeon.ValueKind is not JsonValueKind.Object)
        {
            return new WarEchoDungeonView { Name = name, Star = star };
        }

        bool isPass = Bool(dungeon, "isPass");
        int passTs = 0;
        long timestamp = 0;
        List<EndfieldCharView> chars = [];
        if (TryGet(dungeon, "bestRecord", out JsonElement bestRecord) && bestRecord.ValueKind is JsonValueKind.Object)
        {
            passTs = Int(bestRecord, "passTs");
            timestamp = Long(bestRecord, "ts");
            chars = ParseChars(bestRecord);
        }
        return new WarEchoDungeonView
        {
            Name = string.IsNullOrWhiteSpace(name) ? Str(dungeon, "name") : name,
            Description = CleanMarkup(Str(dungeon, "desc")),
            FeatureText = CleanMarkup(Str(dungeon, "feature")),
            RecommendLevelText = Int(dungeon, "recommendLevel") > 0 ? $"推荐等级 {Int(dungeon, "recommendLevel")}" : "",
            IsPass = isPass,
            HasRecord = chars.Count > 0,
            DurationText = FormatDuration(passTs),
            DateText = FormatTimestamp(timestamp.ToString(CultureInfo.InvariantCulture), "yyyy-MM-dd HH:mm"),
            Star = star,
            Chars = chars,
            Enemies = ParseEnemies(dungeon),
        };
    }


    private static MonumentStageView ParseMonumentStage(JsonElement dungeon, bool isHard)
    {
        int passTs = 0;
        long timestamp = 0;
        List<EndfieldCharView> chars = [];
        if (TryGet(dungeon, "bestRecord", out JsonElement bestRecord) && bestRecord.ValueKind is JsonValueKind.Object)
        {
            passTs = Int(bestRecord, "passTs");
            timestamp = Long(bestRecord, "ts");
            chars = ParseChars(bestRecord);
        }
        string timeText = FormatTimestamp(timestamp.ToString(CultureInfo.InvariantCulture), "yyyy-MM-dd HH:mm");
        return new MonumentStageView
        {
            Name = Str(dungeon, "name"),
            IsHard = isHard,
            IsPass = Bool(dungeon, "isPass"),
            DurationText = passTs > 0 ? FormatDuration(passTs) : "未通关",
            TimestampText = timeText,
            DateText = timeText,
            RecommendLevelText = Int(dungeon, "recommendLevel") > 0 ? $"推荐等级 {Int(dungeon, "recommendLevel")}" : "",
            Description = CleanMarkup(Str(dungeon, "desc")),
            FeatureText = CleanMarkup(Str(dungeon, "feature")),
            Enemies = ParseEnemies(dungeon),
            Chars = chars,
        };
    }


    private static List<EndfieldCharView> ParseChars(JsonElement record)
    {
        List<EndfieldCharView> chars = [];
        if (!TryGet(record, "chars", out JsonElement array) || array.ValueKind is not JsonValueKind.Array)
        {
            return chars;
        }
        foreach (JsonElement character in array.EnumerateArray())
        {
            int level = Int(character, "level");
            int potential = Int(character, "potentialLevel");
            int rarity = 0;
            if (TryGet(character, "rarity", out JsonElement rarityElement))
            {
                rarity = int.TryParse(Str(rarityElement, "value"), out int parsed) ? parsed : 0;
            }
            string property = "";
            if (TryGet(character, "property", out JsonElement propertyElement))
            {
                property = Str(propertyElement, "value");
            }
            EndfieldCharView view = new()
            {
                CharId = Str(character, "charId"),
                AvatarUrl = Str(character, "avatarUrl"),
                LevelText = level > 0 ? $"LV {level}" : "",
                LevelNumberText = level > 0 ? level.ToString(CultureInfo.CurrentCulture) : "—",
                RarityText = rarity > 0 ? rarity.ToString(CultureInfo.CurrentCulture) : "",
                PotentialText = potential > 0 ? $"命座 {potential}" : "",
                PotentialLevel = potential,
                PropertyText = property,
                AccentBrush = EndfieldRecordPalette.Rarity(rarity),
                EquipSlots = ParseEquipSlots(character),
            };
            view.PotentialIconUrl = ParsePotentialIcon(character, potential);
            ApplyWeapon(character, view);
            chars.Add(view);
        }
        return chars;
    }


    private static string ParsePotentialIcon(JsonElement character, int potentialLevel)
    {
        string icon = FirstString(character, "potentialIcon", "potentialIconUrl", "potentialImage");
        if (!string.IsNullOrWhiteSpace(icon))
        {
            return icon;
        }
        if (TryGet(character, "potential", out JsonElement potential) && potential.ValueKind is JsonValueKind.Object)
        {
            icon = FirstString(potential, "icon", "imageUrl", "iconUrl");
            if (!string.IsNullOrWhiteSpace(icon))
            {
                return icon;
            }
        }
        if (TryGet(character, "potentials", out JsonElement potentials) && potentials.ValueKind is JsonValueKind.Array)
        {
            string fallback = "";
            foreach (JsonElement item in potentials.EnumerateArray())
            {
                if (item.ValueKind is not JsonValueKind.Object)
                {
                    continue;
                }
                string itemIcon = FirstString(item, "icon", "imageUrl", "iconUrl");
                if (string.IsNullOrWhiteSpace(itemIcon))
                {
                    continue;
                }
                int level = Int(item, "level");
                if (level == potentialLevel)
                {
                    return itemIcon;
                }
                fallback = itemIcon;
            }
            return fallback;
        }
        return "";
    }


    private static void ApplyWeapon(JsonElement character, EndfieldCharView view)
    {
        foreach (string key in new[] { "weapon", "weaponData", "weaponInfo" })
        {
            if (TryGet(character, key, out JsonElement weapon) && weapon.ValueKind is JsonValueKind.Object)
            {
                view.WeaponName = Str(weapon, "name");
                view.WeaponIcon = FirstString(weapon, "icon", "imageUrl", "avatarUrl", "iconUrl");
                int level = Int(weapon, "level");
                view.WeaponLevelText = level > 0 ? $"Lv.{level}" : "";
                return;
            }
        }
    }


    /// <summary>
    /// 从 card/detail 接口读取信用余额。
    /// </summary>
    public static void ApplyCredit(EndfieldDailyNote note, JsonElement root)
    {
        if (!TryGet(root, "data", out JsonElement data) || data.ValueKind is not JsonValueKind.Object)
        {
            return;
        }
        JsonElement credit = default;
        if (TryGet(data, "credit", out JsonElement direct) && direct.ValueKind is JsonValueKind.Object)
        {
            credit = direct;
        }
        else if (TryGet(data, "detail", out JsonElement detail) && detail.ValueKind is JsonValueKind.Object &&
                 TryGet(detail, "credit", out JsonElement nested) && nested.ValueKind is JsonValueKind.Object)
        {
            credit = nested;
        }
        if (credit.ValueKind is not JsonValueKind.Object)
        {
            return;
        }
        int current = Int(credit, "count");
        if (current == 0) current = Int(credit, "current");
        if (current == 0) current = Int(credit, "value");
        int max = Int(credit, "total");
        if (max == 0) max = Int(credit, "max");
        if (max == 0) max = Int(credit, "max_value");
        note.Credit = current;
        note.CreditMax = max;
    }


    /// <summary>
    /// 解析干员详情接口（card/char?charId=）返回的武器、四件装备与潜能等级。
    /// </summary>
    public static EndfieldCharView? ParseCharacterEquipment(JsonElement root)
    {
        if (!TryGet(root, "data", out JsonElement data)
            || !TryGet(data, "detail", out JsonElement detail)
            || detail.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }

        int level = Int(detail, "level");
        int potential = Int(detail, "potentialLevel");
        EndfieldCharView view = new()
        {
            LevelNumberText = level > 0 ? level.ToString(CultureInfo.CurrentCulture) : "",
            LevelText = level > 0 ? $"LV {level}" : "",
            PotentialLevel = potential,
            PotentialText = potential > 0 ? $"命座 {potential}" : "",
            EquipSlots = [],
        };

        if (TryGet(detail, "weapon", out JsonElement weapon)
            && TryGet(weapon, "weaponData", out JsonElement weaponData))
        {
            view.WeaponName = Str(weaponData, "name");
            view.WeaponIcon = FirstString(weaponData, "iconUrl", "icon", "imageUrl");
            int weaponLevel = Int(weapon, "level");
            view.WeaponLevelText = weaponLevel > 0 ? $"Lv.{weaponLevel}" : "";
        }

        foreach (string key in new[] { "bodyEquip", "armEquip", "firstAccessory", "secondAccessory" })
        {
            EndfieldEquipSlot slot = new();
            if (TryGet(detail, key, out JsonElement equip) && TryGet(equip, "equipData", out JsonElement equipData))
            {
                slot.Name = Str(equipData, "name");
                slot.Icon = FirstString(equipData, "iconUrl", "icon", "imageUrl");
                slot.LevelText = EquipLevelText(equipData);
            }
            view.EquipSlots.Add(slot);
        }
        return view;
    }


    private static string EquipLevelText(JsonElement equipData)
    {
        if (TryGet(equipData, "level", out JsonElement level))
        {
            if (level.ValueKind is JsonValueKind.Object)
            {
                string value = Str(level, "value");
                return int.TryParse(value, out int number) && number > 0 ? $"Lv.{number}" : "";
            }
            if (level.ValueKind is JsonValueKind.Number && level.TryGetInt32(out int direct) && direct > 0)
            {
                return $"Lv.{direct}";
            }
        }
        return "";
    }


    private static List<EndfieldEquipSlot> ParseEquipSlots(JsonElement character)
    {
        List<EndfieldEquipSlot> slots = [];
        foreach (string key in new[] { "equips", "equipments", "equipmentList", "equipment", "items" })
        {
            if (TryGet(character, key, out JsonElement array) && array.ValueKind is JsonValueKind.Array)
            {
                foreach (JsonElement item in array.EnumerateArray())
                {
                    if (item.ValueKind is not JsonValueKind.Object)
                    {
                        continue;
                    }
                    int level = Int(item, "level");
                    slots.Add(new EndfieldEquipSlot
                    {
                        Name = Str(item, "name"),
                        Icon = FirstString(item, "icon", "imageUrl", "avatarUrl", "iconUrl"),
                        LevelText = level > 0 ? $"Lv.{level}" : "",
                    });
                }
                break;
            }
        }
        while (slots.Count < 4)
        {
            slots.Add(new EndfieldEquipSlot());
        }
        return slots.Count > 4 ? slots.Take(4).ToList() : slots;
    }


    /// <summary>
    /// 解析单次挑战详情；森空岛未公开字段，按容错方式读取干员装备与本次启用的指标。
    /// </summary>
    public static CrisisContractRecordDetail ParseCrisisRecordDetail(JsonElement root)
    {
        CrisisContractRecordDetail detail = new();
        if (!TryGet(root, "data", out JsonElement data))
        {
            return detail;
        }

        JsonElement record = data;
        foreach (string key in new[] { "recordDetail", "record", "detail", "challenge", "challengeRecord", "history" })
        {
            if (TryGet(data, key, out JsonElement candidate) && candidate.ValueKind is JsonValueKind.Object)
            {
                record = candidate;
                break;
            }
        }

        detail.Chars = ParseChars(record);
        foreach (string key in new[] { "indicators", "indicatorList", "selectedIndicators", "contracts", "indicator" })
        {
            if (TryGet(record, key, out JsonElement array) && array.ValueKind is JsonValueKind.Array)
            {
                detail.Indicators = ParseIndicatorArray(array);
                break;
            }
        }
        return detail;
    }


    private static List<CrisisContractIndicatorView> ParseIndicatorArray(JsonElement array)
    {
        List<CrisisContractIndicatorView> indicators = [];
        foreach (JsonElement indicator in array.EnumerateArray())
        {
            if (indicator.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }
            int score = Int(indicator, "score");
            if (score <= 0)
            {
                score = Int(indicator, "value");
            }
            indicators.Add(new CrisisContractIndicatorView
            {
                Id = Str(indicator, "id"),
                Name = Str(indicator, "name"),
                Description = CleanMarkup(Str(indicator, "desc"), indicator),
                Icon = FirstString(indicator, "icon", "imageUrl", "avatarUrl", "iconUrl"),
                ScoreText = score > 0 ? $"积分 {score}" : "",
                TypeText = "本次启用",
                IsUnlock = true,
            });
        }
        return indicators;
    }


    private static string FirstString(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            string value = Str(element, name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return "";
    }


    private static List<EndfieldEnemyView> ParseEnemies(JsonElement dungeon)
    {
        List<EndfieldEnemyView> enemies = [];
        if (!TryGet(dungeon, "enemies", out JsonElement array) || array.ValueKind is not JsonValueKind.Array)
        {
            return enemies;
        }
        foreach (JsonElement enemy in array.EnumerateArray())
        {
            int level = Int(enemy, "level");
            enemies.Add(new EndfieldEnemyView
            {
                Name = Str(enemy, "name"),
                LevelText = level > 0 ? $"Lv.{level}" : "",
                ImageUrl = Str(enemy, "imageUrl"),
                Description = CleanMarkup(Str(enemy, "desc")),
                Ability = CleanMarkup(Str(enemy, "ability")),
            });
        }
        return enemies;
    }


    private static string MissionText(JsonElement status, string name)
    {
        if (TryGet(status, name, out JsonElement mission))
        {
            return $"{Int(mission, "count")} / {Int(mission, "total")}";
        }
        return "—";
    }


    private static double MissionPercent(JsonElement status, string name)
    {
        if (TryGet(status, name, out JsonElement mission))
        {
            int total = Int(mission, "total");
            if (total > 0)
            {
                return Math.Clamp(Int(mission, "count") * 100d / total, 0, 100);
            }
        }
        return 0;
    }


    private static string FormatDateRange(JsonElement element, string startName, string endName)
    {
        string start = FormatTimestamp(Str(element, startName), "yyyy/MM/dd");
        string end = FormatTimestamp(Str(element, endName), "yyyy/MM/dd");
        if (string.IsNullOrWhiteSpace(start) || start == "—")
        {
            return end;
        }
        return string.IsNullOrWhiteSpace(end) || end == "—" ? start : $"{start} - {end}";
    }


    private static string FormatTimestamp(string value, string format)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp) && timestamp > 0)
        {
            if (timestamp > 100_000_000_000)
            {
                timestamp /= 1000;
            }
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
            }
            catch (ArgumentOutOfRangeException)
            {
                return "—";
            }
        }
        return "—";
    }


    private static string RelativeTime(long timestamp)
    {
        if (timestamp <= 0)
        {
            return "";
        }
        if (timestamp > 100_000_000_000)
        {
            timestamp /= 1000;
        }
        try
        {
            DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime();
            TimeSpan span = DateTimeOffset.Now - time;
            if (span.TotalMinutes < 1)
            {
                return "刚刚";
            }
            if (span.TotalHours < 1)
            {
                return $"{Math.Max(1, (int)span.TotalMinutes)} 分钟前";
            }
            if (span.TotalDays < 1)
            {
                return $"{(int)span.TotalHours} 小时前";
            }
            if (span.TotalDays < 30)
            {
                return $"{(int)span.TotalDays} 天前";
            }
            return time.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return "";
        }
    }


    private static string FormatDuration(int seconds)
    {
        if (seconds <= 0)
        {
            return "--:--";
        }
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }


    private static readonly Regex _markupRegex = new("<[^>]*>", RegexOptions.Compiled);

    private static readonly Regex _placeholderRegex = new(@"\{@?[0-9a-zA-Z_\-]*@?([0-9a-zA-Z_]+):([^{}]*)\}", RegexOptions.Compiled);


    private static string CleanMarkup(string text, JsonElement? paramsHost = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }
        string cleaned = _markupRegex.Replace(text, "");
        cleaned = _placeholderRegex.Replace(cleaned, match =>
        {
            string key = match.Groups[1].Value;
            string format = match.Groups[2].Value;
            if (paramsHost is JsonElement host
                && TryGet(host, "descParams", out JsonElement parameters)
                && TryGet(parameters, key, out JsonElement value))
            {
                string raw = value.ValueKind is JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    if (format.Contains('%'))
                    {
                        int digits = format.Contains(".0") ? 1 : 0;
                        return (number * 100).ToString($"F{digits}", CultureInfo.CurrentCulture) + "%";
                    }
                    return number.ToString(CultureInfo.CurrentCulture);
                }
            }
            return "";
        });
        return cleaned.Replace("  ", " ").Trim();
    }


    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out value))
        {
            return true;
        }
        value = default;
        return false;
    }


    private static string Str(JsonElement element, string name)
    {
        if (!TryGet(element, name, out JsonElement value))
        {
            return "";
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => "",
        };
    }


    private static int Int(JsonElement element, string name)
    {
        if (!TryGet(element, name, out JsonElement value))
        {
            return 0;
        }
        if (value.ValueKind is JsonValueKind.Number && value.TryGetInt32(out int number))
        {
            return number;
        }
        return int.TryParse(Str(element, name), out number) ? number : 0;
    }


    private static long Long(JsonElement element, string name)
    {
        if (!TryGet(element, name, out JsonElement value))
        {
            return 0;
        }
        if (value.ValueKind is JsonValueKind.Number && value.TryGetInt64(out long number))
        {
            return number;
        }
        return long.TryParse(Str(element, name), out number) ? number : 0;
    }


    private static bool Bool(JsonElement element, string name)
    {
        return TryGet(element, name, out JsonElement value) && value.ValueKind is JsonValueKind.True;
    }
}


/// <summary>
/// 页面共用的固定配色，与截图中的金色强调、徽章层级保持一致。
/// </summary>
internal static class EndfieldRecordPalette
{
    public static readonly Brush Gold = new SolidColorBrush(Color.FromArgb(0xFF, 0xE7, 0xC5, 0x67));

    public static readonly Brush Silver = new SolidColorBrush(Color.FromArgb(0xFF, 0xB9, 0xC4, 0xD2));

    public static readonly Brush Bronze = new SolidColorBrush(Color.FromArgb(0xFF, 0xC9, 0x8B, 0x5E));

    public static readonly Brush Muted = new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x8F, 0x99));

    public static readonly Brush SegmentEmpty = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

    public static readonly Brush EmptyStar = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));

    public static readonly Brush Rarity6 = new SolidColorBrush(Color.FromArgb(0xFF, 0xE7, 0xC5, 0x67));

    public static readonly Brush Rarity5 = new SolidColorBrush(Color.FromArgb(0xFF, 0xB9, 0x8C, 0xFF));

    public static readonly Brush Rarity4 = new SolidColorBrush(Color.FromArgb(0xFF, 0x6F, 0xA8, 0xFF));

    public static readonly Brush Rarity3 = new SolidColorBrush(Color.FromArgb(0xFF, 0x7F, 0xB8, 0xC9));


    public static Brush Rarity(int rarity) => rarity switch
    {
        >= 6 => Rarity6,
        5 => Rarity5,
        4 => Rarity4,
        _ => Rarity3,
    };
}
