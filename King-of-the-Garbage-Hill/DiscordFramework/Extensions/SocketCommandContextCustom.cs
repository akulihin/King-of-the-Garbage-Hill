using Discord.Commands;
using Discord.WebSocket;

namespace King_of_the_Garbage_Hill.DiscordFramework.Extensions
{
    public class SocketCommandContextCustom : ShardedCommandContext
    {
        public SocketCommandContextCustom(DiscordShardedClient client, SocketUserMessage msg,
            CommandsInMemory commandsInMemory, string messageContentForEdit = null, string language = null) : base(
            client, msg)
        {
            if (language == null)
                language = "en";
            CommandsInMemory = commandsInMemory;
            Language = language;
            MessageContentForEdit = messageContentForEdit;
        }

        public string MessageContentForEdit { get; }
        public string Language { get; }
        public CommandsInMemory CommandsInMemory { get; }
    }
}