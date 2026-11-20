using Microsoft.Extensions.Logging;
using Starward.Features.Gacha.Endfield;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord.DailyNote;

public sealed class ArknightsAccount
{
    public string Uid { get; set; } = "";

    public string NickName { get; set; } = "";

    public string ServerName { get; set; } = "";

    [JsonIgnore]
    public string LoginToken { get; set; } = "";
}


public sealed class ArknightsDailyNote
{
    public string RoleName { get; set; } = "";

    public string Uid { get; set; } = "";

    public int Level { get; set; }

    public int CurrentAp { get; set; }

    public int MaxAp { get; set; }

    public TimeSpan RecoverTime { get; set; }

    public int RecoverHours => (int)RecoverTime.TotalHours;

    public int RecoverMinutes => RecoverTime.Minutes;

    public bool IsApFull => MaxAp > 0 && CurrentAp >= MaxAp;

    public int DailyCurrent { get; set; }

    public int DailyMax { get; set; }

    public int WeeklyCurrent { get; set; }

    public int WeeklyMax { get; set; }

    public int RecruitCurrent { get; set; }

    public int RecruitMax { get; set; }

    public int CampaignCurrent { get; set; }

    public int CampaignMax { get; set; }

    public string MainStage { get; set; } = "";

    public string TrainingCharName { get; set; } = "";

    public TimeSpan TrainingRemain { get; set; }

    public bool HasTraining => TrainingRemain > TimeSpan.Zero;
}


public sealed class ArknightsDailyNoteService
{
    private const string OAuthGrantApi = "https://as.hypergryph.com/user/oauth2/v2/grant";

    private const string BindingListApi = "https://binding-api-account-prod.hypergryph.com/account/binding/v1/binding_list";

    private const string KanbanApi = "https://launcher.hypergryph.com/api/game/get_kanban";

    private const string GameAppCode = "GzD1CpaWgmSq1wew";

    private const string OAuthAppCode = "be36d44aa36bfb5b";

    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.6422.112 Safari/537.36";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<ArknightsDailyNoteService> _logger;

    private readonly HttpClient _httpClient;


    public ArknightsDailyNoteService(ILogger<ArknightsDailyNoteService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }


    private static string AccountFilePath
    {
        get
        {
            string folder = AppConfig.UserDataFolder ?? AppContext.BaseDirectory;
            return Path.Combine(folder, "ArknightsAccount.json");
        }
    }


    public ArknightsAccount? GetAccount()
    {
        try
        {
            string path = AccountFilePath;
            if (!File.Exists(path))
            {
                return null;
            }
            StoredArknightsAccount? stored = JsonSerializer.Deserialize<StoredArknightsAccount>(File.ReadAllText(path));
            if (stored is null || string.IsNullOrWhiteSpace(stored.LoginToken) || string.IsNullOrWhiteSpace(stored.Uid))
            {
                return null;
            }
            string token = EndfieldTokenProtector.Unprotect(Convert.FromBase64String(stored.LoginToken));
            return new ArknightsAccount
            {
                Uid = stored.Uid,
                NickName = stored.NickName,
                ServerName = stored.ServerName,
                LoginToken = token,
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read saved Arknights account failed.");
            return null;
        }
    }


    public async Task<ArknightsAccount> AddAccountAsync(string loginToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginToken);
        string oauthToken = await GetOAuthTokenAsync(loginToken, cancellationToken);
        string url = $"{BindingListApi}?token={Uri.EscapeDataString(oauthToken)}&appCode=arknights";
        using HttpRequestMessage request = CreateBrowserRequest(HttpMethod.Get, url);
        using JsonDocument document = await SendJsonAsync(request, "明日方舟角色查询", cancellationToken);
        ArknightsAccount? account = null;
        if (document.RootElement.TryGetProperty("data", out JsonElement data)
            && data.TryGetProperty("list", out JsonElement list)
            && list.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement app in list.EnumerateArray())
            {
                if (!string.Equals(GetString(app, "appCode"), "arknights", StringComparison.OrdinalIgnoreCase)
                    || !app.TryGetProperty("bindingList", out JsonElement bindings)
                    || bindings.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }
                foreach (JsonElement binding in bindings.EnumerateArray())
                {
                    if (binding.TryGetProperty("isDeleted", out JsonElement deleted) && deleted.ValueKind is JsonValueKind.True)
                    {
                        continue;
                    }
                    string uid = GetString(binding, "uid");
                    if (string.IsNullOrWhiteSpace(uid))
                    {
                        continue;
                    }
                    account = new ArknightsAccount
                    {
                        Uid = uid,
                        NickName = GetString(binding, "nickName"),
                        ServerName = GetString(binding, "channelName", "官方"),
                    };
                    break;
                }
                if (account is not null)
                {
                    break;
                }
            }
        }
        if (account is null)
        {
            throw new InvalidOperationException("该鹰角账号未绑定明日方舟角色。");
        }
        StoredArknightsAccount stored = new()
        {
            Uid = account.Uid,
            NickName = account.NickName,
            ServerName = account.ServerName,
            LoginToken = Convert.ToBase64String(EndfieldTokenProtector.Protect(loginToken)),
        };
        string path = AccountFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(stored, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
        return account;
    }


    public async Task<ArknightsDailyNote> GetDailyNoteAsync(ArknightsAccount account, bool forceUpdate = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        string url = $"{KanbanApi}?game_appcode={GameAppCode}" +
                     $"&token={Uri.EscapeDataString(account.LoginToken)}" +
                     $"&uid={Uri.EscapeDataString(account.Uid)}" +
                     $"&role_id={Uri.EscapeDataString(account.Uid)}" +
                     $"&language=zh-cn";
        using HttpRequestMessage request = CreateBrowserRequest(HttpMethod.Get, url);
        using JsonDocument document = await SendJsonAsync(request, "明日方舟体力查询", cancellationToken);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("ark_kanban", out JsonElement kanban) || kanban.ValueKind is not JsonValueKind.Object)
        {
            string bizCode = root.TryGetProperty("biz_code", out JsonElement code) ? code.ToString() : "";
            throw new InvalidOperationException($"官方接口未返回明日方舟看板数据（{bizCode}），请重新登录鹰角账号。");
        }
        _logger.LogInformation("Arknights kanban: {Json}", kanban.GetRawText());
        JsonElement ap = kanban.TryGetProperty("ap", out JsonElement apElement) ? apElement : default;
        JsonElement taskInfo = kanban.TryGetProperty("task_info", out JsonElement taskElement) ? taskElement : default;
        JsonElement daily = taskInfo.ValueKind is JsonValueKind.Object && taskInfo.TryGetProperty("daily", out JsonElement dailyElement) ? dailyElement : default;
        JsonElement weekly = taskInfo.ValueKind is JsonValueKind.Object && taskInfo.TryGetProperty("weekly", out JsonElement weeklyElement) ? weeklyElement : default;
        JsonElement extend = kanban.TryGetProperty("extend_game_play_info", out JsonElement extendElement) ? extendElement : default;
        JsonElement recruit = extend.ValueKind is JsonValueKind.Object && extend.TryGetProperty("recruit", out JsonElement recruitElement) ? recruitElement : default;
        JsonElement campaign = extend.ValueKind is JsonValueKind.Object && extend.TryGetProperty("campaign_info", out JsonElement campaignElement) ? campaignElement : default;
        string recoverText = ap.ValueKind is JsonValueKind.Object ? GetString(ap, "complete_recovery_time") : "";
        long recoverTimestamp = long.TryParse(recoverText, out long parsed) ? parsed : 0;
        TimeSpan recoverTime = TimeSpan.Zero;
        if (recoverTimestamp > 0)
        {
            recoverTime = DateTimeOffset.FromUnixTimeSeconds(recoverTimestamp) - DateTimeOffset.Now;
            if (recoverTime < TimeSpan.Zero)
            {
                recoverTime = TimeSpan.Zero;
            }
        }
        string trainingName = "";
        TimeSpan trainingRemain = TimeSpan.Zero;
        if (kanban.TryGetProperty("building", out JsonElement building)
            && building.ValueKind is JsonValueKind.Object
            && building.TryGetProperty("training", out JsonElement training)
            && training.ValueKind is JsonValueKind.Object)
        {
            trainingName = GetString(training, "char_name");
            string remain = GetString(training, "remain_secs");
            if (long.TryParse(remain, out long seconds) && seconds > 0)
            {
                trainingRemain = TimeSpan.FromSeconds(seconds);
            }
        }
        return new ArknightsDailyNote
        {
            RoleName = GetString(kanban, "nickname", account.NickName),
            Uid = GetString(kanban, "uid", account.Uid),
            Level = GetInt32(kanban, "level"),
            CurrentAp = GetInt32(ap, "current"),
            MaxAp = GetInt32(ap, "max"),
            RecoverTime = recoverTime,
            DailyCurrent = GetInt32(daily, "current"),
            DailyMax = GetInt32(daily, "max"),
            WeeklyCurrent = GetInt32(weekly, "current"),
            WeeklyMax = GetInt32(weekly, "max"),
            RecruitCurrent = GetInt32(recruit, "current"),
            RecruitMax = GetInt32(recruit, "max"),
            CampaignCurrent = GetInt32(campaign, "current"),
            CampaignMax = GetInt32(campaign, "max"),
            MainStage = extend.ValueKind is JsonValueKind.Object ? GetString(extend, "main_stage_progress") : "",
            TrainingCharName = trainingName,
            TrainingRemain = trainingRemain,
        };
    }


    private async Task<string> GetOAuthTokenAsync(string loginToken, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateBrowserRequest(HttpMethod.Post, OAuthGrantApi);
        request.Content = JsonContent.Create(new { token = loginToken, appCode = OAuthAppCode, type = 1 });
        using JsonDocument document = await SendJsonAsync(request, "鹰角账号授权", cancellationToken);
        JsonElement root = document.RootElement;
        if (GetInt32(root, "status") != 0 || !root.TryGetProperty("data", out JsonElement data))
        {
            throw new InvalidOperationException("鹰角账号登录状态已失效，请重新登录。");
        }
        string token = GetString(data, "token");
        return !string.IsNullOrWhiteSpace(token)
            ? token
            : throw new InvalidOperationException("鹰角账号授权未返回有效凭证，请重新登录。");
    }


    private async Task<JsonDocument> SendJsonAsync(HttpRequestMessage request, string operation,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{operation}失败（HTTP {(int)response.StatusCode}）。");
        }
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"{operation}返回了无效数据。");
        }
    }


    private static HttpRequestMessage CreateBrowserRequest(HttpMethod method, string url)
    {
        HttpRequestMessage request = new(method, url);
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        return request;
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


    private sealed class StoredArknightsAccount
    {
        public string Uid { get; set; } = "";

        public string NickName { get; set; } = "";

        public string ServerName { get; set; } = "";

        public string LoginToken { get; set; } = "";
    }

}
