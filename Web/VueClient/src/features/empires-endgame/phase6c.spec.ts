import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { importEmpiresCampaign } from './persistence'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(bundledConfigJson)
  value.empire.eventChance = 0
  return value
}

function baseState(value: EmpiresEndgameConfig): EmpiresCampaignState {
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = value.empire.daysPerPhase
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  state.empire.researchedTechnologyIds = value.empire.technologies
    .filter(technology => !technology.deferredReason)
    .map(technology => technology.id)
  for (const resource of value.empire.resources) {
    state.empire.resources[resource.id] = 1_000_000_000
    for (const city of state.empire.cities) city.resources[resource.id] = 1_000_000_000
  }
  return state
}

function installBuilding(
  value: EmpiresEndgameConfig,
  state: EmpiresCampaignState,
  cityId: string,
  buildingId: string,
  level = 1,
) {
  const city = state.empire.cities.find(item => item.id === cityId)
  const definition = value.empire.buildings.find(item => item.id === buildingId)
  const cityDefinition = value.empire.cities.find(item => item.id === cityId)
  if (!city || !definition || !cityDefinition) throw new Error(`Missing carrier ${cityId}:${buildingId}.`)
  const slot = cityDefinition.slots.find(candidate => (
    candidate.kind === definition.slot && !city.buildingSlotAssignments[candidate.id]
  ))
  if (!slot) throw new Error(`Missing ${definition.slot} slot for ${cityId}:${buildingId}.`)
  city.buildingLevels[buildingId] = level
  city.operationalBuildingLevels[buildingId] = level
  city.buildingSlotAssignments[slot.id] = buildingId
}

function prepareEvent(
  value: EmpiresEndgameConfig,
  state: EmpiresCampaignState,
  eventId: string,
  targetCityId: string,
  targetActorId?: string,
) {
  state.phase = 'event'
  state.event = {
    instanceId: `economy-event-${state.empire.economyContent.nextEventSequence++}`,
    eventId,
    empireSettlementPending: false,
    targetCityId,
    ...(targetActorId ? { targetActorId } : {}),
  }
  state.empire.daysRemaining = 0
}

function settleCon(engine: EmpiresEndgameEngine) {
  engine.state.phase = 'empire'
  engine.state.event = null
  engine.state.minigame = null
  engine.state.outcomeReason = null
  engine.state.empire.daysRemaining = engine.config.empire.daysPerPhase
  engine.state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  engine.state.empire.resources[engine.config.empire.foodResourceId] = 1_000_000_000
  for (const city of engine.state.empire.cities) {
    city.resources[engine.config.empire.foodResourceId] = 1_000_000_000
  }
  const result = engine.finishEmpire()
  if (!result.ok) throw new Error(result.message)
}

function removeCardFromLocations(state: EmpiresCampaignState, cardId: string) {
  state.durak.deck = state.durak.deck.filter(id => id !== cardId)
  state.durak.playerHand = state.durak.playerHand.filter(id => id !== cardId)
  state.durak.godHand = state.durak.godHand.filter(id => id !== cardId)
  state.durak.discard = state.durak.discard.filter(id => id !== cardId)
}

function startEmpirePhaseForTest(engine: EmpiresEndgameEngine) {
  ;(engine as unknown as { startEmpirePhase(): void }).startEmpirePhase()
}

describe('Empire\'s Endgame Phase 6C economy content closure', () => {
  it('publishes the exact live and deferred candidate manifest', () => {
    const value = config()
    const gift = (id: string) => value.gifts.definitions.find(item => item.id === id)!
    const event = (id: string) => value.empire.events.find(item => item.id === id)!
    const resource = (id: string) => value.empire.resources.find(item => item.id === id)!
    const card = (id: string) => value.cards.find(item => item.id === id)!

    for (const id of ['relic-tithe', 'relic-resource-exemption']) {
      expect(gift(id).deferredReason, id).toBeUndefined()
    }
    for (const id of [
      'gift-earthquake', 'gift-tailwind', 'gift-fish-currents',
      'gift-meteor-iron', 'gift-desert-tsunami',
    ]) expect(gift(id).deferredReason, id).toBeTruthy()
    for (const id of ['event-customs-smuggling', 'event-horse-theft', 'event-bank-insurance']) {
      expect(event(id).deferredReason, id).toBeUndefined()
    }
    for (const id of ['event-lumber-concession', 'event-white-stone']) {
      expect(event(id).deferredReason, id).toBeTruthy()
    }
    for (const id of ['whiteStone', 'carpentry']) expect(resource(id).deferredReason, id).toBeTruthy()
    expect(card('card-diamonds-6').normal.deferredReason).toBeTruthy()
    expect(card('card-diamonds-ace').inverted.deferredReason).toBeUndefined()
  })

  it('applies tithe rounding and both material exemptions only through an operational Temple slot', () => {
    const value = config()
    const state = baseState(value)
    const templeCityId = 'city-north-frost-harbor'
    installBuilding(value, state, templeCityId, value.empire.domesticEconomy.temple.buildingId)
    state.empire.claimedGiftIds.push('relic-tithe', 'relic-resource-exemption')
    const engine = new EmpiresEndgameEngine(value, state)
    const baseTithe = engine.domesticEconomyView(templeCityId).temple.projectedTitheGold
    expect(engine.assignTempleRelic(templeCityId, 0, 'relic-tithe')).toMatchObject({ ok: true })
    expect(engine.domesticEconomyView(templeCityId).temple.projectedTitheGold)
      .toBe(Math.floor(baseTithe * 1.5))
    expect(engine.assignTempleRelic(templeCityId, 1, 'relic-resource-exemption'))
      .toMatchObject({ ok: true })
    expect(engine.effectiveEmpireFlagValue('smithyWithoutIron')).toBe(1)
    expect(engine.effectiveEmpireFlagValue('stableWithoutLivestock')).toBe(1)

    const smithCityId = 'city-north-iron-gate'
    const smithCity = engine.state.empire.cities.find(city => city.id === smithCityId)!
    smithCity.buildingLevels['building-mine'] = 2
    smithCity.operationalBuildingLevels['building-mine'] = 2
    smithCity.buildingLevels['building-smithy'] = 1
    smithCity.operationalBuildingLevels['building-smithy'] = 1
    engine.state.empire.resources.iron = 0
    engine.state.empire.cities.find(city => city.id === smithCityId)!.resources.iron = 0
    expect(engine.upgradeBuilding(smithCityId, 'building-smithy')).toMatchObject({ ok: true })

    const stableCityId = 'city-west-horse-march'
    const stableCity = engine.state.empire.cities.find(city => city.id === stableCityId)!
    stableCity.buildingLevels['building-farm'] = 2
    stableCity.operationalBuildingLevels['building-farm'] = 2
    engine.state.empire.resources.horses = 0
    engine.state.empire.cities.find(city => city.id === stableCityId)!.resources.horses = 0
    expect(engine.placeBuilding(stableCityId, 'slot-unique', 'building-stable')).toMatchObject({ ok: true })

    engine.state.empire.cities.find(city => city.id === templeCityId)!
      .operationalBuildingLevels[value.empire.domesticEconomy.temple.buildingId] = 0
    expect(engine.effectiveEmpireFlagValue('smithyWithoutIron')).toBe(0)
    expect(engine.effectiveEmpireFlagValue('stableWithoutLivestock')).toBe(0)
  })

  it('runs both Customs choices once, targets the traded city, settles next-con population, and expires', () => {
    const value = config()
    const rules = value.empire.economyContent.smuggling
    const customsProductionDeltaByMultiplier = new Map<number, number>()
    for (const [choiceId, populationDelta, multiplier] of [
      [rules.stopChoiceId, rules.stopPopulationGrowth, rules.stopCustomsIncomeMultiplier],
      [rules.taxChoiceId, rules.taxPopulationGrowth, rules.taxCustomsIncomeMultiplier],
    ] as const) {
      const state = baseState(value)
      const cityId = 'city-north-frost-harbor'
      installBuilding(value, state, cityId, value.empire.externalEconomy.customs.buildingId)
      state.external.customs.completedTrades = 1
      state.external.customs.smugglingEligible = true
      state.external.customs.lastTradeCon = state.con
      state.external.customs.lastTradeCityId = cityId
      prepareEvent(value, state, rules.eventId, cityId)
      const populationBefore = state.empire.cities.find(city => city.id === cityId)!.population
      const engine = new EmpiresEndgameEngine(value, state)

      expect(engine.chooseEvent(choiceId)).toMatchObject({ ok: true })
      expect(engine.state.empire.economyContent).toMatchObject({
        resolvedOnceEventIds: [rules.eventId],
        smugglingPolicy: {
          choiceId,
          cityId,
          startedAtCon: engine.state.con,
          expiresAfterCon: engine.state.con + rules.durationCons - 1,
        },
      })
      expect(engine.state.empire.economyContent.eventHistory).toEqual([
        expect.objectContaining({ eventId: rules.eventId, choiceId, targetCityId: cityId }),
      ])
      expect(new EmpiresEndgameEngine(value, engine.snapshot()).state.empire.economyContent.eventHistory)
        .toHaveLength(1)

      engine.state.phase = 'empire'
      const policy = engine.state.empire.economyContent.smugglingPolicy!
      engine.state.empire.economyContent.smugglingPolicy = null
      ;(engine as unknown as { refreshProductions(): void }).refreshProductions()
      const baseGoldProduction = engine.state.empire.cities
        .find(city => city.id === cityId)!.lastProduction.gold
      engine.state.empire.economyContent.smugglingPolicy = policy
      ;(engine as unknown as { refreshProductions(): void }).refreshProductions()
      customsProductionDeltaByMultiplier.set(multiplier, engine.state.empire.cities
        .find(city => city.id === cityId)!.lastProduction.gold - baseGoldProduction)
      settleCon(engine)
      expect(engine.state.empire.cities.find(city => city.id === cityId)!.population)
        .toBe(populationBefore + populationDelta)
      expect(engine.state.empire.economyContent.smugglingPolicy).toBeNull()
    }
    expect(customsProductionDeltaByMultiplier.get(rules.stopCustomsIncomeMultiplier)).toBeLessThan(0)
    expect(customsProductionDeltaByMultiplier.get(rules.taxCustomsIncomeMultiplier))
      .toBeCloseTo(-customsProductionDeltaByMultiplier.get(rules.stopCustomsIncomeMultiplier)!, 8)
  })

  it('runs horse-theft hunt, repeat, and hostile-target pact branches with deterministic restore', () => {
    const value = config()
    const rules = value.empire.economyContent.horseTheft
    const make = () => {
      const state = baseState(value)
      const cityId = 'city-west-horse-march'
      installBuilding(value, state, cityId, rules.stableBuildingId)
      state.empire.domesticEconomy.fair.baronUnlockedAtCon = state.con
      for (const relationship of Object.values(state.external.relationships)) relationship.status = 'neutral'
      state.external.relationships['actor-louis'].status = 'hostile'
      prepareEvent(value, state, rules.eventId, cityId, 'actor-louis')
      return { state, cityId }
    }

    const hunt = make()
    const huntEngine = new EmpiresEndgameEngine(value, hunt.state)
    expect(huntEngine.chooseEvent(rules.huntChoiceId)).toMatchObject({ ok: true })
    expect(huntEngine.state.empire.economyContent.horseTheft).toMatchObject({
      disabledAtCon: hunt.state.con,
      pact: null,
    })
    expect(new EmpiresEndgameEngine(value, huntEngine.snapshot())
      .state.empire.economyContent.horseTheft.disabledAtCon).toBe(hunt.state.con)

    const ignored = make()
    const ignoreEngine = new EmpiresEndgameEngine(value, ignored.state)
    const theftCon = ignoreEngine.state.con
    expect(ignoreEngine.chooseEvent(rules.ignoreChoiceId)).toMatchObject({ ok: true })
    expect(ignoreEngine.state.empire.economyContent.horseTheft.nextEligibleCon)
      .toBe(theftCon + rules.recurrenceCooldownCons)
    const invalidRepeat = ignoreEngine.snapshot()
    prepareEvent(value, invalidRepeat, rules.eventId, ignored.cityId, 'actor-louis')
    const advanced = new EmpiresEndgameEngine(value, invalidRepeat)
    expect(advanced.state.phase).not.toBe('event')

    const deal = make()
    const dealEngine = new EmpiresEndgameEngine(value, deal.state)
    const horsesBefore = dealEngine.state.empire.resources[rules.livestockResourceId]
    const noblesBefore = dealEngine.state.empire.cities.map(city => (
      dealEngine.state.empire.loyalty.classModifiers[city.id]?.nobles ?? 0
    ))
    expect(dealEngine.chooseEvent(rules.dealChoiceId)).toMatchObject({ ok: true })
    expect(dealEngine.state.empire.economyContent.horseTheft.pact).toMatchObject({
      actorId: 'actor-louis',
      cityId: deal.cityId,
      startedAtCon: dealEngine.state.con,
    })
    expect(dealEngine.state.empire.cities.map(city => (
      dealEngine.state.empire.loyalty.classModifiers[city.id]?.nobles ?? 0
    )))
      .toEqual(noblesBefore.map(value => value - 1))
    settleCon(dealEngine)
    expect(dealEngine.state.empire.resources[rules.livestockResourceId])
      .toBe(horsesBefore + 100 + rules.enemyYieldPerCon)
    const invalidPact = dealEngine.snapshot()
    invalidPact.external.relationships['actor-louis'].status = 'neutral'
    expect(new EmpiresEndgameEngine(value, invalidPact)
      .state.empire.economyContent.horseTheft.pact).toBeNull()
  })

  it('routes the insurance event into the P6A calm-turn contract and makes decline non-recurring', () => {
    const value = config()
    const rules = value.empire.economyContent.insurance
    const make = () => {
      const state = baseState(value)
      const cityId = 'city-north-frost-harbor'
      installBuilding(value, state, cityId, rules.buildingId)
      prepareEvent(value, state, rules.eventId, cityId)
      return { state, cityId }
    }

    const accepted = make()
    const acceptEngine = new EmpiresEndgameEngine(value, accepted.state)
    expect(acceptEngine.chooseEvent(rules.acceptChoiceId)).toMatchObject({ ok: true })
    expect(acceptEngine.state.empire.domesticEconomy.insuranceContracts[0]).toMatchObject({
      cityId: accepted.cityId,
      status: 'waiting',
      calmTurns: 0,
    })
    for (let turn = 0; turn < value.empire.domesticEconomy.insurance.calmTurnsRequired; turn += 1) {
      settleCon(acceptEngine)
    }
    expect(acceptEngine.state.empire.domesticEconomy.insuranceContracts[0]).toMatchObject({
      status: 'active',
      calmTurns: value.empire.domesticEconomy.insurance.calmTurnsRequired,
    })

    const declined = make()
    const declineEngine = new EmpiresEndgameEngine(value, declined.state)
    expect(declineEngine.chooseEvent(rules.declineChoiceId)).toMatchObject({ ok: true })
    expect(declineEngine.state.empire.economyContent.insuranceOfferedCityIds)
      .toEqual([declined.cityId])
    const invalidRepeat = declineEngine.snapshot()
    prepareEvent(value, invalidRepeat, rules.eventId, declined.cityId)
    expect(new EmpiresEndgameEngine(value, invalidRepeat).state.phase).not.toBe('event')
  })

  it('scales inverted diamond Ace gates, permits same-region trade, and cleans up next con', () => {
    const value = config()
    const state = baseState(value)
    const cardId = 'card-diamonds-ace'
    removeCardFromLocations(state, cardId)
    state.durak.playerHand.push(cardId)
    state.cards[cardId].inverted = true
    state.cards[cardId].level = 2
    state.durak.trumpSuit = 'clubs'
    state.phase = 'cards'
    state.empire.researchedTechnologyIds.push(
      value.empire.externalEconomy.tradeRoutesTechnologyId,
      value.empire.externalEconomy.transfer.compassTechnologyId,
    )
    const from = state.empire.cities.find(city => city.id === 'city-north-frost-harbor')!
    from.resources.wood = 1_000
    const engine = new EmpiresEndgameEngine(value, state)
    startEmpirePhaseForTest(engine)

    expect(engine.effectiveEmpireFlagValue('externalTradeDisabled')).toBe(3)
    expect(engine.effectiveEmpireFlagValue('internalTradeOnly')).toBe(3)
    expect(engine.externalDiplomacyView(from.id).offers.every(offer => (
      offer.quote.blockedReason?.includes('Изоляция валюты')
    ))).toBe(true)
    expect(engine.transferCityResource(from.id, 'city-west-horse-march', 'wood', 50).message)
      .toContain('only inside one region')
    expect(engine.transferCityResource(from.id, 'city-north-iron-gate', 'wood', 50))
      .toMatchObject({ ok: true })

    settleCon(engine)
    expect(engine.effectiveEmpireFlagValue('externalTradeDisabled')).toBe(0)
    expect(engine.effectiveEmpireFlagValue('internalTradeOnly')).toBe(0)
    engine.state.phase = 'empire'
    expect(engine.transferCityResource(from.id, 'city-west-horse-march', 'wood', 50))
      .toMatchObject({ ok: true })
  })

  it('migrates config v10 and save v8 clone-first, validates references, and bounds history', () => {
    const current = config()
    const legacyConfig = structuredClone(current) as unknown as Record<string, unknown>
    legacyConfig.schemaVersion = 10
    delete (legacyConfig.empire as Record<string, unknown>).economyContent
    const untouched = structuredClone(legacyConfig)
    const migrated = migrateEmpiresConfig(legacyConfig)
    expect(legacyConfig).toEqual(untouched)
    expect(migrated).toMatchObject({ schemaVersion: 11, empire: { economyContent: { enabled: false } } })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)

    const badChoice = config()
    badChoice.empire.economyContent.smuggling.taxChoiceId = 'missing-choice'
    expect(() => validateEmpiresConfig(badChoice)).toThrow(/smuggling\.taxChoiceId.*live event choice/i)
    const badFlag = config()
    badFlag.empire.economyContent.tradeCard.internalTradeOnlyFlagId = 'missing-flag'
    expect(() => validateEmpiresConfig(badFlag)).toThrow(/tradeCard.*both live inverted/i)

    const legacyState = new EmpiresEndgameEngine(current).snapshot()
    legacyState.schemaVersion = 8 as never
    delete (legacyState.empire as Partial<EmpiresCampaignState['empire']>).economyContent
    const imported = importEmpiresCampaign({
      schemaVersion: 8,
      savedAt: '2026-07-17T00:00:00.000Z',
      state: legacyState,
    }, current.id)
    expect(imported.schemaVersion).toBe(9)
    expect(new EmpiresEndgameEngine(current, imported).state.empire.economyContent).toMatchObject({
      eventHistory: [],
      resolvedOnceEventIds: [],
      smugglingPolicy: null,
      horseTheft: { disabledAtCon: null, pact: null },
    })

    const bounded = baseState(current)
    const event = current.empire.events.find(item => item.id === current.empire.economyContent.smuggling.eventId)!
    bounded.empire.economyContent.eventHistory = Array.from(
      { length: current.empire.economyContent.eventHistoryRetention + 3 },
      (_, index) => ({
        id: `economy-event-${index + 1}`,
        eventId: event.id,
        choiceId: event.choices[0].id,
        resolvedAtCon: bounded.con,
        targetCityId: null,
        targetActorId: null,
      }),
    )
    const restored = new EmpiresEndgameEngine(current, bounded)
    expect(restored.state.empire.economyContent.eventHistory)
      .toHaveLength(current.empire.economyContent.eventHistoryRetention)
    expect(restored.state.empire.economyContent.compactedEventHistoryCount).toBe(3)
  })
})
