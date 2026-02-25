using System;
using System.Collections.Generic;
using System.Linq;

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
}

public class LootBoxResult
{
    public string Rarity { get; set; }
    public int ZbsAmount { get; set; }
    public DateTimeOffset Timestamp { get; set; }
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

    private static readonly Random Rng = new();

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

    public static LootBoxResult GenerateLootBox(DiscordAccountClass account, ulong gameId)
    {
        var roll = Rng.NextDouble() * 100;
        string rarity;
        int minZbs, maxZbs;

        if (roll < 0.5)
        {
            rarity = "Legendary";
            minZbs = 500;
            maxZbs = 500;
        }
        else if (roll < 3.0)
        {
            rarity = "Epic";
            minZbs = 250;
            maxZbs = 400;
        }
        else if (roll < 15.0)
        {
            rarity = "Rare";
            minZbs = 75;
            maxZbs = 150;
        }
        else if (roll < 40.0)
        {
            rarity = "Uncommon";
            minZbs = 20;
            maxZbs = 50;
        }
        else
        {
            rarity = "Common";
            minZbs = 5;
            maxZbs = 15;
        }

        var zbsAmount = minZbs == maxZbs ? minZbs : Rng.Next(minZbs, maxZbs + 1);

        var result = new LootBoxResult
        {
            Rarity = rarity,
            ZbsAmount = zbsAmount,
            Timestamp = DateTimeOffset.UtcNow
        };

        account.ZbsPoints += zbsAmount;
        account.Quests ??= new QuestData();
        account.Quests.LastLootBox = result;
        account.Quests.LastLootBoxGameId = gameId;

        return result;
    }
}
