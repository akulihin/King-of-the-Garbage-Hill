using System.Linq;
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

        public void ReactionAddedStore(Cacheable<IUserMessage, ulong> cash, ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            cash.DownloadAsync();

            if (!cash.HasValue) return;
            if (cash.Value.Embeds.FirstOrDefault()?.Title != "Магазин") return;
            switch (reaction.Emote.Name)
            {
                case "1️⃣":
                    var d = 0;
                    break;

            }
        }
    }
}
