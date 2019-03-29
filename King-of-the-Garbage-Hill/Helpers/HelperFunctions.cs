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
        private readonly SecureRandom _secureRandom;
        private readonly CharactersPull _charactersPull;
        private readonly Global _global;


        public HelperFunctions(CharactersPull charactersPull, Global global, UserAccounts accounts, SecureRandom secureRandom)
        {
            _charactersPull = charactersPull;
            _global = global;
            _accounts = accounts;
            _secureRandom = secureRandom;
        }


        public Task InitializeAsync()
            => Task.CompletedTask;

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

        public void SubstituteUserWithBot(ulong userId)
        {
            var prevGame = _global.GamesList.Find(
                x => x.PlayersList.Any(m => m.DiscordAccount.DiscordId == userId));
           
            if (prevGame != null)
            {
                CharacterClass character;
                do
                {
                    var index = _secureRandom.Random(0, _charactersPull.AllCharacters.Count - 1);
                    character = _charactersPull.AllCharacters[index];
                } while (prevGame.PlayersList.Any(x => x.Character.Name == character.Name));

                DiscordAccountClass account;

                ulong i = 1;
                do
                {
                    account  = _accounts.GetAccount(i);
                    i++;
                } while (account.IsPlaying);

                account.DiscordUserName = character.Name + " - BOT";
                account.GameId = prevGame.GameId;
                account.IsPlaying = true;
                _accounts.SaveAccounts(account.DiscordId);
                prevGame.PlayersList.Find(x => x.DiscordAccount.DiscordId == userId).DiscordAccount = account;
            }
        }

    }
}