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
        private readonly BotsBehavior _botsBehavior;
        private readonly FinishedGameLog _finishedGameLog;
        private readonly GameUpdateMess _gameUpdateMess;
        private readonly Global _global;
        private readonly CalculateRound _round;
        private readonly GameUpdateMess _upd;
        private readonly UserAccounts _accounts;
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
                Interval = 3000,
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
                var timer = _global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId);
                if(timer == null) return;
                
                var isTimerToCheckEnabled = timer.IsTimerToCheckEnabled;

                if (game.RoundNo > 10)
                {
                    game.WhoWon = game.PlayersList[0].DiscordAccount.DiscordId;
                    game.PreviousGameLogs +=
                        $"\n\n**{game.PlayersList[0].DiscordAccount.DiscordUserName}** победил, играя за **{game.PlayersList[0].Character.Name}**";
                    _finishedGameLog.CreateNewLog(game);

                    foreach (var player in game.PlayersList)
                    {
                        player.DiscordAccount.IsPlaying = false;
                        player.DiscordAccount.GameId = 1000000;
                        _accounts.SaveAccounts(player.DiscordAccount.DiscordId);
                        await _gameUpdateMess.UpdateMessage(player);
                        if (!player.IsBot())
                            await player.Status.SocketMessageFromBot.Channel.SendMessageAsync("ты кончил.");
                    }

                    _global.IsTimerToCheckEnabled.Remove(timer);
                    _global.GamesList.Remove(game);

                    Console.WriteLine("_______________________________________________");
                    return;
                }

                if (!isTimerToCheckEnabled) return;

                var players = _global.GamesList[i].PlayersList;
                var readyTargetCount = players.Count;
                var readyCount = 0;

                foreach (var t in players)
                {
                    await _botsBehavior.HandleBotBehavior(t, game);

                    //TODO: change game.TimePassed.Elapsed.TotalSeconds > 1 to 13
                    if (t.Status.IsReady && t.Status.MoveListPage != 3 && game.TimePassed.Elapsed.TotalSeconds > 1)
                        readyCount++;
                    else
                        Console.WriteLine("NOT READY: = " + t.DiscordAccount.DiscordUserName);

                    if (t.Status.SocketMessageFromBot == null) continue;
                    if (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds >= -6)
                        await _upd.UpdateMessage(t);
                }

                Console.WriteLine("readyCount = " + readyCount);

                if (readyCount != readyTargetCount &&
                    !(game.TimePassed.Elapsed.TotalSeconds >= game.TurnLengthInSecond) ||
                    game.GameStatus != 1) continue;

                timer.IsTimerToCheckEnabled = false;

                foreach (var t in players)
                    if (t.Status.SocketMessageFromBot != null)
                        await _upd.UpdateMessage(t);

                await _round.DeepListMind(game);

                timer.IsTimerToCheckEnabled = true;
            }
        }


        public class IsTimerToCheckEnabledClass
        {
            public ulong GameId;
            public bool IsTimerToCheckEnabled;

            public IsTimerToCheckEnabledClass(ulong gameId)
            {
                IsTimerToCheckEnabled = true;
                GameId = gameId;
            }
        }
    }
}