import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { exportEmpiresCampaign, importEmpiresCampaign } from './persistence'
import { replayTdBattle } from './td/engine'
import type {
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresMinigameSession,
  TdBattleResult,
} from './types'

const EXPEDITION_ID = 'expedition-south-fortress'

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(bundledConfigJson)
  value.empire.eventChance = 0
  value.expeditions.definitions[0].fullProvisionDeathChance = 0
  value.expeditions.definitions[0].emptyProvisionDeathChance = 0
  return value
}

function expeditionEngine(value = config(), unitCount = 2): EmpiresEndgameEngine {
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.empire.daysRemaining = 20
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  const city = state.empire.cities.find(candidate => candidate.regionId === 'south')!
  for (const candidate of state.empire.cities.filter(item => item.regionId === 'south')) {
    candidate.resources.food = candidate.id === city.id ? 100_000 : 0
  }
  const unitInstanceIds = Array.from({ length: unitCount }, (_, index) => `phase11-unit-${index + 1}`)
  city.recruitedUnitCohorts.push({
    id: 'phase11-cohort',
    unitId: 'unit-light',
    loadoutId: 'phase11-loadout',
    count: unitCount,
    unitInstanceIds,
    weaponEquipmentId: 'weapon-mace',
    weapon: { damageLevels: { impact: 100 }, tags: ['mace', 'phase11'] },
    armor: null,
  })
  for (const id of unitInstanceIds) {
    state.army.unitInstances[id] = {
      id,
      cityId: city.id,
      cohortId: 'phase11-cohort',
      unitId: 'unit-light',
      healthRatio: 1,
      veteran: false,
      wounds: 0,
      recoveryStartedAtCon: null,
      readyAtCon: state.con,
    }
  }
  state.army.nextUnitSequence = unitCount + 1
  return new EmpiresEndgameEngine(value, state)
}

function southFood(engine: EmpiresEndgameEngine): number {
  return engine.state.empire.cities
    .filter(city => city.regionId === 'south')
    .reduce((total, city) => total + (city.resources.food ?? 0), 0)
}

function planAndLaunch(engine: EmpiresEndgameEngine, installments = 1): string[] {
  expect(engine.beginExpeditionPlanning(EXPEDITION_ID)).toMatchObject({ ok: true })
  const view = engine.expeditionPlanningView(EXPEDITION_ID)!
  const ids = view.roster.filter(unit => unit.eligible).map(unit => unit.unitInstanceId)
  const required = ids.reduce((total, id) => {
    const unit = view.roster.find(candidate => candidate.unitInstanceId === id)!
    return total + unit.foodPerCon * view.effectiveDurationCons
  }, 0)
  const launched = engine.launchExpedition(EXPEDITION_ID, ids, required, installments)
  if (!launched.ok) throw new Error(launched.message)
  return ids
}

function activeTd(engine: EmpiresEndgameEngine): Extract<EmpiresMinigameSession, { kind: 'td' }> {
  if (engine.state.minigame?.kind !== 'td') throw new Error('Expedition TD did not start.')
  return engine.state.minigame
}

function settleSynthetic(
  engine: EmpiresEndgameEngine,
  outcome: TdBattleResult['outcome'],
  healthRatio: number,
): void {
  const session = activeTd(engine)
  const result = {
    kind: 'td',
    sessionId: session.id,
    planId: session.plan.id,
    planDigest: '',
    rulesIdentity: session.rulesIdentity,
    seed: session.seed,
    outcome,
    terminalReason: outcome === 'victory' ? 'objective-destroyed' : 'all-deployments-lost',
    ticks: 1,
    objectiveHp: outcome === 'victory' ? 0 : 1,
    objectiveMaxHp: 1,
    enemiesSpawned: 0,
    enemiesDefeated: 0,
    deployments: session.plan.deployments.map(deployment => ({
      deploymentId: deployment.id,
      cohortId: deployment.cohortId,
      cityId: deployment.cityId,
      unitId: deployment.unitId,
      deployed: deployment.count,
      survived: deployment.count,
      healthRatio,
    })),
    buildResourcesRemaining: 0,
    equipmentSpent: {},
    damageByType: {},
    hitCount: 0,
    commandLog: [],
    commandDigest: '',
  } as TdBattleResult
  ;(engine as unknown as {
    settleBattleOutcome: (result: TdBattleResult, session: Extract<EmpiresMinigameSession, { kind: 'td' }>) => void
  }).settleBattleOutcome(result, session)
}

describe('Empire\'s Endgame Phase 11 expeditions', () => {
  it('migrates v15 fail-closed, preserves the fortress position, and rejects dangling live links', () => {
    const legacy = structuredClone(bundledConfigJson) as unknown as Record<string, unknown>
    legacy.schemaVersion = 15
    delete legacy.expeditions
    const empire = legacy.empire as Record<string, unknown>
    const map = empire.map as Record<string, unknown>
    const objects = map.objects as Array<Record<string, unknown>>
    const fortress = objects.find(object => object.id === 'map-south-fortress')!
    const position = structuredClone(fortress.position)
    delete fortress.payload
    fortress.properties = { visualKind: 'fortress' }
    const untouched = structuredClone(legacy)

    const migrated = migrateEmpiresConfig(legacy)

    expect(legacy).toEqual(untouched)
    expect(migrated).toMatchObject({
      schemaVersion: 19,
      clash: { enabled: false },
      expeditions: { enabled: false, definitions: [] },
    })
    expect(migrated.empire.map.objects.find(object => object.id === 'map-south-fortress')).toMatchObject({
      id: 'map-south-fortress',
      position,
      kind: 'fortress',
      payload: { kind: 'fortress', expeditionId: null, zoneId: null },
    })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()
    const dangling = config()
    const typedFort = dangling.empire.map.objects.find(object => object.id === 'map-south-fortress')!
    if (typedFort.kind !== 'fortress') throw new Error('Fixture fortress lost its typed payload.')
    typedFort.payload.zoneId = 'missing-zone'
    expect(() => validateEmpiresConfig(dangling)).toThrow(/fortress|expedition|zone/i)
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 20 })).toThrow(/future.*20/i)
  })

  it('withdraws direct provisions once, spends preparation days, restores safely, and retries without refund', () => {
    const engine = expeditionEngine()
    const foodBefore = southFood(engine)
    const daysBefore = engine.state.empire.daysRemaining
    const ids = planAndLaunch(engine)
    const expedition = engine.state.expeditions.byDefinitionId[EXPEDITION_ID]
    expect(expedition).toMatchObject({
      status: 'ready',
      rosterUnitInstanceIds: ids,
      provisionPlan: { paidInstallments: 1, installmentCount: 1 },
    })
    expect(foodBefore - southFood(engine)).toBe(expedition.provisionPlan!.withdrawnAmount)
    expect(engine.state.empire.daysRemaining).toBe(daysBefore - 3)

    const restored = new EmpiresEndgameEngine(
      engine.config,
      importEmpiresCampaign(exportEmpiresCampaign(engine.snapshot()), engine.config.id),
    )
    expect(southFood(restored)).toBe(southFood(engine))
    expect(restored.abortExpedition(EXPEDITION_ID)).toMatchObject({ ok: true })
    const foodAfterAbort = southFood(restored)
    expect(restored.beginExpeditionPlanning(EXPEDITION_ID)).toMatchObject({ ok: true })
    expect(restored.cancelExpeditionPlanning(EXPEDITION_ID)).toMatchObject({ ok: true })
    expect(southFood(restored)).toBe(foodAfterAbort)
    expect(restored.state.expeditions.byDefinitionId[EXPEDITION_ID].resultHistory).toHaveLength(1)
  })

  it('uses Supply Corps installments and logistics/map timing without duplicate withdrawals', () => {
    const engine = expeditionEngine()
    engine.state.empire.flags.expeditionProvisionInstallmentTurns = 4
    engine.state.empire.flags.expeditionSpeedPercent = 25
    engine.state.empire.flags.logisticsMapBonusPercent = 100
    engine.state.empire.flags.worldMaps = 1
    const view = engine.expeditionPlanningView(EXPEDITION_ID)!
    expect(view).toMatchObject({
      effectiveDurationCons: 1,
      maxInstallments: 4,
      enemyIntel: 'exact',
      mapBonusPercent: 100,
      speedPercent: 25,
      veteranDeploymentSpeedPercent: 10,
    })
    expect(view.enemyGroups).toEqual([
      expect.objectContaining({ id: 'south-unarmored-raiders', count: 8, armorClassId: null }),
      expect.objectContaining({ id: 'south-crocodile-hide', count: 3, armorClassId: 'leather' }),
    ])
    const foodBefore = southFood(engine)
    planAndLaunch(engine, 2)
    const expedition = engine.state.expeditions.byDefinitionId[EXPEDITION_ID]
    expect(expedition).toMatchObject({ status: 'provisioning', provisionPlan: { paidInstallments: 1 } })
    expect(foodBefore - southFood(engine)).toBe(expedition.provisionPlan!.requestedAmount / 2)
    expect(engine.payExpeditionInstallment(EXPEDITION_ID)).toMatchObject({ ok: false })
    engine.state.con += 1
    expect(engine.payExpeditionInstallment(EXPEDITION_ID)).toMatchObject({ ok: true })
    expect(expedition).toMatchObject({ status: 'ready', provisionPlan: { paidInstallments: 2 } })
    expect(foodBefore - southFood(engine)).toBe(expedition.provisionPlan!.requestedAmount)

    const militaryLogistics = engine.config.empire.technologies.find(item => item.id === 'tech-military-logistics')!
    const supplyCorps = engine.config.empire.technologies.find(item => item.id === 'tech-supply-corps')!
    const maps = engine.config.cards.find(item => item.id === 'card-spades-3')!
    expect(militaryLogistics.effects).toContainEqual({ kind: 'flag', flagId: 'expeditionSpeedPercent', amount: 25 })
    expect(supplyCorps.effects).toContainEqual({ kind: 'flag', flagId: 'expeditionProvisionInstallmentTurns', amount: 4 })
    expect(maps.normal.deferredReason).toBeUndefined()
    expect(maps.inverted.deferredReason).toBeUndefined()
  })

  it('runs the real expedition TD path and settles zone, reward, complaint, and replay guard once', () => {
    const value = config()
    value.expeditions.definitions[0].complaint.launches = 1
    value.expeditions.definitions[0].rewards = [{ kind: 'resource', resourceId: 'gold', amount: 7 }]
    const variant = value.td.planVariants.find(item => item.id === 'desert-fort-expedition-assault')!
    variant.objective.maxHp = 1
    const engine = expeditionEngine(value)
    const goldBefore = engine.state.empire.resources.gold ?? 0
    const knowledgeBefore = engine.state.empire.resources.knowledge ?? 0
    const zone = value.expeditions.zones.find(item => item.id === 'zone-south-beyond-dunes')!
    const zoneGold = zone.rewards.find(effect => effect.kind === 'resource' && effect.resourceId === 'gold')
    const zoneKnowledge = zone.rewards.find(
      effect => effect.kind === 'resource' && effect.resourceId === 'knowledge',
    )
    planAndLaunch(engine)
    expect(engine.startExpeditionAssault(EXPEDITION_ID)).toMatchObject({ ok: true })
    const session = activeTd(engine)
    const result = replayTdBattle(session.plan, session.seed, [])
    expect(result.outcome).toBe('victory')
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    const settled = engine.snapshot()
    const expedition = engine.state.expeditions.byDefinitionId[EXPEDITION_ID]
    expect(expedition).toMatchObject({
      status: 'won',
      outcome: 'victory',
      rewardApplied: true,
      zoneApplied: true,
      resultHistory: [expect.objectContaining({ complaintApplied: true, rewardApplied: true })],
    })
    expect(engine.state.expeditions.openedZoneIds).toEqual(['zone-south-beyond-dunes'])
    expect(engine.state.empire.resources.gold).toBe(
      goldBefore + 7 + (zoneGold?.kind === 'resource' ? zoneGold.amount : 0),
    )
    expect(engine.state.empire.resources.knowledge).toBe(
      knowledgeBefore + (zoneKnowledge?.kind === 'resource' ? zoneKnowledge.amount : 0),
    )
    expect(engine.state.quests['quest-expedition-south-complaint']).toBeDefined()
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true, message: expect.stringMatching(/already resolved/i) })
    expect(engine.snapshot()).toEqual(settled)
  })

  it('promotes at the exact half-health boundary and removes a veteran on the distinct second wound', () => {
    const engine = expeditionEngine(config(), 1)
    const [unitId] = planAndLaunch(engine)
    expect(engine.startExpeditionAssault(EXPEDITION_ID)).toMatchObject({ ok: true })
    settleSynthetic(engine, 'defeat', 0.5)
    expect(engine.state.army.unitInstances[unitId]).toMatchObject({ veteran: true, wounds: 1, healthRatio: 0.5 })

    const unit = engine.state.army.unitInstances[unitId]
    unit.healthRatio = 1
    unit.recoveryStartedAtCon = null
    unit.readyAtCon = engine.state.con
    expect(engine.beginExpeditionPlanning(EXPEDITION_ID)).toMatchObject({ ok: true })
    const view = engine.expeditionPlanningView(EXPEDITION_ID)!
    expect(engine.launchExpedition(EXPEDITION_ID, [unitId], view.provisionRequired, 1)).toMatchObject({ ok: true })
    expect(engine.startExpeditionAssault(EXPEDITION_ID)).toMatchObject({ ok: true })
    const veteranSession = activeTd(engine)
    const variant = engine.config.td.planVariants.find(item => item.id === 'desert-fort-expedition-assault')!
    expect(veteranSession.plan.deployments[0].speedPerSecond).toBeCloseTo(
      variant.deploymentSpeedPerSecond * 1.1,
    )
    settleSynthetic(engine, 'defeat', 0.9)
    expect(engine.state.army.unitInstances[unitId]).toBeUndefined()
    expect(engine.state.expeditions.byDefinitionId[EXPEDITION_ID].resultHistory.at(-1))
      .toMatchObject({ removedVeteranUnitInstanceIds: [unitId] })
    expect(engine.config.expeditions.veteran).toMatchObject({
      qualifyingMaximumHealthRatio: 0.5,
      removalWounds: 2,
      laterBattleBonus: { kind: 'deploymentSpeedPercent', percent: 10 },
    })
  })

  it('keeps bounded result history and restores canonical roster identities', () => {
    const value = config()
    value.expeditions.resultHistoryRetention = 1
    const engine = expeditionEngine(value, 1)
    const [unitId] = planAndLaunch(engine)
    expect(engine.abortExpedition(EXPEDITION_ID)).toMatchObject({ ok: true })
    expect(engine.beginExpeditionPlanning(EXPEDITION_ID)).toMatchObject({ ok: true })
    const view = engine.expeditionPlanningView(EXPEDITION_ID)!
    expect(engine.launchExpedition(EXPEDITION_ID, [unitId], view.provisionRequired, 1)).toMatchObject({ ok: true })
    expect(engine.abortExpedition(EXPEDITION_ID)).toMatchObject({ ok: true })
    const expedition = engine.state.expeditions.byDefinitionId[EXPEDITION_ID]
    expect(expedition.resultHistory).toHaveLength(1)
    expect(expedition.resultCompaction).toMatchObject({ evictedCount: 1, historyDigest: expect.any(String) })
    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.army.unitInstances[unitId].id).toBe(unitId)
    expect(restored.state.expeditions.byDefinitionId[EXPEDITION_ID].resultCompaction)
      .toEqual(expedition.resultCompaction)
  })
})
