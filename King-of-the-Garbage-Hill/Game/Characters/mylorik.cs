using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Mylorik
    {
        public class MylorikRevengeClass
        {
            public List<MylorikRevengeClassSub> EnemyListPlayerIds;
            public ulong GameId;
            public Guid PlayerId;

            public MylorikRevengeClass(Guid playerId, ulong gameId, Guid firstLost, int roundNumber)
            {
                PlayerId = playerId;
                EnemyListPlayerIds = new List<MylorikRevengeClassSub> {new(firstLost, roundNumber)};
                GameId = gameId;
            }
        }

        public class MylorikRevengeClassSub
        {
            public Guid EnemyPlayerId;
            public bool IsUnique;
            public int RoundNumber;

            public MylorikRevengeClassSub(Guid enemyPlayerId, int roundNumber)
            {
                EnemyPlayerId = enemyPlayerId;
                RoundNumber = roundNumber;
                IsUnique = true;
            }
        }

        public class MylorikSpartanClass
        {
            public List<MylorikSpartanSubClass> Enemies;
            public ulong GameId;
            public Guid PlayerId;

            public MylorikSpartanClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Enemies = new List<MylorikSpartanSubClass>();
            }
        }

        public class MylorikSpartanSubClass
        {
            public Guid EnemyId;
            public int LostTimes;


            public MylorikSpartanSubClass(Guid enemyId)
            {
                EnemyId = enemyId;
                LostTimes = 0;
            }
        }

        public class MylorikSpanishClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public int Times;

            public MylorikSpanishClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Times = 0;
            }
        }
    }
}