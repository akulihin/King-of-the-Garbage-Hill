import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, validateEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
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

function disableTdMinigame(value: EmpiresEndgameConfig): void {
  value.td.enabled = false
  value.td.regionalCatalogEnabled = false
  value.td.towerBases = []
  value.td.battlefields = []
  value.td.towers = []
  value.td.gradeChoices = []
  value.td.waves = []
  value.td.planVariants = []
}

function empireState(engine: EmpiresEndgameEngine, con = 1): EmpiresCampaignState {
  const state = engine.snapshot()
  state.phase = 'empire'
  state.con = con
  state.event = null
  state.minigame = null
  state.empire.daysRemaining = 59
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  for (const resource of engine.config.empire.resources) {
    state.empire.resources[resource.id] = 1_000_000_000
  }
  for (const city of state.empire.cities) {
    city.resources[engine.config.empire.foodResourceId] = 1_000_000_000
  }
  return state
}

function testSteel(
  id: string,
  branchId: string,
  generation: number,
  prerequisites: EmpiresTechnologyDefinition['prerequisites'] = [],
  extra: Partial<NonNullable<EmpiresTechnologyDefinition['steel']>> = {},
): EmpiresTechnologyDefinition {
  return {
    id,
    name: id,
    category: 'steel',
    groupId: branchId,
    timeCostDays: 1,
    resourceCosts: [{ resourceId: 'knowledge', amount: 10 }],
    prerequisites,
    effects: [],
    steel: {
      branchId,
      generation,
      stage: 'whole',
      payoff: 'unlock-only',
      ...extra,
    },
  }
}

describe('Empire\'s Endgame Phase 3B steel and equipment bridge', () => {
  it('catalogues every existing carrier without claiming unsupported steel payoffs are live', () => {
    const value = config()
    const steel = value.empire.technologies.filter(technology => technology.category === 'steel')
    expect(steel).toHaveLength(22)
    expect(steel.every(technology => technology.steel)).toBe(true)
    expect(steel.filter(technology => !technology.deferredReason).map(technology => technology.id)).toEqual([
      'steel-laurel-spearhead',
      'steel-lancet-spearhead',
      'steel-diamond-spearhead',
      'steel-cross-spearhead',
    ])
    expect(steel.filter(technology => !technology.deferredReason)
      .every(technology => technology.steel?.payoff === 'equipment')).toBe(true)
    expect(value.empire.technologies.find(technology => technology.id === 'steel-lance')?.steel)
      .toMatchObject({ generation: 4, stage: 'plus', eliteRequired: true })
    const generals = value.empire.technologies.find(technology => technology.id === 'tech-generals')!
    const foundry = value.empire.technologies.find(technology => technology.id === 'tech-foundry')!
    expect(generals.deferredReason).toBeUndefined()
    expect(generals.deferredSubfeatures).toEqual(expect.any(Array))
    expect(foundry.deferredReason).toBeUndefined()
    expect(foundry).toMatchObject({ name: 'Литье стали', deferredSubfeatures: expect.any(Array) })
    expect(value.empire.buildings.find(building => building.id === 'building-foundry'))
      .toMatchObject({ deferredSubfeatures: [{ id: 'capital-sixth-slot' }] })
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine)
    state.empire.researchedTechnologyIds.push('steel-cross-spearhead')
    engine.restore(state)
    expect(engine.state.empire.steelResearch.delayedFree['steel-lance']).toBeUndefined()
  })

  it('rejects fabricated steel loadouts and incomplete Academy or Hearts-Ace carriers', () => {
    const fabricatedLoadout = config()
    disableTdMinigame(fabricatedLoadout)
    fabricatedLoadout.combat.enabled = false
    const regular = fabricatedLoadout.empire.units!.find(unit => unit.id === 'unit-regular')!
    regular.loadouts![0].equipmentCosts = [{ equipmentId: 'basic-kit', amount: 1 }]
    expect(() => validateEmpiresConfig(fabricatedLoadout))
      .toThrow(/must consume its technology-linked equipment weapon-laurel-spear/)

    const fabricatedFallback = config()
    const fallbackUnit = fabricatedFallback.empire.units!.find(unit => unit.id === 'unit-regular')!
    fallbackUnit.loadouts = undefined
    expect(() => validateEmpiresConfig(fabricatedFallback))
      .toThrow(/must consume its technology-linked equipment weapon-laurel-spear/)

    const academy = config()
    delete academy.empire.buildings
      .find(building => building.id === 'building-military-academy')!.deferredReason
    expect(() => validateEmpiresConfig(academy)).toThrow(/freeUnitsPerWarTechnology/)

    for (const face of ['normal', 'inverted'] as const) {
      const ace = config()
      delete ace.cards.find(card => card.id === 'card-hearts-ace')![face].deferredReason
      expect(() => validateEmpiresConfig(ace)).toThrow(/unit(Morale|Actives)/)
    }

    const crossBranchAccess = config()
    crossBranchAccess.empire.technologies
      .find(technology => technology.id === 'steel-lance')!.steel!.accessTechnologyId = 'steel-ship-cannon'
    expect(() => validateEmpiresConfig(crossBranchAccess)).toThrow(/earlier access stage in the same branch/)

    const ungatedProduction = config()
    const laurelRecipe = ungatedProduction.td.equipmentProduction
      .find(recipe => recipe.equipmentId === 'weapon-laurel-spear')!
    ungatedProduction.td.equipmentProduction.push({
      ...laurelRecipe,
      id: 'ungated-laurel-production',
      technologyId: undefined,
    })
    expect(() => validateEmpiresConfig(ungatedProduction))
      .toThrow(/weapon-laurel-spear must use its equipment technology steel-laurel-spearhead/)

    const duplicateTowerCost = config()
    duplicateTowerCost.td.towerBases![0].loadouts = [{
      id: 'duplicate-cost',
      priority: 1,
      weaponEquipmentId: 'weapon-laurel-spear',
      equipmentCosts: [
        { equipmentId: 'weapon-laurel-spear', amount: 1 },
        { equipmentId: 'weapon-laurel-spear', amount: 1 },
      ],
    }]
    expect(() => validateEmpiresConfig(duplicateTowerCost))
      .toThrow(/repeats equipment cost weapon-laurel-spear/)
  })

  it('persists fork entry pricing and uses the same authoritative quote after restore', () => {
    const value = config()
    value.empire.technologies.push(
      testSteel('steel-test-a0', 'steel-test-a', 0),
      testSteel('steel-test-a1', 'steel-test-a', 1, [{ kind: 'technology', technologyId: 'steel-test-a0' }]),
      testSteel('steel-test-a2', 'steel-test-a', 2, [{ kind: 'technology', technologyId: 'steel-test-a1' }]),
      testSteel('steel-test-b0', 'steel-test-b', 0),
      testSteel(
        'steel-test-b1',
        'steel-test-b',
        1,
        [{ kind: 'technology', technologyId: 'steel-test-b0' }],
        { entryFromTechnologyIds: ['steel-test-a1'] },
      ),
    )
    value.empire.technologies.find(technology => technology.id === 'steel-test-b0')!.groupId = 'test-b-zero'
    value.empire.technologies.find(technology => technology.id === 'steel-test-b1')!.groupId = 'test-b-one'
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine)
    state.empire.researchedTechnologyIds.push('steel-test-a0', 'steel-test-a1')
    engine.restore(state)

    expect(engine.researchQuote('steel-test-b1')).toMatchObject({
      entryFromTechnologyId: 'steel-test-a1',
      costMultiplier: 1,
      resourceCosts: [{ resourceId: 'knowledge', amount: 10 }],
      blockedReason: null,
    })
    expect(engine.research('steel-test-b1')).toMatchObject({ ok: true })
    expect(engine.state.empire.steelResearch).toMatchObject({
      branchCostMultipliers: { 'steel-test-a': 2 },
      branchEntries: [{
        fromTechnologyId: 'steel-test-a1',
        toTechnologyId: 'steel-test-b1',
        fromBranchId: 'steel-test-a',
        toBranchId: 'steel-test-b',
      }],
    })
    const beforeRestore = engine.researchQuote('steel-test-a2')
    expect(beforeRestore).toMatchObject({
      costMultiplier: 2,
      resourceCosts: [{ resourceId: 'knowledge', amount: 20 }],
    })
    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.empire.steelResearch).toEqual(engine.state.empire.steelResearch)
    expect(restored.researchQuote('steel-test-a2')).toEqual(beforeRestore)
    expect(restored.research('steel-test-b0')).toEqual({
      ok: false,
      message: 'That research group was already used this empire phase.',
    })
  })

  it('awards delayed + stages once at the exact con boundary and retains the elite gate', () => {
    const value = config()
    const delayedPlus = testSteel(
      'steel-timer-plus',
      'steel-timer',
      1,
      [{ kind: 'flag', flagId: 'externalSteelGate', minimum: 1 }],
      {
        stage: 'plus',
        accessTechnologyId: 'steel-timer-access',
        eliteRequired: true,
      },
    )
    delayedPlus.effects = [{ kind: 'time', days: 4 }]
    value.empire.technologies.push(
      testSteel('steel-timer-access', 'steel-timer', 0),
      delayedPlus,
      {
        id: 'technology-grant-elite',
        name: 'technology-grant-elite',
        category: 'technology',
        groupId: 'elite-test',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [
          { kind: 'flag', flagId: 'militaryElite', amount: 1 },
          { kind: 'flag', flagId: 'externalSteelGate', amount: 1 },
        ],
      },
    )
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine, 1)
    state.empire.researchedTechnologyIds.push('steel-timer-access')
    engine.restore(state)
    expect(engine.state.empire.steelResearch.delayedFree['steel-timer-plus']).toEqual({
      scheduledAtCon: 1,
      eligibleCon: 3,
      awardedAtCon: null,
    })
    expect(engine.researchQuote('steel-timer-plus').blockedReason)
      .toBe('A military elite is required for this research.')
    const eliteOnly = engine.snapshot()
    eliteOnly.con = 3
    eliteOnly.empire.flags.militaryElite = 1
    const quoteEngine = new EmpiresEndgameEngine(value, eliteOnly)
    expect(quoteEngine.researchQuote('steel-timer-plus').blockedReason)
      .toBe('Missing prerequisite: externalSteelGate.')

    const conTwo = engine.snapshot()
    conTwo.con = 2
    engine.restore(conTwo)
    expect(engine.state.empire.researchedTechnologyIds).not.toContain('steel-timer-plus')
    const conThree = engine.snapshot()
    conThree.con = 3
    conThree.empire.daysRemaining = 1
    engine.restore(conThree)
    expect(engine.state.empire.researchedTechnologyIds).not.toContain('steel-timer-plus')
    expect(engine.researchQuote('steel-timer-plus').blockedReason)
      .toBe('A military elite is required for this research.')
    expect(engine.research('technology-grant-elite')).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('empire')
    expect(engine.state.empire.researchedTechnologyIds.filter(id => id === 'steel-timer-plus')).toHaveLength(1)
    expect(engine.state.empire.steelResearch.delayedFree['steel-timer-plus'].awardedAtCon).toBe(3)
    expect(engine.state.empire.daysRemaining).toBe(4)

    const nextPhase = engine.snapshot()
    nextPhase.phase = 'divineGift'
    nextPhase.giftChoiceIds = ['gift-combat-spirit']
    nextPhase.durak.playerHand = []
    engine.restore(nextPhase)
    expect(engine.chooseGift('gift-combat-spirit')).toMatchObject({ ok: true })
    expect(engine.state.empire.daysRemaining).toBe(59)
    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.empire.researchedTechnologyIds.filter(id => id === 'steel-timer-plus')).toHaveLength(1)

    const boundary = new EmpiresEndgameEngine(value)
    const scheduled = empireState(boundary, 1)
    scheduled.empire.researchedTechnologyIds.push('steel-timer-access')
    scheduled.empire.flags.militaryElite = 1
    scheduled.empire.flags.externalSteelGate = 1
    boundary.restore(scheduled)
    const giftBoundary = boundary.snapshot()
    giftBoundary.con = 3
    giftBoundary.phase = 'divineGift'
    giftBoundary.giftChoiceIds = ['gift-combat-spirit']
    giftBoundary.durak.playerHand = []
    boundary.restore(giftBoundary)
    expect(boundary.state.empire.researchedTechnologyIds).not.toContain('steel-timer-plus')
    expect(boundary.chooseGift('gift-combat-spirit')).toMatchObject({ ok: true })
    expect(boundary.state.empire.researchedTechnologyIds).toContain('steel-timer-plus')
    expect(boundary.state.empire.daysRemaining).toBe(63)

    const sameBranchConfig = config()
    sameBranchConfig.empire.technologies.push(
      testSteel('steel-same-access', 'steel-same', 0),
      testSteel('steel-same-required', 'steel-same', 1, [
        { kind: 'technology', technologyId: 'steel-same-access' },
      ]),
      testSteel('steel-same-plus', 'steel-same', 2, [
        { kind: 'technology', technologyId: 'steel-same-required' },
      ], {
        stage: 'plus',
        accessTechnologyId: 'steel-same-access',
      }),
    )
    const sameBranch = new EmpiresEndgameEngine(sameBranchConfig)
    const waiting = empireState(sameBranch, 1)
    waiting.empire.researchedTechnologyIds.push('steel-same-access')
    sameBranch.restore(waiting)
    const due = sameBranch.snapshot()
    due.con = 3
    sameBranch.restore(due)
    expect(sameBranch.state.empire.researchedTechnologyIds).not.toContain('steel-same-plus')
    expect(sameBranch.researchQuote('steel-same-plus').blockedReason)
      .toBe('Missing prerequisite: steel-same-required.')
  })

  it('shares Smithy capacity, consumes produced stock once, and freezes loadouts into cohorts and TD plans', () => {
    const value = config()
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine)
    state.empire.researchedTechnologyIds.push('doctrine-war', 'tech-ironwork', 'steel-laurel-spearhead')
    engine.restore(state)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.army.equipmentStock).toMatchObject({
      'basic-kit': 65,
      'weapon-laurel-spear': 65,
    })
    expect(engine.state.army.equipmentStock['weapon-lancet-spear'] ?? 0).toBe(0)

    const recruit = empireState(engine)
    recruit.empire.researchedTechnologyIds = ['doctrine-war', 'steel-laurel-spearhead']
    recruit.army.equipmentStock = { 'basic-kit': 2, 'weapon-laurel-spear': 1 }
    engine.restore(recruit)
    expect(engine.recruitmentQuote('city-tetrakor-capital', 'unit-regular')).toMatchObject({
      loadoutId: 'laurel-spear',
      equipmentCosts: [
        { equipmentId: 'basic-kit', amount: 2 },
        { equipmentId: 'weapon-laurel-spear', amount: 1 },
      ],
      blockedReason: null,
    })
    expect(engine.recruitUnits('city-tetrakor-capital', 'unit-regular')).toMatchObject({ ok: true })
    expect(engine.state.army.equipmentStock).toMatchObject({ 'basic-kit': 0, 'weapon-laurel-spear': 0 })
    const laurel = engine.state.empire.cities
      .find(city => city.id === 'city-tetrakor-capital')!.recruitedUnitCohorts[0]
    expect(laurel).toMatchObject({ loadoutId: 'laurel-spear', weaponEquipmentId: 'weapon-laurel-spear' })

    const upgraded = empireState(engine, 2)
    upgraded.empire.researchedTechnologyIds = [
      'doctrine-war',
      'steel-laurel-spearhead',
      'steel-lancet-spearhead',
      'steel-diamond-spearhead',
      'steel-cross-spearhead',
    ]
    upgraded.army.equipmentStock = { 'basic-kit': 2, 'weapon-cross-spear': 1 }
    upgraded.external.nextWaveCon = 2
    engine.restore(upgraded)
    expect(engine.recruitUnits('city-tetrakor-capital', 'unit-regular')).toMatchObject({ ok: true })
    const cohorts = engine.state.empire.cities
      .find(city => city.id === 'city-tetrakor-capital')!.recruitedUnitCohorts
    expect(cohorts.map(cohort => cohort.loadoutId).sort()).toEqual(['cross-spear', 'laurel-spear'])
    expect(cohorts.find(cohort => cohort.loadoutId === 'laurel-spear')?.weapon).toEqual(laurel.weapon)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('minigame')
    expect(engine.state.minigame?.plan.deployments.map(deployment => deployment.cohortId).sort())
      .toEqual(cohorts.map(cohort => cohort.id).sort())
    expect(engine.state.minigame?.plan.deployments.find(deployment => deployment.cohortId === laurel.id)?.weapon)
      .toEqual(laurel.weapon)
  })

  it('keeps campaign equipment production live when the TD minigame is disabled', () => {
    const value = config()
    disableTdMinigame(value)
    expect(() => validateEmpiresConfig(value)).not.toThrow()
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine)
    state.empire.researchedTechnologyIds.push('tech-ironwork', 'steel-laurel-spearhead')
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.army.equipmentStock).toMatchObject({
      'basic-kit': 65,
      'weapon-laurel-spear': 65,
    })
  })

  it('does not merge newly recruited troops into a cohort with an older frozen profile', () => {
    const originalConfig = config()
    const original = new EmpiresEndgameEngine(originalConfig)
    const initial = empireState(original)
    initial.empire.researchedTechnologyIds.push('doctrine-war', 'steel-laurel-spearhead')
    initial.army.equipmentStock = { 'basic-kit': 4, 'weapon-laurel-spear': 2 }
    original.restore(initial)
    expect(original.recruitUnits('city-tetrakor-capital', 'unit-regular')).toMatchObject({ ok: true })
    const originalCohort = original.state.empire.cities
      .find(city => city.id === 'city-tetrakor-capital')!.recruitedUnitCohorts[0]

    const updatedConfig = config()
    const updatedWeapon = updatedConfig.combat.equipment
      .find(equipment => equipment.id === 'weapon-laurel-spear')
    if (!updatedWeapon || updatedWeapon.kind !== 'weapon') throw new Error('Missing laurel spear fixture')
    updatedWeapon.profile.damageLevels.piercing = 3
    const updated = new EmpiresEndgameEngine(updatedConfig, original.snapshot())
    expect(updated.recruitUnits('city-tetrakor-capital', 'unit-regular')).toMatchObject({ ok: true })
    const cohorts = updated.state.empire.cities
      .find(city => city.id === 'city-tetrakor-capital')!.recruitedUnitCohorts

    expect(cohorts).toHaveLength(2)
    expect(new Set(cohorts.map(cohort => cohort.id)).size).toBe(2)
    expect(cohorts.find(cohort => cohort.id === originalCohort.id)?.weapon).toEqual(originalCohort.weapon)
    expect(cohorts.find(cohort => cohort.id !== originalCohort.id)?.weapon?.damageLevels.piercing).toBe(3)
  })

  it('chooses the highest-priority loadout affordable with its combined base costs', () => {
    const value = config()
    const regular = value.empire.units!.find(unit => unit.id === 'unit-regular')!
    regular.equipmentCosts = [{ equipmentId: 'basic-kit', amount: 1 }]
    const laurel = regular.loadouts!.find(loadout => loadout.id === 'laurel-spear')!
    laurel.id = 'unaffordable-overlap'
    laurel.priority = 10
    laurel.equipmentCosts = [
      { equipmentId: 'basic-kit', amount: 1 },
      { equipmentId: 'weapon-laurel-spear', amount: 1 },
    ]
    const lancet = regular.loadouts!.find(loadout => loadout.id === 'lancet-spear')!
    lancet.id = 'affordable-fallback'
    lancet.priority = 5
    lancet.equipmentCosts = [{ equipmentId: 'weapon-lancet-spear', amount: 1 }]
    expect(() => validateEmpiresConfig(value)).not.toThrow()
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine)
    state.empire.researchedTechnologyIds.push(
      'doctrine-war',
      'steel-laurel-spearhead',
      'steel-lancet-spearhead',
    )
    state.army.equipmentStock = {
      'basic-kit': 1,
      'weapon-laurel-spear': 1,
      'weapon-lancet-spear': 1,
    }
    engine.restore(state)

    expect(engine.recruitmentQuote('city-tetrakor-capital', 'unit-regular')).toMatchObject({
      loadoutId: 'affordable-fallback',
      blockedReason: null,
    })
  })

  it('keeps technology-locked tower loadouts out of scheduled TD plans', () => {
    const value = config()
    for (const base of value.td.towerBases!) {
      base.loadouts = [{
        id: 'laurel-tower-test',
        priority: 1,
        weaponEquipmentId: 'weapon-laurel-spear',
        equipmentCosts: [{ equipmentId: 'weapon-laurel-spear', amount: 1 }],
      }]
    }
    const schedule = (researched: boolean) => {
      const engine = new EmpiresEndgameEngine(value)
      const state = empireState(engine, 2)
      state.external.nextWaveCon = 2
      state.army.equipmentStock['weapon-laurel-spear'] = 1
      if (researched) state.empire.researchedTechnologyIds.push('steel-laurel-spearhead')
      engine.restore(state)
      expect(engine.finishEmpire()).toMatchObject({ ok: true })
      return engine.state.minigame!.plan.towerBases.flatMap(base => base.loadouts ?? [])
    }
    expect(schedule(false)).toEqual([])
    expect(schedule(true).map(loadout => loadout.id)).toContain('laurel-tower-test')
  })

  it('settles tower equipment spending against campaign stock exactly once', () => {
    const value = config()
    for (const base of value.td.towerBases!) {
      base.loadouts = [{
        id: 'laurel-stock-settlement',
        priority: 1,
        weaponEquipmentId: 'weapon-laurel-spear',
        equipmentCosts: [{ equipmentId: 'weapon-laurel-spear', amount: 1 }],
      }]
    }
    const engine = new EmpiresEndgameEngine(value)
    const state = empireState(engine, 2)
    state.external.nextWaveCon = 2
    state.empire.researchedTechnologyIds.push('steel-laurel-spearhead')
    state.army.equipmentStock['weapon-laurel-spear'] = 2
    engine.restore(state)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    const session = engine.state.minigame!
    const before = engine.state.army.equipmentStock['weapon-laurel-spear']
    const command = {
      tick: 0,
      sequence: 0,
      sessionId: session.plan.sessionId,
      planId: session.plan.id,
      kind: 'build-tower' as const,
      spotId: session.plan.battlefield.buildSpots[0].id,
      towerBaseId: session.plan.towerBases[0].id,
    }

    expect(engine.abortMinigame([command], 1)).toMatchObject({ ok: true })
    expect(engine.state.army.equipmentStock['weapon-laurel-spear']).toBe(before - 1)
    expect(engine.state.minigameResultLog.at(-1)?.result.equipmentSpent)
      .toEqual({ 'weapon-laurel-spear': 1 })
    const after = engine.state.army.equipmentStock['weapon-laurel-spear']
    expect(engine.abortMinigame([command], 1)).toMatchObject({ ok: true })
    expect(engine.state.army.equipmentStock['weapon-laurel-spear']).toBe(after)
  })

  it('applies Foundry quotes/cadence/upkeep and the relic morale floor through typed consumers', () => {
    const value = config()
    const initialFloor = config()
    initialFloor.empire.initialFlags.minimumCombatSpirit = 2
    expect(new EmpiresEndgameEngine(initialFloor).state.army.morale).toBe(2)
    value.empire.units!.find(unit => unit.id === 'unit-regular')!.resourceCosts = [
      { resourceId: 'gold', amount: 100 },
    ]
    const engine = new EmpiresEndgameEngine(value)
    const blockedCapital = empireState(engine)
    blockedCapital.empire.researchedTechnologyIds.push('tech-foundry')
    const capital = blockedCapital.empire.cities
      .find(candidate => candidate.id === 'city-tetrakor-capital')!
    capital.buildingLevels['building-temple'] = 0
    capital.operationalBuildingLevels['building-temple'] = 0
    engine.restore(blockedCapital)
    const capitalUniqueSlot = value.empire.cities
      .find(candidate => candidate.id === capital.id)!.slots.find(slot => slot.kind === 'unique')!
    expect(engine.placeBuilding(capital.id, capitalUniqueSlot.id, 'building-foundry')).toEqual({
      ok: false,
      message: 'That building cannot be placed in this city.',
    })

    const state = empireState(engine)
    state.empire.researchedTechnologyIds.push(
      'doctrine-war',
      'tech-foundry',
      'steel-laurel-spearhead',
    )
    state.army.equipmentStock['basic-kit'] = 10
    state.army.equipmentStock['weapon-laurel-spear'] = 2
    const city = state.empire.cities.find(candidate => candidate.id === 'city-north-iron-gate')!
    city.buildingLevels['building-foundry'] = 1
    city.operationalBuildingLevels['building-foundry'] = 1
    engine.restore(state)

    expect(engine.recruitmentQuote(city.id, 'unit-regular', 2)).toMatchObject({
      timeCostDays: 0.85,
      usedFoundryInstant: false,
    })
    expect(engine.recruitmentQuote(city.id, 'unit-regular')).toMatchObject({
      resourceCosts: [{ resourceId: 'gold', amount: 85 }],
      timeCostDays: 0,
      usedFoundryInstant: true,
    })
    expect(engine.recruitUnits(city.id, 'unit-regular')).toMatchObject({ ok: true })
    expect(engine.state.army.foundryInstantReadyConByCity[city.id]).toBe(3)
    expect(engine.recruitmentQuote(city.id, 'unit-regular')).toMatchObject({
      timeCostDays: 0.85,
      usedFoundryInstant: false,
    })
    expect(engine.cityArmyFoodUpkeep(city.id)).toBe(850)

    const relic = engine.snapshot()
    relic.phase = 'divineGift'
    relic.giftChoiceIds = ['relic-spirit-floor']
    relic.empire.flags.relicsUnlocked = 1
    relic.army.morale = 0
    engine.restore(relic)
    expect(engine.chooseGift('relic-spirit-floor')).toMatchObject({ ok: true })
    expect(engine.state.army.morale).toBe(2)
    const damaged = engine.snapshot()
    damaged.army.morale = -100
    const restored = new EmpiresEndgameEngine(value, damaged)
    expect(restored.state.army.morale).toBe(2)

    const repeated = restored.snapshot()
    repeated.phase = 'divineGift'
    repeated.giftChoiceIds = ['relic-spirit-floor']
    repeated.empire.flags.relicsUnlocked = 1
    restored.restore(repeated)
    expect(restored.chooseGift('relic-spirit-floor')).toMatchObject({ ok: true })
    expect(restored.state.empire.flags.minimumCombatSpirit).toBe(2)
    expect(restored.state.army.morale).toBe(2)
  })
})
