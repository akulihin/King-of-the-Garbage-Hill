import { cleanup, fireEvent, render, waitFor } from '@testing-library/vue'
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
})
