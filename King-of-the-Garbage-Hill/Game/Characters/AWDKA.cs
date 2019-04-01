using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Awdka : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public Awdka(InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleAwdka(GamePlayerBridgeClass player)
        {
          //  throw new System.NotImplementedException();
          
        }

        public void HandleAwdkaAfter(GamePlayerBridgeClass player)
        {

            if (player.Status.IsLostThisCalculation != 0)
            {
                var awdka = _gameGlobal.AwdkaTryingList.Find(x =>
                    x.GameId == player.DiscordAccount.GameId && x. PlayerDiscordId == player.DiscordAccount.DiscordId);

                if(awdka == null)
                {
                    _gameGlobal.AwdkaTryingList.Add(new TryingClass(player.DiscordAccount.DiscordId, player.DiscordAccount.GameId, player.Status.IsLostThisCalculation ));
                }
                else
                {
                    var enemy = awdka.TryingList.Find(x => x.EnemyId == player.Status.IsLostThisCalculation);
                    if (enemy == null)
                    {
                        awdka.TryingList.Add(new TryingSubClass(player.Status.IsLostThisCalculation));
                    }
                    else
                    {
                        enemy.Times++;
                    }
                 
                }
            }
            
        }

        public class TrollingClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public int Cooldown;

            public TrollingClass(ulong playerDiscordId, ulong gameId )
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                Cooldown = 2;
            }
        }



        public class TryingClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<TryingSubClass> TryingList = new List<TryingSubClass>();

            public TryingClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                TryingList.Add(new TryingSubClass(enemyId));
            }
        }

        public class TryingSubClass
        {
            public ulong EnemyId;
            public int Times;
            public bool IsUnique;

            public TryingSubClass(ulong enemyId)
            {
                EnemyId = enemyId;
                Times = 1;
                IsUnique = false;
            }
        }

    }
}
