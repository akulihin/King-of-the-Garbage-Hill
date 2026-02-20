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

        var snapshot = CaptureSnapshot(game);
        var gameId = game.GameId;

        _ = Task.Run(async () =>
        {
            try
            {
                var prompt = BuildPrompt(snapshot);
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

        sb.AppendLine("Ты — комментатор игры 'King of the Garbage Hill' (Король Мусорной Горы).");
        sb.AppendLine("Это тактическая пошаговая игра на 6 игроков с уникальными персонажами.");
        sb.AppendLine();
        sb.AppendLine("ЗАДАНИЕ: напиши весёлую историю-пересказ этой партии (8-15 строк).");
        sb.AppendLine("ПРАВИЛА:");
        sb.AppendLine("- Описывай взаимодействия МЕЖДУ персонажами: кто кого бил, кто кому мстил, какие способности сработали друг против друга");
        sb.AppendLine("- Привязывай события к раундам из Fight History (\"в раунде #3...\", \"к раунду #7...\")");
        sb.AppendLine("- Шутки должны быть 'in character' — основаны на личности, фразах и способностях персонажа");
        sb.AppendLine("- НЕ описывай каждый раунд — выдели 3-5 самых ярких столкновений и поворотных моментов");
        sb.AppendLine("- Стиль: неформальный, с юмором, сленгом, цитатами персонажей из логов");
        sb.AppendLine("- Используй **жирный** для имён персонажей и ключевых моментов");
        sb.AppendLine("- Каждый момент — отдельная строка (абзац)");
        sb.AppendLine("- Напиши только историю, без заголовков и пояснений");
        sb.AppendLine();

        // ── Characters ──
        sb.AppendLine($"=== РЕЗУЛЬТАТЫ ({snapshot.RoundCount} раундов, режим: {snapshot.GameMode}) ===");
        sb.AppendLine();
        sb.AppendLine("ПЕРСОНАЖИ:");
        foreach (var p in snapshot.Players)
        {
            var botTag = p.IsBot ? " [БОТ]" : "";
            sb.AppendLine($"  #{p.Place} {p.CharacterName}{botTag} ({p.Score} очков)");

            if (!string.IsNullOrWhiteSpace(p.StoryAgent))
                sb.AppendLine($"    Характер: {p.StoryAgent}");

            if (p.Passives.Count > 0)
                sb.AppendLine($"    Способности: {string.Join(" | ", p.Passives)}");
        }

        // ── Fight History (global logs with round numbers, usernames already replaced) ──
        sb.AppendLine();
        sb.AppendLine("=== FIGHT HISTORY ===");
        sb.AppendLine("(⟶ означает победу: проигравший ⟶ победитель)");
        sb.AppendLine(Truncate(StripDiscordEmoji(snapshot.AllGlobalLogs), 3000));

        // ── Per-character personal logs with round numbers ──
        sb.AppendLine();
        sb.AppendLine("=== PERSONAL LOGS (по раундам) ===");
        foreach (var p in snapshot.Players)
        {
            if (string.IsNullOrWhiteSpace(p.PersonalLogs)) continue;
            sb.AppendLine();
            sb.AppendLine($"--- {p.CharacterName} (#{p.Place}) ---");
            // Split by ||| into per-round sections
            var rounds = p.PersonalLogs.Split("|||")
                .Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .ToList();
            for (var i = 0; i < rounds.Count; i++)
            {
                sb.AppendLine($"  Раунд #{i + 1}: {Truncate(StripDiscordEmoji(rounds[i]), 200)}");
            }
        }

        return sb.ToString();
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
