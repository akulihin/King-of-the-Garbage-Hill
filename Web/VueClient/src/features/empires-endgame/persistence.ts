import type { EmpiresCampaignState, EmpiresSnapshotEnvelope } from './types'

export const EMPIRES_SAVE_STORAGE_KEY = 'empires-endgame:campaign:v1'

function isSnapshotEnvelope(value: unknown): value is EmpiresSnapshotEnvelope {
  if (typeof value !== 'object' || value === null) return false
  const candidate = value as Partial<EmpiresSnapshotEnvelope>
  return candidate.schemaVersion === 1
    && typeof candidate.savedAt === 'string'
    && typeof candidate.state === 'object'
    && candidate.state !== null
    && candidate.state.schemaVersion === 1
    && typeof candidate.state.configId === 'string'
}

export function saveEmpiresCampaign(state: EmpiresCampaignState) {
  const envelope: EmpiresSnapshotEnvelope = {
    schemaVersion: 1,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
  window.localStorage.setItem(EMPIRES_SAVE_STORAGE_KEY, JSON.stringify(envelope))
}

export function loadEmpiresCampaign(configId: string): EmpiresCampaignState | null {
  const raw = window.localStorage.getItem(EMPIRES_SAVE_STORAGE_KEY)
  if (!raw) return null
  try {
    const value: unknown = JSON.parse(raw)
    if (!isSnapshotEnvelope(value) || value.state.configId !== configId) return null
    return structuredClone(value.state)
  }
  catch (error) {
    console.warn('Empire\'s Endgame save could not be restored.', error)
    return null
  }
}

export function clearEmpiresCampaign() {
  window.localStorage.removeItem(EMPIRES_SAVE_STORAGE_KEY)
}

export function exportEmpiresCampaign(state: EmpiresCampaignState): EmpiresSnapshotEnvelope {
  return {
    schemaVersion: 1,
    savedAt: new Date().toISOString(),
    state: structuredClone(state),
  }
}

export function importEmpiresCampaign(value: unknown, configId: string): EmpiresCampaignState {
  if (!isSnapshotEnvelope(value)) throw new Error('Это не сохранение Empire\'s Endgame версии 1.')
  if (value.state.configId !== configId) throw new Error('Сохранение создано для другой конфигурации игры.')
  return structuredClone(value.state)
}
