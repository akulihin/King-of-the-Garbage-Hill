using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Awdka : IServiceSingleton
    {
     
        private readonly SecureRandom _rand;
        private readonly InGameGlobal _gameGlobal;
        private readonly GameUpdateMess _upd;

        public Awdka(GameUpdateMess upd, SecureRandom rand, InGameGlobal gameGlobal)
        {
            _upd = upd;
            _rand = rand;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleAWDKA(GameBridgeClass player)
        {
          //  throw new System.NotImplementedException();
        }

        public void HandleAWDKAAfter(GameBridgeClass player)
        {

            if (player.Status.IsLostLastTime != 0)
            {
                var awdka = _gameGlobal.AwdkaTryingList.Find(x =>
                    x.GameId == player.DiscordAccount.GameId && x. PlayerDiscordId == player.DiscordAccount.DiscordId);

                if(awdka == null)
                {
                    _gameGlobal.AwdkaTryingList.Add(new TryingClass(player.DiscordAccount.DiscordId, player.DiscordAccount.GameId, player.Status.IsLostLastTime ));
                }
                else
                {
                    var enemy = awdka.TryingList.Find(x => x.EnemyId == player.Status.IsLostLastTime);
                    enemy.Times++;
                }
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
