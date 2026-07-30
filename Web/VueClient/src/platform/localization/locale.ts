import { ref } from 'vue'

export type AppLocale = 'ru' | 'en'

const localeKey = 'kotgh_locale'
const savedLocale = localStorage.getItem(localeKey)

export const currentLocale = ref<AppLocale>(
  savedLocale === 'ru' || savedLocale === 'en' ? savedLocale : 'en',
)

export function setLocale(nextLocale: AppLocale): void {
  currentLocale.value = nextLocale
  localStorage.setItem(localeKey, nextLocale)
  document.documentElement.lang = nextLocale
}

document.documentElement.lang = currentLocale.value
