using System;
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
        private readonly InGameGlobal _gameGlobal;
        

        private readonly SecureRandom _rand;

        public Mylorik(SecureRandom rand, InGameGlobal gameGlobal)
        {
            _rand = rand;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public void HandleMylorikRevenge(GamePlayerBridgeClass player, Guid enemyIdLostTo, ulong gameId, GameClass game)
        {
            //enemyIdLostTo may be 0
            var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                x.GameId == gameId && x.PlayerId == player.Status.PlayerId);

            if (enemyIdLostTo != Guid.Empty)
            {
                //check if very first lost
                if (mylorik == null)
                {
                    _gameGlobal.MylorikRevenge.Add(new MylorikRevengeClass(player.Status.PlayerId, gameId,
                        enemyIdLostTo));
                    game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                }
                else
                {
                    if (mylorik.EnemyListPlayerIds.All(x => x.EnemyPlayerId != enemyIdLostTo))
                    {
                        mylorik.EnemyListPlayerIds.Add(new MylorikRevengeClassSub(enemyIdLostTo));
                        game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                    }
                }
            }
            else
            {
                var find = mylorik?.EnemyListPlayerIds.Find(x =>
                    x.EnemyPlayerId == player.Status.IsWonThisCalculation && x.IsUnique);

                if (find != null)
                {
                    player.Status.AddRegularPoints(2, "Месть");
                    player.Character.AddPsyche(player.Status, 1, "Месть: ");
                    find.IsUnique = false;
                    game.Phrases.MylorikRevengeVictoryPhrase.SendLog(player, true);
                }
            }
        }




        public void HandleMylorikAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Revenge

            HandleMylorikRevenge(player, player.Status.IsLostThisCalculation, player.GameId, game);
            //end Revenge

            //Spanish
            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                var rand = _rand.Random(1, 3);

                if (rand == 1)
                {
                    player.Character.AddPsyche(player.Status, -1, "Испанец: ");
                    player.MinusPsycheLog(game);
                    game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                }
            }

            //end Spanish
        }

        public class MylorikRevengeClass
        {
            public List<MylorikRevengeClassSub> EnemyListPlayerIds;
            public ulong GameId;
            public Guid PlayerId;

            public MylorikRevengeClass(Guid playerId, ulong gameId, Guid firstLost)
            {
                PlayerId = playerId;
                EnemyListPlayerIds = new List<MylorikRevengeClassSub> {new(firstLost)};
                GameId = gameId;
            }
        }

        public class MylorikRevengeClassSub
        {
            public Guid EnemyPlayerId;
            public bool IsUnique;

            public MylorikRevengeClassSub(Guid enemyPlayerId)
            {
                EnemyPlayerId = enemyPlayerId;
                IsUnique = true;
            }
        }
    }
}