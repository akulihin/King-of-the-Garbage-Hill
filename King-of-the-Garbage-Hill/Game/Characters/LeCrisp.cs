using System;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class LeCrisp
{
    public class LeCrispImpactClass
    {
        public ulong GameId;
        public int ImpactTimes;
        public bool IsLost;
        public Guid PlayerId;

        public LeCrispImpactClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            IsLost = false;
            ImpactTimes = 0;
        }
    }


    public class LeCrispAssassins
    {
        public int AdditionalPsycheCurrent;
        public int AdditionalPsycheForNextRound;
        public ulong GameId;
        public Guid PlayerId;

        public LeCrispAssassins(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            AdditionalPsycheCurrent = 0;
            AdditionalPsycheForNextRound = 0;
        }
    }
}