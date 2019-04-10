using System;
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

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleAwdka(GamePlayerBridgeClass player)
        {
            //  throw new System.NotImplementedException();
        }

        public void HandleAwdkaAfter(GamePlayerBridgeClass player, GameClass game)
        {


         
            //Произошел троллинг:


            if (player.Status.IsWonThisCalculation == Guid.Empty)
            {
                var awdka = _gameGlobal.AwdkaTrollingList.Find(x =>
                    x.GameId == player.DiscordAccount.GameId &&
                    x.PlayerId == player.Status.PlayerId);

                var enemy = awdka.EnemyList.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);

                if (enemy == null)
                    awdka.EnemyList.Add(new Awdka.TrollingSubClass(player.Status.IsWonThisCalculation,
                       game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Status.GetScore()));
                else
                    enemy.Score =  game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Status.GetScore();
            }

            //end Произошел троллинг:
          






            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                var awdka = _gameGlobal.AwdkaTryingList.Find(x =>
                    x.GameId == player.DiscordAccount.GameId && x.PlayerId == player.Status.PlayerId);

                if (awdka == null)
                {
                    _gameGlobal.AwdkaTryingList.Add(new TryingClass(player.Status.PlayerId,
                        player.DiscordAccount.GameId, player.Status.IsLostThisCalculation));
                }
                else
                {
                    var enemy = awdka.TryingList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);
                    if (enemy == null)
                        awdka.TryingList.Add(new TryingSubClass(player.Status.IsLostThisCalculation));
                    else
                        enemy.Times++;
                }
            }
        }

        public class TrollingClass
        {
            public List<TrollingSubClass> EnemyList = new List<TrollingSubClass>();

            public ulong GameId;
            public Guid PlayerId;

            public TrollingClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }

        public class TrollingSubClass
        {
            public Guid EnemyId;
            public int Score;

            public TrollingSubClass(Guid enemyId, int score)
            {
                EnemyId = enemyId;
                Score = score;
            }
        }


        public class TryingClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<TryingSubClass> TryingList = new List<TryingSubClass>();

            public TryingClass(Guid playerId, ulong gameId, Guid enemyPlayerId)
            {
                PlayerId = playerId;
                GameId = gameId;
                TryingList.Add(new TryingSubClass(enemyPlayerId));
            }
        }

        public class TryingSubClass
        {
            public Guid EnemyPlayerId;
            public bool IsUnique;
            public int Times;

            public TryingSubClass(Guid enemyPlayerId)
            {
                EnemyPlayerId = enemyPlayerId;
                Times = 1;
                IsUnique = false;
            }
        }
    }
}