using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
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
        private readonly AwaitForUserMessage _awaitForUserMessage;
        private readonly OctoGameUpdateMess _upd;


        public GameReaction(UserAccounts accounts, 
            Global global,
            AwaitForUserMessage awaitForUserMessage, OctoGameUpdateMess upd)
        {
            _accounts = accounts;
         
            _global = global;
            _awaitForUserMessage = awaitForUserMessage;
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
                        case "🐙":

                          //  await _gameFramework.UpdateTurn(account, enemy);
                     
                            break;
                        case "⬅":
                            await _upd.MainPage(reaction.UserId, reaction.Message.Value);
                            break;
                        case "➡":
                            await _upd.Leaderboard(reaction, reaction.Message.Value);
                            break;
                        case "📖":
                            await _upd.Logs(reaction, reaction.Message.Value);
                            break;
                        case "❌":
                          //  if (await _awaitForUserMessage.FinishTheGame(reaction))
                          //      await _octoGameUpdateMess.EndGame(reaction,
                        //            reaction.Message.Value);
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

                        case "7⃣":
                        {
                           {}
                            break;
                        }

                        case "8⃣":
                        {
                           {}
                            break;
                        }

                        case "9⃣":
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