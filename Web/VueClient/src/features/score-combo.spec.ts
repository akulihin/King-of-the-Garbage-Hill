import { describe, expect, it } from 'vitest'
import type { ScoreBreakdown } from 'src/services/signalr'
import {
  buildScoreAnimHits,
  buildScoreGroups,
  isScoreComboHit,
} from './score-combo'

describe('score combo feed', () => {
  it('puts every negative source first and excludes it from combo numbering', () => {
    const breakdown: ScoreBreakdown = {
      roundMultiplier: 2,
      expectedRoundMultiplier: 2,
      entries: [
        { source: 'Победа', points: 1, isBonus: false },
        { source: 'Блок', points: -1, isBonus: true, isNegative: true },
        { source: 'Пассивка', points: 2, isBonus: false },
        { source: 'Осьминожка', points: -1, isBonus: false, isNegative: true },
        { source: 'Бонус', points: 3, isBonus: true },
      ],
    }

    const groups = buildScoreGroups(breakdown)
    const hits = buildScoreAnimHits(groups)

    expect(groups.map(group => group.type)).toEqual(['negative', 'regular', 'bonus'])
    expect(hits.map(hit => hit.name)).toEqual([
      'Блок',
      'Осьминожка',
      'Победа',
      'Пассивка',
      'Бонус',
    ])
    expect(hits.map(hit => hit.comboIndex)).toEqual([-1, -1, 0, 1, 0])
    expect(hits.filter(isScoreComboHit).map(hit => hit.name))
      .toEqual(['Победа', 'Пассивка', 'Бонус'])
  })

  it('shows Tolya as a text-only negative source without creating a combo hit', () => {
    const breakdown: ScoreBreakdown = {
      roundMultiplier: 1,
      expectedRoundMultiplier: 4,
      entries: [
        {
          source: 'Вас обсчитали',
          points: 0,
          isBonus: false,
          isNegative: true,
          hidePoints: true,
        },
        { source: 'Победа', points: 1, isBonus: false },
      ],
    }

    const hits = buildScoreAnimHits(buildScoreGroups(breakdown))

    expect(hits[0]).toMatchObject({
      name: 'Вас обсчитали',
      hidePoints: true,
      comboIndex: -1,
      groupType: 'negative',
    })
    expect(hits[1]).toMatchObject({ name: 'Победа', comboIndex: 0 })
  })
})
