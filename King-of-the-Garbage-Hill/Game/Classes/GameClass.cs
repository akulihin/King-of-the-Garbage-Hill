using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class GameClass
{
    public GameClass(List<GamePlayerBridgeClass> playersList, ulong gameId, ulong creatorId, int turnLengthInSecond = 300, string gameMode = "Normal")
    {
        RoundNo = 1;
        Phrases = new CharactersUniquePhrase();
        PlayersList = playersList;
        GameId = gameId;
        TurnLengthInSecond = turnLengthInSecond;
        TimePassed = new Stopwatch();
        AllGameGlobalLogs = "";
        GlobalLogs = "";
        IsCheckIfReady = true;
        SkipPlayersThisRound = 0;
        GameVersion = "Версия: 5.0.17";
        GameMode = gameMode;
        CreatorId = creatorId;
        Teams = new List<TeamPlay>();
        ExploitPlayersList = [.. PlayersList.Where(player => !UnknownBug.Is(player) && !player.Passives.IsDead)];
        RollExploit();
    }


    public int RoundNo { get; set; }
    public List<GamePlayerBridgeClass> PlayersList { get; set; }
    public ulong GameId { get; set; }
    public double TurnLengthInSecond { get; set; }
    public string GameVersion { get; set; }
    public Stopwatch TimePassed { get; set; }

    public CharactersUniquePhrase Phrases { get; set; }

    public bool IsCheckIfReady { get; set; }
    public bool IsFinished { get; set; } = false;
    public bool IsKratosEvent { get; set; } = false;
    public List<Guid> WinnerPlayerIds { get; set; } = new();

    /*
     * 1 - Turn
     * 2 - Counting
     * 3 - End
     */
    private string AllGameGlobalLogs { get; set; }

    public int SkipPlayersThisRound { get; set; }

    /// <summary>True once at least one fight is resolved this round. Reset at the start of CalculateAllFights,
    /// set when a winner is decided. Used by Toxic Mate's "Tilted" (+50 only on a zero-battle round, finding M8).</summary>
    public bool AnyFightThisRound { get; set; }

    /// <summary>Halflife 3 can suspend only this game's between-round settlement for 20 seconds.</summary>
    public bool IsRoundTransitionPaused { get; set; }
    public DateTimeOffset? TransitionDeadlineUtc { get; set; }
    public long StateRevision { get; set; }

    private string GlobalLogs { get; set; }
    public string GameMode { get; set; }
    public ulong CreatorId { get; set; }
    public List<TeamPlay> Teams { get; set; }
    public uint TestFightNumber { get; set; }

    /// <summary>Bot AI difficulty. 1 = legacy baseline, 2 = fair player-visible strategy,
    /// 3 = fair strategy with longer memory and rule-based inference. Default 3 everywhere, incl.
    /// Discord/web games; the sim harness can override per-run via --ai-difficulty.</summary>
    public int AiDifficulty { get; set; } = 3;

    public List<BotsBehavior.NanobotClass> NanobotsList { get; set; } = new();

    public bool IsAramPickPhase { get; set; }
    public bool IsDraftPickPhase { get; set; }
    public Dictionary<Guid, List<CharacterClass>> DraftOptions { get; set; } = new();

    public List<GamePlayerBridgeClass> ExploitPlayersList { get; set; }
    public int LastExploit { get; set; } = -1;
    public int TotalExploit { get; set; } = 0;
    public Guid CurrentExploitTargetPlayerId { get; set; } = Guid.Empty;
    public bool ExploitClosed { get; set; }
    public bool ExploitActive => !ExploitClosed
                                 && CurrentExploitTargetPlayerId != Guid.Empty
                                 && PlayersList.Any(player =>
                                     player.GetPlayerId() == CurrentExploitTargetPlayerId
                                     && player.Passives.IsExploitable);

    /// <summary>Structured fight log for the current round (persists until next round starts).</summary>
    public List<FightEntryDto> WebFightLog { get; set; } = new();

    /// <summary>Accumulated same-round pre-fight/result replay snapshots populated by ReplayService.</summary>
    public List<ReplayRoundDto> ReplayRounds { get; set; } = new();

    /// <summary>Text snippets in GlobalLogs that should be stripped for non-admin players (e.g. Saitama hidden fights).</summary>
    public List<string> HiddenGlobalLogSnippets { get; set; } = new();

    /// <summary>Text snippets that should be stripped from GlobalLogs for Kira's "Genius" passive (character-revealing info).</summary>
    public List<string> KiraHiddenLogSnippets { get; set; } = new();

    /// <summary>Player IDs revealed by Коммуникация or Толя (for Pink Ward animation on frontend).</summary>
    public List<Guid> PinkWardRevealedPlayerIds { get; set; } = new();




    public void AddGlobalLogs(string str, string newLine = "\n")
    {
        GlobalLogs += str + newLine;
        AllGameGlobalLogs += str + newLine;
    }

    public void AddGlobalLogsRaw(string str)
    {
       AllGameGlobalLogs += str;
    }


    public string GetGlobalLogs()
    {
        return GlobalLogs;
    }

    public string GetAllGlobalLogs()
    {
        return AllGameGlobalLogs;
    }

    public void SetGlobalLogs(string str)
    {
        GlobalLogs = str;
    }

    public class TeamPlay
    {
        public TeamPlay(int teamId)
        {
            TeamId = teamId;
            TeamPlayers = new List<Guid>();
            TeamPlayersUsernames = new List<string>();
        }

        public int TeamId { get; set; }
        public List<Guid> TeamPlayers { get; set; }
        public List<string> TeamPlayersUsernames { get; set; }
    }

    internal List<Guid> GetTeammates(GamePlayerBridgeClass player)
    {
        return Teams.Find(x => x.TeamPlayers.Contains(player.GetPlayerId()))
            ?.TeamPlayers.Where(y => y != player.GetPlayerId()).ToList() ?? new List<Guid>();
    }

    public void RollExploit()
    {
        var previousTargetId = CurrentExploitTargetPlayerId;
        if (previousTargetId == Guid.Empty)
            previousTargetId = PlayersList.FirstOrDefault(player => player.Passives.IsExploitable)
                ?.GetPlayerId() ?? Guid.Empty;

        foreach (var player in PlayersList)
            player.Passives.IsExploitable = false;

        var owner = UnknownBug.FindOwner(this);
        ExploitPlayersList = PlayersList
            .Where(player => owner == null || player.GetPlayerId() != owner.GetPlayerId())
            .ToList();

        CurrentExploitTargetPlayerId = Guid.Empty;
        LastExploit = -1;
        var availableTargets = ExploitPlayersList.Where(player => !player.Passives.IsDead).ToList();
        if (owner == null || owner.Passives.IsDead || ExploitClosed || availableTargets.Count == 0)
            return;

        var previousIndex = ExploitPlayersList.FindIndex(player =>
            player.GetPlayerId() == previousTargetId && !player.Passives.IsDead);
        var nextIndex = previousIndex + 1;
        var canContinueQueue = previousIndex >= 0
                               && nextIndex < ExploitPlayersList.Count
                               && !ExploitPlayersList[nextIndex].Passives.IsDead;
        var nextTarget = canContinueQueue ? ExploitPlayersList[nextIndex] : availableTargets[0];
        LastExploit = ExploitPlayersList.IndexOf(nextTarget);

        nextTarget.Passives.IsExploitFixed = false;
        nextTarget.Passives.IsExploitable = true;
        CurrentExploitTargetPlayerId = nextTarget.GetPlayerId();
    }

    public void CloseExploit(GamePlayerBridgeClass target)
    {
        ExploitClosed = true;
        CurrentExploitTargetPlayerId = Guid.Empty;
        LastExploit = -1;
        foreach (var player in PlayersList)
            player.Passives.IsExploitable = false;
        if (target != null)
            target.Passives.IsExploitFixed = true;
    }

    /// <summary>Monotonic count of readiness-loop visits. The simulation watchdog uses this
    /// instead of wall time so a slow pass over a large batch is not mistaken for a frozen game.</summary>
    internal long ReadinessLoopVisits;
}
