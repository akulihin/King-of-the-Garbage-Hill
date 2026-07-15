import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { EmpiresEndgameEngine } from './engine'
import {
  EMPIRES_QA_SCENARIO_NAMES,
  checkEmpiresQaPlayerTurnInvariant,
  createEmpiresQaScenarios,
  executeEmpiresQaPlayerCardAction,
  inspectEmpiresQaDeck,
  listEmpiresQaPlayerCardActions,
  runEmpiresQaAutoplay,
  validateEmpiresQaSnapshot,
} from './qa'
import { createEmpiresRngState, shuffleEmpires } from './rng'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function defaultConfig(): EmpiresEndgameConfig {
  return JSON.parse(JSON.stringify(defaultConfigJson)) as EmpiresEndgameConfig
}

describe('Empire\'s Endgame deterministic QA scenarios', () => {
  it('builds every named scenario as a complete validated and restorable snapshot', () => {
    const config = defaultConfig()
    const fixtures = createEmpiresQaScenarios(config, { seed: 'qa-scenarios' })

    expect(Object.keys(fixtures)).toEqual([...EMPIRES_QA_SCENARIO_NAMES])
    for (const name of EMPIRES_QA_SCENARIO_NAMES) {
      const fixture = fixtures[name]
      expect(fixture.name).toBe(name)
      expect(fixture.validation).toEqual({ ok: true, issues: [] })
      expect(validateEmpiresQaSnapshot(config, fixture.snapshot, name)).toEqual({ ok: true, issues: [] })
      const serialized = JSON.parse(JSON.stringify(fixture.snapshot)) as EmpiresCampaignState
      expect(new EmpiresEndgameEngine(config, serialized).snapshot()).toEqual(serialized)
    }

    expect(fixtures['divine-gift'].snapshot.phase).toBe('divineGift')
    expect(fixtures['empire-council-with-points'].snapshot).toMatchObject({
      phase: 'empire',
      upgradePoints: 3,
    })
    expect(fixtures.event.snapshot).toMatchObject({ phase: 'event' })
    expect(fixtures.victory.snapshot).toMatchObject({ phase: 'victory' })
    expect(fixtures.defeat.snapshot).toMatchObject({ phase: 'defeat' })
  })

  it('keeps an explicit action available after the player throws their last card', () => {
    const config = defaultConfig()
    const fixtures = createEmpiresQaScenarios(config)
    const pending = new EmpiresEndgameEngine(config, fixtures['pending-take'].snapshot)
    const empty = new EmpiresEndgameEngine(config, fixtures['empty-hand-pending-finish'].snapshot)

    expect(listEmpiresQaPlayerCardActions(pending).some(action => action.kind === 'play-card')).toBe(true)
    expect(checkEmpiresQaPlayerTurnInvariant(pending)).toMatchObject({ applies: true, ok: true })
    expect(empty.state.durak.playerHand).toEqual([])
    expect(listEmpiresQaPlayerCardActions(empty)).toEqual([
      { kind: 'end-attack', actor: 'player' },
    ])
    const check = checkEmpiresQaPlayerTurnInvariant(empty)
    expect(check).toMatchObject({ applies: true, ok: true, handSize: 0 })
    expect(executeEmpiresQaPlayerCardAction(empty, check.actions[0])).toMatchObject({ ok: true })
    expect(empty.state.durak.table).toEqual([])
  })

  it('reports a card-turn soft lock with state and action diagnostics', () => {
    const config = defaultConfig()
    const fixture = createEmpiresQaScenarios(config)['empty-hand-pending-finish']
    const invalid = JSON.parse(JSON.stringify(fixture.snapshot)) as EmpiresCampaignState
    invalid.durak.deck.push(...invalid.durak.table.map(pair => pair.attackCardId))
    invalid.durak.table = []
    invalid.durak.stage = 'attack'

    const engine = new EmpiresEndgameEngine(config, invalid)
    expect(checkEmpiresQaPlayerTurnInvariant(engine)).toMatchObject({ applies: true, ok: false })
    const result = runEmpiresQaAutoplay(config, { startSnapshot: invalid, maxSteps: 10 })
    expect(result).toMatchObject({
      completed: false,
      steps: 0,
      checkedPlayerTurns: 1,
      stall: {
        code: 'no-player-action',
        at: { phase: 'cards', stage: 'attack', playerHandCount: 0 },
        availablePlayerActions: [],
      },
    })
  })

  it('resolves the named event fixture through the same public engine action', () => {
    const config = defaultConfig()
    const fixture = createEmpiresQaScenarios(config, { seed: 'qa-event-fixture' }).event
    const engine = new EmpiresEndgameEngine(config, fixture.snapshot)
    const event = config.empire.events.find(item => item.id === engine.state.event?.eventId)

    expect(event?.choices.length).toBeGreaterThan(0)
    expect(engine.chooseEvent(event?.choices[0].id ?? '')).toMatchObject({ ok: true })
    expect(engine.state.phase).not.toBe('event')
  })
})

describe('Empire\'s Endgame traced seeded autoplay', () => {
  const seeds = ['qa-seed-1', 'qa-seed-2', 1701]

  it.each(seeds)('crosses cards, gifts, empire and events without a stalled player turn for seed %s', (seed) => {
    const config = defaultConfig()
    config.empire.eventChance = 1
    const result = runEmpiresQaAutoplay(config, { seed, maxSteps: 10_000 })

    expect(result.completed, result.stall?.message).toBe(true)
    expect(result.stall).toBeNull()
    expect(result.checkedPlayerTurns).toBeGreaterThan(0)
    expect(result.phaseVisits.cards).toBeGreaterThan(0)
    expect(result.phaseVisits.divineGift).toBeGreaterThan(0)
    expect(result.phaseVisits.empire).toBeGreaterThan(0)
    expect(result.phaseVisits.event).toBeGreaterThan(0)
    expect(result.resolvedEventIds.length).toBeGreaterThan(0)
    expect(result.trace.every(entry => entry.result.ok)).toBe(true)
    expect(['victory', 'defeat']).toContain(result.snapshot.phase)
  })

  it('replays the same seed to the same action trace, event sequence and final state', () => {
    const config = defaultConfig()
    config.empire.eventChance = 1
    const first = runEmpiresQaAutoplay(config, { seed: 'qa-repeatable' })
    const second = runEmpiresQaAutoplay(config, { seed: 'qa-repeatable' })

    expect(first.stall).toBeNull()
    expect(second.stall).toBeNull()
    expect(second.resolvedEventIds).toEqual(first.resolvedEventIds)
    expect(second.trace).toEqual(first.trace)
    expect(second.snapshot).toEqual(first.snapshot)
  })
})

describe('Empire\'s Endgame deck orientation', () => {
  it('uses deck[0] as the retained trump end and pop() as the next draw', () => {
    const config = defaultConfig()
    config.seed = 'qa-trump-bottom'
    delete config.durak.fixedTrumpSuit
    const shuffled = shuffleEmpires(
      config.cards.map(card => card.id),
      createEmpiresRngState(config.seed),
    )
    const engine = new EmpiresEndgameEngine(config)
    const inspection = inspectEmpiresQaDeck(engine)
    const expectedTrumpCardId = shuffled.find((cardId) => {
      const definition = config.cards.find(card => card.id === cardId)
      return definition?.rank !== 'joker'
    }) as string
    const expectedTrumpSuit = config.cards.find(card => card.id === expectedTrumpCardId)?.suit

    expect(engine.state.durak.deck[0]).toBe(shuffled[0])
    expect(engine.state.durak.playerHand).toEqual(shuffled.slice(-6).reverse())
    expect(engine.state.durak.godHand).toEqual(shuffled.slice(-12, -6).reverse())
    expect(inspection).toMatchObject({
      bottomCardId: shuffled[0],
      nextDrawCardId: shuffled.at(-13),
      trumpSourceCardId: expectedTrumpCardId,
      configuredTrumpSuit: null,
      expectedTrumpSuit,
      actualTrumpSuit: expectedTrumpSuit,
      ok: true,
    })
    expect(inspection.nextDrawCardId).toBe(engine.state.durak.deck.at(-1))
  })
})
