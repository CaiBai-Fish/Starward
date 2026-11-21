using Microsoft.Extensions.Logging;
using Starward.Features.Gacha.Endfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        List<ArknightsAccount> accounts = GetAccounts(out string currentUid);
        return accounts.FirstOrDefault(x => x.Uid == currentUid) ?? accounts.FirstOrDefault();
    }


    public List<ArknightsAccount> GetAccounts()
    {
        return GetAccounts(out _);
    }


    public List<ArknightsAccount> GetAccounts(out string currentUid)
    {
        currentUid = "";
        List<ArknightsAccount> accounts = [];
        try
        {
            StoredArknightsAccounts stored = ReadStoredAccounts();
            currentUid = stored.CurrentUid;
            foreach (StoredArknightsAccount item in stored.Accounts)
            {
                if (string.IsNullOrWhiteSpace(item.Uid) || string.IsNullOrWhiteSpace(item.LoginToken))
                {
                    continue;
                }
                try
                {
                    string token = EndfieldTokenProtector.Unprotect(Convert.FromBase64String(item.LoginToken));
                    accounts.Add(new ArknightsAccount
                    {
                        Uid = item.Uid,
                        NickName = item.NickName,
                        ServerName = item.ServerName,
                        LoginToken = token,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Decrypt saved Arknights account {Uid} failed.", item.Uid);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Read saved Arknights accounts failed.");
        }
        return accounts;
    }


    public void SetCurrentAccount(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return;
        }
        StoredArknightsAccounts stored = ReadStoredAccounts();
        if (stored.Accounts.Any(x => x.Uid == uid))
        {
            stored.CurrentUid = uid;
            WriteStoredAccounts(stored);
        }
    }


    public void RemoveAccount(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return;
        }
        StoredArknightsAccounts stored = ReadStoredAccounts();
        int index = stored.Accounts.FindIndex(x => x.Uid == uid);
        if (index >= 0)
        {
            stored.Accounts.RemoveAt(index);
            if (stored.CurrentUid == uid)
            {
                stored.CurrentUid = stored.Accounts.FirstOrDefault()?.Uid ?? "";
            }
            WriteStoredAccounts(stored);
        }
    }


    private StoredArknightsAccounts ReadStoredAccounts()
    {
        string path = AccountFilePath;
        if (!File.Exists(path))
        {
            return new StoredArknightsAccounts();
        }
        string json = File.ReadAllText(path);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        StoredArknightsAccounts stored = new();
        if (root.ValueKind is JsonValueKind.Object
            && root.TryGetProperty("Accounts", out JsonElement accounts)
            && accounts.ValueKind is JsonValueKind.Array)
        {
            stored.CurrentUid = root.TryGetProperty("CurrentUid", out JsonElement current) ? current.GetString() ?? "" : "";
            foreach (JsonElement element in accounts.EnumerateArray())
            {
                StoredArknightsAccount? account = ReadStoredAccount(element);
                if (account is not null)
                {
                    stored.Accounts.Add(account);
                }
            }
        }
        else
        {
            StoredArknightsAccount? account = ReadStoredAccount(root);
            if (account is not null)
            {
                stored.Accounts.Add(account);
                stored.CurrentUid = account.Uid;
            }
        }
        return stored;
    }


    private static StoredArknightsAccount? ReadStoredAccount(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            return null;
        }
        string uid = element.TryGetProperty("Uid", out JsonElement uidElement) ? uidElement.GetString() ?? "" : "";
        string token = element.TryGetProperty("LoginToken", out JsonElement tokenElement) ? tokenElement.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }
        return new StoredArknightsAccount
        {
            Uid = uid,
            NickName = element.TryGetProperty("NickName", out JsonElement nickname) ? nickname.GetString() ?? "" : "",
            ServerName = element.TryGetProperty("ServerName", out JsonElement server) ? server.GetString() ?? "" : "",
            LoginToken = token,
        };
    }


    private void WriteStoredAccounts(StoredArknightsAccounts accounts)
    {
        string path = AccountFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(accounts, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
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
        StoredArknightsAccounts stored = ReadStoredAccounts();
        StoredArknightsAccount? existing = stored.Accounts.FirstOrDefault(x => x.Uid == account.Uid);
        string protectedToken = Convert.ToBase64String(EndfieldTokenProtector.Protect(loginToken));
        if (existing is null)
        {
            stored.Accounts.Add(new StoredArknightsAccount
            {
                Uid = account.Uid,
                NickName = account.NickName,
                ServerName = account.ServerName,
                LoginToken = protectedToken,
            });
        }
        else
        {
            existing.NickName = account.NickName;
            existing.ServerName = account.ServerName;
            existing.LoginToken = protectedToken;
        }
        stored.CurrentUid = account.Uid;
        WriteStoredAccounts(stored);
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


    private sealed class StoredArknightsAccounts
    {
        public string CurrentUid { get; set; } = "";

        public List<StoredArknightsAccount> Accounts { get; set; } = [];
    }

}
