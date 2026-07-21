import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig } from '../config'
import { EmpiresEndgameEngine } from '../engine'
import {
  abortTdBattle,
  consumeTdFrameTime,
  createTdRulesIdentity,
  createTdSimulation,
  digestTdValue,
  replayTdBattle,
  stepTdSimulation,
  tdCommandDisabledReason,
  validateTdBattlePlan,
  validateTdCommandLog,
} from './engine'
import { createTdPolicyCommandLog, resolveTdWithPolicy, TD_QA_POLICIES } from './qa'
import { EMPIRES_STABILIZATION_BUDGETS } from '../stabilization'
import type {
  TdBattlePlan,
  TdCommand,
  TdDeploymentPlan,
  TdSimulationState,
} from './types'

const config = cloneEmpiresConfig(defaultConfigJson)

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function assaultDeployment(battlePlan: Pick<TdBattlePlan, 'battlefield'>): TdDeploymentPlan {
  return {
    id: 'capital:unit-light',
    cohortId: 'cohort-capital-light',
    cityId: 'capital',
    unitId: 'unit-light',
    unitInstanceIds: Array.from({ length: 8 }, (_, index) => `capital-light-${index + 1}`),
    count: 8,
    nodeId: battlePlan.battlefield.spawnerNodeId,
    speedPerSecond: 70,
    maxHpPerUnit: 20,
    attackRange: 140,
    attackIntervalTicks: 10,
    weapon: { damageLevels: { impact: 3 }, tags: ['unit'] },
    armor: null,
  }
}

function plan(variantId = 'central-castle-defense'): TdBattlePlan {
  const variant = config.td.planVariants!.find(candidate => candidate.id === variantId)
  if (!variant) throw new Error(`Missing test TD variant ${variantId}.`)
  const battlefield = config.td.battlefields.find(candidate => candidate.id === variant.battlefieldId)
  const wave = config.td.waves.find(candidate => candidate.id === variant.waveId)
  if (!battlefield || !wave) throw new Error(`Variant ${variantId} has incomplete test data.`)
  const id = `td-spec-${variantId}`
  const { seed: _initializationSeed, ...settlementConfig } = config
  const battlePlan: TdBattlePlan = {
    id,
    sessionId: `${id}:session`,
    rulesIdentity: createTdRulesIdentity(config.schemaVersion, config.combat, config.td, {
      technologies: config.empire.technologies,
      units: config.empire.units ?? [],
      buildings: config.empire.buildings,
      steelResearch: config.empire.steelResearch,
      medical: config.empire.medical,
      loyalty: config.empire.loyalty,
      expeditions: config.expeditions,
      quests: config.quests,
      settlementConfig,
      sharedResultRetention: {
        td: config.td.resultLogLimit ?? 32,
        alchemy: config.alchemy.resultLogLimit,
        inventory: config.inventory.resultLogLimit,
        saveUtf8Bytes: EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes,
      },
    }),
    mode: variant.mode,
    scheduledCon: 2,
    threat: 0,
    tickMs: config.td.tickMs!,
    maxTicks: config.td.maxTicks!,
    maxCommands: config.td.maxCommands!,
    maxCatchUpTicksPerFrame: config.td.maxCatchUpTicksPerFrame!,
    startingBuildResources: variant.startingBuildResources ?? config.td.startingBuildResources!,
    battlefield: clone(battlefield),
    objective: clone(variant.objective),
    towerBases: clone(config.td.towerBases!.filter(base => battlefield.towerBaseIds.includes(base.id))),
    towerChoices: clone(config.td.towers),
    gradeChoices: clone(config.td.gradeChoices!.filter(set => set.regionId === battlefield.regionId)),
    wave: clone(wave),
    combat: clone(config.combat),
    equipmentStock: {},
    deployments: [],
  }
  if (variant.mode === 'assault') battlePlan.deployments = [assaultDeployment(battlePlan)]
  return battlePlan
}

function commandIdentity(battlePlan: TdBattlePlan, tick = 0, sequence = 0) {
  return {
    tick,
    sequence,
    sessionId: battlePlan.sessionId,
    planId: battlePlan.id,
  }
}

function buildCommand(
  battlePlan: TdBattlePlan,
  tick = 0,
  sequence = 0,
  towerBaseId = battlePlan.towerBases[0].id,
): TdCommand {
  return {
    ...commandIdentity(battlePlan, tick, sequence),
    kind: 'build-tower',
    spotId: battlePlan.battlefield.buildSpots[0].id,
    towerBaseId,
  }
}

function freezeDeep<T>(value: T): T {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value
  for (const child of Object.values(value)) freezeDeep(child)
  return Object.freeze(value)
}

function runWithFrameCadence(
  battlePlan: TdBattlePlan,
  seed: string | number,
  commandLog: readonly TdCommand[],
  elapsedFrames: readonly number[],
  reloadAtTick?: number,
): TdSimulationState {
  let state = createTdSimulation(battlePlan, seed)
  let accumulatorMs = 0
  let frameIndex = 0
  let commandIndex = 0
  let reloaded = false
  let guard = 0
  while (!state.terminalReason) {
    const clock = consumeTdFrameTime(
      accumulatorMs,
      elapsedFrames[frameIndex % elapsedFrames.length],
      battlePlan.tickMs,
      battlePlan.maxCatchUpTicksPerFrame,
    )
    accumulatorMs = clock.accumulatorMs
    frameIndex += 1
    for (let offset = 0; offset < clock.ticks && !state.terminalReason; offset += 1) {
      const due: TdCommand[] = []
      while (commandLog[commandIndex]?.tick === state.tick) {
        due.push(commandLog[commandIndex])
        commandIndex += 1
      }
      stepTdSimulation(battlePlan, state, due)
      if (!reloaded && reloadAtTick !== undefined && state.tick >= reloadAtTick) {
        state = clone(state)
        accumulatorMs = clone(accumulatorMs)
        reloaded = true
      }
    }
    guard += 1
    if (guard > battlePlan.maxTicks * 10) throw new Error('Frame-cadence test exceeded its guard.')
  }
  return state
}

function configureSingleDurableEnemy(battlePlan: TdBattlePlan, categories: string[]): void {
  const group = battlePlan.wave.groups[0]
  group.categoryIds = categories
  group.count = 1
  group.maxHp = 1_000
  group.speedPerSecond = 0.001
  group.attackRange = 1_000
  group.attackIntervalTicks = 1
  group.weapon = { damageLevels: { impact: 100 }, tags: ['alliance'] }
  battlePlan.towerBases[0].weapon = { damageLevels: { impact: 0 }, tags: ['tower'] }
}

describe('Empire\'s Endgame deterministic regional TD engine', () => {
  it('builds valid resolved plans for every authored defense and assault variant', () => {
    expect(config.td.planVariants!.map(variant => variant.id)).toEqual([
      'central-castle-defense',
      'central-fort-assault',
      'swamp-fort-defense',
      'forest-fort-defense',
      'north-ship-defense',
      'desert-fort-defense',
      'desert-fort-expedition-assault',
    ])
    for (const variant of config.td.planVariants!) {
      expect(validateTdBattlePlan(plan(variant.id)), variant.id).toEqual([])
    }
  })

  it.each([
    {
      regionId: 'center',
      variantId: 'central-castle-defense',
      fieldId: 'battlefield-central',
      baseIds: ['tower-base-center'],
      modifierKinds: [],
    },
    {
      regionId: 'east',
      variantId: 'swamp-fort-defense',
      fieldId: 'battlefield-swamp',
      baseIds: ['tower-base-swamp'],
      modifierKinds: ['tower-targeting'],
    },
    {
      regionId: 'west',
      variantId: 'forest-fort-defense',
      fieldId: 'battlefield-forest',
      baseIds: ['tower-base-forest'],
      modifierKinds: ['tower-targeting', 'tower-stat'],
    },
    {
      regionId: 'north',
      variantId: 'north-ship-defense',
      fieldId: 'battlefield-north-shore',
      baseIds: ['tower-base-north-catapult', 'tower-base-north-trebuchet'],
      modifierKinds: [],
    },
    {
      regionId: 'south',
      variantId: 'desert-fort-defense',
      fieldId: 'battlefield-desert',
      baseIds: ['tower-base-desert'],
      modifierKinds: ['deployment-attrition'],
    },
  ])('resolves the $regionId regional field and typed rules', ({
    variantId,
    fieldId,
    regionId,
    baseIds,
    modifierKinds,
  }) => {
    const battlePlan = plan(variantId)
    expect(battlePlan.battlefield.id).toBe(fieldId)
    expect(battlePlan.battlefield.regionId).toBe(regionId)
    expect(battlePlan.towerBases.map(base => base.id)).toEqual(baseIds)
    expect(battlePlan.battlefield.modifiers.map(modifier => modifier.kind)).toEqual(modifierKinds)
    expect(battlePlan.gradeChoices.map(set => set.grade)).toEqual([1, 2, 3, 4])
  })

  it('applies swamp reachability plus forest tree targeting and durability data', () => {
    const swamp = plan('swamp-fort-defense')
    configureSingleDurableEnemy(swamp, ['melee'])
    const swampState = createTdSimulation(swamp, 'swamp-reachability')
    stepTdSimulation(swamp, swampState, [buildCommand(swamp)])
    expect(swampState.towers).toHaveLength(1)
    expect(swampState.towers[0].hp).toBe(swamp.towerBases[0].maxHp)

    const forestMelee = plan('forest-fort-defense')
    configureSingleDurableEnemy(forestMelee, ['melee'])
    const meleeState = createTdSimulation(forestMelee, 'forest-melee')
    stepTdSimulation(forestMelee, meleeState, [buildCommand(forestMelee)])
    expect(meleeState.towers[0].hp).toBe(forestMelee.towerBases[0].maxHp * 1.5)

    const forestRanged = plan('forest-fort-defense')
    configureSingleDurableEnemy(forestRanged, ['ranged'])
    const rangedState = createTdSimulation(forestRanged, 'forest-ranged')
    stepTdSimulation(forestRanged, rangedState, [buildCommand(forestRanged)])
    expect(rangedState.towers[0].hp).toBe(forestRanged.towerBases[0].maxHp * 1.5 - 100)
  })

  it('enforces north artillery categories/no upgrades and applies desert defender attrition', () => {
    const north = plan('north-ship-defense')
    const northState = createTdSimulation(north, 'north-categories')
    expect(tdCommandDisabledReason(north, northState, buildCommand(north))).toBeNull()

    const forbiddenBase = clone(config.td.towerBases!.find(base => base.id === 'tower-base-center')!)
    north.towerBases.push(forbiddenBase)
    north.battlefield.towerBaseIds.push(forbiddenBase.id)
    expect(tdCommandDisabledReason(
      north,
      northState,
      buildCommand(north, 0, 0, forbiddenBase.id),
    )).toContain('forbidden by battlefield categories')

    stepTdSimulation(north, northState, [buildCommand(north)])
    const northChoice = north.towerChoices.find(choice => choice.grade === 1)!
    expect(tdCommandDisabledReason(north, northState, {
      ...commandIdentity(north, northState.tick, 1),
      kind: 'upgrade-tower',
      spotId: north.battlefield.buildSpots[0].id,
      choiceId: northChoice.id,
    })).toContain('Северный источник запрещает улучшения башен')

    const desert = plan('desert-fort-defense')
    desert.deployments = [{
      ...assaultDeployment(desert),
      unitInstanceIds: assaultDeployment(desert).unitInstanceIds.slice(0, 4),
      count: 4,
      speedPerSecond: 0,
      nodeId: desert.battlefield.deploymentNodeId,
    }]
    desert.wave.groups[0].count = 1
    desert.wave.groups[0].maxHp = 1_000
    desert.wave.groups[0].speedPerSecond = 0.001
    desert.wave.groups[0].attackRange = 0
    desert.wave.groups[0].weapon = { damageLevels: { impact: 0 }, tags: ['alliance'] }
    const desertState = createTdSimulation(desert, 'desert-attrition')
    for (let tick = 0; tick <= 100; tick += 1) stepTdSimulation(desert, desertState)
    expect(desertState.squads[0].hp).toBe(79)
    expect(desertState.damageByType.attrition).toBe(1)
  })

  it('rejects executable plans with unknown combat references or disconnected routes', () => {
    const unknownDamage = plan()
    unknownDamage.towerBases[0].weapon.damageLevels = { missing: 1 }
    expect(validateTdBattlePlan(unknownDamage)).toContain(
      `tower base ${unknownDamage.towerBases[0].id} weapon uses unknown damage type missing`,
    )

    const disconnected = plan()
    disconnected.wave.groups[0].routeEdgeIds = ['central-road-2', 'central-road-3']
    expect(validateTdBattlePlan(disconnected)).toContain(
      `group ${disconnected.wave.groups[0].id} route is not contiguous from the spawner`,
    )

    const invalidEquipment = plan()
    invalidEquipment.equipmentStock.bad = Number.POSITIVE_INFINITY
    invalidEquipment.towerBases[0].loadouts = [{
      id: 'invalid-loadout',
      priority: 1,
      weaponEquipmentId: 'missing-weapon',
      equipmentCosts: [{ equipmentId: 'bad', amount: 1 }],
    }]
    expect(validateTdBattlePlan(invalidEquipment)).toEqual(expect.arrayContaining([
      'equipmentStock bad must be finite and non-negative',
      `tower base ${invalidEquipment.towerBases[0].id} loadout invalid-loadout weapon is unavailable`,
    ]))

    const fabricatedSteel = plan()
    fabricatedSteel.towerBases[0].loadouts = [{
      id: 'fabricated-steel',
      priority: 1,
      weaponEquipmentId: 'weapon-laurel-spear',
      equipmentCosts: [{ equipmentId: 'basic-kit', amount: 1 }],
    }]
    expect(validateTdBattlePlan(fabricatedSteel)).toContain(
      `tower base ${fabricatedSteel.towerBases[0].id} loadout fabricated-steel must consume its technology-linked equipment weapon-laurel-spear`,
    )
  })

  it('advances one configured fixed tick and spawns once at the wave boundary', () => {
    const battlePlan = plan()
    const state = createTdSimulation(battlePlan, 'fixed-step')

    stepTdSimulation(battlePlan, state)

    expect(state.tick).toBe(1)
    expect(state.elapsedMs).toBe(battlePlan.tickMs)
    expect(state.enemies).toHaveLength(1)
    expect(state.spawnedByGroup['central-infantry']).toBe(1)
  })

  it('validates logical command ticks, sequence, identity, stable order, kind, and cap', () => {
    const battlePlan = plan()
    const build = buildCommand(battlePlan)
    const gradeOne = battlePlan.gradeChoices.find(set => set.grade === 1)!.choiceIds[0]
    const upgrade: TdCommand = {
      ...commandIdentity(battlePlan, 1, 1),
      kind: 'upgrade-tower',
      spotId: battlePlan.battlefield.buildSpots[0].id,
      choiceId: gradeOne,
    }
    expect(validateTdCommandLog(battlePlan, [build, upgrade])).toEqual([])

    const legalState = createTdSimulation(battlePlan, 'legal-commands')
    stepTdSimulation(battlePlan, legalState, [build])
    stepTdSimulation(battlePlan, legalState, [upgrade])
    expect(legalState.towers[0].choiceIds).toEqual([gradeOne])
    expect(legalState.commandErrors).toEqual([])

    expect(validateTdCommandLog(battlePlan, [
      { ...build, tick: 2 },
      { ...upgrade, tick: 1 },
    ])).toContain('Command log is not monotonic at sequence 1.')
    expect(validateTdCommandLog(battlePlan, [{ ...build, sequence: 7 }]))
      .toContain('Command 0 must have sequence 0.')
    expect(validateTdCommandLog(battlePlan, [{ ...build, sessionId: 'stale-session' }]))
      .toContain('Command 0 has stale plan/session identity.')
    expect(validateTdCommandLog(battlePlan, [{ ...build, tick: battlePlan.maxTicks }]))
      .toContain('Command 0 has an out-of-bounds tick.')
    expect(validateTdCommandLog(battlePlan, [{ ...build, kind: 'wall-clock-command' } as never]))
      .toContain('Command 0 has an unknown kind.')

    const capped = plan()
    capped.maxCommands = 1
    expect(validateTdCommandLog(capped, [buildCommand(capped), buildCommand(capped, 1, 1)]))
      .toContain('Command log exceeds the 1-command limit.')
    expect(replayTdBattle(battlePlan, 'stale-command', [{ ...build, planId: 'old-plan' }]))
      .toMatchObject({ outcome: 'error', terminalReason: 'invalid-command' })
  })

  it('rejects structurally valid commands scheduled after the battle has ended', () => {
    const battlePlan = plan()
    battlePlan.wave.groups[0].count = 1
    battlePlan.wave.groups[0].maxHp = 1
    battlePlan.wave.groups[0].speedPerSecond = 0.001
    battlePlan.deployments = [{
      ...assaultDeployment(battlePlan),
      unitInstanceIds: ['post-terminal-unit'],
      count: 1,
      speedPerSecond: 0,
      nodeId: battlePlan.battlefield.deploymentNodeId,
      attackRange: 10_000,
      attackIntervalTicks: 1,
      weapon: { damageLevels: { impact: 100 }, tags: ['unit'] },
    }]

    const result = replayTdBattle(battlePlan, 'post-terminal-command', [
      buildCommand(battlePlan, 2),
    ])

    expect(result).toMatchObject({
      outcome: 'error',
      terminalReason: 'invalid-command',
      ticks: 1,
      error: 'Command 0 is scheduled after the battle ended at tick 1.',
    })
  })

  it('routes tower and deployed-unit hits through the shared combat damage catalog', () => {
    const battlePlan = plan()
    battlePlan.wave.groups[0].count = 1
    battlePlan.wave.groups[0].maxHp = 100
    battlePlan.wave.groups[0].speedPerSecond = 0.001
    battlePlan.deployments = [{
      ...assaultDeployment(battlePlan),
      unitInstanceIds: ['damage-catalog-unit'],
      count: 1,
      speedPerSecond: 0,
      nodeId: battlePlan.battlefield.deploymentNodeId,
      maxHpPerUnit: 10,
      attackRange: 1_000,
      attackIntervalTicks: 20,
      weapon: { damageLevels: { impact: 2 }, tags: ['unit'] },
    }]
    battlePlan.towerBases[0].weapon = { damageLevels: { impact: 2 }, tags: ['tower'] }
    battlePlan.towerBases[0].range = 1_000
    const state = createTdSimulation(battlePlan, 'combat-hit')

    stepTdSimulation(battlePlan, state, [buildCommand(battlePlan)])

    expect(state.hitCount).toBe(2)
    expect(state.damageByType).toEqual({ impact: 4 })
    expect(state.enemies[0].hp).toBe(96)
  })

  it('lets healer deployments spend deterministic charges on the nearest damaged allied squad', () => {
    const battlePlan = plan()
    battlePlan.wave.groups[0].speedPerSecond = 0.001
    const patient = {
      ...assaultDeployment(battlePlan),
      id: 'a-patient',
      cohortId: 'a-patient',
      unitInstanceIds: ['patient-1'],
      count: 1,
      speedPerSecond: 0,
      nodeId: battlePlan.battlefield.deploymentNodeId,
      maxHpPerUnit: 10,
    }
    const healer = {
      ...assaultDeployment(battlePlan),
      id: 'z-healer',
      cohortId: 'z-healer',
      unitId: 'unit-medical-healer',
      unitInstanceIds: ['healer-1'],
      count: 1,
      speedPerSecond: 0,
      nodeId: battlePlan.battlefield.deploymentNodeId,
      maxHpPerUnit: 12,
      healing: { range: 140, intervalTicks: 20, amountPerUnit: 10_000, chargesPerUnit: 2 },
    }
    battlePlan.deployments = [patient, healer]
    const state = createTdSimulation(battlePlan, 'medical-healing')
    state.squads.find(squad => squad.deploymentId === patient.id)!.hp = 1

    stepTdSimulation(battlePlan, state, [])

    expect(state.squads.find(squad => squad.deploymentId === patient.id)!.hp).toBe(10)
    expect(state.squads.find(squad => squad.deploymentId === healer.id)!.healingChargesRemaining).toBe(1)
  })

  it('resolves tower loadouts by priority/id, consumes exact stock, and restores deterministically', () => {
    const battlePlan = plan()
    battlePlan.maxTicks = 3
    configureSingleDurableEnemy(battlePlan, ['melee'])
    battlePlan.wave.groups[0].weapon = { damageLevels: { impact: 100 }, tags: ['arrow'] }
    battlePlan.combat.equipment.push(
      {
        id: 'weapon-tower-priority',
        name: 'Priority tower weapon',
        kind: 'weapon',
        profile: { damageLevels: { impact: 22 }, tags: ['tower'] },
      },
      {
        id: 'weapon-tower-fallback',
        name: 'Fallback tower weapon',
        kind: 'weapon',
        profile: { damageLevels: { impact: 11 }, tags: ['tower'] },
      },
      {
        id: 'shield-tower',
        name: 'Tower shield',
        kind: 'shield',
        profile: { classId: 'shield', level: 1, tags: ['tower'] },
      },
    )
    battlePlan.towerBases[0].loadouts = [
      {
        id: 'z-priority',
        priority: 10,
        weaponEquipmentId: 'weapon-tower-priority',
        defenseEquipmentId: 'shield-tower',
        equipmentCosts: [
          { equipmentId: 'steel-kit', amount: 2 },
          { equipmentId: 'tower-shield', amount: 1 },
        ],
      },
      {
        id: 'a-priority',
        priority: 10,
        weaponEquipmentId: 'weapon-tower-priority',
        defenseEquipmentId: 'shield-tower',
        equipmentCosts: [
          { equipmentId: 'tower-shield', amount: 1 },
          { equipmentId: 'steel-kit', amount: 2 },
        ],
      },
      {
        id: 'lower-priority',
        priority: 5,
        weaponEquipmentId: 'weapon-tower-fallback',
        equipmentCosts: [{ equipmentId: 'basic-kit', amount: 1 }],
      },
    ]
    battlePlan.equipmentStock = { 'basic-kit': 1, 'steel-kit': 2, 'tower-shield': 1 }
    expect(validateTdBattlePlan(battlePlan)).toEqual([])

    const commands: TdCommand[] = battlePlan.battlefield.buildSpots.map((spot, index) => ({
      ...commandIdentity(battlePlan, index, index),
      kind: 'build-tower',
      spotId: spot.id,
      towerBaseId: battlePlan.towerBases[0].id,
    }))
    const state = createTdSimulation(battlePlan, 'tower-equipment')
    stepTdSimulation(battlePlan, state, [commands[0]])
    expect(state.towers[0]).toMatchObject({
      loadoutId: 'a-priority',
      weaponEquipmentId: 'weapon-tower-priority',
      defenseEquipmentId: 'shield-tower',
      weapon: { damageLevels: { impact: 22 } },
      armor: { classId: 'shield', level: 1 },
      hp: battlePlan.towerBases[0].maxHp,
    })
    expect(state.enemies[0].hp).toBe(978)
    expect(state.equipmentStock).toEqual({ 'basic-kit': 1, 'steel-kit': 0, 'tower-shield': 0 })
    expect(state.equipmentSpent).toEqual({ 'steel-kit': 2, 'tower-shield': 1 })

    const restored = clone(state)
    stepTdSimulation(battlePlan, state, [commands[1]])
    stepTdSimulation(battlePlan, restored, [clone(commands[1])])
    expect(restored).toEqual(state)
    stepTdSimulation(battlePlan, state, [commands[2]])
    expect(state.towers.map(tower => tower.loadoutId)).toEqual([
      'a-priority',
      'lower-priority',
      null,
    ])
    expect(state.towers[2].weapon).toEqual(battlePlan.towerBases[0].weapon)
    expect(state.equipmentStock).toEqual({ 'basic-kit': 0, 'steel-kit': 0, 'tower-shield': 0 })
    expect(state.equipmentSpent).toEqual({ 'basic-kit': 1, 'steel-kit': 2, 'tower-shield': 1 })

    const result = replayTdBattle(battlePlan, 'tower-equipment', commands)
    expect(result.equipmentSpent).toEqual(state.equipmentSpent)
    expect(replayTdBattle(clone(battlePlan), 'tower-equipment', clone(commands))).toEqual(result)
    const aborted = abortTdBattle(battlePlan, 'tower-equipment', [commands[0]], 1)
    expect(aborted).toMatchObject({
      outcome: 'aborted',
      ticks: 1,
      equipmentSpent: { 'steel-kit': 2, 'tower-shield': 1 },
    })
  })

  it('terminates central castle defense with both victory and destruction outcomes', () => {
    const victoryPlan = plan()
    victoryPlan.wave.groups[0].count = 1
    victoryPlan.wave.groups[0].maxHp = 1
    victoryPlan.towerBases[0].range = 1_000
    const victory = replayTdBattle(victoryPlan, 'castle-victory', [buildCommand(victoryPlan)])
    expect(victory).toMatchObject({ outcome: 'victory', terminalReason: 'all-waves-defeated' })
    expect(victory.objectiveMaxHp).toBe(victoryPlan.objective.maxHp)

    const defeatPlan = plan()
    defeatPlan.objective.maxHp = 1
    defeatPlan.wave.groups[0].count = 1
    defeatPlan.wave.groups[0].speedPerSecond = 100_000
    const defeat = replayTdBattle(defeatPlan, 'castle-defeat', [])
    expect(defeat).toMatchObject({ outcome: 'defeat', terminalReason: 'objective-destroyed' })
  })

  it('runs the naval enemy path through the same defense terminal funnel', () => {
    const naval = plan('north-ship-defense')
    expect(naval.wave.groups[0].categoryIds).toEqual(expect.arrayContaining(['naval']))
    naval.wave.groups[0].count = 1
    naval.wave.groups[0].maxHp = 1
    naval.towerBases[0].range = 1_000

    expect(replayTdBattle(naval, 'naval-defense', [buildCommand(naval)])).toMatchObject({
      outcome: 'victory',
      terminalReason: 'all-waves-defeated',
    })
  })

  it('terminates assault with enemy-fort victory and deployment defeat', () => {
    const victoryPlan = plan('central-fort-assault')
    victoryPlan.wave.groups[0].count = 1
    victoryPlan.wave.groups[0].maxHp = 1
    victoryPlan.wave.groups[0].attackRange = 0
    victoryPlan.objective.maxHp = 1
    victoryPlan.deployments[0].attackRange = 1_000
    victoryPlan.deployments[0].weapon = { damageLevels: { impact: 100 }, tags: ['unit'] }
    const victory = replayTdBattle(victoryPlan, 'assault-victory', [])
    expect(victory).toMatchObject({ outcome: 'victory', terminalReason: 'objective-destroyed' })
    expect(victory.deployments[0].cohortId).toBe(victoryPlan.deployments[0].cohortId)

    const defeatPlan = plan('central-fort-assault')
    defeatPlan.wave.groups[0].count = 1
    defeatPlan.wave.groups[0].maxHp = 1_000
    defeatPlan.wave.groups[0].attackRange = 1_000
    defeatPlan.wave.groups[0].attackIntervalTicks = 1
    defeatPlan.wave.groups[0].weapon = { damageLevels: { impact: 100 }, tags: ['alliance'] }
    defeatPlan.deployments[0].count = 1
    defeatPlan.deployments[0].unitInstanceIds = ['assault-defeat-unit']
    defeatPlan.deployments[0].maxHpPerUnit = 1
    defeatPlan.deployments[0].attackRange = 0
    defeatPlan.deployments[0].weapon = { damageLevels: { impact: 0 }, tags: ['unit'] }
    const defeat = replayTdBattle(defeatPlan, 'assault-defeat', [])
    expect(defeat).toMatchObject({ outcome: 'defeat', terminalReason: 'all-deployments-defeated' })
  })

  it('terminates at the configured tick cap instead of hanging', () => {
    const battlePlan = plan()
    battlePlan.maxTicks = 1
    battlePlan.wave.groups[0].speedPerSecond = 0.001
    const result = replayTdBattle(battlePlan, 'tick-cap', [])
    expect(result).toMatchObject({ outcome: 'error', terminalReason: 'tick-cap', ticks: 1 })
  })

  it('does not mutate frozen plan/config inputs', () => {
    const battlePlan = freezeDeep(plan())
    const before = JSON.stringify(battlePlan)
    expect(() => replayTdBattle(battlePlan, 'immutable', [])).not.toThrow()
    expect(JSON.stringify(battlePlan)).toBe(before)
    expect(config.td.tickMs).toBe(50)
  })

  it('canonicalizes rules identity and exposes changed rules as a distinct rejection signal', () => {
    const original = createTdRulesIdentity(config.schemaVersion, config.combat, config.td)
    const reorderedCombat = Object.fromEntries(
      Object.entries(clone(config.combat)).reverse(),
    ) as typeof config.combat
    const reorderedTd = Object.fromEntries(
      Object.entries(clone(config.td)).reverse(),
    ) as typeof config.td
    expect(createTdRulesIdentity(config.schemaVersion, reorderedCombat, reorderedTd)).toEqual(original)
    expect(digestTdValue({ z: 1, nested: { b: 2, a: 3 } }))
      .toBe(digestTdValue({ nested: { a: 3, b: 2 }, z: 1 }))

    const changedTd = clone(config.td)
    changedTd.towerBases![0].cost += 1
    const changed = createTdRulesIdentity(config.schemaVersion, config.combat, changedTd)
    expect(changed).not.toEqual(original)

    const stalePlan = plan()
    stalePlan.rulesIdentity = clone(changed)
    stalePlan.sessionId = `ee:1:${stalePlan.id}:stale-rules`
    const campaign = new EmpiresEndgameEngine(config)
    expect(campaign.beginMinigame({
      kind: 'td',
      id: stalePlan.sessionId,
      sequence: 1,
      attempt: 0,
      rulesIdentity: clone(changed),
      seed: 'stale-rules',
      plan: stalePlan,
      origin: {
        returnPhase: 'cards',
        context: { kind: 'manual', sourceId: 'rules-identity-spec' },
      },
    })).toEqual({
      ok: false,
      message: 'Minigame rules identity does not match the active configuration and plan.',
    })

    const activePlan = plan()
    activePlan.sessionId = `ee:1:${activePlan.id}:active-rules`
    const activeCampaign = new EmpiresEndgameEngine(config)
    expect(activeCampaign.beginMinigame({
      kind: 'td',
      id: activePlan.sessionId,
      sequence: 1,
      attempt: 0,
      rulesIdentity: clone(activePlan.rulesIdentity),
      seed: 'active-rules',
      plan: activePlan,
      origin: {
        returnPhase: 'cards',
        context: { kind: 'manual', sourceId: 'reload-rules-spec' },
      },
    })).toMatchObject({ ok: true })
    const changedConfig = clone(config)
    changedConfig.td.towerBases![0].cost += 1
    expect(() => new EmpiresEndgameEngine(changedConfig, activeCampaign.snapshot()))
      .toThrow(/active minigame rules identity does not match the loaded configuration/i)

    const invalidIdentity = plan()
    invalidIdentity.rulesIdentity.rulesDigest = ''
    expect(validateTdBattlePlan(invalidIdentity)).toContain('rules identity is invalid')
  })

  it('replays the same plan/seed/log and full digest across frame cadences and reload clones', () => {
    const battlePlan = plan()
    const commands = createTdPolicyCommandLog(battlePlan, 'balanced')
    const direct = replayTdBattle(battlePlan, 'frame-cadence', commands)
    const singleTickFrames = runWithFrameCadence(battlePlan, 'frame-cadence', commands, [50])
    const mixedFrames = runWithFrameCadence(
      clone(battlePlan),
      'frame-cadence',
      clone(commands),
      [10, 40, 25, 25],
      75,
    )

    expect(mixedFrames).toEqual(singleTickFrames)
    expect(digestTdValue(mixedFrames)).toBe(digestTdValue(singleTickFrames))
    expect(direct.ticks).toBe(singleTickFrames.tick)
    expect(replayTdBattle(clone(battlePlan), 'frame-cadence', clone(commands))).toEqual(direct)
    expect(digestTdValue(replayTdBattle(battlePlan, 'frame-cadence', commands)))
      .toBe(digestTdValue(direct))
  })

  it('bounds foreground catch-up and discards hidden-tab wall-time backlog', () => {
    const capped = consumeTdFrameTime(0, 60_000, 50, 8)
    expect(capped.ticks).toBe(8)
    expect(capped.accumulatorMs).toBeGreaterThanOrEqual(0)
    expect(capped.accumulatorMs).toBeLessThan(50)
    expect(consumeTdFrameTime(capped.accumulatorMs, 0, 50, 8).ticks).toBe(0)
    expect(consumeTdFrameTime(25, Number.POSITIVE_INFINITY, 50, 8)).toEqual({
      ticks: 0,
      accumulatorMs: 25,
    })
    expect(consumeTdFrameTime(25, -1_000, 50, 8)).toEqual({ ticks: 0, accumulatorMs: 25 })
  })

  it.each([11, 22, 33])('terminates headless for seed %s under every QA policy', (seed) => {
    for (const policy of TD_QA_POLICIES) {
      const result = resolveTdWithPolicy(plan(), seed, policy)
      expect(result.terminalReason).not.toBeNull()
      expect(result.ticks).toBeLessThanOrEqual(config.td.maxTicks!)
      expect(result.terminalReason).not.toBe('invalid-command')
      expect(result.commandLog.length).toBeLessThanOrEqual(config.td.maxCommands!)
    }
  })
})
