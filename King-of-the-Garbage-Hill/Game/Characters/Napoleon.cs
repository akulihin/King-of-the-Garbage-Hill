using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Napoleon
{
    public class AllianceClass
    {
        public Guid AllyId = Guid.Empty;
    }

    public class PeaceTreatyClass
    {
        public List<Guid> TreatyEnemies = new();
    }
}
