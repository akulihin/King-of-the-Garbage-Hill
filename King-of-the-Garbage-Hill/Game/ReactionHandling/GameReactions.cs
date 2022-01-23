using System;
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
    private readonly Logs _logs;
    private readonly GameUpdateMess _upd;

    public GameReaction(UserAccounts accounts,
        Global global,
        GameUpdateMess upd, HelperFunctions help, Logs logs)
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
                
                var now = DateTimeOffset.UtcNow;
                if ((now - player.Status.LastButtonPress).TotalMilliseconds < 1500)
                {
                    button.RespondAsync("Ошибка: Слишком быстро! Нажми на кнопку еще раз.", ephemeral:true);
                    return;
                }

                var status = player.Status;
                var components = new ComponentBuilder();
                var embed = new EmbedBuilder();
                var game = _global.GamesList.Find(x => x.GameId == player.GameId);
                

                switch (button.Data.CustomId)
                {
                    case "auto-move":
                        player.Status.IsAutoMove = true;
                        player.Status.IsReady = true;
                        var textAutomove = $"Ты использовал Авто Ход\n";
                        player.Status.AddInGamePersonalLogs(textAutomove);
                        player.Status.ChangeMindWhat = textAutomove;

                        embed = _upd.FightPage(player);
                        components = _upd.GetGameButtons(player, game);
                        _help.ModifyGameMessage(player, embed, components);
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

                        var newInGameLogs = player.Status.GetInGamePersonalLogs().Replace(status.ChangeMindWhat, $"~~{status.ChangeMindWhat.Replace("\n", "~~\n")}");
                        player.Status.InGamePersonalLogsAll = _help.ReplaceLastOccurrence(player.Status.InGamePersonalLogsAll, status.ChangeMindWhat, $"~~{status.ChangeMindWhat.Replace("\n", "~~\n")}");
                        player.Status.SetInGamePersonalLogs(newInGameLogs);

                        embed = _upd.FightPage(player);

                        components = _upd.GetGameButtons(player, game);
                        _help.ModifyGameMessage(player, embed, components);
                        _help.SendMsgAndDeleteItAfterRound(player, "I've changed my mind, coming back");
                        break;

                    case "confirm-skip":
                        player.Status.ConfirmedSkip = true;
                        embed = _upd.FightPage(player);
                        components = _upd.GetGameButtons(player, game);
                        _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "confirm-prefict":
                        player.Status.ConfirmedPredict = true;
                        game = _global.GamesList.Find(x => x.GameId == player.GameId);
                        embed = _upd.FightPage(player);
                        components = _upd.GetGameButtons(player, game);
                        _help.ModifyGameMessage(player, embed, components);
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
                            break;
                        }

                        if (player.Character.Name == "mylorik")
                        {
                            _help.SendMsgAndDeleteItAfterRound(player, "Спартанцы не капитулируют!!");
                            break;
                        }


                        status.IsBlock = true;
                        status.IsAbleToTurn = false;
                        status.IsReady = true;
                        var text = "Ты поставил блок\n";
                        status.AddInGamePersonalLogs(text);
                        status.ChangeMindWhat = text;

                        _upd.UpdateMessage(player);
                        break;

                    case "moral":
                        var tempMoral = player.Character.GetMoral();


                        if (player.Character.GetMoral() >= 20)
                        {
                            player.Character.AddMoral(player.Status, -20, "Обмен Морали: ", true, true);
                            player.Character.AddBonusPointsFromMoral(14);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Я БОГ ЭТОГО МИРА +14 __бонунсых__ очков");
                        }
                        else if (player.Character.GetMoral() >= 13)
                        {
                            player.Character.AddMoral(player.Status, -13, "Обмен Морали: ", true, true);
                            player.Character.AddBonusPointsFromMoral(8);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: МВП +8 __бонунсых__ очков");
                        }
                        else if (player.Character.GetMoral() >= 8)
                        {
                            player.Character.AddMoral(player.Status, -8, "Обмен Морали: ", true, true);
                            player.Character.AddBonusPointsFromMoral(4);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Я богач! +4 __бонунсых__ очков");
                        }
                        else if (player.Character.GetMoral() >= 5)
                        {
                            player.Character.AddMoral(player.Status, -5, "Обмен Морали: ", true, true);
                            player.Character.AddBonusPointsFromMoral(2);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Изи катка +2 __бонунсых__ очка");
                        }
                        else if (player.Character.GetMoral() >= 3)
                        {
                            player.Character.AddMoral(player.Status, -3, "Обмен Морали: ", true, true);
                            player.Character.AddBonusPointsFromMoral(1);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Ойвей +1  __бонунсых__ очка");
                        }

                        if (tempMoral >= 3)
                            _upd.UpdateMessage(t.PlayersList.Find(x => x.DiscordId == player.DiscordId));
                        break;
                    case "skill":
                        var tempSkill = player.Character.GetMoral();


                        if (player.Character.GetMoral() >= 20)
                        {
                            player.Character.AddMoral(player.Status, -20, "Обмен Морали: ", true, true);
                            player.Character.AddExtraSkill(player.Status, "Обмен Морали: ", 114);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Я БОГ ЭТОГО МИРА!!! +114 *Скилла*");
                        }
                        else if (player.Character.GetMoral() >= 13)
                        {
                            player.Character.AddMoral(player.Status, -13, "Обмен Морали: ", true, true);
                            player.Character.AddExtraSkill(player.Status, "Обмен Морали: ", 69);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: MVP! +69 *Скилла*");
                        }
                        else if (player.Character.GetMoral() >= 8)
                        {
                            player.Character.AddMoral(player.Status, -8, "Обмен Морали: ", true, true);
                            player.Character.AddExtraSkill(player.Status, "Обмен Морали: ", 39);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Я художник! +39 *Скилла*");
                        }
                        else if (player.Character.GetMoral() >= 5)
                        {
                            player.Character.AddMoral(player.Status, -5, "Обмен Морали: ", true, true);
                            player.Character.AddExtraSkill(player.Status, "Обмен Морали: ", 24);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Изи катка +24 *Скилла*");
                        }
                        else if (player.Character.GetMoral() >= 3)
                        {
                            player.Character.AddMoral(player.Status, -3, "Обмен Морали: ", true, true);
                            player.Character.AddExtraSkill(player.Status, "Обмен Морали: ", 14);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Набрался *Скилла*, так сказать. +14 *Скилла*");
                        }
                        else if (player.Character.GetMoral() >= 2)
                        {
                            player.Character.AddMoral(player.Status, -2, "Обмен Морали: ", true, true);
                            player.Character.AddExtraSkill(player.Status, "Обмен Морали: ", 9);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Так вот как в это играть. +9 *Скилла*");
                        }
                        else if (player.Character.GetMoral() >= 1)
                        {
                            player.Character.AddMoral(player.Status, -1, "Обмен Морали: ", true, true);
                            player.Character.AddExtraSkill(player.Status, "Обмен Морали: ", 4);
                            _help.SendMsgAndDeleteItAfterRound(player, "Мораль: Это что? +4 *Скилла*");
                        }

                        if (tempSkill >= 1)
                            _upd.UpdateMessage(t.PlayersList.Find(x => x.DiscordId == player.DiscordId));
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
                player.Status.LastButtonPress = DateTimeOffset.UtcNow;
                
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

        builder = _upd.GetGameButtons(player, game, predictMenu);

        var embed = new EmbedBuilder();
        embed = _upd.FightPage(player);
        

        _help.ModifyGameMessage(player, embed, builder);
    }

    public async Task HandlePredic2(GamePlayerBridgeClass player, SocketMessageComponent button)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        var builder = new ComponentBuilder();
        var embed = new EmbedBuilder();
        embed = _upd.FightPage(player);

        builder = _upd.GetGameButtons(player, game);

        var splitted = string.Join("", button.Data.Values).Split("||spb||");

        if (splitted[0] == "prev-page")
        {
            _help.ModifyGameMessage(player, embed, builder);
            return;
        }

        var predictedPlayerUsername = splitted[0];
        var predictedCharacterName = splitted[1];
        var predictedPlayerId =
            game.PlayersList.Find(x => x.DiscordUsername == predictedPlayerUsername).Status.PlayerId;

        var predicted = player.Predict.Find(x => x.PlayerId == predictedPlayerId);

        if (predicted == null)
            player.Predict.Add(new PredictClass(predictedCharacterName, predictedPlayerId));
        else
            predicted.CharacterName = predictedCharacterName;


        embed = _upd.FightPage(player);
        _help.ModifyGameMessage(player, embed, builder);
    }


    public async Task HandleLvlUp(GamePlayerBridgeClass player, SocketMessageComponent button, int botChoice = -1)
    {
        var emoteNum = !player.IsBot() && !player.Status.IsAutoMove  ? Convert.ToInt32(string.Join("", button.Data.Values)) : botChoice;
        await GetLvlUp(player, emoteNum);
    }

    public async Task<bool> HandleAttack(GamePlayerBridgeClass player, SocketMessageComponent button,
        int botChoice = -1)
    {
        var status = player.Status;

        var emoteNum = !player.IsBot() && !player.Status.IsAutoMove ? Convert.ToInt32(string.Join("", button.Data.Values)) : botChoice;

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

            if (whoToAttack == null) return false;

            status.WhoToAttackThisTurn = whoToAttack.Status.PlayerId;

            if (game.PlayersList.Any(x => x.Character.Name == "Тигр" && x.Status.PlaceAtLeaderBoard == emoteNum) &&
                game.RoundNo == 10)
            {
                status.WhoToAttackThisTurn = Guid.Empty;
                _help.SendMsgAndDeleteItAfterRound(player,
                    "Выбранный игрок недоступен в связи с баном за нарушение правил");
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
                game.Phrases.VampyrNoAttack.SendLogSeparate(player, false);
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
            var text = $"Ты напал на игрока {whoToAttack.DiscordUsername}\n";
            player.Status.AddInGamePersonalLogs(text);
            player.Status.ChangeMindWhat = text;
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

                player.Character.AddIntelligence(player.Status, 1, "Прокачка: ", false);

                if (player.Character.Name == "HardKitty")
                    player.Status.AddInGamePersonalLogs(
                        $"#life: Я прокачал Интеллект на {player.Character.GetIntelligence()}!\n");
                else
                    player.Status.AddInGamePersonalLogs(
                        $"Ты улучшил Интеллект до {player.Character.GetIntelligence()}\n");
                break;
            case 2:

                if (player.Character.GetStrength() >= 10 && player.Character.GetPsyche() <= 9 &&
                    player.Character.GetIntelligence() <= 9 && player.Character.GetSpeed() <= 9)
                {
                    LvlUp10(player);
                    return;
                }

                player.Character.AddStrength(player.Status, 1, "Прокачка: ", false);
                if (player.Character.Name == "HardKitty")
                    player.Status.AddInGamePersonalLogs(
                        $"#life: Я прокачал Силу на {player.Character.GetStrength()}!\n");
                else
                    player.Status.AddInGamePersonalLogs($"Ты улучшил Силу до {player.Character.GetStrength()}\n");

                break;
            case 3:

                if (player.Character.GetSpeed() >= 10 && player.Character.GetPsyche() <= 9 &&
                    player.Character.GetStrength() <= 9 && player.Character.GetIntelligence() <= 9)
                {
                    LvlUp10(player);
                    return;
                }

                player.Character.AddSpeed(player.Status, 1, "Прокачка: ", false);
                if (player.Character.Name == "HardKitty")
                    player.Status.AddInGamePersonalLogs(
                        $"#life: Я прокачал Cкорость на {player.Character.GetSpeed()}!\n");
                else
                    player.Status.AddInGamePersonalLogs($"Ты улучшил Скорость до {player.Character.GetSpeed()}\n");

                break;
            case 4:

                if (player.Character.GetPsyche() >= 10 && player.Character.GetIntelligence() <= 9 &&
                    player.Character.GetStrength() <= 9 && player.Character.GetSpeed() <= 9)
                {
                    LvlUp10(player);
                    return;
                }

                player.Character.AddPsyche(player.Status, 1, "Прокачка: ", false);
                if (player.Character.Name == "HardKitty")
                    player.Status.AddInGamePersonalLogs(
                        $"#life: Я прокачал Психику на {player.Character.GetPsyche()}!\n");
                else
                    player.Status.AddInGamePersonalLogs($"Ты улучшил Психику до {player.Character.GetPsyche()}\n");

                break;
        }

        //Я пытаюсь!
        if (player.Status.LvlUpPoints > 1)
        {
            player.Status.LvlUpPoints--;
            try
            {
                if (!player.IsBot())
                    _help.SendMsgAndDeleteItAfterRound(player,
                        $"Осталось еще {player.Status.LvlUpPoints} очков характеристик. Пытайся!");
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
                //Дизмораль Part #2
                player.Character.AddPsyche(player.Status, -4, "Дизмораль: ");
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

        _upd.UpdateMessage(player);
        await Task.CompletedTask;
    }
}