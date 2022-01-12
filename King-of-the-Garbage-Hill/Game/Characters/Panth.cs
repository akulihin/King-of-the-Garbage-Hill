using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Spartan
{

    public class TheyWontLikeIt
    {
        public List<Guid> FriendList = new();

        public TheyWontLikeIt(Guid playerId, ulong gameId, Guid enemyPlayerId)
        {
            PlayerId = playerId;
            GameId = gameId;
            FriendList.Add(enemyPlayerId);
            BlockedPlayer = Guid.Empty;
        }

        public TheyWontLikeIt(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            BlockedPlayer = Guid.Empty;
        }

        public ulong GameId { get; set; }
        public Guid PlayerId { get; set; }
        public Guid BlockedPlayer { get; set; }
    }
}