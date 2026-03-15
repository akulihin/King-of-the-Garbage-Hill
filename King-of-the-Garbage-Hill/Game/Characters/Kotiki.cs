using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Kotiki
{
    public class StormClass
    {
        public List<Guid> TauntedPlayers { get; set; } = new();     // Already taunted (once per enemy per game)
        public Guid CurrentTauntTarget { get; set; } = Guid.Empty;  // Who was taunted this round
    }

    public class AmbushClass
    {
        public Guid MinkaOnPlayer { get; set; } = Guid.Empty;   // Enemy carrying Minka cat
        public Guid StormOnPlayer { get; set; } = Guid.Empty;   // Enemy carrying Storm cat
        public int MinkaCooldown { get; set; } = 0;             // Rounds until Minka can deploy again
        public int StormCooldown { get; set; } = 0;             // Rounds until Storm can deploy again
        public int MinkaRoundsOnEnemy { get; set; } = 0;        // Rounds Minka has been on enemy
    }

    public class RandomBehaviorClass
    {
        // Per-round trick selection (0=none, 1=fight, 2=bite, 3=vase)
        public int SelectedTrickThisRound { get; set; }

        // Trick 1 — "Запрыгнул в бой"
        public Guid FightTargetAttackerId { get; set; } = Guid.Empty;
        public Guid FightTargetDefenderId { get; set; } = Guid.Empty;
        public bool FightProcessed { get; set; }

        // Trick 2 — "Кусь за жопу"
        public Guid BiteTargetId { get; set; } = Guid.Empty;
        public int BiteLockPosition { get; set; } = -1;       // 1-indexed locked position
        public int BiteLockActiveUntilRound { get; set; }      // Lock expires after this round
        public bool BiteBonusPending { get; set; }

        // Trick 3 — "Скинул вазу" (game-wide)
        public bool VaseUsed { get; set; }
        public List<Guid> VasePendingTargets { get; set; } = new();
        public List<Guid> VaseImmunePlayerIds { get; set; } = new();
    }
}
