const TAVERN_PROFILE_STORAGE_KEY = 'empires-endgame:profile:tavern:v1'

export interface EmpiresTavernProfile {
  schemaVersion: 1
  completedRunOrdinals: number[]
}

function emptyProfile(): EmpiresTavernProfile {
  return { schemaVersion: 1, completedRunOrdinals: [] }
}

export function loadTavernProfile(): EmpiresTavernProfile {
  try {
    const raw = window.localStorage.getItem(TAVERN_PROFILE_STORAGE_KEY)
    if (!raw) return emptyProfile()
    const value = JSON.parse(raw) as Partial<EmpiresTavernProfile>
    if (value.schemaVersion !== 1 || !Array.isArray(value.completedRunOrdinals)) return emptyProfile()
    const completedRunOrdinals = [...new Set(value.completedRunOrdinals
      .filter(run => Number.isInteger(run) && run > 0)
      .map(run => Math.floor(run)))]
      .sort((left, right) => left - right)
      .slice(-64)
    return { schemaVersion: 1, completedRunOrdinals }
  } catch {
    return emptyProfile()
  }
}

export function nextTavernRunOrdinal(profile = loadTavernProfile()): number {
  return Math.max(0, ...profile.completedRunOrdinals) + 1
}

export function recordCompletedTavernRun(runOrdinal: number): EmpiresTavernProfile {
  const profile = loadTavernProfile()
  if (!Number.isInteger(runOrdinal) || runOrdinal < 1
    || profile.completedRunOrdinals.includes(runOrdinal)) return profile
  const next: EmpiresTavernProfile = {
    schemaVersion: 1,
    completedRunOrdinals: [...profile.completedRunOrdinals, runOrdinal]
      .sort((left, right) => left - right)
      .slice(-64),
  }
  window.localStorage.setItem(TAVERN_PROFILE_STORAGE_KEY, JSON.stringify(next))
  return next
}
