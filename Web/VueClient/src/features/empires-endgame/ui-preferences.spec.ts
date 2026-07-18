import { beforeEach, describe, expect, it } from 'vitest'
import {
  EMPIRES_GOD_UI_PREFERENCES_STORAGE_KEY,
  loadEmpiresGodUiPreferences,
  saveEmpiresGodUiPreferences,
  skipFutureDivineMercyConfirmations,
} from './ui-preferences'

describe('Empire\'s Endgame God UI preferences', () => {
  beforeEach(() => window.localStorage.clear())

  it('defaults confirmation skipping off and persists an explicit successful opt-out', () => {
    expect(loadEmpiresGodUiPreferences()).toEqual({
      schemaVersion: 1,
      skipDivineMercyConfirmation: false,
    })

    skipFutureDivineMercyConfirmations()

    expect(loadEmpiresGodUiPreferences()).toEqual({
      schemaVersion: 1,
      skipDivineMercyConfirmation: true,
    })
  })

  it('round-trips the current schema without sharing campaign-save state', () => {
    saveEmpiresGodUiPreferences({
      schemaVersion: 1,
      skipDivineMercyConfirmation: false,
    })

    expect(JSON.parse(window.localStorage.getItem(EMPIRES_GOD_UI_PREFERENCES_STORAGE_KEY) ?? 'null'))
      .toEqual({ schemaVersion: 1, skipDivineMercyConfirmation: false })
  })

  it.each([
    ['corrupt JSON', '{not-json'],
    ['future schema', JSON.stringify({ schemaVersion: 2, skipDivineMercyConfirmation: true })],
    ['malformed value', JSON.stringify({ schemaVersion: 1, skipDivineMercyConfirmation: 'yes' })],
  ])('fails closed for %s', (_label, raw) => {
    window.localStorage.setItem(EMPIRES_GOD_UI_PREFERENCES_STORAGE_KEY, raw)

    expect(loadEmpiresGodUiPreferences().skipDivineMercyConfirmation).toBe(false)
  })
})
