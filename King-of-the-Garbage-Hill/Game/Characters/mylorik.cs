using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Mylorik : IServiceSingleton
    {
     
        private readonly SecureRandom _rand;

        private readonly InGameGlobal _gameGlobal;

        public Mylorik(SecureRandom rand, InGameGlobal gameGlobal)
        {
            _rand = rand;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        
        public void HandleMylorikRevenge(GameBridgeClass player1, ulong player2Id, ulong gameId)
        {
            var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                x.GameId == gameId && x.PlayerDiscordId == player1.DiscordAccount.DiscordId);


            //check if very first lost
            if (mylorik == null && player1.Status.IsLostLastTime != 0)
            {
                _gameGlobal.MylorikRevenge.Add(new MylorikRevengeClass(player1.DiscordAccount.DiscordId, gameId,
                    player1.Status.IsLostLastTime));
                return;
            }

            //check if first lost to unique player
            if (mylorik != null)
            {
                if (mylorik.EnemyListDiscordId.All(x => x.EnemyDiscordId != player2Id))
                {
                    mylorik.EnemyListDiscordId.Add(new MylorikRevengeClassSub(player2Id));
                    return;
                }
            }

            //check if won to revenge
            var find = mylorik?.EnemyListDiscordId.Find(x =>
                x.EnemyDiscordId == player2Id && x.IsUnique);

            if (find != null)
            {
                player1.Status.AddRegularPoints(2);
                find.IsUnique = false;
            }
        }
        public async Task HandleMylorik(GameBridgeClass player, GameClass game)
        {
            //Boole


            await Task.CompletedTask;
            //end Boole
        }


        public void HandleMylorikAfter(GameBridgeClass player)
        {
            //Revenge
     
                HandleMylorikRevenge(player, player.Status.IsWonLastTime, player.DiscordAccount.GameId);
            //end Revenge

            //Spanish
            if (player.Status.IsLostLastTime != 0)
            {
                var rand = _rand.Random(1, 2);

                if (rand == 1) player.Character.Justice.JusticeForNextRound--;
            }

            //end Spanish
        }

        public class MylorikRevengeClass
        {
            public List<MylorikRevengeClassSub> EnemyListDiscordId;
            public ulong GameId;
            public ulong PlayerDiscordId;

            public MylorikRevengeClass(ulong playerDiscordId, ulong gameId, ulong firstLost)
            {
                PlayerDiscordId = playerDiscordId;
                EnemyListDiscordId = new List<MylorikRevengeClassSub> {new MylorikRevengeClassSub(firstLost)};
                GameId = gameId;
            }
        }

        public class MylorikRevengeClassSub
        {
            public ulong EnemyDiscordId;
            public bool IsUnique;

            public MylorikRevengeClassSub(ulong enemyDiscordId)
            {
                EnemyDiscordId = enemyDiscordId;
                IsUnique = true;
            }
        }

    }
}
