import { cleanup, fireEvent, render, waitFor, within } from '@testing-library/vue'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AlchemyBoard from '../../components/empires-endgame/AlchemyBoard.vue'
import type { AlchemyPlan, AlchemyResult } from './alchemy/types'
import type { EmpiresAlchemyMinigameSession } from './types'

let frameCallbacks: FrameRequestCallback[] = []

function session(): EmpiresAlchemyMinigameSession {
  const sessionId = 'alchemy-component-session'
  const plan: AlchemyPlan = {
    id: 'alchemy-component-plan',
    sessionId,
    rulesIdentity: { configSchemaVersion: 15, rulesDigest: 'alchemy-component-rules' },
    originCityId: 'city-center-1',
    buildingId: 'building-alchemy',
    recipe: {
      id: 'alchemy-component-recipe',
      name: 'Компонентный Сбор',
      description: 'Проверка общего пути ввода.',
      mode: 'assembly',
      family: 'experiment',
      initialCells: [{ x: 5, y: 5, color: 'gray' }],
      targetCells: [{ x: 5, y: 5 }, { x: 0, y: 0 }],
      pieceDefinitionIds: ['single'],
      prerequisites: [],
      rewards: [],
    },
    tickMs: 50,
    maxTicks: 2,
    maxCommands: 8,
    maxCatchUpTicksPerFrame: 2,
    board: { width: 11, height: 11, centerX: 5, centerY: 5 },
    spawn: { minDelayTicks: 10, maxDelayTicks: 10, baseMoveIntervalTicks: 10, inwardSpeedMultiplier: 3 },
    acceleration: {
      baseSpeedPercent: 100,
      stepPercent: 1,
      piecesPerStep: 1,
      explosionThresholdPercent: 400,
      explosionBoundary: 'above',
    },
    reagents: { removeColorCharges: 1, addGrayCharges: 1, resetAccelerationCharges: 1 },
    explosion: {
      epidemicDefinitionId: 'epidemic-plague',
      severityMultiplier: 1.5,
      lockBuildingForCon: true,
      mutantAftermath: {
        kind: 'mutant-outbreak',
        delayCons: 2,
        populationLoss: 10_000,
        loyaltyDelta: -1,
      },
    },
    colors: ['red'],
    pieces: [{ id: 'single', name: 'Один блок', cells: [{ x: 0, y: 0 }] }],
  }
  return {
    id: sessionId,
    sequence: 1,
    kind: 'alchemy',
    plan,
    rulesIdentity: structuredClone(plan.rulesIdentity),
    seed: 'alchemy-component-seed',
    attempt: 0,
    origin: {
      returnPhase: 'empire',
      context: { kind: 'alchemy-experiment', cityId: 'city-center-1', recipeId: plan.recipe.id, con: 1 },
    },
  }
}

async function advanceOneTick() {
  const firstFrame = frameCallbacks.shift()
  if (!firstFrame) throw new Error('Alchemy component did not schedule its first frame')
  firstFrame(1)
  const secondFrame = frameCallbacks.shift()
  if (!secondFrame) throw new Error('Alchemy component did not schedule its second frame')
  secondFrame(51)
  await Promise.resolve()
}

async function runNextFrame(timestamp: number) {
  const frame = frameCallbacks.shift()
  if (!frame) throw new Error('Alchemy component did not schedule the expected frame')
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

describe('Empire\'s Endgame Alchemy production input path', () => {
  it.each(['pointer', 'keyboard'] as const)(
    'records a rotate command from %s input and advances it with the fixed fake clock',
    async (input) => {
      const activeSession = session()
      const onResolved = vi.fn<(result: AlchemyResult) => void>()
      const view = render(AlchemyBoard, { props: { session: activeSession, onResolved } })
      await fireEvent.click(view.getByTestId('alchemy-pause'))

      if (input === 'pointer') await fireEvent.click(view.getByTestId('alchemy-rotate'))
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

  it.each(['pointer', 'keyboard'] as const)(
    'opens the reagent menu from %s input and pauses the logical clock',
    async (input) => {
      const onResolved = vi.fn<(result: AlchemyResult) => void>()
      const view = render(AlchemyBoard, { props: { session: session(), onResolved } })
      await fireEvent.click(view.getByTestId('alchemy-pause'))

      if (input === 'pointer') await fireEvent.click(view.getByTestId('alchemy-reagents'))
      else await userEvent.keyboard('{Enter}')

      expect(view.getByTestId('alchemy-reagent-panel')).toBeTruthy()
      expect(view.getByTestId('alchemy-pause').textContent).toContain('Продолжить')
      await advanceOneTick()
      expect(onResolved).not.toHaveBeenCalled()
    },
  )

  it('keeps native Enter and Space activation single-shot on focused controls', async () => {
    const onAbort = vi.fn()
    const view = render(AlchemyBoard, { props: { session: session(), onAbort } })
    await fireEvent.click(view.getByTestId('alchemy-pause'))

    const reagents = view.getByTestId('alchemy-reagents')
    reagents.focus()
    await userEvent.keyboard('{Enter}')
    expect(view.getByTestId('alchemy-reagent-panel')).toBeTruthy()

    await fireEvent.click(reagents)
    await fireEvent.click(view.getByTestId('alchemy-pause'))
    const rotate = view.getByTestId('alchemy-rotate')
    rotate.focus()
    await userEvent.keyboard(' ')
    await fireEvent.click(view.getByTestId('alchemy-abort'))
    await fireEvent.click(view.getByTestId('alchemy-confirm-abort'))

    expect(onAbort).toHaveBeenCalledWith([
      expect.objectContaining({ kind: 'rotate', tick: 0, sequence: 0 }),
    ], 1)
  })

  it('keeps Escape available from focused controls and ignores browser chords and repeated toggles', async () => {
    const view = render(AlchemyBoard, { props: { session: session() } })
    const pause = view.getByTestId('alchemy-pause')
    expect(pause.getAttribute('aria-pressed')).toBeNull()

    await fireEvent.keyDown(window, { key: 'p', code: 'KeyP', ctrlKey: true })
    await fireEvent.keyDown(window, { key: 'ArrowLeft', code: 'ArrowLeft', altKey: true })
    await fireEvent.keyDown(window, { key: 'p', code: 'KeyP', repeat: true })
    await fireEvent.keyDown(window, { key: 'Enter', code: 'Enter', repeat: true })
    expect(pause.textContent).toContain('Продолжить')
    expect(view.queryByTestId('alchemy-reagent-panel')).toBeNull()

    pause.focus()
    await fireEvent.keyDown(pause, { key: 'Escape', code: 'Escape' })
    expect(view.getByRole('alertdialog', { name: 'Прервать лабораторную сессию?' })).toBeTruthy()
  })

  it('pauses and locks input behind a safe-focus trapped abort dialog, then restores play on Escape', async () => {
    vi.spyOn(HTMLElement.prototype, 'getClientRects').mockReturnValue([
      {} as DOMRect,
    ] as unknown as DOMRectList)
    const onAbort = vi.fn()
    const view = render(AlchemyBoard, { props: { session: session(), onAbort } })
    await fireEvent.click(view.getByTestId('alchemy-pause'))
    const abortTrigger = view.getByTestId('alchemy-abort')
    abortTrigger.focus()
    await fireEvent.click(abortTrigger)

    const dialog = view.getByRole('alertdialog', { name: 'Прервать лабораторную сессию?' })
    const continueButton = view.getByTestId('alchemy-continue')
    const confirmButton = view.getByTestId('alchemy-confirm-abort')
    await waitFor(() => expect(document.activeElement).toBe(continueButton))
    expect(within(dialog).getAllByRole('button')).toEqual([continueButton, confirmButton])
    expect(document.getElementById(dialog.getAttribute('aria-describedby')!)?.textContent)
      .toContain('Потраченные на опыт дни не возвращаются')
    expect(view.getByTestId('alchemy-pause').textContent).toContain('Продолжить')

    await fireEvent.keyDown(window, { key: ' ', code: 'Space' })
    await runNextFrame(1)
    await runNextFrame(50_001)
    expect(view.getByTestId('alchemy-text-state').textContent).toContain('Тик 0;')

    continueButton.focus()
    await userEvent.tab({ shift: true })
    expect(document.activeElement).toBe(confirmButton)
    await userEvent.tab()
    expect(document.activeElement).toBe(continueButton)
    await fireEvent.keyDown(dialog, { key: 'Escape' })
    await waitFor(() => expect(view.queryByRole('alertdialog')).toBeNull())
    expect(document.activeElement).toBe(abortTrigger)
    expect(view.getByTestId('alchemy-pause').textContent).toContain('Пауза')

    await fireEvent.click(abortTrigger)
    await fireEvent.click(view.getByTestId('alchemy-confirm-abort'))
    expect(onAbort).toHaveBeenCalledWith([], 0)
  })

  it('drops hidden-tab elapsed time and resumes from a fresh frame origin', async () => {
    const onResolved = vi.fn<(result: AlchemyResult) => void>()
    const view = render(AlchemyBoard, { props: { session: session(), onResolved } })
    await fireEvent.click(view.getByTestId('alchemy-pause'))
    await runNextFrame(1)

    setDocumentHidden(true)
    await runNextFrame(50_001)
    expect(view.getByTestId('alchemy-text-state').textContent).toContain('Тик 0;')
    expect(onResolved).not.toHaveBeenCalled()

    setDocumentHidden(false)
    await fireEvent.click(view.getByTestId('alchemy-pause'))
    await runNextFrame(100_001)
    expect(view.getByTestId('alchemy-text-state').textContent).toContain('Тик 0;')
    await runNextFrame(100_051)
    expect(view.getByTestId('alchemy-text-state').textContent).toContain('Тик 1;')
    expect(onResolved).not.toHaveBeenCalled()
  })

  it('exposes a labelled keyboard surface and a textual board alternative', () => {
    const view = render(AlchemyBoard, { props: { session: session() } })
    const surface = view.getByTestId('alchemy-keyboard-surface')
    expect(surface.getAttribute('role')).toBe('img')
    expect(surface.tabIndex).toBe(0)
    expect(surface.getAttribute('aria-label')).toContain('Поле 11 на 11')
    expect(document.getElementById(surface.getAttribute('aria-describedby')!)?.textContent)
      .toContain('Space — поворот')
    expect(view.getByTestId('alchemy-text-state').textContent).toContain('Текстовое состояние поля')
  })
})
