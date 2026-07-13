import { ref, watch } from 'vue'
import englishCatalog from '../../../King-of-the-Garbage-Hill/DataBase/localization.en.json'
import phrases from '../../../King-of-the-Garbage-Hill/DataBase/phrases.en.json'
import characters from '../../../King-of-the-Garbage-Hill/DataBase/characters.json'

export type AppLocale = 'ru' | 'en'

type EnglishCatalog = {
  exact: Record<string, string>
  terms: Record<string, string>
  russianExact: Record<string, string>
  phraseFallbacks: Record<string, string>
  characters: Record<string, string>
  passives: Record<string, string>
}

type PhraseGroup = {
  passiveNameRussian: string
  passiveNameEnglish: string
  phrases: Array<{ russian: string, english: string }>
}

const catalog = englishCatalog as EnglishCatalog
const phraseCatalog = phrases as Record<string, PhraseGroup>
const contentExact = new Map<string, string>()
const contentRussianExact = new Map<string, string>()
for (const character of characters) {
  const characterTranslation = catalog.characters[character.Name]
  if (characterTranslation && character.Description) {
    contentExact.set(character.Description, characterTranslation)
    contentRussianExact.set(characterTranslation, character.Description)
  }
  for (const passive of character.Passive) {
    const passiveTranslation = catalog.passives[passive.PassiveName]
    if (passiveTranslation && passive.PassiveDescription) {
      contentExact.set(passive.PassiveDescription, passiveTranslation)
      contentRussianExact.set(passiveTranslation, passive.PassiveDescription)
    }
  }
}

function addUnambiguousPhrasePairs(
  target: Map<string, string>, sourceKey: 'russian' | 'english', translationKey: 'russian' | 'english',
): void {
  const passiveNames = new Set(Object.values(phraseCatalog)
    .map(group => sourceKey === 'russian' ? group.passiveNameRussian : group.passiveNameEnglish))
  const values = new Map<string, Set<string>>()
  for (const group of Object.values(phraseCatalog)) {
    for (const pair of group.phrases) {
      const translations = values.get(pair[sourceKey]) ?? new Set<string>()
      translations.add(pair[translationKey])
      values.set(pair[sourceKey], translations)
    }
  }
  for (const [source, translations] of values) {
    if (translations.size === 1 && !passiveNames.has(source) && !target.has(source)
      && (sourceKey !== 'russian' || /[А-Яа-яЁё]/.test(source)))
      target.set(source, translations.values().next().value as string)
  }
}

addUnambiguousPhrasePairs(contentExact, 'russian', 'english')
addUnambiguousPhrasePairs(contentRussianExact, 'english', 'russian')
const localeKey = 'kotgh_locale'
const savedLocale = localStorage.getItem(localeKey)

export const currentLocale = ref<AppLocale>(savedLocale === 'ru' || savedLocale === 'en' ? savedLocale : 'en')

const cyrillicPattern = /[А-Яа-яЁё]/
const regexSpecialCharacters = /[.*+?^${}()|[\]\\]/g
const bilingualPhrasePattern = /\|>Phrase(Text)?V2<\|([A-Za-z0-9_-]+)/g

type ReplacementEntry = {
  source: string
  translation: string
  pattern: RegExp | null
}

function buildReplacementEntries(values: Record<string, string>): ReplacementEntry[] {
  return Object.entries(values)
    .sort(([a], [b]) => b.length - a.length)
    .map(([source, translation]) => ({
      source,
      translation,
      pattern: /^[\p{L}\p{N}_.-]+$/u.test(source)
        ? new RegExp(`(?<![\\p{L}\\p{N}])${source.replace(regexSpecialCharacters, '\\$&')}(?![\\p{L}\\p{N}])`, 'gu')
        : null,
    }))
}

const exactEntries = buildReplacementEntries({ ...catalog.exact, ...Object.fromEntries(contentExact) })
const russianExactEntries = buildReplacementEntries(catalog.russianExact)
const termEntries = buildReplacementEntries(catalog.terms)

const phraseRules: Array<[RegExp, string]> = [
  [/Раунд\s*#(\d+)/gi, 'Round #$1'],
  [/Раунд\s+(\d+)/gi, 'Round $1'],
  [/\+(\d+)\s+очков/gi, '+$1 points'],
  [/(\d+)\s+очков/gi, '$1 points'],
  [/(\d+)\s+очка/gi, '$1 points'],
  [/(\d+)\s+очко/gi, '$1 point'],
  [/Обменять\s+(\d+)\s+Морали\s+на\s+(\d+)\s+бонусных очков/gi, 'Trade $1 Moral for $2 bonus points'],
  [/Обменять\s+(\d+)\s+Морали\s+на\s+(\d+)\s+[CС]килла/gi, 'Trade $1 Moral for $2 Skill'],
  [/Вы не походили\. Использовался Авто Ход/gi, 'You did not act. Auto Move was used'],
  [/(?:Вы|You)\s+поставили\s+(?:блок|Block)!/gi, 'You blocked!'],
  [/(?:Вы|You)\s+поставили\s+(?:блок|Block)/gi, 'You blocked'],
  [/(?:Вы|You)\s+использовали\s+(?:Авто Ход|Auto Move)!/gi, 'You used Auto Move!'],
  [/(?:Вы|You)\s+использовали\s+(?:Авто Ход|Auto Move)/gi, 'You used Auto Move'],
  [/You напали на игрока\s+/gi, 'You attacked '],
  [/Вы напали на игрока\s+/gi, 'You attacked '],
  [/за \*\*сильного\*\* врага/gi, 'for a **strong** enemy'],
  [/за \*\*умного\*\* врага/gi, 'for a **smart** enemy'],
  [/за \*\*быстрого\*\* врага/gi, 'for a **fast** enemy'],
  [/за сильного врага/gi, 'for a strong enemy'],
  [/за умного врага/gi, 'for a smart enemy'],
  [/за быстрого врага/gi, 'for a fast enemy'],
  [/Они скинули\s+(\*\*[^*]+\*\*|[^\r\n!]+)!\s*Сволочи!/gi, 'They threw $1 off the hill! Bastards!'],
  [/Всё,\s*у меня\s+(?:горит|is burning)!/gi, "That's it, I'm absolutely tilted!"],
  [/(?:вас|you)\s+(?:обманул|outsmarted)/gi, 'outsmarted you'],
  [/(?:вас|you)\s+(?:обогнал|overtook)/gi, 'overtook you'],
  [/(?:вас|you)\s+(?:пресанул|pressured)/gi, 'pressured you'],
  [/(?:Rumbling|Рокот Земли):\s*Эрен остался на\s+(\d+)\s+месте\.\s*Между ним и Элдией никого нет\./gi,
    'Rumbling: Eren remains at place $1. No one stands between him and Eldia.'],
  [/Mitsuki\s+отнял в общей сумме\s+(\d+(?:[.,]\d+)?)\s+(?:очков|points)\./gi,
    'Mitsuki took away $1 points in total.'],
  [/(.+?) наконец показал свою ИСТИННУЮ СИЛУ! ONE PUUUUUUNCH!!!/gi, '$1 finally unleashed their TRUE POWER! ONE PUUUUUUNCH!!!'],
  [/#life:\s*Я прокачал (Интеллект|Силу|Скорость|Психику) на (-?\d+)!/gi, '#life: I upgraded $1 to $2!'],
  [/Первый ход:\s*(.+)/gi, 'First turn: $1'],
  [/Не размещено:\s*(.+)/gi, 'Not deployed: $1'],
  [/Нужно ещё\s+(\d+)\s+монет/gi, 'Need $1 more coins'],
  [/заряжается:\s*(\d+)\s+выстр\./gi, 'charges in $1 shots'],
  [/Штраф за убийство суммона в этой зоне/gi, 'Penalty for killing a summon in this region'],
  [/Заменить стандартный/gi, 'Replace standard'],
  [/Передвинуть корабль вверх/gi, 'Move ship up'],
  [/Передвинуть корабль вниз/gi, 'Move ship down'],
  [/Передвинуть корабль влево/gi, 'Move ship left'],
  [/Передвинуть корабль вправо/gi, 'Move ship right'],
  [/Направление: вверх/gi, 'Direction: up'],
  [/Направление: вниз/gi, 'Direction: down'],
  [/Направление: влево/gi, 'Direction: left'],
  [/Направление: вправо/gi, 'Direction: right'],
  [/Выберите клетку на строке 1 вражеского поля/gi, "Choose a cell in row 1 of the enemy board"],
  [/Подтвердить выбор флота/gi, 'Confirm fleet'],
  [/Подтвердить и начать бой/gi, 'Confirm and start battle'],
  [/Нет свободных слотов[^\n]*/gi, 'No free slots — remove a purchased ship'],
  [/Слишком много регионов[^\n]*/gi, 'Too many regions (max 3)'],
  [/Превышен бюджет/gi, 'Over budget'],
  [/потоплен/gi, 'sunk'],
  [/уничтожил палубу/gi, 'destroyed a deck'],
  [/разрушил модуль/gi, 'destroyed a module'],
  [/протаранил/gi, 'rammed'],
  [/захватил/gi, 'captured'],
  [/заморозил/gi, 'froze'],
  [/оглушён/gi, 'stunned'],
  [/маневрирует/gi, 'is maneuvering'],
  [/промахнулся/gi, 'missed'],
  [/увернул/gi, 'dodged'],
  [/сгорел/gi, 'burned down'],
  [/горит/gi, 'is burning'],
  [/врезался/gi, 'crashed'],
  [/взорвал/gi, 'blew up'],
  [/поцарапал/gi, 'scratched'],
  [/опустошил/gi, 'ravaged'],
  [/обогнал/gi, 'overtook'],
  [/обманул/gi, 'outsmarted'],
  [/пресанул/gi, 'pressured'],
  [/Победа:\s*/gi, 'Victory: '],
  [/Поражение:\s*/gi, 'Defeat: '],
  [/Вы улучшили\s+(Интеллект|Силу|Скорость|Психику)\s+до\s+(-?\d+)/gi, 'You upgraded $1 to $2'],
  [/Вам понерфали\s+(Интеллект|Силу|Скорость|Психику)\s+до\s+(-?\d+)/gi, 'Your $1 was nerfed to $2'],
  [/Получено вреда:\s*(\d+)/gi, 'Harm taken: $1'],
  [/Команда\s+#(\d+)\s+победила набрав\s+(-?\d+)\s+(?:Очков|points)/gi, 'Team #$1 won with $2 points'],
  [/Команда\s+#(\d+)\s+Набрала\s+(-?\d+)\s+(?:Очков|points)/gi, 'Team #$1 scored $2 points'],
  [/Шэн:\s*Перепрыгнул\s+(.+?)\.\s*Зарядов осталось:\s*(\d+)/gi, 'Shen: jumped over $1. Charges left: $2'],
  [/Шэн:\s*Атака на\s+(.+?)\.\s*Цель уже позади, заряд потрачен\.\s*Осталось:\s*(\d+)/gi, 'Shen: attacked $1. The target was already behind you; charge spent. Left: $2'],
  [/Шэн:\s*Зиккурат перекрыл прыжок через\s+(.+?)\.\s*Заряд потрачен/gi, 'Shen: a Ziggurat blocked the jump over $1. Charge spent'],
  [/Временная капсула:\s*Кола найдена в переписанной истории!/gi, 'Time Capsule: cola recovered from rewritten history!'],
  [/Великий летописец:\s*История раунда\s+(\d+)\s+переписана!\s*Украдено\s+(-?\d+)\s+(?:очков|points)/gi, 'Great Chronicler: round $1 was rewritten! Stole $2 points'],
  [/Salldorum:\s*Помните\s+(\d+)\s+ход\?\s*На самом деле в этот день пришло подкрепление из Киева и мы всех победили!/gi, 'Salldorum: Remember turn $1? Reinforcements from Kyiv actually arrived that day, and we defeated everyone!'],
  [/Salldorum:\s*А вы знали, что в\s+(\d+)\s+ход на самом деле мы подписали мирный договор и этих поражений не было/gi, 'Salldorum: Did you know that on turn $1 we actually signed a peace treaty, so those defeats never happened'],
  [/Заказ Француза:\s*Новая цель\s*[—-]\s*(.+?)\.\s*3 хода/gi, "Frenchie's contract: new target — $1. 3 turns"],
  [/Тактика выбрана:\s*/gi, 'Strategy selected: '],
  [/Ты стал пешкой Йохана/gi, "You became Johan's pawn"],
  [/Штормяк провоцирует вас!\s*Атакуйте\s+/gi, 'Stormy taunts you! Attack '],
  [/Штормяк провоцирует\s+/gi, 'Stormy taunts '],
  [/Тетрадь смерти:\s*Ты записал имя\s+/gi, 'Death Note: you wrote the name '],
  [/Глаза бога смерти:\s*Активированы!\s*Следующая атака раскроет имя врага/gi, "Shinigami Eyes activated! Your next attack will reveal the enemy's name"],
]

function replaceAllLiteral(value: string, search: string, replacement: string): string {
  return value.split(search).join(replacement)
}

function replaceCatalogEntries(value: string, entries: ReplacementEntry[]): string {
  let translated = value
  for (const entry of entries)
    translated = entry.pattern
      ? translated.replace(entry.pattern, entry.translation)
      : replaceAllLiteral(translated, entry.source, entry.translation)
  return translated
}

function phraseFallback(passiveName: string): string {
  const direct = catalog.phraseFallbacks[passiveName]
  if (direct) return direct

  const canonicalName = Object.entries(catalog.terms)
    .find(([, englishName]) => englishName === passiveName)?.[0]
  return canonicalName ? (catalog.phraseFallbacks[canonicalName] ?? 'Ability triggered.') : 'Ability triggered.'
}

function authoredLegacyPhrase(passiveName: string, phrase: string): string | null {
  const groups = Object.values(phraseCatalog).filter(candidate =>
    candidate.passiveNameRussian === passiveName || candidate.passiveNameEnglish === passiveName)
  if (groups.length === 0) return null
  if (groups.flatMap(group => group.phrases).some(pair => phrase.startsWith(pair.english))) return null

  let translated = phrase
  let changed = false
  const pairs = groups.flatMap(group => group.phrases).sort((a, b) => b.russian.length - a.russian.length)
  for (const pair of pairs) {
    if (!translated.includes(pair.russian)) continue
    translated = replaceAllLiteral(translated, pair.russian, pair.english)
    changed = true
  }
  return changed ? translated : null
}

function resolveAuthoredLegacyMarkers(value: string): string {
  const header = /\|>Phrase<\|([^:\r\n]+):\s*/g
  let translated = ''
  let cursor = 0
  let match = header.exec(value)
  while (match) {
    translated += value.slice(cursor, match.index)
    const bodyStart = header.lastIndex
    const matched = Object.values(phraseCatalog)
      .filter(candidate => candidate.passiveNameRussian === match![1]
        || candidate.passiveNameEnglish === match![1])
      .flatMap(group => group.phrases.map(pair => ({ group, pair })))
      .sort((a, b) => b.pair.russian.length - a.pair.russian.length)
      .find(candidate => value.startsWith(candidate.pair.russian, bodyStart))
    if (!matched) {
      translated += match[0]
      cursor = bodyStart
    } else {
      translated += `|>Phrase<|${matched.group.passiveNameEnglish}: ${matched.pair.english}`
      cursor = bodyStart + matched.pair.russian.length
      header.lastIndex = cursor
    }
    match = header.exec(value)
  }
  return translated + value.slice(cursor)
}

function decodeBilingualPhrase(token: string, textOnly: boolean): string {
  try {
    const base64 = token.replace(/-/g, '+').replace(/_/g, '/')
      .padEnd(token.length + (4 - token.length % 4) % 4, '=')
    const bytes = Uint8Array.from(atob(base64), char => char.charCodeAt(0))
    const values = JSON.parse(new TextDecoder().decode(bytes)) as [string, string, string, string]
    const english = currentLocale.value === 'en'
    const name = english ? values[2] : values[0]
    const phrase = english ? values[3] : values[1]
    return `${textOnly ? '' : '|>Phrase<|'}${name}: ${phrase}`
  } catch (error) {
    console.warn('[i18n] Invalid bilingual phrase payload:', error)
    return textOnly ? 'Ability: Ability triggered.' : '|>Phrase<|Ability: Ability triggered.'
  }
}

function translate(value: string | null | undefined, translatePhraseMarkers: boolean): string {
  if (!value) return ''

  // Keep authored phrase variants opaque while ordinary surrounding logs are translated. This
  // prevents deliberate English words in a Russian meme (or vice versa) from being rewritten.
  const protectedPhrases: string[] = []
  const protectedValue = value.replace(bilingualPhrasePattern, (_match, textOnly: string | undefined, token: string) => {
    const index = protectedPhrases.push(decodeBilingualPhrase(token, Boolean(textOnly))) - 1
    return `\uE000${index}\uE001`
  })
  const translated = translateCore(protectedValue, translatePhraseMarkers)
  return translated.replace(/\uE000(\d+)\uE001/g, (_match, index: string) => protectedPhrases[Number(index)] ?? '')
}

function translateCore(value: string, translatePhraseMarkers: boolean): string {
  if (currentLocale.value === 'en' && !cyrillicPattern.test(value) && !value.includes('|>Phrase<|')) return value
  if (currentLocale.value === 'ru' && !/[A-Za-z]/.test(value)) return value

  if (translatePhraseMarkers && currentLocale.value === 'en' && value.includes('|>Phrase<|')) {
    value = resolveAuthoredLegacyMarkers(value)
    value = value.replace(/\|>Phrase<\|([^:\r\n]+):\s*([^\r\n]*)/g, (_match, passiveName: string, phrase: string) => {
      const translatedPhrase = authoredLegacyPhrase(passiveName, phrase) ?? translate(phrase, false)
      const adaptedPhrase = cyrillicPattern.test(translatedPhrase)
        ? phraseFallback(passiveName)
        : translatedPhrase
      return `|>Phrase<|${translate(passiveName, false)}: ${adaptedPhrase}`
    })
  }

  const leading = value.match(/^\s*/)?.[0] ?? ''
  const trailing = value.match(/\s*$/)?.[0] ?? ''
  const core = value.slice(leading.length, value.length - trailing.length)
  if (currentLocale.value === 'ru') {
    const russian = catalog.russianExact[core]
    const russianContent = contentRussianExact.get(core)
    if (russian || russianContent)
      return `${leading}${russian ?? russianContent}${trailing}`
    return replaceCatalogEntries(value, russianExactEntries)
  }
  const exact = catalog.exact[core]
  if (exact) return `${leading}${exact}${trailing}`
  const content = contentExact.get(core)
  if (content) return `${leading}${content}${trailing}`

  let translated = value
  for (const [pattern, replacement] of phraseRules)
    translated = translated.replace(pattern, replacement)
  translated = replaceCatalogEntries(translated, exactEntries)
  translated = replaceCatalogEntries(translated, termEntries)

  return translated
}

/** Translate canonical, player-facing Russian text without ever changing action values or state keys. */
export function translateText(value: string | null | undefined): string {
  return translate(value, true)
}

export function setLocale(nextLocale: AppLocale): void {
  currentLocale.value = nextLocale
  localStorage.setItem(localeKey, nextLocale)
  document.documentElement.lang = nextLocale
}

type OriginalAttributes = Map<string, string>

const originalText = new WeakMap<Text, string>()
const renderedText = new WeakMap<Text, string>()
const originalAttributes = new WeakMap<Element, OriginalAttributes>()
const renderedAttributes = new WeakMap<Element, Map<string, string>>()
const localizedAttributes = ['title', 'placeholder', 'aria-label', 'alt']

function localizeTextNode(node: Text): void {
  const rendered = renderedText.get(node)
  if (rendered !== node.data || !originalText.has(node))
    originalText.set(node, node.data)

  const source = originalText.get(node) ?? node.data
  const next = translateText(source)
  if (node.data !== next) node.data = next
  renderedText.set(node, next)

  if (import.meta.env.DEV && currentLocale.value === 'en' && cyrillicPattern.test(next))
    console.warn('[i18n] Untranslated text:', next.trim())
}

function localizeElementAttributes(element: Element): void {
  let originals = originalAttributes.get(element)
  let rendered = renderedAttributes.get(element)
  if (!originals) {
    originals = new Map<string, string>()
    originalAttributes.set(element, originals)
  }
  if (!rendered) {
    rendered = new Map<string, string>()
    renderedAttributes.set(element, rendered)
  }

  for (const attribute of localizedAttributes) {
    if (!element.hasAttribute(attribute)) continue
    const current = element.getAttribute(attribute) ?? ''
    if (rendered.get(attribute) !== current || !originals.has(attribute))
      originals.set(attribute, current)
    const source = originals.get(attribute) ?? current
    const next = translateText(source)
    if (current !== next) element.setAttribute(attribute, next)
    rendered.set(attribute, next)
  }
}

function localizeTree(root: Node): void {
  if (root instanceof Text) {
    localizeTextNode(root)
    return
  }
  if (!(root instanceof Element)) return
  localizeElementAttributes(root)
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT)
  let node = walker.nextNode()
  while (node) {
    if (node instanceof Text) localizeTextNode(node)
    else if (node instanceof Element) localizeElementAttributes(node)
    node = walker.nextNode()
  }
}

/**
 * Localizes Vue-rendered text nodes at the DOM boundary. Vue keeps canonical values in memory,
 * so select/button values and Russian passive dispatch identifiers are never modified.
 */
export function installDomLocalization(root: Element): () => void {
  document.documentElement.lang = currentLocale.value
  localizeTree(root)

  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      if (mutation.type === 'characterData' && mutation.target instanceof Text)
        localizeTextNode(mutation.target)
      else if (mutation.type === 'attributes' && mutation.target instanceof Element)
        localizeElementAttributes(mutation.target)
      else
        mutation.addedNodes.forEach(localizeTree)
    }
  })
  observer.observe(root, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: localizedAttributes })

  const stopWatch = watch(currentLocale, () => localizeTree(root), { flush: 'post' })
  return () => {
    stopWatch()
    observer.disconnect()
  }
}

setLocale(currentLocale.value)
