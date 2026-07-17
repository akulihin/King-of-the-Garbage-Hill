import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine, validateEmpiresEndgameConfig } from './engine'
import { createEmpiresQaScenarios } from './qa'
import { resolveTdWithPolicy } from './td/qa'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function config(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(defaultConfigJson)
}

function empireEngine(value = config()): EmpiresEndgameEngine {
  const initial = new EmpiresEndgameEngine(value).snapshot()
  initial.phase = 'empire'
  initial.empire.daysRemaining = value.empire.daysPerPhase
  for (const resource of value.empire.resources) initial.empire.resources[resource.id] = 10_000_000
  return new EmpiresEndgameEngine(value, initial)
}

function eventEngine(choiceEventId = 'event-northern-raids'): EmpiresEndgameEngine {
  const value = config()
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'event'
  state.event = { eventId: choiceEventId, empireSettlementPending: false }
  return new EmpiresEndgameEngine(value, state)
}

describe('Empire\'s Endgame Phase 4A politics', () => {
  it('validates every configured workforce entry and preserves the three authored anchors', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    expect(validateEmpiresEndgameConfig(value)).toEqual([])
    expect(value.empire.loyalty.workforceDivisors).toHaveLength(19)
    for (const entry of value.empire.loyalty.workforceDivisors) {
      expect(engine.workforceDivisorForLoyalty(entry.loyalty)).toBe(entry.divisor)
    }
    expect(engine.workforceDivisorForLoyalty(-9)).toBe(19)
    expect(engine.workforceDivisorForLoyalty(0)).toBe(9)
    expect(engine.workforceDivisorForLoyalty(9)).toBe(1)

    const invalid = config()
    invalid.empire.loyalty.workforceDivisors.splice(1, 1)
    expect(() => validateEmpiresConfig(invalid)).toThrow('every bound value')
  })

  it('clamps one mutation funnel, refreshes workforce and production, and records stable provenance', () => {
    const engine = empireEngine()
    const cityId = 'city-north-iron-gate'
    const neutral = engine.cityLoyaltyView(cityId)!
    expect(neutral.workforceDivisor).toBe(9)

    expect(engine.applyLoyaltyDelta({ kind: 'city', cityId }, 99, 'test:loyalty:max')).toBe(9)
    const loyal = engine.cityLoyaltyView(cityId)!
    expect(loyal.effectiveLoyalty).toBe(9)
    expect(loyal.workforceDivisor).toBe(1)
    expect(loyal.effectiveWorkforce).toBe(loyal.baseWorkforce)
    expect(engine.cityProduction(cityId).food).toBeGreaterThan(0)

    expect(engine.applyLoyaltyDelta({ kind: 'city', cityId }, -99, 'test:loyalty:min')).toBe(-18)
    const hostile = engine.cityLoyaltyView(cityId)!
    expect(hostile.effectiveLoyalty).toBe(-9)
    expect(hostile.workforceDivisor).toBe(19)
    expect(hostile.effectiveWorkforce).toBe(Math.floor(hostile.baseWorkforce / 19))
    expect(engine.state.empire.chronicle.map(entry => entry.id)).toEqual([
      'chronicle-00000001',
      'chronicle-00000002',
    ])
    expect(engine.state.empire.chronicle.at(-1)).toMatchObject({
      kind: 'loyalty',
      sourceId: 'test:loyalty:min',
      requestedAmount: -99,
      appliedAmount: -18,
    })
  })

  it('gates Smithy operation and construction through мещане loyalty', () => {
    const value = config()
    const smithy = value.empire.buildings.find(item => item.id === 'building-smithy')!
    for (const building of value.empire.buildings) {
      for (const level of building.levels) level.workerDemand = 0
    }
    const state = new EmpiresEndgameEngine(value).snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    state.empire.researchedTechnologyIds.push('tech-ironwork')
    const city = state.empire.cities.find(item => item.id === 'city-tetrakor-capital')!
    const engine = new EmpiresEndgameEngine(value, state)

    expect(engine.buildingOperationView(city.id, smithy.id).operationalLevel).toBe(1)
    engine.applyLoyaltyDelta({ kind: 'class', cityId: city.id, populationClassId: 'burghers' }, -1, 'test:burghers')
    expect(engine.buildingOperationView(city.id, smithy.id)).toMatchObject({
      purchasedLevel: 1,
      operationalLevel: 0,
    })
    expect(engine.buildingOperationView(city.id, smithy.id).blockedReason).toContain('Мещане')
    expect(engine.constructionBlockedReason(city.id, smithy.id, 2)).toContain('Мещане')
  })

  it('makes rebellion reversible and distinct from permanent destruction on every city action path', () => {
    const engine = empireEngine()
    engine.applyLoyaltyDelta({ kind: 'region', regionId: 'west' }, -6, 'test:west:1')
    expect(engine.isRegionAccessible('west')).toBe(true)
    engine.applyLoyaltyDelta({ kind: 'region', regionId: 'west' }, -1, 'test:west:2')
    expect(engine.regionAccessBlockedReason('west')).toBe('The region is in rebellion.')
    expect(engine.state.empire.destroyedRegionIds).not.toContain('west')

    const city = engine.state.empire.cities.find(item => item.regionId === 'west')!
    expect(engine.isCityAccessible(city.id)).toBe(false)
    expect(engine.cityProduction(city.id)).toEqual({})
    expect(engine.recruitmentQuote(city.id, 'unit-regular', 1).blockedReason)
      .toBe('The region is in rebellion.')
    expect(engine.constructionBlockedReason(city.id, 'building-farm', 2))
      .toBe('The region is in rebellion.')

    const restored = new EmpiresEndgameEngine(engine.config, engine.snapshot())
    restored.applyLoyaltyDelta({ kind: 'region', regionId: 'west' }, 7, 'test:west:recover-1')
    expect(restored.isRegionAccessible('west')).toBe(false)
    restored.applyLoyaltyDelta({ kind: 'region', regionId: 'west' }, 1, 'test:west:recover-2')
    expect(restored.isRegionAccessible('west')).toBe(true)
    expect(restored.state.empire.chronicle.some(entry => entry.kind === 'recovery')).toBe(true)

    restored.state.empire.destroyedRegionIds.push('west')
    expect(restored.regionAccessBlockedReason('west')).toBe('The region is destroyed.')
  })

  it('consumes canonical TD deployment losses once and does not reapply them after restore', () => {
    const value = config()
    const fixture = createEmpiresQaScenarios(value, { seed: 'phase4-loss' })['battle-defense']
    const engine = new EmpiresEndgameEngine(value, fixture.snapshot)
    const session = engine.state.minigame!
    const before = Object.fromEntries(engine.state.empire.cities.map(city => [city.id, city.loyalty]))
    const result = resolveTdWithPolicy(session.plan, session.seed, 'passive')
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })

    const aggregate = new Map<string, { deployed: number, lost: number }>()
    for (const deployment of result.deployments) {
      const current = aggregate.get(deployment.cityId) ?? { deployed: 0, lost: 0 }
      current.deployed += deployment.deployed
      current.lost += deployment.deployed - deployment.survived
      aggregate.set(deployment.cityId, current)
    }
    const penalized = [...aggregate].filter(([, loss]) => (
      loss.deployed > 0 && loss.lost / loss.deployed >= value.td.settlement!.lossLoyaltyThreshold
    ))
    expect(penalized.length).toBeGreaterThan(0)
    for (const [cityId] of penalized) {
      expect(engine.state.empire.cities.find(city => city.id === cityId)?.loyalty)
        .toBe((before[cityId] ?? 0) + value.td.settlement!.loyaltyDelta)
    }
    const after = engine.snapshot()
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(engine.snapshot().empire.loyalty).toEqual(after.empire.loyalty)
    const restored = new EmpiresEndgameEngine(value, after)
    expect(restored.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(restored.state.empire.loyalty).toEqual(after.empire.loyalty)
  })

  it('provides clamped reputation dependencies and bounded newest-first chronicle projection', () => {
    const value = config()
    const farm = value.empire.buildings.find(item => item.id === 'building-farm')!
    farm.levels[1].dependencies.push({ kind: 'reputation', minimum: 1 })
    value.empire.loyalty.chronicleRetention = 3
    const engine = empireEngine(value)
    const city = engine.state.empire.cities.find(item => (item.buildingLevels[farm.id] ?? 0) === 1)!
    expect(engine.constructionBlockedReason(city.id, farm.id, 2)).toContain('reputation +1')
    for (let index = 0; index < 5; index += 1) {
      engine.applyReputationDelta(index === 0 ? 20 : -1, `test:reputation:${index}`)
    }
    expect(engine.state.empire.reputation).toBe(5)
    expect(engine.state.empire.chronicle).toHaveLength(3)
    expect(engine.chronicleNewestFirst().map(entry => entry.sequence)).toEqual([5, 4, 3])
    expect(engine.constructionBlockedReason(city.id, farm.id, 2)).not.toContain('reputation')
  })

  it('resolves both northern-raids choices through typed loyalty, wood, chronicle, and restore', () => {
    const forbid = eventEngine()
    expect(forbid.chooseEvent('forbid-raids')).toMatchObject({ ok: true })
    expect(forbid.state.empire.loyalty.regions.west.value).toBe(2)
    expect(forbid.state.empire.loyalty.regions.north.value).toBe(-1)
    expect(forbid.state.empire.chronicle.map(entry => entry.sourceId)).toEqual([
      'event:event-northern-raids:forbid-raids',
      'event:event-northern-raids:forbid-raids',
    ])
    expect(new EmpiresEndgameEngine(forbid.config, forbid.snapshot()).state.empire.loyalty)
      .toEqual(forbid.state.empire.loyalty)

    const permit = eventEngine()
    const woodBefore = permit.state.empire.resources.wood
    expect(permit.chooseEvent('permit-raids')).toMatchObject({ ok: true })
    expect(permit.state.empire.resources.wood).toBe(woodBefore + 250_000)
    expect(permit.state.empire.loyalty.regions.north.value).toBe(1)
    expect(permit.state.empire.loyalty.regions.west.value).toBe(-2)
  })

  it('keeps the bundled Forum deferred but its confirmed operational reader doubles both signs temporally', () => {
    const bundled = config()
    expect(bundled.empire.buildings.find(item => item.id === 'municipal-capital-forum')?.deferredReason)
      .toBeTruthy()
    expect(bundled.cards.find(item => item.id === 'card-clubs-2')?.inverted.deferredReason)
      .toBeTruthy()
    const concession = bundled.empire.events.find(item => item.id === 'event-lumber-concession')!
    expect(concession.deferredReason).toContain('follow-up')
    expect(concession.choices.flatMap(choice => choice.effects).some(effect => (
      effect.kind === 'flag' && /^loyalty(?:North|West)$/.test(effect.flagId)
    ))).toBe(false)

    const value = config()
    const forum = value.empire.buildings.find(item => item.id === 'municipal-capital-forum')!
    delete forum.deferredReason
    forum.levels[0].effects = forum.levels[0].effects?.filter(
      effect => effect.kind === 'flag' && effect.flagId === 'loyaltyMultiplierPercent',
    )
    const state = new EmpiresEndgameEngine(value).snapshot()
    const city = state.empire.cities.find(item => item.id === 'city-tetrakor-capital')!
    city.buildingLevels[forum.id] = 1
    city.operationalBuildingLevels[forum.id] = 1
    city.buildingSlotAssignments['slot-municipal'] = forum.id
    const positive = new EmpiresEndgameEngine(value, state)
    positive.applyLoyaltyDelta({ kind: 'city', cityId: city.id }, 2, 'test:forum:positive')
    expect(positive.effectiveCityLoyalty(city.id)).toBe(4)

    const negative = new EmpiresEndgameEngine(value, state)
    negative.applyLoyaltyDelta({ kind: 'city', cityId: city.id }, -2, 'test:forum:negative')
    expect(negative.effectiveCityLoyalty(city.id)).toBe(-4)
    negative.state.empire.cities.find(item => item.id === city.id)!.operationalBuildingLevels[forum.id] = 0
    expect(negative.effectiveCityLoyalty(city.id)).toBe(-2)
  })

  it('migrates magic political flags and queued Phase-3 losses without retaining live aliases', () => {
    const value = config()
    const legacy = new EmpiresEndgameEngine(value).snapshot() as EmpiresCampaignState
    legacy.schemaVersion = 3 as never
    const city = legacy.empire.cities[0]
    legacy.empire.flags[`loyalty:${city.id}`] = 4
    legacy.empire.flags.loyaltyNorth = -3
    legacy.empire.flags.reputation = 2
    delete (legacy.empire as Partial<typeof legacy.empire>).loyalty
    delete (legacy.empire as Partial<typeof legacy.empire>).chronicle
    delete (legacy.empire as Partial<typeof legacy.empire>).nextChronicleSequence
    delete (legacy.empire as Partial<typeof legacy.empire>).reputation
    legacy.army.pendingLoyaltyDeltas = [{ cityId: city.id, amount: -1, sourceId: 'td:legacy' }]

    const restored = new EmpiresEndgameEngine(value, legacy)
    expect(restored.state.schemaVersion).toBe(4)
    expect(restored.state.empire.cities[0].loyalty).toBe(3)
    expect(restored.state.empire.loyalty.regions.north.value).toBe(-3)
    expect(restored.state.empire.reputation).toBe(2)
    expect(restored.state.empire.flags).not.toHaveProperty(`loyalty:${city.id}`)
    expect(restored.state.empire.flags).not.toHaveProperty('loyaltyNorth')
    expect(restored.state.empire.flags).not.toHaveProperty('reputation')
    expect(restored.state.army.pendingLoyaltyDeltas).toBeUndefined()
    expect(restored.state.empire.chronicle.map(entry => entry.kind)).toEqual(['battle-loss', 'loyalty'])
  })
})
