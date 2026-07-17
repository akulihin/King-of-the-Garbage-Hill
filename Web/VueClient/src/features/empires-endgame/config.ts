import type {
  CombatArmorProfile,
  CombatCounterRule,
  CombatDamageAutoPriority,
  CombatWeaponProfile,
  EmpiresCombatConfig,
} from './combat/types'
import type { EmpiresTdConfig } from './td/types'
import type {
  EmpiresBuildingSlotKind,
  EmpiresCampaignState,
  EmpiresEffect,
  EmpiresEndgameConfig,
  EmpiresLoyaltyConfig,
  EmpiresSeasonsConfig,
} from './types'
import { validateEmpiresEndgameConfig } from './engine'

export const EMPIRES_CONFIG_URL = '/empires-endgame/game-config.json'
export const EMPIRES_CONFIG_STORAGE_KEY = 'empires-endgame:config:v1'
export const EMPIRES_CONFIG_SCHEMA_VERSION = 7
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
  lines: [],
  deckMemoryRules: [],
  antiBitoRules: [],
}

const QUESTS_SCAFFOLD = {
  enabled: false,
  definitions: [],
  dialogueGraphs: [],
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
  return migrated
}

const EMPIRES_BUILDING_SLOT_KINDS = new Set<EmpiresBuildingSlotKind>([
  'farm',
  'lumber',
  'mine',
  'smithy',
  'barracks',
  'unique',
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
  'peasantProductivityPercent',
  'productionBoostAssignmentLimit',
  'productionBoostPercent',
  'provisionEfficiencyPercent',
  'recruitmentDisabled',
  'relicsUnlocked',
  'smithyWithoutIron',
  'smithSpecializationLocked',
  'smithCapacity',
  'stableWithoutLivestock',
  'starvationLossMultiplierPercent',
  'surplusFoodPerGold',
  'templarTransferLossPercent',
  'treasuryGoldPerSavedMillion',
  'theocracy',
  'unlimitedTavernRecruitment',
])

function validateLiveEffects(config: EmpiresEndgameConfig): string[] {
  const errors: string[] = []
  const dependencyFlagIds = new Set<string>()
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
  validateScaffoldSection(value.god, 'god', ['lines', 'deckMemoryRules', 'antiBitoRules'])
  validateScaffoldSection(value.quests, 'quests', ['definitions', 'dialogueGraphs'])
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
  validateTechnologySidesAndHiddenCombinations(config)
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
