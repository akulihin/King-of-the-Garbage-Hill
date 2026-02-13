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
}

export type Player = {
  playerId: string
  discordUsername: string
  isBot: boolean
  isWebPlayer: boolean
  teamId: number
  character: Character
  status: PlayerStatus
  predictions?: Prediction[]
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
  classStatDisplayText: string
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
}

export type ActionResult = {
  action: string
  success: boolean
  error?: string
}

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

  async setPreferWeb(gameId: number, preferWeb: boolean): Promise<void> {
    await this.connection?.invoke('SetPreferWeb', gameId, preferWeb)
  }
}

// Singleton instance
export const signalrService = new SignalRService()
