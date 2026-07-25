import { digestTdValue } from '../td/engine'
import { CLASH_CORE_LIVE_UNIT_IDS, CLASH_QA_ABILITIES } from './catalog'
import {
  applyClashCommand,
  clashResultFromState,
  createClashPlan,
  createClashRulesIdentity,
  replayClashState,
} from './engine'
import type {
  ClashCommand,
  ClashPlan,
  ClashResult,
  ClashRulesIdentity,
  ClashSide,
  ClashSimulationState,
  ClashUnitDefinition,
  EmpiresClashConfig,
} from './types'

export const CLASH_QA_POLICIES = ['balanced', 'aggressive', 'ranged-rear'] as const
export type ClashQaPolicy = typeof CLASH_QA_POLICIES[number]

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function definitionScore(definition: ClashUnitDefinition, policy: ClashQaPolicy): number {
  if (definition.attack === null || definition.maxHp === null || definition.speed === null) return -Infinity
  if (policy === 'aggressive') return definition.attack * 100 + definition.speed * 10 + definition.maxHp
  if (policy === 'ranged-rear') {
    const ranged = definition.passives.some(passive => passive.kind === 'ranged') ? -1_000 : 1_000
    return ranged + definition.maxHp * 100 + definition.speed * 10 + definition.attack
  }
  return definition.maxHp * 100 + definition.attack * 10 + definition.speed
}

export function createClashQaPlan(
  config: EmpiresClashConfig,
  seed: string | number,
  sessionId = `qa-clash-${String(seed)}`,
  rulesIdentity: ClashRulesIdentity = createClashRulesIdentity(18, config, { qa: true }),
  planId = `${sessionId}-plan`,
): ClashPlan {
  const field = config.fieldVariants.find(candidate => candidate.id === 'settlement-3x4')
    ?? config.fieldVariants.find(candidate => !candidate.deferredReason)
  const region = config.regions.find(candidate => candidate.id === 'common') ?? config.regions[0]
  if (!field || !region) throw new Error('Clash QA requires one live field and region.')
  const definitions = CLASH_CORE_LIVE_UNIT_IDS
    .map(id => config.roster.find(candidate => candidate.id === id))
    .filter((candidate): candidate is ClashUnitDefinition => Boolean(candidate && !candidate.deferredReason))
  if (definitions.length < 4) throw new Error('Clash QA requires at least four live roster definitions.')
  const perSide = Math.min(6, field.columns * (field.rowsPerSide - field.reinforcementRows))
  const plan = createClashPlan({
    id: planId,
    sessionId,
    rulesIdentity,
    config,
    field,
    region,
    roster: (['attacker', 'defender'] as const).flatMap(side => (
      Array.from({ length: perSide }, (_, index) => ({
        instanceId: `${side}-${index + 1}`,
        definitionId: definitions[index % definitions.length].id,
        side,
      }))
    )),
  })
  const igniteCarrier = plan.units.find(definition => definition.id === 'shield-bearer')
  if (igniteCarrier) igniteCarrier.abilities = [{ ...CLASH_QA_ABILITIES.ignite }]
  const moraleCarrier = plan.units.find(definition => definition.id === 'legionary')
  if (moraleCarrier) moraleCarrier.abilities = [{ ...CLASH_QA_ABILITIES.morale }]
  return plan
}

function choosePlacement(
  plan: ClashPlan,
  state: ClashSimulationState,
  side: ClashSide,
  policy: ClashQaPolicy,
): Extract<ClashCommand, { kind: 'place' }> | null {
  const definitions = new Map(plan.units.map(definition => [definition.id, definition]))
  const reserve = Object.values(state.units)
    .filter(unit => unit.side === side && unit.alive && !unit.deployed)
    .sort((left, right) => {
      const leftDefinition = definitions.get(left.definitionId)!
      const rightDefinition = definitions.get(right.definitionId)!
      return definitionScore(rightDefinition, policy) - definitionScore(leftDefinition, policy)
        || stableCompare(left.instanceId, right.instanceId)
    })[0]
  if (!reserve) return null
  const cells = state.cells
    .filter(cell => cell.side === side && cell.unitInstanceId === null)
    .sort((left, right) => left.row - right.row || (
      policy === 'aggressive' ? left.column - right.column : right.column - left.column
    ))
  if (cells.length === 0) return null
  const minimumRow = Math.min(...cells.map(cell => cell.row))
  const cell = cells.find(candidate => candidate.row === minimumRow)!
  return {
    turn: state.turn + 1,
    kind: 'place',
    side,
    unitInstanceId: reserve.instanceId,
    row: cell.row,
    column: cell.column,
  }
}

function activationLimit(plan: ClashPlan, state: ClashSimulationState, side: ClashSide): number {
  const morale = state.morale[side]
  if (morale > plan.morale.positiveThresholdExclusive) return plan.morale.positiveActivationCharges
  if (morale < plan.morale.negativeThresholdExclusive) {
    return state.clashNumber % plan.morale.negativeActivationCooldownTurns === 0 ? 1 : 0
  }
  return plan.morale.neutralActivationCharges
}

function chooseActivation(
  plan: ClashPlan,
  state: ClashSimulationState,
  side: ClashSide,
): Extract<ClashCommand, { kind: 'activate' }> | null {
  if (state.betweenClashes[side].activationCount >= activationLimit(plan, state, side)) return null
  const definitions = new Map(plan.units.map(definition => [definition.id, definition]))
  const units = Object.values(state.units)
    .filter(unit => unit.side === side && unit.alive && unit.deployed)
    .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  const enemies = Object.values(state.units)
    .filter(unit => unit.side !== side && unit.alive && unit.deployed)
    .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  for (const unit of units) {
    const definition = definitions.get(unit.definitionId)
    if (!definition) continue
    for (const ability of [...definition.abilities].sort((left, right) => stableCompare(left.id, right.id))) {
      if ((unit.abilityCharges[ability.id] ?? 0) <= 0
        || (unit.abilityReadyClash[ability.id] ?? 0) > state.clashNumber) continue
      if (ability.target === 'enemy') {
        const target = enemies[0]
        if (!target) continue
        return {
          turn: state.turn + 1,
          kind: 'activate',
          side,
          unitInstanceId: unit.instanceId,
          abilityId: ability.id,
          targetUnitInstanceId: target.instanceId,
        }
      }
      if (ability.target === 'self' || ability.kind === 'morale') {
        return {
          turn: state.turn + 1,
          kind: 'activate',
          side,
          unitInstanceId: unit.instanceId,
          abilityId: ability.id,
          targetSide: side,
        }
      }
    }
  }
  return null
}

export function chooseClashQaCommand(
  plan: ClashPlan,
  state: ClashSimulationState,
  policy: ClashQaPolicy,
): ClashCommand | null {
  if (state.phase === 'finished') return null
  if (state.phase === 'placement') {
    return choosePlacement(plan, state, state.expectedSide!, policy)
  }
  if (state.phase === 'clash-ready') return { turn: state.turn + 1, kind: 'resolve-clash' }
  const side = state.expectedSide!
  const activation = chooseActivation(plan, state, side)
  if (activation) return activation
  if (!state.betweenClashes[side].placementUsed) {
    const placement = choosePlacement(plan, state, side, policy)
    if (placement) return placement
  }
  return { turn: state.turn + 1, kind: 'end-between-clash', side }
}

export function resolveClashWithPolicy(
  plan: ClashPlan,
  seed: string | number,
  policy: ClashQaPolicy,
  existingTurnLog: readonly ClashCommand[] = [],
): ClashResult {
  let state = replayClashState(plan, seed, existingTurnLog)
  if (state.error || state.commandLog.length !== existingTurnLog.length) {
    throw new Error(state.error ?? 'Clash QA could not restore the persisted turn log.')
  }
  for (let actions = 0; actions < plan.maxCommands && state.phase !== 'finished'; actions += 1) {
    const command = chooseClashQaCommand(plan, state, policy)
    if (!command) break
    state = applyClashCommand(plan, state, command)
    if (state.error) throw new Error(`Clash QA stalled: ${state.error}`)
  }
  const result = clashResultFromState(plan, state)
  if (!result) throw new Error(`Clash QA did not terminate after ${state.commandLog.length} commands.`)
  return result
}

export function digestClashQaResult(result: ClashResult): string {
  return digestTdValue({
    outcome: result.outcome,
    winner: result.winner,
    terminalReason: result.terminalReason,
    turns: result.turns,
    clashes: result.clashes,
    commandDigest: result.commandDigest,
    deployments: result.deployments,
    finalMorale: result.finalMorale,
    revealedPassiveIds: result.revealedPassiveIds,
  })
}
