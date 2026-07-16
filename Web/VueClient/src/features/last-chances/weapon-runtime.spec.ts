import { describe, expect, it } from 'vitest'
import type { LastChancesAttackDefinition, LastChancesResolvedWeapon } from './types'
import {
  attackWithLastChancesAugment,
  resolveLastChancesChargedAttack,
} from './weapon-runtime'

const baseAttack: LastChancesAttackDefinition = {
  name: 'Charged test',
  kind: 'dash',
  behavior: 'spearRam',
  damage: 10,
  cooldownMs: 1000,
  range: 100,
  radius: 10,
  arcDegrees: 30,
  durationMs: 500,
  projectileSpeed: 200,
  pierce: 1,
  knockback: 20,
  color: '#fff',
  charge: {
    maxMs: 1200,
    bands: [
      { id: 'early', label: 'Early', minMs: 0, color: '#0ff', damageMultiplier: 0.8 },
      {
        id: 'late',
        label: 'Late',
        minMs: 800,
        color: '#f80',
        damageMultiplier: 1.8,
        rangeMultiplier: 1.5,
        overrides: { invulnerabilityMs: 500 },
      },
    ],
  },
}

describe('99LC weapon runtime helpers', () => {
  it('selects the last reached charge band and applies its authored scalars', () => {
    const result = resolveLastChancesChargedAttack(baseAttack, 900)
    expect(result.band?.id).toBe('late')
    expect(result.chargeProgress).toBe(0.75)
    expect(result.attack).toMatchObject({
      damage: 18,
      range: 150,
      invulnerabilityMs: 500,
    })
  })

  it('keeps a charge action unarmed before its first authored release band', () => {
    const gated = {
      ...baseAttack,
      charge: {
        maxMs: 1200,
        bands: [
          { id: 'ready', label: 'Ready', minMs: 650, color: '#0ff' },
        ],
      },
    } satisfies LastChancesAttackDefinition
    const result = resolveLastChancesChargedAttack(gated, 500)
    expect(result.band).toBeNull()
    expect(result.attack.damage).toBe(gated.damage)
  })

  it('merges the equipped augment hook without mutating the catalog attack', () => {
    const weapon = {
      id: 'test',
      name: 'Test',
      hand: 'left',
      attacks: {} as LastChancesResolvedWeapon['attacks'],
      tapCombo: [],
      augment: 'poison',
      augmentHooks: {
        poison: {
          damageMultiplier: 1.2,
          hitEffects: [{
            status: 'poison',
            durationMs: 3000,
            tickDamage: 2,
            tickMs: 500,
          }],
        },
      },
    } satisfies LastChancesResolvedWeapon
    const result = attackWithLastChancesAugment(baseAttack, weapon)
    expect(result.damage).toBe(12)
    expect(result.hitEffects?.[0].status).toBe('poison')
    expect(baseAttack.hitEffects).toBeUndefined()
  })

  it('applies an augment only to its authored behaviors', () => {
    const weapon = {
      id: 'scoped',
      name: 'Scoped',
      hand: 'left',
      attacks: {} as LastChancesResolvedWeapon['attacks'],
      tapCombo: [],
      augment: 'fire',
      augmentHooks: {
        fire: {
          behaviors: ['clawRend'],
          hitEffects: [{ status: 'burn', durationMs: 3000 }],
        },
      },
    } satisfies LastChancesResolvedWeapon
    expect(attackWithLastChancesAugment({
      ...baseAttack,
      behavior: 'clawRend',
    }, weapon).hitEffects?.[0].status).toBe('burn')
    expect(attackWithLastChancesAugment({
      ...baseAttack,
      behavior: 'clawDash',
    }, weapon).hitEffects).toBeUndefined()
  })
})
