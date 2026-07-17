import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig } from '../config'
import { createEmpiresRngState } from '../rng'
import type { CombatArmorProfile, CombatWeaponProfile } from './types'
import { autoSelectDamageType, resolveDamage } from './damage'

const rules = cloneEmpiresConfig(defaultConfigJson).combat

function weapon(
  damageLevels: CombatWeaponProfile['damageLevels'],
  options: Omit<CombatWeaponProfile, 'damageLevels' | 'tags'> & { tags?: string[] } = {},
): CombatWeaponProfile {
  return {
    damageLevels,
    tags: options.tags ?? [],
    mixed: options.mixed,
    twoTyped: options.twoTyped,
    passiveIds: options.passiveIds,
  }
}

function armor(classId: string, level = 99): CombatArmorProfile {
  return { classId, level }
}

function deepFreeze<T>(value: T): T {
  if (typeof value !== 'object' || value === null || Object.isFrozen(value)) return value
  for (const entry of Object.values(value)) deepFreeze(entry)
  return Object.freeze(value)
}

describe('Empire\'s Endgame combat counter matrix', () => {
  it.each([
    {
      name: 'Ударные > Кольчуга',
      weapon: weapon({ impact: 2 }),
      armor: armor('mail'),
      chosenType: 'impact',
      ruleId: 'impact-counters-mail',
      counteredSide: 'armor',
      finalDamage: 2,
    },
    {
      name: 'Кольчуга > Режущие',
      weapon: weapon({ cutting: 2 }, { passiveIds: ['bleeding'] }),
      armor: armor('mail'),
      chosenType: 'cutting',
      ruleId: 'mail-counters-cutting',
      counteredSide: 'weapon',
      finalDamage: 2,
    },
    {
      name: 'Рубящее > Бригантина',
      weapon: weapon({ chopping: 4 }),
      armor: armor('brigandine'),
      chosenType: 'chopping',
      ruleId: 'chopping-counters-brigandine',
      counteredSide: 'armor',
      finalDamage: 4,
    },
    {
      name: 'Бригантина > Ударное',
      weapon: weapon({ impact: 4 }),
      armor: armor('brigandine'),
      chosenType: 'impact',
      ruleId: 'brigandine-counters-impact',
      counteredSide: 'weapon',
      finalDamage: 4,
    },
    {
      name: 'Тканевые > Ударные',
      weapon: weapon({ impact: 4 }),
      armor: armor('textile'),
      chosenType: 'impact',
      ruleId: 'textile-counters-impact',
      counteredSide: 'weapon',
      finalDamage: 4,
    },
    {
      name: 'Тканевые > Режущие',
      weapon: weapon({ cutting: 4 }),
      armor: armor('textile'),
      chosenType: 'cutting',
      ruleId: 'textile-counters-cutting',
      counteredSide: 'weapon',
      finalDamage: 4,
    },
    {
      name: 'Колющее выше общего уровня брони',
      weapon: weapon({ piercing: 5 }),
      armor: armor('plate', 4),
      chosenType: 'piercing',
      ruleId: 'piercing-overmatches-armor',
      counteredSide: 'armor',
      finalDamage: 5,
    },
    {
      name: 'Эсток и Ледоруб контрят любую броню',
      weapon: weapon({ chopping: 3 }, { tags: ['armor-breaker'] }),
      armor: armor('plate'),
      chosenType: 'chopping',
      ruleId: 'armor-breaker-counters-all-armor',
      counteredSide: 'armor',
      finalDamage: 3,
    },
    {
      name: 'топоры опускают щиты',
      weapon: weapon({ chopping: 3 }, { tags: ['axe'], passiveIds: ['lower-shields'] }),
      armor: armor('shield'),
      chosenType: 'chopping',
      ruleId: 'axe-counters-shield',
      counteredSide: 'armor',
      finalDamage: 3,
    },
    {
      name: 'щиты выключают стрелы',
      weapon: weapon({ impact: 3 }, { tags: ['arrow'], passiveIds: ['arrow-effect'] }),
      armor: armor('shield'),
      chosenType: 'impact',
      ruleId: 'shield-blocks-arrows',
      counteredSide: 'weapon',
      finalDamage: 0,
    },
  ])('$name', (fixture) => {
    const result = resolveDamage(fixture.weapon, fixture.armor, rules)

    expect(result).toMatchObject({
      chosenType: fixture.chosenType,
      rawDamage: fixture.weapon.damageLevels[fixture.chosenType],
      finalDamage: fixture.finalDamage,
      passivesDisabled: true,
    })
    expect(result.counterRules).toContainEqual(expect.objectContaining({
      ruleId: fixture.ruleId,
      counteredSide: fixture.counteredSide,
    }))
    expect(result.weaponPassivesDisabled).toBe(fixture.counteredSide === 'weapon')
    expect(result.armorPassivesDisabled).toBe(fixture.counteredSide === 'armor')
  })

  it('uses cutting against a naked target even when another authored level is higher', () => {
    const profile = weapon({ impact: 9, cutting: 1, piercing: 8 })

    expect(autoSelectDamageType(profile, null, rules)).toBe('cutting')
    expect(resolveDamage(profile, null, rules)).toMatchObject({
      chosenType: 'cutting',
      selectionReason: 'unarmoredPriority',
      rawDamage: 1,
      finalDamage: 1,
    })
  })

  it('uses strict piercing-over-armor boundaries and not equality', () => {
    const profile = weapon({ impact: 5, piercing: 4 })

    expect(resolveDamage(profile, armor('plate', 3), rules)).toMatchObject({
      chosenType: 'piercing',
      selectionReason: 'armorOvermatchPriority',
    })
    expect(resolveDamage(profile, armor('plate', 4), rules)).toMatchObject({
      chosenType: 'impact',
      selectionReason: 'bestApplicableType',
    })
    expect(resolveDamage(profile, armor('plate', 5), rules)).toMatchObject({
      chosenType: 'impact',
      selectionReason: 'bestApplicableType',
    })
  })

  it('does not let mail counter a piercing strike that fails the overmatch threshold', () => {
    const result = resolveDamage(
      weapon({ piercing: 3 }, { passiveIds: ['point-effect'] }),
      armor('mail', 3),
      rules,
    )

    expect(result).toMatchObject({
      chosenType: 'piercing',
      counterRules: [],
      passivesDisabled: false,
      finalDamage: 3,
    })
  })

  it('chooses a lower-level authored counter before a damage type countered by armor', () => {
    const profile = weapon({ impact: 2, cutting: 8 })

    expect(autoSelectDamageType(profile, armor('mail'), rules)).toBe('impact')
  })

  it('keeps config order as the deterministic final tie-break', () => {
    const profile = weapon({ impact: 2, crushing: 2 })

    expect(autoSelectDamageType(profile, armor('plate'), rules)).toBe('impact')
  })

  it('does not let armor counter a mixed profile', () => {
    const result = resolveDamage(
      weapon({ impact: 3, cutting: 6 }, { mixed: true, passiveIds: ['mixed-effect'] }),
      armor('textile'),
      rules,
    )

    expect(result.counterRules).toEqual([])
    expect(result.passivesDisabled).toBe(false)
    expect(result.weaponPassivesDisabled).toBe(false)
  })

  it('does not demote a mixed profile type merely because armor would counter a normal weapon', () => {
    const result = resolveDamage(
      weapon({ impact: 10, crushing: 1 }, { mixed: true }),
      armor('brigandine'),
      rules,
    )

    expect(result).toMatchObject({
      chosenType: 'impact',
      rawDamage: 10,
      passivesDisabled: false,
    })
  })

  it('checks both sides of a two-type profile and can suppress both items', () => {
    const result = resolveDamage(
      weapon({ impact: 3, cutting: 4 }, { twoTyped: true, passiveIds: ['dual-effect'] }),
      armor('mail'),
      rules,
    )

    expect(result.counterRules).toEqual(expect.arrayContaining([
      expect.objectContaining({ ruleId: 'impact-counters-mail', counteredSide: 'armor' }),
      expect.objectContaining({ ruleId: 'mail-counters-cutting', counteredSide: 'weapon' }),
    ]))
    expect(result.weaponPassivesDisabled).toBe(true)
    expect(result.armorPassivesDisabled).toBe(true)
  })

  it.each([
    'Шило (мизорекордия)',
    'Вилка десмонда',
  ])('%s is not countered by mail', () => {
    const result = resolveDamage(
      weapon({ cutting: 4 }, { tags: ['mail-proof-point'], passiveIds: ['point-effect'] }),
      armor('mail'),
      rules,
    )

    expect(result).toMatchObject({
      chosenType: 'cutting',
      weaponPassivesDisabled: false,
      passivesDisabled: false,
      finalDamage: 4,
    })
    expect(result.counterRules).toEqual([])
  })

  it('is pure, deterministic, and does not consume the serialized RNG stream', () => {
    const frozenWeapon = deepFreeze(weapon(
      { impact: 6, piercing: 4, crushing: 3 },
      { passiveIds: ['stun'] },
    ))
    const frozenArmor = deepFreeze(armor('mail', 4))
    const frozenRules = deepFreeze(JSON.parse(JSON.stringify(rules)) as typeof rules)
    const rng = createEmpiresRngState('combat-purity')
    const rngBefore = { ...rng }

    const first = resolveDamage(frozenWeapon, frozenArmor, frozenRules)
    const second = resolveDamage(frozenWeapon, frozenArmor, frozenRules)

    expect(second).toEqual(first)
    expect(rng).toEqual(rngBefore)
    expect(Object.isFrozen(frozenWeapon)).toBe(true)
    expect(Object.isFrozen(frozenArmor)).toBe(true)
    expect(Object.isFrozen(frozenRules)).toBe(true)
  })
})
