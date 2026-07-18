import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import {
  cloneEmpiresConfig,
  migrateEmpiresConfig,
  validateEmpiresConfig,
} from './config'
import { EmpiresEndgameEngine } from './engine'
import { createEmpiresQaScenarios } from './qa'
import { resolveTdWithPolicy } from './td/qa'
import {
  applySeasonFoodProduction,
  currentSeason,
} from './seasons'
import type {
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresTechnologyDefinition,
} from './types'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(defaultConfigJson)
  value.empire.eventChance = 0
  return value
}

function richEmpireState(value: EmpiresEndgameConfig, con = 1): EmpiresCampaignState {
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'empire'
  state.con = con
  state.event = null
  state.minigame = null
  state.empire.daysRemaining = value.empire.daysPerPhase
  state.empire.researchUsage = {}
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  for (const resource of value.empire.resources) state.empire.resources[resource.id] = 1_000_000_000
  for (const city of state.empire.cities) {
    for (const resource of value.empire.resources) city.resources[resource.id] = 1_000_000_000
  }
  return state
}

function readyToResearch(
  value: EmpiresEndgameConfig,
  technologyId: string,
  con = 1,
): EmpiresEndgameEngine {
  const technology = value.empire.technologies.find(candidate => candidate.id === technologyId)
  if (!technology) throw new Error(`Missing test technology ${technologyId}.`)
  const state = richEmpireState(value, con)
  state.empire.researchedTechnologyIds.push(...technology.prerequisites.flatMap(dependency => (
    dependency.kind === 'technology' ? [dependency.technologyId] : []
  )))
  return new EmpiresEndgameEngine(value, state)
}

function testSides(
  disclosure: NonNullable<EmpiresTechnologyDefinition['sides']>['disclosure'],
  selection: NonNullable<EmpiresTechnologyDefinition['sides']>['selection'] = {
    kind: 'fixed',
    sideId: 'test-dark',
  },
): NonNullable<EmpiresTechnologyDefinition['sides']> {
  return {
    selection,
    disclosure,
    definitions: [
      {
        id: 'test-light',
        name: 'Светлая грань',
        alignment: 'light',
        effects: [],
      },
      {
        id: 'test-dark',
        name: 'Тёмная грань',
        alignment: 'dark',
        effects: [{ kind: 'resource', resourceId: 'iron', amount: 17 }],
        culturalSuppressible: true,
        reputationDelta: -2,
        tags: ['dark-experiment'],
      },
    ],
  }
}

function simpleSteel(): EmpiresTechnologyDefinition {
  return {
    id: 'steel-theocracy-proof',
    name: 'Сталь теократии',
    category: 'steel',
    groupId: 'steel-theocracy-proof',
    timeCostDays: 1,
    resourceCosts: [],
    prerequisites: [
      { kind: 'technology', technologyId: 'tech-ironwork' },
      { kind: 'flag', flagId: 'theocracy', minimum: 1 },
    ],
    effects: [],
    steel: {
      branchId: 'steel-theocracy-proof',
      generation: 0,
      stage: 'whole',
      payoff: 'unlock-only',
    },
  }
}

describe('Empire\'s Endgame Phase 4B seasons and political technology', () => {
  it('derives ordered season boundaries, rounding, production, famine, greenhouse scope, and restore parity', () => {
    const value = config()
    expect(currentSeason(1, value.empire.seasons)?.id).toBe('summer')
    expect(currentSeason(2, value.empire.seasons)?.id).toBe('winter')
    expect(currentSeason(3, value.empire.seasons)?.id).toBe('summer')
    expect(applySeasonFoodProduction(2.75, 1, value.empire.seasons, [])).toBe(5)
    expect(applySeasonFoodProduction(2.75, 2, value.empire.seasons, [])).toBe(2)

    const summerState = richEmpireState(value, 1)
    const winterState = richEmpireState(value, 2)
    const greenhouseState = richEmpireState(value, 2)
    greenhouseState.empire.researchedTechnologyIds.push('tech-greenhouses')
    const summer = new EmpiresEndgameEngine(value, summerState)
    const winter = new EmpiresEndgameEngine(value, winterState)
    const greenhouse = new EmpiresEndgameEngine(value, greenhouseState)
    const cityId = 'city-tetrakor-capital'
    expect(summer.cityProduction(cityId).food).toBe(winter.cityProduction(cityId).food * 2)
    expect(greenhouse.cityProduction(cityId).food).toBe(summer.cityProduction(cityId).food)
    expect(greenhouse.currentSeasonView()).toMatchObject({
      id: 'winter',
      greenhouseEqualized: true,
      foodProductionMultiplierApplied: 2,
    })

    const famineState = richEmpireState(value, 1)
    famineState.empire.resources.food = 0
    const sizing = new EmpiresEndgameEngine(value, famineState)
    for (const city of famineState.empire.cities) {
      city.resources.food = 0
      city.baseProduction.food = Math.ceil(sizing.cityFoodConsumption(city.id) * 0.75)
    }
    const safeSummer = new EmpiresEndgameEngine(value, famineState)
    const winterFamineState = structuredClone(famineState)
    winterFamineState.con = 2
    const hungryWinter = new EmpiresEndgameEngine(value, winterFamineState)
    const summerPopulation = safeSummer.state.empire.cities.map(city => city.population)
    const winterPopulation = hungryWinter.state.empire.cities.map(city => city.population)
    const projectedSummer = safeSummer.cityProduction(cityId).food
    expect(safeSummer.state.empire.cities.find(city => city.id === cityId)?.lastProduction.food)
      .toBe(projectedSummer)
    expect(safeSummer.finishEmpire()).toMatchObject({ ok: true })
    expect(hungryWinter.finishEmpire()).toMatchObject({ ok: true })
    expect(safeSummer.state.empire.cities.map(city => city.population)).toEqual(summerPopulation)
    expect(hungryWinter.state.empire.cities.some((city, index) => city.population < winterPopulation[index])).toBe(true)

    const restored = new EmpiresEndgameEngine(value, safeSummer.snapshot())
    expect(restored.currentSeasonView()).toEqual(safeSummer.currentSeasonView())
    expect(restored.state.empire.chronicle.filter(entry => entry.kind === 'season')).toHaveLength(1)
  })

  it('selects, reveals, applies, and restores a dark side exactly once', () => {
    const value = config()
    const carrier = value.empire.technologies.find(technology => technology.id === 'tech-greenhouses')!
    carrier.prerequisites = []
    carrier.resourceCosts = []
    carrier.timeCostDays = 1
    carrier.sides = testSides({ kind: 'onResearch' })
    const state = richEmpireState(value)
    const beforeIron = state.empire.resources.iron
    const engine = new EmpiresEndgameEngine(value, state)
    expect(engine.research(carrier.id)).toMatchObject({ ok: true })
    expect(engine.technologySideView(carrier.id)).toMatchObject({
      sideId: 'test-dark',
      sideName: 'Тёмная грань',
      alignment: 'dark',
      revealedAtCon: 1,
      effectsAppliedAtCon: 1,
    })
    expect(engine.state.empire.resources.iron).toBe(beforeIron + 17)
    expect(engine.state.empire.reputation).toBe(-2)
    expect(engine.state.empire.chronicle.filter(entry => entry.kind === 'technology-disclosure')).toHaveLength(1)
    const snapshot = engine.snapshot()
    const restored = new EmpiresEndgameEngine(value, snapshot)
    expect(restored.snapshot()).toEqual(snapshot)
    expect(restored.state.empire.resources.iron).toBe(beforeIron + 17)
    expect(restored.state.empire.chronicle.filter(entry => entry.kind === 'technology-disclosure')).toHaveLength(1)
  })

  it('keeps weighted selection deterministic and suppresses dark consequences through culture or theocracy', () => {
    const weighted = config()
    const weightedCarrier = weighted.empire.technologies.find(technology => technology.id === 'tech-greenhouses')!
    weightedCarrier.prerequisites = []
    weightedCarrier.resourceCosts = []
    weightedCarrier.sides = testSides(
      { kind: 'onResearch' },
      { kind: 'weighted', weights: [{ sideId: 'test-light', weight: 1 }, { sideId: 'test-dark', weight: 1 }] },
    )
    const first = new EmpiresEndgameEngine(weighted, richEmpireState(weighted))
    const second = new EmpiresEndgameEngine(weighted, richEmpireState(weighted))
    expect(first.research(weightedCarrier.id)).toMatchObject({ ok: true })
    expect(second.research(weightedCarrier.id)).toMatchObject({ ok: true })
    expect(first.state.empire.technologySides[weightedCarrier.id].sideId)
      .toBe(second.state.empire.technologySides[weightedCarrier.id].sideId)

    for (const suppression of ['culture', 'theocracy'] as const) {
      const value = config()
      const carrier = value.empire.technologies.find(technology => technology.id === 'tech-greenhouses')!
      carrier.prerequisites = []
      carrier.resourceCosts = []
      carrier.sides = testSides({ kind: 'onResearch' })
      const state = richEmpireState(value)
      if (suppression === 'culture') {
        const cultural = value.empire.technologies.find(technology => technology.id === 'doctrine-general')!
        cultural.tags = [...(cultural.tags ?? []), 'cultural-suppression']
        state.empire.researchedTechnologyIds.push(cultural.id)
      } else {
        state.empire.flags.darkExperimentsDisabled = 1
      }
      const beforeIron = state.empire.resources.iron
      const engine = new EmpiresEndgameEngine(value, state)
      expect(engine.research(carrier.id)).toMatchObject({ ok: true })
      expect(engine.state.empire.technologySides[carrier.id].suppressedAtCon).toBe(1)
      expect(engine.state.empire.resources.iron).toBe(beforeIron)
      expect(engine.state.empire.reputation).toBe(0)
    }
  })

  it('remembers hidden combinations and combination-driven disclosure exactly once', () => {
    const value = config()
    const carrier = value.empire.technologies.find(technology => technology.id === 'tech-greenhouses')!
    carrier.prerequisites = []
    carrier.resourceCosts = []
    carrier.sides = testSides({ kind: 'hiddenCombination', combinationId: 'test-combination' })
    value.empire.hiddenCombinations = {
      enabled: true,
      definitions: [{
        id: 'test-combination',
        name: 'Тайное сочетание',
        prerequisites: [{ kind: 'technology', technologyId: carrier.id }],
        tags: ['test'],
      }],
    }
    const engine = new EmpiresEndgameEngine(value, richEmpireState(value))
    expect(engine.research(carrier.id)).toMatchObject({ ok: true })
    expect(engine.state.empire.hiddenCombinationTriggers['test-combination']).toMatchObject({ triggeredAtCon: 1 })
    expect(engine.state.empire.technologySides[carrier.id]).toMatchObject({ revealedAtCon: 1, effectsAppliedAtCon: 1 })
    expect(engine.state.empire.chronicle.filter(entry => entry.kind === 'hidden-combination')).toHaveLength(1)
    expect(engine.state.empire.chronicle.filter(entry => entry.kind === 'technology-disclosure')).toHaveLength(1)
    const snapshot = engine.snapshot()
    const restored = new EmpiresEndgameEngine(value, snapshot)
    expect(restored.snapshot()).toEqual(snapshot)
  })

  it('executes coercion, heroic funerals, and smith control as whole contracts', () => {
    const coercionConfig = config()
    for (const building of coercionConfig.empire.buildings) {
      for (const level of building.levels) level.workerDemand = 0
    }
    const coercion = readyToResearch(coercionConfig, 'reform-coercion')
    expect(coercion.transitionAdvisor('advisor-war', 'pardon')).toMatchObject({ ok: true })
    expect(coercion.transitionAdvisor('advisor-science', 'execute')).toMatchObject({ ok: true })
    expect(coercion.transitionAdvisor('advisor-trade', 'execute')).toMatchObject({ ok: true })
    coercion.state.empire.researchedTechnologyIds.push('tech-ironwork')
    expect(coercion.research('reform-coercion')).toMatchObject({ ok: true })
    expect(coercion.state.empire.cities.every(city => city.loyalty === -1)).toBe(true)
    expect(coercion.buildingOperationView('city-tetrakor-capital', 'building-smithy').operationalLevel).toBe(1)
    expect(coercion.constructionBlockedReason('city-tetrakor-capital', 'building-smithy', 2)).toContain('City loyalty')

    const funeralsConfig = config()
    const funerals = readyToResearch(funeralsConfig, 'reform-heroic-funerals')
    expect(funerals.research('reform-heroic-funerals')).toMatchObject({ ok: true })
    const city = funerals.state.empire.cities[0]
    const loyaltyBefore = city.loyalty
    expect(funerals.consumeBattleLoss({
      id: 'phase4b:heroic-loss',
      target: { kind: 'city', cityId: city.id },
      deployed: 10,
      lost: 10,
    })).toBe(true)
    expect(city.loyalty).toBe(loyaltyBefore)

    const fixture = createEmpiresQaScenarios(funeralsConfig, { seed: 'phase4b-funerals' })['battle-defense']
    fixture.snapshot.empire.flags.casualtyLoyaltyPenaltyDisabled = 1
    fixture.snapshot.empire.flags.casualtyRecruitGrowthPenaltyDisabled = 1
    fixture.snapshot.empire.researchedTechnologyIds.push('reform-heroic-funerals')
    const battle = new EmpiresEndgameEngine(funeralsConfig, fixture.snapshot)
    const militaryBefore = Object.fromEntries(battle.state.empire.cities.map(candidate => [candidate.id, candidate.militaryPopulation]))
    const result = resolveTdWithPolicy(battle.state.minigame!.plan, battle.state.minigame!.seed, 'passive')
    expect(battle.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(battle.state.army.recruitmentPenalties).toEqual({})
    expect(battle.state.empire.cities.every(candidate => candidate.militaryPopulation === militaryBefore[candidate.id])).toBe(true)

    const smithConfig = config()
    smithConfig.empire.loyalty.initialClassLoyalty = 2
    const smith = readyToResearch(smithConfig, 'reform-control-smiths')
    smith.state.empire.researchedTechnologyIds.push(
      'tech-ironwork',
      'steel-laurel-spearhead',
      'steel-lancet-spearhead',
      'steel-diamond-spearhead',
      'steel-cross-spearhead',
    )
    const classBefore = structuredClone(smith.state.empire.loyalty.classModifiers)
    expect(smith.research('reform-control-smiths')).toMatchObject({ ok: true })
    expect(smith.state.empire.cities.every(candidate => (
      smith.state.empire.loyalty.classModifiers[candidate.id].burghers
        === classBefore[candidate.id].burghers - 1
      && smith.state.empire.loyalty.classModifiers[candidate.id].nobles
        === classBefore[candidate.id].nobles - 2
    ))).toBe(true)
    const option = smith.smithSpecializationOptions()[0]
    expect(option).toBeDefined()
    expect(smith.chooseSmithSpecialization(option.recipeId)).toMatchObject({ ok: true })
    expect(smith.chooseSmithSpecialization(smith.smithSpecializationOptions()[1]?.recipeId ?? 'other'))
      .toMatchObject({ ok: false })
    const equipmentBefore = smith.state.army.equipmentStock[option.equipmentId] ?? 0
    expect(smith.finishEmpire()).toMatchObject({ ok: true })
    expect(smith.state.army.equipmentStock[option.equipmentId]).toBeGreaterThan(equipmentBefore)
  })

  it('makes theocracy satisfy an exact steel flag prerequisite and suppress dark experiments', () => {
    const value = config()
    expect(value.empire.technologies.find(technology => technology.id === 'steel-bucket-helm')?.prerequisites)
      .toContainEqual({ kind: 'flag', flagId: 'theocracy', minimum: 1 })
    value.empire.technologies.push(simpleSteel())
    const engine = readyToResearch(value, 'reform-theocracy')
    expect(engine.transitionAdvisor('advisor-science', 'pardon')).toMatchObject({ ok: true })
    expect(engine.transitionAdvisor('advisor-trade', 'execute')).toMatchObject({ ok: true })
    expect(engine.transitionAdvisor('advisor-war', 'execute')).toMatchObject({ ok: true })
    engine.state.empire.researchedTechnologyIds.push('tech-ironwork')
    expect(engine.researchQuote('steel-theocracy-proof').blockedReason).toContain('theocracy')
    expect(engine.research('reform-theocracy')).toMatchObject({ ok: true })
    expect(engine.state.empire.flags).toMatchObject({ theocracy: 1, darkExperimentsDisabled: 1 })
    expect(engine.researchQuote('steel-theocracy-proof').blockedReason).toBeNull()
  })

  it('keeps undefined crime, political faces, technocracy, printing, and epidemic effects deferred', () => {
    const value = config()
    expect(value.empire.technologies.find(item => item.id === 'reform-technocracy')?.deferredReason).toBeTruthy()
    expect(value.empire.technologies.find(item => item.id === 'reform-city-gates')).toMatchObject({
      deferredReason: expect.any(String),
      effects: [],
      sides: { definitions: expect.any(Array) },
    })
    expect(value.empire.technologies.find(item => item.id === 'tech-printing')?.deferredReason).toBeTruthy()
    for (const cardId of ['card-hearts-5', 'card-hearts-king']) {
      const card = value.cards.find(candidate => candidate.id === cardId)!
      expect(card.normal.deferredReason).toBeTruthy()
      expect(card.inverted.deferredReason).toBeTruthy()
    }

    const unreadCrime = config()
    const gates = unreadCrime.empire.technologies.find(item => item.id === 'reform-city-gates')!
    delete gates.deferredReason
    gates.effects.push({ kind: 'flag', flagId: 'crimeMultiplierPercent', amount: -50 })
    expect(() => validateEmpiresConfig(unreadCrime)).toThrow(/unsupported live flag crimeMultiplierPercent/)
  })

  it('migrates v5 season scaffolds without inventing live rules and ships the crossing QA fixture', () => {
    const previous = structuredClone(defaultConfigJson) as unknown as Record<string, unknown>
    previous.schemaVersion = 5
    const previousEmpire = previous.empire as Record<string, unknown>
    previousEmpire.seasons = {
      enabled: true,
      definitions: [{ id: 'legacy', name: 'Legacy' }],
    }
    delete previousEmpire.hiddenCombinations
    const before = structuredClone(previous)
    const migrated = migrateEmpiresConfig(previous) as EmpiresEndgameConfig & {
      empire: EmpiresEndgameConfig['empire'] & { seasons: { legacyDefinitions?: unknown[] } }
    }
    expect(previous).toEqual(before)
    expect(migrated.schemaVersion).toBe(12)
    expect(migrated.empire.seasons).toMatchObject({
      enabled: false,
      definitions: [],
      foodRounding: 'none',
      greenhouse: null,
      legacyDefinitions: [{ id: 'legacy', name: 'Legacy' }],
    })
    expect(migrated.empire.hiddenCombinations).toEqual({ enabled: false, definitions: [] })

    const fixture = createEmpiresQaScenarios(config(), { seed: 'phase4b-qa' })['season-disclosure']
    expect(fixture.snapshot).toMatchObject({ con: 2, empire: { reputation: -1 } })
    expect(fixture.snapshot.empire.chronicle.filter(entry => entry.kind === 'season')).toHaveLength(1)
    expect(fixture.snapshot.empire.chronicle.filter(entry => entry.kind === 'technology-disclosure')).toHaveLength(1)
  })
})
