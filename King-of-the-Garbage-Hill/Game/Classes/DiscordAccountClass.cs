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
        public string UserType {get; set;}
        public int ZBSPoints { get; set; }

        public List<ChampionChances> ChampionChance = new List<ChampionChances>();


        public class ChampionChances
        {
            public string CharacterName;
            public int CharacterChance;

            public ChampionChances(string characterName, int characterChance)
            {
                CharacterName = characterName;
                CharacterChance = characterChance;
            }
        }
    }
}