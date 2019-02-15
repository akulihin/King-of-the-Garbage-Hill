using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lamar;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.DiscordFramework.Language;


namespace King_of_the_Garbage_Hill
{
    public class ProgramKingOfTheGarbageHill
    {
        private DiscordShardedClient _client;
        private Container _services;

        private readonly int[] _shardIds = { 0 };

        private static void Main()
        {
            new ProgramKingOfTheGarbageHill().RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            _client = new DiscordShardedClient(_shardIds, new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                MessageCacheSize = 50,
                TotalShards = 1
            });

            _services = new Container(x =>
            {
                x.AddSingleton(_client)
                    .AddSingleton<CancellationTokenSource>()
                    .AddSingleton<CommandService>()
                    .AddSingleton<HttpClient>()
                    .AddSingleton<IDataStorage, JsonLocalStorage>()
                    .AddSingleton<ILocalization, JsonLocalization>()
                    .AutoAddSingleton()
                    .AutoAddTransient()
                    .BuildServiceProvider();
            });

            await _services.InitializeServicesAsync();

            await _client.SetGameAsync("Кто такой?");

            await _client.LoginAsync(TokenType.Bot, _services.GetRequiredService<Config>().Token);
            await _client.StartAsync();

            SendMessagesUsingConsole.ConsoleInput(_client);

            try
            {

                await Task.Delay(-1, _services.GetRequiredService<CancellationTokenSource>().Token);
            }
            catch (TaskCanceledException)
            { }
        }
    }
}