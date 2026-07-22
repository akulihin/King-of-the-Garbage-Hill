import { reactive } from 'vue'
import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import {
  EMPIRES_ACTIVE_MINIGAME_CONFIG_ERROR,
  cloneEmpiresConfig,
  empiresConfigReplacementDisabledReason,
  validateEmpiresConfig,
} from './config'
import type { CombatArmorProfile, CombatWeaponProfile, EmpiresEndgameConfig } from './types'

function makeConfig(): EmpiresEndgameConfig {
  return JSON.parse(JSON.stringify(defaultConfigJson)) as EmpiresEndgameConfig
}

function gift(config: EmpiresEndgameConfig, giftId: string) {
  const definition = config.gifts.definitions.find(item => item.id === giftId)
  if (!definition) throw new Error(`Missing fixture gift ${giftId}`)
  return definition
}

function levelOneDependencies(config: EmpiresEndgameConfig, buildingId: string) {
  const building = config.empire.buildings.find(item => item.id === buildingId)
  if (!building) throw new Error(`Missing fixture building ${buildingId}`)
  const level = building.levels.find(item => item.level === 1)
  if (!level) throw new Error(`Missing level 1 for fixture building ${buildingId}`)
  return level.dependencies
}

function combatWeapon(config: EmpiresEndgameConfig, equipmentId: string): CombatWeaponProfile {
  const equipment = config.combat.equipment.find(item => item.id === equipmentId)
  if (!equipment || equipment.kind !== 'weapon' || !('damageLevels' in equipment.profile)) {
    throw new Error(`Missing combat weapon fixture ${equipmentId}`)
  }
  return equipment.profile
}

function combatArmor(config: EmpiresEndgameConfig, equipmentId: string): CombatArmorProfile {
  const equipment = config.combat.equipment.find(item => item.id === equipmentId)
  if (!equipment || equipment.kind === 'weapon' || !('classId' in equipment.profile)) {
    throw new Error(`Missing combat armor fixture ${equipmentId}`)
  }
  return equipment.profile
}

describe('Empire\'s Endgame configuration', () => {
  it('rejects config replacement while a minigame session owns the active rules', () => {
    expect(empiresConfigReplacementDisabledReason({ phase: 'minigame', minigame: null }))
      .toBe(EMPIRES_ACTIVE_MINIGAME_CONFIG_ERROR)
    expect(empiresConfigReplacementDisabledReason({ phase: 'cards', minigame: null })).toBeNull()
  })

  it('clones a Vue-reactive constructor definition without DataCloneError', () => {
    const source = reactive(defaultConfigJson as unknown as EmpiresEndgameConfig)

    const clone = cloneEmpiresConfig(source)

    expect(clone).not.toBe(source)
    expect(clone.cards).not.toBe(source.cards)
    expect(clone.cards).toHaveLength(53)
    expect(() => validateEmpiresConfig(clone)).not.toThrow()
  })

  it('wires every authored target contract to its explicit resolution mechanic', () => {
    const config = makeConfig()

    expect(gift(config, 'gift-resource-grant').resolution).toEqual({ kind: 'cityResources' })
    expect(gift(config, 'gift-meteor')).toMatchObject({
      resolution: { kind: 'meteorCity', damageLevels: 1 },
      effects: [
        { kind: 'resource', resourceId: 'iron', amount: 500000 },
      ],
    })
    expect(gift(config, 'gift-meteor-iron').resolution).toEqual({ kind: 'cityResources' })

    for (const regionId of ['west', 'north', 'south', 'east']) {
      expect(gift(config, `gift-sacrifice-${regionId}`).resolution).toEqual({
        kind: 'destroyRegion',
        regionId,
      })
    }
    expect(gift(config, 'relic-production-levels')).toMatchObject({
      resolution: {
        kind: 'buildingLevelBonus',
        slots: ['farm', 'lumber'],
        amount: 1,
      },
      effects: [],
    })
    expect(() => validateEmpiresConfig(config)).not.toThrow()
  })

  it('keeps every accepted reward draftable', () => {
    const config = makeConfig()
    const acceptedIds = [
      'gift-earthquake',
      'gift-tailwind',
      'gift-fish-currents',
      'gift-meteor-iron',
      'gift-desert-tsunami',
    ]

    for (const giftId of acceptedIds) {
      expect(gift(config, giftId).deferredReason).toBeUndefined()
    }
    expect(gift(config, 'relic-tithe').deferredReason).toBeUndefined()
    expect(gift(config, 'relic-resource-exemption').deferredReason).toBeUndefined()
    expect(config.gifts.definitions.filter(definition => !definition.deferredReason).length)
      .toBeGreaterThanOrEqual(config.gifts.choiceCount)
  })

  it('exposes an exact explicit boundary for every bundled future-content catalog', () => {
    const config = makeConfig()
    const deferred = <T extends { id: string, deferredReason?: string }>(items: T[]) =>
      items.filter(item => item.deferredReason).map(item => item.id)

    expect(deferred(config.empire.buildings)).toEqual([])
    expect(deferred(config.empire.units ?? [])).toEqual([])
    expect(deferred(config.empire.events)).toEqual([])
    expect(deferred(config.empire.resources)).toEqual([])
    expect(deferred(config.empire.technologies.filter(technology => !technology.steel))).toEqual([])

    const activeFaces = config.cards.flatMap(card =>
      (['normal', 'inverted'] as const)
        .filter(side => !card[side].deferredReason)
        .map(side => `${card.id}.${side}`))
    expect(activeFaces).toHaveLength(106)
    expect(new Set(activeFaces)).toHaveLength(106)
  })

  it('gives every newly accepted standard face an executable suit default', () => {
    const config = makeConfig()
    const previouslyLive = new Set([
      'card-clubs-8.normal',
      'card-clubs-8.inverted',
      'card-diamonds-ace.inverted',
      'card-hearts-7.normal',
      'card-hearts-7.inverted',
      'card-spades-3.normal',
      'card-spades-5.normal',
      'card-spades-8.normal',
      'card-spades-10.normal',
      'card-spades-10.inverted',
    ])
    let acceptedFaces = 0

    for (const card of config.cards) {
      for (const side of ['normal', 'inverted'] as const) {
        const key = `${card.id}.${side}`
        const face = card[side]
        expect(face.deferredReason, key).toBeUndefined()
        if (previouslyLive.has(key)) continue
        acceptedFaces += 1
        expect(face.description, key).not.toMatch(/пока не определ|Настройте его в редакторе/)
        expect(face.effects.length, key).toBeGreaterThan(0)

        const hasSuitDefault = card.suit === 'clubs'
          ? face.effects.some(effect => effect.kind === 'loyaltyAllCities')
          : card.suit === 'diamonds'
            ? face.effects.some(effect => (
                effect.kind === 'resourceMultiplier' && effect.resourceId === 'gold'
              ))
            : card.suit === 'hearts'
              ? face.effects.some(effect => effect.kind === 'population' && !effect.cityId)
              : card.suit === 'spades'
                ? face.effects.some(effect => (
                    effect.kind === 'resource' && effect.resourceId === 'knowledge'
                  ))
                : face.effects.some(effect => effect.kind === 'time')
        expect(hasSuitDefault, key).toBe(true)
      }
    }

    expect(acceptedFaces).toBe(96)
    expect(config.cards.find(card => card.id === 'card-clubs-2')?.inverted.effects)
      .toContainEqual({
        kind: 'flag',
        flagId: 'streetCleanliness',
        amount: -1,
        amountPerLevel: -1,
      })
    expect(config.cards.find(card => card.id === 'card-hearts-ace')?.normal.effects)
      .toEqual(expect.arrayContaining([
        expect.objectContaining({ kind: 'flag', flagId: 'unitMoraleEnabled' }),
        expect.objectContaining({ kind: 'flag', flagId: 'unitActivesEnabled' }),
      ]))
    expect(() => validateEmpiresConfig(config)).not.toThrow()
  })

  it('rejects malformed future-content labels and unsupported live flags', () => {
    const emptyReason = makeConfig()
    emptyReason.empire.technologies[0].deferredReason = '   '
    expect(() => validateEmpiresConfig(emptyReason)).toThrow(
      /technology doctrine-general deferredReason must be a non-empty string/,
    )

    const unsupported = makeConfig()
    unsupported.cards.find(card => card.id === 'card-clubs-8')!.normal.effects.push({
      kind: 'flag',
      flagId: 'silentFutureFlag',
      amount: 1,
    })
    expect(() => validateEmpiresConfig(unsupported)).toThrow(
      /unsupported live flag silentFutureFlag/,
    )

    const explicitlyDeferred = makeConfig()
    const face = explicitlyDeferred.cards.find(card => card.id === 'card-clubs-8')!.normal
    face.effects.push({ kind: 'flag', flagId: 'futureFlag', amount: 1 })
    face.deferredReason = 'Будущий системный режим.'
    expect(() => validateEmpiresConfig(explicitlyDeferred)).not.toThrow()
  })

  it('requires the authored technology unlocks for level-one fair, temple, smithy, and bank', () => {
    const config = makeConfig()
    const expectedUnlocks = {
      'building-fair': 'tech-fair',
      'building-temple': 'tech-temple',
      'building-smithy': 'tech-ironwork',
      'building-bank': 'tech-banking',
    }

    for (const [buildingId, technologyId] of Object.entries(expectedUnlocks)) {
      expect(levelOneDependencies(config, buildingId)).toContainEqual({
        kind: 'technology',
        technologyId,
      })
    }
  })

  it('rejects invalid gift resolution references and values', () => {
    const unknownRegion = makeConfig()
    gift(unknownRegion, 'gift-sacrifice-west').resolution = {
      kind: 'destroyRegion',
      regionId: 'missing-region',
    }
    expect(() => validateEmpiresConfig(unknownRegion)).toThrow(/unknown region missing-region/)

    const zeroDamage = makeConfig()
    gift(zeroDamage, 'gift-meteor').resolution = { kind: 'meteorCity', damageLevels: 0 }
    expect(() => validateEmpiresConfig(zeroDamage)).toThrow(/damageLevels must be a positive integer/)

    const unknownSlot = makeConfig()
    gift(unknownSlot, 'relic-production-levels').resolution = {
      kind: 'buildingLevelBonus',
      slots: ['unknown-slot' as 'farm'],
      amount: 1,
    }
    expect(() => validateEmpiresConfig(unknownSlot)).toThrow(/references an unknown slot/)

    const unknownResource = makeConfig()
    const resourceEffect = gift(unknownResource, 'gift-resource-grant').effects[0]
    if (resourceEffect?.kind !== 'resource') throw new Error('Missing targeted resource fixture')
    resourceEffect.resourceId = 'missing-resource'
    expect(() => validateEmpiresConfig(unknownResource)).toThrow(/unknown resource missing-resource/)
  })

  it('rejects target mechanics when the scenario has no city and too few draftable gifts', () => {
    const noCityTargets = makeConfig()
    noCityTargets.empire.cities = []
    expect(() => validateEmpiresConfig(noCityTargets)).toThrow(/requires at least one city target/)

    const noDraft = makeConfig()
    for (const definition of noDraft.gifts.definitions) {
      definition.deferredReason = 'Будущий режим.'
    }
    expect(() => validateEmpiresConfig(noDraft)).toThrow(
      /without deferredReason must contain at least choiceCount entries/,
    )

    const relicOnlyDraft = makeConfig()
    for (const definition of relicOnlyDraft.gifts.definitions) {
      if (definition.kind !== 'relic') definition.deferredReason = 'Будущий режим.'
    }
    expect(() => validateEmpiresConfig(relicOnlyDraft)).toThrow(
      /pre-unlock non-relic gift definitions/,
    )
  })

  it('ships the enabled authored combat catalog with accepted equipment defaults', () => {
    const config = makeConfig()

    expect(config.combat.enabled).toBe(true)
    expect(config.combat.damageTypes.map(type => type.name)).toEqual([
      'Ударное',
      'Дробящее',
      'Рубящее',
      'Режущее',
      'Колющее',
    ])
    expect(config.combat.counterRules).toHaveLength(17)
    expect(config.combat.equipment).toHaveLength(45)
    expect(combatWeapon(config, 'weapon-horseman-pick').damageLevels).toEqual({
      impact: 6,
      piercing: 4,
      crushing: 3,
    })
    expect(config.combat.equipment.filter(item => item.deferredReason)).toEqual([])
    expect(combatWeapon(config, 'weapon-long-sword').damageLevels).toEqual({
      chopping: 3,
      cutting: 1,
      piercing: 4,
      impact: 2,
      crushing: 2,
    })
    expect(combatWeapon(config, 'weapon-ice-pick').damageLevels).toEqual({ piercing: 4, impact: 2 })
    expect(combatWeapon(config, 'weapon-lancet-arrow').damageLevels).toEqual({ piercing: 3, cutting: 1 })
    expect(combatWeapon(config, 'weapon-misericorde').damageLevels).toEqual({ piercing: 5, cutting: 1 })
    expect(combatWeapon(config, 'weapon-desmond-fork').damageLevels).toEqual({ piercing: 4, impact: 2 })
    expect(combatArmor(config, 'armor-butted-mail')).toEqual({ classId: 'mail', level: 1 })
    expect(combatArmor(config, 'armor-brigandine')).toEqual({ classId: 'brigandine', level: 4 })
    expect(combatArmor(config, 'armor-padded-jack')).toEqual({ classId: 'textile', level: 2 })
    expect(combatArmor(config, 'armor-iron-breastplate')).toEqual({ classId: 'plate', level: 3 })
    expect(combatArmor(config, 'shield-generic')).toEqual({
      classId: 'shield',
      level: 2,
      tags: ['blocks-arrows'],
    })
    expect(() => validateEmpiresConfig(config)).not.toThrow()
  })

  it('rejects duplicate and malformed combat catalog values', () => {
    const duplicateDamageType = makeConfig()
    duplicateDamageType.combat.damageTypes[1].id = duplicateDamageType.combat.damageTypes[0].id
    expect(() => validateEmpiresConfig(duplicateDamageType)).toThrow(/damageTypes repeats id/)

    const negativeDamage = makeConfig()
    combatWeapon(negativeDamage, 'weapon-mace').damageLevels.impact = -1
    expect(() => validateEmpiresConfig(negativeDamage)).toThrow(/finite and non-negative/)

    const infiniteDamage = makeConfig()
    combatWeapon(infiniteDamage, 'weapon-mace').damageLevels.impact = Number.POSITIVE_INFINITY
    expect(() => validateEmpiresConfig(infiniteDamage)).toThrow(/finite and non-negative/)

    const negativeArmor = makeConfig()
    combatArmor(negativeArmor, 'armor-butted-mail').level = -1
    expect(() => validateEmpiresConfig(negativeArmor)).toThrow(/level must be finite and non-negative/)
  })

  it('rejects unknown combat references, including technology and tag endpoints', () => {
    const unknownDamageType = makeConfig()
    combatWeapon(unknownDamageType, 'weapon-mace').damageLevels.missing = 1
    expect(() => validateEmpiresConfig(unknownDamageType)).toThrow(/unknown damage type missing/)

    const unknownArmorClass = makeConfig()
    combatArmor(unknownArmorClass, 'armor-butted-mail').classId = 'missing-armor'
    expect(() => validateEmpiresConfig(unknownArmorClass)).toThrow(/unknown armor class missing-armor/)

    const unknownRuleEndpoint = makeConfig()
    const rule = unknownRuleEndpoint.combat.counterRules.find(item =>
      item.kind === 'damageTypeCountersArmor')
    if (!rule || rule.kind !== 'damageTypeCountersArmor') throw new Error('Missing counter fixture')
    rule.damageTypeId = 'missing-damage'
    expect(() => validateEmpiresConfig(unknownRuleEndpoint)).toThrow(/unknown damage type missing-damage/)

    const unknownTechnology = makeConfig()
    const equipment = unknownTechnology.combat.equipment.find(item => item.id === 'weapon-halberd')
    if (!equipment) throw new Error('Missing technology equipment fixture')
    equipment.technologyId = 'missing-technology'
    expect(() => validateEmpiresConfig(unknownTechnology)).toThrow(/unknown technology missing-technology/)

    const unknownTag = makeConfig()
    const tagRule = unknownTag.combat.counterRules.find(item =>
      item.kind === 'weaponTagCountersAllArmor')
    if (!tagRule || tagRule.kind !== 'weaponTagCountersAllArmor') {
      throw new Error('Missing tag counter fixture')
    }
    tagRule.weaponTag = 'missing-tag'
    expect(() => validateEmpiresConfig(unknownTag)).toThrow(/unknown weapon tag missing-tag/)
  })

  it('rejects contradictory mixed and two-type profile shapes', () => {
    const contradictory = makeConfig()
    const contradictoryProfile = combatWeapon(contradictory, 'weapon-estoc')
    contradictoryProfile.mixed = true
    contradictoryProfile.twoTyped = true
    expect(() => validateEmpiresConfig(contradictory)).toThrow(/cannot be both mixed and twoTyped/)

    const wrongTwoTypeCount = makeConfig()
    combatWeapon(wrongTwoTypeCount, 'weapon-horseman-pick').twoTyped = true
    expect(() => validateEmpiresConfig(wrongTwoTypeCount)).toThrow(/exactly two damage types/)

    const oneTypeMixed = makeConfig()
    combatWeapon(oneTypeMixed, 'weapon-mace').mixed = true
    expect(() => validateEmpiresConfig(oneTypeMixed)).toThrow(/at least two damage types/)
  })

  it('ships six completed regional TD grade sets and explicit northern non-applicability', () => {
    const config = makeConfig()
    const towerById = new Map(config.td.towers.map(tower => [tower.id, tower]))
    const expectedGradeOne = [
      { cost: 15, maxHpBonus: 0, rangeBonus: 90, attackIntervalTicksDelta: 0, damageLevelBonuses: {} },
      { cost: 15, maxHpBonus: 75, rangeBonus: 0, attackIntervalTicksDelta: 0, damageLevelBonuses: {} },
      { cost: 15, maxHpBonus: 0, rangeBonus: 0, attackIntervalTicksDelta: -4, damageLevelBonuses: {} },
      { cost: 15, maxHpBonus: 0, rangeBonus: 0, attackIntervalTicksDelta: 0, damageLevelBonuses: { impact: 1 } },
    ]
    const expectedGradeFour = [
      { cost: 30, maxHpBonus: 0, rangeBonus: 0, attackIntervalTicksDelta: 0, damageLevelBonuses: { impact: 3 } },
      { cost: 30, maxHpBonus: 0, rangeBonus: 100, attackIntervalTicksDelta: 0, damageLevelBonuses: {} },
      { cost: 30, maxHpBonus: 0, rangeBonus: 0, attackIntervalTicksDelta: -6, damageLevelBonuses: {} },
      { cost: 30, maxHpBonus: 150, rangeBonus: 0, attackIntervalTicksDelta: 0, damageLevelBonuses: {} },
    ]

    expect(config.td.towers).toHaveLength(40)
    for (const regionId of ['east', 'west', 'south']) {
      for (const [grade, expected] of [[1, expectedGradeOne], [4, expectedGradeFour]] as const) {
        const set = config.td.gradeChoices!.find(candidate => (
          candidate.regionId === regionId && candidate.grade === grade
        ))!
        expect(set.choiceIds).toHaveLength(4)
        expect(set.deferredReason).toBeUndefined()
        expect(set.choiceIds.map((choiceId) => {
          const tower = towerById.get(choiceId)!
          return {
            cost: tower.cost,
            maxHpBonus: tower.maxHpBonus,
            rangeBonus: tower.rangeBonus,
            attackIntervalTicksDelta: tower.attackIntervalTicksDelta,
            damageLevelBonuses: tower.damageLevelBonuses,
          }
        })).toEqual(expected)
      }
    }

    const northSets = config.td.gradeChoices!.filter(set => set.regionId === 'north')
    expect(northSets).toHaveLength(4)
    expect(northSets.every(set => (
      set.availability === 'notApplicable'
      && set.choiceIds.length === 0
      && Boolean(set.reason?.trim())
      && !set.deferredReason
    ))).toBe(true)
    expect(config.td.gradeChoices!.filter(set => set.deferredReason)).toEqual([])
    expect(() => validateEmpiresConfig(config)).not.toThrow()
  })

  it('rejects malformed not-applicable TD grade contracts', () => {
    const missingReason = makeConfig()
    const missingReasonSet = missingReason.td.gradeChoices!.find(set => set.id === 'north-grade-1')!
    missingReasonSet.reason = '   '
    expect(() => validateEmpiresConfig(missingReason)).toThrow(/reason must be non-empty/)

    const exposedChoice = makeConfig()
    const exposedChoiceSet = exposedChoice.td.gradeChoices!.find(set => set.id === 'north-grade-1')!
    exposedChoiceSet.choiceIds.push('tower-g1-height')
    expect(() => validateEmpiresConfig(exposedChoice)).toThrow(/must be unavailable/)

    const misplacedReason = makeConfig()
    const misplacedReasonSet = misplacedReason.td.gradeChoices!.find(set => set.id === 'east-grade-1')!
    misplacedReasonSet.reason = 'Only not-applicable sets may explain their absence.'
    expect(() => validateEmpiresConfig(misplacedReason)).toThrow(/only valid when not applicable/)
  })
})
