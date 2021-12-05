using System;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Gleb
{
    public class GlebSkipClass
    {
        public ulong GameId;
        public Guid PlayerId;

        public GlebSkipClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
        }
    }

    public class GlebTeaClass
    {
        public ulong GameId;
        public Guid PlayerId;
        public bool Ready = false;

        public GlebTeaClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
        }
    }
}