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
        private readonly UserAccounts _accounts;

        private readonly Global _global;

        private readonly HelperFunctions _help;

        private readonly GameUpdateMess _upd;

        public GameReaction(UserAccounts accounts,
            Global global,
            GameUpdateMess upd, HelperFunctions help)
        {
            _accounts = accounts;

            _global = global;

            _upd = upd;
            _help = help;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ReactionAddedGameWindow(Cacheable<IUserMessage, ulong> cash,
            ISocketMessageChannel channel, SocketReaction reaction)
        {
            foreach (var t in _global.GamesList)
                if (t.PlayersList.Any(x =>
                    x.DiscordAccount.DiscordId == reaction.UserId &&
                    x.Status.SocketMessageFromBot.Id == reaction.MessageId))
                {
                    var account = _accounts.GetAccount(reaction.UserId);
                    var gameBridge = _global.GetGameAccount(reaction.UserId, account.GameId);
                    var status = gameBridge.Status;

                    // if (!discordAccount.IsAbleToTurn){return;}

                    switch (reaction.Emote.Name)
                    {
                        case "❌":
                            await _upd.EndGame(reaction, reaction.Message.Value);
                            break;

                        case "📖":

                            if (gameBridge.Status.MoveListPage == 1)
                                gameBridge.Status.MoveListPage = 2;
                            else if (gameBridge.Status.MoveListPage == 2) gameBridge.Status.MoveListPage = 1;

                            await _upd.UpdateMessage(gameBridge);
                            break;


                        case "🛡" when status.IsAbleToTurn:
                            if (status.MoveListPage == 3)
                            {
                                var mess = await reaction.Channel.SendMessageAsync("Ходить нельзя, Апни лвл!");
#pragma warning disable 4014
                                _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                                return;
                            }

                            if (gameBridge.Character.Name == "mylorik")
                            {
                                var mess = await reaction.Channel.SendMessageAsync("Спартанцы не капитулируют!!");
#pragma warning disable 4014
                                _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                                return;
                            }

                            status.IsBlock = true;
                            status.IsAbleToTurn = false;
                            status.IsReady = true;

                            var mess1 = await reaction.Channel.SendMessageAsync("Принято");
#pragma warning disable 4014
                            _help.DeleteMessOverTime(mess1, 6);
#pragma warning restore 4014
                            break;

                        default:


                            await HandleAttackOrLvlUp(gameBridge, reaction);

                            break;
                    }

                    return;
                }
        }

        public async Task HandleAttackOrLvlUp(GameBridgeClass player, SocketReaction reaction, int botChoice = -1)
        {
            var status = player.Status;
            var account = player.DiscordAccount;

            var emoteNum = !player.IsBot() ? GetNumberFromEmote(reaction) : botChoice;

            if (status.MoveListPage == 3)
            {
                await GetLvlUp(player, emoteNum);
                return;
            }

            if (!status.IsAbleToTurn)
            {
                if (!player.IsBot())
                {
                    var mess = await reaction.Channel.SendMessageAsync("Ходить нельзя, пока идет подсчёт.");
#pragma warning disable 4014
                    _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                }

                return;
            }

            if (status.MoveListPage == 2)
            {
                if (!player.IsBot())
                {
                    var mess = await reaction.Channel.SendMessageAsync(
                        $"Нажми на {new Emoji("📖")}, чтобы вернуться в основное меню.");
#pragma warning disable 4014
                    _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                }


                return;
            }

            if (status.MoveListPage == 1)
            {
                status.WhoToAttackThisTurn =
                    _global.GamesList.Find(x => x.GameId == account.GameId).PlayersList
                        .Find(x => x.Status.PlaceAtLeaderBoard == emoteNum).DiscordAccount.DiscordId;

                if (status.WhoToAttackThisTurn == account.DiscordId)
                {
                    status.WhoToAttackThisTurn = 0;
                    if (!player.IsBot())
                    {
                        var mess = await reaction.Channel.SendMessageAsync("Зачем ты себя бьешь?");
#pragma warning disable 4014
                        _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                    }

                    return;
                }

                status.IsAbleToTurn = false;
                status.IsReady = true;
                status.IsBlock = false;
                if (!player.IsBot())
                {
                    var mess2 = await reaction.Channel.SendMessageAsync("Принято");
#pragma warning disable 4014
                    _help.DeleteMessOverTime(mess2, 6);
#pragma warning restore 4014
                }
            }
        }


        private async Task GetLvlUp(GameBridgeClass player, int skillNumber)
        {
            switch (skillNumber)
            {
                case 1:
                    player.Character.Intelligence++;
                    if (player.Character.Intelligence > 10 && player.Character.Psyche <= 9 &&
                        player.Character.Strength <= 9 && player.Character.Speed <= 9)
                    {
                        player.Character.Intelligence = 10;
                        if (!player.IsBot())
                        {
                            var mess2 =
                                await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                    "10 максимум, выбери другой");
#pragma warning disable 4014
                            _help.DeleteMessOverTime(mess2, 6);
#pragma warning restore 4014
                        }

                        return;
                    }

                    break;
                case 2:
                    player.Character.Strength++;
                    if (player.Character.Strength > 10 && player.Character.Psyche <= 9 &&
                        player.Character.Intelligence <= 9 && player.Character.Speed <= 9)
                    {
                        player.Character.Strength = 10;
                        if (!player.IsBot())
                        {
                            var mess2 =
                                await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                    "10 максимум, выбери другой");
#pragma warning disable 4014
                            _help.DeleteMessOverTime(mess2, 6);
#pragma warning restore 4014
                        }


                        return;
                    }

                    break;
                case 3:
                    player.Character.Speed++;
                    if (player.Character.Speed > 10 && player.Character.Psyche <= 9 &&
                        player.Character.Strength <= 9 && player.Character.Intelligence <= 9)
                    {
                        player.Character.Speed = 10;
                        if (!player.IsBot())
                        {
                            var mess2 =
                                await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                    "10 максимум, выбери другой");
#pragma warning disable 4014
                            _help.DeleteMessOverTime(mess2, 6);
#pragma warning restore 4014
                        }


                        return;
                    }

                    break;
                case 4:
                    player.Character.Psyche++;
                    if (player.Character.Psyche > 10 && player.Character.Intelligence <= 9 &&
                        player.Character.Strength <= 9 && player.Character.Speed <= 9)
                    {
                        player.Character.Psyche = 10;
                        if (!player.IsBot())
                        {
                            var mess2 =
                                await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                    "10 максимум, выбери другой");
#pragma warning disable 4014
                            _help.DeleteMessOverTime(mess2, 6);
#pragma warning restore 4014
                        }


                        return;
                    }

                    break;
                default:
                    return;
            }
            
            //awdka only:
            if (player.Status.LvlUpPoints == 3 || player.Status.LvlUpPoints == 2)
            {
                player.Status.LvlUpPoints--;
            }
            else
            {
                //end awdka only
                player.Status.MoveListPage = 1;
            }

            await _upd.UpdateMessage(player);
        }

        private int GetNumberFromEmote(SocketReaction reaction)
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