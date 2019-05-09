using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Tolya : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public Tolya(InGameGlobal inGameGlobal)
        {
            _gameGlobal = inGameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleTolya(GamePlayerBridgeClass player)
        {
 


        }

        public void HandleTolyaAfter(GamePlayerBridgeClass player, GameClass game)
        {
            if (player.Status.IsBlock && player.Status.IsWonThisCalculation != Guid.Empty)
                game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Status
                    .IsAbleToWin = true;

        }

        public class TolyaCountClass
        {
            public int Cooldown;
            public ulong GameId;
            public bool IsReadyToUse;
            public Guid PlayerId;
            public List<TolyaCountSubClass> TargetList   = new List<TolyaCountSubClass>();

            public TolyaCountClass(ulong gameId, Guid playerId)
            {
                GameId = gameId;
                PlayerId = playerId;
                IsReadyToUse = false;
                Cooldown = 2;
            }
        }

        public class TolyaCountSubClass
        {
            public int RoundNumber;
            public Guid Target;

            public TolyaCountSubClass(Guid target, int roundNumber)
            {
                Target = target;
                RoundNumber = roundNumber;
            }
        }
    }
}