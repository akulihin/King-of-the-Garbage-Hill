declare module 'virtual:message-catalogs' {
  export type PublicMessage = Readonly<{
    ru: string
    en: string
  }>

  export const publicMessages: Readonly<Record<string, PublicMessage>>
}
