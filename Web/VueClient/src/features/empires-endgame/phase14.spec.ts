import { beforeAll, describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from './config'
import {
  createClashPlan,
  createClashRulesIdentity,
} from './clash/engine'
import { resolveClashWithPolicy } from './clash/qa'
import { AUTHENTIC_V17_V18_RULES_IDENTITIES } from './compatibility/authentic-v17-v18-rules.fixture'
import { EmpiresEndgameEngine } from './engine'
import { createEmpiresQaScenarios } from './qa'
import { EMPIRES_STABILIZATION_BUDGETS } from './stabilization'
import { createTdRulesIdentity } from './td/engine'
import type {
  ClashPlan,
  ClashRulesIdentity,
  EmpiresCampaignState,
  EmpiresClashMinigameSession,
  EmpiresEndgameConfig,
  EmpiresTdMinigameSession,
  TdBattlePlan,
} from './types'

function clone<T>(value: T): T {
  return structuredClone(value)
}

function config(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(bundledConfigJson)
}

function settlementConfig(value: EmpiresEndgameConfig): Omit<EmpiresEndgameConfig, 'seed'> {
  const { seed: _initializationSeed, ...rules } = clone(value)
  return rules
}

function currentSharedRetention(value: EmpiresEndgameConfig) {
  return {
    td: value.td.resultLogLimit ?? 32,
    alchemy: value.alchemy.resultLogLimit,
    inventory: value.inventory.resultLogLimit,
    clash: value.clash.resultLogLimit,
    chess: value.chess.resultLogLimit,
    saveUtf8Bytes: EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes,
  }
}

function currentClashRulesIdentity(value: EmpiresEndgameConfig): ClashRulesIdentity {
  return createClashRulesIdentity(value.schemaVersion, value.clash, {
    expeditions: value.expeditions,
    cities: value.empire.cities,
    loyalty: value.empire.loyalty,
    quests: value.quests,
    settlementConfig: settlementConfig(value),
    sharedResultRetention: currentSharedRetention(value),
  })
}

function currentTdRulesIdentity(value: EmpiresEndgameConfig) {
  return createTdRulesIdentity(value.schemaVersion, value.combat, value.td, {
    technologies: value.empire.technologies,
    units: value.empire.units ?? [],
    buildings: value.empire.buildings,
    steelResearch: value.empire.steelResearch,
    medical: value.empire.medical,
    loyalty: value.empire.loyalty,
    expeditions: value.expeditions,
    quests: value.quests,
    settlementConfig: settlementConfig(value),
    sharedResultRetention: currentSharedRetention(value),
  })
}

function legacyV17TdRulesIdentity(value: EmpiresEndgameConfig) {
  const projected = clone(value) as unknown as Record<string, unknown>
  projected.schemaVersion = 17
  delete projected.clash
  delete projected.chess
  const expeditions = projected.expeditions as EmpiresEndgameConfig['expeditions']
  expeditions.definitions = expeditions.definitions.map((definition) => {
    const legacy = clone(definition) as unknown as Record<string, unknown>
    delete legacy.battleMode
    delete legacy.clashVariantId
    return legacy as unknown as typeof definition
  })
  const projectedSettlement = clone(projected)
  delete projectedSettlement.seed
  return createTdRulesIdentity(17, value.combat, value.td, {
    technologies: value.empire.technologies,
    units: value.empire.units ?? [],
    buildings: value.empire.buildings,
    steelResearch: value.empire.steelResearch,
    medical: value.empire.medical,
    loyalty: value.empire.loyalty,
    expeditions,
    quests: value.quests,
    settlementConfig: projectedSettlement,
    sharedResultRetention: {
      td: value.td.resultLogLimit ?? 32,
      alchemy: value.alchemy.resultLogLimit,
      inventory: value.inventory.resultLogLimit,
      saveUtf8Bytes: EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes,
    },
  })
}

function legacyV18ClashRulesIdentity(value: EmpiresEndgameConfig): ClashRulesIdentity {
  const projected = clone(value) as unknown as Record<string, unknown>
  projected.schemaVersion = 18
  delete projected.chess
  delete projected.seed
  return createClashRulesIdentity(18, value.clash, {
    expeditions: value.expeditions,
    cities: value.empire.cities,
    loyalty: value.empire.loyalty,
    quests: value.quests,
    settlementConfig: projected,
    sharedResultRetention: {
      td: value.td.resultLogLimit ?? 32,
      alchemy: value.alchemy.resultLogLimit,
      inventory: value.inventory.resultLogLimit,
      clash: value.clash.resultLogLimit,
      saveUtf8Bytes: EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes,
    },
  })
}

function legacyV17TdState(value: EmpiresEndgameConfig): EmpiresCampaignState {
  const state = new EmpiresEndgameEngine(value).snapshot()
  const identity = legacyV17TdRulesIdentity(value)
  const variant = value.td.planVariants?.find(candidate => (
    candidate.mode === 'defense' && !candidate.deferredReason
  ))
  const battlefield = variant
    ? value.td.battlefields.find(candidate => candidate.id === variant.battlefieldId)
    : null
  const wave = variant
    ? value.td.waves.find(candidate => candidate.id === variant.waveId)
    : null
  if (!variant || !battlefield || !wave) throw new Error('Phase 14 TD fixture content is unavailable.')
  const sequence = state.minigameResultCompaction.settledThroughSequence + 1
  const seed = 'phase14-legacy-v17-td'
  const planId = 'phase14-legacy-v17-td-plan'
  const sessionId = `ee:${sequence}:${planId}:${seed}`
  const plan: TdBattlePlan = {
    id: planId,
    sessionId,
    rulesIdentity: clone(identity),
    mode: 'defense',
    scheduledCon: state.con,
    threat: 0,
    tickMs: value.td.tickMs!,
    maxTicks: value.td.maxTicks!,
    maxCommands: value.td.maxCommands!,
    maxCatchUpTicksPerFrame: value.td.maxCatchUpTicksPerFrame!,
    startingBuildResources: variant.startingBuildResources ?? value.td.startingBuildResources!,
    battlefield: clone(battlefield),
    objective: clone(variant.objective),
    towerBases: clone(value.td.towerBases!.filter(base => battlefield.towerBaseIds.includes(base.id))),
    towerChoices: clone(value.td.towers),
    gradeChoices: clone(value.td.gradeChoices!.filter(set => set.regionId === battlefield.regionId)),
    wave: clone(wave),
    combat: clone(value.combat),
    equipmentStock: {},
    deployments: [],
  }
  const session: EmpiresTdMinigameSession = {
    id: sessionId,
    sequence,
    kind: 'td',
    plan,
    rulesIdentity: clone(identity),
    seed,
    attempt: 0,
    origin: {
      returnPhase: 'cards',
      context: { kind: 'manual', sourceId: 'phase14:legacy-v17-td' },
    },
  }
  state.phase = 'minigame'
  state.minigame = session
  return state
}

function deterministicPlan(
  value: EmpiresEndgameConfig,
  identity: ClashRulesIdentity,
  sessionId: string,
  planId: string,
  defenderWins = false,
  campaignUnit?: {
    instanceId: string
    cityId: string
    cohortId: string
    unitId: string
  },
): ClashPlan {
  const definitions = value.clash.roster.filter(unit => (
    !unit.deferredReason && !unit.reviewReason
      && unit.attack !== null && unit.maxHp !== null && unit.speed !== null
  ))
  if (definitions.length < 2) throw new Error('Phase 14 needs two live Clash definitions.')
  const field = value.clash.fieldVariants.find(candidate => !candidate.deferredReason)
  const region = value.clash.regions.find(candidate => !candidate.deferredReason)
  if (!field || !region) throw new Error('Phase 14 needs one live Clash field and region.')
  const plan = createClashPlan({
    id: planId,
    sessionId,
    rulesIdentity: identity,
    config: value.clash,
    field,
    region,
    roster: [
      {
        instanceId: 'phase14-attacker',
        definitionId: definitions[0].id,
        side: 'attacker',
        ...(campaignUnit
          ? {
              campaignUnitInstanceId: campaignUnit.instanceId,
              cityId: campaignUnit.cityId,
              cohortId: campaignUnit.cohortId,
              unitId: campaignUnit.unitId,
            }
          : {}),
      },
      {
        instanceId: 'phase14-defender',
        definitionId: definitions[1].id,
        side: 'defender',
      },
    ],
  })
  plan.field.terrainCellIds = []
  plan.maxTurns = Math.max(8, plan.maxTurns)
  plan.maxCommands = Math.max(16, plan.maxCommands)
  for (const definition of plan.units) {
    const wins = definition.id === definitions[defenderWins ? 1 : 0].id
    definition.attack = wins ? 9 : 0
    definition.maxHp = wins ? 9 : 1
    definition.speed = wins ? 9 : 1
    definition.passives = []
    definition.abilities = []
  }
  return plan
}

function activeClashState(
  value: EmpiresEndgameConfig,
  suffix: string,
  options: { defenderWins?: boolean; campaignUnit?: boolean } = {},
): { state: EmpiresCampaignState; session: EmpiresClashMinigameSession; campaignUnitId: string | null } {
  const state = new EmpiresEndgameEngine(value).snapshot()
  const sequence = state.minigameResultCompaction.settledThroughSequence + 1
  const seed = `phase14-${suffix}`
  const planId = `phase14-${suffix}-plan`
  const sessionId = `ee:${sequence}:${planId}:${seed}`
  const identity = currentClashRulesIdentity(value)
  let campaignUnit: Parameters<typeof deterministicPlan>[6]
  if (options.campaignUnit) {
    const city = state.empire.cities[0]
    const cohortId = `phase14-${suffix}-cohort`
    const instanceId = `phase14-${suffix}-campaign-unit`
    const unitId = 'unit-light'
    city.recruitedUnitCohorts.push({
      id: cohortId,
      unitId,
      loadoutId: `phase14-${suffix}-loadout`,
      count: 1,
      unitInstanceIds: [instanceId],
      weaponEquipmentId: 'weapon-mace',
      weapon: { damageLevels: { impact: 1 }, tags: ['phase14'] },
      armor: null,
    })
    state.army.unitInstances[instanceId] = {
      id: instanceId,
      cityId: city.id,
      cohortId,
      unitId,
      healthRatio: 1,
      veteran: false,
      wounds: 0,
      recoveryStartedAtCon: null,
      readyAtCon: state.con,
    }
    campaignUnit = { instanceId, cityId: city.id, cohortId, unitId }
  }
  const plan = deterministicPlan(
    value,
    identity,
    sessionId,
    planId,
    options.defenderWins,
    campaignUnit,
  )
  const session: EmpiresClashMinigameSession = {
    id: sessionId,
    sequence,
    kind: 'clash',
    plan,
    rulesIdentity: clone(identity),
    seed,
    turnLog: [],
    attempt: 0,
    origin: {
      returnPhase: 'cards',
      context: { kind: 'manual', sourceId: `phase14:${suffix}` },
    },
  }
  state.phase = 'minigame'
  state.minigame = clone(session)
  return { state, session, campaignUnitId: campaignUnit?.instanceId ?? null }
}

describe('Empire\'s Endgame Phase 14 Clash integration', () => {
  let value: EmpiresEndgameConfig
  let qaFixtures: ReturnType<typeof createEmpiresQaScenarios>

  beforeAll(() => {
    value = config()
    qaFixtures = createEmpiresQaScenarios(value, { seed: 'phase14-authentic-compatibility' })
  })

  it('migrates schema v17 through fail-closed Clash and Chess defaults and rejects future configs', () => {
    const legacy = clone(bundledConfigJson) as unknown as Record<string, unknown>
    legacy.schemaVersion = 17
    delete legacy.clash
    delete legacy.chess
    const expeditions = legacy.expeditions as { definitions: Array<Record<string, unknown>> }
    for (const definition of expeditions.definitions) {
      delete definition.battleMode
      delete definition.clashVariantId
    }
    const untouched = clone(legacy)

    const migrated = migrateEmpiresConfig(legacy) as EmpiresEndgameConfig

    expect(legacy).toEqual(untouched)
    expect(migrated).toMatchObject({
      schemaVersion: 19,
      clash: { enabled: false },
      chess: { enabled: false },
    })
    expect(migrated.clash).toEqual(bundledConfigJson.clash)
    expect(migrated.expeditions.definitions.every(definition => (
      definition.battleMode === 'td' && definition.clashVariantId === null
    ))).toBe(true)
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()

    const migratedCustom = clone(migrated)
    migratedCustom.clash.assaultRoutes[0].sourceId = 'custom-config-without-canonical-route'
    expect(() => validateEmpiresConfig(migratedCustom)).not.toThrow()

    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 20 }))
      .toThrow(/future.*20/i)
  })

  it('upgrades immutable save-v16 TD and save-v17 Clash sessions during reload', () => {
    const tdState = legacyV17TdState(value)
    tdState.schemaVersion = 16 as typeof tdState.schemaVersion
    if (tdState.minigame?.kind !== 'td') throw new Error('Phase 14 TD fixture is unavailable.')

    const restoredTd = new EmpiresEndgameEngine(value, tdState)
    expect((restoredTd.state.minigame as EmpiresTdMinigameSession).rulesIdentity)
      .toEqual(currentTdRulesIdentity(value))

    const clashFixture = activeClashState(value, 'legacy-v18')
    clashFixture.state.schemaVersion = 17 as typeof clashFixture.state.schemaVersion
    const legacyClashIdentity = legacyV18ClashRulesIdentity(value)
    clashFixture.state.minigame!.rulesIdentity = clone(legacyClashIdentity)
    clashFixture.state.minigame!.plan.rulesIdentity = clone(legacyClashIdentity)

    const restoredClash = new EmpiresEndgameEngine(value, clashFixture.state)
    expect((restoredClash.state.minigame as EmpiresClashMinigameSession).rulesIdentity)
      .toEqual(currentClashRulesIdentity(value))
  })

  it('upgrades authentic bundled v17 and v18 active minigames without rebinding them to stale current rules', () => {
    const cases = [
      { fixture: 'battle-defense', kind: 'td' },
      { fixture: 'mystic-tavern', kind: 'tavern' },
      { fixture: 'alchemy-experiment', kind: 'alchemy' },
      { fixture: 'inventory-packing', kind: 'inventory' },
    ] as const

    for (const testCase of cases) {
      const currentState = clone(qaFixtures[testCase.fixture].snapshot)
      const currentSession = currentState.minigame
      if (!currentSession || currentSession.kind !== testCase.kind) {
        throw new Error(`${testCase.fixture} has no ${testCase.kind} session.`)
      }

      for (const boundary of [
        {
          configSchemaVersion: 17 as const,
          saveSchemaVersion: 16,
          identity: AUTHENTIC_V17_V18_RULES_IDENTITIES[17][testCase.kind],
        },
        {
          configSchemaVersion: 18 as const,
          saveSchemaVersion: 17,
          identity: AUTHENTIC_V17_V18_RULES_IDENTITIES[18][testCase.kind],
        },
      ]) {
        const legacyState = clone(currentState)
        legacyState.schemaVersion = boundary.saveSchemaVersion as EmpiresCampaignState['schemaVersion']
        const legacySession = legacyState.minigame
        if (!legacySession || legacySession.kind !== testCase.kind) {
          throw new Error(`${testCase.fixture} lost its ${testCase.kind} session.`)
        }
        const mutableIdentity = legacySession as unknown as {
          rulesIdentity: { configSchemaVersion: number; rulesDigest: string }
          plan: { rulesIdentity: { configSchemaVersion: number; rulesDigest: string } }
        }
        mutableIdentity.rulesIdentity = clone(boundary.identity)
        mutableIdentity.plan.rulesIdentity = clone(boundary.identity)
        const untouched = clone(legacyState)

        let restored: EmpiresEndgameEngine
        try {
          restored = new EmpiresEndgameEngine(value, legacyState)
        } catch (error) {
          throw new Error(
            `${testCase.kind} v${boundary.configSchemaVersion}: ${String(error)}`,
          )
        }
        const restoredSession = restored.state.minigame
        if (!restoredSession || restoredSession.kind !== testCase.kind) {
          throw new Error(`${testCase.fixture} did not survive v${boundary.configSchemaVersion}.`)
        }

        expect(legacyState, `${testCase.kind} v${boundary.configSchemaVersion} input mutation`)
          .toEqual(untouched)
        expect(restoredSession.rulesIdentity, `${testCase.kind} v${boundary.configSchemaVersion}`)
          .toEqual(currentSession.rulesIdentity)
      }
    }

    const clashFixture = activeClashState(value, 'authentic-v18')
    clashFixture.state.schemaVersion = 17 as EmpiresCampaignState['schemaVersion']
    const clashSession = clashFixture.state.minigame
    if (!clashSession || clashSession.kind !== 'clash') {
      throw new Error('Authentic v18 Clash fixture is unavailable.')
    }
    clashSession.rulesIdentity = clone(AUTHENTIC_V17_V18_RULES_IDENTITIES[18].clash)
    clashSession.plan.rulesIdentity = clone(AUTHENTIC_V17_V18_RULES_IDENTITIES[18].clash)
    const restoredClash = new EmpiresEndgameEngine(value, clashFixture.state)
    expect(restoredClash.state.minigame?.rulesIdentity).toEqual(currentClashRulesIdentity(value))

    const staleConfig = clone(value)
    staleConfig.empire.eventChance = staleConfig.empire.eventChance === 0 ? 0.5 : 0
    const staleLegacy = clone(qaFixtures['battle-defense'].snapshot)
    staleLegacy.schemaVersion = 16 as EmpiresCampaignState['schemaVersion']
    if (staleLegacy.minigame?.kind !== 'td') throw new Error('Stale v17 TD fixture is unavailable.')
    staleLegacy.minigame.rulesIdentity = clone(AUTHENTIC_V17_V18_RULES_IDENTITIES[17].td)
    staleLegacy.minigame.plan.rulesIdentity = clone(AUTHENTIC_V17_V18_RULES_IDENTITIES[17].td)
    expect(() => new EmpiresEndgameEngine(staleConfig, staleLegacy))
      .toThrow(/active minigame rules identity does not match/i)
  })

  it('rejects a missing Clash journal and an over-budget restored plan', () => {
    const missingJournal = activeClashState(value, 'missing-journal').state
    delete (missingJournal.minigame as Partial<EmpiresClashMinigameSession>).turnLog
    expect(() => new EmpiresEndgameEngine(value, missingJournal)).toThrow(/turn log is missing/i)

    const overBudget = activeClashState(value, 'over-budget').state
    if (overBudget.minigame?.kind !== 'clash') throw new Error('Clash fixture is unavailable.')
    overBudget.minigame.plan.maxCommands = 513
    expect(() => new EmpiresEndgameEngine(value, overBudget)).toThrow(/safety ceiling/i)
  })

  it('begins a canonical Clash session, survives reload, and resolves by deterministic replay', () => {
    const fixture = activeClashState(value, 'reload')
    fixture.state.phase = fixture.session.origin.returnPhase
    fixture.state.minigame = null
    const game = new EmpiresEndgameEngine(value, fixture.state)

    expect(game.beginMinigame(fixture.session)).toMatchObject({ ok: true })
    const result = resolveClashWithPolicy(fixture.session.plan, fixture.session.seed, 'balanced')
    const partialTurnLog = result.turnLog.slice(0, -1)
    expect(partialTurnLog.length).toBeGreaterThan(0)
    expect(game.recordClashProgress(partialTurnLog)).toMatchObject({ ok: true })
    const restored = new EmpiresEndgameEngine(value, game.snapshot())
    const active = restored.state.minigame
    if (active?.kind !== 'clash') throw new Error('Reloaded Clash session is unavailable.')

    expect(active.turnLog).toEqual(partialTurnLog)
    expect(result).toMatchObject({ kind: 'clash', outcome: 'victory', winner: 'attacker' })
    expect(restored.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(restored.state.minigame).toBeNull()
    expect(restored.state.minigameResultLog.at(-1)).toMatchObject({
      sessionId: fixture.session.id,
      result: { commandDigest: result.commandDigest, outcome: 'victory' },
    })
  })

  it('requires terminal and abort journals to extend persisted Clash progress', () => {
    const fixture = activeClashState(value, 'journal-prefix')
    const game = new EmpiresEndgameEngine(value, fixture.state)
    const canonical = resolveClashWithPolicy(fixture.session.plan, fixture.session.seed, 'balanced')
    const divergent = resolveClashWithPolicy(fixture.session.plan, fixture.session.seed, 'aggressive')
    const persisted = canonical.turnLog.slice(0, 1)
    expect(divergent.turnLog[0]).not.toEqual(persisted[0])
    expect(game.recordClashProgress(persisted)).toMatchObject({ ok: true })

    const before = game.snapshot()
    expect(game.resolveMinigame(divergent)).toMatchObject({
      ok: false,
      message: expect.stringMatching(/extend the persisted turn log/i),
    })
    expect(game.abortMinigame(divergent.turnLog, divergent.turns)).toMatchObject({
      ok: false,
      message: expect.stringMatching(/extend the persisted turn log/i),
    })
    expect(game.snapshot()).toEqual(before)

    const continued = resolveClashWithPolicy(
      fixture.session.plan,
      fixture.session.seed,
      'balanced',
      persisted,
    )
    expect(continued.turnLog.slice(0, persisted.length)).toEqual(persisted)
    expect(game.resolveMinigame(continued)).toMatchObject({ ok: true })
  })

  it('rejects mismatched and stale Clash settlement, then applies canonical losses exactly once', () => {
    const fixture = activeClashState(value, 'settlement', {
      defenderWins: true,
      campaignUnit: true,
    })
    const game = new EmpiresEndgameEngine(value, fixture.state)
    const active = game.state.minigame
    if (active?.kind !== 'clash' || !fixture.campaignUnitId) {
      throw new Error('Campaign-linked Clash fixture is unavailable.')
    }
    const result = resolveClashWithPolicy(active.plan, active.seed, 'balanced')
    expect(result).toMatchObject({ outcome: 'defeat', winner: 'defender' })

    const mismatched = clone(result)
    mismatched.planId = `${result.planId}-other`
    const beforeMismatch = game.snapshot()
    expect(game.resolveMinigame(mismatched)).toMatchObject({
      ok: false,
      message: expect.stringMatching(/does not match/i),
    })
    expect(game.snapshot()).toEqual(beforeMismatch)

    game.state.army.unitInstances[fixture.campaignUnitId].cohortId = 'stale-cohort'
    const beforeStale = game.snapshot()
    expect(game.resolveMinigame(result)).toMatchObject({
      ok: false,
      message: expect.stringMatching(/stale/i),
    })
    expect(game.snapshot()).toEqual(beforeStale)
    game.state.army.unitInstances[fixture.campaignUnitId].cohortId = active.plan.roster[0].cohortId!

    expect(game.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(game.state.army.unitInstances[fixture.campaignUnitId]).toBeUndefined()
    expect(game.state.minigameResultLog).toHaveLength(1)
    const settled = game.snapshot()
    expect(game.resolveMinigame(result)).toMatchObject({
      ok: true,
      message: expect.stringMatching(/already resolved/i),
    })
    expect(game.snapshot()).toEqual(settled)
  })

  it('settles a Clash abort once and leaves duplicate abort requests inert', () => {
    const fixture = activeClashState(value, 'abort')
    const game = new EmpiresEndgameEngine(value, fixture.state)
    const moraleBefore = game.state.army.morale
    const threatBefore = game.state.external.allianceThreat

    expect(game.abortMinigame([], 0)).toMatchObject({ ok: true })
    expect(game.state.army.morale).toBe(Math.max(
      0,
      moraleBefore + value.clash.settlement.abortMoraleDelta,
    ))
    expect(game.state.external.allianceThreat).toBe(Math.max(
      0,
      threatBefore + value.clash.settlement.abortAllianceThreatDelta,
    ))
    expect(game.state.minigameResultLog).toHaveLength(1)
    expect(game.state.minigameResultLog[0].result).toMatchObject({
      kind: 'clash',
      outcome: 'aborted',
      terminalReason: 'aborted',
    })
    const aborted = game.snapshot()
    expect(game.abortMinigame([], 0)).toMatchObject({
      ok: true,
      message: expect.stringMatching(/already aborted/i),
    })
    expect(game.snapshot()).toEqual(aborted)
  })
})
