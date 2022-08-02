using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Mitsuki
{
    public class GarbageClass
    {
        public List<GarbageSubClass> Training = new();
    }

    public class GarbageSubClass
    {
        public Guid EnemyId;
        public int Times;

        public GarbageSubClass(Guid enemyId)
        {
            EnemyId = enemyId;
            Times = 1;
        }
    }
}