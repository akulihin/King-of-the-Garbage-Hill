using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using Microsoft.AspNetCore.SignalR;

namespace King_of_the_Garbage_Hill.API.Services;

public sealed class AdminLobbyService
{
    private const ulong WebOnlyIdStart = 9_000_000_000_000_000_000;
    private static readonly TimeSpan DirectoryTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OwnerDisconnectGrace = TimeSpan.FromSeconds(30);
    private const string InviteDm =
        "Вы были избраны богом.\nhttps://kotgh.ozvmusic.com";
    private const string CancellationDm =
        "Бог передумал. Ваше место в админской игре освобождено.\nhttps://kotgh.ozvmusic.com";

    private static readonly HashSet<ulong> GodAdminIds =
        new() { 238337696316129280, 181514288278536193 };

    private readonly Global _global;
    private readonly UserAccounts _userAccounts;
    private readonly CharactersPull _charactersPull;
    private readonly GameNotificationService _notificationService;
    private readonly WebGameService _webGameService;
    private readonly StartGameLogic _startGameLogic;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ConcurrentDictionary<ulong, AdminLobby> _lobbies = new();
    private readonly ConcurrentDictionary<ulong, ulong> _reservations = new();
    private readonly SemaphoreSlim _directoryBuildLock = new(1, 1);

    private AdminLobbyDirectoryDto _directorySnapshot;
    private DateTime _directorySnapshotUtc;

    public AdminLobbyService(
        Global global,
        UserAccounts userAccounts,
        CharactersPull charactersPull,
        GameNotificationService notificationService,
        WebGameService webGameService,
        StartGameLogic startGameLogic,
        IHubContext<GameHub> hubContext)
    {
        _global = global;
        _userAccounts = userAccounts;
        _charactersPull = charactersPull;
        _notificationService = notificationService;
        _webGameService = webGameService;
        _startGameLogic = startGameLogic;
        _hubContext = hubContext;
    }

    public static bool IsGodAdmin(ulong id) => GodAdminIds.Contains(id);

    public bool IsReserved(ulong discordId) => _reservations.ContainsKey(discordId);

    public (AdminLobbyStateDto State, string Error) CreateAdminLobby(ulong ownerId)
    {
        if (IsReserved(ownerId))
            return (null, "Вы были избраны богом.");
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");

        var ownerAccount = _userAccounts.GetAccount(ownerId);
        if (ownerAccount == null)
            return (null, "Account not found");
        if (ownerAccount.IsPlaying)
            return (null, "Already in a game");

        var lobby = _lobbies.GetOrAdd(ownerId, _ =>
        {
            var slots = Enumerable.Range(0, 6)
                .Select(_ => new AdminLobbySlot())
                .ToArray();
            slots[0] = new AdminLobbySlot
            {
                Kind = "human",
                DiscordId = ownerId,
                Username = ownerAccount.DiscordUserName ?? ownerId.ToString(),
            };
            return new AdminLobby { OwnerId = ownerId, Slots = slots };
        });

        lock (lobby)
        {
            if (lobby.IsClosed)
                return (null, "Admin lobby not found");
            lobby.OwnerDisconnectedUtc = null;
            return (BuildState(lobby), null);
        }
    }

    public (AdminLobbyStateDto State, string Error) RequestState(ulong ownerId)
    {
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");
        if (!_lobbies.TryGetValue(ownerId, out var lobby))
            return (null, null);

        lock (lobby)
        {
            if (lobby.IsClosed)
                return (null, null);
            lobby.OwnerDisconnectedUtc = null;
            return (BuildState(lobby), null);
        }
    }

    public async Task<(AdminLobbyDirectoryDto Directory, string Error)> GetDirectoryAsync(
        ulong ownerId)
    {
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");
        if (!_lobbies.ContainsKey(ownerId))
            return (null, "Admin lobby not found");

        var snapshot = _directorySnapshot;
        if (snapshot == null || DateTime.UtcNow - _directorySnapshotUtc > DirectoryTtl)
        {
            await _directoryBuildLock.WaitAsync();
            try
            {
                snapshot = _directorySnapshot;
                if (snapshot == null || DateTime.UtcNow - _directorySnapshotUtc > DirectoryTtl)
                {
                    snapshot = await BuildDirectorySnapshotAsync();
                    _directorySnapshot = snapshot;
                    _directorySnapshotUtc = DateTime.UtcNow;
                }
            }
            finally
            {
                _directoryBuildLock.Release();
            }
        }

        return (RefreshDirectoryVolatileState(snapshot), null);
    }

    public (AdminLobbyPresenceDto Presence, string Error) GetPresence(ulong ownerId)
    {
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");
        if (!_lobbies.ContainsKey(ownerId))
            return (null, "Admin lobby not found");

        var accounts = _userAccounts.GetAllAccount()
            .Where(account => account.PlayerType != 404 && account.DiscordId > 1_000_000)
            .ToList();
        return (new AdminLobbyPresenceDto
        {
            OnlineIds = _notificationService.GetOnlinePlayerIds()
                .Select(id => id.ToString())
                .OrderBy(id => id)
                .ToList(),
            BusyIds = accounts.Where(account => account.IsPlaying)
                .Select(account => account.DiscordId.ToString())
                .OrderBy(id => id)
                .ToList(),
            ReservedIds = _reservations.Keys
                .Select(id => id.ToString())
                .OrderBy(id => id)
                .ToList(),
        }, null);
    }

    public async Task<(AdminLobbyStateDto State, string Error)> InvitePlayerAsync(
        ulong ownerId,
        int slotIndex,
        ulong inviteeId)
    {
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");
        if (!_lobbies.TryGetValue(ownerId, out var lobby))
            return (null, "Admin lobby not found");

        AdminLobbySlot invitedSlot;
        lock (lobby)
        {
            var slotError = ValidateMutableSlot(lobby, slotIndex, requireEmpty: true);
            if (slotError != null)
                return (null, slotError);
            if (inviteeId == ownerId)
                return (null, "The owner is already seated");

            // Despite its legacy name, this is a dictionary-only lookup: forged IDs must
            // not create Discord or web-only accounts as a side effect of an invitation.
            var account = _userAccounts.GetOrAddUserAccount(inviteeId);
            if (account == null || account.PlayerType == 404 || inviteeId <= 1_000_000)
                return (null, "User is not available");
            if (account.IsPlaying)
                return (null, "пользователь уже играет");
            if (!_reservations.TryAdd(inviteeId, ownerId))
                return (null, "User is already reserved");

            invitedSlot = new AdminLobbySlot
            {
                Kind = "human",
                DiscordId = inviteeId,
                Username = account.DiscordUserName ?? inviteeId.ToString(),
            };
            lobby.Slots[slotIndex] = invitedSlot;
        }

        var notifiedByDm = false;
        var unreachable = false;
        var connections = _notificationService.GetConnections(inviteeId);
        if (connections.Count > 0)
        {
            try
            {
                await _hubContext.Clients.Clients(connections.ToList())
                    .SendAsync("AdminLobbyReserved", new { reserved = true });
            }
            catch
            {
                unreachable = true;
            }
        }
        else
        {
            var discordUser = _global.Client.GetUser(inviteeId);
            if (discordUser == null)
            {
                unreachable = true;
            }
            else
            {
                try
                {
                    await discordUser.SendMessageAsync(InviteDm);
                    notifiedByDm = true;
                }
                catch
                {
                    unreachable = true;
                }
            }
        }

        AdminLobbyStateDto state;
        var stillSeated = false;
        lock (lobby)
        {
            stillSeated = ReferenceEquals(lobby.Slots[slotIndex], invitedSlot);
            if (stillSeated)
            {
                invitedSlot.NotifiedByDm = notifiedByDm;
                invitedSlot.IsUnreachable = unreachable;
            }
            state = BuildState(lobby);
        }

        if (!stillSeated)
        {
            invitedSlot.NotifiedByDm = notifiedByDm;
            await NotifyReservationReleasedAsync(invitedSlot, cancellation: true);
        }
        return (state, null);
    }

    public (AdminLobbyStateDto State, string Error) AddBot(
        ulong ownerId,
        int slotIndex,
        int aiDifficulty)
    {
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");
        if (!_lobbies.TryGetValue(ownerId, out var lobby))
            return (null, "Admin lobby not found");
        if (aiDifficulty is not (1 or 2 or 3))
            return (null, "AI difficulty must be 1, 2, or 3");

        lock (lobby)
        {
            var slotError = ValidateMutableSlot(lobby, slotIndex, requireEmpty: true);
            if (slotError != null)
                return (null, slotError);
            lobby.Slots[slotIndex] = new AdminLobbySlot
            {
                Kind = "bot",
                Username = $"BOT {slotIndex + 1}",
                AiDifficulty = aiDifficulty,
            };
            return (BuildState(lobby), null);
        }
    }

    public (AdminLobbyStateDto State, string Error) SetCharacter(
        ulong ownerId,
        int slotIndex,
        string characterName)
    {
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");
        if (!_lobbies.TryGetValue(ownerId, out var lobby))
            return (null, "Admin lobby not found");

        characterName ??= "";
        lock (lobby)
        {
            var slotError = ValidateMutableSlot(lobby, slotIndex, requireEmpty: false);
            if (slotError != null)
                return (null, slotError);
            if (lobby.Slots[slotIndex].Kind == "empty")
                return (null, "Seat a player or bot first");

            if (characterName.Length > 0
                && _charactersPull.GetAdminSelectableCharacters()
                    .All(character => character.Name != characterName))
                return (null, "Character not found");

            if (characterName.Length > 0 && lobby.Slots
                    .Where((_, index) => index != slotIndex)
                    .Any(slot => !string.IsNullOrEmpty(slot.CharacterName)
                                 && (slot.CharacterName == characterName
                                     || StartGameLogic.AreMutuallyExclusiveCharacters(
                                         slot.CharacterName, characterName))))
                return (null, "Character conflicts with another seat");

            lobby.Slots[slotIndex].CharacterName = characterName;
            return (BuildState(lobby), null);
        }
    }

    public async Task<(AdminLobbyStateDto State, string Error)> RemoveSlotAsync(
        ulong ownerId,
        int slotIndex)
    {
        if (!IsGodAdmin(ownerId))
            return (null, "Admin access required.");
        if (!_lobbies.TryGetValue(ownerId, out var lobby))
            return (null, "Admin lobby not found");

        AdminLobbySlot removed;
        lock (lobby)
        {
            var slotError = ValidateMutableSlot(lobby, slotIndex, requireEmpty: false);
            if (slotError != null)
                return (null, slotError);
            if (slotIndex == 0)
                return (null, "The owner cannot be removed");

            removed = lobby.Slots[slotIndex];
            lobby.Slots[slotIndex] = new AdminLobbySlot();
            if (removed.Kind == "human")
                _reservations.TryRemove(
                    new KeyValuePair<ulong, ulong>(removed.DiscordId, ownerId));
        }

        if (removed.Kind == "human")
            await NotifyReservationReleasedAsync(removed, cancellation: true);

        lock (lobby)
            return (BuildState(lobby), null);
    }

    public async Task<string> CancelAsync(ulong ownerId)
    {
        if (!IsGodAdmin(ownerId))
            return "Admin access required.";
        if (!_lobbies.TryGetValue(ownerId, out var lobby))
            return null;

        List<AdminLobbySlot> invitees;
        lock (lobby)
        {
            if (lobby.IsStarting)
                return "Admin lobby is starting";
            if (!_lobbies.TryRemove(
                    new KeyValuePair<ulong, AdminLobby>(ownerId, lobby)))
                return null;
            lobby.IsClosed = true;
            invitees = lobby.Slots
                .Where(slot => slot.Kind == "human" && slot.DiscordId != ownerId)
                .Select(slot => slot.Clone())
                .ToList();
            foreach (var invitee in invitees)
                _reservations.TryRemove(
                    new KeyValuePair<ulong, ulong>(invitee.DiscordId, ownerId));
        }

        foreach (var invitee in invitees)
            await NotifyReservationReleasedAsync(invitee, cancellation: true);
        return null;
    }

    public async Task<(ulong GameId, string Error)> StartGameAsync(ulong ownerId)
    {
        if (!IsGodAdmin(ownerId))
            return (0, "Admin access required.");
        if (!_lobbies.TryGetValue(ownerId, out var lobby))
            return (0, "Admin lobby not found");

        AdminLobbySlot[] slots;
        lock (lobby)
        {
            if (lobby.IsClosed)
                return (0, "Admin lobby not found");
            if (lobby.IsStarting)
                return (0, "Admin lobby is starting");

            foreach (var slot in lobby.Slots.Where(slot => slot.Kind == "human"))
            {
                var account = _userAccounts.GetOrAddUserAccount(slot.DiscordId);
                if (account == null)
                    return (0, "Account not found");
                if (account.IsPlaying)
                    return (0, "пользователь уже играет");
            }

            var strictBotCount = lobby.Slots.Count(slot => slot.Kind != "human");
            if (strictBotCount < 2 && lobby.Slots.Any(slot =>
                    slot.CharacterName == Naruto.CharacterName))
                return (0, "Наруто требует минимум два места для ботов");

            lobby.IsStarting = true;
            slots = lobby.Slots.Select(slot => slot.Clone()).ToArray();
        }

        GameClass game = null;
        try
        {
            var ownerAccount = _userAccounts.GetAccount(ownerId);
            var (gameId, createError) = await _webGameService.CreateGame(
                ownerId,
                ownerAccount?.DiscordUserName ?? "Admin",
                recordNaturalUnknownBugRoll: false,
                adminLobbyMode: true);
            if (createError != null)
                return ResetStarting(lobby, 0, createError);

            game = _global.GamesList.Find(candidate => candidate.GameId == gameId);
            if (game == null)
                return ResetStarting(lobby, 0, "Game creation failed");

            var ownerSeat = game.PlayersList.Find(player => player.DiscordId == ownerId);
            if (ownerSeat == null)
                throw new InvalidOperationException("Admin lobby owner seat was not created.");
            game.PlayersList = new[] { ownerSeat }
                .Concat(game.PlayersList.Where(player => !ReferenceEquals(player, ownerSeat)))
                .ToList();

            ApplyExplicitCharacters(game, slots);
            RepairRandomCharacterCollisions(game, slots);

            for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                var slot = slots[slotIndex];
                if (slot.Kind == "human")
                {
                    var seated = _webGameService.SeatAdminLobbyHuman(
                        game,
                        slotIndex,
                        slot.DiscordId,
                        slot.Username,
                        _notificationService.HasWebConnection(slot.DiscordId));
                    if (!seated.success)
                        throw new InvalidOperationException(seated.error);
                }
                else if (slot.Kind == "bot")
                {
                    game.PlayersList[slotIndex].AiDifficulty = slot.AiDifficulty;
                }
            }

            ValidateFinalRoster(game);
            await _webGameService.FinalizeAdminLobbyGame(game);
            game.IsCheckIfReady = true;

            _lobbies.TryRemove(new KeyValuePair<ulong, AdminLobby>(ownerId, lobby));
            lock (lobby)
                lobby.IsClosed = true;

            var humanIds = slots
                .Where(slot => slot.Kind == "human")
                .Select(slot => slot.DiscordId)
                .Distinct()
                .ToList();
            foreach (var humanId in humanIds)
            {
                var connections = _notificationService.GetConnections(humanId);
                foreach (var connectionId in connections)
                {
                    try
                    {
                        await _hubContext.Groups.AddToGroupAsync(
                            connectionId, $"game-{game.GameId}");
                        _notificationService.RegisterGameConnection(game.GameId, connectionId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[WebAPI] Admin lobby group handoff failed for {humanId}: {ex.Message}");
                    }
                }

                if (humanId != ownerId)
                {
                    _reservations.TryRemove(
                        new KeyValuePair<ulong, ulong>(humanId, ownerId));
                    try
                    {
                        await _hubContext.Clients.Clients(connections.ToList())
                            .SendAsync("AdminLobbyReserved", new { reserved = false });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[WebAPI] Admin lobby overlay clear failed for {humanId}: {ex.Message}");
                    }
                }

                if (connections.Count > 0)
                {
                    try
                    {
                        await _hubContext.Clients.Clients(connections.ToList())
                            .SendAsync("AdminLobbyGameStarted", new { gameId = game.GameId });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"[WebAPI] Admin lobby navigation push failed for {humanId}: {ex.Message}");
                    }
                }
            }

            return (game.GameId, null);
        }
        catch (Exception ex)
        {
            if (game != null)
                _webGameService.AbortAdminLobbyGame(game);
            Console.WriteLine($"[WebAPI] Admin lobby start failed: {ex}");
            return ResetStarting(lobby, 0, ex.Message);
        }
    }

    public async Task HandleDisconnectedAsync(ulong discordId)
    {
        if (_lobbies.TryGetValue(discordId, out var ownedLobby)
            && !_notificationService.HasWebConnection(discordId))
        {
            lock (ownedLobby)
            {
                if (!ownedLobby.IsClosed)
                    ownedLobby.OwnerDisconnectedUtc = DateTime.UtcNow;
            }
        }

        if (!_notificationService.HasWebConnection(discordId)
            && _reservations.TryRemove(discordId, out var ownerId)
            && _lobbies.TryGetValue(ownerId, out var lobby))
        {
            AdminLobbySlot removed = null;
            lock (lobby)
            {
                var slotIndex = Array.FindIndex(
                    lobby.Slots,
                    slot => slot.Kind == "human" && slot.DiscordId == discordId);
                if (slotIndex > 0)
                {
                    removed = lobby.Slots[slotIndex];
                    lobby.Slots[slotIndex] = new AdminLobbySlot();
                }
            }

            if (removed != null)
            {
                await NotifyReservationReleasedAsync(removed, cancellation: true);
                await PushStateToOwnerAsync(lobby);
            }
        }
    }

    public void SweepExpiredLobbies()
    {
        var cutoff = DateTime.UtcNow - OwnerDisconnectGrace;
        foreach (var (ownerId, lobby) in _lobbies)
        {
            List<AdminLobbySlot> invitees = null;
            lock (lobby)
            {
                if (lobby.IsClosed
                    || lobby.IsStarting
                    || lobby.OwnerDisconnectedUtc == null
                    || lobby.OwnerDisconnectedUtc > cutoff)
                    continue;
                if (!_lobbies.TryRemove(
                        new KeyValuePair<ulong, AdminLobby>(ownerId, lobby)))
                    continue;

                lobby.IsClosed = true;
                invitees = lobby.Slots
                    .Where(slot => slot.Kind == "human" && slot.DiscordId != ownerId)
                    .Select(slot => slot.Clone())
                    .ToList();
                foreach (var invitee in invitees)
                    _reservations.TryRemove(
                        new KeyValuePair<ulong, ulong>(invitee.DiscordId, ownerId));
            }

            foreach (var invitee in invitees)
                _ = NotifyReservationReleasedAsync(invitee, cancellation: true);
        }
    }

    private void ApplyExplicitCharacters(GameClass game, IReadOnlyList<AdminLobbySlot> slots)
    {
        var catalog = _charactersPull.GetAdminSelectableCharacters()
            .ToDictionary(character => character.Name, StringComparer.Ordinal);
        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var characterName = slots[slotIndex].CharacterName;
            if (string.IsNullOrEmpty(characterName))
                continue;
            game.PlayersList[slotIndex] = ReplaceCharacter(
                game.PlayersList[slotIndex], catalog[characterName]);
        }
    }

    private void RepairRandomCharacterCollisions(
        GameClass game,
        IReadOnlyList<AdminLobbySlot> slots)
    {
        var strictBotCount = slots.Count(slot => slot.Kind != "human");
        var taken = new List<string>();

        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            if (!string.IsNullOrEmpty(slots[slotIndex].CharacterName))
                taken.Add(game.PlayersList[slotIndex].GameCharacter.Name);
        }

        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            if (!string.IsNullOrEmpty(slots[slotIndex].CharacterName))
                continue;

            var currentName = game.PlayersList[slotIndex].GameCharacter.Name;
            var conflicts = taken.Any(name =>
                name == currentName
                || StartGameLogic.AreMutuallyExclusiveCharacters(name, currentName));
            if (strictBotCount < 2 && currentName == Naruto.CharacterName)
                conflicts = true;

            if (conflicts)
            {
                var candidates = _charactersPull.GetRollableCharacters()
                    .Where(character => strictBotCount >= 2
                                        || character.Name != Naruto.CharacterName)
                    .Where(character => taken.All(name =>
                        name != character.Name
                        && !StartGameLogic.AreMutuallyExclusiveCharacters(
                            name, character.Name)))
                    .ToList();
                if (candidates.Count == 0)
                    throw new InvalidOperationException("No valid random character remains.");

                var selected = candidates.OrderBy(_ => Guid.NewGuid()).First();
                game.PlayersList[slotIndex] =
                    ReplaceCharacter(game.PlayersList[slotIndex], selected);
                currentName = selected.Name;
            }

            taken.Add(currentName);
        }
    }

    private static GamePlayerBridgeClass ReplaceCharacter(
        GamePlayerBridgeClass source,
        CharacterClass character)
    {
        var replacement = new GamePlayerBridgeClass(
            character,
            new InGameStatus(),
            source.DiscordId,
            source.GameId,
            source.DiscordUsername,
            source.PlayerType)
        {
            IsWebPlayer = source.IsWebPlayer,
            PreferWeb = source.PreferWeb,
            TeamId = source.TeamId,
            Predict = source.Predict,
            DiscordStatus = source.DiscordStatus,
            AiDifficulty = source.AiDifficulty,
            AiPlaystyle = source.AiPlaystyle,
            AiKnowledge = source.AiKnowledge,
            ConsecutiveBotBlocks = source.ConsecutiveBotBlocks,
            CharacterMasteryPoints = source.CharacterMasteryPoints,
        };
        replacement.Status.IsDraftPickConfirmed = true;
        return replacement;
    }

    private static void ValidateFinalRoster(GameClass game)
    {
        for (var first = 0; first < game.PlayersList.Count; first++)
        for (var second = first + 1; second < game.PlayersList.Count; second++)
        {
            var firstName = game.PlayersList[first].GameCharacter.Name;
            var secondName = game.PlayersList[second].GameCharacter.Name;
            if (firstName == secondName
                || StartGameLogic.AreMutuallyExclusiveCharacters(firstName, secondName))
                throw new InvalidOperationException("The final character roster is invalid.");
        }

        var strictBotCount = game.PlayersList.Count(player => player.PlayerType == 404);
        if (game.PlayersList.Any(player =>
                player.GameCharacter.Name == Naruto.CharacterName)
            && strictBotCount < 2)
            throw new InvalidOperationException(
                "Наруто требует минимум два места для ботов");
    }

    private AdminLobbyStateDto BuildState(AdminLobby lobby)
    {
        return new AdminLobbyStateDto
        {
            OwnerId = lobby.OwnerId.ToString(),
            Slots = lobby.Slots.Select(slot => new AdminLobbySlotDto
            {
                Kind = slot.Kind,
                DiscordId = slot.DiscordId == 0 ? "" : slot.DiscordId.ToString(),
                Username = slot.Username,
                AiDifficulty = slot.AiDifficulty,
                CharacterName = slot.CharacterName ?? "",
                NotifiedByDm = slot.NotifiedByDm,
                IsUnreachable = slot.IsUnreachable,
            }).ToList(),
            Characters = _charactersPull.GetAdminSelectableCharacters()
                .Select(character => new AdminLobbyCharacterDto
                {
                    Name = character.Name,
                    Avatar = character.Avatar,
                    Tier = character.Tier,
                })
                .ToList(),
        };
    }

    private async Task<AdminLobbyDirectoryDto> BuildDirectorySnapshotAsync()
    {
        var accounts = _userAccounts.GetAllAccount()
            .Where(account => account.PlayerType != 404 && account.DiscordId > 1_000_000)
            .ToDictionary(account => account.DiscordId);
        var result = new AdminLobbyDirectoryDto();

        foreach (var guild in _global.Client.Guilds.OrderBy(guild => guild.Name))
        {
            try
            {
                await guild.DownloadUsersAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WebAPI] Admin directory guild download failed for {guild.Id}: {ex.Message}");
            }

            var group = new AdminLobbyGuildDto
            {
                GuildId = guild.Id.ToString(),
                GuildName = guild.Name,
            };
            foreach (var guildUser in guild.Users
                         .Where(user => !user.IsBot && accounts.ContainsKey(user.Id))
                         .OrderBy(user => accounts[user.Id].DiscordUserName))
            {
                var account = accounts[guildUser.Id];
                group.Members.Add(new AdminLobbyUserDto
                {
                    DiscordId = account.DiscordId.ToString(),
                    Username = account.DiscordUserName ?? guildUser.Username,
                    HasDiscord = true,
                    DiscordOnline = guildUser.Status != UserStatus.Offline,
                });
            }
            result.Guilds.Add(group);
        }

        result.Guilds.Add(new AdminLobbyGuildDto
        {
            GuildId = "web-only",
            GuildName = "Без Discord",
            Members = accounts.Values
                .Where(account => account.DiscordId >= WebOnlyIdStart)
                .OrderBy(account => account.DiscordUserName)
                .Select(account => new AdminLobbyUserDto
                {
                    DiscordId = account.DiscordId.ToString(),
                    Username = account.DiscordUserName ?? account.DiscordId.ToString(),
                    HasDiscord = false,
                    DiscordOnline = false,
                })
                .ToList(),
        });
        return result;
    }

    private AdminLobbyDirectoryDto RefreshDirectoryVolatileState(
        AdminLobbyDirectoryDto snapshot)
    {
        var online = _notificationService.GetOnlinePlayerIds().ToHashSet();
        var accounts = _userAccounts.GetAllAccount()
            .ToDictionary(account => account.DiscordId);
        return new AdminLobbyDirectoryDto
        {
            Guilds = snapshot.Guilds.Select(guild =>
            {
                var socketGuild = ulong.TryParse(guild.GuildId, out var guildId)
                    ? _global.Client.GetGuild(guildId)
                    : null;
                return new AdminLobbyGuildDto
                {
                    GuildId = guild.GuildId,
                    GuildName = guild.GuildName,
                    Members = guild.Members.Select(member =>
                    {
                        var id = ulong.Parse(member.DiscordId);
                        accounts.TryGetValue(id, out var account);
                        var discordOnline = member.HasDiscord
                            && (socketGuild?.GetUser(id)?.Status
                                ?? UserStatus.Offline) != UserStatus.Offline;
                        return new AdminLobbyUserDto
                        {
                            DiscordId = member.DiscordId,
                            Username = member.Username,
                            HasDiscord = member.HasDiscord,
                            DiscordOnline = discordOnline,
                            BrowserOnline = online.Contains(id),
                            IsBusy = account?.IsPlaying == true,
                            IsReserved = IsReserved(id),
                        };
                    }).ToList(),
                };
            }).ToList(),
        };
    }

    private static string ValidateMutableSlot(
        AdminLobby lobby,
        int slotIndex,
        bool requireEmpty)
    {
        if (lobby.IsClosed)
            return "Admin lobby not found";
        if (lobby.IsStarting)
            return "Admin lobby is starting";
        if (slotIndex < 0 || slotIndex >= lobby.Slots.Length)
            return "Invalid admin lobby slot";
        if (slotIndex == 0 && requireEmpty)
            return "The owner seat is fixed";
        if (requireEmpty && lobby.Slots[slotIndex].Kind != "empty")
            return "Slot is not empty";
        return null;
    }

    private async Task NotifyReservationReleasedAsync(
        AdminLobbySlot slot,
        bool cancellation)
    {
        var connections = _notificationService.GetConnections(slot.DiscordId);
        if (connections.Count > 0)
        {
            try
            {
                await _hubContext.Clients.Clients(connections.ToList())
                    .SendAsync("AdminLobbyReserved", new { reserved = false });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WebAPI] Admin lobby overlay clear failed for {slot.DiscordId}: {ex.Message}");
            }
        }

        if (!cancellation || !slot.NotifiedByDm)
            return;
        try
        {
            var user = _global.Client.GetUser(slot.DiscordId);
            if (user != null)
                await user.SendMessageAsync(CancellationDm);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[WebAPI] Admin lobby cancellation DM failed for {slot.DiscordId}: {ex.Message}");
        }
    }

    private async Task PushStateToOwnerAsync(AdminLobby lobby)
    {
        var connections = _notificationService.GetConnections(lobby.OwnerId);
        if (connections.Count == 0)
            return;
        AdminLobbyStateDto state;
        lock (lobby)
            state = BuildState(lobby);
        try
        {
            await _hubContext.Clients.Clients(connections.ToList())
                .SendAsync("AdminLobbyState", state);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[WebAPI] Admin lobby owner refresh failed for {lobby.OwnerId}: {ex.Message}");
        }
    }

    private static (ulong GameId, string Error) ResetStarting(
        AdminLobby lobby,
        ulong gameId,
        string error)
    {
        lock (lobby)
            lobby.IsStarting = false;
        return (gameId, error);
    }
}

internal sealed class AdminLobby
{
    public ulong OwnerId { get; init; }
    public AdminLobbySlot[] Slots { get; init; }
    public DateTime? OwnerDisconnectedUtc { get; set; }
    public bool IsStarting { get; set; }
    public bool IsClosed { get; set; }
}

internal sealed class AdminLobbySlot
{
    public string Kind { get; set; } = "empty";
    public ulong DiscordId { get; set; }
    public string Username { get; set; } = "";
    public int AiDifficulty { get; set; }
    public string CharacterName { get; set; } = "";
    public bool NotifiedByDm { get; set; }
    public bool IsUnreachable { get; set; }

    public AdminLobbySlot Clone() => new()
    {
        Kind = Kind,
        DiscordId = DiscordId,
        Username = Username,
        AiDifficulty = AiDifficulty,
        CharacterName = CharacterName,
        NotifiedByDm = NotifiedByDm,
        IsUnreachable = IsUnreachable,
    };
}

public sealed class AdminLobbyStateDto
{
    public string OwnerId { get; set; } = "";
    public List<AdminLobbySlotDto> Slots { get; set; } = new();
    public List<AdminLobbyCharacterDto> Characters { get; set; } = new();
}

public sealed class AdminLobbySlotDto
{
    public string Kind { get; set; } = "empty";
    public string DiscordId { get; set; } = "";
    public string Username { get; set; } = "";
    public int AiDifficulty { get; set; }
    public string CharacterName { get; set; } = "";
    public bool NotifiedByDm { get; set; }
    public bool IsUnreachable { get; set; }
}

public sealed class AdminLobbyCharacterDto
{
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public int Tier { get; set; }
}

public sealed class AdminLobbyDirectoryDto
{
    public List<AdminLobbyGuildDto> Guilds { get; set; } = new();
}

public sealed class AdminLobbyGuildDto
{
    public string GuildId { get; set; } = "";
    public string GuildName { get; set; } = "";
    public List<AdminLobbyUserDto> Members { get; set; } = new();
}

public sealed class AdminLobbyUserDto
{
    public string DiscordId { get; set; } = "";
    public string Username { get; set; } = "";
    public bool HasDiscord { get; set; }
    public bool DiscordOnline { get; set; }
    public bool BrowserOnline { get; set; }
    public bool IsBusy { get; set; }
    public bool IsReserved { get; set; }
}

public sealed class AdminLobbyPresenceDto
{
    public List<string> OnlineIds { get; set; } = new();
    public List<string> BusyIds { get; set; } = new();
    public List<string> ReservedIds { get; set; } = new();
}
