using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Spartan
{

    public class TheyWontLikeIt
    {
        public List<Guid> FriendList = new();
        public Guid BlockedPlayer { get; set; } = Guid.Empty;
    }
}