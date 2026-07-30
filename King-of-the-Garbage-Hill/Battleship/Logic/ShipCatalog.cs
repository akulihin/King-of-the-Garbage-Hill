using System;
using System.Collections.Generic;
using System.Linq;
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
            Factions = new() { Faction.Empire, Faction.Alliance },
            Description = "Базовый однопалубный корабль ближнего боя с Баллистой.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "double", Name = "Double", NameRu = "Двухпалубник",
            DeckCount = 2, Range = RangeClass.Mid, Cost = 0, IsFree = true,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { },
            Factions = new() { Faction.Empire, Faction.Alliance },
            Description = "Базовый двухпалубный корабль средней дальности с Баллистой.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "triple", Name = "Triple", NameRu = "Тройка",
            DeckCount = 3, Range = RangeClass.Tetra, Cost = 0, IsFree = true,
            DefaultArmor = 2, DeckHpOverrides = new() { 2, 4, 2 },
            Speed = 1, Space = 1, Regions = new() { Region.Tetracor },
            Factions = new() { Faction.Empire, Faction.Alliance },
            Description = "Базовый трёхпалубный артиллерийский корабль с Тетракамнемётом.",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Tetracatapult, Ammo = 1, DeckIndex = 1, AimSpeed = 20 }
            },
            AvailableUpgrades = new()
            {
                new UpgradeDefinition { Id = "triple_crew", Name = "Crew", NameRu = "Экипаж", Cost = 2, Description = "Если Трёшка дожила до абордажа, выпускает одну Пиратскую лодку.", Effect = "spawn_pirate_boat" },
                new UpgradeDefinition { Id = "triple_ammo", Name = "Extra Ammo", NameRu = "Второй боезапас", Cost = 4, Description = "При начале абордажа Тетракамнемёт получает 2 дополнительных выбранных снаряда.", Effect = "extra_ammo" },
                new UpgradeDefinition { Id = "triple_armor_1", Name = "Armor Deck 1", NameRu = "Броня палубы 1", Cost = 4, Description = "Увеличивает прочность первой палубы на 4, но не выше 9.", Effect = "armor_deck_0" },
                new UpgradeDefinition { Id = "triple_armor_2", Name = "Armor Deck 2", NameRu = "Броня палубы 2", Cost = 4, Description = "Увеличивает прочность второй палубы на 4, но не выше 9.", Effect = "armor_deck_1" },
                new UpgradeDefinition { Id = "triple_armor_3", Name = "Armor Deck 3", NameRu = "Броня палубы 3", Cost = 4, Description = "Увеличивает прочность третьей палубы на 4, но не выше 9.", Effect = "armor_deck_2" },
            }
        },
        new ShipDefinition
        {
            Id = "tetranavis", Name = "Tetranavis", NameRu = "Тетранавис",
            DeckCount = 4, Range = RangeClass.Mid, Cost = 0, IsFree = true,
            DeckHpOverrides = new() { 2, 4, 2, 2 }, Speed = 1, Space = 1, Regions = new() { Region.Tetracor },
            Description = "Четырёхпалубный флагман: Котельная, Мачта, Тетракамнемёт и Баллиста.",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Boiler, DeckIndex = 0 },
                new WeaponTemplate { Type = WeaponType.Mast, DeckIndex = 1 },
                new WeaponTemplate { Type = WeaponType.Tetracatapult, Ammo = 1, DeckIndex = 2, AimSpeed = 20 },
                new WeaponTemplate { Type = WeaponType.Ballista, DeckIndex = 3 },
            },
            AvailableUpgrades = new()
            {
                new UpgradeDefinition { Id = "tetra_discus", Name = "Discus Thrower", NameRu = "Дискобол", Cost = 1, Description = "Дополнительный дисковый снаряд. Пока не реализован.", Effect = "discus" },
                new UpgradeDefinition { Id = "tetra_boiler_fire", Name = "Greek Fire", NameRu = "Греческий огонь", Cost = 4, Description = "Котельная даёт один выстрел Греческим огнём по своему полю.", Effect = "greek_fire" },
                new UpgradeDefinition { Id = "tetra_boiler_evil_fire", Name = "Evil Greek Fire", NameRu = "Злой Греческий огонь", Cost = 6, Description = "Котельная даёт один выстрел Злым Греческим огнём, доступный в паузу между выстрелами противника.", Effect = "evil_greek_fire" },
                new UpgradeDefinition { Id = "tetra_boiler_brander", Name = "Brander", NameRu = "Брандер", Cost = 4, Description = "Котельная позволяет один раз за матч призвать Брандер.", Effect = "brander" },
            }
        },
        new ShipDefinition
        {
            Id = "famous_diagonal_ship", Name = "Знаменитый диагональный корабль", NameRu = "Знаменитый диагональный корабль",
            DeckCount = 4, Range = RangeClass.Mid, Cost = 0, IsFree = true, IsHome = true,
            DeckHpOverrides = new() { 4, 4, 4, 4 }, Speed = 1, Space = 1,
            Regions = new() { Region.Tetracor }, Factions = new() { Faction.Alliance },
            Abilities = new() { "diagonal_shape" },
            Description = "Домашний флагман Альянса с четырьмя палубами по диагонали: Котельная, Мачта, Тетракамнемёт и Баллиста.",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Boiler, DeckIndex = 0 },
                new WeaponTemplate { Type = WeaponType.Mast, DeckIndex = 1 },
                new WeaponTemplate { Type = WeaponType.Tetracatapult, Ammo = 1, DeckIndex = 2, AimSpeed = 20 },
                new WeaponTemplate { Type = WeaponType.Ballista, DeckIndex = 3 },
            },
            AvailableUpgrades = new()
            {
                new UpgradeDefinition { Id = "tetra_boiler_fire", Name = "Greek Fire", NameRu = "Греческий огонь", Cost = 4, Description = "Котельная даёт один выстрел Греческим огнём по своему полю.", Effect = "greek_fire" },
                new UpgradeDefinition { Id = "tetra_boiler_evil_fire", Name = "Evil Greek Fire", NameRu = "Злой Греческий огонь", Cost = 6, Description = "Котельная даёт один выстрел Злым Греческим огнём, доступный в паузу между выстрелами противника.", Effect = "evil_greek_fire" },
                new UpgradeDefinition { Id = "tetra_boiler_brander", Name = "Brander", NameRu = "Брандер", Cost = 4, Description = "Котельная позволяет один раз за матч призвать Брандер.", Effect = "brander" },
            }
        },

        // ── Upgraded (cost coins) ships ─────────────────────────────
        new ShipDefinition
        {
            Id = "desiccator", Name = "Desiccator", NameRu = "Иссушитель",
            DeckCount = 1, Range = RangeClass.Close, Cost = 34,
            DefaultArmor = 1, Speed = 3, Space = 1, Regions = new() { Region.South, Region.West },
            Abilities = new() { "ballista_immune", "auto_win_boarding" },
            Description = "Не получает урон от Баллисты и гарантирует победу при абордаже, пока жив и не встречает второго живого Иссушителя.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "drakkar", Name = "Drakkar", NameRu = "Драккар",
            DeckCount = 3, Range = RangeClass.CloseMelee, Cost = 28,
            DeckHpOverrides = new() { 4, 4, 4 }, Speed = 1, Space = 1, Regions = new() { Region.North },
            Abilities = new() { "freeze_nearby", "burn_resist" },
            Description = "Замораживает вражеские призывы в своей зоне, не горит и не имеет дальнобойного оружия.",
            DefaultWeapons = new() // CloseMelee = no ranged weapons
        },
        new ShipDefinition
        {
            Id = "alchi_iceberg", Name = "Alchi-Iceberg", NameRu = "Алхи-Айсберг",
            DeckCount = 1, Range = RangeClass.Mid, Cost = 25,
            DeckHpOverrides = new() { 6 }, Speed = 0, Space = 1, IsHome = true, Regions = new() { Region.North, Region.East },
            Abilities = new() { "burn_resist", "poison_cone" },
            Description = "Неподвижный огнеупорный корабль с ядовитым конусом перед носом.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "nimble_single", Name = "Nimble Single", NameRu = "Юркая единичка",
            DeckCount = 1, Range = RangeClass.Close, Cost = 16,
            DefaultArmor = 1, Speed = 3, Space = 1, Regions = new() { Region.West },
            Abilities = new() { "ballista_immune" },
            Description = "Однопалубный корабль, не получающий урон от обычных выстрелов Баллисты.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "alchi_barge", Name = "Alchi-Barge", NameRu = "Алхи-Баржа",
            DeckCount = 1, Range = RangeClass.Close, Cost = 15,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { Region.East },
            Abilities = new() { "poison_cone" },
            Description = "Создаёт перед носом ядовитый конус, уничтожающий корабли и призывы в зоне.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "light_wood_triple", Name = "Light Wood Triple", NameRu = "Тройка из лёгкого дерева",
            DeckCount = 3, Range = RangeClass.Mid, Cost = 14,
            DeckHpOverrides = new() { 1, 1, 1 }, Speed = 2, Space = 1, Regions = new() { Region.West },
            Abilities = new() { "auto_dodge_bow_stern" },
            Description = "Автоматически уклоняется от выстрелов в нос или корму, если позади есть место.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "famous_assembling_ship", Name = "Знаменитый собирающийся корабль", NameRu = "Знаменитый собирающийся корабль",
            DeckCount = 3, Range = RangeClass.Mid, Cost = 20,
            DeckHpOverrides = new() { 1, 1, 1 }, Speed = 1, Space = 1,
            Regions = new() { Region.Tetracor }, Factions = new() { Faction.Alliance },
            Description = "Начинает бой тремя отдельными палубами и собирается в новый трёхпалубный корабль после гибели двух из них.",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Ballista, DeckIndex = 0 },
                new WeaponTemplate { Type = WeaponType.Ballista, DeckIndex = 1 },
                new WeaponTemplate { Type = WeaponType.Ballista, DeckIndex = 2 },
            }
        },
        new ShipDefinition
        {
            Id = "toros", Name = "Toros", NameRu = "Торос",
            DeckCount = 1, Range = RangeClass.Mid, Cost = 12,
            DeckHpOverrides = new() { 6 }, Speed = 0, Space = 1, IsHome = true, Regions = new() { Region.North },
            Abilities = new() { "burn_resist" },
            Description = "Неподвижный однопалубный корабль с прочностью 6 и огнеупорностью.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "cursed_pirate", Name = "Cursed Pirate", NameRu = "Проклятый пират",
            DeckCount = 1, Range = RangeClass.Close, Cost = 6,
            DefaultArmor = 2, Speed = 1, Space = 1, Regions = new() { Region.South },
            Abilities = new() { "spawn_cursed_boat" },
            Description = "После гибели позволяет выпустить Проклятую лодку из колонки своей гибели.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "incendiary_barge", Name = "Incendiary Barge", NameRu = "Горючая баржа",
            DeckCount = 2, Range = RangeClass.Far, Cost = 10,
            DeckHpOverrides = new() { 1, 1 }, Speed = 1, Space = 1, ExplosionRadius = 2, Regions = new() { Region.East },
            Factions = new() { Faction.Empire, Faction.Alliance },
            Abilities = new() { "explode_on_hit" },
            Description = "Взрывается при любом уроне, поражая зону радиусом 2; вооружена Горючкой.",
            DefaultWeapons = new()
            {
                new WeaponTemplate { Type = WeaponType.Incendiary, Ammo = 2, DeckIndex = 0, AimSpeed = 0 } // Горючка
            },
            AvailableUpgrades = new()
            {
                new UpgradeDefinition { Id = "barge_evil_incendiary", Name = "Evil Incendiary", NameRu = "Злая горючка", Cost = 2, Description = "Заменяет Горючку на Злую горючку, уничтожающую остаток корабля при попадании в уже разрушенную палубу.", Effect = "evil_incendiary" },
            }
        },
        new ShipDefinition
        {
            Id = "maneuvering_double", Name = "Maneuvering Double", NameRu = "Маневрирующая двойка",
            DeckCount = 2, Range = RangeClass.Mid, Cost = 5,
            DeckHpOverrides = new() { 1, 1 }, Speed = 2, Space = 1, Regions = new() { Region.West },
            Factions = new() { Faction.Empire, Faction.Alliance },
            Abilities = new() { "manual_move_after_hit" },
            Description = "После потери палубы один раз может вручную сместиться на 1–2 клетки.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "famous_ramming_ship", Name = "Знаменитый Врезающийся корабль", NameRu = "Знаменитый Врезающийся корабль",
            DeckCount = 2, Range = RangeClass.Mid, Cost = 5,
            DeckHpOverrides = new() { 1, 1 }, Speed = 2, Space = 1, Regions = new() { Region.West },
            Factions = new() { Faction.Alliance },
            Abilities = new() { "manual_move_after_hit", "ramming_maneuver" },
            Description = "После потери палубы один раз маневрирует на 1–2 клетки, игнорируя Space союзников и уничтожая перекрытую союзную палубу.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
        },
        new ShipDefinition
        {
            Id = "pirates", Name = "Pirates", NameRu = "Пираты",
            DeckCount = 2, Range = RangeClass.Mid, Cost = 4,
            DeckHpOverrides = new() { 2, 2 }, Space = 1, Regions = new() { Region.South },
            Factions = new() { Faction.Empire, Faction.Alliance },
            Abilities = new() { "spawn_pirate_boat" },
            Description = "После обычной гибели позволяет выпустить Пиратскую лодку из колонки своей гибели.",
            DefaultWeapons = new() { new WeaponTemplate { Type = WeaponType.Ballista } }
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
                MaxAmmo = wt.Ammo,
                AimSpeed = wt.AimSpeed,
                ShipId = ship.Id,
                DeckIndex = wt.DeckIndex,
            });

            if (wt.DeckIndex >= 0 && wt.DeckIndex < ship.Decks.Count && ship.Decks[wt.DeckIndex].Module == null)
                ship.Decks[wt.DeckIndex].Module = WeaponModuleName(wt.Type);
        }

        // Every Mid deck carries its own Ballista, including the special decks of both
        // four-deck flagships. A special module remains the deck's primary visual module.
        if (def.Range == RangeClass.Mid)
        {
            for (var deckIndex = 0; deckIndex < ship.Decks.Count; deckIndex++)
            {
                if (ship.Weapons.Any(w => w.Type == WeaponType.Ballista && w.DeckIndex == deckIndex)) continue;
                ship.Weapons.Add(new Weapon
                {
                    Type = WeaponType.Ballista,
                    MaxAmmo = -1,
                    ShipId = ship.Id,
                    DeckIndex = deckIndex,
                });
                if (ship.Decks[deckIndex].Module == null)
                    ship.Decks[deckIndex].Module = "ballista";
            }
        }

        // Assign the four special deck modules on both faction flagships.
        if (def.Id is "tetranavis" or "famous_diagonal_ship")
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

    /// <summary>
    /// Expand one assembling-ship fleet slot into three independently placed one-deck ships.
    /// Cost stays on the first part so unit-count and first-turn accounting see one purchase.
    /// </summary>
    public static List<Ship> CreateAssemblyComponents(
        ShipDefinition def,
        List<string> upgradeIds = null)
    {
        var groupId = Guid.NewGuid().ToString("N")[..8];
        var components = new List<Ship>();
        for (var componentIndex = 0; componentIndex < 3; componentIndex++)
        {
            var ship = new Ship
            {
                DefinitionId = def.Id,
                Name = $"Собирающаяся палуба {componentIndex + 1}",
                Range = def.Range,
                Cost = componentIndex == 0 ? def.Cost : 0,
                Space = def.Space,
                ExplosionRadius = def.ExplosionRadius,
                Speed = def.Speed,
                Regions = new List<Region>(def.Regions),
                Abilities = new List<string>(def.Abilities),
                IsHome = def.IsHome,
                AssemblyGroupId = groupId,
                AssemblyComponentIndex = componentIndex,
                IsAssemblyComponent = true,
                Upgrades = componentIndex == 0 && upgradeIds != null
                    ? new List<string>(upgradeIds)
                    : new List<string>(),
            };
            ship.Abilities.Add("assembly_component");
            ship.Decks.Add(new Deck
            {
                Index = 0,
                MaxHp = 1,
                CurrentHp = 1,
                Module = "ballista",
            });
            ship.Weapons.Add(new Weapon
            {
                Type = WeaponType.Ballista,
                MaxAmmo = -1,
                ShipId = ship.Id,
                DeckIndex = 0,
            });
            components.Add(ship);
        }

        return components;
    }

    /// <summary>Create the fresh intact hull placed after two assembly parts are destroyed.</summary>
    public static Ship CreateAssembledShip(ShipDefinition def)
    {
        var ship = CreateShip(def);
        ship.Name = "Знаменитый собирающийся корабль";
        ship.AssemblyGroupId = null;
        ship.AssemblyComponentIndex = -1;
        ship.IsAssemblyComponent = false;
        ship.Abilities.Remove("assembly_component");
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

            case "greek_fire":
                ship.Abilities.Add("greek_fire_weapon");
                // Add one-shot GreekFire weapon so the player can select & fire it
                ship.Weapons.Add(new Weapon
                {
                    Type = WeaponType.GreekFire,
                    Ammo = 1,
                    MaxAmmo = 1,
                    ShipId = ship.Id,
                    DeckIndex = 0,
                });
                break;

            case "evil_greek_fire":
                ship.Abilities.Add("evil_greek_fire_weapon");
                ship.Weapons.Add(new Weapon
                {
                    Type = WeaponType.EvilGreekFire,
                    Ammo = 1,
                    MaxAmmo = 1,
                    ShipId = ship.Id,
                    DeckIndex = 0,
                });
                break;

            case "evil_incendiary":
                ship.Abilities.Add("evil_incendiary_weapon");
                ship.Weapons.RemoveAll(weapon => weapon.Type == WeaponType.Incendiary);
                ship.Weapons.Add(new Weapon
                {
                    Type = WeaponType.EvilIncendiary,
                    Ammo = 2,
                    MaxAmmo = 2,
                    AimSpeed = 0,
                    ShipId = ship.Id,
                    DeckIndex = 0,
                });
                break;

            case "brander":
                ship.Abilities.Add("brander_summon");
                break;
        }
    }

    private static string WeaponModuleName(WeaponType type)
    {
        return type switch
        {
            WeaponType.Ballista => "ballista",
            WeaponType.Tetracatapult => "tetracatapult",
            WeaponType.Mast => "mast",
            WeaponType.Boiler => "boiler",
            WeaponType.Incendiary => "incendiary",
            WeaponType.GreekFire => "boiler",
            WeaponType.EvilIncendiary => "incendiary",
            WeaponType.EvilGreekFire => "boiler",
            _ => null,
        };
    }
}
