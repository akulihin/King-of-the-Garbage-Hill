import {
  createTdSimulation,
  replayTdBattle,
  stepTdSimulation,
  tdCommandDisabledReason,
} from './engine'
import type {
  TdBattlePlan,
  TdBattleResult,
  TdCommand,
  TdSimulationState,
  TdTowerBaseDefinition,
  TdTowerChoiceDefinition,
} from './types'

export const TD_QA_POLICIES = ['passive', 'greedy-build', 'balanced'] as const
export type TdQaPolicy = typeof TD_QA_POLICIES[number]

function choiceForGrade(
  plan: TdBattlePlan,
  grade: number,
  choiceIndex: number,
): TdTowerChoiceDefinition | null {
  const set = plan.gradeChoices.find(candidate => (
    candidate.regionId === plan.battlefield.regionId
    && candidate.grade === grade
  ))
  if (!set || set.deferredReason || set.choiceIds.length === 0) return null
  const choiceId = set.choiceIds[choiceIndex % set.choiceIds.length]
  return plan.towerChoices.find(choice => choice.id === choiceId) ?? null
}

function baseForSpot(
  plan: TdBattlePlan,
  spotIndex: number,
): TdTowerBaseDefinition | null {
  if (plan.towerBases.length === 0) return null
  return plan.towerBases[spotIndex % plan.towerBases.length] ?? null
}

/**
 * Build a replayable command stream by checking every command against the same
 * simulator state and legality helper used by the interactive battle.
 */
export function createTdPolicyCommandLog(plan: TdBattlePlan, policy: TdQaPolicy): TdCommand[] {
  if (policy === 'passive' || plan.mode === 'assault') return []
  const spots = plan.battlefield.buildSpots
  if (spots.length === 0 || plan.towerBases.length === 0 || plan.maxCommands <= 0) return []

  const state = createTdSimulation(plan, 'qa-command-authoring')
  const commands: TdCommand[] = []
  const append = (command: TdCommand): boolean => {
    if (commands.length >= plan.maxCommands || state.terminalReason) return false
    const reason = tdCommandDisabledReason(plan, state, command)
    if (reason) return false
    commands.push(command)
    stepTdSimulation(plan, state, [command])
    return !state.terminalReason
  }
  const commandIdentity = (simulation: TdSimulationState) => ({
    tick: simulation.tick,
    sequence: commands.length,
    sessionId: plan.sessionId,
    planId: plan.id,
  })

  const spotCount = policy === 'greedy-build' ? 1 : Math.min(2, spots.length)
  for (let spotIndex = 0; spotIndex < spotCount; spotIndex += 1) {
    const base = baseForSpot(plan, spotIndex)
    if (!base) continue
    append({
      ...commandIdentity(state),
      kind: 'build-tower',
      spotId: spots[spotIndex].id,
      towerBaseId: base.id,
    })
  }

  for (const grade of [1, 2, 3, 4]) {
    const choice = choiceForGrade(plan, grade, policy === 'greedy-build' ? 0 : grade - 1)
    if (!choice) break
    const appended = append({
      ...commandIdentity(state),
      kind: 'upgrade-tower',
      spotId: spots[0].id,
      choiceId: choice.id,
    })
    if (!appended) break
  }
  return commands
}

export function resolveTdWithPolicy(
  plan: TdBattlePlan,
  seed: string | number,
  policy: TdQaPolicy,
): TdBattleResult {
  return replayTdBattle(plan, seed, createTdPolicyCommandLog(plan, policy))
}
