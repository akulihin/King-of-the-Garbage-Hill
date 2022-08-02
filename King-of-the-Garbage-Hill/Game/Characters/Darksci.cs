using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Darksci
{
    public class LuckyClass
    {
        public List<Guid> TouchedPlayers = new();
        public bool Triggered = false;
    }

    public class DarksciType
    {
        public bool  IsStableType = false;
        public bool Triggered = false;
        public bool Sent = false;

    }
}