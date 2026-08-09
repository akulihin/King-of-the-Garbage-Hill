using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Captures per-round game state snapshots and persists replays to disk.
/// </summary>
public class ReplayService : IServiceSingleton
{
    private static readonly string ReplayDir = Path.Combine(AppContext.BaseDirectory, "DataBase", "Replays");
    private static readonly Version PrivateStreamsSafeVersion = new(5, 2, 16);
    private static readonly Regex VersionPattern = new(
        @"(?<version>\d+\.\d+\.\d+)", RegexOptions.Compiled);

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
            var forceProVisibility = game.PlayersList.Any(player => player.IsProMode);
            round.GlobalLogs = SanitizeReplayGlobalLogs(game.GetGlobalLogs(), game);
            round.AllGlobalLogs = SanitizeReplayGlobalLogs(game.GetAllGlobalLogs(), game);
            round.FightLog = game.WebFightLog
                .Where(fight => !fight.ShadowAction && !fight.HiddenFromNonAdmin)
                .Select(fight => SanitizeReplayFightEntry(fight, forceProVisibility))
                .ToList();
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
            round.PostSetupGlobalLogs = SanitizeReplayGlobalLogs(game.GetGlobalLogs(), game);
            round.PostSetupAllGlobalLogs = SanitizeReplayGlobalLogs(game.GetAllGlobalLogs(), game);
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
                    SanitizeReplayGlobalLogs(game.GetGlobalLogs(), game));
            if (round.PostSetupAllGlobalLogs != null)
                round.FinalSettlementAllGlobalLogs = ExtractAppendedLogs(
                    round.PostSetupAllGlobalLogs,
                    SanitizeReplayGlobalLogs(game.GetAllGlobalLogs(), game));

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
            FullChronicle = GameStateMapper.BuildFullChronicle(game, replaySafe: true),
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
        return SanitizeLoadedReplay(replay);
    }

    public ReplayDataDto LoadReplayByGameId(ulong gameId) =>
        LoadReplayByGameIdCore(gameId, sanitizeForPublicRead: true);

    private ReplayDataDto LoadReplayByGameIdCore(ulong gameId, bool sanitizeForPublicRead)
    {
        if (!Directory.Exists(ReplayDir)) return null;

        foreach (var file in Directory.GetFiles(ReplayDir, "replay-*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var replay = JsonSerializer.Deserialize<ReplayDataDto>(json, JsonOptions);
                if (replay?.GameId != gameId || ContainsPrivateRoster(replay)) continue;
                return sanitizeForPublicRead ? SanitizeLoadedReplay(replay) : replay;
            }
            catch { /* skip corrupt files */ }
        }

        return null;
    }

    public void AttachStory(ulong gameId, string html)
    {
        try
        {
            // Story attachment updates the original persisted record. Public API reads apply their
            // legacy privacy projection separately and never rewrite the file merely by viewing it.
            var replay = LoadReplayByGameIdCore(gameId, sanitizeForPublicRead: false);
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

    /// <summary>
    /// Replays saved before the audience-aware capture version do not record whether any seat was
    /// Pro. Their owner-only web streams therefore cannot be classified safely after the fact.
    /// Clear those streams in the public in-memory projection; the persisted JSON remains untouched.
    /// </summary>
    private static ReplayDataDto SanitizeLoadedReplay(ReplayDataDto replay)
    {
        if (replay == null || ContainsPrivateRoster(replay)) return null;
        SanitizeGordonReplayProjection(replay);
        if (HasAudienceAwarePrivateStreams(replay.GameVersion)) return replay;

        foreach (var round in replay.Rounds ?? new List<ReplayRoundDto>())
        {
            ClearPrivateStreams(round.PreFightPlayers);
            ClearPrivateStreams(round.Players);
        }

        return replay;
    }

    private static void SanitizeGordonReplayProjection(ReplayDataDto replay)
    {
        replay.FullChronicle = GameStateMapper.SanitizeGordonGlobalLogs(replay.FullChronicle);
        foreach (var round in replay.Rounds ?? new List<ReplayRoundDto>())
        {
            round.GlobalLogs = GameStateMapper.SanitizeGordonGlobalLogs(round.GlobalLogs);
            round.AllGlobalLogs = GameStateMapper.SanitizeGordonGlobalLogs(round.AllGlobalLogs);
            round.FinalSettlementGlobalLogs =
                GameStateMapper.SanitizeGordonGlobalLogs(round.FinalSettlementGlobalLogs);
            round.FinalSettlementAllGlobalLogs =
                GameStateMapper.SanitizeGordonGlobalLogs(round.FinalSettlementAllGlobalLogs);
            SanitizeReplayPlayers(round.PreFightPlayers);
            SanitizeReplayPlayers(round.Players);
        }
    }

    private static void SanitizeReplayPlayers(IEnumerable<ReplayRoundPlayerDto> snapshots)
    {
        if (snapshots == null) return;
        foreach (var snapshot in snapshots)
        {
            SanitizeReplayPlayer(snapshot?.PlayerState);
            if (snapshot != null)
                snapshot.FinalSettlementLogs = GameStateMapper.RedactGordonFailureForNonOwner(
                    snapshot.FinalSettlementLogs);
        }
    }

    private static bool HasAudienceAwarePrivateStreams(string gameVersion)
    {
        if (string.IsNullOrWhiteSpace(gameVersion)) return false;
        var match = VersionPattern.Match(gameVersion);
        return match.Success
               && Version.TryParse(match.Groups["version"].Value, out var version)
               && version >= PrivateStreamsSafeVersion;
    }

    private static void ClearPrivateStreams(IEnumerable<ReplayRoundPlayerDto> snapshots)
    {
        if (snapshots == null) return;
        foreach (var snapshot in snapshots)
        {
            var status = snapshot?.PlayerState?.Status;
            status?.DirectMessages?.Clear();
            status?.MediaMessages?.Clear();
        }
    }

    private static List<ReplayRoundPlayerDto> CapturePlayers(
        GameClass game,
        GameUpdateMess gameUpdateMess,
        bool includeCurrentScoreEntries = false)
    {
        var result = new List<ReplayRoundPlayerDto>();
        var forceProVisibility = game.PlayersList.Any(candidate => candidate.IsProMode);
        foreach (var player in game.PlayersList)
        {
            // Map as if isMe=true so every player's private state remains scoped to their own replay perspective.
            // Public replays are unauthenticated: one Pro seat makes every saved perspective use the
            // fail-closed Pro source policy, including a real-admin or Casual seat.
            var playerDto = GameStateMapper.ToDto(
                game, player, forceProVisibility: forceProVisibility);
            WebGameService.PopulateCustomLeaderboard(playerDto, game, player, gameUpdateMess);
            var myPlayerDto = playerDto.Players.FirstOrDefault(p => p.PlayerId == player.GetPlayerId());
            if (myPlayerDto == null) continue;

            SanitizeReplayPlayer(myPlayerDto);

            if (forceProVisibility)
            {
                // All six owner snapshots are stored together in one public JSON document. Keeping
                // each owner's private action bits would reconstruct the very Block/Skip outcomes
                // that the common replay fight stream deliberately collapses to `unknown`.
                myPlayerDto.Status.IsBlock = false;
                myPlayerDto.Status.IsSkip = false;
                myPlayerDto.Status.ConfirmedSkip = false;
                myPlayerDto.Status.TurnInterference = "none";
            }

            if (includeCurrentScoreEntries && player.Status.ScoreEntries.Count > 0)
            {
                myPlayerDto.Status.ScoreBreakdown ??= new ScoreBreakdownDto();
                myPlayerDto.Status.ScoreBreakdown.Entries.AddRange(player.Status.ScoreEntries
                    .Where(entry => entry.IsBonus)
                    .Select(entry =>
                        new ScoreEntryDto
                        {
                            Source = entry.SourceVisibility == FeedbackSourceVisibility.RevealedTarget
                                ? forceProVisibility ? "❓" : entry.Source
                                : entry.SourceVisibility == FeedbackSourceVisibility.NeutralTarget
                                  || entry.SourceVisibility == FeedbackSourceVisibility.ProNeutralTarget
                                  && forceProVisibility
                                    ? "❓"
                                    : ProModeVisibility.MaskScoreSource(
                                        entry.Source,
                                        player,
                                        game,
                                        allowAdminBypass: false,
                                        forceProMode: forceProVisibility),
                            Points = entry.Points,
                            IsBonus = entry.IsBonus,
                            IsNegative = entry.Points < 0,
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
            AttackerPlayerId = f.AttackerPlayerId,
            AttackerName = f.AttackerName,
            AttackerCharName = f.AttackerCharName,
            AttackerAvatar = f.AttackerAvatar,
            DefenderPlayerId = f.DefenderPlayerId,
            DefenderName = f.DefenderName,
            DefenderCharName = f.DefenderCharName,
            DefenderAvatar = f.DefenderAvatar,
            Outcome = f.Outcome,
            WinnerPlayerId = f.WinnerPlayerId,
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
            StormAppeared = f.StormAppeared,
            StormWeighingDelta = f.StormWeighingDelta,
            StormFlipped = f.StormFlipped,
        };
    }

    private static FightEntryDto SanitizeReplayFightEntry(
        FightEntryDto fight,
        bool forceProVisibility)
    {
        var projection = DeepCopyFightEntry(fight);
        if (!forceProVisibility || projection.Outcome is not ("block" or "skip"))
            return projection;

        // A replay is a single unauthenticated artifact shared by every selectable perspective.
        // It cannot retain an owner-only Block/Skip outcome without also exposing it to strangers.
        projection.Outcome = "unknown";
        projection.WinnerPlayerId = null;
        projection.WinnerName = "";
        projection.TotalPointsWon = 0;
        return projection;
    }

    private static string StripDopaShadowLogs(string logs, GameClass game)
    {
        if (string.IsNullOrEmpty(logs) || game?.DopaShadowGlobalLogSnippets == null)
            return logs;

        foreach (var snippet in game.DopaShadowGlobalLogSnippets)
            if (!string.IsNullOrEmpty(snippet))
                logs = logs.Replace(snippet, "", StringComparison.Ordinal);
        return logs;
    }

    private static string SanitizeReplayGlobalLogs(string logs, GameClass game)
    {
        logs = GameStateMapper.SanitizeGordonGlobalLogs(logs);
        logs = StripDopaShadowLogs(logs, game);
        if (!string.IsNullOrEmpty(logs) && game?.AllHiddenGlobalLogSnippets != null)
            foreach (var snippet in game.AllHiddenGlobalLogSnippets)
                if (!string.IsNullOrEmpty(snippet))
                    logs = logs.Replace(snippet, "", StringComparison.Ordinal);

        if (string.IsNullOrEmpty(logs)
            || game == null
            || !game.PlayersList.Any(player => player.IsProMode))
            return logs;

        // Replay logs are unauthenticated and have no owner projection. If the match contains a Pro
        // seat, fail closed by removing every snippet that was owner-only for Pro viewers.
        foreach (var scoped in game.ProOwnerGlobalLogSnippets ?? new List<GameClass.ProOwnerGlobalLogClass>())
            if (!string.IsNullOrEmpty(scoped.Text))
                logs = logs.Replace(scoped.Text, "", StringComparison.Ordinal);
        return logs
            .Replace("(Блок)", "(?)", StringComparison.Ordinal)
            .Replace("(Скип)", "(?)", StringComparison.Ordinal)
            .Replace("(Block)", "(?)", StringComparison.Ordinal)
            .Replace("(Skip)", "(?)", StringComparison.Ordinal);
    }

    private static void SanitizeReplayPlayer(PlayerDto player)
    {
        if (player == null) return;
        if (player.Status != null)
        {
            player.Status.PersonalLogs =
                GameStateMapper.RedactGordonFailureForNonOwner(player.Status.PersonalLogs);
            player.Status.PreviousRoundLogs =
                GameStateMapper.RedactGordonFailureForNonOwner(player.Status.PreviousRoundLogs);
            player.Status.AllPersonalLogs =
                GameStateMapper.RedactGordonFailureForNonOwner(player.Status.AllPersonalLogs);
        }

        // Anonymous replay viewers can switch to Gordon's saved perspective, but they are not
        // Gordon. Do not persist the owner-only failure receipt in a pending transition snapshot.
        var replayHalfLife = player.PassiveAbilityStates?.Gordon?.HalfLife;
        if (replayHalfLife == null) return;
        replayHalfLife.DecisionMessage = "";
        replayHalfLife.DecisionMessageText = null;
    }
}
