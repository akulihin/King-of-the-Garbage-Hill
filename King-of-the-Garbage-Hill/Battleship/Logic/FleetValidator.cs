using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Validates fleet selection: 40 coin budget, max 3 regions, valid ship/upgrade combos.
/// Fleet template: 4x1-deck, 3x2-deck, 2x3-deck, 1x4-deck (10 ships total).
/// Purchased ships replace defaults of same deck count.
/// </summary>
public static class FleetValidator
{
    public const int MaxBudget = 40;
    public const int MaxRegions = 3;

    /// <summary>Template: deck-count → number of ships required.</summary>
    public static readonly Dictionary<int, int> Template = new() { { 1, 4 }, { 2, 3 }, { 3, 2 }, { 4, 1 } };

    /// <summary>
    /// Validates purchased ship selections (budget, regions, upgrade combos, deck-count slots).
    /// Purchases are non-free ships only; free defaults are filled by BuildFleetFromSelections.
    /// </summary>
    public static (bool valid, string error) ValidateFleet(List<FleetSelection> selections)
    {
        if (selections == null) selections = new List<FleetSelection>();

        var totalCost = 0;
        var regions = new HashSet<Region>();

        // Count purchased ships per deck-count
        var purchasedPerDeck = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } };

        foreach (var sel in selections)
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def == null)
                return (false, $"Неизвестный корабль: {sel.DefinitionId}");

            // Free ships are not purchases
            if (def.IsFree) continue;

            var shipCost = def.Cost;

            // Validate upgrades
            if (sel.Upgrades != null)
            {
                if (sel.Upgrades.Contains("tetra_boiler_fire") && sel.Upgrades.Contains("tetra_boiler_brander"))
                    return (false, "Греческий огонь и Брандер взаимоисключающие апгрейды.");

                foreach (var uid in sel.Upgrades)
                {
                    var upgDef = def.AvailableUpgrades?.Find(u => u.Id == uid);
                    if (upgDef == null)
                        return (false, $"Неизвестный апгрейд {uid} для корабля {def.Name}");
                    shipCost += upgDef.Cost;
                }
            }

            totalCost += shipCost;
            foreach (var r in def.Regions)
                if (r != Region.Tetracor)
                    regions.Add(r);

            if (purchasedPerDeck.ContainsKey(def.DeckCount))
                purchasedPerDeck[def.DeckCount]++;

            // Check slot overflow
            if (purchasedPerDeck[def.DeckCount] > Template.GetValueOrDefault(def.DeckCount, 0))
                return (false, $"Слишком много кораблей с {def.DeckCount} палубами.");
        }

        // Also count upgrade costs on free ships (triple, tetranavis)
        foreach (var sel in selections)
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def == null || !def.IsFree) continue;
            if (sel.Upgrades != null)
            {
                foreach (var uid in sel.Upgrades)
                {
                    var upgDef = def.AvailableUpgrades?.Find(u => u.Id == uid);
                    if (upgDef != null) totalCost += upgDef.Cost;
                }
            }
        }

        if (totalCost > MaxBudget)
            return (false, $"Превышен бюджет: {totalCost}/{MaxBudget} монет.");

        if (regions.Count > MaxRegions)
            return (false, $"Максимум {MaxRegions} региона. Выбрано: {regions.Count}.");

        return (true, null);
    }

    /// <summary>
    /// Builds full 10-ship fleet from purchases by filling remaining slots with defaults.
    /// </summary>
    public static List<FleetSelection> BuildFleetFromSelections(List<FleetSelection> purchases)
    {
        var result = new List<FleetSelection>();

        // Separate free-ship upgrade entries from actual purchases
        var freeShipUpgrades = new Dictionary<string, List<string>>();
        var purchasedPerDeck = new Dictionary<int, List<FleetSelection>> { { 1, new() }, { 2, new() }, { 3, new() }, { 4, new() } };

        foreach (var sel in purchases ?? new List<FleetSelection>())
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def == null) continue;
            if (def.IsFree)
            {
                // Store upgrades for free ships
                if (sel.Upgrades is { Count: > 0 })
                    freeShipUpgrades[sel.DefinitionId] = sel.Upgrades;
                continue;
            }
            purchasedPerDeck[def.DeckCount].Add(sel);
        }

        // Default free ships per deck count
        var defaults = new Dictionary<int, string> { { 1, "single" }, { 2, "double" }, { 3, "triple" }, { 4, "tetranavis" } };

        foreach (var (deckCount, needed) in Template)
        {
            // Add purchased ships
            foreach (var p in purchasedPerDeck[deckCount])
                result.Add(p);

            // Fill remaining with defaults
            var remaining = needed - purchasedPerDeck[deckCount].Count;
            var defaultId = defaults[deckCount];
            var defaultDef = ShipCatalog.GetById(defaultId);
            for (var i = 0; i < remaining; i++)
            {
                var upgrades = i == 0 && freeShipUpgrades.TryGetValue(defaultId, out var u) ? u : new List<string>();
                result.Add(new FleetSelection
                {
                    DefinitionId = defaultId,
                    ShipName = defaultDef?.Name ?? defaultId,
                    Cost = 0,
                    Upgrades = upgrades
                });
            }
        }

        return result;
    }

    public static int CalculateTotalCost(List<FleetSelection> selections)
    {
        var total = 0;
        foreach (var sel in selections)
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def == null) continue;
            total += def.Cost;
            if (sel.Upgrades != null)
            {
                foreach (var uid in sel.Upgrades)
                {
                    var upgDef = def.AvailableUpgrades?.Find(u => u.Id == uid);
                    if (upgDef != null) total += upgDef.Cost;
                }
            }
        }
        return total;
    }

    /// <summary>
    /// Get the full 10-ship default fleet (all free, no upgrades).
    /// </summary>
    public static List<FleetSelection> GetDefaultFleet()
    {
        return new List<FleetSelection>
        {
            new() { DefinitionId = "single", ShipName = "Single", Cost = 0 },
            new() { DefinitionId = "single", ShipName = "Single", Cost = 0 },
            new() { DefinitionId = "single", ShipName = "Single", Cost = 0 },
            new() { DefinitionId = "single", ShipName = "Single", Cost = 0 },
            new() { DefinitionId = "double", ShipName = "Double", Cost = 0 },
            new() { DefinitionId = "double", ShipName = "Double", Cost = 0 },
            new() { DefinitionId = "double", ShipName = "Double", Cost = 0 },
            new() { DefinitionId = "triple", ShipName = "Triple", Cost = 0 },
            new() { DefinitionId = "triple", ShipName = "Triple", Cost = 0 },
            new() { DefinitionId = "tetranavis", ShipName = "Tetranavis", Cost = 0 },
        };
    }
}
