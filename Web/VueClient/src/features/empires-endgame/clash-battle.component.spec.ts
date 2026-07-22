import { cleanup, fireEvent, render } from '@testing-library/vue'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import ClashBattle from '../../components/empires-endgame/ClashBattle.vue'
import { CLASH_SCAFFOLD } from './clash/catalog'
import { replayClashState } from './clash/engine'
import { createClashQaPlan } from './clash/qa'
import type {
  ClashCommand,
  ClashResult,
  EmpiresClashMinigameSession,
} from './types'

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function session(seed = 'clash-component-seed'): EmpiresClashMinigameSession {
  const config = clone(CLASH_SCAFFOLD)
  const sessionId = 'clash-component-session'
  const plan = createClashQaPlan(config, seed, sessionId)
  return {
    id: sessionId,
    sequence: 1,
    kind: 'clash',
    plan,
    rulesIdentity: clone(plan.rulesIdentity),
    seed,
    turnLog: [],
    attempt: 0,
    origin: {
      returnPhase: 'cards',
      context: { kind: 'manual', sourceId: 'qa-clash-component' },
    },
  }
}

afterEach(() => {
  cleanup()
  vi.useRealTimers()
})

describe('Empire\'s Endgame Clash production input path', () => {
  it('routes pointer, Enter, and Space placement through the immutable command transition', async () => {
    const frozenNow = new Date('2026-06-04T12:00:00.000Z').valueOf()
    vi.useFakeTimers({ now: frozenNow })
    const user = userEvent.setup({
      advanceTimers: delay => vi.advanceTimersByTime(delay),
    })
    const activeSession = session()
    const onProgress = vi.fn<(turnLog: ClashCommand[]) => void>()
    const view = render(ClashBattle, { props: { session: activeSession, onProgress } })

    expect(view.getByRole('grid', { name: 'Поле боя Клэша' })).toBeTruthy()
    expect(view.getByTestId('clash-text-state').textContent).toContain('Ход 0')

    const attackerFirst = view.getByTestId('clash-cell-attacker-0-0') as HTMLButtonElement
    expect(attackerFirst.disabled).toBe(false)
    await user.pointer({ keys: '[MouseLeft]', target: attackerFirst })
    expect(view.getByTestId('clash-text-state').textContent).toContain('Ход 1')
    expect(view.getByTestId('clash-turn-log').textContent).toContain('выставлен')

    const defenderFirst = view.getByTestId('clash-cell-defender-0-0') as HTMLButtonElement
    defenderFirst.focus()
    await user.keyboard('{Enter}')
    expect(view.getByTestId('clash-text-state').textContent).toContain('Ход 2')

    const attackerSecond = view.getByTestId('clash-cell-attacker-0-1') as HTMLButtonElement
    attackerSecond.focus()
    await user.keyboard('[Space]')
    expect(view.getByTestId('clash-text-state').textContent).toContain('Ход 3')

    expect(view.getAllByRole('gridcell').filter(cell => !cell.getAttribute('aria-label')?.includes('пусто')))
      .toHaveLength(3)
    expect(onProgress).toHaveBeenCalledTimes(3)
    expect(onProgress.mock.calls[2][0]).toHaveLength(3)
    expect(view.queryByRole('alert')).toBeNull()
    expect(Date.now()).toBe(frozenNow)
  })

  it('does not advertise or accept placement on a corpse-blocked cell after replay', async () => {
    const activeSession = session('clash-component-corpse-blocking')
    activeSession.plan.field = {
      ...activeSession.plan.field,
      columns: 1,
      rowsPerSide: 2,
      reinforcementRows: 1,
      terrainCellIds: [],
    }
    activeSession.plan.corpseBlocksAdvance = true
    activeSession.plan.betweenClashesFirstSide = 'defender'

    const attacker = activeSession.plan.units.find(unit => unit.id === 'legionary')!
    Object.assign(attacker, { attack: 9, maxHp: 9, speed: 9, passives: [], abilities: [] })
    const defender = activeSession.plan.units.find(unit => unit.id === 'archer')!
    Object.assign(defender, { attack: 1, maxHp: 1, speed: 1, passives: [], abilities: [] })
    activeSession.plan.units = [attacker, defender]
    activeSession.plan.roster = [
      { instanceId: 'attacker-front', definitionId: attacker.id, side: 'attacker' },
      { instanceId: 'attacker-reserve', definitionId: attacker.id, side: 'attacker' },
      { instanceId: 'defender-front', definitionId: defender.id, side: 'defender' },
      { instanceId: 'defender-reserve', definitionId: defender.id, side: 'defender' },
    ]
    activeSession.turnLog = [
      {
        turn: 1,
        kind: 'place',
        side: 'attacker',
        unitInstanceId: 'attacker-front',
        row: 0,
        column: 0,
      },
      {
        turn: 2,
        kind: 'place',
        side: 'defender',
        unitInstanceId: 'defender-front',
        row: 0,
        column: 0,
      },
      { turn: 3, kind: 'resolve-clash' },
    ]

    const replayed = replayClashState(activeSession.plan, activeSession.seed, activeSession.turnLog)
    expect(replayed).toMatchObject({ phase: 'between-clashes', expectedSide: 'defender', turn: 3 })
    expect(replayed.cells.find(cell => (
      cell.side === 'defender' && cell.row === 0 && cell.column === 0
    ))?.corpseIds).toHaveLength(1)
    expect(replayed.units['defender-reserve']).toMatchObject({ alive: true, deployed: false })

    const onProgress = vi.fn<(turnLog: ClashCommand[]) => void>()
    const view = render(ClashBattle, { props: { session: activeSession, onProgress } })
    const reserve = view.getByTestId('clash-reserve-defender-reserve') as HTMLButtonElement
    const blocked = view.getByTestId('clash-cell-defender-0-0') as HTMLButtonElement
    const open = view.getByTestId('clash-cell-defender-1-0') as HTMLButtonElement

    expect(reserve.disabled).toBe(false)
    expect(blocked.disabled).toBe(true)
    expect(blocked.getAttribute('aria-label')).toContain('останков: 1')
    expect(blocked.getAttribute('aria-label')).not.toContain('Выставить')
    expect(blocked.classList.contains('clash__cell--placeable')).toBe(false)
    expect(open.disabled).toBe(false)
    expect(open.getAttribute('aria-label')).toContain('Выставить')

    await fireEvent.click(blocked)
    expect(onProgress).not.toHaveBeenCalled()
    expect(view.getByTestId('clash-text-state').textContent).toContain('Ход 3')
  })

  it('emits the exact command journal and turn when abort is requested', async () => {
    const onAbort = vi.fn<(turnLog: ClashCommand[], turn: number) => void>()
    const view = render(ClashBattle, { props: { session: session(), onAbort } })

    await fireEvent.click(view.getByTestId('clash-cell-attacker-0-0'))
    await fireEvent.click(view.getByTestId('clash-abort'))

    expect(onAbort).toHaveBeenCalledTimes(1)
    expect(onAbort).toHaveBeenCalledWith([
      expect.objectContaining({ kind: 'place', turn: 1, side: 'attacker', row: 0, column: 0 }),
    ], 1)
  })

  it('uses the headless QA policy runner only behind the QA control', async () => {
    const activeSession = session('clash-component-fast-resolve')
    const onResolve = vi.fn<(result: ClashResult) => void>()
    const view = render(ClashBattle, {
      props: { session: activeSession, qaMode: true, qaPolicy: 'aggressive', onResolve },
    })

    await fireEvent.click(view.getByTestId('clash-cell-attacker-0-0'))
    await fireEvent.click(view.getByTestId('clash-qa-resolve'))

    expect(onResolve).toHaveBeenCalledTimes(1)
    const result = onResolve.mock.calls[0][0]
    expect(result.kind).toBe('clash')
    expect(result.sessionId).toBe(activeSession.id)
    expect(result.rulesIdentity).toEqual(activeSession.rulesIdentity)
    expect(result.turnLog[0]).toMatchObject({
      turn: 1,
      kind: 'place',
      side: 'attacker',
      row: 0,
      column: 0,
    })
    expect(['elimination', 'turn-cap']).toContain(result.terminalReason)
    expect(result.turnLog.length).toBeGreaterThan(0)
    expect(result.commandDigest).toMatch(/^[0-9a-f]{16}$/)
  })
})
