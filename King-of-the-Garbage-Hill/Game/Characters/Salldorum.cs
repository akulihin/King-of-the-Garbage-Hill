using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Salldorum
{
    public const int TimeCapsuleMinimumAge = 3;

    public class ShenClass
    {
        public int Charges { get; set; } = 0;
        public int RandomTargetMagnetRound { get; set; } = -1;
        public int HeldPosition { get; set; } = -1;
        public int HoldThroughRound { get; set; } = -1;
    }

    public class TimeCapsuleClass
    {
        public bool Buried { get; set; } = false;
        public int BuriedAtPosition { get; set; } = -1;  // leaderboard position 1-6
        public int BuriedOnRound { get; set; } = -1;
        public bool FirstBlockUsed { get; set; } = false;
        public int FirstConsumedRound { get; set; } = -1;
        public int DrinkCount { get; set; } = 0;
        public int SpeedBonusPending { get; set; } = 0;
    }

    public class ChroniclerClass
    {
        public bool HistoryRewritten { get; set; } = false;
        public int RewrittenRound { get; set; } = -1;
        public List<int> PositionHistory { get; set; } = new(); // index=round-1, value=position
        public Dictionary<int, Dictionary<Guid, List<Guid>>> WinPointRecipients { get; set; } = new();
    }

    public static void RecordWinPointRecipients(
        GamePlayerBridgeClass defeated,
        int roundNumber,
        Guid winnerId,
        IEnumerable<Guid> recipientIds)
    {
        if (defeated.GameCharacter.Name != "Salldorum")
            return;

        var ledger = defeated.Passives.SalldorumChronicler.WinPointRecipients;
        if (!ledger.TryGetValue(roundNumber, out var roundLedger))
        {
            roundLedger = new Dictionary<Guid, List<Guid>>();
            ledger[roundNumber] = roundLedger;
        }

        // Chronicler rewrites one win per distinct enemy, even if that enemy beat
        // Salldorum more than once in the selected round. Keep the recipients of
        // the first such win; an empty list deliberately records a scoreless ally win.
        if (roundLedger.ContainsKey(winnerId))
            return;

        roundLedger[winnerId] = recipientIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    public static bool TryDrinkTimeCapsule(
        GamePlayerBridgeClass player,
        GameClass game,
        bool recoveredFromHistory = false)
    {
        var capsule = player.Passives.SalldorumTimeCapsule;
        var canDrink = capsule.Buried || (recoveredFromHistory && capsule.DrinkCount == 1);
        if (!canDrink || capsule.DrinkCount >= 2)
            return false;

        capsule.Buried = false;
        capsule.DrinkCount++;
        if (capsule.FirstConsumedRound < 0)
            capsule.FirstConsumedRound = game.RoundNo;
        capsule.SpeedBonusPending += 5;

        player.Status.AddBonusPoints(2, "Временная капсула");
        if (recoveredFromHistory)
            player.Status.AddInGamePersonalLogs("Временная капсула: Кола найдена в переписанной истории!\n");
        else
            game.Phrases.SalldorumTimeCapsulePickup.SendLog(player, false);

        return true;
    }

    public static void TryDrinkAvailableTimeCapsule(GamePlayerBridgeClass player, GameClass game)
    {
        if (player.GameCharacter.Name != "Salldorum"
            || player.Passives.PassiveAbilitiesDisabledByKimiko)
            return;

        var capsule = player.Passives.SalldorumTimeCapsule;
        if (!capsule.Buried
            || game.RoundNo - capsule.BuriedOnRound < TimeCapsuleMinimumAge
            || player.Status.GetPlaceAtLeaderBoard() != capsule.BuriedAtPosition)
            return;

        TryDrinkTimeCapsule(player, game);
    }

    public static void ResolveShenDashes(GameClass game)
    {
        foreach (var player in game.PlayersList
                     .Where(holder => holder.GameCharacter.Passive.Any(
                         passive => passive.PassiveName == "Шэн") && !holder.Passives.IsDead)
                     .ToList())
        {
            var shen = player.Passives.SalldorumShen;
            if (shen.Charges <= 0 || player.Status.WhoToAttackThisTurn.Count == 0)
                continue;

            var target = player.Status.WhoToAttackThisTurn
                .Select(targetId => game.PlayersList.Find(candidate => candidate.GetPlayerId() == targetId))
                .FirstOrDefault(candidate => candidate != null
                                             && !candidate.Passives.IsDead
                                             && candidate.GetPlayerId() != player.GetPlayerId());
            if (target == null)
                continue;
            if (UnknownBug.Is(target))
                continue;

            shen.Charges--;
            var originalOrder = game.PlayersList.ToList();
            var originalPosition = player.Status.GetPlaceAtLeaderBoard();
            var selectedPosition = target.Status.GetPlaceAtLeaderBoard();
            if (selectedPosition >= originalPosition)
            {
                player.Status.AddInGamePersonalLogs(
                    $"Шэн: Атака на {target.DiscordUsername}. Цель уже позади, заряд потрачен. Осталось: {shen.Charges}.\n");
                continue;
            }

            var crossedPlayers = originalOrder.Where(candidate =>
                    candidate.Status.GetPlaceAtLeaderBoard() > selectedPosition
                    && candidate.Status.GetPlaceAtLeaderBoard() < originalPosition)
                .ToList();
            var projectedOrder = originalOrder.Where(candidate => candidate.GetPlayerId() != player.GetPlayerId()).ToList();
            projectedOrder.Insert(selectedPosition - 1, player);
            var movesLockedPosition = originalOrder.Any(candidate =>
                candidate.Passives.GoblinZiggurat.IsInZiggurat
                && originalOrder.IndexOf(candidate) != projectedOrder.IndexOf(candidate));
            var movesProtectedBug = originalOrder.Any(candidate =>
                UnknownBug.Is(candidate)
                && originalOrder.IndexOf(candidate) != projectedOrder.IndexOf(candidate));

            if (movesLockedPosition || movesProtectedBug)
            {
                if (movesLockedPosition)
                    player.Status.AddInGamePersonalLogs(
                        $"Шэн: Зиккурат перекрыл прыжок через {target.DiscordUsername}. Заряд потрачен.\n");
                continue;
            }

            ApplyOrder(game, projectedOrder);

            game.Phrases.SalldorumShen.SendLog(player, false);
            shen.RandomTargetMagnetRound = game.RoundNo;
            shen.HeldPosition = selectedPosition;
            shen.HoldThroughRound = game.RoundNo + 1;

            var redirectedActions = 0;
            foreach (var victim in crossedPlayers.Where(candidate =>
                         !candidate.Passives.IsDead
                         && !UnknownBug.Is(candidate)
                         && !(game.RoundNo == 10 && candidate.GameCharacter.Passive.Any(passive =>
                             passive.PassiveName == "Стримснайпят и банят и банят и банят"))))
            {
                var mainAttackIndex = victim.Status.WhoToAttackThisTurn.FindIndex(
                    targetId => targetId != victim.GetPlayerId());
                if (mainAttackIndex < 0)
                    continue;

                victim.Status.WhoToAttackThisTurn[mainAttackIndex] = player.GetPlayerId();
                redirectedActions++;
            }

            player.Status.AddInGamePersonalLogs(
                $"Шэн: Переместился на место {selectedPosition} через {target.DiscordUsername}. " +
                $"Зарядов осталось: {shen.Charges}. Перенаправлено действий: {redirectedActions}.\n");
        }
    }

    public static void ApplyShenPositionHolds(GameClass game)
    {
        foreach (var player in game.PlayersList.Where(holder =>
                     holder.GameCharacter.Passive.Any(passive => passive.PassiveName == "Шэн")).ToList())
        {
            var shen = player.Passives.SalldorumShen;
            if (player.Passives.IsDead || shen.HoldThroughRound < game.RoundNo)
            {
                shen.HeldPosition = -1;
                shen.HoldThroughRound = -1;
                continue;
            }

            if (shen.HeldPosition < 1
                || shen.HeldPosition > game.PlayersList.Count
                || player.Status.GetPlaceAtLeaderBoard() == shen.HeldPosition)
                continue;

            var originalOrder = game.PlayersList.ToList();
            var projectedOrder = originalOrder
                .Where(candidate => candidate.GetPlayerId() != player.GetPlayerId())
                .ToList();
            projectedOrder.Insert(shen.HeldPosition - 1, player);

            var movesLockedPosition = originalOrder.Any(candidate =>
                candidate.Passives.GoblinZiggurat.IsInZiggurat
                && originalOrder.IndexOf(candidate) != projectedOrder.IndexOf(candidate));
            var movesProtectedBug = originalOrder.Any(candidate =>
                UnknownBug.Is(candidate)
                && originalOrder.IndexOf(candidate) != projectedOrder.IndexOf(candidate));
            if (!movesLockedPosition && !movesProtectedBug)
                ApplyOrder(game, projectedOrder);
        }
    }

    private static void ApplyOrder(GameClass game, IEnumerable<GamePlayerBridgeClass> order)
    {
        game.PlayersList.Clear();
        game.PlayersList.AddRange(order);
        for (var index = 0; index < game.PlayersList.Count; index++)
            game.PlayersList[index].Status.SetPlaceAtLeaderBoard(index + 1);
    }

    public static GamePlayerBridgeClass FindRandomTargetMagnet(
        GameClass game,
        GamePlayerBridgeClass effectOwner = null)
    {
        return game.PlayersList
            .Where(player => !player.Passives.IsDead
                             && player.Passives.SalldorumShen.RandomTargetMagnetRound == game.RoundNo
                             && player.GameCharacter.Passive.Any(passive => passive.PassiveName == "Шэн")
                             && (effectOwner == null || player.GetPlayerId() != effectOwner.GetPlayerId()))
            .OrderBy(player => player.Status.GetPlaceAtLeaderBoard())
            .FirstOrDefault();
    }

    public static bool IsRedirectedRandomTarget(
        GameClass game,
        GamePlayerBridgeClass effectOwner,
        GamePlayerBridgeClass candidate,
        IEnumerable<Guid> originalTargets)
    {
        var magnet = FindRandomTargetMagnet(game, effectOwner);
        if (magnet == null)
            return originalTargets.Contains(candidate.GetPlayerId());

        var targets = originalTargets.Distinct().ToList();
        var replacedTarget = targets.FirstOrDefault(targetId => game.PlayersList.Any(player =>
            player.GetPlayerId() == targetId
            && player.GameCharacter.Passive.Any(passive => passive.PassiveName == "Most wanted")));
        if (replacedTarget == Guid.Empty && targets.Count > 0)
            replacedTarget = targets[0];

        return candidate.GetPlayerId() == magnet.GetPlayerId()
               || (candidate.GetPlayerId() != replacedTarget && targets.Contains(candidate.GetPlayerId()));
    }

    public static Guid ResolveRandomTargetId(
        GameClass game,
        GamePlayerBridgeClass effectOwner,
        Guid originalTarget)
    {
        return FindRandomTargetMagnet(game, effectOwner)?.GetPlayerId() ?? originalTarget;
    }

    public static int TakeGeraltContractCount(
        GameClass game,
        GamePlayerBridgeClass geralt,
        GamePlayerBridgeClass candidate)
    {
        var contracts = geralt.Passives.GeraltContracts;
        var magnet = FindRandomTargetMagnet(game, geralt);
        if (magnet != null)
        {
            if (candidate.GetPlayerId() != magnet.GetPlayerId())
                return 0;

            var total = 0;
            foreach (var monsterType in Enum.GetValues<Geralt.MonsterType>())
            {
                total += contracts.GetCount(monsterType);
                contracts.SetCount(monsterType, 0);
            }

            return total;
        }

        var assignedType = candidate.Passives.GeraltMonsterType;
        if (assignedType == null)
            return 0;

        var count = contracts.GetCount(assignedType.Value);
        contracts.SetCount(assignedType.Value, 0);
        return count;
    }

    public static (bool success, string error) RewriteHistory(
        GamePlayerBridgeClass player,
        GameClass game,
        int roundNumber)
    {
        if (player.GameCharacter.Name != "Salldorum")
            return (false, "Only Salldorum can rewrite history");
        if (player.Passives.IsDead)
            return (false, "Dead Salldorum cannot rewrite history");

        var chronicler = player.Passives.SalldorumChronicler;
        if (chronicler.HistoryRewritten)
            return (false, "History has already been rewritten");
        if (game.RoundNo >= 8)
            return (false, "Too late to rewrite history (before round 8 only)");
        if (roundNumber < 1 || roundNumber >= game.RoundNo)
            return (false, "Invalid round number");

        chronicler.HistoryRewritten = true;
        chronicler.RewrittenRound = roundNumber;

        var salloLosses = player.Status.WhoToLostEveryRound
            .Where(x => x.RoundNo == roundNumber)
            .ToList();
        var roundMultiplier = roundNumber switch { <= 4 => 1m, <= 9 => 2m, _ => 4m };
        var roundWinners = salloLosses
            .Select(loss => game.PlayersList.Find(x => x.GetPlayerId() == loss.EnemyId))
            .Where(enemy => enemy != null)
            .DistinctBy(enemy => enemy!.GetPlayerId())
            .ToList();

        decimal totalStolen = 0;
        foreach (var enemy in roundWinners)
        {
            var winnerId = enemy!.GetPlayerId();
            var recipientIds = chronicler.WinPointRecipients.TryGetValue(roundNumber, out var roundLedger)
                               && roundLedger.TryGetValue(winnerId, out var recordedRecipients)
                ? recordedRecipients
                : new List<Guid> { winnerId };

            foreach (var recipientId in recipientIds.Distinct())
            {
                var holder = game.PlayersList.Find(candidate => candidate.GetPlayerId() == recipientId) ?? enemy;
                if (UnknownBug.Is(holder)) continue;

                holder.Status.AddBonusPoints(-roundMultiplier, "Великий летописец");
                player.Status.AddBonusPoints(roundMultiplier, "Великий летописец");
                totalStolen += roundMultiplier;
            }
        }

        player.GameCharacter.AddPsyche(2, "Великий летописец");
        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(2);

        var capsule = player.Passives.SalldorumTimeCapsule;
        var wasAtCapsule = capsule.FirstBlockUsed
                           && chronicler.PositionHistory.Count >= roundNumber
                           && chronicler.PositionHistory[roundNumber - 1] == capsule.BuriedAtPosition;
        var canRecoverConsumedCapsule = capsule.DrinkCount == 1
                                        && capsule.FirstConsumedRound > roundNumber;
        if (wasAtCapsule && (capsule.Buried || canRecoverConsumedCapsule))
        {
            TryDrinkTimeCapsule(player, game, recoveredFromHistory: true);
        }

        player.Status.AddInGamePersonalLogs(
            $"Великий летописец: История раунда {roundNumber} переписана! Украдено {totalStolen} очков.\n");
        game.AddGlobalLogs(Random.Shared.Next(2) == 0
            ? $"Salldorum: Помните {roundNumber} ход? На самом деле в этот день пришло подкрепление из Киева и мы всех победили!"
            : $"Salldorum: А вы знали, что в {roundNumber} ход на самом деле мы подписали мирный договор и этих поражений не было...");

        return (true, null);
    }
}
