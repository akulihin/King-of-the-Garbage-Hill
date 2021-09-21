using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Tigr : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        private readonly CharactersUniquePhrase _phrase;

        public Tigr(InGameGlobal gameGlobal,
            CharactersUniquePhrase phrase)
        {
            _gameGlobal = gameGlobal;

            _phrase = phrase;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleTigr(GamePlayerBridgeClass player)
        {
            //   throw new System.NotImplementedException();
        }

        public void HandleTigrAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //3-0 обоссан: 
            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);


                if (tigr == null)
                {
                    _gameGlobal.TigrThreeZeroList.Add(new ThreeZeroClass(player.Status.PlayerId, game.GameId,
                        player.Status.IsWonThisCalculation));
                }
                else
                {
                    var enemy = tigr.FriendList.Find(x => x.EnemyPlayerId == player.Status.IsWonThisCalculation);
                    if (enemy != null)
                    {
                        enemy.WinsSeries++;

                        if (enemy.WinsSeries >= 3 && enemy.IsUnique)
                        {
                            player.Status.AddRegularPoints(2);


                            var enemyAcc = game.PlayersList.Find(x =>
                                x.Status.PlayerId == player.Status.IsWonThisCalculation);

                            if (enemyAcc != null)
                            {
                                enemyAcc.Character.AddIntelligence(enemyAcc.Status, -1, true, "3-0 обоссан: ");

                                enemyAcc.Character.AddPsyche(enemyAcc.Status, -1, true, "3-0 обоссан: ");
                                enemyAcc.MinusPsycheLog(game);
                                _phrase.TigrThreeZero.SendLog(player);


                                enemy.IsUnique = false;
                            }
                        }
                    }
                    else
                    {
                        tigr.FriendList.Add(new ThreeZeroSubClass(player.Status.IsWonThisCalculation));
                    }
                }
            }
            else
            {
                var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

                var enemy = tigr?.FriendList.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);

                if (enemy != null && enemy.IsUnique) enemy.WinsSeries = 0;
            }

            //end 3-0 обоссан: 
        }


        public class TigrTopClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public int TimeCount;

            public TigrTopClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                TimeCount = 3;
            }
        }

        public class ThreeZeroClass
        {
            public List<ThreeZeroSubClass> FriendList = new();
            public ulong GameId;
            public Guid PlayerId;

            public ThreeZeroClass(Guid playerId, ulong gameId, Guid enemyId)
            {
                PlayerId = playerId;
                GameId = gameId;
                FriendList.Add(new ThreeZeroSubClass(enemyId));
            }
        }

        public class ThreeZeroSubClass
        {
            public Guid EnemyPlayerId;
            public bool IsUnique;
            public int WinsSeries;

            public ThreeZeroSubClass(Guid enemyPlayerId)
            {
                EnemyPlayerId = enemyPlayerId;
                WinsSeries = 1;
                IsUnique = true;
            }
        }
    }
}