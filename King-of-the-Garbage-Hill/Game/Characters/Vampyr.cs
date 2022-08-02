using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Vampyr
{
    public class ScavengerClass
    {
        public Guid EnemyId = Guid.Empty;
        public int EnemyJustice = 0;
    }


    public class HematophagiaClass
    {
        public List<HematophagiaSubClass> Hematophagia = new();
    }

    public class HematophagiaSubClass
    {
        public Guid EnemyId;
        public int StatIndex;


        public HematophagiaSubClass(int statIndex, Guid enemyId)
        {
            StatIndex = statIndex;
            EnemyId = enemyId;
        }
    }
}