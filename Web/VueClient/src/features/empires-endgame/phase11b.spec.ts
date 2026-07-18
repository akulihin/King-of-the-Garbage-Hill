import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import {
  empiresConfigReplacementDisabledReason,
  migrateEmpiresConfig,
  validateEmpiresConfig,
} from './config'
import { EmpiresEndgameEngine } from './engine'
import { resolveInventoryWithPolicy } from './inventory/qa'
import { exportEmpiresCampaign, importEmpiresCampaign } from './persistence'
import { createEmpiresQaScenarios } from './qa'
import { replayTdBattle } from './td/engine'
import type {
  EmpiresEndgameConfig,
  EmpiresInventoryMinigameSession,
  EmpiresTdMinigameSession,
} from './types'

const EXPEDITION_ID = 'expedition-south-fortress'

function config(): EmpiresEndgameConfig {
  const value = migrateEmpiresConfig(structuredClone(bundledConfigJson))
  validateEmpiresConfig(value)
  return value
}

function engine(value = config()): EmpiresEndgameEngine {
  const fixture = createEmpiresQaScenarios(value, { seed: 'phase11b' })['expedition-planning']
  return new EmpiresEndgameEngine(value, fixture.snapshot)
}

function beginPlanning(value: EmpiresEndgameEngine) {
  expect(value.beginExpeditionPlanning(EXPEDITION_ID)).toMatchObject({ ok: true })
  const view = value.expeditionPlanningView(EXPEDITION_ID)!
  const ids = view.roster.filter(item => item.eligible).slice(0, 2).map(item => item.unitInstanceId)
  const required = ids.reduce((total, id) => {
    const unit = view.roster.find(item => item.unitInstanceId === id)!
    return total + unit.foodPerCon * view.effectiveDurationCons
  }, 0)
  return { ids, required }
}

function activeInventory(value: EmpiresEndgameEngine): EmpiresInventoryMinigameSession {
  const session = value.state.minigame
  if (session?.kind !== 'inventory') throw new Error('Expected an active Inventory session.')
  return session
}

function southFood(value: EmpiresEndgameEngine): number {
  return value.state.empire.cities
    .filter(city => city.regionId === 'south')
    .reduce((total, city) => total + (city.resources.food ?? 0), 0)
}

describe('Empire\'s Endgame Phase 11B inventory packing integration', () => {
  it('migrates schema v16 fail-closed, validates resource/equipment references, and rejects future v18', () => {
    const legacy = structuredClone(bundledConfigJson) as unknown as Record<string, unknown>
    legacy.schemaVersion = 16
    delete legacy.inventory
    const untouched = structuredClone(legacy)
    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(untouched)
    expect(migrated).toMatchObject({
      schemaVersion: 17,
      inventory: { enabled: false, itemDefinitions: [] },
    })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()

    const danglingResource = config()
    danglingResource.inventory.itemDefinitions[0].content = { kind: 'resource', resourceId: 'missing' }
    expect(() => validateEmpiresConfig(danglingResource)).toThrow(/inventory.*unknown resource/i)
    const danglingEquipment = config()
    danglingEquipment.inventory.itemDefinitions.push({
      id: 'packing-equipment-test',
      name: 'Проверка экипировки',
      cells: [{ x: 0, y: 0 }],
      weight: 1,
      content: { kind: 'equipment', equipmentId: 'missing' },
      deferredReason: 'Только проверка ссылки.',
    })
    expect(() => validateEmpiresConfig(danglingEquipment)).toThrow(/inventory.*unknown equipment/i)
    const oversizedShape = config()
    oversizedShape.inventory.itemDefinitions[0].cells.push({ x: 0, y: 9 })
    expect(() => validateEmpiresConfig(oversizedShape)).toThrow(/cannot fit.*cart/i)
    const uncoveredExpedition = config()
    uncoveredExpedition.expeditions.definitions[0].provisionResourceId = 'wood'
    expect(() => validateEmpiresConfig(uncoveredExpedition)).toThrow(/live item.*wood/i)
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 18 })).toThrow(/future.*18/i)
  })

  it('consumes packed item ownership once, retains unpacked provisions, and rejects stale or duplicate results', () => {
    const value = config()
    value.inventory.board.cartHeight = 2
    const game = engine(value)
    const { ids, required } = beginPlanning(game)
    const foodBefore = southFood(game)
    expect(game.beginExpeditionPacking(EXPEDITION_ID, ids, required)).toMatchObject({ ok: true })
    const session = activeInventory(game)
    const result = resolveInventoryWithPolicy(session.plan, session.seed, 'center-stack')
    expect(result.outcome).toBe('failure')
    expect(result.packedItemInstanceIds.length).toBeGreaterThan(0)
    expect(result.unpackedItemInstanceIds.length).toBeGreaterThan(0)

    const stale = structuredClone(result)
    stale.packedItemInstanceIds = []
    expect(game.resolveMinigame(stale)).toMatchObject({ ok: false, message: expect.stringMatching(/replay/i) })
    expect(southFood(game)).toBe(foodBefore)
    expect(game.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(foodBefore - southFood(game)).toBe(result.packedProvisionAmount)
    const settled = game.snapshot()
    expect(game.state.expeditions.byDefinitionId[EXPEDITION_ID]).toMatchObject({
      status: 'ready',
      provisionPlan: {
        source: 'packing',
        withdrawnAmount: result.packedProvisionAmount,
        packedItemInstanceIds: result.packedItemInstanceIds,
        packingEfficiencyPercent: result.efficiencyPercent,
        packingScore: result.score,
      },
    })
    expect(game.resolveMinigame(result)).toMatchObject({ ok: true, message: expect.stringMatching(/already resolved/i) })
    expect(game.snapshot()).toEqual(settled)
  })

  it('restarts immutable runtime on reload and aborts the whole attempt without deleting provisions', () => {
    const game = engine()
    const { ids, required } = beginPlanning(game)
    const foodBefore = southFood(game)
    const daysBefore = game.state.empire.daysRemaining
    expect(game.beginExpeditionPacking(EXPEDITION_ID, ids, required)).toMatchObject({ ok: true })
    const original = activeInventory(game)
    expect(empiresConfigReplacementDisabledReason(game.state)).toMatch(/нельзя менять правила/i)
    const staleLifecycle = game.snapshot()
    if (staleLifecycle.minigame?.kind !== 'inventory') throw new Error('Expected Inventory snapshot.')
    staleLifecycle.minigame.plan.rosterUnitInstanceIds = ['stale-unit-instance']
    expect(() => new EmpiresEndgameEngine(game.config, staleLifecycle)).toThrow(/lifecycle/i)
    const changedRules = config()
    changedRules.inventory.gravity.intervalTicks += 1
    expect(() => new EmpiresEndgameEngine(changedRules, game.snapshot())).toThrow(/rules|config/i)
    const restored = new EmpiresEndgameEngine(
      game.config,
      importEmpiresCampaign(exportEmpiresCampaign(game.snapshot()), game.config.id),
    )
    const restarted = activeInventory(restored)
    expect(restarted.plan).toEqual(original.plan)
    expect(restarted.seed).toBe(original.seed)
    expect(restarted.attempt).toBe(original.attempt + 1)
    expect(restored.abortMinigame([], 0)).toMatchObject({ ok: true })
    expect(southFood(restored)).toBe(foodBefore)
    expect(restored.state.empire.daysRemaining).toBe(daysBefore - 3)
    expect(restored.state.expeditions.byDefinitionId[EXPEDITION_ID]).toMatchObject({
      status: 'aborted',
      resultHistory: [expect.objectContaining({ outcome: 'aborted', provisionWithdrawn: 0 })],
    })
  })

  it('keeps the authored direct-provision skip path and completes pack → launch → assault → settlement', () => {
    const skipped = engine()
    const direct = beginPlanning(skipped)
    expect(skipped.launchExpedition(EXPEDITION_ID, direct.ids, direct.required)).toMatchObject({ ok: true })
    expect(skipped.state.expeditions.byDefinitionId[EXPEDITION_ID].provisionPlan).toMatchObject({
      source: 'direct',
      packingEfficiencyPercent: 100,
      packingSessionId: null,
    })

    const value = config()
    value.td.planVariants.find(item => item.id === 'desert-fort-expedition-assault')!.objective.maxHp = 1
    const game = engine(value)
    const packed = beginPlanning(game)
    expect(game.beginExpeditionPacking(EXPEDITION_ID, packed.ids, packed.required)).toMatchObject({ ok: true })
    const inventory = activeInventory(game)
    const inventoryResult = resolveInventoryWithPolicy(inventory.plan, inventory.seed, 'spread')
    expect(inventoryResult.outcome).toBe('completed')
    expect(game.resolveMinigame(inventoryResult)).toMatchObject({ ok: true })
    expect(game.startExpeditionAssault(EXPEDITION_ID)).toMatchObject({ ok: true })
    const td = game.state.minigame as EmpiresTdMinigameSession
    expect(td.kind).toBe('td')
    const battle = replayTdBattle(td.plan, td.seed, [])
    expect(battle.outcome).toBe('victory')
    expect(game.resolveMinigame(battle)).toMatchObject({ ok: true })
    expect(game.state.expeditions.byDefinitionId[EXPEDITION_ID]).toMatchObject({
      status: 'won',
      zoneApplied: true,
      resultHistory: [expect.objectContaining({ outcome: 'victory' })],
    })
  })
})
