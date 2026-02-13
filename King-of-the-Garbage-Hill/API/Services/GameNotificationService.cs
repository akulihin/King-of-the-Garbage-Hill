using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using Microsoft.AspNetCore.SignalR;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Pushes game state updates to web clients via SignalR in real time.
/// Runs on a fast timer and sends every state change immediately.
/// No rate limits — the web client can handle frequent updates.
/// </summary>
public class GameNotificationService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly Global _global;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly Timer _pushTimer;

    // Track which Discord IDs are connected to which SignalR connection(s)
    private readonly ConcurrentDictionary<ulong, HashSet<string>> _playerConnections = new();

    // Track which connections are watching which game (for spectators and players alike)
    private readonly ConcurrentDictionary<ulong, HashSet<string>> _gameConnections = new();

    // Track last known state per game to detect changes
    private readonly ConcurrentDictionary<ulong, GameSnapshot> _lastSnapshot = new();

    public GameNotificationService(IHubContext<GameHub> hubContext, Global global, GameUpdateMess gameUpdateMess)
    {
        _hubContext = hubContext;
        _global = global;
        _gameUpdateMess = gameUpdateMess;

        _pushTimer = new Timer
        {
            AutoReset = true,
            Interval = 300, // Check for updates every 300ms — no Discord rate limits
            Enabled = true,
        };
        _pushTimer.Elapsed += async (_, _) => await PushUpdates();
    }

    // ── Connection tracking by Discord ID ─────────────────────────────

    public void RegisterConnection(ulong discordId, string connectionId)
    {
        _playerConnections.AddOrUpdate(
            discordId,
            _ => new HashSet<string> { connectionId },
            (_, set) => { lock (set) { set.Add(connectionId); } return set; });
    }

    public void RemoveConnection(ulong discordId, string connectionId)
    {
        if (_playerConnections.TryGetValue(discordId, out var set))
        {
            lock (set) { set.Remove(connectionId); }
            if (set.Count == 0)
                _playerConnections.TryRemove(discordId, out _);
        }
    }

    public IReadOnlyCollection<string> GetConnections(ulong discordId)
    {
        if (_playerConnections.TryGetValue(discordId, out var set))
        {
            lock (set) { return set.ToList(); }
        }
        return Array.Empty<string>();
    }

    /// <summary>Returns true if this Discord user has at least one active web connection.</summary>
    public bool HasWebConnection(ulong discordId)
    {
        return _playerConnections.TryGetValue(discordId, out var set) && set.Count > 0;
    }

    // ── Connection tracking by game ID (for spectators) ───────────────

    public void RegisterGameConnection(ulong gameId, string connectionId)
    {
        _gameConnections.AddOrUpdate(
            gameId,
            _ => new HashSet<string> { connectionId },
            (_, set) => { lock (set) { set.Add(connectionId); } return set; });
    }

    public void RemoveGameConnection(ulong gameId, string connectionId)
    {
        if (_gameConnections.TryGetValue(gameId, out var set))
        {
            lock (set) { set.Remove(connectionId); }
            if (set.Count == 0)
                _gameConnections.TryRemove(gameId, out _);
        }
    }

    // ── Send game state to a specific player immediately ──────────────

    public async Task SendGameStateToPlayer(GameClass game, GamePlayerBridgeClass player)
    {
        var connections = GetConnections(player.DiscordId);
        if (connections.Count == 0) return;

        var dto = GameStateMapper.ToDto(game, player);
        WebGameService.PopulateCustomLeaderboard(dto, game, player, _gameUpdateMess);
        await _hubContext.Clients.Clients(connections.ToList()).SendAsync("GameState", dto);
    }

    /// <summary>
    /// Send game state to ALL connected web clients in this game:
    /// - Players (including Discord players who also opened the web UI)
    /// - Spectators (anyone who joined the game room)
    /// </summary>
    public async Task BroadcastGameState(GameClass game)
    {
        var sentConnectionIds = new HashSet<string>();

        // 1) Send personalized state to each player who has a web connection
        foreach (var player in game.PlayersList)
        {
            var connections = GetConnections(player.DiscordId);
            if (connections.Count == 0) continue;

            var dto = GameStateMapper.ToDto(game, player);
            WebGameService.PopulateCustomLeaderboard(dto, game, player, _gameUpdateMess);
            var connList = connections.ToList();
            await _hubContext.Clients.Clients(connList).SendAsync("GameState", dto);

            foreach (var c in connList) sentConnectionIds.Add(c);
        }

        // 2) Send spectator (public) state to anyone in the game room who isn't a player
        if (_gameConnections.TryGetValue(game.GameId, out var gameConns))
        {
            List<string> spectatorConns;
            lock (gameConns) { spectatorConns = gameConns.Where(c => !sentConnectionIds.Contains(c)).ToList(); }

            if (spectatorConns.Count > 0)
            {
                var spectatorDto = GameStateMapper.ToDto(game);
                await _hubContext.Clients.Clients(spectatorConns).SendAsync("GameState", spectatorDto);
            }
        }
    }

    /// <summary>Send a game event to all connections in the game room.</summary>
    public async Task SendGameEvent(ulong gameId, string eventType, object data = null)
    {
        await _hubContext.Clients.Group($"game-{gameId}").SendAsync("GameEvent", new { eventType, data });
    }

    // ── Timer-driven push ─────────────────────────────────────────────

    private async Task PushUpdates()
    {
        foreach (var game in _global.GamesList.ToList())
        {
            try
            {
                var gameId = game.GameId;
                var currentRound = game.RoundNo;
                var currentTime = game.TimePassed.Elapsed.TotalSeconds;
                var anyReady = game.PlayersList.Any(p => p.Status.IsReady);

                _lastSnapshot.TryGetValue(gameId, out var last);
                last ??= new GameSnapshot();

                var roundChanged = currentRound != last.RoundNo;
                // Push every 0.5s for smooth timer, or immediately on round/ready changes
                var timeChanged = Math.Abs(currentTime - last.TimeSeconds) > 0.5;
                var readyChanged = anyReady != last.AnyReady;

                if (roundChanged || timeChanged || readyChanged)
                {
                    _lastSnapshot[gameId] = new GameSnapshot
                    {
                        RoundNo = currentRound,
                        TimeSeconds = currentTime,
                        AnyReady = anyReady,
                    };

                    await BroadcastGameState(game);

                    if (roundChanged)
                    {
                        await SendGameEvent(gameId, "RoundChanged", new { round = currentRound });
                    }
                }

                if (game.IsFinished)
                {
                    await BroadcastGameState(game);
                    await SendGameEvent(gameId, "GameFinished");
                    _lastSnapshot.TryRemove(gameId, out _);
                    _gameConnections.TryRemove(gameId, out _);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebAPI] Notification error for game {game.GameId}: {ex.Message}");
            }
        }
    }

    private class GameSnapshot
    {
        public int RoundNo { get; set; }
        public double TimeSeconds { get; set; }
        public bool AnyReady { get; set; }
    }
}
