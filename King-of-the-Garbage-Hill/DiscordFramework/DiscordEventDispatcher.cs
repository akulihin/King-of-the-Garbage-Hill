﻿using System.Diagnostics;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.ReactionHandling;

namespace King_of_the_Garbage_Hill.DiscordFramework;

public sealed class DiscordEventDispatcher : IServiceSingleton
{
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.

    private readonly DiscordShardedClient _client;
    private readonly CommandHandling _commandHandler;
    private readonly GameReaction _gameReaction;
    private readonly LoginFromConsole _log;
    private readonly Global _global;
    private readonly StoreReactions _storeReactionHandling;
    private readonly TutorialReactions _tutorialReactions;
    private readonly LoreReactions _loreReactions;

    public DiscordEventDispatcher(DiscordShardedClient client, CommandHandling commandHandler,
        GameReaction gameReaction, LoginFromConsole log, Global global, StoreReactions storeReactionHandling,
        TutorialReactions tutorialReactions, LoreReactions loreReactions)
    {
        _client = client;
        _commandHandler = commandHandler;
        _gameReaction = gameReaction;
        _log = log;
        _global = global;
        _storeReactionHandling = storeReactionHandling;
        _tutorialReactions = tutorialReactions;
        _loreReactions = loreReactions;
    }

    public Task InitializeAsync()
    {
        _client.ChannelCreated += ChannelCreated;
        _client.ChannelDestroyed += ChannelDestroyed;
        _client.ChannelUpdated += ChannelUpdated;
        _client.CurrentUserUpdated += CurrentUserUpdated;
        _client.GuildAvailable += GuildAvailable;
        _client.GuildMembersDownloaded += GuildMembersDownloaded;
        _client.GuildMemberUpdated += GuildMemberUpdated;
        _client.GuildUnavailable += GuildUnavailable;
        _client.GuildUpdated += GuildUpdated;
        _client.JoinedGuild += JoinedGuild;
        _client.LeftGuild += LeftGuild;
        _client.Log += Log;
        _client.LoggedIn += LoggedIn;
        _client.LoggedOut += LoggedOut;
        _client.MessageDeleted += MessageDeleted;
        _client.MessageReceived += MessageReceived;
        _client.MessageUpdated += MessageUpdated;
        _client.ReactionAdded += ReactionAdded;
        _client.ButtonExecuted += _client_ButtonExecuted;
        _client.ReactionRemoved += ReactionRemoved;
        _client.ReactionsCleared += ReactionsCleared;
        _client.ShardConnected += _client_ShardConnected;
        _client.RecipientAdded += RecipientAdded;
        _client.RecipientRemoved += RecipientRemoved;
        _client.RoleCreated += RoleCreated;
        _client.RoleDeleted += RoleDeleted;
        _client.RoleUpdated += RoleUpdated;
        _client.UserBanned += UserBanned;
        _client.UserIsTyping += UserIsTyping;
        _client.UserJoined += UserJoined;
        _client.UserUnbanned += UserUnbanned;
        _client.UserUpdated += UserUpdated;
        _client.UserVoiceStateUpdated += UserVoiceStateUpdated;
        _client.SelectMenuExecuted += _client_SelectMenuExecuted;
        return Task.CompletedTask;
    }

    private async Task _client_SelectMenuExecuted(SocketMessageComponent button)
    {
        button.RespondAsync();
        _gameReaction.ReactionAddedGameWindow(button);
        _storeReactionHandling.ReactionAddedStore(button);
        _tutorialReactions.ReactionAddedTutorial(button);
        _loreReactions.ReactionAddedLore(button);
        
    }

    private async Task _client_ButtonExecuted(SocketMessageComponent button)
    {
        button.RespondAsync();
        _gameReaction.ReactionAddedGameWindow(button);
        _storeReactionHandling.ReactionAddedStore(button);
        _tutorialReactions.ReactionAddedTutorial(button);
        _loreReactions.ReactionAddedLore(button);
        
    }

    private async Task UserIsTyping(Cacheable<IUser, ulong> arg1, Cacheable<IMessageChannel, ulong> arg2)
    {
    }

    private async Task _client_ShardConnected(DiscordSocketClient arg)
    {
    }

    private async Task ChannelCreated(SocketChannel channel)
    {
    }

    private async Task ChannelDestroyed(SocketChannel channel)
    {
    }

    private async Task ChannelUpdated(SocketChannel channelBefore, SocketChannel channelAfter)
    {
    }

    private async Task CurrentUserUpdated(SocketSelfUser userBefore, SocketSelfUser userAfter)
    {
    }

    private async Task GuildAvailable(SocketGuild guild)
    {
    }

    private async Task GuildMembersDownloaded(SocketGuild guild)
    {
    }

    private async Task GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> userBefore, SocketGuildUser userAfterarg2)
    {
    }

    private async Task GuildUnavailable(SocketGuild guild)
    {
    }

    private async Task GuildUpdated(SocketGuild guildBefore, SocketGuild guildAfter)
    {
    }

    private async Task JoinedGuild(SocketGuild guild)
    {
    }


    private async Task LeftGuild(SocketGuild guild)
    {
    }

    private async Task Log(LogMessage logMessage)
    {
        _log.Log(logMessage);
    }

    private async Task LoggedIn()
    {
    }

    private async Task LoggedOut()
    {
    }

    private async Task MessageDeleted(Cacheable<IMessage, ulong> cacheMessage,
        Cacheable<IMessageChannel, ulong> channel)
    {
        if (!cacheMessage.HasValue || cacheMessage.Value.Author.IsBot) return; //IActivity guess
        _commandHandler._client_MessageDeleted(cacheMessage, channel.GetOrDownloadAsync().Result);
    }

    private async Task MessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot)
            return;
        _global.TimeSpendOnLastMessage.AddOrUpdate(message.Author.Id, Stopwatch.StartNew(),
            (_, _) => Stopwatch.StartNew());
        _commandHandler.Client_HandleCommandAsync(message);
    }

    private async Task MessageUpdated(Cacheable<IMessage, ulong> cacheMessageBefore, SocketMessage messageAfter,
        ISocketMessageChannel channel)
    {
        if (!cacheMessageBefore.HasValue)
            return;
        if (cacheMessageBefore.Value.Author.IsBot)
            return;


        _global.TimeSpendOnLastMessage.AddOrUpdate(messageAfter.Author.Id, Stopwatch.StartNew(), (_, _) => Stopwatch.StartNew());


        _commandHandler._client_MessageUpdated(cacheMessageBefore, messageAfter, channel);
    }


    private async Task ReactionAdded(Cacheable<IUserMessage, ulong> cacheMessage,
        Cacheable<IMessageChannel, ulong> channelCacheable,
        SocketReaction reaction)
    {
        /*
        if (reaction.User.Value.IsBot) return;
        var channel = channelCacheable.GetOrDownloadAsync().Result;
        _gameReaction.ReactionAddedGameWindow(cacheMessage, channel, reaction);
        _storeReactionHandling.ReactionAddedStore(cacheMessage, channel, reaction);
        */
    }

    private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> cacheMessage,
        Cacheable<IMessageChannel, ulong> channelCacheable,
        SocketReaction reaction)
    {
        /*
        if (reaction.User.Value.IsBot)
            return;
        var channel = channelCacheable.GetOrDownloadAsync().Result;
        _gameReaction.ReactionAddedGameWindow(cacheMessage, channel, reaction);
        _storeReactionHandling.ReactionAddedStore(cacheMessage, channel, reaction);*/
    }

    private async Task ReactionsCleared(Cacheable<IUserMessage, ulong> cacheMessage,
        Cacheable<IMessageChannel, ulong> channel)
    {
    }

    private async Task RecipientAdded(SocketGroupUser user)
    {
    }

    private async Task RecipientRemoved(SocketGroupUser user)
    {
    }

    private async Task RoleCreated(SocketRole role)
    {
    }

    private async Task RoleDeleted(SocketRole role)
    {
    }

    private async Task RoleUpdated(SocketRole roleBefore, SocketRole roleAfter)
    {
    }

    private async Task UserBanned(SocketUser user, SocketGuild guild)
    {
    }

    private async Task UserJoined(SocketGuildUser user)
    {
    }


    private async Task UserUnbanned(SocketUser user, SocketGuild guild)
    {
    }

    private async Task UserUpdated(SocketUser user, SocketUser guild)
    {
    }

    private async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState voiceStateBefore,
        SocketVoiceState voiceStateAfter)
    {
    }
}