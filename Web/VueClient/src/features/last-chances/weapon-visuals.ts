import type { LastChancesVector } from './types'

export type LastChancesHeldWeaponGrip = 'oneHanded' | 'twoHanded' | 'hybridSword'

/**
 * One generated bitmap is treated as a tiny rig: `pivot` is where the weapon is held and
 * `tip` defines its authored forward axis. Both points are normalized against the source PNG,
 * so transparent padding and portrait-oriented art never have to be cropped or stretched.
 */
export interface LastChancesHeldWeaponVisualDefinition {
  assetPath: string
  pivot: LastChancesVector
  tip: LastChancesVector
  frontReachRadii: number
  grip: LastChancesHeldWeaponGrip
  gripSpacingRadii: number
  readyForwardRadii: number
  restDropRadii: number
  handScale: number
  /** The bitmap already contains the gripping hand/body, so a generic palm would cover its art. */
  suppressHandDisc?: boolean
  shadowColor: string
}

export const LAST_CHANCES_HELD_WEAPON_VISUALS: Readonly<
  Record<string, LastChancesHeldWeaponVisualDefinition>
> = {
  'hybrid-sword': {
    assetPath: '/99lc/mercenary-sword.png',
    pivot: { x: 0.155, y: 0.5 },
    tip: { x: 0.98, y: 0.5 },
    frontReachRadii: 4.35,
    grip: 'hybridSword',
    gripSpacingRadii: 0.34,
    readyForwardRadii: 0.48,
    restDropRadii: 0.82,
    handScale: 0.2,
    shadowColor: '#ff3f32',
  },
  'twohand-axe': {
    assetPath: '/99lc/twohand-axe.png',
    pivot: { x: 0.365, y: 0.505 },
    tip: { x: 0.96, y: 0.505 },
    frontReachRadii: 4.7,
    grip: 'twoHanded',
    gripSpacingRadii: 0.62,
    readyForwardRadii: 0.38,
    restDropRadii: 0,
    handScale: 0.22,
    shadowColor: '#ff3a2e',
  },
  'secondary-chain': {
    assetPath: '/99lc/chain-whip.png',
    pivot: { x: 0.115, y: 0.505 },
    tip: { x: 0.975, y: 0.505 },
    frontReachRadii: 4.6,
    grip: 'oneHanded',
    gripSpacingRadii: 0,
    readyForwardRadii: 0.38,
    restDropRadii: 0.9,
    handScale: 0.2,
    shadowColor: '#48d9ff',
  },
  'either-claws': {
    assetPath: '/99lc/ninja-claw.png',
    pivot: { x: 0.205, y: 0.49 },
    tip: { x: 0.965, y: 0.43 },
    frontReachRadii: 2.75,
    grip: 'oneHanded',
    gripSpacingRadii: 0,
    readyForwardRadii: 0.68,
    restDropRadii: 0.72,
    handScale: 0.13,
    shadowColor: '#ff382e',
  },
  'secondary-ouroboros-fang': {
    assetPath: '/99lc/ouroboros-fang.png',
    pivot: { x: 0.22, y: 0.5 },
    tip: { x: 0.96, y: 0.5 },
    frontReachRadii: 2.9,
    grip: 'oneHanded',
    gripSpacingRadii: 0,
    readyForwardRadii: 0.54,
    restDropRadii: 0.8,
    handScale: 0.19,
    shadowColor: '#6dff50',
  },
  'twohand-katana': {
    assetPath: '/99lc/twohand-katana.png',
    pivot: { x: 0.225, y: 0.5 },
    tip: { x: 0.985, y: 0.5 },
    frontReachRadii: 5.15,
    grip: 'twoHanded',
    gripSpacingRadii: 0.48,
    readyForwardRadii: 0.44,
    restDropRadii: 0,
    handScale: 0.2,
    shadowColor: '#ff392f',
  },
  'secondary-spider-knife': {
    assetPath: '/99lc/spider-knife-v2.png',
    pivot: { x: 0.5, y: 0.775 },
    tip: { x: 0.5, y: 0.04 },
    frontReachRadii: 1.8,
    grip: 'oneHanded',
    gripSpacingRadii: 0,
    readyForwardRadii: 0.52,
    restDropRadii: 0.88,
    handScale: 0.19,
    suppressHandDisc: true,
    shadowColor: '#ff332d',
  },
}

export function lastChancesHeldWeaponVisual(
  weaponId: string,
): LastChancesHeldWeaponVisualDefinition | null {
  return LAST_CHANCES_HELD_WEAPON_VISUALS[weaponId] ?? null
}
