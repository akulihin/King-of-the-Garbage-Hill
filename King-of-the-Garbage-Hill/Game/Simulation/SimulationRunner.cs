using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.Simulation;

/// <summary>
/// Headless bot-simulation harness (dotnet run -- --sim …). Creates all-bot games via
/// BotGameFactory (the *stb path), lets the CheckIfReady timer drive them, captures
/// exceptions via Global.SimErrorSink, detects frozen games, and writes a JSON report.
/// Exit codes: 0 = clean, 1 = errors and/or stuck games, 2 = harness-level failure.
/// </summary>
public class SimulationRunner : IServiceSingleton
{
    private const ulong BotAccountIdCeiling = 1_000_000; // matches UserAccounts.GetAccount bot check (<= 1000000)
    private const int StuckGameVisitsWithoutProgress = 5; // loop reached this bot game repeatedly, but its round stayed fixed
    private const int GlobalStallSeconds = 60;           // NO game progresses (hung round-calc pins the timer thread)

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    private readonly BotGameFactory _botGameFactory;
    private readonly CharactersPull _charactersPull;
    private readonly Global _global;
    private readonly UserAccounts _accounts;
    private readonly CheckIfReady _checkIfReady;
    private Random _random = new();

    public SimulationRunner(Global global, BotGameFactory botGameFactory, CharactersPull charactersPull,
        UserAccounts accounts, CheckIfReady checkIfReady)
    {
        _global = global;
        _botGameFactory = botGameFactory;
        _charactersPull = charactersPull;
        _accounts = accounts;
        _checkIfReady = checkIfReady;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<int> RunAsync(string[] args)
    {
        try
        {
            return await RunInternalAsync(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SIM] FATAL: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 2;
        }
    }

    private async Task<int> RunInternalAsync(string[] args)
    {
        // ── Options ──────────────────────────────────────────────────
        var games = GetIntArg(args, "--games", 100);
        var coverage = GetIntArg(args, "--coverage", 0);
        var timeoutMin = GetIntArg(args, "--timeout-min", 10);
        var charactersArg = GetStringArg(args, "--characters");
        var aiDifficulty = GetIntArg(args, "--ai-difficulty", 3);
        var aiProbe = GetIntArg(args, "--ai-probe", -1);                 // -1 = no probe (whole field on --ai-difficulty)
        var aiProbeChar = GetStringArg(args, "--ai-probe-char");         // probe by character name (else slot 0)
        var seed = GetIntArg(args, "--seed", int.MinValue);              // omitted = unseeded (crypto RNG)
        var seeded = seed != int.MinValue;
        var abChar = GetStringArg(args, "--ab-char");                    // in-process paired A/B on this character
        var abTest = GetIntArg(args, "--ab-test", 3);                    // probe level for the test arm
        var abControl = GetIntArg(args, "--ab-control", 1);              // probe level for the control arm
        if (abChar != null && !seeded) { seed = 1; seeded = true; }      // A/B is meaningless unpaired — default seed 1
        var reportPath = GetStringArg(args, "--report")
                         ?? Path.Combine("DataBase", "Simulations", $"sim-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        if (games < 0 || coverage < 0 || timeoutMin <= 0)
        {
            Console.WriteLine("[SIM] Invalid arguments: --games/--coverage must be >= 0, --timeout-min > 0.");
            return 2;
        }

        if (aiDifficulty is < 0 or > 3)
        {
            Console.WriteLine("[SIM] Invalid arguments: --ai-difficulty must be 0, 1, 2 or 3.");
            return 2;
        }

        if (aiProbe is < -1 or > 3)
        {
            Console.WriteLine("[SIM] Invalid arguments: --ai-probe must be 0, 1, 2 or 3 (or omitted).");
            return 2;
        }

        if (abChar != null && (abTest is < 0 or > 3 || abControl is < 0 or > 3))
        {
            Console.WriteLine("[SIM] Invalid arguments: --ab-test / --ab-control must be 0, 1, 2 or 3.");
            return 2;
        }

        // --seed: deterministic sequential A/B. Seed the line-up planner now so the matchup
        // plan is identical across runs; the per-game SecureRandom reseed happens at creation.
        if (seeded) _random = new Random(seed);

        // Natural coverage excludes team-only characters, but an explicit matchup may force them
        // through the same admin/test path used by StartGameLogic.
        var matchupPool = _charactersPull.GetRollableCharacters();
        var coveragePool = matchupPool.Where(x => !x.TeamModeOnly).ToList();

        List<string> matchup = null;
        if (charactersArg != null)
        {
            if (coverage > 0)
            {
                Console.WriteLine("[SIM] --characters (matchup mode) cannot be combined with --coverage.");
                return 2;
            }

            matchup = charactersArg.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var problem = ValidateMatchup(matchup, matchupPool);
            if (problem != null)
            {
                Console.WriteLine($"[SIM] Invalid --characters: {problem}");
                Console.WriteLine($"[SIM] Valid names: {string.Join(", ", matchupPool.Select(x => x.Name))}");
                return 2;
            }
        }

        // ── Line-up plan ─────────────────────────────────────────────
        var lineupPlan = new List<List<string>>(); // null entry = natural bot roll (smoke)
        if (matchup != null)
        {
            for (var i = 0; i < Math.Max(games, 1); i++) lineupPlan.Add(matchup);
        }
        else
        {
            for (var pass = 0; pass < coverage; pass++)
                lineupPlan.AddRange(BuildCoveragePass(coveragePool));
            for (var i = 0; i < games; i++)
                lineupPlan.Add(null);
        }

        if (lineupPlan.Count == 0)
        {
            Console.WriteLine("[SIM] Nothing to run (0 games).");
            return 2;
        }

        var mode = matchup != null ? "matchup"
            : coverage > 0 && games > 0 ? "smoke+coverage"
            : coverage > 0 ? "coverage"
            : "smoke";

        var probeEcho = aiProbe >= 0 ? $"; ai-probe: {aiProbe}{(aiProbeChar != null ? $" ({aiProbeChar})" : " (slot 0)")}" : "";
        var seedEcho = seeded ? $"; seed: {seed} (deterministic sequential)" : "";
        Console.WriteLine($"[SIM] Mode: {mode}; games: {lineupPlan.Count}; report: {reportPath}; ai-difficulty: {aiDifficulty}{probeEcho}{seedEcho}");

        // ── Fresh bots (comparable runs + recovery after killed runs) ─
        var botAccounts = 0;
        foreach (var account in _accounts.GetAllAccount().Where(a => a.DiscordId <= BotAccountIdCeiling))
        {
            account.IsPlaying = false;
            account.TierPity.Clear();
            account.MatchHistory.Clear();
            account.CharacterPlayedLastTime = null;
            botAccounts++;
        }

        Console.WriteLine($"[SIM] Reset {botAccounts} bot accounts.");

        // ── Hooks (before any game can finish) ───────────────────────
        var records = new ConcurrentDictionary<ulong, SimGameRecordDto>();
        var errors = new ConcurrentBag<SimErrorDto>();
        var lineups = new ConcurrentDictionary<ulong, List<string>>();

        _global.OnGameFinished = game =>
        {
            records[game.GameId] = BuildRecord(game);
            return Task.CompletedTask;
        };
        _global.SimErrorSink = (gameId, round, exception) => errors.Add(new SimErrorDto
        {
            GameId = gameId,
            Round = round,
            Lineup = lineups.TryGetValue(gameId, out var lu) ? lu : null,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
        });

        // ── Create games ─────────────────────────────────────────────
        var startedAt = DateTime.UtcNow;
        var myGameIds = new List<ulong>();
        string gameVersion = null;
        var stuckGames = new List<SimStuckDto>();

        if (abChar != null)
            return await RunAbModeAsync(lineupPlan, seed, aiDifficulty, abChar, abTest, abControl,
                errors, lineups, reportPath);

        if (seeded)
        {
            // Deterministic run: disable the background timer and drive each game to completion on
            // THIS thread via TickAsync, reseeding SecureRandom per game. Single-threaded → the RNG
            // stream is reproducible, so a fixed --seed replays the batch bit-for-bit and the
            // L1-probe vs L3-probe runs differ ONLY by the probe player's decisions (common random
            // numbers). A running timer thread would race the seeded loop on the (non-thread-safe)
            // seeded RNG and destroy determinism. The unseeded bulk path (below) is untouched.
            _checkIfReady.SetTimerEnabled(false);
            try
            {
                for (var gi = 0; gi < lineupPlan.Count; gi++)
                {
                    SecureRandom.SetSeed(seed + gi);
                    var game = await _botGameFactory.CreateBotGameAsync(creatorId: 0, mode: "Bot",
                        forcedCharacters: lineupPlan[gi], aiDifficulty: aiDifficulty, aiProbe: aiProbe, aiProbeChar: aiProbeChar);
                    myGameIds.Add(game.GameId);
                    lineups[game.GameId] = game.PlayersList.Select(x => x.GameCharacter.Name).ToList();
                    gameVersion ??= game.GameVersion;

                    // Pump this one game to completion (≈11 round passes). The guard caps a
                    // pathological non-advancing game so a bug can't hang the whole batch.
                    var guard = 0;
                    while (!records.ContainsKey(game.GameId)
                           && _global.GamesList.Any(g => g.GameId == game.GameId)
                           && guard++ < 5000)
                        await _checkIfReady.TickAsync();

                    if (!records.ContainsKey(game.GameId))
                    {
                        var g = _global.GamesList.Find(x => x.GameId == game.GameId);
                        stuckGames.Add(new SimStuckDto
                        {
                            GameId = game.GameId,
                            Round = g?.RoundNo ?? -1,
                            Lineup = lineups.TryGetValue(game.GameId, out var lu) ? lu : null,
                            SecondsStalled = 0,
                        });
                        if (g != null)
                            lock (_global.GamesList)
                            {
                                _global.GamesList.Remove(g);
                            }
                    }
                }
            }
            finally
            {
                SecureRandom.ClearSeed();
                _checkIfReady.SetTimerEnabled(true);
            }

            Console.WriteLine($"[SIM] Ran {myGameIds.Count} seeded games sequentially in {(DateTime.UtcNow - startedAt).TotalSeconds:0.#}s.");
        }
        else
        {
            foreach (var lineup in lineupPlan)
            {
                var game = await _botGameFactory.CreateBotGameAsync(creatorId: 0, mode: "Bot",
                    forcedCharacters: lineup, aiDifficulty: aiDifficulty, aiProbe: aiProbe, aiProbeChar: aiProbeChar);
                myGameIds.Add(game.GameId);
                lineups[game.GameId] = game.PlayersList.Select(x => x.GameCharacter.Name).ToList();
                gameVersion ??= game.GameVersion;
            }

            Console.WriteLine($"[SIM] Created {myGameIds.Count} games in {(DateTime.UtcNow - startedAt).TotalSeconds:0.#}s.");
        }

        // ── Wait loop with watchdog (bulk concurrent mode; seeded already waited per-game) ──
        var handled = new HashSet<ulong>();
        var lastProgress = new Dictionary<ulong, (int Round, DateTime At, long LoopVisits)>();
        var lastAnyProgressUtc = DateTime.UtcNow;
        var waitStartedUtc = DateTime.UtcNow;
        var recordedLastLoop = 0;
        var loops = 0;

        while (!seeded)
        {
            await Task.Delay(1000);
            loops++;

            var pending = myGameIds.Where(id => !records.ContainsKey(id) && !handled.Contains(id)).ToList();
            if (pending.Count == 0) break;

            if (records.Count > recordedLastLoop)
            {
                recordedLastLoop = records.Count;
                lastAnyProgressUtc = DateTime.UtcNow;
            }

            foreach (var id in pending)
            {
                var game = _global.GamesList.Find(g => g.GameId == id);
                if (game == null)
                {
                    // The record is added strictly BEFORE the game leaves GamesList
                    // (HandleLastRound: OnGameFinished → Remove), so if the game vanished
                    // between our records-read and this list-read, the record exists now.
                    if (records.ContainsKey(id)) continue;

                    // Genuinely removed without a record → OnGameFinished failed
                    errors.Add(new SimErrorDto
                    {
                        GameId = id,
                        Round = -1,
                        Lineup = lineups.TryGetValue(id, out var lu) ? lu : null,
                        Message = "Game finished without a record (OnGameFinished failed?)",
                    });
                    handled.Add(id);
                    continue;
                }

                var loopVisits = System.Threading.Interlocked.Read(ref game.ReadinessLoopVisits);
                if (!lastProgress.TryGetValue(id, out var prev) || prev.Round != game.RoundNo)
                {
                    lastProgress[id] = (game.RoundNo, DateTime.UtcNow, loopVisits);
                    lastAnyProgressUtc = DateTime.UtcNow;
                    continue;
                }

                var visitsWithoutProgress = loopVisits - prev.LoopVisits;
                if (visitsWithoutProgress >= StuckGameVisitsWithoutProgress)
                {
                    var stalled = (DateTime.UtcNow - prev.At).TotalSeconds;
                    stuckGames.Add(new SimStuckDto
                    {
                        GameId = id,
                        Round = game.RoundNo,
                        Lineup = lineups.TryGetValue(id, out var lu2) ? lu2 : null,
                        SecondsStalled = Math.Round(stalled),
                        LoopVisitsWithoutProgress = visitsWithoutProgress,
                    });
                    lock (_global.GamesList)
                    {
                        _global.GamesList.Remove(game);
                    }
                    handled.Add(id);
                    Console.WriteLine($"[SIM] STUCK: game #{id} stayed at round {game.RoundNo} for " +
                                      $"{visitsWithoutProgress} readiness-loop visits " +
                                      $"({string.Join(", ", lineups.GetValueOrDefault(id) ?? new List<string>())})");
                }
            }

            var globallyStalled = (DateTime.UtcNow - lastAnyProgressUtc).TotalSeconds > GlobalStallSeconds;
            var timedOut = (DateTime.UtcNow - waitStartedUtc).TotalMinutes > timeoutMin;
            if (globallyStalled || timedOut)
            {
                var reason = timedOut ? $"--timeout-min {timeoutMin} exceeded" : "no game progressed for 60s";
                Console.WriteLine($"[SIM] ABORT: {reason}; dumping remaining games as stuck.");
                foreach (var id in myGameIds.Where(id => !records.ContainsKey(id) && !handled.Contains(id)))
                {
                    var game = _global.GamesList.Find(g => g.GameId == id);
                    stuckGames.Add(new SimStuckDto
                    {
                        GameId = id,
                        Round = game?.RoundNo ?? -1,
                        Lineup = lineups.TryGetValue(id, out var lu3) ? lu3 : null,
                        SecondsStalled = Math.Round((DateTime.UtcNow - lastAnyProgressUtc).TotalSeconds),
                    });
                    if (game != null)
                        lock (_global.GamesList)
                        {
                            _global.GamesList.Remove(game);
                        }
                    handled.Add(id);
                }

                break;
            }

            if (loops % 5 == 0)
                Console.WriteLine($"[SIM] {records.Count}/{myGameIds.Count} finished, {pending.Count} running, " +
                                  $"{errors.Count} errors, {(DateTime.UtcNow - waitStartedUtc).TotalSeconds:0}s elapsed");
        }

        // ── Report ───────────────────────────────────────────────────
        _global.OnGameFinished = null;
        _global.SimErrorSink = null;

        var report = new SimReportDto
        {
            Mode = mode,
            GameVersion = gameVersion,
            StartedAtUtc = startedAt,
            DurationSeconds = Math.Round((DateTime.UtcNow - startedAt).TotalSeconds, 1),
            Options = new SimOptionsDto
                { Games = games, Coverage = coverage, Characters = matchup, TimeoutMin = timeoutMin,
                  AiDifficulty = aiDifficulty, AiProbe = aiProbe, AiProbeChar = aiProbeChar },
            GamesRequested = myGameIds.Count,
            GamesFinished = records.Count,
            GamesStuck = stuckGames.Count,
            Errors = errors.OrderBy(x => x.GameId).ToList(),
            StuckGames = stuckGames,
            Games = records.Values.OrderBy(x => x.GameId).ToList(),
            Characters = BuildCharacterRows(records.Values),
        };
        report.ExitCode = report.Errors.Count > 0 || report.GamesStuck > 0 ? 1 : 0;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions));

        PrintSummary(report, reportPath);
        return report.ExitCode;
    }

    // In-process paired A/B: runs the seeded line-up plan twice in ONE process — control arm
    // (abChar at controlLevel) then test arm (abChar at testLevel), with the SAME per-game seeds —
    // so both arms share the process's string/reference hash seed and are genuinely paired
    // game-by-game (the only difference is abChar's difficulty). This removes the cross-process
    // hash-ordering noise a two-invocation A/B suffers on hash-order-sensitive characters. Games
    // are driven single-threaded via TickAsync (timer off) so the seeded RNG stream is deterministic.
    private async Task<int> RunAbModeAsync(
        List<List<string>> lineupPlan, int seed, int fieldDifficulty, string abChar, int testLevel, int controlLevel,
        ConcurrentBag<SimErrorDto> errors, ConcurrentDictionary<ulong, List<string>> lineups, string reportPath)
    {
        var startedAt = DateTime.UtcNow;
        Console.WriteLine($"[SIM][AB] {abChar}: L{testLevel} (test) vs L{controlLevel} (control); field L{fieldDifficulty}; " +
                          $"{lineupPlan.Count} line-ups; seed {seed}; same-process paired.");

        _global.SimErrorSink = (gameId, round, ex) => errors.Add(new SimErrorDto
        {
            GameId = gameId, Round = round,
            Lineup = lineups.TryGetValue(gameId, out var lu) ? lu : null,
            Message = ex.Message, StackTrace = ex.StackTrace,
        });

        async Task<List<SimGameRecordDto>> RunArm(int probeLevel)
        {
            // Start each arm from the same bot-account state so the two arms are paired: arm 1 must
            // not leave dirtied accounts (TierPity/MatchHistory/CharacterPlayedLastTime) for arm 2.
            foreach (var account in _accounts.GetAllAccount().Where(a => a.DiscordId <= BotAccountIdCeiling))
            {
                account.IsPlaying = false;
                account.TierPity.Clear();
                account.MatchHistory.Clear();
                account.CharacterPlayedLastTime = null;
            }

            var arm = new List<SimGameRecordDto>();
            _global.OnGameFinished = g => { arm.Add(BuildRecord(g)); return Task.CompletedTask; };
            _checkIfReady.SetTimerEnabled(false);
            try
            {
                for (var gi = 0; gi < lineupPlan.Count; gi++)
                {
                    SecureRandom.SetSeed(seed + gi);
                    var game = await _botGameFactory.CreateBotGameAsync(creatorId: 0, mode: "Bot",
                        forcedCharacters: lineupPlan[gi], aiDifficulty: fieldDifficulty,
                        aiProbe: probeLevel, aiProbeChar: abChar);
                    lineups[game.GameId] = game.PlayersList.Select(x => x.GameCharacter.Name).ToList();

                    var startCount = arm.Count;
                    var guard = 0;
                    while (arm.Count == startCount
                           && _global.GamesList.Any(x => x.GameId == game.GameId)
                           && guard++ < 5000)
                        await _checkIfReady.TickAsync();

                    lock (_global.GamesList)
                    {
                        _global.GamesList.RemoveAll(x => x.GameId == game.GameId);
                    }
                }
            }
            finally
            {
                SecureRandom.ClearSeed();
                _checkIfReady.SetTimerEnabled(true);
            }

            return arm;
        }

        var control = await RunArm(controlLevel);
        var test = await RunArm(testLevel);
        _global.OnGameFinished = null;
        _global.SimErrorSink = null;

        // Pair by index — control[i] and test[i] share lineupPlan[i] and seed+i.
        var placeDiffs = new List<double>();
        var scoreDiffs = new List<double>();
        int winT = 0, winC = 0, paired = 0;
        double placeT = 0, placeC = 0, scoreT = 0, scoreC = 0;
        var n = Math.Min(control.Count, test.Count);
        for (var i = 0; i < n; i++)
        {
            var pc = control[i].Players.Find(p => p.Character == abChar);
            var pt = test[i].Players.Find(p => p.Character == abChar);
            if (pc == null || pt == null) continue;
            if (!control[i].Players.Select(p => p.Character).OrderBy(x => x)
                    .SequenceEqual(test[i].Players.Select(p => p.Character).OrderBy(x => x)))
                continue;

            paired++;
            winC += pc.Place == 1 ? 1 : 0;
            winT += pt.Place == 1 ? 1 : 0;
            placeC += pc.Place; placeT += pt.Place;
            scoreC += (double)pc.Score; scoreT += (double)pt.Score;
            placeDiffs.Add(pc.Place - pt.Place);            // >0 => test placed higher (better)
            scoreDiffs.Add((double)(pt.Score - pc.Score));  // >0 => test scored more
        }

        double Mean(List<double> xs) => xs.Count == 0 ? 0 : xs.Sum() / xs.Count;
        double Se(List<double> xs)
        {
            if (xs.Count < 2) return 0;
            var m = Mean(xs);
            return Math.Sqrt(xs.Sum(x => (x - m) * (x - m)) / (xs.Count - 1) / xs.Count);
        }

        if (paired == 0)
        {
            Console.WriteLine($"[SIM][AB] {abChar}: no paired games — is the character in the line-ups?");
            return 2;
        }

        var placeD = Mean(placeDiffs); var placeSe = Se(placeDiffs);
        var scoreD = Mean(scoreDiffs); var scoreSe = Se(scoreDiffs);
        var verdict = placeD - 1.96 * placeSe > 0 ? $"L{testLevel} STRONGER than L{controlLevel} (place CI excludes 0)"
            : placeD + 1.96 * placeSe < 0 ? $"L{testLevel} WEAKER than L{controlLevel} (regression — place CI below 0)"
            : "inconclusive (place CI spans 0 — raise --coverage)";
        var identical = control.Count == test.Count && placeDiffs.All(d => d == 0) && scoreDiffs.All(d => d == 0);

        Console.WriteLine($"[SIM][AB] paired games: {paired}  (ran {control.Count}+{test.Count} in {(DateTime.UtcNow - startedAt).TotalSeconds:0.#}s)");
        Console.WriteLine($"[SIM][AB]   win%:  test {100.0 * winT / paired:0.0}  control {100.0 * winC / paired:0.0}  Δ {100.0 * (winT - winC) / paired:+0.0;-0.0}pp");
        Console.WriteLine($"[SIM][AB]   place: test {placeT / paired:0.00}  control {placeC / paired:0.00}  Δ {placeD:+0.000;-0.000} (95% CI ±{1.96 * placeSe:0.000})  [>0 = test better]");
        Console.WriteLine($"[SIM][AB]   score: test {scoreT / paired:0.00}  control {scoreC / paired:0.00}  Δ {scoreD:+0.00;-0.00} (95% CI ±{1.96 * scoreSe:0.00})");
        if (testLevel == controlLevel)
            Console.WriteLine($"[SIM][AB]   self-test (equal levels): arms {(identical ? "IDENTICAL ✓ (deterministic pairing)" : "DIFFER — residual nondeterminism")}.");
        Console.WriteLine($"[SIM][AB]   Verdict: {verdict}");

        var abReport = new
        {
            Char = abChar, TestLevel = testLevel, ControlLevel = controlLevel, FieldDifficulty = fieldDifficulty,
            Seed = seed, PairedGames = paired,
            WinPercentTest = 100.0 * winT / paired, WinPercentControl = 100.0 * winC / paired,
            PlaceDelta = placeD, PlaceCi95 = 1.96 * placeSe,
            ScoreDelta = scoreD, ScoreCi95 = 1.96 * scoreSe,
            Verdict = verdict, ArmsIdentical = identical,
            ControlGames = control, TestGames = test,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(abReport, JsonOptions));
        Console.WriteLine($"[SIM][AB] Report: {reportPath}");

        return errors.Count > 0 ? 1 : 0;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static int GetIntArg(string[] args, string flag, int defaultValue)
    {
        var i = Array.IndexOf(args, flag);
        if (i < 0 || i + 1 >= args.Length) return defaultValue;
        return int.TryParse(args[i + 1], out var value) ? value : defaultValue;
    }

    private static string GetStringArg(string[] args, string flag)
    {
        var i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string ValidateMatchup(List<string> names, List<CharacterClass> pool)
    {
        if (names.Count != 6) return $"exactly 6 names required, got {names.Count}";
        if (names.Distinct().Count() != 6) return "duplicate names";
        foreach (var name in names.Where(name => pool.All(x => x.Name != name)))
            return $"unknown or team-only character: {name}";
        if (names.Contains("LeCrisp") && names.Contains("Толя"))
            return "LeCrisp and Толя cannot be in the same game";
        if (names.Count(n => pool.First(x => x.Name == n).Tier == 4) > 1
            && !Cthulhu.AdeptNames.All(names.Contains))
            return "at most one Tier-4 character per game";
        return null;
    }

    /// <summary>
    /// One coverage pass: every pool character exactly once, chunked into games of 6
    /// (LeCrisp/Толя apart, ≤1 Tier-4 per game), last chunk padded with random fillers.
    /// </summary>
    private List<List<string>> BuildCoveragePass(List<CharacterClass> pool)
    {
        var remaining = pool.OrderBy(_ => _random.Next()).ToList();
        var result = new List<List<string>>();

        while (remaining.Count > 0)
        {
            var lineup = new List<CharacterClass>();

            for (var i = 0; i < remaining.Count && lineup.Count < 6;)
            {
                if (Fits(lineup, remaining[i]))
                {
                    lineup.Add(remaining[i]);
                    remaining.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }

            if (lineup.Count < 6)
            {
                var fillers = pool
                    .Where(c => lineup.All(x => x.Name != c.Name) && Fits(lineup, c))
                    .OrderBy(_ => _random.Next())
                    .Take(6 - lineup.Count)
                    .ToList();
                if (lineup.Count + fillers.Count < 6)
                    throw new InvalidOperationException("Coverage pass: cannot fill a valid 6-character line-up.");
                lineup.AddRange(fillers);
            }

            result.Add(lineup.Select(x => x.Name).ToList());
        }

        return result;
    }

    private static bool Fits(List<CharacterClass> lineup, CharacterClass candidate)
    {
        if (lineup.Any(x => x.Name == candidate.Name)) return false;
        if (candidate.Name == "LeCrisp" && lineup.Any(x => x.Name == "Толя")) return false;
        if (candidate.Name == "Толя" && lineup.Any(x => x.Name == "LeCrisp")) return false;
        if (candidate.Tier == 4 && lineup.Any(x => x.Tier == 4)) return false;
        return true;
    }

    private static SimGameRecordDto BuildRecord(GameClass game)
    {
        return new SimGameRecordDto
        {
            GameId = game.GameId,
            Rounds = game.RoundNo,
            Players = game.PlayersList
                .OrderBy(x => x.Status.GetPlaceAtLeaderBoard())
                .Select(x => new SimGamePlayerDto
                {
                    Character = x.GameCharacter.Name,
                    BotName = x.DiscordUsername,
                    AiPlaystyle = x.AiPlaystyle,
                    Score = x.Status.GetScore(),
                    Place = x.Status.GetPlaceAtLeaderBoard(),
                    IsDead = x.Passives.IsDead,
                    IsWinner = game.WinnerPlayerIds.Contains(x.GetPlayerId()),
                    IsStructuralClone = Naruto.IsDispersedClone(x),
                })
                .ToList(),
        };
    }

    private static List<SimCharacterRowDto> BuildCharacterRows(IEnumerable<SimGameRecordDto> records)
    {
        var rows = new Dictionary<string, SimCharacterRowDto>();
        var scoreSums = new Dictionary<string, decimal>();
        var placeSums = new Dictionary<string, int>();

        foreach (var player in records.SelectMany(record => record.Players)
                     .Where(player => !player.IsStructuralClone))
        {
            if (!rows.TryGetValue(player.Character, out var row))
            {
                row = new SimCharacterRowDto { Name = player.Character };
                rows[player.Character] = row;
                scoreSums[player.Character] = 0;
                placeSums[player.Character] = 0;
            }

            row.Games++;
            if (player.IsWinner) row.Wins++;
            scoreSums[player.Character] += player.Score;
            placeSums[player.Character] += player.Place;
            switch (player.Place)
            {
                case 1: row.Top1++; break;
                case 2: row.Top2++; break;
                case 3: row.Top3++; break;
                case 4: row.Top4++; break;
                case 5: row.Top5++; break;
                case 6: row.Top6++; break;
            }
        }

        foreach (var row in rows.Values)
        {
            row.WinRate = Math.Round(100.0 * row.Wins / row.Games, 1);
            row.AvgScore = Math.Round((double)scoreSums[row.Name] / row.Games, 1);
            row.AvgPlace = Math.Round((double)placeSums[row.Name] / row.Games, 2);
        }

        return rows.Values.OrderByDescending(x => x.WinRate).ToList();
    }

    private static void PrintSummary(SimReportDto report, string reportPath)
    {
        Console.WriteLine("[SIM] ──────────────────────────────────────────");
        Console.WriteLine($"[SIM] {report.Mode}: {report.GamesFinished}/{report.GamesRequested} finished, " +
                          $"{report.GamesStuck} stuck, {report.Errors.Count} errors, {report.DurationSeconds}s");

        foreach (var error in report.Errors.Take(5))
            Console.WriteLine($"[SIM] ERROR game #{error.GameId} r{error.Round}: {error.Message}");
        if (report.Errors.Count > 5)
            Console.WriteLine($"[SIM] … and {report.Errors.Count - 5} more errors (see report)");

        foreach (var stuck in report.StuckGames.Take(5))
            Console.WriteLine($"[SIM] STUCK game #{stuck.GameId} r{stuck.Round}: " +
                              $"{string.Join(", ", stuck.Lineup ?? new List<string>())}");

        Console.WriteLine("[SIM] Top winrates: " + string.Join(", ",
            report.Characters.Take(5).Select(x => $"{x.Name} {x.WinRate:0.#}% ({x.Wins}/{x.Games})")));
        Console.WriteLine($"[SIM] Report: {reportPath}");
        Console.WriteLine($"[SIM] Exit code: {report.ExitCode}");
    }
}
