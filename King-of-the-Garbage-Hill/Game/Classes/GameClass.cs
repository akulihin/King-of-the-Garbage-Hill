using System.Collections.Generic;
using System.Diagnostics;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GameClass
    {
        public int RoundNo { get; set; }
        public List<GamePlayerBridgeClass> PlayersList { get; set; }
        public ulong GameId{ get; set; }
        public double TurnLengthInSecond { get; set; }
        public Stopwatch TimePassed { get; set; }
        public int GameStatus { get; set; }

        public bool IsTimerToCheckEnabled { get; set; }
        /*
         * 1 - Turn
         * 2 - Counting
         * 3 - End
         */
        private string GameLogs { get; set; }

        public int SkipPlayersThisRound { get; set; }
        private string PreviousGameLogs { get; set; }
        public ulong WhoWon { get; set; }
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
            IsTimerToCheckEnabled = true;
            SkipPlayersThisRound = 0;
        }

        public void AddPreviousGameLogs(string str, string newLine = "\n")
        {
            PreviousGameLogs +=str + newLine;
            GameLogs += str + newLine;
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