import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../../public/empires-endgame/game-config.json'
import { createEmpiresQaScenarios } from '../qa'
import type { EmpiresEndgameConfig, TdBattlePlan } from '../types'
import { createTdPolicyCommandLog, resolveTdWithPolicy } from './qa'

function defaultConfig(): EmpiresEndgameConfig {
  return JSON.parse(JSON.stringify(defaultConfigJson)) as EmpiresEndgameConfig
}

function planFor(
  scenario: 'battle-defense' | 'battle-assault' | 'battle-swamp' | 'battle-forest' | 'battle-north' | 'battle-desert',
): TdBattlePlan {
  const fixture = createEmpiresQaScenarios(defaultConfig(), { seed: `td-qa-${scenario}` })[scenario]
  if (!fixture.snapshot.minigame) throw new Error(`Missing ${scenario} minigame.`)
  return fixture.snapshot.minigame.plan
}

describe('TD deterministic QA policies', () => {
  it('authors bounded, identified and replayable central-defense commands', () => {
    const plan = planFor('battle-defense')
    const first = createTdPolicyCommandLog(plan, 'balanced')
    const second = createTdPolicyCommandLog(plan, 'balanced')

    expect(second).toEqual(first)
    expect(first.length).toBeGreaterThan(0)
    expect(first.length).toBeLessThanOrEqual(plan.maxCommands)
    expect(first[0]).toMatchObject({
      kind: 'build-tower',
      towerBaseId: plan.towerBases[0].id,
      sequence: 0,
      sessionId: plan.sessionId,
      planId: plan.id,
    })
    expect(first.every((command, index) => (
      command.sequence === index
      && command.sessionId === plan.sessionId
      && command.planId === plan.id
      && (index === 0 || command.tick >= first[index - 1].tick)
    ))).toBe(true)
    expect(resolveTdWithPolicy(plan, 'td-qa-replay', 'balanced').terminalReason)
      .not.toBe('invalid-command')
  })

  it('uses completed regional grades without inventing assault or northern upgrades', () => {
    expect(createTdPolicyCommandLog(planFor('battle-assault'), 'balanced')).toEqual([])

    for (const scenario of ['battle-swamp', 'battle-forest', 'battle-desert'] as const) {
      const plan = planFor(scenario)
      const commands = createTdPolicyCommandLog(plan, 'balanced')
      expect(commands.length).toBeGreaterThan(0)
      expect(commands.some(command => command.kind === 'upgrade-tower')).toBe(true)
      expect(resolveTdWithPolicy(plan, `td-qa-${scenario}`, 'balanced').terminalReason)
        .not.toBe('invalid-command')
    }

    const north = planFor('battle-north')
    const northCommands = createTdPolicyCommandLog(north, 'balanced')
    expect(northCommands.length).toBeGreaterThan(0)
    expect(northCommands.every(command => command.kind === 'build-tower')).toBe(true)
    expect(north.gradeChoices.every(set => set.availability === 'notApplicable')).toBe(true)
    expect(resolveTdWithPolicy(north, 'td-qa-battle-north', 'balanced').terminalReason)
      .not.toBe('invalid-command')
  })

  it('honors the plan command cap while authoring a policy log', () => {
    const plan = structuredClone(planFor('battle-defense'))
    plan.maxCommands = 1

    expect(createTdPolicyCommandLog(plan, 'balanced')).toHaveLength(1)
  })
})
