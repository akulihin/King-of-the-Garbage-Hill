using Discord;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class InGameStatus
    {
        public int MoveListPage { get; set; }
        /*
         * 1 = main page ( your character + leaderboard)
         * 2 = Log   
         * 3 = lvlUp     (what stat to update)
         */
       
        public IUserMessage SocketMessageFromBot { get; set; }

        public int Score { get; set; }
        public bool IsBlock { get; set; }
        public bool IsSkip { get; set; }
        public bool IsAbleToTurn { get; set; }
        public bool IsAbleToWin{ get; set; }
        public int PlaceAtLeaderBoard { get; set; }

        public ulong WhoToAttackThisTurn { get; set; }
    

        public bool IsReady { get; set; }
        public int WonTimes { get; set; }
        public ulong IsWonLastTime { get; set; }
        public ulong IsLostLastTime { get; set; }

        public InGameStatus()
        {
            MoveListPage = 1;
            SocketMessageFromBot = null;
            Score = 0;
            IsBlock = false;
            IsAbleToTurn = true;
            PlaceAtLeaderBoard = 0;
            WhoToAttackThisTurn = 0;
        
            IsReady = false;
            IsAbleToWin = true;
            WonTimes = 0;
            IsWonLastTime = 0;
            IsLostLastTime = 0;
            IsSkip = false;
        }

    }
}
