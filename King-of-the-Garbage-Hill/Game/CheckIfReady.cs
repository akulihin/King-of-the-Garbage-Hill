using System.Threading.Tasks;
using System.Timers;

namespace King_of_the_Garbage_Hill.Game
{
    public class CheckIfReady : IServiceSingleton
    {
        public Timer LoopingTimer;
        private readonly Global _global;

        public CheckIfReady(Global global)
        {
            _global = global;
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

        public async void CheckIfEveryoneIsReady(object sender, ElapsedEventArgs e)
        {

            var games = _global.GamesList;
            for (var i = 0; i < games.Count; i++)
            {
                var game = games[i];
                var players = _global.GamesList[i].PlayersList;
                var readyTargetCount = players.Count;
                var readyCount = 0;
                for (var k = 0; k < players.Count; k++)
                {
                    if (players[i].IsReady)
                    {
                        readyCount++;
                    }
                }


                if ( (readyCount == readyTargetCount || game.TimePassed.Elapsed.TotalSeconds >=  game.TurnLengthInSecond) &&  game.GameStatus == 1)
                {

                 
                    //START COUNTING SHIT
                    game.GameStatus = 2;
                    game.TimePassed.Stop();
                    game.TimePassed.Reset();
                    game.TimePassed.Start();
                    //END COUNTING SHIT
                }

            }

            await Task.CompletedTask;
        }

        public Task InitializeAsync()
            => Task.CompletedTask;
    }
}