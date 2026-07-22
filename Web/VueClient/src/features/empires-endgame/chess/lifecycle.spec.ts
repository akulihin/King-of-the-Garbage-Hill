import { describe, expect, it } from 'vitest'
import bundledConfigJson from '../../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, migrateEmpiresConfig, validateEmpiresConfig } from '../config'
import { EmpiresEndgameEngine } from '../engine'
import { exportEmpiresCampaign, importEmpiresCampaign } from '../persistence'
import type { EmpiresCampaignState, EmpiresChessMinigameSession, EmpiresEndgameConfig } from '../types'
import {
  createChessCommand,
  createChessState,
  resolveChess,
} from './engine'

function config(): EmpiresEndgameConfig {
  return cloneEmpiresConfig(bundledConfigJson)
}

function empireEngine(value: EmpiresEndgameConfig): EmpiresEndgameEngine {
  const state = new EmpiresEndgameEngine(value).snapshot()
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = value.empire.daysPerPhase
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  state.questRuntime.activeMandatoryQuestId = null
  state.questRuntime.mandatoryQueue = []
  return new EmpiresEndgameEngine(value, state)
}

function activeChess(value: EmpiresEndgameEngine): EmpiresChessMinigameSession {
  const session = value.state.minigame
  if (session?.kind !== 'chess') throw new Error('Expected an active Chess session.')
  return session
}

function loyaltyByCity(value: EmpiresEndgameEngine): Record<string, number> {
  return Object.fromEntries(value.state.empire.cities.map(city => [city.id, city.loyalty]))
}

describe('Empire\'s Endgame Chess campaign lifecycle', () => {
  it('migrates schema v18 fail-closed and validates bundled entry, settlement, and card references', () => {
    const legacy = structuredClone(bundledConfigJson) as unknown as Record<string, unknown>
    legacy.schemaVersion = 18
    delete legacy.chess
    const untouched = structuredClone(legacy)

    const migrated = migrateEmpiresConfig(legacy) as EmpiresEndgameConfig
    expect(legacy).toEqual(untouched)
    expect(migrated).toMatchObject({
      schemaVersion: 19,
      chess: { enabled: false, setup: [] },
    })
    expect(migrateEmpiresConfig(migrated)).toEqual(migrated)
    expect(() => validateEmpiresConfig(migrated)).not.toThrow()

    const bundled = config()
    expect(bundled.chess).toMatchObject({
      enabled: true,
      entryCapitalSiteId: 'capital-coliseum',
      settlement: {
        victoryGold: 2_500,
        victoryKnowledge: 1_000,
        defeatAllCityLoyaltyDelta: -1,
        abortAllCityLoyaltyDelta: -1,
      },
    })
    expect(bundled.chess.setup).toHaveLength(31)
    expect(bundled.chess.setup.find(piece => piece.id === 'black-anton')).toMatchObject({
      square: 'g8',
      sourceDefinitionId: 'card-spades-jack',
      anton: true,
    })

    const danglingSite = config()
    danglingSite.chess.entryCapitalSiteId = 'missing-coliseum'
    expect(() => validateEmpiresConfig(danglingSite)).toThrow(/chess.*capital site/i)
    const danglingResource = config()
    danglingResource.chess.settlement.goldResourceId = 'missing-gold'
    expect(() => validateEmpiresConfig(danglingResource)).toThrow(/chess.*resource/i)
    const danglingCard = config()
    danglingCard.chess.setup[0].sourceDefinitionId = 'missing-card'
    expect(() => validateEmpiresConfig(danglingCard)).toThrow(/chess.*unknown card/i)
    expect(() => migrateEmpiresConfig({ ...migrated, schemaVersion: 20 })).toThrow(/future.*20/i)
  })

  it('starts from the live Coliseum and settles a victory exactly once', () => {
    const value = config()
    value.chess.setup = [
      { id: 'white-king', side: 'white', role: 'king', square: 'a1' },
      { id: 'white-rook', side: 'white', role: 'rook', square: 'g1' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ]
    validateEmpiresConfig(value)
    const game = empireEngine(value)
    const goldBefore = game.state.empire.resources.gold ?? 0
    const knowledgeBefore = game.state.empire.resources.knowledge ?? 0

    expect(game.chessEntryBlockedReason()).toBeNull()
    expect(game.activateCapitalSite('capital-coliseum')).toMatchObject({ ok: true })
    const session = activeChess(game)
    expect(session.origin).toEqual({
      returnPhase: 'empire',
      context: {
        kind: 'capital-chess',
        capitalSiteId: 'capital-coliseum',
        cityId: value.governance.capital.cityId,
        con: game.state.con,
      },
    })

    const chess = createChessState(session.plan)
    const command = createChessCommand(session.plan, chess, {
      kind: 'move',
      from: 'g1',
      to: 'g8',
    })
    const result = resolveChess(session.plan, session.seed, [command])
    expect(result).toMatchObject({ outcome: 'white-win', terminalReason: 'black-army-captured' })
    expect(game.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(game.state.empire.resources.gold).toBe(goldBefore + 2_500)
    expect(game.state.empire.resources.knowledge).toBe(knowledgeBefore + 1_000)
    expect(game.state.phase).toBe('empire')
    expect(game.state.minigameResultLog).toHaveLength(1)

    const settled = game.snapshot()
    expect(game.resolveMinigame(result)).toMatchObject({
      ok: true,
      message: expect.stringMatching(/already resolved/i),
    })
    expect(game.snapshot()).toEqual(settled)
  })

  it('applies the authored all-city loyalty consequence on defeat and abort', () => {
    const defeatConfig = config()
    defeatConfig.chess.setup = [
      { id: 'white-king', side: 'white', role: 'king', square: 'a1' },
      { id: 'black-rook-a2', side: 'black', role: 'rook', square: 'a2' },
      { id: 'black-rook-b2', side: 'black', role: 'rook', square: 'b2' },
      { id: 'black-anton', side: 'black', role: 'knight', square: 'g8', anton: true },
    ]
    validateEmpiresConfig(defeatConfig)
    const defeated = empireEngine(defeatConfig)
    const beforeDefeat = loyaltyByCity(defeated)
    expect(defeated.startChessMatch()).toMatchObject({ ok: true })
    const defeatSession = activeChess(defeated)
    const result = resolveChess(defeatSession.plan, defeatSession.seed, [])
    expect(result).toMatchObject({ outcome: 'black-win', terminalReason: 'white-checkmated' })
    expect(defeated.resolveMinigame(result)).toMatchObject({ ok: true })
    expect(loyaltyByCity(defeated)).toEqual(Object.fromEntries(
      Object.entries(beforeDefeat).map(([cityId, loyalty]) => [cityId, loyalty - 1]),
    ))

    const aborted = empireEngine(config())
    const beforeAbort = loyaltyByCity(aborted)
    expect(aborted.startChessMatch()).toMatchObject({ ok: true })
    const abort = aborted.abortMinigame()
    expect(abort, abort.message).toMatchObject({ ok: true })
    expect(loyaltyByCity(aborted)).toEqual(Object.fromEntries(
      Object.entries(beforeAbort).map(([cityId, loyalty]) => [cityId, loyalty - 1]),
    ))
    expect(aborted.state.minigameResultLog.at(-1)?.result).toMatchObject({
      kind: 'chess',
      outcome: 'aborted',
    })
  })

  it('restores immutable active sessions under save v18 and rejects stale rules or origin evidence', () => {
    const value = config()
    const game = empireEngine(value)
    expect(game.startChessMatch()).toMatchObject({ ok: true })
    const original = activeChess(game)
    const envelope = exportEmpiresCampaign(game.snapshot())
    expect(envelope.schemaVersion).toBe(18)
    expect(envelope.state.schemaVersion).toBe(18)

    const restored = new EmpiresEndgameEngine(
      value,
      importEmpiresCampaign(envelope, value.id),
    )
    expect(activeChess(restored)).toMatchObject({
      id: original.id,
      seed: original.seed,
      plan: original.plan,
      attempt: original.attempt + 1,
    })

    const changedRules = config()
    changedRules.chess.settlement.victoryGold += 1
    expect(() => new EmpiresEndgameEngine(changedRules, game.snapshot())).toThrow(/rules|configuration/i)

    const staleOrigin = game.snapshot()
    if (staleOrigin.minigame?.kind !== 'chess') throw new Error('Expected Chess snapshot.')
    staleOrigin.empire.flags['capitalSiteLastUsed:capital-coliseum'] = staleOrigin.con - 1
    expect(() => new EmpiresEndgameEngine(value, staleOrigin)).toThrow(/Chess origin/i)

    const legacyState = empireEngine(value).snapshot() as unknown as EmpiresCampaignState
    ;(legacyState as unknown as { schemaVersion: number }).schemaVersion = 16
    const legacyEnvelope = {
      schemaVersion: 16,
      savedAt: '2026-07-21T00:00:00.000Z',
      state: legacyState,
    }
    const migratedState = importEmpiresCampaign(legacyEnvelope, value.id)
    expect(new EmpiresEndgameEngine(value, migratedState).state.schemaVersion).toBe(18)
  })
})
