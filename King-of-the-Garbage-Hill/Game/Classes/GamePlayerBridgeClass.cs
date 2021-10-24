using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GamePlayerBridgeClass
    {
        public CharacterClass Character { get; set; }

        public InGameStatus Status { get; set; }

        public ulong DiscordId { get; set; }
        public ulong GameId { get; set; }
        public string DiscordUsername { get; set; } 
/*
0 == Normal
1 == Casual
2 == Admin
404 == Bot
*/
        public int PlayerType { get; set; }
        public List<ulong> DeleteMessages { get; set; } = new();
        public List<PredictClass> Predict { get; set; } = new();

        public bool IsBot()
        {
            return PlayerType == 404 || Status.SocketMessageFromBot == null;
        }

        public void MinusPsycheLog(GameClass game)
        {
            game.AddGlobalLogs($"\n{DiscordUsername} психанул");
        }
    }
}