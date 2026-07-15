export function hashLastChancesSeed(seed: string | number): number {
  const value = String(seed)
  let hash = 2166136261
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index)
    hash = Math.imul(hash, 16777619)
  }
  return hash >>> 0
}

export function createLastChancesRng(seed: string | number): () => number {
  let state = hashLastChancesSeed(seed)
  return () => {
    state += 0x6D2B79F5
    let value = state
    value = Math.imul(value ^ (value >>> 15), value | 1)
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61)
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296
  }
}

export function lastChancesRandomInt(rng: () => number, minimum: number, maximum: number): number {
  return minimum + Math.floor(rng() * (maximum - minimum + 1))
}

export function lastChancesShuffle<T>(values: readonly T[], rng: () => number): T[] {
  const result = [...values]
  for (let index = result.length - 1; index > 0; index -= 1) {
    const other = Math.floor(rng() * (index + 1))
    const held = result[index]
    result[index] = result[other]
    result[other] = held
  }
  return result
}

export function pickLastChancesWeighted<T extends { weight: number }>(
  values: readonly T[],
  rng: () => number,
): T {
  const total = values.reduce((sum, value) => sum + value.weight, 0)
  let cursor = rng() * total
  for (const value of values) {
    cursor -= value.weight
    if (cursor <= 0) return value
  }
  return values[values.length - 1]
}
