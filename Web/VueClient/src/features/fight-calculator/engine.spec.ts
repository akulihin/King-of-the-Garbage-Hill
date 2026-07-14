import { describe, expect, it } from 'vitest'
import { createDefaultProfile } from './catalog'
import { runFight } from './engine'
import type { CalculatorProfile, DamageValues, WeaponDefinition } from './types'

function emptyDamage(): DamageValues {
  return { Ударное: 0, Дробящее: 0, Рубящее: 0, Режущее: 0, Колющее: 0 }
}

function oneOnOneProfile(): CalculatorProfile {
  const profile = createDefaultProfile('test')
  for (const unit of [...profile.team1, ...profile.team2]) {
    unit.enabled = false
    unit.helmetId = ''
    unit.mailId = ''
    unit.paddingId = ''
    unit.plateId = ''
    unit.talentIds = []
    unit.secondaryWeaponId = ''
    unit.daggerWeaponId = ''
    unit.baseHp = 10
  }
  profile.team1[0].enabled = true
  profile.team2[0].enabled = true
  profile.balance.crushKnockoutChance = 0
  profile.balance.disarmChance = 0
  profile.balance.durabilityLossMin = 0
  profile.balance.durabilityLossMax = 0
  return profile
}

function testWeapon(id: string, attacks: DamageValues, range: number, speed = 1): WeaponDefinition {
  return {
    id,
    name: id,
    category: 'test',
    attacks,
    defense: 0,
    disarm: 0,
    antiShield: 0,
    speed,
    rangeMin: range,
    rangeMax: range,
    handsMin: 1,
    handsMax: 1,
    durability: 100,
    fatigue: 0,
  }
}

describe('fight calculator engine', () => {
  it('replays an identical battle for the same profile and seed', () => {
    const profile = createDefaultProfile('deterministic')

    expect(runFight(profile, 1488)).toEqual(runFight(profile, 1488))
  })

  it('uses mirrored slots first and random fallback for unmatched occupied slots', () => {
    const profile = createDefaultProfile('pairing')
    for (const unit of [...profile.team1, ...profile.team2]) unit.enabled = false
    profile.team1[0].enabled = true
    profile.team1[1].enabled = true
    profile.team2[0].enabled = true
    profile.team2[2].enabled = true

    const result = runFight(profile, 42)

    expect(result.collisions[0].phase).toBe('mirror')
    expect(result.collisions[1].phase).toBe('fallback')
  })

  it('allows closing before a long-range weapon completes a full attack interval', () => {
    const profile = oneOnOneProfile()
    profile.weapons = [
      testWeapon('long', { ...emptyDamage(), Ударное: 2 }, 2, 1),
      testWeapon('short', { ...emptyDamage(), Ударное: 2 }, 1, 1),
    ]
    profile.team1[0].primaryWeaponId = 'long'
    profile.team2[0].primaryWeaponId = 'short'
    profile.team1[0].mastery = true
    profile.team2[0].mastery = true
    profile.team2[0].baseMoveSpeed = 4

    const firstStrike = runFight(profile, 7).collisions[0].steps
      .find(step => step.kind === 'block' || step.kind === 'damage')

    expect(firstStrike?.actorName).toBe(profile.team2[0].name)
    expect(firstStrike?.time).toBe(0.25)
  })

  it('selects the greatest attack-minus-resistance value when cutting cannot penetrate', () => {
    const profile = oneOnOneProfile()
    profile.weapons = [
      testWeapon('technique', { ...emptyDamage(), Дробящее: 6, Колющее: 5 }, 1),
      testWeapon('defender', emptyDamage(), 1),
    ]
    profile.team1[0].primaryWeaponId = 'technique'
    profile.team2[0].primaryWeaponId = 'defender'
    profile.team1[0].mastery = true

    const firstStrike = runFight(profile, 11).collisions[0].steps
      .find(step => step.kind === 'block' || step.kind === 'damage')

    expect(firstStrike?.technique).toBe('Дробящее')
  })
})
