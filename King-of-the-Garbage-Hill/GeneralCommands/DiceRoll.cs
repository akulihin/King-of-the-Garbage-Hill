using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class DiceRollCommands : ModuleBaseCustom
    {
        private readonly SecureRandom _secureRandom;

        public DiceRollCommands(SecureRandom secureRandom)
        {
            _secureRandom = secureRandom;
    
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

        [Command("rst")]
        [Summary("Showing how good is my random")]
        public async Task RandomStats()
        {
            ulong times = 10000;

            var statsList = new List<StatsStruct>
            {
                new StatsStruct(1, 0),
                new StatsStruct(2, 0),
                new StatsStruct(3, 0),
                new StatsStruct(4, 0),
                new StatsStruct(5, 0),
                new StatsStruct(6, 0),
                new StatsStruct(7, 0),
                new StatsStruct(8, 0),
                new StatsStruct(9, 0),
                new StatsStruct(10, 0)
            };

            for (ulong i = 0; i < times; i++)
            {
                var ran = _secureRandom.Random(1, 10);
                statsList.Find(x => x.Number == ran).Count += 1;

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

                await SendMessAsync(embed);
  
        }

        [Command("roll")]
        [Alias("Роллл", "Ролл")]
        [Summary("Rolling a dice multiple times")]
        public async Task Roll(int number, int times)
        {
            try
            {
                var mess = "";
                if (times > 100)
                {
                    await SendMessAsync(
                        "Boole! We are not going to roll that many times!");


                    return;
                }

                if (number > 999999999)
                {
                    await SendMessAsync(
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

                await SendMessAsync( embed);
            }
            catch
            {
                //   await ReplyAsync(
                //       "boo... An error just appear >_< \nTry to use this command properly: **roll [times] [max_value_of_roll]**\n" +
                //       "Alias: Роллл, Ролл");
            }
        }

        [Command("roll")]
        [Alias("Роллл", "Ролл")]
        [Summary("Rolling a dice 1 time")]
        public async Task Roll(int number)
        {
                var randomIndexRoll = _secureRandom.Random(1, number);

                await SendMessAsync( $"It's a {randomIndexRoll}!");
        }
    }
}