using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GameLogsClass
    {
        public ulong  GameId { get; set; }
        public int RoundNumber { get; set; }
        public string PlayerName { get; set; }
        public ulong PlayerId{ get; set; }
        public ulong PlayerIdHeAttacked{ get; set; }
        public bool IsBlock{ get; set; }
        public bool IsWin{ get; set; }
        public bool IsSpecialStatus{ get; set; }
        public string SpecialStatus{ get; set; }

        public int ReceivedJusticeThisRound{ get; set; }
        public int LostJusticeThisRound{ get; set; }

        public int ReceivedPointsThisRound{ get; set; }

        public int PreviousPlaceAtLeaderBoard{ get; set; }
        public int NewPlaceAtLeaderBoard{ get; set; }
        public int ReceivedRandomStage3{ get; set; }

        public GameLogsClass(ulong id, string name)
        {
            PlayerId = id;
            PlayerName = name;
        }
    }

    public class GameLogsSubClass
    {
        public List<GameLogsClass> PlayersLogs;
        public string MainLogString;
    }
}
