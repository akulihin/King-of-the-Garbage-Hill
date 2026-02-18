using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Saitama
{
    /// <summary>Tracks deferred points/moral from "Неприметность" so they can be restored on round 10.</summary>
    public class UnnoticedClass
    {
        /// <summary>Total points stolen by Неприметность across the game.</summary>
        public int DeferredPoints = 0;
        /// <summary>Total moral stolen by Неприметность across the game.</summary>
        public decimal DeferredMoral = 0;
        /// <summary>Top 2 player IDs by combat power — Saitama fights seriously against them.</summary>
        public List<Guid> SeriousTargets = new();
    }
}
