import type { EmpiresRngState } from './types'

export function hashEmpiresSeed(seed: string | number): number {
  const text = String(seed)
  let hash = 2166136261
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index)
    hash = Math.imul(hash, 16777619)
  }
  return hash >>> 0
}

export function createEmpiresRngState(seed: string | number): EmpiresRngState {
  return { state: hashEmpiresSeed(seed), draws: 0 }
}

/** Advances a serializable Mulberry32 state and returns a value in [0, 1). */
export function nextEmpiresRandom(rng: EmpiresRngState): number {
  rng.state = (rng.state + 0x6D2B79F5) >>> 0
  rng.draws += 1
  let value = rng.state
  value = Math.imul(value ^ (value >>> 15), value | 1)
  value ^= value + Math.imul(value ^ (value >>> 7), value | 61)
  return ((value ^ (value >>> 14)) >>> 0) / 4294967296
}

export function nextEmpiresRandomInt(
  rng: EmpiresRngState,
  minimum: number,
  maximum: number,
): number {
  if (!Number.isInteger(minimum) || !Number.isInteger(maximum) || maximum < minimum) {
    throw new RangeError('Invalid deterministic integer range')
  }
  return minimum + Math.floor(nextEmpiresRandom(rng) * (maximum - minimum + 1))
}

export function shuffleEmpires<T>(values: readonly T[], rng: EmpiresRngState): T[] {
  const result = [...values]
  for (let index = result.length - 1; index > 0; index -= 1) {
    const other = nextEmpiresRandomInt(rng, 0, index)
    ;[result[index], result[other]] = [result[other], result[index]]
  }
  return result
}

export function pickEmpiresWeighted<T extends { weight: number }>(
  values: readonly T[],
  rng: EmpiresRngState,
): T | null {
  const candidates = values.filter(value => Number.isFinite(value.weight) && value.weight > 0)
  const total = candidates.reduce((sum, value) => sum + value.weight, 0)
  if (candidates.length === 0 || total <= 0) return null
  let cursor = nextEmpiresRandom(rng) * total
  for (const value of candidates) {
    cursor -= value.weight
    if (cursor < 0) return value
  }
  return candidates[candidates.length - 1]
}

export function pickEmpiresWeightedWithoutReplacement<T extends { weight: number }>(
  values: readonly T[],
  count: number,
  rng: EmpiresRngState,
): T[] {
  const pool = [...values]
  const picked: T[] = []
  while (pool.length > 0 && picked.length < count) {
    const value = pickEmpiresWeighted(pool, rng)
    if (!value) break
    picked.push(value)
    pool.splice(pool.indexOf(value), 1)
  }
  return picked
}
