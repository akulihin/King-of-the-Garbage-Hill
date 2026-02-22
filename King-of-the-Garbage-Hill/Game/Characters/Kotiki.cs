using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Kotiki
{
    public class StormClass
    {
        public List<Guid> TauntedPlayers { get; set; } = new();     // Already taunted (once per enemy per game)
        public Guid CurrentTauntTarget { get; set; } = Guid.Empty;  // Who was taunted this round
    }

    public class AmbushClass
    {
        public Guid MinkaOnPlayer { get; set; } = Guid.Empty;   // Enemy carrying Minka cat
        public Guid StormOnPlayer { get; set; } = Guid.Empty;   // Enemy carrying Storm cat
        public int MinkaCooldown { get; set; } = 0;             // Rounds until Minka can deploy again
        public int StormCooldown { get; set; } = 0;             // Rounds until Storm can deploy again
        public int MinkaRoundsOnEnemy { get; set; } = 0;        // Rounds Minka has been on enemy
    }
}
