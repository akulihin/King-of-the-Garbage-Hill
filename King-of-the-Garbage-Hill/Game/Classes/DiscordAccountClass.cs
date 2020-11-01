using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class DiscordAccountClass
    {

        public string DiscordUserName { get; set; }
        public ulong DiscordId { get; set; }
        public string MyPrefix { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsLogs { get; set; }
        public ulong GameId { get; set; }
        public string UserType { get; set; }
        public int ZbsPoints { get; set; }

        public List<ChampionChances> ChampionChance = new List<ChampionChances>();
        public List<ChampionStatisticsClass> ChampionStatistics = new List<ChampionStatisticsClass>();
        public List<MatchHistoryClass> MatchHistory = new List<MatchHistoryClass>();
        public List<PerformanceStatisticsClass> PerformanceStatistics = new List<PerformanceStatisticsClass>();

        public ulong TotalPlays { get; set; }
        public ulong TotalWins { get; set; }

        public class ChampionChances
        {
            public int CharacterChanceMin;
            public int CharacterChanceMax;
            public string CharacterName;
            public double Multiplier;

            public ChampionChances(string characterName, int characterChanceMin, int characterChanceMax)
            {
                CharacterName = characterName;
                CharacterChanceMin = characterChanceMin;
                CharacterChanceMax = characterChanceMax;
                Multiplier = 1.0;
            }
        }

        public class ChampionStatisticsClass
        {
            public string CharacterName;
            public ulong Plays;
            public ulong Wins;

            public ChampionStatisticsClass(string characterName, int wins)
            {
                CharacterName = characterName;
                Wins = wins == 1 ? 1 : (ulong) 0;
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
            public int Score;
            public int Place;

            public MatchHistoryClass(string characterName, int score, int place)
            {
                CharacterName = characterName;
                Score = score;
                Place = place;
                Date = DateTimeOffset.UtcNow;
            }
        }
    }
}