import type { EmpiresCampaignState, EmpiresSnapshotEnvelope } from './types'

export const EMPIRES_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v5'
export const EMPIRES_LEGACY_V4_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v4'
export const EMPIRES_LEGACY_V3_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v3'
export const EMPIRES_LEGACY_V2_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v2'
export const EMPIRES_LEGACY_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v1'

type UnknownRecord = Record<string, unknown>

function isRecord(value: unknown): value is UnknownRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function migrateEmpiresSnapshotEnvelope(value: unknown): EmpiresSnapshotEnvelope | null {
  if (!isRecord(value) || typeof value.savedAt !== 'string' || !isRecord(value.state)) return null
  if (![1, 2, 3, 4, 5].includes(value.schemaVersion as number)) return null
  const state = cloneJson(value.state)
  if (![1, 2, 3, 4, 5].includes(state.schemaVersion as number)) return null
  if (typeof state.configId !== 'string') return null

  // The engine performs the config-aware cohort and political-state fills.
  state.schemaVersion = 5
  return {
    schemaVersion: 5,
    savedAt: value.savedAt,
    state: state as unknown as EmpiresCampaignState,
  }
}

export function saveEmpiresCampaign(state: EmpiresCampaignState) {
  const envelope: EmpiresSnapshotEnvelope = {
    schemaVersion: 5,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
  window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, JSON.stringify(envelope))
}

export function loadEmpiresCampaign(configId: string): EmpiresCampaignState | null {
  const raw = window.localStorage.getItem(EMPIRES_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V4_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V3_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V2_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_SAVE_STORAGE_KEY)
  if (!raw) return null
  try {
    const envelope = migrateEmpiresSnapshotEnvelope(JSON.parse(raw))
    if (!envelope || envelope.state.configId !== configId) return null
    return structuredClone(envelope.state)
  }
  catch (error) {
    console.warn('Empire\'s Endgame save could not be restored.', error)
    return null
  }
}

export function clearEmpiresCampaign() {
  window.localStorage.removeItem(EMPIRES_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V4_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V3_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V2_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_SAVE_STORAGE_KEY)
}

export function exportEmpiresCampaign(state: EmpiresCampaignState): EmpiresSnapshotEnvelope {
  return {
    schemaVersion: 5,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
}

export function importEmpiresCampaign(value: unknown, configId: string): EmpiresCampaignState {
  const envelope = migrateEmpiresSnapshotEnvelope(value)
  if (!envelope) throw new Error('Это не поддерживаемое сохранение Empire\'s Endgame версии 1, 2, 3, 4 или 5.')
  if (envelope.state.configId !== configId) {
    throw new Error('Сохранение создано для другой конфигурации игры.')
  }
  return structuredClone(envelope.state)
}
