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
    public bool MoralBankruptcyTriggered { get; set; }
    public bool OpenedRoundTenAtLast { get; set; }
    public decimal RoundTenRegularPoints { get; set; }

    public int PortalGunFires { get; set; }
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
    public int ExploitsFired { get; set; }
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
            AchievementCategory.Global, "rocket", "uncommon"),
        new("g_class_advantage", "Rock, Paper, Crown", "Камень, ножницы, корона",
            "Win 3 resolved fights while holding the Nemesis class advantage, on attack or defence.",
            "Победите в 3 состоявшихся боях, имея преимущество анти-класса — в атаке или защите.",
            AchievementCategory.Global, "crown", "rare", 3),
        new("g_target_routine", "Bullseye", "В яблочко",
            "Gain Main Skill from Мишень in 3 different rounds.",
            "Получите основной Скилл от Мишени в 3 разных раундах.",
            AchievementCategory.Global, "target", "common", 3),
        new("g_maximum_sentence", "Maximum Sentence", "Высшая мера",
            "Win a resolved fight while holding 5 live Justice.",
            "Победите в состоявшемся бою, имея 5 действующей Справедливости.",
            AchievementCategory.Global, "balance", "uncommon"),
        new("g_three_drops", "Down the Chute", "Вниз по жёлобу",
            "Personally cause 3 Drops in one match.",
            "Лично вызовите 3 Скидывания за один матч.",
            AchievementCategory.Global, "falling", "rare", 3),
        new("g_twenty_moral", "Moral Bankruptcy", "Моральное банкротство",
            "Convert 20 Moral into 10 bonus points in a single exchange.",
            "Одним обменом превратите 20 Морали в 10 бонусных очков.",
            AchievementCategory.Global, "heart", "uncommon"),
        new("g_open_book", "Open Book", "Открытая книга",
            "Correctly predict every eligible opponent, with at least 3 eligible targets.",
            "Верно предскажите каждого доступного соперника; доступных целей должно быть не меньше 3.",
            AchievementCategory.Global, "eye", "rare"),
        new("g_clean_sweep", "Garbage Collector", "Сборщик мусора",
            "In solo mode, defeat all 5 opponents at least once.",
            "В одиночном режиме победите каждого из 5 соперников хотя бы раз.",
            AchievementCategory.Global, "swords", "rare", 5),
        new("g_round10_comeback", "From Sixth to King", "Из шестого — в короли",
            "Open round 10 in 6th place, then finish alive in 1st place in solo mode.",
            "Начните 10-й раунд на 6-м месте, а завершите одиночный матч живым лидером.",
            AchievementCategory.Global, "crown", "epic"),
        new("g_untouchable", "Untouchable", "Неприкасаемый",
            "Win a solo match with at least 5 resolved wins and no resolved losses.",
            "Победите в одиночном матче, выиграв не меньше 5 состоявшихся боёв и не проиграв ни одного.",
            AchievementCategory.Global, "shield", "epic", 5),
        new("g_quad_damage", "Quad Damage", "Четверной урон",
            "Receive at least 20 net regular points from round 10 after the real multiplier.",
            "Получите не меньше 20 чистых обычных очков за 10-й раунд после реального множителя.",
            AchievementCategory.Global, "bolt", "rare", 20),

        // Character stories
        new("c_boys_orders", "French Connection", "Французская связь",
            "As TheBoys, complete all 3 Francie orders.",
            "Играя за TheBoys, выполните все 3 заказа Francie.",
            AchievementCategory.Character, "badge", "uncommon", 3, characterNames: new[] { "TheBoys" }),
        new("c_goblin_summit", "Built Different", "Особая постройка",
            "As Стая Гоблинов, finish with a Ziggurat at place 1 and receive its enforced win.",
            "Играя за Стая Гоблинов, завершите матч с Зиккуратом на 1-м месте и получите его гарантированную победу.",
            AchievementCategory.Character, "pyramid", "legendary", characterNames: new[] { "Стая Гоблинов" }),
        new("c_rick_portals", "Portal Authority", "Портальная власть",
            "As Рик Санчез, successfully fire Портальная пушка twice in one match.",
            "Играя за Рик Санчез, дважды успешно примените Портальная пушка за матч.",
            AchievementCategory.Character, "portal", "rare", 2, characterNames: new[] { "Рик Санчез" }),
        new("c_saitama_one_punch", "One Punch", "Один удар",
            "As Сайтама, reclaim at least 20 deferred points through Ищет достойного противника.",
            "Играя за Сайтама, верните не меньше 20 отложенных очков через Ищет достойного противника.",
            AchievementCategory.Character, "fist", "epic", 20, characterNames: new[] { "Сайтама" }),
        new("c_madara_tsukuyomi", "Wake Up to Reality", "Очнись и вернись в реальность",
            "As Мадара, finish with Вечное Цукуеми active and without being sealed.",
            "Играя за Мадара, завершите матч с активным Вечное Цукуеми и не будьте запечатаны.",
            AchievementCategory.Character, "eye-glow", "legendary", isSecret: true,
            secretHint: "Let the whole world sleep without allowing it to seal you away.",
            secretHintRu: "Погрузите весь мир в сон и не позвольте ему запечатать вас.",
            characterNames: new[] { "Мадара" }),
        new("c_tigr_six_zero", "Six–Zero", "Шесть — ноль",
            "As Тигр, complete 3-0 обоссан against 2 different enemies.",
            "Играя за Тигр, завершите 3-0 обоссан против 2 разных соперников.",
            AchievementCategory.Character, "trophy", "epic", 2, characterNames: new[] { "Тигр" }),
        new("c_itachi_tax", "Tax Collector", "Сборщик налогов",
            "As Итачи, copy at least 20 total points through Глаза Итачи.",
            "Играя за Итачи, скопируйте не меньше 20 очков через Глаза Итачи.",
            AchievementCategory.Character, "eye", "rare", 20, characterNames: new[] { "Итачи" }),
        new("c_kratos_olympus", "Ghost of Sparta", "Призрак Спарты",
            "As Кратос, personally kill all 5 enemies during Возвращение из мертвых.",
            "Играя за Кратос, лично убейте всех 5 врагов во время Возвращение из мертвых.",
            AchievementCategory.Character, "swords", "legendary", 5, characterNames: new[] { "Кратос" }),
        new("c_kira_perfect_crime", "Perfect Crime", "Идеальное преступление",
            "As Кира, record 3 successful Тетрадь смерти kills on different victims.",
            "Играя за Кира, совершите 3 успешных убийства разных жертв через Тетрадь смерти.",
            AchievementCategory.Character, "notebook", "epic", 3, characterNames: new[] { "Кира" }),
        new("c_monster_apocalypse", "Beautiful Apocalypse", "Прекрасный апокалипсис",
            "As Монстр без имени, execute at least 2 pawns through Пейзаж конца света.",
            "Играя за Монстр без имени, казните не меньше 2 пешек через Пейзаж конца света.",
            AchievementCategory.Character, "radiation", "epic", 2, characterNames: new[] { "Монстр без имени" }),
        new("c_geralt_contracts", "Witcher’s Payday", "Ведьмачья получка",
            "As Геральт, resolve 3 contract fights in one match.",
            "Играя за Геральт, завершите 3 контрактных боя за один матч.",
            AchievementCategory.Character, "medallion", "uncommon", 3, characterNames: new[] { "Геральт" }),
        new("c_kotiki_reunion", "The Cats Came Back", "Котики вернулись",
            "As Котики, reclaim both Минька and Штормяк by winning their return attacks.",
            "Играя за Котики, верните и Минька, и Штормяк, победив в обеих атаках за возвращение.",
            AchievementCategory.Character, "cat", "rare", 2, characterNames: new[] { "Котики" }),
        new("c_darksci_unstable", "Against All Odds", "Вопреки всему",
            "As Darksci, choose unstable, trigger Повезло, and finish in 1st place.",
            "Играя за Darksci, выберите нестабильность, активируйте Повезло и завершите матч на 1-м месте.",
            AchievementCategory.Character, "dice-six", "epic", characterNames: new[] { "Darksci" }),
        new("c_eren_rumbling", "The Rumbling", "Гул Земли",
            "As Эрен Йегер, kill at least 2 players with Rumbling.",
            "Играя за Эрен Йегер, убейте не меньше 2 игроков с помощью Rumbling.",
            AchievementCategory.Character, "falling", "epic", 2, characterNames: new[] { "Эрен Йегер" }),
        new("c_doom_bfg", "BFG Division", "Дивизия BFG",
            "As DooM Guy, defeat at least 3 players in one BFG wave, including its primary target.",
            "Играя за DooM Guy, победите не меньше 3 игроков одной волной BFG, включая основную цель.",
            AchievementCategory.Character, "bolt", "epic", 3, characterNames: new[] { "DooM Guy" }),

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
            "Играя за Загадочный Спартанец в маске, активируйте взаимное усиление Психики с mylorik, а затем победите его в следующем бою.",
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
            (int)Math.Floor(Math.Max(0, tracker.RoundTenRegularPoints)));

        // Character stories
        if (characterName == "TheBoys")
            SetBestProgress(account, "c_boys_orders", player.Passives.TheBoysFrancie.OrdersCompleted);

        if (characterName == "Стая Гоблинов")
        {
            var summit = player.Passives.GoblinZiggurat.BuiltPositions.Contains(1);
            SetBestProgress(account, "c_goblin_summit", summit ? 1 : 0,
                summit && rewardWin && actualPlace == 1);
        }

        if (characterName == "Рик Санчез")
            SetBestProgress(account, "c_rick_portals", tracker.PortalGunFires);

        if (characterName == "Сайтама")
            SetBestProgress(account, "c_saitama_one_punch", tracker.SaitamaDeferredPoints);

        if (characterName == Madara.CharacterName)
        {
            var state = player.Passives.Madara;
            SetBestProgress(account, "c_madara_tsukuyomi",
                state.EternalTsukuyomiActive ? 1 : 0,
                state.EternalTsukuyomiActive && !state.Sealed);
        }

        if (characterName == "Тигр")
            SetBestProgress(account, "c_tigr_six_zero",
                player.Passives.TigrThreeZeroList.FriendList.Count(entry => !entry.IsUnique));

        if (characterName == "Итачи")
            SetBestProgress(account, "c_itachi_tax",
                (int)Math.Floor(player.Passives.ItachiTsukuyomi.TotalStolenPoints));

        if (characterName == "Кратос")
            SetBestProgress(account, "c_kratos_olympus", tracker.KratosEventVictimIds.Count);

        if (characterName == "Кира")
        {
            var successfulVictims = player.Passives.KiraDeathNote.Entries
                .Where(entry => entry.WasCorrect)
                .Select(entry => entry.TargetPlayerId)
                .Distinct()
                .Count();
            SetBestProgress(account, "c_kira_perfect_crime", successfulVictims);
        }

        if (characterName == "Монстр без имени")
            SetBestProgress(account, "c_monster_apocalypse", tracker.MonsterPawnExecutions);

        if (characterName == "Геральт")
            SetBestProgress(account, "c_geralt_contracts", tracker.GeraltContractFightsResolved);

        if (characterName == "Котики")
            SetBestProgress(account, "c_kotiki_reunion", tracker.KotikiCatsReclaimed.Count);

        if (characterName == "Darksci")
        {
            var unstableLucky = player.Passives.DarksciTypeList.Triggered
                                && !player.Passives.DarksciTypeList.IsStableType
                                && player.Passives.DarksciLuckyList.Triggered;
            SetBestProgress(account, "c_darksci_unstable", unstableLucky ? 1 : 0,
                unstableLucky && alive && actualPlace == 1);
        }

        if (characterName == ErenYeager.CharacterName)
            SetBestProgress(account, "c_eren_rumbling", tracker.RumblingVictimIds.Count);

        if (characterName == DoomGuy.CharacterName)
            SetBestProgress(account, "c_doom_bfg", tracker.BfgWaveVictimIds.Count);

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
}
