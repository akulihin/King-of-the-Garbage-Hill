import type {
  ChessAntonRules,
  ChessCommand,
  ChessControlPhase,
  ChessMove,
  ChessPieceSetup,
  ChessPieceState,
  ChessPlan,
  ChessResult,
  ChessRole,
  ChessRulesIdentity,
  ChessSettlementConfig,
  ChessSide,
  ChessSquare,
  ChessState,
  ChessTerminalReason,
  ChessVariantRules,
  EmpiresChessConfig,
} from './types'

interface ChessCoordinates {
  x: number
  y: number
}

const FILES = 'abcdefgh'
const PIECE_VALUES: Record<ChessRole, number> = {
  king: 10_000,
  queen: 900,
  rook: 500,
  bishop: 325,
  knight: 300,
  pawn: 100,
}

const KNIGHT_DELTAS: readonly ChessCoordinates[] = [
  { x: -2, y: -1 }, { x: -2, y: 1 }, { x: -1, y: -2 }, { x: -1, y: 2 },
  { x: 1, y: -2 }, { x: 1, y: 2 }, { x: 2, y: -1 }, { x: 2, y: 1 },
]
const KING_DELTAS: readonly ChessCoordinates[] = [
  { x: -1, y: -1 }, { x: 0, y: -1 }, { x: 1, y: -1 },
  { x: -1, y: 0 }, { x: 1, y: 0 },
  { x: -1, y: 1 }, { x: 0, y: 1 }, { x: 1, y: 1 },
]
const ROOK_DIRECTIONS: readonly ChessCoordinates[] = [
  { x: -1, y: 0 }, { x: 1, y: 0 }, { x: 0, y: -1 }, { x: 0, y: 1 },
]
const BISHOP_DIRECTIONS: readonly ChessCoordinates[] = [
  { x: -1, y: -1 }, { x: -1, y: 1 }, { x: 1, y: -1 }, { x: 1, y: 1 },
]

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function canonicalValue(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(canonicalValue)
  if (!value || typeof value !== 'object') return value
  return Object.fromEntries(Object.entries(value as Record<string, unknown>)
    .sort(([left], [right]) => stableCompare(left, right))
    .map(([key, child]) => [key, canonicalValue(child)]))
}

/** Stable, compact identity for plans, command logs, and replay positions. */
export function digestChessValue(value: unknown): string {
  const text = JSON.stringify(canonicalValue(value))
  let hash = 0x811c9dc5
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index)
    hash = Math.imul(hash, 0x01000193)
  }
  return `chess-${(hash >>> 0).toString(16).padStart(8, '0')}-${text.length}`
}

export function chessSquareCoordinates(square: ChessSquare): ChessCoordinates | null {
  if (!/^[a-h][1-8]$/.test(square)) return null
  return { x: FILES.indexOf(square[0]), y: Number(square[1]) - 1 }
}

export function chessSquareFromCoordinates(coordinates: ChessCoordinates): ChessSquare | null {
  if (!Number.isInteger(coordinates.x) || !Number.isInteger(coordinates.y)
    || coordinates.x < 0 || coordinates.x >= 8
    || coordinates.y < 0 || coordinates.y >= 8) return null
  return `${FILES[coordinates.x]}${coordinates.y + 1}`
}

function ruleIdentityPayload(
  rules: ChessVariantRules,
  anton: ChessAntonRules,
  setup: readonly ChessPieceSetup[],
  maxCommands: number,
  settlement: ChessSettlementConfig,
): unknown {
  return {
    rules,
    anton,
    setup: setup.slice().sort((left, right) => stableCompare(left.id, right.id)),
    maxCommands,
    settlement,
  }
}

export function createChessRulesIdentity(
  configSchemaVersion: number,
  rules: ChessVariantRules,
  anton: ChessAntonRules,
  setup: readonly ChessPieceSetup[],
  maxCommands: number,
  settlement: ChessSettlementConfig,
): ChessRulesIdentity {
  return {
    configSchemaVersion,
    rulesDigest: digestChessValue(ruleIdentityPayload(rules, anton, setup, maxCommands, settlement)),
  }
}

function recommendedSetup(): ChessPieceSetup[] {
  const whiteBackRank: Array<[string, ChessRole, string | undefined]> = [
    ['white-treasury-rook', 'rook', undefined],
    ['white-knight-b1', 'knight', undefined],
    ['white-hearts-ace', 'bishop', 'card-hearts-ace'],
    ['white-hearts-king', 'queen', 'card-hearts-king'],
    ['white-hearts-queen', 'king', 'card-hearts-queen'],
    ['white-hearts-jack', 'bishop', 'card-hearts-jack'],
    ['white-knight-g1', 'knight', undefined],
    ['white-clean-streets', 'rook', 'card-clubs-2'],
  ]
  const blackBackRank: Array<[string, ChessRole, ChessSquare]> = [
    ['black-rook-a8', 'rook', 'a8'],
    ['black-knight-b8', 'knight', 'b8'],
    ['black-bishop-c8', 'bishop', 'c8'],
    ['black-queen-d8', 'queen', 'd8'],
    ['black-bishop-f8', 'bishop', 'f8'],
    ['black-anton', 'knight', 'g8'],
    ['black-rook-h8', 'rook', 'h8'],
  ]
  return [
    ...whiteBackRank.map(([id, role, sourceDefinitionId], index) => ({
      id,
      side: 'white' as const,
      role,
      square: `${FILES[index]}1`,
      ...(sourceDefinitionId ? { sourceDefinitionId } : {}),
    })),
    ...Array.from({ length: 8 }, (_, index) => ({
      id: `white-pawn-${FILES[index]}2`,
      side: 'white' as const,
      role: 'pawn' as const,
      square: `${FILES[index]}2`,
    })),
    ...blackBackRank.map(([id, role, square]) => ({
      id,
      side: 'black' as const,
      role,
      square,
      ...(id === 'black-anton'
        ? { sourceDefinitionId: 'card-spades-jack', anton: true }
        : {}),
    })),
    ...Array.from({ length: 8 }, (_, index) => ({
      id: `black-pawn-${FILES[index]}7`,
      side: 'black' as const,
      role: 'pawn' as const,
      square: `${FILES[index]}7`,
    })),
  ]
}

export function createRecommendedChessPlan(
  options: {
    id?: string
    sessionId?: string
    maxCommands?: number
    configSchemaVersion?: number
  } = {},
): ChessPlan {
  const config = createRecommendedChessConfig()
  if (options.maxCommands !== undefined) config.maxCommands = options.maxCommands
  return createChessPlanFromConfig(options.configSchemaVersion ?? 19, config, {
    id: options.id ?? 'empires-chess',
    sessionId: options.sessionId ?? 'empires-chess-session',
  })
}

export function createRecommendedChessConfig(): EmpiresChessConfig {
  const rules: ChessVariantRules = {
    boardWidth: 8,
    boardHeight: 8,
    firstSide: 'white',
    castling: false,
    enPassant: false,
    promotion: 'queen-only',
    blackKing: 'absent',
    whiteVictory: 'capture-all-black',
    whiteLoss: 'king-captured-or-checkmated',
    drawPlyLimit: 100,
    repetitionCount: 3,
  }
  const anton: ChessAntonRules = {
    pieceId: 'black-anton',
    initialSquare: 'g8',
    extraEveryPlayerTurns: 2,
    extraAction: 'optional-before-black-turn',
  }
  const setup = recommendedSetup()
  return {
    enabled: true,
    entryCapitalSiteId: 'capital-coliseum',
    resultLogLimit: 32,
    maxCommands: 256,
    rules,
    anton,
    setup,
    settlement: {
      goldResourceId: 'gold',
      knowledgeResourceId: 'knowledge',
      victoryGold: 2_500,
      victoryKnowledge: 1_000,
      defeatAllCityLoyaltyDelta: -1,
      drawAllCityLoyaltyDelta: 0,
      abortAllCityLoyaltyDelta: -1,
    },
  }
}

export function createChessPlanFromConfig(
  configSchemaVersion: number,
  config: EmpiresChessConfig,
  identity: { id: string, sessionId: string },
): ChessPlan {
  const rules = clone(config.rules)
  const anton = clone(config.anton)
  const setup = clone(config.setup)
  const settlement = clone(config.settlement)
  return {
    ...identity,
    rulesIdentity: createChessRulesIdentity(
      configSchemaVersion,
      rules,
      anton,
      setup,
      config.maxCommands,
      settlement,
    ),
    rules,
    anton,
    setup,
    settlement,
    maxCommands: config.maxCommands,
  }
}

export function validateChessConfig(
  config: EmpiresChessConfig,
  configSchemaVersion: number,
): string[] {
  const errors: string[] = []
  if (typeof config?.enabled !== 'boolean') errors.push('Chess enabled must be boolean.')
  if (!Number.isInteger(config?.resultLogLimit) || config.resultLogLimit < 1) {
    errors.push('Chess resultLogLimit must be a positive integer.')
  }
  if (!Number.isInteger(config?.maxCommands) || config.maxCommands < 1) {
    errors.push('Chess maxCommands must be a positive integer.')
  }
  if (!config?.rules || !config?.anton || !Array.isArray(config?.setup) || !config?.settlement) {
    errors.push('Chess rules, Anton, setup, and settlement are required.')
    return errors
  }
  const settlementValues = [
    config.settlement.victoryGold,
    config.settlement.victoryKnowledge,
    config.settlement.defeatAllCityLoyaltyDelta,
    config.settlement.drawAllCityLoyaltyDelta,
    config.settlement.abortAllCityLoyaltyDelta,
  ]
  if (settlementValues.some(value => !Number.isFinite(value))
    || config.settlement.victoryGold < 0
    || config.settlement.victoryKnowledge < 0
    || config.settlement.defeatAllCityLoyaltyDelta > 0
    || config.settlement.abortAllCityLoyaltyDelta > 0) {
    errors.push('Chess settlement rewards and loyalty consequences are invalid.')
  }
  if (!config.enabled) return errors
  if (!config.entryCapitalSiteId?.trim()) errors.push('Chess requires a capital-site entry ID.')
  if (!config.settlement.goldResourceId?.trim() || !config.settlement.knowledgeResourceId?.trim()) {
    errors.push('Chess settlement resource IDs are required.')
  }
  if (!Number.isSafeInteger(configSchemaVersion) || configSchemaVersion < 1) {
    errors.push('Chess config schema identity is invalid.')
    return errors
  }
  const plan = createChessPlanFromConfig(configSchemaVersion, config, {
    id: 'chess-config-validation',
    sessionId: 'chess-config-validation-session',
  })
  return [...errors, ...validateChessPlan(plan)]
}

export function validateChessPlan(plan: ChessPlan): string[] {
  const errors: string[] = []
  if (!plan.id?.trim() || !plan.sessionId?.trim()) errors.push('Chess plan IDs are required.')
  if (plan.rules.boardWidth !== 8 || plan.rules.boardHeight !== 8
    || plan.rules.firstSide !== 'white') errors.push('Chess uses an 8x8 board with white moving first.')
  if (plan.rules.castling !== false || plan.rules.enPassant !== false
    || plan.rules.promotion !== 'queen-only') {
    errors.push('Chess allows queen-only promotion and does not allow castling or en passant.')
  }
  if (plan.rules.blackKing !== 'absent'
    || plan.rules.whiteVictory !== 'capture-all-black'
    || plan.rules.whiteLoss !== 'king-captured-or-checkmated') {
    errors.push('Chess termination rules do not match the kingless-black capture-all variant.')
  }
  if (!Number.isInteger(plan.rules.drawPlyLimit) || plan.rules.drawPlyLimit < 1
    || !Number.isInteger(plan.rules.repetitionCount) || plan.rules.repetitionCount < 2) {
    errors.push('Chess draw limits must be positive and repetition must require at least two positions.')
  }
  if (!Number.isInteger(plan.maxCommands) || plan.maxCommands < plan.rules.drawPlyLimit
    || plan.maxCommands > 4096) {
    errors.push('Chess maxCommands must cover the ply cap and remain at or below 4096.')
  }
  if (!Array.isArray(plan.setup) || plan.setup.length === 0 || plan.setup.length > 64) {
    errors.push('Chess setup must contain between 1 and 64 pieces.')
  }
  if (!plan.settlement || !plan.settlement.goldResourceId?.trim()
    || !plan.settlement.knowledgeResourceId?.trim()
    || [
      plan.settlement.victoryGold,
      plan.settlement.victoryKnowledge,
      plan.settlement.defeatAllCityLoyaltyDelta,
      plan.settlement.drawAllCityLoyaltyDelta,
      plan.settlement.abortAllCityLoyaltyDelta,
    ].some(value => !Number.isFinite(value))) {
    errors.push('Chess plan settlement is incomplete.')
  }

  const ids = new Set<string>()
  const squares = new Set<string>()
  for (const piece of plan.setup ?? []) {
    if (!piece.id?.trim() || ids.has(piece.id)) errors.push(`Chess piece ${piece.id || '<missing>'} has an invalid ID.`)
    if (!chessSquareCoordinates(piece.square) || squares.has(piece.square)) {
      errors.push(`Chess piece ${piece.id || '<missing>'} has an invalid or occupied square.`)
    }
    if (!['white', 'black'].includes(piece.side)
      || !['king', 'queen', 'rook', 'bishop', 'knight', 'pawn'].includes(piece.role)) {
      errors.push(`Chess piece ${piece.id || '<missing>'} has an invalid side or role.`)
    }
    ids.add(piece.id)
    squares.add(piece.square)
  }
  const whiteKings = plan.setup.filter(piece => piece.side === 'white' && piece.role === 'king')
  const blackKings = plan.setup.filter(piece => piece.side === 'black' && piece.role === 'king')
  if (whiteKings.length !== 1) errors.push('Chess setup requires exactly one white king.')
  if (blackKings.length !== 0) errors.push('Chess setup must not contain a black king.')
  if (!plan.setup.some(piece => piece.side === 'black')) errors.push('Chess setup requires capturable black material.')

  const anton = plan.setup.find(piece => piece.id === plan.anton.pieceId)
  if (!anton || anton.side !== 'black' || anton.role !== 'knight' || anton.square !== 'g8'
    || anton.anton !== true || plan.setup.filter(piece => piece.anton).length !== 1
    || plan.anton.initialSquare !== 'g8'
    || !Number.isInteger(plan.anton.extraEveryPlayerTurns)
    || plan.anton.extraEveryPlayerTurns < 1
    || plan.anton.extraAction !== 'optional-before-black-turn') {
    errors.push('Chess Anton must be the one shared black knight on g8 with a positive extra-action cadence.')
  }

  const expectedIdentity = createChessRulesIdentity(
    plan.rulesIdentity.configSchemaVersion,
    plan.rules,
    plan.anton,
    plan.setup,
    plan.maxCommands,
    plan.settlement,
  )
  if (!Number.isSafeInteger(plan.rulesIdentity.configSchemaVersion)
    || plan.rulesIdentity.configSchemaVersion < 1
    || plan.rulesIdentity.rulesDigest !== expectedIdentity.rulesDigest) {
    errors.push('Chess rules identity does not match the immutable plan.')
  }
  return errors
}

function copyState(state: ChessState): ChessState {
  return clone(state)
}

function pieceAt(pieces: Readonly<Record<string, ChessPieceState>>, square: ChessSquare): ChessPieceState | null {
  return Object.values(pieces).find(piece => piece.square === square) ?? null
}

function moveSort(left: ChessMove, right: ChessMove): number {
  return stableCompare(left.from, right.from)
    || stableCompare(left.to, right.to)
    || stableCompare(left.pieceId, right.pieceId)
}

function controllerFor(state: ChessState): ChessSide {
  return state.phase === 'anton-extra' ? 'white' : state.sideToAct
}

function selectablePiece(plan: ChessPlan, state: ChessState, piece: ChessPieceState): boolean {
  if (state.phase === 'anton-extra') return piece.id === plan.anton.pieceId
  return piece.side === state.sideToAct
}

function addStepMove(
  moves: ChessMove[],
  pieces: Readonly<Record<string, ChessPieceState>>,
  piece: ChessPieceState,
  controller: ChessSide,
  to: ChessSquare | null,
): boolean {
  if (!to) return false
  const occupant = pieceAt(pieces, to)
  if (occupant?.side === controller) return true
  moves.push({
    pieceId: piece.id,
    from: piece.square,
    to,
    capturedPieceId: occupant?.id ?? null,
    promotion: piece.role === 'pawn' && (to[1] === '1' || to[1] === '8') ? 'queen' : null,
  })
  return Boolean(occupant)
}

function pseudoMovesForPiece(
  state: ChessState,
  piece: ChessPieceState,
  controller: ChessSide,
): ChessMove[] {
  const from = chessSquareCoordinates(piece.square)
  if (!from) return []
  const moves: ChessMove[] = []
  const step = (delta: ChessCoordinates) => addStepMove(
    moves,
    state.pieces,
    piece,
    controller,
    chessSquareFromCoordinates({ x: from.x + delta.x, y: from.y + delta.y }),
  )
  const slide = (directions: readonly ChessCoordinates[]) => {
    for (const direction of directions) {
      for (let distance = 1; distance < 8; distance += 1) {
        const stopped = step({ x: direction.x * distance, y: direction.y * distance })
        if (stopped) break
      }
    }
  }

  if (piece.role === 'knight') KNIGHT_DELTAS.forEach(step)
  else if (piece.role === 'king') KING_DELTAS.forEach(step)
  else if (piece.role === 'rook') slide(ROOK_DIRECTIONS)
  else if (piece.role === 'bishop') slide(BISHOP_DIRECTIONS)
  else if (piece.role === 'queen') slide([...ROOK_DIRECTIONS, ...BISHOP_DIRECTIONS])
  else {
    const direction = piece.side === 'white' ? 1 : -1
    const one = chessSquareFromCoordinates({ x: from.x, y: from.y + direction })
    if (one && !pieceAt(state.pieces, one)) {
      addStepMove(moves, state.pieces, piece, controller, one)
      const startRank = piece.side === 'white' ? 1 : 6
      const two = chessSquareFromCoordinates({ x: from.x, y: from.y + direction * 2 })
      if (!piece.hasMoved && from.y === startRank && two && !pieceAt(state.pieces, two)) {
        addStepMove(moves, state.pieces, piece, controller, two)
      }
    }
    for (const xDelta of [-1, 1]) {
      const targetSquare = chessSquareFromCoordinates({ x: from.x + xDelta, y: from.y + direction })
      const target = targetSquare ? pieceAt(state.pieces, targetSquare) : null
      if (target && target.side !== controller) {
        addStepMove(moves, state.pieces, piece, controller, targetSquare)
      }
    }
  }
  return moves
}

function attacksSquare(
  pieces: Readonly<Record<string, ChessPieceState>>,
  piece: ChessPieceState,
  targetSquare: ChessSquare,
): boolean {
  const from = chessSquareCoordinates(piece.square)
  const target = chessSquareCoordinates(targetSquare)
  if (!from || !target) return false
  const xDelta = target.x - from.x
  const yDelta = target.y - from.y
  if (piece.role === 'pawn') {
    const direction = piece.side === 'white' ? 1 : -1
    return yDelta === direction && Math.abs(xDelta) === 1
  }
  if (piece.role === 'knight') return Math.abs(xDelta * yDelta) === 2
  if (piece.role === 'king') return Math.max(Math.abs(xDelta), Math.abs(yDelta)) === 1

  const diagonal = Math.abs(xDelta) === Math.abs(yDelta) && xDelta !== 0
  const orthogonal = (xDelta === 0) !== (yDelta === 0)
  if (piece.role === 'bishop' && !diagonal) return false
  if (piece.role === 'rook' && !orthogonal) return false
  if (piece.role === 'queen' && !diagonal && !orthogonal) return false
  const xStep = Math.sign(xDelta)
  const yStep = Math.sign(yDelta)
  let x = from.x + xStep
  let y = from.y + yStep
  while (x !== target.x || y !== target.y) {
    const square = chessSquareFromCoordinates({ x, y })
    if (!square || pieceAt(pieces, square)) return false
    x += xStep
    y += yStep
  }
  return true
}

function whiteKingInCheck(pieces: Readonly<Record<string, ChessPieceState>>): boolean {
  const king = Object.values(pieces).find(piece => piece.side === 'white' && piece.role === 'king')
  if (!king) return true
  return Object.values(pieces)
    .filter(piece => piece.side === 'black')
    .some(piece => attacksSquare(pieces, piece, king.square))
}

export function isWhiteKingInCheck(state: ChessState): boolean {
  return whiteKingInCheck(state.pieces)
}

function applyMoveToPieces(
  pieces: Readonly<Record<string, ChessPieceState>>,
  move: ChessMove,
): Record<string, ChessPieceState> {
  const next = clone(pieces) as Record<string, ChessPieceState>
  if (move.capturedPieceId) delete next[move.capturedPieceId]
  const piece = next[move.pieceId]
  if (!piece) return next
  piece.square = move.to
  piece.hasMoved = true
  if (move.promotion === 'queen') piece.role = 'queen'
  return next
}

export function legalChessMoves(plan: ChessPlan, state: ChessState): ChessMove[] {
  if (state.terminalReason) return []
  const controller = controllerFor(state)
  return Object.values(state.pieces)
    .filter(piece => selectablePiece(plan, state, piece))
    .flatMap(piece => pseudoMovesForPiece(state, piece, controller))
    .filter((move) => {
      if (controller !== 'white') return true
      return !whiteKingInCheck(applyMoveToPieces(state.pieces, move))
    })
    .sort(moveSort)
}

function positionPayload(state: ChessState): unknown {
  return {
    pieces: Object.values(state.pieces)
      .map(piece => ({
        id: piece.id,
        side: piece.side,
        role: piece.role,
        square: piece.square,
        anton: Boolean(piece.anton),
      }))
      .sort((left, right) => stableCompare(left.id, right.id)),
    sideToAct: state.sideToAct,
    phase: state.phase,
    playerTurnsSinceAntonExtra: state.playerTurnsSinceAntonExtra,
  }
}

export function chessPositionKey(state: ChessState): string {
  return digestChessValue(positionPayload(state))
}

function markTerminal(
  state: ChessState,
  outcome: NonNullable<ChessState['outcome']>,
  reason: ChessTerminalReason,
  error: string | null = null,
): void {
  state.outcome = outcome
  state.terminalReason = reason
  state.error = error
}

function recordPosition(state: ChessState): number {
  const key = chessPositionKey(state)
  state.positionCounts[key] = (state.positionCounts[key] ?? 0) + 1
  return state.positionCounts[key]
}

function adjudicateNoMoves(plan: ChessPlan, state: ChessState): void {
  if (state.terminalReason || state.phase === 'anton-extra') return
  if (legalChessMoves(plan, state).length > 0) return
  if (state.sideToAct === 'white' && isWhiteKingInCheck(state)) {
    markTerminal(state, 'black-win', 'white-checkmated')
  } else {
    markTerminal(state, 'draw', 'stalemate')
  }
}

export function createChessState(plan: ChessPlan): ChessState {
  const errors = validateChessPlan(plan)
  const pieces = Object.fromEntries(plan.setup.map(piece => [piece.id, {
    ...clone(piece),
    hasMoved: false,
  } satisfies ChessPieceState]))
  const state: ChessState = {
    pieces,
    sideToAct: 'white',
    phase: 'regular',
    commandLog: [],
    capturedPieceIds: [],
    ply: 0,
    playerTurnsCompleted: 0,
    playerTurnsSinceAntonExtra: 0,
    positionCounts: {},
    lastMove: null,
    outcome: null,
    terminalReason: null,
    error: null,
  }
  recordPosition(state)
  if (errors.length > 0) {
    markTerminal(state, 'invalid', 'invalid-command', errors.join('; '))
  } else {
    adjudicateNoMoves(plan, state)
  }
  return state
}

function commandIdentityReason(plan: ChessPlan, state: ChessState, command: ChessCommand): string | null {
  if (state.terminalReason) return `Chess game is already terminal: ${state.terminalReason}.`
  if (command.sequence !== state.commandLog.length) return 'Chess command sequence is not contiguous.'
  if (command.sessionId !== plan.sessionId || command.planId !== plan.id) {
    return 'Chess command belongs to another plan or session.'
  }
  if (command.side !== state.sideToAct || command.phase !== state.phase) {
    return 'Chess command controller or phase does not match the current action.'
  }
  if (state.commandLog.length >= plan.maxCommands) return 'Chess command limit reached.'
  if (command.kind === 'skip-anton') {
    return state.phase === 'anton-extra' ? null : 'Only the optional Anton action can be skipped.'
  }
  if (!chessSquareCoordinates(command.from) || !chessSquareCoordinates(command.to)) {
    return 'Chess move contains an invalid square.'
  }
  const legal = legalChessMoves(plan, state).find(move => move.from === command.from && move.to === command.to)
  if (!legal) return 'Chess move is illegal in the current position.'
  if (command.promotion && legal.promotion !== command.promotion) {
    return 'Chess promotion is legal only as the automatic queen promotion.'
  }
  return null
}

export function chessCommandDisabledReason(
  plan: ChessPlan,
  state: ChessState,
  command: ChessCommand,
): string | null {
  return commandIdentityReason(plan, state, command)
}

function advanceTurnAfterMove(
  plan: ChessPlan,
  state: ChessState,
  actingSide: ChessSide,
  actingPhase: ChessControlPhase,
): void {
  if (actingPhase === 'anton-extra') {
    state.playerTurnsSinceAntonExtra = 0
    state.sideToAct = 'black'
    state.phase = 'regular'
    return
  }
  if (actingSide === 'black') {
    state.sideToAct = 'white'
    state.phase = 'regular'
    return
  }
  state.playerTurnsCompleted += 1
  state.playerTurnsSinceAntonExtra += 1
  if (state.playerTurnsSinceAntonExtra >= plan.anton.extraEveryPlayerTurns
    && state.pieces[plan.anton.pieceId]) {
    state.sideToAct = 'white'
    state.phase = 'anton-extra'
  } else {
    state.sideToAct = 'black'
    state.phase = 'regular'
  }
}

function adjudicateAfterAction(plan: ChessPlan, state: ChessState, capturedPiece: ChessPieceState | null): void {
  if (capturedPiece?.side === 'white' && capturedPiece.role === 'king') {
    markTerminal(state, 'black-win', 'white-king-captured')
    return
  }
  if (!Object.values(state.pieces).some(piece => piece.side === 'black')) {
    markTerminal(state, 'white-win', 'black-army-captured')
    return
  }
  adjudicateNoMoves(plan, state)
  if (state.terminalReason) return
  if (state.ply >= plan.rules.drawPlyLimit) {
    markTerminal(state, 'draw', 'ply-cap')
    return
  }
  if (recordPosition(state) >= plan.rules.repetitionCount) {
    markTerminal(state, 'draw', 'threefold-repetition')
  }
}

export function applyChessCommand(plan: ChessPlan, current: ChessState, command: ChessCommand): ChessState {
  const state = copyState(current)
  const reason = commandIdentityReason(plan, state, command)
  if (reason) {
    markTerminal(state, 'invalid', 'invalid-command', reason)
    return state
  }
  state.commandLog.push(clone(command))
  if (command.kind === 'skip-anton') {
    state.playerTurnsSinceAntonExtra = 0
    state.sideToAct = 'black'
    state.phase = 'regular'
    state.lastMove = null
    adjudicateAfterAction(plan, state, null)
    return state
  }

  const move = legalChessMoves(plan, state).find(candidate => (
    candidate.from === command.from && candidate.to === command.to
  ))!
  const capturedPiece = move.capturedPieceId ? clone(state.pieces[move.capturedPieceId]) : null
  const actingSide = state.sideToAct
  const actingPhase = state.phase
  state.pieces = applyMoveToPieces(state.pieces, move)
  if (move.capturedPieceId) state.capturedPieceIds.push(move.capturedPieceId)
  state.lastMove = clone(move)
  state.ply += 1
  advanceTurnAfterMove(plan, state, actingSide, actingPhase)
  adjudicateAfterAction(plan, state, capturedPiece)
  return state
}

export function replayChess(plan: ChessPlan, commandLog: readonly ChessCommand[]): ChessState {
  let state = createChessState(plan)
  for (const command of commandLog) state = applyChessCommand(plan, state, command)
  return state
}

export function createChessCommand(
  plan: ChessPlan,
  state: ChessState,
  value: { kind: 'move', from: ChessSquare, to: ChessSquare, promotion?: 'queen' }
    | { kind: 'skip-anton' },
): ChessCommand {
  return {
    sequence: state.commandLog.length,
    sessionId: plan.sessionId,
    planId: plan.id,
    side: state.sideToAct,
    phase: state.phase,
    ...value,
  } as ChessCommand
}

function resultFromState(
  plan: ChessPlan,
  seed: string | number,
  state: ChessState,
  override?: Pick<ChessResult, 'outcome' | 'terminalReason' | 'error'>,
): ChessResult {
  const outcome = override?.outcome ?? state.outcome ?? 'invalid'
  const terminalReason = override?.terminalReason ?? state.terminalReason ?? 'incomplete'
  return {
    kind: 'chess',
    sessionId: plan.sessionId,
    planId: plan.id,
    planDigest: digestChessValue(plan),
    commandDigest: digestChessValue(state.commandLog),
    rulesIdentity: clone(plan.rulesIdentity),
    seed,
    commandLog: clone(state.commandLog),
    outcome,
    terminalReason,
    winner: outcome === 'white-win' ? 'white' : outcome === 'black-win' ? 'black' : null,
    completedPly: state.ply,
    finalPositionDigest: digestChessValue(positionPayload(state)),
    capturedPieceIds: [...state.capturedPieceIds],
    error: override
      ? override.error
      : state.error ?? (state.outcome ? null : 'Chess game is not terminal.'),
  }
}

export function resolveChess(
  plan: ChessPlan,
  seed: string | number,
  commandLog: readonly ChessCommand[],
): ChessResult {
  return resultFromState(plan, seed, replayChess(plan, commandLog))
}

export function abortChess(
  plan: ChessPlan,
  seed: string | number,
  commandLog: readonly ChessCommand[],
): ChessResult {
  const state = replayChess(plan, commandLog)
  if (state.outcome === 'invalid') return resultFromState(plan, seed, state)
  return resultFromState(plan, seed, state, {
    outcome: 'aborted',
    terminalReason: 'aborted',
    error: null,
  })
}

function aiScore(plan: ChessPlan, state: ChessState, move: ChessMove, command: ChessCommand): number {
  const controller = controllerFor(state)
  const captured = move.capturedPieceId ? state.pieces[move.capturedPieceId] : null
  const next = applyChessCommand(plan, state, command)
  const desiredOutcome = controller === 'white' ? 'white-win' : 'black-win'
  const losingOutcome = controller === 'white' ? 'black-win' : 'white-win'
  let score = next.outcome === desiredOutcome
    ? 1_000_000_000
    : next.outcome === losingOutcome
      ? -1_000_000_000
      : next.outcome === 'draw'
        ? -10_000
        : 0
  score += captured ? PIECE_VALUES[captured.role] * 1000 : 0
  score += move.promotion ? PIECE_VALUES.queen : 0
  if (controller === 'black' && isWhiteKingInCheck(next)) score += 500
  const target = chessSquareCoordinates(move.to)!
  score += 7 - Math.abs(target.x * 2 - 7) - Math.abs(target.y * 2 - 7)
  if (state.phase === 'anton-extra' && !captured) score -= 100
  return score
}

/**
 * Deterministic one-ply policy. It takes an immediate win, then the highest-value capture,
 * then promotion/check/centralization, with coordinate order as the final stable tie-break.
 */
export function chooseDeterministicChessAiCommand(plan: ChessPlan, state: ChessState): ChessCommand | null {
  if (state.terminalReason) return null
  const moves = legalChessMoves(plan, state)
  const candidates = moves.map((move) => {
    const command = createChessCommand(plan, state, {
      kind: 'move',
      from: move.from,
      to: move.to,
      ...(move.promotion ? { promotion: move.promotion } : {}),
    })
    return {
      command,
      score: aiScore(plan, state, move, command),
      key: `${move.from}:${move.to}:${move.pieceId}`,
    }
  })
  if (state.phase === 'anton-extra') {
    candidates.push({
      command: createChessCommand(plan, state, { kind: 'skip-anton' }),
      score: 0,
      key: 'skip-anton',
    })
  }
  return candidates.sort((left, right) => right.score - left.score || stableCompare(left.key, right.key))[0]?.command ?? null
}

export function createDeterministicChessCommandLog(plan: ChessPlan): ChessCommand[] {
  let state = createChessState(plan)
  while (!state.terminalReason && state.commandLog.length < plan.maxCommands) {
    const command = chooseDeterministicChessAiCommand(plan, state)
    if (!command) break
    state = applyChessCommand(plan, state, command)
  }
  return clone(state.commandLog)
}

export function resolveChessWithDeterministicAi(
  plan: ChessPlan,
  seed: string | number = 0,
): ChessResult {
  return resolveChess(plan, seed, createDeterministicChessCommandLog(plan))
}
