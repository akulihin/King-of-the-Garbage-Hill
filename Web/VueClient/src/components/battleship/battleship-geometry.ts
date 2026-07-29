import type { BattleshipOrientation } from 'src/services/signalr'

export const BATTLESHIP_ORIENTATIONS: readonly BattleshipOrientation[] = [
  'Horizontal',
  'Vertical',
  'HorizontalReverse',
  'VerticalReverse',
]

export type BattleshipGeometry = {
  row: number
  col: number
  deckCount: number
  orientation: BattleshipOrientation
  abilities?: string[]
  /**
   * Deck indices remain stable when Famous Ramming removes a physical deck.
   * Using them as offsets preserves holes instead of compacting the hull.
   */
  decks?: Array<{ index: number }>
}

export type BattleshipCellPosition = { row: number; col: number }
export type BattleshipDeckCellPosition = BattleshipCellPosition & { deckIndex: number }
export type BattleshipBowDirection =
  | 'up'
  | 'right'
  | 'down'
  | 'left'
  | 'up-left'
  | 'up-right'
  | 'down-right'
  | 'down-left'

export function isDiagonalShip(ship: Pick<BattleshipGeometry, 'abilities'>): boolean {
  return ship.abilities?.includes('diagonal_shape') ?? false
}

/** Vector from the bow/anchor towards increasing deck indices. */
export function deckOffsetVector(
  orientation: BattleshipOrientation,
  diagonal: boolean,
): BattleshipCellPosition {
  if (diagonal) {
    switch (orientation) {
      case 'Vertical': return { row: 1, col: -1 }
      case 'HorizontalReverse': return { row: -1, col: -1 }
      case 'VerticalReverse': return { row: -1, col: 1 }
      default: return { row: 1, col: 1 }
    }
  }
  switch (orientation) {
    case 'Vertical': return { row: 1, col: 0 }
    case 'HorizontalReverse': return { row: 0, col: -1 }
    case 'VerticalReverse': return { row: -1, col: 0 }
    default: return { row: 0, col: 1 }
  }
}

export function bowDirectionForOrientation(
  orientation: BattleshipOrientation,
  diagonal: boolean,
): BattleshipBowDirection {
  if (diagonal) {
    switch (orientation) {
      case 'Vertical': return 'up-right'
      case 'HorizontalReverse': return 'down-right'
      case 'VerticalReverse': return 'down-left'
      default: return 'up-left'
    }
  }
  switch (orientation) {
    case 'Vertical': return 'up'
    case 'HorizontalReverse': return 'right'
    case 'VerticalReverse': return 'down'
    default: return 'left'
  }
}

export function orientationLabel(orientation: BattleshipOrientation, diagonal: boolean): string {
  const bow = bowDirectionForOrientation(orientation, diagonal)
  const arrows: Record<BattleshipBowDirection, string> = {
    up: '↑',
    right: '→',
    down: '↓',
    left: '←',
    'up-left': '↖',
    'up-right': '↗',
    'down-right': '↘',
    'down-left': '↙',
  }
  return `${diagonal ? 'диаг., ' : ''}нос ${arrows[bow]}`
}

export function occupiedDeckCells(ship: BattleshipGeometry): BattleshipDeckCellPosition[] {
  const step = deckOffsetVector(ship.orientation, isDiagonalShip(ship))
  const deckIndices = ship.decks?.length
    ? ship.decks.map(deck => deck.index).sort((a, b) => a - b)
    : Array.from({ length: ship.deckCount }, (_, index) => index)
  return deckIndices.map(deckIndex => ({
    row: ship.row + step.row * deckIndex,
    col: ship.col + step.col * deckIndex,
    deckIndex,
  }))
}

export function occupiedCells(ship: BattleshipGeometry): BattleshipCellPosition[] {
  return occupiedDeckCells(ship).map(({ row, col }) => ({ row, col }))
}

export function anchorForDeck(
  ship: Pick<BattleshipGeometry, 'abilities'>,
  hovered: BattleshipCellPosition,
  orientation: BattleshipOrientation,
  deckOffset: number,
): BattleshipCellPosition {
  const step = deckOffsetVector(orientation, isDiagonalShip(ship))
  return {
    row: hovered.row - step.row * deckOffset,
    col: hovered.col - step.col * deckOffset,
  }
}
