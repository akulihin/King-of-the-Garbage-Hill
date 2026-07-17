import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import {
  cloneEmpiresConfig,
  migrateEmpiresConfig,
  validateEmpiresConfig,
} from './config'
import { EmpiresEndgameEngine } from './engine'
import { createEmpiresQaScenarios } from './qa'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function config(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(defaultConfigJson)
}

function governanceSnapshot(value: EmpiresEndgameConfig): EmpiresCampaignState {
  return createEmpiresQaScenarios(value, { seed: 'phase-4c-governance' }).governance.snapshot
}

describe('Empire\'s Endgame Phase 4C governance', () => {
  it('migrates schema 6 to a disabled schema-7 scaffold without mutation and rejects future input', () => {
    const previous = structuredClone(defaultConfigJson) as unknown as Record<string, unknown>
    previous.schemaVersion = 6
    delete previous.governance
    const original = structuredClone(previous)

    const migrated = migrateEmpiresConfig(previous) as EmpiresEndgameConfig
    expect(previous).toEqual(original)
    expect(migrated).toMatchObject({
      schemaVersion: 8,
      governance: {
        enabled: false,
        advisors: [],
        persts: [],
        governor: { assignmentMode: 'permanent', citySites: [] },
      },
    })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(migrateEmpiresConfig(migrated)).not.toBe(migrated)
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 9 })).toThrow(/future.*9/i)
  })

  it('validates advisor, city-site, capital-carrier, and defense-layer references', () => {
    const value = config()
    expect(() => validateEmpiresConfig(value)).not.toThrow()

    const missingAdvisor = config()
    missingAdvisor.empire.technologies.find(technology => technology.id === 'reform-theocracy')!
      .prerequisites = []
    expect(() => validateEmpiresConfig(missingAdvisor)).toThrow(/advisor-science.*must use its advisor prerequisite/)

    const brokenLayer = config()
    brokenLayer.governance.governor.citySites.find(site => site.cityId === 'city-north-governor-3')!
      .defenseLayer = 2
    expect(() => validateEmpiresConfig(brokenLayer)).toThrow(/2 initial.*2 layer-two.*1 layer-three/)

    const brokenCarrier = config()
    brokenCarrier.governance.capital.sites[0].buildingId = 'missing-building'
    expect(() => validateEmpiresConfig(brokenCarrier)).toThrow(/capital site.*invalid carrier/)
  })

  it('resolves exactly one pardon and two executions through the public funnel and persists sequence identity', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value, governanceSnapshot(value))
    expect(engine.researchQuote('reform-theocracy').blockedReason).toContain('advisor-science')

    expect(engine.transitionAdvisor('advisor-science', 'pardon').ok).toBe(true)
    expect(engine.transitionAdvisor('advisor-trade', 'pardon').ok).toBe(false)
    expect(engine.transitionAdvisor('advisor-trade', 'execute').ok).toBe(true)
    expect(engine.transitionAdvisor('advisor-war', 'execute').ok).toBe(true)
    expect(engine.transitionAdvisor('advisor-war', 'execute').ok).toBe(false)
    expect(engine.researchQuote('reform-theocracy').blockedReason).toContain('doctrine-science')

    expect(engine.state.governance.advisors).toMatchObject({
      'advisor-science': { status: 'active', transitionSequence: 1, transitionSourceId: 'advisor-judgment:pardon' },
      'advisor-trade': { status: 'executed', transitionSequence: 2, transitionSourceId: 'advisor-judgment:execute' },
      'advisor-war': { status: 'executed', transitionSequence: 3, transitionSourceId: 'advisor-judgment:execute' },
      'advisor-grand': { status: 'locked', transitionSequence: null },
    })
    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.governance).toEqual(engine.state.governance)
    expect(restored.snapshotEnvelope('2026-07-17T00:00:00.000Z').schemaVersion).toBe(6)
  })

  it('keeps clubs unavailable until an authored Grand Advisor grant, then makes clubs trump exactly once', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    expect(engine.state.durak.trumpSuit).not.toBe('clubs')
    expect(engine.transitionAdvisor('advisor-grand', 'grant-access').ok).toBe(false)
    expect(engine.transitionAdvisor('advisor-grand', 'grant-access', 'qa:grand-advisor').ok).toBe(true)
    expect(engine.state.durak.trumpSuit).toBe('clubs')
    expect(engine.state.governance.advisors['advisor-grand']).toMatchObject({
      status: 'active',
      transitionSequence: 1,
      transitionSourceId: 'qa:grand-advisor',
    })
    expect(engine.transitionAdvisor('advisor-grand', 'grant-access', 'qa:duplicate').ok).toBe(false)
    expect(new EmpiresEndgameEngine(value, engine.snapshot()).state.governance)
      .toEqual(engine.state.governance)
  })

  it('grandfathers a legacy club-trump save while leaving migrated standard advisors unresolved', () => {
    const value = config()
    const legacy = new EmpiresEndgameEngine(value).snapshot() as EmpiresCampaignState & {
      governance?: EmpiresCampaignState['governance']
    }
    legacy.schemaVersion = 4 as never
    legacy.durak.trumpSuit = 'clubs'
    delete legacy.governance

    const restored = new EmpiresEndgameEngine(value, legacy)
    expect(restored.state.schemaVersion).toBe(6)
    expect(restored.state.governance.advisors['advisor-grand']).toMatchObject({
      status: 'active',
      transitionSourceId: 'migration:legacy-restricted-trump',
    })
    expect(restored.state.governance.advisors['advisor-science'].status).toBe('awaiting-judgment')
  })

  it('applies critical trump magnitude to live card effects and rebuilds it identically after reload', () => {
    const value = config()
    value.durak.fixedTrumpSuit = 'spades'
    const card = value.cards.find(definition => (
      definition.suit === 'spades'
      && !definition.normal.deferredReason
      && definition.normal.effects.some(effect => effect.kind === 'resourceMultiplier')
    ))
    expect(card).toBeDefined()
    const multiplierEffect = card!.normal.effects.find(effect => effect.kind === 'resourceMultiplier')!
    if (multiplierEffect.kind !== 'resourceMultiplier') throw new Error('Expected multiplier effect')
    const chosenGift = value.gifts.definitions.find(gift => !gift.deferredReason && !gift.resolution)!
    chosenGift.effects = []
    chosenGift.application = 'once'
    const engine = new EmpiresEndgameEngine(value)
    const ready = engine.snapshot()
    ready.phase = 'divineGift'
    ready.giftChoiceIds = [chosenGift.id]
    ready.durak.playerHand = [card!.id]
    ready.cards[card!.id].inverted = false
    ready.cards[card!.id].level = 0
    const active = new EmpiresEndgameEngine(value, ready)

    expect(active.chooseGift(chosenGift.id).ok).toBe(true)
    const expected = 1 + (multiplierEffect.multiplier - 1)
      * value.governance.trump.criticalEffectMultiplier
    expect(active.state.empire.productionMultipliers[multiplierEffect.resourceId]).toBeCloseTo(expected)
    expect(new EmpiresEndgameEngine(value, active.snapshot()).state.empire.productionMultipliers)
      .toEqual(active.state.empire.productionMultipliers)
  })

  it('permanently expands a governed region from two to five usable city sites and rejects dangling saves', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value, governanceSnapshot(value))
    const newCityId = 'city-north-governor-2-a'
    expect(value.governance.governor.citySites.filter(site => site.regionId === 'north')).toHaveLength(5)
    expect(engine.state.empire.cities.filter(city => city.regionId === 'north' && engine.isCityAccessible(city.id))).toHaveLength(2)
    expect(engine.constructionBlockedReason(newCityId, 'building-farm', 1)).toContain('Perst')

    engine.state.empire.resources = Object.fromEntries(value.empire.resources.map(resource => [resource.id, 1_000_000]))
    engine.state.empire.resources.food = 1_000_000_000_000
    engine.state.empire.daysRemaining = 1_000
    engine.state.empire.cities.find(city => city.id === newCityId)!.baseProduction.food = 1_000_000
    expect(engine.assignGovernor('perst-fourth-trevor', 'north').ok).toBe(true)
    expect(engine.assignGovernor('perst-fourth-trevor', 'west').ok).toBe(false)
    expect(engine.assignGovernor('perst-tenth', 'north').ok).toBe(false)
    expect(engine.state.empire.cities.filter(city => city.regionId === 'north' && engine.isCityAccessible(city.id))).toHaveLength(5)
    expect(engine.constructionBlockedReason(newCityId, 'building-farm', 1)).toBeNull()
    expect(new EmpiresEndgameEngine(value, engine.snapshot()).isCityAccessible(newCityId)).toBe(true)

    const dangling = engine.snapshot()
    dangling.governance.governorAssignments.north.perstId = 'missing-perst'
    expect(() => new EmpiresEndgameEngine(value, dangling)).toThrow(/Invalid governance governor assignment north/)
  })

  it('keeps capital carriers and unresolved semantics explicitly deferred to their owning phases', () => {
    const value = config()
    expect(value.governance.capital).toMatchObject({ cityId: 'city-tetrakor-capital' })
    expect(value.governance.capital.sites.map(site => [site.name, site.owner])).toEqual([
      ['Тетракорархос', 'P4C/P12B'],
      ['Форум', 'P4A'],
      ['Колизей', 'P4C/P12B'],
      ['Военная академия', 'P3B'],
      ['Шахта белого камня', 'P6C'],
    ])
    expect(value.governance.capital.sites.every(site => Boolean(site.deferredReason))).toBe(true)
    expect(value.empire.buildings.find(building => building.id === 'municipal-capital-forum')?.allowedCityIds)
      .toEqual(['city-tetrakor-capital'])
    expect(value.empire.buildings.find(building => building.id === 'building-military-academy')?.allowedCityIds)
      .toEqual(['city-tetrakor-capital'])
  })

  it('does not change standard deck behavior when governance is disabled', () => {
    const value = config()
    value.governance.enabled = false
    value.durak.fixedTrumpSuit = 'clubs'
    expect(new EmpiresEndgameEngine(value).state.durak.trumpSuit).toBe('clubs')
  })
})
