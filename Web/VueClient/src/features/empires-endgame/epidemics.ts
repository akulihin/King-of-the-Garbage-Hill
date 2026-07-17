import { digestTdValue } from './td/engine'
import type {
  EmpiresEpidemicClassImpactDefinition,
  EmpiresEpidemicConfig,
  EmpiresEpidemicDefinition,
} from './types'

function compareIds(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

export function epidemicRulesDigest(config: EmpiresEpidemicConfig): string {
  return digestTdValue({
    rulesVersion: config.rulesVersion,
    populationRounding: config.populationRounding,
    productionRounding: config.productionRounding,
    loyaltyRounding: config.loyaltyRounding,
    maxSpreadTargetsPerSettlement: config.maxSpreadTargetsPerSettlement,
    definitions: config.definitions,
    protections: config.protections,
  })
}

export function epidemicDuration(definition: EmpiresEpidemicDefinition): number {
  return definition.stages.reduce((total, stage) => total + stage.durationCons, 0)
}

export function roundEpidemicValue(
  value: number,
  mode: 'floor' | 'round' | 'none',
): number {
  if (mode === 'none') return value
  if (mode === 'floor') return Math.floor(value)
  return Math.round(value)
}

export function roundSignedEpidemicValue(value: number): number {
  return Math.sign(value) * Math.round(Math.abs(value))
}

/**
 * Allocates an integer loss by authored weights, not by class population share.
 * Largest remainders are resolved by stable class id; exhausted classes are
 * removed and the remainder is reallocated through the same rule.
 */
export function allocateEpidemicClassLoss(
  requestedLoss: number,
  affectedClasses: readonly EmpiresEpidemicClassImpactDefinition[],
  populationClasses: Readonly<Record<string, number>>,
): Record<string, number> {
  let remaining = Math.max(0, Math.floor(requestedLoss))
  const allocations: Record<string, number> = Object.fromEntries(
    affectedClasses.map(item => [item.populationClassId, 0]),
  )
  let candidates = affectedClasses
    .map(item => ({
      id: item.populationClassId,
      weight: item.weight,
      capacity: Math.max(0, Math.floor(populationClasses[item.populationClassId] ?? 0)),
    }))
    .filter(item => item.weight > 0 && item.capacity > 0)
    .sort((left, right) => compareIds(left.id, right.id))

  remaining = Math.min(remaining, candidates.reduce((total, item) => total + item.capacity, 0))
  while (remaining > 0 && candidates.length > 0) {
    const weightTotal = candidates.reduce((total, item) => total + item.weight, 0)
    const requestedThisPass = remaining
    const shares = candidates.map((item) => {
      const raw = requestedThisPass * item.weight / weightTotal
      const base = Math.min(item.capacity, Math.floor(raw))
      return { item, raw, base, fraction: raw - Math.floor(raw) }
    })
    let allocated = shares.reduce((total, share) => total + share.base, 0)
    for (const share of shares) {
      allocations[share.item.id] += share.base
      share.item.capacity -= share.base
    }
    let remainder = requestedThisPass - allocated
    const remainders = shares
      .filter(share => share.item.capacity > 0)
      .sort((left, right) => right.fraction - left.fraction || compareIds(left.item.id, right.item.id))
    for (const share of remainders) {
      if (remainder <= 0) break
      allocations[share.item.id] += 1
      share.item.capacity -= 1
      remainder -= 1
      allocated += 1
    }
    if (allocated <= 0) break
    remaining -= allocated
    candidates = candidates.filter(item => item.capacity > 0)
  }
  return allocations
}
