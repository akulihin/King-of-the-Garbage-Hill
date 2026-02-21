using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

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
    private readonly Global _global;
    private readonly GameReaction _gameReaction;
    private readonly GameUpdateMess _gameUpdateMess;
    private readonly HelperFunctions _helper;
    private readonly CharactersPull _charactersPull;
    private readonly CharacterPassives _characterPassives;
    private readonly StartGameLogic _startGameLogic;
    private readonly UserAccounts _userAccounts;

    public WebGameService(Global global, GameReaction gameReaction, GameUpdateMess gameUpdateMess, HelperFunctions helper, CharactersPull charactersPull, CharacterPassives characterPassives, StartGameLogic startGameLogic, UserAccounts userAccounts)
    {
        _global = global;
        _gameReaction = gameReaction;
        _gameUpdateMess = gameUpdateMess;
        _helper = helper;
        _charactersPull = charactersPull;
        _characterPassives = characterPassives;
        _startGameLogic = startGameLogic;
        _userAccounts = userAccounts;
    }

    // ── Queries ───────────────────────────────────────────────────────

    public LobbyStateDto GetLobbyState()
    {
        var dto = new LobbyStateDto
        {
            ActiveGames = _global.GamesList.Count,
        };

        foreach (var game in _global.GamesList)
        {
            var botCount = game.PlayersList.Count(p => p.IsBot());
            dto.Games.Add(new ActiveGameDto
            {
                GameId = game.GameId,
                RoundNo = game.RoundNo,
                PlayerCount = game.PlayersList.Count,
                HumanCount = game.PlayersList.Count(p => !p.IsBot()),
                GameMode = game.GameMode,
                IsFinished = game.IsFinished,
                BotCount = botCount,
                CanJoin = botCount > 0 && !game.IsFinished,
            });
        }

        return dto;
    }

    public GameStateDto GetGameState(ulong gameId, ulong discordId)
    {
        var game = FindGame(gameId);
        if (game == null) return null;

        var player = game.PlayersList.Find(p => p.DiscordId == discordId);
        var dto = GameStateMapper.ToDto(game, player);
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
        foreach (var playerDto in dto.Players)
        {
            var otherPlayer = game.PlayersList.Find(p => p.GetPlayerId() == playerDto.PlayerId);
            if (otherPlayer != null)
            {
                var rawAfter = gameUpdateMess.CustomLeaderBoardAfterPlayer(viewingPlayer, otherPlayer, game);
                playerDto.CustomLeaderboardText = ConvertDiscordToWeb(rawAfter);

                var rawBefore = gameUpdateMess.CustomLeaderBoardBeforeNumber(viewingPlayer, otherPlayer, game, playerDto.Status.Place);
                playerDto.CustomLeaderboardPrefix = ConvertDiscordToWeb(rawBefore);
            }
        }
    }

    // ── Web Game Creation / Joining ─────────────────────────────────

    /// <summary>
    /// Creates a new 6-player game from the web (1 creator + 5 bots).
    /// Mirrors the flow in General.cs StartGame but skips Discord DMs.
    /// </summary>
    public async Task<(ulong gameId, string error)> CreateGame(ulong creatorId, string creatorUsername)
    {
        var creatorAccount = _userAccounts.GetAccount(creatorId);
        if (creatorAccount == null)
            return (0, "Account not found");
        if (creatorAccount.IsPlaying)
            return (0, "Already in a game");

        // Roll characters for 6 bots (null = bot slot)
        var gameId = _global.GetNewtGamePlayingAndId();
        var players = new List<Discord.IUser> { null, null, null, null, null, null };
        var playersList = _startGameLogic.HandleCharacterRoll(players, gameId, mode: "bot");

        // Shuffle and sort
        playersList = playersList.OrderBy(_ => Guid.NewGuid()).ToList();
        playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        playersList = _characterPassives.HandleEventsBeforeFirstRound(playersList);

        // Assign leaderboard positions
        for (var i = 0; i < playersList.Count; i++)
            playersList[i].Status.SetPlaceAtLeaderBoard(i + 1);

        // Replace the first bot with the creator
        var botToReplace = playersList[0];
        var oldBotAccount = _userAccounts.GetAccount(botToReplace.DiscordId);
        if (oldBotAccount != null) oldBotAccount.IsPlaying = false;

        botToReplace.DiscordId = creatorId;
        botToReplace.DiscordUsername = creatorUsername;
        botToReplace.PlayerType = creatorAccount.PlayerType;
        botToReplace.IsWebPlayer = true;
        botToReplace.PreferWeb = true;
        creatorAccount.IsPlaying = true;

        // Create game
        var game = new GameClass(playersList, gameId, creatorId) { IsCheckIfReady = false };
        game.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));
        game.TimePassed.Start();
        _global.GamesList.Add(game);

        // Handle round #0 (passives + bot predictions)
        await _characterPassives.HandleNextRound(game);
        _characterPassives.HandleBotPredict(game);

        game.IsCheckIfReady = true;
        Console.WriteLine($"[WebAPI] Web game {gameId} created by {creatorUsername} ({creatorId})");
        return (gameId, null);
    }

    /// <summary>
    /// Joins an existing game by replacing a random bot with the web player.
    /// </summary>
    public (bool success, string error) JoinWebGame(ulong gameId, ulong playerId, string playerUsername)
    {
        var game = FindGame(gameId);
        if (game == null) return (false, "Game not found");
        if (game.IsFinished) return (false, "Game is finished");

        // If player is already in this game, just return success
        var existingPlayer = game.PlayersList.Find(p => p.DiscordId == playerId);
        if (existingPlayer != null) return (true, null);

        var playerAccount = _userAccounts.GetAccount(playerId);
        if (playerAccount == null) return (false, "Account not found");
        if (playerAccount.IsPlaying) return (false, "Already in a game");

        // Find a bot to replace
        var bot = game.PlayersList.Find(p => p.IsBot());
        if (bot == null) return (false, "No bot slots available");

        // Release the bot account
        var botAccount = _userAccounts.GetAccount(bot.DiscordId);
        if (botAccount != null) botAccount.IsPlaying = false;

        // Replace bot with the joining player
        bot.DiscordId = playerId;
        bot.DiscordUsername = playerUsername;
        bot.PlayerType = playerAccount.PlayerType;
        bot.IsWebPlayer = true;
        bot.PreferWeb = true;
        bot.DiscordStatus.SocketGameMessage = null;
        playerAccount.IsPlaying = true;

        Console.WriteLine($"[WebAPI] Player {playerUsername} ({playerId}) joined game {gameId}");
        return (true, null);
    }

    // ── Find helpers ──────────────────────────────────────────────────

    private GameClass FindGame(ulong gameId)
    {
        return _global.GamesList.Find(g => g.GameId == gameId);
    }

    private (GameClass game, GamePlayerBridgeClass player) FindGameAndPlayer(ulong gameId, ulong discordId)
    {
        var game = FindGame(gameId);
        var player = game?.PlayersList.Find(p => p.DiscordId == discordId);
        return (game, player);
    }

    // ── Actions ───────────────────────────────────────────────────────

    public async Task<(bool success, string error)> Attack(ulong gameId, ulong discordId, int targetPlace)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (!CanAct(player)) return (false, "Cannot act right now");

        // Use the existing HandleAttack with botChoice parameter
        // We temporarily flag the player so the method reads botChoice instead of button data
        var wasAutoMove = player.Status.IsAutoMove;
        player.Status.IsAutoMove = true;
        var result = await _gameReaction.HandleAttack(player, null, targetPlace);
        if (!result)
        {
            player.Status.IsAutoMove = wasAutoMove;
            return (false, "Attack rejected (invalid target or passive prevented it)");
        }

        player.Status.IsAutoMove = false;
        return (true, null);
    }

    public Task<(bool success, string error)> Block(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!CanAct(player)) return Task.FromResult((false, "Cannot act right now"));

        // Check Sparta passive (cannot block)
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Спарта"))
            return Task.FromResult((false, "Спартанцы не капитулируют!!"));

        player.Status.IsBlock = true;
        player.Status.IsReady = true;
        var text = "Вы поставили блок\n";
        player.Status.AddInGamePersonalLogs(text);
        player.Status.ChangeMindWhat = text;

        return Task.FromResult((true, (string)null));
    }

    public Task<(bool success, string error)> AutoMove(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));

        player.Status.AutoMoveTimes++;
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
        if (player.Status.IsSkip || !player.Status.IsReady)
            return Task.FromResult((false, "Cannot change mind right now"));

        player.Status.IsAbleToChangeMind = false;
        player.Status.IsAutoMove = false;
        player.Status.IsReady = false;
        player.Status.IsBlock = false;
        player.Status.WhoToAttackThisTurn = new List<Guid>();

        if (player.Status.ChangeMindWhat.Contains("Вы использовали Авто Ход"))
            player.Status.AutoMoveTimes--;

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
        if (player.Status.LvlUpPoints <= 0) return (false, "No level-up points available");
        if (statIndex < 1 || statIndex > 4) return (false, "Invalid stat index (1-4)");

        // Use the existing HandleLvlUp with botChoice parameter
        var wasAutoMove = player.Status.IsAutoMove;
        player.Status.IsAutoMove = true;
        await _gameReaction.HandleLvlUp(player, null, statIndex);
        player.Status.IsAutoMove = wasAutoMove;

        return (true, null);
    }

    public async Task<(bool success, string error)> MoralToPoints(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (player.GameCharacter.GetMoral() < 5) return (false, "Not enough moral");

        await _gameReaction.HandleMoralForScore(player);
        return (true, null);
    }

    public async Task<(bool success, string error)> MoralToSkill(ulong gameId, ulong discordId)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return (false, "Game not found");
        if (player == null) return (false, "Player not in this game");
        if (player.GameCharacter.GetMoral() < 1) return (false, "Not enough moral");

        await _gameReaction.HandleMoralForSkill(player);
        return (true, null);
    }

    public Task<(bool success, string error)> Predict(ulong gameId, ulong discordId, Guid targetPlayerId, string characterName)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));

        var target = game.PlayersList.Find(p => p.GetPlayerId() == targetPlayerId);
        if (target == null) return Task.FromResult((false, "Target player not found"));

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

        var dn = player.Passives.KiraDeathNote;
        if (dn.CurrentRoundTarget != Guid.Empty)
            return Task.FromResult((false, "Already written this round"));

        var target = game.PlayersList.Find(p => p.GetPlayerId() == targetPlayerId);
        if (target == null) return Task.FromResult((false, "Target not found"));
        if (target.GetPlayerId() == player.GetPlayerId())
            return Task.FromResult((false, "Cannot write your own name"));
        if (target.Passives.KiraDeathNoteDead || target.Passives.KratosIsDead)
            return Task.FromResult((false, "Target is already dead"));
        if (dn.FailedTargets.Contains(targetPlayerId))
            return Task.FromResult((false, "Already failed for this target"));

        dn.CurrentRoundTarget = targetPlayerId;
        dn.CurrentRoundName = characterName?.Trim() ?? "";
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

    public Task<(bool success, string error)> DopaChoice(ulong gameId, ulong discordId, string tactic)
    {
        var (game, player) = FindGameAndPlayer(gameId, discordId);
        if (game == null) return Task.FromResult((false, "Game not found"));
        if (player == null) return Task.FromResult((false, "Player not in this game"));
        if (!player.GameCharacter.Passive.Any(p => p.PassiveName == "Законодатель меты"))
            return Task.FromResult((false, "You don't have this passive"));
        if (player.Passives.DopaMetaChoice.Triggered)
            return Task.FromResult((false, "Already chosen"));

        var validTactics = new[] { "Стомп", "Фарм", "Доминация", "Роум" };
        if (!validTactics.Contains(tactic))
            return Task.FromResult((false, "Invalid tactic"));

        _characterPassives.ApplyDopaChoice(player, game, tactic);
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

    // ── Helpers ───────────────────────────────────────────────────────

    private static bool CanAct(GamePlayerBridgeClass player)
    {
        return !player.Status.IsReady && !player.Status.IsSkip;
    }
}
