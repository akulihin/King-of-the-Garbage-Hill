using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class DoomGuy
{
    public const string CharacterName = "DooM Guy";
    public const string Rune = "Rune";
    public const string Shield = "Shield";
    public const string Mission = "Mission";
    public const string Gun = "Gun";

    public const string Ascension = "Вознесение";
    public const string Maneuvers = "Маневры";
    public const string Extermination = "Истребление";
    public const string GloryKill = "Glory kill";
    public const string SawShield = "Щит-пила";
    public const string ShockShield = "Шоковый щит";
    public const string HellBlock = "Адский блок";
    public const string CounterAttack = "Контр-атака";
    public const string SharkShield = "Щит-акула";
    public const string DemonNests = "Адеские гнезда";
    public const string MakeAMess = "Навести беспорядок";
    public const string InfernalEnergy = "Инфернальная энергия";
    public const string BecomeGod = "Стань богом";
    public const string Melee = "Ближник";
    public const string Bfg = "BFG";
    public const string Fists = "Кулаки";
    public const string Chainsaw = "Бензопила";
    public const string Railgun = "Рельса";
    public const string TameDragon = "Приручить дракона";
    public const string DragonPassive = "Дракон";
    public const string SharkPassive = "Ничего не понимает";
    public const decimal InfernalEnergyStealRate = 0.20m;
    public const int InfernalEnergySourceLimit = 3;

    public static readonly string[] StageOrder = { Rune, Shield, Mission, Gun };

    public sealed record ModuleDefinition(
        string Name,
        string Stage,
        string Description,
        bool Reward,
        bool SpecialUnlock = false,
        bool MeleeModule = false);

    public static readonly IReadOnlyList<ModuleDefinition> Modules = new List<ModuleDefinition>
    {
        new(Ascension, Rune, "+8 Интеллекта. Придется хитрить: каждое поражение отнимает по одному.", false),
        new(Maneuvers, Rune, "+5 Скорости. Уклоняйся от близких стычек: получаемый Вред отнимает по одному.", false),
        new(Extermination, Rune, "Победи каждого из пятерых врагов хотя бы раз и получишь + все статы и бонусные очки, равные количеству оставшихся ходов до завершения.", true),
        new(GloryKill, Rune, "Использует двойной *Скилл* против ближайших по таблице врагов. Такие победы приносят +1 каждого стата.", true, MeleeModule: true),

        new(SawShield, Shield, "Враги, атакующие в блок, теряют втрое больше очков.", false),
        new(ShockShield, Shield, "Враг, ударивший в блок, пропустит свой следующий ход. Одноразово.", false),
        new(HellBlock, Shield, "Заблокируй две атаки одновременно, получишь 666 Скилла. Это одноразовая акция.", true),
        new(CounterAttack, Shield, "Враги, ударившие в блок, становятся уязвимы на один ход. Лишаются справедливости и *Скилла*.", true),
        new(SharkShield, Shield, "Ничего не понимает...", true),

        new(DemonNests, Mission, "На таблице каждый ход появляются гнезда демонов. +1 очко за уничтожение гнезда, но если гнезд стало больше трех — −20 очков.", false),
        new(MakeAMess, Mission, "Каждая битва дополнительно приносит очко, независимо от исхода.", false),
        new(InfernalEnergy, Mission, "Найдите 3 уникальных источника демонической энергии (кто заработал аномально много очков со способностей, а не с побед). Уничтожение **похищает** 20% заработанных способностями очков.", true),
        new(BecomeGod, Mission, "Не используя блок, продержись до конца без единого проигрыша. Награда: 20 бонусных очков.", true),
        new(Melee, Mission, "Удваивает бонуcы от melee модулей против ближайших по таблице врагов.", true),

        new(Bfg, Gun, "Одноразовая BFG гарантирует победу следующей атаки на этапе рандома и волной продолжает бой по соседним целям, пока хватает мощи.", false),
        new(Fists, Gun, "Теряет всю силу. Победы приносят +2 очка за свэг.", false, MeleeModule: true),
        new(Chainsaw, Gun, "Следующая победа позволяет распилить врага и выбрать одну из четырех его пассивок. Пила после этого выбрасывается.", true, MeleeModule: true),
        new(Railgun, Gun, "Сделедующая атака начнет бой сразу со всеми врагами в стороне той стороне таблицы, где находится цель относительно вас.", true),
        new(TameDragon, Gun, "На 10м ходу всё бросает и залазит на дракона.", true, SpecialUnlock: true),
    };

    public static string StageForRound(int roundNo) => roundNo switch
    {
        3 => Rune,
        5 => Shield,
        7 => Mission,
        9 => Gun,
        _ => "",
    };

    public static ModuleDefinition FindModule(string name) => Modules.FirstOrDefault(x => x.Name == name);

    public static IEnumerable<ModuleDefinition> GetStandardRewardModules(string stage) =>
        Modules.Where(x => x.Stage == stage && x.Reward && !x.SpecialUnlock);

    public static bool IsNearestEnemy(GameClass game, GamePlayerBridgeClass player, GamePlayerBridgeClass enemy)
    {
        if (game == null || player == null || enemy == null || enemy.Passives.IsDead
            || player.IsTeamMember(game, enemy.GetPlayerId())) return false;
        return Math.Abs(player.Status.GetPlaceAtLeaderBoard() - enemy.Status.GetPlaceAtLeaderBoard()) == 1;
    }

    public static bool HasMeleeBonus(GamePlayerBridgeClass player) =>
        player?.Passives?.DoomGuy?.GetActive(Mission) == Melee;

    public static void PrepareSharkShield(GamePlayerBridgeClass player)
    {
        if (player?.GameCharacter?.Name != CharacterName
            || player.Passives.PassiveAbilitiesDisabledByKimiko)
            return;
        var state = player.Passives.DoomGuy;
        if (state.GetActive(Shield) != SharkShield || !player.Status.IsBlock) return;

        player.Status.IsBlock = false;
        player.Status.IsSkip = false;
        player.Status.WhoToAttackThisTurn.Clear();
        state.SharkShieldActiveThisRound = true;
        state.SharkShieldAddedPassive = player.GameCharacter.Passive.All(x => x.PassiveName != SharkPassive);
        if (state.SharkShieldAddedPassive)
            player.GameCharacter.Passive.Add(new Passive(SharkPassive, "", false));
        player.Status.AddInGamePersonalLogs("Щит-акула: Ничего не понимает...\n");
    }

    public static void ApplyFightModules(GamePlayerBridgeClass attacker, GamePlayerBridgeClass defender, GameClass game)
    {
        ApplyFightModulesForOwner(attacker, defender, game);
        ApplyFightModulesForOwner(defender, attacker, game);
    }

    private static void ApplyFightModulesForOwner(
        GamePlayerBridgeClass doom,
        GamePlayerBridgeClass enemy,
        GameClass game)
    {
        if (doom?.GameCharacter?.Name != CharacterName || enemy == null
                                                   || doom.Passives.PassiveAbilitiesDisabledByKimiko)
            return;
        var state = doom.Passives.DoomGuy;

        if (state.GetActive(Rune) == GloryKill && IsNearestEnemy(game, doom, enemy))
        {
            var multiplier = HasMeleeBonus(doom) ? 3 : 2;
            doom.FightCharacter.SetSkillForOneFight(
                doom.FightCharacter.GetSkill() * multiplier, GloryKill);
        }

        if (state.GetActive(Shield) == CounterAttack
            && !UnknownBug.Is(enemy)
            && state.CounterAttackMarks.GetValueOrDefault(enemy.GetPlayerId()) == game.RoundNo)
        {
            enemy.FightCharacter.SetSkillForOneFight(0, CounterAttack);
            enemy.FightCharacter.Justice.SetJusticeForOneFight(0, CounterAttack);
        }
    }

    public static void EnsureFortress(DoomFortressData fortress)
    {
        fortress.UnlockedModules ??= new List<string>();
        fortress.EquippedSlots ??= new Dictionary<string, List<string>>();

        foreach (var stage in StageOrder)
        {
            var starters = Modules.Where(x => x.Stage == stage && !x.Reward).Select(x => x.Name).ToList();
            foreach (var starter in starters)
                if (!fortress.UnlockedModules.Contains(starter))
                    fortress.UnlockedModules.Add(starter);

            if (!fortress.EquippedSlots.TryGetValue(stage, out var slots) || slots == null)
            {
                slots = new List<string>();
                fortress.EquippedSlots[stage] = slots;
            }

            while (slots.Count < 4) slots.Add("");
            if (slots.Count > 4) slots.RemoveRange(4, slots.Count - 4);

            foreach (var starter in starters)
            {
                if (slots.Contains(starter)) continue;
                var empty = slots.FindIndex(string.IsNullOrEmpty);
                if (empty >= 0) slots[empty] = starter;
            }

            for (var i = 0; i < slots.Count; i++)
                if (!string.IsNullOrEmpty(slots[i]) && !fortress.UnlockedModules.Contains(slots[i]))
                    slots[i] = "";
        }
    }

    public static void InitializeForGame(GamePlayerBridgeClass player, DiscordAccountClass account)
    {
        if (player?.GameCharacter?.Name != CharacterName) return;
        account.DoomFortress ??= new DoomFortressData();
        EnsureFortress(account.DoomFortress);
        player.Passives.DoomGuy.LoadoutSlots = account.DoomFortress.EquippedSlots
            .ToDictionary(x => x.Key, x => x.Value.Where(v => !string.IsNullOrEmpty(v)).ToList());
    }

    public static List<ModuleDefinition> GetOptions(DoomGuyState state, string stage)
    {
        if (state == null || string.IsNullOrEmpty(stage)) return new List<ModuleDefinition>();
        var configured = state.LoadoutSlots.TryGetValue(stage, out var slots)
            ? slots.Select(FindModule).Where(x => x != null).ToList()
            : new List<ModuleDefinition>();

        // A stale/pre-Fortress account snapshot must not strand a human behind the mandatory
        // level-up gate. Every valid Fortress contains both starter modules for each stage, so
        // this is the same safe baseline EnsureFortress would have supplied on re-authentication.
        return configured.Count > 0
            ? configured
            : Modules.Where(x => x.Stage == stage && !x.Reward).ToList();
    }

    public static bool ActivateRollMode(GamePlayerBridgeClass player)
    {
        if (player?.GameCharacter?.Name != CharacterName || player.Passives.DoomGuy.RollMode) return false;
        var state = player.Passives.DoomGuy;
        state.RollMode = true;
        player.GameCharacter.DoomRollMode = true;
        player.GameCharacter.SetMoral(0, "Let's Roll!", false);
        player.GameCharacter.ResetMoralBonus();
        player.Predict.Clear();
        player.Status.ConfirmedPredict = true;
        player.Status.AddInGamePersonalLogs("Let's Roll!: предположения и Мораль отключены. Модули будут выбраны случайно.\n");
        return true;
    }

    public static bool ApplySelectedModule(GamePlayerBridgeClass player, GameClass game, string moduleName, bool randomPick)
    {
        var module = FindModule(moduleName);
        if (module == null || player?.GameCharacter?.Name != CharacterName) return false;
        var state = player.Passives.DoomGuy;
        if (state.ActiveModules.ContainsKey(module.Stage)) return false;
        if (!GetOptions(state, module.Stage).Any(x => x.Name == moduleName)) return false;

        state.ActiveModules[module.Stage] = moduleName;
        var passive = player.GameCharacter.Passive.Find(x => x.PassiveName == module.Stage);
        if (passive != null)
        {
            passive.Visible = true;
            passive.PassiveDescription = $"**{moduleName}**\n{module.Description}";
        }

        switch (moduleName)
        {
            case Ascension:
                player.GameCharacter.AddIntelligence(8, Ascension);
                state.AscensionIntelligenceRemaining = 8;
                break;
            case Maneuvers:
                player.GameCharacter.AddSpeed(5, Maneuvers);
                state.ManeuversSpeedRemaining = 5;
                break;
            case DemonNests:
                SpawnDemonNest(player, game);
                break;
            case InfernalEnergy:
                RefreshInfernalEnergySources(player, game);
                break;
            case Bfg:
                state.BfgCharged = true;
                break;
            case Railgun:
                state.RailgunCharged = true;
                break;
            case Fists:
                player.GameCharacter.SetStrength(0, Fists);
                break;
            case TameDragon:
                if (player.GameCharacter.Passive.All(x => x.PassiveName != DragonPassive))
                    player.GameCharacter.Passive.Add(new Passive(DragonPassive, "", false));
                player.Passives.GeraltMonsterType = Geralt.MonsterType.Драконы;
                break;
        }

        player.Status.AddInGamePersonalLogs($"{module.Stage}: выбран модуль **{moduleName}**.\n");
        game?.Phrases.DoomGuyModule.SendLog(player, false);
        if (randomPick)
            player.Status.AddRegularPoints(2, "Let's Roll!");
        return true;
    }

    public static bool ApplyRandomModule(GamePlayerBridgeClass player, GameClass game, SecureRandom random)
    {
        var stage = StageForRound(game.RoundNo);
        var options = GetOptions(player.Passives.DoomGuy, stage);
        if (options.Count == 0) return false;
        return ApplySelectedModule(player, game, options[random.Random(0, options.Count - 1)].Name, true);
    }

    public static void SpawnDemonNest(GamePlayerBridgeClass player, GameClass game)
    {
        if (game == null || player?.Passives?.DoomGuy == null) return;
        var state = player.Passives.DoomGuy;
        if (state.GetActive(Mission) != DemonNests) return;

        var candidates = game.PlayersList.Where(x => x.GetPlayerId() != player.GetPlayerId()
                                                     && !x.Passives.IsDead
                                                     && !state.DemonNests.Contains(x.GetPlayerId())).ToList();
        // Most wanted: гнездо всегда появляется на Рике, пока на нём нет активного
        var rickMw = RickSanchez.FindMostWantedHolder(candidates);
        if (rickMw != null)
            state.DemonNests.Add(rickMw.GetPlayerId());
        else if (candidates.Count > 0)
            state.DemonNests.Add(candidates[SecureRandom.Next(0, candidates.Count - 1)].GetPlayerId());

        if (state.DemonNests.Count > 3)
        {
            player.Status.AddBonusPoints(-20, DemonNests);
            player.Status.AddInGamePersonalLogs("Адеские гнезда: демоны вырвались наружу! −20 очков.\n");
            state.DemonNests.Clear();
        }
    }

    public static void RefreshInfernalEnergySources(GameClass game)
    {
        if (game == null) return;

        foreach (var player in game.PlayersList.Where(candidate =>
                     candidate.GameCharacter.Name == CharacterName
                     && candidate.GameCharacter.Passive.Any(passive => passive.PassiveName == Mission)
                     && !candidate.Passives.IsDead))
            RefreshInfernalEnergySources(player, game);
    }

    private static void RefreshInfernalEnergySources(
        GamePlayerBridgeClass player,
        GameClass game)
    {
        var state = player?.Passives?.DoomGuy;
        if (state == null || game == null) return;

        state.InfernalEnergySourcesThisRound.Clear();
        if (state.GetActive(Mission) != InfernalEnergy) return;

        var sources = game.PlayersList
            .Where(enemy => enemy.GetPlayerId() != player.GetPlayerId()
                            && !enemy.Passives.IsDead
                            && !player.IsTeamMember(game, enemy.GetPlayerId()))
            .Select(enemy => new
            {
                Player = enemy,
                AbilityPoints = enemy.Status.GetLifetimeAbilityPoints(game),
            })
            .Where(source => source.AbilityPoints > 0)
            .OrderByDescending(source => source.AbilityPoints)
            .ThenBy(source => source.Player.Status.GetPlaceAtLeaderBoard())
            .Take(InfernalEnergySourceLimit)
            .Select(source => source.Player.GetPlayerId());

        state.InfernalEnergySourcesThisRound.AddRange(sources);
    }

    public static bool TryStealInfernalEnergy(
        GamePlayerBridgeClass player,
        GameClass game,
        Guid defeatedPlayerId)
    {
        var state = player?.Passives?.DoomGuy;
        if (state == null
            || game == null
            || state.GetActive(Mission) != InfernalEnergy
            || state.InfernalEnergyVictims.Count >= InfernalEnergySourceLimit
            || !state.InfernalEnergySourcesThisRound.Contains(defeatedPlayerId)
            || state.InfernalEnergyVictims.Contains(defeatedPlayerId))
            return false;

        var victim = game.PlayersList.Find(candidate =>
            candidate.GetPlayerId() == defeatedPlayerId);
        if (victim == null
            || victim.Passives.IsDead
            || UnknownBug.Is(victim)
            || !Homelander.CanTransferFrom(victim, InfernalEnergy))
            return false;

        var abilityPoints = victim.Status.GetLifetimeAbilityPoints(game);
        if (abilityPoints <= 0) return false;

        ScamRat.TransferExactBonusPoints(
            victim,
            player,
            abilityPoints * InfernalEnergyStealRate,
            InfernalEnergy,
            FeedbackSourceVisibility.ProNeutralTarget);
        state.InfernalEnergyVictims.Add(defeatedPlayerId);
        return true;
    }

    public static CopiedPassiveResult CopyChainsawPassive(GamePlayerBridgeClass player, string passiveName)
    {
        var state = player?.Passives?.DoomGuy;
        if (state == null || state.ChainsawChoices.Count == 0) return new CopiedPassiveResult(false, "Нет доступного выбора.");
        var chosen = state.ChainsawChoices.Find(x => x.PassiveName == passiveName);
        if (chosen == null) return new CopiedPassiveResult(false, "Эта пассивка не входит в выбор.");

        if (state.CopiedPassiveNames.Count == 0)
            player.GameCharacter.Passive.RemoveAll(x => x.PassiveName == Gun);
        player.GameCharacter.Passive.Add(chosen.DeepCopy());
        state.CopiedPassiveNames.Add(chosen.PassiveName);
        state.CopiedPassiveName = string.Join(" + ", state.CopiedPassiveNames);
        state.ActiveModules[Gun] = state.CopiedPassiveName;
        state.ChainsawChoices.Remove(chosen);
        state.ChainsawSelectionsRemaining = Math.Max(0, state.ChainsawSelectionsRemaining - 1);
        if (state.ChainsawSelectionsRemaining == 0)
            state.ChainsawChoices.Clear();

        if (chosen.PassiveName == "Портальная пушка")
        {
            player.Passives.RickPortalGun.Invented = true;
            player.Passives.RickPortalGun.Charges = Math.Max(1, player.Passives.RickPortalGun.Charges);
        }
        if (chosen.PassiveName == "Шэн")
            player.Passives.SalldorumShen.Charges = Math.Max(1, player.Passives.SalldorumShen.Charges);
        if (chosen.PassiveName == "Изанаги")
            player.Passives.ItachiIzanagi.UsesRemaining = 1;
        if (chosen.PassiveName == "Глаза Итачи")
            player.Passives.ItachiTsukuyomi.ChargeCounter = Math.Max(1, player.Passives.ItachiTsukuyomi.ChargeCounter);

        player.Status.AddInGamePersonalLogs($"Бензопила: получена пассивка **{chosen.PassiveName}**.\n");
        return new CopiedPassiveResult(true, "");
    }

    public static double RewardChance(int totalRewardModules, int remainingRewardModules)
    {
        if (remainingRewardModules <= 0 || totalRewardModules <= 0) return 0;
        if (remainingRewardModules == 1 || totalRewardModules == 1) return 5;
        return Math.Round(5 + 75d * (remainingRewardModules - 1) / (totalRewardModules - 1), 2);
    }

    public static ModuleRewardResult TryAwardModule(DiscordAccountClass account, int place)
    {
        if (place is < 1 or > 4) return new ModuleRewardResult("", "", 0, false);
        account.DoomFortress ??= new DoomFortressData();
        EnsureFortress(account.DoomFortress);

        var highestStageIndex = 4 - place;
        for (var stageIndex = highestStageIndex; stageIndex >= 0; stageIndex--)
        {
            var stage = StageOrder[stageIndex];
            var rewards = GetStandardRewardModules(stage).ToList();
            var missing = rewards.Where(x => !account.DoomFortress.UnlockedModules.Contains(x.Name)).ToList();
            if (missing.Count == 0) continue;

            var chance = RewardChance(rewards.Count, missing.Count);
            if (SecureRandom.Next(1, 100) > chance)
                return new ModuleRewardResult(stage, "", chance, false);

            var module = missing[SecureRandom.Next(0, missing.Count - 1)];
            account.DoomFortress.UnlockedModules.Add(module.Name);
            var slots = account.DoomFortress.EquippedSlots[stage];
            var empty = slots.FindIndex(string.IsNullOrEmpty);
            if (empty >= 0) slots[empty] = module.Name;
            return new ModuleRewardResult(stage, module.Name, chance, true);
        }

        return new ModuleRewardResult("", "", 0, false);
    }

    public static ModuleRewardResult TryAwardDragonTaming(DiscordAccountClass account, DoomGuyState state)
    {
        if (account == null || state?.DefeatedDragonSirinoks != true)
            return new ModuleRewardResult(Gun, "", 0, false);

        account.DoomFortress ??= new DoomFortressData();
        EnsureFortress(account.DoomFortress);
        if (account.DoomFortress.UnlockedModules.Contains(TameDragon))
            return new ModuleRewardResult(Gun, "", 0, false);

        account.DoomFortress.UnlockedModules.Add(TameDragon);
        var slots = account.DoomFortress.EquippedSlots[Gun];
        var empty = slots.FindIndex(string.IsNullOrEmpty);
        if (empty >= 0) slots[empty] = TameDragon;
        return new ModuleRewardResult(Gun, TameDragon, 100, true);
    }

    public sealed record CopiedPassiveResult(bool Success, string Error);
    public sealed record ModuleRewardResult(string Stage, string ModuleName, double Chance, bool Awarded);
}

public class DoomFortressData
{
    public List<string> UnlockedModules { get; set; } = new();
    public Dictionary<string, List<string>> EquippedSlots { get; set; } = new();
}

public class DoomGuyState
{
    public Dictionary<string, List<string>> LoadoutSlots { get; set; } = new();
    public Dictionary<string, string> ActiveModules { get; set; } = new();
    public List<Guid> DemonNests { get; set; } = new();
    public List<Guid> InfernalEnergySourcesThisRound { get; set; } = new();
    public List<Guid> InfernalEnergyVictims { get; set; } = new();
    public List<Guid> ExterminationVictories { get; set; } = new();
    public List<Passive> ChainsawChoices { get; set; } = new();
    public bool RollMode { get; set; }
    public bool BfgCharged { get; set; }
    public bool RailgunCharged { get; set; }
    public int AscensionIntelligenceRemaining { get; set; }
    public int ManeuversSpeedRemaining { get; set; }
    public bool ShockShieldUsed { get; set; }
    public Guid ShockSkipTarget { get; set; } = Guid.Empty;
    public int ShockSkipRound { get; set; }
    public bool HellBlockUsed { get; set; }
    public int BlocksThisRound { get; set; }
    public bool EverBlocked { get; set; }
    public bool EverLost { get; set; }
    public bool BecomeGodAwarded { get; set; }
    public bool ExterminationAwarded { get; set; }
    public bool ChainsawSpent { get; set; }
    public int ChainsawSelectionsRemaining { get; set; }
    public string CopiedPassiveName { get; set; } = "";
    public List<string> CopiedPassiveNames { get; set; } = new();
    public Dictionary<Guid, int> CounterAttackMarks { get; set; } = new();
    public bool SharkShieldActiveThisRound { get; set; }
    public bool SharkShieldAddedPassive { get; set; }
    public bool DefeatedDragonSirinoks { get; set; }

    public string GetActive(string stage) => ActiveModules.GetValueOrDefault(stage, "");
}
