import { resolveDamage } from '../combat/damage'
import type {
  CombatArmorProfile,
  CombatEquipmentDefinition,
  CombatWeaponProfile,
  EmpiresCombatConfig,
} from '../combat/types'
import { createEmpiresRngState, nextEmpiresRandom } from '../rng'
import type {
  EmpiresTdConfig,
  TdBattlePlan,
  TdBattleResult,
  TdCommand,
  TdDeploymentPlan,
  TdEquipmentCost,
  TdEnemyGroupDefinition,
  TdEnemyState,
  TdFrameClock,
  TdPoint,
  TdRulesIdentity,
  TdSimulationState,
  TdSquadState,
  TdTowerBaseDefinition,
  TdTowerChoiceDefinition,
  TdTowerState,
  TdTowerStatModifierDefinition,
  TdTowerTargetingModifierDefinition,
} from './types'

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function finite(value: number): boolean {
  return Number.isFinite(value)
}

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function isRecordValue(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function canonicalJson(value: unknown): string {
  if (value === null || typeof value === 'boolean' || typeof value === 'string') {
    return JSON.stringify(value)
  }
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) throw new Error('Cannot digest a non-finite number.')
    return JSON.stringify(Object.is(value, -0) ? 0 : value)
  }
  if (Array.isArray(value)) return `[${value.map(canonicalJson).join(',')}]`
  if (typeof value === 'object') {
    const record = value as Record<string, unknown>
    const entries = Object.keys(record)
      .filter(key => record[key] !== undefined)
      .sort(stableCompare)
      .map(key => `${JSON.stringify(key)}:${canonicalJson(record[key])}`)
    return `{${entries.join(',')}}`
  }
  throw new Error(`Cannot digest ${typeof value}.`)
}

function fnv1a(value: string): string {
  let hash = 0xcbf29ce484222325n
  for (let index = 0; index < value.length; index += 1) {
    hash ^= BigInt(value.charCodeAt(index))
    hash = BigInt.asUintN(64, hash * 0x100000001b3n)
  }
  return hash.toString(16).padStart(16, '0')
}

/** Stable across object key insertion order; arrays retain authored catalog order. */
export function digestTdValue(value: unknown): string {
  return fnv1a(canonicalJson(value))
}

export function createTdRulesIdentity(
  configSchemaVersion: number,
  combat: EmpiresCombatConfig,
  td: EmpiresTdConfig,
  extraRules?: unknown,
): TdRulesIdentity {
  return {
    configSchemaVersion,
    rulesDigest: digestTdValue({ combat, td, ...(extraRules === undefined ? {} : { extraRules }) }),
  }
}

/** Converts a render delta to a bounded number of simulation ticks. */
export function consumeTdFrameTime(
  accumulatorMs: number,
  elapsedMs: number,
  tickMs: number,
  maxCatchUpTicks: number,
): TdFrameClock {
  if (!Number.isFinite(tickMs) || tickMs <= 0
    || !Number.isInteger(maxCatchUpTicks) || maxCatchUpTicks <= 0) {
    return { ticks: 0, accumulatorMs: 0 }
  }
  const boundedElapsed = Number.isFinite(elapsedMs) ? Math.max(0, elapsedMs) : 0
  const availableMs = Math.max(0, accumulatorMs) + boundedElapsed
  const availableTicks = Math.floor(availableMs / tickMs)
  const ticks = Math.min(maxCatchUpTicks, availableTicks)
  return {
    ticks,
    // Discard backlog beyond the explicit catch-up budget instead of replaying wall time later.
    accumulatorMs: availableTicks > maxCatchUpTicks
      ? availableMs % tickMs
      : availableMs - ticks * tickMs,
  }
}

function pointDistance(left: TdPoint, right: TdPoint): number {
  return Math.hypot(left.x - right.x, left.y - right.y)
}

function nodesById(plan: TdBattlePlan): Map<string, TdPoint> {
  return new Map(plan.battlefield.laneGraph.nodes.map(node => [node.id, node]))
}

function edgesById(plan: TdBattlePlan) {
  return new Map(plan.battlefield.laneGraph.edges.map(edge => [edge.id, edge]))
}

function routeDistance(plan: TdBattlePlan, entity: { routeEdgeIds: string[], edgeIndex: number, edgeProgress: number }): number {
  const nodes = nodesById(plan)
  const edges = edgesById(plan)
  let distance = 0
  for (let index = 0; index < entity.routeEdgeIds.length; index += 1) {
    const edge = edges.get(entity.routeEdgeIds[index])
    if (!edge) continue
    const from = nodes.get(edge.fromNodeId)
    const to = nodes.get(edge.toNodeId)
    if (!from || !to) continue
    const length = pointDistance(from, to)
    if (index < entity.edgeIndex) distance += length
    else if (index === entity.edgeIndex) distance += length * entity.edgeProgress
  }
  return distance
}

function positionOnRoute(
  plan: TdBattlePlan,
  entity: TdEnemyState | TdSquadState,
): TdPoint {
  const nodes = nodesById(plan)
  const edges = edgesById(plan)
  const edge = edges.get(entity.routeEdgeIds[Math.min(entity.edgeIndex, entity.routeEdgeIds.length - 1)])
  if (!edge) return { x: entity.x, y: entity.y }
  const from = nodes.get(edge.fromNodeId)
  const to = nodes.get(edge.toNodeId)
  if (!from || !to) return { x: entity.x, y: entity.y }
  const progress = Math.max(0, Math.min(1, entity.edgeProgress))
  return {
    x: from.x + (to.x - from.x) * progress,
    y: from.y + (to.y - from.y) * progress,
  }
}

function towerBase(plan: TdBattlePlan, tower: TdTowerState): TdTowerBaseDefinition {
  return plan.towerBases.find(base => base.id === tower.towerBaseId) ?? plan.towerBases[0]
}

interface ResolvedTowerLoadout {
  loadoutId: string | null
  weaponEquipmentId: string | null
  defenseEquipmentId?: string
  weapon: CombatWeaponProfile
  armor: CombatArmorProfile | null
  equipmentCosts: TdEquipmentCost[]
}

function aggregateEquipmentCosts(costs: readonly TdEquipmentCost[]): TdEquipmentCost[] {
  const totals = new Map<string, number>()
  for (const cost of costs) {
    totals.set(cost.equipmentId, (totals.get(cost.equipmentId) ?? 0) + cost.amount)
  }
  return [...totals]
    .sort(([left], [right]) => stableCompare(left, right))
    .map(([equipmentId, amount]) => ({ equipmentId, amount }))
}

function canAffordEquipment(
  stock: Readonly<Record<string, number>>,
  costs: readonly TdEquipmentCost[],
): boolean {
  return costs.every(cost => (stock[cost.equipmentId] ?? 0) >= cost.amount)
}

function resolveTowerLoadout(
  plan: TdBattlePlan,
  base: TdTowerBaseDefinition,
  stock: Readonly<Record<string, number>>,
): ResolvedTowerLoadout {
  const equipment = new Map(plan.combat.equipment.map(definition => [definition.id, definition]))
  for (const loadout of [...(base.loadouts ?? [])]
    .sort((left, right) => right.priority - left.priority || stableCompare(left.id, right.id))) {
    const costs = aggregateEquipmentCosts(loadout.equipmentCosts)
    if (!canAffordEquipment(stock, costs)) continue
    const weapon = equipment.get(loadout.weaponEquipmentId)
    const defense = loadout.defenseEquipmentId
      ? equipment.get(loadout.defenseEquipmentId)
      : undefined
    if (!weapon || weapon.kind !== 'weapon' || weapon.deferredReason
      || (loadout.defenseEquipmentId
        && (!defense || defense.kind === 'weapon' || defense.deferredReason))) continue
    return {
      loadoutId: loadout.id,
      weaponEquipmentId: loadout.weaponEquipmentId,
      ...(loadout.defenseEquipmentId ? { defenseEquipmentId: loadout.defenseEquipmentId } : {}),
      weapon: cloneJson(weapon.profile as CombatWeaponProfile),
      armor: defense ? cloneJson(defense.profile as CombatArmorProfile) : null,
      equipmentCosts: costs,
    }
  }
  return {
    loadoutId: null,
    weaponEquipmentId: null,
    weapon: cloneJson(base.weapon),
    armor: null,
    equipmentCosts: [],
  }
}

function towerChoices(plan: TdBattlePlan, tower: TdTowerState): TdTowerChoiceDefinition[] {
  const choices = new Map(plan.towerChoices.map(choice => [choice.id, choice]))
  return tower.choiceIds.flatMap(choiceId => choices.get(choiceId) ?? [])
}

function towerSpot(plan: TdBattlePlan, tower: TdTowerState) {
  return plan.battlefield.buildSpots.find(spot => spot.id === tower.spotId)
}

function matchingTowerStatModifiers(plan: TdBattlePlan, tower: TdTowerState) {
  const terrainId = towerSpot(plan, tower)?.terrainId
  return plan.battlefield.modifiers
    .filter((modifier): modifier is TdTowerStatModifierDefinition => modifier.kind === 'tower-stat'
      && Boolean(terrainId && modifier.terrainIds.includes(terrainId)))
    .sort((left, right) => stableCompare(left.id, right.id))
}

function towerMaxHpMultiplier(plan: TdBattlePlan, tower: TdTowerState): number {
  return matchingTowerStatModifiers(plan, tower)
    .reduce((product, modifier) => product * modifier.maxHpMultiplier, 1)
}

function towerWeapon(plan: TdBattlePlan, tower: TdTowerState): CombatWeaponProfile {
  const weapon = cloneJson(tower.weapon)
  for (const choice of towerChoices(plan, tower)) {
    for (const [damageTypeId, amount] of Object.entries(choice.damageLevelBonuses)) {
      weapon.damageLevels[damageTypeId] = (weapon.damageLevels[damageTypeId] ?? 0) + amount
    }
  }
  return weapon
}

function towerRange(plan: TdBattlePlan, tower: TdTowerState): number {
  const raw = towerBase(plan, tower).range
    + towerChoices(plan, tower).reduce((sum, choice) => sum + choice.rangeBonus, 0)
  const multiplier = matchingTowerStatModifiers(plan, tower)
    .reduce((product, modifier) => product * modifier.rangeMultiplier, 1)
  return Math.max(0, raw * multiplier)
}

function towerAttackInterval(plan: TdBattlePlan, tower: TdTowerState): number {
  return Math.max(1, Math.round(towerBase(plan, tower).attackIntervalTicks
    + towerChoices(plan, tower).reduce((sum, choice) => sum + choice.attackIntervalTicksDelta, 0)))
}

function towerProjectiles(plan: TdBattlePlan, tower: TdTowerState): number {
  return Math.max(1, Math.round(towerBase(plan, tower).projectiles
    + towerChoices(plan, tower).reduce((sum, choice) => sum + choice.projectileBonus, 0)))
}

function towerPriority(plan: TdBattlePlan, tower: TdTowerState) {
  const choices = towerChoices(plan, tower)
  return choices[choices.length - 1]?.targetPriority ?? towerBase(plan, tower).targetPriority
}

function recordHit(
  state: TdSimulationState,
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile | null,
  plan: TdBattlePlan,
): number {
  const damage = resolveDamage(weapon, armor, plan.combat)
  state.hitCount += 1
  state.damageByType[damage.chosenType] = (
    state.damageByType[damage.chosenType] ?? 0
  ) + damage.finalDamage
  return damage.finalDamage
}

function choiceSetForGrade(plan: TdBattlePlan, grade: number) {
  return plan.gradeChoices.find(set => set.regionId === plan.battlefield.regionId && set.grade === grade)
}

export function nextTdTowerGrade(tower: TdTowerState): number {
  return tower.choiceIds.length + 1
}

function categoryAllowed(allowed: readonly string[], actual: readonly string[]): boolean {
  return allowed.length === 0 || actual.some(categoryId => allowed.includes(categoryId))
}

export function tdCommandDisabledReason(
  plan: TdBattlePlan,
  state: TdSimulationState,
  command: TdCommand,
): string | null {
  if (!Number.isInteger(command.tick) || command.tick < 0 || command.tick >= plan.maxTicks) {
    return `Command tick must be between 0 and ${plan.maxTicks - 1}.`
  }
  if (!Number.isInteger(command.sequence) || command.sequence < 0 || command.sequence >= plan.maxCommands) {
    return `Command sequence must be between 0 and ${plan.maxCommands - 1}.`
  }
  if (command.sessionId !== plan.sessionId || command.planId !== plan.id) {
    return 'Command identity does not match the active plan.'
  }
  if (command.tick !== state.tick) return `Command belongs to tick ${command.tick}, not ${state.tick}.`
  const spot = plan.battlefield.buildSpots.find(item => item.id === command.spotId)
  if (!spot) return `Unknown build spot ${command.spotId}.`
  const tower = state.towers.find(item => item.spotId === command.spotId)
  if (command.kind === 'build-tower') {
    if (tower) return `Build spot ${command.spotId} is occupied.`
    const base = plan.towerBases.find(item => item.id === command.towerBaseId)
    if (!base || !plan.battlefield.towerBaseIds.includes(command.towerBaseId)) {
      return `Tower base ${command.towerBaseId} is unavailable on this battlefield.`
    }
    if (!categoryAllowed(plan.battlefield.allowedTowerCategoryIds, base.categoryIds)) {
      return `Tower base ${command.towerBaseId} is forbidden by battlefield categories.`
    }
    if (state.buildResources < base.cost) return `Not enough build resources for ${base.id}.`
    return null
  }
  if (command.kind !== 'upgrade-tower') return 'Unknown TD command kind.'
  if (!tower) return `Build spot ${command.spotId} has no tower to upgrade.`
  const choice = plan.towerChoices.find(item => item.id === command.choiceId)
  if (!choice) return `Unknown tower choice ${command.choiceId}.`
  const grade = nextTdTowerGrade(tower)
  const set = choiceSetForGrade(plan, grade)
  if (!set || set.deferredReason) return set?.deferredReason ?? `Grade ${grade} is unavailable in this region.`
  if (choice.grade !== grade || !set.choiceIds.includes(choice.id)) {
    return `Tower ${command.spotId} requires a configured grade-${grade} choice.`
  }
  if (!categoryAllowed(plan.battlefield.allowedTowerCategoryIds, choice.categoryIds)) {
    return `Tower choice ${choice.id} is forbidden by battlefield categories.`
  }
  if (state.buildResources < choice.cost) return `Not enough build resources for ${choice.id}.`
  return null
}

function applyCommand(plan: TdBattlePlan, state: TdSimulationState, command: TdCommand): boolean {
  const error = tdCommandDisabledReason(plan, state, command)
  if (error) {
    state.commandErrors.push({ tick: state.tick, command: cloneJson(command), message: error })
    state.terminalReason = 'invalid-command'
    return false
  }
  if (command.kind === 'build-tower') {
    const base = plan.towerBases.find(item => item.id === command.towerBaseId)!
    const loadout = resolveTowerLoadout(plan, base, state.equipmentStock)
    const tower: TdTowerState = {
      spotId: command.spotId,
      towerBaseId: base.id,
      choiceIds: [],
      loadoutId: loadout.loadoutId,
      weaponEquipmentId: loadout.weaponEquipmentId,
      ...(loadout.defenseEquipmentId ? { defenseEquipmentId: loadout.defenseEquipmentId } : {}),
      weapon: loadout.weapon,
      armor: loadout.armor,
      hp: base.maxHp,
      nextAttackTick: state.tick,
    }
    tower.hp *= towerMaxHpMultiplier(plan, tower)
    state.buildResources -= base.cost
    for (const cost of loadout.equipmentCosts) {
      state.equipmentStock[cost.equipmentId] = Math.max(
        0,
        (state.equipmentStock[cost.equipmentId] ?? 0) - cost.amount,
      )
      state.equipmentSpent[cost.equipmentId] = (
        state.equipmentSpent[cost.equipmentId] ?? 0
      ) + cost.amount
    }
    state.towers.push(tower)
    state.towers.sort((left, right) => stableCompare(left.spotId, right.spotId))
  } else {
    const choice = plan.towerChoices.find(item => item.id === command.choiceId)!
    const tower = state.towers.find(item => item.spotId === command.spotId)!
    tower.choiceIds.push(choice.id)
    tower.hp += choice.maxHpBonus * towerMaxHpMultiplier(plan, tower)
    state.buildResources -= choice.cost
  }
  return true
}

export function validateTdCommandLog(plan: TdBattlePlan, commandLog: readonly TdCommand[]): string[] {
  const errors: string[] = []
  if (commandLog.length > plan.maxCommands) {
    errors.push(`Command log exceeds the ${plan.maxCommands}-command limit.`)
  }
  let previousTick = -1
  for (let index = 0; index < Math.min(commandLog.length, plan.maxCommands + 1); index += 1) {
    const command = commandLog[index] as TdCommand & { kind: string }
    if (!command || (command.kind !== 'build-tower' && command.kind !== 'upgrade-tower')) {
      errors.push(`Command ${index} has an unknown kind.`)
      continue
    }
    if (command.sequence !== index) errors.push(`Command ${index} must have sequence ${index}.`)
    if (!Number.isInteger(command.tick) || command.tick < 0 || command.tick >= plan.maxTicks) {
      errors.push(`Command ${index} has an out-of-bounds tick.`)
    } else if (command.tick < previousTick) {
      errors.push(`Command log is not monotonic at sequence ${index}.`)
    }
    previousTick = command.tick
    if (command.sessionId !== plan.sessionId || command.planId !== plan.id) {
      errors.push(`Command ${index} has stale plan/session identity.`)
    }
  }
  return errors
}

function spawnEnemy(
  plan: TdBattlePlan,
  state: TdSimulationState,
  group: TdEnemyGroupDefinition,
): void {
  const edge = edgesById(plan).get(group.routeEdgeIds[0])
  const startNodeId = group.stationNodeId ?? edge?.fromNodeId
  const node = startNodeId ? nodesById(plan).get(startNodeId) : null
  const jitter = (nextEmpiresRandom(state.rng) - 0.5) * 0.000_001
  state.enemies.push({
    id: `enemy-${state.nextEnemyId}`,
    groupId: group.id,
    routeEdgeIds: [...group.routeEdgeIds],
    edgeIndex: 0,
    edgeProgress: group.stationNodeId ? 0 : Math.max(0, jitter),
    x: node?.x ?? 0,
    y: node?.y ?? 0,
    hp: group.maxHp,
    maxHp: group.maxHp,
    nextAttackTick: state.tick,
  })
  state.enemies.sort((left, right) => stableCompare(left.id, right.id))
  state.nextEnemyId += 1
  state.spawnedByGroup[group.id] = (state.spawnedByGroup[group.id] ?? 0) + 1
}

function spawnDueEnemies(plan: TdBattlePlan, state: TdSimulationState): void {
  for (const group of [...plan.wave.groups].sort((left, right) => stableCompare(left.id, right.id))) {
    const spawned = state.spawnedByGroup[group.id] ?? 0
    if (spawned >= group.count) continue
    if (state.tick >= group.startTick + spawned * group.spawnIntervalTicks) spawnEnemy(plan, state, group)
  }
}

function livingEnemies(state: TdSimulationState): TdEnemyState[] {
  return state.enemies.filter(enemy => enemy.hp > 0)
}

function livingSquads(state: TdSimulationState): TdSquadState[] {
  return state.squads.filter(squad => squad.hp > 0)
}

function groupForEnemy(plan: TdBattlePlan, enemy: TdEnemyState) {
  return plan.wave.groups.find(group => group.id === enemy.groupId)
}

function canEnemyTargetTower(plan: TdBattlePlan, enemy: TdEnemyState, tower: TdTowerState): boolean {
  const terrainId = towerSpot(plan, tower)?.terrainId
  if (!terrainId) return false
  const categories = groupForEnemy(plan, enemy)?.categoryIds ?? []
  return plan.battlefield.modifiers
    .filter((modifier): modifier is TdTowerTargetingModifierDefinition => modifier.kind === 'tower-targeting'
      && modifier.terrainIds.includes(terrainId))
    .sort((left, right) => stableCompare(left.id, right.id))
    .every(modifier => categories.some(categoryId => modifier.targetableByEnemyCategoryIds.includes(categoryId)))
}

function towerAttacks(plan: TdBattlePlan, state: TdSimulationState): void {
  const spots = new Map(plan.battlefield.buildSpots.map(spot => [spot.id, spot]))
  for (const tower of state.towers.filter(candidate => candidate.hp > 0)
    .sort((left, right) => stableCompare(left.spotId, right.spotId))) {
    if (tower.nextAttackTick > state.tick) continue
    const spot = spots.get(tower.spotId)
    if (!spot) continue
    const inRange = livingEnemies(state).filter(enemy => pointDistance(spot, enemy) <= towerRange(plan, tower))
    if (inRange.length === 0) continue
    const priority = towerPriority(plan, tower)
    inRange.sort((left, right) => priority === 'strongest'
      ? right.maxHp - left.maxHp || routeDistance(plan, right) - routeDistance(plan, left) || stableCompare(left.id, right.id)
      : routeDistance(plan, right) - routeDistance(plan, left) || stableCompare(left.id, right.id))
    const weapon = towerWeapon(plan, tower)
    for (let projectile = 0; projectile < towerProjectiles(plan, tower); projectile += 1) {
      const target = inRange.find(enemy => enemy.hp > 0)
      if (!target) break
      target.hp = Math.max(0, target.hp - recordHit(
        state,
        weapon,
        groupForEnemy(plan, target)?.armor ?? null,
        plan,
      ))
    }
    tower.nextAttackTick = state.tick + towerAttackInterval(plan, tower)
  }
}

function moveAlongRoute(
  plan: TdBattlePlan,
  entity: TdEnemyState | TdSquadState,
  speedPerSecond: number,
): boolean {
  const nodes = nodesById(plan)
  const edges = edgesById(plan)
  let remaining = speedPerSecond * plan.tickMs / 1_000
  while (remaining > 0 && entity.edgeIndex < entity.routeEdgeIds.length) {
    const edge = edges.get(entity.routeEdgeIds[entity.edgeIndex])
    if (!edge) return true
    const from = nodes.get(edge.fromNodeId)
    const to = nodes.get(edge.toNodeId)
    if (!from || !to) return true
    const length = Math.max(Number.EPSILON, pointDistance(from, to))
    const distanceLeft = length * (1 - entity.edgeProgress)
    if (remaining < distanceLeft) {
      entity.edgeProgress += remaining / length
      remaining = 0
    } else {
      remaining -= distanceLeft
      entity.edgeIndex += 1
      entity.edgeProgress = 0
    }
  }
  if (entity.edgeIndex >= entity.routeEdgeIds.length) {
    const objective = nodes.get(plan.objective.nodeId)
    if (objective) {
      entity.x = objective.x
      entity.y = objective.y
    }
    return true
  }
  const point = positionOnRoute(plan, entity)
  entity.x = point.x
  entity.y = point.y
  return false
}

function squadActions(plan: TdBattlePlan, state: TdSimulationState): void {
  const deployments = new Map(plan.deployments.map(item => [item.id, item]))
  const objectivePoint = nodesById(plan).get(plan.objective.nodeId)
  for (const squad of livingSquads(state)
    .sort((left, right) => stableCompare(left.deploymentId, right.deploymentId))) {
    const deployment = deployments.get(squad.deploymentId)
    if (!deployment) continue
    if (deployment.healing
      && squad.healingChargesRemaining > 0
      && squad.nextHealTick <= state.tick) {
      const patient = livingSquads(state)
        .filter(candidate => candidate.hp < candidate.maxHp)
        .filter(candidate => pointDistance(squad, candidate) <= deployment.healing!.range)
        .sort((left, right) => (
          pointDistance(squad, left) - pointDistance(squad, right)
          || stableCompare(left.deploymentId, right.deploymentId)
        ))[0]
      if (patient) {
        const healersAlive = Math.max(1, Math.ceil(squad.hp / deployment.maxHpPerUnit))
        const availableCharges = Math.min(healersAlive, squad.healingChargesRemaining)
        const missingHp = patient.maxHp - patient.hp
        const chargesUsed = Math.min(
          availableCharges,
          Math.max(1, Math.ceil(missingHp / deployment.healing.amountPerUnit)),
        )
        patient.hp = Math.min(
          patient.maxHp,
          patient.hp + chargesUsed * deployment.healing.amountPerUnit,
        )
        squad.healingChargesRemaining -= chargesUsed
        squad.nextHealTick = state.tick + Math.max(1, deployment.healing.intervalTicks)
      }
    }
    const targets = livingEnemies(state)
      .filter(enemy => pointDistance(squad, enemy) <= deployment.attackRange)
      .sort((left, right) => routeDistance(plan, right) - routeDistance(plan, left) || stableCompare(left.id, right.id))
    const target = targets[0]
    if (target && squad.nextAttackTick <= state.tick) {
      const alive = Math.max(1, Math.ceil(squad.hp / deployment.maxHpPerUnit))
      const armor = groupForEnemy(plan, target)?.armor ?? null
      let volleyDamage = 0
      for (let attacker = 0; attacker < alive; attacker += 1) {
        volleyDamage += recordHit(state, deployment.weapon, armor, plan)
      }
      target.hp = Math.max(0, target.hp - volleyDamage)
      squad.nextAttackTick = state.tick + Math.max(1, deployment.attackIntervalTicks)
      continue
    }
    if (plan.mode !== 'assault' || target) continue
    const reachedObjective = objectivePoint
      ? pointDistance(squad, objectivePoint) <= deployment.attackRange
        || moveAlongRoute(plan, squad, deployment.speedPerSecond)
      : true
    if (reachedObjective && squad.nextAttackTick <= state.tick) {
      const alive = Math.max(1, Math.ceil(squad.hp / deployment.maxHpPerUnit))
      let volleyDamage = 0
      for (let attacker = 0; attacker < alive; attacker += 1) {
        volleyDamage += recordHit(state, deployment.weapon, plan.objective.armor, plan)
      }
      state.objectiveHp = Math.max(0, state.objectiveHp - volleyDamage)
      squad.nextAttackTick = state.tick + Math.max(1, deployment.attackIntervalTicks)
    }
  }
}

function defenseEnemyActions(plan: TdBattlePlan, state: TdSimulationState): void {
  const deployments = new Map(plan.deployments.map(item => [item.id, item]))
  const spots = new Map(plan.battlefield.buildSpots.map(spot => [spot.id, spot]))
  for (const enemy of livingEnemies(state).sort((left, right) => stableCompare(left.id, right.id))) {
    const group = groupForEnemy(plan, enemy)
    if (!group) continue
    const squad = livingSquads(state)
      .filter(candidate => pointDistance(candidate, enemy) <= group.attackRange)
      .sort((left, right) => pointDistance(left, enemy) - pointDistance(right, enemy)
        || stableCompare(left.deploymentId, right.deploymentId))[0]
    if (squad) {
      if (enemy.nextAttackTick <= state.tick) {
        const armor = deployments.get(squad.deploymentId)?.armor ?? null
        squad.hp = Math.max(0, squad.hp - recordHit(state, group.weapon, armor, plan))
        enemy.nextAttackTick = state.tick + Math.max(1, group.attackIntervalTicks)
      }
      continue
    }
    const towerTarget = state.towers
      .filter(tower => tower.hp > 0 && canEnemyTargetTower(plan, enemy, tower))
      .flatMap((tower) => {
        const spot = spots.get(tower.spotId)
        return spot ? [{ tower, distance: pointDistance(spot, enemy) }] : []
      })
      .filter(candidate => candidate.distance <= group.attackRange)
      .sort((left, right) => left.distance - right.distance
        || stableCompare(left.tower.spotId, right.tower.spotId))[0]?.tower
    if (towerTarget) {
      if (enemy.nextAttackTick <= state.tick) {
        towerTarget.hp = Math.max(
          0,
          towerTarget.hp - recordHit(state, group.weapon, towerTarget.armor, plan),
        )
        if (towerTarget.hp <= 0) state.towers = state.towers.filter(tower => tower !== towerTarget)
        enemy.nextAttackTick = state.tick + Math.max(1, group.attackIntervalTicks)
      }
      continue
    }
    const reachedObjective = moveAlongRoute(plan, enemy, group.speedPerSecond)
    if (reachedObjective && enemy.nextAttackTick <= state.tick) {
      state.objectiveHp = Math.max(
        0,
        state.objectiveHp - recordHit(state, group.weapon, plan.objective.armor, plan),
      )
      enemy.nextAttackTick = state.tick + Math.max(1, group.attackIntervalTicks)
    }
  }
}

function assaultEnemyActions(plan: TdBattlePlan, state: TdSimulationState): void {
  const deployments = new Map(plan.deployments.map(item => [item.id, item]))
  for (const enemy of livingEnemies(state).sort((left, right) => stableCompare(left.id, right.id))) {
    const group = groupForEnemy(plan, enemy)
    if (!group || enemy.nextAttackTick > state.tick) continue
    const squad = livingSquads(state)
      .filter(candidate => pointDistance(candidate, enemy) <= group.attackRange)
      .sort((left, right) => pointDistance(left, enemy) - pointDistance(right, enemy)
        || stableCompare(left.deploymentId, right.deploymentId))[0]
    if (!squad) continue
    squad.hp = Math.max(
      0,
      squad.hp - recordHit(state, group.weapon, deployments.get(squad.deploymentId)?.armor ?? null, plan),
    )
    enemy.nextAttackTick = state.tick + Math.max(1, group.attackIntervalTicks)
  }
}

function applyBattlefieldModifiers(plan: TdBattlePlan, state: TdSimulationState): void {
  for (const modifier of [...plan.battlefield.modifiers].sort((left, right) => stableCompare(left.id, right.id))) {
    if (modifier.kind !== 'deployment-attrition'
      || !modifier.modes.includes(plan.mode)
      || state.tick === 0
      || state.tick % modifier.intervalTicks !== 0) continue
    for (const squad of livingSquads(state)
      .sort((left, right) => stableCompare(left.deploymentId, right.deploymentId))) {
      const deployment = plan.deployments.find(item => item.id === squad.deploymentId)
      if (!deployment) continue
      const alive = Math.max(1, Math.ceil(squad.hp / deployment.maxHpPerUnit))
      const damage = alive * modifier.damagePerUnit
      squad.hp = Math.max(0, squad.hp - damage)
      state.damageByType.attrition = (state.damageByType.attrition ?? 0) + damage
    }
  }
}

function allEnemiesSpawned(plan: TdBattlePlan, state: TdSimulationState): boolean {
  return plan.wave.groups.every(group => (state.spawnedByGroup[group.id] ?? 0) >= group.count)
}

function resolveTerminal(plan: TdBattlePlan, state: TdSimulationState): void {
  if (state.terminalReason) return
  if (state.objectiveHp <= 0) {
    state.terminalReason = 'objective-destroyed'
    return
  }
  if (plan.mode === 'defense' && allEnemiesSpawned(plan, state) && livingEnemies(state).length === 0) {
    state.terminalReason = 'all-waves-defeated'
    return
  }
  if (plan.mode === 'assault' && livingSquads(state).length === 0) {
    state.terminalReason = 'all-deployments-defeated'
    return
  }
  if (state.tick >= plan.maxTicks) state.terminalReason = 'tick-cap'
}

function primaryRoute(plan: TdBattlePlan): string[] {
  return [...(plan.wave.groups[0]?.routeEdgeIds ?? [])]
}

function squadFromDeployment(plan: TdBattlePlan, deployment: TdDeploymentPlan): TdSquadState {
  const node = nodesById(plan).get(deployment.nodeId)
  return {
    deploymentId: deployment.id,
    cohortId: deployment.cohortId,
    cityId: deployment.cityId,
    unitId: deployment.unitId,
    count: deployment.count,
    routeEdgeIds: plan.mode === 'assault' ? primaryRoute(plan) : [],
    edgeIndex: 0,
    edgeProgress: 0,
    hp: deployment.count * deployment.maxHpPerUnit,
    maxHp: deployment.count * deployment.maxHpPerUnit,
    nextAttackTick: 0,
    nextHealTick: 0,
    healingChargesRemaining: deployment.count * (deployment.healing?.chargesPerUnit ?? 0),
    x: node?.x ?? 0,
    y: node?.y ?? 0,
  }
}

export function validateTdBattlePlan(plan: TdBattlePlan): string[] {
  const errors: string[] = []
  const nodeIds = new Set(plan.battlefield.laneGraph.nodes.map(node => node.id))
  const edges = new Map(plan.battlefield.laneGraph.edges.map(edge => [edge.id, edge]))
  const edgeIds = new Set(edges.keys())
  const choiceIds = new Set(plan.towerChoices.map(choice => choice.id))
  const towerBaseIds = new Set(plan.towerBases.map(base => base.id))
  const damageTypeIds = new Set(plan.combat.damageTypes.map(definition => definition.id))
  const armorClassIds = new Set(plan.combat.armorClasses.map(definition => definition.id))
  const equipmentById = new Map(plan.combat.equipment.map(definition => [definition.id, definition]))
  const validateWeapon = (weapon: CombatWeaponProfile, path: string) => {
    if (!isRecordValue(weapon)
      || !isRecordValue(weapon.damageLevels)
      || !Array.isArray(weapon.tags)
      || weapon.tags.some(tag => typeof tag !== 'string' || !tag)) {
      errors.push(`${path} is not a valid weapon profile`)
      return
    }
    const levels = Object.entries(weapon.damageLevels)
    if (levels.length === 0) errors.push(`${path} has no configured damage type`)
    for (const [damageTypeId, level] of levels) {
      if (!damageTypeIds.has(damageTypeId)) errors.push(`${path} uses unknown damage type ${damageTypeId}`)
      if (!finite(level ?? Number.NaN) || (level ?? -1) < 0) {
        errors.push(`${path} damage level ${damageTypeId} must be finite and non-negative`)
      }
    }
  }
  const validateArmor = (armor: CombatArmorProfile | null, path: string) => {
    if (!armor) return
    if (!isRecordValue(armor) || typeof armor.classId !== 'string' || typeof armor.level !== 'number') {
      errors.push(`${path} is not a valid armor profile`)
      return
    }
    if (!armorClassIds.has(armor.classId)) errors.push(`${path} uses unknown armor class ${armor.classId}`)
    if (!finite(armor.level) || armor.level < 0) errors.push(`${path} level must be finite and non-negative`)
  }
  if (!plan.id.trim()) errors.push('plan id is required')
  if (!plan.sessionId.trim()) errors.push('session id is required')
  if (!plan.rulesIdentity.rulesDigest.trim() || !Number.isInteger(plan.rulesIdentity.configSchemaVersion)) {
    errors.push('rules identity is invalid')
  }
  if (plan.mode !== 'defense' && plan.mode !== 'assault') errors.push('battle mode is unsupported')
  if (plan.objective.owner !== (plan.mode === 'defense' ? 'player' : 'enemy')) {
    errors.push('objective owner does not match battle mode')
  }
  if (!plan.combat.enabled) errors.push('plan combat must be enabled')
  if (!Number.isInteger(plan.scheduledCon) || plan.scheduledCon <= 0) errors.push('scheduledCon must be a positive integer')
  if (!finite(plan.threat) || plan.threat < 0) errors.push('threat must be finite and non-negative')
  if (!Number.isInteger(plan.tickMs) || plan.tickMs <= 0) errors.push('tickMs must be a positive integer')
  if (!Number.isInteger(plan.maxTicks) || plan.maxTicks <= 0) errors.push('maxTicks must be a positive integer')
  if (!Number.isInteger(plan.maxCommands) || plan.maxCommands <= 0) errors.push('maxCommands must be a positive integer')
  if (!Number.isInteger(plan.maxCatchUpTicksPerFrame) || plan.maxCatchUpTicksPerFrame <= 0) {
    errors.push('maxCatchUpTicksPerFrame must be a positive integer')
  }
  if (!finite(plan.startingBuildResources) || plan.startingBuildResources < 0) {
    errors.push('startingBuildResources must be finite and non-negative')
  }
  if (!plan.equipmentStock || typeof plan.equipmentStock !== 'object' || Array.isArray(plan.equipmentStock)) {
    errors.push('equipmentStock must be an object')
  } else {
    for (const [equipmentId, amount] of Object.entries(plan.equipmentStock)) {
      if (!equipmentId.trim()) errors.push('equipmentStock ids must be non-empty')
      if (!finite(amount) || amount < 0) {
        errors.push(`equipmentStock ${equipmentId} must be finite and non-negative`)
      }
    }
  }
  for (const endpoint of [
    plan.battlefield.spawnerNodeId,
    plan.battlefield.deploymentNodeId,
    plan.battlefield.objectiveNodeId,
    plan.objective.nodeId,
  ]) {
    if (!nodeIds.has(endpoint)) errors.push(`battlefield endpoint ${endpoint} is unknown`)
  }
  if (plan.objective.nodeId !== plan.battlefield.objectiveNodeId) errors.push('objective node does not match battlefield')
  if (!finite(plan.objective.maxHp) || plan.objective.maxHp <= 0) errors.push('objective maxHp must be finite and positive')
  validateArmor(plan.objective.armor, 'objective armor')
  for (const edge of plan.battlefield.laneGraph.edges) {
    if (!nodeIds.has(edge.fromNodeId) || !nodeIds.has(edge.toNodeId)) errors.push(`edge ${edge.id} references an unknown node`)
  }
  if (towerBaseIds.size !== plan.towerBases.length) errors.push('tower base ids must be unique')
  for (const baseId of plan.battlefield.towerBaseIds) {
    if (!towerBaseIds.has(baseId)) errors.push(`battlefield references unknown tower base ${baseId}`)
  }
  for (const base of plan.towerBases) {
    validateWeapon(base.weapon, `tower base ${base.id} weapon`)
    if (!finite(base.cost) || base.cost < 0) errors.push(`tower base ${base.id} cost is invalid`)
    if (!finite(base.maxHp) || base.maxHp <= 0) errors.push(`tower base ${base.id} maxHp is invalid`)
    if (!finite(base.range) || base.range <= 0) errors.push(`tower base ${base.id} range is invalid`)
    if (!Number.isInteger(base.attackIntervalTicks) || base.attackIntervalTicks <= 0) {
      errors.push(`tower base ${base.id} attackIntervalTicks must be positive`)
    }
    const loadoutIds = new Set<string>()
    for (const loadout of base.loadouts ?? []) {
      if (!loadout.id.trim()) errors.push(`tower base ${base.id} loadout id is required`)
      if (loadoutIds.has(loadout.id)) errors.push(`tower base ${base.id} repeats loadout ${loadout.id}`)
      loadoutIds.add(loadout.id)
      if (!finite(loadout.priority)) errors.push(`tower base ${base.id} loadout ${loadout.id} priority is invalid`)
      const weapon = equipmentById.get(loadout.weaponEquipmentId)
      if (!weapon || weapon.kind !== 'weapon' || weapon.deferredReason) {
        errors.push(`tower base ${base.id} loadout ${loadout.id} weapon is unavailable`)
      } else {
        validateWeapon(
          weapon.profile as CombatWeaponProfile,
          `tower base ${base.id} loadout ${loadout.id} weapon`,
        )
      }
      if (loadout.defenseEquipmentId) {
        const defense = equipmentById.get(loadout.defenseEquipmentId)
        if (!defense || defense.kind === 'weapon' || defense.deferredReason) {
          errors.push(`tower base ${base.id} loadout ${loadout.id} defense is unavailable`)
        } else {
          validateArmor(
            defense.profile as CombatArmorProfile,
            `tower base ${base.id} loadout ${loadout.id} defense`,
          )
        }
      }
      if (loadout.equipmentCosts.length === 0) {
        errors.push(`tower base ${base.id} loadout ${loadout.id} needs equipment costs`)
      }
      const costIds = new Set<string>()
      for (const cost of loadout.equipmentCosts) {
        if (!cost.equipmentId.trim()) {
          errors.push(`tower base ${base.id} loadout ${loadout.id} has an empty equipment cost id`)
        }
        if (costIds.has(cost.equipmentId)) {
          errors.push(`tower base ${base.id} loadout ${loadout.id} repeats equipment cost ${cost.equipmentId}`)
        }
        costIds.add(cost.equipmentId)
        if (!finite(cost.amount) || cost.amount <= 0) {
          errors.push(`tower base ${base.id} loadout ${loadout.id} equipment cost is invalid`)
        }
      }
      for (const [equipmentId, definition] of [
        [loadout.weaponEquipmentId, weapon],
        ...(loadout.defenseEquipmentId
          ? [[loadout.defenseEquipmentId, equipmentById.get(loadout.defenseEquipmentId)]]
          : []),
      ] as Array<[string, CombatEquipmentDefinition | undefined]>) {
        if (definition?.technologyId && !costIds.has(equipmentId)) {
          errors.push(
            `tower base ${base.id} loadout ${loadout.id} must consume its technology-linked equipment ${equipmentId}`,
          )
        }
      }
    }
  }
  if (choiceIds.size !== plan.towerChoices.length) errors.push('tower choice ids must be unique')
  for (const choice of plan.towerChoices) {
    if (!finite(choice.cost) || choice.cost < 0) errors.push(`tower choice ${choice.id} cost is invalid`)
    for (const [damageTypeId, bonus] of Object.entries(choice.damageLevelBonuses)) {
      if (!damageTypeIds.has(damageTypeId)) errors.push(`tower choice ${choice.id} uses unknown damage type ${damageTypeId}`)
      if (!finite(bonus)) errors.push(`tower choice ${choice.id} damage bonus is not finite`)
    }
  }
  const grades = new Set<number>()
  for (const set of plan.gradeChoices.filter(item => item.regionId === plan.battlefield.regionId)) {
    if (grades.has(set.grade)) errors.push(`region ${set.regionId} repeats grade ${set.grade}`)
    grades.add(set.grade)
    if (set.deferredReason) {
      if (set.choiceIds.length > 0) errors.push(`deferred grade set ${set.id} must not expose choices`)
    } else if (set.choiceIds.length !== 4) {
      errors.push(`live grade set ${set.id} must contain exactly four choices`)
    }
    for (const choiceId of set.choiceIds) {
      const choice = plan.towerChoices.find(item => item.id === choiceId)
      if (!choice || choice.grade !== set.grade) errors.push(`grade set ${set.id} references invalid choice ${choiceId}`)
    }
  }
  if ([1, 2, 3, 4].some(grade => !grades.has(grade))) errors.push('battlefield region needs all four grade rows')
  for (const modifier of plan.battlefield.modifiers) {
    if (modifier.kind === 'tower-stat') {
      if (!finite(modifier.rangeMultiplier) || modifier.rangeMultiplier <= 0
        || !finite(modifier.maxHpMultiplier) || modifier.maxHpMultiplier <= 0) {
        errors.push(`modifier ${modifier.id} multipliers must be finite and positive`)
      }
    } else if (modifier.kind === 'deployment-attrition') {
      if (!Number.isInteger(modifier.intervalTicks) || modifier.intervalTicks <= 0
        || !finite(modifier.damagePerUnit) || modifier.damagePerUnit <= 0) {
        errors.push(`modifier ${modifier.id} attrition values are invalid`)
      }
    }
  }
  for (const group of plan.wave.groups) {
    if (!Number.isInteger(group.count) || group.count <= 0) errors.push(`group ${group.id} count must be positive`)
    if (group.categoryIds.length === 0) errors.push(`group ${group.id} needs an enemy category`)
    if (group.routeEdgeIds.length === 0 || group.routeEdgeIds.some(id => !edgeIds.has(id))) {
      errors.push(`group ${group.id} references an unknown route edge`)
    }
    let nodeId = plan.battlefield.spawnerNodeId
    for (const edgeId of group.routeEdgeIds) {
      const edge = edges.get(edgeId)
      if (!edge || edge.fromNodeId !== nodeId) {
        errors.push(`group ${group.id} route is not contiguous from the spawner`)
        break
      }
      nodeId = edge.toNodeId
    }
    if (nodeId !== plan.objective.nodeId) errors.push(`group ${group.id} route does not reach the objective`)
    if (plan.mode === 'assault' && (!group.stationNodeId || !nodeIds.has(group.stationNodeId))) {
      errors.push(`assault group ${group.id} needs a known stationNodeId`)
    }
    if (!Number.isInteger(group.startTick) || group.startTick < 0) errors.push(`group ${group.id} startTick must be a non-negative integer`)
    if (!Number.isInteger(group.spawnIntervalTicks) || group.spawnIntervalTicks <= 0) errors.push(`group ${group.id} spawnIntervalTicks must be positive`)
    if (!finite(group.maxHp) || group.maxHp <= 0) errors.push(`group ${group.id} maxHp is invalid`)
    if (!finite(group.speedPerSecond) || group.speedPerSecond < 0) errors.push(`group ${group.id} speedPerSecond is invalid`)
    if (plan.mode === 'defense' && group.speedPerSecond <= 0) errors.push(`defense group ${group.id} must move`)
    if (!finite(group.attackRange) || group.attackRange < 0) errors.push(`group ${group.id} attackRange is invalid`)
    if (!Number.isInteger(group.attackIntervalTicks) || group.attackIntervalTicks <= 0) errors.push(`group ${group.id} attackIntervalTicks must be positive`)
    validateWeapon(group.weapon, `group ${group.id} weapon`)
    validateArmor(group.armor, `group ${group.id} armor`)
  }
  if (plan.mode === 'assault' && plan.deployments.length === 0) errors.push('assault mode requires a player deployment')
  const deploymentIds = new Set<string>()
  const cohortIds = new Set<string>()
  for (const deployment of plan.deployments) {
    if (!deployment.id.trim()) errors.push('deployment id is required')
    if (deploymentIds.has(deployment.id)) errors.push(`deployment id ${deployment.id} is repeated`)
    deploymentIds.add(deployment.id)
    if (!deployment.cohortId.trim()) errors.push(`deployment ${deployment.id} cohortId is required`)
    if (cohortIds.has(deployment.cohortId)) {
      errors.push(`deployment cohortId ${deployment.cohortId} is repeated`)
    }
    cohortIds.add(deployment.cohortId)
    if (!nodeIds.has(deployment.nodeId)) errors.push(`deployment ${deployment.id} references an unknown node`)
    if (!Number.isInteger(deployment.count) || deployment.count <= 0) errors.push(`deployment ${deployment.id} count must be positive`)
    if (!finite(deployment.speedPerSecond) || deployment.speedPerSecond < 0) errors.push(`deployment ${deployment.id} speed is invalid`)
    if (plan.mode === 'assault' && deployment.speedPerSecond <= 0) errors.push(`assault deployment ${deployment.id} must move`)
    if (!finite(deployment.maxHpPerUnit) || deployment.maxHpPerUnit <= 0) errors.push(`deployment ${deployment.id} maxHpPerUnit is invalid`)
    if (!finite(deployment.attackRange) || deployment.attackRange < 0) errors.push(`deployment ${deployment.id} attackRange is invalid`)
    if (!Number.isInteger(deployment.attackIntervalTicks) || deployment.attackIntervalTicks <= 0) errors.push(`deployment ${deployment.id} attackIntervalTicks must be positive`)
    if (deployment.healing) {
      if (!finite(deployment.healing.range) || deployment.healing.range < 0) errors.push(`deployment ${deployment.id} healing range is invalid`)
      if (!Number.isInteger(deployment.healing.intervalTicks) || deployment.healing.intervalTicks <= 0) errors.push(`deployment ${deployment.id} healing intervalTicks must be positive`)
      if (!finite(deployment.healing.amountPerUnit) || deployment.healing.amountPerUnit <= 0) errors.push(`deployment ${deployment.id} healing amountPerUnit is invalid`)
      if (!Number.isInteger(deployment.healing.chargesPerUnit) || deployment.healing.chargesPerUnit <= 0) errors.push(`deployment ${deployment.id} healing chargesPerUnit must be positive`)
    }
    validateWeapon(deployment.weapon, `deployment ${deployment.id} weapon`)
    validateArmor(deployment.armor, `deployment ${deployment.id} armor`)
  }
  return errors
}

export function createTdSimulation(plan: TdBattlePlan, seed: string | number): TdSimulationState {
  const errors = validateTdBattlePlan(plan)
  if (errors.length > 0) throw new Error(`Invalid TD plan:\n${errors.join('\n')}`)
  return {
    tick: 0,
    elapsedMs: 0,
    rng: createEmpiresRngState(seed),
    buildResources: plan.startingBuildResources,
    equipmentStock: cloneJson(plan.equipmentStock),
    equipmentSpent: {},
    objectiveHp: plan.objective.maxHp,
    towers: [],
    enemies: [],
    squads: plan.deployments.map(deployment => squadFromDeployment(plan, deployment)),
    spawnedByGroup: {},
    nextEnemyId: 1,
    damageByType: {},
    hitCount: 0,
    terminalReason: null,
    commandErrors: [],
  }
}

/** Advances exactly one configured tick. This is the TD simulator's sole state transition. */
export function stepTdSimulation(
  plan: TdBattlePlan,
  state: TdSimulationState,
  commands: readonly TdCommand[] = [],
): TdSimulationState {
  if (state.terminalReason) return state
  for (const command of commands) {
    if (!applyCommand(plan, state, command)) return state
  }
  spawnDueEnemies(plan, state)
  applyBattlefieldModifiers(plan, state)
  towerAttacks(plan, state)
  squadActions(plan, state)
  if (plan.mode === 'defense') defenseEnemyActions(plan, state)
  else assaultEnemyActions(plan, state)
  state.tick += 1
  state.elapsedMs += plan.tickMs
  resolveTerminal(plan, state)
  return state
}

function resultFromState(
  plan: TdBattlePlan,
  seed: string | number,
  commandLog: readonly TdCommand[],
  state: TdSimulationState,
): TdBattleResult {
  const deployments = plan.deployments.map((deployment) => {
    const squad = state.squads.find(item => item.deploymentId === deployment.id)
    const hp = Math.max(0, squad?.hp ?? 0)
    return {
      deploymentId: deployment.id,
      cohortId: deployment.cohortId,
      cityId: deployment.cityId,
      unitId: deployment.unitId,
      deployed: deployment.count,
      survived: Math.min(deployment.count, Math.ceil(hp / deployment.maxHpPerUnit)),
      healthRatio: deployment.count > 0
        ? Math.max(0, Math.min(1, hp / (deployment.count * deployment.maxHpPerUnit)))
        : 0,
    }
  })
  const terminalReason = state.terminalReason ?? 'tick-cap'
  const outcome = terminalReason === 'all-waves-defeated'
    || (terminalReason === 'objective-destroyed' && plan.mode === 'assault')
    ? 'victory'
    : terminalReason === 'all-deployments-defeated'
      || (terminalReason === 'objective-destroyed' && plan.mode === 'defense')
      ? 'defeat'
      : terminalReason === 'aborted'
        ? 'aborted'
        : 'error'
  const enemiesSpawned = Object.values(state.spawnedByGroup).reduce((sum, count) => sum + count, 0)
  return {
    kind: 'td',
    sessionId: plan.sessionId,
    planId: plan.id,
    planDigest: digestTdValue(plan),
    rulesIdentity: cloneJson(plan.rulesIdentity),
    seed,
    outcome,
    terminalReason,
    ticks: state.tick,
    objectiveHp: state.objectiveHp,
    objectiveMaxHp: plan.objective.maxHp,
    enemiesSpawned,
    enemiesDefeated: state.enemies.filter(enemy => enemy.hp <= 0).length,
    deployments,
    buildResourcesRemaining: state.buildResources,
    equipmentSpent: cloneJson(state.equipmentSpent),
    damageByType: { ...state.damageByType },
    hitCount: state.hitCount,
    commandLog: cloneJson([...commandLog]),
    commandDigest: digestTdValue(commandLog),
    ...(state.commandErrors[0] ? { error: state.commandErrors[0].message } : {}),
  }
}

/** The only supported result path for played and headless TD battles. */
export function replayTdBattle(
  plan: TdBattlePlan,
  seed: string | number,
  commandLog: readonly TdCommand[],
): TdBattleResult {
  const boundedCommands = cloneJson(commandLog.slice(0, plan.maxCommands + 1))
  const state = createTdSimulation(plan, seed)
  const logErrors = validateTdCommandLog(plan, boundedCommands)
  if (logErrors.length > 0) {
    state.commandErrors.push({ tick: 0, command: boundedCommands[0] ?? null, message: logErrors[0] })
    state.terminalReason = 'invalid-command'
    return resultFromState(plan, seed, boundedCommands.slice(0, plan.maxCommands), state)
  }
  let commandIndex = 0
  while (!state.terminalReason) {
    const commands: TdCommand[] = []
    while (boundedCommands[commandIndex]?.tick === state.tick) {
      commands.push(boundedCommands[commandIndex])
      commandIndex += 1
    }
    stepTdSimulation(plan, state, commands)
  }
  if (commandIndex < boundedCommands.length) {
    const command = boundedCommands[commandIndex]
    state.commandErrors.push({
      tick: state.tick,
      command,
      message: `Command ${command.sequence} is scheduled after the battle ended at tick ${state.tick}.`,
    })
    state.terminalReason = 'invalid-command'
  }
  return resultFromState(plan, seed, boundedCommands, state)
}

export function abortTdBattle(
  plan: TdBattlePlan,
  seed: string | number,
  commandLog: readonly TdCommand[] = [],
  abortTick = 0,
): TdBattleResult {
  const boundedCommands = cloneJson(commandLog.slice(0, plan.maxCommands + 1))
  const state = createTdSimulation(plan, seed)
  const logErrors = validateTdCommandLog(plan, boundedCommands)
  if (!Number.isInteger(abortTick) || abortTick < 0 || abortTick > plan.maxTicks) {
    logErrors.push(`Abort tick must be an integer from 0 through ${plan.maxTicks}.`)
  }
  if (boundedCommands.some(command => command.tick >= abortTick)) {
    logErrors.push('Abort command log contains a command that was not applied before the abort tick.')
  }
  if (logErrors.length > 0) {
    state.commandErrors.push({ tick: 0, command: boundedCommands[0] ?? null, message: logErrors[0] })
    state.terminalReason = 'invalid-command'
    return resultFromState(plan, seed, boundedCommands.slice(0, plan.maxCommands), state)
  }
  let commandIndex = 0
  while (!state.terminalReason && state.tick < abortTick) {
    const commands: TdCommand[] = []
    while (boundedCommands[commandIndex]?.tick === state.tick) {
      commands.push(boundedCommands[commandIndex])
      commandIndex += 1
    }
    stepTdSimulation(plan, state, commands)
  }
  if (state.terminalReason) return resultFromState(plan, seed, boundedCommands, state)
  state.terminalReason = 'aborted'
  return resultFromState(plan, seed, boundedCommands, state)
}
