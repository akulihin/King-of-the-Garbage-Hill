using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class CalculateRound : IServiceSingleton
    {
        private readonly CharacterPassives _characterPassives;
        private readonly InGameGlobal _gameGlobal;
        private readonly Global _global;
        private readonly LoginFromConsole _logs;
        private readonly CharactersUniquePhrase _phrase;
        private readonly SecureRandom _rand;

        public CalculateRound(SecureRandom rand, CharacterPassives characterPassives,
            InGameGlobal gameGlobal, Global global, LoginFromConsole logs, CharactersUniquePhrase phrase)
        {
            _rand = rand;
            _characterPassives = characterPassives;
            _gameGlobal = gameGlobal;
            _global = global;
            _logs = logs;
            _phrase = phrase;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        //пристрій судного дня
        public async Task DeepListMind(GameClass game)
        {
            Console.WriteLine("");
            _logs.Info($"calculating game #{game.GameId}, round #{game.RoundNo}");

            var watch = new Stopwatch();
            watch.Start();

            game.TimePassed.Stop();
            game.GameStatus = 2;
            var roundNumber = game.RoundNo + 1;
            if (roundNumber > 10)
            {
                roundNumber = 10;
            }
            game.AddGameLogs($"\n__**Раунд #{roundNumber}**__\n");
            game.SetPreviousGameLogs($"\n__**Раунд #{game.RoundNo+1}**__\n");


            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                var pointsWined = 0;
                var player = game.PlayersList[i];
                await _characterPassives.HandleCharacterBeforeCalculations(player, game);


                if (player.Status.WhoToAttackThisTurn == Guid.Empty && player.Status.IsBlock == false)
                    player.Status.IsBlock = true;

                var randomForTooGood = 50;
                var isTooGoodPlayer = false;
                var isTooGoodEnemy = false;
                //if block => no one gets points, and no redundant playerAttacked variable
                if (player.Status.IsBlock)
                {
                    _characterPassives.HandleCharacterAfterCalculations(player, game);
                    player.Status.IsWonThisCalculation = Guid.Empty;
                    player.Status.IsLostThisCalculation = Guid.Empty;
                    player.Status.IsFighting = Guid.Empty;
                    continue;
                }

                var playerIamAttacking =
                    game.PlayersList.Find(x => x.Status.PlayerId == player.Status.WhoToAttackThisTurn);

                if (playerIamAttacking == null)
                {
                    var leftUser = "ERROR";

                    player.Status.AddRegularPoints();

                    await _global.Client.GetUser(181514288278536193).CreateDMChannelAsync().Result
                        .SendMessageAsync("/CalculateRound.cs:line 74 - ERROR\n" +
                                          $"left user id = {player.Status.WhoToAttackThisTurn}\n" +
                                          $"left user name = {leftUser}\n" +
                                          $"player.Character.Name =  {player.Character.Name}\n" +
                                          $"player.DiscordUserName = {player.DiscordUsername}");
                    continue;
                }


                playerIamAttacking.Status.IsFighting = player.Status.PlayerId;
                player.Status.IsFighting = playerIamAttacking.Status.PlayerId;


                await _characterPassives.HandleCharacterWithKnownEnemyBeforeCalculations(player, game);
                await _characterPassives.HandleCharacterWithKnownEnemyBeforeCalculations(playerIamAttacking, game);

                await _characterPassives.HandleCharacterBeforeCalculations(playerIamAttacking, game);

                if (playerIamAttacking.Status.WhoToAttackThisTurn == Guid.Empty &&
                    playerIamAttacking.Status.IsBlock == false)
                    playerIamAttacking.Status.IsBlock = true;


                //т.е. он получил урон, какие у него дебаффы на этот счет 
                await _characterPassives.HandleEveryAttackOnHim(playerIamAttacking, player, game);

                //т.е. я его аттакую, какие у меня бонусы на это
                await _characterPassives.HandleEveryAttackFromMe(player, playerIamAttacking, game);


                if (!player.Status.IsAbleToWin) pointsWined = -50;

                if (!playerIamAttacking.Status.IsAbleToWin) pointsWined = 50;


                game.AddPreviousGameLogs(
                    $"{player.DiscordUsername} <:war:561287719838547981> {playerIamAttacking.DiscordUsername}",
                    "");


                //if block => no one gets points
                if (playerIamAttacking.Status.IsBlock && player.Status.IsAbleToWin)
                {
                    // var logMess =  await _characterPassives.HandleBlock(player, playerIamAttacking, game);

                    var logMess = " ⟶ *Бой не состоялся...*";

                    game.AddPreviousGameLogs(logMess);

                    //Спарта - никогда не теряет справедливость, атакуя в блок.
                    if (player.Character.Name != "mylorik")
                    {
                        //end Спарта
                        player.Character.Justice.AddJusticeForNextRound(-1);
                        player.Status.AddBonusPoints(-1, "Блок: ");
                    }

                    playerIamAttacking.Character.Justice.AddJusticeForNextRound();

                    _characterPassives.HandleCharacterAfterCalculations(player, game);
                    _characterPassives.HandleCharacterAfterCalculations(playerIamAttacking, game);

                    player.Status.IsWonThisCalculation = Guid.Empty;
                    player.Status.IsLostThisCalculation = Guid.Empty;
                    playerIamAttacking.Status.IsWonThisCalculation = Guid.Empty;
                    playerIamAttacking.Status.IsLostThisCalculation = Guid.Empty;
                    playerIamAttacking.Status.IsFighting = Guid.Empty;
                    player.Status.IsFighting = Guid.Empty;

                    continue;
                }


                if (playerIamAttacking.Status.IsSkip)
                {
                    game.SkipPlayersThisRound++;
                    game.AddPreviousGameLogs(" ⟶ *Бой не состоялся...*");

                    _characterPassives.HandleCharacterAfterCalculations(player, game);
                    _characterPassives.HandleCharacterAfterCalculations(playerIamAttacking, game);

                    player.Status.IsWonThisCalculation = Guid.Empty;
                    player.Status.IsLostThisCalculation = Guid.Empty;
                    playerIamAttacking.Status.IsWonThisCalculation = Guid.Empty;
                    playerIamAttacking.Status.IsLostThisCalculation = Guid.Empty;
                    playerIamAttacking.Status.IsFighting = Guid.Empty;
                    player.Status.IsFighting = Guid.Empty;

                    continue;
                }

                //round 1 (contr)


                var whoIsBetter = WhoIsBetter(player, playerIamAttacking);

                //main formula:
                //main formula:
                var a = player.Character;
                var b = playerIamAttacking.Character;

                var scaleA = (a.GetIntelligence() + a.GetStrength() + a.GetSpeed() + a.GetPsyche()) + a.GetSkill() / 10;
                var scaleB = (b.GetIntelligence() + b.GetStrength() + b.GetSpeed() + b.GetPsyche()) + b.GetSkill() / 10;

                var weighingMachine = scaleA - scaleB;

                var psycheDifference = a.GetPsyche() - b.GetPsyche();

                if (psycheDifference > 0 && psycheDifference < 4)
                    weighingMachine += 1;
                else if (psycheDifference >= 4)
                    weighingMachine += 2;
                else if (psycheDifference < 0 && psycheDifference > -4)
                    weighingMachine -= 1;
                else if (psycheDifference < 0 && psycheDifference <= -4)
                    weighingMachine -= 2;

                if (whoIsBetter == 1)
                    weighingMachine += 5;
                else if (whoIsBetter == 2)
                    weighingMachine -= 5;

                if (weighingMachine >= 14)
                {
                    isTooGoodPlayer = true;
                    randomForTooGood = 68;
                }

                if (weighingMachine <= -14)
                {
                    isTooGoodEnemy = true;
                    randomForTooGood = 32;
                }


                var wtf = 1 + (a.GetSkill() / 200 - b.GetSkill() / 200);
                weighingMachine *= wtf;


                weighingMachine += player.Character.Justice.GetJusticeNow() - playerIamAttacking.Character.Justice.GetJusticeNow();


                if (weighingMachine > 0) pointsWined++;
                if (weighingMachine < 0) pointsWined--;
                //end round 1


                //round 2 (Justice)
                if (player.Character.Justice.GetJusticeNow() > playerIamAttacking.Character.Justice.GetJusticeNow())
                    pointsWined++;
                if (player.Character.Justice.GetJusticeNow() < playerIamAttacking.Character.Justice.GetJusticeNow())
                    pointsWined--;
                //end round 2

                //round 3 (Random)
                if (pointsWined == 0)
                {
                    var randomNumber = _rand.Random(1, 100 - (player.Character.Justice.GetJusticeNow() - playerIamAttacking.Character.Justice.GetJusticeNow()));
                    if (randomNumber <= randomForTooGood) pointsWined++;
                    else pointsWined--;
                }
                //end round 3


                var moral = player.Status.PlaceAtLeaderBoard - playerIamAttacking.Status.PlaceAtLeaderBoard;

                //CheckIfWin to remove Justice
                if (pointsWined >= 1)
                {
                    game.AddPreviousGameLogs($" ⟶ {player.DiscordUsername}");

                    //еврей
                    var point = _characterPassives.HandleJewPassive(player, game);

                    if (point.Result == 0) player.Status.AddInGamePersonalLogs("Евреи...\n");
                    //end еврей

                    //add regular points
                    player.Status.AddRegularPoints(point.Result);

                    //add skill
                    if (player.Status.WhoToAttackThisTurn == playerIamAttacking.Status.PlayerId)
                        switch (player.Character.GetCurrentSkillTarget())
                        {
                            case "Интеллект":
                                if (playerIamAttacking.Character.GetIntelligence() >= playerIamAttacking.Character.GetSpeed() && playerIamAttacking.Character.GetIntelligence() >= playerIamAttacking.Character.GetStrength())
                                    player.Character.AddSkill(player.Status, "**умного**");
                                break;
                            case "Сила":
                                if (playerIamAttacking.Character.GetStrength() >= playerIamAttacking.Character.GetSpeed() && playerIamAttacking.Character.GetStrength() >= playerIamAttacking.Character.GetIntelligence())
                                    player.Character.AddSkill(player.Status, "**сильного**");
                                break;
                            case "Скорость":
                                if (playerIamAttacking.Character.GetSpeed() >= playerIamAttacking.Character.GetStrength() && playerIamAttacking.Character.GetSpeed() >= playerIamAttacking.Character.GetIntelligence())
                                    player.Character.AddSkill(player.Status, "**быстрого**");
                                break;
                        }

                    player.Status.WonTimes++;
                    player.Character.Justice.IsWonThisRound = true;


                    if (player.Status.PlaceAtLeaderBoard > playerIamAttacking.Status.PlaceAtLeaderBoard && game.RoundNo > 1)
                    {
                        player.Character.AddMoral(player.Status, moral);
                        playerIamAttacking.Character.AddMoral(playerIamAttacking.Status, moral * -1);
                    }
                        

                    playerIamAttacking.Character.Justice.AddJusticeForNextRound();

                    player.Status.IsWonThisCalculation = playerIamAttacking.Status.PlayerId;
                    playerIamAttacking.Status.IsLostThisCalculation = player.Status.PlayerId;
                    playerIamAttacking.Status.WhoToLostEveryRound.Add(
                        new InGameStatus.WhoToLostPreviousRoundClass(player.Status.PlayerId, game.RoundNo,
                            isTooGoodPlayer));
                }
                else
                {
                    //octopus  // playerIamAttacking is octopus
                    var check = _characterPassives.HandleOctopus(playerIamAttacking, player, game);
                    //end octopus

                    if (check)
                    {
                        game.AddPreviousGameLogs($" ⟶ {playerIamAttacking.DiscordUsername}");

                        playerIamAttacking.Status.AddRegularPoints();

                        player.Status.WonTimes++;
                        playerIamAttacking.Character.Justice.IsWonThisRound = true;

                        if (player.Status.PlaceAtLeaderBoard < playerIamAttacking.Status.PlaceAtLeaderBoard && game.RoundNo > 1)
                        {
                            player.Character.AddMoral(player.Status, moral );
                            playerIamAttacking.Character.AddMoral(playerIamAttacking.Status, moral * -1);
                        }

                        if (playerIamAttacking.Character.Name == "Толя" && playerIamAttacking.Status.IsBlock)
                            playerIamAttacking.Character.Justice.IsWonThisRound = false;

                        player.Character.Justice.AddJusticeForNextRound();

                        playerIamAttacking.Status.IsWonThisCalculation = player.Status.PlayerId;
                        player.Status.IsLostThisCalculation = playerIamAttacking.Status.PlayerId;
                        player.Status.WhoToLostEveryRound.Add(
                            new InGameStatus.WhoToLostPreviousRoundClass(playerIamAttacking.Status.PlayerId,
                                game.RoundNo, isTooGoodEnemy));
                    }
                }


                //т.е. он получил урон, какие у него дебаффы на этот счет 
                await _characterPassives.HandleEveryAttackOnHimAfterCalculations(playerIamAttacking, player, game);

                //т.е. я его аттакую, какие у меня бонусы на это
                await _characterPassives.HandleEveryAttackFromMeAfterCalculations(player, playerIamAttacking, game);

                //TODO: merge top 2 methods and 2 below... they are the same... or no?


                _characterPassives.HandleCharacterAfterCalculations(player, game);

                _characterPassives.HandleCharacterAfterCalculations(playerIamAttacking, game);
                await _characterPassives.HandleEventsAfterEveryBattle(game); //used only for shark...

                player.Status.IsWonThisCalculation = Guid.Empty;
                player.Status.IsLostThisCalculation = Guid.Empty;
                playerIamAttacking.Status.IsWonThisCalculation = Guid.Empty;
                playerIamAttacking.Status.IsLostThisCalculation = Guid.Empty;
                playerIamAttacking.Status.IsFighting = Guid.Empty;
                player.Status.IsFighting = Guid.Empty;
            }


            await _characterPassives.HandleEndOfRound(game);
            if (game.RoundNo % 2 == 0)
                game.TurnLengthInSecond += 10;
            else
                game.TurnLengthInSecond -= 10;

            if (game.RoundNo == 1) game.TurnLengthInSecond -= 75;

            foreach (var player in game.PlayersList)
            {
                player.Status.IsBlock = false;
                player.Status.IsAbleToWin = true;
                player.Status.IsSkip = false;
                player.Status.IsAbleToTurn = true;
                player.Status.IsReady = false;
                player.Status.WhoToAttackThisTurn = Guid.Empty;
                player.Status.CombineRoundScoreAndGameScore(game, _gameGlobal, _phrase);
                player.Status.ClearInGamePersonalLogs();
                player.Status.InGamePersonalLogsAll += "|||";

                player.Status.MoveListPage = 1;

                if (player.Character.Justice.IsWonThisRound) player.Character.Justice.SetJusticeNow(0);

                player.Character.Justice.IsWonThisRound = false;
                player.Character.Justice.AddJusticeNow(player.Character.Justice.GetJusticeForNextRound());
                player.Character.Justice.SetJusticeForNextRound(0);
            }

            game.SkipPlayersThisRound = 0;
            game.RoundNo++;
            game.GameStatus = 1;


            await _characterPassives.HandleNextRound(game);

            //Handle Moral
            foreach (var p in game.PlayersList)
            {
                p.Status.AddBonusPoints(p.Character.GetBonusPointsFromMoral(), "Мораль: ");
                p.Character.SetBonusPointsFromMoral(0);
            }
            //end Moral

            game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();


            //HardKitty unique
            if (game.PlayersList.Any(x => x.Character.Name == "HardKitty"))
            {
                var tempHard = game.PlayersList.Find(x => x.Character.Name == "HardKitty");
                var hardIndex = game.PlayersList.IndexOf(tempHard);

                for (var i = hardIndex; i < game.PlayersList.Count - 1; i++)
                    game.PlayersList[i] = game.PlayersList[i + 1];

                game.PlayersList[game.PlayersList.Count - 1] = tempHard;
            }
            //end //HardKitty unique


            //Tigr Unique
            if (game.PlayersList.Any(x => x.Character.Name == "Тигр"))
            {
                var tigrTemp = game.PlayersList.Find(x => x.Character.Name == "Тигр");

                var tigr = _gameGlobal.TigrTop.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == tigrTemp.Status.PlayerId);

                if (tigr != null && tigr.TimeCount > 0)
                {
                    var tigrIndex = game.PlayersList.IndexOf(tigrTemp);

                    game.PlayersList[tigrIndex] = game.PlayersList[0];
                    game.PlayersList[0] = tigrTemp;
                    tigr.TimeCount--;
                    // await _phrase.TigrTop.SendLog(tigrTemp);
                }
            }
            //end Tigr Unique

            //sort
            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                if (game.RoundNo == 3 || game.RoundNo == 5 || game.RoundNo == 7 || game.RoundNo == 9)
                    game.PlayersList[i].Status.MoveListPage = 3;
                game.PlayersList[i].Status.PlaceAtLeaderBoard = i + 1;
                game.PlayersList[i].Character.RollCurrentSkillTarget();
            }
            //end sorting

            SortGameLogs(game);
            _logs.Info("Start HandleNextRoundAfterSorting");
            _characterPassives.HandleNextRoundAfterSorting(game);
            _logs.Info("Finished HandleNextRoundAfterSorting");

            game.TimePassed.Reset();
            game.TimePassed.Start();
            _logs.Info(
                $"Finished calculating game #{game.GameId} (round# {game.RoundNo - 1}). || {watch.Elapsed.TotalSeconds}s");
            Console.WriteLine("");
            watch.Stop();
            await Task.CompletedTask;
        }

        public int WhoIsBetter(GamePlayerBridgeClass player1, GamePlayerBridgeClass player2)
        {
            var p1 = player1.Character;
            var p2 = player2.Character;

            int intel = 0, speed = 0, str = 0;

            if (p1.GetIntelligence() - p2.GetIntelligence() > 0)
                intel = 1;
            if (p1.GetIntelligence() - p2.GetIntelligence() < 0)
                intel = -1;

            if (p1.GetSpeed() - p2.GetSpeed() > 0)
                speed = 1;
            if (p1.GetSpeed() - p2.GetSpeed() < 0)
                speed = -1;

            if (p1.GetStrength() - p2.GetStrength() > 0)
                str = 1;
            if (p1.GetStrength() - p2.GetStrength() < 0)
                str = -1;


            if (intel + speed + str >= 2)
                return 1;
            if (intel + speed + str <= -2)
                return 2;
            if (intel + speed + str == 0)
                return 0;


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
            var logsSplit = game.GetPreviousGameLogs().Split("\n").ToList();
            logsSplit.RemoveAll(x => x.Length <= 2);
            sortedGameLogs += $"{logsSplit[0]}\n";
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

                        var fightLineSplitSplit = fightLineSplit[0].Split("<:war:561287719838547981>");

                        fightLine = fightLineSplitSplit[0].Contains($"{player.DiscordUsername}")
                            ? $"{fightLineSplitSplit[0]} <:war:561287719838547981> {fightLineSplitSplit[1]}"
                            : $"{fightLineSplitSplit[1]} <:war:561287719838547981> {fightLineSplitSplit[0]}";


                        fightLine += $" ⟶ {fightLineSplit[1]}";

                        sortedGameLogs += $"{fightLine}\n";
                        logsSplit.RemoveAt(i);
                        i--;
                    }

            sortedGameLogs += extraGameLogs;
            game.SetPreviousGameLogs(sortedGameLogs);
        }
    }
}