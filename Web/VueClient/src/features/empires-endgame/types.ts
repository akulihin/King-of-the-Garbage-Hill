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

export interface EmpiresResourceAmount {
  resourceId: string
  amount: number
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
  slot: 'farm' | 'lumber' | 'mine' | 'smithy' | 'barracks' | 'unique' | 'municipal'
  levels: EmpiresBuildingLevelDefinition[]
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
}

export interface EmpiresEventChoiceDefinition {
  id: string
  label: string
  description?: string
  resourceCosts?: EmpiresResourceAmount[]
  effects: EmpiresEffect[]
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
  kind: 'farm' | 'lumber' | 'mine' | 'smithy' | 'barracks' | 'unique' | 'municipal'
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

export interface EmpiresEmpireConfig {
  daysPerPhase: number
  foodResourceId: string
  eventChance: number
  defeatPopulationAtOrBelow: number
  lockProviderBuildingIds: Record<EmpiresFacilityLock, string>
  resources: Array<{ id: string, name: string }>
  initialResources: Record<string, number>
  initialFlags?: Record<string, number>
  map: EmpiresMapConfig
  populationClasses: EmpiresPopulationClassDefinition[]
  cities: EmpiresInitialCity[]
  buildings: EmpiresBuildingDefinition[]
  units?: EmpiresUnitDefinition[]
  technologies: EmpiresTechnologyDefinition[]
  events: EmpiresEventDefinition[]
}

export interface EmpiresUpgradeConfig {
  improveCost: number
  restoreCost: number
  defaultMaxLevel: number
}

export interface EmpiresEndgameConfig {
  schemaVersion: 1
  id: string
  title: string
  seed: string | number
  cards: EmpiresCardDefinition[]
  durak: EmpiresDurakConfig
  upgrades: EmpiresUpgradeConfig
  gifts: EmpiresGiftConfig
  empire: EmpiresEmpireConfig
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
  recruitedUnits: Record<string, number>
  lockedFacilities: Partial<Record<EmpiresFacilityLock, string>>
  foodCommitted: number
  lastProduction: Record<string, number>
  lastStarvationLoss: number
}

export interface EmpiresProductionBoostAssignment {
  cityId: string
  buildingId: string
}

export interface EmpiresEmpireState {
  daysRemaining: number
  resources: Record<string, number>
  flags: Record<string, number>
  cities: EmpiresCityState[]
  researchedTechnologyIds: string[]
  claimedGiftIds: string[]
  activeGiftIds: string[]
  productionMultipliers: Record<string, number>
  passiveFoodBonuses: Record<string, number>
  /** Temporary flag contribution from the cards held for the current empire phase. */
  cardFlagBonuses: Record<string, number>
  productionBoostAssignments: EmpiresProductionBoostAssignment[]
}

export interface EmpiresEventState {
  eventId: string
}

export interface EmpiresCampaignState {
  schemaVersion: 1
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
  empire: EmpiresEmpireState
  event: EmpiresEventState | null
  outcomeReason: string | null
  revision: number
}

export interface EmpiresSnapshotEnvelope {
  schemaVersion: 1
  savedAt: string
  state: EmpiresCampaignState
}

export type EmpiresStateListener = (state: EmpiresCampaignState) => void
