import { cleanup, fireEvent, render, waitFor, within } from '@testing-library/vue'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../public/99lc/game-config.json'

const mocks = vi.hoisted(() => ({
  loadConfig: vi.fn(),
  interact: vi.fn(),
  constructorCount: 0,
  constructorOptions: [] as unknown[],
  controlSchemes: [] as string[],
  feedbackPreferences: [] as unknown[],
  enableDualSenseFeatures: vi.fn().mockResolvedValue(true),
  disableEnhancedFeedback: vi.fn(),
  applyControlDefinition: vi.fn().mockReturnValue(true),
  uiCommand: null as null | ((command: 'confirm' | 'back' | 'pause') => boolean),
  snapshot: null as unknown,
}))

vi.mock('../features/last-chances', async () => {
  const actual = await vi.importActual<typeof import('../features/last-chances')>(
    '../features/last-chances',
  )
  class FakeLastChancesEngine {
    private readonly callbacks: {
      onSnapshot?: (snapshot: unknown) => void
      onUiCommand?: (command: 'confirm' | 'back' | 'pause') => boolean
    }

    constructor(
      _canvas: unknown,
      _config: unknown,
      callbacks: {
        onSnapshot?: (snapshot: unknown) => void
        onUiCommand?: (command: 'confirm' | 'back' | 'pause') => boolean
      } = {},
      options?: unknown,
    ) {
      this.callbacks = callbacks
      mocks.constructorCount += 1
      mocks.constructorOptions.push(options)
      mocks.uiCommand = callbacks.onUiCommand ?? null
    }

    start() {
      if (mocks.snapshot) this.callbacks.onSnapshot?.(mocks.snapshot)
    }
    destroy() {}
    setPaused() {}
    setTouchMove() {}
    setTouchAim() {}
    press() {}
    release() {}
    setControlScheme(scheme: string) {
      mocks.controlSchemes.push(scheme)
    }
    setFeedbackPreferences(preferences: unknown) {
      mocks.feedbackPreferences.push(preferences)
    }
    enableDualSenseFeatures() {
      return mocks.enableDualSenseFeatures()
    }
    disableEnhancedFeedback() {
      mocks.disableEnhancedFeedback()
    }
    applyControlDefinition(config: unknown) {
      return mocks.applyControlDefinition(config)
    }
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

beforeEach(() => {
  window.localStorage.clear()
})

afterEach(() => {
  cleanup()
  window.localStorage.clear()
  mocks.loadConfig.mockReset()
  mocks.interact.mockReset()
  mocks.constructorCount = 0
  mocks.constructorOptions = []
  mocks.controlSchemes = []
  mocks.feedbackPreferences = []
  mocks.enableDualSenseFeatures.mockClear()
  mocks.disableEnhancedFeedback.mockClear()
  mocks.applyControlDefinition.mockClear()
  mocks.applyControlDefinition.mockReturnValue(true)
  mocks.uiCommand = null
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
    moveQuests: [],
    swarm: null,
    interactionPrompt: null,
    controlScheme: 'legacy',
    controlCue: null,
    controlRoles: [],
    feedback: {
      tier: 0,
      status: 'controls-only',
      mode: 'full',
      intensity: 1,
      reducedHaptics: false,
      permission: 'not-requested',
      message: null,
    },
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
    selectedInteractionChoiceId: null,
    ...overrides,
  }
}

describe('99LC control-scheme preference', () => {
  it('defaults absent storage to DeepList and renders exactly three named options', async () => {
    mocks.loadConfig.mockResolvedValue(cloneLastChancesConfig(defaultConfig))

    const { getByLabelText } = render(LastChances)
    const select = getByLabelText('Control scheme') as HTMLSelectElement

    await waitFor(() => expect(mocks.constructorCount).toBe(1))
    expect(select.value).toBe('legacy')
    expect(Array.from(select.options).map(option => ({ value: option.value, text: option.text }))).toEqual([
      { value: 'legacy', text: 'DeepList' },
      { value: 'mylorik', text: 'mylorik' },
      { value: 'dualsense', text: 'DualSense' },
    ])
    expect(mocks.constructorOptions[0]).toMatchObject({ controlScheme: 'legacy' })
  })

  it('falls back to DeepList when the stored scheme is unknown', async () => {
    window.localStorage.setItem('99lc:control-scheme', 'unknown-scheme')
    mocks.loadConfig.mockResolvedValue(cloneLastChancesConfig(defaultConfig))

    const { getByLabelText } = render(LastChances)

    await waitFor(() => expect(mocks.constructorCount).toBe(1))
    expect((getByLabelText('Control scheme') as HTMLSelectElement).value).toBe('legacy')
    expect(mocks.constructorOptions[0]).toMatchObject({ controlScheme: 'legacy' })
  })

  it('restores a known preference before engine creation', async () => {
    window.localStorage.setItem('99lc:control-scheme', 'mylorik')
    mocks.loadConfig.mockResolvedValue(cloneLastChancesConfig(defaultConfig))

    const { getByLabelText, getByTestId } = render(LastChances)

    await waitFor(() => expect(mocks.constructorCount).toBe(1))
    expect((getByLabelText('Control scheme') as HTMLSelectElement).value).toBe('mylorik')
    expect(getByTestId('control-scheme-summary').textContent).toContain('Immediate strikes')
    expect(mocks.constructorOptions[0]).toMatchObject({ controlScheme: 'mylorik' })
  })

  it('hot-switches and persists without recreating the engine or mutating Builder config', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const original = cloneLastChancesConfig(config)
    mocks.snapshot = makeSnapshot(config)
    mocks.loadConfig.mockResolvedValue(config)
    const { getByLabelText, getByTestId } = render(LastChances)

    await waitFor(() => expect(mocks.constructorCount).toBe(1))
    await fireEvent.update(getByLabelText('Control scheme'), 'mylorik')

    expect(mocks.constructorCount).toBe(1)
    expect(mocks.controlSchemes).toEqual(['mylorik'])
    expect(window.localStorage.getItem('99lc:control-scheme')).toBe('mylorik')
    expect(config).toEqual(original)
    expect(getByTestId('control-scheme-summary').textContent).toContain('Immediate strikes')
    expect(getByTestId('control-guide').textContent).toContain('Technique hold')
    expect(getByTestId('control-guide').textContent).not.toContain('Five gestures per hand')
  })

  it('bridges the Builder live-control payload without recreating or switching the engine', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    mocks.snapshot = makeSnapshot(config, { controlScheme: 'mylorik' })
    mocks.loadConfig.mockResolvedValue(config)
    const { getAllByRole, getByLabelText, getByRole } = render(LastChances)

    await waitFor(() => expect(mocks.constructorCount).toBe(1))
    await fireEvent.click(getAllByRole('button', { name: 'Builder' })[0])
    await fireEvent.update(getByLabelText('Double-tap window (ms)'), '272')
    await fireEvent.click(getByRole('button', { name: 'Apply control tuning live' }))

    expect(mocks.applyControlDefinition).toHaveBeenCalledTimes(1)
    expect((mocks.applyControlDefinition.mock.calls[0][0] as LastChancesConfig).input.doubleTapMs)
      .toBe(272)
    expect(mocks.constructorCount).toBe(1)
    expect(mocks.controlSchemes).toEqual([])
  })

  it('requests enhanced DualSense access only from the explicit action', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    mocks.snapshot = makeSnapshot(config)
    mocks.loadConfig.mockResolvedValue(config)
    const { getByLabelText, getByTestId } = render(LastChances)

    await waitFor(() => expect(mocks.constructorCount).toBe(1))
    await fireEvent.update(getByLabelText('Control scheme'), 'dualsense')
    expect(mocks.enableDualSenseFeatures).not.toHaveBeenCalled()
    expect(getByTestId('dualsense-capability').textContent).toContain('Controls only')
    expect(getByTestId('dualsense-capability').textContent).toContain('Tier 0')
    expect(getByTestId('dualsense-tactile-legend').textContent).toContain('Light click')

    await fireEvent.click(getByTestId('enable-dualsense-features'))
    await waitFor(() => expect(mocks.enableDualSenseFeatures).toHaveBeenCalledTimes(1))
  })

  it('disables enhanced DualSense output only from its explicit action', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    window.localStorage.setItem('99lc:control-scheme', 'dualsense')
    mocks.snapshot = makeSnapshot(config, {
      controlScheme: 'dualsense',
      feedback: {
        tier: 2,
        status: 'enhanced',
        mode: 'full',
        intensity: 1,
        reducedHaptics: false,
        permission: 'granted',
        message: null,
      },
    })
    mocks.loadConfig.mockResolvedValue(config)
    const { getByTestId } = render(LastChances)

    await waitFor(() => expect(mocks.constructorCount).toBe(1))
    await fireEvent.click(getByTestId('disable-dualsense-features'))
    expect(mocks.disableEnhancedFeedback).toHaveBeenCalledTimes(1)
  })

  it('bridges DualSense confirm to page-owned narrative before route selection', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    window.localStorage.setItem('99lc:control-scheme', 'dualsense')
    mocks.snapshot = makeSnapshot(config, {
      phase: 'planning',
      availableNodeIds: ['opening-node'],
      controlScheme: 'dualsense',
    })
    mocks.loadConfig.mockResolvedValue(config)
    const { getByText } = render(LastChances)

    await waitFor(() => expect(mocks.uiCommand).not.toBeNull())
    expect(getByText(`1 / ${config.narrative!.prologue.length}`)).not.toBeNull()
    expect(mocks.uiCommand?.('confirm')).toBe(true)
    await waitFor(() => expect(getByText(`2 / ${config.narrative!.prologue.length}`)).not.toBeNull())
  })

  it('renders the explicit DualSense post-combat controller focus', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    window.localStorage.setItem('99lc:control-scheme', 'dualsense')
    mocks.snapshot = makeSnapshot(config, {
      phase: 'interaction',
      controlScheme: 'dualsense',
      selectedInteractionChoiceId: 'focused-choice',
      interaction: {
        title: 'Controller choice',
        body: 'Select deliberately.',
        choices: [{
          id: 'focused-choice',
          label: 'Focused',
          description: 'The selected choice.',
          effect: {},
          available: true,
        }, {
          id: 'other-choice',
          label: 'Other',
          description: 'Not selected.',
          effect: {},
          available: true,
        }],
      },
    })
    mocks.loadConfig.mockResolvedValue(config)
    const { getByRole } = render(LastChances)

    await waitFor(() => {
      expect(getByRole('button', { name: /Focused/ }).getAttribute('aria-current')).toBe('true')
    })
    expect(getByRole('button', { name: /Other/ }).getAttribute('aria-current')).toBeNull()
  })
})

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
  it('emits control tuning live without replacing the fresh-generation Apply action', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const { container, emitted, getByLabelText, getByRole, getByText } = render(BuilderDrawer, {
      props: { open: true, locale: 'en', config },
    })

    const scalarEdits: Array<[string, string]> = [
      ['Double-tap window (ms)', '271'],
      ['Basic-combo continuation window (ms)', '911'],
      ['Hold threshold (ms)', '661'],
      ['Hold combo limit (ms)', '2311'],
      ['Hold follow-up tap window (ms)', '491'],
      ['Technique hold threshold (ms)', '671'],
      ['One-intent buffer (ms)', '161'],
      ['Continuation window (ms)', '501'],
      ['Trigger activation', '0.24'],
      ['Trigger release', '0.13'],
      ['Trigger hysteresis', '0.1'],
      ['Shallow gate', '0.25'],
      ['Medium gate', '0.5'],
      ['Deep gate', '0.75'],
      ['Final gate', '0.92'],
      ['Maximum magnitude', '0.74'],
      ['Maximum effect (ms)', '940'],
      ['Blocked-cue interval (ms)', '250'],
      ['Start position', '0.19'],
      ['End position', '0.31'],
      ['Resistance', '0.25'],
      ['Force', '0.29'],
      ['Transition (ms)', '36'],
      ['Effect duration (ms)', '91'],
      ['Magnitude', '0.23'],
    ]
    for (const [label, value] of scalarEdits) {
      await fireEvent.update(getByLabelText(label), value)
    }

    const firstNode = container.querySelector<HTMLElement>('.lc-combo-node-editor')!
    await fireEvent.click(within(firstNode).getByLabelText('Override adaptive profile'))
    const overrideEdits: Array<[string, string]> = [
      ['Start position', '0.17'],
      ['End position', '0.32'],
      ['Resistance', '0.26'],
      ['Force', '0.3'],
      ['Transition (ms)', '38'],
      ['Effect duration (ms)', '92'],
      ['Magnitude', '0.24'],
    ]
    for (const [label, value] of overrideEdits) {
      await fireEvent.update(within(firstNode).getByLabelText(label), value)
    }

    expect(getByText('Definition valid')).not.toBeNull()
    await fireEvent.click(getByRole('button', { name: 'Apply control tuning live' }))

    const liveConfig = emitted().applyControls?.[0]?.[0] as LastChancesConfig
    expect(liveConfig.input).toMatchObject({
      doubleTapMs: 271,
      tapComboWindowMs: 911,
      holdMs: 661,
      holdMaxMs: 2311,
      holdThenDoubleTapWindowMs: 491,
      mylorik: {
        techniqueHoldMs: 671,
        bufferMs: 161,
        continuationWindowMs: 501,
      },
      dualsense: {
        activationThreshold: 0.24,
        releaseThreshold: 0.13,
        hysteresis: 0.1,
        gatePositions: { shallow: 0.25, medium: 0.5, deep: 0.75, final: 0.92 },
        feedback: {
          maxMagnitude: 0.74,
          maxDurationMs: 940,
          blockedRepeatMs: 250,
          profiles: {
            click: {
              startPosition: 0.19,
              endPosition: 0.31,
              resistance: 0.25,
              force: 0.29,
              transitionMs: 36,
              effectMs: 91,
              magnitude: 0.23,
            },
          },
        },
      },
    })
    expect(liveConfig.weapons[0].controls?.primary.dualsense.nodes[0].adaptiveOverride).toEqual({
      startPosition: 0.17,
      endPosition: 0.32,
      resistance: 0.26,
      force: 0.3,
      transitionMs: 38,
      effectMs: 92,
      magnitude: 0.24,
    })
    expect(emitted().apply).toBeUndefined()

    await fireEvent.click(getByRole('button', { name: 'Apply & start fresh generation' }))
    expect(emitted().apply).toHaveLength(1)
  })

  it('retunes every authored DualSense node that uses an edited global gate', async () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const originalGates = { ...config.input.dualsense!.gatePositions }
    const replacements = {
      shallow: 0.25,
      medium: 0.5,
      deep: 0.75,
      final: 0.92,
    }
    const originalNodes = config.weapons.flatMap(weapon => (
      [weapon.controls?.primary, weapon.controls?.secondary].flatMap(record => (
        record?.dualsense.nodes.map(node => ({ id: node.id, threshold: node.activationThreshold })) ?? []
      ))
    ))
    const { emitted, getByLabelText, getByRole, getByText } = render(BuilderDrawer, {
      props: { open: true, locale: 'en', config },
    })

    await fireEvent.update(getByLabelText('Shallow gate'), String(replacements.shallow))
    await fireEvent.update(getByLabelText('Medium gate'), String(replacements.medium))
    await fireEvent.update(getByLabelText('Deep gate'), String(replacements.deep))
    await fireEvent.update(getByLabelText('Final gate'), String(replacements.final))

    expect(getByText('Definition valid')).not.toBeNull()
    await fireEvent.click(getByRole('button', { name: 'Apply control tuning live' }))
    const liveConfig = emitted().applyControls?.[0]?.[0] as LastChancesConfig
    expect(liveConfig.input.dualsense?.gatePositions).toEqual(replacements)

    const updatedNodes = liveConfig.weapons.flatMap(weapon => (
      [weapon.controls?.primary, weapon.controls?.secondary].flatMap(record => (
        record?.dualsense.nodes.map(node => ({ id: node.id, threshold: node.activationThreshold })) ?? []
      ))
    ))
    expect(updatedNodes).toHaveLength(originalNodes.length)
    updatedNodes.forEach((node, index) => {
      const original = originalNodes[index]
      const gate = (Object.keys(originalGates) as Array<keyof typeof originalGates>)
        .find(key => originalGates[key] === original.threshold)
      expect(gate).toBeDefined()
      expect(node.id).toBe(original.id)
      expect(node.threshold).toBe(replacements[gate!])
    })
  })

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
