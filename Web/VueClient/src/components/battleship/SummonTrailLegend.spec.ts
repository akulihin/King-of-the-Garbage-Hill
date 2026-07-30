import { cleanup, render } from '@testing-library/vue'
import { afterEach, describe, expect, it } from 'vitest'
import type { BattleshipCell } from 'src/services/signalr'
import SummonTrailLegend from './SummonTrailLegend.vue'

afterEach(cleanup)

function trailCell(summonTrails: BattleshipCell['summonTrails']): BattleshipCell {
  return {
    row: 0,
    col: 0,
    isRevealed: true,
    isHit: false,
    isMiss: false,
    isBurning: false,
    hasShip: false,
    shipId: null,
    hasSummon: false,
    summonOwnerId: null,
    summonType: null,
    isScratched: false,
    summonTrails,
  }
}

describe('SummonTrailLegend', () => {
  it('deduplicates ordinary types while listing boarding source ships dynamically', () => {
    const view = render(SummonTrailLegend, {
      props: {
        cells: [
          trailCell([
            {
              summonId: 'ram-1',
              type: 'Ram',
              isBoardingShip: false,
              sourceShipName: null,
            },
            {
              summonId: 'boarding-1',
              type: 'Ram',
              isBoardingShip: true,
              sourceShipName: 'Single 1',
            },
          ]),
          trailCell([
            {
              summonId: 'ram-2',
              type: 'Ram',
              isBoardingShip: false,
              sourceShipName: null,
            },
            {
              summonId: 'boarding-2',
              type: 'Ram',
              isBoardingShip: true,
              sourceShipName: 'Single 1',
            },
            {
              summonId: 'boarding-3',
              type: 'Ram',
              isBoardingShip: true,
              sourceShipName: 'Drakkar',
            },
          ]),
        ],
      },
    })

    const labels = [...view.container.querySelectorAll('.legend-item')]
      .map(element => element.textContent?.trim())
    expect(labels).toEqual([
      'След: Таран',
      'След: Абордажный корабль (Single 1)',
      'След: Абордажный корабль (Drakkar)',
    ])
  })
})
