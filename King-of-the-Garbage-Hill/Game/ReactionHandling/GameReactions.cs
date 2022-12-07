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

namespace King_of_the_Garbage_Hill.Game.ReactionHandling;

public sealed class GameReaction : IServiceSingleton
{
    private readonly UserAccounts _accounts;
    private readonly Global _global;
    private readonly HelperFunctions _help;
    private readonly LoginFromConsole _logs;
    private readonly GameUpdateMess _upd;

    public GameReaction(UserAccounts accounts, Global global, GameUpdateMess upd, HelperFunctions help, LoginFromConsole logs)
    {
        _accounts = accounts;
        _global = global;
        _upd = upd;
        _help = help;
        _logs = logs;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    //    private readonly InGameGlobal _gameGlobal;

    public async Task HandleMoralForSkill(GamePlayerBridgeClass player)
    {
        var extraText = "";

        if (player.GameCharacter.GetMoral() >= 20)
        {
            player.GameCharacter.AddMoral(-20, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(100, "Обмен Морали");
            extraText = "Мораль: Я БОГ ЭТОГО МИРА!!! +100 *Скилла*";
        }
        else if (player.GameCharacter.GetMoral() >= 13)
        {
            player.GameCharacter.AddMoral(-13, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(50, "Обмен Морали");
            extraText = "Мораль: MVP! +50 *Скилла*";
        }
        else if (!player.IsBot() && player.GameCharacter.GetMoral() >= 7 && player.GameCharacter.Passive.Any(x => x.PassiveName == "Еврей"))
        {
            player.GameCharacter.AddMoral(-7, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(40, "Обмен Морали");
            extraText = "Мораль: 7:40! Время танцевать! +40 *Скилла*";
        }
        else if (player.GameCharacter.GetMoral() >= 8)
        {
            player.GameCharacter.AddMoral(-8, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(30, "Обмен Морали");
            extraText = "Мораль: Я художник! +30 *Скилла*";
        }
        else if (player.GameCharacter.GetMoral() >= 5)
        {
            player.GameCharacter.AddMoral(-5, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(18, "Обмен Морали");
            extraText = "Мораль: Изи катка +18 *Скилла*";
        }
        else if (player.GameCharacter.GetMoral() >= 3)
        {
            player.GameCharacter.AddMoral(-3, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(10, "Обмен Морали");
            extraText = "Мораль: Набрался *Скилла*, так сказать. +10 *Скилла*";
        }
        else if (player.GameCharacter.GetMoral() >= 2)
        {
            player.GameCharacter.AddMoral(-2, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(6, "Обмен Морали");
            extraText = "Мораль: Так вот как в это играть. +6 *Скилла*";
        }
        else if (player.GameCharacter.GetMoral() >= 1)
        {
            player.GameCharacter.AddMoral(-1, "Обмен Морали", true, true);
            player.GameCharacter.AddExtraSkill(2, "Обмен Морали");
            extraText = "Мораль: Это что? +2 *Скилла*";
        }

        if (extraText.Length > 1)
            await _upd.UpdateMessage(player, extraText);
    }

    public async Task HandleMoralForScore(GamePlayerBridgeClass player)
    {
        var extraText = "";

        if (player.GameCharacter.GetMoral() >= 20)
        {
            player.GameCharacter.AddMoral(-20, "Обмен Морали", true, true);
            player.GameCharacter.AddBonusPointsFromMoral(10);
            extraText = "Мораль: Я БОГ ЭТОГО МИРА +10 __бонунсых__ очков";
        }
        else if (player.GameCharacter.GetMoral() >= 13)
        {
            player.GameCharacter.AddMoral(-13, "Обмен Морали", true, true);
            player.GameCharacter.AddBonusPointsFromMoral(5);
            extraText = "Мораль: МВП +5 __бонунсых__ очков";
        }
        else if (player.GameCharacter.GetMoral() >= 8)
        {
            player.GameCharacter.AddMoral(-8, "Обмен Морали", true, true);
            player.GameCharacter.AddBonusPointsFromMoral(2);
            extraText = "Мораль: Я богач! +2 __бонунсых__ очков";
        }
        else if (player.GameCharacter.GetMoral() >= 5)
        {
            player.GameCharacter.AddMoral(-5, "Обмен Морали", true, true);
            player.GameCharacter.AddBonusPointsFromMoral(1);
            extraText = "Мораль: Ойвей +1 __бонунсых__ очка";
        }

        if (extraText.Length > 1)
            await _upd.UpdateMessage(player, extraText);
    }

    public async Task ReactionAddedGameWindow(SocketMessageComponent button)
    {
        foreach (var t in _global.GamesList)
            if (t.PlayersList.Any(x =>
                    x.DiscordId == button.User.Id &&
                    x.DiscordStatus.SocketMessageFromBot.Id == button.Message.Id))
            {
                var player = _global.GetGameAccount(button.User.Id, t.PlayersList.FirstOrDefault()!.GameId);
                
                var now = DateTimeOffset.UtcNow;
                if ((now - player.Status.LastButtonPress).TotalMilliseconds < 700)
                {
                    await button.RespondAsync("Ошибка: Слишком быстро! Нажми на кнопку еще раз.", ephemeral:true);
                    return;
                }
                player.Status.LastButtonPress = DateTimeOffset.UtcNow;

                var status = player.Status;
                var game = _global.GamesList.Find(x => x.GameId == player.GameId);
                var extraText = "";

                switch (button.Data.CustomId)
                {
                    case "auto-move":
                        player.Status.AutoMoveTimes++;
                        switch (player.Status.AutoMoveTimes)
                        {
                            case 1:
                                await game!.Phrases.AutoMove1.SendLogSeparate(player, false);
                                break;
                            case 2:
                                await game!.Phrases.AutoMove2.SendLogSeparate(player, false);
                                break;
                            case 3:
                                await game!.Phrases.AutoMove3.SendLogSeparate(player, false);
                                break;
                            case 4:
                                await game!.Phrases.AutoMove4.SendLogSeparate(player, false);
                                break;
                            case 5:
                                await game!.Phrases.AutoMove5.SendLogSeparate(player, false);
                                break;
                            case 6:
                                await game!.Phrases.AutoMove6.SendLogSeparate(player, false);
                                break;
                            case 7:
                                await game!.Phrases.AutoMove7.SendLogSeparate(player, false);
                                break;
                            case 8:
                                await game!.Phrases.AutoMove8.SendLogSeparate(player, false);
                                break;
                            case 9:
                                await game!.Phrases.AutoMove9.SendLogSeparate(player, false);
                                break;
                            case 10:
                                await game!.Phrases.AutoMove10.SendLogSeparate(player, false);
                                break;
                        }
                        
                        var textAutomove = $"Ты использовал Авто Ход\n";
                        player.Status.AddInGamePersonalLogs(textAutomove);
                        player.Status.ChangeMindWhat = textAutomove;

                        player.Status.IsAutoMove = true;
                        player.Status.IsReady = true;

                        
                        if (game.IsSolo())
                            break;

                        var embed = _upd.FightPage(player);
                        var components = await _upd.GetGameButtons(player, game);
                        await _help.ModifyGameMessage(player, embed, components);

                        break;
                    case "change-mind":
                        if (player.Status.IsSkip || !player.Status.IsReady)
                        {
                            break;
                        }
                        player.Status.IsAbleToChangeMind = false;
                        player.Status.IsAutoMove = false;

                        player.Status.IsReady = false;
                        player.Status.IsBlock = false;
                        player.Status.WhoToAttackThisTurn = new List<Guid>();

                        if (status.ChangeMindWhat.Contains("Ты использовал Авто Ход"))
                        {
                            player.Status.AutoMoveTimes--;
                        }

                        var newInGameLogs = player.Status.GetInGamePersonalLogs().Replace(status.ChangeMindWhat, $"~~{status.ChangeMindWhat.Replace("\n", "~~\n")}");
                        player.Status.InGamePersonalLogsAll = _help.ReplaceLastOccurrence(player.Status.InGamePersonalLogsAll, status.ChangeMindWhat, $"~~{status.ChangeMindWhat.Replace("\n", "~~\n")}");
                        player.Status.SetInGamePersonalLogs(newInGameLogs);


                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);

                        await _help.ModifyGameMessage(player, embed, components, "I've changed my mind, coming back");
                        break;

                    case "confirm-skip":
                        player.Status.ConfirmedSkip = true;

                        if (game.IsSolo())
                            break;

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);

                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "stable-Darksci":
                        var darksciType = player.Passives.DarksciTypeList;
                        darksciType.Triggered = true;
                        darksciType.IsStableType = true;
                        player.GameCharacter.AddExtraSkill(20, "Не повезло");
                        player.GameCharacter.AddMoral(2, "Не повезло");
                        player.Status.AddInGamePersonalLogs("Ну, сегодня мне не повезёт...\n");

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);

                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "not-stable-Darksci":
                        darksciType = player.Passives.DarksciTypeList;
                        darksciType.Triggered = true;
                        darksciType.IsStableType = false;
                        player.Status.AddInGamePersonalLogs("Я чувствую удачу!\n");

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);

                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "confirm-prefict":
                        player.Status.ConfirmedPredict = true;
                        game = _global.GamesList.Find(x => x.GameId == player.GameId);
                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);
                        await _help.ModifyGameMessage(player, embed, components);
                        break;
                    case "end":
                        //var dm = await button.User.CreateDMChannelAsync();
                        await _upd.EndGame(button);
                        break;

                    case "stats":

                        if (player.Status.MoveListPage == 1)
                            player.Status.MoveListPage = 2;
                        else if (player.Status.MoveListPage == 2) player.Status.MoveListPage = 1;

                        await _upd.UpdateMessage(player);
                        break;


                    case "block":
                        if (status.MoveListPage == 3)
                        {
                            await _help.SendMsgAndDeleteItAfterRound(player, "Ходить нельзя, Апни лвл!") ;
                            break;
                        }

                        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Спарта"))
                        {
                            await _help.SendMsgAndDeleteItAfterRound(player, "Спартанцы не капитулируют!!");
                            break;
                        }


                        status.IsBlock = true;
                        status.IsReady = true;

                        var text = "Ты поставил блок\n";
                        status.AddInGamePersonalLogs(text);
                        status.ChangeMindWhat = text;

                        if (game.IsSolo())
                            break;

                        await _upd.UpdateMessage(player);
                        break;
                    case "moral":
                        await HandleMoralForScore(player);
                        break;
                    case "skill":
                        await HandleMoralForSkill(player);
                        break;

                    case "char-select":
                        await HandleLvlUp(player, button);
                        await _upd.UpdateMessage(player);
                        break;
                    case "attack-select":
                        await HandleAttack(player, button);

                        if (game.IsSolo())
                            break;
                        await _upd.UpdateMessage(player);
                        break;

                    case "predict-1":
                        await HandlePredic1(player, button);
                        break;

                    case "predict-2":
                        await HandlePredic2(player, button);
                        break;
                }
                
                return;
            }
    }


    public async Task HandlePredic1(GamePlayerBridgeClass player, SocketMessageComponent button)
    {
        var account = _accounts.GetAccount(player.DiscordId);
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);


        var predictMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("predict-2")
            .WithPlaceholder(string.Join("", button.Data.Values) + " это...");

        var allCharacters = account.CharacterChance.Select(character => character.CharacterName).Where(x => x != "Sakura").ToList();

        predictMenu.AddOption("Предыдущие меню", "prev-page");
        //predictMenu.AddOption("Очистить", "empty");
        foreach (var character in allCharacters)
        {
            predictMenu.AddOption(character, string.Join("", button.Data.Values) + "||spb||" + character);
        }

        var builder = await _upd.GetGameButtons(player, game, predictMenu);

        var embed = _upd.FightPage(player);


        await _help.ModifyGameMessage(player, embed, builder);
    }

    public async Task HandlePredic2(GamePlayerBridgeClass player, SocketMessageComponent button)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);

        var embed  = _upd.FightPage(player);
        var builder = await _upd.GetGameButtons(player, game);

        var splitted = string.Join("", button.Data.Values).Split("||spb||");

        if (splitted.First() == "prev-page")
        {
            await _help.ModifyGameMessage(player, embed, builder);
            return;
        }

        var predictedPlayerUsername = splitted.First();
        var predictedCharacterName = splitted[1];
        var predictedPlayerId =
            game!.PlayersList.Find(x => x.DiscordUsername == predictedPlayerUsername)!.GetPlayerId();

        var predicted = player.Predict.Find(x => x.PlayerId == predictedPlayerId);

        if (predicted == null)
            player.Predict.Add(new PredictClass(predictedCharacterName, predictedPlayerId));
        else
            predicted.CharacterName = predictedCharacterName;


        embed = _upd.FightPage(player);
        await _help.ModifyGameMessage(player, embed, builder);
    }


    public async Task HandleLvlUp(GamePlayerBridgeClass player, SocketMessageComponent button, int botChoice = -1)
    {
        if (player.Status.LvlUpPoints <= 0)
        {
            await _upd.UpdateMessage(player);
            return;
        }
        var emoteNum = !player.IsBot() && !player.Status.IsAutoMove  ? Convert.ToInt32(string.Join("", button.Data.Values)) : botChoice;
        await GetLvlUp(player, emoteNum);
    }

    public async Task<bool> HandleAttack(GamePlayerBridgeClass player, SocketMessageComponent button, int botChoice = -1)
    {
        var status = player.Status;

        var emoteNum = !player.IsBot() && !player.Status.IsAutoMove ? Convert.ToInt32(string.Join("", button.Data.Values)) : botChoice;

        if (botChoice == -10)
        {
            var text = "Ты поставил блок\n";
            status.AddInGamePersonalLogs(text);
            status.ChangeMindWhat = text;
            status.IsBlock = true;
            status.IsReady = true;
            return true;
        }




        if (status.MoveListPage == 1)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var whoToAttack = game!.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == emoteNum);

            if (whoToAttack == null) 
                return false;

            status.WhoToAttackThisTurn.Add(whoToAttack.GetPlayerId());


            //Клинки хаоса
            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Клинки хаоса") && game.RoundNo <= 10)
            {
                var whoToAttack2 = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == emoteNum-1);
                var whoToAttack3 = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == emoteNum + 1);
                
                if (whoToAttack2 != null && whoToAttack2.GetPlayerId() != player.GetPlayerId())
                    status.WhoToAttackThisTurn.Add(whoToAttack2.GetPlayerId());
                if (whoToAttack3 != null && whoToAttack3.GetPlayerId() != player.GetPlayerId())
                    status.WhoToAttackThisTurn.Add(whoToAttack3.GetPlayerId());
            }
            //end Клинки хаоса


            /*
 (если есть лист то у варвкиа иногда появляются особые фразы: "DeepList: Видвик, ФАС!", "DeepList: Взять их!" 
 в начале игры видвик и лист получают психику. сообщение: "**Чья эта безуманя собака?**": +1 Психики)
             */

            //Weedwick
            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Weedwick Pet") && whoToAttack.GameCharacter.Passive.Any(x => x.PassiveName == "DeepList Pet"))
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await _help.SendMsgAndDeleteItAfterRound(player, "DeepList: Не нападай на хозяина, глупый пес!");
                return false;
            }

            if (whoToAttack.GameCharacter.Passive.Any(x => x.PassiveName == "DeepList Pet") && player.GameCharacter.Passive.Any(x => x.PassiveName == "Weedwick Pet"))
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await _help.SendMsgAndDeleteItAfterRound(player, "Не нападай на свою собаку. Ты чего, совсем уже ебнулся?");
                return false;
            }
            // end Weedwick


            if (game.PlayersList.Any(x => x.GameCharacter.Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят") && x.Status.GetPlaceAtLeaderBoard() == emoteNum) && game.RoundNo == 10)
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await _help.SendMsgAndDeleteItAfterRound(player, "Выбранный игрок недоступен в связи с баном за нарушение правил");
                return false;
            }


            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "СОсиновый кол") && player.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 &&  status.WhoToAttackThisTurn.Contains(x.EnemyId)))
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await game.Phrases.VampyrNoAttack.SendLogSeparate(player, false);
                return false;
            }


            if (status.WhoToAttackThisTurn.Contains(player.GetPlayerId()))
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await _help.SendMsgAndDeleteItAfterRound(player, "Зачем ты себя бьешь?");
                return false;
            }

            status.IsReady = true;
            status.IsBlock = false;
            var text = $"Ты напал на игрока {whoToAttack.DiscordUsername}\n";
            player.Status.AddInGamePersonalLogs(text);
            player.Status.ChangeMindWhat = text;
            return true;
        }

        return false;
    }

    //for GetLvlUp ONLY!
    public async Task LvlUp10(GamePlayerBridgeClass player)

    {
        await _help.SendMsgAndDeleteItAfterRound(player, "10 максимум, выбери другой стат"); //not awaited 
    }


    private async Task GetLvlUp(GamePlayerBridgeClass player, int skillNumber)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        switch (skillNumber)
        {
            case 1:
                if (player.GameCharacter.GetIntelligence() >= 10 && 
                    (player.GameCharacter.GetPsyche() <= 9 || player.GameCharacter.GetStrength() <= 9 || player.GameCharacter.GetSpeed() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddIntelligence(1, "Прокачка", false);

                switch (player.GameCharacter.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Интеллект на {player.GameCharacter.GetIntelligence()}!\n");
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Интеллект до {player.GameCharacter.GetIntelligence()}\n");
                        break;
                }
                break;

            case 2:
                if (player.GameCharacter.GetStrength() >= 10 && 
                    (player.GameCharacter.GetPsyche() <= 9 || player.GameCharacter.GetIntelligence() <= 9 || player.GameCharacter.GetSpeed() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddStrength(1, "Прокачка", false);
                switch (player.GameCharacter.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Силу на {player.GameCharacter.GetStrength()}!\n");
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Силу до {player.GameCharacter.GetStrength()}\n");
                        break;
                }
                break;

            case 3:
                if (player.GameCharacter.GetSpeed() >= 10 && 
                    (player.GameCharacter.GetPsyche() <= 9 || player.GameCharacter.GetStrength() <= 9 || player.GameCharacter.GetIntelligence() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddSpeed(1, "Прокачка", false);

                switch (player.GameCharacter.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Cкорость на {player.GameCharacter.GetSpeed()}!\n");
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Скорость до {player.GameCharacter.GetSpeed()}\n");
                        break;
                }
                break;

            case 4:
                if (player.GameCharacter.GetPsyche() >= 10 && 
                    (player.GameCharacter.GetIntelligence() <= 9 || player.GameCharacter.GetStrength() <= 9 || player.GameCharacter.GetSpeed() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddPsyche(1, "Прокачка", false);

                switch (player.GameCharacter.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Психику на {player.GameCharacter.GetPsyche()}!\n");
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Психику до {player.GameCharacter.GetPsyche()}\n");
                        break;
                }
                break;
        }

        player.Status.LvlUpPoints--;

        
        if (player.Status.LvlUpPoints > 0)
        {
            //Я пытаюсь!
            try
            {
                if (!player.IsBot())
                    await _help.SendMsgAndDeleteItAfterRound(player, $"Осталось еще {player.Status.LvlUpPoints} очков характеристик. Пытайся!");
            }
            catch (Exception exception)
            {
                _logs.Critical(exception.Message);
                _logs.Critical(exception.StackTrace);
            }
            //end Я пытаюсь!
        }
        else
        {
            player.Status.MoveListPage = 1;
        }
        

        //Дизмораль
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Дизмораль"))
        {
            if (game!.RoundNo == 9)
                //Дизмораль Part #2
                player.GameCharacter.AddPsyche(-4, "Дизмораль");
            //end Дизмораль Part #2

            //Да всё нахуй эту игру: Part #2
            if (game.RoundNo is 9 or 7 or 5 or 3)
                if (player.GameCharacter.GetPsyche() <= 0)
                {
                    player.Status.IsSkip = true;
                    player.Status.IsBlock = false;
                    player.Status.IsReady = true;
                    player.Status.WhoToAttackThisTurn = new List<Guid>();
                    game.Phrases.DarksciFuckThisGame.SendLog(player, true);
                }
            //end Да всё нахуй эту игру: Part #2
        }
        //end Дизмораль

        //Обучение
        //There is a second part in "HandleEndOfRound"!!!!!!!!!! <<<<<<<<<<
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Обучение"))
        {
            var siriTraining = player.Passives.SirinoksTraining;
            if (siriTraining != null)
            {
                if (siriTraining.Training.Count > 0)
                {
                    var training = siriTraining.Training.First();

                    switch (training.StatIndex)
                    {
                        case 1:
                            if (player.GameCharacter.GetIntelligence() >= training.StatNumber)
                            {
                                player.GameCharacter.AddMoral(3, "Обучение");
                                player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                siriTraining.Training.Clear();
                            }
                            break;
                        case 2:
                            if (player.GameCharacter.GetStrength() >= training.StatNumber)
                            {
                                player.GameCharacter.AddMoral(3, "Обучение");
                                player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                siriTraining.Training.Clear();
                            }
                            break;
                        case 3:
                            if (player.GameCharacter.GetSpeed() >= training.StatNumber)
                            {
                                player.GameCharacter.AddMoral(3, "Обучение");
                                player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                siriTraining.Training.Clear();
                            }
                            break;
                        case 4:
                            if (player.GameCharacter.GetPsyche() >= training.StatNumber)
                            {
                                player.GameCharacter.AddMoral(3, "Обучение");
                                player.GameCharacter.AddIntelligenceQualitySkillBonus(1, "Обучение");
                                siriTraining.Training.Clear();
                            }
                            break;
                    }


                }
            }
        }
        //end Обучение

        await _upd.UpdateMessage(player);
    }
}