using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;
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
    private readonly ConcurrentDictionary<ulong, string> _stories = new();

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 1800;
    private const int MaxStoredStories = 50;

    /// <summary>Callback invoked when a story is generated, for backfilling into replay files.</summary>
    public Action<ulong, string> OnStoryGenerated { get; set; }

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
                    if (!BilingualGeneratedTextParser.TryParse(story, out var generated))
                    {
                        Console.WriteLine($"[GameStory] Invalid bilingual output for game {gameId}; retrying once.");
                        story = await CallClaudeApi(prompt +
                            "\n<format-reminder>Return exactly one non-empty <ru>...</ru> block and one non-empty <en>...</en> block. No text outside them.</format-reminder>");
                    }

                    if (!BilingualGeneratedTextParser.TryParse(story, out generated))
                    {
                        Console.WriteLine($"[GameStory] Rejected non-bilingual output for game {gameId}.");
                        return;
                    }

                    // One shared artifact carries both variants; CSS shows the viewer's locale.
                    var html = $"<div class=\"story-locale story-ru\">{FormatStoryHtml(generated.Russian)}</div>" +
                               $"<div class=\"story-locale story-en\">{FormatStoryHtml(generated.English)}</div>";

                    // Store for later retrieval (e.g. on reconnect/rejoin)
                    StoreStory(gameId, html);

                    // Backfill story into replay file
                    OnStoryGenerated?.Invoke(gameId, html);

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

    // ── Story Storage ─────────────────────────────────────────────────

    public string GetStory(ulong gameId)
    {
        return _stories.TryGetValue(gameId, out var story) ? story : null;
    }

    private void StoreStory(ulong gameId, string html)
    {
        _stories[gameId] = html;

        // Trim oldest entries if we exceed the cap
        if (_stories.Count > MaxStoredStories)
        {
            var keysToRemove = _stories.Keys
                .OrderBy(k => k)
                .Take(_stories.Count - MaxStoredStories)
                .ToList();
            foreach (var key in keysToRemove)
                _stories.TryRemove(key, out _);
        }
    }

    // ── Snapshot ──────────────────────────────────────────────────────

    private GameStorySnapshot CaptureSnapshot(GameClass game)
    {
        // Build username → character name mapping for log replacement
        var nameMap = game.PlayersList
            .Where(p => !string.IsNullOrWhiteSpace(p.DiscordUsername) && !string.IsNullOrWhiteSpace(p.GameCharacter.Name))
            .ToDictionary(p => p.DiscordUsername, p => p.GameCharacter.Name);

        var characterByPlayerId = game.PlayersList.ToDictionary(
            p => p.GetPlayerId(),
            p => p.GameCharacter.Name);
        var personalLogsByPlayerId = game.PlayersList.ToDictionary(
            p => p.GetPlayerId(),
            p => (p.Status.InGamePersonalLogsAll ?? "")
                .Split("|||")
                .Select(log => CleanPromptText(ReplaceUsernames(log, nameMap)))
                .ToList());

        var players = game.PlayersList
            .OrderBy(p => p.Status.GetPlaceAtLeaderBoard())
            .Select(p => new PlayerSnapshot
            {
                CharacterName = p.GameCharacter.Name,
                StoryAgent = p.GameCharacter.StoryAgent ?? "",
                Score = p.Status.GetScore(),
                Place = p.Status.GetPlaceAtLeaderBoard(),
                IsBot = p.PlayerType == 404,
                Passives = p.GameCharacter.Passive
                    .Where(pas => pas.Visible)
                    .Select(pas => CleanPromptText($"{pas.PassiveName}: {pas.PassiveDescription}"))
                    .ToList()
            })
            .ToList();

        // Replay v2 is the authoritative playable-round boundary. It contains only rounds that
        // actually reached fight calculation, including Kratos extensions beyond round 10.
        var rounds = game.ReplayRounds
            .OrderBy(round => round.RoundNo)
            .Select(round => new RoundSnapshot
            {
                RoundNo = round.RoundNo,
                GlobalLogs = CleanPromptText(ReplaceUsernames(round.GlobalLogs ?? "", nameMap)),
                Fights = round.FightLog
                    .Select(fight => CaptureFight(fight, nameMap))
                    .ToList(),
                CharacterLogs = game.PlayersList
                    .Select(player => new CharacterLogSnapshot
                    {
                        CharacterName = player.GameCharacter.Name,
                        Text = personalLogsByPlayerId[player.GetPlayerId()].ElementAtOrDefault(round.RoundNo - 1) ?? ""
                    })
                    .Where(log => !string.IsNullOrWhiteSpace(log.Text))
                    .ToList()
            })
            .ToList();

        // Defensive fallback for an incomplete/legacy in-memory game. Unlike the old story path,
        // the terminal boundary itself is never advertised as a combat round.
        if (rounds.Count == 0)
        {
            var globalRounds = SplitGlobalLogsByRound(game.GetAllGlobalLogs() ?? "");
            var playableRoundCount = Math.Max(0, game.RoundNo - 1);
            for (var roundNo = 1; roundNo <= playableRoundCount; roundNo++)
            {
                rounds.Add(new RoundSnapshot
                {
                    RoundNo = roundNo,
                    GlobalLogs = roundNo <= globalRounds.Count
                        ? CleanPromptText(ReplaceUsernames(globalRounds[roundNo - 1], nameMap))
                        : "",
                    CharacterLogs = game.PlayersList
                        .Select(player => new CharacterLogSnapshot
                        {
                            CharacterName = player.GameCharacter.Name,
                            Text = personalLogsByPlayerId[player.GetPlayerId()].ElementAtOrDefault(roundNo - 1) ?? ""
                        })
                        .Where(log => !string.IsNullOrWhiteSpace(log.Text))
                        .ToList()
                });
            }
        }

        // Replay already isolates HandleLastRound additions from the hidden next-round setup buffer;
        // preserve that distinction as an epilogue instead of inventing another playable round.
        var finalSettlement = new FinalSettlementSnapshot();
        var lastReplayRound = game.ReplayRounds.OrderByDescending(round => round.RoundNo).FirstOrDefault();
        if (lastReplayRound != null)
        {
            finalSettlement.GlobalLogs = CleanPromptText(
                ReplaceUsernames(lastReplayRound.FinalSettlementGlobalLogs ?? "", nameMap));
            finalSettlement.CharacterLogs = lastReplayRound.Players
                .Where(player => characterByPlayerId.ContainsKey(player.PlayerId)
                                 && !string.IsNullOrWhiteSpace(player.FinalSettlementLogs))
                .Select(player => new CharacterLogSnapshot
                {
                    CharacterName = characterByPlayerId[player.PlayerId],
                    Text = CleanPromptText(ReplaceUsernames(player.FinalSettlementLogs, nameMap))
                })
                .Where(log => !string.IsNullOrWhiteSpace(log.Text))
                .ToList();
        }

        return new GameStorySnapshot
        {
            GameId = game.GameId,
            RoundCount = rounds.Select(round => round.RoundNo).DefaultIfEmpty(0).Max(),
            GameMode = game.GameMode,
            Players = players,
            Rounds = rounds,
            FinalSettlement = finalSettlement
        };
    }

    private static FightSnapshot CaptureFight(
        FightEntryDto fight,
        IReadOnlyDictionary<string, string> nameMap)
    {
        var attacker = ResolveFightCharacter(fight.AttackerCharName, fight.AttackerName, nameMap);
        var defender = ResolveFightCharacter(fight.DefenderCharName, fight.DefenderName, nameMap);
        var winner = fight.Outcome switch
        {
            "win" => attacker,
            "loss" => defender,
            _ => ""
        };

        return new FightSnapshot
        {
            Attacker = attacker,
            Defender = defender,
            Outcome = fight.Outcome ?? "",
            Winner = winner,
            PointsFromAttackerPerspective = fight.TotalPointsWon,
            Drops = fight.Drops,
            DroppedCharacter = ResolveFightCharacter("", fight.DroppedPlayerName, nameMap)
        };
    }

    private static string ResolveFightCharacter(
        string characterName,
        string username,
        IReadOnlyDictionary<string, string> nameMap)
    {
        if (!string.IsNullOrWhiteSpace(characterName)) return characterName;
        return !string.IsNullOrWhiteSpace(username) && nameMap.TryGetValue(username, out var mapped)
            ? mapped
            : "";
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
        var director = SelectDirectorCard(snapshot.GameId);

        sb.AppendLine("<game-commentary>");

        // ── Instructions ──
        sb.AppendLine("<instructions>");
        sb.AppendLine("Ты — комментатор игры 'King of the Garbage Hill' (Король Мусорной Горы).");
        sb.AppendLine("Это тактическая пошаговая игра на 6 игроков с уникальными персонажами.");
        sb.AppendLine();
        sb.AppendLine("ЗАДАНИЕ: напиши хаотичную, но фактически точную историю этой партии: 6-10 коротких абзацев, собранных из 4-7 ярких сцен.");
        sb.AppendLine("ПРАВИЛА:");
        sb.AppendLine("- Большинство абзацев должны сталкивать минимум двух названных персонажей: действие → ответ → последствие.");
        sb.AppendLine("- Ищи повторные дуэли, месть, случайные союзы, общую жертву и цепочки A → B → C; если факты позволяют, покажи хотя бы одну трёхперсонажную цепочку.");
        sb.AppendLine("- НЕ пересказывай каждый раунд. Выбирай самые важные столкновения и поворотные моменты; номер раунда упоминай только когда он помогает шутке или причинно-следственной связи.");
        sb.AppendLine("- Элементы <round> — единственные сыгранные раунды. <final-settlement> — эпилог после последнего раунда; никогда не называй его новым раундом.");
        sb.AppendLine("- <fights> — точные связи атакующий/защитник/исход. Прозаические логи добавляют контекст способностей и реплик.");
        sb.AppendLine("- Можно придумывать метафоры, преувеличенные реакции, перебивки и реплики в характере. Нельзя придумывать атаки, исходы, способности, смерти или изменения очков.");
        sb.AppendLine("- Шутки должны быть in character — основаны на personality, способностях и фактических событиях.");
        sb.AppendLine("- Не используй названия способностей дословно как сухой список; вплетай их смысл в сцену.");
        sb.AppendLine("- Используй Markdown **жирный** для имён персонажей и ключевых моментов.");
        sb.AppendLine("- Напиши только историю, без заголовков, нумерации и пояснений.");
        sb.AppendLine("- Верни ДВЕ адаптированные версии одной истории: сначала русскую внутри <ru>...</ru>, затем английскую внутри <en>...</en>.");
        sb.AppendLine("- English version must read like native, funny game commentary, not a literal translation. Preserve the same facts and character jokes.");
        sb.AppendLine("- Теги <ru> и <en> обязательны. Внутри каждой версии 6-10 коротких абзацев; никаких других заголовков.");
        sb.AppendLine("</instructions>");

        sb.AppendLine("<director-card>");
        sb.AppendLine($"  <frame>{EscapeXml(director.Frame)}</frame>");
        sb.AppendLine($"  <structure>{EscapeXml(director.Structure)}</structure>");
        sb.AppendLine($"  <chaos-device>{EscapeXml(director.ChaosDevice)}</chaos-device>");
        sb.AppendLine("  <rule>Карточка меняет только подачу и композицию, но не игровые факты. Не превращай её в заголовок.</rule>");
        sb.AppendLine("</director-card>");

        // ── Characters / Results ──
        sb.AppendLine($"<results playable-rounds=\"{snapshot.RoundCount}\" mode=\"{EscapeXml(snapshot.GameMode)}\">");
        foreach (var p in snapshot.Players)
        {
            var botAttr = p.IsBot ? " bot=\"true\"" : "";
            sb.AppendLine($"  <character place=\"{p.Place}\" name=\"{EscapeXml(p.CharacterName)}\" score=\"{p.Score}\"{botAttr}>");

            if (!string.IsNullOrWhiteSpace(p.StoryAgent))
                sb.AppendLine($"    <personality>{EscapeXml(CleanPromptText(p.StoryAgent))}</personality>");

            if (p.Passives.Count > 0)
            {
                sb.AppendLine("    <abilities>");
                foreach (var pas in p.Passives)
                    sb.AppendLine($"      <ability>{EscapeXml(pas)}</ability>");
                sb.AppendLine("    </abilities>");
            }

            sb.AppendLine("  </character>");
        }
        sb.AppendLine("</results>");

        var interactions = BuildInteractionSummaries(snapshot.Rounds);
        if (interactions.Count > 0)
        {
            sb.AppendLine("<interaction-summary>");
            foreach (var interaction in interactions)
            {
                sb.AppendLine(
                    $"  <pair first=\"{EscapeXml(interaction.First)}\" second=\"{EscapeXml(interaction.Second)}\" " +
                    $"meetings=\"{interaction.Meetings}\" first-attacked-second=\"{interaction.FirstAttackedSecond}\" " +
                    $"second-attacked-first=\"{interaction.SecondAttackedFirst}\" first-wins=\"{interaction.FirstWins}\" " +
                    $"second-wins=\"{interaction.SecondWins}\" />");
            }
            sb.AppendLine("</interaction-summary>");
        }

        sb.AppendLine("<rounds>");
        foreach (var round in snapshot.Rounds)
        {
            sb.AppendLine($"  <round number=\"{round.RoundNo}\">");

            if (round.Fights.Count > 0)
            {
                sb.AppendLine("    <fights>");
                foreach (var fight in round.Fights)
                {
                    var droppedAttr = string.IsNullOrWhiteSpace(fight.DroppedCharacter)
                        ? ""
                        : $" dropped-character=\"{EscapeXml(fight.DroppedCharacter)}\"";
                    sb.AppendLine(
                        $"      <fight attacker=\"{EscapeXml(fight.Attacker)}\" defender=\"{EscapeXml(fight.Defender)}\" " +
                        $"outcome=\"{EscapeXml(fight.Outcome)}\" winner=\"{EscapeXml(fight.Winner)}\" " +
                        $"attacker-points=\"{fight.PointsFromAttackerPerspective}\" drops=\"{fight.Drops}\"{droppedAttr} />");
                }
                sb.AppendLine("    </fights>");
            }

            if (!string.IsNullOrWhiteSpace(round.GlobalLogs))
            {
                sb.AppendLine("    <fight-history>");
                sb.AppendLine($"      {EscapeXml(Truncate(round.GlobalLogs.Trim(), 1200))}");
                sb.AppendLine("    </fight-history>");
            }

            if (round.CharacterLogs.Count > 0)
            {
                sb.AppendLine("    <personal-logs>");
                foreach (var log in round.CharacterLogs)
                    sb.AppendLine(
                        $"      <character name=\"{EscapeXml(log.CharacterName)}\">{EscapeXml(Truncate(log.Text, 700))}</character>");
                sb.AppendLine("    </personal-logs>");
            }

            sb.AppendLine("  </round>");
        }
        sb.AppendLine("</rounds>");

        if (snapshot.FinalSettlement.HasContent)
        {
            sb.AppendLine($"<final-settlement after-round=\"{snapshot.RoundCount}\">");
            if (!string.IsNullOrWhiteSpace(snapshot.FinalSettlement.GlobalLogs))
                sb.AppendLine($"  <global>{EscapeXml(Truncate(snapshot.FinalSettlement.GlobalLogs, 1200))}</global>");
            foreach (var log in snapshot.FinalSettlement.CharacterLogs)
                sb.AppendLine(
                    $"  <character name=\"{EscapeXml(log.CharacterName)}\">{EscapeXml(Truncate(log.Text, 700))}</character>");
            sb.AppendLine("</final-settlement>");
        }

        sb.AppendLine("</game-commentary>");

        return sb.ToString();
    }

    private static DirectorCard SelectDirectorCard(ulong gameId)
    {
        string[] frames =
        [
            "спортивный эфир, который быстро потерял контроль над происходящим",
            "слух из таверны, где каждый очевидец явно врёт о деталях",
            "показания свидетелей в абсурдном суде над всей партией",
            "документальный фильм о катастрофе с чрезмерно серьёзным диктором",
            "сломанный групповой чат, в котором персонажи перебивают друг друга",
            "ненадёжный пересказ победителя, которого постоянно поправляют соперники"
        ];
        string[] structures =
        [
            "начни с самого громкого столкновения, затем объясни, как все до него докатились",
            "строй историю как цепную реакцию: каждое действие создаёт проблему следующему персонажу",
            "сделай центральной повторяющуюся вражду, а остальные события вплетай как помехи",
            "покажи партию через неудачи одного персонажа, которые неожиданно двигают чужие планы",
            "сначала создай ощущение союза, затем раскрой, как фактические атаки его разрушили",
            "используй быстрый монтаж между параллельными конфликтами, которые сходятся в финале"
        ];
        string[] chaosDevices =
        [
            "одна повторяющаяся шутка меняет смысл после каждого нового столкновения",
            "короткие реплики и перебивки персонажей спорят с голосом рассказчика",
            "месть передаётся по цепочке от персонажа к персонажу",
            "после каждого конфликта подчёркивай неожиданного третьего персонажа, которому это помогло",
            "один персонаж уверенно и неправильно трактует мотивы остальных, но факты остаются верными",
            "каждая следующая сцена должна быть немного абсурднее предыдущей, не меняя игровых событий"
        ];

        var seed = MixStorySeed(gameId);
        var frame = frames[(int)(seed % (ulong)frames.Length)];
        seed = MixStorySeed(seed);
        var structure = structures[(int)(seed % (ulong)structures.Length)];
        seed = MixStorySeed(seed);
        var chaosDevice = chaosDevices[(int)(seed % (ulong)chaosDevices.Length)];

        return new DirectorCard(frame, structure, chaosDevice);
    }

    private static ulong MixStorySeed(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static List<InteractionSummary> BuildInteractionSummaries(IEnumerable<RoundSnapshot> rounds)
    {
        return rounds
            .SelectMany(round => round.Fights)
            .Where(fight => !string.IsNullOrWhiteSpace(fight.Attacker)
                            && !string.IsNullOrWhiteSpace(fight.Defender)
                            && fight.Attacker != fight.Defender)
            .GroupBy(fight => string.CompareOrdinal(fight.Attacker, fight.Defender) <= 0
                ? (First: fight.Attacker, Second: fight.Defender)
                : (First: fight.Defender, Second: fight.Attacker))
            .Select(group => new InteractionSummary
            {
                First = group.Key.First,
                Second = group.Key.Second,
                Meetings = group.Count(),
                FirstAttackedSecond = group.Count(fight =>
                    fight.Attacker == group.Key.First && fight.Defender == group.Key.Second),
                SecondAttackedFirst = group.Count(fight =>
                    fight.Attacker == group.Key.Second && fight.Defender == group.Key.First),
                FirstWins = group.Count(fight => fight.Winner == group.Key.First),
                SecondWins = group.Count(fight => fight.Winner == group.Key.Second)
            })
            .OrderByDescending(interaction =>
                Math.Min(interaction.FirstAttackedSecond, interaction.SecondAttackedFirst))
            .ThenByDescending(interaction => interaction.Meetings)
            .Take(8)
            .ToList();
    }

    private static string CleanPromptText(string text)
    {
        var resolved = PhrasePayload.Resolve(text ?? "", GameLocalization.Russian)
            .Replace("|>Stat<|", "")
            .Replace("|>Phrase<|", "")
            .Replace("*", "")
            .Replace("_", "");
        return StripDiscordEmoji(resolved).Trim();
    }

    private static string EscapeXml(string text)
    {
        return (text ?? "")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
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
        public ulong GameId { get; set; }
        public int RoundCount { get; set; }
        public string GameMode { get; set; }
        public List<PlayerSnapshot> Players { get; set; }
        public List<RoundSnapshot> Rounds { get; set; } = new();
        public FinalSettlementSnapshot FinalSettlement { get; set; } = new();
    }

    private class PlayerSnapshot
    {
        public string CharacterName { get; set; }
        public string StoryAgent { get; set; }
        public decimal Score { get; set; }
        public int Place { get; set; }
        public bool IsBot { get; set; }
        public List<string> Passives { get; set; }
    }

    private class RoundSnapshot
    {
        public int RoundNo { get; set; }
        public string GlobalLogs { get; set; } = "";
        public List<FightSnapshot> Fights { get; set; } = new();
        public List<CharacterLogSnapshot> CharacterLogs { get; set; } = new();
    }

    private class FightSnapshot
    {
        public string Attacker { get; set; } = "";
        public string Defender { get; set; } = "";
        public string Outcome { get; set; } = "";
        public string Winner { get; set; } = "";
        public int PointsFromAttackerPerspective { get; set; }
        public int Drops { get; set; }
        public string DroppedCharacter { get; set; } = "";
    }

    private class CharacterLogSnapshot
    {
        public string CharacterName { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private class FinalSettlementSnapshot
    {
        public string GlobalLogs { get; set; } = "";
        public List<CharacterLogSnapshot> CharacterLogs { get; set; } = new();
        public bool HasContent => !string.IsNullOrWhiteSpace(GlobalLogs) || CharacterLogs.Count > 0;
    }

    private class InteractionSummary
    {
        public string First { get; set; } = "";
        public string Second { get; set; } = "";
        public int Meetings { get; set; }
        public int FirstAttackedSecond { get; set; }
        public int SecondAttackedFirst { get; set; }
        public int FirstWins { get; set; }
        public int SecondWins { get; set; }
    }

    private sealed record DirectorCard(string Frame, string Structure, string ChaosDevice);
}
