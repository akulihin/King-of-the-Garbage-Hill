import type { EmpiresCampaignState, EmpiresSnapshotEnvelope } from './types'
import {
  EMPIRES_SAVE_SCHEMA_VERSION,
  EMPIRES_STABILIZATION_BUDGETS,
  empiresUtf8ByteLength,
} from './stabilization'

export const EMPIRES_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v18'
export const EMPIRES_LEGACY_V17_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v17'
export const EMPIRES_LEGACY_V16_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v16'
export const EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v15'
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
  const supportedVersions = Array.from({ length: EMPIRES_SAVE_SCHEMA_VERSION }, (_, index) => index + 1)
  if (!supportedVersions.includes(value.schemaVersion as number)) return null
  const state = cloneJson(value.state)
  if (!supportedVersions.includes(state.schemaVersion as number)
    || state.schemaVersion !== value.schemaVersion) return null
  if (typeof state.configId !== 'string') return null

  // Preserve the source state version: the engine owns config-aware, version-
  // conditioned normalization. Stamping it here would skip those migrations.
  return {
    schemaVersion: EMPIRES_SAVE_SCHEMA_VERSION,
    savedAt: value.savedAt,
    state: state as unknown as EmpiresCampaignState,
  }
}

function serializeBoundedEnvelope(envelope: EmpiresSnapshotEnvelope): string {
  const serialized = JSON.stringify(envelope)
  const bytes = empiresUtf8ByteLength(serialized)
  if (bytes > EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes) {
    throw new Error(`Сохранение Empire's Endgame занимает ${bytes} байт и превышает лимит ${EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes}.`)
  }
  return serialized
}

export function saveEmpiresCampaign(state: EmpiresCampaignState) {
  const envelope: EmpiresSnapshotEnvelope = {
    schemaVersion: EMPIRES_SAVE_SCHEMA_VERSION,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
  window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, serializeBoundedEnvelope(envelope))
}

export function loadEmpiresCampaign(
  configId: string,
  validateState?: (state: EmpiresCampaignState) => void,
): EmpiresCampaignState | null {
  const storageKeys = [
    EMPIRES_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V17_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V16_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V14_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V13_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V12_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V11_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V10_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V9_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V8_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V7_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V6_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V5_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V4_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V3_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_V2_SAVE_STORAGE_KEY,
    EMPIRES_LEGACY_SAVE_STORAGE_KEY,
  ]
  let semanticError: unknown = null
  for (const storageKey of storageKeys) {
    const raw = window.localStorage.getItem(storageKey)
    if (!raw) continue
    try {
      if (storageKey === EMPIRES_SAVE_STORAGE_KEY
        && empiresUtf8ByteLength(raw) > EMPIRES_STABILIZATION_BUDGETS.longCampaignSaveUtf8Bytes) {
        throw new Error('Current save exceeds the shipped localStorage size limit.')
      }
      const envelope = migrateEmpiresSnapshotEnvelope(JSON.parse(raw))
      if (!envelope || envelope.state.configId !== configId) continue
      const state = structuredClone(envelope.state)
      try {
        validateState?.(structuredClone(state))
      }
      catch (error) {
        semanticError ??= error
        throw error
      }
      return state
    }
    catch (error) {
      console.warn(`Empire's Endgame save ${storageKey} could not be restored.`, error)
    }
  }
  if (semanticError) throw semanticError
  return null
}

export function clearEmpiresCampaign() {
  window.localStorage.removeItem(EMPIRES_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V17_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V16_SAVE_STORAGE_KEY)
  window.localStorage.removeItem(EMPIRES_LEGACY_V15_SAVE_STORAGE_KEY)
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
  const envelope: EmpiresSnapshotEnvelope = {
    schemaVersion: EMPIRES_SAVE_SCHEMA_VERSION,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
  serializeBoundedEnvelope(envelope)
  return envelope
}

export function importEmpiresCampaign(value: unknown, configId: string): EmpiresCampaignState {
  const sourceSchemaVersion = isRecord(value) && Number.isInteger(value.schemaVersion)
    ? Number(value.schemaVersion)
    : null
  const envelope = migrateEmpiresSnapshotEnvelope(value)
  if (!envelope) throw new Error('Это не поддерживаемое сохранение Empire\'s Endgame версии 1–18 с согласованной версией состояния.')
  // Legacy histories predate the bounded compaction model. Let the engine
  // normalize them first; every subsequent v18 save/export enforces 512 KiB.
  if (sourceSchemaVersion === EMPIRES_SAVE_SCHEMA_VERSION) serializeBoundedEnvelope(envelope)
  if (envelope.state.configId !== configId) {
    throw new Error('Сохранение создано для другой конфигурации игры.')
  }
  return structuredClone(envelope.state)
}
