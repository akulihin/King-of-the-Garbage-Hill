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
  EmpiresBuildingSlotKind,
  EmpiresCampaignState,
  EmpiresCardDefinition,
  EmpiresCardInstance,
  EmpiresCityState,
  EmpiresDependency,
  EmpiresEffect,
  EmpiresEndgameConfig,
  EmpiresEventDefinition,
  EmpiresGiftDefinition,
  EmpiresPendingGiftResolution,
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

const BUILDING_SLOT_KINDS: readonly EmpiresBuildingSlotKind[] = [
  'farm',
  'lumber',
  'mine',
  'smithy',
  'barracks',
  'unique',
  'municipal',
]

const FAMINE_RATIONING_EVENT_ID = 'event-famine-rationing'

function cloneSerializable<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function success(message: string): EmpiresActionResult {
  return { ok: true, message }
}

function failure(message: string): EmpiresActionResult {
  return { ok: false, message }
}

interface EmpiresResourcePaymentPlan {
  covered: number
  targetSpend: number
  empireSpend: number
  donorSpends: Array<{ cityId: string, amount: number }>
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
  if (config.schemaVersion !== 2) errors.push('schemaVersion must be 2')
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
  if (
    (config.empire.initialFlags?.relicsUnlocked ?? 0) <= 0
    && config.gifts.definitions.filter(
      gift => !gift.deferredReason && gift.kind !== 'relic',
    ).length < config.gifts.choiceCount
  ) {
    errors.push('pre-unlock gift definitions must contain at least choiceCount non-relic entries')
  }
  if (!uniqueIds(config.gifts.definitions)) errors.push('gift ids must be unique')
  if (config.gifts.definitions.some(gift => gift.application !== 'once' && gift.application !== 'eachEmpire')) {
    errors.push('every gift must define a valid application mode')
  }
  for (const gift of config.gifts.definitions) {
    const resolution = gift.resolution
    if (resolution?.kind === 'meteorCity'
      && (!Number.isFinite(resolution.damageLevels) || resolution.damageLevels <= 0)) {
      errors.push(`gift ${gift.id} meteor damageLevels must be finite and positive`)
    }
    if (resolution?.kind === 'destroyRegion'
      && !config.empire.map.regions.some(region => region.id === resolution.regionId)) {
      errors.push(`gift ${gift.id} references unknown region ${resolution.regionId}`)
    }
    if (resolution?.kind === 'buildingLevelBonus'
      && (
        !Number.isFinite(resolution.amount)
        || resolution.slots.length === 0
        || resolution.slots.some(slot => !BUILDING_SLOT_KINDS.includes(slot))
      )) {
      errors.push(`gift ${gift.id} buildingLevelBonus must define slots and a finite amount`)
    }
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
    if (this.state.pendingResolution) return failure('Resolve the current divine gift target first.')
    if (!this.state.giftChoiceIds.includes(giftId)) return failure('That gift is not one of the choices.')
    const gift = this.giftDefinitions.get(giftId)
    if (!gift) return failure('Unknown divine gift.')
    if (gift.deferredReason) return failure(`That divine gift is deferred: ${gift.deferredReason}`)
    if (!this.giftIsUnlocked(gift)) {
      return failure('Relics are locked until Divine Presence is researched.')
    }

    if (gift.resolution?.kind === 'cityResources' || gift.resolution?.kind === 'meteorCity') {
      const eligibleTargetIds = this.accessibleCityIds()
      if (eligibleTargetIds.length === 0) return failure('No accessible city can receive that divine gift.')
      this.claimGift(gift)
      this.state.pendingResolution = gift.resolution.kind === 'cityResources'
        ? { kind: 'cityResources', giftId, eligibleTargetIds }
        : {
            kind: 'meteorCity',
            giftId,
            damageLevels: Math.max(0, Math.floor(gift.resolution.damageLevels)),
            eligibleTargetIds,
          }
      return this.commit(`Gift ${giftId} requires a city target.`)
    }

    this.claimGift(gift)
    if (gift.application === 'once') {
      this.applyFixedGiftResolution(gift)
      this.applyOneShotGiftEffects(gift)
    }
    this.startEmpirePhase()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`Gift ${giftId} accepted.`)
  }

  resolvePendingTarget(targetId: string): EmpiresActionResult {
    const pending = this.state.pendingResolution
    if (!pending) return failure('No gift target is pending.')
    if (!pending.eligibleTargetIds.includes(targetId)) return failure('That target is not eligible.')
    if (!this.isCityAccessible(targetId)) return failure('That city is not accessible.')
    const gift = this.giftDefinitions.get(pending.giftId)
    if (!gift) return failure('Unknown divine gift.')
    if (gift.deferredReason) return failure(`That divine gift is deferred: ${gift.deferredReason}`)
    if (!this.giftIsUnlocked(gift)) {
      return failure('Relics are locked until Divine Presence is researched.')
    }

    this.state.empire.giftResolutionTargets[gift.id] = targetId
    if (gift.application === 'once') {
      this.applyTargetedGiftResolution(gift, targetId, pending)
    }
    this.state.pendingResolution = null
    this.startEmpirePhase()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`Gift ${gift.id} resolved on ${targetId}.`)
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
    if (building.deferredReason) return failure(`That building is deferred: ${building.deferredReason}`)
    if (!this.isCityAccessible(cityId)) return failure('That city is not accessible.')
    if (this.isBuildingInteractionLocked(city, buildingId)) {
      return failure('That building is locked for the current con.')
    }
    const currentLevel = city.buildingLevels[buildingId] ?? 0
    const nextLevel = building.levels.find(level => level.level === currentLevel + 1)
    if (!nextLevel) return failure('The building has no further upgrade.')
    const assignedSlotId = Object.entries(city.buildingSlotAssignments)
      .find(([, assignedBuildingId]) => assignedBuildingId === buildingId)?.[0]
    if (!assignedSlotId) return failure('The building is not assigned to a city slot.')
    const check = this.checkEmpireAction(city, building, nextLevel)
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
    if (building.deferredReason) return failure(`That building is deferred: ${building.deferredReason}`)
    if (!this.isCityAccessible(cityId)) return failure('That city is not accessible.')
    if (this.isBuildingInteractionLocked(city, buildingId)) {
      return failure('That building is locked for the current con.')
    }
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
    const check = this.checkEmpireAction(city, building, firstLevel)
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
    if (unit.deferredReason) return failure(`That unit is deferred: ${unit.deferredReason}`)
    if (!this.isCityAccessible(cityId)) return failure('That city is not accessible.')
    const missingDependency = this.firstMissingDependency(unit.dependencies, city, true)
    if (missingDependency) return failure(`Missing prerequisite: ${missingDependency}.`)
    const equippedRecruitCapacity = this.operationalBuildingFlagValue(city, 'equippedRecruitCapacity')
    if (
      equippedRecruitCapacity !== null
      && (this.state.empire.flags.unlimitedTavernRecruitment ?? 0) <= 0
      && this.recruitedUnitCount(city) + count > equippedRecruitCapacity
    ) {
      return failure('The city has reached its equipped recruitment capacity.')
    }
    const populationCost = unit.populationCost * count
    if (city.militaryPopulation < populationCost) return failure('Not enough recruitable military population.')
    const timeCost = unit.timeCostDays
    if (this.state.empire.daysRemaining < timeCost) return failure('Not enough days remain.')
    const resourceCosts = [...unit.resourceCosts.reduce((totals, cost) => {
      totals.set(cost.resourceId, (totals.get(cost.resourceId) ?? 0) + cost.amount * count)
      return totals
    }, new Map<string, number>())].map(([resourceId, amount]) => ({ resourceId, amount }))
    const missingResource = this.firstMissingResource(resourceCosts, city, true)
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
    const immediateFoodCost = resourceCosts
      .filter(cost => cost.resourceId === this.config.empire.foodResourceId)
      .reduce((total, cost) => total + cost.amount, 0)
    if (!this.canFundFoodDemand(
      projectedCity,
      projectedFoodProduction,
      projectedFoodConsumption,
      0,
      immediateFoodCost,
      true,
    )) {
      return failure('The city does not have enough food surplus.')
    }

    this.payResources(resourceCosts, city, true)
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
    if (building.deferredReason) return failure(`That building is deferred: ${building.deferredReason}`)
    if (!this.isCityAccessible(cityId)) return failure('That city is not accessible.')
    if (this.isBuildingInteractionLocked(city, buildingId)) {
      return failure('That building is locked for the current con.')
    }
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
    if (!this.isCityAccessible(cityId)) return failure('That city is not accessible.')
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
    if (technology.deferredReason) {
      return failure(`That research is deferred: ${technology.deferredReason}`)
    }
    if (this.state.empire.researchedTechnologyIds.includes(technologyId)) {
      return failure('That research is already complete.')
    }
    const usageKey = this.researchUsageKey(technology)
    if (this.state.empire.researchUsage[usageKey]) {
      return failure('That research group was already used this empire phase.')
    }
    const dependency = this.firstMissingDependency(technology.prerequisites)
    if (dependency) return failure(`Missing prerequisite: ${dependency}.`)
    if (this.state.empire.daysRemaining < technology.timeCostDays) return failure('Not enough days remain.')
    const missingResource = this.firstMissingResource(technology.resourceCosts)
    if (missingResource) return failure(`Not enough ${missingResource}.`)

    this.payResources(technology.resourceCosts)
    this.state.empire.daysRemaining -= technology.timeCostDays
    this.state.empire.researchedTechnologyIds.push(technologyId)
    this.state.empire.researchUsage[usageKey] = technologyId
    this.applyEffects(technology.effects, 0)
    this.refreshProductions()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`${technology.name} researched.`)
  }

  chooseEvent(choiceId: string): EmpiresActionResult {
    if (this.state.phase !== 'event' || !this.state.event) return failure('No event choice is pending.')
    const event = this.eventDefinitions.get(this.state.event.eventId)
    if (!event || event.deferredReason) return failure('That event is deferred.')
    const choice = event?.choices.find(item => item.id === choiceId)
    if (!choice) return failure('Unknown event choice.')
    if (choice.deferredReason) return failure(`That event choice is deferred: ${choice.deferredReason}`)
    const missingResource = this.firstMissingResource(choice.resourceCosts ?? [])
    if (missingResource) return failure(`Not enough ${missingResource}.`)
    const pendingEmpireSettlement = this.state.event.empireSettlementPending === true
    const hadStarvationMultiplier = Object.prototype.hasOwnProperty.call(
      this.state.empire.flags,
      'starvationLossMultiplierPercent',
    )
    const starvationMultiplierBefore = this.state.empire.flags.starvationLossMultiplierPercent
    this.payResources(choice.resourceCosts ?? [])
    this.applyEffects(choice.effects, 0)
    this.refreshProductions()
    if (pendingEmpireSettlement) {
      this.settleEmpireEconomy()
      if (hadStarvationMultiplier) {
        this.state.empire.flags.starvationLossMultiplierPercent = starvationMultiplierBefore
      } else {
        delete this.state.empire.flags.starvationLossMultiplierPercent
      }
      if (this.state.phase !== 'defeat') {
        this.clearCardFlagBonuses()
        this.startNextCon()
      }
      return this.commit(`Famine choice ${choiceId} resolved and the empire settled.`)
    }
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
    if (!city || !this.isCityAccessible(cityId)) return 0
    return this.armyFoodUpkeepForCity(city)
  }

  cityFoodConsumption(cityId: string): number {
    const city = this.city(cityId)
    if (!city || !this.isCityAccessible(cityId)) return 0
    return this.foodConsumptionForCity(city)
  }

  cityAvailableResource(
    cityId: string,
    resourceId: string,
    allowTempleTransfers = true,
  ): number {
    const city = this.city(cityId)
    if (!city || !this.isCityAccessible(cityId)) return 0
    let available = Math.max(0, city.resources[resourceId] ?? 0)
      + Math.max(0, this.state.empire.resources[resourceId] ?? 0)
    if (!allowTempleTransfers) return available
    const transferLossPercent = this.operationalBuildingFlagValue(city, 'templarTransferLossPercent')
    if (transferLossPercent === null) return available
    const deliveredFraction = Math.max(0, Math.min(1, (100 - transferLossPercent) / 100))
    if (deliveredFraction <= 0) return available
    const donorResources = this.state.empire.cities
      .filter(candidate => candidate.id !== city.id && this.isCityAccessible(candidate.id))
      .filter(candidate => (
        this.operationalBuildingFlagValue(candidate, 'templarTransferLossPercent') !== null
      ))
      .reduce((total, donor) => total + Math.max(0, donor.resources[resourceId] ?? 0), 0)
    return available + donorResources * deliveredFraction
  }

  cityRecruitmentRemaining(cityId: string): number | null {
    const city = this.city(cityId)
    if (!city || !this.isCityAccessible(cityId)) return 0
    if ((this.state.empire.flags.unlimitedTavernRecruitment ?? 0) > 0) return null
    const capacity = this.operationalBuildingFlagValue(city, 'equippedRecruitCapacity')
    return capacity === null ? null : Math.max(0, capacity - this.recruitedUnitCount(city))
  }

  private armyFoodUpkeepForCity(city: EmpiresCityState): number {
    return Object.entries(city.recruitedUnits).reduce((total, [unitId, count]) => {
      const unit = this.unitDefinitions.get(unitId)
      if (!unit || unit.deferredReason) return total
      return total + Math.max(0, count) * unit.foodUpkeep
    }, 0)
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
    const grossConsumption = civilianConsumption + this.armyFoodUpkeepForCity(city)
    const efficiencyPercent = this.operationalBuildingFlagValue(city, 'provisionEfficiencyPercent') ?? 0
    return grossConsumption * Math.max(0, 100 - efficiencyPercent) / 100
  }

  private canFundFoodDemand(
    projectedCity: EmpiresCityState,
    projectedProduction: number,
    projectedConsumption: number,
    extraCommitted = 0,
    immediateFoodCost = 0,
    allowTempleTransfers = false,
  ): boolean {
    const foodId = this.config.empire.foodResourceId
    const foodPayment = this.resourcePaymentPlan(
      foodId,
      Math.max(0, immediateFoodCost),
      projectedCity,
      allowTempleTransfers,
    )
    if (foodPayment.covered + Number.EPSILON < Math.max(0, immediateFoodCost)) return false
    const remainingCityFood = new Map(this.state.empire.cities.map(city => [
      city.id,
      Math.max(0, city.resources[foodId] ?? 0),
    ]))
    remainingCityFood.set(
      projectedCity.id,
      Math.max(0, (projectedCity.resources[foodId] ?? 0) - foodPayment.targetSpend),
    )
    for (const donor of foodPayment.donorSpends) {
      remainingCityFood.set(
        donor.cityId,
        Math.max(0, (remainingCityFood.get(donor.cityId) ?? 0) - donor.amount),
      )
    }
    const availableEmpireFood = Math.max(
      0,
      (this.state.empire.resources[foodId] ?? 0) - foodPayment.empireSpend,
    )

    const requiredEmpireFood = this.state.empire.cities.reduce((total, city) => {
      if (!this.isCityAccessible(city.id)) return total
      const isProjected = city.id === projectedCity.id
      const production = isProjected
        ? projectedProduction
        : this.productionForCity(city)[foodId] ?? 0
      const consumption = isProjected
        ? projectedConsumption
        : this.foodConsumptionForCity(city)
      const committed = city.foodCommitted + (isProjected ? Math.max(0, extraCommitted) : 0)
      const localFood = remainingCityFood.get(city.id) ?? 0
      return total + Math.max(0, consumption + committed - production - localFood)
    }, 0)
    return requiredEmpireFood <= availableEmpireFood
  }

  hasProductionBoost(cityId: string, buildingId: string): boolean {
    return this.state.empire.productionBoostAssignments.some(
      assignment => assignment.cityId === cityId && assignment.buildingId === buildingId,
    )
  }

  cityProduction(cityId: string): Record<string, number> {
    const city = this.city(cityId)
    if (!city || !this.isCityAccessible(cityId)) return {}
    return this.productionForCity(city)
  }

  isRegionAccessible(regionId: string): boolean {
    return this.isRegionAccessibleInState(this.state, regionId)
  }

  isCityAccessible(cityId: string): boolean {
    const city = this.city(cityId)
    return Boolean(city && this.isRegionAccessible(city.regionId))
  }

  effectiveBuildingLevel(cityId: string, buildingId: string): number {
    const city = this.city(cityId)
    const building = this.buildingDefinitions.get(buildingId)
    if (!city || !building) return 0
    const rawLevel = city.buildingLevels[buildingId] ?? 0
    return rawLevel > 0 ? rawLevel + this.buildingLevelBonus(building.slot) : 0
  }

  effectiveOperationalBuildingLevel(cityId: string, buildingId: string): number {
    const city = this.city(cityId)
    const building = this.buildingDefinitions.get(buildingId)
    if (!city || !building || building.deferredReason) return 0
    return this.effectiveLevelFromRaw(
      building,
      city.operationalBuildingLevels[buildingId] ?? 0,
    )
  }

  effectiveBuildingMaxLevel(buildingId: string): number {
    const building = this.buildingDefinitions.get(buildingId)
    if (!building) return 0
    const configuredMaximum = Math.max(0, ...building.levels.map(level => level.level))
    return configuredMaximum + this.buildingLevelBonus(building.slot)
  }

  projectedBuildingLevel(
    buildingId: string,
    effectiveLevel: number,
  ): EmpiresBuildingLevelDefinition | null {
    const building = this.buildingDefinitions.get(buildingId)
    if (!building || building.deferredReason) return null
    const projection = this.buildingLevelDefinitionAt(building, effectiveLevel)
    return projection ? cloneSerializable(projection) : null
  }

  private productionForCity(city: EmpiresCityState): Record<string, number> {
    const production = { ...city.baseProduction }
    for (const [buildingId, level] of Object.entries(city.operationalBuildingLevels)) {
      if (level <= 0) continue
      const definition = this.buildingDefinitions.get(buildingId)
      if (!definition || definition.deferredReason) continue
      const currentLevel = definition
        ? this.buildingLevelDefinitionAt(definition, this.effectiveLevelFromRaw(definition, level))
        : null
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
      const famineMultiplier = resourceId === foodId ? this.famineFoodMultiplier(city) : 1
      production[resourceId] = amount
        * (this.state.empire.productionMultipliers[resourceId] ?? 1)
        * famineMultiplier
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
        godInterventions: 0,
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
          resources: {},
          buildingInteractionLocks: {},
          lockedFacilities: {},
          foodCommitted: 0,
          lastProduction: {},
          lastStarvationLoss: 0,
          loyalty: 0,
        })),
        researchedTechnologyIds: [],
        claimedGiftIds: [],
        activeGiftIds: [],
        productionMultipliers: {},
        passiveFoodBonuses: {},
        cardFlagBonuses: {},
        productionBoostAssignments: [],
        destroyedRegionIds: [],
        buildingLevelBonuses: {},
        researchUsage: {},
        giftResolutionTargets: {},
      },
      pendingResolution: null,
      minigame: null,
      minigameResultLog: [],
      army: {
        equipmentStock: {},
        pendingLoyaltyDeltas: [],
        morale: 0,
        veterans: {},
      },
      external: {
        allianceThreat: 0,
        pendingOffers: [],
      },
      epidemics: [],
      quests: {},
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
    state.minigame ??= null
    state.minigameResultLog ??= []
    state.army ??= {
      equipmentStock: {},
      pendingLoyaltyDeltas: [],
      morale: 0,
      veterans: {},
    }
    state.army.equipmentStock ??= {}
    state.army.pendingLoyaltyDeltas ??= []
    state.army.morale ??= 0
    state.army.veterans ??= {}
    state.external ??= { allianceThreat: 0, pendingOffers: [] }
    state.external.allianceThreat ??= 0
    state.external.pendingOffers ??= []
    state.epidemics ??= []
    state.quests ??= {}
    state.durak.godInterventions ??= 0
    const missingDestroyedRegionState = state.empire.destroyedRegionIds === undefined
    const missingBuildingBonusState = state.empire.buildingLevelBonuses === undefined
    const citiesMissingInteractionLocks = new Set(
      state.empire.cities
        .filter(city => city.buildingInteractionLocks === undefined)
        .map(city => city.id),
    )
    for (const city of state.empire.cities) {
      city.loyalty ??= 0
      city.operationalBuildingLevels ??= { ...city.buildingLevels }
      city.buildingSlotAssignments ??= Object.fromEntries(
        (this.cityDefinition(city.id)?.slots ?? []).flatMap(
          slot => slot.buildingId ? [[slot.id, slot.buildingId]] : [],
        ),
      )
      city.recruitedUnits ??= {}
      city.resources ??= {}
      city.buildingInteractionLocks ??= {}
    }
    state.empire.cardFlagBonuses ??= state.phase === 'empire' || state.phase === 'event'
      ? this.heldCardFlagBonuses(state)
      : {}
    state.empire.productionBoostAssignments ??= []
    state.empire.destroyedRegionIds = this.migrateDestroyedRegions(
      state,
      missingDestroyedRegionState,
    )
    state.empire.buildingLevelBonuses = this.migrateBuildingLevelBonuses(
      state,
      missingBuildingBonusState,
    )
    state.empire.researchUsage ??= {}
    const knownCityIds = new Set(state.empire.cities.map(city => city.id))
    state.empire.giftResolutionTargets = Object.fromEntries(
      Object.entries(state.empire.giftResolutionTargets ?? {})
        .filter(([, cityId]) => {
          const city = state.empire.cities.find(item => item.id === cityId)
          return knownCityIds.has(cityId)
            && Boolean(city && this.isRegionAccessibleInState(state, city.regionId))
        }),
    )
    state.pendingResolution = this.normalizePendingResolution(state)
    this.normalizeDivineGiftChoices(state)
    if (state.event) {
      state.event.empireSettlementPending ??= false
      if (
        state.event.empireSettlementPending
        && (state.phase !== 'event' || state.event.eventId !== FAMINE_RATIONING_EVENT_ID)
      ) {
        throw new Error('Only a pending famine event can precede empire settlement')
      }
    }
    if (
      state.phase === 'event'
      && (
        !state.event
        || !this.eventCanResolve(this.eventDefinitions.get(state.event.eventId))
      )
    ) {
      this.advanceSnapshotToNextCon(state)
    }
    if (
      citiesMissingInteractionLocks.size > 0
      && (state.phase === 'empire' || state.phase === 'event')
      && (state.empire.flags.militaryArson ?? 0) > 0
    ) {
      this.applyMilitaryArsonToState(state, citiesMissingInteractionLocks)
    }
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
      .filter(gift => !gift.deferredReason)
      .filter(gift => this.giftIsUnlocked(gift))
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
    this.state.pendingResolution = null
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
    this.state.empire.researchUsage = {}
    for (const city of this.state.empire.cities) {
      city.buildingInteractionLocks = Object.fromEntries(
        Object.entries(city.buildingInteractionLocks).filter(([, con]) => con === this.state.con),
      )
      city.lockedFacilities = {}
      city.foodCommitted = 0
      city.lastStarvationLoss = 0
    }

    const phaseEffectKinds = new Set<EffectKind>(['resource', 'time', 'population', 'flag'])
    const timeEffectKind = new Set<EffectKind>(['time'])
    for (const giftId of this.state.empire.activeGiftIds) {
      const gift = this.giftDefinitions.get(giftId)
      if (!gift || gift.deferredReason || !this.giftIsUnlocked(gift)) continue
      const targetCityId = this.state.empire.giftResolutionTargets[giftId]
      if (
        (gift.resolution?.kind === 'cityResources' || gift.resolution?.kind === 'meteorCity')
        && (!targetCityId || !this.isCityAccessible(targetCityId))
      ) continue
      this.applyRecurringGiftResolution(gift)
      this.applyGiftEffects(gift, targetCityId, phaseEffectKinds)
    }
    for (const technologyId of this.state.empire.researchedTechnologyIds) {
      const technology = this.technologyDefinitions.get(technologyId)
      if (!technology || technology.deferredReason) continue
      this.applyEffects(technology.effects, 0, timeEffectKind)
    }
    for (const cardId of this.state.durak.playerHand) {
      const instance = this.state.cards[cardId]
      const definition = this.getDefinition(instance)
      const face = instance.inverted ? definition.inverted : definition.normal
      if (face.deferredReason) continue
      this.applyEffects(face.effects, instance.level, phaseEffectKinds)
      this.recordCardFlagBonuses(face.effects, instance.level)
    }
    if ((this.state.empire.flags.militaryArson ?? 0) > 0) this.applyMilitaryArson()
    for (const city of this.state.empire.cities) this.updateOperationalBuildings(city)
    for (const city of this.state.empire.cities) {
      for (const [buildingId, level] of Object.entries(city.operationalBuildingLevels)) {
        const building = this.buildingDefinitions.get(buildingId)
        if (!building || building.deferredReason) continue
        const levelDefinition = building
          ? this.buildingLevelDefinitionAt(building, this.effectiveLevelFromRaw(building, level))
          : null
        this.applyEffects(levelDefinition?.effects ?? [], 0, timeEffectKind)
      }
    }
    this.state.empire.daysRemaining = Math.max(0, this.state.empire.daysRemaining)
    this.refreshProductions()
  }

  private finishEmpireInternal(): void {
    if (this.state.phase !== 'empire') return
    this.refreshProductions()
    let eventRollResolvedBeforeSettlement = false
    let selectedEvent: EmpiresEventDefinition | null = null
    const famineEvent = this.eventDefinitions.get(FAMINE_RATIONING_EVENT_ID)
    if (
      this.uncoveredFoodDeficit() > 0
      && famineEvent
      && this.eventIsEligible(famineEvent)
    ) {
      eventRollResolvedBeforeSettlement = true
      selectedEvent = this.selectEligibleEvent(this.config.empire.events)
      if (selectedEvent?.id === FAMINE_RATIONING_EVENT_ID) {
        this.state.phase = 'event'
        this.state.event = {
          eventId: selectedEvent.id,
          empireSettlementPending: true,
        }
        return
      }
    }

    this.settleEmpireEconomy()
    if (this.state.phase === 'defeat') return
    if (!eventRollResolvedBeforeSettlement) {
      selectedEvent = this.selectEligibleEvent(
        this.config.empire.events.filter(event => event.id !== FAMINE_RATIONING_EVENT_ID),
      )
    }
    if (selectedEvent) {
      this.state.phase = 'event'
      this.state.event = {
        eventId: selectedEvent.id,
        empireSettlementPending: false,
      }
      return
    }
    this.clearCardFlagBonuses()
    this.startNextCon()
  }

  private settleEmpireEconomy(): void {
    this.refreshProductions()
    const foodId = this.config.empire.foodResourceId
    for (const city of this.state.empire.cities) {
      if (!this.isCityAccessible(city.id)) {
        city.lastProduction = {}
        city.lastStarvationLoss = 0
        continue
      }
      for (const [resourceId, amount] of Object.entries(city.lastProduction)) {
        if (resourceId === foodId) continue
        this.state.empire.resources[resourceId] = (this.state.empire.resources[resourceId] ?? 0) + amount
      }
      const tradeLevyGold = this.tradeLevyGoldForCity(city)
      if (tradeLevyGold > 0) {
        this.state.empire.resources.gold = (this.state.empire.resources.gold ?? 0) + tradeLevyGold
      }
      let deficit = Math.max(
        0,
        this.cityFoodConsumption(city.id) + city.foodCommitted - (city.lastProduction[foodId] ?? 0),
      )
      const cityFood = Math.max(0, city.resources[foodId] ?? 0)
      const cityFoodUsed = Math.min(cityFood, deficit)
      city.resources[foodId] = cityFood - cityFoodUsed
      deficit -= cityFoodUsed
      const empireFood = Math.max(0, this.state.empire.resources[foodId] ?? 0)
      const empireFoodUsed = Math.min(empireFood, deficit)
      this.state.empire.resources[foodId] = empireFood - empireFoodUsed
      deficit -= empireFoodUsed
      const configuredLossPercent = this.state.empire.flags.starvationDeficitLossPercent
      const baseLossMultiplier = Number.isFinite(configuredLossPercent)
        ? Math.max(0, configuredLossPercent) / 100
        : 0.5
      const loss = deficit * baseLossMultiplier * this.empirePercentMultiplier('starvationLossMultiplierPercent')
      city.lastStarvationLoss = loss
      if (loss > 0) this.setCityPopulation(city, Math.max(0, city.population - loss))
    }
    this.applyTreasuryIncome()
    this.refreshProductions()
    if (this.totalPopulation() <= this.config.empire.defeatPopulationAtOrBelow) {
      this.setOutcome('defeat', 'The empire starved.')
    }
  }

  private uncoveredFoodDeficit(): number {
    const foodId = this.config.empire.foodResourceId
    let empireFood = Math.max(0, this.state.empire.resources[foodId] ?? 0)
    let uncovered = 0
    for (const city of this.state.empire.cities) {
      if (!this.isCityAccessible(city.id)) continue
      let deficit = Math.max(
        0,
        this.cityFoodConsumption(city.id) + city.foodCommitted - (city.lastProduction[foodId] ?? 0),
      )
      deficit -= Math.min(Math.max(0, city.resources[foodId] ?? 0), deficit)
      const empireFoodUsed = Math.min(empireFood, deficit)
      empireFood -= empireFoodUsed
      deficit -= empireFoodUsed
      uncovered += deficit
    }
    return uncovered
  }

  private selectEligibleEvent(
    candidates: readonly EmpiresEventDefinition[],
  ): EmpiresEventDefinition | null {
    const eligible = candidates.filter(event => this.eventIsEligible(event))
    if (eligible.length === 0 || nextEmpiresRandom(this.state.rng) >= this.config.empire.eventChance) {
      return null
    }
    return pickEmpiresWeighted(eligible, this.state.rng)
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
    building: EmpiresBuildingDefinition,
    level: EmpiresBuildingLevelDefinition,
  ): EmpiresActionResult {
    const dependencies = this.buildingDependencies(building, level)
    const missingDependency = this.firstMissingDependency(dependencies, city)
    if (missingDependency) return failure(`Missing prerequisite: ${missingDependency}.`)
    if (this.state.empire.daysRemaining < level.timeCostDays) return failure('Not enough days remain.')
    const resourceCosts = this.buildingResourceCosts(building, level)
    const missingResource = this.firstMissingResource(resourceCosts, city, true)
    if (missingResource) return failure(`Not enough ${missingResource}.`)
    const foodProduction = city.lastProduction[this.config.empire.foodResourceId] ?? 0
    const immediateFoodCost = resourceCosts
      .filter(cost => cost.resourceId === this.config.empire.foodResourceId)
      .reduce((total, cost) => total + cost.amount, 0)
    if (!this.canFundFoodDemand(
      city,
      foodProduction,
      this.cityFoodConsumption(city.id),
      level.foodCost,
      immediateFoodCost,
      true,
    )) {
      return failure('The city does not have enough food surplus.')
    }
    for (const lock of level.facilityLocks) {
      if (city.lockedFacilities[lock]) return failure(`The city's ${lock} is already committed this phase.`)
      const providerId = this.config.empire.lockProviderBuildingIds[lock]
      if ((city.operationalBuildingLevels[providerId] ?? 0) < 1) {
        return failure(`The city has no working ${lock}.`)
      }
    }
    return success('Empire action is available.')
  }

  private buildingDependencies(
    building: EmpiresBuildingDefinition,
    level: EmpiresBuildingLevelDefinition,
  ): EmpiresDependency[] {
    if (!this.isStableBuilding(building) || (this.state.empire.flags.stableWithoutLivestock ?? 0) <= 0) {
      return level.dependencies
    }
    return level.dependencies.filter(
      dependency => dependency.kind !== 'flag' || dependency.flagId !== 'livestockAvailable',
    )
  }

  private buildingResourceCosts(
    building: EmpiresBuildingDefinition,
    level: EmpiresBuildingLevelDefinition,
  ): EmpiresResourceAmount[] {
    return level.resourceCosts.filter((cost) => {
      if (building.slot === 'smithy'
        && cost.resourceId === 'iron'
        && (this.state.empire.flags.smithyWithoutIron ?? 0) > 0) return false
      if (this.isStableBuilding(building)
        && cost.resourceId === 'horses'
        && (this.state.empire.flags.stableWithoutLivestock ?? 0) > 0) return false
      return true
    })
  }

  private isStableBuilding(building: EmpiresBuildingDefinition): boolean {
    const identity = `${building.id} ${building.name}`.toLocaleLowerCase('ru-RU')
    return identity.includes('stable') || identity.includes('конюш')
  }

  private completeBuildingLevel(
    city: EmpiresCityState,
    building: EmpiresBuildingDefinition,
    level: EmpiresBuildingLevelDefinition,
    slotId?: string | null,
  ): void {
    this.payResources(this.buildingResourceCosts(building, level), city, true)
    this.state.empire.daysRemaining -= level.timeCostDays
    city.foodCommitted += level.foodCost
    for (const lock of level.facilityLocks) city.lockedFacilities[lock] = `${building.id}:${level.level}`
    if (slotId) city.buildingSlotAssignments[slotId] = building.id
    city.buildingLevels[building.id] = level.level
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
        const technology = this.technologyDefinitions.get(dependency.technologyId)
        if (
          !technology
          || technology.deferredReason
          || !this.state.empire.researchedTechnologyIds.includes(dependency.technologyId)
        ) {
          return dependency.technologyId
        }
      } else if (dependency.kind === 'flag') {
        if ((this.state.empire.flags[dependency.flagId] ?? 0) < dependency.minimum) return dependency.flagId
      } else {
        const cities = (dependency.scope !== 'anyCity' && city ? [city] : this.state.empire.cities)
          .filter(item => this.isCityAccessible(item.id))
        const useOperational = operationalSameCityBuildings && dependency.scope !== 'anyCity' && Boolean(city)
        if (!cities.some((item) => {
          if (this.isBuildingInteractionLocked(item, dependency.buildingId)) return false
          const levels = useOperational ? item.operationalBuildingLevels : item.buildingLevels
          const building = this.buildingDefinitions.get(dependency.buildingId)
          if (!building || building.deferredReason) return false
          return this.effectiveLevelFromRaw(building, levels[dependency.buildingId] ?? 0) >= dependency.level
        })) {
          return `${dependency.buildingId} ${dependency.level}`
        }
      }
    }
    return null
  }

  private firstMissingResource(
    costs: readonly EmpiresResourceAmount[],
    city?: EmpiresCityState,
    allowTempleTransfers = false,
  ): string | null {
    return costs.find(cost => (
      this.resourcePaymentPlan(cost.resourceId, cost.amount, city, allowTempleTransfers).covered
      + Number.EPSILON
      < cost.amount
    ))?.resourceId ?? null
  }

  private payResources(
    costs: readonly EmpiresResourceAmount[],
    city?: EmpiresCityState,
    allowTempleTransfers = false,
  ): void {
    for (const cost of costs) {
      const plan = this.resourcePaymentPlan(cost.resourceId, cost.amount, city, allowTempleTransfers)
      if (city && plan.targetSpend > 0) {
        city.resources[cost.resourceId] = Math.max(
          0,
          (city.resources[cost.resourceId] ?? 0) - plan.targetSpend,
        )
      }
      if (plan.empireSpend > 0) {
        this.state.empire.resources[cost.resourceId] = Math.max(
          0,
          (this.state.empire.resources[cost.resourceId] ?? 0) - plan.empireSpend,
        )
      }
      for (const donorSpend of plan.donorSpends) {
        const donor = this.city(donorSpend.cityId)
        if (!donor) continue
        donor.resources[cost.resourceId] = Math.max(
          0,
          (donor.resources[cost.resourceId] ?? 0) - donorSpend.amount,
        )
      }
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
          ? this.state.empire.cities.filter(
              city => city.id === effect.cityId && this.isCityAccessible(city.id),
            )
          : this.state.empire.cities.filter(city => this.isCityAccessible(city.id))
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
      if (face.deferredReason) continue
      for (const effect of face.effects) {
        if (effect.kind !== 'flag') continue
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * instance.level
        bonuses[effect.flagId] = (bonuses[effect.flagId] ?? 0) + amount
      }
    }
    return bonuses
  }

  private clearCardFlagBonuses(): void {
    this.clearCardFlagBonusesFromState(this.state)
  }

  private clearCardFlagBonusesFromState(state: EmpiresCampaignState): void {
    for (const [flagId, amount] of Object.entries(state.empire.cardFlagBonuses ?? {})) {
      const remaining = (state.empire.flags[flagId] ?? 0) - amount
      if (Math.abs(remaining) < Number.EPSILON) delete state.empire.flags[flagId]
      else state.empire.flags[flagId] = remaining
    }
    state.empire.cardFlagBonuses = {}
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

  private recruitedUnitCount(city: EmpiresCityState): number {
    return Object.entries(city.recruitedUnits).reduce((total, [unitId, count]) => (
      this.unitDefinitions.get(unitId)?.deferredReason
        ? total
        : total + Math.max(0, count)
    ), 0)
  }

  private operationalBuildingFlagEntries(
    city: EmpiresCityState,
    flagId: string,
  ): Array<{ buildingId: string, level: number, value: number }> {
    return Object.entries(city.operationalBuildingLevels).flatMap(([buildingId, rawLevel]) => {
      if (rawLevel <= 0 || this.isBuildingInteractionLocked(city, buildingId)) return []
      const building = this.buildingDefinitions.get(buildingId)
      if (!building || building.deferredReason) return []
      const level = this.effectiveLevelFromRaw(building, rawLevel)
      const levelDefinition = this.buildingLevelDefinitionAt(building, level)
      const values = (levelDefinition?.effects ?? []).flatMap(effect => (
        effect.kind === 'flag' && effect.flagId === flagId ? [effect.amount] : []
      ))
      if (values.length === 0) return []
      return [{ buildingId, level, value: Math.max(...values) }]
    })
  }

  private operationalBuildingFlagValue(city: EmpiresCityState, flagId: string): number | null {
    const values = this.operationalBuildingFlagEntries(city, flagId).map(entry => entry.value)
    return values.length > 0 ? Math.max(...values) : null
  }

  private inheritedOperationalBuildingFlagValue(city: EmpiresCityState, flagId: string): number | null {
    const values = Object.entries(city.operationalBuildingLevels).flatMap(([buildingId, rawLevel]) => {
      if (rawLevel <= 0 || this.isBuildingInteractionLocked(city, buildingId)) return []
      const building = this.buildingDefinitions.get(buildingId)
      if (!building || building.deferredReason) return []
      const effectiveLevel = this.effectiveLevelFromRaw(building, rawLevel)
      return building.levels
        .filter(level => level.level <= effectiveLevel)
        .flatMap(level => (level.effects ?? []).flatMap(effect => (
          effect.kind === 'flag' && effect.flagId === flagId ? [effect.amount] : []
        )))
    })
    return values.length > 0 ? Math.max(...values) : null
  }

  private resourcePaymentPlan(
    resourceId: string,
    amount: number,
    city?: EmpiresCityState,
    allowTempleTransfers = false,
  ): EmpiresResourcePaymentPlan {
    const requested = Math.max(0, amount)
    let remaining = requested
    const targetSpend = Math.min(Math.max(0, city?.resources[resourceId] ?? 0), remaining)
    remaining -= targetSpend
    const empireSpend = Math.min(
      Math.max(0, this.state.empire.resources[resourceId] ?? 0),
      remaining,
    )
    remaining -= empireSpend
    const donorSpends: Array<{ cityId: string, amount: number }> = []

    const transferLossPercent = city && allowTempleTransfers
      ? this.operationalBuildingFlagValue(city, 'templarTransferLossPercent')
      : null
    const deliveredFraction = transferLossPercent === null
      ? 0
      : Math.max(0, Math.min(1, (100 - transferLossPercent) / 100))
    if (city && deliveredFraction > 0 && remaining > Number.EPSILON) {
      const donors = this.state.empire.cities
        .filter(candidate => candidate.id !== city.id && this.isCityAccessible(candidate.id))
        .filter(candidate => (
          this.operationalBuildingFlagValue(candidate, 'templarTransferLossPercent') !== null
        ))
        .sort((left, right) => left.id.localeCompare(right.id))
      for (const donor of donors) {
        const donorAvailable = Math.max(0, donor.resources[resourceId] ?? 0)
        const donorSpend = Math.min(donorAvailable, remaining / deliveredFraction)
        if (donorSpend <= 0) continue
        donorSpends.push({ cityId: donor.id, amount: donorSpend })
        remaining = Math.max(0, remaining - donorSpend * deliveredFraction)
        if (remaining <= Number.EPSILON) break
      }
    }

    return {
      covered: requested - remaining,
      targetSpend,
      empireSpend,
      donorSpends,
    }
  }

  private famineFoodMultiplier(city: EmpiresCityState): number {
    let multiplier = 1
    for (const cardId of this.state.durak.playerHand) {
      const instance = this.state.cards[cardId]
      const definition = instance ? this.definitions.get(instance.definitionId) : null
      if (!instance || !definition) continue
      const face = instance.inverted ? definition.inverted : definition.normal
      if (face.deferredReason) continue
      const isFamineYear = face.effects.some(effect => (
        effect.kind === 'flag'
        && effect.flagId === 'famineYear'
        && effect.amount + (effect.amountPerLevel ?? 0) * instance.level > 0
      ))
      if (!isFamineYear) continue
      for (const effect of face.effects) {
        if (effect.kind !== 'resourceMultiplier'
          || effect.resourceId !== this.config.empire.foodResourceId) continue
        multiplier *= effect.multiplier + (effect.multiplierPerLevel ?? 0) * instance.level
      }
    }
    if (multiplier >= 1) return multiplier
    if ((this.state.empire.flags.famineYearCounter ?? 0) > 0) return 1
    if ((this.inheritedOperationalBuildingFlagValue(city, 'famineProtectionTurns') ?? 0) > 0) return 1
    return Math.max(0, multiplier)
  }

  private tradeLevyGoldForCity(city: EmpiresCityState): number {
    const levyBuildings = Object.entries(city.operationalBuildingLevels).flatMap(([buildingId, rawLevel]) => {
      if (rawLevel <= 0 || this.isBuildingInteractionLocked(city, buildingId)) return []
      const building = this.buildingDefinitions.get(buildingId)
      if (!building || building.deferredReason) return []
      const level = this.effectiveLevelFromRaw(building, rawLevel)
      const levelDefinition = this.buildingLevelDefinitionAt(building, level)
      const flags = (levelDefinition?.effects ?? []).flatMap(effect => (
        effect.kind === 'flag' ? [[effect.flagId, effect.amount] as const] : []
      ))
      const flagValues = Object.fromEntries(flags) as Record<string, number>
      if (!Number.isFinite(flagValues.idleBuildingGoldBase)
        || !Number.isFinite(flagValues.surplusFoodPerGold)) return []
      return [{
        buildingId,
        idleBuildingGoldBase: Math.max(0, flagValues.idleBuildingGoldBase),
        surplusFoodPerGold: Math.max(0, flagValues.surplusFoodPerGold),
      }]
    })
    if (levyBuildings.length === 0) return 0

    const levyBuildingIds = new Set(levyBuildings.map(entry => entry.buildingId))
    const providers = this.config.empire.lockProviderBuildingIds as Record<string, string>
    const busyProviderIds = new Set(
      Object.keys(city.lockedFacilities).flatMap(lock => providers[lock] ? [providers[lock]] : []),
    )
    const idleBuildingGoldBase = Math.max(...levyBuildings.map(entry => entry.idleBuildingGoldBase))
    const idleBuildingGold = Object.entries(city.operationalBuildingLevels).reduce(
      (total, [buildingId, rawLevel]) => {
        if (rawLevel <= 0
          || levyBuildingIds.has(buildingId)
          || busyProviderIds.has(buildingId)
          || this.isBuildingInteractionLocked(city, buildingId)) return total
        const building = this.buildingDefinitions.get(buildingId)
        if (building?.deferredReason) return total
        const effectiveLevel = building ? this.effectiveLevelFromRaw(building, rawLevel) : rawLevel
        const levelDefinition = building
          ? this.buildingLevelDefinitionAt(building, effectiveLevel)
          : null
        if ((levelDefinition?.production?.length ?? 0) > 0) return total
        return total + idleBuildingGoldBase + Math.max(0, effectiveLevel - 1)
      },
      0,
    )
    const surplusFoodPerGold = Math.max(...levyBuildings.map(entry => entry.surplusFoodPerGold))
    const foodId = this.config.empire.foodResourceId
    const surplusFood = Math.max(
      0,
      (city.lastProduction[foodId] ?? 0) - this.cityFoodConsumption(city.id) - city.foodCommitted,
    )
    const surplusFoodGold = surplusFoodPerGold > 0
      ? Math.floor(surplusFood / surplusFoodPerGold)
      : 0
    return idleBuildingGold + surplusFoodGold
  }

  private applyTreasuryIncome(): void {
    const goldPerSavedMillion = this.state.empire.flags.treasuryGoldPerSavedMillion
    if (!Number.isFinite(goldPerSavedMillion) || goldPerSavedMillion <= 0) return
    const savedGold = Math.max(0, this.state.empire.resources.gold ?? 0)
    const bonus = Math.floor(savedGold / 1_000_000) * goldPerSavedMillion
    if (bonus > 0) this.state.empire.resources.gold = savedGold + bonus
  }

  private eventIsEligible(event: EmpiresEventDefinition): boolean {
    if (event.deferredReason || !event.choices.some(choice => !choice.deferredReason)) return false
    if ((event.minimumCon ?? Number.NEGATIVE_INFINITY) > this.state.con) return false
    if ((event.maximumCon ?? Number.POSITIVE_INFINITY) < this.state.con) return false
    if (event.id === 'event-horse-theft' && (this.state.empire.flags.horseTheftDisabled ?? 0) > 0) {
      return false
    }
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
        const building = this.buildingDefinitions.get(assignment.buildingId)
        if (seen.has(key)
          || !this.isCityAccessible(assignment.cityId)
          || !building
          || building.deferredReason) {
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
      const gift = this.giftDefinitions.get(giftId)
      if (!gift || gift.deferredReason || !this.giftIsUnlocked(gift)) continue
      this.applyEffects(gift.effects, 0, productionKinds)
    }
    for (const technologyId of this.state.empire.researchedTechnologyIds) {
      const technology = this.technologyDefinitions.get(technologyId)
      if (!technology || technology.deferredReason) continue
      this.applyEffects(technology.effects, 0, productionKinds)
    }
    for (const city of this.state.empire.cities) {
      for (const [buildingId, level] of Object.entries(city.operationalBuildingLevels)) {
        const building = this.buildingDefinitions.get(buildingId)
        if (!building || building.deferredReason) continue
        const levelDefinition = building
          ? this.buildingLevelDefinitionAt(building, this.effectiveLevelFromRaw(building, level))
          : null
        this.applyEffects(levelDefinition?.effects ?? [], 0, productionKinds)
      }
    }
    for (const cardId of this.state.durak.playerHand) {
      const instance = this.state.cards[cardId]
      const definition = this.getDefinition(instance)
      const face = instance.inverted ? definition.inverted : definition.normal
      if (face.deferredReason) continue
      const famineYear = face.effects.some(effect => (
        effect.kind === 'flag'
        && effect.flagId === 'famineYear'
        && effect.amount + (effect.amountPerLevel ?? 0) * instance.level > 0
      ))
      const effects = famineYear
        ? face.effects.filter(effect => (
            effect.kind !== 'resourceMultiplier'
            || effect.resourceId !== this.config.empire.foodResourceId
          ))
        : face.effects
      this.applyEffects(effects, instance.level, productionKinds)
    }
  }

  private claimGift(gift: EmpiresGiftDefinition): void {
    this.state.empire.claimedGiftIds.push(gift.id)
    if (gift.application === 'eachEmpire' && !this.state.empire.activeGiftIds.includes(gift.id)) {
      this.state.empire.activeGiftIds.push(gift.id)
    }
  }

  private giftIsUnlocked(
    gift: EmpiresGiftDefinition,
    state: EmpiresCampaignState = this.state,
  ): boolean {
    return gift.kind !== 'relic' || (state.empire.flags.relicsUnlocked ?? 0) > 0
  }

  private applyOneShotGiftEffects(gift: EmpiresGiftDefinition, targetCityId?: string): void {
    const immediateKinds = new Set<EffectKind>(['resource', 'time', 'population', 'flag'])
    this.applyGiftEffects(gift, targetCityId, immediateKinds)
  }

  private applyGiftEffects(
    gift: EmpiresGiftDefinition,
    targetCityId?: string,
    allowedKinds?: ReadonlySet<EffectKind>,
  ): void {
    const targetCity = targetCityId ? this.city(targetCityId) : null
    for (const effect of gift.effects) {
      if (allowedKinds && !allowedKinds.has(effect.kind)) continue
      if (targetCity && effect.kind === 'resource') {
        targetCity.resources[effect.resourceId] = (targetCity.resources[effect.resourceId] ?? 0) + effect.amount
        continue
      }
      this.applyEffects([effect], 0)
    }
  }

  private applyFixedGiftResolution(gift: EmpiresGiftDefinition): void {
    const resolution = gift.resolution
    if (!resolution) return
    if (resolution.kind === 'destroyRegion') {
      if (!this.state.empire.destroyedRegionIds.includes(resolution.regionId)) {
        this.state.empire.destroyedRegionIds.push(resolution.regionId)
      }
      this.state.empire.productionBoostAssignments = this.state.empire.productionBoostAssignments.filter(
        assignment => this.isCityAccessible(assignment.cityId),
      )
      return
    }
    if (resolution.kind === 'buildingLevelBonus') {
      const amount = Math.floor(resolution.amount)
      for (const slot of resolution.slots) {
        const current = this.state.empire.buildingLevelBonuses[slot] ?? 0
        this.state.empire.buildingLevelBonuses[slot] = Math.max(0, current + amount)
      }
    }
  }

  private applyTargetedGiftResolution(
    gift: EmpiresGiftDefinition,
    targetCityId: string,
    pending: EmpiresPendingGiftResolution,
  ): void {
    if (pending.kind === 'meteorCity') this.damageCityWithMeteor(targetCityId, pending.damageLevels)
    this.applyOneShotGiftEffects(gift, targetCityId)
  }

  private applyRecurringGiftResolution(gift: EmpiresGiftDefinition): void {
    const resolution = gift.resolution
    if (!resolution) return
    if (resolution.kind === 'cityResources') return
    if (resolution.kind === 'meteorCity') {
      const targetCityId = this.state.empire.giftResolutionTargets[gift.id]
      if (targetCityId && this.isCityAccessible(targetCityId)) {
        this.damageCityWithMeteor(targetCityId, Math.max(0, Math.floor(resolution.damageLevels)))
      }
      return
    }
    this.applyFixedGiftResolution(gift)
  }

  private migrateDestroyedRegions(
    state: EmpiresCampaignState,
    includeLegacySignals: boolean,
  ): string[] {
    const knownRegionIds = new Set(this.config.empire.map.regions.map(region => region.id))
    const destroyed = new Set(
      (state.empire.destroyedRegionIds ?? []).filter(regionId => knownRegionIds.has(regionId)),
    )
    if (includeLegacySignals) {
      const legacyFlags: Record<string, string> = {
        destroyWest: 'west',
        destroyNorth: 'north',
        destroySouth: 'south',
        destroyEast: 'east',
      }
      for (const [flagId, regionId] of Object.entries(legacyFlags)) {
        if ((state.empire.flags[flagId] ?? 0) > 0 && knownRegionIds.has(regionId)) destroyed.add(regionId)
      }
      for (const giftId of state.empire.claimedGiftIds) {
        const resolution = this.giftDefinitions.get(giftId)?.resolution
        if (resolution?.kind === 'destroyRegion' && knownRegionIds.has(resolution.regionId)) {
          destroyed.add(resolution.regionId)
        }
      }
    }
    return [...destroyed]
  }

  private migrateBuildingLevelBonuses(
    state: EmpiresCampaignState,
    includeLegacySignals: boolean,
  ): Partial<Record<EmpiresBuildingSlotKind, number>> {
    const migrated: Partial<Record<EmpiresBuildingSlotKind, number>> = {}
    for (const slot of BUILDING_SLOT_KINDS) {
      const amount = state.empire.buildingLevelBonuses?.[slot]
      if (Number.isFinite(amount) && amount > 0) migrated[slot] = Math.floor(amount)
    }
    if (!includeLegacySignals) return migrated

    const claimedBonuses: Partial<Record<EmpiresBuildingSlotKind, number>> = {}
    for (const giftId of state.empire.claimedGiftIds) {
      const resolution = this.giftDefinitions.get(giftId)?.resolution
      if (resolution?.kind !== 'buildingLevelBonus') continue
      for (const slot of resolution.slots) {
        claimedBonuses[slot] = (claimedBonuses[slot] ?? 0) + Math.max(0, Math.floor(resolution.amount))
      }
    }
    const legacyFlags: Partial<Record<EmpiresBuildingSlotKind, number>> = {
      farm: state.empire.flags.farmLevelBonus,
      lumber: state.empire.flags.lumberLevelBonus,
    }
    for (const slot of BUILDING_SLOT_KINDS) {
      const legacyAmount = Number.isFinite(legacyFlags[slot])
        ? Math.max(0, Math.floor(legacyFlags[slot] ?? 0))
        : 0
      const claimedAmount = claimedBonuses[slot] ?? 0
      const amount = Math.max(legacyAmount, claimedAmount)
      if (amount > 0) migrated[slot] = amount
    }
    return migrated
  }

  private normalizePendingResolution(
    state: EmpiresCampaignState,
  ): EmpiresPendingGiftResolution | null {
    const pending = state.pendingResolution
    if (!pending) return null
    if (state.phase !== 'divineGift') {
      throw new Error('Pending divine-gift resolution is only valid in the divineGift phase')
    }
    const gift = this.giftDefinitions.get(pending.giftId)
    if (!gift || gift.deferredReason || !this.giftIsUnlocked(gift, state)) {
      const claimedIndex = state.empire.claimedGiftIds.lastIndexOf(pending.giftId)
      if (claimedIndex >= 0) state.empire.claimedGiftIds.splice(claimedIndex, 1)
      if (!state.empire.claimedGiftIds.includes(pending.giftId)) {
        state.empire.activeGiftIds = state.empire.activeGiftIds.filter(id => id !== pending.giftId)
      }
      return null
    }
    if (!state.giftChoiceIds.includes(gift.id) || !state.empire.claimedGiftIds.includes(gift.id)) {
      throw new Error('Pending resolution gift was not accepted from the current choices')
    }
    const eligibleTargetIds = this.accessibleCityIdsFromState(state)
    if (eligibleTargetIds.length === 0) throw new Error('Pending resolution has no accessible city targets')
    if (pending.kind === 'cityResources' && gift.resolution?.kind === 'cityResources') {
      return { kind: 'cityResources', giftId: gift.id, eligibleTargetIds }
    }
    if (pending.kind === 'meteorCity' && gift.resolution?.kind === 'meteorCity') {
      const damageLevels = Math.floor(gift.resolution.damageLevels)
      if (damageLevels <= 0) throw new Error('Pending meteor resolution has invalid damage')
      return { kind: 'meteorCity', giftId: gift.id, damageLevels, eligibleTargetIds }
    }
    throw new Error('Pending resolution does not match its gift definition')
  }

  private normalizeDivineGiftChoices(state: EmpiresCampaignState): void {
    if (state.phase !== 'divineGift' || state.pendingResolution) return
    const score = state.performanceScore
    const seen = new Set<string>()
    const existing = state.giftChoiceIds.filter((giftId) => {
      if (seen.has(giftId)) return false
      const gift = this.giftDefinitions.get(giftId)
      if (
        !gift
        || gift.deferredReason
        || !this.giftIsUnlocked(gift, state)
        || (gift.minimumPerformance ?? Number.NEGATIVE_INFINITY) > score
        || (gift.maximumPerformance ?? Number.POSITIVE_INFINITY) < score
      ) return false
      seen.add(giftId)
      return true
    }).slice(0, this.config.gifts.choiceCount)
    const replacements = pickEmpiresWeightedWithoutReplacement(
      this.config.gifts.definitions
        .filter(gift => !seen.has(gift.id))
        .filter(gift => !gift.deferredReason && this.giftIsUnlocked(gift, state))
        .filter(gift => (gift.minimumPerformance ?? Number.NEGATIVE_INFINITY) <= score)
        .filter(gift => (gift.maximumPerformance ?? Number.POSITIVE_INFINITY) >= score)
        .map(gift => ({
          ...gift,
          weight: Math.max(Number.EPSILON, gift.baseWeight + gift.performanceWeight * score),
        })),
      Math.max(0, this.config.gifts.choiceCount - existing.length),
      state.rng,
    ).map(gift => gift.id)
    state.giftChoiceIds = [...existing, ...replacements]
  }

  private eventCanResolve(event: EmpiresEventDefinition | undefined): boolean {
    return Boolean(event && !event.deferredReason && event.choices.some(choice => !choice.deferredReason))
  }

  private advanceSnapshotToNextCon(state: EmpiresCampaignState): void {
    this.clearCardFlagBonusesFromState(state)
    state.phase = 'cards'
    state.con += 1
    state.boutsInCon = 0
    state.performance = emptyPerformance()
    state.performanceScore = 0
    state.giftChoiceIds = []
    state.pendingResolution = null
    state.event = null
    state.durak.stage = 'attack'
    state.durak.defenderHandAtBoutStart = this.handFromState(state, state.durak.defender).length
  }

  private isRegionAccessibleInState(state: EmpiresCampaignState, regionId: string): boolean {
    return this.config.empire.map.regions.some(region => region.id === regionId)
      && !state.empire.destroyedRegionIds.includes(regionId)
  }

  private accessibleCityIdsFromState(state: EmpiresCampaignState): string[] {
    return state.empire.cities
      .filter(city => this.isRegionAccessibleInState(state, city.regionId))
      .map(city => city.id)
  }

  private accessibleCityIds(): string[] {
    return this.accessibleCityIdsFromState(this.state)
  }

  private damageCityWithMeteor(cityId: string, damageLevels: number): void {
    const city = this.city(cityId)
    if (!city || !this.isCityAccessible(cityId)) return
    const target = Object.entries(city.buildingLevels)
      .filter(([buildingId, level]) => {
        const building = this.buildingDefinitions.get(buildingId)
        return level > 0 && Boolean(building && !building.deferredReason)
      })
      .sort(([leftId, leftLevel], [rightId, rightLevel]) => (
        rightLevel - leftLevel || leftId.localeCompare(rightId)
      ))[0]
    if (!target) return
    const [buildingId, level] = target
    city.buildingLevels[buildingId] = Math.max(0, level - damageLevels)
    city.buildingInteractionLocks[buildingId] = this.state.con
    this.refreshProductions()
  }

  private applyMilitaryArson(): void {
    this.applyMilitaryArsonToState(this.state)
  }

  private applyMilitaryArsonToState(
    state: EmpiresCampaignState,
    onlyCityIds?: ReadonlySet<string>,
  ): void {
    for (const city of state.empire.cities) {
      if (onlyCityIds && !onlyCityIds.has(city.id)) continue
      if (!this.isRegionAccessibleInState(state, city.regionId)) continue
      const unitIds = Object.entries(city.recruitedUnits)
        .filter(([, count]) => count > 0)
        .map(([id]) => id)
        .sort((left, right) => left.localeCompare(right))
      const unitId = unitIds.length > 0
        ? unitIds[Math.floor(nextEmpiresRandom(state.rng) * unitIds.length)]
        : undefined
      if (unitId) {
        const remaining = Math.max(0, (city.recruitedUnits[unitId] ?? 0) - 1)
        if (remaining === 0) delete city.recruitedUnits[unitId]
        else city.recruitedUnits[unitId] = remaining
      }

      const barracks = Object.entries(city.buildingLevels)
        .filter(([buildingId, level]) => (
          level > 0 && this.buildingDefinitions.get(buildingId)?.slot === 'barracks'
        ))
        .sort(([leftId, leftLevel], [rightId, rightLevel]) => (
          rightLevel - leftLevel || leftId.localeCompare(rightId)
        ))[0]
      if (!barracks) continue
      const [buildingId, level] = barracks
      city.buildingLevels[buildingId] = Math.max(0, level - 1)
      city.buildingInteractionLocks[buildingId] = state.con
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
      const building = this.buildingDefinitions.get(buildingId)
      if (!building || building.deferredReason) return total
      const current = building
        ? this.buildingLevelDefinitionAt(building, this.effectiveLevelFromRaw(building, level))
        : null
      return total + (current?.workerDemand ?? 0)
    }, 0)
  }

  private updateOperationalBuildings(city: EmpiresCityState): void {
    city.operationalBuildingLevels = Object.fromEntries(
      Object.entries(city.buildingLevels).map(([buildingId, level]) => [
        buildingId,
        this.buildingDefinitions.get(buildingId)?.deferredReason ? 0 : level,
      ]),
    )
    const workforce = this.availableWorkforce(city)
    while (this.workerDemand(city) > workforce) {
      const candidates = Object.entries(city.operationalBuildingLevels)
        .filter(([buildingId, level]) => {
          if (level <= 0) return false
          const definition = this.buildingDefinitions.get(buildingId)
          const effective = definition
            ? this.buildingLevelDefinitionAt(definition, this.effectiveLevelFromRaw(definition, level))
            : null
          return (effective?.workerDemand ?? 0) > 0
        })
        .sort(([leftId, leftLevel], [rightId, rightLevel]) => (
          this.effectiveLevelFromRaw(this.buildingDefinitions.get(rightId), rightLevel)
          - this.effectiveLevelFromRaw(this.buildingDefinitions.get(leftId), leftLevel)
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
    return this.state.empire.cities.reduce(
      (total, city) => total + (this.isCityAccessible(city.id) ? city.population : 0),
      0,
    )
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

  private researchUsageKey(technology: EmpiresTechnologyDefinition): string {
    const family = technology.category === 'reform' || technology.category === 'doctrine'
      ? 'reform'
      : 'technology'
    const group = technology.groupId?.trim() || technology.id
    return `${family}:${group}`
  }

  private isBuildingInteractionLocked(city: EmpiresCityState, buildingId: string): boolean {
    return city.buildingInteractionLocks[buildingId] === this.state.con
  }

  private buildingLevelBonus(slot: EmpiresBuildingSlotKind): number {
    const value = this.state.empire.buildingLevelBonuses[slot]
    return Number.isFinite(value) ? Math.max(0, Math.floor(value ?? 0)) : 0
  }

  private effectiveLevelFromRaw(
    building: EmpiresBuildingDefinition | undefined,
    rawLevel: number,
  ): number {
    if (!building || building.deferredReason || rawLevel <= 0) return 0
    return rawLevel + this.buildingLevelBonus(building.slot)
  }

  private buildingLevelDefinitionAt(
    building: EmpiresBuildingDefinition,
    level: number,
  ): EmpiresBuildingLevelDefinition | null {
    if (level <= 0) return null
    const levels = [...building.levels].sort((left, right) => left.level - right.level)
    const exact = levels.find(candidate => candidate.level === level)
    if (exact) return exact
    const last = levels.at(-1)
    if (!last) return null
    if (level < last.level) {
      return [...levels].reverse().find(candidate => candidate.level < level) ?? levels[0]
    }
    const previous = levels.at(-2) ?? last
    const steps = level - last.level
    const productionIds = new Set([
      ...(last.production ?? []).map(item => item.resourceId),
      ...(previous.production ?? []).map(item => item.resourceId),
    ])
    const production = [...productionIds].map((resourceId) => {
      const lastAmount = last.production?.find(item => item.resourceId === resourceId)?.amount ?? 0
      const previousAmount = previous.production?.find(item => item.resourceId === resourceId)?.amount ?? lastAmount
      return { resourceId, amount: Math.max(0, lastAmount + (lastAmount - previousAmount) * steps) }
    })
    const lastWorkers = last.workerDemand ?? 0
    const previousWorkers = previous.workerDemand ?? lastWorkers
    return {
      ...last,
      level,
      workerDemand: Math.max(0, lastWorkers + (lastWorkers - previousWorkers) * steps),
      production,
    }
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
