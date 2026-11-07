using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.Hypergryph;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Gacha.Endfield;

internal sealed partial class EndfieldGachaService
{

    private const string LauncherDashboardApi = "https://launcher.hypergryph.com/api/game/get_kanban";



    public async Task<EndfieldDailyNote> GetDailyNoteAsync(EndfieldGachaAccount account, bool forceUpdate = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        string loginToken = GetSavedLoginToken(account.AccountKey);
        string appCode = HypergryphGameConstants.GetGameProfile(GameBiz.endfield_cn).GameAppCode;
        string url = $"{LauncherDashboardApi}?game_appcode={Uri.EscapeDataString(appCode)}" +
                     $"&token={Uri.EscapeDataString(loginToken)}" +
                     $"&uid={Uri.EscapeDataString(account.Uid)}" +
                     $"&role_id={Uri.EscapeDataString(account.RoleId)}" +
                     $"&language=zh-cn";
        using var request = CreateBrowserRequest(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Referer", "https://launcher.hypergryph.com/");
        request.Headers.TryAddWithoutValidation("Origin", "https://launcher.hypergryph.com");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"读取终末地实时数据失败（HTTP {(int)response.StatusCode}），请重新登录鹰角账号。");
        }
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("biz_code", out JsonElement bizCode) && bizCode.ValueKind == JsonValueKind.Number &&
            bizCode.GetInt32() != 0)
        {
            throw new InvalidOperationException($"读取终末地实时数据失败（biz_code={bizCode.GetInt32()}），请重新登录鹰角账号。");
        }
        if (!root.TryGetProperty("beyond_kanban", out JsonElement kanban) || kanban.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("官方接口未返回终末地看板数据，请稍后重试。");
        }
        _logger.LogInformation("Endfield kanban: {Json}", kanban.GetRawText());

        var note = new EndfieldDailyNote
        {
            RoleName = ReadString(kanban, "nickname") ?? account.RoleName ?? "",
            Uid = account.Uid,
            RoleId = account.RoleId,
            ServerName = account.ServerName ?? "",
        };
        JsonElement ap = kanban.TryGetProperty("ap", out JsonElement apNode) && apNode.ValueKind == JsonValueKind.Object
            ? apNode
            : default;
        JsonElement stamina = kanban.TryGetProperty("sanity", out JsonElement sanityNode) && sanityNode.ValueKind == JsonValueKind.Object
            ? sanityNode
            : kanban;
        note.CurrentSanity = ReadInt(ap, ReadInt(stamina, 0, "current", "value", "current_sanity", "sanity", "stamina"), "current", "value");
        note.MaxSanity = ReadInt(ap, ReadInt(stamina, 0, "max", "max_sanity", "sanity_max", "max_stamina"), "max", "max_sanity");
        JsonElement level = GetObject(kanban, "level");
        note.Level = ReadInt(level, 0, "cur", "current");
        note.MaxLevel = ReadInt(level, 0, "max_level", "max");
        JsonElement battlePass = GetObject(kanban, "bp_level");
        note.BattlePassLevel = ReadInt(battlePass, 0, "cur", "current");
        note.MaxBattlePassLevel = ReadInt(battlePass, 0, "max_level", "max");
        JsonElement dailyTask = GetObject(kanban, "daily_task_info");
        note.DailyActivity = ReadInt(dailyTask, 0, "activity", "current");
        note.DailyActivityMax = ReadInt(dailyTask, 0, "complete_count", "max");
        JsonElement weeklyTask = GetObject(kanban, "weekly_task_info");
        note.WeeklyTask = ReadInt(weeklyTask, 0, "cur", "current");
        note.WeeklyTaskMax = ReadInt(weeklyTask, 0, "max");

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long fullTime = ReadLong(ap, 0, "complete_recovery_time", "full_time", "recover_full_time");
        if (fullTime <= 0)
        {
            fullTime = ReadLong(kanban, 0, "sanity_full_time", "full_time", "complete_recovery_time");
        }
        if (fullTime > now)
        {
            note.RecoverTime = TimeSpan.FromSeconds(fullTime - now);
        }
        else if (note.MaxSanity > note.CurrentSanity)
        {
            long recoverSeconds = ReadLong(ap, 0, "recover_time", "sanity_recover_time");
            if (recoverSeconds <= 0)
            {
                recoverSeconds = 360;
            }
            note.RecoverTime = TimeSpan.FromSeconds((note.MaxSanity - note.CurrentSanity) * recoverSeconds);
        }
        return note;
    }



    private static int ReadInt(JsonElement element, int defaultValue, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result))
                {
                    return result;
                }
                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out result))
                {
                    return result;
                }
            }
        }
        return defaultValue;
    }



    private static JsonElement GetObject(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) &&
               value.ValueKind == JsonValueKind.Object
            ? value
            : default;
    }



    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };
        }
        return null;
    }



    private static long ReadLong(JsonElement element, long defaultValue, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result))
                {
                    return result;
                }
                if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out result))
                {
                    return result;
                }
            }
        }
        return defaultValue;
    }


}
