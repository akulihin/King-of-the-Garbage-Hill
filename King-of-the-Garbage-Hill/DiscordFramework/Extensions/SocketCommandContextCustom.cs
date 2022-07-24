using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace King_of_the_Garbage_Hill.DiscordFramework.Extensions;
public class SocketCommandContextLocal : ICommandContext
{
    public DiscordSocketClient Client { get; }
    public SocketGuild Guild { get; }
    public ISocketMessageChannel Channel { get; }
    public SocketUser User { get; }
    public SocketUserMessage Message { get; }
    public bool IsPrivate => Channel is IPrivateChannel;


    public SocketCommandContextLocal(DiscordShardedClient client, SocketUserMessage msg)
    {
        Client = client.GetShard(GetShardId(client, msg.Channel is SocketGuildChannel channelClient ? channelClient.Guild : null));
        Guild = msg.Channel is SocketGuildChannel channel ? channel.Guild : null;
        Channel = msg.Channel;
        User = msg.Author;
        Message = msg;
    }

    public SocketCommandContextLocal(DiscordShardedClient client, ISocketMessageChannel messageChannel, SocketUser user)
    {
        Client = client.GetShard(GetShardId(client, messageChannel is SocketGuildChannel channel ? channel.Guild : null));
        Guild = (messageChannel as SocketGuildChannel)?.Guild;
        Channel = messageChannel;
        User = user;
        Message = null;
    }

    IDiscordClient ICommandContext.Client => Client;
    IGuild ICommandContext.Guild => Guild;
    IMessageChannel ICommandContext.Channel => Channel;
    IUser ICommandContext.User => User;
    IUserMessage ICommandContext.Message => Message;

    private static int GetShardId(DiscordShardedClient client, IGuild guild) => guild != null ? client.GetShardIdFor(guild) : 0;
}


public class SocketCommandContextCustom : SocketCommandContextLocal
{
    public SocketCommandContextCustom(DiscordShardedClient client, SocketUserMessage msg, CommandsInMemory commandsInMemory, SocketUser user, ISocketMessageChannel messageChannel, string messageContentForEdit = null, IDMChannel dmChannel = null, string command = "")
        : base(client, msg)
    {
        CommandsInMemory = commandsInMemory;
        MessageContentForEdit = messageContentForEdit;
        User = user;
        Channel = messageChannel;
        Command = Message.Content is { Length: > 0 } ? Message.Content : command;
        GuildName = Guild == null ? "DM" : $"{Guild.Name} [{Guild.Id}]";
        DmChannel = dmChannel;
        SlashCommand = null;
        Client = client;
    }

    public SocketCommandContextCustom(DiscordShardedClient client, SocketUser user, ISocketMessageChannel messageChannel, SocketSlashCommand slash)
        : base(client, messageChannel, user)
    {
        var guild = (messageChannel as SocketGuildChannel)?.Guild;
        User = user;
        Channel = messageChannel;
        Command = slash.Data.Name;
        GuildName = guild == null ? "Slash" : $"{Guild.Name} [{Guild.Id}] as Slash";
        SlashCommand = slash;
        Client = client;
    }
    public SocketCommandContextCustom(DiscordShardedClient client, SocketUser user, ISocketMessageChannel messageChannel, SocketUserCommand contextSlash)
        : base(client, messageChannel, user)
    {
        var guild = (messageChannel as SocketGuildChannel)?.Guild;
        User = user;
        Channel = messageChannel;
        Command = contextSlash.Data.Name;
        GuildName = guild == null ? "Slash" : $"{Guild.Name} [{Guild.Id}] as Slash";
        ContextSlash = contextSlash;
        Client = client;
    }

    public string MessageContentForEdit { get; }
    public CommandsInMemory CommandsInMemory { get; }
    public new SocketUser User { get; }
    public new ISocketMessageChannel Channel { get; }
    public string Command { get; }
    public string GuildName { get; }
    public IDMChannel DmChannel { get; }
    public SocketSlashCommand SlashCommand { get; }
    public SocketUserCommand ContextSlash { get; }
    public new DiscordShardedClient Client { get; }
}
