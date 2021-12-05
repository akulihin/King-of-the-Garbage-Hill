using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Sirinoks
{
    public class SirinoksFriendsClass
    {
        public Guid EnemyId;
        public ulong GameId;
        public bool IsBlock;
        public bool IsSkip;
        public Guid PlayerId;

        public SirinoksFriendsClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            EnemyId = Guid.Empty;
            IsBlock = false;
            IsSkip = false;
        }
    }


    public class StatsClass
    {
        public int Index;
        public int Number;

        public StatsClass(int index, int number)
        {
            Index = index;
            Number = number;
        }
    }

    public class TrainingClass
    {
        public Guid EnemyId;
        public ulong GameId;
        public Guid PlayerId;
        public List<TrainingSubClass> Training = new();
        public List<int> TriggeredBonusFromStat = new();

        public TrainingClass(Guid playerId, ulong gameId, int index, int number, Guid enemyId)
        {
            PlayerId = playerId;
            GameId = gameId;
            EnemyId = enemyId;
            Training.Add(new TrainingSubClass(index, number));
        }
    }

    public class TrainingSubClass
    {
        public int StatIndex;
        public int StatNumber;


        public TrainingSubClass(int statIndex, int statNumber)
        {
            StatIndex = statIndex;
            StatNumber = statNumber;
        }
    }
}