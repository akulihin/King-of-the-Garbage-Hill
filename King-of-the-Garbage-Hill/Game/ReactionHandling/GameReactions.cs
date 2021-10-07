using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public sealed class GameReaction : IServiceSingleton
    {
        private readonly UserAccounts _accounts;
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

        //    private readonly InGameGlobal _gameGlobal;
        public async Task ReactionAddedGameWindow(SocketMessageComponent button)
        {
            
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            foreach (var t in _global.GamesList)
                if (t.PlayersList.Any(x =>
                    x.DiscordId == button.User.Id &&
                    x.Status.SocketMessageFromBot.Id == button.Message.Id))
                {
                    var player = _global.GetGameAccount(button.User.Id, t.PlayersList.FirstOrDefault().GameId);
                    var status = player.Status;

                    // if (!discordAccount.IsAbleToTurn){return;}

                    switch (button.Data.CustomId)
                    {
                        case "end":
                            await _upd.EndGame(button);
                            break;

                        case "stats":

                            if (player.Status.MoveListPage == 1)
                                player.Status.MoveListPage = 2;
                            else if (player.Status.MoveListPage == 2) player.Status.MoveListPage = 1;

                            await _upd.UpdateMessage(player);
                            break;


                        case "block" when status.IsAbleToTurn:
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

                            _upd.UpdateMessage(player);
                            break;

                        case "moral":
                            var tempMoral = player.Character.GetMoral();

                            if (player.Character.GetMoral() >= 15)
                            {
                                player.Character.AddMoral(player.Status, -15, skillName:"Обмен Морали: ");
                                player.Character.SetBonusPointsFromMoral(15);
                                SendMsgAndDeleteIt(player, "Мораль: Я БОГ ЭТОГО МИРА + 15 __бонунсых__ очка");
                            }
                            else if (player.Character.GetMoral() >= 10)
                            {
                                player.Character.AddMoral(player.Status, -10, skillName: "Обмен Морали: ");
                                player.Character.SetBonusPointsFromMoral(8);
                                SendMsgAndDeleteIt(player, "Мораль: МВП + 8 __бонунсых__ очка");
                            }
                            else if (player.Character.GetMoral() >= 5)
                            {
                                player.Character.AddMoral(player.Status, -5, skillName: "Обмен Морали: ");
                                player.Character.SetBonusPointsFromMoral(2);
                                SendMsgAndDeleteIt(player, "Мораль: Изи катка + 2 __бонунсых__ очка");
                            }
                            else if (player.Character.GetMoral() >= 3)
                            {
                                player.Character.AddMoral(player.Status, -3, skillName: "Обмен Морали: ");
                                player.Character.SetBonusPointsFromMoral(1);
                                SendMsgAndDeleteIt(player, "Мораль: Ойвей + 1  __бонунсых__ очка");
                            }
                            else
                            {
                                SendMsgAndDeleteIt(player,
                                    "У тебя недосточно морали, чтобы поменять ее на бонусные очки.\n" +
                                    "3 морали =  1 бонусное очко\n" +
                                    "5 морали = 2 бонусных очка\n" +
                                    "10 морали = 8 бонусных очков\n" +
                                    "15 морали = 15 бонусных очков");
                            }

                            if (tempMoral >= 3)
                            {
                                _upd.UpdateMessage(t.PlayersList.Find(x => x.DiscordId == player.DiscordId));
                            }

                            break;

                        case "char-select":
                            await HandleLvlUp(player, button);
                            _upd.UpdateMessage(player);
                            break;
                        case "attack-select":
                            await HandleAttack(player, button);
                            _upd.UpdateMessage(player);
                            break;
                    }

                    return;
                }
        }

        public async Task HandleLvlUp(GamePlayerBridgeClass player, SocketMessageComponent button,
    int botChoice = -1)
        {
            var status = player.Status;

            var emoteNum = !player.IsBot() ? Convert.ToInt32(string.Join("", button.Data.Values)) : botChoice;
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);


            await GetLvlUp(player, emoteNum);

            if (player.Character.Name == "Darksci")
            {
                if (game.RoundNo == 9)
                {
                    //Дизмораль
                    player.Character.AddPsyche(player.Status, -4,  "Дизмораль: ");
                    _phrase.DarksciDysmoral.SendLog(player);
                    game.AddPreviousGameLogs(
                        $"**{player.DiscordUsername}:** Всё, у меня горит!");
                    //end Дизмораль
                }

                //Да всё нахуй эту игру:
                if (player.Character.GetPsyche() <= 0)
                {
                    player.Status.IsSkip = true;
                    player.Status.IsBlock = false;
                    player.Status.IsAbleToTurn = false;
                    player.Status.IsReady = true;
                    player.Status.WhoToAttackThisTurn = Guid.Empty;
                    _phrase.DarksciFuckThisGame.SendLog(player);

                    if (game.RoundNo == 9 ||
                        game.RoundNo == 10 && !game.GetAllGameLogs().Contains("Нахуй эту игру"))
                        game.AddPreviousGameLogs(
                            $"**{player.DiscordUsername}:** Нахуй эту игру..");
                }
                //end Да всё нахуй эту игру:
            }


        }

        public async Task<bool> HandleAttack(GamePlayerBridgeClass player, SocketMessageComponent button,
            int botChoice = -1)
        {
            var status = player.Status;

            var emoteNum = !player.IsBot() ? Convert.ToInt32(string.Join("", button.Data.Values)) : botChoice;

            if (botChoice == -10)
            {
                status.IsBlock = true;
                status.IsAbleToTurn = false;
                status.IsReady = true;
                return true;
            }


            if (!status.IsAbleToTurn)
            {
                SendMsgAndDeleteIt(player,
                    player.Status.IsSkip
                        ? "Что-то заставило тебя пропустить этот ход..."
                        : "Ходить нельзя, пока идет подсчёт.");

                return true;
            }

            /*
            if (status.MoveListPage == 2)
            {
                SendMsgAndDeleteIt(player, $"Нажми на {new Emoji("📖")}, чтобы вернуться в основное меню.");
                return true;
            }
            */

            if (status.MoveListPage == 1)
            {
                var game = _global.GamesList.Find(x => x.GameId == player.GameId);
                var whoToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == emoteNum);

                if (whoToAttack == null) return false;

                status.WhoToAttackThisTurn = whoToAttack.Status.PlayerId;

                if (game.PlayersList.Any(x => x.Character.Name == "Тигр" && x.Status.PlaceAtLeaderBoard == emoteNum) &&
                    game.RoundNo == 10)
                {
                    status.WhoToAttackThisTurn = Guid.Empty;
                    SendMsgAndDeleteIt(player, "Выбранный игрок недоступен в связи с баном за нарушение правил");
                    return false;
                }
                /*
                if (game.PlayersList.Any(x => x.Character.Name == "Бог ЛоЛа") &&
                    _gameGlobal.LolGodUdyrList.Any(
                        x =>
                            x.GameId == game.GameId && 
                            x.EnemyDiscordId == player.Status.PlayerId) && whoToAttack.Character.Name == "Бог ЛоЛа")
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
                    status.WhoToAttackThisTurn = Guid.Empty;
                    await _phrase.VampyrNoAttack.SendLogSeparate(player);
                    return false;
                }


                if (status.WhoToAttackThisTurn == status.PlayerId)
                {
                    status.WhoToAttackThisTurn = Guid.Empty;
                    SendMsgAndDeleteIt(player, "Зачем ты себя бьешь?");
                    return false;
                }

                status.IsAbleToTurn = false;
                status.IsReady = true;
                status.IsBlock = false;
                player.Status.AddInGamePersonalLogs(
                    $"Ты напал на игрока {whoToAttack.DiscordUsername}\n");
                SendMsgAndDeleteIt(player); //not awaited 
                return true;
            }

            return false;
        }

        //for GetLvlUp ONLY!
#pragma warning disable 1998
        public async Task LvlUp10(GamePlayerBridgeClass player)
#pragma warning restore 1998
        {
            SendMsgAndDeleteIt(player, "10 максимум, выбери другой стат"); //not awaited 
        }


        public async Task SendMsgAndDeleteIt(GamePlayerBridgeClass player, string msg = "Принято", int seconds = 7)
        {
            if (!player.IsBot())
            {
                var mess2 = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync(msg);
                _help.DeleteMessOverTime(mess2);
            }
        }

        private async Task GetLvlUp(GamePlayerBridgeClass player, int skillNumber)
        {
            switch (skillNumber)
            {
                case 1:

                    if (player.Character.GetIntelligence() >= 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        LvlUp10(player);
                        return;
                    }

                    player.Character.AddIntelligence(player.Status, 1,  "Прокачка: ", false);
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

                    player.Character.AddStrength(player.Status, 1,  "Прокачка: ", false);
                    player.Status.AddInGamePersonalLogs($"Ты улучшил силу до {player.Character.GetStrength()}\n");

                    break;
                case 3:

                    if (player.Character.GetSpeed() >= 10 && player.Character.GetPsyche() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetIntelligence() <= 9)
                    {
                        LvlUp10(player);
                        return;
                    }

                    player.Character.AddSpeed(player.Status, 1,  "Прокачка: ", false);
                    player.Status.AddInGamePersonalLogs($"Ты улучшил скорость до {player.Character.GetSpeed()}\n");

                    break;
                case 4:

                    if (player.Character.GetPsyche() >= 10 && player.Character.GetIntelligence() <= 9 &&
                        player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                    {
                        LvlUp10(player);
                        return;
                    }

                    player.Character.AddPsyche(player.Status, 1,  "Прокачка: ", false);
                    player.Status.AddInGamePersonalLogs($"Ты улучшил психику до {player.Character.GetPsyche()}\n");

                    break;
                default:
                    return;
            }

            //awdka only:
            if (player.Status.LvlUpPoints > 1)
            {
                player.Status.LvlUpPoints--;
                var mess2 = await player.Status.SocketMessageFromBot.Channel.SendMessageAsync($"Осталось еще {player.Status.LvlUpPoints} очков характеристик. Пытайся!");
                _help.DeleteMessOverTime(mess2);
            }
            else
            {
                player.Status.MoveListPage = 1; 

            }
            //end awdka

            _upd.UpdateMessage(player);
            await Task.CompletedTask;
        }

        private int GetNumberFromButtonId(string buttonId)
        {
            switch (buttonId)
            {
                case "attack-one":
                {
                    return 1;
                }

                case "attack-two":
                {
                    return 2;
                }

                case "attack-three":
                {
                    return 3;
                }

                case "attack-four":
                {
                    return 4;
                }

                case "attack-five":
                {
                    return 5;
                }

                case "attack-six":
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