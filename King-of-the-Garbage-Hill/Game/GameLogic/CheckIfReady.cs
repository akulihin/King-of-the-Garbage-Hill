using System;
using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.LocalPersistentData.FinishedGameLog;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class CheckIfReady : IServiceSingleton
    {
        private readonly UserAccounts _accounts;
        private readonly BotsBehavior _botsBehavior;
        private readonly FinishedGameLog _finishedGameLog;
        private readonly GameUpdateMess _gameUpdateMess;
        private readonly Global _global;
        private readonly CalculateRound _round;
        private readonly GameUpdateMess _upd;
        public Timer LoopingTimer;

        public CheckIfReady(Global global, GameUpdateMess upd, CalculateRound round, FinishedGameLog finishedGameLog,
            GameUpdateMess gameUpdateMess, BotsBehavior botsBehavior, UserAccounts accounts)
        {
            _global = global;
            _upd = upd;
            _round = round;
            _finishedGameLog = finishedGameLog;
            _gameUpdateMess = gameUpdateMess;
            _botsBehavior = botsBehavior;
            _accounts = accounts;
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


                if (game.RoundNo > 10)
                {
                    game.WhoWon = game.PlayersList[0].Status.PlayerId;
                    game.AddPreviousGameLogs(
                        $"\n**{game.PlayersList[0].DiscordAccount.DiscordUserName}** победил, играя за **{game.PlayersList[0].Character.Name}**");

                    _finishedGameLog.CreateNewLog(game);


                    foreach (var player in game.PlayersList)
                    {
                        await _gameUpdateMess.UpdateMessage(player, game);

                        player.DiscordAccount.IsPlaying = false;
                        player.DiscordAccount.GameId = 1000000;

                        if (!player.IsBot())
                            await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("ты кончил.");
                    }

                    game.IsCheckIfReady = false;
                    _global.GamesList.Remove(game);

                    Console.WriteLine("_______________________________________________");
                    continue;
                }

                if (!game.IsCheckIfReady) continue;

                var players = _global.GamesList[i].PlayersList;
                var readyTargetCount = players.Count;
                var readyCount = 0;

                foreach (var t in players)
                {
                    await _botsBehavior.HandleBotBehavior(t, game);

                    
                    if (t.Status.IsReady && t.Status.MoveListPage != 3 && game.TimePassed.Elapsed.TotalSeconds > 13)
                        readyCount++;
                    else
                        Console.WriteLine("NOT READY: = " + t.DiscordAccount.DiscordUserName);

                    if (t.Status.SocketMessageFromBot == null) continue;
                 //   if (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds >= -6)
                 //       await _upd.UpdateMessage(t);
                }

                Console.WriteLine($"(#{game.GameId}) readyCount = " + readyCount);

                if (readyCount != readyTargetCount &&
                    !(game.TimePassed.Elapsed.TotalSeconds >= game.TurnLengthInSecond) ||
                    game.GameStatus != 1) continue;

                //another way of protecting a frame perfect  bug
                if (!game.IsCheckIfReady) continue;

                game.IsCheckIfReady = false;

                /*
                Parallel.ForEach(players, async t =>
                {
                    if (t.Status.SocketMessageFromBot != null)
                         await _upd.UpdateMessage(t);
                });
                */
                try
                {
                    await _round.DeepListMind(game);

                    Parallel.ForEach(players, async t =>
                    {
                        if (t.Status.SocketMessageFromBot != null)
                        {
                            await _upd.UpdateMessage(t);
                            await _upd.SendMsgAndDeleteIt(t, $"Раунд #{game.RoundNo}", 3);
                        }
                    });
                }
                catch (Exception f)
                {
                 await   _global.Client.GetUser(181514288278536193).GetOrCreateDMChannelAsync().Result
                        .SendMessageAsync("CheckIfEveryoneIsReady ==>  await _round.DeepListMind(game);\n" +
                                          $"{f.StackTrace}");
                }

                game.IsCheckIfReady = true;
            }
        }
    }
}