using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Helpers
{
    public sealed class HelperFunctions : IServiceSingleton
    {
        private readonly UserAccounts _accounts;
        private readonly LoginFromConsole _logs;

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
            "AllMight"
        };

      
        private readonly Global _global;
        private readonly SecureRandom _secureRandom;


        public HelperFunctions( Global global, UserAccounts accounts,
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


        public async Task SendMsgAndDeleteItAfterRound(GamePlayerBridgeClass player, string msg)
        {
            try
            {
                if (!player.IsBot())
                {
                    var mess2 = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(msg);
                    player.DeleteMessages.Add(mess2.Id);
                }
            }
            catch (Exception e)
            {
                _logs.Critical(e.StackTrace);
            }
        }


        public async Task DeleteItAfterRound(GamePlayerBridgeClass player)
        {
            try
            {
                if (!player.IsBot())
                {
                    for (var i = player.DeleteMessages.Count - 1; i >= 0; i--)
                    {
                        var m = await player.Status.SocketMessageFromBot.Channel.GetMessageAsync(player.DeleteMessages[i]);
                        await m.DeleteAsync();
                        player.DeleteMessages.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                _logs.Critical(e.StackTrace);
            }
        }



        public void SubstituteUserWithBot(ulong discordId)
        {
            var prevGame = _global.GamesList.Find(
                x => x.PlayersList.Any(m => m.DiscordId == discordId));

            if (prevGame == null) return;

            var freeBot = GetFreeBot(prevGame.PlayersList);
            var leftUser = prevGame.PlayersList.Find(x => x.DiscordId == discordId);

            leftUser.DiscordId = freeBot.DiscordId;
            leftUser.DiscordUsername = freeBot.DiscordUserName;
            leftUser.PlayerType = freeBot.PlayerType;
            leftUser.Status.SocketMessageFromBot = null;
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
    }
}