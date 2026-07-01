using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class TheBoys
{
    // Текст прокачки, добавляемый в описание члена команды после ПЕРВОЙ прокачки (см. the_boys.txt).
    public const string FrancieUpgradeLine =
        "\n\n✦ **Хим.оружие**: Француз улучшает своё хим.оружие. При победе каждое улучшение хим.оружия приносит __бонусное очко__ в зависимости от сложности врага.";
    public const string ButcherUpgradeLine =
        "\n\n✦ **Кочерга**: При прокачке получает кочергу. Каждая кочерга умножает Скилл в бою и **Вред** при победе.";
    public const string KimikoUpgradeLine =
        "\n\n✦ **Регенерация**: При прокачке повышает волю к жизни и регенерацию: игнорирует всё больше вражеской справедливости при обороне.";
    public const string MMUpgradeLine =
        "\n\n✦ **Компромат**: Следующая атака после прокачки позволяет M.M. добыть компромат на цель. Если на 8м ходу весь компромат сработал, М.М. успокаивается и получает +5 **Морали** за каждый. В конце игры очки за верные предположения увеличиваются за каждый собранный компромат.";

    // Имена членов, их ультимейтов и текста прокачки — единая таблица для UI/анлока.
    public const string FrancieName = "Francie";
    public const string ButcherName = "Butcher";
    public const string KimikoName = "Kimiko";
    public const string MMName = "M.M.";
    public const string VirusUltimate = "Смертельный вирус";
    public const string SuperDickUltimate = "СуперМудень";
    public const string LivingWeaponUltimate = "Живое Оружие";
    public const string ShacklesUltimate = "Оковы Правосудия";

    // Супергерои, которых Бучер помечает всегда (см. решение по спеке).
    public static readonly string[] Superheroes = { "Сайтама", "Кратос", "Загадочный Спартанец в маске", "Кира" };

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

        // Ультимейт: СуперМудень (Butcher x4)
        public bool SuperDickActive { get; set; } = false; // отключает Francie/Kimiko/M.M., x2 бонусы, пробитие резистов
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
