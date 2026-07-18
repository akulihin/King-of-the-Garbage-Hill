import {
  LAST_CHANCES_CONTROL_SCHEMES,
  LAST_CHANCES_FEEDBACK_MODES,
  type LastChancesControlScheme,
  type LastChancesFeedbackMode,
} from './types'

export const LAST_CHANCES_CONTROL_SCHEME_STORAGE_KEY = '99lc:control-scheme'
export const LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY = '99lc:feedback-mode'
export const LAST_CHANCES_FEEDBACK_INTENSITY_STORAGE_KEY = '99lc:feedback-intensity'

export const LAST_CHANCES_FEEDBACK_MODE_LABELS: Readonly<
  Record<LastChancesFeedbackMode, 'Off' | 'Reduced' | 'Full'>
> = {
  off: 'Off',
  reduced: 'Reduced',
  full: 'Full',
}

export const DEFAULT_LAST_CHANCES_CONTROL_SCHEME: LastChancesControlScheme = 'legacy'
export const DEFAULT_LAST_CHANCES_FEEDBACK_INTENSITY = 1

export interface LastChancesFeedbackPreferences {
  mode: LastChancesFeedbackMode
  intensity: number
}

function browserStorage(): Storage | null {
  if (typeof window === 'undefined') return null
  try {
    return window.localStorage
  } catch {
    return null
  }
}

function resolveStorage(storage: Storage | null | undefined): Storage | null {
  return storage === undefined ? browserStorage() : storage
}

function readStorageValue(key: string, storage?: Storage | null): string | null {
  try {
    return resolveStorage(storage)?.getItem(key) ?? null
  } catch {
    return null
  }
}

function writeStorageValue(key: string, value: string, storage?: Storage | null): boolean {
  const target = resolveStorage(storage)
  if (!target) return false
  try {
    target.setItem(key, value)
    return true
  } catch {
    return false
  }
}

export function isLastChancesControlScheme(value: unknown): value is LastChancesControlScheme {
  return typeof value === 'string'
    && (LAST_CHANCES_CONTROL_SCHEMES as readonly string[]).includes(value)
}

export function loadLastChancesControlScheme(
  storage?: Storage | null,
): LastChancesControlScheme {
  const stored = readStorageValue(LAST_CHANCES_CONTROL_SCHEME_STORAGE_KEY, storage)
  return isLastChancesControlScheme(stored)
    ? stored
    : DEFAULT_LAST_CHANCES_CONTROL_SCHEME
}

export function saveLastChancesControlScheme(
  scheme: LastChancesControlScheme,
  storage?: Storage | null,
): boolean {
  if (!isLastChancesControlScheme(scheme)) return false
  return writeStorageValue(LAST_CHANCES_CONTROL_SCHEME_STORAGE_KEY, scheme, storage)
}

export function isLastChancesFeedbackMode(value: unknown): value is LastChancesFeedbackMode {
  return typeof value === 'string'
    && (LAST_CHANCES_FEEDBACK_MODES as readonly string[]).includes(value)
}

export function suggestLastChancesFeedbackMode(
  prefersReducedMotion: boolean,
): LastChancesFeedbackMode {
  return prefersReducedMotion ? 'reduced' : 'full'
}

export function loadLastChancesFeedbackMode(
  storage?: Storage | null,
  prefersReducedMotion = false,
): LastChancesFeedbackMode {
  const stored = readStorageValue(LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY, storage)
  return isLastChancesFeedbackMode(stored)
    ? stored
    : suggestLastChancesFeedbackMode(prefersReducedMotion)
}

export function saveLastChancesFeedbackMode(
  mode: LastChancesFeedbackMode,
  storage?: Storage | null,
): boolean {
  if (!isLastChancesFeedbackMode(mode)) return false
  return writeStorageValue(LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY, mode, storage)
}

export function loadLastChancesFeedbackIntensity(storage?: Storage | null): number {
  const stored = readStorageValue(LAST_CHANCES_FEEDBACK_INTENSITY_STORAGE_KEY, storage)
  if (stored === null || stored.trim() === '') return DEFAULT_LAST_CHANCES_FEEDBACK_INTENSITY
  const parsed = Number(stored)
  return Number.isFinite(parsed) && parsed >= 0 && parsed <= 1
    ? parsed
    : DEFAULT_LAST_CHANCES_FEEDBACK_INTENSITY
}

export function saveLastChancesFeedbackIntensity(
  intensity: number,
  storage?: Storage | null,
): boolean {
  if (!Number.isFinite(intensity)) return false
  const normalized = Math.max(0, Math.min(1, intensity))
  return writeStorageValue(
    LAST_CHANCES_FEEDBACK_INTENSITY_STORAGE_KEY,
    String(normalized),
    storage,
  )
}

export function loadLastChancesFeedbackPreferences(
  storage?: Storage | null,
  prefersReducedMotion = false,
): LastChancesFeedbackPreferences {
  return {
    mode: loadLastChancesFeedbackMode(storage, prefersReducedMotion),
    intensity: loadLastChancesFeedbackIntensity(storage),
  }
}

export function saveLastChancesFeedbackPreferences(
  preferences: LastChancesFeedbackPreferences,
  storage?: Storage | null,
): boolean {
  if (!isLastChancesFeedbackMode(preferences.mode)
    || !Number.isFinite(preferences.intensity)) return false
  const target = resolveStorage(storage)
  if (!target) return false

  const normalizedIntensity = Math.max(0, Math.min(1, preferences.intensity))
  try {
    target.setItem(LAST_CHANCES_FEEDBACK_MODE_STORAGE_KEY, preferences.mode)
    target.setItem(LAST_CHANCES_FEEDBACK_INTENSITY_STORAGE_KEY, String(normalizedIntensity))
    return true
  } catch {
    return false
  }
}
