using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Mitsuki : IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

 

        public void HandleMitsukiAfter(GamePlayerBridgeClass player)
        {
            //  throw new System.NotImplementedException();
        }

        public class GarbageClass
        {
            public ulong GameId;
            public Guid PlayerId;
            public List<Guid> Training = new();

            public GarbageClass(Guid playerId, ulong gameId, Guid enemyId)
            {
                PlayerId = playerId;
                GameId = gameId;
                Training.Add(enemyId);
            }
        }
    }
}