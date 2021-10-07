using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Shark : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;


        public Shark(InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleShark(GamePlayerBridgeClass player)
        {
            //  throw new System.NotImplementedException();
        }

        public void HandleSharkAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Челюсти: 
            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                var shark = _gameGlobal.SharkJawsWin.Find(x =>
                    x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);


                if (!shark.FriendList.Contains(player.Status.IsWonThisCalculation))
                {
                    shark.FriendList.Add(player.Status.IsWonThisCalculation);
                    player.Character.AddSpeed(player.Status, 1, "Челюсти: ");
                }
            }

            //end Челюсти: 
        }

        public class SharkLeaderClass
        {
            public List<int> FriendList = new();

            public SharkLeaderClass(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
            }

            public ulong GameId { get; set; }
            public Guid PlayerId { get; set; }
        }
    }
}