import { createEmpiresRngState, nextEmpiresRandomInt } from '../rng'
import { digestTdValue } from '../td/engine'
import { EMPIRES_STABILIZATION_BUDGETS } from '../stabilization'
import type {
  EmpiresBuildingDefinition,
  EmpiresEpidemicConfig,
  EmpiresEndgameConfig,
  EmpiresLoyaltyConfig,
  EmpiresQuestsConfig,
  EmpiresTechnologyDefinition,
} from '../types'
import {
  ALCHEMY_COLORS,
  ALCHEMY_MOVES,
  ALCHEMY_SIDES,
} from './types'
import type {
  AlchemyActivePiece,
  AlchemyCell,
  AlchemyColor,
  AlchemyCommand,
  AlchemyFrameClock,
  AlchemyMove,
  AlchemyOutcome,
  AlchemyPlan,
  AlchemyPoint,
  AlchemyResult,
  AlchemyRulesIdentity,
  AlchemySide,
  AlchemySimulationState,
  AlchemyTargetCell,
  EmpiresAlchemyConfig,
} from './types'

const SIDE_ORDER = new Map<AlchemySide, number>(ALCHEMY_SIDES.map((side, index) => [side, index]))
const MOVE_DELTAS: Record<AlchemyMove, AlchemyPoint> = {
  up: { x: 0, y: -1 },
  right: { x: 1, y: 0 },
  down: { x: 0, y: 1 },
  left: { x: -1, y: 0 },
}
const INWARD_MOVE: Record<AlchemySide, AlchemyMove> = {
  top: 'down',
  right: 'left',
  bottom: 'up',
  left: 'right',
}
const OUTWARD_MOVE: Record<AlchemySide, AlchemyMove> = {
  top: 'up',
  right: 'right',
  bottom: 'down',
  left: 'left',
}

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function pointKey(point: AlchemyPoint): string {
  return `${point.x}:${point.y}`
}

function triangular(value: number): number {
  return value * (value + 1) / 2
}

function finiteInteger(value: number, minimum = 0): boolean {
  return Number.isInteger(value) && value >= minimum
}

function normalizedRotation(value: number): 0 | 1 | 2 | 3 {
  return ((value % 4 + 4) % 4) as 0 | 1 | 2 | 3
}

function rotatePoint(point: AlchemyPoint, rotation: number): AlchemyPoint {
  let current = { ...point }
  for (let index = 0; index < normalizedRotation(rotation); index += 1) {
    current = { x: -current.y, y: current.x }
  }
  return current
}

function definitionFor(plan: AlchemyPlan, piece: AlchemyActivePiece) {
  return plan.pieces.find(definition => definition.id === piece.definitionId) ?? null
}

export function alchemyPieceCells(
  plan: AlchemyPlan,
  piece: AlchemyActivePiece,
  anchor = piece.anchor,
  rotation = piece.rotation,
): AlchemyCell[] {
  const definition = definitionFor(plan, piece)
  if (!definition) return []
  return definition.cells.map((cell) => {
    const rotated = rotatePoint(cell, rotation)
    return {
      x: anchor.x + rotated.x,
      y: anchor.y + rotated.y,
      color: piece.color,
    }
  })
}

function insideBoard(plan: AlchemyPlan, cell: AlchemyPoint): boolean {
  return cell.x >= 0 && cell.x < plan.board.width && cell.y >= 0 && cell.y < plan.board.height
}

function pieceDistance(plan: AlchemyPlan, piece: AlchemyActivePiece): number {
  return Math.min(...alchemyPieceCells(plan, piece).map(cell => (
    Math.abs(cell.x - plan.board.centerX) + Math.abs(cell.y - plan.board.centerY)
  )))
}

function refreshControlledPiece(plan: AlchemyPlan, state: AlchemySimulationState): void {
  state.controlledPieceId = state.activePieces
    .slice()
    .sort((left, right) => (
      pieceDistance(plan, left) - pieceDistance(plan, right)
      || (SIDE_ORDER.get(left.side) ?? 0) - (SIDE_ORDER.get(right.side) ?? 0)
      || stableCompare(left.id, right.id)
    ))[0]?.id ?? null
}

function speedPercent(plan: AlchemyPlan, accelerationPieces: number): number {
  const steps = Math.floor(accelerationPieces / plan.acceleration.piecesPerStep)
  return plan.acceleration.baseSpeedPercent
    + triangular(steps) * plan.acceleration.stepPercent
}

function explosionReached(plan: AlchemyPlan, value: number): boolean {
  return plan.acceleration.explosionBoundary === 'above'
    ? value > plan.acceleration.explosionThresholdPercent
    : value >= plan.acceleration.explosionThresholdPercent
}

function moveIntervalTicks(plan: AlchemyPlan, state: AlchemySimulationState): number {
  return Math.max(
    1,
    Math.ceil(plan.spawn.baseMoveIntervalTicks * 100 / Math.max(1, state.speedPercent)),
  )
}

function targetMatches(construction: readonly AlchemyCell[], target: readonly AlchemyTargetCell[]): boolean {
  const occupied = new Map(construction.map(cell => [pointKey(cell), cell]))
  return target.every((required) => {
    const actual = occupied.get(pointKey(required))
    return Boolean(actual && (!required.color || actual.color === 'gray' || actual.color === required.color))
  })
}

function recipeComplete(plan: AlchemyPlan, state: AlchemySimulationState): boolean {
  if (plan.recipe.mode === 'assembly') return targetMatches(state.construction, plan.recipe.targetCells)
  const targetKeys = new Set(plan.recipe.targetCells.map(pointKey))
  return state.construction.length === targetKeys.size
    && state.construction.every(cell => targetKeys.has(pointKey(cell)))
    && targetMatches(state.construction, plan.recipe.targetCells)
}

function mergeConstruction(state: AlchemySimulationState, cells: readonly AlchemyCell[]): void {
  const values = new Map(state.construction.map(cell => [pointKey(cell), cell]))
  for (const cell of cells) values.set(pointKey(cell), clone(cell))
  state.construction = [...values.values()].sort((left, right) => (
    left.y - right.y || left.x - right.x || stableCompare(left.color, right.color)
  ))
}

function settlePiece(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  piece: AlchemyActivePiece,
  collisionCells: readonly AlchemyCell[] = [],
): void {
  if (plan.recipe.mode === 'assembly') {
    mergeConstruction(state, alchemyPieceCells(plan, piece))
  } else {
    const removed = new Set(collisionCells.map(pointKey))
    state.construction = state.construction.filter(cell => !removed.has(pointKey(cell)))
  }
  state.activePieces = state.activePieces.filter(candidate => candidate.id !== piece.id)
  state.settledPieces += 1
  state.accelerationPieces += 1
  state.speedPercent = speedPercent(plan, state.accelerationPieces)
  if (explosionReached(plan, state.speedPercent)) state.terminalReason = 'explosion'
  else if (recipeComplete(plan, state)) state.terminalReason = 'recipe-complete'
  refreshControlledPiece(plan, state)
}

function candidateCollision(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  piece: AlchemyActivePiece,
  anchor: AlchemyPoint,
  rotation = piece.rotation,
): { outOfBounds: boolean, construction: AlchemyCell[], active: boolean } {
  const candidate = alchemyPieceCells(plan, piece, anchor, rotation)
  const outOfBounds = candidate.some(cell => !insideBoard(plan, cell))
  const constructionKeys = new Set(state.construction.map(pointKey))
  const construction = candidate.filter(cell => constructionKeys.has(pointKey(cell)))
  // The authored pieces may overlap while travelling. Only the central construction
  // and board edge become solid; this also keeps spawn timing independent of render order.
  return { outOfBounds, construction, active: false }
}

function crossesCenter(plan: AlchemyPlan, piece: AlchemyActivePiece, cells: readonly AlchemyCell[]): boolean {
  if (piece.side === 'top') return cells.some(cell => cell.y > plan.board.centerY)
  if (piece.side === 'bottom') return cells.some(cell => cell.y < plan.board.centerY)
  if (piece.side === 'left') return cells.some(cell => cell.x > plan.board.centerX)
  return cells.some(cell => cell.x < plan.board.centerX)
}

function tryMove(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  piece: AlchemyActivePiece,
  direction: AlchemyMove,
  settleOnConstruction: boolean,
): 'moved' | 'blocked' | 'settled' | 'overflow' {
  const delta = MOVE_DELTAS[direction]
  const anchor = { x: piece.anchor.x + delta.x, y: piece.anchor.y + delta.y }
  const candidateCells = alchemyPieceCells(plan, piece, anchor)
  const collision = candidateCollision(plan, state, piece, anchor)
  if (collision.outOfBounds || crossesCenter(plan, piece, candidateCells)) {
    if (settleOnConstruction) {
      state.terminalReason = 'overflow'
      return 'overflow'
    }
    return 'blocked'
  }
  if (collision.active) return 'blocked'
  if (collision.construction.length > 0) {
    if (settleOnConstruction) {
      settlePiece(plan, state, piece, collision.construction)
      return 'settled'
    }
    return 'blocked'
  }
  piece.anchor = anchor
  refreshControlledPiece(plan, state)
  return 'moved'
}

function pieceSpawnAnchor(plan: AlchemyPlan, side: AlchemySide, definitionId: string, rotation: number): AlchemyPoint {
  const definition = plan.pieces.find(piece => piece.id === definitionId)!
  const cells = definition.cells.map(cell => rotatePoint(cell, rotation))
  const minX = Math.min(...cells.map(cell => cell.x))
  const maxX = Math.max(...cells.map(cell => cell.x))
  const minY = Math.min(...cells.map(cell => cell.y))
  const maxY = Math.max(...cells.map(cell => cell.y))
  const centeredX = plan.board.centerX - Math.floor((minX + maxX) / 2)
  const centeredY = plan.board.centerY - Math.floor((minY + maxY) / 2)
  if (side === 'top') return { x: centeredX, y: -minY }
  if (side === 'bottom') return { x: centeredX, y: plan.board.height - 1 - maxY }
  if (side === 'left') return { x: -minX, y: centeredY }
  return { x: plan.board.width - 1 - maxX, y: centeredY }
}

function nextSpawnDelay(plan: AlchemyPlan, state: AlchemySimulationState): number {
  return nextEmpiresRandomInt(
    state.rng,
    plan.spawn.minDelayTicks,
    plan.spawn.maxDelayTicks,
  )
}

function spawnPiece(plan: AlchemyPlan, state: AlchemySimulationState): void {
  const recipePieces = plan.pieces.filter(piece => plan.recipe.pieceDefinitionIds.includes(piece.id))
  if (recipePieces.length === 0 || plan.colors.length === 0) {
    state.terminalReason = 'invalid-command'
    state.error = 'Alchemy plan has no available pieces or colors.'
    return
  }
  const side = ALCHEMY_SIDES[nextEmpiresRandomInt(state.rng, 0, ALCHEMY_SIDES.length - 1)]
  const definition = recipePieces[nextEmpiresRandomInt(state.rng, 0, recipePieces.length - 1)]
  const color = plan.colors[nextEmpiresRandomInt(state.rng, 0, plan.colors.length - 1)]
  const rotation = nextEmpiresRandomInt(state.rng, 0, 3) as 0 | 1 | 2 | 3
  const sequence = state.nextPieceSequence
  state.nextPieceSequence += 1
  const piece: AlchemyActivePiece = {
    id: `piece:${sequence}`,
    definitionId: definition.id,
    side,
    color,
    anchor: pieceSpawnAnchor(plan, side, definition.id, rotation),
    rotation,
    nextMoveTick: state.tick + moveIntervalTicks(plan, state),
  }
  const collision = candidateCollision(plan, state, piece, piece.anchor)
  if (collision.outOfBounds || collision.active || collision.construction.length > 0) {
    state.terminalReason = 'overflow'
    return
  }
  state.activePieces.push(piece)
  state.nextSpawnTick = state.tick + nextSpawnDelay(plan, state)
  refreshControlledPiece(plan, state)
}

export function createAlchemyRulesIdentity(
  configSchemaVersion: number,
  alchemy: EmpiresAlchemyConfig,
  references: {
    buildings: readonly EmpiresBuildingDefinition[]
    technologies: readonly EmpiresTechnologyDefinition[]
    epidemics: EmpiresEpidemicConfig
    loyalty?: EmpiresLoyaltyConfig
    quests?: EmpiresQuestsConfig
    settlementConfig?: Omit<EmpiresEndgameConfig, 'seed'>
    sharedResultRetention?: unknown
  },
): AlchemyRulesIdentity {
  return {
    configSchemaVersion,
    rulesDigest: digestTdValue({ alchemy, ...references }),
  }
}

export function validateAlchemyPlan(plan: AlchemyPlan): string[] {
  const errors: string[] = []
  if (!plan.id?.trim() || !plan.sessionId?.trim()) errors.push('Alchemy plan and session IDs are required.')
  if (!plan.rulesIdentity?.rulesDigest?.trim()
    || !finiteInteger(plan.rulesIdentity.configSchemaVersion, 1)) errors.push('Alchemy rules identity is invalid.')
  if (!finiteInteger(plan.tickMs, 1) || !finiteInteger(plan.maxTicks, 1)
    || !finiteInteger(plan.maxCommands, 1) || !finiteInteger(plan.maxCatchUpTicksPerFrame, 1)) {
    errors.push('Alchemy timing, tick, command, and catch-up limits must be positive integers.')
  }
  if (plan.maxTicks > EMPIRES_STABILIZATION_BUDGETS.maxTicks
    || plan.maxCommands > EMPIRES_STABILIZATION_BUDGETS.maxCommands
    || plan.maxCatchUpTicksPerFrame > EMPIRES_STABILIZATION_BUDGETS.maxCatchUpTicksPerFrame
    || plan.tickMs * plan.maxTicks > EMPIRES_STABILIZATION_BUDGETS.maxLogicalReplayDurationMs) {
    errors.push('Alchemy runtime limits exceed the shipped safety ceiling.')
  }
  if (!finiteInteger(plan.board.width, 5) || !finiteInteger(plan.board.height, 5)
    || !finiteInteger(plan.board.centerX) || !finiteInteger(plan.board.centerY)
    || plan.board.centerX >= plan.board.width || plan.board.centerY >= plan.board.height) {
    errors.push('Alchemy board dimensions and center are invalid.')
  }
  if (plan.board.width * plan.board.height > EMPIRES_STABILIZATION_BUDGETS.maxBoardCells
    || plan.pieces.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
    || plan.recipe.initialCells.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
    || plan.recipe.targetCells.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems) {
    errors.push('Alchemy board or component count exceeds the shipped safety ceiling.')
  }
  if (!finiteInteger(plan.spawn.minDelayTicks, 1)
    || !finiteInteger(plan.spawn.maxDelayTicks, plan.spawn.minDelayTicks)
    || !finiteInteger(plan.spawn.baseMoveIntervalTicks, 1)
    || !Number.isFinite(plan.spawn.inwardSpeedMultiplier)
    || plan.spawn.inwardSpeedMultiplier < 1) errors.push('Alchemy spawn and movement rules are invalid.')
  if (!Number.isFinite(plan.acceleration.baseSpeedPercent) || plan.acceleration.baseSpeedPercent <= 0
    || !Number.isFinite(plan.acceleration.stepPercent) || plan.acceleration.stepPercent <= 0
    || !finiteInteger(plan.acceleration.piecesPerStep, 1)
    || !Number.isFinite(plan.acceleration.explosionThresholdPercent)
    || plan.acceleration.explosionThresholdPercent <= plan.acceleration.baseSpeedPercent
    || !['above', 'at-or-above'].includes(plan.acceleration.explosionBoundary)) {
    errors.push('Alchemy acceleration and explosion rules are invalid.')
  }
  if (!plan.recipe?.id?.trim() || !['assembly', 'disassembly'].includes(plan.recipe?.mode)
    || plan.recipe.initialCells.length === 0 || plan.recipe.targetCells.length === 0) {
    errors.push('Alchemy recipe must define a mode, initial cells, and target cells.')
  }
  const validateCells = (cells: readonly AlchemyPoint[], label: string) => {
    const keys = new Set<string>()
    for (const cell of cells) {
      if (!finiteInteger(cell.x) || !finiteInteger(cell.y) || !insideBoard(plan, cell)) {
        errors.push(`${label} contains an out-of-board cell.`)
      }
      if (keys.has(pointKey(cell))) errors.push(`${label} contains duplicate cells.`)
      keys.add(pointKey(cell))
    }
  }
  validateCells(plan.recipe.initialCells, `Alchemy recipe ${plan.recipe.id} initialCells`)
  validateCells(plan.recipe.targetCells, `Alchemy recipe ${plan.recipe.id} targetCells`)
  if (plan.recipe.mode === 'assembly' && !targetMatches(plan.recipe.initialCells, plan.recipe.targetCells)
    && plan.recipe.targetCells.every(cell => plan.recipe.initialCells.some(initial => pointKey(initial) === pointKey(cell)))) {
    errors.push(`Alchemy recipe ${plan.recipe.id} initial colors cannot satisfy its target.`)
  }
  const pieceIds = new Set<string>()
  for (const piece of plan.pieces) {
    if (!piece.id?.trim() || pieceIds.has(piece.id) || !piece.name?.trim() || piece.cells.length === 0) {
      errors.push('Alchemy pieces need unique IDs, names, and cells.')
      continue
    }
    pieceIds.add(piece.id)
    const cells = new Set(piece.cells.map(pointKey))
    if (cells.size !== piece.cells.length) errors.push(`Alchemy piece ${piece.id} has duplicate cells.`)
  }
  if (plan.recipe.pieceDefinitionIds.length === 0
    || new Set(plan.recipe.pieceDefinitionIds).size !== plan.recipe.pieceDefinitionIds.length
    || plan.recipe.pieceDefinitionIds.some(id => !pieceIds.has(id))) {
    errors.push(`Alchemy recipe ${plan.recipe.id} references an invalid piece catalog.`)
  }
  if (plan.colors.length === 0 || new Set(plan.colors).size !== plan.colors.length
    || plan.colors.some(color => color === 'gray' || !(ALCHEMY_COLORS as readonly string[]).includes(color))) {
    errors.push('Alchemy plan colors must be unique non-gray reagent colors.')
  }
  if (Object.values(plan.reagents).some(value => !finiteInteger(value))) {
    errors.push('Alchemy reagent charges must be non-negative integers.')
  }
  if (!plan.explosion.epidemicDefinitionId?.trim()
    || !Number.isFinite(plan.explosion.severityMultiplier)
    || plan.explosion.severityMultiplier <= 0
    || typeof plan.explosion.lockBuildingForCon !== 'boolean'
    || plan.explosion.mutantAftermath?.kind !== 'mutant-outbreak'
    || !finiteInteger(plan.explosion.mutantAftermath.delayCons, 1)
    || !Number.isFinite(plan.explosion.mutantAftermath.populationLoss)
    || plan.explosion.mutantAftermath.populationLoss < 0
    || !Number.isFinite(plan.explosion.mutantAftermath.loyaltyDelta)) {
    errors.push('Alchemy explosion consequence is invalid.')
  }
  return errors
}

export function createAlchemySimulation(
  plan: AlchemyPlan,
  seed: string | number,
): AlchemySimulationState {
  const errors = validateAlchemyPlan(plan)
  const state: AlchemySimulationState = {
    tick: 0,
    rng: createEmpiresRngState(seed),
    construction: clone(plan.recipe.initialCells),
    activePieces: [],
    controlledPieceId: null,
    nextPieceSequence: 1,
    nextSpawnTick: 0,
    settledPieces: 0,
    accelerationPieces: 0,
    speedPercent: plan.acceleration.baseSpeedPercent,
    reagentCharges: {
      removeColor: plan.reagents.removeColorCharges,
      addGray: plan.reagents.addGrayCharges,
      resetAcceleration: plan.reagents.resetAccelerationCharges,
    },
    commandLog: [],
    terminalReason: errors.length > 0 ? 'invalid-command' : null,
    error: errors.length > 0 ? errors.join('; ') : null,
  }
  if (!state.terminalReason && recipeComplete(plan, state)) state.terminalReason = 'recipe-complete'
  if (!state.terminalReason) spawnPiece(plan, state)
  return state
}

function commandIdentityReason(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  command: AlchemyCommand,
): string | null {
  if (state.terminalReason) return `Alchemy simulation is already terminal: ${state.terminalReason}.`
  if (command.tick !== state.tick) return 'Alchemy command tick does not match the simulation tick.'
  if (command.sequence !== state.commandLog.length) return 'Alchemy command sequence is not contiguous.'
  if (command.sessionId !== plan.sessionId || command.planId !== plan.id) {
    return 'Alchemy command belongs to another plan or session.'
  }
  if (state.commandLog.length >= plan.maxCommands) return 'Alchemy command log limit was reached.'
  return null
}

export function alchemyCommandDisabledReason(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  command: AlchemyCommand,
): string | null {
  const identity = commandIdentityReason(plan, state, command)
  if (identity) return identity
  const controlled = state.activePieces.find(piece => piece.id === state.controlledPieceId)
  if (command.kind === 'move') {
    if (!controlled) return 'There is no controlled Alchemy piece.'
    if (command.direction === OUTWARD_MOVE[controlled.side]) return 'A piece cannot move back toward its spawn edge.'
    const multiplier = command.direction === INWARD_MOVE[controlled.side]
      ? Math.max(1, Math.floor(plan.spawn.inwardSpeedMultiplier))
      : 1
    let anchor = { ...controlled.anchor }
    for (let index = 0; index < multiplier; index += 1) {
      const delta = MOVE_DELTAS[command.direction]
      anchor = { x: anchor.x + delta.x, y: anchor.y + delta.y }
      const collision = candidateCollision(plan, state, controlled, anchor)
      if (collision.outOfBounds || collision.active) return 'The selected movement is blocked.'
      if (crossesCenter(plan, controlled, alchemyPieceCells(plan, controlled, anchor))) {
        return 'A piece cannot pass through the central construction.'
      }
      if (collision.construction.length > 0) break
    }
    return null
  }
  if (command.kind === 'rotate') {
    if (!controlled) return 'There is no controlled Alchemy piece.'
    const rotation = normalizedRotation(controlled.rotation + 1)
    const collision = candidateCollision(plan, state, controlled, controlled.anchor, rotation)
    if (collision.outOfBounds || collision.active || collision.construction.length > 0
      || crossesCenter(plan, controlled, alchemyPieceCells(plan, controlled, controlled.anchor, rotation))) {
      return 'The controlled piece cannot rotate in this position.'
    }
    return null
  }
  if (command.kind === 'remove-color') {
    if (state.reagentCharges.removeColor <= 0) return 'No remove-color reagent charges remain.'
    if (command.color === 'gray' || !(ALCHEMY_COLORS as readonly string[]).includes(command.color)) {
      return 'Remove-color needs a primary reagent color.'
    }
    return null
  }
  if (command.kind === 'add-gray') {
    if (state.reagentCharges.addGray <= 0) return 'No add-gray reagent charges remain.'
    if (!state.activePieces.some(piece => piece.id === command.pieceId)) return 'Unknown active Alchemy piece.'
    return null
  }
  if (command.kind === 'reset-acceleration' && state.reagentCharges.resetAcceleration <= 0) {
    return 'No reset-acceleration reagent charges remain.'
  }
  return null
}

function applyCommand(plan: AlchemyPlan, state: AlchemySimulationState, command: AlchemyCommand): void {
  const reason = alchemyCommandDisabledReason(plan, state, command)
  if (reason) {
    state.terminalReason = 'invalid-command'
    state.error = reason
    return
  }
  const controlled = state.activePieces.find(piece => piece.id === state.controlledPieceId)
  if (command.kind === 'move' && controlled) {
    const moves = command.direction === INWARD_MOVE[controlled.side]
      ? Math.max(1, Math.floor(plan.spawn.inwardSpeedMultiplier))
      : 1
    for (let index = 0; index < moves && !state.terminalReason; index += 1) {
      const result = tryMove(plan, state, controlled, command.direction, true)
      if (result !== 'moved') break
    }
  } else if (command.kind === 'rotate' && controlled) {
    controlled.rotation = normalizedRotation(controlled.rotation + 1)
    refreshControlledPiece(plan, state)
  } else if (command.kind === 'remove-color') {
    state.reagentCharges.removeColor -= 1
    state.construction = state.construction.filter(cell => cell.color !== command.color)
    state.activePieces = state.activePieces.filter(piece => piece.color !== command.color)
    refreshControlledPiece(plan, state)
  } else if (command.kind === 'add-gray') {
    state.reagentCharges.addGray -= 1
    const piece = state.activePieces.find(candidate => candidate.id === command.pieceId)
    if (piece) piece.color = 'gray'
  } else if (command.kind === 'reset-acceleration') {
    state.reagentCharges.resetAcceleration -= 1
    state.accelerationPieces = 0
    state.speedPercent = plan.acceleration.baseSpeedPercent
  }
  state.commandLog.push(clone(command))
}

export function stepAlchemySimulation(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  commands: readonly AlchemyCommand[] = [],
): AlchemySimulationState {
  if (state.terminalReason) return state
  for (const command of commands) {
    applyCommand(plan, state, command)
    if (state.terminalReason) return state
  }
  if (state.tick >= state.nextSpawnTick) spawnPiece(plan, state)
  for (const piece of state.activePieces.slice().sort((left, right) => stableCompare(left.id, right.id))) {
    if (state.terminalReason || piece.nextMoveTick > state.tick) continue
    const result = tryMove(plan, state, piece, INWARD_MOVE[piece.side], true)
    if (result === 'moved') piece.nextMoveTick = state.tick + moveIntervalTicks(plan, state)
  }
  if (!state.terminalReason && recipeComplete(plan, state)) state.terminalReason = 'recipe-complete'
  state.tick += 1
  if (!state.terminalReason && state.tick >= plan.maxTicks) state.terminalReason = 'tick-cap'
  return state
}

export function validateAlchemyCommandLog(plan: AlchemyPlan, commands: readonly AlchemyCommand[]): string[] {
  const errors: string[] = []
  if (commands.length > plan.maxCommands) errors.push('Alchemy command log exceeds the plan limit.')
  let previousTick = -1
  commands.forEach((command, index) => {
    if (!finiteInteger(command.tick) || command.tick >= plan.maxTicks || command.tick < previousTick) {
      errors.push(`Alchemy command ${index} has a non-monotonic or out-of-range tick.`)
    }
    if (command.sequence !== index) errors.push(`Alchemy command ${index} has a non-contiguous sequence.`)
    if (command.sessionId !== plan.sessionId || command.planId !== plan.id) {
      errors.push(`Alchemy command ${index} has stale plan identity.`)
    }
    if (!['move', 'rotate', 'remove-color', 'add-gray', 'reset-acceleration'].includes(command.kind)) {
      errors.push(`Alchemy command ${index} has an unknown kind.`)
    }
    if (command.kind === 'move' && !(ALCHEMY_MOVES as readonly string[]).includes(command.direction)) {
      errors.push(`Alchemy command ${index} has an unknown movement.`)
    }
    previousTick = command.tick
  })
  return errors
}

function resultFromState(
  plan: AlchemyPlan,
  seed: string | number,
  state: AlchemySimulationState,
  terminalReason = state.terminalReason ?? 'tick-cap',
): AlchemyResult {
  const outcome: AlchemyOutcome = terminalReason === 'recipe-complete'
    ? 'success'
    : terminalReason === 'explosion'
      ? 'explosion'
      : terminalReason === 'aborted'
        ? 'aborted'
        : 'failure'
  return {
    kind: 'alchemy',
    sessionId: plan.sessionId,
    planId: plan.id,
    planDigest: digestTdValue(plan),
    commandDigest: digestTdValue(state.commandLog),
    rulesIdentity: clone(plan.rulesIdentity),
    seed,
    recipeId: plan.recipe.id,
    mode: plan.recipe.mode,
    outcome,
    terminalReason,
    completedTick: state.tick,
    settledPieces: state.settledPieces,
    speedPercent: state.speedPercent,
    construction: clone(state.construction),
    commandLog: clone(state.commandLog),
    explosionRequest: outcome === 'explosion' ? {
      originCityId: plan.originCityId,
      epidemicDefinitionId: plan.explosion.epidemicDefinitionId,
      severity: plan.explosion.severityMultiplier,
      source: { kind: 'alchemy', id: `alchemy:${plan.sessionId}` },
      mutantAftermath: clone(plan.explosion.mutantAftermath),
    } : null,
    error: state.error,
  }
}

export function replayAlchemy(
  plan: AlchemyPlan,
  seed: string | number,
  commandLog: readonly AlchemyCommand[],
): AlchemyResult {
  const errors = [...validateAlchemyPlan(plan), ...validateAlchemyCommandLog(plan, commandLog)]
  const state = createAlchemySimulation(plan, seed)
  if (errors.length > 0) {
    state.terminalReason = 'invalid-command'
    state.error = errors.join('; ')
    return resultFromState(plan, seed, state, 'invalid-command')
  }
  let commandIndex = 0
  while (!state.terminalReason) {
    const due: AlchemyCommand[] = []
    while (commandLog[commandIndex]?.tick === state.tick) {
      due.push(commandLog[commandIndex])
      commandIndex += 1
    }
    stepAlchemySimulation(plan, state, due)
  }
  if (commandIndex < commandLog.length && !state.error) {
    state.terminalReason = 'invalid-command'
    state.error = 'Alchemy command log contains commands after its terminal state.'
  }
  return resultFromState(plan, seed, state)
}

export function abortAlchemy(
  plan: AlchemyPlan,
  seed: string | number,
  commandLog: readonly AlchemyCommand[],
  abortTick: number,
): AlchemyResult {
  const errors = [...validateAlchemyPlan(plan), ...validateAlchemyCommandLog(plan, commandLog)]
  const state = createAlchemySimulation(plan, seed)
  if (!finiteInteger(abortTick) || abortTick >= plan.maxTicks) errors.push('Alchemy abort tick is invalid.')
  if (commandLog.some(command => command.tick > abortTick)) errors.push('Alchemy abort log contains future commands.')
  if (errors.length > 0) {
    state.terminalReason = 'invalid-command'
    state.error = errors.join('; ')
    return resultFromState(plan, seed, state, 'invalid-command')
  }
  let commandIndex = 0
  while (!state.terminalReason && state.tick <= abortTick) {
    const due: AlchemyCommand[] = []
    while (commandLog[commandIndex]?.tick === state.tick) {
      due.push(commandLog[commandIndex])
      commandIndex += 1
    }
    if (state.tick === abortTick) {
      for (const command of due) applyCommand(plan, state, command)
      break
    }
    stepAlchemySimulation(plan, state, due)
  }
  if (state.terminalReason) return resultFromState(plan, seed, state)
  return resultFromState(plan, seed, state, 'aborted')
}

export function consumeAlchemyFrameTime(
  accumulatorMs: number,
  elapsedMs: number,
  tickMs: number,
  maxCatchUpTicksPerFrame: number,
): AlchemyFrameClock {
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
