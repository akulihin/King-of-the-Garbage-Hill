using System;
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
            public Guid PlayerId { get; set; }
            public Guid EnemyPlayerId { get; set; }

            public Udyr(Guid playerId, ulong gameId  )
            {
                PlayerId = playerId;
                GameId = gameId;
                EnemyPlayerId = Guid.Empty;
            }
        }





        public class PushAndDieClass
        {
            public ulong GameId { get; set; }
            public Guid PlayerId { get; set; }
            public List<PushAndDieSubClass> PlayersEveryRound = new List<PushAndDieSubClass>();

            public PushAndDieClass(Guid playerId, ulong gameId, IEnumerable<GamePlayerBridgeClass> players )
            {
                PlayerId = playerId;
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
                    PlayerList.Add(new PushAndDieSubSubClass(player.Status.PlayerId, player.Status.PlaceAtLeaderBoard));
                }
            }
        }

        public class PushAndDieSubSubClass
        {
            public Guid PlayerId  { get; set; }
            public int PlayerPlaceAtLeaderBoard { get; set; }

            public PushAndDieSubSubClass(Guid playerId, int playerPlaceAtLeaderBoard)
            {
                PlayerId = playerId;
                PlayerPlaceAtLeaderBoard = playerPlaceAtLeaderBoard;
            }
        }

    }
}
