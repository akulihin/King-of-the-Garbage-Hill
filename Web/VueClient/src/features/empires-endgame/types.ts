import type { EmpiresCombatConfig } from './combat/types'
import type {
  EmpiresTdConfig,
  TdBattlePlan,
  TdBattleResult,
  TdEquipmentCost,
  TdRulesIdentity,
  TdUnitProfile,
} from './td/types'

export type {
  CombatArmorClassDefinition,
  CombatArmorProfile,
  CombatCounterRule,
  CombatDamageTypeDefinition,
  CombatDamageTypeId,
  CombatEquipmentDefinition,
  CombatWeaponProfile,
  EmpiresCombatConfig,
} from './combat/types'
export type {
  EmpiresTdConfig,
  TdAllianceCurveDefinition,
  TdBattleConsequenceDefinition,
  TdBattlefieldModifierDefinition,
  TdBattleMode,
  TdBattleOutcome,
  TdBattlePlan,
  TdBattleResult,
  TdBattlefieldDefinition,
  TdBuildSpotDefinition,
  TdCommand,
  TdDeploymentPlan,
  TdDeploymentResult,
  TdEnemyGroupDefinition,
  TdEquipmentCost,
  TdEquipmentProductionDefinition,
  TdEquipmentProductionLineDefinition,
  TdFrameClock,
  TdGradeChoiceSetDefinition,
  TdLaneEdgeDefinition,
  TdLaneGraphDefinition,
  TdLaneNodeDefinition,
  TdMoraleDefinition,
  TdObjectiveDefinition,
  TdObjectiveKind,
  TdPlanVariantDefinition,
  TdPoint,
  TdRulesIdentity,
  TdSettlementDefinition,
  TdSimulationState,
  TdTargetPriority,
  TdTerminalReason,
  TdTowerBaseDefinition,
  TdTowerChoiceDefinition,
  TdTowerLoadoutDefinition,
  TdUnitProfile,
  TdWaveDefinition,
} from './td/types'

export const EMPIRES_SUITS = ['clubs', 'diamonds', 'hearts', 'spades'] as const
export const EMPIRES_RANKS = [
  '2',
  '3',
  '4',
  '5',
  '6',
  '7',
  '8',
  '9',
  '10',
  'jack',
  'queen',
  'king',
  'ace',
] as const
export const EMPIRES_ACTORS = ['player', 'god'] as const
export const EMPIRES_PHASES = [
  'cards',
  'divineGift',
  'empire',
  'event',
  'minigame',
  'victory',
  'defeat',
] as const
export const EMPIRES_FACILITY_LOCKS = ['mine', 'lumber'] as const

export type EmpiresSuit = typeof EMPIRES_SUITS[number]
export type EmpiresRank = typeof EMPIRES_RANKS[number]
export type EmpiresCardRank = EmpiresRank | 'joker'
export type EmpiresActor = typeof EMPIRES_ACTORS[number]
export type EmpiresPhase = typeof EMPIRES_PHASES[number]
export type EmpiresFacilityLock = typeof EMPIRES_FACILITY_LOCKS[number]
export type EmpiresActionResult = { ok: true, message: string } | { ok: false, message: string }
export type EmpiresBuildingSlotKind =
  | 'farm'
  | 'lumber'
  | 'mine'
  | 'smithy'
  | 'barracks'
  | 'unique'
  | 'municipal'

export interface EmpiresResourceAmount {
  resourceId: string
  amount: number
}

export interface EmpiresDeferredSubfeature {
  id: string
  reason: string
}

export type EmpiresEffect =
  | {
    kind: 'resource'
    resourceId: string
    amount: number
    amountPerLevel?: number
  }
  | {
    kind: 'resourceMultiplier'
    resourceId: string
    multiplier: number
    multiplierPerLevel?: number
  }
  | {
    kind: 'time'
    days: number
    daysPerLevel?: number
  }
  | {
    kind: 'foodProduction'
    cityId?: string
    amount: number
    amountPerLevel?: number
  }
  | {
    kind: 'population'
    cityId?: string
    amount: number
    amountPerLevel?: number
  }
  | {
    kind: 'loyalty'
    target: EmpiresLoyaltyTarget
    amount: number
    amountPerLevel?: number
  }
  | {
    kind: 'loyaltyAllCities'
    amount: number
    amountPerLevel?: number
  }
  | {
    kind: 'classLoyalty'
    populationClassId: string
    amount: number
    amountPerLevel?: number
  }
  | {
    kind: 'reputation'
    amount: number
    amountPerLevel?: number
  }
  | {
    kind: 'flag'
    flagId: string
    amount: number
    amountPerLevel?: number
  }

export interface EmpiresCardFace {
  title: string
  description: string
  image?: string
  effects: EmpiresEffect[]
  deferredReason?: string
}

export interface EmpiresCardDefinition {
  id: string
  suit: EmpiresSuit | 'joker'
  rank: EmpiresCardRank
  name: string
  value: number
  timeCostDays: number
  drawUpgrade: number
  maxLevel?: number
  normal: EmpiresCardFace
  inverted: EmpiresCardFace
}

export interface EmpiresJokerConfig {
  /** Highest Joker beats every non-Joker and cannot itself be beaten. */
  rule: 'highest-unbeatable' | 'trump' | 'ordinary'
  canAttack: boolean
  canThrowIn: boolean
  canDefend: boolean
  ordinaryStrength?: number
  trumpFallbackSuit: EmpiresSuit
}

export const DEFAULT_EMPIRES_JOKER_CONFIG: Readonly<EmpiresJokerConfig> = {
  rule: 'highest-unbeatable',
  canAttack: true,
  canThrowIn: false,
  canDefend: true,
  trumpFallbackSuit: 'spades',
}

export type EmpiresPerformanceMetric =
  | 'successfulDefenses'
  | 'godTakes'
  | 'maxCardsGivenToGodAtOnce'
  | 'cardsGivenToGod'
  | 'cardsTakenByPlayer'
  | 'boutsWon'
  | 'boutsLost'

export interface EmpiresScoringRule {
  id: string
  metric: EmpiresPerformanceMetric
  comparison: 'gte' | 'lte' | 'eq'
  threshold: number
  points: number
}

export interface EmpiresDurakConfig {
  handSize: number
  maxAttackCards: number
  boutsPerCon: number
  initialAttacker: EmpiresActor | 'lowest-trump'
  fixedTrumpSuit?: EmpiresSuit
  joker: EmpiresJokerConfig
  scoringRules: EmpiresScoringRule[]
  simultaneousEmptyWinner: EmpiresActor | 'attacker' | 'defender'
}

export interface EmpiresGiftDefinition {
  id: string
  name: string
  description: string
  kind: 'boon' | 'relic' | 'catastrophe' | 'empire'
  rarity?: 'common' | 'uncommon' | 'rare' | 'legendary'
  image?: string
  application: 'once' | 'eachEmpire'
  baseWeight: number
  performanceWeight: number
  minimumPerformance?: number
  maximumPerformance?: number
  effects: EmpiresEffect[]
  resolution?: EmpiresGiftResolution
  deferredReason?: string
}

export type EmpiresGiftResolution =
  | {
    kind: 'cityResources'
  }
  | {
    kind: 'meteorCity'
    damageLevels: number
  }
  | {
    kind: 'destroyRegion'
    regionId: string
  }
  | {
    kind: 'buildingLevelBonus'
    slots: EmpiresBuildingSlotKind[]
    amount: number
  }

export type EmpiresPendingGiftResolution =
  | {
    kind: 'cityResources'
    giftId: string
    eligibleTargetIds: string[]
  }
  | {
    kind: 'meteorCity'
    giftId: string
    damageLevels: number
    eligibleTargetIds: string[]
  }

export interface EmpiresGiftConfig {
  choiceCount: number
  definitions: EmpiresGiftDefinition[]
}

export type EmpiresDependency =
  | {
    kind: 'building'
    buildingId: string
    level: number
    scope?: 'sameCity' | 'anyCity'
  }
  | {
    kind: 'technology'
    technologyId: string
  }
  | {
    kind: 'flag'
    flagId: string
    minimum: number
  }
  | {
    kind: 'reputation'
    minimum: number
  }
  | {
    kind: 'advisor'
    advisorId: string
  }

export interface EmpiresBuildingLevelDefinition {
  level: number
  name?: string
  description?: string
  image?: string
  timeCostDays: number
  foodCost: number
  resourceCosts: EmpiresResourceAmount[]
  dependencies: EmpiresDependency[]
  facilityLocks: EmpiresFacilityLock[]
  workerDemand?: number
  production?: EmpiresResourceAmount[]
  effects?: EmpiresEffect[]
}

export interface EmpiresBuildingDefinition {
  id: string
  name: string
  image?: string
  slot: EmpiresBuildingSlotKind
  allowedCityIds?: string[]
  levels: EmpiresBuildingLevelDefinition[]
  deferredSubfeatures?: EmpiresDeferredSubfeature[]
  deferredReason?: string
}

export interface EmpiresUnitLoadoutDefinition {
  id: string
  priority: number
  weaponEquipmentId: string
  defenseEquipmentId?: string
  equipmentCosts: TdEquipmentCost[]
}

export interface EmpiresUnitDefinition {
  id: string
  name: string
  description?: string
  image?: string
  foodUpkeep: number
  populationCost: number
  timeCostDays: number
  resourceCosts: EmpiresResourceAmount[]
  dependencies: EmpiresDependency[]
  equipmentCosts?: TdEquipmentCost[]
  loadouts?: EmpiresUnitLoadoutDefinition[]
  td?: TdUnitProfile
  deferredReason?: string
}

export type EmpiresSteelGenerationStage = 'whole' | 'minus' | 'plus'

export interface EmpiresSteelTechnologyDefinition {
  branchId: string
  generation: number
  stage: EmpiresSteelGenerationStage
  payoff: 'equipment' | 'unlock-only' | 'deferred'
  equipmentIds?: string[]
  eliteRequired?: boolean
  accessTechnologyId?: string
  entryFromTechnologyIds?: string[]
}

export type EmpiresTechnologySideAlignment = 'light' | 'dark'

export interface EmpiresTechnologySideEpidemicPolicy {
  preventsIntercitySpread: boolean
  withinCitySpeedMultiplier: number
}

export interface EmpiresTechnologySideDefinition {
  id: string
  name: string
  alignment: EmpiresTechnologySideAlignment
  effects: EmpiresEffect[]
  tags?: string[]
  culturalSuppressible?: boolean
  reputationDelta?: number
  epidemicPolicy?: EmpiresTechnologySideEpidemicPolicy
}

export type EmpiresTechnologySideSelection =
  | { kind: 'fixed', sideId: string }
  | { kind: 'weighted', weights: Array<{ sideId: string, weight: number }> }

export type EmpiresTechnologySideDisclosure =
  | { kind: 'onResearch' }
  | { kind: 'afterCons', delayCons: number }
  | { kind: 'hiddenCombination', combinationId: string }

export interface EmpiresTechnologySidesDefinition {
  selection: EmpiresTechnologySideSelection
  disclosure: EmpiresTechnologySideDisclosure
  definitions: EmpiresTechnologySideDefinition[]
}

export interface EmpiresTechnologyDefinition {
  id: string
  name: string
  description?: string
  category: 'technology' | 'reform' | 'doctrine' | 'steel'
  groupId?: string
  tier?: number
  position?: EmpiresPoint
  image?: string
  tags?: string[]
  timeCostDays: number
  resourceCosts: EmpiresResourceAmount[]
  prerequisites: EmpiresDependency[]
  effects: EmpiresEffect[]
  steel?: EmpiresSteelTechnologyDefinition
  sides?: EmpiresTechnologySidesDefinition
  deferredSubfeatures?: EmpiresDeferredSubfeature[]
  deferredReason?: string
}

export interface EmpiresSteelResearchConfig {
  forkSourcePriceMultiplier: number
  delayedFreeEmpirePhases: number
  militaryEliteFlagId: string
}

export interface EmpiresEventChoiceDefinition {
  id: string
  label: string
  description?: string
  resourceCosts?: EmpiresResourceAmount[]
  effects: EmpiresEffect[]
  deferredReason?: string
}

export interface EmpiresEventDefinition {
  id: string
  name: string
  description: string
  weight: number
  minimumCon?: number
  maximumCon?: number
  prerequisites?: EmpiresDependency[]
  choices: EmpiresEventChoiceDefinition[]
  deferredReason?: string
}

export interface EmpiresResourceDefinition {
  id: string
  name: string
  deferredReason?: string
}

export interface EmpiresInitialCity {
  id: string
  name: string
  regionId: string
  position: EmpiresPoint
  population: number
  militaryPopulation: number
  populationClasses: Record<string, number>
  baseProduction: Record<string, number>
  buildingLevels: Record<string, number>
  slots: EmpiresCitySlot[]
}

export interface EmpiresPoint {
  x: number
  y: number
}

export interface EmpiresCitySlot {
  id: string
  kind: EmpiresBuildingSlotKind
  buildingId?: string
  position: EmpiresPoint
}

export interface EmpiresPopulationClassDefinition {
  id: string
  name: string
  description?: string
  canWork: boolean
  canRecruit: boolean
  foodPerPerson: number
  workerPriority: number
}

export interface EmpiresSubregionDefinition {
  id: string
  regionId: string
  name: string
  biome: string
  polygon: EmpiresPoint[]
}

export interface EmpiresRegionDefinition {
  id: string
  name: string
  biome: 'ice' | 'forest' | 'desert' | 'swamp' | 'central' | string
  center: EmpiresPoint
  polygon: EmpiresPoint[]
  subregionIds: string[]
  cityIds: string[]
}

export interface EmpiresMapObjectDefinition {
  id: string
  name: string
  kind: 'river' | 'mountain' | 'fortress' | 'city' | 'landmark' | 'resource' | 'custom'
  regionId: string
  subregionId?: string
  position: EmpiresPoint
  size?: EmpiresPoint
  rotation?: number
  draggable: boolean
  image?: string
  properties?: Record<string, string | number | boolean>
}

export interface EmpiresMapConfig {
  width: number
  height: number
  viewportWorldFraction: number
  projection: 'fixed-oblique' | 'top-down'
  regions: EmpiresRegionDefinition[]
  subregions: EmpiresSubregionDefinition[]
  objects: EmpiresMapObjectDefinition[]
}

export interface EmpiresGodScaffoldConfig {
  enabled: boolean
  lines: never[]
  deckMemoryRules: never[]
  antiBitoRules: never[]
}

export interface EmpiresQuestsScaffoldConfig {
  enabled: boolean
  definitions: never[]
  dialogueGraphs: never[]
}

export interface EmpiresSeasonDefinition {
  id: string
  name: string
  description?: string
  durationCons: number
  foodProductionMultiplier: number
}

export interface EmpiresGreenhouseSeasonConfig {
  technologyId: string
  equalizedFoodProductionMultiplier: number
}

export interface EmpiresSeasonsConfig {
  enabled: boolean
  definitions: EmpiresSeasonDefinition[]
  foodRounding: 'floor' | 'round' | 'none'
  greenhouse: EmpiresGreenhouseSeasonConfig | null
}

export interface EmpiresHiddenCombinationDefinition {
  id: string
  name: string
  prerequisites: EmpiresDependency[]
  tags?: string[]
  deferredReason?: string
}

export interface EmpiresHiddenCombinationsConfig {
  enabled: boolean
  definitions: EmpiresHiddenCombinationDefinition[]
}

export type EmpiresLoyaltyTarget =
  | { kind: 'city', cityId: string }
  | { kind: 'region', regionId: string }
  | { kind: 'class', cityId: string, populationClassId: string }

export interface EmpiresLoyaltyWorkforceDivisor {
  loyalty: number
  divisor: number
}

export interface EmpiresLoyaltyClassGateDefinition {
  id: string
  buildingId: string
  populationClassId: string
  minimumLoyalty: number
}

export interface EmpiresLoyaltyRebellionConfig {
  threshold: number
  sustainedApplications: number
  recoveryThreshold: number
  sustainedRecoveryApplications: number
}

export interface EmpiresLoyaltyConfig {
  enabled: boolean
  minimum: number
  maximum: number
  initialCityLoyalty: number
  initialClassLoyalty: number
  initialRegionLoyalty: Record<string, number>
  initialReputation: number
  workforceDivisors: EmpiresLoyaltyWorkforceDivisor[]
  constructionMinimumLoyalty: number
  recruitmentMinimumLoyalty: number
  rebellion: EmpiresLoyaltyRebellionConfig
  classGates: EmpiresLoyaltyClassGateDefinition[]
  chronicleRetention: number
}

export type EmpiresAdvisorStatus = 'locked' | 'awaiting-judgment' | 'active' | 'executed'
export type EmpiresAdvisorTransitionAction = 'pardon' | 'execute' | 'grant-access'

export interface EmpiresAdvisorDefinition {
  id: string
  name: string
  suit: EmpiresSuit
  category: 'science' | 'trade' | 'war' | 'people'
  initialStatus: EmpiresAdvisorStatus
  decisionId?: string
  technologyIds: string[]
  grandAdvisor?: boolean
  accessDeferredReason?: string
}

export interface EmpiresAdvisorDecisionDefinition {
  id: string
  advisorIds: string[]
  pardonsRequired: number
  executionsRequired: number
}

export interface EmpiresGovernanceTrumpConfig {
  restrictedSuit: EmpiresSuit
  grandAdvisorId: string
  lockedFallbackSuit: EmpiresSuit
  criticalEffectMultiplier: number
}

export interface EmpiresPerstDefinition {
  id: string
  name: string
  title: string
  description?: string
}

export interface EmpiresGovernanceCitySiteDefinition {
  cityId: string
  regionId: string
  access: 'initial' | 'governor'
  defenseLayer: 1 | 2 | 3
  order: number
  coastal: boolean
}

export interface EmpiresGovernorConfig {
  assignmentMode: 'permanent'
  regionIds: string[]
  citySites: EmpiresGovernanceCitySiteDefinition[]
}

export interface EmpiresCapitalSiteDefinition {
  id: string
  name: string
  owner: 'P3B' | 'P4A' | 'P4C/P12B' | 'P6C'
  buildingId?: string
  mapObjectId?: string
  deferredReason: string
}

export interface EmpiresCapitalGovernanceConfig {
  cityId: string
  sites: EmpiresCapitalSiteDefinition[]
}

export interface EmpiresGovernanceConfig {
  enabled: boolean
  advisors: EmpiresAdvisorDefinition[]
  advisorDecisions: EmpiresAdvisorDecisionDefinition[]
  trump: EmpiresGovernanceTrumpConfig
  persts: EmpiresPerstDefinition[]
  governor: EmpiresGovernorConfig
  capital: EmpiresCapitalGovernanceConfig
}

export interface EmpiresEmpireConfig {
  daysPerPhase: number
  foodResourceId: string
  eventChance: number
  defeatPopulationAtOrBelow: number
  lockProviderBuildingIds: Record<EmpiresFacilityLock, string>
  resources: EmpiresResourceDefinition[]
  initialResources: Record<string, number>
  initialFlags?: Record<string, number>
  map: EmpiresMapConfig
  populationClasses: EmpiresPopulationClassDefinition[]
  cities: EmpiresInitialCity[]
  buildings: EmpiresBuildingDefinition[]
  units?: EmpiresUnitDefinition[]
  technologies: EmpiresTechnologyDefinition[]
  steelResearch: EmpiresSteelResearchConfig
  events: EmpiresEventDefinition[]
  seasons: EmpiresSeasonsConfig
  hiddenCombinations: EmpiresHiddenCombinationsConfig
  loyalty: EmpiresLoyaltyConfig
}

export interface EmpiresUpgradeConfig {
  improveCost: number
  restoreCost: number
  defaultMaxLevel: number
}

export interface EmpiresEndgameConfig {
  schemaVersion: 7
  id: string
  title: string
  seed: string | number
  cards: EmpiresCardDefinition[]
  durak: EmpiresDurakConfig
  upgrades: EmpiresUpgradeConfig
  gifts: EmpiresGiftConfig
  governance: EmpiresGovernanceConfig
  empire: EmpiresEmpireConfig
  combat: EmpiresCombatConfig
  td: EmpiresTdConfig
  god: EmpiresGodScaffoldConfig
  quests: EmpiresQuestsScaffoldConfig
}

export interface EmpiresRngState {
  state: number
  draws: number
}

export interface EmpiresCardInstance {
  id: string
  definitionId: string
  level: number
  inverted: boolean
}

export interface EmpiresTablePair {
  attackCardId: string
  defenseCardId: string | null
}

export type EmpiresBoutStage = 'attack' | 'defense' | 'throwIn' | 'taking'

export interface EmpiresDurakState {
  deck: string[]
  playerHand: string[]
  godHand: string[]
  discard: string[]
  table: EmpiresTablePair[]
  trumpSuit: EmpiresSuit
  attacker: EmpiresActor
  defender: EmpiresActor
  stage: EmpiresBoutStage
  defenderHandAtBoutStart: number
  bout: number
  godInterventions: number
}

export interface EmpiresPerformanceState {
  successfulDefenses: number
  godTakes: number
  maxCardsGivenToGodAtOnce: number
  cardsGivenToGod: number
  cardsTakenByPlayer: number
  boutsWon: number
  boutsLost: number
}

export interface EmpiresCityState {
  id: string
  name: string
  regionId: string
  population: number
  militaryPopulation: number
  populationClasses: Record<string, number>
  baseProduction: Record<string, number>
  buildingLevels: Record<string, number>
  operationalBuildingLevels: Record<string, number>
  buildingSlotAssignments: Record<string, string>
  /** Migration-only Phase-2 aggregate; current saves normalize it into cohorts. */
  recruitedUnits?: Record<string, number>
  recruitedUnitCohorts: EmpiresRecruitedUnitCohortState[]
  resources: Record<string, number>
  buildingInteractionLocks: Record<string, number>
  lockedFacilities: Partial<Record<EmpiresFacilityLock, string>>
  foodCommitted: number
  lastProduction: Record<string, number>
  lastStarvationLoss: number
  loyalty: number
}

/** Migration-only Phase-2/3 queue; schema-v4 saves consume it into loyalty state. */
export interface EmpiresPendingLoyaltyDelta {
  cityId?: string
  regionId?: string
  amount: number
  sourceId: string
}

export interface EmpiresVeteranState {
  unitId: string
  wounds: number
}

export interface EmpiresRecruitedUnitCohortState {
  id: string
  unitId: string
  loadoutId: string
  count: number
  weaponEquipmentId?: string
  defenseEquipmentId?: string
  weapon: import('./combat/types').CombatWeaponProfile | null
  armor: import('./combat/types').CombatArmorProfile | null
}

export interface EmpiresArmyState {
  equipmentStock: Record<string, number>
  pendingLoyaltyDeltas?: EmpiresPendingLoyaltyDelta[]
  morale: number
  maxMorale: number
  veterans: Record<string, EmpiresVeteranState>
  recruitmentPenalties: Record<string, number>
  foundryInstantReadyConByCity: Record<string, number>
}

export interface EmpiresExternalState {
  allianceThreat: number
  nextWaveCon: number
  pendingOffers: never[]
}

export type EmpiresMinigameOriginContext =
  | {
    kind: 'alliance-wave'
    scheduledCon: number
    waveId: string
  }
  | {
    kind: 'manual'
    sourceId: string
  }

export interface EmpiresMinigameOrigin {
  returnPhase: Exclude<EmpiresPhase, 'minigame'>
  context: EmpiresMinigameOriginContext
}

export interface EmpiresMinigameSession {
  id: string
  kind: 'td'
  plan: TdBattlePlan
  rulesIdentity: TdRulesIdentity
  seed: string | number
  attempt: number
  origin: EmpiresMinigameOrigin
}

export type EmpiresMinigameResult = TdBattleResult

export interface EmpiresMinigameResultRecord {
  sessionId: string
  attempt: number
  origin: EmpiresMinigameOrigin
  result: EmpiresMinigameResult
}

export interface EmpiresMinigameResultCompaction {
  evictedCount: number
  historyDigest: string
  lastSessionId: string | null
  lastRulesDigest: string | null
}

export interface EmpiresProductionBoostAssignment {
  cityId: string
  buildingId: string
}

export interface EmpiresSteelBranchEntryState {
  fromTechnologyId: string
  toTechnologyId: string
  fromBranchId: string
  toBranchId: string
  con: number
}

export interface EmpiresDelayedFreeResearchState {
  scheduledAtCon: number
  eligibleCon: number
  awardedAtCon: number | null
}

export interface EmpiresSteelResearchState {
  branchCostMultipliers: Record<string, number>
  branchEntries: EmpiresSteelBranchEntryState[]
  delayedFree: Record<string, EmpiresDelayedFreeResearchState>
}

export interface EmpiresTechnologySideState {
  sideId: string
  selectedAtCon: number
  revealedAtCon: number | null
  effectsAppliedAtCon: number | null
  suppressedAtCon: number | null
}

export interface EmpiresTechnologySideView extends EmpiresTechnologySideState {
  technologyId: string
  sideName: string
  alignment: EmpiresTechnologySideAlignment | null
  disclosureKind: EmpiresTechnologySideDisclosure['kind']
}

export interface EmpiresSeasonView extends EmpiresSeasonDefinition {
  foodProductionMultiplierApplied: number
  greenhouseEqualized: boolean
}

export interface EmpiresHiddenCombinationTriggerState {
  triggeredAtCon: number
}

export interface EmpiresAdvisorState {
  status: EmpiresAdvisorStatus
  transitionSequence: number | null
  transitionedAtCon: number | null
  transitionSourceId: string | null
}

export interface EmpiresGovernorAssignmentState {
  perstId: string
  regionId: string
  assignedAtCon: number
}

export interface EmpiresGovernanceState {
  advisors: Record<string, EmpiresAdvisorState>
  nextAdvisorTransitionSequence: number
  governorAssignments: Record<string, EmpiresGovernorAssignmentState>
}

export interface EmpiresResearchQuote {
  technologyId: string
  requiredTechnologyIds: string[]
  resourceCosts: EmpiresResourceAmount[]
  timeCostDays: number
  costMultiplier: number
  entryFromTechnologyId: string | null
  freeEligibleCon: number | null
  blockedReason: string | null
  researched: boolean
}

export interface EmpiresRecruitmentQuote {
  cityId: string
  unitId: string
  count: number
  resourceCosts: EmpiresResourceAmount[]
  equipmentCosts: TdEquipmentCost[]
  timeCostDays: number
  loadoutId: string
  usedFoundryInstant: boolean
  blockedReason: string | null
}

export type EmpiresRegionControlStatus = 'controlled' | 'rebellious'

export interface EmpiresRegionLoyaltyState {
  value: number
  status: EmpiresRegionControlStatus
  negativeStreak: number
  recoveryStreak: number
  rebelledAtCon: number | null
  recoveredAtCon: number | null
}

export interface EmpiresLoyaltyState {
  regions: Record<string, EmpiresRegionLoyaltyState>
  classModifiers: Record<string, Record<string, number>>
  consumedBattleLossIds: string[]
}

export type EmpiresChronicleEntryKind =
  | 'loyalty'
  | 'reputation'
  | 'rebellion'
  | 'recovery'
  | 'battle-loss'
  | 'season'
  | 'technology-disclosure'
  | 'hidden-combination'

export interface EmpiresChronicleEntry {
  id: string
  sequence: number
  con: number
  kind: EmpiresChronicleEntryKind
  sourceId: string
  title: string
  description: string
  target?: EmpiresLoyaltyTarget | { kind: 'empire' }
  requestedAmount?: number
  appliedAmount?: number
}

export interface EmpiresCityLoyaltyView {
  cityId: string
  cityLoyalty: number
  regionLoyalty: number
  effectiveLoyalty: number
  baseWorkforce: number
  effectiveWorkforce: number
  workforceDivisor: number
  classLoyalty: Record<string, number>
}

export interface EmpiresBuildingOperationView {
  cityId: string
  buildingId: string
  purchasedLevel: number
  operationalLevel: number
  blockedReason: string | null
}

export interface EmpiresBattleLossLoyaltyInput {
  id: string
  target: Extract<EmpiresLoyaltyTarget, { kind: 'city' | 'region' }>
  deployed: number
  lost: number
}

export interface EmpiresEmpireState {
  daysRemaining: number
  resources: Record<string, number>
  flags: Record<string, number>
  reputation: number
  loyalty: EmpiresLoyaltyState
  chronicle: EmpiresChronicleEntry[]
  nextChronicleSequence: number
  cities: EmpiresCityState[]
  researchedTechnologyIds: string[]
  claimedGiftIds: string[]
  activeGiftIds: string[]
  productionMultipliers: Record<string, number>
  passiveFoodBonuses: Record<string, number>
  /** Temporary flag contribution from the cards held for the current empire phase. */
  cardFlagBonuses: Record<string, number>
  productionBoostAssignments: EmpiresProductionBoostAssignment[]
  destroyedRegionIds: string[]
  buildingLevelBonuses: Partial<Record<EmpiresBuildingSlotKind, number>>
  researchUsage: Record<string, string>
  steelResearch: EmpiresSteelResearchState
  technologySides: Record<string, EmpiresTechnologySideState>
  hiddenCombinationTriggers: Record<string, EmpiresHiddenCombinationTriggerState>
  smithSpecializationRecipeId: string | null
  giftResolutionTargets: Record<string, string>
}

export interface EmpiresEventState {
  eventId: string
  /**
   * A famine crisis selected before end-of-empire food settlement. Missing on
   * legacy snapshots, which means the event was already reached post-settlement.
   */
  empireSettlementPending?: boolean
}

export interface EmpiresCampaignState {
  schemaVersion: 5
  configId: string
  phase: EmpiresPhase
  rng: EmpiresRngState
  cards: Record<string, EmpiresCardInstance>
  durak: EmpiresDurakState
  performance: EmpiresPerformanceState
  con: number
  boutsInCon: number
  upgradePoints: number
  performanceScore: number
  giftChoiceIds: string[]
  governance: EmpiresGovernanceState
  pendingResolution: EmpiresPendingGiftResolution | null
  minigame: EmpiresMinigameSession | null
  minigameResultLog: EmpiresMinigameResultRecord[]
  minigameResultCompaction: EmpiresMinigameResultCompaction
  army: EmpiresArmyState
  external: EmpiresExternalState
  epidemics: never[]
  quests: Record<string, never>
  empire: EmpiresEmpireState
  event: EmpiresEventState | null
  outcomeReason: string | null
  revision: number
}

export interface EmpiresSnapshotEnvelope {
  schemaVersion: 5
  savedAt: string
  state: EmpiresCampaignState
}

export type EmpiresStateListener = (state: EmpiresCampaignState) => void
