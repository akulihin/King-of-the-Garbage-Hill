using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CraboRack
    {

        public class Shell
        {
            public List<Guid> FriendList = new();

            public Shell(Guid playerId, ulong gameId, Guid enemyPlayerId)
            {
                PlayerId = playerId;
                GameId = gameId;
                FriendList.Add(enemyPlayerId);
                CurrentAttacker = Guid.Empty;
            }

            public Shell(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                CurrentAttacker = Guid.Empty;
            }

            public ulong GameId { get; set; }
            public Guid PlayerId { get; set; }
            public Guid CurrentAttacker { get; set; }
        }

        public class BakoBoole
        {
            public BakoBoole(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                CurrentAttacker = Guid.Empty;
            }

            public ulong GameId { get; set; }
            public Guid PlayerId { get; set; }
            public Guid CurrentAttacker { get; set; }
        }
    }
}
