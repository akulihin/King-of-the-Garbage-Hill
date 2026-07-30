import type {
  ClashResolutionEvent,
  ClashUnitDefinition,
  ClashUnitState,
} from './types'

const SAFE_UNIT_ID = /[^a-z0-9-]/g

export function clashUnitArtUrl(definitionId: string): string {
  const safeId = definitionId.toLowerCase().replace(SAFE_UNIT_ID, '')
  return `/clash/art/units/${safeId || 'unknown'}.webp`
}

export function clashUnitInitials(unit: Pick<ClashUnitDefinition, 'name'> | Pick<ClashUnitState, 'name' | 'definitionId'>): string {
  const name = 'name' in unit && unit.name ? unit.name : 'definitionId' in unit ? unit.definitionId : ''
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return '⚔'
  return parts.slice(0, 2).map(part => part[0]?.toUpperCase() ?? '').join('')
}

export function clashEventAnimation(type: string): 'attack' | 'active' | 'passive' | 'hit' | 'death' | 'advance' | 'wait' {
  switch (type.toLowerCase()) {
    case 'attack': return 'attack'
    case 'rangedattack': return 'attack'
    case 'active': return 'active'
    case 'passive': return 'passive'
    case 'block':
    case 'dodge':
    case 'bleedapplied':
      return 'passive'
    case 'damage': return 'hit'
    case 'bleeddamage': return 'hit'
    case 'death': return 'death'
    case 'advance': return 'advance'
    default: return 'wait'
  }
}

export function clashEventIcon(event: Pick<ClashResolutionEvent, 'type'>): string {
  switch (event.type.toLowerCase()) {
    case 'attack': return '⚔'
    case 'rangedattack': return '➶'
    case 'active': return '✦'
    case 'passive': return '◆'
    case 'block': return '◇'
    case 'dodge': return '〽'
    case 'reload': return '↻'
    case 'bleedapplied':
    case 'bleeddamage':
      return '♦'
    case 'damage': return '✹'
    case 'death': return '☠'
    case 'advance': return '➜'
    default: return '◷'
  }
}

export function clashSpeedDelayMs(speed: number): number {
  return Math.max(0, Math.min(8, 9 - Math.round(speed))) * 500
}

export function prefersReducedClashMotion(): boolean {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

export function clashResolutionIdentity(gameId: string, revision: number, clashNumber: number): string {
  return `${gameId}:${revision}:${clashNumber}`
}

export function clashResolutionElapsedMs(startedAtUtc: string, nowMs = Date.now()): number {
  const startedAtMs = Date.parse(startedAtUtc)
  if (!Number.isFinite(startedAtMs)) return 0
  return Math.max(0, nowMs - startedAtMs)
}

/**
 * The last hidden setup row can become public in the same server mutation
 * that emits the first resolution. Reverse the authoritative event trace so
 * those newly-public units enter the animation at their pre-clash state
 * instead of leaking final damage, deaths, or advances immediately.
 */
export function reconstructClashResolutionStartUnit(
  finalUnit: ClashUnitState,
  events: ClashResolutionEvent[],
  chargeLimits?: Pick<ClashUnitDefinition, 'shieldCharges' | 'dodgeCharges'> | null,
): ClashUnitState {
  const unit = { ...finalUnit }
  for (const event of [...events].reverse()) {
    const type = event.type.toLowerCase()
    if (type === 'advance' && event.actorUnitInstanceId === unit.instanceId) {
      unit.boardRow = event.fromBoardRow ?? unit.boardRow
      unit.column = event.column ?? unit.column
    }
    else if (
      (type === 'damage' || type === 'bleeddamage')
      && event.targetUnitInstanceId === unit.instanceId
    ) {
      unit.hp = Math.min(unit.maxHp, unit.hp + Math.abs(event.amount))
    }
    else if (type === 'death' && event.targetUnitInstanceId === unit.instanceId) {
      unit.alive = true
    }
    else if (type === 'block' && event.targetUnitInstanceId === unit.instanceId) {
      unit.shieldCharges = Math.min(
        chargeLimits?.shieldCharges ?? Number.MAX_SAFE_INTEGER,
        unit.shieldCharges + 1,
      )
    }
    else if (type === 'dodge' && event.targetUnitInstanceId === unit.instanceId) {
      unit.dodgeCharges = Math.min(
        chargeLimits?.dodgeCharges ?? Number.MAX_SAFE_INTEGER,
        unit.dodgeCharges + 1,
      )
    }
    else if (type === 'bleedapplied' && event.targetUnitInstanceId === unit.instanceId) {
      unit.bleedStacks = Math.max(0, unit.bleedStacks - Math.max(1, event.amount))
    }
  }
  return unit
}
