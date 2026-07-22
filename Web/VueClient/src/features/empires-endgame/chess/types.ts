export const CHESS_SIDES = ['white', 'black'] as const
export const CHESS_ROLES = ['king', 'queen', 'rook', 'bishop', 'knight', 'pawn'] as const
export const CHESS_CONTROL_PHASES = ['regular', 'anton-extra'] as const

export type ChessSide = typeof CHESS_SIDES[number]
export type ChessRole = typeof CHESS_ROLES[number]
export type ChessControlPhase = typeof CHESS_CONTROL_PHASES[number]
export type ChessSquare = string

export type ChessOutcome = 'white-win' | 'black-win' | 'draw' | 'aborted' | 'invalid'
export type ChessTerminalReason =
  | 'black-army-captured'
  | 'white-king-captured'
  | 'white-checkmated'
  | 'stalemate'
  | 'ply-cap'
  | 'threefold-repetition'
  | 'invalid-command'

export interface ChessRulesIdentity {
  configSchemaVersion: number
  rulesDigest: string
}

export interface ChessPieceSetup {
  id: string
  side: ChessSide
  role: ChessRole
  square: ChessSquare
  /** Stable campaign/content identity for a later integration layer. */
  sourceDefinitionId?: string
  /** The shared black knight that white may move during the extra-action phase. */
  anton?: boolean
}

export interface ChessVariantRules {
  boardWidth: 8
  boardHeight: 8
  firstSide: 'white'
  castling: false
  enPassant: false
  promotion: 'queen-only'
  blackKing: 'absent'
  whiteVictory: 'capture-all-black'
  whiteLoss: 'king-captured-or-checkmated'
  drawPlyLimit: number
  repetitionCount: number
}

export interface ChessAntonRules {
  pieceId: string
  initialSquare: 'g8'
  extraEveryPlayerTurns: number
  extraAction: 'optional-before-black-turn'
}

export interface ChessSettlementConfig {
  goldResourceId: string
  knowledgeResourceId: string
  victoryGold: number
  victoryKnowledge: number
  defeatAllCityLoyaltyDelta: number
  drawAllCityLoyaltyDelta: number
  abortAllCityLoyaltyDelta: number
}

export interface EmpiresChessConfig {
  enabled: boolean
  entryCapitalSiteId: string
  resultLogLimit: number
  maxCommands: number
  rules: ChessVariantRules
  anton: ChessAntonRules
  setup: ChessPieceSetup[]
  settlement: ChessSettlementConfig
}

export interface ChessPlan {
  id: string
  sessionId: string
  rulesIdentity: ChessRulesIdentity
  rules: ChessVariantRules
  anton: ChessAntonRules
  setup: ChessPieceSetup[]
  settlement: ChessSettlementConfig
  maxCommands: number
}

export interface ChessPieceState extends ChessPieceSetup {
  hasMoved: boolean
}

export interface ChessMove {
  pieceId: string
  from: ChessSquare
  to: ChessSquare
  capturedPieceId: string | null
  promotion: 'queen' | null
}

interface ChessCommandIdentity {
  sequence: number
  sessionId: string
  planId: string
  side: ChessSide
  phase: ChessControlPhase
}

export type ChessCommand = ChessCommandIdentity & (
  | {
    kind: 'move'
    from: ChessSquare
    to: ChessSquare
    promotion?: 'queen'
  }
  | {
    kind: 'skip-anton'
  }
)

export interface ChessState {
  pieces: Record<string, ChessPieceState>
  sideToAct: ChessSide
  phase: ChessControlPhase
  commandLog: ChessCommand[]
  capturedPieceIds: string[]
  ply: number
  playerTurnsCompleted: number
  playerTurnsSinceAntonExtra: number
  positionCounts: Record<string, number>
  lastMove: ChessMove | null
  outcome: Exclude<ChessOutcome, 'aborted'> | null
  terminalReason: ChessTerminalReason | null
  error: string | null
}

export interface ChessResult {
  kind: 'chess'
  sessionId: string
  planId: string
  planDigest: string
  commandDigest: string
  rulesIdentity: ChessRulesIdentity
  seed: string | number
  commandLog: ChessCommand[]
  outcome: ChessOutcome
  terminalReason: ChessTerminalReason | 'aborted' | 'incomplete'
  winner: ChessSide | null
  completedPly: number
  finalPositionDigest: string
  capturedPieceIds: string[]
  error: string | null
}
