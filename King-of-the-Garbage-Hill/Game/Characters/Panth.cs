using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Panth : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        public Panth( InGameGlobal gameGlobal)
        {
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandlePanth(GameBridgeClass player)
        {
            //   throw new System.NotImplementedException();
        }

        public void HandlePanthAfter(GameBridgeClass player, GameClass game)
        {
            //Первая кровь: 
            var panth = _gameGlobal.PanthFirstBlood.Find(x =>
                x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

            if (panth != null && panth.FriendList.Count >= 1)
            {
                if (panth.FriendList.Contains(player.Status.IsWonLastTime))
                {
                    player.Character.AddSpeed();
                    panth.FriendList.Clear();
                }
                else if (panth.FriendList.Contains(player.Status.IsLostLastTime))
                {
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsLostLastTime).Character
                        .AddSpeed();
                    panth.FriendList.Clear();
                }
            }
            //end Первая кровь: 

            //Это привилегия
            if (player.Status.IsWonLastTime != 0)
            {
                game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsWonLastTime).Character
                    .Justice.AddJusticeForNextRound();
            }
            //end Это привилегия

            //Им это не понравится: 
             panth = _gameGlobal.PanthMark.Find(x =>
                x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

             if (panth.FriendList.Contains(player.Status.IsWonLastTime))
             {
                player.Status.AddRegularPoints();
             }

             //end Им это не понравится: 
        }
    }
}
