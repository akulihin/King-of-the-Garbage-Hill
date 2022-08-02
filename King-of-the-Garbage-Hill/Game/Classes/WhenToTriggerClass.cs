using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class WhenToTriggerClass
{
    public List<int> WhenToTrigger;

    public WhenToTriggerClass()
    {
        WhenToTrigger = new List<int>();
    }

    public WhenToTriggerClass( int when)
    {
        WhenToTrigger = new List<int> { when };
    }
}