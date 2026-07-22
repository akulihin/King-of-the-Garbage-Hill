import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(bundledConfigJson)
  value.empire.eventChance = 0
  return value
}

function richState(value: EmpiresEndgameConfig): EmpiresCampaignState {
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = 1_000
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  state.empire.researchedTechnologyIds = value.empire.technologies
    .filter(technology => !technology.deferredReason && !technology.steel)
    .map(technology => technology.id)
  for (const resource of value.empire.resources) {
    state.empire.resources[resource.id] = 1_000_000_000
    for (const city of state.empire.cities) city.resources[resource.id] = 1_000_000_000
  }
  return state
}

function install(
  value: EmpiresEndgameConfig,
  state: EmpiresCampaignState,
  cityId: string,
  buildingId: string,
  slotId?: string,
) {
  const city = state.empire.cities.find(candidate => candidate.id === cityId)!
  const cityDefinition = value.empire.cities.find(candidate => candidate.id === cityId)!
  const building = value.empire.buildings.find(candidate => candidate.id === buildingId)!
  const slot = slotId
    ? cityDefinition.slots.find(candidate => candidate.id === slotId)
    : cityDefinition.slots.find(candidate => (
        candidate.kind === building.slot && !city.buildingSlotAssignments[candidate.id]
      ))
  if (!slot) throw new Error(`No ${building.slot} slot for ${cityId}:${buildingId}.`)
  city.buildingLevels[buildingId] = 1
  city.operationalBuildingLevels[buildingId] = 1
  city.buildingSlotAssignments[slot.id] = buildingId
}

function eventState(value: EmpiresEndgameConfig, eventId: string): EmpiresCampaignState {
  const state = richState(value)
  state.phase = 'event'
  state.event = {
    instanceId: `accepted:${eventId}`,
    eventId,
    empireSettlementPending: false,
  }
  state.empire.daysRemaining = 0
  return state
}

describe('accepted campaign design-review content', () => {
  it('delivers delayed Academy units, the Foundry capital slot, and the Generals active exactly once', () => {
    const value = config()
    const state = richState(value)
    state.empire.researchedTechnologyIds = state.empire.researchedTechnologyIds.filter(id => (
      id !== 'tech-generals' && id !== 'tech-foundry'
    ))
    const capitalId = value.governance.capital.cityId
    install(value, state, capitalId, 'building-military-academy', 'slot-unique-academy')
    const engine = new EmpiresEndgameEngine(value, state)
    engine.state.army.morale = 0

    expect(engine.research('tech-generals')).toMatchObject({ ok: true })
    expect(engine.cityRecruitedUnitCount(capitalId, 'unit-light')).toBe(2)
    expect(Object.values(engine.state.army.unitInstances).filter(instance => (
      instance.cityId === capitalId && instance.unitId === 'unit-light'
    )).every(instance => instance.readyAtCon === engine.state.con + 1)).toBe(true)
    expect(engine.rallyGenerals()).toMatchObject({ ok: true })
    expect(engine.state.army.morale).toBe(1)
    expect(engine.rallyGenerals()).toMatchObject({ ok: false })

    engine.state.empire.researchUsage = {}
    engine.state.empire.daysRemaining = 1_000
    expect(engine.research('tech-foundry')).toMatchObject({ ok: true })
    expect(engine.cityRecruitedUnitCount(capitalId, 'unit-light')).toBe(5)
    expect(engine.placeBuilding(capitalId, 'slot-unique-foundry', 'building-foundry'))
      .toMatchObject({ ok: true })
    expect(engine.state.empire.cities.find(city => city.id === capitalId)
      ?.buildingSlotAssignments['slot-unique-foundry']).toBe('building-foundry')
  })

  it('runs Fair exchange, Sea Port expedition speed, and every Insurance Bank incident kind', () => {
    const value = config()
    const state = richState(value)
    const fairCityId = 'city-north-frost-harbor'
    install(value, state, fairCityId, 'building-fair')
    const engine = new EmpiresEndgameEngine(value, state)
    const goldBefore = engine.state.empire.resources.gold
    expect(engine.exchangeAtFair(fairCityId)).toMatchObject({ ok: true })
    expect(engine.state.empire.resources.gold).toBe(goldBefore + 500)
    expect(engine.exchangeAtFair(fairCityId)).toMatchObject({ ok: false })

    const expeditionId = value.expeditions.definitions.find(definition => !definition.deferredReason)!.id
    const speedBefore = engine.expeditionPlanningView(expeditionId)!.speedPercent
    install(value, engine.state, fairCityId, 'building-sea-port')
    expect(engine.expeditionPlanningView(expeditionId)!.speedPercent).toBe(speedBefore + 10)

    const bankCityId = 'city-west-horse-march'
    install(value, engine.state, bankCityId, 'building-jewish-bank')
    expect(engine.startInsurance(bankCityId)).toMatchObject({ ok: true })
    const contract = engine.state.empire.domesticEconomy.insuranceContracts.at(-1)!
    contract.status = 'active'
    contract.calmTurns = value.empire.domesticEconomy.insurance.calmTurnsRequired
    contract.activatedAtCon = engine.state.con
    contract.expiresAfterCon = engine.state.con + 5
    const insuredGold = engine.state.empire.resources.gold
    expect(engine.consumeDomesticIncident(bankCityId, 'nuclear', 'nuclear:test:1'))
      .toMatchObject({ ok: true })
    expect(engine.state.empire.resources.gold).toBeGreaterThan(insuredGold)
    const paidGold = engine.state.empire.resources.gold
    expect(engine.consumeDomesticIncident(bankCityId, 'nuclear', 'nuclear:test:1'))
      .toMatchObject({ ok: true })
    expect(engine.state.empire.resources.gold).toBe(paidGold)
    expect(value.empire.domesticEconomy.insurance.coveredIncidentKinds)
      .toEqual(['epidemic', 'meteor', 'raid', 'nuclear', 'siege'])
  })

  it('executes Printing, Technocracy, Lumber Concession, and White Stone defaults', () => {
    const value = config()
    expect(value.empire.buildings.find(building => building.id === 'building-lumber')?.levels
      .every(level => level.production?.some(production => (
        production.resourceId === 'carpentry' && production.amount > 0
      )))).toBe(true)
    const productionState = richState(value)
    productionState.empire.resources.carpentry = 0
    const production = new EmpiresEndgameEngine(value, productionState)
    expect(production.finishEmpire()).toMatchObject({ ok: true })
    expect(production.state.empire.resources.carpentry).toBeGreaterThan(0)

    const state = richState(value)
    state.empire.researchedTechnologyIds = state.empire.researchedTechnologyIds.filter(id => (
      id !== 'tech-printing' && id !== 'reform-technocracy'
    ))
    const engine = new EmpiresEndgameEngine(value, state)
    expect(engine.research('tech-printing')).toMatchObject({ ok: true })
    expect(engine.state.empire.productionMultipliers.knowledge).toBeGreaterThan(1)

    engine.state.empire.researchUsage = {}
    engine.state.empire.daysRemaining = 1_000
    const loyaltyBefore = engine.state.empire.cities.map(city => city.loyalty)
    const reputationBefore = engine.state.empire.reputation
    expect(engine.research('reform-technocracy')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities.map(city => city.loyalty))
      .toEqual(loyaltyBefore.map(loyalty => loyalty - 1))
    expect(engine.state.empire.reputation).toBe(reputationBefore - 1)

    const lumber = new EmpiresEndgameEngine(value, eventState(value, 'event-lumber-concession'))
    const lumberReputation = lumber.state.empire.reputation
    const woodBefore = lumber.state.empire.resources.wood
    expect(lumber.chooseEvent('alternate-forest')).toMatchObject({ ok: true })
    expect(lumber.state.empire.reputation).toBe(lumberReputation + 1)
    expect(lumber.state.empire.resources.wood).toBe(woodBefore - 150000)

    const whiteStone = new EmpiresEndgameEngine(value, eventState(value, 'event-white-stone'))
    const capital = whiteStone.state.empire.cities.find(city => city.id === value.governance.capital.cityId)!
    const populationBefore = capital.population
    const stoneBefore = whiteStone.state.empire.resources.whiteStone
    expect(whiteStone.chooseEvent('continue-mining')).toMatchObject({ ok: true })
    expect(whiteStone.state.empire.resources.whiteStone).toBe(stoneBefore + 250000)
    expect(capital.population).toBe(populationBefore - 1000)
  })

  it('activates every non-Chess capital site through the shared cooldown and cost substrate', () => {
    const value = config()
    expect(() => validateEmpiresConfig(value)).not.toThrow()
    for (const site of value.governance.capital.sites.filter(candidate => (
      candidate.id !== value.chess.entryCapitalSiteId
    ))) {
      const state = richState(value)
      if (site.buildingId) {
        const slotId = site.buildingId === 'building-military-academy'
          ? 'slot-unique-academy'
          : 'slot-municipal'
        install(value, state, value.governance.capital.cityId, site.buildingId, slotId)
      }
      const engine = new EmpiresEndgameEngine(value, state)
      const capital = engine.state.empire.cities.find(city => city.id === value.governance.capital.cityId)!
      const totalsBefore = Object.fromEntries(site.resourceCosts.map(cost => [
        cost.resourceId,
        (engine.state.empire.resources[cost.resourceId] ?? 0) + (capital.resources[cost.resourceId] ?? 0),
      ]))
      expect(engine.activateCapitalSite(site.id), site.id).toMatchObject({ ok: true })
      for (const cost of site.resourceCosts) {
        expect(
          (engine.state.empire.resources[cost.resourceId] ?? 0) + (capital.resources[cost.resourceId] ?? 0),
          `${site.id}:${cost.resourceId}`,
        ).toBe(totalsBefore[cost.resourceId] - cost.amount)
      }
      expect(engine.state.empire.flags[`capitalSiteLastUsed:${site.id}`]).toBe(engine.state.con)
      expect(engine.activateCapitalSite(site.id), site.id).toMatchObject({ ok: false })
    }
  })
})
