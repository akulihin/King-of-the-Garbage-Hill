using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using Lamar.IoC;

namespace King_of_the_Garbage_Hill.DiscordFramework; 

public sealed class CommandHandling : ModuleBaseCustom, IServiceSingleton
{
    private readonly UserAccounts _accounts;

    private readonly DiscordShardedClient _client;
    private readonly CommandService _commands;
    private readonly CommandsInMemory _commandsInMemory;
    private readonly Global _global;

    private readonly Logs _log;
    private readonly Scope _services;


    public CommandHandling(CommandService commands,
        DiscordShardedClient client, UserAccounts accounts, CommandsInMemory commandsInMemory,
        Scope scope, Logs log, Global global)
    {
        _commands = commands;
        _services = scope;
        _log = log;
        _global = global;
        _client = client;
        _accounts = accounts;

        _commandsInMemory = commandsInMemory;
    }

    public async Task InitializeAsync()
    {
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
    }

    public async Task _client_MessageDeleted(Cacheable<IMessage, ulong> cacheMessage, IMessageChannel channel)
    {
        try
        {
            for (var index = 0; index < _commandsInMemory.CommandList.Count; index++)
            {
                if (cacheMessage.Value.Id != _commandsInMemory.CommandList[index].MessageUserId) continue;
                _global.TotalCommandsDeleted++;
                await _commandsInMemory.CommandList[index].BotSocketMsg.DeleteAsync();
                _commandsInMemory.CommandList.RemoveAt(index);
            }

            
        }
        catch (Exception exception)
        {
            _log.Critical(exception.Message);
            _log.Critical(exception.StackTrace);
        }
    }

    public async Task _client_MessageUpdated(Cacheable<IMessage, ulong> messageBefore,
        SocketMessage messageAfter, ISocketMessageChannel arg3)
    {
        if (messageAfter.Author.IsBot)
            return;
        var after = messageAfter as IUserMessage;
        if (messageAfter.Content == null) return;

        if (messageAfter.Author is SocketGuildUser userCheck && userCheck.IsMuted)
            return;


        var before = messageBefore.HasValue ? messageBefore.Value : null;
        if (before == null)
            return;
        if (arg3 == null)
            return;
        if (before.Content == after?.Content)
            return;


        var list = _commandsInMemory.CommandList;
        foreach (var t in list)
        {
            if (t.MessageUserId != messageAfter.Id) continue;

            if (!(messageAfter is SocketUserMessage message)) continue;

            if (t.BotSocketMsg == null)
                return;
            _global.TotalCommandsChanged++;
            var account = _accounts.GetAccount(messageAfter.Author);
            var context = new SocketCommandContextCustom(_client, message, _commandsInMemory, "edit", "ru");
            var argPos = 0;


            if (message.Channel is SocketDMChannel)
            {
                var resultTask = Task.FromResult(await _commands.ExecuteAsync(
                    context,
                    argPos,
                    _services));


                await resultTask.ContinueWith(async task =>
                    await CommandResults(task, context));

                return;
            }


            if (message.HasStringPrefix("*", ref argPos) || message.HasStringPrefix("*" + " ",
                                                             ref argPos)
                                                         || message.HasMentionPrefix(_client.CurrentUser,
                                                             ref argPos)
                                                         || message.HasStringPrefix(account.MyPrefix + " ",
                                                             ref argPos)
                                                         || message.HasStringPrefix(account.MyPrefix,
                                                             ref argPos))
            {
                var resultTask = Task.FromResult(await _commands.ExecuteAsync(
                    context,
                    argPos,
                    _services));


                await resultTask.ContinueWith(async task =>
                    await CommandResults(task, context));
            }

            return;
        }


        await HandleCommandAsync(messageAfter);
    }


    public async Task HandleCommandAsync(SocketMessage msg)
    {
        var message = msg as SocketUserMessage;
        if (message == null) return;
        var account = _accounts.GetAccount(msg.Author);
        var context = new SocketCommandContextCustom(_client, message, _commandsInMemory, null, "ru");
        var argPos = 0;

        if (message.Author is SocketGuildUser userCheck && userCheck.IsMuted)
            return;

        if (msg.Author.IsBot)
            return;

        var isDm = ((SocketChannel)msg.Channel).Users.Count == 2;

        if (message.HasStringPrefix("*", ref argPos) || message.HasStringPrefix("*" + " ",
                                                         ref argPos)
                                                     || message.HasMentionPrefix(_client.CurrentUser,
                                                         ref argPos)
                                                     || message.HasStringPrefix(account.MyPrefix + " ",
                                                         ref argPos)
                                                     || message.HasStringPrefix(account.MyPrefix,
                                                         ref argPos)
                                                     || isDm)
        {
            var resultTask = _commands.ExecuteAsync(
                context,
                argPos,
                _services);


            await resultTask.ContinueWith(async task =>
                await CommandResults(task, context));
        }
    }


    public async Task CommandResults(Task<IResult> resultTask, SocketCommandContextCustom context)
    {
        _global.TimeSpendOnLastMessage.Remove(context.User.Id, out var watch);

        var speedText = "";
        speedText = watch != null ? $"{watch.Elapsed:m\\:ss\\.ffff}" : "???";

        var guildName = context.Guild == null ? "DM" : $"{context.Guild.Name}({context.Guild.Id})";

        if (!resultTask.Result.IsSuccess)
        {
            _log.Warning(
                $"Command [{context.Message.Content}] by [{context.User}] [{guildName}] after {speedText}s.\n" +
                $"Reason: {resultTask.Result.ErrorReason}", "CommandHandling");
            _log.Error(resultTask.Result.ErrorReason);


            if (!resultTask.Result.ErrorReason.Contains("Unknown command"))
                await SendMessAsync($"Error! {resultTask.Result.ErrorReason}", context);
        }
        else
        {
            _global.TotalCommandsIssued++;

            _log.Info(
                $"Command [{context.Message.Content}] by [{context.User}] [{guildName}] after {speedText}s.",
                "CommandHandling");
        }


        
    }
}