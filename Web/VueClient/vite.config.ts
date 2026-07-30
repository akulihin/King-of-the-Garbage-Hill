/// <reference types="vitest" />

import { URL, fileURLToPath } from 'node:url'
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import vue from '@vitejs/plugin-vue'
import analyzer from 'rollup-plugin-analyzer'
import { defineConfig, type Plugin } from 'vite'

const publicLocalizationId = 'virtual:public-localization'
const resolvedPublicLocalizationId = `\0${publicLocalizationId}`
const messageCatalogId = 'virtual:message-catalogs'
const resolvedMessageCatalogId = `\0${messageCatalogId}`
const messageCatalogRoot = resolve(fileURLToPath(new URL('../../Localization/', import.meta.url)))

type CharacterSource = {
  Name: string
  Tier: number
  BrowserCatalog?: boolean
  Description?: string
  StoryAgent?: string
  Passive?: Array<{ PassiveName: string; PassiveDescription?: string }>
}

const localizationSections = [
  'exact',
  'terms',
  'russianExact',
  'phraseFallbacks',
  'characters',
  'passives',
] as const

type LocalizationSection = (typeof localizationSections)[number]

type LocalizationCatalog = {
  exact: Record<string, string>
  terms: Record<string, string>
  russianExact: Record<string, string>
  phraseFallbacks: Record<string, string>
  characters: Record<string, string>
  passives: Record<string, string>
  browserPrivate?: Partial<Record<LocalizationSection, string[]>>
}

type MessageSource = {
  visibility: 'public' | 'owner' | 'server'
  ru: string
  en: string
}

type ProductMessageCatalog = {
  product: string
  messages: Record<string, MessageSource>
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

function containsPrivateContent(value: string, privateTerms: string[]): boolean {
  return privateTerms.some((term) => {
    const normalized = term.trim()
    if (!normalized) return false
    if (normalized.length >= 12 || /\s/u.test(normalized)) {
      return value.toLowerCase().includes(normalized.toLowerCase())
    }
    return new RegExp(`(^|[^\\p{L}\\p{N}_])${escapeRegExp(normalized)}(?=$|[^\\p{L}\\p{N}_])`, 'iu').test(value)
  })
}

function sanitizeRecord(
  values: Record<string, string>,
  privateTerms: string[],
  privateKeys: string[] = [],
): Record<string, string> {
  const privateKeySet = new Set(privateKeys)
  return Object.fromEntries(Object.entries(values)
    .filter(([key, value]) => !privateKeySet.has(key)
      && !containsPrivateContent(key, privateTerms)
      && !containsPrivateContent(value, privateTerms)))
}

function messagePlaceholders(value: string): string[] {
  return [...value.matchAll(/\{([A-Za-z][A-Za-z0-9_.-]*)\}/g)]
    .map(match => match[1])
    .sort()
}

/** JSON.parse keeps only the final value for a repeated object key. Walk the
 * source first so catalog validation can reject duplicates instead of losing
 * them before the ordinary schema checks run. */
function duplicateJsonProperties(source: string): string[] {
  const duplicates: string[] = []
  let offset = 0

  const skipWhitespace = () => {
    while (offset < source.length && /\s/.test(source[offset])) offset += 1
  }
  const readString = (): string => {
    const start = offset
    offset += 1
    let escaped = false
    while (offset < source.length) {
      const character = source[offset]
      offset += 1
      if (!escaped && character === '"')
        return JSON.parse(source.slice(start, offset)) as string
      if (!escaped && character === '\\') escaped = true
      else escaped = false
    }
    throw new SyntaxError('Unterminated JSON string.')
  }

  const readValue = (path: string): void => {
    skipWhitespace()
    if (source[offset] === '{') {
      readObject(path)
      return
    }
    if (source[offset] === '[') {
      readArray(path)
      return
    }
    if (source[offset] === '"') {
      readString()
      return
    }

    const start = offset
    while (offset < source.length && !/[\s,\]}]/.test(source[offset])) offset += 1
    if (start === offset) throw new SyntaxError(`Expected a JSON value at offset ${offset}.`)
    JSON.parse(source.slice(start, offset))
  }

  const readObject = (path: string): void => {
    offset += 1
    skipWhitespace()
    const names = new Set<string>()
    if (source[offset] === '}') {
      offset += 1
      return
    }

    while (offset < source.length) {
      skipWhitespace()
      if (source[offset] !== '"')
        throw new SyntaxError(`Expected a JSON property at offset ${offset}.`)
      const name = readString()
      const propertyPath = `${path}.${name}`
      if (names.has(name)) duplicates.push(propertyPath)
      names.add(name)
      skipWhitespace()
      if (source[offset] !== ':')
        throw new SyntaxError(`Expected ":" at offset ${offset}.`)
      offset += 1
      readValue(propertyPath)
      skipWhitespace()
      if (source[offset] === '}') {
        offset += 1
        return
      }
      if (source[offset] !== ',')
        throw new SyntaxError(`Expected "," at offset ${offset}.`)
      offset += 1
    }
    throw new SyntaxError('Unterminated JSON object.')
  }

  const readArray = (path: string): void => {
    offset += 1
    skipWhitespace()
    if (source[offset] === ']') {
      offset += 1
      return
    }

    let index = 0
    while (offset < source.length) {
      readValue(`${path}[${index}]`)
      index += 1
      skipWhitespace()
      if (source[offset] === ']') {
        offset += 1
        return
      }
      if (source[offset] !== ',')
        throw new SyntaxError(`Expected "," at offset ${offset}.`)
      offset += 1
    }
    throw new SyntaxError('Unterminated JSON array.')
  }

  readValue('$')
  skipWhitespace()
  if (offset !== source.length)
    throw new SyntaxError(`Unexpected JSON content at offset ${offset}.`)
  return duplicates
}

/**
 * Publishes only explicitly public entries from the shared structured catalogs.
 * Owner/server messages remain on the server; unlike the legacy catalog this does
 * not infer privacy from the message contents.
 */
function messageCatalogPlugin(): Plugin {
  return {
    name: 'kotgh-structured-message-catalogs',
    configureServer(server) {
      const knownCatalogFiles = new Set(
        readdirSync(messageCatalogRoot)
          .filter(file => file.endsWith('.messages.json'))
          .map(file => resolve(messageCatalogRoot, file)),
      )

      const refreshCatalogs = (file: string, event: 'add' | 'change' | 'unlink'): void => {
        const catalogPath = resolve(file)
        if (dirname(catalogPath) !== messageCatalogRoot || !catalogPath.endsWith('.messages.json'))
          return

        if (event === 'add') {
          if (knownCatalogFiles.has(catalogPath))
            return
          knownCatalogFiles.add(catalogPath)
        }
        else if (event === 'unlink') {
          if (!knownCatalogFiles.delete(catalogPath))
            return
        }

        const catalogModule = server.moduleGraph.getModuleById(resolvedMessageCatalogId)
        if (catalogModule)
          server.moduleGraph.invalidateModule(catalogModule)
        server.ws.send({ type: 'full-reload' })
      }

      server.watcher.add(messageCatalogRoot)
      server.watcher.on('add', file => refreshCatalogs(file, 'add'))
      server.watcher.on('change', file => refreshCatalogs(file, 'change'))
      server.watcher.on('unlink', file => refreshCatalogs(file, 'unlink'))
    },
    resolveId(id) {
      return id === messageCatalogId ? resolvedMessageCatalogId : null
    },
    load(id) {
      if (id !== resolvedMessageCatalogId) return null

      const files = readdirSync(messageCatalogRoot)
        .filter(file => file.endsWith('.messages.json'))
        .sort()
      const publicMessages: Record<string, Pick<MessageSource, 'ru' | 'en'>> = {}
      const seenMessageKeys = new Set<string>()
      const errors: string[] = []

      for (const file of files) {
        const catalogPath = join(messageCatalogRoot, file)
        this.addWatchFile(catalogPath)
        const json = readFileSync(catalogPath, 'utf8')
        for (const path of duplicateJsonProperties(json))
          errors.push(`${file}: duplicate JSON property "${path}"`)
        const catalog = JSON.parse(json) as ProductMessageCatalog
        if (!catalog.product || !catalog.messages) {
          errors.push(`${file}: product and messages are required`)
          continue
        }

        for (const [key, message] of Object.entries(catalog.messages)) {
          if (!key.startsWith(`${catalog.product}.`))
            errors.push(`${file}: ${key} must start with "${catalog.product}."`)
          if (!['public', 'owner', 'server'].includes(message.visibility))
            errors.push(`${file}: ${key} has invalid visibility "${message.visibility}"`)
          if (!message.ru?.trim() || !message.en?.trim())
            errors.push(`${file}: ${key} requires both ru and en`)
          if (messagePlaceholders(message.ru).join('\0') !== messagePlaceholders(message.en).join('\0'))
            errors.push(`${file}: ${key} has different ru/en placeholders`)
          if (seenMessageKeys.has(key))
            errors.push(`${file}: duplicate key ${key}`)
          seenMessageKeys.add(key)
          if (message.visibility === 'public')
            publicMessages[key] = { ru: message.ru, en: message.en }
        }
      }

      if (errors.length > 0)
        throw new Error(`Invalid structured localization catalog:\n- ${errors.join('\n- ')}`)

      return `export const publicMessages = Object.freeze(${JSON.stringify(publicMessages)});`
    },
  }
}

/**
 * Builds the browser localization payload at Vite build time. Definitions explicitly
 * marked BrowserCatalog=false never enter the virtual module, while ordinary secret and
 * transform definitions retain their existing localization behavior.
 */
function publicLocalizationPlugin(): Plugin {
  return {
    name: 'kotgh-public-localization',
    resolveId(id) {
      return id === publicLocalizationId ? resolvedPublicLocalizationId : null
    },
    load(id) {
      if (id !== resolvedPublicLocalizationId) return null

      const dataRoot = fileURLToPath(new URL('../../King-of-the-Garbage-Hill/DataBase/', import.meta.url))
      const characters = JSON.parse(readFileSync(`${dataRoot}characters.json`, 'utf8')) as CharacterSource[]
      const catalog = JSON.parse(readFileSync(`${dataRoot}localization.en.json`, 'utf8')) as LocalizationCatalog
      const phrases = JSON.parse(readFileSync(`${dataRoot}phrases.en.json`, 'utf8')) as Record<string, {
        passiveNameRussian: string
        passiveNameEnglish: string
        phrases: Array<{ russian: string; english: string }>
      }>

      const publicCharacters = characters.filter(character => character.BrowserCatalog !== false)
      const privateCharacters = characters.filter(character => character.BrowserCatalog === false)
      const publicNames = new Set(publicCharacters.map(character => character.Name))
      const publicPassiveNames = new Set(publicCharacters.flatMap(character =>
        (character.Passive ?? []).map(passive => passive.PassiveName)))
      const privateTerms = privateCharacters.flatMap((character) => [
        character.Name,
        character.Description ?? '',
        character.StoryAgent ?? '',
        catalog.characters[character.Name] ?? '',
        ...(character.Passive ?? []).flatMap(passive => [
          passive.PassiveName,
          passive.PassiveDescription ?? '',
          catalog.passives[passive.PassiveName] ?? '',
        ]),
      ]).filter(Boolean)
      for (const section of Object.keys(catalog.browserPrivate ?? {})) {
        if (!localizationSections.includes(section as LocalizationSection))
          throw new Error(`Unsupported browser-private localization section "${section}".`)
      }
      for (const section of localizationSections) {
        const seenKeys = new Set<string>()
        for (const key of catalog.browserPrivate?.[section] ?? []) {
          if (seenKeys.has(key))
            throw new Error(`Duplicate browser-private localization key "${key}" in ${section}.`)
          seenKeys.add(key)
          if (!(key in catalog[section]))
            throw new Error(`Browser-private localization key "${key}" is missing from ${section}.`)
        }
      }

      const publicCatalog: LocalizationCatalog = {
        exact: sanitizeRecord(catalog.exact, privateTerms, catalog.browserPrivate?.exact),
        terms: sanitizeRecord(catalog.terms, privateTerms, catalog.browserPrivate?.terms),
        russianExact: sanitizeRecord(catalog.russianExact, privateTerms, catalog.browserPrivate?.russianExact),
        phraseFallbacks: sanitizeRecord(
          catalog.phraseFallbacks,
          privateTerms,
          catalog.browserPrivate?.phraseFallbacks,
        ),
        characters: Object.fromEntries(Object.entries(catalog.characters)
          .filter(([name]) => publicNames.has(name)
            && !(catalog.browserPrivate?.characters ?? []).includes(name))),
        passives: Object.fromEntries(Object.entries(catalog.passives)
          .filter(([name]) => publicPassiveNames.has(name)
            && !(catalog.browserPrivate?.passives ?? []).includes(name))),
      }
      const publicPhrases = Object.fromEntries(Object.entries(phrases)
        .filter(([, group]) => !containsPrivateContent(group.passiveNameRussian, privateTerms)
          && !containsPrivateContent(group.passiveNameEnglish, privateTerms)))

      return [
        `export const englishCatalog = ${JSON.stringify(publicCatalog)};`,
        `export const phrases = ${JSON.stringify(publicPhrases)};`,
        `export const characters = ${JSON.stringify(publicCharacters)};`,
      ].join('\n')
    },
  }
}

// https://vitejs.dev/config/
export default defineConfig({
  resolve: {
    alias: {
      src: fileURLToPath(new URL('src', import.meta.url)),
    },
  },
  build: {
    // Output directly to the C# project's wwwroot for production serving
    outDir: '../../King-of-the-Garbage-Hill/wwwroot',
    emptyOutDir: true,
  },
  plugins: [
    messageCatalogPlugin(),
    publicLocalizationPlugin(),
    vue(),
    analyzer({ summaryOnly: true }),
  ],
  server: {
    proxy: {
      '/api': {
        target: 'http://3.65.44.127',
        changeOrigin: true,
      },
      '/gamehub': {
        target: 'http://3.65.44.127',
        ws: true,
        changeOrigin: true,
      },
      '/art': {
        target: 'http://3.65.44.127',
        changeOrigin: true,
      },
      '/sound': {
        target: 'http://3.65.44.127',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'happy-dom',
    globals: true,
    snapshotFormat: {
      escapeString: false,
    },
    coverage: {
      enabled: true,
      provider: 'v8',
      include: [
        'src',
      ],
      exclude: [
        'src/*.{ts,vue}',
        'src/services/api.ts',
        'src/utils/test',
        '**/*.d.ts',
      ],
      all: true,
    },
  },
})
