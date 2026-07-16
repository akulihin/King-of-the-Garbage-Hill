import { cleanup, fireEvent, render, waitFor } from '@testing-library/vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../public/99lc/game-config.json'

const mocks = vi.hoisted(() => ({
  loadConfig: vi.fn(),
  interact: vi.fn(),
  snapshot: null as unknown,
}))

vi.mock('../features/last-chances', async () => {
  const actual = await vi.importActual<typeof import('../features/last-chances')>(
    '../features/last-chances',
  )
  class FakeLastChancesEngine {
    private readonly callbacks: { onSnapshot?: (snapshot: unknown) => void }

    constructor(
      _canvas: unknown,
      _config: unknown,
      callbacks: { onSnapshot?: (snapshot: unknown) => void } = {},
    ) {
      this.callbacks = callbacks
    }

    start() {
      if (mocks.snapshot) this.callbacks.onSnapshot?.(mocks.snapshot)
    }
    destroy() {}
    setTouchMove() {}
    setTouchAim() {}
    press() {}
    release() {}
    interact() {
      mocks.interact()
      return true
    }
  }
  return {
    ...actual,
    LastChancesEngine: FakeLastChancesEngine,
    loadLastChancesConfig: mocks.loadConfig,
  }
})

import { cloneLastChancesConfig } from '../features/last-chances'
import type {
  LastChancesConfig,
  LastChancesSnapshot,
} from '../features/last-chances'
import BuilderDrawer from '../components/last-chances/BuilderDrawer.vue'
import LastChances from './LastChances.vue'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

afterEach(() => {
  cleanup()
  mocks.loadConfig.mockReset()
  mocks.interact.mockReset()
  mocks.snapshot = null
})

function makeSnapshot(
  config: LastChancesConfig,
  overrides: Partial<LastChancesSnapshot> = {},
): LastChancesSnapshot {
  return {
    phase: 'playing',
    paused: false,
    generation: 1,
    chances: config.chances,
    totalDeaths: 0,
    elapsedMs: 1200,
    currentNodeId: null,
    currentTierIndex: 0,
    attemptPath: [],
    availableNodeIds: [],
    deathReason: null,
    player: {
      position: { x: 100, y: 100 },
      aim: { x: 1, y: 0 },
      hp: config.player.baseStats.maxHp,
      mentalHealth: config.player.baseStats.maxMentalHealth,
      stats: { ...config.player.baseStats },
      invulnerableForMs: 0,
    },
    enemies: [],
    projectiles: [],
    hazards: [],
    interaction: null,
    loadout: config.loadout ? { ...config.loadout } : null,
    cooldowns: [],
    lastGesture: null,
    gestureInputs: (['left', 'right'] as const).map(hand => ({
      hand,
      phase: 'idle',
      pressed: false,
      progress: 0,
      remainingMs: 0,
      heldMs: 0,
      sequence: null,
      candidateGesture: null,
      pendingChargeMs: 0,
    })),
    actionCues: [],
    weaponStates: [],
    interactionPrompt: null,
    gamepad: {
      supported: true,
      connected: false,
      status: 'disconnected',
      activeIndex: null,
      connectedCount: 0,
      id: null,
      mapping: null,
      profile: null,
    },
    selectedNodeId: null,
    ...overrides,
  }
}

describe('99LC page loadout HUD', () => {
  it('ships exactly the seven requested weapon definitions', () => {
    expect(defaultConfig.weapons).toHaveLength(7)
    expect(defaultConfig.weapons.map(weapon => weapon.id)).toEqual([
      'twohand-spear',
      'secondary-chain',
      'either-claws',
      'secondary-spider-knife',
      'twohand-axe',
      'twohand-katana',
      'hybrid-sword',
    ])
  })

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

  it('renders segmented charge, weapon resources, disabled gestures, recovery, DOT and interaction cues', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    config.loadout = {
      primaryWeaponId: 'hybrid-sword',
      secondaryWeaponId: 'secondary-chain',
      primaryAugment: 'none',
      secondaryAugment: 'poison',
    }
    const chain = config.weapons.find(weapon => weapon.id === 'secondary-chain')!
    const charge = chain.attacks.hold.charge!
    mocks.snapshot = makeSnapshot(config, {
      gestureInputs: [
        {
          hand: 'left',
          phase: 'idle',
          pressed: false,
          progress: 0,
          remainingMs: 0,
          heldMs: 0,
          sequence: null,
          candidateGesture: null,
          pendingChargeMs: 0,
        },
        {
          hand: 'right',
          phase: 'pressing',
          pressed: true,
          progress: 1,
          remainingMs: 0,
          heldMs: 1200,
          sequence: 'first',
          candidateGesture: 'hold',
          pendingChargeMs: 1200,
        },
      ],
      cooldowns: [{
        hand: 'right',
        gesture: 'tap',
        remainingMs: 0,
        totalMs: 0,
        ready: false,
      }],
      actionCues: [
        {
          hand: 'left',
          weaponId: 'hybrid-sword',
          phase: 'idle',
          gesture: null,
          color: '#66706c',
          heldMs: 0,
          chargeProgress: 0,
          chargeBands: [],
          recoveryMs: 0,
        },
        {
          hand: 'right',
          weaponId: 'secondary-chain',
          phase: 'charging',
          gesture: 'hold',
          color: chain.attacks.hold.color,
          heldMs: 1200,
          chargeProgress: 1200 / charge.maxMs,
          chargeBands: charge.bands.map((band, index, bands) => ({
            id: band.id,
            label: band.label,
            minMs: band.minMs,
            color: band.color,
            active: 1200 >= band.minMs
              && (bands[index + 1] === undefined || 1200 < bands[index + 1].minMs),
          })),
          recoveryMs: 0,
        },
      ],
      weaponStates: [
        {
          weaponId: 'hybrid-sword',
          hand: 'left',
          resourceKind: 'rhythm',
          resource: 72,
          maxResource: 100,
          resourceLabel: 'Ритм Zornhau',
          resourceColor: '#f0d38a',
          storedDot: null,
          rhythm: 'good',
          recoveryMs: 0,
        },
        {
          weaponId: 'secondary-chain',
          hand: 'right',
          resourceKind: 'chain',
          resource: 1,
          maxResource: 1,
          resourceLabel: 'Цепь в руке',
          resourceColor: '#9cc8ff',
          storedDot: 'poison',
          rhythm: 'idle',
          recoveryMs: 350,
        },
      ],
      interactionPrompt: 'E / обе кнопки: схватить Нож-паука со спины',
    })
    mocks.loadConfig.mockResolvedValue(config)

    const { container, getByTestId } = render(LastChances)

    await waitFor(() => {
      expect(container.querySelectorAll('.lc-weapon')).toHaveLength(2)
      expect(container.querySelectorAll('.lc-charge-band')).toHaveLength(charge.bands.length)
    })
    expect(container.querySelector('.lc-charge-copy')?.textContent).toContain('1200')
    expect(container.querySelector('.lc-charge-labels')?.textContent).toContain('Средний бросок')
    expect(container.querySelector('.lc-state-chip.is-dot')?.textContent).toContain('poison')
    expect(container.querySelector('.lc-state-chip.is-recovery')?.textContent).toContain('350')
    expect(container.querySelector('.lc-state-chip.is-rhythm-good')).not.toBeNull()
    expect(container.querySelectorAll('.lc-weapon.is-primary li.is-disabled').length).toBeGreaterThan(0)
    expect(container.querySelector('.lc-weapon.is-secondary li.is-blocked')?.textContent).toContain('Recovery')
    expect(container.querySelector('.lc-interact-button')).not.toBeNull()

    await fireEvent.click(getByTestId('interaction-prompt'))
    expect(mocks.interact).toHaveBeenCalledTimes(1)
  })

  it('does not render an attack button for an intentionally empty secondary slot', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    config.loadout = {
      primaryWeaponId: 'either-claws',
      secondaryWeaponId: null,
      primaryAugment: 'none',
      secondaryAugment: 'poison',
    }
    mocks.snapshot = makeSnapshot(config)
    mocks.loadConfig.mockResolvedValue(config)

    const { container } = render(LastChances)

    await waitFor(() => {
      expect(container.querySelector('.lc-gesture-button.is-primary')).not.toBeNull()
    })
    expect(container.querySelector('.lc-gesture-button.is-secondary')).toBeNull()
  })
})

describe('99LC builder weapon controls', () => {
  it('keeps enabled and behavior coupled and creates collision-free charge-band IDs', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const attack = config.weapons[0].attacks.doubleTapHold
    attack.charge!.bands.forEach((band, index) => { band.id = `charge-${index + 1}` })

    const { container, getByLabelText, getByRole, getByText } = render(BuilderDrawer, {
      props: { open: true, locale: 'en', config },
    })
    await fireEvent.update(getByLabelText('Gesture'), 'doubleTapHold')

    const enabled = getByLabelText('Gesture enabled') as HTMLInputElement
    await fireEvent.click(enabled)
    expect(getByText('Definition valid')).not.toBeNull()
    await fireEvent.click(enabled)
    expect(getByText('Definition valid')).not.toBeNull()

    const removeButtons = container.querySelectorAll<HTMLButtonElement>('.lc-band-row .is-danger')
    await fireEvent.click(removeButtons[1])
    await fireEvent.click(getByText('Add band'))
    await fireEvent.click(getByRole('tab', { name: /Raw JSON/i }))

    const raw = JSON.parse((getByLabelText('99LC JSON configuration') as HTMLTextAreaElement).value) as LastChancesConfig
    const edited = raw.weapons[0].attacks.doubleTapHold
    expect(edited.enabled).toBe(true)
    expect(edited.behavior).toBe('spearRam')
    const ids = edited.charge!.bands.map(band => band.id)
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('creates a valid standard collider when an unauthored gesture is enabled', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const { getByLabelText, getByRole, getByText } = render(BuilderDrawer, {
      props: { open: true, locale: 'en', config },
    })
    await fireEvent.update(getByLabelText('Weapon'), '4')
    await fireEvent.update(getByLabelText('Gesture'), 'hold')
    await fireEvent.click(getByLabelText('Gesture enabled'))

    expect(getByText('Definition valid')).not.toBeNull()
    await fireEvent.click(getByRole('tab', { name: /Raw JSON/i }))
    const raw = JSON.parse((getByLabelText('99LC JSON configuration') as HTMLTextAreaElement).value) as LastChancesConfig
    expect(raw.weapons[4].attacks.hold).toMatchObject({
      enabled: true,
      behavior: 'standard',
      collider: { shape: 'sector', traceMs: 600 },
    })
  })
})
