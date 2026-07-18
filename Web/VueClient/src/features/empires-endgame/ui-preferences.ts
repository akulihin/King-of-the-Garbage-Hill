export const EMPIRES_GOD_UI_PREFERENCES_STORAGE_KEY = 'empires-endgame:ui:god-presence:v1'
export const EMPIRES_GOD_UI_PREFERENCES_SCHEMA_VERSION = 1

export interface EmpiresGodUiPreferences {
  schemaVersion: 1
  skipDivineMercyConfirmation: boolean
}

function defaultPreferences(): EmpiresGodUiPreferences {
  return {
    schemaVersion: EMPIRES_GOD_UI_PREFERENCES_SCHEMA_VERSION,
    skipDivineMercyConfirmation: false,
  }
}

export function loadEmpiresGodUiPreferences(): EmpiresGodUiPreferences {
  const raw = window.localStorage.getItem(EMPIRES_GOD_UI_PREFERENCES_STORAGE_KEY)
  if (!raw) return defaultPreferences()
  try {
    const value: unknown = JSON.parse(raw)
    if (!value || typeof value !== 'object' || Array.isArray(value)) return defaultPreferences()
    const record = value as Record<string, unknown>
    if (record.schemaVersion !== EMPIRES_GOD_UI_PREFERENCES_SCHEMA_VERSION
      || typeof record.skipDivineMercyConfirmation !== 'boolean') return defaultPreferences()
    return {
      schemaVersion: EMPIRES_GOD_UI_PREFERENCES_SCHEMA_VERSION,
      skipDivineMercyConfirmation: record.skipDivineMercyConfirmation,
    }
  } catch {
    return defaultPreferences()
  }
}

export function saveEmpiresGodUiPreferences(preferences: EmpiresGodUiPreferences): void {
  window.localStorage.setItem(
    EMPIRES_GOD_UI_PREFERENCES_STORAGE_KEY,
    JSON.stringify({
      schemaVersion: EMPIRES_GOD_UI_PREFERENCES_SCHEMA_VERSION,
      skipDivineMercyConfirmation: preferences.skipDivineMercyConfirmation === true,
    } satisfies EmpiresGodUiPreferences),
  )
}

export function skipFutureDivineMercyConfirmations(): void {
  saveEmpiresGodUiPreferences({
    schemaVersion: EMPIRES_GOD_UI_PREFERENCES_SCHEMA_VERSION,
    skipDivineMercyConfirmation: true,
  })
}
