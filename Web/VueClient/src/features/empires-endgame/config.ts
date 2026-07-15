import type { EmpiresEndgameConfig } from './types'
import { validateEmpiresEndgameConfig } from './engine'

export const EMPIRES_CONFIG_URL = '/empires-endgame/game-config.json'
export const EMPIRES_CONFIG_STORAGE_KEY = 'empires-endgame:config:v1'

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

export function validateEmpiresConfig(value: unknown): asserts value is EmpiresEndgameConfig {
  if (!isRecord(value)) throw new Error('Конфигурация должна быть JSON-объектом.')
  if (value.schemaVersion !== 1) throw new Error('Поддерживается только schemaVersion 1.')
  if (typeof value.id !== 'string' || !value.id.trim()) throw new Error('У конфигурации отсутствует id.')
  if (typeof value.title !== 'string' || !value.title.trim()) throw new Error('У конфигурации отсутствует title.')
  if (!Array.isArray(value.cards)) throw new Error('Поле cards должно быть массивом.')
  if (value.cards.length !== 53) throw new Error(`В колоде должно быть 53 карты, сейчас: ${value.cards.length}.`)

  const cardIds = new Set<string>()
  for (const rawCard of value.cards) {
    if (!isRecord(rawCard) || typeof rawCard.id !== 'string' || !rawCard.id) {
      throw new Error('Каждой карте нужен непустой id.')
    }
    if (cardIds.has(rawCard.id)) throw new Error(`Повторяется id карты: ${rawCard.id}.`)
    cardIds.add(rawCard.id)
    if (!isRecord(rawCard.normal) || !isRecord(rawCard.inverted)) {
      throw new Error(`У карты ${rawCard.id} должны быть normal и inverted стороны.`)
    }
  }

  if (!isRecord(value.durak)) throw new Error('Отсутствуют настройки карточной партии.')
  if (!isRecord(value.upgrades)) throw new Error('Отсутствуют настройки улучшений.')
  if (!isRecord(value.gifts) || !Array.isArray(value.gifts.definitions)) {
    throw new Error('Отсутствует каталог божественных даров.')
  }
  if (!isRecord(value.empire)) throw new Error('Отсутствуют настройки имперской фазы.')
  if (!Array.isArray(value.empire.cities) || value.empire.cities.length === 0) {
    throw new Error('Нужен хотя бы один город.')
  }
  if (!isRecord(value.empire.map) || !Array.isArray(value.empire.map.regions) || value.empire.map.regions.length !== 5) {
    throw new Error('На карте должно быть ровно пять регионов.')
  }
  if (!Array.isArray(value.empire.buildings) || !Array.isArray(value.empire.technologies)) {
    throw new Error('Каталоги зданий и технологий должны быть массивами.')
  }

  const engineErrors = validateEmpiresEndgameConfig(value as unknown as EmpiresEndgameConfig)
  if (engineErrors.length > 0) throw new Error(engineErrors.join('\n'))
}

export function cloneEmpiresConfig(config: EmpiresEndgameConfig): EmpiresEndgameConfig {
  // Configs are JSON data, but Vue passes this helper reactive Proxies from the
  // page and Builder props. Browsers reject Proxy objects in structuredClone.
  return JSON.parse(JSON.stringify(config)) as EmpiresEndgameConfig
}

export function parseEmpiresConfig(text: string): EmpiresEndgameConfig {
  const value: unknown = JSON.parse(text)
  validateEmpiresConfig(value)
  return value
}

export async function loadBundledEmpiresConfig(): Promise<EmpiresEndgameConfig> {
  const response = await fetch(EMPIRES_CONFIG_URL, { cache: 'no-cache' })
  if (!response.ok) throw new Error(`Не удалось загрузить игру: HTTP ${response.status}.`)
  const value: unknown = await response.json()
  validateEmpiresConfig(value)
  return value
}

export async function loadEmpiresConfig(): Promise<EmpiresEndgameConfig> {
  const custom = window.localStorage.getItem(EMPIRES_CONFIG_STORAGE_KEY)
  if (custom) {
    try {
      return parseEmpiresConfig(custom)
    }
    catch (error) {
      console.warn('Empire\'s Endgame custom configuration is invalid; loading bundled defaults.', error)
    }
  }

  return loadBundledEmpiresConfig()
}

export function saveEmpiresConfig(config: EmpiresEndgameConfig) {
  validateEmpiresConfig(config)
  window.localStorage.setItem(EMPIRES_CONFIG_STORAGE_KEY, JSON.stringify(config))
}

export function clearCustomEmpiresConfig() {
  window.localStorage.removeItem(EMPIRES_CONFIG_STORAGE_KEY)
}

export function downloadEmpiresJson(filename: string, value: unknown) {
  const blob = new Blob([`${JSON.stringify(value, null, 2)}\n`], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.click()
  URL.revokeObjectURL(url)
}

export async function readEmpiresJsonFile(file: File): Promise<EmpiresEndgameConfig> {
  return parseEmpiresConfig(await file.text())
}
