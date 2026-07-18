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

  it('marks future-mode rewards as deferred while keeping implemented rewards draftable', () => {
    const config = makeConfig()
    const deferredIds = [
      'gift-earthquake',
      'gift-tailwind',
      'gift-fish-currents',
      'gift-meteor-iron',
      'gift-desert-tsunami',
    ]

    for (const giftId of deferredIds) {
      expect(gift(config, giftId).deferredReason?.trim()).toBeTruthy()
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

    expect(deferred(config.empire.buildings)).toEqual([
      'building-military-academy',
      'municipal-capital-forum',
    ])
    expect(deferred(config.empire.units ?? [])).toEqual([])
    expect(deferred(config.empire.events)).toEqual([
      'event-lumber-concession',
      'event-city-gates-epidemic',
      'event-white-stone',
    ])
    expect(deferred(config.empire.resources)).toEqual(['carpentry', 'whiteStone'])
    expect(deferred(config.empire.technologies)).toEqual([
      'tech-printing',
      'reform-technocracy',
      'reform-city-gates',
      'steel-voulge',
      'steel-halberd',
      'steel-lance',
      'steel-butted-mail',
      'steel-riveted-mail',
      'steel-full-mail',
      'steel-double-mail',
      'steel-steel-mail',
      'steel-nasal-helm',
      'steel-bucket-helm',
      'steel-kettle-hat',
      'steel-iron-breastplate',
      'steel-steel-cuirass',
      'steel-water-hammer',
      'steel-heavy-water-hammer',
      'steel-ship-cannon',
      'steel-hand-bombard',
      'steel-arquebus',
    ])

    const activeFaces = config.cards.flatMap(card =>
      (['normal', 'inverted'] as const)
        .filter(side => !card[side].deferredReason)
        .map(side => `${card.id}.${side}`))
    expect(activeFaces).toEqual([
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

  it('ships the enabled authored combat catalog without un-deferring gameplay carriers', () => {
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
    expect(config.combat.equipment).toHaveLength(32)
    expect(combatWeapon(config, 'weapon-horseman-pick').damageLevels).toEqual({
      impact: 6,
      piercing: 4,
      crushing: 3,
    })
    expect(config.combat.equipment.filter(item => item.deferredReason).map(item => item.id)).toEqual([
      'weapon-long-sword',
      'weapon-ice-pick',
      'weapon-lancet-arrow',
      'armor-butted-mail',
      'armor-brigandine',
      'armor-padded-jack',
      'armor-iron-breastplate',
      'shield-generic',
      'weapon-misericorde',
      'weapon-desmond-fork',
    ])
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
})
