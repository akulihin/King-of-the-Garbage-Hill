using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;

namespace King_of_the_Garbage_Hill
{
    public sealed class Global : IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Global(DiscordShardedClient client)
        {
            Client = client;
            TimeBotStarted = DateTime.Now;
            GamePlayingAndId = 0;
            TotalCommandsIssued = 0;
            TotalCommandsDeleted = 0;
            TotalCommandsChanged = 0;
            TimeSpendOnLastMessage = new ConcurrentDictionary<ulong, Stopwatch>();
            GamesList = new List<GameClass>();
            
        }

        public readonly DiscordShardedClient Client;
        public readonly DateTime TimeBotStarted;
        private ulong GamePlayingAndId { get; set; }
        public uint TotalCommandsIssued;
        public uint TotalCommandsDeleted;
        public uint TotalCommandsChanged;
        public ConcurrentDictionary<ulong, Stopwatch> TimeSpendOnLastMessage;


        public List<GameClass> GamesList;


        public GamePlayerBridgeClass GetGameAccount(ulong userId, ulong gameId)
        {
            return GamesList.Find(x => x.GameId == gameId).PlayersList.Find(x => x.DiscordAccount.DiscordId == userId);
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
}