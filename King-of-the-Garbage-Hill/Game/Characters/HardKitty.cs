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
            //Доебаться
            var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x => x.PlayerId == player.Status.PlayerId && game.GameId == x.GameId);

            if (player.Status.WhoToAttackThisTurn != Guid.Empty)
            {
                if (player.Status.IsLostThisCalculation == player.Status.WhoToAttackThisTurn || player.Status.IsTargetBlocked == player.Status.WhoToAttackThisTurn || player.Status.IsTargetSkipped == player.Status.WhoToAttackThisTurn)
                {
                    var found = hardKitty.LostSeries.Find(x => x.EnemyPlayerId == player.Status.WhoToAttackThisTurn);

                    if (found != null)
                        found.Series++;
                    else
                    {
                        hardKitty.LostSeries.Add(new DoebatsyaSubClass(player.Status.WhoToAttackThisTurn));
                    }
                }
            }

            if (player.Status.IsWonThisCalculation != Guid.Empty && player.Status.IsWonThisCalculation == player.Status.WhoToAttackThisTurn)
            {
                var found = hardKitty.LostSeries.Find(x => x.EnemyPlayerId == player.Status.WhoToAttackThisTurn);
                if (found != null)
                {
                    if (found.Series > 0)
                    {
                        player.Status.AddRegularPoints(found.Series, "Доебаться", true);
                        game.Phrases.HardKittyDoebatsyaPhrase.SendLog(player, false);
                        found.Series = 0;
                    }
                }
            }
            //end Доебаться


        }

        public class DoebatsyaClass
        {
            public ulong GameId;
            public List<DoebatsyaSubClass> LostSeries = new();
            public Guid PlayerId;

            public DoebatsyaClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
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