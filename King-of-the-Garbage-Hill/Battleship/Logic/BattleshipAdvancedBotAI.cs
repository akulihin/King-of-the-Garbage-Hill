using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Battleship.Models;

namespace King_of_the_Garbage_Hill.Battleship.Logic;

/// <summary>
/// Honest-information strategies for Battleship bot generations V2 and V3.
/// The only method allowed to inspect the physical enemy board is
/// <see cref="BattleshipBotObservationFactory.Create"/>; it projects exactly the fog fields available to a
/// human player before any tactical method receives them.
/// </summary>
public static class BattleshipAdvancedBotAI
{
    private enum FleetArchetype
    {
        Balanced,
        Artillery,
        Boarding,
        Control,
        Evasion,
        Recon,
    }

    public sealed class BotCellObservation
    {
        public int Row { get; init; }
        public int Col { get; init; }
        public bool IsRevealed { get; init; }
        public bool HasKnownShip { get; init; }
        public bool IsHit { get; init; }
        public bool IsMiss { get; init; }
        public bool IsScratched { get; init; }
        public bool IsDestroyed { get; init; }
        public bool IsShipSunk { get; init; }
        public bool IsBurning { get; init; }
        public bool HasElectricCharge { get; init; }
        public bool IsBurnResistMarked { get; init; }
        public bool IsFrozen { get; init; }
        public bool IsDevastated { get; init; }
        public bool IsCaptured { get; init; }
        public bool WasDodge { get; init; }
        public bool WasManeuverDodge { get; init; }
        public bool HasAnySummon { get; init; }
        public bool HasFriendlySummon { get; init; }
        public string SunkShipName { get; init; }

        public bool IsKnownWater => IsMiss || IsRevealed && !HasKnownShip && !IsHit;
        public bool IsUnresolvedShipEvidence =>
            !WasManeuverDodge && !IsCaptured &&
            (HasKnownShip && !IsShipSunk || IsHit && !IsShipSunk);
        public bool IsDirectLiveTarget =>
            !WasManeuverDodge && !IsCaptured &&
            HasKnownShip && !IsDestroyed && !IsShipSunk;
    }

    public sealed class BotObservation
    {
        private readonly HashSet<(int row, int col)> _reachableAutoDodgeCells = new();
        private readonly HashSet<string> _autoDodgeHullKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _autoDodgeDefinitionKeys = new(StringComparer.Ordinal);
        private readonly HashSet<(int row, int col)> _staleEvidenceCells = new();
        private readonly HashSet<(int row, int col)> _recheckedMovementWaterCells = new();

        public Faction Faction { get; init; }
        public BotCellObservation[,] Cells { get; } = new BotCellObservation[10, 10];
        public HashSet<string> KnownSunkNames { get; } = new(StringComparer.Ordinal);

        public BotCellObservation Cell(int row, int col) =>
            row is >= 0 and < 10 && col is >= 0 and < 10 ? Cells[row, col] : null;

        internal void RegisterAutoDodgePlacement(
            string definitionId,
            IReadOnlyCollection<(int row, int col)> cells)
        {
            var hullKey = PlacementKey(cells);
            _autoDodgeHullKeys.Add(hullKey);
            _autoDodgeDefinitionKeys.Add($"{definitionId}:{hullKey}");
            _reachableAutoDodgeCells.UnionWith(cells);
        }

        internal void MarkStaleEvidence(int row, int col) =>
            _staleEvidenceCells.Add((row, col));

        internal void MarkRecheckedMovementWater(int row, int col) =>
            _recheckedMovementWaterCells.Add((row, col));

        internal bool IsStaleEvidence(BotCellObservation cell) =>
            _staleEvidenceCells.Contains((cell.Row, cell.Col));

        internal bool IsReachableAutoDodgeCell(BotCellObservation cell) =>
            _reachableAutoDodgeCells.Contains((cell.Row, cell.Col));

        internal bool WasMovementWaterRechecked(BotCellObservation cell) =>
            _recheckedMovementWaterCells.Contains((cell.Row, cell.Col));

        internal double MovementFallbackScore(BotCellObservation cell)
        {
            var score = IsReachableAutoDodgeCell(cell) ? 24.0 : 1.0;
            foreach (var (row, col) in _staleEvidenceCells)
            {
                var rowDistance = Math.Abs(cell.Row - row);
                var colDistance = Math.Abs(cell.Col - col);
                // Manual maneuvers are axial. A newly occupied leading deck can be farther
                // than the one- or two-cell anchor shift, so retain a short same-axis cone.
                if ((rowDistance == 0) == (colDistance == 0)) continue;
                var distance = rowDistance + colDistance;
                if (distance <= 6)
                    score = Math.Max(score, 18.0 - distance * 2.0);
            }
            return score;
        }

        internal bool MatchesAutoDodgePlacement(
            IReadOnlyCollection<(int row, int col)> cells) =>
            _autoDodgeHullKeys.Contains(PlacementKey(cells));

        internal bool MatchesAutoDodgePlacement(
            ShipDefinition definition,
            IReadOnlyCollection<(int row, int col)> cells) =>
            _autoDodgeDefinitionKeys.Contains($"{definition.Id}:{PlacementKey(cells)}");

        private static string PlacementKey(IEnumerable<(int row, int col)> cells) =>
            string.Join(",", cells.Select(cell => cell.row * 10 + cell.col).OrderBy(index => index));
    }

    private sealed record PlannedPlacement(int Row, int Col, Orientation Orientation);

    private static bool IsUnresolvedEvidence(
        BotObservation observation,
        BotCellObservation cell) =>
        cell.IsUnresolvedShipEvidence && !observation.IsStaleEvidence(cell);

    private static bool IsDirectLiveTarget(
        BotObservation observation,
        BotCellObservation cell) =>
        cell.IsDirectLiveTarget && !observation.IsStaleEvidence(cell);

    private static bool HasReliableShipEvidence(
        BotObservation observation,
        BotCellObservation cell) =>
        cell.HasKnownShip && !cell.IsCaptured && !observation.IsStaleEvidence(cell);

    private static bool IsBlockedKnownWater(
        BotObservation observation,
        BotCellObservation cell) =>
        cell.IsKnownWater && !observation.IsReachableAutoDodgeCell(cell);

    /// <summary>A currently occupied public cell of an enemy summon on the bot's board.</summary>
    public sealed record BotSummonTarget(Summon Summon, int Row, int Col);

    // ── Fleet selection ──────────────────────────────────────────────

    /// <summary>
    /// Builds a varied, legal Empire fleet. V2 samples broadly from good legal fleets;
    /// V3 evaluates a larger pool and keeps stronger archetype synergies without becoming
    /// deterministic.
    /// </summary>
    public static List<FleetSelection> SelectFleet(BattleshipBotVersion version)
    {
        // Desiccator is a legitimate but highly committal Empire doctrine: one fragile hull
        // consumes most of the budget in exchange for Ballista immunity and Boarding leverage.
        // Keep it deliberately rare, while still making the full Empire catalog reachable.
        if (Random.Shared.NextDouble() <
            (version == BattleshipBotVersion.V3 ? 0.045 : 0.035))
        {
            var desiccatorPlan = BuildDesiccatorPlan();
            if (FleetValidator.ValidateFleet(desiccatorPlan, Faction.Empire).valid)
                return FleetValidator.BuildFleetFromSelections(desiccatorPlan, Faction.Empire);
        }

        var archetype = Enum.GetValues<FleetArchetype>()[Random.Shared.Next(
            Enum.GetValues<FleetArchetype>().Length)];
        // Initiative is a real resource: unspent coins decide first turn before upgrades
        // and Home units. Some expert fleets therefore follow a deliberate tempo curve.
        var tempoPlan = version == BattleshipBotVersion.V3 && Random.Shared.NextDouble() < 0.34;
        var targetSpend = tempoPlan ? Random.Shared.Next(14, 26) : Random.Shared.Next(31, 41);
        var candidates = new List<(List<FleetSelection> selections, double score)>();
        var attempts = version == BattleshipBotVersion.V3 ? 900 : 420;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var selections = GenerateFleetCandidate(archetype, version);
            var (valid, _) = FleetValidator.ValidateFleet(selections, Faction.Empire);
            if (!valid) continue;
            candidates.Add((selections, ScoreFleet(
                selections, archetype, version, targetSpend, tempoPlan)));
        }

        if (candidates.Count == 0)
            return BattleshipBotAI.SelectFleet(Faction.Empire);

        var ordered = candidates
            .OrderByDescending(candidate => candidate.score)
            .Take(version == BattleshipBotVersion.V3 ? 12 : 28)
            .ToList();
        var selected = WeightedTopChoice(ordered.Select(candidate => candidate.score).ToList());
        return FleetValidator.BuildFleetFromSelections(ordered[selected].selections, Faction.Empire);
    }

    private static List<FleetSelection> BuildDesiccatorPlan()
    {
        var desiccator = ShipCatalog.GetById("desiccator")
                          ?? throw new InvalidOperationException("Desiccator is missing from the catalog.");
        var selections = new List<FleetSelection> { NewSelection(desiccator) };

        // The remaining six coins support several initiative/loadout doctrines instead of one
        // recognizable flagship configuration.
        switch (Random.Shared.Next(8))
        {
            case 1:
                selections.Add(UpgradeSelection("tetranavis", "tetra_boiler_fire"));
                break;
            case 2:
                selections.Add(UpgradeSelection("tetranavis", "tetra_boiler_brander"));
                break;
            case 3:
                selections.Add(UpgradeSelection("tetranavis", "tetra_boiler_evil_fire"));
                break;
            case 4:
                selections.Add(UpgradeSelection("double", "double_mast"));
                break;
            case 5:
                selections.Add(UpgradeSelection("triple", "triple_crew"));
                break;
            case 6:
                selections.Add(UpgradeSelection("tetranavis", "tetra_boiler_fire"));
                selections.Add(UpgradeSelection("double", "double_mast"));
                break;
            case 7:
                selections.Add(UpgradeSelection("tetranavis", "tetra_boiler_brander"));
                selections.Add(UpgradeSelection("triple", "triple_crew"));
                break;
        }
        return selections;
    }

    private static FleetSelection UpgradeSelection(string definitionId, params string[] upgradeIds)
    {
        var definition = ShipCatalog.GetById(definitionId)
                         ?? throw new InvalidOperationException($"Ship {definitionId} is missing from the catalog.");
        var upgrades = upgradeIds.ToList();
        return new FleetSelection
        {
            DefinitionId = definition.Id,
            ShipName = definition.Name,
            Cost = upgrades.Sum(upgradeId =>
                definition.AvailableUpgrades.First(upgrade => upgrade.Id == upgradeId).Cost),
            Upgrades = upgrades,
        };
    }

    private static List<FleetSelection> GenerateFleetCandidate(
        FleetArchetype archetype,
        BattleshipBotVersion version)
    {
        var selections = new List<FleetSelection>();
        var regions = new HashSet<Region>();
        var remainingBudget = FleetValidator.EmpireBudget;
        var paidByDeck = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0, [4] = 0 };
        var replacementChance = version == BattleshipBotVersion.V3 ? 0.72 : 0.58;

        foreach (var deckCount in new[] { 3, 1, 2 }.OrderBy(_ => Random.Shared.Next()))
        {
            for (var slot = 0; slot < FleetValidator.Template[deckCount]; slot++)
            {
                if (Random.Shared.NextDouble() > replacementChance) continue;
                var legal = ShipCatalog.AllShips
                    .Where(definition =>
                        !definition.IsFree &&
                        definition.DeckCount == deckCount &&
                        definition.Factions.Contains(Faction.Empire) &&
                        definition.Cost <= remainingBudget &&
                        CanAddRegions(definition, regions))
                    .ToList();
                if (legal.Count == 0) continue;

                var weights = legal.Select(definition =>
                    Math.Max(0.25, DefinitionArchetypeValue(definition, archetype) +
                                   Random.Shared.NextDouble() * 1.8)).ToList();
                var chosen = legal[WeightedChoice(weights)];
                selections.Add(NewSelection(chosen));
                remainingBudget -= chosen.Cost;
                paidByDeck[deckCount]++;
                AddRegions(chosen, regions);
            }
        }

        // One paid Incendiary upgrade can turn a known dead deck into a kill line.
        foreach (var selection in selections
                     .Where(selection => selection.DefinitionId == "incendiary_barge")
                     .OrderBy(_ => Random.Shared.Next()))
        {
            if (remainingBudget < 2 || Random.Shared.NextDouble() >
                (archetype == FleetArchetype.Artillery ? 0.8 : 0.35)) continue;
            selection.Upgrades.Add("barge_evil_incendiary");
            selection.Cost += 2;
            remainingBudget -= 2;
            break;
        }

        // The Empire flagship is always present; vary its boiler rather than locking one build.
        if (remainingBudget >= 4 && Random.Shared.NextDouble() < 0.88)
        {
            var boilerChoices = new List<(string id, int cost, double weight)>
            {
                ("tetra_boiler_fire", 4,
                    archetype is FleetArchetype.Control or FleetArchetype.Recon ? 3.2 : 1.4),
                ("tetra_boiler_brander", 4,
                    archetype is FleetArchetype.Boarding or FleetArchetype.Artillery ? 3.4 : 1.5),
            };
            if (remainingBudget >= 6)
                boilerChoices.Add(("tetra_boiler_evil_fire", 6,
                    archetype is FleetArchetype.Control or FleetArchetype.Boarding ? 2.8 : 1.1));
            var choice = boilerChoices[WeightedChoice(boilerChoices.Select(value => value.weight).ToList())];
            selections.Add(new FleetSelection
            {
                DefinitionId = "tetranavis",
                ShipName = ShipCatalog.GetById("tetranavis")?.Name ?? "Tetranavis",
                Cost = choice.cost,
                Upgrades = new List<string> { choice.id },
            });
            remainingBudget -= choice.cost;
        }

        var defaultDoubles = FleetValidator.Template[2] - paidByDeck[2];
        if (defaultDoubles > 0 && remainingBudget >= 2 && Random.Shared.NextDouble() < 0.72)
        {
            selections.Add(new FleetSelection
            {
                DefinitionId = "double",
                ShipName = "Double",
                Cost = 2,
                Upgrades = new List<string> { "double_mast" },
            });
            remainingBudget -= 2;
        }

        var defaultTriples = FleetValidator.Template[3] - paidByDeck[3];
        for (var index = 0; index < defaultTriples; index++)
        {
            var upgrades = new List<string>();
            if (remainingBudget >= 2 && Random.Shared.NextDouble() <
                (archetype == FleetArchetype.Boarding ? 0.88 : 0.6))
            {
                upgrades.Add("triple_crew");
                remainingBudget -= 2;
            }
            if (remainingBudget >= 6 && Random.Shared.NextDouble() <
                (archetype is FleetArchetype.Artillery or FleetArchetype.Recon ? 0.82 : 0.48))
            {
                upgrades.Add("triple_ammo");
                remainingBudget -= 6;
            }
            foreach (var deck in new[] { 1, 3 }.OrderBy(_ => Random.Shared.Next()))
            {
                if (remainingBudget < 4 || Random.Shared.NextDouble() >
                    (archetype == FleetArchetype.Control ? 0.64 : 0.3)) continue;
                upgrades.Add($"triple_armor_{deck}");
                remainingBudget -= 4;
            }
            if (upgrades.Count == 0) continue;
            selections.Add(new FleetSelection
            {
                DefinitionId = "triple",
                ShipName = "Triple",
                Cost = upgrades.Sum(upgrade =>
                    ShipCatalog.GetById("triple")?.AvailableUpgrades
                        .FirstOrDefault(value => value.Id == upgrade)?.Cost ?? 0),
                Upgrades = upgrades,
            });
        }

        return selections;
    }

    private static FleetSelection NewSelection(ShipDefinition definition) => new()
    {
        DefinitionId = definition.Id,
        ShipName = definition.Name,
        Cost = definition.Cost,
        Upgrades = new List<string>(),
    };

    private static bool CanAddRegions(ShipDefinition definition, HashSet<Region> current)
    {
        var combined = new HashSet<Region>(current);
        foreach (var region in definition.Regions.Where(region => region != Region.Tetracor))
            combined.Add(region);
        return combined.Count <= FleetValidator.MaxRegions;
    }

    private static void AddRegions(ShipDefinition definition, HashSet<Region> current)
    {
        foreach (var region in definition.Regions.Where(region => region != Region.Tetracor))
            current.Add(region);
    }

    private static double ScoreFleet(
        List<FleetSelection> selections,
        FleetArchetype archetype,
        BattleshipBotVersion version,
        int targetSpend,
        bool tempoPlan)
    {
        var fullFleet = FleetValidator.BuildFleetFromSelections(selections, Faction.Empire);
        var spent = FleetValidator.CalculateTotalCost(fullFleet);
        var score = spent * (tempoPlan ? 0.08 : version == BattleshipBotVersion.V3 ? 0.32 : 0.28);
        score -= Math.Abs(spent - targetSpend) * (tempoPlan ? 1.2 : 0.42);
        if (tempoPlan)
        {
            score += (FleetValidator.EmpireBudget - spent) * 0.7;
            score += fullFleet.Count(selection =>
                ShipCatalog.GetById(selection.DefinitionId)?.IsFree == true &&
                selection.Upgrades.Count == 0) * 0.8;
        }
        var distinct = fullFleet.Select(selection => selection.DefinitionId).Distinct().Count();
        score += distinct * 2.2;
        score -= fullFleet.GroupBy(selection => selection.DefinitionId)
            .Sum(group => Math.Max(0, group.Count() - 2)) * 1.6;

        foreach (var selection in fullFleet)
        {
            var definition = ShipCatalog.GetById(selection.DefinitionId);
            if (definition == null) continue;
            score += definition.DeckHpOverrides?.Sum() ?? definition.DeckCount * definition.DefaultArmor;
            score += DefinitionArchetypeValue(definition, archetype) * 2.1;
            score += definition.DefaultWeapons.Count(weapon =>
                weapon.Type is WeaponType.Tetracatapult or WeaponType.Incendiary) * 2.5;
            score += definition.Abilities.Count * 0.85;
            score += selection.Upgrades.Count * 0.75;
        }

        // Reward broad tactical access but do not force the same three regions every game.
        score += fullFleet
            .SelectMany(selection => ShipCatalog.GetById(selection.DefinitionId)?.Regions ?? new())
            .Where(region => region != Region.Tetracor)
            .Distinct()
            .Count() * 1.25;
        score += Random.Shared.NextDouble() * (version == BattleshipBotVersion.V3 ? 8 : 16);
        return score;
    }

    private static double DefinitionArchetypeValue(
        ShipDefinition definition,
        FleetArchetype archetype)
    {
        var abilities = definition.Abilities;
        var value = 1.0;
        if (archetype == FleetArchetype.Artillery &&
            (definition.Range is RangeClass.Far or RangeClass.Tetra ||
             definition.DefaultWeapons.Any(weapon => weapon.Type == WeaponType.Incendiary))) value += 4;
        if (archetype == FleetArchetype.Boarding &&
            (definition.Range is RangeClass.Close or RangeClass.CloseMelee ||
             definition.Regions.Contains(Region.South) || abilities.Any(ability => ability.Contains("spawn")))) value += 4;
        if (archetype == FleetArchetype.Control &&
            (definition.Regions.Contains(Region.North) ||
             abilities.Any(ability => ability is "freeze_nearby" or "burn_resist" or "poison_cone"))) value += 4;
        if (archetype == FleetArchetype.Evasion &&
            abilities.Any(ability => ability is "ballista_immune" or "auto_dodge_bow_stern" or "manual_move_after_hit")) value += 5;
        if (archetype == FleetArchetype.Recon &&
            (definition.Regions.Contains(Region.East) || definition.Range == RangeClass.Tetra)) value += 3.5;
        if (archetype == FleetArchetype.Balanced) value += Math.Min(2.5, definition.Cost / 8.0);
        return value;
    }

    // ── Expert placement ────────────────────────────────────────────

    public static void PlaceFleet(BattleshipPlayer bot, BattleshipBotVersion version)
    {
        if (version != BattleshipBotVersion.V3)
        {
            BattleshipBotAI.PlaceFleet(bot);
            return;
        }

        Dictionary<string, PlannedPlacement> bestPlan = null;
        var bestScore = double.NegativeInfinity;
        var orderedShips = bot.Fleet
            .OrderByDescending(ship => ship.Abilities.Contains("poison_cone") ? 1000 : 0)
            .ThenByDescending(ship => ship.Range is RangeClass.Far or RangeClass.Tetra ? 100 : 0)
            .ThenByDescending(ship => ship.Decks.Count)
            .ToList();

        for (var attempt = 0; attempt < 72; attempt++)
        {
            var board = new Board();
            var plan = new Dictionary<string, PlannedPlacement>(StringComparer.Ordinal);
            var failed = false;
            foreach (var ship in orderedShips)
            {
                var candidates = new List<(PlannedPlacement placement, double score)>();
                for (var row = 0; row < 10; row++)
                for (var col = 0; col < 10; col++)
                foreach (var orientation in Enum.GetValues<Orientation>())
                {
                    var (valid, _) = PlacementValidator.ValidatePlacement(
                        board, ship, row, col, orientation);
                    if (!valid || !IsPoisonSafe(board, ship, row, col, orientation)) continue;
                    candidates.Add((
                        new PlannedPlacement(row, col, orientation),
                        ScorePlacementCandidate(board, ship, row, col, orientation)));
                }

                if (candidates.Count == 0)
                {
                    failed = true;
                    break;
                }

                var shortlist = candidates.OrderByDescending(candidate => candidate.score)
                    .Take(14).ToList();
                var pickIndex = WeightedTopChoice(shortlist.Select(candidate => candidate.score).ToList());
                var pick = shortlist[pickIndex].placement;
                PutShip(board, ship, pick.Row, pick.Col, pick.Orientation);
                plan[ship.Id] = pick;
            }

            if (failed) continue;
            var score = ScoreCompletedPlacement(board) + Random.Shared.NextDouble() * 18;
            if (score <= bestScore) continue;
            bestScore = score;
            bestPlan = plan;
        }

        ResetBoard(bot);
        if (bestPlan == null)
        {
            BattleshipBotAI.PlaceFleet(bot);
            return;
        }

        foreach (var ship in orderedShips)
        {
            var placement = bestPlan[ship.Id];
            PutShip(bot.Board, ship, placement.Row, placement.Col, placement.Orientation);
        }
    }

    private static double ScorePlacementCandidate(
        Board board,
        Ship ship,
        int row,
        int col,
        Orientation orientation)
    {
        var cells = ship.GetOccupiedCells(row, col, orientation);
        var score = Random.Shared.NextDouble() * 7;
        if (ship.Range is RangeClass.Far or RangeClass.Tetra)
        {
            var isArtilleryBarge = ship.Weapons.Any(weapon =>
                weapon.Type is WeaponType.Incendiary or WeaponType.EvilIncendiary);
            score += cells.All(cell => cell.row >= 8)
                ? isArtilleryBarge ? 3 : 12
                : isArtilleryBarge && cells.All(cell => cell.row is >= 5 and <= 7) ? 16 : 2;
        }
        else if (cells.Any(cell => cell.row >= 8))
            score -= 100;

        var edgeCells = cells.Count(cell => cell.row is 0 or 7 or 8 or 9 || cell.col is 0 or 9);
        score += edgeCells * (ship.Decks.Count <= 2 ? 4.2 : 1.4);
        if (cells.Any(cell => cell is (0, 0) or (0, 9) or (7, 0) or (7, 9) or (8, 0) or (8, 9) or (9, 0) or (9, 9)))
            score += ship.Decks.Count <= 2 ? 6 : 2;
        if (cells.Any(cell => cell.row == 0))
            score -= ship.Cost * 0.35 + ship.Decks.Count * 1.8;

        foreach (var placed in board.PlacedShips)
        {
            var distance = cells.SelectMany(_ => placed.GetOccupiedCells(), (mine, other) =>
                    Math.Abs(mine.row - other.row) + Math.Abs(mine.col - other.col))
                .Min();
            score += Math.Min(8, distance) * 0.75;
            if (ship.Abilities.Contains("explode_on_hit") || placed.Abilities.Contains("explode_on_hit"))
            {
                var chebyshev = cells.SelectMany(_ => placed.GetOccupiedCells(), (mine, other) =>
                        Math.Max(Math.Abs(mine.row - other.row), Math.Abs(mine.col - other.col)))
                    .Min();
                var unsafeRadius = Math.Max(2, Math.Max(ship.ExplosionRadius, placed.ExplosionRadius));
                if (chebyshev <= unsafeRadius) score -= 28;
            }
        }

        // Repeating neat rows/orientations is easy to profile; mix both axes and anchors.
        score -= board.PlacedShips.Count(placed => placed.Row == row) * 2.2;
        score -= board.PlacedShips.Count(placed => placed.Col == col) * 1.7;
        score -= board.PlacedShips.Count(placed => placed.Orientation == orientation) * 0.8;
        return score;
    }

    private static double ScoreCompletedPlacement(Board board)
    {
        var ships = board.PlacedShips;
        var score = ships.SelectMany(ship => ship.GetOccupiedCells())
            .Count(cell => cell.row is 0 or 7 or 8 or 9 || cell.col is 0 or 9) * 1.3;
        score += ships.Select(ship => ship.Row).Distinct().Count() * 3.2;
        score += ships.Select(ship => ship.Col).Distinct().Count() * 2.1;
        score += ships.Select(ship => ship.Orientation).Distinct().Count() * 4;
        score -= ships.GroupBy(ship => ship.Row).Sum(group => Math.Max(0, group.Count() - 2)) * 3;
        score -= ships.GroupBy(ship => ship.Col).Sum(group => Math.Max(0, group.Count() - 2)) * 2;
        return score;
    }

    private static bool IsPoisonSafe(
        Board board,
        Ship candidate,
        int row,
        int col,
        Orientation orientation)
    {
        var candidateCells = candidate.GetOccupiedCells(row, col, orientation).ToHashSet();
        foreach (var source in board.PlacedShips.Where(ship =>
                     !ship.IsDestroyed && ship.Abilities.Contains("poison_cone")))
        {
            if (BattleshipGameEngine.GetPoisonConeCells(
                    source, source.Row, source.Col, source.Orientation).Any(candidateCells.Contains))
                return false;
        }
        if (!candidate.Abilities.Contains("poison_cone")) return true;
        var cone = BattleshipGameEngine.GetPoisonConeCells(
            candidate, row, col, orientation).ToHashSet();
        return board.PlacedShips.SelectMany(ship => ship.GetOccupiedCells()).All(cell => !cone.Contains(cell));
    }

    private static void PutShip(
        Board board,
        Ship ship,
        int row,
        int col,
        Orientation orientation)
    {
        ship.Row = row;
        ship.Col = col;
        ship.Orientation = orientation;
        ship.IsPlaced = true;
        foreach (var (cellRow, cellCol) in ship.GetOccupiedCells())
            board.Grid[cellRow, cellCol].ShipRef = ship;
        board.PlacedShips.Add(ship);
    }

    private static void ResetBoard(BattleshipPlayer bot)
    {
        bot.Board = new Board();
        foreach (var ship in bot.Fleet)
        {
            ship.Row = -1;
            ship.Col = -1;
            ship.IsPlaced = false;
        }
    }

    // ── Loadout, weapons and targeting ─────────────────────────────

    public static void ConfigureLoadout(BattleshipPlayer bot, BattleshipBotVersion version)
    {
        var catapults = bot.Fleet.SelectMany(ship => ship.Weapons)
            .Where(weapon => weapon.Type == WeaponType.Tetracatapult)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        if (catapults.Count > 0)
        {
            var whiteStoneCount = version == BattleshipBotVersion.V3
                ? Math.Clamp((catapults.Count + 1) / 2 + Random.Shared.Next(-1, 2), 1, catapults.Count)
                : Random.Shared.Next(0, catapults.Count + 1);
            for (var index = 0; index < catapults.Count; index++)
                catapults[index].ConfiguredShotType = index < whiteStoneCount
                    ? ShotType.WhiteStone
                    : ShotType.Buckshot;
        }
        bot.UseSharedTetracatapultAmmo = true;
        // Ordinary summons gain tempo and create a Penalty dilemma; boats buy one safe
        // information-free shot. Keep both strategically present across generated fleets.
        bot.UseGhostSummons = Random.Shared.NextDouble() <
                              (version == BattleshipBotVersion.V3 ? 0.48 : 0.58);
    }

    public static (string weaponType, string shotType, string weaponId) ChooseWeapon(
        BattleshipPlayer bot,
        BattleshipBotVersion version,
        BotObservation observation,
        IReadOnlyList<Weapon> availableWeapons,
        int shotCount)
    {
        var usable = availableWeapons
            .Where(weapon => weapon.AimSpeed <= bot.RevealedCellCount)
            .ToList();
        if (usable.Count == 0) return ("Ballista", "Ballista", null);

        var captured = bot.Board.PlacedShips.Any(ship =>
            !ship.IsDestroyed && ship.Statuses.Contains(ShipStatusType.Capture));
        if (captured)
        {
            var decisive = usable.OrderBy(weapon => weapon.Type switch
            {
                WeaponType.EvilIncendiary => 0,
                WeaponType.Incendiary => 1,
                WeaponType.Neptune => 2,
                WeaponType.Tetracatapult when weapon.ConfiguredShotType == ShotType.WhiteStone => 3,
                WeaponType.Ballista => 4,
                _ => 4,
            }).First();
            return WeaponSelection(decisive);
        }

        var canAvoidSummonPenalty = usable.Any(weapon =>
            weapon.Type is WeaponType.GreekFire or WeaponType.EvilGreekFire);
        var enemySummon = ChooseEnemySummonTarget(
            bot, shotCount, canAvoidSummonPenalty);
        if (enemySummon != null)
        {
            var boardingBurnResist = enemySummon.Summon.IsBoardingShip &&
                                     (enemySummon.Summon.BoardingStatuses.Contains(
                                          ShipStatusType.BurnResist) ||
                                      enemySummon.Summon.BoardingAbilities.Contains("burn_resist"));
            if (!boardingBurnResist)
            {
                var greek = usable.FirstOrDefault(weapon => weapon.Type == WeaponType.EvilGreekFire)
                            ?? usable.FirstOrDefault(weapon => weapon.Type == WeaponType.GreekFire);
                if (greek != null) return WeaponSelection(greek);
            }
            if (enemySummon.Summon.IsBoardingShip)
            {
                // Converted hulls retain real armor and may be Ballista-immune. Their
                // multi-deck silhouette is public, so spend a decisive legal projectile.
                var whiteStone = usable.FirstOrDefault(weapon =>
                    weapon.Type == WeaponType.Tetracatapult &&
                    weapon.ConfiguredShotType == ShotType.WhiteStone);
                if (whiteStone != null) return WeaponSelection(whiteStone);
                if (!boardingBurnResist)
                {
                    var incendiary = usable.FirstOrDefault(weapon =>
                        weapon.Type is WeaponType.EvilIncendiary or WeaponType.Incendiary);
                    if (incendiary != null) return WeaponSelection(incendiary);
                }

                var boardingBuckshot = usable.FirstOrDefault(weapon =>
                    weapon.Type == WeaponType.Tetracatapult &&
                    weapon.ConfiguredShotType == ShotType.Buckshot);
                if (boardingBuckshot != null) return WeaponSelection(boardingBuckshot);
                if (!enemySummon.Summon.BoardingAbilities.Contains("ballista_immune"))
                {
                    var boardingBallista = usable.FirstOrDefault(weapon =>
                        weapon.Type == WeaponType.Ballista);
                    if (boardingBallista != null) return WeaponSelection(boardingBallista);
                }
            }
        }

        var hasLiveTarget = observation.Cells.Cast<BotCellObservation>()
            .Any(cell => IsDirectLiveTarget(observation, cell) ||
                         cell.IsScratched && !cell.IsCaptured &&
                         !observation.IsStaleEvidence(cell));
        var hasDestroyedDeckOnLivingHull = observation.Cells.Cast<BotCellObservation>()
            .Any(cell => cell.IsDestroyed && !cell.IsShipSunk &&
                         !cell.IsCaptured &&
                         !observation.IsStaleEvidence(cell));
        var hasNeptuneTarget = observation.Cells.Cast<BotCellObservation>()
            .Any(cell => !cell.HasElectricCharge && !cell.IsCaptured &&
                         !observation.IsStaleEvidence(cell) &&
                         (IsDirectLiveTarget(observation, cell) ||
                          cell.IsDestroyed && !cell.IsShipSunk));

        var neptune = usable.FirstOrDefault(weapon =>
            weapon.Type == WeaponType.Neptune);
        if (neptune != null && hasNeptuneTarget)
            return WeaponSelection(neptune);

        if (hasDestroyedDeckOnLivingHull)
        {
            var evilIncendiary = usable.FirstOrDefault(weapon =>
                weapon.Type == WeaponType.EvilIncendiary &&
                CanReachKnownTarget(bot, weapon, observation, includeDestroyed: true,
                    excludeBurnResist: true));
            if (evilIncendiary != null) return WeaponSelection(evilIncendiary);
        }
        if (hasLiveTarget)
        {
            var incendiary = usable.FirstOrDefault(weapon =>
                weapon.Type is (WeaponType.EvilIncendiary or WeaponType.Incendiary) &&
                CanReachKnownTarget(bot, weapon, observation, includeDestroyed: false,
                    excludeBurnResist: true));
            if (incendiary != null &&
                (version == BattleshipBotVersion.V3 || Random.Shared.NextDouble() < 0.72))
                return WeaponSelection(incendiary);

            var whiteStone = usable.FirstOrDefault(weapon =>
                weapon.Type == WeaponType.Tetracatapult &&
                weapon.ConfiguredShotType == ShotType.WhiteStone &&
                CanReachKnownTarget(bot, weapon, observation, includeDestroyed: false,
                    excludeBurnResist: false));
            if (whiteStone != null && (version == BattleshipBotVersion.V3 || Random.Shared.NextDouble() < 0.6))
                return WeaponSelection(whiteStone);
        }

        var buckshot = usable.FirstOrDefault(weapon =>
            weapon.Type == WeaponType.Tetracatapult &&
            weapon.ConfiguredShotType == ShotType.Buckshot);
        if (buckshot != null && !hasLiveTarget &&
            (version == BattleshipBotVersion.V3
                ? bot.RevealedCellCount < 58
                : Random.Shared.NextDouble() < 0.45))
            return WeaponSelection(buckshot);

        var ballista = usable.FirstOrDefault(weapon => weapon.Type == WeaponType.Ballista);
        return ballista != null ? WeaponSelection(ballista) : WeaponSelection(usable[0]);
    }

    private static bool CanReachKnownTarget(
        BattleshipPlayer bot,
        Weapon weapon,
        BotObservation observation,
        bool includeDestroyed,
        bool excludeBurnResist)
    {
        var source = bot.Board.PlacedShips.FirstOrDefault(ship => ship.Id == weapon.ShipId);
        var blocksRearRows = source is { Range: RangeClass.Far, Row: >= 8 };
        return observation.Cells.Cast<BotCellObservation>().Any(cell =>
            !cell.IsCaptured && !cell.IsShipSunk &&
            (!excludeBurnResist || !cell.IsBurnResistMarked) &&
            (!blocksRearRows || cell.Row < 8) &&
            !observation.IsStaleEvidence(cell) &&
            (IsDirectLiveTarget(observation, cell) ||
             cell.IsScratched || includeDestroyed && cell.IsDestroyed));
    }

    private static (string weaponType, string shotType, string weaponId) WeaponSelection(Weapon weapon)
    {
        var shotType = weapon.Type switch
        {
            WeaponType.Tetracatapult => weapon.ConfiguredShotType ?? ShotType.Buckshot,
            WeaponType.Neptune => ShotType.Neptune,
            WeaponType.Incendiary => ShotType.Incendiary,
            WeaponType.EvilIncendiary => ShotType.EvilIncendiary,
            WeaponType.GreekFire => ShotType.GreekFire,
            WeaponType.EvilGreekFire => ShotType.EvilGreekFire,
            _ => ShotType.Ballista,
        };
        return (weapon.Type.ToString(), shotType.ToString(),
            weapon.Type == WeaponType.Ballista ? null : weapon.Id);
    }

    public static BotSummonTarget ChooseEnemySummonTarget(
        BattleshipPlayer bot,
        int shotCount,
        bool avoidsPenalty)
    {
        var candidates = bot.Board.Grid.Cast<Cell>()
            .Where(cell => cell.SummonRef is { IsAlive: true } summon &&
                           summon.OwnerId != bot.DiscordId)
            .Select(cell =>
            {
                var summon = cell.SummonRef;
                var penaltyRisk = !avoidsPenalty && !summon.IsBoardingShip &&
                                  !summon.IsGhost && cell.Row <= 2 &&
                                  shotCount - summon.SpawnedAtShot > 1;
                return (target: new BotSummonTarget(summon, cell.Row, cell.Col),
                    score: SummonThreat(summon) + cell.Row * 2 - (penaltyRisk ? 95 : 0) -
                           (summon.Type == SummonType.Brander
                               ? BranderFriendlyFireRisk(bot, cell.Row, cell.Col, summon)
                               : 0));
            })
            .OrderByDescending(value => value.score)
            .ToList();
        return candidates.Count > 0 && candidates[0].score >= 45
            ? candidates[0].target
            : null;
    }

    private static int SummonThreat(Summon summon)
    {
        if (summon.IsBoardingShip) return 115 + summon.SourceShipDeckCount * 8;
        return summon.Type switch
        {
            SummonType.CursedBoat => 110,
            SummonType.Brander => 100,
            SummonType.Ram => 85,
            SummonType.PirateBoat => 78,
            SummonType.Scout => 55,
            _ => 40,
        };
    }

    private static int BranderFriendlyFireRisk(
        BattleshipPlayer bot,
        int row,
        int col,
        Summon targetBrander)
    {
        var ships = new HashSet<string>(StringComparer.Ordinal);
        var summons = new HashSet<string>(StringComparer.Ordinal);
        var risk = 0;
        for (var targetRow = Math.Max(0, row - 1); targetRow <= Math.Min(9, row + 1); targetRow++)
        for (var targetCol = Math.Max(0, col - 1); targetCol <= Math.Min(9, col + 1); targetCol++)
        {
            var cell = bot.Board.Grid[targetRow, targetCol];
            if (cell.ShipRef is { IsDestroyed: false } ship && ships.Add(ship.Id))
            {
                if (ship.Statuses.Contains(ShipStatusType.Capture)) risk -= 65;
                else if (ship.Statuses.Contains(ShipStatusType.BurnResist)) risk += 12;
                else risk += 150 + ship.Cost * 3 + ship.Decks.Count * 12;
            }

            if (cell.SummonRef is { IsAlive: true } summon &&
                summon.Id != targetBrander.Id && summons.Add(summon.Id))
                risk += summon.OwnerId == bot.DiscordId
                    ? 55 + (summon.IsBoardingShip ? summon.SourceShipDeckCount * 20 : 0)
                    : -30;
        }
        return risk;
    }

    public static (int row, int col) ChooseTarget(
        BattleshipPlayer bot,
        BattleshipBotVersion version,
        ShotType shotType,
        BotObservation observation)
    {
        var captured = bot.Board.PlacedShips
            .Where(ship => !ship.IsDestroyed && ship.Statuses.Contains(ShipStatusType.Capture))
            .SelectMany(ship => ship.Decks.Where(deck => !deck.IsDestroyed)
                .Select(deck => ship.GetDeckCell(deck, ship.Row, ship.Col, ship.Orientation)))
            .ToList();
        if (captured.Count > 0)
            return captured.OrderBy(_ => Random.Shared.Next()).First();

        if (shotType is ShotType.GreekFire or ShotType.EvilGreekFire)
            return ChooseGreekFireTarget(bot);

        var excluded = observation.Cells.Cast<BotCellObservation>()
            .Where(cell => cell.HasFriendlySummon)
            .Select(cell => (cell.Row, cell.Col)).ToHashSet();
        var blockedRows = GetBlockedRows(bot);
        foreach (var row in blockedRows)
            for (var col = 0; col < 10; col++) excluded.Add((row, col));

        if (shotType == ShotType.Ballista && version == BattleshipBotVersion.V3)
        {
            var branderTarget = ChooseBranderDetonationTarget(observation, bot);
            if (branderTarget.HasValue) return branderTarget.Value;
        }

        if (shotType == ShotType.EvilIncendiary)
        {
            var destroyed = observation.Cells.Cast<BotCellObservation>()
                .Where(cell => cell.IsDestroyed && !cell.IsShipSunk &&
                               !cell.IsCaptured && !cell.IsBurnResistMarked &&
                               !observation.IsStaleEvidence(cell) &&
                               !excluded.Contains((cell.Row, cell.Col)))
                .OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
            if (destroyed != null) return (destroyed.Row, destroyed.Col);
        }
        if (shotType is ShotType.Incendiary or ShotType.EvilIncendiary)
        {
            var fireTarget = observation.Cells.Cast<BotCellObservation>()
                .Where(cell =>
                    (IsDirectLiveTarget(observation, cell) ||
                     cell.IsScratched && !cell.IsCaptured &&
                     !observation.IsStaleEvidence(cell)) &&
                    !cell.IsBurnResistMarked &&
                    !excluded.Contains((cell.Row, cell.Col)))
                .OrderByDescending(cell => cell.IsScratched)
                .ThenBy(_ => Random.Shared.Next())
                .FirstOrDefault();
            if (fireTarget != null) return (fireTarget.Row, fireTarget.Col);
        }

        if (shotType == ShotType.Neptune)
        {
            var neptuneTarget = observation.Cells.Cast<BotCellObservation>()
                .Where(cell => !cell.HasElectricCharge && !cell.IsCaptured &&
                               !observation.IsStaleEvidence(cell) &&
                               !excluded.Contains((cell.Row, cell.Col)) &&
                               (IsDirectLiveTarget(observation, cell) ||
                                cell.IsDestroyed && !cell.IsShipSunk))
                .OrderByDescending(cell => cell.IsDestroyed)
                .ThenBy(_ => Random.Shared.Next())
                .FirstOrDefault();
            if (neptuneTarget != null) return (neptuneTarget.Row, neptuneTarget.Col);
            foreach (var charged in observation.Cells.Cast<BotCellObservation>()
                         .Where(cell => cell.HasElectricCharge))
                excluded.Add((charged.Row, charged.Col));
        }

        var hasEvidence = observation.Cells.Cast<BotCellObservation>()
            .Any(cell => IsUnresolvedEvidence(observation, cell));
        var heat = version == BattleshipBotVersion.V3
            ? BuildProbabilityHeat(observation, excluded, hasEvidence)
            : BuildV2Heat(observation, excluded, hasEvidence);

        if (shotType == ShotType.Buckshot)
            return ChooseBuckshotTarget(heat, observation, excluded);

        if (shotType == ShotType.Ballista)
        {
            foreach (var cell in observation.Cells.Cast<BotCellObservation>()
                         .Where(cell => cell.WasDodge))
                heat[cell.Row, cell.Col] = 0;
        }

        return PickHeatTarget(heat, observation, excluded, shotType);
    }

    private static HashSet<int> GetBlockedRows(BattleshipPlayer bot)
    {
        var rows = new HashSet<int>();
        if (bot.SelectedWeapon?.ShipId == null) return rows;
        var source = bot.Board.PlacedShips.FirstOrDefault(ship => ship.Id == bot.SelectedWeapon.ShipId);
        if (source is { Range: RangeClass.Far, Row: >= 8 })
        {
            rows.Add(8);
            rows.Add(9);
        }
        return rows;
    }

    private static (int row, int col) ChooseGreekFireTarget(BattleshipPlayer bot)
    {
        var threat = ChooseEnemySummonTarget(bot, int.MaxValue, avoidsPenalty: true);
        if (threat != null) return (threat.Row, threat.Col);

        // Defensive area denial is legal only on the bot's board. Choose an empty central
        // transit cell and never burn an allied ship merely to spend ammunition.
        var safe = new List<(int row, int col, int score)>();
        for (var row = 2; row < 9; row++)
        for (var col = 0; col < 10; col++)
        {
            var cell = bot.Board.Grid[row, col];
            if (cell.ShipRef != null || cell.IsBurning) continue;
            safe.Add((row, col, 12 - Math.Abs(5 - row) - Math.Abs(4 - col)));
        }
        if (safe.Count == 0) return (0, 0);
        var best = safe.OrderByDescending(value => value.score).Take(8).ToList();
        var chosen = best[Random.Shared.Next(best.Count)];
        return (chosen.row, chosen.col);
    }

    private static (int row, int col)? ChooseBranderDetonationTarget(
        BotObservation observation,
        BattleshipPlayer bot)
    {
        var heat = BuildProbabilityHeat(observation, new HashSet<(int, int)>(), false);
        var bestSingle = heat.Cast<double>().DefaultIfEmpty(0).Max();
        var blockedRows = GetBlockedRows(bot);
        foreach (var brander in bot.Summons.Where(summon =>
                     summon is { IsAlive: true, IsGhost: false, Type: SummonType.Brander } && summon.Row >= 2))
        {
            if (blockedRows.Contains(brander.Row)) continue;
            var local = 0.0;
            var knownTarget = false;
            var friendlyCollateral = false;
            for (var row = Math.Max(0, brander.Row - 1); row <= Math.Min(9, brander.Row + 1); row++)
            for (var col = Math.Max(0, brander.Col - 1); col <= Math.Min(9, brander.Col + 1); col++)
            {
                local += heat[row, col];
                knownTarget |= IsDirectLiveTarget(observation, observation.Cells[row, col]);
                friendlyCollateral |= (row != brander.Row || col != brander.Col) &&
                                      observation.Cells[row, col].HasFriendlySummon;
            }
            if (!friendlyCollateral &&
                (knownTarget || bestSingle > 0 && local >= bestSingle * 4.4))
                return (brander.Row, brander.Col);
        }
        return null;
    }

    private static double[,] BuildV2Heat(
        BotObservation observation,
        HashSet<(int row, int col)> excluded,
        bool targetMode)
    {
        var heat = new double[10, 10];
        var sizes = new[] { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
        var evidence = observation.Cells.Cast<BotCellObservation>()
            .Where(cell => IsUnresolvedEvidence(observation, cell))
            .Select(cell => (cell.Row, cell.Col)).ToHashSet();
        foreach (var size in sizes)
        foreach (var vertical in new[] { false, true })
        for (var row = 0; row < 10; row++)
        for (var col = 0; col < 10; col++)
        {
            var cells = Enumerable.Range(0, size)
                .Select(offset => vertical ? (row + offset, col) : (row, col + offset)).ToList();
            if (cells.Any(cell => cell.Item1 >= 10 || cell.Item2 >= 10)) continue;
            if (cells.Any(cell => observation.Cells[cell.Item1, cell.Item2].IsShipSunk)) continue;
            if (cells.Any(cell => observation.Cells[cell.Item1, cell.Item2].IsKnownWater) &&
                !observation.MatchesAutoDodgePlacement(cells)) continue;
            var covered = cells.Count(cell => evidence.Contains(cell));
            if (targetMode && evidence.Count > 0 && covered == 0) continue;
            foreach (var (cellRow, cellCol) in cells)
                heat[cellRow, cellCol] += 1 + covered * 5;
        }
        foreach (var cell in observation.Cells.Cast<BotCellObservation>())
        {
            if (excluded.Contains((cell.Row, cell.Col)) ||
                IsBlockedKnownWater(observation, cell) ||
                cell.IsShipSunk || cell.IsCaptured || cell.WasManeuverDodge)
                heat[cell.Row, cell.Col] = 0;
            else if (cell.IsScratched && !cell.IsCaptured &&
                     !observation.IsStaleEvidence(cell))
                heat[cell.Row, cell.Col] += 5000;
            else if (IsDirectLiveTarget(observation, cell))
                heat[cell.Row, cell.Col] += 3500;
        }
        return heat;
    }

    private static (int row, int col) ChooseBuckshotTarget(
        double[,] heat,
        BotObservation observation,
        HashSet<(int row, int col)> excluded)
    {
        var candidates = new List<(int row, int col, double score)>();
        for (var row = 0; row < 9; row++)
        for (var col = 0; col < 9; col++)
        {
            var score = 0.0;
            for (var dRow = 0; dRow < 2; dRow++)
            for (var dCol = 0; dCol < 2; dCol++)
            {
                var target = (row + dRow, col + dCol);
                if (excluded.Contains(target)) score -= 10000;
                else if (IsBlockedKnownWater(
                             observation, observation.Cells[target.Item1, target.Item2])) score -= 2;
                else score += heat[target.Item1, target.Item2] + 2;
            }
            candidates.Add((row, col, score));
        }
        var top = candidates.OrderByDescending(value => value.score).Take(6).ToList();
        var selected = top[WeightedTopChoice(top.Select(value => value.score).ToList())];
        return (selected.row, selected.col);
    }

    private static (int row, int col) PickHeatTarget(
        double[,] heat,
        BotObservation observation,
        HashSet<(int row, int col)> excluded,
        ShotType shotType)
    {
        var candidates = new List<(int row, int col, double score)>();
        for (var row = 0; row < 10; row++)
        for (var col = 0; col < 10; col++)
        {
            if (excluded.Contains((row, col))) continue;
            var cell = observation.Cells[row, col];
            if (cell.IsShipSunk || cell.IsCaptured ||
                IsBlockedKnownWater(observation, cell)) continue;
            if (shotType == ShotType.Ballista && cell.WasDodge) continue;
            if (cell.IsDestroyed && shotType != ShotType.EvilIncendiary) continue;
            candidates.Add((row, col, heat[row, col]));
        }

        if (candidates.Count == 0)
        {
            // A hidden manual maneuver may legally enter a cell that was already resolved as
            // water. Once every ordinary target is exhausted, search that old water again.
            // This uses only remembered public evidence and keeps the bot from stranding its
            // own turn when the surviving hull is concealed behind an old miss marker.
            for (var row = 0; row < 10; row++)
            for (var col = 0; col < 10; col++)
            {
                var cell = observation.Cells[row, col];
                if (!excluded.Contains((row, col)) &&
                    !cell.IsCaptured &&
                    !cell.IsShipSunk &&
                    !cell.IsDestroyed &&
                    (!cell.IsKnownWater || !observation.WasMovementWaterRechecked(cell)) &&
                    (shotType != ShotType.Ballista || !cell.WasDodge))
                    candidates.Add((row, col, observation.MovementFallbackScore(cell)));
            }
        }
        if (candidates.Count == 0)
        {
            // No hidden hull can have survived a complete recheck pass. Keep the policy total
            // for defensive recovery from legacy/inconsistent saves while normal win settlement
            // catches up, instead of throwing out of the bot pump.
            for (var row = 0; row < 10; row++)
            for (var col = 0; col < 10; col++)
            {
                var cell = observation.Cells[row, col];
                if (!excluded.Contains((row, col)) &&
                    !cell.IsCaptured &&
                    !cell.IsShipSunk &&
                    !cell.IsDestroyed &&
                    (shotType != ShotType.Ballista || !cell.WasDodge))
                    candidates.Add((row, col, 0.2));
            }
        }
        if (candidates.Count == 0)
            throw new InvalidOperationException("No legal honest-information bot target exists.");
        var top = candidates.OrderByDescending(value => value.score).Take(10).ToList();
        var selected = top[WeightedTopChoice(top.Select(value => value.score).ToList())];
        return (selected.row, selected.col);
    }

    // ── Summons and adaptive movement ──────────────────────────────

    public static (SummonType type, int lane)? ChooseSummonDeploy(
        BattleshipPlayer bot,
        BattleshipBotVersion version,
        BotObservation observation,
        BsGamePhase phase,
        string boardingPlayerId,
        int shotCount,
        bool mandatoryDeployment = false)
    {
        var waitingRam = bot.Summons.FirstOrDefault(summon =>
            summon is { IsAlive: true, Type: SummonType.Ram, WaitingForTurnBack: true });
        if (waitingRam != null)
            return ChooseRamReentry(observation, waitingRam);

        var finalRush = phase == BsGamePhase.Boarding && boardingPlayerId == bot.DiscordId;
        var threshold = 5 * (bot.SummonSlotsUsed + 1);
        if (!mandatoryDeployment && !finalRush &&
            (bot.RevealedCellCount < threshold ||
             shotCount - bot.LastSummonDeployShotCount < 2)) return null;

        var regions = bot.Fleet.Where(ship => !ship.Statuses.Contains(ShipStatusType.Capture))
            .SelectMany(ship => ship.Regions).ToHashSet();
        var available = new List<SummonType>();
        if (mandatoryDeployment
                ? bot.MandatoryBoardingSummonSlots > 0
                : bot.SummonSlotsUsed < bot.MaxSummonSlots)
        {
            if (regions.Contains(Region.West)) available.Add(SummonType.Ram);
            if (regions.Contains(Region.East)) available.Add(SummonType.Scout);
            if (regions.Contains(Region.South)) available.Add(SummonType.PirateBoat);
        }
        if (mandatoryDeployment
                ? bot.MandatoryBoardingBrander
                : !bot.BranderUsed && bot.Fleet.Any(ship =>
                    !ship.IsDestroyed && ship.Abilities.Contains("brander_summon")))
            available.Add(SummonType.Brander);
        if (available.Count == 0) return null;

        var heat = version == BattleshipBotVersion.V3
            ? BuildProbabilityHeat(observation, new HashSet<(int, int)>(), false)
            : BuildV2Heat(observation, new HashSet<(int, int)>(), false);
        var choices = new List<(SummonType type, int col, double score)>();
        foreach (var type in available)
        for (var col = 0; col < 10; col++)
        {
            if (observation.Cells[0, col].HasAnySummon) continue;
            var score = ScoreSummonLane(type, col, observation, heat, bot.RevealedCellCount);
            choices.Add((type, col, score + Random.Shared.NextDouble() * 4));
        }
        if (choices.Count == 0) return null;

        // Preserve a scarce Pirate use until it has a plausible 1–2 deck line unless this
        // is the mandatory final rush.
        if (!finalRush && choices.All(choice => choice.type == SummonType.PirateBoat) &&
            !observation.Cells.Cast<BotCellObservation>()
                .Any(cell => HasReliableShipEvidence(observation, cell)))
            return null;

        var top = choices.OrderByDescending(choice => choice.score)
            .Take(version == BattleshipBotVersion.V3 ? 6 : 12).ToList();
        var pick = top[WeightedTopChoice(top.Select(choice => choice.score).ToList())];
        return (pick.type, pick.col);
    }

    private static (SummonType type, int lane)? ChooseRamReentry(
        BotObservation observation,
        Summon ram)
    {
        if (ram.MoveDirection is Direction.Left or Direction.Right)
        {
            var entryCol = ram.MoveDirection == Direction.Right ? 9 : 0;
            var rows = Enumerable.Range(Math.Max(0, ram.Row - 1),
                    Math.Min(9, ram.Row + 1) - Math.Max(0, ram.Row - 1) + 1)
                .Where(row => !observation.Cells[row, entryCol].HasAnySummon)
                .ToList();
            return rows.Count == 0 ? null : (SummonType.Ram, rows[Random.Shared.Next(rows.Count)]);
        }
        var entryRow = ram.MoveDirection == Direction.Down ? 9 : 0;
        var cols = Enumerable.Range(Math.Max(0, ram.Col - 1),
                Math.Min(9, ram.Col + 1) - Math.Max(0, ram.Col - 1) + 1)
            .Where(col => !observation.Cells[entryRow, col].HasAnySummon)
            .ToList();
        return cols.Count == 0 ? null : (SummonType.Ram, cols[Random.Shared.Next(cols.Count)]);
    }

    private static double ScoreSummonLane(
        SummonType type,
        int col,
        BotObservation observation,
        double[,] heat,
        int revealedCount)
    {
        double score = type switch
        {
            SummonType.Scout => revealedCount < 25 ? 18 : 4,
            SummonType.Ram => 14,
            SummonType.PirateBoat => 9,
            SummonType.Brander => 12,
            _ => 5,
        };
        for (var row = 0; row < 10; row++)
        {
            var decay = 1.0 - row * 0.035;
            var cell = observation.Cells[row, col];
            if (cell.IsBurning) score -= 45;
            if (type == SummonType.Scout)
            {
                for (var dRow = -1; dRow <= 1; dRow++)
                for (var dCol = -1; dCol <= 1; dCol++)
                    if (observation.Cell(row + dRow, col + dCol) is { IsRevealed: false }) score += 0.45;
            }
            else
            {
                score += heat[row, col] * decay * (type == SummonType.Brander ? 0.055 : 0.035);
                if (HasReliableShipEvidence(observation, cell)) score += type switch
                {
                    SummonType.Ram => 22,
                    SummonType.PirateBoat => 28,
                    SummonType.Brander => 16,
                    _ => 4,
                };
            }
        }
        return score;
    }

    public static List<(string pendingId, int col)> ChoosePendingSummonDeploys(
        BattleshipPlayer bot,
        BattleshipBotVersion version,
        BotObservation observation,
        IReadOnlyDictionary<string, List<int>> legalColumnsByPendingId)
    {
        var heat = version == BattleshipBotVersion.V3
            ? BuildProbabilityHeat(observation, new HashSet<(int, int)>(), false)
            : BuildV2Heat(observation, new HashSet<(int, int)>(), false);
        var reserved = Enumerable.Range(0, 10)
            .Where(col => observation.Cells[0, col].HasAnySummon)
            .ToHashSet();
        var result = new List<(string pendingId, int col)>();
        foreach (var pending in bot.PendingSummons
                     .OrderByDescending(pending => pending.IsMandatoryBoarding)
                     .ThenByDescending(pending => pending.IsBoarding)
                     .ThenByDescending(BoardingDeploymentValue)
                     .ThenByDescending(pending => pending.Type is SummonType.CursedBoat or SummonType.Ram))
        {
            if (!pending.IsMandatoryBoarding && !pending.IsFree &&
                bot.SummonSlotsUsed >= bot.MaxSummonSlots) continue;
            var columns = (legalColumnsByPendingId.TryGetValue(pending.Id, out var legalColumns)
                    ? legalColumns
                    : new List<int>())
                .Where(col => !reserved.Contains(col)).ToList();
            if (columns.Count == 0) continue;
            var best = columns.Select(col => new
                {
                    Col = col,
                    Score = ScoreSummonLane(pending.Type, col, observation, heat, bot.RevealedCellCount) +
                            Random.Shared.NextDouble() * 3,
                })
                .OrderByDescending(value => value.Score).Take(4).ToList();
            var choice = best[WeightedTopChoice(best.Select(value => value.Score).ToList())].Col;
            result.Add((pending.Id, choice));
            reserved.Add(choice);
        }
        return result;
    }

    private static int BoardingDeploymentValue(PendingSummonDeploy pending)
    {
        if (!pending.IsBoarding) return 0;
        var value = pending.SourceShipDeckCount * 28 +
                    pending.BoardingDecks.Sum(deck => Math.Max(0, deck.CurrentHp)) * 7;
        if (pending.BoardingAbilities.Contains("ballista_immune")) value += 32;
        if (pending.BoardingAbilities.Contains("burn_resist") ||
            pending.BoardingStatuses.Contains(ShipStatusType.BurnResist)) value += 24;
        if (pending.BoardingAbilities.Contains("capture_reward")) value += 18;
        if (pending.BoardingAbilities.Contains("ram_immune")) value += 10;
        return value;
    }

    public static Direction ChooseCursedBoatDirection(
        BattleshipPlayer bot,
        Summon cursedBoat,
        BattleshipBotVersion version,
        BotObservation observation)
    {
        var heat = version == BattleshipBotVersion.V3
            ? BuildProbabilityHeat(observation, new HashSet<(int, int)>(), false)
            : BuildV2Heat(observation, new HashSet<(int, int)>(), false);
        var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
        return directions.Select(direction =>
            {
                var (dRow, dCol) = DirectionVector(direction);
                var score = 0.0;
                for (var step = 1; step < 10; step++)
                {
                    var row = cursedBoat.Row + dRow * step;
                    var col = cursedBoat.Col + dCol * step;
                    if (row is < 0 or >= 10 || col is < 0 or >= 10) break;
                    score += heat[row, col] * (1.0 - step * 0.06);
                    if (HasReliableShipEvidence(observation, observation.Cells[row, col]))
                        score += 24;
                    if (observation.Cells[row, col].IsBurning) score -= 40;
                }
                return (direction, score: score + Random.Shared.NextDouble() * 2);
            })
            .OrderByDescending(value => value.score)
            .First().direction;
    }

    public static int ChooseManeuverOption(
        BattleshipPlayer bot,
        Ship ship,
        IReadOnlyList<(int row, int col, Direction direction, int distance)> options,
        BattleshipBotVersion version)
    {
        if (options.Count == 0) return -1;
        if (version != BattleshipBotVersion.V3) return Random.Shared.Next(options.Count);
        var scored = options.Select((option, index) =>
        {
            var cells = ship.GetOccupiedCells(option.row, option.col, ship.Orientation);
            var score = cells.Sum(cell =>
            {
                var boardCell = bot.Board.GetCell(cell.row, cell.col);
                if (boardCell == null) return -1000;
                var value = boardCell.IsMiss ? 11 : boardCell.IsRevealed ? 4 : 0;
                if (cell.row is 0 or 7 || cell.col is 0 or 9) value += 3;
                if (boardCell.IsBurning) value -= 80;
                return (double)value;
            });
            score += option.distance * 2.5;
            score += Random.Shared.NextDouble() * 3;
            return (index, score);
        }).OrderByDescending(value => value.score).ToList();
        return scored[WeightedTopChoice(scored.Select(value => value.score).ToList())].index;
    }

    public static Ship ChoosePirateRestore(
        BattleshipPlayer bot,
        BattleshipBotVersion version,
        BsGamePhase phase,
        string boardingPlayerId,
        int shotCount)
    {
        if (!bot.Fleet.SelectMany(ship => ship.Regions).Contains(Region.South) ||
            bot.SummonSlotsUsed >= bot.MaxSummonSlots) return null;
        var finalRush = phase == BsGamePhase.Boarding && boardingPlayerId == bot.DiscordId;
        var threshold = 5 * (bot.SummonSlotsUsed + 1);
        if (!finalRush && (bot.RevealedCellCount < threshold ||
                          shotCount - bot.LastSummonDeployShotCount < 2)) return null;
        return bot.Board.PlacedShips
            .Where(ship => ship.Statuses.Contains(ShipStatusType.Devastated) &&
                           !ship.Statuses.Contains(ShipStatusType.Capture))
            .OrderByDescending(ship => ship.Cost + ship.Decks.Sum(deck => deck.MaxHp) * 2)
            .FirstOrDefault();
    }

    private static (int row, int col) DirectionVector(Direction direction) => direction switch
    {
        Direction.Up => (-1, 0),
        Direction.Down => (1, 0),
        Direction.Left => (0, -1),
        Direction.Right => (0, 1),
        _ => (0, 0),
    };

    // ── Probability model over the sanitized observation ──────────

    private static double[,] BuildProbabilityHeat(
        BotObservation observation,
        HashSet<(int row, int col)> excluded,
        bool targetMode)
    {
        var heat = new double[10, 10];
        var knownFleet = AnalyzeKnownFleet(observation);
        var remainingSlots = new Dictionary<int, int>
        {
            [1] = FleetValidator.Template[1],
            [2] = FleetValidator.Template[2],
            [3] = FleetValidator.Template[3],
            [4] = FleetValidator.Template[4],
        };

        // Exact names are available only while a Mast is operational. A known live special
        // hull reserves its original template slot; a genuinely finished hull consumes it.
        foreach (var (deckCount, count) in knownFleet.ReservedOrFinishedSlots)
            remainingSlots[deckCount] = Math.Max(0, remainingSlots[deckCount] - count);

        var evidence = observation.Cells.Cast<BotCellObservation>()
            .Where(cell => IsUnresolvedEvidence(observation, cell))
            .Select(cell => (cell.Row, cell.Col))
            .ToHashSet();
        var sunkCells = observation.Cells.Cast<BotCellObservation>()
            .Where(cell => cell.IsShipSunk)
            .Select(cell => (cell.Row, cell.Col))
            .ToHashSet();

        if (knownFleet.LiveMatryoshkaDeckCount is { } matryoshkaDeckCount)
        {
            var definition = ShipCatalog.GetById("russian_matryoshka");
            if (definition != null)
            {
                AddProbabilityPlacements(
                    heat,
                    observation,
                    excluded,
                    targetMode,
                    evidence,
                    sunkCells,
                    definition,
                    ShipCatalog.CreateMatryoshkaStageShip(matryoshkaDeckCount),
                    prior: 1,
                    multiplicity: 1,
                    ignoreSunkHalo: true);
            }
        }

        if (knownFleet.DestroyedAssemblyComponents is { } destroyedComponents)
        {
            var definition = ShipCatalog.GetById("famous_assembling_ship");
            if (definition != null)
            {
                var isAssembled = destroyedComponents >= 2;
                var prototype = isAssembled
                    ? ShipCatalog.CreateAssembledShip(definition)
                    : ShipCatalog.CreateAssemblyComponents(definition)[0];
                AddProbabilityPlacements(
                    heat,
                    observation,
                    excluded,
                    targetMode,
                    evidence,
                    sunkCells,
                    definition,
                    prototype,
                    prior: 1,
                    multiplicity: isAssembled ? 1 : 3 - destroyedComponents,
                    ignoreSunkHalo: isAssembled);
            }
        }

        foreach (var (deckCount, slotCount) in remainingSlots.Where(value => value.Value > 0))
        {
            var definitions = ShipCatalog.AllShips
                .Where(definition =>
                    definition.DeckCount == deckCount &&
                    definition.Factions.Contains(observation.Faction))
                .Select(definition => (
                    definition,
                    weight: DefinitionPrior(definition) *
                            FleetCompatibilityPrior(definition, knownFleet, observation.Faction)))
                .Where(candidate => candidate.weight > 0)
                .ToList();
            if (definitions.Count == 0) continue;
            var definitionWeightTotal = definitions.Sum(candidate => candidate.weight);

            foreach (var (definition, definitionWeight) in definitions)
            {
                // The assembling replacement occupies one three-deck fleet slot but starts as
                // three independent one-cell hulls. Without a public component death, preserve
                // that catalog geometry instead of pretending it is an ordinary straight ship.
                var isAssembly = definition.Id == "famous_assembling_ship";
                var prototype = isAssembly
                    ? ShipCatalog.CreateAssemblyComponents(definition)[0]
                    : ShipCatalog.CreateShip(definition);
                AddProbabilityPlacements(
                    heat,
                    observation,
                    excluded,
                    targetMode,
                    evidence,
                    sunkCells,
                    definition,
                    prototype,
                    prior: slotCount * definitionWeight / definitionWeightTotal,
                    multiplicity: isAssembly ? 3 : 1);
            }
        }

        // Direct observations dominate inference; scratched armor is the most urgent target.
        foreach (var cell in observation.Cells.Cast<BotCellObservation>())
        {
            if (excluded.Contains((cell.Row, cell.Col)))
            {
                heat[cell.Row, cell.Col] = 0;
                continue;
            }
            if (cell.IsScratched && !cell.IsShipSunk && !cell.IsCaptured &&
                !observation.IsStaleEvidence(cell))
                heat[cell.Row, cell.Col] += 10000;
            else if (IsDirectLiveTarget(observation, cell))
                heat[cell.Row, cell.Col] += 6500;
            else if (IsBlockedKnownWater(observation, cell) ||
                     cell.IsShipSunk || cell.WasManeuverDodge)
                heat[cell.Row, cell.Col] = 0;
        }

        return heat;
    }

    private sealed class KnownFleetPosterior
    {
        public Dictionary<int, int> ReservedOrFinishedSlots { get; } = new();
        public HashSet<Region> KnownRegions { get; } = new();
        public int KnownCost { get; set; }
        public int? LiveMatryoshkaDeckCount { get; set; }
        public int? DestroyedAssemblyComponents { get; set; }

        public void RegisterIdentity(ShipDefinition definition)
        {
            KnownCost += Math.Max(0, definition.Cost);
            KnownRegions.UnionWith(definition.Regions.Where(region => region != Region.Tetracor));
        }

        public void ReserveOrFinish(int deckCount) =>
            ReservedOrFinishedSlots[deckCount] =
                ReservedOrFinishedSlots.GetValueOrDefault(deckCount) + 1;
    }

    private static KnownFleetPosterior AnalyzeKnownFleet(BotObservation observation)
    {
        var posterior = new KnownFleetPosterior();
        var matryoshka = ShipCatalog.GetById("russian_matryoshka");
        var assembling = ShipCatalog.GetById("famous_assembling_ship");

        var matryoshkaSuccessor = observation.KnownSunkNames
            .Select(name => MatryoshkaSuccessorDeckCount(name, matryoshka))
            .Where(deckCount => deckCount.HasValue)
            .Select(deckCount => deckCount.Value)
            .DefaultIfEmpty(-1)
            .Min();
        if (matryoshkaSuccessor >= 0 && matryoshka != null &&
            matryoshka.Factions.Contains(observation.Faction))
        {
            posterior.RegisterIdentity(matryoshka);
            posterior.ReserveOrFinish(matryoshka.DeckCount);
            if (matryoshkaSuccessor > 0)
                posterior.LiveMatryoshkaDeckCount = matryoshkaSuccessor;
        }

        var assemblyComponentNames = assembling == null
            ? new List<string>()
            : observation.KnownSunkNames
                .Where(name => IsAssemblyComponentName(name, assembling))
                .ToList();
        var assembledHullFinished = assembling != null && observation.KnownSunkNames.Any(name =>
            IsExactDefinitionName(name, assembling));
        if ((assemblyComponentNames.Count > 0 || assembledHullFinished) && assembling != null &&
            assembling.Factions.Contains(observation.Faction))
        {
            posterior.RegisterIdentity(assembling);
            posterior.ReserveOrFinish(assembling.DeckCount);
            if (!assembledHullFinished && assemblyComponentNames.Count < 3)
                posterior.DestroyedAssemblyComponents = assemblyComponentNames.Count;
        }

        foreach (var name in observation.KnownSunkNames)
        {
            if (MatryoshkaSuccessorDeckCount(name, matryoshka).HasValue ||
                assembling != null &&
                (IsAssemblyComponentName(name, assembling) || IsExactDefinitionName(name, assembling)))
                continue;

            var definition = FindDefinitionByVisibleName(name, observation.Faction);
            if (definition == null) continue;
            posterior.RegisterIdentity(definition);
            posterior.ReserveOrFinish(definition.DeckCount);
        }

        return posterior;
    }

    private static int? MatryoshkaSuccessorDeckCount(
        string visibleName,
        ShipDefinition matryoshka)
    {
        if (matryoshka == null) return null;
        if (IsExactDefinitionName(visibleName, matryoshka)) return 3;
        for (var destroyedStage = 3; destroyedStage >= 1; destroyedStage--)
        {
            var stage = ShipCatalog.CreateMatryoshkaStageShip(destroyedStage);
            if (visibleName.Equals(stage.Name, StringComparison.Ordinal))
                return destroyedStage - 1;
        }
        return null;
    }

    private static bool IsAssemblyComponentName(
        string visibleName,
        ShipDefinition definition) =>
        visibleName.StartsWith("Собирающаяся палуба ", StringComparison.Ordinal) ||
        (visibleName.StartsWith(definition.Name + " ", StringComparison.Ordinal) &&
         visibleName.Contains("част", StringComparison.OrdinalIgnoreCase)) ||
        (!string.IsNullOrWhiteSpace(definition.NameRu) &&
         visibleName.StartsWith(definition.NameRu + " ", StringComparison.Ordinal) &&
         visibleName.Contains("част", StringComparison.OrdinalIgnoreCase));

    private static bool IsExactDefinitionName(string visibleName, ShipDefinition definition) =>
        visibleName.Equals(definition.Name, StringComparison.Ordinal) ||
        visibleName.Equals(definition.NameRu, StringComparison.Ordinal);

    private static double FleetCompatibilityPrior(
        ShipDefinition candidate,
        KnownFleetPosterior knownFleet,
        Faction faction)
    {
        var remainingBudget = FleetValidator.GetBudget(faction) - knownFleet.KnownCost;
        if (candidate.Cost > remainingBudget) return 0;

        var candidateRegions = candidate.Regions
            .Where(region => region != Region.Tetracor)
            .ToHashSet();
        var newRegionCount = candidateRegions.Count(region => !knownFleet.KnownRegions.Contains(region));
        if (knownFleet.KnownRegions.Count + newRegionCount > FleetValidator.MaxRegions) return 0;

        // Legal fleets may leave coins and region slots unused. Keep every compatible candidate,
        // applying only a mild pressure toward identities that fit the already-known composition.
        var budgetFit = candidate.Cost == 0
            ? 1.08
            : 0.72 + 0.28 * Math.Max(0, remainingBudget - candidate.Cost) /
              Math.Max(1, remainingBudget);
        var regionFit = Math.Pow(0.86, newRegionCount);
        return budgetFit * regionFit;
    }

    private static void AddProbabilityPlacements(
        double[,] heat,
        BotObservation observation,
        HashSet<(int row, int col)> excluded,
        bool targetMode,
        HashSet<(int row, int col)> evidence,
        HashSet<(int row, int col)> sunkCells,
        ShipDefinition definition,
        Ship prototype,
        double prior,
        int multiplicity,
        bool ignoreSunkHalo = false)
    {
        var blockingSunkCells = ignoreSunkHalo
            ? new HashSet<(int row, int col)>()
            : sunkCells;
        foreach (var orientation in Enum.GetValues<Orientation>())
        for (var row = 0; row < 10; row++)
        for (var col = 0; col < 10; col++)
        {
            var cells = prototype.GetOccupiedCells(row, col, orientation);
            if (!IsPossibleEnemyPlacement(definition, cells, observation, blockingSunkCells)) continue;
            var evidenceCovered = cells.Count(evidence.Contains);
            if (targetMode && evidence.Count > 0 && evidenceCovered == 0) continue;

            var placementWeight = prior * multiplicity * (1 + evidenceCovered * 8.5);
            if (definition.Abilities.Contains("diagonal_shape")) placementWeight *= 0.7;
            foreach (var (cellRow, cellCol) in cells)
            {
                var observed = observation.Cells[cellRow, cellCol];
                if (observed.IsShipSunk || observed.IsDestroyed || observed.IsCaptured ||
                    excluded.Contains((cellRow, cellCol))) continue;
                heat[cellRow, cellCol] += placementWeight;
            }
        }
    }

    private static bool IsPossibleEnemyPlacement(
        ShipDefinition definition,
        List<(int row, int col)> cells,
        BotObservation observation,
        HashSet<(int row, int col)> sunkCells)
    {
        if (cells.Any(cell => cell.row is < 0 or >= 10 || cell.col is < 0 or >= 10)) return false;
        if (definition.Range is not (RangeClass.Far or RangeClass.Tetra) &&
            cells.Any(cell => cell.row >= 8)) return false;
        if (definition.Range is RangeClass.Far or RangeClass.Tetra &&
            cells.Any(cell => cell.row >= 8) && cells.Any(cell => cell.row < 8)) return false;

        var containsKnownWater = cells.Any(cell =>
            observation.Cells[cell.row, cell.col].IsKnownWater);
        if (containsKnownWater &&
            !observation.MatchesAutoDodgePlacement(definition, cells)) return false;

        foreach (var (row, col) in cells)
        {
            var observed = observation.Cells[row, col];
            if (observed.IsBurning ||
                observed.IsShipSunk || observed.IsCaptured) return false;
            // Initial fleets obey Space=1. Keep a sunk hull and its revealed halo out of
            // living-ship hypotheses; hidden-movement evidence is deliberately not hardened.
            if (sunkCells.Any(sunk =>
                    Math.Abs(sunk.row - row) <= 1 && Math.Abs(sunk.col - col) <= 1) &&
                !sunkCells.Contains((row, col)))
                return false;
        }
        return true;
    }

    private static ShipDefinition FindDefinitionByVisibleName(string visibleName, Faction faction)
    {
        return ShipCatalog.AllShips
            .Where(definition => definition.Factions.Contains(faction))
            .OrderByDescending(definition => definition.Name.Length)
            .FirstOrDefault(definition =>
                visibleName.Equals(definition.Name, StringComparison.Ordinal) ||
                visibleName.Equals(definition.NameRu, StringComparison.Ordinal) ||
                visibleName.StartsWith(definition.Name + " ", StringComparison.Ordinal) ||
                visibleName.StartsWith((definition.NameRu ?? definition.Name) + " ", StringComparison.Ordinal));
    }

    private static double DefinitionPrior(ShipDefinition definition)
    {
        if (definition.IsFree) return 4.5;
        // Legal but costly replacements remain possible, with a smaller prior than defaults.
        return Math.Max(0.35, 2.4 - definition.Cost / 18.0);
    }

    // ── Shared random helpers ───────────────────────────────────────

    private static int WeightedChoice(IReadOnlyList<double> weights)
    {
        var total = weights.Sum(weight => Math.Max(0, weight));
        if (total <= 0) return Random.Shared.Next(weights.Count);
        var roll = Random.Shared.NextDouble() * total;
        for (var index = 0; index < weights.Count; index++)
        {
            roll -= Math.Max(0, weights[index]);
            if (roll <= 0) return index;
        }
        return weights.Count - 1;
    }

    private static int WeightedTopChoice(IReadOnlyList<double> scores)
    {
        if (scores.Count == 1) return 0;
        var min = scores.Min();
        return WeightedChoice(scores.Select(score => Math.Max(0.2, score - min + 1)).ToList());
    }
}
