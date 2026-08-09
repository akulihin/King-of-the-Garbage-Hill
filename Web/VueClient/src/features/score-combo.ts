import type { ScoreBreakdown } from 'src/services/signalr'

export type ScoreGroupType = 'negative' | 'regular' | 'bonus'

export interface ScoreSourceEntry {
  name: string
  basePts: number
  pointsEarned: number
  hidePoints: boolean
}

export interface ScoreGroup {
  type: ScoreGroupType
  multiplier: number
  entries: ScoreSourceEntry[]
  totalPoints: number
}

export interface ScoreAnimHit extends ScoreSourceEntry {
  comboIndex: number
  groupType: ScoreGroupType
  groupMultiplier: number
}

export function buildScoreGroups(breakdown: ScoreBreakdown | null | undefined): ScoreGroup[] {
  if (!breakdown) return []

  const multiplier = breakdown.roundMultiplier
  const regularPointsMultiplier = Math.max(1, breakdown.regularPointsMultiplier ?? 1)
  const effectiveRegularMultiplier = multiplier * regularPointsMultiplier
  const negativeEntries: ScoreSourceEntry[] = []
  const regularEntries: ScoreSourceEntry[] = []
  const bonusEntries: ScoreSourceEntry[] = []

  for (const entry of breakdown.entries) {
    const source: ScoreSourceEntry = {
      name: entry.source || (entry.isBonus ? 'Бонус' : 'Очки'),
      basePts: entry.points,
      pointsEarned: entry.isBonus
        ? entry.points
        : Math.round(entry.points * effectiveRegularMultiplier),
      hidePoints: entry.hidePoints === true,
    }
    const isNegative = entry.isNegative === true || source.pointsEarned < 0

    if (isNegative) negativeEntries.push(source)
    else if (entry.isBonus) bonusEntries.push(source)
    else regularEntries.push(source)
  }

  const groups: ScoreGroup[] = []
  if (negativeEntries.length > 0) {
    groups.push({
      type: 'negative',
      multiplier: 1,
      entries: negativeEntries,
      totalPoints: negativeEntries.reduce((sum, entry) => sum + entry.pointsEarned, 0),
    })
  }
  if (regularEntries.length > 0) {
    groups.push({
      type: 'regular',
      multiplier,
      entries: regularEntries,
      totalPoints: Math.round(
        regularEntries.reduce((sum, entry) => sum + entry.basePts, 0)
          * effectiveRegularMultiplier,
      ),
    })
  }
  if (bonusEntries.length > 0) {
    groups.push({
      type: 'bonus',
      multiplier: 1,
      entries: bonusEntries,
      totalPoints: bonusEntries.reduce((sum, entry) => sum + entry.pointsEarned, 0),
    })
  }
  return groups
}

export function buildScoreAnimHits(groups: readonly ScoreGroup[]): ScoreAnimHit[] {
  const hits: ScoreAnimHit[] = []
  for (const group of groups) {
    let comboIndex = 0
    for (const entry of group.entries) {
      const isComboHit = group.type !== 'negative' && entry.pointsEarned > 0
      hits.push({
        ...entry,
        comboIndex: isComboHit ? comboIndex++ : -1,
        groupType: group.type,
        groupMultiplier: group.multiplier,
      })
    }
  }
  return hits
}

export function isScoreComboHit(hit: ScoreAnimHit): boolean {
  return hit.comboIndex >= 0
}
