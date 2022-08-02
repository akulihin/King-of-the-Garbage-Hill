using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Octopus
{
    public class InvulnerabilityClass
    {
        public int Count = 0;
    }

    public class InkClass
    {
        public List<InkSubClass> RealScoreList = new();
    }

    public class InkSubClass
    {
        public Guid PlayerId;
        public int RealScore;

        public InkSubClass(Guid playerDiscordId, int roundNo, int realScore)
        {
            if (roundNo <= 4)
                realScore *= 1; // Why????????????????????????
            else if (roundNo <= 9)
                realScore *= 2;
            else if (roundNo == 10)
                realScore *= 4;

            PlayerId = playerDiscordId;
            RealScore = realScore;
        }

        public void AddRealScore(int roundNo, int realScore = 1)
        {
            if (roundNo <= 4)
                realScore *= 1; // Why????????????????????????
            else if (roundNo <= 9)
                realScore *= 2;
            else if (roundNo == 10)
                realScore *= 4;

            RealScore += realScore;
        }
    }

    public class TentaclesClass
    {
        public List<int> LeaderboardPlace = new();
    }
}