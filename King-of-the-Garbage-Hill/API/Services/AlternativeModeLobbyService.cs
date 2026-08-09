using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using Microsoft.AspNetCore.SignalR;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Server-owned, ten-minute preparation lobbies for the web alternative modes.
/// A preparation lobby is deliberately not a <see cref="GameClass"/>: private
/// character/passive candidates and movable team seats must never leak through
/// the ordinary spectator/state mapper before the final roster is committed.
/// </summary>
public sealed class AlternativeModeLobbyService : IDisposable
{
    public const string TeamMode = "Team";
    public const string AramMode = "Aram";
    public const string TeamAramMode = "TeamAram";
    public const string TeamStage = "Team";
    public const string CharacterStage = "Character";
    public const string AramStage = "Aram";

    private const int SeatCount = 6;
    private const int LobbySeconds = 600;
    private const int SwitchCharacterCost = 5;
    private const string AramAvatar =
        "https://media.discordapp.net/attachments/895072182051430401/1057078633317023855/mylorik_avatar_for_an_rpg_game_where_players_are_forced_to_pick_386de9dc-62ca-491c-ae63-54324a8c95d9.png";

    private readonly Global _global;
    private readonly UserAccounts _userAccounts;
    private readonly HelperFunctions _helper;
    private readonly CharactersPull _charactersPull;
    private readonly StartGameLogic _startGameLogic;
    private readonly GameReaction _gameReaction;
    private readonly CharacterPassives _characterPassives;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly GameNotificationService _notificationService;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly SecureRandom _secureRandom;
    private readonly ConcurrentDictionary<ulong, AlternativeLobby> _lobbies = new();
    private readonly System.Timers.Timer _expiryTimer;
    private int _sweepRunning;

    public AlternativeModeLobbyService(
        Global global,
        UserAccounts userAccounts,
        HelperFunctions helper,
        CharactersPull charactersPull,
        StartGameLogic startGameLogic,
        GameReaction gameReaction,
        CharacterPassives characterPassives,
        GameUpdateMess gameUpdateMess,
        GameNotificationService notificationService,
        IHubContext<GameHub> hubContext,
        SecureRandom secureRandom)
    {
        _global = global;
        _userAccounts = userAccounts;
        _helper = helper;
        _charactersPull = charactersPull;
        _startGameLogic = startGameLogic;
        _gameReaction = gameReaction;
        _characterPassives = characterPassives;
        _gameUpdateMess = gameUpdateMess;
        _notificationService = notificationService;
        _hubContext = hubContext;
        _secureRandom = secureRandom;

        _expiryTimer = new System.Timers.Timer(1000) { AutoReset = true, Enabled = true };
        _expiryTimer.Elapsed += (_, _) => _ = SweepExpiredAsync();
    }

    public static bool IsSupportedMode(string mode) =>
        mode is TeamMode or AramMode or TeamAramMode;

    public IReadOnlyList<ActiveGameDto> GetDirectoryEntries(ulong viewerId = 0)
    {
        var result = new List<ActiveGameDto>();
        foreach (var lobby in _lobbies.Values)
        {
            lock (lobby)
            {
                if (lobby.IsClosed) continue;
                var humanCount = lobby.Seats.Count(seat => seat.Kind == "human");
                var botCount = lobby.Seats.Count(seat => seat.Kind == "bot");
                result.Add(new ActiveGameDto
                {
                    GameId = lobby.LobbyId,
                    RoundNo = 0,
                    PlayerCount = SeatCount,
                    HumanCount = humanCount,
                    BotCount = botCount,
                    GameMode = lobby.Mode,
                    IsFinished = false,
                    CanJoin = CanJoinLocked(lobby)
                              || viewerId != 0 && lobby.Seats.Any(seat =>
                                  seat.Kind == "human" && seat.DiscordId == viewerId),
                    IsPreparation = true,
                    PreparationStage = lobby.Stage,
                    DeadlineUtc = lobby.DeadlineUtc.ToString("O"),
                });
            }
        }

        return result.OrderBy(entry => entry.GameId).ToList();
    }

    public async Task<(ulong LobbyId, string Error)> CreateAsync(
        ulong creatorId,
        string creatorUsername,
        string mode)
    {
        if (!IsSupportedMode(mode)) return (0, "Unknown alternative mode");
        var account = _userAccounts.GetAccount(creatorId);
        if (account == null) return (0, "Account not found");

        lock (account)
        {
            if (account.IsPlaying) return (0, "Already in a game");
            account.IsPlaying = true;
        }

        AlternativeLobby lobby = null;
        try
        {
            var lobbyId = _global.GetNewtGamePlayingAndId();
            lobby = NewLobby(lobbyId, creatorId, mode, adminGame: false);
            lobby.Seats[0] = NewHumanSeat(
                creatorId,
                creatorUsername,
                hasWebConnection: true,
                aiDifficulty: 0);
            if (mode == AramMode)
                lobby.Seats[0].AramBuild = CreateAramBuild();

            if (!_lobbies.TryAdd(lobbyId, lobby))
                throw new InvalidOperationException("Preparation lobby ID collision.");

            await PushStateAsync(lobby);
            return (lobbyId, null);
        }
        catch
        {
            if (lobby != null)
                _lobbies.TryRemove(lobby.LobbyId, out _);
            lock (account)
                account.IsPlaying = false;
            throw;
        }
    }

    /// <summary>Transfers the six curated admin seats into an alternative preparation lobby.</summary>
    public async Task<(ulong LobbyId, string Error)> CreateAdminAsync(
        ulong creatorId,
        string mode,
        int teamSize,
        IReadOnlyList<AlternativeLobbySeedSeat> seeds)
    {
        if (!IsSupportedMode(mode)) return (0, "Unknown alternative mode");
        if (seeds == null || seeds.Count != SeatCount) return (0, "Six admin seats are required");
        if (teamSize is not (2 or 3)) return (0, "Team size must be 2 or 3");

        var humanAccounts = new List<DiscordAccountClass>();
        foreach (var seed in seeds.Where(seed => seed.Kind == "human"))
        {
            var account = _userAccounts.GetAccount(seed.DiscordId);
            if (account == null) return (0, "Account not found");
            lock (account)
            {
                if (account.IsPlaying)
                {
                    foreach (var claimedAccount in humanAccounts)
                        lock (claimedAccount)
                            claimedAccount.IsPlaying = false;
                    return (0, "пользователь уже играет");
                }
                account.IsPlaying = true;
            }
            humanAccounts.Add(account);
        }

        AlternativeLobby lobby = null;
        try
        {
            var lobbyId = _global.GetNewtGamePlayingAndId();
            lobby = NewLobby(lobbyId, creatorId, mode, adminGame: true);
            lobby.TeamSize = teamSize;
            lobby.AiDifficulty = 4;

            var botMarkers = new List<GamePlayerBridgeClass>();
            for (var index = 0; index < SeatCount; index++)
            {
                var seed = seeds[index];
                if (seed.Kind == "human")
                {
                    lobby.Seats[index] = NewHumanSeat(
                        seed.DiscordId,
                        seed.Username,
                        seed.HasWebConnection,
                        0);
                    if (mode == AramMode)
                        lobby.Seats[index].AramBuild = CreateAramBuild();
                }
                else if (seed.Kind == "bot")
                {
                    lobby.Seats[index] = ClaimBotSeat(
                        lobby,
                        botMarkers,
                        seed.AiDifficulty is >= 1 and <= 4 ? seed.AiDifficulty : 4);
                    if (mode == AramMode)
                    {
                        lobby.Seats[index].AramBuild = CreateAramBuild();
                        AutoCompleteAram(lobby.Seats[index]);
                    }
                }
            }

            if (!_lobbies.TryAdd(lobbyId, lobby))
                throw new InvalidOperationException("Preparation lobby ID collision.");
            await PushStateAsync(lobby);
            await NotifyPreparationOpenedAsync(lobby);
            return (lobbyId, null);
        }
        catch
        {
            if (lobby != null)
            {
                _lobbies.TryRemove(lobby.LobbyId, out _);
                ReleaseAccounts(lobby);
            }
            else
            {
                foreach (var account in humanAccounts)
                    lock (account)
                        account.IsPlaying = false;
            }
            throw;
        }
    }

    public async Task<string> JoinAsync(ulong lobbyId, ulong playerId, string username)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        var account = _userAccounts.GetAccount(playerId);
        if (account == null) return "Account not found";

        lock (lobby)
        {
            var existingSeat = lobby.Seats.FirstOrDefault(seat =>
                seat.Kind == "human" && seat.DiscordId == playerId);
            if (existingSeat != null)
            {
                existingSeat.HasWebConnection = true;
            }
            else
            {
                if (!CanJoinLocked(lobby)) return "Preparation lobby cannot be joined";
                lock (account)
                {
                    if (account.IsPlaying) return "Already in a game";
                    account.IsPlaying = true;
                }

                var slotIndex = Array.FindIndex(lobby.Seats, seat => seat.Kind == "empty");
                if (slotIndex < 0)
                {
                    lock (account)
                        account.IsPlaying = false;
                    return "Preparation lobby is full";
                }

                lobby.Seats[slotIndex] = NewHumanSeat(playerId, username, true, 0);
                if (lobby.Mode == AramMode)
                    lobby.Seats[slotIndex].AramBuild = CreateAramBuild();
                ResetTeamReadinessLocked(lobby);
            }
        }

        await PushStateAsync(lobby);
        return null;
    }

    public AlternativeLobbyStateDto GetState(ulong lobbyId, ulong viewerId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return null;
        lock (lobby)
            return BuildStateLocked(lobby, viewerId);
    }

    public async Task<string> SetTeamSizeAsync(ulong lobbyId, ulong playerId, int teamSize)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.CreatorId != playerId) return "Only the lobby creator can change team format";
            if (lobby.Stage != TeamStage) return "Team format is already locked";
            if (teamSize is not (2 or 3)) return "Team size must be 2 or 3";
            lobby.TeamSize = teamSize;
            ResetTeamReadinessLocked(lobby);
        }
        await PushStateAsync(lobby);
        return null;
    }

    public async Task<string> SetAiDifficultyAsync(ulong lobbyId, ulong playerId, int aiDifficulty)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.CreatorId != playerId) return "Only the lobby creator can change bot difficulty";
            if (aiDifficulty is < 1 or > 4) return "Bot difficulty must be between 1 and 4";
            if (lobby.Stage != TeamStage && lobby.Mode != AramMode)
                return "Bot difficulty is already locked";
            lobby.AiDifficulty = aiDifficulty;
        }
        await PushStateAsync(lobby);
        return null;
    }

    public async Task<string> MoveAsync(ulong lobbyId, ulong playerId, int targetSlotIndex)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.Stage != TeamStage) return "Team seats are already locked";
            if (targetSlotIndex is < 0 or >= SeatCount) return "Invalid team slot";
            if (lobby.Seats[targetSlotIndex].Kind != "empty") return "Team slot is occupied";
            var sourceIndex = Array.FindIndex(lobby.Seats,
                seat => seat.Kind == "human" && seat.DiscordId == playerId);
            if (sourceIndex < 0) return "Player not in this preparation lobby";
            lobby.Seats[targetSlotIndex] = lobby.Seats[sourceIndex];
            lobby.Seats[sourceIndex] = new AlternativeSeat();
            ResetTeamReadinessLocked(lobby);
        }
        await PushStateAsync(lobby);
        return null;
    }

    public async Task<string> SetTeamReadyAsync(ulong lobbyId, ulong playerId, bool ready)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.Stage != TeamStage) return "Team stage is already complete";
            var seat = FindHumanSeatLocked(lobby, playerId);
            if (seat == null) return "Player not in this preparation lobby";
            seat.Ready = ready;
        }
        await AdvanceAsync(lobby, force: false);
        return null;
    }

    public async Task<string> SelectCharacterAsync(
        ulong lobbyId,
        ulong playerId,
        string characterName)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.Stage != CharacterStage) return "Not in character selection stage";
            var seat = FindHumanSeatLocked(lobby, playerId);
            if (seat == null) return "Player not in this preparation lobby";
            if (seat.Ready) return "Character is already locked";
            var selected = seat.CharacterOptions.Find(character =>
                string.Equals(character.Name, characterName, StringComparison.Ordinal));
            if (selected == null) return "Character is not one of your options";
            if (selected.Name == Naruto.CharacterName)
                return "Наруто недоступен в командной игре";

            var selectedIndex = seat.CharacterOptions.IndexOf(selected);
            var account = _userAccounts.GetAccount(playerId);
            if (account == null) return "Account not found";
            lock (account)
            {
                if (selectedIndex > 0 && account.ZbsPoints < SwitchCharacterCost)
                    return $"Not enough ZBS points (need {SwitchCharacterCost})";

                var previousCharacter = account.CharacterPlayedLastTime;
                account.CharacterPlayedLastTime = selected.Name;
                if (selectedIndex > 0)
                {
                    var previousBalance = account.ZbsPoints;
                    account.ZbsPoints -= SwitchCharacterCost;
                    if (!_userAccounts.SaveAccount(account))
                    {
                        account.ZbsPoints = previousBalance;
                        account.CharacterPlayedLastTime = previousCharacter;
                        return "Could not save your switch purchase. No ZBS was spent; please try again.";
                    }
                }
            }
            seat.SelectedCharacter = selected;
            seat.Ready = true;
        }
        await AdvanceAsync(lobby, force: false);
        return null;
    }

    public async Task<string> UnlockPassiveAsync(ulong lobbyId, ulong playerId, int slotIndex)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.Stage != AramStage) return "Not in random character creation stage";
            var seat = FindHumanSeatLocked(lobby, playerId);
            if (seat?.AramBuild == null) return "Player not in this preparation lobby";
            if (seat.Ready) return "Random character is already locked";
            if (slotIndex is < 0 or >= 4) return "Invalid passive slot";
            var passiveSlot = seat.AramBuild.PassiveSlots[slotIndex];
            if (passiveSlot.Candidates.Count >= 4) return "All passive cells are already open";

            var usedNames = seat.AramBuild.PassiveSlots
                .SelectMany(slot => slot.Candidates)
                .Select(passive => passive.PassiveName)
                .ToHashSet(StringComparer.Ordinal);
            var available = _charactersPull.GetAramPassives()
                .Where(passive => !usedNames.Contains(passive.PassiveName))
                .ToList();
            if (available.Count == 0) return "No passive candidate is available";
            var selected = available[_secureRandom.Random(0, available.Count - 1)].DeepCopy();
            passiveSlot.Candidates.Add(selected);
            seat.AramBuild.Intelligence--;
            seat.AramBuild.Strength--;
            seat.AramBuild.Speed--;
            seat.AramBuild.Psyche--;
        }
        await PushStateAsync(lobby);
        return null;
    }

    public async Task<string> SelectPassiveAsync(
        ulong lobbyId,
        ulong playerId,
        int slotIndex,
        string passiveName)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.Stage != AramStage) return "Not in random character creation stage";
            var seat = FindHumanSeatLocked(lobby, playerId);
            if (seat?.AramBuild == null) return "Player not in this preparation lobby";
            if (seat.Ready) return "Random character is already locked";
            if (slotIndex is < 0 or >= 4) return "Invalid passive slot";
            var passiveSlot = seat.AramBuild.PassiveSlots[slotIndex];
            var selectedIndex = passiveSlot.Candidates.FindIndex(passive =>
                string.Equals(passive.PassiveName, passiveName, StringComparison.Ordinal));
            if (selectedIndex < 0) return "Passive is not one of your candidates";
            passiveSlot.SelectedIndex = selectedIndex;
        }
        await PushStateAsync(lobby);
        return null;
    }

    public async Task<string> SetAramReadyAsync(ulong lobbyId, ulong playerId, bool ready)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return "Preparation lobby not found";
        lock (lobby)
        {
            if (lobby.Stage != AramStage) return "Not in random character creation stage";
            var seat = FindHumanSeatLocked(lobby, playerId);
            if (seat?.AramBuild == null) return "Player not in this preparation lobby";
            if (ready && seat.AramBuild.PassiveSlots.Any(slot => slot.SelectedIndex < 0))
                return "Choose one passive in every slot";
            seat.Ready = ready;
        }
        await AdvanceAsync(lobby, force: false);
        return null;
    }

    public async Task<string> LeaveAsync(ulong lobbyId, ulong playerId)
    {
        if (!_lobbies.TryGetValue(lobbyId, out var lobby)) return null;
        var cancel = false;
        DiscordAccountClass releasedAccount = null;
        lock (lobby)
        {
            var index = Array.FindIndex(lobby.Seats,
                seat => seat.Kind == "human" && seat.DiscordId == playerId);
            if (index < 0) return null;
            cancel = playerId == lobby.CreatorId
                     || lobby.Seats.Count(seat => seat.Kind == "human") == 1;
            if (!cancel)
            {
                releasedAccount = _userAccounts.GetAccount(playerId);
                if (lobby.Stage == TeamStage || lobby.Mode == AramMode)
                    lobby.Seats[index] = new AlternativeSeat();
                else
                {
                    var replacement = ClaimBotSeat(
                        lobby,
                        BuildBotMarkersLocked(lobby),
                        lobby.AiDifficulty);
                    if (lobby.Stage == CharacterStage)
                    {
                        var excluded = lobby.Seats
                            .Where((_, seatIndex) => seatIndex != index)
                            .SelectMany(seat => seat.CharacterOptions)
                            .DistinctBy(character => character.Name)
                            .ToList();
                        var botAccount = _userAccounts.GetAccount(replacement.DiscordId)
                                         ?? throw new InvalidOperationException(
                                             "Replacement bot account not found.");
                        replacement.CharacterOptions = RollTeamOptions(
                            botAccount,
                            excluded,
                            lobby.Seats.Count(seat => seat.Kind == "bot") + 1,
                            count: 1,
                            forBot: true);
                        replacement.SelectedCharacter = replacement.CharacterOptions[0];
                        replacement.Ready = true;
                    }
                    else if (lobby.Stage == AramStage)
                    {
                        replacement.AramBuild = CreateAramBuild();
                        AutoCompleteAram(replacement);
                    }
                    lobby.Seats[index] = replacement;
                }
                ResetTeamReadinessLocked(lobby);
            }
        }

        if (cancel)
        {
            await CancelAsync(lobby, "Preparation lobby was cancelled");
            return null;
        }

        if (releasedAccount != null)
            lock (releasedAccount)
                releasedAccount.IsPlaying = false;
        await AdvanceAsync(lobby, force: false);
        return null;
    }

    private async Task SweepExpiredAsync()
    {
        if (Interlocked.Exchange(ref _sweepRunning, 1) != 0) return;
        try
        {
            var now = DateTime.UtcNow;
            foreach (var lobby in _lobbies.Values)
            {
                var expired = false;
                lock (lobby)
                    expired = !lobby.IsClosed && !lobby.IsStarting && now >= lobby.DeadlineUtc;
                if (expired)
                    await AdvanceAsync(lobby, force: true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AlternativeLobby] Expiry sweep failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _sweepRunning, 0);
        }
    }

    private async Task AdvanceAsync(AlternativeLobby lobby, bool force)
    {
        var shouldStart = false;
        lock (lobby)
        {
            if (lobby.IsClosed || lobby.IsStarting) return;
            if (lobby.Stage == TeamStage
                && (force || HumansLocked(lobby).All(seat => seat.Ready)))
            {
                EnterSelectionStageLocked(lobby);
            }

            if (force && lobby.Stage == CharacterStage)
            {
                foreach (var seat in lobby.Seats.Where(seat => seat.Kind == "human" && !seat.Ready))
                {
                    seat.SelectedCharacter = seat.CharacterOptions.FirstOrDefault();
                    seat.Ready = seat.SelectedCharacter != null;
                }
            }
            else if (force && lobby.Stage == AramStage)
            {
                foreach (var seat in lobby.Seats.Where(seat => seat.Kind == "human" && !seat.Ready))
                    AutoCompleteAram(seat);
            }

            if (lobby.Stage == AramStage
                && HumansLocked(lobby).All(seat => seat.Ready))
            {
                FillEmptySeatsWithBotsLocked(lobby);
                foreach (var bot in lobby.Seats.Where(seat => seat.Kind == "bot"))
                {
                    bot.AramBuild ??= CreateAramBuild();
                    AutoCompleteAram(bot);
                }
                shouldStart = true;
            }
            else if (lobby.Stage == CharacterStage
                     && lobby.Seats.Where(seat => seat.Kind != "empty").All(seat => seat.Ready))
            {
                shouldStart = true;
            }

            if (shouldStart)
                lobby.IsStarting = true;
        }

        if (shouldStart)
            await FinalizeAsync(lobby);
        else
            await PushStateAsync(lobby);
    }

    private void EnterSelectionStageLocked(AlternativeLobby lobby)
    {
        FillEmptySeatsWithBotsLocked(lobby);
        foreach (var seat in lobby.Seats)
            seat.Ready = false;

        if (lobby.Mode == TeamAramMode)
        {
            lobby.Stage = AramStage;
            foreach (var seat in lobby.Seats)
            {
                seat.AramBuild = CreateAramBuild();
                if (seat.Kind == "bot") AutoCompleteAram(seat);
            }
            return;
        }

        lobby.Stage = CharacterStage;
        var excluded = new List<CharacterClass>();
        var strictBotCount = lobby.Seats.Count(seat => seat.Kind == "bot");
        foreach (var seat in lobby.Seats)
        {
            var account = _userAccounts.GetAccount(seat.DiscordId)
                          ?? throw new InvalidOperationException("Preparation account not found.");
            var optionCount = seat.Kind == "human" ? 3 : 1;
            seat.CharacterOptions = RollTeamOptions(
                account,
                excluded,
                strictBotCount,
                optionCount,
                seat.Kind == "bot");
            excluded.AddRange(seat.CharacterOptions);
            if (seat.Kind == "bot")
            {
                seat.SelectedCharacter = seat.CharacterOptions[0];
                seat.Ready = true;
            }
        }
    }

    private List<CharacterClass> RollTeamOptions(
        DiscordAccountClass account,
        List<CharacterClass> excluded,
        int strictBotCount,
        int count,
        bool forBot)
    {
        var rolled = _startGameLogic.RollDraftOptions(
                account,
                excluded,
                strictBotCount,
                count: Math.Max(count * 3, count),
                isTeamMode: true)
            .Where(character => character.Name != Naruto.CharacterName)
            .Where(character => character.Name != "HardKitty")
            .Where(character => !forBot || StartGameLogic.CanNaturallyAssignToBot(character))
            .Where(character => excluded.All(existing =>
                existing.Name != character.Name
                && !StartGameLogic.AreMutuallyExclusiveCharacters(existing.Name, character.Name)))
            .Where(character => character.Tier != 4 || excluded.All(existing => existing.Tier != 4))
            .Take(count)
            .ToList();

        if (rolled.Count < count)
        {
            var candidates = _charactersPull.GetRollableCharacters()
                .Where(character => character.Name != Naruto.CharacterName)
                .Where(character => character.Name != "HardKitty")
                .Where(character => !forBot || StartGameLogic.CanNaturallyAssignToBot(character))
                .Where(character => excluded.Concat(rolled).All(existing =>
                    existing.Name != character.Name
                    && !StartGameLogic.AreMutuallyExclusiveCharacters(existing.Name, character.Name)))
                .Where(character => character.Tier != 4
                                    || excluded.Concat(rolled).All(existing => existing.Tier != 4))
                .ToList();
            while (rolled.Count < count && candidates.Count > 0)
            {
                var index = _secureRandom.Random(0, candidates.Count - 1);
                var selected = candidates[index];
                rolled.Add(selected);
                candidates.RemoveAll(character =>
                    character.Name == selected.Name
                    || StartGameLogic.AreMutuallyExclusiveCharacters(character.Name, selected.Name)
                    || selected.Tier == 4 && character.Tier == 4);
            }
        }

        if (rolled.Count != count)
            throw new InvalidOperationException("Could not roll enough unique team character options.");
        return rolled;
    }

    private void FillEmptySeatsWithBotsLocked(AlternativeLobby lobby)
    {
        var markers = BuildBotMarkersLocked(lobby);
        for (var index = 0; index < SeatCount; index++)
        {
            if (lobby.Seats[index].Kind != "empty") continue;
            lobby.Seats[index] = ClaimBotSeat(lobby, markers, lobby.AiDifficulty);
        }
    }

    private AlternativeSeat ClaimBotSeat(
        AlternativeLobby lobby,
        List<GamePlayerBridgeClass> markers,
        int aiDifficulty)
    {
        var account = _helper.GetFreeBot(markers);
        var seat = new AlternativeSeat
        {
            Kind = "bot",
            DiscordId = account.DiscordId,
            Username = account.DiscordUserName,
            AiDifficulty = aiDifficulty,
            HasWebConnection = false,
        };
        markers.Add(new GamePlayerBridgeClass(
            CreatePlaceholderCharacter(),
            new InGameStatus(),
            account.DiscordId,
            lobby.LobbyId,
            account.DiscordUserName,
            404,
            account.GameplayMode));
        return seat;
    }

    private static CharacterClass CreatePlaceholderCharacter() => new(
        8, 8, 8, 8, "ARAM", "ARAM", 0, AramAvatar)
    {
        Passive = new List<Passive>(),
    };

    private List<GamePlayerBridgeClass> BuildBotMarkersLocked(AlternativeLobby lobby) =>
        lobby.Seats
            .Where(seat => seat.Kind == "bot")
            .Select(seat => new GamePlayerBridgeClass(
                CreatePlaceholderCharacter(),
                new InGameStatus(),
                seat.DiscordId,
                lobby.LobbyId,
                seat.Username,
                404))
            .ToList();

    private AramBuild CreateAramBuild()
    {
        var passives = SecureRandom.Shuffle(_charactersPull.GetAramPassives());
        var build = new AramBuild
        {
            Intelligence = Math.Max(8, _gameReaction.GetRandomStat()),
            Strength = Math.Max(8, _gameReaction.GetRandomStat()),
            Speed = Math.Max(8, _gameReaction.GetRandomStat()),
            Psyche = Math.Max(8, _gameReaction.GetRandomStat()),
        };
        for (var slotIndex = 0; slotIndex < 4; slotIndex++)
        {
            var passiveSlot = new AramPassiveSlot();
            for (var optionIndex = 0; optionIndex < 2; optionIndex++)
            {
                var passive = passives[slotIndex * 2 + optionIndex].DeepCopy();
                passiveSlot.Candidates.Add(passive);
            }
            build.PassiveSlots.Add(passiveSlot);
        }
        return build;
    }

    private static void AutoCompleteAram(AlternativeSeat seat)
    {
        if (seat.AramBuild == null) return;
        foreach (var passiveSlot in seat.AramBuild.PassiveSlots)
            if (passiveSlot.SelectedIndex < 0)
                passiveSlot.SelectedIndex = 0;
        seat.Ready = true;
    }

    private async Task FinalizeAsync(AlternativeLobby lobby)
    {
        try
        {
            AlternativeSeat[] seats;
            int teamSize;
            int aiDifficulty;
            string mode;
            lock (lobby)
            {
                seats = lobby.Seats.Select(seat => seat.Clone()).ToArray();
                teamSize = lobby.TeamSize;
                aiDifficulty = lobby.AiDifficulty;
                mode = lobby.Mode;
            }

            var players = new List<GamePlayerBridgeClass>(SeatCount);
            for (var index = 0; index < SeatCount; index++)
            {
                var seat = seats[index];
                var account = _userAccounts.GetAccount(seat.DiscordId)
                              ?? throw new InvalidOperationException("Preparation account not found.");
                CharacterClass character;
                if (mode is AramMode or TeamAramMode)
                {
                    var build = seat.AramBuild
                                ?? throw new InvalidOperationException("ARAM build is missing.");
                    var selectedPassives = build.PassiveSlots.Select(passiveSlot =>
                    {
                        if (passiveSlot.SelectedIndex < 0
                            || passiveSlot.SelectedIndex >= passiveSlot.Candidates.Count)
                            throw new InvalidOperationException("ARAM passive selection is incomplete.");
                        return passiveSlot.Candidates[passiveSlot.SelectedIndex].DeepCopy();
                    }).ToList();
                    character = new CharacterClass(
                        build.Intelligence,
                        build.Strength,
                        build.Speed,
                        build.Psyche,
                        "ARAM",
                        "ARAM",
                        0,
                        AramAvatar)
                    {
                        Passive = selectedPassives,
                    };
                }
                else
                {
                    character = seat.SelectedCharacter?.DeepCopy()
                                ?? throw new InvalidOperationException("Team character selection is incomplete.");
                }

                var bridge = new GamePlayerBridgeClass(
                    character,
                    new InGameStatus(),
                    account.DiscordId,
                    lobby.LobbyId,
                    seat.Username,
                    seat.Kind == "bot" ? 404 : account.PlayerType,
                    account.GameplayMode)
                {
                    IsWebPlayer = seat.Kind == "human" && seat.HasWebConnection,
                    PreferWeb = seat.Kind == "human" && seat.HasWebConnection,
                    AiDifficulty = seat.Kind == "bot" && seat.AiDifficulty is >= 1 and <= 4
                        ? seat.AiDifficulty
                        : -1,
                    TeamId = mode is TeamMode or TeamAramMode
                        ? TeamForSlot(index, teamSize)
                        : 0,
                    CharacterMasteryPoints = account.CharacterMastery
                        .GetValueOrDefault(character.Name, 0),
                };
                DoomGuy.InitializeForGame(bridge, account);
                if (seat.Kind == "human")
                {
                    lock (account)
                    {
                        account.CharacterPlayedLastTime = character.Name;
                        if (!UnknownBug.Is(character.Name)
                            && !account.SeenCharacters.Contains(character.Name))
                            account.SeenCharacters.Add(character.Name);
                    }
                }
                players.Add(bridge);
            }

            var game = new GameClass(
                players,
                lobby.LobbyId,
                lobby.CreatorId,
                gameMode: mode)
            {
                IsCheckIfReady = false,
                AiDifficulty = aiDifficulty,
            };
            if (mode is TeamMode or TeamAramMode)
                BuildTeams(game);

            players = _characterPassives.HandleEventsBeforeFirstRound(
                players, ordinaryPredictionsEnabled: mode is not (AramMode or TeamAramMode));
            game.PlayersList = players;
            game.NanobotsList.Clear();
            game.NanobotsList.Add(new BotsBehavior.NanobotClass(players));
            game.ExploitPlayersList = players
                .Where(player => !UnknownBug.Is(player) && !player.Passives.IsDead)
                .ToList();
            for (var index = 0; index < players.Count; index++)
                players[index].Status.SetPlaceAtLeaderBoard(index + 1);
            game.RollExploit();
            SeedTeamPredictions(game);

            foreach (var player in players.Where(player =>
                         player.PlayerType != 404 && !player.PreferWeb))
            {
                try
                {
                    await _gameUpdateMess.WaitMess(player, game);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[AlternativeLobby] Discord setup failed for {player.DiscordId}: {ex.Message}");
                }
            }

            await _characterPassives.HandleNextRound(game);
            _characterPassives.HandleBotPredict(game);
            game.TimePassed.Restart();
            game.IsCheckIfReady = true;
            lock (_global.GamesList)
                _global.GamesList.Add(game);

            _lobbies.TryRemove(new KeyValuePair<ulong, AlternativeLobby>(lobby.LobbyId, lobby));
            lock (lobby)
                lobby.IsClosed = true;
            await HandoffToGameAsync(game);
            Console.WriteLine(
                $"[AlternativeLobby] Preparation #{lobby.LobbyId} started as {mode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AlternativeLobby] Finalization failed: {ex}");
            await CancelAsync(lobby, "Could not start the alternative game");
        }
    }

    private static void BuildTeams(GameClass game)
    {
        game.Teams.Clear();
        foreach (var group in game.PlayersList.GroupBy(player => player.TeamId).OrderBy(group => group.Key))
        {
            var team = new GameClass.TeamPlay(group.Key);
            foreach (var player in group)
            {
                team.TeamPlayers.Add(player.GetPlayerId());
                team.TeamPlayersUsernames.Add(player.DiscordUsername);
            }
            game.Teams.Add(team);
        }
    }

    private static void SeedTeamPredictions(GameClass game)
    {
        if (game.Teams.Count == 0) return;
        foreach (var player in game.PlayersList)
        foreach (var teammate in game.PlayersList.Where(candidate =>
                     candidate.GetPlayerId() != player.GetPlayerId()
                     && player.IsTeamMember(game, candidate.GetPlayerId())
                     && !UnknownBug.Is(candidate)))
            Kira.SetPrediction(player, teammate.GetPlayerId(), teammate.GameCharacter.Name);
    }

    private async Task HandoffToGameAsync(GameClass game)
    {
        foreach (var player in game.PlayersList.Where(player => player.PlayerType != 404))
        {
            var connections = _notificationService.GetConnections(player.DiscordId).ToList();
            foreach (var connectionId in connections)
            {
                try
                {
                    await _hubContext.Groups.AddToGroupAsync(connectionId, $"game-{game.GameId}");
                    _notificationService.RegisterGameConnection(game.GameId, connectionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[AlternativeLobby] Group handoff failed for {player.DiscordId}: {ex.Message}");
                }
            }
            if (connections.Count > 0)
            {
                try
                {
                    await _hubContext.Clients.Clients(connections)
                        .SendAsync("AlternativeLobbyGameStarted", new { gameId = game.GameId });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[AlternativeLobby] Navigation push failed for {player.DiscordId}: {ex.Message}");
                }
            }
        }
    }

    private async Task NotifyPreparationOpenedAsync(AlternativeLobby lobby)
    {
        foreach (var seat in lobby.Seats.Where(seat => seat.Kind == "human"))
        {
            var connections = _notificationService.GetConnections(seat.DiscordId).ToList();
            if (connections.Count > 0)
            {
                try
                {
                    await _hubContext.Clients.Clients(connections)
                        .SendAsync("AlternativeLobbyCreated", new { lobbyId = lobby.LobbyId });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[AlternativeLobby] Preparation navigation failed for {seat.DiscordId}: {ex.Message}");
                }
            }
        }
    }

    private async Task PushStateAsync(AlternativeLobby lobby)
    {
        List<(AlternativeLobbyStateDto State, List<string> Connections)> pushes;
        lock (lobby)
        {
            if (lobby.IsClosed) return;
            pushes = lobby.Seats
                .Where(seat => seat.Kind == "human")
                .GroupBy(seat => seat.DiscordId)
                .Select(group => (
                    BuildStateLocked(lobby, group.Key),
                    _notificationService.GetConnections(group.Key).ToList()))
                .Where(push => push.Item2.Count > 0)
                .ToList();
        }

        foreach (var (state, connections) in pushes)
            await _hubContext.Clients.Clients(connections)
                .SendAsync("AlternativeLobbyState", state);
    }

    private AlternativeLobbyStateDto BuildStateLocked(AlternativeLobby lobby, ulong viewerId)
    {
        var viewerIndex = Array.FindIndex(lobby.Seats,
            seat => seat.Kind == "human" && seat.DiscordId == viewerId);
        if (viewerIndex < 0) return null;
        var viewerTeam = TeamForSlot(viewerIndex, lobby.TeamSize);
        var viewerSeat = lobby.Seats[viewerIndex];
        var dto = new AlternativeLobbyStateDto
        {
            LobbyId = lobby.LobbyId,
            CreatorId = lobby.CreatorId.ToString(),
            Mode = lobby.Mode,
            Stage = lobby.Stage,
            TeamSize = lobby.TeamSize,
            AiDifficulty = lobby.AiDifficulty,
            DeadlineUtc = lobby.DeadlineUtc.ToString("O"),
            IsOwner = lobby.CreatorId == viewerId,
            IsAdminGame = lobby.IsAdminGame,
            CanJoin = CanJoinLocked(lobby),
        };

        for (var index = 0; index < SeatCount; index++)
        {
            var seat = lobby.Seats[index];
            var sameTeam = lobby.Mode is TeamMode or TeamAramMode
                           && TeamForSlot(index, lobby.TeamSize) == viewerTeam;
            var maySeeCharacter = seat.Ready
                                  && seat.SelectedCharacter != null
                                  && (sameTeam || seat.DiscordId == viewerId);
            dto.Seats.Add(new AlternativeLobbySeatDto
            {
                SlotIndex = index,
                Kind = seat.Kind,
                DiscordId = seat.DiscordId == 0 ? "" : seat.DiscordId.ToString(),
                Username = seat.Username,
                TeamId = lobby.Mode is TeamMode or TeamAramMode
                    ? TeamForSlot(index, lobby.TeamSize)
                    : 0,
                Ready = seat.Ready,
                CharacterConfirmed = seat.Ready && seat.SelectedCharacter != null,
                SelectedCharacter = maySeeCharacter
                    ? MapCharacter(seat.SelectedCharacter)
                    : null,
            });
        }

        if (lobby.Stage == CharacterStage && !viewerSeat.Ready)
            dto.CharacterOptions = viewerSeat.CharacterOptions.Select(MapCharacter).ToList();
        if (lobby.Stage == AramStage && viewerSeat.AramBuild != null)
            dto.AramBuild = MapAramBuild(viewerSeat.AramBuild, viewerSeat.Ready);
        return dto;
    }

    private static AlternativeCharacterDto MapCharacter(CharacterClass character) => new()
    {
        Name = character.Name,
        Avatar = character.Avatar,
        Tier = character.Tier,
        Intelligence = character.GetIntelligence(),
        Strength = character.GetStrength(),
        Speed = character.GetSpeed(),
        Psyche = character.GetPsyche(),
    };

    private static AlternativeAramBuildDto MapAramBuild(AramBuild build, bool locked) => new()
    {
        Intelligence = build.Intelligence,
        Strength = build.Strength,
        Speed = build.Speed,
        Psyche = build.Psyche,
        Locked = locked,
        PassiveSlots = build.PassiveSlots.Select((slot, index) => new AlternativePassiveSlotDto
        {
            SlotIndex = index,
            SelectedIndex = slot.SelectedIndex,
            Candidates = slot.Candidates.Select(passive => new AlternativePassiveDto
            {
                Name = passive.PassiveName,
                Description = passive.PassiveDescription,
            }).ToList(),
        }).ToList(),
    };

    private async Task CancelAsync(AlternativeLobby lobby, string error)
    {
        var removed = _lobbies.TryRemove(
            new KeyValuePair<ulong, AlternativeLobby>(lobby.LobbyId, lobby));
        lock (lobby)
        {
            if (lobby.IsClosed && !removed) return;
            lobby.IsClosed = true;
            lobby.IsStarting = false;
        }
        ReleaseAccounts(lobby);
        foreach (var seat in lobby.Seats.Where(seat => seat.Kind == "human"))
        {
            var connections = _notificationService.GetConnections(seat.DiscordId).ToList();
            if (connections.Count > 0)
                await _hubContext.Clients.Clients(connections).SendAsync("Error", error);
        }
    }

    private void ReleaseAccounts(AlternativeLobby lobby)
    {
        foreach (var playerId in lobby.Seats
                     .Where(seat => seat.Kind != "empty")
                     .Select(seat => seat.DiscordId)
                     .Distinct())
        {
            var account = _userAccounts.GetAccount(playerId);
            if (account == null) continue;
            lock (account)
                account.IsPlaying = false;
        }
    }

    private static bool CanJoinLocked(AlternativeLobby lobby) =>
        !lobby.IsClosed
        && !lobby.IsStarting
        && lobby.Seats.Any(seat => seat.Kind == "empty")
        && (lobby.Stage == TeamStage || lobby.Mode == AramMode && lobby.Stage == AramStage)
        && !lobby.Seats.Any(seat => seat.Kind == "human" && seat.Ready);

    private static AlternativeSeat FindHumanSeatLocked(AlternativeLobby lobby, ulong playerId) =>
        lobby.Seats.FirstOrDefault(seat => seat.Kind == "human" && seat.DiscordId == playerId);

    private static IEnumerable<AlternativeSeat> HumansLocked(AlternativeLobby lobby) =>
        lobby.Seats.Where(seat => seat.Kind == "human");

    private static int TeamForSlot(int slotIndex, int teamSize) => slotIndex / teamSize + 1;

    private static void ResetTeamReadinessLocked(AlternativeLobby lobby)
    {
        if (lobby.Stage != TeamStage) return;
        foreach (var human in HumansLocked(lobby))
            human.Ready = false;
    }

    private static AlternativeLobby NewLobby(
        ulong lobbyId,
        ulong creatorId,
        string mode,
        bool adminGame) => new()
    {
        LobbyId = lobbyId,
        CreatorId = creatorId,
        Mode = mode,
        Stage = mode == AramMode ? AramStage : TeamStage,
        IsAdminGame = adminGame,
        CreatedUtc = DateTime.UtcNow,
        DeadlineUtc = DateTime.UtcNow.AddSeconds(LobbySeconds),
        Seats = Enumerable.Range(0, SeatCount).Select(_ => new AlternativeSeat()).ToArray(),
    };

    private static AlternativeSeat NewHumanSeat(
        ulong playerId,
        string username,
        bool hasWebConnection,
        int aiDifficulty) => new()
    {
        Kind = "human",
        DiscordId = playerId,
        Username = username ?? playerId.ToString(),
        HasWebConnection = hasWebConnection,
        AiDifficulty = aiDifficulty,
    };

    public void Dispose()
    {
        _expiryTimer.Stop();
        _expiryTimer.Dispose();
    }
}

internal sealed class AlternativeLobby
{
    public ulong LobbyId { get; init; }
    public ulong CreatorId { get; init; }
    public string Mode { get; init; } = AlternativeModeLobbyService.TeamMode;
    public string Stage { get; set; } = AlternativeModeLobbyService.TeamStage;
    public int TeamSize { get; set; } = 2;
    public int AiDifficulty { get; set; } = 4;
    public DateTime CreatedUtc { get; init; }
    public DateTime DeadlineUtc { get; init; }
    public bool IsAdminGame { get; init; }
    public bool IsStarting { get; set; }
    public bool IsClosed { get; set; }
    public AlternativeSeat[] Seats { get; init; }
}

internal sealed class AlternativeSeat
{
    public string Kind { get; set; } = "empty";
    public ulong DiscordId { get; set; }
    public string Username { get; set; } = "";
    public bool HasWebConnection { get; set; }
    public int AiDifficulty { get; set; }
    public bool Ready { get; set; }
    public List<CharacterClass> CharacterOptions { get; set; } = new();
    public CharacterClass SelectedCharacter { get; set; }
    public AramBuild AramBuild { get; set; }

    public AlternativeSeat Clone() => new()
    {
        Kind = Kind,
        DiscordId = DiscordId,
        Username = Username,
        HasWebConnection = HasWebConnection,
        AiDifficulty = AiDifficulty,
        Ready = Ready,
        CharacterOptions = CharacterOptions.Select(character => character.DeepCopy()).ToList(),
        SelectedCharacter = SelectedCharacter?.DeepCopy(),
        AramBuild = AramBuild?.Clone(),
    };
}

internal sealed class AramBuild
{
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Speed { get; set; }
    public int Psyche { get; set; }
    public List<AramPassiveSlot> PassiveSlots { get; set; } = new();

    public AramBuild Clone() => new()
    {
        Intelligence = Intelligence,
        Strength = Strength,
        Speed = Speed,
        Psyche = Psyche,
        PassiveSlots = PassiveSlots.Select(slot => slot.Clone()).ToList(),
    };
}

internal sealed class AramPassiveSlot
{
    public List<Passive> Candidates { get; set; } = new();
    public int SelectedIndex { get; set; } = -1;

    public AramPassiveSlot Clone() => new()
    {
        Candidates = Candidates.Select(passive => passive.DeepCopy()).ToList(),
        SelectedIndex = SelectedIndex,
    };
}

public sealed class AlternativeLobbySeedSeat
{
    public string Kind { get; set; } = "empty";
    public ulong DiscordId { get; set; }
    public string Username { get; set; } = "";
    public int AiDifficulty { get; set; }
    public bool HasWebConnection { get; set; }
}

public sealed class AlternativeLobbyStateDto
{
    public ulong LobbyId { get; set; }
    public string CreatorId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string Stage { get; set; } = "";
    public int TeamSize { get; set; }
    public int AiDifficulty { get; set; }
    public string DeadlineUtc { get; set; } = "";
    public bool IsOwner { get; set; }
    public bool IsAdminGame { get; set; }
    public bool CanJoin { get; set; }
    public List<AlternativeLobbySeatDto> Seats { get; set; } = new();
    public List<AlternativeCharacterDto> CharacterOptions { get; set; } = new();
    public AlternativeAramBuildDto AramBuild { get; set; }
}

public sealed class AlternativeLobbySeatDto
{
    public int SlotIndex { get; set; }
    public string Kind { get; set; } = "empty";
    public string DiscordId { get; set; } = "";
    public string Username { get; set; } = "";
    public int TeamId { get; set; }
    public bool Ready { get; set; }
    public bool CharacterConfirmed { get; set; }
    public AlternativeCharacterDto SelectedCharacter { get; set; }
}

public sealed class AlternativeCharacterDto
{
    public string Name { get; set; } = "";
    public string Avatar { get; set; } = "";
    public int Tier { get; set; }
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Speed { get; set; }
    public int Psyche { get; set; }
}

public sealed class AlternativeAramBuildDto
{
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Speed { get; set; }
    public int Psyche { get; set; }
    public bool Locked { get; set; }
    public List<AlternativePassiveSlotDto> PassiveSlots { get; set; } = new();
}

public sealed class AlternativePassiveSlotDto
{
    public int SlotIndex { get; set; }
    public int SelectedIndex { get; set; }
    public List<AlternativePassiveDto> Candidates { get; set; } = new();
}

public sealed class AlternativePassiveDto
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}
