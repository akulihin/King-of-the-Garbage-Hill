using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;
using  King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;


namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public sealed class GameReaction : IServiceSingleton
    {
        public Task InitializeAsync()
            => Task.CompletedTask;

        private readonly UserAccounts _accounts;

        private readonly Global _global;

        private readonly GameUpdateMess _upd;


        public GameReaction(UserAccounts accounts, 
            Global global,
          GameUpdateMess upd)
        {
            _accounts = accounts;
         
            _global = global;
       
            _upd = upd;
        }

        public async Task ReactionAddedForOctoGameAsync(Cacheable<IUserMessage, ulong> cash,
            ISocketMessageChannel channel, SocketReaction reaction)
        {
                for (var i = 0; i < _global.GamesList.Count; i++)
                {

                    if(!_global.GamesList[i].PlayersList.Any( x => x.DiscordId == reaction.UserId && x.MsgFromBotId == reaction.MessageId))
                        continue;

                    var globalAccount = _global.Client.GetUser(reaction.UserId);
                    var account = _accounts.GetAccount(globalAccount);
                   // var enemy = _accounts.GetAccount(account.CurrentEnemy);

                    switch (reaction.Emote.Name)
                    {
                         case "🛡":
                           // TODO 
                           var f = true;
                            break;
                        case "📖":
                            await _upd.Logs(reaction, reaction.Message.Value);
                            break;
                    case "⬆":
                        //TODO
                        break;
                        case "❌":
                          
                             await _upd.EndGame(reaction,reaction.Message.Value);
                            break;
                        case "1⃣":
                        {
                            if (account.IsPlaying)
                            {
                                break;
                            }

                            break;
                        }

                        case "2⃣":
                        {
                            if (account.IsPlaying )
                            {
                                break;
                            }

                            break;
                        }

                        case "3⃣":
                        {
                            if (account.IsPlaying)
                            {
                                break;
                            }

                            if (account.IsPlaying)
                               {}

                            break;
                        }

                        case "4⃣":
                        {
                           {}
                            break;
                        }

                        case "5⃣":
                        {
                           {}
                            break;
                        }

                        case "6⃣":
                        {
                           {}
                            break;
                        }
                    }

                }


            if(!(channel is IDMChannel))
            await reaction.Message.Value.RemoveReactionAsync(reaction.Emote,
                reaction.User.Value, RequestOptions.Default);
        }
    }
}