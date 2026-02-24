using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class GoblinSwarm
{
    public class GoblinPopulationClass
    {
        public int TotalGoblins { get; set; } = 20;

        // Rates (every Nth goblin is this type) - modified by level-up upgrades
        public int HobRate { get; set; } = 15;      // default every 15th
        public int WarriorRate { get; set; } = 6;    // default every 6th
        public int WorkerRate { get; set; } = 10;    // default every 10th

        // Upgrade levels (0-3 for progression tracking)
        public int HobUpgradeLevel { get; set; } = 0;
        public int WarriorUpgradeLevel { get; set; } = 0;
        public int WorkerUpgradeLevel { get; set; } = 0;

        // Ziggurats built (each costs 1 worker from the count)
        public int ZigguratWorkerDeductions { get; set; } = 0;

        // Computed type counts
        public int Warriors => TotalGoblins / WarriorRate;
        public int Hobs => TotalGoblins / HobRate;
        public int Workers => Math.Max(0, TotalGoblins / WorkerRate - ZigguratWorkerDeductions);

        // Whether Праздник Гоблинов has been used (one-time only)
        public bool FestivalUsed { get; set; } = false;

        // Growth for current round (base 1 + hobs, doubled on attack win)
        public int GrowthThisRound => 1 + Hobs;
    }

    public class ZigguratClass
    {
        public List<int> BuiltPositions { get; set; } = new();       // Leaderboard places with ziggurats
        public List<string> LearnedPassives { get; set; } = new();   // Passive names learned
        public bool WantsToBuild { get; set; } = false;               // Block was used this round — build pending
        public bool IsInZiggurat { get; set; } = false;              // Currently on a ziggurat position
        public int ZigguratStayRoundsLeft { get; set; } = 0;        // Rounds of position lock remaining
    }
}
