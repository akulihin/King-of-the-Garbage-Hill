import { cleanup, fireEvent, render } from '@testing-library/vue'
import { afterEach, describe, expect, it } from 'vitest'
import type { BattleshipCell } from 'src/services/signalr'
import CellComponent from './CellComponent.vue'

afterEach(cleanup)

function cell(overrides: Partial<BattleshipCell> = {}): BattleshipCell {
  return {
    row: 2,
    col: 3,
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
    ...overrides,
  }
}

describe('CellComponent state priority', () => {
  it.each([
    {
      label: 'own deck',
      isEnemy: false,
      expectedClass: 'cell-ship',
      expectedTooltip: 'Корабль — Маневрирующая двойка',
      expectsIcon: false,
    },
    {
      label: 'revealed enemy deck',
      isEnemy: true,
      expectedClass: 'cell-revealed-ship',
      expectedTooltip: 'Обнаружен корабль',
      expectsIcon: true,
    },
  ])('renders an intact $label over a stale miss', async ({
    isEnemy,
    expectedClass,
    expectedTooltip,
    expectsIcon,
  }) => {
    const view = render(CellComponent, {
      props: {
        cell: cell({ hasShip: true, shipId: 'mover', isMiss: true }),
        isEnemy,
        shipName: 'Маневрирующая двойка',
      },
    })
    const element = view.container.querySelector('.cell')

    expect(element).not.toBeNull()
    expect(element?.classList.contains(expectedClass)).toBe(true)
    expect(element?.classList.contains('cell-miss')).toBe(false)
    expect(Boolean(view.container.querySelector('.cell-icon'))).toBe(expectsIcon)

    await fireEvent.mouseEnter(element!)
    const tooltip = view.emitted().tipShow?.at(-1)?.[1]
    expect(tooltip).toContain(expectedTooltip)
    expect(tooltip).not.toContain('Промах')
  })

  it.each([
    ['destroyed', { isDestroyed: true }, 'cell-destroyed'],
    ['hit', { isHit: true }, 'cell-hit'],
    ['captured', { isCaptured: true }, 'cell-captured'],
  ])('keeps %s state ahead of both ship and miss', (_label, state, expectedClass) => {
    const view = render(CellComponent, {
      props: {
        cell: cell({ hasShip: true, shipId: 'mover', isMiss: true, ...state }),
      },
    })
    const element = view.container.querySelector('.cell')

    expect(element?.classList.contains(expectedClass)).toBe(true)
    expect(element?.classList.contains('cell-ship')).toBe(false)
    expect(element?.classList.contains('cell-miss')).toBe(false)
  })
})

describe('CellComponent summon marker presentation', () => {
  it('labels a live boarding ship by its source while ordinary summons keep type names', async () => {
    const boarding = render(CellComponent, {
      props: {
        cell: cell({
          hasSummon: true,
          summonType: 'Ram',
          summonName: 'Single 1',
          isBoardingSummon: true,
        }),
      },
    })
    const boardingCell = boarding.container.querySelector('.cell')!
    await fireEvent.mouseEnter(boardingCell)
    expect(boarding.emitted().tipShow?.at(-1)?.[1])
      .toContain('Абордажный корабль (Single 1)')
    boarding.unmount()

    const ordinary = render(CellComponent, {
      props: {
        cell: cell({
          hasSummon: true,
          summonType: 'PirateBoat',
          summonName: 'Пираты',
          isBoardingSummon: false,
        }),
      },
    })
    const ordinaryCell = ordinary.container.querySelector('.cell')!
    await fireEvent.mouseEnter(ordinaryCell)
    const ordinaryTooltip = ordinary.emitted().tipShow?.at(-1)?.[1]
    expect(ordinaryTooltip).toContain('Пираты')
    expect(ordinaryTooltip).not.toContain('Абордажный корабль')
  })

  it('keeps ordered death identities and frozen indices while naming boarding history', async () => {
    const view = render(CellComponent, {
      props: {
        cell: cell({
          summonTrails: [
            {
              summonId: 'boarding-1',
              type: 'Ram',
              isBoardingShip: true,
              sourceShipName: 'Drakkar',
            },
            {
              summonId: 'ram-1',
              type: 'Ram',
              isBoardingShip: false,
              sourceShipName: null,
            },
          ],
          summonDeaths: [
            {
              summonId: 'ram-1',
              type: 'Ram',
              isBoardingShip: false,
              sourceShipName: null,
            },
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
          ],
          frozenSummonDeathIndices: [1],
        }),
      },
    })

    const deaths = [...view.container.querySelectorAll('.summon-death')]
    expect(deaths).toHaveLength(3)
    expect(deaths[0]?.classList.contains('summon-death--frozen')).toBe(false)
    expect(deaths[1]?.classList.contains('summon-death--frozen')).toBe(true)
    expect(deaths[2]?.classList.contains('summon-death--boarding')).toBe(true)
    expect(view.container.querySelectorAll('.summon-death-freeze-badge')).toHaveLength(1)

    await fireEvent.mouseEnter(view.container.querySelector('.cell')!)
    const tooltip = view.emitted().tipShow?.at(-1)?.[1]
    expect(tooltip).toContain('След: Абордажный корабль (Drakkar)')
    expect(tooltip).toContain('След: Таран')
    expect(tooltip).toContain('Погиб: Таран')
    expect(tooltip).toContain('Заморожен: Таран')
    expect(tooltip).toContain('Погиб: Абордажный корабль (Single 1)')
  })
})
