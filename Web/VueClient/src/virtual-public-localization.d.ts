declare module 'virtual:public-localization' {
  export const englishCatalog: unknown
  export const phrases: unknown
  export const characters: Array<{
    Name: string
    Description: string
    Passive: Array<{ PassiveName: string; PassiveDescription: string }>
  }>
}
