import { describe, expect, it } from 'vitest'
import {
  DEFAULT_LAST_CHANCES_CONTROL_SCHEME,
  DEFAULT_LAST_CHANCES_FEEDBACK_INTENSITY,
  LAST_CHANCES_CONTROL_SCHEME_STORAGE_KEY,
  LAST_CHANCES_FEEDBACK_INTENSITY_STORAGE_KEY,
  LAST_CHANCES_FEEDBACK_MODE_LABELS,
  LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY,
  isLastChancesControlScheme,
  loadLastChancesControlScheme,
  loadLastChancesFeedbackPreferences,
  saveLastChancesControlScheme,
  saveLastChancesFeedbackIntensity,
  saveLastChancesFeedbackMode,
  saveLastChancesFeedbackPreferences,
  suggestLastChancesFeedbackMode,
} from './preferences'
import {
  LAST_CHANCES_CONTROL_SCHEMES,
  LAST_CHANCES_FEEDBACK_MODES,
} from './types'

class MemoryStorage implements Storage {
  private readonly values = new Map<string, string>()

  get length(): number {
    return this.values.size
  }

  clear(): void {
    this.values.clear()
  }

  getItem(key: string): string | null {
    return this.values.get(key) ?? null
  }

  key(index: number): string | null {
    return [...this.values.keys()][index] ?? null
  }

  removeItem(key: string): void {
    this.values.delete(key)
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value)
  }
}

class ThrowingStorage extends MemoryStorage {
  override getItem(): string | null {
    throw new Error('storage read denied')
  }

  override setItem(): void {
    throw new Error('storage write denied')
  }
}

describe('99LC local preferences', () => {
  it('defines only the three stable control scheme IDs and exact feedback labels', () => {
    expect(LAST_CHANCES_CONTROL_SCHEMES).toEqual(['legacy', 'mylorik', 'dualsense'])
    expect(LAST_CHANCES_FEEDBACK_MODES).toEqual(['off', 'reduced', 'full'])
    expect(LAST_CHANCES_FEEDBACK_MODE_LABELS).toEqual({
      off: 'Off',
      reduced: 'Reduced',
      full: 'Full',
    })
    expect(isLastChancesControlScheme('legacy')).toBe(true)
    expect(isLastChancesControlScheme('DeepList')).toBe(false)
  })

  it('falls back to legacy for absent, unknown, or unavailable storage', () => {
    const storage = new MemoryStorage()
    expect(loadLastChancesControlScheme(storage)).toBe(DEFAULT_LAST_CHANCES_CONTROL_SCHEME)

    storage.setItem(LAST_CHANCES_CONTROL_SCHEME_STORAGE_KEY, 'unknown-scheme')
    expect(loadLastChancesControlScheme(storage)).toBe('legacy')
    expect(loadLastChancesControlScheme(null)).toBe('legacy')
    expect(loadLastChancesControlScheme(new ThrowingStorage())).toBe('legacy')
  })

  it.each(LAST_CHANCES_CONTROL_SCHEMES)('round-trips the known %s scheme', (scheme) => {
    const storage = new MemoryStorage()
    expect(saveLastChancesControlScheme(scheme, storage)).toBe(true)
    expect(storage.getItem('99lc:control-scheme')).toBe(scheme)
    expect(loadLastChancesControlScheme(storage)).toBe(scheme)
  })

  it('rejects unknown schemes and handles denied writes without throwing', () => {
    const storage = new MemoryStorage()
    expect(saveLastChancesControlScheme(
      'future-scheme' as typeof LAST_CHANCES_CONTROL_SCHEMES[number],
      storage,
    )).toBe(false)
    expect(storage.length).toBe(0)
    expect(saveLastChancesControlScheme('legacy', null)).toBe(false)
    expect(saveLastChancesControlScheme('legacy', new ThrowingStorage())).toBe(false)
  })

  it('uses reduced motion only as an initial feedback suggestion', () => {
    const storage = new MemoryStorage()
    expect(suggestLastChancesFeedbackMode(false)).toBe('full')
    expect(suggestLastChancesFeedbackMode(true)).toBe('reduced')
    expect(loadLastChancesFeedbackPreferences(storage, true)).toEqual({
      mode: 'reduced',
      intensity: DEFAULT_LAST_CHANCES_FEEDBACK_INTENSITY,
    })

    storage.setItem(LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY, 'full')
    expect(loadLastChancesFeedbackPreferences(storage, true).mode).toBe('full')
  })

  it('falls back safely for invalid feedback values and unavailable storage', () => {
    const storage = new MemoryStorage()
    storage.setItem(LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY, 'maximum')
    storage.setItem(LAST_CHANCES_FEEDBACK_INTENSITY_STORAGE_KEY, '1.5')
    expect(loadLastChancesFeedbackPreferences(storage)).toEqual({ mode: 'full', intensity: 1 })
    expect(loadLastChancesFeedbackPreferences(storage, true)).toEqual({ mode: 'reduced', intensity: 1 })
    expect(loadLastChancesFeedbackPreferences(new ThrowingStorage(), true)).toEqual({
      mode: 'reduced',
      intensity: 1,
    })
  })

  it('persists feedback mode and normalized intensity under separate keys', () => {
    const storage = new MemoryStorage()
    expect(saveLastChancesFeedbackMode('off', storage)).toBe(true)
    expect(saveLastChancesFeedbackIntensity(1.4, storage)).toBe(true)
    expect(storage.getItem(LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY)).toBe('off')
    expect(storage.getItem(LAST_CHANCES_FEEDBACK_INTENSITY_STORAGE_KEY)).toBe('1')

    expect(saveLastChancesFeedbackPreferences({ mode: 'reduced', intensity: -0.2 }, storage))
      .toBe(true)
    expect(loadLastChancesFeedbackPreferences(storage)).toEqual({ mode: 'reduced', intensity: 0 })
  })

  it('rejects malformed feedback writes and absorbs storage failures', () => {
    const storage = new MemoryStorage()
    expect(saveLastChancesFeedbackMode(
      'maximum' as typeof LAST_CHANCES_FEEDBACK_MODES[number],
      storage,
    )).toBe(false)
    expect(saveLastChancesFeedbackIntensity(Number.NaN, storage)).toBe(false)
    expect(saveLastChancesFeedbackPreferences({ mode: 'full', intensity: Number.POSITIVE_INFINITY }, storage))
      .toBe(false)
    expect(storage.length).toBe(0)

    const unavailable = new ThrowingStorage()
    expect(saveLastChancesFeedbackMode('full', unavailable)).toBe(false)
    expect(saveLastChancesFeedbackIntensity(0.5, unavailable)).toBe(false)
    expect(saveLastChancesFeedbackPreferences({ mode: 'full', intensity: 0.5 }, unavailable))
      .toBe(false)
  })
})
