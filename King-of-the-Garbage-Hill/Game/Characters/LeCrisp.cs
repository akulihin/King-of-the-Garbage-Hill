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

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

 

        public void HandleLeCrispAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Импакт
            if (player.Status.IsLostThisCalculation != Guid.Empty)
            {
                var lePuska = _gameGlobal.LeCrispImpact.Find(x =>
                    x.PlayerId == player.Status.PlayerId && x.GameId == game.GameId);

                if (lePuska != null) lePuska.IsTriggered = true;
            }
            //Импакт
        }

        public class LeCrispImpactClass
        {
            public ulong GameId;
            public bool IsTriggered;
            public Guid PlayerId;

            public LeCrispImpactClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                IsTriggered = false;
            }
        }
    }
}