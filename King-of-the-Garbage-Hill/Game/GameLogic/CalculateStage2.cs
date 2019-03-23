using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class CalculateStage2 : IServiceSingleton
    {
        private readonly CharacterPassives _characterPassives;
        private readonly InGameGlobal _gameGlobal;
        private readonly CharactersUniquePhrase _phrase;
        private readonly SecureRandom _rand;
        private readonly GameUpdateMess _upd;

        public CalculateStage2(GameUpdateMess upd, SecureRandom rand, CharacterPassives characterPassives,
            InGameGlobal gameGlobal, CharactersUniquePhrase phrase)
        {
            _upd = upd;
            _rand = rand;
            _characterPassives = characterPassives;
            _gameGlobal = gameGlobal;
            _phrase = phrase;
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public async Task DeepListMind(GameClass game)
        {
            Console.WriteLine($"calculating game #{game.GameId}...");
            var watch = new Stopwatch();
            watch.Start();

            game.TimePassed.Stop();
            game.GameStatus = 2;
            game.GameLogs += $"\n__**Раунд #{game.RoundNo}**__\n";
            game.PreviousGameLogs = $"\n__**Раунд #{game.RoundNo}**__\n";
            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                var pointsWined = 0;
                var player = game.PlayersList[i];

                await _characterPassives.HandleCharacterBeforeCalculations(player, game);


                if (player.Status.WhoToAttackThisTurn == 0 && player.Status.IsBlock == false)
                    player.Status.IsBlock = true;

                var randomForTooGood = 50;

                //if block => no one gets points, and no redundant playerAttacked variable
                if (player.Status.IsBlock)
                {
                    await _characterPassives.HandleCharacterAfterCalculations(player, game);
                    player.Status.IsWonLastTime = 0;
                    player.Status.IsLostLastTime = 0;
                    player.Status.IsFighting = 0;
                    continue;
                }

                var playerIamAttacking =
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.WhoToAttackThisTurn);


                playerIamAttacking.Status.IsFighting = player.DiscordAccount.DiscordId;
                player.Status.IsFighting = playerIamAttacking.DiscordAccount.DiscordId;

                await _characterPassives.HandleCharacterWithKnownEnemyBeforeCalculations(player, game);
                await _characterPassives.HandleCharacterWithKnownEnemyBeforeCalculations(playerIamAttacking, game);

                await _characterPassives.HandleCharacterBeforeCalculations(playerIamAttacking, game);

                if (playerIamAttacking.Status.WhoToAttackThisTurn == 0 && playerIamAttacking.Status.IsBlock == false)
                    playerIamAttacking.Status.IsBlock = true;

                //т.е. он получил урон, какие у него дебаффы на этот счет 
                await _characterPassives.HandleEveryAttackOnHim(playerIamAttacking, player, game);

                //т.е. я его аттакую, какие у меня бонусы на это
                await _characterPassives.HandleEveryAttackFromMe(player, playerIamAttacking, game);


                if (!player.Status.IsAbleToWin) pointsWined = -50;

                if (!playerIamAttacking.Status.IsAbleToWin) pointsWined = 50;


                game.GameLogs +=
                    $"{player.DiscordAccount.DiscordUserName} {new Emoji("<:war:557070460324675584>")} {playerIamAttacking.DiscordAccount.DiscordUserName}";
                game.PreviousGameLogs +=
                    $"{player.DiscordAccount.DiscordUserName} {new Emoji("<:war:557070460324675584>")} {playerIamAttacking.DiscordAccount.DiscordUserName}";

                //if block => no one gets points

                if (playerIamAttacking.Status.IsBlock && player.Status.IsAbleToWin)
                {
                    // var logMess =  await _characterPassives.HandleBlock(player, playerIamAttacking, game);

                    var logMess = " ⟶ *Бой не состоялся...*\n";

                    game.GameLogs += logMess;
                    game.PreviousGameLogs += logMess;


                    await _characterPassives.HandleCharacterAfterCalculations(player, game);
                    await _characterPassives.HandleCharacterAfterCalculations(playerIamAttacking, game);

                    player.Status.IsWonLastTime = 0;
                    player.Status.IsLostLastTime = 0;
                    playerIamAttacking.Status.IsWonLastTime = 0;
                    playerIamAttacking.Status.IsLostLastTime = 0;
                    playerIamAttacking.Status.IsFighting = 0;
                    player.Status.IsFighting = 0;

                    continue;
                }

                if (playerIamAttacking.Status.IsSkip)
                {
                    game.GameLogs += " ⟶ *Бой не состоялся...*\n";
                    game.PreviousGameLogs += " ⟶ *Бой не состоялся...*\n";

                    player.Character.Justice.JusticeForNextRound--;

                    await _characterPassives.HandleCharacterAfterCalculations(player, game);
                    await _characterPassives.HandleCharacterAfterCalculations(playerIamAttacking, game);

                    player.Status.IsWonLastTime = 0;
                    player.Status.IsLostLastTime = 0;
                    playerIamAttacking.Status.IsWonLastTime = 0;
                    playerIamAttacking.Status.IsLostLastTime = 0;
                    playerIamAttacking.Status.IsFighting = 0;
                    player.Status.IsFighting = 0;

                    continue;
                }

                //round 1 (contr)


                var whoIsBetter = WhoIsBetter(player, playerIamAttacking);

                //main formula:
                var a = player.Character;
                var b = playerIamAttacking.Character;

                var strangeNumber = a.Intelligence - b.Intelligence
                                    + a.Strength - b.Strength
                                    + a.Speed - b.Speed
                                    + a.Psyche - b.Psyche;

                var pd = a.Psyche - b.Psyche;

                if (pd > 0 && pd < 4)
                    strangeNumber += 1;
                else if (pd > 0 && pd >= 4)
                    strangeNumber += 2;
                else if (pd < 0 && pd > -4)
                    strangeNumber -= 1;
                else if (pd < 0 && pd <= -4)
                    strangeNumber -= 2;

                if (whoIsBetter == 1)
                    strangeNumber += 5;
                else if (whoIsBetter == 2)
                    strangeNumber -= 5;

                if (strangeNumber >= 14) randomForTooGood = 68;
                if (strangeNumber <= -14) randomForTooGood = 32;

                if (strangeNumber > 0) pointsWined++;
                if (strangeNumber < 0) pointsWined--;
                //end round 1


                //   var ggg = $"{player.Character.Name} vs {playerIamAttacking.Character.Name} = {strangeNumber}";
                //   Console.WriteLine(ggg);


                //round 2 (Justice)
                if (player.Character.Justice.JusticeNow > playerIamAttacking.Character.Justice.JusticeNow)
                    pointsWined++;
                if (player.Character.Justice.JusticeNow < playerIamAttacking.Character.Justice.JusticeNow)
                    pointsWined--;
                //end round 2

                //round 3 (Random)
                if (pointsWined == 0)
                {
                    var randomNumber = _rand.Random(1, 100);
                    if (randomNumber <= randomForTooGood) pointsWined++;
                }
                //end round 3

                //CheckIfWin to remove Justice
                if (pointsWined >= 1)
                {
                    game.GameLogs += $" ⟶ победил **{player.DiscordAccount.DiscordUserName}**\n";
                    game.PreviousGameLogs += $" ⟶ победил **{player.DiscordAccount.DiscordUserName}**\n";

                    //еврей
                    var point = _characterPassives.HandleJewPassive(player, game);
                    //end еврей

                    player.Status.AddRegularPoints(point.Result);

                    player.Status.WonTimes++;
                    player.Character.Justice.IsWonThisRound = true;

                    playerIamAttacking.Character.Justice.JusticeForNextRound++;

                    player.Status.IsWonLastTime = playerIamAttacking.DiscordAccount.DiscordId;
                    playerIamAttacking.Status.IsLostLastTime = player.DiscordAccount.DiscordId;
                }
                else
                {
                    //octopus  // playerIamAttacking is octopus
                    var check = _characterPassives.HandleOctopus(playerIamAttacking, player, game);
                    //end octopus

                    if (check)
                    {
                        game.GameLogs += $" ⟶ победил **{playerIamAttacking.DiscordAccount.DiscordUserName}**\n";
                        game.PreviousGameLogs +=
                            $" ⟶ победил **{playerIamAttacking.DiscordAccount.DiscordUserName}**\n";
                        playerIamAttacking.Status.AddRegularPoints();
                        player.Status.WonTimes++;
                        playerIamAttacking.Character.Justice.IsWonThisRound = true;

                        player.Character.Justice.JusticeForNextRound++;

                        playerIamAttacking.Status.IsWonLastTime = player.DiscordAccount.DiscordId;
                        player.Status.IsLostLastTime = playerIamAttacking.DiscordAccount.DiscordId;
                    }
                }

                //т.е. он получил урон, какие у него дебаффы на этот счет 
                await _characterPassives.HandleEveryAttackOnHimAfterCalculations(playerIamAttacking, player, game);
                //т.е. я его аттакую, какие у меня бонусы на это
                await _characterPassives.HandleEveryAttackFromMeAfterCalculations(player, playerIamAttacking, game);

                //TODO: merge top 2 methods and 2 below... they are the same... or no?

                await _characterPassives.HandleCharacterAfterCalculations(player, game);
                await _characterPassives.HandleCharacterAfterCalculations(playerIamAttacking, game);

                player.Status.IsWonLastTime = 0;
                player.Status.IsLostLastTime = 0;
                playerIamAttacking.Status.IsWonLastTime = 0;
                playerIamAttacking.Status.IsLostLastTime = 0;
                playerIamAttacking.Status.IsFighting = 0;
                player.Status.IsFighting = 0;
            }

            await _characterPassives.HandleEndOfRound(game);
            if (game.RoundNo % 2 == 0)
                game.TurnLengthInSecond += 10;
            else
                game.TurnLengthInSecond -= 10;

            if (game.RoundNo == 1) game.TurnLengthInSecond -= 75;


            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                var player = game.PlayersList[i];


                player.Status.IsBlock = false;
                player.Status.IsAbleToWin = true;
                player.Status.IsSkip = false;
                player.Status.IsAbleToTurn = true;
                player.Status.IsReady = false;
                player.Status.WhoToAttackThisTurn = 0;
                player.Status.CombineRoundScoreAndGameScore(game.RoundNo);


                player.Status.MoveListPage = 1;

                if (player.Character.Justice.IsWonThisRound) player.Character.Justice.JusticeNow = 0;

                player.Character.Justice.IsWonThisRound = false;
                player.Character.Justice.JusticeNow += player.Character.Justice.JusticeForNextRound;
                player.Character.Justice.JusticeForNextRound = 0;

                if (player.Character.Justice.JusticeNow > 5) player.Character.Justice.JusticeNow = 5;
            }


            game.GameStatus = 1;
            game.RoundNo++;
            await _characterPassives.HandleNextRound(game);


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
                    x.GameId == game.GameId && x.PlayerDiscordId == tigrTemp.DiscordAccount.DiscordId);

                if (tigr != null && tigr.TimeCount > 0)
                {
                    var tigrIndex = game.PlayersList.IndexOf(tigrTemp);

                    game.PlayersList[tigrIndex] = game.PlayersList[0];
                    game.PlayersList[0] = tigrTemp;
                    tigr.TimeCount--;
                    await _phrase.TigrTop.SendLog(tigrTemp);
                }
            }
            //end Tigr Unique
 
            //sort
            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                if (game.RoundNo == 3 || game.RoundNo == 5 || game.RoundNo == 7 || game.RoundNo == 9)
                    game.PlayersList[i].Status.MoveListPage = 3;
                game.PlayersList[i].Status.PlaceAtLeaderBoard = i + 1;
            }
            //end sorting

            SortGameLogs(game);
            await _characterPassives.HandleNextRoundAfterSorting(game);

            foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);

            game.TimePassed.Reset();
            game.TimePassed.Start();
            Console.WriteLine(
                $"Finished calculating game #{game.GameId} (round# {game.RoundNo}). || {watch.Elapsed.TotalSeconds}s");
            watch.Stop();
        }

        public int WhoIsBetter(GameBridgeClass player1, GameBridgeClass player2)
        {
            var p1 = player1.Character;
            var p2 = player2.Character;

            int intel = 0, speed = 0, str = 0;

            if (p1.Intelligence - p2.Intelligence > 0)
                intel = 1;
            if (p1.Intelligence - p2.Intelligence < 0)
                intel = -1;

            if (p1.Speed - p2.Speed > 0)
                speed = 1;
            if (p1.Speed - p2.Speed < 0)
                speed = -1;

            if (p1.Strength - p2.Strength > 0)
                str = 1;
            if (p1.Strength - p2.Strength < 0)
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
            var logsSplit = game.PreviousGameLogs.Split("\n").ToList();
            logsSplit.RemoveAll(x => x.Length <= 2);
            sortedGameLogs += $"{logsSplit[0]}\n";
            logsSplit.RemoveAt(0);

            for (var i = 0; i < logsSplit.Count; i++)
            {
                if (logsSplit[i].Contains("⟶")) continue;
                extraGameLogs += $"{logsSplit[i]}\n";
                logsSplit.RemoveAt(i);
            }


            foreach (var player in game.PlayersList)
                for (var i = 0; i < logsSplit.Count; i++)
                    if (logsSplit[i].Contains($"{player.DiscordAccount.DiscordUserName}"))
                    {
                        var fightLine = logsSplit[i];

                        var fightLineSplit = fightLine.Split("⟶");

                        var fightLineSplitSplit = fightLineSplit[0].Split("<:war:557070460324675584>");
                        fightLine = fightLineSplitSplit[0].Contains($"{player.DiscordAccount.DiscordUserName}")
                            ? $"**{fightLineSplitSplit[0]}** {new Emoji("<:war:557070460324675584>")} **{fightLineSplitSplit[1]}**"
                            : $"**{fightLineSplitSplit[1]}** {new Emoji("<:war:557070460324675584>")} **{fightLineSplitSplit[0]}**";

                        fightLine += $" ⟶ {fightLineSplit[1]}";

                        sortedGameLogs += $"{fightLine}\n";
                        logsSplit.RemoveAt(i);
                        i--;
                    }

            sortedGameLogs += extraGameLogs;
            game.PreviousGameLogs = sortedGameLogs;
        }
    }
}