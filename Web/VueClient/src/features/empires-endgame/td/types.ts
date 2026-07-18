import type {
  CombatArmorProfile,
  CombatWeaponProfile,
  EmpiresCombatConfig,
} from '../combat/types'

export type TdBattleMode = 'defense' | 'assault'
export type TdTerminalReason =
  | 'all-waves-defeated'
  | 'objective-destroyed'
  | 'all-deployments-defeated'
  | 'tick-cap'
  | 'invalid-command'
  | 'aborted'
export type TdBattleOutcome = 'victory' | 'defeat' | 'error' | 'aborted'
export type TdTargetPriority = 'first' | 'strongest'
export type TdObjectiveKind = 'castle' | 'fort'

export interface TdRulesIdentity {
  configSchemaVersion: number
  rulesDigest: string
}

export interface TdFrameClock {
  accumulatorMs: number
  ticks: number
}

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
  terrainId: string
}

export interface TdTowerTargetingModifierDefinition {
  id: string
  kind: 'tower-targeting'
  terrainIds: string[]
  targetableByEnemyCategoryIds: string[]
}

export interface TdTowerStatModifierDefinition {
  id: string
  kind: 'tower-stat'
  terrainIds: string[]
  rangeMultiplier: number
  maxHpMultiplier: number
}

export interface TdDeploymentAttritionModifierDefinition {
  id: string
  kind: 'deployment-attrition'
  modes: TdBattleMode[]
  intervalTicks: number
  damagePerUnit: number
}

export type TdBattlefieldModifierDefinition =
  | TdTowerTargetingModifierDefinition
  | TdTowerStatModifierDefinition
  | TdDeploymentAttritionModifierDefinition

export interface TdBattlefieldDefinition {
  id: string
  name: string
  regionId: string
  width: number
  height: number
  laneGraph: TdLaneGraphDefinition
  buildSpots: TdBuildSpotDefinition[]
  spawnerNodeId: string
  deploymentNodeId: string
  objectiveNodeId: string
  towerBaseIds: string[]
  allowedTowerCategoryIds: string[]
  modifiers: TdBattlefieldModifierDefinition[]
}

export interface TdObjectiveDefinition {
  id: string
  name: string
  kind: TdObjectiveKind
  owner: 'player' | 'enemy'
  nodeId: string
  maxHp: number
  armor: CombatArmorProfile | null
}

export interface TdTowerBaseDefinition {
  id: string
  name: string
  regionId: string
  categoryIds: string[]
  cost: number
  maxHp: number
  range: number
  attackIntervalTicks: number
  projectiles: number
  weapon: CombatWeaponProfile
  loadouts?: TdTowerLoadoutDefinition[]
  targetPriority: TdTargetPriority
}

export interface TdTowerLoadoutDefinition {
  id: string
  priority: number
  weaponEquipmentId: string
  defenseEquipmentId?: string
  equipmentCosts: TdEquipmentCost[]
}

export interface TdTowerChoiceDefinition {
  id: string
  name: string
  grade: 1 | 2 | 3 | 4
  categoryIds: string[]
  cost: number
  maxHpBonus: number
  rangeBonus: number
  attackIntervalTicksDelta: number
  projectileBonus: number
  damageLevelBonuses: Record<string, number>
  targetPriority?: TdTargetPriority
}

export interface TdGradeChoiceSetDefinition {
  id: string
  regionId: string
  grade: 1 | 2 | 3 | 4
  choiceIds: string[]
  deferredReason?: string
}

export interface TdEnemyGroupDefinition {
  id: string
  categoryIds: string[]
  count: number
  startTick: number
  spawnIntervalTicks: number
  routeEdgeIds: string[]
  stationNodeId?: string
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

export interface TdPlanVariantDefinition {
  id: string
  name: string
  mode: TdBattleMode
  purpose?: 'campaign' | 'expedition'
  battlefieldId: string
  waveId: string
  objective: TdObjectiveDefinition
  deploymentSpeedPerSecond: number
  startingBuildResources?: number
  deferredReason?: string
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
  id: string
  equipmentId: string
  lineId: string
  amountPerSmithCapacity: number
  priority: number
  technologyId?: string
}

export interface TdEquipmentProductionLineDefinition {
  id: string
  capacityFlagId: string
  capacityShare: number
}

/**
 * Config fields remain optional in the TypeScript shape so old custom configs can
 * be migrated before the production validator requires the current schema.
 */
export interface EmpiresTdConfig {
  enabled: boolean
  regionalCatalogEnabled?: boolean
  tickMs?: number
  maxTicks?: number
  maxCommands?: number
  resultLogLimit?: number
  maxCatchUpTicksPerFrame?: number
  waveEveryCons?: number
  startingBuildResources?: number
  towerBases?: TdTowerBaseDefinition[]
  alliance?: TdAllianceCurveDefinition
  settlement?: TdSettlementDefinition
  morale?: TdMoraleDefinition
  equipmentProductionLines?: TdEquipmentProductionLineDefinition[]
  equipmentProduction?: TdEquipmentProductionDefinition[]
  battlefields: TdBattlefieldDefinition[]
  towers: TdTowerChoiceDefinition[]
  gradeChoices?: TdGradeChoiceSetDefinition[]
  waves: TdWaveDefinition[]
  planVariants?: TdPlanVariantDefinition[]
}

export interface TdUnitProfile {
  maxHp: number
  attackRange: number
  attackIntervalTicks: number
  weaponEquipmentId: string
  armorEquipmentId?: string
  healing?: {
    range: number
    intervalTicks: number
    amountPerUnit: number
    chargesPerUnit: number
  }
}

export interface TdEquipmentCost {
  equipmentId: string
  amount: number
}

export interface TdDeploymentPlan {
  id: string
  cohortId: string
  cityId: string
  unitId: string
  unitInstanceIds: string[]
  count: number
  nodeId: string
  speedPerSecond: number
  maxHpPerUnit: number
  attackRange: number
  attackIntervalTicks: number
  weapon: CombatWeaponProfile
  armor: CombatArmorProfile | null
  healing?: {
    range: number
    intervalTicks: number
    amountPerUnit: number
    chargesPerUnit: number
  }
}

export interface TdBattlePlan {
  id: string
  sessionId: string
  rulesIdentity: TdRulesIdentity
  mode: TdBattleMode
  scheduledCon: number
  threat: number
  tickMs: number
  maxTicks: number
  maxCommands: number
  maxCatchUpTicksPerFrame: number
  startingBuildResources: number
  battlefield: TdBattlefieldDefinition
  objective: TdObjectiveDefinition
  towerBases: TdTowerBaseDefinition[]
  towerChoices: TdTowerChoiceDefinition[]
  gradeChoices: TdGradeChoiceSetDefinition[]
  wave: TdWaveDefinition
  combat: EmpiresCombatConfig
  equipmentStock: Record<string, number>
  deployments: TdDeploymentPlan[]
}

interface TdCommandIdentity {
  tick: number
  sequence: number
  sessionId: string
  planId: string
}

export type TdCommand =
  | TdCommandIdentity & {
    kind: 'build-tower'
    spotId: string
    towerBaseId: string
  }
  | TdCommandIdentity & {
    kind: 'upgrade-tower'
    spotId: string
    choiceId: string
  }

export interface TdTowerState {
  spotId: string
  towerBaseId: string
  choiceIds: string[]
  loadoutId: string | null
  weaponEquipmentId: string | null
  defenseEquipmentId?: string
  weapon: CombatWeaponProfile
  armor: CombatArmorProfile | null
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
  cohortId: string
  cityId: string
  unitId: string
  count: number
  routeEdgeIds: string[]
  edgeIndex: number
  edgeProgress: number
  hp: number
  maxHp: number
  nextAttackTick: number
  nextHealTick: number
  healingChargesRemaining: number
}

export interface TdCommandError {
  tick: number
  command: TdCommand | null
  message: string
}

export interface TdSimulationState {
  tick: number
  elapsedMs: number
  rng: { state: number, draws: number }
  buildResources: number
  equipmentStock: Record<string, number>
  equipmentSpent: Record<string, number>
  objectiveHp: number
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
  cohortId: string
  cityId: string
  unitId: string
  deployed: number
  survived: number
  healthRatio: number
}

export interface TdBattleResult {
  kind: 'td'
  sessionId: string
  planId: string
  planDigest: string
  rulesIdentity: TdRulesIdentity
  seed: string | number
  outcome: TdBattleOutcome
  terminalReason: TdTerminalReason
  ticks: number
  objectiveHp: number
  objectiveMaxHp: number
  enemiesSpawned: number
  enemiesDefeated: number
  deployments: TdDeploymentResult[]
  buildResourcesRemaining: number
  equipmentSpent: Record<string, number>
  damageByType: Record<string, number>
  hitCount: number
  commandLog: TdCommand[]
  commandDigest: string
  error?: string
}
