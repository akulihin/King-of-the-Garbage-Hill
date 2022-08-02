using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Tolya
{
    public class TolyaCountClass
    {
        public int Cooldown;
        public bool IsReadyToUse = false;
        public List<TolyaCountSubClass> TargetList = new();

        public TolyaCountClass( int cooldown)
        {
            Cooldown = cooldown;
        }
    }

    public class TolyaCountSubClass
    {
        public int RoundNumber;
        public Guid Target;

        public TolyaCountSubClass(Guid target, int roundNumber)
        {
            Target = target;
            RoundNumber = roundNumber;
        }
    }

    public class TolyaTalkedlClass
    {
        public List<Guid> PlayerHeTalkedAbout = new();
    }
}