using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Helpers
{
    public sealed class HelperFunctions : IServiceSingleton
    {
        private readonly UserAccounts _accounts;

        private readonly List<string> _characterNames = new List<string>
        {
            "UselessCrab",
            "Daumond",
            "MegaVova99",
            "YasuoOnly",
            "PETYX",
            "Drone"
        };

        private readonly CharactersPull _charactersPull;
        private readonly Global _global;
        private readonly SecureRandom _secureRandom;

        public HelperFunctions(CharactersPull charactersPull, Global global, UserAccounts accounts,
            SecureRandom secureRandom)
        {
            _charactersPull = charactersPull;
            _global = global;
            _accounts = accounts;
            _secureRandom = secureRandom;
        }


        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DeleteBotAndUserMessage(IUserMessage botMessage, SocketMessage userMessage,
            int timeInSeconds)
        {
            var seconds = timeInSeconds * 1000;
            await Task.Delay(seconds);
            await botMessage.DeleteAsync();
            await userMessage.DeleteAsync();
        }

        public async Task DeleteMessOverTime(IUserMessage message, int timeInSeconds)
        {
            var seconds = timeInSeconds * 1000;
            await Task.Delay(seconds);
            await message.DeleteAsync();
        }

        public void SubstituteUserWithBot(ulong discordId)
        {
            var prevGame = _global.GamesList.Find(
                x => x.PlayersList.Any(m => m.DiscordAccount.DiscordId == discordId));

            if (prevGame != null)
            {
                var account = GetFreeBot(prevGame.PlayersList, prevGame.GameId);
                var leftUser = prevGame.PlayersList.Find(x => x.DiscordAccount.DiscordId == discordId);
                leftUser.DiscordAccount = account.DiscordAccount;
                leftUser.Status.SocketMessageFromBot = null;
            }
        }

        public GamePlayerBridgeClass GetFreeBot(List<GamePlayerBridgeClass> playerList, ulong newGameId)
        {
            CharacterClass character;
            DiscordAccountClass account;
            string name;
            do
            {
                var index = _secureRandom.Random(0, _charactersPull.AllCharacters.Count - 1);
                character = _charactersPull.AllCharacters[index];
            } while (playerList.Any(x => x.Character.Name == character.Name));

            do
            {
                var index = _secureRandom.Random(0, _characterNames.Count - 1);
                name = _characterNames[index];
            } while (playerList.Any(x => x.DiscordAccount.DiscordUserName == name));

            ulong i = 1;
            do
            {
                account = _accounts.GetAccount(i);
                i++;
            } while (account.IsPlaying);

            account.DiscordUserName = name;
            account.GameId = newGameId;
            account.IsPlaying = true;
            _accounts.SaveAccounts(account.DiscordId);

            return new GamePlayerBridgeClass
                {DiscordAccount = account, Character = character, Status = new InGameStatus()};
        }
    }
}