using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class WhenToTriggerClass
{
    public ulong GameId;
    public Guid PlayerId;
    public List<int> WhenToTrigger;

    public WhenToTriggerClass(Guid playerId, ulong gameId)
    {
        PlayerId = playerId;
        WhenToTrigger = new List<int>();
        GameId = gameId;
    }

    public WhenToTriggerClass(Guid playerId, ulong gameId, int when)
    {
        PlayerId = playerId;
        WhenToTrigger = new List<int> { when };
        GameId = gameId;
    }
}