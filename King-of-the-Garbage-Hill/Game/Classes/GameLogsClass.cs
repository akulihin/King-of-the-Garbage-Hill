using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    //unUsed.
    public class GameLogsClass
    {
        public ulong GameId { get; set; }
        public ulong WhoWon { get; set; }
        public List<GameLogsPlayer> PlayerList { get; set; }
        public DateTime Date { get; set; }
        public String GameLogs { get; set; }

        public GameLogsClass(ulong gameId, ulong whoWon, List<GameLogsPlayer> playerList, string gameLogs)
        {
            GameId = gameId;
            WhoWon = whoWon;
            PlayerList = playerList;
            Date = DateTime.Now;
            GameLogs = gameLogs;
        }
    }

    public class GameLogsPlayer
    {
        public ulong PlayerId;
        public string PlayerUserName;
        public string Character;
        public int Score; 
        public int Intelligence; 
        public int Psyche;
        public int Speed;
        public int Strength;


        public GameLogsPlayer(ulong playerId, string playerName, string charName, int score, int intelligence, int strength, int speed, int psyche)
        {
            PlayerId = playerId;
            PlayerUserName = playerName;
            Character = charName;
            Score = score;
            Intelligence = intelligence;
            Strength = strength;
            Speed = speed;
            Psyche = psyche;
        }
    }
}
