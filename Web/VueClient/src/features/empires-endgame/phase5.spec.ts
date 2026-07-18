import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { allocateEpidemicClassLoss } from './epidemics'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function config(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(defaultConfigJson)
}

function empireEngine(value: EmpiresEndgameConfig, eventChance = 0): EmpiresEndgameEngine {
  value.empire.eventChance = eventChance
  const initial = new EmpiresEndgameEngine(value).snapshot()
  initial.phase = 'empire'
  initial.event = null
  initial.minigame = null
  initial.outcomeReason = null
  initial.empire.daysRemaining = value.empire.daysPerPhase
  initial.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  initial.empire.resources[value.empire.foodResourceId] = 1_000_000_000
  for (const city of initial.empire.cities) {
    city.resources[value.empire.foodResourceId] = 1_000_000_000
  }
  return new EmpiresEndgameEngine(value, initial)
}

function origin(engine: EmpiresEndgameEngine) {
  return [...engine.state.empire.cities]
    .filter(city => engine.isCityAccessible(city.id))
    .sort((left, right) => left.id.localeCompare(right.id))[0]
}

function startPlague(engine: EmpiresEndgameEngine, cityId = origin(engine).id) {
  return engine.startEpidemic({
    definitionId: 'epidemic-plague',
    originCityId: cityId,
    source: { kind: 'qa', id: `qa:plague:${cityId}` },
  })
}

describe('Empire\'s Endgame Phase 5 epidemics', () => {
  it('migrates v7 into disabled epidemic/medical and economy scaffolds without mutation and rejects v14', () => {
    const previous = structuredClone(defaultConfigJson) as unknown as Record<string, unknown>
    previous.schemaVersion = 7
    const empire = previous.empire as Record<string, unknown>
    delete empire.epidemics
    delete empire.medical
    delete empire.domesticEconomy
    const cards = previous.cards as Array<Record<string, unknown>>
    const vaccination = cards.find(card => card.id === 'card-spades-10')!
    for (const sideName of ['normal', 'inverted']) {
      const side = vaccination[sideName] as Record<string, unknown>
      side.effects = []
      side.deferredReason = 'Legacy P4 deferred face.'
    }
    const hidden = empire.hiddenCombinations as { definitions: Array<Record<string, unknown>> }
    delete hidden.definitions[0].epidemicStart
    hidden.definitions[0].deferredReason = 'Legacy P4 deferred combination.'
    const gifts = (previous.gifts as { definitions: Array<Record<string, unknown>> }).definitions
    gifts.find(gift => gift.id === 'relic-epidemic-ward')!.deferredReason = 'Legacy P4 deferred relic.'
    const original = structuredClone(previous)

    const migrated = migrateEmpiresConfig(previous) as EmpiresEndgameConfig
    expect(previous).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 13,
      empire: {
        epidemics: { enabled: false, definitions: [], protections: [] },
        medical: { enabled: false, defaultBattleRecoveryCons: 2 },
        domesticEconomy: { enabled: false },
      },
    })
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 14 })).toThrow(/future.*14/i)
  })

  it('allocates class loss by authored weights with stable remainder and capacity handling', () => {
    expect(allocateEpidemicClassLoss(
      7,
      [
        { populationClassId: 'z', weight: 1 },
        { populationClassId: 'a', weight: 1 },
        { populationClassId: 'm', weight: 1 },
      ],
      { a: 10, m: 10, z: 10 },
    )).toEqual({ z: 2, a: 3, m: 2 })
    expect(allocateEpidemicClassLoss(
      8,
      [
        { populationClassId: 'a', weight: 9 },
        { populationClassId: 'b', weight: 1 },
      ],
      { a: 2, b: 20 },
    )).toEqual({ a: 2, b: 6 })
  })

  it('validates starts, preserves provenance, ignores plague duplicates, and rejects inaccessible origins', () => {
    const engine = empireEngine(config())
    const city = origin(engine)
    expect(startPlague(engine, city.id)).toMatchObject({ ok: true })
    const first = engine.state.epidemics[0]
    expect(first).toMatchObject({
      id: `epidemic:epidemic-plague:${city.id}:1`,
      source: { kind: 'qa', id: `qa:plague:${city.id}` },
      originCityId: city.id,
      stageId: 'outbreak',
      remainingDuration: 4,
    })
    expect(startPlague(engine, city.id)).toMatchObject({ ok: true })
    expect(engine.state.epidemics).toHaveLength(1)
    expect(engine.startEpidemic({
      definitionId: 'missing',
      originCityId: city.id,
      source: { kind: 'qa', id: 'qa:missing' },
    })).toMatchObject({ ok: false })

    const inaccessible = engine.state.empire.cities.find(candidate => candidate.regionId !== city.regionId)!
    engine.state.empire.destroyedRegionIds.push(inaccessible.regionId)
    expect(startPlague(engine, inaccessible.id)).toMatchObject({ ok: false })
    const restored = new EmpiresEndgameEngine(engine.config, engine.snapshot())
    expect(restored.state.epidemics).toEqual(engine.state.epidemics)
    expect(restored.epidemicRulesIdentity()).toEqual(engine.epidemicRulesIdentity())
  })

  it('settles impact before decay, applies it once per con, spreads deterministically, and ends cleanly', () => {
    const value = config()
    value.empire.epidemics.maxSpreadTargetsPerSettlement = 1
    const plague = value.empire.epidemics.definitions.find(item => item.id === 'epidemic-plague')!
    for (const stage of plague.stages) stage.spreadChance = 1
    const engine = empireEngine(value)
    const city = origin(engine)
    const before = city.population
    expect(startPlague(engine, city.id).ok).toBe(true)
    const beforeSettlement = engine.snapshot()
    expect(engine.finishEmpire().ok).toBe(true)
    const parent = engine.state.epidemics.find(item => item.originCityId === city.id)!
    expect(parent.lastImpact).toMatchObject({ con: 1, populationLoss: Math.round(before * 0.005) })
    expect(engine.state.empire.cities.find(item => item.id === city.id)!.population)
      .toBe(before - parent.lastImpact!.populationLoss)
    expect(parent.stageId).toBe('crisis')
    expect(parent.spread.spreadTargetCityIds).toHaveLength(1)
    expect(engine.state.epidemics.filter(item => item.endedAtCon === null)).toHaveLength(2)

    const replay = new EmpiresEndgameEngine(value, beforeSettlement)
    expect(replay.finishEmpire().ok).toBe(true)
    expect(replay.snapshot()).toEqual(engine.snapshot())

    const exactOnce = engine.snapshot()
    exactOnce.phase = 'empire'
    exactOnce.con = 1
    exactOnce.empire.daysRemaining = value.empire.daysPerPhase
    exactOnce.external.nextWaveCon = Number.MAX_SAFE_INTEGER
    const idempotent = new EmpiresEndgameEngine(value, exactOnce)
    const populationAfterImpact = idempotent.state.empire.cities.find(item => item.id === city.id)!.population
    expect(idempotent.finishEmpire().ok).toBe(true)
    expect(idempotent.state.empire.cities.find(item => item.id === city.id)!.population)
      .toBe(populationAfterImpact)
  })

  it('stacks only live protections multiplicatively and resolves relic 25 as consequence reduction', () => {
    const value = config()
    value.empire.epidemics.maxSpreadTargetsPerSettlement = 0
    const engine = empireEngine(value)
    const city = origin(engine)
    const academyCity = engine.state.empire.cities.find(candidate => candidate.id !== city.id)!
    engine.state.empire.researchedTechnologyIds.push('tech-medicine', 'doctrine-science')
    city.buildingLevels[value.empire.medical.hospitalBuildingId] = 1
    city.operationalBuildingLevels[value.empire.medical.hospitalBuildingId] = 1
    academyCity.buildingLevels[value.empire.medical.medicalAcademyBuildingId] = 1
    academyCity.operationalBuildingLevels[value.empire.medical.medicalAcademyBuildingId] = 1
    engine.state.empire.flags.vaccinationMortalityReductionPercent = 20
    engine.state.empire.flags.epidemicProtectionPercent = 25
    expect(startPlague(engine, city.id).ok).toBe(true)

    const view = engine.cityEpidemicViews(city.id)[0]
    expect(view.projectedNextImpact.populationLoss).toBe(Math.round(city.population * 0.005 * 0.9 * 0.5 * 0.8 * 0.75))
    expect(view.projectedNextImpact.productionLossPercent).toBeCloseTo(10 * 0.9 * 0.5 * 0.75)
    expect(view.protection.map(item => item.id)).toEqual(expect.arrayContaining([
      'hospital-local-impact',
      'medical-academy-impact',
      'vaccination-mortality',
      'epidemic-ward-impact',
    ]))

    city.buildingInteractionLocks[value.empire.medical.hospitalBuildingId] = engine.state.con
    expect(engine.cityEpidemicViews(city.id)[0].protection.map(item => item.id))
      .not.toContain('hospital-local-impact')
  })

  it('makes both City Gates choices executable when their reform carrier is live', () => {
    for (const choiceId of ['seal-gates', 'open-gates'] as const) {
      const value = config()
      value.empire.epidemics.maxSpreadTargetsPerSettlement = 0
      const reform = value.empire.technologies.find(item => item.id === 'reform-city-gates')!
      const event = value.empire.events.find(item => item.id === 'event-city-gates-epidemic')!
      delete reform.deferredReason
      delete event.deferredReason
      const engine = empireEngine(value)
      const city = origin(engine)
      engine.state.empire.researchedTechnologyIds.push(reform.id)
      expect(startPlague(engine, city.id).ok).toBe(true)
      engine.state.phase = 'event'
      engine.state.event = {
        eventId: event.id,
        epidemicInstanceId: engine.state.epidemics[0].id,
        empireSettlementPending: false,
      }
      expect(engine.chooseEvent(choiceId).ok).toBe(true)
      expect(engine.state.epidemics[0].containment).toMatchObject(choiceId === 'seal-gates'
        ? { mode: 'sealed', preventsIntercitySpread: true, localImpactMultiplier: 2 }
        : { mode: 'open', preventsIntercitySpread: false, localImpactMultiplier: 1 })
      expect(engine.state.empire.chronicle.filter(entry => entry.kind === 'epidemic-containment'))
        .toHaveLength(1)
    }
  })

  it('triggers the Granary plus Alchemy plague once and keeps only the P10 lab capability deferred', () => {
    const value = config()
    value.empire.epidemics.maxSpreadTargetsPerSettlement = 0
    const alchemy = value.empire.buildings.find(item => item.id === 'building-alchemy')!
    expect(alchemy.deferredReason).toBeUndefined()
    expect(alchemy.deferredSubfeatures).toEqual([
      expect.objectContaining({ id: 'alchemyMinigame' }),
    ])
    const engine = empireEngine(value)
    const city = origin(engine)
    city.buildingLevels[alchemy.id] = 1
    city.operationalBuildingLevels[alchemy.id] = 1
    engine.state.empire.researchedTechnologyIds.push('tech-medicine', 'reform-granary')
    expect(engine.finishEmpire().ok).toBe(true)
    expect(engine.state.empire.hiddenCombinationTriggers['hidden-granary-alchemy-plague'])
      .toMatchObject({ triggeredAtCon: 2 })
    expect(engine.state.epidemics).toHaveLength(1)
    expect(engine.state.epidemics[0].source.kind).toBe('hidden-combination')
    expect(new EmpiresEndgameEngine(value, engine.snapshot()).state.epidemics).toHaveLength(1)
  })

  it('activates both vaccination faces and the complete Academy treatment/free-tech contract', () => {
    const vaccination = config().cards.find(card => card.id === 'card-spades-10')!
    expect(vaccination.normal.deferredReason).toBeUndefined()
    expect(vaccination.inverted.deferredReason).toBeUndefined()
    expect(vaccination.inverted.effects).toContainEqual(expect.objectContaining({
      kind: 'epidemicStart',
      definitionId: 'epidemic-vaccination-pandemic',
    }))

    for (const inverted of [false, true]) {
      const cardConfig = config()
      cardConfig.durak.fixedTrumpSuit = 'hearts'
      cardConfig.empire.eventChance = 0
      const state = new EmpiresEndgameEngine(cardConfig).snapshot()
      const cardId = Object.values(state.cards).find(card => card.definitionId === 'card-spades-10')!.id
      state.durak.deck = state.durak.deck.filter(id => id !== cardId)
      state.durak.godHand = state.durak.godHand.filter(id => id !== cardId)
      state.durak.discard = state.durak.discard.filter(id => id !== cardId)
      state.durak.table = state.durak.table.filter(pair => (
        pair.attackCardId !== cardId && pair.defenseCardId !== cardId
      ))
      state.durak.playerHand = [cardId]
      state.cards[cardId].inverted = inverted
      const gift = cardConfig.gifts.definitions.find(item => (
        !item.deferredReason && !item.resolution && item.kind !== 'relic'
      ))!
      state.phase = 'divineGift'
      state.giftChoiceIds = [gift.id]
      const cardEngine = new EmpiresEndgameEngine(cardConfig, state)
      expect(cardEngine.chooseGift(gift.id).ok).toBe(true)
      if (inverted) {
        expect(cardEngine.state.epidemics).toEqual([
          expect.objectContaining({ definitionId: 'epidemic-vaccination-pandemic' }),
        ])
      } else {
        expect(cardEngine.state.empire.flags.vaccinationMortalityReductionPercent).toBe(10)
      }
    }

    const jenna = config().cards.find(card => card.id === 'card-clubs-ace')!
    expect(jenna.normal.title).toBe('Дженна')
    expect(jenna.normal.deferredReason).toBeTruthy()
    expect(jenna.inverted.deferredReason).toBeTruthy()

    const value = config()
    value.empire.epidemics.maxSpreadTargetsPerSettlement = 0
    value.empire.medical.academyTreatmentDeathChance = 0
    const engine = empireEngine(value)
    const city = origin(engine)
    city.buildingLevels[value.empire.medical.medicalAcademyBuildingId] = 1
    city.operationalBuildingLevels[value.empire.medical.medicalAcademyBuildingId] = 1
    engine.state.empire.researchedTechnologyIds.push('doctrine-science')
    engine.state.empire.medical.nextFreeResearchCon = 2
    engine.state.army.veterans.wounded = { unitId: 'unit-light', wounds: 1 }
    expect(engine.treatVeteran('wounded')).toMatchObject({ ok: true })
    expect(engine.state.army.veterans.wounded.wounds).toBe(0)
    expect(engine.treatVeteran('wounded')).toMatchObject({ ok: false })

    expect(engine.finishEmpire().ok).toBe(true)
    expect(engine.state.empire.medical.awardedTechnologyIds).toHaveLength(1)
    expect(engine.state.empire.researchedTechnologyIds)
      .toContain(engine.state.empire.medical.awardedTechnologyIds[0])
    expect(value.empire.units.find(unit => unit.id === value.empire.medical.healerUnitId)?.td?.healing)
      .toMatchObject({ chargesPerUnit: 2, amountPerUnit: 10000 })
  })

  it('keeps wounded survivors unavailable for two cons, or one with an operational Hospital', () => {
    for (const hospital of [false, true]) {
      const value = config()
      const engine = empireEngine(value)
      const city = origin(engine)
      const cohortId = `medical-recovery-${hospital ? 'hospital' : 'field'}`
      city.recruitedUnitCohorts = [{
        id: cohortId,
        unitId: 'unit-light',
        loadoutId: 'medical-test',
        count: 3,
        weaponEquipmentId: 'weapon-mace',
        weapon: { damageLevels: { impact: 2 }, tags: ['mace'] },
        armor: null,
      }]
      city.militaryPopulation = Math.max(city.militaryPopulation, 3)
      if (hospital) {
        engine.state.empire.researchedTechnologyIds.push('tech-medicine')
        city.buildingLevels[value.empire.medical.hospitalBuildingId] = 1
        city.operationalBuildingLevels[value.empire.medical.hospitalBuildingId] = 1
      }
      const deployment = {
        id: cohortId,
        cohortId,
        cityId: city.id,
        unitId: 'unit-light',
        count: 3,
        nodeId: 'node',
        speedPerSecond: 0,
        maxHpPerUnit: 10,
        attackRange: 10,
        attackIntervalTicks: 10,
        weapon: { damageLevels: { impact: 2 }, tags: ['mace'] },
        armor: null,
      }
      const session = {
        id: `session-${cohortId}`,
        attempt: 0,
        origin: { returnPhase: 'empire', context: { kind: 'manual', sourceId: 'phase5-test' } },
        plan: { id: `plan-${cohortId}`, deployments: [deployment], equipmentStock: {} },
      }
      const result = {
        outcome: 'victory',
        deployments: [{
          deploymentId: cohortId,
          cohortId,
          cityId: city.id,
          unitId: 'unit-light',
          deployed: 3,
          survived: 3,
          healthRatio: 0.75,
        }],
        equipmentSpent: {},
      }
      ;(engine as unknown as { settleBattleOutcome: (result: unknown, session: unknown) => void })
        .settleBattleOutcome(result, session)
      expect(engine.state.army.recoveries).toEqual([
        expect.objectContaining({
          cohortId,
          count: 3,
          readyAtCon: engine.state.con + (hospital ? 1 : 2),
        }),
      ])
      const deployments = (engine as unknown as {
        buildTdDeployments: (state: EmpiresCampaignState, nodeId: string, speed: number) => Array<{ cohortId: string }>
      }).buildTdDeployments(engine.state, 'node', 0)
      expect(deployments.map(item => item.cohortId)).not.toContain(cohortId)
    }
  })

  it('uses epidemic-reduced production before the famine eligibility roll', () => {
    const value = config()
    value.empire.epidemics.maxSpreadTargetsPerSettlement = 0
    value.empire.eventChance = 1
    value.empire.events = value.empire.events.filter(event => event.id === 'event-famine-rationing')
    const plague = value.empire.epidemics.definitions.find(item => item.id === 'epidemic-plague')!
    plague.stages = [{
      id: 'shutdown',
      name: 'Shutdown',
      severity: 1,
      durationCons: 1,
      populationLossPercent: 0,
      productionLossPercent: 100,
      loyaltyDelta: 0,
      spreadChance: 0,
      recruitmentBlocked: false,
      facilityLocks: [],
    }]
    const engine = empireEngine(value, 1)
    engine.state.empire.resources[value.empire.foodResourceId] = 0
    for (const city of engine.state.empire.cities) {
      city.resources[value.empire.foodResourceId] = 0
      city.buildingLevels = {}
      city.operationalBuildingLevels = {}
      city.baseProduction[value.empire.foodResourceId] = engine.cityFoodConsumption(city.id)
    }
    const city = origin(engine)
    expect(startPlague(engine, city.id).ok).toBe(true)
    expect(engine.finishEmpire().ok).toBe(true)
    expect(engine.state).toMatchObject({
      phase: 'event',
      event: { eventId: 'event-famine-rationing', empireSettlementPending: true },
    })
  })

  it('normalizes legacy v5 saves into schema v8 epidemic, economy, and external homes', () => {
    const value = config()
    const legacy = empireEngine(value).snapshot() as EmpiresCampaignState
    legacy.schemaVersion = 5 as never
    delete (legacy as Partial<EmpiresCampaignState>).epidemics
    delete (legacy as Partial<EmpiresCampaignState>).nextEpidemicSequence
    delete (legacy.empire as Partial<EmpiresCampaignState['empire']>).medical
    delete (legacy.army as Partial<EmpiresCampaignState['army']>).recoveries
    const restored = new EmpiresEndgameEngine(value, legacy)
    expect(restored.state).toMatchObject({
      schemaVersion: 11,
      epidemics: [],
      nextEpidemicSequence: 1,
      army: { recoveries: [] },
      empire: {
        medical: { nextFreeResearchCon: null, awardedTechnologyIds: [] },
        domesticEconomy: { loans: [], insuranceContracts: [], persecution: null },
      },
    })
  })
})
