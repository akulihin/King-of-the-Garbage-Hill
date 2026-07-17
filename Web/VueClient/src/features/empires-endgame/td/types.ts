import type {
  CombatArmorProfile,
  CombatWeaponProfile,
  EmpiresCombatConfig,
} from '../combat/types'

export type TdBattleMode = 'defense'
export type TdTerminalReason =
  | 'all-waves-defeated'
  | 'castle-destroyed'
  | 'tick-cap'
  | 'invalid-command'
  | 'aborted'
export type TdBattleOutcome = 'victory' | 'defeat' | 'error' | 'aborted'
export type TdTargetPriority = 'first' | 'strongest'

export interface TdPoint {
  x: number
  y: number
}

export interface TdLaneNodeDefinition extends TdPoint {
  id: string
}

export interface TdLaneEdgeDefinition {
  id: string
  fromNodeId: string
  toNodeId: string
}

export interface TdLaneGraphDefinition {
  nodes: TdLaneNodeDefinition[]
  edges: TdLaneEdgeDefinition[]
}

export interface TdBuildSpotDefinition extends TdPoint {
  id: string
}

export interface TdBattlefieldDefinition {
  id: string
  name: string
  mode: TdBattleMode
  width: number
  height: number
  laneGraph: TdLaneGraphDefinition
  buildSpots: TdBuildSpotDefinition[]
  spawnerNodeId: string
  castleNodeId: string
  deploymentNodeId: string
  castleMaxHp: number
  castleArmor: CombatArmorProfile | null
}

export interface TdTowerBaseDefinition {
  id: string
  name: string
  maxHp: number
  range: number
  attackIntervalTicks: number
  projectiles: number
  weapon: CombatWeaponProfile
  targetPriority: TdTargetPriority
}

export interface TdTowerChoiceDefinition {
  id: string
  name: string
  grade: 1 | 2 | 3 | 4
  cost: number
  maxHpBonus: number
  rangeBonus: number
  attackIntervalTicksDelta: number
  projectileBonus: number
  damageLevelBonuses: Record<string, number>
  targetPriority?: TdTargetPriority
}

export interface TdEnemyGroupDefinition {
  id: string
  count: number
  startTick: number
  spawnIntervalTicks: number
  routeEdgeIds: string[]
  maxHp: number
  speedPerSecond: number
  attackRange: number
  attackIntervalTicks: number
  weapon: CombatWeaponProfile
  armor: CombatArmorProfile | null
}

export interface TdWaveDefinition {
  id: string
  name: string
  groups: TdEnemyGroupDefinition[]
}

export interface TdAllianceCurveDefinition {
  baseThreat: number
  threatPerWave: number
  healthPerThreat: number
  countPerThreat: number
  speedPerThreat: number
}

export interface TdBattleConsequenceDefinition {
  moraleDelta: number
  allianceThreatDelta: number
  recruitmentPenaltyPerDeployedUnit: number
}

export interface TdSettlementDefinition {
  lossLoyaltyThreshold: number
  loyaltyDelta: number
  veteranHealthThreshold: number
  recruitmentPenaltyPerLoss: number
  growthPenaltyPerLoss: number
  victory: TdBattleConsequenceDefinition
  defeat: TdBattleConsequenceDefinition
  abort: TdBattleConsequenceDefinition
}

export interface TdMoraleDefinition {
  initial: number
  minimum: number
  maximum: number
}

export interface TdEquipmentProductionDefinition {
  equipmentId: string
  amountPerSmithCapacity: number
}

/**
 * Config schema v2 gained these fields additively. They stay optional in the
 * TypeScript shape so an old disabled v2 custom config can be migrated before
 * validation; enabled definitions are validated as complete.
 */
export interface EmpiresTdConfig {
  enabled: boolean
  tickMs?: number
  maxTicks?: number
  waveEveryCons?: number
  startingBuildResources?: number
  towerBase?: TdTowerBaseDefinition
  alliance?: TdAllianceCurveDefinition
  settlement?: TdSettlementDefinition
  morale?: TdMoraleDefinition
  equipmentProduction?: TdEquipmentProductionDefinition[]
  battlefields: TdBattlefieldDefinition[]
  towers: TdTowerChoiceDefinition[]
  waves: TdWaveDefinition[]
}

export interface TdUnitProfile {
  maxHp: number
  attackRange: number
  attackIntervalTicks: number
  weaponEquipmentId: string
  armorEquipmentId?: string
}

export interface TdEquipmentCost {
  equipmentId: string
  amount: number
}

export interface TdDeploymentPlan {
  id: string
  cityId: string
  unitId: string
  count: number
  nodeId: string
  maxHpPerUnit: number
  attackRange: number
  attackIntervalTicks: number
  weapon: CombatWeaponProfile
  armor: CombatArmorProfile | null
}

export interface TdBattlePlan {
  id: string
  mode: TdBattleMode
  scheduledCon: number
  threat: number
  tickMs: number
  maxTicks: number
  startingBuildResources: number
  battlefield: TdBattlefieldDefinition
  towerBase: TdTowerBaseDefinition
  towerChoices: TdTowerChoiceDefinition[]
  wave: TdWaveDefinition
  combat: EmpiresCombatConfig
  deployments: TdDeploymentPlan[]
}

export type TdCommand =
  | {
    tick: number
    kind: 'build-tower'
    spotId: string
    choiceId: string
  }
  | {
    tick: number
    kind: 'upgrade-tower'
    spotId: string
    choiceId: string
  }

export interface TdTowerState {
  spotId: string
  choiceIds: string[]
  hp: number
  nextAttackTick: number
}

export interface TdEnemyState extends TdPoint {
  id: string
  groupId: string
  routeEdgeIds: string[]
  edgeIndex: number
  edgeProgress: number
  hp: number
  maxHp: number
  nextAttackTick: number
}

export interface TdSquadState extends TdPoint {
  deploymentId: string
  cityId: string
  unitId: string
  count: number
  hp: number
  maxHp: number
  nextAttackTick: number
}

export interface TdCommandError {
  tick: number
  command: TdCommand
  message: string
}

export interface TdSimulationState {
  tick: number
  elapsedMs: number
  rng: { state: number, draws: number }
  buildResources: number
  castleHp: number
  towers: TdTowerState[]
  enemies: TdEnemyState[]
  squads: TdSquadState[]
  spawnedByGroup: Record<string, number>
  nextEnemyId: number
  damageByType: Record<string, number>
  hitCount: number
  terminalReason: TdTerminalReason | null
  commandErrors: TdCommandError[]
}

export interface TdDeploymentResult {
  deploymentId: string
  cityId: string
  unitId: string
  deployed: number
  survived: number
  healthRatio: number
}

export interface TdBattleResult {
  kind: 'td'
  planId: string
  planDigest: string
  seed: string | number
  outcome: TdBattleOutcome
  terminalReason: TdTerminalReason
  ticks: number
  castleHp: number
  castleMaxHp: number
  enemiesSpawned: number
  enemiesDefeated: number
  deployments: TdDeploymentResult[]
  buildResourcesRemaining: number
  damageByType: Record<string, number>
  hitCount: number
  commandLog: TdCommand[]
  commandDigest: string
  error?: string
}
