using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Mitsuki : IServiceSingleton
    {

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleMitsuki(GamePlayerBridgeClass player)
        {
        //    throw new System.NotImplementedException();
        }

        public void HandleMitsukiAfter(GamePlayerBridgeClass player)
        {
          //  throw new System.NotImplementedException();
        }

        public class GarbageClass
        {
            public ulong GameId;
            public ulong PlayerDiscordId;
            public List<ulong> Training = new List<ulong>();

            public GarbageClass(ulong playerDiscordId, ulong gameId, ulong enemyId)
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                Training.Add(enemyId);
            }
        }
    }
}
