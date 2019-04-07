using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    //unUsed.
    public class GameLogsClass
    {
        public GameLogsClass(ulong gameId, Guid whoWon, List<GameLogsPlayer> playerList, string gameLogs)
        {
            GameId = gameId;
            WhoWon = whoWon;
            PlayerList = playerList;
            Date = DateTime.Now;
            GameLogs = gameLogs;
        }

        public ulong GameId { get; set; }
        public Guid WhoWon { get; set; }
        public List<GameLogsPlayer> PlayerList { get; set; }
        public DateTime Date { get; set; }
        public string GameLogs { get; set; }
    }

    public class GameLogsPlayer
    {
        public string Character;
        public string InGamePersonalLogsAll;
        public int Intelligence;
        public ulong PlayerId;
        public string PlayerUserName;
        public int Psyche;
        public int Score;
        public int Speed;
        public int Strength;


        public GameLogsPlayer(ulong playerId, string playerName, string charName, int score, int intelligence,
            int strength, int speed, int psyche, string inGamePersonalLogsAll)
        {
            PlayerId = playerId;
            PlayerUserName = playerName;
            Character = charName;
            Score = score;
            Intelligence = intelligence;
            Strength = strength;
            Speed = speed;
            Psyche = psyche;
            InGamePersonalLogsAll = inGamePersonalLogsAll;
        }
    }
}