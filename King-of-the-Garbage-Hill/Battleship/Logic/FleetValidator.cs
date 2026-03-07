using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Validates fleet selection: 40 coin budget, max 3 regions, valid ship/upgrade combos.
/// </summary>
public static class FleetValidator
{
    public const int MaxBudget = 40;
    public const int MaxRegions = 3;

    public static (bool valid, string error) ValidateFleet(List<FleetSelection> selections)
    {
        if (selections == null || selections.Count == 0)
            return (false, "Нужно выбрать хотя бы один корабль.");

        var totalCost = 0;
        var regions = new HashSet<Region>();

        // Must include basic fleet: 1 single, 1 double, 1 triple, 1 tetranavis
        var hasBasicSingle = false;
        var hasBasicDouble = false;
        var hasBasicTriple = false;
        var hasTetranavis = false;

        foreach (var sel in selections)
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def == null)
                return (false, $"Неизвестный корабль: {sel.DefinitionId}");

            var shipCost = def.Cost;

            // Validate upgrades
            if (sel.Upgrades != null)
            {
                // Greek Fire and Brander are mutually exclusive
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
            // Tetracor region is exempt from the 3-region limit
            foreach (var r in def.Regions)
                if (r != Region.Tetracor)
                    regions.Add(r);

            switch (def.Id)
            {
                case "single": hasBasicSingle = true; break;
                case "double": hasBasicDouble = true; break;
                case "triple": hasBasicTriple = true; break;
                case "tetranavis": hasTetranavis = true; break;
            }
        }

        if (!hasBasicSingle || !hasBasicDouble || !hasBasicTriple || !hasTetranavis)
            return (false, "Флот должен содержать базовые корабли: Одинарка, Двойка, Тройка, Тетранавис.");

        if (totalCost > MaxBudget)
            return (false, $"Превышен бюджет: {totalCost}/{MaxBudget} монет.");

        if (regions.Count > MaxRegions)
            return (false, $"Максимум {MaxRegions} региона. Выбрано: {regions.Count}.");

        return (true, null);
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
    /// Get the default free fleet (basic ships without upgrades).
    /// </summary>
    public static List<FleetSelection> GetDefaultFleet()
    {
        return new List<FleetSelection>
        {
            new() { DefinitionId = "single", ShipName = "Single", Cost = 0 },
            new() { DefinitionId = "double", ShipName = "Double", Cost = 0 },
            new() { DefinitionId = "triple", ShipName = "Triple", Cost = 0 },
            new() { DefinitionId = "tetranavis", ShipName = "Tetranavis", Cost = 0 },
        };
    }
}
