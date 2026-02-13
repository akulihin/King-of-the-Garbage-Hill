using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.API.DTOs;

// ── Top-level game state sent to web clients ──────────────────────────

public class GameStateDto
{
    public ulong GameId { get; set; }
    public int RoundNo { get; set; }
    public double TurnLengthInSecond { get; set; }
    public double TimePassedSeconds { get; set; }
    public string GameVersion { get; set; }
    public string GameMode { get; set; }
    public bool IsFinished { get; set; }
    public bool IsAramPickPhase { get; set; }
    public bool IsKratosEvent { get; set; }
    public string GlobalLogs { get; set; }

    /// <summary>Full history of all global logs across all rounds.</summary>
    public string AllGlobalLogs { get; set; }

    /// <summary>The PlayerId of the requesting player, or null for spectators.</summary>
    public Guid? MyPlayerId { get; set; }

    /// <summary>PlayerType of the requesting player (0/1 = normal, 2 = admin, 404 = bot).</summary>
    public int MyPlayerType { get; set; }

    /// <summary>Whether the requesting player has "Prefer Web" enabled (suppresses Discord messages).</summary>
    public bool PreferWeb { get; set; }

    /// <summary>All character names available for predictions.</summary>
    public List<string> AllCharacterNames { get; set; } = new();

    /// <summary>Full character catalog with base stats (for prediction avatar/stat lookup).</summary>
    public List<CharacterInfoDto> AllCharacters { get; set; } = new();

    public List<PlayerDto> Players { get; set; } = new();
    public List<TeamDto> Teams { get; set; } = new();
}

// ── Per-player state (scoped to the requesting player) ────────────────

public class PlayerDto
{
    public Guid PlayerId { get; set; }
    public string DiscordUsername { get; set; }
    public bool IsBot { get; set; }
    public bool IsWebPlayer { get; set; }
    public int TeamId { get; set; }

    // Character info
    public CharacterDto Character { get; set; }

    // Status
    public PlayerStatusDto Status { get; set; }

    // Predictions (only visible to the owning player)
    public List<PredictDto> Predictions { get; set; }

    /// <summary>Custom prefix before the player's place number (e.g. octopus tentacles). Web-friendly HTML.</summary>
    public string CustomLeaderboardPrefix { get; set; }

    /// <summary>Custom leaderboard annotations after the player name, visible to the requesting player
    /// (e.g. weed counts, hunt targets, win streaks from passives). Web-friendly HTML.</summary>
    public string CustomLeaderboardText { get; set; }
}

public class CharacterDto
{
    public string Name { get; set; }
    public string Avatar { get; set; }
    public string AvatarCurrent { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }

    // Stats
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Speed { get; set; }
    public int Psyche { get; set; }

    // Derived
    public string SkillDisplay { get; set; }
    public string MoralDisplay { get; set; }
    public int Justice { get; set; }
    public int SeenJustice { get; set; }
    public string SkillClass { get; set; }
    public string ClassStatDisplayText { get; set; }

    // Passives (only visible ones for opponents)
    public List<PassiveDto> Passives { get; set; } = new();
}

public class PassiveDto
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Visible { get; set; }
}

public class PlayerStatusDto
{
    public decimal Score { get; set; }
    public int Place { get; set; }
    public bool IsReady { get; set; }
    public bool IsBlock { get; set; }
    public bool IsSkip { get; set; }
    public bool IsAutoMove { get; set; }
    public bool ConfirmedPredict { get; set; }
    public bool ConfirmedSkip { get; set; }
    public int LvlUpPoints { get; set; }
    public int MoveListPage { get; set; }
    public string PersonalLogs { get; set; }
    public string PreviousRoundLogs { get; set; }
    public string AllPersonalLogs { get; set; }
    public string ScoreSource { get; set; }

    /// <summary>Direct messages (ephemeral msgs that are sent then deleted in Discord).</summary>
    public List<string> DirectMessages { get; set; } = new();

    /// <summary>Character phrase media messages (text, audio, images from SendLogSeparate/SendLogSeparateWithFile).</summary>
    public List<MediaMessageDto> MediaMessages { get; set; } = new();
    public bool IsAramRollConfirmed { get; set; }
    public int AramRerolledPassivesTimes { get; set; }
    public int AramRerolledStatsTimes { get; set; }
    public List<PlaceHistoryDto> PlaceHistory { get; set; } = new();
}

public class PlaceHistoryDto
{
    public int Round { get; set; }
    public int Place { get; set; }
}

public class PredictDto
{
    public Guid PlayerId { get; set; }
    public string CharacterName { get; set; }
}

public class TeamDto
{
    public int TeamId { get; set; }
    public List<Guid> PlayerIds { get; set; } = new();
}

public class MediaMessageDto
{
    /// <summary>Name of the passive/ability that triggered this message.</summary>
    public string PassiveName { get; set; }
    /// <summary>The text/phrase content.</summary>
    public string Text { get; set; }
    /// <summary>URL to the media file (e.g. /art/events/kratos_death.jpg, /sound/Kratos_PLAY_ME.mp3). Null for text-only.</summary>
    public string FileUrl { get; set; }
    /// <summary>One of: "text", "audio", "image"</summary>
    public string FileType { get; set; } = "text";
}

// ── Request DTOs ──────────────────────────────────────────────────────

public class AttackRequest
{
    public int TargetPlace { get; set; }
}

public class LevelUpRequest
{
    /// <summary>1=Intelligence, 2=Strength, 3=Speed, 4=Psyche</summary>
    public int StatIndex { get; set; }
}

public class PredictRequest
{
    public Guid TargetPlayerId { get; set; }
    public string CharacterName { get; set; }
}

public class AramRerollRequest
{
    /// <summary>1-4 for passive slots, 5 for base stats</summary>
    public int Slot { get; set; }
}

public class StartGameRequest
{
    public string Mode { get; set; } = "Normal";
}

// ── Lobby DTOs ────────────────────────────────────────────────────────

public class LobbyStateDto
{
    public int ActiveGames { get; set; }
    public List<ActiveGameDto> Games { get; set; } = new();
    public List<CharacterInfoDto> AvailableCharacters { get; set; } = new();
}

public class ActiveGameDto
{
    public ulong GameId { get; set; }
    public int RoundNo { get; set; }
    public int PlayerCount { get; set; }
    public int HumanCount { get; set; }
    public string GameMode { get; set; }
    public bool IsFinished { get; set; }
}

public class CharacterInfoDto
{
    public string Name { get; set; }
    public string Avatar { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }

    // Base stats — used by non-admin players to fill in predicted character info
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Speed { get; set; }
    public int Psyche { get; set; }
}

// ── Auth DTOs ─────────────────────────────────────────────────────────

public class AuthResponse
{
    public bool Success { get; set; }
    public string Token { get; set; }
    public ulong DiscordId { get; set; }
    public string Username { get; set; }
    public string Error { get; set; }
}
