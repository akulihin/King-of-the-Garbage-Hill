using System;
using System.Collections.Generic;
using System.Linq;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class InGameStatus
{
    public InGameStatus(string characterName)
    {
        MoveListPage = 1;
        LvlUpPoints = 0;
        Score = 0;
        IsBlock = false;
        IsAbleToTurn = true;
        PlaceAtLeaderBoard = 0;
        WhoToAttackThisTurn = new List<Guid>();

        IsReady = false;
        IsAutoMove = false;
        IsAbleToWin = true;
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
        ConfirmedPredict = true;
        ConfirmedSkip = true;
        IsAbleToChangeMind = true;
        IsTargetSkipped = Guid.NewGuid();
        IsTargetBlocked = Guid.NewGuid();
        CharacterName = characterName;
        PlaceAtLeaderBoardHistory = new List<PlaceAtLeaderBoardHistoryClass>();
        ChangeMindWhat = "";
        AutoMoveTimes = 0;
        TimesUpdated = 0;
    }


    public int MoveListPage { get; set; }
    /*
     * 1 = main page ( your character + leaderboard)
     * 2 = Log   
     * 3 = lvlUp     (what stat to update)
     */

    private int Score { get; set; }
    public Guid PlayerId { get; set; }
    public bool IsBlock { get; set; }
    public bool IsSkip { get; set; }
    public bool IsArmorBreak { get; set; }
    public bool IsSkipBreak { get; set; }
    public bool IsAutoMove { get; set; }
    public int AutoMoveTimes { get; set; }
    public bool IsAbleToTurn { get; set; }
    public bool IsAbleToWin { get; set; }
    public int PlaceAtLeaderBoard { get; set; }
    public List<Guid> WhoToAttackThisTurn { get; set; }
    public bool IsReady { get; set; }
    public Guid IsWonThisCalculation { get; set; }
    public Guid IsLostThisCalculation { get; set; }
    public Guid IsTargetSkipped { get; set; }
    public Guid IsTargetBlocked { get; set; }
    public Guid IsFighting { get; set; }
    private decimal ScoresToGiveAtEndOfRound { get; set; }
    public int LvlUpPoints { get; set; }
    private string InGamePersonalLogs { get; set; }
    public string InGamePersonalLogsAll { get; set; }
    public string ScoreSource { get; set; }
    public List<WhoToLostPreviousRoundClass> WhoToLostEveryRound { get; set; }
    public List<KnownPlayerClassClass> KnownPlayerClass { get; set; }
    public bool ConfirmedPredict { get; set; }
    public bool ConfirmedSkip { get; set; }
    public bool IsAbleToChangeMind { get; set; }
    public string ChangeMindWhat  { get; set;}
    public string CharacterName { get; set; }
    public int TimesUpdated { get; set; }
    public int RoundNumber { get; set; }

    //Real and Temp stats are used only for Round Mechanics (Fighting). They are used mostly to "ignore" or "swap" characteristics during one fight!
    public int RealIntelligence { get; set; } = -1;
    public int TempIntelligence { get; set; } = -1;
    public int RealStrength { get; set; } = -1;
    public int TempStrength { get; set; } = -1;
    public int RealSpeed { get; set; } = -1;
    public int TempSpeed { get; set; } = -1;
    public int RealPsyche { get; set; } = -1;
    public int TempPsyche { get; set; } = -1;
    public int RealJustice { get; set; } = -1;
    public int TempJustice { get; set; } = -1;

    public List<PlaceAtLeaderBoardHistoryClass> PlaceAtLeaderBoardHistory { get; set; }
    public DateTimeOffset LastButtonPress { get; set; } = DateTimeOffset.UtcNow;

    public void AddInGamePersonalLogs(string str)
    {

        var previous = InGamePersonalLogs.Split("\n");
        if (previous.Length > 1 && !str.Contains("Предположение") && !str.Contains("Безумие") && !str.Contains("Дракон") && !str.Contains("Претендент русского сервера"))
        {
            var currentSkills = str.Split(": ");
            if (currentSkills.Length > 0)
            {
                var currentSkill = currentSkills[0];
                var previousSkills = previous[^2].Split(": ");
                if (previousSkills.Length > 0)
                {
                    var previousSkill = previousSkills[0];
                    if (previousSkill == currentSkill)
                    {
                        str = str.Replace($"{previousSkill}: ", ". ");
                        InGamePersonalLogs = InGamePersonalLogs.Remove(InGamePersonalLogs.Length - 1, 1);
                        InGamePersonalLogsAll = InGamePersonalLogsAll.Remove(InGamePersonalLogsAll.Length - 1, 1);
                    }
                }
            }
        }

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

    public void SetInGamePersonalLogs(string newInGamePersonalLogs)
    {
        InGamePersonalLogs = newInGamePersonalLogs;
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
        if (!isLog) return;

        if (regularPoints >= 0)
        {
            ScoreSource += $"{reason}+";
        }
        else
        {
            if (ScoreSource.Length > 0)
            {
                ScoreSource = ScoreSource.Remove(ScoreSource.Length - 1, 1);
            }
            ScoreSource += $"-{reason}+";
        }
    }

    public void HardKittyMinus(int scoreToAdd, string skillName)
    {
        Score += scoreToAdd;
        AddInGamePersonalLogs($"{skillName}: {scoreToAdd} очков\n");
    }


    public void AddBonusPoints(int bonusPoints = 1, string skillName = "")
    {
        if (bonusPoints > 0)
            AddInGamePersonalLogs($"{skillName}: +{bonusPoints} __**бонусных**__ очков\n");
        else if (bonusPoints < 0) AddInGamePersonalLogs($"{skillName}: {bonusPoints} __**бонусных**__ очков\n");

        Score += bonusPoints;

        if (Score < 0 && CharacterName != "HardKitty")
            Score = 0;
    }

    public decimal GetScoresToGiveAtEndOfRound()
    {
        return ScoresToGiveAtEndOfRound;
    }

    public void CombineRoundScoreAndGameScore(GameClass game)
    {
        var roundNumber = game.RoundNo;

        //Подсчет
        if (game.PlayersList.Any(x => x.GameCharacter.Name == "Толя"))
        {
            var tolyaAcc = game.PlayersList.Find(x => x.GameCharacter.Name == "Толя");

            var tolyaCount = tolyaAcc!.Passives.TolyaCount;


            if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1 && x.Target == PlayerId))
                roundNumber = 1;
        }
        //end Подсчет

        AddScore(GetScoresToGiveAtEndOfRound(), roundNumber);
        SetScoresToGiveAtEndOfRound(0, "", false);
        ScoreSource = "";
    }

    private void AddScore(decimal score, int roundNumber)
    {
        /*
        1-4 х1
        5-9 х2
        10 х4
        */
        if (roundNumber <= 4)
            score *= 1; // Why????????????????????????
        else if (roundNumber <= 9)
            score *= 2;
        else if (roundNumber == 10)
            score *= 4;

        switch ((int)score)
        {
            case > 0:
                AddInGamePersonalLogs($"+{(int)score} **обычных** очков ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
                break;
            case < 0:
                AddInGamePersonalLogs($"{(int)score} **очков**... ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
                break;
            default:
            {
                if(score == 0 && ScoreSource.Length > 0)
                    AddInGamePersonalLogs($"{(int)score} **очков**!? ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
                break;
            }
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

    public class WhoToLostPreviousRoundClass
    {
        public Guid EnemyId;
        public bool IsTooGoodEnemy;
        public int RoundNo;
        public bool IsStatsBetterEnemy;
        public bool IsTooGoodMe;
        public bool IsStatsBetterMe;
        public Guid WhoAttacked;

        public WhoToLostPreviousRoundClass(Guid enemyId, int roundNo, bool isTooGoodEnemy, bool isStatsBetterEnemy, bool isTooGoodMe, bool isStatsBetterMe, Guid whoAttacked)
        {
            EnemyId = enemyId;
            RoundNo = roundNo;
            IsTooGoodEnemy = isTooGoodEnemy;
            IsStatsBetterEnemy = isStatsBetterEnemy;
            IsTooGoodMe = isTooGoodMe;
            IsStatsBetterMe = isStatsBetterMe;
            WhoAttacked = whoAttacked;
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

    public class PlaceAtLeaderBoardHistoryClass
    {
        public int GameRound;
        public int Place;

        public PlaceAtLeaderBoardHistoryClass(int gameRound, int place)
        {
            GameRound = gameRound;
            Place = place;
        }
    }
}