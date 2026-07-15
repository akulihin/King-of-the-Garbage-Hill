import { EmpiresEndgameEngine } from './engine'
import type {
  EmpiresActionResult,
  EmpiresActor,
  EmpiresBoutStage,
  EmpiresCampaignState,
  EmpiresEndgameConfig,
  EmpiresEventDefinition,
  EmpiresPhase,
} from './types'

export const EMPIRES_QA_SCENARIO_NAMES = [
  'pending-take',
  'empty-hand-pending-finish',
  'divine-gift',
  'empire-council-with-points',
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
  | { kind: 'finish-empire' }
  | { kind: 'choose-event', eventId: string, choiceId: string }

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
  daysRemaining: number
  eventId: string | null
  rngDraws: number
  outcomeReason: string | null
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
  | 'no-event-choice'
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
}

export interface EmpiresQaAutoplayResult {
  completed: boolean
  seed: string | number
  steps: number
  checkedPlayerTurns: number
  phaseVisits: Record<EmpiresPhase, number>
  resolvedEventIds: string[]
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
  'divine-gift': {
    title: 'Divine gift',
    description: 'Three deterministic gifts are ready for selection.',
  },
  'empire-council-with-points': {
    title: 'Empire and card council',
    description: 'The empire phase is active and the card council has points to spend.',
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
    (event.minimumCon ?? Number.NEGATIVE_INFINITY) <= con
    && (event.maximumCon ?? Number.POSITIVE_INFINITY) >= con
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

function createGiftSnapshot(engine: EmpiresEndgameEngine): EmpiresCampaignState {
  const state = engine.snapshot()
  const giftChoiceIds = engine.config.gifts.definitions
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

function createEmpireSnapshot(
  config: EmpiresEndgameConfig,
  giftSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const engine = new EmpiresEndgameEngine(config, giftSnapshot)
  const giftId = engine.state.giftChoiceIds[0]
  if (!giftId) throw new Error('QA empire scenario cannot start without a gift choice.')
  const result = engine.chooseGift(giftId)
  if (!result.ok || engine.state.phase !== 'empire') {
    throw new Error(`QA empire scenario could not start: ${result.message}`)
  }
  const state = engine.snapshot()
  state.upgradePoints = Math.max(3, state.upgradePoints)
  return state
}

function createEventSnapshot(
  config: EmpiresEndgameConfig,
  empireSnapshot: EmpiresCampaignState,
): EmpiresCampaignState {
  const state = cloneJson(empireSnapshot)
  const event = firstEligibleEvent(config, state.con)
  if (!event) throw new Error('QA event scenario requires an event eligible for the current con.')
  state.phase = 'event'
  state.event = { eventId: event.id }
  state.empire.daysRemaining = 0
  state.outcomeReason = null
  return state
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

export function digestEmpiresQaState(engine: EmpiresEndgameEngine): EmpiresQaStateDigest {
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
    daysRemaining: engine.state.empire.daysRemaining,
    eventId: engine.state.event?.eventId ?? null,
    rngDraws: engine.state.rng.draws,
    outcomeReason: engine.state.outcomeReason,
  }
}

export function inspectEmpiresQaDeck(engine: EmpiresEndgameEngine): EmpiresQaDeckInspection {
  const bottomCardId = engine.state.durak.deck[0] ?? null
  const nextDrawCardId = engine.state.durak.deck.at(-1) ?? null
  const trumpSourceCardId = engine.config.durak.fixedTrumpSuit
    ? null
    : engine.state.durak.deck.find((cardId) => {
      const definition = engine.getDefinition(cardId)
      return definition.rank !== 'joker'
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
    if (snapshot.giftChoiceIds.length !== config.gifts.choiceCount) {
      add('gift-count', 'Divine gift snapshot does not have the configured number of choices.')
    }
    if (snapshot.giftChoiceIds.some(giftId => !giftIds.has(giftId))) {
      add('gift-unknown', 'Divine gift snapshot contains an unknown gift.')
    }
  }
  if (snapshot.phase === 'event') {
    const event = config.empire.events.find(item => item.id === snapshot.event?.eventId)
    if (!event) add('event-unknown', 'Event snapshot does not reference a configured event.')
    else if (event.choices.length === 0) add('event-empty', 'Pending event has no choices.')
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
    } else if (scenarioName === 'divine-gift' && snapshot.phase !== 'divineGift') {
      add('phase', 'Divine-gift scenario has the wrong phase.')
    } else if (scenarioName === 'empire-council-with-points') {
      if (snapshot.phase !== 'empire') add('phase', 'Empire council scenario has the wrong phase.')
      if (snapshot.upgradePoints <= 0) add('council-points', 'Empire council scenario needs upgrade points.')
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
  const divineGift = createGiftSnapshot(baseEngine)
  const empireCouncil = createEmpireSnapshot(seededConfig, divineGift)
  const event = createEventSnapshot(seededConfig, empireCouncil)
  const snapshots: Record<EmpiresQaScenarioName, EmpiresCampaignState> = {
    'pending-take': pendingTake,
    'empty-hand-pending-finish': emptyHandPendingFinish,
    'divine-gift': divineGift,
    'empire-council-with-points': empireCouncil,
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
    victory: 0,
    defeat: 0,
  }
}

function eventChoiceIsAffordable(
  engine: EmpiresEndgameEngine,
  costs: Array<{ resourceId: string, amount: number }> = [],
): boolean {
  return costs.every(cost => (engine.state.empire.resources[cost.resourceId] ?? 0) >= cost.amount)
}

function chooseAutoplayAction(
  engine: EmpiresEndgameEngine,
): { action: EmpiresQaAction | null, stall: Omit<EmpiresQaStallDiagnostic, 'at'> | null, checkedPlayerTurn: boolean } {
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
    const choice = event?.choices.find(item => eventChoiceIsAffordable(engine, item.resourceCosts))
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
  if (action.kind === 'finish-empire') return engine.finishEmpire()
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

export function runEmpiresQaAutoplay(
  config: EmpiresEndgameConfig,
  options: EmpiresQaAutoplayOptions = {},
): EmpiresQaAutoplayResult {
  const seed = options.seed ?? config.seed
  const seededConfig = configWithSeed(config, seed)
  const engine = new EmpiresEndgameEngine(seededConfig, options.startSnapshot)
  const maxSteps = options.maxSteps ?? 10_000
  const trace: EmpiresQaTraceEntry[] = []
  const phaseVisits = emptyPhaseVisits()
  const resolvedEventIds: string[] = []
  let checkedPlayerTurns = 0
  let stall: EmpiresQaStallDiagnostic | null = null

  while (trace.length < maxSteps && engine.state.phase !== 'victory' && engine.state.phase !== 'defeat') {
    phaseVisits[engine.state.phase] += 1
    const before = digestEmpiresQaState(engine)
    const selected = chooseAutoplayAction(engine)
    if (selected.checkedPlayerTurn) checkedPlayerTurns += 1
    if (!selected.action) {
      stall = selected.stall
        ? { ...selected.stall, at: before }
        : makeStall(engine, 'unsupported-phase', 'QA autoplay could not select an action.')
      break
    }
    const result = executeAutoplayAction(engine, selected.action)
    const after = digestEmpiresQaState(engine)
    trace.push({ step: trace.length + 1, action: selected.action, result, before, after })
    if (!result.ok) {
      stall = makeStall(engine, 'action-failed', result.message, listEmpiresQaPlayerCardActions(engine))
      break
    }
    if (selected.action.kind === 'choose-event') resolvedEventIds.push(selected.action.eventId)
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
    trace,
    stall,
    snapshot: engine.snapshot(),
  }
}
