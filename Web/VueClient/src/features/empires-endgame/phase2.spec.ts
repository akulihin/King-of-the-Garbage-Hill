import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { exportEmpiresCampaign, importEmpiresCampaign } from './persistence'
import { createEmpiresQaScenarios } from './qa'
import { replayTdBattle } from './td/engine'
import { resolveTdWithPolicy } from './td/qa'
import type {
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresMinigameSession,
} from './types'

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function config(): EmpiresEndgameConfig {
  const value = cloneEmpiresConfig(defaultConfigJson)
  value.empire.eventChance = 0
  return value
}

function empireState(engine: EmpiresEndgameEngine, con = 1): EmpiresCampaignState {
  const state = engine.snapshot()
  state.phase = 'empire'
  state.con = con
  state.event = null
  state.minigame = null
  state.empire.daysRemaining = 59
  for (const resource of engine.config.empire.resources) {
    state.empire.resources[resource.id] = 1_000_000_000
  }
  for (const city of state.empire.cities) {
    city.resources[engine.config.empire.foodResourceId] = 1_000_000
  }
  return state
}

function battleSnapshot(
  value: EmpiresEndgameConfig,
  outcome: 'losses' | 'veterans',
): EmpiresCampaignState {
  const snapshot = clone(createEmpiresQaScenarios(value, { seed: `phase2-${outcome}` })['battle-defense'].snapshot)
  const session = snapshot.minigame!
  const deployment = session.plan.deployments[0]
  const group = session.plan.wave.groups[0]
  session.plan.maxTicks = 2_000
  group.count = 1
  group.startTick = 0
  group.spawnIntervalTicks = 1
  if (outcome === 'losses') {
    deployment.attackRange = 0.001
    group.maxHp = 1_000
    group.speedPerSecond = 200
    group.attackRange = 100
    group.attackIntervalTicks = 1
    group.weapon = { damageLevels: { impact: 1_000 }, tags: ['alliance'] }
  } else {
    deployment.attackRange = 1_000
    group.maxHp = 1
    group.speedPerSecond = 1
  }
  const city = snapshot.empire.cities.find(item => item.id === deployment.cityId)!
  const cohort = city.recruitedUnitCohorts.find(item => item.id === deployment.cohortId)
  if (!cohort) throw new Error('The QA deployment must reference its persisted equipment cohort.')
  cohort.count = deployment.count
  city.militaryPopulation = Math.max(100, city.militaryPopulation)
  return snapshot
}

function manualSession(value: EmpiresEndgameConfig): EmpiresMinigameSession {
  const source = createEmpiresQaScenarios(value, { seed: 'phase2-manual' })['battle-defense'].snapshot.minigame!
  const session = clone(source)
  session.id = 'manual-td-session'
  session.plan.id = 'manual-td-plan'
  session.plan.sessionId = session.id
  session.plan.deployments = []
  session.seed = 'manual-td-seed'
  session.attempt = 0
  session.origin = { returnPhase: 'cards', context: { kind: 'manual', sourceId: 'phase2-spec' } }
  return session
}

function putHeartsSevenInHand(state: EmpiresCampaignState, inverted: boolean): void {
  const cardId = 'card-hearts-7'
  const containers = [
    state.durak.deck,
    state.durak.playerHand,
    state.durak.godHand,
    state.durak.discard,
  ]
  const origin = containers.find(cards => cards.includes(cardId))
  if (!origin) throw new Error('Hearts-7 is not in a serializable card location.')
  origin.splice(origin.indexOf(cardId), 1)
  const displaced = state.durak.playerHand.shift()
  if (!displaced) throw new Error('The player hand needs a card to swap with Hearts-7.')
  origin.push(displaced)
  state.durak.playerHand.push(cardId)
  state.cards[cardId].inverted = inverted
}

describe('Empire\'s Endgame Phase 2 campaign bridge', () => {
  it('schedules each two-con Alliance wave once across active-session restores', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine, 2)
    state.external.nextWaveCon = 2
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state).toMatchObject({
      phase: 'minigame',
      con: 3,
      external: { nextWaveCon: 4, allianceThreat: 1 },
      minigame: { attempt: 0, origin: { context: { kind: 'alliance-wave', scheduledCon: 2 } } },
    })
    const saved = engine.snapshot()
    const restored = new EmpiresEndgameEngine(value, saved)
    expect(restored.state.minigame?.attempt).toBe(1)
    expect(restored.state.minigame?.plan).toEqual(saved.minigame?.plan)
    expect(restored.state.minigame?.seed).toBe(saved.minigame?.seed)
    expect(restored.state.external.nextWaveCon).toBe(4)

    const session = restored.state.minigame!
    expect(restored.resolveMinigame(resolveTdWithPolicy(session.plan, session.seed, 'passive')))
      .toMatchObject({ ok: true })
    const conThree = empireState(restored, 3)
    conThree.external.nextWaveCon = 4
    restored.restore(conThree)
    expect(restored.finishEmpire()).toMatchObject({ ok: true })
    expect(restored.state.phase).toBe('cards')
    expect(restored.state.external.nextWaveCon).toBe(4)

    const conFour = empireState(restored, 4)
    conFour.external.nextWaveCon = 4
    restored.restore(conFour)
    expect(restored.finishEmpire()).toMatchObject({ ok: true })
    expect(restored.state.phase).toBe('minigame')
    expect(restored.state.external.nextWaveCon).toBe(6)
    expect(restored.state.minigameResultLog).toHaveLength(1)
  })

  it('validates replay results, resolves once, aborts idempotently, and round-trips result logs', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    const session = manualSession(value)
    expect(engine.beginMinigame(session)).toMatchObject({ ok: true })
    const result = resolveTdWithPolicy(session.plan, session.seed, 'balanced')
    expect(engine.resolveMinigame({ ...result, objectiveHp: result.objectiveHp + 1 })).toEqual({
      ok: false,
      message: 'The minigame result failed deterministic replay validation.',
    })
    expect(engine.state.minigameResultLog).toHaveLength(0)
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(engine.state.minigameResultLog).toHaveLength(1)

    const abortSession = manualSession(value)
    abortSession.id = 'manual-abort-session'
    abortSession.plan.sessionId = abortSession.id
    expect(engine.beginMinigame(abortSession)).toMatchObject({ ok: true })
    expect(engine.abortMinigame()).toMatchObject({ ok: true })
    expect(engine.abortMinigame()).toMatchObject({ ok: true })
    expect(engine.state.minigameResultLog).toHaveLength(2)

    const envelope = exportEmpiresCampaign(engine.snapshot())
    const restored = new EmpiresEndgameEngine(
      value,
      importEmpiresCampaign(clone(envelope), value.id),
    )
    expect(restored.state.minigameResultLog).toEqual(engine.state.minigameResultLog)
  })

  it('migrates a genuine schema-v1 state into all additive Phase 2 homes', () => {
    const value = config()
    const legacy = clone(new EmpiresEndgameEngine(value).snapshot()) as unknown as Record<string, unknown>
    legacy.schemaVersion = 1
    delete legacy.minigame
    delete legacy.minigameResultLog
    delete legacy.army
    delete legacy.external
    const migrated = importEmpiresCampaign({
      schemaVersion: 1,
      savedAt: '2026-07-16T00:00:00.000Z',
      state: legacy,
    }, value.id)
    const restored = new EmpiresEndgameEngine(value, migrated)
    expect(restored.state).toMatchObject({
      schemaVersion: 6,
      minigame: null,
      minigameResultLog: [],
      army: {
        equipmentStock: {},
        morale: 0,
        maxMorale: 2,
        veterans: {},
        recruitmentPenalties: {},
        foundryInstantReadyConByCity: {},
        recoveries: [],
      },
      external: { allianceThreat: 0, nextWaveCon: 2, pendingOffers: [] },
      empire: {
        steelResearch: {
          branchCostMultipliers: {},
          branchEntries: [],
          delayedFree: {},
        },
      },
    })
    expect(restored.state.empire.cities.every(city => Array.isArray(city.recruitedUnitCohorts)))
      .toBe(true)
  })

  it('settles losses into units, recruitment, growth, loyalty pressure, and defeat consequences', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value, battleSnapshot(value, 'losses'))
    const session = engine.state.minigame!
    const deployment = session.plan.deployments[0]
    const city = engine.state.empire.cities.find(item => item.id === deployment.cityId)!
    const militaryBefore = city.militaryPopulation
    const loyaltyBefore = city.loyalty
    const result = replayTdBattle(session.plan, session.seed, [])
    expect(result).toMatchObject({
      outcome: 'defeat',
      terminalReason: 'objective-destroyed',
      deployments: [{ deployed: 3, survived: 0 }],
    })

    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(city.recruitedUnitCohorts.find(cohort => cohort.id === deployment.cohortId)).toBeUndefined()
    expect(city.militaryPopulation).toBe(militaryBefore - 3)
    expect(engine.state.army.recruitmentPenalties[`${city.id}:${deployment.unitId}`]).toBe(3)
    expect(city.loyalty).toBe(loyaltyBefore - 1)
    expect(engine.state.empire.chronicle.map(entry => entry.kind).slice(-2))
      .toEqual(['battle-loss', 'loyalty'])
    expect(engine.state.empire.loyalty.consumedBattleLossIds)
      .toContain(`td-loss:${session.id}:${city.id}`)
    expect(engine.state.external.allianceThreat).toBe(1)
  })

  it('promotes healthy survivors to veterans and applies victory morale', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value, battleSnapshot(value, 'veterans'))
    const session = engine.state.minigame!
    const result = replayTdBattle(session.plan, session.seed, [])
    expect(result).toMatchObject({
      outcome: 'victory',
      deployments: [{ deployed: 3, survived: 3, healthRatio: 1 }],
    })
    expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(Object.values(engine.state.army.veterans)).toHaveLength(3)
    expect(engine.state.army.morale).toBe(1)
  })

  it('applies the configured abort morale, threat, and deployed-unit recruitment penalty', () => {
    const value = config()
    const snapshot = battleSnapshot(value, 'veterans')
    snapshot.army.morale = 2
    const engine = new EmpiresEndgameEngine(value, snapshot)
    const deployment = engine.state.minigame!.plan.deployments[0]
    expect(engine.abortMinigame()).toMatchObject({ ok: true })
    expect(engine.state.army.morale).toBe(0)
    expect(engine.state.external.allianceThreat).toBe(2)
    expect(engine.state.army.recruitmentPenalties[`${deployment.cityId}:${deployment.unitId}`]).toBe(0.75)
  })
})

describe('Empire\'s Endgame Phase 2 army carriers', () => {
  it('keeps disabled-empty TD compatible and validates the enabled bundled 4x4 catalog', () => {
    const enabled = config()
    expect(() => validateEmpiresConfig(enabled)).not.toThrow()
    const disabled = cloneEmpiresConfig(enabled)
    disabled.td.enabled = false
    disabled.td.regionalCatalogEnabled = false
    disabled.td.towerBases = []
    disabled.td.battlefields = []
    disabled.td.towers = []
    disabled.td.gradeChoices = []
    disabled.td.waves = []
    disabled.td.planVariants = []
    expect(() => validateEmpiresConfig(disabled)).not.toThrow()
  })

  it('rejects enabled TD combat references and routes that cannot execute on its battlefield', () => {
    const unknownDamage = config()
    unknownDamage.td.towerBases![0].weapon.damageLevels.missing = 1
    expect(() => validateEmpiresConfig(unknownDamage)).toThrow(/unknown damage type missing/)

    const disconnectedRoute = config()
    disconnectedRoute.td.waves[0].groups[0].routeEdgeIds = ['central-road-2', 'central-road-3']
    expect(() => validateEmpiresConfig(disconnectedRoute)).toThrow(/not contiguous from the spawner/)

    const extraBattlefield = config()
    extraBattlefield.td.battlefields.push(clone(extraBattlefield.td.battlefields[0]))
    extraBattlefield.td.battlefields.at(-1)!.id = 'unused-battlefield'
    expect(() => validateEmpiresConfig(extraBattlefield)).toThrow(/regional catalog.*battlefield region set/)

    const missingAllianceField = config()
    delete (missingAllianceField.td.alliance as unknown as Record<string, unknown>).speedPerThreat
    expect(() => validateEmpiresConfig(missingAllianceField)).toThrow(/td\.alliance\.speedPerThreat/)

    const missingLoyaltyDelta = config()
    delete (missingLoyaltyDelta.td.settlement as unknown as Record<string, unknown>).loyaltyDelta
    expect(() => validateEmpiresConfig(missingLoyaltyDelta)).toThrow(/td\.settlement\.loyaltyDelta/)
  })

  it('reads every live barracks tier as equipped recruitment capacity', () => {
    const value = config()
    const capacities = [1_000, 2_500, 5_000, 9_000, 15_000]
    for (let index = 0; index < capacities.length; index += 1) {
      const engine = new EmpiresEndgameEngine(value)
      const state = empireState(engine)
      const city = state.empire.cities[0]
      city.loyalty = value.empire.loyalty.maximum
      city.buildingLevels['building-barracks'] = index + 1
      for (const classId of Object.keys(city.populationClasses)) city.populationClasses[classId] = 1_000_000
      engine.restore(state)
      expect(engine.effectiveOperationalBuildingLevel(city.id, 'building-barracks')).toBe(index + 1)
      expect(engine.cityRecruitmentRemaining(city.id)).toBe(capacities[index])
    }
  })

  it('uses ironwork to unlock smith operation and produces typed equipment stock', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine)
    for (const city of state.empire.cities) city.buildingLevels['building-smithy'] = 0
    const city = state.empire.cities[0]
    city.buildingLevels['building-smithy'] = 1
    for (const classId of Object.keys(city.populationClasses)) city.populationClasses[classId] = 1_000_000
    state.empire.researchedTechnologyIds = ['doctrine-general']
    engine.restore(state)
    expect(engine.effectiveOperationalBuildingLevel(city.id, 'building-smithy')).toBe(0)
    expect(engine.research('tech-ironwork')).toMatchObject({ ok: true })
    expect(engine.effectiveOperationalBuildingLevel(city.id, 'building-smithy')).toBe(1)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.army.equipmentStock['basic-kit']).toBe(5)
    expect(engine.state.army.equipmentStock['weapon-laurel-spear']).toBeUndefined()
  })

  it('researches the war doctrine, recruits all four equipped units, and deploys their TD profiles', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine, 2)
    state.external.nextWaveCon = 2
    const city = state.empire.cities[0]
    for (const candidate of state.empire.cities) candidate.buildingLevels['building-barracks'] = 0
    city.buildingLevels['building-barracks'] = 5
    for (const classId of Object.keys(city.populationClasses)) city.populationClasses[classId] = 1_000_000
    city.militaryPopulation = 100
    state.army.equipmentStock['basic-kit'] = 100
    state.army.equipmentStock['weapon-laurel-spear'] = 1
    state.empire.researchedTechnologyIds.push('tech-ironwork', 'steel-laurel-spearhead')
    engine.restore(state)
    expect(engine.recruitUnits(city.id, 'unit-light')).toEqual({
      ok: false,
      message: 'Missing prerequisite: doctrine-war.',
    })
    expect(engine.research('doctrine-war')).toMatchObject({ ok: true })
    for (const unitId of ['unit-light', 'unit-regular', 'unit-heavy', 'unit-knight']) {
      expect(engine.recruitUnits(city.id, unitId)).toMatchObject({ ok: true })
    }
    expect(engine.state.army.equipmentStock['basic-kit']).toBe(90)
    expect(engine.state.army.equipmentStock['weapon-laurel-spear']).toBe(0)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('minigame')
    expect(engine.state.minigame?.plan.deployments.map(item => item.unitId).sort()).toEqual([
      'unit-heavy',
      'unit-knight',
      'unit-light',
      'unit-regular',
    ])
  })

  it('executes both Hearts-7 faces and persists the combat-spirit gift cap', () => {
    const value = config()
    const startFace = (inverted: boolean) => {
      const engine = new EmpiresEndgameEngine(value)
      const state = engine.snapshot()
      putHeartsSevenInHand(state, inverted)
      state.phase = 'divineGift'
      state.pendingResolution = null
      state.giftChoiceIds = ['gift-combat-spirit']
      engine.restore(state)
      expect(engine.state.giftChoiceIds).toContain('gift-combat-spirit')
      expect(engine.chooseGift('gift-combat-spirit')).toMatchObject({ ok: true })
      return engine
    }

    const normal = startFace(false)
    expect(normal.state.empire.flags.unlimitedTavernRecruitment).toBe(1)
    expect(normal.state.army.maxMorale).toBe(3)
    expect(normal.state.empire.claimedGiftIds).toContain('gift-combat-spirit')
    const saved = new EmpiresEndgameEngine(value, normal.snapshot())
    expect(saved.state.army.maxMorale).toBe(3)

    const inverted = startFace(true)
    expect(inverted.state.empire.flags).toMatchObject({
      militaryArson: 1,
      recruitmentDisabled: 1,
    })
    expect(inverted.recruitUnits(inverted.state.empire.cities[0].id, 'unit-light')).toEqual({
      ok: false,
      message: 'Recruitment is disabled for this empire phase.',
    })
  })
})
