using System.Collections.Generic;
using System.Diagnostics;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GameClass
    {
        public int RoundNo;
        public List<GameBridgeClass> PlayersList;
        public ulong GameId;
        public double TurnLengthInSecond;
        public Stopwatch TimePassed;
        public int GameStatus;
        /*
         * 1 - Turn
         * 2 - Counting
         * 3 - End
         */
        public string GameLogs ;
        public string PreviousGameLogs ;
        public ulong WhoWon;
        public GameClass(List<GameBridgeClass> playersList, uint gameId, int turnLengthInSecond = 200)
        {
            RoundNo = 1;
            PlayersList = playersList;
            GameId = gameId;
            TurnLengthInSecond = turnLengthInSecond;
            TimePassed = new Stopwatch();
            GameStatus = 1;
            GameLogs = "";
            PreviousGameLogs = "Здесь будут показаны логи игры. \nВыберите цель для нападения по номеру в таблице, используя emoji";
            WhoWon = 228;
        }
    }
}