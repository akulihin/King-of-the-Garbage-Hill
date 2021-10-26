using System;
using System.Collections.Generic;
using System.Diagnostics;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class GameClass
    {
        public GameClass(List<GamePlayerBridgeClass> playersList, ulong gameId, int turnLengthInSecond = 333)
        {
            RoundNo = 1;
            Phrases = new CharactersUniquePhrase();
            PlayersList = playersList;
            GameId = gameId;
            TurnLengthInSecond = turnLengthInSecond;
            TimePassed = new Stopwatch();
            GameStatus = 1;
            AllGameGlobalLogs = "";
            GlobalLogs =
                "<:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567>\n\n" +
                "Сервер игры<:e_:562879579694301184>- https://discord.gg/EpzznDgTe7\n" +
                "Правила <:e_:562879579694301184> <:e_:562879579694301184>- <#561293853076881409>\n" +
                "FAQ <:e_:562879579694301184> <:e_:562879579694301184> <:e_:562879579694301184> - <#561293980730523668>\n" +
                "Новости <:e_:562879579694301184><:e_:562879579694301184> - <#561293699309371393>\n" +
                "Разработчики - <@!181514288278536193>, <@!238337696316129280>, <@!207707809339539457>\n" +
                "GitHub <:e_:562879579694301184><:e_:562879579694301184><:e_:562879579694301184>- https://github.com/mylorik/King-of-the-Garbage-Hill" +
                "\n\n<:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567> <:e_:562879579694301184> <:sparta:561287745675329567>\n";
            WhoWon = Guid.Empty;
            IsCheckIfReady = true;
            SkipPlayersThisRound = 0;
            GameVersion = "версия 0.9.1";
        }

        public int RoundNo { get; set; }
        public List<GamePlayerBridgeClass> PlayersList { get; set; }
        public ulong GameId { get; set; }
        public double TurnLengthInSecond { get; set; }
        public string GameVersion { get; set; }
        public Stopwatch TimePassed { get; set; }
        public int GameStatus { get; set; }
        public CharactersUniquePhrase Phrases { get; set; }
       
        public bool IsCheckIfReady { get; set; }

        /*
         * 1 - Turn
         * 2 - Counting
         * 3 - End
         */
        private string AllGameGlobalLogs { get; set; }

        public int SkipPlayersThisRound { get; set; }
        private string GlobalLogs { get; set; }
        public Guid WhoWon { get; set; }

        public void AddGlobalLogs(string str, string newLine = "\n")
        {
            GlobalLogs += str + newLine;
            AllGameGlobalLogs += str + newLine;
        }

        public string GetGlobalLogs()
        {
            return GlobalLogs;
        }

        public string GetAllGlobalLogs()
        {
            return AllGameGlobalLogs;
        }

        public void SetGlobalLogs(string str)
        {
            GlobalLogs = str;
        }
    }
}