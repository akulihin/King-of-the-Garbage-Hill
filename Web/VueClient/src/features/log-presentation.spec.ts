import {
  isStanEdgarThresholdDialogue,
  orderStanEdgarDismissalLogs,
} from './log-presentation'

describe('Stan Edgar log presentation', () => {
  it.each([
    'Stan Edgar: "Ты не набрал даже 70 очков? Боюсь, Vought не может позволить себе столь ненадежный актив."',
    'Stan Edgar: "You did not even reach 70 points? I am afraid Vought cannot afford an asset this unreliable."',
  ])('keeps the threshold line visible as dialogue', (line) => {
    expect(isStanEdgarThresholdDialogue(line)).toBe(true)
  })

  it('does not mistake the personal score penalty for dialogue', () => {
    expect(isStanEdgarThresholdDialogue('Stan Edgar: -33 бонусных очков')).toBe(false)
  })

  it('moves only the private Noir line behind the complete public sequence', () => {
    const entries = [
      { id: 'personal', raw: 'Другой личный лог', isPhrase: false },
      { id: 'noir', raw: 'Stan Edgar: "У нас есть десятки способов тебя устранить, Нуар был лишь одним из них."', isPhrase: true },
      { id: 'penalty', raw: 'Stan Edgar: -33 бонусных очков', isPhrase: false },
      { id: 'threshold', raw: 'Stan Edgar: "Ты не набрал даже 70 очков? Боюсь, Vought не может позволить себе столь ненадежный актив."', isPhrase: false },
      { id: 'reply', raw: 'Homelander: "Но ведь я сверхчеловек!"', isPhrase: false },
      { id: 'product', raw: 'Stan Edgar: "Для нашей компании ты не больше чем бракованный продукт."', isPhrase: false },
      { id: 'volley', raw: 'Залп V-наводящихся ракет...', isPhrase: false },
      { id: 'global', raw: 'Другой глобальный лог', isPhrase: false },
    ]

    expect(orderStanEdgarDismissalLogs(entries).map(entry => entry.id)).toEqual([
      'personal',
      'penalty',
      'threshold',
      'reply',
      'product',
      'volley',
      'noir',
      'global',
    ])
  })

  it('does not reorder an incomplete dismissal sequence', () => {
    const entries = [
      { raw: 'Stan Edgar: "У нас есть десятки способов тебя устранить, Нуар был лишь одним из них."', isPhrase: true },
      { raw: 'Stan Edgar: "Ты не набрал даже 70 очков?"', isPhrase: false },
      { raw: 'Залп V-наводящихся ракет...', isPhrase: false },
    ]

    expect(orderStanEdgarDismissalLogs(entries)).toBe(entries)
  })
})
