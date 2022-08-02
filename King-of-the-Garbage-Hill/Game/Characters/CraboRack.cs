using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class CraboRack
    {

        public class Shell
        {
            public List<Guid> FriendList = new();
            public Guid CurrentAttacker { get; set; } = Guid.Empty;
            
        }

        public class BokoBoole
        {
            public Guid CurrentAttacker { get; set; } = Guid.Empty;
        }
    }
}
