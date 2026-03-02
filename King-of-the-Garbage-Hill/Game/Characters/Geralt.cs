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
        public bool PlotvaContractsGrantedThisRound { get; set; } = false;
        public Dictionary<Guid, int> ContractProcsOnEnemy { get; set; } = new();
        public HashSet<Guid> EnemiesFoughtThisRound { get; set; } = new();
        public bool RareLootFoundThisRound { get; set; } = false;

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

    public class InvoiceLineItem
    {
        public string Label { get; set; }
        public int Points { get; set; }

        public InvoiceLineItem(string label, int points)
        {
            Label = label;
            Points = points;
        }
    }

    public class InvoiceResult
    {
        public List<InvoiceLineItem> LineItems { get; set; } = new();
        public int Total { get; set; }
        public int PredictedCoins { get; set; }
        public int PredictedDispleasure { get; set; }
    }

    public class ContractDemandClass
    {
        public int Displeasure { get; set; } = 0;          // 0-11, 11 = death
        public int TotalDemandsMade { get; set; } = 0;
        public int TotalSuccessfulDemands { get; set; } = 0;

        // Current round accumulators (written during fights)
        public int CurrentContractWins { get; set; } = 0;
        public int CurrentContractLosses { get; set; } = 0;
        public int CurrentTooGoodWins { get; set; } = 0;    // wins vs position 1-2
        public int CurrentTooStronkWins { get; set; } = 0;  // wins vs stats ≥ 28
        public int CurrentGeraltPosition { get; set; } = 0;

        // Previous round snapshot (copied at HandleEndOfRound, read by demand button)
        public int PrevContractWins { get; set; } = 0;
        public int PrevContractLosses { get; set; } = 0;
        public int PrevContractsFought { get; set; } = 0;
        public int PrevTooGoodWins { get; set; } = 0;
        public int PrevTooStronkWins { get; set; } = 0;
        public int PrevGeraltPosition { get; set; } = 0;
        public bool PrevLambertWasActive { get; set; } = false;
        public bool PrevWasBlocking { get; set; } = false;
        public bool PrevAllContractsFought { get; set; } = false;

        // Phase locks
        public bool DemandedThisPhase { get; set; } = false;
        public bool DemandedForNext { get; set; } = false;

        public bool QuestCompletedThisRound { get; set; } = false;

        public InvoiceResult CalculateInvoice()
        {
            var items = new List<InvoiceLineItem>();

            // Contract wins
            if (PrevContractWins > 0)
                items.Add(new InvoiceLineItem($"Убито монстров: {PrevContractWins}", PrevContractWins * 3));

            // Contract losses
            if (PrevContractLosses > 0)
                items.Add(new InvoiceLineItem($"Проиграно монстрам: {PrevContractLosses}", -PrevContractLosses * 2));

            // Flawless — won all, lost none, fought ≥ 2
            if (PrevContractWins >= 2 && PrevContractLosses == 0)
                items.Add(new InvoiceLineItem("Чистая работа", 3));

            // Barely survived — losses > wins, but at least 1 win
            if (PrevContractLosses > PrevContractWins && PrevContractWins > 0)
                items.Add(new InvoiceLineItem("Выжил чудом", 1));

            // Total failure — no wins at all
            if (PrevContractWins == 0 && PrevContractLosses > 0)
                items.Add(new InvoiceLineItem("Полный провал", -3));

            // Won against top-ranking enemy (position 1-2)
            if (PrevTooGoodWins > 0)
                items.Add(new InvoiceLineItem($"Убил сильнейших: {PrevTooGoodWins}", PrevTooGoodWins * 2));

            // Won against high-stats enemy (stats ≥ 28)
            if (PrevTooStronkWins > 0)
                items.Add(new InvoiceLineItem($"Сильный монстр: {PrevTooStronkWins}", PrevTooStronkWins * 2));

            // Lots of contract fights
            if (PrevContractsFought >= 5)
                items.Add(new InvoiceLineItem($"Горячий бой: {PrevContractsFought} контрактов", 2));
            else if (PrevContractsFought >= 3)
                items.Add(new InvoiceLineItem($"Много контрактов: {PrevContractsFought}", 1));

            // Geralt position — low position = village needs you more
            if (PrevGeraltPosition >= 5)
                items.Add(new InvoiceLineItem($"Геральт на {PrevGeraltPosition}-м месте", 2));
            else if (PrevGeraltPosition >= 3 && PrevGeraltPosition <= 4)
                items.Add(new InvoiceLineItem($"Геральт на {PrevGeraltPosition}-м месте", 1));

            // All contract enemies fought — every assigned enemy attacked or was attacked
            if (PrevAllContractsFought)
                items.Add(new InvoiceLineItem("Все контракты выполнены", 6));

            // Lambert was active — drunk at work
            if (PrevLambertWasActive)
                items.Add(new InvoiceLineItem("Ламберт... (пьянка на работе)", -4));

            // Blocked (meditated) — took too long
            if (PrevWasBlocking)
                items.Add(new InvoiceLineItem("Медитировал (долго возился)", -1));

            // Reputation — low displeasure = villagers trust you
            if (Displeasure <= 2)
                items.Add(new InvoiceLineItem("Хорошая репутация", 1));
            else if (Displeasure >= 8)
                items.Add(new InvoiceLineItem("Плохая репутация", -2));

            // Cumulative penalty
            if (TotalDemandsMade > 0)
                items.Add(new InvoiceLineItem($"Прошлые требования: {TotalDemandsMade}", -TotalDemandsMade));

            var total = 0;
            foreach (var item in items)
                total += item.Points;

            // Determine tier
            int coins, displeasure;
            if (total >= 8) { coins = 2; displeasure = 0; }
            else if (total >= 5) { coins = 1; displeasure = 0; }
            else if (total >= 2) { coins = 1; displeasure = 1; }
            else if (total >= 0) { coins = 0; displeasure = 2; }
            else { coins = 0; displeasure = 3; }

            return new InvoiceResult
            {
                LineItems = items,
                Total = total,
                PredictedCoins = coins,
                PredictedDispleasure = displeasure
            };
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
