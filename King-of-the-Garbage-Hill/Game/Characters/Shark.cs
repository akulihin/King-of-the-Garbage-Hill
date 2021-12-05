using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Shark
{
    public class SharkLeaderClass
    {
        public List<int> FriendList = new();

        public SharkLeaderClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
        }

        public ulong GameId { get; set; }
        public Guid PlayerId { get; set; }
    }

    public class SharkDontUnderstand
    {
        public SharkDontUnderstand(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            EnemyId = Guid.Empty;
            IntelligenceToReturn = 0;
        }

        public ulong GameId { get; set; }
        public Guid PlayerId { get; set; }
        public Guid EnemyId { get; set; }
        public int IntelligenceToReturn { get; set; }
    }
}