import type {
  CombatArmorProfile,
  CombatCounterRule,
  CombatDamageAutoPriority,
  CombatWeaponProfile,
  EmpiresCombatConfig,
} from './combat/types'
import type { EmpiresTdConfig } from './td/types'
import type { EmpiresAlchemyConfig } from './alchemy/types'
import type { EmpiresInventoryConfig } from './inventory/types'
import type {
  EmpiresBuildingSlotKind,
  EmpiresCampaignState,
  EmpiresDependency,
  EmpiresDomesticEconomyConfig,
  EmpiresEconomyContentConfig,
  EmpiresEffect,
  EmpiresEpidemicConfig,
  EmpiresEndgameConfig,
  EmpiresExpeditionsConfig,
  EmpiresExternalConfig,
  EmpiresGodConfig,
  EmpiresLoyaltyConfig,
  EmpiresMedicalConfig,
  EmpiresSeasonsConfig,
  EmpiresTavernConfig,
} from './types'
import { EMPIRES_GOD_DIALOGUE_TRIGGERS } from './types'
import { validateEmpiresEndgameConfig } from './engine'
import { validateEmpiresQuestsConfig } from './quests'

export const EMPIRES_CONFIG_URL = '/empires-endgame/game-config.json'
export const EMPIRES_CONFIG_STORAGE_KEY = 'empires-endgame:config:v1'
export const EMPIRES_CONFIG_SCHEMA_VERSION = 17
export const EMPIRES_ACTIVE_MINIGAME_CONFIG_ERROR = 'Нельзя менять правила во время боя. Сначала завершите бой или выйдите через действие отмены.'

export function empiresConfigReplacementDisabledReason(
  state: Pick<EmpiresCampaignState, 'phase' | 'minigame'> | null,
): string | null {
  return state?.minigame || state?.phase === 'minigame'
    ? EMPIRES_ACTIVE_MINIGAME_CONFIG_ERROR
    : null
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

const COMBAT_SCAFFOLD = {
  enabled: false,
  damageTypes: [],
  armorClasses: [],
  counterRules: [],
  equipment: [],
}

const TD_V2_SCAFFOLD = {
  enabled: false,
  tickMs: 50,
  maxTicks: 4_000,
  waveEveryCons: 2,
  startingBuildResources: 120,
  towerBase: {
    id: 'tower-generic',
    name: 'Базовая башня',
    maxHp: 100,
    range: 240,
    attackIntervalTicks: 20,
    projectiles: 1,
    weapon: { damageLevels: { impact: 2 }, tags: ['tower'] },
    targetPriority: 'first',
  },
  alliance: {
    baseThreat: 0,
    threatPerWave: 1,
    healthPerThreat: 0.15,
    countPerThreat: 0.1,
    speedPerThreat: 0.02,
  },
  settlement: {
    lossLoyaltyThreshold: 0.1,
    loyaltyDelta: -1,
    veteranHealthThreshold: 0.5,
    recruitmentPenaltyPerLoss: 1,
    growthPenaltyPerLoss: 1,
    victory: { moraleDelta: 1, allianceThreatDelta: 0, recruitmentPenaltyPerDeployedUnit: 0 },
    defeat: { moraleDelta: -1, allianceThreatDelta: 1, recruitmentPenaltyPerDeployedUnit: 0 },
    abort: { moraleDelta: -2, allianceThreatDelta: 2, recruitmentPenaltyPerDeployedUnit: 0.25 },
  },
  morale: { initial: 0, minimum: 0, maximum: 2 },
  equipmentProduction: [{ equipmentId: 'basic-kit', amountPerSmithCapacity: 1 }],
  battlefields: [],
  towers: [],
  waves: [],
}

const GOVERNANCE_SCAFFOLD = {
  enabled: false,
  advisors: [],
  advisorDecisions: [],
  trump: {
    restrictedSuit: 'clubs',
    grandAdvisorId: '',
    lockedFallbackSuit: 'spades',
    criticalEffectMultiplier: 1,
  },
  persts: [],
  governor: {
    assignmentMode: 'permanent',
    regionIds: [],
    citySites: [],
  },
  capital: {
    cityId: '',
    sites: [],
  },
}

const TD_V3_SCAFFOLD = {
  enabled: false,
  regionalCatalogEnabled: false,
  tickMs: 50,
  maxTicks: 4_000,
  maxCommands: 128,
  resultLogLimit: 32,
  maxCatchUpTicksPerFrame: 8,
  waveEveryCons: 2,
  startingBuildResources: 120,
  towerBases: [],
  alliance: cloneJson(TD_V2_SCAFFOLD.alliance),
  settlement: cloneJson(TD_V2_SCAFFOLD.settlement),
  morale: cloneJson(TD_V2_SCAFFOLD.morale),
  equipmentProduction: cloneJson(TD_V2_SCAFFOLD.equipmentProduction),
  battlefields: [],
  towers: [],
  gradeChoices: [],
  waves: [],
  planVariants: [],
}

const TD_SCAFFOLD = {
  ...cloneJson(TD_V3_SCAFFOLD),
  equipmentProductionLines: [{
    id: 'smithy-general',
    capacityFlagId: 'smithCapacity',
    capacityShare: 1,
  }],
  equipmentProduction: [{
    id: 'basic-kit',
    equipmentId: 'basic-kit',
    lineId: 'smithy-general',
    amountPerSmithCapacity: 1,
    priority: 0,
  }],
}

const STEEL_RESEARCH_SCAFFOLD = {
  forkSourcePriceMultiplier: 2,
  delayedFreeEmpirePhases: 2,
  militaryEliteFlagId: 'militaryElite',
}

const GOD_SCAFFOLD = {
  enabled: false,
  deckMemory: {
    enabled: false,
    availability: 'always',
    inspectionsPerCon: 0,
    orientation: 'nextDrawFirst',
    excludedDefinitionIds: [],
  },
  antiBito: {
    enabled: false,
    minimumConsecutiveBito: 1,
    returnCount: 1,
    maxInterventions: 0,
    source: 'discard',
    insertion: 'drawBottom',
    orientation: 'preserve',
    excludedDefinitionIds: [],
    historyRetention: 16,
  },
  lines: [],
  dialogueLogRetention: 24,
  mercyConfirmation: {
    enabled: false,
    title: 'Вы собираетесь потратить Божественную Милость ({available}/{cost}) на переворот карты',
    confirmLabel: 'Да я и сам знаю! Не показывайте мне это больше!',
    cancelLabel: 'Нет, тогда я попридержу...',
  },
} satisfies EmpiresGodConfig

const TAVERN_SCAFFOLD = {
  enabled: false,
  buildingId: '',
  spawn: {
    eligibleCon: 4,
    firstRunChance: 0,
    secondRunChance: 1,
    laterRunChance: 0.33,
  },
  visitCooldownCons: 1,
  maxCommands: 8,
  mercenaries: {
    baseOfferCount: 0,
    spiritsOfferCount: 0,
    offers: [],
  },
  spirits: {
    goldCost: 0,
    activationDelayCons: 1,
    durationCons: 2,
    cheapOfferMultiplier: 1,
  },
  rumors: {
    goldCost: 0,
    deckHintPosition: 1,
    fallbackText: 'Слухи сегодня ничего не подтверждают.',
  },
  maria: {
    encounterChance: 0.33,
    standardCardDefinitionId: '',
    title: 'Мария Брауз',
    description: '',
    encounterDeferredReason: 'Точные правила карточной партии 2×2 не определены.',
  },
  queen: {
    mysticDefinitionId: '',
    comboRanks: ['3', '7', 'ace'],
    pulseEveryCons: 3,
  },
  historyRetention: 24,
  deferredSubfeatures: [],
} satisfies EmpiresTavernConfig

const ALCHEMY_SCAFFOLD = {
  enabled: false,
  buildingId: '',
  tickMs: 50,
  maxTicks: 5_000,
  maxCommands: 256,
  resultLogLimit: 32,
  maxCatchUpTicksPerFrame: 8,
  dayCost: 0,
  board: { width: 21, height: 21, centerX: 10, centerY: 10 },
  spawn: {
    minDelayTicks: 30,
    maxDelayTicks: 100,
    baseMoveIntervalTicks: 8,
    inwardSpeedMultiplier: 3,
  },
  acceleration: {
    baseSpeedPercent: 100,
    stepPercent: 1,
    piecesPerStep: 1,
    explosionThresholdPercent: 400,
    explosionBoundary: 'above',
  },
  reagents: {
    removeColorCharges: 0,
    addGrayCharges: 0,
    resetAccelerationCharges: 0,
  },
  explosion: {
    epidemicDefinitionId: '',
    severityMultiplier: 1,
    lockBuildingForCon: true,
  },
  colors: [],
  pieces: [],
  recipes: [],
  deferredSubfeatures: [],
} satisfies EmpiresAlchemyConfig

const EXPEDITIONS_SCAFFOLD = {
  enabled: false,
  resultHistoryRetention: 16,
  timeModel: 'preparation-days-and-abstract-travel-cons',
  veteran: {
    qualifyingMaximumHealthRatio: 0.5,
    removalWounds: 2,
    laterBattleBonus: null,
    laterBattleBonusDeferredReason: 'The raw expedition source names a later-battle veteran payoff but does not define it.',
  },
  zones: [],
  enemyProfiles: [],
  definitions: [],
} satisfies EmpiresExpeditionsConfig

const INVENTORY_SCAFFOLD = {
  enabled: false,
  tickMs: 50,
  maxTicks: 2_400,
  maxCommands: 256,
  resultLogLimit: 32,
  maxCatchUpTicksPerFrame: 8,
  maxItems: 24,
  targetUnitsPerItem: 500,
  board: { width: 10, height: 14, cartHeight: 8 },
  gravity: { intervalTicks: 10, spawnDelayTicks: 2 },
  scoring: { pointsPerWeight: 100, fullRowBonus: 500 },
  skipPolicy: 'direct-provision',
  abortPolicy: 'abort-expedition',
  itemDefinitions: [],
  deferredSubfeatures: [],
} satisfies EmpiresInventoryConfig

const QUESTS_SCAFFOLD = {
  enabled: false,
  historyRetention: 64,
  triggerHistoryRetention: 64,
  definitions: [],
}

const SEASONS_SCAFFOLD = {
  enabled: false,
  definitions: [],
  foodRounding: 'none',
  greenhouse: null,
} satisfies EmpiresSeasonsConfig

const HIDDEN_COMBINATIONS_SCAFFOLD = {
  enabled: false,
  definitions: [],
}

const EPIDEMICS_SCAFFOLD = {
  enabled: false,
  rulesVersion: 1,
  populationRounding: 'round',
  productionRounding: 'floor',
  loyaltyRounding: 'round',
  chronicleImpactEntriesPerEpidemic: 8,
  maxSpreadTargetsPerSettlement: 1,
  definitions: [],
  protections: [],
} satisfies EmpiresEpidemicConfig

const MEDICAL_SCAFFOLD = {
  enabled: false,
  hospitalBuildingId: '',
  medicalAcademyBuildingId: '',
  healerUnitId: '',
  defaultBattleRecoveryCons: 2,
  hospitalBattleRecoveryCons: 1,
  academyFreeResearchCadenceCons: 3,
  academyTreatmentDeathChance: 0.5,
} satisfies EmpiresMedicalConfig

const DOMESTIC_ECONOMY_SCAFFOLD = {
  enabled: false,
  goldResourceId: '',
  knowledgeResourceId: '',
  historyRetention: 16,
  loan: {
    bankBuildingId: '',
    bankingTechnologyId: '',
    principalIncomeTurns: 3,
    termCons: 7,
    paymentIncomeFraction: 0.5,
    maxActiveLoans: 1,
    defaultReputationDelta: -1,
    defaultLoyaltyDelta: -1,
    persecutionKnowledgeLossPercent: 50,
    persecutionReputationDelta: -3,
    persecutionLoyaltyDelta: -2,
  },
  insurance: {
    buildingId: '',
    calmTurnsRequired: 3,
    activeDurationCons: 8,
    basePayoutGold: 3_000,
    payoutPerCalmTurnGold: 500,
    maximumPayoutGold: 10_000,
    coveredIncidentKinds: [],
    unsupportedIncidentReasons: {},
  },
  fair: {
    buildingId: '',
    technologyId: '',
    actions: [],
    baronUnlockActionId: '',
  },
  temple: {
    buildingId: '',
    preachingCooldownCons: 1,
    relicSlotsPerLevel: 2,
    minimumTitheGold: 250,
    titheGoldPerPopulation: 0.001,
    preachingLoyaltyDelta: 1,
    preachingReputationDelta: 1,
  },
  tavern: {
    buildingId: '',
    recruitmentCapacityPerLevel: 1,
    moraleMaximumPerLevel: 1,
  },
} satisfies EmpiresDomesticEconomyConfig

const EXTERNAL_ECONOMY_SCAFFOLD = {
  enabled: false,
  historyRetention: 24,
  offerCadenceCons: 2,
  offerLifetimeCons: 2,
  maxActiveOffers: 2,
  goldResourceId: '',
  knowledgeResourceId: '',
  tradeRoutesTechnologyId: '',
  persecutionPricePenaltyPercent: 25,
  actors: [],
  unions: [],
  offers: [],
  transfer: { baseTimeCostDays: 4, compassTechnologyId: '', speedFlagId: 'transferSpeedPercent' },
  customs: {
    buildingId: '',
    tariffFlagId: 'customsPolicy',
    tariffPercentPerLevel: 10,
    merchantGuildsTechnologyId: '',
    merchantGuildsFlagId: 'merchantGuilds',
    merchantGuildTariffBonusPercent: 5,
    smugglingEventId: 'event-customs-smuggling',
  },
  stable: {
    buildingId: '',
    farmBuildingId: '',
    livestockResourceId: '',
    livestockRegionIds: [],
    mountedFlagId: 'mountedRecruitment',
    mountedUnitIds: [],
  },
  seaPort: {
    buildingId: '',
    capacityFlagId: 'maritimeTradeCapacity',
    maximumAcrossEmpire: 4,
    tradeGoldBonusPercentPerLevel: 10,
    knowledgePerTradePerLevel: 25,
  },
  reviewedAbsentBuildings: [],
} satisfies EmpiresExternalConfig

const ECONOMY_CONTENT_SCAFFOLD = {
  enabled: false,
  eventHistoryRetention: 24,
  smuggling: {
    eventId: 'event-customs-smuggling',
    stopChoiceId: 'stop-smuggling',
    taxChoiceId: 'tax-smuggling',
    durationCons: 1,
    stopCustomsIncomeMultiplier: 0,
    taxCustomsIncomeMultiplier: 2,
    stopPopulationGrowth: 100,
    taxPopulationGrowth: -100,
  },
  horseTheft: {
    eventId: 'event-horse-theft',
    huntChoiceId: 'hunt-thieves',
    dealChoiceId: 'make-deal',
    ignoreChoiceId: 'ignore-theft',
    stableBuildingId: '',
    livestockResourceId: '',
    noblePopulationClassId: '',
    recurrenceCooldownCons: 2,
    enemyYieldPerCon: 100,
  },
  insurance: {
    eventId: 'event-bank-insurance',
    acceptChoiceId: 'accept-insurance',
    declineChoiceId: 'decline-insurance',
    buildingId: '',
  },
  tradeCard: {
    externalTradeDisabledFlagId: 'externalTradeDisabled',
    internalTradeOnlyFlagId: 'internalTradeOnly',
  },
} satisfies EmpiresEconomyContentConfig

const LOYALTY_V4_SCAFFOLD = {
  enabled: false,
  cityRules: [],
  regionRules: [],
}

const DEFAULT_WORKFORCE_DIVISORS = [
  19, 18, 17, 16, 15, 13, 12, 11, 10, 9,
  8, 7, 6, 5, 5, 4, 3, 2, 1,
] as const

export function createDefaultEmpiresLoyaltyConfig(
  regionIds: readonly string[],
): EmpiresLoyaltyConfig {
  return {
    enabled: false,
    minimum: -9,
    maximum: 9,
    initialCityLoyalty: 0,
    initialClassLoyalty: 0,
    initialRegionLoyalty: Object.fromEntries(regionIds.map(regionId => [regionId, 0])),
    initialReputation: 0,
    workforceDivisors: DEFAULT_WORKFORCE_DIVISORS.map((divisor, index) => ({
      loyalty: index - 9,
      divisor,
    })),
    constructionMinimumLoyalty: 0,
    recruitmentMinimumLoyalty: 0,
    rebellion: {
      threshold: -6,
      sustainedApplications: 2,
      recoveryThreshold: 0,
      sustainedRecoveryApplications: 2,
    },
    classGates: [{
      id: 'smithy-burghers-loyalty',
      buildingId: 'building-smithy',
      populationClassId: 'burghers',
      minimumLoyalty: 0,
    }],
    chronicleRetention: 64,
  }
}

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function withScaffoldDefaults(
  value: unknown,
  defaults: Record<string, unknown>,
): unknown {
  if (value === undefined) return cloneJson(defaults)
  if (!isRecord(value)) return value
  return { ...cloneJson(defaults), ...value }
}

function migrateEmpiresConfigV1ToV2(config: Record<string, unknown>): Record<string, unknown> {
  config.combat = withScaffoldDefaults(config.combat, COMBAT_SCAFFOLD)
  config.td = withScaffoldDefaults(config.td, TD_V2_SCAFFOLD)
  config.god = withScaffoldDefaults(config.god, GOD_SCAFFOLD)
  config.quests = withScaffoldDefaults(config.quests, QUESTS_SCAFFOLD)
  if (isRecord(config.empire)) {
    config.empire.seasons = withScaffoldDefaults(config.empire.seasons, SEASONS_SCAFFOLD)
    config.empire.loyalty = withScaffoldDefaults(config.empire.loyalty, LOYALTY_V4_SCAFFOLD)
  }
  config.schemaVersion = 2
  return config
}

function normalizeEmpiresConfigV2(config: Record<string, unknown>): Record<string, unknown> {
  config.combat = withScaffoldDefaults(config.combat, COMBAT_SCAFFOLD)
  config.td = withScaffoldDefaults(config.td, TD_V2_SCAFFOLD)
  return config
}

function legacyTowerCategories(id: unknown): string[] {
  if (id === 'tower-g2-archers') return ['archer']
  if (id === 'tower-g2-crossbows') return ['crossbow']
  if (id === 'tower-g2-ballista') return ['ballista', 'artillery']
  if (id === 'tower-g2-trebuchet') return ['trebuchet', 'artillery']
  return ['tower']
}

function migrateEmpiresConfigV2ToV3(config: Record<string, unknown>): Record<string, unknown> {
  const rawTd = isRecord(config.td) ? config.td : cloneJson(TD_V2_SCAFFOLD)
  const legacyBase = isRecord(rawTd.towerBase) ? cloneJson(rawTd.towerBase) : null
  const towerBaseId = typeof legacyBase?.id === 'string' ? legacyBase.id : 'tower-generic'
  const towerBases = legacyBase
    ? [{ ...legacyBase, regionId: 'center', categoryIds: ['tower'], cost: 0 }]
    : []
  const legacyBattlefields = Array.isArray(rawTd.battlefields) ? rawTd.battlefields : []
  const battlefields = legacyBattlefields.flatMap((value) => {
    if (!isRecord(value)) return []
    const castleNodeId = typeof value.castleNodeId === 'string' ? value.castleNodeId : 'castle'
    const buildSpots = Array.isArray(value.buildSpots)
      ? value.buildSpots.map(spot => isRecord(spot) ? { ...spot, terrainId: 'ground' } : spot)
      : []
    const migrated = {
      ...value,
      regionId: 'center',
      buildSpots,
      objectiveNodeId: castleNodeId,
      towerBaseIds: [towerBaseId],
      allowedTowerCategoryIds: [],
      modifiers: [],
    }
    delete migrated.mode
    delete migrated.castleNodeId
    delete migrated.castleMaxHp
    delete migrated.castleArmor
    return [migrated]
  })
  const towers = Array.isArray(rawTd.towers)
    ? rawTd.towers.map(value => isRecord(value)
      ? { ...value, categoryIds: legacyTowerCategories(value.id) }
      : value)
    : []
  const gradeChoices = towers.length === 0
    ? []
    : [1, 2, 3, 4].map((grade) => {
        const choiceIds = towers.flatMap(value => isRecord(value)
          && value.grade === grade
          && typeof value.id === 'string'
          ? [value.id]
          : [])
        return choiceIds.length === 4
          ? {
              id: `legacy-center-grade-${grade}`,
              regionId: 'center',
              grade,
              choiceIds,
            }
          : {
              id: `legacy-center-grade-${grade}`,
              regionId: 'center',
              grade,
              choiceIds: [],
              deferredReason: `Legacy custom TD did not define four grade-${grade} choices.`,
            }
      })
  const waves = Array.isArray(rawTd.waves)
    ? rawTd.waves.map(value => !isRecord(value) || !Array.isArray(value.groups)
      ? value
      : {
          ...value,
          groups: value.groups.map(group => isRecord(group)
            ? { ...group, categoryIds: ['melee'] }
            : group),
        })
    : []
  const firstField = battlefields.find(isRecord)
  const firstWave = waves.find(isRecord)
  const objectiveNodeId = typeof firstField?.objectiveNodeId === 'string' ? firstField.objectiveNodeId : 'castle'
  const legacyField = legacyBattlefields.find(isRecord)
  const planVariants = firstField && firstWave && typeof firstField.id === 'string' && typeof firstWave.id === 'string'
    ? [{
        id: 'legacy-central-defense',
        name: 'Оборона Тетракора',
        mode: 'defense',
        battlefieldId: firstField.id,
        waveId: firstWave.id,
        objective: {
          id: 'legacy-central-castle',
          name: 'Крепость',
          kind: 'castle',
          owner: 'player',
          nodeId: objectiveNodeId,
          maxHp: typeof legacyField?.castleMaxHp === 'number' ? legacyField.castleMaxHp : 100,
          armor: legacyField?.castleArmor ?? null,
        },
        deploymentSpeedPerSecond: 0,
      }]
    : []
  config.td = {
    ...cloneJson(TD_V3_SCAFFOLD),
    ...rawTd,
    regionalCatalogEnabled: false,
    maxCommands: 128,
    resultLogLimit: 32,
    maxCatchUpTicksPerFrame: 8,
    towerBases,
    battlefields,
    towers,
    gradeChoices,
    waves,
    planVariants,
  }
  delete (config.td as Record<string, unknown>).towerBase
  config.schemaVersion = 3
  return config
}

function normalizeEmpiresConfigV3(config: Record<string, unknown>): Record<string, unknown> {
  config.combat = withScaffoldDefaults(config.combat, COMBAT_SCAFFOLD)
  config.td = withScaffoldDefaults(config.td, TD_V3_SCAFFOLD)
  return config
}

function migrateEmpiresConfigV3ToV4(config: Record<string, unknown>): Record<string, unknown> {
  const empire = isRecord(config.empire) ? config.empire : {}
  empire.steelResearch = withScaffoldDefaults(empire.steelResearch, STEEL_RESEARCH_SCAFFOLD)
  const nonCapitalCityIds = Array.isArray(empire.cities)
    ? empire.cities.flatMap(city => isRecord(city)
      && typeof city.id === 'string'
      && city.id !== 'city-tetrakor-capital' ? [city.id] : [])
    : []
  if (Array.isArray(empire.buildings)) {
    empire.buildings = empire.buildings.map((rawBuilding) => {
      if (!isRecord(rawBuilding)
        || rawBuilding.id !== 'building-foundry'
        || rawBuilding.allowedCityIds !== undefined) return rawBuilding
      return { ...rawBuilding, allowedCityIds: nonCapitalCityIds }
    })
  }
  if (Array.isArray(empire.technologies)) {
    empire.technologies = empire.technologies.map((rawTechnology) => {
      if (!isRecord(rawTechnology) || rawTechnology.category !== 'steel' || isRecord(rawTechnology.steel)) {
        return rawTechnology
      }
      return {
        ...rawTechnology,
        steel: {
          branchId: typeof rawTechnology.groupId === 'string' && rawTechnology.groupId.trim()
            ? rawTechnology.groupId
            : rawTechnology.id,
          generation: typeof rawTechnology.tier === 'number' ? rawTechnology.tier : 0,
          stage: 'whole',
          payoff: typeof rawTechnology.deferredReason === 'string' ? 'deferred' : 'unlock-only',
        },
      }
    })
  }
  config.empire = empire

  const rawTd = isRecord(config.td) ? config.td : cloneJson(TD_V3_SCAFFOLD)
  const hadEquipmentProduction = Array.isArray(rawTd.equipmentProduction)
  const oldRecipes = hadEquipmentProduction ? rawTd.equipmentProduction as unknown[] : []
  const recipeCount = Math.max(1, oldRecipes.length)
  const lines = oldRecipes.map((_, index) => ({
    id: `legacy-smithy-${index + 1}`,
    capacityFlagId: 'smithCapacity',
    capacityShare: 1 / recipeCount,
  }))
  const recipes = oldRecipes.map((rawRecipe, index) => isRecord(rawRecipe)
    ? {
        ...rawRecipe,
        id: typeof rawRecipe.id === 'string' && rawRecipe.id.trim()
          ? rawRecipe.id
          : `legacy-recipe-${String(rawRecipe.equipmentId ?? index + 1)}`,
        lineId: lines[index]?.id ?? 'legacy-smithy-1',
        priority: typeof rawRecipe.priority === 'number' ? rawRecipe.priority : 0,
      }
    : rawRecipe)
  config.td = {
    ...cloneJson(TD_SCAFFOLD),
    ...rawTd,
    equipmentProductionLines: lines.length > 0
      ? lines
      : hadEquipmentProduction ? [] : cloneJson(TD_SCAFFOLD.equipmentProductionLines),
    equipmentProduction: hadEquipmentProduction
      ? recipes
      : cloneJson(TD_SCAFFOLD.equipmentProduction),
  }
  config.schemaVersion = 4
  return config
}

function normalizeEmpiresConfigV4(config: Record<string, unknown>): Record<string, unknown> {
  config.combat = withScaffoldDefaults(config.combat, COMBAT_SCAFFOLD)
  config.td = withScaffoldDefaults(config.td, TD_SCAFFOLD)
  if (isRecord(config.empire)) {
    config.empire.steelResearch = withScaffoldDefaults(
      config.empire.steelResearch,
      STEEL_RESEARCH_SCAFFOLD,
    )
  }
  return config
}

function configRegionIds(config: Record<string, unknown>): string[] {
  if (!isRecord(config.empire) || !isRecord(config.empire.map) || !Array.isArray(config.empire.map.regions)) {
    return []
  }
  return config.empire.map.regions.flatMap(region => (
    isRecord(region) && typeof region.id === 'string' && region.id.trim() ? [region.id] : []
  ))
}

function withLoyaltyDefaults(
  value: unknown,
  regionIds: readonly string[],
): unknown {
  if (!isRecord(value)) return value
  const defaults = createDefaultEmpiresLoyaltyConfig(regionIds)
  return {
    ...defaults,
    ...value,
    initialRegionLoyalty: {
      ...defaults.initialRegionLoyalty,
      ...(isRecord(value.initialRegionLoyalty) ? value.initialRegionLoyalty : {}),
    },
    rebellion: {
      ...defaults.rebellion,
      ...(isRecord(value.rebellion) ? value.rebellion : {}),
    },
  }
}

function migrateEmpiresConfigV4ToV5(config: Record<string, unknown>): Record<string, unknown> {
  const regionIds = configRegionIds(config)
  if (isRecord(config.empire)) {
    const legacy = isRecord(config.empire.loyalty) ? config.empire.loyalty : {}
    const legacyCityRules = Array.isArray(legacy.cityRules) ? cloneJson(legacy.cityRules) : []
    const legacyRegionRules = Array.isArray(legacy.regionRules) ? cloneJson(legacy.regionRules) : []
    config.empire.loyalty = {
      ...createDefaultEmpiresLoyaltyConfig(regionIds),
      // Schema-v4 loyalty was an unread scaffold. Custom definitions stay disabled
      // instead of silently acquiring bundled Phase-4 rules.
      enabled: false,
      ...(legacyCityRules.length > 0 ? { legacyCityRules } : {}),
      ...(legacyRegionRules.length > 0 ? { legacyRegionRules } : {}),
    }
  }
  config.schemaVersion = 5
  return config
}

function normalizeEmpiresConfigV5(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV4(config)
  if (isRecord(config.empire)) {
    config.empire.loyalty = withLoyaltyDefaults(config.empire.loyalty, configRegionIds(config))
  }
  return config
}

function migrateEmpiresConfigV5ToV6(config: Record<string, unknown>): Record<string, unknown> {
  if (isRecord(config.empire)) {
    const legacySeasons = isRecord(config.empire.seasons) ? config.empire.seasons : {}
    const legacyDefinitions = Array.isArray(legacySeasons.definitions)
      ? cloneJson(legacySeasons.definitions)
      : []
    config.empire.seasons = {
      ...cloneJson(SEASONS_SCAFFOLD),
      ...(legacyDefinitions.length > 0 ? { legacyDefinitions } : {}),
    }
    config.empire.hiddenCombinations = cloneJson(HIDDEN_COMBINATIONS_SCAFFOLD)
  }
  config.schemaVersion = 6
  return config
}

function normalizeEmpiresConfigV6(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV5(config)
  if (isRecord(config.empire)) {
    config.empire.seasons = withScaffoldDefaults(config.empire.seasons, SEASONS_SCAFFOLD)
    config.empire.hiddenCombinations = withScaffoldDefaults(
      config.empire.hiddenCombinations,
      HIDDEN_COMBINATIONS_SCAFFOLD,
    )
  }
  return config
}

function migrateEmpiresConfigV6ToV7(config: Record<string, unknown>): Record<string, unknown> {
  config.governance = withScaffoldDefaults(config.governance, GOVERNANCE_SCAFFOLD)
  config.schemaVersion = 7
  return config
}

function normalizeEmpiresConfigV7(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV6(config)
  config.governance = withScaffoldDefaults(config.governance, GOVERNANCE_SCAFFOLD)
  return config
}

function migrateEmpiresConfigV7ToV8(config: Record<string, unknown>): Record<string, unknown> {
  if (isRecord(config.empire)) {
    config.empire.epidemics = withScaffoldDefaults(config.empire.epidemics, EPIDEMICS_SCAFFOLD)
    config.empire.medical = withScaffoldDefaults(config.empire.medical, MEDICAL_SCAFFOLD)
  }
  config.schemaVersion = 8
  return config
}

function normalizeEmpiresConfigV8(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV7(config)
  if (isRecord(config.empire)) {
    config.empire.epidemics = withScaffoldDefaults(config.empire.epidemics, EPIDEMICS_SCAFFOLD)
    config.empire.medical = withScaffoldDefaults(config.empire.medical, MEDICAL_SCAFFOLD)
  }
  return config
}

function migrateEmpiresConfigV8ToV9(config: Record<string, unknown>): Record<string, unknown> {
  if (isRecord(config.empire)) {
    config.empire.domesticEconomy = withScaffoldDefaults(
      config.empire.domesticEconomy,
      DOMESTIC_ECONOMY_SCAFFOLD,
    )
  }
  config.schemaVersion = 9
  return config
}

function normalizeEmpiresConfigV9(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV8(config)
  if (isRecord(config.empire)) {
    config.empire.domesticEconomy = withScaffoldDefaults(
      config.empire.domesticEconomy,
      DOMESTIC_ECONOMY_SCAFFOLD,
    )
  }
  return config
}

function migrateEmpiresConfigV9ToV10(config: Record<string, unknown>): Record<string, unknown> {
  if (isRecord(config.empire)) {
    config.empire.externalEconomy = withScaffoldDefaults(
      config.empire.externalEconomy,
      EXTERNAL_ECONOMY_SCAFFOLD,
    )
  }
  config.schemaVersion = 10
  return config
}

function normalizeEmpiresConfigV10(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV9(config)
  if (isRecord(config.empire)) {
    config.empire.externalEconomy = withScaffoldDefaults(
      config.empire.externalEconomy,
      EXTERNAL_ECONOMY_SCAFFOLD,
    )
  }
  return config
}

function migrateEmpiresConfigV10ToV11(config: Record<string, unknown>): Record<string, unknown> {
  if (isRecord(config.empire)) {
    // Schema v10 could not own this lifecycle. Ignore forward-shaped fields on legacy
    // fixtures/imports instead of accidentally activating P6C against incomplete carriers.
    config.empire.economyContent = cloneJson(ECONOMY_CONTENT_SCAFFOLD)
  }
  config.schemaVersion = 11
  return config
}

function normalizeEmpiresConfigV11(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV10(config)
  if (isRecord(config.empire)) {
    config.empire.economyContent = withScaffoldDefaults(
      config.empire.economyContent,
      ECONOMY_CONTENT_SCAFFOLD,
    )
  }
  return config
}

function migrateEmpiresConfigV11ToV12(config: Record<string, unknown>): Record<string, unknown> {
  config.quests = withScaffoldDefaults(config.quests, QUESTS_SCAFFOLD)
  if (isRecord(config.quests)) delete config.quests.dialogueGraphs
  if (isRecord(config.empire) && Array.isArray(config.empire.events) && isRecord(config.quests)) {
    const knownQuestIds = new Set(
      (Array.isArray(config.quests.definitions) ? config.quests.definitions : [])
        .filter(isRecord)
        .map(definition => definition.id)
        .filter((id): id is string => typeof id === 'string'),
    )
    // Schema v11 could not own an event-to-quest bridge. Fail closed when a
    // forward-shaped legacy fixture contains a bridge without its P7 quest:
    // the whole future event is omitted instead of becoming a live no-op.
    config.empire.events = config.empire.events.filter(event => {
      if (!isRecord(event) || !Array.isArray(event.choices)) return true
      return !event.choices.some(choice => isRecord(choice)
        && isRecord(choice.questResolution)
        && (typeof choice.questResolution.questId !== 'string'
          || !knownQuestIds.has(choice.questResolution.questId)))
    })
  }
  config.schemaVersion = 12
  return config
}

function normalizeEmpiresConfigV12(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV11(config)
  config.quests = withScaffoldDefaults(config.quests, QUESTS_SCAFFOLD)
  if (isRecord(config.quests)) delete config.quests.dialogueGraphs
  return config
}

function migrateEmpiresConfigV12ToV13(config: Record<string, unknown>): Record<string, unknown> {
  // Schema v12 exposed God-presence placeholders but had no executable readers.
  // Replace forward-shaped legacy input with the disabled schema-v13 contract so
  // importing an old custom config cannot silently activate new behavior.
  config.god = cloneJson(GOD_SCAFFOLD)
  config.schemaVersion = 13
  return config
}

function normalizeEmpiresConfigV13(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV12(config)
  config.god = withScaffoldDefaults(config.god, GOD_SCAFFOLD)
  if (isRecord(config.god)) {
    config.god.deckMemory = withScaffoldDefaults(
      config.god.deckMemory,
      GOD_SCAFFOLD.deckMemory,
    )
    config.god.antiBito = withScaffoldDefaults(
      config.god.antiBito,
      GOD_SCAFFOLD.antiBito,
    )
    config.god.mercyConfirmation = withScaffoldDefaults(
      config.god.mercyConfirmation,
      GOD_SCAFFOLD.mercyConfirmation,
    )
  }
  return config
}

function migrateEmpiresConfigV13ToV14(config: Record<string, unknown>): Record<string, unknown> {
  // P9 adds an executable Tavern/minigame contract and a separate mystic catalog.
  // Old custom configs remain fail-closed instead of silently gaining encounters.
  config.mysticCards = []
  config.tavern = cloneJson(TAVERN_SCAFFOLD)
  config.schemaVersion = 14
  return config
}

function normalizeEmpiresConfigV14(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV13(config)
  if (!Array.isArray(config.mysticCards)) config.mysticCards = []
  config.tavern = withScaffoldDefaults(config.tavern, TAVERN_SCAFFOLD)
  if (isRecord(config.tavern)) {
    config.tavern.spawn = withScaffoldDefaults(config.tavern.spawn, TAVERN_SCAFFOLD.spawn)
    config.tavern.mercenaries = withScaffoldDefaults(
      config.tavern.mercenaries,
      TAVERN_SCAFFOLD.mercenaries,
    )
    config.tavern.spirits = withScaffoldDefaults(config.tavern.spirits, TAVERN_SCAFFOLD.spirits)
    config.tavern.rumors = withScaffoldDefaults(config.tavern.rumors, TAVERN_SCAFFOLD.rumors)
    config.tavern.maria = withScaffoldDefaults(config.tavern.maria, TAVERN_SCAFFOLD.maria)
    config.tavern.queen = withScaffoldDefaults(config.tavern.queen, TAVERN_SCAFFOLD.queen)
  }
  return config
}

function migrateEmpiresConfigV14ToV15(config: Record<string, unknown>): Record<string, unknown> {
  // Schema v14 had no Alchemy plan, replay, or settlement arm. Legacy custom
  // configurations remain fail-closed instead of inheriting bundled laboratory rules.
  config.alchemy = cloneJson(ALCHEMY_SCAFFOLD)
  config.schemaVersion = 15
  return config
}

function normalizeEmpiresConfigV15(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV14(config)
  config.alchemy = withScaffoldDefaults(config.alchemy, ALCHEMY_SCAFFOLD)
  if (isRecord(config.alchemy)) {
    config.alchemy.board = withScaffoldDefaults(config.alchemy.board, ALCHEMY_SCAFFOLD.board)
    config.alchemy.spawn = withScaffoldDefaults(config.alchemy.spawn, ALCHEMY_SCAFFOLD.spawn)
    config.alchemy.acceleration = withScaffoldDefaults(
      config.alchemy.acceleration,
      ALCHEMY_SCAFFOLD.acceleration,
    )
    config.alchemy.reagents = withScaffoldDefaults(
      config.alchemy.reagents,
      ALCHEMY_SCAFFOLD.reagents,
    )
    config.alchemy.explosion = withScaffoldDefaults(
      config.alchemy.explosion,
      ALCHEMY_SCAFFOLD.explosion,
    )
  }
  return config
}

function migrateEmpiresConfigV15ToV16(config: Record<string, unknown>): Record<string, unknown> {
  // Schema v15 had only a generic fortress marker and no expedition lifecycle.
  // Custom configs migrate fail-closed: fortress identity/position survives, but
  // no target becomes launchable without an explicit schema-v16 definition.
  config.expeditions = cloneJson(EXPEDITIONS_SCAFFOLD)
  if (isRecord(config.empire) && isRecord(config.empire.map) && Array.isArray(config.empire.map.objects)) {
    config.empire.map.objects = config.empire.map.objects.map((rawObject) => {
      if (!isRecord(rawObject) || rawObject.kind !== 'fortress') return rawObject
      const migrated = { ...rawObject }
      delete migrated.properties
      migrated.payload = {
        kind: 'fortress',
        expeditionId: null,
        zoneId: null,
        deferredReason: 'Legacy fortress preserved without an authored expedition or zone reference.',
      }
      return migrated
    })
  }
  config.schemaVersion = 16
  return config
}

function normalizeEmpiresConfigV16(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV15(config)
  config.expeditions = withScaffoldDefaults(config.expeditions, EXPEDITIONS_SCAFFOLD)
  if (isRecord(config.expeditions)) {
    config.expeditions.veteran = withScaffoldDefaults(
      config.expeditions.veteran,
      EXPEDITIONS_SCAFFOLD.veteran,
    )
  }
  return config
}

function migrateEmpiresConfigV16ToV17(config: Record<string, unknown>): Record<string, unknown> {
  // Schema v16 launches expeditions through direct provisioning and has no typed
  // packing plan/result arm. Imported custom configs remain fail-closed.
  config.inventory = cloneJson(INVENTORY_SCAFFOLD)
  config.schemaVersion = 17
  return config
}

function normalizeEmpiresConfigV17(config: Record<string, unknown>): Record<string, unknown> {
  normalizeEmpiresConfigV16(config)
  config.inventory = withScaffoldDefaults(config.inventory, INVENTORY_SCAFFOLD)
  if (isRecord(config.inventory)) {
    config.inventory.board = withScaffoldDefaults(config.inventory.board, INVENTORY_SCAFFOLD.board)
    config.inventory.gravity = withScaffoldDefaults(config.inventory.gravity, INVENTORY_SCAFFOLD.gravity)
    config.inventory.scoring = withScaffoldDefaults(config.inventory.scoring, INVENTORY_SCAFFOLD.scoring)
  }
  return config
}

const EMPIRES_CONFIG_MIGRATIONS: Record<
  number,
  (config: Record<string, unknown>) => Record<string, unknown>
> = {
  1: migrateEmpiresConfigV1ToV2,
  2: migrateEmpiresConfigV2ToV3,
  3: migrateEmpiresConfigV3ToV4,
  4: migrateEmpiresConfigV4ToV5,
  5: migrateEmpiresConfigV5ToV6,
  6: migrateEmpiresConfigV6ToV7,
  7: migrateEmpiresConfigV7ToV8,
  8: migrateEmpiresConfigV8ToV9,
  9: migrateEmpiresConfigV9ToV10,
  10: migrateEmpiresConfigV10ToV11,
  11: migrateEmpiresConfigV11ToV12,
  12: migrateEmpiresConfigV12ToV13,
  13: migrateEmpiresConfigV13ToV14,
  14: migrateEmpiresConfigV14ToV15,
  15: migrateEmpiresConfigV15ToV16,
  16: migrateEmpiresConfigV16ToV17,
}

/**
 * Clones JSON config input and applies every sequential migration before validation.
 * Current-version inputs are cloned but otherwise unchanged.
 */
export function migrateEmpiresConfig(raw: unknown): unknown {
  if (!isRecord(raw)) return raw
  let migrated = cloneJson(raw)
  let version = migrated.schemaVersion
  if (typeof version === 'number' && version > EMPIRES_CONFIG_SCHEMA_VERSION) {
    throw new Error(`Unsupported future Empire's Endgame config schemaVersion ${version}.`)
  }
  while (typeof version === 'number' && version < EMPIRES_CONFIG_SCHEMA_VERSION) {
    const migrate = EMPIRES_CONFIG_MIGRATIONS[version]
    if (!migrate) break
    migrated = migrate(migrated)
    version = migrated.schemaVersion
  }
  if (version === 2) migrated = normalizeEmpiresConfigV2(migrated)
  if (version === 3) migrated = normalizeEmpiresConfigV3(migrated)
  if (version === 4) migrated = normalizeEmpiresConfigV4(migrated)
  if (version === 5) migrated = normalizeEmpiresConfigV5(migrated)
  if (version === 6) migrated = normalizeEmpiresConfigV6(migrated)
  if (version === 7) migrated = normalizeEmpiresConfigV7(migrated)
  if (version === 8) migrated = normalizeEmpiresConfigV8(migrated)
  if (version === 9) migrated = normalizeEmpiresConfigV9(migrated)
  if (version === 10) migrated = normalizeEmpiresConfigV10(migrated)
  if (version === 11) migrated = normalizeEmpiresConfigV11(migrated)
  if (version === 12) migrated = normalizeEmpiresConfigV12(migrated)
  if (version === 13) migrated = normalizeEmpiresConfigV13(migrated)
  if (version === 14) migrated = normalizeEmpiresConfigV14(migrated)
  if (version === 15) migrated = normalizeEmpiresConfigV15(migrated)
  if (version === 16) migrated = normalizeEmpiresConfigV16(migrated)
  if (version === 17) migrated = normalizeEmpiresConfigV17(migrated)
  return migrated
}

const EMPIRES_BUILDING_SLOT_KINDS = new Set<EmpiresBuildingSlotKind>([
  'farm',
  'lumber',
  'mine',
  'smithy',
  'barracks',
  'unique',
  'maritime',
  'municipal',
])

function validateGiftResolutions(config: EmpiresEndgameConfig): string[] {
  const errors: string[] = []
  const cityIds = new Set(config.empire.cities.map(city => city.id))
  const regionIds = new Set(config.empire.map.regions.map(region => region.id))
  const resourceIds = new Set(config.empire.resources.map(resource => resource.id))
  const availableGiftCount = config.gifts.definitions.filter((gift) => {
    const deferredReason = (gift as { deferredReason?: unknown }).deferredReason
    return (typeof deferredReason !== 'string' || !deferredReason.trim())
      && gift.kind !== 'relic'
  }).length

  if (
    (config.empire.initialFlags?.relicsUnlocked ?? 0) <= 0
    && availableGiftCount < config.gifts.choiceCount
  ) {
    errors.push('pre-unlock non-relic gift definitions without deferredReason must contain at least choiceCount entries')
  }

  for (const gift of config.gifts.definitions) {
    const deferredReason = (gift as { deferredReason?: unknown }).deferredReason
    if (deferredReason !== undefined && (
      typeof deferredReason !== 'string'
      || !deferredReason.trim()
    )) {
      errors.push(`gift ${gift.id} deferredReason must be a non-empty string`)
    }

    const resolution = (gift as { resolution?: unknown }).resolution
    if (resolution === undefined) continue
    if (!isRecord(resolution) || typeof resolution.kind !== 'string') {
      errors.push(`gift ${gift.id} resolution must be an object with a known kind`)
      continue
    }

    const targetedResources = gift.effects.filter(effect => effect.kind === 'resource')
    for (const effect of targetedResources) {
      if (!resourceIds.has(effect.resourceId)) {
        errors.push(`gift ${gift.id} resolution references unknown resource ${effect.resourceId}`)
      }
    }

    if (resolution.kind === 'cityResources') {
      if (cityIds.size === 0) errors.push(`gift ${gift.id} requires at least one city target`)
      if (targetedResources.length === 0) {
        errors.push(`gift ${gift.id} cityResources resolution requires a resource effect`)
      }
      continue
    }

    if (resolution.kind === 'meteorCity') {
      if (cityIds.size === 0) errors.push(`gift ${gift.id} requires at least one city target`)
      if (
        typeof resolution.damageLevels !== 'number'
        || !Number.isInteger(resolution.damageLevels)
        || resolution.damageLevels <= 0
      ) {
        errors.push(`gift ${gift.id} meteor damageLevels must be a positive integer`)
      }
      continue
    }

    if (resolution.kind === 'destroyRegion') {
      if (typeof resolution.regionId !== 'string' || !regionIds.has(resolution.regionId)) {
        errors.push(`gift ${gift.id} destroyRegion references unknown region ${String(resolution.regionId)}`)
      }
      continue
    }

    if (resolution.kind === 'buildingLevelBonus') {
      if (!Array.isArray(resolution.slots) || resolution.slots.length === 0) {
        errors.push(`gift ${gift.id} buildingLevelBonus requires at least one slot`)
      } else {
        const slots = resolution.slots as unknown[]
        if (slots.some(slot => typeof slot !== 'string' || !EMPIRES_BUILDING_SLOT_KINDS.has(
          slot as EmpiresBuildingSlotKind,
        ))) {
          errors.push(`gift ${gift.id} buildingLevelBonus references an unknown slot`)
        }
        if (new Set(slots).size !== slots.length) {
          errors.push(`gift ${gift.id} buildingLevelBonus slots must be unique`)
        }
      }
      if (
        typeof resolution.amount !== 'number'
        || !Number.isInteger(resolution.amount)
        || resolution.amount <= 0
      ) {
        errors.push(`gift ${gift.id} buildingLevelBonus amount must be a positive integer`)
      }
      continue
    }

    errors.push(`gift ${gift.id} resolution has unknown kind ${resolution.kind}`)
  }

  return errors
}

function validateDeferredReasons(config: EmpiresEndgameConfig): string[] {
  const errors: string[] = []
  const check = (label: string, deferredReason: unknown) => {
    if (deferredReason !== undefined && (
      typeof deferredReason !== 'string'
      || !deferredReason.trim()
    )) {
      errors.push(`${label} deferredReason must be a non-empty string`)
    }
  }
  const checkSubfeatures = (label: string, value: unknown) => {
    if (value === undefined) return
    if (!Array.isArray(value)) {
      errors.push(`${label} deferredSubfeatures must be an array`)
      return
    }
    const ids = new Set<string>()
    for (const rawSubfeature of value) {
      if (!isRecord(rawSubfeature)
        || typeof rawSubfeature.id !== 'string'
        || !rawSubfeature.id.trim()
        || typeof rawSubfeature.reason !== 'string'
        || !rawSubfeature.reason.trim()) {
        errors.push(`${label} deferredSubfeatures need non-empty id and reason`)
        continue
      }
      if (ids.has(rawSubfeature.id)) errors.push(`${label} repeats deferred subfeature ${rawSubfeature.id}`)
      ids.add(rawSubfeature.id)
    }
  }

  for (const card of config.cards) {
    check(`card ${card.id} normal face`, card.normal.deferredReason)
    check(`card ${card.id} inverted face`, card.inverted.deferredReason)
  }
  for (const card of config.mysticCards) {
    check(`mystic card ${card.id}`, card.deferredReason)
    check(`mystic card ${card.id} normal face`, card.normal.deferredReason)
    check(`mystic card ${card.id} inverted face`, card.inverted.deferredReason)
  }
  checkSubfeatures('tavern', config.tavern.deferredSubfeatures)
  checkSubfeatures('alchemy', config.alchemy.deferredSubfeatures)
  for (const recipe of config.alchemy.recipes) {
    check(`alchemy recipe ${recipe.id}`, recipe.deferredReason)
  }
  checkSubfeatures('inventory', config.inventory.deferredSubfeatures)
  for (const item of config.inventory.itemDefinitions) {
    check(`inventory item ${item.id}`, item.deferredReason)
  }
  for (const zone of config.expeditions.zones) {
    checkSubfeatures(`expedition zone ${zone.id}`, zone.deferredSubfeatures)
  }
  for (const expedition of config.expeditions.definitions) {
    check(`expedition ${expedition.id}`, expedition.deferredReason)
  }
  for (const object of config.empire.map.objects) {
    if (object.kind === 'fortress') check(`fortress ${object.id}`, object.payload.deferredReason)
  }
  for (const gift of config.gifts.definitions) check(`gift ${gift.id}`, gift.deferredReason)
  for (const resource of config.empire.resources) check(`resource ${resource.id}`, resource.deferredReason)
  for (const building of config.empire.buildings) {
    check(`building ${building.id}`, building.deferredReason)
    checkSubfeatures(`building ${building.id}`, building.deferredSubfeatures)
  }
  for (const unit of config.empire.units ?? []) check(`unit ${unit.id}`, unit.deferredReason)
  for (const technology of config.empire.technologies) {
    check(`technology ${technology.id}`, technology.deferredReason)
    checkSubfeatures(`technology ${technology.id}`, technology.deferredSubfeatures)
  }
  for (const combination of config.empire.hiddenCombinations.definitions) {
    check(`hidden combination ${combination.id}`, combination.deferredReason)
  }
  for (const event of config.empire.events ?? []) {
    check(`event ${event.id}`, event.deferredReason)
    for (const choice of event.choices) {
      check(`event ${event.id} choice ${choice.id}`, choice.deferredReason)
    }
  }
  for (const quest of config.quests.definitions ?? []) {
    check(`quest ${quest.id}`, quest.deferredReason)
    for (const stage of quest.stages ?? []) {
      for (const node of stage.nodes ?? []) {
        for (const choice of node.choices ?? []) {
          check(`quest ${quest.id} choice ${choice.id}`, choice.deferredReason)
        }
      }
    }
  }
  for (const equipment of config.combat.equipment) {
    check(`combat equipment ${equipment.id}`, equipment.deferredReason)
  }
  return errors
}

const EMPIRES_LIVE_FLAG_ALLOWLIST = new Set([
  'famineProtectionTurns',
  'famineYear',
  'famineYearCounter',
  'equippedRecruitCapacity',
  'expeditionProvisionInstallmentTurns',
  'expeditionSpeedPercent',
  'externalTradeDisabled',
  'horseTheftDisabled',
  'idleBuildingGoldBase',
  'loyaltyMultiplierPercent',
  'militaryArson',
  'maxCombatSpirit',
  'minimumCombatSpirit',
  'armyProductionDiscountPercent',
  'armyProductionTimeDiscountPercent',
  'armyUpkeepDiscountPercent',
  'casualtyLoyaltyPenaltyDisabled',
  'casualtyRecruitGrowthPenaltyDisabled',
  'coercionBuildingOverride',
  'darkExperimentsDisabled',
  'instantUnitEveryTurns',
  'internalTradeOnly',
  'logisticsMapBonusPercent',
  'peasantProductivityPercent',
  'productionBoostAssignmentLimit',
  'productionBoostPercent',
  'provisionEfficiencyPercent',
  'recruitmentDisabled',
  'relicsUnlocked',
  'smithyWithoutIron',
  'smithSpecializationLocked',
  'smithCapacity',
  'customsPolicy',
  'mountedRecruitment',
  'maritimeTradeCapacity',
  'merchantGuilds',
  'transferSpeedPercent',
  'stableWithoutLivestock',
  'starvationLossMultiplierPercent',
  'surplusFoodPerGold',
  'templarTransferLossPercent',
  'titheIncomePercent',
  'treasuryGoldPerSavedMillion',
  'theocracy',
  'unlimitedTavernRecruitment',
  'worldMaps',
])

function validateLiveEffects(config: EmpiresEndgameConfig): string[] {
  const errors: string[] = []
  const dependencyFlagIds = new Set<string>()
  const epidemicProtectionFlagIds = new Set(config.empire.epidemics.protections.flatMap(protection => (
    protection.source.kind === 'flag' ? [protection.source.flagId] : []
  )))
  const collectDependencies = (dependencies: readonly { kind: string, flagId?: string }[]) => {
    for (const dependency of dependencies) {
      if (dependency.kind === 'flag' && dependency.flagId) dependencyFlagIds.add(dependency.flagId)
    }
  }
  for (const building of config.empire.buildings) {
    if (building.deferredReason) continue
    for (const level of building.levels) collectDependencies(level.dependencies)
  }
  for (const unit of config.empire.units ?? []) {
    if (!unit.deferredReason) collectDependencies(unit.dependencies)
  }
  for (const technology of config.empire.technologies) {
    if (!technology.deferredReason) collectDependencies(technology.prerequisites)
  }
  for (const event of config.empire.events) {
    if (!event.deferredReason) collectDependencies(event.prerequisites ?? [])
  }
  for (const offer of config.empire.externalEconomy.offers) {
    collectDependencies(offer.prerequisites)
  }
  for (const union of config.empire.externalEconomy.unions) {
    collectDependencies(union.prerequisites)
  }
  for (const quest of config.quests.definitions) {
    if (quest.deferredReason) continue
    if (quest.trigger.kind === 'flag') dependencyFlagIds.add(quest.trigger.flagId)
    for (const stage of quest.stages) {
      for (const node of stage.nodes) {
        for (const choice of node.choices) {
          if (!choice.deferredReason) collectDependencies(choice.requirements ?? [])
        }
      }
    }
  }
  for (const recipe of config.alchemy.recipes) {
    if (!recipe.deferredReason) collectDependencies(recipe.prerequisites)
  }

  const check = (
    label: string,
    effects: readonly { kind: string, flagId?: string }[],
    deferredReason?: string,
  ) => {
    if (deferredReason) return
    for (const effect of effects) {
      if (effect.kind !== 'flag' || !effect.flagId) continue
      if (
        EMPIRES_LIVE_FLAG_ALLOWLIST.has(effect.flagId)
        || epidemicProtectionFlagIds.has(effect.flagId)
        || dependencyFlagIds.has(effect.flagId)
      ) continue
      errors.push(
        `${label} uses unsupported live flag ${effect.flagId}; add engine support or deferredReason`,
      )
    }
  }

  for (const card of config.cards) {
    check(`card ${card.id} normal face`, card.normal.effects, card.normal.deferredReason)
    check(`card ${card.id} inverted face`, card.inverted.effects, card.inverted.deferredReason)
  }
  for (const gift of config.gifts.definitions) {
    check(`gift ${gift.id}`, gift.effects, gift.deferredReason)
  }
  for (const building of config.empire.buildings) {
    for (const level of building.levels) {
      check(`building ${building.id} level ${level.level}`, level.effects ?? [], building.deferredReason)
    }
  }
  for (const technology of config.empire.technologies) {
    check(`technology ${technology.id}`, technology.effects, technology.deferredReason)
    for (const side of technology.sides?.definitions ?? []) {
      check(
        `technology ${technology.id} side ${side.id}`,
        side.effects,
        technology.deferredReason,
      )
    }
  }
  for (const event of config.empire.events) {
    for (const choice of event.choices) {
      check(
        `event ${event.id} choice ${choice.id}`,
        choice.effects,
        event.deferredReason || choice.deferredReason,
      )
    }
  }
  for (const quest of config.quests.definitions) {
    for (const stage of quest.stages) {
      for (const node of stage.nodes) {
        for (const choice of node.choices) {
          check(
            `quest ${quest.id} choice ${choice.id}`,
            choice.effects ?? [],
            quest.deferredReason || choice.deferredReason,
          )
        }
      }
    }
  }
  for (const offer of config.empire.externalEconomy.offers) {
    check(`external offer ${offer.id} decline effects`, offer.declineEffects)
  }
  for (const recipe of config.alchemy.recipes) {
    check(`alchemy recipe ${recipe.id}`, recipe.rewards, recipe.deferredReason)
  }
  return errors
}

function validateScaffoldSection(
  value: unknown,
  path: string,
  catalogKeys: readonly string[],
): void {
  if (!isRecord(value)) throw new Error(`${path} must be an object.`)
  if (typeof value.enabled !== 'boolean') throw new Error(`${path}.enabled must be a boolean.`)
  for (const key of catalogKeys) {
    if (!Array.isArray(value[key])) throw new Error(`${path}.${key} must be an array.`)
    if (value.enabled && value[key].length === 0) {
      throw new Error(`${path}.${key} must not be empty when ${path}.enabled is true.`)
    }
  }
}

function validateSeasonsConfig(config: EmpiresEndgameConfig): void {
  const seasons = config.empire.seasons
  if (!isRecord(seasons) || typeof seasons.enabled !== 'boolean') {
    throw new Error('empire.seasons must be an object with an enabled flag.')
  }
  if (!Array.isArray(seasons.definitions)) {
    throw new Error('empire.seasons.definitions must be an array.')
  }
  if (!['floor', 'round', 'none'].includes(seasons.foodRounding)) {
    throw new Error('empire.seasons.foodRounding must be floor, round, or none.')
  }
  if (seasons.enabled && seasons.definitions.length === 0) {
    throw new Error('empire.seasons.definitions must not be empty when seasons are enabled.')
  }
  const seasonIds = new Set<string>()
  for (const definition of seasons.definitions) {
    if (!definition.id?.trim() || seasonIds.has(definition.id)) {
      throw new Error('empire.seasons definitions need unique non-empty ids.')
    }
    if (!definition.name?.trim()) throw new Error(`season ${definition.id} needs a name.`)
    if (!Number.isInteger(definition.durationCons) || definition.durationCons < 1) {
      throw new Error(`season ${definition.id} durationCons must be a positive integer.`)
    }
    if (!Number.isFinite(definition.foodProductionMultiplier)
      || definition.foodProductionMultiplier < 0) {
      throw new Error(`season ${definition.id} foodProductionMultiplier must be non-negative.`)
    }
    seasonIds.add(definition.id)
  }
  if (seasons.greenhouse !== null) {
    if (!isRecord(seasons.greenhouse)
      || typeof seasons.greenhouse.technologyId !== 'string'
      || !config.empire.technologies.some(
        technology => technology.id === seasons.greenhouse!.technologyId,
      )) {
      throw new Error('empire.seasons.greenhouse must reference a known technology.')
    }
    if (!Number.isFinite(seasons.greenhouse.equalizedFoodProductionMultiplier)
      || seasons.greenhouse.equalizedFoodProductionMultiplier < 0) {
      throw new Error('empire.seasons greenhouse multiplier must be non-negative.')
    }
  }
}

function validateTechnologySidesAndHiddenCombinations(config: EmpiresEndgameConfig): void {
  const combinations = config.empire.hiddenCombinations
  if (!isRecord(combinations) || typeof combinations.enabled !== 'boolean'
    || !Array.isArray(combinations.definitions)) {
    throw new Error('empire.hiddenCombinations must contain enabled and definitions.')
  }
  if (combinations.enabled && combinations.definitions.length === 0) {
    throw new Error('empire.hiddenCombinations needs definitions when enabled.')
  }
  const technologyIds = new Set(config.empire.technologies.map(technology => technology.id))
  const buildingIds = new Set(config.empire.buildings.map(building => building.id))
  const combinationIds = new Set<string>()
  for (const combination of combinations.definitions) {
    if (!combination.id?.trim() || combinationIds.has(combination.id)) {
      throw new Error('hidden combinations need unique non-empty ids.')
    }
    if (!combination.name?.trim() || !Array.isArray(combination.prerequisites)
      || combination.prerequisites.length === 0) {
      throw new Error(`hidden combination ${combination.id} needs a name and prerequisites.`)
    }
    for (const dependency of combination.prerequisites) {
      if (dependency.kind === 'technology' && !technologyIds.has(dependency.technologyId)) {
        throw new Error(`hidden combination ${combination.id} references unknown technology ${dependency.technologyId}.`)
      }
      if (dependency.kind === 'building' && !buildingIds.has(dependency.buildingId)) {
        throw new Error(`hidden combination ${combination.id} references unknown building ${dependency.buildingId}.`)
      }
      if (dependency.kind === 'flag' && !EMPIRES_LIVE_FLAG_ALLOWLIST.has(dependency.flagId)) {
        throw new Error(`hidden combination ${combination.id} uses unread flag ${dependency.flagId}.`)
      }
      if (dependency.kind === 'reputation' && !Number.isFinite(dependency.minimum)) {
        throw new Error(`hidden combination ${combination.id} has invalid reputation prerequisite.`)
      }
    }
    combinationIds.add(combination.id)
  }

  for (const technology of config.empire.technologies) {
    const sides = technology.sides
    if (sides === undefined) continue
    if (!isRecord(sides) || !isRecord(sides.selection) || !isRecord(sides.disclosure)
      || !Array.isArray(sides.definitions)) {
      throw new Error(`technology ${technology.id} sides must define selection, disclosure, and definitions.`)
    }
    if (sides.definitions.length !== 2
      || sides.definitions.filter(side => side.alignment === 'light').length !== 1
      || sides.definitions.filter(side => side.alignment === 'dark').length !== 1) {
      throw new Error(`technology ${technology.id} sides must contain exactly one light and one dark definition.`)
    }
    const sideIds = new Set<string>()
    for (const side of sides.definitions) {
      if (!side.id?.trim() || sideIds.has(side.id) || !side.name?.trim()) {
        throw new Error(`technology ${technology.id} sides need unique ids and names.`)
      }
      if (!Array.isArray(side.effects)) throw new Error(`technology ${technology.id} side ${side.id} needs effects.`)
      if (side.alignment === 'dark') {
        if (!Number.isFinite(side.reputationDelta) || (side.reputationDelta ?? 0) >= 0) {
          throw new Error(`technology ${technology.id} dark side ${side.id} needs a negative reputationDelta.`)
        }
      } else if (side.reputationDelta !== undefined && side.reputationDelta !== 0) {
        throw new Error(`technology ${technology.id} light side ${side.id} cannot change disclosure reputation.`)
      }
      if (side.tags !== undefined) validateUniqueStringList(side.tags, `technology ${technology.id} side ${side.id} tags`)
      if (side.epidemicPolicy !== undefined) {
        if (!isRecord(side.epidemicPolicy)
          || typeof side.epidemicPolicy.preventsIntercitySpread !== 'boolean'
          || !Number.isFinite(side.epidemicPolicy.withinCitySpeedMultiplier)
          || side.epidemicPolicy.withinCitySpeedMultiplier <= 0) {
          throw new Error(`technology ${technology.id} side ${side.id} has an invalid epidemicPolicy.`)
        }
      }
      sideIds.add(side.id)
    }
    if (sides.selection.kind === 'fixed') {
      if (typeof sides.selection.sideId !== 'string' || !sideIds.has(sides.selection.sideId)) {
        throw new Error(`technology ${technology.id} fixed side selection references an unknown side.`)
      }
    } else if (sides.selection.kind === 'weighted') {
      if (!Array.isArray(sides.selection.weights)
        || sides.selection.weights.length !== sideIds.size
        || new Set(sides.selection.weights.map(weight => weight.sideId)).size !== sideIds.size
        || sides.selection.weights.some(weight => !sideIds.has(weight.sideId)
          || !Number.isFinite(weight.weight) || weight.weight <= 0)) {
        throw new Error(`technology ${technology.id} weighted side selection must cover every side with positive weights.`)
      }
    } else {
      throw new Error(`technology ${technology.id} has an unknown side selection kind.`)
    }
    if (sides.disclosure.kind === 'afterCons') {
      if (!Number.isInteger(sides.disclosure.delayCons) || sides.disclosure.delayCons < 0) {
        throw new Error(`technology ${technology.id} side disclosure delayCons must be a non-negative integer.`)
      }
    } else if (sides.disclosure.kind === 'hiddenCombination') {
      if (!combinationIds.has(sides.disclosure.combinationId)) {
        throw new Error(`technology ${technology.id} side disclosure references unknown hidden combination ${sides.disclosure.combinationId}.`)
      }
    } else if (sides.disclosure.kind !== 'onResearch') {
      throw new Error(`technology ${technology.id} has an unknown side disclosure kind.`)
    }
  }
}

function validateEpidemicAndMedicalConfig(config: EmpiresEndgameConfig): void {
  const epidemics = config.empire.epidemics
  if (!isRecord(epidemics) || typeof epidemics.enabled !== 'boolean') {
    throw new Error('empire.epidemics must be an object with an enabled flag.')
  }
  if (!Number.isInteger(epidemics.rulesVersion) || epidemics.rulesVersion < 1) {
    throw new Error('empire.epidemics.rulesVersion must be a positive integer.')
  }
  if (!['floor', 'round'].includes(epidemics.populationRounding)
    || !['floor', 'round', 'none'].includes(epidemics.productionRounding)
    || epidemics.loyaltyRounding !== 'round') {
    throw new Error('empire.epidemics rounding modes are invalid.')
  }
  if (!Number.isInteger(epidemics.chronicleImpactEntriesPerEpidemic)
    || epidemics.chronicleImpactEntriesPerEpidemic < 1
    || !Number.isInteger(epidemics.maxSpreadTargetsPerSettlement)
    || epidemics.maxSpreadTargetsPerSettlement < 0) {
    throw new Error('empire.epidemics retention and spread limits are invalid.')
  }
  if (!Array.isArray(epidemics.definitions) || !Array.isArray(epidemics.protections)) {
    throw new Error('empire.epidemics definitions and protections must be arrays.')
  }
  if (epidemics.enabled && epidemics.definitions.length === 0) {
    throw new Error('empire.epidemics needs definitions when enabled.')
  }
  const classIds = new Set(config.empire.populationClasses.map(item => item.id))
  const definitionIds = new Set<string>()
  for (const definition of epidemics.definitions) {
    if (!definition.id?.trim() || definitionIds.has(definition.id) || !definition.name?.trim()) {
      throw new Error('epidemic definitions need unique ids and names.')
    }
    if (!['ignore', 'refresh'].includes(definition.duplicatePolicy)) {
      throw new Error(`epidemic ${definition.id} has an invalid duplicatePolicy.`)
    }
    if (!Array.isArray(definition.affectedClasses) || definition.affectedClasses.length === 0
      || new Set(definition.affectedClasses.map(item => item.populationClassId)).size
        !== definition.affectedClasses.length) {
      throw new Error(`epidemic ${definition.id} needs unique affected classes.`)
    }
    for (const item of definition.affectedClasses) {
      if (!classIds.has(item.populationClassId) || !Number.isFinite(item.weight) || item.weight <= 0) {
        throw new Error(`epidemic ${definition.id} has an invalid affected class ${item.populationClassId}.`)
      }
    }
    if (!Array.isArray(definition.stages) || definition.stages.length === 0
      || new Set(definition.stages.map(stage => stage.id)).size !== definition.stages.length) {
      throw new Error(`epidemic ${definition.id} needs unique stages.`)
    }
    for (const stage of definition.stages) {
      if (!stage.id?.trim() || !stage.name?.trim() || !Number.isFinite(stage.severity)
        || stage.severity < 0 || !Number.isInteger(stage.durationCons) || stage.durationCons < 1
        || !Number.isFinite(stage.populationLossPercent) || stage.populationLossPercent < 0
        || stage.populationLossPercent > 100
        || !Number.isFinite(stage.productionLossPercent) || stage.productionLossPercent < 0
        || stage.productionLossPercent > 100
        || !Number.isFinite(stage.loyaltyDelta)
        || !Number.isFinite(stage.spreadChance) || stage.spreadChance < 0 || stage.spreadChance > 1
        || typeof stage.recruitmentBlocked !== 'boolean'
        || !Array.isArray(stage.facilityLocks)
        || stage.facilityLocks.some(slot => !EMPIRES_BUILDING_SLOT_KINDS.has(slot))) {
        throw new Error(`epidemic ${definition.id} stage ${stage.id} is invalid.`)
      }
    }
    definitionIds.add(definition.id)
  }

  const buildingIds = new Set(config.empire.buildings.map(item => item.id))
  const protectionIds = new Set<string>()
  for (const protection of epidemics.protections) {
    if (!protection.id?.trim() || protectionIds.has(protection.id) || !protection.name?.trim()
      || !Array.isArray(protection.consequences) || protection.consequences.length === 0
      || new Set(protection.consequences).size !== protection.consequences.length
      || protection.consequences.some(item => !['population', 'production', 'loyalty', 'spread'].includes(item))) {
      throw new Error('epidemic protections need unique ids, names, and consequences.')
    }
    if (protection.source.kind === 'building') {
      if (!buildingIds.has(protection.source.buildingId)
        || !['city', 'empire'].includes(protection.source.scope)
        || !Number.isFinite(protection.source.multiplier)
        || protection.source.multiplier < 0 || protection.source.multiplier > 1) {
        throw new Error(`epidemic protection ${protection.id} has an invalid building source.`)
      }
    } else if (protection.source.kind === 'flag') {
      if (!protection.source.flagId?.trim()
        || !Number.isFinite(protection.source.reductionPercentPerPoint)
        || protection.source.reductionPercentPerPoint < 0
        || !Number.isFinite(protection.source.maximumReductionPercent)
        || protection.source.maximumReductionPercent < 0
        || protection.source.maximumReductionPercent > 100) {
        throw new Error(`epidemic protection ${protection.id} has an invalid flag source.`)
      }
    } else {
      throw new Error(`epidemic protection ${protection.id} has an unknown source.`)
    }
    protectionIds.add(protection.id)
  }

  const validateStart = (start: { definitionId?: string, origin?: unknown }, label: string) => {
    if (typeof start.definitionId !== 'string' || !definitionIds.has(start.definitionId)
      || !isRecord(start.origin) || typeof start.origin.kind !== 'string') {
      throw new Error(`${label} has an invalid epidemic start.`)
    }
    if (start.origin.kind === 'city') {
      if (typeof start.origin.cityId !== 'string'
        || !config.empire.cities.some(city => city.id === start.origin!.cityId)) {
        throw new Error(`${label} references an unknown epidemic origin city.`)
      }
    } else if (start.origin.kind === 'lowest-operational-building-city') {
      if (typeof start.origin.buildingId !== 'string' || !buildingIds.has(start.origin.buildingId)) {
        throw new Error(`${label} references an unknown epidemic origin building.`)
      }
    } else if (!['effect-target-city', 'lowest-accessible-city'].includes(start.origin.kind)) {
      throw new Error(`${label} has an unknown epidemic origin selector.`)
    }
  }
  const effectGroups: Array<[string, readonly EmpiresEffect[]]> = []
  for (const card of config.cards) {
    effectGroups.push([`card ${card.id} normal`, card.normal.effects])
    effectGroups.push([`card ${card.id} inverted`, card.inverted.effects])
  }
  for (const gift of config.gifts.definitions) effectGroups.push([`gift ${gift.id}`, gift.effects])
  for (const technology of config.empire.technologies) {
    effectGroups.push([`technology ${technology.id}`, technology.effects])
    for (const side of technology.sides?.definitions ?? []) {
      effectGroups.push([`technology ${technology.id} side ${side.id}`, side.effects])
    }
  }
  for (const event of config.empire.events) {
    if (event.epidemicTarget?.definitionIds?.some(id => !definitionIds.has(id))) {
      throw new Error(`event ${event.id} targets an unknown epidemic definition.`)
    }
    for (const choice of event.choices) {
      effectGroups.push([`event ${event.id} choice ${choice.id}`, choice.effects])
      if (choice.epidemicContainment && (
        !['open', 'sealed'].includes(choice.epidemicContainment.mode)
        || typeof choice.epidemicContainment.preventsIntercitySpread !== 'boolean'
        || !Number.isFinite(choice.epidemicContainment.localImpactMultiplier)
        || choice.epidemicContainment.localImpactMultiplier <= 0
      )) throw new Error(`event ${event.id} choice ${choice.id} has invalid epidemic containment.`)
    }
  }
  for (const offer of config.empire.externalEconomy.offers) {
    effectGroups.push([`external offer ${offer.id} decline`, offer.declineEffects])
  }
  for (const [label, effects] of effectGroups) {
    for (const effect of effects) if (effect.kind === 'epidemicStart') validateStart(effect, label)
  }
  for (const combination of config.empire.hiddenCombinations.definitions) {
    if (combination.epidemicStart) validateStart(combination.epidemicStart, `hidden combination ${combination.id}`)
  }

  const medical = config.empire.medical
  if (!isRecord(medical) || typeof medical.enabled !== 'boolean') {
    throw new Error('empire.medical must be an object with an enabled flag.')
  }
  if (medical.enabled) {
    const unitIds = new Set((config.empire.units ?? []).map(item => item.id))
    if (!buildingIds.has(medical.hospitalBuildingId)
      || !buildingIds.has(medical.medicalAcademyBuildingId)
      || !unitIds.has(medical.healerUnitId)) {
      throw new Error('empire.medical must reference known Hospital, Medical Academy, and healer carriers.')
    }
  }
  if (!Number.isInteger(medical.defaultBattleRecoveryCons) || medical.defaultBattleRecoveryCons < 1
    || !Number.isInteger(medical.hospitalBattleRecoveryCons) || medical.hospitalBattleRecoveryCons < 1
    || medical.hospitalBattleRecoveryCons > medical.defaultBattleRecoveryCons
    || !Number.isInteger(medical.academyFreeResearchCadenceCons)
    || medical.academyFreeResearchCadenceCons < 1
    || !Number.isFinite(medical.academyTreatmentDeathChance)
    || medical.academyTreatmentDeathChance < 0 || medical.academyTreatmentDeathChance > 1) {
    throw new Error('empire.medical cadence, recovery, and treatment values are invalid.')
  }
}

function validateDomesticEconomyConfig(config: EmpiresEndgameConfig): void {
  const economy = config.empire.domesticEconomy
  if (!isRecord(economy) || typeof economy.enabled !== 'boolean') {
    throw new Error('empire.domesticEconomy must be an object with an enabled flag.')
  }
  if (!Number.isInteger(economy.historyRetention) || economy.historyRetention < 1) {
    throw new Error('empire.domesticEconomy.historyRetention must be a positive integer.')
  }
  if (!economy.enabled) return

  const resourceIds = new Set(config.empire.resources.map(resource => resource.id))
  const buildingById = new Map(config.empire.buildings.map(building => [building.id, building]))
  const technologyById = new Map(config.empire.technologies.map(technology => [technology.id, technology]))
  if (!resourceIds.has(economy.goldResourceId) || !resourceIds.has(economy.knowledgeResourceId)) {
    throw new Error('empire.domesticEconomy must reference known gold and knowledge resources.')
  }

  const liveBuilding = (buildingId: string, label: string) => {
    const building = buildingById.get(buildingId)
    if (!building || building.deferredReason) {
      throw new Error(`empire.domesticEconomy ${label} must reference a live building.`)
    }
  }
  liveBuilding(economy.loan.bankBuildingId, 'loan.bankBuildingId')
  liveBuilding(economy.insurance.buildingId, 'insurance.buildingId')
  liveBuilding(economy.fair.buildingId, 'fair.buildingId')
  liveBuilding(economy.temple.buildingId, 'temple.buildingId')
  liveBuilding(economy.tavern.buildingId, 'tavern.buildingId')
  if (technologyById.get(economy.loan.bankingTechnologyId)?.deferredReason
    || !technologyById.has(economy.loan.bankingTechnologyId)
    || technologyById.get(economy.fair.technologyId)?.deferredReason
    || !technologyById.has(economy.fair.technologyId)) {
    throw new Error('empire.domesticEconomy Bank and Fair technologies must be live.')
  }

  const loanNumbers = [
    economy.loan.principalIncomeTurns,
    economy.loan.termCons,
    economy.loan.paymentIncomeFraction,
    economy.loan.maxActiveLoans,
    economy.loan.persecutionKnowledgeLossPercent,
  ]
  if (loanNumbers.some(value => !Number.isFinite(value) || value <= 0)
    || !Number.isInteger(economy.loan.termCons)
    || !Number.isInteger(economy.loan.maxActiveLoans)
    || economy.loan.paymentIncomeFraction * economy.loan.termCons
      < economy.loan.principalIncomeTurns
    || economy.loan.persecutionKnowledgeLossPercent > 100
    || !Number.isFinite(economy.loan.defaultReputationDelta)
    || !Number.isFinite(economy.loan.defaultLoyaltyDelta)
    || !Number.isFinite(economy.loan.persecutionReputationDelta)
    || !Number.isFinite(economy.loan.persecutionLoyaltyDelta)) {
    throw new Error('empire.domesticEconomy.loan has invalid schedule or consequence values.')
  }

  const incidentKinds = new Set(['epidemic', 'meteor', 'raid', 'nuclear', 'siege'])
  if (!Number.isInteger(economy.insurance.calmTurnsRequired)
    || economy.insurance.calmTurnsRequired < 1
    || !Number.isInteger(economy.insurance.activeDurationCons)
    || economy.insurance.activeDurationCons < 1
    || [
      economy.insurance.basePayoutGold,
      economy.insurance.payoutPerCalmTurnGold,
      economy.insurance.maximumPayoutGold,
    ].some(value => !Number.isFinite(value) || value < 0)
    || economy.insurance.maximumPayoutGold < economy.insurance.basePayoutGold
    || !Array.isArray(economy.insurance.coveredIncidentKinds)
    || economy.insurance.coveredIncidentKinds.length === 0
    || new Set(economy.insurance.coveredIncidentKinds).size
      !== economy.insurance.coveredIncidentKinds.length
    || economy.insurance.coveredIncidentKinds.some(kind => !incidentKinds.has(kind))) {
    throw new Error('empire.domesticEconomy.insurance has invalid cadence, payout, or incident kinds.')
  }
  if (!isRecord(economy.insurance.unsupportedIncidentReasons)) {
    throw new Error('empire.domesticEconomy.insurance.unsupportedIncidentReasons must be an object.')
  }
  for (const [kind, reason] of Object.entries(economy.insurance.unsupportedIncidentReasons)) {
    if (!incidentKinds.has(kind) || typeof reason !== 'string' || !reason.trim()
      || economy.insurance.coveredIncidentKinds.includes(kind as never)) {
      throw new Error(`empire.domesticEconomy insurance blocker ${kind} is invalid.`)
    }
  }

  if (!Array.isArray(economy.fair.actions) || economy.fair.actions.length === 0) {
    throw new Error('empire.domesticEconomy.fair.actions must not be empty.')
  }
  const fairActionIds = new Set<string>()
  for (const action of economy.fair.actions) {
    if (!action.id?.trim() || fairActionIds.has(action.id) || !action.name?.trim()
      || !Number.isFinite(action.goldCost) || action.goldCost < 0
      || !Number.isInteger(action.cooldownCons) || action.cooldownCons < 1
      || !Number.isInteger(action.durationCons) || action.durationCons < 1
      || !Number.isFinite(action.temporaryLoyaltyModifier)
      || !Number.isFinite(action.temporaryReputationModifier)
      || !Number.isFinite(action.perConLoyaltyDelta)
      || !Number.isFinite(action.perConReputationDelta)
      || !Number.isFinite(action.perConPopulationLoss) || action.perConPopulationLoss < 0
      || !Array.isArray(action.perConResourceLosses)
      || action.perConResourceLosses.some(cost => !resourceIds.has(cost.resourceId)
        || !Number.isFinite(cost.amount) || cost.amount < 0)
      || (action.lockBuildingId !== undefined && !buildingById.has(action.lockBuildingId))) {
      throw new Error(`empire.domesticEconomy Fair action ${action.id || '<missing>'} is invalid.`)
    }
    if (action.unlockAfterActionId && !fairActionIds.has(action.unlockAfterActionId)) {
      throw new Error(`empire.domesticEconomy Fair action ${action.id} must follow an earlier action.`)
    }
    fairActionIds.add(action.id)
  }
  if (!fairActionIds.has(economy.fair.baronUnlockActionId)) {
    throw new Error('empire.domesticEconomy.fair.baronUnlockActionId must reference an action.')
  }

  if (!Number.isInteger(economy.temple.preachingCooldownCons)
    || economy.temple.preachingCooldownCons < 1
    || !Number.isInteger(economy.temple.relicSlotsPerLevel)
    || economy.temple.relicSlotsPerLevel < 1
    || !Number.isFinite(economy.temple.minimumTitheGold)
    || economy.temple.minimumTitheGold < 0
    || !Number.isFinite(economy.temple.titheGoldPerPopulation)
    || economy.temple.titheGoldPerPopulation < 0
    || !Number.isFinite(economy.temple.preachingLoyaltyDelta)
    || !Number.isFinite(economy.temple.preachingReputationDelta)) {
    throw new Error('empire.domesticEconomy.temple has invalid slot, tithe, or preaching values.')
  }
  if (!Number.isFinite(economy.tavern.recruitmentCapacityPerLevel)
    || economy.tavern.recruitmentCapacityPerLevel < 0
    || !Number.isFinite(economy.tavern.moraleMaximumPerLevel)
    || economy.tavern.moraleMaximumPerLevel < 0) {
    throw new Error('empire.domesticEconomy.tavern passive values must be non-negative.')
  }
}

function validateTavernConfig(config: EmpiresEndgameConfig): void {
  const tavern = config.tavern
  if (!isRecord(tavern) || typeof tavern.enabled !== 'boolean') {
    throw new Error('tavern must be an object with an enabled flag.')
  }
  if (!Array.isArray(config.mysticCards)) throw new Error('mysticCards must be an array.')
  const mysticIds = new Set<string>()
  for (const card of config.mysticCards) {
    if (!card.id?.trim() || mysticIds.has(card.id) || !card.name?.trim()
      || card.owner !== 'player' || typeof card.startsInverted !== 'boolean'
      || !Number.isInteger(card.returnDelayCons) || card.returnDelayCons < 1
      || !card.normal?.title?.trim() || !card.inverted?.title?.trim()) {
      throw new Error(`mystic card ${card.id || '<missing>'} is invalid.`)
    }
    mysticIds.add(card.id)
  }
  if (!tavern.enabled) return
  const chanceValues = [
    tavern.spawn.firstRunChance,
    tavern.spawn.secondRunChance,
    tavern.spawn.laterRunChance,
    tavern.maria.encounterChance,
  ]
  if (!Number.isInteger(tavern.spawn.eligibleCon) || tavern.spawn.eligibleCon < 1
    || chanceValues.some(value => !Number.isFinite(value) || value < 0 || value > 1)
    || tavern.spawn.firstRunChance !== 0 || tavern.spawn.secondRunChance !== 1
    || !Number.isInteger(tavern.visitCooldownCons) || tavern.visitCooldownCons < 1
    || !Number.isInteger(tavern.maxCommands) || tavern.maxCommands < 1
    || !Number.isInteger(tavern.historyRetention) || tavern.historyRetention < 1) {
    throw new Error('tavern spawn, visit, command, or history rules are invalid.')
  }
  const building = config.empire.buildings.find(candidate => candidate.id === tavern.buildingId)
  if (!building || building.deferredReason) throw new Error('tavern must reference its live building carrier.')
  const unitIds = new Set((config.empire.units ?? []).filter(unit => !unit.deferredReason).map(unit => unit.id))
  const offerIds = new Set<string>()
  for (const offer of tavern.mercenaries.offers) {
    if (!offer.id?.trim() || offerIds.has(offer.id) || !offer.name?.trim()
      || !unitIds.has(offer.unitId) || !Number.isInteger(offer.count) || offer.count < 1
      || !Number.isFinite(offer.goldCost) || offer.goldCost < 0
      || !Number.isFinite(offer.weight) || offer.weight <= 0
      || typeof offer.spiritsEligible !== 'boolean') {
      throw new Error(`tavern mercenary offer ${offer.id || '<missing>'} is invalid.`)
    }
    offerIds.add(offer.id)
  }
  if (!Number.isInteger(tavern.mercenaries.baseOfferCount)
    || !Number.isInteger(tavern.mercenaries.spiritsOfferCount)
    || tavern.mercenaries.baseOfferCount < 1
    || tavern.mercenaries.spiritsOfferCount < tavern.mercenaries.baseOfferCount
    || tavern.mercenaries.spiritsOfferCount > tavern.mercenaries.offers.length) {
    throw new Error('tavern mercenary offer counts are invalid.')
  }
  if (!Number.isFinite(tavern.spirits.goldCost) || tavern.spirits.goldCost < 0
    || !Number.isInteger(tavern.spirits.activationDelayCons) || tavern.spirits.activationDelayCons < 1
    || !Number.isInteger(tavern.spirits.durationCons) || tavern.spirits.durationCons < 1
    || !Number.isFinite(tavern.spirits.cheapOfferMultiplier)
    || tavern.spirits.cheapOfferMultiplier <= 0 || tavern.spirits.cheapOfferMultiplier > 1
    || !Number.isFinite(tavern.rumors.goldCost) || tavern.rumors.goldCost < 0
    || !Number.isInteger(tavern.rumors.deckHintPosition) || tavern.rumors.deckHintPosition < 1
    || !tavern.rumors.fallbackText.trim()) {
    throw new Error('tavern spirits or rumor rules are invalid.')
  }
  const maria = config.cards.find(card => card.id === tavern.maria.standardCardDefinitionId)
  if (!maria || maria.suit !== 'spades' || maria.rank !== 'queen') {
    throw new Error('tavern Maria carrier must be the uniquely mapped standard queen of spades.')
  }
  if (!tavern.maria.title.trim() || !tavern.maria.description.trim()
    || !tavern.maria.encounterDeferredReason.trim()) {
    throw new Error('tavern Maria copy and explicit 2×2 blocker are required.')
  }
  if (!mysticIds.has(tavern.queen.mysticDefinitionId)
    || tavern.queen.comboRanks.join(',') !== '3,7,ace'
    || !Number.isInteger(tavern.queen.pulseEveryCons) || tavern.queen.pulseEveryCons < 1) {
    throw new Error('tavern Queen mystic, 3–7–Т combo, or pulse cadence is invalid.')
  }
}

function validateAlchemyConfig(config: EmpiresEndgameConfig): void {
  const alchemy = config.alchemy
  if (!isRecord(alchemy) || typeof alchemy.enabled !== 'boolean') {
    throw new Error('alchemy must be an object with an enabled flag.')
  }
  for (const [name, value] of Object.entries({
    tickMs: alchemy.tickMs,
    maxTicks: alchemy.maxTicks,
    maxCommands: alchemy.maxCommands,
    resultLogLimit: alchemy.resultLogLimit,
    maxCatchUpTicksPerFrame: alchemy.maxCatchUpTicksPerFrame,
  })) {
    if (!Number.isInteger(value) || value < 1) throw new Error(`alchemy.${name} must be a positive integer.`)
  }
  if (!Number.isFinite(alchemy.dayCost) || alchemy.dayCost < 0) {
    throw new Error('alchemy.dayCost must be finite and non-negative.')
  }
  if (!isRecord(alchemy.board)
    || !Number.isInteger(alchemy.board.width) || alchemy.board.width < 5
    || !Number.isInteger(alchemy.board.height) || alchemy.board.height < 5
    || !Number.isInteger(alchemy.board.centerX) || alchemy.board.centerX < 0
    || alchemy.board.centerX >= alchemy.board.width
    || !Number.isInteger(alchemy.board.centerY) || alchemy.board.centerY < 0
    || alchemy.board.centerY >= alchemy.board.height) {
    throw new Error('alchemy.board dimensions and center are invalid.')
  }
  if (!isRecord(alchemy.spawn)
    || !Number.isInteger(alchemy.spawn.minDelayTicks) || alchemy.spawn.minDelayTicks < 1
    || !Number.isInteger(alchemy.spawn.maxDelayTicks)
    || alchemy.spawn.maxDelayTicks < alchemy.spawn.minDelayTicks
    || !Number.isInteger(alchemy.spawn.baseMoveIntervalTicks)
    || alchemy.spawn.baseMoveIntervalTicks < 1
    || !Number.isFinite(alchemy.spawn.inwardSpeedMultiplier)
    || alchemy.spawn.inwardSpeedMultiplier < 1) {
    throw new Error('alchemy.spawn timing and sourced inward speed are invalid.')
  }
  if (!isRecord(alchemy.acceleration)
    || !Number.isFinite(alchemy.acceleration.baseSpeedPercent)
    || alchemy.acceleration.baseSpeedPercent <= 0
    || !Number.isFinite(alchemy.acceleration.stepPercent)
    || alchemy.acceleration.stepPercent <= 0
    || !Number.isInteger(alchemy.acceleration.piecesPerStep)
    || alchemy.acceleration.piecesPerStep < 1
    || !Number.isFinite(alchemy.acceleration.explosionThresholdPercent)
    || alchemy.acceleration.explosionThresholdPercent <= alchemy.acceleration.baseSpeedPercent
    || !['above', 'at-or-above'].includes(alchemy.acceleration.explosionBoundary)) {
    throw new Error('alchemy.acceleration progression and explosion boundary are invalid.')
  }
  if (!isRecord(alchemy.reagents)
    || Object.values(alchemy.reagents).some(value => !Number.isInteger(value) || value < 0)) {
    throw new Error('alchemy.reagents charges must be non-negative integers.')
  }
  if (!isRecord(alchemy.explosion)
    || typeof alchemy.explosion.epidemicDefinitionId !== 'string'
    || !Number.isFinite(alchemy.explosion.severityMultiplier)
    || alchemy.explosion.severityMultiplier <= 0
    || typeof alchemy.explosion.lockBuildingForCon !== 'boolean') {
    throw new Error('alchemy.explosion must define a typed epidemic and lock policy.')
  }
  if (!Array.isArray(alchemy.colors) || !Array.isArray(alchemy.pieces)
    || !Array.isArray(alchemy.recipes) || !Array.isArray(alchemy.deferredSubfeatures)) {
    throw new Error('alchemy colors, pieces, recipes, and deferredSubfeatures must be arrays.')
  }
  if (!alchemy.enabled) return
  const building = config.empire.buildings.find(candidate => candidate.id === alchemy.buildingId)
  if (!building || building.deferredReason) throw new Error('alchemy must reference a live building carrier.')
  if (building.deferredSubfeatures?.some(item => item.id === 'alchemyMinigame')) {
    throw new Error('live alchemy cannot retain the broad alchemyMinigame blocker.')
  }
  if (!config.empire.epidemics.definitions.some(definition => (
    definition.id === alchemy.explosion.epidemicDefinitionId
  ))) throw new Error('alchemy explosion references an unknown epidemic definition.')
  const validColors = new Set(['red', 'yellow', 'blue', 'green'])
  if (alchemy.colors.length === 0 || new Set(alchemy.colors).size !== alchemy.colors.length
    || alchemy.colors.some(color => !validColors.has(color))) {
    throw new Error('alchemy.colors must contain unique primary reagent colors.')
  }
  const pieceIds = new Set<string>()
  for (const piece of alchemy.pieces) {
    if (!piece.id?.trim() || pieceIds.has(piece.id) || !piece.name?.trim()
      || !Array.isArray(piece.cells) || piece.cells.length === 0
      || piece.cells.some(cell => !Number.isInteger(cell.x) || !Number.isInteger(cell.y))
      || new Set(piece.cells.map(cell => `${cell.x}:${cell.y}`)).size !== piece.cells.length) {
      throw new Error(`alchemy piece ${piece.id || '<missing>'} is invalid.`)
    }
    pieceIds.add(piece.id)
  }
  const recipeIds = new Set<string>()
  const buildingIds = new Set(config.empire.buildings.map(item => item.id))
  const technologyIds = new Set(config.empire.technologies.map(item => item.id))
  const inBoard = (cell: { x: number, y: number }) => Number.isInteger(cell.x)
    && Number.isInteger(cell.y) && cell.x >= 0 && cell.x < alchemy.board.width
    && cell.y >= 0 && cell.y < alchemy.board.height
  const validateDependencies = (dependencies: readonly EmpiresDependency[], recipeId: string) => {
    for (const dependency of dependencies) {
      if (dependency.kind === 'technology' && !technologyIds.has(dependency.technologyId)) {
        throw new Error(`alchemy recipe ${recipeId} references unknown technology ${dependency.technologyId}.`)
      }
      if (dependency.kind === 'building' && !buildingIds.has(dependency.buildingId)) {
        throw new Error(`alchemy recipe ${recipeId} references unknown building ${dependency.buildingId}.`)
      }
    }
  }
  for (const recipe of alchemy.recipes) {
    if (!recipe.id?.trim() || recipeIds.has(recipe.id) || !recipe.name?.trim()
      || !recipe.description?.trim() || !['assembly', 'disassembly'].includes(recipe.mode)
      || !['experiment', 'medicine', 'poison'].includes(recipe.family)
      || !Array.isArray(recipe.initialCells) || recipe.initialCells.length === 0
      || !Array.isArray(recipe.targetCells) || recipe.targetCells.length === 0
      || recipe.initialCells.some(cell => !inBoard(cell) || !['red', 'yellow', 'blue', 'green', 'gray'].includes(cell.color))
      || recipe.targetCells.some(cell => !inBoard(cell)
        || cell.color !== undefined && !validColors.has(cell.color))
      || new Set(recipe.initialCells.map(cell => `${cell.x}:${cell.y}`)).size !== recipe.initialCells.length
      || new Set(recipe.targetCells.map(cell => `${cell.x}:${cell.y}`)).size !== recipe.targetCells.length
      || !Array.isArray(recipe.pieceDefinitionIds) || recipe.pieceDefinitionIds.length === 0
      || new Set(recipe.pieceDefinitionIds).size !== recipe.pieceDefinitionIds.length
      || recipe.pieceDefinitionIds.some(id => !pieceIds.has(id))
      || !Array.isArray(recipe.prerequisites) || !Array.isArray(recipe.rewards)) {
      throw new Error(`alchemy recipe ${recipe.id || '<missing>'} is invalid.`)
    }
    recipeIds.add(recipe.id)
    validateDependencies(recipe.prerequisites, recipe.id)
  }
  if (alchemy.recipes.every(recipe => Boolean(recipe.deferredReason))) {
    throw new Error('enabled alchemy requires at least one live recipe or experiment.')
  }
}

function validateExpeditionsConfig(config: EmpiresEndgameConfig): void {
  const rules = config.expeditions
  if (!rules || typeof rules.enabled !== 'boolean'
    || !Array.isArray(rules.zones)
    || !Array.isArray(rules.enemyProfiles)
    || !Array.isArray(rules.definitions)) {
    throw new Error('expeditions must contain enabled, zones, enemyProfiles, and definitions.')
  }
  if (!Number.isInteger(rules.resultHistoryRetention) || rules.resultHistoryRetention < 1) {
    throw new Error('expeditions.resultHistoryRetention must be a positive integer.')
  }
  if (rules.timeModel !== 'preparation-days-and-abstract-travel-cons') {
    throw new Error('expeditions.timeModel is unknown.')
  }
  if (!Number.isFinite(rules.veteran.qualifyingMaximumHealthRatio)
    || rules.veteran.qualifyingMaximumHealthRatio <= 0
    || rules.veteran.qualifyingMaximumHealthRatio > 1
    || !Number.isInteger(rules.veteran.removalWounds)
    || rules.veteran.removalWounds < 2
    || rules.veteran.laterBattleBonus !== null
    || !rules.veteran.laterBattleBonusDeferredReason?.trim()) {
    throw new Error('expeditions.veteran must define the sourced threshold/removal rule and an explicit missing bonus.')
  }

  const regionIds = new Set(config.empire.map.regions.map(region => region.id))
  const subregions = new Map(config.empire.map.subregions.map(subregion => [subregion.id, subregion]))
  const resourceIds = new Set(config.empire.resources.map(resource => resource.id))
  const unitIds = new Set((config.empire.units ?? []).map(unit => unit.id))
  const armorClassIds = new Set(config.combat.armorClasses.map(armor => armor.id))
  const waveIds = new Set(config.td.waves.map(wave => wave.id))
  const variants = new Map(config.td.planVariants.map(variant => [variant.id, variant]))
  const questIds = new Set(config.quests.definitions.map(quest => quest.id))
  const mapObjects = new Map(config.empire.map.objects.map(object => [object.id, object]))
  const liveTdExpeditions = rules.enabled && config.td.enabled
  if (mapObjects.size !== config.empire.map.objects.length) throw new Error('map object ids must be unique.')

  const zoneIds = new Set<string>()
  for (const zone of rules.zones) {
    if (!zone.id?.trim() || zoneIds.has(zone.id) || !zone.name?.trim() || !regionIds.has(zone.regionId)) {
      throw new Error(`expedition zone ${zone.id || '<missing>'} is invalid or repeated.`)
    }
    zoneIds.add(zone.id)
    if (new Set(zone.subregionIds).size !== zone.subregionIds.length
      || zone.subregionIds.some(id => subregions.get(id)?.regionId !== zone.regionId)) {
      throw new Error(`expedition zone ${zone.id} references an unknown or wrong-region subregion.`)
    }
    for (const effect of zone.rewards) {
      if (effect.kind === 'resource' && !resourceIds.has(effect.resourceId)) {
        throw new Error(`expedition zone ${zone.id} reward references unknown resource ${effect.resourceId}.`)
      }
    }
  }

  const profileIds = new Set<string>()
  for (const profile of rules.enemyProfiles) {
    if (!profile.id?.trim() || profileIds.has(profile.id) || !profile.name?.trim()
      || !profile.description?.trim() || !regionIds.has(profile.regionId)
      || liveTdExpeditions && !waveIds.has(profile.waveId)) {
      throw new Error(`expedition enemy profile ${profile.id || '<missing>'} is invalid or dangling.`)
    }
    profileIds.add(profile.id)
  }

  const expeditionIds = new Set<string>()
  for (const expedition of rules.definitions) {
    if (!expedition.id?.trim() || expeditionIds.has(expedition.id) || !expedition.name?.trim()) {
      throw new Error(`expedition ${expedition.id || '<missing>'} is invalid or repeated.`)
    }
    expeditionIds.add(expedition.id)
    const fort = mapObjects.get(expedition.fortObjectId)
    const variant = variants.get(expedition.tdVariantId)
    const profile = rules.enemyProfiles.find(candidate => candidate.id === expedition.enemyProfileId)
    if (!fort || fort.kind !== 'fortress'
      || fort.payload.expeditionId !== expedition.id
      || fort.payload.zoneId !== expedition.zoneId
      || fort.payload.deferredReason) {
      throw new Error(`expedition ${expedition.id} must own one live typed fortress payload.`)
    }
    if (!zoneIds.has(expedition.zoneId)
      || !regionIds.has(expedition.originRegionId)
      || !regionIds.has(expedition.targetRegionId)
      || !profile
      || liveTdExpeditions && (!variant
        || variant.mode !== 'assault'
        || variant.purpose !== 'expedition'
        || profile.waveId !== variant.waveId)
      || profile.regionId !== expedition.targetRegionId) {
      throw new Error(`expedition ${expedition.id} has a dangling zone, region, profile, or TD assault reference.`)
    }
    if (expedition.triggerQuestId && !questIds.has(expedition.triggerQuestId)) {
      throw new Error(`expedition ${expedition.id} references unknown trigger quest ${expedition.triggerQuestId}.`)
    }
    const complaintQuest = config.quests.definitions.find(quest => quest.id === expedition.complaint.questId)
    if (!complaintQuest || complaintQuest.trigger.kind !== 'manual') {
      throw new Error(`expedition ${expedition.id} complaint must reference a manual quest.`)
    }
    const expectedStages = ['planning', 'provisioning', 'assault', 'settlement']
    if (expedition.stages.join('|') !== expectedStages.join('|')) {
      throw new Error(`expedition ${expedition.id} must use the canonical four-stage funnel.`)
    }
    if (new Set(expedition.eligibleUnitIds).size !== expedition.eligibleUnitIds.length
      || expedition.eligibleUnitIds.some(id => !unitIds.has(id))
      || new Set(expedition.excludedArmorClassIds).size !== expedition.excludedArmorClassIds.length
      || expedition.excludedArmorClassIds.some(id => !armorClassIds.has(id))
      || new Set(expedition.armorExceptionUnitIds).size !== expedition.armorExceptionUnitIds.length
      || expedition.armorExceptionUnitIds.some(id => !unitIds.has(id))) {
      throw new Error(`expedition ${expedition.id} has invalid roster rules.`)
    }
    if (!Number.isInteger(expedition.baseDurationCons) || expedition.baseDurationCons < 1
      || !Number.isInteger(expedition.preparationDays) || expedition.preparationDays < 0
      || !resourceIds.has(expedition.provisionResourceId)
      || !Number.isFinite(expedition.minimumProvisionFraction)
      || expedition.minimumProvisionFraction < 0 || expedition.minimumProvisionFraction > 1
      || !Number.isFinite(expedition.fullProvisionDeathChance)
      || !Number.isFinite(expedition.emptyProvisionDeathChance)
      || expedition.fullProvisionDeathChance < 0
      || expedition.emptyProvisionDeathChance > 1
      || expedition.fullProvisionDeathChance > expedition.emptyProvisionDeathChance) {
      throw new Error(`expedition ${expedition.id} has invalid duration or provision rules.`)
    }
    if (!Number.isInteger(expedition.complaint.launches) || expedition.complaint.launches < 1
      || !Number.isInteger(expedition.complaint.windowCons) || expedition.complaint.windowCons < 1
      || !Number.isFinite(expedition.complaint.loyaltyDelta)
      || [
        expedition.complaint.minimumProvisionFraction,
        expedition.complaint.minimumLossRatio,
      ].some(value => value !== null && (!Number.isFinite(value) || value < 0 || value > 1))
      || (expedition.complaint.minimumDurationCons !== null
        && (!Number.isInteger(expedition.complaint.minimumDurationCons)
          || expedition.complaint.minimumDurationCons < 1))) {
      throw new Error(`expedition ${expedition.id} has invalid complaint criteria.`)
    }
    if (expedition.returnProvisionPolicy !== 'none' || expedition.repeatable) {
      throw new Error(`expedition ${expedition.id} has unsupported return or repeat semantics.`)
    }
  }

  for (const object of config.empire.map.objects) {
    if (object.kind !== 'fortress') continue
    if (object.payload.kind !== 'fortress') throw new Error(`fortress ${object.id} needs a typed payload.`)
    if (object.payload.deferredReason) {
      if (object.payload.expeditionId !== null || object.payload.zoneId !== null) {
        throw new Error(`deferred fortress ${object.id} must not carry live references.`)
      }
      continue
    }
    if (!object.payload.expeditionId || !expeditionIds.has(object.payload.expeditionId)
      || !object.payload.zoneId || !zoneIds.has(object.payload.zoneId)) {
      throw new Error(`fortress ${object.id} has dangling expedition or zone references.`)
    }
  }
  if (rules.enabled && rules.definitions.filter(definition => !definition.deferredReason).length === 0) {
    throw new Error('enabled expeditions require a live definition.')
  }
}

function validateInventoryConfig(config: EmpiresEndgameConfig): void {
  const rules = config.inventory
  if (!rules || typeof rules.enabled !== 'boolean'
    || !Array.isArray(rules.itemDefinitions)
    || !Array.isArray(rules.deferredSubfeatures)) {
    throw new Error('inventory must contain enabled, itemDefinitions, and deferredSubfeatures.')
  }
  if (!Number.isInteger(rules.tickMs) || rules.tickMs < 1
    || !Number.isInteger(rules.maxTicks) || rules.maxTicks < 1
    || !Number.isInteger(rules.maxCommands) || rules.maxCommands < 1
    || !Number.isInteger(rules.resultLogLimit) || rules.resultLogLimit < 1
    || !Number.isInteger(rules.maxCatchUpTicksPerFrame) || rules.maxCatchUpTicksPerFrame < 1
    || !Number.isInteger(rules.maxItems) || rules.maxItems < 1
    || !Number.isFinite(rules.targetUnitsPerItem) || rules.targetUnitsPerItem <= 0) {
    throw new Error('inventory timing, command, retention, item, and catch-up limits must be positive.')
  }
  if (!Number.isInteger(rules.board.width) || rules.board.width < 4
    || !Number.isInteger(rules.board.height) || rules.board.height < 6
    || !Number.isInteger(rules.board.cartHeight) || rules.board.cartHeight < 2
    || rules.board.cartHeight >= rules.board.height) {
    throw new Error('inventory board and cart dimensions are invalid.')
  }
  if (!Number.isInteger(rules.gravity.intervalTicks) || rules.gravity.intervalTicks < 1
    || !Number.isInteger(rules.gravity.spawnDelayTicks) || rules.gravity.spawnDelayTicks < 0) {
    throw new Error('inventory gravity settings are invalid.')
  }
  if (!Number.isFinite(rules.scoring.pointsPerWeight) || rules.scoring.pointsPerWeight < 0
    || !Number.isFinite(rules.scoring.fullRowBonus) || rules.scoring.fullRowBonus < 0
    || rules.skipPolicy !== 'direct-provision'
    || rules.abortPolicy !== 'abort-expedition') {
    throw new Error('inventory scoring, skip, or abort policy is invalid.')
  }
  const resourceIds = new Set(config.empire.resources.map(resource => resource.id))
  const equipmentIds = new Set(config.combat.equipment.map(equipment => equipment.id))
  const definitionIds = new Set<string>()
  for (const definition of rules.itemDefinitions) {
    if (!definition.id?.trim() || definitionIds.has(definition.id) || !definition.name?.trim()
      || !Number.isInteger(definition.weight) || definition.weight < 1
      || !Array.isArray(definition.cells) || definition.cells.length === 0
      || definition.cells.some(cell => !Number.isInteger(cell.x) || !Number.isInteger(cell.y))
      || new Set(definition.cells.map(cell => `${cell.x}:${cell.y}`)).size !== definition.cells.length) {
      throw new Error(`inventory item ${definition.id || '<missing>'} is invalid or repeated.`)
    }
    definitionIds.add(definition.id)
    const width = Math.max(...definition.cells.map(cell => cell.x))
      - Math.min(...definition.cells.map(cell => cell.x)) + 1
    const height = Math.max(...definition.cells.map(cell => cell.y))
      - Math.min(...definition.cells.map(cell => cell.y)) + 1
    if (width > rules.board.width || height > rules.board.cartHeight) {
      throw new Error(`inventory item ${definition.id} cannot fit inside the configured cart.`)
    }
    if (definition.content.kind === 'resource') {
      if (!resourceIds.has(definition.content.resourceId)) {
        throw new Error(`inventory item ${definition.id} references unknown resource ${definition.content.resourceId}.`)
      }
    } else if (definition.content.kind === 'equipment') {
      if (!equipmentIds.has(definition.content.equipmentId)) {
        throw new Error(`inventory item ${definition.id} references unknown equipment ${definition.content.equipmentId}.`)
      }
    } else {
      throw new Error(`inventory item ${definition.id} has an unknown content kind.`)
    }
  }
  if (rules.enabled) {
    const liveResourceIds = new Set(rules.itemDefinitions
      .filter(definition => !definition.deferredReason && definition.content.kind === 'resource')
      .map(definition => definition.content.kind === 'resource' ? definition.content.resourceId : ''))
    for (const expedition of config.expeditions.definitions.filter(definition => !definition.deferredReason)) {
      if (!liveResourceIds.has(expedition.provisionResourceId)) {
        throw new Error(`inventory needs a live item for expedition resource ${expedition.provisionResourceId}.`)
      }
    }
  }
}

function validateExternalEconomyConfig(config: EmpiresEndgameConfig): void {
  const external = config.empire.externalEconomy
  if (!isRecord(external) || typeof external.enabled !== 'boolean') {
    throw new Error('empire.externalEconomy must be an object with an enabled flag.')
  }
  if (!Number.isInteger(external.historyRetention) || external.historyRetention < 1
    || !Number.isInteger(external.offerCadenceCons) || external.offerCadenceCons < 1
    || !Number.isInteger(external.offerLifetimeCons) || external.offerLifetimeCons < 1
    || !Number.isInteger(external.maxActiveOffers) || external.maxActiveOffers < 1
    || !Number.isFinite(external.persecutionPricePenaltyPercent)
    || external.persecutionPricePenaltyPercent < 0) {
    throw new Error('empire.externalEconomy cadence and retention values must be positive integers.')
  }
  if (!external.enabled) return

  const resourceIds = new Set(config.empire.resources.map(resource => resource.id))
  const regionIds = new Set(config.empire.map.regions.map(region => region.id))
  const cityIds = new Set(config.empire.cities.map(city => city.id))
  const buildingById = new Map(config.empire.buildings.map(building => [building.id, building]))
  const technologyById = new Map(config.empire.technologies.map(technology => [technology.id, technology]))
  const advisorIds = new Set(config.governance.advisors.map(advisor => advisor.id))
  const eventIds = new Set(config.empire.events.map(event => event.id))
  const unitIds = new Set((config.empire.units ?? []).map(unit => unit.id))
  if (external.actors.length === 0 || external.offers.length === 0) {
    throw new Error('enabled empire.externalEconomy requires actors and offers.')
  }
  const liveTechnology = (id: string, label: string) => {
    const technology = technologyById.get(id)
    if (!technology || technology.deferredReason) {
      throw new Error(`empire.externalEconomy ${label} must reference a live technology.`)
    }
    return technology
  }
  if (!resourceIds.has(external.goldResourceId) || !resourceIds.has(external.knowledgeResourceId)) {
    throw new Error('empire.externalEconomy must reference known gold, knowledge, and trade-route carriers.')
  }
  liveTechnology(external.tradeRoutesTechnologyId, 'tradeRoutesTechnologyId')
  const compass = liveTechnology(external.transfer.compassTechnologyId, 'transfer.compassTechnologyId')
  const merchantGuilds = liveTechnology(
    external.customs.merchantGuildsTechnologyId,
    'customs.merchantGuildsTechnologyId',
  )
  const validateDependencies = (dependencies: readonly EmpiresDependency[], label: string) => {
    for (const dependency of dependencies) {
      if (dependency.kind === 'technology' && !technologyById.has(dependency.technologyId)) {
        throw new Error(`${label} references unknown technology ${dependency.technologyId}.`)
      }
      if (dependency.kind === 'building' && (
        !buildingById.has(dependency.buildingId)
        || !Number.isInteger(dependency.level) || dependency.level < 1
        || (dependency.scope !== undefined && !['sameCity', 'anyCity'].includes(dependency.scope))
      )) throw new Error(`${label} has an invalid building prerequisite.`)
      if (dependency.kind === 'flag' && (
        !dependency.flagId?.trim() || !EMPIRES_LIVE_FLAG_ALLOWLIST.has(dependency.flagId)
        || !Number.isFinite(dependency.minimum)
      )) throw new Error(`${label} has an invalid flag prerequisite.`)
      if (dependency.kind === 'reputation' && !Number.isFinite(dependency.minimum)) {
        throw new Error(`${label} has an invalid reputation prerequisite.`)
      }
      if (dependency.kind === 'advisor' && !advisorIds.has(dependency.advisorId)) {
        throw new Error(`${label} references unknown advisor ${dependency.advisorId}.`)
      }
    }
  }
  const validateDeclineEffects = (effects: readonly EmpiresEffect[], label: string) => {
    const knownKinds = new Set([
      'resource', 'resourceMultiplier', 'time', 'foodProduction', 'population', 'loyalty',
      'loyaltyAllCities', 'classLoyalty', 'reputation', 'flag', 'epidemicStart',
    ])
    for (const effect of effects) {
      if (!knownKinds.has(effect.kind)) throw new Error(`${label} has an unknown effect kind.`)
      if ((effect.kind === 'resource' || effect.kind === 'resourceMultiplier')
        && !resourceIds.has(effect.resourceId)) {
        throw new Error(`${label} references unknown resource ${effect.resourceId}.`)
      }
      if ((effect.kind === 'foodProduction' || effect.kind === 'population')
        && effect.cityId !== undefined && !cityIds.has(effect.cityId)) {
        throw new Error(`${label} references unknown city ${effect.cityId}.`)
      }
    }
  }
  const actorIds = new Set<string>()
  for (const actor of external.actors) {
    if (!actor.id?.trim() || actorIds.has(actor.id) || !actor.name?.trim()
      || !['hostile', 'neutral', 'allied'].includes(actor.initialRelationship)
      || actor.accessibleRegionIds.length === 0
      || new Set(actor.accessibleRegionIds).size !== actor.accessibleRegionIds.length
      || actor.accessibleRegionIds.some(regionId => !regionIds.has(regionId))) {
      throw new Error(`empire.externalEconomy actor ${actor.id || '<missing>'} is invalid.`)
    }
    actorIds.add(actor.id)
  }
  const offerIds = new Set<string>()
  for (const offer of external.offers) {
    if (!offer.id?.trim() || offerIds.has(offer.id) || !offer.name?.trim()
      || !actorIds.has(offer.actorId) || !resourceIds.has(offer.resourceId)
      || !['import', 'export'].includes(offer.direction)
      || !Number.isFinite(offer.resourceAmount) || offer.resourceAmount <= 0
      || !Number.isFinite(offer.goldAmount) || offer.goldAmount <= 0
      || !Number.isFinite(offer.weight) || offer.weight <= 0
      || !Number.isInteger(offer.stock) || offer.stock < 1
      || offer.relationships.length === 0
      || new Set(offer.relationships).size !== offer.relationships.length
      || offer.relationships.some(value => !['hostile', 'neutral', 'allied'].includes(value))
      || !Number.isFinite(offer.minimumReputation)
      || offer.minimumReputation < config.empire.loyalty.minimum
      || offer.minimumReputation > config.empire.loyalty.maximum
      || (offer.minimumCon !== undefined && (!Number.isInteger(offer.minimumCon) || offer.minimumCon < 1))
      || (offer.maximumCon !== undefined && (!Number.isInteger(offer.maximumCon) || offer.maximumCon < 1))
      || (offer.minimumCon ?? Number.NEGATIVE_INFINITY) > (offer.maximumCon ?? Number.POSITIVE_INFINITY)) {
      throw new Error(`empire.externalEconomy offer ${offer.id || '<missing>'} is invalid.`)
    }
    validateDependencies(offer.prerequisites, `external offer ${offer.id}`)
    validateDeclineEffects(offer.declineEffects, `external offer ${offer.id} decline effects`)
    offerIds.add(offer.id)
  }
  const unionIds = new Set<string>()
  for (const union of external.unions) {
    if (!union.id?.trim() || unionIds.has(union.id) || !union.name?.trim()
      || !actorIds.has(union.actorId) || !Number.isFinite(union.minimumReputation)
      || union.minimumReputation < config.empire.loyalty.minimum
      || union.minimumReputation > config.empire.loyalty.maximum
      || !['hostile', 'neutral', 'allied'].includes(union.minimumRelationship)) {
      throw new Error(`empire.externalEconomy union ${union.id || '<missing>'} is invalid.`)
    }
    validateDependencies(union.prerequisites, `external union ${union.id}`)
    unionIds.add(union.id)
  }

  const liveCarrier = (id: string, label: string) => {
    const building = buildingById.get(id)
    if (!building || building.deferredReason) {
      throw new Error(`empire.externalEconomy ${label} must reference a live building.`)
    }
    return building
  }
  const stable = liveCarrier(external.stable.buildingId, 'stable.buildingId')
  const customs = liveCarrier(external.customs.buildingId, 'customs.buildingId')
  const port = liveCarrier(external.seaPort.buildingId, 'seaPort.buildingId')
  if (port.slot !== 'maritime') throw new Error('Sea Port must use the dedicated maritime slot.')
  if (!buildingById.has(external.stable.farmBuildingId)
    || !resourceIds.has(external.stable.livestockResourceId)
    || external.stable.livestockRegionIds.length === 0
    || new Set(external.stable.livestockRegionIds).size !== external.stable.livestockRegionIds.length
    || external.stable.livestockRegionIds.some(regionId => !regionIds.has(regionId))
    || external.stable.mountedUnitIds.length === 0
    || new Set(external.stable.mountedUnitIds).size !== external.stable.mountedUnitIds.length
    || external.stable.mountedUnitIds.some(unitId => !unitIds.has(unitId))
    || stable.slot !== 'unique') {
    throw new Error('empire.externalEconomy Stable carriers are incomplete.')
  }
  const hasFlagEffect = (
    effects: readonly EmpiresEffect[],
    flagId: string,
  ) => effects.some(effect => effect.kind === 'flag' && effect.flagId === flagId && effect.amount > 0)
  if (!external.transfer.speedFlagId?.trim() || !external.customs.tariffFlagId?.trim()
    || !external.customs.merchantGuildsFlagId?.trim() || !external.stable.mountedFlagId?.trim()
    || !external.seaPort.capacityFlagId?.trim()
    || !Number.isFinite(external.transfer.baseTimeCostDays) || external.transfer.baseTimeCostDays <= 0
    || !Number.isFinite(external.customs.tariffPercentPerLevel)
    || !Number.isFinite(external.customs.merchantGuildTariffBonusPercent)
    || !Number.isInteger(external.seaPort.maximumAcrossEmpire)
    || external.seaPort.maximumAcrossEmpire < 1
    || !Number.isFinite(external.seaPort.tradeGoldBonusPercentPerLevel)
    || !Number.isFinite(external.seaPort.knowledgePerTradePerLevel)) {
    throw new Error('empire.externalEconomy transfer, Customs, or Sea Port values are invalid.')
  }
  if (!hasFlagEffect(compass.effects, external.transfer.speedFlagId)
    || !hasFlagEffect(merchantGuilds.effects, external.customs.merchantGuildsFlagId)
    || !customs.levels.some(level => hasFlagEffect(level.effects ?? [], external.customs.tariffFlagId))
    || !stable.levels.some(level => hasFlagEffect(level.effects ?? [], external.stable.mountedFlagId))
    || !port.levels.some(level => hasFlagEffect(level.effects ?? [], external.seaPort.capacityFlagId))
    || !stable.levels.some(level => level.dependencies.some(dependency => (
      dependency.kind === 'building'
      && dependency.buildingId === external.stable.farmBuildingId
      && dependency.scope === 'sameCity'
      && dependency.level >= 2
    )))
    || external.stable.mountedUnitIds.some((unitId) => {
      const unit = config.empire.units?.find(candidate => candidate.id === unitId)
      return !unit?.dependencies.some(dependency => (
        dependency.kind === 'building'
        && dependency.buildingId === external.stable.buildingId
        && dependency.scope === 'sameCity'
      )) || !unit.dependencies.some(dependency => (
        dependency.kind === 'flag' && dependency.flagId === external.stable.mountedFlagId
      ))
    })
    || !eventIds.has(external.customs.smugglingEventId)) {
    throw new Error('empire.externalEconomy carrier effects and prerequisites are not wired.')
  }
  const coastalCityIds = new Set(config.governance.governor.citySites
    .filter(site => site.coastal).map(site => site.cityId))
  for (const city of config.empire.cities) {
    const maritimeSlots = city.slots.filter(slot => slot.kind === 'maritime')
    if ((coastalCityIds.has(city.id) ? 1 : 0) !== maritimeSlots.length) {
      throw new Error(`city ${city.id} must expose exactly one maritime slot iff it is coastal.`)
    }
  }
  const reviewedIds = new Set<string>()
  for (const reviewed of external.reviewedAbsentBuildings) {
    if (!reviewed.id?.trim() || reviewedIds.has(reviewed.id) || !reviewed.name?.trim()
      || !reviewed.reason?.trim() || buildingById.has(reviewed.id)) {
      throw new Error(`empire.externalEconomy reviewed absent building ${reviewed.id || '<missing>'} is invalid.`)
    }
    reviewedIds.add(reviewed.id)
  }
}

function validateEconomyContentConfig(config: EmpiresEndgameConfig): void {
  const content = config.empire.economyContent
  if (!isRecord(content) || typeof content.enabled !== 'boolean') {
    throw new Error('empire.economyContent must be an object with an enabled flag.')
  }
  if (!Number.isInteger(content.eventHistoryRetention) || content.eventHistoryRetention < 1) {
    throw new Error('empire.economyContent.eventHistoryRetention must be a positive integer.')
  }
  if (!content.enabled) return

  const eventById = new Map(config.empire.events.map(event => [event.id, event]))
  const buildingIds = new Set(config.empire.buildings
    .filter(building => !building.deferredReason)
    .map(building => building.id))
  const resourceIds = new Set(config.empire.resources.map(resource => resource.id))
  const classIds = new Set(config.empire.populationClasses.map(item => item.id))
  const liveChoice = (eventId: string, choiceId: string, label: string) => {
    const event = eventById.get(eventId)
    const choice = event?.choices.find(item => item.id === choiceId)
    if (!event || event.deferredReason || !choice || choice.deferredReason) {
      throw new Error(`empire.economyContent ${label} must reference a live event choice.`)
    }
  }

  const smuggling = content.smuggling
  if (smuggling.eventId !== config.empire.externalEconomy.customs.smugglingEventId
    || !Number.isInteger(smuggling.durationCons) || smuggling.durationCons < 1
    || [
      smuggling.stopCustomsIncomeMultiplier,
      smuggling.taxCustomsIncomeMultiplier,
      smuggling.stopPopulationGrowth,
      smuggling.taxPopulationGrowth,
    ].some(value => !Number.isFinite(value))
    || smuggling.stopCustomsIncomeMultiplier < 0
    || smuggling.taxCustomsIncomeMultiplier < 0) {
    throw new Error('empire.economyContent.smuggling has invalid carriers or values.')
  }
  liveChoice(smuggling.eventId, smuggling.stopChoiceId, 'smuggling.stopChoiceId')
  liveChoice(smuggling.eventId, smuggling.taxChoiceId, 'smuggling.taxChoiceId')

  const horseTheft = content.horseTheft
  if (!buildingIds.has(horseTheft.stableBuildingId)
    || horseTheft.stableBuildingId !== config.empire.externalEconomy.stable.buildingId
    || !resourceIds.has(horseTheft.livestockResourceId)
    || horseTheft.livestockResourceId !== config.empire.externalEconomy.stable.livestockResourceId
    || !classIds.has(horseTheft.noblePopulationClassId)
    || !Number.isInteger(horseTheft.recurrenceCooldownCons)
    || horseTheft.recurrenceCooldownCons < 1
    || !Number.isFinite(horseTheft.enemyYieldPerCon)
    || horseTheft.enemyYieldPerCon < 0) {
    throw new Error('empire.economyContent.horseTheft has invalid carriers or values.')
  }
  liveChoice(horseTheft.eventId, horseTheft.huntChoiceId, 'horseTheft.huntChoiceId')
  liveChoice(horseTheft.eventId, horseTheft.dealChoiceId, 'horseTheft.dealChoiceId')
  liveChoice(horseTheft.eventId, horseTheft.ignoreChoiceId, 'horseTheft.ignoreChoiceId')

  const insurance = content.insurance
  if (!buildingIds.has(insurance.buildingId)
    || insurance.buildingId !== config.empire.domesticEconomy.insurance.buildingId) {
    throw new Error('empire.economyContent.insurance must reference the live insurance building.')
  }
  liveChoice(insurance.eventId, insurance.acceptChoiceId, 'insurance.acceptChoiceId')
  liveChoice(insurance.eventId, insurance.declineChoiceId, 'insurance.declineChoiceId')

  const card = config.cards.find(definition => definition.id === 'card-diamonds-ace')
  const tradeFlags = new Set(card?.inverted.effects.flatMap(effect => (
    effect.kind === 'flag' ? [effect.flagId] : []
  )) ?? [])
  if (card?.inverted.deferredReason
    || !content.tradeCard.externalTradeDisabledFlagId.trim()
    || !content.tradeCard.internalTradeOnlyFlagId.trim()
    || !tradeFlags.has(content.tradeCard.externalTradeDisabledFlagId)
    || !tradeFlags.has(content.tradeCard.internalTradeOnlyFlagId)) {
    throw new Error('empire.economyContent.tradeCard must reference both live inverted ♦A flags.')
  }
}

function validateLoyaltyConfig(config: EmpiresEndgameConfig): void {
  const loyalty = config.empire.loyalty
  if (!isRecord(loyalty) || typeof loyalty.enabled !== 'boolean') {
    throw new Error('empire.loyalty must be an object with an enabled flag.')
  }
  for (const [path, value] of [
    ['minimum', loyalty.minimum],
    ['maximum', loyalty.maximum],
    ['initialCityLoyalty', loyalty.initialCityLoyalty],
    ['initialClassLoyalty', loyalty.initialClassLoyalty],
    ['initialReputation', loyalty.initialReputation],
    ['constructionMinimumLoyalty', loyalty.constructionMinimumLoyalty],
    ['recruitmentMinimumLoyalty', loyalty.recruitmentMinimumLoyalty],
  ] as const) {
    if (!Number.isFinite(value)) throw new Error(`empire.loyalty.${path} must be finite.`)
  }
  if (!Number.isInteger(loyalty.minimum)
    || !Number.isInteger(loyalty.maximum)
    || loyalty.minimum >= loyalty.maximum) {
    throw new Error('empire.loyalty bounds must be increasing integers.')
  }
  const inBounds = (value: number) => value >= loyalty.minimum && value <= loyalty.maximum
  for (const [path, value] of [
    ['initialCityLoyalty', loyalty.initialCityLoyalty],
    ['initialClassLoyalty', loyalty.initialClassLoyalty],
    ['initialReputation', loyalty.initialReputation],
    ['constructionMinimumLoyalty', loyalty.constructionMinimumLoyalty],
    ['recruitmentMinimumLoyalty', loyalty.recruitmentMinimumLoyalty],
  ] as const) {
    if (!inBounds(value)) throw new Error(`empire.loyalty.${path} must be within its bounds.`)
  }

  if (!Array.isArray(loyalty.workforceDivisors)) {
    throw new Error('empire.loyalty.workforceDivisors must be an array.')
  }
  const expectedLoyalties = Array.from(
    { length: loyalty.maximum - loyalty.minimum + 1 },
    (_, index) => loyalty.minimum + index,
  )
  const actualLoyalties = loyalty.workforceDivisors.map(entry => entry.loyalty)
  if (JSON.stringify(actualLoyalties) !== JSON.stringify(expectedLoyalties)) {
    throw new Error('empire.loyalty.workforceDivisors must contain every bound value in ascending order.')
  }
  for (const entry of loyalty.workforceDivisors) {
    if (!Number.isFinite(entry.divisor) || entry.divisor <= 0) {
      throw new Error(`empire.loyalty workforce divisor at ${entry.loyalty} must be positive.`)
    }
  }
  for (let index = 1; index < loyalty.workforceDivisors.length; index += 1) {
    if (loyalty.workforceDivisors[index].divisor > loyalty.workforceDivisors[index - 1].divisor) {
      throw new Error('empire.loyalty workforce divisors must not increase with loyalty.')
    }
  }

  const regionIds = new Set(config.empire.map.regions.map(region => region.id))
  if (!isRecord(loyalty.initialRegionLoyalty)) {
    throw new Error('empire.loyalty.initialRegionLoyalty must be an object.')
  }
  const initialRegionIds = Object.keys(loyalty.initialRegionLoyalty).sort()
  if (initialRegionIds.length !== regionIds.size
    || initialRegionIds.some(regionId => !regionIds.has(regionId))) {
    throw new Error('empire.loyalty.initialRegionLoyalty must cover every configured region exactly once.')
  }
  for (const [regionId, value] of Object.entries(loyalty.initialRegionLoyalty)) {
    if (!Number.isFinite(value) || !inBounds(value)) {
      throw new Error(`empire.loyalty initial value for ${regionId} must be within bounds.`)
    }
  }

  const rebellion = loyalty.rebellion
  if (!isRecord(rebellion)
    || !Number.isFinite(rebellion.threshold)
    || !Number.isFinite(rebellion.recoveryThreshold)
    || !inBounds(rebellion.threshold)
    || !inBounds(rebellion.recoveryThreshold)
    || rebellion.threshold >= rebellion.recoveryThreshold) {
    throw new Error('empire.loyalty.rebellion thresholds must be ordered within loyalty bounds.')
  }
  if (!Number.isInteger(rebellion.sustainedApplications)
    || rebellion.sustainedApplications < 1
    || !Number.isInteger(rebellion.sustainedRecoveryApplications)
    || rebellion.sustainedRecoveryApplications < 1) {
    throw new Error('empire.loyalty.rebellion durations must be positive integers.')
  }
  if (!Number.isInteger(loyalty.chronicleRetention) || loyalty.chronicleRetention < 1) {
    throw new Error('empire.loyalty.chronicleRetention must be a positive integer.')
  }

  if (!Array.isArray(loyalty.classGates)) throw new Error('empire.loyalty.classGates must be an array.')
  const buildingIds = new Set(config.empire.buildings.map(building => building.id))
  const classIds = new Set(config.empire.populationClasses.map(definition => definition.id))
  const gateIds = new Set<string>()
  const gateBuildings = new Set<string>()
  for (const gate of loyalty.classGates) {
    if (!gate.id?.trim() || gateIds.has(gate.id)) throw new Error('empire.loyalty class gates need unique ids.')
    if (!buildingIds.has(gate.buildingId)) {
      throw new Error(`empire.loyalty class gate ${gate.id} references unknown building ${gate.buildingId}.`)
    }
    if (!classIds.has(gate.populationClassId)) {
      throw new Error(`empire.loyalty class gate ${gate.id} references unknown class ${gate.populationClassId}.`)
    }
    if (!Number.isFinite(gate.minimumLoyalty) || !inBounds(gate.minimumLoyalty)) {
      throw new Error(`empire.loyalty class gate ${gate.id} has an out-of-bounds threshold.`)
    }
    if (gateBuildings.has(gate.buildingId)) {
      throw new Error(`empire.loyalty repeats a class gate for building ${gate.buildingId}.`)
    }
    gateIds.add(gate.id)
    gateBuildings.add(gate.buildingId)
  }
}

function validatePoliticalEffects(config: EmpiresEndgameConfig): void {
  const cityIds = new Set(config.empire.cities.map(city => city.id))
  const regionIds = new Set(config.empire.map.regions.map(region => region.id))
  const classIds = new Set(config.empire.populationClasses.map(definition => definition.id))
  const validateEffects = (effects: readonly EmpiresEffect[], path: string) => {
    effects.forEach((effect, index) => {
      if (effect.kind === 'reputation') {
        if (!Number.isFinite(effect.amount) || !Number.isFinite(effect.amountPerLevel ?? 0)) {
          throw new Error(`${path}[${index}] reputation amounts must be finite.`)
        }
        return
      }
      if (effect.kind === 'loyaltyAllCities') {
        if (!Number.isFinite(effect.amount) || !Number.isFinite(effect.amountPerLevel ?? 0)) {
          throw new Error(`${path}[${index}] all-city loyalty amounts must be finite.`)
        }
        return
      }
      if (effect.kind === 'classLoyalty') {
        if (!classIds.has(effect.populationClassId)) {
          throw new Error(`${path}[${index}] references unknown population class ${effect.populationClassId}.`)
        }
        if (!Number.isFinite(effect.amount) || !Number.isFinite(effect.amountPerLevel ?? 0)) {
          throw new Error(`${path}[${index}] class loyalty amounts must be finite.`)
        }
        return
      }
      if (effect.kind !== 'loyalty') return
      if (!Number.isFinite(effect.amount) || !Number.isFinite(effect.amountPerLevel ?? 0)) {
        throw new Error(`${path}[${index}] loyalty amounts must be finite.`)
      }
      const target = effect.target
      if (!isRecord(target) || typeof target.kind !== 'string') {
        throw new Error(`${path}[${index}] loyalty target must be typed.`)
      }
      if (target.kind === 'city' && (typeof target.cityId !== 'string' || !cityIds.has(target.cityId))) {
        throw new Error(`${path}[${index}] references unknown loyalty city ${String(target.cityId)}.`)
      } else if (target.kind === 'region' && (typeof target.regionId !== 'string' || !regionIds.has(target.regionId))) {
        throw new Error(`${path}[${index}] references unknown loyalty region ${String(target.regionId)}.`)
      } else if (target.kind === 'class' && (
        typeof target.cityId !== 'string'
        || !cityIds.has(target.cityId)
        || typeof target.populationClassId !== 'string'
        || !classIds.has(target.populationClassId)
      )) {
        throw new Error(`${path}[${index}] references an unknown city population class.`)
      } else if (!['city', 'region', 'class'].includes(target.kind)) {
        throw new Error(`${path}[${index}] has unknown loyalty target kind ${target.kind}.`)
      }
    })
  }
  for (const card of config.cards) {
    validateEffects(card.normal.effects, `card ${card.id} normal effects`)
    validateEffects(card.inverted.effects, `card ${card.id} inverted effects`)
  }
  for (const gift of config.gifts.definitions) validateEffects(gift.effects, `gift ${gift.id} effects`)
  for (const building of config.empire.buildings) {
    for (const level of building.levels) validateEffects(level.effects ?? [], `building ${building.id} level ${level.level} effects`)
  }
  for (const technology of config.empire.technologies) {
    validateEffects(technology.effects, `technology ${technology.id} effects`)
    for (const side of technology.sides?.definitions ?? []) {
      validateEffects(side.effects, `technology ${technology.id} side ${side.id} effects`)
    }
  }
  for (const event of config.empire.events) {
    for (const choice of event.choices) validateEffects(choice.effects, `event ${event.id} choice ${choice.id} effects`)
  }
  for (const offer of config.empire.externalEconomy.offers) {
    validateEffects(offer.declineEffects, `external offer ${offer.id} decline effects`)
  }
  for (const recipe of config.alchemy.recipes) {
    validateEffects(recipe.rewards, `alchemy recipe ${recipe.id} rewards`)
  }
}

function validateUniqueStringList(value: unknown, path: string): string[] {
  if (!Array.isArray(value)) throw new Error(`${path} must be an array.`)
  const result: string[] = []
  const seen = new Set<string>()
  for (const entry of value) {
    if (typeof entry !== 'string' || !entry.trim()) {
      throw new Error(`${path} entries must be non-empty strings.`)
    }
    if (seen.has(entry)) throw new Error(`${path} entries must be unique.`)
    seen.add(entry)
    result.push(entry)
  }
  return result
}

function validateGodConfig(value: unknown, cardIds: ReadonlySet<string>): void {
  if (!isRecord(value) || typeof value.enabled !== 'boolean') {
    throw new Error('god must be an object with an enabled flag.')
  }
  if (!isRecord(value.deckMemory) || typeof value.deckMemory.enabled !== 'boolean') {
    throw new Error('god.deckMemory must be an object with an enabled flag.')
  }
  const deckMemory = value.deckMemory
  if (!['always', 'perCon'].includes(String(deckMemory.availability))) {
    throw new Error('god.deckMemory.availability must be always or perCon.')
  }
  if (!Number.isInteger(deckMemory.inspectionsPerCon)
    || Number(deckMemory.inspectionsPerCon) < (deckMemory.availability === 'perCon' ? 1 : 0)) {
    throw new Error('god.deckMemory.inspectionsPerCon must match its availability mode.')
  }
  if (deckMemory.orientation !== 'nextDrawFirst') {
    throw new Error('god.deckMemory.orientation must be nextDrawFirst.')
  }
  const deckMemoryExcluded = validateUniqueStringList(
    deckMemory.excludedDefinitionIds,
    'god.deckMemory.excludedDefinitionIds',
  )
  for (const cardId of deckMemoryExcluded) {
    if (!cardIds.has(cardId)) {
      throw new Error(`god.deckMemory references unknown card ${cardId}.`)
    }
  }

  if (!isRecord(value.antiBito) || typeof value.antiBito.enabled !== 'boolean') {
    throw new Error('god.antiBito must be an object with an enabled flag.')
  }
  const antiBito = value.antiBito
  for (const [field, minimum] of [
    ['minimumConsecutiveBito', 1],
    ['returnCount', 1],
    ['maxInterventions', antiBito.enabled ? 1 : 0],
    ['historyRetention', 1],
  ] as const) {
    if (!Number.isInteger(antiBito[field]) || Number(antiBito[field]) < minimum) {
      throw new Error(`god.antiBito.${field} must be an integer of at least ${minimum}.`)
    }
  }
  if (antiBito.source !== 'discard') throw new Error('god.antiBito.source must be discard.')
  if (!['drawBottom', 'drawTop', 'shuffle'].includes(String(antiBito.insertion))) {
    throw new Error('god.antiBito.insertion must be drawBottom, drawTop, or shuffle.')
  }
  if (antiBito.orientation !== 'preserve') {
    throw new Error('god.antiBito.orientation must be preserve.')
  }
  const antiBitoExcluded = validateUniqueStringList(
    antiBito.excludedDefinitionIds,
    'god.antiBito.excludedDefinitionIds',
  )
  for (const cardId of antiBitoExcluded) {
    if (!cardIds.has(cardId)) throw new Error(`god.antiBito references unknown card ${cardId}.`)
  }

  if (!Array.isArray(value.lines)) throw new Error('god.lines must be an array.')
  const lineIds = new Set<string>()
  for (const rawLine of value.lines) {
    if (!isRecord(rawLine) || typeof rawLine.id !== 'string' || !rawLine.id.trim()) {
      throw new Error('god.lines entries need a non-empty id.')
    }
    if (lineIds.has(rawLine.id)) throw new Error(`god.lines repeats id ${rawLine.id}.`)
    lineIds.add(rawLine.id)
    if (!(EMPIRES_GOD_DIALOGUE_TRIGGERS as readonly unknown[]).includes(rawLine.trigger)) {
      throw new Error(`god line ${rawLine.id} has an unknown trigger.`)
    }
    if (typeof rawLine.text !== 'string' || !rawLine.text.trim()) {
      throw new Error(`god line ${rawLine.id} needs authored text.`)
    }
    if (typeof rawLine.weight !== 'number' || !Number.isFinite(rawLine.weight) || rawLine.weight <= 0) {
      throw new Error(`god line ${rawLine.id} weight must be finite and positive.`)
    }
    if (typeof rawLine.once !== 'boolean') throw new Error(`god line ${rawLine.id} once must be boolean.`)
  }
  if (!Number.isInteger(value.dialogueLogRetention) || Number(value.dialogueLogRetention) < 1) {
    throw new Error('god.dialogueLogRetention must be a positive integer.')
  }
  if (!isRecord(value.mercyConfirmation)
    || typeof value.mercyConfirmation.enabled !== 'boolean') {
    throw new Error('god.mercyConfirmation must be an object with an enabled flag.')
  }
  for (const field of ['title', 'confirmLabel', 'cancelLabel'] as const) {
    if (typeof value.mercyConfirmation[field] !== 'string'
      || !String(value.mercyConfirmation[field]).trim()) {
      throw new Error(`god.mercyConfirmation.${field} must be a non-empty string.`)
    }
  }
}

function validateCombatConfig(
  value: unknown,
  technologies: readonly unknown[],
): void {
  if (!isRecord(value)) throw new Error('combat must be an object.')
  const enabled = value.enabled === true
  const damageTypes = value.damageTypes as unknown[]
  const armorClasses = value.armorClasses as unknown[]
  const counterRules = value.counterRules as unknown[]
  const equipment = value.equipment as unknown[]
  const technologyIds = new Set(technologies.flatMap(technology =>
    isRecord(technology) && typeof technology.id === 'string' ? [technology.id] : []))

  const damageTypeIds = new Set<string>()
  const autoPriorities = new Map<CombatDamageAutoPriority, string>()
  for (const rawDefinition of damageTypes) {
    if (!isRecord(rawDefinition) || typeof rawDefinition.id !== 'string' || !rawDefinition.id.trim()) {
      throw new Error('combat.damageTypes entries need a non-empty id.')
    }
    if (damageTypeIds.has(rawDefinition.id)) {
      throw new Error(`combat.damageTypes repeats id ${rawDefinition.id}.`)
    }
    damageTypeIds.add(rawDefinition.id)
    if (typeof rawDefinition.name !== 'string' || !rawDefinition.name.trim()) {
      throw new Error(`combat damage type ${rawDefinition.id} needs a non-empty name.`)
    }
    if (rawDefinition.autoPriority !== undefined) {
      if (rawDefinition.autoPriority !== 'unarmored' && rawDefinition.autoPriority !== 'armorOvermatch') {
        throw new Error(`combat damage type ${rawDefinition.id} has an unknown autoPriority.`)
      }
      const priority = rawDefinition.autoPriority as CombatDamageAutoPriority
      if (autoPriorities.has(priority)) {
        throw new Error(`combat damageTypes may define autoPriority ${priority} only once.`)
      }
      autoPriorities.set(priority, rawDefinition.id)
    }
  }

  const armorClassIds = new Set<string>()
  for (const rawDefinition of armorClasses) {
    if (!isRecord(rawDefinition) || typeof rawDefinition.id !== 'string' || !rawDefinition.id.trim()) {
      throw new Error('combat.armorClasses entries need a non-empty id.')
    }
    if (armorClassIds.has(rawDefinition.id)) {
      throw new Error(`combat.armorClasses repeats id ${rawDefinition.id}.`)
    }
    armorClassIds.add(rawDefinition.id)
    if (typeof rawDefinition.name !== 'string' || !rawDefinition.name.trim()) {
      throw new Error(`combat armor class ${rawDefinition.id} needs a non-empty name.`)
    }
    if (rawDefinition.tags !== undefined) {
      validateUniqueStringList(rawDefinition.tags, `combat armor class ${rawDefinition.id} tags`)
    }
  }

  const equipmentIds = new Set<string>()
  const weaponTags = new Set<string>()
  for (const rawEquipment of equipment) {
    if (!isRecord(rawEquipment) || typeof rawEquipment.id !== 'string' || !rawEquipment.id.trim()) {
      throw new Error('combat.equipment entries need a non-empty id.')
    }
    if (equipmentIds.has(rawEquipment.id)) {
      throw new Error(`combat.equipment repeats id ${rawEquipment.id}.`)
    }
    equipmentIds.add(rawEquipment.id)
    if (typeof rawEquipment.name !== 'string' || !rawEquipment.name.trim()) {
      throw new Error(`combat equipment ${rawEquipment.id} needs a non-empty name.`)
    }
    if (!['weapon', 'armor', 'shield'].includes(String(rawEquipment.kind))) {
      throw new Error(`combat equipment ${rawEquipment.id} has an unknown kind.`)
    }
    if (
      rawEquipment.deferredReason !== undefined
      && (typeof rawEquipment.deferredReason !== 'string' || !rawEquipment.deferredReason.trim())
    ) {
      throw new Error(`combat equipment ${rawEquipment.id} deferredReason must be a non-empty string.`)
    }
    if (rawEquipment.technologyId !== undefined) {
      if (
        typeof rawEquipment.technologyId !== 'string'
        || !technologyIds.has(rawEquipment.technologyId)
      ) {
        throw new Error(
          `combat equipment ${rawEquipment.id} references unknown technology ${String(rawEquipment.technologyId)}.`,
        )
      }
    }
    if (!isRecord(rawEquipment.profile)) {
      throw new Error(`combat equipment ${rawEquipment.id} needs a profile object.`)
    }
    const profile = rawEquipment.profile
    if (rawEquipment.kind === 'weapon') {
      if (!isRecord(profile.damageLevels)) {
        throw new Error(`combat weapon ${rawEquipment.id} needs damageLevels.`)
      }
      if ('classId' in profile) {
        throw new Error(`combat weapon ${rawEquipment.id} cannot use an armor profile.`)
      }
      const tags = validateUniqueStringList(profile.tags, `combat weapon ${rawEquipment.id} tags`)
      for (const tag of tags) weaponTags.add(tag)
      if (profile.mixed !== undefined && typeof profile.mixed !== 'boolean') {
        throw new Error(`combat weapon ${rawEquipment.id} mixed must be a boolean.`)
      }
      if (profile.twoTyped !== undefined && typeof profile.twoTyped !== 'boolean') {
        throw new Error(`combat weapon ${rawEquipment.id} twoTyped must be a boolean.`)
      }
      if (profile.passiveIds !== undefined) {
        validateUniqueStringList(profile.passiveIds, `combat weapon ${rawEquipment.id} passiveIds`)
      }
      const damageEntries = Object.entries(profile.damageLevels)
      for (const [damageTypeId, level] of damageEntries) {
        if (!damageTypeIds.has(damageTypeId)) {
          throw new Error(`combat weapon ${rawEquipment.id} references unknown damage type ${damageTypeId}.`)
        }
        if (typeof level !== 'number' || !Number.isFinite(level) || level < 0) {
          throw new Error(`combat weapon ${rawEquipment.id} damage level ${damageTypeId} must be finite and non-negative.`)
        }
      }
      if (!rawEquipment.deferredReason && damageEntries.length === 0) {
        throw new Error(`combat weapon ${rawEquipment.id} needs at least one damage level or deferredReason.`)
      }
      if (profile.mixed === true && profile.twoTyped === true) {
        throw new Error(`combat weapon ${rawEquipment.id} cannot be both mixed and twoTyped.`)
      }
      if (profile.mixed === true && damageEntries.length < 2) {
        throw new Error(`combat weapon ${rawEquipment.id} mixed profiles need at least two damage types.`)
      }
      if (profile.twoTyped === true && damageEntries.length !== 2) {
        throw new Error(`combat weapon ${rawEquipment.id} twoTyped profiles need exactly two damage types.`)
      }
      continue
    }

    if ('damageLevels' in profile) {
      throw new Error(`combat ${String(rawEquipment.kind)} ${rawEquipment.id} cannot use a weapon profile.`)
    }
    if (typeof profile.classId !== 'string' || !armorClassIds.has(profile.classId)) {
      throw new Error(
        `combat ${String(rawEquipment.kind)} ${rawEquipment.id} references unknown armor class ${String(profile.classId)}.`,
      )
    }
    if (typeof profile.level !== 'number' || !Number.isFinite(profile.level) || profile.level < 0) {
      throw new Error(`combat ${String(rawEquipment.kind)} ${rawEquipment.id} level must be finite and non-negative.`)
    }
    if (profile.tags !== undefined) {
      validateUniqueStringList(profile.tags, `combat ${String(rawEquipment.kind)} ${rawEquipment.id} tags`)
    }
  }

  const counterRuleIds = new Set<string>()
  const counterKinds = new Set<CombatCounterRule['kind']>([
    'damageTypeCountersArmor',
    'armorCountersDamageType',
    'damageTypeOvermatchesArmor',
    'weaponTagCountersArmor',
    'weaponTagCountersAllArmor',
    'armorBlocksWeaponTag',
    'weaponTagIgnoresArmorCounter',
  ])
  for (const rawRule of counterRules) {
    if (!isRecord(rawRule) || typeof rawRule.id !== 'string' || !rawRule.id.trim()) {
      throw new Error('combat.counterRules entries need a non-empty id.')
    }
    if (counterRuleIds.has(rawRule.id)) {
      throw new Error(`combat.counterRules repeats id ${rawRule.id}.`)
    }
    counterRuleIds.add(rawRule.id)
    if (typeof rawRule.kind !== 'string' || !counterKinds.has(rawRule.kind as CombatCounterRule['kind'])) {
      throw new Error(`combat counter rule ${rawRule.id} has an unknown kind.`)
    }
    const requireDamageType = () => {
      if (typeof rawRule.damageTypeId !== 'string' || !damageTypeIds.has(rawRule.damageTypeId)) {
        throw new Error(
          `combat counter rule ${rawRule.id} references unknown damage type ${String(rawRule.damageTypeId)}.`,
        )
      }
    }
    const requireArmorClass = () => {
      if (typeof rawRule.armorClassId !== 'string' || !armorClassIds.has(rawRule.armorClassId)) {
        throw new Error(
          `combat counter rule ${rawRule.id} references unknown armor class ${String(rawRule.armorClassId)}.`,
        )
      }
    }
    const requireWeaponTag = () => {
      if (typeof rawRule.weaponTag !== 'string' || !weaponTags.has(rawRule.weaponTag)) {
        throw new Error(
          `combat counter rule ${rawRule.id} references unknown weapon tag ${String(rawRule.weaponTag)}.`,
        )
      }
    }
    const kind = rawRule.kind as CombatCounterRule['kind']
    if (kind === 'damageTypeCountersArmor' || kind === 'armorCountersDamageType') {
      requireDamageType()
      requireArmorClass()
    }
    else if (kind === 'damageTypeOvermatchesArmor') {
      requireDamageType()
    }
    else if (
      kind === 'weaponTagCountersArmor'
      || kind === 'armorBlocksWeaponTag'
      || kind === 'weaponTagIgnoresArmorCounter'
    ) {
      requireWeaponTag()
      requireArmorClass()
    }
    else {
      requireWeaponTag()
    }
  }

  if (enabled) {
    for (const priority of ['unarmored', 'armorOvermatch'] as const) {
      if (!autoPriorities.has(priority)) {
        throw new Error(`combat.damageTypes needs one ${priority} autoPriority when combat.enabled is true.`)
      }
    }
    const overmatchTypeId = autoPriorities.get('armorOvermatch')
    const hasOvermatchRule = counterRules.some(rule => isRecord(rule)
      && rule.kind === 'damageTypeOvermatchesArmor'
      && rule.damageTypeId === overmatchTypeId)
    if (!hasOvermatchRule) {
      throw new Error('combat.counterRules needs a damageTypeOvermatchesArmor rule for the armorOvermatch type.')
    }
  }
}

function validateSteelResearchConfig(config: EmpiresEndgameConfig): void {
  const steel = config.empire.steelResearch
  if (!steel || typeof steel !== 'object') throw new Error('empire.steelResearch must be an object.')
  if (!Number.isFinite(steel.forkSourcePriceMultiplier) || steel.forkSourcePriceMultiplier < 1) {
    throw new Error('empire.steelResearch.forkSourcePriceMultiplier must be finite and at least 1.')
  }
  if (!Number.isInteger(steel.delayedFreeEmpirePhases) || steel.delayedFreeEmpirePhases < 1) {
    throw new Error('empire.steelResearch.delayedFreeEmpirePhases must be a positive integer.')
  }
  if (typeof steel.militaryEliteFlagId !== 'string' || !steel.militaryEliteFlagId.trim()) {
    throw new Error('empire.steelResearch.militaryEliteFlagId must be a non-empty string.')
  }

  const technologies = new Map(config.empire.technologies.map(technology => [technology.id, technology]))
  const equipment = new Map(config.combat.equipment.map(definition => [definition.id, definition]))
  for (const technology of config.empire.technologies) {
    if (technology.category !== 'steel') {
      if (technology.steel !== undefined) throw new Error(`non-steel technology ${technology.id} cannot define steel metadata.`)
      continue
    }
    const metadata = technology.steel
    if (!metadata) throw new Error(`steel technology ${technology.id} needs steel metadata.`)
    if (!metadata.branchId?.trim()) throw new Error(`steel technology ${technology.id} needs a branchId.`)
    if (!Number.isInteger(metadata.generation) || metadata.generation < 0) {
      throw new Error(`steel technology ${technology.id} generation must be a non-negative integer.`)
    }
    if (!['whole', 'minus', 'plus'].includes(metadata.stage)) {
      throw new Error(`steel technology ${technology.id} has an unknown generation stage.`)
    }
    if (!['equipment', 'unlock-only', 'deferred'].includes(metadata.payoff)) {
      throw new Error(`steel technology ${technology.id} has an unknown payoff.`)
    }
    if (metadata.stage === 'plus') {
      const access = metadata.accessTechnologyId
        ? technologies.get(metadata.accessTechnologyId)
        : undefined
      if (!access) {
        throw new Error(`steel + technology ${technology.id} needs a known accessTechnologyId.`)
      }
      if (!access.steel
        || access.steel.branchId !== metadata.branchId
        || access.steel.stage === 'plus'
        || access.steel.generation > metadata.generation) {
        throw new Error(`steel + technology ${technology.id} needs an earlier access stage in the same branch.`)
      }
    } else if (metadata.accessTechnologyId !== undefined) {
      throw new Error(`steel technology ${technology.id} may use accessTechnologyId only for a + generation.`)
    }
    if (metadata.entryFromTechnologyIds !== undefined) {
      const entries = validateUniqueStringList(
        metadata.entryFromTechnologyIds,
        `steel technology ${technology.id} entryFromTechnologyIds`,
      )
      for (const entryId of entries) {
        const entry = technologies.get(entryId)
        if (!entry?.steel || entry.steel.branchId === metadata.branchId) {
          throw new Error(`steel technology ${technology.id} entry ${entryId} must come from another steel branch.`)
        }
      }
    }
    const equipmentIds = metadata.equipmentIds ?? []
    if (metadata.payoff === 'equipment' && equipmentIds.length === 0) {
      throw new Error(`steel equipment technology ${technology.id} needs equipmentIds.`)
    }
    if (new Set(equipmentIds).size !== equipmentIds.length) {
      throw new Error(`steel technology ${technology.id} equipmentIds must be unique.`)
    }
    for (const equipmentId of equipmentIds) {
      const definition = equipment.get(equipmentId)
      if (!definition || definition.technologyId !== technology.id) {
        throw new Error(`steel technology ${technology.id} does not own equipment ${equipmentId}.`)
      }
      if (!technology.deferredReason && definition.deferredReason) {
        throw new Error(`live steel technology ${technology.id} cannot use deferred equipment ${equipmentId}.`)
      }
      if (!technology.deferredReason) {
        const produced = (config.td.equipmentProduction ?? []).some(recipe => (
          recipe.equipmentId === equipmentId && recipe.technologyId === technology.id
        ))
        if (!produced) {
          throw new Error(`live steel equipment ${equipmentId} needs a technology-gated production recipe.`)
        }
        const consumedByUnit = (config.empire.units ?? []).some(unit => (
          !unit.deferredReason && (unit.loadouts ?? []).some(loadout => (
            (loadout.weaponEquipmentId === equipmentId || loadout.defenseEquipmentId === equipmentId)
            && loadout.equipmentCosts.some(cost => cost.equipmentId === equipmentId && cost.amount > 0)
          ))
        ))
        const consumedByTower = (config.td.towerBases ?? []).some(base => (
          (base.loadouts ?? []).some(loadout => (
            (loadout.weaponEquipmentId === equipmentId || loadout.defenseEquipmentId === equipmentId)
            && loadout.equipmentCosts.some(cost => cost.equipmentId === equipmentId && cost.amount > 0)
          ))
        ))
        if (!consumedByUnit && !consumedByTower) {
          throw new Error(`live steel equipment ${equipmentId} needs a canonical unit or tower loadout consumer.`)
        }
      }
    }
    if (!technology.deferredReason && metadata.payoff === 'deferred') {
      throw new Error(`live steel technology ${technology.id} cannot have a deferred payoff.`)
    }
  }
}

function validateGovernanceConfig(config: EmpiresEndgameConfig): void {
  const governance = config.governance
  if (!governance || typeof governance.enabled !== 'boolean') {
    throw new Error('governance must define an enabled boolean.')
  }
  if (!Array.isArray(governance.advisors)
    || !Array.isArray(governance.advisorDecisions)
    || !Array.isArray(governance.persts)
    || !isRecord(governance.trump)
    || !isRecord(governance.governor)
    || !Array.isArray(governance.governor.regionIds)
    || !Array.isArray(governance.governor.citySites)
    || !isRecord(governance.capital)
    || !Array.isArray(governance.capital.sites)) {
    throw new Error('governance catalogs are incomplete.')
  }
  if (!governance.enabled) return

  const suits = new Set(['clubs', 'diamonds', 'hearts', 'spades'])
  const advisorIds = new Set<string>()
  const decisionIds = new Set(governance.advisorDecisions.map(decision => decision.id))
  const technologyIds = new Set(config.empire.technologies.map(technology => technology.id))
  for (const advisor of governance.advisors) {
    if (!advisor.id?.trim() || advisorIds.has(advisor.id)) {
      throw new Error(`governance repeats or omits advisor id ${advisor.id}.`)
    }
    advisorIds.add(advisor.id)
    if (!advisor.name?.trim() || !suits.has(advisor.suit)) {
      throw new Error(`advisor ${advisor.id} needs a name and standard suit.`)
    }
    if (!['locked', 'awaiting-judgment', 'active', 'executed'].includes(advisor.initialStatus)) {
      throw new Error(`advisor ${advisor.id} has an invalid initialStatus.`)
    }
    if (advisor.decisionId && !decisionIds.has(advisor.decisionId)) {
      throw new Error(`advisor ${advisor.id} references unknown decision ${advisor.decisionId}.`)
    }
    for (const technologyId of advisor.technologyIds) {
      const technology = config.empire.technologies.find(item => item.id === technologyId)
      if (!technologyIds.has(technologyId)
        || !technology?.prerequisites.some(dependency => (
          dependency.kind === 'advisor' && dependency.advisorId === advisor.id
        ))) {
        throw new Error(`advisor ${advisor.id} technology ${technologyId} must use its advisor prerequisite.`)
      }
    }
    if (advisor.grandAdvisor && advisor.initialStatus === 'locked' && !advisor.accessDeferredReason?.trim()) {
      throw new Error(`locked grand advisor ${advisor.id} needs an accessDeferredReason.`)
    }
  }
  for (const decision of governance.advisorDecisions) {
    const members = validateUniqueStringList(decision.advisorIds, `advisor decision ${decision.id}`)
    if (!decision.id?.trim() || members.some(advisorId => !advisorIds.has(advisorId))) {
      throw new Error(`advisor decision ${decision.id} references an unknown advisor.`)
    }
    if (!Number.isInteger(decision.pardonsRequired) || decision.pardonsRequired < 0
      || !Number.isInteger(decision.executionsRequired) || decision.executionsRequired < 0
      || decision.pardonsRequired + decision.executionsRequired !== members.length) {
      throw new Error(`advisor decision ${decision.id} quotas must resolve every member exactly once.`)
    }
    for (const advisorId of members) {
      if (governance.advisors.find(advisor => advisor.id === advisorId)?.decisionId !== decision.id) {
        throw new Error(`advisor decision ${decision.id} membership must be reciprocal for ${advisorId}.`)
      }
    }
  }
  const grandAdvisors = governance.advisors.filter(advisor => advisor.grandAdvisor)
  if (grandAdvisors.length !== 1 || grandAdvisors[0].id !== governance.trump.grandAdvisorId) {
    throw new Error('governance must define exactly one trump grandAdvisorId.')
  }
  if (!suits.has(governance.trump.restrictedSuit)
    || !suits.has(governance.trump.lockedFallbackSuit)
    || governance.trump.restrictedSuit === governance.trump.lockedFallbackSuit
    || !Number.isFinite(governance.trump.criticalEffectMultiplier)
    || governance.trump.criticalEffectMultiplier <= 1) {
    throw new Error('governance trump must define distinct standard suits and a criticalEffectMultiplier above 1.')
  }
  if (config.durak.fixedTrumpSuit === governance.trump.restrictedSuit
    && grandAdvisors[0].initialStatus !== 'active') {
    throw new Error('durak.fixedTrumpSuit cannot select the locked governance restricted suit.')
  }

  const allDependencies = [
    ...config.empire.technologies.flatMap(technology => technology.prerequisites),
    ...config.empire.events.flatMap(event => event.prerequisites ?? []),
    ...config.empire.hiddenCombinations.definitions.flatMap(combination => combination.prerequisites),
    ...(config.empire.units ?? []).flatMap(unit => unit.dependencies),
    ...config.empire.buildings.flatMap(building => building.levels.flatMap(level => level.dependencies)),
    ...config.empire.externalEconomy.offers.flatMap(offer => offer.prerequisites),
    ...config.empire.externalEconomy.unions.flatMap(union => union.prerequisites),
  ]
  for (const dependency of allDependencies) {
    if (dependency.kind === 'advisor' && !advisorIds.has(dependency.advisorId)) {
      throw new Error(`dependency references unknown advisor ${dependency.advisorId}.`)
    }
  }

  const regionIds = new Set(config.empire.map.regions.map(region => region.id))
  const governorRegionIds = validateUniqueStringList(
    governance.governor.regionIds,
    'governance governor regionIds',
  )
  if (governance.governor.assignmentMode !== 'permanent' || governorRegionIds.length !== 4
    || governorRegionIds.some(regionId => !regionIds.has(regionId))) {
    throw new Error('governance governor must permanently manage exactly four known regions.')
  }
  const perstIds = validateUniqueStringList(
    governance.persts.map(perst => perst.id),
    'governance persts',
  )
  if (perstIds.length === 0 || governance.persts.some(perst => !perst.name?.trim() || !perst.title?.trim())) {
    throw new Error('governance needs uniquely identified, named persts.')
  }

  const cityById = new Map(config.empire.cities.map(city => [city.id, city]))
  const siteCityIds = new Set<string>()
  for (const site of governance.governor.citySites) {
    const city = cityById.get(site.cityId)
    if (!city || city.regionId !== site.regionId || siteCityIds.has(site.cityId)) {
      throw new Error(`governance city site ${site.cityId} must map once to its city region.`)
    }
    siteCityIds.add(site.cityId)
    if (!Number.isInteger(site.order) || site.order < 1 || ![1, 2, 3].includes(site.defenseLayer)) {
      throw new Error(`governance city site ${site.cityId} has an invalid layer or order.`)
    }
  }
  if (siteCityIds.size !== cityById.size) {
    throw new Error('governance citySites must cover every configured city exactly once.')
  }
  for (const regionId of governorRegionIds) {
    const sites = governance.governor.citySites.filter(site => site.regionId === regionId)
    const signature = sites
      .map(site => `${site.access}:${site.defenseLayer}`)
      .sort()
    const expected = ['governor:2', 'governor:2', 'governor:3', 'initial:1', 'initial:1']
    if (sites.length !== 5 || signature.join('|') !== expected.join('|')) {
      throw new Error(`governance region ${regionId} must use the 2 initial + 2 layer-two + 1 layer-three city pattern.`)
    }
  }

  if (!cityById.has(governance.capital.cityId)) {
    throw new Error(`governance capital references unknown city ${governance.capital.cityId}.`)
  }
  validateUniqueStringList(governance.capital.sites.map(site => site.id), 'governance capital sites')
  const buildingIds = new Set(config.empire.buildings.map(building => building.id))
  const mapObjectIds = new Set(config.empire.map.objects.map(object => object.id))
  for (const site of governance.capital.sites) {
    if (!site.name?.trim() || !site.deferredReason?.trim()
      || (site.buildingId !== undefined && !buildingIds.has(site.buildingId))
      || (site.mapObjectId !== undefined && !mapObjectIds.has(site.mapObjectId))) {
      throw new Error(`governance capital site ${site.id} has an invalid carrier or deferredReason.`)
    }
  }
}

function validateTdConfig(
  value: unknown,
  combat: unknown,
  units: readonly unknown[],
  technologies: readonly unknown[],
  regionIds: readonly string[],
): void {
  if (!isRecord(value)) throw new Error('td must be an object.')
  const td = value as unknown as EmpiresTdConfig
  if (typeof td.enabled !== 'boolean') throw new Error('td.enabled must be a boolean.')
  if (typeof td.regionalCatalogEnabled !== 'boolean') {
    throw new Error('td.regionalCatalogEnabled must be a boolean.')
  }
  const requireFinite = (number: unknown, path: string, minimum = 0) => {
    if (typeof number !== 'number' || !Number.isFinite(number) || number < minimum) {
      throw new Error(`${path} must be finite and at least ${minimum}.`)
    }
  }
  const requirePositiveInteger = (number: unknown, path: string) => {
    if (typeof number !== 'number' || !Number.isInteger(number) || number <= 0) {
      throw new Error(`${path} must be a positive integer.`)
    }
  }
  requirePositiveInteger(td.tickMs, 'td.tickMs')
  requirePositiveInteger(td.maxTicks, 'td.maxTicks')
  requirePositiveInteger(td.maxCommands, 'td.maxCommands')
  requirePositiveInteger(td.resultLogLimit, 'td.resultLogLimit')
  requirePositiveInteger(td.maxCatchUpTicksPerFrame, 'td.maxCatchUpTicksPerFrame')
  requirePositiveInteger(td.waveEveryCons, 'td.waveEveryCons')
  requireFinite(td.startingBuildResources, 'td.startingBuildResources')

  if (!td.alliance || !isRecord(td.alliance)) throw new Error('td.alliance must be an object.')
  if (!td.settlement || !isRecord(td.settlement)) throw new Error('td.settlement must be an object.')
  if (!td.morale || !isRecord(td.morale)) throw new Error('td.morale must be an object.')
  if (!Array.isArray(td.equipmentProductionLines)) {
    throw new Error('td.equipmentProductionLines must be an array.')
  }
  if (!Array.isArray(td.equipmentProduction)) throw new Error('td.equipmentProduction must be an array.')
  if (!Array.isArray(td.towerBases)
    || !Array.isArray(td.battlefields)
    || !Array.isArray(td.towers)
    || !Array.isArray(td.gradeChoices)
    || !Array.isArray(td.waves)
    || !Array.isArray(td.planVariants)) {
    throw new Error('td towerBases, battlefields, towers, gradeChoices, waves, and planVariants must be arrays.')
  }

  for (const path of [
    'baseThreat',
    'threatPerWave',
    'healthPerThreat',
    'countPerThreat',
    'speedPerThreat',
  ] as const) {
    requireFinite(td.alliance[path], `td.alliance.${path}`)
  }
  requireFinite(td.settlement.lossLoyaltyThreshold, 'td.settlement.lossLoyaltyThreshold')
  if (td.settlement.lossLoyaltyThreshold > 1) {
    throw new Error('td.settlement.lossLoyaltyThreshold must not exceed 1.')
  }
  requireFinite(td.settlement.veteranHealthThreshold, 'td.settlement.veteranHealthThreshold')
  if (td.settlement.veteranHealthThreshold > 1) {
    throw new Error('td.settlement.veteranHealthThreshold must not exceed 1.')
  }
  requireFinite(td.settlement.recruitmentPenaltyPerLoss, 'td.settlement.recruitmentPenaltyPerLoss')
  requireFinite(td.settlement.growthPenaltyPerLoss, 'td.settlement.growthPenaltyPerLoss')
  requireFinite(td.settlement.loyaltyDelta, 'td.settlement.loyaltyDelta', Number.NEGATIVE_INFINITY)
  for (const [outcome, consequence] of Object.entries({
    victory: td.settlement.victory,
    defeat: td.settlement.defeat,
    abort: td.settlement.abort,
  })) {
    if (!isRecord(consequence)) throw new Error(`td.settlement.${outcome} must be an object.`)
    requireFinite(
      consequence.recruitmentPenaltyPerDeployedUnit,
      `td.settlement.${outcome}.recruitmentPenaltyPerDeployedUnit`,
    )
    requireFinite(consequence.moraleDelta, `td.settlement.${outcome}.moraleDelta`, Number.NEGATIVE_INFINITY)
    requireFinite(
      consequence.allianceThreatDelta,
      `td.settlement.${outcome}.allianceThreatDelta`,
      Number.NEGATIVE_INFINITY,
    )
  }
  requireFinite(td.morale.minimum, 'td.morale.minimum', Number.NEGATIVE_INFINITY)
  requireFinite(td.morale.maximum, 'td.morale.maximum', Number.NEGATIVE_INFINITY)
  requireFinite(td.morale.initial, 'td.morale.initial', Number.NEGATIVE_INFINITY)
  if (td.morale.minimum > td.morale.maximum
    || td.morale.initial < td.morale.minimum
    || td.morale.initial > td.morale.maximum) {
    throw new Error('td.morale must satisfy minimum <= initial <= maximum.')
  }

  const technologyIds = new Set(technologies.flatMap(technology => isRecord(technology)
    && typeof technology.id === 'string' ? [technology.id] : []))
  const productionLineIds = new Set<string>()
  const capacityShares = new Map<string, number>()
  for (const line of td.equipmentProductionLines) {
    if (!line.id?.trim()) throw new Error('td equipment production line needs an id.')
    if (productionLineIds.has(line.id)) throw new Error(`td equipment production line repeats ${line.id}.`)
    productionLineIds.add(line.id)
    if (!line.capacityFlagId?.trim()) {
      throw new Error(`td equipment production line ${line.id} needs a capacityFlagId.`)
    }
    requireFinite(line.capacityShare, `td equipment production line ${line.id} capacityShare`, Number.EPSILON)
    if (line.capacityShare > 1) {
      throw new Error(`td equipment production line ${line.id} capacityShare must not exceed 1.`)
    }
    capacityShares.set(
      line.capacityFlagId,
      (capacityShares.get(line.capacityFlagId) ?? 0) + line.capacityShare,
    )
  }
  for (const [capacityFlagId, share] of capacityShares) {
    if (share > 1 + Number.EPSILON) {
      throw new Error(`td equipment production lines over-allocate ${capacityFlagId}.`)
    }
  }

  const equipmentStockIds = new Set<string>()
  const productionRecipeIds = new Set<string>()
  for (const definition of td.equipmentProduction) {
    if (!definition.id?.trim()) throw new Error('td equipment production recipe needs an id.')
    if (productionRecipeIds.has(definition.id)) {
      throw new Error(`td equipment production recipe repeats ${definition.id}.`)
    }
    productionRecipeIds.add(definition.id)
    if (!definition.equipmentId?.trim()) throw new Error('td equipment production needs an equipmentId.')
    if (!productionLineIds.has(definition.lineId)) {
      throw new Error(`td equipment production ${definition.id} references unknown line ${definition.lineId}.`)
    }
    equipmentStockIds.add(definition.equipmentId)
    requireFinite(
      definition.amountPerSmithCapacity,
      `td equipment ${definition.equipmentId} amountPerSmithCapacity`,
      Number.EPSILON,
    )
    requireFinite(definition.priority, `td equipment ${definition.equipmentId} priority`, Number.NEGATIVE_INFINITY)
    if (definition.technologyId !== undefined && !technologyIds.has(definition.technologyId)) {
      throw new Error(`td equipment ${definition.equipmentId} references unknown technology ${definition.technologyId}.`)
    }
  }

  const liveCombat = isRecord(combat) && combat.enabled === true
    ? combat as unknown as EmpiresCombatConfig
    : null
  const combatEquipment = new Map((isRecord(combat) && Array.isArray(combat.equipment)
    ? combat.equipment
    : []).flatMap(entry => isRecord(entry) && typeof entry.id === 'string'
    ? [[entry.id, entry] as const]
    : []))
  for (const recipe of td.equipmentProduction) {
    const equipment = combatEquipment.get(recipe.equipmentId)
    if (equipment?.technologyId !== undefined && recipe.technologyId !== equipment.technologyId) {
      throw new Error(
        `td equipment ${recipe.equipmentId} must use its equipment technology ${equipment.technologyId}.`,
      )
    }
  }
  const damageTypeIds = new Set(liveCombat?.damageTypes.map(definition => definition.id) ?? [])
  const armorClassIds = new Set(liveCombat?.armorClasses.map(definition => definition.id) ?? [])
  const validateWeapon = (profile: CombatWeaponProfile, path: string) => {
    if (!profile || !isRecord(profile.damageLevels) || !Array.isArray(profile.tags)) {
      throw new Error(`${path} must be a combat weapon profile.`)
    }
    const levels = Object.entries(profile.damageLevels)
    if (levels.length === 0) throw new Error(`${path} needs at least one damage level.`)
    for (const [damageTypeId, level] of levels) {
      if (liveCombat && !damageTypeIds.has(damageTypeId)) {
        throw new Error(`${path} references unknown damage type ${damageTypeId}.`)
      }
      requireFinite(level, `${path} ${damageTypeId}`, 0)
    }
  }
  const validateArmor = (profile: CombatArmorProfile | null, path: string) => {
    if (profile === null) return
    if (!profile || (liveCombat && !armorClassIds.has(profile.classId))) {
      throw new Error(`${path} references an unknown armor class.`)
    }
    requireFinite(profile.level, `${path} level`)
  }
  const validateLoadouts = (rawLoadouts: unknown, path: string) => {
    if (rawLoadouts === undefined) return
    if (!Array.isArray(rawLoadouts)) throw new Error(`${path} loadouts must be an array.`)
    const ids = new Set<string>()
    for (const rawLoadout of rawLoadouts) {
      if (!isRecord(rawLoadout) || typeof rawLoadout.id !== 'string' || !rawLoadout.id.trim()) {
        throw new Error(`${path} loadout needs an id.`)
      }
      if (ids.has(rawLoadout.id)) throw new Error(`${path} repeats loadout ${rawLoadout.id}.`)
      ids.add(rawLoadout.id)
      requireFinite(rawLoadout.priority as number, `${path} loadout ${rawLoadout.id} priority`, Number.NEGATIVE_INFINITY)
      const weapon = combatEquipment.get(String(rawLoadout.weaponEquipmentId))
      if (!weapon || weapon.kind !== 'weapon' || weapon.deferredReason) {
        throw new Error(`${path} loadout ${rawLoadout.id} references unavailable weapon equipment.`)
      }
      if (rawLoadout.defenseEquipmentId !== undefined) {
        const defense = combatEquipment.get(String(rawLoadout.defenseEquipmentId))
        if (!defense || defense.kind === 'weapon' || defense.deferredReason) {
          throw new Error(`${path} loadout ${rawLoadout.id} references unavailable defense equipment.`)
        }
      }
      if (!Array.isArray(rawLoadout.equipmentCosts) || rawLoadout.equipmentCosts.length === 0) {
        throw new Error(`${path} loadout ${rawLoadout.id} needs equipmentCosts.`)
      }
      const costIds = new Set<string>()
      for (const cost of rawLoadout.equipmentCosts) {
        if (!isRecord(cost) || typeof cost.equipmentId !== 'string' || !equipmentStockIds.has(cost.equipmentId)) {
          throw new Error(`${path} loadout ${rawLoadout.id} references unknown equipment stock.`)
        }
        if (costIds.has(cost.equipmentId)) {
          throw new Error(`${path} loadout ${rawLoadout.id} repeats equipment cost ${cost.equipmentId}.`)
        }
        requireFinite(cost.amount as number, `${path} loadout ${rawLoadout.id} equipment cost`, Number.EPSILON)
        costIds.add(cost.equipmentId)
      }
      for (const [equipmentId, definition] of [
        [String(rawLoadout.weaponEquipmentId), weapon],
        ...(rawLoadout.defenseEquipmentId === undefined
          ? []
          : [[String(rawLoadout.defenseEquipmentId), combatEquipment.get(String(rawLoadout.defenseEquipmentId))]]),
      ] as Array<[string, Record<string, unknown> | undefined]>) {
        if (definition?.technologyId !== undefined && !costIds.has(equipmentId)) {
          throw new Error(`${path} loadout ${rawLoadout.id} must consume its technology-linked equipment ${equipmentId}.`)
        }
      }
    }
  }

  const knownRegionIds = new Set(regionIds)
  const towerBaseIds = new Set<string>()
  const towerCategoryIds = new Set<string>()
  for (const base of td.towerBases) {
    if (!base.id?.trim() || !base.name?.trim()) throw new Error('td tower base needs an id and name.')
    if (towerBaseIds.has(base.id)) throw new Error(`td repeats tower base ${base.id}.`)
    towerBaseIds.add(base.id)
    if (!knownRegionIds.has(base.regionId)) throw new Error(`td tower base ${base.id} references unknown region ${base.regionId}.`)
    if (!Array.isArray(base.categoryIds) || base.categoryIds.length === 0) {
      throw new Error(`td tower base ${base.id} needs categories.`)
    }
    if (new Set(base.categoryIds).size !== base.categoryIds.length) {
      throw new Error(`td tower base ${base.id} categories must be unique.`)
    }
    for (const categoryId of base.categoryIds) {
      if (!categoryId.trim()) throw new Error(`td tower base ${base.id} has an empty category.`)
      towerCategoryIds.add(categoryId)
    }
    requireFinite(base.cost, `td tower base ${base.id} cost`)
    requireFinite(base.maxHp, `td tower base ${base.id} maxHp`, Number.EPSILON)
    requireFinite(base.range, `td tower base ${base.id} range`, Number.EPSILON)
    requirePositiveInteger(base.attackIntervalTicks, `td tower base ${base.id} attackIntervalTicks`)
    requirePositiveInteger(base.projectiles, `td tower base ${base.id} projectiles`)
    if (base.targetPriority !== 'first' && base.targetPriority !== 'strongest') {
      throw new Error(`td tower base ${base.id} targetPriority is unknown.`)
    }
    validateWeapon(base.weapon, `td tower base ${base.id} weapon`)
    validateLoadouts(base.loadouts, `td tower base ${base.id}`)
  }

  const battlefieldIds = new Set<string>()
  const battlefieldById = new Map<string, typeof td.battlefields[number]>()
  const battlefieldAllowedCategories = new Map<string, readonly string[]>()
  for (const battlefield of td.battlefields) {
    if (!battlefield.id?.trim()) throw new Error('td battlefield needs an id.')
    if (battlefieldIds.has(battlefield.id)) throw new Error(`td repeats battlefield ${battlefield.id}.`)
    battlefieldIds.add(battlefield.id)
    battlefieldById.set(battlefield.id, battlefield)
    if (!knownRegionIds.has(battlefield.regionId)) {
      throw new Error(`td battlefield ${battlefield.id} references unknown region ${battlefield.regionId}.`)
    }
    requireFinite(battlefield.width, `td battlefield ${battlefield.id} width`, Number.EPSILON)
    requireFinite(battlefield.height, `td battlefield ${battlefield.id} height`, Number.EPSILON)
    const nodeIds = new Set<string>()
    for (const node of battlefield.laneGraph.nodes) {
      if (!node.id?.trim()) throw new Error(`td battlefield ${battlefield.id} has a node without an id.`)
      if (nodeIds.has(node.id)) throw new Error(`td battlefield ${battlefield.id} repeats a lane node.`)
      nodeIds.add(node.id)
      requireFinite(node.x, `td battlefield ${battlefield.id} node ${node.id} x`, Number.NEGATIVE_INFINITY)
      requireFinite(node.y, `td battlefield ${battlefield.id} node ${node.id} y`, Number.NEGATIVE_INFINITY)
    }
    if (nodeIds.size !== battlefield.laneGraph.nodes.length) {
      throw new Error(`td battlefield ${battlefield.id} repeats a lane node.`)
    }
    for (const endpoint of [battlefield.spawnerNodeId, battlefield.objectiveNodeId, battlefield.deploymentNodeId]) {
      if (!nodeIds.has(endpoint)) throw new Error(`td battlefield ${battlefield.id} references unknown node ${endpoint}.`)
    }
    const edgeIds = new Set<string>()
    for (const edge of battlefield.laneGraph.edges) {
      if (!edge.id?.trim()) throw new Error(`td battlefield ${battlefield.id} has an edge without an id.`)
      if (edgeIds.has(edge.id)) throw new Error(`td battlefield ${battlefield.id} repeats edge ${edge.id}.`)
      edgeIds.add(edge.id)
      if (!nodeIds.has(edge.fromNodeId) || !nodeIds.has(edge.toNodeId)) {
        throw new Error(`td battlefield ${battlefield.id} edge ${edge.id} references an unknown node.`)
      }
    }
    const spotIds = new Set<string>()
    const terrainIds = new Set<string>()
    for (const spot of battlefield.buildSpots) {
      if (!spot.id?.trim()) throw new Error(`td battlefield ${battlefield.id} has a build spot without an id.`)
      if (spotIds.has(spot.id)) throw new Error(`td battlefield ${battlefield.id} repeats a build spot.`)
      spotIds.add(spot.id)
      if (!spot.terrainId?.trim()) throw new Error(`td battlefield ${battlefield.id} spot ${spot.id} needs terrainId.`)
      terrainIds.add(spot.terrainId)
      requireFinite(spot.x, `td battlefield ${battlefield.id} spot ${spot.id} x`, Number.NEGATIVE_INFINITY)
      requireFinite(spot.y, `td battlefield ${battlefield.id} spot ${spot.id} y`, Number.NEGATIVE_INFINITY)
    }
    if (!Array.isArray(battlefield.towerBaseIds)
      || battlefield.towerBaseIds.length === 0
      || new Set(battlefield.towerBaseIds).size !== battlefield.towerBaseIds.length
      || battlefield.towerBaseIds.some(id => !towerBaseIds.has(id))) {
      throw new Error(`td battlefield ${battlefield.id} references an unknown tower base.`)
    }
    if (battlefield.towerBaseIds.some(id => td.towerBases.find(base => base.id === id)?.regionId !== battlefield.regionId)) {
      throw new Error(`td battlefield ${battlefield.id} must use tower bases from its own region.`)
    }
    if (!Array.isArray(battlefield.allowedTowerCategoryIds)) {
      throw new Error(`td battlefield ${battlefield.id} allowedTowerCategoryIds must be an array.`)
    }
    if (new Set(battlefield.allowedTowerCategoryIds).size !== battlefield.allowedTowerCategoryIds.length
      || battlefield.allowedTowerCategoryIds.some(id => !id.trim())) {
      throw new Error(`td battlefield ${battlefield.id} allowedTowerCategoryIds must be unique and non-empty.`)
    }
    battlefieldAllowedCategories.set(battlefield.id, battlefield.allowedTowerCategoryIds)
    const modifierIds = new Set<string>()
    for (const modifier of battlefield.modifiers) {
      if (!modifier.id?.trim() || modifierIds.has(modifier.id)) {
        throw new Error(`td battlefield ${battlefield.id} repeats or omits a modifier id.`)
      }
      modifierIds.add(modifier.id)
      if (modifier.kind === 'tower-targeting') {
        if (modifier.terrainIds.some(id => !terrainIds.has(id))) {
          throw new Error(`td modifier ${modifier.id} references unknown terrain.`)
        }
        if (new Set(modifier.targetableByEnemyCategoryIds).size !== modifier.targetableByEnemyCategoryIds.length
          || modifier.targetableByEnemyCategoryIds.some(id => !id.trim())) {
          throw new Error(`td modifier ${modifier.id} enemy categories must be unique and non-empty.`)
        }
      } else if (modifier.kind === 'tower-stat') {
        if (modifier.terrainIds.some(id => !terrainIds.has(id))) {
          throw new Error(`td modifier ${modifier.id} references unknown terrain.`)
        }
        requireFinite(modifier.rangeMultiplier, `td modifier ${modifier.id} rangeMultiplier`, Number.EPSILON)
        requireFinite(modifier.maxHpMultiplier, `td modifier ${modifier.id} maxHpMultiplier`, Number.EPSILON)
      } else if (modifier.kind === 'deployment-attrition') {
        if (!modifier.modes.length || modifier.modes.some(mode => mode !== 'defense' && mode !== 'assault')) {
          throw new Error(`td modifier ${modifier.id} has invalid modes.`)
        }
        requirePositiveInteger(modifier.intervalTicks, `td modifier ${modifier.id} intervalTicks`)
        requireFinite(modifier.damagePerUnit, `td modifier ${modifier.id} damagePerUnit`, Number.EPSILON)
      } else {
        throw new Error(`td battlefield ${battlefield.id} has an unknown modifier.`)
      }
    }
  }

  const towerIds = new Set<string>()
  for (const tower of td.towers) {
    if (!tower.id?.trim()) throw new Error('td tower choice needs an id.')
    if (towerIds.has(tower.id)) throw new Error(`td repeats tower choice ${tower.id}.`)
    towerIds.add(tower.id)
    if (![1, 2, 3, 4].includes(tower.grade)) throw new Error(`td tower ${tower.id} has an unknown grade.`)
    if (!Array.isArray(tower.categoryIds) || tower.categoryIds.length === 0) {
      throw new Error(`td tower ${tower.id} needs categories.`)
    }
    if (new Set(tower.categoryIds).size !== tower.categoryIds.length) {
      throw new Error(`td tower ${tower.id} categories must be unique.`)
    }
    for (const categoryId of tower.categoryIds) {
      if (!categoryId.trim()) throw new Error(`td tower ${tower.id} has an empty category.`)
      towerCategoryIds.add(categoryId)
    }
    requireFinite(tower.cost, `td tower ${tower.id} cost`)
    requireFinite(tower.maxHpBonus, `td tower ${tower.id} maxHpBonus`, Number.NEGATIVE_INFINITY)
    requireFinite(tower.rangeBonus, `td tower ${tower.id} rangeBonus`, Number.NEGATIVE_INFINITY)
    requireFinite(
      tower.attackIntervalTicksDelta,
      `td tower ${tower.id} attackIntervalTicksDelta`,
      Number.NEGATIVE_INFINITY,
    )
    requireFinite(tower.projectileBonus, `td tower ${tower.id} projectileBonus`, Number.NEGATIVE_INFINITY)
    if (tower.targetPriority !== undefined
      && tower.targetPriority !== 'first'
      && tower.targetPriority !== 'strongest') {
      throw new Error(`td tower ${tower.id} targetPriority is unknown.`)
    }
    for (const [damageTypeId, bonus] of Object.entries(tower.damageLevelBonuses)) {
      if (liveCombat && !damageTypeIds.has(damageTypeId)) {
        throw new Error(`td tower ${tower.id} references unknown damage type ${damageTypeId}.`)
      }
      requireFinite(bonus, `td tower ${tower.id} ${damageTypeId} damage bonus`, Number.NEGATIVE_INFINITY)
    }
  }
  for (const [battlefieldId, allowedCategories] of battlefieldAllowedCategories) {
    for (const categoryId of allowedCategories) {
      if (!towerCategoryIds.has(categoryId)) {
        throw new Error(`td battlefield ${battlefieldId} references unknown tower category ${categoryId}.`)
      }
    }
  }

  const gradeKeys = new Set<string>()
  for (const set of td.gradeChoices) {
    if (!set.id?.trim()) throw new Error('td grade choice set needs an id.')
    const key = `${set.regionId}:${set.grade}`
    if (gradeKeys.has(key)) throw new Error(`td repeats grade choice set ${key}.`)
    gradeKeys.add(key)
    if (!knownRegionIds.has(set.regionId) || ![1, 2, 3, 4].includes(set.grade)) {
      throw new Error(`td grade choice set ${set.id} has an unknown region or grade.`)
    }
    if (set.deferredReason !== undefined && !set.deferredReason.trim()) {
      throw new Error(`td grade choice set ${set.id} deferredReason must be non-empty.`)
    }
    if (set.deferredReason) {
      if (set.choiceIds.length > 0) throw new Error(`deferred td grade choice set ${set.id} must be unavailable.`)
    } else if (set.choiceIds.length !== 4) {
      throw new Error(`live td grade choice set ${set.id} must contain exactly four choices.`)
    }
    for (const choiceId of set.choiceIds) {
      const choice = td.towers.find(candidate => candidate.id === choiceId)
      if (!choice || choice.grade !== set.grade) {
        throw new Error(`td grade choice set ${set.id} references invalid choice ${choiceId}.`)
      }
    }
  }

  const waveIds = new Set<string>()
  const waveById = new Map<string, typeof td.waves[number]>()
  for (const wave of td.waves) {
    if (!wave.id?.trim()) throw new Error('td wave needs an id.')
    if (waveIds.has(wave.id)) throw new Error(`td repeats wave ${wave.id}.`)
    waveIds.add(wave.id)
    waveById.set(wave.id, wave)
    if (wave.groups.length === 0) throw new Error(`td wave ${wave.id} needs an enemy group.`)
    const groupIds = new Set<string>()
    for (const group of wave.groups) {
      if (!group.id?.trim()) throw new Error(`td wave ${wave.id} has a group without an id.`)
      if (groupIds.has(group.id)) throw new Error(`td wave ${wave.id} repeats group ${group.id}.`)
      groupIds.add(group.id)
      if (!Array.isArray(group.categoryIds) || group.categoryIds.length === 0
        || group.categoryIds.some(id => !id.trim())) {
        throw new Error(`td wave ${wave.id} group ${group.id} needs categories.`)
      }
      if (new Set(group.categoryIds).size !== group.categoryIds.length) {
        throw new Error(`td wave ${wave.id} group ${group.id} categories must be unique.`)
      }
      requirePositiveInteger(group.count, `td wave ${wave.id} group ${group.id} count`)
      if (!Number.isInteger(group.startTick) || group.startTick < 0) {
        throw new Error(`td wave ${wave.id} group ${group.id} startTick must be a non-negative integer.`)
      }
      requirePositiveInteger(group.spawnIntervalTicks, `td wave ${wave.id} group ${group.id} spawnIntervalTicks`)
      requireFinite(group.maxHp, `td wave ${wave.id} group ${group.id} maxHp`, Number.EPSILON)
      requireFinite(group.speedPerSecond, `td wave ${wave.id} group ${group.id} speedPerSecond`)
      requireFinite(group.attackRange, `td wave ${wave.id} group ${group.id} attackRange`)
      requirePositiveInteger(group.attackIntervalTicks, `td wave ${wave.id} group ${group.id} attackIntervalTicks`)
      validateWeapon(group.weapon, `td wave ${wave.id} group ${group.id} weapon`)
      validateArmor(group.armor, `td wave ${wave.id} group ${group.id} armor`)
    }
  }
  const variantIds = new Set<string>()
  const objectiveIds = new Set<string>()
  const liveVariants = []
  for (const variant of td.planVariants) {
    if (!variant.id?.trim() || variantIds.has(variant.id)) throw new Error('td plan variant ids must be unique and non-empty.')
    variantIds.add(variant.id)
    if (variant.mode !== 'defense' && variant.mode !== 'assault') throw new Error(`td plan variant ${variant.id} has unknown mode.`)
    if (variant.purpose !== undefined && variant.purpose !== 'campaign' && variant.purpose !== 'expedition') {
      throw new Error(`td plan variant ${variant.id} has unknown purpose.`)
    }
    if (variant.purpose === 'expedition' && variant.mode !== 'assault') {
      throw new Error(`td expedition variant ${variant.id} must be an assault.`)
    }
    if (variant.deferredReason !== undefined && !variant.deferredReason.trim()) {
      throw new Error(`td plan variant ${variant.id} deferredReason must be non-empty.`)
    }
    const battlefield = battlefieldById.get(variant.battlefieldId)
    const wave = waveById.get(variant.waveId)
    if (!battlefield || !wave) throw new Error(`td plan variant ${variant.id} references an unknown field or wave.`)
    if (variant.objective.nodeId !== battlefield.objectiveNodeId) {
      throw new Error(`td plan variant ${variant.id} objective does not match its battlefield.`)
    }
    if (variant.objective.owner !== (variant.mode === 'defense' ? 'player' : 'enemy')) {
      throw new Error(`td plan variant ${variant.id} objective owner does not match its mode.`)
    }
    if (!variant.objective.id?.trim() || !variant.objective.name?.trim()
      || objectiveIds.has(variant.objective.id)
      || (variant.objective.kind !== 'castle' && variant.objective.kind !== 'fort')) {
      throw new Error(`td plan variant ${variant.id} objective is invalid or repeated.`)
    }
    objectiveIds.add(variant.objective.id)
    requireFinite(variant.objective.maxHp, `td plan variant ${variant.id} objective maxHp`, Number.EPSILON)
    validateArmor(variant.objective.armor, `td plan variant ${variant.id} objective armor`)
    requireFinite(variant.deploymentSpeedPerSecond, `td plan variant ${variant.id} deploymentSpeedPerSecond`)
    if (variant.mode === 'assault' && variant.deploymentSpeedPerSecond <= 0) {
      throw new Error(`td assault variant ${variant.id} deploymentSpeedPerSecond must be positive.`)
    }
    if (variant.startingBuildResources !== undefined) {
      requireFinite(variant.startingBuildResources, `td plan variant ${variant.id} startingBuildResources`)
    }
    const nodes = new Set(battlefield.laneGraph.nodes.map(node => node.id))
    const edges = new Map(battlefield.laneGraph.edges.map(edge => [edge.id, edge]))
    for (const group of wave.groups) {
      let nodeId = battlefield.spawnerNodeId
      for (const edgeId of group.routeEdgeIds) {
        const edge = edges.get(edgeId)
        if (!edge || edge.fromNodeId !== nodeId) {
          throw new Error(`td variant ${variant.id} group ${group.id} route is not contiguous from the spawner.`)
        }
        nodeId = edge.toNodeId
      }
      if (nodeId !== variant.objective.nodeId) {
        throw new Error(`td variant ${variant.id} group ${group.id} route does not reach the objective.`)
      }
      if (variant.mode === 'defense' && group.speedPerSecond <= 0) {
        throw new Error(`td defense variant ${variant.id} group ${group.id} must move.`)
      }
      if (variant.mode === 'assault' && (!group.stationNodeId || !nodes.has(group.stationNodeId))) {
        throw new Error(`td assault variant ${variant.id} group ${group.id} needs a known stationNodeId.`)
      }
    }
    if (!variant.deferredReason) liveVariants.push(variant)
  }

  for (const rawUnit of units) {
    if (!isRecord(rawUnit) || rawUnit.deferredReason) continue
    if (liveCombat && (td.enabled || rawUnit.loadouts !== undefined)) {
      if (!isRecord(rawUnit.td)) throw new Error(`live unit ${String(rawUnit.id)} needs a td profile.`)
      const profile = rawUnit.td
      requireFinite(profile.maxHp, `unit ${String(rawUnit.id)} td.maxHp`, Number.EPSILON)
      requireFinite(profile.attackRange, `unit ${String(rawUnit.id)} td.attackRange`, Number.EPSILON)
      requirePositiveInteger(profile.attackIntervalTicks, `unit ${String(rawUnit.id)} td.attackIntervalTicks`)
      if (profile.healing !== undefined) {
        if (!isRecord(profile.healing)) throw new Error(`unit ${String(rawUnit.id)} td.healing must be an object.`)
        requireFinite(profile.healing.range, `unit ${String(rawUnit.id)} td.healing.range`)
        requirePositiveInteger(profile.healing.intervalTicks, `unit ${String(rawUnit.id)} td.healing.intervalTicks`)
        requireFinite(profile.healing.amountPerUnit, `unit ${String(rawUnit.id)} td.healing.amountPerUnit`, Number.EPSILON)
        requirePositiveInteger(profile.healing.chargesPerUnit, `unit ${String(rawUnit.id)} td.healing.chargesPerUnit`)
      }
      const weapon = combatEquipment.get(String(profile.weaponEquipmentId))
      if (!weapon || weapon.kind !== 'weapon' || weapon.deferredReason) {
        throw new Error(`unit ${String(rawUnit.id)} references an unavailable TD weapon.`)
      }
      if (profile.armorEquipmentId !== undefined) {
        const armor = combatEquipment.get(String(profile.armorEquipmentId))
        if (!armor || armor.kind === 'weapon' || armor.deferredReason) {
          throw new Error(`unit ${String(rawUnit.id)} references unavailable TD armor.`)
        }
      }
      if (!Array.isArray(rawUnit.equipmentCosts) || rawUnit.equipmentCosts.length === 0) {
        throw new Error(`live unit ${String(rawUnit.id)} needs equipmentCosts when combat is enabled.`)
      }
      for (const cost of rawUnit.equipmentCosts) {
        if (!isRecord(cost) || typeof cost.equipmentId !== 'string' || !equipmentStockIds.has(cost.equipmentId)) {
          throw new Error(`unit ${String(rawUnit.id)} references unknown equipment stock.`)
        }
        requireFinite(cost.amount, `unit ${String(rawUnit.id)} equipment cost`, Number.EPSILON)
      }
      if (!Array.isArray(rawUnit.loadouts) || rawUnit.loadouts.length === 0) {
        const baseCostIds = new Set(rawUnit.equipmentCosts.flatMap(cost => (
          isRecord(cost) && typeof cost.equipmentId === 'string' ? [cost.equipmentId] : []
        )))
        for (const [equipmentId, definition] of [
          [String(profile.weaponEquipmentId), weapon],
          ...(profile.armorEquipmentId === undefined
            ? []
            : [[String(profile.armorEquipmentId), combatEquipment.get(String(profile.armorEquipmentId))]]),
        ] as Array<[string, Record<string, unknown> | undefined]>) {
          if (definition?.technologyId !== undefined && !baseCostIds.has(equipmentId)) {
            throw new Error(`unit ${String(rawUnit.id)} must consume its technology-linked equipment ${equipmentId}.`)
          }
        }
      }
    }
    validateLoadouts(rawUnit.loadouts, `unit ${String(rawUnit.id)}`)
  }

  if (!td.enabled) return
  if (!liveCombat) throw new Error('td.enabled requires combat.enabled.')
  if (liveVariants.length === 0) throw new Error('td.enabled requires a live plan variant.')
  if (td.regionalCatalogEnabled) {
    const fieldRegions = new Set(td.battlefields.map(field => field.regionId))
    if (td.battlefields.length !== regionIds.length
      || regionIds.some(regionId => !fieldRegions.has(regionId))
      || fieldRegions.size !== regionIds.length) {
      throw new Error('td regional catalog must contain exactly one authored battlefield region set.')
    }
    for (const regionId of regionIds) {
      for (const grade of [1, 2, 3, 4]) {
        if (!gradeKeys.has(`${regionId}:${grade}`)) {
          throw new Error(`td regional catalog is missing ${regionId} grade ${grade}.`)
        }
      }
      if (!liveVariants.some(variant => variant.mode === 'defense'
        && battlefieldById.get(variant.battlefieldId)?.regionId === regionId)) {
        throw new Error(`td regional catalog is missing a live ${regionId} defense variant.`)
      }
    }
    if (!liveVariants.some(variant => variant.mode === 'assault')) {
      throw new Error('td regional catalog requires a live assault variant.')
    }
  }
  if (td.equipmentProduction.length === 0) {
    throw new Error('td.equipmentProduction must not be empty when td.enabled is true.')
  }
}

export function validateEmpiresConfig(value: unknown): asserts value is EmpiresEndgameConfig {
  if (!isRecord(value)) throw new Error('Конфигурация должна быть JSON-объектом.')
  if (value.schemaVersion !== EMPIRES_CONFIG_SCHEMA_VERSION) {
    throw new Error(`Поддерживается только schemaVersion ${EMPIRES_CONFIG_SCHEMA_VERSION}.`)
  }
  if (typeof value.id !== 'string' || !value.id.trim()) throw new Error('У конфигурации отсутствует id.')
  if (typeof value.title !== 'string' || !value.title.trim()) throw new Error('У конфигурации отсутствует title.')
  if (!Array.isArray(value.cards)) throw new Error('Поле cards должно быть массивом.')
  if (value.cards.length !== 53) throw new Error(`В колоде должно быть 53 карты, сейчас: ${value.cards.length}.`)

  const cardIds = new Set<string>()
  for (const rawCard of value.cards) {
    if (!isRecord(rawCard) || typeof rawCard.id !== 'string' || !rawCard.id) {
      throw new Error('Каждой карте нужен непустой id.')
    }
    if (cardIds.has(rawCard.id)) throw new Error(`Повторяется id карты: ${rawCard.id}.`)
    cardIds.add(rawCard.id)
    if (!isRecord(rawCard.normal) || !isRecord(rawCard.inverted)) {
      throw new Error(`У карты ${rawCard.id} должны быть normal и inverted стороны.`)
    }
  }
  if (!Array.isArray(value.mysticCards)) throw new Error('Поле mysticCards должно быть массивом.')
  if (!isRecord(value.tavern)) throw new Error('Отсутствуют настройки Таверны.')
  if (!isRecord(value.alchemy)) throw new Error('Отсутствуют настройки Алхимии.')
  if (!isRecord(value.inventory)) throw new Error('Отсутствуют настройки упаковки инвентаря.')
  if (!isRecord(value.expeditions)) throw new Error('Отсутствуют настройки экспедиций.')

  if (!isRecord(value.durak)) throw new Error('Отсутствуют настройки карточной партии.')
  if (!isRecord(value.upgrades)) throw new Error('Отсутствуют настройки улучшений.')
  if (!isRecord(value.gifts) || !Array.isArray(value.gifts.definitions)) {
    throw new Error('Отсутствует каталог божественных даров.')
  }
  if (!isRecord(value.empire)) throw new Error('Отсутствуют настройки имперской фазы.')
  if (!isRecord(value.governance)) throw new Error('Отсутствуют настройки управления империей.')
  validateScaffoldSection(value.combat, 'combat', [
    'damageTypes',
    'armorClasses',
    'counterRules',
    'equipment',
  ])
  validateScaffoldSection(value.td, 'td', [
    'towerBases',
    'battlefields',
    'towers',
    'gradeChoices',
    'waves',
    'planVariants',
  ])
  validateGodConfig(value.god, cardIds)
  validateScaffoldSection(value.quests, 'quests', ['definitions'])
  validateSeasonsConfig(value as unknown as EmpiresEndgameConfig)
  if (!Array.isArray(value.empire.cities)) throw new Error('Поле empire.cities должно быть массивом.')
  if (!isRecord(value.empire.map) || !Array.isArray(value.empire.map.regions) || value.empire.map.regions.length !== 5) {
    throw new Error('На карте должно быть ровно пять регионов.')
  }
  if (
    !Array.isArray(value.empire.buildings)
    || !Array.isArray(value.empire.technologies)
    || !Array.isArray(value.empire.events)
  ) {
    throw new Error('Каталоги зданий, технологий и событий должны быть массивами.')
  }
  if (value.empire.units !== undefined && !Array.isArray(value.empire.units)) {
    throw new Error('Каталог войск empire.units должен быть массивом.')
  }
  if (!Array.isArray(value.empire.resources)) {
    throw new Error('Каталог ресурсов empire.resources должен быть массивом.')
  }

  validateCombatConfig(value.combat, value.empire.technologies)
  validateTdConfig(
    value.td,
    value.combat,
    value.empire.units ?? [],
    value.empire.technologies,
    value.empire.map.regions.map(region => region.id),
  )

  const config = value as unknown as EmpiresEndgameConfig
  const questErrors = validateEmpiresQuestsConfig(config)
  if (questErrors.length > 0) throw new Error(questErrors.join('\n'))
  validateTechnologySidesAndHiddenCombinations(config)
  validateEpidemicAndMedicalConfig(config)
  validateDomesticEconomyConfig(config)
  validateTavernConfig(config)
  validateAlchemyConfig(config)
  validateExpeditionsConfig(config)
  validateInventoryConfig(config)
  validateExternalEconomyConfig(config)
  validateEconomyContentConfig(config)
  validateLoyaltyConfig(config)
  validatePoliticalEffects(config)
  validateSteelResearchConfig(config)
  const cityIds = new Set(config.empire.cities.map(city => city.id))
  for (const building of config.empire.buildings) {
    if (building.allowedCityIds !== undefined) {
      const allowed = validateUniqueStringList(
        building.allowedCityIds,
        `building ${building.id} allowedCityIds`,
      )
      for (const cityId of allowed) {
        if (cityIds.size > 0 && !cityIds.has(cityId)) {
          throw new Error(`building ${building.id} references unknown allowed city ${cityId}.`)
        }
      }
    }
  }
  const deferredErrors = validateDeferredReasons(config)
  if (deferredErrors.length > 0) throw new Error(deferredErrors.join('\n'))
  const liveEffectErrors = validateLiveEffects(config)
  if (liveEffectErrors.length > 0) throw new Error(liveEffectErrors.join('\n'))
  const resolutionErrors = validateGiftResolutions(config)
  if (resolutionErrors.length > 0) throw new Error(resolutionErrors.join('\n'))
  validateGovernanceConfig(config)
  if (value.empire.cities.length === 0) throw new Error('Нужен хотя бы один город.')

  const engineErrors = validateEmpiresEndgameConfig(config)
  if (engineErrors.length > 0) throw new Error(engineErrors.join('\n'))
}

function migrateAndValidateEmpiresConfig(value: unknown): EmpiresEndgameConfig {
  const migrated = migrateEmpiresConfig(value)
  validateEmpiresConfig(migrated)
  return migrated
}

export function cloneEmpiresConfig(config: unknown): EmpiresEndgameConfig {
  // Configs are JSON data, but Vue passes this helper reactive Proxies from the
  // page and Builder props. Browsers reject Proxy objects in structuredClone.
  return migrateAndValidateEmpiresConfig(config)
}

export function parseEmpiresConfig(text: string): EmpiresEndgameConfig {
  const value: unknown = JSON.parse(text)
  return migrateAndValidateEmpiresConfig(value)
}

export async function loadBundledEmpiresConfig(): Promise<EmpiresEndgameConfig> {
  const response = await fetch(EMPIRES_CONFIG_URL, { cache: 'no-cache' })
  if (!response.ok) throw new Error(`Не удалось загрузить игру: HTTP ${response.status}.`)
  const value: unknown = await response.json()
  return migrateAndValidateEmpiresConfig(value)
}

export async function loadEmpiresConfig(): Promise<EmpiresEndgameConfig> {
  const custom = window.localStorage.getItem(EMPIRES_CONFIG_STORAGE_KEY)
  if (custom) {
    try {
      return parseEmpiresConfig(custom)
    }
    catch (error) {
      console.warn('Empire\'s Endgame custom configuration is invalid; loading bundled defaults.', error)
    }
  }

  return loadBundledEmpiresConfig()
}

export function saveEmpiresConfig(config: EmpiresEndgameConfig) {
  const migrated = migrateAndValidateEmpiresConfig(config)
  window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(migrated))
}

export function clearCustomEmpiresConfig() {
  window.localStorage.removeItem(EMPIRES_CONFIG_STORAGE_KEY)
}

export function downloadEmpiresJson(filename: string, value: unknown) {
  const blob = new Blob([`${JSON.stringify(value, null, 2)}\n`], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.click()
  URL.revokeObjectURL(url)
}

export async function readEmpiresJsonFile(file: File): Promise<EmpiresEndgameConfig> {
  return parseEmpiresConfig(await file.text())
}
