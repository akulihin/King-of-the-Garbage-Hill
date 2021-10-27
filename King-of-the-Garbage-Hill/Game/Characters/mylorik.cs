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
            //Месть
            //enemyIdLostTo may be 0
            var mylorik = _gameGlobal.MylorikRevenge.Find(x => x.GameId == gameId && x.PlayerId == player.Status.PlayerId);

            if (enemyIdLostTo != Guid.Empty)
            {
                //check if very first lost
                if (mylorik == null)
                {
                    _gameGlobal.MylorikRevenge.Add(new MylorikRevengeClass(player.Status.PlayerId, gameId, enemyIdLostTo, game.RoundNo));
                    game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                }
                else
                {
                    if (mylorik.EnemyListPlayerIds.All(x => x.EnemyPlayerId != enemyIdLostTo))
                    {
                        mylorik.EnemyListPlayerIds.Add(new MylorikRevengeClassSub(enemyIdLostTo, game.RoundNo));
                        game.Phrases.MylorikRevengeLostPhrase.SendLog(player, true);
                    }
                }
            }
            else
            {
                var find = mylorik?.EnemyListPlayerIds.Find(x => x.EnemyPlayerId == player.Status.IsWonThisCalculation && x.IsUnique);

                if (find != null && find.RoundNumber != game.RoundNo)
                {
                    player.Status.AddRegularPoints(2, "Месть");
                    player.Character.AddPsyche(player.Status, 1, "Месть: ");
                    find.IsUnique = false;
                    game.Phrases.MylorikRevengeVictoryPhrase.SendLog(player, true);
                }
            }
            //end //Месть
        }




        public void HandleMylorikAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Месть

            HandleMylorikRevenge(player, player.Status.IsLostThisCalculation, player.GameId, game);
            //end Месть

            //Испанец
            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                var rand = _rand.Random(1, 2);
                var boole = _gameGlobal.MylorikSpanish.Find(x => x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                if (rand == 1)
                {
                    boole.Times = 0;
                    player.Character.AddPsyche(player.Status, -1, "Испанец: ");
                    player.Character.AddExtraSkill(player.Status,  "Испанец: ", 5);
                    player.MinusPsycheLog(game);
                    game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                }
                else
                {
                    boole.Times++;

                    if (boole.Times == 2)
                    {
                        boole.Times = 0;
                        player.Character.AddPsyche(player.Status, -1, "Испанец: ");
                        player.Character.AddExtraSkill(player.Status, "Испанец: ", 5);
                        player.MinusPsycheLog(game);
                        game.Phrases.MylorikSpanishPhrase.SendLog(player, false);
                    }
                }
            }

            //end Испанец
        }

        public class MylorikRevengeClass
        {
            public List<MylorikRevengeClassSub> EnemyListPlayerIds;
            public ulong GameId;
            public Guid PlayerId;

            public MylorikRevengeClass(Guid playerId, ulong gameId, Guid firstLost, int roundNumber)
            {
                PlayerId = playerId;
                EnemyListPlayerIds = new List<MylorikRevengeClassSub> {new(firstLost, roundNumber) };
                GameId = gameId;
            }
        }

        public class MylorikRevengeClassSub
        {
            public Guid EnemyPlayerId;
            public bool IsUnique;
            public int RoundNumber;

            public MylorikRevengeClassSub(Guid enemyPlayerId, int roundNumber)
            {
                EnemyPlayerId = enemyPlayerId;
                RoundNumber = roundNumber;
                IsUnique = true;
            }
        }

        public class MylorikSpartanClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<MylorikSpartanSubClass> Enemies;

            public MylorikSpartanClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Enemies = new List<MylorikSpartanSubClass>();
            }
        }

        public class MylorikSpartanSubClass
        {
            public Guid EnemyId;
            public bool Active;


            public MylorikSpartanSubClass(Guid enemyId)
            {
                EnemyId = enemyId;
                Active = true;
            }
        }

        public class MylorikSpanishClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public int Times;

            public MylorikSpanishClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Times = 0;
            }
        }
    }
}