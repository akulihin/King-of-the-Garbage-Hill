using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class TheBoys
{
    public class FrancieClass
    {
        public int ChemWeaponLevel { get; set; } = 0;
        public Guid OrderTarget { get; set; } = Guid.Empty;
        public int OrderRoundsLeft { get; set; } = 0;
        public int OrdersCompleted { get; set; } = 0;
        public int OrdersFailed { get; set; } = 0;
        public List<Guid> OrderHistory { get; set; } = new();
        public List<Guid> RemainingTargets { get; set; } = new();
    }

    public class ButcherClass
    {
        public int PokerCount { get; set; } = 0;
    }

    public class KimikoClass
    {
        public int RegenLevel { get; set; } = 0;
        public bool DisabledNextRound { get; set; } = false;
        public bool IsDisabled { get; set; } = false;
        public int TotalJusticeBlocked { get; set; } = 0;
    }

    public class MMClass
    {
        public bool NextAttackGathersKompromat { get; set; } = false;
        public List<Guid> KompromatTargets { get; set; } = new();
        public Dictionary<Guid, string> KompromatHints { get; set; } = new();
    }
}
