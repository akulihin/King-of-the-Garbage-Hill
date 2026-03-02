using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
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

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(ReplayDir);
        return Task.CompletedTask;
    }

    // ── Per-round capture (called from DoomsdayMachine) ──────────────

    /// <summary>
    /// Captures the current round state into game.ReplayRounds.
    /// Called BEFORE fight log is cleared each round.
    /// </summary>
    public static void CaptureRound(GameClass game, GameUpdateMess gameUpdateMess)
    {
        try
        {
            var round = new ReplayRoundDto
            {
                RoundNo = game.RoundNo,
                GlobalLogs = game.GetGlobalLogs(),
                AllGlobalLogs = game.GetAllGlobalLogs(),
                FightLog = game.WebFightLog.Select(DeepCopyFightEntry).ToList(),
            };

            foreach (var player in game.PlayersList)
            {
                // Map as if isMe=true so all private data is included
                var playerDto = GameStateMapper.ToDto(game, player);
                WebGameService.PopulateCustomLeaderboard(playerDto, game, player, gameUpdateMess);
                var myPlayerDto = playerDto.Players.FirstOrDefault(p => p.PlayerId == player.GetPlayerId());
                if (myPlayerDto != null)
                {
                    // Save custom leaderboard from this player's perspective for all players
                    var customView = playerDto.Players.Select(p => new ReplayCustomLeaderboardEntryDto
                    {
                        PlayerId = p.PlayerId,
                        CustomLeaderboardPrefix = p.CustomLeaderboardPrefix,
                        CustomLeaderboardText = p.CustomLeaderboardText,
                    }).ToList();

                    round.Players.Add(new ReplayRoundPlayerDto
                    {
                        PlayerId = player.GetPlayerId(),
                        PlayerState = myPlayerDto,
                        CustomLeaderboardView = customView,
                    });
                }
            }

            game.ReplayRounds.Add(round);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Replay] CaptureRound failed for game {game.GameId} round {game.RoundNo}: {ex.Message}");
        }
    }

    // ── Build final replay data ──────────────────────────────────────

    public ReplayDataDto BuildReplayData(GameClass game)
    {
        var replay = new ReplayDataDto
        {
            GameId = game.GameId,
            ReplayHash = Guid.NewGuid().ToString("N")[..8],
            GameVersion = game.GameVersion,
            GameMode = game.GameMode,
            TotalRounds = game.RoundNo,
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
        return JsonSerializer.Deserialize<ReplayDataDto>(json, JsonOptions);
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
                if (replay?.GameId == gameId) return replay;
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
                if (replay == null) continue;

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
