import { publicMessages } from 'virtual:message-catalogs'
import { currentLocale, type AppLocale } from './locale'

export type MessageArguments = Readonly<Record<string, string | number>>
export type LocalizedText = Readonly<Record<AppLocale, string>>

const placeholderPattern = /\{([A-Za-z][A-Za-z0-9_.-]*)\}/g

/**
 * Resolves new authored UI text by stable key. The JSON catalogs are the only
 * source of Russian and English wording; components supply only dynamic values.
 */
export function message(key: string, arguments_: MessageArguments = {}): string {
  const definition = publicMessages[key]
  if (!definition)
    throw new Error(`Unknown or non-public localization key "${key}".`)

  return definition[currentLocale.value].replace(placeholderPattern, (_match, name: string) => {
    const value = arguments_[name]
    if (value == null)
      throw new Error(`Localization key "${key}" is missing argument "${name}".`)
    return String(value)
  })
}

/** Selects already-authored dynamic prose without feeding it through string replacement. */
export function localizedText(value: LocalizedText): string {
  return value[currentLocale.value]
}
