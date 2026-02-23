using System;
using System.Collections.Generic;
using System.Linq;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class InGameStatus
{
    public InGameStatus()
    {
        MoveListPage = 1;
        LvlUpPoints = 0;
        Score = 0;
        IsBlock = false;
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

        PlaceAtLeaderBoardHistory = new List<PlaceAtLeaderBoardHistoryClass>();
        ChangeMindWhat = "";
        AutoMoveTimes = 0;
        TimesUpdated = 0;
        MoralGainedThisFight = 0;
    }


    public int MoveListPage { get; set; }
    /*
     * 1 = main page ( your character + leaderboard)
     * 2 = Log   
     * 3 = lvlUp     (what stat to update)
     * 4 = debug
     * 5 = aram
     */

    private decimal Score { get; set; }
    public Guid PlayerId { get; set; }
    public bool IsBlock { get; set; }
    public bool IsSkip { get; set; }
    public bool IsArmorBreak { get; set; }
    public bool IsSkipBreak { get; set; }
    public bool IsAutoMove { get; set; }
    public int AutoMoveTimes { get; set; }
    public bool IsAbleToWin { get; set; }

    /// <summary>Temporary flag: when true, the current fight should be hidden from non-admin logs.</summary>
    public bool HideCurrentFight { get; set; }

    private int PlaceAtLeaderBoard { get; set; }
    public List<Guid> WhoToAttackThisTurn { get; set; }
    public bool IsReady { get; set; }
    public Guid IsWonThisCalculation { get; set; }
    public Guid IsLostThisCalculation { get; set; }
    public Guid IsTargetSkipped { get; set; }
    public Guid IsTargetBlocked { get; set; }
    public decimal MoralGainedThisFight { get; set; }
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
    public CharacterClass GameCharacter { get; set; }
    public int TimesUpdated { get; set; }
    public int RoundNumber { get; set; }

    //Real and Temp stats are used only for Round Mechanics (Fighting). They are used mostly to "ignore" or "swap" characteristics during one fight!
    public bool IsIntelligenceForOneFight { get; set; } = false;
    public bool IsStrengthForOneFight { get; set; } = false;
    public bool IsSkillForOneFight { get; set; } = false;
    public bool IsSpeedForOneFight { get; set; } = false;
    public bool IsPsycheForOneFight { get; set; } = false;
    public bool IsJusticeForOneFight { get; set; } = false;

    public int AramRerolledPassivesTimes { get; set; } = 0;
    public int AramRerolledStatsTimes { get; set; } = 0;

    // Temporary fight context flags for goblin death percentage calculation
    public bool FightEnemyWasTooGood { get; set; }
    public bool FightEnemyWasTooStronk { get; set; }
    public bool IsAramRollConfirmed { get; set; }
    private string FightingData { get; set; } = "";


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


    public void SetScoresToGiveAtEndOfRound(decimal score, string reason, bool isLog = true)
    {
        ScoresToGiveAtEndOfRound = score;
        if (isLog)
            ScoreSource += $"{reason}+";
    }

    public void AddWinPoints(GameClass game, GamePlayerBridgeClass player, int regularPoints, string reason, bool isLog = true)
    {
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "PointFunnel"))
        {
            return;
        }

        if (player.Passives.PointFunneledTo != Guid.Empty)
        {
            var funnelPoints = regularPoints < 0 ? regularPoints * -1 : regularPoints;
            var bug = game.PlayersList.Find(x => x.GetPlayerId() == player.Passives.PointFunneledTo);
            bug.Status.AddRegularPoints(funnelPoints, "PointFunnel", isLog);
            bug.Status.AddInGamePersonalLogs($"```siphon\nPointFunnel: +{funnelPoints} from {player.DiscordUsername}\n```\n");
        }

        AddRegularPoints(regularPoints, reason, isLog);
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


    public void AddBonusPoints(decimal bonusPoints = 1, string skillName = "")
    {
        if (bonusPoints > 0)
            AddInGamePersonalLogs($"{skillName}: +{bonusPoints} __**бонусных**__ очков\n");
        else if (bonusPoints < 0) AddInGamePersonalLogs($"{skillName}: {bonusPoints} __**бонусных**__ очков\n");

        Score += bonusPoints;

        if (Score < 0 && GameCharacter.Passive.All(x => x.PassiveName != "Никому не нужен"))
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
        foreach (var player in game.PlayersList)
        {
            foreach (var passive in player.GameCharacter.Passive)
                switch (passive.PassiveName)
            {
                    case "Подсчет":
                        var tolyaCount = player.Passives.TolyaCount;
                        if (tolyaCount.TargetList.Any(x => x.RoundNumber == game.RoundNo - 1 && x.Target == PlayerId))
                            roundNumber = 1;
                        break;
            }
        }
        //end Подсчет

        AddScore(GetScoresToGiveAtEndOfRound(), roundNumber);
        SetScoresToGiveAtEndOfRound(0, "", false);
        ScoreSource = "";
    }

    private void AddScore(decimal score, int roundNumber)
    {
        score *= roundNumber switch
        { 
            /*
        1-4 х1
        5-9 х2
        10 х4
        */
            <= 4 => 1,
            <= 9 => 2,
            _ => 4
        };

        switch (score)
        {
            case > 0:
                if (ScoreSource.Length > 0)
                    AddInGamePersonalLogs($"+{score} **обычных** очков ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
                else
                    AddInGamePersonalLogs($"+{score} **обычных** очков\n");
                break;
            case < 0:
                if (ScoreSource.Length > 0)
                    AddInGamePersonalLogs($"{score} **очков**... ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
                else
                    AddInGamePersonalLogs($"{score} **очков**...\n");
                break;
            default:
            {
                if(score == 0 && ScoreSource.Length > 0)
                    AddInGamePersonalLogs($"{score} **очков**!? ({ScoreSource.Remove(ScoreSource.Length - 1, 1)})\n");
                break;
            }
        }
        Score += score;
    }

    public void SetScoreToThisNumber(int score, string text)
    {
        AddInGamePersonalLogs($"{score} **очков**... ({text})\n");
        Score = score;
    }

    public decimal GetScore()
    {
        return Score;
    }

    public int GetPlaceAtLeaderBoard()
    {
        return PlaceAtLeaderBoard;
    }

    public void SetPlaceAtLeaderBoard(int placeAtLeaderBoard)
    {
        PlaceAtLeaderBoard = placeAtLeaderBoard;
    }

    public string GetFightingData()
    {
        return FightingData;
    }

    public void AddFightingData(string data)
    {
        FightingData += $"{data}\n";
    }

    public void ResetFightingData()
    {
        FightingData = "";
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
        public int PlaceAtLeaderBoardMe;
        public int PlaceAtLeaderBoardEnemy;

        public WhoToLostPreviousRoundClass(Guid enemyId, int roundNo, bool isTooGoodEnemy, bool isStatsBetterEnemy, bool isTooGoodMe, bool isStatsBetterMe, Guid whoAttacked, int placeAtLeaderBoardMe, int placeAtLeaderBoardEnemy)
        {
            EnemyId = enemyId;
            RoundNo = roundNo;
            IsTooGoodEnemy = isTooGoodEnemy;
            IsStatsBetterEnemy = isStatsBetterEnemy;
            IsTooGoodMe = isTooGoodMe;
            IsStatsBetterMe = isStatsBetterMe;
            WhoAttacked = whoAttacked;
            PlaceAtLeaderBoardMe = placeAtLeaderBoardMe;
            PlaceAtLeaderBoardEnemy = placeAtLeaderBoardEnemy;

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