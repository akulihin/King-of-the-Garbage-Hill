using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class TheBoys
{
    // Текст прокачки, добавляемый в описание члена команды после ПЕРВОЙ прокачки (см. the_boys.txt).
    public const string FrancieUpgradeLine =
        "\n\n✦ **Хим.оружие**: Француз улучшает своё хим.оружие. При победе каждое улучшение хим.оружия приносит __бонусное очко__ в зависимости от сложности врага.";
    public const string ButcherUpgradeLine =
        "\n\nПри прокачке Бучер получает кочергу.";
    public const string KimikoUpgradeLine =
        "\n\n**Регенирация**\nКимико никогда больше не отключается. Ничто не наносит ей **Вред**.\nОна начинает **похищать** справедливость атакующих врагов, и с каждой прокачкой всё больше.";
    public const string MMUpgradeLine =
        "\n\n✦ **Компромат**: Теперь M.M. занят делом и он __спокоен__! Следующая атака после прокачки позволяет M.M. добыть компромат на цель. Если на 8м ходу весь компромат сработал, М.М. доволен и получает +5 **Морали** за каждый.\nВ конце игры очки за верные предположения увеличиваются за каждый собранный компромат.";

    // Имена членов, их ультимейтов и текста прокачки — единая таблица для UI/анлока.
    public const string FrancieName = "Francie";
    public const string ButcherName = "Butcher";
    public const string KimikoName = "Kimiko";
    public const string MMName = "M.M.";
    public const string VirusUltimate = "Смертельный вирус";
    public const string SuperDickUltimate = "СуперМудень";
    public const string LivingWeaponUltimate = "Живое Оружие";
    public const string ShacklesUltimate = "Оковы Правосудия";
    public const string KillingCoupleCombination = "Убийственная Парочка";
    public const string NoButcherCombination = "Нахер Бучера";
    public const string UnstoppableCombination = "Неудержимые";
    public const string SausagePartyCombination = "Sausage Party";
    public const string TheBoysCombination = "TheBoys";
    public const int VirusPointsPerInfected = 3;
    public const int GovernmentSalaryZbs = 69;
    public const string GovernmentSalarySource = "От самого призедента";

    private static readonly HashSet<string> NonButcherPassiveNames = new(StringComparer.Ordinal)
    {
        FrancieName,
        KimikoName,
        MMName,
        VirusUltimate,
        LivingWeaponUltimate,
        ShacklesUltimate,
        KillingCoupleCombination,
        NoButcherCombination,
        UnstoppableCombination,
        SausagePartyCombination,
        TheBoysCombination,
    };

    // Супергерои, которых Бучер помечает всегда (см. решение по спеке).
    public static readonly string[] Superheroes =
        { "Сайтама", "Кратос", "Загадочный Спартанец в маске", "Кира", Homelander.CharacterName, OmniMan.CharacterName };

    public static bool IsPermanentSup(GamePlayerBridgeClass enemy, int roundNo)
    {
        if (Superheroes.Contains(enemy.GameCharacter.Name)) return true;

        // Молодой Глеб deliberately keeps Name == "Глеб" after transforming; Main Ирелия is his identity marker.
        if (enemy.GameCharacter.Passive.Any(passive => passive.PassiveName == "Main Ирелия")) return true;

        return enemy.GameCharacter.Passive.Any(passive => passive.PassiveName == "Претендент русского сервера")
               && (enemy.Passives.GlebChallengerList.RoundItTriggered == roundNo
                   || enemy.Passives.GlebChallengerTriggeredWhen.WhenToTrigger.Contains(roundNo));
    }

    public static bool ShouldAwardGovernmentSalary(GamePlayerBridgeClass player, bool wonMatch) =>
        player != null
        && wonMatch
        && !player.Passives.IsDead
        && player.GameCharacter.Name == "TheBoys"
        && player.Status.GetPlaceAtLeaderBoard() == 1
        && player.Passives.TheBoysButcher.ActiveCombination == TheBoysCombination;

    public static bool HasActiveCombination(GamePlayerBridgeClass player, string combination) =>
        player != null
        && player.GameCharacter.Name == "TheBoys"
        && player.Passives.TheBoysButcher.ActiveCombination == combination
        && player.GameCharacter.Passive.Any(passive =>
            passive.PassiveName == combination && passive.Visible);

    public static void LockNonButcherPassives(GamePlayerBridgeClass player)
    {
        foreach (var passive in player.GameCharacter.Passive.Where(passive =>
                     NonButcherPassiveNames.Contains(passive.PassiveName)))
            passive.Visible = false;
        player.Passives.TheBoysButcher.ActiveCombination = "";
    }

    public static void DisablePassivesBeforeFights(GameClass game)
    {
        var boys = game.PlayersList.Find(player =>
            player.GameCharacter.Name == "TheBoys"
            && !player.Passives.IsDead
            && player.Passives.TheBoysKimiko.LivingWeapon
            && !player.Passives.TheBoysButcher.SuperDickActive
            && player.GameCharacter.Passive.Any(passive =>
                passive.PassiveName == LivingWeaponUltimate));
        if (boys == null) return;

        foreach (var targetId in boys.Status.WhoToAttackThisTurn.Distinct())
        {
            var target = game.PlayersList.Find(player => player.GetPlayerId() == targetId);
            if (target == null || target.Passives.IsDead
                               || target.GetPlayerId() == boys.GetPlayerId()
                               || UnknownBug.Is(target)
                               || target.Status.IsBlock
                               || target.Status.IsSkip
                               || target.GameCharacter.Passive.Count == 0)
                continue;

            Homelander.RunWithoutProtection(target, () =>
            {
                target.GameCharacter.Passive.Clear();
                target.FightCharacter.Passive.Clear();
            });
            target.Passives.PassiveAbilitiesDisabledByKimiko = true;
            boys.Status.AddInGamePersonalLogs(
                $"Живое Оружие: способности {target.DiscordUsername} отключены до конца игры.\n");
            target.Status.AddInGamePersonalLogs(
                "Живое Оружие: Kimiko лишила вас пассивных способностей до конца игры.\n");
        }
    }

    public static void ApplyKillingCoupleJustice(
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass defender,
        CalculateRounds calculateRounds)
    {
        var boys = HasActiveCombination(attacker, KillingCoupleCombination)
            ? attacker
            : HasActiveCombination(defender, KillingCoupleCombination)
                ? defender
                : null;
        if (boys == null) return;
        if (boys.Passives.TheBoysButcher.SuperDickActive) return;

        var enemy = boys.GetPlayerId() == attacker.GetPlayerId() ? defender : attacker;
        if (UnknownBug.Is(enemy)) return;

        var quality = calculateRounds.CalculateStep1(boys, enemy);
        if (!quality.IsTooGoodEnemy && !quality.IsTooStronkEnemy) return;

        var justiceBefore = enemy.FightCharacter.Justice.GetRealJusticeNow();
        if (justiceBefore <= 0) return;

        enemy.FightCharacter.Justice.SetRealJusticeNow(0, KillingCoupleCombination);
        var stolenJustice = justiceBefore - enemy.FightCharacter.Justice.GetRealJusticeNow();
        if (stolenJustice <= 0) return;

        boys.FightCharacter.Justice.AddRealJusticeNow(stolenJustice);
        boys.Status.AddInGamePersonalLogs(
            $"{KillingCoupleCombination}: похищено {stolenJustice} Справедливости у {enemy.DiscordUsername}.\n");
    }

    public class FrancieClass
    {
        public int ChemWeaponLevel { get; set; } = 0;
        public Guid OrderTarget { get; set; } = Guid.Empty;
        public int OrderRoundsLeft { get; set; } = 0;
        public int OrdersCompleted { get; set; } = 0;
        public int OrdersFailed { get; set; } = 0;
        public List<Guid> OrderHistory { get; set; } = new();
        public List<Guid> RemainingTargets { get; set; } = new();

        // Ультимейт: Смертельный вирус (Francie x4)
        public bool VirusArmed { get; set; } = false; // следующая атака вешает вирус
        public bool VirusUsed { get; set; } = false;  // одноразовая активация уже прожата
    }

    public class ButcherClass
    {
        public int PokerCount { get; set; } = 0;
        public bool FourthUpgradeResolved { get; set; } = false;
        public bool ButcherLeft { get; set; } = false;
        public string ActiveCombination { get; set; } = "";

        // Ультимейт: СуперМудень (Butcher x4)
        public bool SuperDickActive { get; set; } = false; // отключает Francie/Kimiko/M.M., x2 бонусы, пробитие резистов
        public int SuperDickDropsThisTurn { get; set; } = 0; // общий лимит 50 дропов за ход
    }

    public class KimikoClass
    {
        public int RegenLevel { get; set; } = 0;
        public bool DisabledNextRound { get; set; } = false;
        public bool IsDisabled { get; set; } = false;
        public int TotalJusticeBlocked { get; set; } = 0;

        // Ультимейт: Живое Оружие (Kimiko x4)
        public bool LivingWeapon { get; set; } = false; // никогда не отключается + крадёт справедливость атакующих
    }

    public class MMClass
    {
        public bool NextAttackGathersKompromat { get; set; } = false;
        public List<Guid> KompromatTargets { get; set; } = new();
        public Dictionary<Guid, string> KompromatHints { get; set; } = new();

        public int UpgradeLevel { get; set; } = 0; // сколько раз прокачали Психику (для метки "M.M. xN" и анлока x4)

        // База M.M.: учёт боёв за раунд (сбрасывается в конце раунда)
        public int WonThisRound { get; set; } = 0;
        public int LostThisRound { get; set; } = 0;

        // Ультимейт: Оковы Правосудия (M.M. x4)
        public bool IsCalm { get; set; } = false; // M.M. спокоен: не психует и иммунен к потере психики
    }
}
