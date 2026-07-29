import { describe, expect, it } from 'vitest'
import {
  anchorForDeck,
  bowDirectionForOrientation,
  occupiedCells,
} from './battleship-geometry'

describe('battleship geometry', () => {
  it('supports all four bow directions for ordinary ships', () => {
    const straight = { row: 4, col: 4, deckCount: 3 }
    expect(occupiedCells({ ...straight, orientation: 'Horizontal' }))
      .toEqual([{ row: 4, col: 4 }, { row: 4, col: 5 }, { row: 4, col: 6 }])
    expect(occupiedCells({ ...straight, orientation: 'Vertical' }))
      .toEqual([{ row: 4, col: 4 }, { row: 5, col: 4 }, { row: 6, col: 4 }])
    expect(occupiedCells({ ...straight, orientation: 'HorizontalReverse' }))
      .toEqual([{ row: 4, col: 4 }, { row: 4, col: 3 }, { row: 4, col: 2 }])
    expect(occupiedCells({ ...straight, orientation: 'VerticalReverse' }))
      .toEqual([{ row: 4, col: 4 }, { row: 3, col: 4 }, { row: 2, col: 4 }])
  })

  it('builds all four diagonal flagship orientations', () => {
    const diagonal = {
      row: 4,
      col: 4,
      deckCount: 4,
      abilities: ['diagonal_shape'],
    }
    expect(occupiedCells({ ...diagonal, orientation: 'Horizontal' })).toEqual([
      { row: 4, col: 4 }, { row: 5, col: 5 }, { row: 6, col: 6 }, { row: 7, col: 7 },
    ])
    expect(occupiedCells({ ...diagonal, orientation: 'Vertical' })).toEqual([
      { row: 4, col: 4 }, { row: 5, col: 3 }, { row: 6, col: 2 }, { row: 7, col: 1 },
    ])
    expect(occupiedCells({ ...diagonal, orientation: 'HorizontalReverse' })).toEqual([
      { row: 4, col: 4 }, { row: 3, col: 3 }, { row: 2, col: 2 }, { row: 1, col: 1 },
    ])
    expect(occupiedCells({ ...diagonal, orientation: 'VerticalReverse' })).toEqual([
      { row: 4, col: 4 }, { row: 3, col: 5 }, { row: 2, col: 6 }, { row: 1, col: 7 },
    ])
  })

  it('keeps a grabbed deck under the pointer for forward and reverse poses', () => {
    expect(anchorForDeck(
      { abilities: ['diagonal_shape'] },
      { row: 6, col: 6 },
      'Vertical',
      2,
    )).toEqual({ row: 4, col: 8 })
    expect(anchorForDeck(
      { abilities: [] },
      { row: 6, col: 6 },
      'HorizontalReverse',
      2,
    )).toEqual({ row: 6, col: 8 })
  })

  it('keeps stable deck-index gaps after a ramming collision', () => {
    expect(occupiedCells({
      row: 4,
      col: 2,
      deckCount: 3,
      orientation: 'Horizontal',
      decks: [{ index: 0 }, { index: 2 }, { index: 3 }],
    })).toEqual([
      { row: 4, col: 2 },
      { row: 4, col: 4 },
      { row: 4, col: 5 },
    ])
  })

  it('maps every pose to its actual bow direction', () => {
    expect([
      bowDirectionForOrientation('Horizontal', false),
      bowDirectionForOrientation('Vertical', false),
      bowDirectionForOrientation('HorizontalReverse', false),
      bowDirectionForOrientation('VerticalReverse', false),
    ]).toEqual(['left', 'up', 'right', 'down'])
    expect([
      bowDirectionForOrientation('Horizontal', true),
      bowDirectionForOrientation('Vertical', true),
      bowDirectionForOrientation('HorizontalReverse', true),
      bowDirectionForOrientation('VerticalReverse', true),
    ]).toEqual(['up-left', 'up-right', 'down-right', 'down-left'])
  })
})
