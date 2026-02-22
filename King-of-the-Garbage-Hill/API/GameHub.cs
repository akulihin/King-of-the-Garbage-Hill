using System;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.Services;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
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
    private readonly UserAccounts _userAccounts;
    private readonly BlackjackService _blackjackService;

    public GameHub(WebGameService gameService, GameNotificationService notificationService, Global global, UserAccounts userAccounts, BlackjackService blackjackService)
    {
        _gameService = gameService;
        _notificationService = notificationService;
        _global = global;
        _userAccounts = userAccounts;
        _blackjackService = blackjackService;
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

    // ── Web Account & Game Creation ──────────────────────────────────

    /// <summary>
    /// Register a web-only account (no Discord required).
    /// Generates a unique ID in the 9000000000000000000+ range.
    /// </summary>
    public async Task RegisterWebAccount(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 32)
        {
            await Clients.Caller.SendAsync("Error", "Username must be 1-32 characters.");
            return;
        }

        var webId = _userAccounts.GenerateWebUserId();
        _userAccounts.CreateWebAccount(webId, username.Trim());

        // Authenticate this connection with the new web ID
        Context.Items["discordId"] = webId;
        _notificationService.RegisterConnection(webId, Context.ConnectionId);

        await Clients.Caller.SendAsync("WebAccountCreated", new { discordId = webId.ToString(), username = username.Trim() });
        Console.WriteLine($"[WebAPI] Web account created: {username} ({webId})");
    }

    /// <summary>
    /// Create a new web game (1 creator + 5 bots).
    /// </summary>
    public async Task CreateWebGame()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        var username = account?.DiscordUserName ?? "WebPlayer";

        var (gameId, error) = await _gameService.CreateGame(discordId, username);
        if (error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        // Auto-join the SignalR room
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
        Context.Items["gameId"] = gameId;
        _notificationService.RegisterGameConnection(gameId, Context.ConnectionId);

        await Clients.Caller.SendAsync("GameCreated", new { gameId });
    }

    /// <summary>
    /// Join an existing game by replacing a bot.
    /// </summary>
    public async Task JoinWebGame(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        var username = account?.DiscordUserName ?? "WebPlayer";

        var (success, error) = _gameService.JoinWebGame(gameId, discordId, username);
        if (!success)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        // Auto-join the SignalR room
        await Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");
        Context.Items["gameId"] = gameId;
        _notificationService.RegisterGameConnection(gameId, Context.ConnectionId);

        await Clients.Caller.SendAsync("GameJoined", new { gameId });

        // Push initial state
        await PushStateToPlayer(gameId, discordId);
    }

    // ── Game Actions ──────────────────────────────────────────────────

    public async Task Attack(ulong gameId, int targetPlace)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.Attack(gameId, discordId, targetPlace);
        await Clients.Caller.SendAsync("ActionResult", new { action = "attack", success, error });

        await PushStateToPlayer(gameId, discordId);
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

    // ── Blackjack (Dead Player Mini-Game) ────────────────────────────

    public async Task BlackjackJoin(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        var username = account?.DiscordUserName ?? "Player";

        var (state, error) = _blackjackService.JoinTable(gameId, discordId, username);
        if (error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await PushBlackjackState(gameId);
    }

    public async Task BlackjackHit(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (state, error) = _blackjackService.Hit(gameId, discordId);
        if (error != null)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "blackjackHit", success = false, error });
            return;
        }

        await PushBlackjackState(gameId);
    }

    public async Task BlackjackStand(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (state, error) = _blackjackService.Stand(gameId, discordId);
        if (error != null)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "blackjackStand", success = false, error });
            return;
        }

        await PushBlackjackState(gameId);
    }

    public async Task BlackjackNewRound(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (state, error) = _blackjackService.StartNewRound(gameId, discordId);
        if (error != null)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "blackjackNewRound", success = false, error });
            return;
        }

        await PushBlackjackState(gameId);
    }

    public async Task BlackjackSendMessage(ulong gameId, string[] words)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (state, error) = _blackjackService.ComposeMessage(gameId, discordId, words);
        if (error != null)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "blackjackSendMessage", success = false, error });
            return;
        }

        // error is null, state contains the updated table — the message text is in the second return value
        // Inject the message into the active game's global logs
        var message = string.Join(" ", words);
        await InjectGlobalLogMessage(gameId, state, message);

        await PushBlackjackState(gameId);
    }

    private async Task PushBlackjackState(ulong gameId)
    {
        // Send personalized state to each player at the table
        var table = _blackjackService.GetTableState(gameId, 0);
        if (table == null) return;

        foreach (var bjPlayer in table.Players)
        {
            if (!ulong.TryParse(bjPlayer.DiscordId, out var pid)) continue;
            var personalState = _blackjackService.GetTableState(gameId, pid);
            if (personalState == null) continue;

            var connections = _notificationService.GetConnections(pid);
            if (connections.Count > 0)
                await Clients.Clients(connections.ToList()).SendAsync("BlackjackState", personalState);
        }
    }

    private async Task InjectGlobalLogMessage(ulong gameId, DTOs.BlackjackTableStateDto tableState, string message)
    {
        // Find the author from the table state (the player who just sent the message)
        var author = tableState?.LastMessage?.Author ?? "???";
        var logEntry = $"[Шинигами] {author}: \"{message}\"";

        // Try to add to active game's global logs
        var game = _global.GamesList.Find(x => x.GameId == gameId);
        if (game != null)
        {
            game.AddGlobalLogs(logEntry);
            await _notificationService.BroadcastGameState(game);
        }

        // Also broadcast as a game event so spectators see it
        await _notificationService.SendGameEvent(gameId, "BlackjackMessage", new { author, message });
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
