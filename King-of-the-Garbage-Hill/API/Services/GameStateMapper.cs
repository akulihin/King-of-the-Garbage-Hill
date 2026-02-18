using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Maps internal game objects to DTOs for the web client.
/// Handles visibility rules (e.g., don't show opponent passives that are hidden).
/// </summary>
public static class GameStateMapper
{
    // Cache of locally available avatar filenames (lowercase → actual filename)
    private static readonly HashSet<string> _localAvatars;

    // Cache of all visible character names (for prediction dropdowns)
    private static readonly List<string> _allCharacterNames;

    // Full character catalog with base stats (for prediction avatar/stat lookup by non-admins)
    private static readonly List<DTOs.CharacterInfoDto> _allCharacters;

    static GameStateMapper()
    {
        _localAvatars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var avatarDir = Path.Combine(AppContext.BaseDirectory, "DataBase", "art", "avatars");
        if (Directory.Exists(avatarDir))
        {
            foreach (var file in Directory.GetFiles(avatarDir))
            {
                _localAvatars.Add(Path.GetFileName(file));
            }
            Console.WriteLine($"[WebAPI] Loaded {_localAvatars.Count} local avatars from {avatarDir}");
        }

        // Load character names and full catalog for predict dropdowns
        _allCharacterNames = new List<string>();
        _allCharacters = new List<DTOs.CharacterInfoDto>();
        try
        {
            var charsPath = Path.Combine(AppContext.BaseDirectory, "DataBase", "characters.json");
            if (File.Exists(charsPath))
            {
                var json = File.ReadAllText(charsPath);
                var chars = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Game.Classes.CharacterClass>>(json);
                var visible = chars.Where(c => c.Tier >= 0).OrderBy(c => c.Name).ToList();
                _allCharacterNames = visible.Select(c => c.Name).ToList();
                _allCharacters = visible.Select(c => new DTOs.CharacterInfoDto
                {
                    Name = c.Name,
                    Avatar = GetLocalAvatarUrl(c.Avatar),
                    Description = c.Description,
                    Tier = c.Tier,
                    Intelligence = c.GetIntelligence(),
                    Strength = c.GetStrength(),
                    Speed = c.GetSpeed(),
                    Psyche = c.GetPsyche(),
                }).ToList();
                Console.WriteLine($"[WebAPI] Loaded {_allCharacterNames.Count} character names for predictions");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebAPI] Failed to load character names: {ex.Message}");
        }
    }

    /// <summary>
    /// Map a GameClass to a GameStateDto, scoped to the requesting player.
    /// </summary>
    public static GameStateDto ToDto(GameClass game, GamePlayerBridgeClass requestingPlayer = null)
    {
        var isAdmin = requestingPlayer != null && requestingPlayer.PlayerType == 2;

        var dto = new GameStateDto
        {
            GameId = game.GameId,
            RoundNo = game.RoundNo,
            TurnLengthInSecond = game.TurnLengthInSecond,
            TimePassedSeconds = game.TimePassed.Elapsed.TotalSeconds,
            GameVersion = game.GameVersion,
            GameMode = game.GameMode,
            IsFinished = game.IsFinished,
            IsAramPickPhase = game.IsAramPickPhase,
            IsKratosEvent = game.IsKratosEvent,
            GlobalLogs = isAdmin ? game.GetGlobalLogs() : StripHiddenLogs(game.GetGlobalLogs(), game.HiddenGlobalLogSnippets),
            AllGlobalLogs = isAdmin ? game.GetAllGlobalLogs() : StripHiddenLogs(game.GetAllGlobalLogs(), game.HiddenGlobalLogSnippets),
            MyPlayerId = requestingPlayer?.GetPlayerId(),
            MyPlayerType = requestingPlayer?.PlayerType ?? 0,
            PreferWeb = requestingPlayer?.PreferWeb ?? false,
            AllCharacterNames = _allCharacterNames,
            AllCharacters = _allCharacters,
        };

        // Map structured fight log for web animation (scoped: only own fights get full details)
        var myUsername = requestingPlayer?.DiscordUsername;
        dto.FightLog = game.WebFightLog
            // Hidden fights are invisible to non-admin players who aren't a participant
            .Where(f => !f.HiddenFromNonAdmin || isAdmin
                        || (myUsername != null && (f.AttackerName == myUsername || f.DefenderName == myUsername)))
            .Select(f => ScopeFightEntry(f, myUsername, isAdmin))
            .ToList();

        foreach (var player in game.PlayersList)
        {
            var isMe = requestingPlayer != null && player.GetPlayerId() == requestingPlayer.GetPlayerId();
            dto.Players.Add(MapPlayer(player, isMe, isAdmin));
        }

        foreach (var team in game.Teams)
        {
            dto.Teams.Add(new TeamDto
            {
                TeamId = team.TeamId,
                PlayerIds = team.TeamPlayers.ToList(),
            });
        }

        return dto;
    }

    private static PlayerDto MapPlayer(GamePlayerBridgeClass player, bool isMe, bool isAdmin)
    {
        var dto = new PlayerDto
        {
            PlayerId = player.GetPlayerId(),
            DiscordUsername = player.DiscordUsername,
            IsBot = player.IsBot(),
            IsWebPlayer = player.IsWebPlayer,
            TeamId = player.TeamId,
            KratosIsDead = player.Passives.KratosIsDead,
            Character = MapCharacter(player.GameCharacter, isMe, isAdmin),
            Status = MapStatus(player, isMe, isAdmin),
        };

        // Predictions are private — only visible to the owning player
        if (isMe)
        {
            dto.Predictions = player.Predict
                .Select(p => new PredictDto { PlayerId = p.PlayerId, CharacterName = p.CharacterName })
                .ToList();
        }

        return dto;
    }

    private static CharacterDto MapCharacter(CharacterClass character, bool isMe, bool isAdmin)
    {
        // Non-admin viewing an opponent → mask character identity and stats
        if (!isMe && !isAdmin)
        {
            return new CharacterDto
            {
                Name = "???",
                Avatar = "/art/avatars/guess.png",
                AvatarCurrent = "/art/avatars/guess.png",
                Description = "",
                Tier = 0,
                Intelligence = -1, // sentinel: hidden
                Strength = -1,
                Speed = -1,
                Psyche = -1,
                SkillDisplay = "?",
                MoralDisplay = "?",
                Justice = -1,
                SeenJustice = -1,
                SkillClass = "?",
                SkillTarget = "",
                ClassStatDisplayText = "",
                Passives = new List<PassiveDto>(),
            };
        }

        var dto = new CharacterDto
        {
            Name = character.Name,
            Avatar = GetLocalAvatarUrl(character.Avatar),
            AvatarCurrent = GetLocalAvatarUrl(character.AvatarCurrent),
            Description = isMe ? character.Description : "",
            Tier = character.Tier,
            Intelligence = character.GetIntelligence(),
            Strength = character.GetStrength(),
            Speed = character.GetSpeed(),
            Psyche = character.GetPsyche(),
            SkillDisplay = character.GetSkillDisplay(),
            MoralDisplay = character.GetMoralStringWeb(),
            Justice = character.Justice.GetRealJusticeNow(),
            SeenJustice = character.Justice.GetSeenJusticeNow(),
            SkillClass = character.GetSkillClass(),
            SkillTarget = isMe ? character.GetCurrentSkillClassTarget() : "",
            ClassStatDisplayText = character.GetClassStatDisplayTextWeb(),

            // Quality resists
            IntelligenceResist = isMe ? character.GetIntelligenceQualityResistInt() : 0,
            StrengthResist = isMe ? character.GetStrengthQualityResistInt() : 0,
            SpeedResist = isMe ? character.GetSpeedQualityResistInt() : 0,
            PsycheResist = isMe ? character.GetPsycheQualityResistInt() : 0,

            // Quality bonuses
            IntelligenceBonusText = isMe ? GetIntelligenceBonusText(character) : "",
            StrengthBonusText = isMe ? (character.GetStrengthQualityDropBonus() ? "+1 Drop Power" : "") : "",
            SpeedBonusText = isMe ? GetSpeedBonusText(character) : "",
            PsycheBonusText = isMe ? GetPsycheBonusText(character) : "",
        };

        // Show all passives to the owning player, only visible ones to opponents (admin)
        foreach (var passive in character.Passive)
        {
            if (isMe || passive.Visible)
            {
                dto.Passives.Add(new PassiveDto
                {
                    Name = passive.PassiveName,
                    Description = passive.PassiveDescription,
                    Visible = passive.Visible,
                });
            }
        }

        return dto;
    }

    private static PlayerStatusDto MapStatus(GamePlayerBridgeClass player, bool isMe, bool isAdmin)
    {
        var status = player.Status;
        // Non-admin viewing an opponent: hide score (they only see place on leaderboard)
        var canSeeScore = isMe || isAdmin;

        // Extract previous round logs from InGamePersonalLogsAll (split by "|||")
        var previousRoundLogs = "";
        if (isMe)
        {
            var splitLogs = status.InGamePersonalLogsAll.Split("|||");
            if (splitLogs.Length > 1 && splitLogs[^2].Length > 3)
            {
                previousRoundLogs = splitLogs[^2];
            }
        }

        var dto = new PlayerStatusDto
        {
            Score = canSeeScore ? status.GetScore() : -1,
            Place = status.GetPlaceAtLeaderBoard(),
            IsReady = status.IsReady,
            IsBlock = status.IsBlock,
            IsSkip = status.IsSkip,
            IsAutoMove = status.IsAutoMove,
            ConfirmedPredict = status.ConfirmedPredict,
            ConfirmedSkip = status.ConfirmedSkip,
            LvlUpPoints = isMe ? status.LvlUpPoints : 0,
            MoveListPage = isMe ? status.MoveListPage : 1,
            PersonalLogs = isMe ? status.GetInGamePersonalLogs() : "",
            PreviousRoundLogs = previousRoundLogs,
            AllPersonalLogs = isMe ? status.InGamePersonalLogsAll : "",
            ScoreSource = isMe ? status.ScoreSource : "",
            DirectMessages = isMe ? new List<string>(player.WebMessages) : new List<string>(),
            MediaMessages = isMe ? player.WebMediaMessages.Select(m => new MediaMessageDto
            {
                PassiveName = m.PassiveName,
                Text = m.Text,
                FileUrl = m.FileUrl,
                FileType = m.FileType,
                RoundsToPlay = m.RoundsToPlay,
            }).ToList() : new List<MediaMessageDto>(),
            IsAramRollConfirmed = status.IsAramRollConfirmed,
            AramRerolledPassivesTimes = isMe ? status.AramRerolledPassivesTimes : 0,
            AramRerolledStatsTimes = isMe ? status.AramRerolledStatsTimes : 0,
        };

        foreach (var entry in status.PlaceAtLeaderBoardHistory)
        {
            dto.PlaceHistory.Add(new PlaceHistoryDto
            {
                Round = entry.GameRound,
                Place = entry.Place,
            });
        }

        return dto;
    }

    /// <summary>
    /// Converts a remote avatar URL (Discord CDN, imgur, etc.) to a local /art/avatars/ path
    /// if the file exists locally. Otherwise returns the original URL.
    /// </summary>
    public static string GetLocalAvatarUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;

        try
        {
            // Extract filename from the URL
            var uri = new Uri(url);
            var filename = Path.GetFileName(uri.LocalPath);

            if (!string.IsNullOrEmpty(filename) && _localAvatars.Contains(filename))
            {
                return $"/art/avatars/{filename}";
            }
        }
        catch
        {
            // URL parsing failed — return as-is
        }

        return url;
    }

    // ── Quality bonus text helpers (mirror the logic from CharacterClass Get*Resist methods) ──

    private static string GetIntelligenceBonusText(CharacterClass character)
    {
        var skillBonus = character.GetIntelligenceQualitySkillBonus();
        if (skillBonus == 1.0m) return "";
        var pct = (skillBonus - 1) * 100;
        var plus = pct > 0 ? "+" : "";
        return $"{plus}{Math.Round(pct)}% Skill";
    }

    private static string GetSpeedBonusText(CharacterClass character)
    {
        var kite = character.GetSpeedQualityKiteBonus();
        return kite > 0 ? $"+{kite} Kite Distance" : "";
    }

    private static string GetPsycheBonusText(CharacterClass character)
    {
        var moralBonus = character.GetPsycheQualityMoralBonus();
        if (moralBonus == 1.0m) return "";
        var pct = (moralBonus - 1) * 100;
        var plus = pct > 0 ? "+" : "";
        return $"{plus}{Math.Round(pct)}% Moral";
    }

    /// <summary>
    /// Scope fight data visibility: full details only for fights involving the requesting player.
    /// Other fights get stripped of numeric details but keep outcome, participants, and drops (visible to all).
    /// </summary>
    private static FightEntryDto ScopeFightEntry(FightEntryDto f, string myUsername, bool isAdmin)
    {
        // Admins and participants in the fight get full data
        if (isAdmin) return f;
        if (myUsername != null && (f.AttackerName == myUsername || f.DefenderName == myUsername)) return f;

        // Non-participant: strip detailed numeric data, keep participant info + outcome + drops
        return new FightEntryDto
        {
            // Keep: participant identity & outcome
            AttackerName = f.AttackerName,
            AttackerCharName = f.AttackerCharName,
            AttackerAvatar = f.AttackerAvatar,
            DefenderName = f.DefenderName,
            DefenderCharName = f.DefenderCharName,
            DefenderAvatar = f.DefenderAvatar,
            Outcome = f.Outcome,
            WinnerName = f.WinnerName,
            // Keep booleans (no numeric leak)
            IsContrMe = f.IsContrMe,
            IsContrTarget = f.IsContrTarget,
            IsTooGoodMe = f.IsTooGoodMe,
            IsTooGoodEnemy = f.IsTooGoodEnemy,
            IsTooStronkMe = f.IsTooStronkMe,
            IsTooStronkEnemy = f.IsTooStronkEnemy,
            IsStatsBetterMe = f.IsStatsBetterMe,
            IsStatsBetterEnemy = f.IsStatsBetterEnemy,
            UsedRandomRoll = f.UsedRandomRoll,
            QualityDamageApplied = f.QualityDamageApplied,
            // Keep drops (visible to all players)
            Drops = f.Drops,
            DroppedPlayerName = f.DroppedPlayerName,
            // Keep round results (just win/loss direction, no magnitudes)
            Round1PointsWon = f.Round1PointsWon,
            PointsFromJustice = f.PointsFromJustice,
            TotalPointsWon = f.TotalPointsWon > 0 ? 1 : (f.TotalPointsWon < 0 ? -1 : 0),
            // Zero out all numeric details
            AttackerClass = "", DefenderClass = "",
            WhoIsBetterIntel = 0, WhoIsBetterStr = 0, WhoIsBetterSpeed = 0,
            ScaleMe = 0, ScaleTarget = 0,
            ContrMultiplier = 0,
            SkillMultiplierMe = 0, SkillMultiplierTarget = 0,
            PsycheDifference = 0,
            WeighingMachine = 0,
            RandomForPoint = 0,
            ContrWeighingDelta = 0, ScaleWeighingDelta = 0,
            WhoIsBetterWeighingDelta = 0, PsycheWeighingDelta = 0,
            SkillWeighingDelta = 0, JusticeWeighingDelta = 0,
            TooGoodRandomChange = 0, TooStronkRandomChange = 0,
            JusticeRandomChange = 0, ContrRandomChange = 0,
            JusticeMe = 0, JusticeTarget = 0,
            RandomNumber = 0, MaxRandomNumber = 0,
            MoralChange = 0,
            AttackerMoralChange = 0, DefenderMoralChange = 0,
            ResistIntelDamage = 0, ResistStrDamage = 0, ResistPsycheDamage = 0,
            IntellectualDamage = false, EmotionalDamage = false,
            JusticeChange = 0, SkillGainedFromTarget = 0, SkillGainedFromClassAttacker = 0, SkillGainedFromClassDefender = 0,
            SkillDifferenceRandomModifier = 0,
            ContrMultiplierSkillDifference = 0,
        };
    }

    /// <summary>Remove hidden fight text snippets from global logs for non-admin players.</summary>
    private static string StripHiddenLogs(string logs, List<string> hiddenSnippets)
    {
        if (string.IsNullOrEmpty(logs) || hiddenSnippets == null || hiddenSnippets.Count == 0)
            return logs;

        foreach (var snippet in hiddenSnippets)
            logs = logs.Replace(snippet, "");

        return logs;
    }
}
