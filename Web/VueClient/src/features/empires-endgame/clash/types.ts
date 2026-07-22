export const CLASH_SIDES = ['attacker', 'defender'] as const
export const CLASH_OUTCOMES = ['victory', 'defeat', 'aborted'] as const

export type ClashSide = typeof CLASH_SIDES[number]
export type ClashOutcome = typeof CLASH_OUTCOMES[number]
export type ClashPhase = 'placement' | 'between-clashes' | 'clash-ready' | 'finished'
export type ClashUnitRank =
  | 'elite'
  | 'legend'
  | 'hero'
  | 'incredible'
  | 'limited'
  | 'convict'
  | 'creature'
  | 'perst'
  | 'one-of-kind'

export type ClashCostBand =
  | 'cheapest'
  | 'very-cheap'
  | 'cheap'
  | 'medium'
  | 'expensive'
  | 'very-expensive'
  | null

export type ClashStatusKind =
  | 'bleeding'
  | 'ignite'
  | 'scorpion-poison'
  | 'cobra-poison'
  | 'centipede-poison'
  | 'karakurt-poison'
  | 'corpse-centipede-poison'
  | 'lhp-toxin'
  | 'neuro-toxin'
  | 'wither'
  | 'freeze'
  | 'stun'
  | 'dodge'
  | 'paralysis'
  | 'disarm'
  | 'rage'

export interface ClashStatusDefinition {
  id: string
  name: string
  kind: ClashStatusKind
  damagePerTurn?: number
  durationTurns?: number | null
  stacks: boolean
  bypassesShields?: boolean
  clearsShields?: boolean
  wakesOnDamage?: boolean
  clearsRage?: boolean
  attackDivisor?: number
  speedDivisor?: number
  thresholdHpExclusive?: number
  delayedDeathTurns?: number
  deferredReason?: string
}

export type ClashTerrainKind =
  | 'high-ground'
  | 'healing-mushrooms'
  | 'acid'
  | 'cordyceps'
  | 'trap'
  | 'fog'

export interface ClashTerrainDefinition {
  id: string
  name: string
  kind: ClashTerrainKind
  speedDelta?: number
  durationTurns?: number
  healingPerTurn?: number
  maxHpMultiplier?: number
  damage?: number
  archerCapacity?: number
  duplicateActivations?: boolean
  hidesEnemyCell?: boolean
  deferredReason?: string
}

export type ClashTemperature = 'cold' | 'temperate' | 'warm' | 'hot'
export type ClashHumidity = 'dry' | 'neutral' | 'humid'

export interface ClashRegionModifierDefinition {
  id: string
  name: string
  speedDelta: number
  supplyMultiplier: number
  temperature: ClashTemperature
  humidity: ClashHumidity
  imperialCountBonus: number
  heatingRequired: boolean
  deferredReason?: string
}

export type ClashPassiveKind =
  | 'shield'
  | 'ranged'
  | 'legion'
  | 'reach'
  | 'adjacency-poke'
  | 'anti-cavalry'
  | 'cavalry'
  | 'heavy-armor'
  | 'status-on-hit'
  | 'status-immunity'
  | 'heal'
  | 'dodge'
  | 'retaliate'
  | 'multi-strike'
  | 'damage-modifier'
  | 'damage-cap'
  | 'reflect'
  | 'disarm'
  | 'morale'
  | 'climate-stat'
  | 'corpse'
  | 'spawn'
  | 'first-strike'
  | 'terrain-immunity'
  | 'death'
  | 'kill-growth'
  | 'row-buff'
  | 'row-damage-share'
  | 'campaign'

export interface ClashPassiveDefinition {
  id: string
  name: string
  description: string
  kind: ClashPassiveKind
  category: 'weapon' | 'armor' | 'shield' | 'unit'
  hidden?: boolean
  value?: number
  charges?: number
  reloadTurns?: number
  range?: number
  statusId?: string
  targetTag?: string
  threshold?: number
  multiplier?: number
  durationTurns?: number
  bypassesShields?: boolean
  area?: 'target' | 'adjacent' | 'row' | 'column' | 'all'
}

export type ClashAbilityKind =
  | 'damage'
  | 'status'
  | 'heal-full'
  | 'cleanse'
  | 'morale'
  | 'cavalry-charge'
  | 'move-to-front'
  | 'spawn'

export interface ClashAbilityDefinition {
  id: string
  name: string
  kind: ClashAbilityKind
  charges: number
  reloadTurns: number
  target: 'self' | 'ally' | 'enemy' | 'cell' | 'row' | 'column' | 'all-enemies'
  value?: number
  statusId?: string
  durationTurns?: number
  area?: 'target' | 'adjacent' | '2x2' | 'row' | 'column' | 'all'
  ignoresShields?: boolean
  affectsAllies?: boolean
  spawnUnitId?: string
}

export interface ClashUnitDefinition {
  id: string
  name: string
  faction: string
  regions: string[]
  ranks: ClashUnitRank[]
  cost: ClashCostBand
  acquisitionTags: string[]
  attack: number | null
  maxHp: number | null
  speed: number | null
  tags: string[]
  passives: ClashPassiveDefinition[]
  abilities: ClashAbilityDefinition[]
  limitPerGame?: number
  hireOnce?: boolean
  deferredReason?: string
  reviewReason?: string
  sourceMessageIds: string[]
}

export interface ClashFieldVariantDefinition {
  id: string
  name: string
  columns: number
  rowsPerSide: number
  reinforcementRows: number
  unitCountMultiplier: number
  terrainCellIds: Array<{
    side: ClashSide
    row: number
    column: number
    terrainId: string
  }>
  deferredReason?: string
}

export interface ClashMoraleConfig {
  positiveThresholdExclusive: number
  negativeThresholdExclusive: number
  positiveActivationCharges: number
  neutralActivationCharges: number
  negativeActivationCooldownTurns: number
  minimum: number
  maximum: number
}

export interface ClashSettlementConfig {
  victoryMoraleDelta: number
  defeatMoraleDelta: number
  abortMoraleDelta: number
  abortAllianceThreatDelta: number
  recruitmentPenaltyPerLoss: number
  growthPenaltyPerLoss: number
}

export interface EmpiresClashConfig {
  enabled: boolean
  resultLogLimit: number
  maxTurns: number
  maxCommands: number
  defaultFieldVariantId: string
  placementFirstSide: ClashSide
  betweenClashesFirstSide: ClashSide
  speedTieRule: 'attacker-first' | 'defender-first'
  turnCapTieWinner: ClashSide
  victoryRule: 'elimination'
  corpseBlocksAdvance: boolean
  onePlacementPerSideBetweenClashes: boolean
  fieldVariants: ClashFieldVariantDefinition[]
  statuses: ClashStatusDefinition[]
  terrain: ClashTerrainDefinition[]
  regions: ClashRegionModifierDefinition[]
  morale: ClashMoraleConfig
  settlement: ClashSettlementConfig
  assaultRoutes: Array<{
    id: string
    sourceKind: 'campaign' | 'expedition'
    sourceId: string
    battleMode: 'td' | 'clash'
    tdVariantId: string | null
    clashVariantId: string | null
    deferredReason?: string
  }>
  roster: ClashUnitDefinition[]
  deferredSubfeatures: Array<{ id: string; reason: string }>
}

export interface ClashRulesIdentity {
  configSchemaVersion: number
  rulesDigest: string
}

export interface ClashPlanUnit {
  instanceId: string
  definitionId: string
  side: ClashSide
  campaignUnitInstanceId?: string
  cityId?: string
  cohortId?: string
  unitId?: string
}

export interface ClashPlan {
  id: string
  sessionId: string
  rulesIdentity: ClashRulesIdentity
  field: ClashFieldVariantDefinition
  region: ClashRegionModifierDefinition
  units: ClashUnitDefinition[]
  roster: ClashPlanUnit[]
  initialMorale: Record<ClashSide, number>
  maxTurns: number
  maxCommands: number
  resultLogLimit: number
  placementFirstSide: ClashSide
  betweenClashesFirstSide: ClashSide
  speedTieRule: 'attacker-first' | 'defender-first'
  turnCapTieWinner: ClashSide
  victoryRule: 'elimination'
  corpseBlocksAdvance: boolean
  onePlacementPerSideBetweenClashes: boolean
  statuses: ClashStatusDefinition[]
  terrain: ClashTerrainDefinition[]
  morale: ClashMoraleConfig
}

export type ClashCommand =
  | {
    turn: number
    kind: 'place'
    side: ClashSide
    unitInstanceId: string
    row: number
    column: number
  }
  | {
    turn: number
    kind: 'activate'
    side: ClashSide
    unitInstanceId: string
    abilityId: string
    targetUnitInstanceId?: string
    targetSide?: ClashSide
    targetRow?: number
    targetColumn?: number
  }
  | {
    turn: number
    kind: 'end-between-clash'
    side: ClashSide
  }
  | {
    turn: number
    kind: 'resolve-clash'
  }

export interface ClashActiveStatus {
  id: string
  statusId: string
  sourceUnitInstanceId: string | null
  stacks: number
  remainingTurns: number | null
  appliedHp: number
  appliedClash: number
}

export interface ClashUnitState {
  instanceId: string
  definitionId: string
  side: ClashSide
  row: number | null
  column: number | null
  hp: number
  maxHp: number
  attackDelta: number
  speedDelta: number
  shieldCharges: number
  dodgeCharges: number
  statuses: ClashActiveStatus[]
  passiveCharges: Record<string, number>
  abilityCharges: Record<string, number>
  abilityReadyClash: Record<string, number>
  rangedReadyClash: number
  hiddenPassiveIdsRevealed: string[]
  killCount: number
  alive: boolean
  deployed: boolean
}

export interface ClashCorpseState {
  id: string
  unitInstanceId: string
  definitionId: string
  side: ClashSide
  row: number
  column: number
  createdTurn: number
  burned: boolean
  decomposed: boolean
}

export interface ClashCellState {
  side: ClashSide
  row: number
  column: number
  unitInstanceId: string | null
  corpseIds: string[]
  terrainId: string | null
}

export interface ClashLogEntry {
  sequence: number
  turn: number
  kind: 'placement' | 'activation' | 'attack' | 'status' | 'advance' | 'morale' | 'system'
  message: string
  unitInstanceIds: string[]
}

export interface ClashSimulationState {
  planId: string
  seed: string | number
  rng: { state: number; draws: number }
  turn: number
  clashNumber: number
  phase: ClashPhase
  expectedSide: ClashSide | null
  units: Record<string, ClashUnitState>
  cells: ClashCellState[]
  corpses: ClashCorpseState[]
  morale: Record<ClashSide, number>
  betweenClashes: Record<ClashSide, {
    activationCount: number
    placementUsed: boolean
    ended: boolean
  }>
  commandLog: ClashCommand[]
  log: ClashLogEntry[]
  outcome: ClashOutcome | null
  winner: ClashSide | null
  terminalReason: 'elimination' | 'turn-cap' | 'aborted' | null
  error: string | null
}

export interface ClashDeploymentResult {
  unitInstanceId: string
  side: ClashSide
  campaignUnitInstanceId: string | null
  cityId: string | null
  cohortId: string | null
  unitId: string | null
  deployed: number
  survived: number
  healthRatio: number
}

export interface ClashResult {
  kind: 'clash'
  sessionId: string
  planId: string
  planDigest: string
  rulesIdentity: ClashRulesIdentity
  seed: string | number
  outcome: ClashOutcome
  winner: ClashSide | null
  terminalReason: 'elimination' | 'turn-cap' | 'aborted'
  turns: number
  clashes: number
  turnLog: ClashCommand[]
  commandDigest: string
  deployments: ClashDeploymentResult[]
  finalMorale: Record<ClashSide, number>
  revealedPassiveIds: string[]
  log: ClashLogEntry[]
  error: string | null
}
