import { reactive } from 'vue'
import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { cloneEmpiresConfig, validateEmpiresConfig } from './config'
import type { EmpiresEndgameConfig } from './types'

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

describe('Empire\'s Endgame configuration', () => {
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
      'gift-combat-spirit',
      'gift-fish-currents',
      'gift-meteor-iron',
      'gift-desert-tsunami',
      'relic-epidemic-ward',
      'relic-spirit-floor',
      'relic-tithe',
      'relic-resource-exemption',
    ]

    for (const giftId of deferredIds) {
      expect(gift(config, giftId).deferredReason?.trim()).toBeTruthy()
    }
    expect(config.gifts.definitions.filter(definition => !definition.deferredReason).length)
      .toBeGreaterThanOrEqual(config.gifts.choiceCount)
  })

  it('exposes an exact explicit boundary for every bundled future-content catalog', () => {
    const config = makeConfig()
    const deferred = <T extends { id: string, deferredReason?: string }>(items: T[]) =>
      items.filter(item => item.deferredReason).map(item => item.id)

    expect(deferred(config.empire.buildings)).toEqual([
      'building-smithy',
      'building-barracks',
      'building-temple',
      'building-bank',
      'building-fair',
      'building-tavern',
      'building-stable',
      'building-alchemy',
      'building-hospital',
      'building-customs',
      'building-medical-academy',
      'building-military-academy',
      'building-foundry',
      'building-sea-port',
      'building-jewish-bank',
      'municipal-capital-forum',
    ])
    expect(deferred(config.empire.units ?? [])).toEqual([
      'unit-light',
      'unit-regular',
      'unit-heavy',
      'unit-knight',
    ])
    expect(deferred(config.empire.events)).toEqual([
      'event-northern-raids',
      'event-lumber-concession',
      'event-customs-smuggling',
      'event-golden-idol',
      'event-city-gates-epidemic',
      'event-horse-theft',
      'event-bank-insurance',
      'event-white-stone',
      'event-witch-apprenticeship',
    ])
    expect(deferred(config.empire.resources)).toEqual(['carpentry', 'whiteStone'])
    expect(deferred(config.empire.technologies)).toEqual([
      'doctrine-war',
      'tech-fair',
      'tech-ironwork',
      'tech-compass',
      'tech-merchant-guilds',
      'tech-banking',
      'tech-generals',
      'tech-foundry',
      'tech-military-logistics',
      'tech-supply-corps',
      'reform-coercion',
      'reform-heroic-funerals',
      'reform-control-smiths',
      'reform-theocracy',
      'reform-technocracy',
      'reform-city-gates',
      'steel-laurel-spearhead',
      'steel-lancet-spearhead',
      'steel-diamond-spearhead',
      'steel-cross-spearhead',
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
      'card-clubs-2.normal',
      'card-clubs-8.normal',
      'card-clubs-8.inverted',
      'card-diamonds-6.inverted',
      'card-diamonds-ace.normal',
      'card-spades-5.normal',
      'card-spades-5.inverted',
      'card-spades-8.normal',
      'card-spades-8.inverted',
      'card-joker-jester.normal',
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
})
