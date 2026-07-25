using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Captures per-round game state snapshots and persists replays to disk.
/// </summary>
public class ReplayService : IServiceSingleton
{
    private static readonly string ReplayDir = Path.Combine(AppContext.BaseDirectory, "DataBase", "Replays");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false,
    };

    /// <summary>
    /// Per-round capture switch. The headless bot simulation sets this to false: it never saves or
    /// serves a replay, so every captured snapshot is discarded — yet capturing one costs a full
    /// six-player <see cref="GameStateMapper.ToDto"/> projection (including localization) three times
    /// per round, which dominated the simulator's runtime and heap. Production leaves it enabled.
    /// </summary>
    public static bool CaptureEnabled { get; set; } = true;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(ReplayDir);
        return Task.CompletedTask;
    }

    // ── Per-round capture (called from DoomsdayMachine) ──────────────

    /// <summary>
    /// Starts a v2 replay round with the action-locked state used by the fight calculation.
    /// Result-only score/place are captured later as one post-setup snapshot.
    /// Returns null when capture is disabled, which short-circuits the rest of the round's capture.
    /// </summary>
    public static ReplayRoundDto BeginRound(GameClass game, GameUpdateMess gameUpdateMess)
    {
        if (!CaptureEnabled) return null;

        try
        {
            return new ReplayRoundDto
            {
                RoundNo = game.RoundNo,
                PreFightPlayers = CapturePlayers(game, gameUpdateMess),
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Replay] BeginRound failed for game {game.GameId} round {game.RoundNo}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Freezes this round's combat/log stream before next-round effects run. Players are captured as
    /// a fallback here, then replaced atomically after next-round score effects and sorting settle.
    /// </summary>
    public static void CaptureRoundResult(
        ReplayRoundDto round,
        GameClass game,
        GameUpdateMess gameUpdateMess)
    {
        if (round == null) return;

        try
        {
            round.GlobalLogs = game.GetGlobalLogs();
            round.AllGlobalLogs = game.GetAllGlobalLogs();
            round.FightLog = game.WebFightLog.Select(DeepCopyFightEntry).ToList();
            round.Players = CapturePlayers(game, gameUpdateMess);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Replay] CaptureRoundResult failed for game {game.GameId} round {round.RoundNo}: {ex.Message}");
        }
    }

    /// <summary>
    /// Captures the authoritative post-setup result as one snapshot so score and place cannot disagree,
    /// then upserts the round. Fight-facing state remains isolated in PreFightPlayers.
    /// </summary>
    public static void FinalizeRound(
        ReplayRoundDto round,
        GameClass game,
        GameUpdateMess gameUpdateMess)
    {
        if (round == null) return;

        try
        {
            round.Players = CapturePlayers(game, gameUpdateMess, includeCurrentScoreEntries: true);
            round.PostSetupGlobalLogs = game.GetGlobalLogs();
            round.PostSetupAllGlobalLogs = game.GetAllGlobalLogs();
            UpsertRound(round, game);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Replay] FinalizeRound failed for game {game.GameId} round {round.RoundNo}: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes the last round's result after authoritative game-end settlement without appending a fake round 11.
    /// The round-11 setup log buffer is subtracted so only HandleLastRound additions are exposed.
    /// </summary>
    public static void CaptureFinalState(GameClass game, GameUpdateMess gameUpdateMess)
    {
        if (!CaptureEnabled) return;

        var round = game.ReplayRounds.OrderByDescending(x => x.RoundNo).FirstOrDefault();
        if (round == null) return;

        try
        {
            var baselinePlayers = round.Players.ToDictionary(x => x.PlayerId);
            var finalPlayers = CapturePlayers(game, gameUpdateMess);

            foreach (var finalPlayer in finalPlayers)
            {
                if (!baselinePlayers.TryGetValue(finalPlayer.PlayerId, out var baselinePlayer)) continue;
                finalPlayer.FinalSettlementLogs = ExtractAppendedLogs(
                    baselinePlayer.PlayerState?.Status?.PersonalLogs,
                    finalPlayer.PlayerState?.Status?.PersonalLogs);
            }

            if (round.PostSetupGlobalLogs != null)
                round.FinalSettlementGlobalLogs = ExtractAppendedLogs(
                    round.PostSetupGlobalLogs,
                    game.GetGlobalLogs());
            if (round.PostSetupAllGlobalLogs != null)
                round.FinalSettlementAllGlobalLogs = ExtractAppendedLogs(
                    round.PostSetupAllGlobalLogs,
                    game.GetAllGlobalLogs());

            round.Players = finalPlayers;
            UpsertRound(round, game);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Replay] CaptureFinalState failed for game {game.GameId} round {round.RoundNo}: {ex.Message}");
        }
    }

    // ── Build final replay data ──────────────────────────────────────

    public ReplayDataDto BuildReplayData(GameClass game)
    {
        if (game.PlayersList.Any(UnknownBug.Is)
            || Cthulhu.ExcludeFromReplaysAndStory(game))
            throw new InvalidOperationException("This roster cannot be persisted as a replay.");

        var replay = new ReplayDataDto
        {
            GameId = game.GameId,
            ReplayHash = Guid.NewGuid().ToString("N")[..8],
            ReplayFormatVersion = 2,
            GameVersion = game.GameVersion,
            GameMode = game.GameMode,
            TotalRounds = game.ReplayRounds.Count > 0
                ? game.ReplayRounds.Max(x => x.RoundNo)
                : Math.Max(0, game.RoundNo - 1),
            FinishedAt = DateTime.UtcNow,
            AllCharacterNames = GameStateMapper.GetAllCharacterNames(),
            AllCharacters = GameStateMapper.GetAllCharacters(),
            FullChronicle = GameStateMapper.BuildFullChronicle(game),
        };

        foreach (var team in game.Teams)
        {
            replay.Teams.Add(new TeamDto
            {
                TeamId = team.TeamId,
                PlayerIds = team.TeamPlayers.ToList(),
            });
        }

        foreach (var player in game.PlayersList)
        {
            var teamId = game.Teams.Find(t => t.TeamPlayers.Contains(player.GetPlayerId()))?.TeamId ?? 0;
            replay.PlayerSummaries.Add(new ReplayPlayerSummaryDto
            {
                PlayerId = player.GetPlayerId(),
                DiscordUsername = player.DiscordUsername,
                IsBot = player.IsBot(),
                IsWebPlayer = player.IsWebPlayer,
                CharacterName = player.GameCharacter.Name,
                CharacterAvatar = GameStateMapper.GetLocalAvatarUrl(player.GameCharacter.AvatarCurrent ?? player.GameCharacter.Avatar),
                FinalPlace = player.Status.GetPlaceAtLeaderBoard(),
                FinalScore = player.Status.GetScore(),
                CharacterMasteryPoints = player.CharacterMasteryPoints,
                TeamId = teamId,
            });
        }

        replay.Rounds = game.ReplayRounds;

        return replay;
    }

    // ── Persistence ──────────────────────────────────────────────────

    public void SaveReplay(ReplayDataDto replay)
    {
        if (ContainsPrivateRoster(replay))
            return;

        Directory.CreateDirectory(ReplayDir);
        var path = Path.Combine(ReplayDir, $"replay-{replay.ReplayHash}.json");
        var json = JsonSerializer.Serialize(replay, JsonOptions);
        File.WriteAllText(path, json);
        Console.WriteLine($"[Replay] Saved replay for game {replay.GameId} hash={replay.ReplayHash} ({json.Length / 1024}KB)");
    }

    public ReplayDataDto LoadReplay(string hash)
    {
        var path = Path.Combine(ReplayDir, $"replay-{hash}.json");
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        var replay = JsonSerializer.Deserialize<ReplayDataDto>(json, JsonOptions);
        return ContainsPrivateRoster(replay) ? null : replay;
    }

    public ReplayDataDto LoadReplayByGameId(ulong gameId)
    {
        if (!Directory.Exists(ReplayDir)) return null;

        foreach (var file in Directory.GetFiles(ReplayDir, "replay-*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var replay = JsonSerializer.Deserialize<ReplayDataDto>(json, JsonOptions);
                if (replay?.GameId == gameId && !ContainsPrivateRoster(replay)) return replay;
            }
            catch { /* skip corrupt files */ }
        }

        return null;
    }

    public void AttachStory(ulong gameId, string html)
    {
        try
        {
            var replay = LoadReplayByGameId(gameId);
            if (replay == null) return;

            replay.Story = html;
            SaveReplay(replay);
            Console.WriteLine($"[Replay] Attached story to replay {gameId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Replay] AttachStory failed for {gameId}: {ex.Message}");
        }
    }

    public List<ReplayListEntryDto> LoadReplaysByHashes(List<string> hashes, int limit = 20)
    {
        if (!Directory.Exists(ReplayDir) || hashes == null || hashes.Count == 0)
            return new List<ReplayListEntryDto>();

        // Take the most recent hashes first (they're appended chronologically)
        var recentHashes = hashes.AsEnumerable().Reverse().Take(limit).ToList();

        var result = new List<ReplayListEntryDto>();
        foreach (var hash in recentHashes)
        {
            try
            {
                var path = Path.Combine(ReplayDir, $"replay-{hash}.json");
                if (!File.Exists(path)) continue;

                var json = File.ReadAllText(path);
                var replay = JsonSerializer.Deserialize<ReplayDataDto>(json, JsonOptions);
                if (replay == null || ContainsPrivateRoster(replay)) continue;

                result.Add(new ReplayListEntryDto
                {
                    GameId = replay.GameId,
                    ReplayHash = replay.ReplayHash,
                    GameMode = replay.GameMode,
                    TotalRounds = replay.TotalRounds,
                    FinishedAt = replay.FinishedAt,
                    Players = replay.PlayerSummaries.Select(p => new ReplayListPlayerDto
                    {
                        DiscordUsername = p.DiscordUsername,
                        CharacterName = p.CharacterName,
                        CharacterAvatar = p.CharacterAvatar,
                        FinalPlace = p.FinalPlace,
                        FinalScore = p.FinalScore,
                    }).ToList(),
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Replay] Failed to read replay-{hash}.json: {ex.Message}");
            }
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static bool ContainsPrivateRoster(ReplayDataDto replay)
    {
        return replay?.PlayerSummaries?.Any(player =>
            string.Equals(player.CharacterName, UnknownBug.CharacterName, StringComparison.Ordinal)
            || string.Equals(player.CharacterName, UnknownBug.LegacyCharacterName, StringComparison.Ordinal)
            || string.Equals(player.CharacterName, Cthulhu.CharacterName, StringComparison.Ordinal)) == true;
    }

    private static List<ReplayRoundPlayerDto> CapturePlayers(
        GameClass game,
        GameUpdateMess gameUpdateMess,
        bool includeCurrentScoreEntries = false)
    {
        var result = new List<ReplayRoundPlayerDto>();
        foreach (var player in game.PlayersList)
        {
            // Map as if isMe=true so every player's private state remains scoped to their own replay perspective.
            var playerDto = GameStateMapper.ToDto(game, player);
            WebGameService.PopulateCustomLeaderboard(playerDto, game, player, gameUpdateMess);
            var myPlayerDto = playerDto.Players.FirstOrDefault(p => p.PlayerId == player.GetPlayerId());
            if (myPlayerDto == null) continue;

            if (includeCurrentScoreEntries && player.Status.ScoreEntries.Count > 0)
            {
                myPlayerDto.Status.ScoreBreakdown ??= new ScoreBreakdownDto();
                myPlayerDto.Status.ScoreBreakdown.Entries.AddRange(player.Status.ScoreEntries
                    .Where(entry => entry.IsBonus)
                    .Select(entry =>
                        new ScoreEntryDto
                        {
                            Source = entry.Source,
                            Points = entry.Points,
                            IsBonus = entry.IsBonus,
                        }));
            }

            result.Add(new ReplayRoundPlayerDto
            {
                PlayerId = player.GetPlayerId(),
                PlayerState = myPlayerDto,
                CustomLeaderboardView = playerDto.Players.Select(p => new ReplayCustomLeaderboardEntryDto
                {
                    PlayerId = p.PlayerId,
                    CustomLeaderboardPrefix = p.CustomLeaderboardPrefix,
                    CustomLeaderboardText = p.CustomLeaderboardText,
                }).ToList(),
            });
        }

        return result;
    }

    private static void UpsertRound(ReplayRoundDto round, GameClass game)
    {
        game.ReplayRounds.RemoveAll(x => x.RoundNo == round.RoundNo);
        game.ReplayRounds.Add(round);
        game.ReplayRounds.Sort((left, right) => left.RoundNo.CompareTo(right.RoundNo));
    }

    private static string ExtractAppendedLogs(string baseline, string current)
    {
        baseline ??= string.Empty;
        current ??= string.Empty;
        if (baseline.Length == 0) return current;
        if (current.StartsWith(baseline, StringComparison.Ordinal))
            return current[baseline.Length..];

        // AddInGamePersonalLogs may remove only the trailing newline when it combines adjacent
        // entries for the same skill. Accept that one formatting mutation, but fail closed for
        // any larger mismatch so a round-11 setup buffer is never replayed as final settlement.
        var trimmedBaseline = baseline.TrimEnd('\r', '\n');
        return current.StartsWith(trimmedBaseline, StringComparison.Ordinal)
            ? current[trimmedBaseline.Length..]
            : string.Empty;
    }

    private static FightEntryDto DeepCopyFightEntry(FightEntryDto f)
    {
        // FightEntryDto is a simple DTO with only value-type and string fields — shallow copy is sufficient
        return new FightEntryDto
        {
            AttackerName = f.AttackerName,
            AttackerCharName = f.AttackerCharName,
            AttackerAvatar = f.AttackerAvatar,
            DefenderName = f.DefenderName,
            DefenderCharName = f.DefenderCharName,
            DefenderAvatar = f.DefenderAvatar,
            Outcome = f.Outcome,
            WinnerName = f.WinnerName,
            AttackerClass = f.AttackerClass,
            DefenderClass = f.DefenderClass,
            AttackerOriginalClass = f.AttackerOriginalClass,
            DefenderOriginalClass = f.DefenderOriginalClass,
            VersatilityIntel = f.VersatilityIntel,
            VersatilityStr = f.VersatilityStr,
            VersatilitySpeed = f.VersatilitySpeed,
            ScaleMe = f.ScaleMe,
            ScaleTarget = f.ScaleTarget,
            IsNemesisMe = f.IsNemesisMe,
            IsNemesisTarget = f.IsNemesisTarget,
            NemesisMultiplier = f.NemesisMultiplier,
            SkillMultiplierMe = f.SkillMultiplierMe,
            SkillMultiplierTarget = f.SkillMultiplierTarget,
            PsycheDifference = f.PsycheDifference,
            WeighingMachine = f.WeighingMachine,
            IsTooGoodMe = f.IsTooGoodMe,
            IsTooGoodEnemy = f.IsTooGoodEnemy,
            IsTooStronkMe = f.IsTooStronkMe,
            IsTooStronkEnemy = f.IsTooStronkEnemy,
            IsStatsBetterMe = f.IsStatsBetterMe,
            IsStatsBetterEnemy = f.IsStatsBetterEnemy,
            RandomForPoint = f.RandomForPoint,
            NemesisWeighingDelta = f.NemesisWeighingDelta,
            ScaleWeighingDelta = f.ScaleWeighingDelta,
            VersatilityWeighingDelta = f.VersatilityWeighingDelta,
            PsycheWeighingDelta = f.PsycheWeighingDelta,
            SkillWeighingDelta = f.SkillWeighingDelta,
            JusticeWeighingDelta = f.JusticeWeighingDelta,
            TooGoodRandomChange = f.TooGoodRandomChange,
            TooStronkRandomChange = f.TooStronkRandomChange,
            JusticeRandomChange = f.JusticeRandomChange,
            NemesisRandomChange = f.NemesisRandomChange,
            Round1PointsWon = f.Round1PointsWon,
            JusticeMe = f.JusticeMe,
            JusticeTarget = f.JusticeTarget,
            PointsFromJustice = f.PointsFromJustice,
            UsedRandomRoll = f.UsedRandomRoll,
            RandomNumber = f.RandomNumber,
            MaxRandomNumber = f.MaxRandomNumber,
            TotalPointsWon = f.TotalPointsWon,
            MoralChange = f.MoralChange,
            AttackerMoralChange = f.AttackerMoralChange,
            DefenderMoralChange = f.DefenderMoralChange,
            ResistIntelDamage = f.ResistIntelDamage,
            ResistStrDamage = f.ResistStrDamage,
            ResistPsycheDamage = f.ResistPsycheDamage,
            Drops = f.Drops,
            DroppedPlayerName = f.DroppedPlayerName,
            QualityDamageApplied = f.QualityDamageApplied,
            IntellectualDamage = f.IntellectualDamage,
            EmotionalDamage = f.EmotionalDamage,
            JusticeChange = f.JusticeChange,
            SkillGainedFromTarget = f.SkillGainedFromTarget,
            SkillGainedFromClassAttacker = f.SkillGainedFromClassAttacker,
            SkillGainedFromClassDefender = f.SkillGainedFromClassDefender,
            SkillDifferenceRandomModifier = f.SkillDifferenceRandomModifier,
            NemesisMultiplierSkillDifference = f.NemesisMultiplierSkillDifference,
            HiddenFromNonAdmin = f.HiddenFromNonAdmin,
            PortalGunSwap = f.PortalGunSwap,
        };
    }
}
