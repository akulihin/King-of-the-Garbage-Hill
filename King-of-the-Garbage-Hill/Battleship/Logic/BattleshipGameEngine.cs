using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Core combat engine: processes shots, damage, status effects, summon movement, win conditions.
/// </summary>
public static class BattleshipGameEngine
{
    private static readonly Random Rng = new();

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
            game.GameLog.Add($"{player.Username} пропускает ход (штраф)!");
            return true;
        }

        // Stun check — if stunned, skip turn and clear stun (one-time trigger)
        if (player.StunShotExpiry >= game.ShotCount)
        {
            player.StunShotExpiry = -1;
            game.GameLog.Add($"{player.Username} оглушён и пропускает ход!");
            return true;
        }

        return false;
    }

    // ── Shot Processing ──────────────────────────────────────────────

    /// <summary>
    /// Check if player has any alive Mid or Close range ships (needed for ballista restriction).
    /// </summary>
    public static bool HasAliveMidOrCloseShips(BattleshipPlayer player)
    {
        return player.Board.PlacedShips.Any(s => !s.IsDestroyed && !s.IsSummon &&
            s.Range is RangeClass.Mid or RangeClass.Close);
    }

    public static ShotResult ProcessShot(BattleshipGame game, BattleshipPlayer shooter, int row, int col)
    {
        var opponent = game.GetOpponent(shooter.DiscordId);
        if (opponent == null)
            return new ShotResult { Miss = true, Message = "Нет противника." };

        // Ballista restriction: can't fire regular shots if no Mid/Close ships remain
        if (shooter.SelectedShotType is ShotType.Ballista or ShotType.Buckshot &&
            !HasAliveMidOrCloseShips(shooter))
        {
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                Message = "Нет кораблей класса Mid или Close — нельзя стрелять обычными выстрелами!" };
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

        // Increment global shot counter
        game.ShotCount++;

        // Consume ammo from selected weapon
        shooter.SelectedWeapon?.UseAmmo();

        // Far range restriction: ships on rows 8-9 can't target enemy rows 8-9
        // Check if selected weapon belongs to a Far ship on rows 8-9
        if (shooter.SelectedWeapon?.ShipId != null)
        {
            var weaponShip = shooter.Board.PlacedShips.Find(s => s.Id == shooter.SelectedWeapon.ShipId);
            if (weaponShip != null && weaponShip.Range == RangeClass.Far && weaponShip.Row >= 8 && row >= 8)
            {
                return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false,
                    Message = "Дальнобойный корабль не может стрелять по задним рядам!" };
            }
        }

        var cell = opponent.Board.GetCell(row, col);
        if (cell == null)
            return new ShotResult { Miss = true, Message = "Клетка за пределами поля." };

        // Brander detonation: shooter shoots their own Brander on opponent's board
        var friendlyBrander = shooter.Summons.FirstOrDefault(s =>
            s.IsAlive && s.Type == SummonType.Brander && s.Row == row && s.Col == col);
        if (friendlyBrander != null)
        {
            friendlyBrander.IsAlive = false;
            BurnArea(game, opponent, row, col, 1);
            game.GameLog.Add($"Брандер взорвался! ({(char)('A' + col)}{row + 1})");
            return new ShotResult
            {
                Row = row, Col = col, TurnContinues = false, Burned = true,
                Message = "Брандер взорвался!"
            };
        }

        // Allow re-targeting scratched cells (armor survived, deck still has HP)
        // and Incendiary can target already-hit cells to burn the ship via damaged deck
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
            return new ShotResult { Miss = true, Message = "Эта клетка уже была атакована.", TurnContinues = false };
        }

        // Reveal cell and track revealed count
        if (!cell.IsRevealed)
        {
            cell.IsRevealed = true;
            IncrementRevealedCount(opponent);
        }

        // White Stone always stuns opponent regardless of hit/miss
        if (shooter.SelectedShotType == ShotType.WhiteStone)
        {
            opponent.StunShotExpiry = game.ShotCount + 1;
        }

        // Greek Fire: creates permanent burning cell, kills summon without penalty
        if (shooter.SelectedShotType == ShotType.GreekFire)
        {
            return ProcessGreekFireShot(game, shooter, opponent, cell, row, col);
        }

        // Miss — empty cell
        if (cell.ShipRef == null && cell.SummonRef == null)
        {
            cell.IsMiss = true;
            game.GameLog.Add($"{shooter.Username} промахнулся ({(char)('A' + col)}{row + 1})");
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false, Message = "Мимо!" };
        }

        // Hit summon
        if (cell.SummonRef != null)
        {
            // Allied summon hit — turn ends, no penalty
            if (cell.SummonRef.OwnerId == shooter.DiscordId)
            {
                cell.IsHit = true;
                cell.SummonRef.IsAlive = false;
                cell.SummonRef = null;
                game.GameLog.Add($"{shooter.Username} попал в своё призванное существо ({(char)('A' + col)}{row + 1})");
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
    /// Process Greek Fire shot: creates permanent burning cell, kills summon without penalty.
    /// </summary>
    private static ShotResult ProcessGreekFireShot(BattleshipGame game, BattleshipPlayer shooter, BattleshipPlayer opponent, Cell cell, int row, int col)
    {
        // Create permanent burning cell
        cell.IsBurning = true;
        cell.IsHit = true;

        // Hit summon — kill without penalty, turn ends
        if (cell.SummonRef != null)
        {
            cell.SummonRef.IsAlive = false;
            cell.SummonRef = null;
            game.GameLog.Add($"Греческий огонь сжёг призванное существо! ({(char)('A' + col)}{row + 1})");
            return new ShotResult { Row = row, Col = col, TurnContinues = false,
                Message = "Греческий огонь уничтожил призыв без штрафа!" };
        }

        // Hit ship — burn it
        if (cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
        {
            if (!cell.ShipRef.Statuses.Contains(ShipStatusType.BurnResist))
            {
                foreach (var d in cell.ShipRef.Decks) d.CurrentHp = 0;
                cell.ShipRef.Statuses.Add(ShipStatusType.Burn);
                RevealShip(opponent.Board, cell.ShipRef, shooter);
                HandleShipDeath(game, opponent, cell.ShipRef);
                game.GameLog.Add($"Греческий огонь сжёг {cell.ShipRef.Name}!");
                return new ShotResult { Row = row, Col = col, TurnContinues = true,
                    Message = $"Греческий огонь сжёг {cell.ShipRef.Name}!", AffectedShipName = cell.ShipRef.Name };
            }
            game.GameLog.Add($"{cell.ShipRef.Name} устоял против греческого огня (огнеупорность)!");
            return new ShotResult { Row = row, Col = col, TurnContinues = false,
                Message = "Корабль устоял — огнеупорность!" };
        }

        // Miss — but cell still burns permanently
        cell.IsMiss = true;
        game.GameLog.Add($"Греческий огонь горит на ({(char)('A' + col)}{row + 1})!");
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
        deck.CurrentHp -= damage;
        if (deck.CurrentHp < 0) deck.CurrentHp = 0;

        if (ship.IsDestroyed)
        {
            ship.Statuses.Remove(ShipStatusType.Capture);
            game.GameLog.Add($"{shooter.Username} уничтожил захваченный {ship.Name}!");
            return new ShotResult { Row = row, Col = col, TurnContinues = true,
                Message = $"Захваченный {ship.Name} уничтожен!", AffectedShipName = ship.Name };
        }

        if (deck.IsDestroyed)
        {
            game.GameLog.Add($"{shooter.Username} повредил палубу захваченного {ship.Name}!");
            return new ShotResult { Row = row, Col = col, TurnContinues = true,
                Message = $"Палуба захваченного {ship.Name} уничтожена!", AffectedShipName = ship.Name };
        }

        game.GameLog.Add($"{shooter.Username} поцарапал броню захваченного {ship.Name}.");
        return new ShotResult { Row = row, Col = col, TurnContinues = false,
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
        summon.IsAlive = false;
        cell.SummonRef = null;

        // Free up opponent's summon slot
        var opponent = game.GetOpponent(shooter.DiscordId);
        if (opponent != null && opponent.SummonSlotsUsed > 0)
            opponent.SummonSlotsUsed--;

        game.GameLog.Add($"{shooter.Username} уничтожил вражеский призыв на своём поле ({(char)('A' + col)}{row + 1})");

        // Penalty for killing summon in rear rows (0-2)
        var penalty = false;
        if (row <= 2 && game.ShotCount - summon.SpawnedAtShot > 1)
        {
            shooter.HasPenalty = true;
            penalty = true;
            game.GameLog.Add($"{shooter.Username} получает штраф за уничтожение призыва в тылу!");
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
        // Ballista restriction: can't fire buckshot if no Mid/Close ships remain
        if (!HasAliveMidOrCloseShips(shooter))
        {
            return new ShotResult { Miss = true, Row = topRow, Col = topCol, TurnContinues = false,
                Message = "Нет кораблей класса Mid или Close — нельзя стрелять обычными выстрелами!" };
        }

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
            AoECells = new List<(int row, int col)>()
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

            aggregate.AoECells.Add((r, c));

            // Allow re-targeting scratched cells (armor survived)
            if (cell.IsHit && cell.WasScratched && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                // Fall through to process as normal hit
            }
            else if (cell.IsHit || cell.IsMiss) continue;

            if (!cell.IsRevealed)
            {
                cell.IsRevealed = true;
                IncrementRevealedCount(opponent);
            }

            if (cell.ShipRef == null && cell.SummonRef == null)
            {
                cell.IsMiss = true;
                continue;
            }

            if (cell.SummonRef != null)
            {
                cell.IsHit = true;
                var summon = cell.SummonRef;
                summon.IsAlive = false;
                cell.SummonRef = null;
                anyHit = true;
                // Summon kill: never resets turn (Bug #6)
                // Penalty check for rows 0-2 (Bug #7)
                if (r <= 2 && game.ShotCount - summon.SpawnedAtShot > 1)
                {
                    shooter.HasPenalty = true;
                    game.GameLog.Add($"{shooter.Username} получает штраф за уничтожение призыва картечью в тылу!");
                }
                continue;
            }

            // Ship hit with buckshot damage (1)
            var ship = cell.ShipRef;
            cell.IsHit = true;
            cell.WasShipHit = true;

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
                deck.CurrentHp -= 1; // buckshot damage
                if (deck.CurrentHp <= 0) deck.CurrentHp = 0;
                anyHit = true;
                if (deck.IsDestroyed) anyDestroyed = true;
                else cell.WasScratched = true;

                // Check explode_on_hit — any damage = full destruction
                if (ship.Abilities.Contains("explode_on_hit") && !ship.IsDestroyed)
                {
                    ExplodeShip(game, opponent, ship);
                    foreach (var d in ship.Decks) d.CurrentHp = 0;
                }

                if (ship.IsDestroyed)
                {
                    anySunk = true;
                    RevealShip(opponent.Board, ship, shooter);
                    HandleShipDeath(game, opponent, ship);
                    game.GameLog.Add($"Картечь потопила {ship.Name}!");
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

        game.GameLog.Add($"{shooter.Username} выстрелил картечью ({(char)('A' + topCol)}{topRow + 1})");
        return aggregate;
    }

    private static ShotResult ProcessSummonHit(BattleshipGame game, BattleshipPlayer shooter, Cell cell, int row, int col)
    {
        var summon = cell.SummonRef;
        cell.IsHit = true;
        summon.IsAlive = false;
        cell.SummonRef = null;

        // Deferred scout reveal on death
        if (summon.Type == SummonType.Scout && summon.ScoutRevealData.Count > 0)
        {
            var summonOwner = game.GetPlayer(summon.OwnerId);
            var opponent = game.GetOpponent(summon.OwnerId);
            if (opponent != null)
            {
                foreach (var (sr, sc) in summon.ScoutRevealData)
                {
                    var revealCell = opponent.Board.GetCell(sr, sc);
                    if (revealCell != null && !revealCell.IsRevealed)
                    {
                        revealCell.IsRevealed = true;
                        if (opponent != null) IncrementRevealedCount(opponent);
                    }
                }
            }
        }

        game.GameLog.Add($"{shooter.Username} уничтожил призванное существо ({(char)('A' + col)}{row + 1})");

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
                game.GameLog.Add($"{shooter.Username} получает штраф за уничтожение призыва в тылу!");
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
        cell.IsHit = true;
        cell.WasShipHit = true;

        // Find which deck was hit
        var deckIndex = GetDeckIndexAtCell(ship, row, col);
        if (deckIndex < 0 || deckIndex >= ship.Decks.Count)
            return new ShotResult { Miss = true, Row = row, Col = col, TurnContinues = false };

        var deck = ship.Decks[deckIndex];

        // Check auto_dodge_bow_stern (Light Wood Triple)
        if (ship.Abilities.Contains("auto_dodge_bow_stern"))
        {
            var dodgeResult = ProcessAutoDodge(game, opponent, ship, row, col, deckIndex);
            if (dodgeResult != null) return dodgeResult;
        }

        // Check nimble/ballista_immune — reveal cell even on immune
        if (ship.Abilities.Contains("ballista_immune") && shooter.SelectedShotType == ShotType.Ballista)
        {
            cell.IsRevealed = true; // #34: reveal on ballista miss
            var nimbleMsg = Random.Shared.Next(2) == 0
                ? "Юркая единичка! Опять увернулась!"
                : "Ну и юркая же она! Камней бы ей на голову!";
            game.GameLog.Add($"{nimbleMsg} ({(char)('A' + col)}{row + 1})");
            return new ShotResult { Miss = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
                Message = nimbleMsg, AffectedShipName = ship.Name };
        }

        // Incendiary: burn entire ship on ANY hit (damage = 0, kill via burn status)
        if (shooter.SelectedShotType == ShotType.Incendiary)
        {
            if (!ship.Statuses.Contains(ShipStatusType.BurnResist))
            {
                foreach (var d in ship.Decks) d.CurrentHp = 0;
                ship.Statuses.Add(ShipStatusType.Burn);
                RevealShip(opponent.Board, ship, shooter);
                HandleShipDeath(game, opponent, ship);
                game.GameLog.Add($"Зажигательный снаряд сжёг {ship.Name}!");
                return new ShotResult
                {
                    Row = row, Col = col, TurnContinues = true, ShipSunk = true, Burned = true,
                    Message = $"{ship.Name} сгорел!", AffectedShipName = ship.Name
                };
            }
            // BurnResist: incendiary deals 0 damage, just scratches
            cell.WasScratched = true;
            game.GameLog.Add($"Зажигательный снаряд не смог поджечь {ship.Name} (огнеупорность)!");
            return new ShotResult
            {
                Hit = true, Scratched = true, Row = row, Col = col, TurnContinues = false,
                Message = "Огнеупорность! Поцарапал броню!", AffectedShipName = ship.Name
            };
        }

        // Calculate damage
        var damage = GetDamage(shooter);

        // White Stone module destroy
        if (shooter.SelectedShotType == ShotType.WhiteStone && deck.Module != null)
        {
            deck.ModuleDestroyed = true;
            game.GameLog.Add($"Белый камень разрушил модуль {deck.Module} на {ship.Name}!");
        }

        // Apply damage
        deck.CurrentHp -= damage;

        // Check explode_on_hit — triggers on ANY hit, not just death
        // Any damage = full barge destruction (GDD: "При получении любого урона — взрывается")
        if (ship.Abilities.Contains("explode_on_hit") && !ship.IsDestroyed)
        {
            ExplodeShip(game, opponent, ship);
            foreach (var d in ship.Decks) d.CurrentHp = 0;
            RevealShip(opponent.Board, ship, shooter);
            HandleShipDeath(game, opponent, ship);
            game.GameLog.Add($"{ship.Name} взорвался от попадания!");
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
                game.GameLog.Add($"{shooter.Username} потопил {ship.Name}! ({(char)('A' + col)}{row + 1})");
                HandleShipDeath(game, opponent, ship);

                return new ShotResult
                {
                    Hit = true, Destroyed = true, ShipSunk = true,
                    Row = row, Col = col, TurnContinues = true,
                    Message = $"{ship.Name} потоплен!",
                    AffectedShipName = ship.Name
                };
            }

            // Deck destroyed but ship survives
            game.GameLog.Add($"{shooter.Username} уничтожил палубу {ship.Name}! ({(char)('A' + col)}{row + 1})");

            // Mast warning for Maneuvering Double deck destruction — signals move ability activation
            if (ship.Abilities.Contains("manual_move_after_hit"))
            {
                var opponentHasMast = opponent.Board.PlacedShips.Any(s =>
                    !s.IsDestroyed && s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed));
                if (opponentHasMast)
                    game.GameLog.Add("[Мачта] Даёт по вёслам!");
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
        game.GameLog.Add($"{shooter.Username} поцарапал броню {ship.Name} ({(char)('A' + col)}{row + 1})");
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
        return Math.Max(0, (int)(baseDamage * shooter.DamageMultiplier));
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

    // ── Reveal Helpers ────────────────────────────────────────────────

    private static void RevealShip(Board board, Ship ship, BattleshipPlayer shooter = null)
    {
        var radius = ship.Space;
        var occupied = ship.GetOccupiedCells();

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
            }
        }

        // Then: reveal surrounding empty water cells
        foreach (var (r, c) in occupied)
        {
            for (var dr = -radius; dr <= radius; dr++)
            for (var dc = -radius; dc <= radius; dc++)
            {
                var nr = r + dr;
                var nc = c + dc;
                var cell = board.GetCell(nr, nc);
                if (cell != null && !cell.IsHit && cell.ShipRef == null)
                {
                    if (!cell.IsRevealed && shooter != null)
                        IncrementRevealedCount(shooter);
                    cell.IsRevealed = true;
                    cell.IsMiss = true;
                }
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
            if (!cell.IsRevealed && shooter != null)
                IncrementRevealedCount(shooter);
            cell.IsRevealed = true;
        }
    }

    private static void IncrementRevealedCount(BattleshipPlayer player)
    {
        if (player.RevealedCellCount < 100)
            player.RevealedCellCount++;
    }

    // ── Ship Death ────────────────────────────────────────────────────

    private static void HandleShipDeath(BattleshipGame game, BattleshipPlayer owner, Ship ship)
    {
        // Store pending pirate boat deploy on death (Bug #2: delayed ability, not auto-spawn)
        if (ship.Abilities.Contains("spawn_pirate_boat"))
        {
            if (!ship.Statuses.Contains(ShipStatusType.Burn))
            {
                var occupiedCols = ship.GetOccupiedCells().Select(c => c.col).Distinct().OrderBy(c => c).ToList();
                owner.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.PirateBoat,
                    AllowedColumns = occupiedCols,
                    Speed = 1,
                    SourceShipName = ship.Name
                });
                game.GameLog.Add($"Пиратский корабль готов к выпуску после гибели {ship.Name}!");
            }
        }

        // Store pending cursed boat deploy on death
        if (ship.Abilities.Contains("spawn_cursed_boat"))
        {
            if (!ship.Statuses.Contains(ShipStatusType.Burn))
            {
                var cursedCols = ship.GetOccupiedCells().Select(c => c.col).Distinct().OrderBy(c => c).ToList();
                owner.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.CursedBoat,
                    AllowedColumns = cursedCols,
                    Speed = 1,
                    Damage = 999,
                    SourceShipName = ship.Name
                });
                game.GameLog.Add($"Проклятый корабль готов к выпуску после гибели {ship.Name}!");
            }
        }

        // Explode on death (Incendiary Barge still explodes on full death too)
        if (ship.Abilities.Contains("explode_on_hit"))
        {
            ExplodeShip(game, owner, ship);
        }
    }

    /// <summary>
    /// Incendiary Barge explosion — burns surrounding area within Space radius.
    /// </summary>
    private static void ExplodeShip(BattleshipGame game, BattleshipPlayer owner, Ship ship)
    {
        var radius = ship.ExplosionRadius > 0 ? ship.ExplosionRadius : ship.Space;
        var occupied = ship.GetOccupiedCells();
        foreach (var (r, c) in occupied)
        {
            for (var dr = -radius; dr <= radius; dr++)
            for (var dc = -radius; dc <= radius; dc++)
            {
                var cell = owner.Board.GetCell(r + dr, c + dc);
                if (cell?.ShipRef != null && cell.ShipRef.Id != ship.Id && !cell.ShipRef.IsDestroyed)
                {
                    if (!cell.ShipRef.Statuses.Contains(ShipStatusType.BurnResist))
                    {
                        if (!cell.ShipRef.Statuses.Contains(ShipStatusType.Burn))
                        {
                            cell.ShipRef.Statuses.Add(ShipStatusType.Burn);
                            cell.IsBurning = true;
                            // Kill the ship — explosion burn is instant death
                            foreach (var d in cell.ShipRef.Decks) d.CurrentHp = 0;
                            RevealShip(owner.Board, cell.ShipRef, null);
                            HandleShipDeath(game, owner, cell.ShipRef);
                            game.GameLog.Add($"{cell.ShipRef.Name} сгорел от взрыва {ship.Name}!");
                        }
                    }
                }
                // Also kill summons in explosion area
                if (cell?.SummonRef != null && cell.SummonRef.IsAlive)
                {
                    var explodedSummon = cell.SummonRef;

                    // Scout: trigger deferred reveal on death by explosion
                    if (explodedSummon.Type == SummonType.Scout && explodedSummon.ScoutRevealData.Count > 0)
                    {
                        var scoutOwner = game.GetPlayer(explodedSummon.OwnerId);
                        var scoutTarget = game.GetOpponent(explodedSummon.OwnerId);
                        if (scoutTarget != null)
                        {
                            foreach (var (sr, sc) in explodedSummon.ScoutRevealData)
                            {
                                var revealCell = scoutTarget.Board.GetCell(sr, sc);
                                if (revealCell != null && !revealCell.IsRevealed)
                                {
                                    revealCell.IsRevealed = true;
                                    IncrementRevealedCount(scoutTarget);
                                }
                            }
                            explodedSummon.ScoutRevealData.Clear();
                        }
                    }

                    explodedSummon.IsAlive = false;
                    cell.SummonRef = null;
                    game.GameLog.Add($"Призванное существо сгорело от взрыва {ship.Name}!");
                }
            }
        }
    }

    // ── Win Condition ────────────────────────────────────────────────

    public static (bool gameOver, string winnerId) CheckWinCondition(BattleshipGame game)
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

        var p1NoAmmo = CheckNoUsableAmmo(p1);
        var p2NoAmmo = CheckNoUsableAmmo(p2);

        if (p1NoAmmo && !p1AllDestroyed) return (true, p2.DiscordId);
        if (p2NoAmmo && !p2AllDestroyed) return (true, p1.DiscordId);

        // Complex loss: no Mid/Close/CloseMelee ships left AND artillery ammo < opponent's remaining decks
        if (CheckArtilleryLoss(p1, p2)) return (true, p2.DiscordId);
        if (CheckArtilleryLoss(p2, p1)) return (true, p1.DiscordId);

        return (false, null);
    }

    /// <summary>
    /// Check complex loss: player has no Mid/Close/CloseMelee ships, and remaining
    /// artillery (Tetra/Far) ammo is less than opponent's total alive deck count.
    /// </summary>
    private static bool CheckArtilleryLoss(BattleshipPlayer player, BattleshipPlayer opponent)
    {
        if (HasAliveMidOrCloseShips(player)) return false;

        var artilleryAmmo = 0;
        foreach (var ship in player.Board.PlacedShips)
        {
            if (ship.IsDestroyed || ship.Range is not (RangeClass.Tetra or RangeClass.Far)) continue;
            foreach (var weapon in ship.Weapons)
            {
                if (IsShootableWeapon(weapon) && weapon.HasAmmo) artilleryAmmo += weapon.Ammo;
            }
        }

        var opponentDecks = 0;
        foreach (var ship in opponent.Board.PlacedShips)
        {
            if (ship.IsDestroyed) continue;
            opponentDecks += ship.Decks.Count(d => !d.IsDestroyed);
        }

        return artilleryAmmo < opponentDecks;
    }

    private static bool IsShootableWeapon(Weapon weapon)
    {
        return weapon.Type is not (WeaponType.Mast or WeaponType.Boiler);
    }

    private static bool CheckNoUsableAmmo(BattleshipPlayer player)
    {
        foreach (var ship in player.Board.PlacedShips)
        {
            if (ship.IsDestroyed) continue;
            foreach (var weapon in ship.Weapons)
            {
                if (IsShootableWeapon(weapon) && weapon.HasAmmo) return false;
            }
        }
        return true;
    }

    // ── First Turn Calculator ────────────────────────────────────────

    public static string DetermineFirstTurn(BattleshipPlayer p1, BattleshipPlayer p2)
    {
        // 1. More unspent coins → goes first
        if (p1.CoinsRemaining != p2.CoinsRemaining)
            return p1.CoinsRemaining > p2.CoinsRemaining ? p1.DiscordId : p2.DiscordId;

        // 2. Fewer upgraded ships → goes first
        var p1Upgraded = p1.Fleet.Count(s => s.Cost > 0);
        var p2Upgraded = p2.Fleet.Count(s => s.Cost > 0);
        if (p1Upgraded != p2Upgraded)
            return p1Upgraded < p2Upgraded ? p1.DiscordId : p2.DiscordId;

        // 3. Fewer "домашний" ships → goes first
        var p1Home = p1.Fleet.Count(s => s.IsHome);
        var p2Home = p2.Fleet.Count(s => s.IsHome);
        if (p1Home != p2Home)
            return p1Home < p2Home ? p1.DiscordId : p2.DiscordId;

        // 4. Random
        return Rng.Next(2) == 0 ? p1.DiscordId : p2.DiscordId;
    }

    // ── Boarding ─────────────────────────────────────────────────────

    public static bool CheckBoardingTrigger(BattleshipPlayer player)
    {
        var midShips = player.Board.PlacedShips.Where(s => s.Range == RangeClass.Mid && !s.IsSummon).ToList();
        if (midShips.Count == 0) return false;
        return midShips.All(s => s.IsDestroyed);
    }

    public static void TriggerBoarding(BattleshipGame game, BattleshipPlayer player)
    {
        game.Phase = BsGamePhase.Boarding;

        // Desiccator auto-win check
        var p1HasDesiccator = game.Player1.Board.PlacedShips.Any(s => s.Abilities.Contains("auto_win_boarding") && !s.IsDestroyed);
        var p2HasDesiccator = game.Player2.Board.PlacedShips.Any(s => s.Abilities.Contains("auto_win_boarding") && !s.IsDestroyed);

        if (p1HasDesiccator && !p2HasDesiccator)
        {
            game.IsFinished = true;
            game.WinnerId = game.Player1.DiscordId;
            game.Phase = BsGamePhase.GameOver;
            game.GameLog.Add("Иссушитель обеспечил автоматическую победу в абордаже!");
            return;
        }
        if (p2HasDesiccator && !p1HasDesiccator)
        {
            game.IsFinished = true;
            game.WinnerId = game.Player2.DiscordId;
            game.Phase = BsGamePhase.GameOver;
            game.GameLog.Add("Иссушитель обеспечил автоматическую победу в абордаже!");
            return;
        }
        // If both have Desiccator — ALL passives disabled (nimble, ballista_immune, auto_win)
        DisableDualDesiccators(game);

        // Convert close-range ships to pending boarding deploys (Bug #5: player places manually)
        foreach (var ship in player.Board.PlacedShips)
        {
            if (ship.IsDestroyed || ship.IsSummon) continue;
            if (ship.Range is RangeClass.Close or RangeClass.CloseMelee)
            {
                ship.IsSummon = true; // Mark as no longer active ship
                player.PendingSummons.Add(new PendingSummonDeploy
                {
                    Type = SummonType.Ram,
                    Speed = ship.Speed,
                    Damage = 4,
                    RevealRadius = ship.Space,
                    IsBoarding = true,
                    SourceShipName = ship.Name
                });
                game.GameLog.Add($"{ship.Name} готов к абордажу! Разместите на первой строчке вражеского поля.");
            }
        }

        // Triple crew upgrade: spawn free pirate boat (both players)
        foreach (var p in game.GetPlayers())
        {
            foreach (var ship in p.Board.PlacedShips)
            {
                if (!ship.IsDestroyed && ship.Abilities.Contains("spawn_pirate_boat") && ship.DefinitionId == "triple")
                {
                    var pirate = new Summon
                    {
                        Type = SummonType.PirateBoat,
                        Row = 0, Col = ship.Col,
                        Speed = 1, OwnerId = p.DiscordId,
                        SpawnedAtShot = game.ShotCount
                    };
                    p.Summons.Add(pirate);
                    game.GameLog.Add("Экипаж Тройки выпустил пиратский корабль!");
                }
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
                    if (tetraWeapon != null)
                    {
                        tetraWeapon.Ammo += 2;
                        game.GameLog.Add($"Доп. снаряды: +2 белых камня для {ship.Name}!");
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
                foreach (var w in ship.Weapons.Where(w => w.Type == WeaponType.Tetracatapult))
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
                foreach (var w in ship.Weapons.Where(w => w.Type == WeaponType.Incendiary))
                {
                    w.Ammo += 1;
                }
            }
        }
    }

    /// <summary>
    /// If both players have a Desiccator, disable all its passives (nimble, ballista_immune, auto_win_boarding).
    /// </summary>
    public static void DisableDualDesiccators(BattleshipGame game)
    {
        var p1HasDesiccator = game.Player1.Board.PlacedShips.Any(s => s.DefinitionId == "desiccator" && !s.IsDestroyed);
        var p2HasDesiccator = game.Player2.Board.PlacedShips.Any(s => s.DefinitionId == "desiccator" && !s.IsDestroyed);

        if (!p1HasDesiccator || !p2HasDesiccator) return;

        foreach (var p in game.GetPlayers())
        {
            foreach (var ship in p.Board.PlacedShips.Where(s => s.DefinitionId == "desiccator" && !s.IsDestroyed))
            {
                ship.Abilities.Remove("nimble");
                ship.Abilities.Remove("ballista_immune");
                ship.Abilities.Remove("auto_win_boarding");
            }
        }
        game.GameLog.Add("Два Иссушителя в игре — все пассивки Иссушителей отключены!");
    }

    // ── Summon Movement ──────────────────────────────────────────────

    public static void MoveSummons(BattleshipGame game)
    {
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
                        // Scout: reveal accumulated data on out-of-bounds
                        if (summon.Type == SummonType.Scout && summon.ScoutRevealData.Count > 0)
                        {
                            foreach (var (sr, sc) in summon.ScoutRevealData)
                            {
                                var revealCell = opponent.Board.GetCell(sr, sc);
                                if (revealCell != null && !revealCell.IsRevealed)
                                {
                                    revealCell.IsRevealed = true;
                                    IncrementRevealedCount(opponent);
                                }
                            }
                            summon.ScoutRevealData.Clear();
                        }

                        summon.WaitingForTurnBack = true;
                        break;
                    }

                    // Check collision with opponent's ships
                    var targetCell = opponent.Board.GetCell(newRow, newCol);
                    if (targetCell?.ShipRef != null && !targetCell.ShipRef.IsDestroyed)
                    {
                        // Check Drakkar freeze aura
                        if (IsInFreezeZone(opponent, newRow, newCol))
                        {
                            summon.IsAlive = false;
                            game.GameLog.Add($"Призванное существо заморожено аурой Драккара!");
                            // Mast warning
                            if (player.Board.PlacedShips.Any(s => s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed) && !s.IsDestroyed))
                                game.GameLog.Add("[Мачта] Наших заморозили!");
                            break;
                        }

                        HandleSummonCollision(game, summon, targetCell.ShipRef, opponent);
                        // CursedBoat, Brander, and boarding ships that devastated 1-2 deckers continue
                        if (summon.Type == SummonType.CursedBoat)
                        {
                            // CursedBoat applies Devastated, then waits for owner to choose direction
                            summon.Row = newRow;
                            summon.Col = newCol;
                            summon.WaitingForDirectionChoice = true;
                            break;
                        }
                        else if (summon.Type == SummonType.Brander)
                        {
                            // Brander passes through (урон от тарана = 0), continues
                            summon.Row = newRow;
                            summon.Col = newCol;
                            continue;
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

                    // Freeze zone check for empty cells (Drakkar aura)
                    if (IsInFreezeZone(opponent, newRow, newCol))
                    {
                        summon.ScoutRevealData?.Clear(); // GDD: frozen scout doesn't transmit
                        summon.IsAlive = false;
                        game.GameLog.Add($"Призванное существо заморожено аурой Драккара!");
                        if (player.Board.PlacedShips.Any(s => s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed) && !s.IsDestroyed))
                            game.GameLog.Add("[Мачта] Наших заморозили!");
                        break;
                    }

                    // Burning cell (Greek Fire): kill summon that enters it
                    var movingCell = opponent.Board.GetCell(newRow, newCol);
                    if (movingCell != null && movingCell.IsBurning)
                    {
                        summon.IsAlive = false;
                        game.GameLog.Add($"Призванное существо сгорело в огне! ({(char)('A' + newCol)}{newRow + 1})");
                        break;
                    }

                    // Ram/PirateBoat: reveal cells they pass through
                    if (summon.Type is SummonType.Ram or SummonType.PirateBoat)
                    {
                        var passCell = opponent.Board.GetCell(newRow, newCol);
                        if (passCell != null && !passCell.IsRevealed)
                        {
                            passCell.IsRevealed = true;
                            IncrementRevealedCount(opponent);
                        }
                    }

                    // Boarding ships: reveal surrounding cells (radius = RevealRadius from ship's Space)
                    if (summon.IsBoardingShip)
                    {
                        RevealArea(opponent.Board, newRow, newCol, summon.RevealRadius, opponent);
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
                                if (game.PoisonZones.Contains((sr, sc))) continue;
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
            player.Summons.RemoveAll(s => !s.IsAlive && !s.WaitingForTurnBack);
        }

        // Process freeze auras after movement
        ProcessFreezeAuras(game);

        // Process poison cones after movement
        ProcessPoisonCones(game);
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

    private static void HandleSummonCollision(BattleshipGame game, Summon summon, Ship targetShip, BattleshipPlayer targetOwner)
    {
        var attacker = game.GetOpponent(targetOwner.DiscordId);
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
                        HandleShipDeath(game, targetOwner, targetShip);
                        game.GameLog.Add($"Абордажный корабль опустошил {targetShip.Name}!");
                        summon.IsAlive = true; // continue moving (don't die)
                    }
                    else
                    {
                        var aliveDeckB = targetShip.Decks.FirstOrDefault(d => !d.IsDestroyed);
                        if (aliveDeckB != null)
                        {
                            aliveDeckB.CurrentHp -= 4;
                            if (aliveDeckB.CurrentHp < 0) aliveDeckB.CurrentHp = 0;
                            game.GameLog.Add($"Абордажный корабль протаранил {targetShip.Name}! (-4 HP)");
                            if (targetShip.IsDestroyed)
                            {
                                RevealShip(targetOwner.Board, targetShip, attacker);
                                HandleShipDeath(game, targetOwner, targetShip);
                            }
                        }
                        summon.IsAlive = false; // Boarding ship dies against 3-4 deckers
                    }
                    break;
                }

                // Regular ram: 4 damage, dies on collision
                var aliveDeck = targetShip.Decks.FirstOrDefault(d => !d.IsDestroyed);
                if (aliveDeck != null)
                {
                    aliveDeck.CurrentHp -= 4;
                    if (aliveDeck.CurrentHp < 0) aliveDeck.CurrentHp = 0;
                    game.GameLog.Add($"Таран врезался в {targetShip.Name}! (-4 HP)");
                    if (targetShip.IsDestroyed)
                    {
                        RevealShip(targetOwner.Board, targetShip, attacker);
                        HandleShipDeath(game, targetOwner, targetShip);
                    }
                }
                break;

            case SummonType.PirateBoat:
                if (targetShip.Decks.Count <= 2)
                {
                    if (targetShip.Statuses.Contains(ShipStatusType.Capture))
                    {
                        // Recapture — remove enemy Capture status (restores ownership)
                        targetShip.Statuses.Remove(ShipStatusType.Capture);
                        game.GameLog.Add($"Пиратский корабль отбил {targetShip.Name} обратно!");
                    }
                    else
                    {
                        // Capture ship — reveal Space-radius around it (GDD: capture reveals like destroy)
                        targetShip.Statuses.Add(ShipStatusType.Capture);
                        RevealShip(targetOwner.Board, targetShip, attacker);
                        game.GameLog.Add($"Пиратский корабль захватил {targetShip.Name}!");
                    }
                }
                else
                {
                    game.GameLog.Add($"Пиратский корабль разбился о {targetShip.Name}!");
                }
                break;

            case SummonType.CursedBoat:
                // CursedBoat doesn't die on collision, applies Devastated
                targetShip.Statuses.Add(ShipStatusType.Devastated);
                foreach (var d in targetShip.Decks) d.CurrentHp = 0;
                RevealShip(targetOwner.Board, targetShip, attacker);
                HandleShipDeath(game, targetOwner, targetShip);
                game.GameLog.Add($"Проклятый корабль опустошил {targetShip.Name}!");
                break;

            case SummonType.Scout:
                // Reveal accumulated data on collision/death
                var summonOwner = game.GetPlayer(summon.OwnerId);
                if (summon.ScoutRevealData.Count > 0)
                {
                    foreach (var (sr, sc) in summon.ScoutRevealData)
                    {
                        var cell = targetOwner.Board.GetCell(sr, sc);
                        if (cell != null && !cell.IsRevealed)
                        {
                            cell.IsRevealed = true;
                            IncrementRevealedCount(targetOwner);
                        }
                    }
                }
                RevealArea(targetOwner.Board, summon.Row, summon.Col, summon.RevealRadius, targetOwner);
                game.GameLog.Add($"Разведчик обнаружил корабли противника!");
                break;

            case SummonType.Brander:
                // Brander does NOT auto-detonate on collision — passes through (урон от тарана = 0)
                // Owner must shoot Brander's cell to detonate it
                summon.IsAlive = true; // don't die on collision
                game.GameLog.Add($"Брандер проплывает мимо {targetShip.Name}.");
                break;
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
                            summon.ScoutRevealData?.Clear(); // GDD: frozen scout does NOT transmit
                            summon.IsAlive = false;
                            game.GameLog.Add($"Аура Драккара заморозила призванное существо!");
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
        // Collect all poison cone cells first to avoid modification during iteration
        var poisonZones = new HashSet<(int row, int col)>();

        foreach (var player in game.GetPlayers())
        {
            foreach (var ship in player.Board.PlacedShips)
            {
                if (ship.IsDestroyed || ship.IsSummon || !ship.Abilities.Contains("poison_cone") || game.Phase == BsGamePhase.Boarding) continue;

                var coneCells = GetPoisonConeCells(ship);
                var opponent = game.GetOpponent(player.DiscordId);

                foreach (var (cr, cc) in coneCells)
                {
                    poisonZones.Add((cr, cc));

                    // Kill enemy summons in cone
                    if (opponent != null)
                    {
                        foreach (var summon in opponent.Summons.Where(s => s.IsAlive && s.Row == cr && s.Col == cc).ToList())
                        {
                            TransmitScoutReveal(game, summon);
                            summon.IsAlive = false;
                            game.GameLog.Add($"Ядовитый конус {ship.Name} убил призванное существо!");
                        }
                    }

                    // Kill allied summons in cone
                    foreach (var summon in player.Summons.Where(s => s.IsAlive && s.Row == cr && s.Col == cc).ToList())
                    {
                        TransmitScoutReveal(game, summon);
                        summon.IsAlive = false;
                        game.GameLog.Add($"Ядовитый конус {ship.Name} убил союзное существо!");
                    }

                    // Kill enemy ships in cone
                    if (opponent != null)
                    {
                        var enemyCell = opponent.Board.GetCell(cr, cc);
                        if (enemyCell?.ShipRef != null && !enemyCell.ShipRef.IsDestroyed)
                        {
                            var enemyShip = enemyCell.ShipRef;
                            foreach (var d in enemyShip.Decks) d.CurrentHp = 0;
                            RevealShip(opponent.Board, enemyShip, player);
                            HandleShipDeath(game, opponent, enemyShip);
                            game.GameLog.Add($"Ядовитый конус {ship.Name} уничтожил {enemyShip.Name}!");
                        }
                    }

                    // Kill allied ships in cone
                    var allyCell = player.Board.GetCell(cr, cc);
                    if (allyCell?.ShipRef != null && !allyCell.ShipRef.IsDestroyed && allyCell.ShipRef.Id != ship.Id)
                    {
                        var allyShip = allyCell.ShipRef;
                        foreach (var d in allyShip.Decks) d.CurrentHp = 0;
                        RevealShip(player.Board, allyShip, opponent);
                        HandleShipDeath(game, player, allyShip);
                        game.GameLog.Add($"Ядовитый конус {ship.Name} уничтожил союзный {allyShip.Name}!");
                    }
                }
            }
        }

        // Store poison zones for scout blocking (Fix #8)
        game.PoisonZones = poisonZones;
    }

    private static List<(int row, int col)> GetPoisonConeCells(Ship ship)
    {
        var cells = new List<(int, int)>();
        // V-pattern: 2 rows for Alchi-Barge, 3 rows for Alchi-Iceberg
        var baseRow = ship.Row;
        var baseCol = ship.Col;

        // Row 1: directly ahead and 1 to each side (3 cells)
        cells.Add((baseRow - 1, baseCol - 1));
        cells.Add((baseRow - 1, baseCol));
        cells.Add((baseRow - 1, baseCol + 1));
        // Row 2: wider spread (5 cells)
        cells.Add((baseRow - 2, baseCol - 2));
        cells.Add((baseRow - 2, baseCol - 1));
        cells.Add((baseRow - 2, baseCol));
        cells.Add((baseRow - 2, baseCol + 1));
        cells.Add((baseRow - 2, baseCol + 2));
        // Row 3: Alchi-Iceberg only — even wider spread (7 cells)
        if (ship.DefinitionId == "alchi_iceberg")
        {
            for (var dc = -3; dc <= 3; dc++)
                cells.Add((baseRow - 3, baseCol + dc));
        }

        // Filter valid cells
        return cells.Where(c => c.Item1 >= 0 && c.Item1 < 10 && c.Item2 >= 0 && c.Item2 < 10).ToList();
    }

    /// <summary>
    /// Transmit deferred scout reveal data when a scout dies (poison, freeze, etc.).
    /// </summary>
    private static void TransmitScoutReveal(BattleshipGame game, Summon summon)
    {
        if (summon.Type != SummonType.Scout || summon.ScoutRevealData.Count == 0) return;
        var opponent = game.GetOpponent(summon.OwnerId);
        if (opponent == null) return;
        foreach (var (sr, sc) in summon.ScoutRevealData)
        {
            var revealCell = opponent.Board.GetCell(sr, sc);
            if (revealCell != null && !revealCell.IsRevealed)
            {
                revealCell.IsRevealed = true;
                IncrementRevealedCount(opponent);
            }
        }
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

        if (TryMoveShip(shipOwner, ship, dodgeDir) || TryMoveShip(shipOwner, ship, -dodgeDir))
        {
            game.GameLog.Add($"{ship.Name} увернулся от выстрела! ({(char)('A' + col)}{row + 1})");
            // The cell is now empty — mark as miss
            var cell = shipOwner.Board.GetCell(row, col);
            if (cell != null)
            {
                cell.IsMiss = true;
                cell.IsHit = false;
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
                    HandleShipDeath(game, shipOwner, ship);
                    game.GameLog.Add($"{ship.Name} заплыл в огонь при уклонении!");
                    break;
                }
            }
            if (!ship.IsDestroyed && game.PoisonZones != null)
            {
                foreach (var (nr, nc) in ship.GetOccupiedCells())
                {
                    if (game.PoisonZones.Contains((nr, nc)))
                    {
                        foreach (var d in ship.Decks) d.CurrentHp = 0;
                        RevealShip(shipOwner.Board, ship, null);
                        HandleShipDeath(game, shipOwner, ship);
                        game.GameLog.Add($"{ship.Name} заплыл в ядовитый конус при уклонении!");
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

        // Remove from current position
        foreach (var (r, c) in ship.GetOccupiedCells())
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
    /// Generate Mast warnings when opponent deploys summon.
    /// </summary>
    public static string GenerateMastWarning(BattleshipPlayer player, SummonType summonType)
    {
        var hasMast = player.Board.PlacedShips.Any(s =>
            !s.IsDestroyed && s.Decks.Any(d => d.Module == "mast" && !d.ModuleDestroyed));

        if (!hasMast) return null;

        return summonType switch
        {
            SummonType.Ram => "[Мачта] Это таран!",
            SummonType.PirateBoat => "[Мачта] На нас надвигаются пираты!",
            SummonType.Scout => "[Мачта] Вражеский разведчик на горизонте!",
            SummonType.Brander => "[Мачта] Брандер приближается!",
            SummonType.CursedBoat => "[Мачта] Проклятый корабль на горизонте!",
            _ => null
        };
    }

    // ── Burn Status Processing ────────────────────────────────────────

    private static void BurnArea(BattleshipGame game, BattleshipPlayer owner, int centerRow, int centerCol, int radius)
    {
        for (var dr = -radius; dr <= radius; dr++)
        for (var dc = -radius; dc <= radius; dc++)
        {
            var cell = owner.Board.GetCell(centerRow + dr, centerCol + dc);
            if (cell?.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                if (!cell.ShipRef.Statuses.Contains(ShipStatusType.BurnResist))
                {
                    cell.IsBurning = true;
                    foreach (var d in cell.ShipRef.Decks) d.CurrentHp = 0;
                    cell.ShipRef.Statuses.Add(ShipStatusType.Burn);
                    game.GameLog.Add($"{cell.ShipRef.Name} сгорел!");
                }
            }
        }
    }

    public static void ProcessBurnStatus(BattleshipGame game)
    {
        foreach (var player in game.GetPlayers())
        {
            foreach (var ship in player.Board.PlacedShips)
            {
                if (ship.Statuses.Contains(ShipStatusType.Burn) && !ship.Statuses.Contains(ShipStatusType.BurnResist))
                {
                    foreach (var d in ship.Decks)
                    {
                        if (!d.IsDestroyed)
                            d.CurrentHp = 0;
                    }
                    game.GameLog.Add($"{ship.Name} сгорел дотла!");
                }
            }
        }
    }
}
