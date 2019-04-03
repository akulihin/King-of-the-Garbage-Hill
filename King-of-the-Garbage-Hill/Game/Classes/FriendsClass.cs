
using System;
using System.Collections.Generic;


namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class FriendsClass
    {
        public ulong GameId { get; set; }
        public Guid PlayerId { get; set; }
        public List<Guid> FriendList = new List<Guid>();

        public FriendsClass(Guid playerId, ulong gameId, Guid enemyPlayerId)
        {
            PlayerId = playerId;
            GameId = gameId;
            FriendList.Add(enemyPlayerId);
        }
        public FriendsClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
        }
    }
}
