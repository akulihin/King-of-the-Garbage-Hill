import { afterEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../../public/empires-endgame/game-config.json'
import {
  EMPIRES_CONFIG_STORAGE_KEY,
  cloneEmpiresConfig,
  loadBundledEmpiresConfig,
  loadEmpiresConfig,
  migrateEmpiresConfig,
  parseEmpiresConfig,
  readEmpiresJsonFile,
  validateEmpiresConfig,
} from '../config'
import { EmpiresEndgameEngine } from '../engine'
import {
  EMPIRES_SAVE_STORAGE_KEY,
  exportEmpiresCampaign,
  importEmpiresCampaign,
  loadEmpiresCampaign,
  saveEmpiresCampaign,
} from '../persistence'
import { digestEmpiresQaState } from '../qa'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from '../types'

type UnknownRecord = Record<string, unknown>

function jsonClone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

/** Remove fields that did not exist before the schema-v4 steel/equipment bridge. */
function stripSchemaV4Fields(legacy: UnknownRecord): void {
  const empire = legacy.empire as UnknownRecord
  empire.loyalty = { enabled: false, cityRules: [], regionRules: [] }
  delete empire.steelResearch
  for (const technology of empire.technologies as UnknownRecord[]) {
    delete technology.steel
    delete technology.deferredSubfeatures
  }
  for (const building of empire.buildings as UnknownRecord[]) {
    delete building.allowedCityIds
    delete building.deferredSubfeatures
  }
  for (const unit of (empire.units as UnknownRecord[] | undefined) ?? []) delete unit.loadouts
  const combat = legacy.combat as UnknownRecord | undefined
  for (const equipment of (combat?.equipment as UnknownRecord[] | undefined) ?? []) {
    delete equipment.technologyId
  }
  const td = legacy.td as UnknownRecord | undefined
  delete td?.equipmentProductionLines
  for (const towerBase of (td?.towerBases as UnknownRecord[] | undefined) ?? []) delete towerBase.loadouts
  if (Array.isArray(td?.equipmentProduction)) {
    td.equipmentProduction = (td.equipmentProduction as UnknownRecord[]).map(recipe => ({
      equipmentId: recipe.equipmentId,
      amountPerSmithCapacity: recipe.amountPerSmithCapacity,
    }))
  }
}

function makeV1Config(): UnknownRecord {
  const legacy = jsonClone(defaultConfigJson) as UnknownRecord
  stripSchemaV4Fields(legacy)
  legacy.schemaVersion = 1
  delete legacy.combat
  delete legacy.td
  delete legacy.god
  delete legacy.quests
  const empire = legacy.empire as UnknownRecord
  delete empire.seasons
  delete empire.loyalty
  return legacy
}

/** A real pre-P3 custom config shape, before regional fields and rules caps existed. */
function makeV2Config(): UnknownRecord {
  const legacy = jsonClone(defaultConfigJson) as UnknownRecord
  stripSchemaV4Fields(legacy)
  legacy.schemaVersion = 2
  const currentTd = (legacy.td as UnknownRecord)
  const centralField = jsonClone(
    (currentTd.battlefields as UnknownRecord[]).find(field => field.id === 'battlefield-central')!,
  )
  const centralWave = jsonClone(
    (currentTd.waves as UnknownRecord[]).find(wave => wave.id === 'alliance-central-wave')!,
  )
  const centralVariant = (currentTd.planVariants as UnknownRecord[])
    .find(variant => variant.id === 'central-castle-defense')!
  const centralObjective = centralVariant.objective as UnknownRecord
  const centralBase = jsonClone(
    (currentTd.towerBases as UnknownRecord[]).find(base => base.id === 'tower-base-center')!,
  )

  delete centralBase.regionId
  delete centralBase.categoryIds
  delete centralBase.cost
  for (const spot of centralField.buildSpots as UnknownRecord[]) delete spot.terrainId
  centralField.mode = 'defense'
  centralField.castleNodeId = centralField.objectiveNodeId
  centralField.castleMaxHp = centralObjective.maxHp
  centralField.castleArmor = centralObjective.armor
  delete centralField.regionId
  delete centralField.objectiveNodeId
  delete centralField.towerBaseIds
  delete centralField.allowedTowerCategoryIds
  delete centralField.modifiers
  for (const group of centralWave.groups as UnknownRecord[]) {
    delete group.categoryIds
    delete group.stationNodeId
  }

  const legacyTowers = jsonClone(currentTd.towers) as UnknownRecord[]
  for (const tower of legacyTowers) delete tower.categoryIds
  legacy.td = {
    enabled: true,
    tickMs: currentTd.tickMs,
    maxTicks: currentTd.maxTicks,
    waveEveryCons: currentTd.waveEveryCons,
    startingBuildResources: currentTd.startingBuildResources,
    towerBase: centralBase,
    alliance: currentTd.alliance,
    settlement: currentTd.settlement,
    morale: currentTd.morale,
    equipmentProduction: currentTd.equipmentProduction,
    battlefields: [centralField],
    towers: legacyTowers,
    waves: [centralWave],
  }
  return legacy
}

/** Immediate P3A predecessor: regional TD is present, steel/equipment-v4 fields are not. */
function makeV3Config(): UnknownRecord {
  const legacy = jsonClone(defaultConfigJson) as UnknownRecord
  stripSchemaV4Fields(legacy)
  legacy.schemaVersion = 3
  return legacy
}

function currentConfig(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(defaultConfigJson)
}

function deferredReasonPaths(value: unknown, path = ''): string[] {
  if (Array.isArray(value)) {
    return value.flatMap((entry, index) => deferredReasonPaths(entry, `${path}/${index}`))
  }
  if (typeof value !== 'object' || value === null) return []
  return Object.entries(value).flatMap(([key, entry]) => key === 'deferredReason'
    ? [`${path}/deferredReason`]
    : deferredReasonPaths(entry, `${path}/${key}`))
}

function scaffoldState(state: EmpiresCampaignState) {
  return {
    minigame: state.minigame,
    minigameResultLog: state.minigameResultLog,
    minigameResultCompaction: state.minigameResultCompaction,
    army: state.army,
    external: state.external,
    epidemics: state.epidemics,
    quests: state.quests,
    cityLoyalty: state.empire.cities.map(city => city.loyalty),
    reputation: state.empire.reputation,
    loyalty: state.empire.loyalty,
    chronicle: state.empire.chronicle,
    nextChronicleSequence: state.empire.nextChronicleSequence,
    godInterventions: state.durak.godInterventions,
  }
}

afterEach(() => {
  window.localStorage.removeItem(EMPIRES_CONFIG_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_SAVE_STORAGE_KEY)
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('Empire\'s Endgame full-chain compatibility scaffolding', () => {
  it('runs the explicit v1 to v2 to v3 to v4 to v5 chain without mutation and idempotently clones v5', () => {
    const legacy = makeV1Config()
    const original = jsonClone(legacy)

    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 5,
      combat: {
        enabled: false,
        damageTypes: [],
        armorClasses: [],
        counterRules: [],
        equipment: [],
      },
      td: {
        enabled: false,
        regionalCatalogEnabled: false,
        maxCommands: 128,
        resultLogLimit: 32,
        maxCatchUpTicksPerFrame: 8,
        battlefields: [],
        towers: [],
        gradeChoices: [],
        waves: [],
        planVariants: [],
        equipmentProductionLines: [{
          id: 'legacy-smithy-1',
          capacityFlagId: 'smithCapacity',
          capacityShare: 1,
        }],
      },
      god: { enabled: false, lines: [], deckMemoryRules: [], antiBitoRules: [] },
      quests: { enabled: false, definitions: [], dialogueGraphs: [] },
      empire: {
        seasons: { enabled: false, definitions: [] },
        loyalty: {
          enabled: false,
          minimum: -9,
          maximum: 9,
          initialReputation: 0,
          chronicleRetention: 64,
        },
        steelResearch: {
          forkSourcePriceMultiplier: 2,
          delayedFreeEmpirePhases: 2,
          militaryEliteFlagId: 'militaryElite',
        },
      },
    })
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(migrateEmpiresConfig(migrated)).not.toBe(migrated)
  })

  it('rejects an unknown future v6 config before validation', () => {
    const future = jsonClone(defaultConfigJson) as UnknownRecord
    future.schemaVersion = 6

    expect(() => migrateEmpiresConfig(future)).toThrow(
      /Unsupported future Empire's Endgame config schemaVersion 6/,
    )
    expect(() => parseEmpiresConfig(JSON.stringify(future))).toThrow(/future.*schemaVersion 6/i)
  })

  it('migrates an immediate-previous regional v3 config without mutating stored custom JSON', async () => {
    const previous = makeV3Config()
    const original = jsonClone(previous)
    const migrated = cloneEmpiresConfig(previous)

    expect(previous).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 5,
      td: { regionalCatalogEnabled: true },
      empire: { steelResearch: { forkSourcePriceMultiplier: 2 } },
    })
    expect(migrated.empire.buildings.find(building => building.id === 'building-foundry')?.allowedCityIds)
      .toEqual(expect.not.arrayContaining(['city-tetrakor-capital']))
    expect(migrated.empire.buildings.find(building => building.id === 'building-foundry')?.allowedCityIds)
      .toHaveLength(12)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()

    window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(previous))
    expect((await loadEmpiresConfig()).schemaVersion).toBe(5)
  })

  it('preserves an explicit empty equipment catalog in a disabled immediate-v3 config', async () => {
    const previous = makeV3Config()
    const td = previous.td as UnknownRecord
    td.enabled = false
    td.regionalCatalogEnabled = false
    td.towerBases = []
    td.battlefields = []
    td.towers = []
    td.gradeChoices = []
    td.waves = []
    td.planVariants = []
    td.equipmentProduction = []
    const original = jsonClone(previous)

    const migrated = cloneEmpiresConfig(previous)

    expect(previous).toEqual(original)
    expect(migrated.td.equipmentProduction).toEqual([])
    expect(migrated.td.equipmentProductionLines).toEqual([])
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(previous))
    expect((await loadEmpiresConfig()).td.equipmentProduction).toEqual([])
  })

  it('migrates a previous-v2 custom TD catalog into one safe central plan', () => {
    const legacy = makeV2Config()
    const before = jsonClone(legacy)

    const migrated = cloneEmpiresConfig(legacy)

    expect(legacy).toEqual(before)
    expect(migrated.schemaVersion).toBe(5)
    expect(migrated.td.regionalCatalogEnabled).toBe(false)
    expect(migrated.td.towerBases).toEqual([
      expect.objectContaining({
        id: 'tower-base-center',
        regionId: 'center',
        categoryIds: ['tower'],
        cost: 0,
      }),
    ])
    expect(migrated.td.battlefields).toEqual([
      expect.objectContaining({
        id: 'battlefield-central',
        regionId: 'center',
        objectiveNodeId: 'central-objective',
        towerBaseIds: ['tower-base-center'],
        modifiers: [],
      }),
    ])
    expect(migrated.td.gradeChoices).toHaveLength(4)
    expect(migrated.td.gradeChoices!.every(set => set.choiceIds.length === 4)).toBe(true)
    expect(migrated.td.planVariants).toEqual([
      expect.objectContaining({
        id: 'legacy-central-defense',
        mode: 'defense',
        battlefieldId: 'battlefield-central',
        waveId: 'alliance-central-wave',
      }),
    ])
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
  })

  it('gives missing or partial v2 TD sections a disabled non-regional fallback', () => {
    const missingSection = makeV2Config()
    delete missingSection.td
    const missing = parseEmpiresConfig(JSON.stringify(missingSection))
    expect(missing.td).toMatchObject({
      enabled: false,
      regionalCatalogEnabled: false,
      tickMs: 50,
      maxTicks: 4000,
      maxCommands: 128,
      resultLogLimit: 32,
      maxCatchUpTicksPerFrame: 8,
      battlefields: [],
      towers: [],
      gradeChoices: [],
      waves: [],
      planVariants: [],
    })
    expect(missing.td.towerBases?.[0]).toMatchObject({
      id: 'tower-generic',
      regionId: 'center',
      categoryIds: ['tower'],
    })
    expect(() => validateEmpiresConfig(missing)).not.toThrow()

    const partialSection = makeV2Config()
    partialSection.td = { enabled: false, battlefields: [], towers: [], waves: [] }
    const partial = parseEmpiresConfig(JSON.stringify(partialSection))
    expect(partial.td.regionalCatalogEnabled).toBe(false)
    expect(partial.td.battlefields).toEqual([])
    expect(partial.td.equipmentProductionLines).toEqual([{
      id: 'legacy-smithy-1',
      capacityFlagId: 'smithCapacity',
      capacityShare: 1,
    }])
    expect(partial.td.equipmentProduction).toEqual([{
      id: 'legacy-recipe-basic-kit',
      equipmentId: 'basic-kit',
      lineId: 'legacy-smithy-1',
      amountPerSmithCapacity: 1,
      priority: 0,
    }])
    expect(partial.td.planVariants).toEqual([])
  })

  it('accepts disabled current scaffolds and rejects an enabled incomplete TD catalog', () => {
    const disabled = cloneEmpiresConfig(makeV1Config())
    expect(() => validateEmpiresConfig(disabled)).not.toThrow()

    const incomplete = cloneEmpiresConfig(disabled)
    incomplete.td.enabled = true
    expect(() => validateEmpiresConfig(incomplete))
      .toThrow(/td\.battlefields must not be empty when td\.enabled is true/)

    const missingCombat = currentConfig()
    missingCombat.combat.enabled = false
    expect(() => validateEmpiresConfig(missingCombat)).toThrow(/td\.enabled requires combat\.enabled/)
  })

  it('routes bundled, stored, JSON import, and clone boundaries through migration', async () => {
    const legacy = makeV2Config()

    expect(cloneEmpiresConfig(legacy).schemaVersion).toBe(5)
    expect(parseEmpiresConfig(JSON.stringify(legacy)).schemaVersion).toBe(5)
    expect((await readEmpiresJsonFile(new File(
      [JSON.stringify(legacy)],
      'legacy-empires-config.json',
      { type: 'application/json' },
    ))).schemaVersion).toBe(5)

    window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(legacy))
    expect((await loadEmpiresConfig()).schemaVersion).toBe(5)
    window.localStorage.removeItem(EMPIRES_CONFIG_STORAGE_KEY)

    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => jsonClone(legacy),
    })))
    expect((await loadBundledEmpiresConfig()).schemaVersion).toBe(5)
  })

  it('keeps all unrelated deferred carriers unchanged across the full chain', () => {
    const legacy = makeV1Config()
    expect(deferredReasonPaths(migrateEmpiresConfig(legacy))).toEqual(deferredReasonPaths(legacy))
  })

  it('retains the exact completed P2 carrier manifest', () => {
    const bundled = currentConfig()
    expect(bundled.empire.units.map(unit => unit.id)).toEqual([
      'unit-light',
      'unit-regular',
      'unit-heavy',
      'unit-knight',
    ])
    expect(bundled.empire.buildings
      .filter(building => ['building-barracks', 'building-smithy'].includes(building.id))
      .map(building => building.id)
      .sort()).toEqual(['building-barracks', 'building-smithy'])
    expect(bundled.empire.technologies
      .filter(technology => ['doctrine-war', 'tech-ironwork'].includes(technology.id))
      .map(technology => technology.id)
      .sort()).toEqual(['doctrine-war', 'tech-ironwork'])
    const heartSeven = bundled.cards.find(card => card.suit === 'hearts' && card.rank === '7')
    expect(heartSeven).toBeDefined()
    expect(heartSeven?.normal.deferredReason).toBeUndefined()
    expect(heartSeven?.inverted.deferredReason).toBeUndefined()
    expect(bundled.gifts.definitions.find(gift => gift.id === 'gift-combat-spirit'))
      .toBeDefined()
    expect(bundled.gifts.definitions.find(gift => gift.id === 'gift-combat-spirit')?.deferredReason)
      .toBeUndefined()
  })

  it('migrates a genuine v2 aggregate army into frozen cohorts and rejects future save versions', () => {
    const config = currentConfig()
    const state = jsonClone(new EmpiresEndgameEngine(config).snapshot()) as unknown as UnknownRecord
    state.schemaVersion = 2
    const city = ((state.empire as UnknownRecord).cities as UnknownRecord[])[0]
    city.recruitedUnits = { 'unit-regular': 2 }
    delete city.recruitedUnitCohorts
    const envelope: UnknownRecord = {
      schemaVersion: 2,
      savedAt: '2026-07-17T00:00:00.000Z',
      state,
    }
    const original = jsonClone(envelope)

    const imported = importEmpiresCampaign(envelope, config.id)
    expect(envelope).toEqual(original)
    const restored = new EmpiresEndgameEngine(config, imported)
    const cohort = restored.state.empire.cities[0].recruitedUnitCohorts[0]
    expect(cohort).toMatchObject({
      id: `legacy:${restored.state.empire.cities[0].id}:unit-regular`,
      unitId: 'unit-regular',
      loadoutId: 'legacy-default',
      count: 2,
      weaponEquipmentId: 'weapon-laurel-spear',
      weapon: { damageLevels: { piercing: 2, cutting: 2 } },
    })
    expect(new EmpiresEndgameEngine(config, restored.snapshot()).snapshot()).toEqual(restored.snapshot())

    expect(() => importEmpiresCampaign({ ...envelope, schemaVersion: 5 }, config.id))
      .toThrow(/version 1, 2, 3 or 4|версии 1, 2, 3 или 4/)
    expect(() => importEmpiresCampaign({
      ...envelope,
      schemaVersion: 4,
      state: { ...(envelope.state as UnknownRecord), schemaVersion: 5 },
    }, config.id)).toThrow(/version 1, 2, 3 or 4|версии 1, 2, 3 или 4/)
  })

  it('migrates and settles a genuine v2 active TD save with canonical legacy cohort identity', () => {
    const config = currentConfig()
    config.empire.eventChance = 0
    const source = new EmpiresEndgameEngine(config)
    const ready = source.snapshot()
    ready.phase = 'empire'
    ready.con = 2
    ready.empire.daysRemaining = 59
    ready.external.nextWaveCon = 2
    for (const resource of config.empire.resources) ready.empire.resources[resource.id] = 1_000_000_000
    const city = ready.empire.cities.find(candidate => candidate.id === 'city-tetrakor-capital')!
    city.loyalty = config.empire.loyalty.maximum
    city.population = 1_000_000
    city.militaryPopulation = 100
    for (const classId of Object.keys(city.populationClasses)) city.populationClasses[classId] = 1_000_000
    city.resources[config.empire.foodResourceId] = 100_000_000
    city.buildingLevels['building-barracks'] = 5
    city.operationalBuildingLevels['building-barracks'] = 5
    ready.empire.researchedTechnologyIds.push('doctrine-war')
    ready.army.equipmentStock['basic-kit'] = 10
    source.restore(ready)
    const recruitment = source.recruitUnits(city.id, 'unit-light')
    if (!recruitment.ok) throw new Error(recruitment.message)
    expect(source.finishEmpire()).toMatchObject({ ok: true })
    expect(source.state.phase).toBe('minigame')

    const legacy = jsonClone(source.snapshot()) as unknown as UnknownRecord
    legacy.schemaVersion = 2
    const legacyEmpire = legacy.empire as UnknownRecord
    for (const rawCity of legacyEmpire.cities as UnknownRecord[]) {
      const cohorts = (rawCity.recruitedUnitCohorts as UnknownRecord[] | undefined) ?? []
      rawCity.recruitedUnits = Object.fromEntries(cohorts.map(cohort => [cohort.unitId, cohort.count]))
      delete rawCity.recruitedUnitCohorts
    }
    const session = legacy.minigame as UnknownRecord
    const plan = session.plan as UnknownRecord
    delete plan.equipmentStock
    for (const deployment of plan.deployments as UnknownRecord[]) delete deployment.cohortId
    delete session.rulesIdentity
    delete plan.rulesIdentity
    const envelope = { schemaVersion: 2, savedAt: '2026-07-17T00:00:00.000Z', state: legacy }

    const restored = new EmpiresEndgameEngine(
      config,
      importEmpiresCampaign(envelope, config.id),
    )
    const deployment = restored.state.minigame!.plan.deployments[0]
    expect(deployment.cohortId).toBe(`legacy:${deployment.cityId}:${deployment.unitId}`)
    expect(restored.state.minigame!.plan.equipmentStock).toEqual({})
    expect(restored.abortMinigame()).toMatchObject({ ok: true })
  })

  it('rejects an immediate P3A active save with an explicit stale rules identity', () => {
    const config = currentConfig()
    config.empire.eventChance = 0
    const source = new EmpiresEndgameEngine(config)
    const ready = source.snapshot()
    ready.phase = 'empire'
    ready.con = 2
    ready.empire.daysRemaining = 59
    ready.external.nextWaveCon = 2
    for (const resource of config.empire.resources) ready.empire.resources[resource.id] = 1_000_000
    for (const city of ready.empire.cities) city.resources[config.empire.foodResourceId] = 1_000_000
    source.restore(ready)
    expect(source.finishEmpire()).toMatchObject({ ok: true })
    expect(source.state.phase).toBe('minigame')

    const legacy = jsonClone(source.snapshot()) as unknown as UnknownRecord
    legacy.schemaVersion = 3
    const session = legacy.minigame as UnknownRecord
    const plan = session.plan as UnknownRecord
    session.rulesIdentity = { configSchemaVersion: 3, rulesDigest: 'legacy-p3a' }
    plan.rulesIdentity = { configSchemaVersion: 3, rulesDigest: 'legacy-p3a' }
    const envelope = { schemaVersion: 3, savedAt: '2026-07-17T00:00:00.000Z', state: legacy }
    const imported = importEmpiresCampaign(envelope, config.id)

    expect(() => new EmpiresEndgameEngine(config, imported))
      .toThrow(/active minigame rules identity does not match the loaded configuration/i)
  })

  it('rejects malformed frozen cohort profiles while preserving valid old cohorts', () => {
    const config = currentConfig()
    const fresh = new EmpiresEndgameEngine(config)
    const malformed = jsonClone(fresh.snapshot()) as unknown as UnknownRecord
    const empire = malformed.empire as UnknownRecord
    const city = (empire.cities as UnknownRecord[])[0]
    city.recruitedUnitCohorts = [{
      id: 'malformed-cohort',
      unitId: 'unit-light',
      loadoutId: 'legacy-default',
      count: 1,
      weapon: {},
      armor: null,
    }]

    expect(() => new EmpiresEndgameEngine(config, malformed as unknown as EmpiresCampaignState))
      .toThrow(/invalid frozen weapon profile/i)
  })

  it('normalizes a pre-P2 v1 save and round-trips it without gameplay drift', () => {
    const config = currentConfig()
    const fresh = new EmpiresEndgameEngine(config)
    const legacy = jsonClone(fresh.snapshot()) as unknown as UnknownRecord
    delete legacy.minigame
    delete legacy.minigameResultLog
    delete legacy.minigameResultCompaction
    delete legacy.army
    delete legacy.external
    delete legacy.epidemics
    delete legacy.quests
    delete (legacy.durak as UnknownRecord).godInterventions
    const legacyEmpire = legacy.empire as UnknownRecord
    for (const city of legacyEmpire.cities as UnknownRecord[]) delete city.loyalty
    const envelope = {
      schemaVersion: 1,
      savedAt: '2026-07-16T00:00:00.000Z',
      state: legacy,
    }

    const restored = new EmpiresEndgameEngine(
      config,
      importEmpiresCampaign(envelope, config.id),
    )

    expect(scaffoldState(restored.snapshot())).toEqual({
      minigame: null,
      minigameResultLog: [],
      minigameResultCompaction: {
        evictedCount: 0,
        historyDigest: '',
        lastSessionId: null,
        lastRulesDigest: null,
      },
      army: {
        equipmentStock: {},
        foundryInstantReadyConByCity: {},
        morale: 0,
        maxMorale: 2,
        veterans: {},
        recruitmentPenalties: {},
      },
      external: { allianceThreat: 0, nextWaveCon: 2, pendingOffers: [] },
      epidemics: [],
      quests: {},
      cityLoyalty: config.empire.cities.map(() => 0),
      reputation: 0,
      loyalty: fresh.state.empire.loyalty,
      chronicle: [],
      nextChronicleSequence: 1,
      godInterventions: 0,
    })
    expect(scaffoldState(restored.snapshot())).toEqual(scaffoldState(fresh.snapshot()))
    expect(digestEmpiresQaState(restored)).toEqual(digestEmpiresQaState(fresh))
    expect(restored.currentActor()).toBe(fresh.currentActor())
    const actor = fresh.currentActor()
    if (!actor) throw new Error('Fresh campaign did not start on a card turn.')
    expect(restored.legalAttackCardIds(actor)).toEqual(fresh.legalAttackCardIds(actor))

    saveEmpiresCampaign(restored.snapshot())
    const loaded = loadEmpiresCampaign(config.id)
    expect(loaded).not.toBeNull()
    const loadedEngine = new EmpiresEndgameEngine(config, loaded ?? undefined)
    expect(loadedEngine.snapshot()).toEqual(restored.snapshot())

    const importedAgain = importEmpiresCampaign(
      exportEmpiresCampaign(loadedEngine.snapshot()),
      config.id,
    )
    expect(new EmpiresEndgameEngine(config, importedAgain).snapshot()).toEqual(restored.snapshot())
  })
})
