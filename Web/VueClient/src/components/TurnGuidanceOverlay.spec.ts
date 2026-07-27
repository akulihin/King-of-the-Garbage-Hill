import { cleanup, render } from '@testing-library/vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import TurnGuidanceOverlay from './TurnGuidanceOverlay.vue'

afterEach(() => {
  cleanup()
  vi.useRealTimers()
})

describe('TurnGuidanceOverlay', () => {
  it('renders a persistent enemy vignette and a five-second message', async () => {
    vi.useFakeTimers()
    render(TurnGuidanceOverlay, {
      props: {
        interference: 'enemy',
        dimmed: true,
        enemyEventKey: '42:3',
      },
    })

    expect(document.body.querySelector('.turn-interference-vignette--enemy')).not.toBeNull()
    expect(document.body.querySelector('.turn-guidance-dim')).toBeNull()
    expect(document.body.textContent).toContain('Вражеское воздействие...')

    vi.advanceTimersByTime(5000)
    await nextTick()

    expect(document.body.textContent).not.toContain('Вражеское воздействие...')
    expect(document.body.querySelector('.turn-guidance-dim')).not.toBeNull()
    expect(document.body.querySelector('.turn-interference-vignette--enemy')).not.toBeNull()
  })

  it('uses a white self vignette without the enemy message', () => {
    render(TurnGuidanceOverlay, {
      props: {
        interference: 'self',
        dimmed: false,
        enemyEventKey: null,
      },
    })

    expect(document.body.querySelector('.turn-interference-vignette--self')).not.toBeNull()
    expect(document.body.textContent).not.toContain('Вражеское воздействие...')
  })
})
