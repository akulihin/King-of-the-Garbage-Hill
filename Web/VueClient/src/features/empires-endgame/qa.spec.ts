import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { EmpiresEndgameEngine } from './engine'
import {
  EMPIRES_QA_SCENARIO_NAMES,
  EMPIRES_STABILIZATION_SEED_MATRIX,
  checkEmpiresQaPlayerTurnInvariant,
  createEmpiresQaScenarios,
  digestEmpiresQaState,
  executeEmpiresQaPlayerCardAction,
  inspectEmpiresQaDeck,
  listEmpiresQaPlayerCardActions,
  runEmpiresQaAutoplay,
  runEmpiresStabilizationCampaign,
  validateEmpiresQaSnapshot,
} from './qa'
import { EMPIRES_STABILIZATION_BUDGETS } from './stabilization'
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
      const restored = new EmpiresEndgameEngine(config, serialized).snapshot()
      if (serialized.minigame) {
        expect(restored).toEqual({
          ...serialized,
          minigame: { ...serialized.minigame!, attempt: serialized.minigame!.attempt + 1 },
        })
      } else {
        expect(restored).toEqual(serialized)
      }
    }

    expect(fixtures['divine-gift'].snapshot.phase).toBe('divineGift')
    expect(fixtures['divine-gift'].snapshot.giftChoiceIds.every((giftId) =>
      !config.gifts.definitions.find(gift => gift.id === giftId)?.deferredReason)).toBe(true)
    expect(fixtures['target-city-resources'].snapshot).toMatchObject({
      phase: 'divineGift',
      pendingResolution: { kind: 'cityResources' },
    })
    expect(fixtures['target-meteor-city'].snapshot).toMatchObject({
      phase: 'divineGift',
      pendingResolution: { kind: 'meteorCity' },
    })
    expect(fixtures['empire-council-with-points'].snapshot).toMatchObject({
      phase: 'empire',
      upgradePoints: 3,
    })
    expect(digestEmpiresQaState(new EmpiresEndgameEngine(
      config,
      fixtures['domestic-economy'].snapshot,
    ))).toMatchObject({
      activeLoanCount: 1,
      insuranceContractCount: 0,
      activeFairActivityCount: 0,
    })
    expect(fixtures['economy-content-event'].snapshot).toMatchObject({
      phase: 'event',
      event: {
        eventId: config.empire.economyContent.smuggling.eventId,
        targetCityId: expect.any(String),
      },
    })
    expect(digestEmpiresQaState(new EmpiresEndgameEngine(
      config,
      fixtures['economy-content-event'].snapshot,
    ))).toMatchObject({
      economyEventHistoryCount: 0,
      smugglingPolicyActive: false,
      horsePactActive: false,
    })
    expect(digestEmpiresQaState(new EmpiresEndgameEngine(
      config,
      fixtures['quest-dialogue'].snapshot,
    ))).toMatchObject({
      activeMandatoryQuestId: 'quest-palach',
      activeQuestStatus: 'active',
      activeQuestNodeId: 'palach-p28',
      questHistoryCount: 0,
      questStateDigest: expect.stringContaining('"id":"quest-palach"'),
    })
    expect(fixtures['destroyed-west'].snapshot.empire.destroyedRegionIds).toContain('west')
    expect(fixtures['relic-production-levels'].snapshot.empire.buildingLevelBonuses).toMatchObject({
      farm: 1,
      lumber: 1,
    })
    expect(digestEmpiresQaState(new EmpiresEndgameEngine(
      config,
      fixtures['season-disclosure'].snapshot,
    ))).toMatchObject({
      con: 2,
      seasonId: 'winter',
      reputation: -1,
      technologyDisclosureCount: 1,
    })
    expect(fixtures['battle-defense'].snapshot).toMatchObject({
      phase: 'minigame',
      minigame: {
        kind: 'td',
        attempt: 0,
        plan: { mode: 'defense', battlefield: { regionId: 'center' } },
      },
    })
    expect(fixtures['battle-assault'].snapshot.minigame?.plan).toMatchObject({
      mode: 'assault',
      battlefield: { regionId: 'center' },
      objective: { owner: 'enemy', kind: 'fort' },
    })
    expect(fixtures['battle-swamp'].snapshot.minigame?.plan.battlefield.regionId).toBe('east')
    expect(fixtures['battle-forest'].snapshot.minigame?.plan.battlefield.regionId).toBe('west')
    expect(fixtures['battle-north'].snapshot.minigame?.plan).toMatchObject({
      battlefield: { regionId: 'north' },
      gradeChoices: expect.arrayContaining([
        expect.objectContaining({ grade: 1, choiceIds: [], deferredReason: expect.any(String) }),
      ]),
    })
    expect(fixtures['battle-desert'].snapshot.minigame?.plan).toMatchObject({
      battlefield: {
        regionId: 'south',
        modifiers: expect.arrayContaining([
          expect.objectContaining({ kind: 'deployment-attrition' }),
        ]),
      },
    })
    for (const name of EMPIRES_QA_SCENARIO_NAMES.filter(name => name.startsWith('battle-'))) {
      const battleEngine = new EmpiresEndgameEngine(config, fixtures[name].snapshot)
      const digest = digestEmpiresQaState(battleEngine)
      expect(digest.minigameRulesSchemaVersion).toBe(config.schemaVersion)
      expect(digest.minigameRulesDigest).toBe(fixtures[name].snapshot.minigame?.rulesIdentity.rulesDigest)
      expect(digest.minigameCommandLimit).toBe(config.td.maxCommands)
      expect(digest.minigameResultLimit).toBe(config.td.resultLogLimit)
      expect(digest).toMatchObject({
        minigameResultEvictedCount: 0,
        minigameResultHistoryDigest: '',
        minigameResultLastSessionId: null,
        minigameResultLastRulesDigest: null,
      })
    }
    expect(fixtures.event.snapshot).toMatchObject({ phase: 'event' })
    expect(config.empire.events.find(event =>
      event.id === fixtures.event.snapshot.event?.eventId)?.deferredReason).toBeUndefined()
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
    const choice = event?.choices.find(item => !item.deferredReason)

    expect(event?.deferredReason).toBeUndefined()
    expect(choice).toBeDefined()
    expect(engine.chooseEvent(choice?.id ?? '')).toMatchObject({ ok: true })
    expect(engine.state.phase).not.toBe('event')
  })

  it('keeps targeted city resources isolated to the selected city', () => {
    const config = defaultConfig()
    const fixture = createEmpiresQaScenarios(config, { seed: 'qa-city-resources' })['target-city-resources']
    const engine = new EmpiresEndgameEngine(config, fixture.snapshot)
    const pending = engine.state.pendingResolution
    expect(pending?.kind).toBe('cityResources')
    if (!pending || pending.kind !== 'cityResources') throw new Error('Missing city-resource resolution.')
    const targetId = pending.eligibleTargetIds[0]
    const otherId = pending.eligibleTargetIds.find(id => id !== targetId)
    const targetBefore = structuredClone(engine.state.empire.cities.find(city => city.id === targetId)?.resources ?? {})
    const otherBefore = structuredClone(engine.state.empire.cities.find(city => city.id === otherId)?.resources ?? {})

    expect(engine.resolvePendingTarget(targetId)).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('empire')
    const targetAfter = engine.state.empire.cities.find(city => city.id === targetId)?.resources ?? {}
    const otherAfter = engine.state.empire.cities.find(city => city.id === otherId)?.resources ?? {}
    expect(targetAfter).not.toEqual(targetBefore)
    expect(otherAfter).toEqual(otherBefore)
  })

  it('applies meteor damage only to the selected city', () => {
    const config = defaultConfig()
    const fixture = createEmpiresQaScenarios(config, { seed: 'qa-meteor' })['target-meteor-city']
    const engine = new EmpiresEndgameEngine(config, fixture.snapshot)
    const pending = engine.state.pendingResolution
    expect(pending?.kind).toBe('meteorCity')
    if (!pending || pending.kind !== 'meteorCity') throw new Error('Missing meteor resolution.')
    const targetId = pending.eligibleTargetIds.find((id) => {
      const city = engine.state.empire.cities.find(item => item.id === id)
      return city && Object.entries(city.buildingLevels).some(([buildingId, level]) => (
        level > 0
        && !config.empire.buildings.find(building => building.id === buildingId)?.deferredReason
      ))
    }) as string
    const otherId = pending.eligibleTargetIds.find(id => id !== targetId) as string
    const targetBefore = structuredClone(engine.state.empire.cities.find(city => city.id === targetId)?.buildingLevels ?? {})
    const otherBefore = structuredClone(engine.state.empire.cities.find(city => city.id === otherId)?.buildingLevels ?? {})

    expect(engine.resolvePendingTarget(targetId)).toMatchObject({ ok: true })
    const targetAfter = engine.state.empire.cities.find(city => city.id === targetId)?.buildingLevels ?? {}
    const otherAfter = engine.state.empire.cities.find(city => city.id === otherId)?.buildingLevels ?? {}
    expect(Object.keys(targetBefore).some(id => targetAfter[id] < targetBefore[id])).toBe(true)
    expect(otherAfter).toEqual(otherBefore)
  })

  it('restores accessibility and effective building levels in named world fixtures', () => {
    const config = defaultConfig()
    const fixtures = createEmpiresQaScenarios(config)
    const destroyed = new EmpiresEndgameEngine(config, fixtures['destroyed-west'].snapshot)
    const relic = new EmpiresEndgameEngine(config, fixtures['relic-production-levels'].snapshot)
    const city = relic.state.empire.cities.find(item => (item.buildingLevels['building-farm'] ?? 0) > 0)
    expect(destroyed.isRegionAccessible('west')).toBe(false)
    expect(destroyed.isCityAccessible('city-west-green-bastion')).toBe(false)
    expect(city).toBeDefined()
    expect(relic.effectiveBuildingLevel(city?.id ?? '', 'building-farm')).toBe(
      (city?.buildingLevels['building-farm'] ?? 0) + 1,
    )
    expect(relic.effectiveBuildingMaxLevel('building-farm')).toBeGreaterThan(
      Math.max(...config.empire.buildings
        .find(building => building.id === 'building-farm')!
        .levels.map(level => level.level)),
    )
  })
})

describe('Empire\'s Endgame traced seeded autoplay', () => {
  const seeds = ['qa-seed-1', 'qa-seed-2', 1701]

  it.each(seeds)('crosses the implemented campaign phases without a stalled player turn for seed %s', (seed) => {
    const config = defaultConfig()
    config.empire.eventChance = 1
    const result = runEmpiresQaAutoplay(config, { seed, maxSteps: 10_000 })

    expect(result.completed, result.stall?.message).toBe(true)
    expect(result.stall).toBeNull()
    expect(result.checkedPlayerTurns).toBeGreaterThan(0)
    expect(result.phaseVisits.cards).toBeGreaterThan(0)
    expect(result.phaseVisits.divineGift).toBeGreaterThan(0)
    expect(result.phaseVisits.empire).toBeGreaterThan(0)
    expect(result.phaseVisits.minigame).toBeGreaterThan(1)
    expect(result.resolvedMinigames.map(item => item.mode)).toEqual(
      expect.arrayContaining(['defense', 'assault']),
    )
    expect(result.resolvedMinigames.every(item => item.rulesDigest.length > 0)).toBe(true)
    expect(result.trace.some(entry => (
      entry.action.kind === 'resolve-minigame'
      && entry.before.minigameMode === 'defense'
      && entry.before.minigameRegionId === 'center'
      && entry.before.minigameRulesDigest
    ))).toBe(true)
    expect(result.trace.some(entry => (
      entry.action.kind === 'resolve-minigame'
      && entry.before.minigameMode === 'assault'
      && entry.before.minigameRegionId === 'center'
      && entry.before.minigameRulesDigest
    ))).toBe(true)
    expect(result.snapshot.minigameResultLog.length).toBeLessThanOrEqual(config.td.resultLogLimit!)
    if (result.phaseVisits.event > 0) expect(result.resolvedEventIds.length).toBeGreaterThan(0)
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

describe('Empire\'s Endgame Phase 13 stabilization campaign', () => {
  it('crosses every live system within action/save budgets and repeats the critical seed exactly', () => {
    const config = defaultConfig()
    const runs = EMPIRES_STABILIZATION_SEED_MATRIX.map(seed => (
      runEmpiresStabilizationCampaign(config, seed)
    ))

    for (const run of runs) {
      expect(Object.values(run.coverage).every(Boolean)).toBe(true)
      expect(run.steps).toBe(run.autoplaySteps + 35)
      expect(run.steps).toBeLessThanOrEqual(EMPIRES_STABILIZATION_BUDGETS.qaActions)
      expect(run.saveUtf8Bytes).toBeLessThanOrEqual(
        EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes,
      )
      expect(run.checkpointDigests).toHaveLength(13)
      expect(run.finalDigest).toMatch(/^[0-9a-f]{16}$/)

      expect(run.snapshot.phase).toBe('event')
      expect(run.snapshot.event?.eventId).toBe('event-famine-rationing')
      expect(run.snapshot.pendingResolution).toBeNull()
      expect(run.snapshot.minigame).toBeNull()
      expect(run.snapshot.minigameResultLog.map(record => record.sequence)).toEqual([1, 2, 3, 4, 5, 6])
      expect(run.snapshot.minigameResultLog.map(record => record.result.kind)).toEqual([
        'tavern',
        'alchemy',
        'td',
        'td',
        'inventory',
        'td',
      ])
      expect(run.snapshot.empire.claimedGiftIds).toContain('gift-resource-grant')
      expect(run.snapshot.empire.domesticEconomy.loans).toHaveLength(1)
      expect(run.snapshot.empire.domesticEconomy.insuranceContracts).toHaveLength(1)
      expect(run.snapshot.empire.domesticEconomy.fair.activeActivities).toHaveLength(1)
      expect(run.snapshot.external.offerHistory).toHaveLength(2)
      expect(run.snapshot.questRuntime.history.length).toBeGreaterThan(0)
      expect(run.snapshot.god.interventions).toHaveLength(1)
      expect(run.snapshot.mystics.zone).toContain(config.tavern.queen.mysticDefinitionId)
      expect(run.snapshot.mystics.history).toEqual(expect.arrayContaining([
        expect.objectContaining({
          kind: 'spawn',
          instanceIds: [config.tavern.queen.mysticDefinitionId],
        }),
      ]))
      expect(run.snapshot.empire.loyalty.regions.west?.status).toBe('rebellious')
      expect(run.snapshot.empire.loyalty.regions.north?.status).not.toBe('rebellious')
      expect(run.snapshot.expeditions.byDefinitionId['expedition-south-fortress']?.status).toBe('won')
      expect(run.snapshot.empire.chronicle.map(entry => entry.kind)).toEqual(expect.arrayContaining([
        'loan',
        'insurance',
        'fair',
        'rebellion',
        'recovery',
        'tavern',
        'alchemy',
        'battle-loss',
        'expedition',
        'epidemic-impact',
      ]))
    }

    const repeated = runEmpiresStabilizationCampaign(config, 'phase13-beta')
    expect(repeated).toEqual(runs.find(run => run.seed === 'phase13-beta'))
  }, 120_000)
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
        && definition?.suit !== config.governance.trump.restrictedSuit
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
