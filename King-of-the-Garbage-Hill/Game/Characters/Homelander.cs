using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class Homelander
{
    public const string CharacterName = "Homelander";
    public const string Righteousness = "Праведность";
    public const string Modesty = "Скромность";
    public const string VoughtTower = "Башня Vought";
    // Passive-audit exact key: "Супер "Человек""
    public const string SuperHuman = "Супер \"Человек\"";
    public const string Milk = "Молоко";
    public const string LeaderOfTheSeven = "Лидер Семерки";
    public const int RagePerTrigger = 20;
    public const int MaximumRage = 100;

    private static IReadOnlyList<string> DropPhrases => CharactersUniquePhrase.HomelanderDropPhrases;
    private static IReadOnlyList<string> RevealPhrases => CharactersUniquePhrase.HomelanderRevealPhrases;
    private static IReadOnlyList<string> EnemyVoughtPhrases => CharactersUniquePhrase.HomelanderEnemyVoughtPhrases;
    private static IReadOnlyList<string> OwnVoughtPhrases => CharactersUniquePhrase.HomelanderOwnVoughtPhrases;
    private static IReadOnlyList<string> ProtectionPhrases => CharactersUniquePhrase.HomelanderProtectionPhrases;
    private static IReadOnlyList<string> TheBoysDropPhrases => CharactersUniquePhrase.HomelanderTheBoysDropPhrases;

    public sealed class RageMark
    {
        public int Percent { get; set; }
        public bool LaserUsed { get; set; }
    }

    public sealed class State
    {
        public Dictionary<Guid, RageMark> RageByPlayer { get; set; } = new();
        public List<Guid> RevealedByPlayers { get; set; } = new();
        public bool EveryoneGuessed { get; set; }
        public Guid LaserTargetThisFight { get; set; } = Guid.Empty;
        public bool MilkTriggered { get; set; }
        public int LastRighteousnessRound { get; set; }
        public int LastVoughtRound { get; set; }
        public List<int> DropPhrasePool { get; set; } = new();
        public List<int> RevealPhrasePool { get; set; } = new();
        public List<int> EnemyVoughtPhrasePool { get; set; } = new();
        public List<int> OwnVoughtPhrasePool { get; set; } = new();
        public List<int> TheBoysDropPhrasePool { get; set; } = new();
    }

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName;

    public static GamePlayerBridgeClass Find(GameClass game) =>
        game?.PlayersList?.FirstOrDefault(Is);

    public static bool IsProtected(CharacterClass character, InGameStatus status, string source = "")
    {
        if (character?.Name != CharacterName
            || character.Passive.All(passive => passive.PassiveName != SuperHuman))
            return false;

        if (source == Modesty)
            return false;

        return status?.IsFightingTheBoys != true;
    }

    public static void LogProtection(InGameStatus status)
    {
        var owner = status?.GameCharacter;
        if (owner?.Name != CharacterName) return;

        status.AddInGamePersonalLogs(
            Take(ProtectionPhrases, status.HomelanderProtectionPhrasePool) + "\n");
    }

    public static void RunWithoutProtection(GamePlayerBridgeClass target, Action action)
    {
        if (target == null)
        {
            action();
            return;
        }

        var previous = target.Status.IsFightingTheBoys;
        target.Status.IsFightingTheBoys = true;
        try
        {
            action();
        }
        finally
        {
            target.Status.IsFightingTheBoys = previous;
        }
    }

    public static void MoveToInitialLead(List<GamePlayerBridgeClass> players)
    {
        var homelander = players?.FirstOrDefault(Is);
        if (homelander == null
            || players.Any(player => player.GameCharacter.Name == "Злой Школьник"))
            return;

        players.Remove(homelander);
        var protectedLeaderIndex = players.FindIndex(UnknownBug.Is);
        players.Insert(protectedLeaderIndex == 0 ? 1 : 0, homelander);
    }

    public static void ApplyRighteousness(GameClass game)
    {
        var homelander = Find(game);
        if (homelander == null || homelander.Passives.IsDead) return;

        var state = homelander.Passives.Homelander;
        if (state.LastRighteousnessRound == game.RoundNo) return;
        state.LastRighteousnessRound = game.RoundNo;

        var leader = game.PlayersList
            .Where(player => !player.Passives.IsDead)
            .OrderBy(player => player.Status.GetPlaceAtLeaderBoard())
            .FirstOrDefault();
        if (leader == null) return;

        if (leader.GetPlayerId() == homelander.GetPlayerId())
        {
            homelander.GameCharacter.AddMoral(5, Righteousness);
            return;
        }

        AddRage(homelander, leader, game);
    }

    public static void RecordAttackingWin(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass attacker,
        GameClass game)
    {
        if (!Is(homelander)
            || attacker == null
            || attacker.Status.IsWonThisCalculation != homelander.GetPlayerId())
            return;

        AddRage(homelander, attacker, game);
    }

    private static void AddRage(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass enemy,
        GameClass game)
    {
        if (UnknownBug.Is(enemy)
            || homelander.IsTeamMember(game, enemy.GetPlayerId()))
            return;

        var state = homelander.Passives.Homelander;
        if (!state.RageByPlayer.TryGetValue(enemy.GetPlayerId(), out var mark))
        {
            mark = new RageMark();
            state.RageByPlayer[enemy.GetPlayerId()] = mark;
        }

        if (mark.LaserUsed) return;
        mark.Percent = Math.Min(MaximumRage, mark.Percent + RagePerTrigger);
    }

    public static bool ArmLaser(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass target)
    {
        if (!Is(homelander) || target == null) return false;

        var state = homelander.Passives.Homelander;
        if (!state.RageByPlayer.TryGetValue(target.GetPlayerId(), out var mark)
            || mark.LaserUsed
            || mark.Percent < MaximumRage)
            return false;

        state.LaserTargetThisFight = target.GetPlayerId();
        homelander.Status.IsArmorBreak = true;
        homelander.Status.IsSkipBreak = true;
        target.Status.IsAbleToWin = false;
        return true;
    }

    public static bool IsLaserFight(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass target) =>
        Is(homelander)
        && target != null
        && homelander.Passives.Homelander.LaserTargetThisFight == target.GetPlayerId();

    public static int ApplyLaserDrops(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        if (!IsLaserFight(homelander, target)) return 0;

        var state = homelander.Passives.Homelander;
        var mark = state.RageByPlayer[target.GetPlayerId()];
        mark.LaserUsed = true;
        mark.Percent = MaximumRage;

        var drops = 0;
        for (var i = 0; i < 2; i++)
        {
            if (target.GameCharacter.HandleDrop(target.DiscordUsername, game, allowAtLastPlace: true,
                    allowAtZeroScore: true))
                drops++;
        }

        if (drops > 0)
            LogDropByHomelander(homelander);
        return drops;
    }

    public static void LogDropByHomelander(GamePlayerBridgeClass homelander)
    {
        if (!Is(homelander)) return;
        var state = homelander.Passives.Homelander;
        homelander.Status.AddInGamePersonalLogs(
            Take(DropPhrases, state.DropPhrasePool) + "\n");
    }

    public static void LogTheBoysDrop(GamePlayerBridgeClass homelander)
    {
        if (!Is(homelander)) return;
        var state = homelander.Passives.Homelander;
        homelander.Status.AddInGamePersonalLogs(
            Take(TheBoysDropPhrases, state.TheBoysDropPhrasePool) + "\n");
    }

    public static void RecordReveal(
        GameClass game,
        GamePlayerBridgeClass revealer,
        GamePlayerBridgeClass revealed)
    {
        if (!Is(revealed)
            || revealer == null
            || revealer.GetPlayerId() == revealed.GetPlayerId())
            return;

        var state = revealed.Passives.Homelander;
        if (state.RevealedByPlayers.Contains(revealer.GetPlayerId())) return;
        state.RevealedByPlayers.Add(revealer.GetPlayerId());

        var phrase = Take(RevealPhrases, state.RevealPhrasePool)
            .Replace("(ник врага)", revealer.DiscordUsername);
        revealed.Status.AddInGamePersonalLogs(phrase + "\n");
    }

    public static void SuppressJustice(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass enemy)
    {
        if (!Is(homelander)
            || enemy == null
            || homelander.Passives.Homelander.EveryoneGuessed
            || !homelander.Passives.Homelander.RevealedByPlayers.Contains(enemy.GetPlayerId()))
            return;

        homelander.FightCharacter.Justice.SetJusticeForOneFight(0, Modesty);
    }

    public static void EvaluatePredictions(GameClass game)
    {
        var homelander = Find(game);
        if (homelander == null || homelander.Passives.Homelander.EveryoneGuessed) return;

        var enemies = game.PlayersList
            .Where(player => player.GetPlayerId() != homelander.GetPlayerId())
            .ToList();
        if (enemies.Count != 5
            || enemies.Any(enemy => enemy.Predict.All(prediction =>
                prediction.PlayerId != homelander.GetPlayerId()
                || prediction.CharacterName != CharacterName)))
            return;

        homelander.Passives.Homelander.EveryoneGuessed = true;
        homelander.Status.AddInGamePersonalLogs(
            "Все разузнали вашу личность. Патриот: \"Раз вы все такие умные... Тогда мне больше не нужно притворяться. Сперва я уничтожу белый дом... потом армейские базы... потом...\"\"\n");
    }

    public static void TryMilk(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass target)
    {
        if (!Is(homelander)
            || target?.GameCharacter?.Name != "HardKitty"
            || homelander.Passives.Homelander.MilkTriggered)
            return;

        homelander.Passives.Homelander.MilkTriggered = true;
        homelander.Status.AddRegularPoints(1, Milk);
        homelander.Status.AddInGamePersonalLogs(
            "\"ДА, я люблю **молоко в пакетах**!\"\n");
    }

    public static void ApplyVoughtTower(GameClass game)
    {
        var homelander = Find(game);
        if (homelander == null || homelander.Passives.IsDead) return;

        var state = homelander.Passives.Homelander;
        if (state.LastVoughtRound == game.RoundNo) return;
        state.LastVoughtRound = game.RoundNo;

        var gains = game.PlayersList
            .Where(player => !player.Passives.IsDead)
            .Select(player => new
            {
                Player = player,
                Points = player.Status.ScoreEntries
                    .Where(entry => entry.Points > 0)
                    .Sum(entry => entry.IsBonus
                        ? entry.Points
                        : entry.Points * player.Status.GetRoundScoreMultiplier(game))
            })
            .ToList();
        if (gains.Count == 0) return;

        var maximum = gains.Max(entry => entry.Points);
        if (maximum <= 0 || gains.Count(entry => entry.Points == maximum) != 1) return;

        var winner = gains.Single(entry => entry.Points == maximum).Player;
        if (winner.GetPlayerId() != homelander.GetPlayerId())
        {
            if (homelander.IsTeamMember(game, winner.GetPlayerId())) return;
            winner.Status.AddRegularPoints(-1, "Vought");
            var phrase = Take(EnemyVoughtPhrases, state.EnemyVoughtPhrasePool)
                .Replace("(ник врага)", winner.DiscordUsername);
            homelander.Status.AddInGamePersonalLogs(phrase + "\n");
            return;
        }

        var regular = homelander.Status.ScoreEntries
            .Where(entry => !entry.IsBonus && entry.Points > 0)
            .Sum(entry => entry.Points);
        var bonus = homelander.Status.ScoreEntries
            .Where(entry => entry.IsBonus && entry.Points > 0)
            .Sum(entry => entry.Points);
        if (regular > 0)
            homelander.Status.AddRegularPoints((int)regular, "Vought");
        if (bonus > 0)
            homelander.Status.AddBonusPoints(bonus, "Vought");
        homelander.Status.AddInGamePersonalLogs(
            Take(OwnVoughtPhrases, state.OwnVoughtPhrasePool) + "\n");
    }

    public static int RagePercentFor(
        GamePlayerBridgeClass homelander,
        Guid enemyId)
    {
        if (!Is(homelander)) return 0;
        return homelander.Passives.Homelander.RageByPlayer.TryGetValue(enemyId, out var mark)
            ? mark.Percent
            : 0;
    }

    public static bool LaserUsedFor(
        GamePlayerBridgeClass homelander,
        Guid enemyId) =>
        Is(homelander)
        && homelander.Passives.Homelander.RageByPlayer.TryGetValue(enemyId, out var mark)
        && mark.LaserUsed;

    private static string Take(
        IReadOnlyList<string> phrases,
        List<int> remaining)
    {
        if (remaining.Count == 0)
            remaining.AddRange(Enumerable.Range(0, phrases.Count));

        var poolIndex = SecureRandom.Next(0, remaining.Count - 1);
        var phraseIndex = remaining[poolIndex];
        remaining.RemoveAt(poolIndex);
        return phrases[phraseIndex];
    }
}
