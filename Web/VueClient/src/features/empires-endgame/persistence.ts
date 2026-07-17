import type { EmpiresCampaignState, EmpiresSnapshotEnvelope } from './types'

export const EMPIRES_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v2'
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
  if (value.schemaVersion !== 1 && value.schemaVersion !== 2) return null
  const state = cloneJson(value.state)
  if (state.schemaVersion !== 1 && state.schemaVersion !== 2) return null
  if (typeof state.configId !== 'string') return null

  // Phase 2 is the first semantic save migration: v1 had only null/empty
  // minigame homes and no minigame campaign phase.
  state.schemaVersion = 2
  return {
    schemaVersion: 2,
    savedAt: value.savedAt,
    state: state as unknown as EmpiresCampaignState,
  }
}

export function saveEmpiresCampaign(state: EmpiresCampaignState) {
  const envelope: EmpiresSnapshotEnvelope = {
    schemaVersion: 2,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
  window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, JSON.stringify(envelope))
}

export function loadEmpiresCampaign(configId: string): EmpiresCampaignState | null {
  const raw = window.localStorage.getItem(EMPIRES_SAVE_STORAGE_KEY)
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
  window.localStorage.removeItem(EMPIRES_LEGACY_SAVE_STORAGE_KEY)
}

export function exportEmpiresCampaign(state: EmpiresCampaignState): EmpiresSnapshotEnvelope {
  return {
    schemaVersion: 2,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
}

export function importEmpiresCampaign(value: unknown, configId: string): EmpiresCampaignState {
  const envelope = migrateEmpiresSnapshotEnvelope(value)
  if (!envelope) throw new Error('Это не поддерживаемое сохранение Empire\'s Endgame версии 1 или 2.')
  if (envelope.state.configId !== configId) {
    throw new Error('Сохранение создано для другой конфигурации игры.')
  }
  return structuredClone(envelope.state)
}
