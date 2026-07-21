import { describe, expect, it } from 'vitest'
import {
  abortInventory,
  consumeInventoryFrameTime,
  createInventorySimulation,
  inventoryCommandDisabledReason,
  replayInventory,
  stepInventorySimulation,
} from './engine'
import { createInventoryPolicyCommandLog, resolveInventoryWithPolicy } from './qa'
import type { InventoryCommand, InventoryPlan } from './types'

function plan(itemCount = 4, cartHeight = 5): InventoryPlan {
  const items = Array.from({ length: itemCount }, (_, index) => ({
    id: `item-${index + 1}`,
    definitionId: 'crate',
    originCityId: 'city-south',
    content: { kind: 'resource' as const, resourceId: 'food' },
    amount: 500,
  }))
  return {
    id: 'inventory-plan',
    sessionId: 'inventory-session',
    rulesIdentity: { configSchemaVersion: 17, rulesDigest: 'inventory-rules' },
    expeditionId: 'expedition-south-fortress',
    expeditionAttempt: 1,
    originRegionId: 'south',
    provisionResourceId: 'food',
    requestedProvisionAmount: itemCount * 500,
    requiredProvisionAmount: itemCount * 500,
    eligibleProvisionAmount: itemCount * 500,
    rosterUnitInstanceIds: ['unit-1'],
    tickMs: 50,
    maxTicks: 500,
    maxCommands: 64,
    maxCatchUpTicksPerFrame: 4,
    maxItems: itemCount,
    board: { width: 8, height: 10, cartHeight },
    gravity: { intervalTicks: 4, spawnDelayTicks: 1 },
    scoring: { pointsPerWeight: 100, fullRowBonus: 500 },
    itemDefinitions: [{
      id: 'crate',
      name: 'Ящик',
      weight: 4,
      content: { kind: 'resource', resourceId: 'food' },
      cells: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }],
    }],
    itemInstances: items,
  }
}

function command(
  value: Omit<InventoryCommand, 'tick' | 'sequence' | 'sessionId' | 'planId'>,
  state = createInventorySimulation(plan(1), 'command-seed'),
): { state: ReturnType<typeof createInventorySimulation>, command: InventoryCommand } {
  return {
    state,
    command: {
      tick: state.tick,
      sequence: state.commandLog.length,
      sessionId: 'inventory-session',
      planId: 'inventory-plan',
      ...value,
    } as InventoryCommand,
  }
}

describe('inventory packing fixed-step replay', () => {
  it('replays the same plan, seed, and logical command log to the same digest and result', () => {
    const value = plan()
    const firstLog = createInventoryPolicyCommandLog(value, 'packing-seed', 'spread')
    const secondLog = createInventoryPolicyCommandLog(value, 'packing-seed', 'spread')
    const first = replayInventory(value, 'packing-seed', firstLog)
    const second = replayInventory(value, 'packing-seed', secondLog)

    expect(secondLog).toEqual(firstLog)
    expect(second).toEqual(first)
    expect(first).toMatchObject({
      outcome: 'completed',
      terminalReason: 'inventory-complete',
      efficiencyPercent: 100,
      packedProvisionAmount: 2000,
      unpackedItemInstanceIds: [],
    })
    expect(first.score).toBeGreaterThan(0)
  })

  it('supports bounded movement, rotation, hard placement, gravity, and deterministic frame catch-up', () => {
    const value = plan(1)
    const state = createInventorySimulation(value, 'controls')
    const rotate = command({ kind: 'rotate' }, state).command
    expect(inventoryCommandDisabledReason(value, state, rotate)).toBeNull()
    stepInventorySimulation(value, state, [rotate])
    const left = command({ kind: 'move', direction: 'left' }, state).command
    stepInventorySimulation(value, state, [left])
    const place = command({ kind: 'place' }, state).command
    stepInventorySimulation(value, state, [place])

    expect(state).toMatchObject({ terminalReason: 'inventory-complete', score: 400 })
    expect(state.placements[0].cells.every(cell => cell.y >= 5)).toBe(true)
    expect(consumeInventoryFrameTime(0, 1000, 50, 4)).toEqual({
      ticks: 4,
      accumulatorMs: 0,
      droppedMs: 800,
    })

    const runCadence = (cadence: readonly number[]) => {
      const cadenceState = createInventorySimulation(value, 'cadence')
      let accumulatorMs = 0
      let rotatePending = true
      for (const elapsedMs of cadence) {
        const clock = consumeInventoryFrameTime(
          accumulatorMs,
          elapsedMs,
          value.tickMs,
          value.maxCatchUpTicksPerFrame,
        )
        accumulatorMs = clock.accumulatorMs
        for (let index = 0; index < clock.ticks && !cadenceState.terminalReason; index += 1) {
          const commands: InventoryCommand[] = rotatePending
            ? [{
                tick: cadenceState.tick,
                sequence: cadenceState.commandLog.length,
                sessionId: value.sessionId,
                planId: value.id,
                kind: 'rotate',
              }]
            : []
          stepInventorySimulation(value, cadenceState, commands)
          rotatePending = false
        }
      }
      return { state: cadenceState, accumulatorMs }
    }

    const steady = runCadence([50, 50, 50, 50, 50, 50, 50, 50])
    const jittered = runCadence([20, 30, 120, 80, 25, 75, 50])
    expect(jittered).toEqual(steady)
    expect(steady.state.tick).toBe(8)
    expect(steady.state.commandLog).toEqual([
      expect.objectContaining({ kind: 'rotate', tick: 0, sequence: 0 }),
    ])
    expect(replayInventory(value, 'cadence', jittered.state.commandLog))
      .toEqual(replayInventory(value, 'cadence', steady.state.commandLog))
  })

  it('keeps placed items, leaves all other item IDs unpacked, and terminates on cart overflow', () => {
    const value = plan(4, 2)
    const result = resolveInventoryWithPolicy(value, 'overflow', 'center-stack')

    expect(result).toMatchObject({ outcome: 'failure', terminalReason: 'cart-overflow' })
    expect(result.packedItemInstanceIds.length).toBeGreaterThan(0)
    expect(result.unpackedItemInstanceIds.length).toBeGreaterThan(0)
    expect(new Set([...result.packedItemInstanceIds, ...result.unpackedItemInstanceIds]).size).toBe(4)
  })

  it('authenticates command identity, caps logs, and produces a replayable abort without packing effects', () => {
    const value = plan(2)
    value.maxCommands = 1
    const state = createInventorySimulation(value, 'abort')
    const stale = command({ kind: 'place' }, state).command
    stale.sessionId = 'stale-session'
    expect(inventoryCommandDisabledReason(value, state, stale)).toMatch(/another plan or session/i)

    const rotate = command({ kind: 'rotate' }, state).command
    stepInventorySimulation(value, state, [rotate])
    const overCap = command({ kind: 'place' }, state).command
    expect(inventoryCommandDisabledReason(value, state, overCap)).toMatch(/log limit/i)

    const result = abortInventory(value, 'abort', [], 0)
    expect(result).toMatchObject({
      outcome: 'aborted',
      terminalReason: 'aborted',
      packedProvisionAmount: 0,
      packedItemInstanceIds: [],
    })
    expect(result.unpackedItemInstanceIds).toHaveLength(2)
  })
})
