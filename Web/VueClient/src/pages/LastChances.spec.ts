import { cleanup, render, waitFor } from '@testing-library/vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../public/99lc/game-config.json'

const mocks = vi.hoisted(() => ({
  loadConfig: vi.fn(),
}))

vi.mock('../features/last-chances', async () => {
  const actual = await vi.importActual<typeof import('../features/last-chances')>(
    '../features/last-chances',
  )
  class FakeLastChancesEngine {
    start() {}
    destroy() {}
    setTouchMove() {}
    setTouchAim() {}
    press() {}
    release() {}
  }
  return {
    ...actual,
    LastChancesEngine: FakeLastChancesEngine,
    loadLastChancesConfig: mocks.loadConfig,
  }
})

import { cloneLastChancesConfig } from '../features/last-chances'
import type { LastChancesConfig } from '../features/last-chances'
import LastChances from './LastChances.vue'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

afterEach(() => {
  cleanup()
  mocks.loadConfig.mockReset()
})

describe('99LC page loadout HUD', () => {
  it('renders both resolved attack sets of a two-handed weapon', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const weapon = config.weapons[0]
    weapon.id = 'hud-greatblade'
    weapon.name = 'HUD greatblade'
    weapon.equipMode = 'twoHanded'
    delete weapon.hand
    weapon.secondaryAttacks = cloneLastChancesConfig(defaultConfig).weapons[1].attacks
    weapon.secondaryAttacks.tap.name = 'HUD secondary tap'
    config.weapons = [weapon]
    config.loadout = { primaryWeaponId: weapon.id, secondaryWeaponId: null }
    mocks.loadConfig.mockResolvedValue(config)

    const { container } = render(LastChances)

    await waitFor(() => {
      expect(container.querySelectorAll('.lc-weapon')).toHaveLength(2)
    })
    const primary = container.querySelector('.lc-weapon.is-primary')
    const secondary = container.querySelector('.lc-weapon.is-secondary')
    expect(primary?.querySelector('h3')?.textContent).toBe('HUD greatblade')
    expect(secondary?.querySelector('h3')?.textContent).toBe('HUD greatblade')
    expect(primary?.textContent).toContain(
      `${weapon.attacks.tap.name} → ${weapon.tapCombo?.[0].name}`,
    )
    expect(secondary?.textContent).toContain(
      `HUD secondary tap → ${weapon.secondaryTapCombo?.[0].name}`,
    )
  })
})
