import type {
  LastChancesGesture,
  LastChancesGestureInputSnapshot,
  LastChancesGestureResolution,
  LastChancesGestureSequence,
  LastChancesHand,
} from './types'

type PendingGesture = 'tap' | 'hold'

interface HandGestureState {
  down: boolean
  pressedAt: number
  sequence: LastChancesGestureSequence
  firstHoldMs: number
  pending: PendingGesture | null
  pendingUntil: number
  pendingHeldMs: number
}

export interface LastChancesGestureTimings {
  doubleTapMs: number
  holdMs: number
  holdMaxMs: number
  holdThenDoubleTapWindowMs: number
}

function makeState(): HandGestureState {
  return {
    down: false,
    pressedAt: 0,
    sequence: 'first',
    firstHoldMs: 0,
    pending: null,
    pendingUntil: 0,
    pendingHeldMs: 0,
  }
}

export class LastChancesGestureRecognizer {
  private readonly states: Record<LastChancesHand, HandGestureState> = {
    left: makeState(),
    right: makeState(),
  }

  constructor(
    private timings: LastChancesGestureTimings,
    private readonly emit: (resolution: LastChancesGestureResolution) => void,
    private readonly holdMsForHand?: (hand: LastChancesHand) => number,
  ) {}

  updateTimings(timings: LastChancesGestureTimings): void {
    this.timings = timings
  }

  press(hand: LastChancesHand, atMs: number): void {
    const state = this.states[hand]
    if (state.down) return
    if (state.pending && atMs > state.pendingUntil) this.flushPending(hand, state)

    const pending = state.pending
    const pendingHeldMs = state.pendingHeldMs
    state.down = true
    state.pressedAt = atMs
    if (pending === 'tap' && atMs <= state.pendingUntil) {
      state.sequence = 'secondTap'
      state.firstHoldMs = pendingHeldMs
    } else if (pending === 'hold' && atMs <= state.pendingUntil) {
      state.sequence = 'afterHoldTap'
      state.firstHoldMs = pendingHeldMs
    } else {
      state.sequence = 'first'
      state.firstHoldMs = 0
    }
    state.pending = null
    state.pendingUntil = 0
    state.pendingHeldMs = 0
  }

  release(hand: LastChancesHand, atMs: number): void {
    const state = this.states[hand]
    if (!state.down) return
    state.down = false
    const heldMs = Math.max(0, atMs - state.pressedAt)
    const holdMs = this.holdThreshold(hand)

    if (state.sequence === 'secondTap') {
      this.emitResolution(
        hand,
        heldMs >= holdMs ? 'doubleTapHold' : 'doubleTap',
        atMs,
        heldMs,
        state.firstHoldMs,
      )
      return
    }
    if (state.sequence === 'afterHoldTap') {
      this.emitResolution(
        hand,
        heldMs < holdMs ? 'holdThenDoubleTap' : 'hold',
        atMs,
        heldMs,
        state.firstHoldMs,
      )
      return
    }
    if (heldMs > this.timings.holdMaxMs) {
      this.emitResolution(hand, 'hold', atMs, heldMs, heldMs)
    } else if (heldMs >= holdMs) {
      state.pending = 'hold'
      state.pendingUntil = atMs + this.timings.holdThenDoubleTapWindowMs
      state.pendingHeldMs = heldMs
    } else {
      state.pending = 'tap'
      state.pendingUntil = atMs + this.timings.doubleTapMs
      state.pendingHeldMs = heldMs
    }
  }

  update(atMs: number): void {
    for (const hand of ['left', 'right'] as const) {
      const state = this.states[hand]
      if (!state.down && state.pending && atMs >= state.pendingUntil) {
        this.flushPending(hand, state)
      }
    }
  }

  reset(): void {
    this.states.left = makeState()
    this.states.right = makeState()
  }

  /** Abandons one physical input stream without resolving its pending tap/hold. */
  cancel(hand: LastChancesHand): void {
    this.states[hand] = makeState()
  }

  isPressed(hand: LastChancesHand): boolean {
    return this.states[hand].down
  }

  snapshot(hand: LastChancesHand, atMs: number): LastChancesGestureInputSnapshot {
    const state = this.states[hand]
    if (state.down) {
      const heldMs = Math.max(0, atMs - state.pressedAt)
      const holdMs = this.holdThreshold(hand)
      if (state.sequence === 'secondTap') {
        return {
          hand,
          phase: 'secondPress',
          pressed: true,
          progress: Math.min(1, heldMs / holdMs),
          remainingMs: Math.max(0, holdMs - heldMs),
          heldMs,
          sequence: state.sequence,
          candidateGesture: heldMs >= holdMs ? 'doubleTapHold' : 'doubleTap',
          pendingChargeMs: state.firstHoldMs,
        }
      }
      if (state.sequence === 'afterHoldTap') {
        return {
          hand,
          phase: 'holdFollowUp',
          pressed: true,
          progress: Math.min(1, heldMs / holdMs),
          remainingMs: Math.max(0, holdMs - heldMs),
          heldMs,
          sequence: state.sequence,
          candidateGesture: heldMs < holdMs ? 'holdThenDoubleTap' : 'hold',
          pendingChargeMs: state.firstHoldMs,
        }
      }
      return {
        hand,
        phase: 'pressing',
        pressed: true,
        progress: Math.min(1, heldMs / holdMs),
        remainingMs: Math.max(0, holdMs - heldMs),
        heldMs,
        sequence: state.sequence,
        candidateGesture: heldMs >= holdMs ? 'hold' : 'tap',
        pendingChargeMs: 0,
      }
    }
    if (state.pending === 'tap') {
      const remainingMs = Math.max(0, state.pendingUntil - atMs)
      return {
        hand,
        phase: 'doubleTapWindow',
        pressed: false,
        progress: 1 - Math.min(1, remainingMs / this.timings.doubleTapMs),
        remainingMs,
        heldMs: 0,
        sequence: null,
        candidateGesture: 'tap',
        pendingChargeMs: state.pendingHeldMs,
      }
    }
    if (state.pending === 'hold') {
      const remainingMs = Math.max(0, state.pendingUntil - atMs)
      return {
        hand,
        phase: 'holdFollowUpWindow',
        pressed: false,
        progress: 1 - Math.min(1, remainingMs / this.timings.holdThenDoubleTapWindowMs),
        remainingMs,
        heldMs: 0,
        sequence: null,
        candidateGesture: 'hold',
        pendingChargeMs: state.pendingHeldMs,
      }
    }
    return {
      hand,
      phase: 'idle',
      pressed: false,
      progress: 0,
      remainingMs: 0,
      heldMs: 0,
      sequence: null,
      candidateGesture: null,
      pendingChargeMs: 0,
    }
  }

  private holdThreshold(hand: LastChancesHand): number {
    const resolved = this.holdMsForHand?.(hand)
    return typeof resolved === 'number' && Number.isFinite(resolved) && resolved > 0
      ? resolved
      : this.timings.holdMs
  }

  private flushPending(hand: LastChancesHand, state: HandGestureState): void {
    if (!state.pending) return
    const gesture = state.pending === 'tap' ? 'tap' : 'hold'
    this.emitResolution(
      hand,
      gesture,
      state.pendingUntil,
      state.pendingHeldMs,
      state.pendingHeldMs,
    )
    state.pending = null
    state.pendingUntil = 0
    state.pendingHeldMs = 0
  }

  private emitResolution(
    hand: LastChancesHand,
    gesture: LastChancesGesture,
    atMs: number,
    heldMs: number,
    firstHoldMs: number,
  ): void {
    this.emit({ hand, gesture, atMs, heldMs, firstHoldMs })
  }
}
