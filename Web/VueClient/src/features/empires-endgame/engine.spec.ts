import { describe, expect, it } from 'vitest'
import defaultConfigJson from '../../../public/empires-endgame/game-config.json'
import { EmpiresEndgameEngine, validateEmpiresEndgameConfig } from './engine'
import { EMPIRES_RANKS, EMPIRES_SUITS } from './types'
import type {
  EmpiresActor,
  EmpiresCampaignState,
  EmpiresCardDefinition,
  EmpiresEndgameConfig,
  EmpiresEventDefinition,
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

function makeFamineEvent(): EmpiresEventDefinition {
  return {
    id: 'event-famine-rationing',
    name: 'Famine Crisis',
    description: 'Choose emergency rationing before food settlement.',
    weight: 1,
    choices: [
      {
        id: 'strict-rations',
        label: 'Strict rations',
        effects: [{
          kind: 'flag',
          flagId: 'starvationLossMultiplierPercent',
          amount: -20,
        }],
      },
      {
        id: 'buy-food',
        label: 'Buy food',
        resourceCosts: [{ resourceId: 'gold', amount: 1_200 }],
        effects: [{ kind: 'resource', resourceId: 'food', amount: 600 }],
      },
    ],
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
    expect(engine.state.giftChoiceIds).not.toContain('gift-b')
  })

  it('keeps relics locked until Divine Presence enables them', () => {
    const config = makeConfig()
    const engine = new EmpiresEndgameEngine(config)
    const locked = engine.snapshot()
    locked.phase = 'divineGift'
    locked.giftChoiceIds = ['gift-b']
    engine.state = locked

    expect(engine.chooseGift('gift-b')).toEqual({
      ok: false,
      message: 'Relics are locked until Divine Presence is researched.',
    })

    const unlocked = engine.snapshot()
    unlocked.empire.flags.relicsUnlocked = 1
    engine.state = unlocked
    expect(engine.chooseGift('gift-b')).toMatchObject({ ok: true })
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
    expect(borderCity?.operationalBuildingLevels['building-mine']).toBe(1)
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
          if (engine.state.pendingResolution) {
            const targetId = engine.state.pendingResolution.eligibleTargetIds.find(
              cityId => engine.isCityAccessible(cityId),
            )
            result = targetId
              ? engine.resolvePendingTarget(targetId)
              : { ok: false, message: 'Autoplayer received no eligible divine-gift target.' }
          } else {
            const currentGiftId = engine.state.giftChoiceIds[0] ?? null
            firstGiftId ??= currentGiftId
            result = currentGiftId
              ? engine.chooseGift(currentGiftId)
              : { ok: false, message: 'Autoplayer received no divine gifts.' }
          }
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
    expect(first.eventChoices).toBe(0)
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
  it('pauses targeted acquisition gifts, persists the target choice, and reapplies them on later acquisitions', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1500
    config.gifts.definitions[0].resolution = { kind: 'cityResources' }
    config.gifts.definitions[0].effects = [
      { kind: 'resource', resourceId: 'wood', amount: 7 },
      { kind: 'resource', resourceId: 'food', amount: 100 },
      { kind: 'flag', flagId: 'targetedBlessing', amount: 2 },
    ]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = []
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('divineGift')
    expect(engine.state.pendingResolution).toEqual({
      kind: 'cityResources',
      giftId: 'gift-a',
      eligibleTargetIds: ['capital'],
    })
    expect(engine.state.empire.resources.wood).toBe(20)
    expect(engine.state.empire.cities[0].resources).toEqual({})

    const restored = new EmpiresEndgameEngine(
      config,
      JSON.parse(JSON.stringify(engine.snapshot())) as EmpiresCampaignState,
    )
    expect(restored.resolvePendingTarget('capital')).toMatchObject({ ok: true })
    expect(restored.state.phase).toBe('empire')
    expect(restored.state.pendingResolution).toBeNull()
    expect(restored.state.empire.giftResolutionTargets['gift-a']).toBe('capital')
    expect(restored.state.empire.cities[0].resources).toMatchObject({ wood: 7, food: 100 })
    expect(restored.state.empire.resources.wood).toBe(20)
    expect(restored.state.empire.flags.targetedBlessing).toBe(2)

    const replay = restored.snapshot()
    replay.phase = 'divineGift'
    replay.giftChoiceIds = ['gift-a']
    restored.restore(replay)
    expect(restored.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(restored.resolvePendingTarget('capital')).toMatchObject({ ok: true })
    expect(restored.state.empire.claimedGiftIds.filter(id => id === 'gift-a')).toHaveLength(2)
    expect(restored.state.empire.cities[0].resources).toMatchObject({ wood: 14, food: 200 })
    expect(restored.state.empire.flags.targetedBlessing).toBe(4)
  })

  it('reapplies a recurring targeted gift exactly once at each empire start', () => {
    const config = makeConfig()
    const gift = config.gifts.definitions.find(item => item.id === 'gift-b')
    if (!gift) throw new Error('Missing recurring gift fixture.')
    gift.resolution = { kind: 'cityResources' }
    gift.effects = [{ kind: 'resource', resourceId: 'wood', amount: 3 }]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = []
    state.giftChoiceIds = ['gift-b', 'gift-c', 'gift-d']
    state.empire.flags.relicsUnlocked = 1
    engine.restore(state)

    expect(engine.chooseGift('gift-b')).toMatchObject({ ok: true })
    expect(engine.resolvePendingTarget('capital')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.wood).toBe(3)
    expect(engine.state.empire.activeGiftIds).toContain('gift-b')
    expect(engine.finishEmpire()).toMatchObject({ ok: true })

    const nextEmpire = engine.snapshot()
    nextEmpire.phase = 'divineGift'
    nextEmpire.giftChoiceIds = ['gift-c', 'gift-d']
    engine.restore(nextEmpire)
    expect(engine.chooseGift('gift-c')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.wood).toBe(6)
  })

  it('does not redirect recurring targeted rewards when their city becomes inaccessible', () => {
    const config = makeConfig()
    const gift = config.gifts.definitions.find(item => item.id === 'gift-b')
    if (!gift) throw new Error('Missing recurring gift fixture.')
    gift.resolution = { kind: 'cityResources' }
    gift.effects = [{ kind: 'resource', resourceId: 'wood', amount: 3 }]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.giftChoiceIds = ['gift-a', 'gift-c', 'gift-d']
    state.empire.flags.relicsUnlocked = 1
    state.empire.claimedGiftIds = ['gift-b']
    state.empire.activeGiftIds = ['gift-b']
    state.empire.giftResolutionTargets = { 'gift-b': 'capital' }
    state.empire.destroyedRegionIds = ['central']
    engine.restore(state)

    expect(engine.state.empire.giftResolutionTargets).toEqual({})
    expect(engine.chooseGift('gift-c')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.wood).toBeUndefined()
    expect(engine.state.empire.resources.wood).toBe(20)
  })

  it('does not apply a restored recurring relic while relics are still locked', () => {
    const config = makeConfig()
    const gift = config.gifts.definitions.find(item => item.id === 'gift-b')
    if (!gift) throw new Error('Missing recurring relic fixture.')
    gift.resolution = { kind: 'cityResources' }
    gift.effects = [{ kind: 'resource', resourceId: 'wood', amount: 3 }]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.giftChoiceIds = ['gift-a', 'gift-c', 'gift-d']
    state.empire.claimedGiftIds = ['gift-b']
    state.empire.activeGiftIds = ['gift-b']
    state.empire.giftResolutionTargets = { 'gift-b': 'capital' }
    engine.restore(state)

    expect(engine.chooseGift('gift-c')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.wood).toBeUndefined()
    expect(engine.state.empire.resources.wood).toBe(20)
  })

  it('meteor damage skips deferred building shells and hits the highest live building', () => {
    const config = makeConfig()
    const meteor = config.gifts.definitions.find(gift => gift.id === 'gift-a')
    const barracks = config.empire.buildings.find(building => building.id === 'barracks')
    if (!meteor || !barracks) throw new Error('Missing meteor fixtures.')
    meteor.resolution = { kind: 'meteorCity', damageLevels: 1 }
    barracks.deferredReason = 'Future army system.'
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.giftChoiceIds = ['gift-a', 'gift-c', 'gift-d']
    state.empire.cities[0].buildingLevels.barracks = 5
    state.empire.cities[0].buildingLevels.mine = 2
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.resolvePendingTarget('capital')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].buildingLevels.barracks).toBe(5)
    expect(engine.state.empire.cities[0].buildingLevels.mine).toBe(1)
    expect(engine.state.empire.cities[0].buildingInteractionLocks.mine).toBe(engine.state.con)
  })

  it('spends city stockpiles before empire reserves and uses both food reserves before starvation', () => {
    const config = makeConfig()
    config.empire.initialFlags = {
      starvationDeficitLossPercent: 25,
      starvationLossMultiplierPercent: -20,
    }
    config.empire.initialResources = { wood: 20, iron: 20, food: 200 }
    config.empire.cities[0].baseProduction.food = 1500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    state.empire.cities[0].resources = { wood: 7 }
    engine.restore(state)

    expect(engine.upgradeBuilding('capital', 'smithy')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.wood).toBe(0)
    expect(engine.state.empire.resources.wood).toBe(17)

    const starving = engine.snapshot()
    starving.phase = 'empire'
    starving.empire.cities[0].baseProduction.food = 500
    starving.empire.cities[0].resources.food = 100
    starving.empire.resources.food = 200
    starving.empire.cities[0].population = 1000
    starving.empire.cities[0].populationClasses = { workers: 1000 }
    engine.restore(starving)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.food).toBe(0)
    expect(engine.state.empire.resources.food).toBe(0)
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(40)
    expect(engine.state.empire.cities[0].population).toBe(960)
  })

  it('uses local then shared food reserves for construction commitments without double-booking them', () => {
    const config = makeConfig()
    config.empire.initialResources = { wood: 20, iron: 20, food: 50 }
    config.empire.cities[0].baseProduction.food = 1000
    const temple = config.empire.buildings.find(building => building.id === 'temple')
    if (!temple) throw new Error('Missing temple fixture.')
    temple.levels[0].foodCost = 80
    config.empire.map.regions.push({
      id: 'west',
      name: 'West',
      biome: 'forest',
      center: { x: 20, y: 50 },
      polygon: [],
      subregionIds: [],
      cityIds: ['outpost'],
    })
    config.empire.cities.push({
      ...JSON.parse(JSON.stringify(config.empire.cities[0])),
      id: 'outpost',
      name: 'Outpost',
      regionId: 'west',
      position: { x: 20, y: 50 },
    })
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    state.empire.cities[0].resources.food = 30
    engine.restore(state)

    expect(engine.upgradeBuilding('capital', 'temple')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].foodCommitted).toBe(80)
    expect(engine.state.empire.cities[0].resources.food).toBe(30)
    expect(engine.state.empire.resources.food).toBe(50)
    expect(engine.upgradeBuilding('outpost', 'temple')).toEqual({
      ok: false,
      message: 'The city does not have enough food surplus.',
    })

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.food).toBe(0)
    expect(engine.state.empire.resources.food).toBe(0)
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(0)
  })

  it('lets city and empire food reserves make an otherwise unsafe recruitment order viable', () => {
    const config = makeConfig()
    config.empire.initialResources = { wood: 20, iron: 20, food: 40 }
    config.empire.populationClasses[0].canRecruit = true
    config.empire.units = [{
      id: 'levy',
      name: 'Levy',
      foodUpkeep: 100,
      populationCost: 1,
      timeCostDays: 1,
      resourceCosts: [],
      dependencies: [{ kind: 'building', buildingId: 'barracks', level: 1, scope: 'sameCity' }],
    }]
    config.empire.cities[0].baseProduction.food = 1000
    config.empire.cities[0].militaryPopulation = 1
    config.empire.cities[0].buildingLevels.barracks = 1
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    state.empire.cities[0].resources.food = 60
    engine.restore(state)

    expect(engine.recruitUnits('capital', 'levy')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.food).toBe(60)
    expect(engine.state.empire.resources.food).toBe(40)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].resources.food).toBe(0)
    expect(engine.state.empire.resources.food).toBe(1)
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(0)
  })

  it('destroys a region without deleting its records and excludes its cities from empire actions', () => {
    const config = makeConfig()
    config.empire.map.regions.push({
      id: 'west',
      name: 'West',
      biome: 'forest',
      center: { x: 20, y: 50 },
      polygon: [],
      subregionIds: [],
      cityIds: ['outpost'],
    })
    config.empire.cities.push({
      ...JSON.parse(JSON.stringify(config.empire.cities[0])),
      id: 'outpost',
      name: 'Outpost',
      regionId: 'west',
      position: { x: 20, y: 50 },
    })
    config.gifts.definitions[0].resolution = { kind: 'destroyRegion', regionId: 'central' }
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = []
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.isRegionAccessible('central')).toBe(false)
    expect(engine.isCityAccessible('capital')).toBe(false)
    expect(engine.isCityAccessible('outpost')).toBe(true)
    expect(engine.cityProduction('capital')).toEqual({})
    expect(engine.cityProduction('outpost')).toMatchObject({ food: 500, iron: 5, wood: 5 })
    expect(engine.upgradeBuilding('capital', 'smithy')).toEqual({
      ok: false,
      message: 'That city is not accessible.',
    })

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.cities.find(city => city.id === 'capital')?.population).toBe(1000)
    expect(engine.state.empire.cities.find(city => city.id === 'capital')?.lastProduction).toEqual({})
    expect(engine.state.empire.cities.find(city => city.id === 'outpost')?.population).toBe(750)
  })

  it('raises placed farm levels effectively and extrapolates production and workforce beyond the catalog', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 0
    config.empire.cities[0].buildingLevels.farm = 2
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
      levels: [
        {
          level: 1,
          timeCostDays: 0,
          foodCost: 0,
          resourceCosts: [],
          dependencies: [],
          facilityLocks: [],
          workerDemand: 10,
          production: [{ resourceId: 'food', amount: 100 }],
        },
        {
          level: 2,
          timeCostDays: 0,
          foodCost: 0,
          resourceCosts: [],
          dependencies: [],
          facilityLocks: [],
          workerDemand: 20,
          production: [{ resourceId: 'food', amount: 180 }],
        },
      ],
    })
    config.gifts.definitions[0].resolution = {
      kind: 'buildingLevelBonus',
      slots: ['farm'],
      amount: 1,
    }
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = []
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].buildingLevels.farm).toBe(2)
    expect(engine.effectiveBuildingLevel('capital', 'farm')).toBe(3)
    expect(engine.effectiveBuildingMaxLevel('farm')).toBe(3)
    expect(engine.cityProduction('capital').food).toBe(260)
    expect(engine.state.empire.cities[0].operationalBuildingLevels.farm).toBe(2)
  })

  it('damages and locks the highest-level building with a meteor while routing resources to the target city', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1500
    config.empire.cities[0].buildingLevels.mine = 3
    config.gifts.definitions[0].resolution = { kind: 'meteorCity', damageLevels: 1 }
    config.gifts.definitions[0].effects = [
      { kind: 'resource', resourceId: 'iron', amount: 50 },
      { kind: 'flag', flagId: 'radioactiveIron', amount: 1 },
    ]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = []
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.resolvePendingTarget('capital')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].buildingLevels.mine).toBe(2)
    expect(engine.state.empire.cities[0].buildingInteractionLocks.mine).toBe(engine.state.con)
    expect(engine.state.empire.cities[0].resources.iron).toBe(50)
    expect(engine.state.empire.resources.iron).toBe(20)
    expect(engine.state.empire.flags.radioactiveIron).toBe(1)
    expect(engine.upgradeBuilding('capital', 'mine')).toEqual({
      ok: false,
      message: 'That building is locked for the current con.',
    })

    const restored = new EmpiresEndgameEngine(
      config,
      JSON.parse(JSON.stringify(engine.snapshot())) as EmpiresCampaignState,
    )
    expect(restored.state.empire.cities[0].buildingInteractionLocks.mine).toBe(restored.state.con)
    expect(restored.state.empire.giftResolutionTargets['gift-a']).toBe('capital')
  })

  it('applies military arson once per accessible city at empire start', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1500
    config.empire.cities[0].buildingLevels.barracks = 2
    const arson = config.cards.find(card => card.id === 'hearts-7') as EmpiresCardDefinition
    arson.inverted.effects = [{ kind: 'flag', flagId: 'militaryArson', amount: 1 }]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.durak.playerHand = [arson.id]
    state.cards[arson.id].inverted = true
    state.empire.cities[0].recruitedUnits = { archers: 1, levy: 2 }
    state.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    engine.restore(state)

    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(Object.values(engine.state.empire.cities[0].recruitedUnits)
      .reduce((total, count) => total + count, 0)).toBe(2)
    expect(engine.state.empire.cities[0].buildingLevels.barracks).toBe(1)
    expect(engine.state.empire.cities[0].buildingInteractionLocks.barracks).toBe(engine.state.con)
    expect(engine.upgradeBuilding('capital', 'barracks')).toEqual({
      ok: false,
      message: 'That building is locked for the current con.',
    })
  })

  it('honors smithy and stable material exemptions without consuming missing resources', () => {
    const config = makeConfig()
    config.empire.resources.push({ id: 'horses', name: 'Horses' })
    config.empire.initialResources = { wood: 20, iron: 0, horses: 0 }
    config.empire.initialFlags = {
      smithyWithoutIron: 1,
      stableWithoutLivestock: 1,
    }
    config.empire.cities[0].baseProduction.food = 1500
    const smithy = config.empire.buildings.find(building => building.id === 'smithy')
    if (!smithy) throw new Error('Missing smithy fixture.')
    smithy.levels[0].resourceCosts = [
      { resourceId: 'wood', amount: 5 },
      { resourceId: 'iron', amount: 10 },
    ]
    config.empire.cities[0].buildingLevels.stable = 0
    config.empire.cities[0].slots.push({
      id: 'slot-stable',
      kind: 'unique',
      buildingId: 'stable',
      position: { x: 75, y: 10 },
    })
    config.empire.buildings.push({
      id: 'stable',
      name: 'Конюшня',
      slot: 'unique',
      levels: [{
        level: 1,
        timeCostDays: 1,
        foodCost: 0,
        resourceCosts: [
          { resourceId: 'wood', amount: 2 },
          { resourceId: 'horses', amount: 10 },
        ],
        dependencies: [{ kind: 'flag', flagId: 'livestockAvailable', minimum: 1 }],
        facilityLocks: [],
        workerDemand: 0,
      }],
    })
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.upgradeBuilding('capital', 'smithy')).toMatchObject({ ok: true })
    expect(engine.state.empire.resources).toMatchObject({ wood: 15, iron: 0, horses: 0 })
    expect(engine.placeBuilding('capital', 'slot-stable', 'stable')).toMatchObject({ ok: true })
    expect(engine.state.empire.resources).toMatchObject({ wood: 13, iron: 0, horses: 0 })
  })

  it('limits research by authored group and family, then resets usage at the next empire start', () => {
    const config = makeConfig()
    config.empire.technologies = [
      {
        id: 'metallurgy',
        name: 'Metallurgy',
        category: 'technology',
        groupId: 'industry',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [],
      },
      {
        id: 'machinery',
        name: 'Machinery',
        category: 'steel',
        groupId: 'industry',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [],
      },
      {
        id: 'navigation',
        name: 'Navigation',
        category: 'technology',
        groupId: 'sea',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [],
      },
      {
        id: 'reform-a',
        name: 'Reform A',
        category: 'reform',
        groupId: 'civic',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [],
      },
      {
        id: 'doctrine-a',
        name: 'Doctrine A',
        category: 'doctrine',
        groupId: 'civic',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [],
      },
      {
        id: 'independent-a',
        name: 'Independent A',
        category: 'technology',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [],
      },
      {
        id: 'independent-b',
        name: 'Independent B',
        category: 'technology',
        timeCostDays: 1,
        resourceCosts: [],
        prerequisites: [],
        effects: [],
      },
    ]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    engine.restore(state)

    expect(engine.research('metallurgy')).toMatchObject({ ok: true })
    expect(engine.research('machinery')).toEqual({
      ok: false,
      message: 'That research group was already used this empire phase.',
    })
    expect(engine.research('navigation')).toMatchObject({ ok: true })
    expect(engine.research('reform-a')).toMatchObject({ ok: true })
    expect(engine.research('doctrine-a')).toEqual({
      ok: false,
      message: 'That research group was already used this empire phase.',
    })
    expect(engine.research('independent-a')).toMatchObject({ ok: true })
    expect(engine.research('independent-b')).toMatchObject({ ok: true })

    const next = engine.snapshot()
    next.phase = 'divineGift'
    next.durak.playerHand = []
    next.giftChoiceIds = ['gift-c']
    engine.restore(next)
    expect(engine.chooseGift('gift-c')).toMatchObject({ ok: true })
    expect(engine.state.empire.researchUsage).toEqual({})
    expect(engine.research('machinery')).toMatchObject({ ok: true })
    expect(engine.research('doctrine-a')).toMatchObject({ ok: true })
  })

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

  it('applies acquisition gifts once per acceptance without registering a recurring empire effect', () => {
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

  it('keeps acquisition gifts repeatable beyond the finite gift catalog', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1500
    config.gifts.definitions[0].effects = [{ kind: 'resource', resourceId: 'wood', amount: 7 }]
    const engine = new EmpiresEndgameEngine(config)
    const acquisitionCount = config.gifts.definitions.length + 2

    for (let index = 0; index < acquisitionCount; index += 1) {
      const state = engine.snapshot()
      state.phase = 'divineGift'
      state.durak.playerHand = []
      state.giftChoiceIds = ['gift-a']
      state.pendingResolution = null
      engine.restore(state)
      expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    }

    expect(engine.state.empire.claimedGiftIds.filter(id => id === 'gift-a')).toHaveLength(acquisitionCount)
    expect(engine.state.empire.resources.wood).toBe(20 + acquisitionCount * 7)
    expect(engine.state.empire.activeGiftIds).not.toContain('gift-a')
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

  it('applies strict famine rations only to the pending crisis and restores the previous multiplier', () => {
    const config = makeConfig()
    config.empire.eventChance = 1
    config.empire.events = [makeFamineEvent()]
    config.empire.initialFlags = { starvationLossMultiplierPercent: -10 }
    config.empire.cities[0].baseProduction.food = 500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('event')
    expect(engine.state.event).toEqual({
      eventId: 'event-famine-rationing',
      empireSettlementPending: true,
    })
    expect(engine.state.empire.cities[0].population).toBe(1000)

    expect(engine.chooseEvent('strict-rations')).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('cards')
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(175)
    expect(engine.state.empire.cities[0].population).toBe(825)
    expect(engine.state.empire.flags.starvationLossMultiplierPercent).toBe(-10)
  })

  it('lets emergency food purchases cover the pending famine deficit before settlement', () => {
    const config = makeConfig()
    config.empire.eventChance = 1
    config.empire.events = [makeFamineEvent()]
    config.empire.resources.push({ id: 'gold', name: 'Gold' })
    config.empire.initialResources.gold = 1_200
    config.empire.cities[0].baseProduction.food = 500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.event?.empireSettlementPending).toBe(true)
    expect(engine.chooseEvent('buy-food')).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('cards')
    expect(engine.state.empire.resources.gold).toBe(0)
    expect(engine.state.empire.resources.food).toBe(100)
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(0)
    expect(engine.state.empire.cities[0].population).toBe(1000)
  })

  it('restores a pending famine and settles production, levy, Treasury, and starvation exactly once', () => {
    const config = makeConfig()
    config.empire.eventChance = 1
    config.empire.events = [makeFamineEvent()]
    config.empire.resources.push({ id: 'gold', name: 'Gold' })
    config.empire.initialResources.gold = 2_500_000
    config.empire.initialFlags = { treasuryGoldPerSavedMillion: 1_000 }
    config.empire.cities[0].baseProduction.food = 500
    config.empire.cities[0].buildingLevels.temple = 1
    config.empire.cities[0].buildingLevels.tradeLevy = 1
    const municipalSlot = config.empire.cities[0].slots.find(slot => slot.kind === 'municipal')
    if (municipalSlot) municipalSlot.buildingId = 'tradeLevy'
    config.empire.buildings.push({
      id: 'tradeLevy',
      name: 'Trade Levy',
      slot: 'municipal',
      levels: [{
        level: 1,
        timeCostDays: 0,
        foodCost: 0,
        resourceCosts: [],
        dependencies: [],
        facilityLocks: [],
        effects: [
          { kind: 'flag', flagId: 'idleBuildingGoldBase', amount: 10 },
          { kind: 'flag', flagId: 'surplusFoodPerGold', amount: 1_000 },
        ],
      }],
    })
    const source = new EmpiresEndgameEngine(config)
    const state = source.snapshot()
    state.phase = 'empire'
    source.restore(state)

    expect(source.finishEmpire()).toMatchObject({ ok: true })
    expect(source.state.event?.empireSettlementPending).toBe(true)
    expect(source.state.empire.resources).toMatchObject({
      gold: 2_500_000,
      iron: 20,
      wood: 20,
    })
    expect(source.state.empire.cities[0].population).toBe(1000)

    const restored = new EmpiresEndgameEngine(
      config,
      JSON.parse(JSON.stringify(source.snapshot())) as EmpiresCampaignState,
    )
    expect(restored.state.event?.empireSettlementPending).toBe(true)
    expect(restored.chooseEvent('strict-rations')).toMatchObject({ ok: true })
    expect(restored.state.empire.resources).toMatchObject({
      gold: 2_502_010,
      iron: 25,
      wood: 25,
    })
    expect(restored.state.empire.cities[0].population).toBe(800)
    expect(restored.state.empire.cities[0].lastStarvationLoss).toBe(200)

    const settled = restored.snapshot()
    expect(restored.chooseEvent('strict-rations')).toMatchObject({ ok: false })
    expect(restored.snapshot()).toEqual(settled)
  })

  it('does not re-settle legacy post-settlement famine saves without the marker', () => {
    const config = makeConfig()
    config.empire.events = [makeFamineEvent()]
    const source = new EmpiresEndgameEngine(config)
    const legacy = source.snapshot()
    legacy.phase = 'event'
    legacy.event = { eventId: 'event-famine-rationing' }
    legacy.empire.cities[0].population = 750
    legacy.empire.cities[0].populationClasses = { workers: 750 }
    legacy.empire.cities[0].lastStarvationLoss = 250
    legacy.empire.resources.iron = 25

    const restored = new EmpiresEndgameEngine(config, legacy)
    expect(restored.state.event?.empireSettlementPending).toBe(false)
    expect(restored.chooseEvent('strict-rations')).toMatchObject({ ok: true })
    expect(restored.state.empire.cities[0].population).toBe(750)
    expect(restored.state.empire.cities[0].lastStarvationLoss).toBe(250)
    expect(restored.state.empire.resources.iron).toBe(25)
  })

  it('settles starvation without offering famine when eventChance is zero', () => {
    const config = makeConfig()
    config.empire.eventChance = 0
    config.empire.events = [makeFamineEvent()]
    config.empire.cities[0].baseProduction.food = 500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('cards')
    expect(engine.state.event).toBeNull()
    expect(engine.state.empire.cities[0].lastStarvationLoss).toBe(250)
    expect(engine.state.empire.cities[0].population).toBe(750)
  })

  it('filters deferred events and choices at selection and action boundaries', () => {
    const config = makeConfig()
    config.empire.eventChance = 1
    config.empire.cities[0].baseProduction.food = 1_500
    config.empire.events = [
      {
        id: 'deferred-event',
        name: 'Deferred',
        description: 'Deferred',
        weight: 100,
        deferredReason: 'Future mode.',
        choices: [{ id: 'deferred-event-choice', label: 'Deferred', effects: [] }],
      },
      {
        id: 'active-event',
        name: 'Active',
        description: 'Active',
        weight: 1,
        choices: [
          {
            id: 'deferred-choice',
            label: 'Deferred choice',
            effects: [],
            deferredReason: 'Future mode.',
          },
          { id: 'active-choice', label: 'Active choice', effects: [] },
        ],
      },
    ]
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.event?.eventId).toBe('active-event')
    expect(engine.chooseEvent('deferred-choice')).toEqual({
      ok: false,
      message: 'That event choice is deferred: Future mode.',
    })
    expect(engine.chooseEvent('active-choice')).toMatchObject({ ok: true })

    const deferredState = engine.snapshot()
    deferredState.phase = 'event'
    deferredState.event = { eventId: 'deferred-event' }
    const direct = new EmpiresEndgameEngine(config, deferredState)
    expect(direct.state.phase).toBe('cards')
    expect(direct.state.event).toBeNull()
    expect(direct.chooseEvent('deferred-event-choice')).toEqual({
      ok: false,
      message: 'No event choice is pending.',
    })
  })

  it('keeps deferred cards, buildings, units, and research inert across restored states', () => {
    const config = makeConfig()
    for (const card of config.cards) {
      card.normal.deferredReason = 'Future card system.'
      card.normal.effects = [
        { kind: 'resource', resourceId: 'iron', amount: 100 },
        { kind: 'flag', flagId: 'recruitmentDisabled', amount: 1 },
      ]
    }
    const mine = config.empire.buildings.find(building => building.id === 'mine')
    const smithy = config.empire.buildings.find(building => building.id === 'smithy')
    const metallurgy = config.empire.technologies.find(technology => technology.id === 'metallurgy')
    if (!mine || !smithy || !metallurgy) throw new Error('Missing deferred-content fixtures.')
    mine.deferredReason = 'Future building system.'
    smithy.deferredReason = 'Future building system.'
    metallurgy.deferredReason = 'Future research system.'
    metallurgy.effects = [{ kind: 'resourceMultiplier', resourceId: 'iron', multiplier: 2 }]
    config.empire.units = [{
      id: 'future-unit',
      name: 'Future Unit',
      foodUpkeep: 50,
      populationCost: 1,
      timeCostDays: 1,
      resourceCosts: [],
      dependencies: [],
      deferredReason: 'Future combat system.',
    }]

    const engine = new EmpiresEndgameEngine(config)
    expect(engine.state.empire.cities[0].operationalBuildingLevels.mine).toBe(0)
    expect(engine.cityProduction('capital').iron).toBeUndefined()

    const state = engine.snapshot()
    state.phase = 'divineGift'
    state.giftChoiceIds = ['gift-a', 'gift-c', 'gift-d']
    state.empire.researchedTechnologyIds = ['metallurgy']
    state.empire.cities[0].recruitedUnits = { 'future-unit': 2 }
    state.empire.cities[0].militaryPopulation = 10
    engine.restore(state)

    expect(engine.cityArmyFoodUpkeep('capital')).toBe(0)
    expect(engine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(engine.state.empire.resources.iron).toBe(20)
    expect(engine.state.empire.flags.recruitmentDisabled).toBeUndefined()
    expect(engine.state.empire.productionMultipliers.iron).toBeUndefined()
    expect(engine.placeBuilding('capital', 'slot-smithy', 'smithy')).toEqual({
      ok: false,
      message: 'That building is deferred: Future building system.',
    })
    expect(engine.assignProductionBoost('capital', 'mine')).toEqual({
      ok: false,
      message: 'That building is deferred: Future building system.',
    })
    expect(engine.recruitUnits('capital', 'future-unit')).toEqual({
      ok: false,
      message: 'That unit is deferred: Future combat system.',
    })
    expect(engine.research('metallurgy')).toEqual({
      ok: false,
      message: 'That research is deferred: Future research system.',
    })
    expect(engine.research('steel')).toEqual({
      ok: false,
      message: 'Missing prerequisite: metallurgy.',
    })
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
      resources?: Record<string, number>
      buildingInteractionLocks?: Record<string, number>
    }
    const legacyEmpire = legacy.empire as EmpiresCampaignState['empire'] & {
      cardFlagBonuses?: Record<string, number>
      productionBoostAssignments?: Array<{ cityId: string, buildingId: string }>
      destroyedRegionIds?: string[]
      buildingLevelBonuses?: EmpiresCampaignState['empire']['buildingLevelBonuses']
      researchUsage?: Record<string, string>
      giftResolutionTargets?: Record<string, string>
    }
    const legacyState = legacy as EmpiresCampaignState & {
      pendingResolution?: EmpiresCampaignState['pendingResolution']
    }
    delete legacyCity.buildingSlotAssignments
    delete legacyCity.recruitedUnits
    delete legacyCity.resources
    delete legacyCity.buildingInteractionLocks
    delete legacyEmpire.cardFlagBonuses
    delete legacyEmpire.productionBoostAssignments
    delete legacyEmpire.destroyedRegionIds
    delete legacyEmpire.buildingLevelBonuses
    delete legacyEmpire.researchUsage
    delete legacyEmpire.giftResolutionTargets
    delete legacyState.pendingResolution

    const restored = new EmpiresEndgameEngine(config, legacy)
    expect(restored.state.empire.cities[0].buildingSlotAssignments).toMatchObject({
      'slot-mine': 'mine',
      'slot-lumber': 'lumber',
    })
    expect(restored.state.empire.cities[0].recruitedUnits).toEqual({})
    expect(restored.state.empire.cities[0].resources).toEqual({})
    expect(restored.state.empire.cities[0].buildingInteractionLocks).toEqual({})
    expect(restored.state.empire.cardFlagBonuses).toEqual({})
    expect(restored.state.empire.productionBoostAssignments).toEqual([])
    expect(restored.state.empire.destroyedRegionIds).toEqual([])
    expect(restored.state.empire.buildingLevelBonuses).toEqual({})
    expect(restored.state.empire.researchUsage).toEqual({})
    expect(restored.state.empire.giftResolutionTargets).toEqual({})
    expect(restored.state.pendingResolution).toBeNull()
  })

  it('redrafts deferred or locked gift offers and cancels an unresolved deferred target', () => {
    const config = makeConfig()
    const deferredGift = config.gifts.definitions.find(gift => gift.id === 'gift-a')
    if (!deferredGift) throw new Error('Missing deferred gift fixture.')
    deferredGift.deferredReason = 'Future gift system.'
    deferredGift.resolution = { kind: 'cityResources' }
    config.gifts.definitions.push({
      id: 'gift-e',
      name: 'E',
      description: 'E',
      kind: 'boon',
      application: 'once',
      baseWeight: 1,
      performanceWeight: 0,
      effects: [],
    })
    const pending = new EmpiresEndgameEngine(config).snapshot()
    pending.phase = 'divineGift'
    pending.giftChoiceIds = ['gift-a', 'gift-b']
    pending.empire.claimedGiftIds = ['gift-a']
    pending.pendingResolution = {
      kind: 'cityResources',
      giftId: 'gift-a',
      eligibleTargetIds: ['capital'],
    }

    const restored = new EmpiresEndgameEngine(config, pending)
    expect(restored.state.pendingResolution).toBeNull()
    expect(restored.state.empire.claimedGiftIds).not.toContain('gift-a')
    expect(restored.state.giftChoiceIds).toHaveLength(3)
    expect(restored.state.giftChoiceIds).toEqual(expect.arrayContaining(['gift-c', 'gift-d', 'gift-e']))
    expect(restored.state.giftChoiceIds).not.toContain('gift-a')
    expect(restored.state.giftChoiceIds).not.toContain('gift-b')

    const relic = config.gifts.definitions.find(gift => gift.id === 'gift-b')
    if (!relic) throw new Error('Missing locked relic fixture.')
    relic.resolution = { kind: 'cityResources' }
    relic.effects = [{ kind: 'resource', resourceId: 'wood', amount: 3 }]
    const lockedPending = new EmpiresEndgameEngine(config).snapshot()
    lockedPending.phase = 'divineGift'
    lockedPending.giftChoiceIds = ['gift-b', 'gift-c', 'gift-d']
    lockedPending.empire.claimedGiftIds = ['gift-b']
    lockedPending.empire.activeGiftIds = ['gift-b']
    lockedPending.pendingResolution = {
      kind: 'cityResources',
      giftId: 'gift-b',
      eligibleTargetIds: ['capital'],
    }

    const lockedRestored = new EmpiresEndgameEngine(config, lockedPending)
    expect(lockedRestored.state.pendingResolution).toBeNull()
    expect(lockedRestored.state.empire.claimedGiftIds).not.toContain('gift-b')
    expect(lockedRestored.state.empire.activeGiftIds).not.toContain('gift-b')
    expect(lockedRestored.state.giftChoiceIds).not.toContain('gift-b')
  })

  it('advances old saves past events that are now explicitly deferred', () => {
    const config = makeConfig()
    const deferredEvent = config.empire.events.find(event => event.id === 'event-a')
    if (!deferredEvent) throw new Error('Missing deferred event fixture.')
    deferredEvent.deferredReason = 'Future event system.'
    const legacy = new EmpiresEndgameEngine(config).snapshot()
    legacy.phase = 'event'
    legacy.con = 4
    legacy.event = { eventId: 'event-a' }
    legacy.empire.flags.recruitmentDisabled = 1
    legacy.empire.cardFlagBonuses = { recruitmentDisabled: 1 }

    const restored = new EmpiresEndgameEngine(config, legacy)
    expect(restored.state.phase).toBe('cards')
    expect(restored.state.con).toBe(5)
    expect(restored.state.event).toBeNull()
    expect(restored.state.empire.flags.recruitmentDisabled).toBeUndefined()
    expect(restored.state.empire.cardFlagBonuses).toEqual({})
  })

  it('reconstructs destroyed regions and building bonuses from legacy flags and gift history once', () => {
    const config = makeConfig()
    config.empire.map.regions.push(
      {
        id: 'west',
        name: 'West',
        biome: 'forest',
        center: { x: 20, y: 50 },
        polygon: [],
        subregionIds: [],
        cityIds: [],
      },
      {
        id: 'north',
        name: 'North',
        biome: 'ice',
        center: { x: 50, y: 20 },
        polygon: [],
        subregionIds: [],
        cityIds: [],
      },
    )
    config.gifts.definitions[0].resolution = { kind: 'destroyRegion', regionId: 'west' }
    config.gifts.definitions[2].resolution = {
      kind: 'buildingLevelBonus',
      slots: ['farm', 'lumber'],
      amount: 1,
    }
    config.gifts.definitions[3].resolution = { kind: 'destroyRegion', regionId: 'north' }
    const legacy = new EmpiresEndgameEngine(config).snapshot()
    legacy.empire.flags.destroyWest = 1
    legacy.empire.flags.farmLevelBonus = 2
    legacy.empire.claimedGiftIds = ['gift-a', 'gift-c', 'gift-d']
    delete (legacy.empire as EmpiresCampaignState['empire'] & {
      destroyedRegionIds?: string[]
      buildingLevelBonuses?: EmpiresCampaignState['empire']['buildingLevelBonuses']
    }).destroyedRegionIds
    delete (legacy.empire as EmpiresCampaignState['empire'] & {
      buildingLevelBonuses?: EmpiresCampaignState['empire']['buildingLevelBonuses']
    }).buildingLevelBonuses

    const restored = new EmpiresEndgameEngine(config, legacy)
    expect(restored.state.empire.destroyedRegionIds).toEqual(expect.arrayContaining(['west', 'north']))
    expect(restored.state.empire.buildingLevelBonuses).toEqual({ farm: 2, lumber: 1 })

    const roundTrip = new EmpiresEndgameEngine(
      config,
      JSON.parse(JSON.stringify(restored.snapshot())) as EmpiresCampaignState,
    )
    expect(roundTrip.state.empire.destroyedRegionIds).toEqual(restored.state.empire.destroyedRegionIds)
    expect(roundTrip.state.empire.buildingLevelBonuses).toEqual({ farm: 2, lumber: 1 })
  })

  it('migrates a pre-arson mid-empire save exactly once when interaction locks are absent', () => {
    const config = makeConfig()
    const arson = config.cards.find(card => card.id === 'hearts-7') as EmpiresCardDefinition
    arson.inverted.effects = [{ kind: 'flag', flagId: 'militaryArson', amount: 1 }]
    const legacy = new EmpiresEndgameEngine(config).snapshot()
    legacy.phase = 'empire'
    legacy.durak.playerHand = [arson.id]
    legacy.cards[arson.id].inverted = true
    legacy.empire.flags.militaryArson = 1
    legacy.empire.cities[0].buildingLevels.barracks = 2
    legacy.empire.cities[0].recruitedUnits = { archers: 1, levy: 2 }
    delete (legacy.empire.cities[0] as EmpiresCampaignState['empire']['cities'][number] & {
      buildingInteractionLocks?: Record<string, number>
    }).buildingInteractionLocks

    const restored = new EmpiresEndgameEngine(config, legacy)
    const migratedUnitCount = Object.values(restored.state.empire.cities[0].recruitedUnits)
      .reduce((total, count) => total + count, 0)
    expect(migratedUnitCount).toBe(2)
    expect(restored.state.empire.cities[0].buildingLevels.barracks).toBe(1)
    expect(restored.state.empire.cities[0].buildingInteractionLocks.barracks).toBe(restored.state.con)

    const roundTrip = new EmpiresEndgameEngine(
      config,
      JSON.parse(JSON.stringify(restored.snapshot())) as EmpiresCampaignState,
    )
    expect(Object.values(roundTrip.state.empire.cities[0].recruitedUnits)
      .reduce((total, count) => total + count, 0)).toBe(2)
    expect(roundTrip.state.empire.cities[0].buildingLevels.barracks).toBe(1)
  })

  it('rebuilds pending target eligibility from trusted config and rejects mismatched pending gifts', () => {
    const config = makeConfig()
    config.gifts.definitions[0].resolution = { kind: 'meteorCity', damageLevels: 1 }
    const pending = new EmpiresEndgameEngine(config).snapshot()
    pending.phase = 'divineGift'
    pending.giftChoiceIds = ['gift-a']
    pending.empire.claimedGiftIds = ['gift-a']
    pending.empire.giftResolutionTargets = { forged: 'unknown-city' }
    pending.pendingResolution = {
      kind: 'meteorCity',
      giftId: 'gift-a',
      damageLevels: 999,
      eligibleTargetIds: ['unknown-city', 'capital'],
    }

    const restored = new EmpiresEndgameEngine(config, pending)
    expect(restored.state.pendingResolution).toEqual({
      kind: 'meteorCity',
      giftId: 'gift-a',
      damageLevels: 1,
      eligibleTargetIds: ['capital'],
    })
    expect(restored.state.empire.giftResolutionTargets).toEqual({})

    const mismatched = restored.snapshot()
    mismatched.pendingResolution = {
      kind: 'cityResources',
      giftId: 'gift-a',
      eligibleTargetIds: ['capital'],
    }
    expect(() => new EmpiresEndgameEngine(config, mismatched)).toThrow(
      'Pending resolution does not match its gift definition',
    )
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

  it('treats the operational barracks recruit value as a per-city cap and lets Hearts-7 bypass it', () => {
    const config = makeConfig()
    config.empire.populationClasses[0].canRecruit = true
    config.empire.units = [{
      id: 'levy',
      name: 'Levy',
      foodUpkeep: 0,
      populationCost: 1,
      timeCostDays: 1,
      resourceCosts: [],
      dependencies: [{ kind: 'building', buildingId: 'barracks', level: 1, scope: 'sameCity' }],
    }]
    const barracks = config.empire.buildings.find(building => building.id === 'barracks')
    if (barracks) {
      barracks.levels[0].effects = [{
        kind: 'flag',
        flagId: 'equippedRecruitCapacity',
        amount: 2,
      }]
    }
    config.empire.cities[0].buildingLevels.barracks = 1
    config.empire.cities[0].militaryPopulation = 10
    config.empire.cities[0].baseProduction.food = 2_000
    const recruiters = config.cards.find(card => card.id === 'hearts-7') as EmpiresCardDefinition
    recruiters.normal.effects = [{
      kind: 'flag',
      flagId: 'unlimitedTavernRecruitment',
      amount: 1,
    }]

    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    state.empire.flags.equippedRecruitCapacity = 100
    engine.restore(state)

    expect(engine.cityRecruitmentRemaining('capital')).toBe(2)
    expect(engine.recruitUnits('capital', 'levy', 2)).toMatchObject({ ok: true })
    expect(engine.cityRecruitmentRemaining('capital')).toBe(0)
    expect(engine.recruitUnits('capital', 'levy')).toEqual({
      ok: false,
      message: 'The city has reached its equipped recruitment capacity.',
    })

    const bypass = engine.snapshot()
    bypass.empire.flags.unlimitedTavernRecruitment = 1
    engine.restore(bypass)
    expect(engine.cityRecruitmentRemaining('capital')).toBeNull()
    expect(engine.recruitUnits('capital', 'levy')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities[0].recruitedUnits.levy).toBe(3)
  })

  it('lets researched Conservation counter the inverted famine-year production penalty', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1_000
    const famine = config.cards.find(card => card.id === 'spades-8') as EmpiresCardDefinition
    famine.inverted.effects = [
      { kind: 'resourceMultiplier', resourceId: 'food', multiplier: 0.5 },
      { kind: 'flag', flagId: 'famineYear', amount: 1 },
    ]
    config.empire.technologies.push({
      id: 'conservation',
      name: 'Conservation',
      category: 'technology',
      groupId: 'science',
      timeCostDays: 0,
      resourceCosts: [],
      prerequisites: [],
      effects: [{ kind: 'flag', flagId: 'famineYearCounter', amount: 1 }],
    })

    const unprotected = new EmpiresEndgameEngine(config)
    const unprotectedState = unprotected.snapshot()
    unprotectedState.phase = 'divineGift'
    unprotectedState.durak.playerHand = [famine.id]
    unprotectedState.cards[famine.id].inverted = true
    unprotectedState.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    unprotected.restore(unprotectedState)
    expect(unprotected.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(unprotected.cityProduction('capital').food).toBe(500)

    const protectedEngine = new EmpiresEndgameEngine(config)
    const researchState = protectedEngine.snapshot()
    researchState.phase = 'empire'
    researchState.empire.daysRemaining = 59
    protectedEngine.restore(researchState)
    expect(protectedEngine.research('conservation')).toMatchObject({ ok: true })
    const protectedState = protectedEngine.snapshot()
    protectedState.phase = 'divineGift'
    protectedState.durak.playerHand = [famine.id]
    protectedState.cards[famine.id].inverted = true
    protectedState.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    protectedEngine.restore(protectedState)
    expect(protectedEngine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(protectedEngine.state.empire.flags.famineYearCounter).toBe(1)
    expect(protectedEngine.cityProduction('capital').food).toBe(1_000)
  })

  it('derives Granary famine protection and provision efficiency from its operational level', () => {
    const config = makeConfig()
    config.empire.cities[0].baseProduction.food = 1_000
    config.empire.cities[0].buildingLevels.granary = 1
    const municipalSlot = config.empire.cities[0].slots.find(slot => slot.kind === 'municipal')
    if (municipalSlot) municipalSlot.buildingId = 'granary'
    config.empire.buildings.push({
      id: 'granary',
      name: 'Granary',
      slot: 'municipal',
      levels: [
        {
          level: 1,
          timeCostDays: 0,
          foodCost: 0,
          resourceCosts: [],
          dependencies: [],
          facilityLocks: [],
          effects: [{ kind: 'flag', flagId: 'famineProtectionTurns', amount: 1 }],
        },
        {
          level: 2,
          timeCostDays: 0,
          foodCost: 0,
          resourceCosts: [],
          dependencies: [],
          facilityLocks: [],
          effects: [{ kind: 'flag', flagId: 'provisionEfficiencyPercent', amount: 10 }],
        },
      ],
    })
    const famine = config.cards.find(card => card.id === 'spades-8') as EmpiresCardDefinition
    famine.inverted.effects = [
      { kind: 'resourceMultiplier', resourceId: 'food', multiplier: 0.5 },
      { kind: 'flag', flagId: 'famineYear', amount: 1 },
    ]

    const protectedEngine = new EmpiresEndgameEngine(config)
    const famineState = protectedEngine.snapshot()
    famineState.phase = 'divineGift'
    famineState.durak.playerHand = [famine.id]
    famineState.cards[famine.id].inverted = true
    famineState.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    protectedEngine.restore(famineState)
    expect(protectedEngine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(protectedEngine.cityProduction('capital').food).toBe(1_000)

    const efficientConfig = JSON.parse(JSON.stringify(config)) as EmpiresEndgameConfig
    efficientConfig.empire.cities[0].buildingLevels.granary = 2
    efficientConfig.empire.initialFlags = { provisionEfficiencyPercent: 90 }
    const efficientEngine = new EmpiresEndgameEngine(efficientConfig)
    const efficientState = efficientEngine.snapshot()
    efficientState.phase = 'divineGift'
    efficientState.durak.playerHand = [famine.id]
    efficientState.cards[famine.id].inverted = true
    efficientState.giftChoiceIds = ['gift-a', 'gift-b', 'gift-c']
    efficientEngine.restore(efficientState)
    expect(efficientEngine.chooseGift('gift-a')).toMatchObject({ ok: true })
    expect(efficientEngine.cityProduction('capital').food).toBe(1_000)
    expect(efficientEngine.cityFoodConsumption('capital')).toBe(900)
  })

  it('settles Trade Levy income from idle building levels and surplus food exactly once', () => {
    const config = makeConfig()
    config.empire.resources.push({ id: 'gold', name: 'Gold' })
    config.empire.initialResources.gold = 0
    config.empire.initialFlags = {
      idleBuildingGoldBase: 999,
      surplusFoodPerGold: 1,
    }
    config.empire.cities[0].baseProduction.food = 3_000
    config.empire.cities[0].buildingLevels.temple = 1
    config.empire.cities[0].buildingLevels.tradeLevy = 1
    const municipalSlot = config.empire.cities[0].slots.find(slot => slot.kind === 'municipal')
    if (municipalSlot) municipalSlot.buildingId = 'tradeLevy'
    config.empire.buildings.push({
      id: 'tradeLevy',
      name: 'Trade Levy',
      slot: 'municipal',
      levels: [{
        level: 1,
        timeCostDays: 0,
        foodCost: 0,
        resourceCosts: [],
        dependencies: [],
        facilityLocks: [],
        effects: [
          { kind: 'flag', flagId: 'idleBuildingGoldBase', amount: 10 },
          { kind: 'flag', flagId: 'surplusFoodPerGold', amount: 1_000 },
        ],
      }],
    })

    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    engine.restore(state)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.resources.gold).toBe(12)
    expect(engine.finishEmpire()).toMatchObject({ ok: false })
    expect(engine.state.empire.resources.gold).toBe(12)
  })

  it('uses Small Temple city ledgers for construction and recruitment with deterministic 10% loss', () => {
    const config = makeConfig()
    config.empire.populationClasses[0].canRecruit = true
    config.empire.initialResources.wood = 0
    config.empire.initialResources.iron = 0
    const temple = config.empire.buildings.find(building => building.id === 'temple')
    if (temple) {
      temple.levels[0].effects = [{
        kind: 'flag',
        flagId: 'templarTransferLossPercent',
        amount: 10,
      }]
    }
    const smithy = config.empire.buildings.find(building => building.id === 'smithy')
    if (smithy) {
      smithy.levels[0].resourceCosts = [{ resourceId: 'wood', amount: 100 }]
      smithy.levels[0].facilityLocks = []
    }
    config.empire.units = [{
      id: 'levy',
      name: 'Levy',
      foodUpkeep: 0,
      populationCost: 1,
      timeCostDays: 1,
      resourceCosts: [{ resourceId: 'iron', amount: 90 }],
      dependencies: [{ kind: 'building', buildingId: 'barracks', level: 1, scope: 'sameCity' }],
    }]
    config.empire.cities[0].buildingLevels.temple = 1
    config.empire.cities[0].buildingLevels.barracks = 1
    config.empire.cities[0].militaryPopulation = 10
    config.empire.cities[0].baseProduction.food = 2_000
    const sourceCity = config.empire.cities[0]
    const zeta = JSON.parse(JSON.stringify(sourceCity)) as typeof sourceCity
    zeta.id = 'zeta'
    zeta.name = 'Zeta'
    const alpha = JSON.parse(JSON.stringify(sourceCity)) as typeof sourceCity
    alpha.id = 'alpha'
    alpha.name = 'Alpha'
    config.empire.cities.push(zeta, alpha)
    config.empire.map.regions[0].cityIds.push(zeta.id, alpha.id)

    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    state.empire.daysRemaining = 59
    state.empire.flags.templarTransferLossPercent = 99
    const capital = state.empire.cities.find(city => city.id === 'capital')
    const alphaState = state.empire.cities.find(city => city.id === 'alpha')
    const zetaState = state.empire.cities.find(city => city.id === 'zeta')
    if (!capital || !alphaState || !zetaState) throw new Error('Missing test cities')
    capital.resources = { wood: 10, iron: 0 }
    alphaState.resources = { wood: 60, iron: 100 }
    zetaState.resources = { wood: 100, iron: 100 }
    engine.restore(state)

    expect(engine.cityAvailableResource('capital', 'wood')).toBeCloseTo(154)
    expect(engine.cityAvailableResource('capital', 'wood', false)).toBe(10)
    expect(engine.upgradeBuilding('capital', 'smithy')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities.find(city => city.id === 'capital')?.resources.wood).toBe(0)
    expect(engine.state.empire.cities.find(city => city.id === 'alpha')?.resources.wood).toBe(0)
    expect(engine.state.empire.cities.find(city => city.id === 'zeta')?.resources.wood).toBeCloseTo(60)

    expect(engine.recruitUnits('capital', 'levy')).toMatchObject({ ok: true })
    expect(engine.state.empire.cities.find(city => city.id === 'alpha')?.resources.iron).toBe(0)
    expect(engine.state.empire.cities.find(city => city.id === 'zeta')?.resources.iron).toBe(100)
  })

  it('pays Treasury gold from the saved pre-bonus millions exactly once at empire end', () => {
    const config = makeConfig()
    config.empire.resources.push({ id: 'gold', name: 'Gold' })
    config.empire.initialResources.gold = 2_500_000
    config.empire.initialFlags = { treasuryGoldPerSavedMillion: 1_000 }
    config.empire.cities[0].baseProduction.food = 1_500
    const engine = new EmpiresEndgameEngine(config)
    const state = engine.snapshot()
    state.phase = 'empire'
    engine.restore(state)

    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.empire.resources.gold).toBe(2_502_000)
    expect(engine.finishEmpire()).toMatchObject({ ok: false })
    expect(engine.state.empire.resources.gold).toBe(2_502_000)
  })

  it('removes the horse-theft event from future rolls after its disabling choice', () => {
    const config = makeConfig()
    config.empire.eventChance = 1
    config.empire.cities[0].baseProduction.food = 1_500
    config.empire.events = [{
      id: 'event-horse-theft',
      name: 'Horse Theft',
      description: 'Horse theft.',
      weight: 1,
      choices: [{
        id: 'hunt-thieves',
        label: 'Hunt thieves',
        effects: [{ kind: 'flag', flagId: 'horseTheftDisabled', amount: 1 }],
      }],
    }]
    const engine = new EmpiresEndgameEngine(config)
    const firstEmpire = engine.snapshot()
    firstEmpire.phase = 'empire'
    engine.restore(firstEmpire)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.event?.eventId).toBe('event-horse-theft')
    expect(engine.chooseEvent('hunt-thieves')).toMatchObject({ ok: true })
    expect(engine.state.empire.flags.horseTheftDisabled).toBe(1)

    const nextEmpire = engine.snapshot()
    nextEmpire.phase = 'empire'
    engine.restore(nextEmpire)
    expect(engine.finishEmpire()).toMatchObject({ ok: true })
    expect(engine.state.phase).toBe('cards')
    expect(engine.state.event).toBeNull()
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
