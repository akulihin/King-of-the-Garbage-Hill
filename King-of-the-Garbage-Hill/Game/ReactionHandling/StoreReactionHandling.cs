using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public class StoreReactionHandling : IServiceSingleton
    {
        private readonly UserAccounts _userAccounts;

        public StoreReactionHandling(UserAccounts userAccounts)
        {
            _userAccounts = userAccounts;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ReactionAddedStore(Cacheable<IUserMessage, ulong> cash, ISocketMessageChannel channel, SocketReaction reaction)
        {
        }
    }
}
