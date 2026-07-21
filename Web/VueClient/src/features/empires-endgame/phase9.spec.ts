import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { exportEmpiresCampaign, importEmpiresCampaign } from './persistence'
import { nextEmpiresRandom } from './rng'
import { resolveTavern, validateTavernPlan } from './tavern/engine'
import type {
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresMysticCardInstance,
  EmpiresTavernMinigameSession,
} from './types'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(bundledConfigJson)
  value.empire.eventChance = 0
  return value
}

function empireSnapshot(value: EmpiresEndgameConfig, runOrdinal = 2): EmpiresCampaignState {
  const state = new EmpiresEndgameEngine(value, undefined, { tavernRunOrdinal: runOrdinal }).snapshot()
  state.phase = 'empire'
  state.con = Math.max(state.con, value.tavern.spawn.eligibleCon)
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = value.empire.daysPerPhase
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  state.tavern.spawnChecked = true
  state.tavern.spawned = true
  state.tavern.spawnedAtCon = state.con
  state.empire.resources[value.empire.domesticEconomy.goldResourceId] = 100_000
  return new EmpiresEndgameEngine(value, state).snapshot()
}

function tavernSession(engine: EmpiresEndgameEngine): EmpiresTavernMinigameSession {
  const session = engine.state.minigame
  if (session?.kind !== 'tavern') throw new Error('Fixture did not start a Tavern session.')
  return session
}

function mystic(
  definitionId: string,
  con: number,
  overrides: Partial<EmpiresMysticCardInstance> = {},
): EmpiresMysticCardInstance {
  return {
    id: definitionId,
    definitionId,
    owner: 'player',
    inverted: false,
    status: 'zone',
    spawnedAtCon: 1,
    returnAtCon: null,
    lastChangedCon: con,
    ...overrides,
  }
}

type TavernInternals = {
  checkTavernSpawn(state?: EmpiresCampaignState): void
  observeQueenCombo(cardId: string): boolean
  tickMysticCards(state?: EmpiresCampaignState): void
}

function internals(engine: EmpiresEndgameEngine): TavernInternals {
  return engine as unknown as TavernInternals
}

describe('Empire\'s Endgame Phase 9 Tavern and mystic cards', () => {
  it('migrates v13 fail-closed, remains non-mutating/idempotent, and rejects future config', () => {
    const legacy = structuredClone(bundledConfigJson) as unknown as Record<string, unknown>
    legacy.schemaVersion = 13
    delete legacy.mysticCards
    delete legacy.tavern
    const original = structuredClone(legacy)

    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(original)
    expect(migrated).toMatchObject({ schemaVersion: 17, mysticCards: [], tavern: { enabled: false } })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 18 })).toThrow(/future.*18/i)
  })

  it('keeps exactly 52 suited cards plus Joker and maps Maria separately from the mystic Queen', () => {
    const value = config()
    const suited = value.cards.filter(card => card.suit !== 'joker')
    const joker = value.cards.filter(card => card.suit === 'joker' && card.rank === 'joker')
    const maria = value.cards.find(card => card.id === value.tavern.maria.standardCardDefinitionId)
    const queen = value.mysticCards.find(card => card.id === value.tavern.queen.mysticDefinitionId)

    expect(value.cards).toHaveLength(53)
    expect(suited).toHaveLength(52)
    expect(joker).toHaveLength(1)
    expect(maria).toMatchObject({ id: 'card-spades-queen', suit: 'spades', rank: 'queen', name: 'Мария Брауз' })
    expect(queen).toMatchObject({ id: 'mystic-queen-of-spades', startsInverted: true })
    expect(queen).not.toHaveProperty('suit')
    expect(queen).not.toHaveProperty('rank')
    expect(new Set([...value.cards, ...value.mysticCards].map(card => card.id)).size)
      .toBe(value.cards.length + value.mysticCards.length)
  })

  it('applies the exact first/second/later run spawn boundaries through serialized RNG', () => {
    const value = config()
    const create = (runOrdinal: number) => {
      const engine = new EmpiresEndgameEngine(value, undefined, { tavernRunOrdinal: runOrdinal })
      engine.state.con = value.tavern.spawn.eligibleCon
      return engine
    }

    const first = create(1)
    const firstDraws = first.state.rng.draws
    internals(first).checkTavernSpawn()
    expect(first.state.tavern).toMatchObject({ spawnChecked: true, spawned: false, spawnedAtCon: null })
    expect(first.state.rng.draws).toBe(firstDraws)

    const second = create(2)
    const secondDraws = second.state.rng.draws
    internals(second).checkTavernSpawn()
    expect(second.state.tavern).toMatchObject({ spawnChecked: true, spawned: true, spawnedAtCon: value.tavern.spawn.eligibleCon })
    expect(second.state.rng.draws).toBe(secondDraws)

    const later = create(3)
    const predictedRng = structuredClone(later.state.rng)
    const expected = nextEmpiresRandom(predictedRng) < value.tavern.spawn.laterRunChance
    internals(later).checkTavernSpawn()
    expect(later.state.tavern.spawned).toBe(expected)
    expect(later.state.rng).toEqual(predictedRng)
    internals(later).checkTavernSpawn()
    expect(later.state.rng).toEqual(predictedRng)
  })

  it('creates a deterministic two-section encounter and settles hire, spirits, and earned rumor once', () => {
    const value = config()
    value.tavern.maria.encounterChance = 1
    value.god.deckMemory.availability = 'perCon'
    value.god.deckMemory.inspectionsPerCon = 1
    const source = empireSnapshot(value)
    const cityId = source.empire.cities[0].id
    const first = new EmpiresEndgameEngine(value, source)
    const second = new EmpiresEndgameEngine(value, source)
    const cosmeticBefore = structuredClone(first.state.god.cosmeticRng)

    expect(first.startTavernVisit(cityId)).toMatchObject({ ok: true })
    expect(second.startTavernVisit(cityId)).toMatchObject({ ok: true })
    const session = tavernSession(first)
    expect(session).toEqual(tavernSession(second))
    expect(session.plan.sections).toEqual(['tables', 'bar'])
    expect(validateTavernPlan({
      ...session.plan,
      sections: ['bar', 'tables'],
    } as unknown as typeof session.plan)).toContain(
      'Tavern plan sections must preserve the authored tables/bar order.',
    )
    expect(session.plan.mercenaryOffers).toHaveLength(value.tavern.mercenaries.baseOfferCount)
    expect(session.plan.maria).toMatchObject({ present: true, deferredReason: expect.any(String) })
    expect(session.plan.rumor.deckHint).toMatchObject({ position: value.tavern.rumors.deckHintPosition })
    expect(first.state.god.cosmeticRng).toEqual(cosmeticBefore)

    const offer = session.plan.mercenaryOffers[0]
    const goldBefore = first.state.empire.resources[value.empire.domesticEconomy.goldResourceId]
    const result = resolveTavern(session.plan, session.seed, [
      { turn: 1, kind: 'hire', offerId: offer.id },
      { turn: 2, kind: 'buy-drinks' },
      { turn: 3, kind: 'buy-rumor' },
      { turn: 4, kind: 'finish' },
    ])

    expect(first.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(first.state.empire.resources[value.empire.domesticEconomy.goldResourceId])
      .toBe(goldBefore - result.goldSpent)
    expect(first.state.empire.cities.find(city => city.id === cityId)?.recruitedUnitCohorts)
      .toEqual(expect.arrayContaining([expect.objectContaining({ unitId: offer.unitId, count: offer.count })]))
    expect(first.state.tavern).toMatchObject({
      lastVisitedCon: source.con,
      spiritsReadyAtCon: session.plan.drinks.readyAtCon,
      spiritsExpiresAfterCon: session.plan.drinks.expiresAfterCon,
    })
    expect(first.state.durak.deckMemoryInspectionsUsed).toBe(1)
    expect(first.state.minigameResultLog.at(-1)?.result).toEqual(result)

    const settled = first.snapshot()
    expect(first.resolveMinigame(result)).toMatchObject({ ok: true, message: expect.stringMatching(/already resolved/i) })
    expect(first.snapshot()).toEqual(settled)
  })

  it('activates spirits next con for two cons and never exposes unearned deck information', () => {
    const value = config()
    value.god.enabled = false
    const source = empireSnapshot(value)
    const cityId = source.empire.cities[0].id
    const engine = new EmpiresEndgameEngine(value, source)
    expect(engine.startTavernVisit(cityId)).toMatchObject({ ok: true })
    const session = tavernSession(engine)
    expect(session.plan.rumor.deckHint).toBeNull()
    expect(session.plan.rumor.text).toBe(value.tavern.rumors.fallbackText)
    const result = resolveTavern(session.plan, session.seed, [
      { turn: 1, kind: 'buy-drinks' },
      { turn: 2, kind: 'finish' },
    ])
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })

    const before = engine.domesticEconomyView(cityId).tavern
    expect(before.spiritsActive).toBe(false)
    const activeState = engine.snapshot()
    activeState.con = session.plan.drinks.readyAtCon
    activeState.phase = 'empire'
    const active = new EmpiresEndgameEngine(value, activeState)
    expect(active.domesticEconomyView(cityId).tavern.spiritsActive).toBe(true)
    expect(active.startTavernVisit(cityId)).toMatchObject({ ok: true })
    const activePlan = tavernSession(active).plan
    expect(activePlan.mercenaryOffers).toHaveLength(value.tavern.mercenaries.spiritsOfferCount)
    expect(activePlan.rumor.deckHint).toBeNull()
    expect(activePlan.mercenaryOffers.some(offer => {
      const base = value.tavern.mercenaries.offers.find(candidate => candidate.id === offer.id)!
      return base.spiritsEligible
        && offer.goldCost === Math.floor(base.goldCost * value.tavern.spirits.cheapOfferMultiplier)
    })).toBe(true)

    const expiredState = engine.snapshot()
    expiredState.con = session.plan.drinks.expiresAfterCon + 1
    expiredState.phase = 'empire'
    expect(new EmpiresEndgameEngine(value, expiredState).domesticEconomyView(cityId).tavern.spiritsActive)
      .toBe(false)
  })

  it('rejects unfinished, over-budget, stale, and replay-tampered Tavern results without settlement', () => {
    const value = config()
    const source = empireSnapshot(value)
    const engine = new EmpiresEndgameEngine(value, source)
    expect(engine.startTavernVisit(source.empire.cities[0].id)).toMatchObject({ ok: true })
    const session = tavernSession(engine)
    const before = engine.snapshot()

    const unfinished = resolveTavern(session.plan, session.seed, [])
    expect(engine.resolveMinigame(unfinished)).toMatchObject({ ok: false })
    expect(engine.snapshot()).toEqual(before)

    const tampered = resolveTavern(session.plan, session.seed, [{ turn: 1, kind: 'finish' }])
    tampered.goldSpent += 1
    expect(engine.resolveMinigame(tampered)).toMatchObject({ ok: false, message: expect.stringMatching(/replay/i) })
    expect(engine.snapshot()).toEqual(before)

    const stale = resolveTavern(session.plan, session.seed, [{ turn: 1, kind: 'finish' }])
    stale.rulesIdentity.rulesDigest = 'stale'
    expect(engine.resolveMinigame(stale)).toMatchObject({ ok: false })
    expect(engine.snapshot()).toEqual(before)

    const unknown = resolveTavern(session.plan, session.seed, [
      { turn: 1, kind: 'dance' } as never,
    ])
    expect(unknown.error).toMatch(/unknown tavern command/i)
    expect(engine.resolveMinigame(unknown)).toMatchObject({ ok: false })
    expect(engine.snapshot()).toEqual(before)

    const poorState = empireSnapshot(value)
    poorState.empire.resources[value.empire.domesticEconomy.goldResourceId] = 0
    const poor = new EmpiresEndgameEngine(value, poorState)
    expect(poor.startTavernVisit(poorState.empire.cities[0].id)).toMatchObject({ ok: true })
    const poorSession = tavernSession(poor)
    const overBudget = resolveTavern(poorSession.plan, poorSession.seed, [
      { turn: 1, kind: 'hire', offerId: poorSession.plan.mercenaryOffers[0].id },
    ])
    expect(poor.resolveMinigame(overBudget)).toMatchObject({ ok: false, message: expect.stringMatching(/gold/i) })
  })

  it('recognizes 3–7–Т with arbitrary intervening cards only after Maria victory and spawns a separate Queen', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    const byRank = (rank: string) => value.cards.find(card => card.rank === rank)!.id
    const standardQueenId = value.tavern.maria.standardCardDefinitionId

    expect(internals(engine).observeQueenCombo(byRank('3'))).toBe(false)
    expect(engine.state.mystics.queenComboProgress).toBe(0)
    engine.state.tavern.mariaVictory = true
    engine.state.tavern.mariaVictoryAtCon = engine.state.con
    expect(internals(engine).observeQueenCombo(byRank('3'))).toBe(false)
    expect(internals(engine).observeQueenCombo(byRank('6'))).toBe(false)
    expect(internals(engine).observeQueenCombo(byRank('7'))).toBe(false)
    expect(internals(engine).observeQueenCombo(byRank('ace'))).toBe(true)

    expect(engine.state.mystics.queenComboProgress).toBe(3)
    expect(engine.state.mystics.zone).toEqual([value.tavern.queen.mysticDefinitionId])
    expect(engine.state.cards[standardQueenId]).toBeTruthy()
    expect(engine.state.mystics.instances[value.tavern.queen.mysticDefinitionId]).toMatchObject({
      definitionId: value.tavern.queen.mysticDefinitionId,
      inverted: true,
      status: 'zone',
    })
  })

  it('pulses both ordered Queen neighbors atomically, handles edges/singletons, and restores idempotently', () => {
    const value = config()
    value.tavern.historyRetention = 2
    const queenId = value.tavern.queen.mysticDefinitionId
    const listId = 'mystic-list'
    const lorikId = 'mystic-lorik'
    delete value.mysticCards.find(card => card.id === listId)!.deferredReason
    delete value.mysticCards.find(card => card.id === lorikId)!.deferredReason
    const engine = new EmpiresEndgameEngine(value)
    engine.state.con = 4
    engine.state.mystics.instances = {
      [listId]: mystic(listId, 1),
      [queenId]: mystic(queenId, 1, { inverted: true }),
      [lorikId]: mystic(lorikId, 1),
    }
    engine.state.mystics.zone = [listId, queenId, lorikId]

    internals(engine).tickMysticCards()
    expect(engine.state.mystics.lastQueenPulseInstanceIds).toEqual([listId, lorikId])
    expect(engine.state.mystics.instances[listId].inverted).toBe(true)
    expect(engine.state.mystics.instances[lorikId].inverted).toBe(true)
    const once = engine.snapshot()
    internals(engine).tickMysticCards()
    expect(engine.snapshot()).toEqual(once)
    expect(new EmpiresEndgameEngine(value, once).state.mystics).toEqual(once.mystics)

    const edge = new EmpiresEndgameEngine(value)
    edge.state.con = 4
    edge.state.mystics.instances = {
      [queenId]: mystic(queenId, 1, { inverted: true }),
      [listId]: mystic(listId, 1),
    }
    edge.state.mystics.zone = [queenId, listId]
    internals(edge).tickMysticCards()
    expect(edge.state.mystics.lastQueenPulseInstanceIds).toEqual([listId])

    const singleton = new EmpiresEndgameEngine(value)
    singleton.state.con = 4
    singleton.state.mystics.instances = { [queenId]: mystic(queenId, 1, { inverted: true }) }
    singleton.state.mystics.zone = [queenId]
    internals(singleton).tickMysticCards()
    expect(singleton.state.mystics.lastQueenPulseInstanceIds).toEqual([])

    for (const con of [7, 10]) {
      engine.state.con = con
      internals(engine).tickMysticCards()
    }
    expect(engine.state.mystics.history).toHaveLength(2)
    expect(engine.state.mystics.compactedHistoryCount).toBe(1)
    expect(engine.state.mystics.compactedHistoryDigest).toMatch(/^[0-9a-f]{16}$/)
  })

  it('returns a lost mystic inverted at the deterministic ordered-zone tail', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    const listId = 'mystic-list'
    engine.state.con = 5
    engine.state.mystics.instances[listId] = mystic(listId, 4, {
      status: 'returning',
      inverted: false,
      returnAtCon: 5,
    })

    internals(engine).tickMysticCards()

    expect(engine.state.mystics.zone).toEqual([listId])
    expect(engine.state.mystics.instances[listId]).toMatchObject({
      status: 'zone',
      inverted: true,
      returnAtCon: null,
      lastChangedCon: 5,
    })
    expect(engine.state.mystics.history.at(-1)).toMatchObject({ kind: 'return', instanceIds: [listId] })
  })

  it('keeps mystics out of Durak legality, trump, refill, and winner accounting', () => {
    const value = config()
    value.god.antiBito.enabled = false
    const base = new EmpiresEndgameEngine(value)
    const baselineLegal = base.legalAttackCardIds('player')
    const baselineTrump = base.state.durak.trumpSuit
    const state = base.snapshot()
    const queenId = value.tavern.queen.mysticDefinitionId
    state.mystics.instances[queenId] = mystic(queenId, state.con, { inverted: true })
    state.mystics.zone = [queenId]
    const withMystic = new EmpiresEndgameEngine(value, state)
    expect(withMystic.legalAttackCardIds('player')).toEqual(baselineLegal)
    expect(withMystic.state.durak.trumpSuit).toBe(baselineTrump)
    expect([
      ...withMystic.state.durak.deck,
      ...withMystic.state.durak.playerHand,
      ...withMystic.state.durak.godHand,
      ...withMystic.state.durak.discard,
    ]).not.toContain(queenId)

    const winner = withMystic.snapshot()
    const [attackCardId, defenseCardId, godHandCardId] = value.cards.map(card => card.id)
    const occupied = new Set([attackCardId, defenseCardId, godHandCardId])
    winner.phase = 'cards'
    winner.durak.deck = []
    winner.durak.playerHand = []
    winner.durak.godHand = [godHandCardId]
    winner.durak.discard = value.cards.map(card => card.id).filter(id => !occupied.has(id))
    winner.durak.table = [{ attackCardId, defenseCardId }]
    winner.durak.attacker = 'player'
    winner.durak.defender = 'god'
    winner.durak.stage = 'throwIn'
    winner.durak.defenderHandAtBoutStart = 2
    const completion = new EmpiresEndgameEngine(value, winner)
    expect(completion.endAttack('player')).toMatchObject({ ok: true })
    expect(completion.state.phase).toBe('victory')
    expect(completion.state.mystics.zone).toEqual([queenId])
  })

  it('round-trips schema v12 order, additively normalizes v11, and rejects duplicate zones', () => {
    const value = config()
    const state = new EmpiresEndgameEngine(value).snapshot()
    const queenId = value.tavern.queen.mysticDefinitionId
    const listId = 'mystic-list'
    delete value.mysticCards.find(card => card.id === listId)!.deferredReason
    state.mystics.instances = {
      [queenId]: mystic(queenId, state.con, { inverted: true }),
      [listId]: mystic(listId, state.con),
    }
    state.mystics.zone = [listId, queenId]
    const restored = new EmpiresEndgameEngine(
      value,
      importEmpiresCampaign(exportEmpiresCampaign(state), value.id),
    )
    expect(restored.state.schemaVersion).toBe(16)
    expect(restored.state.mystics.zone).toEqual([listId, queenId])

    const legacyState = structuredClone(new EmpiresEndgameEngine(value).snapshot()) as unknown as Record<string, unknown>
    legacyState.schemaVersion = 11
    delete legacyState.tavern
    delete legacyState.mystics
    const migrated = importEmpiresCampaign({
      schemaVersion: 11,
      savedAt: '2026-07-18T00:00:00.000Z',
      state: legacyState,
    }, value.id)
    expect(new EmpiresEndgameEngine(value, migrated).state).toMatchObject({
      schemaVersion: 16,
      mystics: { zone: [], instances: {} },
      tavern: { runOrdinal: 1, spawned: false },
    })

    const duplicateMystic = restored.snapshot()
    duplicateMystic.mystics.zone.push(listId)
    expect(() => new EmpiresEndgameEngine(value, duplicateMystic)).toThrow(/duplicate/i)
    const duplicateCore = restored.snapshot()
    duplicateCore.durak.playerHand.push(duplicateCore.durak.deck[0])
    expect(() => new EmpiresEndgameEngine(value, duplicateCore)).toThrow(/exactly one authoritative/i)
  })

  it('retains explicit blockers for every raw semantic that is still absent', () => {
    const value = config()
    const trio = ['mystic-list', 'mystic-lorik', 'mystic-anatoliy']
      .map(id => value.mysticCards.find(card => card.id === id))
    expect(trio).toEqual(trio.map(card => expect.objectContaining({ deferredReason: expect.any(String) })))
    expect(value.tavern.maria.encounterChance).toBe(0.33)
    expect(value.tavern.maria.encounterDeferredReason).toMatch(/2×2/)
    expect(value.tavern.deferredSubfeatures.map(item => item.id)).toEqual(expect.arrayContaining([
      'maria2x2',
      'mariaGunpowderLegacy',
      'mysticTrioPassives',
      'mysticLeaveAction',
      'queenAppeasement',
    ]))
  })
})
