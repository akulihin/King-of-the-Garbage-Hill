using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Characters;

namespace King_of_the_Garbage_Hill.Game.Classes;

public enum AchievementCategory
{
    Global,
    Character,
    Interaction
}

public class AchievementDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public string Description { get; set; }
    public string DescriptionRu { get; set; }
    public string SecretHint { get; set; }
    public string SecretHintRu { get; set; }
    public AchievementCategory Category { get; set; }
    public bool IsSecret { get; set; }
    public int Target { get; set; } = 1;
    public string Icon { get; set; }
    public string Rarity { get; set; }
    public List<string> CharacterNames { get; set; } = new();
    public int RewardZbs { get; set; }
    public int RewardLootBoxes { get; set; }

    public AchievementDefinition(
        string id,
        string name,
        string nameRu,
        string description,
        string descriptionRu,
        AchievementCategory category,
        string icon,
        string rarity,
        int target = 1,
        bool isSecret = false,
        string secretHint = "",
        string secretHintRu = "",
        params string[] characterNames)
    {
        Id = id;
        Name = name;
        NameRu = nameRu;
        Description = description;
        DescriptionRu = descriptionRu;
        Category = category;
        Icon = icon;
        Rarity = rarity.ToLowerInvariant();
        Target = Math.Max(1, target);
        IsSecret = isSecret;
        SecretHint = secretHint;
        SecretHintRu = secretHintRu;
        CharacterNames = characterNames?.ToList() ?? new List<string>();

        (RewardZbs, RewardLootBoxes) = Rarity switch
        {
            "uncommon" => (25, 0),
            "rare" => (50, 0),
            "epic" => (100, 1),
            "legendary" => (228, 2),
            _ => (10, 0),
        };
    }
}

public class AchievementProgress
{
    public string AchievementId { get; set; }
    public int Current { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTimeOffset? UnlockedAt { get; set; }

    public AchievementProgress() { }

    public AchievementProgress(string id)
    {
        AchievementId = id;
    }
}

public class AchievementData
{
    public List<AchievementProgress> Progress { get; set; } = new();

    // An unacknowledged queue. It is cleared only by ClearNewAchievements, not at match end.
    public List<string> NewlyUnlocked { get; set; } = new();
}

/// <summary>
/// Match-local observations used by Achievement V2. Nothing in this tracker is persisted as a
/// cumulative counter; account progress stores only the best result achieved in one match.
/// </summary>
public class InGameAchievementTracker
{
    public int TotalFightsWon { get; set; }
    public int TotalFightsLost { get; set; }
    public HashSet<Guid> DefeatedPlayerIds { get; set; } = new();
    public HashSet<string> DefeatedCharacterNames { get; set; } = new();
    public int BottomFeederWins { get; set; }
    public int NemesisAdvantageWins { get; set; }
    public HashSet<int> TargetSkillRounds { get; set; } = new();
    public bool WonFightWithMaxJustice { get; set; }
    public int DropsCaused { get; set; }
    public int SuperDickDropsCaused { get; set; }
    public int LivingWeaponJusticeBlocked { get; set; }
    public bool MoralBankruptcyTriggered { get; set; }
    public bool OpenedRoundTenAtLast { get; set; }
    public decimal RoundTenRegularPoints { get; set; }
    public HashSet<int> ExplicitAutoMoveRounds { get; set; } = new();
    public HashSet<string> FoughtCharacterNames { get; set; } = new();

    public int PortalGunFires { get; set; }
    public int DopaVisionProcs { get; set; }
    public int ToxicMaxTransferCount { get; set; }
    public int ToxicCancerReturns { get; set; }
    public int WeedHarvested { get; set; }
    public bool YoungGlebPinkWardUsed { get; set; }
    public bool TransformedFromMylorik { get; set; }
    public bool TransformedFromCthulhu { get; set; }
    public HashSet<Guid> KratosEventVictimIds { get; set; } = new();
    public int GeraltContractFightsResolved { get; set; }
    public Dictionary<Guid, int> GeraltContractFightsRemaining { get; set; } = new();
    public HashSet<string> KotikiCatsReclaimed { get; set; } = new();
    public HashSet<Guid> RumblingVictimIds { get; set; } = new();
    public HashSet<Guid> BfgWaveVictimIds { get; set; } = new();
    public int MonsterPawnExecutions { get; set; }

    public bool SpartanDragonSlayerTriggered { get; set; }
    public bool SpartanDragonSlayerDefeated { get; set; }
    public Guid SpartanRespectTriggeredThisFight { get; set; }
    public HashSet<Guid> SpartanRespectedOpponentIds { get; set; } = new();
    public bool SpartanDefeatedMylorikAfterRespect { get; set; }
    public bool ItachiMadaraCloneAttackGranted { get; set; }
    public bool DamagedReanimatedMadaraWithSuperDick { get; set; }
    public bool WitnessedMonsterApocalypse { get; set; }
    public int GordonHeadcrabsRemoved { get; set; }
    public bool GordonHalfLifeReleased { get; set; }
    public bool GordonCrowbarStoppedSuperDick { get; set; }
    public bool HomelanderOmniManEqualJusticeFight { get; set; }

    // Legacy fields are intentionally tolerated because old server snapshots and older hooks may
    // still populate them. Achievement V2 never evaluates these counters.
    public int ConsecutiveWins { get; set; }
    public int MaxConsecutiveWins { get; set; }
    public int TotalBlocksUsed { get; set; }
    public int RoundsAtFirst { get; set; }
    public int RoundsAtLast { get; set; }
    public int DifferentPlayersDefeated { get; set; }
    public HashSet<string> DefeatedPlayerNames { get; set; } = new();
    public int TimesPickled { get; set; }
    public int KiraKills { get; set; }
    public int KratosKills { get; set; }
    public bool WasKilledByKira { get; set; }
    public bool WasKilledByKratos { get; set; }
    public bool WasKilledByMonster { get; set; }
    public bool SurvivedKiraAttempt { get; set; }
    public bool BuiltZiggurat { get; set; }
    public int PortalGunSwaps { get; set; }
    public bool WasDragonForm { get; set; }
    public bool PickleRickTriggered { get; set; }
    public int VampireFeedCount { get; set; }
    public int GoblinCount { get; set; }
    public int StolenStats { get; set; }
    public bool FinishedWithZeroPsyche { get; set; }
    public bool FinishedWithMaxPsyche { get; set; }
    public bool NeverBlocked { get; set; } = true;
    public bool NeverAttacked { get; set; } = true;
    public int JusticeReached { get; set; }
    public int LvlUpsUsed { get; set; }
    public int MoralToPointsUsed { get; set; }
    public bool WasRevived { get; set; }
    public int EnemiesKilledAsKratos { get; set; }
    public bool KilledAllAsKratos { get; set; }
    public int SalldorumHistoryRewrites { get; set; }
    public int TheBoysOrdersCompleted { get; set; }
    public int GeraltContractsCompleted { get; set; }
    public int NapoleonTreaties { get; set; }
    public int KotikiTaunts { get; set; }
    public int DeepListMockeries { get; set; }
    public int SaitamaDeferredPoints { get; set; }
    public bool DarksciChosenStable { get; set; }
    public int SellerMarksPlaced { get; set; }
    public bool MonsterPawnUsed { get; set; }
    public decimal HighestSingleRoundScore { get; set; }
    public bool CameFromLastToFirst { get; set; }
    public bool WentFromFirstToLast { get; set; }
}

public static class AchievementService
{
    public static readonly List<AchievementDefinition> AllAchievements = new()
    {
        // Global mechanics
        new("g_bottom_feeder", "Bottom Feeder", "Со дна",
            "As the attacker in 6th place, defeat the current player in 1st place.",
            "Атакуя с 6-го места, победите текущего лидера на 1-м месте.",
            AchievementCategory.Global, "rocket", "uncommon", characterNames: Array.Empty<string>()),
        new("g_class_advantage", "Rock, Paper, Crown", "Камень, ножницы, корона",
            "Win 3 resolved fights while holding the Nemesis class advantage, on attack or defence.",
            "Победите в 3 состоявшихся боях, имея преимущество анти-класса — в атаке или защите.",
            AchievementCategory.Global, "crown", "rare", 3, characterNames: Array.Empty<string>()),
        new("g_target_routine", "Bullseye", "В яблочко",
            "Gain Main Skill from Мишень in 3 different rounds.",
            "Получите основной Скилл от Мишени в 3 разных раундах.",
            AchievementCategory.Global, "target", "common", 3, characterNames: Array.Empty<string>()),
        new("g_maximum_sentence", "Maximum Sentence", "Высшая мера",
            "Win a resolved fight while holding 5 live Justice.",
            "Победите в состоявшемся бою, имея 5 действующей Справедливости.",
            AchievementCategory.Global, "balance", "uncommon", characterNames: Array.Empty<string>()),
        new("g_three_drops", "Down the Chute", "Вниз по жёлобу",
            "Personally cause 3 Drops in one match.",
            "Лично вызовите 3 Скидывания за один матч.",
            AchievementCategory.Global, "falling", "rare", 3, characterNames: Array.Empty<string>()),
        new("g_twenty_moral", "Moral Bankruptcy", "Моральное банкротство",
            "Convert 20 Moral into 10 bonus points in a single exchange.",
            "Одним обменом превратите 20 Морали в 10 бонусных очков.",
            AchievementCategory.Global, "heart", "uncommon", characterNames: Array.Empty<string>()),
        new("g_open_book", "Open Book", "Открытая книга",
            "Correctly predict every eligible opponent, with at least 3 eligible targets.",
            "Верно предскажите каждого доступного соперника; доступных целей должно быть не меньше 3.",
            AchievementCategory.Global, "eye", "rare", characterNames: Array.Empty<string>()),
        new("g_clean_sweep", "Garbage Collector", "Сборщик мусора",
            "In solo mode, defeat all 5 opponents at least once.",
            "В одиночном режиме победите каждого из 5 соперников хотя бы раз.",
            AchievementCategory.Global, "swords", "rare", 5, characterNames: Array.Empty<string>()),
        new("g_round10_comeback", "From Sixth to King", "Из шестого — в короли",
            "Open round 10 in 6th place, then finish alive in 1st place in solo mode.",
            "Начните 10-й раунд на 6-м месте, а завершите одиночный матч живым лидером.",
            AchievementCategory.Global, "crown", "epic", characterNames: Array.Empty<string>()),
        new("g_untouchable", "Untouchable", "Неприкасаемый",
            "Win a solo match with at least 5 resolved wins and no resolved losses.",
            "Победите в одиночном матче, выиграв не меньше 5 состоявшихся боёв и не проиграв ни одного.",
            AchievementCategory.Global, "shield", "epic", 5, characterNames: Array.Empty<string>()),
        new("g_quad_damage", "Quad Damage", "Четверной урон",
            "Receive at least 20 net regular points from round 10 after the real multiplier.",
            "Получите не меньше 20 чистых обычных очков за 10-й раунд после реального множителя.",
            AchievementCategory.Global, "bolt", "rare", 20, characterNames: Array.Empty<string>()),
        new("g_auto_pilot", "Definitely Not a Bot", "Точно не бот",
            "Use Auto Move in every standard action round: all 10, except Тигр's banned round 10 and Мадара's locked round 8.",
            "Используйте Авто Ход в каждом стандартном раунде выбора: во всех 10, кроме запретного 10-го у Тигр и заблокированного 8-го у Мадара.",
            AchievementCategory.Global, "games", "epic", isSecret: true,
            secretHint: "Sometimes the most committed strategy is to stop making decisions.",
            secretHintRu: "Иногда самая последовательная стратегия — вообще перестать принимать решения.",
            characterNames: Array.Empty<string>()),

        // Character stories
        new("c_boys_orders", "French Connection", "Французская связь",
            "As TheBoys, complete all 3 Francie orders.",
            "Играя за TheBoys, выполните все 3 заказа Francie.",
            AchievementCategory.Character, "badge", "uncommon", 3, characterNames: new[] { "TheBoys" }),
        new("c_boys_ultimate", "The Boys Are Back", "Пацаны снова в деле",
            "As TheBoys, prove one ultimate: infect 3 players, cause 5 Drops with СуперМудень, block 5 Justice with Живое Оружие, or finish with M.M. fully upgraded and 3 dossiers.",
            "Играя за TheBoys, докажите силу одного ультимейта: заразите 3 игроков, вызовите 5 Скидываний с СуперМудень, заблокируйте 5 Справедливости с Живое Оружие или завершите матч с полностью улучшенным M.M. и 3 компроматами.",
            AchievementCategory.Character, "swords", "epic", characterNames: new[] { "TheBoys" }),

        new("c_goblin_architect", "Location, Location, Ziggurat", "Место, место и Зиккурат",
            "As Стая Гоблинов, build your first Ziggurat.",
            "Играя за Стая Гоблинов, постройте первый Зиккурат.",
            AchievementCategory.Character, "pyramid", "common", characterNames: new[] { "Стая Гоблинов" }),
        new("c_goblin_summit", "Built Different", "Особая постройка",
            "As Стая Гоблинов, finish with a Ziggurat at place 1 and receive its enforced win.",
            "Играя за Стая Гоблинов, завершите матч с Зиккуратом на 1-м месте и получите его гарантированную победу.",
            AchievementCategory.Character, "pyramid", "legendary", characterNames: new[] { "Стая Гоблинов" }),

        new("c_rick_beans", "Bean There, Done That", "Боб был — интеллект вырос",
            "As Рик Санчез, reach 3 Giant Bean stacks.",
            "Играя за Рик Санчез, накопите 3 заряда Гигантских бобов.",
            AchievementCategory.Character, "pickle", "uncommon", 3, characterNames: new[] { "Рик Санчез" }),
        new("c_rick_portals", "Portal Authority", "Портальная власть",
            "As Рик Санчез, successfully fire Портальная пушка twice in one match.",
            "Играя за Рик Санчез, дважды успешно примените Портальная пушка за матч.",
            AchievementCategory.Character, "portal", "rare", 2, characterNames: new[] { "Рик Санчез" }),

        new("c_saitama_serious", "A Worthy Warm-Up", "Достойная разминка",
            "As Сайтама, bank at least 5 deferred points through Неприметность.",
            "Играя за Сайтама, отложите не меньше 5 очков через Неприметность.",
            AchievementCategory.Character, "fist", "common", 5, characterNames: new[] { "Сайтама" }),
        new("c_saitama_one_punch", "One Punch", "Один удар",
            "As Сайтама, reclaim at least 20 deferred points through Ищет достойного противника.",
            "Играя за Сайтама, верните не меньше 20 отложенных очков через Ищет достойного противника.",
            AchievementCategory.Character, "fist", "epic", 20, characterNames: new[] { "Сайтама" }),

        new("c_madara_round_eight", "One Versus an Army", "Один против армии",
            "As Мадара, win 3 fights during round 8.",
            "Играя за Мадара, победите в 3 боях в 8-м раунде.",
            AchievementCategory.Character, "eye-glow", "rare", 3, characterNames: new[] { "Мадара" }),
        new("c_madara_tsukuyomi", "Wake Up to Reality", "Очнись и вернись в реальность",
            "As Мадара, finish with Вечное Цукуеми active and without being sealed.",
            "Играя за Мадара, завершите матч с активным Вечное Цукуеми и не будьте запечатаны.",
            AchievementCategory.Character, "eye-glow", "legendary", isSecret: true,
            secretHint: "Let the whole world sleep without allowing it to seal you away.",
            secretHintRu: "Погрузите весь мир в сон и не позвольте ему запечатать вас.",
            characterNames: new[] { "Мадара" }),

        new("c_tigr_three_zero", "Clean Sheet", "Сухая победа",
            "As Тигр, complete one 3-0 обоссан.",
            "Играя за Тигр, завершите один 3-0 обоссан.",
            AchievementCategory.Character, "medal", "uncommon", characterNames: new[] { "Тигр" }),
        new("c_tigr_six_zero", "Six–Zero", "Шесть — ноль",
            "As Тигр, complete 3-0 обоссан against 2 different enemies.",
            "Играя за Тигр, завершите 3-0 обоссан против 2 разных соперников.",
            AchievementCategory.Character, "trophy", "epic", 2, characterNames: new[] { "Тигр" }),

        new("c_itachi_crows", "Murder of Crows", "Воронья сходка",
            "As Итачи, leave Crow marks on 3 different opponents at once.",
            "Играя за Итачи, одновременно оставьте Ворон на 3 разных соперниках.",
            AchievementCategory.Character, "bird", "uncommon", 3, characterNames: new[] { "Итачи" }),
        new("c_itachi_tax", "Tax Collector", "Сборщик налогов",
            "As Итачи, copy at least 20 total points through Глаза Итачи.",
            "Играя за Итачи, скопируйте не меньше 20 очков через Глаза Итачи.",
            AchievementCategory.Character, "eye", "rare", 20, characterNames: new[] { "Итачи" }),

        new("c_kratos_rampage", "First Blood of Olympus", "Первая кровь Олимпа",
            "As Кратос, personally kill another player during Возвращение из мертвых.",
            "Играя за Кратос, лично убейте другого игрока во время Возвращение из мертвых.",
            AchievementCategory.Character, "axe", "common", characterNames: new[] { "Кратос" }),
        new("c_kratos_olympus", "Ghost of Sparta", "Призрак Спарты",
            "As Кратос, personally kill all 5 other players during Возвращение из мертвых.",
            "Играя за Кратос, лично убейте всех 5 остальных игроков во время Возвращение из мертвых.",
            AchievementCategory.Character, "swords", "legendary", 5, characterNames: new[] { "Кратос" }),

        new("c_kira_first_name", "First Name Basis", "По имени и насмерть",
            "As Кира, write one correct victim into Тетрадь смерти.",
            "Играя за Кира, верно запишите одну жертву в Тетрадь смерти.",
            AchievementCategory.Character, "notebook", "common", characterNames: new[] { "Кира" }),
        new("c_kira_perfect_crime", "Perfect Crime", "Идеальное преступление",
            "As Кира, record 3 successful Тетрадь смерти kills on different victims.",
            "Играя за Кира, совершите 3 успешных убийства разных жертв через Тетрадь смерти.",
            AchievementCategory.Character, "notebook", "epic", 3, characterNames: new[] { "Кира" }),

        new("c_monster_no_escape", "No One Escapes the Story", "Из этой истории не уйти",
            "As Монстр без имени, execute one pawn through Пейзаж конца света.",
            "Играя за Монстр без имени, казните одну пешку через Пейзаж конца света.",
            AchievementCategory.Character, "puppet", "common", characterNames: new[] { "Монстр без имени" }),
        new("c_monster_apocalypse", "Beautiful Apocalypse", "Прекрасный апокалипсис",
            "As Монстр без имени, execute at least 2 pawns through Пейзаж конца света.",
            "Играя за Монстр без имени, казните не меньше 2 пешек через Пейзаж конца света.",
            AchievementCategory.Character, "radiation", "epic", 2, characterNames: new[] { "Монстр без имени" }),

        new("c_seller_marks", "Three Easy Payments", "Три выгодных платежа",
            "As Продавец Сомнительных Тактик, mark 3 different players.",
            "Играя за Продавец Сомнительных Тактик, пометьте 3 разных игроков.",
            AchievementCategory.Character, "briefcase", "uncommon", 3,
            characterNames: new[] { "Продавец Сомнительных Тактик" }),
        new("c_seller_market", "Market Manipulator", "Повелитель рынка",
            "As Продавец Сомнительных Тактик, mark all 5 opponents and accumulate 100 Skill in Секретный билд.",
            "Играя за Продавец Сомнительных Тактик, пометьте всех 5 соперников и накопите 100 Скилла в Секретный билд.",
            AchievementCategory.Character, "briefcase", "epic", characterNames: new[] { "Продавец Сомнительных Тактик" }),

        new("c_dopa_foresight", "Ward Diff", "Разница в вардах",
            "As Dopa, trigger Взгляд в будущее once.",
            "Играя за Dopa, активируйте Взгляд в будущее один раз.",
            AchievementCategory.Character, "eye", "common", characterNames: new[] { "Dopa" }),
        new("c_dopa_big_brain", "Three Steps Ahead", "На три шага впереди",
            "As Dopa, trigger Взгляд в будущее 3 times in one match.",
            "Играя за Dopa, активируйте Взгляд в будущее 3 раза за матч.",
            AchievementCategory.Character, "eye-glow", "epic", 3, characterNames: new[] { "Dopa" }),
        new("c_dopa_permaban", "Rank One, Account Gone", "Топ-1, аккаунта нет",
            "As Dopa, enter turn 10 in first place and get Permabanned.",
            "Играя за Dopa, начните 10-й ход на первом месте и получите Permaban.",
            AchievementCategory.Character, "ban", "epic", characterNames: new[] { "Dopa" }),

        new("c_salldorum_cola", "Open Happiness", "Открой счастье",
            "As Salldorum, drink the Time Capsule cola once.",
            "Играя за Salldorum, один раз выпейте колу из Временной капсулы.",
            AchievementCategory.Character, "hourglass", "uncommon", characterNames: new[] { "Salldorum" }),
        new("c_salldorum_double_cola", "History Repeats Itself", "История повторяется",
            "Cola can be drunk twice if you know history.",
            "Колу можно выпить дважды, если знать историю.",
            AchievementCategory.Character, "hourglass", "legendary", 2, isSecret: true,
            secretHint: "Cola can be drunk twice if you know history.",
            secretHintRu: "Колу можно выпить дважды, если знать историю.",
            characterNames: Array.Empty<string>()),

        new("c_geralt_contracts", "Witcher’s Payday", "Ведьмачья получка",
            "As Геральт, resolve 3 contract fights in one match.",
            "Играя за Геральт, завершите 3 контрактных боя за один матч.",
            AchievementCategory.Character, "medallion", "uncommon", 3, characterNames: new[] { "Геральт" }),
        new("c_geralt_path", "All Signs Point to Trouble", "Все Знаки ведут к беде",
            "As Геральт, reveal all 4 contract targets through Медитация.",
            "Играя за Геральт, раскройте все 4 контрактные цели через Медитация.",
            AchievementCategory.Character, "medallion", "epic", 4, characterNames: new[] { "Геральт" }),

        new("c_kotiki_one_back", "Cat Came Back", "Один котик вернулся",
            "As Котики, reclaim either Минька or Штормяк.",
            "Играя за Котики, верните Минька или Штормяк.",
            AchievementCategory.Character, "cat", "common", characterNames: new[] { "Котики" }),
        new("c_kotiki_reunion", "The Cats Came Back", "Котики вернулись",
            "As Котики, reclaim both Минька and Штормяк by winning their return attacks.",
            "Играя за Котики, верните и Минька, и Штормяк, победив в обеих атаках за возвращение.",
            AchievementCategory.Character, "cat", "rare", 2, characterNames: new[] { "Котики" }),

        new("c_toxic_chain", "Return to Sender", "Вернуть отправителю",
            "As Toxic Mate, have Get cancer return after a chain of at least 2 transfers.",
            "Играя за Toxic Mate, дождитесь возвращения Get cancer после цепочки минимум из 2 передач.",
            AchievementCategory.Character, "radiation", "uncommon", characterNames: new[] { "Toxic Mate" }),
        new("c_toxic_return", "Full Circle of Toxicity", "Полный круг токсичности",
            "As Toxic Mate, have Get cancer return after a chain of at least 5 transfers.",
            "Играя за Toxic Mate, дождитесь возвращения Get cancer после цепочки минимум из 5 передач.",
            AchievementCategory.Character, "radiation", "epic", characterNames: new[] { "Toxic Mate" }),

        new("c_napoleon_alliance", "First-Fight Diplomacy", "Дипломатия первого боя",
            "As Napoleon Wonnafcuk, resolve first-fight business with 3 different players.",
            "Играя за Napoleon Wonnafcuk, решите дела первого боя с 3 разными игроками.",
            AchievementCategory.Character, "handshake", "uncommon", 3,
            characterNames: new[] { "Napoleon Wonnafcuk" }),
        new("c_napoleon_treaties", "There Can Be Only Two", "Останутся только двое",
            "As Napoleon Wonnafcuk in team mode, form an alliance and defeat all 4 non-team enemies.",
            "Играя за Napoleon Wonnafcuk в командном режиме, заключите союз и победите всех 4 врагов вне команды.",
            AchievementCategory.Character, "crown", "epic", 4,
            characterNames: new[] { "Napoleon Wonnafcuk" }),

        new("c_support_buff", "We Scale Together", "Скейлимся вместе",
            "As Таинственный Суппорт, finish alive in the top 3 together with your marked Carry.",
            "Играя за Таинственный Суппорт, завершите матч живым в топ-3 вместе с отмеченным Carry.",
            AchievementCategory.Character, "heart", "rare", characterNames: new[] { "Таинственный Суппорт" }),
        new("c_support_premade", "Duo Queue Takeover", "Захват дуо-очереди",
            "As Таинственный Суппорт, finish alive in 1st while your marked Carry finishes alive in 2nd.",
            "Играя за Таинственный Суппорт, завершите матч живым на 1-м месте, а отмеченный Carry — живым на 2-м.",
            AchievementCategory.Character, "crown", "legendary", characterNames: new[] { "Таинственный Суппорт" }),

        new("c_octopus_tour", "Three Stops, Eight Arms", "Три остановки, восемь щупалец",
            "As Осьминожка, visit 3 different leaderboard places with Раскинуть щупальца.",
            "Играя за Осьминожка, посетите 3 разных места таблицы с помощью Раскинуть щупальца.",
            AchievementCategory.Character, "compass", "uncommon", 3, characterNames: new[] { "Осьминожка" }),
        new("c_octopus_ink", "Every Seat Is Reserved", "Забронированы все места",
            "As Осьминожка, visit all 6 leaderboard places with Раскинуть щупальца.",
            "Играя за Осьминожка, посетите все 6 мест таблицы с помощью Раскинуть щупальца.",
            AchievementCategory.Character, "compass", "epic", 6, characterNames: new[] { "Осьминожка" }),

        new("c_deeplist_mockery", "Read and Roasted", "Прочитал и подколол",
            "As DeepList, trigger Стёб against 2 different players.",
            "Играя за DeepList, активируйте Стёб против 2 разных игроков.",
            AchievementCategory.Character, "jester", "uncommon", 2, characterNames: new[] { "DeepList" }),
        new("c_deeplist_roast", "Everybody Gets the Joke", "Шутку поняли все",
            "As DeepList, trigger Стёб against all 5 opponents.",
            "Играя за DeepList, активируйте Стёб против всех 5 соперников.",
            AchievementCategory.Character, "jester", "epic", 5, characterNames: new[] { "DeepList" }),

        new("c_mylorik_revenge", "Two Names on the List", "Два имени в списке",
            "As mylorik, complete Месть against 2 different players.",
            "Играя за mylorik, завершите Месть против 2 разных игроков.",
            AchievementCategory.Character, "skull", "uncommon", 2, characterNames: new[] { "mylorik" }),
        new("c_mylorik_grudges", "No Grudge Left Behind", "Ни одной незакрытой обиды",
            "As mylorik, complete Месть against all 5 opponents.",
            "Играя за mylorik, завершите Месть против всех 5 соперников.",
            AchievementCategory.Character, "skull-crossbones", "epic", 5, characterNames: new[] { "mylorik" }),

        new("c_gleb_return", "The Tea Can Wait", "Чай подождёт",
            "As classic Глеб, return and meet at least one player who previously caught you away.",
            "Играя за классического Глеб, вернитесь и встретьте хотя бы одного игрока, который раньше застал вас отсутствующим.",
            AchievementCategory.Character, "hourglass", "common", characterNames: new[] { "Глеб" }),
        new("c_gleb_challenger", "Russian Server Final Boss", "Финальный босс русского сервера",
            "As classic Глеб, have Претендент русского сервера scheduled for round 10 and earn 24 net regular points that round.",
            "Играя за классического Глеб, получите Претендент русского сервера в расписании 10-го раунда и заработайте там 24 чистых обычных очка.",
            AchievementCategory.Character, "crown", "epic", characterNames: new[] { "Глеб" }),

        new("c_lecrisp_impact", "Make an Impact", "Произвести впечатление",
            "As LeCrisp, trigger Импакт 4 times.",
            "Играя за LeCrisp, активируйте Импакт 4 раза.",
            AchievementCategory.Character, "bolt", "uncommon", 4, characterNames: new[] { "LeCrisp" }),
        new("c_lecrisp_legend", "Eightfold Impact", "Восьмикратный импакт",
            "As LeCrisp, trigger Импакт 8 times and win at least 3 resolved fights.",
            "Играя за LeCrisp, активируйте Импакт 8 раз и победите минимум в 3 состоявшихся боях.",
            AchievementCategory.Character, "bolt", "epic", 8, characterNames: new[] { "LeCrisp" }),

        new("c_tolya_rammus", "Count on Me", "Можешь на меня подсчитать",
            "As Толя, use Подсчет on another player.",
            "Играя за Толя, примените Подсчет к другому игроку.",
            AchievementCategory.Character, "balance", "uncommon", characterNames: new[] { "Толя" }),
        new("c_tolya_accounting", "King of Accounting", "Король бухгалтерии",
            "As Толя, use Подсчет on 2 different players and finish alive in 1st place.",
            "Играя за Толя, примените Подсчет к 2 разным игрокам и завершите матч живым на 1-м месте.",
            AchievementCategory.Character, "crown", "legendary", 2, characterNames: new[] { "Толя" }),

        new("c_hardkitty_letters", "Dear Everybody", "Дорогие все",
            "As HardKitty, record attacks from 3 different players in Одиночество.",
            "Играя за HardKitty, запишите атаки 3 разных игроков в Одиночество.",
            AchievementCategory.Character, "heart", "rare", 3, characterNames: new[] { "HardKitty" }),
        new("c_hardkitty_love", "Twenty Love Letters", "Двадцать писем любви",
            "As HardKitty, record all 5 opponents in Одиночество and collect at least 20 weighted letter points.",
            "Играя за HardKitty, запишите всех 5 соперников в Одиночество и соберите не меньше 20 взвешенных очков писем.",
            AchievementCategory.Character, "heart", "epic", 20, characterNames: new[] { "HardKitty" }),

        new("c_sirinoks_friends", "Friend Request Accepted", "Заявка в друзья принята",
            "As Sirinoks, befriend 3 different players.",
            "Играя за Sirinoks, подружитесь с 3 разными игроками.",
            AchievementCategory.Character, "handshake", "uncommon", 3, characterNames: new[] { "Sirinoks" }),
        new("c_sirinoks_dragon", "Queen of Friends and Dragons", "Королева друзей и драконов",
            "As Sirinoks, befriend all 5 opponents and finish alive in 1st place.",
            "Играя за Sirinoks, подружитесь со всеми 5 соперниками и завершите матч живой на 1-м месте.",
            AchievementCategory.Character, "dragon", "legendary", 5, characterNames: new[] { "Sirinoks" }),

        new("c_mitsuki_loud", "Schoolyard Regular", "Завсегдатай школьного двора",
            "As Злой Школьник, record 3 different opponents through Запах мусора.",
            "Играя за Злой Школьник, отметьте 3 разных соперников через Запах мусора.",
            AchievementCategory.Character, "trophy-broken", "uncommon", 3, characterNames: new[] { "Злой Школьник" }),
        new("c_mitsuki_garbage", "Everyone Gets Detention", "Всем остаться после уроков",
            "As Злой Школьник, record all 5 opponents through Запах мусора at least twice each.",
            "Играя за Злой Школьник, отметьте всех 5 соперников через Запах мусора минимум по два раза.",
            AchievementCategory.Character, "trophy-broken", "epic", 5, characterNames: new[] { "Злой Школьник" }),

        new("c_awdka_trying", "Actually Trying", "Он действительно пытается",
            "As AWDKA, reach 2 attempts against 2 different players.",
            "Играя за AWDKA, доберитесь до 2 попыток против 2 разных игроков.",
            AchievementCategory.Character, "games", "uncommon", 2, characterNames: new[] { "AWDKA" }),
        new("c_awdka_mastery", "It Finally Worked", "Наконец-то получилось",
            "As AWDKA, reach 2 attempts against all 5 opponents.",
            "Играя за AWDKA, доберитесь до 2 попыток против всех 5 соперников.",
            AchievementCategory.Character, "games", "epic", 5, characterNames: new[] { "AWDKA" }),

        new("c_darksci_stable", "Any Odds Will Do", "Подойдут любые шансы",
            "As Darksci, trigger Повезло with either type.",
            "Играя за Darksci, активируйте Повезло с любым выбранным типом.",
            AchievementCategory.Character, "dice", "uncommon", characterNames: new[] { "Darksci" }),
        new("c_darksci_unstable", "Against All Odds", "Вопреки всему",
            "As Darksci, choose unstable, trigger Повезло, and finish alive in 1st place.",
            "Играя за Darksci, выберите нестабильность, активируйте Повезло, останьтесь в живых и завершите матч на 1-м месте.",
            AchievementCategory.Character, "dice-six", "epic", characterNames: new[] { "Darksci" }),

        new("c_shark_teeth", "Three Rows of Teeth", "Три ряда зубов",
            "As Братишка, win through Челюсти against 3 different players.",
            "Играя за Братишка, победите через Челюсти против 3 разных игроков.",
            AchievementCategory.Character, "swords", "uncommon", 3, characterNames: new[] { "Братишка" }),
        new("c_shark_apex", "Apex Accountant", "Главный по зубам и местам",
            "As Братишка, make your distinct Челюсти victims plus distinct tracked leaderboard places total at least 10.",
            "Играя за Братишка, наберите суммарно минимум 10 уникальных жертв Челюсти и отмеченных мест таблицы.",
            AchievementCategory.Character, "swords", "epic", 10, characterNames: new[] { "Братишка" }),

        new("c_spartan_shame", "Shame Travels Fast", "Стыд быстро разносится",
            "As Загадочный Спартанец в маске, mark 3 players through Они позорят военное искусство.",
            "Играя за Загадочный Спартанец в маске, отметьте 3 игроков через Они позорят военное искусство.",
            AchievementCategory.Character, "shield", "uncommon", 3,
            characterNames: new[] { "Загадочный Спартанец в маске" }),
        new("c_spartan_warrior", "No One Likes the Warrior", "Воина не любит никто",
            "As Загадочный Спартанец в маске, shame all 5 opponents and defeat all 5 at least once.",
            "Играя за Загадочный Спартанец в маске, опозорьте всех 5 соперников и победите каждого хотя бы раз.",
            AchievementCategory.Character, "shield-cross", "legendary", 5,
            characterNames: new[] { "Загадочный Спартанец в маске" }),

        new("c_vampyr_bites", "Three-Course Meal", "Обед из трёх блюд",
            "As Вампур, maintain 3 active Гематофагия bites.",
            "Играя за Вампур, удерживайте 3 активных укуса Гематофагия.",
            AchievementCategory.Character, "bat", "common", 3, characterNames: new[] { "Вампур" }),
        new("c_vampyr_feast", "All-You-Can-Bite", "Кусай сколько влезет",
            "As Вампур, maintain 5 active Гематофагия bites and finish alive.",
            "Играя за Вампур, удерживайте 5 активных укусов Гематофагия и завершите матч живым.",
            AchievementCategory.Character, "bat", "epic", 5, characterNames: new[] { "Вампур" }),

        new("c_crab_shell", "Shell Company", "Панцирная компания",
            "As Краборак, welcome 3 players into Панцирь.",
            "Играя за Краборак, примите 3 игроков в Панцирь.",
            AchievementCategory.Character, "turtle", "uncommon", 3, characterNames: new[] { "Краборак" }),
        new("c_crab_fortress", "Shell Game Champion", "Чемпион панцирной игры",
            "As Краборак, fill Панцирь with all 5 opponents and defeat 3 different players.",
            "Играя за Краборак, заполните Панцирь всеми 5 соперниками и победите 3 разных игроков.",
            AchievementCategory.Character, "turtle", "epic", 5, characterNames: new[] { "Краборак" }),

        new("c_weedwick_smoke", "A Modest Harvest", "Скромный урожай",
            "As Weedwick, harvest 5 Weed in one match.",
            "Играя за Weedwick, соберите 5 единиц Weed за матч.",
            AchievementCategory.Character, "lotus", "common", 5, characterNames: new[] { "Weedwick" }),
        new("c_weedwick_harvest", "Industrial Agriculture", "Промышленное земледелие",
            "As Weedwick, harvest 20 Weed in one match.",
            "Играя за Weedwick, соберите 20 единиц Weed за матч.",
            AchievementCategory.Character, "lotus", "epic", 20, characterNames: new[] { "Weedwick" }),

        new("c_young_gleb_meta", "Pink Is the New Meta", "Розовый — новая мета",
            "As Молодой Глеб, use the Pink Ward from Коммуникация.",
            "Играя за Молодой Глеб, используйте Розовый вард через Коммуникация.",
            AchievementCategory.Character, "eye", "rare", characterNames: new[] { "Глеб", "Молодой Глеб" }),
        new("c_young_gleb_ward", "Top Gap", "Разрыв на топе",
            "As Молодой Глеб, finish alive in 1st place with all four stats at 7 or higher.",
            "Играя за Молодой Глеб, завершите матч живым на 1-м месте со всеми четырьмя статами не ниже 7.",
            AchievementCategory.Character, "crown", "legendary", characterNames: new[] { "Глеб", "Молодой Глеб" }),

        new("c_sakura_three", "Still in the Story", "Всё ещё в сюжете",
            "As Sakura, finish alive in the top 3.",
            "Играя за Sakura, завершите матч живой в топ-3.",
            AchievementCategory.Character, "lotus", "rare", isSecret: true,
            secretHint: "A certain kunoichi would like the podium to remember she exists.",
            secretHintRu: "Одна куноити хочет, чтобы пьедестал вспомнил о её существовании.",
            characterNames: new[] { "Sakura" }),
        new("c_sakura_first", "Useful After All", "Всё-таки полезна",
            "As Sakura, finish alive in 1st place after winning at least 5 resolved fights.",
            "Играя за Sakura, завершите матч живой на 1-м месте после минимум 5 побед в состоявшихся боях.",
            AchievementCategory.Character, "lotus", "legendary", isSecret: true,
            secretHint: "Win enough arguments that nobody can call you useless from below.",
            secretHintRu: "Выиграйте достаточно споров, чтобы снизу вас уже не назвали бесполезной.",
            characterNames: new[] { "Sakura" }),

        new("c_doom_loadout", "Rip, Tear, Roll", "Рви, кромсай, ролль",
            "As DooM Guy, enter Roll Mode with one active module in all 4 stages.",
            "Играя за DooM Guy, войдите в Roll Mode с активным модулем на всех 4 этапах.",
            AchievementCategory.Character, "games", "rare", characterNames: new[] { "DooM Guy" }),
        new("c_doom_bfg", "BFG Division", "Дивизия BFG",
            "As DooM Guy, defeat at least 3 players in one BFG wave, including its primary target.",
            "Играя за DooM Guy, победите не меньше 3 игроков одной волной BFG, включая основную цель.",
            AchievementCategory.Character, "bolt", "epic", 3, characterNames: new[] { "DooM Guy" }),

        new("c_eren_tatake", "Tatake! Tatake!", "Татакай! Татакай!",
            "As Эрен Йегер, trigger the Tatake sound twice.",
            "Играя за Эрен Йегер, дважды активируйте звук Tatake.",
            AchievementCategory.Character, "fist", "uncommon", 2, characterNames: new[] { "Эрен Йегер" }),
        new("c_eren_rumbling", "The Rumbling", "Гул Земли",
            "As Эрен Йегер, kill at least 2 players with Rumbling.",
            "Играя за Эрен Йегер, убейте не меньше 2 игроков с помощью Rumbling.",
            AchievementCategory.Character, "falling", "epic", 2, characterNames: new[] { "Эрен Йегер" }),

        new("c_naruto_harem", "Believe in the Harem", "Поверь в гарем",
            "As the original Наруто, receive 3 donations with Гарем но джутсу.",
            "Играя за оригинального Наруто, получите 3 доната с помощью Гарем но джутсу.",
            AchievementCategory.Character, "heart", "rare", 3, characterNames: new[] { "Наруто" }),
        new("c_naruto_rasengan", "Shadow Hokage Dividend", "Дивиденды теневого Хокаге",
            "As the original Наруто, receive 30 points from Теневые and finish alive in 1st place.",
            "Играя за оригинального Наруто, получите 30 очков от Теневые и завершите матч живым на 1-м месте.",
            AchievementCategory.Character, "sparkles", "legendary", 30, characterNames: new[] { "Наруто" }),

        new("c_gordon_rescue", "Unforeseen Consequences", "Непредвиденные последствия",
            "As Гордон Фримен, remove 3 headcrabs.",
            "Играя за Гордон Фримен, снимите 3 хэдкраба.",
            AchievementCategory.Character, "bug", "uncommon", 3,
            characterNames: new[] { "Гордон Фримен" }),
        new("c_gordon_halflife3", "Half-Life 3 Confirmed", "Halflife 3 подтверждён",
            "As Гордон Фримен, successfully release Halflife 3.",
            "Играя за Гордон Фримен, успешно выпустите Halflife 3.",
            AchievementCategory.Character, "games", "epic",
            characterNames: new[] { "Гордон Фримен" }),

        new("c_jon_king", "Bastard No More", "Больше не бастард",
            "As Джон Сноу, reach 228 Skill and become Король Сервера.",
            "Играя за Джон Сноу, наберите 228 Скилла и станьте Король Сервера.",
            AchievementCategory.Character, "crown", "uncommon",
            characterNames: new[] { JonSnow.CharacterName }),
        new("c_jon_watch", "And Now My Watch Is Ended", "И теперь мой дозор окончен",
            "As Джон Сноу, overcome a death and still finish alive in the top 3.",
            "Играя за Джон Сноу, превозмогите смерть и всё равно завершите матч живым в топ-3.",
            AchievementCategory.Character, "wolf", "epic",
            characterNames: new[] { JonSnow.CharacterName }),

        // Secret interactions
        new("x_spartan_dragon", "Dragon Slayer", "Убийца драконов",
            "As Загадочный Спартанец в маске, trigger DragonSlayer against round-10 Sirinoks/Дракон and defeat her.",
            "Играя за Загадочный Спартанец в маске, активируйте DragonSlayer против Sirinoks/Дракон в 10-м раунде и победите её.",
            AchievementCategory.Interaction, "dragon", "epic", isSecret: true,
            secretHint: "A masked warrior has one very specific round-ten rival.",
            secretHintRu: "У воина в маске есть одна очень особенная соперница в десятом раунде.",
            characterNames: new[] { "Загадочный Спартанец в маске", "Sirinoks" }),
        new("x_kira_kratos", "Gods Don’t Tell Me What to Do", "Боги мне не указ",
            "As Кратос, die to Kira’s Тетрадь смерти and revive through Боги мне не указ.",
            "Играя за Кратос, умрите от Тетрадь смерти Киры и воскресните через Боги мне не указ.",
            AchievementCategory.Interaction, "shield-cross", "legendary", isSecret: true,
            secretHint: "A name written down is not always enough to kill a god.",
            secretHintRu: "Иногда записанного имени недостаточно, чтобы убить бога.",
            characterNames: new[] { "Кира", "Кратос" }),
        new("x_itachi_madara", "Eyes Meet Eyes", "Глаза встретились",
            "As Итачи, correctly lock Мадара in round 8 and receive the extra Клоны Сусано attack.",
            "Играя за Итачи, верно зафиксируйте Мадара в 8-м раунде и получите дополнительную атаку Клоны Сусано.",
            AchievementCategory.Interaction, "eye-glow", "rare", isSecret: true,
            secretHint: "Two pairs of eyes must meet in round eight.",
            secretHintRu: "Две пары глаз должны встретиться в восьмом раунде.",
            characterNames: new[] { "Итачи", "Мадара" }),
        new("x_deeplist_weedwick", "Pet Project", "Любимый проект",
            "As DeepList or Weedwick, finish with both characters alive in the final top 3. Both players earn this achievement.",
            "Играя за DeepList или Weedwick, завершите матч так, чтобы оба были живы и находились в финальном топ-3. Достижение получат оба.",
            AchievementCategory.Interaction, "heart", "epic", isSecret: true,
            secretHint: "An unlikely pet project must reach the podium.",
            secretHintRu: "Необычный любимый проект должен добраться до пьедестала.",
            characterNames: new[] { "DeepList", "Weedwick" }),
        new("x_spartan_mylorik", "Mutual Respect", "Взаимное уважение",
            "As Загадочный Спартанец в маске, trigger the mutual-Psyche interaction with mylorik, then defeat him in a later fight.",
            "Играя за Загадочный Спартанец в маске, активируйте взаимное усиление Психики с mylorik, а затем победите его в более позднем бою.",
            AchievementCategory.Interaction, "handshake", "rare", isSecret: true,
            secretHint: "Respect must come before rivalry.",
            secretHintRu: "Уважение должно появиться раньше соперничества.",
            characterNames: new[] { "Загадочный Спартанец в маске", "mylorik" }),
        new("x_boys_madara", "Nothing Is Immune", "Нет неприкасаемых",
            "As TheBoys with СуперМудень, successfully deal Harm through Мадара’s Воскрешенное тело.",
            "Играя за TheBoys с СуперМудень, успешно нанесите Вред сквозь Воскрешенное тело Мадары.",
            AchievementCategory.Interaction, "swords", "legendary", isSecret: true,
            secretHint: "Even a resurrected body can meet something super.",
            secretHintRu: "Даже воскрешённое тело однажды встречает нечто суперское.",
            characterNames: new[] { "TheBoys", "Мадара" }),
        new("x_monster_witness", "I Saw the Beast", "Я видел Зверя",
            "As a non-pawn, attack Монстр без имени in round 10 and receive the Пейзаж конца света payout.",
            "Не будучи пешкой, атакуйте Монстр без имени в 10-м раунде и получите награду Пейзаж конца света.",
            AchievementCategory.Interaction, "eye", "rare", isSecret: true,
            secretHint: "Witness the end from too close.",
            secretHintRu: "Узрите конец света с опасно близкого расстояния.",
            characterNames: new[] { "Монстр без имени" }),
        new("x_doom_dragon", "How to Tame Your Dragon", "Как приручить дракона",
            "As DooM Guy, defeat Sirinoks after she becomes Дракон.",
            "Играя за DooM Guy, победите Sirinoks после её превращения в Дракон.",
            AchievementCategory.Interaction, "dragon", "legendary", isSecret: true,
            secretHint: "Hell has one flying demon with a friendship problem.",
            secretHintRu: "В аду нашёлся один летающий демон с проблемами в дружбе.",
            characterNames: new[] { "DooM Guy", "Sirinoks" }),
        new("x_rick_most_wanted", "Interdimensional Most Wanted", "Межпространственный розыск",
            "As Рик Санчез, fight Кира, Загадочный Спартанец в маске, and Weedwick in one match.",
            "Играя за Рик Санчез, сразитесь за один матч с Кира, Загадочный Спартанец в маске и Weedwick.",
            AchievementCategory.Interaction, "portal", "epic", isSecret: true,
            secretHint: "A scientist has three especially questionable appointments.",
            secretHintRu: "У одного учёного назначены три особенно сомнительные встречи.",
            characterNames: new[] { "Рик Санчез", "Кира", "Загадочный Спартанец в маске", "Weedwick" }),
        new("x_spartan_kratos", "Spartans Need No Introduction", "Спартанцам не нужны представления",
            "Trigger the special first-contact Psyche interaction between Загадочный Спартанец в маске and Кратос. Both players earn this achievement.",
            "Активируйте особое взаимодействие Психики при первой встрече Загадочный Спартанец в маске и Кратос. Достижение получат оба.",
            AchievementCategory.Interaction, "handshake", "rare", isSecret: true,
            secretHint: "Two Spartans recognize each other before the fighting starts.",
            secretHintRu: "Два спартанца узнают друг друга ещё до начала драки.",
            characterNames: new[] { "Загадочный Спартанец в маске", "Кратос" }),
        new("x_deeplist_octopus", "Eight Arms in the Plan", "Восемь щупалец по плану",
            "As DeepList, befriend Осьминожка through Сомнительная тактика and then trigger Стёб against her.",
            "Играя за DeepList, подружитесь с Осьминожка через Сомнительная тактика, а затем активируйте против неё Стёб.",
            AchievementCategory.Interaction, "jester", "epic", isSecret: true,
            secretHint: "The superior plan needs eight more hands and one punchline.",
            secretHintRu: "Великому плану не хватает восьми рук и одной шутки.",
            characterNames: new[] { "DeepList", "Осьминожка" }),
        new("x_goblin_bad_architecture", "Building Code Violation", "Нарушение строительных норм",
            "As Стая Гоблинов, survive after a Ziggurat learns Булькает.",
            "Играя за Стая Гоблинов, выживите после того, как Зиккурат изучит Булькает.",
            AchievementCategory.Interaction, "pyramid", "rare", isSecret: true,
            secretHint: "Some construction materials should not make that sound.",
            secretHintRu: "Некоторые стройматериалы не должны издавать такие звуки.",
            characterNames: new[] { "Стая Гоблинов", "Братишка", "mylorik" }),
        new("x_eren_goblins", "Tiny Titans", "Крошечные титаны",
            "As Эрен Йегер, take Стая Гоблинов in the Rumbling.",
            "Играя за Эрен Йегер, поглотите Стая Гоблинов с помощью Rumbling.",
            AchievementCategory.Interaction, "falling", "epic", isSecret: true,
            secretHint: "A very large march meets a very numerous little problem.",
            secretHintRu: "Очень большой марш встречает очень многочисленную маленькую проблему.",
            characterNames: new[] { "Эрен Йегер", "Стая Гоблинов" }),
        new("x_naruto_failed_heroes", "Heroes Don't Always Save the World",
            "Герои не всегда спасают мир",
            "",
            "",
            AchievementCategory.Interaction, "shield-cross", "legendary", isSecret: true,
            characterNames: new[] { Naruto.CharacterName, ErenYeager.CharacterName, Madara.CharacterName }),
        new("x_gordon_theboys", "There Is No Counter to a Crowbar but Another Crowbar!",
            "Против лома нет приема, кроме другого лома!",
            "The Hero with a Crowbar stopped the Super Crowbar.",
            "СуперКочергу остановил Герой с Кочергой",
            AchievementCategory.Interaction, "axe", "legendary", isSecret: true,
            characterNames: new[] { "TheBoys", "Гордон Фримен" }),
        new("x_homelander_omniman", "Поезд против самолета!", "Поезд против самолета!",
            "Примите участие в честном сражении Homelander vs Omni-man с равной справедливостью, и выясните кто из них сильнее!",
            "Примите участие в честном сражении Homelander vs Omni-man с равной справедливостью, и выясните кто из них сильнее!",
            AchievementCategory.Interaction, "balance", "rare",
            characterNames: new[] { Homelander.CharacterName, OmniMan.CharacterName }),
    };

    private static readonly Dictionary<string, AchievementDefinition> ById =
        AllAchievements.ToDictionary(achievement => achievement.Id);

    public static AchievementDefinition GetDefinition(string id) =>
        id != null && ById.TryGetValue(id, out var definition) ? definition : null;

    public static void EnsureInitialized(DiscordAccountClass account)
    {
        account.Achievements ??= new AchievementData();
        account.Achievements.Progress ??= new List<AchievementProgress>();
        account.Achievements.NewlyUnlocked ??= new List<string>();
    }

    /// <summary>
    /// Stores the best result from a single match. Unlocking is based only on this attempt, never
    /// on the persisted best from an earlier match, so separate partial runs cannot combine.
    /// </summary>
    public static bool SetBestProgress(
        DiscordAccountClass account,
        string achievementId,
        int value,
        bool conditionEligible = true)
    {
        var definition = GetDefinition(achievementId);
        if (definition == null || account == null || !conditionEligible) return false;

        lock (account)
        {
            EnsureInitialized(account);
            var progress = account.Achievements.Progress.Find(x => x.AchievementId == achievementId);
            if (progress == null)
            {
                progress = new AchievementProgress(achievementId);
                account.Achievements.Progress.Add(progress);
            }

            if (progress.IsUnlocked) return false;

            var attempt = Math.Clamp(value, 0, definition.Target);
            progress.Current = Math.Max(progress.Current, attempt);

            // Use this match's attempt, not progress.Current: the latter may come from another match.
            if (attempt < definition.Target) return false;

            progress.Current = definition.Target;
            progress.IsUnlocked = true;
            progress.UnlockedAt = DateTimeOffset.UtcNow;
            if (!account.Achievements.NewlyUnlocked.Contains(achievementId))
                account.Achievements.NewlyUnlocked.Add(achievementId);

            account.ZbsPoints += definition.RewardZbs;
            account.PendingLootBoxes += definition.RewardLootBoxes;
            return true;
        }
    }

    // Compatibility helpers remain monotonic and treat the supplied value as this match's attempt.
    public static bool SetProgress(DiscordAccountClass account, string achievementId, int value) =>
        SetBestProgress(account, achievementId, value);

    public static bool TryUnlock(DiscordAccountClass account, string achievementId, int progress = 1) =>
        SetBestProgress(account, achievementId, progress);

    // Score-derived decimals can legitimately exceed int.MaxValue (Halflife 3's P^P settlement,
    // and anything that inherits it, e.g. Итачи's stolen points) — clamp before the int cast (m49).
    private static int ToProgress(decimal value) =>
        (int)Math.Floor(Math.Clamp(value, 0, int.MaxValue));

    public static void TrackGameEnd(
        DiscordAccountClass account,
        GamePlayerBridgeClass player,
        GameClass game,
        int rewardPlace)
    {
        EnsureInitialized(account);
        var tracker = player.Passives.AchievementTracker ?? new InGameAchievementTracker();
        var characterName = player.GameCharacter.Name;
        var actualPlace = player.Status.GetPlaceAtLeaderBoard();
        var alive = !player.Passives.IsDead;
        var solo = game.Teams.Count == 0;
        var rewardWin = rewardPlace == 1 && alive;
        var isYoungGleb = (characterName is "Глеб" or "Молодой Глеб")
            && player.GameCharacter.Passive.Any(passive => passive.PassiveName == "Main Ирелия");
        var isMylorik = (characterName == "mylorik" || tracker.TransformedFromMylorik)
                        && !tracker.TransformedFromCthulhu;
        var isNativeShark = characterName == "Братишка"
                            && !tracker.TransformedFromMylorik
                            && !tracker.TransformedFromCthulhu;

        // Вечное Цукуеми projects a different final result to every non-Madara viewer. Evaluating
        // real-result achievements here would leak or contradict that private ending, so V2 is
        // deliberately suppressed for everyone except Madara's own hidden-ending achievement.
        if (Madara.IsEternalTsukuyomiActive(game))
        {
            if (Madara.IsMadara(player))
            {
                var state = player.Passives.Madara;
                SetBestProgress(account, "c_madara_tsukuyomi",
                    state.EternalTsukuyomiActive ? 1 : 0,
                    state.EternalTsukuyomiActive && !state.Sealed);
            }
            return;
        }

        // Global mechanics
        SetBestProgress(account, "g_bottom_feeder", tracker.BottomFeederWins > 0 ? 1 : 0);
        SetBestProgress(account, "g_class_advantage", tracker.NemesisAdvantageWins);
        SetBestProgress(account, "g_target_routine", tracker.TargetSkillRounds.Count);
        SetBestProgress(account, "g_maximum_sentence", tracker.WonFightWithMaxJustice ? 1 : 0);
        SetBestProgress(account, "g_three_drops", tracker.DropsCaused);
        SetBestProgress(account, "g_twenty_moral", tracker.MoralBankruptcyTriggered ? 1 : 0);
        SetBestProgress(account, "g_open_book", HasOpenBook(player, game) ? 1 : 0);
        SetBestProgress(account, "g_clean_sweep", tracker.DefeatedPlayerIds.Count, solo);
        SetBestProgress(account, "g_round10_comeback", tracker.OpenedRoundTenAtLast ? 1 : 0,
            solo && alive && actualPlace == 1);

        var undefeatedWins = tracker.TotalFightsLost == 0 ? tracker.TotalFightsWon : 0;
        SetBestProgress(account, "g_untouchable", undefeatedWins, solo && rewardWin);
        SetBestProgress(account, "g_quad_damage",
            ToProgress(tracker.RoundTenRegularPoints));
        SetBestProgress(account, "g_auto_pilot", HasUsedAutoMoveAllGame(player, tracker) ? 1 : 0);

        // Character stories
        if (characterName == "TheBoys")
        {
            SetBestProgress(account, "c_boys_orders", player.Passives.TheBoysFrancie.OrdersCompleted);

            var virusVictims = game.PlayersList.Count(candidate =>
                candidate.Passives.TheBoysVirus
                && candidate.Passives.TheBoysVirusSource == player.GetPlayerId());
            var ultimateProven = virusVictims >= 3
                                 || player.Passives.TheBoysButcher.SuperDickActive
                                 && tracker.SuperDickDropsCaused >= 5
                                 || player.Passives.TheBoysKimiko.LivingWeapon
                                 && tracker.LivingWeaponJusticeBlocked >= 5
                                 || player.Passives.TheBoysMM.UpgradeLevel >= 4
                                 && player.Passives.TheBoysMM.KompromatTargets.Distinct().Count() >= 3;
            SetBestProgress(account, "c_boys_ultimate", ultimateProven ? 1 : 0);
        }

        if (characterName == "Стая Гоблинов")
        {
            var zig = player.Passives.GoblinZiggurat;
            var builtPositions = zig.BuiltPositions.Distinct().ToList();
            SetBestProgress(account, "c_goblin_architect", builtPositions.Count);

            // The card requires *receiving* the enforced win, not merely owning a place-1 Ziggurat
            // while winning on score: only HandleLastRound's overtake sets EnforcedWinTriggered.
            // Cthulhu/Jon Snow can still displace the Goblins after it, so the place is re-checked (m57).
            SetBestProgress(account, "c_goblin_summit", zig.EnforcedWinTriggered ? 1 : 0,
                zig.EnforcedWinTriggered && rewardWin && actualPlace == 1);
        }

        if (characterName == "Рик Санчез")
        {
            SetBestProgress(account, "c_rick_beans", player.Passives.RickGiantBeans.BeanStacks);
            SetBestProgress(account, "c_rick_portals", tracker.PortalGunFires);
        }

        if (characterName == "Сайтама")
        {
            var deferred = Math.Max(tracker.SaitamaDeferredPoints,
                player.Passives.SaitamaUnnoticed.GetTotalDeferred());
            SetBestProgress(account, "c_saitama_serious", deferred);
            SetBestProgress(account, "c_saitama_one_punch", tracker.SaitamaDeferredPoints);
        }

        if (characterName == Madara.CharacterName)
        {
            var state = player.Passives.Madara;
            SetBestProgress(account, "c_madara_round_eight", state.RoundEightWins);
            SetBestProgress(account, "c_madara_tsukuyomi",
                state.EternalTsukuyomiActive ? 1 : 0,
                state.EternalTsukuyomiActive && !state.Sealed);
        }

        if (characterName == "Тигр")
        {
            var completedThreeZero = player.Passives.TigrThreeZeroList.FriendList.Count(entry => !entry.IsUnique);
            SetBestProgress(account, "c_tigr_three_zero", completedThreeZero);
            SetBestProgress(account, "c_tigr_six_zero", completedThreeZero);
        }

        if (characterName == "Итачи")
        {
            SetBestProgress(account, "c_itachi_crows",
                player.Passives.ItachiCrows.CrowCounts.Count(entry => entry.Value > 0));
            SetBestProgress(account, "c_itachi_tax",
                ToProgress(player.Passives.ItachiTsukuyomi.TotalStolenPoints));
        }

        if (characterName == "Кратос")
        {
            SetBestProgress(account, "c_kratos_rampage", tracker.KratosEventVictimIds.Count);
            SetBestProgress(account, "c_kratos_olympus", tracker.KratosEventVictimIds.Count);
        }

        if (characterName == "Кира")
        {
            var successfulVictims = player.Passives.KiraDeathNote.Entries
                .Where(entry => entry.WasCorrect)
                .Select(entry => entry.TargetPlayerId)
                .Distinct()
                .Count();
            SetBestProgress(account, "c_kira_first_name", successfulVictims);
            SetBestProgress(account, "c_kira_perfect_crime", successfulVictims);
        }

        if (characterName == "Монстр без имени")
        {
            SetBestProgress(account, "c_monster_no_escape", tracker.MonsterPawnExecutions);
            SetBestProgress(account, "c_monster_apocalypse", tracker.MonsterPawnExecutions);
        }

        if (characterName == "Продавец Сомнительных Тактик")
        {
            var markedPlayers = player.Passives.SellerVparitGovna.MarkedPlayers.Distinct().Count();
            SetBestProgress(account, "c_seller_marks", markedPlayers);
            SetBestProgress(account, "c_seller_market", markedPlayers,
                markedPlayers >= 5 && player.Passives.SellerSecretBuild.AccumulatedSkill >= 100);
        }

        if (characterName == "Dopa")
        {
            SetBestProgress(account, "c_dopa_foresight", tracker.DopaVisionProcs);
            SetBestProgress(account, "c_dopa_big_brain", tracker.DopaVisionProcs);
            SetBestProgress(account, "c_dopa_permaban",
                player.Passives.DopaPermabanTriggered ? 1 : 0);
        }

        if (characterName == "Salldorum")
        {
            SetBestProgress(account, "c_salldorum_cola",
                player.Passives.SalldorumTimeCapsule.DrinkCount);
            SetBestProgress(account, "c_salldorum_double_cola",
                player.Passives.SalldorumTimeCapsule.DrinkCount);
        }

        if (characterName == "Геральт")
        {
            SetBestProgress(account, "c_geralt_contracts", tracker.GeraltContractFightsResolved);
            SetBestProgress(account, "c_geralt_path",
                player.Passives.GeraltMeditation.RevealedEnemies.Distinct().Count());
        }

        if (characterName == "Котики")
        {
            SetBestProgress(account, "c_kotiki_one_back", tracker.KotikiCatsReclaimed.Count);
            SetBestProgress(account, "c_kotiki_reunion", tracker.KotikiCatsReclaimed.Count);
        }

        if (characterName == "Toxic Mate")
        {
            var cancerReturned = tracker.ToxicCancerReturns > 0;
            SetBestProgress(account, "c_toxic_chain", cancerReturned ? 1 : 0,
                cancerReturned && tracker.ToxicMaxTransferCount >= 2);
            SetBestProgress(account, "c_toxic_return", cancerReturned ? 1 : 0,
                cancerReturned && tracker.ToxicMaxTransferCount >= 5);
        }

        if (characterName == "Napoleon Wonnafcuk")
        {
            SetBestProgress(account, "c_napoleon_alliance",
                player.Passives.NapoleonFirstFightList.FriendList.Distinct().Count());

            var nonTeamEnemyIds = game.PlayersList
                .Where(candidate => candidate.GetPlayerId() != player.GetPlayerId()
                                    && !player.IsTeamMember(game, candidate.GetPlayerId()))
                .Select(candidate => candidate.GetPlayerId())
                .ToHashSet();
            var defeatedNonTeamEnemies = tracker.DefeatedPlayerIds.Count(nonTeamEnemyIds.Contains);
            var formedAlliance = player.Passives.NapoleonAlliance.AllyId != Guid.Empty;
            SetBestProgress(account, "c_napoleon_treaties", defeatedNonTeamEnemies,
                game.Teams.Count > 0 && nonTeamEnemyIds.Count == 4 && formedAlliance);
        }

        if (characterName == "Таинственный Суппорт")
        {
            var carry = game.PlayersList.Find(candidate =>
                candidate.GetPlayerId() == player.Passives.SupportPremade.MarkedPlayerId);
            var carryAlive = carry is { Passives.IsDead: false };
            var carryPlace = carryAlive ? carry.Status.GetPlaceAtLeaderBoard() : 0;
            var topThreePair = alive && actualPlace <= 3 && carryAlive && carryPlace <= 3;
            SetBestProgress(account, "c_support_buff", topThreePair ? 1 : 0);
            SetBestProgress(account, "c_support_premade",
                alive && actualPlace == 1 && carryAlive && carryPlace == 2 ? 1 : 0);
        }

        if (characterName == "Осьминожка" && !tracker.TransformedFromCthulhu)
        {
            var visitedPlaces = player.Passives.OctopusTentaclesList.LeaderboardPlace.Distinct().Count();
            SetBestProgress(account, "c_octopus_tour", visitedPlaces);
            SetBestProgress(account, "c_octopus_ink", visitedPlaces);
        }

        if (characterName == "DeepList")
        {
            var mockeryTriggers = player.Passives.DeepListMockeryList.WhoWonTimes
                .Where(entry => entry.Triggered)
                .Select(entry => entry.EnemyPlayerId)
                .Distinct()
                .Count();
            SetBestProgress(account, "c_deeplist_mockery", mockeryTriggers);
            SetBestProgress(account, "c_deeplist_roast", mockeryTriggers);
        }

        if (isMylorik)
        {
            var completedRevenge = player.Passives.MylorikRevenge.EnemyListPlayerIds
                .Where(entry => !entry.IsUnique)
                .Select(entry => entry.EnemyPlayerId)
                .Distinct()
                .Count();
            SetBestProgress(account, "c_mylorik_revenge", completedRevenge);
            SetBestProgress(account, "c_mylorik_grudges", completedRevenge);
        }

        if (characterName == "Глеб" && !isYoungGleb)
        {
            SetBestProgress(account, "c_gleb_return",
                player.Passives.GlebSkipFriendListDone.FriendList.Distinct().Count());
            var roundTenChallenger = player.Passives.GlebChallengerTriggeredWhen.WhenToTrigger.Contains(10);
            SetBestProgress(account, "c_gleb_challenger", roundTenChallenger ? 1 : 0,
                roundTenChallenger && tracker.RoundTenRegularPoints >= 24);
        }

        if (characterName == "LeCrisp")
        {
            SetBestProgress(account, "c_lecrisp_impact", player.Passives.LeCrispImpact.ImpactTimes);
            SetBestProgress(account, "c_lecrisp_legend", player.Passives.LeCrispImpact.ImpactTimes,
                tracker.TotalFightsWon >= 3);
        }

        if (characterName == "Толя")
        {
            var countTargets = player.Passives.TolyaCount.TargetList
                .Select(entry => entry.Target)
                .Distinct()
                .Count();
            SetBestProgress(account, "c_tolya_rammus", countTargets);
            SetBestProgress(account, "c_tolya_accounting", countTargets,
                alive && actualPlace == 1);
        }

        if (characterName == "HardKitty")
        {
            var attackHistory = player.Passives.HardKittyLoneliness.AttackHistory;
            var distinctWriters = attackHistory.Select(entry => entry.EnemyId).Distinct().Count();
            var letters = attackHistory.Sum(entry => entry.Times);
            SetBestProgress(account, "c_hardkitty_letters", distinctWriters);
            SetBestProgress(account, "c_hardkitty_love", letters, distinctWriters >= 5);
        }

        if (characterName == "Sirinoks")
        {
            var friends = player.Passives.SirinoksFriendsList.FriendList.Distinct().Count();
            SetBestProgress(account, "c_sirinoks_friends", friends);
            SetBestProgress(account, "c_sirinoks_dragon", friends,
                alive && actualPlace == 1);
        }

        if (characterName == "Злой Школьник")
        {
            var training = player.Passives.MitsukiGarbageList.Training;
            SetBestProgress(account, "c_mitsuki_loud",
                training.Select(entry => entry.EnemyId).Distinct().Count());
            SetBestProgress(account, "c_mitsuki_garbage",
                training.Where(entry => entry.Times >= 2).Select(entry => entry.EnemyId).Distinct().Count());
        }

        if (characterName == "AWDKA")
        {
            var repeatedAttempts = player.Passives.AwdkaTryingList.TryingList
                .Where(entry => entry.Times >= 2)
                .Select(entry => entry.EnemyPlayerId)
                .Distinct()
                .Count();
            SetBestProgress(account, "c_awdka_trying", repeatedAttempts);
            SetBestProgress(account, "c_awdka_mastery", repeatedAttempts);
        }

        if (characterName == "Darksci")
        {
            SetBestProgress(account, "c_darksci_stable",
                player.Passives.DarksciLuckyList.Triggered ? 1 : 0);
            var unstableLucky = player.Passives.DarksciTypeList.Triggered
                                && !player.Passives.DarksciTypeList.IsStableType
                                && player.Passives.DarksciLuckyList.Triggered;
            SetBestProgress(account, "c_darksci_unstable", unstableLucky ? 1 : 0,
                unstableLucky && alive && actualPlace == 1);
        }

        if (isNativeShark)
        {
            var jawsVictims = player.Passives.SharkJawsWin.FriendList.Distinct().Count();
            var trackedPlaces = player.Passives.SharkJawsLeader.FriendList.Distinct().Count();
            SetBestProgress(account, "c_shark_teeth", jawsVictims);
            SetBestProgress(account, "c_shark_apex", jawsVictims + trackedPlaces);
        }

        if (characterName == "Загадочный Спартанец в маске")
        {
            var shamed = player.Passives.SpartanShame.FriendList.Distinct().Count();
            SetBestProgress(account, "c_spartan_shame", shamed);
            SetBestProgress(account, "c_spartan_warrior", tracker.DefeatedPlayerIds.Count,
                shamed >= 5);
        }

        if (characterName == "Вампур")
        {
            var activeBites = player.Passives.VampyrHematophagiaList.HematophagiaCurrent
                .Select(entry => entry.EnemyId)
                .Distinct()
                .Count();
            SetBestProgress(account, "c_vampyr_bites", activeBites);
            SetBestProgress(account, "c_vampyr_feast", activeBites, alive);
        }

        if (characterName == "Краборак" && !tracker.TransformedFromCthulhu)
        {
            var shellFriends = player.Passives.CraboRackShell.FriendList.Distinct().Count();
            SetBestProgress(account, "c_crab_shell", shellFriends);
            SetBestProgress(account, "c_crab_fortress", shellFriends,
                tracker.DefeatedPlayerIds.Count >= 3);
        }

        if (characterName == "Weedwick")
        {
            SetBestProgress(account, "c_weedwick_smoke", tracker.WeedHarvested);
            SetBestProgress(account, "c_weedwick_harvest", tracker.WeedHarvested);
        }

        if (isYoungGleb)
        {
            SetBestProgress(account, "c_young_gleb_meta", tracker.YoungGlebPinkWardUsed ? 1 : 0);
            var sevenAcrossTheBoard = player.GameCharacter.GetIntelligence() >= 7
                                      && player.GameCharacter.GetStrength() >= 7
                                      && player.GameCharacter.GetSpeed() >= 7
                                      && player.GameCharacter.GetPsyche() >= 7;
            SetBestProgress(account, "c_young_gleb_ward", sevenAcrossTheBoard ? 1 : 0,
                alive && actualPlace == 1 && sevenAcrossTheBoard);
        }

        if (characterName == "Sakura")
        {
            SetBestProgress(account, "c_sakura_three", alive && actualPlace <= 3 ? 1 : 0);
            SetBestProgress(account, "c_sakura_first",
                alive && actualPlace == 1 && tracker.TotalFightsWon >= 5 ? 1 : 0);
        }

        if (characterName == DoomGuy.CharacterName)
        {
            var state = player.Passives.DoomGuy;
            SetBestProgress(account, "c_doom_loadout",
                state.RollMode && state.ActiveModules.Count >= DoomGuy.StageOrder.Length ? 1 : 0);
            SetBestProgress(account, "c_doom_bfg", tracker.BfgWaveVictimIds.Count);
        }

        if (characterName == ErenYeager.CharacterName)
        {
            SetBestProgress(account, "c_eren_tatake", player.Passives.Eren.TatakeSoundSerial);
            SetBestProgress(account, "c_eren_rumbling", tracker.RumblingVictimIds.Count);
        }

        if (characterName == Naruto.CharacterName && !player.Passives.Naruto.IsClone)
        {
            SetBestProgress(account, "c_naruto_harem", player.Passives.Naruto.HaremDonationsReceived);
            SetBestProgress(account, "c_naruto_rasengan",
                ToProgress(player.Passives.Naruto.ShadowPointsTransferred),
                alive && actualPlace == 1);
            SetBestProgress(account, "x_naruto_failed_heroes",
                Naruto.HeroesFailedToSaveWorld(player, game) ? 1 : 0);
        }

        if (characterName == "Гордон Фримен")
        {
            SetBestProgress(account, "c_gordon_rescue", tracker.GordonHeadcrabsRemoved);
            SetBestProgress(account, "c_gordon_halflife3", tracker.GordonHalfLifeReleased ? 1 : 0);
        }

        if (characterName == JonSnow.CharacterName)
        {
            SetBestProgress(account, "c_jon_king",
                player.GameCharacter.JonSnowBecameKing ? 1 : 0);
            SetBestProgress(account, "c_jon_watch",
                player.Passives.JonSnow.WatchEnded ? 1 : 0,
                player.Passives.JonSnow.WatchEnded && alive && actualPlace <= 3);
        }

        // Secret interactions
        if (characterName == "Загадочный Спартанец в маске")
        {
            SetBestProgress(account, "x_spartan_dragon", tracker.SpartanDragonSlayerDefeated ? 1 : 0);
            SetBestProgress(account, "x_spartan_mylorik",
                tracker.SpartanDefeatedMylorikAfterRespect ? 1 : 0);
        }

        if (characterName == "Кратос")
            SetBestProgress(account, "x_kira_kratos", player.Passives.KratosGodSlayerUsed ? 1 : 0);

        if (characterName == "Итачи")
            SetBestProgress(account, "x_itachi_madara",
                tracker.ItachiMadaraCloneAttackGranted ? 1 : 0);

        if (characterName is "DeepList" or "Weedwick")
        {
            var partnerName = characterName == "DeepList" ? "Weedwick" : "DeepList";
            var partner = game.PlayersList.Find(candidate => candidate.GameCharacter.Name == partnerName);
            var bothOnPodium = alive && actualPlace <= 3 && partner is
            {
                Passives.IsDead: false
            } && partner.Status.GetPlaceAtLeaderBoard() <= 3;
            SetBestProgress(account, "x_deeplist_weedwick", bothOnPodium ? 1 : 0);
        }

        if (characterName == "TheBoys")
            SetBestProgress(account, "x_boys_madara",
                tracker.DamagedReanimatedMadaraWithSuperDick ? 1 : 0);

        SetBestProgress(account, "x_monster_witness",
            tracker.WitnessedMonsterApocalypse ? 1 : 0);

        if (characterName == DoomGuy.CharacterName)
            SetBestProgress(account, "x_doom_dragon",
                player.Passives.DoomGuy.DefeatedDragonSirinoks ? 1 : 0);

        if (characterName == "Рик Санчез")
        {
            var mostWanted = new[] { "Кира", "Загадочный Спартанец в маске", "Weedwick" };
            SetBestProgress(account, "x_rick_most_wanted",
                mostWanted.All(tracker.FoughtCharacterNames.Contains) ? 1 : 0);
        }

        if (characterName is "Загадочный Спартанец в маске" or "Кратос")
        {
            var spartan = game.PlayersList.Find(candidate =>
                candidate.GameCharacter.Name == "Загадочный Спартанец в маске");
            var kratos = game.PlayersList.Find(candidate => candidate.GameCharacter.Name == "Кратос");
            var recognizedEachOther = spartan != null && kratos != null
                && spartan.Passives.SpartanShame.FriendList.Contains(kratos.GetPlayerId());
            SetBestProgress(account, "x_spartan_kratos", recognizedEachOther ? 1 : 0);
        }

        if (characterName == "DeepList")
        {
            var octopus = game.PlayersList.Find(candidate => candidate.GameCharacter.Name == "Осьминожка");
            var metOctopus = octopus != null
                && player.Passives.DeepListDoubtfulTactic.FriendList.Contains(octopus.GetPlayerId())
                && player.Passives.DeepListMockeryList.WhoWonTimes.Any(entry =>
                    entry.EnemyPlayerId == octopus.GetPlayerId() && entry.Triggered);
            SetBestProgress(account, "x_deeplist_octopus", metOctopus ? 1 : 0);
        }

        if (characterName == "Стая Гоблинов")
        {
            var badArchitecture = alive
                && player.Passives.GoblinZiggurat.LearnedPassives.Contains("Булькает");
            SetBestProgress(account, "x_goblin_bad_architecture", badArchitecture ? 1 : 0);
        }

        if (characterName == ErenYeager.CharacterName)
        {
            var goblins = game.PlayersList.Find(candidate => candidate.GameCharacter.Name == "Стая Гоблинов");
            SetBestProgress(account, "x_eren_goblins",
                goblins != null && tracker.RumblingVictimIds.Contains(goblins.GetPlayerId()) ? 1 : 0);
        }

        if (characterName == "Гордон Фримен")
            SetBestProgress(account, "x_gordon_theboys",
                tracker.GordonCrowbarStoppedSuperDick ? 1 : 0);

        if (characterName is Homelander.CharacterName or OmniMan.CharacterName)
            SetBestProgress(account, "x_homelander_omniman",
                tracker.HomelanderOmniManEqualJusticeFight ? 1 : 0);
    }

    private static bool HasUsedAutoMoveAllGame(
        GamePlayerBridgeClass player,
        InGameAchievementTracker tracker)
    {
        IEnumerable<int> requiredRounds = player.GameCharacter.Name switch
        {
            "Тигр" => Enumerable.Range(1, 9),
            _ when Madara.IsMadara(player) => new[] { 1, 2, 3, 4, 5, 6, 7, 9, 10 },
            _ => Enumerable.Range(1, 10),
        };

        return requiredRounds.All(tracker.ExplicitAutoMoveRounds.Contains);
    }

    private static bool HasOpenBook(GamePlayerBridgeClass player, GameClass game)
    {
        // Kira uses the Death Note, Madara cannot predict, Let's Roll clears predictions, and admins
        // receive automatic answers. None are legitimate Open Book attempts.
        if (player.PlayerType == 2
            || player.GameCharacter.DoomRollMode
            || Madara.IsMadara(player)
            || player.GameCharacter.Passive.Any(passive =>
                passive.PassiveName is "Тетрадь смерти" or "AdminPlayerType"))
            return false;

        var eligibleTargets = game.PlayersList.Where(target =>
                target.GetPlayerId() != player.GetPlayerId()
                && target.PlayerType != 2
                && target.GameCharacter.Tier >= 0
                && !target.GameCharacter.Passive.Any(passive =>
                    passive.PassiveName is "Выдуманный персонаж" or "AdminPlayerType"))
            .ToList();

        return eligibleTargets.Count >= 3 && eligibleTargets.All(target =>
            player.Predict.Any(prediction =>
                prediction.PlayerId == target.GetPlayerId()
                && string.Equals(prediction.CharacterName, target.GameCharacter.Name,
                    StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Creates a detached account-achievement snapshot for finished-game DTO mapping. The caller
    /// must hold the account monitor while reading the persistent source.
    /// </summary>
    public static AchievementData CreateSnapshot(AchievementData source)
    {
        if (source == null) return new AchievementData();

        return new AchievementData
        {
            Progress = source.Progress?.Select(progress => new AchievementProgress
            {
                AchievementId = progress.AchievementId,
                Current = progress.Current,
                IsUnlocked = progress.IsUnlocked,
                UnlockedAt = progress.UnlockedAt,
            }).ToList() ?? new List<AchievementProgress>(),
            NewlyUnlocked = source.NewlyUnlocked?.ToList() ?? new List<string>(),
        };
    }
}
