using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Kira
{
    public class DeathNoteClass
    {
        public Guid CurrentRoundTarget { get; set; } = Guid.Empty;
        public string CurrentRoundName { get; set; } = "";
        public List<DeathNoteEntry> Entries { get; set; } = new();
        // Historical DTO name retained: every target already used in the notebook is locked,
        // regardless of whether the name was wrong, the victim revived or Izanagi prevented death.
        public List<Guid> FailedTargets { get; set; } = new();
    }

    public class DeathNoteEntry
    {
        public Guid TargetPlayerId { get; set; }
        public string WrittenName { get; set; } = "";
        public int RoundWritten { get; set; }
        public bool WasCorrect { get; set; }
        public bool CausedDeath { get; set; } = true;
    }

    public class ShinigamiEyesClass
    {
        public bool EyesActiveForNextAttack { get; set; } = false;
        public List<Guid> RevealedPlayers { get; set; } = new();
    }

    public class LClass
    {
        public Guid LPlayerId { get; set; } = Guid.Empty;
        public bool IsArrested { get; set; } = false;
    }
}
