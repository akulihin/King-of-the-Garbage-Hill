using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class DiscordAccountClass
    {
        public List<CharacterChances> CharacterChance = new();
        public List<CharacterStatisticsClass> CharacterStatistics = new();
        public List<MatchHistoryClass> MatchHistory = new();
        public List<PerformanceStatisticsClass> PerformanceStatistics = new();

        public string DiscordUserName { get; set; }
        public ulong DiscordId { get; set; }
        public string MyPrefix { get; set; }
        public bool IsPlaying { get; set; }
        public List<string> SeenCharacters = new();
        public bool IsNewPlayer { get; set; }
       
        public int ZbsPoints { get; set; }

        public ulong TotalPlays { get; set; }
        public ulong TotalWins { get; set; }

        public string CharacterToGiveNextTime { get; set; }

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
            public double Multiplier;
            public int Tier;

            public CharacterChances(string characterName, int tier, double multiplier = 1.0)
            {
                CharacterName = characterName;
                Tier = tier;
                Multiplier = multiplier;
                Changes = 0;
            }
        }

        public class CharacterStatisticsClass
        {
            public string CharacterName;
            public ulong Plays;
            public ulong Wins;

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
            public int Score;

            public MatchHistoryClass(string characterName, int score, int place)
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
            public int CharacterRangeMin;
            public int CharacterRangeMax;

            public CharacterRollClass(string characterName, int characterRangeMin, int characterRangeMax)
            {
                CharacterName = characterName;
                CharacterRangeMin = characterRangeMin;
                CharacterRangeMax = characterRangeMax;
            }
        }
    }
}