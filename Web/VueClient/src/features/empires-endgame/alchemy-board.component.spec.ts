import { cleanup, fireEvent, render, waitFor } from '@testing-library/vue'
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
    explosion: { epidemicDefinitionId: 'epidemic-plague', severityMultiplier: 1.5, lockBuildingForCon: true },
    colors: ['red'],
    pieces: [{ id: 'single', name: 'Один блок', cells: [{ x: 0, y: 0 }] }],
  }
  return {
    id: sessionId,
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
})
