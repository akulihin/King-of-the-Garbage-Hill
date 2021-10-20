using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;

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
            WhoToAttackThisTurn = Guid.Empty;

            IsReady = false;
            IsAbleToWin = true;
            WonTimes = 0;
            IsWonThisCalculation = Guid.Empty;
            IsLostThisCalculation = Guid.Empty;
            IsFighting = Guid.Empty;
            IsSkip = false;
            ScoresToGiveAtEndOfRound = 0;
            InGamePersonalLogs = "";
            InGamePersonalLogsAll = "";
            ScoreSource = "";
            WhoToLostEveryRound = new List<WhoToLostPreviousRoundClass>();
            PlayerId = Guid.NewGuid();
            KnownPlayerClass = new List<KnownPlayerClassClass>();
        }

        public int MoveListPage { get; set; }
        /*
         * 1 = main page ( your character + leaderboard)
         * 2 = Log   
         * 3 = lvlUp     (what stat to update)
         */

        public IUserMessage SocketMessageFromBot { get; set; }

        private int Score { get; set; }
        public Guid PlayerId { get; set; }
        public bool IsBlock { get; set; }
        public bool IsSkip { get; set; }
        public bool IsAbleToTurn { get; set; }
        public bool IsAbleToWin { get; set; }
        public int PlaceAtLeaderBoard { get; set; }
        public Guid WhoToAttackThisTurn { get; set; }
        public bool IsReady { get; set; }
        public int WonTimes { get; set; }
        public Guid IsWonThisCalculation { get; set; }
        public Guid IsLostThisCalculation { get; set; }
        public Guid IsFighting { get; set; }
        private double ScoresToGiveAtEndOfRound { get; set; }
        public int LvlUpPoints { get; set; }
        private string InGamePersonalLogs { get; set; }
        public string InGamePersonalLogsAll { get; set; }
        public string ScoreSource { get; set; }
        public List<WhoToLostPreviousRoundClass> WhoToLostEveryRound { get; set; }
        public List<KnownPlayerClassClass> KnownPlayerClass { get; set; }


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


        public void SetScoresToGiveAtEndOfRound(int score, string reason, bool isLog = true)
        {
            ScoresToGiveAtEndOfRound = score;
            if (isLog)
                ScoreSource += $"{reason}+";
        }

        public void AddRegularPoints(int regularPoints, string reason, bool isLog = true)
        {
            ScoresToGiveAtEndOfRound += regularPoints;
            if (isLog)
                ScoreSource += $"{reason}+";
        }


        public void AddBonusPoints(int bonusPoints = 1, string skillName = "")
        {
            if (bonusPoints > 0)
                AddInGamePersonalLogs($"{skillName}+{bonusPoints} __**бонусных**__ очков\n");
            else if (bonusPoints < 0) AddInGamePersonalLogs($"{skillName}{bonusPoints} __**бонусных**__ очков\n");

            Score += bonusPoints;
            if (Score < 0)
                Score = 0;
        }

        public double GetScoresToGiveAtEndOfRound()
        {
            return ScoresToGiveAtEndOfRound;
        }

        public void CombineRoundScoreAndGameScore(GameClass game, InGameGlobal gameGlobal,
            CharactersUniquePhrase phrase)
        {
            var roundNumber = game.RoundNo;


            if (game.PlayersList.Any(x => x.Character.Name == "Толя"))
            {
                var tolyaAcc = game.PlayersList.Find(x => x.Character.Name == "Толя");

                var tolyaCount = gameGlobal.TolyaCount.Find(x =>
                    x.PlayerId == tolyaAcc.Status.PlayerId && x.GameId == game.GameId);


                if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1 && x.Target == PlayerId))
                {
                    roundNumber = 1;

                    if (game.PlayersList.Any(x => x.Status.PlayerId == PlayerId && x.Character.Name == "Глеб"))
                        if (gameGlobal.GlebChallengerTriggeredWhen.Any(x => x.WhenToTrigger.Contains(game.RoundNo)))
                            phrase.TolyaCountReadyPhrase.SendLog(game.PlayersList.Find(x => x.Character.Name == "Глеб"), false);
                }
            }


            AddScore(GetScoresToGiveAtEndOfRound(), roundNumber);
            SetScoresToGiveAtEndOfRound(0, "", false);
            ScoreSource = "";
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
                AddInGamePersonalLogs(
                    $"+{(int) score} **обычных** очков ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
            else if ((int) score < 0)
                AddInGamePersonalLogs($"{(int) score} очков... ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
            Score += (int) score;
        }

        public void SetScoreToThisNumber(int score)
        {
            Score = score;
        }

        public int GetScore()
        {
            return Score;
        }

        public class WhoToLostPreviousRoundClass
        {
            public Guid EnemyId;
            public bool IsTooGood;
            public int RoundNo;

            public WhoToLostPreviousRoundClass(Guid enemyId, int roundNo, bool isTooGood)
            {
                EnemyId = enemyId;
                RoundNo = roundNo;
                IsTooGood = isTooGood;
            }
        }

        public class KnownPlayerClassClass
        {
            public Guid EnemyId;
            public string Text;

            public KnownPlayerClassClass(Guid enemyId, string text)
            {
                EnemyId = enemyId;
                Text = text;
            }
        }
    }
}