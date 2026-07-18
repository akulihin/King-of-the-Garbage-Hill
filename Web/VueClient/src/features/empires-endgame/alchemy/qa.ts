import {
  alchemyCommandDisabledReason,
  alchemyPieceCells,
  createAlchemySimulation,
  replayAlchemy,
  stepAlchemySimulation,
} from './engine'
import type {
  AlchemyActivePiece,
  AlchemyCell,
  AlchemyCommand,
  AlchemyMove,
  AlchemyPlan,
  AlchemyPoint,
  AlchemyResult,
  AlchemySimulationState,
} from './types'

export const ALCHEMY_QA_POLICIES = ['careful', 'greedy', 'reagent-spender'] as const
export type AlchemyQaPolicy = typeof ALCHEMY_QA_POLICIES[number]

function commandIdentity(plan: AlchemyPlan, state: AlchemySimulationState) {
  return {
    tick: state.tick,
    sequence: state.commandLog.length,
    sessionId: plan.sessionId,
    planId: plan.id,
  }
}

function safeCommand(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  value: Omit<AlchemyCommand, 'tick' | 'sequence' | 'sessionId' | 'planId'>,
): AlchemyCommand | null {
  const command = { ...commandIdentity(plan, state), ...value } as AlchemyCommand
  return alchemyCommandDisabledReason(plan, state, command) ? null : command
}

function lateralMove(side: string, alternate: boolean): AlchemyMove {
  if (side === 'top' || side === 'bottom') return alternate ? 'left' : 'right'
  return alternate ? 'up' : 'down'
}

export function createAlchemyPolicyCommandLog(
  plan: AlchemyPlan,
  seed: string | number,
  policy: AlchemyQaPolicy,
): AlchemyCommand[] {
  const state = createAlchemySimulation(plan, seed)
  let lastHandledPieceId = ''
  let removeSpent = false
  let resetSpent = false
  while (!state.terminalReason) {
    const commands: AlchemyCommand[] = []
    const controlled = state.activePieces.find(piece => piece.id === state.controlledPieceId)
    if (controlled && controlled.id !== lastHandledPieceId) {
      if (policy === 'careful') {
        const move = safeCommand(plan, state, {
          kind: 'move',
          direction: lateralMove(controlled.side, state.settledPieces % 2 === 0),
        })
        if (move) commands.push(move)
      } else if (policy === 'reagent-spender' && state.reagentCharges.addGray > 0) {
        const gray = safeCommand(plan, state, { kind: 'add-gray', pieceId: controlled.id })
        if (gray) commands.push(gray)
      }
      lastHandledPieceId = controlled.id
    }
    if (policy === 'reagent-spender' && !removeSpent && state.settledPieces >= 2
      && state.reagentCharges.removeColor > 0) {
      const color = state.construction.find(cell => cell.color !== 'gray')?.color
      if (color && color !== 'gray') {
        const remove = safeCommand(plan, state, { kind: 'remove-color', color })
        if (remove) {
          remove.sequence = state.commandLog.length + commands.length
          commands.push(remove)
          removeSpent = true
        }
      }
    }
    if (policy === 'reagent-spender' && !resetSpent && state.settledPieces >= 3
      && state.reagentCharges.resetAcceleration > 0) {
      const reset = safeCommand(plan, state, { kind: 'reset-acceleration' })
      if (reset) {
        reset.sequence = state.commandLog.length + commands.length
        commands.push(reset)
        resetSpent = true
      }
    }
    stepAlchemySimulation(plan, state, commands)
  }
  return state.commandLog
}

export function resolveAlchemyWithPolicy(
  plan: AlchemyPlan,
  seed: string | number,
  policy: AlchemyQaPolicy = 'greedy',
): AlchemyResult {
  return replayAlchemy(plan, seed, createAlchemyPolicyCommandLog(plan, seed, policy))
}

function cellKey(cell: AlchemyPoint): string {
  return `${cell.x}:${cell.y}`
}

function projectedSpeed(plan: AlchemyPlan, accelerationPieces: number): number {
  const steps = Math.floor(accelerationPieces / plan.acceleration.piecesPerStep)
  return plan.acceleration.baseSpeedPercent
    + steps * (steps + 1) / 2 * plan.acceleration.stepPercent
}

function projectedExplosion(plan: AlchemyPlan, accelerationPieces: number): boolean {
  const speed = projectedSpeed(plan, accelerationPieces)
  return plan.acceleration.explosionBoundary === 'above'
    ? speed > plan.acceleration.explosionThresholdPercent
    : speed >= plan.acceleration.explosionThresholdPercent
}

function settlementsUntilExplosion(plan: AlchemyPlan, state: AlchemySimulationState): number {
  for (let count = 1; count <= plan.maxTicks; count += 1) {
    if (projectedExplosion(plan, state.accelerationPieces + count)) return count
  }
  return plan.maxTicks
}

function crossesCenter(plan: AlchemyPlan, piece: AlchemyActivePiece, cells: readonly AlchemyCell[]): boolean {
  if (piece.side === 'top') return cells.some(cell => cell.y > plan.board.centerY)
  if (piece.side === 'bottom') return cells.some(cell => cell.y < plan.board.centerY)
  if (piece.side === 'left') return cells.some(cell => cell.x > plan.board.centerX)
  return cells.some(cell => cell.x < plan.board.centerX)
}

function targetCoverage(plan: AlchemyPlan, construction: readonly AlchemyCell[]): { complete: boolean, matched: number } {
  const occupied = new Map(construction.map(cell => [cellKey(cell), cell]))
  let matched = 0
  for (const target of plan.recipe.targetCells) {
    const actual = occupied.get(cellKey(target))
    if (actual && (!target.color || actual.color === 'gray' || actual.color === target.color)) matched += 1
  }
  return { complete: matched === plan.recipe.targetCells.length, matched }
}

function projectedPlacement(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  piece: AlchemyActivePiece,
  shift: number,
): { clearance: number, matchedTargets: number } | null {
  const constructionKeys = new Set(state.construction.map(cellKey))
  const lateral = piece.side === 'top' || piece.side === 'bottom' ? 'x' : 'y'
  const lateralDelta = Math.sign(shift)
  let anchor = { ...piece.anchor }
  for (let index = 0; index < Math.abs(shift); index += 1) {
    anchor[lateral] += lateralDelta
    const cells = alchemyPieceCells(plan, piece, anchor)
    if (cells.some(cell => (
      cell.x < 0 || cell.x >= plan.board.width || cell.y < 0 || cell.y >= plan.board.height
      || constructionKeys.has(cellKey(cell))
    )) || crossesCenter(plan, piece, cells)) return null
  }

  const inward: AlchemyPoint = piece.side === 'top' ? { x: 0, y: 1 }
    : piece.side === 'right' ? { x: -1, y: 0 }
      : piece.side === 'bottom' ? { x: 0, y: -1 }
        : { x: 1, y: 0 }
  for (let index = 0; index < Math.max(plan.board.width, plan.board.height); index += 1) {
    const next = { x: anchor.x + inward.x, y: anchor.y + inward.y }
    const nextCells = alchemyPieceCells(plan, piece, next)
    if (nextCells.some(cell => cell.x < 0 || cell.x >= plan.board.width || cell.y < 0 || cell.y >= plan.board.height)
      || crossesCenter(plan, piece, nextCells)) return null
    if (nextCells.some(cell => constructionKeys.has(cellKey(cell)))) {
      const settledCells = alchemyPieceCells(plan, piece, anchor)
      const combined = new Map(state.construction.map(cell => [cellKey(cell), cell]))
      for (const cell of settledCells) combined.set(cellKey(cell), cell)
      const coverage = targetCoverage(plan, [...combined.values()])
      const explodes = projectedExplosion(plan, state.accelerationPieces + 1)
      if (coverage.complete && !explodes) return null
      const clearance = piece.side === 'top'
        ? Math.min(...settledCells.map(cell => cell.y))
        : piece.side === 'bottom'
          ? plan.board.height - 1 - Math.max(...settledCells.map(cell => cell.y))
          : piece.side === 'left'
            ? Math.min(...settledCells.map(cell => cell.x))
            : plan.board.width - 1 - Math.max(...settledCells.map(cell => cell.x))
      return { clearance, matchedTargets: coverage.matched }
    }
    anchor = next
  }
  return null
}

function safestLateralShift(
  plan: AlchemyPlan,
  state: AlchemySimulationState,
  piece: AlchemyActivePiece,
): number | null {
  const remainingCommands = plan.maxCommands - state.commandLog.length
  const remainingSettlements = settlementsUntilExplosion(plan, state)
  const budget = Math.max(0, Math.floor(remainingCommands / Math.max(1, remainingSettlements)))
  const candidates: Array<{ shift: number, clearance: number, matchedTargets: number }> = []
  for (let shift = -budget; shift <= budget; shift += 1) {
    const placement = projectedPlacement(plan, state, piece, shift)
    if (placement) candidates.push({ shift, ...placement })
  }
  return candidates.sort((left, right) => (
    right.clearance - left.clearance
    || left.matchedTargets - right.matchedTargets
    || Math.abs(left.shift) - Math.abs(right.shift)
    || left.shift - right.shift
  ))[0]?.shift ?? null
}

/** Packs pieces near the core without completing the recipe so QA can exercise explosion settlement. */
export function resolveAlchemyExplosionFixture(
  plan: AlchemyPlan,
  seed: string | number,
): AlchemyResult {
  const state = createAlchemySimulation(plan, seed)
  let lastHandledPieceId = ''
  while (!state.terminalReason) {
    const commands: AlchemyCommand[] = []
    const controlled = state.activePieces.find(piece => piece.id === state.controlledPieceId)
    if (controlled && controlled.id !== lastHandledPieceId) {
      const shift = safestLateralShift(plan, state, controlled)
      const direction: AlchemyMove = controlled.side === 'top' || controlled.side === 'bottom'
        ? (shift ?? 0) < 0 ? 'left' : 'right'
        : (shift ?? 0) < 0 ? 'up' : 'down'
      for (let index = 0; index < Math.abs(shift ?? 0); index += 1) {
        const move = safeCommand(plan, state, {
          kind: 'move',
          direction,
        })
        if (!move) break
        move.sequence = state.commandLog.length + commands.length
        commands.push(move)
      }
      lastHandledPieceId = controlled.id
    }
    stepAlchemySimulation(plan, state, commands)
  }
  return replayAlchemy(plan, seed, state.commandLog)
}
