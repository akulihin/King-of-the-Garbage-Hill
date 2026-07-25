import { describe, expect, it } from 'vitest'
import { anchorForDeck, occupiedCells } from './battleship-geometry'

describe('battleship geometry', () => {
  it('keeps ordinary ships on their orientation axis', () => {
    expect(occupiedCells({
      row: 2,
      col: 3,
      deckCount: 3,
      orientation: 'Horizontal',
    })).toEqual([
      { row: 2, col: 3 },
      { row: 2, col: 4 },
      { row: 2, col: 5 },
    ])
  })

  it('builds both diagonal flagship orientations', () => {
    const diagonal = {
      row: 1,
      col: 2,
      deckCount: 4,
      abilities: ['diagonal_shape'],
    }
    expect(occupiedCells({ ...diagonal, orientation: 'Horizontal' })).toEqual([
      { row: 1, col: 2 },
      { row: 2, col: 3 },
      { row: 3, col: 4 },
      { row: 4, col: 5 },
    ])
    expect(occupiedCells({ ...diagonal, col: 5, orientation: 'Vertical' })).toEqual([
      { row: 1, col: 5 },
      { row: 2, col: 4 },
      { row: 3, col: 3 },
      { row: 4, col: 2 },
    ])
  })

  it('keeps the grabbed diagonal deck under the pointer', () => {
    expect(anchorForDeck(
      { abilities: ['diagonal_shape'] },
      { row: 6, col: 6 },
      'Vertical',
      2,
    )).toEqual({ row: 4, col: 8 })
  })
})
