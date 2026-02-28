using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Geralt
{
    public enum MonsterType { Утопцы, Волколаки, Вампиры, Драконы }

    public class ContractsClass
    {
        // Contract counts per type
        public int Drowners { get; set; } = 0;      // Утопцы
        public int Werewolves { get; set; } = 0;     // Волколаки
        public int Vampires { get; set; } = 0;       // Вампиры
        public int Dragons { get; set; } = 0;        // Драконы

        // Enemy → MonsterType assignment (4 of 5 enemies)
        public Dictionary<Guid, MonsterType> EnemyTypes { get; set; } = new();

        // Tracking
        public int ContractsFoughtThisRound { get; set; } = 0;
        public int NonContractWinsThisRound { get; set; } = 0;
        public bool PlotvaPhrasedThisRound { get; set; } = false;
        public Dictionary<Guid, int> ContractProcsOnEnemy { get; set; } = new();

        public int GetCount(MonsterType type) => type switch
        {
            MonsterType.Утопцы => Drowners,
            MonsterType.Волколаки => Werewolves,
            MonsterType.Вампиры => Vampires,
            MonsterType.Драконы => Dragons,
            _ => 0
        };

        public void SetCount(MonsterType type, int value)
        {
            switch (type)
            {
                case MonsterType.Утопцы: Drowners = value; break;
                case MonsterType.Волколаки: Werewolves = value; break;
                case MonsterType.Вампиры: Vampires = value; break;
                case MonsterType.Драконы: Dragons = value; break;
            }
        }

        public void AddCount(MonsterType type, int delta)
        {
            SetCount(type, GetCount(type) + delta);
        }
    }

    public class OilClass
    {
        public int DrownersOilTier { get; set; } = 0;    // 0=none, 1=Масло, 2=Улучшенное, 3=Отличное
        public int WerewolvesOilTier { get; set; } = 0;
        public int VampiresOilTier { get; set; } = 0;
        public int DragonsOilTier { get; set; } = 0;
        public bool IsOilApplied { get; set; } = false;

        public int GetTier(MonsterType type) => type switch
        {
            MonsterType.Утопцы => DrownersOilTier,
            MonsterType.Волколаки => WerewolvesOilTier,
            MonsterType.Вампиры => VampiresOilTier,
            MonsterType.Драконы => DragonsOilTier,
            _ => 0
        };

        public void SetTier(MonsterType type, int value)
        {
            switch (type)
            {
                case MonsterType.Утопцы: DrownersOilTier = value; break;
                case MonsterType.Волколаки: WerewolvesOilTier = value; break;
                case MonsterType.Вампиры: VampiresOilTier = value; break;
                case MonsterType.Драконы: DragonsOilTier = value; break;
            }
        }

        public static string GetTierName(int tier) => tier switch
        {
            1 => "Масло",
            2 => "Улучшенное масло",
            3 => "Отличное масло",
            _ => ""
        };
    }

    public class MeditationClass
    {
        public List<Guid> RevealedEnemies { get; set; } = new();
        public bool LambertUsed { get; set; } = false;
        public bool LambertActive { get; set; } = false;
        public decimal LambertSkillLost { get; set; } = 0;
    }

    public class ContractDemandClass
    {
        public int Displeasure { get; set; } = 0;          // 0-11, 11 = death
        public int TotalDemandsMade { get; set; } = 0;
        public int TotalSuccessfulDemands { get; set; } = 0;

        // Current round accumulators (written during fights)
        public int CurrentContractWins { get; set; } = 0;
        public int CurrentContractLosses { get; set; } = 0;
        public int CurrentEnemyTotalStats { get; set; } = 0;
        public int CurrentEnemyPosition { get; set; } = 0;
        public int CurrentGeraltPosition { get; set; } = 0;

        // Previous round snapshot (copied at HandleEndOfRound, read by demand button)
        public int PrevContractWins { get; set; } = 0;
        public int PrevContractLosses { get; set; } = 0;
        public int PrevContractsFought { get; set; } = 0;
        public int PrevEnemyTotalStats { get; set; } = 0;
        public int PrevEnemyPosition { get; set; } = 0;
        public int PrevGeraltPosition { get; set; } = 0;

        // Phase locks
        public bool DemandedThisPhase { get; set; } = false;
        public bool DemandedForNext { get; set; } = false;

        public const int Threshold = 4;

        public int CalculateDemandScore()
        {
            if (PrevContractWins == 0) return -999;
            var score = PrevContractWins * 3
                      - PrevContractLosses * 2
                      + Math.Max(0, 4 - PrevEnemyPosition);
            if (PrevEnemyTotalStats >= 30) score += 2;
            if (PrevEnemyTotalStats >= 35) score += 1;
            if (PrevGeraltPosition >= 4) score += 1;
            if (PrevGeraltPosition >= 5) score += 1;
            if (PrevContractsFought >= 3) score += 1;
            score -= TotalDemandsMade;
            return score;
        }
    }

    // Monster subtype name pools (for contract flavor text)
    public static readonly string[] DrownersNames = { "Утопец", "Кикимора", "Водяной", "Туманник", "Водяная баба", "Сирена", "Эхидна" };
    public static readonly string[] WerewolvesNames = { "Волколак", "Оборотень", "Берсерк", "Лешен", "Чёрт", "Бес" };
    public static readonly string[] VampiresNames = { "Катакан", "Гаркаин", "Бруха", "Альп", "Носферату", "Экимма" };
    public static readonly string[] DragonsNames = { "Дракон", "Виверна", "Кокатрикс", "Василиск", "Грифон", "Вилохвост", "Архигрифон" };

    public static string[] GetNames(MonsterType type) => type switch
    {
        MonsterType.Утопцы => DrownersNames,
        MonsterType.Волколаки => WerewolvesNames,
        MonsterType.Вампиры => VampiresNames,
        MonsterType.Драконы => DragonsNames,
        _ => DrownersNames
    };

    public static string GetMonsterTypeName(MonsterType type) => type switch
    {
        MonsterType.Утопцы => "Утопцев",
        MonsterType.Волколаки => "Волколаков",
        MonsterType.Вампиры => "Вампиров",
        MonsterType.Драконы => "Драконов",
        _ => "Монстров"
    };

    public static string GetMonsterColor(MonsterType type) => type switch
    {
        MonsterType.Утопцы => "#3B82F6",    // blue
        MonsterType.Волколаки => "#22C55E",  // green
        MonsterType.Вампиры => "#A855F7",    // vivid purple
        MonsterType.Драконы => "#EF4444",    // red
        _ => "#888888"
    };

    public static string GetMonsterEmoji(MonsterType type) => type switch
    {
        MonsterType.Утопцы => "💀",
        MonsterType.Волколаки => "🐺",
        MonsterType.Вампиры => "🦇",
        MonsterType.Драконы => "🐉",
        _ => "⚪"
    };

    // Witcher senses one-liners per character
    public static readonly Dictionary<string, string> WitcherSensesHints = new()
    {
    { "Weedwick", "Волчьи следы... Ведут на конопляное поле..." },
    { "Sirinoks", "Чешуя на камнях... Здесь живёт что-то крылатое..." },
    { "Кратос", "Пепел и цепи... Здесь бушевал бог войны..." },
    { "Вампур", "Следы клыков на шее. Высший вампир?" },
    { "Стая Гоблинов", "Маленькие следы. Много. Очень много." },
    { "DeepList", "Этот... слишком умный. Опасно." },
    { "mylorik", "Буйный воин. Жаждет мести." },
    { "Глеб", "Спит? Или притворяется?" },
    { "Тигр", "Зверь на вершине. Территориальный." },
    { "Толя", "Бронированная тварь. Сам не нападёт." },
    { "Осьминожка", "Щупальца повсюду. Неуязвим." },
    { "HardKitty", "Одиночка. Не трогай — не тронет." },
    { "LeCrisp", "Ассасин в тенях. Быстрый." },
    { "Кира", "Тетрадь... Пишет имена. Опасно." },
    { "Итачи", "Шаринган. Не смотри в глаза." },
    { "Котики", "Коты... повсюду коты." },
    { "Dopa", "Анализирует. Адаптируется. Побеждает." },
    { "Наполеон", "Стратег. Строит альянсы." },
    { "Були", "Утонул? Нет, он утопил кого-то другого." },
    { "Штормяк", "Гроза. Шторм идёт." },
    { "Рик", "Портал. Запах спирта. Плохое сочетание." },
    { "Загадочный Спартанец в маске", "Спартанец. Лучше не злить." },
    { "Сайтама", "Один удар... и всё." },
    { "Краборак", "Панцирь крепкий. Клешни острые." },
    { "Napoleon Wonnafcuk", "Полководец. Армия где-то рядом." },
    { "Таинственный Суппорт", "Помогает другим. Но кому?" },
    { "Токсичный Тиммейт", "Ядовитый. Держись подальше." },
    { "Юный Глеб", "Молодой. Много читает. Мета..." },
    { "The Boys", "Супергерои... или нет?" },
    { "Salldorum", "Время... Он его видит иначе." },
    { "Монстр без имени", "Нет имени... Это плохой знак." },
    };
}
