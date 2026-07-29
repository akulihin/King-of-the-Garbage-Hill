using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using Microsoft.Extensions.DependencyInjection;

namespace King_of_the_Garbage_Hill.API.Services;

/// <summary>
/// Bridges web API requests to the existing game logic.
/// Operates on the same GameClass / GamePlayerBridgeClass objects
/// that the Discord bot and CheckIfReady timer use.
/// 
/// No rate limits — the web client doesn't have Discord's constraints.
/// </summary>
public class WebGameService
{
    /// <summary>Set to true to enable draft pick phase for all games, false to skip straight to game.</summary>
    public const bool EnableDraftPick = true;

    private readonly Global _global;
    private readonly GameReaction _gameReaction;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly HelperFunctions _helper;
    private readonly CharactersPull _charactersPull;
    private readonly CharacterPassives _characterPassives;
    private readonly StartGameLogic _startGameLogic;
    private readonly UserAccounts _userAccounts;
    private readonly IServiceProvider _serviceProvider;
    private readonly SecureRandom _secureRandom;

    public WebGameService(Global global, GameReaction gameReaction, GameUpdateMess gameUpdateMess,
        HelperFunctions helper, CharactersPull charactersPull, CharacterPassives characterPassives,
        StartGameLogic startGameLogic, UserAccounts userAccounts, IServiceProvider serviceProvider,
        SecureRandom secureRandom)
    {
        _global = global;
        _gameReaction = gameReaction;
        _gameUpdateMess = gameUpdateMess;
        _helper = helper;
        _charactersPull = charactersPull;
        _characterPassives = characterPassives;
        _startGameLogic = startGameLogic;
        _userAccounts = userAccounts;
        _serviceProvider = serviceProvider;
        _secureRandom = secureRandom;
    }

    private AdminLobbyService AdminLobbies =>
        _serviceProvider.GetService<AdminLobbyService>();

    // ── Queries ───────────────────────────────────────────────────────

    private static GamePlayerBridgeClass FindPreInitializationNaruto(GameClass game)
    {
        var original = game.PlayersList.Find(player =>
            player.GameCharacter.Name == Naruto.CharacterName && !Naruto.IsClone(player));
        return original != null && game.PlayersList.All(player => !Naruto.IsClone(player))
            ? original
            : null;
    }

    private static List<GamePlayerBridgeClass> GetJoinableStrictBots(GameClass game)
    {
        var candidates = game.PlayersList
            .Where(player => player.PlayerType == 404 && !Naruto.IsClone(player))
            .ToList();
        var pendingNaruto = FindPreInitializationNaruto(game);
        if (pendingNaruto == null) return candidates;

        var strictBotCount = game.PlayersList.Count(player => player.PlayerType == 404);
        if (strictBotCount <= 2) return new List<GamePlayerBridgeClass>();

        // With exactly three bots and a bot as the pending original, replacing any
        // other bot would leave only one clone candidate. Turn the original into the
        // joining human instead; the other two strict bots can then become clones.
        if (strictBotCount == 3 && pendingNaruto.PlayerType == 404)
            return candidates.Where(player => ReferenceEquals(player, pendingNaruto)).ToList();

        return candidates;
    }

    private static void ResetBotOwnedStateForHuman(
        GamePlayerBridgeClass seat,
        GameClass game)
    {
        // A bridge represents the seat, so gameplay state legitimately survives its owner.
        // Bot inference does not: exposing its guesses/memory to the joining human both leaks
        // privileged implementation state and gives the new owner predictions they never made.
        // Replace the collection before publishing the new owner identity. State projection is
        // lock-free, so mutating the old list in place could expose one stale DTO or race its mapper.
        seat.Predict = new List<PredictClass>();
        seat.AiKnowledge = new BotKnowledgeState();
        seat.AiPlaystyle = "";
        seat.AiDifficulty = -1;
        seat.ConsecutiveBotBlocks = 0;

        seat.Status.ConfirmedPredict =
            game.RoundNo != 8
            || game.GameMode == "Aram"
            || seat.Passives.IsDead
            || Madara.IsMadara(seat)
            || UnknownBug.Is(seat)
            || seat.GameCharacter.DoomRollMode
            || seat.GameCharacter.Passive.Any(passive =>
                passive.PassiveName is "Тетрадь смерти" or "Булькает");
    }

    public LobbyStateDto GetLobbyState()
    {
        AdminLobbies?.SweepExpiredLobbies();

        var dto = new LobbyStateDto
        {
            ActiveGames = _global.GamesList.Count,
        };

        foreach (var game in _global.GamesList)
        {
            var botCount = game.PlayersList.Count(p => p.PlayerType == 404);
            dto.Games.Add(new ActiveGameDto
            {
                GameId = game.GameId,
                RoundNo = game.RoundNo,
                PlayerCount = game.PlayersList.Count,
                HumanCount = game.PlayersList.Count(p => p.PlayerType != 404),
                GameMode = game.GameMode,
                IsFinished = game.IsFinished,
                BotCount = botCount,
                CanJoin = !game.IsRanked
                          && GetJoinableStrictBots(game).Count > 0
                          && !game.IsFinished,
            });
        }

        return dto;
    }

    public GameStateDto GetGameState(ulong gameId, ulong discordId)
    {
        var game = FindGame(gameId);
        if (game == null) return null;

        var player = game.PlayersList.Find(p => p.DiscordId == discordId);
        var dto = player == null ? GameStateMapper.ToDto(game) : GameStateMapper.ToDto(game, player,
            _userAccounts.GetAccount(discordId) ?? new DiscordAccountClass());
        PopulateCustomLeaderboard(dto, game, player, _gameUpdateMess);
        return dto;
    }

    public GameStateDto GetGameStateForSpectator(ulong gameId)
    {
        var game = FindGame(gameId);
        return game == null ? null : GameStateMapper.ToDto(game);
    }

    /// <summary>
    /// Discord emoji map: converts &lt;:name:id&gt; to local /art/emojis/ images or Unicode fallbacks.
    /// </summary>
    private static readonly Dictionary<string, string> EmojiMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Weedwick
        { "weed", "<img class='lb-emoji' src='/art/emojis/weed.png'/>" },
        { "bong", "<img class='lb-emoji' src='/art/emojis/bone_1.png'/>" },
        { "WUF", "<img class='lb-emoji' src='/art/emojis/wolf_mark.png'/>" },
        // Pets
        { "pet", "<img class='lb-emoji' src='/art/emojis/collar.png'/>" },
        // Tigr
        { "pepe_down", "<img class='lb-emoji' src='/art/emojis/pepe.png'/>" },
        // Spartan / Mylorik
        { "sparta", "<img class='lb-emoji' src='/art/emojis/spartan_mark.png'/>" },
        { "Spartaneon", "<img class='lb-emoji' src='/art/emojis/sparta.png'/>" },
        { "pantheon", "<img class='lb-emoji' src='/art/emojis/spartan_mark.png'/>" },
        { "yasuo", "<img class='lb-emoji' src='/art/emojis/shame_shame.png'/>" },
        { "broken_shield", "<img class='lb-emoji' src='/art/emojis/broken_shield.png'/>" },
        // DeepList
        { "yo_filled", "<img class='lb-emoji' src='/art/emojis/gambit.png'/>" },
        // Vampyr
        { "Y_", "<img class='lb-emoji' src='/art/emojis/vampyr_mark.png'/>" },
        // Ranks / Awdka
        { "bronze", "<img class='lb-emoji' src='/art/emojis/bronze.png'/>" },
        { "plat", "<img class='lb-emoji' src='/art/emojis/plat.png'/>" },
        // HardKitty
        { "393", "<img class='lb-emoji' src='/art/emojis/mail_2.png'/>" },
        { "LoveLetter", "<img class='lb-emoji' src='/art/emojis/mail_1.png'/>" },
        // Sirinoks
        { "fr", "<img class='lb-emoji' src='/art/emojis/friend.png'/>" },
        { "edu", "<img class='lb-emoji' src='/art/emojis/learning.png'/>" },
        // Jaws (Shark)
        { "jaws", "<img class='lb-emoji' src='/art/emojis/fin.png'/>" },
        // Luck
        { "luck", "<img class='lb-emoji' src='/art/emojis/luck.png'/>" },
        // Generic
        { "e_", "" },
        { "war", "<img class='lb-emoji' src='/art/emojis/war.png'/>" },
        { "volibir", "<img class='lb-emoji' src='/art/emojis/voli.png'/>" },
        { "🐙", "<img class='lb-emoji' src='/art/emojis/fish.png'/>" },
    };

    /// <summary>Converts Discord markdown + custom emojis to web-safe HTML.</summary>
    public static string ConvertDiscordToWeb(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        // Replace Discord custom emojis: <:name:id> or <a:name:id>
        text = Regex.Replace(text, @"<a?:(\w+):\d+>", match =>
        {
            var name = match.Groups[1].Value;
            return EmojiMap.TryGetValue(name, out var replacement) ? replacement : $"[{name}]";
        });

        // Discord markdown → HTML
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        text = Regex.Replace(text, @"__(.+?)__", "<u>$1</u>");
        text = Regex.Replace(text, @"\*(.+?)\*", "<em>$1</em>");
        text = Regex.Replace(text, @"~~(.+?)~~", "<del>$1</del>");
        // Custom color tags: {color:#hex}text{/color} → <span style="color:#hex">text</span>
        text = Regex.Replace(text, @"\{color:(#[0-9A-Fa-f]{3,8})\}(.+?)\{/color\}", "<span style=\"color:$1\">$2</span>");
        text = text.Replace("\n", "<br/>");

        return text.Trim();
    }

    /// <summary>
    /// Populates CustomLeaderboardText and CustomLeaderboardPrefix on each PlayerDto.
    /// Called from both WebGameService (REST) and GameNotificationService (SignalR).
    /// </summary>
    public static void PopulateCustomLeaderboard(GameStateDto dto, GameClass game,
        GamePlayerBridgeClass viewingPlayer, GameUpdateMess gameUpdateMess)
    {
        if (viewingPlayer == null || gameUpdateMess == null) return;

        // Pre-compute harm range data for the viewing player
        var showHarmRange = game.RoundNo > 1
            && !viewingPlayer.GameCharacter.Passive.Any(x => x.PassiveName == "Минька")
            && viewingPlayer.GameCharacter.Name != Madara.CharacterName;
        var myRange = viewingPlayer.GameCharacter.GetSpeedQualityResistInt();
        var myPlace = viewingPlayer.Status.GetPlaceAtLeaderBoard();

        foreach (var playerDto in dto.Players)
        {
            var otherPlayer = game.PlayersList.Find(p => p.GetPlayerId() == playerDto.PlayerId);
            if (otherPlayer != null)
            {
                var rawAfter = gameUpdateMess.CustomLeaderBoardAfterPlayer(viewingPlayer, otherPlayer, game, isWeb: true);
                playerDto.CustomLeaderboardText = ConvertDiscordToWeb(rawAfter);

                var rawBefore = gameUpdateMess.CustomLeaderBoardBeforeNumber(viewingPlayer, otherPlayer, game, playerDto.Status.Place);
                playerDto.CustomLeaderboardPrefix = ConvertDiscordToWeb(rawBefore);

                // Harm range indicator
                if (showHarmRange && otherPlayer.GetPlayerId() != viewingPlayer.GetPlayerId())
                {
                    var effectiveRange = myRange - otherPlayer.GameCharacter.GetSpeedQualityKiteBonus();
                    var placeDiff = Math.Abs(myPlace - otherPlayer.Status.GetPlaceAtLeaderBoard());
                    playerDto.IsInMyHarmRange = placeDiff <= effectiveRange;
                }
            }
        }
    }

    // ── Web Game Creation / Joining ─────────────────────────────────

    /// <summary>
    /// Creates a new 6-player game from the web (1 creator + 5 bots).
    /// Mirrors the flow in General.cs StartGame but skips Discord DMs.
    /// </summary>
    public async Task<(ulong gameId, string error)> CreateGame(
        ulong creatorId,
        string creatorUsername,
        bool recordNaturalUnknownBugRoll = true,
        bool adminLobbyMode = false,
        bool ranked = false)
    {
        if (AdminLobbies?.IsReserved(creatorId) == true)
            return (0, "Вы были избраны богом.");

        var creatorAccount = _userAccounts.GetAccount(creatorId);
        if (creatorAccount == null)
            return (0, "Account not found");
        if (creatorAccount.IsPlaying)
            return (0, "Already in a game");
        if (ranked && creatorAccount.GameplayMode != DiscordAccountClass.ProMode)
            return (0, "Ranked games require Pro mode.");

        string queuedCharacterName;
        string forcedCharacterName;
        bool rollCreatorDirectly;
        lock (creatorAccount)
        {
            creatorAccount.LootBoxCharacterQueue ??= new List<string>();
            queuedCharacterName = adminLobbyMode
                ? null
                : creatorAccount.LootBoxCharacterQueue.FirstOrDefault();
            forcedCharacterName = adminLobbyMode
                ? null
                : creatorAccount.CharacterToGiveNextTime;
            rollCreatorDirectly = adminLobbyMode || string.IsNullOrWhiteSpace(queuedCharacterName);
        }

        // A normal web creator owns one real roll seat. That direct path also lets the
        // shared roller consume a forced-next assignment; queued loot retains its legacy
        // guaranteed replacement path.
        var gameId = _global.GetNewtGamePlayingAndId();
        var players = new List<Discord.IUser> { null, null, null, null, null, null };
        var playersList = _startGameLogic.HandleCharacterRoll(
            players,
            gameId,
            mode: "bot",
            accountForFirstBotSlot: rollCreatorDirectly ? creatorAccount : null,
            recordNaturalUnknownBugRoll: recordNaturalUnknownBugRoll,
            ignoreNextCharacterAssignments: adminLobbyMode);

        // Shuffle and sort
        playersList = playersList.OrderBy(_ => Guid.NewGuid()).ToList();
        playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();

        // Replace the matching bot when a loot box queued a guaranteed character. If that
        // character did not naturally enter this roster, replace the ordinary first bot with it.
        var queuedCharacter = string.IsNullOrWhiteSpace(queuedCharacterName)
            ? null
            : _charactersPull.GetVisibleCharacters().Find(character =>
                character.Name == queuedCharacterName);
        if (queuedCharacter != null)
        {
            lock (creatorAccount)
            {
                if (creatorAccount.LootBoxCharacterQueue.Count == 0
                    || !string.Equals(
                        creatorAccount.LootBoxCharacterQueue[0],
                        queuedCharacterName,
                        StringComparison.Ordinal))
                {
                    queuedCharacter = null;
                }
                else
                {
                    creatorAccount.LootBoxCharacterQueue.RemoveAt(0);
                }
            }
        }
        var botToReplace = queuedCharacter == null
            ? rollCreatorDirectly
                ? playersList.Find(player => player.DiscordId == creatorId) ?? playersList[0]
                : playersList[0]
            : playersList.Find(player => player.GameCharacter.Name == queuedCharacter.Name)
              ?? playersList.Find(player => StartGameLogic.AreMutuallyExclusiveCharacters(
                  player.GameCharacter.Name, queuedCharacter.Name))
              ?? playersList[0];
        if (queuedCharacter != null && botToReplace.GameCharacter.Name != queuedCharacter.Name)
        {
            var replacementIndex = playersList.IndexOf(botToReplace);
            botToReplace = new GamePlayerBridgeClass(
                queuedCharacter,
                new InGameStatus(),
                botToReplace.DiscordId,
                gameId,
                botToReplace.DiscordUsername,
                botToReplace.PlayerType,
                botToReplace.AccountGameplayMode);
            playersList[replacementIndex] = botToReplace;
        }

        if (queuedCharacter != null)
        {
            botToReplace.IsLootBoxCharacterReward = true;
            creatorAccount.CharacterPlayedLastTime = queuedCharacter.Name;
        }

        var oldBotAccount = _userAccounts.GetAccount(botToReplace.DiscordId);
        if (oldBotAccount != null && oldBotAccount.DiscordId != creatorId)
            oldBotAccount.IsPlaying = false;

        botToReplace.DiscordId = creatorId;
        botToReplace.DiscordUsername = creatorUsername;
        botToReplace.PlayerType = creatorAccount.PlayerType;
        botToReplace.AccountGameplayMode = botToReplace.GameCharacter.Tier == 0
            ? DiscordAccountClass.ProMode
            : GamePlayerBridgeClass.NormalizeGameplayMode(creatorAccount.GameplayMode);
        botToReplace.IsWebPlayer = true;
        botToReplace.PreferWeb = true;
        if (queuedCharacter != null)
            botToReplace.CharacterMasteryPoints =
                creatorAccount.CharacterMastery.GetValueOrDefault(queuedCharacter.Name, 0);
        if (!rollCreatorDirectly)
            DoomGuy.InitializeForGame(botToReplace, creatorAccount);
        creatorAccount.IsPlaying = true;
        if (!adminLobbyMode)
            DiscoverStoreCharacter(creatorAccount, botToReplace.GameCharacter.Name);
        if (queuedCharacter != null)
            _userAccounts.SaveAccount(creatorAccount);

        // Create game
        var game = new GameClass(
            playersList,
            gameId,
            creatorId,
            gameMode: ranked ? "Ranked" : "Normal")
        {
            IsCheckIfReady = false,
            IsRanked = ranked,
            AiDifficulty = 3,
        };
        game.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));
        game.TimePassed.Start();
        lock (_global.GamesList)
        {
            _global.GamesList.Add(game);
        }

        if (adminLobbyMode)
        {
            foreach (var player in playersList)
                player.Status.IsDraftPickConfirmed = true;
            Console.WriteLine(
                $"[WebAPI] Admin lobby game {gameId} staged by {creatorUsername} ({creatorId})");
        }
        else if (EnableDraftPick)
        {
            // Draft pick: a private natural roll is locked immediately and is never exposed
            // as an option that can be inspected and declined.
            var originalCharacter = botToReplace.GameCharacter;
            var forcedCharacterAssigned = rollCreatorDirectly
                                          && !string.IsNullOrWhiteSpace(forcedCharacterName)
                                          && string.Equals(
                                              originalCharacter.Name,
                                              forcedCharacterName,
                                              StringComparison.Ordinal);
            if (UnknownBug.Is(originalCharacter)
                || Cthulhu.Is(originalCharacter)
                || botToReplace.IsLootBoxCharacterReward
                || forcedCharacterAssigned)
            {
                botToReplace.Status.IsDraftPickConfirmed = true;
            }
            else
            {
                var excludedCharacters = playersList.Select(x => x.GameCharacter).ToList();
                var strictBotCount = playersList.Count(p => p.PlayerType == 404);
                var draftOptions = _startGameLogic.RollDraftOptions(creatorAccount,
                    excludedCharacters, strictBotCount, count: 2);
                var newcomerDoom = draftOptions.Find(x => x.Name == DoomGuy.CharacterName);
                if (newcomerDoom != null)
                {
                    draftOptions.Remove(newcomerDoom);
                    draftOptions.Insert(0, newcomerDoom);
                    draftOptions.Add(originalCharacter);
                }
                else
                {
                    draftOptions.Insert(0, originalCharacter);
                }
                game.DraftOptions[botToReplace.GetPlayerId()] = draftOptions;
            }
            game.IsDraftPickPhase = true;

            foreach (var p in playersList.Where(p => p.IsBot()))
                p.Status.IsDraftPickConfirmed = true;

            game.IsCheckIfReady = true;
            Console.WriteLine($"[WebAPI] Web game {gameId} created by {creatorUsername} ({creatorId}) — draft pick phase");
        }
        else
        {
            if (Cthulhu.RequiresPreGameStage(playersList))
            {
                game.IsDraftPickPhase = true;
                foreach (var player in playersList)
                    player.Status.IsDraftPickConfirmed = true;
            }
            else
            {
                // No draft: run initialization immediately (original flow)
                playersList = _characterPassives.HandleEventsBeforeFirstRound(playersList);
                game.PlayersList = playersList;
                game.ExploitPlayersList = playersList
                    .Where(player => !UnknownBug.Is(player) && !player.Passives.IsDead).ToList();
                for (var i = 0; i < playersList.Count; i++)
                    playersList[i].Status.SetPlaceAtLeaderBoard(i + 1);

                game.RollExploit();
                await _characterPassives.HandleNextRound(game);
                _characterPassives.HandleBotPredict(game);
            }

            game.IsCheckIfReady = true;
            Console.WriteLine($"[WebAPI] Web game {gameId} created by {creatorUsername} ({creatorId})");
        }

        return (gameId, null);
    }

    /// <summary>
    /// Joins an existing game by replacing a random bot with the web player.
    /// </summary>
    public (bool success, string error) JoinWebGame(ulong gameId, ulong playerId, string playerUsername)
    {
        if (AdminLobbies?.IsReserved(playerId) == true)
            return (false, "Вы были избраны богом.");

        var game = FindGame(gameId);
        if (game == null) return (false, "Game not found");
        if (game.IsFinished) return (false, "Game is finished");
        if (game.IsRanked) return (false, "Ranked games cannot be joined.");
        if (game.PlayersList.Any(p => p.DiscordId == playerId)) return (true, null);

        var playerAccount = _userAccounts.GetAccount(playerId);
        if (playerAccount == null) return (false, "Account not found");

        lock (game)
        {
            if (game.IsFinished) return (false, "Game is finished");

            // If player is already in this game, just return success.
            var existingPlayer = game.PlayersList.Find(p => p.DiscordId == playerId);
            if (existingPlayer != null) return (true, null);
            if (playerAccount.IsPlaying) return (false, "Already in a game");

            // Clones are structural bot seats. A pending Naruto roster must also retain
            // the two strict bot seats needed for clone initialization.
            var bot = GetJoinableStrictBots(game).FirstOrDefault();
            if (bot == null) return (false, "No bot slots available");

            // Release the bot account
            var botAccount = _userAccounts.GetAccount(bot.DiscordId);
            if (botAccount != null) botAccount.IsPlaying = false;

            // Clear private bot inference before publishing the human identity. Notification
            // projection is lock-free and must never observe a human-owned bridge with bot guesses.
            ResetBotOwnedStateForHuman(bot, game);

            // Replace bot with the joining player
            bot.DiscordUsername = playerUsername;
            bot.PlayerType = playerAccount.PlayerType;
            bot.AccountGameplayMode = bot.GameCharacter.Tier == 0
                ? DiscordAccountClass.ProMode
                : GamePlayerBridgeClass.NormalizeGameplayMode(playerAccount.GameplayMode);
            bot.IsWebPlayer = true;
            bot.PreferWeb = true;
            bot.DiscordStatus.SocketGameMessage = null;
            DoomGuy.InitializeForGame(bot, playerAccount);
            playerAccount.IsPlaying = true;
            DiscoverStoreCharacter(playerAccount, bot.GameCharacter.Name);
            // Viewer lookup keys on DiscordId, so publish it only after all private/identity
            // state is ready for the new owner.
            bot.DiscordId = playerId;
        }

        Console.WriteLine($"[WebAPI] Player {playerUsername} ({playerId}) joined game {gameId}");
        return (true, null);
    }

    /// <summary>
    /// Replaces the exact staged admin-lobby seat with a human while retaining the ordinary
    /// bot-to-human account, DooM Fortress and discovery boundaries used by JoinWebGame.
    /// </summary>
    public (bool success, string error) SeatAdminLobbyHuman(
        GameClass game,
        int slotIndex,
        ulong playerId,
        string playerUsername,
        bool hasWebConnection)
    {
        if (game == null || slotIndex < 0 || slotIndex >= game.PlayersList.Count)
            return (false, "Invalid admin lobby slot");

        var playerAccount = _userAccounts.GetAccount(playerId);
        if (playerAccount == null)
            return (false, "Account not found");

        lock (game)
        {
            var seat = game.PlayersList[slotIndex];
            if (seat.DiscordId != playerId && playerAccount.IsPlaying)
                return (false, "пользователь уже играет");

            var isOwnershipTransfer = seat.DiscordId != playerId;
            if (isOwnershipTransfer)
            {
                var botAccount = _userAccounts.GetAccount(seat.DiscordId);
                if (botAccount != null)
                    botAccount.IsPlaying = false;

                // The generated seat can currently belong to a bot or to the lobby creator.
                // Clear owner-private state before making the replacement identity observable.
                ResetBotOwnedStateForHuman(seat, game);
            }

            seat.DiscordUsername = playerUsername;
            seat.PlayerType = playerAccount.PlayerType;
            seat.AccountGameplayMode = seat.GameCharacter.Tier == 0
                ? DiscordAccountClass.ProMode
                : GamePlayerBridgeClass.NormalizeGameplayMode(playerAccount.GameplayMode);
            seat.IsWebPlayer = hasWebConnection
                               || playerId >= 9_000_000_000_000_000_000;
            seat.PreferWeb = hasWebConnection;
            seat.DiscordStatus.SocketGameMessage = null;
            DoomGuy.InitializeForGame(seat, playerAccount);
            seat.CharacterMasteryPoints =
                playerAccount.CharacterMastery.GetValueOrDefault(seat.GameCharacter.Name, 0);
            playerAccount.IsPlaying = true;
            playerAccount.CharacterPlayedLastTime = seat.GameCharacter.Name;
            DiscoverStoreCharacter(playerAccount, seat.GameCharacter.Name);
            seat.DiscordId = playerId;
        }

        return (true, null);
    }

    /// <summary>
    /// Completes the ordinary no-draft initialization after an admin lobby has installed its
    /// final seats and characters. The Cthulhu pre-game stage remains deferred to CheckIfReady.
    /// </summary>
    public async Task FinalizeAdminLobbyGame(GameClass game)
    {
        if (game == null) return;

        foreach (var player in game.PlayersList)
            player.Status.IsDraftPickConfirmed = true;

        game.CthulhuState.RosterHadCthulhu = game.PlayersList.Any(Cthulhu.Is);
        game.IsDraftPickPhase = Cthulhu.RequiresPreGameStage(game.PlayersList);
        RebuildAdminLobbyRosterReferences(game);

        if (!game.IsDraftPickPhase)
        {
            var initializedPlayers = _characterPassives.HandleEventsBeforeFirstRound(game.PlayersList);
            game.PlayersList = initializedPlayers;
            RebuildAdminLobbyRosterReferences(game);
        }

        foreach (var player in game.PlayersList.Where(player =>
                     player.PlayerType != 404
                     && !player.IsWebPlayer
                     && !player.PreferWeb))
        {
            try
            {
                await _gameUpdateMess.WaitMess(player, game);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WebAPI] Admin lobby Discord setup failed for {player.DiscordId}: {ex.Message}");
            }
        }

        if (!game.IsDraftPickPhase)
        {
            await _characterPassives.HandleNextRound(game);
            _characterPassives.HandleBotPredict(game);
        }

        foreach (var player in game.PlayersList.Where(player =>
                     player.PlayerType != 404
                     && !player.IsWebPlayer
                     && !player.PreferWeb
                     && player.DiscordStatus.SocketGameMessage != null))
        {
            try
            {
                await _gameUpdateMess.UpdateMessage(player);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[WebAPI] Admin lobby Discord refresh failed for {player.DiscordId}: {ex.Message}");
            }
        }

        game.TimePassed.Restart();
    }

    public void AbortAdminLobbyGame(GameClass game)
    {
        if (game == null) return;

        game.IsCheckIfReady = false;
        game.IsFinished = true;
        game.TimePassed.Stop();
        lock (_global.GamesList)
            _global.GamesList.Remove(game);

        foreach (var player in game.PlayersList)
        {
            var account = _userAccounts.GetAccount(player.DiscordId);
            if (account != null)
                account.IsPlaying = false;
        }
    }

    private static void RebuildAdminLobbyRosterReferences(GameClass game)
    {
        game.NanobotsList.Clear();
        game.NanobotsList.Add(new BotsBehavior.NanobotClass(game.PlayersList));
        game.ExploitPlayersList = game.PlayersList
            .Where(player => !UnknownBug.Is(player) && !player.Passives.IsDead)
            .ToList();
        for (var i = 0; i < game.PlayersList.Count; i++)
            game.PlayersList[i].Status.SetPlaceAtLeaderBoard(i + 1);
        game.RollExploit();
    }

    // ── Draft Pick ──────────────────────────────────────────────────

    public Task<(bool success, string error)> BeginAdeptChoice(
        ulong gameId,
        ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));

        lock (game)
        {
            return Task.FromResult(Cthulhu.TryBeginAdeptStage(
                    game, player, _charactersPull)
                ? (true, (string)null)
                : (false, "Adept choice is not available"));
        }
    }

    public Task<(bool success, string error)> DraftSelect(ulong gameId, ulong discordId, string characterName)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));

        // Web and Discord draft picks share this monitor so duplicate validation, a paid pick and
        // the bridge replacement form one serialized decision.
        lock (game)
        {
            if (!game.PlayersList.Contains(player)) return Task.FromResult((false, "Player not in this game"));
            if (!game.DraftOptions.TryGetValue(player.GetPlayerId(), out var options))
                return Task.FromResult((false, "No draft options found"));

            var selected = options.Find(c => c.Name == characterName);
            if (selected == null) return Task.FromResult((false, "Character not in your draft options"));
            if (game.CthulhuState.AdeptStageActive && Cthulhu.IsUntransformed(player))
            {
                var herald = Cthulhu.ApplyAdeptChoice(
                    game, player, selected.Name, _userAccounts, _charactersPull,
                    _secureRandom);
                return Task.FromResult(herald != null
                    ? (true, (string)null)
                    : (false, "Adept choice is no longer available"));
            }
            if (!game.IsDraftPickPhase) return Task.FromResult((false, "Not in draft pick phase"));
            if (player.Status.IsDraftPickConfirmed) return Task.FromResult((false, "Already confirmed"));
            if (UnknownBug.Is(selected))
                return Task.FromResult((false, "Character is not available as a draft choice"));

            if (selected.Name == Naruto.CharacterName && !Naruto.CanInitializeForDraft(game))
                return Task.FromResult((false, "Naruto requires at least two bot slots"));

            // Validate the shared game constraint before touching the account economy.
            if (game.PlayersList.Any(p => p != player
                                          && (p.GameCharacter.Name == characterName
                                              || StartGameLogic.AreMutuallyExclusiveCharacters(
                                                  p.GameCharacter.Name, characterName))))
                return Task.FromResult((false, "Character already taken by another player"));

            var idx = game.PlayersList.IndexOf(player);
            if (idx < 0) return Task.FromResult((false, "Player not in this game"));

            var selectedIndex = options.IndexOf(selected);
            var account = _userAccounts.GetAccount(discordId);
            if (selectedIndex > 0 && account == null)
                return Task.FromResult((false, "Account not found"));

            // Build the replacement before a paid transaction so construction cannot strand a debit.
            var newBridge = new GamePlayerBridgeClass(
                selected,
                new InGameStatus(),
                player.DiscordId,
                player.GameId,
                player.DiscordUsername,
                player.PlayerType,
                player.AccountGameplayMode
            );
            newBridge.IsWebPlayer = player.IsWebPlayer;
            newBridge.PreferWeb = player.PreferWeb;
            newBridge.TeamId = player.TeamId;
            newBridge.Predict = player.Predict;
            newBridge.DiscordStatus = player.DiscordStatus;
            newBridge.Status.IsDraftPickConfirmed = true;

            if (account != null)
            {
                lock (account)
                {
                    if (selectedIndex > 0 && account.ZbsPoints < 5)
                        return Task.FromResult((false, "Not enough ZBS points (need 5)"));

                    newBridge.CharacterMasteryPoints =
                        account.CharacterMastery.GetValueOrDefault(selected.Name, 0);
                    DoomGuy.InitializeForGame(newBridge, account);

                    var previousCharacter = account.CharacterPlayedLastTime;
                    account.CharacterPlayedLastTime = selected.Name;

                    if (selectedIndex > 0)
                    {
                        var previousBalance = account.ZbsPoints;
                        account.ZbsPoints -= 5;
                        if (!_userAccounts.SaveAccount(account))
                        {
                            account.ZbsPoints = previousBalance;
                            account.CharacterPlayedLastTime = previousCharacter;
                            return Task.FromResult((false,
                                "Could not save your draft purchase. No ZBS was spent; please try again."));
                        }
                    }

                    DiscoverStoreCharacter(account, selected.Name);

                }
            }

            game.PlayersList[idx] = newBridge;
            return Task.FromResult((true, (string)null));
        }
    }

    public (bool success, string error) DepthsCallChoice(
        ulong gameId, ulong discordId, bool agree)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        lock (game)
        {
            return Cthulhu.SubmitDepthsAnswer(game, player, agree)
                ? (true, null)
                : (false, "No pending depths call choice");
        }
    }

    // ── Find helpers ──────────────────────────────────────────────────

    private GameClass FindGame(ulong gameId)
    {
        return _global.GamesList.Find(g => g.GameId == gameId);
    }

    private (GameClass game, GamePlayerBridgeClass player) FindGameAndPlayer(
        ulong gameId, ulong discordId, bool allowPausedTransition = false)
    {
        var game = FindGame(gameId);
        if (game?.IsRoundTransitionPaused == true && !allowPausedTransition)
            return (game, null);
        var player = game?.PlayersList.Find(p => p.DiscordId == discordId);
        return (game, player);
    }

    // ── Actions ───────────────────────────────────────────────────────

    public async Task<(bool success, string error)> Attack(ulong gameId, ulong discordId, int targetPlace)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (Cthulhu.MustChooseAdept(game, player))
            return (false, "Сначала выбери адепта");
        // Pickle Rick is "ready" (IsReady=true) during the pickle turn, so CanAct is false — but he
        // is still allowed to fire a charged Portal Gun, which auto-confirms his pickle skip.
        var picklePortalReady = player.Passives.RickPickle.PickleTurnsRemaining > 0
            && player.Passives.RickPortalGun.Invented && player.Passives.RickPortalGun.Charges > 0;
        if (!CanAct(player) && !picklePortalReady) return (false, "Cannot act right now");
        var (lvlBlocked, lvlError) = LevelUpGate(player);
        if (lvlBlocked) return (false, lvlError);

        // Use the existing HandleAttack with botChoice parameter
        // We temporarily flag the player so the method reads botChoice instead of button data
        var wasAutoMove = player.Status.IsAutoMove;
        player.Status.IsAutoMove = true;
        var result = await _gameReaction.HandleAttack(player, null, targetPlace);
        if (!result)
        {
            player.Status.IsAutoMove = wasAutoMove;
            var specificError = player.WebMessages.LastOrDefault() ?? "Attack rejected";
            // Remove so it doesn't also arrive via directMessages in the next state broadcast
            if (player.WebMessages.Count > 0)
                player.WebMessages.RemoveAt(player.WebMessages.Count - 1);
            return (false, specificError);
        }

        player.Status.IsAutoMove = false;
        return (true, null);
    }

    public Task<(bool success, string error)> Block(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (Cthulhu.MustChooseAdept(game, player))
            return Task.FromResult((false, "Сначала выбери адепта"));
        var madaraError = MadaraActionError(game, player);
        if (madaraError != null) return Task.FromResult((false, madaraError));
        if (!CanAct(player)) return Task.FromResult((false, "Cannot act right now"));
        var (lvlBlocked, lvlError) = LevelUpGate(player);
        if (lvlBlocked) return Task.FromResult((false, lvlError));

        if (GordonFreeman.Is(player))
            return Task.FromResult(GordonFreeman.AnnounceHalfLife3(player, game)
                ? (true, (string)null)
                : (false, "Halflife 3 cannot be announced right now"));

        if (!Naruto.CanChooseBlock(player))
            return Task.FromResult((false, "Naruto clones cannot block"));

        // Check Sparta passive (cannot block)
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Спарта"))
            return Task.FromResult((false, "Спартанцы не капитулируют!!"));

        // Aggress passive (cannot block)
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Aggress"))
            return Task.FromResult((false, "I. WONT. STOP."));

        // Dopa Макро — block counts as one of two actions
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Макро"))
        {
            if (player.Status.WhoToAttackThisTurn.Count == 0)
            {
                // Block first — register self as first target, wait for attack
                player.Status.WhoToAttackThisTurn.Add(player.GetPlayerId());
                var macroBlockText = "Макро: Блок зарегистрирован. Выберите цель для нападения.\n";
                player.Status.AddInGamePersonalLogs(macroBlockText);
                player.Status.ChangeMindWhat = macroBlockText;
                return Task.FromResult((true, (string)null));
            }
            else if (player.Status.WhoToAttackThisTurn.Count == 1)
            {
                // Attack was first — block is second action, mark self as second target
                player.Status.WhoToAttackThisTurn.Add(player.GetPlayerId());
                player.Status.IsReady = true;
                var macroBlockText2 = "Макро: Блок зарегистрирован как второе действие.\n";
                player.Status.AddInGamePersonalLogs(macroBlockText2);
                player.Status.ChangeMindWhat = macroBlockText2;
                return Task.FromResult((true, (string)null));
            }
        }

        player.Status.IsBlock = true;
        player.Status.IsReady = true;
        var text = "Вы поставили блок\n";
        player.Status.AddInGamePersonalLogs(text);
        player.Status.ChangeMindWhat = text;

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> AnnounceHalfLife3(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        var (levelBlocked, levelError) = LevelUpGate(player);
        if (levelBlocked) return Task.FromResult((false, levelError));
        return Task.FromResult(GordonFreeman.AnnounceHalfLife3(player, game)
            ? (true, (string)null)
            : (false, "Halflife 3 cannot be announced right now"));
    }

    public Task<(bool success, string error)> WakeGordon(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        return Task.FromResult(GordonFreeman.Wake(player, game)
            ? (true, (string)null)
            : (false, "Gordon cannot wake right now"));
    }

    public Task<(bool success, string error)> ResolveHalfLife3Decision(
        ulong gameId, ulong discordId, int serial, string choice)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId, allowPausedTransition: true);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (choice is not "freeze" and not "postpone" and not "release")
            return Task.FromResult((false, "Invalid Halflife 3 decision"));
        return Task.FromResult(GordonFreeman.ResolveHalfLifeDecision(player, game, serial, choice)
            ? (true, (string)null)
            : (false, "Halflife 3 decision is stale or unavailable"));
    }

    public Task<(bool success, string error)> AutoMove(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (Cthulhu.MustChooseAdept(game, player))
            return Task.FromResult((false, "Сначала выбери адепта"));
        var madaraError = MadaraActionError(game, player);
        if (madaraError != null) return Task.FromResult((false, madaraError));
        var (lvlBlocked, lvlError) = LevelUpGate(player);
        if (lvlBlocked) return Task.FromResult((false, lvlError));

        player.Status.AutoMoveTimes++;
        player.Passives.AchievementTracker.ExplicitAutoMoveRounds.Add(game.RoundNo);
        var text = "Вы использовали Авто Ход\n";
        player.Status.AddInGamePersonalLogs(text);
        player.Status.ChangeMindWhat = text;
        player.Status.IsAutoMove = true;
        player.Status.IsReady = true;
        game.Phrases.AutoMove.SendLogSeparateWeb(player, delete:true, isRandomOrder:false, isEvent:false);
        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> ChangeMind(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (Cthulhu.MustChooseAdept(game, player))
            return Task.FromResult((false, "Сначала выбери адепта"));
        var madaraError = MadaraActionError(game, player);
        if (madaraError != null) return Task.FromResult((false, madaraError));
        if (player.Status.IsSkip || !player.Status.IsReady)
            return Task.FromResult((false, "Cannot change mind right now"));

        player.Status.IsAbleToChangeMind = false;
        player.Status.IsAutoMove = false;
        player.Status.IsReady = false;
        player.Status.IsBlock = false;
        player.Status.WhoToAttackThisTurn = new List<Guid>();
        Cthulhu.ClearPendingNechtoAttack(game, player);

        if (player.Status.ChangeMindWhat.Contains("Вы использовали Авто Ход"))
        {
            player.Status.AutoMoveTimes--;
            player.Passives.AchievementTracker.ExplicitAutoMoveRounds.Remove(game.RoundNo);
        }

        var newLogs = player.Status.GetInGamePersonalLogs()
            .Replace(player.Status.ChangeMindWhat, $"~~{player.Status.ChangeMindWhat.Replace("\n", "~~\n")}");
        player.Status.InGamePersonalLogsAll = player.Status.InGamePersonalLogsAll
            .Replace(player.Status.ChangeMindWhat, $"~~{player.Status.ChangeMindWhat.Replace("\n", "~~\n")}");
        player.Status.SetInGamePersonalLogs(newLogs);

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> ConfirmSkip(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (Cthulhu.MustChooseAdept(game, player))
            return Task.FromResult((false, "Сначала выбери адепта"));
        var madaraError = MadaraActionError(game, player);
        if (madaraError != null) return Task.FromResult((false, madaraError));
        var (lvlBlocked, lvlError) = LevelUpGate(player);
        if (lvlBlocked) return Task.FromResult((false, lvlError));

        player.Status.ConfirmedSkip = true;
        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> ConfirmPredict(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));

        player.Status.ConfirmedPredict = true;
        return Task.FromResult((true, (string)null));
    }

    public async Task<(bool success, string error)> LevelUp(ulong gameId, ulong discordId, int statIndex)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (Madara.IsMadara(player)) return (false, "У Мадары нет прокачки");
        if (statIndex < 1 || statIndex > 4) return (false, "Invalid stat index (1-4)");
        if (ScamRat.Is(player))
            return ScamRat.TryPurchaseStat(player, game, statIndex)
                ? (true, null)
                : (false, "Stat is maxed or no Sharing is CARRYING points are available");
        if (player.Status.LvlUpPoints <= 0) return (false, "No level-up points available");

        // Use the existing HandleLvlUp with botChoice parameter
        var wasAutoMove = player.Status.IsAutoMove;
        var pointsBefore = player.Status.LvlUpPoints;
        player.Status.IsAutoMove = true;
        await _gameReaction.HandleLvlUp(player, null, statIndex);
        player.Status.IsAutoMove = wasAutoMove;

        if (player.Status.LvlUpPoints == pointsBefore)
            return (false, "Invalid level-up/module choice");

        return (true, null);
    }

    public async Task<(bool success, string error)> MoralToPoints(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (player.GameCharacter.DoomRollMode) return (false, "Moral is disabled by Let's Roll!");
        if (player.GameCharacter.GetMoral() < 5) return (false, "Not enough moral");

        await _gameReaction.HandleMoralForScore(player);
        return (true, null);
    }

    public async Task<(bool success, string error)> MoralToSkill(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (player.GameCharacter.DoomRollMode) return (false, "Moral is disabled by Let's Roll!");
        if (player.GameCharacter.GetMoral() < 1) return (false, "Not enough moral");

        await _gameReaction.HandleMoralForSkill(player);
        return (true, null);
    }

    public (bool success, string error) DemandContractReward(ulong gameId, ulong discordId, string demandType)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (player.GameCharacter.Name != "Геральт") return (false, "Not Geralt");
        if (player.Passives.IsDead) return (false, "Dead");

        var demand = player.Passives.GeraltContractDemand;

        if (demandType == "previous")
        {
            if (demand.DemandedThisPhase) return (false, "Already demanded this phase");
            if (demand.PrevContractsFought <= 0) return (false, "No contracts fought");

            var invoice = demand.CalculateInvoice();
            demand.DemandedThisPhase = true;
            demand.TotalDemandsMade++;

            if (invoice.PredictedCoins > 0)
            {
                demand.TotalSuccessfulDemands++;
                demand.QuestCompletedThisRound = true;

                if (invoice.PredictedCoins >= 2)
                {
                    player.Status.AddRegularPoints(invoice.PredictedCoins - 1, "Чеканная монета");
                    player.Status.AddRegularPoints(1, "Благодарность");
                    player.Status.AddInGamePersonalLogs($"Чеканная монета: +{invoice.PredictedCoins - 1} очк. (счёт: {invoice.Total})\n");
                    player.Status.AddInGamePersonalLogs("Благодарность: Еще одна монета за ваш подвиг! Всем селом скинулись.\n");
                }
                else
                {
                    player.Status.AddRegularPoints(invoice.PredictedCoins, "Чеканная монета");
                    player.Status.AddInGamePersonalLogs($"Чеканная монета: +{invoice.PredictedCoins} очк. (счёт: {invoice.Total})\n");
                }
            }
            if (invoice.PredictedDispleasure > 0)
            {
                demand.Displeasure += invoice.PredictedDispleasure;
                player.Status.AddInGamePersonalLogs($"Чеканная монета: Недовольство +{invoice.PredictedDispleasure} (счёт: {invoice.Total})\n");
            }
            if (invoice.PredictedCoins == 0 && invoice.PredictedDispleasure == 0)
            {
                player.Status.AddInGamePersonalLogs($"Чеканная монета: Ничего не получено (счёт: {invoice.Total})\n");
            }

            // "Barely survived" additional effects
            if (invoice.AdditionalCoins > 0)
            {
                player.Status.AddRegularPoints(invoice.AdditionalCoins, "Выжил чудом");
                player.Status.AddInGamePersonalLogs($"Выжил чудом: +{invoice.AdditionalCoins} очко\n");
            }
            if (invoice.AdditionalDispleasure > 0)
            {
                demand.Displeasure += invoice.AdditionalDispleasure;
                player.Status.AddInGamePersonalLogs($"Выжил чудом: Недовольство +{invoice.AdditionalDispleasure}\n");
            }
        }
        else if (demandType == "next")
        {
            if (demand.Displeasure >= 5) return (false, "Too much displeasure");
            if (demand.DemandedForNext) return (false, "Already demanded for next");
            if (demand.AdvancePending) return (false, "Advance already pending");

            demand.DemandedForNext = true;
            demand.TotalDemandsMade++;
            demand.AdvancePending = true;
            player.Status.AddInGamePersonalLogs("Чеканная монета: Аванс за следующий заказ! +2 очка (в следующем раунде)\n");
        }
        else
        {
            return (false, "Invalid demand type");
        }

        // Death by pitchforks
        if (demand.Displeasure >= 11)
        {
            player.Passives.IsDead = true;
            player.Passives.DeathSource = "Pitchforks";
            player.Status.AddBonusPointsIgnoringFloor(-500, "Вилы разъяренной толпы");
            game.AddGlobalLogs($"Жители деревни подняли {player.DiscordUsername} на вилы за жадность! Ведьмак мёртв.");
            player.Status.AddInGamePersonalLogs("Чеканная монета: Толпа с вилами! Вы мертвы. -500 очков.\n");
        }

        return (true, null);
    }

    public Task<(bool success, string error)> Predict(ulong gameId, ulong discordId, Guid targetPlayerId, string characterName)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (Madara.IsMadara(player))
            return Task.FromResult((false, "У Мадары нет предположений"));
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Булькает"))
            return Task.FromResult((false, "Бууууууль"));
        if (player.GameCharacter.DoomRollMode)
            return Task.FromResult((false, "Predictions are disabled by Let's Roll!"));
        if (!_charactersPull.GetVisibleCharacters().Any(character => character.Name == characterName))
            return Task.FromResult((false, "Character is not available for predictions"));
        if (!HasUnlockedCharacter(discordId, characterName)) return Task.FromResult((false, "Character is not unlocked for predictions"));

        var target = game.PlayersList.Find(p => p.GetPlayerId() == targetPlayerId);
        if (target == null) return Task.FromResult((false, "Target player not found"));
        if (Sakura.Is(target))
            return Task.FromResult((false, "Target is not available for predictions"));

        var existing = player.Predict.Find(p => p.PlayerId == targetPlayerId);
        if (existing == null)
            player.Predict.Add(new PredictClass(characterName, targetPlayerId));
        else
            existing.CharacterName = characterName;

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> AramReroll(ulong gameId, ulong discordId, int slot)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!game.IsAramPickPhase) return Task.FromResult((false, "Not in ARAM pick phase"));

        if (slot >= 1 && slot <= 4)
        {
            _gameReaction.HandlePassiveRoll(player, slot, game);
        }
        else if (slot == 5)
        {
            _gameReaction.HandleBasicStatRoll(player);
        }
        else
        {
            return Task.FromResult((false, "Invalid slot (1-5)"));
        }

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> AramConfirm(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!game.IsAramPickPhase) return Task.FromResult((false, "Not in ARAM pick phase"));

        player.Status.IsAramRollConfirmed = true;
        return Task.FromResult((true, (string)null));
    }

    // ── Kira Actions ─────────────────────────────────────────────────

    public Task<(bool success, string error)> DeathNoteWrite(ulong gameId, ulong discordId, Guid targetPlayerId, string characterName)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!player.GameCharacter.Passive.Any(p => p.PassiveName == "Тетрадь смерти"))
            return Task.FromResult((false, "You don't have the Death Note"));
        var submittedName = characterName?.Trim() ?? "";
        if (!_charactersPull.GetVisibleCharacters().Any(character => character.Name == submittedName))
            return Task.FromResult((false, "Invalid character name"));
        if (!HasUnlockedCharacter(discordId, submittedName)) return Task.FromResult((false, "Character is not unlocked"));

        var dn = player.Passives.KiraDeathNote;
        if (dn.CurrentRoundTarget != Guid.Empty)
            return Task.FromResult((false, "Already written this round"));

        var target = game.PlayersList.Find(p => p.GetPlayerId() == targetPlayerId);
        if (target == null) return Task.FromResult((false, "Target not found"));
        if (target.GetPlayerId() == player.GetPlayerId())
            return Task.FromResult((false, "Cannot write your own name"));
        if (Sakura.Is(target))
            return Task.FromResult((false, "Target is not available to the Death Note"));
        if (target.Passives.IsDead)
            return Task.FromResult((false, "Target is already dead"));
        if (dn.FailedTargets.Contains(targetPlayerId))
            return Task.FromResult((false, "Already failed for this target"));

        dn.CurrentRoundTarget = targetPlayerId;
        dn.CurrentRoundName = submittedName;
        player.Status.AddInGamePersonalLogs($"Тетрадь смерти: Ты записал имя **{dn.CurrentRoundName}** для {target.DiscordUsername}\n");

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> ShinigamiEyes(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!player.GameCharacter.Passive.Any(p => p.PassiveName == "Глаза бога смерти"))
            return Task.FromResult((false, "You don't have Shinigami Eyes"));
        if (player.GameCharacter.GetMoral() < 25)
            return Task.FromResult((false, "Not enough moral (need 25)"));
        if (player.Passives.KiraShinigamiEyes.EyesActiveForNextAttack)
            return Task.FromResult((false, "Already active"));

        player.GameCharacter.AddMoral(-25, "Глаза бога смерти");
        player.Passives.KiraShinigamiEyes.EyesActiveForNextAttack = true;
        player.Status.AddInGamePersonalLogs("Глаза бога смерти: Активированы! Следующая атака раскроет имя врага.\n");

        return Task.FromResult((true, (string)null));
    }

    // ── Darksci / Young Gleb ──────────────────────────────────────────

    public Task<(bool success, string error)> DarksciChoice(ulong gameId, ulong discordId, bool isStable)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!player.GameCharacter.Passive.Any(p => p.PassiveName == "Мне (не)везет"))
            return Task.FromResult((false, "You don't have this passive"));

        var darksciType = player.Passives.DarksciTypeList;
        if (darksciType.Triggered)
            return Task.FromResult((false, "Already chosen"));

        darksciType.Triggered = true;
        darksciType.IsStableType = isStable;

        if (isStable)
        {
            player.GameCharacter.AddExtraSkill(20, "Не повезло");
            player.GameCharacter.AddMoral(2, "Не повезло");
            player.Status.AddInGamePersonalLogs("Ну, сегодня мне не повезёт...\n");
        }
        else
        {
            player.Status.AddInGamePersonalLogs("Я чувствую удачу!\n");
        }

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> YoungGleb(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!player.GameCharacter.Passive.Any(p => p.PassiveName == "Yong Gleb"))
            return Task.FromResult((false, "You don't have this passive"));
        if (player.GameCharacter.Name == "Молодой Глеб")
            return Task.FromResult((false, "Already transformed"));

        var character = _charactersPull.GetAllCharactersNoFilter().First(x => x.Name == "Молодой Глеб");
        player.GameCharacter.Passive = new List<Passive>();
        player.GameCharacter.Passive = character.Passive;
        player.GameCharacter.Avatar = character.Avatar;
        player.GameCharacter.AvatarCurrent = character.Avatar;
        player.GameCharacter.Description = character.Description;
        player.GameCharacter.Tier = character.Tier;
        player.GameCharacter.SetIntelligence(character.GetIntelligence(), "yong-gleb", false);
        player.GameCharacter.SetStrength(character.GetStrength(), "yong-gleb", false);
        player.GameCharacter.SetSpeed(character.GetSpeed(), "yong-gleb", false);
        player.GameCharacter.SetPsyche(character.GetPsyche(), "yong-gleb", false);

        // Clear sleep state (Спящее хуйло)
        player.Status.IsSkip = false;
        player.Status.ConfirmedSkip = true;
        player.Status.IsReady = false;
        player.Status.WhoToAttackThisTurn = new List<Guid>();
        player.GameCharacter.AddExtraSkill(30, "Спящее хуйло", false);
        player.Status.ClearInGamePersonalLogs();

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> DoomRoll(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (game.RoundNo != 1) return Task.FromResult((false, "Let's Roll! is only available on round 1"));
        return Task.FromResult(DoomGuy.ActivateRollMode(player)
            ? (true, (string)null)
            : (false, "Let's Roll! is not available"));
    }

    public Task<(bool success, string error)> DoomChainsaw(ulong gameId, ulong discordId, string passiveName)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        var result = DoomGuy.CopyChainsawPassive(player, passiveName);
        return Task.FromResult((result.Success, result.Error));
    }

    // ── Leave / Finish ────────────────────────────────────────────────

    /// <summary>
    /// Player finishes / leaves the game: replaced by a bot.
    /// </summary>
    public (bool success, string error) FinishGame(ulong gameId, ulong discordId)
    {
        var game = _global.GamesList.Find(x => x.GameId == gameId);
        if (game == null) return (false, "Game not found.");
        var player = game.PlayersList.Find(x => x.DiscordId == discordId);
        if (player == null) return (false, "Player not in game.");

        _helper.EndGame(discordId);
        return (true, null);
    }

    // ── Salldorum Actions ──────────────────────────────────────────────

    public (bool success, string error) RewriteHistory(ulong gameId, ulong discordId, int roundNumber)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        return Salldorum.RewriteHistory(player, game, roundNumber);
    }

    // ── Test Game (Admin) ─────────────────────────────────────────────

    /// <summary>
    /// Returns the public character list, optionally extended for the admin test picker.
    /// </summary>
    public List<object> GetCharacterList(bool includePrivateTestCharacters = false)
    {
        var characters = includePrivateTestCharacters
            ? _charactersPull.GetAdminSelectableCharacters()
            : _charactersPull.GetVisibleCharacters();
        return characters
            .Select(c => (object)new { name = c.Name, avatar = c.Avatar, tier = c.Tier })
            .ToList();
    }

    /// <summary>
    /// Creates a test game with the admin playing a specific character.
    /// </summary>
    public async Task<(ulong gameId, string error)> CreateTestGame(ulong creatorId, string creatorUsername, string characterName)
    {
        if (AdminLobbies?.IsReserved(creatorId) == true)
            return (0, "Вы были избраны богом.");

        // Validate character exists
        var allCharacters = _charactersPull.GetAdminSelectableCharacters();
        var selectedChar = allCharacters.Find(c => c.Name == characterName);
        if (selectedChar == null)
            return (0, "Character not found");

        // Create a normal game first
        var (gameId, error) = await CreateGame(
            creatorId,
            creatorUsername,
            recordNaturalUnknownBugRoll: false);
        if (error != null)
            return (0, error);

        var game = FindGame(gameId);
        if (game == null)
            return (0, "Game creation failed");

        var player = game.PlayersList.Find(p => p.DiscordId == creatorId);
        if (player == null)
            return (0, "Player not found in game");

        // If the selected character or its mutually exclusive counterpart is assigned to a bot,
        // give that bot the creator's original character.
        var conflictingBot = game.PlayersList.Find(p => p != player
                                                        && (p.GameCharacter.Name == characterName
                                                            || StartGameLogic.AreMutuallyExclusiveCharacters(
                                                                p.GameCharacter.Name, characterName)));
        if (conflictingBot != null)
        {
            var originalChar = player.GameCharacter;
            var botNewBridge = new GamePlayerBridgeClass(
                originalChar, new InGameStatus(),
                conflictingBot.DiscordId, conflictingBot.GameId,
                conflictingBot.DiscordUsername, conflictingBot.PlayerType);
            botNewBridge.TeamId = conflictingBot.TeamId;
            botNewBridge.DiscordStatus = conflictingBot.DiscordStatus;
            botNewBridge.Status.IsDraftPickConfirmed = true;
            var conflictAccount = _userAccounts.GetAccount(conflictingBot.DiscordId);
            if (conflictAccount != null) DoomGuy.InitializeForGame(botNewBridge, conflictAccount);
            var botIdx = game.PlayersList.IndexOf(conflictingBot);
            if (botIdx >= 0) game.PlayersList[botIdx] = botNewBridge;
        }

        // Replace the player's character with the selected one (follows DraftSelect pattern)
        var newBridge = new GamePlayerBridgeClass(
            selectedChar,
            new InGameStatus(),
            player.DiscordId,
            player.GameId,
            player.DiscordUsername,
            player.PlayerType,
            player.AccountGameplayMode
        );
        newBridge.IsWebPlayer = player.IsWebPlayer;
        newBridge.PreferWeb = player.PreferWeb;
        newBridge.TeamId = player.TeamId;
        newBridge.Predict = player.Predict;
        newBridge.DiscordStatus = player.DiscordStatus;
        newBridge.Status.IsDraftPickConfirmed = true;

        var idx = game.PlayersList.IndexOf(player);
        if (idx >= 0)
            game.PlayersList[idx] = newBridge;

        // Update account's last played character and mastery
        var account = _userAccounts.GetAccount(creatorId);
        if (account != null)
        {
            newBridge.CharacterMasteryPoints = account.CharacterMastery.GetValueOrDefault(selectedChar.Name, 0);
            DoomGuy.InitializeForGame(newBridge, account);
            account.CharacterPlayedLastTime = selectedChar.Name;
        }

        // Auto-confirm draft pick if in draft phase
        if (game.IsDraftPickPhase)
        {
            // Remove old draft options for this player
            var oldPlayerId = player.GetPlayerId();
            game.DraftOptions.Remove(oldPlayerId);
        }

        var loggedCharacter = UnknownBug.Is(selectedChar) ? "(private)" : selectedChar.Name;
        Console.WriteLine($"[WebAPI] Test game {gameId} created by {creatorUsername} ({creatorId}) with character {loggedCharacter}");
        return (gameId, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static bool CanAct(GamePlayerBridgeClass player)
    {
        return !player.Status.IsReady && !player.Status.IsSkip;
    }

    private static string MadaraActionError(GameClass game, GamePlayerBridgeClass player)
    {
        if (!Madara.IsMadara(player)) return null;
        if (player.Passives.Madara.Sealed) return "Игрок запечатан";
        return game.RoundNo == 8
            ? "Мадара ждёт, кто осмелится бросить ему вызов."
            : null;
    }

    // A pending level-up must be spent before a player can end their turn. Mirrors Discord,
    // where a granted level-up flips the message to the level-up page (MoveListPage 3) and hides
    // the fight controls until the points are spent (GameUpdateMess.cs; GameReactions.cs:1221).
    // Applies to every character; Main Ирелия (a forced-nerf level-up) keeps her own flavored refusal. (M15)
    private static (bool blocked, string error) LevelUpGate(GamePlayerBridgeClass player)
    {
        if (player.Status.LvlUpPoints <= 0) return (false, null);
        var isIrelia = player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия");
        return (true, isIrelia ? "Риоты не прощают, нерфа не избежать" : "Остались очки прокачки — потрать их!");
    }

    // ── Character store ──────────────────────────────────────────────

    public const int StoreBasePrice = 10;
    public const double StoreMinMultiplier = 0.5;
    public const double StoreMaxMultiplier = 2.0;

    private static void DiscoverStoreCharacter(DiscordAccountClass account, string characterName)
    {
        if (UnknownBug.Is(characterName)) return;

        lock (account)
        {
            account.SeenCharacters ??= new List<string>();
            if (!account.SeenCharacters.Contains(characterName))
                account.SeenCharacters.Add(characterName);

            account.CharacterChance ??= new List<DiscordAccountClass.CharacterChances>();
            if (account.CharacterChance.All(chance => chance.CharacterName != characterName))
                account.CharacterChance.Add(new DiscordAccountClass.CharacterChances(characterName));
        }
    }

    public (StoreStateDto State, string Error) GetStoreState(ulong discordId)
    {
        var account = _userAccounts.GetAccount(discordId);
        if (account == null) return (null, "Account not found.");

        lock (account)
            return (BuildStoreState(account), null);
    }

    public (StoreStateDto State, string Error) AdjustStoreCharacter(
        ulong discordId,
        string characterName,
        int percentagePoints)
    {
        if (percentagePoints is not (-10 or -1 or 1 or 10))
            return (null, "Store adjustments must be -10, -1, 1, or 10 percentage points.");

        var account = _userAccounts.GetAccount(discordId);
        if (account == null) return (null, "Account not found.");

        lock (account)
        {
            var characterExists = !string.IsNullOrWhiteSpace(characterName)
                                  && _charactersPull.GetRollableCharacters().Any(character =>
                                      !UnknownBug.Is(character) && character.Name == characterName);
            if (!characterExists)
                return (null, "Character is not available in the store.");

            if (account.SeenCharacters == null
                || !account.SeenCharacters.Contains(characterName))
                return (null, "Play this character before changing their roll weight.");

            account.CharacterChance ??= new List<DiscordAccountClass.CharacterChances>();
            var chance = account.CharacterChance.Find(entry => entry.CharacterName == characterName);
            var createdChance = chance == null;
            if (createdChance)
            {
                chance = new DiscordAccountClass.CharacterChances(characterName);
                account.CharacterChance.Add(chance);
            }

            var targetMultiplier = Math.Round(chance.Multiplier + percentagePoints / 100d, 2);
            if (targetMultiplier < StoreMinMultiplier
                || targetMultiplier > StoreMaxMultiplier)
            {
                if (createdChance) account.CharacterChance.Remove(chance);
                return (null,
                    $"Roll weight must stay between {StoreMinMultiplier:0.00}x and {StoreMaxMultiplier:0.00}x.");
            }

            var steps = Math.Abs(percentagePoints);
            var cost = CalculateStoreCost(chance.Changes, steps);
            if (account.ZbsPoints < cost)
            {
                if (createdChance) account.CharacterChance.Remove(chance);
                return (null, $"Not enough ZBS points. This adjustment costs {cost} ZBS.");
            }

            var previousBalance = account.ZbsPoints;
            var previousMultiplier = chance.Multiplier;
            var previousChanges = chance.Changes;
            chance.Multiplier = targetMultiplier;
            chance.Changes += steps;
            account.ZbsPoints -= cost;

            if (!_userAccounts.SaveAccount(account))
            {
                account.ZbsPoints = previousBalance;
                chance.Multiplier = previousMultiplier;
                chance.Changes = previousChanges;
                if (createdChance) account.CharacterChance.Remove(chance);
                return (null, "The purchase could not be saved. No ZBS was spent; please try again.");
            }

            return (BuildStoreState(account), null);
        }
    }

    public (StoreStateDto State, string Error) ResetStoreCharacter(ulong discordId, string characterName)
    {
        var account = _userAccounts.GetAccount(discordId);
        if (account == null) return (null, "Account not found.");

        lock (account)
        {
            if (UnknownBug.Is(characterName))
                return (null, "Character is not available in your store.");

            if (string.IsNullOrWhiteSpace(characterName)
                || account.SeenCharacters == null
                || !account.SeenCharacters.Contains(characterName))
                return (null, "Character is not available in your store.");

            var chance = account.CharacterChance?.Find(entry => entry.CharacterName == characterName);
            if (chance == null || chance.Changes <= 0)
                return (BuildStoreState(account), null);

            var previousBalance = account.ZbsPoints;
            var previousMultiplier = chance.Multiplier;
            var previousChanges = chance.Changes;
            account.ZbsPoints += CalculateStoreRefund(chance.Changes);
            chance.Multiplier = 1.0;
            chance.Changes = 0;

            if (!_userAccounts.SaveAccount(account))
            {
                account.ZbsPoints = previousBalance;
                chance.Multiplier = previousMultiplier;
                chance.Changes = previousChanges;
                return (null, "The refund could not be saved. Nothing changed; please try again.");
            }

            return (BuildStoreState(account), null);
        }
    }

    public (StoreStateDto State, string Error) ResetStoreAllCharacters(ulong discordId)
    {
        var account = _userAccounts.GetAccount(discordId);
        if (account == null) return (null, "Account not found.");

        lock (account)
        {
            var changed = account.CharacterChance?
                .Where(chance => chance.Changes > 0 && !UnknownBug.Is(chance.CharacterName))
                .ToList() ?? new List<DiscordAccountClass.CharacterChances>();
            if (changed.Count == 0) return (BuildStoreState(account), null);

            var previousBalance = account.ZbsPoints;
            var previousChances = changed
                .Select(chance => (Chance: chance, chance.Multiplier, chance.Changes))
                .ToList();

            account.ZbsPoints += changed.Sum(chance => CalculateStoreRefund(chance.Changes));
            foreach (var chance in changed)
            {
                chance.Multiplier = 1.0;
                chance.Changes = 0;
            }

            if (!_userAccounts.SaveAccount(account))
            {
                account.ZbsPoints = previousBalance;
                foreach (var previous in previousChances)
                {
                    previous.Chance.Multiplier = previous.Multiplier;
                    previous.Chance.Changes = previous.Changes;
                }
                return (null, "The refunds could not be saved. Nothing changed; please try again.");
            }

            return (BuildStoreState(account), null);
        }
    }

    private StoreStateDto BuildStoreState(DiscordAccountClass account)
    {
        var rollableCharacters = _charactersPull.GetRollableCharacters()
            .Where(character => !UnknownBug.Is(character.Name))
            .ToDictionary(character => character.Name, StringComparer.Ordinal);
        var seenNames = (account.SeenCharacters ?? new List<string>())
            .Where(name => !UnknownBug.Is(name))
            .Distinct(StringComparer.Ordinal);
        var chances = account.CharacterChance ?? new List<DiscordAccountClass.CharacterChances>();
        var storeChances = chances.Where(chance => !UnknownBug.Is(chance.CharacterName)).ToList();
        var state = new StoreStateDto
        {
            ZbsPoints = account.ZbsPoints,
            BasePrice = StoreBasePrice,
            MinMultiplier = StoreMinMultiplier,
            MaxMultiplier = StoreMaxMultiplier,
            TotalInvestedZbs = storeChances.Sum(chance => CalculateStoreRefund(chance.Changes)),
        };

        foreach (var name in seenNames)
        {
            if (!rollableCharacters.TryGetValue(name, out var character)) continue;
            var chance = chances.Find(entry => entry.CharacterName == name);
            var changes = Math.Max(0, chance?.Changes ?? 0);
            state.Characters.Add(new StoreCharacterDto
            {
                Name = name,
                Avatar = character.Avatar,
                Tier = character.Tier,
                Multiplier = chance?.GetEffectiveMultiplier() ?? 1.0,
                Changes = changes,
                CostOne = CalculateStoreCost(changes, 1),
                CostTen = CalculateStoreCost(changes, 10),
                RefundZbs = CalculateStoreRefund(changes),
            });
        }

        return state;
    }

    private static int CalculateStoreCost(int existingChanges, int steps)
    {
        var changes = Math.Max(0, existingChanges);
        return Enumerable.Range(0, Math.Max(0, steps))
            .Sum(step => StoreBasePrice + changes + step);
    }

    private static int CalculateStoreRefund(int changes)
    {
        var normalizedChanges = Math.Max(0, changes);
        return normalizedChanges * StoreBasePrice
               + normalizedChanges * (normalizedChanges - 1) / 2;
    }

    private bool HasUnlockedCharacter(ulong discordId, string characterName)
    {
        var account = _userAccounts.GetAccount(discordId);
        if (account == null) return false;

        lock (account)
        {
            return account.SeenCharacters?.Contains(characterName, StringComparer.Ordinal) == true;
        }
    }
}
