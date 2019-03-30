using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

using King_of_the_Garbage_Hill.Game.GameGlobalVariables;


namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Shark : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;



        public Shark( InGameGlobal gameGlobal)
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
            if (player.Status.IsWonLastTime != 0)
            {
                var shark = _gameGlobal.SharkJawsWin.Find(x =>
                    x.GameId == game.GameId && x.PlayerDiscordId == player.DiscordAccount.DiscordId);

                if (shark == null)
                {
                    _gameGlobal.SharkJawsWin.Add(new Sirinoks.FriendsClass(player.DiscordAccount.DiscordId, game.GameId,
                        player.Status.IsWonLastTime));
                    player.Character.AddSpeed(player.Status);
                }
                else
                {
                    if (!shark.FriendList.Contains(player.Status.IsWonLastTime))
                    {
                        shark.FriendList.Add(player.Status.IsWonLastTime);
                        player.Character.AddSpeed(player.Status);
                    }
                }

            }

            //end Челюсти: 
        }
    }
}