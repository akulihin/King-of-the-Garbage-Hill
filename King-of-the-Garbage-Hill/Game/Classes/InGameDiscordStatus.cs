using Discord;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class InGameDiscordStatus
    {
        public InGameDiscordStatus()
        {
            SocketMessageFromBot = null;
        }

        public IUserMessage SocketMessageFromBot { get; set; }
    }
}