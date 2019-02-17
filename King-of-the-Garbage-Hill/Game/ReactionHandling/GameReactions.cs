using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public sealed class GameReaction : IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        private readonly UserAccounts _accounts;

        private readonly Global _global;

        private readonly GameUpdateMess _upd;

        private readonly HelperFunctions _help;

        public GameReaction(UserAccounts accounts,
            Global global,
            GameUpdateMess upd, HelperFunctions help)
        {
            _accounts = accounts;

            _global = global;

            _upd = upd;
            _help = help;
        }

        public async Task ReactionAddedForOctoGameAsync(Cacheable<IUserMessage, ulong> cash,
            ISocketMessageChannel channel, SocketReaction reaction)
        {
            for (var i = 0; i < _global.GamesList.Count; i++)
            {
                if (!_global.GamesList[i].PlayersList
                    .Any(x => x.DiscordId == reaction.UserId && x.MsgFromBotId == reaction.MessageId))
                    continue;


                var account = _accounts.GetAccount(reaction.UserId);


                // if (!account.IsAbleToTurn){return;}

                switch (reaction.Emote.Name)
                {
                    case "📖":
                        await _upd.Logs(reaction, reaction.Message.Value);
                        break;

                    case "❌":
                        await _upd.EndGame(reaction, reaction.Message.Value);
                        break;


                    case "🛡" when account.IsAbleToTurn:
                        account.IsBlock = true;
                        account.IsAbleToTurn = false;
                        account.IsReady = true;
                        _accounts.SaveAccounts(account);
                        break;

                    default:
                        var emoteNum = GetNumberFromEmote(reaction);

                        if (account.MoveListPage == 3)
                        {
                            await GetLvlUp(account, emoteNum, reaction.Message.Value);
                            break;
                        }

                        if (account.MoveListPage == 2)
                        {
                           var mess = await reaction.Channel.SendMessageAsync($"Нажми на {new Emoji("📖")}, чтобы вернуться в основное меню.");
                           await _help.DeleteMessOverTime(mess, 6);
                            break;
                        }

                        if (account.MoveListPage == 1)
                        {
                            account.WhoToAttackThisTurn =
                                _global.GamesList.Find(x => x.GameId == account.GameId).PlayersList
                                    .Find(x => x.PlaceAtLeaderBoard == emoteNum).DiscordId;

                            account.IsAbleToTurn = false;
                            account.IsReady = true;
                            account.IsBlock = false;
                            _accounts.SaveAccounts(account);
                        }

                        break;
                }
            }


            if (!(channel is IDMChannel))
                await reaction.Message.Value.RemoveReactionAsync(reaction.Emote,
                    reaction.User.Value, RequestOptions.Default);
        }



        public async Task GetLvlUp(MainAccountClass account, ulong skillNumber, IUserMessage socketMsg)
        {
            switch ((int)skillNumber)
            {
                case 1:
                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4:
                    break;
                default:
                    return;
            }

            await _upd.MainPage(account.DiscordId, socketMsg);
        }

        public ulong GetNumberFromEmote(SocketReaction reaction)
        {
            switch (reaction.Emote.Name)
            {
                case "1⃣":
                {
                    return 1;
                }

                case "2⃣":
                {
                    return 2;
                }

                case "3⃣":
                {
                    return 3;
                }

                case "4⃣":
                {
                    return 4;
                }

                case "5⃣":
                {
                    return 5;
                }

                case "6⃣":
                {
                    return 6;
                }

                default:
                {
                    return 99;
                }
            }
        }
    }
}