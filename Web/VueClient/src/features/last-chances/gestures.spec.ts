import { describe, expect, it } from 'vitest'
import { LastChancesGestureRecognizer } from './gestures'
import type { LastChancesGesture, LastChancesHand } from './types'

interface GestureEvent {
  hand: LastChancesHand
  gesture: LastChancesGesture
}

function makeRecognizer(events: GestureEvent[]): LastChancesGestureRecognizer {
  return new LastChancesGestureRecognizer(
    { doubleTapMs: 260, holdMs: 650, holdMaxMs: 1000, holdThenDoubleTapWindowMs: 480 },
    (hand, gesture) => events.push({ hand, gesture }),
  )
}

describe('99LC five-gesture recognizer', () => {
  it('emits a tap only after its double-tap window closes', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 80)
    recognizer.update(339)
    expect(events).toEqual([])
    recognizer.update(340)

    expect(events).toEqual([{ hand: 'left', gesture: 'tap' }])
  })

  it('distinguishes double tap from double tap and hold', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 60)
    recognizer.press('left', 180)
    recognizer.release('left', 230)
    recognizer.press('right', 400)
    recognizer.release('right', 460)
    recognizer.press('right', 560)
    recognizer.update(1210)
    recognizer.release('right', 1250)

    expect(events).toEqual([
      { hand: 'left', gesture: 'doubleTap' },
      { hand: 'right', gesture: 'doubleTapHold' },
    ])
  })

  it('requires two short presses after a hold for holdThenDoubleTap', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 700)
    recognizer.press('left', 810)
    recognizer.release('left', 850)
    expect(events).toEqual([])
    recognizer.press('left', 970)
    recognizer.release('left', 1010)

    expect(events).toEqual([{ hand: 'left', gesture: 'holdThenDoubleTap' }])
  })

  it('falls back to hold when the post-hold double tap is incomplete', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('right', 0)
    recognizer.release('right', 700)
    recognizer.press('right', 820)
    recognizer.release('right', 860)
    recognizer.update(1120)

    expect(events).toEqual([{ hand: 'right', gesture: 'hold' }])
  })

  it('does not open the combo window after a hold longer than one second', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 1100)

    expect(events).toEqual([{ hand: 'left', gesture: 'hold' }])
  })
})
