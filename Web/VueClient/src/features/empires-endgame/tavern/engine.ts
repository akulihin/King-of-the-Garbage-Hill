import { digestTdValue } from '../td/engine'
import { EMPIRES_STABILIZATION_BUDGETS } from '../stabilization'
import type {
  TavernCommand,
  TavernPlan,
  TavernReplayState,
  TavernResult,
  TavernRulesIdentity,
} from './types'

export function createTavernRulesIdentity(
  configSchemaVersion: number,
  rules: unknown,
): TavernRulesIdentity {
  return {
    configSchemaVersion,
    rulesDigest: digestTdValue(rules),
  }
}

export function validateTavernPlan(plan: TavernPlan): string[] {
  const errors: string[] = []
  if (!plan.id.trim() || !plan.sessionId.trim()) errors.push('Tavern plan IDs are required.')
  if (!plan.cityId.trim()) errors.push('Tavern plan cityId is required.')
  if (!Number.isInteger(plan.con) || plan.con < 1) errors.push('Tavern plan con must be positive.')
  if (!Number.isFinite(plan.goldAvailable) || plan.goldAvailable < 0) {
    errors.push('Tavern plan gold must be finite and non-negative.')
  }
  if (!Number.isInteger(plan.maxCommands) || plan.maxCommands < 1) {
    errors.push('Tavern plan maxCommands must be a positive integer.')
  }
  if (plan.maxCommands > EMPIRES_STABILIZATION_BUDGETS.maxCommands) {
    errors.push('Tavern plan maxCommands exceeds the shipped safety ceiling.')
  }
  if (plan.mercenaryOffers.length > EMPIRES_STABILIZATION_BUDGETS.maxTavernOffers) {
    errors.push('Tavern offer count exceeds the shipped safety ceiling.')
  }
  if (plan.sections.length !== 2
    || plan.sections[0] !== 'tables'
    || plan.sections[1] !== 'bar') {
    errors.push('Tavern plan sections must preserve the authored tables/bar order.')
  }
  const offerIds = new Set<string>()
  for (const offer of plan.mercenaryOffers) {
    if (!offer.id.trim() || offerIds.has(offer.id) || !offer.name.trim() || !offer.unitId.trim()
      || !Number.isInteger(offer.count) || offer.count < 1
      || !Number.isFinite(offer.goldCost) || offer.goldCost < 0) {
      errors.push(`Tavern mercenary offer ${offer.id || '<missing>'} is invalid.`)
    }
    offerIds.add(offer.id)
  }
  if (!Number.isFinite(plan.drinks.goldCost) || plan.drinks.goldCost < 0
    || !Number.isInteger(plan.drinks.readyAtCon) || plan.drinks.readyAtCon <= plan.con
    || !Number.isInteger(plan.drinks.expiresAfterCon)
    || plan.drinks.expiresAfterCon < plan.drinks.readyAtCon) {
    errors.push('Tavern drinks plan is invalid.')
  }
  if (!Number.isFinite(plan.rumor.goldCost) || plan.rumor.goldCost < 0 || !plan.rumor.text.trim()) {
    errors.push('Tavern rumor plan is invalid.')
  }
  if (plan.rumor.deckHint && (
    !Number.isInteger(plan.rumor.deckHint.position) || plan.rumor.deckHint.position < 1
    || !plan.rumor.deckHint.suit.trim() || !plan.rumor.deckHint.rank.trim()
  )) errors.push('Tavern rumor deck hint is invalid.')
  if (plan.maria.present && !plan.maria.title.trim()) errors.push('Tavern Maria title is required.')
  if (!Number.isInteger(plan.maria.roundsToWin) || plan.maria.roundsToWin < 1
    || plan.maria.playerRoundWins.length !== plan.maria.roundsToWin * 2 - 1) {
    errors.push('Tavern Maria match must freeze a complete odd best-of series.')
  }
  if (!plan.rulesIdentity.rulesDigest.trim()) errors.push('Tavern rules identity is required.')
  return errors
}

export function createTavernReplayState(): TavernReplayState {
  return {
    turn: 0,
    commandLog: [],
    hiredOfferId: null,
    drinksPurchased: false,
    rumorPurchased: false,
    mariaPlayed: false,
    mariaVictory: false,
    goldSpent: 0,
    finished: false,
    error: null,
  }
}

export function applyTavernCommand(
  plan: TavernPlan,
  current: TavernReplayState,
  command: TavernCommand,
): TavernReplayState {
  // Vue exposes component state as proxies, which the browser structured-clone
  // algorithm rejects. Tavern replay state is deliberately flat apart from the
  // immutable command log, so copy it explicitly at this boundary.
  const state: TavernReplayState = {
    ...current,
    commandLog: current.commandLog.map(entry => ({ ...entry })),
  }
  if (state.finished || state.error) return { ...state, error: state.error ?? 'Tavern session is finished.' }
  if (state.commandLog.length >= plan.maxCommands) return { ...state, error: 'Tavern command limit reached.' }
  if (command.turn !== state.turn + 1) return { ...state, error: 'Tavern command turn is not sequential.' }

  const spend = (amount: number): boolean => {
    if (state.goldSpent + amount > plan.goldAvailable + Number.EPSILON) {
      state.error = 'Not enough gold remains in the Tavern plan.'
      return false
    }
    state.goldSpent += amount
    return true
  }

  if (command.kind === 'hire') {
    const offer = plan.mercenaryOffers.find(candidate => candidate.id === command.offerId)
    if (!offer) state.error = 'Unknown Tavern mercenary offer.'
    else if (state.hiredOfferId) state.error = 'A mercenary offer was already chosen.'
    else if (spend(offer.goldCost)) state.hiredOfferId = offer.id
  } else if (command.kind === 'buy-drinks') {
    if (state.drinksPurchased) state.error = 'Drinks were already purchased.'
    else if (spend(plan.drinks.goldCost)) state.drinksPurchased = true
  } else if (command.kind === 'buy-rumor') {
    if (state.rumorPurchased) state.error = 'A rumor was already purchased.'
    else if (spend(plan.rumor.goldCost)) state.rumorPurchased = true
  } else if (command.kind === 'play-maria') {
    if (!plan.maria.present) state.error = 'Maria is not present in this Tavern visit.'
    else if (state.mariaPlayed) state.error = 'The Maria match was already played.'
    else {
      state.mariaPlayed = true
      state.mariaVictory = plan.maria.playerRoundWins.filter(Boolean).length
        >= plan.maria.roundsToWin
    }
  } else if (command.kind === 'finish') {
    state.finished = true
  } else {
    state.error = 'Unknown Tavern command.'
  }

  if (!state.error) {
    state.turn = command.turn
    state.commandLog.push({ ...command })
  }
  return state
}

export function replayTavern(plan: TavernPlan, commandLog: readonly TavernCommand[]): TavernReplayState {
  return commandLog.reduce(
    (state, command) => applyTavernCommand(plan, state, command),
    createTavernReplayState(),
  )
}

export function resolveTavern(
  plan: TavernPlan,
  seed: string | number,
  commandLog: readonly TavernCommand[],
): TavernResult {
  const replay = replayTavern(plan, commandLog)
  return {
    kind: 'tavern',
    sessionId: plan.sessionId,
    planId: plan.id,
    planDigest: digestTdValue(plan),
    rulesIdentity: { ...plan.rulesIdentity },
    seed,
    commandLog: commandLog.map(command => ({ ...command })),
    commandDigest: digestTdValue(commandLog),
    hiredOfferId: replay.hiredOfferId,
    drinksPurchased: replay.drinksPurchased,
    rumorPurchased: replay.rumorPurchased,
    mariaPlayed: replay.mariaPlayed,
    mariaVictory: replay.mariaVictory,
    goldSpent: replay.goldSpent,
    mariaPresent: plan.maria.present,
    outcome: 'completed',
    error: replay.error ?? (replay.finished ? null : 'Tavern session has not been finished.'),
  }
}
