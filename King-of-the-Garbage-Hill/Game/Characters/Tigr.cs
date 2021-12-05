using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Tigr
{
    public class TigrTopClass
    {
        public ulong GameId;
        public Guid PlayerId;
        public int TimeCount;

        public TigrTopClass(Guid playerId, ulong gameId)
        {
            PlayerId = playerId;
            GameId = gameId;
            TimeCount = 3;
        }
    }

    public class ThreeZeroClass
    {
        public List<ThreeZeroSubClass> FriendList = new();
        public ulong GameId;
        public Guid PlayerId;

        public ThreeZeroClass(Guid playerId, ulong gameId, Guid enemyId)
        {
            PlayerId = playerId;
            GameId = gameId;
            FriendList.Add(new ThreeZeroSubClass(enemyId));
        }
    }

    public class ThreeZeroSubClass
    {
        public Guid EnemyPlayerId;
        public bool IsUnique;
        public int WinsSeries;

        public ThreeZeroSubClass(Guid enemyPlayerId)
        {
            EnemyPlayerId = enemyPlayerId;
            WinsSeries = 1;
            IsUnique = true;
        }
    }
}