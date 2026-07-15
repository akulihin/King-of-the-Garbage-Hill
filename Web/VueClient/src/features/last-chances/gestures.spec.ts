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

  it('recognizes a hold followed by one timely repeat tap', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 700)
    recognizer.press('left', 810)
    recognizer.release('left', 850)

    expect(events).toEqual([{ hand: 'left', gesture: 'holdThenDoubleTap' }])
  })

  it('falls back to hold when the post-hold double tap is incomplete', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('right', 0)
    recognizer.release('right', 700)
    recognizer.update(1180)

    expect(events).toEqual([{ hand: 'right', gesture: 'hold' }])
  })

  it('exposes press and timing windows for immediate input feedback', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    expect(recognizer.snapshot('left', 325)).toMatchObject({
      phase: 'pressing',
      pressed: true,
      progress: 0.5,
      remainingMs: 325,
    })
    recognizer.release('left', 700)
    expect(recognizer.snapshot('left', 820)).toMatchObject({
      phase: 'holdFollowUpWindow',
      pressed: false,
      progress: 0.25,
      remainingMs: 360,
    })
    recognizer.press('left', 900)
    expect(recognizer.snapshot('left', 920)).toMatchObject({
      phase: 'holdFollowUp',
      pressed: true,
      progress: 1,
    })
  })

  it('keeps the hold follow-up boundary inclusive and falls back just outside it', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 700)
    recognizer.press('left', 1180)
    recognizer.release('left', 1200)
    expect(events).toEqual([{ hand: 'left', gesture: 'holdThenDoubleTap' }])

    recognizer.press('right', 0)
    recognizer.release('right', 700)
    recognizer.press('right', 1181)
    recognizer.release('right', 1200)
    recognizer.update(1460)
    expect(events).toEqual([
      { hand: 'left', gesture: 'holdThenDoubleTap' },
      { hand: 'right', gesture: 'hold' },
      { hand: 'right', gesture: 'tap' },
    ])
  })

  it('does not open the combo window after a hold longer than one second', () => {
    const events: GestureEvent[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 1100)

    expect(events).toEqual([{ hand: 'left', gesture: 'hold' }])
  })
})
