using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class FriendsClass
{
    public List<Guid> FriendList = new();

    public FriendsClass(Guid enemyPlayerId)
    {
        FriendList.Add(enemyPlayerId);
    }
    public FriendsClass()
    {
    }
}