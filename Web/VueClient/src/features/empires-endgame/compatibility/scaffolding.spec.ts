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

function makeV1Config(): UnknownRecord {
  const legacy = jsonClone(defaultConfigJson) as UnknownRecord
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
  it('runs the explicit v1 to v2 to v3 chain without mutation and idempotently clones v3', () => {
    const legacy = makeV1Config()
    const original = jsonClone(legacy)

    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 3,
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
      },
      god: { enabled: false, lines: [], deckMemoryRules: [], antiBitoRules: [] },
      quests: { enabled: false, definitions: [], dialogueGraphs: [] },
      empire: {
        seasons: { enabled: false, definitions: [] },
        loyalty: { enabled: false, cityRules: [], regionRules: [] },
      },
    })
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(migrateEmpiresConfig(migrated)).not.toBe(migrated)
  })

  it('rejects an unknown future v4 config before validation', () => {
    const future = jsonClone(defaultConfigJson) as UnknownRecord
    future.schemaVersion = 4

    expect(() => migrateEmpiresConfig(future)).toThrow(
      /Unsupported future Empire's Endgame config schemaVersion 4/,
    )
    expect(() => parseEmpiresConfig(JSON.stringify(future))).toThrow(/future.*schemaVersion 4/i)
  })

  it('migrates a previous-v2 custom TD catalog into one safe central plan', () => {
    const legacy = makeV2Config()
    const before = jsonClone(legacy)

    const migrated = cloneEmpiresConfig(legacy)

    expect(legacy).toEqual(before)
    expect(migrated.schemaVersion).toBe(3)
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
    expect(partial.td.equipmentProduction).toEqual([{
      equipmentId: 'basic-kit',
      amountPerSmithCapacity: 1,
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

    expect(cloneEmpiresConfig(legacy).schemaVersion).toBe(3)
    expect(parseEmpiresConfig(JSON.stringify(legacy)).schemaVersion).toBe(3)
    expect((await readEmpiresJsonFile(new File(
      [JSON.stringify(legacy)],
      'legacy-empires-config.json',
      { type: 'application/json' },
    ))).schemaVersion).toBe(3)

    window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(legacy))
    expect((await loadEmpiresConfig()).schemaVersion).toBe(3)
    window.localStorage.removeItem(EMPIRES_CONFIG_STORAGE_KEY)

    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => jsonClone(legacy),
    })))
    expect((await loadBundledEmpiresConfig()).schemaVersion).toBe(3)
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
        pendingLoyaltyDeltas: [],
        morale: 0,
        maxMorale: 2,
        veterans: {},
        recruitmentPenalties: {},
      },
      external: { allianceThreat: 0, nextWaveCon: 2, pendingOffers: [] },
      epidemics: [],
      quests: {},
      cityLoyalty: config.empire.cities.map(() => 0),
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
