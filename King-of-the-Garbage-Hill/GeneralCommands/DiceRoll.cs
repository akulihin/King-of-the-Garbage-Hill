using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.GeneralCommands;

public class DiceRollCommands : ModuleBaseCustom
{
    private readonly SecureRandom _secureRandom;
    private readonly LoginFromConsole _logs;

    public DiceRollCommands(SecureRandom secureRandom, LoginFromConsole logs)
    {
        _secureRandom = secureRandom;
        _logs = logs;
    }

    [Command("rst")]
    [Summary("Showing how good is my random")]
    public async Task RandomStats()
    {
        ulong times = 10000;

        var statsList = new List<StatsStruct>
        {
            new(1, 0),
            new(2, 0),
            new(3, 0),
            new(4, 0),
            new(5, 0),
            new(6, 0),
            new(7, 0),
            new(8, 0),
            new(9, 0),
            new(10, 0)
        };

        for (ulong i = 0; i < times; i++)
        {
            var ran = _secureRandom.Random(1, 10);
            statsList.Find(x => x.Number == ran)!.Count += 1;
        }

        var mess = $"{statsList[0].Number} = {statsList[0].Count}\n" +
                   $"{statsList[1].Number} = {statsList[1].Count}\n" +
                   $"{statsList[2].Number} = {statsList[2].Count}\n" +
                   $"{statsList[3].Number} = {statsList[3].Count}\n" +
                   $"{statsList[4].Number} = {statsList[4].Count}\n" +
                   $"{statsList[5].Number} = {statsList[5].Count}\n" +
                   $"{statsList[6].Number} = {statsList[6].Count}\n" +
                   $"{statsList[7].Number} = {statsList[7].Count}\n" +
                   $"{statsList[8].Number} = {statsList[8].Count}\n" +
                   $"{statsList[9].Number} = {statsList[9].Count}\n";

        var embed = new EmbedBuilder();

        embed.WithTitle($"Roll {times} times:");
        embed.WithDescription($"{mess}");

        await SendMessageAsync(embed);
    }

    [Command("Ролл")]
    [Alias("Роллл", "roll")]
    [Summary("Rolling a dice multiple times")]
    public async Task Roll(int number, int times)
    {
        try
        {
            var mess = "";
            if (times > 100)
            {
                await SendMessageAsync(
                    "Boole! We are not going to roll that many times!");


                return;
            }

            if (number > 999999999)
            {
                await SendMessageAsync(
                    "Boole! This numbers is way too big for us :c");


                return;
            }

            for (var i = 0; i < times; i++)
            {
                var randomIndexRoll = _secureRandom.Random(1, number);
                mess += $"It's a {randomIndexRoll}!\n";
            }

            var embed = new EmbedBuilder();
            embed.WithFooter("lil octo notebook");
            embed.WithTitle($"Roll {times} times:");
            embed.WithDescription($"{mess}");

            await SendMessageAsync(embed);
        }
        catch (Exception exception)
        {
            _logs.Critical(exception.Message);
            _logs.Critical(exception.StackTrace);
        }
    }

    [Command("Роллл")]
    [Alias("Роллл", "roll")]
    [Summary("Rolling a dice 1 time")]
    public async Task Roll(int number)
    {
        var randomIndexRoll = _secureRandom.Random(1, number);
        await SendMessageAsync($"It's a {randomIndexRoll}!");
    }

    public class StatsStruct
    {
        public readonly int Number;
        public int Count;

        public StatsStruct(int number, int count)
        {
            Number = number;
            Count = count;
        }
    }
}