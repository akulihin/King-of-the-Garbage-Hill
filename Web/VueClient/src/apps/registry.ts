import type { ProductId } from './products'

export type ProductApplication = Readonly<{
  mount(root: Element): void
}>

type ProductRegistration = Readonly<{
  id: ProductId
  matches(pathname: string): boolean
  load(): Promise<ProductApplication>
}>

const prefix = (basePath: string) =>
  (pathname: string) => pathname === basePath || pathname.startsWith(`${basePath}/`)

/**
 * One host page, independent Vue roots. A product's route selects its entry
 * module before Vue, Pinia, or any product router is created.
 */
const registrations: readonly ProductRegistration[] = [
  {
    id: 'battleship',
    matches: prefix('/battleship'),
    load: () => import('./battleship'),
  },
  {
    id: 'clash',
    matches: prefix('/clash'),
    load: () => import('./clash'),
  },
  {
    id: 'last-chances',
    matches: prefix('/99lc'),
    load: () => import('./last-chances'),
  },
  {
    id: 'empires-endgame',
    matches: prefix('/empires-endgame'),
    load: () => import('./empires-endgame'),
  },
  {
    id: 'kotgh',
    matches: () => true,
    load: () => import('./kotgh'),
  },
]

export function resolveProductApplication(pathname: string): Promise<ProductApplication> {
  const registration = registrations.find(candidate => candidate.matches(pathname))
  if (!registration)
    throw new Error(`No application is registered for "${pathname}".`)
  document.documentElement.dataset.product = registration.id
  return registration.load()
}
