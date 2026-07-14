using System;
using System.Collections.Generic;
using King_of_the_Garbage_Hill.Game.Characters;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class DiscordAccountClass
{
    public List<CharacterChances> CharacterChance = new();
    public List<CharacterStatisticsClass> CharacterStatistics = new();
    public List<MatchHistoryClass> MatchHistory = new();
    public List<PerformanceStatisticsClass> PerformanceStatistics = new();
    public List<string> SeenCharacters = new();

    public bool WidgetAuthorized = false;
    public string WidgetStatTextLeft = "L";
    public string WidgetStatTextRight = "R";
    public int WidgetFavoriteNumber = 228;

    public string DiscordUserName { get; set; }
    public ulong DiscordId { get; set; }
    public string MyPrefix { get; set; }
    public bool IsPlaying { get; set; }
    public bool IsNewPlayer { get; set; }
    public bool PassedTutorial { get; set; }
    /// <summary>Player-facing locale. Russian remains the canonical internal language.</summary>
    public string Language { get; set; } = "ru";

    public int ZbsPoints { get; set; }
    public QuestData Quests { get; set; }
    public AchievementData Achievements { get; set; }
    /// <summary>Battleship mini-game W/L record + daily meta. Null on old accounts — lazily created on first settlement.</summary>
    public BattleshipStatsData BattleshipStats { get; set; }

    public ulong TotalPlays { get; set; }
    public ulong TotalWins { get; set; }

    public string CharacterToGiveNextTime { get; set; }
    public string CharacterPlayedLastTime { get; set; }
    /// <summary>FIFO account rewards. The next entry is consumed when a newly created game assigns it.</summary>
    public List<string> LootBoxCharacterQueue { get; set; } = new();

    public Dictionary<int, int> TierPity { get; set; } = new();
    public Dictionary<string, int> CharacterMastery { get; set; } = new();
    public List<string> ReplayHashes { get; set; } = new();
    public int PendingLootBoxes { get; set; }
    public DoomFortressData DoomFortress { get; set; } = new();

    /*
    0 == Normal
    1 == Casual
    2 == Admin
    404 == Bot
    */
    public int PlayerType { get; set; }


    public bool IsBot()
    {
        return PlayerType == 404;
    }


    public class CharacterChances
    {
        public int Changes;
        public string CharacterName;
        /// <summary>Paid store adjustment around the 1.00 baseline.</summary>
        public double Multiplier;
        /// <summary>Permanent, non-refundable percentage points awarded by loot boxes.</summary>
        public int LootBoxBonusPercentagePoints;

        public CharacterChances(string characterName, double multiplier = 1.0)
        {
            CharacterName = characterName;
            Multiplier = multiplier;
            Changes = 0;
            LootBoxBonusPercentagePoints = 0;
        }

        public double GetEffectiveMultiplier() =>
            Math.Round(Multiplier + LootBoxBonusPercentagePoints / 100d, 2);
    }

    public class CharacterStatisticsClass
    {
        public string CharacterName;
        public ulong Plays;
        public ulong Wins;
        public DateTime LastPlayedAt = DateTime.MinValue;

        public CharacterStatisticsClass(string characterName, ulong wins)
        {
            CharacterName = characterName;
            Wins = wins;
            Plays = 1;
        }
    }

    public class PerformanceStatisticsClass
    {
        public int Place;
        public ulong Times;

        public PerformanceStatisticsClass(int place)
        {
            Place = place;
            Times = 1;
        }
    }

    public class MatchHistoryClass
    {
        public string CharacterName;
        public DateTimeOffset Date;
        public int Place;
        public decimal Score;

        public MatchHistoryClass(string characterName, decimal score, int place)
        {
            CharacterName = characterName;
            Score = score;
            Place = place;
            Date = DateTimeOffset.UtcNow;
        }
    }

    public class CharacterRollClass
    {
        public string CharacterName;
        public int CharacterRangeMax;
        public int CharacterRangeMin;

        public CharacterRollClass(string characterName, int characterRangeMin, int characterRangeMax)
        {
            CharacterName = characterName;
            CharacterRangeMin = characterRangeMin;
            CharacterRangeMax = characterRangeMax;
        }
    }

    /// <summary>
    /// Battleship mini-game persistent meta. Day keys are "yyyy-MM-dd" UTC;
    /// the daily win streak advances like QuestClass.AdvanceStreak (consecutive win-days).
    /// </summary>
    public class BattleshipStatsData
    {
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int CurrentDailyStreak { get; set; }
        public int BestDailyStreak { get; set; }
        public string LastWinDayUtc { get; set; }
        public string LastFirstWinAwardDayUtc { get; set; }
        public int TotalZbsEarned { get; set; }
    }
}
