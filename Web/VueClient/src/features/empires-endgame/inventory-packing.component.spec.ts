import { cleanup, fireEvent, render, waitFor, within } from '@testing-library/vue'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import InventoryPacking from '../../components/empires-endgame/InventoryPacking.vue'
import type { InventoryPlan, InventoryResult } from './inventory/types'
import type { EmpiresInventoryMinigameSession } from './types'

let frameCallbacks: FrameRequestCallback[] = []

function session(): EmpiresInventoryMinigameSession {
  const sessionId = 'inventory-component-session'
  const plan: InventoryPlan = {
    id: 'inventory-component-plan',
    sessionId,
    rulesIdentity: { configSchemaVersion: 17, rulesDigest: 'inventory-component-rules' },
    expeditionId: 'expedition-south-fortress',
    expeditionAttempt: 1,
    originRegionId: 'south',
    provisionResourceId: 'food',
    requestedProvisionAmount: 500,
    requiredProvisionAmount: 500,
    eligibleProvisionAmount: 500,
    rosterUnitInstanceIds: ['unit-instance-1'],
    tickMs: 50,
    maxTicks: 2,
    maxCommands: 8,
    maxCatchUpTicksPerFrame: 2,
    maxItems: 1,
    board: { width: 8, height: 10, cartHeight: 5 },
    gravity: { intervalTicks: 10, spawnDelayTicks: 1 },
    scoring: { pointsPerWeight: 100, fullRowBonus: 500 },
    itemDefinitions: [{
      id: 'single',
      name: 'Один тюк',
      weight: 1,
      content: { kind: 'resource', resourceId: 'food' },
      cells: [{ x: 0, y: 0 }],
    }],
    itemInstances: [{
      id: 'item-1',
      definitionId: 'single',
      originCityId: 'city-south',
      content: { kind: 'resource', resourceId: 'food' },
      amount: 500,
    }],
  }
  return {
    id: sessionId,
    sequence: 1,
    kind: 'inventory',
    plan,
    rulesIdentity: structuredClone(plan.rulesIdentity),
    seed: 'inventory-component-seed',
    attempt: 0,
    origin: {
      returnPhase: 'empire',
      context: { kind: 'expedition-packing', expeditionId: plan.expeditionId, attempt: 1 },
    },
  }
}

async function advanceOneTick() {
  const firstFrame = frameCallbacks.shift()
  if (!firstFrame) throw new Error('Inventory component did not schedule its first frame')
  firstFrame(1)
  const secondFrame = frameCallbacks.shift()
  if (!secondFrame) throw new Error('Inventory component did not schedule its second frame')
  secondFrame(51)
  await Promise.resolve()
}

async function runNextFrame(timestamp: number) {
  const frame = frameCallbacks.shift()
  if (!frame) throw new Error('Inventory component did not schedule the expected frame')
  frame(timestamp)
  await Promise.resolve()
}

function setDocumentHidden(hidden: boolean) {
  Object.defineProperty(document, 'hidden', { configurable: true, value: hidden })
  document.dispatchEvent(new Event('visibilitychange'))
}

beforeEach(() => {
  frameCallbacks = []
  Object.defineProperty(document, 'hidden', { configurable: true, value: false })
  vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback) => {
    frameCallbacks.push(callback)
    return frameCallbacks.length
  })
  vi.spyOn(window, 'cancelAnimationFrame').mockImplementation(() => {})
})

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe('Empire\'s Endgame InventoryPacking production input path', () => {
  it.each(['pointer', 'keyboard'] as const)(
    'records a rotate command from %s input and advances it with the fixed fake clock',
    async (input) => {
      const activeSession = session()
      const onResolved = vi.fn<(result: InventoryResult) => void>()
      const view = render(InventoryPacking, { props: { session: activeSession, onResolved } })
      await fireEvent.click(view.getByTestId('inventory-pause'))

      if (input === 'pointer') await fireEvent.click(view.getByTestId('inventory-rotate'))
      else await userEvent.keyboard(' ')

      await advanceOneTick()
      await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1))
      expect(onResolved.mock.calls[0][0]).toMatchObject({
        sessionId: activeSession.id,
        terminalReason: 'tick-cap',
        commandLog: [{
          tick: 0,
          sequence: 0,
          sessionId: activeSession.id,
          planId: activeSession.plan.id,
          kind: 'rotate',
        }],
      })
    },
  )

  it('requires confirmation before aborting and returns the logical command prefix', async () => {
    const onAbort = vi.fn()
    const view = render(InventoryPacking, { props: { session: session(), onAbort } })
    await fireEvent.click(view.getByTestId('inventory-abort'))
    expect(onAbort).not.toHaveBeenCalled()
    await fireEvent.click(view.getByTestId('inventory-confirm-abort'))
    expect(onAbort).toHaveBeenCalledWith([], 0)
  })

  it('lets focused native buttons activate once without leaking Enter or Space into game shortcuts', async () => {
    const onAbort = vi.fn()
    const view = render(InventoryPacking, { props: { session: session(), onAbort } })
    await fireEvent.click(view.getByTestId('inventory-pause'))

    const rotate = view.getByTestId('inventory-rotate')
    rotate.focus()
    await userEvent.keyboard(' ')
    const abortTrigger = view.getByTestId('inventory-abort')
    abortTrigger.focus()
    await userEvent.keyboard('{Enter}')
    await fireEvent.click(view.getByTestId('inventory-confirm-abort'))

    expect(onAbort).toHaveBeenCalledWith([
      expect.objectContaining({ kind: 'rotate', tick: 0, sequence: 0 }),
    ], 1)
  })

  it('does not treat Enter on the focused abort button as an inventory placement', async () => {
    const onAbort = vi.fn()
    const view = render(InventoryPacking, { props: { session: session(), onAbort } })
    await fireEvent.click(view.getByTestId('inventory-pause'))
    const abortTrigger = view.getByTestId('inventory-abort')
    abortTrigger.focus()
    await userEvent.keyboard('{Enter}')
    await fireEvent.click(view.getByTestId('inventory-confirm-abort'))
    expect(onAbort).toHaveBeenCalledWith([], 0)
  })

  it('keeps Escape available from focused controls and ignores browser chords and repeated pause toggles', async () => {
    const view = render(InventoryPacking, { props: { session: session() } })
    const pause = view.getByTestId('inventory-pause')
    expect(pause.getAttribute('aria-pressed')).toBeNull()

    await fireEvent.keyDown(window, { key: 'p', code: 'KeyP', metaKey: true })
    await fireEvent.keyDown(window, { key: 'ArrowRight', code: 'ArrowRight', altKey: true })
    await fireEvent.keyDown(window, { key: 'p', code: 'KeyP', repeat: true })
    expect(pause.textContent).toContain('Начать')

    pause.focus()
    await fireEvent.keyDown(pause, { key: 'Escape', code: 'Escape' })
    expect(view.getByRole('alertdialog', { name: 'Прервать упаковку и экспедицию?' })).toBeTruthy()
  })

  it('pauses and locks input behind a safe-focus trapped abort dialog, then restores play on Escape', async () => {
    vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([
      {} as DOMRect,
    ] as unknown as DOMRectList)
    const onAbort = vi.fn()
    const view = render(InventoryPacking, { props: { session: session(), onAbort } })
    await fireEvent.click(view.getByTestId('inventory-pause'))
    const abortTrigger = view.getByTestId('inventory-abort')
    abortTrigger.focus()
    await fireEvent.click(abortTrigger)

    const dialog = view.getByRole('alertdialog', { name: 'Прервать упаковку и экспедицию?' })
    const continueButton = view.getByTestId('inventory-continue')
    const confirmButton = view.getByTestId('inventory-confirm-abort')
    await waitFor(() => expect(document.activeElement).toBe(continueButton))
    expect(within(dialog).getAllByRole('button')).toEqual([continueButton, confirmButton])
    expect(document.getElementById(dialog.getAttribute('aria-describedby')!)?.textContent)
      .toContain('Подготовительные дни уже потрачены')
    expect(view.getByTestId('inventory-pause').textContent).toContain('Начать')

    await fireEvent.keyDown(window, { key: 'Enter', code: 'Enter' })
    await runNextFrame(1)
    await runNextFrame(50_001)
    expect(view.getByTestId('inventory-text-state').textContent).toContain('Тик 0;')

    continueButton.focus()
    await userEvent.tab({ shift: true })
    expect(document.activeElement).toBe(confirmButton)
    await userEvent.tab()
    expect(document.activeElement).toBe(continueButton)
    await fireEvent.keyDown(dialog, { key: 'Escape' })
    await waitFor(() => expect(view.queryByRole('alertdialog')).toBeNull())
    expect(document.activeElement).toBe(abortTrigger)
    expect(view.getByTestId('inventory-pause').textContent).toContain('Пауза')

    await fireEvent.click(abortTrigger)
    await fireEvent.click(view.getByTestId('inventory-confirm-abort'))
    expect(onAbort).toHaveBeenCalledWith([], 0)
  })

  it('drops hidden-tab elapsed time and resumes from a fresh frame origin', async () => {
    const onResolved = vi.fn<(result: InventoryResult) => void>()
    const view = render(InventoryPacking, { props: { session: session(), onResolved } })
    await fireEvent.click(view.getByTestId('inventory-pause'))
    await runNextFrame(1)

    setDocumentHidden(true)
    await runNextFrame(50_001)
    expect(view.getByTestId('inventory-text-state').textContent).toContain('Тик 0;')
    expect(onResolved).not.toHaveBeenCalled()

    setDocumentHidden(false)
    await fireEvent.click(view.getByTestId('inventory-pause'))
    await runNextFrame(100_001)
    expect(view.getByTestId('inventory-text-state').textContent).toContain('Тик 0;')
    await runNextFrame(100_051)
    expect(view.getByTestId('inventory-text-state').textContent).toContain('Тик 1;')
    expect(onResolved).not.toHaveBeenCalled()
  })

  it('exposes a labelled keyboard surface and a textual cart alternative', () => {
    const view = render(InventoryPacking, { props: { session: session() } })
    const surface = view.getByTestId('inventory-keyboard-surface')
    expect(surface.getAttribute('role')).toBe('img')
    expect(surface.tabIndex).toBe(0)
    expect(surface.getAttribute('aria-label')).toContain('Поле 8 на 10')
    expect(document.getElementById(surface.getAttribute('aria-describedby')!)?.textContent)
      .toContain('Enter или ↓')
    expect(view.getByTestId('inventory-text-state').textContent).toContain('Текстовое состояние тележки')
  })
})
