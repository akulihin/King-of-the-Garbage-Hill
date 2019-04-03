using System;
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

        public void HandleLeCrisp(GamePlayerBridgeClass player)
        {
          //  throw new System.NotImplementedException();
        }

        public void HandleLeCrispAfter(GamePlayerBridgeClass player, GameClass game)
        {
            player.Status.IsAbleToWin = true;

            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                if (lePuska != null)
                {
                    lePuska.IsTriggered = true;

                }
            }
        }

        public class LeCrispImpactClass
        {
            public Guid PlayerId;
            public ulong GameId;
            public bool IsTriggered;

            public LeCrispImpactClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                IsTriggered = false;
            }
        }
    }
}
