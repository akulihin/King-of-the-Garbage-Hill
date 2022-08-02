using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Mylorik
{
    public class MylorikRevengeClass
    {
        public List<MylorikRevengeClassSub> EnemyListPlayerIds = new();
    }

    public class MylorikRevengeClassSub
    {
        public Guid EnemyPlayerId;
        public bool IsUnique;
        public int RoundNumber;

        public MylorikRevengeClassSub(Guid enemyPlayerId, int roundNumber)
        {
            EnemyPlayerId = enemyPlayerId;
            RoundNumber = roundNumber;
            IsUnique = true;
        }
    }

    public class MylorikSpartanClass
    {
        public List<MylorikSpartanSubClass> Enemies = new();
    }

    public class MylorikSpartanSubClass
    {
        public Guid EnemyId;
        public int LostTimes;


        public MylorikSpartanSubClass(Guid enemyId)
        {
            EnemyId = enemyId;
            LostTimes = 0;
        }
    }

    public class MylorikSpanishClass
    {
        public int Times = 0;
    }

    public class MylorikBooleClass
    {
        public bool IsBoole = false;
    }

}