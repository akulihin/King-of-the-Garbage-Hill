import { EmpiresEndgameEngine } from './engine'
import { resolveTdWithPolicy } from './td/qa'
import { createTdRulesIdentity } from './td/engine'
import { resolveTavern } from './tavern/engine'
import { resolveAlchemyWithPolicy } from './alchemy/qa'
import { initialQuestMemory, questCurrentNode } from './quests'
import type { TdQaPolicy } from './td/qa'
import type { CombatArmorProfile, CombatWeaponProfile } from './combat/types'
import type {
  EmpiresActionResult,
  EmpiresActor,
  EmpiresBoutStage,
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresEventDefinition,
  EmpiresPhase,
  TdBattlePlan,
  TdBattleMode,
  EmpiresMinigameKind,
} from './types'

export const EMPIRES_QA_SCENARIO_NAMES = [
  'pending-take',
  'empty-hand-pending-finish',
  'anti-bito',
  'divine-gift',
  'target-city-resources',
  'target-meteor-city',
  'empire-council-with-points',
  'expedition-planning',
  'governance',
  'domestic-economy',
  'mystic-tavern',
  'alchemy-experiment',
  'external-trade',
  'economy-content-event',
  'quest-dialogue',
  'destroyed-west',
  'loyalty-rebellion',
  'relic-production-levels',
  'season-disclosure',
  'epidemic-outbreak',
  'battle-defense',
  'battle-assault',
  'battle-swamp',
  'battle-forest',
  'battle-north',
  'battle-desert',
  'event',
  'victory',
  'defeat',
] as const

export type EmpiresQaScenarioName = typeof EMPIRES_QA_SCENARIO_NAMES[number]

export interface EmpiresQaValidationIssue {
  code: string
  message: string
}

export interface EmpiresQaSnapshotValidation {
  ok: boolean
  issues: EmpiresQaValidationIssue[]
}

export interface EmpiresQaScenarioFixture {
  name: EmpiresQaScenarioName
  title: string
  description: string
  seed: string | number
  snapshot: EmpiresCampaignState
  validation: EmpiresQaSnapshotValidation
}

export interface EmpiresQaScenarioOptions {
  seed?: string | number
}

export type EmpiresQaPlayerCardAction =
  | { kind: 'play-card', actor: 'player', cardId: string, attackIndex?: number }
  | { kind: 'take-cards', actor: 'player' }
  | { kind: 'end-attack', actor: 'player' }

export interface EmpiresQaPlayerTurnCheck {
  applies: boolean
  ok: boolean
  phase: EmpiresPhase
  stage: EmpiresBoutStage
  handSize: number
  actions: EmpiresQaPlayerCardAction[]
  message: string
}

export type EmpiresQaAction =
  | EmpiresQaPlayerCardAction
  | { kind: 'advance-god' }
  | { kind: 'choose-gift', giftId: string }
  | { kind: 'resolve-target', targetId: string }
  | { kind: 'finish-empire' }
  | { kind: 'resolve-minigame', policy: TdQaPolicy | 'tavern-fast' | 'alchemy-greedy' }
  | { kind: 'choose-event', eventId: string, choiceId: string }
  | { kind: 'advance-dialogue', questId: string, choiceId: string }
  | { kind: 'dismiss-dialogue', questId: string }

export interface EmpiresQaStateDigest {
  phase: EmpiresPhase
  revision: number
  con: number
  boutsInCon: number
  currentActor: EmpiresActor | null
  stage: EmpiresBoutStage
  deckCount: number
  playerHandCount: number
  godHandCount: number
  tableAttackCount: number
  undefendedAttackCount: number
  giftChoiceCount: number
  pendingResolutionKind: string | null
  pendingTargetCount: number
  destroyedRegionCount: number
  rebelliousRegionCount: number
  reputation: number
  chronicleCount: number
  chronicleLastId: string | null
  daysRemaining: number
  eventId: string | null
  minigameId: string | null
  minigameKind: EmpiresMinigameKind | null
  minigameAttempt: number | null
  minigameMode: TdBattleMode | null
  minigameRegionId: string | null
  minigameRulesSchemaVersion: number | null
  minigameRulesDigest: string | null
  minigameCommandLimit: number | null
  minigameResultCount: number
  minigameResultLimit: number | null
  minigameResultEvictedCount: number
  minigameResultHistoryDigest: string
  minigameResultLastSessionId: string | null
  minigameResultLastRulesDigest: string | null
  rngDraws: number
  cosmeticRngDraws: number
  deckMemoryInspectionsUsed: number
  godInterventionCount: number
  godInterventionHistoryCount: number
  godInterventionHistoryDigest: string
  godDialogueCount: number
  godDialogueOrder: string[]
  godDialogueHistoryDigest: string
  outcomeReason: string | null
  seasonId: string | null
  technologyDisclosureCount: number
  activeAdvisorCount: number
  governorAssignmentCount: number
  activeEpidemicCount: number
  activeLoanCount: number
  insuranceContractCount: number
  activeFairActivityCount: number
  activeExternalOfferCount: number
  externalOfferHistoryCount: number
  economyEventHistoryCount: number
  smugglingPolicyActive: boolean
  horsePactActive: boolean
  activeMandatoryQuestId: string | null
  activeQuestStatus: string | null
  activeQuestNodeId: string | null
  questHistoryCount: number
  questStateDigest: string
}

export interface EmpiresQaTraceEntry {
  step: number
  action: EmpiresQaAction
  result: EmpiresActionResult
  before: EmpiresQaStateDigest
  after: EmpiresQaStateDigest
}

export type EmpiresQaStallCode =
  | 'no-player-action'
  | 'no-gift-choice'
  | 'no-pending-target'
  | 'no-event-choice'
  | 'no-dialogue-choice'
  | 'no-minigame-session'
  | 'unsupported-phase'
  | 'action-failed'
  | 'state-not-advanced'
  | 'step-limit'

export interface EmpiresQaStallDiagnostic {
  code: EmpiresQaStallCode
  message: string
  at: EmpiresQaStateDigest
  availablePlayerActions: EmpiresQaPlayerCardAction[]
}

export interface EmpiresQaAutoplayOptions {
  seed?: string | number
  startSnapshot?: EmpiresCampaignState
  maxSteps?: number
  tdPolicy?: TdQaPolicy
}

export interface EmpiresQaAutoplayResult {
  completed: boolean
  seed: string | number
  steps: number
  checkedPlayerTurns: number
  phaseVisits: Record<EmpiresPhase, number>
  resolvedEventIds: string[]
  resolvedMinigames: Array<{
    sessionId: string
    kind: EmpiresMinigameKind
    mode: TdBattleMode | null
    regionId: string | null
    rulesDigest: string
  }>
  trace: EmpiresQaTraceEntry[]
  stall: EmpiresQaStallDiagnostic | null
  snapshot: EmpiresCampaignState
}

export interface EmpiresQaDeckInspection {
  bottomCardId: string | null
  nextDrawCardId: string | null
  trumpSourceCardId: string | null
  configuredTrumpSuit: string | null
  expectedTrumpSuit: string
  actualTrumpSuit: string
  ok: boolean
}

const SCENARIO_COPY: Record<EmpiresQaScenarioName, { title: string, description: string }> = {
  'pending-take': {
    title: 'Pending take with throw-ins',
    description: 'God has declared a take and the player still has a legal matching throw-in.',
  },
  'empty-hand-pending-finish': {
    title: 'Empty hand pending finish',
    description: 'The player has no card left, but must still end the pending take.',
  },
  'anti-bito': {
    title: 'Anti-bito winner interception',
    description: 'A premature winner path returns eligible discard instances before the configured cap.',
  },
  'divine-gift': {
    title: 'Divine gift',
    description: 'Three deterministic gifts are ready for selection.',
  },
  'target-city-resources': {
    title: 'Targeted city resources',
    description: 'The resource grant has been accepted and is waiting for one city target.',
  },
  'target-meteor-city': {
    title: 'Targeted meteor strike',
    description: 'The meteor has been accepted and is waiting for one city target.',
  },
  'empire-council-with-points': {
    title: 'Empire and card council',
    description: 'The empire phase is active and the card council has points to spend.',
  },
  'expedition-planning': {
    title: 'Southern expedition planning',
    description: 'The South has a canonical roster and enough regional food to launch the fortress expedition.',
  },
  governance: {
    title: 'Advisor judgment and Perst assignment',
    description: 'The empire phase is ready for one advisor judgment and one permanent Perst governor flow.',
  },
  'domestic-economy': {
    title: 'Domestic economy obligations and carriers',
    description: 'Bank, insurance, Fair, Temple, and Tavern carriers expose executable state and an active loan schedule.',
  },
  'mystic-tavern': {
    title: 'Tavern and mystic cards',
    description: 'A deterministic Tavern visit exposes both authored sections and the separate mystic row.',
  },
  'alchemy-experiment': {
    title: 'Tetris-alchemy experiment',
    description: 'A source-backed Assembly session is ready for real controls or deterministic QA explosion settlement.',
  },
  'external-trade': {
    title: 'External actors and persisted offers',
    description: 'Людовик and Alliance merchants expose deterministic accept, decline, trade, transfer, and denial paths.',
  },
  'economy-content-event': {
    title: 'Customs smuggling decision',
    description: 'A completed Customs trade exposes its typed target, next-con policy, and bounded decision history.',
  },
  'quest-dialogue': {
    title: 'Quest dialogue',
    description: 'Палач is active at its authored opening node with deterministic dialogue actions.',
  },
  'destroyed-west': {
    title: 'Destroyed western region',
    description: 'The west is permanently destroyed and its cities are inaccessible.',
  },
  'loyalty-rebellion': {
    title: 'Loyalty rebellion and recovery',
    description: 'Battle loss, rebellion, recovery, restore, and chronicle retention are all represented.',
  },
  'relic-production-levels': {
    title: 'Farm and lumber relic',
    description: 'Farm and lumber current and maximum levels receive the relic bonus.',
  },
  'season-disclosure': {
    title: 'Season and technology disclosure',
    description: 'The campaign crossed from summer into winter and disclosed one deterministic dark technology side.',
  },
  'epidemic-outbreak': {
    title: 'Epidemic outbreak and medical protection',
    description: 'A live plague exposes its map badge, city projection, stacked protection, and serialized lifecycle.',
  },
  'battle-defense': {
    title: 'Central Alliance defense',
    description: 'A deterministic central defense wave is ready for canvas play or QA fast resolve.',
  },
  'battle-assault': {
    title: 'Central Alliance assault',
    description: 'A deterministic player assault on the central Alliance fort is ready for QA.',
  },
  'battle-swamp': {
    title: 'Eastern swamp defense',
    description: 'The swamp battlefield and its inaccessible tower clearings are ready for QA.',
  },
  'battle-forest': {
    title: 'Western forest defense',
    description: 'The forest battlefield and its ranged tower rules are ready for QA.',
  },
  'battle-north': {
    title: 'Northern shore defense',
    description: 'The ship wave and artillery-only northern tower catalog are ready for QA.',
  },
  'battle-desert': {
    title: 'Southern desert defense',
    description: 'The desert battlefield and deployment attrition rule are ready for QA.',
  },
  event: {
    title: 'Pending event',
    description: 'A deterministic empire event is waiting for a choice.',
  },
  victory: {
    title: 'Victory outcome',
    description: 'A terminal state in which the player emptied their hand.',
  },
  defeat: {
    title: 'Defeat outcome',
    description: 'A terminal state in which God emptied their hand.',
  },
}

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function qaRulesIdentity(config: EmpiresEndgameConfig) {
  return createTdRulesIdentity(config.schemaVersion, config.combat, config.td, {
    technologies: config.empire.technologies,
    units: config.empire.units ?? [],
    buildings: config.empire.buildings,
    steelResearch: config.empire.steelResearch,
  })
}

function configWithSeed(config: EmpiresEndgameConfig, seed: string | number): EmpiresEndgameConfig {
  const seeded = cloneJson(config)
  seeded.seed = seed
  return seeded
}

function allLocatedCardIds(state: EmpiresCampaignState): string[] {
  return [
    ...state.durak.deck,
    ...state.durak.playerHand,
    ...state.durak.godHand,
    ...state.durak.discard,
    ...state.durak.table.flatMap(pair => [
      pair.attackCardId,
      ...(pair.defenseCardId ? [pair.defenseCardId] : []),
    ]),
  ]
}

function firstEligibleEvent(config: EmpiresEndgameConfig, con: number): EmpiresEventDefinition | null {
  return config.empire.events.find(event => (
    !event.deferredReason
    && (event.minimumCon ?? Number.NEGATIVE_INFINITY) <= con
    && (event.maximumCon ?? Number.POSITIVE_INFINITY) >= con
    && event.choices.some(choice => !choice.deferredReason)
  )) ?? null
}

function createPendingTakeSnapshot(
  engine: EmpiresEndgameEngine,
  emptyPlayerHand: boolean,
): EmpiresCampaignState {
  const state = engine.snapshot()
  const regularCards = engine.config.cards.filter(card => card.rank !== 'joker')
  const rankPair = regularCards
    .map(card => regularCards.filter(candidate => candidate.rank === card.rank))
    .find(cards => cards.length >= 2)
  if (!rankPair || engine.config.durak.maxAttackCards < 2) {
    throw new Error('QA pending-take scenarios require two regular cards of one rank and maxAttackCards >= 2.')
  }

  const [attack, throwIn] = rankPair
  const excluded = new Set([attack.id, throwIn.id])
  const defenderCards = engine.config.cards
    .filter(card => !excluded.has(card.id))
    .slice(0, Math.max(2, Math.min(engine.config.durak.handSize, 3)))
    .map(card => card.id)
  defenderCards.forEach(cardId => excluded.add(cardId))
  const playerHand = emptyPlayerHand ? [] : [throwIn.id]
  if (emptyPlayerHand) excluded.delete(throwIn.id)

  state.phase = 'cards'
  state.outcomeReason = null
  state.event = null
  state.giftChoiceIds = []
  state.durak.attacker = 'player'
  state.durak.defender = 'god'
  state.durak.stage = 'taking'
  state.durak.table = [{ attackCardId: attack.id, defenseCardId: null }]
  state.durak.playerHand = playerHand
  state.durak.godHand = defenderCards
  state.durak.discard = []
  state.durak.deck = engine.config.cards
    .map(card => card.id)
    .filter(cardId => !excluded.has(cardId))
  state.durak.defenderHandAtBoutStart = Math.max(2, defenderCards.length)
  return state
}

function createAntiBitoSnapshot(engine: EmpiresEndgameEngine): EmpiresCampaignState {
  const state = engine.snapshot()
  const excludedDefinitions = new Set(engine.config.god.antiBito.excludedDefinitionIds)
  const ordered = engine.config.cards.map(card => card.id)
  const eligible = ordered.filter(cardId => !excludedDefinitions.has(state.cards[cardId].definitionId))
  const attackCardId = eligible[0]
  const defenseCardId = eligible[1]
  const godHandCardId = eligible[2]
  if (!engine.config.god.enabled || !engine.config.god.antiBito.enabled
    || !attackCardId || !defenseCardId || !godHandCardId) {
    throw new Error('QA anti-bito scenario requires live God rules and three eligible cards.')
  }
  const occupied = new Set([attackCardId, defenseCardId, godHandCardId])
  state.phase = 'cards'
  state.durak.consecutiveBito = Math.max(
    0,
    engine.config.god.antiBito.minimumConsecutiveBito - 1,
  )
  state.boutsInCon = 0
  state.durak.deck = []
  state.durak.playerHand = []
  state.durak.godHand = [godHandCardId]
  state.durak.discard = ordered.filter(cardId => !occupied.has(cardId))
  state.durak.table = [{ attackCardId, defenseCardId }]
  state.durak.attacker = 'player'
  state.durak.defender = 'god'
  state.durak.stage = 'throwIn'
  state.durak.defenderHandAtBoutStart = 2
  state.durak.godInterventions = 0
  state.god.interventions = []
  state.god.interventionCompaction = { evictedCount: 0, historyDigest: '' }
  state.god.nextInterventionSequence = 1
  state.outcomeReason = null
  return new EmpiresEndgameEngine(engine.config, state).snapshot()
}

function createGiftSnapshot(engine: EmpiresEndgameEngine): EmpiresCampaignState {
  const state = engine.snapshot()
  const giftChoiceIds = engine.config.gifts.definitions
    .filter(gift => !gift.deferredReason)
    .slice(0, engine.config.gifts.choiceCount)
    .map(gift => gift.id)
  if (giftChoiceIds.length !== engine.config.gifts.choiceCount) {
    throw new Error('QA divine-gift scenario requires a complete gift choice set.')
  }
  state.phase = 'divineGift'
  state.giftChoiceIds = giftChoiceIds
  state.performanceScore = Math.max(1, state.performanceScore)
  state.upgradePoints = Math.max(3, state.upgradePoints)
  state.event = null
  state.outcomeReason = null
  state.durak.table = []
  return state
}

function createTargetedGiftSnapshot(
  config: EmpiresEndgameConfig,
  giftSnapshot: EmpiresCampaignState,
  kind: 'cityResources' | 'meteorCity',
): EmpiresCampaignState {
  const gift = config.gifts.definitions.find(item => item.resolution?.kind === kind)
  if (!gift) throw new Error(`QA ${kind} scenario requires a matching targeted gift.`)
  const state = cloneJson(giftSnapshot)
  state.giftChoiceIds = [
    gift.id,
    ...config.gifts.definitions
      .filter(item => item.id !== gift.id && !item.deferredReason)
      .slice(0, Math.max(0, config.gifts.choiceCount - 1))
      .map(item => item.id),
  ]
  const engine = new EmpiresEndgameEngine(config, state)
  const result = engine.chooseGift(gift.id)
  if (!result.ok
    || engine.state.phase !== 'divineGift'
    || engine.state.pendingResolution?.kind !== kind) {
    throw new Error(`QA ${kind} scenario could not start: ${result.message}`)
  }
  return engine.snapshot()
}

function createEmpireSnapshot(
  config: EmpiresEndgameConfig,
  giftSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const engine = new EmpiresEndgameEngine(config, giftSnapshot)
  const giftId = engine.state.giftChoiceIds.find((id) => {
    const gift = config.gifts.definitions.find(item => item.id === id)
    return !gift?.deferredReason
  })
  if (!giftId) throw new Error('QA empire scenario cannot start without a gift choice.')
  const result = engine.chooseGift(giftId)
  if (!result.ok) {
    throw new Error(`QA empire scenario could not start: ${result.message}`)
  }
  if (engine.state.pendingResolution) {
    const targetId = engine.state.pendingResolution.eligibleTargetIds
      .find(id => engine.isCityAccessible(id))
    if (!targetId) throw new Error('QA empire scenario cannot resolve its targeted gift.')
    const targetResult = engine.resolvePendingTarget(targetId)
    if (!targetResult.ok) {
      throw new Error(`QA empire scenario could not resolve its gift: ${targetResult.message}`)
    }
  }
  if (engine.state.phase !== 'empire') {
    throw new Error('QA empire scenario did not enter the empire phase.')
  }
  const state = engine.snapshot()
  state.upgradePoints = Math.max(3, state.upgradePoints)
  const mercyCardId = state.durak.playerHand.at(-1)
  if (mercyCardId) state.cards[mercyCardId].inverted = true
  return state
}

function createExpeditionPlanningSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = cloneJson(empireSnapshot)
  const city = state.empire.cities.find(candidate => candidate.regionId === 'south')
  if (!city) throw new Error('QA expedition scenario requires an accessible southern city.')
  for (const candidate of state.empire.cities.filter(item => item.regionId === 'south')) {
    candidate.resources[config.empire.foodResourceId] = candidate.id === city.id ? 100_000 : 0
  }
  const cohortId = 'qa-expedition-south-light'
  const unitInstanceIds = ['qa-expedition-unit-1', 'qa-expedition-unit-2']
  city.recruitedUnitCohorts.push({
    id: cohortId,
    unitId: 'unit-light',
    loadoutId: 'qa-expedition-loadout',
    count: unitInstanceIds.length,
    unitInstanceIds,
    weaponEquipmentId: 'weapon-mace',
    weapon: { damageLevels: { impact: 100 }, tags: ['mace', 'qa-expedition'] },
    armor: null,
  })
  for (const id of unitInstanceIds) {
    state.army.unitInstances[id] = {
      id,
      cityId: city.id,
      cohortId,
      unitId: 'unit-light',
      healthRatio: 1,
      veteran: false,
      wounds: 0,
      recoveryStartedAtCon: null,
      readyAtCon: state.con,
    }
  }
  state.empire.daysRemaining = Math.max(state.empire.daysRemaining, 10)
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  return new EmpiresEndgameEngine(config, state).snapshot()
}

function createDomesticEconomySnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = cloneJson(empireSnapshot)
  const rules = config.empire.domesticEconomy
  const carrierIds = [
    rules.loan.bankBuildingId,
    rules.insurance.buildingId,
    rules.fair.buildingId,
    rules.temple.buildingId,
    rules.tavern.buildingId,
  ]
  const accessEngine = new EmpiresEndgameEngine(config, state)
  const cities = accessEngine.state.empire.cities
    .filter(city => accessEngine.isCityAccessible(city.id))
    .slice(0, carrierIds.length)
  if (!rules.enabled || cities.length !== carrierIds.length) {
    throw new Error('QA domestic-economy scenario requires enabled rules and five accessible cities.')
  }
  for (const [index, buildingId] of carrierIds.entries()) {
    const city = state.empire.cities.find(item => item.id === cities[index].id)!
    const slot = config.empire.cities.find(item => item.id === city.id)?.slots.find(item => item.kind === 'unique')
    if (!slot) throw new Error(`QA domestic-economy carrier ${buildingId} has no unique slot.`)
    city.buildingLevels[buildingId] = 1
    city.operationalBuildingLevels[buildingId] = 1
    city.buildingSlotAssignments[slot.id] = buildingId
    city.baseProduction[rules.goldResourceId] = 200
    city.lastProduction[rules.goldResourceId] = 200
  }
  state.empire.resources[rules.goldResourceId] = Math.max(
    state.empire.resources[rules.goldResourceId] ?? 0,
    20_000,
  )
  state.empire.researchedTechnologyIds = [...new Set([
    ...state.empire.researchedTechnologyIds,
    rules.loan.bankingTechnologyId,
    rules.fair.technologyId,
    ...carrierIds.flatMap(buildingId => config.empire.buildings
      .find(building => building.id === buildingId)?.levels
      .flatMap(level => level.dependencies ?? [])
      .flatMap(dependency => dependency.kind === 'technology' ? [dependency.technologyId] : []) ?? []),
  ])]
  const relicId = config.gifts.definitions.find(gift => gift.kind === 'relic' && !gift.deferredReason)?.id
  if (relicId && !state.empire.claimedGiftIds.includes(relicId)) state.empire.claimedGiftIds.push(relicId)
  const engine = new EmpiresEndgameEngine(config, state)
  const loan = engine.takeLoan(cities[0].id)
  if (!loan.ok) throw new Error(`QA domestic-economy loan could not start: ${loan.message}`)
  return engine.snapshot()
}

function createTavernSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  if (!config.tavern.enabled) throw new Error('QA Tavern scenario requires live Tavern rules.')
  const state = cloneJson(empireSnapshot)
  state.phase = 'empire'
  state.con = Math.max(state.con, config.tavern.spawn.eligibleCon)
  state.tavern.spawnChecked = true
  state.tavern.spawned = true
  state.tavern.spawnedAtCon = state.con
  state.tavern.lastVisitedCon = null
  state.empire.resources[config.empire.domesticEconomy.goldResourceId] = 100_000
  const engine = new EmpiresEndgameEngine(config, state)
  const city = engine.state.empire.cities.find(candidate => engine.isCityAccessible(candidate.id))
  if (!city) throw new Error('QA Tavern scenario requires an accessible city.')
  const result = engine.startTavernVisit(city.id)
  if (!result.ok || engine.state.minigame?.kind !== 'tavern') {
    throw new Error(`QA Tavern scenario could not start: ${result.message}`)
  }
  return engine.snapshot()
}

function createAlchemySnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const recipe = config.alchemy.recipes.find(candidate => !candidate.deferredReason)
  if (!config.alchemy.enabled || !recipe) {
    throw new Error('QA Alchemy scenario requires one live recipe.')
  }
  const state = cloneJson(empireSnapshot)
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = Math.max(config.alchemy.dayCost + 1, config.empire.daysPerPhase)
  const accessEngine = new EmpiresEndgameEngine(config, state)
  const cityDefinition = config.empire.cities.find(definition => (
    definition.slots.some(slot => slot.kind === 'unique')
    && accessEngine.isCityAccessible(definition.id)
  ))
  const city = state.empire.cities.find(candidate => candidate.id === cityDefinition?.id)
  const slot = cityDefinition?.slots.find(candidate => candidate.kind === 'unique')
  if (!city || !slot) throw new Error('QA Alchemy scenario requires an accessible unique city slot.')
  city.buildingLevels[config.alchemy.buildingId] = 1
  city.operationalBuildingLevels[config.alchemy.buildingId] = 1
  city.buildingSlotAssignments[slot.id] = config.alchemy.buildingId
  for (const dependency of recipe.prerequisites) {
    if (dependency.kind === 'technology') {
      state.empire.researchedTechnologyIds = [...new Set([
        ...state.empire.researchedTechnologyIds,
        dependency.technologyId,
      ])]
    }
  }
  const engine = new EmpiresEndgameEngine(config, state)
  const result = engine.startAlchemyExperiment(city.id, recipe.id)
  if (!result.ok || engine.state.minigame?.kind !== 'alchemy') {
    throw new Error(`QA Alchemy scenario could not start: ${result.message}`)
  }
  return engine.snapshot()
}

function createExternalTradeSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const external = config.empire.externalEconomy
  if (!external.enabled || external.offers.length < 2) {
    throw new Error('QA external-trade scenario requires enabled rules and at least two offers.')
  }
  const state = cloneJson(empireSnapshot)
  const cityDefinition = config.empire.cities.find(city => (
    city.slots.some(slot => slot.kind === 'maritime')
  ))
  const city = state.empire.cities.find(candidate => candidate.id === cityDefinition?.id)
  if (!city || !cityDefinition) throw new Error('QA external-trade scenario requires a maritime city.')
  const uniqueSlot = cityDefinition.slots.find(slot => slot.kind === 'unique')
  const maritimeSlot = cityDefinition.slots.find(slot => slot.kind === 'maritime')
  if (!uniqueSlot || !maritimeSlot) throw new Error('QA external-trade city lacks trade carrier slots.')
  city.buildingLevels[external.customs.buildingId] = 1
  city.operationalBuildingLevels[external.customs.buildingId] = 1
  city.buildingSlotAssignments[uniqueSlot.id] = external.customs.buildingId
  city.buildingLevels[external.seaPort.buildingId] = 1
  city.operationalBuildingLevels[external.seaPort.buildingId] = 1
  city.buildingSlotAssignments[maritimeSlot.id] = external.seaPort.buildingId
  city.resources[external.goldResourceId] = 20_000
  for (const offer of external.offers) city.resources[offer.resourceId] = Math.max(
    city.resources[offer.resourceId] ?? 0,
    offer.resourceAmount * 2,
  )
  state.empire.reputation = Math.max(3, ...external.offers.map(offer => offer.minimumReputation))
  state.empire.researchedTechnologyIds = [...new Set([
    ...state.empire.researchedTechnologyIds,
    external.tradeRoutesTechnologyId,
    external.transfer.compassTechnologyId,
    external.customs.merchantGuildsTechnologyId,
  ])]
  state.empire.flags[external.transfer.speedFlagId] = 25
  state.empire.flags[external.customs.merchantGuildsFlagId] = 1
  state.external.nextOfferRefreshCon = state.con
  const engine = new EmpiresEndgameEngine(config, state)
  const refresh = engine.refreshExternalOffers()
  if (!refresh.ok || engine.state.external.activeOffers.length < 2) {
    throw new Error(`QA external-trade offers could not refresh: ${refresh.message}`)
  }
  return engine.snapshot()
}

function createEventSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = cloneJson(empireSnapshot)
  const event = firstEligibleEvent(config, state.con)
  if (!event) throw new Error('QA event scenario requires an event eligible for the current con.')
  state.phase = 'event'
  state.event = {
    instanceId: `economy-event-${state.empire.economyContent.nextEventSequence++}`,
    eventId: event.id,
    empireSettlementPending: false,
  }
  state.empire.daysRemaining = 0
  state.outcomeReason = null
  return state
}

function createQuestDialogueSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const definition = config.quests.definitions.find(quest => quest.id === 'quest-palach')
  const stage = definition?.stages.find(candidate => candidate.id === definition.entryStageId)
  const node = stage?.nodes.find(candidate => candidate.id === stage.entryNodeId)
  if (!config.quests.enabled || !definition || !stage || !node || definition.deferredReason) {
    throw new Error('QA quest-dialogue scenario requires the live Палач graph.')
  }
  const state = cloneJson(empireSnapshot)
  state.con = Math.max(2, state.con)
  state.quests[definition.id] = {
    questId: definition.id,
    status: 'active',
    stageId: stage.id,
    nodeId: node.id,
    memory: initialQuestMemory(definition),
    run: 1,
    nodeVisit: 1,
    lastAppliedChoiceIdentity: null,
    consumedTriggerIds: [`quest:${definition.id}:trigger:conReached:con:2`],
    compactedTriggerCount: 0,
    compactedTriggerDigest: '',
    startedAtCon: state.con,
    finishedAtCon: null,
  }
  state.questRuntime.activeMandatoryQuestId = definition.id
  state.questRuntime.mandatoryQueue = []
  return new EmpiresEndgameEngine(config, state).snapshot()
}

function createEconomyContentEventSnapshot(
  config: EmpiresEndgameConfig,
  externalSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = cloneJson(externalSnapshot)
  const content = config.empire.economyContent
  const city = state.empire.cities
    .filter(candidate => (candidate.operationalBuildingLevels[
      config.empire.externalEconomy.customs.buildingId
    ] ?? 0) > 0)
    .sort((left, right) => left.id.localeCompare(right.id))[0]
  const event = config.empire.events.find(item => item.id === content.smuggling.eventId)
  if (!content.enabled || !city || !event || event.deferredReason) {
    throw new Error('QA economy-content scenario requires live Customs smuggling carriers.')
  }
  state.external.customs.completedTrades = Math.max(1, state.external.customs.completedTrades)
  state.external.customs.smugglingEligible = true
  state.external.customs.lastTradeCon = state.con
  state.external.customs.lastTradeCityId = city.id
  state.phase = 'event'
  state.event = {
    instanceId: `economy-event-${state.empire.economyContent.nextEventSequence++}`,
    eventId: event.id,
    empireSettlementPending: false,
    targetCityId: city.id,
  }
  state.empire.daysRemaining = 0
  state.outcomeReason = null
  return new EmpiresEndgameEngine(config, state).snapshot()
}

function createDestroyedRegionSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
  regionId: string,
): EmpiresCampaignState {
  const state = cloneJson(empireSnapshot)
  state.empire.destroyedRegionIds = Array.from(new Set([
    ...state.empire.destroyedRegionIds,
    regionId,
  ]))
  return new EmpiresEndgameEngine(config, state).snapshot()
}

function createLoyaltyRebellionSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const engine = new EmpiresEndgameEngine(config, empireSnapshot)
  const retention = config.empire.loyalty.chronicleRetention
  for (let index = 0; index < retention + 3; index += 1) {
    engine.applyReputationDelta(index % 2 === 0 ? 1 : -1, `qa:reputation:${index}`)
  }
  const northernCity = engine.state.empire.cities.find(city => city.regionId === 'north')
  if (!northernCity) throw new Error('QA loyalty scenario requires a northern city.')
  engine.consumeBattleLoss({
    id: 'qa:battle-loss:north',
    target: { kind: 'city', cityId: northernCity.id },
    deployed: 10,
    lost: 1,
  })
  engine.applyLoyaltyDelta({ kind: 'region', regionId: 'north' }, -6, 'qa:north:unrest-1')
  engine.applyLoyaltyDelta({ kind: 'region', regionId: 'north' }, -1, 'qa:north:unrest-2')

  const restored = new EmpiresEndgameEngine(config, engine.snapshot())
  restored.applyLoyaltyDelta({ kind: 'region', regionId: 'north' }, 7, 'qa:north:recovery-1')
  restored.applyLoyaltyDelta({ kind: 'region', regionId: 'north' }, 1, 'qa:north:recovery-2')
  restored.applyLoyaltyDelta({ kind: 'region', regionId: 'west' }, -6, 'qa:west:unrest-1')
  restored.applyLoyaltyDelta({ kind: 'region', regionId: 'west' }, -1, 'qa:west:unrest-2')
  return restored.snapshot()
}

function createRelicBuildingLevelSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = cloneJson(empireSnapshot)
  const relic = config.gifts.definitions.find(gift => gift.resolution?.kind === 'buildingLevelBonus')
  if (!relic || relic.resolution?.kind !== 'buildingLevelBonus') {
    throw new Error('QA relic scenario requires a building-level-bonus gift.')
  }
  for (const slot of relic.resolution.slots) {
    state.empire.buildingLevelBonuses[slot] = (
      state.empire.buildingLevelBonuses[slot] ?? 0
    ) + relic.resolution.amount
  }
  if (!state.empire.claimedGiftIds.includes(relic.id)) state.empire.claimedGiftIds.push(relic.id)
  return new EmpiresEndgameEngine(config, state).snapshot()
}

function createSeasonDisclosureSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const scenarioConfig = cloneJson(config)
  scenarioConfig.empire.eventChance = 0
  const state = cloneJson(empireSnapshot)
  const carrier = scenarioConfig.empire.technologies.find(technology => technology.id === 'reform-city-gates')
  const darkSide = carrier?.sides?.definitions.find(side => side.alignment === 'dark')
  if (!carrier?.sides || !darkSide) {
    throw new Error('QA season-disclosure scenario requires the typed city-gates side carrier.')
  }
  delete carrier.deferredReason
  state.phase = 'empire'
  state.con = 1
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = scenarioConfig.empire.daysPerPhase
  state.external.nextWaveCon = Number.MAX_SAFE_INTEGER
  state.empire.resources[scenarioConfig.empire.foodResourceId] = 1_000_000
  for (const city of state.empire.cities) {
    city.resources[scenarioConfig.empire.foodResourceId] = 1_000_000
  }
  if (!state.empire.researchedTechnologyIds.includes(carrier.id)) {
    state.empire.researchedTechnologyIds.push(carrier.id)
  }
  state.empire.technologySides[carrier.id] = {
    sideId: darkSide.id,
    selectedAtCon: 1,
    revealedAtCon: null,
    effectsAppliedAtCon: null,
    suppressedAtCon: null,
  }
  const engine = new EmpiresEndgameEngine(scenarioConfig, state)
  const result = engine.finishEmpire()
  if (!result.ok || engine.state.con !== 2) {
    throw new Error(`QA season-disclosure scenario could not cross its con: ${result.message}`)
  }
  engine.state.phase = 'empire'
  engine.state.empire.daysRemaining = scenarioConfig.empire.daysPerPhase
  return engine.snapshot()
}

function createEpidemicOutbreakSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = cloneJson(empireSnapshot)
  state.phase = 'empire'
  state.event = null
  state.minigame = null
  state.outcomeReason = null
  state.empire.daysRemaining = config.empire.daysPerPhase
  const initiallyVisibleRegionId = config.empire.map.regions[0]?.id
  const accessEngine = new EmpiresEndgameEngine(config, state)
  const cities = state.empire.cities
    .filter(city => accessEngine.isCityAccessible(city.id))
    .sort((left, right) => (
      Number(right.regionId === initiallyVisibleRegionId)
      - Number(left.regionId === initiallyVisibleRegionId)
      || left.id.localeCompare(right.id)
    ))
  const origin = cities[0]
  const academyCity = cities[1] ?? origin
  if (!origin || !academyCity) throw new Error('QA epidemic scenario requires an accessible city.')
  for (const technologyId of ['tech-medicine', 'doctrine-science']) {
    if (!state.empire.researchedTechnologyIds.includes(technologyId)) {
      state.empire.researchedTechnologyIds.push(technologyId)
    }
  }
  origin.buildingLevels[config.empire.medical.hospitalBuildingId] = 1
  origin.operationalBuildingLevels[config.empire.medical.hospitalBuildingId] = 1
  academyCity.buildingLevels[config.empire.medical.medicalAcademyBuildingId] = 1
  academyCity.operationalBuildingLevels[config.empire.medical.medicalAcademyBuildingId] = 1
  const engine = new EmpiresEndgameEngine(config, state)
  const result = engine.startEpidemic({
    definitionId: 'epidemic-plague',
    originCityId: origin.id,
    source: { kind: 'qa', id: 'qa:epidemic-outbreak' },
  })
  if (!result.ok || engine.cityEpidemicViews(origin.id).length !== 1) {
    throw new Error(`QA epidemic scenario could not start its plague: ${result.message}`)
  }
  return new EmpiresEndgameEngine(config, engine.snapshot()).snapshot()
}

function createOutcomeSnapshot(
  engine: EmpiresEndgameEngine,
  phase: 'victory' | 'defeat',
): EmpiresCampaignState {
  const state = engine.snapshot()
  const cardIds = engine.config.cards.map(card => card.id)
  const remainingCardId = cardIds[0]
  state.phase = phase
  state.event = null
  state.giftChoiceIds = []
  state.outcomeReason = phase === 'victory'
    ? 'QA: player emptied their hand.'
    : 'QA: God emptied their hand.'
  state.durak.deck = []
  state.durak.discard = cardIds.slice(1)
  state.durak.table = []
  state.durak.stage = 'attack'
  state.durak.playerHand = phase === 'victory' ? [] : [remainingCardId]
  state.durak.godHand = phase === 'defeat' ? [] : [remainingCardId]
  state.durak.attacker = phase === 'victory' ? 'player' : 'god'
  state.durak.defender = phase === 'victory' ? 'god' : 'player'
  state.durak.defenderHandAtBoutStart = phase === 'victory' ? 1 : 1
  return state
}

const TD_QA_VARIANTS: Record<
  Extract<EmpiresQaScenarioName, `battle-${string}`>,
  { variantId: string, expectedMode: TdBattleMode, expectedRegionId: string }
> = {
  'battle-defense': {
    variantId: 'central-castle-defense',
    expectedMode: 'defense',
    expectedRegionId: 'center',
  },
  'battle-assault': {
    variantId: 'central-fort-assault',
    expectedMode: 'assault',
    expectedRegionId: 'center',
  },
  'battle-swamp': {
    variantId: 'swamp-fort-defense',
    expectedMode: 'defense',
    expectedRegionId: 'east',
  },
  'battle-forest': {
    variantId: 'forest-fort-defense',
    expectedMode: 'defense',
    expectedRegionId: 'west',
  },
  'battle-north': {
    variantId: 'north-ship-defense',
    expectedMode: 'defense',
    expectedRegionId: 'north',
  },
  'battle-desert': {
    variantId: 'desert-fort-defense',
    expectedMode: 'defense',
    expectedRegionId: 'south',
  },
}

function createBattleSnapshot(
  config: EmpiresEndgameConfig,
  engine: EmpiresEndgameEngine,
  scenarioName: keyof typeof TD_QA_VARIANTS,
): EmpiresCampaignState {
  const scenario = TD_QA_VARIANTS[scenarioName]
  const variant = (config.td.planVariants ?? []).find(candidate => (
    candidate.id === scenario.variantId && !candidate.deferredReason
  ))
  const battlefield = config.td.battlefields.find(candidate => candidate.id === variant?.battlefieldId)
  const wave = config.td.waves.find(candidate => candidate.id === variant?.waveId)
  const unit = (config.empire.units ?? []).find(definition => !definition.deferredReason && definition.td)
  const city = engine.state.empire.cities.find(candidate => engine.isCityAccessible(candidate.id))
  const weaponDefinition = unit?.td
    ? config.combat.equipment.find(equipment => (
        equipment.id === unit.td!.weaponEquipmentId
        && equipment.kind === 'weapon'
        && !equipment.deferredReason
      ))
    : null
  const armorDefinition = unit?.td?.armorEquipmentId
    ? config.combat.equipment.find(equipment => (
        equipment.id === unit.td!.armorEquipmentId
        && equipment.kind !== 'weapon'
        && !equipment.deferredReason
      ))
    : null
  if (!config.td.enabled
    || !variant
    || !battlefield
    || !wave
    || !config.td.maxCommands
    || !config.td.maxCatchUpTicksPerFrame
    || !(config.td.towerBases?.length)
    || !(config.td.gradeChoices?.length)
    || !unit?.td
    || !city
    || !weaponDefinition
    || weaponDefinition.kind !== 'weapon') {
    throw new Error(`QA ${scenarioName} scenario requires the complete live TD and army catalog.`)
  }
  if (variant.mode !== scenario.expectedMode || battlefield.regionId !== scenario.expectedRegionId) {
    throw new Error(`QA ${scenarioName} variant no longer matches its expected mode and region.`)
  }
  const rulesIdentity = qaRulesIdentity(config)
  const planId = `qa-${scenarioName}-${variant.id}`
  const sessionId = `${planId}:qa-seed`
  const deploymentNodeId = variant.mode === 'assault'
    ? battlefield.spawnerNodeId
    : battlefield.deploymentNodeId
  const state = engine.snapshot()
  const campaignCity = state.empire.cities.find(candidate => candidate.id === city.id)!
  const cohortId = `qa:${city.id}:${unit.id}:default`
  const unitInstanceIds = Array.from({ length: 3 }, (_, index) => (
    `qa:${scenarioName}:${city.id}:${unit.id}:${index + 1}`
  ))
  campaignCity.recruitedUnitCohorts = campaignCity.recruitedUnitCohorts
    .filter(cohort => cohort.id !== cohortId)
  campaignCity.recruitedUnitCohorts.push({
    id: cohortId,
    unitId: unit.id,
    loadoutId: 'qa-default',
    count: 3,
    unitInstanceIds,
    weaponEquipmentId: weaponDefinition.id,
    ...(armorDefinition ? { defenseEquipmentId: armorDefinition.id } : {}),
    weapon: cloneJson(weaponDefinition.profile as CombatWeaponProfile),
    armor: armorDefinition && armorDefinition.kind !== 'weapon'
      ? cloneJson(armorDefinition.profile as CombatArmorProfile)
      : null,
  })
  const plan: TdBattlePlan = {
    id: planId,
    sessionId,
    rulesIdentity: cloneJson(rulesIdentity),
    mode: variant.mode,
    scheduledCon: 2,
    threat: config.td.alliance?.baseThreat ?? 0,
    tickMs: config.td.tickMs!,
    maxTicks: config.td.maxTicks!,
    maxCommands: config.td.maxCommands,
    maxCatchUpTicksPerFrame: config.td.maxCatchUpTicksPerFrame,
    startingBuildResources: variant.startingBuildResources ?? config.td.startingBuildResources!,
    battlefield: cloneJson(battlefield),
    objective: cloneJson(variant.objective),
    towerBases: cloneJson(config.td.towerBases.filter(base => (
      battlefield.towerBaseIds.includes(base.id)
    ))),
    towerChoices: cloneJson(config.td.towers),
    gradeChoices: cloneJson(config.td.gradeChoices.filter(set => (
      set.regionId === battlefield.regionId
    ))),
    wave: cloneJson(wave),
    combat: cloneJson(config.combat),
    equipmentStock: cloneJson(state.army.equipmentStock),
    deployments: [{
      id: cohortId,
      cohortId,
      cityId: city.id,
      unitId: unit.id,
      unitInstanceIds,
      count: 3,
      nodeId: deploymentNodeId,
      speedPerSecond: variant.deploymentSpeedPerSecond,
      maxHpPerUnit: unit.td.maxHp,
      attackRange: unit.td.attackRange,
      attackIntervalTicks: unit.td.attackIntervalTicks,
      weapon: cloneJson(weaponDefinition.profile as CombatWeaponProfile),
      armor: armorDefinition && armorDefinition.kind !== 'weapon'
        ? cloneJson(armorDefinition.profile as CombatArmorProfile)
        : null,
    }],
  }
  for (const id of unitInstanceIds) {
    state.army.unitInstances[id] = {
      id,
      cityId: city.id,
      cohortId,
      unitId: unit.id,
      healthRatio: 1,
      veteran: false,
      wounds: 0,
      recoveryStartedAtCon: null,
      readyAtCon: state.con,
    }
  }
  state.phase = 'minigame'
  state.minigame = {
    id: sessionId,
    kind: 'td',
    plan,
    rulesIdentity: cloneJson(rulesIdentity),
    seed: `qa-${scenarioName}`,
    attempt: 0,
    origin: {
      returnPhase: 'cards',
      context: { kind: 'alliance-wave', scheduledCon: 2, waveId: wave.id },
    },
  }
  return state
}

export function listEmpiresQaPlayerCardActions(
  engine: EmpiresEndgameEngine,
): EmpiresQaPlayerCardAction[] {
  if (engine.state.phase !== 'cards' || engine.currentActor() !== 'player') return []
  const actions: EmpiresQaPlayerCardAction[] = []
  if (engine.state.durak.stage === 'defense') {
    const attackIndex = engine.state.durak.table.findIndex(pair => !pair.defenseCardId)
    for (const cardId of engine.legalDefenseCardIds('player', attackIndex)) {
      actions.push({ kind: 'play-card', actor: 'player', cardId, attackIndex })
    }
  } else {
    for (const cardId of engine.legalAttackCardIds('player')) {
      actions.push({ kind: 'play-card', actor: 'player', cardId })
    }
  }
  if (engine.canTake('player')) actions.push({ kind: 'take-cards', actor: 'player' })
  if (engine.canEndAttack('player')) actions.push({ kind: 'end-attack', actor: 'player' })
  return actions
}

export function checkEmpiresQaPlayerTurnInvariant(
  engine: EmpiresEndgameEngine,
): EmpiresQaPlayerTurnCheck {
  const applies = engine.state.phase === 'cards' && engine.currentActor() === 'player'
  const actions = listEmpiresQaPlayerCardActions(engine)
  const ok = !applies || actions.length > 0
  return {
    applies,
    ok,
    phase: engine.state.phase,
    stage: engine.state.durak.stage,
    handSize: engine.state.durak.playerHand.length,
    actions,
    message: ok
      ? applies
        ? `${actions.length} player action(s) available.`
        : 'The current state is not a player card turn.'
      : `Player card turn has no action (stage=${engine.state.durak.stage}, hand=${engine.state.durak.playerHand.length}).`,
  }
}

export function executeEmpiresQaPlayerCardAction(
  engine: EmpiresEndgameEngine,
  action: EmpiresQaPlayerCardAction,
): EmpiresActionResult {
  if (action.kind === 'play-card') return engine.playCard(action.cardId, action.attackIndex)
  if (action.kind === 'take-cards') return engine.takeCards('player')
  return engine.endAttack('player')
}

export type EmpiresQaExternalOfferPolicy =
  | 'accept-first'
  | 'decline-first'
  | { decision: 'accept' | 'decline', offerId: string, cityId?: string }

export function executeEmpiresQaExternalOfferPolicy(
  engine: EmpiresEndgameEngine,
  policy: EmpiresQaExternalOfferPolicy,
): EmpiresActionResult {
  const offers = [...engine.state.external.activeOffers]
    .sort((left, right) => left.id.localeCompare(right.id))
  const requestedId = typeof policy === 'string' ? offers[0]?.id : policy.offerId
  const offer = offers.find(candidate => candidate.id === requestedId)
  if (!offer) return { ok: false, message: 'QA offer policy could not find its stable offer ID.' }
  const decision = typeof policy === 'string'
    ? (policy === 'accept-first' ? 'accept' : 'decline')
    : policy.decision
  if (decision === 'decline') return engine.declineExternalOffer(offer.id)
  const configuredCityId = typeof policy === 'string' ? undefined : policy.cityId
  const cityId = configuredCityId ?? engine.state.empire.cities
    .map(city => city.id)
    .find(cityId => !engine.externalDiplomacyView(cityId).offers
      .find(candidate => candidate.id === offer.id)?.quote.blockedReason)
  return cityId
    ? engine.acceptExternalOffer(offer.id, cityId)
    : { ok: false, message: 'QA offer policy found no eligible trade city.' }
}

export type EmpiresQaDialoguePolicy =
  | 'first-legal'
  | { questId: string, choiceId: string }

export function executeEmpiresQaDialoguePolicy(
  engine: EmpiresEndgameEngine,
  policy: EmpiresQaDialoguePolicy,
): EmpiresActionResult {
  const questId = typeof policy === 'string'
    ? engine.state.questRuntime.activeMandatoryQuestId
    : policy.questId
  if (!questId) return { ok: false, message: 'QA dialogue policy found no active mandatory quest.' }
  const quest = engine.state.quests[questId]
  if (quest?.status === 'completed' || quest?.status === 'failed') {
    return engine.dismissDialogue(questId)
  }
  const definition = engine.config.quests.definitions.find(item => item.id === questId)
  const node = definition && quest ? questCurrentNode(definition, quest) : null
  const choiceId = typeof policy === 'string'
    ? node?.choices.find(choice => !engine.questChoiceBlockedReason(questId, choice.id))?.id
    : policy.choiceId
  return choiceId
    ? engine.advanceDialogue(questId, choiceId)
    : { ok: false, message: `QA dialogue policy found no legal choice for ${questId}.` }
}

export function digestEmpiresQaState(engine: EmpiresEndgameEngine): EmpiresQaStateDigest {
  const activeMandatoryQuestId = engine.state.questRuntime.activeMandatoryQuestId
  const activeQuest = activeMandatoryQuestId ? engine.state.quests[activeMandatoryQuestId] : null
  return {
    phase: engine.state.phase,
    revision: engine.state.revision,
    con: engine.state.con,
    boutsInCon: engine.state.boutsInCon,
    currentActor: engine.currentActor(),
    stage: engine.state.durak.stage,
    deckCount: engine.state.durak.deck.length,
    playerHandCount: engine.state.durak.playerHand.length,
    godHandCount: engine.state.durak.godHand.length,
    tableAttackCount: engine.state.durak.table.length,
    undefendedAttackCount: engine.state.durak.table.filter(pair => !pair.defenseCardId).length,
    giftChoiceCount: engine.state.giftChoiceIds.length,
    pendingResolutionKind: engine.state.pendingResolution?.kind ?? null,
    pendingTargetCount: engine.state.pendingResolution?.eligibleTargetIds.length ?? 0,
    destroyedRegionCount: engine.state.empire.destroyedRegionIds.length,
    rebelliousRegionCount: Object.values(engine.state.empire.loyalty.regions)
      .filter(region => region.status === 'rebellious').length,
    reputation: engine.state.empire.reputation,
    chronicleCount: engine.state.empire.chronicle.length,
    chronicleLastId: engine.state.empire.chronicle.at(-1)?.id ?? null,
    daysRemaining: engine.state.empire.daysRemaining,
    eventId: engine.state.event?.eventId ?? null,
    minigameId: engine.state.minigame?.id ?? null,
    minigameKind: engine.state.minigame?.kind ?? null,
    minigameAttempt: engine.state.minigame?.attempt ?? null,
    minigameMode: engine.state.minigame?.kind === 'td' ? engine.state.minigame.plan.mode : null,
    minigameRegionId: engine.state.minigame?.kind === 'td'
      ? engine.state.minigame.plan.battlefield.regionId
      : null,
    minigameRulesSchemaVersion: engine.state.minigame?.rulesIdentity.configSchemaVersion ?? null,
    minigameRulesDigest: engine.state.minigame?.rulesIdentity.rulesDigest ?? null,
    minigameCommandLimit: engine.state.minigame?.plan.maxCommands ?? null,
    minigameResultCount: engine.state.minigameResultLog.length,
    minigameResultLimit: Math.max(1, engine.config.td.resultLogLimit ?? 32),
    minigameResultEvictedCount: engine.state.minigameResultCompaction.evictedCount,
    minigameResultHistoryDigest: engine.state.minigameResultCompaction.historyDigest,
    minigameResultLastSessionId: engine.state.minigameResultCompaction.lastSessionId,
    minigameResultLastRulesDigest: engine.state.minigameResultCompaction.lastRulesDigest,
    rngDraws: engine.state.rng.draws,
    cosmeticRngDraws: engine.state.god.cosmeticRng.draws,
    deckMemoryInspectionsUsed: engine.state.durak.deckMemoryInspectionsUsed,
    godInterventionCount: engine.state.durak.godInterventions,
    godInterventionHistoryCount: engine.state.god.interventions.length,
    godInterventionHistoryDigest: engine.state.god.interventionCompaction.historyDigest,
    godDialogueCount: engine.state.god.dialogueLog.length,
    godDialogueOrder: engine.state.god.dialogueLog.map(entry => `${entry.sequence}:${entry.lineId}`),
    godDialogueHistoryDigest: engine.state.god.dialogueCompaction.historyDigest,
    outcomeReason: engine.state.outcomeReason,
    seasonId: engine.currentSeasonView()?.id ?? null,
    technologyDisclosureCount: engine.state.empire.chronicle
      .filter(entry => entry.kind === 'technology-disclosure').length,
    activeAdvisorCount: Object.values(engine.state.governance.advisors)
      .filter(advisor => advisor.status === 'active').length,
    governorAssignmentCount: Object.keys(engine.state.governance.governorAssignments).length,
    activeEpidemicCount: engine.state.epidemics.filter(epidemic => epidemic.endedAtCon === null).length,
    activeLoanCount: engine.state.empire.domesticEconomy.loans
      .filter(loan => loan.status === 'active' || loan.status === 'defaulted').length,
    insuranceContractCount: engine.state.empire.domesticEconomy.insuranceContracts.length,
    activeFairActivityCount: engine.state.empire.domesticEconomy.fair.activeActivities
      .filter(activity => activity.expiresAfterCon >= engine.state.con).length,
    activeExternalOfferCount: engine.state.external.activeOffers.length,
    externalOfferHistoryCount: engine.state.external.offerHistory.length,
    economyEventHistoryCount: engine.state.empire.economyContent.eventHistory.length,
    smugglingPolicyActive: engine.state.empire.economyContent.smugglingPolicy !== null,
    horsePactActive: engine.state.empire.economyContent.horseTheft.pact !== null,
    activeMandatoryQuestId,
    activeQuestStatus: activeQuest?.status ?? null,
    activeQuestNodeId: activeQuest?.nodeId ?? null,
    questHistoryCount: engine.state.questRuntime.history.length,
    questStateDigest: JSON.stringify(Object.values(engine.state.quests)
      .sort((left, right) => left.questId.localeCompare(right.questId))
      .map(quest => ({
        id: quest.questId,
        status: quest.status,
        stage: quest.stageId,
        node: quest.nodeId,
        run: quest.run,
        memory: Object.fromEntries(Object.entries(quest.memory)
          .sort(([left], [right]) => left.localeCompare(right))),
      }))),
  }
}

export function inspectEmpiresQaDeck(engine: EmpiresEndgameEngine): EmpiresQaDeckInspection {
  const bottomCardId = engine.state.durak.deck[0] ?? null
  const nextDrawCardId = engine.state.durak.deck[engine.state.durak.deck.length - 1] ?? null
  const trumpSourceCardId = engine.config.durak.fixedTrumpSuit
    ? null
    : engine.state.durak.deck.find((cardId) => {
      const definition = engine.getDefinition(cardId)
      const restricted = engine.config.governance.enabled
        && definition.suit === engine.config.governance.trump.restrictedSuit
        && engine.state.governance.advisors[engine.config.governance.trump.grandAdvisorId]?.status !== 'active'
      return definition.rank !== 'joker' && !restricted
    }) ?? null
  const expectedTrumpSuit = engine.config.durak.fixedTrumpSuit
    ?? (trumpSourceCardId
      ? engine.getDefinition(trumpSourceCardId).suit
      : engine.config.durak.joker.trumpFallbackSuit)
  return {
    bottomCardId,
    nextDrawCardId,
    trumpSourceCardId,
    configuredTrumpSuit: engine.config.durak.fixedTrumpSuit ?? null,
    expectedTrumpSuit,
    actualTrumpSuit: engine.state.durak.trumpSuit,
    ok: expectedTrumpSuit === engine.state.durak.trumpSuit,
  }
}

export function validateEmpiresQaSnapshot(
  config: EmpiresEndgameConfig,
  snapshot: EmpiresCampaignState,
  scenarioName?: EmpiresQaScenarioName,
): EmpiresQaSnapshotValidation {
  const issues: EmpiresQaValidationIssue[] = []
  const add = (code: string, message: string) => issues.push({ code, message })
  let restoredEngine: EmpiresEndgameEngine | null = null
  try {
    restoredEngine = new EmpiresEndgameEngine(config, snapshot)
  }
  catch (error) {
    add('snapshot-rejected', error instanceof Error ? error.message : String(error))
  }

  if (snapshot.configId !== config.id) add('config-id', 'Snapshot configId does not match the config.')
  const expectedIds = new Set(config.cards.map(card => card.id))
  const instanceIds = Object.keys(snapshot.cards)
  if (instanceIds.length !== expectedIds.size || instanceIds.some(cardId => !expectedIds.has(cardId))) {
    add('card-instances', 'Snapshot card instances do not match the configured deck.')
  }
  const located = allLocatedCardIds(snapshot)
  const counts = new Map<string, number>()
  for (const cardId of located) counts.set(cardId, (counts.get(cardId) ?? 0) + 1)
  const missing = [...expectedIds].filter(cardId => !counts.has(cardId))
  const duplicated = [...counts].filter(([, count]) => count > 1).map(([cardId]) => cardId)
  const unknown = [...counts.keys()].filter(cardId => !expectedIds.has(cardId))
  if (missing.length > 0) add('cards-missing', `Cards missing from all locations: ${missing.join(', ')}.`)
  if (duplicated.length > 0) add('cards-duplicated', `Cards in multiple locations: ${duplicated.join(', ')}.`)
  if (unknown.length > 0) add('cards-unknown', `Unknown cards in locations: ${unknown.join(', ')}.`)

  if (snapshot.phase === 'divineGift') {
    const giftIds = new Set(config.gifts.definitions.map(gift => gift.id))
    if (!snapshot.pendingResolution && snapshot.giftChoiceIds.length !== config.gifts.choiceCount) {
      add('gift-count', 'Divine gift snapshot does not have the configured number of choices.')
    }
    if (snapshot.giftChoiceIds.some(giftId => !giftIds.has(giftId))) {
      add('gift-unknown', 'Divine gift snapshot contains an unknown gift.')
    }
    if (snapshot.pendingResolution) {
      if (!giftIds.has(snapshot.pendingResolution.giftId)) {
        add('pending-gift-unknown', 'Pending target resolution references an unknown gift.')
      }
      if (snapshot.pendingResolution.eligibleTargetIds.length === 0) {
        add('pending-target-empty', 'Pending target resolution has no eligible city.')
      }
    }
  }
  if (snapshot.phase === 'event') {
    const event = config.empire.events.find(item => item.id === snapshot.event?.eventId)
    if (!event) add('event-unknown', 'Event snapshot does not reference a configured event.')
    else if (event.deferredReason) add('event-deferred', 'Pending event is marked as future content.')
    else if (!event.choices.some(choice => !choice.deferredReason)) {
      add('event-empty', 'Pending event has no implemented choices.')
    }
  }
  if (snapshot.phase === 'minigame' && !snapshot.minigame) {
    add('minigame-missing', 'Minigame phase does not contain an active session.')
  }
  if ((snapshot.phase === 'victory' || snapshot.phase === 'defeat') && !snapshot.outcomeReason) {
    add('outcome-reason', 'Terminal snapshots must include an outcome reason.')
  }

  if (scenarioName && restoredEngine) {
    const engine = restoredEngine
    if (scenarioName === 'pending-take') {
      if (snapshot.phase !== 'cards' || snapshot.durak.stage !== 'taking') {
        add('pending-take-stage', 'Pending-take scenario must be in the card taking stage.')
      }
      if (engine.legalAttackCardIds('player').length === 0) {
        add('pending-take-throw-in', 'Pending-take scenario must expose a legal player throw-in.')
      }
    } else if (scenarioName === 'empty-hand-pending-finish') {
      if (snapshot.durak.playerHand.length !== 0) {
        add('empty-hand', 'Empty-hand scenario still contains a player card.')
      }
      if (!engine.canEndAttack('player')) {
        add('pending-finish', 'Empty-hand scenario must expose the end-attack action.')
      }
    } else if (scenarioName === 'anti-bito') {
      if (snapshot.phase !== 'cards' || snapshot.durak.deck.length !== 0
        || !engine.canEndAttack('player')) {
        add('anti-bito-winner-path', 'Anti-bito scenario must expose an empty-deck winner path.')
      }
      const excluded = new Set(config.god.antiBito.excludedDefinitionIds)
      const eligibleDiscards = snapshot.durak.discard.filter(cardId => (
        !excluded.has(snapshot.cards[cardId].definitionId)
      ))
      if (eligibleDiscards.length < config.god.antiBito.returnCount) {
        add('anti-bito-discard', 'Anti-bito scenario needs the configured number of eligible discards.')
      }
    } else if (scenarioName === 'divine-gift' && snapshot.phase !== 'divineGift') {
      add('phase', 'Divine-gift scenario has the wrong phase.')
    } else if (scenarioName === 'target-city-resources') {
      if (snapshot.phase !== 'divineGift' || snapshot.pendingResolution?.kind !== 'cityResources') {
        add('pending-resolution', 'City-resource scenario must wait for a city target.')
      }
    } else if (scenarioName === 'target-meteor-city') {
      if (snapshot.phase !== 'divineGift' || snapshot.pendingResolution?.kind !== 'meteorCity') {
        add('pending-resolution', 'Meteor scenario must wait for a city target.')
      }
    } else if (scenarioName === 'empire-council-with-points') {
      if (snapshot.phase !== 'empire') add('phase', 'Empire council scenario has the wrong phase.')
      if (snapshot.upgradePoints <= 0) add('council-points', 'Empire council scenario needs upgrade points.')
    } else if (scenarioName === 'governance') {
      const unresolved = config.governance.advisors.filter(advisor => !advisor.grandAdvisor)
        .filter(advisor => snapshot.governance.advisors[advisor.id]?.status === 'awaiting-judgment')
      if (snapshot.phase !== 'empire') add('phase', 'Governance scenario has the wrong phase.')
      if (unresolved.length !== 3) add('advisor-judgment', 'Governance scenario must begin before advisor judgment.')
      if (Object.keys(snapshot.governance.governorAssignments).length !== 0) {
        add('perst-assignment', 'Governance scenario must begin before Perst assignment.')
      }
    } else if (scenarioName === 'expedition-planning') {
      const planning = engine.expeditionPlanningView('expedition-south-fortress')
      if (snapshot.phase !== 'empire' || !planning
        || planning.roster.filter(unit => unit.eligible).length < 2
        || planning.provisionAvailable < planning.provisionRequired) {
        add('expedition-planning', 'Expedition scenario must expose a funded canonical southern roster.')
      }
    } else if (scenarioName === 'domestic-economy') {
      const economy = snapshot.empire.domesticEconomy
      const operationalCarriers = [
        config.empire.domesticEconomy.loan.bankBuildingId,
        config.empire.domesticEconomy.insurance.buildingId,
        config.empire.domesticEconomy.fair.buildingId,
        config.empire.domesticEconomy.temple.buildingId,
        config.empire.domesticEconomy.tavern.buildingId,
      ].filter(buildingId => snapshot.empire.cities.some(city => (
        (city.operationalBuildingLevels[buildingId] ?? 0) > 0
      )))
      if (snapshot.phase !== 'empire') add('phase', 'Domestic-economy scenario has the wrong phase.')
      if (operationalCarriers.length !== 5) {
        add('economy-carriers', `Domestic-economy carriers are incomplete: ${operationalCarriers.join(', ') || 'none'}.`)
      }
      if (!economy.loans.some(loan => loan.status === 'active')) {
        add('economy-loan', 'Domestic-economy scenario must contain an active scheduled loan.')
      }
    } else if (scenarioName === 'external-trade') {
      if (snapshot.phase !== 'empire') add('phase', 'External-trade scenario has the wrong phase.')
      if (snapshot.external.activeOffers.length < 2) {
        add('external-offers', 'External-trade scenario must contain at least two serialized offers.')
      }
      if (!config.empire.externalEconomy.actors.some(actor => actor.id === 'actor-louis')) {
        add('external-louis', 'External-trade scenario requires the authored Людовик actor.')
      }
    } else if (scenarioName === 'economy-content-event') {
      const content = config.empire.economyContent
      if (snapshot.phase !== 'event' || snapshot.event?.eventId !== content.smuggling.eventId) {
        add('phase', 'Economy-content scenario must expose the Customs smuggling event.')
      }
      if (!snapshot.event?.targetCityId
        || snapshot.external.customs.lastTradeCityId !== snapshot.event.targetCityId) {
        add('economy-event-target', 'Economy-content scenario must retain the traded Customs city.')
      }
    } else if (scenarioName === 'quest-dialogue') {
      const questId = snapshot.questRuntime.activeMandatoryQuestId
      const quest = questId ? snapshot.quests[questId] : null
      if (questId !== 'quest-palach' || quest?.status !== 'active') {
        add('quest-dialogue', 'Quest-dialogue scenario must expose the active mandatory Палач quest.')
      }
      if (!quest || engine.questChoiceBlockedReason(questId, 'palach-p28-bed') !== null) {
        add('quest-choice', 'Quest-dialogue scenario must expose a legal stable-ID opening choice.')
      }
    } else if (scenarioName === 'destroyed-west') {
      if (snapshot.phase !== 'empire' || !snapshot.empire.destroyedRegionIds.includes('west')) {
        add('destroyed-region', 'Destroyed-west scenario must make the west inaccessible.')
      }
    } else if (scenarioName === 'loyalty-rebellion') {
      const north = snapshot.empire.loyalty.regions.north
      const west = snapshot.empire.loyalty.regions.west
      if (snapshot.phase !== 'empire') add('phase', 'Loyalty scenario has the wrong phase.')
      if (north?.status !== 'controlled' || !snapshot.empire.chronicle.some(entry => entry.kind === 'recovery')) {
        add('loyalty-recovery', 'Loyalty scenario must restore northern control after reload.')
      }
      if (west?.status !== 'rebellious' || snapshot.empire.destroyedRegionIds.includes('west')) {
        add('loyalty-rebellion', 'Loyalty scenario must show a non-destroyed western rebellion.')
      }
      if (!snapshot.empire.chronicle.some(entry => entry.kind === 'battle-loss')) {
        add('battle-loss', 'Loyalty scenario must retain the consumed battle loss.')
      }
      if (snapshot.empire.chronicle.length > config.empire.loyalty.chronicleRetention) {
        add('chronicle-retention', 'Loyalty scenario exceeds configured chronicle retention.')
      }
    } else if (scenarioName === 'relic-production-levels') {
      if (snapshot.phase !== 'empire'
        || (snapshot.empire.buildingLevelBonuses.farm ?? 0) < 1
        || (snapshot.empire.buildingLevelBonuses.lumber ?? 0) < 1) {
        add('building-level-bonus', 'Relic scenario must increase farm and lumber levels.')
      }
    } else if (scenarioName === 'season-disclosure') {
      const side = snapshot.empire.technologySides['reform-city-gates']
      if (snapshot.con !== 2 || engine.currentSeasonView()?.id !== 'winter') {
        add('season-boundary', 'Season-disclosure scenario must cross from summer into winter.')
      }
      if (side?.revealedAtCon !== 2 || side.effectsAppliedAtCon !== 2) {
        add('technology-disclosure', 'Season-disclosure scenario must reveal and apply its side in con 2.')
      }
      if (snapshot.empire.chronicle.filter(entry => entry.kind === 'season').length !== 1
        || snapshot.empire.chronicle.filter(entry => entry.kind === 'technology-disclosure').length !== 1) {
        add('chronicle-exact-once', 'Season and disclosure chronicle entries must each occur exactly once.')
      }
    } else if (scenarioName === 'epidemic-outbreak') {
      const active = snapshot.epidemics.filter(epidemic => epidemic.endedAtCon === null)
      if (snapshot.phase !== 'empire' || active.length !== 1) {
        add('epidemic', 'Epidemic scenario must contain one live outbreak in the empire phase.')
      } else if (restoredEngine.cityEpidemicViews(active[0].cityId).length !== 1) {
        add('epidemic-view', 'Epidemic scenario must expose its restored city projection.')
      }
    } else if (scenarioName === 'mystic-tavern') {
      if (snapshot.phase !== 'minigame' || snapshot.minigame?.kind !== 'tavern') {
        add('tavern-minigame', 'Tavern scenario must contain an active Tavern session.')
      } else if (snapshot.minigame.plan.sections.join(',') !== 'tables,bar') {
        add('tavern-sections', 'Tavern scenario must preserve the authored tables/bar order.')
      }
    } else if (scenarioName === 'alchemy-experiment') {
      if (snapshot.phase !== 'minigame' || snapshot.minigame?.kind !== 'alchemy') {
        add('alchemy-minigame', 'Alchemy scenario must contain an active laboratory session.')
      } else if (snapshot.minigame.plan.recipe.deferredReason
        || snapshot.minigame.plan.rulesIdentity.rulesDigest !== snapshot.minigame.rulesIdentity.rulesDigest) {
        add('alchemy-rules', 'Alchemy scenario must carry a live recipe and matching immutable rules identity.')
      }
    } else if (scenarioName in TD_QA_VARIANTS) {
      const expected = TD_QA_VARIANTS[scenarioName as keyof typeof TD_QA_VARIANTS]
      const expectedRules = qaRulesIdentity(config)
      if (snapshot.phase !== 'minigame' || snapshot.minigame?.kind !== 'td') {
        add('minigame', `${scenarioName} scenario must contain an active TD session.`)
      } else {
        const session = snapshot.minigame
        if (session.plan.mode !== expected.expectedMode
          || session.plan.battlefield.regionId !== expected.expectedRegionId) {
          add('battle-variant', `${scenarioName} scenario has the wrong TD mode or region.`)
        }
        if (session.rulesIdentity.rulesDigest !== expectedRules.rulesDigest
          || session.rulesIdentity.configSchemaVersion !== expectedRules.configSchemaVersion
          || session.plan.rulesIdentity.rulesDigest !== session.rulesIdentity.rulesDigest) {
          add('battle-rules', `${scenarioName} scenario does not carry the current TD rules identity.`)
        }
        if (session.plan.maxCommands !== config.td.maxCommands) {
          add('battle-command-cap', `${scenarioName} scenario does not carry the configured command cap.`)
        }
      }
    } else if (scenarioName === 'event' && snapshot.phase !== 'event') {
      add('phase', 'Event scenario has the wrong phase.')
    } else if ((scenarioName === 'victory' || scenarioName === 'defeat') && snapshot.phase !== scenarioName) {
      add('phase', `${scenarioName} scenario has the wrong phase.`)
    }
    const playerTurn = checkEmpiresQaPlayerTurnInvariant(engine)
    if (!playerTurn.ok) add('player-turn-action', playerTurn.message)
  }

  return { ok: issues.length === 0, issues }
}

export function createEmpiresQaScenarios(
  config: EmpiresEndgameConfig,
  options: EmpiresQaScenarioOptions = {},
): Record<EmpiresQaScenarioName, EmpiresQaScenarioFixture> {
  const seed = options.seed ?? config.seed
  const seededConfig = configWithSeed(config, seed)
  const baseEngine = new EmpiresEndgameEngine(seededConfig)
  const pendingTake = createPendingTakeSnapshot(baseEngine, false)
  const emptyHandPendingFinish = createPendingTakeSnapshot(baseEngine, true)
  const antiBito = createAntiBitoSnapshot(baseEngine)
  const divineGift = createGiftSnapshot(baseEngine)
  const targetCityResources = createTargetedGiftSnapshot(seededConfig, divineGift, 'cityResources')
  const targetMeteorCity = createTargetedGiftSnapshot(seededConfig, divineGift, 'meteorCity')
  const empireCouncil = createEmpireSnapshot(seededConfig, divineGift)
  const expeditionPlanning = createExpeditionPlanningSnapshot(seededConfig, empireCouncil)
  const destroyedWest = createDestroyedRegionSnapshot(seededConfig, empireCouncil, 'west')
  const loyaltyRebellion = createLoyaltyRebellionSnapshot(seededConfig, empireCouncil)
  const relicProductionLevels = createRelicBuildingLevelSnapshot(seededConfig, empireCouncil)
  const seasonDisclosure = createSeasonDisclosureSnapshot(seededConfig, empireCouncil)
  const epidemicOutbreak = createEpidemicOutbreakSnapshot(seededConfig, empireCouncil)
  const domesticEconomy = createDomesticEconomySnapshot(seededConfig, empireCouncil)
  const mysticTavern = createTavernSnapshot(seededConfig, empireCouncil)
  const alchemyExperiment = createAlchemySnapshot(seededConfig, empireCouncil)
  const externalTrade = createExternalTradeSnapshot(seededConfig, empireCouncil)
  const economyContentEvent = createEconomyContentEventSnapshot(seededConfig, externalTrade)
  const questDialogue = createQuestDialogueSnapshot(seededConfig, empireCouncil)
  const event = createEventSnapshot(seededConfig, empireCouncil)
  const battleDefense = createBattleSnapshot(seededConfig, baseEngine, 'battle-defense')
  const battleAssault = createBattleSnapshot(seededConfig, baseEngine, 'battle-assault')
  const battleSwamp = createBattleSnapshot(seededConfig, baseEngine, 'battle-swamp')
  const battleForest = createBattleSnapshot(seededConfig, baseEngine, 'battle-forest')
  const battleNorth = createBattleSnapshot(seededConfig, baseEngine, 'battle-north')
  const battleDesert = createBattleSnapshot(seededConfig, baseEngine, 'battle-desert')
  const snapshots: Record<EmpiresQaScenarioName, EmpiresCampaignState> = {
    'pending-take': pendingTake,
    'empty-hand-pending-finish': emptyHandPendingFinish,
    'anti-bito': antiBito,
    'divine-gift': divineGift,
    'target-city-resources': targetCityResources,
    'target-meteor-city': targetMeteorCity,
    'empire-council-with-points': empireCouncil,
    'expedition-planning': expeditionPlanning,
    governance: cloneJson(empireCouncil),
    'domestic-economy': domesticEconomy,
    'mystic-tavern': mysticTavern,
    'alchemy-experiment': alchemyExperiment,
    'external-trade': externalTrade,
    'economy-content-event': economyContentEvent,
    'quest-dialogue': questDialogue,
    'destroyed-west': destroyedWest,
    'loyalty-rebellion': loyaltyRebellion,
    'relic-production-levels': relicProductionLevels,
    'season-disclosure': seasonDisclosure,
    'epidemic-outbreak': epidemicOutbreak,
    'battle-defense': battleDefense,
    'battle-assault': battleAssault,
    'battle-swamp': battleSwamp,
    'battle-forest': battleForest,
    'battle-north': battleNorth,
    'battle-desert': battleDesert,
    event,
    victory: createOutcomeSnapshot(baseEngine, 'victory'),
    defeat: createOutcomeSnapshot(baseEngine, 'defeat'),
  }

  return Object.fromEntries(EMPIRES_QA_SCENARIO_NAMES.map((name) => {
    const snapshot = snapshots[name]
    const validation = validateEmpiresQaSnapshot(seededConfig, snapshot, name)
    if (!validation.ok) {
      throw new Error(`Invalid Empire's Endgame QA scenario ${name}: ${validation.issues
        .map(issue => `${issue.code}: ${issue.message}`)
        .join('; ')}`)
    }
    return [name, {
      name,
      ...SCENARIO_COPY[name],
      seed,
      snapshot: cloneJson(snapshot),
      validation,
    }]
  })) as Record<EmpiresQaScenarioName, EmpiresQaScenarioFixture>
}

function emptyPhaseVisits(): Record<EmpiresPhase, number> {
  return {
    cards: 0,
    divineGift: 0,
    empire: 0,
    event: 0,
    minigame: 0,
    victory: 0,
    defeat: 0,
  }
}

function chooseAutoplayAction(
  engine: EmpiresEndgameEngine,
  tdPolicy: TdQaPolicy,
): { action: EmpiresQaAction | null, stall: Omit<EmpiresQaStallDiagnostic, 'at'> | null, checkedPlayerTurn: boolean } {
  const mandatoryQuestId = engine.state.questRuntime.activeMandatoryQuestId
  if (mandatoryQuestId) {
    const quest = engine.state.quests[mandatoryQuestId]
    if (quest?.status === 'completed' || quest?.status === 'failed') {
      return {
        action: { kind: 'dismiss-dialogue', questId: mandatoryQuestId },
        stall: null,
        checkedPlayerTurn: false,
      }
    }
    const definition = engine.config.quests.definitions.find(item => item.id === mandatoryQuestId)
    const node = definition && quest ? questCurrentNode(definition, quest) : null
    const choice = node?.choices.find(item => !engine.questChoiceBlockedReason(mandatoryQuestId, item.id))
    return choice
      ? {
          action: { kind: 'advance-dialogue', questId: mandatoryQuestId, choiceId: choice.id },
          stall: null,
          checkedPlayerTurn: false,
        }
      : {
          action: null,
          stall: {
            code: 'no-dialogue-choice',
            message: `Mandatory quest ${mandatoryQuestId} has no legal dialogue choice.`,
            availablePlayerActions: [],
          },
          checkedPlayerTurn: false,
        }
  }
  if (engine.state.phase === 'cards') {
    if (engine.currentActor() === 'god') {
      return { action: { kind: 'advance-god' }, stall: null, checkedPlayerTurn: false }
    }
    const check = checkEmpiresQaPlayerTurnInvariant(engine)
    if (!check.ok) {
      return {
        action: null,
        stall: {
          code: 'no-player-action',
          message: check.message,
          availablePlayerActions: check.actions,
        },
        checkedPlayerTurn: true,
      }
    }
    const play = check.actions.find(action => action.kind === 'play-card')
    const fallback = engine.state.durak.stage === 'defense'
      ? check.actions.find(action => action.kind === 'take-cards')
      : check.actions.find(action => action.kind === 'end-attack')
    return {
      action: play ?? fallback ?? check.actions[0] ?? null,
      stall: null,
      checkedPlayerTurn: true,
    }
  }
  if (engine.state.phase === 'divineGift') {
    const pending = engine.state.pendingResolution
    if (pending) {
      const targetId = pending.eligibleTargetIds.find(id => engine.isCityAccessible(id))
      return targetId
        ? { action: { kind: 'resolve-target', targetId }, stall: null, checkedPlayerTurn: false }
        : {
            action: null,
            stall: {
              code: 'no-pending-target',
              message: `Pending ${pending.kind} resolution has no accessible city target.`,
              availablePlayerActions: [],
            },
            checkedPlayerTurn: false,
          }
    }
    const giftId = engine.state.giftChoiceIds[0]
    return giftId
      ? { action: { kind: 'choose-gift', giftId }, stall: null, checkedPlayerTurn: false }
      : {
          action: null,
          stall: {
            code: 'no-gift-choice',
            message: 'Divine gift phase has no selectable gift.',
            availablePlayerActions: [],
          },
          checkedPlayerTurn: false,
        }
  }
  if (engine.state.phase === 'empire') {
    return { action: { kind: 'finish-empire' }, stall: null, checkedPlayerTurn: false }
  }
  if (engine.state.phase === 'event') {
    const event = engine.config.empire.events.find(item => item.id === engine.state.event?.eventId)
    const choice = event?.choices.find(item => !engine.eventChoiceBlockedReason(item.id))
    return event && choice
      ? {
          action: { kind: 'choose-event', eventId: event.id, choiceId: choice.id },
          stall: null,
          checkedPlayerTurn: false,
        }
      : {
          action: null,
          stall: {
            code: 'no-event-choice',
            message: event
              ? `Event ${event.id} has no affordable choice.`
              : 'Event phase does not reference a configured event.',
            availablePlayerActions: [],
          },
          checkedPlayerTurn: false,
        }
  }
  if (engine.state.phase === 'minigame') {
    return engine.state.minigame
      ? {
          action: {
            kind: 'resolve-minigame',
            policy: engine.state.minigame.kind === 'tavern'
              ? 'tavern-fast'
              : engine.state.minigame.kind === 'alchemy'
                ? 'alchemy-greedy'
                : tdPolicy,
          },
          stall: null,
          checkedPlayerTurn: false,
        }
      : {
          action: null,
          stall: {
            code: 'no-minigame-session',
            message: 'Minigame phase has no active session.',
            availablePlayerActions: [],
          },
          checkedPlayerTurn: false,
        }
  }
  return {
    action: null,
    stall: {
      code: 'unsupported-phase',
      message: `No QA action is defined for phase ${engine.state.phase}.`,
      availablePlayerActions: [],
    },
    checkedPlayerTurn: false,
  }
}

function executeAutoplayAction(
  engine: EmpiresEndgameEngine,
  action: EmpiresQaAction,
): EmpiresActionResult {
  if (action.kind === 'play-card' || action.kind === 'take-cards' || action.kind === 'end-attack') {
    return executeEmpiresQaPlayerCardAction(engine, action)
  }
  if (action.kind === 'advance-god') return engine.advanceGod()
  if (action.kind === 'choose-gift') return engine.chooseGift(action.giftId)
  if (action.kind === 'resolve-target') return engine.resolvePendingTarget(action.targetId)
  if (action.kind === 'finish-empire') return engine.finishEmpire()
  if (action.kind === 'resolve-minigame') {
    const session = engine.state.minigame
    if (!session) return { ok: false, message: 'No minigame session is active.' }
    if (session.kind === 'tavern') {
      return engine.resolveMinigame(resolveTavern(session.plan, session.seed, [
        { turn: 1, kind: 'finish' },
      ]))
    }
    if (session.kind === 'alchemy') {
      return engine.resolveMinigame(resolveAlchemyWithPolicy(session.plan, session.seed, 'greedy'))
    }
    if (action.policy === 'tavern-fast' || action.policy === 'alchemy-greedy') {
      return { ok: false, message: 'The selected fast-resolve policy cannot settle a TD session.' }
    }
    return engine.resolveMinigame(resolveTdWithPolicy(session.plan, session.seed, action.policy))
  }
  if (action.kind === 'advance-dialogue') {
    return engine.advanceDialogue(action.questId, action.choiceId)
  }
  if (action.kind === 'dismiss-dialogue') return engine.dismissDialogue(action.questId)
  return engine.chooseEvent(action.choiceId)
}

function makeStall(
  engine: EmpiresEndgameEngine,
  code: EmpiresQaStallCode,
  message: string,
  availablePlayerActions: EmpiresQaPlayerCardAction[] = [],
): EmpiresQaStallDiagnostic {
  return { code, message, at: digestEmpiresQaState(engine), availablePlayerActions }
}

function createAutoplayStartSnapshot(config: EmpiresEndgameConfig): EmpiresCampaignState {
  const bootstrap = new EmpiresEndgameEngine(config)
  const snapshot = bootstrap.snapshot()
  const unit = [...(config.empire.units ?? [])]
    .reverse()
    .find(definition => !definition.deferredReason && definition.td)
  const city = snapshot.empire.cities.find(candidate => candidate.id === 'city-tetrakor-capital')
    ?? snapshot.empire.cities[0]
  if (unit && city) {
    // A stable QA-only army guarantees the scheduled central assault remains
    // reachable after settling the preceding central defense.
    const weapon = config.combat.equipment.find(candidate => (
      candidate.id === unit.td?.weaponEquipmentId
      && candidate.kind === 'weapon'
      && !candidate.deferredReason
    ))
    const armor = unit.td?.armorEquipmentId
      ? config.combat.equipment.find(candidate => (
          candidate.id === unit.td?.armorEquipmentId
          && candidate.kind !== 'weapon'
          && !candidate.deferredReason
        ))
      : null
    if (weapon?.kind === 'weapon') {
      const cohortId = `qa-autoplay:${city.id}:${unit.id}:default`
      city.recruitedUnitCohorts = city.recruitedUnitCohorts.filter(cohort => cohort.id !== cohortId)
      city.recruitedUnitCohorts.push({
        id: cohortId,
        unitId: unit.id,
        loadoutId: 'qa-default',
        count: 24,
        weaponEquipmentId: weapon.id,
        ...(armor ? { defenseEquipmentId: armor.id } : {}),
        weapon: cloneJson(weapon.profile as CombatWeaponProfile),
        armor: armor && armor.kind !== 'weapon'
          ? cloneJson(armor.profile as CombatArmorProfile)
          : null,
      })
    }
  }
  return snapshot
}

export function runEmpiresQaAutoplay(
  config: EmpiresEndgameConfig,
  options: EmpiresQaAutoplayOptions = {},
): EmpiresQaAutoplayResult {
  const seed = options.seed ?? config.seed
  const seededConfig = configWithSeed(config, seed)
  const startSnapshot = options.startSnapshot ?? createAutoplayStartSnapshot(seededConfig)
  const engine = new EmpiresEndgameEngine(seededConfig, startSnapshot)
  const maxSteps = options.maxSteps ?? 10_000
  const tdPolicy = options.tdPolicy ?? 'balanced'
  const trace: EmpiresQaTraceEntry[] = []
  const phaseVisits = emptyPhaseVisits()
  const resolvedEventIds: string[] = []
  const resolvedMinigames: EmpiresQaAutoplayResult['resolvedMinigames'] = []
  let checkedPlayerTurns = 0
  let stall: EmpiresQaStallDiagnostic | null = null

  while (trace.length < maxSteps && engine.state.phase !== 'victory' && engine.state.phase !== 'defeat') {
    phaseVisits[engine.state.phase] += 1
    const before = digestEmpiresQaState(engine)
    const selected = chooseAutoplayAction(engine, tdPolicy)
    if (selected.checkedPlayerTurn) checkedPlayerTurns += 1
    if (!selected.action) {
      stall = selected.stall
        ? { ...selected.stall, at: before }
        : makeStall(engine, 'unsupported-phase', 'QA autoplay could not select an action.')
      break
    }
    const resolvingMinigame = selected.action.kind === 'resolve-minigame' && engine.state.minigame
      ? {
          sessionId: engine.state.minigame.id,
          kind: engine.state.minigame.kind,
          mode: engine.state.minigame.kind === 'td' ? engine.state.minigame.plan.mode : null,
          regionId: engine.state.minigame.kind === 'td'
            ? engine.state.minigame.plan.battlefield.regionId
            : null,
          rulesDigest: engine.state.minigame.rulesIdentity.rulesDigest,
        }
      : null
    const result = executeAutoplayAction(engine, selected.action)
    const after = digestEmpiresQaState(engine)
    trace.push({ step: trace.length + 1, action: selected.action, result, before, after })
    if (!result.ok) {
      stall = makeStall(engine, 'action-failed', result.message, listEmpiresQaPlayerCardActions(engine))
      break
    }
    if (selected.action.kind === 'choose-event') resolvedEventIds.push(selected.action.eventId)
    if (resolvingMinigame) resolvedMinigames.push(resolvingMinigame)
    if (after.revision === before.revision) {
      stall = makeStall(engine, 'state-not-advanced', `Successful ${selected.action.kind} did not advance revision.`)
      break
    }
  }

  const completed = engine.state.phase === 'victory' || engine.state.phase === 'defeat'
  if (completed) phaseVisits[engine.state.phase] += 1
  if (!completed && !stall && trace.length >= maxSteps) {
    stall = makeStall(engine, 'step-limit', `QA autoplay reached the ${maxSteps}-step limit.`)
  }
  return {
    completed,
    seed,
    steps: trace.length,
    checkedPlayerTurns,
    phaseVisits,
    resolvedEventIds,
    resolvedMinigames,
    trace,
    stall,
    snapshot: engine.snapshot(),
  }
}
