namespace King_of_the_Garbage_Hill.Battleship.Models;

public enum BsGamePhase
{
    Lobby,
    ArmySelection,
    FleetBuilding,
    ShipPlacement,
    Combat,
    Boarding,
    GameOver
}

/// <summary>
/// Publicly selectable Battleship bot generations. V1 preserves the original bot,
/// V2 uses honest probability tactics, and V3 adds fleet inference and adaptive play.
/// </summary>
public enum BattleshipBotVersion
{
    V1 = 1,
    V2 = 2,
    V3 = 3,
}

public enum RangeClass
{
    Close,
    CloseMelee,
    Mid,
    Tetra,
    Far
}

public enum ShotType
{
    Ballista,       // default: 2 damage
    Cannon,         // Captain Flint shared cannon pool: 3 damage
    Fortuna,        // Fortune flagship shared pool: 4 damage
    Warming,        // Fast Warming ship cannon: 2 damage, unlimited
    WhiteStone,     // 4x damage (=8), destroys module, stuns
    Buckshot,       // 0.5x damage (=1), 4-cell AoE
    Neptune,        // 1 damage, creates Electric Charge marks and a three-point Burn triangle
    Incendiary,     // burns entire ship
    GreekFire,      // Котельная upgrade: one-shot, kills summon without penalty, creates permanent burning cell
    EvilIncendiary, // Горючая баржа upgrade: also destroys the rest of a ship when aimed at its dead deck
    EvilGreekFire   // Котельная upgrade: Greek Fire that can be fired during the opponent's shot pause
}

public enum WeaponType
{
    Ballista,
    Cannon,
    Fortuna,
    Warming,
    Tetracatapult,
    Neptune,
    Mast,
    Boiler,
    Incendiary,
    GreekFire,
    EvilIncendiary,
    EvilGreekFire
}

public enum SummonType
{
    Ram,
    PirateBoat,
    Scout,
    Brander,
    CursedBoat,
    Parrot
}

public enum ShipStatusType
{
    Freeze,
    Devastated,
    Burn,
    Capture,
    BurnResist
}

public enum Faction
{
    Empire,
    Alliance,
    CaptainFlint
}

public enum Region
{
    South,    // Юг
    West,     // Запад
    North,    // Север
    East,     // Восток
    Tetracor  // Triple/Tetranavis — exempt from 3-region limit
}

public enum Orientation
{
    Horizontal,
    Vertical,
    HorizontalReverse,
    VerticalReverse
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}
