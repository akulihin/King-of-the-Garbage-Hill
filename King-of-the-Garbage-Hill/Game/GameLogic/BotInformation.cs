using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

/// <summary>
/// The information boundary for strategic AI. It records only the projection an ordinary player could
/// retain after a resolved round. The shared round-10 Monster hunt at every strict-bot level, all Level 2/3
/// decisions and Legacy+'s fair channels must not reconstruct observations from raw opponent GameCharacter,
/// Passives, Status action flags, or unfiltered cumulative logs.
/// </summary>
public static class BotInformation
{
    public static void CaptureVisibleRound(GamePlayerBridgeClass viewer, GameClass game)
    {
        if (viewer.PlayerType != 404)
            return;

        var completedRound = game.RoundNo - 1;
        if (completedRound <= 0)
            return;
        var memory = viewer.AiKnowledge;
        if (memory.LastCapturedRound >= completedRound)
            return;

        memory.VisibleGlobalLogsByRound[completedRound] = VisibleCurrentGlobalLogs(viewer, game);
        foreach (var staleRound in memory.VisibleGlobalLogsByRound.Keys
                     .Where(round => round < completedRound - 9).ToList())
            memory.VisibleGlobalLogsByRound.Remove(staleRound);

        // Leaderboard place is public. Keeping the historical sequence lets L3 recognize rule-shaped
        // patterns (for example, a player repeatedly forced to place 6) without reading an identity.
        foreach (var opponent in game.PlayersList.Where(player =>
                     player.GetPlayerId() != viewer.GetPlayerId()))
            memory.Opponent(opponent.GetPlayerId()).PlacesByRound[completedRound] =
                opponent.Status.GetPlaceAtLeaderBoard();

        foreach (var fight in game.WebFightLog)
        {
            var viewerParticipated = FightSideMatches(
                                         fight, viewer, game, fight.AttackerPlayerId, fight.AttackerName)
                                     || FightSideMatches(
                                         fight, viewer, game, fight.DefenderPlayerId, fight.DefenderName);
            if (fight.HiddenFromNonAdmin && !viewerParticipated)
                continue;

            var attacker = ResolveFightPlayer(game, fight, fight.AttackerPlayerId, fight.AttackerName);
            var defender = ResolveFightPlayer(game, fight, fight.DefenderPlayerId, fight.DefenderName);
            if (attacker == null || defender == null)
                continue;

            if (attacker.GetPlayerId() != viewer.GetPlayerId())
            {
                var attackerMemory = memory.Opponent(attacker.GetPlayerId());
                AddRoundCount(attackerMemory.AttacksByRound, completedRound);
                if (defender.GetPlayerId() == viewer.GetPlayerId())
                    AddRoundCount(attackerMemory.AttacksOnViewerByRound, completedRound);
            }

            if (defender.GetPlayerId() != viewer.GetPlayerId())
            {
                var defenderMemory = memory.Opponent(defender.GetPlayerId());
                AddRoundCount(defenderMemory.TimesTargetedByRound, completedRound);
                if (fight.Outcome is "block" or "skip")
                    AddRoundCount(defenderMemory.NonFightsByRound, completedRound);
            }

            if (fight.Outcome == "win")
            {
                RecordPublicResult(memory, attacker.GetPlayerId(), defender.GetPlayerId(), completedRound);
            }
            else if (fight.Outcome == "loss")
            {
                RecordPublicResult(memory, defender.GetPlayerId(), attacker.GetPlayerId(), completedRound);
            }

            if (!viewerParticipated)
                continue;

            var viewerWasAttacker = FightSideMatches(
                fight, viewer, game, fight.AttackerPlayerId, fight.AttackerName);
            var opponent = viewerWasAttacker ? defender : attacker;
            var observed = memory.Opponent(opponent.GetPlayerId());
            AddRoundCount(observed.FightsWithViewerByRound, completedRound);
            if (fight.Outcome is not ("win" or "loss"))
                continue;

            observed.LastObservedJustice = viewerWasAttacker ? fight.JusticeTarget : fight.JusticeMe;
            observed.LastObservedJusticeRound = completedRound;
            observed.LastObservedClass = viewerWasAttacker ? fight.DefenderClass : fight.AttackerClass;
            observed.LastObservedFightRound = completedRound;

            var versatility = fight.VersatilityIntel + fight.VersatilityStr + fight.VersatilitySpeed;
            var scale = fight.ScaleMe - fight.ScaleTarget;
            var psyche = PsycheTerm(fight.PsycheDifference);
            var edge = scale + versatility * 2 + psyche;
            observed.LastObservedFightEdge = viewerWasAttacker ? edge : -edge;
        }

        memory.LastCapturedRound = completedRound;
    }

    private static bool FightSideMatches(
        FightEntryDto fight,
        GamePlayerBridgeClass viewer,
        GameClass game,
        Guid? sidePlayerId,
        string legacyUsername)
    {
        if (viewer == null)
            return false;
        if (HasStructuredFightIds(fight))
            return sidePlayerId == viewer.GetPlayerId();
        return !string.IsNullOrEmpty(legacyUsername)
               && string.Equals(legacyUsername, viewer.DiscordUsername, StringComparison.Ordinal)
               && game.PlayersList.Count(player =>
                   string.Equals(player.DiscordUsername, legacyUsername, StringComparison.Ordinal)) == 1;
    }

    private static GamePlayerBridgeClass ResolveFightPlayer(
        GameClass game,
        FightEntryDto fight,
        Guid? sidePlayerId,
        string legacyUsername)
    {
        if (HasStructuredFightIds(fight))
            return sidePlayerId.HasValue
                ? game.PlayersList.Find(player => player.GetPlayerId() == sidePlayerId.Value)
                : null;

        var matches = game.PlayersList.Where(player =>
            string.Equals(player.DiscordUsername, legacyUsername, StringComparison.Ordinal)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool HasStructuredFightIds(FightEntryDto fight) =>
        fight.AttackerPlayerId.HasValue
        || fight.DefenderPlayerId.HasValue
        || fight.WinnerPlayerId.HasValue;

    public static string VisibleCurrentGlobalLogs(GamePlayerBridgeClass viewer, GameClass game)
    {
        var logs = game.ApplyProGlobalLogVisibility(game.GetGlobalLogs(), viewer);
        foreach (var hidden in game.HiddenGlobalLogSnippets)
            logs = logs.Replace(hidden, "", StringComparison.Ordinal);

        if (viewer.GameCharacter.Passive.Any(passive => passive.PassiveName == "Гений"))
            foreach (var hidden in game.KiraHiddenLogSnippets)
                logs = logs.Replace(hidden, "", StringComparison.Ordinal);

        if (viewer.IsProMode && viewer.PlayerType != 2)
            logs = logs
                .Replace("(Блок)", "(?)", StringComparison.Ordinal)
                .Replace("(Скип)", "(?)", StringComparison.Ordinal)
                .Replace("(Block)", "(?)", StringComparison.Ordinal)
                .Replace("(Skip)", "(?)", StringComparison.Ordinal);

        return logs
            .Replace(UnknownBug.CharacterName, "???", StringComparison.Ordinal)
            .Replace(UnknownBug.LegacyCharacterName, "???", StringComparison.Ordinal);
    }

    public static string VisibleGlobalHistory(GamePlayerBridgeClass viewer)
        => string.Join("\n", viewer.AiKnowledge.VisibleGlobalLogsByRound
            .OrderBy(entry => entry.Key)
            .Select(entry => entry.Value));

    public static BotPredictionEvidence PredictionFor(GamePlayerBridgeClass viewer, Guid targetId)
        => viewer.AiKnowledge.PredictionEvidence.GetValueOrDefault(targetId);

    public static void RecordPrediction(GamePlayerBridgeClass viewer, Guid targetId, string characterName,
        int confidence, string evidence, int round, bool exactReveal = false)
    {
        if (UnknownBug.Is(characterName) || Sakura.Is(characterName))
            return;

        var old = viewer.AiKnowledge.PredictionEvidence.GetValueOrDefault(targetId);
        if (old is { IsExactReveal: true } && !exactReveal)
            return;
        if (old != null && old.Confidence > confidence && !exactReveal)
            return;

        ReplacePrediction(viewer, targetId, characterName, confidence, evidence, round, exactReveal);
    }

    /// <summary>
    /// Replaces both the bot's submitted row and the evidence consumed by later AI decisions. Use this
    /// only for authoritative character/script cleanup that intentionally wins over ordinary confidence.
    /// </summary>
    public static void ReplacePrediction(GamePlayerBridgeClass viewer, Guid targetId, string characterName,
        int confidence, string evidence, int round, bool exactReveal = false)
    {
        var displacedKiraTargets = Kira.SetPrediction(viewer, targetId, characterName);
        foreach (var displacedTargetId in displacedKiraTargets)
            viewer.AiKnowledge.PredictionEvidence.Remove(displacedTargetId);

        viewer.AiKnowledge.PredictionEvidence[targetId] = new BotPredictionEvidence
        {
            CharacterName = characterName,
            Confidence = Math.Clamp(confidence, 0, 100),
            Evidence = evidence,
            RoundUpdated = round,
            IsExactReveal = exactReveal,
        };
    }

    public static void EnforceSingleKiraPrediction(GamePlayerBridgeClass viewer)
    {
        foreach (var removedTargetId in Kira.EnforceSingleKiraPrediction(viewer))
            viewer.AiKnowledge.PredictionEvidence.Remove(removedTargetId);
    }

    public static void RemovePrediction(GamePlayerBridgeClass viewer, Guid targetId)
    {
        viewer.AiKnowledge.PredictionEvidence.Remove(targetId);
        viewer.Predict.RemoveAll(prediction => prediction.PlayerId == targetId);
    }

    public static decimal RecentAverage(Dictionary<int, int> values, int currentRound, int horizon)
    {
        decimal weighted = 0;
        decimal weightTotal = 0;
        for (var age = 1; age <= horizon; age++)
        {
            var round = currentRound - age;
            var weight = horizon - age + 1;
            weighted += values.GetValueOrDefault(round) * weight;
            weightTotal += weight;
        }

        return weightTotal == 0 ? 0 : weighted / weightTotal;
    }

    public static decimal DefenseRate(BotOpponentKnowledge knowledge, int currentRound, int horizon)
    {
        decimal defenses = 0;
        decimal targeted = 0;
        for (var age = 1; age <= horizon; age++)
        {
            var round = currentRound - age;
            var weight = horizon - age + 1;
            defenses += knowledge.NonFightsByRound.GetValueOrDefault(round) * weight;
            targeted += knowledge.TimesTargetedByRound.GetValueOrDefault(round) * weight;
        }

        return targeted <= 0 ? 0 : Math.Clamp(defenses / targeted, 0, 1);
    }

    private static void AddRoundCount(Dictionary<int, int> values, int round)
        => values[round] = values.GetValueOrDefault(round) + 1;

    private static void RecordPublicResult(BotKnowledgeState memory, Guid winnerId, Guid loserId, int round)
    {
        if (memory.Opponents.TryGetValue(winnerId, out var winner))
            AddRoundCount(winner.WinsByRound, round);
        if (memory.Opponents.TryGetValue(loserId, out var loser))
            AddRoundCount(loser.LossesByRound, round);
    }

    private static decimal PsycheTerm(int difference) => difference switch
    {
        > 0 and <= 3 => 1,
        >= 4 and <= 5 => 2,
        >= 6 => 4,
        < 0 and >= -3 => -1,
        >= -5 and <= -4 => -2,
        <= -6 => -4,
        _ => 0,
    };
}
