import { cleanup, render } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { installDomLocalization, setLocale } from 'src/i18n'
import { useBattleshipStore } from 'src/store/battleship'
import FleetBuilder from './FleetBuilder.vue'

beforeEach(() => {
  setActivePinia(createPinia())
  setLocale('ru')
})

afterEach(() => {
  cleanup()
  setLocale('en')
})

describe('FleetBuilder range labels', () => {
  it('keeps the Close range enum literal in the Russian UI', () => {
    const store = useBattleshipStore()
    store.shipCatalog = [{
      id: 'desiccator',
      name: 'Desiccator',
      nameRu: 'Иссушитель',
      deckCount: 1,
      range: 'Close',
      cost: 34,
      defaultArmor: 1,
      deckHpOverrides: null,
      space: 1,
      speed: 3,
      isFree: false,
      abilities: [],
      description: '',
      region: 'South',
      regions: ['South'],
      availableUpgrades: [],
    }]

    const view = render(FleetBuilder)
    const stopLocalization = installDomLocalization(view.container)
    const range = view.container.querySelector('.range-class')

    expect(range?.getAttribute('translate')).toBe('no')
    expect(range?.textContent).toBe('Close')
    expect(view.container.textContent).not.toContain('Закрыть')

    stopLocalization()
  })
})
