import { afterEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../../public/empires-endgame/game-config.json'
import {
  EMPIRES_CONFIG_STORAGE_KEY,
  cloneEmpiresConfig,
  loadBundledEmpiresConfig,
  loadEmpiresConfig,
  migrateEmpiresConfig,
  migrateEmpiresConfigOneStep,
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
import { authenticEmpiresV1Config } from './authentic-v1-config.fixture'
import { authenticEmpiresV1SaveChain } from './authentic-v1-save-chain.fixture'
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
  delete empire.domesticEconomy
  delete empire.externalEconomy
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
  return jsonClone(authenticEmpiresV1Config()) as UnknownRecord
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

  const centralChoiceIds = new Set((currentTd.gradeChoices as UnknownRecord[])
    .filter(set => set.regionId === 'center')
    .flatMap(set => Array.isArray(set.choiceIds) ? set.choiceIds as string[] : []))
  const legacyTowers = (jsonClone(currentTd.towers) as UnknownRecord[])
    .filter(tower => typeof tower.id === 'string' && centralChoiceIds.has(tower.id))
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
    epidemicCompaction: state.epidemicCompaction,
    domesticEconomy: state.empire.domesticEconomy,
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
  it('runs the explicit v1 through v19 chain without mutation and idempotently clones v19', () => {
    const legacy = makeV1Config()
    const original = jsonClone(legacy)

    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 19,
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
      god: {
        enabled: false,
        deckMemory: { enabled: false, availability: 'always', inspectionsPerCon: 0 },
        antiBito: { enabled: false, minimumConsecutiveBito: 1, returnCount: 1, maxInterventions: 0 },
        lines: [],
        dialogueLogRetention: 24,
        mercyConfirmation: { enabled: false },
      },
      quests: {
        enabled: false,
        historyRetention: 64,
        triggerHistoryRetention: 64,
        definitions: [],
      },
      inventory: {
        enabled: false,
        itemDefinitions: [],
      },
      empire: {
        seasons: {
          enabled: false,
          definitions: [],
          foodRounding: 'none',
          greenhouse: null,
        },
        hiddenCombinations: { enabled: false, definitions: [] },
        domesticEconomy: { enabled: false },
        externalEconomy: { enabled: false },
        economyContent: { enabled: false },
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

  it('matches the authentic pre-P0 artifact through direct and explicit one-step migration', () => {
    const authentic = makeV1Config()
    const untouched = jsonClone(authentic)
    expect(Object.keys(authentic).sort()).toEqual([
      'cards', 'durak', 'empire', 'gifts', 'id', 'schemaVersion', 'seed', 'title', 'upgrades',
    ])

    const direct = migrateEmpiresConfig(authentic)
    let sequential: unknown = authentic
    for (let version = 1; version < 19; version += 1) {
      const input = sequential
      const before = jsonClone(sequential)
      sequential = migrateEmpiresConfigOneStep(sequential)
      expect(sequential).toMatchObject({ schemaVersion: version + 1 })
      expect(input).toEqual(before)
    }
    sequential = migrateEmpiresConfig(sequential)

    expect(authentic).toEqual(untouched)
    expect(sequential).toEqual(direct)
    expect(() => validateEmpiresConfig(sequential)).not.toThrow()
  })

  it('restores a representative immutable custom config from every schema generation', () => {
    const generations: UnknownRecord[] = []
    let generation = makeV1Config()
    generations.push(jsonClone(generation))
    while (Number(generation.schemaVersion) < 19) {
      generation = migrateEmpiresConfigOneStep(generation) as UnknownRecord
      generations.push(jsonClone(generation))
    }

    expect(generations.map(item => item.schemaVersion)).toEqual(
      Array.from({ length: 19 }, (_, index) => index + 1),
    )
    for (const candidate of generations) {
      const version = Number(candidate.schemaVersion)
      candidate.id = `custom-schema-${version}`
      candidate.seed = `custom-seed-${version}`
      candidate.title = `Custom schema ${version}`
      ;(candidate.empire as UnknownRecord).daysPerPhase = 40 + version

      if (version === 13) {
        const god = candidate.god as UnknownRecord
        god.dialogueLogRetention = 19
        god.lines = [{
          id: 'custom-god-line',
          trigger: 'giftOffered',
          text: 'Custom preserved God line.',
          weight: 2,
          once: true,
        }]
      }
      if (version === 14) {
        const tavern = candidate.tavern as UnknownRecord
        const rumors = tavern.rumors as UnknownRecord
        tavern.historyRetention = 17
        rumors.fallbackText = 'Custom preserved rumor.'
        candidate.mysticCards = [{
          id: 'custom-mystic',
          name: 'Custom Mystic',
          owner: 'player',
          startsInverted: true,
          returnDelayCons: 5,
          normal: { title: 'Custom Face', description: 'Custom normal face.' },
          inverted: { title: 'Custom Reverse', description: 'Custom inverted face.' },
          deferredReason: 'Custom fixture remains unavailable.',
        }]
      }
      if (version === 15) {
        const alchemy = candidate.alchemy as UnknownRecord
        alchemy.dayCost = 7
        alchemy.colors = ['red']
        alchemy.pieces = [{
          id: 'custom-alchemy-piece',
          name: 'Custom Piece',
          cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }],
        }]
        alchemy.recipes = [{
          id: 'custom-alchemy-recipe',
          name: 'Custom Recipe',
          description: 'Custom preserved disabled recipe.',
          mode: 'assembly',
          family: 'experiment',
          initialCells: [{ x: 10, y: 10, color: 'gray' }],
          targetCells: [{ x: 10, y: 9, color: 'red' }],
          pieceDefinitionIds: ['custom-alchemy-piece'],
          prerequisites: [],
          rewards: [],
          deferredReason: 'Custom fixture remains unavailable.',
        }]
      }
      if (version === 16) {
        const expeditions = candidate.expeditions as UnknownRecord
        const map = (candidate.empire as UnknownRecord).map as UnknownRecord
        const regionId = String(((map.regions as UnknownRecord[])[0]).id)
        expeditions.resultHistoryRetention = 13
        expeditions.zones = [{
          id: 'custom-expedition-zone',
          name: 'Custom Zone',
          regionId,
          subregionIds: [],
          rewards: [],
        }]
        expeditions.enemyProfiles = [{
          id: 'custom-expedition-profile',
          name: 'Custom Profile',
          regionId,
          waveId: 'custom-disabled-wave',
          description: 'Custom preserved disabled enemy profile.',
        }]
      }
      if (version === 17) {
        const inventory = candidate.inventory as UnknownRecord
        const empire = candidate.empire as UnknownRecord
        const resourceId = String(((empire.resources as UnknownRecord[])[0]).id)
        inventory.targetUnitsPerItem = 321
        inventory.itemDefinitions = [{
          id: 'custom-inventory-item',
          name: 'Custom Item',
          cells: [{ x: 0, y: 0 }, { x: 0, y: 1 }],
          weight: 2,
          content: { kind: 'resource', resourceId },
          deferredReason: 'Custom fixture remains unavailable.',
        }]
      }

      const before = jsonClone(candidate)
      const restored = cloneEmpiresConfig(candidate)
      expect(candidate).toEqual(before)
      expect(restored).toMatchObject({
        schemaVersion: 19,
        id: `custom-schema-${version}`,
        seed: `custom-seed-${version}`,
        title: `Custom schema ${version}`,
        empire: { daysPerPhase: 40 + version },
      })
      if (version === 13) {
        expect(restored.god).toMatchObject({
          dialogueLogRetention: 19,
          lines: [{
            id: 'custom-god-line',
            trigger: 'giftOffered',
            text: 'Custom preserved God line.',
            weight: 2,
            once: true,
          }],
        })
      }
      if (version === 14) {
        expect(restored.tavern).toMatchObject({
          historyRetention: 17,
          rumors: { fallbackText: 'Custom preserved rumor.' },
        })
        expect(restored.mysticCards).toEqual([
          expect.objectContaining({ id: 'custom-mystic', returnDelayCons: 5 }),
        ])
      }
      if (version === 15) {
        expect(restored.alchemy).toMatchObject({
          dayCost: 7,
          colors: ['red'],
          pieces: [expect.objectContaining({ id: 'custom-alchemy-piece' })],
          recipes: [expect.objectContaining({
            id: 'custom-alchemy-recipe',
            pieceDefinitionIds: ['custom-alchemy-piece'],
          })],
        })
      }
      if (version === 16) {
        expect(restored.expeditions).toMatchObject({
          resultHistoryRetention: 13,
          zones: [expect.objectContaining({ id: 'custom-expedition-zone' })],
          enemyProfiles: [expect.objectContaining({ id: 'custom-expedition-profile' })],
        })
      }
      if (version === 17) {
        expect(restored.inventory).toMatchObject({
          targetUnitsPerItem: 321,
          itemDefinitions: [expect.objectContaining({ id: 'custom-inventory-item', weight: 2 })],
        })
      }
    }

    const dangling = currentConfig()
    dangling.inventory.itemDefinitions[0].content = { kind: 'resource', resourceId: 'missing-resource' }
    expect(() => validateEmpiresConfig(dangling)).toThrow(/inventory.*resource|unknown.*resource/i)
  })

  it('rejects an unknown future v20 config before validation', () => {
    const future = jsonClone(defaultConfigJson) as UnknownRecord
    future.schemaVersion = 20

    expect(() => migrateEmpiresConfig(future)).toThrow(
      /Unsupported future Empire's Endgame config schemaVersion 20/,
    )
    expect(() => parseEmpiresConfig(JSON.stringify(future))).toThrow(/future.*schemaVersion 20/i)
  })

  it('migrates an immediate-previous regional v3 config without mutating stored custom JSON', async () => {
    const previous = makeV3Config()
    const original = jsonClone(previous)
    const migrated = cloneEmpiresConfig(previous)

    expect(previous).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 19,
      td: { regionalCatalogEnabled: true },
      empire: { steelResearch: { forkSourcePriceMultiplier: 2 } },
    })
    expect(migrated.empire.buildings.find(building => building.id === 'building-foundry')?.allowedCityIds)
      .toEqual(expect.not.arrayContaining(['city-tetrakor-capital']))
    expect(migrated.empire.buildings.find(building => building.id === 'building-foundry')?.allowedCityIds)
      .toHaveLength(24)
    expect(migrated.td.equipmentProductionLines.reduce(
      (total, line) => total + line.capacityShare,
      0,
    )).toBeLessThanOrEqual(1)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()

    window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(previous))
    expect((await loadEmpiresConfig()).schemaVersion).toBe(19)
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
    expect(migrated.schemaVersion).toBe(19)
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

    expect(cloneEmpiresConfig(legacy).schemaVersion).toBe(19)
    expect(parseEmpiresConfig(JSON.stringify(legacy)).schemaVersion).toBe(19)
    expect((await readEmpiresJsonFile(new File(
      [JSON.stringify(legacy)],
      'legacy-empires-config.json',
      { type: 'application/json' },
    ))).schemaVersion).toBe(19)

    window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(legacy))
    expect((await loadEmpiresConfig()).schemaVersion).toBe(19)
    window.localStorage.removeItem(EMPIRES_CONFIG_STORAGE_KEY)

    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => jsonClone(legacy),
    })))
    expect((await loadBundledEmpiresConfig()).schemaVersion).toBe(19)
  })

  it('keeps all unrelated deferred carriers unchanged across the full chain', () => {
    const legacy = makeV1Config()
    const migrated = migrateEmpiresConfig(legacy) as UnknownRecord
    const withoutEventIndexes = (value: unknown) => deferredReasonPaths(value)
      .filter(path => !path.startsWith('/empire/events/')
        && !path.startsWith('/tavern/')
        && !path.startsWith('/mysticCards/')
        && !path.startsWith('/alchemy/')
        && !path.startsWith('/expeditions/')
        && !path.startsWith('/inventory/')
        && !path.startsWith('/clash/')
        && !path.startsWith('/chess/')
        && !path.endsWith('/payload/deferredReason'))
    expect(withoutEventIndexes(migrated)).toEqual(withoutEventIndexes(legacy))

    const questEventIds = new Set(['event-golden-idol', 'event-witch-apprenticeship'])
    const eventDeferredCarriers = (value: UnknownRecord) => {
      const empire = value.empire as UnknownRecord
      return ((empire.events as UnknownRecord[]) ?? [])
        .filter(event => !questEventIds.has(String(event.id)))
        .map(event => ({
          eventId: event.id,
          deferredReason: event.deferredReason,
          choices: ((event.choices as UnknownRecord[]) ?? []).map(choice => ({
            choiceId: choice.id,
            deferredReason: choice.deferredReason,
          })),
        }))
    }
    expect(eventDeferredCarriers(migrated)).toEqual(eventDeferredCarriers(legacy))
  })

  it('retains the exact completed P2 carrier manifest', () => {
    const bundled = currentConfig()
    expect(bundled.empire.units.map(unit => unit.id)).toEqual([
      'unit-light',
      'unit-regular',
      'unit-heavy',
      'unit-knight',
      'unit-medical-healer',
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

    expect(() => importEmpiresCampaign({ ...envelope, schemaVersion: 19 }, config.id))
      .toThrow(/версии 1–18/)
    expect(() => importEmpiresCampaign({
      ...envelope,
      schemaVersion: 18,
      state: { ...(envelope.state as UnknownRecord), schemaVersion: 19 },
    }, config.id)).toThrow(/версии 1–18/)
  })

  it('restores the authentic v1 save directly and through every historical schema checkpoint', () => {
    const config = currentConfig()
    const checkpoints = authenticEmpiresV1SaveChain()
    const untouched = jsonClone(checkpoints)
    const expectedCommits = [
      'b1dba5d8',
      'd21c8e08',
      'f9dde5df',
      '79602d95',
      '1940a5bf',
      'a73a4c2c',
      '5e57d597',
      '38aab631',
      'e7d1b902',
      'e775f3cf',
      'c9b524a0',
      'b64b3c0a',
      'e789bc98',
      '49f96d9a',
      '473015d4',
    ]
    expect(checkpoints.map(checkpoint => checkpoint.sourceCommit)).toEqual(expectedCommits)
    expect(checkpoints.map(checkpoint => checkpoint.envelope.schemaVersion)).toEqual(
      Array.from({ length: 15 }, (_, index) => index + 1),
    )
    expect(checkpoints.map(checkpoint => checkpoint.envelope.state.schemaVersion)).toEqual(
      Array.from({ length: 15 }, (_, index) => index + 1),
    )

    const restore = (envelope: unknown, expectedSourceVersion: number) => {
      const imported = importEmpiresCampaign(envelope, config.id)
      expect(imported.schemaVersion).toBe(expectedSourceVersion)
      return new EmpiresEndgameEngine(config, imported).snapshot()
    }
    const direct = restore(checkpoints[0]!.envelope, 1)
    // The v15 checkpoint is the result of restoring the same v1 state through
    // each historical production engine exactly once, not a relabelled v1.
    const sequential = restore(checkpoints.at(-1)!.envelope, 15)

    expect(sequential).toEqual(direct)
    for (const checkpoint of checkpoints) {
      expect(restore(checkpoint.envelope, checkpoint.envelope.schemaVersion)).toEqual(direct)
    }
    expect(checkpoints).toEqual(untouched)

    const currentEnvelope = {
      schemaVersion: 18,
      savedAt: checkpoints[0]!.envelope.savedAt,
      state: direct,
    }
    const currentBefore = jsonClone(currentEnvelope)
    expect(restore(currentEnvelope, 18)).toEqual(direct)
    expect(currentEnvelope).toEqual(currentBefore)
  })

  it('preserves a legacy state version through envelope import until engine normalization', () => {
    const config = currentConfig()
    const legacy = jsonClone(new EmpiresEndgameEngine(config).snapshot()) as unknown as UnknownRecord
    legacy.schemaVersion = 4
    ;(legacy.durak as UnknownRecord).trumpSuit = 'clubs'
    delete legacy.governance
    const envelope = {
      schemaVersion: 4,
      savedAt: '2026-07-21T00:00:00.000Z',
      state: legacy,
    }
    const untouched = jsonClone(envelope)

    const imported = importEmpiresCampaign(envelope, config.id)
    expect(imported.schemaVersion).toBe(4)
    expect(envelope).toEqual(untouched)
    const restored = new EmpiresEndgameEngine(config, imported)
    expect(restored.state.schemaVersion).toBe(18)
    expect(restored.state.governance.advisors['advisor-grand']).toMatchObject({
      status: 'active',
      transitionSourceId: 'migration:legacy-restricted-trump',
    })
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
    legacy.schemaVersion = 1
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
        settledThroughSequence: 0,
        legacySettledSessionIds: [],
      },
      army: {
        equipmentStock: {},
        foundryInstantReadyConByCity: {},
        morale: 0,
        maxMorale: 2,
        unitInstances: {},
        nextUnitSequence: 1,
        recruitmentPenalties: {},
      },
      external: fresh.state.external,
      epidemics: [],
      epidemicCompaction: {
        evictedCount: 0,
        historyDigest: '',
        lastInstanceId: null,
        lastRulesDigest: null,
        maxEvictedSequence: 0,
      },
      domesticEconomy: fresh.state.empire.domesticEconomy,
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
