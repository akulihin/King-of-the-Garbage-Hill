using System;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Spartan : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public Spartan(InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }



        public void HandleSpartanAfter(GamePlayerBridgeClass player, GameClass game)
        {
            //Первая кровь: 
            var Spartan = _gameGlobal.SpartanFirstBlood.Find(x =>
                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

            if (Spartan.FriendList.Count == 1)
            {
                if (Spartan.FriendList.Contains(player.Status.IsWonThisCalculation))
                {
                    player.Character.AddSpeed(player.Status, 1, "Первая кровь: ");
                    game.AddGlobalLogs($"Они познают войну!\n");
                }
                else if (Spartan.FriendList.Contains(player.Status.IsLostThisCalculation))
                {
                    var ene = game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsLostThisCalculation);
                    ene.Character.AddSpeed(ene.Status, 1, "Первая кровь: ");
                }

                Spartan.FriendList.Add(Guid.Empty);
            }
            //end Первая кровь: 

            //Это привилегия
            if (player.Status.IsWonThisCalculation != Guid.Empty && game.RoundNo > 4)
            {
                game.PlayersList.Find(x => x.Status.PlayerId == player.Status.IsWonThisCalculation).Character
                    .Justice.AddJusticeForNextRound();
                player.Character.AddIntelligence(player.Status, -1, "Это привилегия: ");
            }
            //end Это привилегия

            //Им это не понравится: 
            Spartan = _gameGlobal.SpartanMark.Find(x =>
                x.GameId == game.GameId && x.PlayerId == player.Status.PlayerId);

            if (Spartan.FriendList.Contains(player.Status.IsWonThisCalculation)) player.Status.AddRegularPoints(1, "Им это не понравится");

            //end Им это не понравится: 
        }
    }
}