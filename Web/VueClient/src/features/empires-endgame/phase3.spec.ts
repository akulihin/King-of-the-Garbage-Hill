import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig } from './config'
import { EmpiresEndgameEngine } from './engine'
import { createEmpiresQaScenarios } from './qa'
import { replayTdBattle } from './td/engine'
import type {
  EmpiresEndgameConfig,
  EmpiresMinigameSession,
  TdBattleResult,
} from './types'

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function config(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(defaultConfigJson)
}

function shortSession(value: EmpiresEndgameConfig, suffix: string): EmpiresMinigameSession {
  const source = createEmpiresQaScenarios(value, { seed: `phase3-${suffix}` })['battle-defense']
    .snapshot.minigame!
  const session = clone(source)
  session.id = `phase3-session-${suffix}`
  session.plan.id = `phase3-plan-${suffix}`
  session.plan.sessionId = session.id
  session.plan.maxTicks = 1
  session.plan.deployments = []
  session.seed = `phase3-seed-${suffix}`
  session.attempt = 0
  session.origin = {
    returnPhase: 'cards',
    context: { kind: 'manual', sourceId: `phase3-${suffix}` },
  }
  return session
}

function resolveSession(
  engine: EmpiresEndgameEngine,
  session: EmpiresMinigameSession,
): TdBattleResult {
  expect(engine.beginMinigame(session)).toMatchObject({ ok: true })
  const result = replayTdBattle(session.plan, session.seed, [])
  expect(engine.resolveMinigame(result)).toMatchObject({ ok: true })
  return result
}

describe('Empire\'s Endgame Phase 3 minigame retention', () => {
  it('bounds complete result history and retains rolling audit identity across reload', () => {
    const value = config()
    value.td.resultLogLimit = 1
    const engine = new EmpiresEndgameEngine(value)
    const first = shortSession(value, 'first')
    const firstResult = resolveSession(engine, first)
    const second = shortSession(value, 'second')
    resolveSession(engine, second)

    expect(engine.state.minigameResultLog).toHaveLength(1)
    expect(engine.state.minigameResultLog[0].sessionId).toBe(second.id)
    expect(engine.state.minigameResultCompaction).toMatchObject({
      evictedCount: 1,
      lastSessionId: first.id,
      lastRulesDigest: first.rulesIdentity.rulesDigest,
    })
    expect(engine.state.minigameResultCompaction.historyDigest).toMatch(/^[0-9a-f]{16}$/)

    expect(engine.resolveMinigame(firstResult)).toEqual({
      ok: false,
      message: 'No minigame session is active.',
    })
    expect(engine.state.minigameResultCompaction.evictedCount).toBe(1)

    const restored = new EmpiresEndgameEngine(value, engine.snapshot())
    expect(restored.state.minigameResultLog).toEqual(engine.state.minigameResultLog)
    expect(restored.state.minigameResultCompaction).toEqual(engine.state.minigameResultCompaction)
  })
})
