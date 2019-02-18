using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class General : ModuleBaseCustom
    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning disable CS1998 // This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.

   
    
        private readonly UserAccounts _accounts;
        private readonly SecureRandom _secureRandom;
        private readonly OctoPicPull _octoPicPull;
        private readonly OctoNamePull _octoNmaNamePull;
        private readonly CharactersPull _charactersPull;
        private readonly HelperFunctions _helperFunctions;
  
        private readonly CommandsInMemory _commandsInMemory;
        private readonly Global _global;
        private readonly GameUpdateMess _upd;

      
        

  
        public General( UserAccounts accounts, SecureRandom secureRandom, OctoPicPull octoPicPull, 
            OctoNamePull octoNmaNamePull, HelperFunctions helperFunctions,  CommandsInMemory commandsInMemory, Global global, GameUpdateMess upd, CharactersPull charactersPull) 
        {   
            _accounts = accounts;
            _secureRandom = secureRandom;
            _octoPicPull = octoPicPull;
            _octoNmaNamePull = octoNmaNamePull;
            _helperFunctions = helperFunctions;  
            _commandsInMemory = commandsInMemory;
            _global = global;
            _upd = upd;
            _charactersPull = charactersPull;
        }


        
        [Command("s")]
        [RequireOwner]
        public async Task StartGame(SocketUser user2 = null , SocketUser user3 = null, SocketUser user4 = null, SocketUser user5 = null, SocketUser user6 = null)
        {
            var rawList = new List<SocketUser>
            {
                Context.User,
                user2,
                user3,
                user4,
                user5,
                user6        
            };
            var playersList = new List<GameBridgeClass>();
            var availableChamps = _charactersPull.AllCharacters;


            for (var i = 0; i < rawList.Count; i++)
            {
                var t = rawList[i];
                if (t != null)
                {
                    //TODO get new character

                    var account = _accounts.GetAccount(t.Id);
                    account.GameId = _global.GamePlayingAndId;
                    account.IsPlaying = true;
                    _accounts.SaveAccounts(account.DiscordId);

                    var randomIndex = _secureRandom.Random(0, availableChamps.Count - 1);
                    var character = availableChamps[randomIndex];
                    availableChamps.RemoveAt(randomIndex);


                    var status = new InGameStatus();

                    var bridge = new GameBridgeClass {Account = account, Character = character, Status = status};

                    playersList.Add(bridge);
                }
                else
                {
                    //TODO: add a bot

                    var account = _accounts.GetAccount((ulong)i);
                    account.GameId = _global.GamePlayingAndId;
                    account.IsPlaying = true;
                    _accounts.SaveAccounts(account.DiscordId);

                    var randomIndex = _secureRandom.Random(0, availableChamps.Count - 1);
                    var character = availableChamps[randomIndex];
                    availableChamps.RemoveAt(randomIndex);

                    var status = new InGameStatus( );

                    var bridge = new GameBridgeClass {Account = account, Character = character, Status = status};

                        //playersList.Add(bridge);
                }
            }

            //randomize order
            playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
            for (var i = 0; i < playersList.Count; i++){playersList[i].Status.PlaceAtLeaderBoard = i + 1;}

            var game = new GameClass(playersList,  _global.GamePlayingAndId);

            foreach (var t in playersList)
            {
                if(t.Account.DiscordId > 1000)
                _upd.WaitMess(t);
            }

       
            game.TimePassed.Start();
            _global.GamePlayingAndId++;
            _global.GamesList.Add(game);
        }
       
       [Command("cc")]
       [RequireOwner]
       public async Task CheckSomething()
       {
           var game = _global.GamesList[0];
           var player =                    
               game.PlayersList.Find(x => x.Account.DiscordId == Context.User.Id);
           player.Status.Score++;
           var ff = 0;
       }

        [Command("updMaxRam")]
        [RequireOwner]
        [Summary("updates maximum number of commands bot will save in memory (default 1000 every time you launch this app)")]
        public async Task ChangeMaxNumberOfCommandsInRam(uint number)
        {
            _commandsInMemory.MaximumCommandsInRam = number;
            SendMessAsync($"now I will store {number} of commands");
        }

        [Command("clearMaxRam")]
        [RequireOwner]
        [Summary("CAREFUL! This will delete ALL commands in ram")]
        public async Task ClearCommandsInRam()
        {
            var toBeDeleted = _commandsInMemory.CommandList.Count;
            _commandsInMemory.CommandList.Clear();
            SendMessAsync($"I have deleted {toBeDeleted} commands");
        }

        [Command("uptime")]
        [Summary("showing general info about the bot")]
        public async Task UpTime()
        {
            _global.TimeSpendOnLastMessage.TryGetValue(Context.User.Id, out var watch);

            var time = DateTime.Now -_global.TimeBotStarted;
            var ramUsage = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);

            var embed = new EmbedBuilder()
               // .WithAuthor(Context.Client.CurrentUser)
               // .WithTitle("My internal statistics")
                .WithColor(Color.DarkGreen)
                .WithCurrentTimestamp()
                .WithFooter("Version: 0.0a | Thank you for using me!")
                .AddField("Numbers:", "" +
                                      $"Working for: {time.Days}d {time.Hours}h {time.Minutes}m and {time:ss\\.fff}s\n" +
                                      $"Total Games Started: {_global.GamePlayingAndId}\n" +
                                      $"Total Commands issued while running: {_global.TotalCommandsIssued}\n" +
                                      $"Total Commands changed: {_global.TotalCommandsChanged}\n" +
                                      $"Total Commands deleted: {_global.TotalCommandsDeleted}\n" +
                                      $"Total Commands in memory: {_commandsInMemory.CommandList.Count} (max {_commandsInMemory.MaximumCommandsInRam})\n" +
                                      $"Client Latency: {_global.Client.Latency}\n" +
                                      $"Time I spend processing your command: {watch?.Elapsed:m\\:ss\\.ffff}s\n" +
                                      $"This time counts from from the moment he receives this command.\n" +
                                      $"Memory Used: {ramUsage}");


            SendMessAsync(embed);

            // Context.User.GetAvatarUrl()
        }

        [Command("myPrefix")]
        [Summary("Shows or sets YOUR OWN prefix for the bot")]
        public async Task SetMyPrefix([Remainder] string prefix = null)
        {
            var account = _accounts.GetAccount(Context.User);

            if (prefix == null)
            {
                var myAccountPrefix = account.MyPrefix ?? "You don't have own prefix yet";

                await SendMessAsync(
                    $"Your prefix: **{myAccountPrefix}**");
                return;
            }

            if (prefix.Length < 100)
            {
                account.MyPrefix = prefix;
                if (prefix.Contains("everyone") || prefix.Contains("here"))
                {
                    await SendMessAsync(
                        "No `here` or `everyone` prefix allowed.");
                    return;
                }

                _accounts.SaveAccounts(Context.User);
                await SendMessAsync(
                    $"Booole~, your own prefix is now **{prefix}**");
            }
            else
            {
                await SendMessAsync(
                    "Booooo! Prefix have to be less than 100 characters");
            }
        }

        [Command("octo")]
        [Alias("окто", "octopus", "Осьминог", "Осьминожка", "Осьминога", "o", "oct", "о")]
        [Summary("Куда же без осьминожек")]
        public async Task OctopusPicture()
        {
            var octoIndex = _secureRandom.Random(0, _octoPicPull.OctoPics.Length-1);
            var octoToPost = _octoPicPull.OctoPics[octoIndex];


            var color1Index = _secureRandom.Random(0, 255);
            var color2Index = _secureRandom.Random(0, 255);
            var color3Index = _secureRandom.Random(0, 255);

            var randomIndex = _secureRandom.Random(0, _octoNmaNamePull.OctoNameRu.Length-1);
            var randomOcto = _octoNmaNamePull.OctoNameRu[randomIndex];

            var embed = new EmbedBuilder();
            embed.WithDescription($"{randomOcto} found:");
            embed.WithFooter("lil octo notebook");
            embed.WithColor(color1Index, color2Index, color3Index);
            embed.WithAuthor(Context.User);
            embed.WithImageUrl("" + octoToPost);

            await SendMessAsync( embed);

            switch (octoIndex)
            {
                case 19:
                    {
                        var lll = await Context.Channel.SendMessageAsync("Ooooo, it was I who just passed Dark Souls!");
                        _helperFunctions.DeleteMessOverTime(lll, 6);
                        break;
                    }
                case 9:
                    {
                        var lll = await Context.Channel.SendMessageAsync("I'm drawing an octopus :3");
                        _helperFunctions.DeleteMessOverTime(lll, 6);
                        break;
                    }
                case 26:
                    {
                        var lll = await Context.Channel.SendMessageAsync(
                            "Oh, this is New Year! time to gift turtles!!");
                        _helperFunctions.DeleteMessOverTime(lll, 6);
                        break;
                    }
            }
        }
    }
}