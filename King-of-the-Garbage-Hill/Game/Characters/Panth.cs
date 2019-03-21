using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class Panth : IServiceSingleton
    {
        
        private readonly SecureRandom _rand;
        private readonly InGameGlobal _gameGlobal;
        private readonly GameUpdateMess _upd;

        public Panth(GameUpdateMess upd, SecureRandom rand, InGameGlobal gameGlobal)
        {
            _upd = upd;
            _rand = rand;
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
                    player.Character.Speed++;
                    panth.FriendList.Clear();
                }
                else if (panth.FriendList.Contains(player.Status.IsLostLastTime))
                {
                    game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsLostLastTime).Character
                        .Speed++;
                    panth.FriendList.Clear();
                }
            }
            //end Первая кровь: 

            //Это привилегия
            if (player.Status.IsWonLastTime != 0)
            {
                game.PlayersList.Find(x => x.DiscordAccount.DiscordId == player.Status.IsWonLastTime).Character
                    .Justice.JusticeForNextRound++;
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
