import { ref, watch } from 'vue'
import englishCatalog from '../../../King-of-the-Garbage-Hill/DataBase/localization.en.json'
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

const catalog = englishCatalog as EnglishCatalog
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
  [/Шэн:\s*Активирован на позицию\s+(\d+)\.\s*Зарядов:\s*(\d+)/gi, 'Shen: activated at position $1. Charges: $2'],
  [/Шэн:\s*Деактивирован\.\s*Заряд возвращён/gi, 'Shen: deactivated. Charge refunded'],
  [/Великий летописец:\s*История раунда\s+(\d+)\s+переписана!\s*Украдено\s+(-?\d+)\s+(?:очков|points)/gi, 'Great Chronicler: round $1 was rewritten! Stole $2 points'],
  [/Salldorum переписал историю раунда\s+(\d+)/gi, 'Salldorum rewrote round $1'],
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
  if (currentLocale.value === 'en' && !cyrillicPattern.test(value)) return value
  if (currentLocale.value === 'ru' && !/[A-Za-z]/.test(value)) return value

  if (translatePhraseMarkers && currentLocale.value === 'en' && value.includes('|>Phrase<|')) {
    value = value.replace(/\|>Phrase<\|([^:\r\n]+):\s*([^\r\n]*)/g, (_match, passiveName: string, phrase: string) => {
      const translatedPhrase = translate(phrase, false)
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
