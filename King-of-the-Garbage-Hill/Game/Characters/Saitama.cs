using System;
using System.Collections.Generic;
using System.Linq;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Saitama
{
    /// <summary>Tracks deferred points/moral from "Неприметность" so they can be restored on round 10.</summary>
    public class UnnoticedClass
    {
        /// <summary>
        /// Per-recipient ledger of points Saitama lost to "Неприметность", already scaled by the round
        /// multiplier at deferral time (like <see cref="Octopus.InkSubClass"/>). On round 10 these are
        /// reclaimed: given to Saitama and taken back from each recipient.
        /// </summary>
        public List<DeferredEntry> Ledger = new();

        /// <summary>Total moral stolen by Неприметность across the game (raw, not round-multiplied — it was Saitama's own moral).</summary>
        public decimal DeferredMoral = 0;

        /// <summary>Top 2 player IDs by combat power — Saitama fights seriously against them.</summary>
        public List<Guid> SeriousTargets = new();

        /// <summary>
        /// Bank a deferred win for <paramref name="recipientId"/>, scaling the point by the round
        /// multiplier (×1 rounds 1-4, ×2 rounds 5-9, ×4 round 10). Deferral only runs rounds 1-9 in
        /// practice, so the value is 1 or 2.
        /// </summary>
        public void AddDeferred(Guid recipientId, int roundNo, int rawPoints = 1)
        {
            var points = rawPoints * RoundMultiplier(roundNo);
            var entry = Ledger.Find(x => x.RecipientId == recipientId);
            if (entry == null)
                Ledger.Add(new DeferredEntry { RecipientId = recipientId, Points = points });
            else
                entry.Points += points;
        }

        /// <summary>Sum of all deferred points across recipients — the amount restored to Saitama at reclaim.</summary>
        public int GetTotalDeferred()
        {
            return Ledger.Sum(x => x.Points);
        }

        private static int RoundMultiplier(int roundNo)
        {
            if (roundNo <= 4) return 1;
            if (roundNo <= 9) return 2;
            return 4;
        }
    }

    public class DeferredEntry
    {
        /// <summary>Player who pocketed the point Saitama gave up (or the Jew who stole it from them).</summary>
        public Guid RecipientId;

        /// <summary>Round-multiplied point value owed back to Saitama / taken from this recipient at reclaim.</summary>
        public int Points;
    }
}
