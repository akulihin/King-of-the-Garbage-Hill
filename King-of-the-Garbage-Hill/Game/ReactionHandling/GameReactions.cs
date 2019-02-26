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

        public async Task ReactionAddedGameWindow(Cacheable<IUserMessage, ulong> cash,
            ISocketMessageChannel channel, SocketReaction reaction)
        {
            for (var i = 0; i < _global.GamesList.Count; i++)
            {
                if (!_global.GamesList[i].PlayersList
                    .Any(x => x.DiscordAccount.DiscordId == reaction.UserId &&
                              x.Status.SocketMessageFromBot.Id == reaction.MessageId))
                    continue;


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
                            await _help.DeleteMessOverTime(mess, 6);
                            return;
                        }

                        if (gameBridge.Character.Name == "mylorik")
                        {
                            var mess = await reaction.Channel.SendMessageAsync("Спартанцы не капитулируют!!");
                            await _help.DeleteMessOverTime(mess, 6);
                            return;
                        }

                        status.IsBlock = true;
                        status.IsAbleToTurn = false;
                        status.IsReady = true;
                        var mess1 = await reaction.Channel.SendMessageAsync("Принято");
                        await _help.DeleteMessOverTime(mess1, 6);
                        break;

                    default:
                        if (!status.IsAbleToTurn)
                        {
                            var mess = await reaction.Channel.SendMessageAsync("Ходить нельзя, пока идет подсчёт.");
                            await _help.DeleteMessOverTime(mess, 6);
                            return;
                        }


                        var emoteNum = GetNumberFromEmote(reaction);

                        if (status.MoveListPage == 3)
                        {
                            await GetLvlUp(gameBridge, emoteNum);
                            break;
                        }

                        if (status.MoveListPage == 2)
                        {
                            var mess = await reaction.Channel.SendMessageAsync(
                                $"Нажми на {new Emoji("📖")}, чтобы вернуться в основное меню.");
                            await _help.DeleteMessOverTime(mess, 6);
                            break;
                        }

                        if (status.MoveListPage == 1)
                        {
                            status.WhoToAttackThisTurn =
                                _global.GamesList.Find(x => x.GameId == account.GameId).PlayersList
                                    .Find(x => x.Status.PlaceAtLeaderBoard == emoteNum).DiscordAccount.DiscordId;

                            if (status.WhoToAttackThisTurn == account.DiscordId)
                            {
                                status.WhoToAttackThisTurn = 0;
                                var mess = await reaction.Channel.SendMessageAsync("Зачем ты себя бьешь?");
                                await _help.DeleteMessOverTime(mess, 6);
                                return;
                            }

                            status.IsAbleToTurn = false;
                            status.IsReady = true;
                            status.IsBlock = false;

                            var mess2 = await reaction.Channel.SendMessageAsync("Принято");
                            await _help.DeleteMessOverTime(mess2, 6);
                        }

                        break;
                }
            }
        }


        public async Task GetLvlUp(GameBridgeClass gameBridge, int skillNumber)
        {
            //TODO:
            switch (skillNumber)
            {
                case 1:
                    gameBridge.Character.Intelligence++;
                    if (gameBridge.Character.Intelligence > 10 && gameBridge.Character.Psyche <= 9 && gameBridge.Character.Strength <= 9 && gameBridge.Character.Speed <= 9 )
                    {
                        gameBridge.Character.Intelligence = 10;
                        var mess2 =
                            await gameBridge.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                "10 максимум, выбери другой");
                        await _help.DeleteMessOverTime(mess2, 6);
                        return;
                    }

                    break;
                case 2:
                    gameBridge.Character.Strength++;
                    if (gameBridge.Character.Strength > 10 && gameBridge.Character.Psyche <= 9 && gameBridge.Character.Intelligence <= 9 && gameBridge.Character.Speed <= 9 )
                    {
                        gameBridge.Character.Strength = 10;
                        var mess2 =
                            await gameBridge.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                "10 максимум, выбери другой");
                        await _help.DeleteMessOverTime(mess2, 6);
                        return;
                    }

                    break;
                case 3:
                    gameBridge.Character.Speed++;
                    if (gameBridge.Character.Speed > 10 && gameBridge.Character.Psyche <= 9 && gameBridge.Character.Strength <= 9 && gameBridge.Character.Intelligence <= 9 )
                    {
                        gameBridge.Character.Speed = 10;
                        var mess2 =
                            await gameBridge.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                "10 максимум, выбери другой");
                        await _help.DeleteMessOverTime(mess2, 6);
                        return;
                    }

                    break;
                case 4:
                    gameBridge.Character.Psyche++;
                    if (gameBridge.Character.Psyche > 10 && gameBridge.Character.Intelligence <= 9 && gameBridge.Character.Strength <= 9 && gameBridge.Character.Speed <= 9 )
                    {
                        gameBridge.Character.Psyche = 10;
                        var mess2 =
                            await gameBridge.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                                "10 максимум, выбери другой");
                        await _help.DeleteMessOverTime(mess2, 6);
                        return;
                    }

                    break;
                default:
                    return;
            }

            gameBridge.Status.MoveListPage = 1;
            await _upd.UpdateMessage(gameBridge);
        }

        public int GetNumberFromEmote(SocketReaction reaction)
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