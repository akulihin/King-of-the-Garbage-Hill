import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../../public/empires-endgame/game-config.json'
import {
  collectEmpiresConfigCarriers,
  summarizeEmpiresContentCoverage,
  validateEmpiresContentCoverage,
  type EmpiresContentCoverageManifest,
} from '../content-coverage'
import {
  EMPIRES_CONTENT_CARRIER_SNAPSHOT,
  EMPIRES_CONTENT_COVERAGE_MANIFEST,
} from '../content-coverage-manifest'
import { EmpiresEndgameEngine } from '../engine'
import { EMPIRES_QA_SCENARIO_NAMES } from '../qa'
import type { EmpiresEndgameConfig } from '../types'

function config(): EmpiresEndgameConfig {
  return structuredClone(bundledConfigJson) as EmpiresEndgameConfig
}

function cloneManifest(): EmpiresContentCoverageManifest {
  return structuredClone(EMPIRES_CONTENT_COVERAGE_MANIFEST)
}

function countProperty(value: unknown, property: string): number {
  if (!value || typeof value !== 'object') return 0
  if (Array.isArray(value)) return value.reduce((total, item) => total + countProperty(item, property), 0)
  const record = value as Record<string, unknown>
  return (Object.hasOwn(record, property) ? 1 : 0)
    + Object.values(record).reduce((total, item) => total + countProperty(item, property), 0)
}

function collectArrays(value: unknown, property: string): unknown[][] {
  if (!value || typeof value !== 'object') return []
  if (Array.isArray(value)) return value.flatMap(item => collectArrays(item, property))
  const record = value as Record<string, unknown>
  const own = Array.isArray(record[property]) ? [record[property] as unknown[]] : []
  return own.concat(Object.values(record).flatMap(item => collectArrays(item, property)))
}

function coverageProjectionFingerprint(manifest: EmpiresContentCoverageManifest): string {
  const projection = {
    sources: manifest.sourceInventory.map(source => [
      source.id,
      source.path,
      source.messageCount,
      source.role,
      source.evidence,
      source.messageIds,
      source.residualDisposition,
      source.residualOwner,
      source.residualConsumer,
      source.residualTestEvidence,
      source.residualDesignerQuestion ?? null,
    ]),
    raw: manifest.rawCatalogGroups.map(group => [
      group.id,
      group.disposition,
      group.rawSources,
      group.owner,
      group.consumer,
      group.testEvidence,
      group.stableIdentities,
      group.expectedLinkedConfigCarrierCount,
      group.linkedConfigCarrierKeys,
      group.designerQuestion ?? null,
    ]),
  }
  const input = JSON.stringify(projection)
  let hash = 0xcbf29ce484222325n
  for (let index = 0; index < input.length; index += 1) {
    hash ^= BigInt(input.charCodeAt(index))
    hash = BigInt.asUintN(64, hash * 0x100000001b3n)
  }
  return hash.toString(16).padStart(16, '0')
}

describe('Empire\'s Endgame Phase 12B content closure', () => {
  it('owns the exact bundled carrier snapshot and leaves no ready-now row', () => {
    const value = config()
    expect(validateEmpiresContentCoverage(value, EMPIRES_CONTENT_COVERAGE_MANIFEST)).toEqual([])
    expect(collectEmpiresConfigCarriers(value)).toHaveLength(1354)
    expect(Object.values(EMPIRES_CONTENT_CARRIER_SNAPSHOT).flat()).toHaveLength(1354)

    expect(summarizeEmpiresContentCoverage(EMPIRES_CONTENT_COVERAGE_MANIFEST)).toEqual({
      config: {
        live: 1160,
        'ready-now': 0,
        'blocked-semantic': 120,
        'blocked-substrate': 11,
        review: 63,
        out: 0,
      },
      raw: {
        live: 67,
        'ready-now': 0,
        'blocked-semantic': 194,
        'blocked-substrate': 52,
        review: 133,
        out: 15,
      },
      configTotal: 1354,
      rawTotal: 461,
      sourceTotal: 33,
      sourceMessageTotal: 1149,
    })
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.configGroups).not.toContainEqual(
      expect.objectContaining({ disposition: 'ready-now' }),
    )
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups).not.toContainEqual(
      expect.objectContaining({ disposition: 'ready-now' }),
    )
  })

  it('fails closed for nested config IDs, marker removal, raw links, source messages, and dispositions', () => {
    const unowned = config()
    unowned.empire.resources.push({
      ...structuredClone(unowned.empire.resources[0]),
      id: 'phase12b-unowned-resource',
    })
    expect(validateEmpiresContentCoverage(unowned, EMPIRES_CONTENT_COVERAGE_MANIFEST))
      .toContain('unowned config carrier resource:phase12b-unowned-resource')

    const markerIntroduced = config()
    markerIntroduced.cards.find(card => card.id === 'card-clubs-2')!.inverted.deferredReason =
      'Synthetic regression marker.'
    expect(validateEmpiresContentCoverage(markerIntroduced, EMPIRES_CONTENT_COVERAGE_MANIFEST))
      .toContain('config carrier card-face:card-clubs-2:inverted changed availability from configured to deferred')

    const nestedUnowned = config()
    const city = nestedUnowned.empire.cities[0]
    city.slots.push({ ...structuredClone(city.slots[0]), id: 'phase12b-unowned-city-slot' })
    expect(validateEmpiresContentCoverage(nestedUnowned, EMPIRES_CONTENT_COVERAGE_MANIFEST))
      .toContain(`unowned config carrier city-slot:${city.id}:phase12b-unowned-city-slot`)

    const missingDisposition = cloneManifest() as unknown as {
      rawCatalogGroups: Array<Record<string, unknown>>
    } & EmpiresContentCoverageManifest
    const currentCards = missingDisposition.rawCatalogGroups
      .find(group => group.id === 'current-authored-card-faces')!
    delete currentCards.disposition
    expect(validateEmpiresContentCoverage(config(), missingDisposition)).toContain(
      'raw coverage group current-authored-card-faces has unknown disposition undefined',
    )

    const missingLink = cloneManifest()
    missingLink.rawCatalogGroups
      .find(group => group.id === 'current-authored-card-faces')!
      .linkedConfigCarrierKeys.pop()
    expect(validateEmpiresContentCoverage(config(), missingLink)).toContain(
      'raw coverage group current-authored-card-faces expected 10 config links but has 9',
    )

    const missingMessage = cloneManifest()
    missingMessage.sourceInventory.find(source => source.id === 'war')!.messageIds!.pop()
    expect(validateEmpiresContentCoverage(config(), missingMessage)).toContain(
      'raw source war expected 230 messageIds but has 229',
    )
  })

  it('fingerprints the maintained source spine and exact raw-group projection', () => {
    // This freezes the maintained manifest projection. It does not read or discover raw files.
    const expectedFingerprint = '2750a78d8ecb4055'
    expect(coverageProjectionFingerprint(EMPIRES_CONTENT_COVERAGE_MANIFEST))
      .toBe(expectedFingerprint)

    const sameCountMessageSubstitution = cloneManifest()
    sameCountMessageSubstitution.sourceInventory
      .find(source => source.id === 'war')!.messageIds![0] = '999999999999999999'
    expect(validateEmpiresContentCoverage(config(), sameCountMessageSubstitution)).toEqual([])
    expect(coverageProjectionFingerprint(sameCountMessageSubstitution)).not.toBe(expectedFingerprint)

    const validButWrongLink = cloneManifest()
    validButWrongLink.rawCatalogGroups
      .find(group => group.id === 'god-anti-bito-too-fast')!
      .linkedConfigCarrierKeys[0] = 'lifecycle:td'
    expect(validateEmpiresContentCoverage(config(), validButWrongLink)).toEqual([])
    expect(coverageProjectionFingerprint(validButWrongLink)).not.toBe(expectedFingerprint)
  })

  it('records every raw source and stable raw identity exactly once', () => {
    const sourceIds = EMPIRES_CONTENT_COVERAGE_MANIFEST.sourceInventory.map(source => source.id)
    const paths = EMPIRES_CONTENT_COVERAGE_MANIFEST.sourceInventory.map(source => source.path)
    expect(new Set(sourceIds).size).toBe(sourceIds.length)
    expect(new Set(paths).size).toBe(paths.length)
    expect(sourceIds).toEqual(expect.arrayContaining([
      'empire-prompt', 'cards', 'characters', 'war', 'steel', 'quests', 'td', 'alchemy',
      'chess', 'zbs', 'population-image',
    ]))
    const messageIds = EMPIRES_CONTENT_COVERAGE_MANIFEST.sourceInventory
      .flatMap(source => source.messageIds ?? [])
    expect(messageIds).toHaveLength(1149)
    expect(new Set(messageIds).size).toBe(messageIds.length)
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.sourceInventory
      .find(source => source.id === 'war')!.messageIds).toHaveLength(230)
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.sourceInventory
      .find(source => source.id === 'doctrines')!.messageIds).toHaveLength(88)
    for (const sourceId of [
      'empire-prompt', 'population-image', 'doctrines', 'steel', 'war', 'construction',
      'cards', 'quests', 'td', 'chess', 'zbs',
    ]) {
      expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.sourceInventory
        .find(source => source.id === sourceId)!.evidence)
        .toMatch(/not content-digested by the 1,149/)
    }

    const rawIdentities = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .flatMap(group => group.stableIdentities)
    expect(new Set(rawIdentities).size).toBe(rawIdentities.length)
    const messageIdsBySource = new Map(EMPIRES_CONTENT_COVERAGE_MANIFEST.sourceInventory
      .map(source => [source.id, new Set(source.messageIds ?? [])]))
    for (const group of EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups) {
      for (const identity of group.stableIdentities) {
        for (const match of identity.matchAll(/(?:^|\+)([a-z][a-z0-9-]*):(\d{18,})(?=[:+])/g)) {
          const [, sourceId, messageId] = match
          if (!group.rawSources.includes(sourceId)) {
            throw new Error(`raw identity ${identity} cites ${sourceId} outside group ${group.id} sources`)
          }
          if (!messageIdsBySource.get(sourceId)?.has(messageId)) {
            throw new Error(`raw identity ${identity} cites ${messageId} outside ${sourceId} message spine`)
          }
        }
      }
    }
    const military = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'war-named-message-samples')!
    expect(military.stableIdentities).toHaveLength(45)
    const equipment = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'unreachable-combat-equipment-acquisition')!
    expect(equipment.stableIdentities).toHaveLength(24)
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'p2-army-acquisition-categories')!.stableIdentities).toEqual([
      'construction:1515084481201967266:Имперцы/легионеры-гвардия',
      'construction:1515084481201967266:Пограничные феодалы',
      'construction:1515084481201967266:Региональные феодалы',
      'construction:1515084481201967266:Наемники',
      'construction:1515084481201967266:Дружина',
      'construction:1515084481201967266:Ополчение',
      'construction:1515084481201967266:Солдаты',
    ])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'p3b-absent-steel-tree-items')!.stableIdentities).toHaveLength(51)
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'p3b-absent-method-and-gear-prerequisites')!.stableIdentities)
      .toHaveLength(8)

    const expeditionProfiles = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'expedition-profile-reachability-residuals')!
    expect(expeditionProfiles.expectedLinkedConfigCarrierCount).toBe(8)
    expect(expeditionProfiles.linkedConfigCarrierKeys).toEqual(expect.arrayContaining([
      'td-wave-group:expedition-west-profile-wave:west-leather-warriors',
      'td-wave-group:expedition-west-profile-wave:west-bone-guards',
      'td-wave-group:expedition-swamp-profile-wave:swamp-creatures',
      'td-wave-group:expedition-swamp-profile-wave:swamp-plants',
    ]))

    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'p3a-assault-campaign-semantic-residuals')!
      .linkedConfigCarrierKeys).toEqual(['lifecycle:td'])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'p3a-naval-player-fleet')!
      .linkedConfigCarrierKeys).toEqual([
      'building:building-sea-port',
      'building-level:building-sea-port:1',
      'lifecycle:expeditions',
    ])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'p3a-naval-siege-residual')!)
      .toMatchObject({ disposition: 'blocked-substrate', expectedLinkedConfigCarrierCount: 3 })
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'small-temple-residuals')!
      .linkedConfigCarrierKeys).toEqual(['building:building-small-temple'])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'customs-residuals')!
      .linkedConfigCarrierKeys).toEqual(['building:building-customs'])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'economy-system-residuals')!
      .linkedConfigCarrierKeys).toEqual([])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'current-tech-carpentry-identity-review')!
      .linkedConfigCarrierKeys).toEqual(['resource:carpentry'])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'god-anti-bito-too-fast')!).toMatchObject({
      disposition: 'live',
      linkedConfigCarrierKeys: ['god-line:god-anti-bito-too-fast'],
      stableIdentities: [
        'zbs:1287539702731247656:игра закончится слишком быстро и это не интересно:god-line:god-anti-bito-too-fast',
      ],
    })
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'god-empty-dialogue-trigger-pools')!
      .linkedConfigCarrierKeys).toEqual(['lifecycle:god'])

    for (const [groupId, cardId] of [
      ['hearts-jack-loss-kidnapping', 'card-hearts-jack'],
      ['hearts-queen-loss-unfinished', 'card-hearts-queen'],
      ['hearts-king-loss-illness', 'card-hearts-king'],
      ['spades-ace-zigmund-viktor-identity', 'card-spades-ace'],
      ['hearts-ace-competing-identities', 'card-hearts-ace'],
    ] as const) {
      const group = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
        .find(candidate => candidate.id === groupId)!
      expect(group.linkedConfigCarrierKeys).toEqual([
        `card-face:${cardId}:normal`,
        `card-face:${cardId}:inverted`,
      ])
    }
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'hearts-ace-loss-father-death')!
      .linkedConfigCarrierKeys).toEqual([
      'card-face:card-hearts-ace:normal',
      'card-face:card-hearts-ace:inverted',
    ])
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'hearts-ace-competing-identities')!
      .stableIdentities).toContain(
      'cards:1471239614445523077:Туз-червы:Мистер-пропер-пропагандист',
    )
  })

  it('collects the complete current nested ID surface', () => {
    const value = config()
    const keys = new Set(collectEmpiresConfigCarriers(value).map(carrier => carrier.key))
    const city = value.empire.cities[0]
    const quest = value.quests.definitions[0]
    const stage = quest.stages[0]
    const node = stage.nodes[0]
    const wave = value.td.waves[0]
    const battlefield = value.td.battlefields[0]
    const unit = value.empire.units?.find(candidate => candidate.loadouts?.length)

    expect(keys).toContain(`config:${value.id}`)
    expect(keys).toContain(`city-slot:${city.id}:${city.slots[0].id}`)
    expect(keys).toContain(`quest-stage:${quest.id}:${stage.id}`)
    expect(keys).toContain(`quest-node:${quest.id}:${stage.id}:${node.id}`)
    expect(keys).toContain(`td-wave-group:${wave.id}:${wave.groups[0].id}`)
    expect(keys).toContain(`td-lane-node:${battlefield.id}:${battlefield.laneGraph.nodes[0].id}`)
    expect(keys).toContain(`td-build-spot:${battlefield.id}:${battlefield.buildSpots[0].id}`)
    expect(keys).toContain(`loyalty-class-gate:${value.empire.loyalty.classGates[0].id}`)
    expect(unit).toBeDefined()
    expect(keys).toContain(`unit-loadout:${unit!.id}:${unit!.loadouts![0].id}`)
  })

  it('links population provenance to every exact 500,000-population city carrier', () => {
    const value = config()
    const population = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'population-image-allocation')!
    const linkedCityIds = population.linkedConfigCarrierKeys
      .filter(key => key.startsWith('city:'))
      .map(key => key.slice('city:'.length))
      .sort()
    const exactCityIds = value.empire.cities
      .filter(city => city.population === 500_000)
      .map(city => city.id)
      .sort()

    expect(population.rawSources).toEqual(['population-image', 'general', 'construction'])
    expect(linkedCityIds).toHaveLength(20)
    expect(linkedCityIds).toEqual(exactCityIds)
    for (const cityId of linkedCityIds) {
      const city = value.empire.cities.find(candidate => candidate.id === cityId)!
      expect(city.populationClasses.nonworking).toBe(city.population / 2)
      expect(Object.values(city.populationClasses)
        .reduce((total, amount) => total + amount, 0)).toBe(city.population)
    }
  })

  it('keeps all 106 exact card faces executable, including the source-mapped identities', () => {
    const value = config()
    expect(value.cards).toHaveLength(53)
    expect(value.cards.flatMap(card => [card.normal, card.inverted])).toHaveLength(106)
    expect(value.cards.flatMap(card => [card.normal, card.inverted])
      .filter(side => !side.deferredReason)).toHaveLength(106)

    expect(value.cards.find(card => card.id === 'card-spades-jack')).toMatchObject({
      name: 'Антон де Лорян',
      normal: { title: 'Антон де Лорян', effects: expect.any(Array) },
      inverted: { effects: expect.any(Array) },
    })
    expect(value.cards.find(card => card.id === 'card-spades-queen')).toMatchObject({
      name: 'Мария Брауз',
      normal: { title: 'Мария Брауз', effects: expect.any(Array) },
      inverted: { effects: expect.any(Array) },
    })
    expect(value.cards.find(card => card.id === 'card-spades-king')).toMatchObject({
      name: 'Конрад Лоуренс',
      normal: { title: 'Конрад Лоуренс', effects: expect.any(Array) },
      inverted: { effects: expect.any(Array) },
    })

    const liveMappings = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'current-authored-card-faces')!
    const incompleteMappings = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'mapped-but-incomplete-card-faces')!
    expect(liveMappings.stableIdentities).toHaveLength(10)
    expect(incompleteMappings.stableIdentities).toHaveLength(18)
    expect([...liveMappings.stableIdentities, ...incompleteMappings.stableIdentities]
      .some(identity => /^(current-card-face|mapped-incomplete):/.test(identity))).toBe(false)
    for (const group of [liveMappings, incompleteMappings]) {
      const boundCarriers = group.stableIdentities.map((identity) => {
        const [cardId, side] = identity.split(':').slice(-2)
        return `card-face:${cardId}:${side}`
      })
      expect(boundCarriers).toEqual(group.linkedConfigCarrierKeys)
    }
    expect(EMPIRES_CONTENT_COVERAGE_MANIFEST.configGroups
      .find(group => group.id === 'config:cards:live:configured')!.configCarrierKeys)
      .toEqual(expect.arrayContaining([
        'card-face:card-hearts-ace:normal',
        'card-face:card-hearts-ace:inverted',
      ]))

    const recruiters = value.cards.find(card => card.id === 'card-hearts-7')!
    const currency = value.cards.find(card => card.id === 'card-diamonds-ace')!
    expect(recruiters.maxLevel).toBe(1)
    expect(currency.maxLevel).toBe(1)
    expect([...recruiters.normal.effects, ...recruiters.inverted.effects,
      ...currency.inverted.effects].every(effect => !Object.hasOwn(effect, 'amountPerLevel')))
      .toBe(true)
    for (const [cardId, side] of [
      ['card-diamonds-6', 'inverted'],
      ['card-diamonds-ace', 'normal'],
      ['card-spades-5', 'inverted'],
      ['card-spades-8', 'inverted'],
    ] as const) {
      const face = value.cards.find(card => card.id === cardId)![side]
      expect(face.deferredReason).toBeUndefined()
      expect(face.effects.length).toBeGreaterThan(0)
    }
  })

  it('applies authored and deterministic fallback card effects at their exact scaling boundary', () => {
    const value = config()
    const held = (cardId: string, inverted: boolean): EmpiresEndgameEngine => {
      const state = new EmpiresEndgameEngine(value).snapshot()
      state.phase = 'divineGift'
      state.durak.deck = []
      state.durak.playerHand = [cardId]
      state.durak.godHand = []
      state.durak.discard = value.cards.map(card => card.id).filter(id => id !== cardId)
      state.durak.table = []
      state.durak.trumpSuit = 'clubs'
      state.cards[cardId].level = 1
      state.cards[cardId].inverted = inverted
      state.giftChoiceIds = ['gift-combat-spirit']
      state.pendingResolution = null
      const engine = new EmpiresEndgameEngine(value, state)
      expect(engine.chooseGift('gift-combat-spirit')).toMatchObject({ ok: true })
      return engine
    }

    const farms = held('card-spades-8', false)
    expect(farms.state.empire.productionMultipliers.food).toBe(2)

    const famine = held('card-spades-8', true)
    expect(famine.state.empire.productionMultipliers.food ?? 1).toBe(1)
    expect(famine.state.empire.flags.famineYear).toBe(2)

    const currency = held('card-diamonds-ace', false)
    expect(currency.state.empire.productionMultipliers.gold ?? 1).toBe(1.07)

    const cartel = held('card-diamonds-6', true)
    expect(cartel.state.empire.productionMultipliers.gold ?? 1).toBeCloseTo(0.93)

    const brainDrain = held('card-spades-5', true)
    expect(brainDrain.state.empire.resources.knowledge)
      .toBe(value.empire.initialResources.knowledge - 1_000)

    const isolation = held('card-diamonds-ace', true)
    expect(isolation.state.empire.flags).toMatchObject({
      externalTradeDisabled: 1,
      internalTradeOnly: 1,
    })

    for (const engine of [held('card-hearts-7', false), isolation]) {
      engine.state.upgradePoints = 10
      const pointsBefore = engine.state.upgradePoints
      expect(engine.improveCard(engine.state.durak.playerHand[0])).toEqual({
        ok: false,
        message: 'The card is already at its maximum level.',
      })
      expect(engine.state.upgradePoints).toBe(pointsBefore)
    }
  })

  it('keeps the accepted campaign free of deferral markers and rejects unknown mystics', () => {
    const value = config()
    const withoutClash = structuredClone(value)
    delete (withoutClash as unknown as Record<string, unknown>).clash
    expect(countProperty(withoutClash, 'deferredReason')).toBe(0)
    const deferredSubfeatures = collectArrays(withoutClash, 'deferredSubfeatures')
    expect(deferredSubfeatures.reduce((total, items) => total + items.length, 0)).toBe(0)
    expect(countProperty(withoutClash, 'accessDeferredReason')).toBe(0)
    expect(countProperty(withoutClash, 'encounterDeferredReason')).toBe(0)
    expect(countProperty(withoutClash, 'laterBattleBonusDeferredReason')).toBe(0)
    expect(value.mysticCards).toHaveLength(4)
    expect(value.mysticCards.every(definition => (
      !definition.deferredReason
      && !definition.normal.deferredReason
      && !definition.inverted.deferredReason
    ))).toBe(true)

    const state = new EmpiresEndgameEngine(value).snapshot()
    const unknownId = 'missing-mystic'
    state.mystics.instances[unknownId] = {
      id: unknownId,
      definitionId: unknownId,
      owner: 'player',
      inverted: false,
      status: 'zone',
      spawnedAtCon: 1,
      returnAtCon: null,
      lastChangedCon: Math.max(1, state.con),
    }
    state.mystics.zone.push(unknownId)
    expect(() => new EmpiresEndgameEngine(value, state)).toThrow(/Invalid mystic instance/)
  })

  it('owns the live P12 Chess carrier, QA scenario, and shared lifecycle boundary', () => {
    const value = config()
    expect(value.chess.enabled).toBe(true)
    const chessCarriers = collectEmpiresConfigCarriers(value)
      .map(carrier => carrier.key)
      .filter(key => key.startsWith('chess') || key === 'lifecycle:chess')
    expect(chessCarriers).toEqual(expect.arrayContaining([
      'chess:variant',
      'chess-entry:capital-coliseum',
      'chess:anton',
      'chess:settlement',
      'lifecycle:chess',
    ]))
    expect(chessCarriers.filter(key => key.startsWith('chess-piece:'))).toHaveLength(31)
    expect(EMPIRES_QA_SCENARIO_NAMES).toContain('chess-match')
    const chessGate = EMPIRES_CONTENT_COVERAGE_MANIFEST.rawCatalogGroups
      .find(group => group.id === 'chess-gate')
    expect(chessGate).toMatchObject({
      disposition: 'live',
      expectedLinkedConfigCarrierCount: 36,
    })
  })
})
