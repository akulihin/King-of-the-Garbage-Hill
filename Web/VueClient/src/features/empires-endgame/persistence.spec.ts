import { afterEach, describe, expect, it, vi } from 'vitest'
import bundledConfigJson from '../../../public/empires-endgame/game-config.json'
import { EmpiresEndgameEngine } from './engine'
import {
  EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY,
  EMPIRES_SAVE_STORAGE_KEY,
  clearEmpiresCampaign,
  loadEmpiresCampaign,
} from './persistence'
import type { EmpiresCampaignState, EmpiresEndgameConfig } from './types'

function config(): EmpiresEndgameConfig {
  return structuredClone(bundledConfigJson) as EmpiresEndgameConfig
}

function envelope(state: EmpiresCampaignState) {
  return {
    schemaVersion: state.schemaVersion,
    savedAt: '2026-07-21T00:00:00.000Z',
    state,
  }
}

afterEach(() => {
  clearEmpiresCampaign()
  vi.restoreAllMocks()
})

describe('Empire\'s Endgame storage selection', () => {
  it('skips a syntactically valid but semantically corrupt current save for a valid legacy key', () => {
    const value = config()
    const invalidCurrent = new EmpiresEndgameEngine(value).snapshot()
    delete (invalidCurrent as Partial<EmpiresCampaignState>).minigameResultCompaction
    const validLegacy = new EmpiresEndgameEngine(value).snapshot()
    validLegacy.schemaVersion = 15 as typeof validLegacy.schemaVersion
    window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, JSON.stringify(envelope(invalidCurrent)))
    window.localStorage.setItem(
      EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY,
      JSON.stringify(envelope(validLegacy)),
    )
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const validate = vi.fn((candidate: EmpiresCampaignState) => {
      new EmpiresEndgameEngine(value, candidate)
    })

    expect(loadEmpiresCampaign(value.id, validate)).toEqual(validLegacy)
    expect(validate).toHaveBeenCalledTimes(2)
  })

  it('surfaces semantic corruption when no older valid save exists', () => {
    const value = config()
    const invalidCurrent = new EmpiresEndgameEngine(value).snapshot()
    delete (invalidCurrent as Partial<EmpiresCampaignState>).minigameResultCompaction
    window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, JSON.stringify(envelope(invalidCurrent)))
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)

    expect(() => loadEmpiresCampaign(value.id, (candidate) => {
      new EmpiresEndgameEngine(value, candidate)
    })).toThrow(/current snapshot requires minigame/i)
  })
})
