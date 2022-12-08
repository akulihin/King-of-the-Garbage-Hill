using Discord;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class InGameDiscordStatus
    {
        public InGameDiscordStatus()
        {
            SocketMessageFromBot = null;
            SocketSecondaryMessageFromBot = null;
        }

        public IUserMessage SocketMessageFromBot { get; set; }
        public IUserMessage SocketSecondaryMessageFromBot { get; set; }
    }
}