using System;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class ToxicMate
{
    public class CancerClass
    {
        public bool IsActive { get; set; }
        public Guid CurrentHolder { get; set; } = Guid.Empty;
        public int TransferCount { get; set; }
        public bool FirstLossTriggered { get; set; }
        public bool TransferredThisRound { get; set; }
    }
}
