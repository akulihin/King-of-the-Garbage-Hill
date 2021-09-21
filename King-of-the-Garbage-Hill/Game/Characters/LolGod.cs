using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters
{
    public class LolGod : IServiceSingleton
    {
        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

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
            public Udyr(Guid playerId, ulong gameId)
            {
                PlayerId = playerId;
                GameId = gameId;
                EnemyPlayerId = Guid.Empty;
            }

            public ulong GameId { get; set; }
            public Guid PlayerId { get; set; }
            public Guid EnemyPlayerId { get; set; }
        }


        public class PushAndDieClass
        {
            public List<PushAndDieSubClass> PlayersEveryRound = new();

            public PushAndDieClass(Guid playerId, ulong gameId, IEnumerable<GamePlayerBridgeClass> players)
            {
                PlayerId = playerId;
                GameId = gameId;
                PlayersEveryRound.Add(new PushAndDieSubClass(1, players));
            }

            public ulong GameId { get; set; }
            public Guid PlayerId { get; set; }
        }

        public class PushAndDieSubClass
        {
            public List<PushAndDieSubSubClass> PlayerList = new();

            public PushAndDieSubClass(int roundNo, IEnumerable<GamePlayerBridgeClass> players)
            {
                RoundNo = roundNo;
                foreach (var player in players)
                    PlayerList.Add(new PushAndDieSubSubClass(player.Status.PlayerId, player.Status.PlaceAtLeaderBoard));
            }

            public int RoundNo { get; set; }
        }

        public class PushAndDieSubSubClass
        {
            public PushAndDieSubSubClass(Guid playerId, int playerPlaceAtLeaderBoard)
            {
                PlayerId = playerId;
                PlayerPlaceAtLeaderBoard = playerPlaceAtLeaderBoard;
            }

            public Guid PlayerId { get; set; }
            public int PlayerPlaceAtLeaderBoard { get; set; }
        }
    }
}