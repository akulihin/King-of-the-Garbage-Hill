using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using Microsoft.AspNetCore.SignalR;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Generates a fun narrative summary of a finished game using Claude Haiku API.
/// Fire-and-forget: if no API key is configured or the API call fails, nothing happens.
/// </summary>
public class GameStoryService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 1024;

    public GameStoryService(IHubContext<GameHub> hubContext, HttpClient httpClient, Config config)
    {
        _hubContext = hubContext;
        _httpClient = httpClient;
        _apiKey = config.AnthropicApiKey ?? "";
    }

    /// <summary>
    /// Entry point. Captures a snapshot of the finished game and fires a background task
    /// to generate the story via LLM. Returns immediately.
    /// </summary>
    public void GenerateStoryAsync(GameClass game)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return;

        // Don't waste API calls on bot-only games — no humans to read the story
        if (game.PlayersList.All(p => p.IsBot()))
            return;

        var snapshot = CaptureSnapshot(game);
        var gameId = game.GameId;

        _ = Task.Run(async () =>
        {
            try
            {
                var prompt = BuildPrompt(snapshot);
                Console.WriteLine($"[GameStory] Prompt for game {gameId}:\n{prompt}");
                var story = await CallClaudeApi(prompt);

                if (!string.IsNullOrWhiteSpace(story))
                {
                    // Convert markdown-style formatting to HTML for web display
                    var html = FormatStoryHtml(story);

                    await _hubContext.Clients.Group($"game-{gameId}")
                        .SendAsync("GameEvent", new { eventType = "GameStory", data = new { story = html } });
                    Console.WriteLine($"[GameStory] Story delivered for game {gameId} ({html.Length} chars)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameStory] Error generating story for game {gameId}: {ex.Message}");
            }
        });
    }

    // ── Snapshot ──────────────────────────────────────────────────────

    private GameStorySnapshot CaptureSnapshot(GameClass game)
    {
        // Build username → character name mapping for log replacement
        var nameMap = game.PlayersList
            .Where(p => !string.IsNullOrWhiteSpace(p.DiscordUsername) && !string.IsNullOrWhiteSpace(p.GameCharacter.Name))
            .ToDictionary(p => p.DiscordUsername, p => p.GameCharacter.Name);

        var players = game.PlayersList
            .OrderBy(p => p.Status.GetPlaceAtLeaderBoard())
            .Select(p => new PlayerSnapshot
            {
                CharacterName = p.GameCharacter.Name,
                Description = p.GameCharacter.Description ?? "",
                StoryAgent = p.GameCharacter.StoryAgent ?? "",
                Score = p.Status.GetScore(),
                Place = p.Status.GetPlaceAtLeaderBoard(),
                IsBot = p.PlayerType == 404,
                Passives = p.GameCharacter.Passive
                    .Where(pas => pas.Visible)
                    .Select(pas => $"{pas.PassiveName}: {StripDiscordEmoji(pas.PassiveDescription)}")
                    .ToList(),
                PersonalLogs = ReplaceUsernames(p.Status.InGamePersonalLogsAll ?? "", nameMap)
            })
            .ToList();

        return new GameStorySnapshot
        {
            RoundCount = game.RoundNo,
            GameMode = game.GameMode,
            AllGlobalLogs = ReplaceUsernames(game.GetAllGlobalLogs() ?? "", nameMap),
            Players = players
        };
    }

    /// <summary>
    /// Replaces all Discord usernames in text with character names so the LLM
    /// only sees character identities, never player nicknames.
    /// </summary>
    private static string ReplaceUsernames(string text, Dictionary<string, string> nameMap)
    {
        if (string.IsNullOrEmpty(text) || nameMap.Count == 0) return text;

        // Replace longer names first to avoid partial matches
        foreach (var pair in nameMap.OrderByDescending(p => p.Key.Length))
        {
            text = text.Replace(pair.Key, pair.Value);
        }

        return text;
    }

    // ── Prompt ────────────────────────────────────────────────────────

    private static string BuildPrompt(GameStorySnapshot snapshot)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<game-commentary>");

        // ── Instructions ──
        sb.AppendLine("<instructions>");
        sb.AppendLine("Ты — комментатор игры 'King of the Garbage Hill' (Король Мусорной Горы).");
        sb.AppendLine("Это тактическая пошаговая игра на 6 игроков с уникальными персонажами.");
        sb.AppendLine();
        sb.AppendLine("ЗАДАНИЕ: напиши весёлую историю-пересказ этой партии (8-15 строк).");
        sb.AppendLine("ПРАВИЛА:");
        sb.AppendLine("- Описывай взаимодействия МЕЖДУ персонажами: кто кого бил, кто кому мстил, какие способности сработали друг против друга");
        sb.AppendLine("- Привязывай события к раундам (\"в раунде #3...\", \"к раунду #7...\")");
        sb.AppendLine("- Шутки должны быть 'in character' — основаны на личности, фразах и способностях персонажа");
        sb.AppendLine("- НЕ описывай каждый раунд — выдели 3-5 самых ярких столкновений и поворотных моментов");
        sb.AppendLine("- Стиль: неформальный, с юмором, сленгом, цитатами персонажей из логов");
        sb.AppendLine("- Используй **жирный** для имён персонажей и ключевых моментов");
        sb.AppendLine("- Каждый момент — отдельная строка (абзац)");
        sb.AppendLine("- Напиши только историю, без заголовков и пояснений");
        sb.AppendLine("- Не выдумывай ничего своего, используй только информацию из логов.");
        sb.AppendLine("- Твоя цель - пересказать историю каждого раунда.");
        sb.AppendLine("</instructions>");

        // ── Characters / Results ──
        sb.AppendLine($"<results rounds=\"{snapshot.RoundCount}\" mode=\"{snapshot.GameMode}\">");
        foreach (var p in snapshot.Players)
        {
            var botAttr = p.IsBot ? " bot=\"true\"" : "";
            sb.AppendLine($"  <character place=\"{p.Place}\" name=\"{p.CharacterName}\" score=\"{p.Score}\"{botAttr}>");

            if (!string.IsNullOrWhiteSpace(p.StoryAgent))
                sb.AppendLine($"    <personality>{p.StoryAgent}</personality>");

            if (p.Passives.Count > 0)
            {
                sb.AppendLine("    <abilities>");
                foreach (var pas in p.Passives)
                    sb.AppendLine($"      <ability>{pas}</ability>");
                sb.AppendLine("    </abilities>");
            }

            sb.AppendLine("  </character>");
        }
        sb.AppendLine("</results>");

        // ── Per-round structured data ──
        var globalRounds = SplitGlobalLogsByRound(snapshot.AllGlobalLogs);
        var personalRoundLogs = snapshot.Players.Select(p => new
        {
            p.CharacterName,
            Rounds = (p.PersonalLogs ?? "").Split("|||")
                .Select(r => r.Trim())
                .ToList()
        }).ToList();

        var maxRounds = snapshot.RoundCount;
        if (maxRounds == 0)
            maxRounds = Math.Max(
                globalRounds.Count,
                personalRoundLogs.Select(x => x.Rounds.Count(r => r.Length > 0)).DefaultIfEmpty(0).Max()
            );

        sb.AppendLine("<rounds>");
        for (var i = 0; i < maxRounds; i++)
        {
            sb.AppendLine($"  <round number=\"{i + 1}\">");

            if (i < globalRounds.Count && !string.IsNullOrWhiteSpace(globalRounds[i]))
            {
                sb.AppendLine("    <fight-history>");
                sb.AppendLine($"      {Truncate(StripDiscordEmoji(globalRounds[i].Trim()), 500)}");
                sb.AppendLine("    </fight-history>");
            }

            var hasAnyLogs = personalRoundLogs.Any(p =>
                i < p.Rounds.Count && !string.IsNullOrWhiteSpace(p.Rounds[i]));

            if (hasAnyLogs)
            {
                sb.AppendLine("    <personal-logs>");
                foreach (var p in personalRoundLogs)
                {
                    if (i < p.Rounds.Count && !string.IsNullOrWhiteSpace(p.Rounds[i]))
                        sb.AppendLine(
                            $"      <character name=\"{p.CharacterName}\">{Truncate(StripDiscordEmoji(p.Rounds[i]), 300)}</character>");
                }
                sb.AppendLine("    </personal-logs>");
            }

            sb.AppendLine("  </round>");
        }
        sb.AppendLine("</rounds>");

        sb.AppendLine("</game-commentary>");

        return sb.ToString();
    }

    /// <summary>
    /// Splits AllGameGlobalLogs by round markers (__**Раунд #N**__:).
    /// Returns a list where index 0 = round 1 content, index 1 = round 2, etc.
    /// </summary>
    private static List<string> SplitGlobalLogsByRound(string allLogs)
    {
        if (string.IsNullOrWhiteSpace(allLogs))
            return new List<string>();

        var parts = Regex.Split(allLogs, @"__\*\*Раунд #\d+\*\*__:");

        // First element is pre-round content (usually empty), skip it
        return parts
            .Skip(1)
            .Select(p => p.Trim())
            .ToList();
    }

    // ── Formatting ───────────────────────────────────────────────────

    /// <summary>
    /// Converts LLM markdown output to safe HTML for web display.
    /// Handles **bold**, *italic*, and preserves line breaks.
    /// </summary>
    private static string FormatStoryHtml(string text)
    {
        // Sanitize HTML entities first
        text = text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");

        // Convert markdown bold **text** → <strong>text</strong>
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        // Convert markdown italic *text* → <em>text</em>
        text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");

        // Strip any markdown headers (# Title)
        text = Regex.Replace(text, @"^#{1,3}\s+.*$", "", RegexOptions.Multiline);

        // Trim empty lines at start/end
        text = text.Trim();

        return text;
    }

    private static string StripDiscordEmoji(string text)
    {
        return Regex.Replace(text, @"<:\w+:\d+>", "");
    }

    private static string Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "\n[...обрезано...]";
    }

    // ── API Call ──────────────────────────────────────────────────────

    private async Task<string> CallClaudeApi(string prompt)
    {
        var requestBody = new
        {
            model = Model,
            max_tokens = MaxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[GameStory] API error {response.StatusCode}: {responseBody}");
            return null;
        }

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return content;
    }

    // ── Data Classes ─────────────────────────────────────────────────

    private class GameStorySnapshot
    {
        public int RoundCount { get; set; }
        public string GameMode { get; set; }
        public string AllGlobalLogs { get; set; }
        public List<PlayerSnapshot> Players { get; set; }
    }

    private class PlayerSnapshot
    {
        public string CharacterName { get; set; }
        public string Description { get; set; }
        public string StoryAgent { get; set; }
        public decimal Score { get; set; }
        public int Place { get; set; }
        public bool IsBot { get; set; }
        public List<string> Passives { get; set; }
        public string PersonalLogs { get; set; }
    }
}
