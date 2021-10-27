using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public class TutorialReactions : IServiceSingleton
    {
        private readonly CharactersPull _charactersPull;
        private readonly UserAccounts _userAccounts;
        private readonly SecureRandom _random;

        public TutorialReactions(UserAccounts userAccounts, CharactersPull charactersPull, SecureRandom random)
        {
            _userAccounts = userAccounts;
            _charactersPull = charactersPull;
            _random = random;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ShowRulesAndChar(SocketUser user, GamePlayerBridgeClass player)
        {
            var pass = "";
            var characterPassivesList = player.Character.Passive;
            foreach (var passive in characterPassivesList)
            {
                if (!passive.Visible) continue;
                pass += $"__**{passive.PassiveName}**__";
                pass += ": ";
                pass += passive.PassiveDescription;
                pass += "\n";
            }


            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkOrange);
            embed.AddField("Твой Персонаж:", $"Name: {player.Character.Name}\n" +
                                             $"Интеллект: {player.Character.GetIntelligence()}\n" +
                                             $"Сила: {player.Character.GetStrength()}\n" +
                                             $"Скорость: {player.Character.GetSpeed()}\n" +
                                             $"Психика: {player.Character.GetPsyche()}\n");
            embed.AddField("Пассивки", $"{pass}");

            await user.SendMessageAsync("", false, embed.Build());
        }



        public async Task ReactionAddedTutorial(SocketMessageComponent button)
        {
            try
            {
                //var value = string.Join("", button.Data.Values);

                switch (button.Data.CustomId)
                {
                    case "tutorial-1":
                        break;
                    case "tutorial-2":
                        break;
                    case "tutorial-3":
                        break;
                }
            }
            catch
            {
                //ingored
            }
        }
    }
}