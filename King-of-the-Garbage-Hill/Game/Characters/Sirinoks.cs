using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;


namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Sirinoks : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public Sirinoks(InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleSirinoks(GameBridgeClass player)
        {
            //   throw new System.NotImplementedException();
        }

        public void HandleSirinoksAfter(GameBridgeClass player, GameClass game)
        {
            //обучение
            if (player.Status.IsLostLastTime != 0)
            {
                var playerSheLostLastTime = game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsLostLastTime);
                var intel = new List<StatsClass>
                {
                    new StatsClass(1, playerSheLostLastTime.Character.Intelligence),
                    new StatsClass(2, playerSheLostLastTime.Character.Strength),
                    new StatsClass(3, playerSheLostLastTime.Character.Speed),
                    new StatsClass(4, playerSheLostLastTime.Character.Psyche)
                };
                var best = intel.OrderByDescending(x => x.Number).ToList()[0];

                var siri = _gameGlobal.SirinoksTraining.Find(x =>
                    x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                if (siri == null)
                {
                    _gameGlobal.SirinoksTraining.Add(new TrainingClass(player.DiscordAccount.DiscordId, game.GameId, best.Index, best.Number));
                }
                else
                {
                    siri.Training.Add(new TrainingSubClass(best.Index, best.Number));
                }
            }
            //обучение end
        }

        public class StatsClass
        {
            public int Index;
            public int Number;

            public StatsClass(int index, int number)
            {
                Index = index;
                Number = number;
            }
        }

        public class FriendsClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<ulong> FriendList = new List<ulong>();

            public FriendsClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                FriendList.Add(enemyId);
            }

            
        }


        public class TrainingClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<TrainingSubClass> Training = new List<TrainingSubClass>();

            public TrainingClass(ulong playerDiscordId, ulong gameId, int index, int number)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                Training.Add(new TrainingSubClass(index, number));
            }
        }

        public class TrainingSubClass
        {
            public int StatIndex;
            public int StatNumber;
          

            public TrainingSubClass(int statIndex, int statNumber)
            {
                StatIndex = statIndex;
                StatNumber = statNumber;
               
            }
        }
    }
}
