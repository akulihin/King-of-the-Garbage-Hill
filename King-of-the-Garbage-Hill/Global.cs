using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill;

public sealed class Global : IServiceSingleton
{
    public readonly DiscordShardedClient Client;
    public readonly DateTime TimeBotStarted;


    public List<GameClass> GamesList = new();

    public ConcurrentDictionary<ulong, Stopwatch> TimeSpendOnLastMessage = new();


    public Global(DiscordShardedClient client)
    {
        Client = client;
        TimeBotStarted = DateTime.Now;
    }

    public uint TotalCommandsIssued { get; set; }
    public uint TotalCommandsDeleted { get; set; }
    public uint TotalCommandsChanged { get; set; }
    private readonly object _gameIdLock = new();
    private ulong GamePlayingAndId { get; set; }

    public ConcurrentDictionary<string, WinRateClass> WinRates = new();

    public ConcurrentDictionary<ulong, GameClass> FinishedGamesList = new();
    private readonly object _finishedGamesLock = new();
    private readonly ConcurrentDictionary<ulong, DateTime> _finishedGameStoredAt = new();
    private static readonly TimeSpan FinishedGameRetention = TimeSpan.FromMinutes(30);
    private const int FinishedGameRetentionLimit = 128;

    /// <summary>
    /// Callback registered by GameNotificationService to broadcast final game state
    /// to web clients before the game is removed from GamesList.
    /// </summary>
    public Func<GameClass, Task> OnGameFinished { get; set; }

    /// <summary>
    /// Callback registered by GameNotificationService to save a replay when the game finishes.
    /// </summary>
    public Action<GameClass> OnReplaySave { get; set; }

    /// <summary>
    /// Callback registered by SimulationRunner (headless --sim mode) to capture per-game
    /// exceptions from the CheckIfReady loop: (gameId, roundNo, exception). Unset in production.
    /// </summary>
    public Action<ulong, int, Exception> SimErrorSink { get; set; }

    /// <summary>
    /// Null-safe fire-and-forget send to the service/debug channel. No-ops when Discord is
    /// offline (headless --sim mode) or the guild/channel isn't cached (mid-reconnect) — in both
    /// cases <c>GetGuild(...)</c> returns null and the old direct call threw a NullReferenceException.
    /// Callers still record the real error via _logs.Critical / SimErrorSink; this only keeps the
    /// diagnostic send itself from throwing. See docs/AUDIT-FINDINGS.md M13.
    /// </summary>
    public async Task TrySendServiceMessage(string text)
    {
        try
        {
            var channel = Client?.GetGuild(561282595799826432)?.GetTextChannel(935324189437624340);
            if (channel != null)
                await channel.SendMessageAsync(text);
        }
        catch
        {
            // Discord unavailable — the diagnostic send is best-effort; the caller already logged.
        }
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    public GamePlayerBridgeClass GetGameAccount(ulong userId, ulong gameId)
    {
        return GamesList.Find(x => x.GameId == gameId)!.PlayersList.Find(x => x.DiscordId == userId);
    }


    public ulong GetLastGamePlayingAndId()
    {
        lock (_gameIdLock)
            return GamePlayingAndId;
    }

    public ulong GetNewtGamePlayingAndId()
    {
        lock (_gameIdLock)
        {
            GamePlayingAndId++;
            return GamePlayingAndId;
        }
    }

    public void StoreFinishedGame(GameClass game)
    {
        if (game == null) return;

        lock (_finishedGamesLock)
        {
            var now = DateTime.UtcNow;
            FinishedGamesList[game.GameId] = game;
            _finishedGameStoredAt[game.GameId] = now;

            var cutoff = now - FinishedGameRetention;
            foreach (var (gameId, storedAt) in _finishedGameStoredAt)
            {
                if (storedAt >= cutoff) continue;
                _finishedGameStoredAt.TryRemove(gameId, out _);
                FinishedGamesList.TryRemove(gameId, out _);
            }

            foreach (var gameId in _finishedGameStoredAt.Keys
                         .OrderByDescending(id => id)
                         .Skip(FinishedGameRetentionLimit))
            {
                _finishedGameStoredAt.TryRemove(gameId, out _);
                FinishedGamesList.TryRemove(gameId, out _);
            }
        }
    }

    public GameClass GetFinishedGame(ulong gameId)
    {
        lock (_finishedGamesLock)
        {
            if (!_finishedGameStoredAt.TryGetValue(gameId, out var storedAt)
                || storedAt < DateTime.UtcNow - FinishedGameRetention)
            {
                _finishedGameStoredAt.TryRemove(gameId, out _);
                FinishedGamesList.TryRemove(gameId, out _);
                return null;
            }

            return FinishedGamesList.GetValueOrDefault(gameId);
        }
    }

    public class WinRateClass
    {
        public string CharacterName { get; set; }
        public WinRateClass( string characterName)
        {
            CharacterName = characterName;
        }

        public double Top1 { get; set; }
        public double Top2 { get; set; }
        public double Top3 { get; set; }
        public double Top4 { get; set; }
        public double Top5 { get; set; }
        public double Top6 { get; set; }
        public double GameTimes { get; set; }
        public double WinRate { get; set; }
        public double Elo { get; set; }
    }
}
