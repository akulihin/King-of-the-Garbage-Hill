import { beforeAll, describe, expect, it, vi } from 'vitest'
import bundledConfigJson from '../../../../public/empires-endgame/game-config.json'
import { validateEmpiresConfig } from '../config'
import {
  collectEmpiresConfigCarriers,
  summarizeEmpiresContentCoverage,
  type EmpiresContentCoverageManifest,
} from '../content-coverage'
import {
  EMPIRES_CONTENT_CARRIER_SNAPSHOT,
  EMPIRES_CONTENT_COVERAGE_MANIFEST,
} from '../content-coverage-manifest'
import { EmpiresEndgameEngine } from '../engine'
import { createAlchemyRulesIdentity } from '../alchemy/engine'
import { createInventoryRulesIdentity } from '../inventory/engine'
import {
  EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY,
  EMPIRES_SAVE_STORAGE_KEY,
  exportEmpiresCampaign,
  importEmpiresCampaign,
  loadEmpiresCampaign,
} from '../persistence'
import { createEmpiresQaScenarios } from '../qa'
import { questTriggerWasConsumed } from '../quests'
import {
  EMPIRES_STABILIZATION_BUDGETS,
  empiresUtf8ByteLength,
} from '../stabilization'
import { createTavernRulesIdentity } from '../tavern/engine'
import { createTdRulesIdentity, validateTdBattlePlan } from '../td/engine'
import type {
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresMinigameResult,
} from '../types'

function clone<T>(value: T): T {
  return structuredClone(value)
}

function config(): EmpiresEndgameConfig {
  return clone(bundledConfigJson) as EmpiresEndgameConfig
}

/**
 * Recreates the active-session shape emitted by the final schema-v15 engine
 * (commit 473015d4): non-canonical plan:seed IDs, no sequence/watermark, and
 * the exact rules-identity dependency sets that predate P13 stabilization.
 */
function legacyV15ActiveState(
  value: EmpiresEndgameConfig,
  source: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = clone(source)
  const session = state.minigame
  if (!session) throw new Error('Legacy active-session fixture is missing its minigame.')

  state.schemaVersion = 15 as typeof state.schemaVersion
  delete (session as Partial<typeof session>).sequence
  delete (state.minigameResultCompaction as Partial<
    typeof state.minigameResultCompaction
  >).settledThroughSequence
  delete (state.minigameResultCompaction as Partial<
    typeof state.minigameResultCompaction
  >).legacySettledSessionIds

  const legacySessionId = `${session.plan.id}:${String(session.seed)}`
  const previousSessionId = session.id
  session.id = legacySessionId
  session.plan.sessionId = legacySessionId

  if (session.kind === 'td') {
    const identity = createTdRulesIdentity(value.schemaVersion, value.combat, value.td, {
      technologies: value.empire.technologies,
      units: value.empire.units ?? [],
      buildings: value.empire.buildings,
      steelResearch: value.empire.steelResearch,
    })
    session.rulesIdentity = identity
    session.plan.rulesIdentity = clone(identity)
  } else if (session.kind === 'tavern') {
    const identity = createTavernRulesIdentity(value.schemaVersion, {
      tavern: value.tavern,
      mysticCards: value.mysticCards,
      units: (value.empire.units ?? []).map(unit => ({
        id: unit.id,
        deferredReason: unit.deferredReason,
        td: unit.td,
      })),
      combatEquipment: value.combat.equipment,
      godDeckMemory: value.god.deckMemory,
    })
    session.rulesIdentity = identity
    session.plan.rulesIdentity = clone(identity)
  } else if (session.kind === 'alchemy') {
    const identity = createAlchemyRulesIdentity(value.schemaVersion, value.alchemy, {
      buildings: value.empire.buildings,
      technologies: value.empire.technologies,
      epidemics: value.empire.epidemics,
    })
    session.rulesIdentity = identity
    session.plan.rulesIdentity = clone(identity)
  } else {
    const identity = createInventoryRulesIdentity(value.schemaVersion, value.inventory, {
      resources: value.empire.resources,
      equipment: value.combat.equipment,
      expeditions: value.expeditions.definitions,
    })
    session.rulesIdentity = identity
    session.plan.rulesIdentity = clone(identity)
  }

  if (session.origin.context.kind === 'expedition-assault'
    || session.origin.context.kind === 'expedition-packing') {
    const expedition = state.expeditions.byDefinitionId[session.origin.context.expeditionId]
    if (expedition?.activeSessionId === previousSessionId) {
      expedition.activeSessionId = legacySessionId
    }
    if (session.origin.context.kind === 'expedition-packing'
      && expedition?.provisionPlan?.source === 'packing'
      && expedition.provisionPlan.packingSessionId === previousSessionId) {
      expedition.provisionPlan.packingSessionId = legacySessionId
    }
  }

  return state
}

function coverageProjectionFingerprint(manifest: EmpiresContentCoverageManifest): string {
  const projection = {
    sources: manifest.sourceInventory.map(source => [
      source.id,
      source.path,
      source.messageCount,
      source.role,
      source.evidence,
      source.messageIds,
      source.residualDisposition,
      source.residualOwner,
      source.residualConsumer,
      source.residualTestEvidence,
      source.residualDesignerQuestion ?? null,
    ]),
    raw: manifest.rawCatalogGroups.map(group => [
      group.id,
      group.disposition,
      group.rawSources,
      group.owner,
      group.consumer,
      group.testEvidence,
      group.stableIdentities,
      group.expectedLinkedConfigCarrierCount,
      group.linkedConfigCarrierKeys,
      group.designerQuestion ?? null,
    ]),
  }
  const input = JSON.stringify(projection)
  let hash = 0xcbf29ce484222325n
  for (let index = 0; index < input.length; index += 1) {
    hash ^= BigInt(input.charCodeAt(index))
    hash = BigInt.asUintN(64, hash * 0x100000001b3n)
  }
  return hash.toString(16).padStart(16, '0')
}

describe('Empire\'s Endgame Phase 13 stabilization contracts', () => {
  let fixtures: ReturnType<typeof createEmpiresQaScenarios>

  beforeAll(() => {
    fixtures = createEmpiresQaScenarios(config(), { seed: 'phase13-contracts' })
  })

  it('freezes the final P12B carrier/raw inventory and maintained projection fingerprint', () => {
    const value = config()
    expect(collectEmpiresConfigCarriers(value)).toHaveLength(1_354)
    expect(Object.values(EMPIRES_CONTENT_CARRIER_SNAPSHOT).flat()).toHaveLength(1_354)
    expect(summarizeEmpiresContentCoverage(EMPIRES_CONTENT_COVERAGE_MANIFEST)).toEqual({
      config: {
        live: 1160,
        'ready-now': 0,
        'blocked-semantic': 120,
        'blocked-substrate': 11,
        review: 63,
        out: 0,
      },
      raw: {
        live: 67,
        'ready-now': 0,
        'blocked-semantic': 194,
        'blocked-substrate': 52,
        review: 133,
        out: 15,
      },
      configTotal: 1_354,
      rawTotal: 461,
      sourceTotal: 33,
      sourceMessageTotal: 1_149,
    })
    expect(coverageProjectionFingerprint(EMPIRES_CONTENT_COVERAGE_MANIFEST))
      .toBe('2750a78d8ecb4055')
  })

  it('restores every active minigame variant immutably and enforces its authored abort policy', () => {
    const cases = [
      { name: 'battle-defense', abortable: true },
      { name: 'battle-assault', abortable: true },
      { name: 'mystic-tavern', abortable: false },
      { name: 'alchemy-experiment', abortable: true },
      { name: 'inventory-packing', abortable: true },
    ] as const

    for (const testCase of cases) {
      const source = clone(fixtures[testCase.name].snapshot)
      const untouched = clone(source)
      const sourceSession = source.minigame
      if (!sourceSession) throw new Error(`${testCase.name} has no active minigame fixture.`)

      let engine: EmpiresEndgameEngine
      try {
        engine = new EmpiresEndgameEngine(config(), source)
      } catch (error) {
        const detail = error instanceof Error ? error.message : String(error)
        throw new Error(`${testCase.name} restore failed: ${detail}`, { cause: error })
      }
      const restored = engine.state.minigame
      if (!restored) throw new Error(`${testCase.name} did not restore its active minigame.`)
      expect(source).toEqual(untouched)
      expect(restored).toMatchObject({
        id: sourceSession.id,
        sequence: sourceSession.sequence,
        kind: sourceSession.kind,
        seed: sourceSession.seed,
        attempt: sourceSession.attempt + 1,
        origin: sourceSession.origin,
      })
      expect(restored.plan).toEqual(sourceSession.plan)
      expect(restored.rulesIdentity).toEqual(sourceSession.rulesIdentity)

      const beforeAbort = engine.snapshot()
      const abort = engine.abortMinigame([], 0)
      if (!testCase.abortable) {
        expect(abort).toMatchObject({
          ok: false,
          message: expect.stringMatching(/no authored abort consequence/i),
        })
        expect(engine.snapshot()).toEqual(beforeAbort)
        continue
      }

      expect(abort).toMatchObject({ ok: true })
      expect(engine.state.minigame).toBeNull()
      expect(engine.state.phase).toBe(sourceSession.origin.returnPhase)
      expect(engine.state.minigameResultLog.at(-1)).toMatchObject({
        sessionId: sourceSession.id,
        sequence: sourceSession.sequence,
        result: { outcome: 'aborted' },
      })
      const settled = engine.snapshot()
      expect(engine.abortMinigame([], 0)).toMatchObject({
        ok: true,
        message: expect.stringMatching(/already aborted/i),
      })
      expect(engine.snapshot()).toEqual(settled)
    }
  })

  it('upgrades every schema-v15 active session once and survives a second current-schema restore', () => {
    const value = config()
    const cases = [
      'battle-defense',
      'battle-assault',
      'mystic-tavern',
      'alchemy-experiment',
      'inventory-packing',
    ] as const

    for (const name of cases) {
      const currentFixture = clone(fixtures[name].snapshot)
      const expectedCurrentSession = currentFixture.minigame
      if (!expectedCurrentSession) throw new Error(`${name} has no active minigame fixture.`)
      const legacy = legacyV15ActiveState(value, currentFixture)
      const untouched = clone(legacy)
      const legacySession = legacy.minigame
      if (!legacySession) throw new Error(`${name} lost its legacy active session.`)

      const firstImported = importEmpiresCampaign({
        schemaVersion: 15,
        savedAt: '2026-07-20T00:00:00.000Z',
        state: legacy,
      }, value.id)
      const first = new EmpiresEndgameEngine(value, firstImported)
      const firstSession = first.state.minigame
      if (!firstSession) throw new Error(`${name} did not survive its legacy restore.`)

      expect(legacy, `${name} legacy input mutation`).toEqual(untouched)
      expect(firstSession.id, `${name} canonical ID`).toBe(expectedCurrentSession.id)
      expect(firstSession.sequence, `${name} canonical sequence`).toBe(expectedCurrentSession.sequence)
      expect(firstSession.plan, `${name} current plan identity`).toEqual(expectedCurrentSession.plan)
      expect(firstSession.rulesIdentity, `${name} current rules identity`)
        .toEqual(expectedCurrentSession.rulesIdentity)
      expect(firstSession.rulesIdentity, `${name} actually exercised legacy identity upgrade`)
        .not.toEqual(legacySession.rulesIdentity)
      expect(firstSession.attempt, `${name} first restart attempt`).toBe(legacySession.attempt + 1)

      const currentEnvelope = exportEmpiresCampaign(first.snapshot())
      expect(currentEnvelope).toMatchObject({ schemaVersion: 18, state: { schemaVersion: 18 } })
      const second = new EmpiresEndgameEngine(
        value,
        importEmpiresCampaign(currentEnvelope, value.id),
      )
      const secondSession = second.state.minigame
      if (!secondSession) throw new Error(`${name} did not survive its current restore.`)
      expect(secondSession.id, `${name} stable second ID`).toBe(firstSession.id)
      expect(secondSession.sequence, `${name} stable second sequence`).toBe(firstSession.sequence)
      expect(secondSession.plan, `${name} stable second plan`).toEqual(firstSession.plan)
      expect(secondSession.rulesIdentity, `${name} stable second rules`).toEqual(firstSession.rulesIdentity)
      expect(secondSession.attempt, `${name} second restart attempt`).toBe(firstSession.attempt + 1)
    }
  })

  it('rejects stale settlement and retention dependencies for every active minigame identity', () => {
    const staleTd = config()
    staleTd.empire.medical.defaultBattleRecoveryCons += 1
    expect(() => new EmpiresEndgameEngine(
      staleTd,
      clone(fixtures['battle-defense'].snapshot),
    )).toThrow(/active minigame rules identity does not match/i)

    const staleTavern = config()
    staleTavern.empire.domesticEconomy.goldResourceId = staleTavern.empire.foodResourceId
    expect(() => new EmpiresEndgameEngine(
      staleTavern,
      clone(fixtures['mystic-tavern'].snapshot),
    )).toThrow(/active minigame rules identity does not match/i)

    const staleAlchemy = config()
    staleAlchemy.quests.triggerHistoryRetention -= 1
    expect(() => new EmpiresEndgameEngine(
      staleAlchemy,
      clone(fixtures['alchemy-experiment'].snapshot),
    )).toThrow(/active minigame rules identity does not match/i)

    const staleInventory = config()
    staleInventory.expeditions.resultHistoryRetention -= 1
    expect(() => new EmpiresEndgameEngine(
      staleInventory,
      clone(fixtures['inventory-packing'].snapshot),
    )).toThrow(/active minigame rules identity does not match/i)

    const staleSharedRetention = config()
    staleSharedRetention.inventory.resultLogLimit -= 1
    expect(() => new EmpiresEndgameEngine(
      staleSharedRetention,
      clone(fixtures['battle-defense'].snapshot),
    )).toThrow(/active minigame rules identity does not match/i)
  })

  it('binds a final-day expedition TD session to the complete empire settlement rules', () => {
    const value = config()
    const expeditionId = 'expedition-south-fortress'
    const source = new EmpiresEndgameEngine(value, clone(fixtures['expedition-planning'].snapshot))
    expect(source.beginExpeditionPlanning(expeditionId)).toMatchObject({ ok: true })
    const planning = source.expeditionPlanningView(expeditionId)
    const roster = planning?.selectedUnitInstanceIds ?? []
    if (!planning || roster.length === 0) throw new Error('Final-day expedition TD fixture is unavailable.')
    expect(source.launchExpedition(
      expeditionId,
      roster,
      planning.provisionRequired,
      1,
    )).toMatchObject({ ok: true })
    source.state.empire.daysRemaining = 0
    expect(source.startExpeditionAssault(expeditionId)).toMatchObject({ ok: true })

    const missingOrigin = source.snapshot()
    delete (missingOrigin.minigame as Partial<NonNullable<typeof missingOrigin.minigame>>).origin
    expect(() => new EmpiresEndgameEngine(value, missingOrigin))
      .toThrow(/active minigame origin is missing or malformed/i)

    const wrongLifecycle = source.snapshot()
    if (wrongLifecycle.minigame?.origin.context.kind !== 'expedition-assault') {
      throw new Error('Final-day expedition TD origin fixture is unavailable.')
    }
    wrongLifecycle.minigame.origin.context.attempt += 1
    expect(() => new EmpiresEndgameEngine(value, wrongLifecycle))
      .toThrow(/active expedition assault does not match its persisted lifecycle/i)

    const changedSettlement = config()
    changedSettlement.empire.eventChance = changedSettlement.empire.eventChance === 0 ? 0.5 : 0
    expect(() => new EmpiresEndgameEngine(changedSettlement, source.snapshot()))
      .toThrow(/active minigame rules identity does not match/i)
  })

  it('rejects incompatible current Inventory and Alchemy origin lifecycles', () => {
    const value = config()
    const wrongInventoryKind = clone(fixtures['inventory-packing'].snapshot)
    if (wrongInventoryKind.minigame?.kind !== 'inventory') {
      throw new Error('Inventory origin fixture is unavailable.')
    }
    wrongInventoryKind.minigame.origin = {
      returnPhase: 'empire',
      context: {
        kind: 'alchemy-experiment',
        cityId: wrongInventoryKind.empire.cities[0]?.id ?? 'city-center-1',
        recipeId: 'forged-recipe',
        con: wrongInventoryKind.con,
      },
    }
    expect(() => new EmpiresEndgameEngine(value, wrongInventoryKind))
      .toThrow(/origin context is incompatible|Alchemy origin does not match/i)

    const wrongInventoryLifecycle = clone(fixtures['inventory-packing'].snapshot)
    if (wrongInventoryLifecycle.minigame?.origin.context.kind !== 'expedition-packing') {
      throw new Error('Inventory lifecycle fixture is unavailable.')
    }
    wrongInventoryLifecycle.minigame.origin.context.attempt += 1
    expect(() => new EmpiresEndgameEngine(value, wrongInventoryLifecycle))
      .toThrow(/active expedition packing does not match its persisted lifecycle/i)

    const wrongAlchemyPlan = clone(fixtures['alchemy-experiment'].snapshot)
    if (wrongAlchemyPlan.minigame?.kind !== 'alchemy'
      || wrongAlchemyPlan.minigame.origin.context.kind !== 'alchemy-experiment') {
      throw new Error('Alchemy origin fixture is unavailable.')
    }
    wrongAlchemyPlan.minigame.origin.context.recipeId = 'forged-recipe'
    expect(() => new EmpiresEndgameEngine(value, wrongAlchemyPlan))
      .toThrow(/active Alchemy origin does not match its immutable plan/i)
  })

  it('round-trips representative live runtime lifecycle states without drift', () => {
    const value = config()
    const states: Array<{ name: string, state: ReturnType<EmpiresEndgameEngine['snapshot']> }> = [
      { name: 'rebellion-recovery', state: clone(fixtures['loyalty-rebellion'].snapshot) },
      { name: 'epidemic', state: clone(fixtures['epidemic-outbreak'].snapshot) },
      { name: 'external-offers', state: clone(fixtures['external-trade'].snapshot) },
      { name: 'quest-dialogue', state: clone(fixtures['quest-dialogue'].snapshot) },
      { name: 'expedition-planning', state: clone(fixtures['expedition-planning'].snapshot) },
      { name: 'pending-target', state: clone(fixtures['target-city-resources'].snapshot) },
      { name: 'pending-event', state: clone(fixtures.event.snapshot) },
    ]

    const domestic = new EmpiresEndgameEngine(value, clone(fixtures['domestic-economy'].snapshot))
    const economy = value.empire.domesticEconomy
    const insuranceCity = domestic.state.empire.cities.find(city => (
      (city.operationalBuildingLevels[economy.insurance.buildingId] ?? 0) > 0
    ))
    const fairCity = domestic.state.empire.cities.find(city => (
      (city.operationalBuildingLevels[economy.fair.buildingId] ?? 0) > 0
    ))
    if (!insuranceCity || !fairCity || !economy.fair.actions[0]) {
      throw new Error('Domestic lifecycle fixture is incomplete.')
    }
    expect(domestic.startInsurance(insuranceCity.id)).toMatchObject({ ok: true })
    expect(domestic.performFairAction(fairCity.id, economy.fair.actions[0].id)).toMatchObject({ ok: true })
    states.push({ name: 'loan-insurance-fair', state: domestic.snapshot() })

    const god = new EmpiresEndgameEngine(value, clone(fixtures['anti-bito'].snapshot))
    expect(god.endAttack('player')).toMatchObject({ ok: true })
    expect(god.state.god.interventions.length).toBeGreaterThan(0)
    expect(god.state.god.dialogueLog.length).toBeGreaterThan(0)
    states.push({ name: 'god-intervention-line', state: god.snapshot() })

    const governance = new EmpiresEndgameEngine(value, clone(fixtures.governance.snapshot))
    const advisor = value.governance.advisors.find(candidate => (
      governance.state.governance.advisors[candidate.id]?.status === 'awaiting-judgment'
      && !governance.advisorTransitionBlockedReason(candidate.id, 'pardon')
    ))
    const perst = value.governance.persts[0]
    const regionId = value.governance.governor.regionIds[0]
    if (!advisor || !perst || !regionId) throw new Error('Governance lifecycle fixture is incomplete.')
    expect(governance.transitionAdvisor(advisor.id, 'pardon')).toMatchObject({ ok: true })
    expect(governance.assignGovernor(perst.id, regionId)).toMatchObject({ ok: true })
    states.push({ name: 'advisor-perst', state: governance.snapshot() })

    const mystic = clone(fixtures['empire-council-with-points'].snapshot)
    const queenId = value.tavern.queen.mysticDefinitionId
    mystic.mystics.instances[queenId] = {
      id: queenId,
      definitionId: queenId,
      owner: 'player',
      inverted: true,
      status: 'zone',
      spawnedAtCon: mystic.con,
      returnAtCon: null,
      lastChangedCon: mystic.con,
    }
    mystic.mystics.zone = [queenId]
    mystic.mystics.queenComboProgress = 3
    mystic.mystics.queenComboCompletedAtCon = mystic.con
    mystic.mystics.lastQueenPulseCon = mystic.con
    mystic.mystics.lastQueenPulseInstanceIds = []
    mystic.mystics.history = [{
      sequence: 1,
      con: mystic.con,
      kind: 'queen-pulse',
      sourceId: `queen-pulse:${queenId}:${mystic.con}`,
      instanceIds: [],
    }]
    mystic.mystics.nextHistorySequence = 2
    states.push({ name: 'mystic-queen-tick', state: mystic })

    for (const lifecycle of states) {
      const imported = importEmpiresCampaign(exportEmpiresCampaign(lifecycle.state), value.id)
      const restored = new EmpiresEndgameEngine(value, imported).snapshot()
      expect(restored, lifecycle.name).toEqual(lifecycle.state)
    }
  })

  it('preserves compacted minigame exact-once identity across export and import', () => {
    const value = config()
    const template = clone(fixtures['battle-defense'].snapshot.minigame)
    if (!template || template.kind !== 'td') throw new Error('TD compaction fixture is unavailable.')
    const engine = new EmpiresEndgameEngine(value, clone(fixtures['battle-defense'].snapshot))
    const limit = Math.min(
      value.td.resultLogLimit ?? 32,
      value.alchemy.resultLogLimit,
      value.inventory.resultLogLimit,
    )

    expect(engine.abortMinigame([], 0)).toMatchObject({ ok: true })
    const evictedResult = clone(engine.state.minigameResultLog[0].result)
    for (let sequence = 2; sequence <= limit + 2; sequence += 1) {
      const session = clone(template)
      session.sequence = sequence
      session.id = `ee:${sequence}:${session.plan.id}:${String(session.seed)}`
      session.plan.sessionId = session.id
      session.origin.returnPhase = engine.state.phase
      expect(engine.beginMinigame(session)).toMatchObject({ ok: true })
      expect(engine.abortMinigame([], 0)).toMatchObject({ ok: true })
    }

    expect(engine.state.minigameResultLog).toHaveLength(limit)
    expect(engine.state.minigameResultCompaction).toMatchObject({
      evictedCount: 2,
      settledThroughSequence: limit + 2,
      lastSessionId: `ee:2:${template.plan.id}:${String(template.seed)}`,
      lastRulesDigest: evictedResult.rulesIdentity.rulesDigest,
    })
    expect(engine.state.minigameResultCompaction.historyDigest).not.toBe('')
    expect(engine.state.minigameResultLog.some(record => (
      record.sessionId === evictedResult.sessionId
    ))).toBe(false)

    const imported = importEmpiresCampaign(
      exportEmpiresCampaign(engine.snapshot()),
      value.id,
    )
    const restored = new EmpiresEndgameEngine(value, imported)
    const beforeDuplicate = restored.snapshot()
    expect(restored.resolveMinigame(evictedResult as EmpiresMinigameResult)).toMatchObject({
      ok: true,
      message: expect.stringMatching(/already resolved/i),
    })
    expect(restored.snapshot()).toEqual(beforeDuplicate)
  })

  it('bounds ended epidemics and battle-loss identities while retaining compacted evidence', () => {
    const value = config()
    const epidemicState = clone(fixtures['epidemic-outbreak'].snapshot)
    const epidemic = epidemicState.epidemics[0]
    if (!epidemic) throw new Error('Epidemic compaction fixture is unavailable.')
    const epidemicCount = EMPIRES_STABILIZATION_BUDGETS.endedEpidemicRetention + 3
    epidemicState.epidemics = Array.from({ length: epidemicCount }, (_, index) => ({
      ...clone(epidemic),
      id: `phase13-epidemic:${index + 1}`,
      startedCon: 1,
      remainingStageDuration: 0,
      remainingDuration: 0,
      endedAtCon: index + 1,
      endReason: 'resolved' as const,
    }))
    epidemicState.epidemicCompaction = {
      evictedCount: 0,
      historyDigest: '',
      lastInstanceId: null,
      lastRulesDigest: null,
      maxEvictedSequence: 0,
    }
    epidemicState.nextEpidemicSequence = epidemicCount + 1

    const restoredEpidemics = new EmpiresEndgameEngine(value, epidemicState)
    expect(restoredEpidemics.state.epidemics)
      .toHaveLength(EMPIRES_STABILIZATION_BUDGETS.endedEpidemicRetention)
    expect(restoredEpidemics.state.epidemicCompaction).toMatchObject({
      evictedCount: 3,
      lastInstanceId: 'phase13-epidemic:3',
      lastRulesDigest: epidemic.rulesDigest,
    })
    expect(restoredEpidemics.state.epidemicCompaction.historyDigest).not.toBe('')

    const battleLosses = new EmpiresEndgameEngine(value)
    const cityId = battleLosses.state.empire.cities[0].id
    for (let index = 1;
      index <= EMPIRES_STABILIZATION_BUDGETS.recentBattleLossIdentityRetention + 1;
      index += 1) {
      expect(battleLosses.consumeBattleLoss({
        id: `phase13-legacy-loss:${index}`,
        target: { kind: 'city', cityId },
        deployed: 1,
        lost: 0,
      })).toBe(true)
    }
    expect(battleLosses.state.empire.loyalty.consumedBattleLossIds)
      .toHaveLength(EMPIRES_STABILIZATION_BUDGETS.recentBattleLossIdentityRetention)
    expect(battleLosses.state.empire.loyalty.battleLossCompaction).toMatchObject({
      evictedCount: 1,
      sealedLegacyIdentities: true,
    })
    expect(battleLosses.state.empire.loyalty.battleLossCompaction.historyDigest).not.toBe('')
    const sealed = battleLosses.snapshot()
    expect(battleLosses.consumeBattleLoss({
      id: 'phase13-legacy-loss:future',
      target: { kind: 'city', cityId },
      deployed: 1,
      lost: 0,
    })).toBe(false)
    expect(battleLosses.snapshot()).toEqual(sealed)
  })

  it('rejects schema-v16 snapshots that erase or contradict durable compaction evidence', () => {
    const value = config()
    const cases: Array<{
      name: string
      mutate(state: ReturnType<EmpiresEndgameEngine['snapshot']>): void
      message: RegExp
    }> = [
      {
        name: 'minigame settlement watermark',
        mutate: state => delete (state.minigameResultCompaction as Partial<
          typeof state.minigameResultCompaction
        >).settledThroughSequence,
        message: /minigame compaction state is missing or malformed/i,
      },
      {
        name: 'epidemic high-water mark',
        mutate: state => delete (state.epidemicCompaction as Partial<
          typeof state.epidemicCompaction
        >).maxEvictedSequence,
        message: /epidemic compaction evidence is missing or malformed/i,
      },
      {
        name: 'battle-loss evidence',
        mutate: state => delete (state.empire.loyalty as Partial<
          typeof state.empire.loyalty
        >).battleLossCompaction,
        message: /battle-loss compaction evidence is missing or malformed/i,
      },
      {
        name: 'God dialogue evidence',
        mutate: state => delete (state.god as Partial<typeof state.god>).dialogueCompaction,
        message: /God dialogue compaction is missing or malformed/i,
      },
      {
        name: 'expedition result evidence',
        mutate: (state) => {
          const expedition = Object.values(state.expeditions.byDefinitionId)[0]
          delete (expedition as Partial<typeof expedition>).resultCompaction
        },
        message: /expedition .* missing result compaction state/i,
      },
    ]

    for (const testCase of cases) {
      const malformed = new EmpiresEndgameEngine(value).snapshot()
      testCase.mutate(malformed)
      expect(
        () => new EmpiresEndgameEngine(value, malformed),
        testCase.name,
      ).toThrow(testCase.message)
    }

    const settled = new EmpiresEndgameEngine(value, clone(fixtures['battle-defense'].snapshot))
    expect(settled.abortMinigame()).toMatchObject({ ok: true })
    const mismatchedResult = settled.snapshot()
    mismatchedResult.minigameResultLog[0].result.sessionId = 'different-session'
    expect(() => new EmpiresEndgameEngine(value, mismatchedResult))
      .toThrow(/result session identities are inconsistent/i)

    const canonicalLegacyTail = new EmpiresEndgameEngine(value).snapshot()
    canonicalLegacyTail.minigameResultCompaction.legacySettledSessionIds = ['ee:1:plan:seed']
    expect(() => new EmpiresEndgameEngine(value, canonicalLegacyTail))
      .toThrow(/legacy minigame identity tail contains a canonical session/i)
  })

  it('requires authoritative parent state in schema v16 while legacy snapshots may synthesize it', () => {
    const value = config()
    const cases: Array<{
      name: string
      mutate(state: ReturnType<EmpiresEndgameEngine['snapshot']>): void
      message: RegExp
    }> = [
      {
        name: 'quests',
        mutate: state => delete (state as Partial<typeof state>).quests,
        message: /requires quest state and quest runtime state/i,
      },
      {
        name: 'quest runtime',
        mutate: state => delete (state as Partial<typeof state>).questRuntime,
        message: /requires quest state and quest runtime state/i,
      },
      {
        name: 'expeditions',
        mutate: state => delete (state as Partial<typeof state>).expeditions,
        message: /requires expedition state/i,
      },
      {
        name: 'loyalty',
        mutate: state => delete (state.empire as Partial<typeof state.empire>).loyalty,
        message: /requires Empire loyalty state/i,
      },
    ]

    for (const testCase of cases) {
      const malformed = new EmpiresEndgameEngine(value).snapshot()
      testCase.mutate(malformed)
      expect(
        () => new EmpiresEndgameEngine(value, malformed),
        testCase.name,
      ).toThrow(testCase.message)
    }

    const legacy = new EmpiresEndgameEngine(value).snapshot()
    legacy.schemaVersion = 15 as typeof legacy.schemaVersion
    delete (legacy as Partial<typeof legacy>).quests
    delete (legacy as Partial<typeof legacy>).questRuntime
    delete (legacy as Partial<typeof legacy>).expeditions
    delete (legacy.empire as Partial<typeof legacy.empire>).loyalty
    const restored = new EmpiresEndgameEngine(value, legacy)
    expect(restored.state.quests).toEqual({})
    expect(restored.state.questRuntime).toBeDefined()
    expect(restored.state.expeditions).toBeDefined()
    expect(restored.state.empire.loyalty).toBeDefined()
  })

  it('retains exact-once quest coordinates after readable trigger identities are compacted', () => {
    const state = clone(Object.values(fixtures['quest-dialogue'].snapshot.quests)[0])
    if (!state) throw new Error('Quest compaction fixture is unavailable.')
    state.consumedTriggerIds = []
    state.compactedTriggerWatermarks = {
      'quest:phase13-trigger:trigger:minigameResult:ee': 7,
      'quest:phase13-trigger:trigger:conReached': 4,
    }
    state.sealedTriggerKinds = ['manual']

    expect(questTriggerWasConsumed(
      state,
      'quest:phase13-trigger:trigger:minigameResult:ee:7:plan:seed',
    )).toBe(true)
    expect(questTriggerWasConsumed(
      state,
      'quest:phase13-trigger:trigger:minigameResult:ee:8:plan:seed',
    )).toBe(false)
    expect(questTriggerWasConsumed(
      state,
      'quest:phase13-trigger:trigger:conReached:con:4',
    )).toBe(true)
    expect(questTriggerWasConsumed(
      state,
      'quest:phase13-trigger:trigger:manual:any-non-coordinate-source',
    )).toBe(true)
  })

  it('keeps public manual quest triggers exact-once after overflow and save/reload', () => {
    const value = config()
    value.quests.triggerHistoryRetention = 2
    const questId = 'quest-expedition-south-complaint'
    const choiceId = 'acknowledge-south-expedition-complaint'
    const engine = new EmpiresEndgameEngine(value)

    for (let attempt = 1; attempt <= 4; attempt += 1) {
      expect(engine.startManualQuest(
        questId,
        `expedition-complaint:expedition-south-fortress:${attempt}`,
      )).toMatchObject({ ok: true })
      expect(engine.advanceDialogue(questId, choiceId)).toMatchObject({ ok: true })
    }
    expect(engine.state.quests[questId]).toMatchObject({
      run: 4,
      compactedTriggerCount: 2,
      compactedTriggerWatermarks: {
        'quest:quest-expedition-south-complaint:trigger:manual:expedition-complaint:expedition-south-fortress': 2,
      },
    })

    const restored = new EmpiresEndgameEngine(
      value,
      importEmpiresCampaign(exportEmpiresCampaign(engine.snapshot()), value.id),
    )
    const beforeReplay = restored.snapshot()
    expect(restored.startManualQuest(
      questId,
      'expedition-complaint:expedition-south-fortress:1',
    )).toMatchObject({ ok: false })
    expect(restored.snapshot()).toEqual(beforeReplay)
    expect(restored.startManualQuest(
      questId,
      'expedition-complaint:expedition-south-fortress:5',
    )).toMatchObject({ ok: true })
  })

  it('preserves real expedition complaint and result watermarks through overflow and reload', () => {
    const value = config()
    const expeditionId = 'expedition-south-fortress'
    const questId = 'quest-expedition-south-complaint'
    const choiceId = 'acknowledge-south-expedition-complaint'
    const definition = value.expeditions.definitions.find(item => item.id === expeditionId)
    if (!definition) throw new Error('Expedition complaint definition is unavailable.')
    definition.preparationDays = 0
    definition.complaint.loyaltyDelta = 0
    value.expeditions.resultHistoryRetention = 1
    value.quests.triggerHistoryRetention = 1
    const engine = new EmpiresEndgameEngine(
      value,
      clone(createEmpiresQaScenarios(value, { seed: 'phase13-expedition-overflow' })
        ['expedition-planning'].snapshot),
    )

    const runAbortedAttempt = () => {
      expect(engine.beginExpeditionPlanning(expeditionId)).toMatchObject({ ok: true })
      const planning = engine.expeditionPlanningView(expeditionId)
      const roster = planning?.selectedUnitInstanceIds.slice(0, 1) ?? []
      if (!planning || roster.length !== 1) throw new Error('Expedition overflow roster is unavailable.')
      expect(engine.launchExpedition(expeditionId, roster, 0, 1)).toMatchObject({ ok: true })
      expect(engine.abortExpedition(expeditionId)).toMatchObject({ ok: true })
      if (engine.state.quests[questId]?.status === 'active') {
        expect(engine.advanceDialogue(questId, choiceId)).toMatchObject({ ok: true })
      }
    }

    for (let attempt = 1; attempt <= 66; attempt += 1) runAbortedAttempt()
    const expedition = engine.state.expeditions.byDefinitionId[expeditionId]
    expect(expedition).toMatchObject({
      assaultAttempts: 66,
      complaintThroughAttempt: 66,
      resultCompaction: {
        evictedCount: 65,
        maxEvictedAttempt: 65,
      },
    })
    expect(expedition.resultHistory.map(entry => entry.attempt)).toEqual([66])
    expect(expedition.complaintTriggerIds).toHaveLength(
      EMPIRES_STABILIZATION_BUDGETS.recentExpeditionComplaintRetention,
    )
    expect(expedition.complaintTriggerIds[0]).toBe(`expedition-complaint:${expeditionId}:3`)
    expect(engine.state.quests[questId]).toMatchObject({
      run: 65,
      compactedTriggerCount: 64,
    })

    const restored = new EmpiresEndgameEngine(
      value,
      importEmpiresCampaign(exportEmpiresCampaign(engine.snapshot()), value.id),
    )
    const restoredExpedition = restored.state.expeditions.byDefinitionId[expeditionId]
    expect(restoredExpedition.resultCompaction).toEqual(expedition.resultCompaction)
    expect(restoredExpedition.complaintThroughAttempt).toBe(66)
    const beforeReplay = restored.snapshot()
    expect(restored.startManualQuest(questId, `expedition-complaint:${expeditionId}:2`))
      .toMatchObject({ ok: false })
    expect(restored.snapshot()).toEqual(beforeReplay)
  })

  it('treats bundled cohorts as bounded simulation actors while separately capping roster IDs', () => {
    const session = clone(fixtures['battle-defense'].snapshot.minigame)
    if (!session || session.kind !== 'td' || !session.plan.deployments[0]) {
      throw new Error('TD roster ceiling fixture is unavailable.')
    }
    const plan = session.plan
    const deployment = plan.deployments[0]
    deployment.count = 300
    deployment.unitInstanceIds = Array.from({ length: 300 }, (_, index) => `phase13-unit:${index}`)
    expect(validateTdBattlePlan(plan)).not.toContain(
      'TD plan component or actor count exceeds the shipped safety ceiling',
    )

    deployment.count = EMPIRES_STABILIZATION_BUDGETS.maxRosterUnitInstances + 1
    deployment.unitInstanceIds = Array.from(
      { length: deployment.count },
      (_, index) => `phase13-overflow-unit:${index}`,
    )
    expect(validateTdBattlePlan(plan)).toContain(
      'TD plan component or actor count exceeds the shipped safety ceiling',
    )
  })

  it('runs final empire settlement after Inventory consumes the last preparation day', () => {
    const source = clone(fixtures['inventory-packing'].snapshot)
    source.empire.daysRemaining = 0
    const engine = new EmpiresEndgameEngine(config(), source)

    expect(engine.abortMinigame()).toMatchObject({ ok: true })
    expect(engine.state.minigame).toBeNull()
    expect(engine.state.phase).not.toBe('empire')
  })

  it('records a final-day settlement before the next con schedules a due wave', () => {
    const value = config()
    value.empire.eventChance = 0
    const source = clone(createEmpiresQaScenarios(value, {
      seed: 'phase13-final-day-wave',
    })['inventory-packing'].snapshot)
    const settledSession = source.minigame
    if (!settledSession || settledSession.kind !== 'inventory') {
      throw new Error('Final-day Inventory fixture is unavailable.')
    }
    source.empire.daysRemaining = 0
    source.external.nextWaveCon = source.con
    source.empire.resources[value.empire.foodResourceId] = 1_000_000_000
    for (const city of source.empire.cities) {
      city.resources[value.empire.foodResourceId] = 1_000_000_000
    }

    const engine = new EmpiresEndgameEngine(value, source)
    expect(engine.abortMinigame()).toMatchObject({ ok: true })
    expect(engine.state.minigameResultCompaction.settledThroughSequence)
      .toBe(settledSession.sequence)
    expect(engine.state.minigame).toMatchObject({
      kind: 'td',
      sequence: settledSession.sequence + 1,
    })
    expect(engine.state.phase).toBe('minigame')

    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.minigame).toMatchObject({
      kind: 'td',
      sequence: settledSession.sequence + 1,
      attempt: 1,
    })
  })

  it('rejects forged canonical battle-loss identities without consuming their durable slot', () => {
    const engine = new EmpiresEndgameEngine(config(), clone(fixtures['battle-defense'].snapshot))
    const session = engine.state.minigame
    if (!session || session.kind !== 'td' || !session.plan.deployments[0]) {
      throw new Error('TD battle-loss fixture is unavailable.')
    }
    const cityId = session.plan.deployments[0].cityId
    const before = engine.snapshot()
    expect(engine.consumeBattleLoss({
      id: `td-loss:minigame:${session.sequence + 1}:${cityId}`,
      target: { kind: 'city', cityId },
      deployed: 1,
      lost: 1,
    })).toBe(false)
    expect(engine.consumeBattleLoss({
      id: `td-loss:minigame:${session.sequence}:not-the-deployed-city`,
      target: { kind: 'city', cityId },
      deployed: 1,
      lost: 1,
    })).toBe(false)
    expect(engine.snapshot()).toEqual(before)
  })

  it('skips a corrupt newest local save and restores the next valid legacy key', () => {
    const value = config()
    const legacy = new EmpiresEndgameEngine(value).snapshot()
    legacy.schemaVersion = 15 as typeof legacy.schemaVersion
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, '{corrupt-json')
    window.localStorage.setItem(EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY, JSON.stringify({
      schemaVersion: 15,
      savedAt: '2026-07-21T00:00:00.000Z',
      state: legacy,
    }))
    try {
      expect(loadEmpiresCampaign(value.id)).toEqual(legacy)
      const oversizedCurrent = new EmpiresEndgameEngine(value).snapshot()
      oversizedCurrent.outcomeReason = 'x'.repeat(
        EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes,
      )
      window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, JSON.stringify({
        schemaVersion: 18,
        savedAt: '2026-07-21T00:00:00.000Z',
        state: oversizedCurrent,
      }))
      expect(loadEmpiresCampaign(value.id)).toEqual(legacy)
    } finally {
      warn.mockRestore()
      window.localStorage.removeItem(EMPIRES_SAVE_STORAGE_KEY)
      window.localStorage.removeItem(EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY)
    }
  })

  it('imports oversized legacy readable history, compacts it, and exports the bounded result', () => {
    const value = config()
    const source = new EmpiresEndgameEngine(value, clone(fixtures['battle-defense'].snapshot))
    expect(source.abortMinigame()).toMatchObject({ ok: true })
    const legacy = source.snapshot()
    const template = legacy.minigameResultLog[0]
    legacy.schemaVersion = 15 as typeof legacy.schemaVersion
    legacy.minigameResultLog = Array.from({ length: 1_000 }, (_, index) => {
      const record = clone(template)
      record.sessionId = `phase13-legacy-result:${index}`
      record.result.sessionId = record.sessionId
      return record
    })
    const envelope = {
      schemaVersion: 15,
      savedAt: '2026-07-21T00:00:00.000Z',
      state: legacy,
    }
    expect(empiresUtf8ByteLength(JSON.stringify(envelope)))
      .toBeGreaterThan(EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes)

    const imported = importEmpiresCampaign(envelope, value.id)
    const restored = new EmpiresEndgameEngine(value, imported)
    expect(restored.state.minigameResultLog.length).toBeLessThanOrEqual(
      Math.min(
        value.td.resultLogLimit ?? 32,
        value.alchemy.resultLogLimit,
        value.inventory.resultLogLimit,
      ),
    )
    expect(() => exportEmpiresCampaign(restored.snapshot())).not.toThrow()
  })

  it('rejects over-ceiling runtime budgets and oversized save exports/imports', () => {
    const overTd = config()
    overTd.td.maxCatchUpTicksPerFrame = EMPIRES_STABILIZATION_BUDGETS.maxCatchUpTicksPerFrame + 1
    expect(() => validateEmpiresConfig(overTd)).toThrow(/td\.maxCatchUpTicksPerFrame.*safety ceiling/i)

    const overAlchemy = config()
    overAlchemy.alchemy.maxTicks = EMPIRES_STABILIZATION_BUDGETS.maxTicks + 1
    expect(() => validateEmpiresConfig(overAlchemy)).toThrow(/alchemy\.maxTicks.*safety ceiling/i)

    const overInventory = config()
    overInventory.inventory.maxCommands = EMPIRES_STABILIZATION_BUDGETS.maxCommands + 1
    expect(() => validateEmpiresConfig(overInventory)).toThrow(/inventory\.maxCommands.*safety ceiling/i)

    const overTavern = config()
    overTavern.tavern.maxCommands = EMPIRES_STABILIZATION_BUDGETS.maxCommands + 1
    expect(() => validateEmpiresConfig(overTavern)).toThrow(/tavern\.maxCommands.*safety ceiling/i)

    const overHistory = config()
    overHistory.empire.loyalty.chronicleRetention = EMPIRES_STABILIZATION_BUDGETS.maxHistoryRetention + 1
    expect(() => validateEmpiresConfig(overHistory)).toThrow(/chronicleRetention.*safety ceiling/i)

    const unreachableComplaint = config()
    unreachableComplaint.expeditions.resultHistoryRetention = 1
    unreachableComplaint.expeditions.definitions[0].complaint.launches = 3
    expect(() => validateEmpiresConfig(unreachableComplaint))
      .toThrow(/resultHistoryRetention must retain at least 2 prior launches/i)

    const oversized = new EmpiresEndgameEngine(config()).snapshot()
    oversized.outcomeReason = 'x'.repeat(EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes)
    expect(() => exportEmpiresCampaign(oversized)).toThrow(/превышает лимит/i)
    expect(() => importEmpiresCampaign({
      schemaVersion: oversized.schemaVersion,
      savedAt: '2026-07-21T00:00:00.000Z',
      state: oversized,
    }, oversized.configId)).toThrow(/превышает лимит/i)
  })
})
