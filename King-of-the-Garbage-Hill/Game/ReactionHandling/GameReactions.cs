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

            if (botChoice == -10)
            {
                status.IsBlock = true;
                status.IsAbleToTurn = false;
                status.IsReady = true;
                return;
            }

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
                var game = _global.GamesList.Find(x => x.GameId == account.GameId);
                var whoToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == emoteNum);
                status.WhoToAttackThisTurn = whoToAttack.DiscordAccount.DiscordId;

                if (game.PlayersList.Any(x => x.Character.Name == "Тигр" && x.Status.PlaceAtLeaderBoard == emoteNum) && game.RoundNo == 10)
                {
                    status.WhoToAttackThisTurn = 0;
                    if (!player.IsBot())
                    {
                        var mess = await reaction.Channel.SendMessageAsync("На этого игрока нельзя нападать, почему-то...");
#pragma warning disable 4014
                        _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                    }
                    return;
                }


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
                player.Status.AddInGamePersonalLogs($"Ты напал на игрока {whoToAttack.DiscordAccount.DiscordUserName}");
                if (!player.IsBot())
                {
                    var mess2 = await reaction.Channel.SendMessageAsync("Принято");
#pragma warning disable 4014
                    _help.DeleteMessOverTime(mess2, 6);
#pragma warning restore 4014
                }
            }
        }

        //for GetLvlUp ONLY!
        public async Task LvlUp10(GameBridgeClass player)
        {
            if (!player.IsBot())
            {
                var mess2 =
                    await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(
                        "10 максимум, выбери другой стат");
#pragma warning disable 4014
                _help.DeleteMessOverTime(mess2, 6);
#pragma warning restore 4014
            }
        }

        private async Task GetLvlUp(GameBridgeClass player, int skillNumber)
        {
            switch (skillNumber)
            {
                case 1:
                   
                    if (player.Character.GetIntelligence() > 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        await LvlUp10(player);
                        return;
                    }
                    player.Character.AddIntelligence();
                    player.Status.AddInGamePersonalLogs($"Ты улучшил интеллект до {player.Character.GetIntelligence()}");
                    break;
                case 2:

                    if (player.Character.GetStrength() >= 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetIntelligence() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        await LvlUp10(player);
                        return;
                    }

                    player.Character.AddStrength();
                    player.Status.AddInGamePersonalLogs($"Ты улучшил силу до {player.Character.GetStrength()}");

                    break;
                case 3:

                    if (player.Character.GetSpeed() > 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetIntelligence() <= 9)
                    {
                        await LvlUp10(player);
                        return;
                    }

                    player.Character.AddSpeed();
                    player.Status.AddInGamePersonalLogs($"Ты улучшил скорость до {player.Character.GetSpeed()}");

                    break;
                case 4:

                    if (player.Character.GetPsyche() > 10 && player.Character.GetIntelligence() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        await LvlUp10(player);
                        return;
                    }

                    player.Character.AddPsyche();
                    player.Status.AddInGamePersonalLogs( $"Ты улучшил психику до {player.Character.GetPsyche()}");

                    break;
                default:
                    return;
            }

            //awdka only:
            if (player.Status.LvlUpPoints == 3 || player.Status.LvlUpPoints == 2)
                player.Status.LvlUpPoints--;
            else
                player.Status.MoveListPage = 1;

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