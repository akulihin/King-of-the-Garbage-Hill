import { ref, watch } from 'vue'
import englishCatalog from '../../../King-of-the-Garbage-Hill/DataBase/localization.en.json'
import characters from '../../../King-of-the-Garbage-Hill/DataBase/characters.json'

export type AppLocale = 'ru' | 'en'

type EnglishCatalog = {
  exact: Record<string, string>
  terms: Record<string, string>
  russianExact: Record<string, string>
  characters: Record<string, string>
  passives: Record<string, string>
}

const catalog = englishCatalog as EnglishCatalog
const contentExact = new Map<string, string>()
for (const character of characters) {
  const characterTranslation = catalog.characters[character.Name]
  if (characterTranslation && character.Description)
    contentExact.set(character.Description, characterTranslation)
  for (const passive of character.Passive) {
    const passiveTranslation = catalog.passives[passive.PassiveName]
    if (passiveTranslation && passive.PassiveDescription)
      contentExact.set(passive.PassiveDescription, passiveTranslation)
  }
}
const localeKey = 'kotgh_locale'
const savedLocale = localStorage.getItem(localeKey)

export const currentLocale = ref<AppLocale>(savedLocale === 'ru' || savedLocale === 'en' ? savedLocale : 'en')

const cyrillicPattern = /[А-Яа-яЁё]/
const regexSpecialCharacters = /[.*+?^${}()|[\]\\]/g
const termEntries = Object.entries(catalog.terms)
  .sort(([a], [b]) => b.length - a.length)
  .map(([source, translation]) => ({
    source,
    translation,
    pattern: /^[\p{L}\p{N}_.-]+$/u.test(source)
      ? new RegExp(`(?<![\\p{L}\\p{N}])${source.replace(regexSpecialCharacters, '\\$&')}(?![\\p{L}\\p{N}])`, 'gu')
      : null,
  }))

const phraseRules: Array<[RegExp, string]> = [
  [/Раунд\s*#(\d+)/gi, 'Round #$1'],
  [/Раунд\s+(\d+)/gi, 'Round $1'],
  [/\+(\d+)\s+очков/gi, '+$1 points'],
  [/(\d+)\s+очков/gi, '$1 points'],
  [/(\d+)\s+очка/gi, '$1 points'],
  [/(\d+)\s+очко/gi, '$1 point'],
  [/Обменять\s+(\d+)\s+Морали\s+на\s+(\d+)\s+бонусных очков/gi, 'Trade $1 Moral for $2 bonus points'],
  [/Обменять\s+(\d+)\s+Морали\s+на\s+(\d+)\s+[CС]килла/gi, 'Trade $1 Moral for $2 Skill'],
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
]

function replaceAllLiteral(value: string, search: string, replacement: string): string {
  return value.split(search).join(replacement)
}

/** Translate canonical, player-facing Russian text without ever changing action values or state keys. */
export function translateText(value: string | null | undefined): string {
  if (!value) return ''

  const leading = value.match(/^\s*/)?.[0] ?? ''
  const trailing = value.match(/\s*$/)?.[0] ?? ''
  const core = value.slice(leading.length, value.length - trailing.length)
  if (currentLocale.value === 'ru') {
    const russian = catalog.russianExact[core]
    return russian ? `${leading}${russian}${trailing}` : value
  }
  const exact = catalog.exact[core]
  if (exact) return `${leading}${exact}${trailing}`
  const content = contentExact.get(core)
  if (content) return `${leading}${content}${trailing}`

  let translated = value
  for (const [pattern, replacement] of phraseRules)
    translated = translated.replace(pattern, replacement)
  for (const term of termEntries)
    translated = term.pattern
      ? translated.replace(term.pattern, term.translation)
      : replaceAllLiteral(translated, term.source, term.translation)

  return translated
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
