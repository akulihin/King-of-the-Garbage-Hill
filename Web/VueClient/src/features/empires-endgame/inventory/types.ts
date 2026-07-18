import type { EmpiresDeferredSubfeature } from '../types'

export const INVENTORY_MOVES = ['left', 'right'] as const

export type InventoryMove = typeof INVENTORY_MOVES[number]
export type InventoryOutcome = 'completed' | 'failure' | 'aborted'
export type InventoryTerminalReason =
  | 'inventory-complete'
  | 'cart-overflow'
  | 'tick-cap'
  | 'aborted'
  | 'invalid-command'

export interface InventoryPoint {
  x: number
  y: number
}

export type InventoryItemContent =
  | { kind: 'resource', resourceId: string }
  | { kind: 'equipment', equipmentId: string }

export interface InventoryItemDefinition {
  id: string
  name: string
  cells: InventoryPoint[]
  weight: number
  content: InventoryItemContent
  deferredReason?: string
}

export interface EmpiresInventoryConfig {
  enabled: boolean
  tickMs: number
  maxTicks: number
  maxCommands: number
  resultLogLimit: number
  maxCatchUpTicksPerFrame: number
  maxItems: number
  targetUnitsPerItem: number
  board: {
    width: number
    height: number
    cartHeight: number
  }
  gravity: {
    intervalTicks: number
    spawnDelayTicks: number
  }
  scoring: {
    pointsPerWeight: number
    fullRowBonus: number
  }
  skipPolicy: 'direct-provision'
  abortPolicy: 'abort-expedition'
  itemDefinitions: InventoryItemDefinition[]
  deferredSubfeatures: EmpiresDeferredSubfeature[]
}

export interface InventoryRulesIdentity {
  configSchemaVersion: number
  rulesDigest: string
}

export interface InventoryItemInstance {
  id: string
  definitionId: string
  originCityId: string
  content: InventoryItemContent
  amount: number
}

export interface InventoryPlan {
  id: string
  sessionId: string
  rulesIdentity: InventoryRulesIdentity
  expeditionId: string
  expeditionAttempt: number
  originRegionId: string
  provisionResourceId: string
  requestedProvisionAmount: number
  requiredProvisionAmount: number
  eligibleProvisionAmount: number
  rosterUnitInstanceIds: string[]
  tickMs: number
  maxTicks: number
  maxCommands: number
  maxCatchUpTicksPerFrame: number
  maxItems: number
  board: EmpiresInventoryConfig['board']
  gravity: EmpiresInventoryConfig['gravity']
  scoring: EmpiresInventoryConfig['scoring']
  itemDefinitions: InventoryItemDefinition[]
  itemInstances: InventoryItemInstance[]
}

interface InventoryCommandIdentity {
  tick: number
  sequence: number
  sessionId: string
  planId: string
}

export type InventoryCommand = InventoryCommandIdentity & (
  | { kind: 'move', direction: InventoryMove }
  | { kind: 'rotate' }
  | { kind: 'place' }
)

export interface InventoryActiveItem {
  instanceId: string
  definitionId: string
  anchor: InventoryPoint
  rotation: 0 | 1 | 2 | 3
  nextGravityTick: number
}

export interface InventoryPlacement {
  instanceId: string
  definitionId: string
  anchor: InventoryPoint
  rotation: 0 | 1 | 2 | 3
  cells: InventoryPoint[]
  amount: number
  weight: number
  score: number
}

export interface InventorySimulationState {
  tick: number
  rng: { state: number, draws: number }
  queueItemInstanceIds: string[]
  activeItem: InventoryActiveItem | null
  placements: InventoryPlacement[]
  nextSpawnTick: number
  score: number
  completedRows: number[]
  commandLog: InventoryCommand[]
  terminalReason: InventoryTerminalReason | null
  error: string | null
}

export interface InventoryResult {
  kind: 'inventory'
  sessionId: string
  planId: string
  planDigest: string
  commandDigest: string
  rulesIdentity: InventoryRulesIdentity
  seed: string | number
  expeditionId: string
  expeditionAttempt: number
  outcome: InventoryOutcome
  terminalReason: InventoryTerminalReason
  completedTick: number
  score: number
  efficiencyPercent: number
  packedProvisionAmount: number
  eligibleProvisionAmount: number
  packedItemInstanceIds: string[]
  unpackedItemInstanceIds: string[]
  placements: InventoryPlacement[]
  commandLog: InventoryCommand[]
  error: string | null
}

export interface InventoryFrameClock {
  ticks: number
  accumulatorMs: number
  droppedMs: number
}
