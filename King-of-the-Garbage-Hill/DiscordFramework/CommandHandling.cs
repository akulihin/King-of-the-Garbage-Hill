using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using Lamar.IoC;
using ParameterInfo = Discord.Commands.ParameterInfo;

namespace King_of_the_Garbage_Hill.DiscordFramework;

public sealed class CommandHandling : ModuleBaseCustom, IServiceSingleton
{
    private readonly UserAccounts _accounts;

    private readonly DiscordShardedClient _client;
    private readonly CommandService _commands;
    private readonly CommandsInMemory _commandsInMemory;
    private readonly Global _global;
    private readonly LoginFromConsole _log;

    private readonly Scope _services;


    public CommandHandling(CommandService commands,
        DiscordShardedClient client, UserAccounts accounts,
        CommandsInMemory commandsInMemory,
        Scope scope, LoginFromConsole log, Global global)
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

            await Task.CompletedTask;
        }
        catch (Exception e)
        {
            _log.Critical(e.Message);
        }
    }


    public async Task _client_MessageUpdated(Cacheable<IMessage, ulong> messageBefore,
        SocketMessage messageAfter, ISocketMessageChannel arg3)
    {
        if (messageAfter.Author.IsBot)
            return;

        var after = messageAfter as IUserMessage;
        if (messageAfter.Content == null) return;

        if (messageAfter.Author is SocketGuildUser { IsMuted: true })
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
            var context =
                new SocketCommandContextCustom(_client, message, _commandsInMemory, message.Author, message.Channel,
                    "edit");
            var argPos = 0;


            if (message.Channel is SocketDMChannel)
            {
                var resultTask = Task.FromResult(await _commands.ExecuteAsync(context, argPos, _services));
                await resultTask.ContinueWith(async task => await CommandResults(task, context));

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


    public async Task Client_MessageUpdated(Cacheable<IMessage, ulong> messageBefore,
        SocketMessage messageAfter, ISocketMessageChannel arg3)
    {
        await _client_MessageUpdated(messageBefore, messageAfter, arg3);
    }


    public async Task HandleCommandAsync(SocketMessage msg)
    {
        if (!(msg is SocketUserMessage message)) return;

        var account = _accounts.GetAccount(msg.Author);
        var myPrefix = "*";

        if (account.MyPrefix != null)
            myPrefix = account.MyPrefix;

        var context =
            new SocketCommandContextCustom(_client, message, _commandsInMemory, message.Author, message.Channel);

        var argPos = 0;

        if (msg.Author.IsBot)
            return;

        if (message.HasStringPrefix("*", ref argPos) || message.HasStringPrefix("*" + " ",
                                                         ref argPos)
                                                     || message.HasMentionPrefix(_client.CurrentUser,
                                                         ref argPos)
                                                     || message.HasStringPrefix(myPrefix + " ",
                                                         ref argPos)
                                                     || message.HasStringPrefix(myPrefix, ref argPos)
                                                     || context.GuildName == "DM"
           )
        {
            var resultTask = _commands.ExecuteAsync(context, argPos, _services);
            await resultTask.ContinueWith(async task => await CommandResults(task, context));
        }
    }

    public async Task Client_HandleCommandAsync(SocketMessage msg)
    {
        await HandleCommandAsync(msg);
    }

    public async Task Client_HandleSlashCommandAsync(SocketSlashCommand command)
    {
        _global.TimeSpendOnLastMessage.AddOrUpdate(command.User.Id, Stopwatch.StartNew(),
            (_, _) => Stopwatch.StartNew());
        var context = new SocketCommandContextCustom(_client, command.User, command.Channel, command);
        var options = "";
        const char kek = '"';
        foreach (var option in command.Data.Options)
            switch (option.Type)
            {
                case ApplicationCommandOptionType.User:
                case ApplicationCommandOptionType.Channel:
                case ApplicationCommandOptionType.Role:
                case ApplicationCommandOptionType.Mentionable:
                case ApplicationCommandOptionType.Attachment:
                    options += $" {kek}{((dynamic)option.Value).Id}{kek} ";
                    break;
                default:
                    options += $" {kek}{option.Value}{kek} ";
                    break;
            }

        var commandString = $"{command.Data.Name} {options}";
        _log.Debug(commandString);
        var resultTask = _commands.ExecuteAsync(context, commandString, _services);
        await resultTask.ContinueWith(async task => await CommandResults(task, context));
    }

    public async Task Client_HandleContextUserCommandAsync(SocketUserCommand command)
    {
        _global.TimeSpendOnLastMessage.AddOrUpdate(command.User.Id, Stopwatch.StartNew(),
            (_, _) => Stopwatch.StartNew());
        var context = new SocketCommandContextCustom(_client, command.User, command.Channel, command);

        var commandString = $"{command.Data.Name} {command.Data.Member.Id}";
        _log.Debug(commandString);
        var resultTask = _commands.ExecuteAsync(context, commandString, _services);
        await resultTask.ContinueWith(async task => await CommandResults(task, context));
    }


    public ApplicationCommandOptionType HandleParameterInfo(ParameterInfo parameter)
    {
        var paramTypeString = parameter.Type.Name;
        if (parameter.Type.Name.Contains("User"))
            paramTypeString = "User";
        if (parameter.Type.Name.Contains("Int"))
            paramTypeString = "Integer";
        if (parameter.Type.Name.Contains("Channel"))
            paramTypeString = "Channel";
        if (parameter.Type.Name.Contains("Single"))
            paramTypeString = "Number";
        if (parameter.Type.Name.Contains("Role"))
            paramTypeString = "Role";
        if (parameter.Type.Name.Contains("bool"))
            paramTypeString = "Boolean";

        _ = Enum.TryParse(paramTypeString, out ApplicationCommandOptionType paramType);

        return paramType;
    }


    [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
    private static bool AreOptionsRequired(bool? option1, bool? option2)
    {
        if (option1 == false && !option2.HasValue)
            return false;

        return option1.Value != option2.Value;
    }

    private static bool AreOptionsEqual(RestApplicationCommand command1, SlashCommandProperties command2)
    {
        var equal = true;

        if (!command2.Options.IsSpecified) return command1.Options.Count == 0;

        var options1 = command1.Options.ToList();
        var options2 = command2.Options.Value;

        if (options1.Count != options2.Count)
            return false;

        foreach (var _ in options1.Where(o => options2.All(x =>
                     x.Name != o.Name || x.Description != o.Description || x.Type != o.Type ||
                     AreOptionsRequired(x.IsRequired, o.IsRequired)))) equal = false;

        return equal;
    }

    public async Task SlashCommandRegistration()
    {
        try
        {
            var commandModules = _commands.Modules;
            var existingCommands = await _client.Rest.GetGuildApplicationCommands(372150439728381953);
            var commandsCount = 0;
            var allCommands = new List<SlashCommandProperties>();
            foreach (var module in commandModules)
            foreach (var command in module.Commands)
            {
                var guildCommand = new SlashCommandBuilder();
                var commandName = command.Module.Group != null
                    ? $"{command.Module.Group} {command.Name}"
                    : command.Name;
                var moduleName = module.Remarks != null ? $"{module.Remarks} {module.Name}" : module.Name;
                var summary = command.Summary;

                if (moduleName != "General" && moduleName != "Top" && moduleName != "DiceRollCommands" &&
                    moduleName != "BlackList" && moduleName != "RandomOctopus" && moduleName != "StatsUser" &&
                    moduleName != "Reminder")
                    continue;
                if (string.IsNullOrEmpty(command.Summary))
                    continue;

                if (summary.Length > 100)
                    summary = summary[..97] + "...";

                guildCommand.WithDescription(summary);
                guildCommand.WithName(commandName.ToLower());

                foreach (var parameter in command.Parameters)
                    guildCommand.AddOption(parameter.Name.ToLower(), HandleParameterInfo(parameter), "_",
                        !parameter.IsOptional);

                allCommands.Add(guildCommand.Build());
            }

            foreach (var command in allCommands.Where(command => !existingCommands.Any(x =>
                         x.Name == command.Name.Value && x.Description == command.Description.Value &&
                         AreOptionsEqual(x, command))))
            {
                await _client.Rest.CreateGuildCommand(command, 372150439728381953);
                commandsCount++;
                _log.Info($"Registered {command.Name}");
            }

            _log.Info($"Registered {commandsCount} Slash Commands");
        }
        catch (Exception exception)
        {
            _log.Critical(exception.Message);
            _log.Critical(exception.StackTrace);
        }
    }

    public async Task GuildContextMessageCommandsRegistration()
    {
        try
        {
            var existingCommands = await _client.Rest.GetGuildApplicationCommands(372150439728381953);
            var totalRegistered = 0;
            foreach (var command in new ApplicationCommandProperties[]
                     {
                         new MessageCommandBuilder().WithName("Report").Build(),
                         new MessageCommandBuilder().WithName("Пожаловаться").Build(),
                         new MessageCommandBuilder().WithName("Известить Модерацию").Build(),
                         new MessageCommandBuilder().WithName("Сообщить о Нарушении").Build(),
                         new MessageCommandBuilder().WithName("Подать Жалобу").Build()
                     })
            {
                if (existingCommands.Any(x => x.Name == command.Name.Value)) continue;
                await _client.Rest.CreateGuildCommand(command, 372150439728381953);
                totalRegistered++;
                _log.Info($"Registered {command.Name}");
            }

            _log.Info($"Registered {totalRegistered} Guild Context Message Commands");
        }

        catch (Exception exception)
        {
            _log.Critical(exception.Message);
            _log.Critical(exception.StackTrace);
        }
    }

    public async Task GlobalContextMessageCommandsRegistration()
    {
        try
        {
            var existingCommands = await _client.Rest.GetGlobalApplicationCommands();
            var totalRegistered = 0;
            foreach (var command in new ApplicationCommandProperties[]
                     {
                         new MessageCommandBuilder().WithName("Позвать Модераторов").Build(),
                         new MessageCommandBuilder().WithName("Наказать Плохого").Build(),
                         new MessageCommandBuilder().WithName("Указать на Проблему").Build(),
                         new MessageCommandBuilder().WithName("Вызвать Админа").Build(),
                         new MessageCommandBuilder().WithName("Репрессировать").Build()
                     })
            {
                if (existingCommands.Any(x => x.Name == command.Name.Value)) continue;
                await _client.Rest.CreateGlobalCommand(command);
                totalRegistered++;
                _log.Info($"Registered {command.Name}");
            }

            _log.Info($"Registered {totalRegistered} Global Context Message Commands");
        }

        catch (Exception exception)
        {
            _log.Critical(exception.Message);
            _log.Critical(exception.StackTrace);
        }
    }

    public async Task GuildContextUserCommandsRegistration()
    {
        try
        {
            var existingCommands = await _client.Rest.GetGuildApplicationCommands(372150439728381953);
            var totalRegistered = 0;
            foreach (var command in new ApplicationCommandProperties[]
                     {
                         new UserCommandBuilder().WithName("Raid Report").Build(),
                         new UserCommandBuilder().WithName("Статы").Build(),
                         new UserCommandBuilder().WithName("Заблокировать").Build(),
                         new UserCommandBuilder().WithName("Разблокировать").Build(),
                         new UserCommandBuilder().WithName("Лидер").Build()
                     })
            {
                if (existingCommands.Any(x => x.Name == command.Name.Value)) continue;
                await _client.Rest.CreateGuildCommand(command, 372150439728381953);
                totalRegistered++;
                _log.Info($"Registered {command.Name}");
            }

            _log.Info($"Registered {totalRegistered} Guild Context User Commands");
        }

        catch (Exception exception)
        {
            _log.Critical(exception.Message);
            _log.Critical(exception.StackTrace);
        }
    }

    public async Task GlobalContextUserCommandsRegistration()
    {
        try
        {
            var existingCommands = await _client.Rest.GetGlobalApplicationCommands();
            var totalRegistered = 0;
            foreach (var command in new ApplicationCommandProperties[]
                     {
                         new UserCommandBuilder().WithName("Найти").Build()
                     })
            {
                if (existingCommands.Any(x => x.Name == command.Name.Value)) continue;
                await _client.Rest.CreateGlobalCommand(command);
                totalRegistered++;
                _log.Info($"Registered {command.Name}");
            }

            _log.Info($"Registered {totalRegistered} Global Context User Commands");
        }

        catch (Exception exception)
        {
            _log.Critical(exception.Message);
            _log.Critical(exception.StackTrace);
        }
    }

    public async Task CommandResults(Task<IResult> resultTask, SocketCommandContextCustom context)
    {
        _global.TimeSpendOnLastMessage.Remove(context.User.Id, out var watch);


        if (!resultTask.Result.IsSuccess)
        {
            _log.Warning($"Command [{context.Command}] by [{context.User}] [{context.GuildName}] after {watch?.Elapsed:m\\:ss\\.ffff}s.\n" +
                $"Reason: {resultTask.Result.ErrorReason}");
            _log.Error(resultTask.Result.ErrorReason);


            if (!resultTask.Result.ErrorReason.Contains("Unknown command"))
                await SendMessageAsync($"Error! {resultTask.Result.ErrorReason}", context);
        }
        else
        {
            _global.TotalCommandsIssued++;

            _log.Info(
                $"Command [{context.Command}] by [{context.User}] [{context.GuildName}] after {watch?.Elapsed:m\\:ss\\.ffff}s.");
        }
    }
}