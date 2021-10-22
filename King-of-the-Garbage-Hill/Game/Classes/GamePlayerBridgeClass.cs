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
        public bool IsLogs { get; set; }
        public string UserType { get; set; }
        public List<ulong> DeleteMessages { get; set; } = new List<ulong>();

        public bool IsBot()
        {
            if (DiscordId <= 1000000) return true;
            if (Status.SocketMessageFromBot == null) return true;

            return false;
        }

        public void MinusPsycheLog(GameClass game)
        {
            game.AddGlobalLogs($"\n{DiscordUsername} психанул");
        }
    }
}