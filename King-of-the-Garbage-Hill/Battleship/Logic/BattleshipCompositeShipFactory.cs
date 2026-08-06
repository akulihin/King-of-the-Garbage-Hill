using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Builds the persistent hull produced when a Merging Ship overlaps one deck of one ally.
/// The result owns real deck, module and weapon state; it is not a display-only wrapper.
/// </summary>
public static class BattleshipCompositeShipFactory
{
    public const string DefinitionId = "cozy_joint_ship";
    public const string DisplayName = "Уютный Совместный корабль";

    public static bool TryMerge(
        BattleshipPlayer player,
        Ship mergingShip,
        int newRow,
        int newCol,
        out Ship composite,
        out string error)
    {
        composite = null;
        error = null;
        if (player == null || mergingShip == null ||
            !mergingShip.Abilities.Contains("merge_maneuver"))
        {
            error = "Этот корабль не умеет сливаться.";
            return false;
        }

        var movingCells = mergingShip.Decks.ToDictionary(
            deck => deck.Index,
            deck => mergingShip.GetDeckCell(deck, newRow, newCol, mergingShip.Orientation));
        var collidedShips = movingCells.Values
            .Select(cell => player.Board.GetCell(cell.row, cell.col)?.ShipRef)
            .Where(ship => ship != null && ship.Id != mergingShip.Id)
            .DistinctBy(ship => ship.Id)
            .ToList();
        if (collidedShips.Count != 1)
        {
            error = collidedShips.Count == 0
                ? "Для слияния нужно перекрыть палубу союзного корабля."
                : "За один манёвр можно слиться только с одним кораблём.";
            return false;
        }

        var target = collidedShips[0];
        if (target.IsAssemblyComponent)
        {
            error = "Нельзя осиротить группу собирающихся палуб слиянием.";
            return false;
        }
        if (target.IsDestroyed || target.Statuses.Any(status =>
                status is ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze))
        {
            error = "Нельзя слиться с выведенным из игры кораблём.";
            return false;
        }

        var sourceShips = new[] { mergingShip, target };
        var oldDeckByCell = sourceShips
            .SelectMany(ship => ship.Decks.Select(deck =>
            {
                var position = ship.GetDeckCell(deck, ship.Row, ship.Col, ship.Orientation);
                return new SourceDeckAtCell(ship, deck, position.row, position.col);
            }))
            .ToDictionary(value => (value.Row, value.Col));
        var inheritedStaleCells = sourceShips
            .SelectMany(ship => ship.ManeuverStaleHitCells)
            .Distinct()
            .ToList();

        var overlappedTargetDecks = target.Decks
            .Where(deck => movingCells.Values.Contains(target.GetDeckCell(deck, target.Row, target.Col, target.Orientation)))
            .Select(deck => deck.Index)
            .ToHashSet();
        if (overlappedTargetDecks.Count == 0)
        {
            error = "Сливающийся корабль должен заменить палубу союзника.";
            return false;
        }
        if (overlappedTargetDecks.Count != 1)
        {
            error = "Сливающийся корабль должен заменить ровно одну палубу союзника.";
            return false;
        }
        var replacedTargetDeck = target.Decks.Single(deck =>
            overlappedTargetDecks.Contains(deck.Index));
        var replacedTargetCell = target.GetDeckCell(
            replacedTargetDeck, target.Row, target.Col, target.Orientation);

        var retained = new List<RetainedDeck>();
        retained.AddRange(mergingShip.Decks
            .OrderBy(deck => deck.Index)
            .Select(deck => new RetainedDeck(
                mergingShip,
                deck,
                movingCells[deck.Index].row,
                movingCells[deck.Index].col)));
        retained.AddRange(target.Decks
            .Where(deck => !overlappedTargetDecks.Contains(deck.Index))
            .OrderBy(deck => deck.Index)
            .Select(deck =>
            {
                var cell = target.GetDeckCell(deck, target.Row, target.Col, target.Orientation);
                return new RetainedDeck(target, deck, cell.row, cell.col);
            }));

        var duplicateCell = retained
            .GroupBy(deck => (deck.Row, deck.Col))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateCell != null)
        {
            error = "После слияния две палубы заняли одну клетку.";
            return false;
        }

        var anchor = retained[0];
        composite = new Ship
        {
            DefinitionId = DefinitionId,
            Name = DisplayName,
            Row = anchor.Row,
            Col = anchor.Col,
            Orientation = Orientation.Horizontal,
            Range = RangeClass.Mid,
            Cost = mergingShip.Cost + target.Cost,
            Space = Math.Max(mergingShip.Space, target.Space),
            ExplosionRadius = Math.Max(mergingShip.ExplosionRadius, target.ExplosionRadius),
            Speed = mergingShip.Speed,
            Regions = mergingShip.Regions.Concat(target.Regions).Distinct().ToList(),
            // The design promises the weapon union, not re-anchored source passives. Spatial
            // abilities (poison, Grab, Freeze, collision movement) have source-specific geometry
            // and cannot be projected safely onto an arbitrary composite anchor.
            Abilities = new List<string>(),
            Upgrades = mergingShip.Upgrades.Concat(target.Upgrades)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Statuses = new List<ShipStatusType>(),
            IsHome = mergingShip.IsHome || target.IsHome,
            IsPlaced = true,
            HasManeuvered = true,
            HasHiddenMovement = true,
        };

        var deckMap = new Dictionary<(string shipId, int deckIndex), int>();
        var compositeDeckByCell = new Dictionary<(int row, int col), int>();
        for (var index = 0; index < retained.Count; index++)
        {
            var source = retained[index];
            var isReplacementDeck = (source.Row, source.Col) == replacedTargetCell;
            var replacementModules = isReplacementDeck
                ? new[] { (ship: target, deck: replacedTargetDeck),
                          (ship: source.Ship, deck: source.Deck) }
                    .Where(value => SourceDeckHasModule(value.ship, value.deck))
                    .ToList()
                : null;
            string replacementDisplayModule = null;
            if (replacementModules is { Count: > 0 })
            {
                var displaySource = replacementModules.FirstOrDefault(value =>
                    value.deck.Module != null &&
                    SourceDeckHasOperationalModule(value.ship, value.deck));
                if (displaySource.deck == null)
                    displaySource = replacementModules.FirstOrDefault(value => value.deck.Module != null);
                replacementDisplayModule = displaySource.deck?.Module;
            }
            deckMap[(source.Ship.Id, source.Deck.Index)] = index;
            compositeDeckByCell[(source.Row, source.Col)] = index;
            composite.Decks.Add(new Deck
            {
                Index = index,
                OffsetRow = source.Row - anchor.Row,
                OffsetCol = source.Col - anchor.Col,
                MaxHp = source.Deck.MaxHp,
                CurrentHp = source.Deck.CurrentHp,
                // When the moving deck replaces a special module, the shared physical deck keeps
                // that module identity/state; every weapon from both source decks is attached below.
                Module = isReplacementDeck && replacementDisplayModule != null
                    ? replacementDisplayModule
                    : source.Deck.Module,
                // The physical deck is globally disabled only when every retained module at
                // the overlap was already disabled. Per-source differences live on Weapon.
                ModuleDestroyed = isReplacementDeck && replacementModules is { Count: > 0 }
                    ? replacementModules.All(value =>
                        !SourceDeckHasOperationalModule(value.ship, value.deck))
                    : !SourceDeckHasOperationalModule(source.Ship, source.Deck),
            });
        }

        foreach (var sourceShip in sourceShips)
        foreach (var weapon in sourceShip.Weapons)
        {
            var sourceDeck = sourceShip.Decks.FirstOrDefault(deck => deck.Index == weapon.DeckIndex);
            if (!deckMap.TryGetValue((sourceShip.Id, weapon.DeckIndex), out var newDeckIndex))
            {
                // The moving deck physically replaces exactly one target deck, but the
                // resulting construction keeps every pre-merge weapon. Attach a weapon from
                // that replaced deck to the replacement deck occupying the same cell.
                var replacedDeck = sourceShip == target
                    ? target.Decks.FirstOrDefault(deck =>
                        deck.Index == weapon.DeckIndex && overlappedTargetDecks.Contains(deck.Index))
                    : null;
                if (replacedDeck == null)
                    continue;
                var replacedCell = target.GetDeckCell(
                    replacedDeck, target.Row, target.Col, target.Orientation);
                if (!compositeDeckByCell.TryGetValue(replacedCell, out newDeckIndex))
                    continue;
            }
            composite.Weapons.Add(new Weapon
            {
                Type = weapon.Type,
                Ammo = weapon.Ammo,
                MaxAmmo = weapon.MaxAmmo,
                AimSpeed = weapon.AimSpeed,
                ShipId = composite.Id,
                DeckIndex = newDeckIndex,
                PreservedModuleDestroyed = weapon.PreservedModuleDestroyed ||
                    (compositeDeckByCell[replacedTargetCell] == newDeckIndex &&
                     sourceDeck != null &&
                     (sourceDeck.IsDestroyed || sourceDeck.ModuleDestroyed)),
                ConfiguredShotType = weapon.ConfiguredShotType,
            });
        }

        var currentCompositeCells = compositeDeckByCell.Keys.ToHashSet();
        foreach (var cell in player.Board.Grid.Cast<Cell>())
        {
            // Historical observations follow the same retained physical deck when it receives
            // the composite identity. The replaced target deck no longer exists, so only its
            // exact key is severed; all visual snapshot fields remain as last-known history.
            if (cell.KnownShipId is { } knownShipId &&
                (knownShipId == mergingShip.Id || knownShipId == target.Id))
            {
                if (deckMap.TryGetValue((knownShipId, cell.KnownDeckIndex), out var knownDeckIndex))
                {
                    cell.KnownShipId = composite.Id;
                    cell.KnownDeckIndex = knownDeckIndex;
                }
                else
                {
                    cell.KnownShipId = null;
                    cell.KnownDeckIndex = -1;
                }
            }

            if (cell.ShipRef == mergingShip || cell.ShipRef == target)
                cell.ShipRef = null;
        }

        foreach (var (position, _) in oldDeckByCell)
        {
            if (currentCompositeCells.Contains(position))
                continue;

            var cell = player.Board.GetCell(position.Row, position.Col);
            if (cell == null) continue;

            // Existing exact observations remain in the cell snapshot. Coordinate-level reveal
            // alone may describe water and must never manufacture an observed ship.
            cell.IsHit = false;
        }

        composite.ManeuverStaleHitCells = inheritedStaleCells
            .Concat(oldDeckByCell.Keys)
            .Where(position => !currentCompositeCells.Contains(position))
            .Distinct()
            .ToList();
        player.Board.PlacedShips.Remove(mergingShip);
        player.Board.PlacedShips.Remove(target);
        player.Fleet.Remove(mergingShip);
        player.Fleet.Remove(target);
        player.Board.PlacedShips.Add(composite);
        player.Fleet.Add(composite);
        foreach (var (row, col) in composite.GetOccupiedCells())
            player.Board.Grid[row, col].ShipRef = composite;

        mergingShip.IsPlaced = false;
        target.IsPlaced = false;
        return true;
    }

    private static bool SourceDeckHasModule(Ship ship, Deck deck) =>
        deck.Module != null || ship.Weapons.Any(weapon => weapon.DeckIndex == deck.Index);

    private static bool SourceDeckHasOperationalModule(Ship ship, Deck deck)
    {
        if (deck.IsDestroyed || deck.ModuleDestroyed) return false;
        var weapons = ship.Weapons.Where(weapon => weapon.DeckIndex == deck.Index).ToList();
        return weapons.Count == 0
            ? deck.Module != null
            : weapons.Any(weapon => !weapon.PreservedModuleDestroyed);
    }

    private sealed record RetainedDeck(Ship Ship, Deck Deck, int Row, int Col);
    private sealed record SourceDeckAtCell(Ship Ship, Deck Deck, int Row, int Col);
}
