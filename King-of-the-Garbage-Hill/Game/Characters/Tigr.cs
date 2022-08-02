using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Tigr
{
    public class TigrTopClass
    {
        public int TimeCount = 3;
    }

    public class ThreeZeroClass
    {
        public List<ThreeZeroSubClass> FriendList = new();

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