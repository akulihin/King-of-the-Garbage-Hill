using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using King_of_the_Garbage_Hill.Clash.Logic;
using King_of_the_Garbage_Hill.Clash.Models;

namespace King_of_the_Garbage_Hill.API.Services;

public sealed class ClashService : IDisposable
{
    private const int ProcessedCommandLimit = 256;
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, ClashGame> _games = new();
    private readonly object _gamesGate = new();
    private readonly Timer _cleanupTimer;

    public ClashService()
    {
        _cleanupTimer = new Timer(300_000) { AutoReset = true, Enabled = true };
        _cleanupTimer.Elapsed += (_, _) => CleanupStaleGames();
    }

    public ClashCatalogDto GetCatalog()
    {
        return ClashCatalog.ToDto();
    }

    public ClashLobbyDto GetLobbyState()
    {
        var games = new List<ClashLobbyGameDto>();
        foreach (var game in _games.Values)
        {
            lock (game.SyncRoot)
            {
                if (game.IsFinished) continue;
                games.Add(new ClashLobbyGameDto
                {
                    GameId = game.GameId,
                    Phase = game.Phase.ToString(),
                    Width = game.Width,
                    Length = game.Length,
                    HostName = game.Host?.Username ?? "",
                    GuestName = game.Guest?.Username ?? "",
                    VsBot = game.VsBot,
                    CanJoin = !game.VsBot && game.Guest == null &&
                              game.Phase == ClashGamePhase.Lobby,
                    CreatedAt = game.CreatedAt.ToString("o"),
                });
            }
        }

        return new ClashLobbyDto
        {
            Games = games.OrderByDescending(game => game.CreatedAt).ToList(),
        };
    }

    public (string gameId, string error) CreateGame(
        string playerId,
        string username,
        bool vsBot,
        int width = ClashCatalog.DefaultWidth,
        int length = ClashCatalog.DefaultLength)
    {
        if (!ClashGameEngine.ValidateDimensions(width, length, out var dimensionError))
            return (null, dimensionError);

        lock (_gamesGate)
        {
            if (PlayerHasActiveGame(playerId))
                return (null, "У вас уже есть активная игра Clash.");

            var game = new ClashGame
            {
                Width = width,
                Length = length,
                VsBot = vsBot,
                Host = new ClashPlayer
                {
                    PlayerId = playerId,
                    Username = string.IsNullOrWhiteSpace(username) ? "Player" : username,
                    IsHost = true,
                    Morale = ClashCatalog.StartingMorale,
                },
            };

            if (vsBot)
            {
                game.Guest = CreateBot(game);
            }

            _games[game.GameId] = game;
            return (game.GameId, null);
        }
    }

    public ClashMutationResult JoinGame(string gameId, string playerId, string username)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return ClashMutationResult.Fail("Игра не найдена.");

        lock (_gamesGate)
        lock (game.SyncRoot)
        {
            if (game.IsFinished) return ClashMutationResult.Fail("Игра уже завершена.");
            if (game.Host?.PlayerId == playerId || game.Guest?.PlayerId == playerId)
                return ClashMutationResult.Ok();
            if (PlayerHasActiveGame(playerId, gameId))
                return ClashMutationResult.Fail("У вас уже есть активная игра Clash.");
            if (game.VsBot) return ClashMutationResult.Fail("Эта игра создана против бота.");
            if (game.Phase != ClashGamePhase.Lobby || game.Guest != null)
                return ClashMutationResult.Fail("Лобби уже заполнено.");

            game.Guest = new ClashPlayer
            {
                PlayerId = playerId,
                Username = string.IsNullOrWhiteSpace(username) ? "Player" : username,
                Morale = ClashCatalog.StartingMorale,
            };
            ClashGameEngine.Touch(game);
            return ClashMutationResult.Ok();
        }
    }

    public ClashMutationResult LeaveGame(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return ClashMutationResult.Fail("Игра не найдена.");

        lock (game.SyncRoot)
        {
            var player = game.GetPlayer(playerId);
            if (player == null) return ClashMutationResult.Fail("Вы не участвуете в этой игре.");
            if (game.IsFinished) return ClashMutationResult.Ok();

            if (game.Phase == ClashGamePhase.Lobby && player == game.Guest && !game.VsBot)
            {
                game.Guest = null;
                ClashGameEngine.Touch(game);
                return ClashMutationResult.Ok();
            }

            ClashGameEngine.EndByPlayerExit(game, playerId, ClashTerminalReason.Leave);
            return ClashMutationResult.Ok();
        }
    }

    public ClashMutationResult SetConfiguration(
        string gameId,
        string playerId,
        int width,
        int length,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            if (game.Phase != ClashGamePhase.Lobby)
                return ClashMutationResult.Fail("Конфигурацию можно менять только в лобби.");
            if (game.Host?.PlayerId != playerId)
                return ClashMutationResult.Fail("Только хост может менять поле.");
            if (!ClashGameEngine.ValidateDimensions(width, length, out var error))
                return ClashMutationResult.Fail(error);

            game.Width = width;
            game.Length = length;
            game.Host.ArmyDefinitionIds.Clear();
            game.Host.IsReady = false;
            if (game.Guest != null)
            {
                game.Guest.ArmyDefinitionIds = game.Guest.IsBot
                    ? ClashBotAI.ChooseArmy(width, length)
                    : new List<string>();
                game.Guest.IsReady = game.Guest.IsBot;
            }
            ClashGameEngine.Touch(game);
            return ClashMutationResult.Ok();
        });
    }

    public ClashMutationResult SetArmy(
        string gameId,
        string playerId,
        List<string> unitDefinitionIds,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            if (game.Phase != ClashGamePhase.Lobby)
                return ClashMutationResult.Fail("Армию можно собирать только в лобби.");
            var player = game.GetPlayer(playerId);
            if (player == null || player.IsBot)
                return ClashMutationResult.Fail("Вы не участвуете в этом лобби.");

            var error = ValidateArmy(game, unitDefinitionIds);
            if (error != null) return ClashMutationResult.Fail(error);

            player.ArmyDefinitionIds = unitDefinitionIds.ToList();
            player.IsReady = false;
            ClashGameEngine.Touch(game);
            return ClashMutationResult.Ok();
        });
    }

    public ClashMutationResult ConfirmLobbyReady(
        string gameId,
        string playerId,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            if (game.Phase != ClashGamePhase.Lobby)
                return ClashMutationResult.Fail("Лобби уже завершило подготовку.");
            var player = game.GetPlayer(playerId);
            if (player == null || player.IsBot)
                return ClashMutationResult.Fail("Вы не участвуете в этом лобби.");
            if (game.Guest == null)
                return ClashMutationResult.Fail("Сначала дождитесь второго игрока.");

            var error = ValidateArmy(game, player.ArmyDefinitionIds);
            if (error != null) return ClashMutationResult.Fail(error);
            player.IsReady = true;
            ClashGameEngine.Touch(game);

            if (game.Host.IsReady && game.Guest.IsReady)
                ClashGameEngine.InstantiateArmies(game);
            return ClashMutationResult.Ok();
        });
    }

    public ClashMutationResult PlaceUnit(
        string gameId,
        string playerId,
        string unitInstanceId,
        int row,
        int column,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            var error = ClashGameEngine.PlaceInitialUnit(
                game, playerId, unitInstanceId, row, column);
            return error == null
                ? ClashMutationResult.Ok()
                : ClashMutationResult.Fail(error);
        }, allowParallelFrontPlacement: true);
    }

    public ClashMutationResult RemoveUnit(
        string gameId,
        string playerId,
        string unitInstanceId,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            var error = ClashGameEngine.RemoveInitialUnit(game, playerId, unitInstanceId);
            return error == null
                ? ClashMutationResult.Ok()
                : ClashMutationResult.Fail(error);
        }, allowParallelFrontPlacement: true);
    }

    public ClashMutationResult ConfirmPlacement(
        string gameId,
        string playerId,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            var resolution = ClashGameEngine.ConfirmPlacement(game, playerId, out var error);
            if (error != null) return ClashMutationResult.Fail(error);
            return ClashMutationResult.Ok(resolution == null ? null : new[] { resolution });
        }, allowParallelFrontPlacement: true);
    }

    public ClashMutationResult PlaceReinforcement(
        string gameId,
        string playerId,
        string unitInstanceId,
        int row,
        int column,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            var error = ClashGameEngine.PlaceReinforcement(
                game, playerId, unitInstanceId, row, column);
            return error == null
                ? ClashMutationResult.Ok()
                : ClashMutationResult.Fail(error);
        });
    }

    public ClashMutationResult UseActive(
        string gameId,
        string playerId,
        string sourceUnitInstanceId,
        string abilityId,
        string targetUnitInstanceId,
        int? targetRow,
        int? targetColumn,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            var resolution = ClashGameEngine.UseActive(
                game, playerId, sourceUnitInstanceId, abilityId,
                targetUnitInstanceId, targetRow, targetColumn, out var error);
            if (error != null) return ClashMutationResult.Fail(error);
            return ClashMutationResult.Ok(resolution == null ? null : new[] { resolution });
        });
    }

    public ClashMutationResult Continue(
        string gameId,
        string playerId,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            if (game.Phase is ClashGamePhase.GuestReinforcement or ClashGamePhase.HostReinforcement)
            {
                var error = ClashGameEngine.ContinueReinforcement(game, playerId);
                return error == null
                    ? ClashMutationResult.Ok()
                    : ClashMutationResult.Fail(error);
            }

            var resolution = ClashGameEngine.ContinueActiveExchange(game, playerId, out var activeError);
            if (activeError != null) return ClashMutationResult.Fail(activeError);
            return ClashMutationResult.Ok(resolution == null ? null : new[] { resolution });
        });
    }

    public ClashMutationResult Forfeit(
        string gameId,
        string playerId,
        long? expectedRevision = null,
        string commandId = null)
    {
        return Mutate(gameId, expectedRevision, commandId, game =>
        {
            if (game.GetPlayer(playerId) == null)
                return ClashMutationResult.Fail("Вы не участвуете в этой игре.");
            if (game.IsFinished) return ClashMutationResult.Ok();
            ClashGameEngine.EndByPlayerExit(game, playerId, ClashTerminalReason.Forfeit);
            return ClashMutationResult.Ok();
        }, runBot: false);
    }

    public ClashGameStateDto GetGameState(string gameId, string playerId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return null;
        lock (game.SyncRoot)
        {
            if (game.GetPlayer(playerId) == null) return null;
            return ToDto(game, playerId);
        }
    }

    public string GetActiveGameId(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return null;
        foreach (var game in _games.Values)
        {
            lock (game.SyncRoot)
            {
                if (!game.IsFinished && game.GetPlayer(playerId) != null)
                    return game.GameId;
            }
        }
        return null;
    }

    public List<string> GetPlayerIds(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return new();
        lock (game.SyncRoot)
        {
            return game.GetPlayers()
                .Where(player => !player.IsBot)
                .Select(player => player.PlayerId)
                .ToList();
        }
    }

    public bool HasPendingResolution(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        lock (game.SyncRoot)
        {
            return !game.IsFinished &&
                   game.Phase == ClashGamePhase.ResolvingClash &&
                   game.LatestResolution != null;
        }
    }

    public ClashResolutionCompletionResult CompleteResolution(string gameId)
    {
        var result = new ClashResolutionCompletionResult();
        if (!_games.TryGetValue(gameId, out var game)) return result;

        lock (game.SyncRoot)
        {
            if (game.IsFinished ||
                game.Phase != ClashGamePhase.ResolvingClash ||
                game.LatestResolution == null)
                return result;

            var deadline = game.ResolutionEndsAtUtc;
            if (deadline == null &&
                DateTime.TryParse(
                    game.LatestResolution.StartedAtUtc,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var startedAt))
            {
                deadline = startedAt.AddMilliseconds(game.LatestResolution.DurationMs);
                game.ResolutionEndsAtUtc = deadline;
            }

            var remaining = (deadline ?? DateTime.UtcNow) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                result.Pending = true;
                result.RemainingMs = Math.Max(1, (int)Math.Ceiling(remaining.TotalMilliseconds));
                return result;
            }

            game.ResolutionEndsAtUtc = null;
            game.Phase = ClashGamePhase.GuestReinforcement;
            game.CurrentTurnPlayerId = game.Guest?.PlayerId;
            ClashGameEngine.Touch(game);
            result.Completed = true;
            if (!game.IsFinished)
                result.Resolutions.AddRange(RunBotUntilHumanTurn(game));
            return result;
        }
    }

    private ClashMutationResult Mutate(
        string gameId,
        long? expectedRevision,
        string commandId,
        Func<ClashGame, ClashMutationResult> mutation,
        bool runBot = true,
        bool allowParallelFrontPlacement = false)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return ClashMutationResult.Fail("Игра не найдена.");

        lock (game.SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(commandId) &&
                game.ProcessedCommandIds.Contains(commandId))
                return ClashMutationResult.Ok();
            if (expectedRevision.HasValue && expectedRevision.Value != game.Revision &&
                !(allowParallelFrontPlacement &&
                  game.Phase == ClashGamePhase.InitialFrontPlacement))
                return ClashMutationResult.Fail(
                    $"Состояние устарело: ожидалась ревизия {expectedRevision.Value}, текущая {game.Revision}.");

            var result = mutation(game);
            if (!result.Success) return result;

            RememberCommand(game, commandId);
            if (runBot && !game.IsFinished)
                result.Resolutions.AddRange(RunBotUntilHumanTurn(game));
            return result;
        }
    }

    private List<ClashResolutionDto> RunBotUntilHumanTurn(ClashGame game)
    {
        var resolutions = new List<ClashResolutionDto>();
        if (game.Guest?.IsBot != true) return resolutions;

        for (var step = 0; step < 256 && !game.IsFinished; step++)
        {
            var state = ToDto(game, game.Guest.PlayerId);
            var decision = ClashBotAI.Decide(state);
            if (decision.Kind == ClashBotActionKind.None) break;

            string error = null;
            ClashResolutionDto resolution = null;
            switch (decision.Kind)
            {
                case ClashBotActionKind.PlaceUnit:
                    error = ClashGameEngine.PlaceInitialUnit(
                        game, game.Guest.PlayerId, decision.UnitInstanceId,
                        decision.Row, decision.Column);
                    break;
                case ClashBotActionKind.ConfirmPlacement:
                    resolution = ClashGameEngine.ConfirmPlacement(
                        game, game.Guest.PlayerId, out error);
                    break;
                case ClashBotActionKind.PlaceReinforcement:
                    error = ClashGameEngine.PlaceReinforcement(
                        game, game.Guest.PlayerId, decision.UnitInstanceId,
                        decision.Row, decision.Column);
                    break;
                case ClashBotActionKind.Continue:
                    if (game.Phase is ClashGamePhase.GuestReinforcement or ClashGamePhase.HostReinforcement)
                        error = ClashGameEngine.ContinueReinforcement(game, game.Guest.PlayerId);
                    else
                        resolution = ClashGameEngine.ContinueActiveExchange(
                            game, game.Guest.PlayerId, out error);
                    break;
            }

            if (resolution != null) resolutions.Add(resolution);
            if (error != null) break;
        }

        return resolutions;
    }

    private ClashGameStateDto ToDto(ClashGame game, string requestingPlayerId)
    {
        var requester = game.GetPlayer(requestingPlayerId);
        var requiredRow = requester == null
            ? null
            : ClashGameEngine.RequiredPlacementRow(game, requestingPlayerId);
        var canPlace = requester != null &&
                       ClashGameEngine.CanPlaceInitialUnit(game, requestingPlayerId);
        var canConfirmPlacement = canPlace && requiredRow.HasValue &&
                                  IsRowFull(game, requester, requiredRow.Value);
        var isCurrentPlayer = requester != null &&
                              game.CurrentTurnPlayerId == requester.PlayerId;
        var isReinforcementTurn = requester != null &&
                                  ClashGameEngine.IsReinforcementTurn(game, requester.PlayerId);
        var isActiveTurn = requester != null &&
                           game.Phase == ClashGamePhase.ActiveExchange &&
                           isCurrentPlayer && !requester.ActiveDone;

        var cells = new List<ClashBoardCellDto>(game.Width * game.Length * 2);
        for (var boardRow = 0; boardRow < game.Length * 2; boardRow++)
        for (var column = 0; column < game.Width; column++)
        {
            var unit = game.Units.FirstOrDefault(candidate =>
                candidate.Alive && candidate.Deployed &&
                candidate.BoardRow == boardRow && candidate.Column == column);
            var visibleUnit = unit != null && IsVisibleTo(unit, requestingPlayerId)
                ? ClashGameEngine.ToUnitDto(game, unit)
                : null;
            cells.Add(new ClashBoardCellDto
            {
                BoardRow = boardRow,
                Column = column,
                TerritorySide = ClashGameEngine.TerritoryFor(game, boardRow).ToString(),
                Unit = visibleUnit,
                // Hidden deployment is deliberately indistinguishable from an empty cell.
                IsHidden = false,
            });
        }

        return new ClashGameStateDto
        {
            GameId = game.GameId,
            Revision = game.Revision,
            Phase = game.Phase.ToString(),
            Width = game.Width,
            Length = game.Length,
            ClashNumber = game.ClashNumber,
            VsBot = game.VsBot,
            IsFinished = game.IsFinished,
            WinnerId = game.WinnerId,
            IsDraw = game.IsDraw,
            TerminalReason = game.TerminalReason == ClashTerminalReason.None
                ? null
                : game.TerminalReason.ToString(),
            CurrentTurnPlayerId = game.CurrentTurnPlayerId,
            IsMyTurn = isCurrentPlayer,
            MyPlayerId = requester?.PlayerId,
            RequiredPlacementRow = requiredRow,
            PlacementActionLabel = requiredRow switch
            {
                0 => "Сблизиться!",
                1 => "Становись!",
                2 => "Вступить в бой!",
                _ => null,
            },
            Host = MapPlayer(game, game.Host, requestingPlayerId),
            Guest = MapPlayer(game, game.Guest, requestingPlayerId),
            BoardCells = cells,
            LatestResolution = game.LatestResolution,
            CanConfigure = requester == game.Host && game.Phase == ClashGamePhase.Lobby,
            CanSetArmy = requester is { IsBot: false } &&
                         game.Phase == ClashGamePhase.Lobby,
            CanConfirmReady = requester is { IsBot: false, IsReady: false } &&
                              game.Phase == ClashGamePhase.Lobby &&
                              game.Guest != null &&
                              ValidateArmy(game, requester.ArmyDefinitionIds) == null,
            CanPlace = canPlace,
            CanRemove = canPlace,
            CanConfirmPlacement = canConfirmPlacement,
            CanPlaceReinforcement = isReinforcementTurn,
            CanUseActive = isActiveTurn &&
                           requester.ActiveSelectionsUsed <
                           ClashGameEngine.ActiveSelectionLimit(requester.Morale) &&
                           game.Units.Any(unit =>
                               unit.OwnerId == requester.PlayerId && unit.Alive && unit.Deployed &&
                               ClashCatalog.Get(unit.DefinitionId).Abilities.Count > 0),
            CanContinue = isReinforcementTurn || isActiveTurn,
            CanForfeit = requester != null && !game.IsFinished,
        };
    }

    private static ClashPlayerDto MapPlayer(
        ClashGame game,
        ClashPlayer player,
        string requestingPlayerId)
    {
        if (player == null) return null;
        var isMe = player.PlayerId == requestingPlayerId;
        var hand = game.Units
            .Where(unit => unit.OwnerId == player.PlayerId && unit.Alive && !unit.Deployed)
            .OrderBy(unit => unit.InstanceId, StringComparer.Ordinal)
            .ToList();
        return new ClashPlayerDto
        {
            PlayerId = player.PlayerId,
            Username = player.Username,
            IsBot = player.IsBot,
            IsHost = player.IsHost,
            IsMe = isMe,
            IsReady = player.IsReady,
            InitialFrontConfirmed = player.FrontConfirmed,
            Morale = player.Morale,
            ArmySize = isMe ? player.ArmyDefinitionIds.Count : 0,
            HandCount = isMe ? hand.Count : 0,
            SelectedArmyDefinitionIds = isMe
                ? player.ArmyDefinitionIds.ToList()
                : new(),
            Hand = isMe
                ? hand.Select(unit => ClashGameEngine.ToUnitDto(game, unit)).ToList()
                : new(),
            UsedActiveIds = player.UsedActiveKeys.ToList(),
            ActiveSelectionsUsed = player.ActiveSelectionsUsed,
            ActiveSelectionLimit = ClashGameEngine.ActiveSelectionLimit(player.Morale),
            CanRepeatActive = player.Morale == 4 &&
                              player.ActiveSelectionsUsed == 3 &&
                              !player.RepeatedActiveUsed,
            ActiveEffectsDoubled = ClashGameEngine.ActiveEffectsDoubled(player.Morale),
            HasContinued = player.ActiveDone,
        };
    }

    private static bool IsVisibleTo(ClashUnit unit, string requestingPlayerId)
    {
        return unit.IsVisible || unit.OwnerId == requestingPlayerId;
    }

    private static bool IsRowFull(ClashGame game, ClashPlayer player, int localRow)
    {
        var side = player.IsHost ? ClashSide.Host : ClashSide.Guest;
        var boardRow = ClashGameEngine.LocalToBoardRow(game, side, localRow);
        return game.Units.Count(unit =>
            unit.OwnerId == player.PlayerId && unit.Alive && unit.Deployed &&
            unit.BoardRow == boardRow) == game.Width;
    }

    private static string ValidateArmy(ClashGame game, IReadOnlyCollection<string> definitions)
    {
        var minimum = game.Width * 3;
        var maximum = game.Width * game.Length;
        var count = definitions?.Count ?? 0;
        if (count < minimum || count > maximum)
            return $"В руке должно быть от {minimum} до {maximum} юнитов.";
        var invalidId = definitions.FirstOrDefault(id => !ClashCatalog.TryGet(id, out _));
        return invalidId == null ? null : $"Неизвестный или недоступный юнит: {invalidId}.";
    }

    private static ClashPlayer CreateBot(ClashGame game)
    {
        return new ClashPlayer
        {
            PlayerId = $"bot_clash_{game.GameId}",
            Username = "Бот",
            IsBot = true,
            Morale = ClashCatalog.StartingMorale,
            ArmyDefinitionIds = ClashBotAI.ChooseArmy(game.Width, game.Length),
            IsReady = true,
        };
    }

    private bool PlayerHasActiveGame(string playerId, string exceptGameId = null)
    {
        foreach (var game in _games.Values)
        {
            if (game.GameId == exceptGameId) continue;
            lock (game.SyncRoot)
            {
                if (!game.IsFinished && game.GetPlayer(playerId) != null) return true;
            }
        }
        return false;
    }

    private static void RememberCommand(ClashGame game, string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId) ||
            !game.ProcessedCommandIds.Add(commandId))
            return;
        game.ProcessedCommandOrder.Enqueue(commandId);
        while (game.ProcessedCommandOrder.Count > ProcessedCommandLimit)
        {
            var expired = game.ProcessedCommandOrder.Dequeue();
            game.ProcessedCommandIds.Remove(expired);
        }
    }

    private void CleanupStaleGames()
    {
        var cutoff = DateTime.UtcNow - StaleAfter;
        foreach (var pair in _games)
        {
            lock (pair.Value.SyncRoot)
            {
                if (pair.Value.LastActivity < cutoff &&
                    pair.Value.Phase is ClashGamePhase.Lobby or ClashGamePhase.Finished)
                    _games.TryRemove(pair.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}
