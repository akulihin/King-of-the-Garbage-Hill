import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { createEmpiresQaScenarios, executeEmpiresQaExternalOfferPolicy } from './qa'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

const config = bundledConfigJson as unknown as EmpiresEndgameConfig

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function externalState(): EmpiresCampaignState {
  return createEmpiresQaScenarios(config, { seed: 'phase-6b' })['external-trade'].snapshot
}

describe('Empire endgame Phase 6B external diplomacy', () => {
  it('refreshes weighted offers deterministically and does not reroll on reload', () => {
    const state = externalState()
    const before = clone(state.external.activeOffers)
    const draws = state.rng.draws
    const restored = new EmpiresEndgameEngine(config, state)
    expect(restored.state.external.activeOffers).toEqual(before)
    expect(restored.state.rng.draws).toBe(draws)
    expect(restored.refreshExternalOffers()).toEqual({
      ok: true,
      message: 'External offers are already current.',
    })
    expect(restored.state.external.activeOffers).toEqual(before)
  })

  it('validates external Builder identities and every live carrier flag', () => {
    const unknownDependency = clone(config)
    unknownDependency.empire.externalEconomy.offers[0].prerequisites.push({
      kind: 'building', buildingId: 'building-missing', level: 1, scope: 'sameCity',
    })
    expect(() => validateEmpiresConfig(unknownDependency)).toThrow(/invalid building prerequisite/)

    const unwiredCarrier = clone(config)
    unwiredCarrier.empire.externalEconomy.customs.tariffFlagId = 'missingCustomsFlag'
    expect(() => validateEmpiresConfig(unwiredCarrier)).toThrow(/carrier effects and prerequisites/)
  })

  it('accepts and declines each stable offer once with real Customs and Port arithmetic', () => {
    const engine = new EmpiresEndgameEngine(config, externalState())
    const cityId = 'city-north-frost-harbor'
    const first = engine.externalDiplomacyView(cityId).offers[0]
    expect(first.quote.blockedReason).toBeNull()
    expect(first.quote.tariffGold).toBeGreaterThan(0)
    expect(first.quote.knowledgeBonus).toBeGreaterThan(0)
    expect(executeEmpiresQaExternalOfferPolicy(engine, {
      decision: 'accept',
      offerId: first.id,
      cityId,
    }).ok).toBe(true)
    expect(engine.state.external.offerHistory.at(-1)?.resolution).toBe('accepted')
    expect(engine.acceptExternalOffer(first.id, cityId).ok).toBe(false)
    expect(engine.state.external.customs.smugglingEligible).toBe(true)

    const remaining = engine.state.external.activeOffers[0]
    const reputation = engine.state.empire.reputation
    expect(executeEmpiresQaExternalOfferPolicy(engine, {
      decision: 'decline',
      offerId: remaining.id,
    }).ok).toBe(true)
    expect(engine.state.external.offerHistory.at(-1)?.resolution).toBe('declined')
    expect(engine.state.empire.reputation).toBe(reputation)
  })

  it('rechecks reputation, relationship, accessibility, stock, and expiry in the engine', () => {
    const state = externalState()
    state.empire.reputation = -9
    const denied = new EmpiresEndgameEngine(config, state)
    const offer = denied.state.external.activeOffers[0]
    expect(denied.acceptExternalOffer(offer.id, 'city-north-frost-harbor').message).toContain('Reputation')

    const relationshipState = externalState()
    const relationshipDenied = new EmpiresEndgameEngine(config, relationshipState)
    const relationshipOffer = relationshipDenied.state.external.activeOffers.find(active => (
      config.empire.externalEconomy.offers.find(definition => definition.id === active.definitionId)
        ?.actorId === 'actor-louis'
    ))!
    relationshipDenied.state.external.relationships['actor-louis'].status = 'hostile'
    expect(relationshipDenied.acceptExternalOffer(relationshipOffer.id, 'city-north-frost-harbor').message)
      .toContain('Relationship')

    const accessibilityConfig = clone(config)
    accessibilityConfig.empire.externalEconomy.maxActiveOffers = 3
    const accessibilityState = externalState()
    accessibilityState.external.activeOffers = []
    accessibilityState.external.nextOfferRefreshCon = accessibilityState.con
    const accessibilityDefinition = accessibilityConfig.empire.externalEconomy.offers[0]
    const actor = accessibilityConfig.empire.externalEconomy.actors
      .find(candidate => candidate.id === accessibilityDefinition.actorId)!
    actor.accessibleRegionIds = ['west']
    const inaccessible = new EmpiresEndgameEngine(accessibilityConfig, accessibilityState)
    expect(inaccessible.refreshExternalOffers().ok).toBe(true)
    const accessibilityOffer = inaccessible.state.external.activeOffers.find(
      active => active.actorId === actor.id,
    )!
    expect(inaccessible.acceptExternalOffer(accessibilityOffer.id, 'city-north-frost-harbor').message)
      .toContain('has no access to north')

    const emptyStock = new EmpiresEndgameEngine(config, externalState())
    const emptyOffer = emptyStock.state.external.activeOffers[0]
    emptyOffer.stockRemaining = 0
    expect(emptyStock.acceptExternalOffer(emptyOffer.id, 'city-north-frost-harbor').message)
      .toContain('no stock')

    const expiredState = externalState()
    const expiredOffer = expiredState.external.activeOffers[0]
    expiredState.con = expiredOffer.expiresAfterCon + 1
    expiredState.external.nextOfferRefreshCon = expiredState.con + 1
    const expired = new EmpiresEndgameEngine(config, expiredState)
    expect(expired.refreshExternalOffers().ok).toBe(true)
    expect(expired.state.external.offerHistory.some(record => (
      record.offerId === expiredOffer.id && record.resolution === 'expired'
    ))).toBe(true)
  })

  it('uses Compass for real city transfers and preserves the exact amount', () => {
    const state = externalState()
    const from = state.empire.cities.find(city => city.id === 'city-north-frost-harbor')!
    from.resources.wood = 500
    const engine = new EmpiresEndgameEngine(config, state)
    const beforeDays = engine.state.empire.daysRemaining
    const targetBefore = engine.state.empire.cities
      .find(city => city.id === 'city-north-iron-gate')?.resources.wood ?? 0
    const result = engine.transferCityResource(
      from.id,
      'city-north-iron-gate',
      'wood',
      200,
    )
    expect(result.ok).toBe(true)
    expect(engine.state.empire.daysRemaining).toBe(beforeDays - 3)
    expect(engine.state.empire.cities.find(city => city.id === 'city-north-iron-gate')?.resources.wood)
      .toBe(targetBefore + 200)
    expect(engine.state.external.transferHistory.at(-1)?.amount).toBe(200)
  })

  it('keeps external history bounded, advances stable IDs, and honors disabled migrations', () => {
    const state = externalState()
    const seedOffer = state.external.activeOffers[0]
    const definition = config.empire.externalEconomy.offers.find(
      offer => offer.id === seedOffer.definitionId,
    )!
    const rulesIdentity = seedOffer.rulesIdentity
    state.external.activeOffers = []
    state.external.offerHistory = Array.from({ length: 30 }, (_, index) => ({
      offerId: `external-offer-${index + 1}-${definition.id}`,
      definitionId: definition.id,
      actorId: definition.actorId,
      rulesIdentity,
      resolution: 'declined' as const,
      resolvedAtCon: state.con,
      cityId: null,
      resourceAmount: 0,
      goldDelta: 0,
      tariffGold: 0,
    }))
    state.external.nextOfferSequence = 1
    const restored = new EmpiresEndgameEngine(config, state)
    expect(restored.state.external.offerHistory).toHaveLength(24)
    expect(restored.state.external.compactedOfferHistoryCount).toBe(6)
    expect(restored.state.external.nextOfferSequence).toBe(31)

    const changedConfig = clone(config)
    const staleState = externalState()
    changedConfig.empire.externalEconomy.offers.find(
      offer => offer.id === staleState.external.activeOffers[0].definitionId,
    )!.goldAmount += 1
    expect(() => new EmpiresEndgameEngine(changedConfig, staleState))
      .toThrow(/Invalid external active offer/)

    const disabledConfig = clone(config)
    disabledConfig.empire.externalEconomy.enabled = false
    const disabled = new EmpiresEndgameEngine(disabledConfig)
    disabled.startEmpirePhase()
    expect(disabled.transferCityResource(
      'city-north-frost-harbor',
      'city-north-iron-gate',
      'wood',
      1,
    ).message).toContain('disabled')
    expect(disabled.externalDiplomacyView('city-north-frost-harbor').enabled).toBe(false)
  })

  it('enforces western livestock plus Farm II and makes the live knight consume Stable and horses', () => {
    const state = externalState()
    state.empire.daysRemaining = 100
    state.empire.resources.gold = 100_000
    state.empire.resources.wood = 100_000
    state.empire.resources.horses = 1_000
    state.empire.resources.food = 1_000_000_000_000
    const west = state.empire.cities.find(city => city.id === 'city-west-horse-march')!
    west.buildingLevels['building-farm'] = 2
    west.operationalBuildingLevels['building-farm'] = 2
    west.baseProduction.food = 1_000_000_000
    const engine = new EmpiresEndgameEngine(config, state)
    expect(engine.placeBuilding('city-north-iron-gate', 'slot-unique', 'building-stable').message)
      .toContain('livestock region')
    const placement = engine.placeBuilding(west.id, 'slot-unique', 'building-stable')
    if (!placement.ok) throw new Error(placement.message)
    const knight = config.empire.units?.find(unit => unit.id === 'unit-knight')!
    expect(knight.resourceCosts).toContainEqual({ resourceId: 'horses', amount: 5 })
    expect(knight.dependencies).toContainEqual({
      kind: 'building', buildingId: 'building-stable', level: 1, scope: 'sameCity',
    })
    expect(knight.td?.weaponEquipmentId).toBe('weapon-horseman-pick')
  })

  it('rejects non-coastal and fifth Sea Ports through the shared construction rule', () => {
    const state = externalState()
    const portId = config.empire.externalEconomy.seaPort.buildingId
    const nonCoastal = new EmpiresEndgameEngine(config, clone(state))
    expect(nonCoastal.placeBuilding('city-north-iron-gate', 'slot-unique', portId).message)
      .toContain('coastal')

    for (const cityId of [
      'city-north-frost-harbor', 'city-west-horse-march', 'city-east-alchemy-gate', 'city-center-east',
    ]) {
      const city = state.empire.cities.find(candidate => candidate.id === cityId)!
      const slot = config.empire.cities.find(candidate => candidate.id === cityId)!
        .slots.find(candidate => candidate.kind === 'maritime')!
      city.buildingLevels[portId] = 1
      city.operationalBuildingLevels[portId] = 1
      city.buildingSlotAssignments[slot.id] = portId
    }
    state.governance.governorAssignments.north = {
      regionId: 'north',
      perstId: config.governance.persts[0].id,
      assignedAtCon: state.con,
    }
    const capped = new EmpiresEndgameEngine(config, state)
    expect(capped.placeBuilding('city-north-governor-2-b', 'slot-maritime', portId).message)
      .toContain('maximum 4')
  })
})
