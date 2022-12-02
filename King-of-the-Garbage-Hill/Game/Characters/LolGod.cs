using System;
using System.Collections.Generic;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class LolGod
{
    public class Udyr
    {
        public Guid EnemyPlayerId { get; set; } = Guid.Empty;
    }


    public class PushAndDieClass
    {
        public List<PushAndDieSubClass> PlayersEveryRound = new();

        public PushAndDieClass(IEnumerable<GamePlayerBridgeClass> players)
        {
            PlayersEveryRound.Add(new PushAndDieSubClass(1, players));
        }
    }

    public class PushAndDieSubClass
    {
        public List<PushAndDieSubSubClass> PlayerList = new();

        public PushAndDieSubClass(int roundNo, IEnumerable<GamePlayerBridgeClass> players)
        {
            RoundNo = roundNo;
            foreach (var player in players)
                PlayerList.Add(new PushAndDieSubSubClass(player.GetPlayerId(), player.Status.GetPlaceAtLeaderBoard()));
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