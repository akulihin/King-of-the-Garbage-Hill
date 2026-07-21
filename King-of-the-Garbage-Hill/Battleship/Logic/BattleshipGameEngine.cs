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
        GreekFire,
        Explosion,
        Collision,
        Capture,
        Devastated,
        Poison,
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
                            WeaponType.Incendiary or WeaponType.GreekFire) &&
                        (!capturedEnemyIds.Contains(x.ship.Id) || x.weapon.Type == WeaponType.Ballista) &&
                        (x.weapon.Type != WeaponType.Ballista || x.ship.Range is RangeClass.Mid or RangeClass.Close) &&
                        (type == null || x.weapon.Type == type) && x.weapon.HasAmmo && IsWeaponOperational(x.ship, x.weapon));
    }

    public static bool HasAnyLegalShot(BattleshipGame game, BattleshipPlayer player)
    {
        if (player.Board.PlacedShips.Any(s =>
                !s.IsDestroyed && s.Statuses.Contains(ShipStatusType.Capture)))
            return HasUsableBallista(game, player);
        if (HasUsableBallista(game, player)) return true;
        return GetUsableWeapons(game, player).Any(x =>
            (x.weapon.Type is WeaponType.Tetracatapult or WeaponType.Incendiary) &&
            x.weapon.AimSpeed <= player.RevealedCellCount);
    }

    public static bool IsWeaponOperational(Ship ship, Weapon weapon)
    {
        if (ship == null || weapon == null || ship.IsDestroyed || weapon.ShipId != ship.Id) return false;
        if (weapon.DeckIndex < 0 || weapon.DeckIndex >= ship.Decks.Count) return false;
        var deck = ship.Decks[weapon.DeckIndex];
        return !deck.IsDestroyed && !deck.ModuleDestroyed;
    }

    private static IEnumerable<Ship> GetControlledShips(BattleshipGame game, BattleshipPlayer player)
    {
        foreach (var ship in player.Board.PlacedShips.Where(s => !s.Statuses.Contains(ShipStatusType.Capture)))
            yield return ship;

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
            if (shooter.SelectedShotType != ShotType.Ballista)
            {
                return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                    Message = "Захваченный корабль нужно уничтожить Баллистой." };
            }
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
        if (shooter.SelectedShotType == ShotType.GreekFire)
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

        // Consume ammo from selected weapon
        shooter.SelectedWeapon?.UseAmmo();

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
            if (cell.WasScratched && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                return ProcessShipHit(game, shooter, opponent, cell, row, col);
            }
            if (shooter.SelectedShotType == ShotType.Incendiary && cell.IsHit && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
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
            // Allied summon hit — turn ends, no penalty
            if (cell.SummonRef.OwnerId == shooter.DiscordId)
            {
                cell.IsHit = true;
                var alliedSummon = cell.SummonRef;
                TransmitScoutReveal(game, alliedSummon);
                alliedSummon.IsAlive = false;
                cell.SummonRef = null;
                game.AddLog($"{shooter.Username} попал в своё призванное существо ({(char)('A' + col)}{row + 1})");
                return new ShotResult
                {
                    Hit = true, Row = row, Col = col, TurnContinues = false,
                    Destroyed = true, Message = "Попадание в своё призванное существо! Ход прерван."
                };
            }
            return ProcessSummonHit(game, shooter, cell, row, col);
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
            var deadSummon = cell.SummonRef;
            TransmitScoutReveal(game, deadSummon);
            deadSummon.IsAlive = false;
            cell.SummonRef = null;
            // The four normal summons are a per-match use limit, not reusable active slots.
            // Brander is tracked separately and neither counter is refunded on death (ТЗ #10).
            game.AddLog($"Греческий огонь сжёг призванное существо! ({(char)('A' + col)}{row + 1})");
            // ТЗ #13/#10.4: fire detonates the Brander — on the shooter's own board
            if (deadSummon.Type == SummonType.Brander)
            {
                DetonateBrander(game, shooter, deadSummon, row, col);
            }
            return new ShotResult { Hit = true, Destroyed = true, Row = row, Col = col, TurnContinues = false,
                Message = "Греческий огонь уничтожил призыв без штрафа!" };
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
                game.AddLog($"Греческий огонь сжёг {cell.ShipRef.Name}!");
                return new ShotResult { Hit = true, Destroyed = true, ShipSunk = true, Burned = true,
                    Row = row, Col = col, TurnContinues = false,
                    Message = $"Греческий огонь сжёг {cell.ShipRef.Name}!", AffectedShipName = cell.ShipRef.Name };
            }
            game.AddLog($"{cell.ShipRef.Name} устоял против греческого огня (огнеупорность)!");
            return new ShotResult { Row = row, Col = col, TurnContinues = false,
                Message = "Корабль устоял — огнеупорность!" };
        }

        // Empty cell — permanent fire (area denial against summons)
        cell.IsMiss = true;
        game.AddLog($"Греческий огонь горит на ({(char)('A' + col)}{row + 1})!");
        return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
            Message = "Греческий огонь — клетка горит!" };
    }

    /// <summary>
    /// Process shot against a captured ship on shooter's own board.
    /// </summary>
    private static ShotResult ProcessCapturedShipShot(BattleshipGame game, BattleshipPlayer shooter, int row, int col)
    {
        game.ShotCount++;
        var cell = shooter.Board.GetCell(row, col);
        if (cell?.ShipRef == null || !cell.ShipRef.Statuses.Contains(ShipStatusType.Capture))
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false, Message = "Мимо!" };

        var ship = cell.ShipRef;
        var deckIndex = GetDeckIndexAtCell(ship, row, col);
        if (deckIndex < 0)
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false, Message = "Мимо!" };

        var deck = ship.Decks[deckIndex];
        var damage = GetDamage(shooter);
        if (deck.IsDestroyed)
        {
            cell.IsMiss = true;
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                Message = "Эта палуба уже уничтожена." };
        }

        shooter.SelectedWeapon?.UseAmmo();
        deck.CurrentHp -= damage;
        if (deck.CurrentHp < 0) deck.CurrentHp = 0;

        if (ship.IsDestroyed)
        {
            // The capturer owns the reconnaissance gained when the originally enemy ship
            // finally dies on its physical board.
            var capturer = game.GetOpponent(shooter.DiscordId);
            RevealShip(shooter.Board, ship, capturer);
            ship.Statuses.Remove(ShipStatusType.Capture);
            game.AddLog($"{shooter.Username} уничтожил захваченный {ship.Name}!");
            HandleShipDeath(game, shooter, ship, ShipDestructionCause.Capture);
            return new ShotResult { Hit = true, Destroyed = true, ShipSunk = true, Row = row, Col = col, TurnContinues = false,
                Message = $"Захваченный {ship.Name} уничтожен!", AffectedShipName = ship.Name };
        }

        if (deck.IsDestroyed)
        {
            game.AddLog($"{shooter.Username} повредил палубу захваченного {ship.Name}!");
            return new ShotResult { Hit = true, Destroyed = true, Row = row, Col = col, TurnContinues = false,
                Message = $"Палуба захваченного {ship.Name} уничтожена!", AffectedShipName = ship.Name };
        }

        game.AddLog($"{shooter.Username} поцарапал броню захваченного {ship.Name}.");
        return new ShotResult { Hit = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
            Message = "Поцарапал броню захваченного корабля!", AffectedShipName = ship.Name };
    }

    /// <summary>
    /// Shoot own board to kill an enemy summon. Turn is always interrupted + penalty for rows 0-2.
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

        game.ShotCount++;

        var summon = cell.SummonRef;
        TransmitScoutReveal(game, summon);
        summon.IsAlive = false;
        cell.SummonRef = null;

        // Normal summon uses are a per-match cap and are not refunded on death (ТЗ #10).

        game.AddLog($"{shooter.Username} уничтожил вражеский призыв на своём поле ({(char)('A' + col)}{row + 1})");

        // ТЗ #10.4: enemy Brander shot down on own half explodes and damages the shooter's own ships
        if (summon.Type == SummonType.Brander)
        {
            cell.IsHit = true;
            DetonateBrander(game, shooter, summon, row, col);
        }

        // Penalty for killing summon in rear rows (0-2)
        var penalty = false;
        if (row <= 2 && game.ShotCount - summon.SpawnedAtShot > 1)
        {
            shooter.HasPenalty = true;
            penalty = true;
            game.AddLog($"{shooter.Username} получает штраф за уничтожение призыва в тылу!");
        }

        return new ShotResult
        {
            Hit = true, Row = row, Col = col, TurnContinues = false,
            Destroyed = true, Message = penalty
                ? "Вражеский призыв уничтожен! Штраф: пропуск хода."
                : "Вражеский призыв уничтожен!"
        };
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
        shooter.SelectedWeapon?.UseAmmo();

        var aggregate = new ShotResult
        {
            Row = topRow, Col = topCol,
            TurnContinues = false,
        };

        var anyHit = false;
        var anyDestroyed = false;
        var anySunk = false;

        // Process 2x2 area
        for (var dr = 0; dr < 2; dr++)
        for (var dc = 0; dc < 2; dc++)
        {
            var r = topRow + dr;
            var c = topCol + dc;
            var cell = opponent.Board.GetCell(r, c);
            if (cell == null) continue;

            // Allow re-targeting scratched cells (armor survived)
            if (cell.IsHit && cell.WasScratched && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
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
                cell.IsHit = true;
                var summon = cell.SummonRef;
                TransmitScoutReveal(game, summon);
                summon.IsAlive = false;
                cell.SummonRef = null;
                anyHit = true;
                // ТЗ #13: a shot detonates the Brander
                if (summon.Type == SummonType.Brander)
                {
                    DetonateBrander(game, opponent, summon, r, c, shooter);
                }
                // Summon kill: never resets turn (Bug #6)
                // Penalty check for rows 0-2 (Bug #7)
                if (r <= 2 && game.ShotCount - summon.SpawnedAtShot > 1)
                {
                    shooter.HasPenalty = true;
                    game.AddLog($"{shooter.Username} получает штраф за уничтожение призыва картечью в тылу!");
                }
                continue;
            }

            // Ship hit with buckshot damage (1)
            var ship = cell.ShipRef;

            var deckIndex = GetDeckIndexAtCell(ship, r, c);
            if (deckIndex >= 0 && deckIndex < ship.Decks.Count)
            {
                // Check auto_dodge_bow_stern (Light Wood Triple) — dodges all shots including buckshot
                if (ship.Abilities.Contains("auto_dodge_bow_stern"))
                {
                    var dodgeResult = ProcessAutoDodge(game, opponent, ship, r, c, deckIndex);
                    if (dodgeResult != null)
                    {
                        cell.IsHit = false;
                        cell.WasShipHit = false;
                        continue;
                    }
                }

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
                    cell.WasScratched = false; // No longer scratched — deck is destroyed (ТЗ #9)
                    // ТЗ #20: shooter's mast spots the Maneuvering Double (same rule as ProcessShipHit)
                    if (wasAlive && ship.Abilities.Contains("manual_move_after_hit") &&
                        shooter.Board.PlacedShips.Any(s => !s.IsDestroyed && s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed)))
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
                    RevealShip(opponent.Board, ship, shooter);
                    HandleShipDeath(game, opponent, ship);
                    game.AddLog($"Картечь потопила {ship.Name}!");
                }
            }
        }

        // Only ship deck kills/sinks reset the turn, not summon kills (Bug #6)
        if (anySunk || anyDestroyed) aggregate.TurnContinues = true;

        aggregate.Hit = anyHit;
        aggregate.Destroyed = anyDestroyed;
        aggregate.ShipSunk = anySunk;
        aggregate.Miss = !anyHit;
        aggregate.Message = anyHit ? "Картечь поразила цель!" : "Картечь — мимо!";

        game.AddLog($"{shooter.Username} выстрелил картечью ({(char)('A' + topCol)}{topRow + 1})");
        return aggregate;
    }

    private static ShotResult ProcessSummonHit(BattleshipGame game, BattleshipPlayer shooter, Cell cell, int row, int col)
    {
        var summon = cell.SummonRef;
        cell.IsHit = true;
        summon.IsAlive = false;
        cell.SummonRef = null;

        TransmitScoutReveal(game, summon);

        game.AddLog($"{shooter.Username} уничтожил призванное существо ({(char)('A' + col)}{row + 1})");
        if (summon.Type == SummonType.Brander)
            DetonateBrander(game, game.GetOpponent(summon.OwnerId), summon, row, col, shooter);

        // Summon kill penalty: rows 0-2 = penalty, unless just spawned
        var turnContinues = false;
        var penalty = false;
        if (row <= 2)
        {
            // Check if summon was just spawned (within 1 shot)
            if (game.ShotCount - summon.SpawnedAtShot > 1)
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
            Destroyed = true, Message = penalty
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

        // Check auto_dodge_bow_stern (Light Wood Triple)
        if (ship.Abilities.Contains("auto_dodge_bow_stern"))
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
            var nimbleMsg = Random.Shared.Next(2) == 0
                ? "Юркая единичка! Опять увернулась!"
                : "Ну и юркая же она! Камней бы ей на голову!";
            game.AddLog($"{nimbleMsg} ({(char)('A' + col)}{row + 1})");
            return new ShotResult { Miss = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
                Message = nimbleMsg, AffectedShipName = ship.Name };
        }

        // Incendiary: kills the entire ship on ANY hit (ТЗ #18: no cell «Горит» — fire only from Greek Fire)
        if (shooter.SelectedShotType == ShotType.Incendiary)
        {
            if (!ship.Statuses.Contains(ShipStatusType.BurnResist))
            {
                KillShipByFire(game, opponent, ship, shooter, applyBurnStatus: true);
                HandleShipDeath(game, opponent, ship, ShipDestructionCause.Incendiary);
                game.AddLog($"Зажигательный снаряд сжёг {ship.Name}!");
                return new ShotResult
                {
                    Hit = true, Destroyed = true, Row = row, Col = col,
                    TurnContinues = true, ShipSunk = true, Burned = true,
                    Message = $"{ship.Name} сгорел!", AffectedShipName = ship.Name
                };
            }
            // BurnResist: incendiary deals 0 damage — dark-green mark (ТЗ #4)
            cell.BurnResistMarked = true;
            game.AddLog($"Зажигательный снаряд не смог поджечь {ship.Name} (огнеупорность)!");
            return new ShotResult
            {
                Hit = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
                Message = "Огнеупорность! Снаряд бессилен!", AffectedShipName = ship.Name
            };
        }

        // Calculate damage
        var damage = GetDamage(shooter);

        // White Stone always stuns on a real deck hit and destroys that deck's module,
        // even when 8 damage is not enough to break the armor.
        if (shooter.SelectedShotType == ShotType.WhiteStone)
        {
            opponent.StunShotExpiry = game.ShotCount + 1;
            if (deck.Module != null)
            {
                deck.ModuleDestroyed = true;
                game.AddLog($"Белый камень разрушил модуль {deck.Module} на {ship.Name}!");
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
            game.AddLog($"{ship.Name} взорвался от попадания!");
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
                game.AddLog($"{shooter.Username} потопил {ship.Name}! ({(char)('A' + col)}{row + 1})");
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
            game.AddLog($"{shooter.Username} уничтожил палубу {ship.Name}! ({(char)('A' + col)}{row + 1})");

            // ТЗ #20: the SHOOTER's mast spots the Maneuvering Double preparing to row away —
            // personal message, gated on the shooter's own mast
            if (ship.Abilities.Contains("manual_move_after_hit"))
            {
                var shooterHasMast = shooter.Board.PlacedShips.Any(s =>
                    !s.IsDestroyed && s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed));
                if (shooterHasMast)
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
        game.AddLog($"{shooter.Username} поцарапал броню {ship.Name} ({(char)('A' + col)}{row + 1})");
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
            ShotType.Incendiary => 0,      // burn mechanic handles kill
            _ => 2                         // standard ballista
        };
        return baseDamage;
    }

    private static int GetDeckIndexAtCell(Ship ship, int row, int col)
    {
        var cells = ship.GetOccupiedCells();
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].row == row && cells[i].col == col)
                return i;
        }
        return -1;
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

        // First: reveal all cells of the destroyed ship itself (even if not directly hit)
        foreach (var (r, c) in occupied)
        {
            var shipCell = board.GetCell(r, c);
            if (shipCell != null)
            {
                if (!shipCell.IsRevealed && shooter != null)
                    IncrementRevealedCount(shooter);
                shipCell.IsRevealed = true;
                shipCell.IsHit = true; // mark all ship cells as hit for visual display
                shipCell.IsMiss = false;
                shipCell.WasShipHit = true;
                shipCell.WasScratched = false;
                shipCell.WasDodge = false;
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
        if (cell.ShipRef == null && !cell.IsBurning)
            cell.IsMiss = true;
    }

    private static void ReconcileManeuverHistory(Board board, Ship ship, BattleshipPlayer beneficiary)
    {
        if (!ship.IsDestroyed || ship.ManeuverStaleHitCells.Count == 0) return;
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
            cell.WasDodge = false;
        }
        ship.ManeuverStaleHitCells.Clear();
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
        var passiveDisabled = ship.Statuses.Contains(ShipStatusType.Capture);
        var suppressDeathSummon = passiveDisabled || cause is ShipDestructionCause.Incendiary or ShipDestructionCause.GreekFire
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
                    SourceShipName = ship.Name
                });
                game.AddLog($"Пиратская лодка готова к выпуску после гибели {ship.Name}!");
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
                    SourceShipName = ship.Name
                });
                game.AddLog($"Проклятый корабль готов к выпуску после гибели {ship.Name}!");
            }
        }

        // Explode on death (Incendiary Barge still explodes on full death too)
        if (!passiveDisabled && ship.Abilities.Contains("explode_on_hit") && cause != ShipDestructionCause.Capture)
        {
            var deathAttacker = game.GetOpponent(owner.DiscordId);
            ExplodeShip(game, owner, ship, deathAttacker);
        }
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
    /// Unified explosion zone (ТЗ #4/#5/#13): every cell within Chebyshev radius of the center
    /// cells — empty water → «Промах»; ship with a deck in radius → the WHOLE ship is killed
    /// (BurnResist ships are marked dark-green instead, no damage); summons die (scouts transmit
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
                        game.AddLog($"{target.Name} сгорел от взрыва! ({sourceName})");
                        HandleShipDeath(game, boardOwner, target, ShipDestructionCause.Explosion); // may chain-explode
                    }
                    else
                    {
                        cell.BurnResistMarked = true; // dark-green mark (ТЗ #4)
                        if (resistLogged.Add(target.Id))
                            game.AddLog($"{target.Name} устоял против взрыва (огнеупорность)!");
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
            if (cell == null) continue;
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

        TransmitScoutReveal(game, explodedSummon);

        explodedSummon.IsAlive = false;
        cell.SummonRef = null;
        game.AddLog($"Призванное существо сгорело от взрыва! ({sourceName})");
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
        var cell = boardOwner.Board.GetCell(row, col);
        if (cell?.SummonRef == brander) cell.SummonRef = null;
        ExplodeArea(game, boardOwner, new List<(int row, int col)> { (row, col) }, 1, "Брандер", attacker);
        game.AddLog($"Брандер взорвался! ({(char)('A' + col)}{row + 1})");
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
            .All(s => s.IsDestroyed);
        var p2AllDestroyed = p2.Board.PlacedShips.Where(s => !s.Statuses.Contains(ShipStatusType.Capture))
            .All(s => s.IsDestroyed);

        if (p1AllDestroyed) return (true, p2.DiscordId);
        if (p2AllDestroyed) return (true, p1.DiscordId);
        return (false, null);
    }

    private static bool HasDamageCapability(BattleshipGame game, BattleshipPlayer player)
    {
        if (HasAnyLegalShot(game, player)) return true;
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

        var boardingPlayers = game.GetPlayers()
            .Where(player => !HasLivingMidShip(player))
            .ToHashSet();

        // Only the side whose Mid line is gone deploys Close ships onto the enemy board.
        // If an effect removes both Mid lines in one resolution, both sides qualify.
        foreach (var player in boardingPlayers)
        {
            foreach (var ship in player.Board.PlacedShips)
            {
                if (ship.IsDestroyed || ship.IsSummon || ship.Statuses.Contains(ShipStatusType.Capture)) continue;
                if (ship.Range is not (RangeClass.Close or RangeClass.CloseMelee)) continue;
                ship.IsSummon = true;
                player.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.Ram,
                    Speed = ship.Speed,
                    CollisionDamage = 4,
                    RevealRadius = ship.Space,
                    IsBoarding = true,
                    SourceShipName = ship.Name
                });
                game.AddLog($"{ship.Name} готов к абордажу! Разместите на первой строчке вражеского поля.");
            }
        }

        // Triple crew: one visible spawn per player, from one surviving eligible Triple.
        foreach (var p in game.GetPlayers())
        {
            var ship = p.Board.PlacedShips.FirstOrDefault(s =>
                !s.IsDestroyed && s.DefinitionId == "triple" && s.Abilities.Contains("spawn_pirate_boat"));
            if (ship != null)
            {
                var opponent = game.GetOpponent(p.DiscordId);
                var availableColumn = Enumerable.Range(0, 10)
                    .OrderBy(c => Math.Abs(c - ship.Col))
                    .Cast<int?>()
                    .FirstOrDefault(c => opponent?.Board.GetCell(0, c.Value)?.SummonRef == null);
                if (availableColumn == null) continue;
                var pirate = new Summon
                {
                    Type = SummonType.PirateBoat,
                    Row = 0, Col = availableColumn.Value,
                    Speed = 1, OwnerId = p.DiscordId,
                    SpawnedAtShot = game.ShotCount
                };
                p.Summons.Add(pirate);
                RegisterSummonOnTargetBoard(game, p, pirate);
                game.AddLog("Экипаж Тройки выпустил Пиратскую лодку!");
            }
        }

        // Triple extra_ammo upgrade: +2 white stones to Tetracatapult (both players)
        foreach (var p in game.GetPlayers())
        {
            foreach (var ship in p.Board.PlacedShips)
            {
                if (!ship.IsDestroyed && ship.Abilities.Contains("extra_ammo_boarding"))
                {
                    var tetraWeapon = ship.Weapons.Find(w => w.Type == WeaponType.Tetracatapult);
                    if (tetraWeapon != null && IsWeaponOperational(ship, tetraWeapon))
                    {
                        tetraWeapon.Ammo += 2;
                        game.AddLog($"Доп. снаряды: +2 белых камня для {ship.Name}!");
                    }
                }
            }
        }

        // All Tetracatapults get +1 white stone on boarding
        foreach (var p in game.GetPlayers())
        {
            foreach (var ship in p.Board.PlacedShips)
            {
                if (ship.IsDestroyed) continue;
                foreach (var w in ship.Weapons.Where(w =>
                             w.Type == WeaponType.Tetracatapult && IsWeaponOperational(ship, w)))
                {
                    w.Ammo += 1;
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
                             w.Type == WeaponType.Incendiary && IsWeaponOperational(ship, w)))
                {
                    w.Ammo += 1;
                }
            }
        }
    }

    private static bool HasLivingMidShip(BattleshipPlayer player)
    {
        return player.Board.PlacedShips.Any(s => s.Range == RangeClass.Mid && !s.IsSummon &&
            !s.IsDestroyed && !s.Statuses.Contains(ShipStatusType.Capture));
    }

    private static (bool gameOver, string winnerId) CheckDesiccatorBoardingWin(BattleshipGame game)
    {
        if (!game.BoardingTriggered) return (false, null);
        var owners = game.GetPlayers().Where(player => player.Board.PlacedShips.Any(s =>
                s.Abilities.Contains("auto_win_boarding") && !s.IsDestroyed &&
                !s.Statuses.Contains(ShipStatusType.Capture)))
            .ToList();
        return owners.Count == 1 ? (true, owners[0].DiscordId) : (false, null);
    }

    // ── Summon Movement ──────────────────────────────────────────────

    public static void MoveSummons(BattleshipGame game)
    {
        RefreshPoisonZones(game);
        foreach (var player in game.GetPlayers())
        {
            var opponent = game.GetOpponent(player.DiscordId);
            if (opponent == null) continue;

            foreach (var summon in player.Summons.Where(s => s.IsAlive && !s.WaitingForTurnBack && !s.WaitingForDirectionChoice && s.SpawnedAtShot < game.ShotCount).ToList())
            {
                for (var step = 0; step < summon.Speed; step++)
                {
                    // Clear old SummonRef on opponent's board
                    var oldCell = opponent.Board.GetCell(summon.Row, summon.Col);
                    if (oldCell?.SummonRef == summon) oldCell.SummonRef = null;

                    var (newRow, newCol) = GetNextPosition(summon.Row, summon.Col, summon.MoveDirection);

                    // Out of bounds — mark for turn-back
                    if (newRow < 0 || newRow >= 10 || newCol < 0 || newCol >= 10)
                    {
                        TransmitScoutReveal(game, summon);
                        // Only the ordinary Ram can reverse from row 10. Boarding ships and
                        // every other summon leave the board permanently.
                        if (summon.Type == SummonType.Ram && !summon.IsBoardingShip)
                            summon.WaitingForTurnBack = true;
                        else
                            summon.IsAlive = false;
                        break;
                    }

                    var targetCell = opponent.Board.GetCell(newRow, newCol);
                    MarkSummonTrail(opponent.Board, newRow, newCol, summon.Type);

                    // Entry precedence: permanent fire, Freeze, live-deck collision, poison, reveal.
                    if (targetCell is { IsBurning: true })
                    {
                        TransmitScoutReveal(game, summon);
                        summon.IsAlive = false;
                        game.AddLog($"Призванное существо сгорело в огне! ({(char)('A' + newCol)}{newRow + 1})");
                        if (summon.Type == SummonType.Brander)
                            DetonateBrander(game, opponent, summon, newRow, newCol, player);
                        break;
                    }

                    if (IsInFreezeZone(opponent, newRow, newCol))
                    {
                        FreezeSummon(game, player, opponent, summon);
                        break;
                    }

                    // Only living decks block movement.
                    if (targetCell?.ShipRef != null && HasAliveDeckAt(targetCell.ShipRef, newRow, newCol))
                    {
                        HandleSummonCollision(game, summon, targetCell.ShipRef, opponent, newRow, newCol);
                        // CursedBoat and boarding ships that devastated 1-2 deckers continue;
                        // Brander no longer passes through live decks (ТЗ #16) — dies via the else below
                        if (summon.Type == SummonType.CursedBoat)
                        {
                            // CursedBoat applies Devastated, then waits for owner to choose direction
                            summon.Row = newRow;
                            summon.Col = newCol;
                            summon.WaitingForDirectionChoice = true;
                            break;
                        }
                        else if (summon.IsBoardingShip && summon.IsAlive)
                        {
                            // Boarding ship devastated a small ship, continues
                            summon.Row = newRow;
                            summon.Col = newCol;
                            continue;
                        }
                        else
                        {
                            summon.IsAlive = false;
                        }
                        break;
                    }

                    if (game.PoisonZonesByBoardOwner.TryGetValue(opponent.DiscordId, out var poisonZone) &&
                        poisonZone.Contains((newRow, newCol)))
                    {
                        TransmitScoutReveal(game, summon);
                        summon.IsAlive = false;
                        game.AddLog("Призванное существо погибло в ядовитом конусе!");
                        break;
                    }

                    // Ram/PirateBoat: reveal cells they pass through
                    if (summon.Type is SummonType.Ram or SummonType.PirateBoat)
                    {
                        var passCell = opponent.Board.GetCell(newRow, newCol);
                        if (passCell != null) RevealCell(opponent.Board, passCell, player);
                    }

                    // Boarding ships: reveal surrounding cells (radius = RevealRadius from ship's Space)
                    if (summon.IsBoardingShip)
                    {
                        RevealArea(opponent.Board, newRow, newCol, summon.RevealRadius, player);
                    }

                    // Scout: accumulate reveal data (deferred), skip poison zones
                    if (summon.Type == SummonType.Scout)
                    {
                        for (var dr = -summon.RevealRadius; dr <= summon.RevealRadius; dr++)
                        for (var dc = -summon.RevealRadius; dc <= summon.RevealRadius; dc++)
                        {
                            var sr = newRow + dr;
                            var sc = newCol + dc;
                            if (sr >= 0 && sr < 10 && sc >= 0 && sc < 10)
                            {
                                // Skip cells in poison cone zones
                                if (game.PoisonZonesByBoardOwner.TryGetValue(opponent.DiscordId, out var zone) &&
                                    zone.Contains((sr, sc))) continue;
                                if (!summon.ScoutRevealData.Contains((sr, sc)))
                                    summon.ScoutRevealData.Add((sr, sc));
                            }
                        }
                    }

                    summon.Row = newRow;
                    summon.Col = newCol;

                    // Set SummonRef on opponent's board at new position
                    var newCell = opponent.Board.GetCell(newRow, newCol);
                    if (newCell != null) newCell.SummonRef = summon;
                }
            }

            // Remove dead summons (but keep WaitingForTurnBack ones)
            // Clear SummonRef for dead summons
            foreach (var deadSummon in player.Summons.Where(s => !s.IsAlive))
            {
                var deadCell = opponent.Board.GetCell(deadSummon.Row, deadSummon.Col);
                if (deadCell?.SummonRef == deadSummon) deadCell.SummonRef = null;
            }
            player.Summons.RemoveAll(s => !s.IsAlive);
        }

        // Rebuild zones and resolve ships after all stable-order summon movement.
        ProcessPoisonCones(game);
    }

    private static void FreezeSummon(
        BattleshipGame game,
        BattleshipPlayer summonOwner,
        BattleshipPlayer boardOwner,
        Summon summon)
    {
        summon.ScoutRevealData.Clear();
        summon.WaitingForTurnBack = false;
        summon.WaitingForDirectionChoice = false;
        summon.IsAlive = false;
        game.AddLogFor(boardOwner.DiscordId, "[Драккар] Призванное существо заморожено аурой Драккара");
        if (summonOwner.Board.PlacedShips.Any(s =>
                !s.IsDestroyed && s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed)))
            game.AddLogFor(summonOwner.DiscordId, "[Мачта] Наших заморозили!");
    }

    /// <summary>Register a spawn/re-entry and immediately resolve its entry hazards.</summary>
    public static bool RegisterSummonOnTargetBoard(BattleshipGame game, BattleshipPlayer owner, Summon summon)
    {
        var boardOwner = game.GetOpponent(owner.DiscordId);
        var cell = boardOwner?.Board.GetCell(summon.Row, summon.Col);
        if (boardOwner == null || cell == null || cell.SummonRef is { IsAlive: true }) return false;

        MarkSummonTrail(boardOwner.Board, summon.Row, summon.Col, summon.Type);
        RefreshPoisonZones(game);
        if (cell.IsBurning)
        {
            TransmitScoutReveal(game, summon);
            summon.IsAlive = false;
            if (summon.Type == SummonType.Brander)
                DetonateBrander(game, boardOwner, summon, summon.Row, summon.Col, owner);
            return true;
        }
        if (IsInFreezeZone(boardOwner, summon.Row, summon.Col))
        {
            FreezeSummon(game, owner, boardOwner, summon);
            return true;
        }
        if (cell.ShipRef != null && HasAliveDeckAt(cell.ShipRef, summon.Row, summon.Col))
        {
            HandleSummonCollision(game, summon, cell.ShipRef, boardOwner, summon.Row, summon.Col);
            if (summon.Type == SummonType.CursedBoat)
            {
                summon.WaitingForDirectionChoice = true;
                cell.SummonRef = summon;
            }
            else if (summon.IsBoardingShip && summon.IsAlive)
            {
                cell.SummonRef = summon;
            }
            else
                summon.IsAlive = false;
            return true;
        }
        if (game.PoisonZonesByBoardOwner.TryGetValue(boardOwner.DiscordId, out var zone) &&
            zone.Contains((summon.Row, summon.Col)))
        {
            TransmitScoutReveal(game, summon);
            summon.IsAlive = false;
            return true;
        }

        if (summon.Type is SummonType.Ram or SummonType.PirateBoat)
            RevealCell(boardOwner.Board, cell, owner);
        if (summon.IsBoardingShip)
            RevealArea(boardOwner.Board, summon.Row, summon.Col, summon.RevealRadius, owner);
        cell.SummonRef = summon;
        return true;
    }

    private static void MarkSummonTrail(Board board, int row, int col, SummonType type)
    {
        var cell = board.GetCell(row, col);
        cell?.SummonTrails.Add(type);
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
                    if (targetShip.Decks.Count <= 2)
                    {
                        targetShip.Statuses.Add(ShipStatusType.Devastated);
                        foreach (var d in targetShip.Decks) d.CurrentHp = 0;
                        RevealShip(targetOwner.Board, targetShip, attacker);
                        HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Devastated);
                        game.AddLog($"Абордажный корабль опустошил {targetShip.Name}! {coord}");
                        summon.IsAlive = true; // continue moving (don't die)
                    }
                    else
                    {
                        var collisionDeckIndex = GetDeckIndexAtCell(targetShip, collisionRow, collisionCol);
                        var collisionDeck = collisionDeckIndex >= 0 ? targetShip.Decks[collisionDeckIndex] : null;
                        if (collisionDeck is { IsDestroyed: false })
                        {
                            collisionDeck.CurrentHp -= summon.CollisionDamage;
                            if (collisionDeck.CurrentHp < 0) collisionDeck.CurrentHp = 0;
                            game.AddLog($"Абордажный корабль протаранил {targetShip.Name}! (-{summon.CollisionDamage} HP) {coord}");
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
                    ramDeck.CurrentHp -= summon.CollisionDamage;
                    if (ramDeck.CurrentHp < 0) ramDeck.CurrentHp = 0;
                    game.AddLog($"Таран врезался в {targetShip.Name}! (-{summon.CollisionDamage} HP) {coord}");
                    // Mark cell on target owner's board (#8)
                    MarkRamDamageOnBoard(targetOwner, attacker, collisionRow, collisionCol, ramDeck);
                    // Ram triggers barge explosion (#9); both players see decks + zone statuses (ТЗ #5)
                    if (targetShip.Abilities.Contains("explode_on_hit") && !targetShip.IsDestroyed)
                    {
                        ExplodeShip(game, targetOwner, targetShip, attacker);
                        HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Explosion);
                        game.AddLog($"{targetShip.Name} взорвался от тарана! {coord}");
                    }
                    else if (targetShip.IsDestroyed)
                    {
                        RevealShip(targetOwner.Board, targetShip, attacker);
                        HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Collision);
                    }
                }
                break;

            case SummonType.PirateBoat:
                if (targetShip.Decks.Count <= 2)
                {
                    if (!targetShip.Statuses.Contains(ShipStatusType.Capture))
                    {
                        targetShip.Statuses.Add(ShipStatusType.Capture);
                        targetOwner.SelectedShotType = ShotType.Ballista;
                        targetOwner.SelectedWeapon = null;
                        foreach (var (r, c) in targetShip.GetOccupiedCells())
                        {
                            var capturedCell = targetOwner.Board.GetCell(r, c);
                            if (capturedCell == null) continue;
                            RevealCell(targetOwner.Board, capturedCell, attacker);
                        }
                        game.AddLog($"Пиратская лодка захватила {targetShip.Name}! {coord}");
                    }
                    else game.AddLog($"{targetShip.Name} уже находится под CAPTURE. {coord}");
                }
                else
                {
                    game.AddLog($"Пиратская лодка разбилась о {targetShip.Name}! {coord}");
                }
                break;

            case SummonType.CursedBoat:
                if (!targetShip.Statuses.Contains(ShipStatusType.Devastated))
                    targetShip.Statuses.Add(ShipStatusType.Devastated);
                foreach (var d in targetShip.Decks) d.CurrentHp = 0;
                RevealShip(targetOwner.Board, targetShip, attacker);
                HandleShipDeath(game, targetOwner, targetShip, ShipDestructionCause.Devastated);
                game.AddLog($"Проклятый корабль опустошил {targetShip.Name}! {coord}");
                break;

            case SummonType.Scout:
                // Reveal accumulated data on collision/death
                var summonOwner = game.GetPlayer(summon.OwnerId);
                TransmitScoutReveal(game, summon);
                RevealArea(targetOwner.Board, collisionRow, collisionCol, summon.RevealRadius, summonOwner);
                game.AddLog($"Разведчик обнаружил корабли противника!");
                break;

            case SummonType.Brander:
                // ТЗ #16: Brander cannot pass live decks — разбивается без детонации (решение дизайнера)
                game.AddLog($"Брандер разбился о {targetShip.Name}! {coord}");
                break;
        }
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
            if (ship.IsDestroyed || !ship.Abilities.Contains("freeze_nearby")) continue;
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
                if (ship.IsDestroyed || !ship.Abilities.Contains("freeze_nearby")) continue;
                var occupied = ship.GetOccupiedCells();

                // Freeze enemy summons
                foreach (var summon in opponent.Summons.Where(s => s.IsAlive).ToList())
                {
                    foreach (var (sr, sc) in occupied)
                    {
                        if (Math.Abs(sr - summon.Row) <= ship.Space && Math.Abs(sc - summon.Col) <= ship.Space)
                        {
                            FreezeSummon(game, opponent, player, summon);
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
        RefreshPoisonZones(game);

        foreach (var player in game.GetPlayers())
        {
            foreach (var ship in player.Board.PlacedShips)
            {
                if (ship.IsDestroyed || ship.Statuses.Contains(ShipStatusType.Capture) ||
                    !ship.Abilities.Contains("poison_cone") || game.Phase == BsGamePhase.Boarding) continue;

                var coneCells = GetPoisonConeCells(ship);
                var opponent = game.GetOpponent(player.DiscordId);

                foreach (var (cr, cc) in coneCells)
                {
                    // Enemy summons physically occupy this player's board.
                    if (opponent != null)
                    {
                        foreach (var summon in opponent.Summons.Where(s => s.IsAlive && s.Row == cr && s.Col == cc).ToList())
                        {
                            TransmitScoutReveal(game, summon);
                            summon.IsAlive = false;
                            game.AddLog($"Ядовитый конус {ship.Name} убил призванное существо!");
                        }
                    }

                    // The cone exists only on its source's physical board.
                    var allyCell = player.Board.GetCell(cr, cc);
                    if (allyCell?.ShipRef != null && !allyCell.ShipRef.IsDestroyed && allyCell.ShipRef.Id != ship.Id)
                    {
                        var allyShip = allyCell.ShipRef;
                        foreach (var d in allyShip.Decks) d.CurrentHp = 0;
                        RevealShip(player.Board, allyShip, opponent);
                        HandleShipDeath(game, player, allyShip, ShipDestructionCause.Poison);
                        game.AddLog($"Ядовитый конус {ship.Name} уничтожил союзный {allyShip.Name}!");
                    }
                }
            }
        }

    }

    public static void RefreshPoisonZones(BattleshipGame game)
    {
        game.PoisonZonesByBoardOwner.Clear();
        foreach (var player in game.GetPlayers())
        {
            var zones = player.Board.PlacedShips
                .Where(s => !s.IsDestroyed && !s.Statuses.Contains(ShipStatusType.Capture) &&
                            s.Abilities.Contains("poison_cone") &&
                            game.Phase != BsGamePhase.Boarding)
                .SelectMany(GetPoisonConeCells)
                .ToHashSet();
            game.PoisonZonesByBoardOwner[player.DiscordId] = zones;
        }
    }

    private static List<(int row, int col)> GetPoisonConeCells(Ship ship)
    {
        var cells = new List<(int, int)>();
        var firstDeck = ship.GetOccupiedCells()[0];
        var (forwardRow, forwardCol) = ship.Orientation == Orientation.Vertical ? (-1, 0) : (0, -1);
        var (sideRow, sideCol) = ship.Orientation == Orientation.Vertical ? (0, 1) : (1, 0);
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

    /// <summary>
    /// Transmit deferred scout reveal data when a scout dies or leaves the map.
    /// Freeze explicitly clears the payload before this helper can run.
    /// </summary>
    private static void TransmitScoutReveal(BattleshipGame game, Summon summon)
    {
        if (summon.Type != SummonType.Scout || summon.ScoutRevealData.Count == 0) return;
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
        var dodgeDir = deckIndex == 0 ? 1 : -1; // if bow hit, move stern-ward; if stern hit, move bow-ward

        if (TryMoveShip(shipOwner, ship, dodgeDir))
        {
            game.AddLog($"{ship.Name} увернулся от выстрела! ({(char)('A' + col)}{row + 1})");
            // The cell is now empty — mark as miss
            var cell = shipOwner.Board.GetCell(row, col);
            if (cell != null)
            {
                cell.IsMiss = true;
                cell.IsHit = false;
                cell.WasShipHit = false;
                cell.WasScratched = false;
            }

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
                    game.AddLog($"{ship.Name} заплыл в огонь при уклонении!");
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
                        game.AddLog($"{ship.Name} заплыл в ядовитый конус при уклонении!");
                        break;
                    }
                }
            }

            return new ShotResult
            {
                Miss = true, Row = row, Col = col, TurnContinues = false,
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
        if (ship.Orientation == Orientation.Horizontal)
            newCol += delta;
        else
            newRow += delta;

        // Check all cells of new position
        var newCells = new List<(int, int)>();
        for (var i = 0; i < ship.Decks.Count; i++)
        {
            var r = ship.Orientation == Orientation.Vertical ? newRow + i : newRow;
            var c = ship.Orientation == Orientation.Horizontal ? newCol + i : newCol;
            if (r < 0 || r >= 10 || c < 0 || c >= 10) return false;
            newCells.Add((r, c));
        }

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
                    if (currentCells.Contains((nr, nc))) continue;
                    if (Math.Abs(ar - nr) <= spacing && Math.Abs(ac - nc) <= spacing)
                        return false;
                }
            }
        }

        // Move ship
        foreach (var (r, c) in ship.GetOccupiedCells())
            owner.Board.Grid[r, c].ShipRef = null;

        ship.Row = newRow;
        ship.Col = newCol;

        foreach (var (r, c) in ship.GetOccupiedCells())
            owner.Board.Grid[r, c].ShipRef = ship;

        return true;
    }

    /// <summary>
    /// Manual move ship 1-2 cells in a direction.
    /// </summary>
    public static bool ManualMoveShip(BattleshipPlayer player, Ship ship, Direction direction, int distance)
    {
        if (distance < 1 || distance > 2) return false;

        // Restrict to forward/backward along ship orientation axis only
        if (ship.Orientation == Orientation.Horizontal && direction is not (Direction.Left or Direction.Right))
            return false;
        if (ship.Orientation == Orientation.Vertical && direction is not (Direction.Up or Direction.Down))
            return false;

        var (dr, dc) = direction switch
        {
            Direction.Up => (-1, 0),
            Direction.Down => (1, 0),
            Direction.Left => (0, -1),
            Direction.Right => (0, 1),
            _ => (0, 0)
        };

        var oldCells = ship.GetOccupiedCells();

        // Remove from current position
        foreach (var (r, c) in oldCells)
            player.Board.Grid[r, c].ShipRef = null;

        var newRow = ship.Row + dr * distance;
        var newCol = ship.Col + dc * distance;

        // Validate new position
        var newCells = new List<(int, int)>();
        for (var i = 0; i < ship.Decks.Count; i++)
        {
            var r = ship.Orientation == Orientation.Vertical ? newRow + i : newRow;
            var c = ship.Orientation == Orientation.Horizontal ? newCol + i : newCol;
            if (r < 0 || r >= 10 || c < 0 || c >= 10)
            {
                // Restore
                foreach (var (or, oc) in ship.GetOccupiedCells())
                    player.Board.Grid[or, oc].ShipRef = ship;
                return false;
            }
            newCells.Add((r, c));
        }

        // Check no collisions
        foreach (var (r, c) in newCells)
        {
            var cell = player.Board.GetCell(r, c);
            if (cell?.ShipRef != null && cell.ShipRef.Id != ship.Id)
            {
                foreach (var (or, oc) in ship.GetOccupiedCells())
                    player.Board.Grid[or, oc].ShipRef = ship;
                return false;
            }
        }

        for (var i = 0; i < oldCells.Count && i < ship.Decks.Count; i++)
        {
            if (ship.Decks[i].IsDestroyed && !ship.ManeuverStaleHitCells.Contains(oldCells[i]))
                ship.ManeuverStaleHitCells.Add(oldCells[i]);
        }

        // Clear the own-view hit flag from vacated cells — deck damage lives on the Ship
        // (ТЗ #19/#22); fog snapshots WasShipHit/WasScratched deliberately stay (the opponent
        // keeps seeing the OLD hit spot, not the new position).
        foreach (var (r, c) in oldCells)
            player.Board.Grid[r, c].IsHit = false;

        ship.Row = newRow;
        ship.Col = newCol;

        foreach (var (r, c) in ship.GetOccupiedCells())
            player.Board.Grid[r, c].ShipRef = ship;

        return true;
    }

    /// <summary>
    /// Set CursedBoat direction after collision (player's choice of 4 directions).
    /// </summary>
    public static bool SetCursedBoatDirection(BattleshipPlayer player, string summonId, Direction direction)
    {
        var summon = player.Summons.FirstOrDefault(s => s.Id == summonId && s.IsAlive && s.WaitingForDirectionChoice);
        if (summon == null) return false;

        summon.MoveDirection = direction;
        summon.WaitingForDirectionChoice = false;
        return true;
    }

    /// <summary>
    /// Generate Mast warnings when opponent deploys summon. Includes spawn coordinates.
    /// </summary>
    public static string GenerateMastWarning(BattleshipPlayer player, SummonType summonType, int row = -1, int col = -1)
    {
        var hasMast = player.Board.PlacedShips.Any(s =>
            !s.IsDestroyed && s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed));

        if (!hasMast) return null;

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
