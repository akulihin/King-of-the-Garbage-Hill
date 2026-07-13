// @vitest-environment happy-dom

import { beforeEach, describe, expect, it, vi } from 'vitest'

interface FakeBuffer {
  id: string
}

const starts: Array<{ id: string; when: number; loop: boolean }> = []
const stops: string[] = []

class FakeAudioContext {
  state = 'running'
  currentTime = 7
  destination = {}

  createGain() {
    return { gain: { value: 1 }, connect: vi.fn() }
  }

  createBufferSource() {
    const source: {
      buffer: FakeBuffer | null
      loop: boolean
      connect: ReturnType<typeof vi.fn>
      start: (when: number) => void
      stop: () => void
    } = {
      buffer: null,
      loop: false,
      connect: vi.fn(),
      start: (when: number) => starts.push({ id: source.buffer?.id ?? '', when, loop: source.loop }),
      stop: () => stops.push(source.buffer?.id ?? ''),
    }
    return source
  }

  async decodeAudioData(data: ArrayBuffer): Promise<FakeBuffer> {
    return data as unknown as FakeBuffer
  }

  async resume(): Promise<void> {}
}

function audioResponse(id: string, delayMs = 0) {
  return {
    ok: true,
    async arrayBuffer() {
      if (delayMs > 0) await new Promise(resolve => setTimeout(resolve, delayMs))
      return { id } as unknown as ArrayBuffer
    },
  }
}

describe('batched sound layers', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.restoreAllMocks()
    starts.length = 0
    stops.length = 0
    localStorage.clear()
    vi.stubGlobal('AudioContext', FakeAudioContext)
  })

  it('lets a cold randomized layer join a cached primary inside the shared deadline', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string | URL) => {
      const url = String(input)
      if (url === '/sound-config.json') return { ok: false }
      if (url.endsWith('/layer.mp3')) return audioResponse('layer', 20)
      return audioResponse('primary')
    }))

    const { playClipsBatched } = await import('./sound')
    const primary = { path: 'dooms_day/win_lose/1_w.mp3', group: 'doomsDayWinLose' as const }
    await playClipsBatched([primary])
    starts.length = 0

    await playClipsBatched([
      primary,
      { path: 'dooms_day/win_lose/layers/layer.mp3', group: 'doomsDayLayers' },
    ])

    expect(starts.map(start => start.id)).toEqual(['primary', 'layer'])
    expect(new Set(starts.map(start => start.when)).size).toBe(1)
  })

  it('does not extend primary playback past the deadline for a slow layer', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string | URL) => {
      const url = String(input)
      if (url === '/sound-config.json') return { ok: false }
      if (url.endsWith('/slow-layer.mp3')) return audioResponse('slow-layer', 250)
      return audioResponse('primary')
    }))

    const { playClipsBatched } = await import('./sound')
    const primary = { path: 'dooms_day/win_lose/1_w.mp3', group: 'doomsDayWinLose' as const }
    await playClipsBatched([primary])
    starts.length = 0

    const startedAt = performance.now()
    await playClipsBatched([
      primary,
      { path: 'dooms_day/win_lose/layers/slow-layer.mp3', group: 'doomsDayLayers' },
    ])

    expect(performance.now() - startedAt).toBeLessThan(230)
    expect(starts.map(start => start.id)).toEqual(['primary'])
  })

  it('loops Eren\'s global Rumbling theme until it is stopped', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: string | URL) => {
      const url = String(input)
      if (url === '/sound-config.json') return { ok: false }
      if (url.endsWith('/eren_final_attack_theme.mp3')) return audioResponse('rumbling-theme')
      return audioResponse('rumbling-cue')
    }))

    const { playErenRumblingWarning, stopErenRumblingWarning } = await import('./sound')
    playErenRumblingWarning()

    await vi.waitFor(() => expect(starts).toHaveLength(2))
    expect(starts.find(start => start.id === 'rumbling-theme')?.loop).toBe(true)
    expect(starts.find(start => start.id === 'rumbling-cue')?.loop).toBe(false)

    stopErenRumblingWarning()
    expect(stops).toContain('rumbling-theme')
  })
})
