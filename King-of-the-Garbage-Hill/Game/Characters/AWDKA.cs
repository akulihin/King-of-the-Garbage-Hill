using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Awdka
    {
        public class TrollingClass
        {
            public List<TrollingSubClass> EnemyList = new();

            public ulong GameId;
            public Guid PlayerId;

            public TrollingClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }

        public class TrollingSubClass
        {
            public Guid EnemyId;
            public int Score;

            public TrollingSubClass(Guid enemyId, int score)
            {
                EnemyId = enemyId;
                Score = score;
            }
        }

        public class TeachToPlayHistory
        {
            public ulong GameId;
            public List<TeachToPlayHistoryListClass> History = new();
            public Guid PlayerId;

            public TeachToPlayHistory(Guid playerId, ulong gameId)
            {
                GameId = gameId;
                PlayerId = playerId;
            }
        }

        public class TeachToPlayHistoryListClass
        {
            public Guid EnemyPlayerId;
            public int Stat;
            public string Text;

            public TeachToPlayHistoryListClass(Guid enemyPlayerId, string text, int stat)
            {
                EnemyPlayerId = enemyPlayerId;
                Text = text;
                Stat = stat;
            }
        }

        public class TryingClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<TryingSubClass> TryingList = new();

            public TryingClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }

        public class TryingSubClass
        {
            public Guid EnemyPlayerId;
            public bool IsUnique;
            public int Times;

            public TryingSubClass(Guid enemyPlayerId)
            {
                EnemyPlayerId = enemyPlayerId;
                Times = 1;
                IsUnique = false;
            }
        }
    }
}