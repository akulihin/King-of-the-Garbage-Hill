using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using King_of_the_Garbage_Hill.Battleship.Logic;
using King_of_the_Garbage_Hill.Battleship.Models;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.API.Services;

public class BattleshipService
{
    private static readonly TimeSpan ComboHitDelay = TimeSpan.FromSeconds(8);
    /// <summary>ZBS paid for the first battleship win of the (UTC) day. Anchor: a daily quest card pays 20.</summary>
    public const int BattleshipFirstWinZbs = 10;

    private readonly ConcurrentDictionary<string, BattleshipGame> _games = new();
    private readonly Timer _cleanupTimer;
    private readonly UserAccounts _userAccounts;
    private static readonly Random Rng = new();

    public BattleshipService(UserAccounts userAccounts)
    {
        _userAccounts = userAccounts;
        _cleanupTimer = new Timer
        {
            AutoReset = true,
            Interval = 300_000, // 5 minutes
            Enabled = true,
        };
        _cleanupTimer.Elapsed += (_, _) => CleanupStaleGames();
    }

    // ── Meta settlement (W/L record, daily streak, first-win ZBS) ────

    /// <summary>
    /// Settle the persistent meta exactly once per finished game. The game has four
    /// distinct end paths (LeaveGame, Forfeit, CheckAndApplyWin, Desiccator auto-win in
    /// TriggerBoarding), so every public mutating method calls this at the end while
    /// still holding the game lock. Lock order is always game → account; quest/lootbox
    /// settlement only ever takes the account lock, so no cycle is possible.
    /// </summary>
    private void TrySettleGameEnd(BattleshipGame game)
    {
        if (!game.IsFinished || game.MetaSettled) return;
        game.MetaSettled = true;

        // A game abandoned before combat (lobby/setup leave) is not a played match.
        if (!game.CombatStarted) return;

        foreach (var player in game.GetPlayers())
        {
            // Human players only — bot ids are "bot_<gameId>" and never parse.
            if (player?.DiscordId == null || !ulong.TryParse(player.DiscordId, out var discordId)) continue;
            var account = _userAccounts.GetAccount(discordId);
            if (account == null) continue;

            var won = game.WinnerId == player.DiscordId;
            game.EndRewards[player.DiscordId] = SettlePlayerMeta(account, won);
        }
    }

    private BattleshipEndReward SettlePlayerMeta(DiscordAccountClass account, bool won)
    {
        lock (account)
        {
            var firstWinAwarded = false;
            var zbsAwarded = 0;
            var stats = account.BattleshipStats ??= new DiscordAccountClass.BattleshipStatsData();
            var now = DateTime.UtcNow;
            var today = now.ToString("yyyy-MM-dd");
            var yesterday = now.AddDays(-1).ToString("yyyy-MM-dd");

            if (won)
            {
                stats.Wins++;

                // Daily win streak — mirrors QuestClass.AdvanceStreak (consecutive win-days)
                if (stats.LastWinDayUtc != today)
                {
                    stats.CurrentDailyStreak = stats.LastWinDayUtc == yesterday ? stats.CurrentDailyStreak + 1 : 1;
                    stats.BestDailyStreak = Math.Max(stats.BestDailyStreak, stats.CurrentDailyStreak);
                    stats.LastWinDayUtc = today;
                }

                // First win of the day → ZBS bonus
                if (stats.LastFirstWinAwardDayUtc != today)
                {
                    stats.LastFirstWinAwardDayUtc = today;
                    account.ZbsPoints += BattleshipFirstWinZbs;
                    stats.TotalZbsEarned += BattleshipFirstWinZbs;
                    firstWinAwarded = true;
                    zbsAwarded = BattleshipFirstWinZbs;
                }
            }
            else
            {
                stats.Losses++;
            }

            // SaveAccount takes the same (re-entrant) account monitor. Keeping the
            // save inside this critical section prevents another settlement from
            // changing the snapshot between mutation and persistence.
            _userAccounts.SaveAccount(account);

            var streakIsFresh = stats.LastWinDayUtc == today || stats.LastWinDayUtc == yesterday;
            return new BattleshipEndReward
            {
                Won = won,
                Wins = stats.Wins,
                Losses = stats.Losses,
                CurrentDailyStreak = streakIsFresh ? stats.CurrentDailyStreak : 0,
                BestDailyStreak = stats.BestDailyStreak,
                FirstWinAwarded = firstWinAwarded,
                ZbsAwarded = zbsAwarded,
            };
        }
    }

    /// <summary>Lobby stats panel data. Streak reads as 0 when stale (last win before yesterday).</summary>
    public BattleshipStatsDto GetPlayerStats(DiscordAccountClass account)
    {
        if (account == null) return null;

        lock (account)
        {
            var stats = account.BattleshipStats;
            var now = DateTime.UtcNow;
            var today = now.ToString("yyyy-MM-dd");
            var yesterday = now.AddDays(-1).ToString("yyyy-MM-dd");
            var streakIsFresh = stats?.LastWinDayUtc == today || stats?.LastWinDayUtc == yesterday;

            return new BattleshipStatsDto
            {
                Wins = stats?.Wins ?? 0,
                Losses = stats?.Losses ?? 0,
                CurrentDailyStreak = streakIsFresh ? stats.CurrentDailyStreak : 0,
                BestDailyStreak = stats?.BestDailyStreak ?? 0,
                FirstWinAvailable = stats?.LastFirstWinAwardDayUtc != today,
                FirstWinZbs = BattleshipFirstWinZbs,
                ZbsBalance = account.ZbsPoints,
            };
        }
    }

    // ── Lobby ────────────────────────────────────────────────────────

    public BattleshipLobbyDto GetLobbyState()
    {
        var games = _games.Values
            .Where(g => !g.IsFinished)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new BattleshipLobbyGameDto
            {
                GameId = g.GameId,
                Phase = g.Phase.ToString(),
                Player1Name = g.Player1?.Username ?? "",
                Player2Name = g.Player2?.Username ?? "",
                Player1IsBot = g.Player1?.IsBot ?? false,
                Player2IsBot = g.Player2?.IsBot ?? false,
                TurnNumber = g.TurnNumber,
                CreatedAt = g.CreatedAt.ToString("o"),
            })
            .ToList();

        return new BattleshipLobbyDto { Games = games };
    }

    public (string gameId, string error) CreateGame(string discordId, string username)
    {
        // Check if player already has an active game
        foreach (var g in _games.Values)
        {
            if (!g.IsFinished && (g.Player1?.DiscordId == discordId || g.Player2?.DiscordId == discordId))
                return (null, "У вас уже есть активная игра в Морской Бой - minigame.");
        }

        var game = new BattleshipGame();
        game.Player1 = new BattleshipPlayer
        {
            DiscordId = discordId,
            Username = username
        };
        game.Player2 = new BattleshipPlayer
        {
            DiscordId = $"bot_{game.GameId}",
            Username = "Бот",
            IsBot = true
        };

        _games[game.GameId] = game;
        Console.WriteLine($"[Battleship] Game {game.GameId} created by {username}");

        return (game.GameId, null);
    }

    public (bool success, string error) JoinGame(string gameId, string discordId, string username)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.IsFinished)
                return (false, "Игра уже завершена.");

            // Already in game?
            if (game.Player1?.DiscordId == discordId || game.Player2?.DiscordId == discordId)
                return (true, null);

            // A human may replace the placeholder bot only before setup begins.
            if (game.Player2?.IsBot == true && game.Phase == BsGamePhase.Lobby)
            {
                game.Player2 = new BattleshipPlayer
                {
                    DiscordId = discordId,
                    Username = username
                };
                game.LastActivity = DateTime.UtcNow;
                Console.WriteLine($"[Battleship] {username} joined game {gameId} (replaced bot)");
                return (true, null);
            }

            return (false, "Игра уже заполнена.");
        }
    }

    public (bool success, string error) LeaveGame(string gameId, string discordId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Player1?.DiscordId == discordId)
            {
                // If the creator leaves, end the game
                game.IsFinished = true;
                game.WinnerId = game.Player2?.DiscordId;
                game.Phase = BsGamePhase.GameOver;
                TrySettleGameEnd(game);
                return (true, null);
            }

            if (game.Player2?.DiscordId == discordId)
            {
                if (game.CombatStarted)
                {
                    var leaverName = game.Player2.Username;
                    game.IsFinished = true;
                    game.WinnerId = game.Player1?.DiscordId;
                    game.Phase = BsGamePhase.GameOver;
                    game.AddLog($"{leaverName} покинул бой. Победитель: {game.Player1?.Username ?? "???"}!");
                    TrySettleGameEnd(game);
                    return (true, null);
                }

                if (game.Phase != BsGamePhase.Lobby)
                {
                    game.IsFinished = true;
                    game.Phase = BsGamePhase.GameOver;
                    game.WinnerId = null;
                    game.AddLog("Второй игрок покинул игру до начала боя. Игра отменена.");
                    TrySettleGameEnd(game);
                    return (true, null);
                }

                game.Player2 = new BattleshipPlayer
                {
                    DiscordId = $"bot_{game.GameId}",
                    Username = "Бот",
                    IsBot = true
                };

                game.LastActivity = DateTime.UtcNow;
                // Taking over can immediately run the bot's turn and finish the
                // match. Settle the remaining human before returning in that case.
                TrySettleGameEnd(game);
                return (true, null);
            }

            return (false, "Вы не в этой игре.");
        }
    }

    // ── Phase: Army Selection ────────────────────────────────────────

    public (bool success, string error) SelectArmy(string gameId, string discordId, string faction)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.ArmySelection)
                return (false, "Сейчас не фаза выбора армии.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");
            player.Faction = Faction.Empire; // only option for now
            player.IsReady = true;
            game.LastActivity = DateTime.UtcNow;

            // Check if both players ready
            CheckPhaseTransition(game);
            TrySettleGameEnd(game);

            return (true, null);
        }
    }

    // ── Phase: Fleet Building ────────────────────────────────────────

    public (bool success, string error) SelectFleet(string gameId, string discordId, List<FleetSelection> selections)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.FleetBuilding)
                return (false, "Сейчас не фаза сборки флота.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            // Validate fleet purchases
            var (valid, error) = FleetValidator.ValidateFleet(selections);
            if (!valid) return (false, error);

            // Build full 10-ship fleet from purchases (fills defaults)
            var fullFleet = FleetValidator.BuildFleetFromSelections(selections);

            // Store selection and build ships
            player.SelectedShips = fullFleet;
            player.CoinsRemaining = FleetValidator.MaxBudget - FleetValidator.CalculateTotalCost(fullFleet);
            player.Fleet.Clear();

            foreach (var sel in fullFleet)
            {
                var def = ShipCatalog.GetById(sel.DefinitionId);
                if (def != null)
                {
                    var ship = ShipCatalog.CreateShip(def, sel.Upgrades);
                    player.Fleet.Add(ship);
                }
            }
            NumberDuplicateShips(player.Fleet);

            player.IsReady = true;
            game.LastActivity = DateTime.UtcNow;

            CheckPhaseTransition(game);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    // ── Phase: Ship Placement ────────────────────────────────────────

    public (bool success, string error) PlaceShip(string gameId, string discordId, string shipId, int row, int col, string orientationStr)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.ShipPlacement)
                return (false, "Сейчас не фаза размещения.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");
            if (player.IsReady)
                return (false, "Размещение уже подтверждено.");

            var ship = player.Fleet.Find(s => s.Id == shipId);
            if (ship == null)
                return (false, "Корабль не найден.");

            if (!Enum.TryParse<Orientation>(orientationStr, true, out var orientation))
                return (false, "Неверная ориентация.");

            var wasPlaced = ship.IsPlaced;
            var oldRow = ship.Row;
            var oldCol = ship.Col;
            var oldOrientation = ship.Orientation;
            if (wasPlaced)
                RemoveShipFromBoard(player, ship);

            // Validate and place
            var (valid, error) = PlacementValidator.ValidatePlacement(player.Board, ship, row, col, orientation);
            if (!valid)
            {
                if (wasPlaced)
                    PlaceShipOnBoard(player, ship, oldRow, oldCol, oldOrientation);
                return (false, error);
            }

            PlaceShipOnBoard(player, ship, row, col, orientation);

            game.LastActivity = DateTime.UtcNow;
            return (true, null);
        }
    }

    public (bool success, string error) RemoveShip(string gameId, string discordId, string shipId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.ShipPlacement)
                return (false, "Сейчас не фаза размещения.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");
            if (player.IsReady)
                return (false, "Размещение уже подтверждено.");

            var ship = player.Fleet.Find(s => s.Id == shipId);
            if (ship == null)
                return (false, "Корабль не найден.");

            if (ship.IsPlaced)
                RemoveShipFromBoard(player, ship);

            game.LastActivity = DateTime.UtcNow;
            return (true, null);
        }
    }

    public (bool success, string error) ConfirmPlacement(string gameId, string discordId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.ShipPlacement)
                return (false, "Сейчас не фаза размещения.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            // Validate all ships placed
            var (valid, error) = PlacementValidator.ValidateAllPlaced(player.Fleet, player.Board);
            if (!valid) return (false, error);

            player.IsReady = true;
            game.LastActivity = DateTime.UtcNow;

            CheckPhaseTransition(game);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    // ── Phase: Combat ────────────────────────────────────────────────

    public (ShotResult result, string error) Shoot(string gameId, string discordId, int row, int col)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (null, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.Combat && game.Phase != BsGamePhase.Boarding)
                return (null, "Сейчас не фаза боя.");

            if (game.CurrentTurnPlayerId != discordId)
                return (null, "Сейчас не ваш ход.");

            var shooter = game.GetPlayer(discordId);
            if (shooter == null)
                return (null, "Вы не в этой игре.");

            if (row < 0 || row >= 10 || col < 0 || col >= 10)
                return (null, "Клетка за пределами поля.");

            // Boarding deployment is a global pause, not merely a restriction on the
            // player whose turn happened to be active when the transition fired.
            if (HasPendingBoardingDeployments(game))
                return (null, "Разместите все абордажные корабли!");

            var delayError = GetShotDelayError(shooter);
            if (delayError != null) return (null, delayError);

            if (BattleshipGameEngine.ProcessTurnStart(game, shooter))
            {
                SwitchTurn(game);
                game.TurnNumber++;
                game.LastActivity = DateTime.UtcNow;
                TrySettleGameEnd(game);
                return (new ShotResult { WasSkipped = true, TurnContinues = false, Message = "Ход пропущен!" }, null);
            }

            var weaponError = ValidateSelectedWeapon(game, shooter, ownBoard: false);
            if (weaponError != null) return (null, weaponError);
            if (shooter.SelectedShotType == ShotType.Buckshot && (row > 8 || col > 8))
                return (null, "Картечь должна полностью помещаться на поле.");

            // Process shot (buckshot uses 2x2 AoE)
            ShotResult result;
            if (shooter.SelectedShotType == ShotType.Buckshot)
                result = BattleshipGameEngine.ProcessBuckshotShot(game, shooter, row, col);
            else
                result = BattleshipGameEngine.ProcessShot(game, shooter, row, col);

            ApplyComboShotDelay(shooter, result);
            DescribeShot(game, shooter, result, ownBoard: false);
            ResetExpendedSelection(shooter);
            shooter.HasShotThisTurn = true;
            game.LastActivity = DateTime.UtcNow;
            CompleteActionResolution(game, result.TurnContinues, moveSummons: true);
            if (!game.IsFinished)
                CheckAndApplyWin(game);
            TrySettleGameEnd(game);

            return (result, null);
        }
    }

    public (ShotResult result, string error) ShootOwnBoard(string gameId, string discordId, int row, int col)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (null, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.Combat && game.Phase != BsGamePhase.Boarding)
                return (null, "Сейчас не фаза боя.");

            if (game.CurrentTurnPlayerId != discordId)
                return (null, "Сейчас не ваш ход.");

            var shooter = game.GetPlayer(discordId);
            if (shooter == null)
                return (null, "Вы не в этой игре.");

            if (row < 0 || row >= 10 || col < 0 || col >= 10)
                return (null, "Клетка за пределами поля.");

            if (HasPendingBoardingDeployments(game))
                return (null, "Разместите все абордажные корабли!");

            var delayError = GetShotDelayError(shooter);
            if (delayError != null) return (null, delayError);

            if (BattleshipGameEngine.ProcessTurnStart(game, shooter))
            {
                SwitchTurn(game);
                game.TurnNumber++;
                game.LastActivity = DateTime.UtcNow;

                TrySettleGameEnd(game);

                return (new ShotResult { WasSkipped = true, TurnContinues = false, Message = "Ход пропущен!" }, null);
            }

            var captured = shooter.Board.PlacedShips.Any(s =>
                !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture));
            if (!captured && shooter.SelectedShotType != ShotType.GreekFire)
            {
                var cell = shooter.Board.GetCell(row, col);
                if (cell?.SummonRef is not { IsAlive: true } enemySummon || enemySummon.OwnerId == shooter.DiscordId)
                    return (null, "На этой клетке нет вражеского призыва.");
            }

            var weaponError = ValidateSelectedWeapon(game, shooter, ownBoard: true);
            if (weaponError != null) return (null, weaponError);

            var isGreekFire = shooter.SelectedShotType == ShotType.GreekFire;
            ShotResult result;
            if (captured)
            {
                result = BattleshipGameEngine.ProcessShot(game, shooter, row, col);
            }
            else if (isGreekFire)
            {
                shooter.SelectedWeapon.UseAmmo();
                result = BattleshipGameEngine.ProcessOwnBoardGreekFireShot(game, shooter, row, col);
            }
            else
            {
                result = BattleshipGameEngine.ProcessOwnBoardShot(game, shooter, row, col);
            }

            ApplyComboShotDelay(shooter, result);
            DescribeShot(game, shooter, result, ownBoard: true);
            ResetExpendedSelection(shooter);
            shooter.HasShotThisTurn = true;
            game.LastActivity = DateTime.UtcNow;
            CompleteActionResolution(game, turnContinues: false, moveSummons: true);
            if (!game.IsFinished) CheckAndApplyWin(game);

            TrySettleGameEnd(game);

            return (result, null);
        }
    }

    public (bool success, string error) Forfeit(string gameId, string discordId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.IsFinished)
                return (false, "Игра уже завершена.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            var opponent = game.GetOpponent(discordId);
            if (opponent == null)
                return (false, "Нет противника.");

            game.IsFinished = true;
            game.WinnerId = opponent.DiscordId;
            game.Phase = BsGamePhase.GameOver;
            game.AddLog($"{player.Username} сдался. Победитель: {opponent.Username}!");
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    private void CheckAndApplyWin(BattleshipGame game)
    {
        var (gameOver, winnerId) = BattleshipGameEngine.CheckWinCondition(game);
        if (gameOver)
        {
            game.IsFinished = true;
            game.WinnerId = winnerId;
            game.Phase = BsGamePhase.GameOver;
            game.AddLog($"Победитель: {game.GetPlayer(winnerId)?.Username ?? "???"}!");
        }
    }

    private static string ValidateSelectedWeapon(BattleshipGame game, BattleshipPlayer player, bool ownBoard)
    {
        if (player.SelectedShotType == ShotType.GreekFire && !ownBoard)
            return "Греческий огонь стреляет только по своему полю.";

        var captured = player.Board.PlacedShips.Any(s =>
            !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture));
        if (captured && player.SelectedShotType != ShotType.Ballista)
            return "Сначала уничтожьте захваченный корабль Баллистой.";

        var requiredType = player.SelectedShotType switch
        {
            ShotType.WhiteStone or ShotType.Buckshot => WeaponType.Tetracatapult,
            ShotType.Incendiary => WeaponType.Incendiary,
            ShotType.GreekFire => WeaponType.GreekFire,
            _ => WeaponType.Ballista,
        };
        var usable = BattleshipGameEngine.GetUsableWeapons(game, player, requiredType).ToList();
        if (player.SelectedWeapon == null ||
            (requiredType == WeaponType.Ballista && usable.All(x => x.weapon.Id != player.SelectedWeapon.Id)))
            player.SelectedWeapon = usable.Select(x => x.weapon).FirstOrDefault();
        if (player.SelectedWeapon == null || usable.All(x => x.weapon.Id != player.SelectedWeapon.Id))
            return "Выбранное оружие или его модуль уничтожены, либо закончились боеприпасы.";
        if (player.SelectedWeapon.AimSpeed > player.RevealedCellCount)
            return $"Оружие ещё заряжается! Нужно разведать {player.SelectedWeapon.AimSpeed - player.RevealedCellCount} клеток.";
        return null;
    }

    private static void ResetExpendedSelection(BattleshipPlayer player)
    {
        if (player.SelectedShotType is ShotType.WhiteStone or ShotType.Buckshot or ShotType.GreekFire ||
            player.SelectedWeapon is { HasAmmo: false })
        {
            player.SelectedShotType = ShotType.Ballista;
            player.SelectedWeapon = null;
        }
    }

    private static void DescribeShot(
        BattleshipGame game,
        BattleshipPlayer shooter,
        ShotResult result,
        bool ownBoard)
    {
        if (result == null) return;
        var visualSource = shooter.SelectedShotType == ShotType.Ballista
            ? SelectNextBallistaAnimationSource(game, shooter)
            : null;
        result.SourceShipId = visualSource?.weapon.ShipId ?? shooter.SelectedWeapon?.ShipId;
        result.SourceDeckIndex = visualSource?.weapon.DeckIndex ?? shooter.SelectedWeapon?.DeckIndex ?? -1;
        result.ProjectileType = shooter.SelectedShotType switch
        {
            ShotType.WhiteStone => "Stone",
            ShotType.Buckshot => "Buckshot",
            ShotType.Incendiary or ShotType.GreekFire => "Fire",
            _ => "Arrow",
        };
        result.TargetPlayerId = ownBoard
            ? shooter.DiscordId
            : game.GetOpponent(shooter.DiscordId)?.DiscordId;
    }

    private static (Ship ship, Weapon weapon)? SelectNextBallistaAnimationSource(
        BattleshipGame game,
        BattleshipPlayer shooter)
    {
        var sources = BattleshipGameEngine.GetUsableWeapons(game, shooter, WeaponType.Ballista)
            .Where(x => x.ship.Range == RangeClass.Mid ||
                        (game.Phase == BsGamePhase.Boarding && x.ship.Range == RangeClass.Close))
            .OrderBy(x => x.ship.Row)
            .ThenBy(x => x.ship.Col)
            .ThenBy(x => x.ship.Id, StringComparer.Ordinal)
            .ToList();
        if (sources.Count == 0) return null;
        var index = Math.Abs(shooter.NextBallistaAnimationIndex) % sources.Count;
        shooter.NextBallistaAnimationIndex = (index + 1) % sources.Count;
        return sources[index];
    }

    private static void ApplyComboShotDelay(BattleshipPlayer shooter, ShotResult result)
    {
        shooter.NextShotAllowedAtUtc = result is { Hit: true, TurnContinues: true }
            ? DateTime.UtcNow.Add(ComboHitDelay)
            : DateTime.MinValue;
    }

    private static string GetShotDelayError(BattleshipPlayer shooter)
    {
        var remaining = shooter.NextShotAllowedAtUtc - DateTime.UtcNow;
        return remaining > TimeSpan.Zero
            ? $"После попадания следующий выстрел будет доступен через {Math.Ceiling(remaining.TotalSeconds)} сек."
            : null;
    }

    private static bool HasPendingBoardingDeployments(BattleshipGame game) =>
        game.GetPlayers().Any(p => p.PendingSummons.Any(s => s.IsBoarding));

    private void CompleteActionResolution(BattleshipGame game, bool turnContinues, bool moveSummons)
    {
        CheckAndApplyFleetDestructionWin(game);
        TryTriggerBoarding(game);
        CheckAndApplyWin(game);
        if (!game.IsFinished && HasPendingBoardingDeployments(game))
        {
            game.BoardingResolutionPaused = true;
            game.PausedTurnContinues = turnContinues;
            game.PausedMoveSummons = moveSummons;
            return;
        }
        if (!game.IsFinished && moveSummons)
            BattleshipGameEngine.MoveSummons(game);
        CheckAndApplyFleetDestructionWin(game);
        TryTriggerBoarding(game);
        CheckAndApplyWin(game);
        if (!game.IsFinished && !turnContinues)
        {
            SwitchTurn(game);
            game.TurnNumber++;
        }
    }

    private void ResumeBoardingResolution(BattleshipGame game)
    {
        if (!game.BoardingResolutionPaused || HasPendingBoardingDeployments(game) || game.IsFinished) return;
        var turnContinues = game.PausedTurnContinues;
        var moveSummons = game.PausedMoveSummons;
        game.BoardingResolutionPaused = false;
        game.PausedTurnContinues = false;
        game.PausedMoveSummons = false;

        if (moveSummons)
            BattleshipGameEngine.MoveSummons(game);
        CheckAndApplyFleetDestructionWin(game);
        CheckAndApplyWin(game);
        if (!game.IsFinished && !turnContinues)
        {
            SwitchTurn(game);
            game.TurnNumber++;
        }
    }

    private void CheckAndApplyFleetDestructionWin(BattleshipGame game)
    {
        var (gameOver, winnerId) = BattleshipGameEngine.CheckFleetDestructionWin(game);
        if (!gameOver) return;
        game.IsFinished = true;
        game.WinnerId = winnerId;
        game.Phase = BsGamePhase.GameOver;
        game.AddLog($"Победитель: {game.GetPlayer(winnerId)?.Username ?? "???"}!");
    }

    private static void TryTriggerBoarding(BattleshipGame game)
    {
        if (!game.IsFinished && BattleshipGameEngine.CheckBoardingTrigger(game))
            BattleshipGameEngine.TriggerBoarding(game);
    }

    public (bool success, string error) PassBoardingTurn(string gameId, string discordId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return (false, "Игра не найдена.");
        lock (game)
        {
            if (game.Phase != BsGamePhase.Boarding) return (false, "Пропуск доступен только в финальном абордаже.");
            if (game.CurrentTurnPlayerId != discordId) return (false, "Сейчас не ваш ход.");
            var player = game.GetPlayer(discordId);
            if (player == null) return (false, "Вы не в этой игре.");
            if (HasPendingBoardingDeployments(game)) return (false, "Разместите все абордажные корабли!");
            if (BattleshipGameEngine.HasAnyLegalShot(game, player)) return (false, "У вас есть доступный выстрел.");

            var skipped = BattleshipGameEngine.ProcessTurnStart(game, player);
            CompleteActionResolution(game, turnContinues: false, moveSummons: !skipped);
            game.LastActivity = DateTime.UtcNow;
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    /// <summary>
    /// Map WeaponType to the default ShotType for combat resolution.
    /// Tetracatapult defaults to White Stone; Buckshot is selected as its alternative projectile.
    /// </summary>
    private static ShotType WeaponTypeToShotType(WeaponType wt)
    {
        return wt switch
        {
            WeaponType.Tetracatapult => ShotType.WhiteStone,
            WeaponType.Incendiary => ShotType.Incendiary,
            WeaponType.GreekFire => ShotType.GreekFire,
            _ => ShotType.Ballista,
        };
    }

    public (bool success, string error) SelectWeapon(
        string gameId,
        string discordId,
        string weaponType,
        string shotType,
        string weaponId = null)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");
            if (game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding))
                return (false, "Сейчас нельзя выбирать оружие.");

            if (!Enum.TryParse<WeaponType>(weaponType, true, out var wt) ||
                wt is WeaponType.Mast or WeaponType.Boiler)
                return (false, "Неизвестное оружие.");

            var selectedShotType = WeaponTypeToShotType(wt);
            if (wt == WeaponType.Tetracatapult)
            {
                if (!Enum.TryParse<ShotType>(shotType, true, out var requested) ||
                    requested is not (ShotType.WhiteStone or ShotType.Buckshot))
                    return (false, "Неверный тип снаряда.");
                selectedShotType = requested;
            }

            if (player.Board.PlacedShips.Any(s =>
                    !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture)) && wt != WeaponType.Ballista)
                return (false, "Сначала уничтожьте захваченный корабль Баллистой.");

            // Ballista remains the baseline action; every special must resolve to a real,
            // living, loaded weapon. This closes forged Greek Fire/Incendiary selections.
            var usable = BattleshipGameEngine.GetUsableWeapons(game, player, wt).ToList();
            // Ballista is a shared baseline action; an exact source id is meaningful only
            // for special weapons. The visual source is selected when the shot resolves.
            var selectedWeapon = wt == WeaponType.Ballista || weaponId == null
                ? usable.Select(x => x.weapon).FirstOrDefault()
                : usable.Select(x => x.weapon).FirstOrDefault(w => w.Id == weaponId);
            if (selectedWeapon == null)
                return (false, "Это оружие уничтожено или у него закончились боеприпасы.");
            if (selectedWeapon.AimSpeed > player.RevealedCellCount)
                return (false, $"Оружие ещё заряжается! Нужно разведать {selectedWeapon.AimSpeed - player.RevealedCellCount} клеток.");

            player.SelectedShotType = selectedShotType;
            player.SelectedWeapon = selectedWeapon;
            return (true, null);
        }
    }

    public (bool success, string error) DeploySummon(string gameId, string discordId, string summonTypeStr, int col)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.Combat && game.Phase != BsGamePhase.Boarding)
                return (false, "Сейчас не фаза боя.");
            if (HasPendingBoardingDeployments(game))
                return (false, "Сначала разместите все абордажные корабли.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            if (!Enum.TryParse<SummonType>(summonTypeStr, true, out var summonType))
                return (false, "Неизвестный тип призыва.");

            var waitingSummon = player.Summons.FirstOrDefault(s =>
                s.WaitingForTurnBack && s.IsAlive && s.Type == SummonType.Ram &&
                !s.IsBoardingShip && s.Type == summonType);

            // ТЗ #10: Brander is outside the four normal uses (its own cap is below)
            if (waitingSummon == null && summonType != SummonType.Brander &&
                player.SummonSlotsUsed >= player.MaxSummonSlots)
                return (false, "Лимит обычных призывов исчерпан.");

            // Brander requires the boiler upgrade on Tetranavis
            if (waitingSummon == null && summonType == SummonType.Brander &&
                !player.Board.PlacedShips.Any(s => !s.IsDestroyed &&
                    !s.Statuses.Contains(ShipStatusType.Capture) && s.Abilities.Contains("brander_summon")))
                return (false, "Для призыва Брандера нужен апгрейд Котельной.");

            // Region check: Ram requires West, Scout requires East, PirateBoat requires South
            var playerRegions = player.Board.PlacedShips
                .Where(s => !s.Statuses.Contains(ShipStatusType.Capture))
                .SelectMany(s => s.Regions).Distinct().ToHashSet();
            if (waitingSummon == null && summonType == SummonType.Ram && !playerRegions.Contains(Region.West))
                return (false, "Для призыва Тарана нужен флот из региона Запад.");
            if (waitingSummon == null && summonType == SummonType.Scout && !playerRegions.Contains(Region.East))
                return (false, "Для призыва Разведчика нужен флот из региона Восток.");
            if (waitingSummon == null && summonType == SummonType.PirateBoat && !playerRegions.Contains(Region.South))
                return (false, "Для призыва Пиратской лодки нужен флот из региона Юг.");

            if (col < 0 || col >= 10)
                return (false, "Неверная колонка.");

            var opponent = game.GetOpponent(discordId);

            // Deployment threshold: need 5 revealed cells per summon index
            var summonIndex = player.SummonSlotsUsed;
            if (waitingSummon == null &&
                player.RevealedCellCount < 5 * (summonIndex + 1) && game.Phase != BsGamePhase.Boarding)
                return (false, $"Нужно разведать ещё {5 * (summonIndex + 1) - player.RevealedCellCount} клеток для призыва.");

            // Deployment cooldown: 2 shots between deployments
            if (game.ShotCount - player.LastSummonDeployShotCount < 2 && game.Phase != BsGamePhase.Boarding)
                return (false, "Слишком рано для нового призыва (перезарядка 2 выстрела).");

            // Re-send waiting summon (turn-back)
            if (waitingSummon != null)
            {
                var isHorizontal = waitingSummon.MoveDirection is Direction.Left or Direction.Right;

                if (isHorizontal)
                {
                    // For horizontal CursedBoat: 'col' param = row to enter at, must be adjacent to current row
                    if (Math.Abs(waitingSummon.Row - col) > 1)
                        return (false, "Можно отправить только в соседний ряд.");

                    waitingSummon.WaitingForTurnBack = false;
                    waitingSummon.Row = col;
                    waitingSummon.Col = waitingSummon.MoveDirection == Direction.Right ? 9 : 0;
                    waitingSummon.MoveDirection = waitingSummon.MoveDirection == Direction.Right ? Direction.Left : Direction.Right;
                }
                else
                {
                    // For vertical movement: 'col' param = column to enter at, must be adjacent
                    if (Math.Abs(waitingSummon.Col - col) > 1)
                        return (false, "Можно отправить только в соседнюю колонку.");

                    waitingSummon.WaitingForTurnBack = false;
                    waitingSummon.Row = waitingSummon.MoveDirection == Direction.Down ? 9 : 0;
                    waitingSummon.Col = col;
                    waitingSummon.MoveDirection = waitingSummon.MoveDirection == Direction.Down ? Direction.Up : Direction.Down;
                }
                var reentryCell = opponent?.Board.GetCell(waitingSummon.Row, waitingSummon.Col);
                if (reentryCell?.SummonRef is { IsAlive: true })
                {
                    waitingSummon.WaitingForTurnBack = true;
                    return (false, "Клетка входа занята другим призывом.");
                }
                BattleshipGameEngine.RegisterSummonOnTargetBoard(game, player, waitingSummon);

                player.LastSummonDeployShotCount = game.ShotCount;
                game.LastActivity = DateTime.UtcNow;
                game.AddLog($"{player.Username} перенаправил {summonType}!");

                // Mast warning (personal — it's their mast)
                if (opponent != null)
                {
                    var warning = BattleshipGameEngine.GenerateMastWarning(
                        opponent, summonType, waitingSummon.Row, waitingSummon.Col);
                    if (warning != null) game.AddLogFor(opponent.DiscordId, warning);
                }

                TrySettleGameEnd(game);
                return (true, null);
            }

            // ТЗ #10: Brander — максимум 1 за матч (redirect of a waiting one is handled above)
            if (summonType == SummonType.Brander && player.BranderUsed)
                return (false, "Брандер уже был использован в этом матче.");

            // Cursed boats only come from their ship death. Preserve re-entry above, but do
            // not let a forged SignalR enum value create a fresh one.
            if (summonType == SummonType.CursedBoat)
                return (false, "Проклятую лодку можно выпустить только после гибели её корабля.");
            if (opponent?.Board.GetCell(0, col)?.SummonRef is { IsAlive: true })
                return (false, "Клетка входа занята другим призывом.");

            var summon = new Summon
            {
                Type = summonType,
                Row = 0,
                Col = col,
                OwnerId = discordId,
                MoveDirection = Direction.Down,
                SpawnedAtShot = game.ShotCount,
            };

            switch (summonType)
            {
                case SummonType.Ram:
                    summon.Speed = 2;
                    summon.CollisionDamage = 4;
                    break;
                case SummonType.PirateBoat:
                    summon.Speed = 1;
                    break;
                case SummonType.Scout:
                    summon.Speed = 1;
                    summon.RevealRadius = 1;
                    break;
                case SummonType.Brander:
                    summon.Speed = 1;
                    break;
                case SummonType.CursedBoat:
                    summon.Speed = 1;
                    summon.CollisionDamage = 999;
                    break;
            }

            player.Summons.Add(summon);
            if (summonType == SummonType.Brander)
                player.BranderUsed = true; // ТЗ #10: вне лимита обычных призывов, 1 раз за матч
            else
                player.SummonSlotsUsed++;
            player.LastSummonDeployShotCount = game.ShotCount;
            game.LastActivity = DateTime.UtcNow;

            BattleshipGameEngine.RegisterSummonOnTargetBoard(game, player, summon);

            game.AddLog($"{player.Username} развернул {summonType}! ({(char)('A' + col)}1)");

            // Mast warning for opponent (ТЗ #3: include spawn cell; personal — it's their mast)
            if (opponent != null)
            {
                var warning = BattleshipGameEngine.GenerateMastWarning(opponent, summonType, summon.Row, summon.Col);
                if (warning != null) game.AddLogFor(opponent.DiscordId, warning);
            }

            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    /// <summary>
    /// Deploy a pending summon (pirate/cursed boat from ship death, or boarding ship).
    /// Free, no cooldown, no revelation threshold. Column restricted for pirate/cursed.
    /// </summary>
    public (bool success, string error) DeployPendingSummon(string gameId, string discordId, string pendingId, int col)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.Combat && game.Phase != BsGamePhase.Boarding)
                return (false, "Сейчас не фаза боя.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            var pending = player.PendingSummons.FirstOrDefault(p => p.Id == pendingId);
            if (pending == null)
                return (false, "Нет такого ожидающего призыва.");
            if (HasPendingBoardingDeployments(game) && !pending.IsBoarding)
                return (false, "Сначала разместите все абордажные корабли.");

            if (col < 0 || col >= 10)
                return (false, "Неверная колонка.");

            // Column restriction for pirate/cursed boat
            if (pending.AllowedColumns.Count > 0 && !pending.AllowedColumns.Contains(col))
                return (false, $"Можно разместить только в колонках: {string.Join(", ", pending.AllowedColumns.Select(c => (char)('A' + c)))}");

            if (!pending.IsFree && player.SummonSlotsUsed >= player.MaxSummonSlots)
                return (false, "Лимит обычных призывов исчерпан.");

            var opponent = game.GetOpponent(discordId);
            if (opponent?.Board.GetCell(0, col)?.SummonRef is { IsAlive: true })
                return (false, "Клетка входа занята другим призывом.");

            var summon = new Summon
            {
                Type = pending.Type,
                Row = 0,
                Col = col,
                Speed = pending.Speed,
                CollisionDamage = pending.CollisionDamage,
                RevealRadius = pending.RevealRadius,
                OwnerId = discordId,
                MoveDirection = Direction.Down,
                SpawnedAtShot = game.ShotCount,
                IsBoardingShip = pending.IsBoarding
            };

            player.Summons.Add(summon);
            if (!pending.IsFree)
                player.SummonSlotsUsed++;
            player.PendingSummons.Remove(pending);
            game.LastActivity = DateTime.UtcNow;

            BattleshipGameEngine.RegisterSummonOnTargetBoard(game, player, summon);

            game.AddLog($"{player.Username} выпустил {pending.SourceShipName ?? pending.Type.ToString()}! ({(char)('A' + col)}1)");

            // Mast warning (personal — it's their mast)
            if (opponent != null)
            {
                var warning = BattleshipGameEngine.GenerateMastWarning(opponent, pending.Type, summon.Row, summon.Col);
                if (warning != null) game.AddLogFor(opponent.DiscordId, warning);
            }

            ResumeBoardingResolution(game);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    public (bool success, string error) ManualMoveShip(string gameId, string discordId, string shipId, string directionStr, int distance = 1)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (HasPendingBoardingDeployments(game))
                return (false, "Сначала разместите все абордажные корабли.");
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            // Only at start of own turn, before shooting
            if (game.CurrentTurnPlayerId != discordId)
                return (false, "Можно двигаться только в свой ход.");

            if (player.HasShotThisTurn)
                return (false, "Маневр возможен только в начале хода.");

            var ship = player.Board.PlacedShips.Find(s => s.Id == shipId);
            if (ship == null || ship.IsDestroyed)
                return (false, "Корабль не найден или уничтожен.");

            if (!ship.Abilities.Contains("manual_move_after_hit"))
                return (false, "Этот корабль не может двигаться.");

            // Requires at least one deck destroyed
            if (!ship.Decks.Any(d => d.IsDestroyed))
                return (false, "Корабль не был повреждён. Маневр невозможен.");

            // ТЗ #21: one-time use PER SHIP — a second Maneuvering Double can still move
            if (ship.HasManeuvered)
                return (false, "Этот корабль уже использовал манёвр.");

            if (distance < 1 || distance > 2)
                return (false, "Можно переместиться на 1 или 2 клетки.");

            if (!Enum.TryParse<Direction>(directionStr, true, out var direction))
                return (false, "Неверное направление.");

            var success = BattleshipGameEngine.ManualMoveShip(player, ship, direction, distance);
            if (!success)
                return (false, "Невозможно переместить корабль в этом направлении.");

            ship.HasManeuvered = true;
            game.LastActivity = DateTime.UtcNow;
            // ТЗ #20: only the mover sees the move message; the opponent gets their mast warning
            // at hit time (ProcessShipHit), not at move time
            game.AddLogFor(discordId, "Маневрирующая двойка маневрирует!");

            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    public (bool success, string error) SetCursedBoatDirection(string gameId, string discordId, string summonId, string directionStr)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (HasPendingBoardingDeployments(game))
                return (false, "Сначала разместите все абордажные корабли.");
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            if (!Enum.TryParse<Direction>(directionStr, true, out var direction))
                return (false, "Неверное направление.");

            var success = BattleshipGameEngine.SetCursedBoatDirection(player, summonId, direction);
            if (!success)
                return (false, "Проклятый корабль не ожидает выбора направления.");

            game.LastActivity = DateTime.UtcNow;
            game.AddLog($"Проклятый корабль меняет курс!");
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    // ── Confirm Ready (advances phase) ───────────────────────────────

    public (bool success, string error) ConfirmReady(string gameId, string discordId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.Lobby)
                return (false, "Подтверждение готовности доступно только в лобби.");
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            player.IsReady = true;
            game.LastActivity = DateTime.UtcNow;

            CheckPhaseTransition(game);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    // ── State Retrieval ──────────────────────────────────────────────

    public BattleshipGameStateDto GetGameState(string gameId, string discordId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return null;

        lock (game)
        {
            return ToDto(game, discordId);
        }
    }

    public BattleshipGameStateDto GetSpectatorState(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return null;

        lock (game)
        {
            return ToDto(game, null);
        }
    }

    public bool HasActiveGame(string gameId)
    {
        return _games.ContainsKey(gameId) && !_games[gameId].IsFinished;
    }

    public List<string> GetPlayerIds(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return new();
        return game.GetPlayers().Where(p => !p.IsBot).Select(p => p.DiscordId).ToList();
    }

    // ── Ship Catalog (for frontend) ──────────────────────────────────

    public List<ShipCatalogDto> GetShipCatalog()
    {
        return ShipCatalog.AllShips.Select(def => new ShipCatalogDto
        {
            Id = def.Id,
            Name = def.Name,
            NameRu = def.NameRu,
            DeckCount = def.DeckCount,
            Range = def.Range.ToString(),
            Cost = def.Cost,
            DefaultArmor = def.DefaultArmor,
            DeckHpOverrides = def.DeckHpOverrides,
            Space = def.Space,
            Speed = def.Speed,
            IsFree = def.IsFree,
            Abilities = def.Abilities,
            Description = def.IsFree
                ? $"{def.Description} Бесплатно."
                : $"{def.Description} Цена: {def.Cost} монет.",
            Region = def.Regions.Any(r => r != Region.Tetracor)
                ? def.Regions.First(r => r != Region.Tetracor).ToString()
                : null,
            Regions = def.Regions.Where(r => r != Region.Tetracor).Select(r => r.ToString()).ToList(),
            AvailableUpgrades = def.AvailableUpgrades?.Where(u => u.Id != "tetra_discus").Select(u => new UpgradeDto
            {
                Id = u.Id,
                Name = u.Name,
                NameRu = u.NameRu,
                Cost = u.Cost,
                Description = $"{u.Description} Цена: {u.Cost} монет.",
            }).ToList() ?? new(),
        }).ToList();
    }

    // ── Internal Logic ───────────────────────────────────────────────

    private void CheckPhaseTransition(BattleshipGame game)
    {
        var p1Ready = game.Player1?.IsReady ?? false;
        var p2Ready = game.Player2?.IsReady ?? false;
        var p2IsBot = game.Player2?.IsBot ?? false;

        // Bot is always ready
        if (p2IsBot) p2Ready = true;

        if (!p1Ready || !p2Ready) return;

        // Both ready — advance phase
        switch (game.Phase)
        {
            case BsGamePhase.Lobby:
                game.Phase = BsGamePhase.ArmySelection;
                ResetReady(game);
                // Bot auto-selects army
                if (game.Player2?.IsBot == true)
                {
                    game.Player2.Faction = Faction.Empire;
                    game.Player2.IsReady = true;
                }
                break;

            case BsGamePhase.ArmySelection:
                game.Phase = BsGamePhase.FleetBuilding;
                ResetReady(game);
                HandleBotFleetSelection(game);
                break;

            case BsGamePhase.FleetBuilding:
                game.Phase = BsGamePhase.ShipPlacement;
                ResetReady(game);
                HandleBotPlacement(game);
                break;

            case BsGamePhase.ShipPlacement:
                foreach (var player in game.GetPlayers())
                {
                    var (valid, error) = PlacementValidator.ValidateAllPlaced(player.Fleet, player.Board);
                    if (valid) continue;
                    player.IsReady = false;
                    game.AddLogFor(player.DiscordId, $"Размещение нужно исправить: {error}");
                    return;
                }
                game.Phase = BsGamePhase.Combat;
                game.CombatStarted = true; // from here on, leaving/forfeiting counts as a loss
                game.TurnNumber = 1;
                game.CurrentTurnPlayerId = BattleshipGameEngine.DetermineFirstTurn(game);
                game.AddLog($"Бой начинается! Первый ход: {game.GetPlayer(game.CurrentTurnPlayerId)?.Username}");
                TryTriggerBoarding(game);

                break;
        }
    }

    private void HandleBotFleetSelection(BattleshipGame game)
    {
        if (game.Player2?.IsBot != true) return;

        var botFleet = BattleshipBotAI.SelectFleet();
        game.Player2.SelectedShips = botFleet;
        game.Player2.CoinsRemaining = FleetValidator.MaxBudget - FleetValidator.CalculateTotalCost(botFleet);
        game.Player2.Fleet.Clear();

        foreach (var sel in botFleet)
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def != null)
                game.Player2.Fleet.Add(ShipCatalog.CreateShip(def, sel.Upgrades));
        }
        NumberDuplicateShips(game.Player2.Fleet);

        game.Player2.IsReady = true;
    }

    private static void NumberDuplicateShips(List<Ship> fleet)
    {
        foreach (var group in fleet.GroupBy(s => s.DefinitionId).Where(g => g.Count() > 1))
        {
            var index = 1;
            foreach (var ship in group)
                ship.Name = $"{ship.Name} {index++}";
        }
    }

    private void HandleBotPlacement(BattleshipGame game)
    {
        if (game.Player2?.IsBot != true) return;

        BattleshipBotAI.PlaceFleet(game.Player2);
        game.Player2.IsReady = true;
    }

    public bool IsBotTurn(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        lock (game)
            return !game.IsFinished &&
                   (game.GetPlayer(game.CurrentTurnPlayerId)?.IsBot == true ||
                    game.GetPlayers().Any(p => p.IsBot && p.PendingSummons.Any(s => s.IsBoarding)));
    }

    /// <summary>
    /// Resolve at most one bot shot/pass. The hub calls this through a delayed pump so the
    /// game lock is released between reset shots and the human can deploy summons meanwhile.
    /// </summary>
    public BattleshipBotStepResult ProcessBotStep(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return new();
        lock (game)
        {
            var forcedBoardingBot = game.GetPlayers()
                .FirstOrDefault(p => p.IsBot && p.PendingSummons.Any(s => s.IsBoarding));
            if (forcedBoardingBot != null)
            {
                var opponentForPlacement = game.GetOpponent(forcedBoardingBot.DiscordId);
                if (opponentForPlacement == null) return new();
                var boardingIds = forcedBoardingBot.PendingSummons
                    .Where(s => s.IsBoarding)
                    .Select(s => s.Id)
                    .ToHashSet();
                var before = boardingIds.Count;
                foreach (var (pendingId, pendingCol) in
                         BattleshipBotAI.ChoosePendingSummonDeploys(forcedBoardingBot, opponentForPlacement)
                             .Where(x => boardingIds.Contains(x.pendingId)))
                    DeployPendingSummon(game.GameId, forcedBoardingBot.DiscordId, pendingId, pendingCol);
                var after = forcedBoardingBot.PendingSummons.Count(s => s.IsBoarding);
                game.LastActivity = DateTime.UtcNow;
                return new BattleshipBotStepResult { Acted = after < before };
            }

            // A human mandatory deployment freezes the bot too. The hub will restart the
            // pump after the human places the final boarding ship.
            if (HasPendingBoardingDeployments(game)) return new();

            var bot = game.GetPlayer(game.CurrentTurnPlayerId);
            if (bot == null || !bot.IsBot || game.IsFinished) return new();
            var opponent = game.GetOpponent(bot.DiscordId);
            if (opponent == null) return new();

            if (game.BotPreparedTurnNumber != game.TurnNumber)
            {
                game.BotPreparedTurnNumber = game.TurnNumber;
                if (BattleshipGameEngine.ProcessTurnStart(game, bot))
                {
                    SwitchTurn(game);
                    game.TurnNumber++;
                    game.LastActivity = DateTime.UtcNow;
                    TrySettleGameEnd(game);
                    return new BattleshipBotStepResult
                    {
                        Acted = true,
                        Shot = new ShotResult { WasSkipped = true, TurnContinues = false, Message = "Ход пропущен!" },
                    };
                }

                TryBotManeuvers(game, bot);
                foreach (var (pendingId, pendingCol) in BattleshipBotAI.ChoosePendingSummonDeploys(bot, opponent))
                {
                    DeployPendingSummon(game.GameId, bot.DiscordId, pendingId, pendingCol);
                    if (game.IsFinished) return new BattleshipBotStepResult { Acted = true };
                }
                foreach (var summon in bot.Summons.Where(s => s.IsAlive && s.WaitingForDirectionChoice).ToList())
                {
                    var direction = BattleshipBotAI.ChooseCursedBoatDirection(summon, opponent);
                    BattleshipGameEngine.SetCursedBoatDirection(bot, summon.Id, direction);
                }
                var openingSummon = BattleshipBotAI.ChooseSummonDeploy(game, bot);
                if (openingSummon != null)
                {
                    var (summonType, summonCol) = openingSummon.Value;
                    DeploySummon(game.GameId, bot.DiscordId, summonType.ToString(), summonCol);
                }
            }

            if (game.Phase == BsGamePhase.Boarding && !BattleshipGameEngine.HasAnyLegalShot(game, bot))
            {
                CompleteActionResolution(game, turnContinues: false, moveSummons: true);
                game.LastActivity = DateTime.UtcNow;
                TrySettleGameEnd(game);
                return new BattleshipBotStepResult { Acted = true };
            }

            var (weaponType, shotType) = BattleshipBotAI.ChooseWeapon(bot, opponent, game.Phase);
            var captured = bot.Board.PlacedShips.Any(s =>
                !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture));
            var enemySummon = captured ? null : opponent.Summons.FirstOrDefault(s =>
                s.IsAlive && bot.Board.GetCell(s.Row, s.Col)?.SummonRef == s);
            if (enemySummon != null && weaponType != WeaponType.GreekFire.ToString())
                (weaponType, shotType) = ("Ballista", "Ballista");

            var (selected, _) = SelectWeapon(game.GameId, bot.DiscordId, weaponType, shotType);
            if (!selected)
                (selected, _) = SelectWeapon(game.GameId, bot.DiscordId, "Ballista", "Ballista");
            if (!selected)
            {
                CheckAndApplyWin(game);
                if (!game.IsFinished) CompleteActionResolution(game, turnContinues: false, moveSummons: true);
                TrySettleGameEnd(game);
                return new BattleshipBotStepResult { Acted = true };
            }

            var (targetRow, targetCol) = enemySummon != null
                ? (enemySummon.Row, enemySummon.Col)
                : BattleshipBotAI.ChooseTarget(bot, opponent, bot.SelectedShotType);
            var ownBoard = captured || enemySummon != null;

            ShotResult result;
            if (enemySummon != null && bot.SelectedShotType == ShotType.GreekFire)
            {
                bot.SelectedWeapon.UseAmmo();
                result = BattleshipGameEngine.ProcessOwnBoardGreekFireShot(game, bot, targetRow, targetCol);
            }
            else if (enemySummon != null)
                result = BattleshipGameEngine.ProcessOwnBoardShot(game, bot, targetRow, targetCol);
            else if (bot.SelectedShotType == ShotType.Buckshot)
                result = BattleshipGameEngine.ProcessBuckshotShot(game, bot, targetRow, targetCol);
            else
                result = BattleshipGameEngine.ProcessShot(game, bot, targetRow, targetCol);

            ApplyComboShotDelay(bot, result);
            DescribeShot(game, bot, result, ownBoard);
            bot.HasShotThisTurn = true;
            ResetExpendedSelection(bot);
            CompleteActionResolution(game, result.TurnContinues, moveSummons: true);

            if (!game.IsFinished && game.CurrentTurnPlayerId == bot.DiscordId)
            {
                foreach (var (pendingId, pendingCol) in BattleshipBotAI.ChoosePendingSummonDeploys(bot, opponent))
                    DeployPendingSummon(game.GameId, bot.DiscordId, pendingId, pendingCol);
                if (result.TurnContinues)
                {
                    var midSummon = BattleshipBotAI.ChooseSummonDeploy(game, bot);
                    if (midSummon != null)
                    {
                        var (summonType, summonCol) = midSummon.Value;
                        DeploySummon(game.GameId, bot.DiscordId, summonType.ToString(), summonCol);
                    }
                }
            }

            game.LastActivity = DateTime.UtcNow;
            TrySettleGameEnd(game);
            return new BattleshipBotStepResult { Acted = true, Shot = result };
        }
    }

    private static void TryBotManeuvers(BattleshipGame game, BattleshipPlayer bot)
    {
        foreach (var ship in bot.Board.PlacedShips.Where(s =>
                     !s.IsDestroyed && !s.HasManeuvered && s.Abilities.Contains("manual_move_after_hit") &&
                     s.Decks.Any(d => d.IsDestroyed)).ToList())
        {
            var directions = ship.Orientation == Orientation.Horizontal
                ? new[] { Direction.Left, Direction.Right }
                : new[] { Direction.Up, Direction.Down };
            foreach (var direction in directions.OrderBy(_ => Rng.Next()))
            foreach (var distance in new[] { 2, 1 })
            {
                if (!BattleshipGameEngine.ManualMoveShip(bot, ship, direction, distance)) continue;
                ship.HasManeuvered = true;
                game.AddLogFor(bot.DiscordId, "Маневрирующая двойка маневрирует!");
                goto NextShip;
            }

            NextShip: ;
        }
    }

    private static void SwitchTurn(BattleshipGame game)
    {
        if (game.Player1 == null || game.Player2 == null) return;
        // Reset shot flag for the player whose turn is ending
        var current = game.GetPlayer(game.CurrentTurnPlayerId);
        if (current != null) current.HasShotThisTurn = false;
        game.CurrentTurnPlayerId = game.CurrentTurnPlayerId == game.Player1.DiscordId
            ? game.Player2.DiscordId
            : game.Player1.DiscordId;
    }

    private static void ResetReady(BattleshipGame game)
    {
        if (game.Player1 != null) game.Player1.IsReady = false;
        if (game.Player2 != null) game.Player2.IsReady = false;
    }

    private static void RemoveShipFromBoard(BattleshipPlayer player, Ship ship)
    {
        if (!ship.IsPlaced) return;

        var cells = ship.GetOccupiedCells();
        foreach (var (r, c) in cells)
        {
            if (r >= 0 && r < 10 && c >= 0 && c < 10)
                player.Board.Grid[r, c].ShipRef = null;
        }
        player.Board.PlacedShips.Remove(ship);
        ship.IsPlaced = false;
    }

    private static void PlaceShipOnBoard(
        BattleshipPlayer player,
        Ship ship,
        int row,
        int col,
        Orientation orientation)
    {
        ship.Row = row;
        ship.Col = col;
        ship.Orientation = orientation;
        ship.IsPlaced = true;
        foreach (var (r, c) in ship.GetOccupiedCells())
            player.Board.Grid[r, c].ShipRef = ship;
        if (!player.Board.PlacedShips.Contains(ship))
            player.Board.PlacedShips.Add(ship);
    }

    private void CleanupStaleGames()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);
        foreach (var kvp in _games)
        {
            if (kvp.Value.LastActivity < cutoff)
            {
                _games.TryRemove(kvp.Key, out _);
                Console.WriteLine($"[Battleship] Cleaned up stale game {kvp.Key}");
            }
        }
    }

    // ── DTO Mapping ──────────────────────────────────────────────────

    private BattleshipGameStateDto ToDto(BattleshipGame game, string requestingDiscordId)
    {
        var isSpectator = requestingDiscordId == null;
        var isPlayer1 = game.Player1?.DiscordId == requestingDiscordId;
        var isPlayer2 = game.Player2?.DiscordId == requestingDiscordId;

        return new BattleshipGameStateDto
        {
            GameId = game.GameId,
            Phase = game.Phase.ToString(),
            TurnNumber = game.TurnNumber,
            ShotCount = game.ShotCount,
            IsFinished = game.IsFinished,
            WinnerId = game.WinnerId,
            CurrentTurnPlayerId = game.CurrentTurnPlayerId,
            IsMyTurn = game.CurrentTurnPlayerId == requestingDiscordId,
            MyPlayerId = requestingDiscordId,
            GameLog = game.GameLog
                .Where(e => isSpectator || e.VisibleTo == null || e.VisibleTo == requestingDiscordId)
                .Select(e => e.Text)
                .TakeLast(50).ToList(),
            Player1 = MapPlayer(game, game.Player1, requestingDiscordId, isPlayer1 || isSpectator, isSpectator, game.ShotCount),
            Player2 = MapPlayer(game, game.Player2, requestingDiscordId, isPlayer2 || isSpectator, isSpectator, game.ShotCount),
            ShipCatalog = game.Phase == BsGamePhase.FleetBuilding ? GetShipCatalog() : null,
            MyEndReward = requestingDiscordId != null && game.EndRewards.TryGetValue(requestingDiscordId, out var reward)
                ? reward
                : null,
        };
    }

    private static BattleshipPlayerDto MapPlayer(
        BattleshipGame game,
        BattleshipPlayer player,
        string requestingId,
        bool showOwnBoard,
        bool isSpectator,
        int gameShotCount)
    {
        if (player == null) return null;
        var isMe = player.DiscordId == requestingId;

        return new BattleshipPlayerDto
        {
            DiscordId = player.DiscordId,
            Username = player.Username,
            IsBot = player.IsBot,
            IsMe = isMe,
            Faction = player.Faction.ToString(),
            CoinsRemaining = player.CoinsRemaining,
            IsReady = player.IsReady,
            SummonSlotsUsed = player.SummonSlotsUsed,
            MaxSummonSlots = player.MaxSummonSlots,
            BranderUsed = player.BranderUsed,
            SelectedShotType = player.SelectedShotType.ToString(),
            SelectedWeaponId = isMe ? player.SelectedWeapon?.Id : null,
            RevealedCellCount = player.RevealedCellCount,
            StunShotExpiry = player.StunShotExpiry,
            HasPenalty = player.HasPenalty,
            HasShotThisTurn = player.HasShotThisTurn,
            HasPendingBoardingDeployment = player.PendingSummons.Any(p => p.IsBoarding),
            ShotDelayRemainingMs = Math.Max(0,
                (int)Math.Ceiling((player.NextShotAllowedAtUtc - DateTime.UtcNow).TotalMilliseconds)),
            SummonCooldownRemaining = Math.Max(0, 2 - (gameShotCount - player.LastSummonDeployShotCount)),
            Fleet = isMe || isSpectator ? MapFleet(player.Fleet, player.RevealedCellCount) : null,
            Board = showOwnBoard ? MapBoard(player.Board, isMe || isSpectator) : MapFogBoard(player.Board),
            Summons = player.Summons.Where(s => s.IsAlive).Select(s => new SummonDto
            {
                Id = s.Id,
                Type = s.Type.ToString(),
                Row = s.Row,
                Col = s.Col,
                Speed = s.Speed,
                IsAlive = s.IsAlive,
                MoveDirection = s.MoveDirection.ToString(),
                WaitingForTurnBack = s.WaitingForTurnBack,
                WaitingForDirectionChoice = s.WaitingForDirectionChoice,
                IsBoardingShip = s.IsBoardingShip,
            }).ToList(),
            PendingSummons = isMe ? player.PendingSummons.Select(p => new PendingSummonDto
            {
                Id = p.Id,
                Type = p.Type.ToString(),
                AllowedColumns = p.AllowedColumns,
                IsBoarding = p.IsBoarding,
                SourceShipName = p.SourceShipName,
            }).ToList() : new(),
            SelectedShips = isMe || isSpectator ? player.SelectedShips?.Select(s => new FleetSelectionDto
            {
                DefinitionId = s.DefinitionId,
                ShipName = s.ShipName,
                Cost = s.Cost,
                Upgrades = s.Upgrades,
            }).ToList() : null,
            AvailableWeapons = isMe ? MapAvailableWeapons(game, player) : new(),
            CanPassBoarding = isMe && game.Phase == BsGamePhase.Boarding &&
                game.CurrentTurnPlayerId == player.DiscordId &&
                !HasPendingBoardingDeployments(game) &&
                !BattleshipGameEngine.HasAnyLegalShot(game, player),
        };
    }

    private static List<AvailableWeaponDto> MapAvailableWeapons(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        var captureForcesBallista = player.Board.PlacedShips.Any(s =>
            !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture));
        var usable = BattleshipGameEngine.GetUsableWeapons(game, player)
            .Where(x => !captureForcesBallista || x.weapon.Type == WeaponType.Ballista)
            .ToList();

        // Ballista is one shared baseline action. Its real projectile source is selected
        // separately by the stable animation cycle when the shot resolves.
        var visible = usable.Where(x => x.weapon.Type != WeaponType.Ballista).ToList();
        var ballista = usable.FirstOrDefault(x => x.weapon.Type == WeaponType.Ballista);
        if (ballista.weapon != null) visible.Insert(0, ballista);

        return visible.Select(x => new AvailableWeaponDto
        {
            Id = x.weapon.Id,
            ShipId = x.ship.Id,
            ShipName = x.weapon.Type == WeaponType.Ballista ? "Общий выстрел" : x.ship.Name,
            Type = x.weapon.Type.ToString(),
            Ammo = x.weapon.Ammo,
            DeckIndex = x.weapon.DeckIndex,
            AimRemaining = Math.Max(0, x.weapon.AimSpeed - player.RevealedCellCount),
        }).ToList();
    }

    private static List<ShipDto> MapFleet(List<Ship> fleet, int opponentRevealedCount = 0)
    {
        return fleet?.Select(s => new ShipDto
        {
            Id = s.Id,
            DefinitionId = s.DefinitionId,
            Name = s.Name,
            DeckCount = s.Decks.Count,
            Row = s.Row,
            Col = s.Col,
            Orientation = s.Orientation.ToString(),
            IsDestroyed = s.IsDestroyed,
            IsPlaced = s.IsPlaced,
            IsSummon = s.IsSummon,
            HasManeuvered = s.HasManeuvered,
            Range = s.Range.ToString(),
            Cost = s.Cost,
            Abilities = s.Abilities,
            Upgrades = s.Upgrades,
            Speed = s.Speed,
            Space = s.Space,
            ExplosionRadius = s.ExplosionRadius,
            Regions = s.Regions.Select(r => r.ToString()).ToList(),
            Decks = s.Decks.Select(d => new DeckDto
            {
                Index = d.Index,
                MaxHp = d.MaxHp,
                CurrentHp = d.CurrentHp,
                IsDestroyed = d.IsDestroyed,
                Module = d.Module,
                ModuleDestroyed = d.ModuleDestroyed,
            }).ToList(),
            Weapons = s.Weapons.Select(w => new WeaponDto
            {
                Id = w.Id,
                ShipId = w.ShipId,
                Type = w.Type.ToString(),
                Ammo = w.Ammo,
                DeckIndex = w.DeckIndex,
                HasAmmo = w.HasAmmo,
                AimSpeed = w.AimSpeed > 0 ? Math.Max(0, w.AimSpeed - opponentRevealedCount) : 0,
            }).ToList(),
        }).ToList() ?? new();
    }

    private static BoardDto MapBoard(Board board, bool showShips)
    {
        var cells = new List<CellDto>();
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];
            cells.Add(new CellDto
            {
                Row = r,
                Col = c,
                IsRevealed = true,
                // ТЗ #19: killed decks derive from the live Ship object, so the mark follows a
                // Maneuvering Double to its new cells after a manual move
                IsHit = cell.IsHit || (showShips && IsDeckDestroyedAt(cell)),
                IsMiss = cell.IsMiss,
                IsBurning = cell.IsBurning,
                HasShip = showShips && cell.ShipRef != null,
                ShipId = showShips ? cell.ShipRef?.Id : null,
                HasSummon = cell.SummonRef != null && cell.SummonRef.IsAlive,
                SummonOwnerId = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.OwnerId : null,
                SummonType = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.Type.ToString() : null,
                IsScratched = IsCellScratched(cell),
                SummonTrails = cell.SummonTrails.Select(t => t.ToString()).OrderBy(t => t).ToList(),
                IsBurnResistMarked = cell.BurnResistMarked,
                IsDodgeMarked = cell.WasDodge,
                IsDestroyed = IsDeckDestroyedAt(cell),
                IsShipSunk = cell.ShipRef?.IsDestroyed == true,
                IsFrozen = cell.ShipRef?.Statuses.Contains(ShipStatusType.Freeze) == true,
                IsDevastated = cell.ShipRef?.Statuses.Contains(ShipStatusType.Devastated) == true,
                IsCaptured = cell.ShipRef?.Statuses.Contains(ShipStatusType.Capture) == true,
                IsFirePermanent = cell.IsBurning,
            });
        }
        return new BoardDto { Cells = cells };
    }

    private static BoardDto MapFogBoard(Board board)
    {
        var cells = new List<CellDto>();
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];
            cells.Add(new CellDto
            {
                Row = r,
                Col = c,
                IsRevealed = cell.IsRevealed,
                // WasShipHit implies hit — keeps the snapshot visible after the ship moved away
                // and its live IsHit flag was cleared (ТЗ #19/#22)
                IsHit = cell.IsHit || cell.WasShipHit,
                IsMiss = cell.IsMiss,
                IsBurning = cell.IsBurning,
                HasShip = cell.WasShipHit || (cell.IsRevealed && cell.ShipRef != null),
                ShipId = null,
                HasSummon = cell.SummonRef != null && cell.SummonRef.IsAlive,
                SummonOwnerId = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.OwnerId : null,
                SummonType = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.Type.ToString() : null,
                IsScratched = cell.WasScratched, // Snapshot: scratched state persists after ship moves
                SummonTrails = cell.SummonTrails.Select(t => t.ToString()).OrderBy(t => t).ToList(),
                IsBurnResistMarked = cell.BurnResistMarked,
                IsDodgeMarked = cell.WasDodge,
                IsDestroyed = IsDeckDestroyedAt(cell) || (cell.WasShipHit && !cell.WasScratched),
                IsShipSunk = cell.ShipRef?.IsDestroyed == true && cell.IsRevealed,
                IsFrozen = cell.ShipRef?.Statuses.Contains(ShipStatusType.Freeze) == true,
                IsDevastated = cell.ShipRef?.Statuses.Contains(ShipStatusType.Devastated) == true,
                IsCaptured = cell.ShipRef?.Statuses.Contains(ShipStatusType.Capture) == true,
                IsFirePermanent = cell.IsBurning,
            });
        }
        return new BoardDto { Cells = cells };
    }

    /// <summary>The deck of the ship occupying this cell is destroyed (derived from the live Ship, ТЗ #19).</summary>
    private static bool IsDeckDestroyedAt(Cell cell)
    {
        if (cell.ShipRef == null) return false;
        var ship = cell.ShipRef;
        var cells = ship.GetOccupiedCells();
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].row == cell.Row && cells[i].col == cell.Col)
                return i < ship.Decks.Count && ship.Decks[i].IsDestroyed;
        }
        return false;
    }

    /// <summary>Cell has a ship deck that was hit but not destroyed (scratched).</summary>
    private static bool IsCellScratched(Cell cell)
    {
        if (!cell.IsHit || cell.ShipRef == null) return false;
        var ship = cell.ShipRef;
        var cells = ship.GetOccupiedCells();
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].row == cell.Row && cells[i].col == cell.Col)
                return i < ship.Decks.Count && ship.Decks[i].CurrentHp > 0;
        }
        return false;
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────

public class BattleshipBotStepResult
{
    public bool Acted { get; set; }
    public ShotResult Shot { get; set; }
}

public class BattleshipLobbyDto
{
    public List<BattleshipLobbyGameDto> Games { get; set; } = new();
}

public class BattleshipLobbyGameDto
{
    public string GameId { get; set; }
    public string Phase { get; set; }
    public string Player1Name { get; set; }
    public string Player2Name { get; set; }
    public bool Player1IsBot { get; set; }
    public bool Player2IsBot { get; set; }
    public int TurnNumber { get; set; }
    public string CreatedAt { get; set; }
}

public class BattleshipGameStateDto
{
    public string GameId { get; set; }
    public string Phase { get; set; }
    public int TurnNumber { get; set; }
    public int ShotCount { get; set; }
    public bool IsFinished { get; set; }
    public string WinnerId { get; set; }
    public string CurrentTurnPlayerId { get; set; }
    public bool IsMyTurn { get; set; }
    public string MyPlayerId { get; set; }
    public List<string> GameLog { get; set; } = new();
    public BattleshipPlayerDto Player1 { get; set; }
    public BattleshipPlayerDto Player2 { get; set; }
    public List<ShipCatalogDto> ShipCatalog { get; set; }
    public BattleshipEndReward MyEndReward { get; set; }
}

public class BattleshipStatsDto
{
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int CurrentDailyStreak { get; set; }
    public int BestDailyStreak { get; set; }
    public bool FirstWinAvailable { get; set; }
    public int FirstWinZbs { get; set; }
    public int ZbsBalance { get; set; }
}

public class BattleshipPlayerDto
{
    public string DiscordId { get; set; }
    public string Username { get; set; }
    public bool IsBot { get; set; }
    public bool IsMe { get; set; }
    public string Faction { get; set; }
    public int CoinsRemaining { get; set; }
    public bool IsReady { get; set; }
    public int SummonSlotsUsed { get; set; }
    public int MaxSummonSlots { get; set; }
    public bool BranderUsed { get; set; }
    public string SelectedShotType { get; set; }
    public string SelectedWeaponId { get; set; }
    public int RevealedCellCount { get; set; }
    public int StunShotExpiry { get; set; }
    public bool HasPenalty { get; set; }
    public bool HasShotThisTurn { get; set; }
    public bool HasPendingBoardingDeployment { get; set; }
    public int ShotDelayRemainingMs { get; set; }
    public int SummonCooldownRemaining { get; set; }
    public List<ShipDto> Fleet { get; set; }
    public BoardDto Board { get; set; }
    public List<SummonDto> Summons { get; set; } = new();
    public List<PendingSummonDto> PendingSummons { get; set; } = new();
    public List<FleetSelectionDto> SelectedShips { get; set; }
    public List<AvailableWeaponDto> AvailableWeapons { get; set; } = new();
    public bool CanPassBoarding { get; set; }
}

public class BoardDto
{
    public List<CellDto> Cells { get; set; } = new();
}

public class CellDto
{
    public int Row { get; set; }
    public int Col { get; set; }
    public bool IsRevealed { get; set; }
    public bool IsHit { get; set; }
    public bool IsMiss { get; set; }
    public bool IsBurning { get; set; }
    public bool HasShip { get; set; }
    public string ShipId { get; set; }
    public bool HasSummon { get; set; }
    public string SummonOwnerId { get; set; }
    public string SummonType { get; set; }
    public bool IsScratched { get; set; }
    public List<string> SummonTrails { get; set; } = new();
    public bool IsBurnResistMarked { get; set; }
    public bool IsDodgeMarked { get; set; }
    public bool IsDestroyed { get; set; }
    public bool IsShipSunk { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsDevastated { get; set; }
    public bool IsCaptured { get; set; }
    public bool IsFirePermanent { get; set; }
}

public class AvailableWeaponDto
{
    public string Id { get; set; }
    public string ShipId { get; set; }
    public string ShipName { get; set; }
    public string Type { get; set; }
    public int Ammo { get; set; }
    public int DeckIndex { get; set; }
    public int AimRemaining { get; set; }
}

public class ShipDto
{
    public string Id { get; set; }
    public string DefinitionId { get; set; }
    public string Name { get; set; }
    public int DeckCount { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public string Orientation { get; set; }
    public bool IsDestroyed { get; set; }
    public bool IsPlaced { get; set; }
    public bool IsSummon { get; set; }
    public string Range { get; set; }
    public int Cost { get; set; }
    public List<string> Abilities { get; set; } = new();
    public List<string> Upgrades { get; set; } = new();
    public int Speed { get; set; }
    public int Space { get; set; }
    public int ExplosionRadius { get; set; }
    public List<string> Regions { get; set; } = new();
    public bool HasManeuvered { get; set; }
    public List<DeckDto> Decks { get; set; } = new();
    public List<WeaponDto> Weapons { get; set; } = new();
}

public class DeckDto
{
    public int Index { get; set; }
    public int MaxHp { get; set; }
    public int CurrentHp { get; set; }
    public bool IsDestroyed { get; set; }
    public string Module { get; set; }
    public bool ModuleDestroyed { get; set; }
}

public class WeaponDto
{
    public string Id { get; set; }
    public string ShipId { get; set; }
    public string Type { get; set; }
    public int Ammo { get; set; }
    public int DeckIndex { get; set; }
    public bool HasAmmo { get; set; }
    public int AimSpeed { get; set; }
}

public class SummonDto
{
    public string Id { get; set; }
    public string Type { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int Speed { get; set; }
    public bool IsAlive { get; set; }
    public string MoveDirection { get; set; }
    public bool WaitingForTurnBack { get; set; }
    public bool WaitingForDirectionChoice { get; set; }
    public bool IsBoardingShip { get; set; }
}

public class PendingSummonDto
{
    public string Id { get; set; }
    public string Type { get; set; }
    public List<int> AllowedColumns { get; set; } = new();
    public bool IsBoarding { get; set; }
    public string SourceShipName { get; set; }
}

public class FleetSelectionDto
{
    public string DefinitionId { get; set; }
    public string ShipName { get; set; }
    public int Cost { get; set; }
    public List<string> Upgrades { get; set; } = new();
}

public class ShipCatalogDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public int DeckCount { get; set; }
    public string Range { get; set; }
    public int Cost { get; set; }
    public int DefaultArmor { get; set; }
    public List<int> DeckHpOverrides { get; set; }
    public int Space { get; set; }
    public int Speed { get; set; }
    public bool IsFree { get; set; }
    public List<string> Abilities { get; set; } = new();
    public string Description { get; set; }
    public string Region { get; set; }
    public List<string> Regions { get; set; } = new();
    public List<UpgradeDto> AvailableUpgrades { get; set; } = new();
}

public class UpgradeDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public int Cost { get; set; }
    public string Description { get; set; }
}
