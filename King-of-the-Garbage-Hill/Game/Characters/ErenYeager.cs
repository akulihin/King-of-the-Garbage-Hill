using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class ErenYeager
{
    public const string CharacterName = "Эрен Йегер";
    public const string Sheep = "Овца в загоне";
    public const string Fighter = "Дрочун";
    public const string AttackTitan = "Атакующий Титан";
    public const string Rumbling = "Rumbling";

    public sealed class State
    {
        public int RageGained { get; set; }
        public int Losses { get; set; }
        public bool AttackTitanActiveThisRound { get; set; }
        public int AttackTitanCooldown { get; set; }
        public int AttackTitanSoundSerial { get; set; }
        public int TatakeSoundSerial { get; set; }
        public List<Guid> MutualAttackRewardsThisRound { get; set; } = new();
        public bool RumblingWarningPlayed { get; set; }
        public bool RumblingTriggered { get; set; }
        public int RumblingPlace { get; set; }
    }

    public static void MoveToLast(List<GamePlayerBridgeClass> players, GamePlayerBridgeClass eren)
    {
        var index = players.IndexOf(eren);
        if (index < 0 || index == players.Count - 1) return;
        players.RemoveAt(index);
        players.Add(eren);
    }

    public static List<GamePlayerBridgeClass> ProjectRoundEndLeaderboard(GameClass game)
    {
        return game.PlayersList
            .Select((player, index) => new
            {
                Player = player,
                OriginalIndex = index,
                ProjectedScore = player.Status.GetScore()
                                 + GordonFreeman.ProjectRegularSettlement(player, game),
            })
            .OrderByDescending(x => x.ProjectedScore)
            .ThenBy(x => x.OriginalIndex)
            .Select(x => x.Player)
            .ToList();
    }

    public static bool IsRumblingWarningActive(GameClass game)
    {
        if (game.RoundNo != 10) return false;

        return game.PlayersList.Any(player =>
            player.GameCharacter.Name == CharacterName
            && player.GameCharacter.Passive.Any(passive => passive.PassiveName == Rumbling)
            && player.Passives.Eren.RumblingWarningPlayed);
    }
}
