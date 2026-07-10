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
                                 + player.Status.GetScoresToGiveAtEndOfRound()
                                 * GetRoundMultiplier(player, game),
            })
            .OrderByDescending(x => x.ProjectedScore)
            .ThenBy(x => x.OriginalIndex)
            .Select(x => x.Player)
            .ToList();
    }

    private static int GetRoundMultiplier(GamePlayerBridgeClass player, GameClass game)
    {
        var roundNumber = game.RoundNo;
        if (game.PlayersList.Any(tolya =>
                tolya.GameCharacter.Passive.Any(passive => passive.PassiveName == "Подсчет")
                && tolya.Passives.TolyaCount.TargetList.Any(target =>
                    target.RoundNumber == game.RoundNo - 1 && target.Target == player.GetPlayerId())))
        {
            roundNumber = 1;
        }

        return roundNumber switch
        {
            <= 4 => 1,
            <= 9 => 2,
            _ => 4,
        };
    }
}
