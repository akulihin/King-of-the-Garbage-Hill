using System;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.Services;
using Microsoft.AspNetCore.SignalR;

namespace King_of_the_Garbage_Hill.API;

/// <summary>
/// SignalR Hub for real-time game communication.
/// Web clients connect here to receive game state updates and send actions.
/// 
/// Discord IDs are passed as STRINGS to avoid JavaScript number precision loss
/// (Discord snowflake IDs exceed Number.MAX_SAFE_INTEGER).
/// </summary>
public class GameHub : Hub
{
    private readonly WebGameService _gameService;
    private readonly GameNotificationService _notificationService;
    private readonly Global _global;

    public GameHub(WebGameService gameService, GameNotificationService notificationService, Global global)
    {
        _gameService = gameService;
        _notificationService = notificationService;
        _global = global;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        Console.WriteLine($"[WebAPI] SignalR client connected: {Context.ConnectionId}");
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (Context.Items.TryGetValue("discordId", out var discordIdObj) && discordIdObj is ulong discordId)
        {
            _notificationService.RemoveConnection(discordId, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
        Console.WriteLine($"[WebAPI] SignalR client disconnected: {Context.ConnectionId}");
    }

    // ── Authentication ────────────────────────────────────────────────

    /// <summary>
    /// Register the Discord ID for this connection.
    /// Accepts STRING to avoid JS number precision loss on large snowflake IDs.
    /// </summary>
    public async Task Authenticate(string discordIdStr)
    {
        if (!ulong.TryParse(discordIdStr, out var discordId))
        {
            await Clients.Caller.SendAsync("Error", $"Invalid Discord ID: {discordIdStr}");
            return;
        }

        Context.Items["discordId"] = discordId;
        _notificationService.RegisterConnection(discordId, Context.ConnectionId);

        // Return the ID as a string so JS doesn't lose precision
        await Clients.Caller.SendAsync("Authenticated", new { success = true, discordId = discordIdStr });
        Console.WriteLine($"[WebAPI] Connection {Context.ConnectionId} authenticated as Discord user {discordId}");
    }

    // ── Game Room Management ──────────────────────────────────────────

    public async Task JoinGame(ulong gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");

        // Also track which game this connection is watching
        Context.Items["gameId"] = gameId;
        _notificationService.RegisterGameConnection(gameId, Context.ConnectionId);

        var discordId = GetDiscordId();
        if (discordId == 0)
        {
            // Not authenticated — send spectator view
            var spectatorState = _gameService.GetGameStateForSpectator(gameId);
            if (spectatorState != null)
                await Clients.Caller.SendAsync("GameState", spectatorState);
            else
                await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        var state = _gameService.GetGameState(gameId, discordId);
        if (state != null)
        {
            await Clients.Caller.SendAsync("GameState", state);
        }
        else
        {
            // Player not in this game — send spectator view instead
            var spectatorState = _gameService.GetGameStateForSpectator(gameId);
            if (spectatorState != null)
                await Clients.Caller.SendAsync("GameState", spectatorState);
            else
                await Clients.Caller.SendAsync("Error", "Game not found.");
        }
    }

    public async Task LeaveGame(ulong gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game-{gameId}");
        _notificationService.RemoveGameConnection(gameId, Context.ConnectionId);
    }

    // ── Game Actions ──────────────────────────────────────────────────

    public async Task Attack(ulong gameId, int targetPlace)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.Attack(gameId, discordId, targetPlace);
        await Clients.Caller.SendAsync("ActionResult", new { action = "attack", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task Block(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.Block(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "block", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task DoAutoMove(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.AutoMove(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "autoMove", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task ChangeMind(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.ChangeMind(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "changeMind", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task ConfirmSkip(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.ConfirmSkip(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "confirmSkip", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task ConfirmPredict(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.ConfirmPredict(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "confirmPredict", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task LevelUp(ulong gameId, int statIndex)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.LevelUp(gameId, discordId, statIndex);
        await Clients.Caller.SendAsync("ActionResult", new { action = "levelUp", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task MoralToPoints(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.MoralToPoints(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "moralToPoints", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task MoralToSkill(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.MoralToSkill(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "moralToSkill", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task Predict(ulong gameId, Guid targetPlayerId, string characterName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.Predict(gameId, discordId, targetPlayerId, characterName);
        await Clients.Caller.SendAsync("ActionResult", new { action = "predict", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task AramReroll(ulong gameId, int slot)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.AramReroll(gameId, discordId, slot);
        await Clients.Caller.SendAsync("ActionResult", new { action = "aramReroll", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task AramConfirm(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.AramConfirm(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "aramConfirm", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    // ── Darksci / Young Gleb ─────────────────────────────────────────

    public async Task DarksciChoice(ulong gameId, bool isStable)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.DarksciChoice(gameId, discordId, isStable);
        await Clients.Caller.SendAsync("ActionResult", new { action = "darksciChoice", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task YoungGleb(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.YoungGleb(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "youngGleb", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task DopaChoice(ulong gameId, string tactic)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.DopaChoice(gameId, discordId, tactic);
        await Clients.Caller.SendAsync("ActionResult", new { action = "dopaChoice", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    // ── Kira Actions ─────────────────────────────────────────────────

    public async Task DeathNoteWrite(ulong gameId, Guid targetPlayerId, string characterName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.DeathNoteWrite(gameId, discordId, targetPlayerId, characterName);
        await Clients.Caller.SendAsync("ActionResult", new { action = "deathNoteWrite", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task ShinigamiEyes(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.ShinigamiEyes(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "shinigamiEyes", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    // ── Leave / Finish ────────────────────────────────────────────────

    /// <summary>
    /// Player voluntarily leaves the game (replaced by bot).
    /// </summary>
    public async Task FinishGame(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _gameService.FinishGame(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "finishGame", success, error });
    }

    // ── Settings ───────────────────────────────────────────────────────

    /// <summary>
    /// Toggle "Prefer Web" mode: when enabled, Discord messages are suppressed
    /// and the player only interacts via the web UI.
    /// </summary>
    public async Task SetPreferWeb(ulong gameId, bool preferWeb)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var game = _global.GamesList.Find(x => x.GameId == gameId);
        var player = game?.PlayersList.Find(x => x.DiscordId == discordId);
        if (player == null)
        {
            await Clients.Caller.SendAsync("Error", "Player not found in this game.");
            return;
        }

        player.PreferWeb = preferWeb;
        await Clients.Caller.SendAsync("ActionResult", new { action = "setPreferWeb", success = true, error = (string)null });
        Console.WriteLine($"[WebAPI] Player {discordId} set PreferWeb={preferWeb} in game {gameId}");
    }

    // ── Request State ─────────────────────────────────────────────────

    public async Task RequestGameState(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0)
        {
            var spectatorState = _gameService.GetGameStateForSpectator(gameId);
            if (spectatorState != null)
                await Clients.Caller.SendAsync("GameState", spectatorState);
            return;
        }

        var state = _gameService.GetGameState(gameId, discordId);
        if (state != null)
            await Clients.Caller.SendAsync("GameState", state);
        else
        {
            var spectatorState = _gameService.GetGameStateForSpectator(gameId);
            if (spectatorState != null)
                await Clients.Caller.SendAsync("GameState", spectatorState);
        }
    }

    public async Task RequestLobbyState()
    {
        var state = _gameService.GetLobbyState();
        await Clients.Caller.SendAsync("LobbyState", state);
    }

    // ── Private helpers ───────────────────────────────────────────────

    private ulong GetDiscordId()
    {
        if (Context.Items.TryGetValue("discordId", out var val) && val is ulong id)
            return id;
        return 0;
    }

    private async Task SendNotAuthenticated()
    {
        await Clients.Caller.SendAsync("Error", "Not authenticated. Call Authenticate first.");
    }

    private async Task PushStateToPlayer(ulong gameId, ulong discordId)
    {
        var state = _gameService.GetGameState(gameId, discordId);
        if (state != null)
        {
            var connections = _notificationService.GetConnections(discordId);
            if (connections.Count > 0)
                await Clients.Clients(connections.ToList()).SendAsync("GameState", state);
        }
    }
}
