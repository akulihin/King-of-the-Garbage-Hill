using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling
{
    public sealed class GameReaction : IServiceSingleton
    {
        private readonly Global _global;
        private readonly HelperFunctions _help;
        private readonly LoginFromConsole _logs;
        private readonly GameUpdateMess _upd;
        private readonly UserAccounts _accounts;

        public GameReaction(UserAccounts accounts,
            Global global,
            GameUpdateMess upd, HelperFunctions help, LoginFromConsole logs)
        {
            _accounts = accounts;
            _global = global;

            _upd = upd;
            _help = help;
            _logs = logs;
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
                        case "confirm-prefict":
                            player.Status.CanSelectAttack = true;
                            var builder = new ComponentBuilder();
                            var embed = new EmbedBuilder();
                            var game = _global.GamesList.Find(x => x.GameId == player.GameId);


                            embed = _upd.FightPage(player);
                            embed.WithFooter($"{_upd.GetTimeLeft(player)} |{embed.Length}|");

                            builder.WithButton(_upd.GetBlockButton(player, game), 0);
                            builder.WithButton(_upd.GetMoralButton(player, game), 0);
                            builder.WithButton(_upd.GetEndGameButton(), 0);
                            builder.WithSelectMenu(_upd.GetAttackMenu(player, game), 1);
                            builder.WithButton(_upd.GetPlaceHolderButton(player, game), 2);
                            builder.WithSelectMenu(_upd.GetPredictMenu(player, game), 3);
                            await button.Message.ModifyAsync(message =>
                            {
                                message.Embed = embed.Build();
                                message.Components = builder.Build();
                            });
                            break;
                            


                        case "end":
                            var dm = await button.User.CreateDMChannelAsync();
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
                                _help.SendMsgAndDeleteItAfterRound(player, "Ходить нельзя, Апни лвл!");
                                return;
                            }

                            if (player.Character.Name == "mylorik")
                            {
                                _help.SendMsgAndDeleteItAfterRound(player, "Спартанцы не капитулируют!!");
                                return;
                            }


                            status.IsBlock = true;
                            status.IsAbleToTurn = false;
                            status.IsReady = true;
                            status.AddInGamePersonalLogs("Ты поставил блок\n");

                            _upd.UpdateMessage(player);
                            break;

                        case "moral":
                            var tempMoral = player.Character.GetMoral();

                            if (player.Character.GetMoral() >= 15)
                            {
                                player.Character.AddMoral(player.Status, -15, skillName:"Обмен Морали: ");
                                player.Character.AddBonusPointsFromMoral(12);
                                _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Я БОГ ЭТОГО МИРА + 12 __бонунсых__ очка");
                            }
                            else if (player.Character.GetMoral() >= 10)
                            {
                                player.Character.AddMoral(player.Status, -10, skillName: "Обмен Морали: ");
                                player.Character.AddBonusPointsFromMoral(8);
                                _help.SendMsgAndDeleteItAfterRound(player, "Мораль: МВП + 6 __бонунсых__ очка");
                            }
                            else if (player.Character.GetMoral() >= 5)
                            {
                                player.Character.AddMoral(player.Status, -5, skillName: "Обмен Морали: ");
                                player.Character.AddBonusPointsFromMoral(2);
                                _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Изи катка + 2 __бонунсых__ очка");
                            }
                            else if (player.Character.GetMoral() >= 3)
                            {
                                player.Character.AddMoral(player.Status, -3, skillName: "Обмен Морали: ");
                                player.Character.AddBonusPointsFromMoral(1);
                                _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Ойвей + 1  __бонунсых__ очка");
                            }
                            else
                            {
                                _help.SendMsgAndDeleteItAfterRound(player,
                                    "У тебя недосточно морали, чтобы поменять ее на бонусные очки.\n" +
                                    "3 морали =  1 бонусное очко\n" +
                                    "5 морали = 2 бонусных очка\n" +
                                    "10 морали = 6 бонусных очков\n" +
                                    "15 морали = 12 бонусных очков");
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
                        
                        case "predict-1":
                            await HandlePredic1(player, button);
                            break;

                        case "predict-2":
                            HandlePredic2(player, button);
                            break;
                    }

                    return;
                }
        }


        public async Task HandlePredic1(GamePlayerBridgeClass player, SocketMessageComponent button)
        {

            var account = _accounts.GetAccount(player.DiscordId);
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var builder = new ComponentBuilder();
            
            var predictMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("predict-2")
                .WithPlaceholder(string.Join("", button.Data.Values) + " это...");

            var allCharacters = account.CharacterChance.Select(character => character.CharacterName).ToList();

            var i = 0;
            predictMenu.AddOption("Предыдущие меню", "prev-page");
            //predictMenu.AddOption("Очистить", "empty");
            foreach (var character in allCharacters)
            {
                i++;
                predictMenu.AddOption(character, string.Join("", button.Data.Values) + "||spb||" + character);
            }


            builder.WithButton(_upd.GetBlockButton(player, game), 0);
            builder.WithButton(_upd.GetMoralButton(player, game), 0);
            builder.WithButton(_upd.GetEndGameButton(), 0);
            builder.WithSelectMenu(_upd.GetAttackMenu(player, game), 1);
            builder.WithButton(_upd.GetPlaceHolderButton(player, game), 2);
            builder.WithSelectMenu(predictMenu, 3);


            await button.Message.ModifyAsync(message =>
            {
                message.Components = builder.Build();
            });

        }

        public async Task HandlePredic2(GamePlayerBridgeClass player, SocketMessageComponent button)
        {

            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var builder = new ComponentBuilder();
            var embed = new EmbedBuilder();


            embed = _upd.FightPage(player);
            embed.WithFooter($"{_upd.GetTimeLeft(player)} |{embed.Length}|");

            builder.WithButton(_upd.GetBlockButton(player, game), 0);
            builder.WithButton(_upd.GetMoralButton(player, game), 0);
            builder.WithButton(_upd.GetEndGameButton(), 0);
            builder.WithSelectMenu(_upd.GetAttackMenu(player, game), 1);
            builder.WithButton(_upd.GetPlaceHolderButton(player, game), 2);
            builder.WithSelectMenu(_upd.GetPredictMenu(player, game), 3);

            var splitted = string.Join("", button.Data.Values).Split("||spb||");

            if (splitted[0] == "prev-page")
            {
                await button.Message.ModifyAsync(message =>
                {
                    message.Embed = embed.Build();
                    message.Components = builder.Build();
                });
                return;
            }

            var predictedPlayerUsername = splitted[0];
            var predictedCharacterName = splitted[1];
            var predictedPlayerId = game.PlayersList.Find(x => x.DiscordUsername == predictedPlayerUsername).Status.PlayerId;

            var predicted = player.Predict.Find(x => x.PlayerId == predictedPlayerId);

            if (predicted == null)
            {
                player.Predict.Add(new PredictClass(predictedCharacterName, predictedPlayerId));
            }
            else
            {
                predicted.CharacterName = predictedCharacterName;
            }


            embed = _upd.FightPage(player);
            await button.Message.ModifyAsync(message =>
            {
                message.Embed = embed.Build();
                message.Components = builder.Build();
            });
        }


        public async Task HandleLvlUp(GamePlayerBridgeClass player, SocketMessageComponent button, int botChoice = -1)
        {
            var emoteNum = !player.IsBot() ? Convert.ToInt32(string.Join("", button.Data.Values)) : botChoice;
            await GetLvlUp(player, emoteNum);
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
                _help.SendMsgAndDeleteItAfterRound(player,
                    player.Status.IsSkip
                        ? "Что-то заставило тебя пропустить этот ход..."
                        : "Ходить нельзя, пока идет подсчёт.");

                return true;
            }

            /*
            if (status.MoveListPage == 2)
            {
                SendMsgAndDeleteItAfterRound(player, $"Нажми на {new Emoji("📖")}, чтобы вернуться в основное меню.");
                return true;
            }
            */

            if (status.MoveListPage == 1)
            {
                var game = _global.GamesList.Find(x => x.GameId == player.GameId);
                var whoToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == emoteNum);

                if (whoToAttack == null)
                {
                    return false;
                }

                status.WhoToAttackThisTurn = whoToAttack.Status.PlayerId;

                if (game.PlayersList.Any(x => x.Character.Name == "Тигр" && x.Status.PlaceAtLeaderBoard == emoteNum) &&
                    game.RoundNo == 10)
                {
                    status.WhoToAttackThisTurn = Guid.Empty;
                    _help.SendMsgAndDeleteItAfterRound(player, "Выбранный игрок недоступен в связи с баном за нарушение правил");
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
                    await game.Phrases.VampyrNoAttack.SendLogSeparate(player, false);
                    return false;
                }


                if (status.WhoToAttackThisTurn == status.PlayerId)
                {
                    status.WhoToAttackThisTurn = Guid.Empty;
                    _help.SendMsgAndDeleteItAfterRound(player, "Зачем ты себя бьешь?");
                    return false;
                }

                status.IsAbleToTurn = false;
                status.IsReady = true;
                status.IsBlock = false;
                player.Status.AddInGamePersonalLogs(
                    $"Ты напал на игрока {whoToAttack.DiscordUsername}\n");
                return true;
            }

            return false;
        }

        //for GetLvlUp ONLY!
#pragma warning disable 1998
        public async Task LvlUp10(GamePlayerBridgeClass player)
#pragma warning restore 1998
        {
            _help.SendMsgAndDeleteItAfterRound(player, "10 максимум, выбери другой стат"); //not awaited 
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

            //Я пытаюсь!
            if (player.Status.LvlUpPoints > 1)
            {
                player.Status.LvlUpPoints--;
                try
                {
                    if (!player.IsBot())
                    {
                        _help.SendMsgAndDeleteItAfterRound(player, $"Осталось еще {player.Status.LvlUpPoints} очков характеристик. Пытайся!");
                    }
                }
                catch (Exception e)
                {
                    _logs.Critical(e.StackTrace);
                }
            }
            else
            {
                player.Status.MoveListPage = 1;
            }
            //end Я пытаюсь!



            //Дизмораль
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            if (player.Character.Name == "Darksci")
            {

                if (game.RoundNo == 9)
                {
                    //Дизмораль Part #2
                    player.Character.AddPsyche(player.Status, -4, "Дизмораль: ");
                    //end Дизмораль Part #2
                }

                //Да всё нахуй эту игру: Part #2
                if (game.RoundNo == 9 || game.RoundNo == 7 || game.RoundNo == 5 || game.RoundNo == 3)
                    if (player.Character.GetPsyche() <= 0)
                    {
                        player.Status.IsSkip = true;
                        player.Status.IsBlock = false;
                        player.Status.IsAbleToTurn = false;
                        player.Status.IsReady = true;
                        player.Status.WhoToAttackThisTurn = Guid.Empty;
                        game.Phrases.DarksciFuckThisGame.SendLog(player, true);
                    }
                //end Да всё нахуй эту игру: Part #2
            }
            //end Дизмораль

            _upd.UpdateMessage(player);
            await Task.CompletedTask;
        }
    }
}