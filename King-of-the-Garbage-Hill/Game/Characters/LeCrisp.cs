using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;


namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class LeCrisp : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public LeCrisp(InGameGlobal global)
        {
            _gameGlobal = global;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleLeCrisp(GameBridgeClass player)
        {
          //  throw new System.NotImplementedException();
        }

        public void HandleLeCrispAfter(GameBridgeClass player, GameClass game)
        {
            player.Status.IsAbleToWin = true;

            if (player.Status.IsLostLastTime != 0)
            {
                var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                    x.DiscordId == player.DiscordAccount.DiscordId && x.GameId == game.GameId);

                if (lePuska != null)
                {
                    lePuska.IsTriggered = true;

                }
            }
        }

        public class LeCrispImpactClass
        {
            public ulong DiscordId;
            public ulong GameId;
            public bool IsTriggered;

            public LeCrispImpactClass(ulong discordId, ulong gameId)
            {
                DiscordId = discordId;
                GameId = gameId;
                IsTriggered = false;
            }
        }
    }
}
