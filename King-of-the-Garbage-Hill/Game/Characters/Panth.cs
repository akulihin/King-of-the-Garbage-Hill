using System;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Panth : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public Panth(InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandlePanth(GamePlayerBridgeClass player, GameClass game)
        {
            //   throw new System.NotImplementedException();
        }

        public void HandlePanthAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Первая кровь: 
            var panth = _gameGlobal.PanthFirstBlood.Find(x =>
                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

            if (panth != null && panth.FriendList.Count == 1)
            {
                if (panth.FriendList.Contains(player.Status.IsWonThisCalculation))
                {
                    player.Character.AddSpeed(player.Status);
                    panth.FriendList.Clear();
                }
                else if (panth.FriendList.Contains(player.Status.IsLostThisCalculation))
                {
                    var ene = game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsLostThisCalculation);
                    ene.Character.AddSpeed(ene.Status);
                    panth.FriendList.Clear();
                }

                panth.FriendList.Add(Guid.Empty);
            }
            //end Первая кровь: 

            //Это привилегия
            if (player.Status.IsWonThisCalculation != Guid.Empty)
            {
                game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Character
                    .Justice.AddJusticeForNextRound();
                player.Character.AddIntelligence(player.Status, -1);
            }
            //end Это привилегия

            //Им это не понравится: 
            panth = _gameGlobal.PanthMark.Find(x =>
                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

            if (panth.FriendList.Contains(player.Status.IsWonThisCalculation)) player.Status.AddRegularPoints();

            //end Им это не понравится: 
        }
    }
}