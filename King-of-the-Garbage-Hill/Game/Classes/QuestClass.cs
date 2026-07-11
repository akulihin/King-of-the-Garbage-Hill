using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Classes;

public enum QuestLane
{
    Anchor,
    Skirmish,
    Ambition,
}

public enum QuestAggregation
{
    DailySum,
    BestMatch,
}

public class QuestDefinition
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public string Description { get; set; }
    public string DescriptionRu { get; set; }
    public QuestLane Lane { get; set; }
    public string Icon { get; set; }
    public QuestAggregation Aggregation { get; set; }
    public int Target { get; set; }
    public int ZbsReward { get; set; }
    public int LootBoxReward { get; set; }
    public Func<DailyQuestMetrics, int> MetricValue { get; set; }

    public QuestDefinition(
        string id,
        string name,
        string nameRu,
        string description,
        string descriptionRu,
        QuestLane lane,
        string icon,
        QuestAggregation aggregation,
        int target,
        int zbsReward,
        Func<DailyQuestMetrics, int> metricValue,
        int lootBoxReward = 0)
    {
        Id = id;
        Name = name;
        NameRu = nameRu;
        Description = description;
        DescriptionRu = descriptionRu;
        Lane = lane;
        Icon = icon;
        Aggregation = aggregation;
        Target = target;
        ZbsReward = zbsReward;
        LootBoxReward = lootBoxReward;
        MetricValue = metricValue;
    }
}

public class QuestProgress
{
    public string QuestId { get; set; }
    // Retained as a persisted snapshot for compatibility with cached/legacy clients. Evaluation
    // always uses QuestId against the server-owned catalog.
    public string Description { get; set; }
    public int Target { get; set; }
    public int Current { get; set; }
    public bool IsCompleted { get; set; }
    public int ZbsReward { get; set; }
    public int LootBoxReward { get; set; }
    public bool RewardGranted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public QuestProgress() { }

    public QuestProgress(QuestDefinition def)
    {
        QuestId = def.Id;
        Description = def.Description;
        Target = def.Target;
        ZbsReward = def.ZbsReward;
        LootBoxReward = def.LootBoxReward;
        Current = 0;
        IsCompleted = false;
    }
}

/// <summary>
/// Every daily metric is recorded even when its quest is not selected, allowing the one free
/// reroll to recompute the replacement quest from already-finished matches without replaying them.
/// </summary>
public class DailyQuestMetrics
{
    public int EligibleMatches { get; set; }
    public int ResolvedFights { get; set; }
    public int FightWins { get; set; }
    public int UniqueOpponentsBest { get; set; }
    public int ConsecutiveWinsBest { get; set; }
    public int NemesisWins { get; set; }
    public int FinishedAliveBest { get; set; }
    public int PodiumAliveBest { get; set; }
    public int PlacesClimbedBest { get; set; }
    public int RoundsAtFirstBest { get; set; }
    public int JusticeReachedBest { get; set; }
    public int MatchWins { get; set; }
}

public class DailyQuestState
{
    public int CatalogVersion { get; set; }
    public string Date { get; set; } // yyyy-MM-dd format
    public List<QuestProgress> Quests { get; set; } = new();
    public DailyQuestMetrics Metrics { get; set; } = new();
    public bool AllCompleted => Quests?.Count > 0 && Quests.All(q => q.IsCompleted);
    public bool DailyCompleted { get; set; }
    /// <summary>Legacy compatibility alias; V2 sets this with DailyCompleted at two of three quests.</summary>
    public bool BonusClaimed { get; set; }
    public bool DailyBonusGranted { get; set; }
    public bool MasteryBonusGranted { get; set; }
    public int RerollsRemaining { get; set; } = 1;
    public List<string> RerolledQuestIds { get; set; } = new();
}

public class WeeklyQuestState
{
    public string WeekKey { get; set; }
    public List<string> CompletedDates { get; set; } = new();
    public bool RewardGranted { get; set; }
}

public class QuestData
{
    public int SchemaVersion { get; set; }
    public DailyQuestState ActiveDay { get; set; }
    public int StreakDays { get; set; }
    public int BestStreakDays { get; set; }
    public string LastStreakDate { get; set; } // yyyy-MM-dd
    public WeeklyQuestState WeeklyJourney { get; set; }
    public LootBoxResult LastLootBox { get; set; }
    public ulong LastLootBoxGameId { get; set; }
    /// <summary>Consecutive loot boxes below Rare. Nine means the next box is guaranteed Rare+.</summary>
    public int LootBoxPity { get; set; }
}

public class LootBoxResult
{
    public string OpeningId { get; set; }
    public string Rarity { get; set; }
    public int ZbsAmount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool Acknowledged { get; set; }
    public bool WasPityUpgrade { get; set; }
    public int ZbsBalance { get; set; }
    public int RemainingLootBoxes { get; set; }
    public int LootBoxPity { get; set; }
    public int GuaranteedRareIn { get; set; }
}

public class LootBoxOddsTier
{
    public string Rarity { get; set; }
    public double Chance { get; set; }
    public int MinZbs { get; set; }
    public int MaxZbs { get; set; }

    public LootBoxOddsTier(string rarity, double chance, int minZbs, int maxZbs)
    {
        Rarity = rarity;
        Chance = chance;
        MinZbs = minZbs;
        MaxZbs = maxZbs;
    }
}

public class LootBoxOpenOutcome
{
    public LootBoxResult Result { get; set; }
    public string Error { get; set; }
}

public sealed class QuestAccountStateSnapshot
{
    public int ZbsPoints { get; init; }
    public int PendingLootBoxes { get; init; }
    public QuestData Quests { get; init; }
}

public static class QuestService
{
    public const int CurrentSchemaVersion = 2;
    public const int CurrentCatalogVersion = 2;
    public const int DailyQuestRequirement = 2;
    public const int DailyBonusZbs = 20;
    public const int MasteryBonusLootBoxes = 1;
    public const int WeeklyTargetDays = 5;
    public const int WeeklyRewardZbs = 100;

    public static readonly IReadOnlyList<QuestDefinition> DailyQuestCatalog = new List<QuestDefinition>
    {
        new("dq_clock_in", "Clock In", "На смену",
            "Finish a match.", "Завершите матч.",
            QuestLane.Anchor, "hourglass", QuestAggregation.DailySum, 1, 20,
            metrics => metrics.EligibleMatches),

        new("dq_thick_of_it", "In the Thick of It", "В гуще событий",
            "Take part in 4 resolved fights.", "Примите участие в 4 завершённых боях.",
            QuestLane.Skirmish, "swords", QuestAggregation.DailySum, 4, 30,
            metrics => metrics.ResolvedFights),
        new("dq_throw_hands", "Throw Hands", "Распустить руки",
            "Win 2 fights.", "Победите в 2 боях.",
            QuestLane.Skirmish, "fist", QuestAggregation.DailySum, 2, 30,
            metrics => metrics.FightWins),
        new("dq_rival_tour", "Rival Tour", "Тур по соперникам",
            "Defeat 2 different opponents in one match.", "Победите 2 разных соперников за один матч.",
            QuestLane.Skirmish, "target", QuestAggregation.BestMatch, 2, 30,
            metrics => metrics.UniqueOpponentsBest),
        new("dq_hot_streak", "Hot Streak", "Горячая серия",
            "Win 2 fights in a row in one match.", "Победите в 2 боях подряд за один матч.",
            QuestLane.Skirmish, "flame", QuestAggregation.BestMatch, 2, 30,
            metrics => metrics.ConsecutiveWinsBest),
        new("dq_counterplay", "Counterplay", "Контригра",
            "Win a fight with class advantage.", "Победите в бою с преимуществом класса.",
            QuestLane.Skirmish, "balance", QuestAggregation.DailySum, 1, 30,
            metrics => metrics.NemesisWins),

        new("dq_still_standing", "Still Standing", "Остаться в строю",
            "Finish a match alive.", "Завершите матч в живых.",
            QuestLane.Ambition, "shield", QuestAggregation.BestMatch, 1, 30,
            metrics => metrics.FinishedAliveBest),
        new("dq_podium", "Podium Finish", "На пьедестале",
            "Finish in the top 3 while alive.", "Завершите матч в топ-3 и останьтесь в живых.",
            QuestLane.Ambition, "medal", QuestAggregation.BestMatch, 1, 30,
            metrics => metrics.PodiumAliveBest),
        new("dq_claw_back", "Claw Back", "Выкарабкаться",
            "Finish 2 places above your lowest position.", "Завершите матч на 2 места выше своей худшей позиции.",
            QuestLane.Ambition, "arrows", QuestAggregation.BestMatch, 2, 30,
            metrics => metrics.PlacesClimbedBest),
        new("dq_top_seat", "Top Seat", "Первое кресло",
            "Spend 2 rounds in 1st place in one match.", "Проведите 2 раунда на 1-м месте за один матч.",
            QuestLane.Ambition, "crown", QuestAggregation.BestMatch, 2, 30,
            metrics => metrics.RoundsAtFirstBest),
        new("dq_balanced_scales", "Balanced Scales", "Равные весы",
            "Reach 3 Justice in one match.", "Достигните 3 Справедливости за один матч.",
            QuestLane.Ambition, "balance", QuestAggregation.BestMatch, 3, 30,
            metrics => metrics.JusticeReachedBest),
        new("dq_take_hill", "Take the Hill", "Взять гору",
            "Earn a winning result in solo or play for the winning team.",
            "Добейтесь победного результата в одиночной игре или сыграйте за победившую команду.",
            QuestLane.Ambition, "mountain", QuestAggregation.BestMatch, 1, 30,
            metrics => metrics.MatchWins),
    };

    private static readonly IReadOnlyDictionary<string, QuestDefinition> QuestById =
        DailyQuestCatalog.ToDictionary(definition => definition.Id, StringComparer.Ordinal);

    public const int RarePityLimit = 10;

    public static readonly IReadOnlyList<LootBoxOddsTier> LootBoxOdds = new List<LootBoxOddsTier>
    {
        new("Common", 60.0, 15, 30),
        new("Uncommon", 25.0, 40, 75),
        new("Rare", 12.0, 100, 175),
        new("Epic", 2.5, 300, 450),
        new("Legendary", 0.5, 750, 750),
    };

    public static QuestDefinition GetDefinition(string questId)
    {
        return questId != null && QuestById.TryGetValue(questId, out var definition)
            ? definition
            : null;
    }

    public static bool EnsureQuestsInitialized(DiscordAccountClass account)
    {
        return EnsureQuestsInitialized(account, DateTimeOffset.UtcNow);
    }

    public static bool EnsureQuestsInitialized(DiscordAccountClass account, DateTimeOffset now)
    {
        if (account == null) throw new ArgumentNullException(nameof(account));
        now = now.ToUniversalTime();
        var today = DateKey(now);
        var yesterday = DateKey(now.AddDays(-1));
        var changed = false;

        if (account.Quests == null)
        {
            account.Quests = new QuestData();
            changed = true;
        }

        var data = account.Quests;
        if (data.SchemaVersion != CurrentSchemaVersion)
        {
            data.SchemaVersion = CurrentSchemaVersion;
            changed = true;
        }

        if (data.BestStreakDays < data.StreakDays)
        {
            data.BestStreakDays = data.StreakDays;
            changed = true;
        }

        changed |= EnsureWeeklyJourney(data, now);

        var oldDay = data.ActiveDay;
        var legacyToday = oldDay?.Date == today && oldDay.CatalogVersion != CurrentCatalogVersion;
        var legacyTodayWasSettled = legacyToday && oldDay.BonusClaimed;
        var needsNewDay = oldDay == null
                          || oldDay.Date != today
                          || oldDay.CatalogVersion != CurrentCatalogVersion;

        if (needsNewDay)
        {
            if (data.LastStreakDate != yesterday && data.LastStreakDate != today && data.StreakDays != 0)
                data.StreakDays = 0;

            data.ActiveDay = CreateDailyState(today, account.DiscordId);
            changed = true;

            // A paid legacy board is represented as fully settled for the rest of its UTC day.
            // This prevents a deployment from paying the same day twice while keeping loot state intact.
            if (legacyTodayWasSettled)
            {
                foreach (var quest in data.ActiveDay.Quests)
                {
                    quest.Current = quest.Target;
                    quest.IsCompleted = true;
                    quest.RewardGranted = true;
                    quest.CompletedAt = now;
                }

                data.ActiveDay.DailyCompleted = true;
                data.ActiveDay.BonusClaimed = true;
                data.ActiveDay.DailyBonusGranted = true;
                data.ActiveDay.MasteryBonusGranted = true;
                data.ActiveDay.RerollsRemaining = 0;
                AddWeeklyCompletion(data, today, now);
            }
        }

        changed |= NormalizeActiveDay(data.ActiveDay, account.DiscordId, now);
        changed |= GrantAvailableRewards(account, now);
        return changed;
    }

    public static void TrackGameEnd(
        DiscordAccountClass account,
        GamePlayerBridgeClass player,
        GameClass game,
        bool isMatchWinner)
    {
        TrackGameEnd(account, player, game, isMatchWinner, DateTimeOffset.UtcNow);
    }

    public static void TrackGameEnd(
        DiscordAccountClass account,
        GamePlayerBridgeClass player,
        GameClass game,
        bool isMatchWinner,
        DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        EnsureQuestsInitialized(account, now);
        var day = account.Quests.ActiveDay;
        var metrics = day.Metrics;
        var tracker = player.Passives.AchievementTracker ?? new InGameAchievementTracker();

        metrics.EligibleMatches++;
        metrics.ResolvedFights += Math.Max(0, tracker.TotalFightsWon + tracker.TotalFightsLost);

        // Every non-Madara viewer receives a private projected ending under Eternal Tsukuyomi.
        // Participation remains safe to count; real outcome/result facts would contradict that view.
        var suppressResultFacts = Madara.IsEternalTsukuyomiActive(game) && !Madara.IsMadara(player);
        if (!suppressResultFacts)
        {
            metrics.FightWins += Math.Max(0, tracker.TotalFightsWon);
            metrics.UniqueOpponentsBest = Math.Max(metrics.UniqueOpponentsBest, tracker.DefeatedPlayerIds?.Count ?? 0);
            metrics.ConsecutiveWinsBest = Math.Max(metrics.ConsecutiveWinsBest, tracker.MaxConsecutiveWins);
            metrics.NemesisWins += Math.Max(0, tracker.NemesisAdvantageWins);

            var alive = !player.Passives.IsDead;
            var finalPlace = player.Status.GetPlaceAtLeaderBoard();
            metrics.FinishedAliveBest = Math.Max(metrics.FinishedAliveBest, alive ? 1 : 0);
            metrics.PodiumAliveBest = Math.Max(metrics.PodiumAliveBest, alive && finalPlace <= 3 ? 1 : 0);

            var lowestPlace = player.Status.PlaceAtLeaderBoardHistory?
                .Where(entry => entry.Place > 0)
                .Select(entry => entry.Place)
                .Append(finalPlace)
                .DefaultIfEmpty(finalPlace)
                .Max() ?? finalPlace;
            metrics.PlacesClimbedBest = Math.Max(metrics.PlacesClimbedBest, Math.Max(0, lowestPlace - finalPlace));
            metrics.RoundsAtFirstBest = Math.Max(metrics.RoundsAtFirstBest, tracker.RoundsAtFirst);
            metrics.JusticeReachedBest = Math.Max(metrics.JusticeReachedBest, tracker.JusticeReached);
            if (isMatchWinner) metrics.MatchWins++;
        }

        RecomputeSelectedQuests(day, now);
        GrantAvailableRewards(account, now);
    }

    public static bool TryRerollDailyQuest(
        DiscordAccountClass account,
        string questId,
        DateTimeOffset now,
        out string error)
    {
        error = null;
        now = now.ToUniversalTime();
        EnsureQuestsInitialized(account, now);

        var day = account.Quests.ActiveDay;
        var index = day.Quests.FindIndex(quest => string.Equals(quest.QuestId, questId, StringComparison.Ordinal));
        if (index < 0)
        {
            error = "Daily quest not found.";
            return false;
        }

        var current = day.Quests[index];
        var currentDefinition = GetDefinition(current.QuestId);
        if (currentDefinition == null || currentDefinition.Lane == QuestLane.Anchor)
        {
            error = "The anchor quest cannot be rerolled.";
            return false;
        }
        if (current.IsCompleted)
        {
            error = "A completed quest cannot be rerolled.";
            return false;
        }
        if (day.RerollsRemaining <= 0)
        {
            error = "No daily rerolls remaining.";
            return false;
        }

        var selectedIds = day.Quests.Select(quest => quest.QuestId).ToHashSet(StringComparer.Ordinal);
        var candidates = DailyQuestCatalog
            .Where(definition => definition.Lane == currentDefinition.Lane
                                 && !selectedIds.Contains(definition.Id)
                                 && !(day.RerolledQuestIds?.Contains(definition.Id) ?? false))
            .ToList();
        if (candidates.Count == 0)
        {
            error = "No replacement quest is available.";
            return false;
        }

        var replacementIndex = StableIndex(
            $"reroll:{current.QuestId}", day.Date, account.DiscordId, currentDefinition.Lane, candidates.Count);
        var replacement = new QuestProgress(candidates[replacementIndex]);
        day.Quests[index] = replacement;
        day.RerollsRemaining--;
        day.RerolledQuestIds ??= new List<string>();
        if (!day.RerolledQuestIds.Contains(current.QuestId, StringComparer.Ordinal))
            day.RerolledQuestIds.Add(current.QuestId);

        RecomputeQuest(replacement, candidates[replacementIndex], day.Metrics, now);
        GrantAvailableRewards(account, now);
        return true;
    }

    public static DateTimeOffset GetResetAt(DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        return new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
    }

    public static DateTimeOffset GetWeekEndsAt(DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        var date = now.UtcDateTime.Date;
        var daysUntilNextMonday = ((int)DayOfWeek.Monday - (int)date.DayOfWeek + 7) % 7;
        if (daysUntilNextMonday == 0) daysUntilNextMonday = 7;
        return new DateTimeOffset(date.AddDays(daysUntilNextMonday), TimeSpan.Zero);
    }

    /// <summary>The caller must hold the account monitor.</summary>
    public static QuestAccountStateSnapshot CaptureAccountState(DiscordAccountClass account)
    {
        return new QuestAccountStateSnapshot
        {
            ZbsPoints = account.ZbsPoints,
            PendingLootBoxes = account.PendingLootBoxes,
            Quests = CloneQuestData(account.Quests),
        };
    }

    /// <summary>The caller must hold the account monitor.</summary>
    public static void RestoreAccountState(DiscordAccountClass account, QuestAccountStateSnapshot snapshot)
    {
        account.ZbsPoints = snapshot.ZbsPoints;
        account.PendingLootBoxes = snapshot.PendingLootBoxes;
        account.Quests = snapshot.Quests;
    }

    private static DailyQuestState CreateDailyState(string date, ulong accountId)
    {
        var anchor = DailyQuestCatalog.Single(definition => definition.Lane == QuestLane.Anchor);
        return new DailyQuestState
        {
            CatalogVersion = CurrentCatalogVersion,
            Date = date,
            Quests = new List<QuestProgress>
            {
                new(anchor),
                new(SelectLaneQuest(QuestLane.Skirmish, date, accountId)),
                new(SelectLaneQuest(QuestLane.Ambition, date, accountId)),
            },
            Metrics = new DailyQuestMetrics(),
            RerollsRemaining = 1,
            RerolledQuestIds = new List<string>(),
        };
    }

    private static QuestDefinition SelectLaneQuest(QuestLane lane, string date, ulong accountId)
    {
        var candidates = DailyQuestCatalog.Where(definition => definition.Lane == lane).ToList();
        return candidates[StableIndex("daily", date, accountId, lane, candidates.Count)];
    }

    private static int StableIndex(string purpose, string date, ulong accountId, QuestLane lane, int count)
    {
        var input = $"kotgh:daily-quests:v{CurrentCatalogVersion}:{date}:{accountId}:{lane}:{purpose}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(digest) % (uint)count);
    }

    private static bool NormalizeActiveDay(DailyQuestState day, ulong accountId, DateTimeOffset now)
    {
        var changed = false;
        if (day.Metrics == null) { day.Metrics = new DailyQuestMetrics(); changed = true; }
        if (day.Quests == null) { day.Quests = new List<QuestProgress>(); changed = true; }
        if (day.RerolledQuestIds == null) { day.RerolledQuestIds = new List<string>(); changed = true; }
        var clampedRerolls = Math.Clamp(day.RerollsRemaining, 0, 1);
        if (day.RerollsRemaining != clampedRerolls) { day.RerollsRemaining = clampedRerolls; changed = true; }

        var normalized = new List<QuestProgress>();
        var lanes = new HashSet<QuestLane>();
        foreach (var quest in day.Quests)
        {
            var definition = GetDefinition(quest?.QuestId);
            if (definition == null || !lanes.Add(definition.Lane)) { changed = true; continue; }
            changed |= ApplyDefinitionSnapshot(quest, definition);
            normalized.Add(quest);
        }

        foreach (var lane in Enum.GetValues<QuestLane>())
        {
            if (lanes.Contains(lane)) continue;
            var definition = lane == QuestLane.Anchor
                ? DailyQuestCatalog.Single(candidate => candidate.Lane == QuestLane.Anchor)
                : SelectLaneQuest(lane, day.Date, accountId);
            var replacement = new QuestProgress(definition);
            // Corrupt/missing progress on an already-settled day must never reopen an economy payout.
            replacement.RewardGranted = day.DailyCompleted || day.BonusClaimed;
            if (day.MasteryBonusGranted)
            {
                replacement.Current = replacement.Target;
                replacement.IsCompleted = true;
                replacement.CompletedAt = now;
            }
            normalized.Add(replacement);
            lanes.Add(lane);
            changed = true;
        }

        if (!day.Quests.SequenceEqual(normalized))
        {
            day.Quests = normalized.OrderBy(quest => GetDefinition(quest.QuestId).Lane).ToList();
            changed = true;
        }

        foreach (var quest in day.Quests)
        {
            var before = (quest.Current, quest.IsCompleted, quest.CompletedAt);
            RecomputeQuest(quest, GetDefinition(quest.QuestId), day.Metrics, now);
            if (before != (quest.Current, quest.IsCompleted, quest.CompletedAt)) changed = true;
        }

        if (day.DailyBonusGranted && (!day.DailyCompleted || !day.BonusClaimed))
        {
            day.DailyCompleted = true;
            day.BonusClaimed = true;
            changed = true;
        }
        return changed;
    }

    private static bool ApplyDefinitionSnapshot(QuestProgress quest, QuestDefinition definition)
    {
        var changed = false;
        if (quest.Description != definition.Description) { quest.Description = definition.Description; changed = true; }
        if (quest.Target != definition.Target) { quest.Target = definition.Target; changed = true; }
        if (quest.ZbsReward != definition.ZbsReward) { quest.ZbsReward = definition.ZbsReward; changed = true; }
        if (quest.LootBoxReward != definition.LootBoxReward) { quest.LootBoxReward = definition.LootBoxReward; changed = true; }
        var normalizedCurrent = Math.Clamp(quest.Current, 0, quest.Target);
        if (quest.IsCompleted) normalizedCurrent = quest.Target;
        if (quest.Current != normalizedCurrent) { quest.Current = normalizedCurrent; changed = true; }
        if (quest.Current >= quest.Target && !quest.IsCompleted) { quest.IsCompleted = true; changed = true; }
        return changed;
    }

    private static void RecomputeSelectedQuests(DailyQuestState day, DateTimeOffset now)
    {
        foreach (var quest in day.Quests)
            RecomputeQuest(quest, GetDefinition(quest.QuestId), day.Metrics, now);
    }

    private static void RecomputeQuest(
        QuestProgress quest,
        QuestDefinition definition,
        DailyQuestMetrics metrics,
        DateTimeOffset now)
    {
        if (quest == null || definition == null || metrics == null) return;
        var value = Math.Clamp(definition.MetricValue(metrics), 0, definition.Target);
        quest.Current = Math.Max(quest.Current, value);
        if (quest.Current < quest.Target) return;
        if (!quest.IsCompleted) quest.IsCompleted = true;
        quest.CompletedAt ??= now;
    }

    private static bool GrantAvailableRewards(DiscordAccountClass account, DateTimeOffset now)
    {
        var changed = false;
        var data = account.Quests;
        var day = data.ActiveDay;
        foreach (var quest in day.Quests.Where(quest => quest.IsCompleted && !quest.RewardGranted))
        {
            account.ZbsPoints += quest.ZbsReward;
            account.PendingLootBoxes += quest.LootBoxReward;
            quest.RewardGranted = true;
            changed = true;
        }

        var completed = day.Quests.Count(quest => quest.IsCompleted);
        if (completed >= DailyQuestRequirement && !day.DailyBonusGranted)
        {
            account.ZbsPoints += DailyBonusZbs;
            day.DailyCompleted = true;
            day.BonusClaimed = true;
            day.DailyBonusGranted = true;
            AdvanceStreak(data, day.Date, now);
            AddWeeklyCompletion(data, day.Date, now);
            changed = true;
        }

        if (completed == day.Quests.Count && day.Quests.Count > 0 && !day.MasteryBonusGranted)
        {
            account.PendingLootBoxes += MasteryBonusLootBoxes;
            day.MasteryBonusGranted = true;
            changed = true;
        }

        var weekly = data.WeeklyJourney;
        if (weekly.CompletedDates.Count >= WeeklyTargetDays && !weekly.RewardGranted)
        {
            account.ZbsPoints += WeeklyRewardZbs;
            weekly.RewardGranted = true;
            changed = true;
        }

        return changed;
    }

    private static void AdvanceStreak(QuestData data, string today, DateTimeOffset now)
    {
        if (data.LastStreakDate == today) return;
        var yesterday = DateKey(now.AddDays(-1));
        data.StreakDays = data.LastStreakDate == yesterday ? data.StreakDays + 1 : 1;
        data.BestStreakDays = Math.Max(data.BestStreakDays, data.StreakDays);
        data.LastStreakDate = today;
    }

    private static bool EnsureWeeklyJourney(QuestData data, DateTimeOffset now)
    {
        var weekKey = WeekKey(now);
        if (data.WeeklyJourney?.WeekKey == weekKey)
        {
            var changed = false;
            if (data.WeeklyJourney.CompletedDates == null)
            {
                data.WeeklyJourney.CompletedDates = new List<string>();
                changed = true;
            }
            var normalizedDates = data.WeeklyJourney.CompletedDates
                .Where(date => !string.IsNullOrWhiteSpace(date))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (!data.WeeklyJourney.CompletedDates.SequenceEqual(normalizedDates, StringComparer.Ordinal))
            {
                data.WeeklyJourney.CompletedDates = normalizedDates;
                changed = true;
            }
            return changed;
        }

        data.WeeklyJourney = new WeeklyQuestState { WeekKey = weekKey };
        return true;
    }

    private static void AddWeeklyCompletion(QuestData data, string date, DateTimeOffset now)
    {
        data.WeeklyJourney ??= new WeeklyQuestState { WeekKey = WeekKey(now) };
        data.WeeklyJourney.CompletedDates ??= new List<string>();
        if (!data.WeeklyJourney.CompletedDates.Contains(date, StringComparer.Ordinal))
            data.WeeklyJourney.CompletedDates.Add(date);
    }

    private static string DateKey(DateTimeOffset now) =>
        now.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string WeekKey(DateTimeOffset now)
    {
        var date = now.ToUniversalTime().UtcDateTime.Date;
        return $"{ISOWeek.GetYear(date):D4}-W{ISOWeek.GetWeekOfYear(date):D2}";
    }

    private static QuestData CloneQuestData(QuestData source)
    {
        if (source == null) return null;
        return new QuestData
        {
            SchemaVersion = source.SchemaVersion,
            ActiveDay = CloneDailyState(source.ActiveDay),
            StreakDays = source.StreakDays,
            BestStreakDays = source.BestStreakDays,
            LastStreakDate = source.LastStreakDate,
            WeeklyJourney = source.WeeklyJourney == null ? null : new WeeklyQuestState
            {
                WeekKey = source.WeeklyJourney.WeekKey,
                CompletedDates = source.WeeklyJourney.CompletedDates?.ToList() ?? new List<string>(),
                RewardGranted = source.WeeklyJourney.RewardGranted,
            },
            LastLootBox = source.LastLootBox,
            LastLootBoxGameId = source.LastLootBoxGameId,
            LootBoxPity = source.LootBoxPity,
        };
    }

    private static DailyQuestState CloneDailyState(DailyQuestState source)
    {
        if (source == null) return null;
        return new DailyQuestState
        {
            CatalogVersion = source.CatalogVersion,
            Date = source.Date,
            Quests = source.Quests?.Select(quest => new QuestProgress
            {
                QuestId = quest.QuestId,
                Description = quest.Description,
                Target = quest.Target,
                Current = quest.Current,
                IsCompleted = quest.IsCompleted,
                ZbsReward = quest.ZbsReward,
                LootBoxReward = quest.LootBoxReward,
                RewardGranted = quest.RewardGranted,
                CompletedAt = quest.CompletedAt,
            }).ToList() ?? new List<QuestProgress>(),
            Metrics = source.Metrics == null ? null : new DailyQuestMetrics
            {
                EligibleMatches = source.Metrics.EligibleMatches,
                ResolvedFights = source.Metrics.ResolvedFights,
                FightWins = source.Metrics.FightWins,
                UniqueOpponentsBest = source.Metrics.UniqueOpponentsBest,
                ConsecutiveWinsBest = source.Metrics.ConsecutiveWinsBest,
                NemesisWins = source.Metrics.NemesisWins,
                FinishedAliveBest = source.Metrics.FinishedAliveBest,
                PodiumAliveBest = source.Metrics.PodiumAliveBest,
                PlacesClimbedBest = source.Metrics.PlacesClimbedBest,
                RoundsAtFirstBest = source.Metrics.RoundsAtFirstBest,
                JusticeReachedBest = source.Metrics.JusticeReachedBest,
                MatchWins = source.Metrics.MatchWins,
            },
            DailyCompleted = source.DailyCompleted,
            BonusClaimed = source.BonusClaimed,
            DailyBonusGranted = source.DailyBonusGranted,
            MasteryBonusGranted = source.MasteryBonusGranted,
            RerollsRemaining = source.RerollsRemaining,
            RerolledQuestIds = source.RerolledQuestIds?.ToList() ?? new List<string>(),
        };
    }

    public static int GetGuaranteedRareIn(int pity)
    {
        return RarePityLimit - Math.Clamp(pity, 0, RarePityLimit - 1);
    }

    public static LootBoxResult GetLastUnacknowledgedLootBox(DiscordAccountClass account)
    {
        if (account == null) return null;

        lock (account)
        {
            account.Quests ??= new QuestData();
            NormalizeLootBoxPity(account.Quests);
            var last = account.Quests.LastLootBox;
            return last != null
                   && !string.IsNullOrWhiteSpace(last.OpeningId)
                   && !last.Acknowledged
                ? last
                : null;
        }
    }

    /// <summary>
    /// Atomically opens one pending box. Repeated calls return the same unacknowledged
    /// opening without consuming or crediting another box.
    /// </summary>
    public static LootBoxOpenOutcome OpenLootBox(DiscordAccountClass account, ulong gameId)
    {
        if (account == null)
            return new LootBoxOpenOutcome { Error = "Account not found." };

        lock (account)
        {
            account.Quests ??= new QuestData();
            NormalizeLootBoxPity(account.Quests);

            var unacknowledged = account.Quests.LastLootBox;
            if (unacknowledged != null
                && !string.IsNullOrWhiteSpace(unacknowledged.OpeningId)
                && !unacknowledged.Acknowledged)
            {
                return new LootBoxOpenOutcome { Result = unacknowledged };
            }

            if (account.PendingLootBoxes <= 0)
                return new LootBoxOpenOutcome { Error = "No loot boxes available." };

            account.PendingLootBoxes--;
            var result = GenerateLootBox(account);
            account.Quests.LastLootBox = result;
            account.Quests.LastLootBoxGameId = gameId;

            return new LootBoxOpenOutcome { Result = result };
        }
    }

    public static bool AcknowledgeLootBox(DiscordAccountClass account, string openingId)
    {
        if (account == null || string.IsNullOrWhiteSpace(openingId)) return false;

        lock (account)
        {
            var last = account.Quests?.LastLootBox;
            if (last == null
                || string.IsNullOrWhiteSpace(last.OpeningId)
                || last.Acknowledged
                || !string.Equals(last.OpeningId, openingId, StringComparison.Ordinal))
                return false;

            last.Acknowledged = true;
            return true;
        }
    }

    private static LootBoxResult GenerateLootBox(DiscordAccountClass account)
    {
        var pityBefore = Math.Clamp(account.Quests.LootBoxPity, 0, RarePityLimit - 1);
        var tier = RollLootBoxTier();
        var wasPityUpgrade = pityBefore == RarePityLimit - 1 && IsBelowRare(tier.Rarity);
        if (wasPityUpgrade)
            tier = LootBoxOdds.First(x => x.Rarity == "Rare");

        account.Quests.LootBoxPity = IsBelowRare(tier.Rarity) ? pityBefore + 1 : 0;

        var zbsAmount = tier.MinZbs == tier.MaxZbs
            ? tier.MinZbs
            : SecureRandom.Next(tier.MinZbs, tier.MaxZbs);
        account.ZbsPoints += zbsAmount;

        return new LootBoxResult
        {
            OpeningId = Guid.NewGuid().ToString("N"),
            Rarity = tier.Rarity,
            ZbsAmount = zbsAmount,
            Timestamp = DateTimeOffset.UtcNow,
            Acknowledged = false,
            WasPityUpgrade = wasPityUpgrade,
            ZbsBalance = account.ZbsPoints,
            RemainingLootBoxes = account.PendingLootBoxes,
            LootBoxPity = account.Quests.LootBoxPity,
            GuaranteedRareIn = GetGuaranteedRareIn(account.Quests.LootBoxPity),
        };
    }

    private static LootBoxOddsTier RollLootBoxTier()
    {
        // SecureRandom.Next is inclusive. Ten thousand equally likely outcomes preserve the
        // advertised 0.5 / 2.5 / 12 / 25 / 60 percent base distribution exactly.
        var roll = SecureRandom.Next(1, 10_000);
        if (roll <= 50) return LootBoxOdds.First(x => x.Rarity == "Legendary");
        if (roll <= 300) return LootBoxOdds.First(x => x.Rarity == "Epic");
        if (roll <= 1_500) return LootBoxOdds.First(x => x.Rarity == "Rare");
        if (roll <= 4_000) return LootBoxOdds.First(x => x.Rarity == "Uncommon");
        return LootBoxOdds.First(x => x.Rarity == "Common");
    }

    private static bool IsBelowRare(string rarity)
    {
        return rarity is "Common" or "Uncommon";
    }

    private static void NormalizeLootBoxPity(QuestData quests)
    {
        quests.LootBoxPity = Math.Clamp(quests.LootBoxPity, 0, RarePityLimit - 1);
    }
}
