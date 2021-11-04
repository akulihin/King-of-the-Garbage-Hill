using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Mitsuki
    {
        public class GarbageClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<GarbageSubClass> Training = new();

            public GarbageClass(Guid playerId, ulong gameId, Guid enemyId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Training.Add(new GarbageSubClass(enemyId));
            }
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
}