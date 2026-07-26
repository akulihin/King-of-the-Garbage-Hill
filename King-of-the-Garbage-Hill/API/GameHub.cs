using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.Services;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using King_of_the_Garbage_Hill.Helpers;
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
    private static readonly ConcurrentDictionary<string, byte> BattleshipBotPumps = new();
    private readonly WebGameService _gameService;
    private readonly GameNotificationService _notificationService;
    private readonly Global _global;
    private readonly UserAccounts _userAccounts;
    private readonly BlackjackService _blackjackService;
    private readonly GameStoryService _storyService;
    private readonly BattleshipService _battleshipService;
    private readonly CharactersPull _charactersPull;
    private readonly AdminLobbyService _adminLobbyService;

    public GameHub(WebGameService gameService, GameNotificationService notificationService,
        Global global, UserAccounts userAccounts, BlackjackService blackjackService,
        GameStoryService storyService, BattleshipService battleshipService,
        CharactersPull charactersPull, AdminLobbyService adminLobbyService)
    {
        _gameService = gameService;
        _notificationService = notificationService;
        _global = global;
        _userAccounts = userAccounts;
        _blackjackService = blackjackService;
        _storyService = storyService;
        _battleshipService = battleshipService;
        _charactersPull = charactersPull;
        _adminLobbyService = adminLobbyService;
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
            await _adminLobbyService.HandleDisconnectedAsync(discordId);
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

        BindConnectionToPlayer(discordId);

        // Return the ID as a string so JS doesn't lose precision, include playerType for admin checks
        var account = _userAccounts.GetAccount(discordId);
        var playerType = account?.PlayerType ?? 0;
        var lastPlayedCharacter = account?.CharacterPlayedLastTime ?? "";
        if (UnknownBug.Is(lastPlayedCharacter)) lastPlayedCharacter = "";
        await Clients.Caller.SendAsync("Authenticated", new
        {
            success = true,
            discordId = discordIdStr,
            playerType,
            lastPlayedCharacter,
            isGodAdmin = AdminLobbyService.IsGodAdmin(discordId),
        });
        await Clients.Caller.SendAsync(
            "AdminLobbyReserved",
            new { reserved = _adminLobbyService.IsReserved(discordId) });
        Console.WriteLine($"[WebAPI] Connection {Context.ConnectionId} authenticated as Discord user {discordId}");
    }

    /// <summary>Persist the viewer locale without changing canonical gameplay strings.</summary>
    public async Task SetLanguage(string language)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var normalized = GameLocalization.Normalize(language);
        var account = _userAccounts.GetAccount(discordId);
        if (account != null) account.Language = normalized;
        GameLocalization.SetUserLanguage(discordId, normalized);
        await Clients.Caller.SendAsync("LanguageChanged", new { language = normalized });
        if (Context.Items.TryGetValue("gameId", out var gameIdValue) && gameIdValue is ulong gameId)
            await PushStateToPlayer(gameId, discordId);
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

        // Send stored story if available (covers reconnect / late join)
        var story = _storyService.GetStory(gameId);
        if (story != null)
            await Clients.Caller.SendAsync("GameEvent", new { eventType = "GameStory", data = new { story } });
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

        // Authenticate this connection with the new web ID.
        BindConnectionToPlayer(webId);

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

    public async Task AnnounceHalfLife3(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.AnnounceHalfLife3(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "announceHalfLife3", success, error });
        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task WakeGordon(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.WakeGordon(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "wakeGordon", success, error });
        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task ResolveHalfLife3Decision(ulong gameId, int serial, string choice)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.ResolveHalfLife3Decision(
            gameId, discordId, serial, choice);
        await Clients.Caller.SendAsync("ActionResult", new { action = "resolveHalfLife3Decision", success, error });
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

    public async Task DemandContractReward(ulong gameId, string demandType)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _gameService.DemandContractReward(gameId, discordId, demandType);
        await Clients.Caller.SendAsync("ActionResult", new { action = "demandContractReward", success, error });

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

    // ── Draft Pick ──────────────────────────────────────────────────

    public async Task DraftSelect(ulong gameId, string characterName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = await _gameService.DraftSelect(gameId, discordId, characterName);
        await Clients.Caller.SendAsync("ActionResult", new { action = "draftSelect", success, error });

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

    public async Task DepthsCallChoice(ulong gameId, bool agree)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _gameService.DepthsCallChoice(gameId, discordId, agree);
        await Clients.Caller.SendAsync("ActionResult",
            new { action = "depthsCallChoice", success, error });

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

    public async Task DoomRoll(ulong gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        var (success, error) = await _gameService.DoomRoll(gameId, discordId);
        await Clients.Caller.SendAsync("ActionResult", new { action = "doomRoll", success, error });
        if (success) await PushStateToPlayer(gameId, discordId);
    }

    public async Task DoomChainsaw(ulong gameId, string passiveName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        var (success, error) = await _gameService.DoomChainsaw(gameId, discordId, passiveName);
        await Clients.Caller.SendAsync("ActionResult", new { action = "doomChainsaw", success, error });
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

    // ── Salldorum Actions ───────────────────────────────────────────────

    public async Task RewriteHistory(ulong gameId, int roundNumber)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _gameService.RewriteHistory(gameId, discordId, roundNumber);
        await Clients.Caller.SendAsync("ActionResult", new { action = "rewriteHistory", success, error });

        if (success) await PushStateToPlayer(gameId, discordId);
    }

    // ── Admin: Test Game ──────────────────────────────────────────────

    /// <summary>
    /// Returns the public character list, extended with private test choices for admins.
    /// </summary>
    public async Task GetCharacterList()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        var characters = _gameService.GetCharacterList(account?.PlayerType == 2);
        await Clients.Caller.SendAsync("CharacterList", characters);
    }

    /// <summary>
    /// Create a test game with a specific character (admin only, PlayerType == 2).
    /// </summary>
    public async Task CreateTestGame(string characterName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        if (account == null || account.PlayerType != 2)
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var username = account.DiscordUserName ?? "Admin";
        var (gameId, error) = await _gameService.CreateTestGame(discordId, username, characterName);
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

    // ── God Admin: Curated Lobby ─────────────────────────────────────

    public async Task CreateAdminLobby()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (state, error) = _adminLobbyService.CreateAdminLobby(discordId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyState", state);
    }

    public async Task RequestAdminLobbyState()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (state, error) = _adminLobbyService.RequestState(discordId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyState", state);
    }

    public async Task RequestAdminLobbyDirectory()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (directory, error) = await _adminLobbyService.GetDirectoryAsync(discordId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyDirectory", directory);
    }

    public async Task RequestAdminLobbyPresence()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (presence, error) = _adminLobbyService.GetPresence(discordId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyPresence", presence);
    }

    public async Task AdminLobbyInvitePlayer(int slotIndex, string discordIdStr)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }
        if (!ulong.TryParse(discordIdStr, out var inviteeId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid Discord ID.");
            return;
        }

        var (state, error) = await _adminLobbyService.InvitePlayerAsync(
            discordId, slotIndex, inviteeId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyState", state);
    }

    public async Task AdminLobbyAddBot(int slotIndex, int aiDifficulty)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (state, error) = _adminLobbyService.AddBot(
            discordId, slotIndex, aiDifficulty);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyState", state);
    }

    public async Task AdminLobbySetCharacter(int slotIndex, string characterName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (state, error) = _adminLobbyService.SetCharacter(
            discordId, slotIndex, characterName);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyState", state);
    }

    public async Task AdminLobbyRemoveSlot(int slotIndex)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (state, error) = await _adminLobbyService.RemoveSlotAsync(
            discordId, slotIndex);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyState", state);
    }

    public async Task AdminLobbyStart()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var (_, error) = await _adminLobbyService.StartGameAsync(discordId);
        if (error != null)
            await Clients.Caller.SendAsync("Error", error);
    }

    public async Task AdminLobbyCancel()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        if (!AdminLobbyService.IsGodAdmin(discordId))
        {
            await Clients.Caller.SendAsync("Error", "Admin access required.");
            return;
        }

        var error = await _adminLobbyService.CancelAsync(discordId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("AdminLobbyState", (object)null);
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

    // ── Character Store ──────────────────────────────────────────────

    public async Task RequestStore()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (state, error) = _gameService.GetStoreState(discordId);
        if (error != null) { await Clients.Caller.SendAsync("Error", error); return; }
        await Clients.Caller.SendAsync("StoreState", state);
    }

    public async Task AdjustStoreCharacter(string characterName, int percentagePoints)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) throw new HubException("Not authenticated.");

        var (state, error) = _gameService.AdjustStoreCharacter(discordId, characterName, percentagePoints);
        if (error != null) throw new HubException(error);
        await Clients.Caller.SendAsync("StoreState", state);
    }

    public async Task ResetStoreCharacter(string characterName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) throw new HubException("Not authenticated.");

        var (state, error) = _gameService.ResetStoreCharacter(discordId, characterName);
        if (error != null) throw new HubException(error);
        await Clients.Caller.SendAsync("StoreState", state);
    }

    public async Task ResetStoreAllCharacters()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) throw new HubException("Not authenticated.");

        var (state, error) = _gameService.ResetStoreAllCharacters(discordId);
        if (error != null) throw new HubException(error);
        await Clients.Caller.SendAsync("StoreState", state);
    }

    // ── Quests ─────────────────────────────────────────────────────────

    public async Task RequestDoomFortress()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        var account = _userAccounts.GetAccount(discordId);
        if (account == null) { await Clients.Caller.SendAsync("Error", "Account not found."); return; }
        await Clients.Caller.SendAsync("DoomFortressState", BuildDoomFortressState(account));
    }

    public async Task EquipDoomModule(string stage, int slotIndex, string moduleName)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }
        var account = _userAccounts.GetAccount(discordId);
        if (account == null) { await Clients.Caller.SendAsync("Error", "Account not found."); return; }

        account.DoomFortress ??= new DoomFortressData();
        DoomGuy.EnsureFortress(account.DoomFortress);
        var module = DoomGuy.FindModule(moduleName);
        if (!DoomGuy.StageOrder.Contains(stage) || module == null || module.Stage != stage
            || !account.DoomFortress.UnlockedModules.Contains(moduleName) || slotIndex is < 0 or > 3)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "equipDoomModule", success = false, error = "Invalid module or slot" });
            return;
        }

        var slots = account.DoomFortress.EquippedSlots[stage];
        var oldIndex = slots.IndexOf(moduleName);
        if (oldIndex >= 0 && oldIndex != slotIndex)
        {
            // Moving an equipped module into an empty slot would create a new empty slot, which is forbidden.
            // A filled target swaps; an empty target leaves the existing valid loadout unchanged.
            if (!string.IsNullOrEmpty(slots[slotIndex]))
                (slots[oldIndex], slots[slotIndex]) = (slots[slotIndex], slots[oldIndex]);
        }
        else
            slots[slotIndex] = moduleName;

        await Clients.Caller.SendAsync("ActionResult", new { action = "equipDoomModule", success = true, error = (string)null });
        await Clients.Caller.SendAsync("DoomFortressState", BuildDoomFortressState(account));
    }

    private static DTOs.DoomFortressStateDto BuildDoomFortressState(Game.Classes.DiscordAccountClass account)
    {
        account.DoomFortress ??= new DoomFortressData();
        DoomGuy.EnsureFortress(account.DoomFortress);
        var dto = new DTOs.DoomFortressStateDto();
        foreach (var stage in DoomGuy.StageOrder)
        {
            var rewardModules = DoomGuy.GetStandardRewardModules(stage).ToList();
            var remaining = rewardModules.Count(x => !account.DoomFortress.UnlockedModules.Contains(x.Name));
            dto.Stages.Add(new DTOs.DoomFortressStageDto
            {
                Name = stage,
                Slots = account.DoomFortress.EquippedSlots[stage].ToList(),
                UnlockedModules = DoomGuy.Modules
                    .Where(x => x.Stage == stage && account.DoomFortress.UnlockedModules.Contains(x.Name))
                    .Select(x => new DTOs.DoomModuleDto
                    {
                        Name = x.Name, Stage = x.Stage, Description = x.Description, Reward = x.Reward,
                    }).ToList(),
                RewardModulesRemaining = remaining,
                CurrentDropChance = DoomGuy.RewardChance(rewardModules.Count, remaining),
            });
        }
        return dto;
    }

    public async Task RequestQuests()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        if (account == null)
        {
            await Clients.Caller.SendAsync("Error", "Account not found.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        DTOs.QuestStateDto questState = null;
        var persistenceFailed = false;
        lock (account)
        {
            var snapshot = Game.Classes.QuestService.CaptureAccountState(account);
            var changed = Game.Classes.QuestService.EnsureQuestsInitialized(account, now);
            if (changed && !_userAccounts.SaveAccount(account))
            {
                Game.Classes.QuestService.RestoreAccountState(account, snapshot);
                persistenceFailed = true;
            }
            else
            {
                questState = BuildQuestState(account, now);
            }
        }

        if (persistenceFailed)
            throw new HubException("Daily quests could not be saved. Please try again.");

        await Clients.Caller.SendAsync("QuestState", questState);
    }

    public async Task RerollDailyQuest(string questId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        if (account == null)
        {
            await Clients.Caller.SendAsync("Error", "Account not found.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        DTOs.QuestStateDto questState = null;
        string rerollError = null;
        var persistenceFailed = false;
        lock (account)
        {
            var snapshot = Game.Classes.QuestService.CaptureAccountState(account);
            if (!Game.Classes.QuestService.TryRerollDailyQuest(account, questId, now, out rerollError))
            {
                // Validation may have lazily initialized a new UTC day; an unsuccessful reroll
                // must not leave that unsaved mutation behind.
                Game.Classes.QuestService.RestoreAccountState(account, snapshot);
            }
            else if (!_userAccounts.SaveAccount(account))
            {
                Game.Classes.QuestService.RestoreAccountState(account, snapshot);
                persistenceFailed = true;
            }
            else
            {
                questState = BuildQuestState(account, now);
            }
        }

        if (rerollError != null) throw new HubException(rerollError);
        if (persistenceFailed)
            throw new HubException("The daily quest reroll could not be saved. Nothing changed; please try again.");

        await Clients.Caller.SendAsync("QuestState", questState);
    }

    /// <summary>The caller must hold the account monitor.</summary>
    private static DTOs.QuestStateDto BuildQuestState(DiscordAccountClass account, DateTimeOffset now)
    {
        var data = account.Quests;
        var active = data.ActiveDay;
        var weekly = data.WeeklyJourney;
        var lootBoxPity = Math.Clamp(data.LootBoxPity, 0, Game.Classes.QuestService.RarePityLimit - 1);
        var lastUnacknowledgedLootBox = Game.Classes.QuestService.GetLastUnacknowledgedLootBox(account);
        var completedQuestCount = active.Quests.Count(quest => quest.IsCompleted);

        return new DTOs.QuestStateDto
        {
            ActiveDate = active.Date,
            ServerNow = now.ToUniversalTime().ToString("o"),
            ResetsAt = Game.Classes.QuestService.GetResetAt(now).ToString("o"),
            AllCompletedToday = active.AllCompleted,
            DailyCompleted = active.DailyCompleted || active.BonusClaimed,
            CompletedQuestCount = completedQuestCount,
            DailyQuestRequirement = Game.Classes.QuestService.DailyQuestRequirement,
            DailyBonusZbs = Game.Classes.QuestService.DailyBonusZbs,
            DailyBonusGranted = active.DailyBonusGranted,
            MasteryBonusLootBoxes = Game.Classes.QuestService.MasteryBonusLootBoxes,
            MasteryBonusGranted = active.MasteryBonusGranted,
            RerollsRemaining = active.RerollsRemaining,
            StreakDays = data.StreakDays,
            BestStreakDays = data.BestStreakDays,
            WeeklyCompletedDays = weekly?.CompletedDates?.Distinct(StringComparer.Ordinal).Count() ?? 0,
            WeeklyTargetDays = Game.Classes.QuestService.WeeklyTargetDays,
            WeeklyRewardZbs = Game.Classes.QuestService.WeeklyRewardZbs,
            WeeklyRewardGranted = weekly?.RewardGranted ?? false,
            WeekEndsAt = Game.Classes.QuestService.GetWeekEndsAt(now).ToString("o"),
            ZbsPoints = account.ZbsPoints,
            PendingLootBoxes = account.PendingLootBoxes,
            LootBoxPity = lootBoxPity,
            GuaranteedRareIn = Game.Classes.QuestService.GetGuaranteedRareIn(lootBoxPity),
            LootBoxOdds = MapLootBoxOdds(),
            LastUnacknowledgedLootBox = MapLootBoxResult(
                lastUnacknowledgedLootBox,
                account.ZbsPoints,
                account.PendingLootBoxes),
            PendingGuaranteedCharacters = account.LootBoxCharacterQueue?.Count ?? 0,
            NextGuaranteedCharacterName = account.LootBoxCharacterQueue?.FirstOrDefault(),
            Quests = active.Quests
                .Select(quest => MapDailyQuestProgress(quest, active))
                .Where(quest => quest != null)
                .ToList(),
        };
    }

    private static DTOs.QuestProgressDto MapDailyQuestProgress(
        QuestProgress progress,
        DailyQuestState active)
    {
        var definition = Game.Classes.QuestService.GetDefinition(progress?.QuestId);
        if (definition == null) return null;

        return new DTOs.QuestProgressDto
        {
            Id = definition.Id,
            Name = definition.Name,
            NameRu = definition.NameRu,
            Description = definition.Description,
            DescriptionRu = definition.DescriptionRu,
            Lane = definition.Lane.ToString(),
            Icon = definition.Icon,
            Aggregation = definition.Aggregation.ToString(),
            Current = progress.Current,
            Target = progress.Target,
            IsCompleted = progress.IsCompleted,
            ZbsReward = progress.ZbsReward,
            RewardLootBoxes = progress.LootBoxReward,
            RewardGranted = progress.RewardGranted,
            CompletedAt = progress.CompletedAt?.ToString("o"),
            CanReroll = !progress.IsCompleted
                        && definition.Lane != QuestLane.Anchor
                        && active.RerollsRemaining > 0,
        };
    }

    // ── Loot Boxes ──────────────────────────────────────────────────

    /// <summary>
    /// Legacy one-shot opening used by cached pre-V2 clients, which have no acknowledgement call.
    /// The result is acknowledged in the same durable transaction so a second legacy open advances.
    /// </summary>
    public Task OpenLootBox() => OpenLootBoxCore(acknowledgeImmediately: true);

    /// <summary>
    /// Resumable V2 opening. The stored result remains pending until AcknowledgeLootBox succeeds.
    /// </summary>
    public Task OpenLootBoxV2() => OpenLootBoxCore(acknowledgeImmediately: false);

    private async Task OpenLootBoxCore(bool acknowledgeImmediately)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        if (account == null)
        {
            await Clients.Caller.SendAsync("Error", "Account not found.");
            return;
        }

        DTOs.LootBoxResultDto resultDto = null;
        string openError = null;
        var persistenceFailed = false;

        lock (account)
        {
            var snapshot = CaptureLootBoxAccountState(account);
            var outcome = Game.Classes.QuestService.OpenLootBox(
                account,
                0,
                _charactersPull.GetVisibleCharacters());
            if (outcome.Result == null)
            {
                openError = outcome.Error ?? "Unable to open loot box.";
            }
            else
            {
                resultDto = MapLootBoxResult(outcome.Result);

                if (acknowledgeImmediately)
                    Game.Classes.QuestService.AcknowledgeLootBox(account, outcome.Result.OpeningId);

                // Do not publish a debit/reward until its complete account state is durable.
                if (!_userAccounts.SaveAccount(account))
                {
                    RestoreLootBoxAccountState(account, snapshot);
                    resultDto = null;
                    persistenceFailed = true;
                }
            }
        }

        if (openError != null)
        {
            await Clients.Caller.SendAsync("Error", openError);
            return;
        }

        if (persistenceFailed)
            throw new HubException("The loot box could not be saved. Nothing was consumed; please try again.");

        await Clients.Caller.SendAsync("LootBoxOpened", resultDto);
    }

    public async Task AcknowledgeLootBox(string openingId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        if (account == null)
        {
            await Clients.Caller.SendAsync("Error", "Account not found.");
            return;
        }

        var persistenceFailed = false;
        lock (account)
        {
            var snapshot = CaptureLootBoxAccountState(account);

            // A stale or mismatched acknowledgement is intentionally a safe no-op.
            if (Game.Classes.QuestService.AcknowledgeLootBox(account, openingId)
                && !_userAccounts.SaveAccount(account))
            {
                RestoreLootBoxAccountState(account, snapshot);
                persistenceFailed = true;
            }
        }

        if (persistenceFailed)
            throw new HubException("The loot-box acknowledgement could not be saved. Please try again.");
    }

    // ── Achievements ──────────────────────────────────────────────────

    public async Task RequestAchievements()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        if (account == null)
        {
            await Clients.Caller.SendAsync("Error", "Account not found.");
            return;
        }

        var definitions = Game.Classes.AchievementService.AllAchievements;
        var liveIds = definitions.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        DTOs.AchievementBoardDto board;
        lock (account)
        {
            Game.Classes.AchievementService.EnsureInitialized(account);
            var unlockedDefinitions = definitions.Where(def =>
                account.Achievements.Progress.Any(progress =>
                    progress.AchievementId == def.Id && progress.IsUnlocked)).ToList();

            board = new DTOs.AchievementBoardDto
            {
                TotalAchievements = definitions.Count,
                TotalUnlocked = unlockedDefinitions.Count,
                NewlyUnlocked = account.Achievements.NewlyUnlocked
                    .Where(liveIds.Contains)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                EarnedRewardZbs = unlockedDefinitions.Sum(x => x.RewardZbs),
                TotalRewardZbs = definitions.Sum(x => x.RewardZbs),
                EarnedRewardLootBoxes = unlockedDefinitions.Sum(x => x.RewardLootBoxes),
                TotalRewardLootBoxes = definitions.Sum(x => x.RewardLootBoxes),
            };

            foreach (var def in definitions)
            {
                var progress = account.Achievements.Progress.Find(p => p.AchievementId == def.Id);
                board.Achievements.Add(MapAchievementEntry(def, progress));
            }
        }

        await Clients.Caller.SendAsync("AchievementBoard", board);
    }

    public async Task ClearNewAchievements()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) return;

        var account = _userAccounts.GetAccount(discordId);
        if (account == null) return;

        var persistenceFailed = false;
        lock (account)
        {
            var queue = account.Achievements?.NewlyUnlocked;
            if (queue?.Count > 0)
            {
                var snapshot = queue.ToList();
                queue.Clear();
                if (!_userAccounts.SaveAccount(account))
                {
                    queue.AddRange(snapshot);
                    persistenceFailed = true;
                }
            }
        }

        if (persistenceFailed)
            throw new HubException("The achievement acknowledgement could not be saved. Please try again.");
    }

    public async Task AcknowledgeAchievements(List<string> achievementIds)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        if (account == null)
        {
            await Clients.Caller.SendAsync("Error", "Account not found.");
            return;
        }

        var liveIds = Game.Classes.AchievementService.AllAchievements
            .Select(definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);
        var acknowledgedIds = (achievementIds ?? new List<string>())
            .Where(liveIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (acknowledgedIds.Count == 0) return;

        var persistenceFailed = false;
        lock (account)
        {
            var queue = account.Achievements?.NewlyUnlocked;
            if (queue != null)
            {
                var snapshot = queue.ToList();
                if (queue.RemoveAll(acknowledgedIds.Contains) > 0
                    && !_userAccounts.SaveAccount(account))
                {
                    queue.Clear();
                    queue.AddRange(snapshot);
                    persistenceFailed = true;
                }
            }
        }

        if (persistenceFailed)
            throw new HubException("The achievement acknowledgement could not be saved. Please try again.");
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

    // ── Battleship (Sea Battle Mini-Game) ──────────────────────────────

    public async Task RequestBattleshipLobby()
    {
        var state = _battleshipService.GetLobbyState();
        await Clients.Caller.SendAsync("BattleshipLobby", state);
    }

    public async Task RequestBattleshipStats()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        var stats = _battleshipService.GetPlayerStats(account);
        if (stats != null)
            await Clients.Caller.SendAsync("BattleshipStats", stats);
    }

    public async Task CreateBattleshipGame()
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        var username = account?.DiscordUserName ?? "Player";

        var (gameId, error) = _battleshipService.CreateGame(discordId.ToString(), username);
        if (error != null)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"bs-{gameId}");
        await Clients.Caller.SendAsync("BattleshipGameCreated", new { gameId });

        // Push state to creator
        await PushBattleshipStateToPlayer(gameId, discordId.ToString());
    }

    public async Task JoinBattleshipWebGame(string gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var account = _userAccounts.GetAccount(discordId);
        var username = account?.DiscordUserName ?? "Player";

        var (success, error) = _battleshipService.JoinGame(gameId, discordId.ToString(), username);
        if (!success)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"bs-{gameId}");
        await Clients.Caller.SendAsync("BattleshipGameJoined", new { gameId });
        await PushBattleshipStateToAll(gameId);
    }

    public async Task LeaveBattleshipWebGame(string gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.LeaveGame(gameId, discordId.ToString());
        if (!success)
        {
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"bs-{gameId}");
        await PushBattleshipStateToAll(gameId);
    }

    public async Task JoinBattleshipGame(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"bs-{gameId}");

        var discordId = GetDiscordId();
        var did = discordId == 0 ? null : discordId.ToString();
        var state = did != null
            ? _battleshipService.GetGameState(gameId, did)
            : _battleshipService.GetSpectatorState(gameId);

        if (state != null)
            await Clients.Caller.SendAsync("BattleshipState", state);
    }

    public async Task LeaveBattleshipGame(string gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"bs-{gameId}");
    }

    public async Task BattleshipConfirmReady(string gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.ConfirmReady(gameId, discordId.ToString());
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipConfirmReady", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
        await RunBattleshipBotPump(gameId);
    }

    public async Task BattleshipSelectArmy(string gameId, string faction)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.SelectArmy(gameId, discordId.ToString(), faction);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipSelectArmy", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
    }

    public async Task BattleshipSelectFleet(string gameId, List<FleetSelectionDto> selections)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var fleetSelections = selections?.Select(s => new Battleship.Models.FleetSelection
        {
            DefinitionId = s.DefinitionId,
            ShipName = s.ShipName,
            Cost = s.Cost,
            Upgrades = s.Upgrades ?? new(),
        }).ToList() ?? new();

        var (success, error) = _battleshipService.SelectFleet(gameId, discordId.ToString(), fleetSelections);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipSelectFleet", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
    }

    public async Task BattleshipPlaceShip(string gameId, string shipId, int row, int col, string orientation)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.PlaceShip(gameId, discordId.ToString(), shipId, row, col, orientation);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipPlaceShip", success = false, error });
            return;
        }

        await PushBattleshipStateToPlayer(gameId, discordId.ToString());
    }

    public async Task BattleshipRemoveShip(string gameId, string shipId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.RemoveShip(gameId, discordId.ToString(), shipId);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipRemoveShip", success = false, error });
            return;
        }

        await PushBattleshipStateToPlayer(gameId, discordId.ToString());
    }

    public async Task BattleshipConfirmPlacement(string gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.ConfirmPlacement(gameId, discordId.ToString());
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipConfirmPlacement", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
        await RunBattleshipBotPump(gameId);
    }

    public async Task BattleshipShoot(string gameId, int row, int col)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (result, error) = _battleshipService.Shoot(gameId, discordId.ToString(), row, col);
        if (error != null)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipShoot", success = false, error });
            await Clients.Caller.SendAsync("Error", error);
            await PushBattleshipStateToPlayer(gameId, discordId.ToString());
            return;
        }

        await SendBattleshipShotEvent(gameId, result);

        await PushBattleshipStateToAll(gameId);
        await RunBattleshipBotPump(gameId);
    }

    public async Task BattleshipShootOwnBoard(string gameId, int row, int col)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (result, error) = _battleshipService.ShootOwnBoard(gameId, discordId.ToString(), row, col);
        if (error != null)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipShootOwnBoard", success = false, error });
            await Clients.Caller.SendAsync("Error", error);
            await PushBattleshipStateToPlayer(gameId, discordId.ToString());
            return;
        }

        await SendBattleshipShotEvent(gameId, result);

        await PushBattleshipStateToAll(gameId);
        await RunBattleshipBotPump(gameId);
    }

    public async Task BattleshipSelectWeapon(string gameId, string weaponType, string shotType, string weaponId = null)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.SelectWeapon(gameId, discordId.ToString(), weaponType, shotType, weaponId);
        if (!success)
        {
            await Clients.Caller.SendAsync("Error", error);
            await PushBattleshipStateToPlayer(gameId, discordId.ToString());
            return;
        }
        await PushBattleshipStateToPlayer(gameId, discordId.ToString());
    }

    public async Task BattleshipPassBoardingTurn(string gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.PassBoardingTurn(gameId, discordId.ToString());
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipPassBoardingTurn", success = false, error });
            return;
        }
        await PushBattleshipStateToAll(gameId);
        await RunBattleshipBotPump(gameId);
    }

    public async Task BattleshipDeploySummon(string gameId, string summonType, int col)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.DeploySummon(gameId, discordId.ToString(), summonType, col);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipDeploySummon", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
    }

    public async Task BattleshipDeployPendingSummon(string gameId, string pendingId, int col)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.DeployPendingSummon(gameId, discordId.ToString(), pendingId, col);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipDeployPendingSummon", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
        await RunBattleshipBotPump(gameId);
    }

    public async Task BattleshipManualMove(string gameId, string shipId, string direction, int distance = 1)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.ManualMoveShip(gameId, discordId.ToString(), shipId, direction, distance);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipManualMove", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
    }

    public async Task BattleshipSetCursedBoatDirection(string gameId, string summonId, string direction)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.SetCursedBoatDirection(gameId, discordId.ToString(), summonId, direction);
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipSetCursedBoatDirection", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
    }

    public async Task BattleshipForfeit(string gameId)
    {
        var discordId = GetDiscordId();
        if (discordId == 0) { await SendNotAuthenticated(); return; }

        var (success, error) = _battleshipService.Forfeit(gameId, discordId.ToString());
        if (!success)
        {
            await Clients.Caller.SendAsync("ActionResult", new { action = "battleshipForfeit", success = false, error });
            return;
        }

        await PushBattleshipStateToAll(gameId);
    }

    public async Task RequestBattleshipState(string gameId)
    {
        var discordId = GetDiscordId();
        var did = discordId == 0 ? null : discordId.ToString();
        var state = did != null
            ? _battleshipService.GetGameState(gameId, did)
            : _battleshipService.GetSpectatorState(gameId);

        if (state != null)
            await Clients.Caller.SendAsync("BattleshipState", state);
    }

    public async Task RequestShipCatalog()
    {
        var catalog = _battleshipService.GetShipCatalog();
        await Clients.Caller.SendAsync("ShipCatalog", catalog);
    }

    private async Task PushBattleshipStateToAll(string gameId)
    {
        // Send personalized state to each player, collecting their connection IDs
        var playerIds = _battleshipService.GetPlayerIds(gameId);
        var playerConnectionIds = new List<string>();
        foreach (var pid in playerIds)
        {
            if (!ulong.TryParse(pid, out var discordId)) continue;
            var personalState = _battleshipService.GetGameState(gameId, pid);
            if (personalState == null) continue;

            var connections = _notificationService.GetConnections(discordId);
            if (connections.Count > 0)
            {
                var connList = connections.ToList();
                playerConnectionIds.AddRange(connList);
                await Clients.Clients(connList).SendAsync("BattleshipState", personalState);
            }
        }

        // Send spectator state to the group, excluding player connections
        var spectatorState = _battleshipService.GetSpectatorState(gameId);
        if (spectatorState != null)
        {
            if (playerConnectionIds.Count > 0)
                await Clients.GroupExcept($"bs-{gameId}", playerConnectionIds).SendAsync("BattleshipState", spectatorState);
            else
                await Clients.Group($"bs-{gameId}").SendAsync("BattleshipState", spectatorState);
        }
    }

    private async Task PushBattleshipStateToPlayer(string gameId, string discordId)
    {
        if (!ulong.TryParse(discordId, out var did)) return;
        var state = _battleshipService.GetGameState(gameId, discordId);
        if (state == null) return;

        var connections = _notificationService.GetConnections(did);
        if (connections.Count > 0)
            await Clients.Clients(connections.ToList()).SendAsync("BattleshipState", state);
    }

    private async Task SendBattleshipShotEvent(string gameId, Battleship.Models.ShotResult result)
    {
        if (result == null) return;
        await Clients.Group($"bs-{gameId}").SendAsync("BattleshipEvent", new
        {
            eventType = "ShotResult",
            data = new
            {
                result.WasSkipped,
                result.Hit,
                result.Miss,
                result.Scratched,
                result.Destroyed,
                result.ShipSunk,
                result.Burned,
                result.Dodged,
                result.Row,
                result.Col,
                result.TurnContinues,
                result.ShotDelayMs,
                result.Message,
                result.AffectedShipName,
                result.SourceShipId,
                result.SourceDeckIndex,
                result.SourceRow,
                result.SourceCol,
                result.SourceBoardPlayerId,
                result.ProjectileType,
                result.TargetPlayerId,
            }
        });
    }

    private async Task RunBattleshipBotPump(string gameId)
    {
        if (!BattleshipBotPumps.TryAdd(gameId, 0)) return;
        try
        {
            var delayBeforeNextStepMs = 0;
            while (_battleshipService.IsBotTurn(gameId))
            {
                // Misses hand control over immediately. Reset hits wait for the exact
                // server-selected 2/8-second interval while the game lock stays free.
                if (delayBeforeNextStepMs > 0)
                    await Task.Delay(delayBeforeNextStepMs);
                var step = _battleshipService.ProcessBotStep(gameId);
                if (!step.Acted) break;
                if (step.Shot != null)
                    await SendBattleshipShotEvent(gameId, step.Shot);
                await PushBattleshipStateToAll(gameId);
                delayBeforeNextStepMs = step.Shot?.ShotDelayMs ?? 0;
            }
        }
        finally
        {
            BattleshipBotPumps.TryRemove(gameId, out _);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────

    private static List<DTOs.LootBoxOddsDto> MapLootBoxOdds()
    {
        return Game.Classes.QuestService.LootBoxOdds.Select(tier => new DTOs.LootBoxOddsDto
        {
            Rarity = tier.Rarity,
            Chance = tier.Chance,
            MinZbs = tier.MinZbs,
            MaxZbs = tier.MaxZbs,
            RollWeightBonusPercentagePoints = tier.RollWeightBonusPercentagePoints,
            GuaranteedCharacterMaxTier = tier.GuaranteedCharacterMaxTier,
        }).ToList();
    }

    private static DTOs.LootBoxResultDto MapLootBoxResult(
        LootBoxResult result,
        int? currentZbsBalance = null,
        int? currentPendingLootBoxes = null)
    {
        if (result == null) return null;

        return new DTOs.LootBoxResultDto
        {
            OpeningId = result.OpeningId,
            Rarity = result.Rarity,
            ZbsAmount = result.ZbsAmount,
            ZbsBalance = currentZbsBalance ?? result.ZbsBalance,
            RemainingLootBoxes = currentPendingLootBoxes ?? result.RemainingLootBoxes,
            OpenedAt = result.Timestamp.ToString("o"),
            WasPityUpgrade = result.WasPityUpgrade,
            LootBoxPity = result.LootBoxPity,
            GuaranteedRareIn = result.GuaranteedRareIn,
            CharacterName = result.CharacterName,
            CharacterAvatar = result.CharacterAvatar,
            CharacterTier = result.CharacterTier,
            RollWeightBonusPercentagePoints = result.RollWeightBonusPercentagePoints,
            GuaranteedForNextGame = result.GuaranteedForNextGame,
            PendingGuaranteedCharacters = result.PendingGuaranteedCharacters,
        };
    }

    private sealed class LootBoxAccountState
    {
        public int ZbsPoints { get; init; }
        public int PendingLootBoxes { get; init; }
        public QuestData Quests { get; init; }
        public LootBoxResult LastLootBox { get; init; }
        public bool LastLootBoxAcknowledged { get; init; }
        public ulong LastLootBoxGameId { get; init; }
        public int LootBoxPity { get; init; }
        public List<DiscordAccountClass.CharacterChances> CharacterChance { get; init; }
        public List<string> SeenCharacters { get; init; }
        public List<string> LootBoxCharacterQueue { get; init; }
    }

    /// <summary>The caller must hold the account monitor.</summary>
    private static LootBoxAccountState CaptureLootBoxAccountState(DiscordAccountClass account)
    {
        var quests = account.Quests;
        return new LootBoxAccountState
        {
            ZbsPoints = account.ZbsPoints,
            PendingLootBoxes = account.PendingLootBoxes,
            Quests = quests,
            LastLootBox = quests?.LastLootBox,
            LastLootBoxAcknowledged = quests?.LastLootBox?.Acknowledged ?? false,
            LastLootBoxGameId = quests?.LastLootBoxGameId ?? 0,
            LootBoxPity = quests?.LootBoxPity ?? 0,
            CharacterChance = account.CharacterChance?.Select(chance =>
            {
                var copy = new DiscordAccountClass.CharacterChances(
                    chance.CharacterName,
                    chance.Multiplier)
                {
                    Changes = chance.Changes,
                    LootBoxBonusPercentagePoints = chance.LootBoxBonusPercentagePoints,
                };
                return copy;
            }).ToList(),
            SeenCharacters = account.SeenCharacters?.ToList(),
            LootBoxCharacterQueue = account.LootBoxCharacterQueue?.ToList(),
        };
    }

    /// <summary>The caller must hold the account monitor.</summary>
    private static void RestoreLootBoxAccountState(
        DiscordAccountClass account,
        LootBoxAccountState snapshot)
    {
        account.ZbsPoints = snapshot.ZbsPoints;
        account.PendingLootBoxes = snapshot.PendingLootBoxes;
        account.Quests = snapshot.Quests;
        account.CharacterChance = snapshot.CharacterChance;
        account.SeenCharacters = snapshot.SeenCharacters;
        account.LootBoxCharacterQueue = snapshot.LootBoxCharacterQueue;

        if (snapshot.Quests == null) return;

        snapshot.Quests.LastLootBox = snapshot.LastLootBox;
        snapshot.Quests.LastLootBoxGameId = snapshot.LastLootBoxGameId;
        snapshot.Quests.LootBoxPity = snapshot.LootBoxPity;
        if (snapshot.LastLootBox != null)
            snapshot.LastLootBox.Acknowledged = snapshot.LastLootBoxAcknowledged;
    }

    private static DTOs.AchievementEntryDto MapAchievementEntry(
        AchievementDefinition definition, AchievementProgress progress)
    {
        var isUnlocked = progress?.IsUnlocked ?? false;
        var maskSecret = definition.IsSecret && !isUnlocked;

        return new DTOs.AchievementEntryDto
        {
            Id = maskSecret ? GetOpaqueAchievementId(definition.Id) : definition.Id,
            Name = maskSecret ? "???" : definition.Name,
            NameRu = maskSecret ? "???" : definition.NameRu,
            Description = maskSecret ? definition.SecretHint : definition.Description,
            DescriptionRu = maskSecret ? definition.SecretHintRu : definition.DescriptionRu,
            SecretHint = definition.SecretHint,
            SecretHintRu = definition.SecretHintRu,
            Category = definition.Category.ToString(),
            IsSecret = definition.IsSecret,
            Icon = definition.Icon,
            Rarity = definition.Rarity,
            CharacterNames = maskSecret
                ? new List<string>()
                : definition.CharacterNames?.ToList() ?? new List<string>(),
            RewardZbs = definition.RewardZbs,
            RewardLootBoxes = definition.RewardLootBoxes,
            Target = definition.Target,
            Current = progress?.Current ?? 0,
            IsUnlocked = isUnlocked,
            UnlockedAt = progress?.UnlockedAt?.ToString("o"),
        };
    }

    private static string GetOpaqueAchievementId(string achievementId)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"kotgh:hidden-achievement:{achievementId}"));
        return $"hidden-{Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant()}";
    }

    private void BindConnectionToPlayer(ulong discordId)
    {
        if (Context.Items.TryGetValue("discordId", out var previousDiscordIdObj)
            && previousDiscordIdObj is ulong previousDiscordId
            && previousDiscordId != discordId)
        {
            _notificationService.RemoveConnection(previousDiscordId, Context.ConnectionId);
        }

        Context.Items["discordId"] = discordId;
        _notificationService.RegisterConnection(discordId, Context.ConnectionId);
    }

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
