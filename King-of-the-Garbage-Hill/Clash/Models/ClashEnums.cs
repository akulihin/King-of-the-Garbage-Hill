namespace King_of_the_Garbage_Hill.Clash.Models;

public enum ClashSide
{
    Host,
    Guest,
}

public enum ClashAttackPattern
{
    AdjacentForward,
    ForwardReach,
    RangedColumn,
}

public enum ClashGamePhase
{
    Lobby,
    InitialFrontPlacement,
    GuestSecondRowPlacement,
    HostSecondRowPlacement,
    GuestThirdRowPlacement,
    HostThirdRowPlacement,
    ResolvingClash,
    GuestReinforcement,
    HostReinforcement,
    ActiveExchange,
    Finished,
}

public enum ClashTerminalReason
{
    None,
    Breach,
    DualBreach,
    Elimination,
    MutualElimination,
    Forfeit,
    Leave,
}

public enum ClashResolutionEventType
{
    ClashStart,
    Wait,
    Reload,
    Attack,
    RangedAttack,
    Block,
    Dodge,
    Damage,
    BleedApplied,
    BleedDamage,
    Death,
    Advance,
    Passive,
    ClashEnd,
}

public enum ClashBotActionKind
{
    None,
    PlaceUnit,
    ConfirmPlacement,
    PlaceReinforcement,
    Continue,
}
