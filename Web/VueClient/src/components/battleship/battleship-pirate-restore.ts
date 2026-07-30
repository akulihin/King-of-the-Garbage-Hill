import type { BattleshipCell } from 'src/services/signalr'

export type PirateRestoreCell = {
  row: number
  col: number
}

export function pirateRestoreShipIds(cells: BattleshipCell[]): Set<string> {
  return new Set(cells.flatMap(cell =>
    cell.hasShip && cell.shipId && cell.isDevastated && !cell.isCaptured
      ? [cell.shipId]
      : []))
}

export function pirateRestoreCells(cells: BattleshipCell[]): PirateRestoreCell[] {
  const eligibleShipIds = pirateRestoreShipIds(cells)
  return cells
    .filter(cell => !!cell.shipId && eligibleShipIds.has(cell.shipId))
    .map(cell => ({ row: cell.row, col: cell.col }))
}
