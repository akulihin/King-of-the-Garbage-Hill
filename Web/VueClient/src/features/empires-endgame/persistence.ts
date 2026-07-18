import type { EmpiresCampaignState, EmpiresSnapshotEnvelope } from './types'

export const EMPIRES_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v15'
export const EMPIRES_LEGACY_V14_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v14'
export const EMPIRES_LEGACY_V13_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v13'
export const EMPIRES_LEGACY_V12_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v12'
export const EMPIRES_LEGACY_V11_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v11'
export const EMPIRES_LEGACY_V10_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v10'
export const EMPIRES_LEGACY_V9_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v9'
export const EMPIRES_LEGACY_V8_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v8'
export const EMPIRES_LEGACY_V7_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v7'
export const EMPIRES_LEGACY_V6_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v6'
export const EMPIRES_LEGACY_V5_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v5'
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
  if (![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15].includes(value.schemaVersion as number)) return null
  const state = cloneJson(value.state)
  if (![1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15].includes(state.schemaVersion as number)) return null
  if (typeof state.configId !== 'string') return null

  // The engine performs the config-aware cohort and political-state fills.
  state.schemaVersion = 15
  return {
    schemaVersion: 15,
    savedAt: value.savedAt,
    state: state as unknown as EmpiresCampaignState,
  }
}

export function saveEmpiresCampaign(state: EmpiresCampaignState) {
  const envelope: EmpiresSnapshotEnvelope = {
    schemaVersion: 15,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
  window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, JSON.stringify(envelope))
}

export function loadEmpiresCampaign(configId: string): EmpiresCampaignState | null {
  const raw = window.localStorage.getItem(EMPIRES_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V14_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V13_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V12_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V11_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V10_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V9_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V8_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V7_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V6_SAVE_STORAGE_KEY)
    ?? window.localStorage.getItem(EMPIRES_LEGACY_V5_SAVE_STORAGE_KEY)
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
  window.localStorage.removeItem(EMPIRES_LEGACY_V14_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V13_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V12_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V11_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V10_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V9_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V8_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V7_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V6_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V5_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V4_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V3_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V2_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_SAVE_STORAGE_KEY)
}

export function exportEmpiresCampaign(state: EmpiresCampaignState): EmpiresSnapshotEnvelope {
  return {
    schemaVersion: 15,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
}

export function importEmpiresCampaign(value: unknown, configId: string): EmpiresCampaignState {
  const envelope = migrateEmpiresSnapshotEnvelope(value)
  if (!envelope) throw new Error('Это не поддерживаемое сохранение Empire\'s Endgame версии 1–15.')
  if (envelope.state.configId !== configId) {
    throw new Error('Сохранение создано для другой конфигурации игры.')
  }
  return structuredClone(envelope.state)
}
