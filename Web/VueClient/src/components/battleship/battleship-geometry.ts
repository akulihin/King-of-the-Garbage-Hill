export type BattleshipGeometry = {
  row: number
  col: number
  deckCount: number
  orientation: string
  abilities?: string[]
}

export type BattleshipCellPosition = { row: number; col: number }

export function isDiagonalShip(ship: Pick<BattleshipGeometry, 'abilities'>): boolean {
  return ship.abilities?.includes('diagonal_shape') ?? false
}

export function occupiedCells(ship: BattleshipGeometry): BattleshipCellPosition[] {
  const diagonal = isDiagonalShip(ship)
  return Array.from({ length: ship.deckCount }, (_, index) => {
    if (diagonal) {
      return {
        row: ship.row + index,
        col: ship.col + (ship.orientation === 'Vertical' ? -index : index),
      }
    }
    return {
      row: ship.orientation === 'Vertical' ? ship.row + index : ship.row,
      col: ship.orientation === 'Horizontal' ? ship.col + index : ship.col,
    }
  })
}

export function anchorForDeck(
  ship: Pick<BattleshipGeometry, 'abilities'>,
  hovered: BattleshipCellPosition,
  orientation: string,
  deckOffset: number,
): BattleshipCellPosition {
  if (isDiagonalShip(ship)) {
    return {
      row: hovered.row - deckOffset,
      col: hovered.col + (orientation === 'Vertical' ? deckOffset : -deckOffset),
    }
  }
  return {
    row: hovered.row - (orientation === 'Vertical' ? deckOffset : 0),
    col: hovered.col - (orientation === 'Horizontal' ? deckOffset : 0),
  }
}
