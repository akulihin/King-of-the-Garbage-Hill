using System;
using System.Collections.Generic;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Itachi
{
    public const string ShisuiEye = "Глаз Шусуи";

    public static bool TryPreventDeath(GamePlayerBridgeClass player, GameClass game)
    {
        if (player == null
            || game == null
            || player.Passives.ItachiShisuiUsed
            || !player.GameCharacter.Passive.Exists(passive => passive.PassiveName == ShisuiEye))
            return false;

        player.Passives.ItachiShisuiUsed = true;
        player.Passives.IsDead = false;
        player.Passives.DeathSource = "";
        var shisui = player.GameCharacter.Passive.Find(passive => passive.PassiveName == ShisuiEye);
        if (shisui != null)
            shisui.Visible = true;

        game.AddGlobalLogs(
            $"**Изанаги!**\n**{UnknownBug.PublicName(player)}** избежал смерти\n" +
            "\"Я планировал приберечь глаз Шисуи для кое-чего другого... но ладно.\"");
        return true;
    }

    public class CrowsClass
    {
        // Per-enemy crow count: playerId -> number of crows
        public Dictionary<Guid, int> CrowCounts { get; set; } = new();
        // Set after level-up; next attack throws a crow
        public bool CrowReadyToThrow { get; set; } = false;
    }

    public class IzanagiClass
    {
        public int UsesRemaining { get; set; } = 2;
    }

    public class TsukuyomiClass
    {
        public int ChargeCounter { get; set; } = 0;
        public Guid TsukuyomiTargetThisRound { get; set; } = Guid.Empty;
        public Guid TsukuyomiActiveTarget { get; set; } = Guid.Empty;
        public decimal TotalStolenPoints { get; set; } = 0;
        public Dictionary<Guid, decimal> StolenFromPlayers { get; set; } = new();
        // Enemies ever marked by Tsukuyomi: the technique never works twice on the same enemy
        public List<Guid> CaughtPlayers { get; set; } = new();
    }
}
