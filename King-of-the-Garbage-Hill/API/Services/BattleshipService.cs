using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using King_of_the_Garbage_Hill.Battleship.Logic;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.API.Services;

public class BattleshipService
{
    private readonly ConcurrentDictionary<string, BattleshipGame> _games = new();
    private readonly Timer _cleanupTimer;
    private static readonly Random Rng = new();

    public BattleshipService()
    {
        _cleanupTimer = new Timer
        {
            AutoReset = true,
            Interval = 300_000, // 5 minutes
            Enabled = true,
        };
        _cleanupTimer.Elapsed += (_, _) => CleanupStaleGames();
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
                return (null, "У вас уже есть активная игра в Морской бой.");
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

            // Replace bot
            if (game.Player2?.IsBot == true)
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
                return (true, null);
            }

            if (game.Player2?.DiscordId == discordId)
            {
                // Replace with bot
                game.Player2 = new BattleshipPlayer
                {
                    DiscordId = $"bot_{game.GameId}",
                    Username = "Бот",
                    IsBot = true
                };

                // If game is in progress, bot takes over
                if (game.Phase > BsGamePhase.Lobby)
                {
                    HandleBotTakeOver(game, game.Player2);
                }

                game.LastActivity = DateTime.UtcNow;
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

            // Validate fleet
            var (valid, error) = FleetValidator.ValidateFleet(selections);
            if (!valid) return (false, error);

            // Store selection and build ships
            player.SelectedShips = selections;
            player.CoinsRemaining = FleetValidator.MaxBudget - FleetValidator.CalculateTotalCost(selections);
            player.Fleet.Clear();

            foreach (var sel in selections)
            {
                var def = ShipCatalog.GetById(sel.DefinitionId);
                if (def != null)
                {
                    var ship = ShipCatalog.CreateShip(def, sel.Upgrades);
                    player.Fleet.Add(ship);
                }
            }

            player.IsReady = true;
            game.LastActivity = DateTime.UtcNow;

            CheckPhaseTransition(game);
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

            var ship = player.Fleet.Find(s => s.Id == shipId);
            if (ship == null)
                return (false, "Корабль не найден.");

            if (!Enum.TryParse<Orientation>(orientationStr, true, out var orientation))
                return (false, "Неверная ориентация.");

            // Remove from board if already placed
            if (ship.IsPlaced)
                RemoveShipFromBoard(player, ship);

            // Validate and place
            var (valid, error) = PlacementValidator.ValidatePlacement(player.Board, ship, row, col, orientation);
            if (!valid) return (false, error);

            ship.Row = row;
            ship.Col = col;
            ship.Orientation = orientation;
            ship.IsPlaced = true;

            var cells = ship.GetOccupiedCells();
            foreach (var (r, c) in cells)
            {
                player.Board.Grid[r, c].ShipRef = ship;
            }
            player.Board.PlacedShips.Add(ship);

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

            // Must deploy all boarding ships before shooting
            if (shooter.PendingSummons.Any(p => p.IsBoarding))
                return (null, "Разместите все абордажные корабли!");

            // Process stun/penalty at turn start
            if (BattleshipGameEngine.ProcessTurnStart(game, shooter))
            {
                // Turn skipped — move summons, switch turn
                BattleshipGameEngine.MoveSummons(game);
                SwitchTurn(game);
                game.TurnNumber++;
                game.LastActivity = DateTime.UtcNow;

                // Bot turn after skip
                ProcessBotIfNeeded(game);

                return (new ShotResult { Miss = true, TurnContinues = false, Message = "Ход пропущен!" }, null);
            }

            // AimSpeed check: weapon locked until enough enemy cells revealed
            var opponent = game.GetOpponent(discordId);
            if (shooter.SelectedWeapon != null && shooter.SelectedWeapon.AimSpeed > 0
                && opponent != null && opponent.RevealedCellCount < shooter.SelectedWeapon.AimSpeed)
            {
                return (null, $"Оружие ещё заряжается! Нужно разведать {shooter.SelectedWeapon.AimSpeed - opponent.RevealedCellCount} клеток.");
            }

            // Buckshot not allowed during boarding — auto-downgrade if it was pre-selected
            if (shooter.SelectedShotType == ShotType.Buckshot && game.Phase == BsGamePhase.Boarding)
            {
                shooter.SelectedShotType = ShotType.WhiteStone;
            }

            // Process shot (buckshot uses 2x2 AoE)
            ShotResult result;
            if (shooter.SelectedShotType == ShotType.Buckshot)
                result = BattleshipGameEngine.ProcessBuckshotShot(game, shooter, row, col);
            else
                result = BattleshipGameEngine.ProcessShot(game, shooter, row, col);

            shooter.HasShotThisTurn = true;
            game.LastActivity = DateTime.UtcNow;

            // Move summons after EVERY shot
            BattleshipGameEngine.MoveSummons(game);

            if (!result.TurnContinues)
            {
                SwitchTurn(game);
                game.TurnNumber++;
            }

            // Check win
            CheckAndApplyWin(game);

            // Check boarding trigger
            if (!game.IsFinished)
            {
                foreach (var p in game.GetPlayers())
                {
                    if (BattleshipGameEngine.CheckBoardingTrigger(p))
                    {
                        BattleshipGameEngine.TriggerBoarding(game, p);
                        break;
                    }
                }
            }

            // Bot turn
            ProcessBotIfNeeded(game);

            // Re-check win after bot's turn
            if (!game.IsFinished)
                CheckAndApplyWin(game);

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

            var result = BattleshipGameEngine.ProcessOwnBoardShot(game, shooter, row, col);

            if (result.Hit)
            {
                shooter.HasShotThisTurn = true;
                game.LastActivity = DateTime.UtcNow;

                BattleshipGameEngine.MoveSummons(game);

                SwitchTurn(game);
                game.TurnNumber++;

                CheckAndApplyWin(game);
                ProcessBotIfNeeded(game);

                if (!game.IsFinished)
                    CheckAndApplyWin(game);
            }

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
            game.GameLog.Add($"{player.Username} сдался. Победитель: {opponent.Username}!");
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
            game.GameLog.Add($"Победитель: {game.GetPlayer(winnerId)?.Username ?? "???"}!");
        }
    }

    private void ProcessBotIfNeeded(BattleshipGame game)
    {
        if (!game.IsFinished && game.CurrentTurnPlayerId != null)
        {
            var currentPlayer = game.GetPlayer(game.CurrentTurnPlayerId);
            if (currentPlayer?.IsBot == true)
                ProcessBotTurn(game);
        }
    }

    /// <summary>
    /// Map WeaponType to the correct ShotType for combat resolution.
    /// Catapult fires Buckshot (2x2 AoE), Tetracatapult fires White Stones (8 dmg + stun).
    /// </summary>
    private static ShotType WeaponTypeToShotType(WeaponType wt)
    {
        return wt switch
        {
            WeaponType.Catapult => ShotType.Buckshot,
            WeaponType.Tetracatapult => ShotType.WhiteStone,
            WeaponType.Incendiary => ShotType.Incendiary,
            WeaponType.GreekFire => ShotType.GreekFire,
            _ => ShotType.Ballista,
        };
    }

    public (bool success, string error) SelectWeapon(string gameId, string discordId, string weaponType, string shotType)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            // Find and set the actual weapon object (needed for Far range validation)
            if (Enum.TryParse<WeaponType>(weaponType, true, out var wt))
            {
                // Tetracatapult can fire as WhiteStone or Buckshot (player chooses)
                if (wt == WeaponType.Tetracatapult
                    && Enum.TryParse<ShotType>(shotType, true, out var st)
                    && st == ShotType.Buckshot)
                {
                    // Buckshot disabled during boarding
                    if (game.Phase == BsGamePhase.Boarding)
                        return (false, "Дробь недоступна во время абордажа.");
                    player.SelectedShotType = ShotType.Buckshot;
                }
                else
                {
                    player.SelectedShotType = WeaponTypeToShotType(wt);
                }

                foreach (var ship in player.Board.PlacedShips)
                {
                    if (ship.IsDestroyed) continue;
                    var weapon = ship.Weapons.Find(w => w.Type == wt && w.HasAmmo);
                    if (weapon != null)
                    {
                        // AimSpeed check: weapon locked until enough enemy cells revealed
                        if (weapon.AimSpeed > 0)
                        {
                            var opp = game.GetOpponent(discordId);
                            if (opp != null && opp.RevealedCellCount < weapon.AimSpeed)
                                return (false, $"Оружие ещё заряжается! Нужно разведать {weapon.AimSpeed - opp.RevealedCellCount} клеток.");
                        }
                        player.SelectedWeapon = weapon;
                        break;
                    }
                }
            }

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

            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            if (player.SummonSlotsUsed >= player.MaxSummonSlots)
                return (false, "Все слоты призыва заняты.");

            if (!Enum.TryParse<SummonType>(summonTypeStr, true, out var summonType))
                return (false, "Неизвестный тип призыва.");

            // Brander requires the boiler upgrade on Tetranavis
            if (summonType == SummonType.Brander &&
                !player.Fleet.Any(s => !s.IsDestroyed && s.Abilities.Contains("brander_summon")))
                return (false, "Для призыва Брандера нужен апгрейд Котельной.");

            // Region check: Ram requires West, Scout requires East, PirateBoat requires South
            var playerRegions = player.Fleet.SelectMany(s => s.Regions).Distinct().ToHashSet();
            if (summonType == SummonType.Ram && !playerRegions.Contains(Region.West))
                return (false, "Для призыва Тарана нужен флот из региона Запад.");
            if (summonType == SummonType.Scout && !playerRegions.Contains(Region.East))
                return (false, "Для призыва Разведчика нужен флот из региона Восток.");
            if (summonType == SummonType.PirateBoat && !playerRegions.Contains(Region.South))
                return (false, "Для призыва Пиратского корабля нужен флот из региона Юг.");

            if (col < 0 || col >= 10)
                return (false, "Неверная колонка.");

            var opponent = game.GetOpponent(discordId);

            // Deployment threshold: need 5 revealed cells per summon index
            var summonIndex = player.SummonSlotsUsed;
            if (opponent != null && opponent.RevealedCellCount < 5 * (summonIndex + 1) && game.Phase != BsGamePhase.Boarding)
                return (false, $"Нужно разведать ещё {5 * (summonIndex + 1) - opponent.RevealedCellCount} клеток для призыва.");

            // Deployment cooldown: 2 shots between deployments
            if (game.ShotCount - player.LastSummonDeployShotCount < 2 && game.Phase != BsGamePhase.Boarding)
                return (false, "Слишком рано для нового призыва (перезарядка 2 выстрела).");

            // Re-send waiting summon (turn-back)
            var waitingSummon = player.Summons.FirstOrDefault(s => s.WaitingForTurnBack && s.Type == summonType);
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
                player.LastSummonDeployShotCount = game.ShotCount;
                game.LastActivity = DateTime.UtcNow;
                game.GameLog.Add($"{player.Username} перенаправил {summonType}!");

                // Mast warning
                if (opponent != null)
                {
                    var warning = BattleshipGameEngine.GenerateMastWarning(opponent, summonType);
                    if (warning != null) game.GameLog.Add(warning);
                }

                return (true, null);
            }

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
                    summon.Damage = 4;
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
                    summon.Damage = 999;
                    break;
            }

            player.Summons.Add(summon);
            player.SummonSlotsUsed++;
            player.LastSummonDeployShotCount = game.ShotCount;
            game.LastActivity = DateTime.UtcNow;

            // Set SummonRef on opponent's board so summon can be targeted by shots
            var opponentCell = opponent?.Board.GetCell(summon.Row, summon.Col);
            if (opponentCell != null) opponentCell.SummonRef = summon;

            game.GameLog.Add($"{player.Username} развернул {summonType}!");

            // Mast warning for opponent
            if (opponent != null)
            {
                var warning = BattleshipGameEngine.GenerateMastWarning(opponent, summonType);
                if (warning != null) game.GameLog.Add(warning);
            }

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

            if (col < 0 || col >= 10)
                return (false, "Неверная колонка.");

            // Column restriction for pirate/cursed boat
            if (pending.AllowedColumns.Count > 0 && !pending.AllowedColumns.Contains(col))
                return (false, $"Можно разместить только в колонках: {string.Join(", ", pending.AllowedColumns.Select(c => (char)('A' + c)))}");

            if (player.SummonSlotsUsed >= player.MaxSummonSlots)
                return (false, "Все слоты призыва заняты.");

            var opponent = game.GetOpponent(discordId);

            var summon = new Summon
            {
                Type = pending.Type,
                Row = 0,
                Col = col,
                Speed = pending.Speed,
                Damage = pending.Damage,
                RevealRadius = pending.RevealRadius,
                OwnerId = discordId,
                MoveDirection = Direction.Down,
                SpawnedAtShot = game.ShotCount,
                IsBoardingShip = pending.IsBoarding
            };

            player.Summons.Add(summon);
            player.SummonSlotsUsed++;
            player.PendingSummons.Remove(pending);
            game.LastActivity = DateTime.UtcNow;

            // Set SummonRef on opponent's board
            var opponentCell = opponent?.Board.GetCell(summon.Row, summon.Col);
            if (opponentCell != null) opponentCell.SummonRef = summon;

            game.GameLog.Add($"{player.Username} выпустил {pending.SourceShipName ?? pending.Type.ToString()}!");

            // Mast warning
            if (opponent != null)
            {
                var warning = BattleshipGameEngine.GenerateMastWarning(opponent, pending.Type);
                if (warning != null) game.GameLog.Add(warning);
            }

            return (true, null);
        }
    }

    public (bool success, string error) ManualMoveShip(string gameId, string discordId, string shipId, string directionStr, int distance = 1)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
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

            // One-time use
            if (player.ManeuveringDoubleUsed)
                return (false, "Маневренное перемещение уже было использовано.");

            if (distance < 1 || distance > 2)
                return (false, "Можно переместиться на 1 или 2 клетки.");

            if (!Enum.TryParse<Direction>(directionStr, true, out var direction))
                return (false, "Неверное направление.");

            var success = BattleshipGameEngine.ManualMoveShip(player, ship, direction, distance);
            if (!success)
                return (false, "Невозможно переместить корабль в этом направлении.");

            player.ManeuveringDoubleUsed = true;
            game.LastActivity = DateTime.UtcNow;
            game.GameLog.Add($"{ship.Name} маневрирует!");

            // Mast warning: "Даёт по вёслам!"
            var opponent = game.GetOpponent(discordId);
            if (opponent != null)
            {
                var hasMast = opponent.Board.PlacedShips.Any(s =>
                    !s.IsDestroyed && s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed));
                if (hasMast)
                    game.GameLog.Add("[Мачта] Даёт по вёслам!");
            }

            return (true, null);
        }
    }

    public (bool success, string error) SetCursedBoatDirection(string gameId, string discordId, string summonId, string directionStr)
    {
        if (!_games.TryGetValue(gameId, out var game))
            return (false, "Игра не найдена.");

        lock (game)
        {
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            if (!Enum.TryParse<Direction>(directionStr, true, out var direction))
                return (false, "Неверное направление.");

            var success = BattleshipGameEngine.SetCursedBoatDirection(player, summonId, direction);
            if (!success)
                return (false, "Проклятый корабль не ожидает выбора направления.");

            game.LastActivity = DateTime.UtcNow;
            game.GameLog.Add($"Проклятый корабль меняет курс!");
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
            var player = game.GetPlayer(discordId);
            if (player == null)
                return (false, "Вы не в этой игре.");

            player.IsReady = true;
            game.LastActivity = DateTime.UtcNow;

            CheckPhaseTransition(game);
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
            Description = def.Description,
            Region = def.Regions.Any(r => r != Region.Tetracor)
                ? def.Regions.First(r => r != Region.Tetracor).ToString()
                : null,
            AvailableUpgrades = def.AvailableUpgrades?.Select(u => new UpgradeDto
            {
                Id = u.Id,
                Name = u.Name,
                NameRu = u.NameRu,
                Cost = u.Cost,
                Description = u.Description,
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
                game.Phase = BsGamePhase.Combat;
                game.TurnNumber = 1;
                // If both players have Desiccator, disable all its passives
                BattleshipGameEngine.DisableDualDesiccators(game);
                game.CurrentTurnPlayerId = BattleshipGameEngine.DetermineFirstTurn(game.Player1, game.Player2);
                game.GameLog.Add($"Бой начинается! Первый ход: {game.GetPlayer(game.CurrentTurnPlayerId)?.Username}");

                // If bot goes first, process its turn
                if (game.GetPlayer(game.CurrentTurnPlayerId)?.IsBot == true)
                    ProcessBotTurn(game);
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

        game.Player2.IsReady = true;
    }

    private void HandleBotPlacement(BattleshipGame game)
    {
        if (game.Player2?.IsBot != true) return;

        BattleshipBotAI.PlaceFleet(game.Player2);
        game.Player2.IsReady = true;
    }

    private void HandleBotTakeOver(BattleshipGame game, BattleshipPlayer bot)
    {
        // Give bot a default fleet and auto-place if needed
        if (bot.Fleet.Count == 0)
        {
            var botFleet = BattleshipBotAI.SelectFleet();
            foreach (var sel in botFleet)
            {
                var def = ShipCatalog.GetById(sel.DefinitionId);
                if (def != null) bot.Fleet.Add(ShipCatalog.CreateShip(def));
            }
        }

        if (game.Phase >= BsGamePhase.ShipPlacement && bot.Board.PlacedShips.Count == 0)
        {
            BattleshipBotAI.PlaceFleet(bot);
        }

        bot.IsReady = true;

        // If it's bot's turn, process immediately
        if (game.CurrentTurnPlayerId == bot.DiscordId)
            ProcessBotTurn(game);
    }

    private void ProcessBotTurn(BattleshipGame game)
    {
        var bot = game.GetPlayer(game.CurrentTurnPlayerId);
        if (bot == null || !bot.IsBot || game.IsFinished) return;

        var opponent = game.GetOpponent(bot.DiscordId);
        if (opponent == null) return;

        // Check stun/penalty
        if (BattleshipGameEngine.ProcessTurnStart(game, bot))
        {
            BattleshipGameEngine.MoveSummons(game);
            SwitchTurn(game);
            game.TurnNumber++;
            return;
        }

        // Deploy pending summons (pirate/cursed boats from ship death, boarding ships)
        var pendingDeploys = BattleshipBotAI.ChoosePendingSummonDeploys(bot, opponent);
        foreach (var (pendingId, pendingCol) in pendingDeploys)
        {
            DeployPendingSummon(game.GameId, bot.DiscordId, pendingId, pendingCol);
            if (game.IsFinished) return;
        }

        // Handle CursedBoat direction choices
        foreach (var summon in bot.Summons.Where(s => s.IsAlive && s.WaitingForDirectionChoice).ToList())
        {
            var dir = BattleshipBotAI.ChooseCursedBoatDirection(summon, opponent);
            BattleshipGameEngine.SetCursedBoatDirection(bot, summon.Id, dir);
        }

        // Bot summon deployment (strategic type selection)
        var summonChoice = BattleshipBotAI.ChooseSummonDeploy(game, bot);
        if (summonChoice != null)
        {
            var (summonType, summonCol) = summonChoice.Value;
            DeploySummon(game.GameId, bot.DiscordId, summonType.ToString(), summonCol);
        }

        // Bot shoots in a loop until turn ends
        var maxShots = 100;
        while (game.CurrentTurnPlayerId == bot.DiscordId && !game.IsFinished && maxShots-- > 0)
        {
            // Select weapon before each shot (may change based on situation)
            var (weaponType, shotType) = BattleshipBotAI.ChooseWeapon(bot, opponent, game.Phase);
            SelectWeapon(game.GameId, bot.DiscordId, weaponType, shotType);

            var (targetRow, targetCol) = BattleshipBotAI.ChooseTarget(bot, opponent, bot.SelectedShotType);

            ShotResult result;
            if (bot.SelectedShotType == ShotType.Buckshot)
                result = BattleshipGameEngine.ProcessBuckshotShot(game, bot, targetRow, targetCol);
            else
                result = BattleshipGameEngine.ProcessShot(game, bot, targetRow, targetCol);

            // Move summons after every shot
            BattleshipGameEngine.MoveSummons(game);

            if (!result.TurnContinues)
            {
                SwitchTurn(game);
                game.TurnNumber++;
                CheckAndApplyWin(game);
                break;
            }

            // Check win after each shot
            CheckAndApplyWin(game);
            if (game.IsFinished) break;

            // Check boarding trigger
            foreach (var p in game.GetPlayers())
            {
                if (BattleshipGameEngine.CheckBoardingTrigger(p))
                {
                    BattleshipGameEngine.TriggerBoarding(game, p);

                    // Deploy boarding pending summons immediately
                    var boardingDeploys = BattleshipBotAI.ChoosePendingSummonDeploys(bot, opponent);
                    foreach (var (pid, pcol) in boardingDeploys)
                        DeployPendingSummon(game.GameId, bot.DiscordId, pid, pcol);

                    break;
                }
            }

            // Try deploying a summon between shots if we got a reset
            if (result.TurnContinues)
            {
                var midSummon = BattleshipBotAI.ChooseSummonDeploy(game, bot);
                if (midSummon != null)
                {
                    var (mType, mCol) = midSummon.Value;
                    DeploySummon(game.GameId, bot.DiscordId, mType.ToString(), mCol);
                }
            }
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
            GameLog = game.GameLog.TakeLast(50).ToList(),
            Player1 = MapPlayer(game.Player1, requestingDiscordId, isPlayer1 || isSpectator, isSpectator, game.ShotCount, game.Player2?.RevealedCellCount ?? 0),
            Player2 = MapPlayer(game.Player2, requestingDiscordId, isPlayer2 || isSpectator, isSpectator, game.ShotCount, game.Player1?.RevealedCellCount ?? 0),
            ShipCatalog = game.Phase == BsGamePhase.FleetBuilding ? GetShipCatalog() : null,
        };
    }

    private static BattleshipPlayerDto MapPlayer(BattleshipPlayer player, string requestingId, bool showOwnBoard, bool isSpectator, int gameShotCount, int opponentRevealedCount = 0)
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
            SelectedShotType = player.SelectedShotType.ToString(),
            RevealedCellCount = player.RevealedCellCount,
            StunShotExpiry = player.StunShotExpiry,
            HasPenalty = player.HasPenalty,
            ManeuveringDoubleUsed = player.ManeuveringDoubleUsed,
            HasShotThisTurn = player.HasShotThisTurn,
            SummonCooldownRemaining = Math.Max(0, 2 - (gameShotCount - player.LastSummonDeployShotCount)),
            Fleet = isMe || isSpectator ? MapFleet(player.Fleet, opponentRevealedCount) : null,
            Board = showOwnBoard ? MapBoard(player.Board, isMe || isSpectator) : MapFogBoard(player.Board),
            Summons = player.Summons.Where(s => s.IsAlive || s.WaitingForTurnBack || s.WaitingForDirectionChoice).Select(s => new SummonDto
            {
                Id = s.Id,
                Type = s.Type.ToString(),
                Row = s.Row,
                Col = s.Col,
                Speed = s.Speed,
                IsAlive = s.IsAlive,
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
        };
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
            Range = s.Range.ToString(),
            Cost = s.Cost,
            Abilities = s.Abilities,
            Upgrades = s.Upgrades,
            Speed = s.Speed,
            Space = s.Space,
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
                Type = w.Type.ToString(),
                Ammo = w.Ammo,
                Damage = w.Damage,
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
                IsHit = cell.IsHit,
                IsMiss = cell.IsMiss,
                IsBurning = cell.IsBurning,
                HasShip = showShips && cell.ShipRef != null,
                ShipId = showShips ? cell.ShipRef?.Id : null,
                HasSummon = cell.SummonRef != null && cell.SummonRef.IsAlive,
                SummonOwnerId = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.OwnerId : null,
                SummonType = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.Type.ToString() : null,
                IsScratched = IsCellScratched(cell),
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
                IsHit = cell.IsHit,
                IsMiss = cell.IsMiss,
                IsBurning = cell.IsBurning,
                HasShip = cell.WasShipHit, // Snapshot: show ship where it was hit, not current position
                ShipId = cell.WasShipHit ? cell.ShipRef?.Id : null,
                HasSummon = cell.SummonRef != null && cell.SummonRef.IsAlive,
                SummonOwnerId = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.OwnerId : null,
                SummonType = cell.SummonRef is { IsAlive: true } ? cell.SummonRef.Type.ToString() : null,
                IsScratched = cell.WasScratched, // Snapshot: scratched state persists after ship moves
            });
        }
        return new BoardDto { Cells = cells };
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
    public string SelectedShotType { get; set; }
    public int RevealedCellCount { get; set; }
    public int StunShotExpiry { get; set; }
    public bool HasPenalty { get; set; }
    public bool ManeuveringDoubleUsed { get; set; }
    public bool HasShotThisTurn { get; set; }
    public int SummonCooldownRemaining { get; set; }
    public List<ShipDto> Fleet { get; set; }
    public BoardDto Board { get; set; }
    public List<SummonDto> Summons { get; set; } = new();
    public List<PendingSummonDto> PendingSummons { get; set; } = new();
    public List<FleetSelectionDto> SelectedShips { get; set; }
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
    public string Type { get; set; }
    public int Ammo { get; set; }
    public int Damage { get; set; }
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
