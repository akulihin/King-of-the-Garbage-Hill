import type {
  CombatArmorProfile,
  CombatCounterRule,
  CombatDamageAutoPriority,
  CombatWeaponProfile,
  EmpiresCombatConfig,
} from './combat/types'
import type { EmpiresTdConfig } from './td/types'
import type { EmpiresBuildingSlotKind, EmpiresEndgameConfig } from './types'
import { validateEmpiresEndgameConfig } from './engine'

export const EMPIRES_CONFIG_URL = '/empires-endgame/game-config.json'
export const EMPIRES_CONFIG_STORAGE_KEY = 'empires-endgame:config:v1'
export const EMPIRES_CONFIG_SCHEMA_VERSION = 2

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

const TD_SCAFFOLD = {
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
}

const LOYALTY_SCAFFOLD = {
  enabled: false,
  cityRules: [],
  regionRules: [],
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
  config.td = withScaffoldDefaults(config.td, TD_SCAFFOLD)
  config.god = withScaffoldDefaults(config.god, GOD_SCAFFOLD)
  config.quests = withScaffoldDefaults(config.quests, QUESTS_SCAFFOLD)
  if (isRecord(config.empire)) {
    config.empire.seasons = withScaffoldDefaults(config.empire.seasons, SEASONS_SCAFFOLD)
    config.empire.loyalty = withScaffoldDefaults(config.empire.loyalty, LOYALTY_SCAFFOLD)
  }
  config.schemaVersion = 2
  return config
}

function normalizeEmpiresConfigV2(config: Record<string, unknown>): Record<string, unknown> {
  config.combat = withScaffoldDefaults(config.combat, COMBAT_SCAFFOLD)
  config.td = withScaffoldDefaults(config.td, TD_SCAFFOLD)
  return config
}

const EMPIRES_CONFIG_MIGRATIONS: Record<
  number,
  (config: Record<string, unknown>) => Record<string, unknown>
> = {
  1: migrateEmpiresConfigV1ToV2,
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

  for (const card of config.cards) {
    check(`card ${card.id} normal face`, card.normal.deferredReason)
    check(`card ${card.id} inverted face`, card.inverted.deferredReason)
  }
  for (const gift of config.gifts.definitions) check(`gift ${gift.id}`, gift.deferredReason)
  for (const resource of config.empire.resources) check(`resource ${resource.id}`, resource.deferredReason)
  for (const building of config.empire.buildings) check(`building ${building.id}`, building.deferredReason)
  for (const unit of config.empire.units ?? []) check(`unit ${unit.id}`, unit.deferredReason)
  for (const technology of config.empire.technologies) {
    check(`technology ${technology.id}`, technology.deferredReason)
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
  'militaryArson',
  'maxCombatSpirit',
  'peasantProductivityPercent',
  'productionBoostAssignmentLimit',
  'productionBoostPercent',
  'provisionEfficiencyPercent',
  'recruitmentDisabled',
  'relicsUnlocked',
  'smithyWithoutIron',
  'smithCapacity',
  'stableWithoutLivestock',
  'starvationLossMultiplierPercent',
  'surplusFoodPerGold',
  'templarTransferLossPercent',
  'treasuryGoldPerSavedMillion',
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

function validateTdConfig(
  value: unknown,
  combat: unknown,
  units: readonly unknown[],
): void {
  if (!isRecord(value)) throw new Error('td must be an object.')
  const td = value as unknown as EmpiresTdConfig
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
  requirePositiveInteger(td.waveEveryCons, 'td.waveEveryCons')
  requireFinite(td.startingBuildResources, 'td.startingBuildResources')

  if (!td.towerBase || !isRecord(td.towerBase)) throw new Error('td.towerBase must be an object.')
  if (!td.alliance || !isRecord(td.alliance)) throw new Error('td.alliance must be an object.')
  if (!td.settlement || !isRecord(td.settlement)) throw new Error('td.settlement must be an object.')
  if (!td.morale || !isRecord(td.morale)) throw new Error('td.morale must be an object.')
  if (!Array.isArray(td.equipmentProduction)) throw new Error('td.equipmentProduction must be an array.')
  if (!Array.isArray(td.battlefields) || !Array.isArray(td.towers) || !Array.isArray(td.waves)) {
    throw new Error('td battlefields, towers, and waves must be arrays.')
  }

  requireFinite(td.towerBase.maxHp, 'td.towerBase.maxHp', Number.EPSILON)
  requireFinite(td.towerBase.range, 'td.towerBase.range', Number.EPSILON)
  requirePositiveInteger(td.towerBase.attackIntervalTicks, 'td.towerBase.attackIntervalTicks')
  requirePositiveInteger(td.towerBase.projectiles, 'td.towerBase.projectiles')
  if (!td.towerBase.id?.trim() || !td.towerBase.name?.trim()) {
    throw new Error('td.towerBase needs an id and name.')
  }
  if (td.towerBase.targetPriority !== 'first' && td.towerBase.targetPriority !== 'strongest') {
    throw new Error('td.towerBase.targetPriority is unknown.')
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

  const equipmentStockIds = new Set<string>()
  for (const definition of td.equipmentProduction) {
    if (!definition.equipmentId?.trim()) throw new Error('td equipment production needs an equipmentId.')
    if (equipmentStockIds.has(definition.equipmentId)) {
      throw new Error(`td equipment production repeats ${definition.equipmentId}.`)
    }
    equipmentStockIds.add(definition.equipmentId)
    requireFinite(
      definition.amountPerSmithCapacity,
      `td equipment ${definition.equipmentId} amountPerSmithCapacity`,
    )
  }

  const battlefieldIds = new Set<string>()
  for (const battlefield of td.battlefields) {
    if (!battlefield.id?.trim()) throw new Error('td battlefield needs an id.')
    if (battlefieldIds.has(battlefield.id)) throw new Error(`td repeats battlefield ${battlefield.id}.`)
    battlefieldIds.add(battlefield.id)
    if (battlefield.mode !== 'defense') throw new Error(`td battlefield ${battlefield.id} has an unknown mode.`)
    requireFinite(battlefield.width, `td battlefield ${battlefield.id} width`, Number.EPSILON)
    requireFinite(battlefield.height, `td battlefield ${battlefield.id} height`, Number.EPSILON)
    requireFinite(battlefield.castleMaxHp, `td battlefield ${battlefield.id} castleMaxHp`, Number.EPSILON)
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
    for (const endpoint of [battlefield.spawnerNodeId, battlefield.castleNodeId, battlefield.deploymentNodeId]) {
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
    for (const spot of battlefield.buildSpots) {
      if (!spot.id?.trim()) throw new Error(`td battlefield ${battlefield.id} has a build spot without an id.`)
      if (spotIds.has(spot.id)) throw new Error(`td battlefield ${battlefield.id} repeats a build spot.`)
      spotIds.add(spot.id)
      requireFinite(spot.x, `td battlefield ${battlefield.id} spot ${spot.id} x`, Number.NEGATIVE_INFINITY)
      requireFinite(spot.y, `td battlefield ${battlefield.id} spot ${spot.id} y`, Number.NEGATIVE_INFINITY)
    }
  }

  const towerIds = new Set<string>()
  for (const tower of td.towers) {
    if (!tower.id?.trim()) throw new Error('td tower choice needs an id.')
    if (towerIds.has(tower.id)) throw new Error(`td repeats tower choice ${tower.id}.`)
    towerIds.add(tower.id)
    if (![1, 2, 3, 4].includes(tower.grade)) throw new Error(`td tower ${tower.id} has an unknown grade.`)
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
      requireFinite(bonus, `td tower ${tower.id} ${damageTypeId} damage bonus`, Number.NEGATIVE_INFINITY)
    }
  }

  const allEdgeIds = new Set(td.battlefields.flatMap(field => field.laneGraph.edges.map(edge => edge.id)))
  const waveIds = new Set<string>()
  for (const wave of td.waves) {
    if (!wave.id?.trim()) throw new Error('td wave needs an id.')
    if (waveIds.has(wave.id)) throw new Error(`td repeats wave ${wave.id}.`)
    waveIds.add(wave.id)
    if (wave.groups.length === 0) throw new Error(`td wave ${wave.id} needs an enemy group.`)
    const groupIds = new Set<string>()
    for (const group of wave.groups) {
      if (!group.id?.trim()) throw new Error(`td wave ${wave.id} has a group without an id.`)
      if (groupIds.has(group.id)) throw new Error(`td wave ${wave.id} repeats group ${group.id}.`)
      groupIds.add(group.id)
      requirePositiveInteger(group.count, `td wave ${wave.id} group ${group.id} count`)
      if (!Number.isInteger(group.startTick) || group.startTick < 0) {
        throw new Error(`td wave ${wave.id} group ${group.id} startTick must be a non-negative integer.`)
      }
      requirePositiveInteger(group.spawnIntervalTicks, `td wave ${wave.id} group ${group.id} spawnIntervalTicks`)
      requireFinite(group.maxHp, `td wave ${wave.id} group ${group.id} maxHp`, Number.EPSILON)
      requireFinite(group.speedPerSecond, `td wave ${wave.id} group ${group.id} speedPerSecond`, Number.EPSILON)
      requireFinite(group.attackRange, `td wave ${wave.id} group ${group.id} attackRange`)
      requirePositiveInteger(group.attackIntervalTicks, `td wave ${wave.id} group ${group.id} attackIntervalTicks`)
      if (!group.routeEdgeIds.length || group.routeEdgeIds.some(edgeId => !allEdgeIds.has(edgeId))) {
        throw new Error(`td wave ${wave.id} group ${group.id} references an unknown route edge.`)
      }
    }
  }

  if (!td.enabled) return
  if (!isRecord(combat) || combat.enabled !== true) throw new Error('td.enabled requires combat.enabled.')
  if (td.battlefields.length !== 1) {
    throw new Error('Phase-2 td.enabled requires exactly one central battlefield.')
  }
  const liveCombat = combat as unknown as EmpiresCombatConfig
  const damageTypeIds = new Set(liveCombat.damageTypes.map(definition => definition.id))
  const armorClassIds = new Set(liveCombat.armorClasses.map(definition => definition.id))
  const validateWeapon = (profile: CombatWeaponProfile, path: string) => {
    if (!profile || !isRecord(profile.damageLevels) || !Array.isArray(profile.tags)) {
      throw new Error(`${path} must be a combat weapon profile.`)
    }
    const levels = Object.entries(profile.damageLevels)
    if (levels.length === 0) throw new Error(`${path} needs at least one damage level.`)
    for (const [damageTypeId, level] of levels) {
      if (!damageTypeIds.has(damageTypeId)) {
        throw new Error(`${path} references unknown damage type ${damageTypeId}.`)
      }
      requireFinite(level, `${path} ${damageTypeId}`, 0)
    }
  }
  const validateArmor = (profile: CombatArmorProfile | null, path: string) => {
    if (profile === null) return
    if (!profile || !armorClassIds.has(profile.classId)) {
      throw new Error(`${path} references an unknown armor class.`)
    }
    requireFinite(profile.level, `${path} level`)
  }
  validateWeapon(td.towerBase.weapon, 'td.towerBase.weapon')
  for (const tower of td.towers) {
    for (const damageTypeId of Object.keys(tower.damageLevelBonuses)) {
      if (!damageTypeIds.has(damageTypeId)) {
        throw new Error(`td tower ${tower.id} references unknown damage type ${damageTypeId}.`)
      }
    }
  }
  const battlefield = td.battlefields[0]
  const battlefieldEdges = new Map(battlefield.laneGraph.edges.map(edge => [edge.id, edge]))
  validateArmor(battlefield.castleArmor, `td battlefield ${battlefield.id} castleArmor`)
  for (const wave of td.waves) {
    for (const group of wave.groups) {
      validateWeapon(group.weapon, `td wave ${wave.id} group ${group.id} weapon`)
      validateArmor(group.armor, `td wave ${wave.id} group ${group.id} armor`)
      let nodeId = battlefield.spawnerNodeId
      for (const edgeId of group.routeEdgeIds) {
        const edge = battlefieldEdges.get(edgeId)
        if (!edge || edge.fromNodeId !== nodeId) {
          throw new Error(`td wave ${wave.id} group ${group.id} route is not contiguous from the spawner.`)
        }
        nodeId = edge.toNodeId
      }
      if (nodeId !== battlefield.castleNodeId) {
        throw new Error(`td wave ${wave.id} group ${group.id} route does not reach the castle.`)
      }
    }
  }
  for (const grade of [1, 2, 3, 4]) {
    if (td.towers.filter(tower => tower.grade === grade).length !== 4) {
      throw new Error(`td.towers requires exactly four grade-${grade} choices when enabled.`)
    }
  }
  if (td.equipmentProduction.length === 0) {
    throw new Error('td.equipmentProduction must not be empty when td.enabled is true.')
  }
  const combatEquipment = new Map((combat.equipment as Array<Record<string, unknown>>)
    .flatMap(entry => typeof entry.id === 'string' ? [[entry.id, entry] as const] : []))
  for (const rawUnit of units) {
    if (!isRecord(rawUnit) || rawUnit.deferredReason) continue
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
      throw new Error(`live unit ${String(rawUnit.id)} needs equipmentCosts when td.enabled is true.`)
    }
    for (const cost of rawUnit.equipmentCosts) {
      if (!isRecord(cost) || typeof cost.equipmentId !== 'string' || !equipmentStockIds.has(cost.equipmentId)) {
        throw new Error(`unit ${String(rawUnit.id)} references unknown equipment stock.`)
      }
      requireFinite(cost.amount, `unit ${String(rawUnit.id)} equipment cost`, Number.EPSILON)
    }
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
  validateScaffoldSection(value.combat, 'combat', [
    'damageTypes',
    'armorClasses',
    'counterRules',
    'equipment',
  ])
  validateScaffoldSection(value.td, 'td', ['battlefields', 'towers', 'waves'])
  validateScaffoldSection(value.god, 'god', ['lines', 'deckMemoryRules', 'antiBitoRules'])
  validateScaffoldSection(value.quests, 'quests', ['definitions', 'dialogueGraphs'])
  validateScaffoldSection(value.empire.seasons, 'empire.seasons', ['definitions'])
  validateScaffoldSection(value.empire.loyalty, 'empire.loyalty', ['cityRules', 'regionRules'])
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
  validateTdConfig(value.td, value.combat, value.empire.units ?? [])

  const config = value as unknown as EmpiresEndgameConfig
  const deferredErrors = validateDeferredReasons(config)
  if (deferredErrors.length > 0) throw new Error(deferredErrors.join('\n'))
  const liveEffectErrors = validateLiveEffects(config)
  if (liveEffectErrors.length > 0) throw new Error(liveEffectErrors.join('\n'))
  const resolutionErrors = validateGiftResolutions(config)
  if (resolutionErrors.length > 0) throw new Error(resolutionErrors.join('\n'))
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
