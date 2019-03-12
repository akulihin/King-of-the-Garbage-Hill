using System.Threading.Tasks;
using System.Timers;
using King_of_the_Garbage_Hill.Game.DiscordMessages;

namespace King_of_the_Garbage_Hill.Game
{
    public class CheckIfReady : IServiceSingleton
    {
        public Timer LoopingTimer;
        private readonly Global _global;
        private readonly GameUpdateMess _upd;
        private readonly CalculateStage2 _stage2;
       

        public CheckIfReady(Global global, GameUpdateMess upd, CalculateStage2 stage2)
        {
            _global = global;
            _upd = upd;
            _stage2 = stage2;
            CheckTimer();
        }


        public  Task CheckTimer()
        {
            LoopingTimer = new Timer
            {
                AutoReset = true,
                Interval = 5000,
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
                var isTimerToCheckEnabled = _global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId)
                    .IsTimerToCheckEnabled;
                if (!isTimerToCheckEnabled){return;}

                var players = _global.GamesList[i].PlayersList;
                var readyTargetCount = players.Count;
                var readyCount = 0;

                for (var k = 0; k < players.Count; k++)
                {
              
              

                    if (players[k].Status.IsReady)
                    {
                        readyCount++;
                    }

                    if (players[k].Status.SocketMessageFromBot != null)
                    {
                        if (game.TurnLengthInSecond - game.TimePassed.Elapsed.TotalSeconds >= -6)
                        {
                            await _upd.UpdateMessage(players[k]);
                        }
                    }
                }

                if ((readyCount == readyTargetCount || game.TimePassed.Elapsed.TotalSeconds >=  game.TurnLengthInSecond) &&  game.GameStatus == 1)
                {
                    _global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId)
                        .IsTimerToCheckEnabled = false;


                    for (var k = 0; k < players.Count; k++)
                    {
              
     
                        if (players[k].Status.SocketMessageFromBot != null)
                        {
                                await _upd.UpdateMessage(players[k]);
                        }
                    }



                   await  _stage2.DeepListMind(game);
                  
                   _global.IsTimerToCheckEnabled.Find(x => x.GameId == game.GameId)
                       .IsTimerToCheckEnabled = true;

                }
            }
        }

        public Task InitializeAsync()
            => Task.CompletedTask;

        public class IsTimerToCheckEnabledClass
        {
            public bool IsTimerToCheckEnabled;
            public ulong GameId;

            public IsTimerToCheckEnabledClass(ulong gameId)
            {
                IsTimerToCheckEnabled = true;
                GameId = gameId;
            }
        }
    }
}