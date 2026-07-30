import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { signalrService } from 'src/services/signalr'
import type {
  ClashResolutionEvent,
  ClashUnitState,
  ClashVisualUnitOverride,
} from 'src/features/clash/types'
import { useClashStore } from './clash'

function unit(instanceId: string): ClashUnitState {
  return {
    instanceId,
    definitionId: 'legionary',
    ownerId: 'host',
    ownerSide: 'Host',
    boardRow: 4,
    column: 1,
    hp: 5,
    maxHp: 5,
    attack: 2,
    speed: 4,
    shieldCharges: 0,
    dodgeCharges: 0,
    bleedStacks: 0,
    rangedReadyClash: 0,
    alive: true,
    deployed: true,
    isHidden: false,
    diesToAoe: false,
  }
}

function event(
  sequence: number,
  type: string,
  actorUnitInstanceId: string | null,
  targetUnitInstanceId: string | null,
  amount = 0,
): ClashResolutionEvent {
  return {
    sequence,
    type,
    actorUnitInstanceId,
    targetUnitInstanceId,
    speed: 4,
    startOffsetMs: 0,
    impactOffsetMs: 100,
    amount,
    fromBoardRow: type === 'Advance' ? 4 : null,
    toBoardRow: type === 'Advance' ? 5 : null,
    column: 1,
    message: '',
  }
}

describe('Clash resolution visual synchronization', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('applies damage, death, and advance only when their impacts fire', () => {
    const store = useClashStore()
    const target = unit('target')
    const attacker = unit('attacker')
    const visual = (snapshot: ClashUnitState): ClashVisualUnitOverride => ({
      snapshot,
      hp: snapshot.hp,
      alive: snapshot.alive,
      boardRow: snapshot.boardRow,
      column: snapshot.column,
      shieldCharges: snapshot.shieldCharges,
      dodgeCharges: snapshot.dodgeCharges,
      bleedStacks: snapshot.bleedStacks,
      animation: 'idle',
      animationSequence: 0,
    })
    store.visualOverrides = new Map([
      [target.instanceId, visual(target)],
      [attacker.instanceId, visual(attacker)],
    ])

    expect(store.visualOverrides.get(target.instanceId)?.hp).toBe(5)
    expect(store.visualOverrides.get(attacker.instanceId)?.boardRow).toBe(4)

    store.impactTimelineEvent(event(1, 'Damage', attacker.instanceId, target.instanceId, 3))
    expect(store.visualOverrides.get(target.instanceId)).toMatchObject({
      hp: 2,
      alive: true,
      animation: 'hit',
    })

    store.impactTimelineEvent(event(2, 'Death', null, target.instanceId))
    expect(store.visualOverrides.get(target.instanceId)).toMatchObject({
      hp: 0,
      alive: false,
      animation: 'death',
    })

    store.impactTimelineEvent(event(3, 'Advance', attacker.instanceId, null))
    expect(store.visualOverrides.get(attacker.instanceId)).toMatchObject({
      boardRow: 5,
      column: 1,
      animation: 'advance',
    })
  })

  it('applies shield, dodge, and bleed state only at their impact events', () => {
    const store = useClashStore()
    const target = {
      ...unit('target'),
      shieldCharges: 1,
      dodgeCharges: 1,
    }
    store.visualOverrides = new Map([[
      target.instanceId,
      {
        snapshot: target,
        hp: target.hp,
        alive: target.alive,
        boardRow: target.boardRow,
        column: target.column,
        shieldCharges: target.shieldCharges,
        dodgeCharges: target.dodgeCharges,
        bleedStacks: target.bleedStacks,
        animation: 'idle',
        animationSequence: 0,
      },
    ]])

    store.impactTimelineEvent(event(1, 'Block', 'attacker', target.instanceId))
    store.impactTimelineEvent(event(2, 'Dodge', 'attacker', target.instanceId))
    store.impactTimelineEvent(event(3, 'BleedApplied', 'attacker', target.instanceId, 1))

    expect(store.visualOverrides.get(target.instanceId)).toMatchObject({
      shieldCharges: 0,
      dodgeCharges: 0,
      bleedStacks: 1,
    })
  })

  it('does not replay latestResolution when reconnecting after combat', () => {
    const store = useClashStore()
    store.initCallbacks()
    signalrService.onClashState?.({
      gameId: 'reconnect',
      revision: 9,
      phase: 'GuestReinforcement',
      width: 5,
      length: 5,
      latestResolution: {
        gameId: 'reconnect',
        revision: 9,
        clashNumber: 1,
        startedAtUtc: new Date(Date.now() - 10_000).toISOString(),
        durationMs: 4000,
        events: [event(1, 'Damage', 'a', 'b', 2)],
        finalUnits: [],
        winnerId: null,
        isDraw: false,
        terminalReason: null,
      },
    } as never)

    expect(store.timelinePlaying).toBe(false)
    expect(store.gameState?.phase).toBe('GuestReinforcement')
  })

  it('resumes an unfinished terminal resolution before showing the result', () => {
    const store = useClashStore()
    store.initCallbacks()
    signalrService.onClashState?.({
      gameId: 'terminal-reconnect',
      revision: 10,
      phase: 'Finished',
      width: 5,
      length: 5,
      latestResolution: {
        gameId: 'terminal-reconnect',
        revision: 10,
        clashNumber: 2,
        startedAtUtc: new Date(Date.now() - 100).toISOString(),
        durationMs: 4000,
        events: [event(1, 'Death', null, 'b')],
        finalUnits: [],
        winnerId: 'host',
        isDraw: false,
        terminalReason: 'Elimination',
      },
    } as never)

    expect(store.timelinePlaying).toBe(true)
    expect(store.gameState?.phase).toBe('Finished')
  })

  it('resets the create latch and reports failure when the server rejects creation', async () => {
    const store = useClashStore()
    store.initCallbacks()
    vi.spyOn(signalrService, 'createClashGame').mockImplementation(async () => {
      signalrService.onClashError?.('Лобби создать нельзя.')
    })

    await expect(store.createGame(true, 5, 5)).resolves.toBe(false)
    expect(store.isCreating).toBe(false)
    expect(store.errorMessage).toBe('Лобби создать нельзя.')
  })

  it('keeps personalized resume state independent from generic lobby broadcasts', () => {
    const store = useClashStore()
    store.initCallbacks()

    signalrService.onClashMyActiveGame?.({ gameId: 'active-1' })
    signalrService.onClashLobby?.({ games: [] })
    expect(store.myActiveGameId).toBe('active-1')

    signalrService.onClashMyActiveGame?.({ gameId: null })
    expect(store.myActiveGameId).toBeNull()
  })
})

afterEach(() => {
  signalrService.onClashState = null
  signalrService.onClashError = null
  signalrService.onClashMyActiveGame = null
  vi.restoreAllMocks()
})
