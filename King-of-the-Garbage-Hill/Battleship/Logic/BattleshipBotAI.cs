using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Bot AI for all game phases: fleet selection with upgrades, strategic ship placement,
/// combat with weapon selection and probability-based targeting, summon deployment.
/// </summary>
public static class BattleshipBotAI
{
    private static readonly Random Rng = new();

    // ── Fleet Selection ──────────────────────────────────────────────

    /// <summary>
    /// Randomly selects ships within same constraints as player (budget, regions, template).
    /// Uses BuildFleetFromSelections to fill defaults.
    /// </summary>
    public static List<FleetSelection> SelectFleet(Faction faction = Faction.Empire)
    {
        var budget = FleetValidator.GetBudget(faction);
        var regions = new HashSet<Region>();
        var purchases = new List<FleetSelection>();

        // Track purchased slots per deck-count
        var slotsUsed = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 } };

        // Shuffle non-free candidates
        var candidates = ShipCatalog.AllShips
            .Where(s => !s.IsFree && s.Id != "desiccator" && s.Factions.Contains(faction))
            .OrderBy(_ => Rng.Next())
            .ToList();

        foreach (var def in candidates)
        {
            if (def.Cost > budget) continue;
            if (!CanAddRegion(def, regions)) continue;
            var maxSlots = FleetValidator.Template.GetValueOrDefault(def.DeckCount, 0);
            if (slotsUsed[def.DeckCount] >= maxSlots) continue;

            purchases.Add(new FleetSelection
            {
                DefinitionId = def.Id,
                ShipName = def.Name,
                Cost = def.Cost
            });
            budget -= def.Cost;
            AddRegions(def, regions);
            slotsUsed[def.DeckCount]++;
            if (budget <= 0) break;
        }

        // Apply upgrades with remaining budget
        var archetype = Rng.Next(6);
        ApplyUpgrades(purchases, ref budget, archetype);

        // Build full fleet (fills defaults for empty slots)
        return FleetValidator.BuildFleetFromSelections(purchases, faction);
    }

    private static bool CanAddRegion(ShipDefinition def, HashSet<Region> regions)
    {
        var newRegions = def.Regions.Where(r => r != Region.Tetracor).ToList();
        var combined = new HashSet<Region>(regions);
        foreach (var r in newRegions) combined.Add(r);
        return combined.Count <= FleetValidator.MaxRegions;
    }

    private static void AddRegions(ShipDefinition def, HashSet<Region> regions)
    {
        foreach (var r in def.Regions)
            if (r != Region.Tetracor)
                regions.Add(r);
    }

    private static void ApplyUpgrades(List<FleetSelection> fleet, ref int budget, int archetype)
    {
        // Tetranavis boiler upgrade (Greek Fire or Brander) — add entry if not present
        var tetra = fleet.FirstOrDefault(f => f.DefinitionId == "tetranavis");
        if (tetra == null && budget >= 4)
        {
            tetra = new FleetSelection { DefinitionId = "tetranavis", ShipName = "Tetranavis", Cost = 0 };
            fleet.Add(tetra);
        }
        if (tetra != null && budget >= 4)
        {
            tetra.Upgrades ??= new List<string>();
            // Burn archetype prefers Greek Fire; others 50/50
            var useGreekFire = archetype == 1 || Rng.Next(2) == 0;
            var upgradeId = useGreekFire ? "tetra_boiler_fire" : "tetra_boiler_brander";
            tetra.Upgrades.Add(upgradeId);
            tetra.Cost += 4;
            budget -= 4;
        }

        // Triple upgrades — add entry if not present
        var triple = fleet.FirstOrDefault(f => f.DefinitionId == "triple");
        if (triple == null && budget >= 2)
        {
            triple = new FleetSelection { DefinitionId = "triple", ShipName = "Triple", Cost = 0 };
            fleet.Add(triple);
        }
        if (triple != null && budget >= 2)
        {
            triple.Upgrades ??= new List<string>();

            // Crew upgrade (2 coins) — always good value
            if (budget >= 2 && Rng.Next(3) > 0)
            {
                triple.Upgrades.Add("triple_crew");
                triple.Cost += 2;
                budget -= 2;
            }

            // Extra ammo (4 coins) — good for late game
            if (budget >= 4 && Rng.Next(3) > 0)
            {
                triple.Upgrades.Add("triple_ammo");
                triple.Cost += 4;
                budget -= 4;
            }

            // Armor upgrades (4 coins each) — if lots of budget left
            for (var i = 0; i < 3 && budget >= 4; i++)
            {
                if (Rng.Next(3) == 0)
                {
                    var armorId = $"triple_armor_{i + 1}";
                    triple.Upgrades.Add(armorId);
                    triple.Cost += 4;
                    budget -= 4;
                }
            }
        }
    }

    // ── Ship Placement ───────────────────────────────────────────────

    /// <summary>
    /// Places ships with strategic spread. Far/Tetra in back rows, others distributed across rows 0-7.
    /// Tries to avoid clustering by preferring different rows/columns.
    /// </summary>
    public static void PlaceFleet(BattleshipPlayer bot)
    {
        // Place poison sources before the ships that must avoid their cones, then keep the
        // existing largest/Far-first strategy for the rest of the fleet.
        var sortedFleet = bot.Fleet
            .OrderByDescending(s => s.Abilities.Contains("poison_cone") ? 1000 : 0)
            .ThenByDescending(s => s.Range is RangeClass.Far or RangeClass.Tetra ? 100 : 0)
            .ThenByDescending(s => s.Decks.Count)
            .ToList();

        foreach (var ship in sortedFleet)
        {
            var placed = false;

            // Generate candidate positions and score them
            var candidates = new List<(int row, int col, Orientation orient, int score)>();

            for (var r = 0; r < 10; r++)
            for (var c = 0; c < 10; c++)
            foreach (var o in Enum.GetValues<Orientation>())
            {
                var (valid, _) = PlacementValidator.ValidatePlacement(bot.Board, ship, r, c, o);
                if (!valid || !IsPoisonSafePlacement(bot.Board, ship, r, c, o)) continue;

                var score = ScorePlacement(bot, ship, r, c, o);
                candidates.Add((r, c, o, score));
            }

            if (candidates.Count > 0)
            {
                // Weighted random from top candidates for variety
                candidates.Sort((a, b) => b.score.CompareTo(a.score));
                var topCount = Math.Min(5, candidates.Count);
                var pick = candidates[Rng.Next(topCount)];
                PlaceShipOnBoard(bot, ship, pick.row, pick.col, pick.orient);
                placed = true;
            }

            if (!placed)
            {
                // Fallback: first valid position
                for (var r = 0; r < 10 && !placed; r++)
                for (var c = 0; c < 10 && !placed; c++)
                foreach (var o in Enum.GetValues<Orientation>())
                {
                    var (valid, _) = PlacementValidator.ValidatePlacement(bot.Board, ship, r, c, o);
                    if (valid && IsPoisonSafePlacement(bot.Board, ship, r, c, o))
                    {
                        PlaceShipOnBoard(bot, ship, r, c, o);
                        placed = true;
                    }
                }
            }
        }
    }

    private static bool IsPoisonSafePlacement(
        Board board,
        Ship candidate,
        int row,
        int col,
        Orientation orientation)
    {
        var candidateCells = candidate.GetOccupiedCells(row, col, orientation).ToHashSet();

        // A newly placed allied ship may not occupy any cell in an existing poison cone.
        foreach (var poisonShip in board.PlacedShips.Where(s =>
                     !s.IsDestroyed && s.Abilities.Contains("poison_cone")))
        {
            if (BattleshipGameEngine
                .GetPoisonConeCells(poisonShip, poisonShip.Row, poisonShip.Col, poisonShip.Orientation)
                .Any(candidateCells.Contains))
                return false;
        }

        if (!candidate.Abilities.Contains("poison_cone")) return true;

        // Conversely, the new poison source may not aim its cone through an already placed deck.
        var candidateCone = BattleshipGameEngine
            .GetPoisonConeCells(candidate, row, col, orientation)
            .ToHashSet();
        return board.PlacedShips
            .SelectMany(ship => ship.GetOccupiedCells())
            .All(cell => !candidateCone.Contains(cell));
    }

    private static int ScorePlacement(BattleshipPlayer bot, Ship ship, int row, int col, Orientation orient)
    {
        var score = 0;

        // Far/Tetra ships should be on rows 8-9
        if (ship.Range is RangeClass.Far or RangeClass.Tetra)
        {
            if (row >= 8) score += 20;
            else score -= 20;
        }
        else
        {
            // Other ships prefer rows 0-7
            if (row >= 8) score -= 30;
        }

        // Prefer edges of the board for small ships (harder to find)
        if (ship.Decks.Count <= 2)
        {
            if (row == 0 || row >= 7) score += 5;
            if (col == 0 || col >= 9) score += 5;
        }

        // Spread: penalty for being near already-placed ships
        foreach (var placed in bot.Board.PlacedShips)
        {
            var placedCells = placed.GetOccupiedCells();
            var myRow = row;
            var myCol = col;
            foreach (var (pr, pc) in placedCells)
            {
                var dist = Math.Abs(pr - myRow) + Math.Abs(pc - myCol);
                if (dist <= 2) score -= 5;
                else if (dist <= 4) score -= 2;
            }
        }

        // Randomness for variety
        score += Rng.Next(8);

        return score;
    }

    private static void PlaceShipOnBoard(BattleshipPlayer player, Ship ship, int row, int col, Orientation orientation)
    {
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
    }

    // ── Weapon Selection ─────────────────────────────────────────────

    /// <summary>
    /// Selects the best weapon/shot type for the current situation.
    /// Returns (weaponType, shotType) strings matching the SelectWeapon API.
    /// </summary>
    public static (string weaponType, string shotType) ChooseWeapon(
        BattleshipGame game, BattleshipPlayer bot, BattleshipPlayer opponent, BsGamePhase phase)
    {
        var hasTarget = FindTargetCells(opponent.Board).Count > 0;

        // CAPTURE fixes the target, not the ammunition. Prefer a decisive operational
        // projectile, but fall back through every server-legal source rather than passing
        // merely because Ballista is unavailable.
        var hasCaptured = bot.Board.PlacedShips
            .Any(s => s.Statuses.Contains(ShipStatusType.Capture) && !s.IsDestroyed);
        if (hasCaptured)
        {
            var captureWeapon = BattleshipGameEngine.GetUsableWeapons(game, bot)
                .Where(value => value.weapon.AimSpeed <= bot.RevealedCellCount)
                .OrderBy(value => value.weapon.Type switch
                {
                    WeaponType.EvilIncendiary => 0,
                    WeaponType.Incendiary => 1,
                    WeaponType.Tetracatapult => 2,
                    WeaponType.Ballista => 3,
                    WeaponType.EvilGreekFire => 4,
                    WeaponType.GreekFire => 5,
                    _ => 6,
                })
                .Select(value => value.weapon)
                .FirstOrDefault();
            if (captureWeapon != null)
            {
                var captureShot = captureWeapon.Type == WeaponType.Tetracatapult
                    ? captureWeapon.ConfiguredShotType ?? ShotType.WhiteStone
                    : captureWeapon.Type switch
                    {
                        WeaponType.EvilIncendiary => ShotType.EvilIncendiary,
                        WeaponType.Incendiary => ShotType.Incendiary,
                        WeaponType.EvilGreekFire => ShotType.EvilGreekFire,
                        WeaponType.GreekFire => ShotType.GreekFire,
                        _ => ShotType.Ballista,
                    };
                return (captureWeapon.Type.ToString(), captureShot.ToString());
            }
        }

        var revealedByBot = bot.RevealedCellCount;

        // Greek Fire variants are own-board-only, so select one only for a summon physically
        // present there.
        var hasEnemySummonOnOwnBoard = bot.Board.Grid.Cast<Cell>().Any(c =>
            c.SummonRef is { IsAlive: true } summon && summon.OwnerId != bot.DiscordId);
        if (hasEnemySummonOnOwnBoard)
        {
            foreach (var type in new[] { WeaponType.EvilGreekFire, WeaponType.GreekFire })
            {
                if (FindWeapon(bot, type, revealedByBot) != null)
                    return (type.ToString(), type.ToString());
            }
        }

        // Incendiary variants: burn a ship if we know where one is (have hit but not sunk).
        if (hasTarget)
        {
            foreach (var type in new[] { WeaponType.EvilIncendiary, WeaponType.Incendiary })
            {
                if (FindWeapon(bot, type, revealedByBot) != null && Rng.Next(3) > 0)
                    return (type.ToString(), type.ToString());
            }
        }

        // Tetracatapult: White Stone or Buckshot
        var tetraWeapons = FindWeapons(bot, WeaponType.Tetracatapult, revealedByBot).ToList();
        if (tetraWeapons.Count > 0)
        {
            var configuredTypes = tetraWeapons
                .Where(weapon => weapon.ConfiguredShotType.HasValue)
                .Select(weapon => weapon.ConfiguredShotType.Value)
                .ToHashSet();
            if (configuredTypes.Contains(ShotType.WhiteStone) && hasTarget && Rng.Next(3) > 0)
                return ("Tetracatapult", "WhiteStone");
            if (configuredTypes.Contains(ShotType.Buckshot) && !hasTarget && Rng.Next(2) == 0)
                return ("Tetracatapult", "Buckshot");
        }

        // Default: Ballista (unlimited ammo)
        return ("Ballista", "Ballista");
    }

    private static Weapon FindWeapon(BattleshipPlayer player, WeaponType type, int opponentRevealedCount = 0)
    {
        return FindWeapons(player, type, opponentRevealedCount).FirstOrDefault();
    }

    private static IEnumerable<Weapon> FindWeapons(
        BattleshipPlayer player,
        WeaponType type,
        int opponentRevealedCount = 0)
    {
        foreach (var ship in player.Board.PlacedShips)
        {
            if (ship.IsDestroyed) continue;
            foreach (var weapon in ship.Weapons.Where(weapon =>
                         weapon.Type == type && weapon.HasAmmo &&
                         BattleshipGameEngine.IsWeaponOperational(ship, weapon)))
            {
                // AimSpeed: weapon locked until enough enemy cells revealed
                if (weapon.AimSpeed > 0 && opponentRevealedCount < weapon.AimSpeed)
                    continue;
                yield return weapon;
            }
        }
    }

    // ── Combat AI (Probability-based Hunt/Target) ─────────────────────

    /// <summary>
    /// Chooses a target cell using probability density and orientation-aware targeting.
    /// Handles scratched cells, captured ships, Far range restrictions, and special weapons.
    /// </summary>
    public static (int row, int col) ChooseTarget(
        BattleshipPlayer bot, BattleshipPlayer opponent, ShotType shotType = ShotType.Ballista)
    {
        // Captured ships on the own board remain mandatory targets for any selected ammunition.
        var capturedShips = bot.Board.PlacedShips
            .Where(s => s.Statuses.Contains(ShipStatusType.Capture) && !s.IsDestroyed).ToList();
        if (capturedShips.Count > 0)
        {
            var capturedCells = capturedShips
                .SelectMany(ship =>
                    ship.Decks
                        .Where(deck => deck.CurrentHp > 0)
                        .Select(deck => ship.GetDeckCell(
                            deck,
                            ship.Row,
                            ship.Col,
                            ship.Orientation)))
                .ToList();
            if (capturedCells.Count > 0)
                return capturedCells[Rng.Next(capturedCells.Count)];
        }

        // Compute own summon positions to exclude from targeting (#4)
        var ownSummonCells = new HashSet<(int, int)>();
        foreach (var s in bot.Summons.Where(s => s.IsAlive))
            ownSummonCells.Add((s.Row, s.Col));

        // Determine blocked rows (Far weapon on rows 8-9 can't hit enemy rows 8-9)
        var blockedRows = new HashSet<int>();
        if (bot.SelectedWeapon?.ShipId != null)
        {
            var weaponShip = bot.Board.PlacedShips.Find(s => s.Id == bot.SelectedWeapon.ShipId);
            if (weaponShip != null && weaponShip.Range == RangeClass.Far && weaponShip.Row >= 8)
            {
                blockedRows.Add(8);
                blockedRows.Add(9);
            }
        }

        // Buckshot: use AoE targeting
        if (shotType == ShotType.Buckshot)
            return ChooseBuckshotTarget(opponent.Board, blockedRows);

        // Target mode: if we have hit-but-not-sunk ships, pursue them
        var targetCells = FindTargetCells(opponent.Board);

        // Incendiary special: can re-target already-hit cells with ships still alive
        if (shotType is ShotType.Incendiary or ShotType.EvilIncendiary)
        {
            var incendiaryTargets = FindIncendiaryTargets(opponent.Board);
            incendiaryTargets.RemoveAll(c => ownSummonCells.Contains((c.row, c.col)));
            if (incendiaryTargets.Count > 0)
                return incendiaryTargets[Rng.Next(incendiaryTargets.Count)];
        }

        // Exclude own summons and blocked rows
        targetCells.RemoveAll(c => ownSummonCells.Contains((c.row, c.col)));
        var avoidDodgeCells = shotType == ShotType.Ballista;
        if (avoidDodgeCells)
            targetCells.RemoveAll(c => opponent.Board.GetCell(c.row, c.col)?.WasDodge == true);
        if (blockedRows.Count > 0)
            targetCells.RemoveAll(c => blockedRows.Contains(c.row));

        if (targetCells.Count > 0)
        {
            // Prioritize orientation-aligned targets
            var aligned = GetOrientationAlignedTargets(opponent.Board, targetCells);
            if (aligned.Count > 0)
                return aligned[Rng.Next(aligned.Count)];
            return targetCells[Rng.Next(targetCells.Count)];
        }

        // Hunt mode: probability density map
        return ChooseHuntTarget(opponent.Board, blockedRows, ownSummonCells, avoidDodgeCells);
    }

    /// <summary>
    /// Finds cells adjacent to hit (but not sunk) ships — standard targeting.
    /// Also includes re-targetable scratched cells.
    /// </summary>
    private static List<(int row, int col)> FindTargetCells(Board board)
    {
        var targets = new List<(int, int)>();

        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];

            // Re-targetable scratched cell (armor survived, ship still alive)
            if (cell.WasScratched && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                if (!targets.Contains((r, c)))
                    targets.Add((r, c));
                continue;
            }

            if (!cell.IsHit || cell.ShipRef == null || cell.ShipRef.IsDestroyed) continue;

            // Adjacent unhit cells
            var adjacents = new[] { (r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1) };
            foreach (var (ar, ac) in adjacents)
            {
                var adjCell = board.GetCell(ar, ac);
                if (adjCell != null && !adjCell.IsHit && !adjCell.IsMiss)
                {
                    if (!targets.Contains((ar, ac)))
                        targets.Add((ar, ac));
                }
            }
        }

        return targets;
    }

    /// <summary>
    /// For Incendiary: find already-hit cells where the ship is still alive (burn the whole ship).
    /// </summary>
    private static List<(int row, int col)> FindIncendiaryTargets(Board board)
    {
        var targets = new List<(int, int)>();
        var seenShips = new HashSet<string>();

        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];
            if (cell.ShipRef != null && !cell.ShipRef.IsDestroyed &&
                !cell.ShipRef.Statuses.Contains(ShipStatusType.BurnResist) &&
                cell.IsHit && !seenShips.Contains(cell.ShipRef.Id))
            {
                targets.Add((r, c));
                seenShips.Add(cell.ShipRef.Id);
            }
        }

        return targets;
    }

    /// <summary>
    /// Filters target cells to prefer those aligned with detected ship orientation.
    /// If two hits on the same ship are in the same row, prefer horizontal continuation.
    /// </summary>
    private static List<(int row, int col)> GetOrientationAlignedTargets(
        Board board, List<(int row, int col)> candidates)
    {
        // Find hit ships and their orientations
        var shipHits = new Dictionary<string, List<(int row, int col)>>();
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];
            if (cell.IsHit && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
            {
                if (!shipHits.ContainsKey(cell.ShipRef.Id))
                    shipHits[cell.ShipRef.Id] = new List<(int, int)>();
                shipHits[cell.ShipRef.Id].Add((r, c));
            }
        }

        var aligned = new List<(int row, int col)>();

        foreach (var (shipId, hits) in shipHits)
        {
            if (hits.Count < 2) continue;

            // Determine orientation from multiple hits
            var sameRow = hits.All(h => h.row == hits[0].row);
            var sameCol = hits.All(h => h.col == hits[0].col);

            if (sameRow)
            {
                // Ship is horizontal — only add candidates in the same row
                var row = hits[0].row;
                var minCol = hits.Min(h => h.col);
                var maxCol = hits.Max(h => h.col);
                // Extend left and right
                foreach (var c in candidates)
                {
                    if (c.row == row && (c.col == minCol - 1 || c.col == maxCol + 1))
                        aligned.Add(c);
                }
            }
            else if (sameCol)
            {
                // Ship is vertical — only add candidates in the same column
                var col = hits[0].col;
                var minRow = hits.Min(h => h.row);
                var maxRow = hits.Max(h => h.row);
                foreach (var c in candidates)
                {
                    if (c.col == col && (c.row == minRow - 1 || c.row == maxRow + 1))
                        aligned.Add(c);
                }
            }
        }

        return aligned;
    }

    /// <summary>
    /// Hunt mode: calculates probability density for each cell based on remaining ship sizes.
    /// Uses checkerboard parity for efficiency and avoids impossible placements.
    /// </summary>
    private static (int row, int col) ChooseHuntTarget(
        Board board,
        HashSet<int> blockedRows,
        HashSet<(int, int)> excludeCells = null,
        bool avoidDodgeCells = false)
    {
        var density = new int[10, 10];

        // Fleet positions, live deck counts and armor are hidden. Use only the public
        // baseline fleet silhouette for hunt density; shot/reveal marks below remain public.
        var aliveShipSizes = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };

        var minShipSize = aliveShipSizes.Min();

        // For each remaining ship size, count how many placements go through each cell
        foreach (var size in aliveShipSizes)
        {
            // Horizontal placements
            for (var r = 0; r < 10; r++)
            for (var c = 0; c <= 10 - size; c++)
            {
                var valid = true;
                for (var i = 0; i < size; i++)
                {
                    var cell = board.Grid[r, c + i];
                    if (cell.IsHit || cell.IsMiss || (avoidDodgeCells && cell.WasDodge))
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid)
                {
                    for (var i = 0; i < size; i++)
                        density[r, c + i]++;
                }
            }

            // Vertical placements
            for (var r = 0; r <= 10 - size; r++)
            for (var c = 0; c < 10; c++)
            {
                var valid = true;
                for (var i = 0; i < size; i++)
                {
                    var cell = board.Grid[r + i, c];
                    if (cell.IsHit || cell.IsMiss || (avoidDodgeCells && cell.WasDodge))
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid)
                {
                    for (var i = 0; i < size; i++)
                        density[r + i, c]++;
                }
            }
        }

        // Apply checkerboard parity bonus for minimum ship size
        var parity = minShipSize % 2 == 0 ? 0 : 1; // doesn't matter which, just needs to be consistent
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            if ((r + c) % 2 == parity)
                density[r, c] += 2;
        }

        // Zero out already-hit, missed, and blocked-row cells
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = board.Grid[r, c];
            if (cell.IsHit || cell.IsMiss || (avoidDodgeCells && cell.WasDodge) ||
                blockedRows.Contains(r) || (excludeCells != null && excludeCells.Contains((r, c))))
                density[r, c] = 0;
        }

        // Find max density cells and pick one randomly
        var maxDensity = 0;
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
            if (density[r, c] > maxDensity) maxDensity = density[r, c];

        if (maxDensity == 0)
        {
            // Fallback: random untouched cell
            var fallback = new List<(int, int)>();
            for (var r = 0; r < 10; r++)
            for (var c = 0; c < 10; c++)
            {
                var cell = board.Grid[r, c];
                if (!cell.IsHit && !cell.IsMiss && (!avoidDodgeCells || !cell.WasDodge) &&
                    !blockedRows.Contains(r) && !(excludeCells != null && excludeCells.Contains((r, c))))
                    fallback.Add((r, c));
            }
            if (fallback.Count > 0) return fallback[Rng.Next(fallback.Count)];

            // A late-game board can have no untouched cells. Relax only the prior-shot
            // constraint first; a Ballista must still never return to a nimble-dodge mark.
            for (var r = 0; r < 10; r++)
            for (var c = 0; c < 10; c++)
            {
                var cell = board.Grid[r, c];
                if ((!avoidDodgeCells || !cell.WasDodge) && !blockedRows.Contains(r) &&
                    !(excludeCells != null && excludeCells.Contains((r, c))))
                    fallback.Add((r, c));
            }
            if (fallback.Count > 0) return fallback[Rng.Next(fallback.Count)];

            // Preserve the absolute dodge exclusion even if every other targeting constraint
            // has become impossible (for example, all remaining cells are in Far-blocked rows).
            for (var r = 0; r < 10; r++)
            for (var c = 0; c < 10; c++)
                if (!avoidDodgeCells || !board.Grid[r, c].WasDodge)
                    fallback.Add((r, c));
            if (fallback.Count > 0) return fallback[Rng.Next(fallback.Count)];

            throw new InvalidOperationException("No Ballista target exists outside nimble-dodge cells.");
        }

        var best = new List<(int, int)>();
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
            if (density[r, c] == maxDensity)
                best.Add((r, c));

        return best[Rng.Next(best.Count)];
    }

    /// <summary>
    /// Buckshot (2x2 AoE) targeting: pick the 2x2 area with the most untouched cells.
    /// Prefer areas adjacent to known hits.
    /// </summary>
    private static (int row, int col) ChooseBuckshotTarget(Board board, HashSet<int> blockedRows)
    {
        var best = new List<(int row, int col, int score)>();

        for (var r = 0; r < 9; r++)
        for (var c = 0; c < 9; c++)
        {
            if (blockedRows.Contains(r) || blockedRows.Contains(r + 1)) continue;

            var score = 0;
            var blocked = false;
            for (var dr = 0; dr < 2; dr++)
            for (var dc = 0; dc < 2; dc++)
            {
                var cell = board.Grid[r + dr, c + dc];
                if (cell.IsHit && !cell.WasScratched || cell.IsMiss)
                    blocked = true;
                else
                    score += 3; // untouched cells
            }
            if (blocked && score == 0) continue;

            // Bonus for being near known hits
            for (var dr = -1; dr <= 2; dr++)
            for (var dc = -1; dc <= 2; dc++)
            {
                var adj = board.GetCell(r + dr, c + dc);
                if (adj != null && adj.IsHit && adj.ShipRef != null && !adj.ShipRef.IsDestroyed)
                    score += 5;
            }

            best.Add((r, c, score));
        }

        if (best.Count == 0) return (Rng.Next(9), Rng.Next(9));

        best.Sort((a, b) => b.score.CompareTo(a.score));
        var topCount = Math.Min(3, best.Count);
        var pick = best[Rng.Next(topCount)];
        return (pick.row, pick.col);
    }

    // ── Summon Deployment ────────────────────────────────────────────

    /// <summary>
    /// Determines if the bot should deploy a summon, and which type.
    /// Returns null if no deployment should happen.
    /// </summary>
    public static (SummonType type, int col)? ChooseSummonDeploy(
        BattleshipGame game, BattleshipPlayer bot)
    {
        var opponent = game.GetOpponent(bot.DiscordId);
        if (opponent == null) return null;

        var waitingRam = bot.Summons.FirstOrDefault(summon =>
            summon.IsAlive &&
            summon.Type == SummonType.Ram &&
            summon.WaitingForTurnBack);
        if (waitingRam != null)
        {
            if (waitingRam.MoveDirection is Direction.Left or Direction.Right)
            {
                var entryCol = waitingRam.MoveDirection == Direction.Right ? 9 : 0;
                var rows = Enumerable.Range(
                        Math.Max(0, waitingRam.Row - 1),
                        Math.Min(9, waitingRam.Row + 1) - Math.Max(0, waitingRam.Row - 1) + 1)
                    .Where(row => opponent.Board.GetCell(row, entryCol)?.SummonRef is not { IsAlive: true })
                    .ToList();
                return rows.Count == 0 ? null : (SummonType.Ram, rows[Rng.Next(rows.Count)]);
            }

            var entryRow = waitingRam.MoveDirection == Direction.Down ? 9 : 0;
            var cols = Enumerable.Range(
                    Math.Max(0, waitingRam.Col - 1),
                    Math.Min(9, waitingRam.Col + 1) - Math.Max(0, waitingRam.Col - 1) + 1)
                .Where(col => opponent.Board.GetCell(entryRow, col)?.SummonRef is not { IsAlive: true })
                .ToList();
            return cols.Count == 0 ? null : (SummonType.Ram, cols[Rng.Next(cols.Count)]);
        }

        // Check reveal threshold
        var threshold = 5 * (bot.SummonSlotsUsed + 1);
        if (bot.RevealedCellCount < threshold && game.Phase != BsGamePhase.Boarding) return null;

        // Check cooldown
        if (game.ShotCount - bot.LastSummonDeployShotCount < 2 && game.Phase != BsGamePhase.Boarding) return null;

        // Don't deploy too many summons at once
        var aliveSummons = bot.Summons.Count(s => s.IsAlive);
        if (aliveSummons >= 2) return null;

        // Determine available summon types based on fleet regions.
        // Regular summons share four per-match uses; Brander has its own 1-per-match cap (ТЗ #10)
        var slotsFull = bot.SummonSlotsUsed >= bot.MaxSummonSlots;
        var regions = bot.Fleet
            .Where(s => !s.Statuses.Contains(ShipStatusType.Capture))
            .SelectMany(s => s.Regions).Distinct().ToHashSet();
        var available = new List<SummonType>();

        if (!slotsFull)
        {
            if (regions.Contains(Region.West)) available.Add(SummonType.Ram);
            if (regions.Contains(Region.East)) available.Add(SummonType.Scout);
            if (regions.Contains(Region.South)) available.Add(SummonType.PirateBoat);
        }
        if (!bot.BranderUsed && bot.Fleet.Any(s => !s.IsDestroyed && s.Abilities.Contains("brander_summon")))
            available.Add(SummonType.Brander);

        if (available.Count == 0) return null;

        // Choose strategically
        SummonType chosen;
        if (available.Contains(SummonType.Ram) && Rng.Next(3) > 0)
        {
            // Ram is generally the best — direct damage
            chosen = SummonType.Ram;
        }
        else if (available.Contains(SummonType.Scout) && bot.RevealedCellCount < 30)
        {
            // Scout useful early for information
            chosen = SummonType.Scout;
        }
        else if (available.Contains(SummonType.PirateBoat))
        {
            chosen = SummonType.PirateBoat;
        }
        else
        {
            chosen = available[Rng.Next(available.Count)];
        }

        // Choose column: aim at opponent's revealed ships or random
        var col = ChooseSummonColumn(opponent);

        return (chosen, col);
    }

    /// <summary>
    /// Picks a column for summon deployment. Prefers columns where opponent has known ships.
    /// </summary>
    private static int ChooseSummonColumn(BattleshipPlayer opponent)
    {
        // Find columns with known (hit but not destroyed) ships
        var shipCols = new HashSet<int>();
        for (var r = 0; r < 10; r++)
        for (var c = 0; c < 10; c++)
        {
            var cell = opponent.Board.Grid[r, c];
            if (cell.IsHit && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
                shipCols.Add(c);
        }

        if (shipCols.Count > 0 && Rng.Next(3) > 0)
            return shipCols.ElementAt(Rng.Next(shipCols.Count));

        return Rng.Next(10);
    }

    // ── Pending Summon Deployment ────────────────────────────────────

    /// <summary>
    /// Deploys pending summons (pirate/cursed boats from ship death, boarding ships).
    /// Returns a list of (pendingId, column) to deploy.
    /// </summary>
    public static List<(string pendingId, int col)> ChoosePendingSummonDeploys(
        BattleshipPlayer bot, BattleshipPlayer opponent)
    {
        var deploys = new List<(string, int)>();
        var reservedEntryColumns = Enumerable.Range(0, 10)
            .Where(col => opponent.Board.GetCell(0, col)?.SummonRef is { IsAlive: true })
            .ToHashSet();

        foreach (var pending in bot.PendingSummons.ToList())
        {
            var allowedColumns = (pending.AllowedColumns.Count > 0
                    ? pending.AllowedColumns
                    : Enumerable.Range(0, 10))
                .Where(col => !reservedEntryColumns.Contains(col))
                .ToList();
            if (allowedColumns.Count == 0) continue;

            // Every mandatory Boarding unit MUST be deployed immediately.
            if (pending.IsMandatoryBoarding)
            {
                var preferred = ChooseSummonColumn(opponent);
                var col = allowedColumns.Contains(preferred)
                    ? preferred
                    : allowedColumns[Rng.Next(allowedColumns.Count)];
                deploys.Add((pending.Id, col));
                reservedEntryColumns.Add(col);
                continue;
            }

            // Deploy free pending summons with some probability
            if (pending.IsFree || bot.SummonSlotsUsed < bot.MaxSummonSlots)
            {
                var preferred = ChooseSummonColumn(opponent);
                var col = allowedColumns.Contains(preferred)
                    ? preferred
                    : allowedColumns[Rng.Next(allowedColumns.Count)];
                deploys.Add((pending.Id, col));
                reservedEntryColumns.Add(col);
            }
        }

        return deploys;
    }

    // ── CursedBoat Direction Choice ──────────────────────────────────

    /// <summary>
    /// Chooses a direction for a CursedBoat after collision.
    /// Picks the direction most likely to hit more ships.
    /// </summary>
    public static Direction ChooseCursedBoatDirection(Summon cursedBoat, BattleshipPlayer opponent)
    {
        var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right }
            .Where(direction =>
            {
                var (dr, dc) = direction switch
                {
                    Direction.Up => (-1, 0),
                    Direction.Down => (1, 0),
                    Direction.Left => (0, -1),
                    Direction.Right => (0, 1),
                    _ => (0, 0),
                };
                var nextRow = cursedBoat.Row + dr;
                var nextCol = cursedBoat.Col + dc;
                return nextRow is >= 0 and < 10 && nextCol is >= 0 and < 10;
            })
            .ToArray();
        var bestDir = directions.FirstOrDefault();
        var bestScore = -1;

        foreach (var dir in directions)
        {
            var score = 0;
            var (dr, dc) = dir switch
            {
                Direction.Up => (-1, 0),
                Direction.Down => (1, 0),
                Direction.Left => (0, -1),
                Direction.Right => (0, 1),
                _ => (0, 0)
            };

            // Look ahead for ships in this direction
            for (var step = 1; step <= 9; step++)
            {
                var nr = cursedBoat.Row + dr * step;
                var nc = cursedBoat.Col + dc * step;
                var cell = opponent.Board.GetCell(nr, nc);
                if (cell == null) break;

                if (cell.IsRevealed && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
                    score += 10;
                if (cell.IsHit && cell.ShipRef != null && !cell.ShipRef.IsDestroyed)
                    score += 5; // known target
            }

            // Add randomness
            score += Rng.Next(3);

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
            }
        }

        return bestDir;
    }
}
