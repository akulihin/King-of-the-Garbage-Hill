using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.API;
using King_of_the_Garbage_Hill.API.Services;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Game.Simulation;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using King_of_the_Garbage_Hill.Localization;
using Lamar;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace King_of_the_Garbage_Hill;

public class ProgramKingOfTheGarbageHill
{
    private readonly int[] _shardIds = { 0 };
    private DiscordShardedClient _client;
    private Container _services;

    public static Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        return new ProgramKingOfTheGarbageHill().MainAsync(args);
    }

    public async Task MainAsync(string[] args)
    {
        // Must precede the container: UserAccounts loads the account store in its constructor.
        UserAccounts.DisableDiskPersistence = args.Contains("--sim");

        _client = new DiscordShardedClient(_shardIds, new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Verbose,
            MessageCacheSize = 300,
            TotalShards = 1,
            GatewayIntents = GatewayIntents.All
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

        // Headless simulation harness: same container (the CheckIfReady timer is already
        // running), but no Discord login and no Kestrel. See docs/ARCHITECTURE.md §10.
        if (args.Contains("--sim"))
        {
            ClaudeHaikuService.Disabled = true;
            // Nothing in the harness reads a replay (no OnReplaySave hook, no web clients), yet capturing
            // one projects and localizes all six players three times per round — snapshots that are then
            // discarded. Off, the simulator spends its time on game logic instead.
            ReplayService.CaptureEnabled = false;
            var exitCode = await _services.GetRequiredService<SimulationRunner>().RunAsync(args);
            Environment.Exit(exitCode);
        }

        _ = Task.Run(() => StartWebApi());

        await _client.SetGameAsync("*st - Запустить игру");
        await _client.LoginAsync(TokenType.Bot, _services.GetRequiredService<Config>().Token);
        await _client.StartAsync();

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

    /// <summary>
    /// Starts the ASP.NET Core Kestrel web server alongside the Discord bot.
    /// Shares the same singleton instances (Global, GameReaction, etc.) via DI.
    /// </summary>
    private async Task StartWebApi()
    {
        try
        {
            var builder = WebApplication.CreateBuilder();

            // Share key singletons from the Lamar container with ASP.NET Core
            builder.Services.AddSingleton(_services.GetRequiredService<Global>());
            builder.Services.AddSingleton(_services.GetRequiredService<GameReaction>());
            builder.Services.AddSingleton(_services.GetRequiredService<Game.GameLogic.CheckIfReady>());
            builder.Services.AddSingleton(_services.GetRequiredService<Game.DiscordMessages.GameUpdateMess>());
            builder.Services.AddSingleton(_services.GetRequiredService<Helpers.HelperFunctions>());
            builder.Services.AddSingleton(_services.GetRequiredService<SecureRandom>());
            builder.Services.AddSingleton(_services.GetRequiredService<Game.MemoryStorage.CharactersPull>());
            builder.Services.AddSingleton(_services.GetRequiredService<Game.GameLogic.CharacterPassives>());
            builder.Services.AddSingleton(_services.GetRequiredService<Config>());
            builder.Services.AddSingleton(_services.GetRequiredService<HttpClient>());
            builder.Services.AddSingleton(_services.GetRequiredService<Game.GameLogic.StartGameLogic>());
            builder.Services.AddSingleton(_services.GetRequiredService<LocalPersistentData.UsersAccounts.UserAccounts>());
            builder.Services.AddSingleton(_services.GetRequiredService<Game.Services.DiscordWidgetService>());
            // Validate and register the aggregate of product-scoped RU/EN catalogs
            // used by new server-authored messages. Legacy replay text remains compatible.
            builder.Services.AddSingleton(new MessageCatalog());

            // Register web-specific services
            builder.Services.AddSingleton<WebGameService>();
            builder.Services.AddSingleton<GameNotificationService>();
            builder.Services.AddSingleton<AdminLobbyService>();
            builder.Services.AddSingleton<GameStoryService>();
            builder.Services.AddSingleton<BlackjackService>();
            builder.Services.AddSingleton<BattleshipService>();
            builder.Services.AddSingleton<ClashService>();
            builder.Services.AddSingleton<ReplayService>();

            // Add SignalR for real-time communication
            builder.Services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.PropertyNamingPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase;
                });

            // Add controllers
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy =
                        System.Text.Json.JsonNamingPolicy.CamelCase;
                });

            // CORS — allow the Vue dev server and production domain
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy
                        .WithOrigins(
                            "http://localhost:5173",   // Vite dev server
                            "http://localhost:3535",   // C# dev server
                            "http://localhost",        // Local port 80
                            "http://kotgh.ozvmusic.com",  // Production (Cloudflare → origin over HTTP)
                            "https://kotgh.ozvmusic.com"  // Production (client-facing HTTPS)
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            var app = builder.Build();

            // Serve static files from wwwroot (for production Vue build)
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Serve game art (avatars, emojis, events) from DataBase/art/
            var artPath = Path.Combine(AppContext.BaseDirectory, "DataBase", "art");
            if (Directory.Exists(artPath))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(artPath),
                    RequestPath = "/art"
                });
                Console.WriteLine($"[WebAPI] Serving game art from {artPath} at /art/");
            }
            else
            {
                Console.WriteLine($"[WebAPI] WARNING: Art directory not found at {artPath}");
            }

            var soundPath = Path.Combine(AppContext.BaseDirectory, "DataBase", "sound");
            if (Directory.Exists(soundPath))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(soundPath),
                    RequestPath = "/sound"
                });
                Console.WriteLine($"[WebAPI] Serving game sounds from {soundPath} at /sound/");
            }
            else
            {
                Console.WriteLine($"[WebAPI] WARNING: Sound directory not found at {soundPath}");
            }

            app.UseRouting();
            app.UseCors();

            app.MapControllers();
            app.MapHub<GameHub>("/gamehub");

            // SPA fallback: serve index.html for any unmatched routes
            app.MapFallbackToFile("index.html");

            var port = Environment.GetEnvironmentVariable("KOTGH_PORT") ?? "80";
            Console.WriteLine($"[WebAPI] Starting web server on http://0.0.0.0:{port}");
            await app.RunAsync($"http://0.0.0.0:{port}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebAPI] ERROR starting web server: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
