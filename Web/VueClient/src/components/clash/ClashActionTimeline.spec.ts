import { cleanup, render } from '@testing-library/vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ClashResolutionEvent } from 'src/features/clash/types'
import ClashActionTimeline from './ClashActionTimeline.vue'

function timelineEvent(
  sequence: number,
  startOffsetMs: number,
  impactOffsetMs: number,
): ClashResolutionEvent {
  return {
    sequence,
    type: 'Damage',
    actorUnitInstanceId: 'attacker',
    targetUnitInstanceId: 'target',
    speed: 5,
    startOffsetMs,
    impactOffsetMs,
    amount: 1,
    fromBoardRow: null,
    toBoardRow: null,
    column: 0,
    message: `event-${sequence}`,
  }
}

afterEach(() => {
  cleanup()
  vi.useRealTimers()
})

describe('ClashActionTimeline server-clock playback', () => {
  it('applies elapsed impacts immediately and schedules only the remainder', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-07-29T12:00:01.000Z'))
    const onStart = vi.fn()
    const onImpact = vi.fn()
    const onComplete = vi.fn()
    const elapsed = timelineEvent(1, 100, 500)
    const remaining = timelineEvent(2, 2000, 2500)

    render(ClashActionTimeline, {
      props: {
        identity: 'clock:1',
        events: [elapsed, remaining],
        durationMs: 3000,
        startedAtUtc: '2026-07-29T12:00:00.000Z',
        onStart,
        onImpact,
        onComplete,
      },
    })

    expect(onStart).toHaveBeenCalledWith(elapsed)
    expect(onImpact).toHaveBeenCalledWith(elapsed)
    expect(onStart).not.toHaveBeenCalledWith(remaining)
    expect(onImpact).not.toHaveBeenCalledWith(remaining)

    vi.advanceTimersByTime(1000)
    expect(onStart).toHaveBeenCalledWith(remaining)
    expect(onImpact).not.toHaveBeenCalledWith(remaining)

    vi.advanceTimersByTime(500)
    expect(onImpact).toHaveBeenCalledWith(remaining)
    expect(onComplete).not.toHaveBeenCalled()

    vi.advanceTimersByTime(500)
    expect(onComplete).toHaveBeenCalledTimes(1)
  })
})
