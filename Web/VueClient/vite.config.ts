/// <reference types="vitest" />

import { URL, fileURLToPath } from 'node:url'
import { readFileSync } from 'node:fs'
import vue from '@vitejs/plugin-vue'
import analyzer from 'rollup-plugin-analyzer'
import { defineConfig, type Plugin } from 'vite'

const publicLocalizationId = 'virtual:public-localization'
const resolvedPublicLocalizationId = `\0${publicLocalizationId}`

type CharacterSource = {
  Name: string
  Tier: number
  BrowserCatalog?: boolean
  Description?: string
  StoryAgent?: string
  Passive?: Array<{ PassiveName: string; PassiveDescription?: string }>
}

type LocalizationCatalog = {
  exact: Record<string, string>
  terms: Record<string, string>
  russianExact: Record<string, string>
  phraseFallbacks: Record<string, string>
  characters: Record<string, string>
  passives: Record<string, string>
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

function sanitizeRecord(values: Record<string, string>, privateTerms: string[]): Record<string, string> {
  return Object.fromEntries(Object.entries(values)
    .filter(([key, value]) => !containsPrivateContent(key, privateTerms)
      && !containsPrivateContent(value, privateTerms)))
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

      const publicCatalog: LocalizationCatalog = {
        exact: sanitizeRecord(catalog.exact, privateTerms),
        terms: sanitizeRecord(catalog.terms, privateTerms),
        russianExact: sanitizeRecord(catalog.russianExact, privateTerms),
        phraseFallbacks: sanitizeRecord(catalog.phraseFallbacks, privateTerms),
        characters: Object.fromEntries(Object.entries(catalog.characters)
          .filter(([name]) => publicNames.has(name))),
        passives: Object.fromEntries(Object.entries(catalog.passives)
          .filter(([name]) => publicPassiveNames.has(name))),
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
    setupFiles: './src/setupTests.ts',
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
        'src/setupTests.ts',
        'src/utils/test',
        '**/*.d.ts',
      ],
      all: true,
    },
  },
})
