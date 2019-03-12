using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;


namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Gleb : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public Gleb( InGameGlobal gameGlobal)
        {
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
