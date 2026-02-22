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
  isKratosEvent: boolean
  globalLogs: string
  /** Full history of global logs across all rounds */
  allGlobalLogs: string
  /** Full game chronicle (global events + all players' personal logs). Only set when isFinished. */
  fullChronicle?: string
  /** The PlayerId of the requesting player, or null for spectators */
  myPlayerId: string | null
  /** PlayerType: 0/1 = normal, 2 = admin, 404 = bot */
  myPlayerType: number
  /** Whether this player has "Prefer Web" enabled (suppresses Discord messages) */
  preferWeb: boolean
  /** All character names for prediction dropdowns */
  allCharacterNames: string[]
  /** Full character catalog with base stats for prediction lookup */
  allCharacters: CharacterInfo[]
  players: Player[]
  teams: Team[]
  /** Structured fight log for the current round (for fight animation) */
  fightLog: FightEntry[]
}

export type Player = {
  playerId: string
  discordUsername: string
  isBot: boolean
  isWebPlayer: boolean
  teamId: number
  /** Whether this player was killed by Kratos (hidden from leaderboard). */
  kratosIsDead: boolean
  /** Whether this player was killed by Kira's Death Note. */
  kiraDeathNoteDead: boolean
  /** Whether this player is Kira (uses Death Note instead of predictions). */
  isKira: boolean
  /** Whether this player is Баг (sees exploit markers). */
  isBug: boolean
  /** Death Note state (only populated for the Kira player). */
  deathNote?: DeathNote
  /** Portal Gun state (only populated for Rick). */
  portalGun?: PortalGun
  /** Exploit state (only populated for the Баг player). */
  exploitState?: ExploitState
  /** Tsukuyomi state (only populated for the Itachi player). */
  tsukuyomiState?: TsukuyomiState
  /** Passive ability widget states (only populated for the owning player). */
  passiveAbilityStates?: PassiveAbilityStates
  /** Whether this player is currently marked as exploitable (only visible to Баг). */
  isExploitable?: boolean
  /** Whether this player's exploit has been fixed by Баг. */
  isExploitFixed?: boolean
  /** True when Darksci needs to choose stable/unstable (round 1). */
  darksciChoiceNeeded?: boolean
  /** True when Gleb can transform to Young Gleb (round 1). */
  youngGlebAvailable?: boolean
  /** True when Dopa needs to choose a tactic. */
  dopaChoiceNeeded?: boolean
  character: Character
  status: PlayerStatus
  predictions?: Prediction[]
  /** Custom prefix before place number (e.g. octopus tentacles) */
  customLeaderboardPrefix?: string
  /** Custom leaderboard annotations from passives (web-safe HTML) */
  customLeaderboardText?: string
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

export type ExploitState = {
  totalExploit: number
  fixedCount: number
  totalPlayers: number
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
  sellerMark?: SellerMarkState
  dopa?: DopaState
  goblinSwarm?: GoblinSwarmState
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
export type PrivilegeState = { markedCount: number }
export type VampirismState = { activeFeeds: number; ignoredJustice: number }
export type WeedState = { totalWeedAvailable: number; lastHarvestRound: number }
export type SaitamaState = { deferredPoints: number; deferredMoral: number }
export type ShinigamiEyesWidgetState = { isActive: boolean }
export type SellerState = { cooldown: number; markedCount: number; secretBuildSkill: number }
export type SellerMarkState = { roundsRemaining: number }
export type DopaState = {
  visionReady: boolean
  visionCooldown: number
  chosenTactic: string
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
  zigguratPositions: number[]
  isInZiggurat: boolean
  festivalUsed: boolean
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
}

export type PlayerStatus = {
  score: number
  place: number
  isReady: boolean
  isBlock: boolean
  isSkip: boolean
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
  aramRerolledPassivesTimes: number
  aramRerolledStatsTimes: number
  placeHistory: { round: number; place: number }[]
}

export type Prediction = {
  playerId: string
  characterName: string
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

export type ActiveGame = {
  gameId: number
  roundNo: number
  playerCount: number
  humanCount: number
  gameMode: string
  isFinished: boolean
  botCount: number
  canJoin: boolean
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

export type MediaMessage = {
  passiveName: string
  text: string
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

  /** Whether a Portal Gun swap occurred in this fight. */
  portalGunSwap: boolean
}

export type ActionResult = {
  action: string
  success: boolean
  error?: string
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

  // Event callbacks
  onGameState: ((state: GameState) => void) | null = null
  onLobbyState: ((state: LobbyState) => void) | null = null
  onActionResult: ((result: ActionResult) => void) | null = null
  onGameEvent: ((event: GameEvent) => void) | null = null
  onError: ((error: string) => void) | null = null
  onAuthenticated: ((data: { success: boolean; discordId: string }) => void) | null = null
  onConnectionChanged: ((connected: boolean) => void) | null = null
  onWebAccountCreated: ((data: { discordId: string; username: string }) => void) | null = null
  onGameCreated: ((data: { gameId: number }) => void) | null = null
  onGameJoined: ((data: { gameId: number }) => void) | null = null
  onBlackjackState: ((state: BlackjackTableState) => void) | null = null

  get isConnected() {
    return this._isConnected
  }

  async connect(): Promise<void> {
    if (this.connection) return

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

    this.connection.on('Authenticated', (data: { success: boolean; discordId: string }) => {
      this.onAuthenticated?.(data)
    })

    this.connection.on('WebAccountCreated', (data: { discordId: string; username: string }) => {
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

    this.connection.onreconnecting(() => {
      console.log('[SignalR] Reconnecting...')
      this._isConnected = false
      this.onConnectionChanged?.(false)
    })

    this.connection.onreconnected(() => {
      console.log('[SignalR] Reconnected')
      this._isConnected = true
      this.onConnectionChanged?.(true)
    })

    this.connection.onclose(() => {
      console.log('[SignalR] Connection closed')
      this._isConnected = false
      this.onConnectionChanged?.(false)
    })

    await this.connection.start()
    this._isConnected = true
    this.onConnectionChanged?.(true)
    console.log('[SignalR] Connected')
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop()
      this.connection = null
      this._isConnected = false
      this.onConnectionChanged?.(false)
    }
  }

  // ── Hub Methods ─────────────────────────────────────────────────

  async authenticate(discordId: string): Promise<void> {
    // Send as string to avoid JS number precision loss on large snowflake IDs
    await this.connection?.invoke('Authenticate', discordId)
  }

  async joinGame(gameId: number): Promise<void> {
    await this.connection?.invoke('JoinGame', gameId)
  }

  async leaveGame(gameId: number): Promise<void> {
    await this.connection?.invoke('LeaveGame', gameId)
  }

  async requestGameState(gameId: number): Promise<void> {
    await this.connection?.invoke('RequestGameState', gameId)
  }

  async requestLobbyState(): Promise<void> {
    await this.connection?.invoke('RequestLobbyState')
  }

  // ── Game Actions ────────────────────────────────────────────────

  async attack(gameId: number, targetPlace: number): Promise<void> {
    await this.connection?.invoke('Attack', gameId, targetPlace)
  }

  async block(gameId: number): Promise<void> {
    await this.connection?.invoke('Block', gameId)
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

  async predict(gameId: number, targetPlayerId: string, characterName: string): Promise<void> {
    await this.connection?.invoke('Predict', gameId, targetPlayerId, characterName)
  }

  async aramReroll(gameId: number, slot: number): Promise<void> {
    await this.connection?.invoke('AramReroll', gameId, slot)
  }

  async aramConfirm(gameId: number): Promise<void> {
    await this.connection?.invoke('AramConfirm', gameId)
  }

  // ── Darksci / Young Gleb ───────────────────────────────────────

  async darksciChoice(gameId: number, isStable: boolean): Promise<void> {
    await this.connection?.invoke('DarksciChoice', gameId, isStable)
  }

  async youngGleb(gameId: number): Promise<void> {
    await this.connection?.invoke('YoungGleb', gameId)
  }

  async dopaChoice(gameId: number, tactic: string): Promise<void> {
    await this.connection?.invoke('DopaChoice', gameId, tactic)
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
    await this.connection?.invoke('RegisterWebAccount', username)
  }

  async createWebGame(): Promise<void> {
    await this.connection?.invoke('CreateWebGame')
  }

  async joinWebGame(gameId: number): Promise<void> {
    await this.connection?.invoke('JoinWebGame', gameId)
  }
}

// Singleton instance
export const signalrService = new SignalRService()
