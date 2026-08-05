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
        public int DrinkCount { get; set; } = 0;
        public int SpeedBonusPending { get; set; } = 0;
    }

    public class ChroniclerClass
    {
        public bool HistoryRewritten { get; set; } = false;
        public int RewrittenRound { get; set; } = -1;
        public List<int> PositionHistory { get; set; } = new(); // index=round-1, value=position
        public List<HistoricalLossClass> HistoricalLosses { get; set; } = new();
    }

    public class HistoricalLossClass
    {
        public int RoundNumber { get; set; }
        public decimal RewrittenWinPoints { get; set; }
        public decimal RewrittenWinMoral { get; set; }
        public decimal RewrittenWinSkill { get; set; }
        public List<HistoricalPointRecipientClass> PointRecipients { get; set; } = new();
    }

    public class HistoricalPointRecipientClass
    {
        public Guid PlayerId { get; set; }
        public decimal Points { get; set; }
    }

    public static void RecordHistoricalLoss(
        GamePlayerBridgeClass defeated,
        GamePlayerBridgeClass winner,
        GameClass game,
        IEnumerable<Guid> recipientIds,
        bool teamMate)
    {
        if (defeated.GameCharacter.Name != "Salldorum")
            return;

        var rewrittenWinSkill = 0m;
        if (defeated.FightCharacter.GetSkillClass() == "Сила")
        {
            rewrittenWinSkill = 4 * defeated.GameCharacter.GetClassSkillMultiplier();
            var extraSkillMultiplier = defeated.GameCharacter.GetExtraSkillMultiplier();
            if (extraSkillMultiplier > 0)
                rewrittenWinSkill *= extraSkillMultiplier + 1;
        }

        var historicalLoss = new HistoricalLossClass
        {
            RoundNumber = game.RoundNo,
            RewrittenWinPoints = teamMate ? 0 : defeated.Status.GetRoundScoreMultiplier(game),
            RewrittenWinMoral = teamMate || game.RoundNo <= 1
                ? 0
                : Math.Max(0,
                    defeated.Status.GetPlaceAtLeaderBoard() - winner.Status.GetPlaceAtLeaderBoard()),
            RewrittenWinSkill = rewrittenWinSkill,
        };

        historicalLoss.PointRecipients = recipientIds
            .Where(playerId => playerId != Guid.Empty)
            .Distinct()
            .Select(playerId =>
            {
                var recipient = game.PlayersList.Find(candidate => candidate.GetPlayerId() == playerId);
                return new HistoricalPointRecipientClass
                {
                    PlayerId = playerId,
                    Points = recipient?.Status.GetRoundScoreMultiplier(game) ?? 0,
                };
            })
            .Where(recipient => recipient.Points > 0)
            .ToList();

        defeated.Passives.SalldorumChronicler.HistoricalLosses.Add(historicalLoss);
    }

    public static bool TryDrinkTimeCapsule(
        GamePlayerBridgeClass player,
        GameClass game,
        bool freeHistoryActivation = false)
    {
        var capsule = player.Passives.SalldorumTimeCapsule;
        var canDrink = freeHistoryActivation ? capsule.FirstBlockUsed : capsule.Buried;
        if (!canDrink || capsule.DrinkCount >= 2)
            return false;

        if (!freeHistoryActivation)
            capsule.Buried = false;
        capsule.DrinkCount++;
        capsule.SpeedBonusPending += 5;

        player.Status.AddBonusPoints(2, "Временная капсула");
        if (freeHistoryActivation)
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
            var selectedPosition = target.Status.GetPlaceAtLeaderBoard();
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

            var redirectedAction = false;
            if (!target.Passives.IsDead
                && !UnknownBug.Is(target)
                && !Tigr.IsRoundTenBanned(target, game.RoundNo))
            {
                var mainAttackIndex = target.Status.WhoToAttackThisTurn.FindIndex(
                    targetId => targetId != target.GetPlayerId());
                if (mainAttackIndex >= 0)
                {
                    target.Status.WhoToAttackThisTurn[mainAttackIndex] = player.GetPlayerId();
                    redirectedAction = true;
                }
            }

            player.Status.AddInGamePersonalLogs(
                $"Шэн: Переместился на место {selectedPosition} через {target.DiscordUsername}. " +
                $"Зарядов осталось: {shen.Charges}. Действие цели перенаправлено: {(redirectedAction ? "да" : "нет")}.\n");
        }
    }

    public static bool IsNearestLowerEnemy(
        GameClass game,
        GamePlayerBridgeClass passiveHolder,
        GamePlayerBridgeClass attacker)
    {
        var holderPosition = passiveHolder.Status.GetPlaceAtLeaderBoard();
        var nearestLowerEnemy = game.PlayersList
            .Where(candidate => !candidate.Passives.IsDead
                                && candidate.GetPlayerId() != passiveHolder.GetPlayerId()
                                && candidate.Status.GetPlaceAtLeaderBoard() > holderPosition
                                && !passiveHolder.IsTeamMember(game, candidate.GetPlayerId()))
            .OrderBy(candidate => candidate.Status.GetPlaceAtLeaderBoard())
            .FirstOrDefault();

        return nearestLowerEnemy?.GetPlayerId() == attacker.GetPlayerId();
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
            && player.GameCharacter.Passive.Any(
                passive => passive.PassiveName == RickSanchez.MostWanted)));
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
        if (game.RoundNo > 9)
            return (false, "Too late to rewrite history (through round 9 only)");
        if (roundNumber < 1 || roundNumber >= game.RoundNo)
            return (false, "Invalid round number");

        chronicler.HistoryRewritten = true;
        chronicler.RewrittenRound = roundNumber;

        var historicalLosses = chronicler.HistoricalLosses
            .Where(loss => loss.RoundNumber == roundNumber)
            .ToList();

        decimal totalPointsAwarded = 0;
        decimal totalPointsRecalled = 0;
        decimal totalMoralAwarded = 0;
        decimal totalSkillAwarded = 0;
        foreach (var historicalLoss in historicalLosses)
        {
            totalMoralAwarded += historicalLoss.RewrittenWinMoral;
            totalSkillAwarded += historicalLoss.RewrittenWinSkill;

            var recalledAnyPoint = false;
            foreach (var pointRecipient in historicalLoss.PointRecipients
                         .GroupBy(recipient => recipient.PlayerId)
                         .Select(group => group.First()))
            {
                var holder = game.PlayersList.Find(candidate =>
                    candidate.GetPlayerId() == pointRecipient.PlayerId);
                if (holder == null || UnknownBug.Is(holder))
                    continue;
                if (!Homelander.CanTransferFrom(holder, "Великий летописец"))
                    continue;

                holder.Status.AddBonusPoints(-pointRecipient.Points, "Великий летописец");
                totalPointsRecalled += pointRecipient.Points;
                recalledAnyPoint = true;
            }

            if (historicalLoss.RewrittenWinPoints > 0
                && (historicalLoss.PointRecipients.Count == 0 || recalledAnyPoint))
            {
                player.Status.AddBonusPoints(historicalLoss.RewrittenWinPoints, "Великий летописец");
                totalPointsAwarded += historicalLoss.RewrittenWinPoints;
            }
        }

        if (totalMoralAwarded > 0)
            player.GameCharacter.AddMoral(
                totalMoralAwarded, "Великий летописец", isFightMoral: true);

        if (totalSkillAwarded > 0)
        {
            var currentExtraSkillMultiplier = player.GameCharacter.GetExtraSkillMultiplier();
            var skillBeforeCurrentMultiplier = currentExtraSkillMultiplier > 0
                ? totalSkillAwarded / (currentExtraSkillMultiplier + 1)
                : totalSkillAwarded;
            player.GameCharacter.AddExtraSkill(
                skillBeforeCurrentMultiplier, "Великий летописец");
        }

        player.GameCharacter.AddPsyche(2, "Великий летописец");
        player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(2);

        var capsule = player.Passives.SalldorumTimeCapsule;
        var wasAtCapsule = capsule.FirstBlockUsed
                           && chronicler.PositionHistory.Count >= roundNumber
                           && chronicler.PositionHistory[roundNumber - 1] == capsule.BuriedAtPosition;
        if (wasAtCapsule)
            TryDrinkTimeCapsule(player, game, freeHistoryActivation: true);

        player.Status.AddInGamePersonalLogs(
            $"Великий летописец: История раунда {roundNumber} переписана! " +
            $"Поражений обращено в победы: {historicalLosses.Count}. " +
            $"Начислено {totalPointsAwarded} очков, {totalMoralAwarded} Морали и " +
            $"{totalSkillAwarded} Скилла; отозвано {totalPointsRecalled} победных очков.\n");
        game.AddGlobalLogs(Random.Shared.Next(2) == 0
            ? $"Salldorum: Помните {roundNumber} ход? На самом деле в этот день пришло подкрепление из Киева и мы всех победили!"
            : $"Salldorum: А вы знали, что в {roundNumber} ход на самом деле мы подписали мирный договор и этих поражений не было...");

        return (true, null);
    }
}
