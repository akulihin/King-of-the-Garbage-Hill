import type {
  EmpiresSeasonDefinition,
  EmpiresSeasonsConfig,
} from './types'

export function currentSeason(
  con: number,
  config: EmpiresSeasonsConfig,
): EmpiresSeasonDefinition | null {
  if (!config.enabled || config.definitions.length === 0) return null
  const cycleLength = config.definitions.reduce(
    (total, definition) => total + definition.durationCons,
    0,
  )
  if (cycleLength <= 0) return null
  const normalizedCon = Math.max(1, Math.floor(con))
  let cycleOffset = (normalizedCon - 1) % cycleLength
  for (const definition of config.definitions) {
    if (cycleOffset < definition.durationCons) return definition
    cycleOffset -= definition.durationCons
  }
  return config.definitions[0]
}

export function currentSeasonFoodMultiplier(
  con: number,
  config: EmpiresSeasonsConfig,
  researchedTechnologyIds: readonly string[],
): number {
  const season = currentSeason(con, config)
  if (!season) return 1
  const greenhouse = config.greenhouse
  if (greenhouse && researchedTechnologyIds.includes(greenhouse.technologyId)) {
    return greenhouse.equalizedFoodProductionMultiplier
  }
  return season.foodProductionMultiplier
}

export function applySeasonFoodProduction(
  amount: number,
  con: number,
  config: EmpiresSeasonsConfig,
  researchedTechnologyIds: readonly string[],
): number {
  const seasonalAmount = amount * currentSeasonFoodMultiplier(
    con,
    config,
    researchedTechnologyIds,
  )
  if (!config.enabled || config.foodRounding === 'none') return seasonalAmount
  if (config.foodRounding === 'round') return Math.round(seasonalAmount)
  return Math.floor(seasonalAmount)
}
