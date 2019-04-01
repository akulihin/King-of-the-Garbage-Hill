using System.Collections.Generic;
using Discord;

namespace King_of_the_Garbage_Hill.Game.Classes
{
    public class InGameStatus
    {
        public InGameStatus()
        {
            MoveListPage = 1;
            LvlUpPoints = 1;
            SocketMessageFromBot = null;
            Score = 0;
            IsBlock = false;
            IsAbleToTurn = true;
            PlaceAtLeaderBoard = 0;
            WhoToAttackThisTurn = 0;

            IsReady = false;
            IsAbleToWin = true;
            WonTimes = 0;
            IsWonThisCalculation = 0;
            IsLostThisCalculation = 0;
            IsFighting = 0;
            IsSkip = false;
            ScoresToGiveAtEndOfRound = 0;
            InGamePersonalLogs = "";
            InGamePersonalLogsAll = "";
            WhoToLostEveryRound = new List<WhoToLostPreviousRoundClass>();
        }

        public class WhoToLostPreviousRoundClass
        {
            public ulong EnemyId;
            public int RoundNo;

            public WhoToLostPreviousRoundClass(ulong enemyId, int roundNo)
            {
                EnemyId = enemyId;
                RoundNo = roundNo;
            }
        }

        public int MoveListPage { get; set; }
        /*
         * 1 = main page ( your character + leaderboard)
         * 2 = Log   
         * 3 = lvlUp     (what stat to update)
         */

        public IUserMessage SocketMessageFromBot { get; set; }

        private int Score { get; set; }
        public bool IsBlock { get; set; }
        public bool IsSkip { get; set; }
        public bool IsAbleToTurn { get; set; }
        public bool IsAbleToWin { get; set; }
        public int PlaceAtLeaderBoard { get; set; }

        public ulong WhoToAttackThisTurn { get; set; }

      

        public bool IsReady { get; set; }
        public int WonTimes { get; set; }
        public ulong IsWonThisCalculation { get; set; }
        public ulong IsLostThisCalculation { get; set; }
        public ulong IsFighting { get; set; }
        private double ScoresToGiveAtEndOfRound { get; set; }
        public int LvlUpPoints { get; set; }
        private string InGamePersonalLogs { get; set; }
        public string InGamePersonalLogsAll { get; set; }
        public List<WhoToLostPreviousRoundClass> WhoToLostEveryRound { get; set; }


        public void AddInGamePersonalLogs(string str)
        {
            InGamePersonalLogs += str;
            InGamePersonalLogsAll += str;
        }

        public void ClearInGamePersonalLogs()
        {
            InGamePersonalLogs = "";
        }

        public string GetInGamePersonalLogs()
        {
            return InGamePersonalLogs;
        }



        public void SetScoresToGiveAtEndOfRound(int score)
        {
            ScoresToGiveAtEndOfRound = score;
        }

        public void AddRegularPoints(double regularPoints)
        {
            ScoresToGiveAtEndOfRound += regularPoints;
        }

        public void AddRegularPoints(int regularPoints = 1)
        {
            ScoresToGiveAtEndOfRound += regularPoints;
        }

        public void AddBonusPoints(int bonusPoints = 1)
        {
            if(bonusPoints > 0)
                AddInGamePersonalLogs(  $"+{bonusPoints} **бонусных** очков\n");
            else if(bonusPoints < 0)
                AddInGamePersonalLogs ( $"{bonusPoints} **бонусных** очков\n");

            Score += bonusPoints;
            if (Score < 0)
                Score = 0;
        }

        public double GetScoresToGiveAtEndOfRound()
        {
            return ScoresToGiveAtEndOfRound;
        }

        public void CombineRoundScoreAndGameScore(int roundNumber)
        {
            AddScore(GetScoresToGiveAtEndOfRound(), roundNumber);
            SetScoresToGiveAtEndOfRound(0);
        }

        private void AddScore(double score, int roundNumber)
        {
            /*
            1-4 х1
            5-9 х2
            10 х4
            */
            if (roundNumber <= 4)
                score = score * 1; // Why????????????????????????
            else if (roundNumber <= 9)
                score = score * 2;
            else if (roundNumber == 10) 
                score = score * 4;

            if ((int) score > 0)
            {
                AddInGamePersonalLogs($"+{(int) score} **обычных** очков\n");
            }
            else if ((int) score < 0)
            {
                AddInGamePersonalLogs($"{(int) score} очков...\n");
            }
            Score += (int)score;
        }

        public void SetScoreToThisNumber(int score)
        {
            Score = score;
        }

        public int GetScore()
        {
            return Score;
        }
    }
}