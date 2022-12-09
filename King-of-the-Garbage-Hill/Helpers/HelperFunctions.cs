using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Helpers;

public sealed class HelperFunctions : IServiceSingleton
{
    private readonly UserAccounts _accounts;

    private readonly List<string> _characterNames = new()
    {
        "UselessCrab",
        "Daumond",
        "MegaVova99",
        "YasuoOnly",
        "PETYX",
        "Drone",
        "Shark",
        "EloBoost",
        "Ratata",
        "R.I.D.",
        "2kEloPro",
        "Razer",
        "AllMight",
        "Taki",
        "Goose",
        "Kymberi",
        "Guardian",
        "Skillet",
        "Flash",
        "MicroCentral",
        "Fibonacl",
        "ArtFull",
        "Archer",
        "Anastasia",
        "Liba",
        "Solar",
        "Legue",
        "Morda",
        "KekJuice",
        "Waffle",
        "LogMeIn",
        "Atsassin",
        "Loki",
        "John",
        "Xsir",
        "qqq",
        "Access",
        "2kEloPro",
        "Tonuuu",
        "TTK",
        "Jay",
        "Duke",
        "Crucio",
        "Gladd",
        "Yet",
        "Pizza",
        "Olaf",
        "Pain",
        "Ultimatum",
        "Cut",
        "KilLaKill",
        "Mordor",
        "Avalanch",
        "Egg",
        "Simple",
        "Medoed",
        "Truck",
        "Fairy",
        "SIDESHOW",
        "PKPKPK",
        "GSX",
        "Coach",
        "Bell",
        "Salt",
        "Papito",
        "Bubble",
        "Frodo",
        "Shonen",
        "Tough",
        "Twiggy",
        "Chain",
        "Buds",
        "Diet Coke",
        "Heisenberg",
        "Lock",
        "Ash",
        "Fat",
        "Amorcita",
        "Juicy",
        "Kitten",
        "Apple",
        "Chump",
        "Chicken",
        "Stitch",
        "Goon",
        "Chewbacca",
        "PintSize",
        "Boomer",
        "Pig",
        "Daria",
        "Shut",
        "Tarzan",
        "Cruella",
        "Cotton",
        "Cringe",
        "Dear",
        "Loli",
        "Samurai",
        "CumLess",
        "M/J",
        "Effect",
        "Ruler",
        "Flat",
        "Snatch",
        "GoRdOn JaMzY",
        "Joker",
        "WhiteMain",
        "DropBox",
        "BlackHat",
        "Simca",
        "Huligan",
        "Butcha",
        "Angel",
        "Exotic",
        "FinalRemedy",
        "Christopher",
        "Simpson",
        "Croissant",
        "DrugLord",
        "Cyclon",
        "Mocorella",
        "Ninja",
        "Vortex",
        "Bunny",
        "Hawno",
        "Rat",
        "EdgeTurtle",
        "Paris",
        "Fleet",
        "Gold 5",
        "Vodka",
        "Elihandro",
        "SlyCrab",
        "Raider",
        "QuickFawn",
        "Alan Sparks",
        "PsychoMob",
        "Basic",
        "Doofus",
        "Squirt",
        "Guny",
        "Smuggler",
        "Tata",
        "Lion",
        "ButterCup",
        "BaBa",
        "Toots",
        "Skunk",
        "Skinny Minny",
        "Ticklebutt",
        "Pebbles",
        "Bandit",
        "TDawg",
        "Boo Bear",
        "Anvil",
        "Gumdrop",
        "Admiral",
        "Teeny"
    };


    private readonly Global _global;
    private readonly LoginFromConsole _logs;
    private readonly SecureRandom _secureRandom;
    private readonly List<Guid> _embedQueue = new();
    private readonly List<Guid> _messageQueue = new();

    public HelperFunctions(Global global, UserAccounts accounts,
        SecureRandom secureRandom, LoginFromConsole log)
    {
        _global = global;
        _accounts = accounts;
        _secureRandom = secureRandom;
        _logs = log;
    }


    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public string ReplaceLastOccurrence(string source, string find, string replace)
    {
        var place = source.LastIndexOf(find, StringComparison.Ordinal);
        return place == -1 ? source : source.Remove(place, find.Length).Insert(place, replace);
    }


    public async Task ModifyGameMessage(GamePlayerBridgeClass player, EmbedBuilder embed, ComponentBuilder components, string extraText = "", int delayMs = 0)
    {
        if (player.IsBot())
        {
            return;
        }

        if (embed.Footer.Text.Contains("ERROR"))
        {
            return;
        }

        try
        {
            while (_embedQueue.Contains(player.GetPlayerId()))
            {
                await Task.Delay(200);
            }
            _embedQueue.Add(player.GetPlayerId());

            await player.DiscordStatus.SocketMessageFromBot.ModifyAsync(message =>
            {
                message.Embed = embed.Build();
                message.Components = components.Build();
            });

            if (extraText.Length > 0)
            {
                await SendMsgAndDeleteItAfterRound(player, extraText, delayMs);
            }

            _embedQueue.Remove(player.GetPlayerId());
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }


    public async Task SendMsgAndDeleteItAfterRound(GamePlayerBridgeClass player, string msg, int delayMs)
    {
        try
        {
            if (player.IsBot())
                return;
        
            while (_messageQueue.Contains(player.GetPlayerId()))
            {
                await Task.Delay(300);
            }
            _messageQueue.Add(player.GetPlayerId());

            var mess2 = await player.DiscordStatus.SocketMessageFromBot.Channel.SendMessageAsync(msg);
            player.DeleteMessages.Add(new GamePlayerBridgeClass.DeleteMessagesClass(mess2.Id, delayMs));

            _messageQueue.Remove(player.GetPlayerId());
          
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }


    public async Task DeleteItAfterRound(GamePlayerBridgeClass player)
    {
        try
        {
            if (player.IsBot())
            {
                return;
            }

            foreach (var message in player.DeleteMessages.ToList())
            {
                player.DeleteMessages.Remove(message);

                var m = await player.DiscordStatus.SocketMessageFromBot.Channel.GetMessageAsync(message.MessageId);
                if (message.DelayMs > 0)
                {
                    await Task.Delay(message.DelayMs);
                }
                await m.DeleteAsync();
                
            }
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }


    public void SubstituteUserWithBot(ulong discordId)
    {
        var prevGame = _global.GamesList.Find(
            x => x.PlayersList.Any(m => m.DiscordId == discordId));

        if (prevGame == null) return;

        var freeBot = GetFreeBot(prevGame.PlayersList);
        var leftUser = prevGame.PlayersList.Find(x => x.DiscordId == discordId);

        leftUser!.DiscordId = freeBot.DiscordId;
        leftUser.DiscordUsername = freeBot.DiscordUserName;
        leftUser.PlayerType = freeBot.PlayerType;
        leftUser.DiscordStatus.SocketMessageFromBot = null;
        freeBot.IsPlaying = true;
    }

    public DiscordAccountClass GetFreeBot(List<GamePlayerBridgeClass> playerList)
    {
        DiscordAccountClass account;
        string name;

        do
        {
            var index = _secureRandom.Random(0, _characterNames.Count - 1);
            name = _characterNames[index];
        } while (playerList.Any(x => x.DiscordUsername == name));

        ulong botId = 1;
        do
        {
            account = _accounts.GetAccount(botId);
            botId++;
        } while (account.IsPlaying);

        account.DiscordUserName = name;


        return account;
    }

    public async Task DeleteMessOverTime(IUserMessage message, int timeInSeconds)
    {
        var seconds = timeInSeconds * 1000;
        await Task.Delay(seconds);
        await message.DeleteAsync();
    }
}