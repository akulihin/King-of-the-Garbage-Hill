using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class CalculateRound : IServiceSingleton
{
    private readonly CharacterPassives _characterPassives;
    private readonly InGameGlobal _gameGlobal;
    private readonly Global _global;
    private readonly LoginFromConsole _logs;


    private readonly SecureRandom _rand;

    public CalculateRound(SecureRandom rand, CharacterPassives characterPassives,
        InGameGlobal gameGlobal, Global global, LoginFromConsole logs)
    {
        _rand = rand;
        _characterPassives = characterPassives;
        _gameGlobal = gameGlobal;
        _global = global;
        _logs = logs;
    }

    public async Task InitializeAsync()
    {
        await Task.CompletedTask;
    }

    /*
Intelligence => Speed
Strength => Intelligence
Speed => Strength
*/
    public string GetLostContrText(GamePlayerBridgeClass me, GamePlayerBridgeClass target)
    {
        if (me.Character.GetSkillClass() == "Интеллект" && target.Character.GetSkillClass() == "Скорость")
        {
            target.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(me.GetPlayerId(), "(**Умный** ?) "));

            return "вас обманул";
        }

        if (me.Character.GetSkillClass() == "Сила" && target.Character.GetSkillClass() == "Интеллект")
        {
            target.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(me.GetPlayerId(), "(**Сильный** ?) "));
            return "вас пресанул";
        }

        if (me.Character.GetSkillClass() == "Скорость" && target.Character.GetSkillClass() == "Сила")
        {
            target.Status.KnownPlayerClass.Add(new InGameStatus.KnownPlayerClassClass(me.GetPlayerId(), "(**Быстрый** ?) "));
            return "вас обогнал";
        }

        return "буль?";
    }


    public void ResetFight(GamePlayerBridgeClass player, GamePlayerBridgeClass playerIamAttacking = null)
    {
        player.Status.IsWonThisCalculation = Guid.Empty;
        player.Status.IsLostThisCalculation = Guid.Empty;
        player.Status.IsFighting = Guid.Empty;
        player.Status.IsTargetSkipped = Guid.Empty;
        player.Status.IsTargetBlocked = Guid.Empty;

        if (playerIamAttacking != null)
        {
            playerIamAttacking.Status.IsWonThisCalculation = Guid.Empty;
            playerIamAttacking.Status.IsLostThisCalculation = Guid.Empty;
            playerIamAttacking.Status.IsFighting = Guid.Empty;
            playerIamAttacking.Status.IsTargetSkipped = Guid.Empty;
            playerIamAttacking.Status.IsTargetBlocked = Guid.Empty;
        }
    }


    //пристрій судного дня
    public async Task CalculateAllFights(GameClass game)    
    {
        _logs.Critical("");
        _logs.Info($"calculating game #{game.GameId}, round #{game.RoundNo}");

        var watch = new Stopwatch();
        watch.Start();

        game.TimePassed.Stop();
        var roundNumber = game.RoundNo + 1;
        if (roundNumber > 10) roundNumber = 10;

        /*
        1-4 х1
        5-9 х2
        10  х4
         */

        game.SetGlobalLogs($"\n__**Раунд #{roundNumber}**__:\n\n");


        foreach (var player in game.PlayersList)
        {
            var pointsWined = 0;
            var randomForTooGood = 50;
            var isTooGoodMe = false;
            var isTooGoodEnemy = false;
            var isStatsBetterMe = false;
            var isStatsBettterEnemy = false;
            var isContrLost = 0;
            var isTooGoodLost = 0;
            var playerIamAttacking = game.PlayersList.Find(x => x.GetPlayerId() == player.Status.WhoToAttackThisTurn);


            //if block => no one gets points, and no redundant playerAttacked variable
            if (player.Status.IsBlock || player.Status.IsSkip)
            {
                _characterPassives.HandleCharacterAfterFight(player, game);
                ResetFight(player);
                continue;
            }

            if (playerIamAttacking == null)
            {
                await _global.Client.GetGuild(561282595799826432).GetTextChannel(935324189437624340).SendMessageAsync(
                    "playerIamAttacking == null\n" +
                    $"Round: {game.RoundNo}\n" +
                    $"{player.Status.CharacterName} | {player.DiscordUsername}\n");
                _characterPassives.HandleCharacterAfterFight(player, game);
                ResetFight(player);
                continue;
            }


            playerIamAttacking.Status.IsFighting = player.GetPlayerId();
            player.Status.IsFighting = playerIamAttacking.GetPlayerId();


            _characterPassives.HandleCharacterWithKnownEnemyBeforeFight(player, game);
            _characterPassives.HandleCharacterWithKnownEnemyBeforeFight(playerIamAttacking, game);
            _characterPassives.HandleDefenseBeforeFight(playerIamAttacking, player, game);
            _characterPassives.HandleAttackBeforeFight(player, playerIamAttacking, game);


            //умный
            if (player.Character.GetSkillClass() == "Интеллект" &&
                playerIamAttacking.Character.Justice.GetFullJusticeNow() == 0)
                player.Character.AddExtraSkill(player.Status, 6, "Класс");


            if (!player.Status.IsAbleToWin) pointsWined = -50;
            if (!playerIamAttacking.Status.IsAbleToWin) pointsWined = 50;


            game.AddGlobalLogs(
                $"{player.DiscordUsername} <:war:561287719838547981> {playerIamAttacking.DiscordUsername}",
                "");

            //add skill
            if (player.Character.GetCurrentSkillClassTarget() == playerIamAttacking.Character.GetSkillClass())
            {
                var text1 = "";
                var text2 = "";

                if (playerIamAttacking.Character.GetSkillClass() == "Интеллект")
                {
                    text1 = "**умного**";
                    text2 = "(**Умный** ?) ";
                }
                else if (playerIamAttacking.Character.GetSkillClass() == "Сила")
                {
                    text1 = "**сильного**";
                    text2 = "(**Сильный** ?) ";
                }
                else if (playerIamAttacking.Character.GetSkillClass() == "Скорость")
                {
                    text1 = "**быстрого**";
                    text2 = "(**Быстрый** ?) ";
                }
                else
                {
                    text1 = "**буля**";
                    text2 = "(**БУЛЬ** ?!) ";
                }


                //Претендент русского сервера
                if (player.Status.GetInGamePersonalLogs().Contains("Претендент русского сервера"))
                    player.Character.SetSkillMultiplier(2);
                //end Претендент русского сервера

                player.Character.AddMainSkill(player.Status, text1);

                //Претендент русского сервера
                if (player.Status.GetInGamePersonalLogs().Contains("Претендент русского сервера"))
                    player.Character.SetSkillMultiplier();
                //end Претендент русского сервера



                var known = player.Status.KnownPlayerClass.Find(x =>
                    x.EnemyId == playerIamAttacking.GetPlayerId());
                if (known != null)
                    player.Status.KnownPlayerClass.Remove(known);
                player.Status.KnownPlayerClass.Add(
                    new InGameStatus.KnownPlayerClassClass(playerIamAttacking.GetPlayerId(),
                        text2));
            }


            //check skill text
            switch (player.Character.GetCurrentSkillClassTarget())
            {
                case "Интеллект":
                    if (playerIamAttacking.Character.GetSkillClass() != "Интеллект")
                    {
                        var knownEnemy =
                            player.Status.KnownPlayerClass.Find(
                                x => x.EnemyId == playerIamAttacking.GetPlayerId());
                        if (knownEnemy != null)
                            if (knownEnemy.Text.Contains("Умный"))
                                player.Status.KnownPlayerClass.Remove(knownEnemy);
                    }

                    break;
                case "Сила":
                    if (playerIamAttacking.Character.GetSkillClass() != "Сила")
                    {
                        var knownEnemy =
                            player.Status.KnownPlayerClass.Find(
                                x => x.EnemyId == playerIamAttacking.GetPlayerId());
                        if (knownEnemy != null)
                            if (knownEnemy.Text.Contains("Сильный"))
                                player.Status.KnownPlayerClass.Remove(knownEnemy);
                    }

                    break;
                case "Скорость":
                    if (playerIamAttacking.Character.GetSkillClass() != "Скорость")
                    {
                        var knownEnemy =
                            player.Status.KnownPlayerClass.Find(
                                x => x.EnemyId == playerIamAttacking.GetPlayerId());
                        if (knownEnemy != null)
                            if (knownEnemy.Text.Contains("Быстрый"))
                                player.Status.KnownPlayerClass.Remove(knownEnemy);
                    }

                    break;
                case "Буль":
                    if (playerIamAttacking.Character.GetSkillClass() != "Буль")
                    {
                        var knownEnemy =
                            player.Status.KnownPlayerClass.Find(
                                x => x.EnemyId == playerIamAttacking.GetPlayerId());
                        if (knownEnemy != null)
                            if (knownEnemy.Text.Contains("БУЛЬ"))
                                player.Status.KnownPlayerClass.Remove(knownEnemy);
                    }

                    break;
            }

            //if block => no one gets points
            if (playerIamAttacking.Status.IsBlock && player.Status.IsAbleToWin)
            {
                player.Status.IsTargetBlocked = playerIamAttacking.GetPlayerId();
                // var logMess =  await _characterPassives.HandleBlock(player, playerIamAttacking, game);

                var logMess = " ⟶ *Бой не состоялся (Блок)...*";

                game.AddGlobalLogs(logMess);


                player.Status.AddBonusPoints(-1, "Блок");

                playerIamAttacking.Character.Justice.AddJusticeForNextRoundFromFight();

                _characterPassives.HandleCharacterAfterFight(player, game);
                _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game);
                _characterPassives.HandleDefenseAfterBlockOrFight(playerIamAttacking, player, game);

                ResetFight(player, playerIamAttacking);

                continue;
            }


            // if skip => something
            if (playerIamAttacking.Status.IsSkip)
            {
                player.Status.IsTargetSkipped = playerIamAttacking.GetPlayerId();
                game.SkipPlayersThisRound++;
                game.AddGlobalLogs(" ⟶ *Бой не состоялся (Скип)...*");

                _characterPassives.HandleCharacterAfterFight(player, game);
                _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game);

                ResetFight(player, playerIamAttacking);

                continue;
            }

            //round 1 (contr)


            //быстрый
            if (playerIamAttacking.Character.GetSkillClass() == "Скорость")
                playerIamAttacking.Character.AddExtraSkill(playerIamAttacking.Status, 2, "Класс");

            if (player.Character.GetSkillClass() == "Скорость")
                player.Character.AddExtraSkill(player.Status, 2, "Класс");


            //main formula:
            //main formula:
            var me = player.Character;
            var target = playerIamAttacking.Character;
            double weighingMachine = 0;

            var skillMultiplierMe = 1;
            var skillMultiplierTarget = 1;

            double contrMultiplier = 1;

            if (me.GetWhoIContre() == target.GetSkillClass())
            {
                contrMultiplier = 1.5;
                weighingMachine += 2;
                skillMultiplierMe = 2;
                isContrLost -= 1;
            }


            if (target.GetWhoIContre() == me.GetSkillClass())
            {
                weighingMachine -= 2;
                skillMultiplierTarget = 2;
                isContrLost += 1;
            }


            var scaleMe = me.ExtraWeight + me.GetIntelligence() + me.GetStrength() + me.GetSpeed() + me.GetPsyche() +
                          me.GetSkill() * skillMultiplierMe / 50;
            var scaleTarget = target.ExtraWeight + target.GetIntelligence() + target.GetStrength() + target.GetSpeed() +
                              target.GetPsyche() + target.GetSkill() * skillMultiplierTarget / 50;
            weighingMachine += scaleMe - scaleTarget;

            switch (WhoIsBetter(player, playerIamAttacking))
            {
                case 1:
                    weighingMachine += 5;
                    break;
                case 2:
                    weighingMachine -= 5;
                    break;
            }

            var psycheDifference = me.GetPsyche() - target.GetPsyche();
            switch (psycheDifference)
            {
                case > 0 and <= 3:
                    weighingMachine += 1;
                    break;
                case >= 4 and <= 5:
                    weighingMachine += 2;
                    break;
                case >= 6:
                    weighingMachine += 4;
                    break;
                case < 0 and >= -3:
                    weighingMachine -= 1;
                    break;
                case >= -5 and <= -4:
                    weighingMachine -= 2;
                    break;
                case <= -6:
                    weighingMachine -= 4;
                    break;
            }


            switch (weighingMachine)
            {
                case >= 13:
                    isTooGoodMe = true;
                    randomForTooGood = 75;
                    isTooGoodLost = 1;
                    break;
                case <= -13:
                    isTooGoodEnemy = true;
                    randomForTooGood = 25;
                    isTooGoodLost = -1;
                    break;
            }

            var myWtf = me.GetSkill() * skillMultiplierMe / 500 * contrMultiplier;
            var targetWtf = target.GetSkill() * skillMultiplierTarget / 500;
            var wtf = scaleMe * (1 + (myWtf - targetWtf)) - scaleMe;
            weighingMachine += wtf;


            weighingMachine += player.Character.Justice.GetFullJusticeNow() -
                               playerIamAttacking.Character.Justice.GetFullJusticeNow();


            switch (weighingMachine)
            {
                case > 0:
                    pointsWined++;
                    isContrLost -= 1;
                    isStatsBetterMe = true;
                    break;
                case < 0:
                    pointsWined--;
                    isContrLost += 1;
                    isStatsBettterEnemy = true;
                    break;
            }
            //end round 1


            //round 2 (Justice)
            if (player.Character.Justice.GetFullJusticeNow() > playerIamAttacking.Character.Justice.GetFullJusticeNow())
                pointsWined++;
            if (player.Character.Justice.GetFullJusticeNow() < playerIamAttacking.Character.Justice.GetFullJusticeNow())
                pointsWined--;
            //end round 2

            //round 3 (Random)
            if (pointsWined == 0)
            {
                var maxRandomNumber = 100;
                if (player.Character.Justice.GetFullJusticeNow() > 1 ||
                    playerIamAttacking.Character.Justice.GetFullJusticeNow() > 1)
                {
                    var myJustice = (int)(player.Character.Justice.GetFullJusticeNow() * contrMultiplier);
                    var targetJustice = playerIamAttacking.Character.Justice.GetFullJusticeNow();
                    maxRandomNumber -= (myJustice - targetJustice) * 5;
                }

                var randomNumber = _rand.Random(1, maxRandomNumber);
                if (randomNumber <= randomForTooGood) pointsWined++;
                else pointsWined--;
            }
            //end round 3


            var moral = player.Status.PlaceAtLeaderBoard - playerIamAttacking.Status.PlaceAtLeaderBoard;

            //octopus  // playerIamAttacking is octopus
            if (pointsWined <= 0) pointsWined =  await _characterPassives.HandleOctopus(playerIamAttacking, player, game);
            //end octopus


            //team
            var teamMate = false;
            if (game.Teams.Count > 0)
            {
                var playerTeam = game.Teams.Find(x => x.TeamPlayers.Contains(player.Status.PlayerId)).TeamId;
                var playerIamAttackingTeam = game.Teams.Find(x => x.TeamPlayers.Contains(playerIamAttacking.Status.PlayerId)).TeamId;
                if (playerTeam == playerIamAttackingTeam)
                {
                    teamMate = true;
                }
            }

            //CheckIfWin to remove Justice
            if (pointsWined >= 1)
            {
                var point = 1;
                //сильный
                if (player.Character.GetSkillClass() == "Сила")
                    player.Character.AddExtraSkill(player.Status, 4, "Класс");

                isContrLost -= 1;
                game.AddGlobalLogs($" ⟶ {player.DiscordUsername}");

                //еврей
                if (!teamMate)
                    point = await _characterPassives.HandleJews(player, game);
                if (point == 0) player.Status.AddInGamePersonalLogs("Евреи...\n");
                //end еврей


                //add regular points
                if (!teamMate) 
                    if (player.Character.Name == "HardKitty")
                    {
                        player.Status.AddRegularPoints(point*-1, "Победа");
                    }
                    else
                    {
                        player.Status.AddRegularPoints(point, "Победа");
                    }


                player.Status.WonTimes++;

                if(!teamMate)
                    player.Character.Justice.IsWonThisRound = true;


                if (player.Status.PlaceAtLeaderBoard > playerIamAttacking.Status.PlaceAtLeaderBoard && game.RoundNo > 1)
                {
                    if (!teamMate)
                    {
                        player.Character.AddMoral(player.Status, moral, "Победа");
                        playerIamAttacking.Character.AddMoral(playerIamAttacking.Status, moral * -1, "Поражение");
                    }
                }

                if (!teamMate)
                    playerIamAttacking.Character.Justice.AddJusticeForNextRoundFromFight();

                player.Status.IsWonThisCalculation = playerIamAttacking.GetPlayerId();
                playerIamAttacking.Status.IsLostThisCalculation = player.GetPlayerId();
                playerIamAttacking.Status.WhoToLostEveryRound.Add(new InGameStatus.WhoToLostPreviousRoundClass(
                    player.GetPlayerId(), game.RoundNo, isTooGoodMe, isStatsBetterMe, isTooGoodEnemy,
                    isStatsBettterEnemy, player.GetPlayerId()));

                //Quality
                var range = player.Character.GetSpeedQualityResistInt();
                if (playerIamAttacking.Character.GetSpeedQualityRangeBonus())
                    range -= 1;
                var placeDiff = player.Status.PlaceAtLeaderBoard - playerIamAttacking.Status.PlaceAtLeaderBoard;
                if (placeDiff < 0)
                    placeDiff *= -1;

                if (placeDiff <= range)
                {
                    playerIamAttacking.Character.LowerQualityResist(playerIamAttacking.DiscordUsername, game, playerIamAttacking.Status, 1, player.Character.GetStrengthQualityDropBonus());
                }
                
                //end Quality
            }
            else
            {
                //сильный
                if (playerIamAttacking.Character.GetSkillClass() == "Сила")
                    playerIamAttacking.Character.AddExtraSkill(playerIamAttacking.Status, 4, "Класс");

                if (isTooGoodLost == -1)
                    player.Status.AddInGamePersonalLogs(
                        $"{playerIamAttacking.DiscordUsername} is __TOO GOOD__ for you\n");

                isContrLost += 1;


                game.AddGlobalLogs($" ⟶ {playerIamAttacking.DiscordUsername}");

                if (!teamMate)
                    playerIamAttacking.Status.AddRegularPoints(1, "Победа");

                player.Status.WonTimes++;
                if (!teamMate)
                    playerIamAttacking.Character.Justice.IsWonThisRound = true;

                if (player.Status.PlaceAtLeaderBoard < playerIamAttacking.Status.PlaceAtLeaderBoard && game.RoundNo > 1)
                {
                    if (!teamMate)
                    {
                        player.Character.AddMoral(player.Status, moral, "Поражение");
                        playerIamAttacking.Character.AddMoral(playerIamAttacking.Status, moral * -1, "Победа");
                    }
                }

                if (playerIamAttacking.Character.Name == "Толя" && playerIamAttacking.Status.IsBlock)
                    if (!teamMate)
                        playerIamAttacking.Character.Justice.IsWonThisRound = false;

                if (!teamMate)
                    player.Character.Justice.AddJusticeForNextRoundFromFight();

                playerIamAttacking.Status.IsWonThisCalculation = player.GetPlayerId();
                player.Status.IsLostThisCalculation = playerIamAttacking.GetPlayerId();
                player.Status.WhoToLostEveryRound.Add(new InGameStatus.WhoToLostPreviousRoundClass(
                    playerIamAttacking.GetPlayerId(), game.RoundNo, isTooGoodEnemy, isStatsBettterEnemy, isTooGoodMe,
                    isStatsBetterMe, player.GetPlayerId()));
            }


            //т.е. он получил урон, какие у него дебаффы на этот счет 
            _characterPassives.HandleDefenseAfterFight(playerIamAttacking, player, game);
            _characterPassives.HandleDefenseAfterBlockOrFight(playerIamAttacking, player, game);

            //т.е. я его аттакую, какие у меня бонусы на это
            _characterPassives.HandleAttackAfterFight(player, playerIamAttacking, game);

            //TODO: merge top 2 methods and 2 below... they are the same... or no?


            switch (isContrLost)
            {
                case 3:
                    player.Status.AddInGamePersonalLogs(
                        $"Поражение: {playerIamAttacking.DiscordUsername} {GetLostContrText(playerIamAttacking, player)}\n");
                    break;
                case -3:
                    playerIamAttacking.Status.AddInGamePersonalLogs(
                        $"Поражение: {player.DiscordUsername} {GetLostContrText(player, playerIamAttacking)}\n");
                    break;
            }

            _characterPassives.HandleCharacterAfterFight(player, game);

            _characterPassives.HandleCharacterAfterFight(playerIamAttacking, game);
            _characterPassives.HandleShark(game); //used only for shark...

            ResetFight(player, playerIamAttacking);
        }


        _characterPassives.HandleEndOfRound(game);

        foreach (var player in game.PlayersList)
        {
            player.Status.TimesUpdated = 0;
            player.Status.IsAutoMove = false;
            player.Status.IsBlock = false;
            player.Status.IsAbleToWin = true;
            player.Status.IsSkip = false;
            player.Status.IsAbleToTurn = true;
            player.Status.IsReady = false;
            player.Status.WhoToAttackThisTurn = Guid.Empty;
            player.Status.MoveListPage = 1;
            player.Status.IsAbleToChangeMind = true;
            player.Status.RoundNumber = game.RoundNo+1;

            player.Character.SetSpeedResist();

            player.Character.Justice.HandleEndOfRoundJustice(player.Status);

            player.Status.CombineRoundScoreAndGameScore(game, _gameGlobal);
            player.Status.ClearInGamePersonalLogs();
            player.Status.InGamePersonalLogsAll += "|||";
        }

        game.SkipPlayersThisRound = 0;
        game.RoundNo++;


        await _characterPassives.HandleNextRound(game);

        //Handle Moral
        foreach (var p in game.PlayersList)
        {
            p.Status.AddBonusPoints(p.Character.GetBonusPointsFromMoral(), "Мораль");
            p.Character.SetBonusPointsFromMoral(0);
        }
        //end Moral

        game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();


        //Tigr Unique
        if (game.PlayersList.Any(x => x.Character.Name == "Тигр"))
        {
            var tigrTemp = game.PlayersList.Find(x => x.Character.Name == "Тигр");

            var tigr = _gameGlobal.TigrTop.Find(x => x.GameId == game.GameId && x.PlayerId == tigrTemp.GetPlayerId());

            if (tigr is { TimeCount: > 0 })
            {
                var tigrIndex = game.PlayersList.IndexOf(tigrTemp);

                game.PlayersList[tigrIndex] = game.PlayersList.First();
                game.PlayersList[0] = tigrTemp;
                tigr.TimeCount--;
                // game.Phrases.TigrTop.SendLog(tigrTemp);
            }
        }
        //end Tigr Unique


        //HardKitty unique
        if (game.PlayersList.Any(x => x.Character.Name == "HardKitty"))
        {
            var tempHard = game.PlayersList.Find(x => x.Character.Name == "HardKitty");
            var hardIndex = game.PlayersList.IndexOf(tempHard);

            for (var i = hardIndex; i < game.PlayersList.Count - 1; i++)
                game.PlayersList[i] = game.PlayersList[i + 1];

            game.PlayersList[^1] = tempHard;
        }
        //end //HardKitty unique


        //sort
        for (var i = 0; i < game.PlayersList.Count; i++)
        {
            if (game.RoundNo == 3 || game.RoundNo == 5 || game.RoundNo == 7 || game.RoundNo == 9)
                game.PlayersList[i].Status.MoveListPage = 3;
            game.PlayersList[i].Status.PlaceAtLeaderBoard = i + 1;
            game.PlayersList[i].Character.RollSkillTargetForNextRound();
            game.PlayersList[i].Status.PlaceAtLeaderBoardHistory.Add(new InGameStatus.PlaceAtLeaderBoardHistoryClass(game.RoundNo, game.PlayersList[i].Status.PlaceAtLeaderBoard));
        }
        //end sorting

        //Quality Drop
        var droppedPlayers = game.PlayersList.Where(x => x.Character.GetStrengthQualityDropDebuff() + 1 == game.RoundNo && x.Status.PlaceAtLeaderBoard != 6).OrderByDescending(x => x.Status.PlaceAtLeaderBoard).ToList();
        foreach (var player in droppedPlayers)
        {
            var oldIndex = game.PlayersList.IndexOf(player);
            var newIndex = oldIndex + 1;

            if(newIndex == 5 && game.PlayersList[newIndex].Character.Name == "HardKitty")
                continue;

            game.PlayersList[oldIndex] = game.PlayersList[newIndex];
            game.PlayersList[newIndex] = player;
        }

        if (droppedPlayers.Count > 0)
        {
            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                game.PlayersList[i].Status.PlaceAtLeaderBoard = i + 1;
            }
        }
        //end //Quality Drop

        SortGameLogs(game);
        _characterPassives.HandleNextRoundAfterSorting(game);
        _characterPassives.HandleBotPredict(game);
        game.TimePassed.Reset();
        game.TimePassed.Start();
        _logs.Info(
            $"Finished calculating game #{game.GameId} (round# {game.RoundNo - 1}). || {watch.Elapsed.TotalSeconds}s");
        _logs.Critical("");
        watch.Stop();
    }

    public void Move<T>(List<T> list, int oldIndex, int newIndex)
    {
        // exit if positions are equal or outside array
        if ((oldIndex == newIndex) || (0 > oldIndex) || (oldIndex >= list.Count) || (0 > newIndex) ||
            (newIndex >= list.Count)) return;
        // local variables
        var i = 0;
        T tmp = list[oldIndex];
        // move element down and shift other elements up
        if (oldIndex < newIndex)
        {
            for (i = oldIndex; i < newIndex; i++)
            {
                list[i] = list[i + 1];
            }
        }
        // move element up and shift other elements down
        else
        {
            for (i = oldIndex; i > newIndex; i--)
            {
                list[i] = list[i - 1];
            }
        }
        // put element from position 1 to destination
        list[newIndex] = tmp;
    }


    public int WhoIsBetter(GamePlayerBridgeClass meClass, GamePlayerBridgeClass targetClass)
    {
        var me = meClass.Character;
        var target = targetClass.Character;

        int intel = 0, speed = 0, str = 0;

        if (me.GetIntelligence() - target.GetIntelligence() > 0)
            intel = 1;
        if (me.GetIntelligence() - target.GetIntelligence() < 0)
            intel = -1;

        if (me.GetSpeed() - target.GetSpeed() > 0)
            speed = 1;
        if (me.GetSpeed() - target.GetSpeed() < 0)
            speed = -1;

        if (me.GetStrength() - target.GetStrength() > 0)
            str = 1;
        if (me.GetStrength() - target.GetStrength() < 0)
            str = -1;


        if (intel + speed + str >= 2)
            return 1;
        if (intel + speed + str <= -2)
            return 2;

        if (intel == 1 && str != -1)
            return 1;
        if (str == 1 && speed != -1)
            return 1;
        if (speed == 1 && intel != -1)
            return 1;

        return 2;
    }


    public void SortGameLogs(GameClass game)
    {
        var sortedGameLogs = "";
        var extraGameLogs = "\n";
        var logsSplit = game.GetGlobalLogs().Split("\n").ToList();
        logsSplit.RemoveAll(x => x.Length <= 2);
        sortedGameLogs += $"{logsSplit.First()}\n";
        logsSplit.RemoveAt(0);

        for (var i = 0; i < logsSplit.Count; i++)
        {
            if (logsSplit[i].Contains(":war:")) continue;
            extraGameLogs += $"{logsSplit[i]}\n";
            logsSplit.RemoveAt(i);
            i--;
        }


        foreach (var player in game.PlayersList)
            for (var i = 0; i < logsSplit.Count; i++)
                if (logsSplit[i].Contains($"{player.DiscordUsername}"))
                {
                    var fightLine = logsSplit[i];

                    var fightLineSplit = fightLine.Split("⟶");

                    var fightLineSplitSplit = fightLineSplit.First().Split("<:war:561287719838547981>");

                    fightLine = fightLineSplitSplit.First().Contains($"{player.DiscordUsername}")
                        ? $"{fightLineSplitSplit.First()} <:war:561287719838547981> {fightLineSplitSplit[1]}"
                        : $"{fightLineSplitSplit[1]} <:war:561287719838547981> {fightLineSplitSplit.First()}";


                    fightLine += $" ⟶ {fightLineSplit[1]}";

                    sortedGameLogs += $"{fightLine}\n";
                    logsSplit.RemoveAt(i);
                    i--;
                }

        sortedGameLogs += extraGameLogs;
        game.SetGlobalLogs(sortedGameLogs);
    }
}