using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling;

public sealed class GameReaction : IServiceSingleton
{
    private readonly UserAccounts _accounts;
    private readonly Global _global;
    private readonly HelperFunctions _help;
    private readonly Logs _logs;
    private readonly GameUpdateMess _upd;
    private readonly InGameGlobal _gameGlobal;
   

    public GameReaction(UserAccounts accounts,
        Global global,
        GameUpdateMess upd, HelperFunctions help, Logs logs, InGameGlobal inGameGlobal)
    {
        _accounts = accounts;
        _global = global;

        _upd = upd;
        _help = help;
        _logs = logs;
        _gameGlobal = inGameGlobal;

        //  _gameGlobal = gameGlobal;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    //    private readonly InGameGlobal _gameGlobal;
    public async Task ReactionAddedGameWindow(SocketMessageComponent button)
    {
        foreach (var t in _global.GamesList)
            if (t.PlayersList.Any(x =>
                    x.DiscordId == button.User.Id &&
                    x.Status.SocketMessageFromBot.Id == button.Message.Id))
            {
                var player = _global.GetGameAccount(button.User.Id, t.PlayersList.FirstOrDefault().GameId);
                
                var now = DateTimeOffset.UtcNow;
                if ((now - player.Status.LastButtonPress).TotalMilliseconds < 700)
                {
                    await button.RespondAsync("Ошибка: Слишком быстро! Нажми на кнопку еще раз.", ephemeral:true);
                    return;
                }
                player.Status.LastButtonPress = DateTimeOffset.UtcNow;

                var status = player.Status;
                var components = new ComponentBuilder();
                var embed = new EmbedBuilder();
                var game = _global.GamesList.Find(x => x.GameId == player.GameId);
                var extraText = "";

                switch (button.Data.CustomId)
                {
                    case "auto-move":
                        player.Status.IsAutoMove = true;
                        player.Status.IsReady = true;
                        player.Status.AutoMoveTimes++;
                        switch (player.Status.AutoMoveTimes)
                        {
                            case 1:
                                await game.Phrases.AutoMove1.SendLogSeparate(player, false);
                                break;
                            case 2:
                                await game.Phrases.AutoMove2.SendLogSeparate(player, false);
                                break;
                            case 3:
                                await game.Phrases.AutoMove3.SendLogSeparate(player, false);
                                break;
                            case 4:
                                await game.Phrases.AutoMove4.SendLogSeparate(player, false);
                                break;
                            case 5:
                                await game.Phrases.AutoMove5.SendLogSeparate(player, false);
                                break;
                            case 6:
                                await game.Phrases.AutoMove6.SendLogSeparate(player, false);
                                break;
                            case 7:
                                await game.Phrases.AutoMove7.SendLogSeparate(player, false);
                                break;
                            case 8:
                                await game.Phrases.AutoMove8.SendLogSeparate(player, false);
                                break;
                            case 9:
                                await game.Phrases.AutoMove9.SendLogSeparate(player, false);
                                break;
                            case 10:
                                await game.Phrases.AutoMove10.SendLogSeparate(player, false);
                                break;
                        }
                        var textAutomove = $"Ты использовал Авто Ход\n";
                        player.Status.AddInGamePersonalLogs(textAutomove);
                        player.Status.ChangeMindWhat = textAutomove;

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);
                        await _help.ModifyGameMessage(player, embed, components);
                        break;
                    case "change-mind":
                        if (player.Status.IsSkip || !player.Status.IsReady)
                        {
                            break;
                        }
                        player.Status.IsAbleToChangeMind = false;
                        player.Status.IsAutoMove = false;

                        player.Status.IsAbleToTurn = true;
                        player.Status.IsReady = false;
                        player.Status.IsBlock = false;
                        player.Status.WhoToAttackThisTurn = Guid.Empty;

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
                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);
                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "stable-Darksci":
                        var darksciType = _gameGlobal.DarksciTypeList.Find(x => x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);
                        darksciType.Triggered = true;
                        darksciType.IsStableType = true;
                        player.Character.AddExtraSkill(player.Status, 20, "Не повезло");
                        player.Character.AddMoral(player.Status, 2, "Не повезло");
                        player.Status.AddInGamePersonalLogs("Ну, сегодня мне не повезёт...\n");

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);
                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "not-stable-Darksci":
                        darksciType = _gameGlobal.DarksciTypeList.Find(x => x.PlayerId == player.GetPlayerId() && game.GameId == x.GameId);
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
                            await _help.SendMsgAndDeleteItAfterRound(player, "Ходить нельзя, Апни лвл!") ;
                            break;
                        }

                        if (player.Character.Name == "mylorik")
                        {
                            await _help.SendMsgAndDeleteItAfterRound(player, "Спартанцы не капитулируют!!");
                            break;
                        }


                        status.IsBlock = true;
                        status.IsAbleToTurn = false;
                        status.IsReady = true;

                        var text = "Ты поставил блок\n";
                        status.AddInGamePersonalLogs(text);
                        status.ChangeMindWhat = text;

                        await _upd.UpdateMessage(player);
                        break;
                    case "moral":
                        var tempMoral = player.Character.GetMoral();


                        if (player.Character.GetMoral() >= 20)
                        {
                            player.Character.AddMoral(player.Status, -20, "Обмен Морали", true, true);
                            player.Character.AddBonusPointsFromMoral(10);
                            extraText = "Мораль: Я БОГ ЭТОГО МИРА +10 __бонунсых__ очков";
                        }
                        else if (player.Character.GetMoral() >= 13)
                        {
                            player.Character.AddMoral(player.Status, -13, "Обмен Морали", true, true);
                            player.Character.AddBonusPointsFromMoral(5);
                            extraText = "Мораль: МВП +5 __бонунсых__ очков";
                        }
                        else if (player.Character.GetMoral() >= 8)
                        {
                            player.Character.AddMoral(player.Status, -8, "Обмен Морали", true, true);
                            player.Character.AddBonusPointsFromMoral(2);
                            extraText = "Мораль: Я богач! +2 __бонунсых__ очков";
                        }
                        else if (player.Character.GetMoral() >= 5)
                        {
                            player.Character.AddMoral(player.Status, -5, "Обмен Морали", true, true);
                            player.Character.AddBonusPointsFromMoral(1);
                            extraText = "Мораль: Ойвей +1 __бонунсых__ очка";
                        }

                        if (tempMoral >= 3)
                            await _upd.UpdateMessage(t.PlayersList.Find(x => x.DiscordId == player.DiscordId), extraText);
                        break;
                    case "skill":
                        var tempSkill = player.Character.GetMoral();


                        if (player.Character.GetMoral() >= 20)
                        {
                            player.Character.AddMoral(player.Status, -20, "Обмен Морали", true, true);
                            player.Character.AddExtraSkill(player.Status,  100, "Обмен Морали");
                            extraText = "Мораль: Я БОГ ЭТОГО МИРА!!! +100 *Скилла*";
                        }
                        else if (player.Character.GetMoral() >= 13)
                        {
                            player.Character.AddMoral(player.Status, -13, "Обмен Морали", true, true);
                            player.Character.AddExtraSkill(player.Status,  50, "Обмен Морали");
                            extraText = "Мораль: MVP! +50 *Скилла*";
                        }
                        else if (player.Character.GetMoral() >= 8)
                        {
                            player.Character.AddMoral(player.Status, -8, "Обмен Морали", true, true);
                            player.Character.AddExtraSkill(player.Status,  30, "Обмен Морали");
                            extraText = "Мораль: Я художник! +30 *Скилла*";
                        }
                        else if (player.Character.GetMoral() >= 5)
                        {
                            player.Character.AddMoral(player.Status, -5, "Обмен Морали", true, true);
                            player.Character.AddExtraSkill(player.Status, 18, "Обмен Морали");
                            extraText = "Мораль: Изи катка +18 *Скилла*";
                        }
                        else if (player.Character.GetMoral() >= 3)
                        {
                            player.Character.AddMoral(player.Status, -3, "Обмен Морали", true, true);
                            player.Character.AddExtraSkill(player.Status,  10, "Обмен Морали");
                            extraText = "Мораль: Набрался *Скилла*, так сказать. +10 *Скилла*";
                        }
                        else if (player.Character.GetMoral() >= 2)
                        {
                            player.Character.AddMoral(player.Status, -2, "Обмен Морали", true, true);
                            player.Character.AddExtraSkill(player.Status,  6, "Обмен Морали");
                            extraText = "Мораль: Так вот как в это играть. +6 *Скилла*";
                        }
                        else if (player.Character.GetMoral() >= 1)
                        {
                            player.Character.AddMoral(player.Status, -1, "Обмен Морали", true, true);
                            player.Character.AddExtraSkill(player.Status,  2, "Обмен Морали");
                            extraText = "Мораль: Это что? +2 *Скилла*";
                        }

                        if (tempSkill >= 1)
                            await _upd.UpdateMessage(t.PlayersList.Find(x => x.DiscordId == player.DiscordId), extraText);
                        break;

                    case "char-select":
                        await HandleLvlUp(player, button);
                        await _upd.UpdateMessage(player);
                        break;
                    case "attack-select":
                        await HandleAttack(player, button);
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

        builder = await _upd.GetGameButtons(player, game, predictMenu);

        var embed = new EmbedBuilder();
        embed = _upd.FightPage(player);


        await _help.ModifyGameMessage(player, embed, builder);
    }

    public async Task HandlePredic2(GamePlayerBridgeClass player, SocketMessageComponent button)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        var builder = new ComponentBuilder();
        var embed = new EmbedBuilder();
        embed = _upd.FightPage(player);

        builder = await _upd.GetGameButtons(player, game);

        var splitted = string.Join("", button.Data.Values).Split("||spb||");

        if (splitted.First() == "prev-page")
        {
            await _help.ModifyGameMessage(player, embed, builder);
            return;
        }

        var predictedPlayerUsername = splitted.First();
        var predictedCharacterName = splitted[1];
        var predictedPlayerId =
            game.PlayersList.Find(x => x.DiscordUsername == predictedPlayerUsername).GetPlayerId();

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
            status.IsAbleToTurn = false;
            status.IsReady = true;
            return true;
        }


        if (!status.IsAbleToTurn)
        {
            await _help.SendMsgAndDeleteItAfterRound(player,
                player.Status.IsSkip
                    ? "Что-то заставило тебя пропустить этот ход..."
                    : "Ходить нельзя, пока идет подсчёт.");

            return true;
        }


        if (status.MoveListPage == 1)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var whoToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == emoteNum);

            if (whoToAttack == null) 
                return false;

            status.WhoToAttackThisTurn = whoToAttack.GetPlayerId();

            if (game.PlayersList.Any(x => x.Character.Name == "Тигр" && x.Status.PlaceAtLeaderBoard == emoteNum) && game.RoundNo == 10)
            {
                status.WhoToAttackThisTurn = Guid.Empty;
                await _help.SendMsgAndDeleteItAfterRound(player, "Выбранный игрок недоступен в связи с баном за нарушение правил");
                return false;
            }


            if (player.Character.Name == "Вампур" && player.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 && x.EnemyId == status.WhoToAttackThisTurn))
            {
                status.WhoToAttackThisTurn = Guid.Empty;
                await game.Phrases.VampyrNoAttack.SendLogSeparate(player, false);
                return false;
            }


            if (status.WhoToAttackThisTurn == player.GetPlayerId())
            {
                status.WhoToAttackThisTurn = Guid.Empty;
                await _help.SendMsgAndDeleteItAfterRound(player, "Зачем ты себя бьешь?");
                return false;
            }

            status.IsAbleToTurn = false;
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

                if (player.Character.GetIntelligence() >= 10 && player.Character.GetPsyche() <= 9 &&
                    player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                {
                    if (player.Character.Name != "Вампур_")
                    {
                        await LvlUp10(player);
                        return;
                    }
                }

                player.Character.AddIntelligence(player.Status, 1, "Прокачка", false);

                switch (player.Character.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Интеллект на {player.Character.GetIntelligence()}!\n");
                        break;
                    case "Вампур_":
                        player.Character.AddIntelligence(player.Status, -1, "Прокачка", false);
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Интеллект до {player.Character.GetIntelligence()}\n");
                        break;
                }
                break;
            case 2:

                if (player.Character.GetStrength() >= 10 && player.Character.GetPsyche() <= 9 &&
                    player.Character.GetIntelligence() <= 9 && player.Character.GetSpeed() <= 9)
                {
                    if (player.Character.Name != "Вампур_")
                    {
                        await LvlUp10(player);
                        return;
                    }
                }

                player.Character.AddStrength(player.Status, 1, "Прокачка", false);
                switch (player.Character.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Силу на {player.Character.GetStrength()}!\n");
                        break;
                    case "Вампур_":
                        player.Character.AddStrength(player.Status, -1, "Прокачка", false);
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Силу до {player.Character.GetStrength()}\n");
                        break;
                }

                break;
            case 3:

                if (player.Character.GetSpeed() >= 10 && player.Character.GetPsyche() <= 9 &&
                    player.Character.GetStrength() <= 9 && player.Character.GetIntelligence() <= 9)
                {
                    if (player.Character.Name != "Вампур_")
                    {
                        await LvlUp10(player);
                        return;
                    }
                }

                player.Character.AddSpeed(player.Status, 1, "Прокачка", false);
                switch (player.Character.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Cкорость на {player.Character.GetSpeed()}!\n");
                        break;
                    case "Вампур_":
                        player.Character.AddSpeed(player.Status, -1, "Прокачка", false);
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Скорость до {player.Character.GetSpeed()}\n");
                        break;
                }

                break;
            case 4:

                if (player.Character.GetPsyche() >= 10 && player.Character.GetIntelligence() <= 9 && player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                {
                    if (player.Character.Name != "Вампур_")
                    {
                        await LvlUp10(player);
                        return;
                    }
                }

                player.Character.AddPsyche(player.Status, 1, "Прокачка", false);
                switch (player.Character.Name)
                {
                    case "HardKitty":
                        player.Status.AddInGamePersonalLogs($"#life: Я прокачал Психику на {player.Character.GetPsyche()}!\n");
                        break;
                    case "Вампур_":
                        player.Character.AddPsyche(player.Status, -1, "Прокачка", false);
                        break;
                    default:
                        player.Status.AddInGamePersonalLogs($"Ты улучшил Психику до {player.Character.GetPsyche()}\n");
                        break;
                }

                break;
        }

        //Я пытаюсь!
        if (player.Status.LvlUpPoints > 1)
        {
            player.Status.LvlUpPoints--;
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
        }
        else
        {
            player.Status.MoveListPage = 1;
        }
        //end Я пытаюсь!


        //Дизмораль
        
        if (player.Character.Name == "Darksci")
        {
            if (game.RoundNo == 9)
                //Дизмораль Part #2
                player.Character.AddPsyche(player.Status, -4, "Дизмораль");
            //end Дизмораль Part #2

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

       await _upd.UpdateMessage(player);
        
    }
}