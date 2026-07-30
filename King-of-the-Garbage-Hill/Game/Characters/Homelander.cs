using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.API.Services;
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
    public const int LeaderMoral = 7;
    public const int LeaderRage = 25;
    public const int VictoryRage = 10;
    public const int LeaderVictoryRage = 50;
    public const int MaximumRage = 100;
    public const int LaserMoral = 10;
    public const int SevenPoints = 7;

    // Stan Edgar is a hidden mechanic and deliberately NOT a characters.json passive: it must
    // survive Kimiko's Живое Оружие (which HasPassive rejects), must not be copyable by Ziggurat
    // or transforms, and must stay out of the ARAM passive pool. It is gated on Is() alone.
    public const string StanEdgar = "Stan Edgar";
    public const int StanEdgarScoreThreshold = 70;
    public const int StanEdgarPenalty = 33;

    public const string StanEdgarAvatar = "https://r2.ozvmusic.com/kotgh/art/avatars/stan_edgar.png";

    private static IReadOnlyList<string> DropPhrases => CharactersUniquePhrase.HomelanderDropPhrases;
    private static IReadOnlyList<string> RevealPhrases => CharactersUniquePhrase.HomelanderRevealPhrases;
    private static IReadOnlyList<string> EnemyVoughtPhrases => CharactersUniquePhrase.HomelanderEnemyVoughtPhrases;
    private static IReadOnlyList<string> OwnVoughtPhrases => CharactersUniquePhrase.HomelanderOwnVoughtPhrases;
    private static IReadOnlyList<string> ProtectionPhrases => CharactersUniquePhrase.HomelanderProtectionPhrases;
    private static IReadOnlyList<string> TheBoysDropPhrases => CharactersUniquePhrase.HomelanderTheBoysDropPhrases;

    public sealed class RageMark
    {
        public int Percent { get; set; }
        public int LastLeaderChargeRound { get; set; }
    }

    public sealed class State
    {
        public Dictionary<Guid, RageMark> RageByPlayer { get; set; } = new();
        public List<Guid> RevealedByPlayers { get; set; } = new();
        public bool EveryoneGuessed { get; set; }
        public Guid LaserTargetThisFight { get; set; } = Guid.Empty;
        public bool MilkTriggered { get; set; }
        public bool StanEdgarResolved { get; set; }
        public bool SevenPointsHaveBeenFrozen { get; set; }
        public int LastRighteousnessRound { get; set; }
        public bool WasLeaderAtRighteousnessStart { get; set; }
        public int LastResolvedWinRound { get; set; }
        public int LastVoughtRound { get; set; }
        public List<int> DropPhrasePool { get; set; } = new();
        public List<int> RevealPhrasePool { get; set; } = new();
        public List<int> EnemyVoughtPhrasePool { get; set; } = new();
        public List<int> OwnVoughtPhrasePool { get; set; } = new();
        public List<int> TheBoysDropPhrasePool { get; set; } = new();
    }

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName;

    public static bool HasPassive(GamePlayerBridgeClass player, string passiveName) =>
        Is(player)
        && !player.Passives.PassiveAbilitiesDisabledByKimiko
        && player.GameCharacter.Passive.Any(passive => passive.PassiveName == passiveName);

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
            $"{SuperHuman}: {Take(ProtectionPhrases, status.HomelanderProtectionPhrasePool)}\n");
    }

    public static bool CanTransferFrom(GamePlayerBridgeClass victim, string source)
    {
        if (!IsProtected(victim?.GameCharacter, victim?.Status, source))
            return true;

        LogProtection(victim.Status);
        return false;
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
        if (homelander == null) return;

        if (players.All(player => player.GameCharacter.Name != "Злой Школьник"))
        {
            players.Remove(homelander);
            var protectedLeaderIndex = players.FindIndex(UnknownBug.Is);
            players.Insert(protectedLeaderIndex == 0 ? 1 : 0, homelander);
        }

        var sevenPointsActive = HasPassive(homelander, Righteousness)
                                && players.FirstOrDefault()?.GetPlayerId() == homelander.GetPlayerId();
        homelander.Status.HomelanderSevenPointsActive = sevenPointsActive;
        homelander.Passives.Homelander.SevenPointsHaveBeenFrozen = !sevenPointsActive;
    }

    public static void UpdateSevenPointsAvailability(GameClass game)
    {
        if (game?.PlayersList == null) return;
        UpdateSevenPointsAvailability(game.PlayersList);
    }

    public static bool UpdateSevenPointsAvailability(
        IReadOnlyList<GamePlayerBridgeClass> players)
    {
        var homelander = players?.FirstOrDefault(Is);
        if (homelander == null) return false;

        var shouldBeActive = HasPassive(homelander, Righteousness)
                             && !homelander.Passives.IsDead
                             && players.FirstOrDefault()?.GetPlayerId() == homelander.GetPlayerId();
        if (homelander.Status.HomelanderSevenPointsActive == shouldBeActive)
            return false;

        homelander.Status.HomelanderSevenPointsActive = shouldBeActive;
        if (!shouldBeActive)
        {
            homelander.Passives.Homelander.SevenPointsHaveBeenFrozen = true;
            return true;
        }

        if (homelander.Passives.Homelander.SevenPointsHaveBeenFrozen)
            homelander.Status.AddInGamePersonalLogs(
                $"{Righteousness}: Сильная личность!\n");
        return true;
    }

    public static void OnPassivesDisabled(GamePlayerBridgeClass homelander)
    {
        if (!Is(homelander)) return;
        homelander.Status.HomelanderSevenPointsActive = false;
        homelander.Passives.Homelander.LaserTargetThisFight = Guid.Empty;
    }

    public static void ApplyRighteousness(GameClass game)
    {
        var homelander = Find(game);
        if (homelander == null) return;
        UpdateSevenPointsAvailability(game);
        if (!HasPassive(homelander, Righteousness) || homelander.Passives.IsDead) return;

        var state = homelander.Passives.Homelander;
        if (state.LastRighteousnessRound == game.RoundNo) return;
        state.LastRighteousnessRound = game.RoundNo;

        var leader = game.PlayersList
            .Where(player => !player.Passives.IsDead)
            .OrderBy(player => player.Status.GetPlaceAtLeaderBoard())
            .FirstOrDefault();
        if (leader == null) return;

        state.WasLeaderAtRighteousnessStart =
            leader.GetPlayerId() == homelander.GetPlayerId();
        if (state.WasLeaderAtRighteousnessStart) return;

        AddRage(homelander, leader, game, LeaderRage, leaderCharge: true);
    }

    public static void SettleRighteousnessMoral(GameClass game)
    {
        var homelander = Find(game);
        if (!HasPassive(homelander, Righteousness)
            || homelander.Passives.IsDead)
            return;

        var state = homelander.Passives.Homelander;
        if (state.LastRighteousnessRound != game.RoundNo
            || !state.WasLeaderAtRighteousnessStart)
            return;

        if (state.LastResolvedWinRound == game.RoundNo)
            homelander.GameCharacter.AddMoral(LeaderMoral, Righteousness);
    }

    public static void RecordResolvedWin(
        GamePlayerBridgeClass winner,
        GameClass game)
    {
        if (!Is(winner) || game == null) return;
        winner.Passives.Homelander.LastResolvedWinRound = game.RoundNo;
    }

    public static void RecordEnemyVictory(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass enemy,
        GameClass game)
    {
        if (!HasPassive(homelander, Righteousness)
            || enemy == null
            || (homelander.Passives.Homelander.LastRighteousnessRound == game.RoundNo
                && homelander.Passives.Homelander.WasLeaderAtRighteousnessStart)
            || (enemy.Status.IsWonThisCalculation != homelander.GetPlayerId()
                && homelander.Status.IsLostThisCalculation != enemy.GetPlayerId()))
            return;

        var state = homelander.Passives.Homelander;
        var wonAsLeader = state.RageByPlayer.TryGetValue(enemy.GetPlayerId(), out var mark)
                          && mark.LastLeaderChargeRound == game.RoundNo;
        AddRage(
            homelander,
            enemy,
            game,
            wonAsLeader ? LeaderVictoryRage : VictoryRage);
    }

    private static void AddRage(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass enemy,
        GameClass game,
        int amount,
        bool leaderCharge = false)
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

        mark.Percent = Math.Min(MaximumRage, mark.Percent + amount);
        if (leaderCharge)
            mark.LastLeaderChargeRound = game.RoundNo;
    }

    public static bool ArmLaser(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        if (!HasPassive(homelander, Righteousness) || target == null
            || Sirinoks.BlocksAutowinFrom(target, homelander, game))
            return false;

        var state = homelander.Passives.Homelander;
        if (!state.RageByPlayer.TryGetValue(target.GetPlayerId(), out var mark)
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
        // The laser is repeatable against the same enemy: spending it only empties the bar,
        // so the enemy can charge Homelander up again and be lasered any number of times.
        mark.Percent = 0;
        homelander.GameCharacter.AddMoral(LaserMoral, Righteousness);

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
        if (!HasPassive(homelander, Righteousness)) return;
        var state = homelander.Passives.Homelander;
        homelander.Status.AddInGamePersonalLogs(
            $"{Righteousness}: {Take(DropPhrases, state.DropPhrasePool)}\n");
    }

    public static void LogTheBoysDrop(GamePlayerBridgeClass homelander)
    {
        if (!HasPassive(homelander, SuperHuman)) return;
        var state = homelander.Passives.Homelander;
        homelander.Status.AddInGamePersonalLogs(
            $"{SuperHuman}: {Take(TheBoysDropPhrases, state.TheBoysDropPhrasePool)}\n");
    }

    // AGENT CONTRACT: every present or future mechanic that reveals a player's exact identity
    // must call RecordReveal. Homelander's Скромность suppression and owner-only leaderboard marker
    // are both derived from this registry; adding a reveal without this call is an interaction bug.
    public static void RecordReveal(
        GameClass game,
        GamePlayerBridgeClass revealer,
        GamePlayerBridgeClass revealed)
    {
        if (!HasPassive(revealed, Modesty)
            || revealer == null
            || revealer.GetPlayerId() == revealed.GetPlayerId())
            return;

        var state = revealed.Passives.Homelander;
        if (state.RevealedByPlayers.Contains(revealer.GetPlayerId())) return;
        state.RevealedByPlayers.Add(revealer.GetPlayerId());

        var phrase = Take(RevealPhrases, state.RevealPhrasePool)
            .Replace("(ник врага)", $"**{revealer.DiscordUsername}**");
        revealed.Status.AddInGamePersonalLogs($"{Modesty}: {phrase}\n");
    }

    public static void SuppressJustice(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass enemy)
    {
        if (!HasPassive(homelander, Modesty)
            || enemy == null
            || homelander.Passives.Homelander.EveryoneGuessed
            || !homelander.Passives.Homelander.RevealedByPlayers.Contains(enemy.GetPlayerId()))
            return;

        homelander.FightCharacter.Justice.SetJusticeForOneFight(0, Modesty);
    }

    public static void EvaluatePredictions(GameClass game)
    {
        var homelander = Find(game);
        if (!HasPassive(homelander, Modesty)
            || homelander.Passives.Homelander.EveryoneGuessed)
            return;

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
            $"{Modesty}: Все разузнали вашу личность. Патриот: \"Раз вы все такие умные... Тогда мне больше не нужно притворяться. Сперва я уничтожу белый дом... потом армейские базы... потом...\"\"\n");
    }

    public static void TryMilk(
        GamePlayerBridgeClass homelander,
        GamePlayerBridgeClass target)
    {
        if (!HasPassive(homelander, Milk)
            || target?.GameCharacter?.Name != "HardKitty"
            || homelander.Passives.Homelander.MilkTriggered)
            return;

        homelander.Passives.Homelander.MilkTriggered = true;
        homelander.Status.AddRegularPoints(1, Milk);
        homelander.Status.AddInGamePersonalLogs(
            $"{Milk}: \"ДА, я люблю **молоко в пакетах**!\"\n");
    }

    public static void ApplyVoughtTower(GameClass game)
    {
        var homelander = Find(game);
        if (!HasPassive(homelander, VoughtTower) || homelander.Passives.IsDead) return;

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
            winner.Status.AddBonusPoints(-1, "Vought");
            var phrase = Take(EnemyVoughtPhrases, state.EnemyVoughtPhrasePool)
                .Replace("(ник врага)", $"**{winner.DiscordUsername}**");
            homelander.Status.AddInGamePersonalLogs($"{VoughtTower}: {phrase}\n");
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
            $"{VoughtTower}: {Take(OwnVoughtPhrases, state.OwnVoughtPhrasePool)}\n");
    }

    // Vought writes off an unreliable asset. Hidden from players and permanently active: unlike every
    // other part of the kit this ignores Живое Оружие, Супер "Человек" and even death. It is cancelled
    // only by an early game end (Космический ужас / Вилтрумайты both return before the call site) or
    // by Вечное Цукуеми. Runs once, right after turn 10 has settled.
    public static void ApplyStanEdgar(GameClass game)
    {
        var homelander = Find(game);
        if (homelander == null) return;

        var state = homelander.Passives.Homelander;
        if (state.StanEdgarResolved) return;
        state.StanEdgarResolved = true;

        if (Madara.IsEternalTsukuyomiActive(game)) return;
        if (homelander.Status.GetScore() >= StanEdgarScoreThreshold) return;

        // Public fight row. The :war: marker is what keeps SortGameLogs from pushing the line
        // out of the round's fight table and into the trailing extra-log block.
        game.AddGlobalLogs($"{StanEdgar} <:war:561287719838547981> {homelander.DiscordUsername}", "");
        game.AddGlobalLogs($" ⟶ {StanEdgar}");

        game.WebFightLog.Add(new FightEntryDto
        {
            AttackerName = StanEdgar,
            AttackerCharName = StanEdgar,
            AttackerAvatar = StanEdgarAvatar,
            DefenderName = homelander.DiscordUsername,
            DefenderCharName = homelander.GameCharacter.Name,
            DefenderAvatar = GameStateMapper.GetLocalAvatarUrl(
                homelander.GameCharacter.AvatarCurrent ?? homelander.GameCharacter.Avatar),
            Outcome = "win",
            WinnerName = StanEdgar,
        });

        game.AddGlobalLogs(
            $"{StanEdgar}: \"Ты не набрал даже {StanEdgarScoreThreshold} очков? Боюсь, Vought не может позволить себе столь ненадежный актив.\"");
        game.AddGlobalLogs($"{CharacterName}: \"Но ведь я сверхчеловек!\"");
        game.AddGlobalLogs($"{StanEdgar}: \"Для нашей компании ты не больше чем бракованный продукт.\"");
        game.AddGlobalLogs("Залп V-наводящихся ракет...");

        homelander.Status.AddInGamePersonalLogs(PhrasePayload.Encode(
            StanEdgar,
            "\"У нас есть десятки способов тебя устранить, Нуар был лишь одним из них.\"",
            StanEdgar,
            "\"We have dozens of ways to get rid of you. Noir was only one of them.\"") + "\n");

        // Супер "Человек" would reject the debit outright, so it is spent through the same
        // protection bypass TheBoys uses. AddBonusPoints writes its own personal line.
        RunWithoutProtection(homelander, () =>
            homelander.Status.AddBonusPoints(-StanEdgarPenalty, StanEdgar));
    }

    public static int RagePercentFor(
        GamePlayerBridgeClass homelander,
        Guid enemyId)
    {
        if (!HasPassive(homelander, Righteousness)) return 0;
        return homelander.Passives.Homelander.RageByPlayer.TryGetValue(enemyId, out var mark)
            ? mark.Percent
            : 0;
    }

    public static bool WasRevealedBy(
        GamePlayerBridgeClass homelander,
        Guid enemyId) =>
        HasPassive(homelander, Modesty)
        && homelander.Passives.Homelander.RevealedByPlayers.Contains(enemyId);

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
