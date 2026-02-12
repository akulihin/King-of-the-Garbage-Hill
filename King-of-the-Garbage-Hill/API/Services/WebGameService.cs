using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.ReactionHandling;

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

    public WebGameService(Global global, GameReaction gameReaction)
    {
        _global = global;
        _gameReaction = gameReaction;
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
            dto.Games.Add(new ActiveGameDto
            {
                GameId = game.GameId,
                RoundNo = game.RoundNo,
                PlayerCount = game.PlayersList.Count,
                HumanCount = game.PlayersList.Count(p => !p.IsBot()),
                GameMode = game.GameMode,
                IsFinished = game.IsFinished,
            });
        }

        return dto;
    }

    public GameStateDto GetGameState(ulong gameId, ulong discordId)
    {
        var game = FindGame(gameId);
        if (game == null) return null;

        var player = game.PlayersList.Find(p => p.DiscordId == discordId);
        return GameStateMapper.ToDto(game, player);
    }

    public GameStateDto GetGameStateForSpectator(ulong gameId)
    {
        var game = FindGame(gameId);
        return game == null ? null : GameStateMapper.ToDto(game);
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

    // ── Helpers ───────────────────────────────────────────────────────

    private static bool CanAct(GamePlayerBridgeClass player)
    {
        return !player.Status.IsReady && !player.Status.IsSkip;
    }
}
