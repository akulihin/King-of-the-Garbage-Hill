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

    public ConcurrentDictionary<ulong, Stopwatch> TimeSpendOnLastMessage =
        new();


    public Global(DiscordShardedClient client)
    {
        Client = client;
        TimeBotStarted = DateTime.Now;
    }

    public uint TotalCommandsIssued { get; set; }
    public uint TotalCommandsDeleted { get; set; }
    public uint TotalCommandsChanged { get; set; }
    private ulong GamePlayingAndId { get; set; }

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
}