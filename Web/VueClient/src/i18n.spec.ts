// @vitest-environment happy-dom

import { beforeEach, describe, expect, it } from 'vitest'
import { setLocale, translateText } from './i18n'
import phrases from '../../../King-of-the-Garbage-Hill/DataBase/phrases.en.json'

function phrasePayload(values: [string, string, string, string], textOnly = false): string {
  const bytes = new TextEncoder().encode(JSON.stringify(values))
  const binary = Array.from(bytes, byte => String.fromCharCode(byte)).join('')
  const token = btoa(binary).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_')
  return `|>Phrase${textOnly ? 'Text' : ''}V2<|${token}`
}

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

  it('finishes mixed replay logs instead of leaving Russian fragments', () => {
    expect(translateText('Последний шанс!')).toBe('Last chance!')
    expect(translateText("I'm Trying!: +20 Cкилла")).toBe("I'm Trying!: +20 Skill")
    expect(translateText('Class: +2 Cкилла')).toBe('Class: +2 Skill')
    expect(translateText('Justice: + 4!Верь в мою веру в тебя!'))
      .toBe('Justice: + 4! Believe in me believing in you!')
    expect(translateText('+4 обычных points (Victory)'))
      .toBe('+4 regular points (Victory)')
    expect(translateText('Произошел Троллинг: ВампYр Затроллился : +20.5 бонусных points'))
      .toBe('Trolled Again: Vampoor got trolled : +20.5 bonus points')
    expect(translateText('Juicy вырывает point на последних секундах!'))
      .toBe('Juicy snatches a point at the buzzer!')
  })

  it('translates the newly reported character and legacy replay logs', () => {
    expect(translateText('qqq наконец показал свою ИСТИННУЮ СИЛУ! ONE PUUUUUUNCH!!!'))
      .toBe('qqq finally unleashed their TRUE POWER! ONE PUUUUUUNCH!!!')
    expect(translateText('Рик Санчез')).toBe('Rick Sanchez')
    expect(translateText('Мишень: +16 Skill (за сильного врага)'))
      .toBe('Target: +16 Skill (for a strong enemy)')
    expect(translateText('Мишень: +20 Skill (за умного врага)'))
      .toBe('Target: +20 Skill (for a smart enemy)')
    expect(translateText('Они скинули Weedwick! Сволочи!'))
      .toBe('They threw Weedwick off the hill! Bastards!')
    expect(translateText('100 отжиманий. 100 приседаний. 100 подъёмов корпуса. 10 км бега. КАЖДЫЙ ДЕНЬ. Побочный эффект - потеря волос.'))
      .toBe('100 push-ups. 100 sit-ups. 100 squats. A 10 km run. EVERY SINGLE DAY. Side effect: hair loss.')
    expect(translateText('Unremarkable (condescending): Boring. Пойду домой.'))
      .toBe("Unremarkable (condescending): Boring. I'm going home.")
    expect(translateText('You напали на игрока Itachi')).toBe('You attacked Itachi')
    expect(translateText('Dead Broke (monster): Если никто не видел, считается ли это за подвиг?'))
      .toBe('Dead Broke (monster): If nobody saw it, does it still count as hero work?')
    expect(translateText('Чутьё: Шаринган. Не смотри в глаза. (Itachi)'))
      .toBe('Witcher senses: Sharingan. Do not meet his eyes. (Itachi)')
    expect(translateText('+2 regular points (Контракт+Контракт)'))
      .toBe('+2 regular points (Contract+Contract)')
    expect(translateText('Meditation: Закрываю глаза... Нет, запах никуда не делся.Meditation: Закрываю глаза... Нет, запах никуда не делся.'))
      .toBe("Meditation: Close my eyes... Nope, smell's still here.Meditation: Close my eyes... Nope, smell's still here.")
  })

  it('has an exact legacy-replay translation for every authored PhraseClass variant', () => {
    for (const group of Object.values(phrases)) {
      for (const pair of group.phrases) {
        const actual = translateText(`|>Phrase<|${group.passiveNameRussian}: ${pair.russian}`)
        const allowed = group.phrases
          .filter(candidate => candidate.russian === pair.russian)
          .map(candidate => `|>Phrase<|${group.passiveNameEnglish}: ${candidate.english}`)
        if (!allowed.includes(actual))
          throw new Error(JSON.stringify({ group: group.passiveNameRussian, pair, actual, allowed }))
      }
    }
  })

  it('translates the canonical formatted forms before replay markdown is rendered', () => {
    expect(translateText('*Справедливость*: ***+ 4!***<:e_:562879579694301184>Верь в мою веру в тебя!'))
      .toBe('*Justice*: ***+ 4!***<:e_:562879579694301184> Believe in me believing in you!')
    expect(translateText('+4 **обычных** очков (Победа)'))
      .toBe('+4 **regular** points (Victory)')
    expect(translateText('**Произошел Троллинг:** ВампYр Затроллился : +20.5 __**бонусных**__ очков'))
      .toBe('**Trolled Again:** Vampoor got trolled : +20.5 __**bonus**__ points')
    expect(translateText('**Juicy** вырывает **очко** на последних секундах!'))
      .toBe('**Juicy** snatches **a point** at the buzzer!')

    setLocale('ru')
    expect(translateText('Trolled Again: Vampoor got trolled : +20.5 bonus points'))
      .toBe('Произошел Троллинг: ВампYр Затроллился : +20.5 бонусных очков')
    expect(translateText("I'm Trying!: +20 Skill")).toBe('Я пытаюсь!: +20 Скилл')
  })

  it('adapts canonical character phrase records in Russian replay snapshots', () => {
    expect(translateText('|>Phrase<|Одиночество: Что делаешь?'))
      .toBe('|>Phrase<|Party of One: What are you doing?')
    expect(translateText('|>Phrase<|Доебаться: Вам ответили на письмо!'))
      .toBe("|>Phrase<|Won't Stop Messaging: You got a reply to your email!")
    expect(translateText('|>Phrase<|Неприметность (напал кто-то еще): Этот монстр пал от руки Кинга! ...наверное.'))
      .toBe('|>Phrase<|Unremarkable (someone else attacked): King defeated this monster! ...probably.')
    expect(translateText('|>Phrase<|Да всё нахуй эту игру: Нахуй эту игру'))
      .toBe('|>Phrase<|Screw This Game: Screw this game')
  })

  it('repairs phrase records partially translated by older projections', () => {
    expect(translateText('|>Phrase<|Party of One: Как дела?'))
      .toBe("|>Phrase<|Party of One: How's it going?")
  })

  it('renders the exact authored variant from bilingual live and replay records', () => {
    const awdka = phrasePayload([
      'Научите играть',
      'Два игнайта на вуконга!',
      'Teach Me to Play',
      'Two Ignites on Wukong! Peak esports.',
    ])
    const mylorik = phrasePayload([
      'Месть',
      'ВЬЕТНАМСКАЯ ТРИСТАНА!!!',
      'Vengeance',
      '**VIETNAMESE TRISTANA!!!**',
    ])

    expect(translateText(`${awdka}\n${mylorik}`)).toBe(
      '|>Phrase<|Teach Me to Play: Two Ignites on Wukong! Peak esports.\n'
      + '|>Phrase<|Vengeance: **VIETNAMESE TRISTANA!!!**',
    )

    setLocale('ru')
    expect(translateText(`${awdka}\n${mylorik}`)).toBe(
      '|>Phrase<|Научите играть: Два игнайта на вуконга!\n'
      + '|>Phrase<|Месть: ВЬЕТНАМСКАЯ ТРИСТАНА!!!',
    )
  })

  it('renders bilingual direct-message records without a log marker', () => {
    const payload = phrasePayload(['Авто Ход', 'Ты что, бот?', 'Auto Move', 'What are you, a bot?'], true)
    expect(translateText(payload)).toBe('Auto Move: What are you, a bot?')
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
