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
using King_of_the_Garbage_Hill.Game.MemoryStorage;
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
    private const int StuckGameSeconds = 30;             // one game without round progress
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
    private readonly Random _random = new();

    public SimulationRunner(Global global, BotGameFactory botGameFactory, CharactersPull charactersPull,
        UserAccounts accounts)
    {
        _global = global;
        _botGameFactory = botGameFactory;
        _charactersPull = charactersPull;
        _accounts = accounts;
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
        var reportPath = GetStringArg(args, "--report")
                         ?? Path.Combine("DataBase", "Simulations", $"sim-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        if (games < 0 || coverage < 0 || timeoutMin <= 0)
        {
            Console.WriteLine("[SIM] Invalid arguments: --games/--coverage must be >= 0, --timeout-min > 0.");
            return 2;
        }

        // Pool used for coverage generation AND matchup validation
        var pool = _charactersPull.GetRollableCharacters().Where(x => !x.TeamModeOnly).ToList();

        List<string> matchup = null;
        if (charactersArg != null)
        {
            if (coverage > 0)
            {
                Console.WriteLine("[SIM] --characters (matchup mode) cannot be combined with --coverage.");
                return 2;
            }

            matchup = charactersArg.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var problem = ValidateMatchup(matchup, pool);
            if (problem != null)
            {
                Console.WriteLine($"[SIM] Invalid --characters: {problem}");
                Console.WriteLine($"[SIM] Valid names: {string.Join(", ", pool.Select(x => x.Name))}");
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
                lineupPlan.AddRange(BuildCoveragePass(pool));
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

        Console.WriteLine($"[SIM] Mode: {mode}; games: {lineupPlan.Count}; report: {reportPath}");

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

        // ── Create games (they start running as created) ─────────────
        var startedAt = DateTime.UtcNow;
        var myGameIds = new List<ulong>();
        string gameVersion = null;

        foreach (var lineup in lineupPlan)
        {
            var game = await _botGameFactory.CreateBotGameAsync(creatorId: 0, mode: "Bot",
                forcedCharacters: lineup);
            myGameIds.Add(game.GameId);
            lineups[game.GameId] = game.PlayersList.Select(x => x.GameCharacter.Name).ToList();
            gameVersion ??= game.GameVersion;
        }

        Console.WriteLine($"[SIM] Created {myGameIds.Count} games in {(DateTime.UtcNow - startedAt).TotalSeconds:0.#}s.");

        // ── Wait loop with watchdog ──────────────────────────────────
        var stuckGames = new List<SimStuckDto>();
        var handled = new HashSet<ulong>();
        var lastProgress = new Dictionary<ulong, (int Round, DateTime At)>();
        var lastAnyProgressUtc = DateTime.UtcNow;
        var waitStartedUtc = DateTime.UtcNow;
        var recordedLastLoop = 0;
        var loops = 0;

        while (true)
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

                if (!lastProgress.TryGetValue(id, out var prev) || prev.Round != game.RoundNo)
                {
                    lastProgress[id] = (game.RoundNo, DateTime.UtcNow);
                    lastAnyProgressUtc = DateTime.UtcNow;
                    continue;
                }

                var stalled = (DateTime.UtcNow - prev.At).TotalSeconds;
                if (stalled > StuckGameSeconds)
                {
                    stuckGames.Add(new SimStuckDto
                    {
                        GameId = id,
                        Round = game.RoundNo,
                        Lineup = lineups.TryGetValue(id, out var lu2) ? lu2 : null,
                        SecondsStalled = Math.Round(stalled),
                    });
                    _global.GamesList.Remove(game);
                    handled.Add(id);
                    Console.WriteLine($"[SIM] STUCK: game #{id} frozen at round {game.RoundNo} " +
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
                    if (game != null) _global.GamesList.Remove(game);
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
                { Games = games, Coverage = coverage, Characters = matchup, TimeoutMin = timeoutMin },
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
        if (names.Count(n => pool.First(x => x.Name == n).Tier == 4) > 1)
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
                    Score = x.Status.GetScore(),
                    Place = x.Status.GetPlaceAtLeaderBoard(),
                    IsDead = x.Passives.IsDead,
                })
                .ToList(),
        };
    }

    private static List<SimCharacterRowDto> BuildCharacterRows(IEnumerable<SimGameRecordDto> records)
    {
        var rows = new Dictionary<string, SimCharacterRowDto>();
        var scoreSums = new Dictionary<string, decimal>();
        var placeSums = new Dictionary<string, int>();

        foreach (var player in records.SelectMany(record => record.Players))
        {
            if (!rows.TryGetValue(player.Character, out var row))
            {
                row = new SimCharacterRowDto { Name = player.Character };
                rows[player.Character] = row;
                scoreSums[player.Character] = 0;
                placeSums[player.Character] = 0;
            }

            row.Games++;
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
            row.WinRate = Math.Round(100.0 * row.Top1 / row.Games, 1);
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
            report.Characters.Take(5).Select(x => $"{x.Name} {x.WinRate:0.#}% ({x.Top1}/{x.Games})")));
        Console.WriteLine($"[SIM] Report: {reportPath}");
        Console.WriteLine($"[SIM] Exit code: {report.ExitCode}");
    }
}
