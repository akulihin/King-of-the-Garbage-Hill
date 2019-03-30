using System.Collections.Generic;
using System.Diagnostics;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GameClass
    {
        public int RoundNo;
        public List<GamePlayerBridgeClass> PlayersList;
        public ulong GameId;
        public double TurnLengthInSecond;
        public Stopwatch TimePassed;
        public int GameStatus;
        /*
         * 1 - Turn
         * 2 - Counting
         * 3 - End
         */
        private string GameLogs { get; set; }
        private string PreviousGameLogs { get; set; }
        public ulong WhoWon;
        public GameClass(List<GamePlayerBridgeClass> playersList, ulong gameId, int turnLengthInSecond = 200)
        {
            RoundNo = 1;
            PlayersList = playersList;
            GameId = gameId;
            TurnLengthInSecond = turnLengthInSecond;
            TimePassed = new Stopwatch();
            GameStatus = 1;
            GameLogs = "";
            PreviousGameLogs = "Здесь будут показаны логи игры. \nВыберите цель для нападения по номеру в таблице, используя emoji\n";
            WhoWon = 228;
        }

        public void AddPreviousGameLogs(string str)
        {
            PreviousGameLogs +=str + "\n";
            GameLogs += str + "\n";
        }

        public void AddGameLogs(string str)
        {
            GameLogs += str;
        }

        public string GetPreviousGameLogs()
        {
            return PreviousGameLogs;
        }

        public string GetAllGameLogs()
        {
            return GameLogs;
        }

        public void SetPreviousGameLogs(string str)
        {
            PreviousGameLogs = str;
        }
    }
}