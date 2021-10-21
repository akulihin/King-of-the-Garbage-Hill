using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class HardKitty : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;
        

        public HardKitty(InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

    

        public void HandleHardKittyAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Doebatsya
            var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x =>
                x.GameId == player.GameId &&
                x.PlayerId == player.Status.PlayerId);
            //can be null

            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                if (hardKitty == null)
                {
                    _gameGlobal.HardKittyDoebatsya.Add(new DoebatsyaClass(
                        player.Status.PlayerId, player.GameId,
                        player.Status.IsLostThisCalculation));
                }
                else
                {
                    var exists =
                        hardKitty.LostSeries.Find(x => x.EnemyPlayerId == player.Status.IsLostThisCalculation);
                    if (exists == null)
                        hardKitty.LostSeries.Add(
                            new DoebatsyaSubClass(player.Status.IsLostThisCalculation));
                    else
                        exists.Series++;
                }

                return;
            }

            var wonPlayer =
                hardKitty?.LostSeries.Find(x => x.EnemyPlayerId == player.Status.IsWonThisCalculation);
            if (wonPlayer != null)
            {
                player.Status.AddRegularPoints(wonPlayer.Series, "Доебаться");
                if (wonPlayer.Series >= 2)
                {
                    var player2 = game.PlayersList.Find(x =>
                        x.Status.PlayerId == player.Status.IsWonThisCalculation);

                    player2.Character.AddPsyche(player2.Status, -1, "Доебаться: ");
                    player2.MinusPsycheLog(game);
                }

                wonPlayer.Series = 0;


                game.Phrases.HardKittyDoebatsyaPhrase.SendLog(player, false);
            }

            // end Doebatsya
        }

        public class DoebatsyaClass
        {
            public ulong GameId;
            public List<DoebatsyaSubClass> LostSeries = new();
            public Guid PlayerId;

            public DoebatsyaClass(Guid playerId, ulong gameId, Guid enemyId)
            {
                PlayerId = playerId;
                GameId = gameId;
                LostSeries.Add(new DoebatsyaSubClass(enemyId));
            }
        }

        public class DoebatsyaSubClass
        {
            public Guid EnemyPlayerId;
            public int Series;

            public DoebatsyaSubClass(Guid enemyPlayerId)
            {
                EnemyPlayerId = enemyPlayerId;
                Series = 1;
            }
        }


        public class MuteClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<Guid> UniquePlayers = new();

            public MuteClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }
        }

        public class LonelinessClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public bool Activated;

            public LonelinessClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Activated = false;
            }
        }
    }
}