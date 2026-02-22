using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class RickSanchez
{
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
