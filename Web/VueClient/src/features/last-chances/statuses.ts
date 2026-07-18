import type { LastChancesHitEffectDefinition } from './types'

export const LAST_CHANCES_DOT_KINDS = ['bleed', 'poison', 'burn', 'chemical'] as const

export type LastChancesDotKind = typeof LAST_CHANCES_DOT_KINDS[number]

export interface LastChancesRuntimeDot {
  kind: LastChancesDotKind
  stacks: number
  tickDamage: number
  tickMs: number
  remainingMs: number
  tickAccumulatorMs: number
}

export interface LastChancesRuntimeStatuses {
  dots: Record<LastChancesDotKind, LastChancesRuntimeDot>
  stunMs: number
  disarmMs: number
  antiHealMs: number
  armorBreak: number
  armorBreakMs: number
  slowMultiplier: number
  slowMs: number
  attackSlowMultiplier: number
  attackSlowMs: number
  openingMs: number
  boundMs: number
  /** Control immunity granted to elites/bosses after enough cumulative Sword stagger. */
  unstoppableMs: number
  /** Sword stagger accumulated since the previous Unstoppable window. */
  staggerAccumulatedMs: number
}

export interface LastChancesStoredDot {
  kind: Exclude<LastChancesDotKind, 'bleed'>
  stacks: number
  tickDamage: number
  tickMs: number
  remainingMs: number
}

const DEFAULT_DOT_TICK_MS = 500
const DEFAULT_BLEED_TICK_DAMAGE = 0.9

function makeDot(kind: LastChancesDotKind): LastChancesRuntimeDot {
  return {
    kind,
    stacks: 0,
    tickDamage: 0,
    tickMs: DEFAULT_DOT_TICK_MS,
    remainingMs: 0,
    tickAccumulatorMs: 0,
  }
}

export function createLastChancesStatuses(): LastChancesRuntimeStatuses {
  return {
    dots: {
      bleed: makeDot('bleed'),
      poison: makeDot('poison'),
      burn: makeDot('burn'),
      chemical: makeDot('chemical'),
    },
    stunMs: 0,
    disarmMs: 0,
    antiHealMs: 0,
    armorBreak: 0,
    armorBreakMs: 0,
    slowMultiplier: 1,
    slowMs: 0,
    attackSlowMultiplier: 1,
    attackSlowMs: 0,
    openingMs: 0,
    boundMs: 0,
    unstoppableMs: 0,
    staggerAccumulatedMs: 0,
  }
}

function applyDot(
  status: LastChancesRuntimeStatuses,
  kind: LastChancesDotKind,
  stacks: number,
  tickDamage: number,
  tickMs: number,
  durationMs: number,
  refresh: LastChancesHitEffectDefinition['refresh'] = 'refresh',
): void {
  if (stacks <= 0 || tickDamage <= 0 || tickMs <= 0 || durationMs <= 0) return
  const dot = status.dots[kind]
  const wasActive = dot.stacks > 0 && dot.remainingMs > 0
  if (refresh === 'replace') {
    dot.stacks = stacks
    dot.remainingMs = durationMs
  } else if (refresh === 'extend') {
    dot.stacks = Math.max(dot.stacks, stacks)
    dot.remainingMs += durationMs
  } else {
    dot.stacks = refresh === 'stack' ? dot.stacks + stacks : Math.max(dot.stacks, stacks)
    dot.remainingMs = Math.max(dot.remainingMs, durationMs)
  }
  dot.tickDamage = wasActive ? Math.max(dot.tickDamage, tickDamage) : tickDamage
  dot.tickMs = wasActive ? Math.min(dot.tickMs, tickMs) : tickMs
}

export function applyLastChancesStatusEffects(
  status: LastChancesRuntimeStatuses,
  effects: LastChancesHitEffectDefinition[] | undefined,
  random: () => number = Math.random,
): void {
  if (!effects) return
  for (const effect of effects) {
    if ((effect.chance ?? 1) < random()) continue
    if (status.unstoppableMs > 0 && [
      'stun',
      'microstun',
      'disarm',
      'slow',
      'attackSlow',
      'bound',
    ].includes(effect.status)) continue
    const stacks = Math.max(1, effect.stacks ?? 1)
    if (LAST_CHANCES_DOT_KINDS.includes(effect.status as LastChancesDotKind)) {
      const kind = effect.status as LastChancesDotKind
      applyDot(
        status,
        kind,
        stacks,
        effect.tickDamage ?? (kind === 'bleed' ? DEFAULT_BLEED_TICK_DAMAGE : 1),
        effect.tickMs ?? DEFAULT_DOT_TICK_MS,
        effect.durationMs,
        effect.refresh,
      )
      continue
    }
    if (effect.status === 'stun' || effect.status === 'microstun') {
      status.stunMs = Math.max(status.stunMs, effect.durationMs)
    }
    if (effect.status === 'disarm') {
      status.disarmMs = Math.max(status.disarmMs, effect.durationMs)
    }
    if (effect.status === 'healingBlocked') {
      status.antiHealMs = Math.max(status.antiHealMs, effect.durationMs)
    }
    if (effect.status === 'armorBreak') {
      status.armorBreak = Math.max(status.armorBreak, effect.magnitude ?? 1)
      status.armorBreakMs = Math.max(status.armorBreakMs, effect.durationMs)
    }
    if (effect.status === 'slow') {
      status.slowMs = Math.max(status.slowMs, effect.durationMs)
      status.slowMultiplier = Math.min(status.slowMultiplier, effect.magnitude ?? 0.5)
    }
    if (effect.status === 'attackSlow') {
      status.attackSlowMs = Math.max(status.attackSlowMs, effect.durationMs)
      status.attackSlowMultiplier = Math.max(status.attackSlowMultiplier, effect.magnitude ?? 2)
    }
    if (effect.status === 'opening') {
      status.openingMs = Math.max(status.openingMs, effect.durationMs)
    }
    if (effect.status === 'bound') {
      status.boundMs = Math.max(status.boundMs, effect.durationMs)
    }
    if (effect.status === 'unstoppable') {
      status.unstoppableMs = Math.max(status.unstoppableMs, effect.durationMs)
    }
  }
}

export function updateLastChancesStatuses(
  status: LastChancesRuntimeStatuses,
  deltaMs: number,
  damage: (amount: number, kind: LastChancesDotKind) => void,
): void {
  for (const dot of Object.values(status.dots)) {
    if (dot.remainingMs <= 0 || dot.stacks <= 0) continue
    const activeMs = Math.min(deltaMs, dot.remainingMs)
    const healingBlocked = dot.kind === 'bleed' && status.antiHealMs > 0
    if (!healingBlocked) dot.remainingMs = Math.max(0, dot.remainingMs - deltaMs)
    dot.tickAccumulatorMs += activeMs
    while (dot.tickAccumulatorMs >= dot.tickMs) {
      dot.tickAccumulatorMs -= dot.tickMs
      damage(dot.tickDamage * dot.stacks, dot.kind)
    }
    if (dot.remainingMs <= 0) {
      dot.stacks = 0
      dot.tickDamage = 0
      dot.tickMs = DEFAULT_DOT_TICK_MS
      dot.tickAccumulatorMs = 0
    }
  }

  status.stunMs = Math.max(0, status.stunMs - deltaMs)
  status.disarmMs = Math.max(0, status.disarmMs - deltaMs)
  status.antiHealMs = Math.max(0, status.antiHealMs - deltaMs)
  status.openingMs = Math.max(0, status.openingMs - deltaMs)
  status.armorBreakMs = Math.max(0, status.armorBreakMs - deltaMs)
  if (status.armorBreakMs <= 0) status.armorBreak = 0
  status.slowMs = Math.max(0, status.slowMs - deltaMs)
  if (status.slowMs <= 0) status.slowMultiplier = 1
  status.attackSlowMs = Math.max(0, status.attackSlowMs - deltaMs)
  if (status.attackSlowMs <= 0) status.attackSlowMultiplier = 1
  status.boundMs = Math.max(0, status.boundMs - deltaMs)
  status.unstoppableMs = Math.max(0, status.unstoppableMs - deltaMs)
}

export function captureLastChancesDot(
  status: LastChancesRuntimeStatuses,
): LastChancesStoredDot | null {
  const candidates = (['poison', 'burn', 'chemical'] as const)
    .map(kind => status.dots[kind])
    .filter(dot => dot.remainingMs > 0 && dot.stacks > 0)
    .sort((left, right) => (
      right.tickDamage * right.stacks / right.tickMs - left.tickDamage * left.stacks / left.tickMs
      || right.remainingMs - left.remainingMs
    ))
  const dot = candidates[0]
  if (!dot) return null
  return {
    kind: dot.kind as LastChancesStoredDot['kind'],
    stacks: dot.stacks,
    tickDamage: dot.tickDamage,
    tickMs: dot.tickMs,
    remainingMs: dot.remainingMs,
  }
}

export function spreadLastChancesDot(
  status: LastChancesRuntimeStatuses,
  stored: LastChancesStoredDot,
): void {
  applyDot(
    status,
    stored.kind,
    stored.stacks,
    stored.tickDamage,
    stored.tickMs,
    stored.remainingMs,
  )
}

export function consumeLastChancesBleed(status: LastChancesRuntimeStatuses): number {
  const bleed = status.dots.bleed
  const remainingTicks = bleed.tickMs > 0 ? Math.ceil(bleed.remainingMs / bleed.tickMs) : 0
  const remainingDamage = bleed.tickDamage * bleed.stacks * remainingTicks
  bleed.stacks = 0
  bleed.tickDamage = 0
  bleed.tickMs = DEFAULT_DOT_TICK_MS
  bleed.remainingMs = 0
  bleed.tickAccumulatorMs = 0
  return remainingDamage
}

export function refreshLastChancesBleed(
  status: LastChancesRuntimeStatuses,
  durationMs = 5000,
  multiplier = 1,
): void {
  const bleed = status.dots.bleed
  if (bleed.stacks <= 0) return
  bleed.stacks = Math.max(1, Math.round(bleed.stacks * multiplier))
  bleed.remainingMs = Math.max(bleed.remainingMs, durationMs)
}
