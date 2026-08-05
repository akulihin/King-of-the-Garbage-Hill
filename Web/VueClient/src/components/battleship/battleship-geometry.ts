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
  decks?: Array<{
    index: number
    offsetRow?: number | null
    offsetCol?: number | null
  }>
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
  return rotateDeckOffset(diagonal ? { row: 1, col: 1 } : { row: 0, col: 1 }, orientation)
}

/** Rotate a hull-local offset defined for a ship facing right. */
export function rotateDeckOffset(
  offset: BattleshipCellPosition,
  orientation: BattleshipOrientation,
): BattleshipCellPosition {
  switch (orientation) {
    case 'Vertical': return { row: offset.col, col: -offset.row }
    case 'HorizontalReverse': return { row: -offset.row, col: -offset.col }
    case 'VerticalReverse': return { row: -offset.col, col: offset.row }
    default: return offset
  }
}

function localDeckOffset(
  ship: Pick<BattleshipGeometry, 'abilities'>,
  deck: { index: number; offsetRow?: number | null; offsetCol?: number | null },
): BattleshipCellPosition {
  if (typeof deck.offsetRow === 'number' && typeof deck.offsetCol === 'number') {
    return { row: deck.offsetRow, col: deck.offsetCol }
  }

  return isDiagonalShip(ship)
    ? { row: deck.index, col: deck.index }
    : { row: 0, col: deck.index }
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
  const decks = ship.decks?.length
    ? [...ship.decks].sort((a, b) => a.index - b.index)
    : Array.from({ length: ship.deckCount }, (_, index) => ({ index }))
  return decks.map(deck => {
    const offset = rotateDeckOffset(localDeckOffset(ship, deck), ship.orientation)
    return {
      row: ship.row + offset.row,
      col: ship.col + offset.col,
      deckIndex: deck.index,
    }
  })
}

export function occupiedCells(ship: BattleshipGeometry): BattleshipCellPosition[] {
  return occupiedDeckCells(ship).map(({ row, col }) => ({ row, col }))
}

export function anchorForDeck(
  ship: Pick<BattleshipGeometry, 'abilities' | 'decks'>,
  hovered: BattleshipCellPosition,
  orientation: BattleshipOrientation,
  deckIndex: number,
): BattleshipCellPosition {
  const deck = ship.decks?.find(candidate => candidate.index === deckIndex) ?? { index: deckIndex }
  const offset = rotateDeckOffset(localDeckOffset(ship, deck), orientation)
  return {
    row: hovered.row - offset.row,
    col: hovered.col - offset.col,
  }
}
