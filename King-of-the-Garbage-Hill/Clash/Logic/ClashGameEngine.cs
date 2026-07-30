using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Clash.Models;

namespace King_of_the_Garbage_Hill.Clash.Logic;

public static class ClashGameEngine
{
    private const int SpeedStepMs = 500;

    private sealed class BattleIntent
    {
        public ClashUnit Actor { get; init; }
        public ClashUnit Target { get; init; }
        public int Speed { get; init; }
        public bool IsRanged { get; init; }
        public bool IsReloading { get; init; }
    }

    private sealed class AdvanceClaim
    {
        public ClashUnit Attacker { get; init; }
        public ClashUnit Target { get; init; }
        public int ImpactOffsetMs { get; init; }
    }

    private sealed class UnopposedAdvanceClaim
    {
        public ClashUnit Actor { get; init; }
        public int FromBoardRow { get; init; }
        public int ToBoardRow { get; init; }
        public int ImpactOffsetMs { get; init; }
    }

    private sealed class HitOutcome
    {
        public BattleIntent Intent { get; init; }
        public bool Blocked { get; init; }
        public bool Dodged { get; init; }
        public int Damage { get; init; }
        public bool AppliesBleed { get; init; }
    }

    public static bool ValidateDimensions(int width, int length, out string error)
    {
        if (width is < ClashCatalog.MinWidth or > ClashCatalog.MaxWidth)
        {
            error = $"Ширина поля должна быть от {ClashCatalog.MinWidth} до {ClashCatalog.MaxWidth}.";
            return false;
        }

        if (length is < ClashCatalog.MinLength or > ClashCatalog.MaxLength)
        {
            error = $"Длина поля должна быть от {ClashCatalog.MinLength} до {ClashCatalog.MaxLength}.";
            return false;
        }

        error = null;
        return true;
    }

    public static int LocalToBoardRow(ClashGame game, ClashSide side, int localRow)
    {
        return side == ClashSide.Host
            ? game.Length - 1 - localRow
            : game.Length + localRow;
    }

    public static int BoardRowToLocalRow(ClashGame game, ClashSide side, int boardRow)
    {
        return side == ClashSide.Host
            ? game.Length - 1 - boardRow
            : boardRow - game.Length;
    }

    public static ClashSide TerritoryFor(ClashGame game, int boardRow)
    {
        return boardRow < game.Length ? ClashSide.Host : ClashSide.Guest;
    }

    public static void InstantiateArmies(ClashGame game)
    {
        game.Units.Clear();
        AddArmy(game, game.Host, ClashSide.Host);
        AddArmy(game, game.Guest, ClashSide.Guest);
        game.Host.FrontConfirmed = false;
        game.Guest.FrontConfirmed = false;
        game.Phase = ClashGamePhase.InitialFrontPlacement;
        game.CurrentTurnPlayerId = null;
        Touch(game);
    }

    private static void AddArmy(ClashGame game, ClashPlayer player, ClashSide side)
    {
        for (var index = 0; index < player.ArmyDefinitionIds.Count; index++)
        {
            var definitionId = player.ArmyDefinitionIds[index];
            var definition = ClashCatalog.Get(definitionId);
            if (definition == null) continue;
            game.Units.Add(new ClashUnit
            {
                InstanceId = $"{side.ToString().ToLowerInvariant()}-{index + 1}-{Guid.NewGuid():N}"[..24],
                DefinitionId = definition.Id,
                OwnerId = player.PlayerId,
                OwnerSide = side,
                Hp = definition.MaxHp,
                ShieldCharges = definition.ShieldCharges,
                DodgeCharges = definition.DodgeCharges,
                BleedCharges = definition.AppliesBleed ? 1 : 0,
            });
        }
    }

    public static int? RequiredPlacementRow(ClashGame game, string playerId)
    {
        var player = game.GetPlayer(playerId);
        if (player == null) return null;

        return game.Phase switch
        {
            ClashGamePhase.InitialFrontPlacement when !player.FrontConfirmed => 0,
            ClashGamePhase.GuestSecondRowPlacement when player == game.Guest => 1,
            ClashGamePhase.HostSecondRowPlacement when player == game.Host => 1,
            ClashGamePhase.GuestThirdRowPlacement when player == game.Guest => 2,
            ClashGamePhase.HostThirdRowPlacement when player == game.Host => 2,
            _ => null,
        };
    }

    public static bool CanPlaceInitialUnit(ClashGame game, string playerId)
    {
        var requiredRow = RequiredPlacementRow(game, playerId);
        if (requiredRow == null) return false;
        if (game.Phase == ClashGamePhase.InitialFrontPlacement) return true;
        return game.CurrentTurnPlayerId == playerId;
    }

    public static string PlaceInitialUnit(
        ClashGame game,
        string playerId,
        string unitInstanceId,
        int localRow,
        int column)
    {
        if (!CanPlaceInitialUnit(game, playerId))
            return "Сейчас нельзя выставлять юнита.";

        var requiredRow = RequiredPlacementRow(game, playerId);
        if (requiredRow != localRow)
            return $"Сейчас заполняется ряд {requiredRow.GetValueOrDefault() + 1}.";
        if (column < 0 || column >= game.Width)
            return "Клетка находится за пределами поля.";

        var playerSide = game.GetSide(playerId);
        if (playerSide == null) return "Игрок не участвует в этой игре.";
        var unit = game.Units.FirstOrDefault(candidate =>
            candidate.InstanceId == unitInstanceId && candidate.OwnerId == playerId);
        if (unit == null) return "Юнит не найден в вашей руке.";
        if (!unit.Alive || unit.Deployed) return "Этот юнит уже выставлен.";

        var boardRow = LocalToBoardRow(game, playerSide.Value, localRow);
        if (Occupied(game, boardRow, column))
            return "Клетка уже занята.";

        unit.BoardRow = boardRow;
        unit.Column = column;
        unit.Deployed = true;
        unit.IsVisible = false;
        return null;
    }

    public static string RemoveInitialUnit(ClashGame game, string playerId, string unitInstanceId)
    {
        if (!CanPlaceInitialUnit(game, playerId))
            return "Сейчас нельзя менять расстановку.";

        var unit = game.Units.FirstOrDefault(candidate =>
            candidate.InstanceId == unitInstanceId && candidate.OwnerId == playerId);
        if (unit == null || !unit.Deployed || unit.IsVisible)
            return "Этот юнит уже подтверждён или не выставлен.";

        var requiredRow = RequiredPlacementRow(game, playerId);
        var side = game.GetSide(playerId);
        if (requiredRow == null || side == null ||
            unit.BoardRow != LocalToBoardRow(game, side.Value, requiredRow.Value))
            return "Можно убрать юнита только из текущего ряда.";

        unit.BoardRow = null;
        unit.Column = null;
        unit.Deployed = false;
        return null;
    }

    public static ClashResolutionDto ConfirmPlacement(ClashGame game, string playerId, out string error)
    {
        error = null;
        if (!CanPlaceInitialUnit(game, playerId))
        {
            error = "Сейчас нельзя подтверждать расстановку.";
            return null;
        }

        var side = game.GetSide(playerId);
        var row = RequiredPlacementRow(game, playerId);
        if (side == null || row == null)
        {
            error = "Игрок не участвует в текущем этапе.";
            return null;
        }

        var boardRow = LocalToBoardRow(game, side.Value, row.Value);
        var placed = game.Units.Count(unit =>
            unit.OwnerId == playerId && unit.Alive && unit.Deployed && unit.BoardRow == boardRow);
        if (placed != game.Width)
        {
            error = $"Ряд должен быть заполнен полностью: {placed}/{game.Width}.";
            return null;
        }

        switch (game.Phase)
        {
            case ClashGamePhase.InitialFrontPlacement:
            {
                game.GetPlayer(playerId).FrontConfirmed = true;
                if (game.Host.FrontConfirmed && game.Guest.FrontConfirmed)
                {
                    RevealRow(game, game.Host, 0);
                    RevealRow(game, game.Guest, 0);
                    game.Phase = ClashGamePhase.GuestSecondRowPlacement;
                    game.CurrentTurnPlayerId = game.Guest.PlayerId;
                }
                break;
            }
            case ClashGamePhase.GuestSecondRowPlacement:
                RevealRow(game, game.Guest, 1);
                game.Phase = ClashGamePhase.HostSecondRowPlacement;
                game.CurrentTurnPlayerId = game.Host.PlayerId;
                break;
            case ClashGamePhase.HostSecondRowPlacement:
                RevealRow(game, game.Host, 1);
                game.Phase = ClashGamePhase.GuestThirdRowPlacement;
                game.CurrentTurnPlayerId = game.Guest.PlayerId;
                break;
            case ClashGamePhase.GuestThirdRowPlacement:
                RevealRow(game, game.Guest, 2);
                game.Phase = ClashGamePhase.HostThirdRowPlacement;
                game.CurrentTurnPlayerId = game.Host.PlayerId;
                break;
            case ClashGamePhase.HostThirdRowPlacement:
                RevealRow(game, game.Host, 2);
                game.Phase = ClashGamePhase.ResolvingClash;
                game.CurrentTurnPlayerId = null;
                Touch(game);
                return ResolveClash(game);
        }

        Touch(game);
        return null;
    }

    private static void RevealRow(ClashGame game, ClashPlayer player, int localRow)
    {
        var side = player.IsHost ? ClashSide.Host : ClashSide.Guest;
        var boardRow = LocalToBoardRow(game, side, localRow);
        foreach (var unit in game.Units.Where(unit =>
                     unit.OwnerId == player.PlayerId && unit.Deployed && unit.BoardRow == boardRow))
            unit.IsVisible = true;
    }

    public static string PlaceReinforcement(
        ClashGame game,
        string playerId,
        string unitInstanceId,
        int localRow,
        int column)
    {
        if (!IsReinforcementTurn(game, playerId))
            return "Сейчас не ваш ход подкрепления.";
        if (localRow < 2 || localRow >= game.Length)
            return $"Подкрепление можно выставить только в ряды 3–{game.Length}.";
        if (column < 0 || column >= game.Width)
            return "Клетка находится за пределами поля.";

        var side = game.GetSide(playerId);
        var unit = game.Units.FirstOrDefault(candidate =>
            candidate.InstanceId == unitInstanceId && candidate.OwnerId == playerId);
        if (side == null || unit == null) return "Юнит не найден в вашей руке.";
        if (!unit.Alive || unit.Deployed) return "Этот юнит уже выставлен.";

        var boardRow = LocalToBoardRow(game, side.Value, localRow);
        if (Occupied(game, boardRow, column))
            return "Клетка уже занята.";

        unit.BoardRow = boardRow;
        unit.Column = column;
        unit.Deployed = true;
        unit.IsVisible = true;
        CompleteReinforcementTurn(game);
        Touch(game);
        return null;
    }

    public static bool IsReinforcementTurn(ClashGame game, string playerId)
    {
        return game.CurrentTurnPlayerId == playerId &&
               ((game.Phase == ClashGamePhase.GuestReinforcement && game.Guest.PlayerId == playerId) ||
                (game.Phase == ClashGamePhase.HostReinforcement && game.Host.PlayerId == playerId));
    }

    public static string ContinueReinforcement(ClashGame game, string playerId)
    {
        if (!IsReinforcementTurn(game, playerId))
            return "Сейчас не ваш ход подкрепления.";
        CompleteReinforcementTurn(game);
        Touch(game);
        return null;
    }

    private static void CompleteReinforcementTurn(ClashGame game)
    {
        if (game.Phase == ClashGamePhase.GuestReinforcement)
        {
            game.Phase = ClashGamePhase.HostReinforcement;
            game.CurrentTurnPlayerId = game.Host.PlayerId;
            return;
        }

        BeginActiveExchange(game);
    }

    private static void BeginActiveExchange(ClashGame game)
    {
        foreach (var player in game.GetPlayers())
        {
            player.ActiveSelectionsUsed = 0;
            player.UsedActiveKeys.Clear();
            player.RepeatedActiveUsed = false;
            player.ActiveDone = false;
        }

        game.ActivePassStreak = 0;
        game.Phase = ClashGamePhase.ActiveExchange;
        game.CurrentTurnPlayerId = game.Guest.PlayerId;
    }

    public static int ActiveSelectionLimit(int morale)
    {
        return Math.Clamp(morale, 0, 4);
    }

    public static bool ActiveEffectsDoubled(int morale)
    {
        return Math.Clamp(morale, 0, 5) == 5;
    }

    public static ClashResolutionDto ContinueActiveExchange(
        ClashGame game,
        string playerId,
        out string error)
    {
        error = null;
        if (game.Phase != ClashGamePhase.ActiveExchange ||
            game.CurrentTurnPlayerId != playerId)
        {
            error = "Сейчас не ваша очередь активок.";
            return null;
        }

        var player = game.GetPlayer(playerId);
        player.ActiveDone = true;
        game.ActivePassStreak++;
        var opponent = game.GetOpponent(playerId);
        if (opponent == null || opponent.ActiveDone)
        {
            game.Phase = ClashGamePhase.ResolvingClash;
            game.CurrentTurnPlayerId = null;
            Touch(game);
            return ResolveClash(game);
        }

        game.CurrentTurnPlayerId = opponent.PlayerId;
        Touch(game);
        return null;
    }

    public static ClashResolutionDto UseActive(
        ClashGame game,
        string playerId,
        string sourceUnitInstanceId,
        string abilityId,
        string targetUnitInstanceId,
        int? targetBoardRow,
        int? targetColumn,
        out string error)
    {
        error = null;
        if (game.Phase != ClashGamePhase.ActiveExchange ||
            game.CurrentTurnPlayerId != playerId)
        {
            error = "Сейчас не ваша очередь активок.";
            return null;
        }

        var player = game.GetPlayer(playerId);
        var limit = ActiveSelectionLimit(player.Morale);
        if (player.ActiveSelectionsUsed >= limit)
        {
            error = "Лимит активок на этот ход исчерпан.";
            return null;
        }

        var source = game.Units.FirstOrDefault(unit =>
            unit.InstanceId == sourceUnitInstanceId && unit.OwnerId == playerId &&
            unit.Alive && unit.Deployed);
        var definition = source == null ? null : ClashCatalog.Get(source.DefinitionId);
        var ability = definition?.Abilities.FirstOrDefault(candidate => candidate.Id == abilityId);
        if (source == null || ability == null)
        {
            error = "Активка не найдена или её носитель недоступен.";
            return null;
        }

        var targets = TargetsForAbility(
            game, playerId, source, ability,
            targetUnitInstanceId, targetBoardRow, targetColumn, out error);
        if (error != null) return null;

        var activeKey = $"{source.InstanceId}:{ability.Id}";
        if (player.UsedActiveKeys.Contains(activeKey))
        {
            if (player.Morale != 4 ||
                player.ActiveSelectionsUsed != 3 ||
                player.RepeatedActiveUsed)
            {
                error = "Повтор активки при морали 4 доступен только четвёртым выбором.";
                return null;
            }
            player.RepeatedActiveUsed = true;
        }
        else
        {
            player.UsedActiveKeys.Add(activeKey);
        }

        // The strict live catalog intentionally contains no actives yet. This generic
        // branch keeps the morale contract authoritative when a reviewed active is added.
        var applications = ActiveEffectsDoubled(player.Morale) ? 2 : 1;
        var offensiveAoe = ability.IsAoe ||
                           ability.Target is "all-enemies" or "row" or "column" or "aoe";
        for (var application = 0; application < applications; application++)
        {
            foreach (var target in targets.Where(target => target.Alive && target.Deployed))
            {
                var targetDefinition = ClashCatalog.Get(target.DefinitionId);
                if (target.OwnerId == playerId &&
                    ability.Target is "self" or "ally")
                {
                    target.Hp = Math.Min(
                        targetDefinition.MaxHp, target.Hp + Math.Max(0, ability.Value));
                }
                else if (target.OwnerId != playerId)
                {
                    if (offensiveAoe && targetDefinition.DiesToAoe)
                    {
                        target.Hp = 0;
                        target.Alive = false;
                    }
                    else if (ability.Value > 0)
                    {
                        target.Hp = Math.Max(0, target.Hp - ability.Value);
                        if (target.Hp == 0) target.Alive = false;
                    }
                }
            }
        }

        player.ActiveSelectionsUsed++;
        game.ActivePassStreak = 0;
        if (player.ActiveSelectionsUsed >= limit) player.ActiveDone = true;
        EvaluateTerminal(game);
        if (game.IsFinished)
        {
            Touch(game);
            return null;
        }

        var opponent = game.GetOpponent(playerId);
        if (opponent == null || (player.ActiveDone && opponent.ActiveDone))
        {
            game.Phase = ClashGamePhase.ResolvingClash;
            game.CurrentTurnPlayerId = null;
            Touch(game);
            return ResolveClash(game);
        }

        game.CurrentTurnPlayerId = opponent.ActiveDone ? player.PlayerId : opponent.PlayerId;
        Touch(game);
        return null;
    }

    private static List<ClashUnit> TargetsForAbility(
        ClashGame game,
        string playerId,
        ClashUnit source,
        ClashAbilityDefinition ability,
        string targetUnitInstanceId,
        int? targetBoardRow,
        int? targetColumn,
        out string error)
    {
        error = null;
        List<ClashUnit> targets;
        switch (ability.Target)
        {
            case "self":
                targets = new List<ClashUnit> { source };
                break;
            case "ally":
            case "enemy":
            {
                var target = game.Units.FirstOrDefault(unit =>
                    unit.InstanceId == targetUnitInstanceId &&
                    unit.Alive && unit.Deployed);
                var expectsAlly = ability.Target == "ally";
                if (target == null || (target.OwnerId == playerId) != expectsAlly)
                {
                    error = "Для активки выбрана недопустимая цель.";
                    return new List<ClashUnit>();
                }
                targets = new List<ClashUnit> { target };
                break;
            }
            case "all-enemies":
            case "aoe":
                targets = game.Units.Where(unit =>
                    unit.Alive && unit.Deployed && unit.OwnerId != playerId).ToList();
                break;
            case "row":
                if (targetBoardRow is null ||
                    targetBoardRow < 0 || targetBoardRow >= game.Length * 2)
                {
                    error = "Для активки выбран недопустимый глобальный ряд.";
                    return new List<ClashUnit>();
                }
                targets = game.Units.Where(unit =>
                    unit.Alive && unit.Deployed && unit.OwnerId != playerId &&
                    unit.BoardRow == targetBoardRow).ToList();
                break;
            case "column":
                if (targetColumn is null ||
                    targetColumn < 0 || targetColumn >= game.Width)
                {
                    error = "Для активки выбрана недопустимая колонка.";
                    return new List<ClashUnit>();
                }
                targets = game.Units.Where(unit =>
                    unit.Alive && unit.Deployed && unit.OwnerId != playerId &&
                    unit.Column == targetColumn).ToList();
                break;
            default:
                error = "Тип цели активки не поддерживается.";
                return new List<ClashUnit>();
        }

        if (targets.Count == 0)
            error = "У активки нет легальной цели.";
        return targets;
    }

    public static ClashResolutionDto ResolveClash(ClashGame game)
    {
        game.Phase = ClashGamePhase.ResolvingClash;
        game.CurrentTurnPlayerId = null;
        game.ClashNumber++;
        var resolution = new ClashResolutionDto
        {
            GameId = game.GameId,
            ClashNumber = game.ClashNumber,
            StartedAtUtc = DateTime.UtcNow.ToString("o"),
        };
        var sequence = 0;
        AddEvent(resolution, ref sequence, ClashResolutionEventType.ClashStart,
            message: $"Клэш {game.ClashNumber} начинается.");

        var opposingFrontPairs = BuildAdvancePairs(game);
        ApplyBleeding(game, resolution, ref sequence);
        EvaluateTerminal(game);

        if (!game.IsFinished)
        {
            // Bleeding can remove a member of a full Legion row before actions.
            // Announce only the bonus represented by the actual intent snapshot.
            EmitLegionFeedback(game, resolution, ref sequence);
            var intents = BuildIntents(game);
            foreach (var tier in intents.GroupBy(intent => intent.Speed).OrderByDescending(group => group.Key))
            {
                var speed = tier.Key;
                var impactOffset = ImpactOffsetForSpeed(speed);
                var aliveActors = tier.Where(intent => intent.Actor.Alive).ToList();
                var aliveTargetIds = aliveActors
                    .Where(intent => intent.Target?.Alive == true)
                    .Select(intent => intent.Target.InstanceId)
                    .ToHashSet(StringComparer.Ordinal);
                var attacks = new List<BattleIntent>();

                foreach (var intent in aliveActors.OrderBy(intent => intent.Actor.InstanceId, StringComparer.Ordinal))
                {
                    if (intent.IsReloading)
                    {
                        AddEvent(resolution, ref sequence, ClashResolutionEventType.Reload,
                            intent.Actor, speed: speed, startOffsetMs: impactOffset,
                            impactOffsetMs: impactOffset,
                            message: $"{Name(intent.Actor)} перезаряжается.");
                        continue;
                    }

                    if (intent.Target == null || !aliveTargetIds.Contains(intent.Target.InstanceId))
                    {
                        AddEvent(resolution, ref sequence, ClashResolutionEventType.Wait,
                            intent.Actor, intent.Target, speed, impactOffset, impactOffset,
                            message: $"{Name(intent.Actor)} выжидает.");
                        continue;
                    }

                    var eventType = intent.IsRanged
                        ? ClashResolutionEventType.RangedAttack
                        : ClashResolutionEventType.Attack;
                    AddEvent(resolution, ref sequence, eventType,
                        intent.Actor, intent.Target, speed,
                        Math.Max(0, impactOffset - 250), impactOffset,
                        amount: ClashCatalog.Get(intent.Actor.DefinitionId).Attack,
                        column: intent.Actor.Column,
                        message: $"{Name(intent.Actor)} атакует {Name(intent.Target)}.");

                    attacks.Add(intent);
                    if (intent.IsRanged)
                    {
                        var definition = ClashCatalog.Get(intent.Actor.DefinitionId);
                        intent.Actor.RangedReadyClash =
                            game.ClashNumber + definition.ReloadClashes + 1;
                    }
                }

                ApplySimultaneousHits(
                    resolution, ref sequence, attacks, impactOffset);

                var deadThisTier = game.Units
                    .Where(unit => unit.Alive && unit.Deployed && unit.Hp <= 0)
                    .OrderBy(unit => unit.InstanceId, StringComparer.Ordinal)
                    .ToList();
                foreach (var dead in deadThisTier)
                {
                    dead.Alive = false;
                    AddEvent(resolution, ref sequence, ClashResolutionEventType.Death,
                        target: dead, speed: speed, startOffsetMs: impactOffset,
                        impactOffsetMs: impactOffset + 1, column: dead.Column,
                        message: $"{Name(dead)} погибает.");
                }

                EvaluateTerminal(game);
                if (game.IsFinished) break;
            }

            if (!game.IsFinished)
            {
                var advanceOffset = Math.Max(
                    0, resolution.Events.Max(item => item.ImpactOffsetMs)) + 500;
                var advanceClaims = opposingFrontPairs
                    .Where(pair => pair.Attacker.Alive && !pair.Target.Alive)
                    .Select(pair => new AdvanceClaim
                    {
                        Attacker = pair.Attacker,
                        Target = pair.Target,
                        ImpactOffsetMs = advanceOffset,
                    })
                    .ToList();
                var killAdvancerIds = advanceClaims
                    .Select(claim => claim.Attacker.InstanceId)
                    .ToHashSet(StringComparer.Ordinal);
                var unopposedClaims = BuildUnopposedAdvanceClaims(
                    game, killAdvancerIds, advanceOffset);

                ApplyAdvances(game, resolution, ref sequence, advanceClaims);
                ApplyUnopposedAdvances(
                    game, resolution, ref sequence, unopposedClaims);
                EvaluateTerminal(game);
            }
        }

        var endOffset = Math.Max(250,
            resolution.Events.Count == 0 ? 0 : resolution.Events.Max(item => item.ImpactOffsetMs)) + 250;
        AddEvent(resolution, ref sequence, ClashResolutionEventType.ClashEnd,
            startOffsetMs: endOffset, impactOffsetMs: endOffset,
            message: game.IsFinished ? "Игра завершена." : $"Клэш {game.ClashNumber} завершён.");

        Touch(game);
        resolution.Revision = game.Revision;
        resolution.DurationMs = endOffset + 500;
        resolution.WinnerId = game.WinnerId;
        resolution.IsDraw = game.IsDraw;
        resolution.TerminalReason = game.TerminalReason == ClashTerminalReason.None
            ? null
            : game.TerminalReason.ToString();
        resolution.FinalUnits = game.Units
            .Where(unit => unit.Deployed)
            .OrderBy(unit => unit.InstanceId, StringComparer.Ordinal)
            .Select(unit => ToUnitDto(game, unit))
            .ToList();
        game.LatestResolution = resolution;
        game.ResolutionEndsAtUtc = game.IsFinished
            ? null
            : DateTime.Parse(
                resolution.StartedAtUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind)
                .AddMilliseconds(resolution.DurationMs);
        return resolution;
    }

    private static List<BattleIntent> BuildIntents(ClashGame game)
    {
        var living = game.Units
            .Where(unit => unit.Alive && unit.Deployed && unit.BoardRow != null && unit.Column != null)
            .ToList();
        var fronts = new Dictionary<(ClashSide side, int column), ClashUnit>();
        foreach (var side in new[] { ClashSide.Host, ClashSide.Guest })
        for (var column = 0; column < game.Width; column++)
        {
            var candidates = living.Where(unit => unit.OwnerSide == side && unit.Column == column);
            var front = side == ClashSide.Host
                ? candidates.OrderByDescending(unit => unit.BoardRow).ThenBy(unit => unit.InstanceId).FirstOrDefault()
                : candidates.OrderBy(unit => unit.BoardRow).ThenBy(unit => unit.InstanceId).FirstOrDefault();
            fronts[(side, column)] = front;
        }

        var intents = new List<BattleIntent>();
        foreach (var unit in living.OrderBy(unit => unit.InstanceId, StringComparer.Ordinal))
        {
            var definition = ClashCatalog.Get(unit.DefinitionId);
            var speed = EffectiveSpeed(game, unit);
            var opponentSide = unit.OwnerSide == ClashSide.Host ? ClashSide.Guest : ClashSide.Host;
            fronts.TryGetValue((opponentSide, unit.Column.Value), out var target);
            var isFront = fronts.TryGetValue((unit.OwnerSide, unit.Column.Value), out var ownFront) &&
                          ownFront?.InstanceId == unit.InstanceId;
            var reloading = definition.IsRanged && unit.RangedReadyClash > game.ClashNumber;
            intents.Add(new BattleIntent
            {
                Actor = unit,
                // Front-line melee units engage the opposing front in their column
                // even across a cleared gap. This preserves total progress after a
                // previous front dies and its next rank is more than one cell away.
                Target = reloading || (!definition.IsRanged && (!isFront || target == null))
                    ? null
                    : target,
                Speed = speed,
                IsRanged = definition.IsRanged,
                IsReloading = reloading,
            });
        }
        return intents;
    }

    private static List<AdvanceClaim> BuildAdvancePairs(ClashGame game)
    {
        var pairs = new List<AdvanceClaim>();
        for (var column = 0; column < game.Width; column++)
        {
            var hostFront = game.Units
                .Where(unit => unit.Alive && unit.Deployed &&
                               unit.OwnerSide == ClashSide.Host &&
                               unit.Column == column && unit.BoardRow != null)
                .OrderByDescending(unit => unit.BoardRow)
                .FirstOrDefault();
            var guestFront = game.Units
                .Where(unit => unit.Alive && unit.Deployed &&
                               unit.OwnerSide == ClashSide.Guest &&
                               unit.Column == column && unit.BoardRow != null)
                .OrderBy(unit => unit.BoardRow)
                .FirstOrDefault();
            if (hostFront?.BoardRow == null || guestFront?.BoardRow == null)
                continue;

            // These are opposing fronts even when earlier casualties left empty
            // cells between them. A surviving melee front occupies the fallen
            // opposing front's actual cell after resolution.
            if (!ClashCatalog.Get(hostFront.DefinitionId).IsRanged)
                pairs.Add(new AdvanceClaim { Attacker = hostFront, Target = guestFront });
            if (!ClashCatalog.Get(guestFront.DefinitionId).IsRanged)
                pairs.Add(new AdvanceClaim { Attacker = guestFront, Target = hostFront });
        }
        return pairs;
    }

    private static void EmitLegionFeedback(
        ClashGame game,
        ClashResolutionDto resolution,
        ref int sequence)
    {
        foreach (var unit in game.Units
                     .Where(unit => unit.Alive && unit.Deployed && RowHasLegion(game, unit))
                     .OrderBy(unit => unit.BoardRow)
                     .ThenBy(unit => unit.Column)
                     .ThenBy(unit => unit.InstanceId, StringComparer.Ordinal))
        {
            AddEvent(resolution, ref sequence, ClashResolutionEventType.Passive,
                actor: unit,
                speed: EffectiveSpeed(game, unit),
                startOffsetMs: 0,
                impactOffsetMs: 0,
                amount: 2,
                column: unit.Column,
                message: $"Легион! {Name(unit)} получает +2 скорости.");
        }
    }

    private static int EffectiveSpeed(ClashGame game, ClashUnit unit)
    {
        var definition = ClashCatalog.Get(unit.DefinitionId);
        var legionBonus = RowHasLegion(game, unit) ? 2 : 0;
        return Math.Clamp(definition.Speed + legionBonus, 1, 9);
    }

    private static bool RowHasLegion(ClashGame game, ClashUnit unit)
    {
        if (unit.BoardRow == null) return false;
        for (var column = 0; column < game.Width; column++)
        {
            var rowUnit = game.Units.FirstOrDefault(candidate =>
                candidate.Alive && candidate.Deployed &&
                candidate.OwnerSide == unit.OwnerSide &&
                candidate.BoardRow == unit.BoardRow &&
                candidate.Column == column);
            if (rowUnit == null ||
                !ClashCatalog.Get(rowUnit.DefinitionId).Tags.Contains("legion-candidate"))
                return false;
        }
        return true;
    }

    private static void ApplySimultaneousHits(
        ClashResolutionDto resolution,
        ref int sequence,
        IReadOnlyCollection<BattleIntent> attacks,
        int impactOffset)
    {
        if (attacks.Count == 0) return;

        var snapshots = attacks
            .Select(intent => intent.Target)
            .DistinctBy(target => target.InstanceId)
            .ToDictionary(target => target.InstanceId, target => new
            {
                target.Hp,
                target.ShieldCharges,
                target.DodgeCharges,
            }, StringComparer.Ordinal);
        var outcomes = new List<HitOutcome>(attacks.Count);
        foreach (var targetAttacks in attacks.GroupBy(intent => intent.Target.InstanceId))
        {
            var snapshot = snapshots[targetAttacks.Key];
            // Allocate finite defenses against the strongest distinct hits. Gameplay
            // traits break equal-damage ties before stable IDs; IDs only order otherwise
            // equivalent log/animation entries and cannot change the final state.
            var ordered = targetAttacks
                .OrderByDescending(intent => ClashCatalog.Get(intent.Actor.DefinitionId).Attack)
                .ThenByDescending(intent => intent.Actor.BleedCharges > 0)
                .ThenBy(intent => intent.Actor.DefinitionId, StringComparer.Ordinal)
                .ThenBy(intent => intent.Actor.InstanceId, StringComparer.Ordinal)
                .ToList();
            var blocked = ordered
                .Take(Math.Min(snapshot.ShieldCharges, ordered.Count))
                .ToHashSet();
            var dodged = ordered
                .Where(intent => !blocked.Contains(intent))
                .Take(Math.Min(snapshot.DodgeCharges, ordered.Count - blocked.Count))
                .ToHashSet();

            foreach (var intent in targetAttacks)
            {
                var wasBlocked = blocked.Contains(intent);
                var wasDodged = dodged.Contains(intent);
                var rawDamage = ClashCatalog.Get(intent.Actor.DefinitionId).Attack;
                var damage = wasBlocked ? 0 : wasDodged ? Math.Min(1, rawDamage) : rawDamage;
                outcomes.Add(new HitOutcome
                {
                    Intent = intent,
                    Blocked = wasBlocked,
                    Dodged = wasDodged,
                    Damage = Math.Max(0, damage),
                    AppliesBleed = damage > 0 && intent.Actor.BleedCharges > 0,
                });
            }
        }

        foreach (var targetGroup in outcomes.GroupBy(outcome => outcome.Intent.Target.InstanceId))
        {
            var target = targetGroup.First().Intent.Target;
            var snapshot = snapshots[target.InstanceId];
            target.ShieldCharges = Math.Max(
                0, snapshot.ShieldCharges - targetGroup.Count(outcome => outcome.Blocked));
            target.DodgeCharges = Math.Max(
                0, snapshot.DodgeCharges - targetGroup.Count(outcome => outcome.Dodged));
            target.Hp = Math.Max(
                0, snapshot.Hp - targetGroup.Sum(outcome => outcome.Damage));
            target.BleedStacks += targetGroup.Count(outcome => outcome.AppliesBleed);
        }

        foreach (var actorGroup in outcomes
                     .Where(outcome => outcome.AppliesBleed)
                     .GroupBy(outcome => outcome.Intent.Actor.InstanceId))
        {
            var actor = actorGroup.First().Intent.Actor;
            actor.BleedCharges = Math.Max(0, actor.BleedCharges - actorGroup.Count());
        }

        foreach (var outcome in outcomes)
        {
            var actor = outcome.Intent.Actor;
            var target = outcome.Intent.Target;
            if (outcome.Blocked)
            {
                AddEvent(resolution, ref sequence, ClashResolutionEventType.Block,
                    actor, target, outcome.Intent.Speed, impactOffset, impactOffset,
                    column: target.Column,
                    message: $"{Name(target)} блокирует атаку.");
                continue;
            }

            if (outcome.Dodged)
            {
                AddEvent(resolution, ref sequence, ClashResolutionEventType.Dodge,
                    actor, target, outcome.Intent.Speed, impactOffset, impactOffset,
                    amount: outcome.Damage, column: target.Column,
                    message: $"{Name(target)} уклоняется, получая только {outcome.Damage} урона.");
            }

            AddEvent(resolution, ref sequence, ClashResolutionEventType.Damage,
                actor, target, outcome.Intent.Speed, impactOffset, impactOffset,
                amount: outcome.Damage, column: target.Column,
                message: $"{Name(target)} получает {outcome.Damage} урона.");

            if (outcome.AppliesBleed)
            {
                AddEvent(resolution, ref sequence, ClashResolutionEventType.BleedApplied,
                    actor, target, outcome.Intent.Speed, impactOffset, impactOffset + 1,
                    amount: 1, column: target.Column,
                    message: $"{Name(actor)} накладывает кровотечение.");
            }
        }
    }

    private static void ApplyBleeding(
        ClashGame game,
        ClashResolutionDto resolution,
        ref int sequence)
    {
        foreach (var unit in game.Units
                     .Where(unit => unit.Alive && unit.Deployed && unit.BleedStacks > 0)
                     .OrderBy(unit => unit.InstanceId, StringComparer.Ordinal))
        {
            var damage = Math.Min(unit.Hp, unit.BleedStacks);
            unit.Hp = Math.Max(0, unit.Hp - unit.BleedStacks);
            AddEvent(resolution, ref sequence, ClashResolutionEventType.BleedDamage,
                target: unit, speed: 9, amount: damage, column: unit.Column,
                message: $"{Name(unit)} теряет {damage} ХП от кровотечения.");
        }

        foreach (var unit in game.Units
                     .Where(unit => unit.Alive && unit.Deployed && unit.Hp <= 0)
                     .OrderBy(unit => unit.InstanceId, StringComparer.Ordinal))
        {
            unit.Alive = false;
            AddEvent(resolution, ref sequence, ClashResolutionEventType.Death,
                target: unit, speed: 9, impactOffsetMs: 1, column: unit.Column,
                message: $"{Name(unit)} погибает от кровотечения.");
        }
    }

    private static void ApplyAdvances(
        ClashGame game,
        ClashResolutionDto resolution,
        ref int sequence,
        IEnumerable<AdvanceClaim> claims)
    {
        foreach (var claim in claims
                     .OrderBy(claim => claim.ImpactOffsetMs)
                     .ThenBy(claim => claim.Attacker.InstanceId, StringComparer.Ordinal))
        {
            var attacker = claim.Attacker;
            var target = claim.Target;
            if (!attacker.Alive || target.Alive || target.BoardRow == null || target.Column == null)
                continue;
            if (Occupied(game, target.BoardRow.Value, target.Column.Value))
                continue;

            var from = attacker.BoardRow;
            attacker.BoardRow = target.BoardRow;
            attacker.Column = target.Column;
            AddEvent(resolution, ref sequence, ClashResolutionEventType.Advance,
                attacker, target, EffectiveSpeed(game, attacker),
                claim.ImpactOffsetMs, claim.ImpactOffsetMs,
                fromBoardRow: from, toBoardRow: attacker.BoardRow,
                column: attacker.Column,
                message: $"{Name(attacker)} занимает клетку павшего врага.");
        }
    }

    private static List<UnopposedAdvanceClaim> BuildUnopposedAdvanceClaims(
        ClashGame game,
        IReadOnlySet<string> killAdvancerIds,
        int impactOffsetMs)
    {
        var claims = new List<UnopposedAdvanceClaim>();
        foreach (var side in new[] { ClashSide.Host, ClashSide.Guest })
        for (var column = 0; column < game.Width; column++)
        {
            var ownUnits = game.Units
                .Where(unit => unit.Alive && unit.Deployed &&
                               unit.OwnerSide == side &&
                               unit.Column == column &&
                               unit.BoardRow != null)
                .ToList();
            if (ownUnits.Count == 0) continue;

            var front = side == ClashSide.Host
                ? ownUnits.OrderByDescending(unit => unit.BoardRow).First()
                : ownUnits.OrderBy(unit => unit.BoardRow).First();
            if (killAdvancerIds.Contains(front.InstanceId))
                continue;

            var hasEnemyAhead = game.Units.Any(enemy =>
                enemy.Alive && enemy.Deployed &&
                enemy.OwnerSide != side &&
                enemy.Column == column &&
                enemy.BoardRow != null &&
                (side == ClashSide.Host
                    ? enemy.BoardRow.Value > front.BoardRow.Value
                    : enemy.BoardRow.Value < front.BoardRow.Value));
            if (hasEnemyAhead) continue;

            var destination = front.BoardRow.Value +
                              (side == ClashSide.Host ? 1 : -1);
            if (destination < 0 || destination >= game.Length * 2 ||
                Occupied(game, destination, column))
                continue;

            claims.Add(new UnopposedAdvanceClaim
            {
                Actor = front,
                FromBoardRow = front.BoardRow.Value,
                ToBoardRow = destination,
                ImpactOffsetMs = impactOffsetMs,
            });
        }
        return claims;
    }

    private static void ApplyUnopposedAdvances(
        ClashGame game,
        ClashResolutionDto resolution,
        ref int sequence,
        IEnumerable<UnopposedAdvanceClaim> claims)
    {
        // A completely cleared enemy column otherwise creates a permanent
        // cross-column deadlock. Its surviving front unit, ranged or melee,
        // marches exactly one adjacent empty cell and never after a kill advance.
        foreach (var claim in claims
                     .OrderBy(claim => claim.Actor.InstanceId, StringComparer.Ordinal))
        {
            var actor = claim.Actor;
            if (!actor.Alive || actor.BoardRow != claim.FromBoardRow ||
                Occupied(game, claim.ToBoardRow, actor.Column.Value))
                continue;

            actor.BoardRow = claim.ToBoardRow;
            AddEvent(resolution, ref sequence, ClashResolutionEventType.Advance,
                actor, speed: EffectiveSpeed(game, actor),
                startOffsetMs: claim.ImpactOffsetMs,
                impactOffsetMs: claim.ImpactOffsetMs,
                fromBoardRow: claim.FromBoardRow,
                toBoardRow: claim.ToBoardRow,
                column: actor.Column,
                message: $"{Name(actor)} продвигается по свободной линии.");
        }
    }

    public static void EvaluateTerminal(ClashGame game)
    {
        var hostLiving = game.Units.Count(unit =>
            unit.OwnerSide == ClashSide.Host && unit.Alive && unit.Deployed);
        var guestLiving = game.Units.Count(unit =>
            unit.OwnerSide == ClashSide.Guest && unit.Alive && unit.Deployed);
        var hostBreach = game.Units.Any(unit =>
            unit.OwnerSide == ClashSide.Host && unit.Alive && unit.Deployed &&
            unit.BoardRow == game.Length * 2 - 1);
        var guestBreach = game.Units.Any(unit =>
            unit.OwnerSide == ClashSide.Guest && unit.Alive && unit.Deployed &&
            unit.BoardRow == 0);

        var hostLost = guestBreach || hostLiving == 0;
        var guestLost = hostBreach || guestLiving == 0;
        if (!hostLost && !guestLost) return;

        game.Phase = ClashGamePhase.Finished;
        game.CurrentTurnPlayerId = null;
        game.ResolutionEndsAtUtc = null;
        if (hostLost && guestLost)
        {
            game.WinnerId = null;
            game.IsDraw = true;
            game.TerminalReason = hostBreach && guestBreach
                ? ClashTerminalReason.DualBreach
                : ClashTerminalReason.MutualElimination;
            return;
        }

        game.IsDraw = false;
        game.WinnerId = hostLost ? game.Guest.PlayerId : game.Host.PlayerId;
        game.TerminalReason = hostBreach || guestBreach
            ? ClashTerminalReason.Breach
            : ClashTerminalReason.Elimination;
    }

    public static void EndByPlayerExit(
        ClashGame game,
        string leavingPlayerId,
        ClashTerminalReason reason)
    {
        var opponent = game.GetOpponent(leavingPlayerId);
        game.WinnerId = opponent?.PlayerId;
        game.IsDraw = opponent == null;
        game.TerminalReason = reason;
        game.Phase = ClashGamePhase.Finished;
        game.CurrentTurnPlayerId = null;
        game.ResolutionEndsAtUtc = null;
        Touch(game);
    }

    public static bool Occupied(ClashGame game, int boardRow, int column)
    {
        return game.Units.Any(unit =>
            unit.Alive && unit.Deployed &&
            unit.BoardRow == boardRow && unit.Column == column);
    }

    public static ClashUnitDto ToUnitDto(ClashGame game, ClashUnit unit)
    {
        var definition = ClashCatalog.Get(unit.DefinitionId);
        return new ClashUnitDto
        {
            InstanceId = unit.InstanceId,
            DefinitionId = unit.DefinitionId,
            OwnerId = unit.OwnerId,
            OwnerSide = unit.OwnerSide.ToString(),
            BoardRow = unit.BoardRow,
            Column = unit.Column,
            Hp = unit.Hp,
            MaxHp = definition?.MaxHp ?? unit.Hp,
            Attack = definition?.Attack ?? 0,
            Speed = unit.Deployed && unit.Alive
                ? EffectiveSpeed(game, unit)
                : definition?.Speed ?? 0,
            ShieldCharges = unit.ShieldCharges,
            DodgeCharges = unit.DodgeCharges,
            BleedStacks = unit.BleedStacks,
            RangedReadyClash = unit.RangedReadyClash,
            DiesToAoe = definition?.DiesToAoe ?? false,
            Alive = unit.Alive,
            Deployed = unit.Deployed,
        };
    }

    private static int ImpactOffsetForSpeed(int speed)
    {
        return Math.Max(0, 9 - Math.Clamp(speed, 1, 9)) * SpeedStepMs;
    }

    private static string Name(ClashUnit unit)
    {
        return unit == null ? "Юнит" : ClashCatalog.Get(unit.DefinitionId)?.Name ?? unit.InstanceId;
    }

    private static void AddEvent(
        ClashResolutionDto resolution,
        ref int sequence,
        ClashResolutionEventType type,
        ClashUnit actor = null,
        ClashUnit target = null,
        int speed = 0,
        int startOffsetMs = 0,
        int impactOffsetMs = 0,
        int amount = 0,
        int? fromBoardRow = null,
        int? toBoardRow = null,
        int? column = null,
        string message = null)
    {
        resolution.Events.Add(new ClashResolutionEventDto
        {
            Sequence = ++sequence,
            Type = type.ToString(),
            ActorUnitInstanceId = actor?.InstanceId,
            TargetUnitInstanceId = target?.InstanceId,
            Speed = speed,
            StartOffsetMs = startOffsetMs,
            ImpactOffsetMs = impactOffsetMs,
            Amount = amount,
            FromBoardRow = fromBoardRow,
            ToBoardRow = toBoardRow,
            Column = column,
            Message = message,
        });
    }

    public static void Touch(ClashGame game)
    {
        game.Revision++;
        game.LastActivity = DateTime.UtcNow;
    }
}
