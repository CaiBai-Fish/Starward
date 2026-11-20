using Microsoft.Extensions.Logging;
using Starward.Features.GameRecord.DailyNote;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.ArknightsRecord;

public sealed class ArknightsRecord
{
    public string RoleName { get; set; } = "";

    public string Uid { get; set; } = "";

    public int Level { get; set; }

    public string MainStage { get; set; } = "";

    public int CharCount { get; set; }

    public int SkinCount { get; set; }

    public string AvatarUrl { get; set; } = "";

    public List<ArknightsCampaignRecord> Campaigns { get; } = [];

    public List<ArknightsTowerRecord> Towers { get; } = [];

    public List<ArknightsRogueRecord> Rogues { get; } = [];

    public List<ArknightsActivityRecord> Activities { get; } = [];

    public List<ArknightsBossRushRecord> BossRushes { get; } = [];

    public List<ArknightsSandboxRecord> Sandboxes { get; } = [];

    public ArknightsGarrisonRecord? Garrison { get; set; }
}


public sealed class ArknightsCampaignRecord
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string ZoneName { get; set; } = "";

    public int MaxKills { get; set; }

    public string PicUrl { get; set; } = "";
}


public sealed class ArknightsTowerRecord
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string SubName { get; set; } = "";

    public int Best { get; set; }

    public string PicUrl { get; set; } = "";
}


public sealed class ArknightsRogueRecord
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public int ClearTime { get; set; }

    public int BpLevel { get; set; }

    public int MedalCurrent { get; set; }

    public int MedalTotal { get; set; }

    public string PicUrl { get; set; } = "";
}


public sealed class ArknightsActivityRecord
{
    public string ActId { get; set; } = "";

    public string Name { get; set; } = "";

    public int ClearedStages { get; set; }

    public int TotalStages { get; set; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    public string PicUrl { get; set; } = "";
}


public sealed class ArknightsBossRushRecord
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string StageName { get; set; } = "";

    public string Difficulty { get; set; } = "";

    public bool Played { get; set; }

    public string PicUrl { get; set; } = "";
}


public sealed class ArknightsSandboxRecord
{
    public string Name { get; set; } = "";

    public int MaxDay { get; set; }

    public int BaseLv { get; set; }

    public int EnemyKill { get; set; }

    public int SubQuestDone { get; set; }

    public int SubQuestTotal { get; set; }
}


public sealed class ArknightsGarrisonRecord
{
    public string SeasonName { get; set; } = "";

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    public List<ArknightsGarrisonMatch> Matches { get; } = [];
}


public sealed class ArknightsGarrisonMatch
{
    public long Timestamp { get; set; }

    public int KillCount { get; set; }

    public int Trophy { get; set; }

    public bool Completed { get; set; }

    public string BandId { get; set; } = "";
}


public sealed class ArknightsRecordService
{
    private const string SklandAppCode = "4ca99fa6b56cc2ba";

    private const string GrantApi = "https://as.hypergryph.com/user/oauth2/v2/grant";

    private const string CredApi = "https://zonai.skland.com/api/v1/user/auth/generate_cred_by_code";

    private const string SklandApi = "https://zonai.skland.com/api/v1";

    private const string SklandUserAgent = "Skland/1.32.1 (com.hypergryph.skland; build:103201004; Android 33; ) Okhttp/4.11.0";

    private readonly ILogger<ArknightsRecordService> _logger;

    private readonly HttpClient _httpClient;

    private readonly ArknightsDailyNoteService _dailyNoteService;


    public ArknightsRecordService(ILogger<ArknightsRecordService> logger, HttpClient httpClient,
        ArknightsDailyNoteService dailyNoteService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _dailyNoteService = dailyNoteService;
    }


    public async Task<ArknightsRecord> GetRecordAsync(CancellationToken cancellationToken = default)
    {
        ArknightsAccount? account = _dailyNoteService.GetAccount()
            ?? throw new InvalidOperationException("请先登录明日方舟的鹰角账号。");
        SklandCredential credential = await GetCredentialAsync(account.LoginToken, cancellationToken);
        string uid = Uri.EscapeDataString(account.Uid);
        ArknightsRecord record = new()
        {
            Uid = account.Uid,
            RoleName = account.NickName,
        };
        using (JsonDocument player = await GetSignedAsync($"{SklandApi}/game/player/info?uid={uid}", credential, cancellationToken))
        {
            ParsePlayer(player.RootElement, record);
        }
        try
        {
            await LoadGarrisonAsync(record, uid, credential, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load Arknights garrison record failed.");
        }
        return record;
    }


    private async Task<SklandCredential> GetCredentialAsync(string loginToken, CancellationToken cancellationToken)
    {
        using HttpRequestMessage grantRequest = new(HttpMethod.Post, GrantApi);
        grantRequest.Headers.TryAddWithoutValidation("User-Agent", SklandUserAgent);
        grantRequest.Content = JsonContent.Create(new { token = loginToken, appCode = SklandAppCode, type = 0 });
        using HttpResponseMessage grantResponse = await _httpClient.SendAsync(grantRequest, cancellationToken);
        using JsonDocument grant = await JsonDocument.ParseAsync(await grantResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!grantResponse.IsSuccessStatusCode || GetInt32(grant.RootElement, "status") != 0
            || !grant.RootElement.TryGetProperty("data", out JsonElement grantData)
            || !grantData.TryGetProperty("code", out JsonElement codeElement))
        {
            throw new InvalidOperationException("获取森空岛授权失败，请重新登录鹰角账号。");
        }
        string code = codeElement.GetString() ?? "";
        using HttpRequestMessage credRequest = new(HttpMethod.Post, CredApi);
        credRequest.Headers.TryAddWithoutValidation("User-Agent", SklandUserAgent);
        credRequest.Content = JsonContent.Create(new { kind = 1, code });
        using HttpResponseMessage credResponse = await _httpClient.SendAsync(credRequest, cancellationToken);
        using JsonDocument cred = await JsonDocument.ParseAsync(await credResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!credResponse.IsSuccessStatusCode || cred.RootElement.TryGetProperty("code", out JsonElement credCode) && GetInt32(cred.RootElement, "code") != 0
            || !cred.RootElement.TryGetProperty("data", out JsonElement credData))
        {
            throw new InvalidOperationException("获取森空岛凭证失败，请重新登录鹰角账号。");
        }
        return new SklandCredential
        {
            Cred = GetString(credData, "cred"),
            Token = GetString(credData, "token"),
            UserId = GetString(credData, "userId"),
        };
    }


    private async Task<JsonDocument> GetSignedAsync(string url, SklandCredential credential, CancellationToken cancellationToken)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
        Uri uri = new(url);
        string query = uri.Query.TrimStart('?');
        string headerJson = $"{{\"platform\":\"\",\"timestamp\":\"{timestamp}\",\"dId\":\"\",\"vName\":\"\"}}";
        string secret = $"{uri.AbsolutePath}{query}{timestamp}{headerJson}";
        byte[] hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(credential.Token), Encoding.UTF8.GetBytes(secret));
        string hexHash = Convert.ToHexString(hash).ToLowerInvariant();
        string sign = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(hexHash))).ToLowerInvariant();
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", SklandUserAgent);
        request.Headers.TryAddWithoutValidation("cred", credential.Cred);
        request.Headers.TryAddWithoutValidation("sign", sign);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp.ToString(CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("platform", "");
        request.Headers.TryAddWithoutValidation("dId", "");
        request.Headers.TryAddWithoutValidation("vName", "");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"森空岛接口请求失败（HTTP {(int)response.StatusCode}）。");
        }
        JsonDocument document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("code", out JsonElement code) && GetInt32(document.RootElement, "code") != 0)
        {
            string message = GetString(document.RootElement, "message");
            document.Dispose();
            throw new InvalidOperationException($"森空岛接口返回错误：{message}");
        }
        return document;
    }


    private static void ParsePlayer(JsonElement root, ArknightsRecord record)
    {
        if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidOperationException("森空岛未返回玩家数据。");
        }
        JsonElement status = GetObject(data, "status");
        record.RoleName = GetString(status, "name", record.RoleName);
        record.Uid = GetString(status, "uid", record.Uid);
        record.Level = GetInt32(status, "level");
        record.MainStage = GetString(status, "mainStageProgress");
        record.CharCount = GetInt32(status, "charCnt");
        record.SkinCount = GetInt32(status, "skinCnt");
        record.AvatarUrl = GetString(GetObject(status, "avatar"), "url");

        JsonElement activityInfoMap = GetObject(data, "activityInfoMap");
        JsonElement stageInfoMap = GetObject(data, "stageInfoMap");
        JsonElement campaignInfoMap = GetObject(data, "campaignInfoMap");
        JsonElement campaignZoneInfoMap = GetObject(data, "campaignZoneInfoMap");
        JsonElement towerInfoMap = GetObject(data, "towerInfoMap");
        JsonElement rogueInfoMap = GetObject(data, "rogueInfoMap");

        JsonElement campaign = GetObject(data, "campaign");
        if (campaign.ValueKind is JsonValueKind.Object && campaign.TryGetProperty("records", out JsonElement campaignRecords))
        {
            foreach (JsonElement item in campaignRecords.EnumerateArray())
            {
                string id = GetString(item, "campaignId");
                JsonElement info = GetObject(campaignInfoMap, id);
                string zoneId = GetString(info, "campaignZoneId");
                record.Campaigns.Add(new ArknightsCampaignRecord
                {
                    Id = id,
                    Name = GetString(info, "name", id),
                    ZoneName = GetString(GetObject(campaignZoneInfoMap, zoneId), "name"),
                    MaxKills = GetInt32(item, "maxKills"),
                    PicUrl = GetString(info, "picUrl"),
                });
            }
        }
        JsonElement tower = GetObject(data, "tower");
        if (tower.ValueKind is JsonValueKind.Object && tower.TryGetProperty("records", out JsonElement towerRecords))
        {
            foreach (JsonElement item in towerRecords.EnumerateArray())
            {
                string id = GetString(item, "towerId");
                JsonElement info = GetObject(towerInfoMap, id);
                record.Towers.Add(new ArknightsTowerRecord
                {
                    Id = id,
                    Name = GetString(info, "name", id),
                    SubName = GetString(info, "subName"),
                    Best = GetInt32(item, "best"),
                    PicUrl = GetString(info, "picUrl"),
                });
            }
        }
        JsonElement rogue = GetObject(data, "rogue");
        if (rogue.ValueKind is JsonValueKind.Object && rogue.TryGetProperty("records", out JsonElement rogueRecords))
        {
            foreach (JsonElement item in rogueRecords.EnumerateArray())
            {
                string id = GetString(item, "rogueId");
                JsonElement info = GetObject(rogueInfoMap, id);
                JsonElement medal = GetObject(item, "medal");
                record.Rogues.Add(new ArknightsRogueRecord
                {
                    Id = id,
                    Name = GetString(info, "name", id),
                    ClearTime = GetInt32(item, "clearTime"),
                    BpLevel = GetInt32(item, "bpLevel"),
                    MedalCurrent = GetInt32(medal, "current"),
                    MedalTotal = GetInt32(medal, "total"),
                    PicUrl = GetString(info, "picUrl"),
                });
            }
        }
        if (data.TryGetProperty("activity", out JsonElement activities) && activities.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement item in activities.EnumerateArray())
            {
                string actId = GetString(item, "actId");
                JsonElement info = GetObject(activityInfoMap, actId);
                int cleared = 0;
                int total = 0;
                if (item.TryGetProperty("zones", out JsonElement zones) && zones.ValueKind is JsonValueKind.Array)
                {
                    foreach (JsonElement zone in zones.EnumerateArray())
                    {
                        cleared += GetInt32(zone, "clearedStage");
                        total += GetInt32(zone, "totalStage");
                    }
                }
                record.Activities.Add(new ArknightsActivityRecord
                {
                    ActId = actId,
                    Name = GetString(info, "name", actId),
                    ClearedStages = cleared,
                    TotalStages = total,
                    StartTime = ParseUnixTime(GetString(info, "startTime")),
                    EndTime = ParseUnixTime(GetString(info, "endTime")),
                    PicUrl = GetString(info, "picUrl"),
                });
            }
        }
        if (data.TryGetProperty("bossRush", out JsonElement bossRush) && bossRush.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement item in bossRush.EnumerateArray())
            {
                string id = GetString(item, "id");
                JsonElement recordInfo = GetObject(item, "record");
                string stageId = GetString(recordInfo, "stageId");
                JsonElement stageInfo = GetObject(stageInfoMap, stageId);
                string stageName = GetString(stageInfo, "name", stageId);
                string code = GetString(stageInfo, "code");
                if (!string.IsNullOrWhiteSpace(code))
                {
                    stageName = $"{code} {stageName}";
                }
                bool played = recordInfo.ValueKind is JsonValueKind.Object
                              && recordInfo.TryGetProperty("played", out JsonElement playedElement)
                              && playedElement.ValueKind is JsonValueKind.True;
                record.BossRushes.Add(new ArknightsBossRushRecord
                {
                    Id = id,
                    Name = FormatBossRushName(id),
                    StageName = played ? stageName : "",
                    Difficulty = played ? GetString(recordInfo, "difficulty") : "",
                    Played = played,
                    PicUrl = GetString(item, "picUrl"),
                });
            }
        }
        if (data.TryGetProperty("sandbox", out JsonElement sandbox) && sandbox.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement item in sandbox.EnumerateArray())
            {
                int done = 0;
                int total = 0;
                if (item.TryGetProperty("subQuest", out JsonElement subQuests) && subQuests.ValueKind is JsonValueKind.Array)
                {
                    foreach (JsonElement quest in subQuests.EnumerateArray())
                    {
                        total++;
                        if (quest.TryGetProperty("done", out JsonElement doneElement) && doneElement.ValueKind is JsonValueKind.True)
                        {
                            done++;
                        }
                    }
                }
                record.Sandboxes.Add(new ArknightsSandboxRecord
                {
                    Name = GetString(item, "name", "生息演算"),
                    MaxDay = GetInt32(item, "maxDay"),
                    BaseLv = GetInt32(item, "baseLv"),
                    EnemyKill = GetInt32(item, "enemyKill"),
                    SubQuestDone = done,
                    SubQuestTotal = total,
                });
            }
        }
    }


    private static string FormatBossRushName(string id)
    {
        string digits = new(id.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? id : $"引航者试炼 #{digits.PadLeft(2, '0')}";
    }


    private static JsonElement GetObject(JsonElement element, string name)
    {
        return element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.Object
            ? value
            : default;
    }


    private async Task LoadGarrisonAsync(ArknightsRecord record, string uid, SklandCredential credential,
        CancellationToken cancellationToken)
    {
        using JsonDocument resource = await GetSignedAsync("https://zonai.skland.com/api/ms/game/arknights/garrison/resource", credential, cancellationToken);
        if (!resource.RootElement.TryGetProperty("data", out JsonElement resourceData)
            || !resourceData.TryGetProperty("resources", out JsonElement resources)
            || resources.ValueKind is not JsonValueKind.Array
            || !resources.EnumerateArray().Any())
        {
            return;
        }
        JsonElement first = resources[0];
        JsonElement version = first.TryGetProperty("versionInfo", out JsonElement versionElement) ? versionElement : default;
        string activityId = GetString(version, "activityId");
        if (string.IsNullOrWhiteSpace(activityId))
        {
            return;
        }
        ArknightsGarrisonRecord garrison = new()
        {
            SeasonName = GetString(version, "seasonName"),
            StartTime = ParseUnixTime(GetString(version, "startTime")),
            EndTime = ParseUnixTime(GetString(version, "endTime")),
        };
        using JsonDocument info = await GetSignedAsync(
            $"https://zonai.skland.com/api/ms/game/arknights/garrison/info?uid={uid}&activityId={Uri.EscapeDataString(activityId)}",
            credential, cancellationToken);
        if (info.RootElement.TryGetProperty("data", out JsonElement infoData)
            && infoData.TryGetProperty("history", out JsonElement history)
            && history.TryGetProperty("records", out JsonElement records)
            && records.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement item in records.EnumerateArray())
            {
                string id = GetString(item, "id");
                long timestamp = 0;
                int dash = id.LastIndexOf('-');
                if (dash >= 0)
                {
                    long.TryParse(id[(dash + 1)..], out timestamp);
                }
                garrison.Matches.Add(new ArknightsGarrisonMatch
                {
                    Timestamp = timestamp,
                    KillCount = GetInt32(item, "killCnt"),
                    Trophy = GetInt32(item, "trophyAfter"),
                    Completed = GetInt32(item, "completeState") == 1,
                    BandId = GetString(item, "bandId"),
                });
            }
        }
        record.Garrison = garrison;
    }


    private static DateTimeOffset ParseUnixTime(string value)
    {
        return long.TryParse(value, out long seconds) && seconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.MinValue;
    }


    private static string GetString(JsonElement element, string name, string defaultValue = "")
    {
        if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? defaultValue,
                JsonValueKind.Number => value.GetRawText(),
                _ => defaultValue,
            };
        }
        return defaultValue;
    }


    private static int GetInt32(JsonElement element, string name, int defaultValue = 0)
    {
        if (element.ValueKind is JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value))
        {
            if (value.ValueKind is JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }
            if (value.ValueKind is JsonValueKind.String && int.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }
        return defaultValue;
    }


    private sealed class SklandCredential
    {
        public string Cred { get; set; } = "";

        public string Token { get; set; } = "";

        public string UserId { get; set; } = "";
    }

}
