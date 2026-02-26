using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Itachi
{
    public class CrowsClass
    {
        // Per-enemy crow count: playerId -> number of crows
        public Dictionary<Guid, int> CrowCounts { get; set; } = new();
        // Set after level-up; next attack throws a crow
        public bool CrowReadyToThrow { get; set; } = false;
    }

    public class IzanagiClass
    {
        public int UsesRemaining { get; set; } = 2;
    }

    public class TsukuyomiClass
    {
        public int ChargeCounter { get; set; } = 0;
        public Guid TsukuyomiTargetThisRound { get; set; } = Guid.Empty;
        public Guid TsukuyomiActiveTarget { get; set; } = Guid.Empty;
        public decimal TotalStolenPoints { get; set; } = 0;
        public Dictionary<Guid, decimal> StolenFromPlayers { get; set; } = new();
    }
}
