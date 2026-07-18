import {
  createInventorySimulation,
  inventoryCommandDisabledReason,
  replayInventory,
  stepInventorySimulation,
} from './engine'
import type {
  InventoryCommand,
  InventoryPlan,
  InventoryResult,
  InventorySimulationState,
} from './types'

export const INVENTORY_QA_POLICIES = ['spread', 'center-stack'] as const
export type InventoryQaPolicy = typeof INVENTORY_QA_POLICIES[number]

function command(
  plan: InventoryPlan,
  state: InventorySimulationState,
  value: Omit<InventoryCommand, 'tick' | 'sequence' | 'sessionId' | 'planId'>,
): InventoryCommand | null {
  const next = {
    tick: state.tick,
    sequence: state.commandLog.length,
    sessionId: plan.sessionId,
    planId: plan.id,
    ...value,
  } as InventoryCommand
  return inventoryCommandDisabledReason(plan, state, next) ? null : next
}

export function createInventoryPolicyCommandLog(
  plan: InventoryPlan,
  seed: string | number,
  policy: InventoryQaPolicy = 'spread',
): InventoryCommand[] {
  const state = createInventorySimulation(plan, seed)
  while (!state.terminalReason) {
    if (!state.activeItem) {
      stepInventorySimulation(plan, state)
      continue
    }
    const definition = plan.itemDefinitions.find(item => item.id === state.activeItem!.definitionId)!
    const rotated = definition.cells
    const minX = Math.min(...rotated.map(cell => cell.x))
    const maxX = Math.max(...rotated.map(cell => cell.x))
    const width = maxX - minX + 1
    const placementIndex = state.placements.length
    const lanes = Math.max(1, Math.floor(plan.board.width / Math.max(1, width)))
    const targetX = policy === 'center-stack'
      ? Math.floor((plan.board.width - width) / 2) - minX
      : Math.min(plan.board.width - width, (placementIndex % lanes) * width) - minX
    if (state.activeItem.anchor.x !== targetX) {
      const move = command(plan, state, {
        kind: 'move',
        direction: state.activeItem.anchor.x > targetX ? 'left' : 'right',
      })
      if (move) {
        stepInventorySimulation(plan, state, [move])
        continue
      }
    }
    const place = command(plan, state, { kind: 'place' })
    if (!place) {
      stepInventorySimulation(plan, state)
      continue
    }
    stepInventorySimulation(plan, state, [place])
  }
  return state.commandLog
}

export function resolveInventoryWithPolicy(
  plan: InventoryPlan,
  seed: string | number,
  policy: InventoryQaPolicy = 'spread',
): InventoryResult {
  return replayInventory(plan, seed, createInventoryPolicyCommandLog(plan, seed, policy))
}
