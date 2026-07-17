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

describe('Empire\'s Endgame Phase 0 compatibility scaffolding', () => {
  it('migrates v1 to v2 without mutation and keeps current-version migration idempotent', () => {
    const legacy = makeV1Config()
    const original = jsonClone(legacy)

    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 2,
      combat: {
        enabled: false,
        damageTypes: [],
        armorClasses: [],
        counterRules: [],
        equipment: [],
      },
      td: { enabled: false, battlefields: [], towers: [], waves: [] },
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

  it('rejects an unknown future config version before validation', () => {
    const future = jsonClone(defaultConfigJson) as UnknownRecord
    future.schemaVersion = 3

    expect(() => migrateEmpiresConfig(future)).toThrow(
      /Unsupported future Empire's Endgame config schemaVersion 3/,
    )
    expect(() => parseEmpiresConfig(JSON.stringify(future))).toThrow(/future.*schemaVersion 3/i)
  })

  it('accepts disabled empty sections and rejects every enabled incomplete section specifically', () => {
    const config = cloneEmpiresConfig(makeV1Config())
    expect(() => validateEmpiresConfig(config)).not.toThrow()

    const cases: Array<{
      path: string
      mutate: (candidate: EmpiresEndgameConfig) => void
      message: RegExp
    }> = [
      {
        path: 'combat',
        mutate: candidate => { candidate.combat.enabled = true },
        message: /combat\.damageTypes must not be empty when combat\.enabled is true/,
      },
      {
        path: 'td',
        mutate: candidate => { candidate.td.enabled = true },
        message: /td\.battlefields must not be empty when td\.enabled is true/,
      },
      {
        path: 'god',
        mutate: candidate => { candidate.god.enabled = true },
        message: /god\.lines must not be empty when god\.enabled is true/,
      },
      {
        path: 'quests',
        mutate: candidate => { candidate.quests.enabled = true },
        message: /quests\.definitions must not be empty when quests\.enabled is true/,
      },
      {
        path: 'empire.seasons',
        mutate: candidate => { candidate.empire.seasons.enabled = true },
        message: /empire\.seasons\.definitions must not be empty when empire\.seasons\.enabled is true/,
      },
      {
        path: 'empire.loyalty',
        mutate: candidate => { candidate.empire.loyalty.enabled = true },
        message: /empire\.loyalty\.cityRules must not be empty when empire\.loyalty\.enabled is true/,
      },
    ]

    for (const fixture of cases) {
      const candidate = cloneEmpiresConfig(config)
      fixture.mutate(candidate)
      expect(() => validateEmpiresConfig(candidate), fixture.path).toThrow(fixture.message)
    }
  })

  it('backfills missing Phase-0 combat keys in v2 without copying the bundled catalog', () => {
    const missingSection = jsonClone(defaultConfigJson) as UnknownRecord
    delete missingSection.combat

    expect(migrateEmpiresConfig(missingSection)).toMatchObject({
      schemaVersion: 2,
      combat: {
        enabled: false,
        damageTypes: [],
        armorClasses: [],
        counterRules: [],
        equipment: [],
      },
    })
    expect(() => parseEmpiresConfig(JSON.stringify(missingSection))).not.toThrow()

    const partialSection = jsonClone(defaultConfigJson) as UnknownRecord
    partialSection.combat = { enabled: false }
    const normalized = parseEmpiresConfig(JSON.stringify(partialSection))

    expect(normalized.combat).toEqual({
      enabled: false,
      damageTypes: [],
      armorClasses: [],
      counterRules: [],
      equipment: [],
    })
  })

  it('routes bundled, stored, JSON import, and clone boundaries through the migration chain', async () => {
    const legacy = makeV1Config()

    expect(cloneEmpiresConfig(legacy).schemaVersion).toBe(2)
    expect(parseEmpiresConfig(JSON.stringify(legacy)).schemaVersion).toBe(2)
    expect((await readEmpiresJsonFile(new File(
      [JSON.stringify(legacy)],
      'legacy-empires-config.json',
      { type: 'application/json' },
    ))).schemaVersion).toBe(2)

    window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(legacy))
    expect((await loadEmpiresConfig()).schemaVersion).toBe(2)
    window.localStorage.removeItem(EMPIRES_CONFIG_STORAGE_KEY)

    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => jsonClone(legacy),
    })))
    expect((await loadBundledEmpiresConfig()).schemaVersion).toBe(2)
  })

  it('keeps the complete deferred carrier set unchanged across migration', () => {
    const legacy = makeV1Config()
    const before = deferredReasonPaths(legacy)
    const after = deferredReasonPaths(migrateEmpiresConfig(legacy))

    expect(before).toHaveLength(175)
    expect(after).toEqual(before)
  })

  it('normalizes a pre-phase v1 save exactly and round-trips it without gameplay drift', () => {
    const config = currentConfig()
    const fresh = new EmpiresEndgameEngine(config)
    const legacy = jsonClone(fresh.snapshot()) as unknown as UnknownRecord
    delete legacy.minigame
    delete legacy.minigameResultLog
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
      army: {
        equipmentStock: {},
        pendingLoyaltyDeltas: [],
        morale: 0,
        veterans: {},
      },
      external: { allianceThreat: 0, pendingOffers: [] },
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
