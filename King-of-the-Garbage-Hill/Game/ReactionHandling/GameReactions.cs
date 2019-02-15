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
     //   private readonly GameFramework _gameFramework;


        public GameReaction(UserAccounts accounts, 
            Global global,
            AwaitForUserMessage awaitForUserMessage)
        {
            _accounts = accounts;
         
            _global = global;
            _awaitForUserMessage = awaitForUserMessage;
     
        }

        public async Task ReactionAddedForOctoGameAsync(Cacheable<IUserMessage, ulong> cash,
            ISocketMessageChannel channel, SocketReaction reaction)
        {
                for (var i = 0; i < _global.OctopusGameMessIdList.Count; i++)
                {

                    if(!_global.OctopusGameMessIdList[i].Any( x => x.PlayerDiscordAccount.Id == reaction.UserId && x.GamingWindowFromBot.Id == reaction.MessageId))
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
                         //   await _octoGameUpdateMess.SkillPageLeft(reaction,
                         //       reaction.Message.Value);
                            break;
                        case "➡":
                         //   await _octoGameUpdateMess.SkillPageRight(reaction,
                         //       reaction.Message.Value);
                            break;
                        case "📖":
                            //  await _octoGameUpdateMess.OctoGameLogs(reaction,
                            //       _global.OctopusGameMessIdList[i].bot_gaming_msg_1);
                          //  account.MoveListPage = 5;
                         //   await _octoGameUpdateMess.MainPage(reaction.UserId,
                         //       reaction.Message.Value);
                            break;
                        case "❌":
                          //  if (await _awaitForUserMessage.FinishTheGame(reaction))
                          //      await _octoGameUpdateMess.EndGame(reaction,
                        //            reaction.Message.Value);
                            break;
                        case "1⃣":
                        {
                            if (account.PlayingStatus == 1)
                            {

                                break;
                            }

                            if (account.PlayingStatus == 2)
                               {}

                            break;
                        }

                        case "2⃣":
                        {
                            if (account.PlayingStatus == 1)
                            {
          
                                break;
                            }

                            if (account.PlayingStatus == 2)
                               {}
                            break;
                        }

                        case "3⃣":
                        {
                            if (account.PlayingStatus == 1)
                            {
 
                                break;
                            }

                            if (account.PlayingStatus == 2)
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