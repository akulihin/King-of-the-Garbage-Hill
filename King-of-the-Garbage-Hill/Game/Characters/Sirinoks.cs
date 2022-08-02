using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Sirinoks
{
    public class SirinoksFriendsClass
    {
        public Guid EnemyId = Guid.Empty;
        public bool IsBlock = false;
        public bool IsSkip = false;
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
        public List<TrainingSubClass> Training = new();
        public List<int> TriggeredBonusFromStat = new();
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