import type { LastChancesFeedbackPreferences } from './preferences'
import type {
  LastChancesAdaptiveTriggerProfileDefinition,
  LastChancesFeedbackSnapshot,
  LastChancesFeedbackState,
  LastChancesFeedbackTier,
  LastChancesHand,
  LastChancesTactileProfile,
} from './types'

export type LastChancesFeedbackHand = LastChancesHand | 'both'

export interface DualSenseFeedbackControllerConfig {
  maxMagnitude: number
  maxDurationMs: number
  blockedRepeatMs: number
  profiles: Record<LastChancesTactileProfile, LastChancesAdaptiveTriggerProfileDefinition>
}

export interface LastChancesSemanticFeedbackEvent {
  state: LastChancesFeedbackState
  profile: LastChancesTactileProfile
  hand?: LastChancesHand
  strength?: number
  adaptiveOverride?: Partial<LastChancesAdaptiveTriggerProfileDefinition>
}

export interface LastChancesFeedbackEffect {
  state: LastChancesFeedbackState
  profile: LastChancesTactileProfile
  hand: LastChancesFeedbackHand
  magnitude: number
  durationMs: number
  priority: number
  adaptiveProfile: LastChancesAdaptiveTriggerProfileDefinition
}

export interface LastChancesFeedbackOutputCapability {
  tier: LastChancesFeedbackTier
  status: LastChancesFeedbackSnapshot['status']
  permission?: LastChancesFeedbackSnapshot['permission']
  message: string | null
}

export interface LastChancesFeedbackOutput {
  capability(): LastChancesFeedbackOutputCapability
  play(effect: LastChancesFeedbackEffect): Promise<boolean>
  neutralize(): Promise<void>
  dispose?(): Promise<void>
}

export interface LastChancesEnhancedFeedbackOutput extends LastChancesFeedbackOutput {
  enableEnhancedFeatures(): Promise<boolean>
  disableEnhancedFeatures(): Promise<void>
}

export interface LastChancesGamepadHapticEffectParameters {
  duration: number
  startDelay: number
  strongMagnitude: number
  weakMagnitude: number
  leftTrigger: number
  rightTrigger: number
}

export interface LastChancesGamepadHapticActuatorLike {
  readonly effects?: readonly string[]
  playEffect?: (
    type: string,
    parameters: LastChancesGamepadHapticEffectParameters,
  ) => Promise<unknown>
  pulse?: (magnitude: number, durationMs: number) => Promise<unknown>
  reset?: () => Promise<unknown>
}

export interface LastChancesHapticGamepadLike {
  readonly connected?: boolean
  readonly vibrationActuator?: LastChancesGamepadHapticActuatorLike | null
  readonly hapticActuators?: readonly LastChancesGamepadHapticActuatorLike[]
}

interface QueuedFeedbackEffect extends LastChancesFeedbackEffect {
  sequence: number
}

export interface DualSenseFeedbackControllerOutputs {
  gamepad?: LastChancesHapticGamepadLike | null
  enhanced?: LastChancesEnhancedFeedbackOutput | null
  now?: () => number
}

const MAX_PENDING_EFFECTS = 8

const PROFILE_PRIORITIES: Record<LastChancesTactileProfile, number> = {
  click: 20,
  ramp: 30,
  bandLight: 60,
  bandMedium: 65,
  bandStrong: 70,
  gate: 50,
  followUp: 55,
  blocked: 80,
  impact: 100,
  tension: 35,
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value))
}

function normalizedIntensity(value: number): number {
  return Number.isFinite(value) ? clamp(value, 0, 1) : 1
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error)
}

function effectType(actuator: LastChancesGamepadHapticActuatorLike): string | null {
  if (typeof actuator.playEffect !== 'function') return null
  const effects = actuator.effects
  if (!effects || effects.length === 0) return 'dual-rumble'
  if (effects.includes('dual-rumble')) return 'dual-rumble'
  if (effects.includes('trigger-rumble')) return 'trigger-rumble'
  return null
}

function actuatorFor(
  gamepad: LastChancesHapticGamepadLike | null,
): LastChancesGamepadHapticActuatorLike | null {
  if (!gamepad || gamepad.connected === false) return null
  const candidates = [
    gamepad.vibrationActuator ?? null,
    ...(gamepad.hapticActuators ?? []),
  ]
  return candidates.find(candidate => Boolean(
    candidate
    && (effectType(candidate) !== null || typeof candidate.pulse === 'function'),
  )) ?? null
}

/** Standards-only vibration output. It never claims adaptive-trigger resistance. */
export class StandardsGamepadHapticsOutput implements LastChancesFeedbackOutput {
  private gamepad: LastChancesHapticGamepadLike | null
  private demotedMessage: string | null = null

  constructor(
    gamepad: LastChancesHapticGamepadLike | null = null,
    private readonly maxMagnitude = 1,
    private readonly maxDurationMs = 1000,
  ) {
    this.gamepad = gamepad
  }

  setGamepad(gamepad: LastChancesHapticGamepadLike | null): void {
    if (gamepad === this.gamepad) return
    this.gamepad = gamepad
    this.demotedMessage = null
  }

  capability(): LastChancesFeedbackOutputCapability {
    if (this.demotedMessage) {
      return { tier: 0, status: 'error', message: this.demotedMessage }
    }
    return actuatorFor(this.gamepad)
      ? { tier: 1, status: 'vibration', message: null }
      : { tier: 0, status: 'controls-only', message: null }
  }

  async play(effect: LastChancesFeedbackEffect): Promise<boolean> {
    const actuator = actuatorFor(this.gamepad)
    if (!actuator || this.demotedMessage) return false
    const magnitude = clamp(effect.magnitude, 0, this.maxMagnitude)
    const duration = Math.round(clamp(effect.durationMs, 0, this.maxDurationMs))
    try {
      const type = effectType(actuator)
      if (type && actuator.playEffect) {
        await actuator.playEffect(type, {
          duration,
          startDelay: 0,
          strongMagnitude: magnitude,
          weakMagnitude: magnitude * 0.65,
          leftTrigger: effect.hand === 'right' ? 0 : magnitude,
          rightTrigger: effect.hand === 'left' ? 0 : magnitude,
        })
      } else if (actuator.pulse) {
        await actuator.pulse(magnitude, duration)
      } else {
        return false
      }
      return true
    } catch (error) {
      this.demotedMessage = `Gamepad haptics failed: ${errorMessage(error)}`
      return false
    }
  }

  async neutralize(): Promise<void> {
    const actuator = actuatorFor(this.gamepad)
    if (!actuator) return
    try {
      if (actuator.reset) {
        await actuator.reset()
        return
      }
      const type = effectType(actuator)
      if (type && actuator.playEffect) {
        await actuator.playEffect(type, {
          duration: 0,
          startDelay: 0,
          strongMagnitude: 0,
          weakMagnitude: 0,
          leftTrigger: 0,
          rightTrigger: 0,
        })
      } else if (actuator.pulse) {
        await actuator.pulse(0, 0)
      }
    } catch (error) {
      this.demotedMessage = `Gamepad haptics cleanup failed: ${errorMessage(error)}`
    }
  }

  async dispose(): Promise<void> {
    await this.neutralize()
    this.gamepad = null
  }
}

export class DualSenseFeedbackController {
  private readonly gamepadOutput: StandardsGamepadHapticsOutput
  private readonly enhancedOutput: LastChancesEnhancedFeedbackOutput | null
  private readonly now: () => number
  private preferences: LastChancesFeedbackPreferences
  private readonly queue: QueuedFeedbackEffect[] = []
  private drainPromise: Promise<void> | null = null
  private sequence = 0
  private lastBlockedAt = Number.NEGATIVE_INFINITY
  private disposed = false
  private controllerError: string | null = null
  private gamepad: LastChancesHapticGamepadLike | null
  private outputBlockCount = 0
  private neutralizePromise: Promise<void> | null = null
  private disposePromise: Promise<void> | null = null
  private gamepadTransition: Promise<void> = Promise.resolve()

  constructor(
    private readonly config: DualSenseFeedbackControllerConfig,
    preferences: LastChancesFeedbackPreferences,
    outputs: DualSenseFeedbackControllerOutputs = {},
  ) {
    this.preferences = {
      mode: preferences.mode,
      intensity: normalizedIntensity(preferences.intensity),
    }
    this.gamepad = outputs.gamepad ?? null
    this.gamepadOutput = new StandardsGamepadHapticsOutput(
      this.gamepad,
      Math.max(0, config.maxMagnitude),
      Math.max(0, config.maxDurationMs),
    )
    this.enhancedOutput = outputs.enhanced ?? null
    this.now = outputs.now ?? (() => performance.now())
  }

  snapshot(): LastChancesFeedbackSnapshot {
    const enhanced = this.enhancedOutput?.capability()
    const gamepad = this.gamepadOutput.capability()
    const selected = enhanced?.tier === 2 ? enhanced : gamepad.tier === 1 ? gamepad : enhanced ?? gamepad
    const status = this.controllerError ? 'error' : selected.status
    return {
      tier: selected.tier,
      status,
      mode: this.preferences.mode,
      intensity: this.preferences.intensity,
      reducedHaptics: this.preferences.mode === 'reduced',
      permission: enhanced?.permission ?? (this.enhancedOutput ? 'not-requested' : 'unavailable'),
      message: this.controllerError
        ?? (enhanced?.tier !== 2 && enhanced?.message ? enhanced.message : selected.message),
    }
  }

  async setPreferences(preferences: LastChancesFeedbackPreferences): Promise<void> {
    if (this.disposed) return
    this.preferences = {
      mode: preferences.mode,
      intensity: normalizedIntensity(preferences.intensity),
    }
    if (preferences.mode === 'off') {
      await this.neutralize()
    } else if (preferences.mode === 'reduced') {
      for (let index = this.queue.length - 1; index >= 0; index -= 1) {
        if (!this.effectAllowedInReducedMode(this.queue[index])) this.queue.splice(index, 1)
      }
    }
  }

  async setGamepad(gamepad: LastChancesHapticGamepadLike | null): Promise<void> {
    if (this.disposed || gamepad === this.gamepad) return
    this.gamepad = gamepad
    const releaseOutput = this.blockOutputs()
    this.queue.splice(0)
    const transition = this.gamepadTransition.then(async () => {
      await this.flush()
      await this.gamepadOutput.neutralize()
      if (!this.disposed && this.gamepad === gamepad) this.gamepadOutput.setGamepad(gamepad)
    })
    this.gamepadTransition = transition
    await transition.finally(releaseOutput)
  }

  /**
   * Keeps a newly-created controller silent until an older output writer has
   * finished its asynchronous cleanup. Gameplay can continue while the barrier
   * is pending; semantic effects are queued and drained only after it settles.
   */
  waitForOutputBarrier(barrier: Promise<unknown>): Promise<void> {
    if (this.disposed) return Promise.resolve()
    const releaseOutput = this.blockOutputs()
    return Promise.resolve(barrier)
      .catch((error) => {
        this.controllerError = `Feedback output barrier failed: ${errorMessage(error)}`
      })
      .finally(releaseOutput)
      .then(() => undefined)
  }

  emit(event: LastChancesSemanticFeedbackEvent): boolean {
    if (this.disposed || this.preferences.mode === 'off') return false
    const baseProfile = this.config.profiles[event.profile]
    if (!baseProfile) return false
    const adaptiveProfile = { ...baseProfile, ...event.adaptiveOverride }
    if (this.preferences.mode === 'reduced' && !this.effectAllowedInReducedMode(event)) return false

    const atMs = this.now()
    const isBlocked = event.state === 'blocked'
    if (isBlocked) {
      const repeatMs = Math.max(0, this.config.blockedRepeatMs)
      if (atMs - this.lastBlockedAt < repeatMs) return false
    }

    const strength = clamp(event.strength ?? 1, 0, 1)
    const effect: QueuedFeedbackEffect = {
      state: event.state,
      profile: event.profile,
      hand: event.hand ?? 'both',
      magnitude: clamp(
        adaptiveProfile.magnitude * strength * this.preferences.intensity,
        0,
        Math.max(0, this.config.maxMagnitude),
      ),
      durationMs: clamp(
        adaptiveProfile.effectMs,
        0,
        Math.max(0, this.config.maxDurationMs),
      ),
      priority: PROFILE_PRIORITIES[event.profile],
      adaptiveProfile: { ...adaptiveProfile },
      sequence: this.sequence++,
    }
    if (effect.magnitude <= 0 || effect.durationMs <= 0) return false
    if (!this.enqueue(effect)) return false
    if (isBlocked) this.lastBlockedAt = atMs
    this.scheduleDrain()
    return true
  }

  async enableEnhancedFeatures(): Promise<boolean> {
    if (this.disposed || !this.enhancedOutput) return false
    const enabled = await this.enhancedOutput.enableEnhancedFeatures()
    if (enabled) {
      this.controllerError = null
      await this.gamepadOutput.neutralize()
    }
    return enabled
  }

  async disableEnhancedFeatures(): Promise<void> {
    if (!this.enhancedOutput) return
    await this.enhancedOutput.disableEnhancedFeatures()
  }

  async flush(): Promise<void> {
    while (this.drainPromise) await this.drainPromise
  }

  neutralize(): Promise<void> {
    this.queue.splice(0)
    if (this.disposed) return this.disposePromise ?? Promise.resolve()
    if (this.neutralizePromise) return this.neutralizePromise

    const releaseOutput = this.blockOutputs()
    const operation = (async () => {
      await this.gamepadTransition
      await this.flush()
      await this.safeNeutralize(this.enhancedOutput)
      await this.safeNeutralize(this.gamepadOutput)
    })()
    this.neutralizePromise = operation.finally(() => {
      this.neutralizePromise = null
      releaseOutput()
    })
    return this.neutralizePromise
  }

  dispose(): Promise<void> {
    if (this.disposePromise) return this.disposePromise
    this.disposed = true
    this.queue.splice(0)
    const releaseOutput = this.blockOutputs()
    const pendingNeutralize = this.neutralizePromise
    this.disposePromise = (async () => {
      if (pendingNeutralize) {
        await pendingNeutralize
      }
      await this.gamepadTransition
      if (!pendingNeutralize) {
        await this.flush()
        await this.safeNeutralize(this.enhancedOutput)
      }
      try {
        await this.enhancedOutput?.dispose?.()
      } catch (error) {
        this.controllerError = `Feedback disposal failed: ${errorMessage(error)}`
      }
      try {
        await this.gamepadOutput.dispose()
      } catch (error) {
        this.controllerError = `Feedback disposal failed: ${errorMessage(error)}`
      }
    })().finally(releaseOutput)
    return this.disposePromise
  }

  private effectAllowedInReducedMode(
    effect: Pick<LastChancesFeedbackEffect, 'state' | 'profile'>,
  ): boolean {
    return effect.state === 'blocked' || effect.state === 'impact'
      || effect.profile === 'blocked' || effect.profile === 'impact'
  }

  private enqueue(effect: QueuedFeedbackEffect): boolean {
    const same = this.queue.find(candidate => (
      candidate.state === effect.state && candidate.profile === effect.profile
    ))
    if (same) {
      same.hand = same.hand === effect.hand ? same.hand : 'both'
      same.magnitude = Math.max(same.magnitude, effect.magnitude)
      same.durationMs = Math.max(same.durationMs, effect.durationMs)
      same.priority = Math.max(same.priority, effect.priority)
      return true
    }

    const strongestPending = this.queue.reduce(
      (strongest, candidate) => Math.max(strongest, candidate.priority),
      Number.NEGATIVE_INFINITY,
    )
    if (strongestPending > effect.priority) return false
    if (effect.priority > strongestPending) {
      for (let index = this.queue.length - 1; index >= 0; index -= 1) {
        if (this.queue[index].priority < effect.priority) this.queue.splice(index, 1)
      }
    }
    this.queue.push(effect)
    this.queue.sort((left, right) => right.priority - left.priority || left.sequence - right.sequence)
    if (this.queue.length > MAX_PENDING_EFFECTS) this.queue.splice(MAX_PENDING_EFFECTS)
    return true
  }

  private scheduleDrain(): void {
    if (this.drainPromise || this.outputBlockCount > 0 || this.disposed) return
    this.drainPromise = Promise.resolve()
      .then(() => this.drain())
      .catch((error) => {
        this.controllerError = `Feedback output failed: ${errorMessage(error)}`
        this.queue.splice(0)
      })
      .finally(() => {
        this.drainPromise = null
        if (this.queue.length > 0 && !this.disposed) this.scheduleDrain()
      })
  }

  private async drain(): Promise<void> {
    while (this.queue.length > 0
      && this.outputBlockCount === 0
      && !this.disposed
      && this.preferences.mode !== 'off') {
      const effect = this.queue.shift()
      if (!effect) return
      const enhanced = this.enhancedOutput?.capability().tier === 2
        ? this.enhancedOutput
        : null
      if (enhanced && await enhanced.play(effect)) continue
      if (await this.gamepadOutput.play(effect)) continue
      return
    }
  }

  private async safeNeutralize(output: LastChancesFeedbackOutput | null): Promise<void> {
    if (!output) return
    try {
      await output.neutralize()
    } catch (error) {
      this.controllerError = `Feedback cleanup failed: ${errorMessage(error)}`
    }
  }

  private blockOutputs(): () => void {
    this.outputBlockCount += 1
    let released = false
    return () => {
      if (released) return
      released = true
      this.outputBlockCount = Math.max(0, this.outputBlockCount - 1)
      if (this.outputBlockCount === 0 && this.queue.length > 0 && !this.disposed) {
        this.scheduleDrain()
      }
    }
  }
}
