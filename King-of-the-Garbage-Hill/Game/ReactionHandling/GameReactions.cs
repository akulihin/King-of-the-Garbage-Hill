using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public sealed class GameReaction : IServiceSingleton
    {
        private readonly UserAccounts _accounts;
    //    private readonly InGameGlobal _gameGlobal;
        private readonly Global _global;
        private readonly HelperFunctions _help;
        private readonly CharactersUniquePhrase _phrase;
        private readonly GameUpdateMess _upd;

        public GameReaction(UserAccounts accounts,
            Global global,
            GameUpdateMess upd, HelperFunctions help, CharactersUniquePhrase phrase)
        {
            _accounts = accounts;

            _global = global;

            _upd = upd;
            _help = help;
            _phrase = phrase;
          //  _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task ReactionAddedGameWindow(Cacheable<IUserMessage, ulong> cash,
            ISocketMessageChannel channel, SocketReaction reaction)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            foreach (var t in _global.GamesList)
                if (t.PlayersList.Any(x =>
                    x.DiscordAccount.DiscordId == reaction.UserId &&
                    x.Status.SocketMessageFromBot.Id == reaction.MessageId))
                {
                    var account = _accounts.GetAccount(reaction.UserId);
                    var player = _global.GetGameAccount(reaction.UserId, account.GameId);
                    var status = player.Status;

                    // if (!discordAccount.IsAbleToTurn){return;}

                    switch (reaction.Emote.Name)
                    {
                        case "❌":
                            await _upd.EndGame(reaction, reaction.Message.Value);
                            break;

                        case "📖":

                            if (player.Status.MoveListPage == 1)
                                player.Status.MoveListPage = 2;
                            else if (player.Status.MoveListPage == 2) player.Status.MoveListPage = 1;

                            await _upd.UpdateMessage(player);
                            break;


                        case "🛡" when status.IsAbleToTurn:
                            if (status.MoveListPage == 3)
                            {
                                SendMsgAndDeleteIt(player, "Ходить нельзя, Апни лвл!");
                                return;
                            }

                            if (player.Character.Name == "mylorik")
                            {
                                SendMsgAndDeleteIt(player, "Спартанцы не капитулируют!!");
                                return;
                            }


                            status.IsBlock = true;
                            status.IsAbleToTurn = false;
                            status.IsReady = true;
                            status.AddInGamePersonalLogs("Ты поставил блок\n");
                            SendMsgAndDeleteIt(player);
                            break;

                        default:


                            await HandleAttackOrLvlUp(player, reaction);

                            break;
                    }

                    return;
                }
        }

        public async Task HandleAttackOrLvlUp(GamePlayerBridgeClass player, SocketReaction reaction, int botChoice = -1)
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
                SendMsgAndDeleteIt(player,
                    player.Status.IsSkip
                        ? "Что-то заставило тебя пропустить этот ход..."
                        : "Ходить нельзя, пока идет подсчёт.");

                return;
            }

            if (status.MoveListPage == 2)
            {
                SendMsgAndDeleteIt(player, $"Нажми на {new Emoji("📖")}, чтобы вернуться в основное меню.");
                return;
            }

            if (status.MoveListPage == 1)
            {
                var game = _global.GamesList.Find(x => x.GameId == account.GameId);
                var whoToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == emoteNum);

                if (whoToAttack == null) return;

                status.WhoToAttackThisTurn = whoToAttack.DiscordAccount.DiscordId;

                if (game.PlayersList.Any(x => x.Character.Name == "Тигр" && x.Status.PlaceAtLeaderBoard == emoteNum) &&
                    game.RoundNo == 10)
                {
                    status.WhoToAttackThisTurn = 0;
                    if (!player.IsBot())
                    {
                        var mess = await reaction.Channel.SendMessageAsync(
                            "На этого игрока нельзя нападать, почему-то...");
#pragma warning disable 4014
                        _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                    }

                    return;
                }
                /*
                if (game.PlayersList.Any(x => x.Character.Name == "Бог ЛоЛа") &&
                    _gameGlobal.LolGodUdyrList.Any(
                        x =>
                            x.GameId == game.GameId && 
                            x.EnemyDiscordId == player.DiscordAccount.DiscordId) && whoToAttack.Character.Name == "Бог ЛоЛа")
                {
                    status.WhoToAttackThisTurn = 0;
                    if (!player.IsBot())
                    {
                        var mess = await reaction.Channel.SendMessageAsync(
                            "На этого игрока нельзя нападать, почему-то...");
#pragma warning disable 4014
                        _help.DeleteMessOverTime(mess, 6);
#pragma warning restore 4014
                    }

                    return;
                }
                */

                if (player.Character.Name == "Вампур" && player.Status.WhoToLostEveryRound.Any(x =>
                        x.RoundNo == game.RoundNo - 1 && x.EnemyId == status.WhoToAttackThisTurn))
                {
                    status.WhoToAttackThisTurn = 0;
                    await _phrase.VampyrNoAttack.SendLog(player);
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
                player.Status.AddInGamePersonalLogs(
                    $"Ты напал на игрока {whoToAttack.DiscordAccount.DiscordUserName}\n");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                SendMsgAndDeleteIt(player); //not awaited 
            }
        }

        //for GetLvlUp ONLY!
        public void LvlUp10(GamePlayerBridgeClass player)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            SendMsgAndDeleteIt(player, "10 максимум, выбери другой стат"); //not awaited 
        }


        public async Task SendMsgAndDeleteIt(GamePlayerBridgeClass player, string msg = "Принято", int seconds = 6)
        {
            if (!player.IsBot())
            {
                var mess2 =
                    await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(msg);
                _help.DeleteMessOverTime(mess2, seconds);
            }
        }

        private async Task GetLvlUp(GamePlayerBridgeClass player, int skillNumber)
        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            switch (skillNumber)
            {
                case 1:

                    if (player.Character.GetIntelligence() >= 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        LvlUp10(player);
                        return;
                    }

                    player.Character.AddIntelligence(player.Status, 1, false);
                    player.Status.AddInGamePersonalLogs(
                        $"Ты улучшил интеллект до {player.Character.GetIntelligence()}\n");
                    break;
                case 2:

                    if (player.Character.GetStrength() >= 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetIntelligence() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        LvlUp10(player);
                        return;
                    }

                    player.Character.AddStrength(player.Status, 1, false);
                    player.Status.AddInGamePersonalLogs($"Ты улучшил силу до {player.Character.GetStrength()}\n");

                    break;
                case 3:

                    if (player.Character.GetSpeed() >= 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetIntelligence() <= 9)
                    {
                        LvlUp10(player);
                        return;
                    }

                    player.Character.AddSpeed(player.Status, 1, false);
                    player.Status.AddInGamePersonalLogs($"Ты улучшил скорость до {player.Character.GetSpeed()}\n");

                    break;
                case 4:

                    if (player.Character.GetPsyche() >= 10 && player.Character.GetIntelligence() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        LvlUp10(player);
                        return;
                    }

                    player.Character.AddPsyche(player.Status, 1, false);
                    player.Status.AddInGamePersonalLogs($"Ты улучшил психику до {player.Character.GetPsyche()}\n");

                    break;
                default:
                    return;
            }

            //awdka only:
            if (player.Status.LvlUpPoints == 3 || player.Status.LvlUpPoints == 2)
                player.Status.LvlUpPoints--;
            else
                player.Status.MoveListPage = 1;

            _upd.UpdateMessage(player);
            await Task.CompletedTask;
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