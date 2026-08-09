using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Core combat engine: processes shots, damage, status effects, summon movement, win conditions.
/// </summary>
public static class BattleshipGameEngine
{
    private enum ShipDestructionCause
    {
        Shot,
        Incendiary,
        EvilIncendiary,
        GreekFire,
        Explosion,
        Collision,
        Capture,
        Devastated,
        Poison,
        Freeze,
    }

    // ── Turn Start Processing ─────────────────────────────────────────

    /// <summary>
    /// Process turn start: check stun and penalty before allowing a player to act.
    /// Returns true if the player's turn is skipped.
    /// </summary>
    public static bool ProcessTurnStart(BattleshipGame game, BattleshipPlayer player)
    {
        // Penalty check — skip turn, clear flag
        if (player.HasPenalty)
        {
            player.HasPenalty = false;
            game.AddLog($"{player.Username} пропускает ход (штраф)!");
            return true;
        }

        // Stun check — if stunned, skip turn and clear stun (one-time trigger)
        if (player.StunShotExpiry >= game.ShotCount)
        {
            player.StunShotExpiry = -1;
            game.AddLog($"{player.Username} оглушён и пропускает ход!");
            return true;
        }

        return false;
    }

    // ── Shot Processing ──────────────────────────────────────────────

    /// <summary>
    /// A normal Ballista needs a living Mid/Close source and its own living module.
    /// A captured enemy Close ship supplies its Ballista to the capturing player until destroyed.
    /// </summary>
    public static bool HasUsableBallista(BattleshipGame game, BattleshipPlayer player)
    {
        return GetControlledShips(game, player).Any(s =>
            !s.IsDestroyed && s.Range is RangeClass.Mid or RangeClass.Close &&
            s.Weapons.Any(w => w.Type == WeaponType.Ballista && IsWeaponOperational(s, w)));
    }

    public static IEnumerable<(Ship ship, Weapon weapon)> GetUsableWeapons(
        BattleshipGame game, BattleshipPlayer player, WeaponType? type = null)
    {
        var opponent = game.GetOpponent(player.DiscordId);
        var capturedEnemyIds = opponent?.Board.PlacedShips
            .Where(s => s.Statuses.Contains(ShipStatusType.Capture) && s.Range == RangeClass.Close)
            .Select(s => s.Id)
            .ToHashSet() ?? new HashSet<string>();
        return GetControlledShips(game, player)
            .Where(s => !s.IsDestroyed)
            .SelectMany(s => s.Weapons.Select(w => (ship: s, weapon: w)))
            .Where(x => (x.weapon.Type is WeaponType.Ballista or WeaponType.Tetracatapult or
                            WeaponType.Incendiary or WeaponType.EvilIncendiary or
                            WeaponType.GreekFire or WeaponType.EvilGreekFire) &&
                        (!capturedEnemyIds.Contains(x.ship.Id) || x.weapon.Type == WeaponType.Ballista) &&
                        (x.weapon.Type != WeaponType.Ballista || x.ship.Range is RangeClass.Mid or RangeClass.Close) &&
                        (type == null || x.weapon.Type == type) && HasWeaponAmmo(game, player, x.weapon) &&
                        IsWeaponOperational(x.ship, x.weapon));
    }

    private static bool HasWeaponAmmo(BattleshipGame game, BattleshipPlayer player, Weapon weapon)
    {
        if (weapon.Type != WeaponType.Tetracatapult || !player.UseSharedTetracatapultAmmo)
            return weapon.HasAmmo;
        return weapon.ConfiguredShotType is { } shotType &&
               GetSharedTetracatapultAmmo(game, player, shotType) > 0;
    }

    public static void InitializeSharedTetracatapultAmmo(BattleshipGame game, BattleshipPlayer player)
    {
        player.SharedTetracatapultAmmo.Clear();
        if (!player.UseSharedTetracatapultAmmo) return;
        foreach (var shotType in new[] { ShotType.WhiteStone, ShotType.Buckshot })
        {
            var ammo = GetControlledShips(game, player)
                .Where(ship => !ship.IsDestroyed)
                .SelectMany(ship => ship.Weapons.Select(weapon => (ship, weapon)))
                .Where(value => value.weapon.Type == WeaponType.Tetracatapult &&
                                value.weapon.ConfiguredShotType == shotType &&
                                IsWeaponOperational(value.ship, value.weapon))
                .Sum(value => Math.Max(0, value.weapon.Ammo));
            player.SharedTetracatapultAmmo[shotType] = ammo;
        }
    }

    public static int GetSharedTetracatapultMaxAmmo(
        BattleshipGame game,
        BattleshipPlayer player,
        ShotType shotType)
    {
        if (!player.UseSharedTetracatapultAmmo) return 0;
        return GetControlledShips(game, player)
            .Where(ship => !ship.IsDestroyed)
            .SelectMany(ship => ship.Weapons.Select(weapon => (ship, weapon)))
            .Where(value => value.weapon.Type == WeaponType.Tetracatapult &&
                            value.weapon.ConfiguredShotType == shotType &&
                            IsWeaponOperational(value.ship, value.weapon))
            .Sum(value => Math.Max(0, value.weapon.MaxAmmo));
    }

    public static int GetSharedTetracatapultAmmo(
        BattleshipGame game,
        BattleshipPlayer player,
        ShotType shotType)
    {
        if (!player.UseSharedTetracatapultAmmo) return 0;
        var maximum = GetSharedTetracatapultMaxAmmo(game, player, shotType);
        var current = player.SharedTetracatapultAmmo.GetValueOrDefault(shotType);
        current = Math.Clamp(current, 0, maximum);
        player.SharedTetracatapultAmmo[shotType] = current;
        return current;
    }

    private static void AddSharedTetracatapultAmmo(
        BattleshipGame game,
        BattleshipPlayer player,
        ShotType? shotType,
        int amount)
    {
        if (!player.UseSharedTetracatapultAmmo || shotType == null || amount <= 0) return;
        var current = GetSharedTetracatapultAmmo(game, player, shotType.Value);
        var maximum = GetSharedTetracatapultMaxAmmo(game, player, shotType.Value);
        player.SharedTetracatapultAmmo[shotType.Value] = Math.Min(maximum, current + amount);
    }

    public static void ConsumeSelectedWeaponAmmo(BattleshipGame game, BattleshipPlayer player)
    {
        var weapon = player.SelectedWeapon;
        if (weapon?.Type == WeaponType.Tetracatapult &&
            player.UseSharedTetracatapultAmmo &&
            weapon.ConfiguredShotType is { } shotType)
        {
            var current = GetSharedTetracatapultAmmo(game, player, shotType);
            if (current > 0) player.SharedTetracatapultAmmo[shotType] = current - 1;
            return;
        }
        weapon?.UseAmmo();
    }

    public static bool HasAnyLegalShot(BattleshipGame game, BattleshipPlayer player)
    {
        if (player.Board.PlacedShips.Any(s =>
                !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture)))
            return GetUsableWeapons(game, player).Any(x =>
                x.weapon.AimSpeed <= player.RevealedCellCount);
        if (HasUsableBallista(game, player)) return true;
        return GetUsableWeapons(game, player).Any(x =>
            (x.weapon.Type is WeaponType.Tetracatapult or WeaponType.Incendiary or
                WeaponType.EvilIncendiary or WeaponType.GreekFire or WeaponType.EvilGreekFire) &&
            x.weapon.AimSpeed <= player.RevealedCellCount);
    }

    public static bool IsWeaponOperational(Ship ship, Weapon weapon)
    {
        if (ship == null || weapon == null || ship.IsDestroyed || weapon.ShipId != ship.Id) return false;
        var deck = ship.Decks.FirstOrDefault(value => value.Index == weapon.DeckIndex);
        if (deck == null) return false;
        return !deck.IsDestroyed && !deck.ModuleDestroyed && !weapon.PreservedModuleDestroyed;
    }

    private static IEnumerable<Ship> GetControlledShips(BattleshipGame game, BattleshipPlayer player)
    {
        foreach (var ship in player.Board.PlacedShips.Where(s => !s.Statuses.Contains(ShipStatusType.Capture)))
            yield return ship;

        // Converted Close ships leave the source board. Their hull metadata remains in Fleet
        // only so a living deployed boarding unit can retain its operational Ballista.
        // Once that unit dies it disappears from this controlled-source set immediately.
        if (game.Phase == BsGamePhase.Boarding)
        {
            var activeBoardingSourceIds = player.Summons
                .Where(s => s.IsAlive && s.IsBoardingShip && s.SourceShipId != null)
                .Select(s => s.SourceShipId)
                .ToHashSet();
            foreach (var ship in player.Fleet.Where(s =>
                         activeBoardingSourceIds.Contains(s.Id) &&
                         !s.Statuses.Contains(ShipStatusType.Capture)))
                yield return ship;
        }

        var opponent = game.GetOpponent(player.DiscordId);
        if (opponent == null) yield break;
        foreach (var ship in opponent.Board.PlacedShips.Where(s =>
                     s.Statuses.Contains(ShipStatusType.Capture) && s.Range == RangeClass.Close))
            yield return ship;
    }

    public static ShotResult ProcessShot(BattleshipGame game, BattleshipPlayer shooter, int row, int col)
    {
        var opponent = game.GetOpponent(shooter.DiscordId);
        if (opponent == null)
            return new ShotResult { Miss = true, Message = "Нет противника." };

        // Ballista restriction: source and compatible module must both still be alive.
        if (shooter.SelectedShotType == ShotType.Ballista && !HasUsableBallista(game, shooter))
        {
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                Message = "Нет живой Баллисты на корабле класса Mid или Close." };
        }

        // Capture targeting: if shooter has captured ships on their own board, must target those cells
        // (shooting at own board to destroy captured ship)
        var capturedShips = shooter.Board.PlacedShips
            .Where(s => s.Statuses.Contains(ShipStatusType.Capture) && !s.IsDestroyed).ToList();
        if (capturedShips.Count > 0)
        {
            var capturedCells = capturedShips.SelectMany(s => s.GetOccupiedCells()).ToHashSet();
            if (!capturedCells.Contains((row, col)))
            {
                return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                    Message = "Нужно сначала уничтожить захваченный корабль!" };
            }
            // Process shot against own board (captured ship)
            return ProcessCapturedShipShot(game, shooter, row, col);
        }

        var cell = opponent.Board.GetCell(row, col);
        if (cell == null)
            return new ShotResult { Miss = true, Message = "Клетка за пределами поля." };

        // Greek Fire is an own-board-only Boiler shot.
        if (shooter.SelectedShotType is ShotType.GreekFire or ShotType.EvilGreekFire)
            return new ShotResult { Miss = true, Row = row, Col = col, Message = "Греческий огонь стреляет только по своему полю." };

        // Far range restriction is legality, so an invalid attempt spends neither shot nor ammo.
        if (shooter.SelectedWeapon?.ShipId != null)
        {
            var weaponShip = game.GetPlayers().SelectMany(p => p.Board.PlacedShips)
                .FirstOrDefault(s => s.Id == shooter.SelectedWeapon.ShipId);
            if (weaponShip != null && weaponShip.Range == RangeClass.Far && weaponShip.Row >= 8 && row >= 8)
            {
                return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                    Message = "Дальнобойный корабль не может стрелять по задним рядам!" };
            }
        }

        // Increment global shot counter
        game.ShotCount++;
        ApplyWhiteStoneStun(game, shooter, opponent);

        // Consume ammo from selected weapon
        ConsumeSelectedWeaponAmmo(game, shooter);

        // Brander detonation: shooter shoots their own Brander on opponent's board
        var friendlyBrander = shooter.Summons.FirstOrDefault(s =>
            s.IsAlive && s.Type == SummonType.Brander && s.Row == row && s.Col == col);
        if (friendlyBrander != null)
        {
            cell.IsHit = true;
            DetonateBrander(game, opponent, friendlyBrander, row, col, shooter);
            return new ShotResult
            {
                Row = row, Col = col, TurnContinues = false, Burned = true,
                Message = "Брандер взорвался!"
            };
        }

        // Allow re-targeting scratched cells and incendiary retargets
        if (cell.IsHit || cell.IsMiss)
        {
            if (cell.SummonRef is { IsAlive: true })
                return ProcessSummonHit(game, shooter, opponent.Board, cell, row, col);
            if (cell.WasScratched && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                return ProcessShipHit(game, shooter, opponent, cell, row, col);
            }
            if (shooter.SelectedShotType is ShotType.Incendiary or ShotType.EvilIncendiary &&
                cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                return ProcessShipHit(game, shooter, opponent, cell, row, col);
            }
            // Allow re-shooting revealed/hit cells — treat as miss if no ship
            if (cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                return ProcessShipHit(game, shooter, opponent, cell, row, col);
            }
            // Already empty — miss
            cell.IsMiss = true;
            game.AddLog($"{shooter.Username} промахнулся ({(char)('A' + col)}{row + 1})");
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false, Message = "Мимо!" };
        }

        // Reveal the physical result once; intact occupancy and empty water are distinct states.
        RevealCell(opponent.Board, cell, shooter);

        // Miss — empty cell
        if (cell.ShipRef == null && cell.SummonRef == null)
        {
            cell.IsMiss = true;
            game.AddLog($"{shooter.Username} промахнулся ({(char)('A' + col)}{row + 1})");
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false, Message = "Мимо!" };
        }

        // Hit summon
        if (cell.SummonRef != null)
        {
            return ProcessSummonHit(game, shooter, opponent.Board, cell, row, col);
        }

        // Hit ship
        return ProcessShipHit(game, shooter, opponent, cell, row, col);
    }

    /// <summary>
    /// Greek Fire fired at the shooter's OWN board (ТЗ #23): same semantics as the enemy-board
    /// version — permanent burning cell, kills an enemy summon without penalty, burns own
    /// non-BurnResist ships. Turn always ends (handled by the service).
    /// </summary>
    public static ShotResult ProcessOwnBoardGreekFireShot(BattleshipGame game, BattleshipPlayer shooter, int row, int col)
    {
        var cell = shooter.Board.GetCell(row, col);
        if (cell == null)
            return new ShotResult { Miss = true, Message = "Клетка за пределами поля." };

        game.ShotCount++;
        cell.IsBurning = true;
        cell.IsHit = true;

        // Enemy summon on own board — kill without penalty; per-match use counters persist
        if (cell.SummonRef != null && cell.SummonRef.IsAlive && cell.SummonRef.OwnerId != shooter.DiscordId)
        {
            return ProcessSummonHit(game, shooter, shooter.Board, cell, row, col);
        }

        // Own ship — burns like any ship (BurnResist immune)
        if (cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
        {
            if (!cell.ShipRef.Statuses.Contains(ShipStatusType.BurnResist))
            {
                foreach (var d in cell.ShipRef.Decks) d.CurrentHp = 0;
                cell.ShipRef.Statuses.Add(ShipStatusType.Burn);
                RevealShip(shooter.Board, cell.ShipRef);
                HandleShipDeath(game, shooter, cell.ShipRef, ShipDestructionCause.GreekFire);
                game.AddBoardDetailLog(shooter.DiscordId,
                    $"Греческий огонь сжёг {cell.ShipRef.Name}!");
                return new ShotResult { Hit = true, Destroyed = true, ShipSunk = true, Burned = true,
                    Row = row, Col = col, TurnContinues = false,
                    Message = $"Греческий огонь сжёг {cell.ShipRef.Name}!", AffectedShipName = cell.ShipRef.Name };
            }
            MarkFireResistance(cell);
            game.AddBoardDetailLog(shooter.DiscordId,
                $"{cell.ShipRef.Name}: Корабль устоял против огня! Поцарапано.");
            return new ShotResult { Row = row, Col = col, TurnContinues = false,
                Hit = true, Scratched = true,
                Message = "Корабль устоял против огня. Поцарапано.",
                AffectedShipName = cell.ShipRef.Name };
        }

        // Empty cell — permanent fire (area denial against summons)
        cell.IsMiss = true;
        game.AddBoardDetailLog(shooter.DiscordId,
            $"Греческий огонь горит на ({(char)('A' + col)}{row + 1})!");
        return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
            Message = "Греческий огонь — клетка горит!" };
    }

    /// <summary>
    /// Process shot against a captured ship on shooter's own board.
    /// </summary>
    private static ShotResult ProcessCapturedShipShot(BattleshipGame game, BattleshipPlayer shooter, int row, int col)
    {
        var cell = shooter.Board.GetCell(row, col);
        if (cell?.ShipRef == null || !cell.ShipRef.Statuses.Contains(ShipStatusType.Capture))
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false, Message = "Мимо!" };

        var ship = cell.ShipRef;
        var deckIndex = GetDeckIndexAtCell(ship, row, col);
        if (deckIndex < 0)
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false, Message = "Мимо!" };

        var deck = ship.Decks[deckIndex];
        var isGreekFire = shooter.SelectedShotType is ShotType.GreekFire or ShotType.EvilGreekFire;
        var isIncendiary = shooter.SelectedShotType is ShotType.Incendiary or ShotType.EvilIncendiary;
        var canIgniteDeadDeck = isGreekFire || shooter.SelectedShotType == ShotType.EvilIncendiary;
        if (deck.IsDestroyed && !canIgniteDeadDeck)
        {
            cell.IsMiss = true;
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                Message = "Эта палуба уже уничтожена." };
        }

        game.ShotCount++;
        ConsumeSelectedWeaponAmmo(game, shooter);
        cell.IsRevealed = true;
        cell.IsHit = true;
        cell.IsMiss = false;
        cell.WasShipHit = true;
        cell.WasDodge = false;
        cell.WasManeuverDodge = false;

        // Capture disables ordinary death passives, but the Incendiary Barge is an explicit
        // exception: the original owner detonates it with the first valid hit.
        if (ship.Abilities.Contains("explode_on_hit") && !ship.HasExploded)
        {
            ExplodeShip(game, shooter, ship, shooter);
            game.AddBoardDetailLog(shooter.DiscordId,
                $"Захваченный {ship.Name} взорвался от первого попадания!");
            return FinishCapturedShipDestruction(
                game, shooter, ship, row, col, burned: true);
        }

        if (isGreekFire)
            cell.IsBurning = true;

        if (isGreekFire || isIncendiary)
        {
            if (ship.Statuses.Contains(ShipStatusType.BurnResist))
            {
                MarkFireResistance(cell);
                game.AddBoardDetailLog(shooter.DiscordId,
                    $"{ship.Name}: Корабль устоял против огня! Поцарапано.");
                return new ShotResult
                {
                    Hit = true,
                    Scratched = true,
                    Row = row,
                    Col = col,
                    TurnContinues = false,
                    Message = "Корабль устоял против огня. Поцарапано.",
                    AffectedShipName = ship.Name,
                };
            }

            foreach (var capturedDeck in ship.Decks)
                capturedDeck.CurrentHp = 0;
            if (!ship.Statuses.Contains(ShipStatusType.Burn))
                ship.Statuses.Add(ShipStatusType.Burn);
            return FinishCapturedShipDestruction(
                game, shooter, ship, row, col, burned: true);
        }

        if (shooter.SelectedShotType == ShotType.WhiteStone)
        {
            if (deck.Module != null)
            {
                deck.ModuleDestroyed = true;
                game.AddBoardDetailLog(shooter.DiscordId,
                    $"Белый камень разрушил модуль {deck.Module} на захваченном {ship.Name}!");
            }
        }

        var damage = GetDamage(shooter);
        deck.CurrentHp -= damage;
        if (deck.CurrentHp < 0) deck.CurrentHp = 0;

        if (ship.IsDestroyed)
            return FinishCapturedShipDestruction(
                game, shooter, ship, row, col, burned: false);

        if (deck.IsDestroyed)
        {
            cell.WasScratched = false;
            game.AddBoardDetailLog(shooter.DiscordId,
                $"{shooter.Username} повредил палубу захваченного {ship.Name}!");
            return new ShotResult { Hit = true, Destroyed = true, Row = row, Col = col, TurnContinues = false,
                Message = $"Палуба захваченного {ship.Name} уничтожена!", AffectedShipName = ship.Name };
        }

        cell.WasScratched = true;
        game.AddBoardDetailLog(shooter.DiscordId,
            $"{shooter.Username} поцарапал броню захваченного {ship.Name}.");
        return new ShotResult { Hit = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
            Message = "Поцарапал броню захваченного корабля!", AffectedShipName = ship.Name };
    }

    private static ShotResult FinishCapturedShipDestruction(
        BattleshipGame game,
        BattleshipPlayer originalOwner,
        Ship ship,
        int row,
        int col,
        bool burned)
    {
        // The capturer owns the reconnaissance gained when the originally enemy ship
        // finally dies on its physical board.
        var capturer = game.GetOpponent(originalOwner.DiscordId);
        RevealShip(originalOwner.Board, ship, capturer);
        ship.Statuses.Remove(ShipStatusType.Capture);
        game.AddBoardDetailLog(originalOwner.DiscordId,
            $"{originalOwner.Username} уничтожил захваченный {ship.Name}!");
        HandleShipDeath(game, originalOwner, ship, ShipDestructionCause.Capture);
        return new ShotResult
        {
            Hit = true,
            Destroyed = true,
            ShipSunk = true,
            Burned = burned,
            Row = row,
            Col = col,
            TurnContinues = false,
            Message = $"Захваченный {ship.Name} уничтожен!",
            AffectedShipName = ship.Name,
        };
    }

    /// <summary>
    /// Shoot own board to damage an enemy summon or boarding hull.
    /// </summary>
    public static ShotResult ProcessOwnBoardShot(BattleshipGame game, BattleshipPlayer shooter, int row, int col)
    {
        var cell = shooter.Board.GetCell(row, col);
        if (cell == null)
            return new ShotResult { Miss = true, Message = "Клетка за пределами поля." };

        // Must have an enemy summon on this cell
        if (cell.SummonRef == null || !cell.SummonRef.IsAlive || cell.SummonRef.OwnerId == shooter.DiscordId)
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                Message = "На этой клетке нет вражеского призыва." };

        var summon = cell.SummonRef;
        game.ShotCount++;
        var stunRecipient = summon.IsBoardingShip
            ? game.GetPlayer(summon.OwnerId)
            : shooter;
        ApplyWhiteStoneStun(game, shooter, stunRecipient);
        ConsumeSelectedWeaponAmmo(game, shooter);
        return ProcessSummonHit(game, shooter, shooter.Board, cell, row, col);
    }

    /// <summary>
    /// Process a Buckshot (2x2 AoE) shot. topRow/topCol is the top-left corner.
    /// </summary>
    public static ShotResult ProcessBuckshotShot(BattleshipGame game, BattleshipPlayer shooter, int topRow, int topCol)
    {
        var opponent = game.GetOpponent(shooter.DiscordId);
        if (opponent == null)
            return new ShotResult { Miss = true, Message = "Нет противника." };

        game.ShotCount++;

        // Consume ammo from selected weapon
        ConsumeSelectedWeaponAmmo(game, shooter);

        var aggregate = new ShotResult
        {
            Row = topRow, Col = topCol,
            TurnContinues = false,
        };

        var anyHit = false;
        var anyDestroyed = false;
        var anySunk = false;
        var anyResettingDestruction = false;
        var penaltyApplied = false;

        // Process 2x2 area
        for (var dr = 0; dr < 2; dr++)
        for (var dc = 0; dc < 2; dc++)
        {
            var r = topRow + dr;
            var c = topCol + dc;
            var cell = opponent.Board.GetCell(r, c);
            if (cell == null) continue;

            // Allow re-targeting scratched cells (armor survived)
            if (cell.IsHit && cell.WasScratched &&
                (cell.ShipRef != null && !cell.ShipRef.IsDestroyed ||
                 cell.SummonRef is { IsAlive: true, IsBoardingShip: true }))
            {
                // Fall through to process as normal hit
            }
            else if (cell.IsHit || cell.IsMiss) continue;

            RevealCell(opponent.Board, cell, shooter);

            if (cell.ShipRef == null && cell.SummonRef == null)
            {
                cell.IsMiss = true;
                continue;
            }

            if (cell.SummonRef != null)
            {
                var summon = cell.SummonRef;
                var summonHit = ProcessSummonHit(game, shooter, opponent.Board, cell, r, c);
                anyHit |= summonHit.Hit;
                penaltyApplied |= summonHit.PenaltyApplied;
                aggregate.ForcesTurnEnd |= summonHit.ForcesTurnEnd;
                // Legacy summons remain non-resetting. A converted hull retains ship-like
                // per-deck destruction and only sinks after its last living deck is gone.
                if (summon.IsBoardingShip)
                {
                    anyDestroyed |= summonHit.Destroyed;
                    anySunk |= summonHit.ShipSunk;
                    // A destroyed boarding deck keeps the ordinary deck reset; only the
                    // final hull kill is explicitly non-resetting.
                    anyResettingDestruction |= summonHit.Destroyed && !summonHit.ShipSunk;
                }
                continue;
            }

            // Ship hit with buckshot damage (1)
            var ship = cell.ShipRef;

            var deckIndex = GetDeckIndexAtCell(ship, r, c);
            if (deckIndex >= 0 && deckIndex < ship.Decks.Count)
            {
                var deck = ship.Decks[deckIndex];
                if (deck.IsDestroyed)
                {
                    cell.IsMiss = true;
                    cell.WasScratched = false;
                    continue;
                }
                cell.IsHit = true;
                cell.IsMiss = false;
                cell.WasShipHit = true;
                cell.WasDodge = false;
                var wasAlive = !deck.IsDestroyed;
                deck.CurrentHp -= 1; // buckshot damage
                if (deck.CurrentHp <= 0) deck.CurrentHp = 0;
                anyHit = true;
                if (deck.IsDestroyed)
                {
                    anyDestroyed = true;
                    anyResettingDestruction = true;
                    cell.WasScratched = false; // No longer scratched — deck is destroyed (ТЗ #9)
                    // ТЗ #20: shooter's mast spots the Maneuvering Double (same rule as ProcessShipHit)
                    if (wasAlive && ship.Abilities.Contains("manual_move_after_hit") &&
                        HasLivingMast(shooter))
                    {
                        game.AddLogFor(shooter.DiscordId, "[Мачта] Даёт по вёслам!");
                    }
                }
                else cell.WasScratched = true;

                // Check explode_on_hit — any damage = full destruction
                if (ship.Abilities.Contains("explode_on_hit") && !ship.IsDestroyed)
                {
                    ExplodeShip(game, opponent, ship, shooter); // kills all decks + marks cells (ТЗ #4)
                }

                if (ship.IsDestroyed)
                {
                    anySunk = true;
                    anyResettingDestruction = true;
                    RevealShip(opponent.Board, ship, shooter);
                    HandleShipDeath(game, opponent, ship);
                    game.AddBoardDetailLog(opponent.DiscordId,
                        $"Картечь потопила {ship.Name}!");
                }
            }
        }

        // Enemy Boarding decks retain ordinary reset behavior while the hull survives.
        // Destroying any deck of the shooter's own Boarding hull ends the whole action.
        aggregate.TurnContinues = anyResettingDestruction && !aggregate.ForcesTurnEnd;

        aggregate.Hit = anyHit;
        aggregate.Destroyed = anyDestroyed;
        aggregate.ShipSunk = anySunk;
        aggregate.PenaltyApplied = penaltyApplied;
        aggregate.Miss = !anyHit;
        aggregate.Message = anyHit ? "Картечь поразила цель!" : "Картечь — мимо!";

        game.AddLog($"{shooter.Username} выстрелил картечью ({(char)('A' + topCol)}{topRow + 1})");
        return aggregate;
    }

    private static void DestroyBoardingHull(
        BattleshipGame game,
        Board board,
        Summon summon,
        bool suppressDeferredReveal,
        bool frozen = false)
    {
        var occupied = GetLiveBoardingDeckCells(summon);
        ResolveDeferredRevealOnDeath(game, summon, suppressDeferredReveal || frozen);
        foreach (var (row, col, _) in occupied)
            MarkSummonDeath(board, row, col, summon, frozen);
        foreach (var deck in summon.BoardingDecks)
            deck.CurrentHp = 0;
        if (frozen && !summon.BoardingStatuses.Contains(ShipStatusType.Freeze))
            summon.BoardingStatuses.Add(ShipStatusType.Freeze);
        summon.IsAlive = false;
        summon.WaitingForTurnBack = false;
        summon.WaitingForDirectionChoice = false;
        ClearSummonFromBoard(board, summon);
        var owner = game.GetPlayer(summon.OwnerId);
        SyncBoardingSourceState(owner, summon);
        MarkBoardingSourceDestroyed(owner, summon);
    }

    private static ShotResult ProcessSummonHit(
        BattleshipGame game,
        BattleshipPlayer shooter,
        Board board,
        Cell cell,
        int row,
        int col)
    {
        var summon = cell.SummonRef;
        var physicalBoardOwner = game.GetPlayers()
            .FirstOrDefault(player => ReferenceEquals(player.Board, board));
        var physicalBoardOwnerId = physicalBoardOwner?.DiscordId;
        if (summon == null || !summon.IsAlive)
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false };

        if (HasBoardingHull(summon))
        {
            var deck = GetLiveBoardingDeckAtCell(summon, row, col);
            if (deck == null)
                return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false };

            cell.IsHit = true;
            cell.IsMiss = false;
            cell.WasDodge = false;
            cell.WasManeuverDodge = false;
            var sourceName = summon.SourceShipName ?? "Абордажный корабль";
            var targetsOwnBoardingHull = summon.OwnerId == shooter.DiscordId;

            if (shooter.SelectedShotType == ShotType.Ballista &&
                BoardingHasAbility(summon, "ballista_immune"))
            {
                cell.IsHit = false;
                cell.IsRevealed = true;
                cell.WasDodge = true;
                game.AddBoardDetailLog(physicalBoardOwnerId,
                    $"{sourceName} неуязвим для Баллисты! ({(char)('A' + col)}{row + 1})");
                return new ShotResult
                {
                    Miss = true,
                    Scratched = true,
                    Row = row,
                    Col = col,
                    TurnContinues = false,
                    ForcesTurnEnd = targetsOwnBoardingHull,
                    Message = $"{sourceName} не получает урон от Баллисты.",
                    AffectedShipName = sourceName,
                };
            }

            var directIncendiary =
                shooter.SelectedShotType is ShotType.Incendiary or ShotType.EvilIncendiary;
            var fire = directIncendiary ||
                       shooter.SelectedShotType is ShotType.GreekFire or ShotType.EvilGreekFire;
            if (fire)
            {
                if (BoardingHasStatus(summon, ShipStatusType.BurnResist))
                {
                    MarkFireResistance(cell);
                    game.AddBoardDetailLog(physicalBoardOwnerId,
                        $"{sourceName}: Корабль устоял против огня! Поцарапано.");
                    return new ShotResult
                    {
                        Hit = true,
                        Scratched = true,
                        Row = row,
                        Col = col,
                        TurnContinues = false,
                        ForcesTurnEnd = targetsOwnBoardingHull,
                        Message = "Корабль устоял против огня. Поцарапано.",
                        AffectedShipName = sourceName,
                    };
                }

                if (!summon.BoardingStatuses.Contains(ShipStatusType.Burn))
                    summon.BoardingStatuses.Add(ShipStatusType.Burn);
                DestroyBoardingHull(game, board, summon, directIncendiary);
                game.AddBoardDetailLog(physicalBoardOwnerId,
                    $"{sourceName} сгорел! ({(char)('A' + col)}{row + 1})");
                return new ShotResult
                {
                    Hit = true,
                    Destroyed = true,
                    ShipSunk = true,
                    Burned = true,
                    Row = row,
                    Col = col,
                    TurnContinues = false,
                    ForcesTurnEnd = targetsOwnBoardingHull,
                    Message = $"{sourceName} сгорел!",
                    AffectedShipName = sourceName,
                };
            }

            if (shooter.SelectedShotType == ShotType.WhiteStone && deck.Module != null)
            {
                deck.ModuleDestroyed = true;
                game.AddBoardDetailLog(physicalBoardOwnerId,
                    $"Белый камень разрушил модуль {deck.Module} на {sourceName}!");
            }

            deck.CurrentHp = Math.Max(0, deck.CurrentHp - GetDamage(shooter));
            var deckDestroyed = deck.IsDestroyed;
            if (deckDestroyed)
            {
                MarkSummonDeath(board, row, col, summon);
                if (cell.SummonRef == summon) cell.SummonRef = null;
            }
            else
            {
                cell.WasScratched = true;
            }

            summon.IsAlive = summon.BoardingDecks.Any(value => !value.IsDestroyed);
            var owner = game.GetPlayer(summon.OwnerId);
            SyncBoardingSourceState(owner, summon);
            if (!summon.IsAlive)
            {
                ResolveDeferredRevealOnDeath(game, summon, suppress: false);
                ClearSummonFromBoard(board, summon);
                MarkBoardingSourceDestroyed(owner, summon);
            }

            if (deckDestroyed)
            {
                cell.WasScratched = false;
                var message = summon.IsAlive
                    ? $"Палуба {sourceName} уничтожена!"
                    : $"{sourceName} уничтожен!";
                game.AddBoardDetailLog(physicalBoardOwnerId,
                    $"{shooter.Username}: {message} ({(char)('A' + col)}{row + 1})");
                return new ShotResult
                {
                    Hit = true,
                    Destroyed = true,
                    ShipSunk = !summon.IsAlive,
                    Row = row,
                    Col = col,
                    TurnContinues = summon.IsAlive && !targetsOwnBoardingHull,
                    ForcesTurnEnd = targetsOwnBoardingHull,
                    Message = message,
                    AffectedShipName = sourceName,
                };
            }

            game.AddBoardDetailLog(physicalBoardOwnerId,
                $"{shooter.Username} поцарапал броню {sourceName} ({(char)('A' + col)}{row + 1})");
            return new ShotResult
            {
                Hit = true,
                Scratched = true,
                Row = row,
                Col = col,
                TurnContinues = false,
                ForcesTurnEnd = targetsOwnBoardingHull,
                Message = "Поцарапал броню абордажного корабля!",
                AffectedShipName = sourceName,
            };
        }

        cell.IsHit = true;
        summon.IsAlive = false;
        MarkSummonDeath(board, row, col, summon);
        ClearSummonFromBoard(board, summon);

        ResolveDeferredRevealOnDeath(game, summon,
            shooter.SelectedShotType is ShotType.Incendiary or ShotType.EvilIncendiary);

        game.AddBoardDetailLog(physicalBoardOwnerId,
            $"{shooter.Username} уничтожил призванное существо ({(char)('A' + col)}{row + 1})");
        if (summon.Type == SummonType.Brander)
            DetonateBrander(game, game.GetOpponent(summon.OwnerId), summon, row, col, shooter);

        // Ordinary summon kill penalty: rows 0-2 = penalty, unless just spawned.
        var turnContinues = false;
        var penalty = false;
        if (!summon.IsBoardingShip && summon.OwnerId != shooter.DiscordId &&
            shooter.SelectedShotType is not (ShotType.GreekFire or ShotType.EvilGreekFire) &&
            row <= 2)
        {
            // Check if summon was just spawned (within 1 shot)
            if (!summon.IsGhost && game.ShotCount - summon.SpawnedAtShot > 1)
            {
                shooter.HasPenalty = true;
                penalty = true;
                game.AddLog($"{shooter.Username} получает штраф за уничтожение призыва в тылу!");
            }
        }
        // rows 3+ = just TurnContinues=false (already set)

        return new ShotResult
        {
            Hit = true, Row = row, Col = col, TurnContinues = turnContinues,
            Destroyed = true, PenaltyApplied = penalty, Message = penalty
                ? "Призыв уничтожен! Штраф: пропуск хода."
                : "Призванное существо уничтожено!"
        };
    }

    private static ShotResult ProcessShipHit(BattleshipGame game, BattleshipPlayer shooter, BattleshipPlayer opponent, Cell cell, int row, int col)
    {
        var ship = cell.ShipRef;

        // Find which deck was hit
        var deckIndex = GetDeckIndexAtCell(ship, row, col);
        if (deckIndex < 0 || deckIndex >= ship.Decks.Count)
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false };

        var deck = ship.Decks[deckIndex];
        if (deck.IsDestroyed)
        {
            if (shooter.SelectedShotType == ShotType.EvilIncendiary && !ship.IsDestroyed)
            {
                cell.IsHit = true;
                cell.IsMiss = false;
                cell.WasShipHit = true;
                if (!ship.Statuses.Contains(ShipStatusType.BurnResist))
                {
                    KillShipByFire(game, opponent, ship, shooter, applyBurnStatus: true);
                    HandleShipDeath(game, opponent, ship, ShipDestructionCause.EvilIncendiary);
                    game.AddBoardDetailLog(opponent.DiscordId,
                        $"Злая горючка взорвала {ship.Name} через уничтоженную палубу!");
                    return new ShotResult
                    {
                        Hit = true, Destroyed = true, Row = row, Col = col,
                        TurnContinues = true, ShipSunk = true, Burned = true,
                        Message = $"{ship.Name} взорван Злой горючкой!",
                        AffectedShipName = ship.Name
                    };
                }

                MarkFireResistance(cell);
                game.AddBoardDetailLog(opponent.DiscordId,
                    $"{ship.Name}: Корабль устоял против огня! Поцарапано.");
                return new ShotResult
                {
                    Hit = true, Scratched = true, Row = row, Col = col,
                    TurnContinues = false,
                    Message = "Корабль устоял против огня. Поцарапано.",
                    AffectedShipName = ship.Name
                };
            }

            // A dead deck is water for turn resolution. Preserve the deck's existing red
            // projection, but do not manufacture a fresh hit, reset or Mast warning.
            cell.IsMiss = true;
            cell.WasScratched = false;
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                Message = "Эта палуба уже уничтожена.", AffectedShipName = ship.Name };
        }

        cell.IsHit = true;
        cell.IsMiss = false;
        cell.WasShipHit = true;
        cell.WasDodge = false;
        cell.WasManeuverDodge = false;

        // Check auto_dodge_bow_stern (Light Wood Triple)
        if (shooter.SelectedShotType == ShotType.Ballista &&
            ship.Abilities.Contains("auto_dodge_bow_stern"))
        {
            var dodgeResult = ProcessAutoDodge(game, opponent, ship, row, col, deckIndex);
            if (dodgeResult != null) return dodgeResult;
        }

        // Check nimble/ballista_immune — reveal cell even on immune (#12: undo premature hit flags)
        if (ship.Abilities.Contains("ballista_immune") && shooter.SelectedShotType == ShotType.Ballista)
        {
            cell.IsHit = false;
            cell.WasShipHit = false;
            cell.IsRevealed = true;
            cell.WasDodge = true; // ТЗ #6: static салатовый mark for both players
            cell.WasManeuverDodge = false;
            var nimbleMsg = Random.Shared.Next(2) == 0
                ? "Юркая единичка! Опять увернулась!"
                : "Ну и юркая же она! Камней бы ей на голову!";
            game.AddBoardDetailLog(opponent.DiscordId,
                $"{nimbleMsg} ({(char)('A' + col)}{row + 1})");
            return new ShotResult { Miss = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
                Message = nimbleMsg, AffectedShipName = ship.Name };
        }

        // Incendiary: kills the entire ship on ANY hit (ТЗ #18: no cell «Горит» — fire only from Greek Fire)
        if (shooter.SelectedShotType is ShotType.Incendiary or ShotType.EvilIncendiary)
        {
            if (!ship.Statuses.Contains(ShipStatusType.BurnResist))
            {
                KillShipByFire(game, opponent, ship, shooter, applyBurnStatus: true);
                var destructionCause = shooter.SelectedShotType == ShotType.EvilIncendiary
                    ? ShipDestructionCause.EvilIncendiary
                    : ShipDestructionCause.Incendiary;
                HandleShipDeath(game, opponent, ship, destructionCause);
                var weaponName = shooter.SelectedShotType == ShotType.EvilIncendiary
                    ? "Злая горючка"
                    : "Зажигательный снаряд";
                game.AddBoardDetailLog(opponent.DiscordId,
                    $"{weaponName} сжёг {ship.Name}!");
                return new ShotResult
                {
                    Hit = true, Destroyed = true, Row = row, Col = col,
                    TurnContinues = true, ShipSunk = true, Burned = true,
                    Message = $"{ship.Name} сгорел!", AffectedShipName = ship.Name
                };
            }
            MarkFireResistance(cell);
            game.AddBoardDetailLog(opponent.DiscordId,
                $"{ship.Name}: Корабль устоял против огня! Поцарапано.");
            return new ShotResult
            {
                Hit = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
                Message = "Корабль устоял против огня. Поцарапано.", AffectedShipName = ship.Name
            };
        }

        // Calculate damage
        var damage = GetDamage(shooter);

        // White Stone destroys the struck deck's module even when 8 damage is not enough
        // to break the armor. Stun is applied once when the valid shot lands.
        if (shooter.SelectedShotType == ShotType.WhiteStone)
        {
            if (deck.Module != null)
            {
                deck.ModuleDestroyed = true;
                game.AddBoardDetailLog(opponent.DiscordId,
                    $"Белый камень разрушил модуль {deck.Module} на {ship.Name}!");
            }
        }

        // Apply damage
        deck.CurrentHp -= damage;

        // Check explode_on_hit — triggers on ANY hit, not just death
        // Any damage = full barge destruction (GDD: "При получении любого урона — взрывается")
        if (ship.Abilities.Contains("explode_on_hit") && !ship.IsDestroyed)
        {
            ExplodeShip(game, opponent, ship, shooter); // kills all decks + marks cells for both players (ТЗ #4)
            HandleShipDeath(game, opponent, ship, ShipDestructionCause.Explosion);
            game.AddBoardDetailLog(opponent.DiscordId,
                $"{ship.Name} взорвался от попадания!");
            return new ShotResult
            {
                Hit = true, Destroyed = true, ShipSunk = true,
                Row = row, Col = col, TurnContinues = true, Burned = true,
                Message = $"{ship.Name} взорвался!", AffectedShipName = ship.Name
            };
        }

        if (deck.CurrentHp <= 0)
        {
            deck.CurrentHp = 0;
            cell.WasScratched = false; // No longer scratched — deck is destroyed

            // Check if entire ship is sunk
            if (ship.IsDestroyed)
            {
                RevealShip(opponent.Board, ship, shooter);
                game.AddBoardDetailLog(opponent.DiscordId,
                    $"{shooter.Username} потопил {ship.Name}! ({(char)('A' + col)}{row + 1})");
                HandleShipDeath(game, opponent, ship, ShipDestructionCause.Shot);

                return new ShotResult
                {
                    Hit = true, Destroyed = true, ShipSunk = true,
                    Row = row, Col = col, TurnContinues = true,
                    Message = $"{ship.Name} потоплен!",
                    AffectedShipName = ship.Name
                };
            }

            // Deck destroyed but ship survives
            game.AddBoardDetailLog(opponent.DiscordId,
                $"{shooter.Username} уничтожил палубу {ship.Name}! ({(char)('A' + col)}{row + 1})");

            // ТЗ #20: the SHOOTER's mast spots the Maneuvering Double preparing to row away —
            // personal message, gated on the shooter's own mast
            if (ship.Abilities.Contains("manual_move_after_hit"))
            {
                if (HasLivingMast(shooter))
                    game.AddLogFor(shooter.DiscordId, "[Мачта] Даёт по вёслам!");
            }

            return new ShotResult
            {
                Hit = true, Destroyed = true,
                Row = row, Col = col, TurnContinues = true,
                Message = $"Палуба {ship.Name} уничтожена!",
                AffectedShipName = ship.Name
            };
        }

        // Scratched — damage didn't destroy the deck
        cell.WasScratched = true;
        game.AddBoardDetailLog(opponent.DiscordId,
            $"{shooter.Username} поцарапал броню {ship.Name} ({(char)('A' + col)}{row + 1})");
        return new ShotResult
        {
            Hit = true, Scratched = true,
            Row = row, Col = col, TurnContinues = false,
            Message = "Поцарапал броню!",
            AffectedShipName = ship.Name
        };
    }

    private static int GetDamage(BattleshipPlayer shooter)
    {
        var baseDamage = shooter.SelectedShotType switch
        {
            ShotType.WhiteStone => 8,     // 4x standard
            ShotType.Buckshot => 1,        // 0.5x standard
            ShotType.Incendiary or ShotType.EvilIncendiary => 0, // burn mechanic handles kill
            _ => 2                         // standard ballista
        };
        return baseDamage;
    }

    /// <summary>
    /// White Stone stuns the explicitly selected recipient of a valid shot.
    /// Call only after the shot has passed validation and incremented <see cref="BattleshipGame.ShotCount"/>.
    /// </summary>
    public static void ApplyWhiteStoneStun(
        BattleshipGame game,
        BattleshipPlayer shooter,
        BattleshipPlayer recipient)
    {
        if (game == null || shooter == null || recipient == null ||
            shooter.SelectedShotType != ShotType.WhiteStone)
            return;

        recipient.StunShotExpiry = game.ShotCount + 1;
    }

    private static int GetDeckIndexAtCell(Ship ship, int row, int col)
    {
        for (var i = 0; i < ship.Decks.Count; i++)
        {
            var cell = ship.GetDeckCell(ship.Decks[i], ship.Row, ship.Col, ship.Orientation);
            if (cell.row == row && cell.col == col)
                return i;
        }
        return -1;
    }

    private static void MarkFireResistance(Cell cell)
    {
        if (cell == null) return;
        cell.BurnResistMarked = true;
        cell.IsRevealed = true;
        cell.IsHit = true;
        cell.IsMiss = false;
        cell.WasShipHit = true;
        cell.WasScratched = true;
        cell.WasDodge = false;
        cell.WasManeuverDodge = false;
    }

    /// <summary>The ship deck at this cell is still alive (ТЗ #15: summons pass through 0-HP decks).</summary>
    private static bool HasAliveDeckAt(Ship ship, int row, int col)
    {
        var idx = GetDeckIndexAtCell(ship, row, col);
        return idx >= 0 && idx < ship.Decks.Count && ship.Decks[idx].CurrentHp > 0;
    }

    // ── Reveal Helpers ────────────────────────────────────────────────

    private static void RevealShip(Board board, Ship ship, BattleshipPlayer shooter = null)
    {
        var radius = ship.Space;
        var occupied = ship.GetOccupiedCells();

        ReconcileManeuverHistory(board, ship, shooter);
        ClearLatestManeuverDodge(board, ship);

        // First: reveal all cells of the destroyed ship itself (even if not directly hit)
        foreach (var (r, c) in occupied)
        {
            var shipCell = board.GetCell(r, c);
            if (shipCell != null)
            {
                // A ramming maneuver can replace a destroyed allied deck with the moving
                // ship. Reveal the current occupant anonymously instead of painting it dead.
                if (shipCell.ShipRef != ship)
                {
                    RevealCell(board, shipCell, shooter);
                    continue;
                }
                if (!shipCell.IsRevealed && shooter != null)
                    IncrementRevealedCount(shooter);
                shipCell.IsRevealed = true;
                var revealedDeck = ship.Decks.FirstOrDefault(deck =>
                {
                    var position = ship.GetDeckCell(deck, ship.Row, ship.Col, ship.Orientation);
                    return position.row == r && position.col == c;
                });
                if (revealedDeck != null)
                    RecordKnownDeck(board, shipCell, ship, revealedDeck);
                shipCell.IsHit = true; // mark all ship cells as hit for visual display
                shipCell.IsMiss = false;
                shipCell.WasShipHit = true;
                shipCell.WasScratched = false;
                shipCell.WasRevealedShip = false;
                shipCell.WasDodge = false;
                shipCell.WasManeuverDodge = false;
                shipCell.SunkShipName = ship.Name;
            }
        }

        // Then reveal the whole Space area. Empty water receives a permanent miss mark;
        // intact neighbouring ships remain anonymous but are projected as revealed occupancy.
        foreach (var (r, c) in occupied)
        {
            for (var dr = -radius; dr <= radius; dr++)
            for (var dc = -radius; dc <= radius; dc++)
            {
                var nr = r + dr;
                var nc = c + dc;
                var cell = board.GetCell(nr, nc);
                if (cell != null) RevealCell(board, cell, shooter);
            }
        }
    }

    public static void RevealArea(Board board, int centerRow, int centerCol, int radius, BattleshipPlayer shooter = null)
    {
        for (var dr = -radius; dr <= radius; dr++)
        for (var dc = -radius; dc <= radius; dc++)
        {
            var cell = board.GetCell(centerRow + dr, centerCol + dc);
            if (cell == null) continue;
            RevealCell(board, cell, shooter);
        }
    }

    private static void RevealCell(Board board, Cell cell, BattleshipPlayer beneficiary = null)
    {
        if (cell == null) return;
        if (!cell.IsRevealed && beneficiary != null)
            IncrementRevealedCount(beneficiary);
        cell.IsRevealed = true;
        if (cell.ShipRef != null)
        {
            var ship = cell.ShipRef;
            var deck = ship.Decks.FirstOrDefault(candidate =>
            {
                var position = ship.GetDeckCell(candidate, ship.Row, ship.Col, ship.Orientation);
                return position.row == cell.Row && position.col == cell.Col;
            });
            if (deck != null)
                RecordKnownDeck(board, cell, ship, deck);
        }
        else
        {
            ClearKnownDeckSnapshot(cell);
        }
        if (cell.ShipRef == null && !cell.IsBurning)
            cell.IsMiss = true;
    }

    private static void RecordKnownDeck(Board board, Cell current, Ship ship, Deck deck)
    {
        foreach (var previous in board.Grid.Cast<Cell>().Where(cell =>
                     cell != current &&
                     cell.KnownShipId == ship.Id &&
                     cell.KnownDeckIndex == deck.Index))
            ClearKnownDeckSnapshot(previous);

        current.KnownShipId = ship.Id;
        current.KnownDeckIndex = deck.Index;
        current.IsRevealed = true;
        if (deck.IsDestroyed)
        {
            current.IsHit = true;
            current.IsMiss = false;
            current.WasShipHit = true;
            current.WasScratched = false;
            current.WasRevealedShip = false;
            current.WasDodge = false;
            current.WasManeuverDodge = false;
            current.SunkShipName = ship.IsDestroyed ? ship.Name : null;
        }
        else
        {
            current.IsHit = false;
            current.IsMiss = false;
            current.WasShipHit = false;
            current.WasScratched = false;
            current.WasRevealedShip = true;
            current.SunkShipName = null;
        }
    }

    private static void ClearKnownDeckSnapshot(Cell cell)
    {
        if (cell.KnownShipId == null) return;
        cell.KnownShipId = null;
        cell.KnownDeckIndex = -1;
        cell.IsHit = false;
        cell.WasShipHit = false;
        cell.WasScratched = false;
        cell.WasRevealedShip = false;
        cell.SunkShipName = null;
        if (cell.ShipRef == null && !cell.IsBurning)
            cell.IsMiss = true;
    }

    /// <summary>
    /// Persist one discarded Matryoshka deck independently of the live ShipRef occupying the
    /// board. Transport code can therefore render the red wreck after the parent is removed.
    /// </summary>
    public static void MarkMatryoshkaWreck(Cell cell, Ship sourceShip, int sourceDeckIndex)
    {
        if (cell == null || sourceShip == null) return;
        cell.Wreck = new CellWreckState
        {
            Kind = CellWreckKind.Matryoshka,
            SourceShipId = sourceShip.Id,
            SourceShipName = sourceShip.Name,
            SourceDeckIndex = sourceDeckIndex,
        };
        cell.IsRevealed = true;
        cell.IsHit = true;
        cell.IsMiss = false;
        cell.WasShipHit = true;
        cell.WasScratched = false;
        cell.SunkShipName = sourceShip.Name;
    }

    public static void ClearCellWreck(Cell cell)
    {
        if (cell != null) cell.Wreck = null;
    }

    private static void ReconcileManeuverHistory(Board board, Ship ship, BattleshipPlayer beneficiary)
    {
        if (!ship.IsDestroyed) return;
        if (ship.ManeuverStaleHitCells.Count == 0)
        {
            ship.HasHiddenMovement = false;
            return;
        }
        var currentCells = ship.GetOccupiedCells().ToHashSet();
        foreach (var (row, col) in ship.ManeuverStaleHitCells.Distinct())
        {
            if (currentCells.Contains((row, col))) continue;
            var cell = board.GetCell(row, col);
            if (cell == null) continue;
            if (!cell.IsRevealed && beneficiary != null) IncrementRevealedCount(beneficiary);
            cell.IsRevealed = true;
            cell.IsHit = false;
            cell.IsMiss = true;
            cell.WasShipHit = false;
            cell.WasScratched = false;
            cell.WasRevealedShip = false;
            cell.WasDodge = false;
            cell.WasManeuverDodge = false;
            cell.BurnResistMarked = false;
            cell.SunkShipName = null;
            cell.KnownShipId = null;
            cell.KnownDeckIndex = -1;
        }
        ship.ManeuverStaleHitCells.Clear();
        ship.HasHiddenMovement = false;
    }

    private static void ClearLatestManeuverDodge(Board board, Ship ship)
    {
        if (ship.LastManeuverDodgeRow < 0 || ship.LastManeuverDodgeCol < 0) return;
        var previous = board.GetCell(ship.LastManeuverDodgeRow, ship.LastManeuverDodgeCol);
        if (previous != null) previous.WasManeuverDodge = false;
        ship.LastManeuverDodgeRow = -1;
        ship.LastManeuverDodgeCol = -1;
    }

    private static void MarkManeuverDodgeMiss(Board board, Ship ship, int row, int col)
    {
        ClearLatestManeuverDodge(board, ship);

        var cell = board.GetCell(row, col);
        if (cell == null) return;

        // ProcessShot observes the occupied cell before the Light Wood ability resolves.
        // A successful dodge supersedes that provisional deck snapshot with revealed water.
        ClearKnownDeckSnapshot(cell);
        cell.KnownShipId = null;
        cell.KnownDeckIndex = -1;
        cell.IsRevealed = true;
        cell.IsHit = false;
        cell.IsMiss = true;
        cell.WasShipHit = false;
        cell.WasScratched = false;
        cell.WasRevealedShip = false;
        cell.BurnResistMarked = false;
        cell.WasDodge = false;
        cell.SunkShipName = null;
        cell.WasManeuverDodge = true;
        ship.LastManeuverDodgeRow = row;
        ship.LastManeuverDodgeCol = col;
    }

    private static void IncrementRevealedCount(BattleshipPlayer player)
    {
        if (player.RevealedCellCount < 100)
            player.RevealedCellCount++;
    }

    // ── Ship Death ────────────────────────────────────────────────────

    private static void HandleShipDeath(
        BattleshipGame game,
        BattleshipPlayer owner,
        Ship ship,
        ShipDestructionCause cause = ShipDestructionCause.Shot)
    {
        if (cause == ShipDestructionCause.Capture)
            ship.MatryoshkaReplacementSuppressionReasons |=
                MatryoshkaReplacementSuppression.Capture;
        else if (cause == ShipDestructionCause.Devastated)
            ship.MatryoshkaReplacementSuppressionReasons |=
                MatryoshkaReplacementSuppression.Devastated;

        var passiveDisabled = ship.Statuses.Contains(ShipStatusType.Capture);
        var suppressDeathSummon = passiveDisabled || cause is ShipDestructionCause.Incendiary or
            ShipDestructionCause.EvilIncendiary or ShipDestructionCause.GreekFire
            or ShipDestructionCause.Explosion or ShipDestructionCause.Capture;

        // Store pending pirate boat deploy on death (Bug #2: delayed ability, not auto-spawn)
        if (ship.Abilities.Contains("spawn_pirate_boat"))
        {
            if (!suppressDeathSummon)
            {
                var occupiedCols = ship.GetOccupiedCells().Select(c => c.col).Distinct().OrderBy(c => c).ToList();
                owner.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.PirateBoat,
                    AllowedColumns = occupiedCols,
                    Speed = 1,
                    SourceShipName = ship.Name,
                    SourceShipId = ship.Id,
                });
                game.AddBoardDetailLog(owner.DiscordId,
                    $"Пиратская лодка готова к выпуску после гибели {ship.Name}!");
            }
        }

        // Store pending cursed boat deploy on death
        if (ship.Abilities.Contains("spawn_cursed_boat"))
        {
            if (!suppressDeathSummon)
            {
                var cursedCols = ship.GetOccupiedCells().Select(c => c.col).Distinct().OrderBy(c => c).ToList();
                owner.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.CursedBoat,
                    AllowedColumns = cursedCols,
                    Speed = 1,
                    CollisionDamage = 999,
                    SourceShipName = ship.Name,
                    SourceShipId = ship.Id,
                });
                game.AddBoardDetailLog(owner.DiscordId,
                    $"Проклятый корабль готов к выпуску после гибели {ship.Name}!");
            }
        }

        // Explode on death (Incendiary Barge still explodes on full death too)
        if (!passiveDisabled && ship.Abilities.Contains("explode_on_hit") && cause != ShipDestructionCause.Capture)
        {
            var deathAttacker = game.GetOpponent(owner.DiscordId);
            ExplodeShip(game, owner, ship, deathAttacker);
        }

        // The Fast Warming Ship explodes either at its owner's shot threshold (resolved by
        // TriggerOverheatExplosion) or immediately when a Burn-producing effect kills it.
        // Ordinary shot/collision/poison/freeze deaths deliberately remain non-explosive.
        if (!passiveDisabled && ship.Abilities.Contains("overheat_after_20_shots") &&
            cause is ShipDestructionCause.Incendiary or ShipDestructionCause.EvilIncendiary or
                ShipDestructionCause.GreekFire or ShipDestructionCause.Explosion)
        {
            var deathAttacker = game.GetOpponent(owner.DiscordId);
            ExplodeShip(game, owner, ship, deathAttacker);
        }

        MarkAssemblyEligibility(game, owner, ship);
    }

    private static void MarkAssemblyEligibility(
        BattleshipGame game,
        BattleshipPlayer owner,
        Ship destroyedComponent)
    {
        if (!destroyedComponent.IsAssemblyComponent ||
            string.IsNullOrWhiteSpace(destroyedComponent.AssemblyGroupId))
            return;

        var group = owner.Board.PlacedShips
            .Where(candidate =>
                candidate.IsAssemblyComponent &&
                candidate.AssemblyGroupId == destroyedComponent.AssemblyGroupId)
            .ToList();
        if (group.Count != 3 || group.Count(candidate => !candidate.IsDestroyed) != 1)
            return;

        var turnsUntilOwnerCanAssemble =
            game.CurrentTurnPlayerId == owner.DiscordId ? 2 : 1;
        var eligibleTurn = game.TurnNumber + turnsUntilOwnerCanAssemble;
        foreach (var component in group.Where(component => component.AssemblyEligibleTurnNumber < 0))
            component.AssemblyEligibleTurnNumber = eligibleTurn;
    }

    /// <summary>
    /// Ship explosion (Incendiary Barge / explode_on_hit): the ship dies whole — all decks and
    /// cells marked killed for BOTH players (ТЗ #4.1/#5) — and the area within ExplosionRadius
    /// explodes. Idempotent via Ship.HasExploded (death paths re-enter through HandleShipDeath).
    /// </summary>
    private static void ExplodeShip(BattleshipGame game, BattleshipPlayer owner, Ship ship, BattleshipPlayer attacker = null)
    {
        if (ship.HasExploded) return;
        ship.HasExploded = true;
        var radius = ship.ExplosionRadius > 0 ? ship.ExplosionRadius : ship.Space;
        KillShipByFire(game, owner, ship, attacker, applyBurnStatus: true);
        ExplodeArea(game, owner, ship.GetOccupiedCells(), radius, ship.Name, attacker);
    }

    /// <summary>
    /// Resolve the living Fast Warming hull after its owner reaches twenty valid shots.
    /// Returns true only when a new explosion was produced.
    /// </summary>
    public static bool TriggerOverheatExplosion(BattleshipGame game, BattleshipPlayer owner)
    {
        if (game == null || owner == null || owner.TotalShotsFired < 20) return false;
        var ship = owner.Board.PlacedShips.FirstOrDefault(candidate =>
            !candidate.IsDestroyed &&
            !candidate.Statuses.Any(status => status is
                ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) &&
            candidate.Abilities.Contains("overheat_after_20_shots") &&
            !candidate.HasExploded);
        if (ship == null) return false;

        game.AddBoardDetailLog(owner.DiscordId,
            $"{ship.Name} перегрелся после 20-го выстрела и взорвался!");
        ExplodeShip(game, owner, ship, game.GetOpponent(owner.DiscordId));
        HandleShipDeath(game, owner, ship, ShipDestructionCause.Explosion);
        return true;
    }

    /// <summary>
    /// Unified explosion zone (ТЗ #4/#5/#13): every cell within Chebyshev radius of the center
    /// cells — empty water → «Промах»; ship with a deck in radius → the WHOLE ship is killed
    /// (BurnResist ships are marked black instead, no damage); summons die (scouts transmit
    /// first). Statuses are written so both players see them. No cell «Горит» (ТЗ #18).
    /// </summary>
    public static void ExplodeArea(BattleshipGame game, BattleshipPlayer boardOwner, List<(int row, int col)> centerCells, int radius, string sourceName, BattleshipPlayer attacker = null)
    {
        var processed = new HashSet<(int, int)>();
        var resistLogged = new HashSet<string>();
        foreach (var (cr, cc) in centerCells)
        {
            for (var dr = -radius; dr <= radius; dr++)
            for (var dc = -radius; dc <= radius; dc++)
            {
                var r = cr + dr;
                var c = cc + dc;
                if (!processed.Add((r, c))) continue;
                var cell = boardOwner.Board.GetCell(r, c);
                if (cell == null) continue;

                RevealCell(boardOwner.Board, cell, attacker);

                if (cell.SummonRef != null && cell.SummonRef.IsAlive)
                    KillSummonByExplosion(game, boardOwner, cell, sourceName, attacker);

                if (cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
                {
                    var target = cell.ShipRef;
                    if (!target.Statuses.Contains(ShipStatusType.BurnResist))
                    {
                        var isIncendiaryBargeBlast = sourceName == "Incendiary Barge";
                        KillShipByFire(game, boardOwner, target, attacker, isIncendiaryBargeBlast);
                        game.AddBoardDetailLog(boardOwner.DiscordId,
                            $"{target.Name} сгорел от взрыва! ({sourceName})");
                        HandleShipDeath(game, boardOwner, target, ShipDestructionCause.Explosion); // may chain-explode
                    }
                    else
                    {
                        MarkFireResistance(cell);
                        if (resistLogged.Add(target.Id))
                            game.AddBoardDetailLog(boardOwner.DiscordId,
                                $"{target.Name}: Корабль устоял против огня! Поцарапано.");
                    }
                }
                else if (cell.ShipRef == null && !cell.IsBurning)
                {
                    cell.IsMiss = true; // ТЗ #4: пустая клетка в радиусе — «Промах»
                }
            }
        }
    }

    /// <summary>
    /// Kill a ship by fire/explosion: zero all decks, mark every cell killed for BOTH players
    /// (own view IsHit + fog snapshot WasShipHit, ТЗ #4/#5), reveal it. The internal Burn ship
    /// status (blocks pirate/cursed spawn-on-death) is kept — it is NOT the cell «Горит» display.
    /// </summary>
    private static void KillShipByFire(
        BattleshipGame game,
        BattleshipPlayer owner,
        Ship ship,
        BattleshipPlayer attacker = null,
        bool applyBurnStatus = false)
    {
        foreach (var d in ship.Decks) d.CurrentHp = 0;
        if (applyBurnStatus && !ship.Statuses.Contains(ShipStatusType.Burn))
            ship.Statuses.Add(ShipStatusType.Burn);
        RevealShip(owner.Board, ship, attacker);
        foreach (var (r, c) in ship.GetOccupiedCells())
        {
            var cell = owner.Board.GetCell(r, c);
            if (cell == null || cell.ShipRef != ship) continue;
            cell.IsHit = true;
            cell.WasShipHit = true;
            cell.WasScratched = false;
        }
    }

    private static void KillSummonByExplosion(
        BattleshipGame game,
        BattleshipPlayer boardOwner,
        Cell cell,
        string sourceName,
        BattleshipPlayer attacker)
    {
        var explodedSummon = cell.SummonRef;

        if (HasBoardingHull(explodedSummon) &&
            BoardingHasStatus(explodedSummon, ShipStatusType.BurnResist))
        {
            var wasAlreadyMarked = cell.BurnResistMarked;
            MarkFireResistance(cell);
            if (!wasAlreadyMarked)
                game.AddBoardDetailLog(boardOwner.DiscordId,
                    $"{explodedSummon.SourceShipName ?? "Абордажный корабль"}: Корабль устоял против огня! Поцарапано.");
            return;
        }

        if (HasBoardingHull(explodedSummon))
        {
            if (!explodedSummon.BoardingStatuses.Contains(ShipStatusType.Burn))
                explodedSummon.BoardingStatuses.Add(ShipStatusType.Burn);
            DestroyBoardingHull(game, boardOwner.Board, explodedSummon, suppressDeferredReveal: false);
            game.AddBoardDetailLog(boardOwner.DiscordId,
                $"{explodedSummon.SourceShipName ?? "Абордажный корабль"} сгорел от взрыва! ({sourceName})");
            return;
        }

        TransmitScoutReveal(game, explodedSummon);

        explodedSummon.IsAlive = false;
        MarkSummonDeath(boardOwner.Board, cell.Row, cell.Col, explodedSummon);
        cell.SummonRef = null;
        game.AddBoardDetailLog(boardOwner.DiscordId,
            $"Призванное существо сгорело от взрыва! ({sourceName})");
        if (explodedSummon.Type == SummonType.Brander)
            DetonateBrander(game, boardOwner, explodedSummon, cell.Row, cell.Col, attacker);
    }

    private static void DetonateBrander(
        BattleshipGame game,
        BattleshipPlayer boardOwner,
        Summon brander,
        int row,
        int col,
        BattleshipPlayer attacker = null)
    {
        if (brander.HasDetonated) return;
        brander.HasDetonated = true;
        brander.IsAlive = false;
        MarkSummonDeath(boardOwner.Board, row, col, brander);
        var cell = boardOwner.Board.GetCell(row, col);
        if (cell?.SummonRef == brander) cell.SummonRef = null;
        ExplodeArea(game, boardOwner, new List<(int row, int col)> { (row, col) }, 1, "Брандер", attacker);
        game.AddBoardDetailLog(boardOwner.DiscordId,
            $"Брандер взорвался! ({(char)('A' + col)}{row + 1})");
    }

    // ── Win Condition ────────────────────────────────────────────────

    public static (bool gameOver, string winnerId) CheckWinCondition(BattleshipGame game)
    {
        var p1 = game.Player1;
        var p2 = game.Player2;
        if (p1 == null || p2 == null) return (false, null);

        // Once Boarding has begun, a sole surviving Desiccator guarantees victory even
        // when both sides had one at the transition and temporarily cancelled each other.
        var desiccatorWin = CheckDesiccatorBoardingWin(game);
        if (desiccatorWin.gameOver) return desiccatorWin;

        var destroyedWin = CheckFleetDestructionWin(game);
        if (destroyedWin.gameOver) return destroyedWin;

        if (!HasDamageCapability(game, p1)) return (true, p2.DiscordId);
        if (!HasDamageCapability(game, p2)) return (true, p1.DiscordId);

        return (false, null);
    }

    public static (bool gameOver, string winnerId) CheckFleetDestructionWin(BattleshipGame game)
    {
        var p1 = game.Player1;
        var p2 = game.Player2;
        if (p1 == null || p2 == null) return (false, null);

        var p1AllDestroyed = p1.Board.PlacedShips.Where(s => !s.Statuses.Contains(ShipStatusType.Capture))
                                 .All(s => s.IsDestroyed) &&
                             !HasLivingOrPendingBoardingShip(p1);
        var p2AllDestroyed = p2.Board.PlacedShips.Where(s => !s.Statuses.Contains(ShipStatusType.Capture))
                                 .All(s => s.IsDestroyed) &&
                             !HasLivingOrPendingBoardingShip(p2);

        if (p1AllDestroyed) return (true, p2.DiscordId);
        if (p2AllDestroyed) return (true, p1.DiscordId);
        return (false, null);
    }

    private static bool HasLivingOrPendingBoardingShip(BattleshipPlayer player)
    {
        return player.PendingSummons.Any(s => s.IsBoarding) ||
               player.Summons.Any(s => s.IsAlive && s.IsBoardingShip);
    }

    private static bool HasDamageCapability(BattleshipGame game, BattleshipPlayer player)
    {
        if (HasAnyLegalShot(game, player)) return true;
        if (player.MandatoryBoardingSummonSlots > 0 ||
            player.MandatoryBoardingBrander ||
            player.PendingSummons.Any(s => s.IsMandatoryBoarding))
            return true;
        if (player.Summons.Any(s => s.IsAlive && s.Type is SummonType.Ram or SummonType.Brander or SummonType.CursedBoat))
            return true;
        return player.PendingSummons.Any(s => s.Type is SummonType.Ram or SummonType.Brander or SummonType.CursedBoat);
    }

    // ── First Turn Calculator ────────────────────────────────────────

    public static string DetermineFirstTurn(BattleshipGame game)
    {
        var p1 = game.Player1;
        var p2 = game.Player2;
        // 1. More unspent coins → goes first
        if (p1.CoinsRemaining != p2.CoinsRemaining)
            return p1.CoinsRemaining > p2.CoinsRemaining ? p1.DiscordId : p2.DiscordId;

        // 2. Fewer upgraded ships → goes first
        var p1Upgraded = p1.Fleet.Count(s => s.Cost > 0 || s.Upgrades.Count > 0);
        var p2Upgraded = p2.Fleet.Count(s => s.Cost > 0 || s.Upgrades.Count > 0);
        if (p1Upgraded != p2Upgraded)
            return p1Upgraded < p2Upgraded ? p1.DiscordId : p2.DiscordId;

        // 3. Fewer "домашний" ships → goes first
        var p1Home = p1.Fleet.Count(s => s.IsHome);
        var p2Home = p2.Fleet.Count(s => s.IsHome);
        if (p1Home != p2Home)
            return p1Home < p2Home ? p1.DiscordId : p2.DiscordId;

        // 4. Seeded random — stable for this game id, subjective nickname ordering is not a rule.
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(game.GameId));
        var seed = BitConverter.ToInt32(seedBytes, 0);
        return new Random(seed).Next(2) == 0 ? p1.DiscordId : p2.DiscordId;
    }

    // ── Boarding ─────────────────────────────────────────────────────

    public static bool CheckBoardingTrigger(BattleshipGame game)
    {
        if (game.BoardingTriggered || game.Phase != BsGamePhase.Combat) return false;
        return game.GetPlayers().Any(player =>
            !player.Board.PlacedShips.Any(s => s.Range == RangeClass.Mid && !s.IsSummon && !s.IsDestroyed &&
                !s.Statuses.Contains(ShipStatusType.Capture)));
    }

    public static void TriggerBoarding(BattleshipGame game)
    {
        if (game.BoardingTriggered || game.IsFinished) return;
        game.BoardingTriggered = true;
        var currentPlayer = game.GetPlayer(game.CurrentTurnPlayerId);
        var currentOpponent = game.GetOpponent(game.CurrentTurnPlayerId);
        var boardingPlayer = currentOpponent != null && !HasLivingMidShip(currentOpponent)
            ? currentOpponent
            : currentPlayer != null && !HasLivingMidShip(currentPlayer)
                ? currentPlayer
                : game.GetPlayers().FirstOrDefault(player => !HasLivingMidShip(player));
        game.BoardingPlayerId = boardingPlayer?.DiscordId;

        // Resolve this before entering the deployment phase. Two living Desiccators only
        // suppress this auto-win while both remain alive; their other passives stay active.
        var desiccatorWin = CheckDesiccatorBoardingWin(game);
        if (desiccatorWin.gameOver)
        {
            game.IsFinished = true;
            game.WinnerId = desiccatorWin.winnerId;
            game.Phase = BsGamePhase.GameOver;
            game.AddLog("Иссушитель обеспечил автоматическую победу до расстановки абордажных кораблей!");
            return;
        }

        game.Phase = BsGamePhase.Boarding;

        PrepareMandatoryBoardingSummons(game, boardingPlayer);

        // Final Boarding belongs to exactly one side for the rest of the match. A later
        // loss of the other Mid line never converts that second fleet.
        QueueBoardingShipsForMidlessPlayers(game);
        foreach (var player in game.GetPlayers().Where(player =>
                     player.BoardingDeploymentCapacity <= 0))
            DiscardMandatoryBoardingRemainder(game, player);

        // Every Buckshot-configured Tetracatapult receives one White Stone at Boarding.
        // Second Ammo overrides that amount with two White Stones regardless of its loadout.
        foreach (var p in game.GetPlayers())
        {
            foreach (var ship in p.Board.PlacedShips)
            {
                if (ship.IsDestroyed) continue;
                var operationalTetracatapults = ship.Weapons
                    .Where(weapon => weapon.Type == WeaponType.Tetracatapult &&
                                     IsWeaponOperational(ship, weapon))
                    .ToList();
                foreach (var tetraWeapon in operationalTetracatapults)
                {
                    var hasSecondAmmo = ship.Abilities.Contains("extra_ammo_boarding");
                    var bonus = hasSecondAmmo
                        ? 2
                        : tetraWeapon.ConfiguredShotType == ShotType.Buckshot ? 1 : 0;
                    if (bonus == 0) continue;

                    var whiteStoneWeapon = ship.Weapons.FirstOrDefault(weapon =>
                        weapon.Type == WeaponType.Tetracatapult &&
                        weapon.DeckIndex == tetraWeapon.DeckIndex &&
                        weapon.ConfiguredShotType == ShotType.WhiteStone);
                    if (whiteStoneWeapon == null)
                    {
                        whiteStoneWeapon = new Weapon
                        {
                            Type = WeaponType.Tetracatapult,
                            Ammo = 0,
                            MaxAmmo = 0,
                            AimSpeed = tetraWeapon.AimSpeed,
                            ShipId = ship.Id,
                            DeckIndex = tetraWeapon.DeckIndex,
                            PreservedModuleDestroyed = tetraWeapon.PreservedModuleDestroyed,
                            ConfiguredShotType = ShotType.WhiteStone,
                        };
                        ship.Weapons.Add(whiteStoneWeapon);
                    }

                    whiteStoneWeapon.Ammo += bonus;
                    whiteStoneWeapon.MaxAmmo += bonus;
                    AddSharedTetracatapultAmmo(game, p, ShotType.WhiteStone, bonus);
                    if (hasSecondAmmo)
                        game.AddBoardDetailLog(p.DiscordId,
                            $"Доп. снаряды: +2 Белых камня для {ship.Name}!");
                }
            }
        }

        // Горючка weapons get +1 shot on boarding
        foreach (var p in game.GetPlayers())
        {
            foreach (var ship in p.Board.PlacedShips)
            {
                if (ship.IsDestroyed) continue;
                foreach (var w in ship.Weapons.Where(w =>
                             w.Type is WeaponType.Incendiary or WeaponType.EvilIncendiary &&
                             IsWeaponOperational(ship, w)))
                {
                    w.Ammo += 1;
                    w.MaxAmmo += 1;
                }
            }
        }
    }

    private static void PrepareMandatoryBoardingSummons(
        BattleshipGame game,
        BattleshipPlayer boardingPlayer)
    {
        foreach (var player in game.GetPlayers())
        {
            if (player.BoardingSummonsPrepared) continue;
            player.BoardingSummonsPrepared = true;
            if (player != boardingPlayer) continue;
            var opponent = game.GetOpponent(player.DiscordId);
            player.BoardingDeploymentCapacity = opponent == null
                ? 0
                : Enumerable.Range(0, 10).Count(col =>
                    opponent.Board.GetCell(0, col)?.SummonRef is not { IsAlive: true });

            foreach (var pending in player.PendingSummons)
                pending.IsMandatoryBoarding = true;

            foreach (var crewShip in player.Board.PlacedShips.Where(ship =>
                         !ship.IsDestroyed &&
                         !ship.Statuses.Any(status => status is
                             ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) &&
                         ship.Abilities.Contains("crew_boarding_pirate")))
            {
                player.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.PirateBoat,
                    AllowedColumns = crewShip.GetOccupiedCells()
                        .Select(cell => cell.col)
                        .Distinct()
                        .OrderBy(col => col)
                        .ToList(),
                    Speed = 1,
                    SourceShipName = crewShip.Name,
                    SourceShipId = crewShip.Id,
                    IsMandatoryBoarding = true,
                });
                game.AddBoardDetailLog(player.DiscordId,
                    $"Экипаж {crewShip.Name} готов выпустить Пиратскую лодку!");
            }

            var summonRegions = player.Fleet.SelectMany(ship => ship.Regions).ToHashSet();
            player.MandatoryBoardingSummonSlots =
                summonRegions.Overlaps(new[] { Region.West, Region.East, Region.South })
                    ? Math.Max(0, player.MaxSummonSlots - player.SummonSlotsUsed)
                    : 0;
            player.MandatoryBoardingBrander =
                !player.BranderUsed &&
                player.Fleet.Any(ship => ship.Abilities.Contains("brander_summon"));
        }
    }

    private static bool HasLivingMidShip(BattleshipPlayer player)
    {
        return player.Board.PlacedShips.Any(s => s.Range == RangeClass.Mid && !s.IsSummon &&
            !s.IsDestroyed && !s.Statuses.Contains(ShipStatusType.Capture));
    }

    public static void QueueBoardingShipsForMidlessPlayers(BattleshipGame game)
    {
        foreach (var player in game.GetPlayers().Where(player =>
                     player.DiscordId == game.BoardingPlayerId && !HasLivingMidShip(player)))
        {
            var boardingShips = player.Board.PlacedShips
                .Where(ship =>
                    !ship.IsDestroyed &&
                    ship.Decks.Any(deck => deck.Index == 0 && !deck.IsDestroyed) &&
                    !ship.IsSummon &&
                    !ship.Statuses.Contains(ShipStatusType.Capture) &&
                    ship.Range is RangeClass.Close or RangeClass.CloseMelee)
                .ToList();
            foreach (var ship in boardingShips)
            {
                ship.IsSummon = true;
                player.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.Ram,
                    Speed = ship.Speed,
                    CollisionDamage = 4,
                    RevealRadius = ship.Space,
                    IsBoarding = true,
                    IsMandatoryBoarding = true,
                    SourceShipName = ship.Name,
                    SourceShipId = ship.Id,
                    SourceShipDeckCount = ship.Decks.Count,
                    BoardingDecks = CloneBoardingDecks(ship.Decks),
                    BoardingAbilities = ship.Abilities.ToList(),
                    BoardingStatuses = ship.Statuses.ToList(),
                });
                RemoveBoardingSourceFromOwnBoard(player, ship);
                game.AddBoardDetailLog(player.DiscordId,
                    $"{ship.Name} готов к абордажу! Разместите на первой строчке вражеского поля.");
            }
        }
    }

    private static List<Deck> CloneBoardingDecks(IEnumerable<Deck> decks)
    {
        return (decks ?? Enumerable.Empty<Deck>()).Select(deck => new Deck
        {
            Index = deck.Index,
            OffsetRow = deck.OffsetRow,
            OffsetCol = deck.OffsetCol,
            MaxHp = deck.MaxHp,
            CurrentHp = deck.CurrentHp,
            Module = deck.Module,
            ModuleDestroyed = deck.ModuleDestroyed,
        }).ToList();
    }

    /// <summary>
    /// Materialize a pending deployment without sharing its mutable hull snapshot. Transport
    /// callers should use this factory so every converted ship retains its armor and traits.
    /// </summary>
    public static Summon CreateSummonFromPending(
        PendingSummonDeploy pending,
        string ownerId,
        int row,
        int col,
        Direction direction,
        int spawnedAtShot,
        bool useGhostSummons)
    {
        if (pending == null) return null;
        return new Summon
        {
            Type = pending.Type,
            Row = row,
            Col = col,
            Speed = pending.Speed,
            CollisionDamage = pending.CollisionDamage,
            RevealRadius = pending.RevealRadius,
            OwnerId = ownerId,
            MoveDirection = direction,
            SpawnedAtShot = spawnedAtShot,
            IsBoardingShip = pending.IsBoarding,
            SourceShipId = pending.SourceShipId,
            SourceShipName = pending.SourceShipName,
            SourceShipDeckCount = pending.SourceShipDeckCount,
            BoardingDecks = CloneBoardingDecks(pending.BoardingDecks),
            BoardingAbilities = pending.BoardingAbilities?.ToList() ?? new List<string>(),
            BoardingStatuses = pending.BoardingStatuses?.ToList() ?? new List<ShipStatusType>(),
            IsGhost = !pending.IsBoarding && useGhostSummons,
        };
    }

    private static void RemoveBoardingSourceFromOwnBoard(BattleshipPlayer player, Ship ship)
    {
        foreach (var (row, col) in ship.GetOccupiedCells())
        {
            var cell = player.Board.GetCell(row, col);
            if (cell?.ShipRef != ship) continue;
            cell.ShipRef = null;
            cell.WasBoardingSourceCell = true;
        }

        player.Board.PlacedShips.Remove(ship);
        ship.IsPlaced = false;
        ship.Row = -1;
        ship.Col = -1;
    }

    public static void DiscardMandatoryBoardingRemainder(
        BattleshipGame game,
        BattleshipPlayer player)
    {
        var pending = player.PendingSummons.Where(summon => summon.IsMandatoryBoarding).ToList();
        foreach (var boarding in pending.Where(summon =>
                     summon.IsBoarding && summon.SourceShipId != null))
        {
            var source = player.Fleet.FirstOrDefault(ship => ship.Id == boarding.SourceShipId);
            if (source == null) continue;
            foreach (var deck in source.Decks)
                deck.CurrentHp = 0;
        }

        var discarded = pending.Count +
                        player.MandatoryBoardingSummonSlots +
                        (player.MandatoryBoardingBrander ? 1 : 0);
        player.PendingSummons.RemoveAll(summon => summon.IsMandatoryBoarding);
        if (player.MandatoryBoardingSummonSlots > 0)
            player.SummonSlotsUsed = player.MaxSummonSlots;
        if (player.MandatoryBoardingBrander)
            player.BranderUsed = true;
        player.MandatoryBoardingSummonSlots = 0;
        player.MandatoryBoardingBrander = false;
        player.BoardingDeploymentCapacity = 0;
        if (discarded > 0)
            game.AddBoardDetailLog(player.DiscordId,
                $"{player.Username}: {discarded} лишних единиц исчезли при абордаже.");
    }

    private static (bool gameOver, string winnerId) CheckDesiccatorBoardingWin(BattleshipGame game)
    {
        if (!game.BoardingTriggered) return (false, null);
        var owners = game.GetPlayers().Where(HasLivingDesiccator)
            .ToList();
        return owners.Count == 1 ? (true, owners[0].DiscordId) : (false, null);
    }

    private static bool HasLivingDesiccator(BattleshipPlayer player)
    {
        if (player.Board.PlacedShips.Any(s =>
                s.Abilities.Contains("auto_win_boarding") && !s.IsDestroyed &&
                !s.Statuses.Contains(ShipStatusType.Capture)))
            return true;

        var boardingSourceIds = player.PendingSummons
            .Where(s => s.IsBoarding && s.SourceShipId != null)
            .Select(s => s.SourceShipId)
            .Concat(player.Summons
                .Where(s => s.IsAlive && s.IsBoardingShip && s.SourceShipId != null)
                .Select(s => s.SourceShipId))
            .ToHashSet();
        return player.Fleet.Any(s =>
            boardingSourceIds.Contains(s.Id) &&
            !s.IsDestroyed &&
            s.Abilities.Contains("auto_win_boarding"));
    }

    private static bool HasBoardingHull(Summon summon) =>
        summon is { IsBoardingShip: true } && summon.BoardingDecks.Count > 0;

    private static void EnsureBoardingSnapshot(BattleshipPlayer owner, Summon summon)
    {
        if (summon is not { IsBoardingShip: true } || owner == null ||
            summon.SourceShipId == null || summon.BoardingDecks.Count > 0)
            return;
        var source = owner.Fleet.FirstOrDefault(ship => ship.Id == summon.SourceShipId);
        if (source == null) return;
        summon.BoardingDecks = CloneBoardingDecks(source.Decks);
        summon.BoardingAbilities = source.Abilities.ToList();
        summon.BoardingStatuses = source.Statuses.ToList();
        summon.SourceShipDeckCount = source.Decks.Count;
    }

    private static (int row, int col) GetBoardingDeckCell(
        Summon summon,
        Deck deck,
        int row,
        int col,
        Direction direction)
    {
        var hasExplicitOffset = deck.OffsetRow.HasValue && deck.OffsetCol.HasValue;
        var baseRowOffset = hasExplicitOffset
            ? deck.OffsetRow.Value
            : summon.BoardingAbilities.Contains("diagonal_shape") ? deck.Index : 0;
        var baseColOffset = hasExplicitOffset ? deck.OffsetCol.Value : deck.Index;
        var (rowOffset, colOffset) = direction switch
        {
            Direction.Right => (baseRowOffset, baseColOffset),
            Direction.Down => (baseColOffset, -baseRowOffset),
            Direction.Left => (-baseRowOffset, -baseColOffset),
            Direction.Up => (-baseColOffset, baseRowOffset),
            _ => (baseRowOffset, baseColOffset),
        };
        return (row + rowOffset, col + colOffset);
    }

    private static List<(int row, int col, Deck deck)> GetLiveBoardingDeckCells(
        Summon summon,
        int? row = null,
        int? col = null,
        Direction? direction = null)
    {
        if (!HasBoardingHull(summon)) return new List<(int row, int col, Deck deck)>();
        var anchorRow = row ?? summon.Row;
        var anchorCol = col ?? summon.Col;
        var resolvedDirection = direction ?? summon.MoveDirection;
        return summon.BoardingDecks
            .Where(deck => !deck.IsDestroyed)
            .Select(deck =>
            {
                var cell = GetBoardingDeckCell(
                    summon, deck, anchorRow, anchorCol, resolvedDirection);
                return (cell.row, cell.col, deck);
            })
            .ToList();
    }

    /// <summary>
    /// Resolve every currently living physical deck of a converted hull. Legacy boarding
    /// snapshots without deck data retain their historical single anchor cell.
    /// </summary>
    public static List<(int row, int col)> GetLiveBoardingOccupiedCells(
        Summon summon,
        int? row = null,
        int? col = null,
        Direction? direction = null)
    {
        if (summon is not { IsAlive: true, IsBoardingShip: true })
            return new List<(int row, int col)>();
        if (!HasBoardingHull(summon))
            return new List<(int row, int col)> { (row ?? summon.Row, col ?? summon.Col) };
        return GetLiveBoardingDeckCells(summon, row, col, direction)
            .Select(value => (value.row, value.col))
            .Distinct()
            .ToList();
    }

    private static List<(int row, int col)> GetLiveSummonOccupiedCells(
        Summon summon,
        int? row = null,
        int? col = null,
        Direction? direction = null)
    {
        if (summon is not { IsAlive: true }) return new List<(int row, int col)>();
        if (summon.IsBoardingShip)
            return GetLiveBoardingOccupiedCells(summon, row, col, direction);
        return new List<(int row, int col)> { (row ?? summon.Row, col ?? summon.Col) };
    }

    /// <summary>Remove every cell reference owned by this summon, including a sparse hull.</summary>
    public static void ClearSummonFromBoard(Board board, Summon summon)
    {
        if (board == null || summon == null) return;
        foreach (var cell in board.Grid.Cast<Cell>())
        {
            if (cell.SummonRef == summon) cell.SummonRef = null;
        }
    }

    /// <summary>
    /// Validate only full-hull geometry. Course selection may point at another summon: movement
    /// will wait atomically until that destination becomes free instead of deadlocking the turn.
    /// </summary>
    public static bool CanFitSummonHullInBounds(
        Summon summon,
        int row,
        int col,
        Direction direction)
    {
        if (summon is not { IsAlive: true }) return false;
        var occupied = GetLiveSummonOccupiedCells(summon, row, col, direction);
        return occupied.Count > 0 && occupied.Distinct().Count() == occupied.Count &&
               occupied.All(position =>
                   position.row is >= 0 and < 10 && position.col is >= 0 and < 10);
    }

    /// <summary>
    /// Validate the full live hull without mutating board state. Existing cells owned by this
    /// summon may overlap the destination.
    /// </summary>
    public static bool CanPlaceSummonOnBoard(
        Board board,
        Summon summon,
        int row,
        int col,
        Direction direction)
    {
        if (board == null || !CanFitSummonHullInBounds(summon, row, col, direction)) return false;
        var occupied = GetLiveSummonOccupiedCells(summon, row, col, direction);
        return occupied.All(position =>
               {
                   var occupant = board.GetCell(position.row, position.col)?.SummonRef;
                   return occupant is not { IsAlive: true } || occupant == summon;
               });
    }

    /// <summary>
    /// Atomically place the full live hull. Another living summon or an invalid/duplicated hull
    /// coordinate rejects it without disturbing the current position.
    /// </summary>
    public static bool TryPlaceSummonOnBoard(
        Board board,
        Summon summon,
        int row,
        int col,
        Direction direction)
    {
        if (!CanPlaceSummonOnBoard(board, summon, row, col, direction)) return false;
        var occupied = GetLiveSummonOccupiedCells(summon, row, col, direction);

        ClearSummonFromBoard(board, summon);
        summon.Row = row;
        summon.Col = col;
        summon.MoveDirection = direction;
        foreach (var (occupiedRow, occupiedCol) in occupied)
            board.GetCell(occupiedRow, occupiedCol).SummonRef = summon;
        return true;
    }

    /// <summary>Resolve the living physical deck of a converted boarding hull at a board cell.</summary>
    public static Deck GetLiveBoardingDeckAtCell(Summon summon, int row, int col)
    {
        return GetLiveBoardingDeckCells(summon)
            .FirstOrDefault(value => value.row == row && value.col == col).deck;
    }

    private static bool BoardingHasAbility(Summon summon, string ability) =>
        summon is { IsBoardingShip: true } && summon.BoardingAbilities.Contains(ability);

    private static bool BoardingHasStatus(Summon summon, ShipStatusType status) =>
        summon is { IsBoardingShip: true } &&
        (summon.BoardingStatuses.Contains(status) ||
         status == ShipStatusType.BurnResist && BoardingHasAbility(summon, "burn_resist"));

    private static void SyncBoardingSourceState(BattleshipPlayer owner, Summon summon)
    {
        if (!HasBoardingHull(summon) || owner == null || summon.SourceShipId == null) return;
        var source = owner.Fleet.FirstOrDefault(ship => ship.Id == summon.SourceShipId);
        if (source == null) return;
        foreach (var snapshot in summon.BoardingDecks)
        {
            var sourceDeck = source.Decks.FirstOrDefault(deck => deck.Index == snapshot.Index);
            if (sourceDeck == null) continue;
            sourceDeck.MaxHp = snapshot.MaxHp;
            sourceDeck.CurrentHp = snapshot.CurrentHp;
            sourceDeck.Module = snapshot.Module;
            sourceDeck.ModuleDestroyed = snapshot.ModuleDestroyed;
            sourceDeck.OffsetRow = snapshot.OffsetRow;
            sourceDeck.OffsetCol = snapshot.OffsetCol;
        }
        source.Statuses.Clear();
        source.Statuses.AddRange(summon.BoardingStatuses.Distinct());
    }

    private enum SummonEntryOutcome
    {
        Continue,
        Stop,
        Dead,
    }

    private static void DestroySummonAtCurrentPosition(
        BattleshipGame game,
        BattleshipPlayer owner,
        BattleshipPlayer boardOwner,
        Summon summon,
        bool suppressDeferredReveal = false,
        bool frozen = false)
    {
        if (HasBoardingHull(summon))
        {
            DestroyBoardingHull(
                game, boardOwner.Board, summon, suppressDeferredReveal, frozen);
            return;
        }

        ResolveDeferredRevealOnDeath(game, summon, suppressDeferredReveal || frozen);
        MarkSummonDeath(boardOwner.Board, summon.Row, summon.Col, summon, frozen);
        summon.IsAlive = false;
        summon.WaitingForTurnBack = false;
        summon.WaitingForDirectionChoice = false;
        ClearSummonFromBoard(boardOwner.Board, summon);
        MarkBoardingSourceDestroyed(owner, summon);
    }

    private static void ProcessBoardingDrakkarFreezeAura(
        BattleshipGame game,
        BattleshipPlayer owner,
        BattleshipPlayer boardOwner,
        Summon summon)
    {
        if (!HasBoardingHull(summon) ||
            !BoardingHasAbility(summon, "freeze_nearby") ||
            !summon.IsAlive)
            return;

        var auraCells = GetLiveBoardingDeckCells(summon)
            .Select(value => (value.row, value.col))
            .ToList();
        var radius = summon.RevealRadius;
        var targets = boardOwner.Board.PlacedShips.Where(ship =>
                !ship.IsDestroyed &&
                !ship.Statuses.Contains(ShipStatusType.Capture) &&
                ship.Decks.Where(deck => !deck.IsDestroyed).Any(deck =>
                {
                    var target = ship.GetDeckCell(deck, ship.Row, ship.Col, ship.Orientation);
                    return auraCells.Any(aura =>
                        Math.Abs(aura.row - target.row) <= radius &&
                        Math.Abs(aura.col - target.col) <= radius);
                }))
            .ToList();

        foreach (var target in targets)
        {
            if (!target.Statuses.Contains(ShipStatusType.Freeze))
                target.Statuses.Add(ShipStatusType.Freeze);
            foreach (var deck in target.Decks)
                deck.CurrentHp = 0;
        }
        foreach (var target in targets)
        {
            RevealShip(boardOwner.Board, target, owner);
            HandleShipDeath(game, boardOwner, target, ShipDestructionCause.Freeze);
            game.AddBoardDetailLog(boardOwner.DiscordId,
                $"Аура абордажного Драккара заморозила {target.Name}!");
        }
    }

    private static SummonEntryOutcome ResolveSummonEntry(
        BattleshipGame game,
        BattleshipPlayer owner,
        BattleshipPlayer boardOwner,
        Summon summon)
    {
        var occupied = GetLiveSummonOccupiedCells(summon);
        foreach (var (row, col) in occupied)
        {
            MarkSummonTrail(boardOwner.Board, row, col, summon);
            AccumulateDeferredReveal(game, summon, boardOwner, row, col);
        }

        // Grab destroys a whole converted Boarding hull when any living deck enters the
        // trap. Drakkar is the sole exception and crosses the cell normally. This is a death,
        // not an ordinary summon capture: no cloned Ram/reward is created for the trap owner.
        if (HasBoardingHull(summon) &&
            !IsBoardingDrakkar(owner, summon) &&
            occupied.Any(position =>
                BattleshipCapturingMechanics.FindLiveGrabShip(
                    boardOwner, position.row, position.col) != null))
        {
            DestroySummonAtCurrentPosition(game, owner, boardOwner, summon);
            return SummonEntryOutcome.Dead;
        }

        foreach (var (row, col) in occupied)
        {
            if (!BattleshipCapturingMechanics.TryGrabSummon(
                    game, owner, boardOwner, summon, row, col))
                continue;
            MarkBoardingSourceDestroyed(owner, summon);
            return SummonEntryOutcome.Dead;
        }

        var burningCells = occupied
            .Select(position => boardOwner.Board.GetCell(position.row, position.col))
            .Where(cell => cell is { IsBurning: true })
            .ToList();
        if (burningCells.Count > 0)
        {
            if (HasBoardingHull(summon) &&
                BoardingHasStatus(summon, ShipStatusType.BurnResist))
            {
                foreach (var burningCell in burningCells)
                    MarkFireResistance(burningCell);
                game.AddBoardDetailLog(boardOwner.DiscordId,
                    $"{summon.SourceShipName ?? "Абордажный корабль"}: Корабль устоял против огня! Поцарапано.");
            }
            else
            {
                if (HasBoardingHull(summon) &&
                    !summon.BoardingStatuses.Contains(ShipStatusType.Burn))
                    summon.BoardingStatuses.Add(ShipStatusType.Burn);
                DestroySummonAtCurrentPosition(game, owner, boardOwner, summon);
                game.AddBoardDetailLog(boardOwner.DiscordId,
                    $"Призванное существо сгорело в огне! ({(char)('A' + summon.Col)}{summon.Row + 1})");
                if (summon.Type == SummonType.Brander)
                    DetonateBrander(game, boardOwner, summon, summon.Row, summon.Col, owner);
                return SummonEntryOutcome.Dead;
            }
        }

        if (occupied.Any(position => IsInFreezeZone(boardOwner, position.row, position.col)))
        {
            FreezeSummon(game, owner, boardOwner, summon, summon.Row, summon.Col);
            return SummonEntryOutcome.Dead;
        }

        // A converted Drakkar resolves its hostile ship aura before physical collision. It
        // deliberately never iterates summons, so allied boats/boarding hulls are untouched.
        ProcessBoardingDrakkarFreezeAura(game, owner, boardOwner, summon);

        var collisions = GetLiveSummonOccupiedCells(summon)
            .Select(position =>
            {
                var cell = boardOwner.Board.GetCell(position.row, position.col);
                return (position.row, position.col, ship: cell?.ShipRef);
            })
            .Where(value => value.ship != null &&
                            (HasAliveDeckAt(value.ship, value.row, value.col) ||
                             summon.Type == SummonType.PirateBoat &&
                             value.ship.Statuses.Contains(ShipStatusType.Devastated) &&
                             GetDeckIndexAtCell(value.ship, value.row, value.col) >= 0))
            .GroupBy(value => value.ship.Id)
            .Select(group => group.First())
            .ToList();

        foreach (var collision in collisions)
        {
            HandleSummonCollision(
                game, summon, collision.ship, boardOwner, collision.row, collision.col);
            if (summon.Type == SummonType.CursedBoat)
                summon.WaitingForDirectionChoice = true;
            if (!summon.IsAlive)
            {
                DestroySummonAtCurrentPosition(game, owner, boardOwner, summon);
                return SummonEntryOutcome.Dead;
            }
            if (summon.WaitingForDirectionChoice)
                return SummonEntryOutcome.Stop;
            if (!summon.IsBoardingShip)
            {
                DestroySummonAtCurrentPosition(game, owner, boardOwner, summon);
                return SummonEntryOutcome.Dead;
            }
        }

        if (GetLiveSummonOccupiedCells(summon).Any(position =>
                IsInPoisonConeFromAnotherSource(
                    game, boardOwner, summon, position.row, position.col)))
        {
            DestroySummonAtCurrentPosition(game, owner, boardOwner, summon);
            AddPoisonSummonMastWarning(game, owner);
            return SummonEntryOutcome.Dead;
        }

        if (!summon.IsBoardingShip &&
            summon.Type is SummonType.Ram or SummonType.PirateBoat)
        {
            foreach (var (row, col) in GetLiveSummonOccupiedCells(summon))
                RevealCell(boardOwner.Board, boardOwner.Board.GetCell(row, col), owner);
        }

        return summon.WaitingForDirectionChoice
            ? SummonEntryOutcome.Stop
            : SummonEntryOutcome.Continue;
    }

    private static bool IsBoardingDrakkar(BattleshipPlayer owner, Summon summon) =>
        owner?.Fleet.Any(ship =>
            ship.Id == summon?.SourceShipId && ship.DefinitionId == "drakkar") == true;

    /// <summary>
    /// A mandatory Matryoshka replacement can appear while summon movement is paused. Re-run
    /// persistent board hazards and physical overlap resolution before movement resumes from
    /// the already-advanced cursor at its next unprocessed work item.
    /// </summary>
    public static void ResolveMatryoshkaPlacementInteractions(
        BattleshipGame game,
        BattleshipPlayer boardOwner,
        Ship placedShip)
    {
        if (game == null || boardOwner == null || placedShip == null) return;

        if (placedShip.GetOccupiedCells().Any(position =>
                boardOwner.Board.GetCell(position.row, position.col)?.IsBurning == true))
        {
            foreach (var deck in placedShip.Decks) deck.CurrentHp = 0;
            if (!placedShip.Statuses.Contains(ShipStatusType.Burn))
                placedShip.Statuses.Add(ShipStatusType.Burn);
            RevealShip(boardOwner.Board, placedShip, game.GetOpponent(boardOwner.DiscordId));
            HandleShipDeath(game, boardOwner, placedShip, ShipDestructionCause.GreekFire);
            return;
        }

        var hostileDrakkars = game.GetPlayers()
            .Where(owner => owner.DiscordId != boardOwner.DiscordId)
            .SelectMany(owner => owner.Summons.Select(summon => (owner, summon)))
            .Where(value =>
                value.summon.IsAlive &&
                BoardingHasAbility(value.summon, "freeze_nearby") &&
                GetLiveSummonOccupiedCells(value.summon).Any(position =>
                    boardOwner.Board.GetCell(position.row, position.col)?.SummonRef ==
                    value.summon))
            .ToList();

        foreach (var (owner, summon) in hostileDrakkars)
        {
            ProcessBoardingDrakkarFreezeAura(game, owner, boardOwner, summon);
            if (placedShip.IsDestroyed) return;
        }

        var overlappingSummons = placedShip.GetOccupiedCells()
            .Select(position => boardOwner.Board.GetCell(position.row, position.col)?.SummonRef)
            .Where(summon => summon is { IsAlive: true })
            .DistinctBy(summon => summon.Id)
            .ToList();

        foreach (var summon in overlappingSummons)
        {
            var owner = game.GetPlayer(summon.OwnerId);
            if (owner == null || owner.DiscordId == boardOwner.DiscordId) continue;
            ResolveSummonEntry(game, owner, boardOwner, summon);
            if (placedShip.IsDestroyed) return;
        }

        RefreshPoisonZones(game);
        if (!game.PoisonZonesByBoardOwner.TryGetValue(
                boardOwner.DiscordId, out var poisonZone) ||
            !placedShip.GetOccupiedCells().Any(poisonZone.Contains))
            return;

        foreach (var deck in placedShip.Decks) deck.CurrentHp = 0;
        RevealShip(boardOwner.Board, placedShip, game.GetOpponent(boardOwner.DiscordId));
        HandleShipDeath(game, boardOwner, placedShip, ShipDestructionCause.Poison);
    }

    /// <summary>
    /// Poison is indiscriminate, but a converted Alchi hull cannot collide with the stale cone
    /// produced by its own previous position while it advances. Resolve current physical sources
    /// directly and exclude only the entering source itself.
    /// </summary>
    private static bool IsInPoisonConeFromAnotherSource(
        BattleshipGame game,
        BattleshipPlayer boardOwner,
        Summon enteringSummon,
        int row,
        int col)
    {
        if (boardOwner.Board.PlacedShips.Any(ship =>
                !ship.IsDestroyed &&
                !ship.Statuses.Contains(ShipStatusType.Capture) &&
                ship.Abilities.Contains("poison_cone") &&
                GetPoisonConeCells(ship).Contains((row, col))))
            return true;

        return game.GetPlayers().SelectMany(player => player.Summons).Any(source =>
            source != enteringSummon &&
            source.IsAlive &&
            BoardingHasAbility(source, "poison_cone") &&
            GetLiveSummonOccupiedCells(source).Any(position =>
                boardOwner.Board.GetCell(position.row, position.col)?.SummonRef == source) &&
            GetBoardingPoisonConeCells(source).Contains((row, col)));
    }

    // ── Summon Movement ──────────────────────────────────────────────

    /// <summary>
    /// Move every eligible summon in stable player/list order. When <paramref name="shouldPause"/>
    /// returns true, the durable cursor on <paramref name="game"/> retains the next unprocessed
    /// work item. Calling this method again resumes instead of replaying movement or poison damage.
    /// </summary>
    /// <returns>True when the whole movement and poison resolution completed; false when paused.</returns>
    public static bool MoveSummons(
        BattleshipGame game,
        Func<BattleshipGame, bool> shouldPause = null)
    {
        if (game == null) return true;
        if (game.SummonMovementState == null)
        {
            RefreshPoisonZones(game);
            game.SummonMovementState = new SummonMovementResolutionState
            {
                PlayerIds = game.GetPlayers().Select(player => player.DiscordId).ToList(),
            };
        }

        var state = game.SummonMovementState;
        while (true)
        {
            switch (state.Phase)
            {
                case SummonMovementPhase.PreparePlayer:
                {
                    if (state.PlayerIndex >= state.PlayerIds.Count)
                    {
                        state.Phase = SummonMovementPhase.ResolvePoison;
                        continue;
                    }

                    var player = game.GetPlayer(state.PlayerIds[state.PlayerIndex]);
                    var opponent = player == null ? null : game.GetOpponent(player.DiscordId);
                    if (player == null || opponent == null)
                    {
                        state.PlayerIndex++;
                        continue;
                    }

                    state.JustMaterializedSummonIds = player.Summons
                        .Where(summon => summon.IsAlive && summon.IsGhost &&
                                         summon.SpawnedAtShot < game.ShotCount)
                        .Select(summon => summon.Id)
                        .ToList();
                    state.MaterializeIndex = 0;
                    state.MovingSummonIds.Clear();
                    state.MovingSummonIndex = 0;
                    state.MovingStepIndex = 0;
                    state.Phase = SummonMovementPhase.MaterializeGhosts;
                    continue;
                }

                case SummonMovementPhase.MaterializeGhosts:
                {
                    var player = game.GetPlayer(state.PlayerIds[state.PlayerIndex]);
                    if (state.MaterializeIndex < state.JustMaterializedSummonIds.Count)
                    {
                        var summonId = state.JustMaterializedSummonIds[state.MaterializeIndex++];
                        var summon = player?.Summons.FirstOrDefault(value => value.Id == summonId);
                        if (player != null && summon != null)
                        {
                            summon.IsGhost = false;
                            RegisterSummonOnTargetBoard(game, player, summon);
                        }

                        if (shouldPause?.Invoke(game) == true) return false;
                        continue;
                    }

                    if (player != null)
                    {
                        var justMaterializedIds = state.JustMaterializedSummonIds.ToHashSet();
                        state.MovingSummonIds = player.Summons
                            .Where(summon => summon.IsAlive && !summon.IsGhost &&
                                             !justMaterializedIds.Contains(summon.Id) &&
                                             !summon.WaitingForTurnBack &&
                                             !summon.WaitingForDirectionChoice &&
                                             summon.SpawnedAtShot < game.ShotCount)
                            .Select(summon => summon.Id)
                            .ToList();
                    }

                    state.MovingSummonIndex = 0;
                    state.MovingStepIndex = 0;
                    state.Phase = SummonMovementPhase.MoveSummons;
                    continue;
                }

                case SummonMovementPhase.MoveSummons:
                {
                    if (state.MovingSummonIndex >= state.MovingSummonIds.Count)
                    {
                        state.Phase = SummonMovementPhase.CleanupPlayer;
                        continue;
                    }

                    var player = game.GetPlayer(state.PlayerIds[state.PlayerIndex]);
                    var opponent = player == null ? null : game.GetOpponent(player.DiscordId);
                    var summon = player?.Summons.FirstOrDefault(value =>
                        value.Id == state.MovingSummonIds[state.MovingSummonIndex]);
                    if (player == null || opponent == null || summon is not { IsAlive: true } ||
                        state.MovingStepIndex >= summon.Speed)
                    {
                        AdvanceSummonMovementCursor(state);
                        continue;
                    }

                    var continueMoving = MoveSummonOneStep(game, player, opponent, summon);
                    state.MovingStepIndex++;
                    if (!continueMoving || state.MovingStepIndex >= summon.Speed)
                        AdvanceSummonMovementCursor(state);

                    if (shouldPause?.Invoke(game) == true) return false;
                    continue;
                }

                case SummonMovementPhase.CleanupPlayer:
                {
                    var player = game.GetPlayer(state.PlayerIds[state.PlayerIndex]);
                    var opponent = player == null ? null : game.GetOpponent(player.DiscordId);
                    if (player != null && opponent != null)
                    {
                        foreach (var deadSummon in player.Summons.Where(summon => !summon.IsAlive))
                        {
                            ClearSummonFromBoard(opponent.Board, deadSummon);
                            MarkBoardingSourceDestroyed(player, deadSummon);
                        }
                        player.Summons.RemoveAll(summon => !summon.IsAlive);
                    }

                    state.PlayerIndex++;
                    state.Phase = SummonMovementPhase.PreparePlayer;
                    if (shouldPause?.Invoke(game) == true) return false;
                    continue;
                }

                case SummonMovementPhase.ResolvePoison:
                    state.PoisonState ??= CreatePoisonResolutionState(game);
                    if (!ContinuePoisonResolution(game, state.PoisonState, shouldPause))
                        return false;
                    state.Phase = SummonMovementPhase.Complete;
                    continue;

                case SummonMovementPhase.Complete:
                    game.SummonMovementState = null;
                    return true;

                default:
                    throw new InvalidOperationException($"Unknown summon movement phase: {state.Phase}");
            }
        }
    }

    private static void AdvanceSummonMovementCursor(SummonMovementResolutionState state)
    {
        state.MovingSummonIndex++;
        state.MovingStepIndex = 0;
    }

    /// <summary>Resolve exactly one speed step; false means the current summon is done this cycle.</summary>
    private static bool MoveSummonOneStep(
        BattleshipGame game,
        BattleshipPlayer player,
        BattleshipPlayer opponent,
        Summon summon)
    {
        var (newRow, newCol) = GetNextPosition(summon.Row, summon.Col, summon.MoveDirection);
        var destination = GetLiveSummonOccupiedCells(
            summon, newRow, newCol, summon.MoveDirection);

        // A multi-deck hull turns back before any living deck would leave the map.
        if (destination.Count == 0 || destination.Any(position =>
                position.row is < 0 or >= 10 || position.col is < 0 or >= 10))
        {
            // Every Ram, including a converted boarding ship, can be redirected.
            if (summon.Type == SummonType.Ram || summon.IsBoardingShip)
            {
                ClearSummonFromBoard(opponent.Board, summon);
                summon.WaitingForTurnBack = true;
            }
            else
            {
                DestroySummonAtCurrentPosition(game, player, opponent, summon);
            }
            return false;
        }

        // Another live summon blocks the whole destination atomically. The old hull remains
        // registered and no trail/hazard/collision is resolved.
        if (!TryPlaceSummonOnBoard(
                opponent.Board, summon, newRow, newCol, summon.MoveDirection))
            return false;

        return ResolveSummonEntry(game, player, opponent, summon) == SummonEntryOutcome.Continue;
    }

    private static void FreezeSummon(
        BattleshipGame game,
        BattleshipPlayer summonOwner,
        BattleshipPlayer boardOwner,
        Summon summon,
        int row,
        int col)
    {
        var occupied = GetLiveSummonOccupiedCells(summon);
        if (occupied.Count == 0) occupied.Add((row, col));
        foreach (var (deathRow, deathCol) in occupied)
            RevealCell(boardOwner.Board, boardOwner.Board.GetCell(deathRow, deathCol), summonOwner);
        DestroySummonAtCurrentPosition(
            game, summonOwner, boardOwner, summon, suppressDeferredReveal: true, frozen: true);
        game.AddLogFor(boardOwner.DiscordId, "[Драккар] Призванное существо заморожено аурой Драккара");
        if (HasLivingMast(summonOwner))
            game.AddLogFor(summonOwner.DiscordId, "[Мачта] Наших заморозили!");
    }

    /// <summary>Register a spawn/re-entry and immediately resolve its entry hazards.</summary>
    public static bool RegisterSummonOnTargetBoard(BattleshipGame game, BattleshipPlayer owner, Summon summon)
    {
        var boardOwner = game.GetOpponent(owner.DiscordId);
        if (boardOwner == null || summon == null) return false;
        if (summon.IsBoardingShip)
        {
            summon.IsGhost = false;
            EnsureBoardingSnapshot(owner, summon);
        }
        if (!TryPlaceSummonOnBoard(
                boardOwner.Board, summon, summon.Row, summon.Col, summon.MoveDirection))
            return false;

        if (summon.IsGhost)
        {
            foreach (var (row, col) in GetLiveSummonOccupiedCells(summon))
                MarkSummonTrail(boardOwner.Board, row, col, summon);
            return true;
        }

        RefreshPoisonZones(game);
        ResolveSummonEntry(game, owner, boardOwner, summon);
        if (summon.IsAlive && BoardingHasAbility(summon, "poison_cone"))
            ProcessPoisonCones(game);
        return true;
    }

    private static SummonMarker CreateSummonMarker(Summon summon) => new()
    {
        SummonId = summon.Id,
        Type = summon.Type,
        IsBoardingShip = summon.IsBoardingShip,
        SourceShipName = summon.SourceShipName,
    };

    private static void MarkSummonTrail(Board board, int row, int col, Summon summon)
    {
        var cell = board.GetCell(row, col);
        if (cell == null || cell.SummonTrails.Any(marker =>
                summon.IsBoardingShip
                    ? marker.SummonId == summon.Id
                    : !marker.IsBoardingShip && marker.Type == summon.Type))
            return;
        cell.SummonTrails.Add(CreateSummonMarker(summon));
    }

    private static void MarkSummonDeath(
        Board board,
        int row,
        int col,
        Summon summon,
        bool frozen = false)
    {
        var cell = board.GetCell(row, col);
        if (cell == null) return;
        if (cell.SummonDeaths.Any(marker => marker.SummonId == summon.Id)) return;
        cell.SummonDeaths.Add(CreateSummonMarker(summon));
        if (frozen)
            cell.FrozenSummonDeathIndices.Add(cell.SummonDeaths.Count - 1);
    }

    private static void MarkBoardingSourceDestroyed(BattleshipPlayer owner, Summon summon)
    {
        if (summon is not { IsAlive: false, IsBoardingShip: true } ||
            summon.SourceShipId == null || owner == null)
            return;
        SyncBoardingSourceState(owner, summon);
        var source = owner.Fleet.FirstOrDefault(ship => ship.Id == summon.SourceShipId);
        if (source == null || source.IsDestroyed) return;
        foreach (var deck in source.Decks)
            deck.CurrentHp = 0;
    }

    private static (int row, int col) GetNextPosition(int row, int col, Direction dir)
    {
        return dir switch
        {
            Direction.Up => (row - 1, col),
            Direction.Down => (row + 1, col),
            Direction.Left => (row, col - 1),
            Direction.Right => (row, col + 1),
            _ => (row, col)
        };
    }

    private static void HandleSummonCollision(BattleshipGame game, Summon summon, Ship targetShip, BattleshipPlayer targetOwner, int collisionRow, int collisionCol)
    {
        var attacker = game.GetOpponent(targetOwner.DiscordId);
        var coord = $"({(char)('A' + collisionCol)}{collisionRow + 1})";
        switch (summon.Type)
        {
            case SummonType.Ram:
                // Boarding Close ships: devastate 1-2 deckers (continue), ram 3-4 deckers (die)
                if (summon.IsBoardingShip)
                {
                    if (targetShip.Decks.Count <= 2 ||
                        BoardingHasAbility(summon, "spawn_cursed_boat"))
                    {
                        var destroyedLivingDeck = targetShip.Decks.Any(deck => !deck.IsDestroyed);
                        if (!targetShip.Statuses.Contains(ShipStatusType.Devastated))
                            targetShip.Statuses.Add(ShipStatusType.Devastated);
                        foreach (var d in targetShip.Decks) d.CurrentHp = 0;
                        AddRamManeuverWarning(game, attacker, targetShip, destroyedLivingDeck);
                        RevealShip(targetOwner.Board, targetShip, attacker);
                        HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Devastated);
                        game.AddBoardDetailLog(targetOwner.DiscordId,
                            $"Абордажный корабль опустошил {targetShip.Name}! {coord}");
                        // Ordinary devastation leaves the Boarding hull alive. A more-specific
                        // target death passive (notably Incendiary Barge explosion) may kill it;
                        // never resurrect that already-resolved hull or queue an empty choice.
                        if (summon.IsAlive && BoardingHasAbility(summon, "spawn_cursed_boat"))
                            summon.WaitingForDirectionChoice = true;
                    }
                    else
                    {
                        var collisionDeckIndex = GetDeckIndexAtCell(targetShip, collisionRow, collisionCol);
                        var collisionDeck = collisionDeckIndex >= 0 ? targetShip.Decks[collisionDeckIndex] : null;
                        if (collisionDeck is { IsDestroyed: false })
                        {
                            var wasAlive = !collisionDeck.IsDestroyed;
                            collisionDeck.CurrentHp -= summon.CollisionDamage;
                            if (collisionDeck.CurrentHp < 0) collisionDeck.CurrentHp = 0;
                            AddRamManeuverWarning(
                                game, attacker, targetShip, wasAlive && collisionDeck.IsDestroyed);
                            game.AddBoardDetailLog(targetOwner.DiscordId,
                                $"Абордажный корабль протаранил {targetShip.Name}! (-{summon.CollisionDamage} HP) {coord}");
                            // Mark cell on target owner's board (#8)
                            MarkRamDamageOnBoard(targetOwner, attacker, collisionRow, collisionCol, collisionDeck);
                            if (targetShip.IsDestroyed)
                            {
                                RevealShip(targetOwner.Board, targetShip, attacker);
                                HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Collision);
                            }
                        }
                        summon.IsAlive = false; // Boarding ship dies against 3-4 deckers
                    }
                    break;
                }

                // Regular ram: 4 damage, dies on collision
                var ramDeckIndex = GetDeckIndexAtCell(targetShip, collisionRow, collisionCol);
                var ramDeck = ramDeckIndex >= 0 ? targetShip.Decks[ramDeckIndex] : null;
                if (ramDeck is { IsDestroyed: false })
                {
                    var wasAlive = !ramDeck.IsDestroyed;
                    ramDeck.CurrentHp -= summon.CollisionDamage;
                    if (ramDeck.CurrentHp < 0) ramDeck.CurrentHp = 0;
                    AddRamManeuverWarning(
                        game, attacker, targetShip, wasAlive && ramDeck.IsDestroyed);
                    game.AddBoardDetailLog(targetOwner.DiscordId,
                        $"Таран врезался в {targetShip.Name}! (-{summon.CollisionDamage} HP) {coord}");
                    // Mark cell on target owner's board (#8)
                    MarkRamDamageOnBoard(targetOwner, attacker, collisionRow, collisionCol, ramDeck);
                    // Ram triggers barge explosion (#9); both players see decks + zone statuses (ТЗ #5)
                    if (targetShip.Abilities.Contains("explode_on_hit") && !targetShip.IsDestroyed)
                    {
                        ExplodeShip(game, targetOwner, targetShip, attacker);
                        HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Explosion);
                        game.AddBoardDetailLog(targetOwner.DiscordId,
                            $"{targetShip.Name} взорвался от тарана! {coord}");
                    }
                    else if (targetShip.IsDestroyed)
                    {
                        RevealShip(targetOwner.Board, targetShip, attacker);
                        HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Collision);
                    }
                }
                break;

            case SummonType.PirateBoat:
                var wasDevastated =
                    targetShip.Statuses.Contains(ShipStatusType.Devastated);
                if (wasDevastated)
                    RestoreDevastatedShip(game, targetOwner, targetShip, fullRepair: false);
                if (wasDevastated || targetShip.Decks.Count <= 2)
                {
                    if (!targetShip.Statuses.Contains(ShipStatusType.Capture))
                    {
                        targetShip.Statuses.Add(ShipStatusType.Capture);
                        BattleshipCapturingMechanics.GrantCaptureReward(
                            game, attacker, targetOwner, targetShip);
                        targetOwner.SelectedShotType = ShotType.Ballista;
                        targetOwner.SelectedWeapon = null;
                        foreach (var (r, c) in targetShip.GetOccupiedCells())
                        {
                            var capturedCell = targetOwner.Board.GetCell(r, c);
                            if (capturedCell == null) continue;
                            RevealCell(targetOwner.Board, capturedCell, attacker);
                        }
                        game.AddBoardDetailLog(targetOwner.DiscordId,
                            $"Пиратская лодка захватила {targetShip.Name}! {coord}");
                    }
                    else game.AddBoardDetailLog(targetOwner.DiscordId,
                        $"{targetShip.Name} уже находится под CAPTURE. {coord}");
                }
                else
                {
                    game.AddBoardDetailLog(targetOwner.DiscordId,
                        $"Пиратская лодка разбилась о {targetShip.Name}! {coord}");
                }
                break;

            case SummonType.CursedBoat:
                if (!targetShip.Statuses.Contains(ShipStatusType.Devastated))
                    targetShip.Statuses.Add(ShipStatusType.Devastated);
                foreach (var d in targetShip.Decks) d.CurrentHp = 0;
                RevealShip(targetOwner.Board, targetShip, attacker);
                HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Devastated);
                game.AddBoardDetailLog(targetOwner.DiscordId,
                    $"Проклятый корабль опустошил {targetShip.Name}! {coord}");
                break;

            case SummonType.Scout:
                // Reveal accumulated data on collision/death
                var summonOwner = game.GetPlayer(summon.OwnerId);
                TransmitScoutReveal(game, summon);
                RevealArea(targetOwner.Board, collisionRow, collisionCol, summon.RevealRadius, summonOwner);
                game.AddBoardDetailLog(targetOwner.DiscordId,
                    "Разведчик обнаружил корабли противника!");
                break;

            case SummonType.Brander:
                // ТЗ #16: Brander cannot pass live decks — разбивается без детонации (решение дизайнера)
                game.AddBoardDetailLog(targetOwner.DiscordId,
                    $"Брандер разбился о {targetShip.Name}! {coord}");
                break;
        }
    }

    private static void AddRamManeuverWarning(
        BattleshipGame game,
        BattleshipPlayer attacker,
        Ship targetShip,
        bool destroyedLivingDeck)
    {
        if (!destroyedLivingDeck || attacker == null ||
            !targetShip.Abilities.Contains("manual_move_after_hit") ||
            !HasLivingMast(attacker))
            return;

        game.AddLogFor(attacker.DiscordId, "[Мачта] Даёт по вёслам!");
    }

    public static bool RestoreDevastatedShip(
        BattleshipGame game,
        BattleshipPlayer owner,
        Ship ship,
        bool fullRepair)
    {
        if (ship == null ||
            !ship.Statuses.Contains(ShipStatusType.Devastated) ||
            ship.Statuses.Contains(ShipStatusType.Capture))
            return false;

        if (fullRepair && owner.UseSharedTetracatapultAmmo)
        {
            GetSharedTetracatapultAmmo(game, owner, ShotType.WhiteStone);
            GetSharedTetracatapultAmmo(game, owner, ShotType.Buckshot);
        }

        ship.Statuses.Remove(ShipStatusType.Devastated);
        ship.MatryoshkaReplacementSuppressionReasons &=
            ~MatryoshkaReplacementSuppression.Devastated;
        ship.Statuses.Remove(ShipStatusType.Burn);
        foreach (var deck in ship.Decks)
        {
            deck.CurrentHp = deck.MaxHp;
            if (fullRepair)
                deck.ModuleDestroyed = false;
        }
        if (fullRepair)
        {
            foreach (var weapon in ship.Weapons)
                weapon.PreservedModuleDestroyed = false;
            foreach (var weapon in ship.Weapons)
            {
                weapon.Ammo = weapon.MaxAmmo;
                if (weapon.Type == WeaponType.Tetracatapult)
                    AddSharedTetracatapultAmmo(
                        game, owner, weapon.ConfiguredShotType, Math.Max(0, weapon.MaxAmmo));
            }
        }

        ship.AssemblyEligibleTurnNumber = -1;
        owner.PendingSummons.RemoveAll(pending =>
            pending.SourceShipId == ship.Id &&
            !pending.IsBoarding);

        foreach (var deck in ship.Decks)
        {
            var (row, col) = ship.GetDeckCell(deck, ship.Row, ship.Col, ship.Orientation);
            var cell = owner.Board.GetCell(row, col);
            if (cell?.ShipRef != ship) continue;
            cell.IsHit = false;
            cell.WasShipHit = false;
            cell.WasScratched = false;
            cell.WasRevealedShip = true;
            cell.SunkShipName = null;
            cell.BurnResistMarked = false;
            RecordKnownDeck(owner.Board, cell, ship, deck);
        }

        return true;
    }

    /// <summary>
    /// Mark ram damage on the target owner's board cells (#8).
    /// </summary>
    private static void MarkRamDamageOnBoard(
        BattleshipPlayer targetOwner,
        BattleshipPlayer attacker,
        int row,
        int col,
        Deck hitDeck)
    {
        var cell = targetOwner.Board.GetCell(row, col);
        if (cell == null) return;
        RevealCell(targetOwner.Board, cell, attacker);
        cell.IsHit = true;
        cell.IsMiss = false;
        cell.WasShipHit = true;
        cell.WasDodge = false;
        if (hitDeck.IsDestroyed)
        {
            cell.WasScratched = false;
        }
        else
        {
            cell.WasScratched = true;
        }
    }

    // ── Ship Abilities ────────────────────────────────────────────────

    /// <summary>
    /// Drakkar Freeze Aura: check if a position is within any alive Drakkar's Space radius.
    /// </summary>
    private static bool IsInFreezeZone(BattleshipPlayer player, int row, int col)
    {
        foreach (var ship in player.Board.PlacedShips)
        {
            if (ship.IsDestroyed ||
                ship.Statuses.Any(status => status is
                    ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) ||
                !ship.Abilities.Contains("freeze_nearby"))
                continue;
            var occupied = ship.GetOccupiedCells();
            foreach (var (sr, sc) in occupied)
            {
                if (Math.Abs(sr - row) <= ship.Space && Math.Abs(sc - col) <= ship.Space)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Process Drakkar freeze auras: kill enemy summons within range after movement.
    /// </summary>
    public static void ProcessFreezeAuras(BattleshipGame game)
    {
        foreach (var player in game.GetPlayers())
        {
            var opponent = game.GetOpponent(player.DiscordId);
            if (opponent == null) continue;

            foreach (var ship in player.Board.PlacedShips)
            {
                if (ship.IsDestroyed ||
                    ship.Statuses.Any(status => status is
                        ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) ||
                    !ship.Abilities.Contains("freeze_nearby"))
                    continue;
                var occupied = ship.GetOccupiedCells();

                // Freeze enemy summons
                foreach (var summon in opponent.Summons.Where(s => s.IsAlive && !s.IsGhost).ToList())
                {
                    var summonCells = GetLiveSummonOccupiedCells(summon);
                    foreach (var (sr, sc) in occupied)
                    {
                        if (summonCells.Any(cell =>
                                Math.Abs(sr - cell.row) <= ship.Space &&
                                Math.Abs(sc - cell.col) <= ship.Space))
                        {
                            FreezeSummon(game, opponent, player, summon, summon.Row, summon.Col);
                            break;
                        }
                    }
                }

            }
        }
    }

    /// <summary>
    /// Poison Cone: V-pattern 2 cells forward from Alchi-Barge/Alchi-Iceberg.
    /// Kills ships and summons (including allied) in cone.
    /// </summary>
    public static void ProcessPoisonCones(BattleshipGame game)
    {
        if (game == null) return;
        var state = CreatePoisonResolutionState(game);
        ContinuePoisonResolution(game, state, shouldPause: null);
    }

    private static PoisonResolutionState CreatePoisonResolutionState(BattleshipGame game)
    {
        RefreshPoisonZones(game);
        var state = new PoisonResolutionState();

        foreach (var player in game.GetPlayers())
        {
            foreach (var ship in player.Board.PlacedShips)
            {
                if (ship.IsDestroyed || ship.Statuses.Contains(ShipStatusType.Capture) ||
                    !ship.Abilities.Contains("poison_cone")) continue;

                foreach (var (row, col) in GetPoisonConeCells(ship))
                {
                    state.NormalCells.Add(new NormalPoisonCellWorkItem
                    {
                        BoardOwnerId = player.DiscordId,
                        SourceShipId = ship.Id,
                        Row = row,
                        Col = col,
                    });
                }
            }
        }

        return state;
    }

    /// <summary>
    /// Continue one poison resolution until complete or until the callback requests a pause.
    /// Every cursor is advanced before yielding, so already applied damage is never replayed.
    /// </summary>
    private static bool ContinuePoisonResolution(
        BattleshipGame game,
        PoisonResolutionState state,
        Func<BattleshipGame, bool> shouldPause)
    {
        while (true)
        {
            switch (state.Phase)
            {
                case PoisonResolutionPhase.NormalShipCells:
                {
                    if (state.NormalCellIndex < state.NormalCells.Count)
                    {
                        var item = state.NormalCells[state.NormalCellIndex++];
                        var sourceKey = $"{item.BoardOwnerId}:{item.SourceShipId}";
                        if (state.CurrentNormalSourceKey != sourceKey)
                        {
                            state.CurrentNormalSourceKey = sourceKey;
                            var boardOwner = game.GetPlayer(item.BoardOwnerId);
                            state.CurrentNormalSourceActive = boardOwner?.Board.PlacedShips.Any(ship =>
                                ship.Id == item.SourceShipId &&
                                !ship.IsDestroyed &&
                                !ship.Statuses.Contains(ShipStatusType.Capture) &&
                                ship.Abilities.Contains("poison_cone")) == true;
                        }

                        if (state.CurrentNormalSourceActive)
                            ProcessNormalPoisonCell(game, item);
                        if (shouldPause?.Invoke(game) == true) return false;
                        continue;
                    }

                    InitializeBoardingPoisonSources(game, state);
                    state.Phase = PoisonResolutionPhase.BoardingSources;
                    continue;
                }

                case PoisonResolutionPhase.BoardingSources:
                {
                    if (state.BoardingSourceIndex >= state.BoardingSources.Count)
                    {
                        state.Phase = PoisonResolutionPhase.Complete;
                        continue;
                    }

                    var work = state.BoardingSources[state.BoardingSourceIndex];
                    var boardOwner = game.GetPlayer(work.BoardOwnerId);
                    var sourceOwner = game.GetPlayer(work.SourceOwnerId);
                    var source = sourceOwner?.Summons.FirstOrDefault(summon =>
                        summon.Id == work.SourceSummonId);

                    if (!state.CurrentSourceInitialized)
                    {
                        if (boardOwner == null || sourceOwner == null || source == null ||
                            !source.IsAlive || !BoardingHasAbility(source, "poison_cone") ||
                            !GetLiveSummonOccupiedCells(source).Any(position =>
                                boardOwner.Board.GetCell(position.row, position.col)?.SummonRef == source))
                        {
                            AdvanceBoardingPoisonSource(state);
                            continue;
                        }

                        state.CurrentBoardingConeCells = GetBoardingPoisonConeCells(source);
                        state.CurrentPoisonedShipIds = state.CurrentBoardingConeCells
                            .Select(position => boardOwner.Board.GetCell(position.row, position.col)?.ShipRef)
                            .Where(ship => ship is { IsDestroyed: false })
                            .DistinctBy(ship => ship.Id)
                            .Select(ship => ship.Id)
                            .ToList();
                        state.CurrentPoisonedShipIndex = 0;
                        state.CurrentPoisonedSummonIds.Clear();
                        state.CurrentPoisonedSummonIndex = 0;
                        state.CurrentSummonTargetsInitialized = false;
                        state.CurrentSourceInitialized = true;
                    }

                    if (state.CurrentPoisonedShipIndex < state.CurrentPoisonedShipIds.Count)
                    {
                        var targetId = state.CurrentPoisonedShipIds[state.CurrentPoisonedShipIndex++];
                        var poisonedShip = boardOwner?.Board.PlacedShips.FirstOrDefault(ship =>
                            ship.Id == targetId && !ship.IsDestroyed);
                        if (poisonedShip != null && sourceOwner != null && source != null)
                        {
                            foreach (var deck in poisonedShip.Decks) deck.CurrentHp = 0;
                            RevealShip(boardOwner.Board, poisonedShip, sourceOwner);
                            HandleShipDeath(game, boardOwner, poisonedShip, ShipDestructionCause.Poison);
                            game.AddBoardDetailLog(boardOwner.DiscordId,
                                $"Ядовитый конус {source.SourceShipName ?? "абордажного корабля"} уничтожил {poisonedShip.Name}!");
                        }

                        if (shouldPause?.Invoke(game) == true) return false;
                        continue;
                    }

                    if (!state.CurrentSummonTargetsInitialized)
                    {
                        state.CurrentPoisonedSummonIds = state.CurrentBoardingConeCells
                            .Select(position => boardOwner?.Board.GetCell(position.row, position.col)?.SummonRef)
                            .Where(target => target is { IsAlive: true, IsGhost: false } &&
                                             target.Id != work.SourceSummonId)
                            .DistinctBy(target => target.Id)
                            .Select(target => target.Id)
                            .ToList();
                        state.CurrentPoisonedSummonIndex = 0;
                        state.CurrentSummonTargetsInitialized = true;
                    }

                    if (state.CurrentPoisonedSummonIndex < state.CurrentPoisonedSummonIds.Count)
                    {
                        var targetId = state.CurrentPoisonedSummonIds[state.CurrentPoisonedSummonIndex++];
                        var poisonedSummon = game.GetPlayers()
                            .SelectMany(player => player.Summons)
                            .FirstOrDefault(target => target.Id == targetId && target.IsAlive && !target.IsGhost);
                        var poisonedOwner = poisonedSummon == null
                            ? null
                            : game.GetPlayer(poisonedSummon.OwnerId);
                        if (poisonedSummon != null && poisonedOwner != null && boardOwner != null)
                        {
                            DestroySummonAtCurrentPosition(
                                game, poisonedOwner, boardOwner, poisonedSummon);
                            AddPoisonSummonMastWarning(game, poisonedOwner);
                        }

                        if (shouldPause?.Invoke(game) == true) return false;
                        continue;
                    }

                    AdvanceBoardingPoisonSource(state);
                    continue;
                }

                case PoisonResolutionPhase.Complete:
                    return true;

                default:
                    throw new InvalidOperationException($"Unknown poison resolution phase: {state.Phase}");
            }
        }
    }

    private static void ProcessNormalPoisonCell(
        BattleshipGame game,
        NormalPoisonCellWorkItem item)
    {
        var boardOwner = game.GetPlayer(item.BoardOwnerId);
        var source = boardOwner?.Board.PlacedShips.FirstOrDefault(ship =>
            ship.Id == item.SourceShipId);
        if (boardOwner == null || source == null) return;
        var opponent = game.GetOpponent(boardOwner.DiscordId);

        // Enemy summons physically occupy this player's board.
        if (opponent != null)
        {
            foreach (var summon in opponent.Summons.Where(candidate =>
                         candidate.IsAlive && !candidate.IsGhost &&
                         GetLiveSummonOccupiedCells(candidate).Contains((item.Row, item.Col))).ToList())
            {
                DestroySummonAtCurrentPosition(game, opponent, boardOwner, summon);
                AddPoisonSummonMastWarning(game, opponent);
            }
        }

        // The cone exists only on its source's physical board.
        var allyCell = boardOwner.Board.GetCell(item.Row, item.Col);
        if (allyCell?.ShipRef == null || allyCell.ShipRef.IsDestroyed ||
            allyCell.ShipRef.Id == source.Id) return;
        var allyShip = allyCell.ShipRef;
        foreach (var deck in allyShip.Decks) deck.CurrentHp = 0;
        RevealShip(boardOwner.Board, allyShip, opponent);
        HandleShipDeath(game, boardOwner, allyShip, ShipDestructionCause.Poison);
        game.AddBoardDetailLog(boardOwner.DiscordId,
            $"Ядовитый конус {source.Name} уничтожил союзный {allyShip.Name}!");
    }

    private static void InitializeBoardingPoisonSources(
        BattleshipGame game,
        PoisonResolutionState state)
    {
        if (state.BoardingSourcesInitialized) return;
        foreach (var boardOwner in game.GetPlayers())
        foreach (var sourceOwner in game.GetPlayers())
        foreach (var source in sourceOwner.Summons.Where(summon =>
                     summon.IsAlive &&
                     BoardingHasAbility(summon, "poison_cone") &&
                     GetLiveSummonOccupiedCells(summon).Any(position =>
                         boardOwner.Board.GetCell(position.row, position.col)?.SummonRef == summon)))
        {
            state.BoardingSources.Add(new BoardingPoisonSourceWorkItem
            {
                BoardOwnerId = boardOwner.DiscordId,
                SourceOwnerId = sourceOwner.DiscordId,
                SourceSummonId = source.Id,
            });
        }

        state.BoardingSourcesInitialized = true;
    }

    private static void AdvanceBoardingPoisonSource(PoisonResolutionState state)
    {
        state.BoardingSourceIndex++;
        state.CurrentSourceInitialized = false;
        state.CurrentPoisonedShipIds.Clear();
        state.CurrentPoisonedShipIndex = 0;
        state.CurrentBoardingConeCells.Clear();
        state.CurrentSummonTargetsInitialized = false;
        state.CurrentPoisonedSummonIds.Clear();
        state.CurrentPoisonedSummonIndex = 0;
    }

    public static void RefreshPoisonZones(BattleshipGame game)
    {
        game.PoisonZonesByBoardOwner.Clear();
        foreach (var player in game.GetPlayers())
        {
            var zones = player.Board.PlacedShips
                .Where(s => !s.IsDestroyed && !s.Statuses.Contains(ShipStatusType.Capture) &&
                            s.Abilities.Contains("poison_cone"))
                .SelectMany(ship => GetPoisonConeCells(ship))
                .ToHashSet();
            foreach (var sourceOwner in game.GetPlayers())
            foreach (var source in sourceOwner.Summons.Where(summon =>
                         summon.IsAlive &&
                         BoardingHasAbility(summon, "poison_cone") &&
                         GetLiveSummonOccupiedCells(summon).Any(position =>
                             player.Board.GetCell(position.row, position.col)?.SummonRef == summon)))
                zones.UnionWith(GetBoardingPoisonConeCells(source));
            game.PoisonZonesByBoardOwner[player.DiscordId] = zones;
        }
    }

    private static void AddPoisonSummonMastWarning(BattleshipGame game, BattleshipPlayer summonOwner)
    {
        if (summonOwner == null || !HasLivingMast(summonOwner)) return;
        game.AddLogFor(summonOwner.DiscordId,
            "Призванное существо погибло в ядовитом конусе!");
    }

    public static bool HasLivingMast(BattleshipPlayer player) =>
        player != null && player.Fleet.Any(ship =>
            !ship.IsDestroyed &&
            !ship.Statuses.Any(status => status is
                ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze) &&
            ship.Weapons.Any(weapon =>
                weapon.Type == WeaponType.Mast && IsWeaponOperational(ship, weapon)));

    public static List<(int row, int col)> GetPoisonConeCells(
        Ship ship,
        int? row = null,
        int? col = null,
        Orientation? orientation = null)
    {
        var cells = new List<(int, int)>();
        var resolvedOrientation = orientation ?? ship.Orientation;
        var firstDeck = ship.GetDeckCell(
            ship.Decks.OrderBy(deck => deck.Index).First(),
            row ?? ship.Row,
            col ?? ship.Col,
            resolvedOrientation);
        var (forwardRow, forwardCol) = resolvedOrientation switch
        {
            Orientation.Vertical => (-1, 0),
            Orientation.VerticalReverse => (1, 0),
            Orientation.HorizontalReverse => (0, 1),
            _ => (0, -1),
        };
        var (sideRow, sideCol) = forwardRow != 0 ? (0, 1) : (1, 0);
        for (var depth = 1; depth <= 2; depth++)
        {
            var halfWidth = depth;
            for (var side = -halfWidth; side <= halfWidth; side++)
                cells.Add((firstDeck.row + forwardRow * depth + sideRow * side,
                    firstDeck.col + forwardCol * depth + sideCol * side));
        }

        // Filter valid cells
        return cells.Where(c => c.Item1 >= 0 && c.Item1 < 10 && c.Item2 >= 0 && c.Item2 < 10).ToList();
    }

    private static List<(int row, int col)> GetBoardingPoisonConeCells(Summon summon)
    {
        var sourceCell = GetLiveBoardingDeckCells(summon)
            .OrderBy(value => value.deck.Index)
            .Select(value => (value.row, value.col))
            .FirstOrDefault();
        var (forwardRow, forwardCol) = summon.MoveDirection switch
        {
            Direction.Up => (-1, 0),
            Direction.Left => (0, -1),
            Direction.Right => (0, 1),
            _ => (1, 0),
        };
        var (sideRow, sideCol) = forwardRow != 0 ? (0, 1) : (1, 0);
        var cells = new List<(int row, int col)>();
        for (var depth = 1; depth <= 2; depth++)
        for (var side = -depth; side <= depth; side++)
        {
            var row = sourceCell.row + forwardRow * depth + sideRow * side;
            var col = sourceCell.col + forwardCol * depth + sideCol * side;
            if (row is >= 0 and < 10 && col is >= 0 and < 10)
                cells.Add((row, col));
        }
        return cells;
    }

    private static void AccumulateDeferredReveal(
        BattleshipGame game,
        Summon summon,
        BattleshipPlayer boardOwner,
        int centerRow,
        int centerCol)
    {
        if (summon.Type != SummonType.Scout && !summon.IsBoardingShip) return;
        for (var dr = -summon.RevealRadius; dr <= summon.RevealRadius; dr++)
        for (var dc = -summon.RevealRadius; dc <= summon.RevealRadius; dc++)
        {
            var row = centerRow + dr;
            var col = centerCol + dc;
            if (row is < 0 or >= 10 || col is < 0 or >= 10) continue;
            if (game.PoisonZonesByBoardOwner.TryGetValue(boardOwner.DiscordId, out var zone) &&
                zone.Contains((row, col))) continue;
            if (!summon.ScoutRevealData.Contains((row, col)))
                summon.ScoutRevealData.Add((row, col));
        }
    }

    private static void ResolveDeferredRevealOnDeath(
        BattleshipGame game,
        Summon summon,
        bool suppress)
    {
        if (suppress)
        {
            summon.ScoutRevealData.Clear();
            return;
        }
        TransmitScoutReveal(game, summon);
    }

    /// <summary>
    /// Transmit deferred Scout/boarding reveal data when the unit dies or leaves the map.
    /// Freeze and direct Incendiary variants explicitly clear the payload first.
    /// </summary>
    private static void TransmitScoutReveal(BattleshipGame game, Summon summon)
    {
        if (summon.Type != SummonType.Scout && !summon.IsBoardingShip ||
            summon.ScoutRevealData.Count == 0) return;
        var summonOwner = game.GetPlayer(summon.OwnerId);
        var opponent = game.GetOpponent(summon.OwnerId);
        if (opponent == null) return;
        foreach (var (sr, sc) in summon.ScoutRevealData)
        {
            var revealCell = opponent.Board.GetCell(sr, sc);
            if (revealCell != null) RevealCell(opponent.Board, revealCell, summonOwner);
        }
        summon.ScoutRevealData.Clear();
    }

    /// <summary>
    /// Auto-dodge for Light Wood Triple: if shot targets bow or stern, try to move ship.
    /// Returns a ShotResult if dodged, null if no dodge.
    /// </summary>
    private static ShotResult ProcessAutoDodge(BattleshipGame game, BattleshipPlayer shipOwner, Ship ship, int row, int col, int deckIndex)
    {
        // Only dodge bow (first deck) or stern (last deck) hits
        if (deckIndex != 0 && deckIndex != ship.Decks.Count - 1)
            return null;

        // Determine dodge direction (opposite of hit direction)
        var axisSign = ship.Orientation is Orientation.HorizontalReverse or Orientation.VerticalReverse
            ? -1
            : 1;
        var dodgeDir = (deckIndex == 0 ? 1 : -1) * axisSign;

        if (TryMoveShip(shipOwner, ship, dodgeDir))
        {
            game.AddBoardDetailLog(shipOwner.DiscordId,
                $"{ship.Name} увернулся от выстрела! ({(char)('A' + col)}{row + 1})");
            MarkManeuverDodgeMiss(shipOwner.Board, ship, row, col);

            // Check if ship dodged into hazards (burning cells, poison cones) (Bug #10)
            foreach (var (nr, nc) in ship.GetOccupiedCells())
            {
                var newCell = shipOwner.Board.GetCell(nr, nc);
                if (newCell is { IsBurning: true } && !ship.Statuses.Contains(ShipStatusType.BurnResist))
                {
                    foreach (var d in ship.Decks) d.CurrentHp = 0;
                    ship.Statuses.Add(ShipStatusType.Burn);
                    RevealShip(shipOwner.Board, ship, null);
                    HandleShipDeath(game, shipOwner, ship, ShipDestructionCause.GreekFire);
                    game.AddBoardDetailLog(shipOwner.DiscordId,
                        $"{ship.Name} заплыл в огонь при уклонении!");
                    break;
                }
            }
            if (!ship.IsDestroyed && game.PoisonZonesByBoardOwner.TryGetValue(shipOwner.DiscordId, out var poisonZone))
            {
                foreach (var (nr, nc) in ship.GetOccupiedCells())
                {
                    if (poisonZone.Contains((nr, nc)))
                    {
                        foreach (var d in ship.Decks) d.CurrentHp = 0;
                        RevealShip(shipOwner.Board, ship, null);
                        HandleShipDeath(game, shipOwner, ship, ShipDestructionCause.Poison);
                        game.AddBoardDetailLog(shipOwner.DiscordId,
                            $"{ship.Name} заплыл в ядовитый конус при уклонении!");
                        break;
                    }
                }
            }

            return new ShotResult
            {
                Miss = true, Dodged = true, Row = row, Col = col, TurnContinues = false,
                Message = $"{ship.Name} увернулся!", AffectedShipName = ship.Name
            };
        }

        return null; // couldn't dodge
    }

    /// <summary>
    /// Try to move a ship by `delta` cells along its orientation axis.
    /// Returns true if successful.
    /// </summary>
    private static bool TryMoveShip(BattleshipPlayer owner, Ship ship, int delta)
    {
        int newRow = ship.Row, newCol = ship.Col;
        if (ship.Orientation is Orientation.Horizontal or Orientation.HorizontalReverse)
            newCol += delta;
        else
            newRow += delta;

        var newCells = ship.GetOccupiedCells(newRow, newCol, ship.Orientation);
        if (newCells.Any(cell => cell.row < 0 || cell.row >= 10 || cell.col < 0 || cell.col >= 10))
            return false;
        if (ship.Range == RangeClass.Mid && newCells.Any(cell => cell.row >= 8))
            return false;

        // Check no other ships in new position and Space distance to allies
        var currentCells = ship.GetOccupiedCells().ToHashSet();
        foreach (var (r, c) in newCells)
        {
            if (currentCells.Contains((r, c))) continue;
            var cell = owner.Board.GetCell(r, c);
            if (cell?.ShipRef != null && cell.ShipRef.Id != ship.Id)
                return false;
        }

        // Check Space gap to allied ships
        foreach (var allyShip in owner.Board.PlacedShips)
        {
            if (allyShip.Id == ship.Id || allyShip.IsDestroyed) continue;
            var spacing = Math.Max(ship.Space, allyShip.Space);
            foreach (var (ar, ac) in allyShip.GetOccupiedCells())
            {
                foreach (var (nr, nc) in newCells)
                {
                    if (Math.Abs(ar - nr) <= spacing && Math.Abs(ac - nc) <= spacing)
                        return false;
                }
            }
        }

        // Move ship
        foreach (var (r, c) in currentCells)
        {
            var oldCell = owner.Board.Grid[r, c];
            if (oldCell.ShipRef == ship)
                oldCell.ShipRef = null;
            if (!newCells.Contains((r, c)) && !ship.ManeuverStaleHitCells.Contains((r, c)))
                ship.ManeuverStaleHitCells.Add((r, c));
        }

        ship.Row = newRow;
        ship.Col = newCol;
        ship.HasHiddenMovement = true;

        foreach (var (r, c) in ship.GetOccupiedCells())
            owner.Board.Grid[r, c].ShipRef = ship;

        return true;
    }

    /// <summary>
    /// Legal endpoints for an axial manual maneuver.
    /// </summary>
    public static List<ManualMoveOption> GetManualMoveOptions(BattleshipPlayer player, Ship ship)
    {
        var directions = ship.Orientation is Orientation.Horizontal or Orientation.HorizontalReverse
            ? new[] { Direction.Left, Direction.Right }
            : new[] { Direction.Up, Direction.Down };
        var options = new List<ManualMoveOption>();
        foreach (var direction in directions)
        foreach (var distance in new[] { 1, 2 })
        {
            var (newRow, newCol) = ManualMoveAnchor(ship, direction, distance);
            if (!CanManualMoveShip(player, ship, newRow, newCol)) continue;
            options.Add(new ManualMoveOption
            {
                Direction = direction,
                Distance = distance,
                Row = newRow,
                Col = newCol,
            });
        }
        return options;
    }

    private static (int row, int col) ManualMoveAnchor(Ship ship, Direction direction, int distance)
    {
        var (dr, dc) = direction switch
        {
            Direction.Up => (-1, 0),
            Direction.Down => (1, 0),
            Direction.Left => (0, -1),
            Direction.Right => (0, 1),
            _ => (0, 0)
        };
        return (ship.Row + dr * distance, ship.Col + dc * distance);
    }

    private static bool CanManualMoveShip(BattleshipPlayer player, Ship ship, int newRow, int newCol)
    {
        if (player == null || ship == null || ship.IsDestroyed ||
            ship.Statuses.Any(status => status is
                ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze))
            return false;

        var newCells = ship.GetOccupiedCells(newRow, newCol, ship.Orientation);
        if (newCells.Any(cell => cell.row < 0 || cell.row >= 10 || cell.col < 0 || cell.col >= 10))
            return false;
        if (ship.Range == RangeClass.Mid && newCells.Any(cell => cell.row >= 8))
            return false;

        var canRamAllies = ship.Abilities.Contains("ramming_maneuver");
        var canMergeOnCollision = ship.Abilities.Contains("merge_maneuver") ||
                                  ship.Abilities.Contains("merge_maneuver_after_hit");
        var overlaps = newCells
            .Select(position =>
                (position.row, position.col,
                    ship: player.Board.GetCell(position.row, position.col)?.ShipRef))
            .Where(value => value.ship != null && value.ship.Id != ship.Id)
            .ToList();

        Ship mergeTarget = null;
        if (overlaps.Count > 0)
        {
            if (canRamAllies && !canMergeOnCollision)
                return true;
            if (!canMergeOnCollision || overlaps.Select(value => value.ship.Id).Distinct().Count() != 1)
                return false;
            mergeTarget = overlaps[0].ship;
            var overlappedDecks = overlaps
                .Select(value => GetDeckIndexAtCell(mergeTarget, value.row, value.col))
                .Where(deckIndex => deckIndex >= 0)
                .Distinct()
                .ToList();
            if (overlappedDecks.Count != 1 ||
                mergeTarget.IsDestroyed ||
                mergeTarget.Statuses.Any(status => status is
                    ShipStatusType.Capture or ShipStatusType.Devastated or ShipStatusType.Freeze))
                return false;
        }

        // Ramming movement may enter allied Space. The Ver.2 combination still validates an
        // actual collision as exactly one mergeable deck before taking this fast path.
        if (canRamAllies) return true;

        foreach (var ally in player.Board.PlacedShips)
        {
            if (ally.Id == ship.Id || ally.IsDestroyed) continue;
            if (mergeTarget != null && ally.Id == mergeTarget.Id) continue;
            var spacing = Math.Max(ship.Space, ally.Space);
            foreach (var (allyRow, allyCol) in ally.GetOccupiedCells())
            foreach (var (row, col) in newCells)
            {
                if (Math.Abs(allyRow - row) <= spacing && Math.Abs(allyCol - col) <= spacing)
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Apply a validated maneuver. The Alliance ramming variant removes overlapped allied decks;
    /// the merging variant replaces one eligible allied overlap with a persistent composite hull.
    /// </summary>
    public static bool ManualMoveShip(
        BattleshipGame game,
        BattleshipPlayer player,
        Ship ship,
        Direction direction,
        int distance)
    {
        if (distance < 1 || distance > 2) return false;
        if (ship.Orientation is Orientation.Horizontal or Orientation.HorizontalReverse &&
            direction is not (Direction.Left or Direction.Right))
            return false;
        if (ship.Orientation is Orientation.Vertical or Orientation.VerticalReverse &&
            direction is not (Direction.Up or Direction.Down))
            return false;

        var (newRow, newCol) = ManualMoveAnchor(ship, direction, distance);
        if (!CanManualMoveShip(player, ship, newRow, newCol)) return false;

        var oldCells = ship.GetOccupiedCells();
        var newCells = ship.GetOccupiedCells(newRow, newCol, ship.Orientation);
        var collisions = new List<(Ship ship, Deck deck, int row, int col)>();
        foreach (var (row, col) in newCells)
        {
            var target = player.Board.GetCell(row, col)?.ShipRef;
            if (target == null || target.Id == ship.Id) continue;
            var deckIndex = GetDeckIndexAtCell(target, row, col);
            if (deckIndex >= 0)
            {
                var deck = target.Decks[deckIndex];
                if (collisions.All(x => x.ship.Id != target.Id || x.deck.Index != deck.Index))
                    collisions.Add((target, deck, row, col));
            }
        }

        if ((ship.Abilities.Contains("merge_maneuver") ||
             ship.Abilities.Contains("merge_maneuver_after_hit")) &&
            collisions.Count > 0)
        {
            return BattleshipCompositeShipFactory.TryMerge(
                player, ship, newRow, newCol, out _, out _);
        }

        foreach (var (r, c) in oldCells)
        {
            var oldCell = player.Board.Grid[r, c];
            if (oldCell.ShipRef == ship)
                oldCell.ShipRef = null;
            oldCell.IsHit = false;
            if (!newCells.Contains((r, c)) && !ship.ManeuverStaleHitCells.Contains((r, c)))
                ship.ManeuverStaleHitCells.Add((r, c));
        }

        ship.Row = newRow;
        ship.Col = newCol;
        ship.HasHiddenMovement = true;

        foreach (var (r, c) in ship.GetOccupiedCells())
            player.Board.Grid[r, c].ShipRef = ship;

        foreach (var (target, deck, row, col) in collisions)
        {
            var originalDeckIndex = deck.Index;
            target.MatryoshkaReplacementSuppressionReasons |=
                MatryoshkaReplacementSuppression.StructuralDeckRemoval;
            target.Weapons.RemoveAll(weapon => weapon.DeckIndex == originalDeckIndex);
            target.Decks.Remove(deck);
            game.AddLogFor(player.DiscordId,
                $"{ship.Name} уничтожил палубу союзного {target.Name} во время манёвра!");
            if (target.Decks.Count > 0 && !target.IsDestroyed) continue;

            ClearLatestManeuverDodge(player.Board, target);
            foreach (var cell in player.Board.Grid.Cast<Cell>())
            {
                if (cell.ShipRef == target) cell.ShipRef = null;
            }
            player.Board.PlacedShips.Remove(target);
            player.Fleet.Remove(target);
        }

        return true;
    }

    /// <summary>
    /// Set CursedBoat direction after collision (player's choice of 4 directions).
    /// </summary>
    public static bool SetCursedBoatDirection(BattleshipPlayer player, string summonId, Direction direction)
    {
        var summon = player.Summons.FirstOrDefault(s => s.Id == summonId && s.IsAlive && s.WaitingForDirectionChoice);
        if (summon == null) return false;
        var (nextRow, nextCol) = GetNextPosition(summon.Row, summon.Col, direction);
        if (!CanFitSummonHullInBounds(summon, nextRow, nextCol, direction))
            return false;

        summon.MoveDirection = direction;
        summon.WaitingForDirectionChoice = false;
        return true;
    }

    /// <summary>
    /// Generate Mast warnings when opponent deploys summon. Includes spawn coordinates.
    /// </summary>
    public static string GenerateMastWarning(BattleshipPlayer player, SummonType summonType, int row = -1, int col = -1)
    {
        if (!HasLivingMast(player)) return null;

        var coord = row >= 0 && col >= 0 ? $" ({(char)('A' + col)}{row + 1})" : "";

        return summonType switch
        {
            SummonType.Ram => $"[Мачта] Это таран!{coord}",
            SummonType.PirateBoat => $"[Мачта] На нас надвигаются пираты!{coord}",
            SummonType.Scout => $"[Мачта] Вражеский разведчик на горизонте!{coord}",
            SummonType.Brander => $"[Мачта] Брандер приближается!{coord}",
            SummonType.CursedBoat => $"[Мачта] Проклятый корабль на горизонте!{coord}",
            _ => null
        };
    }

}
