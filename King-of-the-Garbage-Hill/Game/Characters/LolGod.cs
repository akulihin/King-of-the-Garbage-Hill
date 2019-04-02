using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class LolGod : IServiceSingleton
    {
        
        private readonly SecureRandom _rand;

        private readonly GameUpdateMess _upd;

        public LolGod(GameUpdateMess upd, SecureRandom rand)
        {
            _upd = upd;
            _rand = rand;

        }

        public Task InitializeAsync() => Task.CompletedTask;

        public void HandleLolGod(GamePlayerBridgeClass player)
        {
            //   throw new System.NotImplementedException();
        }

        public void HandleLolGodAfter(GamePlayerBridgeClass player)
        {
            //     throw new System.NotImplementedException();
        }

        public class Udyr
        {
            public ulong GameId { get; set; }
            public ulong PlayerDiscordId { get; set; }
            public ulong EnemyDiscordId { get; set; }

            public Udyr(ulong playerDiscordId, ulong gameId  )
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                EnemyDiscordId = 99999999;
            }
        }





        public class PushAndDieClass
        {
            public ulong GameId { get; set; }
            public ulong PlayerDiscordId { get; set; }
            public List<PushAndDieSubClass> PlayersEveryRound = new List<PushAndDieSubClass>();

            public PushAndDieClass(ulong playerDiscordId, ulong gameId, IEnumerable<GamePlayerBridgeClass> players )
            {
                PlayerDiscordId = playerDiscordId;
                GameId = gameId;
                PlayersEveryRound.Add(new PushAndDieSubClass(1, players));
            }

        }

        public class PushAndDieSubClass
        {
            public int RoundNo  { get; set; }
            public List<PushAndDieSubSubClass> PlayerList = new List<PushAndDieSubSubClass>();

            public PushAndDieSubClass(int roundNo, IEnumerable<GamePlayerBridgeClass> players)
            {
                RoundNo = roundNo;
                foreach (var player in players)
                {
                    PlayerList.Add(new PushAndDieSubSubClass(player.DiscordAccount.DiscordId, player.Status.PlaceAtLeaderBoard));
                }
            }
        }

        public class PushAndDieSubSubClass
        {
            public ulong PlayerId  { get; set; }
            public int PlayerPlaceAtLeaderBoard { get; set; }

            public PushAndDieSubSubClass(ulong playerId, int playerPlaceAtLeaderBoard)
            {
                PlayerId = playerId;
                PlayerPlaceAtLeaderBoard = playerPlaceAtLeaderBoard;
            }
        }

    }
}
