using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// The single physical-board boundary for V2/V3. It mirrors the human fog projection and
/// returns an immutable-by-contract value model with no enemy Ship, Deck, Board or Player
/// references. Combat policies therefore cannot accidentally inspect hidden state.
/// </summary>
public static class BattleshipBotObservationFactory
{
    public static BattleshipAdvancedBotAI.BotObservation Create(
        BattleshipGame game,
        BattleshipPlayer bot)
    {
        var opponent = game.GetOpponent(bot.DiscordId)
                       ?? throw new InvalidOperationException("Battleship bot has no opponent.");
        var observation = new BattleshipAdvancedBotAI.BotObservation
        {
            Faction = opponent.Faction,
        };
        var canReadSunkNames = BattleshipGameEngine.HasLivingMast(bot);
        if (!game.BotKnowledge.TryGetValue(bot.DiscordId, out var memory))
        {
            memory = new BattleshipBotKnowledge();
            game.BotKnowledge[bot.DiscordId] = memory;
        }

        for (var row = 0; row < 10; row++)
        for (var col = 0; col < 10; col++)
        {
            var source = opponent.Board.Grid[row, col];
            // This is projection logic, not tactical evidence: mirror MapFogBoard exactly,
            // then let every belief update below consume only the sanitized value object.
            var hiddenMovedShip = source.ShipRef is
                { HasHiddenMovement: true, IsDestroyed: false } && !source.WasShipHit;
            var visibleDeck = hiddenMovedShip ? null : GetDeckAtCell(source, row, col);
            var hasKnownShip = source.WasShipHit || source.WasRevealedShip ||
                               source.IsRevealed && source.ShipRef != null && !hiddenMovedShip;
            var isDestroyed = source.WasShipHit && !source.WasScratched ||
                              visibleDeck?.IsDestroyed == true;
            var isSunk = !hiddenMovedShip && source.ShipRef?.IsDestroyed == true &&
                         source.IsRevealed;
            var sunkName = canReadSunkNames ? source.SunkShipName : null;
            var cell = new BattleshipAdvancedBotAI.BotCellObservation
            {
                Row = row,
                Col = col,
                IsRevealed = source.IsRevealed,
                HasKnownShip = hasKnownShip,
                IsHit = source.IsHit || source.WasShipHit,
                IsMiss = source.IsMiss,
                IsScratched = source.WasScratched,
                IsDestroyed = isDestroyed,
                IsShipSunk = isSunk,
                IsBurning = source.IsBurning || source.IsNeptuneBurned,
                HasElectricCharge = source.HasElectricCharge,
                IsBurnResistMarked = source.BurnResistMarked,
                IsFrozen = !hiddenMovedShip &&
                           source.ShipRef?.Statuses.Contains(ShipStatusType.Freeze) == true,
                IsDevastated = !hiddenMovedShip &&
                              source.ShipRef?.Statuses.Contains(ShipStatusType.Devastated) == true,
                IsCaptured = !hiddenMovedShip &&
                            source.ShipRef?.Statuses.Contains(ShipStatusType.Capture) == true,
                WasDodge = source.WasDodge,
                WasManeuverDodge = source.WasManeuverDodge,
                HasAnySummon = source.SummonRef is { IsAlive: true },
                HasFriendlySummon = source.SummonRef is { IsAlive: true } summon &&
                                    summon.OwnerId == bot.DiscordId,
                SunkShipName = sunkName,
            };
            observation.Cells[row, col] = cell;
            if (!string.IsNullOrWhiteSpace(sunkName))
                memory.KnownSunkShipNames.Add(sunkName);
        }

        UpdatePublicMovementMemory(observation, memory);
        RegisterPublicAutoDodgePlacements(observation);
        observation.KnownSunkNames.UnionWith(memory.KnownSunkShipNames);
        return observation;
    }

    private static void UpdatePublicMovementMemory(
        BattleshipAdvancedBotAI.BotObservation observation,
        BattleshipBotKnowledge memory)
    {
        memory.LastObservedLiveEnemyCells ??= new HashSet<int>();
        memory.StaleEnemyEvidenceCells ??= new HashSet<int>();
        memory.RecheckedMovementWaterCells ??= new HashSet<int>();
        var currentLiveCells = new HashSet<int>();
        var discoveredNewStaleEvidence = false;

        foreach (var cell in observation.Cells.Cast<BattleshipAdvancedBotAI.BotCellObservation>())
        {
            var index = cell.Row * 10 + cell.Col;
            var hasPublicMissResolution = cell.IsMiss;
            var isKnownLive = !hasPublicMissResolution && cell.HasKnownShip && !cell.IsDestroyed &&
                              !cell.IsShipSunk && !cell.WasManeuverDodge;

            // A human can remember that yesterday's visible deck has now resolved to water.
            // The comparison deliberately consumes only two already-sanitized observations.
            if (memory.LastObservedLiveEnemyCells.Contains(index) && hasPublicMissResolution)
                discoveredNewStaleEvidence |= memory.StaleEnemyEvidenceCells.Add(index);
            if (cell.WasManeuverDodge)
                discoveredNewStaleEvidence |= memory.StaleEnemyEvidenceCells.Add(index);

            if (isKnownLive)
            {
                currentLiveCells.Add(index);
                // A new public sighting supersedes an older stale-paint inference.
                memory.StaleEnemyEvidenceCells.Remove(index);
            }
        }

        memory.LastObservedLiveEnemyCells = currentLiveCells;
        if (discoveredNewStaleEvidence)
            memory.RecheckedMovementWaterCells.Clear();
        foreach (var index in memory.StaleEnemyEvidenceCells)
        {
            if (index is < 0 or >= 100) continue;
            observation.MarkStaleEvidence(index / 10, index % 10);
        }
        foreach (var index in memory.RecheckedMovementWaterCells)
        {
            if (index is < 0 or >= 100) continue;
            observation.MarkRecheckedMovementWater(index / 10, index % 10);
        }
    }

    private static void RegisterPublicAutoDodgePlacements(
        BattleshipAdvancedBotAI.BotObservation observation)
    {
        var markers = observation.Cells.Cast<BattleshipAdvancedBotAI.BotCellObservation>()
            .Where(cell => cell.WasManeuverDodge)
            .ToList();
        if (markers.Count == 0) return;

        var definitions = ShipCatalog.AllShips.Where(definition =>
            definition.Factions.Contains(observation.Faction) &&
            definition.Abilities.Contains("auto_dodge_bow_stern") &&
            definition.DeckCount > 1);

        foreach (var definition in definitions)
        {
            var prototype = ShipCatalog.CreateShip(definition);
            foreach (var marker in markers)
            foreach (var orientation in Enum.GetValues<Orientation>())
            foreach (var hitDeckIndex in new[] { 0, prototype.Decks.Count - 1 }.Distinct())
            {
                var hitOffset = prototype.GetDeckCell(
                    prototype.Decks[hitDeckIndex], 0, 0, orientation);
                var oldRow = marker.Row - hitOffset.row;
                var oldCol = marker.Col - hitOffset.col;
                var oldCells = prototype.GetOccupiedCells(oldRow, oldCol, orientation);
                if (!IsCatalogLegalPlacement(definition, oldCells)) continue;

                var adjacentDeckIndex = hitDeckIndex == 0 ? 1 : prototype.Decks.Count - 2;
                var adjacent = prototype.GetDeckCell(
                    prototype.Decks[adjacentDeckIndex], oldRow, oldCol, orientation);
                var rowStep = Math.Sign(adjacent.row - marker.Row);
                var colStep = Math.Sign(adjacent.col - marker.Col);
                if (Math.Abs(rowStep) + Math.Abs(colStep) != 1) continue;

                var movedCells = oldCells
                    .Select(cell => (row: cell.row + rowStep, col: cell.col + colStep))
                    .ToList();
                if (!IsCatalogLegalPlacement(definition, movedCells) ||
                    movedCells.Contains((marker.Row, marker.Col))) continue;

                // A surviving Light Wood hull cannot occupy a sunk cell or remain in fire;
                // every other hidden-board constraint stays unknown, just as it does to a human.
                if (movedCells.Any(cell =>
                        observation.Cells[cell.row, cell.col].IsShipSunk ||
                        observation.Cells[cell.row, cell.col].IsBurning)) continue;

                observation.RegisterAutoDodgePlacement(definition.Id, movedCells);
            }
        }
    }

    private static bool IsCatalogLegalPlacement(
        ShipDefinition definition,
        IReadOnlyCollection<(int row, int col)> cells)
    {
        if (cells.Count != definition.DeckCount ||
            cells.Distinct().Count() != definition.DeckCount ||
            cells.Any(cell => cell.row is < 0 or >= 10 || cell.col is < 0 or >= 10))
            return false;
        if (definition.Range is not (RangeClass.Far or RangeClass.Tetra))
            return cells.All(cell => cell.row < 8);
        return cells.All(cell => cell.row < 8) || cells.All(cell => cell.row >= 8);
    }

    private static Deck GetDeckAtCell(Cell cell, int row, int col)
    {
        var ship = cell.ShipRef;
        if (ship == null) return null;
        return ship.Decks.FirstOrDefault(deck =>
        {
            var position = ship.GetDeckCell(deck, ship.Row, ship.Col, ship.Orientation);
            return position.row == row && position.col == col;
        });
    }
}
