using System;
using System.Collections.Generic;
using System.Diagnostics;
using Discord;

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

        public bool IsCheckIfReady { get; set; }
        /*
         * 1 - Turn
         * 2 - Counting
         * 3 - End
         */
        private string GameLogs { get; set; }

        public int SkipPlayersThisRound { get; set; }
        private string PreviousGameLogs { get; set; }
        public Guid WhoWon { get; set; }
        public GameClass(List<GamePlayerBridgeClass> playersList, ulong gameId, int turnLengthInSecond = 300)
        {
            RoundNo = 1;
            PlayersList = playersList;
            GameId = gameId;
            TurnLengthInSecond = turnLengthInSecond;
            TimePassed = new Stopwatch();
            GameStatus = 1;
            GameLogs = "";
            PreviousGameLogs = $"{new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")}\n" +
                               "**Выберите цель для нападения по номеру в таблице, используя emoji**\n \n" +
                               "**Нажатие** и **Отжатие** одинаковые действия!\n" +
                               "**__Сообщение обновляется только каждые 5 секунд__**, но я сообщу вам отдельным сообщением, если вы напали или пытались напасть\n" +
                               $"{new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")} {new Emoji("<:e_:562879579694301184>")} {new Emoji("<:sparta:561287745675329567>")}\n";
            WhoWon = Guid.Empty;
            IsCheckIfReady = true;
            SkipPlayersThisRound = 0;
        }

        public void AddPreviousGameLogs(string str, string newLine = "\n", bool isAddItTooAllGameLogs = true)
        {
            PreviousGameLogs +=str + newLine;
            if(isAddItTooAllGameLogs)
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