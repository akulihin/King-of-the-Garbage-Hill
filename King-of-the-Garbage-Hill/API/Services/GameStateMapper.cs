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
                    Avatar = ToLocalAvatarUrl(c.Avatar),
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
            GlobalLogs = game.GetGlobalLogs(),
            MyPlayerId = requestingPlayer?.GetPlayerId(),
            MyPlayerType = requestingPlayer?.PlayerType ?? 0,
            AllCharacterNames = _allCharacterNames,
            AllCharacters = _allCharacters,
        };

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
            Character = MapCharacter(player.GameCharacter, isMe, isAdmin),
            Status = MapStatus(player.Status, isMe, isAdmin),
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
                ClassStatDisplayText = "",
                Passives = new List<PassiveDto>(),
            };
        }

        var dto = new CharacterDto
        {
            Name = character.Name,
            Avatar = ToLocalAvatarUrl(character.Avatar),
            AvatarCurrent = ToLocalAvatarUrl(character.AvatarCurrent),
            Description = isMe ? character.Description : "",
            Tier = character.Tier,
            Intelligence = character.GetIntelligence(),
            Strength = character.GetStrength(),
            Speed = character.GetSpeed(),
            Psyche = character.GetPsyche(),
            SkillDisplay = character.GetSkillDisplay(),
            MoralDisplay = character.GetMoralString(),
            Justice = character.Justice.GetRealJusticeNow(),
            SeenJustice = character.Justice.GetSeenJusticeNow(),
            SkillClass = character.GetSkillClass(),
            ClassStatDisplayText = character.GetClassStatDisplayText(),
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

    private static PlayerStatusDto MapStatus(InGameStatus status, bool isMe, bool isAdmin)
    {
        // Non-admin viewing an opponent: hide score (they only see place on leaderboard)
        var canSeeScore = isMe || isAdmin;
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
            AllPersonalLogs = isMe ? status.InGamePersonalLogsAll : "",
            ScoreSource = isMe ? status.ScoreSource : "",
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
    private static string ToLocalAvatarUrl(string url)
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
}
