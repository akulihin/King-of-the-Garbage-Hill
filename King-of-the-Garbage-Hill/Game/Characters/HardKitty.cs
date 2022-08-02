using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class HardKitty
{
    public class DoebatsyaClass
    {
        public List<DoebatsyaSubClass> LostSeries = new();
    }

    public class DoebatsyaSubClass
    {
        public Guid EnemyPlayerId;
        public int Series;

        public DoebatsyaSubClass(Guid enemyPlayerId)
        {
            EnemyPlayerId = enemyPlayerId;
            Series = 1;
        }
    }


    public class MuteClass
    {
        public List<Guid> UniquePlayers = new();
    }

    public class LonelinessClass
    {
        public bool Activated = false;
        public List<LonelinessSubClass> AttackHistory = new();

    }

    public class LonelinessSubClass
    {
        public Guid EnemyId;
        public int Times;

        public LonelinessSubClass(Guid enemyId)
        {
            EnemyId = enemyId;
            Times = 0;
        }
    }
}