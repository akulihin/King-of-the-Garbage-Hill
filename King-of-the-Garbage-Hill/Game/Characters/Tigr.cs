using System.Collections.Generic;
using System.Linq;
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

        public async Task HandleTigrAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //3-0 обоссан: 
            if (player.Status.IsWonThisCalculation != 0)
            {
                var tigr = _gameGlobal.TigrThreeZeroList.Find(x =>
                    x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);


                if (tigr == null)
                {
                    _gameGlobal.TigrThreeZeroList.Add(new ThreeZeroClass(player.DiscordAccount.DiscordId, game.GameId,
                        player.Status.IsWonThisCalculation));
                }
                else
                {
                    var enemy = tigr.FriendList.Find(x => x.EnemyId == player.Status.IsWonThisCalculation);
                    if (enemy != null)
                    {
                        enemy.WinsSeries++;

                        if (enemy.WinsSeries >= 3 && enemy.IsUnique)
                        {
                            player.Status.AddRegularPoints(2);



                            var enemyAcc = game.PlayersList.Find(x =>
                                x.DiscordAccount.DiscordId == player.Status.IsWonThisCalculation);

                            if (enemyAcc != null)
                            {
                                enemyAcc.Character.AddIntelligence(enemyAcc.Status, -1);

                                    enemyAcc.Character.AddPsyche(enemyAcc.Status, -1);
                                    enemyAcc.MinusPsycheLog(game);
                                    await _phrase.TigrThreeZero.SendLog(player);


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
                    x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                var enemy = tigr?.FriendList.Find(x => x.EnemyId == player.Status.IsLostThisCalculation);

                if (enemy != null && enemy.IsUnique) enemy.WinsSeries = 0;
            }

            //end 3-0 обоссан: 
        }


        public class TigrTopClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public int TimeCount;

            public TigrTopClass(ulong playerDiscordId, ulong gameId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                TimeCount = 2;
            }
        }

        public class ThreeZeroClass
        {
            public List<ThreeZeroSubClass> FriendList = new List<ThreeZeroSubClass>();
            public ulong GameId;
            public ulong PlayerDiscordId;

            public ThreeZeroClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                FriendList.Add(new ThreeZeroSubClass(enemyId));
            }
        }

        public class ThreeZeroSubClass
        {
            public ulong EnemyId;
            public bool IsUnique;
            public int WinsSeries;

            public ThreeZeroSubClass(ulong enemyId)
            {
                EnemyId = enemyId;
                WinsSeries = 1;
                IsUnique = true;
            }
        }
    }
}