using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill;

public sealed class Global : IServiceSingleton
{
    public readonly DiscordShardedClient Client;
    public readonly DateTime TimeBotStarted;


    public List<GameClass> GamesList = new();

    public ConcurrentDictionary<ulong, Stopwatch> TimeSpendOnLastMessage = new();


    public Global(DiscordShardedClient client)
    {
        Client = client;
        TimeBotStarted = DateTime.Now;
    }

    public uint TotalCommandsIssued { get; set; }
    public uint TotalCommandsDeleted { get; set; }
    public uint TotalCommandsChanged { get; set; }
    private ulong GamePlayingAndId { get; set; }

    public ConcurrentDictionary<string, WinRateClass> WinRates = new();

    public ConcurrentDictionary<ulong, GameClass> FinishedGamesList = new();


    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    public GamePlayerBridgeClass GetGameAccount(ulong userId, ulong gameId)
    {
        return GamesList.Find(x => x.GameId == gameId).PlayersList.Find(x => x.DiscordId == userId);
    }


    public ulong GetLastGamePlayingAndId()
    {
        return GamePlayingAndId;
    }

    public ulong GetNewtGamePlayingAndId()
    {
        GamePlayingAndId++;
        return GamePlayingAndId;
    }

    public class WinRateClass
    {
        public string CharacterName { get; set; }
        public WinRateClass( string characterName)
        {
            CharacterName = characterName;
        }

        public double Top1 { get; set; }
        public double Top2 { get; set; }
        public double Top3 { get; set; }
        public double Top4 { get; set; }
        public double Top5 { get; set; }
        public double Top6 { get; set; }
        public double GameTimes { get; set; }
        public double WinRate { get; set; }
        public double Elo { get; set; }
    }
}