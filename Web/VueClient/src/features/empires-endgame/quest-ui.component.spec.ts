import { cleanup, fireEvent, render, waitFor } from '@testing-library/vue'
import { afterEach, describe, expect, it } from 'vitest'
import DialogueOverlay from '../../components/empires-endgame/DialogueOverlay.vue'
import QuestJournal from '../../components/empires-endgame/QuestJournal.vue'

afterEach(cleanup)

describe('Empire\'s Endgame quest UI', () => {
  it('traps focus, supports numeric keyboard choices, and protects mandatory dismissal', async () => {
    const view = render(DialogueOverlay, {
      props: {
        open: true,
        questId: 'quest-palach',
        title: 'Палач',
        stageName: 'Вербовка Палача',
        speaker: 'Гранд-Советник',
        text: 'Точный авторский текст.',
        status: 'active',
        mandatory: true,
        choices: [
          { id: 'one', label: 'Первый ответ', costs: [], effects: [] },
          { id: 'two', label: 'Второй ответ', costs: ['−1 день'], effects: ['Память: Да'] },
          { id: 'blocked', label: 'Отложенный ответ', costs: [], effects: [], disabled: true, disabledReason: 'Нужен носитель.' },
        ],
      },
    })

    const dialog = await view.findByRole('dialog')
    await waitFor(() => expect(document.activeElement).toBe(dialog))
    await fireEvent.keyDown(dialog, { key: 'Escape' })
    expect(view.emitted().close).toBeUndefined()
    await fireEvent.keyDown(dialog, { key: '2' })
    expect(view.emitted().choose?.[0]).toEqual(['two'])
    expect((view.getByRole('button', { name: /Отложенный ответ/ }) as HTMLButtonElement).disabled).toBe(true)

    await view.rerender({ status: 'completed', choices: [] })
    await fireEvent.click(view.getByTestId('dialogue-dismiss'))
    expect(view.emitted().close).toHaveLength(1)
  })

  it('lists active, completed, failed, and suspended quest records with visible memory', () => {
    const view = render(QuestJournal, {
      props: {
        open: true,
        entries: [
          {
            id: 'active', name: 'Палач', description: 'Описание', stageName: 'Вербовка',
            status: 'active', startedAtCon: 2, finishedAtCon: null,
            memory: [{ label: 'Палач играет в картишки', value: 'Да' }],
          },
          {
            id: 'done', name: 'Золотой идол', description: 'Описание', stageName: 'Решение',
            status: 'completed', startedAtCon: 3, finishedAtCon: 3, memory: [],
          },
          {
            id: 'failed', name: 'Ошибочный путь', description: 'Описание', stageName: 'Финал',
            status: 'failed', startedAtCon: 4, finishedAtCon: 4, memory: [],
          },
          {
            id: 'suspended', name: 'Старое задание', description: 'Описание', stageName: 'Узел',
            status: 'suspended', startedAtCon: 1, finishedAtCon: null, memory: [],
            compatibilityReason: 'Узел отсутствует.',
          },
        ],
      },
    })

    expect(view.getByText('Палач играет в картишки')).toBeTruthy()
    expect(view.getByText('Выполнено · кон 3')).toBeTruthy()
    expect(view.getByText('Провалено · кон 4')).toBeTruthy()
    expect(view.getByText('Узел отсутствует.')).toBeTruthy()
  })
})
