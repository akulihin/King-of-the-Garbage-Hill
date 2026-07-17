import {
  createEmpiresRngState,
  nextEmpiresRandom,
  pickEmpiresWeighted,
  pickEmpiresWeightedWithoutReplacement,
  shuffleEmpires,
} from './rng'
import {
  abortTdBattle,
  createTdRulesIdentity,
  digestTdValue,
  replayTdBattle,
  validateTdBattlePlan,
} from './td/engine'
import { resolveEmpiresUnitLoadout } from './equipment'
import {
  applySeasonFoodProduction,
  currentSeason,
  currentSeasonFoodMultiplier,
} from './seasons'
import { EMPIRES_RANKS, EMPIRES_SUITS } from './types'
import type {
  EmpiresActionResult,
  EmpiresActor,
  EmpiresBuildingDefinition,
  EmpiresBuildingOperationView,
  EmpiresBuildingLevelDefinition,
  EmpiresBuildingSlotKind,
  EmpiresCampaignState,
  EmpiresCardDefinition,
  EmpiresCardInstance,
  EmpiresChronicleEntry,
  EmpiresChronicleEntryKind,
  EmpiresCityLoyaltyView,
  EmpiresCityState,
  EmpiresDependency,
  EmpiresEffect,
  EmpiresEndgameConfig,
  EmpiresBattleLossLoyaltyInput,
  EmpiresEventDefinition,
  EmpiresGiftDefinition,
  EmpiresMinigameResult,
  EmpiresMinigameSession,
  EmpiresLoyaltyState,
  EmpiresLoyaltyTarget,
  EmpiresPendingGiftResolution,
  EmpiresPerformanceState,
  EmpiresPhase,
  EmpiresResourceAmount,
  EmpiresResearchQuote,
  EmpiresRecruitmentQuote,
  EmpiresRecruitedUnitCohortState,
  EmpiresSnapshotEnvelope,
  EmpiresStateListener,
  EmpiresSuit,
  EmpiresTechnologyDefinition,
  EmpiresTechnologySideDefinition,
  EmpiresTechnologySideView,
  EmpiresSeasonView,
  EmpiresUnitDefinition,
  TdBattleConsequenceDefinition,
  TdBattlePlan,
  TdCommand,
  TdDeploymentPlan,
  TdPlanVariantDefinition,
  TdRulesIdentity,
  TdWaveDefinition,
} from './types'
import type {
  CombatArmorProfile,
  CombatEquipmentDefinition,
  CombatWeaponProfile,
} from './combat/types'

const EFFECT_KINDS = [
  'resource',
  'resourceMultiplier',
  'time',
  'foodProduction',
  'population',
  'loyalty',
  'loyaltyAllCities',
  'classLoyalty',
  'reputation',
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

function stableStringCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function isRecordValue(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every(entry => typeof entry === 'string' && entry.length > 0)
}

function isFrozenWeaponProfile(value: unknown): value is CombatWeaponProfile {
  if (!isRecordValue(value) || !isRecordValue(value.damageLevels) || !isStringArray(value.tags)) return false
  const levels = Object.entries(value.damageLevels)
  if (levels.length === 0 || levels.some(([id, level]) => (
    !id || typeof level !== 'number' || !Number.isFinite(level) || level < 0
  ))) return false
  if (value.mixed !== undefined && typeof value.mixed !== 'boolean') return false
  if (value.twoTyped !== undefined && typeof value.twoTyped !== 'boolean') return false
  return value.passiveIds === undefined || isStringArray(value.passiveIds)
}

function isFrozenArmorProfile(value: unknown): value is CombatArmorProfile {
  return isRecordValue(value)
    && typeof value.classId === 'string'
    && value.classId.length > 0
    && typeof value.level === 'number'
    && Number.isFinite(value.level)
    && value.level >= 0
    && (value.tags === undefined || isStringArray(value.tags))
}

function legacyTdTowerCategories(id: unknown): string[] {
  if (id === 'tower-g2-archers') return ['archer']
  if (id === 'tower-g2-crossbows') return ['crossbow']
  if (id === 'tower-g2-ballista') return ['ballista', 'artillery']
  if (id === 'tower-g2-trebuchet') return ['trebuchet', 'artillery']
  return ['tower']
}

function migrateLegacyActiveTdPlan(
  value: unknown,
  sessionId: string,
  rulesIdentity: TdRulesIdentity,
  maxCommands: number,
  maxCatchUpTicksPerFrame: number,
): TdBattlePlan {
  const plan = cloneSerializable(value) as Record<string, unknown>
  if (Array.isArray(plan.towerBases) && plan.objective && plan.gradeChoices) {
    plan.sessionId = sessionId
    plan.rulesIdentity = cloneSerializable(rulesIdentity)
    plan.maxCommands = maxCommands
    plan.maxCatchUpTicksPerFrame = maxCatchUpTicksPerFrame
    plan.equipmentStock ??= {}
    if (Array.isArray(plan.deployments)) {
      plan.deployments = plan.deployments.map(deployment => typeof deployment === 'object' && deployment
        ? {
            speedPerSecond: 0,
            cohortId: `legacy:${String((deployment as Record<string, unknown>).cityId)}:${String((deployment as Record<string, unknown>).unitId)}`,
            ...deployment,
          }
        : deployment)
    }
    return plan as unknown as TdBattlePlan
  }
  const battlefield = cloneSerializable(plan.battlefield) as Record<string, unknown>
  const towerBase = cloneSerializable(plan.towerBase) as Record<string, unknown>
  const towerBaseId = typeof towerBase.id === 'string' ? towerBase.id : 'tower-generic'
  const oldCastleNodeId = typeof battlefield.castleNodeId === 'string'
    ? battlefield.castleNodeId
    : 'castle'
  const castleMaxHp = typeof battlefield.castleMaxHp === 'number' ? battlefield.castleMaxHp : 100
  const castleArmor = battlefield.castleArmor ?? null
  battlefield.regionId = 'center'
  battlefield.objectiveNodeId = oldCastleNodeId
  battlefield.towerBaseIds = [towerBaseId]
  battlefield.allowedTowerCategoryIds = []
  battlefield.modifiers = []
  if (Array.isArray(battlefield.buildSpots)) {
    battlefield.buildSpots = battlefield.buildSpots.map(spot => typeof spot === 'object' && spot
      ? { ...spot, terrainId: 'ground' }
      : spot)
  }
  delete battlefield.mode
  delete battlefield.castleNodeId
  delete battlefield.castleMaxHp
  delete battlefield.castleArmor
  towerBase.regionId = 'center'
  towerBase.categoryIds = ['tower']
  towerBase.cost = 0
  const choices = Array.isArray(plan.towerChoices)
    ? plan.towerChoices.map(choice => typeof choice === 'object' && choice
      ? { ...choice, categoryIds: legacyTdTowerCategories((choice as Record<string, unknown>).id) }
      : choice)
    : []
  const wave = cloneSerializable(plan.wave) as Record<string, unknown>
  if (Array.isArray(wave.groups)) {
    wave.groups = wave.groups.map(group => typeof group === 'object' && group
      ? { ...group, categoryIds: ['melee'] }
      : group)
  }
  plan.sessionId = sessionId
  plan.rulesIdentity = cloneSerializable(rulesIdentity)
  plan.maxCommands = maxCommands
  plan.maxCatchUpTicksPerFrame = maxCatchUpTicksPerFrame
  plan.mode = 'defense'
  plan.battlefield = battlefield
  plan.objective = {
    id: 'legacy-active-castle',
    name: 'Крепость',
    kind: 'castle',
    owner: 'player',
    nodeId: oldCastleNodeId,
    maxHp: castleMaxHp,
    armor: castleArmor,
  }
  plan.towerBases = [towerBase]
  plan.towerChoices = choices
  plan.gradeChoices = [1, 2, 3, 4].map(grade => ({
    id: `legacy-active-center-grade-${grade}`,
    regionId: 'center',
    grade,
    choiceIds: choices.flatMap(choice => typeof choice === 'object' && choice
      && (choice as Record<string, unknown>).grade === grade
      && typeof (choice as Record<string, unknown>).id === 'string'
      ? [(choice as Record<string, unknown>).id as string]
      : []),
  }))
  plan.wave = wave
  plan.equipmentStock ??= {}
  if (Array.isArray(plan.deployments)) {
    plan.deployments = plan.deployments.map(deployment => typeof deployment === 'object' && deployment
      ? {
          speedPerSecond: 0,
          cohortId: `legacy:${String((deployment as Record<string, unknown>).cityId)}:${String((deployment as Record<string, unknown>).unitId)}`,
          ...deployment,
        }
      : deployment)
  }
  delete plan.towerBase
  return plan as unknown as TdBattlePlan
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
  if (config.schemaVersion !== 6) errors.push('schemaVersion must be 6')
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
    if ((unit.equipmentCosts ?? []).some(cost => !Number.isFinite(cost.amount) || cost.amount < 0)) {
      errors.push(`unit ${unit.id} equipment costs must be finite and non-negative`)
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
  private readonly combatEquipmentDefinitions = new Map<string, CombatEquipmentDefinition>()
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
    for (const definition of this.config.combat.equipment) {
      this.combatEquipmentDefinitions.set(definition.id, definition)
    }

    this.state = snapshot ? this.validateAndCloneSnapshot(snapshot) : this.createInitialState()
    this.syncArmyMoraleCap()
    this.scheduleDelayedSteelResearch()
    this.awardDueSteelResearch()
    this.evaluateHiddenCombinations()
    this.processTechnologyDisclosures()
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
    return { schemaVersion: 4, savedAt, state: this.snapshot() }
  }

  restore(snapshot: EmpiresCampaignState): void {
    this.state = this.validateAndCloneSnapshot(snapshot)
    this.scheduleDelayedSteelResearch()
    this.awardDueSteelResearch()
    this.evaluateHiddenCombinations()
    this.processTechnologyDisclosures()
    this.refreshProductions()
    this.emit()
  }

  beginMinigame(session: EmpiresMinigameSession): EmpiresActionResult {
    if (this.state.minigame || this.state.phase === 'minigame') {
      return failure('A minigame session is already active.')
    }
    if (this.state.phase === 'victory' || this.state.phase === 'defeat') {
      return failure('A terminal campaign cannot start a minigame.')
    }
    if (session.kind !== 'td') return failure('Unsupported minigame kind.')
    const expectedRules = this.currentTdRulesIdentity()
    if (session.id !== session.plan.sessionId
      || session.rulesIdentity.configSchemaVersion !== expectedRules.configSchemaVersion
      || session.rulesIdentity.rulesDigest !== expectedRules.rulesDigest
      || digestTdValue(session.rulesIdentity) !== digestTdValue(session.plan.rulesIdentity)) {
      return failure('Minigame rules identity does not match the active configuration and plan.')
    }
    const planErrors = validateTdBattlePlan(session.plan)
    if (planErrors.length > 0) return failure(`Invalid TD plan: ${planErrors.join('; ')}`)
    if (session.origin.returnPhase !== this.state.phase) {
      return failure('Minigame origin must return to the current campaign phase.')
    }
    this.state.minigame = cloneSerializable({
      ...session,
      attempt: Math.max(0, Math.floor(session.attempt)),
    })
    this.state.phase = 'minigame'
    return this.commit(`Minigame ${session.id} started.`)
  }

  resolveMinigame(result: EmpiresMinigameResult): EmpiresActionResult {
    const session = this.state.minigame
    if (!session) {
      const existing = this.state.minigameResultLog.find(record => (
        record.sessionId === result.sessionId
        && record.result.planId === result.planId
        && record.result.planDigest === result.planDigest
        && record.result.commandDigest === result.commandDigest
      ))
      return existing
        ? success(`Minigame ${existing.sessionId} was already resolved.`)
        : failure('No minigame session is active.')
    }
    if (this.state.phase !== 'minigame') return failure('The campaign is not in its minigame phase.')
    const expectedRules = this.currentTdRulesIdentity()
    if (result.kind !== session.kind
      || result.sessionId !== session.id
      || result.planId !== session.plan.id
      || result.planDigest !== digestTdValue(session.plan)
      || result.rulesIdentity.configSchemaVersion !== session.rulesIdentity.configSchemaVersion
      || result.rulesIdentity.rulesDigest !== session.rulesIdentity.rulesDigest
      || session.rulesIdentity.configSchemaVersion !== expectedRules.configSchemaVersion
      || session.rulesIdentity.rulesDigest !== expectedRules.rulesDigest
      || result.seed !== session.seed) {
      return failure('The minigame result does not match the active session.')
    }
    const replayed = replayTdBattle(session.plan, session.seed, result.commandLog)
    if (JSON.stringify(replayed) !== JSON.stringify(result)) {
      return failure('The minigame result failed deterministic replay validation.')
    }
    this.settleBattleOutcome(result, session)
    return this.commit(`Minigame ${session.id} resolved as ${result.outcome}.`)
  }

  abortMinigame(commandLog: readonly TdCommand[] = [], abortTick = 0): EmpiresActionResult {
    const session = this.state.minigame
    if (!session || this.state.phase !== 'minigame') {
      const lastResult = this.state.minigameResultLog[
        this.state.minigameResultLog.length - 1
      ]?.result
      return lastResult?.outcome === 'aborted'
        ? success('The last minigame was already aborted.')
        : failure('No minigame session is active.')
    }
    const result = abortTdBattle(session.plan, session.seed, commandLog, abortTick)
    if (result.outcome !== 'aborted') {
      return failure(result.error ?? 'The minigame could not be aborted from that command log.')
    }
    this.settleBattleOutcome(result, session)
    return this.commit(`Minigame ${session.id} aborted with its configured penalty.`)
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
    const targetCity = this.city(targetId)
    const regionBlocked = targetCity ? this.regionAccessBlockedReason(targetCity.regionId) : null
    if (regionBlocked) return failure(regionBlocked)
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
    const regionBlocked = this.regionAccessBlockedReason(city.regionId)
    if (regionBlocked) return failure(regionBlocked)
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
    const regionBlocked = this.regionAccessBlockedReason(city.regionId)
    if (regionBlocked) return failure(regionBlocked)
    if (building.allowedCityIds && !building.allowedCityIds.includes(cityId)) {
      return failure('That building cannot be placed in this city.')
    }
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
    const quote = this.recruitmentQuote(cityId, unitId, count)
    if (quote.blockedReason) return failure(quote.blockedReason)
    const city = this.city(cityId)
    const unit = this.unitDefinitions.get(unitId)
    if (!city || !unit) return failure('Unknown city or unit.')
    const loadout = resolveEmpiresUnitLoadout(
      unit,
      this.config.combat.equipment,
      this.state.empire.researchedTechnologyIds,
      this.state.army.equipmentStock,
      count,
    )
    const populationCost = unit.populationCost * count

    this.payResources(quote.resourceCosts, city, true)
    for (const cost of quote.equipmentCosts) {
      this.state.army.equipmentStock[cost.equipmentId] = Math.max(
        0,
        (this.state.army.equipmentStock[cost.equipmentId] ?? 0) - cost.amount,
      )
    }
    this.state.empire.daysRemaining -= quote.timeCostDays
    this.consumeRecruitmentPopulation(city, populationCost)
    this.addOrMergeCohort(city, unit, loadout, count)
    const instantCadence = this.operationalBuildingFlagValue(city, 'instantUnitEveryTurns')
    if (quote.usedFoundryInstant && instantCadence !== null && instantCadence > 0) {
      this.state.army.foundryInstantReadyConByCity[city.id] = this.state.con + Math.ceil(instantCadence)
    }
    this.refreshProductions()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`${count} ${unit.name} recruited.`)
  }

  recruitmentQuote(cityId: string, unitId: string, count = 1): EmpiresRecruitmentQuote {
    const empty = (blockedReason: string): EmpiresRecruitmentQuote => ({
      cityId,
      unitId,
      count,
      resourceCosts: [],
      equipmentCosts: [],
      timeCostDays: 0,
      loadoutId: '',
      usedFoundryInstant: false,
      blockedReason,
    })
    if (this.state.phase !== 'empire') return empty('Units can only be recruited in the empire phase.')
    if (!Number.isInteger(count) || count <= 0) return empty('Unit count must be a positive integer.')
    if ((this.state.empire.flags.recruitmentDisabled ?? 0) > 0) {
      return empty('Recruitment is disabled for this empire phase.')
    }
    const city = this.city(cityId)
    const unit = this.unitDefinitions.get(unitId)
    if (!city || !unit) return empty('Unknown city or unit.')
    if (unit.deferredReason) return empty(`That unit is deferred: ${unit.deferredReason}`)
    const loyaltyBlocked = this.recruitmentLoyaltyBlockedReason(city)
    if (loyaltyBlocked) return empty(loyaltyBlocked)
    const missingDependency = this.firstMissingDependency(unit.dependencies, city, true)
    if (missingDependency) return empty(`Missing prerequisite: ${missingDependency}.`)
    const equippedRecruitCapacity = this.operationalBuildingFlagValue(city, 'equippedRecruitCapacity')
    const recruitmentPenalty = this.state.army.recruitmentPenalties[
      this.recruitmentPenaltyKey(city.id, unit.id)
    ] ?? 0
    if (equippedRecruitCapacity !== null
      && (this.state.empire.flags.unlimitedTavernRecruitment ?? 0) <= 0
      && this.recruitedUnitCount(city) + count > Math.max(0, equippedRecruitCapacity - recruitmentPenalty)) {
      return empty('The city has reached its equipped recruitment capacity.')
    }
    const populationCost = unit.populationCost * count
    if (city.militaryPopulation < populationCost
      || city.population < populationCost
      || this.recruitablePopulation(city) < populationCost) {
      return empty('Not enough recruitable population.')
    }
    let loadout: ReturnType<typeof resolveEmpiresUnitLoadout>
    try {
      loadout = resolveEmpiresUnitLoadout(
        unit,
        this.config.combat.equipment,
        this.state.empire.researchedTechnologyIds,
        this.state.army.equipmentStock,
        count,
      )
    } catch (error) {
      return empty(error instanceof Error ? error.message : 'No valid unit loadout is available.')
    }
    const equipmentCosts = loadout.equipmentCosts.map(cost => ({
      equipmentId: cost.equipmentId,
      amount: cost.amount * count,
    }))
    const missingEquipment = equipmentCosts.find(cost => (
      (this.state.army.equipmentStock[cost.equipmentId] ?? 0) + Number.EPSILON < cost.amount
    ))
    if (missingEquipment) return empty(`Not enough equipment: ${missingEquipment.equipmentId}.`)
    const discount = Math.max(0, Math.min(
      100,
      this.operationalBuildingFlagValue(city, 'armyProductionDiscountPercent') ?? 0,
    ))
    const resourceCosts = [...unit.resourceCosts.reduce((totals, cost) => {
      totals.set(cost.resourceId, (totals.get(cost.resourceId) ?? 0) + cost.amount * count)
      return totals
    }, new Map<string, number>())]
      .map(([resourceId, amount]) => ({ resourceId, amount: amount * (100 - discount) / 100 }))
    const missingResource = this.firstMissingResource(resourceCosts, city, true)
    if (missingResource) return empty(`Not enough ${missingResource}.`)
    const instantCadence = this.operationalBuildingFlagValue(city, 'instantUnitEveryTurns')
    const instantReady = count === 1
      && instantCadence !== null
      && instantCadence > 0
      && this.state.con >= (this.state.army.foundryInstantReadyConByCity[city.id] ?? this.state.con)
    const timeDiscount = Math.max(0, Math.min(
      100,
      this.operationalBuildingFlagValue(city, 'armyProductionTimeDiscountPercent') ?? 0,
    ))
    const timeCostDays = instantReady ? 0 : unit.timeCostDays * (100 - timeDiscount) / 100
    if (this.state.empire.daysRemaining < timeCostDays) return empty('Not enough days remain.')
    const projectedCity = cloneSerializable(city)
    this.consumeRecruitmentPopulation(projectedCity, populationCost)
    projectedCity.recruitedUnitCohorts.push(this.createCohort(projectedCity, unit, loadout, count))
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
      return empty('The city does not have enough food surplus.')
    }
    return {
      cityId,
      unitId,
      count,
      resourceCosts,
      equipmentCosts,
      timeCostDays,
      loadoutId: loadout.id,
      usedFoundryInstant: instantReady,
      blockedReason: null,
    }
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
    const quote = this.researchQuote(technologyId)
    if (quote.blockedReason) return failure(quote.blockedReason)
    const technology = this.technologyDefinitions.get(technologyId)
    if (!technology) return failure('Unknown technology.')

    this.payResources(quote.resourceCosts)
    this.state.empire.daysRemaining -= quote.timeCostDays
    this.state.empire.researchedTechnologyIds.push(technologyId)
    this.state.empire.researchUsage[this.researchUsageKey(technology)] = technologyId
    if (quote.entryFromTechnologyId && technology.steel) {
      const source = this.technologyDefinitions.get(quote.entryFromTechnologyId)?.steel
      if (source && source.branchId !== technology.steel.branchId) {
        this.state.empire.steelResearch.branchEntries.push({
          fromTechnologyId: quote.entryFromTechnologyId,
          toTechnologyId: technology.id,
          fromBranchId: source.branchId,
          toBranchId: technology.steel.branchId,
          con: this.state.con,
        })
        this.state.empire.steelResearch.branchCostMultipliers[source.branchId] = Math.max(
          this.state.empire.steelResearch.branchCostMultipliers[source.branchId] ?? 1,
          this.config.empire.steelResearch.forkSourcePriceMultiplier,
        )
      }
    }
    this.applyEffects(technology.effects, 0)
    this.selectTechnologySide(technology)
    this.scheduleDelayedSteelResearch()
    this.awardDueSteelResearch()
    this.evaluateHiddenCombinations()
    this.processTechnologyDisclosures()
    this.refreshProductions()
    if (this.state.empire.daysRemaining <= 0) this.finishEmpireInternal()
    return this.commit(`${technology.name} researched.`)
  }

  researchQuote(technologyId: string): EmpiresResearchQuote {
    const technology = this.technologyDefinitions.get(technologyId)
    const researched = this.state.empire.researchedTechnologyIds.includes(technologyId)
    const empty = (blockedReason: string): EmpiresResearchQuote => ({
      technologyId,
      requiredTechnologyIds: [],
      resourceCosts: [],
      timeCostDays: 0,
      costMultiplier: 1,
      entryFromTechnologyId: null,
      freeEligibleCon: this.state.empire.steelResearch.delayedFree[technologyId]?.eligibleCon ?? null,
      blockedReason,
      researched,
    })
    if (!technology) return empty('Unknown technology.')
    const multiplier = technology.steel
      ? Math.max(1, this.state.empire.steelResearch.branchCostMultipliers[technology.steel.branchId] ?? 1)
      : 1
    const base: EmpiresResearchQuote = {
      technologyId,
      requiredTechnologyIds: technology.prerequisites.flatMap(dependency => (
        dependency.kind === 'technology' ? [dependency.technologyId] : []
      )),
      resourceCosts: technology.resourceCosts.map(cost => ({
        resourceId: cost.resourceId,
        amount: cost.amount * multiplier,
      })),
      timeCostDays: technology.timeCostDays,
      costMultiplier: multiplier,
      entryFromTechnologyId: null,
      freeEligibleCon: this.state.empire.steelResearch.delayedFree[technologyId]?.eligibleCon ?? null,
      blockedReason: null,
      researched,
    }
    if (this.state.phase !== 'empire') return { ...base, blockedReason: 'Research is only available in the empire phase.' }
    if (technology.deferredReason) {
      return { ...base, blockedReason: `That research is deferred: ${technology.deferredReason}` }
    }
    if (researched) return { ...base, blockedReason: 'That research is already complete.' }
    if (technology.steel?.stage === 'plus') {
      const eligible = base.freeEligibleCon
      const gateReason = this.delayedSteelGateBlockedReason(technology)
      return {
        ...base,
        blockedReason: eligible === null
          ? 'This + steel stage is not unlocked yet.'
          : gateReason ?? `This + steel stage unlocks automatically at con ${eligible}.`,
      }
    }
    if (technology.steel?.eliteRequired
      && (this.state.empire.flags[this.config.empire.steelResearch.militaryEliteFlagId] ?? 0) <= 0) {
      return { ...base, blockedReason: 'A military elite is required for this research.' }
    }
    const usageKey = this.researchUsageKey(technology)
    if (this.state.empire.researchUsage[usageKey]) {
      return { ...base, blockedReason: 'That research group was already used this empire phase.' }
    }
    let missingDependency = this.firstMissingDependency(technology.prerequisites)
    if (missingDependency && technology.steel?.entryFromTechnologyIds?.length) {
      const targetBranchId = technology.steel.branchId
      const entryFromTechnologyId = [...technology.steel.entryFromTechnologyIds]
        .filter(id => this.state.empire.researchedTechnologyIds.includes(id))
        .filter((id) => {
          const source = this.technologyDefinitions.get(id)
          return Boolean(source?.steel && source.steel.branchId !== targetBranchId)
        })
        .sort(stableStringCompare)[0]
      if (entryFromTechnologyId) {
        const remaining = technology.prerequisites.filter((dependency) => {
          if (dependency.kind !== 'technology') return true
          return this.technologyDefinitions.get(dependency.technologyId)?.steel?.branchId !== targetBranchId
        })
        base.requiredTechnologyIds = remaining.flatMap(dependency => (
          dependency.kind === 'technology' ? [dependency.technologyId] : []
        ))
        missingDependency = this.firstMissingDependency(remaining)
        if (!missingDependency) base.entryFromTechnologyId = entryFromTechnologyId
      }
    }
    if (missingDependency) return { ...base, blockedReason: `Missing prerequisite: ${missingDependency}.` }
    if (this.state.empire.daysRemaining < base.timeCostDays) {
      return { ...base, blockedReason: 'Not enough days remain.' }
    }
    const missingResource = this.firstMissingResource(base.resourceCosts)
    if (missingResource) return { ...base, blockedReason: `Not enough ${missingResource}.` }
    return base
  }

  currentSeasonView(): EmpiresSeasonView | null {
    const season = currentSeason(this.state.con, this.config.empire.seasons)
    if (!season) return null
    const applied = currentSeasonFoodMultiplier(
      this.state.con,
      this.config.empire.seasons,
      this.state.empire.researchedTechnologyIds,
    )
    return {
      ...cloneSerializable(season),
      foodProductionMultiplierApplied: applied,
      greenhouseEqualized: applied !== season.foodProductionMultiplier,
    }
  }

  technologySideView(technologyId: string): EmpiresTechnologySideView | null {
    const technology = this.technologyDefinitions.get(technologyId)
    const state = this.state.empire.technologySides[technologyId]
    if (!technology?.sides || !state) return null
    const side = technology.sides.definitions.find(definition => definition.id === state.sideId)
    if (!side) return null
    const revealed = state.revealedAtCon !== null
    return {
      ...cloneSerializable(state),
      technologyId,
      sideName: revealed ? side.name : 'Сторона пока скрыта',
      alignment: revealed ? side.alignment : null,
      disclosureKind: technology.sides.disclosure.kind,
    }
  }

  activeEpidemicPolicy(): EmpiresTechnologySideDefinition['epidemicPolicy'] | null {
    const policies = Object.entries(this.state.empire.technologySides)
      .flatMap(([technologyId, state]) => {
        if (state.revealedAtCon === null || state.suppressedAtCon !== null) return []
        const technology = this.technologyDefinitions.get(technologyId)
        if (technology?.deferredReason) return []
        const side = technology?.sides?.definitions
          .find(definition => definition.id === state.sideId)
        return side?.epidemicPolicy ? [side.epidemicPolicy] : []
      })
    if (policies.length === 0) return null
    return {
      preventsIntercitySpread: policies.some(policy => policy.preventsIntercitySpread),
      withinCitySpeedMultiplier: policies.reduce(
        (multiplier, policy) => multiplier * policy.withinCitySpeedMultiplier,
        1,
      ),
    }
  }

  smithSpecializationOptions(): Array<{ recipeId: string, equipmentId: string }> {
    const researched = new Set(this.state.empire.researchedTechnologyIds)
    return (this.config.td.equipmentProduction ?? [])
      .filter(recipe => !recipe.technologyId || researched.has(recipe.technologyId))
      .filter((recipe) => {
        const equipment = this.combatEquipmentDefinitions.get(recipe.equipmentId)
        return equipment?.kind === 'weapon' && !equipment.deferredReason
      })
      .sort((left, right) => stableStringCompare(left.id, right.id))
      .map(recipe => ({ recipeId: recipe.id, equipmentId: recipe.equipmentId }))
  }

  chooseSmithSpecialization(recipeId: string): EmpiresActionResult {
    if (this.state.phase !== 'empire') return failure('Smith specialization is only available in the empire phase.')
    if ((this.state.empire.flags.smithSpecializationLocked ?? 0) <= 0
      || !this.state.empire.researchedTechnologyIds.includes('reform-control-smiths')) {
      return failure('Контроль кузнецов must be researched first.')
    }
    const existing = this.state.empire.smithSpecializationRecipeId
    if (existing) {
      return existing === recipeId
        ? success('That smith specialization is already locked.')
        : failure('Smith specialization is permanent and cannot be changed.')
    }
    if (!this.smithSpecializationOptions().some(option => option.recipeId === recipeId)) {
      return failure('That smith specialization is not currently available.')
    }
    this.state.empire.smithSpecializationRecipeId = recipeId
    return this.commit(`Smith specialization locked to ${recipeId}.`)
  }

  chooseEvent(choiceId: string): EmpiresActionResult {
    const blockedReason = this.eventChoiceBlockedReason(choiceId)
    if (blockedReason) return failure(blockedReason)
    if (!this.state.event) return failure('No event choice is pending.')
    const event = this.eventDefinitions.get(this.state.event.eventId)
    if (!event) return failure('That event is deferred.')
    const choice = event?.choices.find(item => item.id === choiceId)
    if (!choice) return failure('Unknown event choice.')
    const pendingEmpireSettlement = this.state.event.empireSettlementPending === true
    const hadStarvationMultiplier = Object.prototype.hasOwnProperty.call(
      this.state.empire.flags,
      'starvationLossMultiplierPercent',
    )
    const starvationMultiplierBefore = this.state.empire.flags.starvationLossMultiplierPercent
    this.payResources(choice.resourceCosts ?? [])
    this.applyEffects(choice.effects, 0, undefined, `event:${event.id}:${choice.id}`)
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

  cityRecruitmentRemaining(cityId: string, unitId?: string): number | null {
    const city = this.city(cityId)
    if (!city || !this.isCityAccessible(cityId)) return 0
    if ((this.state.empire.flags.unlimitedTavernRecruitment ?? 0) > 0) return null
    const capacity = this.operationalBuildingFlagValue(city, 'equippedRecruitCapacity')
    if (capacity === null) return null
    const penalty = unitId
      ? this.state.army.recruitmentPenalties[this.recruitmentPenaltyKey(cityId, unitId)] ?? 0
      : Object.entries(this.state.army.recruitmentPenalties).reduce((total, [key, amount]) => (
          key.startsWith(`${cityId}:`) ? total + Math.max(0, amount) : total
        ), 0)
    return Math.max(0, capacity - penalty - this.recruitedUnitCount(city))
  }

  private armyFoodUpkeepForCity(city: EmpiresCityState): number {
    const gross = city.recruitedUnitCohorts.reduce((total, cohort) => {
      const unit = this.unitDefinitions.get(cohort.unitId)
      if (!unit || unit.deferredReason) return total
      return total + Math.max(0, cohort.count) * unit.foodUpkeep
    }, 0)
    const discount = Math.max(0, Math.min(
      100,
      this.operationalBuildingFlagValue(city, 'armyUpkeepDiscountPercent') ?? 0,
    ))
    return gross * (100 - discount) / 100
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

  workforceDivisorForLoyalty(value: number): number {
    if (!this.config.empire.loyalty.enabled) return 1
    const loyalty = Math.round(this.clampLoyalty(value))
    return this.config.empire.loyalty.workforceDivisors
      .find(entry => entry.loyalty === loyalty)?.divisor ?? 1
  }

  effectiveCityLoyalty(cityId: string): number {
    const city = this.city(cityId)
    if (!city) return 0
    const region = this.state.empire.loyalty.regions[city.regionId]
    const combined = this.clampLoyalty(city.loyalty + (region?.value ?? 0))
    if (!this.config.empire.loyalty.enabled) return combined
    const forumPercent = this.operationalBuildingFlagValue(city, 'loyaltyMultiplierPercent') ?? 0
    return this.clampLoyalty(combined * Math.max(0, 1 + forumPercent / 100))
  }

  effectiveClassLoyalty(cityId: string, populationClassId: string): number {
    const modifier = this.state.empire.loyalty.classModifiers[cityId]?.[populationClassId] ?? 0
    return this.clampLoyalty(this.effectiveCityLoyalty(cityId) + modifier)
  }

  cityLoyaltyView(cityId: string): EmpiresCityLoyaltyView | null {
    const city = this.city(cityId)
    if (!city) return null
    const regionLoyalty = this.state.empire.loyalty.regions[city.regionId]?.value ?? 0
    const baseWorkforce = this.baseAvailableWorkforce(city)
    const effectiveLoyalty = this.effectiveCityLoyalty(city.id)
    const workforceDivisor = this.workforceDivisorForLoyalty(effectiveLoyalty)
    return {
      cityId,
      cityLoyalty: city.loyalty,
      regionLoyalty,
      effectiveLoyalty,
      baseWorkforce,
      effectiveWorkforce: this.availableWorkforce(city),
      workforceDivisor,
      classLoyalty: Object.fromEntries(this.config.empire.populationClasses.map(definition => [
        definition.id,
        this.effectiveClassLoyalty(city.id, definition.id),
      ])),
    }
  }

  regionAccessBlockedReason(regionId: string): string | null {
    if (!this.config.empire.map.regions.some(region => region.id === regionId)) {
      return 'Unknown region.'
    }
    if (this.state.empire.destroyedRegionIds.includes(regionId)) {
      return 'The region is destroyed.'
    }
    if (this.state.empire.loyalty.regions[regionId]?.status === 'rebellious') {
      return 'The region is in rebellion.'
    }
    return null
  }

  applyLoyaltyDelta(target: EmpiresLoyaltyTarget, amount: number, sourceId: string): number {
    if (!Number.isFinite(amount)) throw new Error('Loyalty delta must be finite.')
    if (!sourceId.trim()) throw new Error('Loyalty delta source is required.')
    if (!this.config.empire.loyalty.enabled) return 0
    const before = this.loyaltyTargetValue(this.state, target)
    const after = this.clampLoyalty(before + amount)
    this.setLoyaltyTargetValue(this.state, target, after)
    const appliedAmount = after - before
    this.appendChronicle(this.state, {
      kind: 'loyalty',
      sourceId,
      title: 'Изменение лояльности',
      description: `${this.loyaltyTargetLabel(target)}: ${this.signedNumber(before)} → ${this.signedNumber(after)}.`,
      target: cloneSerializable(target),
      requestedAmount: amount,
      appliedAmount,
    })
    if (target.kind === 'region') this.updateRegionControl(this.state, target.regionId, sourceId)
    this.refreshLoyaltyDependents()
    return appliedAmount
  }

  applyReputationDelta(amount: number, sourceId: string): number {
    if (!Number.isFinite(amount)) throw new Error('Reputation delta must be finite.')
    if (!sourceId.trim()) throw new Error('Reputation delta source is required.')
    if (!this.config.empire.loyalty.enabled) return 0
    const before = this.state.empire.reputation
    const after = this.clampLoyalty(before + amount)
    this.state.empire.reputation = after
    const appliedAmount = after - before
    this.appendChronicle(this.state, {
      kind: 'reputation',
      sourceId,
      title: 'Изменение репутации',
      description: `Репутация империи: ${this.signedNumber(before)} → ${this.signedNumber(after)}.`,
      target: { kind: 'empire' },
      requestedAmount: amount,
      appliedAmount,
    })
    return appliedAmount
  }

  consumeBattleLoss(input: EmpiresBattleLossLoyaltyInput): boolean {
    if (!input.id.trim()) throw new Error('Battle-loss identity is required.')
    if (!Number.isFinite(input.deployed) || input.deployed <= 0
      || !Number.isFinite(input.lost) || input.lost < 0 || input.lost > input.deployed) {
      throw new Error('Battle-loss counts are invalid.')
    }
    if (this.state.empire.loyalty.consumedBattleLossIds.includes(input.id)) return false
    this.state.empire.loyalty.consumedBattleLossIds.push(input.id)
    const ratio = input.lost / input.deployed
    this.appendChronicle(this.state, {
      kind: 'battle-loss',
      sourceId: input.id,
      title: 'Военные потери учтены',
      description: `${this.loyaltyTargetLabel(input.target)}: потеряно ${input.lost} из ${input.deployed} (${Math.round(ratio * 100)}%).`,
      target: cloneSerializable(input.target),
    })
    if (ratio >= this.config.td.settlement!.lossLoyaltyThreshold
      && (this.state.empire.flags.casualtyLoyaltyPenaltyDisabled ?? 0) <= 0) {
      this.applyLoyaltyDelta(input.target, this.config.td.settlement!.loyaltyDelta, input.id)
    }
    return true
  }

  chronicleNewestFirst(): EmpiresChronicleEntry[] {
    return [...this.state.empire.chronicle]
      .sort((left, right) => right.sequence - left.sequence)
      .map(entry => cloneSerializable(entry))
  }

  buildingOperationView(cityId: string, buildingId: string): EmpiresBuildingOperationView {
    const city = this.city(cityId)
    const building = this.buildingDefinitions.get(buildingId)
    const purchasedLevel = city?.buildingLevels[buildingId] ?? 0
    const operationalLevel = city?.operationalBuildingLevels[buildingId] ?? 0
    return {
      cityId,
      buildingId,
      purchasedLevel,
      operationalLevel,
      blockedReason: !city || !building
        ? 'Unknown city or building.'
        : this.buildingOperationBlockedReason(city, building, purchasedLevel, operationalLevel),
    }
  }

  constructionBlockedReason(
    cityId: string,
    buildingId: string,
    targetLevel: number,
  ): string | null {
    if (this.state.phase !== 'empire') return 'Buildings can only be changed in the empire phase.'
    const city = this.city(cityId)
    const building = this.buildingDefinitions.get(buildingId)
    const level = building?.levels.find(candidate => candidate.level === targetLevel)
    if (!city || !building || !level) return 'Unknown city, building, or level.'
    if (building.deferredReason) return `That building is deferred: ${building.deferredReason}`
    const result = this.checkEmpireAction(city, building, level)
    return result.ok ? null : result.message
  }

  eventChoiceBlockedReason(choiceId: string): string | null {
    if (this.state.phase !== 'event' || !this.state.event) return 'No event choice is pending.'
    const event = this.eventDefinitions.get(this.state.event.eventId)
    if (!event || event.deferredReason) return 'That event is deferred.'
    const choice = event.choices.find(item => item.id === choiceId)
    if (!choice) return 'Unknown event choice.'
    if (choice.deferredReason) return `That event choice is deferred: ${choice.deferredReason}`
    const missingResource = this.firstMissingResource(choice.resourceCosts ?? [])
    return missingResource ? `Not enough ${missingResource}.` : null
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
      const produced = amount
        * (this.state.empire.productionMultipliers[resourceId] ?? 1)
        * famineMultiplier
      production[resourceId] = resourceId === foodId
        ? applySeasonFoodProduction(
            produced,
            this.state.con,
            this.config.empire.seasons,
            this.state.empire.researchedTechnologyIds,
          )
        : produced
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
      schemaVersion: 4,
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
        reputation: this.clampLoyalty(this.config.empire.loyalty.initialReputation),
        loyalty: this.initialLoyaltyState(),
        chronicle: [],
        nextChronicleSequence: 1,
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
          recruitedUnitCohorts: [],
          resources: {},
          buildingInteractionLocks: {},
          lockedFacilities: {},
          foodCommitted: 0,
          lastProduction: {},
          lastStarvationLoss: 0,
          loyalty: this.clampLoyalty(this.config.empire.loyalty.initialCityLoyalty),
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
        steelResearch: {
          branchCostMultipliers: {},
          branchEntries: [],
          delayedFree: {},
        },
        technologySides: {},
        hiddenCombinationTriggers: {},
        smithSpecializationRecipeId: null,
        giftResolutionTargets: {},
      },
      pendingResolution: null,
      minigame: null,
      minigameResultLog: [],
      minigameResultCompaction: {
        evictedCount: 0,
        historyDigest: '',
        lastSessionId: null,
        lastRulesDigest: null,
      },
      army: {
        equipmentStock: {},
        morale: this.config.td.morale?.initial ?? 0,
        maxMorale: this.config.empire.initialFlags?.maxCombatSpirit
          ?? this.config.td.morale?.maximum
          ?? 0,
        veterans: {},
        recruitmentPenalties: {},
        foundryInstantReadyConByCity: {},
      },
      external: {
        allianceThreat: this.config.td.alliance?.baseThreat ?? 0,
        nextWaveCon: this.config.td.waveEveryCons ?? Number.MAX_SAFE_INTEGER,
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
    const snapshotVersion = (snapshot as { schemaVersion: number }).schemaVersion
    if (snapshotVersion !== 1 && snapshotVersion !== 2 && snapshotVersion !== 3 && snapshotVersion !== 4) {
      throw new Error('Unsupported Empire\'s Endgame snapshot schema')
    }
    if (snapshot.configId !== this.config.id) throw new Error('Snapshot belongs to a different config')
    const state = cloneSerializable(snapshot)
    state.schemaVersion = 4
    state.minigame ??= null
    state.minigameResultLog ??= []
    state.minigameResultCompaction ??= {
      evictedCount: 0,
      historyDigest: '',
      lastSessionId: null,
      lastRulesDigest: null,
    }
    state.army ??= {
      equipmentStock: {},
      morale: 0,
      maxMorale: this.config.empire.initialFlags?.maxCombatSpirit
        ?? this.config.td.morale?.maximum
        ?? 0,
      veterans: {},
      recruitmentPenalties: {},
      foundryInstantReadyConByCity: {},
    }
    state.army.equipmentStock ??= {}
    state.army.equipmentStock = Object.fromEntries(
      Object.entries(state.army.equipmentStock)
        .filter(([, amount]) => Number.isFinite(amount) && amount >= 0),
    )
    state.army.morale ??= 0
    state.army.maxMorale ??= this.config.empire.initialFlags?.maxCombatSpirit
      ?? this.config.td.morale?.maximum
      ?? 0
    state.army.veterans ??= {}
    state.army.recruitmentPenalties ??= {}
    state.army.foundryInstantReadyConByCity ??= {}
    state.army.foundryInstantReadyConByCity = Object.fromEntries(
      Object.entries(state.army.foundryInstantReadyConByCity)
        .filter(([, con]) => Number.isFinite(con) && con >= 0)
        .map(([cityId, con]) => [cityId, Math.floor(con)]),
    )
    state.external ??= {
      allianceThreat: this.config.td.alliance?.baseThreat ?? 0,
      nextWaveCon: this.nextWaveConAtOrAfter(state.con),
      pendingOffers: [],
    }
    state.external.allianceThreat ??= 0
    state.external.nextWaveCon ??= this.nextWaveConAtOrAfter(state.con)
    state.external.pendingOffers ??= []
    state.epidemics ??= []
    state.quests ??= {}
    state.durak.godInterventions ??= 0
    this.normalizeMinigameState(state)
    const missingDestroyedRegionState = state.empire.destroyedRegionIds === undefined
    const missingBuildingBonusState = state.empire.buildingLevelBonuses === undefined
    const citiesMissingInteractionLocks = new Set(
      state.empire.cities
        .filter(city => city.buildingInteractionLocks === undefined)
        .map(city => city.id),
    )
    for (const city of state.empire.cities) {
      const legacyCityLoyalty = state.empire.flags[`loyalty:${city.id}`]
      city.loyalty = this.clampLoyalty(Number.isFinite(legacyCityLoyalty)
        ? legacyCityLoyalty
        : city.loyalty ?? this.config.empire.loyalty.initialCityLoyalty)
      city.operationalBuildingLevels ??= { ...city.buildingLevels }
      city.buildingSlotAssignments ??= Object.fromEntries(
        (this.cityDefinition(city.id)?.slots ?? []).flatMap(
          slot => slot.buildingId ? [[slot.id, slot.buildingId]] : [],
        ),
      )
      const legacyUnits = city.recruitedUnits ?? {}
      city.recruitedUnitCohorts ??= []
      if (city.recruitedUnitCohorts.length === 0) {
        for (const [unitId, rawCount] of Object.entries(legacyUnits).sort(([left], [right]) => (
          stableStringCompare(left, right)
        ))) {
          const unit = this.unitDefinitions.get(unitId)
          const count = Math.max(0, Math.floor(rawCount))
          if (count === 0) continue
          const weapon = unit?.td
            ? this.combatEquipmentDefinitions.get(unit.td.weaponEquipmentId)
            : undefined
          const armor = unit?.td?.armorEquipmentId
            ? this.combatEquipmentDefinitions.get(unit.td.armorEquipmentId)
            : undefined
          const usableWeapon = weapon && weapon.kind === 'weapon' && !weapon.deferredReason
            ? weapon
            : undefined
          city.recruitedUnitCohorts.push({
            id: `legacy:${city.id}:${unitId}`,
            unitId,
            loadoutId: 'legacy-default',
            count,
            ...(usableWeapon ? { weaponEquipmentId: usableWeapon.id } : {}),
            ...(armor && armor.kind !== 'weapon' && !armor.deferredReason
              ? { defenseEquipmentId: armor.id }
              : {}),
            weapon: usableWeapon ? cloneSerializable(usableWeapon.profile as CombatWeaponProfile) : null,
            armor: armor && armor.kind !== 'weapon' && !armor.deferredReason
              ? cloneSerializable(armor.profile as CombatArmorProfile)
              : null,
          })
        }
      }
      delete city.recruitedUnits
      const cohortIds = new Set<string>()
      city.recruitedUnitCohorts = city.recruitedUnitCohorts.filter((cohort) => {
        if (!cohort || typeof cohort.id !== 'string' || !cohort.id
          || typeof cohort.unitId !== 'string' || !cohort.unitId
          || !Number.isFinite(cohort.count)) {
          throw new Error(`Invalid recruited cohort in city ${city.id}`)
        }
        if (cohort.count <= 0) return false
        if (this.unitDefinitions.size > 0 && !this.unitDefinitions.has(cohort.unitId)) {
          throw new Error(`Recruited cohort ${cohort.id} references unknown unit ${cohort.unitId}`)
        }
        if (cohortIds.has(cohort.id)) throw new Error(`Duplicate recruited cohort ${cohort.id}`)
        cohortIds.add(cohort.id)
        cohort.count = Math.floor(cohort.count)
        cohort.loadoutId ||= 'legacy-default'
        cohort.weapon ??= null
        cohort.armor ??= null
        if (typeof cohort.loadoutId !== 'string' || !cohort.loadoutId
          || (cohort.weaponEquipmentId !== undefined
            && (typeof cohort.weaponEquipmentId !== 'string' || !cohort.weaponEquipmentId))
          || (cohort.defenseEquipmentId !== undefined
            && (typeof cohort.defenseEquipmentId !== 'string' || !cohort.defenseEquipmentId))) {
          throw new Error(`Recruited cohort ${cohort.id} has invalid loadout identity`)
        }
        if (cohort.weapon !== null && !isFrozenWeaponProfile(cohort.weapon)) {
          throw new Error(`Recruited cohort ${cohort.id} has an invalid frozen weapon profile`)
        }
        if (cohort.armor !== null && !isFrozenArmorProfile(cohort.armor)) {
          throw new Error(`Recruited cohort ${cohort.id} has an invalid frozen armor profile`)
        }
        return cohort.count > 0
      })
      city.resources ??= {}
      city.buildingInteractionLocks ??= {}
    }
    this.normalizePoliticalState(state)
    this.migratePendingLoyaltyDeltas(state)
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
    state.empire.steelResearch ??= {
      branchCostMultipliers: {},
      branchEntries: [],
      delayedFree: {},
    }
    state.empire.steelResearch.branchCostMultipliers ??= {}
    state.empire.steelResearch.branchEntries ??= []
    state.empire.steelResearch.delayedFree ??= {}
    const steelTechnologyById = new Map(this.config.empire.technologies
      .filter(technology => technology.steel)
      .map(technology => [technology.id, technology]))
    const steelBranchIds = new Set([...steelTechnologyById.values()].map(technology => technology.steel!.branchId))
    state.empire.steelResearch.branchCostMultipliers = Object.fromEntries(
      Object.entries(state.empire.steelResearch.branchCostMultipliers)
        .filter(([branchId, multiplier]) => (
          steelBranchIds.has(branchId) && Number.isFinite(multiplier) && multiplier >= 1
        )),
    )
    state.empire.steelResearch.branchEntries = state.empire.steelResearch.branchEntries.filter((entry) => {
      const from = steelTechnologyById.get(entry.fromTechnologyId)?.steel
      const to = steelTechnologyById.get(entry.toTechnologyId)?.steel
      return Boolean(from && to
        && from.branchId !== to.branchId
        && from.branchId === entry.fromBranchId
        && to.branchId === entry.toBranchId
        && Number.isFinite(entry.con)
        && entry.con >= 0)
    })
    state.empire.steelResearch.delayedFree = Object.fromEntries(
      Object.entries(state.empire.steelResearch.delayedFree).filter(([technologyId, delayed]) => {
        const technology = steelTechnologyById.get(technologyId)
        return Boolean(technology?.steel?.stage === 'plus'
          && delayed
          && Number.isFinite(delayed.scheduledAtCon)
          && Number.isFinite(delayed.eligibleCon)
          && delayed.eligibleCon >= delayed.scheduledAtCon
          && (delayed.awardedAtCon === null || Number.isFinite(delayed.awardedAtCon)))
      }),
    )
    state.empire.technologySides ??= {}
    const researchedTechnologyIds = new Set(state.empire.researchedTechnologyIds)
    state.empire.technologySides = Object.fromEntries(
      Object.entries(state.empire.technologySides).filter(([technologyId, sideState]) => {
        if (!sideState) return false
        const sides = this.technologyDefinitions.get(technologyId)?.sides
        const selectedSideExists = sides?.definitions.some(side => side.id === sideState.sideId)
        const selectedAtCon = sideState.selectedAtCon
        const revealedAtCon = sideState.revealedAtCon
        const effectsAppliedAtCon = sideState.effectsAppliedAtCon
        const suppressedAtCon = sideState.suppressedAtCon
        return Boolean(researchedTechnologyIds.has(technologyId)
          && selectedSideExists
          && Number.isInteger(selectedAtCon)
          && selectedAtCon >= 0
          && (revealedAtCon === null
            || (Number.isInteger(revealedAtCon) && revealedAtCon >= selectedAtCon))
          && (effectsAppliedAtCon === null
            || (Number.isInteger(effectsAppliedAtCon) && effectsAppliedAtCon >= selectedAtCon))
          && (suppressedAtCon === null
            || (Number.isInteger(suppressedAtCon) && suppressedAtCon >= selectedAtCon))
          && (suppressedAtCon === null || revealedAtCon !== null)
          && (suppressedAtCon === null || effectsAppliedAtCon === null))
      }),
    )
    for (const technologyId of [...state.empire.researchedTechnologyIds].sort(stableStringCompare)) {
      const technology = this.technologyDefinitions.get(technologyId)
      if (technology?.sides && !technology.deferredReason) this.selectTechnologySide(technology, state)
    }
    const hiddenCombinationIds = new Set(
      this.config.empire.hiddenCombinations.definitions.map(combination => combination.id),
    )
    state.empire.hiddenCombinationTriggers ??= {}
    state.empire.hiddenCombinationTriggers = Object.fromEntries(
      Object.entries(state.empire.hiddenCombinationTriggers).filter(([combinationId, trigger]) => (
        hiddenCombinationIds.has(combinationId)
        && Number.isFinite(trigger?.triggeredAtCon)
        && trigger.triggeredAtCon >= 0
      )),
    )
    state.empire.smithSpecializationRecipeId ??= null
    if (state.empire.smithSpecializationRecipeId !== null) {
      const recipe = this.config.td.equipmentProduction?.find(
        definition => definition.id === state.empire.smithSpecializationRecipeId,
      )
      const equipment = recipe
        ? this.combatEquipmentDefinitions.get(recipe.equipmentId)
        : null
      if (!recipe
        || equipment?.kind !== 'weapon'
        || equipment.deferredReason
        || (recipe.technologyId
          && !state.empire.researchedTechnologyIds.includes(recipe.technologyId))
        || !state.empire.researchedTechnologyIds.includes('reform-control-smiths')) {
        state.empire.smithSpecializationRecipeId = null
      }
    }
    this.scheduleDelayedSteelResearch(state)
    this.syncArmyMoraleCap(state)
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

  private nextWaveConAtOrAfter(con: number): number {
    const cadence = this.config.td.enabled ? this.config.td.waveEveryCons ?? 2 : Number.MAX_SAFE_INTEGER
    return Math.max(cadence, Math.ceil(Math.max(1, con) / cadence) * cadence)
  }

  private scheduleDelayedSteelResearch(state: EmpiresCampaignState = this.state): void {
    const researched = new Set(state.empire.researchedTechnologyIds)
    for (const technology of this.config.empire.technologies) {
      const steel = technology.steel
      if (!steel || steel.stage !== 'plus' || technology.deferredReason || researched.has(technology.id)) continue
      if (!steel.accessTechnologyId || !researched.has(steel.accessTechnologyId)) continue
      state.empire.steelResearch.delayedFree[technology.id] ??= {
        scheduledAtCon: state.con,
        eligibleCon: state.con + this.config.empire.steelResearch.delayedFreeEmpirePhases,
        awardedAtCon: null,
      }
    }
  }

  private delayedSteelGateBlockedReason(technology: EmpiresTechnologyDefinition): string | null {
    const steel = technology.steel
    if (!steel || steel.stage !== 'plus') return null
    if (steel.eliteRequired
      && (this.state.empire.flags[this.config.empire.steelResearch.militaryEliteFlagId] ?? 0) <= 0) {
      return 'A military elite is required for this research.'
    }
    for (const dependency of technology.prerequisites) {
      const missing = this.firstMissingDependency([dependency])
      if (missing) return `Missing prerequisite: ${missing}.`
    }
    return null
  }

  private awardDueSteelResearch(): boolean {
    if (this.state.phase !== 'empire') return false
    let awardedAny = false
    let awarded = true
    while (awarded) {
      awarded = false
      this.scheduleDelayedSteelResearch()
      for (const technology of [...this.config.empire.technologies].sort((left, right) => (
        stableStringCompare(left.id, right.id)
      ))) {
        const steel = technology.steel
        const delayed = this.state.empire.steelResearch.delayedFree[technology.id]
        if (!steel || steel.stage !== 'plus' || !delayed || delayed.awardedAtCon !== null) continue
        if (delayed.eligibleCon > this.state.con || technology.deferredReason) continue
        if (this.delayedSteelGateBlockedReason(technology)) continue
        if (this.state.empire.researchedTechnologyIds.includes(technology.id)) {
          delayed.awardedAtCon = this.state.con
          continue
        }
        this.state.empire.researchedTechnologyIds.push(technology.id)
        delayed.awardedAtCon = this.state.con
        this.applyEffects(technology.effects, 0)
        awardedAny = true
        awarded = true
      }
    }
    return awardedAny
  }

  private currentTdRulesIdentity(): TdRulesIdentity {
    return createTdRulesIdentity(this.config.schemaVersion, this.config.combat, this.config.td, {
      technologies: this.config.empire.technologies,
      units: this.config.empire.units ?? [],
      buildings: this.config.empire.buildings,
      steelResearch: this.config.empire.steelResearch,
    })
  }

  private normalizeMinigameState(state: EmpiresCampaignState): void {
    const currentRules = this.currentTdRulesIdentity()
    const rawLog = Array.isArray(state.minigameResultLog) ? state.minigameResultLog : []
    state.minigameResultLog = rawLog.flatMap((rawRecord, index) => {
      if (!rawRecord || typeof rawRecord !== 'object') return []
      const record = rawRecord as Partial<(typeof state.minigameResultLog)[number]>
      const result = record.result
      if (!result || result.kind !== 'td') return []
      const sessionId = record.sessionId ?? `legacy-result-${result.planId}-${index}`
      const normalizedResult = cloneSerializable(result) as EmpiresMinigameResult & {
        castleHp?: number
        castleMaxHp?: number
      }
      normalizedResult.sessionId ??= sessionId
      normalizedResult.rulesIdentity ??= {
        configSchemaVersion: 2,
        rulesDigest: result.planDigest,
      }
      normalizedResult.objectiveHp ??= normalizedResult.castleHp ?? 0
      normalizedResult.objectiveMaxHp ??= normalizedResult.castleMaxHp ?? 0
      if ((normalizedResult.terminalReason as string) === 'castle-destroyed') {
        normalizedResult.terminalReason = 'objective-destroyed'
      }
      return [{
        sessionId,
        attempt: Math.max(0, Math.floor(record.attempt ?? 0)),
        origin: record.origin ?? {
          returnPhase: 'cards',
          context: { kind: 'manual', sourceId: 'legacy-result' },
        },
        result: normalizedResult,
      }]
    })
    state.minigameResultCompaction ??= {
      evictedCount: 0,
      historyDigest: '',
      lastSessionId: null,
      lastRulesDigest: null,
    }
    this.compactMinigameResultLog(state)

    if (!state.minigame) {
      if (state.phase === 'minigame') throw new Error('Minigame phase requires an active session')
      return
    }
    const session = state.minigame as EmpiresMinigameSession & {
      id?: string
      attempt?: number
      origin?: EmpiresMinigameSession['origin']
      rulesIdentity?: TdRulesIdentity
    }
    if (session.kind !== 'td' || !session.plan || session.seed === undefined) {
      throw new Error('Active minigame session is malformed')
    }
    session.id ??= `${session.plan.id}:${String(session.seed)}`
    if (!session.rulesIdentity) {
      session.rulesIdentity = cloneSerializable(currentRules)
      session.plan = migrateLegacyActiveTdPlan(
        session.plan,
        session.id,
        currentRules,
        this.config.td.maxCommands ?? 128,
        this.config.td.maxCatchUpTicksPerFrame ?? 8,
      )
    } else if (session.rulesIdentity.configSchemaVersion !== currentRules.configSchemaVersion
      || session.rulesIdentity.rulesDigest !== currentRules.rulesDigest) {
      throw new Error('Active minigame rules identity does not match the loaded configuration')
    }
    if (session.plan.sessionId !== session.id
      || digestTdValue(session.plan.rulesIdentity) !== digestTdValue(session.rulesIdentity)) {
      throw new Error('Active minigame plan identity is stale or malformed')
    }
    const planErrors = validateTdBattlePlan(session.plan)
    if (planErrors.length > 0) throw new Error(`Invalid restored TD plan: ${planErrors.join('; ')}`)
    session.origin ??= {
      returnPhase: 'cards',
      context: { kind: 'manual', sourceId: 'legacy-minigame' },
    }
    if ((session.origin as { returnPhase: string }).returnPhase === 'minigame') {
      session.origin.returnPhase = 'cards'
    }
    session.attempt = Math.max(0, Math.floor(session.attempt ?? 0)) + 1
    state.phase = 'minigame'
  }

  private compactMinigameResultLog(state: EmpiresCampaignState = this.state): void {
    const limit = Math.max(1, Math.floor(this.config.td.resultLogLimit ?? 32))
    while (state.minigameResultLog.length > limit) {
      const evicted = state.minigameResultLog.shift()!
      state.minigameResultCompaction.evictedCount += 1
      state.minigameResultCompaction.historyDigest = digestTdValue({
        previous: state.minigameResultCompaction.historyDigest,
        sessionId: evicted.sessionId,
        attempt: evicted.attempt,
        rulesDigest: evicted.result.rulesIdentity.rulesDigest,
        resultDigest: digestTdValue(evicted.result),
      })
      state.minigameResultCompaction.lastSessionId = evicted.sessionId
      state.minigameResultCompaction.lastRulesDigest = evicted.result.rulesIdentity.rulesDigest
    }
  }

  private syncArmyMoraleCap(state: EmpiresCampaignState = this.state): void {
    const minimum = this.armyMoraleMinimum(state)
    const configuredMaximum = this.config.td.morale?.maximum ?? 0
    const flagMaximum = state.empire.flags.maxCombatSpirit
    state.army.maxMorale = Math.max(
      minimum,
      configuredMaximum,
      state.army.maxMorale ?? 0,
      Number.isFinite(flagMaximum) ? flagMaximum : 0,
    )
    state.army.morale = Math.max(minimum, Math.min(state.army.maxMorale, state.army.morale))
  }

  private armyMoraleMinimum(state: EmpiresCampaignState = this.state): number {
    const relicMinimum = state.empire.flags.minimumCombatSpirit
    return Math.max(
      this.config.td.morale?.minimum ?? 0,
      Number.isFinite(relicMinimum) ? Math.max(0, relicMinimum) : 0,
    )
  }

  private combatWeaponProfile(equipmentId: string): CombatWeaponProfile | null {
    const equipment = this.combatEquipmentDefinitions.get(equipmentId)
    if (!equipment || equipment.kind !== 'weapon' || equipment.deferredReason) return null
    return cloneSerializable(equipment.profile as CombatWeaponProfile)
  }

  private combatArmorProfile(equipmentId?: string): CombatArmorProfile | null {
    if (!equipmentId) return null
    const equipment = this.combatEquipmentDefinitions.get(equipmentId)
    if (!equipment || equipment.kind === 'weapon' || equipment.deferredReason) return null
    return cloneSerializable(equipment.profile as CombatArmorProfile)
  }

  private buildTdDeployments(
    state: EmpiresCampaignState,
    nodeId: string,
    speedPerSecond: number,
  ): TdDeploymentPlan[] {
    return state.empire.cities
      .filter(city => this.isRegionAccessibleInState(state, city.regionId))
      .sort((left, right) => stableStringCompare(left.id, right.id))
      .flatMap(city => city.recruitedUnitCohorts
        .filter(cohort => cohort.count > 0)
        .sort((left, right) => stableStringCompare(left.id, right.id))
        .flatMap((cohort) => {
          const unit = this.unitDefinitions.get(cohort.unitId)
          if (!unit || unit.deferredReason || !unit.td || !cohort.weapon) return []
          return [{
            id: cohort.id,
            cohortId: cohort.id,
            cityId: city.id,
            unitId: cohort.unitId,
            count: Math.max(0, Math.floor(cohort.count)),
            nodeId,
            speedPerSecond,
            maxHpPerUnit: unit.td.maxHp,
            attackRange: unit.td.attackRange,
            attackIntervalTicks: unit.td.attackIntervalTicks,
            weapon: cloneSerializable(cohort.weapon),
            armor: cloneSerializable(cohort.armor),
          }]
        }))
  }

  private towerLoadoutAvailableForResearch(
    loadout: NonNullable<TdBattlePlan['towerBases'][number]['loadouts']>[number],
    researchedTechnologyIds: ReadonlySet<string>,
  ): boolean {
    const equipmentIds = [
      loadout.weaponEquipmentId,
      ...(loadout.defenseEquipmentId ? [loadout.defenseEquipmentId] : []),
      ...loadout.equipmentCosts.map(cost => cost.equipmentId),
    ]
    return equipmentIds.every((equipmentId) => {
      const equipment = this.combatEquipmentDefinitions.get(equipmentId)
      return !equipment?.technologyId || researchedTechnologyIds.has(equipment.technologyId)
    })
  }

  private scaleAllianceWave(wave: TdWaveDefinition, threat: number): TdWaveDefinition {
    const curve = this.config.td.alliance!
    const scaled = cloneSerializable(wave)
    for (const group of scaled.groups) {
      group.count = Math.max(1, Math.round(group.count * (1 + curve.countPerThreat * threat)))
      group.maxHp = Math.max(Number.EPSILON, group.maxHp * (1 + curve.healthPerThreat * threat))
      group.speedPerSecond = Math.max(
        Number.EPSILON,
        group.speedPerSecond * (1 + curve.speedPerThreat * threat),
      )
    }
    return scaled
  }

  private scheduleDueWaveOnState(state: EmpiresCampaignState, completedCon: number): void {
    const td = this.config.td
    if (!td.enabled || state.minigame || completedCon < state.external.nextWaveCon) return
    const liveVariants = (td.planVariants ?? []).filter(variant => !variant.deferredReason)
    if (liveVariants.length === 0) throw new Error('TD has no live plan variant')
    const preferredIndex = Math.max(0, Math.floor(completedCon / td.waveEveryCons!) - 1)
      % liveVariants.length
    let selected: {
      variant: TdPlanVariantDefinition
      deployments: TdDeploymentPlan[]
    } | null = null
    for (let offset = 0; offset < liveVariants.length; offset += 1) {
      const variant = liveVariants[(preferredIndex + offset) % liveVariants.length]
      const battlefield = td.battlefields.find(field => field.id === variant.battlefieldId)
      if (!battlefield) continue
      const nodeId = variant.mode === 'assault'
        ? battlefield.spawnerNodeId
        : battlefield.deploymentNodeId
      const deployments = this.buildTdDeployments(
        state,
        nodeId,
        variant.deploymentSpeedPerSecond,
      )
      if (variant.mode === 'assault' && deployments.length === 0) continue
      selected = { variant, deployments }
      break
    }
    if (!selected) throw new Error('TD has no plan variant compatible with the current deployment')
    const { variant, deployments } = selected
    const battlefield = td.battlefields.find(field => field.id === variant.battlefieldId)!
    const wave = td.waves.find(candidate => candidate.id === variant.waveId)
    if (!wave) throw new Error(`TD variant ${variant.id} references a missing wave`)
    const threat = Math.max(0, state.external.allianceThreat)
    const seed = Math.floor(nextEmpiresRandom(state.rng) * 0x1_0000_0000)
    const planId = `td-${variant.mode}-${completedCon}-${variant.id}`
    const sessionId = `${planId}:${seed}`
    const rulesIdentity = this.currentTdRulesIdentity()
    const researchedTechnologyIds = new Set(state.empire.researchedTechnologyIds)
    const plan: TdBattlePlan = {
      id: planId,
      sessionId,
      rulesIdentity: cloneSerializable(rulesIdentity),
      mode: variant.mode,
      scheduledCon: completedCon,
      threat,
      tickMs: td.tickMs!,
      maxTicks: td.maxTicks!,
      maxCommands: td.maxCommands!,
      maxCatchUpTicksPerFrame: td.maxCatchUpTicksPerFrame!,
      startingBuildResources: variant.startingBuildResources ?? td.startingBuildResources!,
      battlefield: cloneSerializable(battlefield),
      objective: cloneSerializable(variant.objective),
      towerBases: cloneSerializable((td.towerBases ?? [])
        .filter(base => battlefield.towerBaseIds.includes(base.id))
        .map(base => ({
          ...base,
          ...(base.loadouts
            ? {
                loadouts: base.loadouts.filter(loadout => (
                  this.towerLoadoutAvailableForResearch(loadout, researchedTechnologyIds)
                )),
              }
            : {}),
        }))),
      towerChoices: cloneSerializable(td.towers),
      gradeChoices: cloneSerializable((td.gradeChoices ?? [])
        .filter(set => set.regionId === battlefield.regionId)),
      wave: this.scaleAllianceWave(wave, threat),
      combat: cloneSerializable(this.config.combat),
      deployments,
      equipmentStock: cloneSerializable(state.army.equipmentStock),
    }
    const planErrors = validateTdBattlePlan(plan)
    if (planErrors.length > 0) throw new Error(`Scheduled TD plan is invalid: ${planErrors.join('; ')}`)
    state.external.nextWaveCon += td.waveEveryCons!
    state.external.allianceThreat = Math.max(0, threat + td.alliance!.threatPerWave)
    state.minigame = {
      id: sessionId,
      kind: 'td',
      plan,
      rulesIdentity,
      seed,
      attempt: 0,
      origin: {
        returnPhase: 'cards',
        context: { kind: 'alliance-wave', scheduledCon: completedCon, waveId: variant.waveId },
      },
    }
    state.phase = 'minigame'
  }

  private recruitmentPenaltyKey(cityId: string, unitId: string): string {
    return `${cityId}:${unitId}`
  }

  private settleBattleOutcome(
    result: EmpiresMinigameResult,
    session: EmpiresMinigameSession,
  ): void {
    if (this.state.minigameResultLog.some(record => record.sessionId === session.id)) {
      throw new Error(`Minigame ${session.id} was already settled`)
    }
    const settlement = this.config.td.settlement!
    const planDeployments = new Map(session.plan.deployments.map(item => [item.id, item]))
    const resultDeployments = new Map(result.deployments.map(item => [item.deploymentId, item]))
    if (planDeployments.size !== resultDeployments.size) {
      throw new Error('TD result deployment count does not match its plan')
    }

    const cityLosses = new Map<string, { deployed: number, lost: number }>()
    for (const [deploymentId, deployment] of planDeployments) {
      const deploymentResult = resultDeployments.get(deploymentId)
      if (!deploymentResult
        || deploymentResult.cohortId !== deployment.cohortId
        || deploymentResult.cityId !== deployment.cityId
        || deploymentResult.unitId !== deployment.unitId
        || deploymentResult.deployed !== deployment.count
        || deploymentResult.survived < 0
        || deploymentResult.survived > deployment.count) {
        throw new Error(`TD result deployment ${deploymentId} does not match its plan`)
      }
      const city = this.city(deployment.cityId)
      if (!city) throw new Error(`TD deployment references missing city ${deployment.cityId}`)
      const cohort = city.recruitedUnitCohorts.find(candidate => candidate.id === deployment.cohortId)
      const current = Math.max(0, cohort?.count ?? 0)
      if (!cohort || cohort.unitId !== deployment.unitId || current < deployment.count) {
        throw new Error(`TD deployment ${deploymentId} exceeds the city cohort`)
      }
      const lost = deployment.count - deploymentResult.survived
      const remaining = Math.max(0, current - lost)
      if (remaining === 0) {
        city.recruitedUnitCohorts = city.recruitedUnitCohorts.filter(candidate => candidate.id !== cohort.id)
      } else {
        cohort.count = remaining
      }

      if ((this.state.empire.flags.casualtyRecruitGrowthPenaltyDisabled ?? 0) <= 0) {
        const penaltyKey = this.recruitmentPenaltyKey(deployment.cityId, deployment.unitId)
        const lossPenalty = lost * settlement.recruitmentPenaltyPerLoss
        this.state.army.recruitmentPenalties[penaltyKey] = (
          this.state.army.recruitmentPenalties[penaltyKey] ?? 0
        ) + lossPenalty
        city.militaryPopulation = Math.max(
          0,
          city.militaryPopulation - lost * settlement.growthPenaltyPerLoss,
        )
      }
      const aggregate = cityLosses.get(city.id) ?? { deployed: 0, lost: 0 }
      aggregate.deployed += deployment.count
      aggregate.lost += lost
      cityLosses.set(city.id, aggregate)

      if (deploymentResult.healthRatio > settlement.veteranHealthThreshold) {
        for (let index = 0; index < deploymentResult.survived; index += 1) {
          const veteranId = `${session.plan.id}:${deploymentId}:${index + 1}`
          this.state.army.veterans[veteranId] = {
            unitId: deployment.unitId,
            wounds: deploymentResult.healthRatio < 1 ? 1 : 0,
          }
        }
      }
    }

    for (const [equipmentId, amount] of Object.entries(result.equipmentSpent ?? {})) {
      if (!Number.isFinite(amount) || amount < 0
        || amount > (session.plan.equipmentStock[equipmentId] ?? 0) + Number.EPSILON
        || amount > (this.state.army.equipmentStock[equipmentId] ?? 0) + Number.EPSILON) {
        throw new Error(`TD result has invalid equipment spend for ${equipmentId}`)
      }
      this.state.army.equipmentStock[equipmentId] = Math.max(
        0,
        (this.state.army.equipmentStock[equipmentId] ?? 0) - amount,
      )
    }

    for (const [cityId, loss] of cityLosses) {
      this.consumeBattleLoss({
        id: `td-loss:${session.id}:${cityId}`,
        target: { kind: 'city', cityId },
        deployed: loss.deployed,
        lost: loss.lost,
      })
    }

    const consequence: TdBattleConsequenceDefinition = result.outcome === 'victory'
      ? settlement.victory
      : result.outcome === 'aborted'
        ? settlement.abort
        : settlement.defeat
    if (consequence.recruitmentPenaltyPerDeployedUnit > 0) {
      for (const deployment of session.plan.deployments) {
        const key = this.recruitmentPenaltyKey(deployment.cityId, deployment.unitId)
        this.state.army.recruitmentPenalties[key] = (
          this.state.army.recruitmentPenalties[key] ?? 0
        ) + deployment.count * consequence.recruitmentPenaltyPerDeployedUnit
      }
    }
    this.syncArmyMoraleCap()
    const moraleMinimum = this.armyMoraleMinimum()
    this.state.army.morale = Math.max(
      moraleMinimum,
      Math.min(this.state.army.maxMorale, this.state.army.morale + consequence.moraleDelta),
    )
    this.state.external.allianceThreat = Math.max(
      0,
      this.state.external.allianceThreat + consequence.allianceThreatDelta,
    )
    this.state.minigameResultLog.push({
      sessionId: session.id,
      attempt: session.attempt,
      origin: cloneSerializable(session.origin),
      result: cloneSerializable(result),
    })
    this.compactMinigameResultLog()
    this.state.minigame = null
    this.state.phase = session.origin.returnPhase
    this.refreshProductions()
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
    this.scheduleDelayedSteelResearch()
    this.awardDueSteelResearch()
    for (const city of this.state.empire.cities) {
      city.buildingInteractionLocks = Object.fromEntries(
        Object.entries(city.buildingInteractionLocks).filter(([, con]) => con === this.state.con),
      )
      city.lockedFacilities = {}
      city.foodCommitted = 0
      city.lastStarvationLoss = 0
    }

    const phaseEffectKinds = new Set<EffectKind>([
      'resource', 'time', 'population', 'loyalty', 'loyaltyAllCities',
      'classLoyalty', 'reputation', 'flag',
    ])
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
      const delayedSteel = this.state.empire.steelResearch.delayedFree[technologyId]
      if (delayedSteel?.awardedAtCon === this.state.con) continue
      this.applyEffects(technology.effects, 0, timeEffectKind)
    }
    for (const cardId of this.state.durak.playerHand) {
      const instance = this.state.cards[cardId]
      const definition = this.getDefinition(instance)
      const face = instance.inverted ? definition.inverted : definition.normal
      if (face.deferredReason) continue
      this.applyEffects(face.effects, instance.level, phaseEffectKinds, `card:${definition.id}:${instance.inverted ? 'inverted' : 'normal'}`)
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
    this.awardDueSteelResearch()
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
    this.settleEquipmentProduction()
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
    const completedCon = this.state.con
    const previousSeason = currentSeason(completedCon, this.config.empire.seasons)
    this.state.phase = 'cards'
    this.state.con += 1
    this.state.boutsInCon = 0
    this.state.performance = emptyPerformance()
    this.state.performanceScore = 0
    this.state.giftChoiceIds = []
    this.state.event = null
    this.state.durak.stage = 'attack'
    this.state.durak.defenderHandAtBoutStart = this.hand(this.state.durak.defender).length
    const nextSeason = currentSeason(this.state.con, this.config.empire.seasons)
    if (nextSeason && nextSeason.id !== previousSeason?.id) {
      const sourceId = `season:${nextSeason.id}:${this.state.con}`
      if (!this.state.empire.chronicle.some(entry => entry.sourceId === sourceId)) {
        const foodMultiplier = currentSeasonFoodMultiplier(
          this.state.con,
          this.config.empire.seasons,
          this.state.empire.researchedTechnologyIds,
        )
        this.appendChronicle(this.state, {
          kind: 'season',
          sourceId,
          title: `Наступает ${nextSeason.name.toLocaleLowerCase('ru-RU')}`,
          description: `Производство еды: ×${foodMultiplier}.`,
          target: { kind: 'empire' },
        })
      }
    }
    this.evaluateHiddenCombinations()
    this.processTechnologyDisclosures()
    this.scheduleDueWaveOnState(this.state, completedCon)
    this.refreshProductions()
  }

  private settleEquipmentProduction(): void {
    const definitions = this.config.td.equipmentProduction ?? []
    const lines = this.config.td.equipmentProductionLines ?? []
    if (definitions.length === 0 || lines.length === 0) return
    const researched = new Set(this.state.empire.researchedTechnologyIds)
    for (const city of this.state.empire.cities) {
      if (!this.isCityAccessible(city.id)) continue
      const specialization = definitions.find(definition => (
        definition.id === this.state.empire.smithSpecializationRecipeId
        && (!definition.technologyId || researched.has(definition.technologyId))
        && !this.combatEquipmentDefinitions.get(definition.equipmentId)?.deferredReason
      ))
      if (specialization) {
        const focusedCapacity = lines.reduce((total, line) => {
          const capacity = this.operationalBuildingFlagValue(city, line.capacityFlagId) ?? 0
          return total + Math.max(0, capacity) * Math.max(0, line.capacityShare)
        }, 0)
        if (focusedCapacity > 0) {
          this.state.army.equipmentStock[specialization.equipmentId] = (
            this.state.army.equipmentStock[specialization.equipmentId] ?? 0
          ) + focusedCapacity * specialization.amountPerSmithCapacity
        }
        continue
      }
      for (const line of [...lines].sort((left, right) => stableStringCompare(left.id, right.id))) {
        const capacity = this.operationalBuildingFlagValue(city, line.capacityFlagId) ?? 0
        if (capacity <= 0 || line.capacityShare <= 0) continue
        const definition = definitions
          .filter(recipe => recipe.lineId === line.id)
          .filter(recipe => !recipe.technologyId || researched.has(recipe.technologyId))
          .filter((recipe) => {
            const equipment = this.combatEquipmentDefinitions.get(recipe.equipmentId)
            return !equipment?.deferredReason
          })
          .sort((left, right) => right.priority - left.priority || stableStringCompare(left.id, right.id))[0]
        if (!definition) continue
        this.state.army.equipmentStock[definition.equipmentId] = (
          this.state.army.equipmentStock[definition.equipmentId] ?? 0
        ) + capacity * line.capacityShare * definition.amountPerSmithCapacity
      }
    }
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
    const loyaltyBlocked = this.constructionLoyaltyBlockedReason(city, building)
    if (loyaltyBlocked) return failure(loyaltyBlocked)
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
    this.awardDueSteelResearch()
    this.evaluateHiddenCombinations()
    this.processTechnologyDisclosures()
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
      } else if (dependency.kind === 'reputation') {
        if (this.state.empire.reputation < dependency.minimum) {
          return `reputation ${this.signedNumber(dependency.minimum)}`
        }
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
    sourceId = 'effect:unknown',
  ): void {
    let moraleCapChanged = false
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
      } else if (effect.kind === 'loyalty') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        this.applyLoyaltyDelta(effect.target, amount, sourceId)
      } else if (effect.kind === 'loyaltyAllCities') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        for (const city of this.state.empire.cities) {
          this.applyLoyaltyDelta({ kind: 'city', cityId: city.id }, amount, sourceId)
        }
      } else if (effect.kind === 'classLoyalty') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        for (const city of this.state.empire.cities) {
          this.applyLoyaltyDelta({
            kind: 'class',
            cityId: city.id,
            populationClassId: effect.populationClassId,
          }, amount, sourceId)
        }
      } else if (effect.kind === 'reputation') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        this.applyReputationDelta(amount, sourceId)
      } else if (effect.kind === 'flag') {
        const amount = effect.amount + (effect.amountPerLevel ?? 0) * level
        this.state.empire.flags[effect.flagId] = effect.flagId === 'minimumCombatSpirit'
          ? Math.max(this.state.empire.flags[effect.flagId] ?? 0, amount)
          : (this.state.empire.flags[effect.flagId] ?? 0) + amount
        moraleCapChanged ||= effect.flagId === 'maxCombatSpirit'
          || effect.flagId === 'minimumCombatSpirit'
      }
    }
    if (moraleCapChanged) this.syncArmyMoraleCap()
  }

  private selectTechnologySide(
    technology: EmpiresTechnologyDefinition,
    state: EmpiresCampaignState = this.state,
  ): void {
    const sides = technology.sides
    if (!sides || technology.deferredReason || state.empire.technologySides[technology.id]) return
    let sideId: string
    if (sides.selection.kind === 'fixed') {
      sideId = sides.selection.sideId
    } else {
      const totalWeight = sides.selection.weights.reduce((total, item) => total + item.weight, 0)
      let roll = nextEmpiresRandom(state.rng) * totalWeight
      sideId = sides.selection.weights.at(-1)!.sideId
      for (const item of sides.selection.weights) {
        roll -= item.weight
        if (roll < 0) {
          sideId = item.sideId
          break
        }
      }
    }
    state.empire.technologySides[technology.id] = {
      sideId,
      selectedAtCon: state.con,
      revealedAtCon: null,
      effectsAppliedAtCon: null,
      suppressedAtCon: null,
    }
  }

  private evaluateHiddenCombinations(): void {
    const combinations = this.config.empire.hiddenCombinations
    if (!combinations.enabled) return
    for (const combination of [...combinations.definitions]
      .sort((left, right) => stableStringCompare(left.id, right.id))) {
      if (combination.deferredReason
        || this.state.empire.hiddenCombinationTriggers[combination.id]
        || this.firstMissingDependency(combination.prerequisites)) continue
      this.state.empire.hiddenCombinationTriggers[combination.id] = {
        triggeredAtCon: this.state.con,
      }
      const sourceId = `hidden-combination:${combination.id}`
      if (!this.state.empire.chronicle.some(entry => entry.sourceId === sourceId)) {
        this.appendChronicle(this.state, {
          kind: 'hidden-combination',
          sourceId,
          title: 'Обнаружено скрытое сочетание',
          description: combination.name,
          target: { kind: 'empire' },
        })
      }
    }
  }

  private processTechnologyDisclosures(): void {
    for (const technology of [...this.config.empire.technologies]
      .sort((left, right) => stableStringCompare(left.id, right.id))) {
      const sides = technology.sides
      const state = this.state.empire.technologySides[technology.id]
      if (!sides || technology.deferredReason || !state || state.revealedAtCon !== null) continue
      const disclosure = sides.disclosure
      const eligible = disclosure.kind === 'onResearch'
        || (disclosure.kind === 'afterCons'
          && this.state.con >= state.selectedAtCon + disclosure.delayCons)
        || (disclosure.kind === 'hiddenCombination'
          && Boolean(this.state.empire.hiddenCombinationTriggers[disclosure.combinationId]))
      if (!eligible) continue
      const side = sides.definitions.find(definition => definition.id === state.sideId)
      if (!side) throw new Error(`Unknown selected technology side ${technology.id}:${state.sideId}`)
      state.revealedAtCon = this.state.con
      const culturallySuppressed = side.alignment === 'dark'
        && side.culturalSuppressible === true
        && this.state.empire.researchedTechnologyIds.some((technologyId) => (
          this.technologyDefinitions.get(technologyId)?.tags?.includes('cultural-suppression')
        ))
      const theocracySuppressed = side.alignment === 'dark'
        && side.tags?.includes('dark-experiment')
        && (this.state.empire.flags.darkExperimentsDisabled ?? 0) > 0
      const suppressed = culturallySuppressed || theocracySuppressed
      state.suppressedAtCon = suppressed ? this.state.con : null
      state.effectsAppliedAtCon = suppressed ? null : this.state.con
      const sourceId = `technology-side:${technology.id}:${side.id}`
      this.appendChronicle(this.state, {
        kind: 'technology-disclosure',
        sourceId,
        title: `${technology.name}: ${side.name}`,
        description: suppressed
          ? 'Тёмная сторона раскрыта, но её последствия подавлены.'
          : side.alignment === 'dark'
            ? 'Тёмная сторона раскрыта; последствия применены.'
            : 'Светлая сторона раскрыта; последствия применены.',
        target: { kind: 'empire' },
      })
      if (suppressed) continue
      if (side.alignment === 'dark') {
        this.applyReputationDelta(side.reputationDelta!, sourceId)
      }
      this.applyEffects(side.effects, 0, undefined, sourceId)
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
    return city.recruitedUnitCohorts.reduce((total, cohort) => (
      this.unitDefinitions.get(cohort.unitId)?.deferredReason
        ? total
        : total + Math.max(0, cohort.count)
    ), 0)
  }

  cityRecruitedUnitCount(cityId: string, unitId?: string): number {
    const city = this.city(cityId)
    if (!city) return 0
    return city.recruitedUnitCohorts.reduce((total, cohort) => (
      unitId && cohort.unitId !== unitId ? total : total + Math.max(0, cohort.count)
    ), 0)
  }

  private createCohort(
    city: EmpiresCityState,
    unit: EmpiresUnitDefinition,
    loadout: ReturnType<typeof resolveEmpiresUnitLoadout>,
    count: number,
  ): EmpiresRecruitedUnitCohortState {
    const defenseKey = loadout.defenseEquipmentId ?? 'none'
    const weaponKey = loadout.weaponEquipmentId ?? 'none'
    const profileKey = digestTdValue({ weapon: loadout.weapon, armor: loadout.armor })
    return {
      id: `${city.id}:${unit.id}:${loadout.id}:${weaponKey}:${defenseKey}:${profileKey}`,
      unitId: unit.id,
      loadoutId: loadout.id,
      count,
      ...(loadout.weaponEquipmentId ? { weaponEquipmentId: loadout.weaponEquipmentId } : {}),
      ...(loadout.defenseEquipmentId ? { defenseEquipmentId: loadout.defenseEquipmentId } : {}),
      weapon: cloneSerializable(loadout.weapon),
      armor: cloneSerializable(loadout.armor),
    }
  }

  private addOrMergeCohort(
    city: EmpiresCityState,
    unit: EmpiresUnitDefinition,
    loadout: ReturnType<typeof resolveEmpiresUnitLoadout>,
    count: number,
  ): void {
    const cohort = this.createCohort(city, unit, loadout, count)
    const existing = city.recruitedUnitCohorts.find(candidate => candidate.id === cohort.id)
    if (existing) existing.count += count
    else city.recruitedUnitCohorts.push(cohort)
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
    const immediateKinds = new Set<EffectKind>([
      'resource', 'time', 'population', 'loyalty', 'reputation', 'flag',
    ])
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
    const completedCon = state.con
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
    this.scheduleDueWaveOnState(state, completedCon)
  }

  private isRegionAccessibleInState(state: EmpiresCampaignState, regionId: string): boolean {
    return this.config.empire.map.regions.some(region => region.id === regionId)
      && !state.empire.destroyedRegionIds.includes(regionId)
      && state.empire.loyalty.regions[regionId]?.status !== 'rebellious'
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
      const cohorts = city.recruitedUnitCohorts
        .filter(cohort => cohort.count > 0)
        .sort((left, right) => left.id.localeCompare(right.id))
      const unitIds = [...new Set(cohorts.map(cohort => cohort.unitId))]
        .sort((left, right) => left.localeCompare(right))
      const unitId = unitIds.length > 0
        ? unitIds[Math.floor(nextEmpiresRandom(state.rng) * unitIds.length)]
        : undefined
      const cohort = unitId ? cohorts.find(candidate => candidate.unitId === unitId) : undefined
      if (cohort) {
        cohort.count = Math.max(0, cohort.count - 1)
        if (cohort.count === 0) {
          city.recruitedUnitCohorts = city.recruitedUnitCohorts.filter(candidate => candidate.id !== cohort.id)
        }
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

  private clampLoyalty(value: number): number {
    const { minimum, maximum } = this.config.empire.loyalty
    return Math.max(minimum, Math.min(maximum, value))
  }

  private signedNumber(value: number): string {
    return `${value > 0 ? '+' : ''}${Number.isInteger(value) ? value : value.toFixed(2)}`
  }

  private initialLoyaltyState(): EmpiresLoyaltyState {
    const initialClass = this.clampLoyalty(this.config.empire.loyalty.initialClassLoyalty)
    return {
      regions: Object.fromEntries(this.config.empire.map.regions.map(region => [region.id, {
        value: this.clampLoyalty(this.config.empire.loyalty.initialRegionLoyalty[region.id] ?? 0),
        status: 'controlled' as const,
        negativeStreak: 0,
        recoveryStreak: 0,
        rebelledAtCon: null,
        recoveredAtCon: null,
      }])),
      classModifiers: Object.fromEntries(this.config.empire.cities.map(city => [
        city.id,
        Object.fromEntries(this.config.empire.populationClasses.map(definition => [
          definition.id,
          initialClass,
        ])),
      ])),
      consumedBattleLossIds: [],
    }
  }

  private normalizePoliticalState(state: EmpiresCampaignState): void {
    const initial = this.initialLoyaltyState()
    const legacyFlags = state.empire.flags
    const rawReputation = Number.isFinite(state.empire.reputation)
      ? state.empire.reputation
      : legacyFlags.reputation ?? this.config.empire.loyalty.initialReputation
    state.empire.reputation = this.clampLoyalty(rawReputation)
    state.empire.loyalty ??= initial
    state.empire.loyalty.regions ??= {}
    state.empire.loyalty.classModifiers ??= {}
    state.empire.loyalty.consumedBattleLossIds = [...new Set(
      (state.empire.loyalty.consumedBattleLossIds ?? [])
        .filter(id => typeof id === 'string' && id.length > 0),
    )]

    for (const definition of this.config.empire.map.regions) {
      const flagId = `loyalty${definition.id.charAt(0).toUpperCase()}${definition.id.slice(1)}`
      const previous = state.empire.loyalty.regions[definition.id]
      const legacyValue = legacyFlags[flagId]
      const fallback = this.config.empire.loyalty.initialRegionLoyalty[definition.id] ?? 0
      const status = previous?.status === 'rebellious' ? 'rebellious' : 'controlled'
      state.empire.loyalty.regions[definition.id] = {
        value: this.clampLoyalty(Number.isFinite(legacyValue) ? legacyValue : previous?.value ?? fallback),
        status,
        negativeStreak: Math.max(0, Math.floor(previous?.negativeStreak ?? 0)),
        recoveryStreak: Math.max(0, Math.floor(previous?.recoveryStreak ?? 0)),
        rebelledAtCon: Number.isFinite(previous?.rebelledAtCon) ? previous!.rebelledAtCon : null,
        recoveredAtCon: Number.isFinite(previous?.recoveredAtCon) ? previous!.recoveredAtCon : null,
      }
      delete legacyFlags[flagId]
    }
    state.empire.loyalty.regions = Object.fromEntries(
      this.config.empire.map.regions.map(region => [region.id, state.empire.loyalty.regions[region.id]]),
    )

    const initialClass = this.clampLoyalty(this.config.empire.loyalty.initialClassLoyalty)
    state.empire.loyalty.classModifiers = Object.fromEntries(state.empire.cities.map(city => {
      const previous = state.empire.loyalty.classModifiers[city.id] ?? {}
      return [city.id, Object.fromEntries(this.config.empire.populationClasses.map(definition => [
        definition.id,
        this.clampLoyalty(Number.isFinite(previous[definition.id]) ? previous[definition.id] : initialClass),
      ]))]
    }))

    for (const city of state.empire.cities) delete legacyFlags[`loyalty:${city.id}`]
    delete legacyFlags.loyalty
    delete legacyFlags.reputation

    const validKinds = new Set<EmpiresChronicleEntryKind>([
      'loyalty', 'reputation', 'rebellion', 'recovery', 'battle-loss',
      'season', 'technology-disclosure', 'hidden-combination',
    ])
    const seenIds = new Set<string>()
    state.empire.chronicle = (state.empire.chronicle ?? [])
      .filter(entry => Boolean(entry
        && typeof entry.id === 'string'
        && entry.id.length > 0
        && !seenIds.has(entry.id)
        && Number.isInteger(entry.sequence)
        && entry.sequence > 0
        && Number.isFinite(entry.con)
        && validKinds.has(entry.kind)
        && typeof entry.sourceId === 'string'
        && typeof entry.title === 'string'
        && typeof entry.description === 'string'
        && (seenIds.add(entry.id) || true)))
      .sort((left, right) => left.sequence - right.sequence || stableStringCompare(left.id, right.id))
    const retention = this.config.empire.loyalty.chronicleRetention
    state.empire.chronicle = state.empire.chronicle.slice(-retention)
    const nextSequence = state.empire.chronicle.reduce(
      (maximum, entry) => Math.max(maximum, entry.sequence + 1),
      1,
    )
    state.empire.nextChronicleSequence = Math.max(
      nextSequence,
      Number.isInteger(state.empire.nextChronicleSequence) ? state.empire.nextChronicleSequence : 1,
    )
  }

  private migratePendingLoyaltyDeltas(state: EmpiresCampaignState): void {
    const pending = state.army.pendingLoyaltyDeltas ?? []
    for (const [index, delta] of pending.entries()) {
      if (!delta || !Number.isFinite(delta.amount) || typeof delta.sourceId !== 'string') continue
      const target: EmpiresLoyaltyTarget | null = delta.cityId
        ? { kind: 'city', cityId: delta.cityId }
        : delta.regionId
          ? { kind: 'region', regionId: delta.regionId }
          : null
      if (!target) continue
      const identity = `legacy-loss:${delta.sourceId}:${target.kind}:${
        target.kind === 'city' ? target.cityId : target.regionId
      }:${index}`
      if (state.empire.loyalty.consumedBattleLossIds.includes(identity)) continue
      state.empire.loyalty.consumedBattleLossIds.push(identity)
      this.appendChronicle(state, {
        kind: 'battle-loss',
        sourceId: identity,
        title: 'Военные потери перенесены',
        description: `${this.loyaltyTargetLabel(target)}: учтён результат боя из старого сохранения.`,
        target: cloneSerializable(target),
      })
      const before = this.loyaltyTargetValue(state, target)
      const after = this.clampLoyalty(before + delta.amount)
      this.setLoyaltyTargetValue(state, target, after)
      this.appendChronicle(state, {
        kind: 'loyalty',
        sourceId: identity,
        title: 'Изменение лояльности',
        description: `${this.loyaltyTargetLabel(target)}: ${this.signedNumber(before)} → ${this.signedNumber(after)}.`,
        target: cloneSerializable(target),
        requestedAmount: delta.amount,
        appliedAmount: after - before,
      })
      if (target.kind === 'region') this.updateRegionControl(state, target.regionId, identity)
    }
    delete state.army.pendingLoyaltyDeltas
  }

  private loyaltyTargetValue(state: EmpiresCampaignState, target: EmpiresLoyaltyTarget): number {
    if (target.kind === 'city') {
      const city = state.empire.cities.find(candidate => candidate.id === target.cityId)
      if (!city) throw new Error(`Unknown loyalty city ${target.cityId}.`)
      return city.loyalty
    }
    if (target.kind === 'region') {
      const region = state.empire.loyalty.regions[target.regionId]
      if (!region) throw new Error(`Unknown loyalty region ${target.regionId}.`)
      return region.value
    }
    const cityClasses = state.empire.loyalty.classModifiers[target.cityId]
    if (!cityClasses || !this.config.empire.populationClasses.some(
      definition => definition.id === target.populationClassId,
    )) {
      throw new Error(`Unknown loyalty class ${target.cityId}:${target.populationClassId}.`)
    }
    return cityClasses[target.populationClassId] ?? 0
  }

  private setLoyaltyTargetValue(
    state: EmpiresCampaignState,
    target: EmpiresLoyaltyTarget,
    value: number,
  ): void {
    if (target.kind === 'city') {
      const city = state.empire.cities.find(candidate => candidate.id === target.cityId)
      if (!city) throw new Error(`Unknown loyalty city ${target.cityId}.`)
      city.loyalty = value
      return
    }
    if (target.kind === 'region') {
      const region = state.empire.loyalty.regions[target.regionId]
      if (!region) throw new Error(`Unknown loyalty region ${target.regionId}.`)
      region.value = value
      return
    }
    if (!state.empire.loyalty.classModifiers[target.cityId]) {
      throw new Error(`Unknown loyalty city ${target.cityId}.`)
    }
    state.empire.loyalty.classModifiers[target.cityId][target.populationClassId] = value
  }

  private loyaltyTargetLabel(target: EmpiresLoyaltyTarget): string {
    if (target.kind === 'city') {
      return this.config.empire.cities.find(city => city.id === target.cityId)?.name ?? target.cityId
    }
    if (target.kind === 'region') {
      return this.config.empire.map.regions.find(region => region.id === target.regionId)?.name
        ?? target.regionId
    }
    const city = this.config.empire.cities.find(item => item.id === target.cityId)?.name ?? target.cityId
    const populationClass = this.config.empire.populationClasses.find(
      definition => definition.id === target.populationClassId,
    )?.name ?? target.populationClassId
    return `${city} · ${populationClass}`
  }

  private appendChronicle(
    state: EmpiresCampaignState,
    entry: Omit<EmpiresChronicleEntry, 'id' | 'sequence' | 'con'> & { kind: EmpiresChronicleEntryKind },
  ): void {
    const sequence = state.empire.nextChronicleSequence
    state.empire.nextChronicleSequence += 1
    state.empire.chronicle.push({
      id: `chronicle-${String(sequence).padStart(8, '0')}`,
      sequence,
      con: state.con,
      ...cloneSerializable(entry),
    })
    const retention = this.config.empire.loyalty.chronicleRetention
    if (state.empire.chronicle.length > retention) {
      state.empire.chronicle.splice(0, state.empire.chronicle.length - retention)
    }
  }

  private updateRegionControl(
    state: EmpiresCampaignState,
    regionId: string,
    sourceId: string,
  ): void {
    const region = state.empire.loyalty.regions[regionId]
    if (!region) throw new Error(`Unknown loyalty region ${regionId}.`)
    const rule = this.config.empire.loyalty.rebellion
    if (region.status === 'controlled') {
      region.recoveryStreak = 0
      region.negativeStreak = region.value <= rule.threshold ? region.negativeStreak + 1 : 0
      if (region.negativeStreak < rule.sustainedApplications) return
      region.status = 'rebellious'
      region.rebelledAtCon = state.con
      region.recoveredAtCon = null
      region.recoveryStreak = 0
      this.appendChronicle(state, {
        kind: 'rebellion',
        sourceId,
        title: 'Регион восстал',
        description: `${this.loyaltyTargetLabel({ kind: 'region', regionId })} вышел из обычного управления; история региона сохранена.`,
        target: { kind: 'region', regionId },
      })
      return
    }

    region.negativeStreak = region.value <= rule.threshold ? region.negativeStreak + 1 : 0
    region.recoveryStreak = region.value >= rule.recoveryThreshold ? region.recoveryStreak + 1 : 0
    if (region.recoveryStreak < rule.sustainedRecoveryApplications) return
    region.status = 'controlled'
    region.recoveredAtCon = state.con
    region.negativeStreak = 0
    region.recoveryStreak = 0
    this.appendChronicle(state, {
      kind: 'recovery',
      sourceId,
      title: 'Регион вернулся',
      description: `${this.loyaltyTargetLabel({ kind: 'region', regionId })} восстановил обычное управление.`,
      target: { kind: 'region', regionId },
    })
  }

  private refreshLoyaltyDependents(): void {
    this.normalizeProductionBoostAssignments()
    for (const city of this.state.empire.cities) this.updateOperationalBuildings(city)
    for (const city of this.state.empire.cities) {
      city.lastProduction = this.isCityAccessible(city.id) ? this.productionForCity(city) : {}
    }
  }

  private classGateBlockedReason(
    city: EmpiresCityState,
    building: EmpiresBuildingDefinition,
    allowCoercion = false,
  ): string | null {
    if (!this.config.empire.loyalty.enabled) return null
    if (allowCoercion && (this.state.empire.flags.coercionBuildingOverride ?? 0) > 0) return null
    const gate = this.config.empire.loyalty.classGates.find(
      definition => definition.buildingId === building.id,
    )
    if (!gate) return null
    const actual = this.effectiveClassLoyalty(city.id, gate.populationClassId)
    if (actual >= gate.minimumLoyalty) return null
    const className = this.config.empire.populationClasses.find(
      definition => definition.id === gate.populationClassId,
    )?.name ?? gate.populationClassId
    return `Class loyalty ${className} is ${this.signedNumber(actual)}; ${this.signedNumber(gate.minimumLoyalty)} is required.`
  }

  private constructionLoyaltyBlockedReason(
    city: EmpiresCityState,
    building: EmpiresBuildingDefinition,
  ): string | null {
    const access = this.regionAccessBlockedReason(city.regionId)
    if (access) return access
    if (!this.config.empire.loyalty.enabled) return null
    const effective = this.effectiveCityLoyalty(city.id)
    if (effective < this.config.empire.loyalty.constructionMinimumLoyalty) {
      return `City loyalty is ${this.signedNumber(effective)}; ${this.signedNumber(this.config.empire.loyalty.constructionMinimumLoyalty)} is required for construction.`
    }
    return this.classGateBlockedReason(city, building)
  }

  private recruitmentLoyaltyBlockedReason(city: EmpiresCityState): string | null {
    const access = this.regionAccessBlockedReason(city.regionId)
    if (access) return access
    if (!this.config.empire.loyalty.enabled) return null
    const effective = this.effectiveCityLoyalty(city.id)
    return effective < this.config.empire.loyalty.recruitmentMinimumLoyalty
      ? `City loyalty is ${this.signedNumber(effective)}; ${this.signedNumber(this.config.empire.loyalty.recruitmentMinimumLoyalty)} is required for recruitment.`
      : null
  }

  private buildingOperationBlockedReason(
    city: EmpiresCityState,
    building: EmpiresBuildingDefinition,
    purchasedLevel: number,
    operationalLevel: number,
  ): string | null {
    if (purchasedLevel <= 0) return null
    const access = this.regionAccessBlockedReason(city.regionId)
    if (access) return access
    if (building.deferredReason) return `That building is deferred: ${building.deferredReason}`
    const classGate = this.classGateBlockedReason(city, building, true)
    if (classGate) return classGate
    if (operationalLevel >= purchasedLevel) return null
    const workforce = this.availableWorkforce(city)
    return `Effective workforce ${Math.floor(workforce)} cannot operate all ${purchasedLevel} purchased levels.`
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
      Object.entries(city.buildingLevels).map(([buildingId, level]) => {
        const building = this.buildingDefinitions.get(buildingId)
        return [
          buildingId,
          building && this.classGateBlockedReason(city, building, true)
            ? 0
            : this.dependencyPermittedBuildingLevel(city, buildingId, level),
        ]
      }),
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

  private dependencyPermittedBuildingLevel(
    city: EmpiresCityState,
    buildingId: string,
    rawLevel: number,
  ): number {
    const building = this.buildingDefinitions.get(buildingId)
    if (!building || building.deferredReason || rawLevel <= 0) return 0
    let permittedLevel = 0
    for (const level of building.levels
      .filter(definition => definition.level <= rawLevel)
      .sort((left, right) => left.level - right.level)) {
      const operationalUnlocks = this.buildingDependencies(building, level)
        .filter(dependency => dependency.kind !== 'building')
      if (this.firstMissingDependency(operationalUnlocks, city)) break
      permittedLevel = level.level
    }
    return permittedLevel
  }

  private availableWorkforce(city: EmpiresCityState): number {
    const base = this.baseAvailableWorkforce(city)
    if (!this.config.empire.loyalty.enabled) return base
    if ((this.state.empire.flags.coercionBuildingOverride ?? 0) > 0) return base
    return Math.floor(base / this.workforceDivisorForLoyalty(this.effectiveCityLoyalty(city.id)))
  }

  private baseAvailableWorkforce(city: EmpiresCityState): number {
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
    const group = technology.steel?.branchId
      ?? technology.groupId?.trim()
      ?? technology.id
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
    if (this.awardDueSteelResearch()) this.refreshProductions()
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
