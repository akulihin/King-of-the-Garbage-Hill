using System;
using System.Collections.Generic;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Static catalog of all ship definitions from the GDD.
/// Basic ships are free; upgraded ships cost coins from the 40-coin budget.
/// </summary>
public static class ShipCatalog
{
    public static readonly List<ShipDefinition> AllShips = new()
    {
        // ── Basic (free) ships ──────────────────────────────────────
        new ShipDefinition
        {
            Id = "single", Name = "Single", NameRu = "Однёр",
            DeckCount = 1, Range = RangeClass.Close, Cost = 0, IsFree = true,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { },
            Description = "Basic 1-deck close-range ship",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "double", Name = "Double", NameRu = "Двухпалубник",
            DeckCount = 2, Range = RangeClass.Mid, Cost = 0, IsFree = true,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { },
            Description = "Basic 2-deck mid-range ship",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "triple", Name = "Triple", NameRu = "Тройка",
            DeckCount = 3, Range = RangeClass.Tetra, Cost = 0, IsFree = true,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { Region.Tetracor },
            Description = "Basic 3-deck ship with Tetracatapult",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Tetracatapult, Damage = 2, Ammo = 1, DeckIndex = 1, AimSpeed = 20 }
            },
            AvailableUpgrades = new()
            {
                new UpgradeDefinition { Id = "triple_crew", Name = "Crew", NameRu = "Экипаж", Cost = 2, Description = "Spawns pirate boat on death", Effect = "spawn_pirate_boat" },
                new UpgradeDefinition { Id = "triple_ammo", Name = "Extra Ammo", NameRu = "Доп. снаряды", Cost = 4, Description = "+2 White Stones on boarding", Effect = "extra_ammo" },
                new UpgradeDefinition { Id = "triple_armor_1", Name = "Armor Deck 1", NameRu = "Броня палуба 1", Cost = 4, Description = "+4 HP to deck 1", Effect = "armor_deck_0" },
                new UpgradeDefinition { Id = "triple_armor_2", Name = "Armor Deck 2", NameRu = "Броня палуба 2", Cost = 4, Description = "+4 HP to deck 2", Effect = "armor_deck_1" },
                new UpgradeDefinition { Id = "triple_armor_3", Name = "Armor Deck 3", NameRu = "Броня палуба 3", Cost = 4, Description = "+4 HP to deck 3", Effect = "armor_deck_2" },
            }
        },
        new ShipDefinition
        {
            Id = "tetranavis", Name = "Tetranavis", NameRu = "Тетранавис",
            DeckCount = 4, Range = RangeClass.Mid, Cost = 0, IsFree = true,
            DeckHpOverrides = new() { 2, 4, 2, 2 }, Speed = 1, Space = 1, Regions = new() { Region.Tetracor },
            Description = "4-deck flagship with boiler, mast, tetracatapult, and ballista",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Boiler, Damage = 0, DeckIndex = 0 },
                new WeaponTemplate { Type = WeaponType.Mast, Damage = 0, DeckIndex = 1 },
                new WeaponTemplate { Type = WeaponType.Tetracatapult, Damage = 2, Ammo = 1, DeckIndex = 2, AimSpeed = 20 },
                new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2, DeckIndex = 3 },
            },
            AvailableUpgrades = new()
            {
                new UpgradeDefinition { Id = "tetra_discus", Name = "Discus Thrower", NameRu = "Дискобол", Cost = 1, Description = "Additional disc projectile", Effect = "discus" },
                new UpgradeDefinition { Id = "tetra_boiler_fire", Name = "Greek Fire", NameRu = "Греческий огонь", Cost = 4, Description = "Boiler produces Greek Fire summon", Effect = "greek_fire" },
                new UpgradeDefinition { Id = "tetra_boiler_brander", Name = "Brander", NameRu = "Брандер", Cost = 4, Description = "Boiler produces Brander summon", Effect = "brander" },
            }
        },

        // ── Upgraded (cost coins) ships ─────────────────────────────
        new ShipDefinition
        {
            Id = "desiccator", Name = "Desiccator", NameRu = "Иссушитель",
            DeckCount = 1, Range = RangeClass.Close, Cost = 34,
            DefaultArmor = 1, Speed = 3, Space = 1, Regions = new() { Region.South, Region.West },
            Abilities = new() { "nimble", "ballista_immune", "auto_win_boarding" },
            Description = "Nimble + auto-win on boarding",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "drakkar", Name = "Drakkar", NameRu = "Драккар",
            DeckCount = 3, Range = RangeClass.CloseMelee, Cost = 28,
            DeckHpOverrides = new() { 4, 4, 4 }, Speed = 1, Space = 1, Regions = new() { Region.North },
            Abilities = new() { "freeze_nearby", "burn_resist" },
            Description = "Freezes nearby ships, immune to burn, no ranged weapons",
            DefaultWeapons = new() // CloseMelee = no ranged weapons
        },
        new ShipDefinition
        {
            Id = "alchi_iceberg", Name = "Alchi-Iceberg", NameRu = "Алхи-Айсберг",
            DeckCount = 1, Range = RangeClass.Mid, Cost = 25,
            DeckHpOverrides = new() { 6 }, Speed = 0, Space = 1, IsHome = true, Regions = new() { Region.North, Region.East },
            Abilities = new() { "burn_resist", "poison_cone" },
            Description = "Burn resist + poison cone attack",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "nimble_single", Name = "Nimble Single", NameRu = "Юркая единичка",
            DeckCount = 1, Range = RangeClass.Close, Cost = 16,
            DefaultArmor = 1, Speed = 3, Space = 1, Regions = new() { Region.West },
            Abilities = new() { "nimble", "ballista_immune" },
            Description = "Immune to ballista shots",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "alchi_barge", Name = "Alchi-Barge", NameRu = "Алхи-Баржа",
            DeckCount = 1, Range = RangeClass.Close, Cost = 15,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { Region.East },
            Abilities = new() { "poison_cone" },
            Description = "Poison cone attack",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "light_wood_triple", Name = "Light Wood Triple", NameRu = "Тройка из лёгкого дерева",
            DeckCount = 3, Range = RangeClass.Mid, Cost = 14,
            DeckHpOverrides = new() { 1, 1, 1 }, Speed = 2, Space = 1, Regions = new() { Region.West },
            Abilities = new() { "auto_dodge_bow_stern" },
            Description = "Auto-dodges bow/stern shots, speed 2",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "toros", Name = "Toros", NameRu = "Торос",
            DeckCount = 1, Range = RangeClass.Mid, Cost = 12,
            DeckHpOverrides = new() { 6 }, Speed = 0, Space = 1, IsHome = true, Regions = new() { Region.North },
            Abilities = new() { "burn_resist", "stationary" },
            Description = "Heavy armor, stationary, burn resist",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "cursed_pirate", Name = "Cursed Pirate", NameRu = "Проклятый пират",
            DeckCount = 1, Range = RangeClass.Close, Cost = 6,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { Region.South },
            Abilities = new() { "spawn_cursed_boat" },
            Description = "Spawns cursed boat on death",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "incendiary_barge", Name = "Incendiary Barge", NameRu = "Горючая баржа",
            DeckCount = 2, Range = RangeClass.Far, Cost = 10,
            DeckHpOverrides = new() { 1, 1 }, Speed = 1, Space = 1, ExplosionRadius = 2, Regions = new() { Region.East },
            Abilities = new() { "explode_on_hit" },
            Description = "Explodes on ANY hit, burns area (radius=2). Горючка weapon.",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Incendiary, Damage = 0, Ammo = 2, DeckIndex = 0, AimSpeed = 20 } // Горючка
            }
        },
        new ShipDefinition
        {
            Id = "maneuvering_double", Name = "Maneuvering Double", NameRu = "Маневрирующая двойка",
            DeckCount = 2, Range = RangeClass.Mid, Cost = 5,
            DeckHpOverrides = new() { 1, 1 }, Speed = 2, Space = 1, Regions = new() { Region.West },
            Abilities = new() { "manual_move_after_hit" },
            Description = "Manual move after being hit, speed 2",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
        new ShipDefinition
        {
            Id = "pirates", Name = "Pirates", NameRu = "Пираты",
            DeckCount = 2, Range = RangeClass.Mid, Cost = 4,
            DeckHpOverrides = new() { 2, 2 }, Space = 1, Regions = new() { Region.South },
            Abilities = new() { "spawn_pirate_boat" },
            Description = "Spawns pirate boat on death",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista, Damage = 2 } }
        },
    };

    public static ShipDefinition GetById(string id)
    {
        return AllShips.Find(s => s.Id == id);
    }

    /// <summary>
    /// Create a Ship instance from a definition, applying upgrades.
    /// </summary>
    public static Ship CreateShip(ShipDefinition def, List<string> upgradeIds = null)
    {
        var ship = new Ship
        {
            DefinitionId = def.Id,
            Name = def.Name,
            Range = def.Range,
            Cost = def.Cost,
            Space = def.Space,
            ExplosionRadius = def.ExplosionRadius,
            Speed = def.Speed,
            Regions = new List<Region>(def.Regions),
            Abilities = new List<string>(def.Abilities),
            IsHome = def.IsHome,
        };

        // Create decks
        for (var i = 0; i < def.DeckCount; i++)
        {
            var hp = def.DeckHpOverrides != null && i < def.DeckHpOverrides.Count
                ? def.DeckHpOverrides[i]
                : def.DefaultArmor;

            ship.Decks.Add(new Deck { Index = i, MaxHp = hp, CurrentHp = hp });
        }

        // Add weapons
        foreach (var wt in def.DefaultWeapons)
        {
            ship.Weapons.Add(new Weapon
            {
                Type = wt.Type,
                Ammo = wt.Ammo,
                Damage = wt.Damage,
                AimSpeed = wt.AimSpeed,
                ShipId = ship.Id
            });
        }

        // Assign modules to decks for Tetranavis
        if (def.Id == "tetranavis")
        {
            if (ship.Decks.Count > 0) ship.Decks[0].Module = "boiler";
            if (ship.Decks.Count > 1) ship.Decks[1].Module = "mast";
            if (ship.Decks.Count > 2) ship.Decks[2].Module = "tetracatapult";
            if (ship.Decks.Count > 3) ship.Decks[3].Module = "ballista";
        }

        // Apply upgrades
        if (upgradeIds != null && upgradeIds.Count > 0)
        {
            foreach (var uid in upgradeIds)
            {
                ship.Upgrades.Add(uid);
                ApplyUpgrade(ship, def, uid);
            }

            // Any upgrade on a Triple makes it "домашний"
            if (def.Id == "triple")
                ship.IsHome = true;
        }

        // Burn resist ships get the status by default
        if (def.Abilities.Contains("burn_resist"))
            ship.Statuses.Add(ShipStatusType.BurnResist);

        return ship;
    }

    private static void ApplyUpgrade(Ship ship, ShipDefinition def, string upgradeId)
    {
        var upgradeDef = def.AvailableUpgrades?.Find(u => u.Id == upgradeId);
        if (upgradeDef == null) return;

        switch (upgradeDef.Effect)
        {
            case "extra_ammo":
                // Don't add ammo now — +2 White Stones added on boarding
                ship.Abilities.Add("extra_ammo_boarding");
                break;

            case "armor_deck_0":
            case "armor_deck_1":
            case "armor_deck_2":
                var deckIdx = int.Parse(upgradeDef.Effect.Split('_')[2]);
                if (deckIdx < ship.Decks.Count)
                {
                    ship.Decks[deckIdx].MaxHp = Math.Min(ship.Decks[deckIdx].MaxHp + 4, 9);
                    ship.Decks[deckIdx].CurrentHp = ship.Decks[deckIdx].MaxHp;
                }
                break;

            case "spawn_pirate_boat":
                if (!ship.Abilities.Contains("spawn_pirate_boat"))
                    ship.Abilities.Add("spawn_pirate_boat");
                break;

            case "discus":
                ship.Abilities.Add("discus_thrower");
                break;

            case "greek_fire":
                ship.Abilities.Add("greek_fire_summon");
                // Add one-shot GreekFire weapon so the player can select & fire it
                ship.Weapons.Add(new Weapon
                {
                    Type = WeaponType.GreekFire,
                    Ammo = 1,
                    Damage = 0, // burn mechanic handles damage
                    ShipId = ship.Id,
                });
                break;

            case "brander":
                ship.Abilities.Add("brander_summon");
                break;
        }
    }
}
