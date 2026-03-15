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
    public List<string> GameLog { get; set; } = new();
    public int ShotCount { get; set; } // Global shot counter (both players)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public HashSet<(int row, int col)> PoisonZones { get; set; } = new();

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
    public int SummonSlotsUsed { get; set; }
    public int MaxSummonSlots { get; set; } = 4;
    public List<Summon> Summons { get; set; } = new();
    public Weapon SelectedWeapon { get; set; }
    public ShotType SelectedShotType { get; set; } = ShotType.Ballista;
    public int RevealedCellCount { get; set; } // Per-player revealed cells (max 100)
    public int StunShotExpiry { get; set; } = -1; // Shot# when stun expires (-1=none)
    public bool HasPenalty { get; set; } // Skip next turn
    public int LastSummonDeployShotCount { get; set; } = -10; // For 2-shot cooldown
    public bool ManeuveringDoubleUsed { get; set; } // One-time manual move
    public bool HasShotThisTurn { get; set; } // For manual move before-shot restriction
    public List<PendingSummonDeploy> PendingSummons { get; set; } = new(); // Delayed summon abilities (pirate/cursed boat death, boarding)
    public double DamageMultiplier { get; set; } = 1.0; // Modifiable damage multiplier (GDD: "множитель")
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
    public bool SummonTrail { get; set; } // Enemy summon passed through this cell
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

    public List<(int row, int col)> GetOccupiedCells()
    {
        var cells = new List<(int, int)>();
        for (var i = 0; i < Decks.Count; i++)
        {
            var r = Orientation == Orientation.Vertical ? Row + i : Row;
            var c = Orientation == Orientation.Horizontal ? Col + i : Col;
            cells.Add((r, c));
        }
        return cells;
    }
}

public class Deck
{
    public int Index { get; set; }
    public int MaxHp { get; set; } = 2;
    public int CurrentHp { get; set; } = 2;
    public bool IsDestroyed => CurrentHp <= 0;
    public string Module { get; set; } // e.g. "ballista", "catapult", "mast", "boiler"
    public bool ModuleDestroyed { get; set; }
}

public class Weapon
{
    public WeaponType Type { get; set; }
    public int Ammo { get; set; } = -1; // -1 = unlimited
    public int AimSpeed { get; set; }
    public int Damage { get; set; } = 2;
    public string ShipId { get; set; }

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
    public int Damage { get; set; }
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
    public int Damage { get; set; }
    public int RevealRadius { get; set; } = 1; // From original ship's Space
    public string SourceShipName { get; set; } // For log messages
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
    public bool Hit { get; set; }
    public bool Miss { get; set; }
    public bool Scratched { get; set; }
    public bool Destroyed { get; set; }
    public bool Burned { get; set; }
    public bool ShipSunk { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public bool TurnContinues { get; set; }
    public string Message { get; set; }
    public string AffectedShipName { get; set; }
    public List<(int row, int col)> AoECells { get; set; } = new();
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
    public Faction Faction { get; set; } = Faction.Empire;
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
    public int Damage { get; set; } = 2;
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
