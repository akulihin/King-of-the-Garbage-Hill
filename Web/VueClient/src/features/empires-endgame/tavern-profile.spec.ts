import { beforeEach, describe, expect, it } from 'vitest'
import {
  loadTavernProfile,
  nextTavernRunOrdinal,
  recordCompletedTavernRun,
} from './tavern/profile'

const STORAGE_KEY = 'empires-endgame:profile:tavern:v1'

describe('Empire\'s Endgame Tavern run profile', () => {
  beforeEach(() => window.localStorage.clear())

  it('persists completed cross-campaign run ordinals exactly once', () => {
    expect(nextTavernRunOrdinal()).toBe(1)
    expect(recordCompletedTavernRun(1).completedRunOrdinals).toEqual([1])
    expect(recordCompletedTavernRun(1).completedRunOrdinals).toEqual([1])
    expect(recordCompletedTavernRun(2).completedRunOrdinals).toEqual([1, 2])
    expect(loadTavernProfile().completedRunOrdinals).toEqual([1, 2])
    expect(nextTavernRunOrdinal()).toBe(3)
  })

  it('fails closed on malformed data and bounds retained run history', () => {
    window.localStorage.setItem(STORAGE_KEY, '{not-json')
    expect(loadTavernProfile()).toEqual({ schemaVersion: 1, completedRunOrdinals: [] })
    window.localStorage.clear()
    for (let run = 1; run <= 70; run += 1) recordCompletedTavernRun(run)
    expect(loadTavernProfile().completedRunOrdinals).toEqual(
      Array.from({ length: 64 }, (_, index) => index + 7),
    )
  })
})
