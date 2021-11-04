using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class HardKitty
    {
        public class DoebatsyaClass
        {
            public ulong GameId;
            public List<DoebatsyaSubClass> LostSeries = new();
            public Guid PlayerId;

            public DoebatsyaClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
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
            public ulong GameId;
            public Guid PlayerId;
            public List<Guid> UniquePlayers = new();

            public MuteClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }

        public class LonelinessClass
        {
            public bool Activated;
            public List<LonelinessSubClass> AttackHistory;
            public ulong GameId;
            public Guid PlayerId;

            public LonelinessClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Activated = false;
                AttackHistory = new List<LonelinessSubClass>();
            }
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
}