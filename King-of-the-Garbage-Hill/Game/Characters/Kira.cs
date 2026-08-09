using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Kira
{
    public const string CharacterName = "Кира";
    public const string DeathNoteInterruptedMessageKey = "kotgh.gameplay.kira.deathNoteInterrupted";

    public enum PredictionConfirmationSource
    {
        HumanAction,
        StrictBot,
    }

    public static bool IsKiraGuess(string characterName) =>
        string.Equals(characterName, CharacterName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this seat owns an ordinary prediction sheet that it can actually use. L assignment uses
    /// this capability check instead of guessing from the character's chronology or player type.
    /// </summary>
    public static bool CanMakeOrdinaryPredictions(
        GamePlayerBridgeClass player,
        bool ordinaryPredictionsEnabled = true) =>
        player != null
        && !player.Passives.IsDead
        && ordinaryPredictionsEnabled
        && !player.GameCharacter.DoomRollMode
        && !UnknownBug.Is(player)
        && !Madara.IsMadara(player)
        && player.GameCharacter.Passive.All(passive =>
            passive.PassiveName is not ("Тетрадь смерти" or "Булькает" or "AdminPlayerType"));

    /// <summary>
    /// Upserts one prediction while enforcing Kira's special one-candidate rule. Submitted rows and
    /// bot PredictionEvidence are cleared together so retained AI evidence cannot restore an old suspect.
    /// </summary>
    public static void SetPrediction(
        GamePlayerBridgeClass player,
        Guid targetId,
        string characterName)
    {
        var displacedKiraTargets = new List<Guid>();
        if (IsKiraGuess(characterName))
        {
            displacedKiraTargets = player.Predict
                .Where(prediction => prediction.PlayerId != targetId
                                     && IsKiraGuess(prediction.CharacterName))
                .Select(prediction => prediction.PlayerId)
                .Distinct()
                .ToList();
            player.Predict.RemoveAll(prediction => prediction.PlayerId != targetId
                                                      && IsKiraGuess(prediction.CharacterName));
            foreach (var displacedTargetId in displacedKiraTargets)
                player.AiKnowledge.PredictionEvidence.Remove(displacedTargetId);
        }

        player.Predict.RemoveAll(prediction => prediction.PlayerId == targetId);
        player.AiKnowledge.PredictionEvidence.Remove(targetId);
        player.Predict.Add(new PredictClass(characterName, targetId));
        EnforceSingleKiraPrediction(player);
    }

    /// <summary>Compatibility cleanup for an old sheet that already contains several Kira rows.</summary>
    public static void EnforceSingleKiraPrediction(GamePlayerBridgeClass player)
    {
        var kiraRows = player.Predict.Where(prediction => IsKiraGuess(prediction.CharacterName)).ToList();
        if (kiraRows.Count <= 1)
            return;

        var retainedTarget = kiraRows[^1].PlayerId;
        var removedTargets = kiraRows
            .Where(prediction => prediction.PlayerId != retainedTarget)
            .Select(prediction => prediction.PlayerId)
            .Distinct()
            .ToList();
        player.Predict.RemoveAll(prediction => IsKiraGuess(prediction.CharacterName)
                                               && prediction.PlayerId != retainedTarget);
        foreach (var removedTargetId in removedTargets)
            player.AiKnowledge.PredictionEvidence.Remove(removedTargetId);
    }

    public static bool HasCorrectKiraPrediction(
        GamePlayerBridgeClass predictor,
        GamePlayerBridgeClass kira) =>
        predictor.Predict.Any(prediction =>
            prediction.PlayerId == kira.GetPlayerId()
            && IsKiraGuess(prediction.CharacterName));

    /// <summary>
    /// Freezes the L identity chosen by round-scoped random-target redirection before any fight in the
    /// simultaneous batch can kill that redirect holder.
    /// </summary>
    public static void CaptureActiveLForRound(GameClass game)
    {
        lock (game)
        {
            foreach (var kira in game.PlayersList.Where(candidate =>
                         candidate.Passives.KiraL.LPlayerId != Guid.Empty))
            {
                var state = kira.Passives.KiraL;
                state.ActiveLPlayerId = Salldorum.ResolveRandomTargetId(
                    game, kira, state.LPlayerId);
                state.ActiveLRound = game.RoundNo;
            }
        }
    }

    public static Guid ResolveActiveLPlayerId(GameClass game, GamePlayerBridgeClass kira)
    {
        lock (game)
        {
            var state = kira.Passives.KiraL;
            if (state.ActiveLRound == game.RoundNo && state.ActiveLPlayerId != Guid.Empty)
                return state.ActiveLPlayerId;

            return state.LPlayerId == Guid.Empty
                ? Guid.Empty
                : Salldorum.ResolveRandomTargetId(game, kira, state.LPlayerId);
        }
    }

    /// <summary>
    /// Records the round-eight prediction sheet that was actually confirmed. This is deliberately
    /// separate from Status.ConfirmedPredict, which is also set by timeouts and dead-player auto-ready.
    /// </summary>
    public static bool TryFinalizePredictionConfirmation(
        GameClass game,
        GamePlayerBridgeClass predictor,
        PredictionConfirmationSource source)
    {
        if (game == null || predictor == null)
            return false;

        lock (game)
        {
            // Ownership can change when a disconnected seat is reclaimed. Validate every mutable
            // prerequisite under the same lock that snapshots the final sheet.
            if (game.RoundNo != 8 || predictor.Passives.IsDead)
                return false;
            if (game.IsAramMode
                || predictor.GameCharacter.DoomRollMode
                || UnknownBug.Is(predictor)
                || Madara.IsMadara(predictor)
                || predictor.GameCharacter.Passive.Any(passive =>
                    passive.PassiveName is "Тетрадь смерти" or "Булькает" or "AdminPlayerType"))
                return false;
            if ((source == PredictionConfirmationSource.HumanAction && predictor.PlayerType == 404)
                || (source == PredictionConfirmationSource.StrictBot && predictor.PlayerType != 404))
                return false;
            if (source == PredictionConfirmationSource.HumanAction
                && (!game.IsCheckIfReady || predictor.Status.ConfirmedPredict))
                return false;

            var states = game.PlayersList
                .Where(candidate => candidate.Passives.KiraL.LPlayerId != Guid.Empty)
                .Select(candidate => (Kira: candidate, State: candidate.Passives.KiraL))
                .ToList();
            var predictorId = predictor.GetPlayerId();
            if (source == PredictionConfirmationSource.StrictBot
                && states.Count > 0
                && states.All(entry =>
                    entry.State.ConfirmedPredictionPlayerIds.Contains(predictorId)))
                return false;

            EnforceSingleKiraPrediction(predictor);
            Homelander.RecordConfirmedPrediction(game, predictor);
            foreach (var entry in states)
            {
                if (!entry.State.ConfirmedPredictionPlayerIds.Add(predictorId))
                    continue;
                if (HasCorrectKiraPrediction(predictor, entry.Kira))
                    entry.State.ConfirmedCorrectKiraGuessPlayerIds.Add(predictorId);
            }

            predictor.Status.ConfirmedPredict = true;
            return true;
        }
    }

    public static void ClearPredictionConfirmation(
        GameClass game,
        GamePlayerBridgeClass predictor)
    {
        if (game == null || predictor == null)
            return;

        lock (game)
        {
            var predictorId = predictor.GetPlayerId();
            foreach (var kira in game.PlayersList.Where(candidate =>
                         candidate.Passives.KiraL.LPlayerId != Guid.Empty))
            {
                kira.Passives.KiraL.ConfirmedPredictionPlayerIds.Remove(predictorId);
                kira.Passives.KiraL.ConfirmedCorrectKiraGuessPlayerIds.Remove(predictorId);
            }
        }
    }

    /// <summary>
    /// Freezes L's sheet at the first real death before the generic dead-player auto-confirm runs.
    /// Immediate death prevention (for example Shisui Izanagi) never reaches this state.
    /// </summary>
    public static void CaptureLDeathPredictionStates(GameClass game)
    {
        lock (game)
        {
            foreach (var lPlayer in game.PlayersList.Where(candidate =>
                         candidate.Passives.IsDead
                         || (candidate.Passives.JonSnow.WatchEnded
                             && candidate.Passives.JonSnow.WatchDeathRound == game.RoundNo)))
                CaptureLDeathPredictionStateUnsafe(game, lPlayer);
        }
    }

    public static void CaptureLDeathPredictionState(
        GameClass game,
        GamePlayerBridgeClass lPlayer)
    {
        if (game == null || lPlayer == null)
            return;

        lock (game)
            CaptureLDeathPredictionStateUnsafe(game, lPlayer);
    }

    private static void CaptureLDeathPredictionStateUnsafe(
        GameClass game,
        GamePlayerBridgeClass lPlayer)
    {
        var lPlayerId = lPlayer.GetPlayerId();
        foreach (var kira in game.PlayersList.Where(candidate =>
                     candidate.Passives.KiraL.LPlayerId == lPlayerId))
        {
            var state = kira.Passives.KiraL;
            if (state.LDeathObserved)
                continue;

            state.LDeathObserved = true;
            state.LPredictedKiraAtDeath = HasCorrectKiraPrediction(lPlayer, kira);
            state.LConfirmedPredictionsAtDeath =
                state.ConfirmedPredictionPlayerIds.Contains(lPlayerId);
            state.LConfirmedCorrectKiraAtDeath =
                state.ConfirmedCorrectKiraGuessPlayerIds.Contains(lPlayerId);
        }
    }

    /// <summary>
    /// Resolves the arrest evidence exactly once after the round-eight Confirm deadline. If Izanagi
    /// prevents the lethal arrest, the frozen qualification remains available on the next opening.
    /// </summary>
    public static bool IsArrestQualified(GameClass game, GamePlayerBridgeClass kira)
    {
        lock (game)
        {
            var state = kira.Passives.KiraL;
            if (state.ArrestQualificationResolved)
                return state.ArrestQualified;
            if (game.RoundNo < 9)
                return false;

            state.ArrestQualificationResolved = true;
            if (state.LPlayerId == Guid.Empty)
                return false;

            var lPlayer = game.PlayersList.Find(candidate => candidate.GetPlayerId() == state.LPlayerId);
            if (lPlayer == null)
                return false;

            if (!state.LDeathObserved)
            {
                state.ArrestQualified =
                    state.ConfirmedCorrectKiraGuessPlayerIds.Contains(lPlayer.GetPlayerId());
                return state.ArrestQualified;
            }

            if (state.LConfirmedPredictionsAtDeath)
            {
                state.ArrestQualified = state.LConfirmedCorrectKiraAtDeath;
                return state.ArrestQualified;
            }

            if (!state.LPredictedKiraAtDeath)
                return false;

            var survivingInvestigators = game.PlayersList.Where(candidate =>
                    candidate.GetPlayerId() != kira.GetPlayerId()
                    && candidate.GetPlayerId() != state.LPlayerId
                    && !candidate.Passives.IsDead)
                .ToList();
            state.ArrestQualified = survivingInvestigators.Count > 0
                                    && survivingInvestigators.All(candidate =>
                                        state.ConfirmedCorrectKiraGuessPlayerIds.Contains(
                                            candidate.GetPlayerId()));
            return state.ArrestQualified;
        }
    }

    public static bool IsCurrentDeathNoteEntryInterrupted(
        GameClass game,
        GamePlayerBridgeClass kira,
        GamePlayerBridgeClass target)
    {
        if (target.Status.WhoToAttackThisTurn.Contains(kira.GetPlayerId()))
            return true;

        var lState = kira.Passives.KiraL;
        if (lState.LPlayerId == Guid.Empty)
            return false;

        var activeL = ResolveActiveLPlayerId(game, kira);
        return kira.Status.WhoToLostEveryRound.Any(result =>
            result.RoundNo == game.RoundNo && result.EnemyId == activeL);
    }

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
        public int IdentityRevealSerial { get; set; }
        public bool IsArrested { get; set; } = false;
        public HashSet<Guid> ConfirmedPredictionPlayerIds { get; set; } = new();
        public HashSet<Guid> ConfirmedCorrectKiraGuessPlayerIds { get; set; } = new();
        public Guid ActiveLPlayerId { get; set; } = Guid.Empty;
        public int ActiveLRound { get; set; } = -1;
        public bool LDeathObserved { get; set; }
        public bool LPredictedKiraAtDeath { get; set; }
        public bool LConfirmedPredictionsAtDeath { get; set; }
        public bool LConfirmedCorrectKiraAtDeath { get; set; }
        public bool ArrestQualificationResolved { get; set; }
        public bool ArrestQualified { get; set; }
    }
}
