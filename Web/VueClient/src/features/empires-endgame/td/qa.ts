import { replayTdBattle } from './engine'
import type { TdBattlePlan, TdBattleResult, TdCommand } from './types'

export const TD_QA_POLICIES = ['passive', 'greedy-build', 'balanced'] as const
export type TdQaPolicy = typeof TD_QA_POLICIES[number]

function choicesByGrade(plan: TdBattlePlan, grade: number) {
  return plan.towerChoices.filter(choice => choice.grade === grade)
}

export function createTdPolicyCommandLog(plan: TdBattlePlan, policy: TdQaPolicy): TdCommand[] {
  if (policy === 'passive') return []
  const spots = plan.battlefield.buildSpots
  if (spots.length === 0) return []
  const firstChoices = [1, 2, 3, 4].map(grade => choicesByGrade(plan, grade)[0]).filter(Boolean)
  if (policy === 'greedy-build') {
    return firstChoices.map((choice, index) => ({
      tick: index,
      kind: index === 0 ? 'build-tower' as const : 'upgrade-tower' as const,
      spotId: spots[0].id,
      choiceId: choice.id,
    }))
  }

  const commands: TdCommand[] = []
  const gradeOne = choicesByGrade(plan, 1)
  for (let index = 0; index < Math.min(2, spots.length); index += 1) {
    const choice = gradeOne[index % gradeOne.length]
    if (choice) commands.push({
      tick: 0,
      kind: 'build-tower',
      spotId: spots[index].id,
      choiceId: choice.id,
    })
  }
  for (const grade of [2, 3, 4]) {
    const choice = choicesByGrade(plan, grade)[Math.min(grade - 2, 3)]
    if (!choice) continue
    commands.push({
      tick: grade - 1,
      kind: 'upgrade-tower',
      spotId: spots[0].id,
      choiceId: choice.id,
    })
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
