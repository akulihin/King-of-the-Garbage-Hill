using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.FinishedGameLog;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class CheckIfReady : IServiceSingleton
    {
        private readonly UserAccounts _accounts;
        private readonly BotsBehavior _botsBehavior;
        private readonly FinishedGameLog _finishedGameLog;
        private readonly InGameGlobal _gameGlobal;
        private readonly GameUpdateMess _gameUpdateMess;
        private readonly Global _global;
        private readonly LoginFromConsole _logs;
        private readonly CalculateRound _round;
        private readonly GameUpdateMess _upd;
        public Timer LoopingTimer;
        private readonly HelperFunctions _help;

        public CheckIfReady(Global global, GameUpdateMess upd, CalculateRound round, FinishedGameLog finishedGameLog,
            GameUpdateMess gameUpdateMess, BotsBehavior botsBehavior, LoginFromConsole logs, UserAccounts accounts,
            InGameGlobal gameGlobal, HelperFunctions help)
        {
            _global = global;
            _upd = upd;
            _round = round;
            _finishedGameLog = finishedGameLog;
            _gameUpdateMess = gameUpdateMess;
            _botsBehavior = botsBehavior;
            _logs = logs;
            _accounts = accounts;
            _gameGlobal = gameGlobal;
            _help = help;
            CheckTimer();
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task CheckTimer()
        {
            LoopingTimer = new Timer
            {
                AutoReset = true,
                Interval = 7000,
                Enabled = true
            };

            LoopingTimer.Elapsed += CheckIfEveryoneIsReady;
            return Task.CompletedTask;
        }

        private async void CheckIfEveryoneIsReady(object sender, ElapsedEventArgs e)
        {
            var games = _global.GamesList;


            for (var i = 0; i < games.Count; i++)
            {
                var game = games[i];


                    

                if (game.RoundNo == 11)
                {

                    //predict
                    foreach (var player in from player in game.PlayersList from predict in player.Predict let enemy = game.PlayersList.Find(x => x.Status.PlayerId == predict.PlayerId) where enemy.Character.Name == predict.CharacterName select player)
                    {
                        player.Status.AddBonusPoints(4, "Предположение: ");
                    }

                    //

                    //sort
                    game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                    for (var k = 0; k < game.PlayersList.Count; k++)
                        game.PlayersList[k].Status.PlaceAtLeaderBoard = k + 1;
                    //end sorting


                    try
                    {
                        //case "AWDKA":
                        var AWDKA = game.PlayersList.Find(x => x.Character.Name == "AWDKA");
                        //trolling
                        if (AWDKA != null)
                        {
                            var awdkaTroll = _gameGlobal.AwdkaTrollingList.Find(x =>
                                x.GameId == AWDKA.GameId &&
                                x.PlayerId == AWDKA.Status.PlayerId);


                            var enemy = awdkaTroll.EnemyList.Find(x => x.EnemyId == game.PlayersList.Find(y => y.Status.PlaceAtLeaderBoard == 1).Status.PlayerId);

                            var trolledText = "";
                            if (enemy != null)
                            {
                                var tolled = game.PlayersList.Find(x => x.Status.PlayerId == enemy.EnemyId);

                                trolledText = tolled.Character.Name switch
                                {
                                    "DeepList" => "Лист Затроллился, хех",
                                    "mylorik" => "Лорик Затроллился, МММ!",
                                    "Глеб" => "Спящее Хуйло",
                                    "LeCrisp" => "ЛеПуська Затроллилась",
                                    "Толя" => "Раммус Продал Тормейл",
                                    "HardKitty" => "Пакет Молока Пролился На Клавиатуру",
                                    "Sirinoks" => "Айсик Затроллилась#",
                                    "Mit*suki*" => "МитСУКИ Затроллился",
                                    "AWDKA" => "AWDKA Затроллился сам по себе...",
                                    "Осьминожка" => "Осьминожка Забулькался",
                                    "Darksci" => "Даркси Не Повезло...",
                                    "Братишка" => "Братишка Забулькался",
                                    "Загадочный Спартанец в маске" => "Спатанец Затроллился!? А-я-йо...",
                                    "Вампур" => "ВампYр Затроллился",
                                    "Тигр" => "Тигр Обоссался, и кто теперь обоссан!?",
                                    _ => ""
                                };

                                AWDKA.Status.AddBonusPoints((enemy.Score + 1) / 2, $"**Произошел Троллинг:** {trolledText} ");
                                game.Phrases.AwdkaTrolling.SendLog(AWDKA, true);
                            }

                            //sort
                            game.PlayersList = game.PlayersList.OrderByDescending(x => x.Status.GetScore()).ToList();
                            for (var k = 0; k < game.PlayersList.Count; k++)
                                game.PlayersList[k].Status.PlaceAtLeaderBoard = k + 1;
                            //end sorting

                            if (enemy != null && game.PlayersList[0].Character.Name == "AWDKA")
                            {
                                game.AddGlobalLogs($"**Произошел Троллинг:** {trolledText} ");
                            }
                        }
                        //end //trolling
                    }

                    catch (Exception ex)
                    {
                        await _global.Client.GetUser(181514288278536193).CreateDMChannelAsync().Result
                            .SendMessageAsync("AWDKA trolling\n" +
                                              $"{ex.StackTrace}");
                    }



                    game.WhoWon = game.PlayersList[0].Status.PlayerId;
                    game.AddGlobalLogs(
                        game.PlayersList.FindAll(x => x.Status.GetScore() == game.PlayersList[0].Status.GetScore())
                            .Count > 1
                            ? "\n**Ничья**"
                            : $"\n**{game.PlayersList[0].DiscordUsername}** победил, играя за **{game.PlayersList[0].Character.Name}**");


                    _finishedGameLog.CreateNewLog(game);


                    foreach (var player in game.PlayersList)
                    {
                        await _gameUpdateMess.UpdateMessage(player);

                        var account = _accounts.GetAccount(player.DiscordId);
                        account.IsPlaying = false;
                        player.GameId = 1000000;


                        account.TotalPlays++;
                        account.TotalWins += player.Status.PlaceAtLeaderBoard == 1 ? 1 : (ulong) 0;
                        account.MatchHistory.Add(
                            new DiscordAccountClass.MatchHistoryClass(player.Character.Name, player.Status.GetScore(),
                                player.Status.PlaceAtLeaderBoard));
                        account.ZbsPoints += (player.Status.PlaceAtLeaderBoard - 6) * -1 + 1;
                        if (player.Status.PlaceAtLeaderBoard == 1)
                            account.ZbsPoints += 4;

                        var characterStatistics =
                            account.CharacterStatistics.Find(x =>
                                x.CharacterName == player.Character.Name);

                        if (characterStatistics == null)
                        {
                            account.CharacterStatistics.Add(
                                new DiscordAccountClass.CharacterStatisticsClass(player.Character.Name,
                                    player.Status.PlaceAtLeaderBoard));
                        }
                        else
                        {
                            characterStatistics.Plays++;
                            characterStatistics.Wins += player.Status.PlaceAtLeaderBoard == 1 ? 1 : (ulong) 0;
                        }

                        var performanceStatistics =
                            account.PerformanceStatistics.Find(x =>
                                x.Place == player.Status.PlaceAtLeaderBoard);

                        if (performanceStatistics == null)
                            account.PerformanceStatistics.Add(
                                new DiscordAccountClass.PerformanceStatisticsClass(player.Status.PlaceAtLeaderBoard));
                        else
                            performanceStatistics.Times++;
                        try
                        {
                            if (!player.IsBot())
                                await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("ты кончил.");
                        }
                        catch (Exception ee)
                        {
                            _logs.Critical(ee.StackTrace);
                        }
                    }

                    game.IsCheckIfReady = false;
                    _global.GamesList.Remove(game);

                    _logs.Critical("_______________________________________________");
                    continue;
                }

                if (!game.IsCheckIfReady) continue;

                var players = _global.GamesList[i].PlayersList;
                var readyTargetCount = players.Count;
                var readyCount = 0;

                _logs.Critical(" ");
                foreach (var t in players)
                {
                    await _botsBehavior.HandleBotBehavior(t, game);


                    //if (t.Status.IsReady && t.Status.MoveListPage != 3)
                    if (t.Status.IsReady && t.Status.MoveListPage != 3 && game.TimePassed.Elapsed.TotalSeconds > 13)
                        readyCount++;
                    else
                        _logs.Info("NOT READY: = " + t.DiscordUsername);

                    if (t.Status.SocketMessageFromBot == null) continue;
                    //   if (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds >= -6)
                    //       await _upd.UpdateMessage(t);
                }

                _logs.Info($"(#{game.GameId}) readyCount = " + readyCount);
                _logs.Critical(" ");

                if (readyCount != readyTargetCount &&
                    !(game.TimePassed.Elapsed.TotalSeconds >= game.TurnLengthInSecond) ||
                    game.GameStatus != 1) continue;

                //another way of protecting a frame perfect  bug
                if (!game.IsCheckIfReady) continue;

                game.IsCheckIfReady = false;

                foreach (var t in players.Where(t => t.Status.WhoToAttackThisTurn == Guid.Empty &&
                                                     t.Status.IsBlock == false && t.Status.IsSkip == false))
                    t.Status.IsBlock = true;


                foreach (var player in game.PlayersList)
                {
                    _help.DeleteItAfterRound(player);
                }

                await _round.CalculateAllFights(game);

                foreach (var t in players)
                    try
                    {
                        if (t.Status.SocketMessageFromBot != null)
                        {
                            await _upd.UpdateMessage(t);
                            if (game.RoundNo <= 10)
                                await _help.SendMsgAndDeleteItAfterRound(t, $"Раунд #{game.RoundNo}");
                            if (game.RoundNo == 8)
                                await _help.SendMsgAndDeleteItAfterRound(t, $"Это последний раунд, когда можно сделать **предложение**!");

                        }
                    }
                    catch (Exception f)
                    {
                        await _global.Client.GetUser(181514288278536193).CreateDMChannelAsync().Result
                            .SendMessageAsync("CheckIfEveryoneIsReady ==>  await _round.CalculateAllFights(game);\n" +
                                              $"{f.StackTrace}");
                    }

                game.IsCheckIfReady = true;
            }
        }
    }
}