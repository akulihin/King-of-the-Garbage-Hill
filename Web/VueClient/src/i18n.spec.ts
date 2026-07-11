// @vitest-environment happy-dom

import { beforeEach, describe, expect, it } from 'vitest'
import { setLocale, translateText } from './i18n'

describe('English presentation localization', () => {
  beforeEach(() => setLocale('en'))

  it('translates the reported system and level-up log templates', () => {
    expect(translateText('Вы не походили. Использовался Авто Ход'))
      .toBe('You did not act. Auto Move was used')
    expect(translateText('#life: Я прокачал Психику на 8!'))
      .toBe('#life: I upgraded Psyche to 8!')
    expect(translateText('Мало морали')).toBe('Not enough Moral')
    expect(translateText('Предположение')).toBe('Prediction')
    expect(translateText('Вечное Цукуеми: mylorik выиграл свой бой.'))
      .toBe('Infinite Tsukuyomi: mylorik won their fight.')
    expect(translateText('mylorik победил, играя за Darksci'))
      .toBe('mylorik won as Darksci')
  })

  it('adapts canonical character phrase records in Russian replay snapshots', () => {
    expect(translateText('|>Phrase<|Одиночество: Что делаешь?'))
      .toBe('|>Phrase<|Party of One: Look at all my friends not answering.')
    expect(translateText('|>Phrase<|Доебаться: Вам ответили на письмо!'))
      .toBe("|>Phrase<|Won't Stop Messaging: You have 37 unread messages.")
    expect(translateText('|>Phrase<|Лысина: Этот монстр пал от руки Кинга! ...наверное.'))
      .toBe('|>Phrase<|Baldness: Serious Series: one very ordinary punch.')
    expect(translateText('|>Phrase<|Да всё нахуй эту игру: Нахуй эту игру'))
      .toBe('|>Phrase<|Screw This Game: Screw this game.')
  })

  it('repairs phrase records partially translated by older projections', () => {
    expect(translateText('|>Phrase<|Party of One: Как дела?'))
      .toBe('|>Phrase<|Party of One: Look at all my friends not answering.')
  })

  it('translates class labels and their split tooltip fragments', () => {
    expect(translateText('Сильный')).toBe('Strong')
    expect(translateText('Быстрый')).toBe('Fast')
    expect(translateText('побеждает!')).toBe('wins fights!')
  })

  it('translates a complete passive description before markdown rendering', () => {
    const russian = 'Удваивает текущие очки после состоявшегося боя с последним нетронутым ранее врагом (<:luck:1051721236322988092>). Получает 2 Психики и 2 *Морали*.'
    const english = 'After a real fight with the final untouched enemy (<:luck:1051721236322988092>), doubles his current score and gains 2 Psyche and 2 *Moral*.'

    expect(translateText(russian)).toBe(english)
    setLocale('ru')
    expect(translateText(english)).toBe(russian)
  })
})
