using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class HardKitty : IServiceSingleton
    {

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleHardKitty(GamePlayerBridgeClass player)
        {
         //   throw new System.NotImplementedException();
        }

        public void HandleHardKittyAfter(GamePlayerBridgeClass player, GameClass game)
        {
          //

        }

        public class DoebatsyaClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<DoebatsyaSubClass> LostSeries = new List<DoebatsyaSubClass>();

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
            public List<Guid> UniquePlayers = new List<Guid>();

            public MuteClass(Guid playerId, ulong gameId, Guid enemyPlayerId)
            {
                PlayerId = playerId;
                GameId = gameId;
                UniquePlayers.Add(enemyPlayerId);
            }
        }


    }
}
