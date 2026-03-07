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
    WhiteStone,     // 4x damage (=8), destroys module, stuns
    Buckshot,       // 0.5x damage (=1), 4-cell AoE
    Incendiary,     // burns entire ship
    Catapult,       // standard catapult
    Tetracatapult,  // from Triple ships
    GreekFire       // Котельная upgrade: one-shot, kills summon without penalty, creates permanent burning cell
}

public enum WeaponType
{
    Ballista,
    Catapult,
    Tetracatapult,
    Mast,
    Boiler,
    Incendiary,
    GreekFire
}

public enum SummonType
{
    Ram,
    PirateBoat,
    Scout,
    Brander,
    CursedBoat
}

public enum ShipStatusType
{
    None,
    Stun,
    Penalty,
    Freeze,
    Devastated,
    Burn,
    Capture,
    Destroy,
    BurnResist
}

public enum Faction
{
    Empire
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
    Vertical
}

public enum CellState
{
    Unknown,
    Empty,
    Miss,
    Hit,
    Burning,
    Ship,
    Summon
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}
