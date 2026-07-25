import { cleanup, fireEvent, render, waitFor } from '@testing-library/vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import Clash from '../../pages/Clash.vue'
import { CLASH_SCAFFOLD } from './clash/catalog'
import { loadBundledEmpiresConfig } from './config'
import type { EmpiresEndgameConfig } from './types'

vi.mock('./config', () => ({
  loadBundledEmpiresConfig: vi.fn(),
}))

function clone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

beforeEach(() => {
  window.history.replaceState({}, '', '/clash')
  vi.mocked(loadBundledEmpiresConfig).mockResolvedValue({
    schemaVersion: 19,
    clash: clone(CLASH_SCAFFOLD),
  } as unknown as EmpiresEndgameConfig)
})

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

describe('standalone Clash page', () => {
  it('runs the production board outside an Empire campaign and can deterministically fast-resolve', async () => {
    window.history.replaceState({}, '', '/clash?seed=standalone-spec&policy=aggressive')
    const view = render(Clash)

    await waitFor(() => {
      expect(view.getByTestId('clash-minigame')).toBeTruthy()
    })
    expect((view.getByTestId('clash-standalone-seed') as HTMLInputElement).value)
      .toBe('standalone-spec')
    expect((view.getByTestId('clash-qa-policy') as HTMLSelectElement).value)
      .toBe('aggressive')

    await fireEvent.click(view.getByTestId('clash-cell-attacker-0-0'))
    expect(view.getByTestId('clash-text-state').textContent).toContain('Ход 1')
    await fireEvent.click(view.getByTestId('clash-qa-resolve'))

    await waitFor(() => {
      expect(view.getByTestId('clash-standalone-result')).toBeTruthy()
    })
    expect(view.queryByTestId('clash-minigame')).toBeNull()
    expect(view.getByTestId('clash-standalone-digest').textContent).toMatch(/^[0-9a-f]{16}$/)

    await fireEvent.update(view.getByTestId('clash-standalone-seed'), 'standalone-restart')
    await fireEvent.click(view.getByTestId('clash-standalone-restart'))

    await waitFor(() => {
      expect(view.getByTestId('clash-minigame')).toBeTruthy()
    })
    expect(view.getByTestId('clash-text-state').textContent).toContain('Ход 0')
    expect((view.getByTestId('clash-standalone-seed') as HTMLInputElement).value)
      .toBe('standalone-restart')
  })

  it('turns a standalone retreat into a canonical aborted result', async () => {
    const view = render(Clash)
    await waitFor(() => {
      expect(view.getByTestId('clash-minigame')).toBeTruthy()
    })

    await fireEvent.click(view.getByTestId('clash-cell-attacker-0-0'))
    await fireEvent.click(view.getByTestId('clash-abort'))

    await waitFor(() => {
      expect(view.getByTestId('clash-standalone-result').textContent).toContain('Бой прерван')
    })
    expect(view.getByTestId('clash-standalone-result').textContent).toContain('aborted')
  })
})
