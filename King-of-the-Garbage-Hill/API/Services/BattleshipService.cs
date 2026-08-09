using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using King_of_the_Garbage_Hill.Battleship.Logic;
using King_of_the_Garbage_Hill.Battleship.Models;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;
using King_of_the_Garbage_Hill.Localization;

namespace King_of_the_Garbage_Hill.API.Services;

public class BattleshipService
{
    private static readonly TimeSpan ComboHitDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan FastComboHitDelay = TimeSpan.FromSeconds(2);
    /// <summary>ZBS paid for the first battleship win of the (UTC) day. Anchor: a daily quest card pays 20.</summary>
    public const int BattleshipFirstWinZbs = 10;

    private readonly ConcurrentDictionary<string, BattleshipGame> _games = new();
    private readonly Timer _cleanupTimer;
    private readonly UserAccounts _userAccounts;
    private readonly MessageCatalog _messageCatalog;
    private static readonly Random Rng = new();

    public BattleshipService(UserAccounts userAccounts, MessageCatalog messageCatalog)
    {
        _userAccounts = userAccounts;
        _messageCatalog = messageCatalog;
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
            if (!Enum.TryParse<Faction>(faction, true, out var selectedFaction))
                return (false, "Неизвестная фракция.");
            player.Faction = selectedFaction;
            player.CoinsRemaining = FleetValidator.GetBudget(selectedFaction);
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
            var (valid, error) = FleetValidator.ValidateFleet(selections, player.Faction);
            if (!valid) return (false, error);

            // Build full 10-ship fleet from purchases (fills defaults)
            var fullFleet = FleetValidator.BuildFleetFromSelections(selections, player.Faction);

            // Store selection and build ships
            player.SelectedShips = fullFleet;
            player.CoinsRemaining = FleetValidator.GetBudget(player.Faction) -
                                    FleetValidator.CalculateTotalCost(fullFleet);
            player.Fleet.Clear();

            foreach (var sel in fullFleet)
            {
                var def = ShipCatalog.GetById(sel.DefinitionId);
                if (def != null)
                {
                    if (def.Id == "famous_assembling_ship")
                        player.Fleet.AddRange(ShipCatalog.CreateAssemblyComponents(def, sel.Upgrades));
                    else
                        player.Fleet.Add(ShipCatalog.CreateShip(def, sel.Upgrades));
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

    public (bool success, string error) ConfirmPlacement(
        string gameId,
        string discordId,
        List<TetracatapultLoadoutDto> loadouts = null,
        bool? useSharedTetracatapultAmmo = null,
        bool? useGhostSummons = null)
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
                return (false, "Расстановка уже подтверждена.");

            // Validate all ships placed
            var (valid, error) = PlacementValidator.ValidateAllPlaced(player.Fleet, player.Board);
            if (!valid) return (false, error);

            var tetracatapults = player.Fleet
                .Where(ship => !ship.IsDestroyed)
                .SelectMany(ship => ship.Weapons)
                .Where(weapon => weapon.Type == WeaponType.Tetracatapult)
                .ToList();
            loadouts ??= new List<TetracatapultLoadoutDto>();
            if (loadouts.Select(value => value.WeaponId).Distinct(StringComparer.Ordinal).Count() != loadouts.Count ||
                loadouts.Any(value => tetracatapults.All(weapon => weapon.Id != value.WeaponId)))
                return (false, "Выберите Белый камень или Дробь отдельно для каждого Тетракамнемёта.");

            foreach (var weapon in tetracatapults)
            {
                var selected = loadouts.FirstOrDefault(value => value.WeaponId == weapon.Id);
                var shotType = ShotType.Buckshot;
                if (selected != null &&
                    (!Enum.TryParse<ShotType>(selected.ShotType, true, out shotType) ||
                    shotType is not (ShotType.WhiteStone or ShotType.Buckshot))
                   )
                    return (false, "Для каждого Тетракамнемёта нужно выбрать Белый камень или Дробь.");
                weapon.ConfiguredShotType = shotType;
            }

            player.UseSharedTetracatapultAmmo = useSharedTetracatapultAmmo ?? true;
            player.UseGhostSummons = useGhostSummons ?? true;
            BattleshipGameEngine.InitializeSharedTetracatapultAmmo(game, player);

            player.IsReady = true;
            game.LastActivity = DateTime.UtcNow;

            CheckPhaseTransition(game);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    public (bool success, string error) CancelPlacement(string gameId, string discordId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase != BsGamePhase.ShipPlacement)
                return (false, "Расстановку уже нельзя изменить.");
            var player = game.GetPlayer(discordId);
            if (player == null) return (false, "Вы не в этой игре.");
            var opponent = game.GetOpponent(discordId);
            if (!player.IsReady) return (false, "Расстановка ещё не подтверждена.");
            if (opponent?.IsReady == true)
                return (false, "Противник уже подтвердил расстановку.");

            player.IsReady = false;
            game.LastActivity = DateTime.UtcNow;
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
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (null, matryoshkaError);
            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null) return (null, cursedDirectionError);

            if (row < 0 || row >= 10 || col < 0 || col >= 10)
                return (null, "Клетка за пределами поля.");

            // Boarding deployment is a global pause, not merely a restriction on the
            // player whose turn happened to be active when the transition fired.
            if (HasPendingBoardingDeployments(game))
                return (null, "Сначала выпустите все обязательные единицы абордажа!");

            var delayError = GetShotDelayError(shooter);
            if (delayError != null) return (null, delayError);

            var assemblyError = GetAssemblyLockError(game, shooter);
            if (assemblyError != null) return (null, assemblyError);
            var maneuverError = GetManeuverLockError(game, shooter);
            if (maneuverError != null) return (null, maneuverError);

            if (shooter.Board.PlacedShips.Any(ship =>
                    !ship.IsDestroyed && ship.Statuses.Contains(ShipStatusType.Capture)))
                return (null, "Сначала уничтожьте захваченный корабль на своём поле.");

            var weaponError = ValidateSelectedWeapon(game, shooter, ownBoard: false);
            if (weaponError != null) return (null, weaponError);
            if (shooter.SelectedShotType == ShotType.Buckshot && (row > 8 || col > 8))
                return (null, "Картечь должна полностью помещаться на поле.");

            var firstResolutionLogIndex = game.GameLog.Count;
            var targetBoardOwner = game.GetOpponent(shooter.DiscordId);
            var shotCountBefore = game.ShotCount;

            // Process shot (buckshot uses 2x2 AoE)
            ShotResult result;
            if (shooter.SelectedShotType == ShotType.Buckshot)
                result = BattleshipGameEngine.ProcessBuckshotShot(game, shooter, row, col);
            else
                result = BattleshipGameEngine.ProcessShot(game, shooter, row, col);

            DescribeShot(game, shooter, result, ownBoard: false);
            result.TurnContinues = RegisterResolvedPlayerShot(
                game, shooter, shotCountBefore, result, allowSecondShot: true);
            ResetExpendedSelection(game, shooter);
            shooter.HasShotThisTurn = true;
            game.LastActivity = DateTime.UtcNow;
            CompleteActionResolution(game, result.TurnContinues, moveSummons: true);
            GateNewBoardDetailLogs(game, shooter, targetBoardOwner, firstResolutionLogIndex);
            if (!game.IsFinished)
                CheckAndApplyWin(game);
            ApplyComboShotDelay(game, shooter, result);
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

            var shooter = game.GetPlayer(discordId);
            if (shooter == null)
                return (null, "Вы не в этой игре.");
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (null, matryoshkaError);
            var activeShooter = game.GetPlayer(game.CurrentTurnPlayerId);
            var isEvilGreekFireReaction =
                game.CurrentTurnPlayerId != discordId &&
                shooter.SelectedShotType == ShotType.EvilGreekFire &&
                activeShooter != null &&
                activeShooter.NextShotAllowedAtUtc > DateTime.UtcNow;
            if (game.CurrentTurnPlayerId != discordId && !isEvilGreekFireReaction)
                return (null, "Сейчас не ваш ход.");
            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null) return (null, cursedDirectionError);

            if (row < 0 || row >= 10 || col < 0 || col >= 10)
                return (null, "Клетка за пределами поля.");

            if (HasPendingBoardingDeployments(game))
                return (null, "Сначала выпустите все обязательные единицы абордажа!");

            if (!isEvilGreekFireReaction)
            {
                var delayError = GetShotDelayError(shooter);
                if (delayError != null) return (null, delayError);
            }

            var assemblyError = GetAssemblyLockError(game, shooter);
            if (assemblyError != null) return (null, assemblyError);
            var maneuverError = GetManeuverLockError(game, shooter);
            if (maneuverError != null) return (null, maneuverError);

            var captured = shooter.Board.PlacedShips.Any(s =>
                !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture));
            if (!captured && shooter.SelectedShotType is not (ShotType.GreekFire or ShotType.EvilGreekFire))
            {
                var cell = shooter.Board.GetCell(row, col);
                if (cell?.SummonRef is not { IsAlive: true } enemySummon || enemySummon.OwnerId == shooter.DiscordId)
                    return (null, "На этой клетке нет вражеского призыва.");
            }

            var weaponError = ValidateSelectedWeapon(game, shooter, ownBoard: true);
            if (weaponError != null) return (null, weaponError);

            var firstResolutionLogIndex = game.GameLog.Count;
            var isGreekFire = shooter.SelectedShotType is ShotType.GreekFire or ShotType.EvilGreekFire;
            var shotCountBefore = game.ShotCount;
            ShotResult result;
            if (captured)
            {
                result = BattleshipGameEngine.ProcessShot(game, shooter, row, col);
            }
            else if (isGreekFire)
            {
                BattleshipGameEngine.ConsumeSelectedWeaponAmmo(game, shooter);
                result = BattleshipGameEngine.ProcessOwnBoardGreekFireShot(game, shooter, row, col);
            }
            else
            {
                result = BattleshipGameEngine.ProcessOwnBoardShot(game, shooter, row, col);
            }

            DescribeShot(game, shooter, result, ownBoard: true);
            result.TurnContinues = false;
            var resolvedTurnContinues = RegisterResolvedPlayerShot(
                game,
                shooter,
                shotCountBefore,
                result,
                allowSecondShot: !isEvilGreekFireReaction);
            if (!isEvilGreekFireReaction)
                result.TurnContinues = resolvedTurnContinues;
            ResetExpendedSelection(game, shooter);
            game.LastActivity = DateTime.UtcNow;
            if (isEvilGreekFireReaction)
            {
                ResolveImmediateEffectTransitions(game);
                GateNewBoardDetailLogs(game, shooter, shooter, firstResolutionLogIndex);
                TrySettleGameEnd(game);
                return (result, null);
            }

            shooter.HasShotThisTurn = true;
            CompleteActionResolution(game, resolvedTurnContinues, moveSummons: true);
            GateNewBoardDetailLogs(game, shooter, shooter, firstResolutionLogIndex);
            if (!game.IsFinished) CheckAndApplyWin(game);
            ApplyComboShotDelay(game, shooter, result);

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
        if (HasPendingMatryoshkaDeployments(game)) return;
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
        if (player.SelectedShotType is ShotType.GreekFire or ShotType.EvilGreekFire && !ownBoard)
            return "Греческий огонь стреляет только по своему полю.";

        var requiredType = player.SelectedShotType switch
        {
            ShotType.WhiteStone or ShotType.Buckshot => WeaponType.Tetracatapult,
            ShotType.Incendiary => WeaponType.Incendiary,
            ShotType.EvilIncendiary => WeaponType.EvilIncendiary,
            ShotType.GreekFire => WeaponType.GreekFire,
            ShotType.EvilGreekFire => WeaponType.EvilGreekFire,
            _ => WeaponType.Ballista,
        };
        var usable = BattleshipGameEngine.GetUsableWeapons(game, player, requiredType).ToList();
        if (player.SelectedWeapon == null || usable.All(x => x.weapon.Id != player.SelectedWeapon.Id) ||
            requiredType == WeaponType.Tetracatapult &&
            player.SelectedWeapon.ConfiguredShotType != player.SelectedShotType)
            player.SelectedWeapon = requiredType == WeaponType.Tetracatapult
                ? usable.Select(x => x.weapon)
                    .FirstOrDefault(weapon => weapon.ConfiguredShotType == player.SelectedShotType)
                : usable.Select(x => x.weapon).FirstOrDefault();
        if (player.SelectedWeapon == null || usable.All(x => x.weapon.Id != player.SelectedWeapon.Id))
            return "Выбранное оружие или его модуль уничтожены, либо закончились боеприпасы.";
        if (player.SelectedWeapon.AimSpeed > player.RevealedCellCount)
            return $"Оружие ещё заряжается! Нужно разведать {player.SelectedWeapon.AimSpeed - player.RevealedCellCount} клеток.";
        return null;
    }

    private static void ResetExpendedSelection(BattleshipGame game, BattleshipPlayer player)
    {
        var firedIncendiary =
            player.SelectedShotType is ShotType.Incendiary or ShotType.EvilIncendiary;
        if (firedIncendiary && BattleshipGameEngine.HasUsableBallista(game, player) ||
            player.SelectedShotType is ShotType.WhiteStone or ShotType.Buckshot or
                ShotType.GreekFire or ShotType.EvilGreekFire ||
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
        result.SourceRow = visualSource?.row ?? -1;
        result.SourceCol = visualSource?.col ?? -1;
        result.SourceBoardPlayerId = visualSource?.boardOwnerId ??
                                     (shooter.SelectedWeapon != null ? shooter.DiscordId : null);
        result.ProjectileType = shooter.SelectedShotType switch
        {
            ShotType.WhiteStone => "Stone",
            ShotType.Buckshot => "Buckshot",
            ShotType.Incendiary or ShotType.EvilIncendiary or
                ShotType.GreekFire or ShotType.EvilGreekFire => "Fire",
            _ => "Arrow",
        };
        result.TargetPlayerId = ownBoard
            ? shooter.DiscordId
            : game.GetOpponent(shooter.DiscordId)?.DiscordId;
    }

    private static bool HasLivingMast(BattleshipPlayer player) =>
        BattleshipGameEngine.HasLivingMast(player);

    /// <summary>
    /// Count one validated attack action for owner-scoped passives. The original warming ship
    /// guarantees a second attack; Ver.2 continues until it explodes or misses twice in a row.
    /// Out-of-turn Evil Greek Fire still counts toward overheating but never takes over the turn.
    /// </summary>
    private static bool RegisterResolvedPlayerShot(
        BattleshipGame game,
        BattleshipPlayer shooter,
        int shotCountBefore,
        ShotResult result,
        bool allowSecondShot)
    {
        var resolvedShots = Math.Max(0, game.ShotCount - shotCountBefore);
        if (resolvedShots == 0) return result?.TurnContinues ?? false;

        shooter.TotalShotsFired += resolvedShots;
        if (allowSecondShot)
        {
            shooter.ShotsFiredThisTurn += resolvedShots;
            shooter.ConsecutiveWarmingMisses = result?.Miss == true
                ? shooter.ConsecutiveWarmingMisses + 1
                : 0;
        }

        var hadLivingWarmingV2 = allowSecondShot && shooter.Board.PlacedShips.Any(ship =>
            IsOperationalPassiveSource(ship, "warming_chain_until_two_misses"));
        BattleshipGameEngine.TriggerOverheatExplosion(game, shooter);
        if (result?.ForcesTurnEnd == true) return false;

        var hasLivingWarmingV2 = allowSecondShot && shooter.Board.PlacedShips.Any(ship =>
            IsOperationalPassiveSource(ship, "warming_chain_until_two_misses"));
        if (hadLivingWarmingV2 && !hasLivingWarmingV2)
            return false;
        if (hasLivingWarmingV2)
            return shooter.ConsecutiveWarmingMisses < 2;

        var hasLivingDoubleShotSource = shooter.Board.PlacedShips.Any(ship =>
            IsOperationalPassiveSource(ship, "double_shot_while_alive"));
        var guaranteedSecondShot = allowSecondShot &&
                                   shooter.ShotsFiredThisTurn < 2 &&
                                   hasLivingDoubleShotSource;
        return (result?.TurnContinues ?? false) || guaranteedSecondShot;
    }

    private static bool IsOperationalPassiveSource(Ship ship, string ability) =>
        !ship.IsDestroyed &&
        !ship.Statuses.Any(status => status is
            ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) &&
        ship.Abilities.Contains(ability);

    /// <summary>
    /// Status-resolution logs are produced by the engine before the personalized state push.
    /// Keep them exact for the physical board owner and spectators, while an observer whose
    /// last living Mast is gone receives only non-tactical action summaries.
    /// </summary>
    private static void GateNewBoardDetailLogs(
        BattleshipGame game,
        BattleshipPlayer actionPlayer,
        BattleshipPlayer physicalTarget,
        int firstLogIndex)
    {
        if (game == null || physicalTarget == null || firstLogIndex >= game.GameLog.Count) return;
        var observer = game.GetOpponent(physicalTarget.DiscordId);
        if (observer == null) return;

        var newEntries = game.GameLog.Skip(Math.Max(0, firstLogIndex)).ToList();
        if (HasLivingMast(observer)) return;

        foreach (var entry in newEntries)
        {
            // Explicit board ownership carries its immutable event-time audience. Never
            // re-route it using action-wide/name heuristics.
            if (entry.DetailBoardOwnerId != null) continue;

            if (entry.VisibleTo == observer.DiscordId)
            {
                // Several historical engine checks considered a Mast on a destroyed deck alive.
                // Redirect those false-positive private reports to the board that resolved them.
                entry.VisibleTo = ResolveBoardDetailOwner(game, physicalTarget, entry.Text)?.DiscordId ??
                                  physicalTarget.DiscordId;
                continue;
            }

            if (entry.VisibleTo != null) continue;
            if (IsPrivateActionSummary(entry.Text, actionPlayer))
            {
                entry.VisibleTo = actionPlayer.DiscordId;
                continue;
            }
            if (IsPublicActionSummary(entry.Text, actionPlayer))
                continue;

            entry.VisibleTo = ResolveBoardDetailOwner(game, physicalTarget, entry.Text)?.DiscordId ??
                              physicalTarget.DiscordId;
        }
    }

    private static bool IsPublicActionSummary(string text, BattleshipPlayer actionPlayer)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (text.StartsWith("Победитель:", StringComparison.Ordinal) ||
            text.Contains("автоматическую победу", StringComparison.OrdinalIgnoreCase))
            return true;
        if (actionPlayer == null) return false;

        return text.StartsWith($"{actionPlayer.Username} промахнулся", StringComparison.Ordinal) ||
               text.StartsWith($"{actionPlayer.Username} выстрелил картечью", StringComparison.Ordinal) ||
               text.StartsWith($"{actionPlayer.Username} получает штраф", StringComparison.Ordinal);
    }

    private static bool IsPrivateActionSummary(string text, BattleshipPlayer actionPlayer)
    {
        if (string.IsNullOrWhiteSpace(text) || actionPlayer == null) return false;
        return text.StartsWith($"{actionPlayer.Username} развернул", StringComparison.Ordinal) ||
               text.StartsWith($"{actionPlayer.Username} выпустил", StringComparison.Ordinal) ||
               text.StartsWith($"{actionPlayer.Username} перенаправил", StringComparison.Ordinal) ||
               text.StartsWith($"{actionPlayer.Username} восстановил", StringComparison.Ordinal);
    }

    private static BattleshipPlayer ResolveBoardDetailOwner(
        BattleshipGame game,
        BattleshipPlayer preferredBoardOwner,
        string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // A name on a currently placed hull identifies the physical board whose private
        // information the log describes. Prefer the action target when both fleets contain
        // identically named catalog ships; choosing the observer in that ambiguity would leak.
        var players = new[] { preferredBoardOwner }
            .Concat(game.GetPlayers().Where(player => player != preferredBoardOwner));
        foreach (var player in players)
        {
            if (player == null) continue;
            if (player.Board.PlacedShips
                .OrderByDescending(ship => ship.Name?.Length ?? 0)
                .Any(ship => !string.IsNullOrWhiteSpace(ship.Name) &&
                             text.Contains(ship.Name, StringComparison.Ordinal)))
                return player;
        }

        return null;
    }

    private static (Ship ship, Weapon weapon, int row, int col, string boardOwnerId)?
        SelectNextBallistaAnimationSource(
        BattleshipGame game,
        BattleshipPlayer shooter)
    {
        var usable = BattleshipGameEngine.GetUsableWeapons(game, shooter, WeaponType.Ballista).ToList();
        var sources = new List<(Ship ship, Weapon weapon, int row, int col, string boardOwnerId)>();

        if (game.Phase == BsGamePhase.Boarding)
        {
            // Only the Mid-less side fires from Close ships it actually sent to boarding.
            // The retained side continues to animate from its operational Mid line.
            var boardingByShip = shooter.Summons
                .Where(s => s.IsAlive && s.IsBoardingShip && s.SourceShipId != null)
                .GroupBy(s => s.SourceShipId)
                .ToDictionary(group => group.Key, group => group.First());
            foreach (var source in usable.Where(x => x.ship.Range == RangeClass.Close))
            {
                if (boardingByShip.TryGetValue(source.ship.Id, out var boarding))
                {
                    sources.Add((source.ship, source.weapon, boarding.Row, boarding.Col,
                        game.GetOpponent(shooter.DiscordId)?.DiscordId));
                    continue;
                }
                if (shooter.Board.PlacedShips.Contains(source.ship))
                    sources.Add((source.ship, source.weapon, source.ship.Row, source.ship.Col,
                        shooter.DiscordId));
            }
        }

        if (sources.Count == 0)
        {
            sources.AddRange(usable
                .Where(x => x.ship.Range == RangeClass.Mid)
                .Select(x => (x.ship, x.weapon, x.ship.Row, x.ship.Col, shooter.DiscordId)));
        }

        sources = sources
            .OrderBy(x => x.row)
            .ThenBy(x => x.col)
            .ThenBy(x => x.ship.Id, StringComparer.Ordinal)
            .ThenBy(x => x.weapon.DeckIndex)
            .ToList();
        if (sources.Count == 0) return null;
        var index = Math.Abs(shooter.NextBallistaAnimationIndex) % sources.Count;
        shooter.NextBallistaAnimationIndex = (index + 1) % sources.Count;
        return sources[index];
    }

    private static void ApplyComboShotDelay(
        BattleshipGame game,
        BattleshipPlayer shooter,
        ShotResult result)
    {
        // A reset delay is only ever produced by a ship deck kill on the opponent's board: every
        // own-board and summon-kill path ends the turn, so `defender` is exactly the owner of the
        // field that lost the deck. The long window exists only when a human defender has at least
        // one summon that the server would accept for placement at this exact state boundary.
        var defender = game.GetOpponent(shooter.DiscordId);
        var delay = !game.IsFinished && result is { Hit: true, TurnContinues: true }
            ? defender is { IsBot: false } && CanDeployAnySummon(game, defender)
                ? ComboHitDelay        // 8 s — a human defender can answer with a summon
                : FastComboHitDelay    // 2 s — no summon response is currently available
            : TimeSpan.Zero;           // miss / turn ends / game over
        result.ShotDelayMs = (int)delay.TotalMilliseconds;
        shooter.CurrentShotDelayMs = result.ShotDelayMs;
        shooter.NextShotAllowedAtUtc = delay > TimeSpan.Zero
            ? DateTime.UtcNow.Add(delay)
            : DateTime.MinValue;
    }

    private static string GetShotDelayError(BattleshipPlayer shooter)
    {
        var remaining = shooter.NextShotAllowedAtUtc - DateTime.UtcNow;
        return remaining > TimeSpan.Zero
            ? $"После попадания следующий выстрел будет доступен через {Math.Ceiling(remaining.TotalSeconds)} сек."
            : null;
    }

    private static bool HasMandatoryBoardingDeployment(BattleshipPlayer player) =>
        player != null &&
        (player.PendingSummons.Any(summon => summon.IsMandatoryBoarding) ||
         player.MandatoryBoardingSummonSlots > 0 ||
         player.MandatoryBoardingBrander);

    private static bool HasPendingBoardingDeployments(BattleshipGame game) =>
        game.GetPlayers().Any(HasMandatoryBoardingDeployment);

    private static bool HasPendingMatryoshkaDeployments(BattleshipGame game) =>
        game.GetPlayers().Any(player => player.PendingMatryoshka != null);

    private string GetPlayerLanguage(string discordId)
    {
        if (!ulong.TryParse(discordId, out var userId)) return "ru";
        return _userAccounts.GetAccount(userId)?.Language == "en" ? "en" : "ru";
    }

    private static string GetMatryoshkaLockError(BattleshipGame game, string _) =>
        HasPendingMatryoshkaDeployments(game)
            ? "Выберите подсвеченное свободное место."
            : null;

    private static int GetMatryoshkaStage(Ship ship)
    {
        if (ship?.Abilities.Contains("matryoshka_stage_4") == true) return 4;
        if (ship?.Abilities.Contains("matryoshka_stage_3") == true) return 3;
        if (ship?.Abilities.Contains("matryoshka_stage_2") == true) return 2;
        return 0;
    }

    /// <summary>
    /// Convert a newly destroyed Matryoshka stage into one mandatory in-place choice.
    /// Only one choice is exposed globally; another simultaneous wreck is queued on the
    /// next resolution pass after the current replacement is placed.
    /// </summary>
    private void QueueMatryoshkaReplacements(BattleshipGame game)
    {
        // Geometry is private and choices are globally sequential: never expose two
        // replacement prompts at once, even when one effect destroyed both fleets.
        if (HasPendingMatryoshkaDeployments(game)) return;

        foreach (var player in game.GetPlayers())
        {
            var parent = player.Board.PlacedShips
                .FirstOrDefault(ship =>
                    ship.IsDestroyed &&
                    !ship.MatryoshkaReplacementQueued &&
                    !ship.MatryoshkaReplacementSuppressed &&
                    !ship.Statuses.Contains(ShipStatusType.Capture) &&
                    !ship.Statuses.Contains(ShipStatusType.Devastated) &&
                    GetMatryoshkaStage(ship) > 1);
            if (parent == null) continue;

            var childDeckCount = GetMatryoshkaStage(parent) - 1;
            var wreckCells = parent.Decks
                .OrderBy(deck => deck.Index)
                .Select(deck => parent.GetDeckCell(deck, parent.Row, parent.Col, parent.Orientation))
                .ToList();
            if (wreckCells.Count != childDeckCount + 1) continue;

            var options = Enumerable.Range(0, 2)
                .Select(offset => new MatryoshkaPlacementOption
                {
                    // The click target is the unique end of this subset. Placement
                    // itself remains anchored at Cells[0].
                    Row = (offset == 0 ? wreckCells[0] : wreckCells[^1]).row,
                    Col = (offset == 0 ? wreckCells[0] : wreckCells[^1]).col,
                    Orientation = parent.Orientation,
                    Cells = wreckCells.Skip(offset).Take(childDeckCount).ToList(),
                })
                .ToList();

            parent.MatryoshkaReplacementQueued = true;
            player.PendingMatryoshka = new PendingMatryoshkaReplacement
            {
                ParentShipId = parent.Id,
                ChildName = ShipCatalog.CreateMatryoshkaStageShip(childDeckCount).Name,
                ChildDeckCount = childDeckCount,
                Options = options,
            };

            var opponent = game.GetOpponent(player.DiscordId);
            if (opponent != null)
            {
                game.AddLogFor(opponent.DiscordId, _messageCatalog.Render(
                    new LocalizedMessage("battleship.matryoshka.enemyOpened"),
                    GetPlayerLanguage(opponent.DiscordId)));
            }

            return;
        }
    }

    private static bool HasWaitingRamReturn(BattleshipPlayer player) =>
        player?.Summons.Any(summon =>
            summon.IsAlive &&
            summon.Type == SummonType.Ram &&
            summon.WaitingForTurnBack) == true;

    private static void CompleteMandatoryBoardingDeployment(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        player.BoardingDeploymentCapacity =
            Math.Max(0, player.BoardingDeploymentCapacity - 1);
        if (player.BoardingDeploymentCapacity == 0 ||
            !HasLegalMandatoryBoardingDeployment(game, player))
            BattleshipGameEngine.DiscardMandatoryBoardingRemainder(game, player);
    }

    private static List<int> GetLegalPendingSummonColumns(
        BattleshipGame game,
        BattleshipPlayer player,
        PendingSummonDeploy pending)
    {
        var opponent = game?.GetOpponent(player?.DiscordId);
        if (opponent == null || pending == null) return new();
        var columns = pending.AllowedColumns.Count > 0
            ? pending.AllowedColumns
            : Enumerable.Range(0, 10);
        return columns.Where(col =>
        {
            if (col is < 0 or >= 10) return false;
            var preview = BattleshipGameEngine.CreateSummonFromPending(
                pending,
                player.DiscordId,
                0,
                col,
                Direction.Down,
                game.ShotCount,
                player.UseGhostSummons);
            return preview != null && BattleshipGameEngine.CanPlaceSummonOnBoard(
                opponent.Board, preview, 0, col, Direction.Down);
        }).Distinct().OrderBy(col => col).ToList();
    }

    private static bool HasLegalMandatoryBoardingDeployment(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        if (!HasMandatoryBoardingDeployment(player) ||
            player.BoardingDeploymentCapacity <= 0)
            return false;
        var opponent = game.GetOpponent(player.DiscordId);
        if (opponent == null) return false;
        if (player.PendingSummons.Any(pending =>
                pending.IsMandatoryBoarding &&
                GetLegalPendingSummonColumns(game, player, pending).Count > 0))
            return true;

        var regions = player.Fleet.SelectMany(ship => ship.Regions).ToHashSet();
        if (player.MandatoryBoardingSummonSlots > 0 &&
            regions.Contains(Region.South) &&
            player.Board.PlacedShips.Any(ship =>
                ship.Statuses.Contains(ShipStatusType.Devastated) &&
                !ship.Statuses.Contains(ShipStatusType.Capture)))
            return true;
        var hasOpenEntry = Enumerable.Range(0, 10).Any(col =>
            opponent.Board.GetCell(0, col)?.SummonRef is not { IsAlive: true });
        if (player.MandatoryBoardingSummonSlots > 0 &&
            regions.Overlaps(new[] { Region.West, Region.East, Region.South }) &&
            hasOpenEntry)
            return true;
        return player.MandatoryBoardingBrander && hasOpenEntry;
    }

    private static PendingAssemblyDto GetPendingAssembly(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        if (player == null ||
            game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding) ||
            game.CurrentTurnPlayerId != player.DiscordId ||
            player.HasShotThisTurn ||
            player.HasPenalty ||
            player.StunShotExpiry >= game.ShotCount ||
            HasPendingMatryoshkaDeployments(game) ||
            HasPendingBoardingDeployments(game) ||
            HasPendingCursedBoatDirection(game))
            return null;

        var group = player.Board.PlacedShips
            .Where(ship => ship.IsAssemblyComponent && !string.IsNullOrWhiteSpace(ship.AssemblyGroupId))
            .GroupBy(ship => ship.AssemblyGroupId)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .FirstOrDefault(value =>
                value.Count() == 3 &&
                value.Count(ship => !ship.IsDestroyed) == 1 &&
                value.All(ship =>
                    ship.AssemblyEligibleTurnNumber >= 0 &&
                    ship.AssemblyEligibleTurnNumber <= game.TurnNumber));
        if (group == null) return null;

        var definition = ShipCatalog.GetById("famous_assembling_ship");
        if (definition == null) return null;
        var preview = ShipCatalog.CreateAssembledShip(definition);
        var componentIds = group.Select(ship => ship.Id).ToHashSet();
        var options = new List<AssemblyPlacementOptionDto>();
        foreach (var orientation in Enum.GetValues<Orientation>())
        for (var row = 0; row < 10; row++)
        for (var col = 0; col < 10; col++)
        {
            var cells = preview.GetOccupiedCells(row, col, orientation);
            if (cells.Any(cell => cell.row is < 0 or >= 10 || cell.col is < 0 or >= 10))
                continue;
            if (cells.Any(cell => cell.row >= 8))
                continue;
            if (cells.Any(cell =>
            {
                var occupiedCell = player.Board.GetCell(cell.row, cell.col);
                return occupiedCell?.SummonRef is { IsAlive: true } ||
                       occupiedCell?.ShipRef is { } occupyingShip &&
                       !componentIds.Contains(occupyingShip.Id);
            }))
                continue;

            var tooClose = player.Board.PlacedShips
                .Where(ship => !componentIds.Contains(ship.Id) && !ship.IsDestroyed)
                .Any(ship =>
                {
                    var spacing = Math.Max(preview.Space, ship.Space);
                    return ship.GetOccupiedCells().Any(existing =>
                        cells.Any(candidate =>
                            Math.Abs(existing.row - candidate.row) <= spacing &&
                            Math.Abs(existing.col - candidate.col) <= spacing));
                });
            if (tooClose) continue;
            options.Add(new AssemblyPlacementOptionDto
            {
                Row = row,
                Col = col,
                Orientation = orientation.ToString(),
            });
        }

        return options.Count == 0
            ? null
            : new PendingAssemblyDto { GroupId = group.Key, Options = options };
    }

    private static string GetAssemblyLockError(BattleshipGame game, BattleshipPlayer player) =>
        GetPendingAssembly(game, player) == null
            ? null
            : "Сначала соберите Заслуженный собирающийся корабль на подсвеченном месте.";

    /// <summary>
    /// Whether this player can legally place at least one summon right now. The caller-only
    /// <c>CanDeployAnySummon</c> DTO field drives the client controls, and the same authoritative
    /// predicate decides whether a human defender receives the long combo-response window.
    /// </summary>
    private static bool CanDeployAnySummon(BattleshipGame game, BattleshipPlayer player)
    {
        if (player == null ||
            game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding) ||
            HasPendingMatryoshkaDeployments(game))
            return false;

        var opponent = game.GetOpponent(player.DiscordId);
        if (opponent == null) return false;
        var hasFinalRush = HasFinalRush(game, player);

        bool EntryIsOpen(int row, int col) =>
            opponent.Board.GetCell(row, col)?.SummonRef is not { IsAlive: true };

        var waitingRams = player.Summons.Where(s =>
                s.IsAlive && s.WaitingForTurnBack && s.Type == SummonType.Ram)
            .ToList();
        if (waitingRams.Count > 0)
        {
            var returnCooldownReady =
                hasFinalRush ||
                game.ShotCount - player.LastSummonDeployShotCount >= 2;
            if (!returnCooldownReady) return false;
            foreach (var waiting in waitingRams)
            {
                if (waiting.MoveDirection is Direction.Left or Direction.Right)
                {
                    var edgeCol = waiting.MoveDirection == Direction.Right ? 9 : 0;
                    var returnDirection = waiting.MoveDirection == Direction.Right
                        ? Direction.Left
                        : Direction.Right;
                    if (Enumerable.Range(Math.Max(0, waiting.Row - 1), Math.Min(9, waiting.Row + 1) - Math.Max(0, waiting.Row - 1) + 1)
                        .Any(row => BattleshipGameEngine.CanPlaceSummonOnBoard(
                            opponent.Board, waiting, row, edgeCol, returnDirection)))
                        return true;
                }
                else
                {
                    var edgeRow = waiting.MoveDirection == Direction.Down ? 9 : 0;
                    var returnDirection = waiting.MoveDirection == Direction.Down
                        ? Direction.Up
                        : Direction.Down;
                    if (Enumerable.Range(Math.Max(0, waiting.Col - 1), Math.Min(9, waiting.Col + 1) - Math.Max(0, waiting.Col - 1) + 1)
                        .Any(col => BattleshipGameEngine.CanPlaceSummonOnBoard(
                            opponent.Board, waiting, edgeRow, col, returnDirection)))
                        return true;
                }
            }
            return false;
        }
        if (HasPendingBoardingDeployments(game))
            return HasLegalMandatoryBoardingDeployment(game, player);
        if (
            HasPendingCursedBoatDirection(game) ||
            GetPendingAssembly(game, player) != null ||
            GetPendingManeuver(game, player) != null)
            return false;

        foreach (var pending in player.PendingSummons.Where(p => !p.IsMandatoryBoarding))
        {
            if (!pending.IsFree && player.SummonSlotsUsed >= player.MaxSummonSlots) continue;
            if (!pending.IsFree && !hasFinalRush &&
                (game.ShotCount - player.LastSummonDeployShotCount < 2 ||
                 player.RevealedCellCount < 5 * (player.SummonSlotsUsed + 1)))
                continue;
            if (GetLegalPendingSummonColumns(game, player, pending).Count > 0) return true;
        }

        var summonCooldownReady =
            hasFinalRush ||
            game.ShotCount - player.LastSummonDeployShotCount >= 2;
        if (!summonCooldownReady) return false;

        var summonIndex = player.SummonSlotsUsed;
        if (!hasFinalRush &&
            player.RevealedCellCount < 5 * (summonIndex + 1))
            return false;

        var regions = player.Board.PlacedShips
            .Where(ship => !ship.Statuses.Contains(ShipStatusType.Capture))
            .SelectMany(ship => ship.Regions)
            .ToHashSet();
        var normalSlotAvailable = player.SummonSlotsUsed < player.MaxSummonSlots;
        if (normalSlotAvailable &&
            regions.Contains(Region.South) &&
            player.Board.PlacedShips.Any(ship =>
                ship.Statuses.Contains(ShipStatusType.Devastated) &&
                !ship.Statuses.Contains(ShipStatusType.Capture)))
            return true;
        if (!Enumerable.Range(0, 10).Any(col => EntryIsOpen(0, col))) return false;
        if (normalSlotAvailable &&
            (regions.Contains(Region.West) ||
             regions.Contains(Region.East) ||
             regions.Contains(Region.South)))
            return true;

        return !player.BranderUsed && player.Board.PlacedShips.Any(ship =>
            !ship.IsDestroyed &&
            !ship.Statuses.Contains(ShipStatusType.Capture) &&
            ship.Abilities.Contains("brander_summon"));
    }

    private static bool HasFinalRush(BattleshipGame game, BattleshipPlayer player) =>
        game?.Phase == BsGamePhase.Boarding &&
        player != null &&
        game.BoardingPlayerId == player.DiscordId;

    private static PendingManeuverDto GetPendingManeuver(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        if (player == null ||
            game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding) ||
            HasPendingCursedBoatDirection(game) ||
            game.CurrentTurnPlayerId != player.DiscordId ||
            player.HasShotThisTurn ||
            player.HasPenalty ||
            player.StunShotExpiry >= game.ShotCount ||
            GetPendingAssembly(game, player) != null ||
            HasPendingMatryoshkaDeployments(game) ||
            HasPendingBoardingDeployments(game))
            return null;

        foreach (var ship in player.Board.PlacedShips
                     .Where(ship => !ship.IsDestroyed &&
                                    !ship.IsSummon &&
                                    !ship.HasManeuvered &&
                                    !ship.Statuses.Contains(ShipStatusType.Capture) &&
                                    ship.Abilities.Contains("manual_move_after_hit") &&
                                    ship.Decks.Any(deck => deck.IsDestroyed))
                     .OrderBy(ship => ship.Id, StringComparer.Ordinal))
        {
            var options = BattleshipGameEngine.GetManualMoveOptions(player, ship);
            if (options.Count == 0) continue; // no legal direction: this stage is auto-skipped
            return new PendingManeuverDto
            {
                ShipId = ship.Id,
                ShipName = ship.Name,
                Options = options.Select(option => new ManeuverOptionDto
                {
                    Direction = option.Direction.ToString(),
                    Distance = option.Distance,
                    Row = option.Row,
                    Col = option.Col,
                }).ToList(),
            };
        }
        return null;
    }

    private static string GetManeuverLockError(BattleshipGame game, BattleshipPlayer player) =>
        GetPendingManeuver(game, player) == null
            ? null
            : "Сначала выполните обязательный манёвр по подсвеченной клетке.";

    private static List<VoluntaryManeuverDto> GetVoluntaryManeuvers(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        var result = new List<VoluntaryManeuverDto>();
        if (player == null ||
            game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding) ||
            game.CurrentTurnPlayerId != player.DiscordId ||
            player.HasPenalty ||
            player.StunShotExpiry >= game.ShotCount ||
            HasWaitingRamReturn(player) ||
            HasPendingCursedBoatDirection(game) ||
            HasPendingMatryoshkaDeployments(game) ||
            HasPendingBoardingDeployments(game) ||
            GetPendingAssembly(game, player) != null ||
            GetPendingManeuver(game, player) != null)
            return result;

        foreach (var ship in player.Board.PlacedShips
                     .Where(ship =>
                         !ship.IsDestroyed &&
                         !ship.IsSummon &&
                         !ship.HasManeuvered &&
                         ship.Abilities.Contains("merge_maneuver") &&
                         !ship.Statuses.Any(status => status is
                             ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze))
                     .OrderBy(ship => ship.Id, StringComparer.Ordinal))
        {
            var options = BattleshipGameEngine.GetManualMoveOptions(player, ship);
            result.Add(new VoluntaryManeuverDto
            {
                ShipId = ship.Id,
                ShipName = ship.Name,
                Options = options.Select(option => new ManeuverOptionDto
                {
                    Direction = option.Direction.ToString(),
                    Distance = option.Distance,
                    Row = option.Row,
                    Col = option.Col,
                }).ToList(),
            });
        }
        return result;
    }

    public (bool success, string error) AssembleShip(
        string gameId,
        string discordId,
        string groupId,
        int row,
        int col,
        string orientationStr)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            var player = game.GetPlayer(discordId);
            if (player == null) return (false, "Вы не в этой игре.");
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);
            var pending = GetPendingAssembly(game, player);
            if (pending == null || pending.GroupId != groupId)
                return (false, "Этот корабль сейчас нельзя собрать.");
            if (!Enum.TryParse<Orientation>(orientationStr, true, out var orientation) ||
                pending.Options.All(option =>
                    option.Row != row || option.Col != col ||
                    !option.Orientation.Equals(orientation.ToString(), StringComparison.Ordinal)))
                return (false, "Выберите подсвеченное свободное место.");

            var components = player.Board.PlacedShips
                .Where(ship => ship.IsAssemblyComponent && ship.AssemblyGroupId == groupId)
                .ToList();
            if (components.Count != 3)
                return (false, "Компоненты собирающегося корабля не найдены.");

            var survivingComponentCells = components
                .Where(component => !component.IsDestroyed)
                .SelectMany(component => component.GetOccupiedCells())
                .Distinct()
                .ToList();
            var opponent = game.GetOpponent(discordId);
            var opponentHasLivingMast = HasLivingMast(opponent);
            foreach (var component in components)
            {
                foreach (var cell in player.Board.Grid.Cast<Cell>())
                {
                    if (cell.ShipRef != component) continue;
                    if (component.IsDestroyed && opponentHasLivingMast)
                    {
                        cell.IsHit = false;
                        cell.IsMiss = cell.IsRevealed;
                        cell.WasShipHit = false;
                        cell.WasScratched = false;
                        cell.WasRevealedShip = false;
                        cell.SunkShipName = null;
                        cell.KnownShipId = null;
                        cell.KnownDeckIndex = -1;
                    }
                    cell.ShipRef = null;
                }
                player.Board.PlacedShips.Remove(component);
                player.Fleet.Remove(component);
            }

            var definition = ShipCatalog.GetById("famous_assembling_ship");
            var assembled = ShipCatalog.CreateAssembledShip(definition);
            assembled.HasHiddenMovement = true;
            assembled.ManeuverStaleHitCells.AddRange(survivingComponentCells);
            player.Fleet.Add(assembled);
            PlaceShipOnBoard(player, assembled, row, col, orientation);

            if (opponentHasLivingMast)
            {
                game.AddLogFor(opponent.DiscordId,
                    "[Мачта] “Капитан, невиданное диво, корабли собрались в один…”");
            }

            game.LastActivity = DateTime.UtcNow;
            return (true, null);
        }
    }

    public (bool success, string error) DeployMatryoshka(
        string gameId,
        string discordId,
        string parentShipId,
        int row,
        int col,
        string orientationStr)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding))
                return (false, "Сейчас не фаза боя.");
            var player = game.GetPlayer(discordId);
            if (player == null) return (false, "Вы не в этой игре.");
            var pending = player.PendingMatryoshka;
            if (pending == null || pending.ParentShipId != parentShipId)
                return (false, "Выберите подсвеченное свободное место.");
            if (!Enum.TryParse<Orientation>(orientationStr, true, out var orientation))
                return (false, "Неверная ориентация.");

            var option = pending.Options.FirstOrDefault(value =>
                value.Row == row && value.Col == col && value.Orientation == orientation);
            if (option == null)
                return (false, "Выберите подсвеченное свободное место.");

            var parent = player.Board.PlacedShips.FirstOrDefault(ship =>
                ship.Id == pending.ParentShipId && ship.IsDestroyed);
            if (parent == null || option.Cells.Any(position =>
                    player.Board.GetCell(position.row, position.col)?.ShipRef?.Id != parent.Id))
                return (false, "Выберите подсвеченное свободное место.");

            foreach (var deck in parent.Decks)
            {
                var position = parent.GetDeckCell(
                    deck, parent.Row, parent.Col, parent.Orientation);
                BattleshipGameEngine.MarkMatryoshkaWreck(
                    player.Board.GetCell(position.row, position.col),
                    parent,
                    deck.Index);
            }

            RemoveShipFromBoard(player, parent);
            player.Fleet.Remove(parent);

            var child = ShipCatalog.CreateMatryoshkaStageShip(pending.ChildDeckCount);
            player.Fleet.Add(child);
            var childAnchor = option.Cells[0];
            PlaceShipOnBoard(
                player,
                child,
                childAnchor.row,
                childAnchor.col,
                option.Orientation);

            foreach (var deck in child.Decks)
            {
                var position = child.GetDeckCell(deck, child.Row, child.Col, child.Orientation);
                var cell = player.Board.GetCell(position.row, position.col);
                if (cell == null) continue;
                BattleshipGameEngine.ClearCellWreck(cell);
                cell.IsRevealed = true;
                cell.IsHit = false;
                cell.IsMiss = false;
                cell.WasShipHit = false;
                cell.WasScratched = false;
                cell.WasRevealedShip = true;
                cell.BurnResistMarked = false;
                cell.WasDodge = false;
                cell.WasManeuverDodge = false;
                cell.SunkShipName = null;
                cell.KnownShipId = child.Id;
                cell.KnownDeckIndex = deck.Index;
            }

            player.PendingMatryoshka = null;
            BattleshipGameEngine.ResolveMatryoshkaPlacementInteractions(
                game, player, child);
            game.LastActivity = DateTime.UtcNow;
            ResumeMatryoshkaResolution(game);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    private static PendingCursedBoatDirectionDto GetPendingCursedBoatDirection(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        if (HasPendingMatryoshkaDeployments(game)) return null;
        var summon = player?.Summons
            .Where(value => value.IsAlive &&
                            (value.Type == SummonType.CursedBoat ||
                             value.IsBoardingShip &&
                             value.BoardingAbilities.Contains("spawn_cursed_boat")) &&
                            value.WaitingForDirectionChoice)
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (summon == null) return null;

        var directions = new[]
        {
            (Direction.Up, -1, 0),
            (Direction.Down, 1, 0),
            (Direction.Left, 0, -1),
            (Direction.Right, 0, 1),
        };
        var targetBoard = game?.GetOpponent(player.DiscordId)?.Board;
        return new PendingCursedBoatDirectionDto
        {
            SummonId = summon.Id,
            Row = summon.Row,
            Col = summon.Col,
            Options = directions
                .Select(value => new CursedBoatDirectionOptionDto
                {
                    Direction = value.Item1.ToString(),
                    Row = summon.Row + value.Item2,
                    Col = summon.Col + value.Item3,
                })
                .Where(value =>
                {
                    if (value.Row is < 0 or >= 10 || value.Col is < 0 or >= 10)
                        return false;
                    if (targetBoard == null ||
                        !Enum.TryParse<Direction>(value.Direction, out var direction))
                        return false;
                    return BattleshipGameEngine.CanFitSummonHullInBounds(
                        summon, value.Row, value.Col, direction);
                })
                .ToList(),
        };
    }

    private static bool HasPendingCursedBoatDirection(BattleshipGame game) =>
        game.GetPlayers().Any(player => GetPendingCursedBoatDirection(game, player) != null);

    private static string GetCursedBoatDirectionLockError(BattleshipGame game) =>
        HasPendingCursedBoatDirection(game)
            ? "Сначала выберите подсвеченную клетку для нового курса Проклятой лодки."
            : null;

    private void CompleteActionResolution(BattleshipGame game, bool turnContinues, bool moveSummons)
    {
        CheckAndApplyFleetDestructionWin(game);
        TryTriggerBoarding(game);
        CheckAndApplyWin(game);
        if (!game.IsFinished && HasPendingMatryoshkaDeployments(game))
        {
            game.MatryoshkaResolutionPaused = true;
            game.PausedTurnContinues = turnContinues;
            game.PausedMoveSummons = moveSummons;
            return;
        }
        if (!game.IsFinished && HasPendingBoardingDeployments(game))
        {
            game.BoardingResolutionPaused = true;
            game.PausedTurnContinues = turnContinues;
            game.PausedMoveSummons = moveSummons;
            return;
        }
        if (!game.IsFinished && moveSummons)
        {
            var movementCompleted = BattleshipGameEngine.MoveSummons(game, currentGame =>
            {
                QueueMatryoshkaReplacements(currentGame);
                return HasPendingMatryoshkaDeployments(currentGame);
            });
            if (!movementCompleted)
            {
                game.MatryoshkaResolutionPaused = true;
                game.PausedTurnContinues = turnContinues;
                game.PausedMoveSummons = true;
                return;
            }
        }
        CheckAndApplyFleetDestructionWin(game);
        TryTriggerBoarding(game);
        CheckAndApplyWin(game);
        if (!game.IsFinished && HasPendingMatryoshkaDeployments(game))
        {
            game.MatryoshkaResolutionPaused = true;
            game.PausedTurnContinues = turnContinues;
            game.PausedMoveSummons = false;
            return;
        }
        if (!game.IsFinished && HasPendingBoardingDeployments(game))
        {
            game.BoardingResolutionPaused = true;
            game.PausedTurnContinues = turnContinues;
            game.PausedMoveSummons = false;
            return;
        }
        if (!game.IsFinished && !turnContinues)
            AdvanceTurn(game);
    }

    private void ResumeBoardingResolution(BattleshipGame game)
    {
        if (!game.BoardingResolutionPaused || HasPendingBoardingDeployments(game) || game.IsFinished) return;
        if (HasPendingMatryoshkaDeployments(game))
        {
            game.BoardingResolutionPaused = false;
            game.MatryoshkaResolutionPaused = true;
            return;
        }
        var turnContinues = game.PausedTurnContinues;
        var moveSummons = game.PausedMoveSummons;
        game.BoardingResolutionPaused = false;
        game.PausedTurnContinues = false;
        game.PausedMoveSummons = false;

        CompleteActionResolution(game, turnContinues, moveSummons);
    }

    private void ResumeMatryoshkaResolution(BattleshipGame game)
    {
        QueueMatryoshkaReplacements(game);
        if (HasPendingMatryoshkaDeployments(game) || game.IsFinished) return;

        if (!game.MatryoshkaResolutionPaused)
        {
            ResolveImmediateEffectTransitions(game);
            return;
        }

        var turnContinues = game.PausedTurnContinues;
        var moveSummons = game.PausedMoveSummons;
        game.MatryoshkaResolutionPaused = false;
        game.PausedTurnContinues = false;
        game.PausedMoveSummons = false;
        CompleteActionResolution(game, turnContinues, moveSummons);
    }

    private void CheckAndApplyFleetDestructionWin(BattleshipGame game)
    {
        QueueMatryoshkaReplacements(game);
        if (HasPendingMatryoshkaDeployments(game)) return;
        var (gameOver, winnerId) = BattleshipGameEngine.CheckFleetDestructionWin(game);
        if (!gameOver) return;
        game.IsFinished = true;
        game.WinnerId = winnerId;
        game.Phase = BsGamePhase.GameOver;
        game.AddLog($"Победитель: {game.GetPlayer(winnerId)?.Username ?? "???"}!");
    }

    private static void TryTriggerBoarding(BattleshipGame game)
    {
        if (HasPendingMatryoshkaDeployments(game)) return;
        if (!game.IsFinished && BattleshipGameEngine.CheckBoardingTrigger(game))
            BattleshipGameEngine.TriggerBoarding(game);
        if (!game.IsFinished && game.Phase == BsGamePhase.Boarding)
        {
            BattleshipGameEngine.QueueBoardingShipsForMidlessPlayers(game);
            foreach (var player in game.GetPlayers().Where(player =>
                         HasMandatoryBoardingDeployment(player) &&
                         !HasLegalMandatoryBoardingDeployment(game, player)))
                BattleshipGameEngine.DiscardMandatoryBoardingRemainder(game, player);
        }
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
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);
            if (HasPendingBoardingDeployments(game))
                return (false, "Сначала выпустите все обязательные единицы абордажа!");
            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null) return (false, cursedDirectionError);
            var assemblyError = GetAssemblyLockError(game, player);
            if (assemblyError != null) return (false, assemblyError);
            var maneuverError = GetManeuverLockError(game, player);
            if (maneuverError != null) return (false, maneuverError);
            if (BattleshipGameEngine.HasAnyLegalShot(game, player)) return (false, "У вас есть доступный выстрел.");

            var firstResolutionLogIndex = game.GameLog.Count;
            CompleteActionResolution(game, turnContinues: false, moveSummons: true);
            GateNewBoardDetailLogs(
                game,
                player,
                game.GetOpponent(player.DiscordId),
                firstResolutionLogIndex);
            game.LastActivity = DateTime.UtcNow;
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    /// <summary>
    /// Map WeaponType to the default ShotType for combat resolution.
    /// Tetracatapult defaults to Buckshot; White Stone is its placement-selected alternative.
    /// </summary>
    private static ShotType WeaponTypeToShotType(WeaponType wt)
    {
        return wt switch
        {
            WeaponType.Tetracatapult => ShotType.Buckshot,
            WeaponType.Incendiary => ShotType.Incendiary,
            WeaponType.EvilIncendiary => ShotType.EvilIncendiary,
            WeaponType.GreekFire => ShotType.GreekFire,
            WeaponType.EvilGreekFire => ShotType.EvilGreekFire,
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
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);
            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null) return (false, cursedDirectionError);
            var assemblyError = GetAssemblyLockError(game, player);
            if (assemblyError != null) return (false, assemblyError);
            var maneuverError = GetManeuverLockError(game, player);
            if (maneuverError != null) return (false, maneuverError);

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

            // Ballista remains the baseline action; every special must resolve to a real,
            // living, loaded weapon. This closes forged Greek Fire/Incendiary selections.
            var usable = BattleshipGameEngine.GetUsableWeapons(game, player, wt).ToList();
            // Ballista is a shared baseline action; an exact source id is meaningful only
            // for special weapons. The visual source is selected when the shot resolves.
            var exactWeaponSelection = weaponId != null &&
                                       !(wt == WeaponType.Tetracatapult &&
                                         player.UseSharedTetracatapultAmmo);
            var selectedWeapon = wt == WeaponType.Ballista
                ? usable.Select(x => x.weapon).FirstOrDefault()
                : exactWeaponSelection
                    ? usable.Select(x => x.weapon).FirstOrDefault(w => w.Id == weaponId)
                    : wt == WeaponType.Tetracatapult
                        ? usable.Select(x => x.weapon)
                            .Where(w => w.ConfiguredShotType == selectedShotType)
                            .OrderBy(w => Math.Max(0, w.AimSpeed - player.RevealedCellCount))
                            .FirstOrDefault()
                        : usable.Select(x => x.weapon).FirstOrDefault();
            if (selectedWeapon == null)
                return (false, "Это оружие уничтожено или у него закончились боеприпасы.");
            if (wt == WeaponType.Tetracatapult &&
                selectedWeapon.ConfiguredShotType != selectedShotType)
                return (false, "Этот Тетракамнемёт заряжен другим типом снаряда.");
            if (selectedWeapon.AimSpeed > player.RevealedCellCount)
                return (false, $"Оружие ещё заряжается! Нужно разведать {selectedWeapon.AimSpeed - player.RevealedCellCount} клеток.");

            player.SelectedShotType = selectedShotType;
            player.SelectedWeapon = selectedWeapon;
            return (true, null);
        }
    }

    public (bool success, string error) DeploySummon(
        string gameId,
        string discordId,
        string summonTypeStr,
        int col,
        string summonId = null)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            // INVARIANT: deployment is never gated on turn ownership or on the shooter's reset
            // reload. A defender must be able to answer at any moment of an enemy turn, including
            // while an enemy shot is still resolving or animating. Do NOT add a
            // `game.CurrentTurnPlayerId` or `GetShotDelayError` check here or in DeployPendingSummon
            // — that is the entire reason the long reset window exists.
            if (game.Phase != BsGamePhase.Combat && game.Phase != BsGamePhase.Boarding)
                return (false, "Сейчас не фаза боя.");

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");
            var hasFinalRush = HasFinalRush(game, player);

            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);

            if (!Enum.TryParse<SummonType>(summonTypeStr, true, out var summonType))
                return (false, "Неизвестный тип призыва.");

            var hasWaitingRam = HasWaitingRamReturn(player);
            var waitingSummon = player.Summons.FirstOrDefault(s =>
                s.WaitingForTurnBack && s.IsAlive && s.Type == SummonType.Ram &&
                s.Type == summonType &&
                (summonId == null || s.Id == summonId));
            if (hasWaitingRam && waitingSummon == null)
                return (false, "Сначала верните ожидающий разворота Таран.");

            var mandatoryBarrier = HasPendingBoardingDeployments(game);
            var mandatoryOrdinary =
                !hasWaitingRam &&
                mandatoryBarrier &&
                game.Phase == BsGamePhase.Boarding &&
                summonType is SummonType.Ram or SummonType.Scout or SummonType.PirateBoat &&
                player.MandatoryBoardingSummonSlots > 0;
            var mandatoryBrander =
                !hasWaitingRam &&
                mandatoryBarrier &&
                game.Phase == BsGamePhase.Boarding &&
                summonType == SummonType.Brander &&
                player.MandatoryBoardingBrander;
            if (mandatoryBarrier && waitingSummon == null &&
                !mandatoryOrdinary && !mandatoryBrander)
                return (false, "Сначала выпустите все обязательные единицы абордажа.");

            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null && waitingSummon == null &&
                !mandatoryOrdinary && !mandatoryBrander)
                return (false, cursedDirectionError);
            var assemblyError = GetAssemblyLockError(game, player);
            if (assemblyError != null && waitingSummon == null &&
                !mandatoryOrdinary && !mandatoryBrander)
                return (false, assemblyError);
            var maneuverError = GetManeuverLockError(game, player);
            if (maneuverError != null && waitingSummon == null &&
                !mandatoryOrdinary && !mandatoryBrander)
                return (false, maneuverError);

            if (summonId != null && waitingSummon == null)
                return (false, "Ожидающий разворота призыв не найден.");

            // ТЗ #10: Brander is outside the four normal uses (its own cap is below)
            if (waitingSummon == null && summonType != SummonType.Brander &&
                !mandatoryOrdinary && player.SummonSlotsUsed >= player.MaxSummonSlots)
                return (false, "Лимит обычных призывов исчерпан.");

            // Brander requires the boiler upgrade on Tetranavis
            if (waitingSummon == null && summonType == SummonType.Brander && !mandatoryBrander &&
                !player.Board.PlacedShips.Any(s => !s.IsDestroyed &&
                    !s.Statuses.Contains(ShipStatusType.Capture) && s.Abilities.Contains("brander_summon")))
                return (false, "Для призыва Брандера нужен апгрейд Котельной.");

            // Region check: Ram requires West, Scout requires East, PirateBoat requires South
            var regionShips = mandatoryOrdinary ? player.Fleet : player.Board.PlacedShips;
            var playerRegions = regionShips
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
            var firstResolutionLogIndex = game.GameLog.Count;

            // Deployment threshold: need 5 revealed cells per summon index
            var summonIndex = player.SummonSlotsUsed;
            if (waitingSummon == null &&
                player.RevealedCellCount < 5 * (summonIndex + 1) && !hasFinalRush)
                return (false, $"Нужно разведать ещё {5 * (summonIndex + 1) - player.RevealedCellCount} клеток для призыва.");

            // Deployment cooldown: 2 shots between deployments
            if (game.ShotCount - player.LastSummonDeployShotCount < 2 && !hasFinalRush)
                return (false, "Слишком рано для нового призыва (перезарядка 2 выстрела).");

            // Re-send waiting summon (turn-back)
            if (waitingSummon != null)
            {
                var isHorizontal = waitingSummon.MoveDirection is Direction.Left or Direction.Right;
                int reentryRow;
                int reentryCol;
                Direction reentryDirection;

                if (isHorizontal)
                {
                    // For horizontal CursedBoat: 'col' param = row to enter at, must be adjacent to current row
                    if (Math.Abs(waitingSummon.Row - col) > 1)
                        return (false, "Можно отправить только в соседний ряд.");

                    reentryRow = col;
                    reentryCol = waitingSummon.MoveDirection == Direction.Right ? 9 : 0;
                    reentryDirection = waitingSummon.MoveDirection == Direction.Right
                        ? Direction.Left
                        : Direction.Right;
                }
                else
                {
                    // For vertical movement: 'col' param = column to enter at, must be adjacent
                    if (Math.Abs(waitingSummon.Col - col) > 1)
                        return (false, "Можно отправить только в соседнюю колонку.");

                    reentryRow = waitingSummon.MoveDirection == Direction.Down ? 9 : 0;
                    reentryCol = col;
                    reentryDirection = waitingSummon.MoveDirection == Direction.Down
                        ? Direction.Up
                        : Direction.Down;
                }

                if (opponent == null || !BattleshipGameEngine.CanPlaceSummonOnBoard(
                        opponent.Board,
                        waitingSummon,
                        reentryRow,
                        reentryCol,
                        reentryDirection))
                    return (false, "Корпус не помещается на выбранной клетке входа.");

                var previousRow = waitingSummon.Row;
                var previousCol = waitingSummon.Col;
                var previousDirection = waitingSummon.MoveDirection;
                var previousSpawnedAtShot = waitingSummon.SpawnedAtShot;
                var previousIsGhost = waitingSummon.IsGhost;
                waitingSummon.WaitingForTurnBack = false;
                waitingSummon.Row = reentryRow;
                waitingSummon.Col = reentryCol;
                waitingSummon.MoveDirection = reentryDirection;
                waitingSummon.SpawnedAtShot = game.ShotCount;
                waitingSummon.IsGhost = player.UseGhostSummons;
                if (!BattleshipGameEngine.RegisterSummonOnTargetBoard(game, player, waitingSummon))
                {
                    waitingSummon.WaitingForTurnBack = true;
                    waitingSummon.Row = previousRow;
                    waitingSummon.Col = previousCol;
                    waitingSummon.MoveDirection = previousDirection;
                    waitingSummon.SpawnedAtShot = previousSpawnedAtShot;
                    waitingSummon.IsGhost = previousIsGhost;
                    return (false, "Корпус не помещается на выбранной клетке входа.");
                }
                ResolveImmediateEffectTransitions(game);

                player.LastSummonDeployShotCount = game.ShotCount;
                game.LastActivity = DateTime.UtcNow;
                game.AddLog($"{player.Username} перенаправил {summonType}!");

                // Mast warning (personal — it's their mast)
                if (HasLivingMast(opponent))
                {
                    var warning = BattleshipGameEngine.GenerateMastWarning(
                        opponent, summonType, waitingSummon.Row, waitingSummon.Col);
                    if (warning != null) game.AddLogFor(opponent.DiscordId, warning);
                }

                GateNewBoardDetailLogs(game, player, opponent, firstResolutionLogIndex);
                TrySettleGameEnd(game);
                return (true, null);
            }

            // ТЗ #10: Brander — максимум 1 за матч (redirect of a waiting one is handled above)
            if (summonType == SummonType.Brander && player.BranderUsed && !mandatoryBrander)
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
                IsGhost = player.UseGhostSummons,
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
            {
                player.BranderUsed = true; // ТЗ #10: вне лимита обычных призывов, 1 раз за матч
                if (mandatoryBrander)
                    player.MandatoryBoardingBrander = false;
            }
            else
            {
                player.SummonSlotsUsed++;
                if (mandatoryOrdinary)
                    player.MandatoryBoardingSummonSlots--;
            }
            if (mandatoryOrdinary || mandatoryBrander)
                CompleteMandatoryBoardingDeployment(game, player);
            player.LastSummonDeployShotCount = game.ShotCount;
            game.LastActivity = DateTime.UtcNow;

            BattleshipGameEngine.RegisterSummonOnTargetBoard(game, player, summon);
            ResolveImmediateEffectTransitions(game);

            game.AddLog($"{player.Username} развернул {summonType}! ({(char)('A' + col)}1)");

            // Mast warning for opponent (ТЗ #3: include spawn cell; personal — it's their mast)
            if (HasLivingMast(opponent))
            {
                var warning = BattleshipGameEngine.GenerateMastWarning(opponent, summonType, summon.Row, summon.Col);
                if (warning != null) game.AddLogFor(opponent.DiscordId, warning);
            }

            ResumeBoardingResolution(game);
            GateNewBoardDetailLogs(game, player, opponent, firstResolutionLogIndex);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    /// <summary>
    /// Deploy a pending summon (pirate/cursed boat from ship death, or boarding ship).
    /// Free pending units bypass cooldown/revelation; any future paid pending unit follows
    /// ordinary cadence unless its owner holds final_rush. Column restricted for pirate/cursed.
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
            var hasFinalRush = HasFinalRush(game, player);

            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);

            var pending = player.PendingSummons.FirstOrDefault(p => p.Id == pendingId);
            if (pending == null)
                return (false, "Нет такого ожидающего призыва.");
            var mandatoryBarrier = HasPendingBoardingDeployments(game);
            if (mandatoryBarrier && !pending.IsMandatoryBoarding)
                return (false, "Сначала выпустите все обязательные единицы абордажа.");
            if (HasWaitingRamReturn(player))
                return (false, "Сначала верните ожидающий разворота Таран.");
            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null && !pending.IsMandatoryBoarding)
                return (false, cursedDirectionError);
            var assemblyError = GetAssemblyLockError(game, player);
            if (assemblyError != null && !pending.IsMandatoryBoarding) return (false, assemblyError);
            var maneuverError = GetManeuverLockError(game, player);
            if (maneuverError != null && !pending.IsMandatoryBoarding) return (false, maneuverError);

            if (col < 0 || col >= 10)
                return (false, "Неверная колонка.");

            // Column restriction for pirate/cursed boat
            if (pending.AllowedColumns.Count > 0 && !pending.AllowedColumns.Contains(col))
                return (false, $"Можно разместить только в колонках: {string.Join(", ", pending.AllowedColumns.Select(c => (char)('A' + c)))}");

            var legalColumns = GetLegalPendingSummonColumns(game, player, pending);
            if (!legalColumns.Contains(col))
            {
                if (!player.IsBot || legalColumns.Count == 0)
                    return (false, "Корпус призыва не помещается в выбранной колонке.");
                col = legalColumns[Rng.Next(legalColumns.Count)];
            }

            if (!pending.IsFree && player.SummonSlotsUsed >= player.MaxSummonSlots)
                return (false, "Лимит обычных призывов исчерпан.");
            if (!pending.IsFree && !hasFinalRush)
            {
                var summonIndex = player.SummonSlotsUsed;
                if (player.RevealedCellCount < 5 * (summonIndex + 1))
                    return (false,
                        $"Нужно разведать ещё {5 * (summonIndex + 1) - player.RevealedCellCount} клеток для призыва.");
                if (game.ShotCount - player.LastSummonDeployShotCount < 2)
                    return (false, "Слишком рано для нового призыва (перезарядка 2 выстрела).");
            }

            var opponent = game.GetOpponent(discordId);
            if (opponent == null)
                return (false, "Противник не найден.");
            var firstResolutionLogIndex = game.GameLog.Count;

            var summon = BattleshipGameEngine.CreateSummonFromPending(
                pending,
                discordId,
                row: 0,
                col,
                Direction.Down,
                game.ShotCount,
                player.UseGhostSummons);
            if (summon == null ||
                !BattleshipGameEngine.CanPlaceSummonOnBoard(
                    opponent.Board, summon, summon.Row, summon.Col, summon.MoveDirection))
                return (false, "Корпус призыва не помещается в выбранной колонке.");

            player.Summons.Add(summon);
            if (!BattleshipGameEngine.RegisterSummonOnTargetBoard(game, player, summon))
            {
                player.Summons.Remove(summon);
                return (false, "Клетки входа заняты другим призывом.");
            }
            if (!pending.IsFree)
            {
                player.SummonSlotsUsed++;
                player.LastSummonDeployShotCount = game.ShotCount;
            }
            player.PendingSummons.Remove(pending);
            if (pending.IsMandatoryBoarding)
                CompleteMandatoryBoardingDeployment(game, player);
            game.LastActivity = DateTime.UtcNow;

            ResolveImmediateEffectTransitions(game);

            game.AddLog($"{player.Username} выпустил {pending.SourceShipName ?? pending.Type.ToString()}! ({(char)('A' + col)}1)");

            // Mast warning (personal — it's their mast)
            if (HasLivingMast(opponent))
            {
                var warning = pending.IsBoarding
                    ? _messageCatalog.Render(
                        new LocalizedMessage(
                            "battleship.mast.boardingShipIncoming",
                            new Dictionary<string, string>
                            {
                                ["coordinate"] = $"{(char)('A' + summon.Col)}{summon.Row + 1}",
                            }),
                        GetPlayerLanguage(opponent.DiscordId))
                    : BattleshipGameEngine.GenerateMastWarning(
                        opponent, pending.Type, summon.Row, summon.Col);
                if (warning != null) game.AddLogFor(opponent.DiscordId, warning);
            }

            ResumeBoardingResolution(game);
            GateNewBoardDetailLogs(game, player, opponent, firstResolutionLogIndex);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    public (bool success, string error) RestoreShipWithPirateBoat(
        string gameId,
        string discordId,
        string shipId)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding))
                return (false, "Сейчас не фаза боя.");
            var player = game.GetPlayer(discordId);
            if (player == null) return (false, "Вы не в этой игре.");
            var hasFinalRush = HasFinalRush(game, player);
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);
            var ship = player.Board.PlacedShips.FirstOrDefault(candidate =>
                candidate.Id == shipId &&
                candidate.Statuses.Contains(ShipStatusType.Devastated) &&
                !candidate.Statuses.Contains(ShipStatusType.Capture));
            if (ship == null)
                return (false, "Этот корабль нельзя восстановить Пиратской лодкой.");

            var mandatoryBarrier = HasPendingBoardingDeployments(game);
            var mandatoryPirate =
                mandatoryBarrier &&
                game.Phase == BsGamePhase.Boarding &&
                player.MandatoryBoardingSummonSlots > 0 &&
                player.Fleet.SelectMany(candidate => candidate.Regions).Contains(Region.South);
            if (mandatoryBarrier && !mandatoryPirate)
                return (false, "Сначала выпустите все обязательные единицы абордажа.");
            if (HasWaitingRamReturn(player))
                return (false, "Сначала верните ожидающий разворота Таран.");

            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null && !mandatoryPirate)
                return (false, cursedDirectionError);
            var assemblyError = GetAssemblyLockError(game, player);
            if (assemblyError != null && !mandatoryPirate)
                return (false, assemblyError);
            var maneuverError = GetManeuverLockError(game, player);
            if (maneuverError != null && !mandatoryPirate)
                return (false, maneuverError);

            if (!mandatoryPirate && player.SummonSlotsUsed >= player.MaxSummonSlots)
                return (false, "Лимит обычных призывов исчерпан.");
            var regionShips = mandatoryPirate ? player.Fleet : player.Board.PlacedShips;
            if (!regionShips.SelectMany(candidate => candidate.Regions).Contains(Region.South))
                return (false, "Для Пиратской лодки нужен флот из региона Юг.");
            var summonIndex = player.SummonSlotsUsed;
            if (!hasFinalRush &&
                player.RevealedCellCount < 5 * (summonIndex + 1))
                return (false,
                    $"Нужно разведать ещё {5 * (summonIndex + 1) - player.RevealedCellCount} клеток для призыва.");
            if (!hasFinalRush &&
                game.ShotCount - player.LastSummonDeployShotCount < 2)
                return (false, "Слишком рано для нового призыва (перезарядка 2 выстрела).");

            var firstResolutionLogIndex = game.GameLog.Count;
            if (!BattleshipGameEngine.RestoreDevastatedShip(game, player, ship, fullRepair: true))
                return (false, "Корабль уже нельзя восстановить.");

            player.SummonSlotsUsed++;
            if (mandatoryPirate)
            {
                player.MandatoryBoardingSummonSlots--;
                CompleteMandatoryBoardingDeployment(game, player);
            }
            player.LastSummonDeployShotCount = game.ShotCount;
            game.LastActivity = DateTime.UtcNow;
            game.AddLog($"{player.Username} восстановил {ship.Name} Пиратской лодкой!");

            ResolveImmediateEffectTransitions(game);
            ResumeBoardingResolution(game);
            GateNewBoardDetailLogs(
                game,
                player,
                game.GetOpponent(player.DiscordId),
                firstResolutionLogIndex);
            TrySettleGameEnd(game);
            return (true, null);
        }
    }

    /// <summary>
    /// Deployment and re-entry resolve fire/freeze/collision immediately, outside the
    /// ordinary shot pipeline. Re-run the same terminal/Boarding transitions so a summon
    /// that destroys the last Mid cannot leave the game stranded in Combat.
    /// </summary>
    private void ResolveImmediateEffectTransitions(BattleshipGame game)
    {
        CheckAndApplyFleetDestructionWin(game);
        TryTriggerBoarding(game);
        if (!game.IsFinished &&
            !HasPendingMatryoshkaDeployments(game) &&
            game.Phase == BsGamePhase.Boarding)
        {
            BattleshipGameEngine.QueueBoardingShipsForMidlessPlayers(game);
            foreach (var player in game.GetPlayers().Where(player =>
                         HasMandatoryBoardingDeployment(player) &&
                         !HasLegalMandatoryBoardingDeployment(game, player)))
                BattleshipGameEngine.DiscardMandatoryBoardingRemainder(game, player);
        }
        CheckAndApplyWin(game);
    }

    public (bool success, string error) ManualMoveShip(string gameId, string discordId, string shipId, string directionStr, int distance = 1)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            if (game.Phase is not (BsGamePhase.Combat or BsGamePhase.Boarding))
                return (false, "Манёвр доступен только во время боя.");
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);
            if (HasPendingBoardingDeployments(game))
                return (false, "Сначала выпустите все обязательные единицы абордажа.");
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");
            var cursedDirectionError = GetCursedBoatDirectionLockError(game);
            if (cursedDirectionError != null) return (false, cursedDirectionError);
            var assemblyError = GetAssemblyLockError(game, player);
            if (assemblyError != null) return (false, assemblyError);

            if (game.CurrentTurnPlayerId != discordId)
                return (false, "Можно двигаться только в свой ход.");

            var ship = player.Board.PlacedShips.Find(s => s.Id == shipId);
            if (ship == null || ship.IsDestroyed)
                return (false, "Корабль не найден или уничтожен.");

            var isMergingManeuver = ship.Abilities.Contains("merge_maneuver");
            var isMandatoryDamageManeuver = ship.Abilities.Contains("manual_move_after_hit");
            if (!isMergingManeuver && !isMandatoryDamageManeuver)
                return (false, "Этот корабль не может двигаться.");

            if (isMandatoryDamageManeuver && player.HasShotThisTurn)
                return (false, "Маневр возможен только в начале хода.");

            // The original maneuvering ships unlock only after losing a deck. A merging ship
            // deliberately exposes its one-time action throughout its own turn.
            if (isMandatoryDamageManeuver && !ship.Decks.Any(d => d.IsDestroyed))
                return (false, "Корабль не был повреждён. Маневр невозможен.");

            // ТЗ #21: one-time use PER SHIP — a second Maneuvering Double can still move
            if (ship.HasManeuvered)
                return (false, "Этот корабль уже использовал манёвр.");

            if (distance < 1 || distance > 2)
                return (false, "Можно переместиться на 1 или 2 клетки.");

            if (!Enum.TryParse<Direction>(directionStr, true, out var direction))
                return (false, "Неверное направление.");

            var offeredOptions = isMergingManeuver
                ? GetVoluntaryManeuvers(game, player)
                    .FirstOrDefault(maneuver => maneuver.ShipId == ship.Id)?.Options
                : GetPendingManeuver(game, player)?.Options;
            if (offeredOptions == null ||
                offeredOptions.All(option =>
                    !option.Direction.Equals(direction.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    option.Distance != distance))
                return (false, "Этот манёвр сейчас недоступен.");

            var success = BattleshipGameEngine.ManualMoveShip(game, player, ship, direction, distance);
            if (!success)
                return (false, "Невозможно переместить корабль в этом направлении.");

            ship.HasManeuvered = true;
            game.LastActivity = DateTime.UtcNow;
            // ТЗ #20: only the mover sees the move message; the opponent gets their mast warning
            // at hit time (ProcessShipHit), not at move time
            game.AddLogFor(discordId, isMergingManeuver || ship.DefinitionId == "merging_ship_v2"
                ? "Сливающийся корабль маневрирует!"
                : ship.DefinitionId == "famous_ramming_ship"
                    ? "Знаменитый Врезающийся корабль маневрирует!"
                    : "Маневрирующая двойка маневрирует!");

            CheckAndApplyFleetDestructionWin(game);
            if (!game.IsFinished) TryTriggerBoarding(game);
            if (!game.IsFinished) CheckAndApplyWin(game);
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
            var matryoshkaError = GetMatryoshkaLockError(game, discordId);
            if (matryoshkaError != null) return (false, matryoshkaError);
            if (HasPendingBoardingDeployments(game))
                return (false, "Сначала выпустите все обязательные единицы абордажа.");
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            if (!Enum.TryParse<Direction>(directionStr, true, out var direction))
                return (false, "Неверное направление.");

            var pending = GetPendingCursedBoatDirection(game, player);
            if (pending?.SummonId != summonId ||
                pending.Options.All(option =>
                    !option.Direction.Equals(direction.ToString(), StringComparison.OrdinalIgnoreCase)))
                return (false, "Это направление сейчас недоступно.");

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
            return ToDto(game, game.GetPlayer(discordId) == null ? null : discordId);
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

    public bool CanPlayerSeeShotDetails(string gameId, string discordId, ShotResult result)
    {
        if (result == null || result.WasSkipped || string.IsNullOrWhiteSpace(result.TargetPlayerId))
            return true;
        if (!_games.TryGetValue(gameId, out var game)) return false;
        lock (game)
        {
            var player = game.GetPlayer(discordId);
            return player != null &&
                   (player.DiscordId == result.TargetPlayerId || HasLivingMast(player));
        }
    }

    public List<ShotResult> TakePendingTurnSkipEvents(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return new();
        lock (game)
        {
            var events = game.PendingTurnSkipEvents.ToList();
            game.PendingTurnSkipEvents.Clear();
            return events;
        }
    }

    // ── Ship Catalog (for frontend) ──────────────────────────────────

    public List<ShipCatalogDto> GetShipCatalog(Faction? faction = null)
    {
        return ShipCatalog.AllShips
            .Where(def => faction == null || def.Factions.Contains(faction.Value))
            .Select(def => new ShipCatalogDto
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
            Factions = def.Factions.Select(value => value.ToString()).ToList(),
            Description = string.IsNullOrWhiteSpace(def.Description)
                ? null
                : def.IsFree
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
                Description = string.IsNullOrWhiteSpace(u.Description)
                    ? null
                    : $"{u.Description} Цена: {u.Cost} монет.",
                DescriptionKey = u.DescriptionKey,
                IsPreinstalled = u.IsPreinstalled,
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

        var botFleet = BattleshipBotAI.SelectFleet(game.Player2.Faction);
        game.Player2.SelectedShips = botFleet;
        game.Player2.CoinsRemaining = FleetValidator.GetBudget(game.Player2.Faction) -
                                      FleetValidator.CalculateTotalCost(botFleet);
        game.Player2.Fleet.Clear();

        foreach (var sel in botFleet)
        {
            var def = ShipCatalog.GetById(sel.DefinitionId);
            if (def != null)
            {
                if (def.Id == "famous_assembling_ship")
                    game.Player2.Fleet.AddRange(ShipCatalog.CreateAssemblyComponents(def, sel.Upgrades));
                else
                    game.Player2.Fleet.Add(ShipCatalog.CreateShip(def, sel.Upgrades));
            }
        }
        NumberDuplicateShips(game.Player2.Fleet);

        game.Player2.IsReady = true;
    }

    private static void NumberDuplicateShips(List<Ship> fleet)
    {
        foreach (var group in fleet
                     .Where(ship => !ship.IsAssemblyComponent)
                     .GroupBy(s => s.DefinitionId)
                     .Where(g => g.Count() > 1))
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
        foreach (var weapon in game.Player2.Fleet.SelectMany(ship => ship.Weapons)
                     .Where(weapon => weapon.Type == WeaponType.Tetracatapult))
        {
            weapon.ConfiguredShotType = ShotType.Buckshot;
        }
        game.Player2.UseSharedTetracatapultAmmo = true;
        game.Player2.UseGhostSummons = true;
        BattleshipGameEngine.InitializeSharedTetracatapultAmmo(game, game.Player2);
        game.Player2.IsReady = true;
    }

    public bool IsBotTurn(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return false;
        lock (game)
        {
            if (game.IsFinished) return false;
            if (HasPendingMatryoshkaDeployments(game))
                return game.GetPlayers().Any(player =>
                    player.IsBot && player.PendingMatryoshka != null);
            if (HasPendingBoardingDeployments(game))
                return game.GetPlayers().Any(player =>
                    player.IsBot && HasMandatoryBoardingDeployment(player));
            if (game.GetPlayers().Any(player =>
                    !player.IsBot && GetPendingCursedBoatDirection(game, player) != null))
                return false;
            return game.GetPlayers().Any(player =>
                       player.IsBot && GetPendingCursedBoatDirection(game, player) != null) ||
                   game.GetPlayer(game.CurrentTurnPlayerId)?.IsBot == true;
        }
    }

    public int GetCurrentBotShotDelayRemainingMs(string gameId)
    {
        if (!_games.TryGetValue(gameId, out var game)) return 0;
        lock (game)
        {
            var current = game.GetPlayer(game.CurrentTurnPlayerId);
            if (current?.IsBot != true) return 0;
            return Math.Max(0,
                (int)Math.Ceiling((current.NextShotAllowedAtUtc - DateTime.UtcNow).TotalMilliseconds));
        }
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
            var matryoshkaBot = game.GetPlayers()
                .FirstOrDefault(player => player.IsBot && player.PendingMatryoshka != null);
            if (matryoshkaBot != null)
            {
                var pending = matryoshkaBot.PendingMatryoshka;
                var option = pending?.Options.OrderBy(_ => Rng.Next()).FirstOrDefault();
                if (pending == null || option == null) return new();
                var (success, _) = DeployMatryoshka(
                    game.GameId,
                    matryoshkaBot.DiscordId,
                    pending.ParentShipId,
                    option.Row,
                    option.Col,
                    option.Orientation.ToString());
                return new BattleshipBotStepResult { Acted = success };
            }

            if (HasPendingMatryoshkaDeployments(game)) return new();

            // Mandatory Boarding placement has priority over a Cursed Boat course choice.
            var priorityBoardingBot = game.GetPlayers()
                .FirstOrDefault(player =>
                    player.IsBot && HasMandatoryBoardingDeployment(player));
            if (priorityBoardingBot != null)
            {
                var opponentForPlacement = game.GetOpponent(priorityBoardingBot.DiscordId);
                if (opponentForPlacement == null) return new();
                if (HasWaitingRamReturn(priorityBoardingBot))
                {
                    var reentry = BattleshipBotAI.ChooseSummonDeploy(game, priorityBoardingBot);
                    if (reentry == null) return new();
                    var waitingRam = priorityBoardingBot.Summons.First(summon =>
                        summon.IsAlive &&
                        summon.Type == SummonType.Ram &&
                        summon.WaitingForTurnBack);
                    var (reentryType, reentryLane) = reentry.Value;
                    var (success, _) = DeploySummon(
                        game.GameId,
                        priorityBoardingBot.DiscordId,
                        reentryType.ToString(),
                        reentryLane,
                        waitingRam.Id);
                    return new BattleshipBotStepResult { Acted = success };
                }
                var boardingIds = priorityBoardingBot.PendingSummons
                    .Where(summon => summon.IsMandatoryBoarding)
                    .Select(summon => summon.Id)
                    .ToHashSet();
                var before = priorityBoardingBot.PendingSummons.Count +
                             priorityBoardingBot.MandatoryBoardingSummonSlots +
                             (priorityBoardingBot.MandatoryBoardingBrander ? 1 : 0);
                foreach (var (pendingId, pendingCol) in
                         BattleshipBotAI.ChoosePendingSummonDeploys(priorityBoardingBot, opponentForPlacement)
                             .Where(value => boardingIds.Contains(value.pendingId)))
                    DeployPendingSummon(game.GameId, priorityBoardingBot.DiscordId, pendingId, pendingCol);

                if (HasMandatoryBoardingDeployment(priorityBoardingBot) &&
                    priorityBoardingBot.MandatoryBoardingSummonSlots > 0)
                {
                    var regions = priorityBoardingBot.Fleet
                        .SelectMany(ship => ship.Regions)
                        .ToHashSet();
                    var devastated = regions.Contains(Region.South)
                        ? priorityBoardingBot.Board.PlacedShips.FirstOrDefault(ship =>
                            ship.Statuses.Contains(ShipStatusType.Devastated) &&
                            !ship.Statuses.Contains(ShipStatusType.Capture))
                        : null;
                    if (devastated != null)
                    {
                        RestoreShipWithPirateBoat(
                            game.GameId, priorityBoardingBot.DiscordId, devastated.Id);
                    }
                    else
                    {
                        var type = regions.Contains(Region.West)
                            ? SummonType.Ram
                            : regions.Contains(Region.East)
                                ? SummonType.Scout
                                : SummonType.PirateBoat;
                        var openColumns = Enumerable.Range(0, 10)
                            .Where(col => opponentForPlacement.Board.GetCell(0, col)?.SummonRef
                                is not { IsAlive: true })
                            .ToList();
                        if (openColumns.Count > 0)
                            DeploySummon(game.GameId, priorityBoardingBot.DiscordId,
                                type.ToString(), openColumns[Rng.Next(openColumns.Count)]);
                    }
                }
                else if (HasMandatoryBoardingDeployment(priorityBoardingBot) &&
                         priorityBoardingBot.MandatoryBoardingBrander)
                {
                    var openColumns = Enumerable.Range(0, 10)
                        .Where(col => opponentForPlacement.Board.GetCell(0, col)?.SummonRef
                            is not { IsAlive: true })
                        .ToList();
                    if (openColumns.Count > 0)
                        DeploySummon(game.GameId, priorityBoardingBot.DiscordId,
                            SummonType.Brander.ToString(), openColumns[Rng.Next(openColumns.Count)]);
                }

                if (HasMandatoryBoardingDeployment(priorityBoardingBot) &&
                    !HasLegalMandatoryBoardingDeployment(game, priorityBoardingBot))
                {
                    BattleshipGameEngine.DiscardMandatoryBoardingRemainder(
                        game, priorityBoardingBot);
                    ResumeBoardingResolution(game);
                }
                var after = priorityBoardingBot.PendingSummons.Count +
                            priorityBoardingBot.MandatoryBoardingSummonSlots +
                            (priorityBoardingBot.MandatoryBoardingBrander ? 1 : 0);
                game.LastActivity = DateTime.UtcNow;
                return new BattleshipBotStepResult { Acted = after < before };
            }

            // A human mandatory deployment freezes bot course choices and ordinary turns too.
            if (HasPendingBoardingDeployments(game)) return new();

            var forcedDirectionBot = game.GetPlayers()
                .FirstOrDefault(player =>
                    player.IsBot && GetPendingCursedBoatDirection(game, player) != null);
            if (forcedDirectionBot != null)
            {
                var targetBoardOwner = game.GetOpponent(forcedDirectionBot.DiscordId);
                if (targetBoardOwner == null ||
                    !ResolveBotCursedBoatDirections(game, forcedDirectionBot, targetBoardOwner))
                    return new();
                game.LastActivity = DateTime.UtcNow;
                return new BattleshipBotStepResult { Acted = true };
            }

            var bot = game.GetPlayer(game.CurrentTurnPlayerId);
            if (bot == null || !bot.IsBot || game.IsFinished) return new();
            var opponent = game.GetOpponent(bot.DiscordId);
            if (opponent == null) return new();

            if (game.BotPreparedTurnNumber != game.TurnNumber)
            {
                game.BotPreparedTurnNumber = game.TurnNumber;
                TryBotManeuvers(game, bot);
                foreach (var (pendingId, pendingCol) in BattleshipBotAI.ChoosePendingSummonDeploys(bot, opponent))
                {
                    DeployPendingSummon(game.GameId, bot.DiscordId, pendingId, pendingCol);
                    if (game.IsFinished) return new BattleshipBotStepResult { Acted = true };
                    ResolveBotCursedBoatDirections(game, bot, opponent);
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

            var (weaponType, shotType) = BattleshipBotAI.ChooseWeapon(game, bot, opponent, game.Phase);
            var captured = bot.Board.PlacedShips.Any(s =>
                !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture));
            var enemySummon = captured ? null : opponent.Summons.FirstOrDefault(s =>
                s.IsAlive && bot.Board.GetCell(s.Row, s.Col)?.SummonRef == s);
            if (enemySummon != null &&
                weaponType is not (nameof(WeaponType.GreekFire) or nameof(WeaponType.EvilGreekFire)))
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
            var firstResolutionLogIndex = game.GameLog.Count;
            var shotCountBefore = game.ShotCount;

            ShotResult result;
            if (captured)
                result = BattleshipGameEngine.ProcessShot(game, bot, targetRow, targetCol);
            else if (enemySummon != null &&
                bot.SelectedShotType is ShotType.GreekFire or ShotType.EvilGreekFire)
            {
                BattleshipGameEngine.ConsumeSelectedWeaponAmmo(game, bot);
                result = BattleshipGameEngine.ProcessOwnBoardGreekFireShot(game, bot, targetRow, targetCol);
            }
            else if (enemySummon != null)
                result = BattleshipGameEngine.ProcessOwnBoardShot(game, bot, targetRow, targetCol);
            else if (bot.SelectedShotType == ShotType.Buckshot)
                result = BattleshipGameEngine.ProcessBuckshotShot(game, bot, targetRow, targetCol);
            else
                result = BattleshipGameEngine.ProcessShot(game, bot, targetRow, targetCol);

            DescribeShot(game, bot, result, ownBoard);
            if (ownBoard)
                result.TurnContinues = false;
            result.TurnContinues = RegisterResolvedPlayerShot(
                game,
                bot,
                shotCountBefore,
                result,
                allowSecondShot: true);
            bot.HasShotThisTurn = true;
            ResetExpendedSelection(game, bot);
            CompleteActionResolution(game, result.TurnContinues, moveSummons: true);
            ResolveBotCursedBoatDirections(game, bot, opponent);

            if (!game.IsFinished && game.CurrentTurnPlayerId == bot.DiscordId)
            {
                foreach (var (pendingId, pendingCol) in BattleshipBotAI.ChoosePendingSummonDeploys(bot, opponent))
                {
                    DeployPendingSummon(game.GameId, bot.DiscordId, pendingId, pendingCol);
                    ResolveBotCursedBoatDirections(game, bot, opponent);
                }
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
            GateNewBoardDetailLogs(
                game,
                bot,
                ownBoard ? bot : opponent,
                firstResolutionLogIndex);
            ApplyComboShotDelay(game, bot, result);
            TrySettleGameEnd(game);
            return new BattleshipBotStepResult { Acted = true, Shot = result };
        }
    }

    private static bool ResolveBotCursedBoatDirections(
        BattleshipGame game,
        BattleshipPlayer bot,
        BattleshipPlayer targetBoardOwner)
    {
        var changed = false;
        for (var attempt = 0; attempt < bot.Summons.Count; attempt++)
        {
            var pending = GetPendingCursedBoatDirection(game, bot);
            if (pending == null) break;
            var summon = bot.Summons.FirstOrDefault(value => value.Id == pending.SummonId);
            if (summon == null) break;
            var preferred = BattleshipBotAI.ChooseCursedBoatDirection(summon, targetBoardOwner);
            var option = pending.Options.FirstOrDefault(value =>
                             value.Direction.Equals(preferred.ToString(), StringComparison.OrdinalIgnoreCase))
                         ?? pending.Options.FirstOrDefault();
            if (option == null ||
                !Enum.TryParse<Direction>(option.Direction, out var direction) ||
                !BattleshipGameEngine.SetCursedBoatDirection(bot, pending.SummonId, direction))
                break;
            game.AddLog("Проклятый корабль меняет курс!");
            changed = true;
        }
        return changed;
    }

    private static void TryBotManeuvers(BattleshipGame game, BattleshipPlayer bot)
    {
        // Mandatory maneuvers are resolved in the same stable order exposed to a human.
        // A bounded loop also handles multiple damaged maneuvering ships on one turn.
        for (var attempt = 0; attempt < bot.Board.PlacedShips.Count; attempt++)
        {
            var pending = GetPendingManeuver(game, bot);
            if (pending == null) break;
            var ship = bot.Board.PlacedShips.FirstOrDefault(value => value.Id == pending.ShipId);
            var option = pending.Options.OrderBy(_ => Rng.Next()).FirstOrDefault();
            if (ship == null || option == null ||
                !Enum.TryParse<Direction>(option.Direction, out var direction) ||
                !BattleshipGameEngine.ManualMoveShip(game, bot, ship, direction, option.Distance))
                break;

            ship.HasManeuvered = true;
            game.AddLogFor(bot.DiscordId, ship.DefinitionId switch
            {
                "famous_ramming_ship" => "Знаменитый Врезающийся корабль маневрирует!",
                "merging_ship_v2" => "Сливающийся корабль маневрирует!",
                _ => "Маневрирующая двойка маневрирует!",
            });
        }

        // Voluntary merging maneuvers are independent one-time actions. Prefer an actual
        // overlap over a free reposition, but keep the same server-authoritative option list.
        for (var attempt = 0; attempt < bot.Board.PlacedShips.Count; attempt++)
        {
            var maneuver = GetVoluntaryManeuvers(game, bot).FirstOrDefault();
            if (maneuver == null) break;
            var ship = bot.Board.PlacedShips.FirstOrDefault(value => value.Id == maneuver.ShipId);
            if (ship == null) break;
            var option = maneuver.Options
                .OrderByDescending(candidate => ship
                    .GetOccupiedCells(candidate.Row, candidate.Col, ship.Orientation)
                    .Any(cell => bot.Board.GetCell(cell.row, cell.col)?.ShipRef is { } occupant &&
                                 occupant.Id != ship.Id))
                .ThenBy(_ => Rng.Next())
                .FirstOrDefault();
            if (option == null ||
                !Enum.TryParse<Direction>(option.Direction, out var direction) ||
                !BattleshipGameEngine.ManualMoveShip(game, bot, ship, direction, option.Distance))
                break;

            ship.HasManeuvered = true;
            game.AddLogFor(bot.DiscordId, "Сливающийся корабль маневрирует!");
        }
    }

    private static void SwitchTurn(BattleshipGame game)
    {
        if (game.Player1 == null || game.Player2 == null) return;
        // Reset shot flag for the player whose turn is ending
        var current = game.GetPlayer(game.CurrentTurnPlayerId);
        if (current != null)
        {
            current.HasShotThisTurn = false;
            current.ShotsFiredThisTurn = 0;
            current.ConsecutiveWarmingMisses = 0;
        }
        game.CurrentTurnPlayerId = game.CurrentTurnPlayerId == game.Player1.DiscordId
            ? game.Player2.DiscordId
            : game.Player1.DiscordId;
    }

    private static void AdvanceTurn(BattleshipGame game)
    {
        SwitchTurn(game);
        game.TurnNumber++;

        // Penalty and Stun cancel a turn as soon as it starts. Each player can hold at
        // most one of each, so four iterations cover the longest possible alternating
        // chain while retaining the original Penalty-before-Stun order.
        for (var skipCount = 0; skipCount < 4; skipCount++)
        {
            var player = game.GetPlayer(game.CurrentTurnPlayerId);
            if (player == null) return;

            var reason = player.HasPenalty
                ? "Penalty"
                : player.StunShotExpiry >= game.ShotCount
                    ? "Stun"
                    : null;
            if (reason == null || !BattleshipGameEngine.ProcessTurnStart(game, player))
                return;

            game.PendingTurnSkipEvents.Enqueue(new ShotResult
            {
                WasSkipped = true,
                TurnContinues = false,
                Message = "Ход пропущен!",
                SkippedPlayerId = player.DiscordId,
                SkipReason = reason,
            });

            SwitchTurn(game);
            game.TurnNumber++;
        }
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
            BoardingPlayerId = game.BoardingPlayerId,
            IsMyTurn = game.CurrentTurnPlayerId == requestingDiscordId,
            MyPlayerId = requestingDiscordId,
            GameLog = game.GameLog
                .Where(entry => CanViewLogEntry(
                    game, entry, requestingDiscordId, isSpectator))
                .Select(e => e.Text)
                .TakeLast(50).ToList(),
            Player1 = MapPlayer(game, game.Player1, requestingDiscordId, isPlayer1 || isSpectator, isSpectator, game.ShotCount),
            Player2 = MapPlayer(game, game.Player2, requestingDiscordId, isPlayer2 || isSpectator, isSpectator, game.ShotCount),
            ShipCatalog = game.Phase == BsGamePhase.FleetBuilding
                ? GetShipCatalog(game.GetPlayer(requestingDiscordId)?.Faction)
                : null,
            MyEndReward = requestingDiscordId != null && game.EndRewards.TryGetValue(requestingDiscordId, out var reward)
                ? reward
                : null,
        };
    }

    private static bool CanViewLogEntry(
        BattleshipGame game,
        LogEntry entry,
        string requestingDiscordId,
        bool isSpectator)
    {
        if (isSpectator) return true;
        if (entry.VisibleTo != null)
            return entry.VisibleTo == requestingDiscordId;
        if (entry.DetailBoardOwnerId == null)
            return true;
        return entry.DetailBoardOwnerId == requestingDiscordId ||
               entry.DetailObserverId == requestingDiscordId;
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
            UseSharedTetracatapultAmmo = player.UseSharedTetracatapultAmmo,
            UseGhostSummons = player.UseGhostSummons,
            SelectedShotType = player.SelectedShotType.ToString(),
            SelectedWeaponId = isMe ? player.SelectedWeapon?.Id : null,
            RevealedCellCount = player.RevealedCellCount,
            TotalShotsFired = player.TotalShotsFired,
            StunShotExpiry = player.StunShotExpiry,
            HasPenalty = player.HasPenalty,
            HasShotThisTurn = player.HasShotThisTurn,
            HasPendingMatryoshka = player.PendingMatryoshka != null,
            HasPendingBoardingDeployment = HasMandatoryBoardingDeployment(player),
            MandatoryBoardingSummonSlots = isMe ? player.MandatoryBoardingSummonSlots : 0,
            MandatoryBoardingBrander = isMe && player.MandatoryBoardingBrander,
            BoardingDeploymentCapacity = isMe ? player.BoardingDeploymentCapacity : 0,
            PendingMatryoshka = isMe && player.PendingMatryoshka != null
                ? new PendingMatryoshkaDto
                {
                    ParentShipId = player.PendingMatryoshka.ParentShipId,
                    ChildName = player.PendingMatryoshka.ChildName,
                    ChildDeckCount = player.PendingMatryoshka.ChildDeckCount,
                    Options = player.PendingMatryoshka.Options.Select(option =>
                        new MatryoshkaPlacementOptionDto
                        {
                            Row = option.Row,
                            Col = option.Col,
                            Orientation = option.Orientation.ToString(),
                            Cells = option.Cells.Select(cell => new BoardCoordinateDto
                            {
                                Row = cell.row,
                                Col = cell.col,
                            }).ToList(),
                        }).ToList(),
                }
                : null,
            PendingAssembly = isMe ? GetPendingAssembly(game, player) : null,
            PendingManeuver = isMe ? GetPendingManeuver(game, player) : null,
            VoluntaryManeuvers = isMe ? GetVoluntaryManeuvers(game, player) : new(),
            PendingCursedBoatDirection = isMe && !HasPendingBoardingDeployments(game)
                ? GetPendingCursedBoatDirection(game, player)
                : null,
            ShotDelayRemainingMs = Math.Max(0,
                (int)Math.Ceiling((player.NextShotAllowedAtUtc - DateTime.UtcNow).TotalMilliseconds)),
            ShotDelayDurationMs = player.CurrentShotDelayMs,
            SummonCooldownRemaining = Math.Max(0, 2 - (gameShotCount - player.LastSummonDeployShotCount)),
            CanDeployAnySummon = isMe && CanDeployAnySummon(game, player),
            Fleet = isMe || isSpectator ? MapFleet(player.Fleet, player.RevealedCellCount) : null,
            Board = showOwnBoard
                ? MapBoard(player.Board, isMe || isSpectator)
                : MapFogBoard(
                    player.Board,
                    BattleshipGameEngine.HasLivingMast(game.GetPlayer(requestingId))),
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
                SourceShipName = s.SourceShipName,
                SourceShipDeckCount = s.SourceShipDeckCount,
                IsGhost = s.IsGhost,
            }).ToList(),
            PendingSummons = isMe ? player.PendingSummons.Select(p => new PendingSummonDto
            {
                Id = p.Id,
                Type = p.Type.ToString(),
                AllowedColumns = GetLegalPendingSummonColumns(game, player, p),
                IsBoarding = p.IsBoarding,
                IsMandatoryBoarding = p.IsMandatoryBoarding,
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
                !HasPendingMatryoshkaDeployments(game) &&
                !HasPendingBoardingDeployments(game) &&
                GetPendingAssembly(game, player) == null &&
                GetPendingCursedBoatDirection(game, player) == null &&
                GetPendingManeuver(game, player) == null &&
                !BattleshipGameEngine.HasAnyLegalShot(game, player),
        };
    }

    private static List<AvailableWeaponDto> MapAvailableWeapons(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        var usable = BattleshipGameEngine.GetUsableWeapons(game, player)
            .ToList();

        // Ballista is one shared baseline action. Its real projectile source is selected
        // separately by the stable animation cycle when the shot resolves.
        var result = new List<AvailableWeaponDto>();
        var ballista = usable.FirstOrDefault(x => x.weapon.Type == WeaponType.Ballista);
        if (ballista.weapon != null)
        {
            result.Add(new AvailableWeaponDto
            {
                Id = ballista.weapon.Id,
                ShipId = ballista.ship.Id,
                ShipName = "Общий выстрел",
                Type = WeaponType.Ballista.ToString(),
                ShotType = ShotType.Ballista.ToString(),
                Ammo = -1,
                MaxAmmo = -1,
                DeckIndex = ballista.weapon.DeckIndex,
                Sources = new() { "Общий выстрел" },
            });
        }

        var tetracatapults = usable
            .Where(value => value.weapon.Type == WeaponType.Tetracatapult)
            .ToList();
        if (player.UseSharedTetracatapultAmmo)
        {
            foreach (var group in tetracatapults
                         .Where(value => value.weapon.ConfiguredShotType.HasValue)
                         .GroupBy(value => value.weapon.ConfiguredShotType.Value))
            {
                var first = group.First();
                var sources = group.Select(value => value.ship.Name)
                    .Distinct(StringComparer.Ordinal).ToList();
                result.Add(new AvailableWeaponDto
                {
                    Id = first.weapon.Id,
                    ShipId = first.ship.Id,
                    ShipName = string.Join(" + ", sources),
                    Type = WeaponType.Tetracatapult.ToString(),
                    ShotType = group.Key.ToString(),
                    Ammo = BattleshipGameEngine.GetSharedTetracatapultAmmo(game, player, group.Key),
                    MaxAmmo = BattleshipGameEngine.GetSharedTetracatapultMaxAmmo(game, player, group.Key),
                    DeckIndex = first.weapon.DeckIndex,
                    AimRemaining = group.Min(value =>
                        Math.Max(0, value.weapon.AimSpeed - player.RevealedCellCount)),
                    IsShared = true,
                    Sources = sources,
                });
            }
        }
        else
        {
            result.AddRange(tetracatapults.Select(value => new AvailableWeaponDto
            {
                Id = value.weapon.Id,
                ShipId = value.ship.Id,
                ShipName = value.ship.Name,
                Type = value.weapon.Type.ToString(),
                ShotType = value.weapon.ConfiguredShotType?.ToString(),
                Ammo = value.weapon.Ammo,
                MaxAmmo = value.weapon.MaxAmmo,
                DeckIndex = value.weapon.DeckIndex,
                AimRemaining = Math.Max(0, value.weapon.AimSpeed - player.RevealedCellCount),
                Sources = new() { value.ship.Name },
            }));
        }

        result.AddRange(usable.Where(value => value.weapon.Type is not
                (WeaponType.Ballista or WeaponType.Tetracatapult))
            .Select(value => new AvailableWeaponDto
            {
                Id = value.weapon.Id,
                ShipId = value.ship.Id,
                ShipName = value.ship.Name,
                Type = value.weapon.Type.ToString(),
                ShotType = WeaponTypeToShotType(value.weapon.Type).ToString(),
                Ammo = value.weapon.Ammo,
                MaxAmmo = value.weapon.MaxAmmo,
                DeckIndex = value.weapon.DeckIndex,
                AimRemaining = Math.Max(0, value.weapon.AimSpeed - player.RevealedCellCount),
                Sources = new() { value.ship.Name },
            }));
        return result;
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
                OffsetRow = d.OffsetRow,
                OffsetCol = d.OffsetCol,
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
                IsOperational = !s.Statuses.Any(status => status is
                    ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) &&
                    BattleshipGameEngine.IsWeaponOperational(s, w),
                AimSpeed = w.AimSpeed > 0 ? Math.Max(0, w.AimSpeed - opponentRevealedCount) : 0,
                ConfiguredShotType = w.ConfiguredShotType?.ToString(),
            }).ToList(),
            GrabRow = BattleshipCapturingMechanics.IsActiveAbilitySource(s, "grab_summon")
                ? BattleshipCapturingMechanics.GetGrabCell(s).row
                : null,
            GrabCol = BattleshipCapturingMechanics.IsActiveAbilitySource(s, "grab_summon")
                ? BattleshipCapturingMechanics.GetGrabCell(s).col
                : null,
        }).ToList() ?? new();
    }

    private static BoardDto MapBoard(Board board, bool showShips)
    {
        var cells = new List<CellDto>();
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];
            var liveDeck = GetDeckAtCell(cell);
            var matryoshkaWreck = cell.IsMatryoshkaWreck && cell.ShipRef == null;
            var boardingDeck = cell.SummonRef is { IsAlive: true, IsBoardingShip: true } boardingSummon
                ? BattleshipGameEngine.GetLiveBoardingDeckAtCell(boardingSummon, r, c)
                : null;
            var deckIsDamaged = liveDeck != null && liveDeck.CurrentHp < liveDeck.MaxHp;
            var deckIsScratched = liveDeck is { CurrentHp: > 0 } && deckIsDamaged;
            var boardingDeckIsScratched = boardingDeck is { CurrentHp: > 0 } &&
                                          (boardingDeck.CurrentHp < boardingDeck.MaxHp ||
                                           cell.BurnResistMarked);
            var preservedBoardingHit = cell.WasBoardingSourceCell &&
                                       liveDeck == null &&
                                       cell.IsHit &&
                                       cell.WasShipHit;
            cells.Add(new CellDto
            {
                Row = r,
                Col = c,
                IsRevealed = true,
                // ТЗ #19: killed decks derive from the live Ship object, so the mark follows a
                // Maneuvering Double to its new cells after a manual move
                // Historical fog snapshots remain in Cell after hidden movement/assembly,
                // while a Boarding conversion explicitly retains any pre-existing source-cell paint.
                IsHit = deckIsDamaged || matryoshkaWreck || preservedBoardingHit,
                IsMiss = liveDeck == null && !matryoshkaWreck && cell.IsMiss,
                IsBurning = cell.IsBurning,
                HasShip = showShips && (cell.ShipRef != null || matryoshkaWreck),
                ShipId = showShips
                    ? cell.ShipRef?.Id ?? cell.Wreck?.SourceShipId
                    : null,
                HasSummon = cell.SummonRef != null && cell.SummonRef.IsAlive,
                SummonOwnerId = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.OwnerId : null,
                SummonType = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.Type.ToString() : null,
                SummonName = cell.SummonRef is { IsAlive: true }
                    ? cell.SummonRef.SourceShipName
                    : null,
                IsBoardingSummon = cell.SummonRef is { IsAlive: true, IsBoardingShip: true },
                IsGhostSummon = cell.SummonRef is { IsAlive: true, IsGhost: true },
                SummonMoveDirection = cell.SummonRef is { IsAlive: true }
                    ? cell.SummonRef.MoveDirection.ToString()
                    : null,
                BoardingShipDeckCount = cell.SummonRef is { IsAlive: true, IsBoardingShip: true }
                    ? 1
                    : 0,
                IsScratched = deckIsScratched || boardingDeckIsScratched ||
                               preservedBoardingHit && cell.WasScratched,
                SummonTrails = cell.SummonTrails.Select(MapSummonMarker).ToList(),
                SummonDeaths = cell.SummonDeaths.Select(MapSummonMarker).ToList(),
                FrozenSummonDeathIndices = cell.FrozenSummonDeathIndices.ToList(),
                IsBurnResistMarked = cell.BurnResistMarked &&
                                     (deckIsScratched || boardingDeck is { CurrentHp: > 0 } ||
                                      preservedBoardingHit),
                IsDodgeMarked = cell.WasDodge,
                IsManeuverDodgeMarked = cell.WasManeuverDodge,
                IsDestroyed = liveDeck?.IsDestroyed == true || matryoshkaWreck ||
                              preservedBoardingHit && !cell.WasScratched,
                IsShipSunk = cell.ShipRef?.IsDestroyed == true || matryoshkaWreck,
                IsFrozen = cell.ShipRef?.Statuses.Contains(ShipStatusType.Freeze) == true,
                IsDevastated = cell.ShipRef?.Statuses.Contains(ShipStatusType.Devastated) == true,
                IsCaptured = cell.ShipRef?.Statuses.Contains(ShipStatusType.Capture) == true,
                IsFirePermanent = cell.IsBurning,
                IsGrabCell = showShips && board.PlacedShips.Any(ship =>
                    BattleshipCapturingMechanics.IsActiveAbilitySource(ship, "grab_summon") &&
                    BattleshipCapturingMechanics.GetGrabCell(ship) == (r, c)),
                SunkShipName = cell.ShipRef != null
                    ? cell.ShipRef.IsDestroyed ? cell.ShipRef.Name : null
                    : cell.Wreck?.SourceShipName ?? cell.SunkShipName,
            });
        }
        return new BoardDto { Cells = cells };
    }

    private static BoardDto MapFogBoard(Board board, bool includeSunkShipNames)
    {
        var cells = new List<CellDto>();
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];
            var boardingDeck = cell.SummonRef is { IsAlive: true, IsBoardingShip: true } boardingSummon
                ? BattleshipGameEngine.GetLiveBoardingDeckAtCell(boardingSummon, r, c)
                : null;
            var hiddenMovedShip = cell.ShipRef is
                { HasHiddenMovement: true, IsDestroyed: false } && !cell.WasShipHit;
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
                HasShip = cell.WasShipHit || cell.WasRevealedShip ||
                          (cell.IsRevealed && cell.ShipRef != null && !hiddenMovedShip),
                ShipId = null,
                HasSummon = cell.SummonRef != null && cell.SummonRef.IsAlive,
                SummonOwnerId = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.OwnerId : null,
                SummonType = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.Type.ToString() : null,
                SummonName = cell.SummonRef is { IsAlive: true }
                    ? cell.SummonRef.SourceShipName
                    : null,
                IsBoardingSummon = cell.SummonRef is { IsAlive: true, IsBoardingShip: true },
                IsGhostSummon = cell.SummonRef is { IsAlive: true, IsGhost: true },
                SummonMoveDirection = cell.SummonRef is { IsAlive: true }
                    ? cell.SummonRef.MoveDirection.ToString()
                    : null,
                BoardingShipDeckCount = cell.SummonRef is { IsAlive: true, IsBoardingShip: true }
                    ? 1
                    : 0,
                IsScratched = cell.WasScratched ||
                                boardingDeck is { CurrentHp: > 0 } &&
                                boardingDeck.CurrentHp < boardingDeck.MaxHp,
                SummonTrails = cell.SummonTrails.Select(MapSummonMarker).ToList(),
                SummonDeaths = cell.SummonDeaths.Select(MapSummonMarker).ToList(),
                FrozenSummonDeathIndices = cell.FrozenSummonDeathIndices.ToList(),
                IsBurnResistMarked = cell.BurnResistMarked,
                IsDodgeMarked = cell.WasDodge,
                IsManeuverDodgeMarked = cell.WasManeuverDodge,
                IsDestroyed = (cell.WasShipHit && !cell.WasScratched) ||
                              (!hiddenMovedShip && IsDeckDestroyedAt(cell)),
                IsShipSunk = !hiddenMovedShip && cell.ShipRef?.IsDestroyed == true && cell.IsRevealed,
                IsFrozen = !hiddenMovedShip &&
                           cell.ShipRef?.Statuses.Contains(ShipStatusType.Freeze) == true,
                IsDevastated = !hiddenMovedShip &&
                               cell.ShipRef?.Statuses.Contains(ShipStatusType.Devastated) == true,
                IsCaptured = !hiddenMovedShip &&
                             cell.ShipRef?.Statuses.Contains(ShipStatusType.Capture) == true,
                IsFirePermanent = cell.IsBurning,
                SunkShipName = includeSunkShipNames ? cell.SunkShipName : null,
            });
        }
        return new BoardDto { Cells = cells };
    }

    /// <summary>The deck of the ship occupying this cell is destroyed (derived from the live Ship, ТЗ #19).</summary>
    private static bool IsDeckDestroyedAt(Cell cell)
    {
        return GetDeckAtCell(cell)?.IsDestroyed == true;
    }

    private static Deck GetDeckAtCell(Cell cell)
    {
        if (cell.ShipRef == null) return null;
        var ship = cell.ShipRef;
        return ship.Decks.FirstOrDefault(deck =>
        {
            var position = ship.GetDeckCell(deck, ship.Row, ship.Col, ship.Orientation);
            return position.row == cell.Row && position.col == cell.Col;
        });
    }

    private static SummonMarkerDto MapSummonMarker(SummonMarker marker) => new()
    {
        SummonId = marker.SummonId,
        Type = marker.Type.ToString(),
        IsBoardingShip = marker.IsBoardingShip,
        SourceShipName = marker.SourceShipName,
    };

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
    public string BoardingPlayerId { get; set; }
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
    public bool UseSharedTetracatapultAmmo { get; set; }
    public bool UseGhostSummons { get; set; }
    public string SelectedShotType { get; set; }
    public string SelectedWeaponId { get; set; }
    public int RevealedCellCount { get; set; }
    public int TotalShotsFired { get; set; }
    public int StunShotExpiry { get; set; }
    public bool HasPenalty { get; set; }
    public bool HasShotThisTurn { get; set; }
    public bool HasPendingMatryoshka { get; set; }
    public bool HasPendingBoardingDeployment { get; set; }
    public int MandatoryBoardingSummonSlots { get; set; }
    public bool MandatoryBoardingBrander { get; set; }
    public int BoardingDeploymentCapacity { get; set; }
    public PendingMatryoshkaDto PendingMatryoshka { get; set; }
    public PendingAssemblyDto PendingAssembly { get; set; }
    public PendingManeuverDto PendingManeuver { get; set; }
    public List<VoluntaryManeuverDto> VoluntaryManeuvers { get; set; } = new();
    public PendingCursedBoatDirectionDto PendingCursedBoatDirection { get; set; }
    public int ShotDelayRemainingMs { get; set; }
    public int ShotDelayDurationMs { get; set; }
    public int SummonCooldownRemaining { get; set; }
    public bool CanDeployAnySummon { get; set; }
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
    public string SummonName { get; set; }
    public bool IsBoardingSummon { get; set; }
    public bool IsGhostSummon { get; set; }
    public string SummonMoveDirection { get; set; }
    public int BoardingShipDeckCount { get; set; }
    public bool IsScratched { get; set; }
    public List<SummonMarkerDto> SummonTrails { get; set; } = new();
    public List<SummonMarkerDto> SummonDeaths { get; set; } = new();
    public List<int> FrozenSummonDeathIndices { get; set; } = new();
    public bool IsBurnResistMarked { get; set; }
    public bool IsDodgeMarked { get; set; }
    public bool IsManeuverDodgeMarked { get; set; }
    public bool IsDestroyed { get; set; }
    public bool IsShipSunk { get; set; }
    public bool IsFrozen { get; set; }
    public bool IsDevastated { get; set; }
    public bool IsCaptured { get; set; }
    public bool IsFirePermanent { get; set; }
    public bool IsGrabCell { get; set; }
    public string SunkShipName { get; set; }
}

public class SummonMarkerDto
{
    public string SummonId { get; set; }
    public string Type { get; set; }
    public bool IsBoardingShip { get; set; }
    public string SourceShipName { get; set; }
}

public class PendingAssemblyDto
{
    public string GroupId { get; set; }
    public List<AssemblyPlacementOptionDto> Options { get; set; } = new();
}

public class PendingMatryoshkaDto
{
    public string ParentShipId { get; set; }
    public string ChildName { get; set; }
    public int ChildDeckCount { get; set; }
    public List<MatryoshkaPlacementOptionDto> Options { get; set; } = new();
}

public class MatryoshkaPlacementOptionDto
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string Orientation { get; set; }
    public List<BoardCoordinateDto> Cells { get; set; } = new();
}

public class BoardCoordinateDto
{
    public int Row { get; set; }
    public int Col { get; set; }
}

public class AssemblyPlacementOptionDto
{
    public int Row { get; set; }
    public int Col { get; set; }
    public string Orientation { get; set; }
}

public class PendingManeuverDto
{
    public string ShipId { get; set; }
    public string ShipName { get; set; }
    public List<ManeuverOptionDto> Options { get; set; } = new();
}

public class VoluntaryManeuverDto
{
    public string ShipId { get; set; }
    public string ShipName { get; set; }
    public List<ManeuverOptionDto> Options { get; set; } = new();
}

public class ManeuverOptionDto
{
    public string Direction { get; set; }
    public int Distance { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
}

public class PendingCursedBoatDirectionDto
{
    public string SummonId { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public List<CursedBoatDirectionOptionDto> Options { get; set; } = new();
}

public class CursedBoatDirectionOptionDto
{
    public string Direction { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
}

public class AvailableWeaponDto
{
    public string Id { get; set; }
    public string ShipId { get; set; }
    public string ShipName { get; set; }
    public string Type { get; set; }
    public string ShotType { get; set; }
    public int Ammo { get; set; }
    public int DeckIndex { get; set; }
    public int AimRemaining { get; set; }
    public int MaxAmmo { get; set; }
    public bool IsShared { get; set; }
    public List<string> Sources { get; set; } = new();
}

public class ShipDto
{
    public string Id { get; set; }
    public string DefinitionId { get; set; }
    public string Name { get; set; }
    public int DeckCount { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int? GrabRow { get; set; }
    public int? GrabCol { get; set; }
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
    public int? OffsetRow { get; set; }
    public int? OffsetCol { get; set; }
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
    public bool IsOperational { get; set; }
    public int AimSpeed { get; set; }
    public string ConfiguredShotType { get; set; }
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
    public string SourceShipName { get; set; }
    public int SourceShipDeckCount { get; set; }
    public bool IsGhost { get; set; }
}

public class PendingSummonDto
{
    public string Id { get; set; }
    public string Type { get; set; }
    public List<int> AllowedColumns { get; set; } = new();
    public bool IsBoarding { get; set; }
    public bool IsMandatoryBoarding { get; set; }
    public string SourceShipName { get; set; }
}

public class FleetSelectionDto
{
    public string DefinitionId { get; set; }
    public string ShipName { get; set; }
    public int Cost { get; set; }
    public List<string> Upgrades { get; set; } = new();
}

public class TetracatapultLoadoutDto
{
    public string WeaponId { get; set; }
    public string ShotType { get; set; }
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
    public List<string> Factions { get; set; } = new();
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
    public string DescriptionKey { get; set; }
    public bool IsPreinstalled { get; set; }
}
