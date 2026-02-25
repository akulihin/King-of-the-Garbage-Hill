using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Salldorum
{
    public class ShenClass
    {
        public int Charges { get; set; } = 0;
        public int TargetPosition { get; set; } = -1;  // 1-6, -1 = not using
        public bool ActiveThisTurn { get; set; } = false;
    }

    public class TimeCapsuleClass
    {
        public bool Buried { get; set; } = false;
        public int BuriedAtPosition { get; set; } = -1;  // leaderboard position 1-6
        public int BuriedOnRound { get; set; } = -1;
        public bool FirstBlockUsed { get; set; } = false;
        public bool PickedUpThisTurn { get; set; } = false;
    }

    public class ChroniclerClass
    {
        public bool HistoryRewritten { get; set; } = false;
        public int RewrittenRound { get; set; } = -1;
        public List<int> PositionHistory { get; set; } = new(); // index=round-1, value=position
    }
}
