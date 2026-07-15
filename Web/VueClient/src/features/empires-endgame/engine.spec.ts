import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { EmpiresEndgameEngine, validateEmpiresEndgameConfig } from './engine'
import { EMPIRES_RANKS, EMPIRES_SUITS } from './types'
import type {
  EmpiresActor,
  EmpiresCampaignState,
  EmpiresCardDefinition,
  EmpiresEndgameConfig,
} from './types'

function makeCards(): EmpiresCardDefinition[] {
  const regular = EMPIRES_SUITS.flatMap(suit => EMPIRES_RANKS.map(rank => ({
    id: `${suit}-${rank}`,
    suit,
    rank,
    name: `${suit} ${rank}`,
    value: EMPIRES_RANKS.indexOf(rank) + 2,
    timeCostDays: 1,
    drawUpgrade: 1,
    normal: { title: 'Normal', description: 'Normal', effects: [] },
    inverted: { title: 'Inverted', description: 'Inverted', effects: [] },
  })))
  return [
    ...regular,
    {
      id: 'joker',
      suit: 'joker',
      rank: 'joker',
      name: 'Joker',
      value: 20,
      timeCostDays: 1,
      drawUpgrade: 1,
      normal: { title: 'Joker', description: 'Joker', effects: [] },
      inverted: { title: 'Inverted Joker', description: 'Inverted Joker', effects: [] },
    },
  ]
}

function makeConfig(): EmpiresEndgameConfig {
  return {
    schemaVersion: 1,
    id: 'engine-test',
    title: "Empire's Endgame",
    seed: 'deterministic-test',
    cards: makeCards(),
    durak: {
      handSize: 6,
      maxAttackCards: 6,
      boutsPerCon: 10,
      initialAttacker: 'player',
      fixedTrumpSuit: 'spades',
      simultaneousEmptyWinner: 'attacker',
      joker: {
        rule: 'highest-unbeatable',
        canAttack: true,
        canThrowIn: true,
        canDefend: true,
        trumpFallbackSuit: 'spades',
      },
      scoringRules: [{
        id: 'defend-once',
        metric: 'successfulDefenses',
        comparison: 'gte',
        threshold: 1,
        points: 1,
      }],
    },
    upgrades: { improveCost: 1, restoreCost: 1, defaultMaxLevel: 5 },
    gifts: {
      choiceCount: 3,
      definitions: [
        { id: 'gift-a', name: 'A', description: 'A', kind: 'boon', application: 'once', baseWeight: 1, performanceWeight: 1, effects: [] },
        { id: 'gift-b', name: 'B', description: 'B', kind: 'relic', application: 'eachEmpire', baseWeight: 1, performanceWeight: 0, effects: [] },
        { id: 'gift-c', name: 'C', description: 'C', kind: 'empire', application: 'once', baseWeight: 1, performanceWeight: 0, effects: [] },
        { id: 'gift-d', name: 'D', description: 'D', kind: 'catastrophe', application: 'once', baseWeight: 1, performanceWeight: -0.1, effects: [] },
      ],
    },
    empire: {
      daysPerPhase: 59,
      foodResourceId: 'food',
      eventChance: 0,
      defeatPopulationAtOrBelow: 0,
      lockProviderBuildingIds: { mine: 'mine', lumber: 'lumber' },
      resources: [
        { id: 'wood', name: 'Wood' },
        { id: 'iron', name: 'Iron' },
        { id: 'food', name: 'Food' },
      ],
      initialResources: { wood: 20, iron: 20 },
      initialFlags: {},
      map: {
        width: 100,
        height: 100,
        viewportWorldFraction: 0.2,
        projection: 'fixed-oblique',
        regions: [{
          id: 'central',
          name: 'Central',
          biome: 'central',
          center: { x: 50, y: 50 },
          polygon: [],
          subregionIds: [],
          cityIds: ['capital'],
        }],
        subregions: [],
        objects: [],
      },
      populationClasses: [{
        id: 'workers',
        name: 'Workers',
        canWork: true,
        canRecruit: false,
        foodPerPerson: 1,
        workerPriority: 1,
      }],
      cities: [{
        id: 'capital',
        name: 'Capital',
        regionId: 'central',
        position: { x: 50, y: 50 },
        population: 1000,
        militaryPopulation: 0,
        populationClasses: { workers: 1000 },
        baseProduction: { food: 500 },
        buildingLevels: { mine: 1, lumber: 1, smithy: 0, barracks: 0, temple: 0 },
        slots: [
          { id: 'slot-mine', kind: 'mine', buildingId: 'mine', position: { x: 10, y: 10 } },
          { id: 'slot-lumber', kind: 'lumber', buildingId: 'lumber', position: { x: 20, y: 10 } },
          { id: 'slot-smithy', kind: 'smithy', buildingId: 'smithy', position: { x: 30, y: 10 } },
          { id: 'slot-barracks', kind: 'barracks', buildingId: 'barracks', position: { x: 40, y: 10 } },
          { id: 'slot-unique', kind: 'unique', buildingId: 'temple', position: { x: 50, y: 10 } },
          { id: 'slot-municipal', kind: 'municipal', position: { x: 60, y: 10 } },
        ],
      }],
      buildings: [
        {
          id: 'mine',
          name: 'Mine',
          slot: 'mine',
          levels: [{
            level: 1,
            timeCostDays: 0,
            foodCost: 0,
            resourceCosts: [],
            dependencies: [],
            facilityLocks: [],
            workerDemand: 10,
            production: [{ resourceId: 'iron', amount: 5 }],
          }],
        },
        {
          id: 'lumber',
          name: 'Lumber',
          slot: 'lumber',
          levels: [{
            level: 1,
            timeCostDays: 0,
            foodCost: 0,
            resourceCosts: [],
            dependencies: [],
            facilityLocks: [],
            workerDemand: 10,
            production: [{ resourceId: 'wood', amount: 5 }],
          }],
        },
        {
          id: 'smithy',
          name: 'Smithy',
          slot: 'smithy',
          levels: [{
            level: 1,
            timeCostDays: 5,
            foodCost: 0,
            resourceCosts: [{ resourceId: 'wood', amount: 10 }],
            dependencies: [{ kind: 'building', buildingId: 'mine', level: 1 }],
            facilityLocks: ['mine'],
            workerDemand: 20,
          }],
        },
        {
          id: 'barracks',
          name: 'Barracks',
          slot: 'barracks',
          levels: [{
            level: 1,
            timeCostDays: 3,
            foodCost: 0,
            resourceCosts: [],
            dependencies: [{ kind: 'building', buildingId: 'smithy', level: 1 }],
            facilityLocks: [],
            workerDemand: 10,
          }],
        },
        {
          id: 'temple',
          name: 'Temple',
          slot: 'unique',
          levels: [{
            level: 1,
            timeCostDays: 3,
            foodCost: 0,
            resourceCosts: [],
            dependencies: [],
            facilityLocks: ['mine'],
            workerDemand: 10,
          }],
        },
      ],
      technologies: [
        {
          id: 'metallurgy',
          name: 'Metallurgy',
          category: 'technology',
          timeCostDays: 2,
          resourceCosts: [],
          prerequisites: [],
          effects: [],
        },
        {
          id: 'steel',
          name: 'Steel',
          category: 'steel',
          timeCostDays: 4,
          resourceCosts: [{ resourceId: 'iron', amount: 10 }],
          prerequisites: [
            { kind: 'technology', technologyId: 'metallurgy' },
            { kind: 'building', buildingId: 'smithy', level: 1, scope: 'anyCity' },
          ],
          effects: [],
        },
      ],
      events: [
        {
          id: 'event-a',
          name: 'Event A',
          description: 'A',
          weight: 1,
          choices: [{ id: 'event-a-choice', label: 'A', effects: [] }],
        },
        {
          id: 'event-b',
          name: 'Event B',
          description: 'B',
          weight: 3,
          choices: [{ id: 'event-b-choice', label: 'B', effects: [] }],
        },
      ],
    },
  }
}

interface RoundSetup {
  attacker: EmpiresActor
  playerHand: string[]
  godHand: string[]
  deck?: string[]
}

function setupRound(engine: EmpiresEndgameEngine, setup: RoundSetup): void {
  const state = engine.snapshot()
  state.phase = 'cards'
  state.durak.attacker = setup.attacker
  state.durak.defender = setup.attacker === 'player' ? 'god' : 'player'
  state.durak.stage = 'attack'
  state.durak.table = []
  state.durak.discard = []
  state.durak.playerHand = [...setup.playerHand]
  state.durak.godHand = [...setup.godHand]
  state.durak.deck = [...(setup.deck ?? ['clubs-ace'])]
  state.durak.defenderHandAtBoutStart = setup.attacker === 'player'
    ? setup.godHand.length
    : setup.playerHand.length
  state.boutsInCon = 0
  state.performance = {
    successfulDefenses: 0,
    godTakes: 0,
    maxCardsGivenToGodAtOnce: 0,
    cardsGivenToGod: 0,
    cardsTakenByPlayer: 0,
    boutsWon: 0,
    boutsLost: 0,
  }
  engine.restore(state)
}

describe('Empire\'s Endgame Durak', () => {
  it('allows only ranks already on the table for throw-ins', () => {
    const engine = new EmpiresEndgameEngine(makeConfig())
    setupRound(engine, {
      attacker: 'player',
      playerHand: ['hearts-5', 'clubs-5', 'diamonds-7', 'clubs-9'],
      godHand: ['hearts-7', 'clubs-king'],
    })

    expect(engine.playCard('hearts-5').ok).toBe(true)
    expect(engine.playCardFor('god', 'hearts-7').ok).toBe(true)
    expect(engine.legalAttackCardIds('player')).toEqual(expect.arrayContaining(['clubs-5', 'diamonds-7']))
    expect(engine.legalAttackCardIds('player')).not.toContain('clubs-9')
  })

  it('supports same-suit and trump defense plus an unbeatable configurable Joker', () => {
    const engine = new EmpiresEndgameEngine(makeConfig())

    expect(engine.canCardBeat('hearts-7', 'hearts-5')).toBe(true)
    expect(engine.canCardBeat('spades-2', 'hearts-ace')).toBe(true)
    expect(engine.canCardBeat('hearts-ace', 'spades-2')).toBe(false)
    expect(engine.canCardBeat('joker', 'spades-ace')).toBe(true)
    expect(engine.canCardBeat('spades-ace', 'joker')).toBe(false)
  })

  it('refills the bout attacker first, then swaps roles only after a successful defense', () => {
    const config = makeConfig()
    config.durak.handSize = 2
    const engine = new EmpiresEndgameEngine(config)
    setupRound(engine, {
      attacker: 'player',
      playerHand: ['hearts-5'],
      godHand: ['hearts-7'],
      deck: ['clubs-2', 'diamonds-2', 'spades-2', 'clubs-3'],
    })

    engine.playCard('hearts-5')
    engine.playCardFor('god', 'hearts-7')
    expect(engine.endAttack('player').ok).toBe(true)

    expect(engine.state.durak.playerHand).toEqual(['clubs-3', 'spades-2'])
    expect(engine.state.durak.godHand).toEqual(['diamonds-2', 'clubs-2'])
    expect(engine.state.cards['clubs-3']).toMatchObject({ level: 1, inverted: false })
    expect(engine.state.durak.attacker).toBe('god')
    expect(engine.state.durak.defender).toBe('player')
  })

  it('keeps the attacker active for matching throw-ins after God declares a take', () => {
    const engine = new EmpiresEndgameEngine(makeConfig())
    setupRound(engine, {
      attacker: 'player',
      playerHand: ['hearts-5', 'clubs-5', 'diamonds-5', 'diamonds-4'],
      godHand: ['clubs-2', 'clubs-king', 'hearts-ace'],
      deck: ['diamonds-ace'],
    })

    engine.playCard('hearts-5')
    expect(engine.takeCards('god').ok).toBe(true)
    expect(engine.state.durak.stage).toBe('taking')
    expect(engine.currentActor()).toBe('player')
    expect(engine.state.durak.godHand).toHaveLength(3)
    const restoredPendingTake = new EmpiresEndgameEngine(
      makeConfig(),
      JSON.parse(JSON.stringify(engine.snapshot())),
    )
    expect(restoredPendingTake.state.durak.stage).toBe('taking')
    expect(restoredPendingTake.currentActor()).toBe('player')
    expect(restoredPendingTake.legalAttackCardIds('player')).toContain('clubs-5')
    expect(engine.playCard('clubs-5').ok).toBe(true)
    expect(engine.playCard('diamonds-5').ok).toBe(true)
    expect(engine.legalAttackCardIds('player')).toEqual([])
    expect(engine.state.durak.godHand).toHaveLength(3)
    expect(engine.endAttack('player').ok).toBe(true)
    expect(engine.state.durak.attacker).toBe('player')
    expect(engine.state.durak.defender).toBe('god')
    expect(engine.state.performance).toMatchObject({
      godTakes: 1,
      cardsGivenToGod: 3,
      maxCardsGivenToGodAtOnce: 3,
      boutsWon: 1,
    })
  })

  it('lets God add final throw-ins and inverts all God attack cards the player takes', () => {
    const engine = new EmpiresEndgameEngine(makeConfig())
    setupRound(engine, {
      attacker: 'god',
      playerHand: ['clubs-2', 'diamonds-4', 'spades-9'],
      godHand: ['hearts-5', 'clubs-5', 'diamonds-5', 'clubs-king'],
      deck: ['diamonds-ace'],
    })

    engine.playCardFor('god', 'hearts-5')
    expect(engine.takeCards('player').ok).toBe(true)
    expect(engine.state.durak.stage).toBe('taking')
    expect(engine.state.cards['hearts-5'].inverted).toBe(false)
    expect(engine.advanceGod().ok).toBe(true)
    expect(engine.advanceGod().ok).toBe(true)
    expect(engine.state.durak.playerHand).toHaveLength(3)
    expect(engine.advanceGod().ok).toBe(true)
    expect(engine.state.durak.attacker).toBe('god')
    expect(engine.state.cards['hearts-5'].inverted).toBe(true)
    expect(engine.state.cards['clubs-5'].inverted).toBe(true)
    expect(engine.state.cards['diamonds-5'].inverted).toBe(true)
    expect(engine.state.performance).toMatchObject({ cardsTakenByPlayer: 3, boutsLost: 1 })

    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.upgradePoints = 1
    engine.restore(state)
    expect(engine.restoreCard('hearts-5').ok).toBe(true)
    expect(engine.state.cards['hearts-5'].inverted).toBe(false)
  })

  it('moves to three weighted gifts and awards points at the configured con boundary', () => {
    const config = makeConfig()
    config.durak.boutsPerCon = 1
    const engine = new EmpiresEndgameEngine(config)
    setupRound(engine, {
      attacker: 'god',
      playerHand: ['hearts-7', 'diamonds-4'],
      godHand: ['hearts-5', 'clubs-king'],
      deck: ['diamonds-ace'],
    })

    engine.playCardFor('god', 'hearts-5')
    engine.playCard('hearts-7')
    engine.endAttack('god')

    expect(engine.state.phase).toBe('divineGift')
    expect(engine.state.upgradePoints).toBe(1)
    expect(engine.state.giftChoiceIds).toHaveLength(3)
  })
})

describe('default game config integration', () => {
  it('loads the editable 53-card campaign config without engine errors', () => {
    const config = defaultConfigJson as unknown as EmpiresEndgameConfig

    expect(validateEmpiresEndgameConfig(config)).toEqual([])
    const engine = new EmpiresEndgameEngine(config)
    expect(Object.keys(engine.state.cards)).toHaveLength(53)
    const borderCity = engine.state.empire.cities.find(city => city.id === 'city-north-iron-gate')
    expect(borderCity?.population).toBe(500_000)
    expect(borderCity?.operationalBuildingLevels['building-mine']).toBe(0)
    expect(engine.cityProduction('city-north-iron-gate').food).toBe(600_000)
    expect(JSON.parse(JSON.stringify(engine.snapshot()))).toEqual(engine.snapshot())
  })

  it('autoplays the deterministic default campaign through every phase to an outcome', () => {
    const autoplay = () => {
      const config = defaultConfigJson as unknown as EmpiresEndgameConfig
      const engine = new EmpiresEndgameEngine(config)
      const forced = engine.snapshot()
      const cardId = (suit: string, rank: string) => config.cards.find(
        card => card.suit === suit && card.rank === rank,
      )?.id as string
      const playerHand = [
        cardId('clubs', 'ace'),
        cardId('spades', 'ace'),
        cardId('hearts', 'king'),
        cardId('hearts', 'queen'),
        cardId('hearts', 'jack'),
        cardId('hearts', '10'),
      ]
      const godHand = config.cards
        .filter(card => card.suit === 'diamonds' && card.rank !== 'ace')
        .slice(0, config.durak.handSize)
        .map(card => card.id)
      const dealt = new Set([...playerHand, ...godHand])
      forced.phase = 'cards'
      forced.durak.playerHand = playerHand
      forced.durak.godHand = godHand
      forced.durak.deck = config.cards.map(card => card.id).filter(id => !dealt.has(id))
      forced.durak.discard = []
      forced.durak.table = []
      forced.durak.trumpSuit = 'clubs'
      forced.durak.attacker = 'player'
      forced.durak.defender = 'god'
      forced.durak.stage = 'attack'
      forced.durak.defenderHandAtBoutStart = godHand.length
      engine.restore(forced)
      let firstGiftId: string | null = null
      let empireFinishes = 0
      let eventChoices = 0
      let pendingTakeThrowIns = 0
      let steps = 0
      const safetyLimit = 10_000

      while (engine.state.phase !== 'victory' && engine.state.phase !== 'defeat' && steps < safetyLimit) {
        steps += 1
        let result
        if (engine.state.phase === 'cards') {
          const actor = engine.currentActor()
          if (actor === 'god') {
            result = engine.advanceGod()
          } else if (engine.state.durak.stage === 'defense') {
            const defenses = engine.legalDefenseCardIds('player')
            result = defenses.length > 0
              ? engine.playCard(defenses[0])
              : engine.takeCards('player')
          } else if (engine.state.durak.stage === 'taking') {
            const attacks = engine.legalAttackCardIds('player')
            if (attacks.length > 0) pendingTakeThrowIns += 1
            result = attacks.length > 0
              ? engine.playCard(attacks[0])
              : engine.endAttack('player')
          } else if (engine.canEndAttack('player')) {
            result = engine.endAttack('player')
          } else {
            const attacks = engine.legalAttackCardIds('player')
            const hasMatchingCard = (cardId: string) => {
              const rank = engine.getDefinition(cardId).rank
              return engine.state.durak.playerHand.some(
                otherId => otherId !== cardId && engine.getDefinition(otherId).rank === rank,
              )
            }
            const godCannotDefend = (cardId: string) => !engine.state.durak.godHand.some(
              defenseId => engine.canCardBeat(defenseId, cardId),
            )
            const preferredAttack = attacks.find(cardId => hasMatchingCard(cardId) && godCannotDefend(cardId))
              ?? attacks.find(godCannotDefend)
              ?? attacks.find(hasMatchingCard)
              ?? attacks[0]
            result = preferredAttack
              ? engine.playCard(preferredAttack)
              : { ok: false, message: 'Autoplayer found no legal player attack.' }
          }
        } else if (engine.state.phase === 'divineGift') {
          firstGiftId ??= engine.state.giftChoiceIds[0] ?? null
          result = firstGiftId
            ? engine.chooseGift(engine.state.giftChoiceIds[0])
            : { ok: false, message: 'Autoplayer received no divine gifts.' }
        } else if (engine.state.phase === 'empire') {
          empireFinishes += 1
          result = engine.finishEmpire()
        } else {
          const event = engine.config.empire.events.find(item => item.id === engine.state.event?.eventId)
          const affordable = event?.choices.find(choice => (choice.resourceCosts ?? []).every(
            cost => (engine.state.empire.resources[cost.resourceId] ?? 0) >= cost.amount,
          ))
          if (affordable) eventChoices += 1
          result = affordable
            ? engine.chooseEvent(affordable.id)
            : { ok: false, message: 'Autoplayer found no affordable event choice.' }
        }
        if (!result.ok) throw new Error(`Autoplay stalled in ${engine.state.phase}: ${result.message}`)
      }

      return {
        engine,
        firstGiftId,
        empireFinishes,
        eventChoices,
        pendingTakeThrowIns,
        steps,
        safetyLimit,
      }
    }

    const first = autoplay()
    const second = autoplay()
    expect(first.steps).toBeLessThan(first.safetyLimit)
    expect(first.firstGiftId).not.toBeNull()
    expect(first.empireFinishes).toBeGreaterThan(0)
    expect(first.eventChoices).toBeGreaterThan(0)
    expect(first.pendingTakeThrowIns).toBeGreaterThan(0)
    expect(['victory', 'defeat']).toContain(first.engine.state.phase)
    expect(second.engine.snapshot()).toEqual(first.engine.snapshot())
    expect(second).toMatchObject({
      firstGiftId: first.firstGiftId,
      empireFinishes: first.empireFinishes,
      eventChoices: first.eventChoices,
      pendingTakeThrowIns: first.pendingTakeThrowIns,
      steps: first.steps,
    })
  })
})

describe('Empire phase economy', () => {
  it('starts with 59 days minus the time cost of every held card', () => {
    const config = makeConfig()
    const hearts5 = config.cards.find(card => card.id === 'hearts-5') as EmpiresCardDefinition
    const clubs6 = config.cards.find(card => card.id === 'clubs-6') as EmpiresCardDefinition
    hearts5.timeCostDays = 4
    clubs6.timeCostDays = 3
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = ['hearts-5', 'clubs-6']
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a').ok).toBe(true)
    expect(engine.state.phase).toBe('empire')
    expect(engine.state.empire.daysRemaining).toBe(52)
  })

  it('applies one-shot gifts once without registering a recurring empire effect', () => {
    const config = makeConfig()
    config.gifts.definitions[0].effects = [{ kind: 'resource', resourceId: 'wood', amount: 7 }]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    state.durak.playerHand = []
    engine.restore(state)

    engine.chooseGift('gift-a')
    expect(engine.state.empire.resources.wood).toBe(27)
    expect(engine.state.empire.claimedGiftIds).toContain('gift-a')
    expect(engine.state.empire.activeGiftIds).not.toContain('gift-a')

    const next = engine.snapshot()
    next.phase = 'divineGift'
    next.giftChoiceIds = ['gift-c']
    engine.restore(next)
    engine.chooseGift('gift-c')
    expect(engine.state.empire.resources.wood).toBe(27)
  })

  it('clears empire-only production effects before serializing the next card con', () => {
    const config = makeConfig()
    config.gifts.definitions[0].effects = [{
      kind: 'resourceMultiplier',
      resourceId: 'food',
      multiplier: 2,
    }]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = []
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.cityProduction('capital').food).toBe(1000)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('cards')
    expect(engine.state.empire.productionMultipliers).toEqual({})
    expect(engine.cityProduction('capital').food).toBe(500)

    const saved = JSON.parse(JSON.stringify(engine.snapshot()))
    const restored = new EmpiresEndgameEngine(config, saved)
    expect(restored.snapshot()).toEqual(saved)
  })

  it('removes hand-card flag passives after the empire phase without erasing permanent flags', () => {
    const config = makeConfig()
    config.empire.initialFlags = { seasonalFlag: 5 }
    const card = config.cards.find(item => item.id === 'hearts-5') as EmpiresCardDefinition
    card.normal.effects = [{ kind: 'flag', flagId: 'seasonalFlag', amount: 2, amountPerLevel: 1 }]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.cards['hearts-5'].level = 1
    state.durak.playerHand = ['hearts-5']
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.state.empire.flags.seasonalFlag).toBe(8)
    expect(engine.state.empire.cardFlagBonuses).toEqual({ seasonalFlag: 3 })
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.flags.seasonalFlag).toBe(5)
    expect(engine.state.empire.cardFlagBonuses).toEqual({})

    const restored = new EmpiresEndgameEngine(config, JSON.parse(JSON.stringify(engine.snapshot())))
    expect(restored.state.empire.flags.seasonalFlag).toBe(5)
  })

  it('removes half of the food deficit from population at empire end', () => {
    const engine = new EmpiresEndgameEngine(makeConfig())
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 10
    state.empire.cities[0].population = 1000
    state.empire.cities[0].populationClasses = { workers: 1000 }
    state.empire.cities[0].baseProduction.food = 500
    engine.restore(state)

    expect(engine.finishEmpire().ok).toBe(true)
    expect(engine.state.empire.cities[0].population).toBe(750)
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(250)
  })

  it('automatically shuts down the highest operational levels until workers suffice', () => {
    const config = makeConfig()
    config.empire.cities[0].population = 400
    config.empire.cities[0].populationClasses = { workers: 400 }
    config.empire.cities[0].baseProduction = { food: 0, wood: 0, iron: 0 }
    config.empire.cities[0].buildingLevels = {
      farm: 3,
      mine: 2,
      lumber: 1,
      smithy: 0,
      barracks: 0,
      temple: 0,
    }
    config.empire.buildings.push({
      id: 'farm',
      name: 'Farm',
      slot: 'farm',
      levels: [
        { level: 1, timeCostDays: 0, foodCost: 0, resourceCosts: [], dependencies: [], facilityLocks: [], workerDemand: 100, production: [{ resourceId: 'food', amount: 100 }] },
        { level: 2, timeCostDays: 0, foodCost: 0, resourceCosts: [], dependencies: [], facilityLocks: [], workerDemand: 200, production: [{ resourceId: 'food', amount: 200 }] },
        { level: 3, timeCostDays: 0, foodCost: 0, resourceCosts: [], dependencies: [], facilityLocks: [], workerDemand: 300, production: [{ resourceId: 'food', amount: 300 }] },
      ],
    })
    const mine = config.empire.buildings.find(building => building.id === 'mine')
    mine?.levels.push({
      level: 2,
      timeCostDays: 0,
      foodCost: 0,
      resourceCosts: [],
      dependencies: [],
      facilityLocks: [],
      workerDemand: 200,
      production: [{ resourceId: 'iron', amount: 20 }],
    })
    const engine = new EmpiresEndgameEngine(config)
    const city = engine.state.empire.cities[0]

    expect(city.buildingLevels).toMatchObject({ farm: 3, mine: 2, lumber: 1 })
    expect(city.operationalBuildingLevels).toMatchObject({ farm: 2, mine: 1, lumber: 1 })
    expect(engine.cityProduction('capital')).toMatchObject({ food: 200, iron: 5, wood: 5 })

    const restored = new EmpiresEndgameEngine(config, JSON.parse(JSON.stringify(engine.snapshot())))
    expect(restored.state.empire.cities[0].operationalBuildingLevels).toMatchObject({
      farm: 2,
      mine: 1,
      lumber: 1,
    })
  })

  it('allows construction beyond workforce and keeps purchased levels while facilities shut down', () => {
    const config = makeConfig()
    config.empire.cities[0].population = 25
    config.empire.cities[0].populationClasses = { workers: 25 }
    config.empire.cities[0].baseProduction.food = 1000
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.upgradeBuilding('capital', 'smithy')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].buildingLevels).toMatchObject({
      mine: 1,
      lumber: 1,
      smithy: 1,
    })
    expect(engine.state.empire.cities[0].operationalBuildingLevels).toMatchObject({
      mine: 0,
      lumber: 0,
      smithy: 1,
    })
  })

  it('falls back to non-military population when a city has no population-class map', () => {
    const config = makeConfig()
    config.empire.cities[0].population = 25
    config.empire.cities[0].militaryPopulation = 5
    config.empire.cities[0].populationClasses = {}
    const engine = new EmpiresEndgameEngine(config)

    expect(engine.state.empire.cities[0].operationalBuildingLevels).toMatchObject({
      mine: 1,
      lumber: 1,
    })
  })

  it('rebuilds operational production effects after a population-changing gift', () => {
    const config = makeConfig()
    config.empire.cities[0].population = 20
    config.empire.cities[0].populationClasses = { workers: 20 }
    config.empire.cities[0].baseProduction.food = 0
    const mine = config.empire.buildings.find(building => building.id === 'mine')
    if (mine) {
      mine.levels[0].effects = [{
        kind: 'foodProduction',
        cityId: 'capital',
        amount: 100,
      }]
    }
    config.gifts.definitions[0].effects = [
      { kind: 'population', amount: -15 },
      { kind: 'foodProduction', cityId: 'capital', amount: 10 },
    ]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = []
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].population).toBe(5)
    expect(engine.state.empire.cities[0].operationalBuildingLevels.mine).toBe(0)
    expect(engine.cityProduction('capital').food).toBe(10)
  })

  it('enforces building dependencies, costs, time, and per-phase mine locks', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.upgradeBuilding('capital', 'barracks')).toMatchObject({ ok: false })
    expect(engine.upgradeBuilding('capital', 'smithy')).toMatchObject({ ok: true })
    expect(engine.state.empire.resources.wood).toBe(10)
    expect(engine.state.empire.daysRemaining).toBe(54)
    expect(engine.state.empire.cities[0].lockedFacilities.mine).toBe('smithy:1')
    expect(engine.upgradeBuilding('capital', 'temple')).toMatchObject({ ok: false })
    expect(engine.upgradeBuilding('capital', 'barracks')).toMatchObject({ ok: true })
  })

  it('enforces technology prerequisites in a config-driven research graph', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    state.empire.cities[0].buildingLevels.smithy = 1
    engine.restore(state)

    expect(engine.research('steel')).toMatchObject({ ok: false })
    expect(engine.research('metallurgy')).toMatchObject({ ok: true })
    expect(engine.research('steel')).toMatchObject({ ok: true })
    expect(engine.state.empire.researchedTechnologyIds).toEqual(['metallurgy', 'steel'])
  })

  it('recruits configurable units from an operational same-city barracks with per-order time', () => {
    const config = makeConfig()
    config.empire.units = [{
      id: 'levy',
      name: 'Levy',
      description: 'A test formation.',
      image: '/levy.webp',
      foodUpkeep: 100,
      populationCost: 2,
      timeCostDays: 3,
      resourceCosts: [{ resourceId: 'wood', amount: 2 }],
      dependencies: [{ kind: 'building', buildingId: 'barracks', level: 1, scope: 'sameCity' }],
    }]
    config.empire.populationClasses[0].canRecruit = true
    config.empire.cities[0].militaryPopulation = 10
    config.empire.cities[0].baseProduction.food = 1294
    config.empire.cities[0].buildingLevels.barracks = 1
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.recruitUnits('capital', 'levy', 0)).toMatchObject({ ok: false })
    expect(engine.recruitUnits('capital', 'levy', 3)).toMatchObject({ ok: true })
    expect(engine.state.empire.daysRemaining).toBe(56)
    expect(engine.state.empire.resources.wood).toBe(14)
    expect(engine.state.empire.cities[0]).toMatchObject({
      population: 994,
      militaryPopulation: 4,
      populationClasses: { workers: 994 },
      recruitedUnits: { levy: 3 },
    })
    expect(engine.cityArmyFoodUpkeep('capital')).toBe(300)
    expect(engine.cityFoodConsumption('capital')).toBe(1294)
    const beforeUnsafeRecruitment = engine.snapshot()
    expect(engine.recruitUnits('capital', 'levy')).toEqual({
      ok: false,
      message: 'The city does not have enough food surplus.',
    })
    expect(engine.snapshot()).toEqual(beforeUnsafeRecruitment)
    expect(engine.recruitUnits('capital', 'levy', 4)).toMatchObject({ ok: false })

    const saved = JSON.parse(JSON.stringify(engine.snapshot())) as EmpiresCampaignState
    const restored = new EmpiresEndgameEngine(config, saved)
    expect(restored.state.empire.cities[0].recruitedUnits).toEqual({ levy: 3 })
    expect(restored.cityArmyFoodUpkeep('capital')).toBe(300)

    const shutState = restored.snapshot()
    shutState.empire.cities[0].populationClasses.workers = 0
    shutState.empire.cities[0].militaryPopulation = 10
    shutState.empire.cities[0].recruitedUnits = {}
    restored.restore(shutState)
    expect(restored.state.empire.cities[0].operationalBuildingLevels.barracks).toBe(0)
    expect(restored.recruitUnits('capital', 'levy')).toMatchObject({ ok: false })
  })

  it('deducts recruited people proportionally from recruitable classes only', () => {
    const config = makeConfig()
    config.empire.populationClasses[0].canRecruit = true
    config.empire.populationClasses.push(
      {
        id: 'nobles',
        name: 'Nobles',
        canWork: false,
        canRecruit: true,
        foodPerPerson: 2,
        workerPriority: 2,
      },
      {
        id: 'children',
        name: 'Children',
        canWork: false,
        canRecruit: false,
        foodPerPerson: 0.5,
        workerPriority: 3,
      },
    )
    config.empire.units = [{
      id: 'cohort',
      name: 'Cohort',
      foodUpkeep: 0,
      populationCost: 80,
      timeCostDays: 1,
      resourceCosts: [],
      dependencies: [{ kind: 'building', buildingId: 'barracks', level: 1, scope: 'sameCity' }],
    }]
    config.empire.cities[0].populationClasses = { workers: 600, nobles: 200, children: 200 }
    config.empire.cities[0].militaryPopulation = 100
    config.empire.cities[0].baseProduction.food = 1100
    config.empire.cities[0].buildingLevels.barracks = 1
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.recruitUnits('capital', 'cohort')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0]).toMatchObject({
      population: 920,
      militaryPopulation: 20,
      populationClasses: { workers: 540, nobles: 180, children: 200 },
      recruitedUnits: { cohort: 1 },
    })
  })

  it('uses projected post-recruitment workforce and production for food safety', () => {
    const config = makeConfig()
    config.empire.populationClasses[0].canRecruit = true
    config.empire.units = [{
      id: 'levy',
      name: 'Levy',
      foodUpkeep: 0,
      populationCost: 10,
      timeCostDays: 1,
      resourceCosts: [],
      dependencies: [{ kind: 'building', buildingId: 'barracks', level: 1, scope: 'sameCity' }],
    }]
    config.empire.buildings.push({
      id: 'farm',
      name: 'Farm',
      slot: 'farm',
      levels: [{
        level: 1,
        timeCostDays: 0,
        foodCost: 0,
        resourceCosts: [],
        dependencies: [],
        facilityLocks: [],
        workerDemand: 985,
        production: [{ resourceId: 'food', amount: 1300 }],
      }],
    })
    config.empire.cities[0].slots.push({
      id: 'slot-farm',
      kind: 'farm',
      buildingId: 'farm',
      position: { x: 70, y: 10 },
    })
    config.empire.cities[0].buildingLevels.mine = 0
    config.empire.cities[0].buildingLevels.lumber = 0
    config.empire.cities[0].buildingLevels.barracks = 1
    config.empire.cities[0].buildingLevels.farm = 1
    config.empire.cities[0].baseProduction.food = 0
    config.empire.cities[0].militaryPopulation = 10
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)
    expect(engine.state.empire.cities[0].operationalBuildingLevels.farm).toBe(1)

    const beforeRecruitment = engine.snapshot()
    expect(engine.recruitUnits('capital', 'levy')).toEqual({
      ok: false,
      message: 'The city does not have enough food surplus.',
    })
    expect(engine.snapshot()).toEqual(beforeRecruitment)
  })

  it('blocks every recruitment order while the recruitmentDisabled card flag is active', () => {
    const config = makeConfig()
    config.empire.units = [{
      id: 'levy',
      name: 'Levy',
      foodUpkeep: 100,
      populationCost: 1,
      timeCostDays: 1,
      resourceCosts: [],
      dependencies: [{ kind: 'building', buildingId: 'barracks', level: 1, scope: 'sameCity' }],
    }]
    config.empire.cities[0].militaryPopulation = 10
    config.empire.cities[0].baseProduction.food = 2_000
    config.empire.cities[0].buildingLevels.barracks = 1
    const agitators = config.cards.find(card => card.id === 'hearts-7') as EmpiresCardDefinition
    agitators.inverted.effects = [{ kind: 'flag', flagId: 'recruitmentDisabled', amount: 1 }]

    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = [agitators.id]
    state.cards[agitators.id].inverted = true
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.state.empire.flags.recruitmentDisabled).toBe(1)
    expect(engine.recruitUnits('capital', 'levy')).toEqual({
      ok: false,
      message: 'Recruitment is disabled for this empire phase.',
    })
    expect(engine.state.empire.cities[0].recruitedUnits).toEqual({})
    expect(engine.state.empire.cities[0].militaryPopulation).toBe(10)
  })

  it('applies peasantProductivityPercent only to operational farm output', () => {
    const config = makeConfig()
    config.empire.populationClasses[0].id = 'peasants'
    config.empire.cities[0].populationClasses = { peasants: 1_000 }
    config.empire.cities[0].baseProduction.food = 0
    config.empire.cities[0].buildingLevels.farm = 1
    config.empire.cities[0].slots.push({
      id: 'slot-farm',
      kind: 'farm',
      buildingId: 'farm',
      position: { x: 70, y: 10 },
    })
    config.empire.buildings.push({
      id: 'farm',
      name: 'Farm',
      slot: 'farm',
      levels: [{
        level: 1,
        timeCostDays: 0,
        foodCost: 0,
        resourceCosts: [],
        dependencies: [],
        facilityLocks: [],
        workerDemand: 10,
        production: [{ resourceId: 'food', amount: 100 }],
      }],
    })
    const nutrition = config.cards.find(card => card.id === 'clubs-8') as EmpiresCardDefinition
    nutrition.normal.effects = [{
      kind: 'flag',
      flagId: 'peasantProductivityPercent',
      amount: 50,
      amountPerLevel: 50,
    }]
    nutrition.inverted.effects = [{
      kind: 'flag',
      flagId: 'peasantProductivityPercent',
      amount: -50,
    }]

    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = [nutrition.id]
    state.cards[nutrition.id].level = 1
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.state.empire.flags.peasantProductivityPercent).toBe(100)
    expect(engine.cityProduction('capital')).toMatchObject({ food: 200, iron: 5, wood: 5 })

    const invertedEngine = new EmpiresEndgameEngine(config)
    const inverted = invertedEngine.snapshot()
    inverted.phase = 'divineGift'
    inverted.durak.playerHand = [nutrition.id]
    inverted.cards[nutrition.id].inverted = true
    inverted.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    invertedEngine.restore(inverted)
    expect(invertedEngine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(invertedEngine.state.empire.flags.peasantProductivityPercent).toBe(-50)
    expect(invertedEngine.cityProduction('capital')).toMatchObject({ food: 50, iron: 5, wood: 5 })
  })

  it('uses population-class food rates and army upkeep for starvation', () => {
    const config = makeConfig()
    config.empire.populationClasses[0].foodPerPerson = 0.5
    config.empire.populationClasses.push({
      id: 'elite',
      name: 'Elite',
      canWork: false,
      canRecruit: false,
      foodPerPerson: 2,
      workerPriority: 2,
    })
    config.empire.units = [{
      id: 'guard',
      name: 'Guard',
      foodUpkeep: 100,
      populationCost: 1,
      timeCostDays: 1,
      resourceCosts: [],
      dependencies: [],
    }]
    config.empire.cities[0].populationClasses = { workers: 600, elite: 400 }
    config.empire.cities[0].baseProduction.food = 1000
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.cities[0].recruitedUnits = { guard: 2 }
    engine.restore(state)

    expect(engine.cityArmyFoodUpkeep('capital')).toBe(200)
    expect(engine.cityFoodConsumption('capital')).toBe(1300)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].population).toBe(850)
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(150)

    const fallbackConfig = makeConfig()
    fallbackConfig.empire.cities[0].populationClasses = {}
    expect(new EmpiresEndgameEngine(fallbackConfig).cityFoodConsumption('capital')).toBe(1000)
  })

  it('applies starvationLossMultiplierPercent to the half-deficit population loss', () => {
    const config = makeConfig()
    config.empire.initialFlags = { starvationLossMultiplierPercent: -20 }
    config.empire.cities[0].baseProduction.food = 500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].population).toBe(800)
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(200)
  })

  it('assigns the exclusive production boost to one operational target without spending days', () => {
    const config = makeConfig()
    config.empire.initialFlags = {
      productionBoostPercent: 200,
    }
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.assignProductionBoost('capital', 'smithy')).toMatchObject({ ok: false })
    expect(engine.assignProductionBoost('capital', 'mine')).toMatchObject({ ok: true })
    expect(engine.state.empire.daysRemaining).toBe(59)
    expect(engine.hasProductionBoost('capital', 'mine')).toBe(true)
    expect(engine.cityProduction('capital')).toMatchObject({ iron: 10, wood: 5 })

    expect(engine.assignProductionBoost('capital', 'lumber')).toMatchObject({ ok: true })
    expect(engine.hasProductionBoost('capital', 'mine')).toBe(false)
    expect(engine.hasProductionBoost('capital', 'lumber')).toBe(true)
    expect(engine.state.empire.productionBoostAssignments).toEqual([
      { cityId: 'capital', buildingId: 'lumber' },
    ])
    expect(engine.cityProduction('capital')).toMatchObject({ iron: 5, wood: 10 })

    const oversizedSnapshot = JSON.parse(JSON.stringify(engine.snapshot())) as EmpiresCampaignState
    oversizedSnapshot.empire.productionBoostAssignments.push({ cityId: 'capital', buildingId: 'mine' })
    const restored = new EmpiresEndgameEngine(config, oversizedSnapshot)
    expect(restored.state.empire.productionBoostAssignments).toEqual([
      { cityId: 'capital', buildingId: 'lumber' },
    ])
    expect(restored.cityProduction('capital').wood).toBe(10)
    expect(restored.clearProductionBoost('capital', 'lumber')).toMatchObject({ ok: true })
    expect(restored.cityProduction('capital').wood).toBe(5)
  })

  it('places a level-one building into a matching runtime slot before upgrades are allowed', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1500
    config.empire.cities[0].buildingLevels.hall = 0
    config.empire.cities[0].buildingLevels['old-hall'] = 0
    const municipalSlot = config.empire.cities[0].slots.find(slot => slot.id === 'slot-municipal')
    if (municipalSlot) municipalSlot.buildingId = 'old-hall'
    config.empire.buildings.push({
      id: 'hall',
      name: 'Town Hall',
      slot: 'municipal',
      levels: [
        {
          level: 1,
          timeCostDays: 2,
          foodCost: 100,
          resourceCosts: [{ resourceId: 'wood', amount: 2 }],
          dependencies: [],
          facilityLocks: [],
        },
        {
          level: 2,
          timeCostDays: 3,
          foodCost: 0,
          resourceCosts: [{ resourceId: 'wood', amount: 3 }],
          dependencies: [],
          facilityLocks: [],
        },
      ],
    })
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.upgradeBuilding('capital', 'hall')).toMatchObject({ ok: false })
    expect(engine.placeBuilding('capital', 'slot-mine', 'hall')).toMatchObject({ ok: false })
    expect(engine.placeBuilding('capital', 'slot-municipal', 'hall')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].buildingSlotAssignments['slot-municipal']).toBe('hall')
    expect(engine.state.empire.cities[0].buildingLevels.hall).toBe(1)
    expect(engine.upgradeBuilding('capital', 'hall')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].buildingLevels.hall).toBe(2)
    expect(engine.state.empire.resources.wood).toBe(15)
    expect(engine.state.empire.daysRemaining).toBe(54)
  })

  it('migrates schema-one snapshots missing the new serialized economy fields', () => {
    const config = makeConfig()
    const legacy = new EmpiresEndgameEngine(config).snapshot()
    const legacyCity = legacy.empire.cities[0] as EmpiresCampaignState['empire']['cities'][number] & {
      buildingSlotAssignments?: Record<string, string>
      recruitedUnits?: Record<string, number>
    }
    const legacyEmpire = legacy.empire as EmpiresCampaignState['empire'] & {
      cardFlagBonuses?: Record<string, number>
      productionBoostAssignments?: Array<{ cityId: string, buildingId: string }>
    }
    delete legacyCity.buildingSlotAssignments
    delete legacyCity.recruitedUnits
    delete legacyEmpire.cardFlagBonuses
    delete legacyEmpire.productionBoostAssignments

    const restored = new EmpiresEndgameEngine(config, legacy)
    expect(restored.state.empire.cities[0].buildingSlotAssignments).toMatchObject({
      'slot-mine': 'mine',
      'slot-lumber': 'lumber',
    })
    expect(restored.state.empire.cities[0].recruitedUnits).toEqual({})
    expect(restored.state.empire.cardFlagBonuses).toEqual({})
    expect(restored.state.empire.productionBoostAssignments).toEqual([])
  })

  it('reconstructs held-card flag ownership in legacy empire and event snapshots', () => {
    const config = makeConfig()
    config.empire.eventChance = 1
    config.empire.initialFlags = { seasonalFlag: 7 }
    config.empire.cities[0].baseProduction.food = 1500
    const seasonalCard = config.cards.find(card => card.id === 'hearts-7') as EmpiresCardDefinition
    seasonalCard.normal.effects = [{
      kind: 'flag',
      flagId: 'seasonalFlag',
      amount: 2,
      amountPerLevel: 1,
    }]

    const source = new EmpiresEndgameEngine(config)
    const readyForGift = source.snapshot()
    readyForGift.phase = 'divineGift'
    readyForGift.durak.playerHand = [seasonalCard.id]
    readyForGift.cards[seasonalCard.id].level = 2
    readyForGift.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    source.restore(readyForGift)
    expect(source.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(source.state.empire.flags.seasonalFlag).toBe(11)
    expect(source.state.empire.cardFlagBonuses).toEqual({ seasonalFlag: 4 })

    const legacyEmpireState = source.snapshot()
    delete (legacyEmpireState.empire as EmpiresCampaignState['empire'] & {
      cardFlagBonuses?: Record<string, number>
    }).cardFlagBonuses
    const empireRestored = new EmpiresEndgameEngine(config, legacyEmpireState)
    expect(empireRestored.state.empire.flags.seasonalFlag).toBe(11)
    expect(empireRestored.state.empire.cardFlagBonuses).toEqual({ seasonalFlag: 4 })

    expect(empireRestored.finishEmpire()).toMatchObject({ ok: true })
    expect(empireRestored.state.phase).toBe('event')
    const legacyEventState = empireRestored.snapshot()
    delete (legacyEventState.empire as EmpiresCampaignState['empire'] & {
      cardFlagBonuses?: Record<string, number>
    }).cardFlagBonuses
    const eventRestored = new EmpiresEndgameEngine(config, legacyEventState)
    expect(eventRestored.state.empire.cardFlagBonuses).toEqual({ seasonalFlag: 4 })

    const eventDefinition = config.empire.events.find(event => event.id === eventRestored.state.event?.eventId)
    expect(eventDefinition).toBeDefined()
    expect(eventRestored.chooseEvent(eventDefinition?.choices[0].id ?? '')).toMatchObject({ ok: true })
    expect(eventRestored.state.empire.flags.seasonalFlag).toBe(7)
    expect(eventRestored.state.empire.cardFlagBonuses).toEqual({})
  })

  it('creates deterministic autosave-friendly snapshots', () => {
    const first = new EmpiresEndgameEngine(makeConfig())
    const second = new EmpiresEndgameEngine(makeConfig())

    expect(first.snapshot()).toEqual(second.snapshot())
    const saved = first.snapshot()
    first.restore(saved)
    expect(first.snapshot()).toEqual(saved)
  })

  it('selects weighted events deterministically from the serialized RNG state', () => {
    const config = makeConfig()
    config.empire.eventChance = 1
    const first = new EmpiresEndgameEngine(config)
    const second = new EmpiresEndgameEngine(config)
    for (const engine of [first, second]) {
      const state = engine.snapshot()
      state.phase = 'empire'
      state.empire.daysRemaining = 1
      engine.restore(state)
      engine.finishEmpire()
    }

    expect(first.state.phase).toBe('event')
    expect(first.state.event).toEqual(second.state.event)
    expect(first.state.rng).toEqual(second.state.rng)
  })
})
