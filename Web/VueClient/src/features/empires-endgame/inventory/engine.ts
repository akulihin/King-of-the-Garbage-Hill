import { createEmpiresRngState, nextEmpiresRandomInt } from '../rng'
import { digestTdValue } from '../td/engine'
import { EMPIRES_STABILIZATION_BUDGETS } from '../stabilization'
import type {
  CombatEquipmentDefinition,
  EmpiresExpeditionDefinition,
  EmpiresExpeditionsConfig,
  EmpiresEndgameConfig,
  EmpiresInitialCity,
  EmpiresLoyaltyConfig,
  EmpiresQuestsConfig,
  EmpiresResourceDefinition,
} from '../types'
import { INVENTORY_MOVES } from './types'
import type {
  EmpiresInventoryConfig,
  InventoryActiveItem,
  InventoryCommand,
  InventoryFrameClock,
  InventoryItemDefinition,
  InventoryMove,
  InventoryPlacement,
  InventoryPlan,
  InventoryPoint,
  InventoryResult,
  InventoryRulesIdentity,
  InventorySimulationState,
  InventoryTerminalReason,
} from './types'

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function finiteInteger(value: number, minimum = 0): boolean {
  return Number.isInteger(value) && value >= minimum
}

function pointKey(point: InventoryPoint): string {
  return `${point.x}:${point.y}`
}

function normalizedRotation(value: number): 0 | 1 | 2 | 3 {
  return ((value % 4 + 4) % 4) as 0 | 1 | 2 | 3
}

function rotatePoint(point: InventoryPoint, rotation: number): InventoryPoint {
  let current = { ...point }
  for (let index = 0; index < normalizedRotation(rotation); index += 1) {
    current = { x: -current.y, y: current.x }
  }
  return current
}

function definitionFor(plan: InventoryPlan, definitionId: string): InventoryItemDefinition | null {
  return plan.itemDefinitions.find(definition => definition.id === definitionId) ?? null
}

function instanceFor(plan: InventoryPlan, instanceId: string) {
  return plan.itemInstances.find(instance => instance.id === instanceId) ?? null
}

export function inventoryItemCells(
  plan: InventoryPlan,
  item: InventoryActiveItem,
  anchor = item.anchor,
  rotation = item.rotation,
): InventoryPoint[] {
  const definition = definitionFor(plan, item.definitionId)
  if (!definition) return []
  return definition.cells.map((cell) => {
    const rotated = rotatePoint(cell, rotation)
    return { x: anchor.x + rotated.x, y: anchor.y + rotated.y }
  })
}

function occupiedKeys(state: InventorySimulationState): Set<string> {
  return new Set(state.placements.flatMap(placement => placement.cells.map(pointKey)))
}

function candidateBlocked(
  plan: InventoryPlan,
  state: InventorySimulationState,
  item: InventoryActiveItem,
  anchor: InventoryPoint,
  rotation = item.rotation,
): boolean {
  const occupied = occupiedKeys(state)
  return inventoryItemCells(plan, item, anchor, rotation).some(cell => (
    cell.x < 0 || cell.x >= plan.board.width || cell.y < 0 || cell.y >= plan.board.height
      || occupied.has(pointKey(cell))
  ))
}

function spawnAnchor(plan: InventoryPlan, definition: InventoryItemDefinition): InventoryPoint {
  const minX = Math.min(...definition.cells.map(cell => cell.x))
  const maxX = Math.max(...definition.cells.map(cell => cell.x))
  const minY = Math.min(...definition.cells.map(cell => cell.y))
  return {
    x: Math.floor((plan.board.width - (maxX - minX + 1)) / 2) - minX,
    y: -minY,
  }
}

function shuffleIds(ids: readonly string[], state: InventorySimulationState): string[] {
  const shuffled = [...ids]
  for (let index = shuffled.length - 1; index > 0; index -= 1) {
    const swap = nextEmpiresRandomInt(state.rng, 0, index)
    ;[shuffled[index], shuffled[swap]] = [shuffled[swap], shuffled[index]]
  }
  return shuffled
}

function spawnItem(plan: InventoryPlan, state: InventorySimulationState): void {
  if (state.activeItem || state.queueItemInstanceIds.length === 0) return
  const instanceId = state.queueItemInstanceIds.shift()!
  const instance = instanceFor(plan, instanceId)
  const definition = instance ? definitionFor(plan, instance.definitionId) : null
  if (!instance || !definition) {
    state.terminalReason = 'invalid-command'
    state.error = `Inventory item ${instanceId} is missing from its immutable plan.`
    return
  }
  const active: InventoryActiveItem = {
    instanceId,
    definitionId: definition.id,
    anchor: spawnAnchor(plan, definition),
    rotation: 0,
    nextGravityTick: state.tick + plan.gravity.intervalTicks,
  }
  if (candidateBlocked(plan, state, active, active.anchor)) {
    state.terminalReason = 'cart-overflow'
    return
  }
  state.activeItem = active
}

function completedCartRows(plan: InventoryPlan, state: InventorySimulationState): number[] {
  const occupied = occupiedKeys(state)
  const cartTop = plan.board.height - plan.board.cartHeight
  const rows: number[] = []
  for (let y = cartTop; y < plan.board.height; y += 1) {
    if (Array.from({ length: plan.board.width }, (_, x) => occupied.has(`${x}:${y}`)).every(Boolean)) {
      rows.push(y)
    }
  }
  return rows
}

function lockActiveItem(plan: InventoryPlan, state: InventorySimulationState): void {
  const active = state.activeItem
  if (!active) return
  const instance = instanceFor(plan, active.instanceId)
  const definition = definitionFor(plan, active.definitionId)
  if (!instance || !definition) {
    state.terminalReason = 'invalid-command'
    state.error = 'The active inventory item is stale.'
    return
  }
  const cells = inventoryItemCells(plan, active)
  const cartTop = plan.board.height - plan.board.cartHeight
  if (cells.some(cell => cell.y < cartTop)) {
    state.activeItem = null
    state.terminalReason = 'cart-overflow'
    return
  }
  const priorRows = new Set(state.completedRows)
  const placement: InventoryPlacement = {
    instanceId: instance.id,
    definitionId: definition.id,
    anchor: clone(active.anchor),
    rotation: active.rotation,
    cells: clone(cells),
    amount: instance.amount,
    weight: definition.weight,
    score: definition.weight * plan.scoring.pointsPerWeight,
  }
  state.placements.push(placement)
  const rows = completedCartRows(plan, state)
  const newRows = rows.filter(row => !priorRows.has(row))
  placement.score += newRows.length * plan.scoring.fullRowBonus
  state.completedRows = rows
  state.score += placement.score
  state.activeItem = null
  if (state.queueItemInstanceIds.length === 0) state.terminalReason = 'inventory-complete'
  else state.nextSpawnTick = state.tick + plan.gravity.spawnDelayTicks
}

function tryMove(plan: InventoryPlan, state: InventorySimulationState, direction: InventoryMove): boolean {
  const active = state.activeItem
  if (!active) return false
  const delta = direction === 'left' ? -1 : 1
  const anchor = { x: active.anchor.x + delta, y: active.anchor.y }
  if (candidateBlocked(plan, state, active, anchor)) return false
  active.anchor = anchor
  return true
}

function hardDrop(plan: InventoryPlan, state: InventorySimulationState): void {
  const active = state.activeItem
  if (!active) return
  while (!candidateBlocked(plan, state, active, { x: active.anchor.x, y: active.anchor.y + 1 })) {
    active.anchor.y += 1
  }
  lockActiveItem(plan, state)
}

export function createInventoryRulesIdentity(
  configSchemaVersion: number,
  inventory: EmpiresInventoryConfig,
  references: {
    resources: readonly EmpiresResourceDefinition[]
    equipment: readonly CombatEquipmentDefinition[]
    expeditions: readonly EmpiresExpeditionDefinition[] | EmpiresExpeditionsConfig
    cities?: readonly EmpiresInitialCity[]
    loyalty?: EmpiresLoyaltyConfig
    quests?: EmpiresQuestsConfig
    settlementConfig?: Omit<EmpiresEndgameConfig, 'seed'>
    sharedResultRetention?: unknown
  },
): InventoryRulesIdentity {
  return {
    configSchemaVersion,
    rulesDigest: digestTdValue({ inventory, ...references }),
  }
}

export function validateInventoryPlan(plan: InventoryPlan): string[] {
  const errors: string[] = []
  if (!plan.id?.trim() || !plan.sessionId?.trim() || !plan.expeditionId?.trim()) {
    errors.push('Inventory plan, session, and expedition IDs are required.')
  }
  if (!plan.rulesIdentity?.rulesDigest?.trim()
    || !finiteInteger(plan.rulesIdentity.configSchemaVersion, 1)) errors.push('Inventory rules identity is invalid.')
  if (!finiteInteger(plan.expeditionAttempt, 1)
    || !finiteInteger(plan.tickMs, 1) || !finiteInteger(plan.maxTicks, 1)
    || !finiteInteger(plan.maxCommands, 1) || !finiteInteger(plan.maxCatchUpTicksPerFrame, 1)
    || !finiteInteger(plan.maxItems, 1)) {
    errors.push('Inventory attempt, timing, command, item, and catch-up limits must be positive integers.')
  }
  if (plan.maxTicks > EMPIRES_STABILIZATION_BUDGETS.maxTicks
    || plan.maxCommands > EMPIRES_STABILIZATION_BUDGETS.maxCommands
    || plan.maxCatchUpTicksPerFrame > EMPIRES_STABILIZATION_BUDGETS.maxCatchUpTicksPerFrame
    || plan.maxItems > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
    || plan.tickMs * plan.maxTicks > EMPIRES_STABILIZATION_BUDGETS.maxLogicalReplayDurationMs) {
    errors.push('Inventory runtime limits exceed the shipped safety ceiling.')
  }
  if (!finiteInteger(plan.board.width, 4) || !finiteInteger(plan.board.height, 6)
    || !finiteInteger(plan.board.cartHeight, 2) || plan.board.cartHeight >= plan.board.height) {
    errors.push('Inventory board and cart dimensions are invalid.')
  }
  if (plan.board.width * plan.board.height > EMPIRES_STABILIZATION_BUDGETS.maxBoardCells
    || plan.rosterUnitInstanceIds.length > EMPIRES_STABILIZATION_BUDGETS.maxRosterUnitInstances
    || plan.itemDefinitions.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
    || plan.itemInstances.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems) {
    errors.push('Inventory board or component count exceeds the shipped safety ceiling.')
  }
  if (!finiteInteger(plan.gravity.intervalTicks, 1)
    || !finiteInteger(plan.gravity.spawnDelayTicks)) errors.push('Inventory gravity rules are invalid.')
  if (!Number.isFinite(plan.scoring.pointsPerWeight) || plan.scoring.pointsPerWeight < 0
    || !Number.isFinite(plan.scoring.fullRowBonus) || plan.scoring.fullRowBonus < 0) {
    errors.push('Inventory scoring rules are invalid.')
  }
  if (!Number.isFinite(plan.requestedProvisionAmount) || plan.requestedProvisionAmount < 0
    || !Number.isFinite(plan.requiredProvisionAmount) || plan.requiredProvisionAmount < 0
    || !Number.isFinite(plan.eligibleProvisionAmount) || plan.eligibleProvisionAmount < 0
    || plan.eligibleProvisionAmount > plan.requestedProvisionAmount + Number.EPSILON) {
    errors.push('Inventory provision amounts are invalid.')
  }
  if (plan.rosterUnitInstanceIds.length === 0
    || new Set(plan.rosterUnitInstanceIds).size !== plan.rosterUnitInstanceIds.length) {
    errors.push('Inventory plan needs a unique non-empty roster snapshot.')
  }
  const definitions = new Map<string, InventoryItemDefinition>()
  for (const definition of plan.itemDefinitions) {
    if (!definition.id?.trim() || definitions.has(definition.id) || !definition.name?.trim()
      || !finiteInteger(definition.weight, 1) || definition.cells.length === 0
      || new Set(definition.cells.map(pointKey)).size !== definition.cells.length) {
      errors.push(`Inventory item definition ${definition.id || '<missing>'} is invalid or repeated.`)
      continue
    }
    const width = Math.max(...definition.cells.map(cell => cell.x))
      - Math.min(...definition.cells.map(cell => cell.x)) + 1
    const height = Math.max(...definition.cells.map(cell => cell.y))
      - Math.min(...definition.cells.map(cell => cell.y)) + 1
    if (width > plan.board.width || height > plan.board.cartHeight) {
      errors.push(`Inventory item definition ${definition.id} cannot fit inside the configured cart.`)
      continue
    }
    definitions.set(definition.id, definition)
  }
  const instanceIds = new Set<string>()
  let totalAmount = 0
  for (const instance of plan.itemInstances) {
    const definition = definitions.get(instance.definitionId)
    if (!instance.id?.trim() || instanceIds.has(instance.id) || !definition
      || !instance.originCityId?.trim() || !Number.isFinite(instance.amount) || instance.amount <= 0
      || digestTdValue(instance.content) !== digestTdValue(definition.content)
      || instance.content.kind !== 'resource'
      || instance.content.resourceId !== plan.provisionResourceId) {
      errors.push(`Inventory item instance ${instance.id || '<missing>'} is invalid, stale, or not a provision.`)
      continue
    }
    instanceIds.add(instance.id)
    totalAmount += instance.amount
  }
  if (plan.itemInstances.length === 0 || plan.itemInstances.length > plan.maxItems
    || Math.abs(totalAmount - plan.eligibleProvisionAmount) > 0.000001) {
    errors.push('Inventory item instances must cover the eligible provision amount exactly.')
  }
  return errors
}

export function createInventorySimulation(
  plan: InventoryPlan,
  seed: string | number,
): InventorySimulationState {
  const errors = validateInventoryPlan(plan)
  const state: InventorySimulationState = {
    tick: 0,
    rng: createEmpiresRngState(seed),
    queueItemInstanceIds: [],
    activeItem: null,
    placements: [],
    nextSpawnTick: 0,
    score: 0,
    completedRows: [],
    commandLog: [],
    terminalReason: errors.length > 0 ? 'invalid-command' : null,
    error: errors.length > 0 ? errors.join('; ') : null,
  }
  if (!state.terminalReason) {
    state.queueItemInstanceIds = shuffleIds(plan.itemInstances.map(item => item.id), state)
    spawnItem(plan, state)
  }
  return state
}

function commandIdentityReason(
  plan: InventoryPlan,
  state: InventorySimulationState,
  command: InventoryCommand,
): string | null {
  if (state.terminalReason) return `Inventory simulation is already terminal: ${state.terminalReason}.`
  if (command.tick !== state.tick) return 'Inventory command tick does not match the simulation tick.'
  if (command.sequence !== state.commandLog.length) return 'Inventory command sequence is not contiguous.'
  if (command.sessionId !== plan.sessionId || command.planId !== plan.id) {
    return 'Inventory command belongs to another plan or session.'
  }
  if (state.commandLog.length >= plan.maxCommands) return 'Inventory command log limit was reached.'
  if (!state.activeItem) return 'There is no falling inventory item.'
  return null
}

export function inventoryCommandDisabledReason(
  plan: InventoryPlan,
  state: InventorySimulationState,
  command: InventoryCommand,
): string | null {
  const identity = commandIdentityReason(plan, state, command)
  if (identity) return identity
  const active = state.activeItem!
  if (command.kind === 'move') {
    const delta = command.direction === 'left' ? -1 : 1
    return candidateBlocked(plan, state, active, { x: active.anchor.x + delta, y: active.anchor.y })
      ? 'The falling item cannot move in that direction.'
      : null
  }
  if (command.kind === 'rotate') {
    const rotation = normalizedRotation(active.rotation + 1)
    return candidateBlocked(plan, state, active, active.anchor, rotation)
      ? 'The falling item cannot rotate in this position.'
      : null
  }
  return null
}

function applyCommand(plan: InventoryPlan, state: InventorySimulationState, command: InventoryCommand): void {
  const reason = inventoryCommandDisabledReason(plan, state, command)
  if (reason) {
    state.terminalReason = 'invalid-command'
    state.error = reason
    return
  }
  if (command.kind === 'move') tryMove(plan, state, command.direction)
  else if (command.kind === 'rotate' && state.activeItem) {
    state.activeItem.rotation = normalizedRotation(state.activeItem.rotation + 1)
  } else if (command.kind === 'place') hardDrop(plan, state)
  state.commandLog.push(clone(command))
}

export function stepInventorySimulation(
  plan: InventoryPlan,
  state: InventorySimulationState,
  commands: readonly InventoryCommand[] = [],
): InventorySimulationState {
  if (state.terminalReason) return state
  for (const command of commands) {
    applyCommand(plan, state, command)
    if (state.terminalReason) return state
  }
  if (!state.activeItem && state.tick >= state.nextSpawnTick) spawnItem(plan, state)
  if (state.activeItem && state.tick >= state.activeItem.nextGravityTick) {
    const anchor = { x: state.activeItem.anchor.x, y: state.activeItem.anchor.y + 1 }
    if (candidateBlocked(plan, state, state.activeItem, anchor)) lockActiveItem(plan, state)
    else {
      state.activeItem.anchor = anchor
      state.activeItem.nextGravityTick = state.tick + plan.gravity.intervalTicks
    }
  }
  state.tick += 1
  if (!state.terminalReason && state.tick >= plan.maxTicks) state.terminalReason = 'tick-cap'
  return state
}

export function validateInventoryCommandLog(
  plan: InventoryPlan,
  commands: readonly InventoryCommand[],
): string[] {
  const errors: string[] = []
  if (commands.length > plan.maxCommands) errors.push('Inventory command log exceeds the plan limit.')
  let previousTick = -1
  commands.forEach((command, index) => {
    if (!finiteInteger(command.tick) || command.tick >= plan.maxTicks || command.tick < previousTick) {
      errors.push(`Inventory command ${index} has a non-monotonic or out-of-range tick.`)
    }
    if (command.sequence !== index) errors.push(`Inventory command ${index} has a non-contiguous sequence.`)
    if (command.sessionId !== plan.sessionId || command.planId !== plan.id) {
      errors.push(`Inventory command ${index} has stale plan identity.`)
    }
    if (!['move', 'rotate', 'place'].includes(command.kind)) {
      errors.push(`Inventory command ${index} has an unknown kind.`)
    }
    if (command.kind === 'move' && !(INVENTORY_MOVES as readonly string[]).includes(command.direction)) {
      errors.push(`Inventory command ${index} has an unknown movement.`)
    }
    previousTick = command.tick
  })
  return errors
}

function resultFromState(
  plan: InventoryPlan,
  seed: string | number,
  state: InventorySimulationState,
  terminalReason = state.terminalReason ?? 'tick-cap',
): InventoryResult {
  const packed = new Set(state.placements.map(placement => placement.instanceId))
  const packedProvisionAmount = plan.itemInstances
    .filter(instance => packed.has(instance.id))
    .reduce((total, instance) => total + instance.amount, 0)
  return {
    kind: 'inventory',
    sessionId: plan.sessionId,
    planId: plan.id,
    planDigest: digestTdValue(plan),
    commandDigest: digestTdValue(state.commandLog),
    rulesIdentity: clone(plan.rulesIdentity),
    seed,
    expeditionId: plan.expeditionId,
    expeditionAttempt: plan.expeditionAttempt,
    outcome: terminalReason === 'inventory-complete'
      ? 'completed'
      : terminalReason === 'aborted'
        ? 'aborted'
        : 'failure',
    terminalReason,
    completedTick: state.tick,
    score: state.score,
    efficiencyPercent: plan.eligibleProvisionAmount > 0
      ? Math.round(packedProvisionAmount / plan.eligibleProvisionAmount * 100)
      : 100,
    packedProvisionAmount,
    eligibleProvisionAmount: plan.eligibleProvisionAmount,
    packedItemInstanceIds: plan.itemInstances.filter(instance => packed.has(instance.id)).map(instance => instance.id),
    unpackedItemInstanceIds: plan.itemInstances.filter(instance => !packed.has(instance.id)).map(instance => instance.id),
    placements: clone(state.placements),
    commandLog: clone(state.commandLog),
    error: state.error,
  }
}

export function replayInventory(
  plan: InventoryPlan,
  seed: string | number,
  commandLog: readonly InventoryCommand[],
): InventoryResult {
  const errors = [...validateInventoryPlan(plan), ...validateInventoryCommandLog(plan, commandLog)]
  const state = createInventorySimulation(plan, seed)
  if (errors.length > 0) {
    state.terminalReason = 'invalid-command'
    state.error = errors.join('; ')
    return resultFromState(plan, seed, state, 'invalid-command')
  }
  let commandIndex = 0
  while (!state.terminalReason) {
    const due: InventoryCommand[] = []
    while (commandLog[commandIndex]?.tick === state.tick) {
      due.push(commandLog[commandIndex])
      commandIndex += 1
    }
    stepInventorySimulation(plan, state, due)
  }
  if (commandIndex < commandLog.length && !state.error) {
    state.terminalReason = 'invalid-command'
    state.error = 'Inventory command log contains commands after its terminal state.'
  }
  return resultFromState(plan, seed, state)
}

export function abortInventory(
  plan: InventoryPlan,
  seed: string | number,
  commandLog: readonly InventoryCommand[],
  abortTick: number,
): InventoryResult {
  const errors = [...validateInventoryPlan(plan), ...validateInventoryCommandLog(plan, commandLog)]
  const state = createInventorySimulation(plan, seed)
  if (!finiteInteger(abortTick) || abortTick >= plan.maxTicks) errors.push('Inventory abort tick is invalid.')
  if (commandLog.some(command => command.tick > abortTick)) errors.push('Inventory abort log contains future commands.')
  if (errors.length > 0) {
    state.terminalReason = 'invalid-command'
    state.error = errors.join('; ')
    return resultFromState(plan, seed, state, 'invalid-command')
  }
  let commandIndex = 0
  while (!state.terminalReason && state.tick <= abortTick) {
    const due: InventoryCommand[] = []
    while (commandLog[commandIndex]?.tick === state.tick) {
      due.push(commandLog[commandIndex])
      commandIndex += 1
    }
    if (state.tick === abortTick) {
      for (const command of due) applyCommand(plan, state, command)
      break
    }
    stepInventorySimulation(plan, state, due)
  }
  if (state.terminalReason) return resultFromState(plan, seed, state)
  return resultFromState(plan, seed, state, 'aborted')
}

export function consumeInventoryFrameTime(
  accumulatorMs: number,
  elapsedMs: number,
  tickMs: number,
  maxCatchUpTicksPerFrame: number,
): InventoryFrameClock {
  const safeElapsed = Math.max(0, Number.isFinite(elapsedMs) ? elapsedMs : 0)
  const capped = Math.min(safeElapsed, tickMs * maxCatchUpTicksPerFrame)
  const total = Math.max(0, accumulatorMs) + capped
  const ticks = Math.min(maxCatchUpTicksPerFrame, Math.floor(total / tickMs))
  return {
    ticks,
    accumulatorMs: total - ticks * tickMs,
    droppedMs: Math.max(0, safeElapsed - capped),
  }
}
