import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { exportEmpiresCampaign, importEmpiresCampaign } from './persistence'
import { createEmpiresQaScenarios } from './qa'
import { replayAlchemy } from './alchemy/engine'
import { resolveAlchemyExplosionFixture, resolveAlchemyWithPolicy } from './alchemy/qa'
import type {
  EmpiresAlchemyMinigameSession,
  EmpiresCampaignState,
  EmpiresEndgameConfig,
} from './types'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(bundledConfigJson)
  value.empire.eventChance = 0
  return value
}

function labState(value: EmpiresEndgameConfig): { state: EmpiresCampaignState, cityId: string, recipeId: string } {
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = Math.max(value.empire.daysPerPhase, value.alchemy.dayCost + 1)
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  const cityDefinition = value.empire.cities.find(city => city.slots.some(slot => slot.kind === 'unique'))!
  const city = state.empire.cities.find(candidate => candidate.id === cityDefinition.id)!
  const slot = cityDefinition.slots.find(candidate => candidate.kind === 'unique')!
  city.buildingLevels[value.alchemy.buildingId] = 1
  city.operationalBuildingLevels[value.alchemy.buildingId] = 1
  city.buildingSlotAssignments[slot.id] = value.alchemy.buildingId
  const recipe = value.alchemy.recipes.find(candidate => !candidate.deferredReason)!
  const requiredTechnologies = [
    ...recipe.prerequisites,
    ...(value.empire.buildings.find(building => building.id === value.alchemy.buildingId)
      ?.levels.flatMap(level => level.dependencies) ?? []),
  ].flatMap(dependency => dependency.kind === 'technology' ? [dependency.technologyId] : [])
  state.empire.researchedTechnologyIds = [...new Set([
    ...state.empire.researchedTechnologyIds,
    ...requiredTechnologies,
  ])]
  return { state: new EmpiresEndgameEngine(value, state).snapshot(), cityId: city.id, recipeId: recipe.id }
}

function session(engine: EmpiresEndgameEngine): EmpiresAlchemyMinigameSession {
  if (engine.state.minigame?.kind !== 'alchemy') throw new Error('Fixture did not start Alchemy.')
  return engine.state.minigame
}

describe('Empire\'s Endgame Phase 10 Tetris-alchemy', () => {
  it('migrates v14 fail-closed without mutation, is idempotent, and rejects future schema v20', () => {
    const legacy = structuredClone(bundledConfigJson) as unknown as Record<string, unknown>
    legacy.schemaVersion = 14
    delete legacy.alchemy
    const original = structuredClone(legacy)

    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(original)
    expect(migrated).toMatchObject({ schemaVersion: 19, alchemy: { enabled: false, recipes: [] } })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 20 })).toThrow(/future.*20/i)
  })

  it('ships assembly, disassembly, poison-wall, and medicine recipes with no deferred Alchemy ledger entries', () => {
    const value = config()
    const building = value.empire.buildings.find(candidate => candidate.id === value.alchemy.buildingId)!
    expect(building.deferredReason).toBeUndefined()
    expect(building.deferredSubfeatures ?? []).toEqual([])
    expect(value.alchemy.recipes.filter(recipe => !recipe.deferredReason)).toEqual([
      expect.objectContaining({ id: 'alchemy-calibration-assembly', mode: 'assembly', family: 'experiment' }),
      expect.objectContaining({
        id: 'alchemy-salvage-disassembly',
        mode: 'disassembly',
        rewards: [{ kind: 'resource', resourceId: 'stone', amount: 300 }],
      }),
      expect.objectContaining({
        id: 'alchemy-poison-wall-assembly',
        mode: 'assembly',
        family: 'poison',
        rewards: [{ kind: 'resource', resourceId: 'stone', amount: 500 }],
      }),
      expect.objectContaining({
        id: 'alchemy-clinical-lattice-assembly',
        mode: 'assembly',
        family: 'medicine',
        rewards: [{ kind: 'resource', resourceId: 'knowledge', amount: 400 }],
      }),
    ])
    expect(value.alchemy.deferredSubfeatures).toEqual([])
    expect(() => validateEmpiresConfig(value)).not.toThrow()

    const unknownRecipe = cloneEmpiresConfig(value)
    unknownRecipe.alchemy.recipes[0].pieceDefinitionIds = ['missing-piece']
    expect(() => validateEmpiresConfig(unknownRecipe)).toThrow(/recipe/i)
    const unknownEpidemic = cloneEmpiresConfig(value)
    unknownEpidemic.alchemy.explosion.epidemicDefinitionId = 'missing-epidemic'
    expect(() => validateEmpiresConfig(unknownEpidemic)).toThrow(/epidemic/i)
  })

  it('blocks inaccessible launches, freezes rules, spends the authored day cost, and rewards success once', () => {
    const value = config()
    value.alchemy.recipes[0].rewards = [{ kind: 'resource', resourceId: 'gold', amount: 7 }]
    const locked = new EmpiresEndgameEngine(value)
    const firstCity = locked.state.empire.cities[0].id
    expect(locked.startAlchemyExperiment(firstCity, value.alchemy.recipes[0].id)).toMatchObject({ ok: false })

    const fixture = labState(value)
    const engine = new EmpiresEndgameEngine(value, fixture.state)
    const daysBefore = engine.state.empire.daysRemaining
    const goldBefore = engine.state.empire.resources.gold ?? 0
    expect(engine.startAlchemyExperiment(fixture.cityId, fixture.recipeId)).toMatchObject({ ok: true })
    const active = session(engine)
    expect(active.plan.recipe.rewards).toEqual([{ kind: 'resource', resourceId: 'gold', amount: 7 }])
    expect(engine.state.empire.daysRemaining).toBe(daysBefore - value.alchemy.dayCost)
    const result = resolveAlchemyWithPolicy(active.plan, active.seed, 'greedy')
    expect(result.outcome).toBe('success')
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(engine.state.empire.resources.gold).toBe(goldBefore + 7)
    expect(engine.state.empire.chronicle.at(-1)).toMatchObject({ kind: 'alchemy' })
    expect(engine.state.minigameResultLog.at(-1)?.result).toEqual(result)

    const settled = engine.snapshot()
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true, message: expect.stringMatching(/already resolved/i) })
    expect(engine.snapshot()).toEqual(settled)
  })

  it('settles abort and tick-cap failure without refunds or rewards and never duplicates either result', () => {
    const value = config()
    value.alchemy.recipes[0].rewards = [{ kind: 'resource', resourceId: 'gold', amount: 99 }]
    const abortFixture = labState(value)
    const aborted = new EmpiresEndgameEngine(value, abortFixture.state)
    const daysBefore = aborted.state.empire.daysRemaining
    const goldBefore = aborted.state.empire.resources.gold ?? 0
    expect(aborted.startAlchemyExperiment(abortFixture.cityId, abortFixture.recipeId)).toMatchObject({ ok: true })
    expect(aborted.abortMinigame([], 0)).toMatchObject({ ok: true })
    expect(aborted.state.empire.daysRemaining).toBe(daysBefore - value.alchemy.dayCost)
    expect(aborted.state.empire.resources.gold ?? 0).toBe(goldBefore)
    expect(aborted.state.minigameResultLog.at(-1)?.result).toMatchObject({ outcome: 'aborted' })

    const failureConfig = config()
    failureConfig.alchemy.maxTicks = 1
    const failureFixture = labState(failureConfig)
    const failed = new EmpiresEndgameEngine(failureConfig, failureFixture.state)
    expect(failed.startAlchemyExperiment(failureFixture.cityId, failureFixture.recipeId)).toMatchObject({ ok: true })
    const active = session(failed)
    const result = replayAlchemy(active.plan, active.seed, [])
    expect(result).toMatchObject({ outcome: 'failure', terminalReason: 'tick-cap' })
    expect(failed.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(failed.state.minigameResultLog).toHaveLength(1)
  })

  it('routes a trusted explosion through the epidemic funnel, locks the laboratory, and persists the anti-retry state', () => {
    const value = config()
    value.alchemy.acceleration.explosionThresholdPercent = 101
    const fixture = labState(value)
    const engine = new EmpiresEndgameEngine(value, fixture.state)
    expect(engine.startAlchemyExperiment(fixture.cityId, fixture.recipeId)).toMatchObject({ ok: true })
    const active = session(engine)
    const result = replayAlchemy(active.plan, active.seed, [])
    expect(result).toMatchObject({ outcome: 'explosion', explosionRequest: {
      originCityId: fixture.cityId,
      epidemicDefinitionId: value.alchemy.explosion.epidemicDefinitionId,
      severity: value.alchemy.explosion.severityMultiplier,
      source: { kind: 'alchemy', id: `alchemy:${active.id}` },
      mutantAftermath: value.alchemy.explosion.mutantAftermath,
    } })
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    const epidemic = engine.state.epidemics.find(candidate => candidate.source.kind === 'alchemy')
    const city = engine.state.empire.cities.find(candidate => candidate.id === fixture.cityId)!
    expect(epidemic).toMatchObject({ cityId: fixture.cityId, severityMultiplier: 1.5 })
    expect(city.buildingInteractionLocks[value.alchemy.buildingId]).toBe(engine.state.con)
    expect(engine.state.alchemy).toMatchObject({
      explosionCount: 1,
      lastExplosion: { sessionId: active.id, epidemicInstanceId: epidemic?.id },
      pendingMutantAftermaths: [{
        id: `alchemy-mutants:${active.id}`,
        sourceSessionId: active.id,
        cityId: fixture.cityId,
        scheduledAtCon: engine.state.con,
        dueCon: engine.state.con + value.alchemy.explosion.mutantAftermath.delayCons,
        populationLoss: value.alchemy.explosion.mutantAftermath.populationLoss,
        loyaltyDelta: value.alchemy.explosion.mutantAftermath.loyaltyDelta,
      }],
    })
    expect(engine.domesticEconomyView(fixture.cityId).alchemy).toMatchObject({
      pendingMutantAftermathCount: 1,
      nextMutantAftermathCon: engine.state.con + value.alchemy.explosion.mutantAftermath.delayCons,
    })
    expect(engine.alchemyExperimentBlockedReason(fixture.cityId, fixture.recipeId)).toMatch(/закрыта|недоступ/i)

    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.epidemics).toHaveLength(1)
    expect(restored.state.alchemy.explosionCount).toBe(1)
    expect(restored.alchemyExperimentBlockedReason(fixture.cityId, fixture.recipeId)).not.toBeNull()
    const populationBefore = restored.state.empire.cities.find(candidate => candidate.id === fixture.cityId)!.population
    const advance = restored as unknown as { startNextCon: () => void }
    for (let elapsed = 0; elapsed < value.alchemy.explosion.mutantAftermath.delayCons; elapsed += 1) {
      advance.startNextCon()
    }
    const affectedCity = restored.state.empire.cities.find(candidate => candidate.id === fixture.cityId)!
    expect(affectedCity.population).toBe(Math.max(
      0,
      populationBefore - value.alchemy.explosion.mutantAftermath.populationLoss,
    ))
    expect(restored.state.alchemy).toMatchObject({
      pendingMutantAftermaths: [],
      lastMutantAftermath: {
        id: `alchemy-mutants:${active.id}`,
        sourceSessionId: active.id,
        populationLost: populationBefore - affectedCity.population,
        loyaltyDelta: value.alchemy.explosion.mutantAftermath.loyaltyDelta,
      },
    })
    const settledPopulation = affectedCity.population
    advance.startNextCon()
    expect(restored.state.empire.cities.find(candidate => candidate.id === fixture.cityId)!.population)
      .toBe(settledPopulation)
  })

  it('provides a deterministic QA explosion fixture for the bundled launch rules', () => {
    const value = config()
    const fixture = labState(value)
    const engine = new EmpiresEndgameEngine(value, fixture.state)
    expect(engine.startAlchemyExperiment(fixture.cityId, fixture.recipeId)).toMatchObject({ ok: true })
    const active = session(engine)
    const result = resolveAlchemyExplosionFixture(active.plan, active.seed)
    expect({
      outcome: result.outcome,
      terminalReason: result.terminalReason,
      settledPieces: result.settledPieces,
      speedPercent: result.speedPercent,
      completedTick: result.completedTick,
    }).toEqual({
      outcome: 'explosion',
      terminalReason: 'explosion',
      settledPieces: 25,
      speedPercent: 425,
      completedTick: expect.any(Number),
    })
  })

  it('settles the browser QA explosion seed into a visible epidemic projection', () => {
    const value = config()
    const fixture = createEmpiresQaScenarios(value, { seed: 'cypress-empires-alchemy' })['alchemy-experiment']
    const engine = new EmpiresEndgameEngine(value, fixture.snapshot)
    const active = session(engine)
    const result = resolveAlchemyExplosionFixture(active.plan, active.seed)

    expect(result).toMatchObject({
      outcome: 'explosion',
      terminalReason: 'explosion',
      settledPieces: 25,
      speedPercent: 425,
    })
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(engine.state.epidemics).toEqual([
      expect.objectContaining({ cityId: active.plan.originCityId, endedAtCon: null }),
    ])
    expect(engine.cityEpidemicViews(active.plan.originCityId)).toHaveLength(1)
  })

  it('rejects replay tampering, inaccessible origins, and active-session rules mismatches without settlement', () => {
    const value = config()
    const fixture = labState(value)
    const tamperedEngine = new EmpiresEndgameEngine(value, fixture.state)
    expect(tamperedEngine.startAlchemyExperiment(fixture.cityId, fixture.recipeId)).toMatchObject({ ok: true })
    const active = session(tamperedEngine)
    const result = resolveAlchemyWithPolicy(active.plan, active.seed, 'greedy')
    result.rulesIdentity.rulesDigest = 'stale'
    const before = tamperedEngine.snapshot()
    expect(tamperedEngine.resolveMinigame(result)).toMatchObject({ ok: false })
    expect(tamperedEngine.snapshot()).toEqual(before)

    const inaccessible = new EmpiresEndgameEngine(value, fixture.state)
    expect(inaccessible.startAlchemyExperiment(fixture.cityId, fixture.recipeId)).toMatchObject({ ok: true })
    const inaccessibleSession = session(inaccessible)
    const inaccessibleResult = resolveAlchemyWithPolicy(inaccessibleSession.plan, inaccessibleSession.seed, 'greedy')
    const regionId = inaccessible.state.empire.cities.find(city => city.id === fixture.cityId)!.regionId
    inaccessible.state.empire.destroyedRegionIds.push(regionId)
    const inaccessibleBefore = inaccessible.snapshot()
    expect(inaccessible.resolveMinigame(inaccessibleResult)).toMatchObject({ ok: false, message: expect.stringMatching(/accessible/i) })
    expect(inaccessible.snapshot()).toEqual(inaccessibleBefore)

    const reload = new EmpiresEndgameEngine(value, fixture.state)
    expect(reload.startAlchemyExperiment(fixture.cityId, fixture.recipeId)).toMatchObject({ ok: true })
    const activeSnapshot = reload.snapshot()
    const restored = new EmpiresEndgameEngine(value, activeSnapshot)
    expect(session(restored).attempt).toBe(session(reload).attempt + 1)
    const changed = cloneEmpiresConfig(value)
    changed.alchemy.tickMs += 1
    expect(() => new EmpiresEndgameEngine(changed, activeSnapshot)).toThrow(/rules|config/i)
  })

  it('round-trips schema v18, normalizes v12 additively, and rejects a future v19 envelope', () => {
    const value = config()
    const state = new EmpiresEndgameEngine(value).snapshot()
    const exported = exportEmpiresCampaign(state)
    expect(exported).toMatchObject({ schemaVersion: 18, state: { schemaVersion: 18 } })
    expect(importEmpiresCampaign(exported, value.id)).toEqual(state)

    const legacyState = structuredClone(state) as unknown as Record<string, unknown>
    legacyState.schemaVersion = 12
    delete legacyState.alchemy
    const restored = importEmpiresCampaign({
      schemaVersion: 12,
      savedAt: '2026-07-18T00:00:00.000Z',
      state: legacyState,
    }, value.id)
    expect(new EmpiresEndgameEngine(value, restored).state).toMatchObject({
      schemaVersion: 18,
      alchemy: { explosionCount: 0, lastExplosion: null },
    })
    expect(() => importEmpiresCampaign({ ...exported, schemaVersion: 19 }, value.id)).toThrow(/1–18/)
  })
})
