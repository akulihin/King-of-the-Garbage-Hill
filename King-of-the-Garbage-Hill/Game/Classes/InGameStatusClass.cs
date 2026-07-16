using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Helpers;

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
        PlayerId = SecureRandom.NextGuid();   // deterministic under a seeded sim (unique either way)
        KnownPlayerClass = new List<KnownPlayerClassClass>();
        ConfirmedPredict = true;
        ConfirmedSkip = true;
        IsAbleToChangeMind = true;
        IsTargetSkipped = SecureRandom.NextGuid();
        IsTargetBlocked = SecureRandom.NextGuid();

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
    private bool _isAbleToWin;
    public bool IsAbleToWin
    {
        get => _isAbleToWin;
        set
        {
            if (!value && UnknownBug.Is(GameCharacter)) return;
            _isAbleToWin = value;
        }
    }

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
    public List<ScoreEntry> ScoreEntries { get; set; } = new();
    public List<ScoreEntry> PreviousRoundScoreEntries { get; set; } = new();
    public int ActualRoundMultiplier { get; set; } = 1;
    public int ExpectedRoundMultiplier { get; set; } = 1;
    public Guid IsFighting { get; set; }
    private decimal ScoresToGiveAtEndOfRound { get; set; }
    private decimal BonusPointsEarnedThisRound { get; set; }
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

    public List<ForOneFightMod> ForOneFightMods { get; set; } = new();

    public int AramRerolledPassivesTimes { get; set; } = 0;
    public int AramRerolledStatsTimes { get; set; } = 0;

    // Temporary fight context flags for goblin death percentage calculation
    public bool FightEnemyWasTooGood { get; set; }
    public bool FightEnemyWasTooStronk { get; set; }
    public bool IsAramRollConfirmed { get; set; }
    public bool IsDraftPickConfirmed { get; set; }
    private string FightingData { get; set; } = "";


    public List<PlaceAtLeaderBoardHistoryClass> PlaceAtLeaderBoardHistory { get; set; }
    public DateTimeOffset LastButtonPress { get; set; } = DateTimeOffset.UtcNow;

    public void AddInGamePersonalLogs(string str)
    {

        var previous = InGamePersonalLogs.Split("\n");
        if (previous.Length > 1 && !str.Contains("Предположение") && !str.Contains("Безумие") && !str.Contains("Дракон") && !str.Contains("Претендент русского сервера") && !str.Contains("Глаза Итачи"))
        {
            var currentSkills = str.Split(": ");
            if (currentSkills.Length > 1)
            {
                var currentSkill = currentSkills[0];
                var previousSkills = previous[^2].Split(": ");
                if (previousSkills.Length > 1)
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
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == UnknownBug.PointFunnel))
        {
            return;
        }

        AddRegularPoints(regularPoints, reason, isLog);
    }

    public void AddRegularPoints(int regularPoints, string reason, bool isLog = true)
    {
        if (regularPoints < 0 && UnknownBug.Is(GameCharacter)) return;

        ScoresToGiveAtEndOfRound += regularPoints;
        ScoreEntries.Add(new ScoreEntry { Source = reason, Points = regularPoints, IsBonus = false });
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
        if (scoreToAdd < 0 && UnknownBug.Is(GameCharacter)) return;

        Score += scoreToAdd;
        ScoreEntries.Add(new ScoreEntry { Source = skillName, Points = scoreToAdd, IsBonus = true });
        AddInGamePersonalLogs($"{skillName}: {scoreToAdd} очков\n");
    }


    public void AddBonusPoints(decimal bonusPoints = 1, string skillName = "")
        => AddBonusPointsCore(bonusPoints, skillName, bypassScoreFloor: false);

    public void AddBonusPointsIgnoringFloor(decimal bonusPoints, string skillName)
        => AddBonusPointsCore(bonusPoints, skillName, bypassScoreFloor: true);

    private void AddBonusPointsCore(decimal bonusPoints, string skillName, bool bypassScoreFloor)
    {
        if (bonusPoints < 0 && UnknownBug.Is(GameCharacter)) return;

        if (bonusPoints > 0)
            AddInGamePersonalLogs($"{skillName}: +{bonusPoints} __**бонусных**__ очков\n");
        else if (bonusPoints < 0) AddInGamePersonalLogs($"{skillName}: {bonusPoints} __**бонусных**__ очков\n");

        Score += bonusPoints;
        BonusPointsEarnedThisRound += bonusPoints;
        ScoreEntries.Add(new ScoreEntry { Source = skillName, Points = bonusPoints, IsBonus = true });

        if (Score < 0 && !bypassScoreFloor
                      && GameCharacter.Passive.All(x => x.PassiveName != "Никому не нужен"))
            Score = 0;
    }

    public decimal GetScoresToGiveAtEndOfRound()
    {
        return ScoresToGiveAtEndOfRound;
    }

    public decimal GetBonusPointsEarnedThisRound()
    {
        return BonusPointsEarnedThisRound;
    }

    public int GetRoundScoreMultiplier(GameClass game)
    {
        var roundNumber = game.RoundNo;
        if (UnknownBug.Is(GameCharacter))
        {
            return roundNumber switch
            {
                <= 4 => 1,
                <= 9 => 2,
                _ => 4,
            };
        }

        if (IsRoundScoreMultiplierDisabledByTolya(game))
            roundNumber = 1;

        return roundNumber switch
        {
            <= 4 => 1,
            <= 9 => 2,
            _ => 4,
        };
    }

    public bool IsRoundScoreMultiplierDisabledByTolya(GameClass game)
    {
        if (game == null || UnknownBug.Is(GameCharacter)) return false;
        return game.PlayersList.Any(player =>
            player.GameCharacter.Passive.Any(passive => passive.PassiveName == "Подсчет")
            && player.Passives.TolyaCount.TargetList.Any(entry =>
                entry.RoundNumber == game.RoundNo - 1 && entry.Target == PlayerId));
    }

    public decimal DrainSettledScoreForTransfer(GameClass game, string reason)
    {
        var settledScore = Score + ScoresToGiveAtEndOfRound * GetRoundScoreMultiplier(game);
        AddInGamePersonalLogs(PhrasePayload.Encode(
            reason,
            $"{settledScore} очков передано оригиналу.",
            GameLocalization.Text(reason, GameLocalization.English),
            $"{settledScore} points were transferred to the original.") + "\n");
        Score = 0;
        ScoresToGiveAtEndOfRound = 0;
        BonusPointsEarnedThisRound = 0;
        ScoreSource = "";
        ScoreEntries.Clear();
        return settledScore;
    }

    public void DiscardScoreAfterDeath()
    {
        Score = 0;
        ScoresToGiveAtEndOfRound = 0;
        BonusPointsEarnedThisRound = 0;
        ScoreSource = "";
        ScoreEntries.Clear();
        PreviousRoundScoreEntries.Clear();
    }

    public void AddSettledScore(decimal score, string reason)
    {
        Score += score;
        ScoreEntries.Add(new ScoreEntry { Source = reason, Points = score, IsBonus = true });
        AddInGamePersonalLogs(PhrasePayload.Encode(
            reason,
            $"+{score} очков к финальному счету.",
            GameLocalization.Text(reason, GameLocalization.English),
            $"+{score} points added to the final score.") + "\n");
    }

    public void CombineRoundScoreAndGameScore(GameClass game, decimal? regularScoreOverride = null)
    {
        // Expected multiplier (before any passive overrides)
        ExpectedRoundMultiplier = game.RoundNo switch
        {
            <= 4 => 1,
            <= 9 => 2,
            _ => 4
        };

        // Actual multiplier (after passive overrides) + snapshot
        ActualRoundMultiplier = regularScoreOverride.HasValue ? 1 : GetRoundScoreMultiplier(game);
        PreviousRoundScoreEntries = new List<ScoreEntry>(ScoreEntries);
        ScoreEntries.Clear();

        var rawScore = GetScoresToGiveAtEndOfRound();
        if (regularScoreOverride.HasValue)
        {
            PreviousRoundScoreEntries.Add(new ScoreEntry
            {
                Source = GordonFreeman.HalfLife3,
                Points = regularScoreOverride.Value - rawScore,
                IsBonus = false,
            });
            AddScoreWithMultiplier(regularScoreOverride.Value, 1);
        }
        else
        {
            AddScoreWithMultiplier(rawScore, ActualRoundMultiplier);
        }
        SetScoresToGiveAtEndOfRound(0, "", false);
        BonusPointsEarnedThisRound = 0;
        ScoreSource = "";
    }

    private void AddScoreWithMultiplier(decimal score, int multiplier)
    {
        score *= multiplier;
        if (score < 0 && UnknownBug.Is(GameCharacter)) return;

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
        if (Score < 0 && GameCharacter.Passive.All(x => x.PassiveName != "Никому не нужен"))
            Score = 0;
    }

    public void SetScoreToThisNumber(int score, string text)
    {
        if (score < Score && UnknownBug.Is(GameCharacter)) return;

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
        public bool IsTooStronkEnemy;
        public int RoundNo;
        public bool IsStatsBetterEnemy;
        public bool IsTooGoodMe;
        public bool IsTooStronkMe;
        public bool IsStatsBetterMe;
        public Guid WhoAttacked;
        public int PlaceAtLeaderBoardMe;
        public int PlaceAtLeaderBoardEnemy;

        public WhoToLostPreviousRoundClass(Guid enemyId, int roundNo,
            bool isTooGoodEnemy, bool isTooStronkEnemy, bool isStatsBetterEnemy,
            bool isTooGoodMe, bool isTooStronkMe, bool isStatsBetterMe,
            Guid whoAttacked, int placeAtLeaderBoardMe, int placeAtLeaderBoardEnemy)
        {
            EnemyId = enemyId;
            RoundNo = roundNo;
            IsTooGoodEnemy = isTooGoodEnemy;
            IsTooStronkEnemy = isTooStronkEnemy;
            IsStatsBetterEnemy = isStatsBetterEnemy;
            IsTooGoodMe = isTooGoodMe;
            IsTooStronkMe = isTooStronkMe;
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

public class ForOneFightMod
{
    public string Source { get; set; }
    public string Stat { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal NewValue { get; set; }
}

public class ScoreEntry
{
    public string Source { get; set; }
    public decimal Points { get; set; }
    public bool IsBonus { get; set; }
}
