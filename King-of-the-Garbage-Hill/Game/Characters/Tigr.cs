using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Tigr
{
    public const string RoundTenBanPassive = "Стримснайпят и банят и банят и банят";

    public static bool IsRoundTenBanned(GamePlayerBridgeClass player, int roundNo)
    {
        return roundNo == 10 && (player.Passives.DopaPermabanTriggered
            || player.GameCharacter.Passive.Any(passive =>
                passive.PassiveName == RoundTenBanPassive));
    }

    public static void ApplyRoundTenBan(GamePlayerBridgeClass player, GameClass game)
    {
        if (player.Passives.RoundTenBanApplied) return;

        player.Passives.RoundTenBanApplied = true;
        player.Status.IsSkip = true;
        player.Status.ConfirmedSkip = false;
        player.Status.IsBlock = false;
        player.Status.IsReady = true;
        player.Status.WhoToAttackThisTurn = new List<Guid>();
        player.GameCharacter.SetPsyche(0, RoundTenBanPassive);
        player.GameCharacter.SetIntelligence(0, RoundTenBanPassive);
        player.GameCharacter.SetStrength(10, RoundTenBanPassive);
        game.AddGlobalLogs($"{player.DiscordUsername}: ЕБАННЫЕ БАНЫ НА 10 ЛЕТ");
    }

    public class TigrTopClass
    {
        public int TimeCount = 3;
    }

    public class ThreeZeroClass
    {
        public List<Guid> WhoToLostThisRound = new();
        public List<Guid> WhoToWinThisRound = new();
        public List<ThreeZeroSubClass> FriendList = new();
    }

    public class ThreeZeroSubClass
    {
        public Guid EnemyPlayerId;
        public bool IsUnique;
        public int WinsSeries;

        public ThreeZeroSubClass(Guid enemyPlayerId)
        {
            EnemyPlayerId = enemyPlayerId;
            WinsSeries = 1;
            IsUnique = true;
        }
    }
}
