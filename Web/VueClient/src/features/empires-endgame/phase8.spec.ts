import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import type { EmpiresCampaignState, EmpiresEndgameConfig, EmpiresGodLineDefinition } from './types'

function config(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(defaultConfigJson)
}

function winnerSnapshot(
  value: EmpiresEndgameConfig,
  source?: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = source
    ? new EmpiresEndgameEngine(value, source).snapshot()
    : new EmpiresEndgameEngine(value).snapshot()
  const ids = value.cards.map(card => card.id)
  const [attackCardId, defenseCardId, godHandCardId] = ids
  if (!attackCardId || !defenseCardId || !godHandCardId) throw new Error('Winner fixture needs three cards.')
  const occupied = new Set([attackCardId, defenseCardId, godHandCardId])
  state.phase = 'cards'
  state.durak.consecutiveBito = Math.max(0, value.god.antiBito.minimumConsecutiveBito - 1)
  state.boutsInCon = 0
  state.durak.deck = []
  state.durak.playerHand = []
  state.durak.godHand = [godHandCardId]
  state.durak.discard = ids.filter(cardId => !occupied.has(cardId))
  state.durak.table = [{ attackCardId, defenseCardId }]
  state.durak.attacker = 'player'
  state.durak.defender = 'god'
  state.durak.stage = 'throwIn'
  state.durak.defenderHandAtBoutStart = 2
  state.questRuntime.activeMandatoryQuestId = null
  state.questRuntime.mandatoryQueue = []
  state.outcomeReason = null
  return new EmpiresEndgameEngine(value, state).snapshot()
}

function takeSnapshot(
  value: EmpiresEndgameConfig,
  source?: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = source
    ? new EmpiresEndgameEngine(value, source).snapshot()
    : new EmpiresEndgameEngine(value).snapshot()
  const ids = value.cards.map(card => card.id)
  const attackCardId = ids[0]
  if (!attackCardId) throw new Error('Take fixture needs a card.')
  state.phase = 'cards'
  state.durak.consecutiveBito = value.god.antiBito.minimumConsecutiveBito
  state.boutsInCon = 0
  state.durak.deck = []
  state.durak.playerHand = []
  state.durak.godHand = []
  state.durak.discard = ids.slice(1)
  state.durak.table = [{ attackCardId, defenseCardId: null }]
  state.durak.attacker = 'god'
  state.durak.defender = 'player'
  state.durak.stage = 'taking'
  state.durak.defenderHandAtBoutStart = 1
  state.questRuntime.activeMandatoryQuestId = null
  state.questRuntime.mandatoryQueue = []
  state.outcomeReason = null
  return new EmpiresEndgameEngine(value, state).snapshot()
}

function line(
  id: string,
  trigger: EmpiresGodLineDefinition['trigger'],
  once = false,
  weight = 1,
): EmpiresGodLineDefinition {
  return { id, trigger, text: `${trigger}:${id}`, once, weight }
}

describe('Empire\'s Endgame Phase 8 God presence', () => {
  it('migrates schema v12 fail-closed, validates God JSON, and rejects future schemas', () => {
    const legacy = structuredClone(defaultConfigJson) as unknown as Record<string, unknown>
    legacy.schemaVersion = 12
    const original = structuredClone(legacy)

    const migrated = migrateEmpiresConfig(legacy) as EmpiresEndgameConfig

    expect(legacy).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 15,
      god: {
        enabled: false,
        deckMemory: { enabled: false, availability: 'always' },
        antiBito: { enabled: false, maxInterventions: 0 },
        mercyConfirmation: { enabled: false },
      },
    })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()

    const malformed = config()
    malformed.god.lines.push({ ...malformed.god.lines[0] })
    expect(() => validateEmpiresConfig(malformed)).toThrow(/god\.lines repeats id/i)
    const invalidWeight = config()
    invalidWeight.god.lines[0].weight = 0
    expect(() => validateEmpiresConfig(invalidWeight)).toThrow(/weight must be finite and positive/i)
    const invalidCardReference = config()
    invalidCardReference.god.antiBito.excludedDefinitionIds.push('missing-card')
    expect(() => validateEmpiresConfig(invalidCardReference)).toThrow(/unknown card missing-card/i)
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 16 })).toThrow(/future.*16/i)
  })

  it('projects immutable next-draw order and consumes only successful limited openings', () => {
    const value = config()
    value.god.deckMemory.availability = 'perCon'
    value.god.deckMemory.inspectionsPerCon = 1
    value.empire.eventChance = 0
    value.td.enabled = false
    const engine = new EmpiresEndgameEngine(value)
    const expectedOrder = [...engine.state.durak.deck].reverse()
    const nextCardId = expectedOrder[0]
    if (!nextCardId) throw new Error('Deck-memory fixture needs a next card.')
    engine.state.cards[nextCardId].inverted = true
    const deckBefore = [...engine.state.durak.deck]
    const rngBefore = structuredClone(engine.state.rng)

    const opened = engine.inspectDeck()

    expect(opened.ok).toBe(true)
    if (!opened.ok) throw new Error(opened.message)
    expect(opened.cards.map(card => card.instanceId)).toEqual(expectedOrder)
    expect(opened.cards[0]).toMatchObject({ position: 1, instanceId: nextCardId, inverted: true })
    expect(Object.isFrozen(opened.cards)).toBe(true)
    expect(Object.isFrozen(opened.cards[0])).toBe(true)
    expect(engine.state.durak.deck).toEqual(deckBefore)
    expect(engine.state.rng).toEqual(rngBefore)
    expect(engine.state.durak.deckMemoryInspectionsUsed).toBe(1)

    const consumed = engine.snapshot()
    expect(engine.inspectDeck()).toMatchObject({ ok: false })
    expect(engine.snapshot()).toEqual(consumed)
    const restored = new EmpiresEndgameEngine(value, consumed)
    expect(restored.canInspectDeck()).toMatchObject({ allowed: false, remainingInspections: 0 })
    expect(restored.state.durak.deckMemoryInspectionsUsed).toBe(1)

    const empireState = restored.snapshot()
    empireState.phase = 'empire'
    empireState.empire.daysRemaining = 1
    const nextCon = new EmpiresEndgameEngine(value, empireState)
    expect(nextCon.inspectDeck()).toMatchObject({ ok: false })
    expect(nextCon.finishEmpire().ok).toBe(true)
    expect(nextCon.state.durak.deckMemoryInspectionsUsed).toBe(0)
  })

  it('intercepts the winner once with deterministic eligible discard instances and a replay digest', () => {
    const value = config()
    const preIntervention = winnerSnapshot(value)
    for (const cardId of Object.keys(preIntervention.cards)) {
      if (value.god.antiBito.excludedDefinitionIds.includes(
        preIntervention.cards[cardId].definitionId,
      )) continue
      preIntervention.cards[cardId].level = 2
      preIntervention.cards[cardId].inverted = true
    }
    const first = new EmpiresEndgameEngine(value, preIntervention)
    const second = new EmpiresEndgameEngine(value, preIntervention)

    expect(first.endAttack('player').ok).toBe(true)
    expect(second.endAttack('player').ok).toBe(true)

    expect(first.state.phase).toBe('cards')
    expect(first.state.durak.godInterventions).toBe(1)
    expect(first.state.god.interventions).toHaveLength(1)
    const record = first.state.god.interventions[0]
    expect(record.returnedInstanceIds).toHaveLength(value.god.antiBito.returnCount)
    expect(record).toMatchObject({
      con: preIntervention.con,
      insertion: 'drawBottom',
      trigger: 'winner-after-consecutive-bito',
    })
    expect(record.resultingDigest).toMatch(/^[0-9a-f]{16}$/)
    expect(record.returnedInstanceIds).not.toEqual(expect.arrayContaining(
      value.god.antiBito.excludedDefinitionIds,
    ))
    expect(first.state.durak.playerHand).toEqual([...record.returnedInstanceIds].reverse())
    expect(record.returnedInstanceIds.every(cardId => (
      first.state.cards[cardId].level === 2 && first.state.cards[cardId].inverted
    ))).toBe(true)
    expect(second.state.god.interventions).toEqual(first.state.god.interventions)
    expect(second.state.durak).toEqual(first.state.durak)
    expect(second.state.rng).toEqual(first.state.rng)
  })

  it('terminates safely for empty, insufficient, threshold, and cap boundaries', () => {
    const emptyValue = config()
    emptyValue.god.antiBito.excludedDefinitionIds = emptyValue.cards.map(card => card.id)
    const empty = new EmpiresEndgameEngine(emptyValue, winnerSnapshot(emptyValue))
    expect(empty.endAttack('player').ok).toBe(true)
    expect(empty.state.phase).toBe('victory')
    expect(empty.state.god.interventions).toHaveLength(0)

    const fewerValue = config()
    const allowedDiscardId = fewerValue.cards[3].id
    fewerValue.god.antiBito.excludedDefinitionIds = fewerValue.cards
      .map(card => card.id)
      .filter(cardId => cardId !== allowedDiscardId)
    const fewer = new EmpiresEndgameEngine(fewerValue, winnerSnapshot(fewerValue))
    expect(fewer.endAttack('player').ok).toBe(true)
    expect(fewer.state.god.interventions[0].returnedInstanceIds).toEqual([allowedDiscardId])

    const thresholdValue = config()
    const thresholdState = winnerSnapshot(thresholdValue)
    thresholdState.durak.consecutiveBito = Math.max(
      0,
      thresholdValue.god.antiBito.minimumConsecutiveBito - 2,
    )
    const threshold = new EmpiresEndgameEngine(thresholdValue, thresholdState)
    expect(threshold.endAttack('player').ok).toBe(true)
    expect(threshold.state.phase).toBe('victory')
    expect(threshold.state.durak.godInterventions).toBe(0)

    const cappedValue = config()
    const cappedState = winnerSnapshot(cappedValue)
    cappedState.durak.godInterventions = cappedValue.god.antiBito.maxInterventions
    const capped = new EmpiresEndgameEngine(cappedValue, cappedState)
    expect(capped.endAttack('player').ok).toBe(true)
    expect(capped.state.phase).toBe('victory')
    expect(capped.state.durak.godInterventions).toBe(cappedValue.god.antiBito.maxInterventions)
  })

  it('orders authored triggers, preserves once state, bounds logs, and isolates cosmetic RNG', () => {
    const value = config()
    value.god.dialogueLogRetention = 2
    value.god.lines = [
      line('take-once', 'take', true),
      line('invert', 'inversion'),
      line('loss', 'boutLost'),
      line('anti-a', 'antiBito', false, 2),
      line('anti-b', 'antiBito', false, 1),
    ]
    const preIntervention = takeSnapshot(value)
    const first = new EmpiresEndgameEngine(value, preIntervention)
    const replay = new EmpiresEndgameEngine(value, preIntervention)

    expect(first.endAttack('god').ok).toBe(true)
    expect(replay.endAttack('god').ok).toBe(true)
    expect(first.state.god.dialogueLog.map(entry => entry.trigger)).toEqual([
      'inversion', 'boutLost',
    ])
    expect(first.state.god.dialogueCompaction.evictedCount).toBe(1)
    expect(first.state.god.dialogueCompaction.historyDigest).toMatch(/^[0-9a-f]{16}$/)
    expect(first.state.god.interventions).toHaveLength(0)
    expect(first.state.durak.consecutiveBito).toBe(0)
    expect(replay.state.god).toEqual(first.state.god)
    expect(new EmpiresEndgameEngine(value, first.snapshot()).state.god.dialogueLog)
      .toEqual(first.state.god.dialogueLog)

    const repeated = new EmpiresEndgameEngine(value, takeSnapshot(value, first.snapshot()))
    expect(repeated.endAttack('god').ok).toBe(true)
    expect(repeated.state.god.dialogueOccurrences['take-once']).toBe(1)
    expect(repeated.state.god.dialogueOccurrences.loss).toBe(2)
    expect(repeated.state.god.dialogueLog).toHaveLength(2)

    const silentValue = config()
    silentValue.god.lines = []
    const silent = new EmpiresEndgameEngine(silentValue, winnerSnapshot(silentValue))
    const speakingValue = config()
    speakingValue.god.lines = [line('anti-heavy', 'antiBito', false, 3), line('anti-light', 'antiBito')]
    const speaking = new EmpiresEndgameEngine(speakingValue, winnerSnapshot(speakingValue))
    expect(silent.endAttack('player').ok).toBe(true)
    expect(speaking.endAttack('player').ok).toBe(true)
    expect(speaking.state.rng).toEqual(silent.state.rng)
    expect(speaking.state.god.interventions[0]).toEqual(silent.state.god.interventions[0])
    expect(speaking.state.god.cosmeticRng.draws).toBeGreaterThan(silent.state.god.cosmeticRng.draws)
    expect(speaking.state.god.dialogueLog.at(-1)?.lineId).toMatch(/^anti-(heavy|light)$/)
  })

  it('compacts intervention history without weakening the total intervention cap', () => {
    const value = config()
    value.god.antiBito.historyRetention = 1
    value.god.antiBito.maxInterventions = 3
    let engine = new EmpiresEndgameEngine(value, winnerSnapshot(value))
    expect(engine.endAttack('player').ok).toBe(true)
    engine = new EmpiresEndgameEngine(value, winnerSnapshot(value, engine.snapshot()))
    expect(engine.endAttack('player').ok).toBe(true)

    expect(engine.state.durak.godInterventions).toBe(2)
    expect(engine.state.god.interventions).toHaveLength(1)
    expect(engine.state.god.interventionCompaction).toMatchObject({ evictedCount: 1 })
    expect(engine.state.god.interventionCompaction.historyDigest).toMatch(/^[0-9a-f]{16}$/)
  })
})
