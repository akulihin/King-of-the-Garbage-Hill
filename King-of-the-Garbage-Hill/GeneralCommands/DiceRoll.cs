
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