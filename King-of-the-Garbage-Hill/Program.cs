using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using Lamar;
using Microsoft.Extensions.DependencyInjection;

namespace King_of_the_Garbage_Hill;

public class ProgramKingOfTheGarbageHill
{
    private readonly int[] _shardIds = { 0 };
    private DiscordShardedClient _client;
    private Container _services;

    public static Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        return new ProgramKingOfTheGarbageHill().MainAsync();
    }

    public async Task MainAsync()
    {
        _client = new DiscordShardedClient(_shardIds, new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Verbose,
            MessageCacheSize = 300,
            TotalShards = 1,
            GatewayIntents = GatewayIntents.All
            //AlwaysDownloadUsers = true,
            //ExclusiveBulkDelete = true
        });

        _services = new Container(x =>
        {
            x.AddSingleton(_client)
                .AddSingleton<CancellationTokenSource>()
                .AddSingleton<CommandService>()
                .AddSingleton<HttpClient>()
                .AddSingletonAutomatically()
                .AddTransientAutomatically()
                .BuildServiceProvider();
        });

        await _services.InitializeServicesAsync();

        await _client.SetGameAsync("*st - Запустить игру");

        await _client.LoginAsync(TokenType.Bot, _services.GetRequiredService<Config>().Token);
        await _client.StartAsync();

        //not needed
        //SendMessagesUsingConsole.ConsoleInput(_client);

        try
        {
            await Task.Delay(-1, _services.GetRequiredService<CancellationTokenSource>().Token);
        }
        catch (Exception exception)
        {
            Console.Write(exception.Message);
            Console.Write(exception.StackTrace);
        }
    }
}