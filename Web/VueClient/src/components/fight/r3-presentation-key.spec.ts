import { createR3PresentationKey } from './r3-presentation-key'

const fight = {
  attackerName: 'Attacker',
  defenderName: 'Defender',
  outcome: 'win',
  usedRandomRoll: true,
  randomNumber: 38,
  randomForPoint: 32,
  maxRandomNumber: 100,
}

describe('createR3PresentationKey', () => {
  it('ignores object identity for identical live DTO snapshots', () => {
    const input = {
      roundKey: 'game-7:8',
      fightIndex: 0,
      perspectiveUsername: 'Attacker',
      fight,
    }

    expect(createR3PresentationKey(input)).toBe(createR3PresentationKey({
      ...input,
      fight: structuredClone(fight),
    }))
  })

  it.each([
    ['round', { roundKey: 'game-7:9' }],
    ['selected fight', { fightIndex: 1 }],
    ['perspective', { perspectiveUsername: 'Defender' }],
    ['participant', { fight: { ...fight, defenderName: 'Other' } }],
    ['outcome', { fight: { ...fight, outcome: 'loss' } }],
    ['roll', { fight: { ...fight, randomNumber: 39 } }],
    ['threshold', { fight: { ...fight, randomForPoint: 33 } }],
    ['range', { fight: { ...fight, maxRandomNumber: 110 } }],
  ])('changes when the %s presentation changes', (_label, change) => {
    const base = {
      roundKey: 'game-7:8',
      fightIndex: 0,
      perspectiveUsername: 'Attacker',
      fight,
    }

    expect(createR3PresentationKey({ ...base, ...change }))
      .not.toBe(createR3PresentationKey(base))
  })
})
