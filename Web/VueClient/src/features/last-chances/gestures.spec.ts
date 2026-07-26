import { describe, expect, it } from 'vitest'
import { LastChancesGestureRecognizer } from './gestures'
import type { LastChancesGestureResolution } from './types'

function makeRecognizer(events: LastChancesGestureResolution[]): LastChancesGestureRecognizer {
  return new LastChancesGestureRecognizer(
    { doubleTapMs: 260, holdMs: 650, holdMaxMs: 1000, holdThenDoubleTapWindowMs: 480 },
    resolution => events.push(resolution),
  )
}

describe('99LC five-gesture recognizer', () => {
  it('emits a tap only after its double-tap window closes', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 80)
    recognizer.update(339)
    expect(events).toEqual([])
    recognizer.update(340)

    expect(events).toEqual([{
      hand: 'left',
      gesture: 'tap',
      atMs: 340,
      heldMs: 80,
      firstHoldMs: 80,
    }])
  })

  it('distinguishes double tap from double tap and hold', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 60)
    recognizer.press('left', 180)
    recognizer.release('left', 230)
    recognizer.press('right', 400)
    recognizer.release('right', 460)
    recognizer.press('right', 560)
    recognizer.update(1210)
    expect(events).toEqual([{
      hand: 'left',
      gesture: 'doubleTap',
      atMs: 230,
      heldMs: 50,
      firstHoldMs: 60,
    }])
    recognizer.release('right', 1250)

    expect(events).toEqual([
      { hand: 'left', gesture: 'doubleTap', atMs: 230, heldMs: 50, firstHoldMs: 60 },
      { hand: 'right', gesture: 'doubleTapHold', atMs: 1250, heldMs: 690, firstHoldMs: 60 },
    ])
  })

  it('recognizes a hold followed by one timely repeat tap', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 700)
    recognizer.press('left', 810)
    recognizer.release('left', 850)

    expect(events).toEqual([{
      hand: 'left',
      gesture: 'holdThenDoubleTap',
      atMs: 850,
      heldMs: 40,
      firstHoldMs: 700,
    }])
  })

  it('falls back to hold when the post-hold double tap is incomplete', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('right', 0)
    recognizer.release('right', 700)
    recognizer.update(1180)

    expect(events).toEqual([{
      hand: 'right',
      gesture: 'hold',
      atMs: 1180,
      heldMs: 700,
      firstHoldMs: 700,
    }])
  })

  it('exposes press and timing windows for immediate input feedback', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    expect(recognizer.snapshot('left', 325)).toMatchObject({
      phase: 'pressing',
      pressed: true,
      progress: 0.5,
      remainingMs: 325,
      heldMs: 325,
      sequence: 'first',
      candidateGesture: 'tap',
      pendingChargeMs: 0,
    })
    recognizer.release('left', 700)
    expect(recognizer.snapshot('left', 820)).toMatchObject({
      phase: 'holdFollowUpWindow',
      pressed: false,
      progress: 0.25,
      remainingMs: 360,
      heldMs: 0,
      sequence: null,
      candidateGesture: 'hold',
      pendingChargeMs: 700,
    })
    recognizer.press('left', 900)
    expect(recognizer.snapshot('left', 920)).toMatchObject({
      phase: 'holdFollowUp',
      pressed: true,
      heldMs: 20,
      sequence: 'afterHoldTap',
      candidateGesture: 'holdThenDoubleTap',
      pendingChargeMs: 700,
    })
  })

  it('cancels one hand without emitting or disturbing the other hand', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.press('right', 20)
    recognizer.cancel('left')
    recognizer.update(900)

    expect(recognizer.snapshot('left', 900).phase).toBe('idle')
    expect(recognizer.snapshot('right', 900)).toMatchObject({
      phase: 'pressing',
      pressed: true,
      heldMs: 880,
    })
    expect(events).toEqual([])
  })

  it('keeps the hold follow-up boundary inclusive and falls back just outside it', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 700)
    recognizer.press('left', 1180)
    recognizer.release('left', 1200)
    expect(events).toEqual([{
      hand: 'left',
      gesture: 'holdThenDoubleTap',
      atMs: 1200,
      heldMs: 20,
      firstHoldMs: 700,
    }])

    recognizer.press('right', 0)
    recognizer.release('right', 700)
    recognizer.press('right', 1181)
    recognizer.release('right', 1200)
    recognizer.update(1460)
    expect(events).toEqual([
      { hand: 'left', gesture: 'holdThenDoubleTap', atMs: 1200, heldMs: 20, firstHoldMs: 700 },
      { hand: 'right', gesture: 'hold', atMs: 1180, heldMs: 700, firstHoldMs: 700 },
      { hand: 'right', gesture: 'tap', atMs: 1460, heldMs: 19, firstHoldMs: 19 },
    ])
  })

  it('does not open the combo window after a hold longer than one second', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 1100)

    expect(events).toEqual([{
      hand: 'left',
      gesture: 'hold',
      atMs: 1100,
      heldMs: 1100,
      firstHoldMs: 1100,
    }])
  })

  it('keeps the hold threshold inclusive on second-press release', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = makeRecognizer(events)

    recognizer.press('left', 0)
    recognizer.release('left', 40)
    recognizer.press('left', 200)
    recognizer.release('left', 850)

    expect(events).toEqual([{
      hand: 'left',
      gesture: 'doubleTapHold',
      atMs: 850,
      heldMs: 650,
      firstHoldMs: 40,
    }])
  })

  it('keeps late spear and axe charge bands reachable through the real follow-up gesture', () => {
    const events: LastChancesGestureResolution[] = []
    const recognizer = new LastChancesGestureRecognizer(
      { doubleTapMs: 260, holdMs: 650, holdMaxMs: 2300, holdThenDoubleTapWindowMs: 480 },
      resolution => events.push(resolution),
    )

    recognizer.press('left', 0)
    recognizer.release('left', 1700)
    expect(recognizer.snapshot('left', 1800)).toMatchObject({
      phase: 'holdFollowUpWindow',
      pendingChargeMs: 1700,
    })
    recognizer.press('left', 1820)
    recognizer.release('left', 1870)

    expect(events).toEqual([{
      hand: 'left',
      gesture: 'holdThenDoubleTap',
      atMs: 1870,
      heldMs: 50,
      firstHoldMs: 1700,
    }])
  })
})
