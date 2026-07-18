import { fireEvent, render } from '@testing-library/vue'
import { nextTick } from 'vue'
import { describe, expect, it } from 'vitest'
import DeckMemoryPanel from '../../components/empires-endgame/DeckMemoryPanel.vue'
import DivineMercyConfirmation from '../../components/empires-endgame/DivineMercyConfirmation.vue'

describe('Empire\'s Endgame God-presence surfaces', () => {
  it('renders immutable deck knowledge in next-draw order and closes accessibly', async () => {
    const view = render(DeckMemoryPanel, {
      props: {
        open: true,
        remainingInspections: 0,
        cards: [
          {
            position: 1,
            instanceId: 'card-hearts-ace',
            definitionId: 'card-hearts-ace',
            name: 'Туз червей',
            suit: 'hearts',
            rank: 'ace',
            inverted: true,
          },
          {
            position: 2,
            instanceId: 'card-clubs-jack',
            definitionId: 'card-clubs-jack',
            name: 'Валет треф',
            suit: 'clubs',
            rank: 'jack',
            inverted: false,
          },
        ],
      },
    })
    await nextTick()

    expect(view.getByRole('dialog', { name: 'Порядок оставшейся колоды' })).toBeTruthy()
    const items = view.getAllByRole('listitem')
    expect(items[0].textContent).toContain('Туз червей')
    expect(items[0].textContent).toContain('Перевёрнута')
    expect(items[1].textContent).toContain('Валет треф')
    expect(items[1].textContent).toContain('Прямая')
    const close = view.getByRole('button', { name: 'Закрыть память колоды' })
    expect(document.activeElement).toBe(close)
    await fireEvent.click(close)
    expect(view.emitted().close).toHaveLength(1)
  })

  it('shows the exact authored Mercy choices, focuses cancel, and emits no hidden action', async () => {
    const view = render(DivineMercyConfirmation, {
      props: {
        open: true,
        title: 'Вы собираетесь потратить Божественную Милость (1/1) на (переворот карты)',
        confirmLabel: 'Да я и сам знаю! Не показывайте мне это больше!',
        cancelLabel: 'Нет, тогда я попридержу...',
        cardName: 'Туз червей',
      },
    })
    await nextTick()

    const cancel = view.getByRole('button', { name: 'Нет, тогда я попридержу...' })
    expect(document.activeElement).toBe(cancel)
    expect(view.emitted().confirm).toBeUndefined()
    await fireEvent.click(cancel)
    expect(view.emitted().cancel).toHaveLength(1)
    expect(view.emitted().confirm).toBeUndefined()
  })
})
