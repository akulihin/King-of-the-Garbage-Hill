using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
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
    private readonly SecureRandom _random;
    private readonly CharactersPull _charactersPull;

    public GameReaction(UserAccounts accounts, Global global, GameUpdateMess upd, HelperFunctions help, LoginFromConsole logs, SecureRandom random, CharactersPull charactersPull)
    {
        _accounts = accounts;
        _global = global;
        _upd = upd;
        _help = help;
        _logs = logs;
        _random = random;
        _charactersPull = charactersPull;
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
        else if (player.GameCharacter.GetMoral() >= 7 && player.GameCharacter.Passive.Any(x => x.PassiveName == "Еврей"))
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
                    x.DiscordStatus.SocketGameMessage.Id == button.Message.Id))
            {
                var player = _global.GetGameAccount(button.User.Id, t.PlayersList.FirstOrDefault()!.GameId);
                
                var now = DateTimeOffset.UtcNow;
                if ((now - player.Status.LastButtonPress).TotalMilliseconds < 700)
                {
                    await button.FollowupAsync("Ошибка: Слишком быстро! Нажми на кнопку еще раз.", ephemeral:true);
                    return;
                }
                player.Status.LastButtonPress = DateTimeOffset.UtcNow;

                var status = player.Status;
                var game = _global.GamesList.Find(x => x.GameId == player.GameId);
                var extraText = "";


                switch (button.Data.CustomId)
                {
                    case "mobile-device":
                        player.IsMobile = true;
                        var embed = _upd.FightPage(player);
                        var components = await _upd.GetGameButtons(player, game);
                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "auto-move":
                        player.Status.AutoMoveTimes++;
                        await game!.Phrases.AutoMove.SendLogSeparate(player, true, 7000, false);

                        var textAutomove = $"Вы использовали Авто Ход\n";
                        player.Status.AddInGamePersonalLogs(textAutomove);
                        player.Status.ChangeMindWhat = textAutomove;

                        player.Status.IsAutoMove = true;
                        player.Status.IsReady = true;

                        
                        if (player.IsSolo(game))
                            break;

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

                        player.Status.IsReady = false;
                        player.Status.IsBlock = false;
                        player.Status.WhoToAttackThisTurn = new List<Guid>();

                        if (status.ChangeMindWhat.Contains("Вы использовали Авто Ход"))
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

                        if (player.IsSolo(game))
                            break;

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);

                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "stable-Darksci":
                        var darksciType = player.Passives.DarksciTypeList;
                        if(darksciType.Triggered)
                            break;
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
                        if (darksciType.Triggered)
                            break;
                        darksciType.Triggered = true;
                        darksciType.IsStableType = false;
                        player.Status.AddInGamePersonalLogs("Я чувствую удачу!\n");

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);

                        await _help.ModifyGameMessage(player, embed, components);
                        break;

                    case "yong-gleb":
                        var character = _charactersPull.GetAllCharactersNoFilter().First(x => x.Name == "Молодой Глеб");
                        //player.GameCharacter.Name = character.Name;
                        player.GameCharacter.Passive = new List<Passive>();
                        player.GameCharacter.Passive = character.Passive;
                        player.GameCharacter.Avatar = character.Avatar;
                        player.GameCharacter.AvatarCurrent = character.Avatar;
                        player.GameCharacter.Description = character.Description;
                        player.GameCharacter.Tier = character.Tier;
                        player.GameCharacter.SetIntelligence(character.GetIntelligence(), "yong-gleb", false);
                        player.GameCharacter.SetStrength(character.GetStrength(), "yong-gleb", false);
                        player.GameCharacter.SetSpeed(character.GetSpeed(), "yong-gleb", false);
                        player.GameCharacter.SetPsyche(character.GetPsyche(), "yong-gleb", false);

                        //Спящее хуйло
                        player.Status.IsSkip = false;
                        player.Status.ConfirmedSkip = true;
                        player.Status.IsReady = false;
                        player.Status.WhoToAttackThisTurn = new List<Guid>();

                        player.GameCharacter.AddExtraSkill(30, "Спящее хуйло", false);
                        player.Status.ClearInGamePersonalLogs();
                        //end Спящее хуйло

                        embed = _upd.FightPage(player);
                        components = await _upd.GetGameButtons(player, game);

                        await _upd.UpdateCharacterMessage(player);
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
                        player.Status.MoveListPage = player.Status.MoveListPage == 2 ? 1 : 2;

                        await _upd.UpdateMessage(player);
                        break;

                    case "debug_info":
                        player.Status.MoveListPage = player.Status.MoveListPage == 4 ? 1 : 4;

                        await _upd.UpdateMessage(player);
                        break;


                    case "block":
                        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Спарта"))
                        {
                            await _help.SendMsgAndDeleteItAfterRound(player, "Спартанцы не капитулируют!!", 0);
                            break;
                        }

                        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Aggress"))
                        {
                            await _help.SendMsgAndDeleteItAfterRound(player, "I. WONT. STOP.", 0);
                            break;
                        }

                        // Dopa Макро — block counts as one of two actions
                        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Макро"))
                        {
                            if (status.WhoToAttackThisTurn.Count == 0)
                            {
                                // Block first — register self as first target, wait for attack
                                status.WhoToAttackThisTurn.Add(player.GetPlayerId());
                                var macroBlockText = "Макро: Блок зарегистрирован. Выберите цель для нападения.\n";
                                status.AddInGamePersonalLogs(macroBlockText);
                                status.ChangeMindWhat = macroBlockText;
                                await _upd.UpdateMessage(player);
                                break;
                            }
                            else if (status.WhoToAttackThisTurn.Count == 1)
                            {
                                // Attack was first — block is second action, mark self as second target
                                status.WhoToAttackThisTurn.Add(player.GetPlayerId());
                                status.IsReady = true;
                                var macroBlockText2 = "Макро: Блок зарегистрирован как второе действие.\n";
                                status.AddInGamePersonalLogs(macroBlockText2);
                                status.ChangeMindWhat = macroBlockText2;
                                await _upd.UpdateMessage(player);
                                break;
                            }
                        }

                        status.IsBlock = true;
                        status.IsReady = true;

                        var text = "Вы поставили блок\n";
                        status.AddInGamePersonalLogs(text);
                        status.ChangeMindWhat = text;

                        if (player.IsSolo(game))
                            break;

                        await _upd.UpdateMessage(player);
                        break;
                    case "moral":
                        await HandleMoralForScore(player);
                        break;
                    case "skill":
                        await HandleMoralForSkill(player);
                        break;

                    case "lvl-up":
                        await HandleLvlUp(player, button);
                        await _upd.UpdateMessage(player);
                        break;
                    case "attack-select":
                        await HandleAttack(player, button);

                        if (player.IsSolo(game))
                            break;
                        await _upd.UpdateMessage(player);
                        break;

                    case "predict-1":
                        await HandlePredic1(player, button);
                        break;

                    case "predict-2":
                        await HandlePredic2(player, button);
                        break;

                    case "aram_reroll_1":
                        HandlePassiveRoll(player, 1, game);
                        await _upd.UpdateMessage(player);
                        break;
                    case "aram_reroll_2":
                        HandlePassiveRoll(player, 2, game);
                        await _upd.UpdateMessage(player);
                        break;
                    case "aram_reroll_3":
                        HandlePassiveRoll(player, 3, game);
                        await _upd.UpdateMessage(player);
                        break;
                    case "aram_reroll_4":
                        HandlePassiveRoll(player, 4, game);
                        await _upd.UpdateMessage(player);
                        break;
                    case "aram_reroll_5":
                        HandleBasicStatRoll(player);
                        await _upd.UpdateMessage(player);
                        break;
                    case "aram_roll_confirm":
                        player.Status.IsAramRollConfirmed  = true;
                        await _upd.UpdateMessage(player);
                        break;
                    case "draft_pick_0":
                    case "draft_pick_1":
                    case "draft_pick_2":
                        await HandleDraftPick(player, game, int.Parse(button.Data.CustomId.Split('_')[2]));
                        break;
                }
                
                return;
            }
    }

    public int GetRandomStat()
    {
        var n = _random.Random(1, 100);

        var statNumber = n switch
        {
            1 => 1,
            2 or 3 => 2,
            4 or 5 or 6 => 3,
            >= 7 and <= 16 => 4,
            >= 17 and <= 31 => 5,
            >= 32 and <= 51 => 6,
            >= 52 and <= 71 => 7,
            >= 72 and <= 86 => 8,
            >= 87 and <= 96 => 9,
            >= 97 => 10,
            _ => 0
        };

        return statNumber;
    }


    public void HandlePassiveRoll(GamePlayerBridgeClass player, int choice, GameClass game)
    {
        if (player.Status.AramRerolledPassivesTimes >= 4)
        {
            return;
        }
        player.Status.AramRerolledPassivesTimes++;

        var passives = _charactersPull.GetAramPassives();
        passives = passives.OrderBy(_ => Guid.NewGuid()).ToList();



        foreach (var pp in from p in game.PlayersList from passive in p.GameCharacter.Passive from pp in passives.ToList() where pp.PassiveName == passive.PassiveName select pp)
        {
            passives.Remove(pp);
        }

        var newPassive = passives[_random.Random(0, passives.Count-1)];
        player.GameCharacter.Passive[choice - 1] = newPassive;
    }

    public void HandleBasicStatRoll(GamePlayerBridgeClass player)
    {
        
        if (player.Status.AramRerolledStatsTimes >= 1)
        {
            return;
        }
        player.Status.AramRerolledStatsTimes++;

        var intelligence = GetRandomStat();
        var strength = GetRandomStat();
        var speed = GetRandomStat();
        var psyche = GetRandomStat();

        player.GameCharacter.SetIntelligence(intelligence, "Aram", false);
        player.GameCharacter.SetStrength(strength, "Aram", false);
        player.GameCharacter.SetSpeed(speed, "Aram", false);
        player.GameCharacter.SetPsyche(psyche, "Aram", false);
    }


    public async Task HandleDraftPick(GamePlayerBridgeClass player, GameClass game, int optionIndex)
    {
        if (!game.IsDraftPickPhase) return;
        if (player.Status.IsDraftPickConfirmed) return;
        if (!game.DraftOptions.TryGetValue(player.GetPlayerId(), out var options)) return;
        if (optionIndex < 0 || optionIndex >= options.Count) return;

        // Side characters (index > 0) cost 5 ZBS points
        var account = _accounts.GetAccount(player.DiscordId);
        if (optionIndex > 0)
        {
            if (account == null || account.ZbsPoints < 5) return;
            account.ZbsPoints -= 5;
        }

        var selected = options[optionIndex];

        // Replace the player's character with the selected one
        var newBridge = new GamePlayerBridgeClass(
            selected,
            new InGameStatus(),
            player.DiscordId,
            player.GameId,
            player.DiscordUsername,
            player.PlayerType
        );
        newBridge.IsWebPlayer = player.IsWebPlayer;
        newBridge.PreferWeb = player.PreferWeb;
        newBridge.TeamId = player.TeamId;
        newBridge.Predict = player.Predict;
        newBridge.DiscordStatus = player.DiscordStatus;
        newBridge.Status.IsDraftPickConfirmed = true;
        newBridge.Status.MoveListPage = 6;
        newBridge.CharacterMasteryPoints = account?.CharacterMastery.GetValueOrDefault(selected.Name, 0) ?? 0;

        // Replace in the players list
        var idx = game.PlayersList.IndexOf(player);
        if (idx >= 0)
            game.PlayersList[idx] = newBridge;

        // Update ExploitPlayersList so it references the new bridge
        var exploitIdx = game.ExploitPlayersList.IndexOf(player);
        if (exploitIdx >= 0 && selected.Passive.All(p => p.PassiveName != "Exploit"))
            game.ExploitPlayersList[exploitIdx] = newBridge;
        else if (exploitIdx >= 0)
            game.ExploitPlayersList.RemoveAt(exploitIdx);

        // Update account's last played character
        if (account != null)
            account.CharacterPlayedLastTime = selected.Name;

        // Only show "waiting for others" if not everyone has picked yet.
        // When all are confirmed, CheckIfReady handles the transition to avoid
        // a race condition where this UpdateMessage overwrites the game page.
        if (!game.PlayersList.All(x => x.Status.IsDraftPickConfirmed))
            await _upd.UpdateMessage(newBridge);
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

        var allCharacters = _charactersPull.GetVisibleCharacters().Select(x => x.Name).ToList();

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
        var predictedPlayer = game!.PlayersList.Find(x => x.DiscordUsername == predictedPlayerUsername);
        if (predictedPlayer == null) return;
        var predictedPlayerId = predictedPlayer.GetPlayerId();

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

        if (botChoice == -10)
        {
            var blockText = "Вы поставили блок\n";
            status.AddInGamePersonalLogs(blockText);
            status.ChangeMindWhat = blockText;
            status.IsBlock = true;
            status.IsReady = true;
            return true;
        }

            var game = _global.GamesList.Find(x => x.GameId == player.GameId);

            GamePlayerBridgeClass whoToAttack;
            if (!player.IsBot() && !player.Status.IsAutoMove)
            {
                var selectedId = Guid.Parse(string.Join("", button.Data.Values));
                whoToAttack = game!.PlayersList.Find(x => x.GetPlayerId() == selectedId);
            }
            else
            {
                whoToAttack = game!.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == botChoice);
            }

            if (whoToAttack == null) 
                return false;

            status.WhoToAttackThisTurn.Add(whoToAttack.GetPlayerId());


            //Клинки хаоса
            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Клинки хаоса") && game.RoundNo <= 10)
            {
                var targetPlace = whoToAttack.Status.GetPlaceAtLeaderBoard();
                var whoToAttack2 = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == targetPlace - 1);
                var whoToAttack3 = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == targetPlace + 1);

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
                await _help.SendMsgAndDeleteItAfterRound(player, "DeepList: Не нападай на хозяина, глупый пес!", 0);
                return false;
            }

            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "DeepList Pet") && whoToAttack.GameCharacter.Passive.Any(x => x.PassiveName == "Weedwick Pet"))
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await _help.SendMsgAndDeleteItAfterRound(player, "Не нападай на свою собаку. Ты чего, совсем уже ебнулся?", 0);
                return false;
            }
            // end Weedwick


            if (whoToAttack.GameCharacter.Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят") && game.RoundNo == 10)
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await _help.SendMsgAndDeleteItAfterRound(player, "Выбранный игрок недоступен в связи с баном за нарушение правил", 0);
                return false;
            }


            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "СОсиновый кол") && player.Status.WhoToLostEveryRound.Any(x => x.RoundNo == game.RoundNo - 1 &&  status.WhoToAttackThisTurn.Contains(x.EnemyId)))
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await game.Phrases.VampyrNoAttack.SendLogSeparate(player, false, 0);
                return false;
            }


            if (status.WhoToAttackThisTurn.Contains(player.GetPlayerId())
                && !player.GameCharacter.Passive.Any(x => x.PassiveName == "Макро"))
            {
                status.WhoToAttackThisTurn = new List<Guid>();
                await _help.SendMsgAndDeleteItAfterRound(player, "Зачем ты себя бьешь?", 0);
                return false;
            }

            // Dopa Макро — first attack doesn't set ready
            if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Макро")
                && status.WhoToAttackThisTurn.Count == 1)
            {
                var macroText = $"Макро: Первая цель - {whoToAttack.DiscordUsername}. Выберите вторую цель.\n";
                player.Status.AddInGamePersonalLogs(macroText);
                player.Status.ChangeMindWhat = macroText;
                return true;
            }
            // end Dopa Макро

            status.IsReady = true;
            status.IsBlock = false;
            var text = $"Вы напали на игрока {whoToAttack.DiscordUsername}\n";
            player.Status.AddInGamePersonalLogs(text);
            player.Status.ChangeMindWhat = text;
            return true;
    }

    //for GetLvlUp ONLY!
    public async Task LvlUp10(GamePlayerBridgeClass player)

    {
        await _help.SendMsgAndDeleteItAfterRound(player, "10 максимум, выбери другой стат", 0); //not awaited 
    }


    private async Task GetLvlUp(GamePlayerBridgeClass player, int skillNumber)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);

        // Стая Гоблинов — custom level-up
        if (player.GameCharacter.Name == "Стая Гоблинов")
        {
            var pop = player.Passives.GoblinPopulation;
            switch (skillNumber)
            {
                case 1: // Правильное питание - Hob rate: 15→14→13→12→11
                    if (pop.HobUpgradeLevel < 4)
                    {
                        pop.HobRate = 15 - 1 - pop.HobUpgradeLevel; // 14, 13, 12, 11
                        pop.HobUpgradeLevel++;
                        player.Status.AddInGamePersonalLogs($"Правильное питание! Хобы: каждый {pop.HobRate}й\n");
                    }
                    else
                    {
                        player.Status.AddInGamePersonalLogs("Правильное питание: Максимальный уровень!\n");
                    }
                    break;
                case 2: // Контрактная армия - Warrior rate: 6→5→4→3→2
                    if (pop.WarriorUpgradeLevel < 4)
                    {
                        pop.WarriorRate = 6 - 1 - pop.WarriorUpgradeLevel; // 5, 4, 3, 2
                        pop.WarriorUpgradeLevel++;
                        player.Status.AddInGamePersonalLogs($"Контрактная армия! Воины: каждый {pop.WarriorRate}й\n");
                    }
                    else
                    {
                        player.Status.AddInGamePersonalLogs("Контрактная армия: Максимальный уровень!\n");
                    }
                    break;
                case 3: // Трудовые условия - Worker rate: 10→9→8→7→6
                    if (pop.WorkerUpgradeLevel < 4)
                    {
                        pop.WorkerRate = 10 - 1 - pop.WorkerUpgradeLevel; // 9, 8, 7, 6
                        pop.WorkerUpgradeLevel++;
                        player.Status.AddInGamePersonalLogs($"Трудовые условия! Трудяги: каждый {pop.WorkerRate}й\n");
                    }
                    else
                    {
                        player.Status.AddInGamePersonalLogs("Трудовые условия: Максимальный уровень!\n");
                    }
                    break;
                case 4: // Праздник Гоблинов - double population (one-time only)
                    if (pop.FestivalUsed)
                    {
                        player.Status.AddInGamePersonalLogs("Праздник Гоблинов уже был!\n");
                        return;
                    }
                    pop.TotalGoblins *= 2;
                    pop.FestivalUsed = true;
                    player.Status.AddInGamePersonalLogs($"Праздник Гоблинов! Гоблинов: {pop.TotalGoblins}\n");
                    break;
            }
            player.Status.LvlUpPoints--;
            return;
        }
        //end Стая Гоблинов

        // Геральт — oil tier upgrade on level-up
        if (player.GameCharacter.Name == "Геральт")
        {
            var oil = player.Passives.GeraltOil;

            // First level-up: all tiers are 0 → give tier 1 to ALL types at once
            if (oil.DrownersOilTier == 0 && oil.WerewolvesOilTier == 0 && oil.VampiresOilTier == 0 && oil.DragonsOilTier == 0)
            {
                oil.DrownersOilTier = 1;
                oil.WerewolvesOilTier = 1;
                oil.VampiresOilTier = 1;
                oil.DragonsOilTier = 1;
                player.Status.AddInGamePersonalLogs("Масло от любой заразы!\n");
                player.Status.LvlUpPoints--;
                return;
            }

            Geralt.MonsterType monsterType;
            string typeName;
            int currentTier;
            switch (skillNumber)
            {
                case 1:
                    monsterType = Geralt.MonsterType.Утопцы;
                    if (oil.DrownersOilTier >= 3) { player.Status.AddInGamePersonalLogs("Масло против Утопцев уже максимального уровня!\n"); return; }
                    oil.DrownersOilTier++;
                    currentTier = oil.DrownersOilTier;
                    typeName = Geralt.GetMonsterTypeName(monsterType);
                    break;
                case 2:
                    monsterType = Geralt.MonsterType.Волколаки;
                    if (oil.WerewolvesOilTier >= 3) { player.Status.AddInGamePersonalLogs("Масло против Волколаков уже максимального уровня!\n"); return; }
                    oil.WerewolvesOilTier++;
                    currentTier = oil.WerewolvesOilTier;
                    typeName = Geralt.GetMonsterTypeName(monsterType);
                    break;
                case 3:
                    monsterType = Geralt.MonsterType.Вампиры;
                    if (oil.VampiresOilTier >= 3) { player.Status.AddInGamePersonalLogs("Масло против Вампиров уже максимального уровня!\n"); return; }
                    oil.VampiresOilTier++;
                    currentTier = oil.VampiresOilTier;
                    typeName = Geralt.GetMonsterTypeName(monsterType);
                    break;
                case 4:
                    monsterType = Geralt.MonsterType.Драконы;
                    if (oil.DragonsOilTier >= 3) { player.Status.AddInGamePersonalLogs("Масло против Драконов уже максимального уровня!\n"); return; }
                    oil.DragonsOilTier++;
                    currentTier = oil.DragonsOilTier;
                    typeName = Geralt.GetMonsterTypeName(monsterType);
                    break;
                default:
                    return;
            }
            var tierName = Geralt.OilClass.GetTierName(currentTier);
            player.Status.AddInGamePersonalLogs($"{tierName} против {typeName}!\n");
            player.Status.LvlUpPoints--;
            return;
        }
        //end Геральт

        // Котики — lvl-мяк: level up gives only +1 Justice
        if (player.GameCharacter.Name == "Котики")
        {
            player.GameCharacter.Justice.AddRealJusticeNow(1);
            game.Phrases.KotikiLevelUp.SendLog(player, false);
            player.Status.LvlUpPoints--;
            return;
        }
        //end Котики

        // TheBoys — Пацаны: +2 to chosen stat + upgrade team member
        if (player.GameCharacter.Name == "TheBoys")
        {
            switch (skillNumber)
            {
                case 1: // Intelligence → Франция хим.оружие
                    player.GameCharacter.AddIntelligence(2, "Пацаны");
                    player.Passives.TheBoysFrancie.ChemWeaponLevel++;
                    player.Status.AddInGamePersonalLogs($"Француз: Хим.оружие уровень {player.Passives.TheBoysFrancie.ChemWeaponLevel}\n");
                    break;
                case 2: // Strength → Бучер кочерга
                    player.GameCharacter.AddStrength(2, "Пацаны");
                    player.Passives.TheBoysButcher.PokerCount++;
                    player.Status.AddInGamePersonalLogs($"Бучер: Кочерга #{player.Passives.TheBoysButcher.PokerCount}\n");
                    break;
                case 3: // Speed → Кимико регенерация
                    player.GameCharacter.AddSpeed(2, "Пацаны");
                    player.Passives.TheBoysKimiko.RegenLevel++;
                    player.Passives.TheBoysKimiko.DisabledNextRound = false;
                    player.Passives.TheBoysKimiko.IsDisabled = false;
                    player.Status.AddInGamePersonalLogs($"Kimiko: Регенерация уровень {player.Passives.TheBoysKimiko.RegenLevel}\n");
                    break;
                case 4: // Psyche → М.М. компромат
                    player.GameCharacter.AddPsyche(2, "Пацаны");
                    player.Passives.TheBoysMM.NextAttackGathersKompromat = true;
                    player.Status.AddInGamePersonalLogs("М.М.: Следующая атака соберёт компромат!\n");
                    break;
            }
            player.Status.LvlUpPoints--;
            return;
        }
        //end TheBoys

        /*//Vampyr Позорный
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Vampyr Позорный"))
        {
            await game.Phrases.VampyrTheLoh.SendLogSeparate(player, true, 0);
            skillNumber = 0;
        }
        //end Vampyr Позорный*/
        var howMuchTooAdd = 1;
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
        {
            howMuchTooAdd = -1;
        }

        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Закуп"))
        {
            howMuchTooAdd = 10;
            game.Phrases.SellerZakup.SendLog(player, false);
        }

        switch (skillNumber)
        {
            case 1:
                // "Гигантские бобы" (Rick) can level INT past 10
                if (player.GameCharacter.GetIntelligence() >= 10 &&
                    !player.GameCharacter.Passive.Any(x => x.PassiveName == "Гигантские бобы") &&
                    (player.GameCharacter.GetPsyche() <= 9 || player.GameCharacter.GetStrength() <= 9 || player.GameCharacter.GetSpeed() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddIntelligence(howMuchTooAdd, "Прокачка", false);

                if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Одиночество"))
                {
                    player.Status.AddInGamePersonalLogs($"#life: Я прокачал Интеллект на {player.GameCharacter.GetIntelligence()}!\n");
                }
                else if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
                {
                    player.Status.AddInGamePersonalLogs($"Вам понерфали Интеллект до {player.GameCharacter.GetIntelligence()}!\n");
                    game.Phrases.YongGlebIrelia.SendLog(player, false, isRandomOrder: false, suffix: " -1 Интеллект");
                }
                else
                {
                    player.Status.AddInGamePersonalLogs($"Вы улучшили Интеллект до {player.GameCharacter.GetIntelligence()}\n");
                }

                // Sirinoks — intelligence reaches 10 before round 10
                if (player.GameCharacter.Name == "Sirinoks"
                    && player.GameCharacter.GetIntelligence() >= 10
                    && game != null && game.RoundNo < 10)
                {
                    game.Phrases.SirinoksGeniusPhrase.SendLog(player, false);
                }
                break;

            case 2:
                if (player.GameCharacter.GetStrength() >= 10 && 
                    (player.GameCharacter.GetPsyche() <= 9 || player.GameCharacter.GetIntelligence() <= 9 || player.GameCharacter.GetSpeed() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddStrength(howMuchTooAdd, "Прокачка", false);

                if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Одиночество"))
                {
                    player.Status.AddInGamePersonalLogs($"#life: Я прокачал Силу на {player.GameCharacter.GetStrength()}!\n");
                }
                else if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
                {
                    player.Status.AddInGamePersonalLogs($"Вам понерфали Силу до {player.GameCharacter.GetStrength()}!\n");
                    game.Phrases.YongGlebIrelia.SendLog(player, false, isRandomOrder: false, suffix: " -1 Сила");
                }
                else
                {
                    player.Status.AddInGamePersonalLogs($"Вы улучшили Силу до {player.GameCharacter.GetStrength()}\n");
                }
                break;

            case 3:
                if (player.GameCharacter.GetSpeed() >= 10 && 
                    (player.GameCharacter.GetPsyche() <= 9 || player.GameCharacter.GetStrength() <= 9 || player.GameCharacter.GetIntelligence() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddSpeed(howMuchTooAdd, "Прокачка", false);

                if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Одиночество"))
                {
                    player.Status.AddInGamePersonalLogs($"#life: Я прокачал Скорость на {player.GameCharacter.GetSpeed()}!\n");
                }
                else if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
                {
                    player.Status.AddInGamePersonalLogs($"Вам понерфали Скорость до {player.GameCharacter.GetSpeed()}!\n");
                    game.Phrases.YongGlebIrelia.SendLog(player, false, isRandomOrder: false, suffix: " -1 Скорость");
                }
                else
                {
                    player.Status.AddInGamePersonalLogs($"Вы улучшили Скорость до {player.GameCharacter.GetSpeed()}\n");
                }
                break;

            case 4:
                if (player.GameCharacter.GetPsyche() >= 10 && 
                    (player.GameCharacter.GetIntelligence() <= 9 || player.GameCharacter.GetStrength() <= 9 || player.GameCharacter.GetSpeed() <= 9))
                {
                    await LvlUp10(player);
                    return;
                }

                player.GameCharacter.AddPsyche(howMuchTooAdd, "Прокачка", false);

                if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Одиночество"))
                {
                    player.Status.AddInGamePersonalLogs($"#life: Я прокачал Психику на {player.GameCharacter.GetPsyche()}!\n");
                }
                else if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
                {
                    player.Status.AddInGamePersonalLogs($"Вам понерфали Психику до {player.GameCharacter.GetPsyche()}!\n");
                    game.Phrases.YongGlebIrelia.SendLog(player, false, isRandomOrder: false, suffix: " -1 Психика");
                }
                else
                {
                    player.Status.AddInGamePersonalLogs($"Вы улучшили Психику до {player.GameCharacter.GetPsyche()}\n");
                }

                // Gleb — psyche reaches 10
                if (player.GameCharacter.Name == "Глеб"
                    && player.GameCharacter.GetPsyche() >= 10
                    && game != null)
                {
                    game.Phrases.GlebPsyche10.SendLog(player, false);
                }
                break;
        }

        player.Status.LvlUpPoints--;

        // Итачи — next attack throws a crow after level-up
        if (player.GameCharacter.Name == "Итачи")
            player.Passives.ItachiCrows.CrowReadyToThrow = true;

        // Giant Beans — track base intelligence on INT upgrade
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Гигантские бобы") && skillNumber == 1)
        {
            var beans = player.Passives.RickGiantBeans;
            beans.BaseIntelligence++;
            if (beans.BeanStacks > 0)
            {
                var oldFake = beans.FakeIntelligence;
                beans.FakeIntelligence = beans.BaseIntelligence * beans.BeanStacks;
                player.GameCharacter.AddIntelligence(beans.FakeIntelligence - oldFake, "Гигантские бобы");
            }
        }

        // Portal Gun — check invention + grant charge on level-up
        // (placed before ingredient code to avoid game-null NRE blocking the check)
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Портальная пушка"))
        {
            var gun = player.Passives.RickPortalGun;
            if (!gun.Invented && player.GameCharacter.GetIntelligence() >= 30)
            {
                gun.Invented = true;
                game?.Phrases.RickPortalGunInvented.SendLog(player, false);
            }
            if (gun.Invented)
                gun.Charges++;
        }

        // Salldorum — Шэн: +1 charge on level-up
        if (player.GameCharacter.Name == "Salldorum")
        {
            player.Passives.SalldorumShen.Charges++;
            player.Status.AddInGamePersonalLogs($"Шэн: +1 заряд. Всего: {player.Passives.SalldorumShen.Charges}\n");
        }

        // Giant Beans — on each level-up, put ingredients on up to 3 enemies who don't have one yet
        if (game != null && player.GameCharacter.Passive.Any(x => x.PassiveName == "Гигантские бобы"))
        {
            var beans = player.Passives.RickGiantBeans;
            beans.IngredientsActive = true;

            // Remove targets that are no longer in the game
            var enemies = game.PlayersList
                .Where(x => x.GetPlayerId() != player.GetPlayerId())
                .ToList();
            beans.IngredientTargets.RemoveAll(id => enemies.All(e => e.GetPlayerId() != id));

            // Add ingredients on up to 3 enemies who don't currently have one
            var available = enemies
                .Where(e => !beans.IngredientTargets.Contains(e.GetPlayerId()))
                .ToList();

            var added = 0;
            while (added < 3 && available.Count > 0)
            {
                var idx = _random.Random(0, available.Count - 1);
                beans.IngredientTargets.Add(available[idx].GetPlayerId());
                available.RemoveAt(idx);
                added++;
            }

            if (added > 0)
                game.Phrases.RickGiantBeans.SendLog(player, false);
        }

        if (player.Status.LvlUpPoints > 0)
        {
            //Я пытаюсь!
            try
            {
                if (!player.IsBot())
                    await _help.SendMsgAndDeleteItAfterRound(player, $"Осталось еще {player.Status.LvlUpPoints} очков характеристик. Пытайся!", 0);
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
                player.GameCharacter.AddPsyche(-5, "Дизмораль");
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