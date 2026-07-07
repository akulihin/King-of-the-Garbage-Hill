using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Simulation;

/// <summary>
/// JSON report written by SimulationRunner (headless --sim mode).
/// Default location: DataBase/Simulations/sim-&lt;timestamp&gt;.json (gitignored).
/// </summary>
public class SimReportDto
{
    /// <summary>smoke | coverage | smoke+coverage | matchup</summary>
    public string Mode { get; set; }

    public string GameVersion { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public double DurationSeconds { get; set; }
    public SimOptionsDto Options { get; set; }
    public int GamesRequested { get; set; }
    public int GamesFinished { get; set; }
    public int GamesStuck { get; set; }

    /// <summary>0 = clean; 1 = errors and/or stuck games; 2 = harness-level failure.</summary>
    public int ExitCode { get; set; }

    public List<SimErrorDto> Errors { get; set; } = new();
    public List<SimStuckDto> StuckGames { get; set; } = new();
    public List<SimCharacterRowDto> Characters { get; set; } = new();
    public List<SimGameRecordDto> Games { get; set; } = new();
}

public class SimOptionsDto
{
    public int Games { get; set; }
    public int Coverage { get; set; }
    public List<string> Characters { get; set; }
    public double TimeoutMin { get; set; }

    /// <summary>Bot AI difficulty for the run: 0 pure-random, 1 legacy, 2 smarter heuristics, 3 omniscient (default).</summary>
    public int AiDifficulty { get; set; } = 3;

    /// <summary>Measurement probe: difficulty applied to a single bot (the rest use AiDifficulty). -1 = no probe.</summary>
    public int AiProbe { get; set; } = -1;

    /// <summary>Character name the probe is applied to (null = first slot). Only meaningful when AiProbe ≥ 0.</summary>
    public string AiProbeChar { get; set; }
}

public class SimErrorDto
{
    public ulong GameId { get; set; }
    public int Round { get; set; }
    public List<string> Lineup { get; set; }
    public string Message { get; set; }
    public string StackTrace { get; set; }
}

public class SimStuckDto
{
    public ulong GameId { get; set; }
    public int Round { get; set; }
    public List<string> Lineup { get; set; }
    public double SecondsStalled { get; set; }
}

public class SimCharacterRowDto
{
    public string Name { get; set; }
    public int Games { get; set; }
    public int Top1 { get; set; }
    public int Top2 { get; set; }
    public int Top3 { get; set; }
    public int Top4 { get; set; }
    public int Top5 { get; set; }
    public int Top6 { get; set; }

    /// <summary>Top1 / Games × 100. Bot-meta statistic — needs hundreds of games for drift detection.</summary>
    public double WinRate { get; set; }

    public double AvgScore { get; set; }
    public double AvgPlace { get; set; }
}

public class SimGameRecordDto
{
    public ulong GameId { get; set; }
    public int Rounds { get; set; }
    public List<SimGamePlayerDto> Players { get; set; } = new();
}

public class SimGamePlayerDto
{
    public string Character { get; set; }
    public string BotName { get; set; }
    public decimal Score { get; set; }
    public int Place { get; set; }
    public bool IsDead { get; set; }
}
