import type {
  EmpiresDeferredSubfeature,
  EmpiresDependency,
  EmpiresEffect,
} from '../types'

export const ALCHEMY_SIDES = ['top', 'right', 'bottom', 'left'] as const
export const ALCHEMY_COLORS = ['red', 'yellow', 'blue', 'green', 'gray'] as const
export const ALCHEMY_MOVES = ['up', 'right', 'down', 'left'] as const

export type AlchemySide = typeof ALCHEMY_SIDES[number]
export type AlchemyColor = typeof ALCHEMY_COLORS[number]
export type AlchemyMove = typeof ALCHEMY_MOVES[number]
export type AlchemyMode = 'assembly' | 'disassembly'
export type AlchemyRecipeFamily = 'experiment' | 'medicine' | 'poison'
export type AlchemyOutcome = 'success' | 'failure' | 'explosion' | 'aborted'
export type AlchemyTerminalReason =
  | 'recipe-complete'
  | 'explosion'
  | 'overflow'
  | 'tick-cap'
  | 'aborted'
  | 'invalid-command'

export interface AlchemyPoint {
  x: number
  y: number
}

export interface AlchemyCell extends AlchemyPoint {
  color: AlchemyColor
}

export interface AlchemyTargetCell extends AlchemyPoint {
  /** Omitted means any reagent color is accepted. Gray is always a neutral wildcard. */
  color?: Exclude<AlchemyColor, 'gray'>
}

export interface AlchemyPieceDefinition {
  id: string
  name: string
  cells: AlchemyPoint[]
}

export interface AlchemyRecipeDefinition {
  id: string
  name: string
  description: string
  mode: AlchemyMode
  family: AlchemyRecipeFamily
  initialCells: AlchemyCell[]
  targetCells: AlchemyTargetCell[]
  pieceDefinitionIds: string[]
  prerequisites: EmpiresDependency[]
  rewards: EmpiresEffect[]
  deferredReason?: string
}

export interface AlchemyMutantAftermathDefinition {
  kind: 'mutant-outbreak'
  delayCons: number
  populationLoss: number
  loyaltyDelta: number
}

export interface EmpiresAlchemyConfig {
  enabled: boolean
  buildingId: string
  tickMs: number
  maxTicks: number
  maxCommands: number
  resultLogLimit: number
  maxCatchUpTicksPerFrame: number
  dayCost: number
  board: {
    width: number
    height: number
    centerX: number
    centerY: number
  }
  spawn: {
    minDelayTicks: number
    maxDelayTicks: number
    baseMoveIntervalTicks: number
    inwardSpeedMultiplier: number
  }
  acceleration: {
    baseSpeedPercent: number
    stepPercent: number
    piecesPerStep: number
    explosionThresholdPercent: number
    explosionBoundary: 'above' | 'at-or-above'
  }
  reagents: {
    removeColorCharges: number
    addGrayCharges: number
    resetAccelerationCharges: number
  }
  explosion: {
    epidemicDefinitionId: string
    severityMultiplier: number
    lockBuildingForCon: boolean
    mutantAftermath: AlchemyMutantAftermathDefinition
  }
  colors: Array<Exclude<AlchemyColor, 'gray'>>
  pieces: AlchemyPieceDefinition[]
  recipes: AlchemyRecipeDefinition[]
  deferredSubfeatures: EmpiresDeferredSubfeature[]
}

export interface AlchemyRulesIdentity {
  configSchemaVersion: number
  rulesDigest: string
}

export interface AlchemyPlan {
  id: string
  sessionId: string
  rulesIdentity: AlchemyRulesIdentity
  originCityId: string
  buildingId: string
  recipe: AlchemyRecipeDefinition
  tickMs: number
  maxTicks: number
  maxCommands: number
  maxCatchUpTicksPerFrame: number
  board: EmpiresAlchemyConfig['board']
  spawn: EmpiresAlchemyConfig['spawn']
  acceleration: EmpiresAlchemyConfig['acceleration']
  reagents: EmpiresAlchemyConfig['reagents']
  explosion: EmpiresAlchemyConfig['explosion']
  colors: EmpiresAlchemyConfig['colors']
  pieces: AlchemyPieceDefinition[]
}

interface AlchemyCommandIdentity {
  tick: number
  sequence: number
  sessionId: string
  planId: string
}

export type AlchemyCommand = AlchemyCommandIdentity & (
  | { kind: 'move', direction: AlchemyMove }
  | { kind: 'rotate' }
  | { kind: 'remove-color', color: Exclude<AlchemyColor, 'gray'> }
  | { kind: 'add-gray', pieceId: string }
  | { kind: 'reset-acceleration' }
)

export interface AlchemyActivePiece {
  id: string
  definitionId: string
  side: AlchemySide
  color: AlchemyColor
  anchor: AlchemyPoint
  rotation: 0 | 1 | 2 | 3
  nextMoveTick: number
}

export interface AlchemySimulationState {
  tick: number
  rng: { state: number, draws: number }
  construction: AlchemyCell[]
  activePieces: AlchemyActivePiece[]
  controlledPieceId: string | null
  nextPieceSequence: number
  nextSpawnTick: number
  settledPieces: number
  accelerationPieces: number
  speedPercent: number
  reagentCharges: {
    removeColor: number
    addGray: number
    resetAcceleration: number
  }
  commandLog: AlchemyCommand[]
  terminalReason: AlchemyTerminalReason | null
  error: string | null
}

export interface AlchemyExplosionRequest {
  originCityId: string
  epidemicDefinitionId: string
  severity: number
  mutantAftermath: AlchemyMutantAftermathDefinition
  source: {
    kind: 'alchemy'
    id: string
  }
}

export interface AlchemyResult {
  kind: 'alchemy'
  sessionId: string
  planId: string
  planDigest: string
  commandDigest: string
  rulesIdentity: AlchemyRulesIdentity
  seed: string | number
  recipeId: string
  mode: AlchemyMode
  outcome: AlchemyOutcome
  terminalReason: AlchemyTerminalReason
  completedTick: number
  settledPieces: number
  speedPercent: number
  construction: AlchemyCell[]
  commandLog: AlchemyCommand[]
  explosionRequest: AlchemyExplosionRequest | null
  error: string | null
}

export interface AlchemyFrameClock {
  ticks: number
  accumulatorMs: number
  droppedMs: number
}
