using System.Collections.Generic;
using System.Diagnostics;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GameClass
    {
        public int RoundNo;
        public List<MainAccountClass> PlayersList;
        public ulong GameId;
        public double TurnLengthInSecond;
        public Stopwatch TimePassed;
        public int GameStatus;
        /*
         * 1 - Turn
         * 2 - Counting
         * 3 - End
         */


        public GameClass(List<MainAccountClass> playersList, uint gameId, int turnLengthInSecond = 60)
        {
            RoundNo = 1;
            PlayersList = playersList;
            GameId = gameId;
            TurnLengthInSecond = turnLengthInSecond;
            TimePassed = new Stopwatch();
            GameStatus = 1;
        }
    }
}