using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
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
    }
}