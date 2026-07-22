import { describe, expect, it } from 'vitest'
import { CLASH_SCAFFOLD } from './catalog'
import {
  CLASH_QA_POLICIES,
  createClashQaPlan,
  digestClashQaResult,
  resolveClashWithPolicy,
} from './qa'

function config() {
  return JSON.parse(JSON.stringify(CLASH_SCAFFOLD)) as typeof CLASH_SCAFFOLD
}

describe('Clash headless QA', () => {
  it.each(['clash-seed-1', 'clash-seed-2', 'clash-seed-3'])('terminates every policy for %s', (seed) => {
    const rules = config()
    const plan = createClashQaPlan(rules, seed)
    for (const policy of CLASH_QA_POLICIES) {
      const result = resolveClashWithPolicy(plan, seed, policy)
      expect(['elimination', 'turn-cap']).toContain(result.terminalReason)
      expect(result.turns).toBeLessThanOrEqual(plan.maxTurns)
    }
  })

  it('repeats the exact result digest from plan, seed, and turn log', () => {
    const rules = config()
    const plan = createClashQaPlan(rules, 'repeatable')
    const left = resolveClashWithPolicy(plan, 'repeatable', 'balanced')
    const right = resolveClashWithPolicy(plan, 'repeatable', 'balanced')
    expect(right).toEqual(left)
    expect(digestClashQaResult(right)).toBe(digestClashQaResult(left))
  })
})
