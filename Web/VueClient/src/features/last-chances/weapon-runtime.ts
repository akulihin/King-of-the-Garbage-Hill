import type {
  LastChancesAttackDefinition,
  LastChancesAugment,
  LastChancesChargeBandDefinition,
  LastChancesGesture,
  LastChancesHitEffectDefinition,
  LastChancesResolvedWeapon,
} from './types'

export const LAST_CHANCES_GESTURE_COLORS: Record<LastChancesGesture, string> = {
  tap: '#f2c66d',
  doubleTap: '#66d6ff',
  doubleTapHold: '#b785ff',
  hold: '#ff9a5a',
  holdThenDoubleTap: '#64e6a5',
}

export interface LastChancesResolvedAttack {
  attack: LastChancesAttackDefinition
  band: LastChancesChargeBandDefinition | null
  heldMs: number
  chargeProgress: number
}

function cloneAttack(attack: LastChancesAttackDefinition): LastChancesAttackDefinition {
  return JSON.parse(JSON.stringify(attack)) as LastChancesAttackDefinition
}

function applyMultiplier(
  value: number,
  multiplier: number | undefined,
): number {
  return multiplier === undefined ? value : value * multiplier
}

export function resolveLastChancesChargedAttack(
  source: LastChancesAttackDefinition,
  heldMs: number,
): LastChancesResolvedAttack {
  const attack = cloneAttack(source)
  const charge = attack.charge
  if (!charge) return { attack, band: null, heldMs, chargeProgress: 0 }
  const clampedHeldMs = Math.max(0, Math.min(heldMs, charge.maxMs))
  const band = [...charge.bands]
    .sort((left, right) => left.minMs - right.minMs)
    .filter(candidate => clampedHeldMs >= candidate.minMs)
    .at(-1) ?? null
  if (band) {
    attack.damage = applyMultiplier(attack.damage, band.damageMultiplier)
    attack.range = applyMultiplier(attack.range, band.rangeMultiplier)
    attack.knockback = applyMultiplier(attack.knockback, band.knockbackMultiplier)
    attack.durationMs = applyMultiplier(attack.durationMs, band.durationMultiplier)
    attack.projectileSpeed = applyMultiplier(attack.projectileSpeed, band.speedMultiplier)
    if (band.overrides) Object.assign(attack, band.overrides)
  }
  return {
    attack,
    band,
    heldMs: clampedHeldMs,
    chargeProgress: charge.maxMs > 0 ? clampedHeldMs / charge.maxMs : 1,
  }
}

export function attackWithLastChancesAugment(
  source: LastChancesAttackDefinition,
  weapon: LastChancesResolvedWeapon,
): LastChancesAttackDefinition {
  const attack = cloneAttack(source)
  const hook = weapon.augmentHooks?.[weapon.augment]
  if (!hook) return attack
  if (hook.behaviors?.length && (!attack.behavior || !hook.behaviors.includes(attack.behavior))) {
    return attack
  }
  if (hook.damageMultiplier !== undefined) attack.damage *= hook.damageMultiplier
  if (hook.hitEffects?.length) {
    attack.hitEffects = [
      ...(attack.hitEffects ?? []),
      ...hook.hitEffects.map(effect => ({ ...effect })),
    ]
  }
  return attack
}

export function lastChancesAttackEffects(
  attack: LastChancesAttackDefinition,
  status: LastChancesHitEffectDefinition['status'],
): LastChancesHitEffectDefinition[] {
  return (attack.hitEffects ?? []).filter(effect => effect.status === status)
}

export function lastChancesAugmentForHand(
  weapon: LastChancesResolvedWeapon | undefined,
): LastChancesAugment {
  return weapon?.augment ?? 'none'
}
