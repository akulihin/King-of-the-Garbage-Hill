import { cleanup, render } from '@testing-library/vue'
import { afterEach, describe, expect, it } from 'vitest'
import type { ClashUnitState, ClashVisualUnitOverride } from 'src/features/clash/types'
import ClashBoard from './ClashBoard.vue'

afterEach(cleanup)

describe('ClashBoard production geometry', () => {
  it('renders the maximum 10×5-per-side battlefield as one aligned grid', () => {
    const view = render(ClashBoard, {
      props: {
        width: 10,
        length: 5,
        cells: [],
        catalogById: new Map(),
        label: 'Большое поле',
      },
    })

    const cells = view.getAllByRole('button')
    expect(cells).toHaveLength(100)
    expect(cells[0]?.getAttribute('data-board-row')).toBe('0')
    expect(cells[99]?.getAttribute('data-board-row')).toBe('9')
  })

  it('does not invent a unit for an owner-private empty DTO cell', () => {
    const view = render(ClashBoard, {
      props: {
        width: 3,
        length: 3,
        cells: [{
          boardRow: 3,
          column: 1,
          territorySide: 'Guest',
          unit: null,
          isHidden: false,
        }],
        catalogById: new Map(),
      },
    })

    expect(view.queryByText('Неизвестный юнит')).toBeNull()
    expect(view.getAllByRole('button')).toHaveLength(18)
  })

  it('does not reveal a post-clash effective-speed change before the timeline ends', () => {
    const before: ClashUnitState = {
      instanceId: 'legionary-1',
      definitionId: 'legionary',
      name: 'Легионер',
      ownerId: 'host',
      ownerSide: 'Host',
      boardRow: 2,
      column: 1,
      hp: 4,
      maxHp: 4,
      attack: 1,
      speed: 3,
      shieldCharges: 0,
      dodgeCharges: 0,
      bleedStacks: 0,
      rangedReadyClash: 0,
      alive: true,
      deployed: true,
      isHidden: false,
      diesToAoe: false,
    }
    const after = { ...before, speed: 1 }
    const visual: ClashVisualUnitOverride = {
      snapshot: before,
      hp: before.hp,
      alive: true,
      boardRow: before.boardRow,
      column: before.column,
      shieldCharges: 0,
      dodgeCharges: 0,
      bleedStacks: 0,
      animation: 'idle',
      animationSequence: 0,
    }
    const view = render(ClashBoard, {
      props: {
        width: 3,
        length: 3,
        cells: [{
          boardRow: after.boardRow,
          column: after.column,
          territorySide: 'Host',
          unit: after,
          isHidden: false,
        }],
        catalogById: new Map(),
        visualOverrides: new Map([[before.instanceId, visual]]),
      },
    })

    expect(view.getByLabelText(/Легионер:.*скорость 3/)).toBeTruthy()
    expect(view.queryByLabelText(/Легионер:.*скорость 1/)).toBeNull()
  })
})
