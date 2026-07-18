import { describe, expect, it, vi } from 'vitest'
import type { LastChancesFeedbackPreferences } from './preferences'
import {
  DualSenseFeedbackController,
  StandardsGamepadHapticsOutput,
  type DualSenseFeedbackControllerConfig,
  type LastChancesEnhancedFeedbackOutput,
  type LastChancesFeedbackEffect,
  type LastChancesFeedbackOutputCapability,
  type LastChancesGamepadHapticActuatorLike,
} from './feedback'
import {
  LAST_CHANCES_TACTILE_PROFILES,
  type LastChancesAdaptiveTriggerProfileDefinition,
  type LastChancesTactileProfile,
} from './types'

function adaptiveProfile(
  overrides: Partial<LastChancesAdaptiveTriggerProfileDefinition> = {},
): LastChancesAdaptiveTriggerProfileDefinition {
  return {
    startPosition: 0.15,
    endPosition: 0.75,
    resistance: 0.35,
    force: 0.4,
    transitionMs: 40,
    effectMs: 120,
    magnitude: 0.6,
    ...overrides,
  }
}

function controllerConfig(): DualSenseFeedbackControllerConfig {
  const profiles = Object.fromEntries(
    LAST_CHANCES_TACTILE_PROFILES.map(profile => [profile, adaptiveProfile()]),
  ) as Record<LastChancesTactileProfile, LastChancesAdaptiveTriggerProfileDefinition>
  profiles.impact = adaptiveProfile({ effectMs: 500, magnitude: 0.9 })
  profiles.blocked = adaptiveProfile({ effectMs: 90, magnitude: 0.4 })
  return {
    maxMagnitude: 0.5,
    maxDurationMs: 150,
    blockedRepeatMs: 250,
    profiles,
  }
}

function effect(overrides: Partial<LastChancesFeedbackEffect> = {}): LastChancesFeedbackEffect {
  return {
    state: 'impact',
    profile: 'impact',
    hand: 'both',
    magnitude: 0.8,
    durationMs: 400,
    priority: 100,
    adaptiveProfile: adaptiveProfile(),
    ...overrides,
  }
}

function deferred(): { promise: Promise<void>, resolve: () => void } {
  let resolve = () => undefined
  const promise = new Promise<void>((next) => { resolve = next })
  return { promise, resolve }
}

class FakeEnhancedOutput implements LastChancesEnhancedFeedbackOutput {
  readonly effects: LastChancesFeedbackEffect[] = []
  neutralizeCalls = 0
  enableCalls = 0
  disableCalls = 0
  disposeCalls = 0
  playResult = true
  maxConcurrent = 0
  playGate: Promise<void> | null = null
  private concurrent = 0
  private enabled = false

  constructor(
    private readonly callLog?: string[],
    private readonly label = 'enhanced',
  ) {}

  capability(): LastChancesFeedbackOutputCapability {
    return this.enabled
      ? { tier: 2, status: 'enhanced', permission: 'granted', message: null }
      : { tier: 0, status: 'controls-only', permission: 'not-requested', message: null }
  }

  async play(next: LastChancesFeedbackEffect): Promise<boolean> {
    this.concurrent += 1
    this.maxConcurrent = Math.max(this.maxConcurrent, this.concurrent)
    this.effects.push(next)
    this.callLog?.push(`${this.label}:play:${next.hand}`)
    await (this.playGate ?? Promise.resolve())
    this.concurrent -= 1
    return this.playResult
  }

  async neutralize(): Promise<void> {
    this.neutralizeCalls += 1
    this.callLog?.push(`${this.label}:neutralize`)
  }

  async enableEnhancedFeatures(): Promise<boolean> {
    this.enableCalls += 1
    this.enabled = true
    return true
  }

  async disableEnhancedFeatures(): Promise<void> {
    this.disableCalls += 1
    this.enabled = false
  }

  async dispose(): Promise<void> {
    this.disposeCalls += 1
    this.callLog?.push(`${this.label}:dispose`)
  }
}

const fullPreferences: LastChancesFeedbackPreferences = { mode: 'full', intensity: 1 }

describe('99LC feedback controller', () => {
  it('coalesces a frame to the strongest semantic effect and applies configured caps', async () => {
    const enhanced = new FakeEnhancedOutput()
    await enhanced.enableEnhancedFeatures()
    const controller = new DualSenseFeedbackController(
      controllerConfig(),
      { mode: 'full', intensity: 0.8 },
      { enhanced },
    )

    expect(controller.emit({ state: 'ready', profile: 'click', hand: 'left' })).toBe(true)
    expect(controller.emit({ state: 'charge', profile: 'bandLight', hand: 'left' })).toBe(true)
    expect(controller.emit({ state: 'impact', profile: 'impact', hand: 'right' })).toBe(true)
    await controller.flush()

    expect(enhanced.effects).toHaveLength(1)
    expect(enhanced.effects[0]).toMatchObject({
      state: 'impact',
      profile: 'impact',
      hand: 'right',
      magnitude: 0.5,
      durationMs: 150,
    })
  })

  it('serializes equal-priority effects through exactly one writer', async () => {
    const enhanced = new FakeEnhancedOutput()
    await enhanced.enableEnhancedFeatures()
    const controller = new DualSenseFeedbackController(controllerConfig(), fullPreferences, { enhanced })

    controller.emit({ state: 'impact', profile: 'impact', hand: 'left' })
    controller.emit({ state: 'blocked', profile: 'impact', hand: 'right' })
    await controller.flush()

    expect(enhanced.effects).toHaveLength(2)
    expect(enhanced.maxConcurrent).toBe(1)
  })

  it('merges an authored combo-node adaptive override into the semantic profile', async () => {
    const enhanced = new FakeEnhancedOutput()
    await enhanced.enableEnhancedFeatures()
    const controller = new DualSenseFeedbackController(controllerConfig(), fullPreferences, { enhanced })

    controller.emit({
      state: 'continuation',
      profile: 'gate',
      hand: 'right',
      adaptiveOverride: {
        startPosition: 0.52,
        force: 0.7,
        effectMs: 80,
        magnitude: 0.25,
      },
    })
    await controller.flush()

    expect(enhanced.effects[0]).toMatchObject({
      magnitude: 0.25,
      durationMs: 80,
      adaptiveProfile: {
        startPosition: 0.52,
        endPosition: 0.75,
        force: 0.7,
        effectMs: 80,
      },
    })
  })

  it('rate-limits blocked cues and keeps Reduced mode to blocked/impact feedback', async () => {
    let now = 100
    const enhanced = new FakeEnhancedOutput()
    await enhanced.enableEnhancedFeatures()
    const controller = new DualSenseFeedbackController(
      controllerConfig(),
      { mode: 'reduced', intensity: 1 },
      { enhanced, now: () => now },
    )

    expect(controller.emit({ state: 'ready', profile: 'click' })).toBe(false)
    expect(controller.emit({ state: 'blocked', profile: 'blocked' })).toBe(true)
    expect(controller.emit({ state: 'blocked', profile: 'blocked' })).toBe(false)
    await controller.flush()
    now += 250
    expect(controller.emit({ state: 'blocked', profile: 'blocked' })).toBe(true)
    await controller.flush()
    expect(enhanced.effects).toHaveLength(2)

    await controller.setPreferences({ mode: 'off', intensity: 1 })
    expect(controller.emit({ state: 'impact', profile: 'impact' })).toBe(false)
    expect(enhanced.neutralizeCalls).toBeGreaterThan(0)
  })

  it('negotiates enhanced, standard vibration, and controls-only tiers without implicit enable', async () => {
    const pulse = vi.fn(async () => true)
    const enhanced = new FakeEnhancedOutput()
    const controller = new DualSenseFeedbackController(controllerConfig(), fullPreferences, {
      enhanced,
      gamepad: { connected: true, vibrationActuator: { pulse } },
    })

    expect(enhanced.enableCalls).toBe(0)
    expect(controller.snapshot()).toMatchObject({
      tier: 1,
      status: 'vibration',
      permission: 'not-requested',
    })
    expect(await controller.enableEnhancedFeatures()).toBe(true)
    expect(enhanced.enableCalls).toBe(1)
    expect(controller.snapshot()).toMatchObject({ tier: 2, status: 'enhanced', permission: 'granted' })
    pulse.mockClear()

    controller.emit({ state: 'impact', profile: 'impact' })
    await controller.flush()
    expect(enhanced.effects).toHaveLength(1)
    expect(pulse).not.toHaveBeenCalled()

    await controller.disableEnhancedFeatures()
    expect(controller.snapshot()).toMatchObject({ tier: 1, status: 'vibration' })
    await controller.setGamepad(null)
    expect(controller.snapshot()).toMatchObject({ tier: 0, status: 'controls-only' })
  })

  it('falls back to standards haptics when an enhanced write fails', async () => {
    const pulse = vi.fn(async () => true)
    const enhanced = new FakeEnhancedOutput()
    enhanced.playResult = false
    await enhanced.enableEnhancedFeatures()
    const controller = new DualSenseFeedbackController(controllerConfig(), fullPreferences, {
      enhanced,
      gamepad: { connected: true, vibrationActuator: { pulse } },
    })

    controller.emit({ state: 'impact', profile: 'impact' })
    await controller.flush()
    expect(enhanced.effects).toHaveLength(1)
    expect(pulse).toHaveBeenCalledTimes(1)
  })

  it('neutralizes all outputs on cleanup and disposal', async () => {
    const reset = vi.fn(async () => undefined)
    const enhanced = new FakeEnhancedOutput()
    await enhanced.enableEnhancedFeatures()
    const controller = new DualSenseFeedbackController(controllerConfig(), fullPreferences, {
      enhanced,
      gamepad: { connected: true, vibrationActuator: { reset, pulse: async () => true } },
    })

    await controller.neutralize()
    expect(enhanced.neutralizeCalls).toBe(1)
    expect(reset).toHaveBeenCalledTimes(1)
    await controller.dispose()
    expect(enhanced.disposeCalls).toBe(1)
  })

  it('holds effects emitted during cleanup until the previous writer is neutral', async () => {
    const calls: string[] = []
    const firstWrite = deferred()
    const enhanced = new FakeEnhancedOutput(calls, 'writer')
    enhanced.playGate = firstWrite.promise
    await enhanced.enableEnhancedFeatures()
    const controller = new DualSenseFeedbackController(controllerConfig(), fullPreferences, { enhanced })

    controller.emit({ state: 'impact', profile: 'impact', hand: 'left' })
    await Promise.resolve()
    await Promise.resolve()
    expect(calls).toEqual(['writer:play:left'])

    const cleanup = controller.neutralize()
    expect(controller.emit({ state: 'impact', profile: 'impact', hand: 'right' })).toBe(true)
    await Promise.resolve()
    expect(calls).toEqual(['writer:play:left'])

    firstWrite.resolve()
    await cleanup
    await controller.flush()
    expect(calls).toEqual([
      'writer:play:left',
      'writer:neutralize',
      'writer:play:right',
    ])
  })

  it('keeps a replacement writer behind disposal of the previous controller', async () => {
    const calls: string[] = []
    const firstWrite = deferred()
    const previousOutput = new FakeEnhancedOutput(calls, 'previous')
    previousOutput.playGate = firstWrite.promise
    await previousOutput.enableEnhancedFeatures()
    const previous = new DualSenseFeedbackController(
      controllerConfig(),
      fullPreferences,
      { enhanced: previousOutput },
    )
    previous.emit({ state: 'impact', profile: 'impact', hand: 'left' })
    await Promise.resolve()
    await Promise.resolve()

    const cleanup = previous.dispose()
    const nextOutput = new FakeEnhancedOutput(calls, 'next')
    await nextOutput.enableEnhancedFeatures()
    const next = new DualSenseFeedbackController(
      controllerConfig(),
      fullPreferences,
      { enhanced: nextOutput },
    )
    const replacementReady = next.waitForOutputBarrier(cleanup)
    expect(next.emit({ state: 'impact', profile: 'impact', hand: 'right' })).toBe(true)
    await Promise.resolve()
    expect(calls).toEqual(['previous:play:left'])

    firstWrite.resolve()
    await cleanup
    await replacementReady
    await next.flush()
    expect(calls).toEqual([
      'previous:play:left',
      'previous:neutralize',
      'previous:dispose',
      'next:play:right',
    ])
  })
})

describe('standards Gamepad haptics output', () => {
  it('detects supported effects and bounds every playEffect parameter', async () => {
    const playEffect = vi.fn(async () => 'complete')
    const actuator: LastChancesGamepadHapticActuatorLike = {
      effects: ['dual-rumble'],
      playEffect,
    }
    const output = new StandardsGamepadHapticsOutput(
      { connected: true, vibrationActuator: actuator },
      0.45,
      90,
    )

    expect(output.capability()).toMatchObject({ tier: 1, status: 'vibration' })
    expect(await output.play(effect({ hand: 'left' }))).toBe(true)
    expect(playEffect).toHaveBeenCalledWith('dual-rumble', {
      duration: 90,
      startDelay: 0,
      strongMagnitude: 0.45,
      weakMagnitude: 0.29250000000000004,
      leftTrigger: 0.45,
      rightTrigger: 0,
    })
  })

  it('uses pulse as a feature-detected fallback and sends a neutral stop', async () => {
    const pulse = vi.fn(async () => true)
    const output = new StandardsGamepadHapticsOutput({ vibrationActuator: { pulse } })
    expect(await output.play(effect({ magnitude: 0.3, durationMs: 75 }))).toBe(true)
    await output.neutralize()
    expect(pulse.mock.calls).toEqual([[0.3, 75], [0, 0]])
  })

  it('absorbs rejected haptic promises and demotes capability without an unhandled rejection', async () => {
    const playEffect = vi.fn(async () => { throw new Error('actuator disconnected') })
    const output = new StandardsGamepadHapticsOutput({
      vibrationActuator: { effects: ['dual-rumble'], playEffect },
    })
    expect(await output.play(effect())).toBe(false)
    expect(output.capability()).toMatchObject({ tier: 0, status: 'error' })
    expect(output.capability().message).toContain('actuator disconnected')
  })
})
