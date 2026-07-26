using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class OmniMan
{
    public const string CharacterName = "Omni-man";
    public const string ThinkMark = "Подумай, Марк!";
    public const string GuardiansOfTheGlobe = "Стражи Земли";
    public const string ParticleOfOurPower = "Частица нашей силы";
    public const string UndergroundTrain = "Подземный Поезд";
    public const string IntelligenceBattle = "Битва интеллекта";
    public const string ThinkMarkPhraseSource = "Подумай, Марк:";
    public const string InvasionMessage = "Земля единогласно присоединилась к Вилтрумайтской империи!";
    public const int IntelligenceAdvantageToResist = 2;
    public const int IntelligenceBattlePoints = 3;
    public const int SkillPerIncomingAttack = 10;
    public const int UndergroundTrainDrops = 4;
    public const decimal UndergroundTrainStealPercent = 0.10m;

    private static IReadOnlyList<string> IntelligenceCheckPhrases =>
        CharactersUniquePhrase.OmniManIntelligenceCheckPhrases;

    private static IReadOnlyList<CharactersUniquePhrase.OmniManIntelligenceDialogue>
        IntelligenceSuccessDialogues =>
        CharactersUniquePhrase.OmniManIntelligenceSuccessDialogues;

    private static IReadOnlyList<string> GuardiansPhrases =>
        CharactersUniquePhrase.OmniManGuardiansPhrases;

    private static IReadOnlyList<string> ParticlePhrases =>
        CharactersUniquePhrase.OmniManParticlePhrases;

    private static IReadOnlyList<string> UndergroundTrainPhrases =>
        CharactersUniquePhrase.OmniManUndergroundTrainPhrases;

    public sealed class State
    {
        public HashSet<Guid> IdiotPlayerIds { get; set; } = new();
        public int IntelligenceCheckPhraseIndex { get; set; }
        public int IntelligenceSuccessPhraseIndex { get; set; }
        public List<int> ParticlePhrasePool { get; set; } = new();
        public int GuardiansPhraseIndex { get; set; }
        public int LastGuardiansRound { get; set; }
        public Guid GuardiansSleepingPlayerId { get; set; } = Guid.Empty;
        public int GuardiansSleepingRound { get; set; }
        public bool UndergroundTrainUsed { get; set; }
        public List<int> UndergroundTrainPhrasePool { get; set; } = new();
        public int UndergroundTrainSerial { get; set; }
        public string UndergroundTrainPhrase { get; set; } = "";
        public int InvasionScheduledRound { get; set; }
        public bool InvasionTriggered { get; set; }
        public int InvasionSerial { get; set; }
    }

    public static bool Is(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName;

    public static GamePlayerBridgeClass Find(GameClass game) =>
        game?.PlayersList?.FirstOrDefault(Is);

    public static bool HasPassive(GamePlayerBridgeClass player, string passiveName) =>
        player?.GameCharacter?.Passive?.Any(passive => passive.PassiveName == passiveName) == true;

    public static void HandleIntelligenceWin(
        GamePlayerBridgeClass omniMan,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        if (!Is(omniMan)
            || !HasPassive(omniMan, ThinkMark)
            || target == null
            || omniMan.Status.IsWonThisCalculation != target.GetPlayerId()
            || target.Passives.IsDead
            || UnknownBug.Is(target)
            || omniMan.IsTeamMember(game, target.GetPlayerId()))
            return;

        var state = omniMan.Passives.OmniMan;
        if (target.FightCharacter.GetIntelligence()
            >= omniMan.FightCharacter.GetIntelligence() + IntelligenceAdvantageToResist)
        {
            var phrase = IntelligenceCheckPhrases[
                state.IntelligenceCheckPhraseIndex % IntelligenceCheckPhrases.Count];
            state.IntelligenceCheckPhraseIndex++;
            omniMan.Status.AddInGamePersonalLogs(
                $"{ThinkMarkPhraseSource} \"{phrase}\"\n");
            return;
        }

        if (!state.IdiotPlayerIds.Add(target.GetPlayerId())) return;

        omniMan.Status.AddBonusPoints(IntelligenceBattlePoints, IntelligenceBattle);
        var dialogue = IntelligenceSuccessDialogues[
            Math.Min(state.IntelligenceSuccessPhraseIndex, IntelligenceSuccessDialogues.Count - 1)];
        state.IntelligenceSuccessPhraseIndex++;
        omniMan.Status.AddInGamePersonalLogs(
            $"{ThinkMarkPhraseSource} \"{dialogue.Question}\"\n" +
            $"**{target.DiscordUsername}**: \"{dialogue.EnemyReply}\" **(проверка не интеллект провалена!)**\n" +
            $"{CharacterName}: \"{dialogue.OmniReply}\"\n");
    }

    public static void HandleEnemyAttack(
        GamePlayerBridgeClass omniMan,
        GamePlayerBridgeClass attacker,
        GameClass game)
    {
        if (!Is(omniMan)
            || !HasPassive(omniMan, ParticleOfOurPower)
            || omniMan.Passives.IsDead
            || attacker == null
            || attacker.GetPlayerId() == omniMan.GetPlayerId()
            || omniMan.IsTeamMember(game, attacker.GetPlayerId()))
            return;

        attacker.GameCharacter.AddExtraSkill(SkillPerIncomingAttack, ParticleOfOurPower);
        omniMan.Status.AddInGamePersonalLogs(
            $"{ParticleOfOurPower}: " +
            Take(ParticlePhrases, omniMan.Passives.OmniMan.ParticlePhrasePool) + "\n");
    }

    public static void ApplyGuardiansOfTheGlobe(GameClass game)
    {
        var omniMan = Find(game);
        if (omniMan == null
            || omniMan.Passives.IsDead
            || !HasPassive(omniMan, GuardiansOfTheGlobe)
            || game.RoundNo >= 10)
            return;

        var state = omniMan.Passives.OmniMan;
        if (state.LastGuardiansRound == game.RoundNo) return;
        state.LastGuardiansRound = game.RoundNo;

        var enemyGains = game.PlayersList
            .Where(player => player.GetPlayerId() != omniMan.GetPlayerId()
                             && !player.Passives.IsDead
                             && !UnknownBug.Is(player)
                             && !omniMan.IsTeamMember(game, player.GetPlayerId()))
            .Select(player => new
            {
                Player = player,
                Points = player.Status.ScoreEntries
                    .Sum(entry => entry.IsBonus
                        ? entry.Points
                        : entry.Points * player.Status.GetRoundScoreMultiplier(game))
            })
            .ToList();
        if (enemyGains.Count == 0) return;

        var maximum = enemyGains.Max(entry => entry.Points);
        if (maximum <= 0 || enemyGains.Count(entry => entry.Points == maximum) != 1) return;

        var guardian = enemyGains.Single(entry => entry.Points == maximum).Player;
        var nextRound = game.RoundNo + 1;
        if (!guardian.Passives.GlebTeaTriggeredWhen.WhenToTrigger.Contains(nextRound))
            guardian.Passives.GlebTeaTriggeredWhen.WhenToTrigger.Add(nextRound);
        state.GuardiansSleepingPlayerId = guardian.GetPlayerId();
        state.GuardiansSleepingRound = nextRound;

        var phrase = GuardiansPhrases[
                Math.Min(state.GuardiansPhraseIndex, GuardiansPhrases.Count - 1)]
            .Replace("(ник врага)", $"**{guardian.DiscordUsername}**");
        state.GuardiansPhraseIndex++;
        omniMan.Status.AddInGamePersonalLogs(
            $"{GuardiansOfTheGlobe}: {phrase}\n");
    }

    public static void HandleUndergroundTrainWin(
        GamePlayerBridgeClass omniMan,
        GamePlayerBridgeClass target,
        GameClass game)
    {
        if (!Is(omniMan)
            || !HasPassive(omniMan, UndergroundTrain)
            || target == null
            || omniMan.Passives.OmniMan.UndergroundTrainUsed
            || omniMan.Status.IsWonThisCalculation != target.GetPlayerId()
            || omniMan.Status.GetPlaceAtLeaderBoard() != 1
            || target.Status.GetPlaceAtLeaderBoard() != 2
            || target.Passives.IsDead
            || UnknownBug.Is(target)
            || omniMan.IsTeamMember(game, target.GetPlayerId()))
            return;

        var state = omniMan.Passives.OmniMan;
        state.UndergroundTrainUsed = true;

        var path = game.PlayersList
            .Where(player => player.Status.GetPlaceAtLeaderBoard() > 2)
            .OrderBy(player => player.Status.GetPlaceAtLeaderBoard())
            .Take(UndergroundTrainDrops)
            .ToList();

        var appliedDrops = 0;
        for (var index = 0; index < UndergroundTrainDrops; index++)
        {
            if (target.GameCharacter.HandleDrop(
                    target.DiscordUsername,
                    game,
                    allowAtLastPlace: true,
                    allowAtZeroScore: true))
                appliedDrops++;
        }

        foreach (var victim in path.Take(appliedDrops))
        {
            if (victim.Passives.IsDead
                || UnknownBug.Is(victim)
                || omniMan.IsTeamMember(game, victim.GetPlayerId()))
                continue;
            if (Homelander.IsProtected(victim.GameCharacter, victim.Status, UndergroundTrain))
            {
                Homelander.LogProtection(victim.Status);
                continue;
            }

            var stolen = Math.Max(
                1m,
                Math.Ceiling(victim.Status.GetScore() * UndergroundTrainStealPercent));
            var debit = JonSnow.IsKingActive(
                victim.GameCharacter,
                victim.Status.GetPlaceAtLeaderBoard())
                ? -stolen / 2m
                : -stolen;

            victim.Status.AddBonusPoints(debit, UndergroundTrain);
            omniMan.Status.AddBonusPoints(stolen, UndergroundTrain);
        }

        state.UndergroundTrainPhrase = Take(
            UndergroundTrainPhrases,
            state.UndergroundTrainPhrasePool);
        state.UndergroundTrainSerial++;
        omniMan.Status.AddInGamePersonalLogs(
            $"{UndergroundTrain}: \"{state.UndergroundTrainPhrase}\"\n");
    }

    public static void EvaluateInvasion(GameClass game)
    {
        var omniMan = Find(game);
        if (omniMan == null
            || omniMan.Passives.IsDead
            || !HasPassive(omniMan, ThinkMark)
            || game.RoundNo >= 10)
            return;

        var state = omniMan.Passives.OmniMan;
        if (game.CthulhuState.HorrorFired || game.IsFinished)
        {
            state.InvasionScheduledRound = 0;
            return;
        }
        if (state.InvasionTriggered || state.InvasionScheduledRound > 0) return;

        var enemies = EligibleInvasionEnemies(game, omniMan);
        if (enemies.Count > 0
            && enemies.All(enemy => state.IdiotPlayerIds.Contains(enemy.GetPlayerId())))
            state.InvasionScheduledRound = game.RoundNo + 1;
    }

    public static bool TryTriggerInvasion(GameClass game)
    {
        var omniMan = Find(game);
        if (omniMan == null) return false;

        var state = omniMan.Passives.OmniMan;
        if (state.InvasionTriggered || state.InvasionScheduledRound != game.RoundNo) return false;
        if (game.CthulhuState.HorrorFired || game.IsFinished)
        {
            state.InvasionScheduledRound = 0;
            return false;
        }

        var enemies = EligibleInvasionEnemies(game, omniMan);
        if (omniMan.Passives.IsDead
            || !HasPassive(omniMan, ThinkMark)
            || enemies.Count == 0
            || enemies.Any(enemy => !state.IdiotPlayerIds.Contains(enemy.GetPlayerId())))
        {
            state.InvasionScheduledRound = 0;
            return false;
        }

        state.InvasionScheduledRound = 0;
        state.InvasionTriggered = true;
        state.InvasionSerial++;
        ForceFirstPlace(game, omniMan);
        game.AddGlobalLogs(InvasionMessage);
        game.IsFinished = true;
        return true;
    }

    public static void RecordEqualJusticeFight(
        GamePlayerBridgeClass first,
        GamePlayerBridgeClass second,
        int firstJustice,
        int secondJustice)
    {
        if (first == null
            || second == null
            || firstJustice != secondJustice
            || !((Is(first) && second.GameCharacter.Name == Homelander.CharacterName)
                 || (Is(second) && first.GameCharacter.Name == Homelander.CharacterName)))
            return;

        first.Passives.AchievementTracker.HomelanderOmniManEqualJusticeFight = true;
        second.Passives.AchievementTracker.HomelanderOmniManEqualJusticeFight = true;
    }

    public static bool IsIdiot(GamePlayerBridgeClass omniMan, Guid enemyId) =>
        Is(omniMan) && omniMan.Passives.OmniMan.IdiotPlayerIds.Contains(enemyId);

    public static bool IsSleepingFromGuardians(
        GamePlayerBridgeClass omniMan,
        GamePlayerBridgeClass enemy,
        GameClass game)
    {
        if (!Is(omniMan) || enemy == null || game == null) return false;

        var state = omniMan.Passives.OmniMan;
        return state.GuardiansSleepingPlayerId == enemy.GetPlayerId()
               && state.GuardiansSleepingRound == game.RoundNo
               && enemy.Passives.GlebTeaTriggeredWhen.WhenToTrigger.Contains(game.RoundNo)
               && enemy.Status.IsSkip;
    }

    public static int GetInvasionSerial(GameClass game) =>
        Find(game)?.Passives.OmniMan.InvasionSerial ?? 0;

    public static int GetUndergroundTrainSerial(GameClass game) =>
        Find(game)?.Passives.OmniMan.UndergroundTrainSerial ?? 0;

    public static string GetUndergroundTrainPhrase(GameClass game) =>
        Find(game)?.Passives.OmniMan.UndergroundTrainPhrase ?? "";

    public static GamePlayerBridgeClass GetInvasionWinner(GameClass game)
    {
        if (game?.CthulhuState.HorrorFired == true) return null;
        var omniMan = Find(game);
        return omniMan?.Passives.OmniMan.InvasionTriggered == true ? omniMan : null;
    }

    public static void MoveFromInitialLead(List<GamePlayerBridgeClass> players)
    {
        if (players == null || players.Count < 2 || !Is(players[0])) return;

        var swapIndex = players.FindIndex(
            1,
            player => !UnknownBug.Is(player)
                      && player.GameCharacter.Name != JonSnow.CharacterName);
        if (swapIndex < 1) swapIndex = 1;
        (players[0], players[swapIndex]) = (players[swapIndex], players[0]);
    }

    public static void ForceFirstPlace(GameClass game, GamePlayerBridgeClass omniMan)
    {
        if (game?.PlayersList == null || omniMan == null) return;

        game.PlayersList.Remove(omniMan);
        game.PlayersList.Insert(0, omniMan);
        for (var index = 0; index < game.PlayersList.Count; index++)
            game.PlayersList[index].Status.SetPlaceAtLeaderBoard(index + 1);
    }

    private static List<GamePlayerBridgeClass> EligibleInvasionEnemies(
        GameClass game,
        GamePlayerBridgeClass omniMan) =>
        game.PlayersList
            .Where(player => player.GetPlayerId() != omniMan.GetPlayerId()
                             && !player.Passives.IsDead
                             && !UnknownBug.Is(player)
                             && !Madara.IsSealed(player)
                             && !Naruto.IsDispersedClone(player)
                             && !Tigr.IsRoundTenBanned(player, game.RoundNo)
                             && !omniMan.IsTeamMember(game, player.GetPlayerId()))
            .ToList();

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
