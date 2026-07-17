import { fireEvent, render, waitFor } from '@testing-library/vue'
import { describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import BuilderDrawer from '../../components/empires-endgame/BuilderDrawer.vue'
import { cloneEmpiresConfig, parseEmpiresConfig } from './config'
import type { EmpiresEndgameConfig } from './types'

describe('Empire\'s Endgame Builder combat JSON boundary', () => {
  it('round-trips the complete combat catalog through edit, import, and export controls', async () => {
    const config = cloneEmpiresConfig(defaultConfigJson)
    const updates: EmpiresEndgameConfig[] = []
    const onExport = vi.fn()
    const { container, getByRole } = render(BuilderDrawer, {
      props: {
        config,
        'onUpdate:config': (next: EmpiresEndgameConfig) => updates.push(next),
        onExport,
      },
    })

    await fireEvent.click(getByRole('button', { name: /Весь JSON/ }))
    const editor = container.querySelector<HTMLTextAreaElement>('.json-editor')
    if (!editor) throw new Error('Builder JSON editor did not render')
    const exportedText = editor.value
    const exported = parseEmpiresConfig(exportedText)

    expect(exported.combat).toEqual(config.combat)
    expect(exported.combat.enabled).toBe(true)
    expect(exported.combat.equipment).toHaveLength(31)

    await fireEvent.update(editor, exportedText)
    await fireEvent.click(getByRole('button', { name: /Проверить и применить/ }))
    expect(updates.at(-1)).toEqual(config)

    await fireEvent.click(getByRole('button', { name: /Экспорт/ }))
    expect(onExport).toHaveBeenCalledTimes(1)

    const input = container.querySelector<HTMLInputElement>('input[type="file"]')
    if (!input) throw new Error('Builder JSON import input did not render')
    Object.defineProperty(input, 'files', {
      configurable: true,
      value: [new File([`${exportedText}\n`], 'empires-combat.json', { type: 'application/json' })],
    })
    await fireEvent.update(input)
    await waitFor(() => expect(updates).toHaveLength(2))
    expect(updates.at(-1)?.combat).toEqual(config.combat)
  })
})
