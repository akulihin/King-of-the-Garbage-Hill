import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig } from '../config'
import {
  createTdSimulation,
  digestTdValue,
  replayTdBattle,
  stepTdSimulation,
  validateTdBattlePlan,
} from './engine'
import { createTdPolicyCommandLog, resolveTdWithPolicy, TD_QA_POLICIES } from './qa'
import type { TdBattlePlan, TdCommand, TdSimulationState } from './types'

const config = cloneEmpiresConfig(defaultConfigJson)

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function plan(): TdBattlePlan {
  return {
    id: 'td-spec-plan',
    mode: 'defense',
    scheduledCon: 2,
    threat: 0,
    tickMs: config.td.tickMs!,
    maxTicks: config.td.maxTicks!,
    startingBuildResources: config.td.startingBuildResources!,
    battlefield: clone(config.td.battlefields[0]),
    towerBase: clone(config.td.towerBase!),
    towerChoices: clone(config.td.towers),
    wave: clone(config.td.waves[0]),
    combat: clone(config.combat),
    deployments: [],
  }
}

function freezeDeep<T>(value: T): T {
  if (!value || typeof value !== 'object' || Object.isFrozen(value)) return value
  for (const child of Object.values(value)) freezeDeep(child)
  return Object.freeze(value)
}

function runInFrameChunks(
  battlePlan: TdBattlePlan,
  seed: string | number,
  commandLog: readonly TdCommand[],
  chunks: readonly number[],
): TdSimulationState {
  const state = createTdSimulation(battlePlan, seed)
  let chunkIndex = 0
  while (!state.terminalReason) {
    const ticksThisFrame = chunks[chunkIndex % chunks.length]
    chunkIndex += 1
    for (let offset = 0; offset < ticksThisFrame && !state.terminalReason; offset += 1) {
      stepTdSimulation(
        battlePlan,
        state,
        commandLog.filter(command => command.tick === state.tick),
      )
    }
  }
  return state
}

describe('Empire\'s Endgame deterministic TD engine', () => {
  it('accepts the bundled complete 4x4 defense plan', () => {
    expect(validateTdBattlePlan(plan())).toEqual([])
    expect(config.td.towers.filter(choice => choice.grade === 1)).toHaveLength(4)
    expect(config.td.towers.filter(choice => choice.grade === 2)).toHaveLength(4)
    expect(config.td.towers.filter(choice => choice.grade === 3)).toHaveLength(4)
    expect(config.td.towers.filter(choice => choice.grade === 4)).toHaveLength(4)
  })

  it('rejects executable plans with unknown combat references or disconnected routes', () => {
    const unknownDamage = plan()
    unknownDamage.towerBase.weapon.damageLevels = { missing: 1 }
    expect(validateTdBattlePlan(unknownDamage)).toContain(
      'tower base weapon uses unknown damage type missing',
    )

    const disconnected = plan()
    disconnected.wave.groups[0].routeEdgeIds = ['central-road-2', 'central-road-3']
    expect(validateTdBattlePlan(disconnected)).toContain(
      `group ${disconnected.wave.groups[0].id} route is not contiguous from the spawner`,
    )
  })

  it('advances one configured fixed tick per step at the spawn boundary', () => {
    const battlePlan = plan()
    const state = createTdSimulation(battlePlan, 'fixed-step')

    stepTdSimulation(battlePlan, state)

    expect(state.tick).toBe(1)
    expect(state.elapsedMs).toBe(battlePlan.tickMs)
    expect(state.enemies).toHaveLength(1)
    expect(state.spawnedByGroup['alliance-infantry']).toBe(1)
  })

  it('executes legal sequential tower grades and rejects illegal commands deterministically', () => {
    const battlePlan = plan()
    const spotId = battlePlan.battlefield.buildSpots[0].id
    const choices = [1, 2, 3, 4].map(grade => (
      battlePlan.towerChoices.find(choice => choice.grade === grade)!
    ))
    const legalState = createTdSimulation(battlePlan, 'legal')
    for (let index = 0; index < choices.length; index += 1) {
      stepTdSimulation(battlePlan, legalState, [{
        tick: legalState.tick,
        kind: index === 0 ? 'build-tower' : 'upgrade-tower',
        spotId,
        choiceId: choices[index].id,
      }])
    }
    expect(legalState.towers[0].choiceIds).toEqual(choices.map(choice => choice.id))
    expect(legalState.commandErrors).toEqual([])

    const illegalState = createTdSimulation(battlePlan, 'illegal')
    stepTdSimulation(battlePlan, illegalState, [{
      tick: 0,
      kind: 'build-tower',
      spotId,
      choiceId: choices[1].id,
    }])
    expect(illegalState.terminalReason).toBe('invalid-command')
    expect(illegalState.commandErrors[0].message).toContain('grade-1')
  })

  it('routes tower and deployed-unit hits through the shared combat damage catalog', () => {
    const battlePlan = plan()
    battlePlan.wave.groups[0].count = 1
    battlePlan.wave.groups[0].maxHp = 100
    battlePlan.wave.groups[0].speedPerSecond = 0.001
    battlePlan.deployments = [{
      id: 'capital:unit-light',
      cityId: 'capital',
      unitId: 'unit-light',
      count: 1,
      nodeId: battlePlan.battlefield.deploymentNodeId,
      maxHpPerUnit: 10,
      attackRange: 1_000,
      attackIntervalTicks: 20,
      weapon: { damageLevels: { impact: 2 }, tags: ['unit'] },
      armor: null,
    }]
    const state = createTdSimulation(battlePlan, 'combat-hit')
    const gradeOne = battlePlan.towerChoices.find(choice => choice.grade === 1)!

    stepTdSimulation(battlePlan, state, [{
      tick: 0,
      kind: 'build-tower',
      spotId: battlePlan.battlefield.buildSpots[0].id,
      choiceId: gradeOne.id,
    }])

    expect(state.hitCount).toBe(2)
    expect(state.damageByType).toEqual({ impact: 4 })
    expect(Object.values(state.damageByType).reduce((sum, value) => sum + value, 0)).toBe(4)
    expect(state.enemies[0].hp).toBe(96)
  })

  it('lets enemies destroy towers and applies stacked tower durability bonuses', () => {
    const battlePlan = plan()
    const group = battlePlan.wave.groups[0]
    group.count = 1
    group.maxHp = 1_000
    group.speedPerSecond = 0.001
    group.attackRange = 1_000
    group.attackIntervalTicks = 1
    group.weapon = { damageLevels: { impact: 150 }, tags: ['alliance'] }
    battlePlan.towerBase.weapon = { damageLevels: { impact: 0 }, tags: ['tower'] }
    const spotId = battlePlan.battlefield.buildSpots[0].id
    const heightChoice = battlePlan.towerChoices.find(choice => choice.id === 'tower-g1-height')!
    const thickChoice = battlePlan.towerChoices.find(choice => choice.id === 'tower-g1-thick')!

    const heightState = createTdSimulation(battlePlan, 'fragile-tower')
    stepTdSimulation(battlePlan, heightState, [{
      tick: 0,
      kind: 'build-tower',
      spotId,
      choiceId: heightChoice.id,
    }])
    expect(heightState.towers).toEqual([])
    expect(heightState.castleHp).toBe(battlePlan.battlefield.castleMaxHp)

    const thickState = createTdSimulation(battlePlan, 'durable-tower')
    stepTdSimulation(battlePlan, thickState, [{
      tick: 0,
      kind: 'build-tower',
      spotId,
      choiceId: thickChoice.id,
    }])
    expect(thickState.towers).toHaveLength(1)
    expect(thickState.towers[0].hp).toBe(50)
    expect(thickState.castleHp).toBe(battlePlan.battlefield.castleMaxHp)
  })

  it('terminates with both castle victory and castle destruction outcomes', () => {
    const victoryPlan = plan()
    victoryPlan.wave.groups[0].count = 1
    victoryPlan.wave.groups[0].maxHp = 1
    const gradeOne = victoryPlan.towerChoices.find(choice => choice.grade === 1)!
    const victory = replayTdBattle(victoryPlan, 'victory', [{
      tick: 0,
      kind: 'build-tower',
      spotId: victoryPlan.battlefield.buildSpots[0].id,
      choiceId: gradeOne.id,
    }])
    expect(victory.outcome).toBe('victory')
    expect(victory.terminalReason).toBe('all-waves-defeated')

    const defeatPlan = plan()
    defeatPlan.battlefield.castleMaxHp = 1
    defeatPlan.wave.groups[0].count = 1
    defeatPlan.wave.groups[0].speedPerSecond = 100_000
    const defeat = replayTdBattle(defeatPlan, 'defeat', [])
    expect(defeat.outcome).toBe('defeat')
    expect(defeat.terminalReason).toBe('castle-destroyed')
  })

  it('terminates at the configured tick cap instead of hanging', () => {
    const battlePlan = plan()
    battlePlan.maxTicks = 1
    battlePlan.wave.groups[0].speedPerSecond = 0.001
    const result = replayTdBattle(battlePlan, 'tick-cap', [])
    expect(result.outcome).toBe('error')
    expect(result.terminalReason).toBe('tick-cap')
    expect(result.ticks).toBe(1)
  })

  it('does not mutate frozen plan/config inputs', () => {
    const battlePlan = freezeDeep(plan())
    const before = JSON.stringify(battlePlan)
    expect(() => replayTdBattle(battlePlan, 'immutable', [])).not.toThrow()
    expect(JSON.stringify(battlePlan)).toBe(before)
    expect(config.td.tickMs).toBe(50)
  })

  it('replays identical plan, seed, and command log to an identical result and digest', () => {
    const battlePlan = plan()
    const commands = createTdPolicyCommandLog(battlePlan, 'balanced')
    const first = replayTdBattle(battlePlan, 42, commands)
    const second = replayTdBattle(battlePlan, 42, commands)
    expect(second).toEqual(first)
    expect(digestTdValue(second)).toBe(digestTdValue(first))
  })

  it.each([11, 22, 33])('terminates headless for seed %s under every QA policy', (seed) => {
    for (const policy of TD_QA_POLICIES) {
      const result = resolveTdWithPolicy(plan(), seed, policy)
      expect(result.terminalReason).not.toBeNull()
      expect(result.ticks).toBeLessThanOrEqual(config.td.maxTicks!)
      expect(result.terminalReason).not.toBe('invalid-command')
    }
  })

  it('produces the same state under different rendering frame chunk sizes', () => {
    const battlePlan = plan()
    const commands = createTdPolicyCommandLog(battlePlan, 'balanced')
    const singleTickFrames = runInFrameChunks(battlePlan, 'frame-chunks', commands, [1])
    const mixedFrames = runInFrameChunks(battlePlan, 'frame-chunks', commands, [4, 2, 1])
    expect(mixedFrames).toEqual(singleTickFrames)
    expect(replayTdBattle(battlePlan, 'frame-chunks', commands).ticks).toBe(singleTickFrames.tick)
  })
})
