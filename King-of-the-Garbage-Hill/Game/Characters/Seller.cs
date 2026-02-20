using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Seller
{
    public class VparitGovnaClass
    {
        public int Cooldown { get; set; } = 0;
        public List<Guid> MarkedPlayers { get; set; } = new();
    }

    public class SecretBuildClass
    {
        public decimal AccumulatedSkill { get; set; } = 0;
    }
}
