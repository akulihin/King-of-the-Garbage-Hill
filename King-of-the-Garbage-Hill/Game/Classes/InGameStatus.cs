using Discord;
using Discord.WebSocket;

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
        public bool IsAbleToTurn { get; set; }
        public int PlaceAtLeaderBoard { get; set; }

        public ulong WhoToAttackThisTurn { get; set; }
    

        public bool IsReady { get; set; }


        public InGameStatus()
        {
            MoveListPage = 1;
            SocketMessageFromBot = null;
            Score = 0;
            IsBlock = false;
            IsAbleToTurn = false;
            PlaceAtLeaderBoard = 0;
            WhoToAttackThisTurn = 0;
        
            IsReady = false;

        }

    }
}
