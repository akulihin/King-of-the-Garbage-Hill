using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using King_of_the_Garbage_Hill.API.DTOs;
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
        GameVersion = "Версия: 4.3.5";
        GameMode = gameMode;
        CreatorId = creatorId;
        Teams = new List<TeamPlay>();
        ExploitPlayersList = [.. PlayersList.Where(player => player.GameCharacter.Passive.All(x => x.PassiveName != "Exploit"))];
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

    private string GlobalLogs { get; set; }
    public string GameMode { get; set; }
    public ulong CreatorId { get; set; }
    public List<TeamPlay> Teams { get; set; }
    public uint TestFightNumber { get; set; }

    /// <summary>Bot AI difficulty. 1 = legacy, 2 = smarter heuristics (same skeleton),
    /// 3 = omniscient predictions from AiFullKnowledgeRound. Default 3 everywhere, incl.
    /// Discord/web games; the sim harness can override per-run via --ai-difficulty.</summary>
    public int AiDifficulty { get; set; } = 3;

    /// <summary>Round from which AiDifficulty-3 bots know every enemy's character (tunable; may become 2 or 1).</summary>
    public int AiFullKnowledgeRound { get; set; } = 3;

    public List<BotsBehavior.NanobotClass> NanobotsList { get; set; } = new();

    public bool IsAramPickPhase { get; set; }
    public bool IsDraftPickPhase { get; set; }
    public Dictionary<Guid, List<CharacterClass>> DraftOptions { get; set; } = new();

    public List<GamePlayerBridgeClass> ExploitPlayersList { get; set; }
    public int LastExploit { get; set; } = -1;
    public int TotalExploit { get; set; } = 0;

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
        // no Баг in this game — nobody can consume the exploit (m5)
        if (ExploitPlayersList.Count == PlayersList.Count)
        {
            return;
        }
        if (ExploitPlayersList.Count(x => x.Passives.IsExploitFixed) == ExploitPlayersList.Count)
        {
            return;
        }
        LastExploit++;
        if (LastExploit >= ExploitPlayersList.Count)
        {
            LastExploit = 0;
        }

        foreach (var player in ExploitPlayersList)
        {
            player.Passives.IsExploitable = false;
        }

        while (true)
        {
            if (LastExploit >= ExploitPlayersList.Count)
            {
                LastExploit = 0;
            }

            if (ExploitPlayersList[LastExploit].Passives.IsExploitFixed)
            {
                LastExploit++;
            }
            else
            {
                ExploitPlayersList[LastExploit].Passives.IsExploitable = true;
                break;
            }
        }
    }
}
