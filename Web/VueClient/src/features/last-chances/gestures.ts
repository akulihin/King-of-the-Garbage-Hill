import type { LastChancesGesture, LastChancesHand } from './types'

type PressSequence = 'first' | 'secondTap' | 'afterHoldFirstTap' | 'afterHoldSecondTap'
type PendingGesture = 'tap' | 'hold' | 'holdFirstTap'

interface HandGestureState {
  down: boolean
  pressedAt: number
  sequence: PressSequence
  consumed: boolean
  pending: PendingGesture | null
  pendingUntil: number
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
    consumed: false,
    pending: null,
    pendingUntil: 0,
  }
}

export class LastChancesGestureRecognizer {
  private readonly states: Record<LastChancesHand, HandGestureState> = {
    left: makeState(),
    right: makeState(),
  }

  constructor(
    private readonly timings: LastChancesGestureTimings,
    private readonly emit: (hand: LastChancesHand, gesture: LastChancesGesture, atMs: number) => void,
  ) {}

  press(hand: LastChancesHand, atMs: number): void {
    this.update(atMs)
    const state = this.states[hand]
    if (state.down) return
    state.down = true
    state.pressedAt = atMs
    state.consumed = false
    if (state.pending === 'tap' && atMs <= state.pendingUntil) {
      state.sequence = 'secondTap'
      state.pending = null
    } else if (state.pending === 'hold' && atMs <= state.pendingUntil) {
      state.sequence = 'afterHoldFirstTap'
      state.pending = null
    } else if (state.pending === 'holdFirstTap' && atMs <= state.pendingUntil) {
      state.sequence = 'afterHoldSecondTap'
      state.pending = null
    } else {
      state.sequence = 'first'
      state.pending = null
    }
  }

  release(hand: LastChancesHand, atMs: number): void {
    const state = this.states[hand]
    if (!state.down) return
    state.down = false
    const heldFor = Math.max(0, atMs - state.pressedAt)
    if (state.consumed) return

    if (state.sequence === 'secondTap') {
      this.emit(hand, heldFor >= this.timings.holdMs ? 'doubleTapHold' : 'doubleTap', atMs)
      return
    }
    if (state.sequence === 'afterHoldFirstTap') {
      if (heldFor >= this.timings.holdMs) {
        this.emit(hand, 'hold', atMs)
      } else {
        state.pending = 'holdFirstTap'
        state.pendingUntil = atMs + this.timings.doubleTapMs
      }
      return
    }
    if (state.sequence === 'afterHoldSecondTap') {
      this.emit(hand, heldFor < this.timings.holdMs ? 'holdThenDoubleTap' : 'hold', atMs)
      return
    }
    if (heldFor > this.timings.holdMaxMs) {
      this.emit(hand, 'hold', atMs)
    } else if (heldFor >= this.timings.holdMs) {
      state.pending = 'hold'
      state.pendingUntil = atMs + this.timings.holdThenDoubleTapWindowMs
    } else {
      state.pending = 'tap'
      state.pendingUntil = atMs + this.timings.doubleTapMs
    }
  }

  update(atMs: number): void {
    for (const hand of ['left', 'right'] as const) {
      const state = this.states[hand]
      if (state.down && state.sequence === 'secondTap' && !state.consumed
        && atMs - state.pressedAt >= this.timings.holdMs) {
        state.consumed = true
        this.emit(hand, 'doubleTapHold', atMs)
      }
      if (!state.down && state.pending && atMs >= state.pendingUntil) {
        const gesture = state.pending === 'tap' ? 'tap' : 'hold'
        state.pending = null
        this.emit(hand, gesture, state.pendingUntil)
      }
    }
  }

  reset(): void {
    this.states.left = makeState()
    this.states.right = makeState()
  }

  isPressed(hand: LastChancesHand): boolean {
    return this.states[hand].down
  }
}
