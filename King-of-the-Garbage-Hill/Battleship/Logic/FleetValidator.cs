using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Validates fleet selection: faction budget, max 3 regions, valid ship/upgrade combos.
/// Fleet template: 4x1-deck, 3x2-deck, 2x3-deck, 1x4-deck (10 ships total).
/// Purchased ships replace defaults of same deck count.
/// </summary>
public static class FleetValidator
{
    public const int EmpireBudget = 40;
    public const int AllianceBudget = 50;
    public const int MaxRegions = 3;

    public static int GetBudget(Faction faction) =>
        faction == Faction.Alliance ? AllianceBudget : EmpireBudget;

    /// <summary>Template: deck-count → number of ships required.</summary>
    public static readonly Dictionary<int, int> Template = new() { { 1, 4 }, { 2, 3 }, { 3, 2 }, { 4, 1 } };

    /// <summary>
    /// Validates purchased ship selections (budget, regions, upgrade combos, deck-count slots).
    /// Purchases are non-free ships only; free defaults are filled by BuildFleetFromSelections.
    /// </summary>
    public static (bool valid, string error) ValidateFleet(
        List<FleetSelection> selections,
        Faction faction = Faction.Empire)
    {
        if (selections == null) selections = new List<FleetSelection>();

        var totalCost = 0;
        var regions = new HashSet<Region>();
        var freeSelectionsPerDefinition = new Dictionary<string, int>();
        var doubleMastUpgradeCount = 0;

        // Count purchased ships per deck-count
        var purchasedPerDeck = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } };

        foreach (var sel in selections)
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def == null)
                return (false, $"Неизвестный корабль: {sel.DefinitionId}");
            if (!def.Factions.Contains(faction))
                return (false, $"{def.NameRu ?? def.Name} недоступен для фракции {faction}.");

            if (sel.Upgrades?.Count != sel.Upgrades?.Distinct().Count())
                return (false, $"Апгрейд корабля {def.Name} выбран несколько раз.");

            if (def.Id == "double" && sel.Upgrades?.Contains("double_mast") == true &&
                ++doubleMastUpgradeCount > 1)
                return (false, "Мачту для Double можно купить только один раз.");

            if (sel.Upgrades?.Contains("tetra_discus") == true)
                return (false, "Дискобол пока не реализован и недоступен для покупки.");

            var boilerUpgradeCount = sel.Upgrades?.Count(upgradeId =>
                upgradeId is "tetra_boiler_fire" or
                    "tetra_boiler_evil_fire" or
                    "tetra_boiler_brander") ?? 0;
            if (boilerUpgradeCount > 1)
                return (false, "Греческий огонь, Злой Греческий огонь и Брандер взаимоисключающие апгрейды.");

            if (def.IsFree)
            {
                freeSelectionsPerDefinition[def.Id] = freeSelectionsPerDefinition.GetValueOrDefault(def.Id) + 1;
                if (freeSelectionsPerDefinition[def.Id] > Template.GetValueOrDefault(def.DeckCount, 0))
                    return (false, $"Слишком много экземпляров {def.Name} с индивидуальными апгрейдами.");

                foreach (var uid in sel.Upgrades ?? new List<string>())
                {
                    var upgDef = def.AvailableUpgrades?.Find(u => u.Id == uid);
                    if (upgDef == null)
                        return (false, $"Неизвестный апгрейд {uid} для корабля {def.Name}");
                    totalCost += upgDef.Cost;
                }
                continue;
            }

            var shipCost = def.Cost;

            // Validate upgrades
            if (sel.Upgrades != null)
            {
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

        var maxBudget = GetBudget(faction);
        if (totalCost > maxBudget)
            return (false, $"Превышен бюджет: {totalCost}/{maxBudget} монет.");

        if (regions.Count > MaxRegions)
            return (false, $"Максимум {MaxRegions} региона. Выбрано: {regions.Count}.");

        var defaultIds = DefaultIds(faction);
        foreach (var (deckCount, defaultId) in defaultIds)
        {
            var remainingDefaultSlots = Template[deckCount] - purchasedPerDeck[deckCount];
            if (freeSelectionsPerDefinition.GetValueOrDefault(defaultId) > remainingDefaultSlots)
                return (false, $"Для {defaultId} выбрано больше индивидуальных апгрейдов, чем осталось бесплатных слотов.");
        }

        return (true, null);
    }

    /// <summary>
    /// Builds full 10-ship fleet from purchases by filling remaining slots with defaults.
    /// </summary>
    public static List<FleetSelection> BuildFleetFromSelections(
        List<FleetSelection> purchases,
        Faction faction = Faction.Empire)
    {
        var result = new List<FleetSelection>();

        // Separate free-ship upgrade entries from actual purchases
        var freeShipUpgrades = new Dictionary<string, Queue<List<string>>>();
        var purchasedPerDeck = new Dictionary<int, List<FleetSelection>> { { 1, new() }, { 2, new() }, { 3, new() }, { 4, new() } };

        foreach (var sel in purchases ?? new List<FleetSelection>())
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def == null || !def.Factions.Contains(faction)) continue;
            if (def.IsFree)
            {
                // Each free Triple/Tetranavis slot keeps its own paid upgrades.
                if (!freeShipUpgrades.TryGetValue(sel.DefinitionId, out var queue))
                {
                    queue = new Queue<List<string>>();
                    freeShipUpgrades[sel.DefinitionId] = queue;
                }
                queue.Enqueue(sel.Upgrades is { Count: > 0 } ? new List<string>(sel.Upgrades) : new List<string>());
                continue;
            }
            purchasedPerDeck[def.DeckCount].Add(sel);
        }

        // Default free ships per deck count
        var defaults = DefaultIds(faction);

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
                var upgrades = freeShipUpgrades.TryGetValue(defaultId, out var queue) && queue.Count > 0
                    ? queue.Dequeue()
                    : new List<string>();
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
    public static List<FleetSelection> GetDefaultFleet(Faction faction = Faction.Empire)
    {
        var flagshipId = faction == Faction.Alliance ? "famous_diagonal_ship" : "tetranavis";
        var flagshipName = ShipCatalog.GetById(flagshipId)?.Name ?? flagshipId;
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
            new() { DefinitionId = flagshipId, ShipName = flagshipName, Cost = 0 },
        };
    }

    private static Dictionary<int, string> DefaultIds(Faction faction) => new()
    {
        { 1, "single" },
        { 2, "double" },
        { 3, "triple" },
        { 4, faction == Faction.Alliance ? "famous_diagonal_ship" : "tetranavis" },
    };
}
