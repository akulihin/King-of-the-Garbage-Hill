using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Gleb : IServiceSingleton
    {
       
        private readonly SecureRandom _rand;
        private readonly InGameGlobal _gameGlobal;
        private readonly GameUpdateMess _upd;

        public Gleb(GameUpdateMess upd, SecureRandom rand, InGameGlobal gameGlobal)
        {
            _upd = upd;
            _rand = rand;
            _gameGlobal = gameGlobal;
        }
        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleGleb(GameBridgeClass player)
        {
           // throw new System.NotImplementedException();
        }

        public void HandleGlebAfter(GameBridgeClass player)
        {
            //skip check
           var skip = _gameGlobal.GlebSkipList.Find(x =>
                x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == player.DiscordAccount.GameId);
           if (skip != null)
           {
               player.Status.IsSkip = false;
               _gameGlobal.GlebSkipList.Remove(skip);
           }
        }

        public class GlebSkipClass
        {
            public ulong DiscordId;
            public ulong GameId;

            public GlebSkipClass(ulong discordId, ulong gameId)
            {
                DiscordId = discordId;
                GameId = gameId;

            }
        }
    }
}
