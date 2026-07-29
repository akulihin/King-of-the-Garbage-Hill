using System;
using System.Collections.Generic;
using System.Linq;

namespace King_of_the_Garbage_Hill.Battleship.Models;

public class BattleshipGame
{
    public string GameId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public BsGamePhase Phase { get; set; } = BsGamePhase.Lobby;
    public BattleshipPlayer Player1 { get; set; }
    public BattleshipPlayer Player2 { get; set; }
    public string CurrentTurnPlayerId { get; set; }
    public int TurnNumber { get; set; }
    public bool IsFinished { get; set; }
    public string WinnerId { get; set; }
    public List<LogEntry> GameLog { get; set; } = new();
    public int ShotCount { get; set; } // Global shot counter (both players)
    /// <summary>The turn whose one-time bot setup (status gate, maneuver and deploys) has run.</summary>
    public int BotPreparedTurnNumber { get; set; } = -1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    /// <summary>Final Boarding is one global transition; this guard prevents repeat bonuses/conversions.</summary>
    public bool BoardingTriggered { get; set; }
    /// <summary>The shot that triggered Boarding is suspended until every mandatory ship is deployed.</summary>
    public bool BoardingResolutionPaused { get; set; }
    public bool PausedTurnContinues { get; set; }
    public bool PausedMoveSummons { get; set; }
    /// <summary>Poison belongs to a physical board. Key = that board owner's DiscordId.</summary>
    public Dictionary<string, HashSet<(int row, int col)>> PoisonZonesByBoardOwner { get; set; } = new();

    /// <summary>Set when the game reaches Combat — leaving from the lobby/setup phases is not counted as a loss.</summary>
    public bool CombatStarted { get; set; }
    /// <summary>Idempotency guard: the W/L / ZBS settlement ran (the game has four distinct end paths).</summary>
    public bool MetaSettled { get; set; }
    /// <summary>Per-player settlement summary, keyed by DiscordId. Only ever sent to that player (see ToDto).</summary>
    public Dictionary<string, BattleshipEndReward> EndRewards { get; set; } = new();

    public BattleshipPlayer GetPlayer(string discordId)
    {
        if (Player1?.DiscordId == discordId) return Player1;
        if (Player2?.DiscordId == discordId) return Player2;
        return null;
    }

    public BattleshipPlayer GetOpponent(string discordId)
    {
        if (Player1?.DiscordId == discordId) return Player2;
        if (Player2?.DiscordId == discordId) return Player1;
        return null;
    }

    public List<BattleshipPlayer> GetPlayers()
    {
        var list = new List<BattleshipPlayer>();
        if (Player1 != null) list.Add(Player1);
        if (Player2 != null) list.Add(Player2);
        return list;
    }

    public void AddLog(string text)
    {
        GameLog.Add(new LogEntry { Text = text });
    }

    public void AddLogFor(string discordId, string text)
    {
        GameLog.Add(new LogEntry { Text = text, VisibleTo = discordId });
    }
}

/// <summary>
/// What the meta settlement did for one player when the game ended.
/// Snapshot of the account stats after the update; rides the state DTO as MyEndReward.
/// </summary>
public class BattleshipEndReward
{
    public bool Won { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int CurrentDailyStreak { get; set; }
    public int BestDailyStreak { get; set; }
    public bool FirstWinAwarded { get; set; }
    public int ZbsAwarded { get; set; }
}

/// <summary>
/// Game log entry. VisibleTo = null → both players (and spectators) see it;
/// otherwise only the player with that DiscordId (spectators see everything).
/// </summary>
public class LogEntry
{
    public string Text { get; set; }
    public string VisibleTo { get; set; }
}

public class BattleshipPlayer
{
    public string DiscordId { get; set; }
    public string Username { get; set; }
    public bool IsBot { get; set; }
    public Faction Faction { get; set; } = Faction.Empire;
    public int CoinsRemaining { get; set; } = 40;
    public Board Board { get; set; } = new();
    public List<Ship> Fleet { get; set; } = new();
    public List<FleetSelection> SelectedShips { get; set; } = new();
    public bool IsReady { get; set; }
    public int SummonSlotsUsed { get; set; } // Normal summon uses this match; never refunded on death
    public int MaxSummonSlots { get; set; } = 4;
    public bool BranderUsed { get; set; } // ТЗ #10: separate from the four normal uses, max 1 per match
    public List<Summon> Summons { get; set; } = new();
    public Weapon SelectedWeapon { get; set; }
    public ShotType SelectedShotType { get; set; } = ShotType.Ballista;
    public int RevealedCellCount { get; set; } // Per-player revealed cells (max 100)
    public int StunShotExpiry { get; set; } = -1; // Shot# when stun expires (-1=none)
    public bool HasPenalty { get; set; } // Skip next turn
    public int LastSummonDeployShotCount { get; set; } = -10; // For 2-shot cooldown
    public bool HasShotThisTurn { get; set; } // For manual move before-shot restriction
    public List<PendingSummonDeploy> PendingSummons { get; set; } = new(); // Delayed summon abilities (pirate/cursed boat death, boarding)
    /// <summary>Stable cursor for the shared Ballista projectile-origin cycle.</summary>
    public int NextBallistaAnimationIndex { get; set; }
    /// <summary>Server-authoritative end of the pause after a combo-preserving hit.</summary>
    public DateTime NextShotAllowedAtUtc { get; set; } = DateTime.MinValue;
    /// <summary>Total duration of the current pause, used to render the same reload bar for both players.</summary>
    public int CurrentShotDelayMs { get; set; }
}

public class Board
{
    public Cell[,] Grid { get; set; } = new Cell[10, 10];
    public List<Ship> PlacedShips { get; set; } = new();

    public Board()
    {
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
            Grid[r, c] = new Cell { Row = r, Col = c };
    }

    public Cell GetCell(int row, int col)
    {
        if (row < 0 || row >= 10 || col < 0 || col >= 10) return null;
        return Grid[row, col];
    }
}

public class Cell
{
    public int Row { get; set; }
    public int Col { get; set; }
    public bool IsRevealed { get; set; }
    public bool IsHit { get; set; }
    public bool IsMiss { get; set; }
    public bool IsBurning { get; set; }
    public Ship ShipRef { get; set; }
    public Summon SummonRef { get; set; }
    public bool WasShipHit { get; set; } // Snapshot: a ship was present when this cell was hit (persists after ship moves)
    public bool WasScratched { get; set; } // Snapshot: hit damaged but didn't destroy a deck (persists after ship moves)
    public bool WasRevealedShip { get; set; } // Anonymous intact occupancy preserved while the moved ship remains alive
    /// <summary>Every summon type that has visited this physical cell, including the spawn cell.</summary>
    public HashSet<SummonType> SummonTrails { get; set; } = new();
    /// <summary>Persistent type-specific markers for summons destroyed on this physical cell.</summary>
    public List<SummonType> SummonDeaths { get; set; } = new();
    /// <summary>
    /// Indices in <see cref="SummonDeaths"/> whose summon was destroyed by Drakkar Freeze.
    /// Keeping indices preserves the cause of each repeated same-type death independently.
    /// </summary>
    public List<int> FrozenSummonDeathIndices { get; set; } = new();
    public bool BurnResistMarked { get; set; } // BurnResist ship survived fire/explosion here — shown black to both players (ТЗ #4)
    public bool WasDodge { get; set; } // Юркая единичка dodged a ballista shot here — static салатовый mark for both players (ТЗ #6)
    public bool WasManeuverDodge { get; set; } // Light Wood Triple moved away — persistent pink shot-history mark
    /// <summary>Snapshot name retained after a sunk ship is later removed or transformed.</summary>
    public string SunkShipName { get; set; }
}

public class Ship
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string DefinitionId { get; set; }
    public string Name { get; set; }
    public List<Deck> Decks { get; set; } = new();
    public int Row { get; set; } = -1;
    public int Col { get; set; } = -1;
    public Orientation Orientation { get; set; } = Orientation.Horizontal;
    public List<ShipStatusType> Statuses { get; set; } = new();
    public List<string> Upgrades { get; set; } = new();
    public List<Weapon> Weapons { get; set; } = new();
    public RangeClass Range { get; set; }
    public int Cost { get; set; }
    public int Space { get; set; } = 1;
    public int ExplosionRadius { get; set; }
    public bool IsDestroyed => Decks.All(d => d.IsDestroyed);
    public bool IsSummon { get; set; }
    public bool IsPlaced { get; set; }
    public int Speed { get; set; } = 1;
    public List<Region> Regions { get; set; } = new();
    public List<string> Abilities { get; set; } = new();
    public bool IsHome { get; set; } // "Домашний" unit — used for first-turn tiebreaker
    public bool HasExploded { get; set; } // Idempotency guard: explode_on_hit fires once (death paths re-enter via HandleShipDeath)
    public bool HasManeuvered { get; set; } // ТЗ #21: manual_move_after_hit is once PER SHIP, not per player
    /// <summary>The latest Light Wood Triple dodge marker; -1 means none.</summary>
    public int LastManeuverDodgeRow { get; set; } = -1;
    public int LastManeuverDodgeCol { get; set; } = -1;
    /// <summary>Coordinates vacated by hidden movement; reconciled only on final death.</summary>
    public List<(int row, int col)> ManeuverStaleHitCells { get; set; } = new();
    /// <summary>While alive, the opponent must not infer the ship's new position from an earlier reveal.</summary>
    public bool HasHiddenMovement { get; set; }
    /// <summary>Stable identity shared by the three initial parts of an assembling ship.</summary>
    public string AssemblyGroupId { get; set; }
    /// <summary>Zero-based part number within an assembly group; -1 for ordinary ships.</summary>
    public int AssemblyComponentIndex { get; set; } = -1;
    public bool IsAssemblyComponent { get; set; }
    /// <summary>
    /// First turn number on which the one-survivor group may assemble. This prevents a player
    /// from assembling immediately after destroying their own second component in the same turn.
    /// </summary>
    public int AssemblyEligibleTurnNumber { get; set; } = -1;

    public List<(int row, int col)> GetOccupiedCells()
    {
        return GetOccupiedCells(Row, Col, Orientation);
    }

    public List<(int row, int col)> GetOccupiedCells(int row, int col, Orientation orientation)
    {
        var cells = new List<(int, int)>();
        foreach (var deck in Decks)
            cells.Add(GetDeckCell(deck, row, col, orientation));
        return cells;
    }

    /// <summary>
    /// Resolve one physical deck from the bow anchor. Deck.Index is deliberately used as
    /// the offset so a deck removed by a ramming maneuver leaves a real gap in the hull.
    /// </summary>
    public (int row, int col) GetDeckCell(
        Deck deck,
        int row,
        int col,
        Orientation orientation)
    {
        var offset = deck.Index;
        if (Abilities.Contains("diagonal_shape"))
        {
            return orientation switch
            {
                Orientation.Horizontal => (row + offset, col + offset),
                Orientation.Vertical => (row + offset, col - offset),
                Orientation.HorizontalReverse => (row - offset, col - offset),
                Orientation.VerticalReverse => (row - offset, col + offset),
                _ => (row + offset, col + offset),
            };
        }

        return orientation switch
        {
            Orientation.Horizontal => (row, col + offset),
            Orientation.HorizontalReverse => (row, col - offset),
            Orientation.Vertical => (row + offset, col),
            Orientation.VerticalReverse => (row - offset, col),
            _ => (row, col + offset),
        };
    }
}

public class Deck
{
    public int Index { get; set; }
    public int MaxHp { get; set; } = 2;
    public int CurrentHp { get; set; } = 2;
    public bool IsDestroyed => CurrentHp <= 0;
    public string Module { get; set; } // e.g. "ballista", "tetracatapult", "mast", "boiler"
    public bool ModuleDestroyed { get; set; }
}

public class Weapon
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public WeaponType Type { get; set; }
    public int Ammo { get; set; } = -1; // -1 = unlimited
    public int AimSpeed { get; set; }
    public string ShipId { get; set; }
    public int DeckIndex { get; set; }
    /// <summary>Placement-time ammo choice for weapons such as the Tetracatapult.</summary>
    public ShotType? ConfiguredShotType { get; set; }

    public bool HasAmmo => Ammo == -1 || Ammo > 0;

    public void UseAmmo()
    {
        if (Ammo > 0) Ammo--;
    }
}

public class Summon
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public SummonType Type { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int Speed { get; set; }
    public int CollisionDamage { get; set; }
    public string OwnerId { get; set; }
    public bool IsAlive { get; set; } = true;
    public int RevealRadius { get; set; } = 1;
    public Direction MoveDirection { get; set; } = Direction.Down;
    public int SpawnedAtShot { get; set; } // Track when summon appeared
    public List<(int row, int col)> ScoutRevealData { get; set; } = new(); // Deferred reveal for scouts
    public List<int> AllowedColumns { get; set; } // Pirate/CursedPirate column restriction
    public bool WaitingForTurnBack { get; set; } // Summon at edge, waiting to be re-sent
    public bool WaitingForDirectionChoice { get; set; } // CursedBoat waiting for owner to choose direction after collision
    public bool IsBoardingShip { get; set; } // Close ship converted during Final Boarding
    public string SourceShipId { get; set; } // Original Close ship for boarding Ballista VFX
    public string SourceShipName { get; set; } // Player-facing identity of a converted Close ship
    public bool HasDetonated { get; set; } // Brander chain-explosion idempotency guard
}

/// <summary>
/// Delayed summon ability — player can deploy this summon later (pirate/cursed boat on death, boarding ships).
/// </summary>
public class PendingSummonDeploy
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public SummonType Type { get; set; }
    public List<int> AllowedColumns { get; set; } = new(); // Columns where ship died (empty = any)
    public bool IsFree { get; set; } = true; // No slot cost / no cooldown
    public bool IsBoarding { get; set; } // Boarding ship — must deploy to first row of enemy field
    public int Speed { get; set; } = 1;
    public int CollisionDamage { get; set; }
    public int RevealRadius { get; set; } = 1; // From original ship's Space
    public string SourceShipName { get; set; } // For log messages
    public string SourceShipId { get; set; } // Original Close ship for boarding Ballista VFX
}

public class ManualMoveOption
{
    public Direction Direction { get; set; }
    public int Distance { get; set; }
    /// <summary>New first-deck anchor selected by clicking the highlighted board cell.</summary>
    public int Row { get; set; }
    public int Col { get; set; }
}

public class FleetSelection
{
    public string DefinitionId { get; set; }
    public string ShipName { get; set; }
    public int Cost { get; set; }
    public List<string> Upgrades { get; set; } = new();
}

public class ShotResult
{
    public bool WasSkipped { get; set; }
    public bool Hit { get; set; }
    public bool Miss { get; set; }
    public bool Scratched { get; set; }
    public bool Destroyed { get; set; }
    public bool Burned { get; set; }
    public bool Dodged { get; set; }
    public bool ShipSunk { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public bool TurnContinues { get; set; }
    /// <summary>Server-selected pause before the same player may fire again; 0 when the turn ends.</summary>
    public int ShotDelayMs { get; set; }
    public string Message { get; set; }
    public string AffectedShipName { get; set; }
    /// <summary>Opaque source identity; only its owner can resolve it to a visible ship coordinate.</summary>
    public string SourceShipId { get; set; }
    public int SourceDeckIndex { get; set; } = -1;
    public int SourceRow { get; set; } = -1;
    public int SourceCol { get; set; } = -1;
    /// <summary>Physical board owner containing the projectile origin.</summary>
    public string SourceBoardPlayerId { get; set; }
    public string ProjectileType { get; set; }
    /// <summary>Physical board owner targeted by this action; fixes own-board VFX routing.</summary>
    public string TargetPlayerId { get; set; }
}

public class ShipDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public int DeckCount { get; set; }
    public RangeClass Range { get; set; }
    public int Cost { get; set; }
    public int DefaultArmor { get; set; } = 2; // HP per deck
    public int Space { get; set; } = 1;
    public int ExplosionRadius { get; set; }
    public List<Faction> Factions { get; set; } = new() { Faction.Empire };
    public List<Region> Regions { get; set; } = new();
    public List<string> Abilities { get; set; } = new();
    public List<WeaponTemplate> DefaultWeapons { get; set; } = new();
    public List<int> DeckHpOverrides { get; set; } // if different HP per deck
    public int Speed { get; set; } = 1;
    public bool IsFree { get; set; }
    public bool IsHome { get; set; } // "Домашний" unit
    public string Description { get; set; }
    public List<UpgradeDefinition> AvailableUpgrades { get; set; } = new();
}

public class WeaponTemplate
{
    public WeaponType Type { get; set; }
    public int Ammo { get; set; } = -1;
    public int DeckIndex { get; set; } // which deck this weapon is on
    public int AimSpeed { get; set; } // 0 = no charge requirement; >0 = revealed cells needed
}

public class UpgradeDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public int Cost { get; set; }
    public string Description { get; set; }
    public string Effect { get; set; }
}
