using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace King_of_the_Garbage_Hill.API.DTOs;

// ── Top-level game state sent to web clients ──────────────────────────

public class GameStateDto
{
    public ulong GameId { get; set; }
    public int RoundNo { get; set; }
    public double TurnLengthInSecond { get; set; }
    public double TimePassedSeconds { get; set; }
    public string GameVersion { get; set; }
    public string GameMode { get; set; }
    public bool IsFinished { get; set; }
    public bool IsAramPickPhase { get; set; }
    public bool IsDraftPickPhase { get; set; }
    public List<DraftOptionDto> DraftOptions { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string DraftPickHeading { get; set; }
    public bool IsKratosEvent { get; set; }
    public bool IsRumblingWarningActive { get; set; }
    public int RumblingKillCount { get; set; }
    public bool IsRoundTransitionPaused { get; set; }
    public string TransitionDeadlineUtc { get; set; }
    public int HalfLifeReleaseSerial { get; set; }
    public int AbyssSerial { get; set; }
    public string GlobalLogs { get; set; }

    /// <summary>Full history of all global logs across all rounds.</summary>
    public string AllGlobalLogs { get; set; }

    /// <summary>The PlayerId of the requesting player, or null for spectators.</summary>
    public Guid? MyPlayerId { get; set; }

    /// <summary>PlayerType of the requesting player (0/1 = normal, 2 = admin, 404 = bot).</summary>
    public int MyPlayerType { get; set; }

    /// <summary>Whether the requesting player has "Prefer Web" enabled (suppresses Discord messages).</summary>
    public bool PreferWeb { get; set; }

    /// <summary>All character names available for predictions.</summary>
    public List<string> AllCharacterNames { get; set; } = new();

    /// <summary>Full character catalog with base stats (for prediction avatar/stat lookup).</summary>
    public List<CharacterInfoDto> AllCharacters { get; set; } = new();

    /// <summary>Player IDs revealed by Коммуникация or Толя (for Pink Ward animation on frontend).</summary>
    public List<Guid> PinkWardRevealedPlayerIds { get; set; } = new();

    public List<PlayerDto> Players { get; set; } = new();
    public List<TeamDto> Teams { get; set; } = new();

    /// <summary>
    /// Full game chronicle for the Летопись tab (populated only when IsFinished = true).
    /// Contains AllGlobalLogs + per-player personal logs with character name headers.
    /// </summary>
    public string FullChronicle { get; set; }

    /// <summary>Structured fight log for the current round (for web fight animation).</summary>
    public List<FightEntryDto> FightLog { get; set; } = new();

    /// <summary>Achievements newly unlocked this game (populated on game finish).</summary>
    public List<AchievementEntryDto> NewlyUnlockedAchievements { get; set; } = new();
}

// ── Per-player state (scoped to the requesting player) ────────────────

public class PlayerDto
{
    public Guid PlayerId { get; set; }
    public string DiscordUsername { get; set; }
    public bool IsBot { get; set; }
    public bool IsWebPlayer { get; set; }
    public int TeamId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsBoardEntity { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsDeepSession { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DepthsCallPromptActive { get; set; }

    /// <summary>Whether this player is another member of the viewing Naruto's initialized trio.</summary>
    public bool IsNarutoAlly { get; set; }

    /// <summary>Whether Madara publicly acknowledged this player as the Red Tiger.</summary>
    public bool IsMadaraRedTiger { get; set; }

    // Character info
    public CharacterDto Character { get; set; }

    // Status
    public PlayerStatusDto Status { get; set; }

    // Predictions (only visible to the owning player)
    public List<PredictDto> Predictions { get; set; }

    /// <summary>Whether this player is dead (killed by any mechanic).</summary>
    public bool IsDead { get; set; }

    /// <summary>Who/what killed this player ("Kratos", "Kira", "Monster", etc.). Empty if alive.</summary>
    public string DeathSource { get; set; }

    /// <summary>Whether this player is Kira (uses Death Note instead of predictions).</summary>
    public bool IsKira { get; set; }

    /// <summary>Whether this is the requesting player's private terminal-mode projection.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsTerminalMode { get; set; }

    /// <summary>Death Note state (only populated for the Kira player viewing their own state).</summary>
    public DeathNoteDto DeathNote { get; set; }

    /// <summary>Portal Gun state (only populated for Rick viewing their own state).</summary>
    public PortalGunDto PortalGun { get; set; }

    /// <summary>Private terminal state, populated only on the owning player's row.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TerminalStateDto TerminalState { get; set; }

    /// <summary>Whether this row is the active private terminal marker.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasTerminalMarker { get; set; }

    /// <summary>Butcher sup marker, serialized only when the viewing player is TheBoys.</summary>
    public bool IsTheBoysSupTarget { get; set; }

    /// <summary>Owner-scoped Homelander rage against this opponent.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? HomelanderRagePercent { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HomelanderLaserUsed { get; set; }

    /// <summary>Tsukuyomi state (only populated for the Itachi player viewing their own state).</summary>
    public TsukuyomiStateDto TsukuyomiState { get; set; }

    /// <summary>Passive ability widget states (only populated for the owning player).</summary>
    public PassiveAbilityStatesDto PassiveAbilityStates { get; set; }

    /// <summary>True when this player is Darksci and hasn't chosen stable/unstable yet (round 1 only).</summary>
    public bool DarksciChoiceNeeded { get; set; }

    /// <summary>True when this player is Gleb and can transform to "Молодой Глеб" (round 1 only).</summary>
    public bool YoungGlebAvailable { get; set; }

    /// <summary>Character mastery points for this player's character (only set for isMe).</summary>
    public int CharacterMasteryPoints { get; set; }

    /// <summary>Custom prefix before the player's place number (e.g. octopus tentacles). Web-friendly HTML.</summary>
    public string CustomLeaderboardPrefix { get; set; }

    /// <summary>Custom leaderboard annotations after the player name, visible to the requesting player
    /// (e.g. weed counts, hunt targets, win streaks from passives). Web-friendly HTML.</summary>
    public string CustomLeaderboardText { get; set; }

    /// <summary>Whether this opponent is within the viewing player's harm (Вред) range.</summary>
    public bool IsInMyHarmRange { get; set; }
}

public class CharacterDto
{
    public string Name { get; set; }
    public string Avatar { get; set; }
    public string AvatarCurrent { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }

    // Stats
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Speed { get; set; }
    public int Psyche { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string StatDisplayOverride { get; set; }

    // Derived
    public string SkillDisplay { get; set; }
    public string MoralDisplay { get; set; }
    public int Justice { get; set; }
    public int SeenJustice { get; set; }
    public string SkillClass { get; set; }
    public string SkillTarget { get; set; }
    public string ClassStatDisplayText { get; set; }

    // Quality resists & bonuses
    public int IntelligenceResist { get; set; }
    public int StrengthResist { get; set; }
    public int SpeedResist { get; set; }
    public int PsycheResist { get; set; }

    /// <summary>Intelligence quality skill bonus text (e.g. "+10% Skill"), empty if none.</summary>
    public string IntelligenceBonusText { get; set; }
    /// <summary>Strength quality drop bonus ("+1 Drop Power"), empty if none.</summary>
    public string StrengthBonusText { get; set; }
    /// <summary>Speed quality kite distance bonus (e.g. "+2 Kite Distance"), empty if none.</summary>
    public string SpeedBonusText { get; set; }
    /// <summary>Psyche quality moral bonus text (e.g. "+20% Moral"), empty if none.</summary>
    public string PsycheBonusText { get; set; }

    // Passives (only visible ones for opponents)
    public List<PassiveDto> Passives { get; set; } = new();
}

public class PassiveDto
{
    public string Name { get; set; }
    public string Description { get; set; }
    public bool Visible { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Theme { get; set; }
}

public class PlayerStatusDto
{
    public decimal Score { get; set; }
    public int Place { get; set; }
    public bool IsReady { get; set; }
    public bool IsBlock { get; set; }
    public bool IsSkip { get; set; }
    public bool IsAutoMove { get; set; }
    public bool ConfirmedPredict { get; set; }
    public bool ConfirmedSkip { get; set; }
    public int LvlUpPoints { get; set; }
    public int MoveListPage { get; set; }
    public string PersonalLogs { get; set; }
    public string PreviousRoundLogs { get; set; }
    public string AllPersonalLogs { get; set; }
    public string ScoreSource { get; set; }

    /// <summary>Direct messages (ephemeral msgs that are sent then deleted in Discord).</summary>
    public List<string> DirectMessages { get; set; } = new();

    /// <summary>Character phrase media messages (text, audio, images from SendLogSeparate/SendLogSeparateWithFile).</summary>
    public List<MediaMessageDto> MediaMessages { get; set; } = new();
    public bool IsAramRollConfirmed { get; set; }
    public bool IsDraftPickConfirmed { get; set; }
    public int AramRerolledPassivesTimes { get; set; }
    public int AramRerolledStatsTimes { get; set; }
    public List<PlaceHistoryDto> PlaceHistory { get; set; } = new();
    public ScoreBreakdownDto ScoreBreakdown { get; set; }
}

public class PlaceHistoryDto
{
    public int Round { get; set; }
    public int Place { get; set; }
}

public class ScoreEntryDto
{
    public string Source { get; set; }
    public decimal Points { get; set; }
    public bool IsBonus { get; set; }
}

public class ScoreBreakdownDto
{
    public int RoundMultiplier { get; set; }
    public int ExpectedRoundMultiplier { get; set; }
    public List<ScoreEntryDto> Entries { get; set; } = new();
}

public class PredictDto
{
    public Guid PlayerId { get; set; }
    public string CharacterName { get; set; }

    /// <summary>Actual character name (populated only at game end).</summary>
    public string ActualCharacterName { get; set; }

    /// <summary>Whether the prediction matches the actual character (populated only at game end).</summary>
    public bool? IsCorrect { get; set; }

    /// <summary>Actual character avatar URL (populated only at game end when prediction was wrong).</summary>
    public string ActualAvatar { get; set; }
}

public class TeamDto
{
    public int TeamId { get; set; }
    public List<Guid> PlayerIds { get; set; } = new();
}

public class MediaMessageDto
{
    /// <summary>Name of the passive/ability that triggered this message.</summary>
    public string PassiveName { get; set; }
    /// <summary>The text/phrase content.</summary>
    public string Text { get; set; }
    /// <summary>Authored English ability name. Null in legacy replay snapshots.</summary>
    public string PassiveNameEnglish { get; set; }
    /// <summary>Authored English phrase. Null in legacy replay snapshots.</summary>
    public string TextEnglish { get; set; }
    /// <summary>URL to the media file (e.g. /art/events/kratos_death.jpg, /sound/Kratos_PLAY_ME.mp3). Null for text-only.</summary>
    public string FileUrl { get; set; }
    /// <summary>One of: "text", "audio", "image"</summary>
    public string FileType { get; set; } = "text";
    /// <summary>How many rounds this media should play. Audio with &gt;1 loops across rounds.</summary>
    public int RoundsToPlay { get; set; } = 1;
}

// ── Request DTOs ──────────────────────────────────────────────────────

public class AttackRequest
{
    public int TargetPlace { get; set; }
}

public class LevelUpRequest
{
    /// <summary>1=Intelligence, 2=Strength, 3=Speed, 4=Psyche</summary>
    public int StatIndex { get; set; }
}

public class GordonHalfLifeDecisionRequest
{
    public int Serial { get; set; }
    public string Choice { get; set; }
}

public class PredictRequest
{
    public Guid TargetPlayerId { get; set; }
    public string CharacterName { get; set; }
}

public class AramRerollRequest
{
    /// <summary>1-4 for passive slots, 5 for base stats</summary>
    public int Slot { get; set; }
}

public class DraftOptionDto
{
    public string Name { get; set; }
    public string Avatar { get; set; }
    public int Intelligence { get; set; }
    public int Psyche { get; set; }
    public int Speed { get; set; }
    public int Strength { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }
    public int Cost { get; set; }
    public List<PassiveDto> Passives { get; set; } = new();
}

public class StartGameRequest
{
    public string Mode { get; set; } = "Normal";
}

// ── Lobby DTOs ────────────────────────────────────────────────────────

public class LobbyStateDto
{
    public int ActiveGames { get; set; }
    public List<ActiveGameDto> Games { get; set; } = new();
    public List<CharacterInfoDto> AvailableCharacters { get; set; } = new();
}

public class ActiveGameDto
{
    public ulong GameId { get; set; }
    public int RoundNo { get; set; }
    public int PlayerCount { get; set; }
    public int HumanCount { get; set; }
    public string GameMode { get; set; }
    public bool IsFinished { get; set; }
    public int BotCount { get; set; }
    public bool CanJoin { get; set; }
}

public class CharacterInfoDto
{
    public string Name { get; set; }
    public string Avatar { get; set; }
    public string Description { get; set; }
    public int Tier { get; set; }

    // Base stats — used by non-admin players to fill in predicted character info
    public int Intelligence { get; set; }
    public int Strength { get; set; }
    public int Speed { get; set; }
    public int Psyche { get; set; }
}

// ── Fight Animation DTOs ──────────────────────────────────────────────

public class FightEntryDto
{
    internal FightEntryDto CopyForProjection() => (FightEntryDto)MemberwiseClone();

    // Participants
    public string AttackerName { get; set; }
    public string AttackerCharName { get; set; }
    public string AttackerAvatar { get; set; }
    public string DefenderName { get; set; }
    public string DefenderCharName { get; set; }
    public string DefenderAvatar { get; set; }

    /// <summary>"win" (attacker wins), "loss" (defender wins), "block", "skip"</summary>
    public string Outcome { get; set; }
    public string WinnerName { get; set; }

    // Class info for Nemesis/Versatility display
    public string AttackerClass { get; set; }
    public string DefenderClass { get; set; }
    // Original class before ForOneFight stat overrides (empty if unchanged)
    public string AttackerOriginalClass { get; set; }
    public string DefenderOriginalClass { get; set; }

    // Versatility stat breakdown (+1 attacker better, -1 defender better, 0 equal)
    public int VersatilityIntel { get; set; }
    public int VersatilityStr { get; set; }
    public int VersatilitySpeed { get; set; }

    // Step1: Stats calculation from CalculateStep1
    public decimal ScaleMe { get; set; }
    public decimal ScaleTarget { get; set; }
    public bool IsNemesisMe { get; set; }
    public bool IsNemesisTarget { get; set; }
    public decimal NemesisMultiplier { get; set; }
    public int SkillMultiplierMe { get; set; }
    public int SkillMultiplierTarget { get; set; }
    public int PsycheDifference { get; set; }
    public decimal WeighingMachine { get; set; }
    public bool IsTooGoodMe { get; set; }
    public bool IsTooGoodEnemy { get; set; }
    public bool IsTooStronkMe { get; set; }
    public bool IsTooStronkEnemy { get; set; }
    public bool IsStatsBetterMe { get; set; }
    public bool IsStatsBetterEnemy { get; set; }
    public decimal RandomForPoint { get; set; }

    // Round 1 per-step weighing deltas (for web fight animation)
    public decimal NemesisWeighingDelta { get; set; }
    public decimal ScaleWeighingDelta { get; set; }
    public decimal VersatilityWeighingDelta { get; set; }
    public decimal PsycheWeighingDelta { get; set; }
    public decimal SkillWeighingDelta { get; set; }
    public decimal JusticeWeighingDelta { get; set; }

    // Round 3 random modifiers
    /// <summary>How TooGood shifted randomForPoint (e.g. +25 or -25), 0 if not triggered.</summary>
    public decimal TooGoodRandomChange { get; set; }
    /// <summary>How TooStronk shifted randomForPoint, 0 if not triggered.</summary>
    public decimal TooStronkRandomChange { get; set; }
    /// <summary>How pure Justice difference shifted maxRandomNumber in Step3: (meJustice - targetJustice) * 5, 0 if not triggered.</summary>
    public decimal JusticeRandomChange { get; set; }
    /// <summary>How NemesisMultiplier shifted maxRandomNumber in Step3: meJustice * (nemesisMultiplier - 1) * 5, 0 if not triggered.</summary>
    public decimal NemesisRandomChange { get; set; }

    /// <summary>Points won from Round 1 alone (before Step2/Step3).</summary>
    public int Round1PointsWon { get; set; }

    // Step2: Justice comparison
    public int JusticeMe { get; set; }
    public int JusticeTarget { get; set; }
    public int PointsFromJustice { get; set; }

    // Step3: Random roll (only if tie after step1+step2)
    public bool UsedRandomRoll { get; set; }
    public int RandomNumber { get; set; }
    public decimal MaxRandomNumber { get; set; }

    // Final
    public int TotalPointsWon { get; set; }
    /// <summary>Deprecated: raw moral calc. Use AttackerMoralChange/DefenderMoralChange instead.</summary>
    public int MoralChange { get; set; }
    /// <summary>Actual moral change received by the attacker (after passive checks). Can be 0 if blocked.</summary>
    public decimal AttackerMoralChange { get; set; }
    /// <summary>Actual moral change received by the defender (after passive checks). Can be 0 if blocked.</summary>
    public decimal DefenderMoralChange { get; set; }

    // Fight outcome details (resist damage to the loser)
    /// <summary>Intelligence resist lost by the loser.</summary>
    public int ResistIntelDamage { get; set; }
    /// <summary>Strength resist lost by the loser.</summary>
    public int ResistStrDamage { get; set; }
    /// <summary>Psyche resist lost by the loser.</summary>
    public int ResistPsycheDamage { get; set; }
    /// <summary>Number of score drops (-1 each) suffered by the loser.</summary>
    public int Drops { get; set; }
    /// <summary>Discord username of the player who got dropped.</summary>
    public string DroppedPlayerName { get; set; }
    /// <summary>Whether quality resist damage was applied this fight.</summary>
    public bool QualityDamageApplied { get; set; }
    /// <summary>Whether intelligence resist broke (IntelligenceQualitySkillBonus decreased).</summary>
    public bool IntellectualDamage { get; set; }
    /// <summary>Whether psyche resist broke (PsycheQualityMoralBonus decreased).</summary>
    public bool EmotionalDamage { get; set; }
    /// <summary>Justice gained by the loser of this fight (+1), 0 if teammate fight.</summary>
    public int JusticeChange { get; set; }
    /// <summary>Skill gained by the attacker from hitting their SkillTarget class. 0 if not applicable.</summary>
    public decimal SkillGainedFromTarget { get; set; }
    /// <summary>Skill gained by the attacker from  their Skill Class. 0 if not applicable.</summary>
    public decimal SkillGainedFromClassAttacker { get; set; }
    /// <summary>Skill gained by the defender from  their Skill Class. 0 if not applicable.</summary>
    public decimal SkillGainedFromClassDefender { get; set; }

    /// <summary>Skill difference random modifier (based on skill difference between attacker and defender).</summary>
    public decimal SkillDifferenceRandomModifier { get; set; }

    /// <summary>How much skill is added from counter.</summary>
    public decimal NemesisMultiplierSkillDifference { get; set; }

    /// <summary>ForOneFight stat modifications applied to the attacker this fight.</summary>
    public List<ForOneFightModDto> AttackerForOneFightMods { get; set; } = new();
    /// <summary>ForOneFight stat modifications applied to the defender this fight.</summary>
    public List<ForOneFightModDto> DefenderForOneFightMods { get; set; } = new();

    /// <summary>When true, this fight entry is hidden from non-admin players (e.g. Saitama solo kills).</summary>
    public bool HiddenFromNonAdmin { get; set; }
    /// <summary>When true, this is Dopa's second, shadow action.</summary>
    public bool ShadowAction { get; set; }

    /// <summary>Whether a Portal Gun swap occurred in this fight (Rick swaps leaderboard position with the loser).</summary>
    public bool PortalGunSwap { get; set; }

    /// <summary>Whether Storm (Котики) appeared in this fight via "Рандомное поведение".</summary>
    public bool StormAppeared { get; set; }
    /// <summary>How much Storm shifted the weighing machine (+5 or -5).</summary>
    public decimal StormWeighingDelta { get; set; }
    /// <summary>Whether Storm's intervention flipped the fight outcome.</summary>
    public bool StormFlipped { get; set; }

    /// <summary>Whether Homelander resolved this attack with his charged eye laser.</summary>
    public bool HomelanderLaser { get; set; }
}

// ── ForOneFight Mod DTOs ─────────────────────────────────────────────

public class ForOneFightModDto
{
    public string Source { get; set; }
    public string Stat { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal NewValue { get; set; }
    public bool IsOnEnemy { get; set; }
}

// ── Portal Gun DTOs ──────────────────────────────────────────────────

public class PortalGunDto
{
    public bool Invented { get; set; }
    public int Charges { get; set; }
}

// ── Private terminal DTOs ───────────────────────────────────────────

public class TerminalStateDto
{
    public int BufferedPoints { get; set; }
    public Guid? StreamTargetPlayerId { get; set; }
    public Guid? ActiveNodePlayerId { get; set; }
    public bool IsNodeActive { get; set; }
    public int CommitSerial { get; set; }
    public int LastCommitPoints { get; set; }
}

// ── Tsukuyomi DTOs ──────────────────────────────────────────────────

public class TsukuyomiStateDto
{
    public int ChargeCounter { get; set; }
    public bool IsReady { get; set; }
    public decimal TotalStolenPoints { get; set; }
}

// ── Death Note DTOs ──────────────────────────────────────────────────

public class DeathNoteDto
{
    public Guid CurrentRoundTarget { get; set; }
    public string CurrentRoundName { get; set; }
    public List<DeathNoteEntryDto> Entries { get; set; } = new();
    public List<Guid> FailedTargets { get; set; } = new();

    /// <summary>The player designated as L.</summary>
    public Guid LPlayerId { get; set; }
    public bool IsArrested { get; set; }
    public bool ShinigamiEyesActive { get; set; }

    /// <summary>Players whose character names have been revealed by Shinigami Eyes.</summary>
    public List<DeathNoteRevealedPlayerDto> RevealedPlayers { get; set; } = new();
}

public class DeathNoteEntryDto
{
    public Guid TargetPlayerId { get; set; }
    public string WrittenName { get; set; }
    public int RoundWritten { get; set; }
    public bool WasCorrect { get; set; }
}

public class DeathNoteRevealedPlayerDto
{
    public Guid PlayerId { get; set; }
    public string CharacterName { get; set; }
}

// ── Passive Ability Widget DTOs ───────────────────────────────────────

public class PassiveAbilityStatesDto
{
    public BulkStateDto Bulk { get; set; }
    public TeaStateDto Tea { get; set; }
    public JewStateDto Jew { get; set; }
    public HardKittyStateDto HardKitty { get; set; }
    public TrainingStateDto Training { get; set; }
    public DragonStateDto Dragon { get; set; }
    public GarbageStateDto Garbage { get; set; }
    public CopycatStateDto Copycat { get; set; }
    public InkScreenStateDto InkScreen { get; set; }
    public TigerTopStateDto TigerTop { get; set; }
    public JawsStateDto Jaws { get; set; }
    public PrivilegeStateDto Privilege { get; set; }
    public VampirismStateDto Vampirism { get; set; }
    public WeedStateDto Weed { get; set; }
    public SaitamaStateDto Saitama { get; set; }
    public ShinigamiEyesWidgetDto ShinigamiEyes { get; set; }
    public SellerStateDto Seller { get; set; }
    public DopaStateDto Dopa { get; set; }
    public GoblinSwarmStateDto GoblinSwarm { get; set; }
    public KotikiStateDto Kotiki { get; set; }
    public MonsterStateDto Monster { get; set; }
    public PickleRickStateDto PickleRick { get; set; }
    public GiantBeansStateDto GiantBeans { get; set; }
    public TolyaCountStateDto TolyaCount { get; set; }
    public ImpactStateDto Impact { get; set; }
    public DarksciStateDto Darksci { get; set; }
    public DeepListStateDto DeepList { get; set; }
    public CraboRackStateDto CraboRack { get; set; }
    public NapoleonStateDto Napoleon { get; set; }
    public SupportStateDto Support { get; set; }
    public ToxicMateStateDto ToxicMate { get; set; }
    public YongGlebStateDto YongGleb { get; set; }
    public TheBoysStateDto TheBoys { get; set; }
    public SalldorumStateDto Salldorum { get; set; }
    public GeraltStateDto Geralt { get; set; }
    public DoomGuyStateDto DoomGuy { get; set; }
    public ErenStateDto Eren { get; set; }
    public NarutoStateDto Naruto { get; set; }
    public GordonStateDto Gordon { get; set; }
    public JonSnowStateDto JonSnow { get; set; }
}

public class JonSnowStateDto
{
    public decimal Skill { get; set; }
    public int SkillTarget { get; set; }
    public bool IsKing { get; set; }
    public bool KingBlockedByCastle { get; set; }
    public int BastardIntelligenceBonus { get; set; }
    public bool BlackCastleActive { get; set; }
    public int BlackCastleTurnsRemaining { get; set; }
    public bool WatchEnded { get; set; }
    public int LoyaltyVictories { get; set; }
    public List<JonSnowWeakestPlayerDto> WeakestPlayers { get; set; } = new();
}

public class JonSnowWeakestPlayerDto
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; }
}

public class GordonStateDto
{
    public int ResolvedFights { get; set; }
    public int CrowbarProgress { get; set; }
    public bool WakeUsed { get; set; }
    public bool CanWake { get; set; }
    public bool WakeReservedForTsukuyomi { get; set; }
    public int HeadcrabsRemoved { get; set; }
    public int ZombieCount { get; set; }
    public List<GordonHeadcrabDto> ActiveHeadcrabs { get; set; } = new();
    public GordonHalfLifeStateDto HalfLife { get; set; }
}

public class GordonHeadcrabDto
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; }
    public int RoundsLeft { get; set; }
}

public class GordonHalfLifeStateDto
{
    public bool Announced { get; set; }
    public bool Finished { get; set; }
    public bool Released { get; set; }
    public int Postponements { get; set; }
    public bool CanAnnounce { get; set; }
    public bool PendingDecision { get; set; }
    public string DecisionKind { get; set; }
    public int DecisionSerial { get; set; }
    public string DeadlineUtc { get; set; }
    public decimal RawPoints { get; set; }
    public bool SuperMultiplierDisabled { get; set; }
    public decimal Exponent { get; set; }
    public decimal FinalPoints { get; set; }
    public string FreezeLabel { get; set; }
    public string PostponeLabel { get; set; }
    public string DecisionMessage { get; set; }
}

public class BulkStateDto
{
    public int DrownChance { get; set; }
    public bool IsBuffed { get; set; }
}

public class TeaStateDto
{
    public bool IsReady { get; set; }
}

public class JewStateDto
{
    public int StolenPsyche { get; set; }
}

public class HardKittyStateDto
{
    public int FriendsCount { get; set; }
}

public class TrainingStateDto
{
    public int CurrentStatIndex { get; set; }
    public string StatName { get; set; }
    public int TargetStatValue { get; set; }
}

public class DragonStateDto
{
    public bool IsAwakened { get; set; }
    public int RoundsUntilAwaken { get; set; }
}

public class GarbageStateDto
{
    public int MarkedCount { get; set; }
    public int TotalTracked { get; set; }
}

public class CopycatStateDto
{
    public string CopiedStatName { get; set; }
    public int HistoryCount { get; set; }
}

public class InkScreenStateDto
{
    public int FakeDefeatCount { get; set; }
    public int TotalDeferredScore { get; set; }
}

public class TigerTopStateDto
{
    public bool IsActive { get; set; }
    public int SwapsRemaining { get; set; }
}

public class JawsStateDto
{
    public int CurrentSpeed { get; set; }
    public int UniqueDefeated { get; set; }
    public int UniquePositions { get; set; }
}

public class PrivilegeStateDto
{
    public int MarkedCount { get; set; }
    public List<string> MarkedNames { get; set; }
}

public class VampirismStateDto
{
    public int ActiveFeeds { get; set; }
    public int IgnoredJustice { get; set; }
}

public class WeedStateDto
{
    public int TotalWeedAvailable { get; set; }
    public int LastHarvestRound { get; set; }
}

public class SaitamaStateDto
{
    public int DeferredPoints { get; set; }
    public decimal DeferredMoral { get; set; }
}

public class ShinigamiEyesWidgetDto
{
    public bool IsActive { get; set; }
}

public class SellerStateDto
{
    public int Cooldown { get; set; }
    public int MarkedCount { get; set; }
    public decimal SecretBuildSkill { get; set; }
}

public class DopaStateDto
{
    public bool VisionReady { get; set; }
    public int VisionCooldown { get; set; }
    public string ChosenTactic { get; set; }
    public bool MetaChoiceReady { get; set; }
    public bool NeedSecondAttack { get; set; }
}

public class GoblinSwarmStateDto
{
    public int TotalGoblins { get; set; }
    public int Warriors { get; set; }
    public int Hobs { get; set; }
    public int Workers { get; set; }
    public int HobRate { get; set; }
    public int WarriorRate { get; set; }
    public int WorkerRate { get; set; }
    public int HobUpgradeLevel { get; set; }
    public int WarriorUpgradeLevel { get; set; }
    public int WorkerUpgradeLevel { get; set; }
    public List<int> ZigguratPositions { get; set; }
    public bool IsInZiggurat { get; set; }
    public bool FestivalUsed { get; set; }
}

public class KotikiStateDto
{
    public int TauntedCount { get; set; }
    public int TauntedMax { get; set; }
    public string MinkaOnPlayerName { get; set; }
    public string StormOnPlayerName { get; set; }
    public int MinkaCooldown { get; set; }
    public int StormCooldown { get; set; }
    public int MinkaRoundsOnEnemy { get; set; }
}

// ── Монстр без имени DTOs ────────────────────────────────────────────

public class MonsterStateDto
{
    public int PawnCount { get; set; }
}

// ── Эрен Йегер DTOs ─────────────────────────────────────────────────

public class ErenStateDto
{
    public int RageGained { get; set; }
    public int Losses { get; set; }
    public bool AttackTitanActive { get; set; }
    public int AttackTitanCooldown { get; set; }
    public int AttackTitanSoundSerial { get; set; }
    public int TatakeSoundSerial { get; set; }
    public bool RumblingTriggered { get; set; }
    public int RumblingPlace { get; set; }
    public List<ErenHatredMarkDto> HatredMarks { get; set; } = new();
}

public class ErenHatredMarkDto
{
    public string PlayerName { get; set; }
    public int Marks { get; set; }
}

// ── Наруто DTOs ──────────────────────────────────────────────────────

public class NarutoStateDto
{
    public bool HaremActive { get; set; }
    public int HaremCooldown { get; set; }
}

// ── Rick Sanchez Passive DTOs ────────────────────────────────────────

public class PickleRickStateDto
{
    public int PickleTurnsRemaining { get; set; }
    public bool WasAttackedAsPickle { get; set; }
    public int PenaltyTurnsRemaining { get; set; }
}

public class GiantBeansStateDto
{
    public int BeanStacks { get; set; }
    public bool IngredientsActive { get; set; }
    public int IngredientTargetCount { get; set; }
}

public class TolyaCountStateDto
{
    public bool IsReady { get; set; }
    public int Cooldown { get; set; }
}

public class ImpactStateDto
{
    public int Streak { get; set; }
}

public class DarksciStateDto
{
    public bool IsStableType { get; set; }
    public bool TypeChosen { get; set; }
    public int UniqueEnemiesLeft { get; set; }
}

public class DeepListStateDto
{
    public int KnownCount { get; set; }
    public int MockeryTriggered { get; set; }
}

public class CraboRackStateDto
{
    public int ShellsUsed { get; set; }
}

public class NapoleonStateDto
{
    public string AllyName { get; set; }
    public int TreatyCount { get; set; }
}

public class SupportStateDto
{
    public string CarryName { get; set; }
}

public class ToxicMateStateDto
{
    public bool CancerActive { get; set; }
    public int TransferCount { get; set; }
    public string CurrentHolderName { get; set; }
}

public class YongGlebStateDto
{
    public bool TeaReady { get; set; }
    public int TeaCooldown { get; set; }
}

// ── Blackjack DTOs ────────────────────────────────────────────────────

public class BlackjackTableStateDto
{
    public string Phase { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public string DealerName { get; set; }
    public List<BlackjackCardDto> DealerHand { get; set; } = new();
    public int DealerTotal { get; set; }
    public BlackjackMessageDto LastMessage { get; set; }
    public List<WordCategoryDto> WordCategories { get; set; } = new();
    public List<BlackjackPlayerDto> Players { get; set; } = new();
}

public class BlackjackPlayerDto
{
    public string DiscordId { get; set; }
    public string Username { get; set; }
    public List<BlackjackCardDto> Hand { get; set; } = new();
    public int Total { get; set; }
    public string Status { get; set; }
    public string Result { get; set; }
    public int Wins { get; set; }
    public bool IsCurrentTurn { get; set; }
    public bool IsMe { get; set; }
    public bool CanSendMessage { get; set; }
}

public class BlackjackCardDto
{
    public string Suit { get; set; }
    public string Rank { get; set; }
    public bool FaceUp { get; set; }
}

public class BlackjackMessageDto
{
    public string Author { get; set; }
    public string Text { get; set; }
}

public class WordCategoryDto
{
    public string Name { get; set; }
    public List<string> Words { get; set; } = new();
}

// ── TheBoys DTOs ─────────────────────────────────────────────────────

public class TheBoysStateDto
{
    // Francie
    public int ChemWeaponLevel { get; set; }
    public string OrderTargetName { get; set; }
    public int OrderRoundsLeft { get; set; }
    public int OrdersCompleted { get; set; }
    public int OrdersFailed { get; set; }
    public bool VirusArmed { get; set; }
    public bool VirusUsed { get; set; }
    // Butcher
    public int PokerCount { get; set; }
    public bool SuperDickActive { get; set; }
    public bool ButcherLeft { get; set; }
    public string ActiveCombination { get; set; }
    // Kimiko
    public int RegenLevel { get; set; }
    public bool KimikoDisabled { get; set; }
    public int TotalJusticeBlocked { get; set; }
    public bool LivingWeapon { get; set; }
    // M.M.
    public int MMUpgradeLevel { get; set; }
    public int KompromatCount { get; set; }
    public bool NextAttackGathersKompromat { get; set; }
    public bool IsCalm { get; set; }
    public List<TheBoysKompromatEntryDto> KompromatEntries { get; set; }
    // UI signals (front follows serial changes to fire cinematic reveal/unlock animations)
    public string LastRevealedMember { get; set; }
    public int RevealSerial { get; set; }
    public string LastUnlockedAbility { get; set; }
    public bool LastUnlockWasCombination { get; set; }
    public int UnlockSerial { get; set; }
    // Enemies the Boys player currently sees infected (owner view)
    public List<string> VirusNames { get; set; }
}

public class TheBoysKompromatEntryDto
{
    public string TargetName { get; set; }
    public string Hint { get; set; }
}

// ── Salldorum DTOs ──────────────────────────────────────────────────

public class SalldorumStateDto
{
    public int ShenCharges { get; set; }
    public bool ColaBuried { get; set; }
    public int ColaBuriedPosition { get; set; }
    public int ColaBuriedRound { get; set; }
    public bool ColaReady { get; set; }
    public int ColaReadyRound { get; set; }
    public int ColaDrinks { get; set; }
    public bool HistoryRewritten { get; set; }
    public int RewrittenRound { get; set; }
    public List<int> PositionHistory { get; set; } = new();
}

// ── Auth DTOs ─────────────────────────────────────────────────────────

public class AuthResponse
{
    public bool Success { get; set; }
    public string Token { get; set; }
    public ulong DiscordId { get; set; }
    public string Username { get; set; }
    public string Error { get; set; }
}

public class InvoiceLineItemDto
{
    public string Label { get; set; }
    public int Points { get; set; }
}

public class GeraltStateDto
{
    // Contracts
    public int DrownersContracts { get; set; }
    public int WerewolvesContracts { get; set; }
    public int VampiresContracts { get; set; }
    public int DragonsContracts { get; set; }

    // Oil tiers (0-3)
    public int DrownersOilTier { get; set; }
    public int WerewolvesOilTier { get; set; }
    public int VampiresOilTier { get; set; }
    public int DragonsOilTier { get; set; }
    public bool IsOilApplied { get; set; }

    // Meditation
    public int RevealedCount { get; set; }
    public bool LambertUsed { get; set; }
    public bool LambertActive { get; set; }

    // Enemy type assignments (enemyName → type)
    public Dictionary<string, string> EnemyMonsterTypes { get; set; } = new();

    // Contract demand
    public int Displeasure { get; set; }
    public bool CanDemandPrevious { get; set; }
    public bool CanDemandNext { get; set; }
    public bool DemandedThisPhase { get; set; }
    public bool AdvancePending { get; set; }

    // Invoice (populated when CanDemandPrevious)
    public List<InvoiceLineItemDto> InvoiceItems { get; set; }
    public int? InvoiceTotal { get; set; }
    public int? InvoicePredictedCoins { get; set; }
    public int? InvoicePredictedDispleasure { get; set; }
    public bool QuestCompletedThisRound { get; set; }
    public bool RareLootFoundThisRound { get; set; }
}

public class DoomGuyStateDto
{
    public bool RollMode { get; set; }
    public bool RollAvailable { get; set; }
    public string CurrentStage { get; set; }
    public List<DoomModuleDto> CurrentOptions { get; set; } = new();
    public Dictionary<string, string> ActiveModules { get; set; } = new();
    public List<string> DemonNestNames { get; set; } = new();
    public bool BfgCharged { get; set; }
    public bool RailgunCharged { get; set; }
    public int AscensionIntelligenceRemaining { get; set; }
    public int ManeuversSpeedRemaining { get; set; }
    public int ExterminationVictories { get; set; }
    public bool ExterminationAwarded { get; set; }
    public bool ShockShieldUsed { get; set; }
    public int BlocksThisRound { get; set; }
    public bool HellBlockUsed { get; set; }
    public List<string> CounterAttackMarkedNames { get; set; } = new();
    public bool SharkShieldActive { get; set; }
    public bool EverBlocked { get; set; }
    public bool EverLost { get; set; }
    public bool BecomeGodAwarded { get; set; }
    public bool ChainsawSpent { get; set; }
    public int ChainsawSelectionsRemaining { get; set; }
    public List<DoomCopiedPassiveDto> ChainsawChoices { get; set; } = new();
    public string CopiedPassiveName { get; set; }
    public List<string> CopiedPassiveNames { get; set; } = new();
}

public class DoomModuleDto
{
    public string Name { get; set; }
    public string Stage { get; set; }
    public string Description { get; set; }
    public bool Reward { get; set; }
}

public class DoomCopiedPassiveDto
{
    public string Name { get; set; }
    public string Description { get; set; }
}

public class DoomFortressStateDto
{
    public List<DoomFortressStageDto> Stages { get; set; } = new();
}

public class DoomFortressStageDto
{
    public string Name { get; set; }
    public List<string> Slots { get; set; } = new();
    public List<DoomModuleDto> UnlockedModules { get; set; } = new();
    public int RewardModulesRemaining { get; set; }
    public double CurrentDropChance { get; set; }
}

// ── Character Store DTOs ─────────────────────────────────────────────

public class StoreStateDto
{
    public int ZbsPoints { get; set; }
    public int BasePrice { get; set; }
    public double MinMultiplier { get; set; }
    public double MaxMultiplier { get; set; }
    public int TotalInvestedZbs { get; set; }
    public List<StoreCharacterDto> Characters { get; set; } = new();
}

public class StoreCharacterDto
{
    public string Name { get; set; }
    public string Avatar { get; set; }
    public int Tier { get; set; }
    public double Multiplier { get; set; }
    public int LootBoxBonusPercentagePoints { get; set; }
    public int Changes { get; set; }
    public int CostOne { get; set; }
    public int CostTen { get; set; }
    public int RefundZbs { get; set; }
}

// ── Quest & Loot Box DTOs ────────────────────────────────────────────

public class QuestStateDto
{
    public List<QuestProgressDto> Quests { get; set; } = new();
    public string ActiveDate { get; set; }
    public string ServerNow { get; set; }
    public string ResetsAt { get; set; }
    public bool AllCompletedToday { get; set; }
    public bool DailyCompleted { get; set; }
    public int CompletedQuestCount { get; set; }
    public int DailyQuestRequirement { get; set; }
    public int DailyBonusZbs { get; set; }
    public bool DailyBonusGranted { get; set; }
    public int MasteryBonusLootBoxes { get; set; }
    public bool MasteryBonusGranted { get; set; }
    public int RerollsRemaining { get; set; }
    public int StreakDays { get; set; }
    public int BestStreakDays { get; set; }
    public int WeeklyCompletedDays { get; set; }
    public int WeeklyTargetDays { get; set; }
    public int WeeklyRewardZbs { get; set; }
    public bool WeeklyRewardGranted { get; set; }
    public string WeekEndsAt { get; set; }
    public int ZbsPoints { get; set; }
    public int PendingLootBoxes { get; set; }
    public int LootBoxPity { get; set; }
    public int GuaranteedRareIn { get; set; }
    public List<LootBoxOddsDto> LootBoxOdds { get; set; } = new();
    public LootBoxResultDto LastUnacknowledgedLootBox { get; set; }
    public int PendingGuaranteedCharacters { get; set; }
    public string NextGuaranteedCharacterName { get; set; }
}

public class QuestProgressDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public string Description { get; set; }
    public string DescriptionRu { get; set; }
    public string Lane { get; set; }
    public string Icon { get; set; }
    public string Aggregation { get; set; }
    public int Current { get; set; }
    public int Target { get; set; }
    public bool IsCompleted { get; set; }
    public int ZbsReward { get; set; }
    public int RewardLootBoxes { get; set; }
    public bool RewardGranted { get; set; }
    public string CompletedAt { get; set; }
    public bool CanReroll { get; set; }
}

public class LootBoxResultDto
{
    public string OpeningId { get; set; }
    public string Rarity { get; set; }
    public int ZbsAmount { get; set; }
    public int ZbsBalance { get; set; }
    public int RemainingLootBoxes { get; set; }
    public string OpenedAt { get; set; }
    public bool WasPityUpgrade { get; set; }
    public int LootBoxPity { get; set; }
    public int GuaranteedRareIn { get; set; }
    public string CharacterName { get; set; }
    public string CharacterAvatar { get; set; }
    public int CharacterTier { get; set; }
    public int RollWeightBonusPercentagePoints { get; set; }
    public bool GuaranteedForNextGame { get; set; }
    public int PendingGuaranteedCharacters { get; set; }
}

public class LootBoxOddsDto
{
    public string Rarity { get; set; }
    public double Chance { get; set; }
    public int MinZbs { get; set; }
    public int MaxZbs { get; set; }
    public int RollWeightBonusPercentagePoints { get; set; }
    public int? GuaranteedCharacterMaxTier { get; set; }
}

// ── Achievement DTOs ─────────────────────────────────────────────────

public class AchievementBoardDto
{
    public List<AchievementEntryDto> Achievements { get; set; } = new();
    public int TotalUnlocked { get; set; }
    public int TotalAchievements { get; set; }
    public List<string> NewlyUnlocked { get; set; } = new();
    public int EarnedRewardZbs { get; set; }
    public int TotalRewardZbs { get; set; }
    public int EarnedRewardLootBoxes { get; set; }
    public int TotalRewardLootBoxes { get; set; }
}

public class AchievementEntryDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NameRu { get; set; }
    public string Description { get; set; }
    public string DescriptionRu { get; set; }
    public string SecretHint { get; set; }
    public string SecretHintRu { get; set; }
    public string Category { get; set; }
    public bool IsSecret { get; set; }
    public string Icon { get; set; }
    public string Rarity { get; set; }
    public List<string> CharacterNames { get; set; } = new();
    public int RewardZbs { get; set; }
    public int RewardLootBoxes { get; set; }
    public int Target { get; set; }
    public int Current { get; set; }
    public bool IsUnlocked { get; set; }
    public string UnlockedAt { get; set; }
}
