import { describe, expect, it } from 'vitest'
import type { BattleshipCell } from 'src/services/signalr'
import {
  pirateRestoreCells,
  pirateRestoreShipIds,
} from './battleship-pirate-restore'

function cell(overrides: Partial<BattleshipCell>): BattleshipCell {
  return {
    row: 0,
    col: 0,
    isRevealed: true,
    isHit: false,
    isMiss: false,
    isBurning: false,
    hasShip: true,
    shipId: 'ship',
    hasSummon: false,
    summonOwnerId: null,
    summonType: null,
    isScratched: false,
    ...overrides,
  }
}

describe('PirateBoat restore targeting', () => {
  it('highlights every deck of eligible Devastated ships only', () => {
    const cells = [
      cell({ row: 2, col: 1, shipId: 'devastated', isDevastated: true }),
      cell({ row: 2, col: 2, shipId: 'devastated', isDevastated: true }),
      cell({ row: 4, col: 1, shipId: 'intact', isDevastated: false }),
      cell({
        row: 6,
        col: 1,
        shipId: 'captured',
        isDevastated: true,
        isCaptured: true,
      }),
    ]

    expect([...pirateRestoreShipIds(cells)]).toEqual(['devastated'])
    expect(pirateRestoreCells(cells)).toEqual([
      { row: 2, col: 1 },
      { row: 2, col: 2 },
    ])
  })
})
