export type ProductId =
  | 'kotgh'
  | 'battleship'
  | 'clash'
  | 'last-chances'
  | 'empires-endgame'

export type AuthenticatedProductId = Extract<ProductId, 'kotgh' | 'battleship' | 'clash'>

export type KotghNavigation = Readonly<{
  href: string
  labelKey: string
}>

export type ProductNavigation = Readonly<{
  id: Exclude<ProductId, 'kotgh'>
  href: string
  labelKey: string
}>

/** Product metadata only. Wording lives in Localization/*.messages.json. */
export const kotghNavigation: readonly KotghNavigation[] = [
  { href: '/games', labelKey: 'shell.nav.lobby' },
  { href: '/home', labelKey: 'shell.nav.home' },
  { href: '/fortress-of-doom', labelKey: 'shell.nav.fortress' },
  { href: '/store', labelKey: 'shell.nav.store' },
  { href: '/achievements', labelKey: 'shell.nav.achievements' },
  { href: '/fight-calculator', labelKey: 'shell.nav.fightCalculator' },
]

export const productNavigation: readonly ProductNavigation[] = [
  { id: 'battleship', href: '/battleship', labelKey: 'battleship.title' },
  { id: 'last-chances', href: '/99lc', labelKey: 'last-chances.title' },
  { id: 'empires-endgame', href: '/empires-endgame', labelKey: 'empires-endgame.title' },
  { id: 'clash', href: '/clash', labelKey: 'clash.title' },
]
