using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Darksci
{
    public class LuckyClass
    {
        public ulong GameId;
        public Guid PlayerId;
        public List<Guid> TouchedPlayers = new();
        public bool Triggered;

        public LuckyClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            Triggered = false;
        }
    }

    public class DarksciType
    {
        public ulong GameId;
        public Guid PlayerId;
        public bool  IsStableType;
        public bool Triggered;
        public bool Sent;

        public DarksciType(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            IsStableType = false;
            Triggered = false;
            Sent = false;
        }
    }
}