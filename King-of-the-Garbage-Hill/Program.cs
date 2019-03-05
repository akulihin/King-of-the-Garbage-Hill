using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.BotFramework.Extensions;
using Lamar;
using Microsoft.Extensions.DependencyInjection;

namespace King_of_the_Garbage_Hill
{
    public class ProgramKingOfTheGarbageHill
    {
        private readonly int[] _shardIds = {0};
        private DiscordShardedClient _client;
        private Container _services;

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
                    .AutoAddSingleton()
                    .AutoAddTransient()
                    .BuildServiceProvider();
            });

            await _services.InitializeServicesAsync();

            await _client.SetGameAsync("Кто такой?");

            await _client.LoginAsync(TokenType.Bot, _services.GetRequiredService<Config>().Token);
            await _client.StartAsync();


            try
            {
                await Task.Delay(-1, _services.GetRequiredService<CancellationTokenSource>().Token);
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}