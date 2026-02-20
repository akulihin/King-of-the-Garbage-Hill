using System;
using System.IO;
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
            builder.Services.AddSingleton(_services.GetRequiredService<Game.MemoryStorage.CharactersPull>());
            builder.Services.AddSingleton(_services.GetRequiredService<Game.GameLogic.CharacterPassives>());
            builder.Services.AddSingleton(_services.GetRequiredService<Config>());
            builder.Services.AddSingleton(_services.GetRequiredService<HttpClient>());

            // Register web-specific services
            builder.Services.AddSingleton<WebGameService>();
            builder.Services.AddSingleton<GameNotificationService>();
            builder.Services.AddSingleton<GameStoryService>();

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

            app.UseCors();

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
