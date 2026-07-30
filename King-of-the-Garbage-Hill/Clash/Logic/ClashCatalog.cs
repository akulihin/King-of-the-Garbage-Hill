using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Clash.Models;

namespace King_of_the_Garbage_Hill.Clash.Logic;

public static class ClashCatalog
{
    public const int MinWidth = 3;
    public const int MaxWidth = 10;
    public const int MinLength = 3;
    public const int MaxLength = 5;
    public const int DefaultWidth = 5;
    public const int DefaultLength = 5;
    public const int StartingMorale = 1;

    private static ClashPassiveDefinition Passive(string id, string name, string description)
    {
        return new ClashPassiveDefinition { Id = id, Name = name, Description = description };
    }

    private static readonly IReadOnlyList<ClashUnitDefinition> LiveUnits = new List<ClashUnitDefinition>
    {
        new()
        {
            Id = "shield-bearer",
            Name = "Щитарь",
            Faction = "Альянс",
            Attack = 1,
            MaxHp = 5,
            Speed = 1,
            ShieldCharges = 1,
            Tags = new() { "shield", "legion-candidate" },
            Passives = new()
            {
                Passive("shield-bearer-block", "Блок", "Блокирует любую атаку один раз за бой."),
                Passive("shield-bearer-legion", "Легион!", "Полный ряд легионных бойцов получает +2 скорости."),
            },
        },
        new()
        {
            Id = "legionary",
            Name = "Легионер",
            Faction = "Империя",
            Attack = 1,
            MaxHp = 4,
            Speed = 1,
            ShieldCharges = 1,
            Tags = new() { "shield" },
            Passives = new()
            {
                Passive("legionary-block", "Блок", "Блокирует любую атаку один раз за бой."),
            },
        },
        new()
        {
            Id = "archer",
            Name = "Лучник",
            Faction = "Нейтральный",
            Attack = 1,
            MaxHp = 1,
            Speed = 3,
            IsRanged = true,
            Tags = new() { "archer", "bow", "short-weapon" },
            Passives = new()
            {
                Passive("archer-ranged", "Лучник", "Атакует в клэше из любого ряда."),
            },
        },
        new()
        {
            Id = "shield-bow",
            Name = "Щито-лук",
            Faction = "Империя",
            Attack = 2,
            MaxHp = 4,
            Speed = 2,
            IsRanged = true,
            ShieldCharges = 1,
            Tags = new() { "archer", "bow", "shield" },
            Passives = new()
            {
                Passive("shield-bow-ranged", "Лучник", "Атакует в клэше из любого ряда."),
                Passive("shield-bow-block", "Блок", "Блокирует любую атаку один раз за бой."),
            },
        },
        new()
        {
            Id = "dancer",
            Name = "Танцор",
            Faction = "Лес",
            Attack = 1,
            MaxHp = 4,
            Speed = 5,
            DodgeCharges = 4,
            AppliesBleed = true,
            DiesToAoe = true,
            Tags = new() { "blade", "agile" },
            Passives = new()
            {
                Passive("dancer-bleed", "Кровотечение", "Первая точная атака оставляет кровотечение."),
                Passive("dancer-dodge", "Увороты", "Четыре точные атаки наносят не более 1 урона каждая. Любая АОЕ-атака убивает мгновенно."),
            },
        },
        new()
        {
            Id = "nimble-gek",
            Name = "Проворный Гек",
            Faction = "Нейтральный",
            Attack = 1,
            MaxHp = 3,
            Speed = 7,
            DodgeCharges = 3,
            DiesToAoe = true,
            Tags = new() { "gek", "creature", "agile" },
            Passives = new()
            {
                Passive("nimble-gek-dodge", "Увороты", "Три точные атаки наносят не более 1 урона каждая. Любая АОЕ-атака убивает мгновенно."),
            },
        },
        new()
        {
            Id = "mechanical-crossbow-08",
            Name = "Мехакинетический Самострел 0.8",
            Faction = "Империя",
            Attack = 2,
            MaxHp = 3,
            Speed = 2,
            IsRanged = true,
            ReloadClashes = 2,
            Tags = new() { "archer", "crossbow" },
            Passives = new()
            {
                Passive("mechanical-crossbow-08-ranged", "Лучник", "Атакует из любого ряда и перезаряжается два клэша."),
            },
        },
        new()
        {
            Id = "mechanical-bolt-thrower-10",
            Name = "Мехакинетический Стреломёт 1.0",
            Faction = "Империя",
            Attack = 2,
            MaxHp = 2,
            Speed = 5,
            IsRanged = true,
            Tags = new() { "archer", "crossbow", "automatic" },
            Passives = new()
            {
                Passive("mechanical-bolt-thrower-10-ranged", "Лучник", "Атакует в клэше из любого ряда."),
            },
        },
    };

    private static readonly IReadOnlyDictionary<string, ClashUnitDefinition> ById =
        LiveUnits.ToDictionary(unit => unit.Id, StringComparer.Ordinal);

    public static IReadOnlyList<ClashUnitDefinition> All => LiveUnits;

    public static bool TryGet(string definitionId, out ClashUnitDefinition definition)
    {
        if (definitionId != null)
            return ById.TryGetValue(definitionId, out definition);
        definition = null;
        return false;
    }

    public static ClashUnitDefinition Get(string definitionId)
    {
        return TryGet(definitionId, out var definition) ? definition : null;
    }

    public static ClashCatalogDto ToDto()
    {
        return new ClashCatalogDto
        {
            Units = LiveUnits.ToList(),
            MinWidth = MinWidth,
            MaxWidth = MaxWidth,
            MinLength = MinLength,
            MaxLength = MaxLength,
            DefaultWidth = DefaultWidth,
            DefaultLength = DefaultLength,
            StartingMorale = StartingMorale,
        };
    }
}
