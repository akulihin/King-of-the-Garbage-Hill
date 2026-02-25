using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.API.DTOs;

// ── Replay Data (full game replay) ──────────────────────────────────

public class ReplayDataDto
{
    public ulong GameId { get; set; }
    public string ReplayHash { get; set; }
    public string GameVersion { get; set; }
    public string GameMode { get; set; }
    public string Story { get; set; }
    public string FullChronicle { get; set; }
    public int TotalRounds { get; set; }
    public DateTime FinishedAt { get; set; }
    public List<string> AllCharacterNames { get; set; } = new();
    public List<CharacterInfoDto> AllCharacters { get; set; } = new();
    public List<TeamDto> Teams { get; set; } = new();
    public List<ReplayPlayerSummaryDto> PlayerSummaries { get; set; } = new();
    public List<ReplayRoundDto> Rounds { get; set; } = new();
}

public class ReplayPlayerSummaryDto
{
    public Guid PlayerId { get; set; }
    public string DiscordUsername { get; set; }
    public bool IsBot { get; set; }
    public bool IsWebPlayer { get; set; }
    public string CharacterName { get; set; }
    public string CharacterAvatar { get; set; }
    public int FinalPlace { get; set; }
    public decimal FinalScore { get; set; }
    public int CharacterMasteryPoints { get; set; }
    public int TeamId { get; set; }
}

public class ReplayRoundDto
{
    public int RoundNo { get; set; }
    public string GlobalLogs { get; set; }
    public string AllGlobalLogs { get; set; }
    public List<FightEntryDto> FightLog { get; set; } = new();
    public List<ReplayRoundPlayerDto> Players { get; set; } = new();
}

public class ReplayRoundPlayerDto
{
    public Guid PlayerId { get; set; }
    public PlayerDto PlayerState { get; set; }
    /// <summary>
    /// Custom leaderboard strings as seen by THIS player for all players in the game.
    /// </summary>
    public List<ReplayCustomLeaderboardEntryDto> CustomLeaderboardView { get; set; } = new();
}

public class ReplayCustomLeaderboardEntryDto
{
    public Guid PlayerId { get; set; }
    public string CustomLeaderboardPrefix { get; set; }
    public string CustomLeaderboardText { get; set; }
}

// ── Replay List (for browsing) ──────────────────────────────────────

public class ReplayListEntryDto
{
    public ulong GameId { get; set; }
    public string ReplayHash { get; set; }
    public string GameMode { get; set; }
    public int TotalRounds { get; set; }
    public DateTime FinishedAt { get; set; }
    public List<ReplayListPlayerDto> Players { get; set; } = new();
}

public class ReplayListPlayerDto
{
    public string DiscordUsername { get; set; }
    public string CharacterName { get; set; }
    public string CharacterAvatar { get; set; }
    public int FinalPlace { get; set; }
    public decimal FinalScore { get; set; }
}
