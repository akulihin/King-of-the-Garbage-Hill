using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class RickSanchez
{
    public const string MostWanted = "Most wanted";

    // Most wanted: every random enemy bonus/mark force-targets the living holder.
    // Kira's L is the sole exception and must keep its ordinary random draw.
    // See docs/INTERACTION-MATRIX.md "Rick Most wanted".
    public static GamePlayerBridgeClass FindMostWantedHolder(
        IEnumerable<GamePlayerBridgeClass> players,
        GamePlayerBridgeClass effectOwner = null)
    {
        return players.FirstOrDefault(x => !x.Passives.IsDead
            && x.GameCharacter.Passive.Any(y => y.PassiveName == MostWanted)
            && (effectOwner == null || x.GetPlayerId() != effectOwner.GetPlayerId()));
    }

    public class GiantBeansClass
    {
        public int BaseIntelligence;
        public int FakeIntelligence = 0;
        public int BeanStacks = 0;
        public List<Guid> IngredientTargets = new();
        public bool IngredientsActive = false;
    }

    public class PickleRickClass
    {
        public int PickleTurnsRemaining = 0;
        public bool WasAttackedAsPickle = false;
        public int PenaltyTurnsRemaining = 0;
    }

    public class PortalGunClass
    {
        public bool Invented = false;
        public int Charges = 0;
        public bool SwapActive = false;
        public Guid SwappedWith = Guid.Empty;
        public bool FiredThisRound = false;
    }
}
