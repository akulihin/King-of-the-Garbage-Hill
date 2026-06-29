using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.Services;

public class DiscordWidgetService : IServiceSingleton
{
    private const string ClientId = "901706293977432124";
    private readonly HttpClient _httpClient;
    private readonly UserAccounts _accounts;
    private readonly CharactersPull _charactersPull;
    private readonly string _botToken;
    private readonly LoginFromConsole _logs;

    public DiscordWidgetService(UserAccounts accounts, CharactersPull charactersPull, Config config, LoginFromConsole logs, HttpClient httpClient)
    {
        _accounts = accounts;
        _charactersPull = charactersPull;
        _botToken = config.Token ?? "";
        _logs = logs;
        _httpClient = httpClient;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<bool> SyncAsync(ulong discordUserId)
    {
        var account = _accounts.GetAccount(discordUserId);
        if (account == null) return false;
        if (!account.WidgetAuthorized) return false;

        // Snapshot to avoid "collection modified during enumeration" — CharacterStatistics
        // is mutated by game-end stamping on another thread.
        var statsSnapshot = account.CharacterStatistics.ToList();

        var recentChars = statsSnapshot
            .OrderByDescending(x => x.LastPlayedAt)
            .Take(4)
            .ToList();

        if (recentChars.Count == 0) return false;

        var allCharacters = _charactersPull.GetRollableCharacters();
        var dynamicData = new List<object>();

        for (int i = 0; i < recentChars.Count; i++)
        {
            var stat = recentChars[i];
            var charDef = allCharacters.FirstOrDefault(c => c.Name == stat.CharacterName);
            if (charDef == null) continue;

            dynamicData.Add(new { type = 3, name = $"character_icon_url_{i + 1}", value = new { url = charDef.Avatar } });
        }

        var firstCharDef = allCharacters.FirstOrDefault(c => c.Name == recentChars[0].CharacterName);
        if (firstCharDef != null)
        {
            dynamicData.Add(new { type = 3, name = "character_favorite", value = new { url = firstCharDef.Avatar } });
        }
        dynamicData.Add(new { type = 2, name = "character_favorite_number", value = account.WidgetFavoriteNumber });

        for (int i = 0; i < recentChars.Count; i++)
        {
            var stat = recentChars[i];
            var winPct = stat.Plays > 0 ? (int)(stat.Wins * 100 / stat.Plays) : 0;
            dynamicData.Add(new { type = 1, name = $"character_wr_{i + 1}", value = $"{winPct}% ({stat.Plays} games)" });
        }

        dynamicData.Add(new { type = 1, name = "stat_text_left", value = account.WidgetStatTextLeft });
        dynamicData.Add(new { type = 1, name = "stat_text_right", value = account.WidgetStatTextRight });

        var payload = new { username = account.DiscordUserName, data = new { dynamic = dynamicData } };
        var json = JsonSerializer.Serialize(payload);

        var url = $"https://discord.com/api/v9/applications/{ClientId}/users/{discordUserId}/identities/{discordUserId}/profile";
        using var req = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", _botToken);

        try
        {
            var response = await _httpClient.SendAsync(req);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                account.WidgetAuthorized = false;
                _logs.Warning($"DiscordWidgetService: 403 for user {discordUserId}, clearing WidgetAuthorized");
                return false;
            }
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logs.Warning($"DiscordWidgetService: PATCH failed ({(int)response.StatusCode}) for {discordUserId}: {body}");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logs.Critical($"DiscordWidgetService.SyncAsync error for {discordUserId}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TryVerifyAndAuthorizeAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return false;

        ulong discordUserId;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.SendAsync(req);
            if (!response.IsSuccessStatusCode) return false;
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var idStr = doc.RootElement.GetProperty("id").GetString();
            if (!ulong.TryParse(idStr, out discordUserId)) return false;
        }
        catch (Exception ex)
        {
            _logs.Critical($"DiscordWidgetService.TryVerifyAndAuthorizeAsync /users/@me error: {ex.Message}");
            return false;
        }

        var account = _accounts.GetAccount(discordUserId);
        if (account == null) return false;

        account.WidgetAuthorized = true;
        return await SyncAsync(discordUserId);
    }
}
