import * as signalR from '@microsoft/signalr'

const HUB_URL = import.meta.env.VITE_SIGNALR_HUB || '/gamehub'

export type GameState = {
  gameId: number
  roundNo: number
  turnLengthInSecond: number
  timePassedSeconds: number
  gameVersion: string
  gameMode: string
  isFinished: boolean
  isAramPickPhase: boolean
  isDraftPickPhase: boolean
  draftOptions: DraftOptionDto[] | null
  draftPickHeading?: string
  isKratosEvent: boolean
  /** True while the round pipeline is waiting for an inter-round character decision. */
  isRoundTransitionPaused: boolean
  /** Server-authoritative deadline for the current transition decision. */
  transitionDeadlineUtc?: string
  /** Public monotonic event identity for the Halflife 3 release celebration. */
  halfLifeReleaseSerial: number
  /** Public monotonic identity for the full-screen abyss transition. */
  abyssSerial: number
  /** Public monotonic identity for the Viltrumite invasion ending. */
  omniManInvasionSerial: number
  /** Omni-man-owner-only identity and phrase for the Подземный Поезд overlay. */
  omniManUndergroundTrainSerial?: number
  omniManUndergroundTrainPhrase?: string
  isRumblingWarningActive: boolean
  /** Persistent public Rumbling aftermath intensity, clamped to 0..4 victims. */
  rumblingKillCount: number
  globalLogs: string
  /** Full history of global logs across all rounds */
  allGlobalLogs: string
  /** Full game chronicle (global events + all players' personal logs). Only set when isFinished. */
  fullChronicle?: string
  /** The PlayerId of the requesting player, or null for spectators */
  myPlayerId: string | null
  /** PlayerType: 0/1 = normal, 2 = admin, 404 = bot */
  myPlayerType: number
  /** Match-scoped Pro rules, including a temporary PRO-tier character override. */
  isProMode: boolean
  /** Whether this player has "Prefer Web" enabled (suppresses Discord messages) */
  preferWeb: boolean
  /** All character names for prediction dropdowns */
  allCharacterNames: string[]
  /** Full character catalog with base stats for prediction lookup */
  allCharacters: CharacterInfo[]
  /** Player IDs revealed by Коммуникация or Толя (for Pink Ward animation). */
  pinkWardRevealedPlayerIds?: string[]
  players: Player[]
  teams: Team[]
  /** Structured fight log for the current round (for fight animation) */
  fightLog: FightEntry[]
  /** Achievements newly unlocked this game (populated on game finish) */
  newlyUnlockedAchievements?: AchievementEntry[]
}

export type Player = {
  playerId: string
  discordUsername: string
  isBot: boolean
  isWebPlayer: boolean
  teamId: number
  /** Synthetic public board row rather than a roster participant. */
  isBoardEntity?: boolean
  /** Owner-only presentation flag for the abyssal session theme. */
  isDeepSession?: boolean
  /** Owner-only pre-game binary prompt. */
  depthsCallPromptActive?: boolean
  /** Owner-only round-one action that opens the Cthulhu adept ritual. */
  adeptChoiceAvailable?: boolean
  /** Whether this player is another member of the viewing Naruto's initialized trio. */
  isNarutoAlly: boolean
  /** Public recognition awarded by Madara after the Red Tiger phrase. */
  isMadaraRedTiger: boolean
  /** Whether this player is dead (killed by any mechanic). */
  isDead: boolean
  /** Who/what killed this player ("Kratos", "Kira", "Monster", etc.). Empty if alive. */
  deathSource: string
  /** Whether this player is Kira (uses Death Note instead of predictions). */
  isKira: boolean
  /** Enables the owner-only terminal presentation and private fight projection. */
  isTerminalMode: boolean
  /** Death Note state (only populated for the Kira player). */
  deathNote?: DeathNote
  /** Portal Gun state (only populated for Rick). */
  portalGun?: PortalGun
  /** Owner-only terminal state. */
  terminalState?: TerminalState
  /** Tsukuyomi state (only populated for the Itachi player). */
  tsukuyomiState?: TsukuyomiState
  /** Passive ability widget states (only populated for the owning player). */
  passiveAbilityStates?: PassiveAbilityStates
  /** Owner-scoped marker projected onto the current terminal node. */
  hasTerminalMarker?: boolean
  /** Butcher's secret sup marker; only supplied to the TheBoys viewer. */
  isTheBoysSupTarget?: boolean
  /** Deadly Virus toxin marker; only supplied to the TheBoys player that owns the infection. */
  isTheBoysVirusTarget?: boolean
  /** Homelander-owner-only rage accumulated against this opponent. */
  homelanderRagePercent?: number
  /** Homelander-owner-only marker for an opponent who revealed his identity. */
  homelanderIdentityRevealer?: boolean
  /** Omni-man-owner-only marker for an opponent who failed Подумай, Марк!. */
  omniManIdiot?: boolean
  /** Omni-man-owner-only marker for the opponent currently sleeping from Стражи Земли. */
  omniManGuardiansAsleep?: boolean
  /** True when Darksci needs to choose stable/unstable (round 1). */
  darksciChoiceNeeded?: boolean
  /** True when Gleb can transform to Young Gleb (round 1). */
  youngGlebAvailable?: boolean
  character: Character
  status: PlayerStatus
  predictions?: Prediction[]
  /** Custom prefix before place number (e.g. octopus tentacles) */
  customLeaderboardPrefix?: string
  /** Custom leaderboard annotations from passives (web-safe HTML) */
  customLeaderboardText?: string
  /** Character mastery points (only set for the owning player). */
  characterMasteryPoints?: number
  /** Whether this opponent is within the viewing player's harm range. */
  isInMyHarmRange?: boolean
}

export type DeathNote = {
  currentRoundTarget: string
  currentRoundName: string
  entries: DeathNoteEntry[]
  failedTargets: string[]
  lPlayerId: string
  isArrested: boolean
  shinigamiEyesActive: boolean
  revealedPlayers: DeathNoteRevealedPlayer[]
}

export type DeathNoteEntry = {
  targetPlayerId: string
  writtenName: string
  roundWritten: number
  wasCorrect: boolean
}

export type DeathNoteRevealedPlayer = {
  playerId: string
  characterName: string
}

export type PortalGun = {
  invented: boolean
  charges: number
}

export type TerminalState = {
  bufferedPoints: number
  streamTargetPlayerId?: string
  activeNodePlayerId?: string
  isNodeActive: boolean
  commitSerial: number
  lastCommitPoints: number
}

export type TsukuyomiState = {
  chargeCounter: number
  isReady: boolean
  totalStolenPoints: number
}

export type PassiveAbilityStates = {
  bulk?: BulkState
  tea?: TeaState
  jew?: JewState
  hardKitty?: HardKittyState
  training?: TrainingState
  dragon?: DragonState
  garbage?: GarbageState
  copycat?: CopycatState
  inkScreen?: InkScreenState
  tigerTop?: TigerTopState
  jaws?: JawsState
  privilege?: PrivilegeState
  vampirism?: VampirismState
  weed?: WeedState
  saitama?: SaitamaState
  shinigamiEyes?: ShinigamiEyesWidgetState
  seller?: SellerState
  scamRat?: ScamRatState
  dopa?: DopaState
  goblinSwarm?: GoblinSwarmState
  kotiki?: KotikiState
  monster?: MonsterState
  pickleRick?: PickleRickState
  giantBeans?: GiantBeansState
  tolyaCount?: TolyaCountState
  impact?: ImpactState
  darksci?: DarksciState
  deepList?: DeepListState
  craboRack?: CraboRackState
  napoleon?: NapoleonState
  support?: SupportState
  toxicMate?: ToxicMateState
  yongGleb?: YongGlebState
  theBoys?: TheBoysState
  salldorum?: SalldorumState
  geralt?: GeraltState
  doomGuy?: DoomGuyState
  eren?: ErenState
  naruto?: NarutoState
  gordon?: GordonState
  jonSnow?: JonSnowState
}

export type BulkState = { drownChance: number; isBuffed: boolean }
export type TeaState = { isReady: boolean }
export type JewState = { stolenPsyche: number }
export type HardKittyState = { friendsCount: number }
export type TrainingState = { currentStatIndex: number; statName: string; targetStatValue: number }
export type DragonState = { isAwakened: boolean; roundsUntilAwaken: number }
export type GarbageState = { markedCount: number; totalTracked: number }
export type CopycatState = { copiedStatName: string; historyCount: number }
export type InkScreenState = { fakeDefeatCount: number; totalDeferredScore: number }
export type TigerTopState = { isActive: boolean; swapsRemaining: number }
export type JawsState = { currentSpeed: number; uniqueDefeated: number; uniquePositions: number }
export type PrivilegeState = { markedCount: number; markedNames: string[] }
export type VampirismState = { activeFeeds: number; ignoredJustice: number }
export type WeedState = { totalWeedAvailable: number; lastHarvestRound: number }
export type SaitamaState = { deferredPoints: number; deferredMoral: number }
export type ShinigamiEyesWidgetState = { isActive: boolean }
export type SellerState = { cooldown: number; markedCount: number; secretBuildSkill: number }
export type ScamRatState = {
  activeGpuCount: number
  soldGpuCount: number
  carryPoints: number
  maximumJustice: number
  lastIntelligenceRoll: number
  lastExplosionPoints: number
  totalExplosionPoints: number
  activeGpuOwners: string[]
}
export type DopaState = {
  visionReady: boolean
  visionCooldown: number
  chosenTactic: string
  metaChoiceReady: boolean
  needSecondAttack: boolean
}
export type GoblinSwarmState = {
  totalGoblins: number
  warriors: number
  hobs: number
  workers: number
  hobRate: number
  warriorRate: number
  workerRate: number
  hobUpgradeLevel: number
  warriorUpgradeLevel: number
  workerUpgradeLevel: number
  zigguratPositions: number[]
  isInZiggurat: boolean
  festivalUsed: boolean
}

export type KotikiState = {
  tauntedCount: number
  tauntedMax: number
  minkaOnPlayerName: string
  stormOnPlayerName: string
  minkaCooldown: number
  stormCooldown: number
  minkaRoundsOnEnemy: number
}

export type MonsterState = {
  pawnCount: number
}

export type ErenState = {
  rageGained: number
  losses: number
  attackTitanActive: boolean
  attackTitanCooldown: number
  attackTitanSoundSerial: number
  tatakeSoundSerial: number
  rumblingTriggered: boolean
  rumblingPlace: number
  hatredMarks: ErenHatredMark[]
}

export type ErenHatredMark = {
  playerName: string
  marks: number
}

export type NarutoState = {
  haremActive: boolean
  haremCooldown: number
}

export type GordonHeadcrabState = {
  playerId: string
  playerName: string
  roundsLeft: number
}

export type GordonHalfLifeState = {
  announced: boolean
  finished: boolean
  released: boolean
  postponements: number
  canAnnounce: boolean
  pendingDecision: boolean
  decisionKind: 'failure' | 'release'
  decisionSerial: number
  deadlineUtc?: string
  rawPoints: number
  superMultiplierDisabled: boolean
  exponent: number
  finalPoints: number
  freezeLabel: string
  postponeLabel: string
  decisionMessage: string
}

export type GordonState = {
  resolvedFights: number
  crowbarProgress: number
  wakeUsed: boolean
  canWake: boolean
  wakeReservedForTsukuyomi: boolean
  headcrabsRemoved: number
  zombieCount: number
  activeHeadcrabs: GordonHeadcrabState[]
  halfLife: GordonHalfLifeState
}

export type JonSnowWeakestPlayer = {
  playerId: string
  playerName: string
}

export type JonSnowState = {
  skill: number
  skillTarget: number
  isKing: boolean
  kingBlockedByCastle: boolean
  bastardIntelligenceBonus: number
  blackCastleActive: boolean
  blackCastleTurnsRemaining: number
  watchEnded: boolean
  loyaltyVictories: number
  weakestPlayers: JonSnowWeakestPlayer[]
}

export type PickleRickState = {
  pickleTurnsRemaining: number
  wasAttackedAsPickle: boolean
  penaltyTurnsRemaining: number
}

export type GiantBeansState = {
  beanStacks: number
  ingredientsActive: boolean
  ingredientTargetCount: number
}

export type TolyaCountState = { isReady: boolean; cooldown: number }
export type ImpactState = { streak: number }
export type DarksciState = { isStableType: boolean; typeChosen: boolean; uniqueEnemiesLeft: number }
export type DeepListState = { knownCount: number; mockeryTriggered: number }
export type CraboRackState = { shellsUsed: number }
export type NapoleonState = { allyName: string; treatyCount: number }
export type SupportState = { carryName: string }
export type ToxicMateState = { cancerActive: boolean; transferCount: number; currentHolderName: string }
export type YongGlebState = { teaReady: boolean; teaCooldown: number }

export type TheBoysState = {
  chemWeaponLevel: number
  orderTargetName: string | null
  orderRoundsLeft: number
  ordersCompleted: number
  ordersFailed: number
  virusArmed: boolean
  virusUsed: boolean
  pokerCount: number
  superDickActive: boolean
  butcherLeft: boolean
  activeCombination: string
  regenLevel: number
  kimikoDisabled: boolean
  totalJusticeBlocked: number
  livingWeapon: boolean
  mmUpgradeLevel: number
  kompromatCount: number
  nextAttackGathersKompromat: boolean
  isCalm: boolean
  kompromatEntries: { targetName: string; hint: string }[]
  lastRevealedMember: string
  revealSerial: number
  lastUnlockedAbility: string
  lastUnlockWasCombination: boolean
  unlockSerial: number
  virusNames: string[]
}

export type SalldorumState = {
  shenCharges: number
  colaBuried: boolean
  colaBuriedPosition: number
  colaBuriedRound: number
  colaReady: boolean
  colaReadyRound: number
  colaDrinks: number
  historyRewritten: boolean
  rewrittenRound: number
  positionHistory: number[]
}

export type InvoiceLineItem = {
  label: string
  points: number
}

export type GeraltState = {
  drownersContracts: number
  werewolvesContracts: number
  vampiresContracts: number
  dragonsContracts: number
  drownersOilTier: number
  werewolvesOilTier: number
  vampiresOilTier: number
  dragonsOilTier: number
  isOilApplied: boolean
  revealedCount: number
  lambertUsed: boolean
  lambertActive: boolean
  enemyMonsterTypes: Record<string, string>
  displeasure: number
  canDemandPrevious: boolean
  canDemandNext: boolean
  demandedThisPhase: boolean
  advancePending: boolean
  invoiceItems?: InvoiceLineItem[]
  invoiceTotal?: number
  invoicePredictedCoins?: number
  invoicePredictedDispleasure?: number
  questCompletedThisRound: boolean
  rareLootFoundThisRound: boolean
}

// ── Blackjack Types ───────────────────────────────────────────────

export type BlackjackTableState = {
  phase: string
  currentPlayerIndex: number
  dealerName: string
  dealerHand: BlackjackCard[]
  dealerTotal: number
  lastMessage: BlackjackMessage | null
  wordCategories: WordCategory[]
  players: BlackjackPlayerState[]
}

export type BlackjackPlayerState = {
  discordId: string
  username: string
  hand: BlackjackCard[]
  total: number
  status: string
  result: string | null
  wins: number
  isCurrentTurn: boolean
  isMe: boolean
  canSendMessage: boolean
}

export type BlackjackCard = {
  suit: string | null
  rank: string | null
  faceUp: boolean
}

export type BlackjackMessage = {
  author: string
  text: string
}

export type WordCategory = {
  name: string
  words: string[]
}

export type Character = {
  name: string
  avatar: string
  avatarCurrent: string
  description: string
  tier: number
  intelligence: number
  strength: number
  speed: number
  psyche: number
  statDisplayOverride?: string
  skillDisplay: string
  moralDisplay: string
  justice: number
  seenJustice: number
  skillClass: string
  skillTarget: string
  classStatDisplayText: string

  // Quality resists & bonuses
  intelligenceResist: number
  strengthResist: number
  speedResist: number
  psycheResist: number
  intelligenceBonusText: string
  strengthBonusText: string
  speedBonusText: string
  psycheBonusText: string

  passives: Passive[]
}

export type Passive = {
  name: string
  description: string
  visible: boolean
  theme?: string
}

export type ScoreBreakdownEntry = {
  source: string
  points: number
  isBonus: boolean
  /** Explicit penalty classification; absent in legacy replay snapshots. */
  isNegative?: boolean
  /** Text-only source such as Tolya's hidden multiplier penalty. */
  hidePoints?: boolean
}

export type ScoreBreakdown = {
  roundMultiplier: number
  expectedRoundMultiplier: number
  entries: ScoreBreakdownEntry[]
}

export type PlayerStatus = {
  score: number
  place: number
  isReady: boolean
  isBlock: boolean
  isSkip: boolean
  /** Owner-only visual classification for why the current action is unavailable. */
  turnInterference: 'none' | 'self' | 'enemy'
  isAutoMove: boolean
  confirmedPredict: boolean
  confirmedSkip: boolean
  lvlUpPoints: number
  moveListPage: number
  personalLogs: string
  previousRoundLogs: string
  allPersonalLogs: string
  scoreSource: string
  directMessages: string[]
  mediaMessages: MediaMessage[]
  isAramRollConfirmed: boolean
  isDraftPickConfirmed: boolean
  aramRerolledPassivesTimes: number
  aramRerolledStatsTimes: number
  placeHistory: { round: number; place: number }[]
  scoreBreakdown: ScoreBreakdown | null
}

export type Prediction = {
  playerId: string
  characterName: string
  /** Actual character name (populated only at game end). */
  actualCharacterName?: string
  /** Whether prediction matches actual (populated only at game end). */
  isCorrect?: boolean
  /** Actual character avatar URL (populated only when wrong at game end). */
  actualAvatar?: string
}

export type Team = {
  teamId: number
  playerIds: string[]
}

export type LobbyState = {
  activeGames: number
  games: ActiveGame[]
  availableCharacters: CharacterInfo[]
}

export type AdminLobbySlot = {
  kind: 'empty' | 'human' | 'bot'
  discordId: string
  username: string
  aiDifficulty: number
  characterName: string
  notifiedByDm: boolean
  isUnreachable: boolean
}

export type AdminLobbyCharacter = {
  name: string
  avatar: string
  tier: number
}

export type AdminLobbyState = {
  ownerId: string
  slots: AdminLobbySlot[]
  characters: AdminLobbyCharacter[]
}

export type AdminLobbyUser = {
  discordId: string
  username: string
  hasDiscord: boolean
  discordOnline: boolean
  browserOnline: boolean
  isBusy: boolean
  isReserved: boolean
}

export type AdminLobbyGuild = {
  guildId: string
  guildName: string
  members: AdminLobbyUser[]
}

export type AdminLobbyDirectory = {
  guilds: AdminLobbyGuild[]
}

export type AdminLobbyPresence = {
  onlineIds: string[]
  busyIds: string[]
  reservedIds: string[]
}

export type ActiveGame = {
  gameId: number
  roundNo: number
  playerCount: number
  humanCount: number
  gameMode: string
  isFinished: boolean
  botCount: number
  canJoin: boolean
  /** Character avatars for preview (optional, may not be sent by older backends) */
  characterAvatars?: { name: string; avatar: string; tier: number }[]
}

export type CharacterInfo = {
  name: string
  avatar: string
  description: string
  tier: number
  intelligence: number
  strength: number
  speed: number
  psyche: number
}

export type CharacterListEntry = {
  name: string
  avatar: string
  tier: number
}

export type DraftOptionDto = {
  name: string
  avatar: string
  intelligence: number
  psyche: number
  speed: number
  strength: number
  description: string
  tier: number
  cost: number
  passives: { name: string; description: string; visible: boolean }[]
}

export type MediaMessage = {
  passiveName: string
  text: string
  passiveNameEnglish?: string | null
  textEnglish?: string | null
  fileUrl: string | null
  /** "text" | "audio" | "image" */
  fileType: string
  /** How many rounds this media should play. Audio with >1 loops across rounds. */
  roundsToPlay: number
}

export type FightEntry = {
  // Participants
  attackerName: string
  attackerCharName: string
  attackerAvatar: string
  defenderName: string
  defenderCharName: string
  defenderAvatar: string

  // Outcome: "win" (attacker wins), "loss" (defender wins), "block", "skip"
  outcome: string
  winnerName: string | null

  // Class info for Nemesis/Versatility display
  attackerClass: string
  defenderClass: string
  attackerOriginalClass: string
  defenderOriginalClass: string
  versatilityIntel: number  // +1 attacker better, -1 defender, 0 equal
  versatilityStr: number
  versatilitySpeed: number

  // Step1: Stats
  scaleMe: number
  scaleTarget: number
  isNemesisMe: boolean
  isNemesisTarget: boolean
  nemesisMultiplier: number
  skillMultiplierMe: number
  skillMultiplierTarget: number
  psycheDifference: number
  weighingMachine: number
  isTooGoodMe: boolean
  isTooGoodEnemy: boolean
  isTooStronkMe: boolean
  isTooStronkEnemy: boolean
  isStatsBetterMe: boolean
  isStatsBetterEnemy: boolean
  randomForPoint: number

  // Round 1 per-step weighing deltas
  nemesisWeighingDelta: number
  scaleWeighingDelta: number
  versatilityWeighingDelta: number
  psycheWeighingDelta: number
  skillWeighingDelta: number
  justiceWeighingDelta: number

  // Round 3 random modifiers
  tooGoodRandomChange: number
  tooStronkRandomChange: number
  justiceRandomChange: number
  nemesisRandomChange: number

  // Round results
  round1PointsWon: number

  // Step2: Justice
  justiceMe: number
  justiceTarget: number
  pointsFromJustice: number

  // Step3: Random roll (only if tie)
  usedRandomRoll: boolean
  randomNumber: number
  maxRandomNumber: number

  // Final
  totalPointsWon: number
  moralChange: number
  attackerMoralChange: number
  defenderMoralChange: number

  // Resist/drop details
  resistIntelDamage: number
  resistStrDamage: number
  resistPsycheDamage: number
  drops: number
  droppedPlayerName: string
  qualityDamageApplied: boolean
  intellectualDamage: boolean
  emotionalDamage: boolean
  justiceChange: number

  skillGainedFromTarget: number
  skillGainedFromClassAttacker: number
  skillGainedFromClassDefender: number
  skillDifferenceRandomModifier: number
  nemesisMultiplierSkillDifference: number

  /** ForOneFight stat modifications applied to the attacker. */
  attackerForOneFightMods: ForOneFightMod[]
  /** ForOneFight stat modifications applied to the defender. */
  defenderForOneFightMods: ForOneFightMod[]

  /** Hidden from non-participants by the server. */
  hiddenFromNonAdmin: boolean
  /** Dopa's second, visually shadowed action. */
  shadowAction: boolean
  /** Whether a Portal Gun swap occurred in this fight. */
  portalGunSwap: boolean
  /** Whether Storm (Котики) appeared in this fight via "Рандомное поведение". */
  stormAppeared: boolean
  /** How much Storm shifted the weighing machine (+5 or -5). */
  stormWeighingDelta: number
  /** Whether Storm's intervention flipped the fight outcome. */
  stormFlipped: boolean
  /** Whether Homelander resolved this attack with his charged eye laser. */
  homelanderLaser: boolean
}

export type ForOneFightMod = {
  source: string
  stat: string
  originalValue: number
  newValue: number
  isOnEnemy: boolean
}

// ── Quest & Loot Box Types ──────────────────────────────────────────

export type QuestState = {
  gameplayMode: 'Casual' | 'Pro'
  eloRating: number
  activeDate: string
  serverNow: string
  resetsAt: string
  quests: QuestProgress[]
  allCompletedToday: boolean
  dailyCompleted: boolean
  completedQuestCount: number
  dailyQuestRequirement: number
  dailyBonusZbs: number
  dailyBonusGranted: boolean
  masteryBonusLootBoxes: number
  masteryBonusGranted: boolean
  rerollsRemaining: number
  streakDays: number
  bestStreakDays: number
  weeklyCompletedDays: number
  weeklyTargetDays: number
  weeklyRewardZbs: number
  weeklyRewardGranted: boolean
  weekEndsAt: string
  zbsPoints: number
  pendingLootBoxes: number
  lootBoxPity: number
  guaranteedRareIn: number
  lootBoxOdds: LootBoxOdds[]
  lastUnacknowledgedLootBox: LootBoxResult | null
  pendingGuaranteedCharacters: number
  nextGuaranteedCharacterName: string | null
}

export type QuestProgress = {
  id: string
  name: string
  nameRu: string
  description: string
  descriptionRu: string
  lane: string
  icon: string
  aggregation: string
  current: number
  target: number
  isCompleted: boolean
  zbsReward: number
  rewardLootBoxes: number
  rewardGranted: boolean
  completedAt: string | null
  canReroll: boolean
}

export type LootBoxResult = {
  rarity: string
  zbsAmount: number
  openingId: string
  zbsBalance: number
  remainingLootBoxes: number
  openedAt: string
  wasPityUpgrade: boolean
  lootBoxPity: number
  guaranteedRareIn: number
  characterName: string | null
  characterAvatar: string | null
  characterTier: number
  guaranteedForNextGame: boolean
  pendingGuaranteedCharacters: number
}

export type LootBoxOdds = {
  rarity: string
  chance: number
  minZbs: number
  maxZbs: number
  guaranteedCharacterMaxTier: number | null
}

export type DoomModule = {
  name: string
  stage: string
  description: string
  reward: boolean
}

export type DoomCopiedPassive = { name: string; description: string }

export type DoomGuyState = {
  rollMode: boolean
  rollAvailable: boolean
  currentStage: string
  currentOptions: DoomModule[]
  activeModules: Record<string, string>
  demonNestNames: string[]
  bfgCharged: boolean
  railgunCharged: boolean
  ascensionIntelligenceRemaining: number
  maneuversSpeedRemaining: number
  exterminationVictories: number
  exterminationAwarded: boolean
  shockShieldUsed: boolean
  blocksThisRound: number
  hellBlockUsed: boolean
  counterAttackMarkedNames: string[]
  sharkShieldActive: boolean
  everBlocked: boolean
  everLost: boolean
  becomeGodAwarded: boolean
  chainsawSpent: boolean
  chainsawSelectionsRemaining: number
  chainsawChoices: DoomCopiedPassive[]
  copiedPassiveName: string
  copiedPassiveNames: string[]
}

export type DoomFortressState = { stages: DoomFortressStage[] }
export type DoomFortressStage = {
  name: string
  slots: string[]
  unlockedModules: DoomModule[]
  rewardModulesRemaining: number
  currentDropChance: number
}

// ── Character Store Types ─────────────────────────────────────────

export type StoreState = {
  zbsPoints: number
  basePrice: number
  minMultiplier: number
  maxMultiplier: number
  totalInvestedZbs: number
  characters: StoreCharacter[]
}

export type StoreCharacter = {
  name: string
  avatar: string
  tier: number
  multiplier: number
  changes: number
  costOne: number
  costTen: number
  refundZbs: number
}

// ── Achievement Types ────────────────────────────────────────────────

export type AchievementBoard = {
  achievements: AchievementEntry[]
  totalUnlocked: number
  totalAchievements: number
  newlyUnlocked: string[]
  earnedRewardZbs: number
  totalRewardZbs: number
  earnedRewardLootBoxes: number
  totalRewardLootBoxes: number
}

export type AchievementEntry = {
  id: string
  name: string
  description: string
  secretHint: string
  category: string
  isSecret: boolean
  icon: string
  rarity: string
  target: number
  current: number
  isUnlocked: boolean
  unlockedAt: string | null
  nameRu: string
  descriptionRu: string
  secretHintRu: string
  characterNames: string[]
  rewardZbs: number
  rewardLootBoxes: number
}

export type ActionResult = {
  action: string
  success: boolean
  error?: string
}

// ── Battleship Types ──────────────────────────────────────────────

export type BattleshipOrientation =
  | 'Horizontal'
  | 'Vertical'
  | 'HorizontalReverse'
  | 'VerticalReverse'

export type BattleshipLobbyState = {
  games: BattleshipLobbyGame[]
}

export type BattleshipLobbyGame = {
  gameId: string
  phase: string
  player1Name: string
  player2Name: string
  player1IsBot: boolean
  player2IsBot: boolean
  turnNumber: number
  createdAt: string
}

export type BattleshipGameState = {
  gameId: string
  phase: string
  turnNumber: number
  shotCount: number
  isFinished: boolean
  winnerId: string | null
  currentTurnPlayerId: string | null
  isMyTurn: boolean
  myPlayerId: string | null
  gameLog: string[]
  player1: BattleshipPlayerState | null
  player2: BattleshipPlayerState | null
  shipCatalog: BattleshipShipCatalogEntry[] | null
  myEndReward: BattleshipEndReward | null
}

/** Per-player settlement summary; only ever populated for the requesting player. */
export type BattleshipEndReward = {
  won: boolean
  wins: number
  losses: number
  currentDailyStreak: number
  bestDailyStreak: number
  firstWinAwarded: boolean
  zbsAwarded: number
}

export type BattleshipStats = {
  wins: number
  losses: number
  currentDailyStreak: number
  bestDailyStreak: number
  firstWinAvailable: boolean
  firstWinZbs: number
  zbsBalance: number
}

export type BattleshipPlayerState = {
  discordId: string
  username: string
  isBot: boolean
  isMe: boolean
  faction: string
  coinsRemaining: number
  isReady: boolean
  summonSlotsUsed: number
  maxSummonSlots: number
  branderUsed: boolean
  selectedShotType: string
  selectedWeaponId: string | null
  revealedCellCount: number
  stunShotExpiry: number
  hasPenalty: boolean
  hasShotThisTurn: boolean
  hasPendingBoardingDeployment: boolean
  pendingManeuver: BattleshipPendingManeuver | null
  pendingCursedBoatDirection: BattleshipPendingCursedBoatDirection | null
  pendingAssembly: BattleshipPendingAssembly | null
  shotDelayRemainingMs: number
  shotDelayDurationMs: number
  summonCooldownRemaining: number
  canDeployAnySummon: boolean
  fleet: BattleshipShip[] | null
  board: BattleshipBoard | null
  summons: BattleshipSummon[]
  pendingSummons: BattleshipPendingSummon[]
  selectedShips: BattleshipFleetSelection[] | null
  availableWeapons: BattleshipAvailableWeapon[]
  canPassBoarding: boolean
}

export type BattleshipAvailableWeapon = {
  id: string
  shipId: string
  shipName: string
  type: string
  ammo: number
  deckIndex: number
  aimRemaining: number
  shotType: string
}

export type BattleshipWeaponLoadout = {
  weaponId: string
  shotType: 'WhiteStone' | 'Buckshot'
}

export type BattleshipPendingSummon = {
  id: string
  type: string
  allowedColumns: number[]
  isBoarding: boolean
  sourceShipName: string
}

export type BattleshipPendingManeuver = {
  shipId: string
  shipName: string
  options: BattleshipManeuverOption[]
}

export type BattleshipManeuverOption = {
  direction: string
  distance: number
  row: number
  col: number
}

export type BattleshipPendingCursedBoatDirection = {
  summonId: string
  row: number
  col: number
  options: BattleshipCursedBoatDirectionOption[]
}

export type BattleshipCursedBoatDirectionOption = {
  direction: string
  row: number
  col: number
}

export type BattleshipPendingAssembly = {
  groupId: string
  options: BattleshipAssemblyOption[]
}

export type BattleshipAssemblyOption = {
  row: number
  col: number
  orientation: BattleshipOrientation
}

export type BattleshipBoard = {
  cells: BattleshipCell[]
}

export type BattleshipCell = {
  row: number
  col: number
  isRevealed: boolean
  isHit: boolean
  isMiss: boolean
  isBurning: boolean
  hasShip: boolean
  shipId: string | null
  hasSummon: boolean
  summonOwnerId: string | null
  summonType: string | null
  summonName?: string | null
  isBoardingSummon?: boolean
  isScratched: boolean
  summonTrails?: string[]
  summonDeaths?: string[]
  frozenSummonDeathIndices?: number[]
  isBurnResistMarked?: boolean
  isDodgeMarked?: boolean
  isManeuverDodgeMarked?: boolean
  isDestroyed?: boolean
  isShipSunk?: boolean
  isFrozen?: boolean
  isDevastated?: boolean
  isCaptured?: boolean
  isFirePermanent?: boolean
  sunkShipName?: string | null
}

export type BattleshipShip = {
  id: string
  definitionId: string
  name: string
  deckCount: number
  row: number
  col: number
  orientation: BattleshipOrientation
  isDestroyed: boolean
  isPlaced: boolean
  isSummon: boolean
  hasManeuvered: boolean
  range: string
  cost: number
  abilities: string[]
  factions: string[]
  upgrades: string[]
  speed: number
  space: number
  explosionRadius: number
  regions: string[]
  decks: BattleshipDeck[]
  weapons: BattleshipWeapon[]
}

export type BattleshipDeck = {
  index: number
  maxHp: number
  currentHp: number
  isDestroyed: boolean
  module: string | null
  moduleDestroyed: boolean
}

export type BattleshipWeapon = {
  id: string
  shipId: string
  type: string
  ammo: number
  deckIndex: number
  hasAmmo: boolean
  aimSpeed: number
  configuredShotType: string | null
}

export type BattleshipSummon = {
  id: string
  type: string
  row: number
  col: number
  speed: number
  isAlive: boolean
  moveDirection: string
  waitingForTurnBack: boolean
  waitingForDirectionChoice: boolean
  isBoardingShip: boolean
  sourceShipName?: string | null
}

export type BattleshipFleetSelection = {
  definitionId: string
  shipName: string
  cost: number
  upgrades: string[]
}

export type BattleshipShipCatalogEntry = {
  id: string
  name: string
  nameRu: string
  deckCount: number
  range: string
  cost: number
  defaultArmor: number
  deckHpOverrides: number[] | null
  space: number
  speed: number
  isFree: boolean
  abilities: string[]
  description: string
  region: string | null
  regions: string[]
  availableUpgrades: BattleshipUpgrade[]
}

export type BattleshipUpgrade = {
  id: string
  name: string
  nameRu: string
  cost: number
  description: string
}

export type BattleshipShotResult = {
  wasSkipped: boolean
  hit: boolean
  miss: boolean
  scratched: boolean
  destroyed: boolean
  shipSunk: boolean
  burned: boolean
  dodged: boolean
  row: number
  col: number
  turnContinues: boolean
  shotDelayMs: number
  message: string
  affectedShipName: string | null
  sourceShipId: string | null
  sourceDeckIndex: number
  sourceRow: number
  sourceCol: number
  sourceBoardPlayerId: string | null
  projectileType: 'Arrow' | 'Stone' | 'Buckshot' | 'Fire' | null
  targetPlayerId: string | null
}

export type BattleshipEvent = {
  eventType: string
  data?: unknown
}

// ── Replay Types ────────────────────────────────────────────────────

export type ReplayData = {
  gameId: number
  replayHash: string
  /** 0/1 = legacy boundary snapshots; 2 = same-round pre-fight + result snapshots. */
  replayFormatVersion?: number
  gameVersion: string
  gameMode: string
  story: string | null
  fullChronicle: string | null
  totalRounds: number
  finishedAt: string
  allCharacterNames: string[]
  allCharacters: CharacterInfo[]
  teams: Team[]
  playerSummaries: ReplayPlayerSummary[]
  rounds: ReplayRound[]
}

export type ReplayPlayerSummary = {
  playerId: string
  discordUsername: string
  isBot: boolean
  isWebPlayer: boolean
  characterName: string
  characterAvatar: string
  finalPlace: number
  finalScore: number
  characterMasteryPoints: number
  teamId: number
}

export type ReplayRound = {
  roundNo: number
  globalLogs: string
  allGlobalLogs: string
  fightLog: FightEntry[]
  /** HandleLastRound-only suffixes; round-11 setup logs are deliberately excluded. */
  finalSettlementGlobalLogs?: string
  finalSettlementAllGlobalLogs?: string
  /** Present in replay format v2; legacy files reconstruct this from the preceding boundary. */
  preFightPlayers?: ReplayRoundPlayer[]
  players: ReplayRoundPlayer[]
}

export type ReplayRoundPlayer = {
  playerId: string
  playerState: Player
  /** HandleLastRound-only additions; excludes the already-captured round-11 setup buffer. */
  finalSettlementLogs?: string
  customLeaderboardView: ReplayCustomLeaderboardEntry[]
}

export type ReplayCustomLeaderboardEntry = {
  playerId: string
  customLeaderboardPrefix: string
  customLeaderboardText: string
}

export type ReplayListEntry = {
  gameId: number
  replayHash: string
  gameMode: string
  totalRounds: number
  finishedAt: string
  players: ReplayListPlayer[]
}

export type ReplayListPlayer = {
  discordUsername: string
  characterName: string
  characterAvatar: string
  finalPlace: number
  finalScore: number
}

/**
 * Game event pushed via SignalR.
 * Known eventTypes: "RoundChanged", "GameFinished", "GameStory"
 * - GameStory data: { story: string } — AI-generated narrative summary
 */
export type GameEvent = {
  eventType: string
  data?: unknown
}

// ── SignalR Connection Manager ─────────────────────────────────────

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private _isConnected = false
  private _isSessionReady = false
  private _lastDiscordId: string | null = null
  private _currentGameId: number | null = null

  // Event callbacks
  onGameState: ((state: GameState) => void) | null = null
  onLobbyState: ((state: LobbyState) => void) | null = null
  onAdminLobbyState: ((state: AdminLobbyState | null) => void) | null = null
  onAdminLobbyDirectory: ((directory: AdminLobbyDirectory) => void) | null = null
  onAdminLobbyPresence: ((presence: AdminLobbyPresence) => void) | null = null
  onAdminLobbyReserved: ((data: { reserved: boolean }) => void) | null = null
  onAdminLobbyGameStarted: ((data: { gameId: number }) => void) | null = null
  onActionResult: ((result: ActionResult) => void) | null = null
  onGameEvent: ((event: GameEvent) => void) | null = null
  onError: ((error: string) => void) | null = null
  onAuthenticated: ((data: { success: boolean; discordId: string; playerType: number; lastPlayedCharacter: string; gameplayMode: 'Casual' | 'Pro'; eloRating: number; isGodAdmin: boolean }) => void) | null = null
  onGameplayModeChanged: ((data: { gameplayMode: 'Casual' | 'Pro'; eloRating: number }) => void) | null = null
  onConnectionChanged: ((connected: boolean) => void) | null = null
  onWebAccountCreated: ((data: { discordId: string; username: string }) => void) | null = null
  onGameCreated: ((data: { gameId: number }) => void) | null = null
  onGameJoined: ((data: { gameId: number }) => void) | null = null
  onBlackjackState: ((state: BlackjackTableState) => void) | null = null
  onQuestState: ((state: QuestState) => void) | null = null
  onLootBoxOpened: ((result: LootBoxResult) => void) | null = null
  onAchievementBoard: ((board: AchievementBoard) => void) | null = null
  onStoreState: ((state: StoreState) => void) | null = null
  onCharacterList: ((list: CharacterListEntry[]) => void) | null = null
  onDoomFortressState: ((state: DoomFortressState) => void) | null = null
  onBattleshipLobby: ((state: BattleshipLobbyState) => void) | null = null
  onBattleshipState: ((state: BattleshipGameState) => void) | null = null
  onBattleshipGameCreated: ((data: { gameId: string }) => void) | null = null
  onBattleshipGameJoined: ((data: { gameId: string }) => void) | null = null
  onBattleshipEvent: ((event: BattleshipEvent) => void) | null = null
  onShipCatalog: ((catalog: BattleshipShipCatalogEntry[]) => void) | null = null
  onBattleshipStats: ((stats: BattleshipStats) => void) | null = null

  get isConnected() {
    return this._isConnected
  }

  private requireConnected(): signalR.HubConnection {
    if (
      !this.connection
      || this.connection.state !== signalR.HubConnectionState.Connected
      || !this._isSessionReady
    ) {
      throw new Error('Connection unavailable. Wait for reconnection, then try again.')
    }
    return this.connection
  }

  async connect(): Promise<void> {
    if (this.connection) {
      if (
        this.connection.state === signalR.HubConnectionState.Connected
        || this.connection.state === signalR.HubConnectionState.Connecting
        || this.connection.state === signalR.HubConnectionState.Reconnecting
      ) return

      // A failed start (or an exhausted reconnect policy) leaves a disconnected
      // HubConnection object behind. Dispose it so an explicit login retry can
      // build a fresh transport instead of returning early forever.
      const staleConnection = this.connection
      this.connection = null
      try {
        await staleConnection.stop()
      }
      catch {
        // It is already disconnected; the important part is releasing it.
      }
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // Register event handlers
    this.connection.on('GameState', (state: GameState) => {
      this.onGameState?.(state)
    })

    this.connection.on('LobbyState', (state: LobbyState) => {
      this.onLobbyState?.(state)
    })

    this.connection.on('AdminLobbyState', (state: AdminLobbyState | null) => {
      this.onAdminLobbyState?.(state)
    })

    this.connection.on('AdminLobbyDirectory', (directory: AdminLobbyDirectory) => {
      this.onAdminLobbyDirectory?.(directory)
    })

    this.connection.on('AdminLobbyPresence', (presence: AdminLobbyPresence) => {
      this.onAdminLobbyPresence?.(presence)
    })

    this.connection.on('AdminLobbyReserved', (data: { reserved: boolean }) => {
      this.onAdminLobbyReserved?.(data)
    })

    this.connection.on('AdminLobbyGameStarted', (data: { gameId: number }) => {
      this.onAdminLobbyGameStarted?.(data)
    })

    this.connection.on('ActionResult', (result: ActionResult) => {
      this.onActionResult?.(result)
    })

    this.connection.on('GameEvent', (event: GameEvent) => {
      this.onGameEvent?.(event)
    })

    this.connection.on('Error', (error: string) => {
      console.error('[SignalR] Server error:', error)
      this.onError?.(error)
    })

    this.connection.on('Authenticated', (data: { success: boolean; discordId: string; playerType: number; lastPlayedCharacter: string; gameplayMode: 'Casual' | 'Pro'; eloRating: number; isGodAdmin: boolean }) => {
      this._isSessionReady = data.success
      this.onAuthenticated?.(data)
    })

    this.connection.on('GameplayModeChanged', (data: { gameplayMode: 'Casual' | 'Pro'; eloRating: number }) => {
      this.onGameplayModeChanged?.(data)
    })

    this.connection.on('WebAccountCreated', (data: { discordId: string; username: string }) => {
      this._isSessionReady = true
      this.onWebAccountCreated?.(data)
    })

    this.connection.on('GameCreated', (data: { gameId: number }) => {
      this.onGameCreated?.(data)
    })

    this.connection.on('GameJoined', (data: { gameId: number }) => {
      this.onGameJoined?.(data)
    })

    this.connection.on('BlackjackState', (state: BlackjackTableState) => {
      this.onBlackjackState?.(state)
    })

    this.connection.on('QuestState', (state: QuestState) => {
      this.onQuestState?.(state)
    })

    this.connection.on('LootBoxOpened', (result: LootBoxResult) => {
      this.onLootBoxOpened?.(result)
    })

    this.connection.on('AchievementBoard', (board: AchievementBoard) => {
      this.onAchievementBoard?.(board)
    })

    this.connection.on('StoreState', (state: StoreState) => {
      this.onStoreState?.(state)
    })

    this.connection.on('CharacterList', (list: CharacterListEntry[]) => {
      this.onCharacterList?.(list)
    })

    this.connection.on('DoomFortressState', (state: DoomFortressState) => {
      this.onDoomFortressState?.(state)
    })

    this.connection.on('BattleshipLobby', (state: BattleshipLobbyState) => {
      this.onBattleshipLobby?.(state)
    })

    this.connection.on('BattleshipState', (state: BattleshipGameState) => {
      this.onBattleshipState?.(state)
    })

    this.connection.on('BattleshipGameCreated', (data: { gameId: string }) => {
      this.onBattleshipGameCreated?.(data)
    })

    this.connection.on('BattleshipGameJoined', (data: { gameId: string }) => {
      this.onBattleshipGameJoined?.(data)
    })

    this.connection.on('BattleshipEvent', (event: BattleshipEvent) => {
      this.onBattleshipEvent?.(event)
    })

    this.connection.on('ShipCatalog', (catalog: BattleshipShipCatalogEntry[]) => {
      this.onShipCatalog?.(catalog)
    })

    this.connection.on('BattleshipStats', (stats: BattleshipStats) => {
      this.onBattleshipStats?.(stats)
    })

    this.connection.onreconnecting(() => {
      console.log('[SignalR] Reconnecting...')
      this._isConnected = false
      this._isSessionReady = false
      this.onConnectionChanged?.(false)
    })

    this.connection.onreconnected(async () => {
      console.log('[SignalR] Reconnected')
      this._isConnected = true
      this.onConnectionChanged?.(true)

      // Re-authenticate and re-join game group after reconnect
      if (this._lastDiscordId) await this.authenticate(this._lastDiscordId)
      if (this._currentGameId) await this.joinGame(this._currentGameId)
    })

    const startedConnection = this.connection

    this.connection.onclose(() => {
      console.log('[SignalR] Connection closed')
      this._isConnected = false
      this._isSessionReady = false
      this.onConnectionChanged?.(false)
      if (this.connection === startedConnection) this.connection = null
    })

    try {
      await startedConnection.start()
      this._isConnected = true
      this.onConnectionChanged?.(true)
      console.log('[SignalR] Connected')
    }
    catch (error) {
      if (this.connection === startedConnection) this.connection = null
      this._isConnected = false
      this._isSessionReady = false
      this.onConnectionChanged?.(false)
      try {
        await startedConnection.stop()
      }
      catch {
        // start() may fail before a transport exists.
      }
      throw error
    }
  }

  async disconnect(): Promise<void> {
    const connection = this.connection

    // A deliberate disconnect is also a session boundary. Clear these before
    // stopping so an in-flight reconnect can never authenticate the old user.
    this.connection = null
    this._lastDiscordId = null
    this._currentGameId = null
    this._isConnected = false
    this._isSessionReady = false
    this.onConnectionChanged?.(false)

    if (!connection) return
    try {
      await connection.stop()
    }
    catch {
      // Local logout must still complete when the transport is already gone.
    }
  }

  // ── Hub Methods ─────────────────────────────────────────────────

  async authenticate(discordId: string): Promise<void> {
    // Send as string to avoid JS number precision loss on large snowflake IDs
    this._lastDiscordId = discordId
    this._isSessionReady = false
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Connection unavailable. Try connecting again.')
    }
    await this.connection.invoke('Authenticate', discordId)
  }

  async setLanguage(language: 'ru' | 'en'): Promise<void> {
    await this.connection?.invoke('SetLanguage', language)
  }

  async joinGame(gameId: number): Promise<void> {
    this._currentGameId = gameId
    await this.connection?.invoke('JoinGame', gameId)
  }

  async leaveGame(gameId: number): Promise<void> {
    this._currentGameId = null
    await this.connection?.invoke('LeaveGame', gameId)
  }

  async requestGameState(gameId: number): Promise<void> {
    await this.connection?.invoke('RequestGameState', gameId)
  }

  async requestLobbyState(): Promise<void> {
    await this.connection?.invoke('RequestLobbyState')
  }

  async createAdminLobby(): Promise<void> {
    await this.connection?.invoke('CreateAdminLobby')
  }

  async requestAdminLobbyState(): Promise<void> {
    await this.connection?.invoke('RequestAdminLobbyState')
  }

  async requestAdminLobbyDirectory(): Promise<void> {
    await this.connection?.invoke('RequestAdminLobbyDirectory')
  }

  async requestAdminLobbyPresence(): Promise<void> {
    await this.connection?.invoke('RequestAdminLobbyPresence')
  }

  async adminLobbyInvitePlayer(slotIndex: number, discordId: string): Promise<void> {
    await this.connection?.invoke('AdminLobbyInvitePlayer', slotIndex, discordId)
  }

  async adminLobbyAddBot(slotIndex: number, aiDifficulty: number): Promise<void> {
    await this.connection?.invoke('AdminLobbyAddBot', slotIndex, aiDifficulty)
  }

  async adminLobbySetCharacter(slotIndex: number, characterName: string): Promise<void> {
    await this.connection?.invoke('AdminLobbySetCharacter', slotIndex, characterName)
  }

  async adminLobbyRemoveSlot(slotIndex: number): Promise<void> {
    await this.connection?.invoke('AdminLobbyRemoveSlot', slotIndex)
  }

  async adminLobbyStart(): Promise<void> {
    await this.connection?.invoke('AdminLobbyStart')
  }

  async adminLobbyCancel(): Promise<void> {
    await this.connection?.invoke('AdminLobbyCancel')
  }

  // ── Game Actions ────────────────────────────────────────────────

  async attack(gameId: number, targetPlace: number): Promise<void> {
    await this.connection?.invoke('Attack', gameId, targetPlace)
  }

  async block(gameId: number): Promise<void> {
    await this.connection?.invoke('Block', gameId)
  }

  async announceHalfLife3(gameId: number): Promise<void> {
    await this.connection?.invoke('AnnounceHalfLife3', gameId)
  }

  async wakeGordon(gameId: number): Promise<void> {
    await this.connection?.invoke('WakeGordon', gameId)
  }

  async resolveHalfLife3Decision(
    gameId: number,
    decisionSerial: number,
    choice: 'freeze' | 'postpone' | 'release',
  ): Promise<void> {
    await this.connection?.invoke('ResolveHalfLife3Decision', gameId, decisionSerial, choice)
  }

  async autoMove(gameId: number): Promise<void> {
    await this.connection?.invoke('DoAutoMove', gameId)
  }

  async changeMind(gameId: number): Promise<void> {
    await this.connection?.invoke('ChangeMind', gameId)
  }

  async confirmSkip(gameId: number): Promise<void> {
    await this.connection?.invoke('ConfirmSkip', gameId)
  }

  async confirmPredict(gameId: number): Promise<void> {
    await this.connection?.invoke('ConfirmPredict', gameId)
  }

  async levelUp(gameId: number, statIndex: number): Promise<void> {
    await this.connection?.invoke('LevelUp', gameId, statIndex)
  }

  async moralToPoints(gameId: number): Promise<void> {
    await this.connection?.invoke('MoralToPoints', gameId)
  }

  async moralToSkill(gameId: number): Promise<void> {
    await this.connection?.invoke('MoralToSkill', gameId)
  }

  async demandContractReward(gameId: number, demandType: string): Promise<void> {
    await this.connection?.invoke('DemandContractReward', gameId, demandType)
  }

  async predict(gameId: number, targetPlayerId: string, characterName: string): Promise<void> {
    await this.connection?.invoke('Predict', gameId, targetPlayerId, characterName)
  }

  async aramReroll(gameId: number, slot: number): Promise<void> {
    await this.connection?.invoke('AramReroll', gameId, slot)
  }

  async aramConfirm(gameId: number): Promise<void> {
    await this.connection?.invoke('AramConfirm', gameId)
  }

  // ── Draft Pick ──────────────────────────────────────────────────

  async beginAdeptChoice(gameId: number): Promise<void> {
    await this.connection?.invoke('BeginAdeptChoice', gameId)
  }

  async draftSelect(gameId: number, characterName: string): Promise<void> {
    await this.connection?.invoke('DraftSelect', gameId, characterName)
  }

  async depthsCallChoice(gameId: number, agree: boolean): Promise<void> {
    await this.connection?.invoke('DepthsCallChoice', gameId, agree)
  }

  // ── Darksci / Young Gleb ───────────────────────────────────────

  async darksciChoice(gameId: number, isStable: boolean): Promise<void> {
    await this.connection?.invoke('DarksciChoice', gameId, isStable)
  }

  async youngGleb(gameId: number): Promise<void> {
    await this.connection?.invoke('YoungGleb', gameId)
  }

  async doomRoll(gameId: number): Promise<void> {
    await this.connection?.invoke('DoomRoll', gameId)
  }

  async doomChainsaw(gameId: number, passiveName: string): Promise<void> {
    await this.connection?.invoke('DoomChainsaw', gameId, passiveName)
  }

  // ── Kira Actions ───────────────────────────────────────────────

  async deathNoteWrite(gameId: number, targetPlayerId: string, characterName: string): Promise<void> {
    await this.connection?.invoke('DeathNoteWrite', gameId, targetPlayerId, characterName)
  }

  async shinigamiEyes(gameId: number): Promise<void> {
    await this.connection?.invoke('ShinigamiEyes', gameId)
  }

  async setPreferWeb(gameId: number, preferWeb: boolean): Promise<void> {
    await this.connection?.invoke('SetPreferWeb', gameId, preferWeb)
  }

  // ── Salldorum Actions ──────────────────────────────────────────

  async rewriteHistory(gameId: number, roundNumber: number): Promise<void> {
    await this.connection?.invoke('RewriteHistory', gameId, roundNumber)
  }

  async finishGame(gameId: number): Promise<void> {
    await this.connection?.invoke('FinishGame', gameId)
  }

  // ── Blackjack (Dead Player Mini-Game) ──────────────────────────

  async blackjackJoin(gameId: number): Promise<void> {
    await this.connection?.invoke('BlackjackJoin', gameId)
  }

  async blackjackHit(gameId: number): Promise<void> {
    await this.connection?.invoke('BlackjackHit', gameId)
  }

  async blackjackStand(gameId: number): Promise<void> {
    await this.connection?.invoke('BlackjackStand', gameId)
  }

  async blackjackNewRound(gameId: number): Promise<void> {
    await this.connection?.invoke('BlackjackNewRound', gameId)
  }

  async blackjackSendMessage(gameId: number, words: string[]): Promise<void> {
    await this.connection?.invoke('BlackjackSendMessage', gameId, words)
  }

  // ── Web Game Creation ──────────────────────────────────────────

  async registerWebAccount(username: string): Promise<void> {
    this._isSessionReady = false
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('Connection unavailable. Try connecting again.')
    }
    await this.connection.invoke('RegisterWebAccount', username)
  }

  async createWebGame(): Promise<void> {
    await this.connection?.invoke('CreateWebGame')
  }

  async createRankedGame(): Promise<void> {
    await this.connection?.invoke('CreateRankedGame')
  }

  async setGameplayMode(mode: 'Casual' | 'Pro'): Promise<void> {
    await this.connection?.invoke('SetGameplayMode', mode)
  }

  async joinWebGame(gameId: number): Promise<void> {
    await this.connection?.invoke('JoinWebGame', gameId)
  }

  async getCharacterList(): Promise<void> {
    await this.connection?.invoke('GetCharacterList')
  }

  async createTestGame(characterName: string): Promise<void> {
    await this.connection?.invoke('CreateTestGame', characterName)
  }

  async requestQuests(): Promise<void> {
    await this.requireConnected().invoke('RequestQuests')
  }

  async rerollDailyQuest(questId: string): Promise<void> {
    await this.requireConnected().invoke('RerollDailyQuest', questId)
  }

  async openLootBoxV2(): Promise<void> {
    await this.requireConnected().invoke('OpenLootBoxV2')
  }

  async acknowledgeLootBox(openingId: string): Promise<void> {
    await this.requireConnected().invoke('AcknowledgeLootBox', openingId)
  }

  async requestAchievements(): Promise<void> {
    await this.requireConnected().invoke('RequestAchievements')
  }

  async clearNewAchievements(): Promise<void> {
    await this.connection?.invoke('ClearNewAchievements')
  }

  async acknowledgeAchievements(achievementIds: string[]): Promise<void> {
    await this.requireConnected().invoke('AcknowledgeAchievements', achievementIds)
  }

  async requestStore(): Promise<void> {
    await this.requireConnected().invoke('RequestStore')
  }

  async adjustStoreCharacter(characterName: string, percentagePoints: number): Promise<void> {
    await this.requireConnected().invoke('AdjustStoreCharacter', characterName, percentagePoints)
  }

  async resetStoreCharacter(characterName: string): Promise<void> {
    await this.requireConnected().invoke('ResetStoreCharacter', characterName)
  }

  async resetStoreAllCharacters(): Promise<void> {
    await this.requireConnected().invoke('ResetStoreAllCharacters')
  }

  async requestDoomFortress(): Promise<void> {
    await this.connection?.invoke('RequestDoomFortress')
  }

  async equipDoomModule(stage: string, slotIndex: number, moduleName: string): Promise<void> {
    await this.connection?.invoke('EquipDoomModule', stage, slotIndex, moduleName)
  }

  // ── Battleship (Sea Battle) ───────────────────────────────────────

  async requestBattleshipLobby(): Promise<void> {
    await this.connection?.invoke('RequestBattleshipLobby')
  }

  async createBattleshipGame(): Promise<void> {
    await this.connection?.invoke('CreateBattleshipGame')
  }

  async joinBattleshipWebGame(gameId: string): Promise<void> {
    await this.connection?.invoke('JoinBattleshipWebGame', gameId)
  }

  async leaveBattleshipWebGame(gameId: string): Promise<void> {
    await this.connection?.invoke('LeaveBattleshipWebGame', gameId)
  }

  async joinBattleshipGame(gameId: string): Promise<void> {
    await this.connection?.invoke('JoinBattleshipGame', gameId)
  }

  async leaveBattleshipGame(gameId: string): Promise<void> {
    await this.connection?.invoke('LeaveBattleshipGame', gameId)
  }

  async battleshipConfirmReady(gameId: string): Promise<void> {
    await this.connection?.invoke('BattleshipConfirmReady', gameId)
  }

  async battleshipSelectArmy(gameId: string, faction: string): Promise<void> {
    await this.connection?.invoke('BattleshipSelectArmy', gameId, faction)
  }

  async battleshipSelectFleet(gameId: string, selections: BattleshipFleetSelection[]): Promise<void> {
    await this.connection?.invoke('BattleshipSelectFleet', gameId, selections)
  }

  async battleshipPlaceShip(gameId: string, shipId: string, row: number, col: number, orientation: BattleshipOrientation): Promise<void> {
    await this.connection?.invoke('BattleshipPlaceShip', gameId, shipId, row, col, orientation)
  }

  async battleshipRemoveShip(gameId: string, shipId: string): Promise<void> {
    await this.connection?.invoke('BattleshipRemoveShip', gameId, shipId)
  }

  async battleshipConfirmPlacement(gameId: string, loadouts: BattleshipWeaponLoadout[]): Promise<void> {
    await this.connection?.invoke('BattleshipConfirmPlacement', gameId, loadouts)
  }

  async battleshipCancelPlacement(gameId: string): Promise<void> {
    await this.connection?.invoke('BattleshipCancelPlacement', gameId)
  }

  async battleshipShoot(gameId: string, row: number, col: number): Promise<void> {
    await this.connection?.invoke('BattleshipShoot', gameId, row, col)
  }

  async battleshipShootOwnBoard(gameId: string, row: number, col: number): Promise<void> {
    await this.connection?.invoke('BattleshipShootOwnBoard', gameId, row, col)
  }

  async battleshipSelectWeapon(gameId: string, weaponType: string, shotType: string, weaponId: string): Promise<void> {
    await this.connection?.invoke('BattleshipSelectWeapon', gameId, weaponType, shotType, weaponId)
  }

  async battleshipPassBoardingTurn(gameId: string): Promise<void> {
    await this.connection?.invoke('BattleshipPassBoardingTurn', gameId)
  }

  async battleshipDeploySummon(gameId: string, summonType: string, col: number): Promise<void> {
    await this.connection?.invoke('BattleshipDeploySummon', gameId, summonType, col)
  }

  async battleshipDeployPendingSummon(gameId: string, pendingId: string, col: number): Promise<void> {
    await this.connection?.invoke('BattleshipDeployPendingSummon', gameId, pendingId, col)
  }

  async battleshipManualMove(gameId: string, shipId: string, direction: string, distance: number = 1): Promise<void> {
    await this.connection?.invoke('BattleshipManualMove', gameId, shipId, direction, distance)
  }

  async battleshipSetCursedBoatDirection(gameId: string, summonId: string, direction: string): Promise<void> {
    await this.connection?.invoke('BattleshipSetCursedBoatDirection', gameId, summonId, direction)
  }

  async battleshipAssembleShip(
    gameId: string,
    groupId: string,
    row: number,
    col: number,
    orientation: BattleshipOrientation,
  ): Promise<void> {
    await this.connection?.invoke('BattleshipAssembleShip', gameId, groupId, row, col, orientation)
  }

  async battleshipForfeit(gameId: string): Promise<void> {
    await this.connection?.invoke('BattleshipForfeit', gameId)
  }

  async requestBattleshipState(gameId: string): Promise<void> {
    await this.connection?.invoke('RequestBattleshipState', gameId)
  }

  async requestShipCatalog(): Promise<void> {
    await this.connection?.invoke('RequestShipCatalog')
  }

  async requestBattleshipStats(): Promise<void> {
    await this.requireConnected().invoke('RequestBattleshipStats')
  }
}

// Singleton instance
export const signalrService = new SignalRService()
