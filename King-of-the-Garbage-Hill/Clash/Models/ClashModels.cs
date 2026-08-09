using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace King_of_the_Garbage_Hill.Clash.Models;

public sealed class ClashGame
{
    public object SyncRoot { get; } = new();
    public string GameId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public ClashGamePhase Phase { get; set; } = ClashGamePhase.Lobby;
    public int Width { get; set; } = 5;
    public int Length { get; set; } = 5;
    public bool VsBot { get; set; }
    public ClashPlayer Host { get; set; }
    public ClashPlayer Guest { get; set; }
    public long Revision { get; set; }
    public int ClashNumber { get; set; }
    public string CurrentTurnPlayerId { get; set; }
    public string WinnerId { get; set; }
    public bool IsDraw { get; set; }
    public ClashTerminalReason TerminalReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public DateTime? ResolutionEndsAtUtc { get; set; }
    public List<ClashUnit> Units { get; set; } = new();
    public ClashResolutionDto LatestResolution { get; set; }
    public int ActivePassStreak { get; set; }
    public HashSet<string> ProcessedCommandIds { get; set; } = new(StringComparer.Ordinal);
    public Queue<string> ProcessedCommandOrder { get; set; } = new();

    public bool IsFinished => Phase == ClashGamePhase.Finished;

    public ClashPlayer GetPlayer(string playerId)
    {
        if (Host?.PlayerId == playerId) return Host;
        if (Guest?.PlayerId == playerId) return Guest;
        return null;
    }

    public ClashPlayer GetPlayer(ClashSide side)
    {
        return side == ClashSide.Host ? Host : Guest;
    }

    public ClashPlayer GetOpponent(string playerId)
    {
        if (Host?.PlayerId == playerId) return Guest;
        if (Guest?.PlayerId == playerId) return Host;
        return null;
    }

    public ClashSide? GetSide(string playerId)
    {
        if (Host?.PlayerId == playerId) return ClashSide.Host;
        if (Guest?.PlayerId == playerId) return ClashSide.Guest;
        return null;
    }

    public IEnumerable<ClashPlayer> GetPlayers()
    {
        if (Host != null) yield return Host;
        if (Guest != null) yield return Guest;
    }
}

public sealed class ClashPlayer
{
    public string PlayerId { get; set; }
    public string Username { get; set; }
    public bool IsBot { get; set; }
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
    public bool FrontConfirmed { get; set; }
    /// <summary>Боевой дух стороны; в правилах и коде также называется «Мораль».</summary>
    public int Morale { get; set; } = 1;
    public List<string> ArmyDefinitionIds { get; set; } = new();
    public List<string> UsedActiveKeys { get; set; } = new();
    public int ActiveSelectionsUsed { get; set; }
    public bool RepeatedActiveUsed { get; set; }
    public bool ActiveDone { get; set; }
}

public sealed class ClashUnit
{
    public string InstanceId { get; set; }
    public string DefinitionId { get; set; }
    public string OwnerId { get; set; }
    public ClashSide OwnerSide { get; set; }
    public int? BoardRow { get; set; }
    public int? Column { get; set; }
    public int Hp { get; set; }
    public int ShieldCharges { get; set; }
    public int DodgeCharges { get; set; }
    public int BleedCharges { get; set; }
    public int BleedStacks { get; set; }
    public int RangedReadyClash { get; set; } = 1;
    public bool Alive { get; set; } = true;
    public bool Deployed { get; set; }
    public bool IsVisible { get; set; }
}

public sealed class ClashUnitDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Faction { get; set; }
    public int Attack { get; set; }
    public int MaxHp { get; set; }
    public int Speed { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ClashAttackPattern AttackPattern { get; set; } = ClashAttackPattern.AdjacentForward;
    public int AttackRange { get; set; } = 1;
    public bool CanDefaultAdvance { get; set; } = true;
    public bool IsRanged { get; set; }
    public int ReloadClashes { get; set; }
    public int ShieldCharges { get; set; }
    public int DodgeCharges { get; set; }
    public bool AppliesBleed { get; set; }
    public bool DiesToAoe { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ClashPassiveDefinition> Passives { get; set; } = new();
    public List<ClashAbilityDefinition> Abilities { get; set; } = new();
}

public sealed class ClashPassiveDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

public sealed class ClashAbilityDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Target { get; set; }
    public int Value { get; set; }
    public bool IsAoe { get; set; }
}

public sealed class ClashMutationResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
    public List<ClashResolutionDto> Resolutions { get; set; } = new();

    public static ClashMutationResult Ok(IEnumerable<ClashResolutionDto> resolutions = null)
    {
        return new ClashMutationResult
        {
            Success = true,
            Resolutions = resolutions?.ToList() ?? new(),
        };
    }

    public static ClashMutationResult Fail(string error)
    {
        return new ClashMutationResult { Error = error };
    }
}

public sealed class ClashResolutionCompletionResult
{
    public bool Pending { get; set; }
    public bool Completed { get; set; }
    public int RemainingMs { get; set; }
    public List<ClashResolutionDto> Resolutions { get; set; } = new();
}

public sealed class ClashBotDecision
{
    public ClashBotActionKind Kind { get; set; }
    public string UnitInstanceId { get; set; }
    public int Row { get; set; }
    public int Column { get; set; }
}

// ── SignalR DTOs ──────────────────────────────────────────────────────

public sealed class ClashLobbyDto
{
    public List<ClashLobbyGameDto> Games { get; set; } = new();
}

public sealed class ClashLobbyGameDto
{
    public string GameId { get; set; }
    public string Phase { get; set; }
    public int Width { get; set; }
    public int Length { get; set; }
    public string HostName { get; set; }
    public string GuestName { get; set; }
    public bool VsBot { get; set; }
    public bool CanJoin { get; set; }
    public string CreatedAt { get; set; }
}

public sealed class ClashCatalogDto
{
    public List<ClashUnitDefinition> Units { get; set; } = new();
    public int MinWidth { get; set; }
    public int MaxWidth { get; set; }
    public int MinLength { get; set; }
    public int MaxLength { get; set; }
    public int DefaultWidth { get; set; }
    public int DefaultLength { get; set; }
    public int StartingMorale { get; set; }
}

public sealed class ClashGameStateDto
{
    public string GameId { get; set; }
    public long Revision { get; set; }
    public string Phase { get; set; }
    public int Width { get; set; }
    public int Length { get; set; }
    public int ClashNumber { get; set; }
    public bool VsBot { get; set; }
    public bool IsFinished { get; set; }
    public string WinnerId { get; set; }
    public bool IsDraw { get; set; }
    public string TerminalReason { get; set; }
    public string CurrentTurnPlayerId { get; set; }
    public bool IsMyTurn { get; set; }
    public string MyPlayerId { get; set; }
    public int? RequiredPlacementRow { get; set; }
    public string PlacementActionLabel { get; set; }
    public ClashPlayerDto Host { get; set; }
    public ClashPlayerDto Guest { get; set; }
    public List<ClashBoardCellDto> BoardCells { get; set; } = new();
    public ClashResolutionDto LatestResolution { get; set; }
    public bool CanConfigure { get; set; }
    public bool CanSetArmy { get; set; }
    public bool CanConfirmReady { get; set; }
    public bool CanPlace { get; set; }
    public bool CanRemove { get; set; }
    public bool CanConfirmPlacement { get; set; }
    public bool CanPlaceReinforcement { get; set; }
    public bool CanUseActive { get; set; }
    public bool CanContinue { get; set; }
    public bool CanForfeit { get; set; }
}

public sealed class ClashPlayerDto
{
    public string PlayerId { get; set; }
    public string Username { get; set; }
    public bool IsBot { get; set; }
    public bool IsHost { get; set; }
    public bool IsMe { get; set; }
    public bool IsReady { get; set; }
    public bool InitialFrontConfirmed { get; set; }
    public int Morale { get; set; }
    public int ArmySize { get; set; }
    public int HandCount { get; set; }
    public List<string> SelectedArmyDefinitionIds { get; set; } = new();
    public List<ClashUnitDto> Hand { get; set; } = new();
    public List<string> UsedActiveIds { get; set; } = new();
    public int ActiveSelectionsUsed { get; set; }
    public int ActiveSelectionLimit { get; set; }
    public bool CanRepeatActive { get; set; }
    public bool ActiveEffectsDoubled { get; set; }
    public bool HasContinued { get; set; }
}

public sealed class ClashBoardCellDto
{
    public int BoardRow { get; set; }
    public int Column { get; set; }
    public string TerritorySide { get; set; }
    public ClashUnitDto Unit { get; set; }
    public bool IsHidden { get; set; }
}

public sealed class ClashUnitDto
{
    public string InstanceId { get; set; }
    public string DefinitionId { get; set; }
    public string OwnerId { get; set; }
    public string OwnerSide { get; set; }
    public int? BoardRow { get; set; }
    public int? Column { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Attack { get; set; }
    public int Speed { get; set; }
    public int ShieldCharges { get; set; }
    public int DodgeCharges { get; set; }
    public int BleedStacks { get; set; }
    public int RangedReadyClash { get; set; }
    public bool DiesToAoe { get; set; }
    public bool Alive { get; set; }
    public bool Deployed { get; set; }
    public bool IsHidden { get; set; }
}

public sealed class ClashResolutionDto
{
    public string GameId { get; set; }
    public long Revision { get; set; }
    public int ClashNumber { get; set; }
    public string StartedAtUtc { get; set; }
    public int DurationMs { get; set; }
    public List<ClashResolutionEventDto> Events { get; set; } = new();
    public List<ClashUnitDto> FinalUnits { get; set; } = new();
    public string WinnerId { get; set; }
    public bool IsDraw { get; set; }
    public string TerminalReason { get; set; }
}

public sealed class ClashResolutionEventDto
{
    public int Sequence { get; set; }
    public string Type { get; set; }
    public string ActorUnitInstanceId { get; set; }
    public string TargetUnitInstanceId { get; set; }
    public int Speed { get; set; }
    public int StartOffsetMs { get; set; }
    public int ImpactOffsetMs { get; set; }
    public int Amount { get; set; }
    public int? FromBoardRow { get; set; }
    public int? ToBoardRow { get; set; }
    public int? Column { get; set; }
    public string Message { get; set; }
}
