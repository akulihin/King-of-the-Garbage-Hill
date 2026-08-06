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
    /// <summary>The only player whose fleet and summon entitlements enter Final Boarding.</summary>
    public string BoardingPlayerId { get; set; }
    /// <summary>The shot that triggered Boarding is suspended until every mandatory ship is deployed.</summary>
    public bool BoardingResolutionPaused { get; set; }
    /// <summary>
    /// The action that destroyed a Matryoshka hull is suspended until its owner chooses
    /// which contiguous part of the wreck the next hull occupies.
    /// </summary>
    public bool MatryoshkaResolutionPaused { get; set; }
    public bool PausedTurnContinues { get; set; }
    public bool PausedMoveSummons { get; set; }
    /// <summary>
    /// Durable cursor for one summon-movement resolution. A mandatory interaction may suspend
    /// the resolution between atomic movement/poison work items and resume it without replaying
    /// work that has already happened.
    /// </summary>
    public SummonMovementResolutionState SummonMovementState { get; set; }
    /// <summary>
    /// Transient public events produced when Penalty/Stun automatically advances past a turn.
    /// The transport drains this queue before publishing the resulting state.
    /// </summary>
    public Queue<ShotResult> PendingTurnSkipEvents { get; set; } = new();
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

    /// <summary>
    /// Add tactical detail about one physical board. Its owner always sees it; the opponent
    /// is added to the immutable audience only when an operational Mast exists at event time.
    /// Spectators retain the full audit trail.
    /// </summary>
    public void AddBoardDetailLog(string boardOwnerId, string text)
    {
        var observer = GetOpponent(boardOwnerId);
        GameLog.Add(new LogEntry
        {
            Text = text,
            DetailBoardOwnerId = boardOwnerId,
            DetailObserverId = HasOperationalMast(observer) ? observer.DiscordId : null,
        });
    }

    private static bool HasOperationalMast(BattleshipPlayer player) =>
        player != null && player.Fleet.Any(ship =>
            !ship.IsDestroyed &&
            !ship.Statuses.Any(status => status is
                ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) &&
            ship.Weapons.Any(weapon =>
                weapon.Type == WeaponType.Mast &&
                weapon.ShipId == ship.Id &&
                !weapon.PreservedModuleDestroyed &&
                ship.Decks.Any(deck =>
                    deck.Index == weapon.DeckIndex &&
                    !deck.IsDestroyed &&
                    !deck.ModuleDestroyed)));
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
/// Game log entry. VisibleTo restricts a personal entry to one player. DetailBoardOwnerId marks
/// exact physical-board intelligence; DetailObserverId snapshots the opposing Mast-authorized
/// viewer at event time. Entries with neither visibility mode are public; spectators see all.
/// </summary>
public class LogEntry
{
    public string Text { get; set; }
    public string VisibleTo { get; set; }
    public string DetailBoardOwnerId { get; set; }
    public string DetailObserverId { get; set; }
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
    /// <summary>Unused ordinary summon uses that must be deployed before Boarding can resume.</summary>
    public int MandatoryBoardingSummonSlots { get; set; }
    /// <summary>A purchased, unused Brander that must be deployed during Boarding.</summary>
    public bool MandatoryBoardingBrander { get; set; }
    /// <summary>Guards the one-time conversion of unused summon entitlements into mandatory Boarding actions.</summary>
    public bool BoardingSummonsPrepared { get; set; }
    /// <summary>Successful mandatory Boarding deployments remaining before excess units are discarded.</summary>
    public int BoardingDeploymentCapacity { get; set; }
    public List<Summon> Summons { get; set; } = new();
    public Weapon SelectedWeapon { get; set; }
    public ShotType SelectedShotType { get; set; } = ShotType.Ballista;
    /// <summary>v2 pools every operational Tetracatapult by projectile type. Default for new games.</summary>
    public bool UseSharedTetracatapultAmmo { get; set; } = true;
    /// <summary>Boat mode gives every newly deployed summon one ghost shot before materializing.</summary>
    public bool UseGhostSummons { get; set; } = true;
    /// <summary>Current v2 ammunition; live operational modules determine and clamp its maximum.</summary>
    public Dictionary<ShotType, int> SharedTetracatapultAmmo { get; set; } = new();
    public int RevealedCellCount { get; set; } // Per-player revealed cells (max 100)
    /// <summary>Every valid shot action fired by this player, independent of weapon and turn resets.</summary>
    public int TotalShotsFired { get; set; }
    /// <summary>Valid shot actions fired since this player's current turn began.</summary>
    public int ShotsFiredThisTurn { get; set; }
    public int StunShotExpiry { get; set; } = -1; // Shot# when stun expires (-1=none)
    public bool HasPenalty { get; set; } // Skip next turn
    public int LastSummonDeployShotCount { get; set; } = -10; // For 2-shot cooldown
    public bool HasShotThisTurn { get; set; } // For manual move before-shot restriction
    public List<PendingSummonDeploy> PendingSummons { get; set; } = new(); // Delayed summon abilities (pirate/cursed boat death, boarding)
    /// <summary>Mandatory in-place replacement created when a Russian Matryoshka stage is destroyed.</summary>
    public PendingMatryoshkaReplacement PendingMatryoshka { get; set; }
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
    /// <summary>Every distinct summon that has visited this physical cell, including the spawn cell.</summary>
    public List<SummonMarker> SummonTrails { get; set; } = new();
    /// <summary>Persistent identity-preserving markers for summons destroyed on this physical cell.</summary>
    public List<SummonMarker> SummonDeaths { get; set; } = new();
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
    /// <summary>
    /// Identity of the deck last authoritatively observed in this cell. It lets a later Scout
    /// observation move only that deck's white/red marker without revealing the rest of a hidden hull.
    /// </summary>
    public string KnownShipId { get; set; }
    public int KnownDeckIndex { get; set; } = -1;
    /// <summary>
    /// Explicit historical wreck occupying this physical cell after its source hull has been
    /// removed. This must not be inferred from the current <see cref="ShipRef"/>.
    /// </summary>
    public CellWreckState Wreck { get; set; }
    public bool IsMatryoshkaWreck => Wreck?.Kind == CellWreckKind.Matryoshka;
}

public enum CellWreckKind
{
    None,
    Matryoshka,
}

public class CellWreckState
{
    public CellWreckKind Kind { get; set; }
    public string SourceShipId { get; set; }
    public string SourceShipName { get; set; }
    public int SourceDeckIndex { get; set; } = -1;
}

public class SummonMarker
{
    public string SummonId { get; set; }
    public SummonType Type { get; set; }
    public bool IsBoardingShip { get; set; }
    public string SourceShipName { get; set; }
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
    /// <summary>Idempotency guard for the one replacement spawned by a destroyed Matryoshka stage.</summary>
    public bool MatryoshkaReplacementQueued { get; set; }
    /// <summary>
    /// Reasons this physical hull may no longer unfold into another Matryoshka stage. Flags are
    /// retained independently because restoration clears Devastated but cannot repair a deck
    /// that was structurally removed.
    /// </summary>
    public MatryoshkaReplacementSuppression MatryoshkaReplacementSuppressionReasons { get; set; }
    public bool MatryoshkaReplacementSuppressed =>
        MatryoshkaReplacementSuppressionReasons != MatryoshkaReplacementSuppression.None;
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
    /// Resolve one physical deck from the bow anchor. Definitions may provide arbitrary
    /// per-deck offsets; legacy straight and diagonal hulls retain their index-based shapes.
    /// </summary>
    public (int row, int col) GetDeckCell(
        Deck deck,
        int row,
        int col,
        Orientation orientation)
    {
        var hasExplicitOffset = deck.OffsetRow.HasValue && deck.OffsetCol.HasValue;
        var baseRowOffset = hasExplicitOffset
            ? deck.OffsetRow.Value
            : Abilities.Contains("diagonal_shape") ? deck.Index : 0;
        var baseColOffset = hasExplicitOffset ? deck.OffsetCol.Value : deck.Index;

        var (rowOffset, colOffset) = orientation switch
        {
            Orientation.Horizontal => (baseRowOffset, baseColOffset),
            Orientation.Vertical => (baseColOffset, -baseRowOffset),
            Orientation.HorizontalReverse => (-baseRowOffset, -baseColOffset),
            Orientation.VerticalReverse => (-baseColOffset, baseRowOffset),
            _ => (baseRowOffset, baseColOffset),
        };

        return (row + rowOffset, col + colOffset);
    }
}

[Flags]
public enum MatryoshkaReplacementSuppression
{
    None = 0,
    Capture = 1 << 0,
    Devastated = 1 << 1,
    StructuralDeckRemoval = 1 << 2,
}

public class Deck
{
    public int Index { get; set; }
    /// <summary>
    /// Hull-local position when the ship faces right. Null preserves the legacy shape derived
    /// from Deck.Index, which keeps already-created and deserialized ships compatible.
    /// </summary>
    public int? OffsetRow { get; set; }
    public int? OffsetCol { get; set; }
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
    /// <summary>Restorable ammo capacity, including permanent Boarding ammo bonuses.</summary>
    public int MaxAmmo { get; set; } = -1;
    public int AimSpeed { get; set; }
    public string ShipId { get; set; }
    public int DeckIndex { get; set; }
    /// <summary>
    /// A Cozy Joint replacement deck can retain weapons from two former modules. This flag
    /// preserves one source module's disabled state independently of the shared physical deck;
    /// ordinary weapons leave it false and continue to follow Deck.ModuleDestroyed.
    /// </summary>
    public bool PreservedModuleDestroyed { get; set; }
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
    public List<(int row, int col)> ScoutRevealData { get; set; } = new(); // Deferred reveal for Scouts and boarding ships
    public List<int> AllowedColumns { get; set; } // Pirate/CursedPirate column restriction
    public bool WaitingForTurnBack { get; set; } // Summon at edge, waiting to be re-sent
    public bool WaitingForDirectionChoice { get; set; } // CursedBoat waiting for owner to choose direction after collision
    public bool IsBoardingShip { get; set; } // Close ship converted during Final Boarding
    public string SourceShipId { get; set; } // Original Close ship for boarding Ballista VFX
    public string SourceShipName { get; set; } // Player-facing identity of a converted Close ship
    public int SourceShipDeckCount { get; set; } // Preserves the converted hull silhouette for board rendering
    /// <summary>
    /// Independent combat snapshot of a converted ship's decks. Current/max HP, module state and
    /// hull-local offsets continue to change on the boarding unit without aliasing the source ship.
    /// Empty keeps deserialized legacy summons on their original one-cell/one-hit behavior.
    /// </summary>
    public List<Deck> BoardingDecks { get; set; } = new();
    /// <summary>Source traits retained by a converted boarding hull.</summary>
    public List<string> BoardingAbilities { get; set; } = new();
    /// <summary>Source statuses retained by a converted boarding hull.</summary>
    public List<ShipStatusType> BoardingStatuses { get; set; } = new();
    public bool HasDetonated { get; set; } // Brander chain-explosion idempotency guard
    /// <summary>Ghost boats can be shot, but ignore hazards/collisions and do not move for their first shot.</summary>
    public bool IsGhost { get; set; }
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
    /// <summary>Must resolve before Boarding combat resumes; does not turn an ordinary summon into a boarding ship.</summary>
    public bool IsMandatoryBoarding { get; set; }
    public int Speed { get; set; } = 1;
    public int CollisionDamage { get; set; }
    public int RevealRadius { get; set; } = 1; // From original ship's Space
    public string SourceShipName { get; set; } // For log messages
    public string SourceShipId { get; set; } // Original Close ship for boarding Ballista VFX
    public int SourceShipDeckCount { get; set; } // Converted hull silhouette
    /// <summary>Immutable-at-conversion deck snapshot copied again when the pending hull deploys.</summary>
    public List<Deck> BoardingDecks { get; set; } = new();
    public List<string> BoardingAbilities { get; set; } = new();
    public List<ShipStatusType> BoardingStatuses { get; set; } = new();
}

public class ManualMoveOption
{
    public Direction Direction { get; set; }
    public int Distance { get; set; }
    /// <summary>New first-deck anchor selected by clicking the highlighted board cell.</summary>
    public int Row { get; set; }
    public int Col { get; set; }
}

/// <summary>
/// A destroyed Matryoshka stage leaves two legal, overlapping placements for the next
/// one-deck-shorter hull. Row/Col is the option's unique server-authoritative click target;
/// Cells[0] is the placement anchor for the preserved orientation.
/// </summary>
public class MatryoshkaPlacementOption
{
    public int Row { get; set; }
    public int Col { get; set; }
    public Orientation Orientation { get; set; }
    public List<(int row, int col)> Cells { get; set; } = new();
}

public class PendingMatryoshkaReplacement
{
    public string ParentShipId { get; set; }
    public string ChildName { get; set; }
    public int ChildDeckCount { get; set; }
    public List<MatryoshkaPlacementOption> Options { get; set; } = new();
}

public enum SummonMovementPhase
{
    PreparePlayer,
    MaterializeGhosts,
    MoveSummons,
    CleanupPlayer,
    ResolvePoison,
    Complete,
}

/// <summary>
/// Stable cursor for one call chain of BattleshipGameEngine.MoveSummons. IDs snapshot the same
/// iteration order as the former nested foreach loops while allowing the resolution to yield.
/// </summary>
public class SummonMovementResolutionState
{
    public SummonMovementPhase Phase { get; set; } = SummonMovementPhase.PreparePlayer;
    public List<string> PlayerIds { get; set; } = new();
    public int PlayerIndex { get; set; }
    public List<string> JustMaterializedSummonIds { get; set; } = new();
    public int MaterializeIndex { get; set; }
    public List<string> MovingSummonIds { get; set; } = new();
    public int MovingSummonIndex { get; set; }
    public int MovingStepIndex { get; set; }
    public PoisonResolutionState PoisonState { get; set; }
}

public enum PoisonResolutionPhase
{
    NormalShipCells,
    BoardingSources,
    Complete,
}

public class PoisonResolutionState
{
    public PoisonResolutionPhase Phase { get; set; } = PoisonResolutionPhase.NormalShipCells;
    public List<NormalPoisonCellWorkItem> NormalCells { get; set; } = new();
    public int NormalCellIndex { get; set; }
    public string CurrentNormalSourceKey { get; set; }
    public bool CurrentNormalSourceActive { get; set; }
    public bool BoardingSourcesInitialized { get; set; }
    public List<BoardingPoisonSourceWorkItem> BoardingSources { get; set; } = new();
    public int BoardingSourceIndex { get; set; }
    public bool CurrentSourceInitialized { get; set; }
    public List<string> CurrentPoisonedShipIds { get; set; } = new();
    public int CurrentPoisonedShipIndex { get; set; }
    public List<(int row, int col)> CurrentBoardingConeCells { get; set; } = new();
    public bool CurrentSummonTargetsInitialized { get; set; }
    public List<string> CurrentPoisonedSummonIds { get; set; } = new();
    public int CurrentPoisonedSummonIndex { get; set; }
}

public class NormalPoisonCellWorkItem
{
    public string BoardOwnerId { get; set; }
    public string SourceShipId { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
}

public class BoardingPoisonSourceWorkItem
{
    public string BoardOwnerId { get; set; }
    public string SourceOwnerId { get; set; }
    public string SourceSummonId { get; set; }
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
    public string SkippedPlayerId { get; set; }
    /// <summary>"Penalty" or "Stun" for an automatic turn-skip presentation.</summary>
    public string SkipReason { get; set; }
    public bool Hit { get; set; }
    public bool Miss { get; set; }
    public bool Scratched { get; set; }
    public bool Destroyed { get; set; }
    public bool Burned { get; set; }
    public bool Dodged { get; set; }
    /// <summary>The resolving action immediately applied a Penalty to its shooter.</summary>
    public bool PenaltyApplied { get; set; }
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
    /// <summary>Hull-local per-deck offsets for the Horizontal orientation.</summary>
    public List<DeckOffset> DeckOffsets { get; set; } = new();
    public int Speed { get; set; } = 1;
    public bool IsFree { get; set; }
    public bool IsHome { get; set; } // "Домашний" unit
    public string Description { get; set; }
    public List<UpgradeDefinition> AvailableUpgrades { get; set; } = new();
}

public class DeckOffset
{
    public int Row { get; set; }
    public int Col { get; set; }
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
    public bool IsPreinstalled { get; set; }
}
