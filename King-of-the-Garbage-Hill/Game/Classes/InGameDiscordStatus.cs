using Discord;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class InGameDiscordStatus
    {
        public InGameDiscordStatus()
        {
            SocketGameMessage = null;
            SocketCharacterMessage = null;
        }

        public IUserMessage SocketGameMessage { get; set; }
        public IUserMessage SocketCharacterMessage { get; set; }
    }
}