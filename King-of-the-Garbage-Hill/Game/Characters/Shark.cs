using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Shark : IServiceSingleton
    {
        private readonly InGameGlobal _gameGlobal;

        private readonly SecureRandom _rand;
        private readonly GameUpdateMess _upd;

        public Shark(GameUpdateMess upd, SecureRandom rand, InGameGlobal gameGlobal)
        {
            _upd = upd;
            _rand = rand;
            _gameGlobal = gameGlobal;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public void HandleShark(GameBridgeClass player)
        {
            //  throw new System.NotImplementedException();
        }

        public void HandleSharkAfter(GameBridgeClass player, GameClass game)
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
                    player.Character.Speed++;
                }
                else
                {
                    if (!shark.FriendList.Contains(player.Status.IsWonLastTime))
                    {
                        shark.FriendList.Add(player.Status.IsWonLastTime);
                        player.Character.Speed++;
                    }
                }

                if (player.Character.Speed > 10) player.Character.Speed = 10;
            }

            //end Челюсти: 
        }
    }
}