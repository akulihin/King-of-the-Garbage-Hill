import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { importEmpiresCampaign } from './persistence'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(defaultConfigJson)
  value.empire.eventChance = 0
  return value
}

function addCarrier(
  value: EmpiresEndgameConfig,
  state: EmpiresCampaignState,
  cityId: string,
  buildingId: string,
) {
  const city = state.empire.cities.find(item => item.id === cityId)!
  const slot = value.empire.cities.find(item => item.id === cityId)?.slots.find(item => item.kind === 'unique')
  if (!slot) throw new Error(`No unique slot for ${cityId}.`)
  city.buildingLevels[buildingId] = 1
  city.operationalBuildingLevels[buildingId] = 1
  city.buildingSlotAssignments[slot.id] = buildingId
}

function economyEngine(mutateConfig?: (value: EmpiresEndgameConfig) => void) {
  const value = config()
  mutateConfig?.(value)
  const initial = new EmpiresEndgameEngine(value)
  const state = initial.snapshot()
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = value.empire.daysPerPhase
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  const rules = value.empire.domesticEconomy
  const carrierIds = [
    rules.loan.bankBuildingId,
    rules.insurance.buildingId,
    rules.fair.buildingId,
    rules.temple.buildingId,
    rules.tavern.buildingId,
  ]
  const accessible = state.empire.cities.filter(city => initial.isCityAccessible(city.id))
  if (accessible.length < carrierIds.length) throw new Error('Phase 6 tests require five accessible cities.')
  carrierIds.forEach((buildingId, index) => addCarrier(value, state, accessible[index].id, buildingId))
  for (const city of accessible.slice(0, carrierIds.length)) {
    city.baseProduction[rules.goldResourceId] = 100
    city.lastProduction[rules.goldResourceId] = 100
  }
  state.empire.researchedTechnologyIds = [...new Set([
    ...state.empire.researchedTechnologyIds,
    rules.loan.bankingTechnologyId,
    rules.fair.technologyId,
    ...carrierIds.flatMap(buildingId => value.empire.buildings
      .find(building => building.id === buildingId)?.levels
      .flatMap(level => level.dependencies ?? [])
      .flatMap(dependency => dependency.kind === 'technology' ? [dependency.technologyId] : []) ?? []),
  ])]
  for (const resource of value.empire.resources) state.empire.resources[resource.id] = 1_000_000_000
  state.empire.resources[rules.knowledgeResourceId] = 10_000
  state.empire.resources[value.empire.foodResourceId] = 1_000_000_000
  for (const city of state.empire.cities) city.resources[value.empire.foodResourceId] = 1_000_000_000
  const relicId = value.gifts.definitions.find(gift => gift.kind === 'relic' && !gift.deferredReason)!.id
  state.empire.claimedGiftIds.push(relicId)
  return {
    value,
    engine: new EmpiresEndgameEngine(value, state),
    cityIds: carrierIds.map((_, index) => accessible[index].id),
    relicId,
  }
}

function settle(engine: EmpiresEndgameEngine) {
  engine.state.phase = 'empire'
  engine.state.event = null
  engine.state.minigame = null
  engine.state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  engine.state.empire.daysRemaining = engine.config.empire.daysPerPhase
  engine.state.empire.resources[engine.config.empire.foodResourceId] = 1_000_000_000
  for (const city of engine.state.empire.cities) {
    city.resources[engine.config.empire.foodResourceId] = 1_000_000_000
  }
  const result = engine.finishEmpire()
  if (!result.ok) throw new Error(result.message)
}

describe('Empire\'s Endgame Phase 6A domestic economy', () => {
  it('quotes, stacks by config, schedules, repays, and bounds Bank obligations', () => {
    const { value, engine, cityIds } = economyEngine()
    const [bankCity] = cityIds
    const quote = engine.loanQuote(bankCity)
    expect(quote).toMatchObject({ blockedReason: null, termCons: 7 })
    expect(quote.principal).toBe(quote.incomeAtOrigination * value.empire.domesticEconomy.loan.principalIncomeTurns)
    expect(quote.installmentAmount).toBe(
      quote.incomeAtOrigination * value.empire.domesticEconomy.loan.paymentIncomeFraction,
    )
    expect(quote.totalRepayment).toBe(quote.installmentAmount * quote.termCons)
    expect(engine.takeLoan(bankCity)).toMatchObject({ ok: true })
    const loan = engine.state.empire.domesticEconomy.loans[0]
    expect(loan.installments.map(item => item.dueCon)).toEqual([2, 3, 4, 5, 6, 7, 8])
    expect(engine.takeLoan(bankCity)).toMatchObject({ ok: false })
    expect(engine.repayLoan(loan.id)).toMatchObject({ ok: true })
    expect(loan.status).toBe('repaid')
    expect(loan.installments.every(item => item.status === 'paid')).toBe(true)

    const historyLimit = value.empire.domesticEconomy.historyRetention
    for (let index = 0; index < historyLimit + 2; index += 1) {
      expect(engine.takeLoan(bankCity).ok).toBe(true)
      const open = engine.state.empire.domesticEconomy.loans.find(item => item.status === 'active')!
      expect(engine.repayLoan(open.id).ok).toBe(true)
    }
    expect(engine.state.empire.domesticEconomy.loans).toHaveLength(historyLimit)
    expect(engine.state.empire.domesticEconomy.compactedLoanCount).toBe(3)

    const stacked = economyEngine(custom => {
      custom.empire.domesticEconomy.loan.maxActiveLoans = 2
    })
    expect(stacked.engine.takeLoan(stacked.cityIds[0]).ok).toBe(true)
    expect(stacked.engine.takeLoan(stacked.cityIds[0]).ok).toBe(true)
    expect(stacked.engine.takeLoan(stacked.cityIds[0]).ok).toBe(false)
  })

  it('settles the due boundary once, defaults once on insufficient funds, and makes гонения permanent', () => {
    const { value, engine, cityIds } = economyEngine((custom) => {
      for (const building of custom.empire.buildings) {
        for (const level of building.levels) {
          for (const production of level.production ?? []) {
            if (production.resourceId === custom.empire.domesticEconomy.goldResourceId) production.amount = 0
          }
        }
      }
    })
    const [bankCity] = cityIds
    expect(engine.takeLoan(bankCity).ok).toBe(true)
    const loan = engine.state.empire.domesticEconomy.loans[0]
    for (const city of engine.state.empire.cities) {
      city.baseProduction[value.empire.domesticEconomy.goldResourceId] = 0
      city.lastProduction[value.empire.domesticEconomy.goldResourceId] = 0
    }
    engine.state.empire.resources[value.empire.domesticEconomy.goldResourceId] = 0

    settle(engine)
    expect(loan.installments[0].status).toBe('pending')
    const reputationBefore = engine.state.empire.reputation
    const loyaltyBefore = engine.state.empire.cities.find(city => city.id === bankCity)!.loyalty
    settle(engine)
    expect(loan).toMatchObject({ status: 'defaulted', defaultedAtCon: 2 })
    expect(engine.state.empire.reputation).toBe(
      reputationBefore + value.empire.domesticEconomy.loan.defaultReputationDelta,
    )
    expect(engine.state.empire.cities.find(city => city.id === bankCity)!.loyalty).toBe(
      loyaltyBefore + value.empire.domesticEconomy.loan.defaultLoyaltyDelta,
    )
    const defaultChronicleCount = engine.state.empire.chronicle
      .filter(entry => entry.sourceId === `default:${loan.id}`).length
    settle(engine)
    expect(engine.state.empire.chronicle.filter(entry => entry.sourceId === `default:${loan.id}`)).toHaveLength(defaultChronicleCount)

    engine.state.phase = 'empire'
    const knowledgeBefore = engine.state.empire.resources[value.empire.domesticEconomy.knowledgeResourceId]
    expect(engine.beginPersecution(bankCity)).toMatchObject({ ok: true })
    expect(loan.status).toBe('persecuted')
    expect(loan.installments.every(item => item.status !== 'pending')).toBe(true)
    expect(engine.state.empire.resources[value.empire.domesticEconomy.knowledgeResourceId]).toBe(
      knowledgeBefore * (1 - value.empire.domesticEconomy.loan.persecutionKnowledgeLossPercent / 100),
    )
    expect(engine.loanQuote(bankCity).blockedReason).toMatch(/permanently unavailable/i)
    expect(new EmpiresEndgameEngine(value, engine.snapshot()).loanQuote(bankCity).blockedReason)
      .toMatch(/permanently unavailable/i)
  })

  it('activates insurance after three calm settlements, pays a covered siege once, and expires', () => {
    const { value, engine, cityIds } = economyEngine()
    const insuranceCity = cityIds[1]
    expect(engine.startInsurance(insuranceCity)).toMatchObject({ ok: true })
    const contract = engine.state.empire.domesticEconomy.insuranceContracts[0]
    settle(engine)
    expect(contract).toMatchObject({ status: 'waiting', calmTurns: 1 })
    settle(engine)
    expect(contract).toMatchObject({ status: 'waiting', calmTurns: 2 })
    settle(engine)
    expect(contract).toMatchObject({ status: 'active', calmTurns: 3, activatedAtCon: 3 })
    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    const restoredContract = restored.state.empire.domesticEconomy.insuranceContracts[0]
    const goldBefore = restored.state.empire.resources[value.empire.domesticEconomy.goldResourceId]
    expect(restored.consumeDomesticIncident(insuranceCity, 'siege', 'siege:qa')).toMatchObject({ ok: true })
    const expectedPayout = Math.min(
      value.empire.domesticEconomy.insurance.maximumPayoutGold,
      value.empire.domesticEconomy.insurance.basePayoutGold
        + restoredContract.calmTurns * value.empire.domesticEconomy.insurance.payoutPerCalmTurnGold,
    )
    expect(restoredContract).toMatchObject({
      status: 'consumed',
      payoutGold: expectedPayout,
      payoutIncidentId: 'siege:qa',
    })
    expect(restored.state.empire.resources[value.empire.domesticEconomy.goldResourceId]).toBe(goldBefore + expectedPayout)
    expect(restored.consumeDomesticIncident(insuranceCity, 'siege', 'siege:qa')).toMatchObject({ ok: true })
    expect(restored.state.empire.resources[value.empire.domesticEconomy.goldResourceId]).toBe(goldBefore + expectedPayout)

    const expiry = economyEngine()
    expect(expiry.engine.startInsurance(expiry.cityIds[1]).ok).toBe(true)
    for (let con = 1; con <= 10; con += 1) settle(expiry.engine)
    expect(expiry.engine.state.empire.domesticEconomy.insuranceContracts[0]).toMatchObject({
      status: 'expired',
      payoutGold: 0,
    })
  })

  it('runs Fair progression in authored order with reload-safe cadence, consequences, cooldown, expiry, and барон hook', () => {
    const { value, engine, cityIds } = economyEngine()
    const fairCity = cityIds[2]
    const fair = value.empire.domesticEconomy.fair
    expect(engine.performFairAction(fairCity, 'traveling-artists')).toMatchObject({ ok: false })
    expect(engine.performFairAction(fairCity, 'carnival')).toMatchObject({ ok: true })
    expect(engine.effectiveCityLoyalty(fairCity)).toBe(1)
    expect(engine.performFairAction(fairCity, 'traveling-artists')).toMatchObject({ ok: true })
    expect(engine.effectiveReputation()).toBe(1)
    const populationBefore = engine.state.empire.cities.reduce((sum, city) => sum + city.population, 0)
    const horsesBefore = engine.state.empire.resources.horses
    expect(engine.performFairAction(fairCity, fair.baronUnlockActionId)).toMatchObject({ ok: true })
    expect(engine.effectiveReputation()).toBe(2)
    expect(engine.domesticEconomyView(fairCity).fair.actions.find(action => action.id === 'carnival')?.blockedReason)
      .toMatch(/cooldown.*5/i)

    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.effectiveReputation()).toBe(2)
    for (let con = 1; con <= 4; con += 1) settle(restored)
    const tabour = fair.actions.find(action => action.id === fair.baronUnlockActionId)!
    expect(restored.state.empire.cities.reduce((sum, city) => sum + city.population, 0)).toBe(
      populationBefore - tabour.perConPopulationLoss * tabour.durationCons,
    )
    const horseLoss = tabour.perConResourceLosses.find(loss => loss.resourceId === 'horses')!.amount
    expect(restored.state.empire.resources.horses).toBe(horsesBefore - horseLoss * tabour.durationCons)
    expect(restored.state.empire.domesticEconomy.fair.activeActivities).toEqual([])
    expect(restored.state.empire.domesticEconomy.fair.baronUnlockedAtCon).toBe(4)
    restored.state.phase = 'empire'
    expect(restored.domesticEconomyView(fairCity).fair.actions.find(action => action.id === 'carnival')?.blockedReason)
      .toBeNull()
  })

  it('executes Temple preaching and makes relic effects depend on an operational slot', () => {
    const { value, engine, cityIds, relicId } = economyEngine()
    const templeCity = cityIds[3]
    const beforeGold = engine.state.empire.resources[value.empire.domesticEconomy.goldResourceId]
    const beforeLoyalty = engine.state.empire.cities.find(city => city.id === templeCity)!.loyalty
    const beforeReputation = engine.state.empire.reputation
    const view = engine.domesticEconomyView(templeCity)
    expect(view.temple.slots).toHaveLength(value.empire.domesticEconomy.temple.relicSlotsPerLevel)
    expect(engine.preachAtTemple(templeCity)).toMatchObject({ ok: true })
    expect(engine.state.empire.resources[value.empire.domesticEconomy.goldResourceId]).toBe(
      beforeGold + view.temple.projectedTitheGold,
    )
    expect(engine.state.empire.cities.find(city => city.id === templeCity)!.loyalty).toBe(
      beforeLoyalty + value.empire.domesticEconomy.temple.preachingLoyaltyDelta,
    )
    expect(engine.state.empire.reputation).toBe(
      beforeReputation + value.empire.domesticEconomy.temple.preachingReputationDelta,
    )
    expect(engine.preachAtTemple(templeCity)).toMatchObject({ ok: false })
    expect(engine.assignTempleRelic(templeCity, 0, relicId)).toMatchObject({ ok: true })
    expect(engine.effectiveEmpireFlagValue('epidemicProtectionPercent')).toBe(25)
    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.effectiveEmpireFlagValue('epidemicProtectionPercent')).toBe(25)
    expect(restored.clearTempleRelic(templeCity, 0)).toMatchObject({ ok: true })
    expect(restored.effectiveEmpireFlagValue('epidemicProtectionPercent')).toBe(0)
  })

  it('feeds Tavern levels into recruitment and morale with all accepted guest mechanics live', () => {
    const { value, engine, cityIds } = economyEngine()
    const tavernCity = cityIds[4]
    const rules = value.empire.domesticEconomy.tavern
    const view = engine.domesticEconomyView(tavernCity).tavern
    expect(view).toMatchObject({
      available: false,
      blockedReason: expect.any(String),
      recruitmentCapacityBonus: rules.recruitmentCapacityPerLevel,
      moraleMaximumBonus: rules.moraleMaximumPerLevel,
    })
    expect(view.deferredCapabilities).toEqual([])
    expect(engine.cityRecruitmentRemaining(tavernCity)).toBe(1000 + rules.recruitmentCapacityPerLevel)
    expect(engine.state.army.maxMorale).toBe(
      (value.empire.initialFlags?.maxCombatSpirit ?? 0) + rules.moraleMaximumPerLevel,
    )
  })

  it('migrates config v8 and save v6 clone-first, idempotently, and without reactivating legacy relic flags', () => {
    const legacyConfig = structuredClone(defaultConfigJson) as unknown as Record<string, unknown>
    legacyConfig.schemaVersion = 8
    delete (legacyConfig.empire as Record<string, unknown>).domesticEconomy
    delete (legacyConfig.empire as Record<string, unknown>).externalEconomy
    delete (legacyConfig.empire as Record<string, unknown>).economyContent
    const original = structuredClone(legacyConfig)
    const migrated = migrateEmpiresConfig(legacyConfig)
    expect(legacyConfig).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 19,
      empire: {
        domesticEconomy: { enabled: false },
        externalEconomy: { enabled: false },
        economyContent: { enabled: false },
      },
    })
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 20 })).toThrow(/future.*20/i)

    const value = config()
    const legacyState = new EmpiresEndgameEngine(value).snapshot() as EmpiresCampaignState
    legacyState.schemaVersion = 6 as never
    delete (legacyState.empire as Partial<EmpiresCampaignState['empire']>).domesticEconomy
    legacyState.empire.claimedGiftIds.push('relic-epidemic-ward')
    legacyState.empire.flags.epidemicProtectionPercent = 25
    const imported = importEmpiresCampaign({
      schemaVersion: 6,
      savedAt: '2026-07-17T00:00:00.000Z',
      state: legacyState,
    }, value.id)
    const restored = new EmpiresEndgameEngine(value, imported)
    expect(restored.state.schemaVersion).toBe(18)
    expect(restored.state.empire.domesticEconomy).toMatchObject({
      loans: [],
      insuranceContracts: [],
      persecution: null,
      temple: { activatedRelicIds: ['relic-epidemic-ward'], relicAssignments: {} },
    })
    expect(restored.effectiveEmpireFlagValue('epidemicProtectionPercent')).toBe(0)
  })
})
