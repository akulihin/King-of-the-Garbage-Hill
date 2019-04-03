
namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GamePlayerBridgeClass
    {
        public DiscordAccountClass DiscordAccount { get; set; }
        public CharacterClass Character { get; set; }

        public InGameStatus Status { get; set; }

        public bool IsBot()
        {
            if (DiscordAccount.DiscordId <= 1000000) return true;
            if (Status.SocketMessageFromBot == null) return true;

            return false;
        }

        public void MinusPsycheLog(GameClass game)
        {
            game.AddPreviousGameLogs($"\n{DiscordAccount.DiscordUserName} психанул");
        }
    }
}