import {
  createEmpiresRngState,
  nextEmpiresRandom,
  pickEmpiresWeighted,
  pickEmpiresWeightedWithoutReplacement,
  shuffleEmpires,
} from './rng'
import { EMPIRES_RANKS, EMPIRES_SUITS } from './types'
import type {
  EmpiresActionResult,
  EmpiresActor,
  EmpiresBuildingDefinition,
  EmpiresBuildingLevelDefinition,
  EmpiresCampaignState,
  EmpiresCardDefinition,
  EmpiresCardInstance,
  EmpiresCityState,
  EmpiresDependency,
  EmpiresEffect,
  EmpiresEndgameConfig,
  EmpiresEventDefinition,
  EmpiresGiftDefinition,
  EmpiresPerformanceState,
  EmpiresPhase,
  EmpiresResourceAmount,
  EmpiresSnapshotEnvelope,
  EmpiresStateListener,
  EmpiresSuit,
  EmpiresTechnologyDefinition,
  EmpiresUnitDefinition,
} from './types'

const EFFECT_KINDS = [
  'resource',
  'resourceMultiplier',
  'time',
  'foodProduction',
  'population',
  'flag',
] as const

type EffectKind = typeof EFFECT_KINDS[number]

function cloneSerializable<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function success(message: string): EmpiresActionResult {
  return { ok: true, message }
}

function failure(message: string): EmpiresActionResult {
  return { ok: false, message }
}

function otherActor(actor: EmpiresActor): EmpiresActor {
  return actor === 'player' ? 'god' : 'player'
}

function emptyPerformance(): EmpiresPerformanceState {
  return {
    successfulDefenses: 0,
    godTakes: 0,
    maxCardsGivenToGodAtOnce: 0,
    cardsGivenToGod: 0,
    cardsTakenByPlayer: 0,
    boutsWon: 0,
    boutsLost: 0,
  }
}

function compareMetric(value: number, comparison: 'gte' | 'lte' | 'eq', threshold: number): boolean {
  if (comparison === 'gte') return value >= threshold
  if (comparison === 'lte') return value <= threshold
  return value === threshold
}

function uniqueIds<T extends { id: string }>(values: readonly T[]): boolean {
  return new Set(values.map(value => value.id)).size === values.length
}

export function validateEmpiresEndgameConfig(config: EmpiresEndgameConfig): string[] {
  const errors: string[] = []
  if (config.schemaVersion !== 1) errors.push('schemaVersion must be 1')
  if (!config.id.trim()) errors.push('config id is required')
  if (config.cards.length !== 53) errors.push('cards must contain exactly 53 definitions')
  if (!uniqueIds(config.cards)) errors.push('card definition ids must be unique')

  for (const suit of EMPIRES_SUITS) {
    for (const rank of EMPIRES_RANKS) {
      const count = config.cards.filter(card => card.suit === suit && card.rank === rank).length
      if (count !== 1) errors.push(`cards must contain exactly one ${suit}:${rank}`)
    }
  }
  const jokers = config.cards.filter(card => card.suit === 'joker' && card.rank === 'joker')
  if (jokers.length !== 1) errors.push('cards must contain exactly one Joker')
  if (config.cards.some(card => (card.suit === 'joker') !== (card.rank === 'joker'))) {
    errors.push('Joker suit and rank must be used together')
  }
  if (config.cards.some(card => card.drawUpgrade < 0 || !Number.isFinite(card.drawUpgrade))) {
    errors.push('card drawUpgrade values must be finite and non-negative')
  }
  if (!Number.isInteger(config.durak.handSize) || config.durak.handSize < 1) {
    errors.push('durak.handSize must be a positive integer')
  }
  if (!Number.isInteger(config.durak.maxAttackCards) || config.durak.maxAttackCards < 1) {
    errors.push('durak.maxAttackCards must be a positive integer')
  }
  if (!Number.isInteger(config.durak.boutsPerCon) || config.durak.boutsPerCon < 1) {
    errors.push('durak.boutsPerCon must be a positive integer')
  }
  if (config.gifts.choiceCount !== 3) errors.push('gifts.choiceCount must be 3')
  if (config.gifts.definitions.length < config.gifts.choiceCount) {
    errors.push('gift definitions must contain at least choiceCount entries')
  }
  if (!uniqueIds(config.gifts.definitions)) errors.push('gift ids must be unique')
  if (config.gifts.definitions.some(gift => gift.application !== 'once' && gift.application !== 'eachEmpire')) {
    errors.push('every gift must define a valid application mode')
  }
  if (config.empire.daysPerPhase < 0) errors.push('empire.daysPerPhase cannot be negative')
  if (config.empire.eventChance < 0 || config.empire.eventChance > 1) {
    errors.push('empire.eventChance must be between 0 and 1')
  }
  if (!uniqueIds(config.empire.cities)) errors.push('city ids must be unique')
  if (!uniqueIds(config.empire.buildings)) errors.push('building ids must be unique')
  if (!uniqueIds(config.empire.units ?? [])) errors.push('unit ids must be unique')
  if (!uniqueIds(config.empire.technologies)) errors.push('technology ids must be unique')
  if (!uniqueIds(config.empire.events)) errors.push('event ids must be unique')
  if (config.empire.populationClasses.some(
    definition => !Number.isFinite(definition.foodPerPerson) || definition.foodPerPerson < 0,
  )) {
    errors.push('population class foodPerPerson values must be finite and non-negative')
  }
  for (const building of config.empire.buildings) {
    const levels = building.levels.map(level => level.level)
    if (levels.some(level => !Number.isInteger(level) || level < 1)) {
      errors.push(`building ${building.id} levels must be positive integers`)
    }
    if (new Set(levels).size !== levels.length) {
      errors.push(`building ${building.id} has duplicate levels`)
    }
  }
  for (const unit of config.empire.units ?? []) {
    if (!Number.isFinite(unit.foodUpkeep) || unit.foodUpkeep < 0) {
      errors.push(`unit ${unit.id} foodUpkeep must be finite and non-negative`)
    }
    if (!Number.isFinite(unit.populationCost) || unit.populationCost < 0) {
      errors.push(`unit ${unit.id} populationCost must be finite and non-negative`)
    }
    if (!Number.isFinite(unit.timeCostDays) || unit.timeCostDays < 0) {
      errors.push(`unit ${unit.id} timeCostDays must be finite and non-negative`)
    }
    if (unit.resourceCosts.some(cost => !Number.isFinite(cost.amount) || cost.amount < 0)) {
      errors.push(`unit ${unit.id} resource costs must be finite and non-negative`)
    }
  }
  return errors
}

export class EmpiresEndgameEngine {
  readonly config: EmpiresEndgameConfig
  state: EmpiresCampaignState

  private readonly definitions = new Map<string, EmpiresCardDefinition>()
  private readonly buildingDefinitions = new Map<string, EmpiresBuildingDefinition>()
  private readonly technologyDefinitions = new Map<string, EmpiresTechnologyDefinition>()
  private readonly unitDefinitions = new Map<string, EmpiresUnitDefinition>()
  private readonly giftDefinitions = new Map<string, EmpiresGiftDefinition>()
  private readonly eventDefinitions = new Map<string, EmpiresEventDefinition>()
  private readonly listeners = new Set<EmpiresStateListener>()

  constructor(config: EmpiresEndgameConfig, snapshot?: EmpiresCampaignState) {
    const errors = validateEmpiresEndgameConfig(config)
    if (errors.length > 0) throw new Error(`Invalid Empire's Endgame config:\n${errors.join('\n')}`)
    this.config = cloneSerializable(config)
    for (const definition of this.config.cards) this.definitions.set(definition.id, definition)
    for (const definition of this.config.empire.buildings) {
      this.buildingDefinitions.set(definition.id, definition)
    }
    for (const definition of this.config.empire.technologies) {
      this.technologyDefinitions.set(definition.id, definition)
    }
    for (const definition of this.config.empire.units ?? []) this.unitDefinitions.set(definition.id, definition)
    for (const definition of this.config.gifts.definitions) this.giftDefinitions.set(definition.id, definition)
    for (const definition of this.config.empire.events) this.eventDefinitions.set(definition.id, definition)

    this.state = snapshot ? this.validateAndCloneSnapshot(snapshot) : this.createInitialState()
    this.refreshProductions()
  }

  subscribe(listener: EmpiresStateListener): () => void {
    this.listeners.add(listener)
    listener(this.snapshot())
    return () => this.listeners.delete(listener)
  }

  snapshot(): EmpiresCampaignState {
    return cloneSerializable(this.state)
  }

  snapshotEnvelope(savedAt = new Date().toISOString()): EmpiresSnapshotEnvelope {
    return { schemaVersion: 1, savedAt, state: this.snapshot() }
  }

  restore(snapshot: EmpiresCampaignState): void {
    this.state = this.validateAndCloneSnapshot(snapshot)
    this.refreshProductions()
    this.emit()
  }

  getDefinition(instanceOrId: EmpiresCardInstance | string): EmpiresCardDefinition {
    const instanceId = typeof instanceOrId === 'string' ? instanceOrId : instanceOrId.definitionId
    const instance = typeof instanceOrId === 'string' ? this.state.cards[instanceOrId] : null
    const definition = this.definitions.get(instance?.definitionId ?? instanceId)
    if (!definition) throw new Error(`Unknown card ${instanceId}`)
    return definition
  }

  currentActor(): EmpiresActor | null {
    if (this.state.phase !== 'cards') return null
    return this.state.durak.stage === 'defense'
      ? this.state.durak.defender
      : this.state.durak.attacker
  }

  legalAttackCardIds(actor = this.state.durak.attacker): string[] {
    const durak = this.state.durak
    if (this.state.phase !== 'cards' || actor !== durak.attacker) return []
    if (durak.stage !== 'attack' && durak.stage !== 'throwIn' && durak.stage !== 'taking') return []
    const capacity = Math.min(this.config.durak.maxAttackCards, durak.defenderHandAtBoutStart)
    if (durak.table.length >= capacity) return []
    const hand = this.hand(actor)
    if (durak.table.length === 0) {
      return hand.filter(cardId => this.canUseJokerForAttack(cardId, false))
    }
    const tableRanks = new Set(durak.table.flatMap((pair) => [
      this.getDefinition(pair.attackCardId).rank,
      ...(pair.defenseCardId ? [this.getDefinition(pair.defenseCardId).rank] : []),
    ]))
    return hand.filter((cardId) => {
      const definition = this.getDefinition(cardId)
      if (definition.rank === 'joker') return this.config.durak.joker.canThrowIn
      return tableRanks.has(definition.rank)
    })
  }

  legalDefenseCardIds(
    actor = this.state.durak.defender,
    attackIndex = this.firstUndefendedAttackIndex(),
  ): string[] {
    const durak = this.state.durak
    if (this.state.phase !== 'cards' || durak.stage !== 'defense' || actor !== durak.defender) return []
    const pair = durak.table[attackIndex]
    if (!pair || pair.defenseCardId) return []
    return this.hand(actor).filter(cardId => this.canCardBeat(cardId, pair.attackCardId))
  }

  canTake(actor: EmpiresActor = 'player'): boolean {
    const durak = this.state.durak
    return this.state.phase === 'cards'
      && actor === durak.defender
      && durak.table.length > 0
      && (durak.stage === 'defense' || durak.stage === 'throwIn')
  }

  canEndAttack(actor: EmpiresActor = 'player'): boolean {
    const durak = this.state.durak
    return this.state.phase === 'cards'
      && actor === durak.attacker
      && durak.table.length > 0
      && (
        durak.stage === 'taking'
        || (durak.stage === 'throwIn' && durak.table.every(pair => pair.defenseCardId))
      )
  }

  canCardBeat(defenseCardId: string, attackCardId: string): boolean {
    const defense = this.getDefinition(defenseCardId)
    const attack = this.getDefinition(attackCardId)
    const joker = this.config.durak.joker
    if (defense.rank === 'joker') {
      if (!joker.canDefend) return false
      if (joker.rule === 'highest-unbeatable' || joker.rule === 'trump') return attack.rank !== 'joker'
      if (attack.rank === 'joker') return false
      if (attack.suit === this.state.durak.trumpSuit) return false
      return (joker.ordinaryStrength ?? 0) > this.rankStrength(attack.rank)
    }
    if (attack.rank === 'joker') {
      if (joker.rule !== 'ordinary') return false
      return defense.suit === this.state.durak.trumpSuit
        && this.rankStrength(defense.rank) > (joker.ordinaryStrength ?? 0)
    }
    if (defense.suit === attack.suit) return this.rankStrength(defense.rank) > this.rankStrength(attack.rank)
    return defense.suit === this.state.durak.trumpSuit && attack.suit !== this.state.durak.trumpSuit
  }

  playCard(cardId: string, attackIndex?: number): EmpiresActionResult {
    return this.playCardFor('player', cardId, attackIndex)
  }

  playCardFor(actor: EmpiresActor, cardId: string, attackIndex?: number): EmpiresActionResult {
    if (this.state.phase !== 'cards') return failure('Cards can only be played during the card phase.')
    if (this.currentActor() !== actor) return failure(`It is not ${actor}'s turn.`)
    return this.state.durak.stage === 'defense'
      ? this.playDefense(actor, cardId, attackIndex)
      : this.playAttack(actor, cardId)
  }

  takeCards(actor: EmpiresActor = 'player'): EmpiresActionResult {
    if (!this.canTake(actor)) return failure('Only the current defender can take an active attack.')
    this.state.durak.stage = 'taking'
    return this.commit(`${actor} will take the attack after final throw-ins.`)
  }

  endAttack(actor: EmpiresActor = 'player'): EmpiresActionResult {
    const durak = this.state.durak
    if (!this.canEndAttack(actor)) return failure('Only the attacker can end a resolved attack.')
    if (durak.stage === 'taking') return this.finalizeTake()
    if (durak.defender === 'player') {
      this.state.performance.successfulDefenses += 1
      this.state.performance.boutsWon += 1
    } else {
      this.state.performance.boutsLost += 1
    }
    this.resolveBout(true)
    return this.commit('The defense succeeded.')
  }

  advanceGod(): EmpiresActionResult {
    if (this.state.phase !== 'cards') return failure('God only acts during the card phase.')
    if (this.currentActor() !== 'god') return failure('It is not God\'s turn.')
    const stage = this.state.durak.stage
    if (stage === 'defense') {
      const attackIndex = this.firstUndefendedAttackIndex()
      const defenses = this.sortCardIds(this.legalDefenseCardIds('god', attackIndex))
      if (defenses.length === 0) return this.takeCards('god')
      return this.playDefense('god', defenses[0], attackIndex)
    }
    const attacks = this.sortCardIds(this.legalAttackCardIds('god'))
    if (attacks.length > 0) return this.playAttack('god', attacks[0])
    if (stage === 'throwIn' || stage === 'taking') return this.endAttack('god')
    return failure('God has no legal card action.')
  }

  chooseGift(giftId: string): EmpiresActionResult {
    if (this.state.phase !== 'divineGift') return failure('No divine gift is currently offered.')
    if (!this.state.giftChoiceIds.includes(giftId)) return failure('That gift is not one of the choices.')
    const gift = this.giftDefinitions.get(giftId)
    if (!gift) return failure('Unknown divine gift.')
    this.state.empire.claimedGiftIds.push(giftId)
    if (gift.application === 'eachEmpire') this.state.empire.activeGiftIds.push(giftId)
    this.startEmpirePhase()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`Gift ${giftId} accepted.`)
  }

  improveCard(cardId: string): EmpiresActionResult {
    const availability = this.checkCardUpgradeAvailability(cardId, this.config.upgrades.improveCost)
    if (!availability.ok) return availability
    const instance = this.state.cards[cardId]
    const maximum = this.getDefinition(cardId).maxLevel ?? this.config.upgrades.defaultMaxLevel
    if (instance.level >= maximum) return failure('The card is already at its maximum level.')
    this.state.upgradePoints -= this.config.upgrades.improveCost
    instance.level += 1
    return this.commit(`${this.getDefinition(cardId).name} improved to level ${instance.level}.`)
  }

  restoreCard(cardId: string): EmpiresActionResult {
    const availability = this.checkCardUpgradeAvailability(cardId, this.config.upgrades.restoreCost)
    if (!availability.ok) return availability
    const instance = this.state.cards[cardId]
    if (!instance.inverted) return failure('The card is already in its normal form.')
    this.state.upgradePoints -= this.config.upgrades.restoreCost
    instance.inverted = false
    return this.commit(`${this.getDefinition(cardId).name} restored.`)
  }

  upgradeBuilding(cityId: string, buildingId: string): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('Buildings can only be upgraded in the empire phase.')
    const city = this.city(cityId)
    const building = this.buildingDefinitions.get(buildingId)
    if (!city || !building) return failure('Unknown city or building.')
    const currentLevel = city.buildingLevels[buildingId] ?? 0
    const nextLevel = building.levels.find(level => level.level === currentLevel + 1)
    if (!nextLevel) return failure('The building has no further upgrade.')
    const assignedSlotId = Object.entries(city.buildingSlotAssignments)
      .find(([, assignedBuildingId]) => assignedBuildingId === buildingId)?.[0]
    if (!assignedSlotId) return failure('The building is not assigned to a city slot.')
    const check = this.checkEmpireAction(city, nextLevel)
    if (!check.ok) return check

    this.completeBuildingLevel(city, building, nextLevel)
    return this.commit(`${building.name} upgraded to level ${nextLevel.level}.`)
  }

  placeBuilding(cityId: string, slotId: string, buildingId: string): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('Buildings can only be placed in the empire phase.')
    const city = this.city(cityId)
    const cityDefinition = this.cityDefinition(cityId)
    const slot = cityDefinition?.slots.find(item => item.id === slotId)
    const building = this.buildingDefinitions.get(buildingId)
    if (!city || !slot || !building) return failure('Unknown city, slot, or building.')
    if (slot.kind !== building.slot) return failure('The building does not match that slot.')
    if ((city.buildingLevels[buildingId] ?? 0) > 0) return failure('That building is already placed.')
    const currentBuildingId = city.buildingSlotAssignments[slotId]
    if (currentBuildingId && (city.buildingLevels[currentBuildingId] ?? 0) > 0) {
      return failure('That building slot is occupied.')
    }
    const assignedElsewhere = Object.entries(city.buildingSlotAssignments).some(
      ([assignedSlotId, assignedBuildingId]) => assignedSlotId !== slotId && assignedBuildingId === buildingId,
    )
    if (assignedElsewhere) return failure('That building is assigned to another slot.')
    const firstLevel = building.levels.find(level => level.level === 1)
    if (!firstLevel) return failure('The building has no initial level.')
    const check = this.checkEmpireAction(city, firstLevel)
    if (!check.ok) return check

    this.completeBuildingLevel(city, building, firstLevel, slotId)
    return this.commit(`${building.name} placed in ${slot.id}.`)
  }

  recruitUnits(cityId: string, unitId: string, count = 1): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('Units can only be recruited in the empire phase.')
    if (!Number.isInteger(count) || count <= 0) return failure('Unit count must be a positive integer.')
    if ((this.state.empire.flags.recruitmentDisabled ?? 0) > 0) {
      return failure('Recruitment is disabled for this empire phase.')
    }
    const city = this.city(cityId)
    const unit = this.unitDefinitions.get(unitId)
    if (!city || !unit) return failure('Unknown city or unit.')
    const missingDependency = this.firstMissingDependency(unit.dependencies, city, true)
    if (missingDependency) return failure(`Missing prerequisite: ${missingDependency}.`)
    const populationCost = unit.populationCost * count
    if (city.militaryPopulation < populationCost) return failure('Not enough recruitable military population.')
    const timeCost = unit.timeCostDays
    if (this.state.empire.daysRemaining < timeCost) return failure('Not enough days remain.')
    const resourceCosts = [...unit.resourceCosts.reduce((totals, cost) => {
      totals.set(cost.resourceId, (totals.get(cost.resourceId) ?? 0) + cost.amount * count)
      return totals
    }, new Map<string, number>())].map(([resourceId, amount]) => ({ resourceId, amount }))
    const missingResource = this.firstMissingResource(resourceCosts)
    if (missingResource) return failure(`Not enough ${missingResource}.`)
    if (city.population < populationCost || this.recruitablePopulation(city) < populationCost) {
      return failure('Not enough recruitable population.')
    }

    const projectedCity = cloneSerializable(city)
    this.consumeRecruitmentPopulation(projectedCity, populationCost)
    projectedCity.recruitedUnits[unitId] = (projectedCity.recruitedUnits[unitId] ?? 0) + count
    this.updateOperationalBuildings(projectedCity)
    const projectedFoodProduction = this.productionForCity(projectedCity)[this.config.empire.foodResourceId] ?? 0
    const projectedFoodConsumption = this.foodConsumptionForCity(projectedCity)
    if (projectedFoodProduction - city.foodCommitted < projectedFoodConsumption) {
      return failure('The city does not have enough food surplus.')
    }

    this.payResources(resourceCosts)
    this.state.empire.daysRemaining -= timeCost
    this.consumeRecruitmentPopulation(city, populationCost)
    city.recruitedUnits[unitId] = (city.recruitedUnits[unitId] ?? 0) + count
    this.refreshProductions()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`${count} ${unit.name} recruited.`)
  }

  assignProductionBoost(cityId: string, buildingId: string): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('Production can only be boosted in the empire phase.')
    const city = this.city(cityId)
    const building = this.buildingDefinitions.get(buildingId)
    if (!city || !building) return failure('Unknown city or building.')
    if (building.slot !== 'farm' && building.slot !== 'lumber' && building.slot !== 'mine') {
      return failure('Only a farm, lumber operation, or mine can receive the production boost.')
    }
    if ((city.operationalBuildingLevels[buildingId] ?? 0) < 1) {
      return failure('The target building is not operational.')
    }
    if (this.hasProductionBoost(cityId, buildingId)) return success('That building already has the production boost.')
    const limit = this.productionBoostAssignmentLimit()
    if (limit < 1) return failure('No production boost assignments are available.')
    if (limit === 1) {
      this.state.empire.productionBoostAssignments = [{ cityId, buildingId }]
    } else if (this.state.empire.productionBoostAssignments.length >= limit) {
      return failure('All production boost assignments are already in use.')
    } else {
      this.state.empire.productionBoostAssignments.push({ cityId, buildingId })
    }
    this.refreshProductions()
    return this.commit(`${building.name} received the production boost.`)
  }

  clearProductionBoost(cityId: string, buildingId?: string): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('Production boosts can only be cleared in the empire phase.')
    if (!this.city(cityId)) return failure('Unknown city.')
    const previousLength = this.state.empire.productionBoostAssignments.length
    this.state.empire.productionBoostAssignments = this.state.empire.productionBoostAssignments.filter(
      assignment => assignment.cityId !== cityId
        || (buildingId !== undefined && assignment.buildingId !== buildingId),
    )
    if (this.state.empire.productionBoostAssignments.length === previousLength) {
      return failure('No matching production boost assignment exists.')
    }
    this.refreshProductions()
    return this.commit('Production boost assignment cleared.')
  }

  research(technologyId: string): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('Research is only available in the empire phase.')
    const technology = this.technologyDefinitions.get(technologyId)
    if (!technology) return failure('Unknown technology.')
    if (this.state.empire.researchedTechnologyIds.includes(technologyId)) {
      return failure('That research is already complete.')
    }
    const dependency = this.firstMissingDependency(technology.prerequisites)
    if (dependency) return failure(`Missing prerequisite: ${dependency}.`)
    if (this.state.empire.daysRemaining < technology.timeCostDays) return failure('Not enough days remain.')
    const missingResource = this.firstMissingResource(technology.resourceCosts)
    if (missingResource) return failure(`Not enough ${missingResource}.`)

    this.payResources(technology.resourceCosts)
    this.state.empire.daysRemaining -= technology.timeCostDays
    this.state.empire.researchedTechnologyIds.push(technologyId)
    this.applyEffects(technology.effects, 0)
    this.refreshProductions()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`${technology.name} researched.`)
  }

  chooseEvent(choiceId: string): EmpiresActionResult {
    if (this.state.phase !== 'event' || !this.state.event) return failure('No event choice is pending.')
    const event = this.eventDefinitions.get(this.state.event.eventId)
    const choice = event?.choices.find(item => item.id === choiceId)
    if (!choice) return failure('Unknown event choice.')
    const missingResource = this.firstMissingResource(choice.resourceCosts ?? [])
    if (missingResource) return failure(`Not enough ${missingResource}.`)
    this.payResources(choice.resourceCosts ?? [])
    this.applyEffects(choice.effects, 0)
    this.refreshProductions()
    this.clearCardFlagBonuses()
    if (this.totalPopulation() <= this.config.empire.defeatPopulationAtOrBelow) {
      this.setOutcome('defeat', 'The empire lost its population.')
    } else {
      this.startNextCon()
    }
    return this.commit(`Event choice ${choiceId} resolved.`)
  }

  finishEmpire(): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('There is no empire phase to finish.')
    this.finishEmpireInternal()
    return this.commit('The empire phase ended.')
  }

  cityArmyFoodUpkeep(cityId: string): number {
    const city = this.city(cityId)
    if (!city) return 0
    return this.armyFoodUpkeepForCity(city)
  }

  cityFoodConsumption(cityId: string): number {
    const city = this.city(cityId)
    if (!city) return 0
    return this.foodConsumptionForCity(city)
  }

  private armyFoodUpkeepForCity(city: EmpiresCityState): number {
    return Object.entries(city.recruitedUnits).reduce((total, [unitId, count]) => (
      total + Math.max(0, count) * (this.unitDefinitions.get(unitId)?.foodUpkeep ?? 0)
    ), 0)
  }

  private foodConsumptionForCity(city: EmpiresCityState): number {
    const classDefinitions = new Map(
      this.config.empire.populationClasses.map(definition => [definition.id, definition]),
    )
    const classConsumption = Object.entries(city.populationClasses).reduce((total, [classId, count]) => {
      const definition = classDefinitions.get(classId)
      return definition ? total + Math.max(0, count) * definition.foodPerPerson : total
    }, 0)
    const hasDefinedClasses = Object.keys(city.populationClasses).some(classId => classDefinitions.has(classId))
    const civilianConsumption = hasDefinedClasses ? classConsumption : city.population
    return civilianConsumption + this.armyFoodUpkeepForCity(city)
  }

  hasProductionBoost(cityId: string, buildingId: string): boolean {
    return this.state.empire.productionBoostAssignments.some(
      assignment => assignment.cityId === cityId && assignment.buildingId === buildingId,
    )
  }

  cityProduction(cityId: string): Record<string, number> {
    const city = this.city(cityId)
    if (!city) return {}
    return this.productionForCity(city)
  }

  private productionForCity(city: EmpiresCityState): Record<string, number> {
    const production = { ...city.baseProduction }
    for (const [buildingId, level] of Object.entries(city.operationalBuildingLevels)) {
      if (level <= 0) continue
      const definition = this.buildingDefinitions.get(buildingId)
      const currentLevel = definition?.levels.find(item => item.level === level)
      const targetedMultiplier = this.hasProductionBoost(city.id, buildingId)
        ? this.productionBoostPercent() / 100
        : 1
      const workerProductivityMultiplier = definition?.slot === 'farm'
        ? this.empirePercentMultiplier('peasantProductivityPercent')
        : 1
      for (const item of currentLevel?.production ?? []) {
        production[item.resourceId] = (production[item.resourceId] ?? 0)
          + item.amount * targetedMultiplier * workerProductivityMultiplier
      }
    }
    const foodId = this.config.empire.foodResourceId
    production[foodId] = (production[foodId] ?? 0) + (this.state.empire.passiveFoodBonuses[city.id] ?? 0)
    for (const [resourceId, amount] of Object.entries(production)) {
      production[resourceId] = amount * (this.state.empire.productionMultipliers[resourceId] ?? 1)
    }
    return production
  }

  private createInitialState(): EmpiresCampaignState {
    const rng = createEmpiresRngState(this.config.seed)
    const cards: Record<string, EmpiresCardInstance> = Object.fromEntries(this.config.cards.map(definition => [
      definition.id,
      { id: definition.id, definitionId: definition.id, level: 0, inverted: false },
    ]))
    const deck = shuffleEmpires(this.config.cards.map(card => card.id), rng)
    const trumpSuit = this.resolveTrumpSuit(deck)
    const playerHand: string[] = []
    const godHand: string[] = []
    const state: EmpiresCampaignState = {
      schemaVersion: 1,
      configId: this.config.id,
      phase: 'cards',
      rng,
      cards,
      durak: {
        deck,
        playerHand,
        godHand,
        discard: [],
        table: [],
        trumpSuit,
        attacker: 'player',
        defender: 'god',
        stage: 'attack',
        defenderHandAtBoutStart: 0,
        bout: 1,
      },
      performance: emptyPerformance(),
      con: 1,
      boutsInCon: 0,
      upgradePoints: 0,
      performanceScore: 0,
      giftChoiceIds: [],
      empire: {
        daysRemaining: 0,
        resources: { ...this.config.empire.initialResources },
        flags: { ...(this.config.empire.initialFlags ?? {}) },
        cities: this.config.empire.cities.map(city => ({
          id: city.id,
          name: city.name,
          regionId: city.regionId,
          population: city.population,
          militaryPopulation: city.militaryPopulation,
          populationClasses: { ...city.populationClasses },
          baseProduction: { ...city.baseProduction },
          buildingLevels: { ...city.buildingLevels },
          operationalBuildingLevels: { ...city.buildingLevels },
          buildingSlotAssignments: Object.fromEntries(
            city.slots.flatMap(slot => slot.buildingId ? [[slot.id, slot.buildingId]] : []),
          ),
          recruitedUnits: {},
          lockedFacilities: {},
          foodCommitted: 0,
          lastProduction: {},
          lastStarvationLoss: 0,
        })),
        researchedTechnologyIds: [],
        claimedGiftIds: [],
        activeGiftIds: [],
        productionMultipliers: {},
        passiveFoodBonuses: {},
        cardFlagBonuses: {},
        productionBoostAssignments: [],
      },
      event: null,
      outcomeReason: null,
      revision: 0,
    }
    this.drawToHand(state, 'player')
    this.drawToHand(state, 'god')
    const attacker = this.initialAttacker(state)
    state.durak.attacker = attacker
    state.durak.defender = otherActor(attacker)
    state.durak.defenderHandAtBoutStart = this.handFromState(state, state.durak.defender).length
    return state
  }

  private validateAndCloneSnapshot(snapshot: EmpiresCampaignState): EmpiresCampaignState {
    if (snapshot.schemaVersion !== 1) throw new Error('Unsupported Empire\'s Endgame snapshot schema')
    if (snapshot.configId !== this.config.id) throw new Error('Snapshot belongs to a different config')
    const state = cloneSerializable(snapshot)
    for (const city of state.empire.cities) {
      city.operationalBuildingLevels ??= { ...city.buildingLevels }
      city.buildingSlotAssignments ??= Object.fromEntries(
        (this.cityDefinition(city.id)?.slots ?? []).flatMap(
          slot => slot.buildingId ? [[slot.id, slot.buildingId]] : [],
        ),
      )
      city.recruitedUnits ??= {}
    }
    state.empire.cardFlagBonuses ??= state.phase === 'empire' || state.phase === 'event'
      ? this.heldCardFlagBonuses(state)
      : {}
    state.empire.productionBoostAssignments ??= []
    return state
  }

  private playAttack(actor: EmpiresActor, cardId: string): EmpiresActionResult {
    if (!this.legalAttackCardIds(actor).includes(cardId)) return failure('That card is not a legal attack.')
    const defenderIsTaking = this.state.durak.stage === 'taking'
    this.removeFromHand(actor, cardId)
    this.state.durak.table.push({ attackCardId: cardId, defenseCardId: null })
    this.state.durak.stage = defenderIsTaking ? 'taking' : 'defense'
    return this.commit(`${actor} attacked with ${this.getDefinition(cardId).name}.`)
  }

  private playDefense(actor: EmpiresActor, cardId: string, attackIndex?: number): EmpiresActionResult {
    const index = attackIndex ?? this.firstUndefendedAttackIndex()
    if (!this.legalDefenseCardIds(actor, index).includes(cardId)) return failure('That card cannot beat the attack.')
    this.removeFromHand(actor, cardId)
    this.state.durak.table[index].defenseCardId = cardId
    this.state.durak.stage = this.state.durak.table.some(pair => !pair.defenseCardId)
      ? 'defense'
      : 'throwIn'
    return this.commit(`${actor} defended with ${this.getDefinition(cardId).name}.`)
  }

  private finalizeTake(): EmpiresActionResult {
    const durak = this.state.durak
    const defender = durak.defender
    const collected = durak.table.flatMap(pair => [
      pair.attackCardId,
      ...(pair.defenseCardId ? [pair.defenseCardId] : []),
    ])
    this.hand(defender).push(...collected)
    if (defender === 'player' && durak.attacker === 'god') {
      for (const pair of durak.table) this.state.cards[pair.attackCardId].inverted = true
      this.state.performance.cardsTakenByPlayer += collected.length
    }
    if (defender === 'god') {
      this.state.performance.godTakes += 1
      this.state.performance.cardsGivenToGod += collected.length
      this.state.performance.maxCardsGivenToGodAtOnce = Math.max(
        this.state.performance.maxCardsGivenToGodAtOnce,
        collected.length,
      )
    }
    if (durak.attacker === 'player') this.state.performance.boutsWon += 1
    else this.state.performance.boutsLost += 1
    this.resolveBout(false)
    return this.commit(`${defender} took ${collected.length} cards.`)
  }

  private resolveBout(successfulDefense: boolean): void {
    const durak = this.state.durak
    const boutAttacker = durak.attacker
    const boutDefender = durak.defender
    if (successfulDefense) {
      durak.discard.push(...durak.table.flatMap(pair => [
        pair.attackCardId,
        ...(pair.defenseCardId ? [pair.defenseCardId] : []),
      ]))
    }
    durak.table = []
    this.drawToHand(this.state, boutAttacker)
    this.drawToHand(this.state, boutDefender)
    if (successfulDefense) {
      durak.attacker = boutDefender
      durak.defender = boutAttacker
    }
    durak.stage = 'attack'
    durak.bout += 1
    this.state.boutsInCon += 1

    const winner = this.finishedCardGameWinner(boutAttacker, boutDefender)
    if (winner) {
      this.setOutcome(winner === 'player' ? 'victory' : 'defeat', `${winner} emptied their hand.`)
      return
    }
    durak.defenderHandAtBoutStart = this.hand(durak.defender).length
    if (this.state.boutsInCon >= this.config.durak.boutsPerCon) this.beginDivineGift()
  }

  private finishedCardGameWinner(
    boutAttacker: EmpiresActor,
    boutDefender: EmpiresActor,
  ): EmpiresActor | null {
    if (this.state.durak.deck.length > 0) return null
    const playerEmpty = this.state.durak.playerHand.length === 0
    const godEmpty = this.state.durak.godHand.length === 0
    if (!playerEmpty && !godEmpty) return null
    if (playerEmpty && !godEmpty) return 'player'
    if (godEmpty && !playerEmpty) return 'god'
    const setting = this.config.durak.simultaneousEmptyWinner
    if (setting === 'attacker') return boutAttacker
    if (setting === 'defender') return boutDefender
    return setting
  }

  private beginDivineGift(): void {
    const score = this.config.durak.scoringRules.reduce((total, rule) => {
      const metric = this.state.performance[rule.metric]
      return total + (compareMetric(metric, rule.comparison, rule.threshold) ? rule.points : 0)
    }, 0)
    this.state.performanceScore = score
    this.state.upgradePoints += score
    const weighted = this.config.gifts.definitions
      .filter(gift => (gift.minimumPerformance ?? Number.NEGATIVE_INFINITY) <= score)
      .filter(gift => (gift.maximumPerformance ?? Number.POSITIVE_INFINITY) >= score)
      .map(gift => ({
        ...gift,
        weight: Math.max(Number.EPSILON, gift.baseWeight + gift.performanceWeight * score),
      }))
    this.state.giftChoiceIds = pickEmpiresWeightedWithoutReplacement(
      weighted,
      this.config.gifts.choiceCount,
      this.state.rng,
    ).map(gift => gift.id)
    this.state.phase = 'divineGift'
  }

  private startEmpirePhase(): void {
    this.clearCardFlagBonuses()
    this.state.phase = 'empire'
    this.state.event = null
    this.state.empire.daysRemaining = this.config.empire.daysPerPhase
      - this.state.durak.playerHand.reduce(
        (total, cardId) => total + this.getDefinition(cardId).timeCostDays,
        0,
      )
    this.state.empire.productionMultipliers = {}
    this.state.empire.passiveFoodBonuses = {}
    for (const city of this.state.empire.cities) {
      city.lockedFacilities = {}
      city.foodCommitted = 0
      city.lastStarvationLoss = 0
    }

    const phaseEffectKinds = new Set<EffectKind>(['resource', 'time', 'population', 'flag'])
    const timeEffectKind = new Set<EffectKind>(['time'])
    for (const giftId of this.currentEmpireGiftIds()) {
      this.applyEffects(this.giftDefinitions.get(giftId)?.effects ?? [], 0, phaseEffectKinds)
    }
    for (const technologyId of this.state.empire.researchedTechnologyIds) {
      this.applyEffects(this.technologyDefinitions.get(technologyId)?.effects ?? [], 0, timeEffectKind)
    }
    for (const cardId of this.state.durak.playerHand) {
      const instance = this.state.cards[cardId]
      const definition = this.getDefinition(instance)
      const face = instance.inverted ? definition.inverted : definition.normal
      this.applyEffects(face.effects, instance.level, phaseEffectKinds)
      this.recordCardFlagBonuses(face.effects, instance.level)
    }
    for (const city of this.state.empire.cities) this.updateOperationalBuildings(city)
    for (const city of this.state.empire.cities) {
      for (const [buildingId, level] of Object.entries(city.operationalBuildingLevels)) {
        const levelDefinition = this.buildingDefinitions.get(buildingId)?.levels
          .find(item => item.level === level)
        this.applyEffects(levelDefinition?.effects ?? [], 0, timeEffectKind)
      }
    }
    this.state.empire.daysRemaining = Math.max(0, this.state.empire.daysRemaining)
    this.refreshProductions()
  }

  private finishEmpireInternal(): void {
    if (this.state.phase !== 'empire') return
    this.refreshProductions()
    const foodId = this.config.empire.foodResourceId
    for (const city of this.state.empire.cities) {
      for (const [resourceId, amount] of Object.entries(city.lastProduction)) {
        if (resourceId === foodId) continue
        this.state.empire.resources[resourceId] = (this.state.empire.resources[resourceId] ?? 0) + amount
      }
      const availableFood = Math.max(0, (city.lastProduction[foodId] ?? 0) - city.foodCommitted)
      const deficit = Math.max(0, this.cityFoodConsumption(city.id) - availableFood)
      const loss = deficit / 2 * this.empirePercentMultiplier('starvationLossMultiplierPercent')
      city.lastStarvationLoss = loss
      if (loss > 0) this.setCityPopulation(city, Math.max(0, city.population - loss))
    }
    this.refreshProductions()
    if (this.totalPopulation() <= this.config.empire.defeatPopulationAtOrBelow) {
      this.setOutcome('defeat', 'The empire starved.')
      return
    }

    const eligible = this.config.empire.events.filter(event => this.eventIsEligible(event))
    if (eligible.length > 0 && nextEmpiresRandom(this.state.rng) < this.config.empire.eventChance) {
      const picked = pickEmpiresWeighted(eligible, this.state.rng)
      if (picked) {
        this.state.phase = 'event'
        this.state.event = { eventId: picked.id }
        return
      }
    }
    this.clearCardFlagBonuses()
    this.startNextCon()
  }

  private startNextCon(): void {
    this.state.phase = 'cards'
    this.state.con += 1
    this.state.boutsInCon = 0
    this.state.performance = emptyPerformance()
    this.state.performanceScore = 0
    this.state.giftChoiceIds = []
    this.state.event = null
    this.state.durak.stage = 'attack'
    this.state.durak.defenderHandAtBoutStart = this.hand(this.state.durak.defender).length
    this.refreshProductions()
  }

  private checkCardUpgradeAvailability(cardId: string, cost: number): EmpiresActionResult {
    if (this.state.phase !== 'divineGift' && this.state.phase !== 'empire') {
      return failure('Cards can only be changed between card cons.')
    }
    if (!this.state.durak.playerHand.includes(cardId)) return failure('The player is not holding that card.')
    if (this.state.upgradePoints < cost) return failure('Not enough upgrade points.')
    return success('Card change is available.')
  }

  private checkEmpireAction(
    city: EmpiresCityState,
    level: EmpiresBuildingLevelDefinition,
  ): EmpiresActionResult {
    const missingDependency = this.firstMissingDependency(level.dependencies, city)
    if (missingDependency) return failure(`Missing prerequisite: ${missingDependency}.`)
    if (this.state.empire.daysRemaining < level.timeCostDays) return failure('Not enough days remain.')
    const missingResource = this.firstMissingResource(level.resourceCosts)
    if (missingResource) return failure(`Not enough ${missingResource}.`)
    const foodProduction = city.lastProduction[this.config.empire.foodResourceId] ?? 0
    const foodSurplus = foodProduction - this.cityFoodConsumption(city.id) - city.foodCommitted
    if (foodSurplus < level.foodCost) return failure('The city does not have enough food surplus.')
    for (const lock of level.facilityLocks) {
      if (city.lockedFacilities[lock]) return failure(`The city's ${lock} is already committed this phase.`)
      const providerId = this.config.empire.lockProviderBuildingIds[lock]
      if ((city.operationalBuildingLevels[providerId] ?? 0) < 1) {
        return failure(`The city has no working ${lock}.`)
      }
    }
    return success('Empire action is available.')
  }

  private completeBuildingLevel(
    city: EmpiresCityState,
    building: EmpiresBuildingDefinition,
    level: EmpiresBuildingLevelDefinition,
    slotId?: string | null,
  ): void {
    this.payResources(level.resourceCosts)
    this.state.empire.daysRemaining -= level.timeCostDays
    city.foodCommitted += level.foodCost
    for (const lock of level.facilityLocks) city.lockedFacilities[lock] = `${building.id}:${level.level}`
    if (slotId) city.buildingSlotAssignments[slotId] = building.id
    city.buildingLevels[building.id] = level.level
    this.applyEffects(level.effects ?? [], 0)
    this.refreshProductions()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
  }

  private firstMissingDependency(
    dependencies: readonly EmpiresDependency[],
    city?: EmpiresCityState,
    operationalSameCityBuildings = false,
  ): string | null {
    for (const dependency of dependencies) {
      if (dependency.kind === 'technology') {
        if (!this.state.empire.researchedTechnologyIds.includes(dependency.technologyId)) {
          return dependency.technologyId
        }
      } else if (dependency.kind === 'flag') {
        if ((this.state.empire.flags[dependency.flagId] ?? 0) < dependency.minimum) return dependency.flagId
      } else {
        const cities = dependency.scope !== 'anyCity' && city ? [city] : this.state.empire.cities
        const useOperational = operationalSameCityBuildings && dependency.scope !== 'anyCity' && Boolean(city)
        if (!cities.some((item) => {
          const levels = useOperational ? item.operationalBuildingLevels : item.buildingLevels
          return (levels[dependency.buildingId] ?? 0) >= dependency.level
        })) {
          return `${dependency.buildingId} ${dependency.level}`
        }
      }
    }
    return null
  }

  private firstMissingResource(costs: readonly EmpiresResourceAmount[]): string | null {
    return costs.find(cost => (this.state.empire.resources[cost.resourceId] ?? 0) < cost.amount)?.resourceId ?? null
  }

  private payResources(costs: readonly EmpiresResourceAmount[]): void {
    for (const cost of costs) {
      this.state.empire.resources[cost.resourceId] = (this.state.empire.resources[cost.resourceId] ?? 0)
        - cost.amount
    }
  }

  private applyEffects(
    effects: readonly EmpiresEffect[],
    level: number,
    allowedKinds?: ReadonlySet<EffectKind>,
  ): void {
    for (const effect of effects) {
      if (allowedKinds && !allowedKinds.has(effect.kind)) continue
      if (effect.kind === 'resource') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        this.state.empire.resources[effect.resourceId] = (this.state.empire.resources[effect.resourceId] ?? 0)
          + amount
      } else if (effect.kind === 'resourceMultiplier') {
        const multiplier = effect.multiplier + (effect.multiplierPerLevel ?? 0) * level
        this.state.empire.productionMultipliers[effect.resourceId] = (
          this.state.empire.productionMultipliers[effect.resourceId] ?? 1
        ) * multiplier
      } else if (effect.kind === 'time') {
        this.state.empire.daysRemaining += effect.days + (effect.daysPerLevel ?? 0) * level
      } else if (effect.kind === 'foodProduction') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        const cityIds = effect.cityId ? [effect.cityId] : this.state.empire.cities.map(city => city.id)
        for (const cityId of cityIds) {
          this.state.empire.passiveFoodBonuses[cityId] = (
            this.state.empire.passiveFoodBonuses[cityId] ?? 0
          ) + amount
        }
      } else if (effect.kind === 'population') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        const cities = effect.cityId
          ? this.state.empire.cities.filter(city => city.id === effect.cityId)
          : this.state.empire.cities
        const perCity = cities.length > 0 ? amount / cities.length : 0
        for (const city of cities) this.setCityPopulation(city, Math.max(0, city.population + perCity))
      } else {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        this.state.empire.flags[effect.flagId] = (this.state.empire.flags[effect.flagId] ?? 0) + amount
      }
    }
  }

  private recordCardFlagBonuses(effects: readonly EmpiresEffect[], level: number): void {
    for (const effect of effects) {
      if (effect.kind !== 'flag') continue
      const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
      this.state.empire.cardFlagBonuses[effect.flagId] = (
        this.state.empire.cardFlagBonuses[effect.flagId] ?? 0
      ) + amount
    }
  }

  private heldCardFlagBonuses(state: EmpiresCampaignState): Record<string, number> {
    const bonuses: Record<string, number> = {}
    for (const cardId of state.durak.playerHand) {
      const instance = state.cards[cardId]
      const definition = instance ? this.definitions.get(instance.definitionId) : null
      if (!instance || !definition) throw new Error(`Unknown card ${cardId}`)
      const face = instance.inverted ? definition.inverted : definition.normal
      for (const effect of face.effects) {
        if (effect.kind !== 'flag') continue
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * instance.level
        bonuses[effect.flagId] = (bonuses[effect.flagId] ?? 0) + amount
      }
    }
    return bonuses
  }

  private clearCardFlagBonuses(): void {
    for (const [flagId, amount] of Object.entries(this.state.empire.cardFlagBonuses ?? {})) {
      const remaining = (this.state.empire.flags[flagId] ?? 0) - amount
      if (Math.abs(remaining) < Number.EPSILON) delete this.state.empire.flags[flagId]
      else this.state.empire.flags[flagId] = remaining
    }
    this.state.empire.cardFlagBonuses = {}
  }

  private setCityPopulation(city: EmpiresCityState, population: number): void {
    const previous = city.population
    city.population = population
    city.militaryPopulation = Math.min(city.militaryPopulation, population)
    const classEntries = Object.entries(city.populationClasses)
    if (classEntries.length === 0) return
    if (previous > 0 && population <= previous) {
      const ratio = population / previous
      for (const [classId, count] of classEntries) city.populationClasses[classId] = count * ratio
      return
    }
    const added = population - previous
    if (added > 0) city.populationClasses[classEntries[0][0]] += added
  }

  private recruitablePopulation(city: EmpiresCityState): number {
    const representedClasses = this.config.empire.populationClasses.filter(
      definition => Object.prototype.hasOwnProperty.call(city.populationClasses, definition.id),
    )
    if (representedClasses.length === 0) return city.population
    return representedClasses.reduce((total, definition) => (
      definition.canRecruit ? total + Math.max(0, city.populationClasses[definition.id] ?? 0) : total
    ), 0)
  }

  private consumeRecruitmentPopulation(city: EmpiresCityState, amount: number): void {
    const recruitableClasses = this.config.empire.populationClasses
      .filter(definition => definition.canRecruit)
      .map(definition => ({ id: definition.id, count: Math.max(0, city.populationClasses[definition.id] ?? 0) }))
      .filter(entry => entry.count > 0)

    city.population = Math.max(0, city.population - amount)
    city.militaryPopulation = Math.max(0, city.militaryPopulation - amount)
    if (recruitableClasses.length === 0) {
      const classEntries = Object.entries(city.populationClasses)
      const classTotal = classEntries.reduce((total, [, count]) => total + Math.max(0, count), 0)
      if (classTotal <= 0) return
      for (const [classId, count] of classEntries) {
        city.populationClasses[classId] = Math.max(0, count - amount * Math.max(0, count) / classTotal)
      }
      return
    }

    let remainingAmount = amount
    let remainingPopulation = recruitableClasses.reduce((total, entry) => total + entry.count, 0)
    for (const [index, entry] of recruitableClasses.entries()) {
      const removed = index === recruitableClasses.length - 1
        ? remainingAmount
        : remainingAmount * entry.count / remainingPopulation
      city.populationClasses[entry.id] = Math.max(0, entry.count - removed)
      remainingAmount -= removed
      remainingPopulation -= entry.count
    }
  }

  private eventIsEligible(event: EmpiresEventDefinition): boolean {
    if ((event.minimumCon ?? Number.NEGATIVE_INFINITY) > this.state.con) return false
    if ((event.maximumCon ?? Number.POSITIVE_INFINITY) < this.state.con) return false
    return !this.firstMissingDependency(event.prerequisites ?? [])
  }

  private refreshProductions(): void {
    this.normalizeProductionBoostAssignments()
    for (const city of this.state.empire.cities) this.updateOperationalBuildings(city)
    this.rebuildProductionEffects()
    for (const city of this.state.empire.cities) city.lastProduction = this.cityProduction(city.id)
  }

  private normalizeProductionBoostAssignments(): void {
    const seen = new Set<string>()
    this.state.empire.productionBoostAssignments = this.state.empire.productionBoostAssignments
      .filter((assignment) => {
        const key = `${assignment.cityId}\u0000${assignment.buildingId}`
        if (seen.has(key) || !this.city(assignment.cityId) || !this.buildingDefinitions.has(assignment.buildingId)) {
          return false
        }
        seen.add(key)
        return true
      })
      .slice(0, this.productionBoostAssignmentLimit())
  }

  private rebuildProductionEffects(): void {
    this.state.empire.productionMultipliers = {}
    this.state.empire.passiveFoodBonuses = {}
    if (this.state.phase !== 'empire' && this.state.phase !== 'event') return

    const productionKinds = new Set<EffectKind>(['resourceMultiplier', 'foodProduction'])
    for (const giftId of this.currentEmpireGiftIds()) {
      this.applyEffects(this.giftDefinitions.get(giftId)?.effects ?? [], 0, productionKinds)
    }
    for (const technologyId of this.state.empire.researchedTechnologyIds) {
      this.applyEffects(this.technologyDefinitions.get(technologyId)?.effects ?? [], 0, productionKinds)
    }
    for (const city of this.state.empire.cities) {
      for (const [buildingId, level] of Object.entries(city.operationalBuildingLevels)) {
        const levelDefinition = this.buildingDefinitions.get(buildingId)?.levels
          .find(item => item.level === level)
        this.applyEffects(levelDefinition?.effects ?? [], 0, productionKinds)
      }
    }
    for (const cardId of this.state.durak.playerHand) {
      const instance = this.state.cards[cardId]
      const definition = this.getDefinition(instance)
      const face = instance.inverted ? definition.inverted : definition.normal
      this.applyEffects(face.effects, instance.level, productionKinds)
    }
  }

  private currentEmpireGiftIds(): string[] {
    const giftIds = [...this.state.empire.activeGiftIds]
    if (this.state.phase !== 'empire' && this.state.phase !== 'event') return giftIds
    const claimedGiftIds = this.state.empire.claimedGiftIds
    const currentGiftId = claimedGiftIds[claimedGiftIds.length - 1]
    if (currentGiftId && this.giftDefinitions.get(currentGiftId)?.application === 'once') {
      giftIds.push(currentGiftId)
    }
    return giftIds
  }

  private workerDemand(city: EmpiresCityState): number {
    return Object.entries(city.operationalBuildingLevels).reduce((total, [buildingId, level]) => {
      const current = this.buildingDefinitions.get(buildingId)?.levels.find(item => item.level === level)
      return total + (current?.workerDemand ?? 0)
    }, 0)
  }

  private updateOperationalBuildings(city: EmpiresCityState): void {
    city.operationalBuildingLevels = { ...city.buildingLevels }
    const workforce = this.availableWorkforce(city)
    while (this.workerDemand(city) > workforce) {
      const candidates = Object.entries(city.operationalBuildingLevels)
        .filter(([buildingId, level]) => {
          if (level <= 0) return false
          const definition = this.buildingDefinitions.get(buildingId)
          return (definition?.levels.find(item => item.level === level)?.workerDemand ?? 0) > 0
        })
        .sort(([leftId, leftLevel], [rightId, rightLevel]) => (
          rightLevel - leftLevel
          || this.shutdownPriority(leftId) - this.shutdownPriority(rightId)
          || leftId.localeCompare(rightId)
        ))
      const selected = candidates[0]
      if (!selected) break
      const [buildingId, currentLevel] = selected
      const nextLevel = this.buildingDefinitions.get(buildingId)?.levels
        .filter(level => level.level < currentLevel)
        .sort((left, right) => right.level - left.level)[0]?.level ?? 0
      city.operationalBuildingLevels[buildingId] = nextLevel
    }
  }

  private availableWorkforce(city: EmpiresCityState): number {
    const workingClasses = this.config.empire.populationClasses.filter(definition => definition.canWork)
    if (workingClasses.length === 0 || Object.keys(city.populationClasses).length === 0) {
      return Math.max(0, city.population - city.militaryPopulation)
    }
    return Math.max(0, workingClasses.reduce(
      (total, definition) => total + (city.populationClasses[definition.id] ?? 0),
      0,
    ))
  }

  private shutdownPriority(buildingId: string): number {
    const slot = this.buildingDefinitions.get(buildingId)?.slot
    if (slot === 'mine') return 0
    if (slot === 'lumber') return 1
    if (slot === 'farm') return 2
    return 3
  }

  private totalPopulation(): number {
    return this.state.empire.cities.reduce((total, city) => total + city.population, 0)
  }

  private resolveTrumpSuit(deck: readonly string[]): EmpiresSuit {
    if (this.config.durak.fixedTrumpSuit) return this.config.durak.fixedTrumpSuit
    for (const cardId of deck) {
      const definition = this.definitions.get(cardId)
      if (definition && definition.suit !== 'joker') return definition.suit
    }
    return this.config.durak.joker.trumpFallbackSuit
  }

  private initialAttacker(state: EmpiresCampaignState): EmpiresActor {
    const setting = this.config.durak.initialAttacker
    if (setting !== 'lowest-trump') return setting
    const lowest = (actor: EmpiresActor): number => this.handFromState(state, actor).reduce((result, cardId) => {
      const definition = this.definitions.get(state.cards[cardId].definitionId)
      if (!definition || definition.suit !== state.durak.trumpSuit || definition.rank === 'joker') return result
      return Math.min(result, this.rankStrength(definition.rank))
    }, Number.POSITIVE_INFINITY)
    const player = lowest('player')
    const god = lowest('god')
    return god < player ? 'god' : 'player'
  }

  private drawToHand(state: EmpiresCampaignState, actor: EmpiresActor): void {
    const hand = this.handFromState(state, actor)
    while (hand.length < this.config.durak.handSize && state.durak.deck.length > 0) {
      const cardId = state.durak.deck.pop()
      if (!cardId) break
      hand.push(cardId)
      if (actor === 'player') {
        const instance = state.cards[cardId]
        const definition = this.definitions.get(instance.definitionId)
        const maximum = definition?.maxLevel ?? this.config.upgrades.defaultMaxLevel
        instance.inverted = false
        instance.level = Math.min(maximum, instance.level + (definition?.drawUpgrade ?? 0))
      }
    }
  }

  private rankStrength(rank: EmpiresCardDefinition['rank']): number {
    return rank === 'joker' ? Number.POSITIVE_INFINITY : EMPIRES_RANKS.indexOf(rank)
  }

  private canUseJokerForAttack(cardId: string, throwIn: boolean): boolean {
    const definition = this.getDefinition(cardId)
    if (definition.rank !== 'joker') return true
    return throwIn ? this.config.durak.joker.canThrowIn : this.config.durak.joker.canAttack
  }

  private sortCardIds(cardIds: readonly string[]): string[] {
    return [...cardIds].sort((leftId, rightId) => {
      const left = this.getDefinition(leftId)
      const right = this.getDefinition(rightId)
      const trump = this.state.durak.trumpSuit
      const leftTrump = left.suit === trump || left.rank === 'joker' ? 1 : 0
      const rightTrump = right.suit === trump || right.rank === 'joker' ? 1 : 0
      return leftTrump - rightTrump || this.rankStrength(left.rank) - this.rankStrength(right.rank)
    })
  }

  private firstUndefendedAttackIndex(): number {
    return this.state.durak.table.findIndex(pair => !pair.defenseCardId)
  }

  private hand(actor: EmpiresActor): string[] {
    return this.handFromState(this.state, actor)
  }

  private handFromState(state: EmpiresCampaignState, actor: EmpiresActor): string[] {
    return actor === 'player' ? state.durak.playerHand : state.durak.godHand
  }

  private removeFromHand(actor: EmpiresActor, cardId: string): void {
    const hand = this.hand(actor)
    hand.splice(hand.indexOf(cardId), 1)
  }

  private city(cityId: string): EmpiresCityState | null {
    return this.state.empire.cities.find(city => city.id === cityId) ?? null
  }

  private cityDefinition(cityId: string) {
    return this.config.empire.cities.find(city => city.id === cityId) ?? null
  }

  private productionBoostAssignmentLimit(flags = this.state.empire.flags): number {
    const configured = flags.productionBoostAssignmentLimit
    return Number.isFinite(configured) ? Math.max(0, Math.floor(configured)) : 1
  }

  private productionBoostPercent(): number {
    const configured = this.state.empire.flags.productionBoostPercent
    return Number.isFinite(configured) ? Math.max(0, configured) : 200
  }

  private empirePercentMultiplier(flagId: string): number {
    const adjustment = this.state.empire.flags[flagId]
    return Number.isFinite(adjustment) ? Math.max(0, (100 + adjustment) / 100) : 1
  }

  private setOutcome(phase: Extract<EmpiresPhase, 'victory' | 'defeat'>, reason: string): void {
    this.state.phase = phase
    this.state.outcomeReason = reason
    this.state.event = null
  }

  private commit(message: string): EmpiresActionResult {
    this.state.revision += 1
    this.emit()
    return success(message)
  }

  private emit(): void {
    if (this.listeners.size === 0) return
    const snapshot = this.snapshot()
    for (const listener of this.listeners) listener(snapshot)
  }
}
