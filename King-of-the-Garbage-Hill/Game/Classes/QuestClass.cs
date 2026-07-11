using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Classes;

public enum QuestType
{
    PlayGames,
    WinGame,
    Top3Finish,
    PlayDifferentCharacters,
    Play5Games,
    Score50Plus
}

public class QuestDefinition
{
    public string Id { get; set; }
    public QuestType Type { get; set; }
    public string Description { get; set; }
    public int Target { get; set; }

    public QuestDefinition(string id, QuestType type, string description, int target)
    {
        Id = id;
        Type = type;
        Description = description;
        Target = target;
    }
}

public class QuestProgress
{
    public string QuestId { get; set; }
    public QuestType Type { get; set; }
    public string Description { get; set; }
    public int Target { get; set; }
    public int Current { get; set; }
    public bool IsCompleted { get; set; }
    public int ZbsReward { get; set; } = 25;

    public QuestProgress() { }

    public QuestProgress(QuestDefinition def)
    {
        QuestId = def.Id;
        Type = def.Type;
        Description = def.Description;
        Target = def.Target;
        Current = 0;
        IsCompleted = false;
    }

    public void Increment(int amount = 1)
    {
        if (IsCompleted) return;
        Current += amount;
        if (Current >= Target)
        {
            Current = Target;
            IsCompleted = true;
        }
    }

    public void SetProgress(int value)
    {
        if (IsCompleted) return;
        Current = value;
        if (Current >= Target)
        {
            Current = Target;
            IsCompleted = true;
        }
    }
}

public class DailyQuestState
{
    public string Date { get; set; } // yyyy-MM-dd format
    public List<QuestProgress> Quests { get; set; } = new();
    public bool AllCompleted => Quests.Count > 0 && Quests.All(q => q.IsCompleted);
    public bool BonusClaimed { get; set; }
}

public class QuestData
{
    public DailyQuestState ActiveDay { get; set; }
    public int StreakDays { get; set; }
    public string LastStreakDate { get; set; } // yyyy-MM-dd
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

public static class QuestService
{
    private static readonly List<QuestDefinition> QuestPool = new()
    {
        new("play1", QuestType.PlayGames, "Finish any game", 1),
        new("play3", QuestType.PlayGames, "Finish 3 games in a day", 3),
        new("win1", QuestType.WinGame, "Finish 1st place", 1),
        new("top3", QuestType.Top3Finish, "Finish in top 3", 1),
        new("chars3", QuestType.PlayDifferentCharacters, "Play 3 different characters", 3),
        new("play5", QuestType.Play5Games, "Finish 5 games in a day", 5),
        new("score50", QuestType.Score50Plus, "Score 50+ points in a game", 1),
    };

    public const int RarePityLimit = 10;

    public static readonly IReadOnlyList<LootBoxOddsTier> LootBoxOdds = new List<LootBoxOddsTier>
    {
        new("Common", 60.0, 15, 30),
        new("Uncommon", 25.0, 40, 75),
        new("Rare", 12.0, 100, 175),
        new("Epic", 2.5, 300, 450),
        new("Legendary", 0.5, 750, 750),
    };

    public static void EnsureQuestsInitialized(DiscordAccountClass account)
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        account.Quests ??= new QuestData();

        if (account.Quests.ActiveDay == null || account.Quests.ActiveDay.Date != today)
        {
            // Check streak before resetting
            if (account.Quests.ActiveDay != null)
            {
                var yesterday = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
                if (account.Quests.ActiveDay.AllCompleted && account.Quests.LastStreakDate == yesterday)
                {
                    // Streak continues
                }
                else if (account.Quests.ActiveDay.AllCompleted && account.Quests.ActiveDay.Date == yesterday)
                {
                    // Yesterday's quests were all completed — streak was maintained
                }
                else if (account.Quests.LastStreakDate != yesterday && account.Quests.LastStreakDate != today)
                {
                    // Streak broken
                    account.Quests.StreakDays = 0;
                }
            }

            // Roll new quests for today
            account.Quests.ActiveDay = new DailyQuestState
            {
                Date = today,
                Quests = RollDailyQuests(today)
            };
        }
    }

    private static List<QuestProgress> RollDailyQuests(string dateSeed)
    {
        // Use date as seed for deterministic daily quests (same for all players)
        var seed = dateSeed.GetHashCode();
        var rng = new Random(seed);
        var pool = new List<QuestDefinition>(QuestPool);

        var selected = new List<QuestProgress>();
        for (var i = 0; i < 3 && pool.Count > 0; i++)
        {
            var idx = rng.Next(pool.Count);
            selected.Add(new QuestProgress(pool[idx]));
            pool.RemoveAt(idx);
        }

        return selected;
    }

    public static void TrackGameEnd(DiscordAccountClass account, GamePlayerBridgeClass player, GameClass game)
    {
        EnsureQuestsInitialized(account);

        var quests = account.Quests.ActiveDay.Quests;
        var place = player.Status.GetPlaceAtLeaderBoard();
        var score = player.Status.GetScore();
        var characterName = player.GameCharacter.Name;

        foreach (var quest in quests)
        {
            if (quest.IsCompleted) continue;

            switch (quest.Type)
            {
                case QuestType.PlayGames:
                case QuestType.Play5Games:
                    quest.Increment();
                    break;
                case QuestType.WinGame:
                    if (place == 1) quest.Increment();
                    break;
                case QuestType.Top3Finish:
                    if (place <= 3) quest.Increment();
                    break;
                case QuestType.PlayDifferentCharacters:
                    // Count unique characters played today from match history
                    var today = DateTimeOffset.UtcNow.Date;
                    var uniqueChars = account.MatchHistory
                        .Where(m => m.Date.Date == today)
                        .Select(m => m.CharacterName)
                        .Distinct()
                        .Count();
                    // Include current game character
                    var todayChars = account.MatchHistory
                        .Where(m => m.Date.Date == today)
                        .Select(m => m.CharacterName)
                        .Append(characterName)
                        .Distinct()
                        .Count();
                    quest.SetProgress(todayChars);
                    break;
                case QuestType.Score50Plus:
                    if (score >= 50) quest.Increment();
                    break;
            }
        }

        // Check if all quests completed — award bonuses
        var allDone = quests.All(q => q.IsCompleted);
        if (allDone && !account.Quests.ActiveDay.BonusClaimed)
        {
            // Award individual quest rewards + all-complete bonus
            var totalReward = quests.Sum(q => q.ZbsReward) + 25; // 25 bonus for all 3
            account.ZbsPoints += totalReward;
            account.Quests.ActiveDay.BonusClaimed = true;

            // Update streak
            var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
            var yesterday = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

            if (account.Quests.LastStreakDate == yesterday || account.Quests.StreakDays == 0)
                account.Quests.StreakDays++;
            else if (account.Quests.LastStreakDate != today)
                account.Quests.StreakDays = 1;

            account.Quests.LastStreakDate = today;

            // 7-day streak bonus
            if (account.Quests.StreakDays >= 7 && account.Quests.StreakDays % 7 == 0)
            {
                account.ZbsPoints += 500;
            }
        }
        else if (!allDone)
        {
            // Award individual completed quest rewards immediately
            foreach (var quest in quests.Where(q => q.IsCompleted))
            {
                // Rewards are given as lump sum when all complete, or we can track per-quest
                // For simplicity, individual quest rewards are part of the all-complete bonus
            }
        }
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
