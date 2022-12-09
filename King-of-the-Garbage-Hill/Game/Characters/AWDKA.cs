using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Awdka
{
    public class TrollingClass
    {
        public List<TrollingSubClass> EnemyList = new();
    }

    public class TrollingSubClass
    {
        public Guid EnemyId;
        public decimal Score;

        public TrollingSubClass(Guid enemyId, decimal score)
        {
            EnemyId = enemyId;
            Score = score;
        }
    }

    public class TeachToPlayHistory
    {
        public List<TeachToPlayHistoryListClass> History = new();
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
        public List<TryingSubClass> TryingList = new();
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