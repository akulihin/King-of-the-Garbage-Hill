using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Tolya
{
    public class TolyaCountClass
    {
        public int Cooldown;
        public ulong GameId;
        public bool IsReadyToUse;
        public Guid PlayerId;
        public List<TolyaCountSubClass> TargetList = new();

        public TolyaCountClass(ulong gameId, Guid playerId, int cooldown)
        {
            GameId = gameId;
            PlayerId = playerId;
            IsReadyToUse = false;
            Cooldown = cooldown;
        }
    }

    public class TolyaCountSubClass
    {
        public int RoundNumber;
        public Guid Target;

        public TolyaCountSubClass(Guid target, int roundNumber)
        {
            Target = target;
            RoundNumber = roundNumber;
        }
    }

    public class TolyaTalkedlClass
    {
        public ulong GameId;
        public List<Guid> PlayerHeTalkedAbout = new();
        public Guid PlayerId;

        public TolyaTalkedlClass(ulong gameId, Guid playerId)
        {
            GameId = gameId;
            PlayerId = playerId;
        }
    }
}