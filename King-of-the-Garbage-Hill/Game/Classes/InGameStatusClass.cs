using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Classes;

public enum TurnInterferenceKind
{
    None,
    Self,
    Enemy,
}

/// <summary>
/// Presentation contract for a canonical gameplay source. NeutralTarget preserves the real source
/// in state/ledgers while showing only a question mark to the affected recipient in every mode.
/// RevealedTarget is a narrow, explicitly approved end-of-game receipt: its owner may see the
/// canonical source even in Pro, while spectator projections still apply ordinary masking.
/// </summary>
public enum FeedbackSourceVisibility
{
    NamedTarget = 0,
    NeutralTarget = 1,
    RevealedTarget = 2,
    ProNeutralTarget = 3,
}

public class InGameStatus
{
    // Designer-authored neutral default for an enemy-forced lost turn. Do not invent a
    // source-specific target receipt: new enemy Skip mechanics that need feedback reuse this text.
    public const string EnemyForcedSkipNotice = "Тебя усыпили...\n";

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
    private bool _isSkip;
    public bool IsSkip
    {
        get => _isSkip;
        set
        {
            _isSkip = value;
            if (!value)
            {
                TurnInterference = TurnInterferenceKind.None;
                return;
            }

            // Existing/self-authored skip paths remain safe by default. Enemy mechanics
            // explicitly overwrite this classification at the point where they apply.
            if (TurnInterference == TurnInterferenceKind.None)
                TurnInterference = TurnInterferenceKind.Self;
        }
    }
    public TurnInterferenceKind TurnInterference { get; set; }
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
            if (!value && (UnknownBug.Is(GameCharacter)
                           || Homelander.IsProtected(GameCharacter, this)))
            {
                if (Homelander.IsProtected(GameCharacter, this))
                    Homelander.LogProtection(this);
                return;
            }
            _isAbleToWin = value;
        }
    }

    /// <summary>Temporary flag: when true, the current fight should be hidden from non-admin logs.</summary>
    public bool HideCurrentFight { get; set; }
    /// <summary>Temporary flag: the current fight is Dopa's second, shadow action.</summary>
    public bool IsShadowAction { get; set; }
    /// <summary>Temporary bypass for Homelander's defenses while TheBoys affect or fight him.</summary>
    public bool IsFightingTheBoys { get; set; }
    public List<int> HomelanderProtectionPhrasePool { get; set; } = new();
    /// <summary>Whether Праведность's hidden Семерка score is currently counted.</summary>
    public bool HomelanderSevenPointsActive { get; set; }

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
    public bool WasRoundScoreMultiplierReducedByTolya { get; set; }
    public decimal LifetimeAbilityPointsEarned { get; private set; }
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
    /// <summary>
    /// True from the round snapshot until every fight in that calculation has resolved.
    /// Result-driven transformations must wait until this simultaneous batch is complete.
    /// </summary>
    public bool IsFightBatchActive { get; set; }

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

        AddRegularPoints(regularPoints, reason, isLog, isNaturalWin: true);
    }

    public void AddRegularPoints(
        int regularPoints,
        string reason,
        bool isLog = true,
        bool isNaturalWin = false,
        FeedbackSourceVisibility sourceVisibility = FeedbackSourceVisibility.NamedTarget)
    {
        if (regularPoints < 0 && (UnknownBug.Is(GameCharacter)
                                 || Homelander.IsProtected(GameCharacter, this)))
        {
            if (Homelander.IsProtected(GameCharacter, this))
                Homelander.LogProtection(this);
            return;
        }

        ScoresToGiveAtEndOfRound += regularPoints;
        ScoreEntries.Add(new ScoreEntry
        {
            Source = reason,
            Points = regularPoints,
            IsBonus = false,
            IsNaturalWin = isNaturalWin,
            SourceVisibility = sourceVisibility,
        });
        if (!isLog) return;

        var displayedReason = sourceVisibility == FeedbackSourceVisibility.ProNeutralTarget
            ? PhrasePayload.EncodeProNeutralSource(
                reason,
                GameLocalization.Text(reason, GameLocalization.English))
            : reason;

        if (regularPoints >= 0)
        {
            ScoreSource += $"{displayedReason}+";
        }
        else
        {
            if (ScoreSource.Length > 0)
            {
                ScoreSource = ScoreSource.Remove(ScoreSource.Length - 1, 1);
            }
            ScoreSource += $"-{displayedReason}+";
        }
    }

    public void HardKittyMinus(int scoreToAdd, string skillName)
    {
        if (scoreToAdd < 0 && (UnknownBug.Is(GameCharacter)
                               || Homelander.IsProtected(GameCharacter, this)))
        {
            if (Homelander.IsProtected(GameCharacter, this))
                Homelander.LogProtection(this);
            return;
        }

        Score += scoreToAdd;
        ScoreEntries.Add(new ScoreEntry { Source = skillName, Points = scoreToAdd, IsBonus = true });
        AddInGamePersonalLogs($"{skillName}: {scoreToAdd} очков\n");
    }


    public void AddBonusPoints(
        decimal bonusPoints = 1,
        string skillName = "",
        FeedbackSourceVisibility sourceVisibility = FeedbackSourceVisibility.NamedTarget)
        => AddBonusPointsCore(
            bonusPoints, skillName, bypassScoreFloor: false, sourceVisibility);

    public void AddBonusPointsIgnoringFloor(
        decimal bonusPoints,
        string skillName,
        FeedbackSourceVisibility sourceVisibility = FeedbackSourceVisibility.NamedTarget)
        => AddBonusPointsCore(
            bonusPoints, skillName, bypassScoreFloor: true, sourceVisibility);

    private void AddBonusPointsCore(
        decimal bonusPoints,
        string skillName,
        bool bypassScoreFloor,
        FeedbackSourceVisibility sourceVisibility)
    {
        if (bonusPoints < 0 && (UnknownBug.Is(GameCharacter)
                               || Homelander.IsProtected(GameCharacter, this)))
        {
            if (Homelander.IsProtected(GameCharacter, this))
                Homelander.LogProtection(this);
            return;
        }

        var isRoyal = JonSnow.IsKingActive(GameCharacter, PlaceAtLeaderBoard);
        if (isRoyal)
            bonusPoints *= 2;
        var pointType = isRoyal ? "королевских" : "бонусных";
        var englishPointType = isRoyal ? "royal" : "bonus";
        var displaySource = sourceVisibility == FeedbackSourceVisibility.NeutralTarget
            ? "❓"
            : skillName;

        if (bonusPoints != 0)
        {
            var signedPoints = bonusPoints > 0 ? $"+{bonusPoints}" : bonusPoints.ToString();
            if (sourceVisibility == FeedbackSourceVisibility.RevealedTarget)
            {
                var englishSource = GameLocalization.Text(skillName, GameLocalization.English);
                AddInGamePersonalLogs(PhrasePayload.EncodeOwnerOnly(
                    skillName,
                    $"{skillName}: {signedPoints} __**{pointType}**__ очков",
                    englishSource,
                    $"{englishSource}: {signedPoints} __**{englishPointType}**__ points",
                    $"❓: {signedPoints} __**{pointType}**__ очков",
                    $"❓: {signedPoints} __**{englishPointType}**__ points",
                    PlayerId) + "\n");
            }
            else if (sourceVisibility == FeedbackSourceVisibility.ProNeutralTarget)
            {
                var englishSource = GameLocalization.Text(skillName, GameLocalization.English);
                AddInGamePersonalLogs(PhrasePayload.EncodeProNeutral(
                    skillName,
                    $"{signedPoints} __**{pointType}**__ очков",
                    englishSource,
                    $"{signedPoints} __**{englishPointType}**__ points") + "\n");
            }
            else
            {
                AddInGamePersonalLogs(
                    $"{displaySource}: {signedPoints} __**{pointType}**__ очков\n");
            }
        }

        Score += bonusPoints;
        BonusPointsEarnedThisRound += bonusPoints;
        ScoreEntries.Add(new ScoreEntry
        {
            Source = skillName,
            Points = bonusPoints,
            IsBonus = true,
            SourceVisibility = sourceVisibility,
        });

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

    public decimal GetPreviousRoundAbilityPoints()
        => GetAbilityPoints(PreviousRoundScoreEntries, ActualRoundMultiplier);

    public decimal GetLifetimeAbilityPoints(GameClass game)
        => LifetimeAbilityPointsEarned
           + GetAbilityPoints(ScoreEntries, GetRoundScoreMultiplier(game));

    private static decimal GetAbilityPoints(
        IEnumerable<ScoreEntry> entries,
        int regularMultiplier)
    {
        var entryList = entries.ToList();
        var bonusPoints = entryList
            .Where(entry => entry.IsBonus && !entry.IsNaturalWin && entry.Points > 0)
            .Sum(entry => entry.Points);
        var regularPoints = entryList
            .Where(entry => !entry.IsBonus
                            && !entry.IsNaturalWin
                            && (entry.Points > 0 || entry.Source == GordonFreeman.HalfLife3))
            .Sum(entry => entry.Points);

        return bonusPoints + Math.Max(0, regularPoints) * regularMultiplier;
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
        WasRoundScoreMultiplierReducedByTolya = false;
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

    public void CombineRoundScoreAndGameScore(
        GameClass game,
        decimal halfLifeSettlementAdjustment = 0)
    {
        // Expected multiplier (before any passive overrides)
        ExpectedRoundMultiplier = game.RoundNo switch
        {
            <= 4 => 1,
            <= 9 => 2,
            _ => 4
        };

        // Actual multiplier (after passive overrides) + snapshot
        var multiplierDisabledByTolya = IsRoundScoreMultiplierDisabledByTolya(game);
        var ordinaryRoundMultiplier = GetRoundScoreMultiplier(game);
        WasRoundScoreMultiplierReducedByTolya =
            multiplierDisabledByTolya && ordinaryRoundMultiplier < ExpectedRoundMultiplier;
        ActualRoundMultiplier = ordinaryRoundMultiplier;
        PreviousRoundScoreEntries = new List<ScoreEntry>(ScoreEntries);
        ScoreEntries.Clear();

        var rawScore = GetScoresToGiveAtEndOfRound();
        if (halfLifeSettlementAdjustment != 0)
        {
            PreviousRoundScoreEntries.Add(new ScoreEntry
            {
                Source = GordonFreeman.HalfLife3,
                Points = halfLifeSettlementAdjustment,
                IsBonus = true,
            });
        }

        LifetimeAbilityPointsEarned += GetAbilityPoints(
            PreviousRoundScoreEntries,
            ActualRoundMultiplier);
        AddScoreWithMultiplier(rawScore, ActualRoundMultiplier);
        ApplyFlatSettlementAdjustment(
            halfLifeSettlementAdjustment,
            GordonFreeman.HalfLife3);
        SetScoresToGiveAtEndOfRound(0, "", false);
        BonusPointsEarnedThisRound = 0;
        ScoreSource = "";
    }

    private void ApplyFlatSettlementAdjustment(decimal score, string source)
    {
        if (score == 0 || score < 0 && UnknownBug.Is(GameCharacter)) return;

        var sign = score > 0 ? "+" : "";
        AddInGamePersonalLogs($"{source}: {sign}{score} **очков**\n");
        Score += score;
        if (Score < 0 && GameCharacter.Passive.All(x => x.PassiveName != "Никому не нужен"))
            Score = 0;
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
        return Score + (HomelanderSevenPointsActive ? Homelander.SevenPoints : 0);
    }

    public void RestoreEternalTsukuyomiScore(
        decimal capturedScore,
        bool homelanderSevenPointsActive)
    {
        HomelanderSevenPointsActive = homelanderSevenPointsActive;
        Score = capturedScore - (homelanderSevenPointsActive ? Homelander.SevenPoints : 0);
        ScoresToGiveAtEndOfRound = 0;
        BonusPointsEarnedThisRound = 0;
        ScoreSource = "";
        ScoreEntries.Clear();
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
    public bool IsNaturalWin { get; set; }
    public FeedbackSourceVisibility SourceVisibility { get; set; }
}
