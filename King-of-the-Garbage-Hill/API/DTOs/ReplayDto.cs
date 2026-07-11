using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace King_of_the_Garbage_Hill.API.DTOs;

// ── Replay Data (full game replay) ──────────────────────────────────

public class ReplayDataDto
{
    public ulong GameId { get; set; }
    public string ReplayHash { get; set; }
    /// <summary>
    /// 0/1 = legacy boundary snapshots; 2 = each ReplayRoundDto owns matching pre-fight and result state.
    /// Kept at the default 0 so old JSON remains distinguishable when deserialized.
    /// </summary>
    public int ReplayFormatVersion { get; set; }
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
    /// <summary>HandleLastRound-only global-log suffix, excluding round-11 setup logs.</summary>
    public string FinalSettlementGlobalLogs { get; set; } = "";
    /// <summary>HandleLastRound-only all-global-log suffix, excluding round-11 setup logs.</summary>
    public string FinalSettlementAllGlobalLogs { get; set; } = "";
    [JsonIgnore] public string PostSetupGlobalLogs { get; set; }
    [JsonIgnore] public string PostSetupAllGlobalLogs { get; set; }
    /// <summary>Per-viewer state captured after actions close but before this round's fight calculation.</summary>
    public List<ReplayRoundPlayerDto> PreFightPlayers { get; set; } = new();
    /// <summary>Per-viewer result state captured after this round settles.</summary>
    public List<ReplayRoundPlayerDto> Players { get; set; } = new();
}

public class ReplayRoundPlayerDto
{
    public Guid PlayerId { get; set; }
    public PlayerDto PlayerState { get; set; }
    /// <summary>
    /// Logs appended by HandleLastRound after the round-11 setup buffer was already captured.
    /// Kept separate so final settlement remains visible without leaking next-round tilt/ban logs.
    /// </summary>
    public string FinalSettlementLogs { get; set; } = "";
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
