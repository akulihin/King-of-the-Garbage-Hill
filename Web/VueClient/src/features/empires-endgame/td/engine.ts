import { resolveDamage } from '../combat/damage'
import type { CombatArmorProfile, CombatWeaponProfile } from '../combat/types'
import { createEmpiresRngState, nextEmpiresRandom } from '../rng'
import type {
  TdBattlePlan,
  TdBattleResult,
  TdCommand,
  TdDeploymentPlan,
  TdEnemyGroupDefinition,
  TdEnemyState,
  TdPoint,
  TdSimulationState,
  TdSquadState,
  TdTowerChoiceDefinition,
  TdTowerState,
} from './types'

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function finite(value: number): boolean {
  return Number.isFinite(value)
}

function fnv1a(value: string): string {
  let hash = 0x811c9dc5
  for (let index = 0; index < value.length; index += 1) {
    hash ^= value.charCodeAt(index)
    hash = Math.imul(hash, 0x01000193)
  }
  return (hash >>> 0).toString(16).padStart(8, '0')
}

export function digestTdValue(value: unknown): string {
  return fnv1a(JSON.stringify(value))
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

function routeDistance(plan: TdBattlePlan, enemy: TdEnemyState): number {
  const nodes = nodesById(plan)
  const edges = edgesById(plan)
  let distance = 0
  for (let index = 0; index < enemy.routeEdgeIds.length; index += 1) {
    const edge = edges.get(enemy.routeEdgeIds[index])
    if (!edge) continue
    const from = nodes.get(edge.fromNodeId)
    const to = nodes.get(edge.toNodeId)
    if (!from || !to) continue
    const length = pointDistance(from, to)
    if (index < enemy.edgeIndex) distance += length
    else if (index === enemy.edgeIndex) distance += length * enemy.edgeProgress
  }
  return distance
}

function positionOnRoute(plan: TdBattlePlan, enemy: TdEnemyState): TdPoint {
  const nodes = nodesById(plan)
  const edges = edgesById(plan)
  const edge = edges.get(enemy.routeEdgeIds[Math.min(enemy.edgeIndex, enemy.routeEdgeIds.length - 1)])
  if (!edge) return { x: enemy.x, y: enemy.y }
  const from = nodes.get(edge.fromNodeId)
  const to = nodes.get(edge.toNodeId)
  if (!from || !to) return { x: enemy.x, y: enemy.y }
  const progress = Math.max(0, Math.min(1, enemy.edgeProgress))
  return {
    x: from.x + (to.x - from.x) * progress,
    y: from.y + (to.y - from.y) * progress,
  }
}

function towerChoices(plan: TdBattlePlan, tower: TdTowerState): TdTowerChoiceDefinition[] {
  const choices = new Map(plan.towerChoices.map(choice => [choice.id, choice]))
  return tower.choiceIds.flatMap(choiceId => choices.get(choiceId) ?? [])
}

function towerWeapon(plan: TdBattlePlan, tower: TdTowerState): CombatWeaponProfile {
  const weapon = cloneJson(plan.towerBase.weapon)
  for (const choice of towerChoices(plan, tower)) {
    for (const [damageTypeId, amount] of Object.entries(choice.damageLevelBonuses)) {
      weapon.damageLevels[damageTypeId] = (weapon.damageLevels[damageTypeId] ?? 0) + amount
    }
  }
  return weapon
}

function towerRange(plan: TdBattlePlan, tower: TdTowerState): number {
  return Math.max(0, plan.towerBase.range
    + towerChoices(plan, tower).reduce((sum, choice) => sum + choice.rangeBonus, 0))
}

function towerAttackInterval(plan: TdBattlePlan, tower: TdTowerState): number {
  return Math.max(1, Math.round(plan.towerBase.attackIntervalTicks
    + towerChoices(plan, tower).reduce((sum, choice) => sum + choice.attackIntervalTicksDelta, 0)))
}

function towerProjectiles(plan: TdBattlePlan, tower: TdTowerState): number {
  return Math.max(1, Math.round(plan.towerBase.projectiles
    + towerChoices(plan, tower).reduce((sum, choice) => sum + choice.projectileBonus, 0)))
}

function towerPriority(plan: TdBattlePlan, tower: TdTowerState) {
  const choices = towerChoices(plan, tower)
  return choices[choices.length - 1]?.targetPriority ?? plan.towerBase.targetPriority
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

function validateCommand(
  plan: TdBattlePlan,
  state: TdSimulationState,
  command: TdCommand,
): string | null {
  if (!Number.isInteger(command.tick) || command.tick < 0) return 'Command tick must be a non-negative integer.'
  if (command.tick !== state.tick) return `Command belongs to tick ${command.tick}, not ${state.tick}.`
  const spot = plan.battlefield.buildSpots.find(item => item.id === command.spotId)
  if (!spot) return `Unknown build spot ${command.spotId}.`
  const choice = plan.towerChoices.find(item => item.id === command.choiceId)
  if (!choice) return `Unknown tower choice ${command.choiceId}.`
  const tower = state.towers.find(item => item.spotId === command.spotId)
  if (command.kind === 'build-tower') {
    if (tower) return `Build spot ${command.spotId} is occupied.`
    if (choice.grade !== 1) return 'A new tower must start with a grade-1 choice.'
  } else {
    if (!tower) return `Build spot ${command.spotId} has no tower to upgrade.`
    if (choice.grade !== tower.choiceIds.length + 1) {
      return `Tower ${command.spotId} requires grade ${tower.choiceIds.length + 1}.`
    }
  }
  if (state.buildResources < choice.cost) return `Not enough build resources for ${choice.id}.`
  return null
}

function applyCommand(plan: TdBattlePlan, state: TdSimulationState, command: TdCommand): boolean {
  const error = validateCommand(plan, state, command)
  if (error) {
    state.commandErrors.push({ tick: state.tick, command: cloneJson(command), message: error })
    state.terminalReason = 'invalid-command'
    return false
  }
  const choice = plan.towerChoices.find(item => item.id === command.choiceId)!
  state.buildResources -= choice.cost
  if (command.kind === 'build-tower') {
    const maxHp = plan.towerBase.maxHp + choice.maxHpBonus
    state.towers.push({
      spotId: command.spotId,
      choiceIds: [choice.id],
      hp: maxHp,
      nextAttackTick: state.tick,
    })
  } else {
    const tower = state.towers.find(item => item.spotId === command.spotId)!
    tower.choiceIds.push(choice.id)
    tower.hp += choice.maxHpBonus
  }
  return true
}

function spawnEnemy(
  plan: TdBattlePlan,
  state: TdSimulationState,
  group: TdEnemyGroupDefinition,
): void {
  const edge = edgesById(plan).get(group.routeEdgeIds[0])
  const node = edge ? nodesById(plan).get(edge.fromNodeId) : null
  const jitter = (nextEmpiresRandom(state.rng) - 0.5) * 0.000_001
  state.enemies.push({
    id: `enemy-${state.nextEnemyId}`,
    groupId: group.id,
    routeEdgeIds: [...group.routeEdgeIds],
    edgeIndex: 0,
    edgeProgress: Math.max(0, jitter),
    x: node?.x ?? 0,
    y: node?.y ?? 0,
    hp: group.maxHp,
    maxHp: group.maxHp,
    nextAttackTick: state.tick,
  })
  state.nextEnemyId += 1
  state.spawnedByGroup[group.id] = (state.spawnedByGroup[group.id] ?? 0) + 1
}

function spawnDueEnemies(plan: TdBattlePlan, state: TdSimulationState): void {
  for (const group of plan.wave.groups) {
    const spawned = state.spawnedByGroup[group.id] ?? 0
    if (spawned >= group.count) continue
    if (state.tick >= group.startTick + spawned * group.spawnIntervalTicks) {
      spawnEnemy(plan, state, group)
    }
  }
}

function livingEnemies(state: TdSimulationState): TdEnemyState[] {
  return state.enemies.filter(enemy => enemy.hp > 0)
}

function livingSquads(state: TdSimulationState): TdSquadState[] {
  return state.squads.filter(squad => squad.hp > 0)
}

function towerAttacks(plan: TdBattlePlan, state: TdSimulationState): void {
  const spots = new Map(plan.battlefield.buildSpots.map(spot => [spot.id, spot]))
  for (const tower of state.towers.filter(candidate => candidate.hp > 0)) {
    if (tower.nextAttackTick > state.tick) continue
    const spot = spots.get(tower.spotId)
    if (!spot) continue
    const inRange = livingEnemies(state).filter(enemy => pointDistance(spot, enemy) <= towerRange(plan, tower))
    if (inRange.length === 0) continue
    const priority = towerPriority(plan, tower)
    inRange.sort((left, right) => priority === 'strongest'
      ? right.maxHp - left.maxHp || routeDistance(plan, right) - routeDistance(plan, left) || left.id.localeCompare(right.id)
      : routeDistance(plan, right) - routeDistance(plan, left) || left.id.localeCompare(right.id))
    const weapon = towerWeapon(plan, tower)
    for (let projectile = 0; projectile < towerProjectiles(plan, tower); projectile += 1) {
      const target = inRange.find(enemy => enemy.hp > 0)
      if (!target) break
      target.hp = Math.max(0, target.hp - recordHit(state, weapon, plan.wave.groups
        .find(group => group.id === target.groupId)?.armor ?? null, plan))
    }
    tower.nextAttackTick = state.tick + towerAttackInterval(plan, tower)
  }
}

function squadAttacks(plan: TdBattlePlan, state: TdSimulationState): void {
  const deployments = new Map(plan.deployments.map(item => [item.id, item]))
  for (const squad of livingSquads(state)) {
    if (squad.nextAttackTick > state.tick) continue
    const deployment = deployments.get(squad.deploymentId)
    if (!deployment) continue
    const targets = livingEnemies(state)
      .filter(enemy => pointDistance(squad, enemy) <= deployment.attackRange)
      .sort((left, right) => routeDistance(plan, right) - routeDistance(plan, left) || left.id.localeCompare(right.id))
    const target = targets[0]
    if (!target) continue
    const alive = Math.max(1, Math.ceil(squad.hp / deployment.maxHpPerUnit))
    const armor = plan.wave.groups.find(group => group.id === target.groupId)?.armor ?? null
    let volleyDamage = 0
    for (let attacker = 0; attacker < alive; attacker += 1) {
      volleyDamage += recordHit(state, deployment.weapon, armor, plan)
    }
    target.hp = Math.max(0, target.hp - volleyDamage)
    squad.nextAttackTick = state.tick + Math.max(1, deployment.attackIntervalTicks)
  }
}

function moveEnemy(plan: TdBattlePlan, enemy: TdEnemyState): boolean {
  const group = plan.wave.groups.find(item => item.id === enemy.groupId)
  if (!group) return true
  const nodes = nodesById(plan)
  const edges = edgesById(plan)
  let remaining = group.speedPerSecond * plan.tickMs / 1_000
  while (remaining > 0 && enemy.edgeIndex < enemy.routeEdgeIds.length) {
    const edge = edges.get(enemy.routeEdgeIds[enemy.edgeIndex])
    if (!edge) return true
    const from = nodes.get(edge.fromNodeId)
    const to = nodes.get(edge.toNodeId)
    if (!from || !to) return true
    const length = Math.max(Number.EPSILON, pointDistance(from, to))
    const distanceLeft = length * (1 - enemy.edgeProgress)
    if (remaining < distanceLeft) {
      enemy.edgeProgress += remaining / length
      remaining = 0
    } else {
      remaining -= distanceLeft
      enemy.edgeIndex += 1
      enemy.edgeProgress = 0
    }
  }
  if (enemy.edgeIndex >= enemy.routeEdgeIds.length) {
    const castle = nodes.get(plan.battlefield.castleNodeId)
    if (castle) {
      enemy.x = castle.x
      enemy.y = castle.y
    }
    return true
  }
  const point = positionOnRoute(plan, enemy)
  enemy.x = point.x
  enemy.y = point.y
  return false
}

function enemyActions(plan: TdBattlePlan, state: TdSimulationState): void {
  const deployments = new Map(plan.deployments.map(item => [item.id, item]))
  const spots = new Map(plan.battlefield.buildSpots.map(spot => [spot.id, spot]))
  for (const enemy of livingEnemies(state)) {
    const group = plan.wave.groups.find(item => item.id === enemy.groupId)
    if (!group) continue
    const squad = livingSquads(state)
      .filter(candidate => pointDistance(candidate, enemy) <= group.attackRange)
      .sort((left, right) => pointDistance(left, enemy) - pointDistance(right, enemy)
        || left.deploymentId.localeCompare(right.deploymentId))[0]
    if (squad) {
      if (enemy.nextAttackTick <= state.tick) {
        const armor = deployments.get(squad.deploymentId)?.armor ?? null
        squad.hp = Math.max(0, squad.hp - recordHit(state, group.weapon, armor, plan))
        enemy.nextAttackTick = state.tick + Math.max(1, group.attackIntervalTicks)
      }
      continue
    }
    const towerTarget = state.towers
      .filter(tower => tower.hp > 0)
      .flatMap((tower) => {
        const spot = spots.get(tower.spotId)
        return spot ? [{ tower, distance: pointDistance(spot, enemy) }] : []
      })
      .filter(candidate => candidate.distance <= group.attackRange)
      .sort((left, right) => left.distance - right.distance
        || left.tower.spotId.localeCompare(right.tower.spotId))[0]?.tower
    if (towerTarget) {
      if (enemy.nextAttackTick <= state.tick) {
        towerTarget.hp = Math.max(
          0,
          towerTarget.hp - recordHit(state, group.weapon, null, plan),
        )
        if (towerTarget.hp <= 0) {
          state.towers = state.towers.filter(tower => tower !== towerTarget)
        }
        enemy.nextAttackTick = state.tick + Math.max(1, group.attackIntervalTicks)
      }
      continue
    }
    const reachedCastle = moveEnemy(plan, enemy)
    if (reachedCastle && enemy.nextAttackTick <= state.tick) {
      state.castleHp = Math.max(
        0,
        state.castleHp - recordHit(state, group.weapon, plan.battlefield.castleArmor, plan),
      )
      enemy.nextAttackTick = state.tick + Math.max(1, group.attackIntervalTicks)
    }
  }
}

function allEnemiesSpawned(plan: TdBattlePlan, state: TdSimulationState): boolean {
  return plan.wave.groups.every(group => (state.spawnedByGroup[group.id] ?? 0) >= group.count)
}

function resolveTerminal(plan: TdBattlePlan, state: TdSimulationState): void {
  if (state.terminalReason) return
  if (state.castleHp <= 0) {
    state.terminalReason = 'castle-destroyed'
    return
  }
  if (allEnemiesSpawned(plan, state) && livingEnemies(state).length === 0) {
    state.terminalReason = 'all-waves-defeated'
    return
  }
  if (state.tick >= plan.maxTicks) state.terminalReason = 'tick-cap'
}

function squadFromDeployment(plan: TdBattlePlan, deployment: TdDeploymentPlan): TdSquadState {
  const node = nodesById(plan).get(deployment.nodeId)
  return {
    deploymentId: deployment.id,
    cityId: deployment.cityId,
    unitId: deployment.unitId,
    count: deployment.count,
    hp: deployment.count * deployment.maxHpPerUnit,
    maxHp: deployment.count * deployment.maxHpPerUnit,
    nextAttackTick: 0,
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
  const damageTypeIds = new Set(plan.combat.damageTypes.map(definition => definition.id))
  const armorClassIds = new Set(plan.combat.armorClasses.map(definition => definition.id))
  const validateWeapon = (weapon: CombatWeaponProfile, path: string) => {
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
    if (!armorClassIds.has(armor.classId)) errors.push(`${path} uses unknown armor class ${armor.classId}`)
    if (!finite(armor.level) || armor.level < 0) errors.push(`${path} level must be finite and non-negative`)
  }
  if (!plan.id.trim()) errors.push('plan id is required')
  if (plan.mode !== 'defense') errors.push('only defense mode is supported')
  if (!plan.combat.enabled) errors.push('plan combat must be enabled')
  if (!Number.isInteger(plan.scheduledCon) || plan.scheduledCon <= 0) {
    errors.push('scheduledCon must be a positive integer')
  }
  if (!finite(plan.threat) || plan.threat < 0) errors.push('threat must be finite and non-negative')
  if (!Number.isInteger(plan.tickMs) || plan.tickMs <= 0) errors.push('tickMs must be a positive integer')
  if (!Number.isInteger(plan.maxTicks) || plan.maxTicks <= 0) errors.push('maxTicks must be a positive integer')
  if (!finite(plan.startingBuildResources) || plan.startingBuildResources < 0) {
    errors.push('startingBuildResources must be finite and non-negative')
  }
  if (!nodeIds.has(plan.battlefield.spawnerNodeId)) errors.push('battlefield spawnerNodeId is unknown')
  if (!nodeIds.has(plan.battlefield.castleNodeId)) errors.push('battlefield castleNodeId is unknown')
  if (!nodeIds.has(plan.battlefield.deploymentNodeId)) errors.push('battlefield deploymentNodeId is unknown')
  if (!finite(plan.battlefield.castleMaxHp) || plan.battlefield.castleMaxHp <= 0) {
    errors.push('battlefield castleMaxHp must be finite and positive')
  }
  validateArmor(plan.battlefield.castleArmor, 'battlefield castle armor')
  for (const edge of plan.battlefield.laneGraph.edges) {
    if (!nodeIds.has(edge.fromNodeId) || !nodeIds.has(edge.toNodeId)) {
      errors.push(`edge ${edge.id} references an unknown node`)
    }
  }
  validateWeapon(plan.towerBase.weapon, 'tower base weapon')
  if (!finite(plan.towerBase.maxHp) || plan.towerBase.maxHp <= 0) {
    errors.push('tower base maxHp must be finite and positive')
  }
  if (!finite(plan.towerBase.range) || plan.towerBase.range <= 0) {
    errors.push('tower base range must be finite and positive')
  }
  if (!Number.isInteger(plan.towerBase.attackIntervalTicks) || plan.towerBase.attackIntervalTicks <= 0) {
    errors.push('tower base attackIntervalTicks must be a positive integer')
  }
  if (!Number.isInteger(plan.towerBase.projectiles) || plan.towerBase.projectiles <= 0) {
    errors.push('tower base projectiles must be a positive integer')
  }
  if (choiceIds.size !== plan.towerChoices.length) errors.push('tower choice ids must be unique')
  for (const choice of plan.towerChoices) {
    if (!finite(choice.cost) || choice.cost < 0) errors.push(`tower choice ${choice.id} cost is invalid`)
    for (const [damageTypeId, bonus] of Object.entries(choice.damageLevelBonuses)) {
      if (!damageTypeIds.has(damageTypeId)) {
        errors.push(`tower choice ${choice.id} uses unknown damage type ${damageTypeId}`)
      }
      if (!finite(bonus)) errors.push(`tower choice ${choice.id} damage bonus is not finite`)
    }
  }
  for (const grade of [1, 2, 3, 4]) {
    if (plan.towerChoices.filter(choice => choice.grade === grade).length !== 4) {
      errors.push(`tower grade ${grade} must contain exactly four choices`)
    }
  }
  for (const group of plan.wave.groups) {
    if (!Number.isInteger(group.count) || group.count <= 0) errors.push(`group ${group.id} count must be positive`)
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
    if (nodeId !== plan.battlefield.castleNodeId) {
      errors.push(`group ${group.id} route does not reach the castle`)
    }
    if (!Number.isInteger(group.startTick) || group.startTick < 0) {
      errors.push(`group ${group.id} startTick must be a non-negative integer`)
    }
    if (!Number.isInteger(group.spawnIntervalTicks) || group.spawnIntervalTicks <= 0) {
      errors.push(`group ${group.id} spawnIntervalTicks must be positive`)
    }
    if (!finite(group.maxHp) || group.maxHp <= 0) errors.push(`group ${group.id} maxHp is invalid`)
    if (!finite(group.speedPerSecond) || group.speedPerSecond <= 0) {
      errors.push(`group ${group.id} speedPerSecond is invalid`)
    }
    if (!finite(group.attackRange) || group.attackRange < 0) errors.push(`group ${group.id} attackRange is invalid`)
    if (!Number.isInteger(group.attackIntervalTicks) || group.attackIntervalTicks <= 0) {
      errors.push(`group ${group.id} attackIntervalTicks must be positive`)
    }
    validateWeapon(group.weapon, `group ${group.id} weapon`)
    validateArmor(group.armor, `group ${group.id} armor`)
  }
  for (const deployment of plan.deployments) {
    if (!nodeIds.has(deployment.nodeId)) errors.push(`deployment ${deployment.id} references an unknown node`)
    if (!Number.isInteger(deployment.count) || deployment.count <= 0) {
      errors.push(`deployment ${deployment.id} count must be positive`)
    }
    if (!finite(deployment.maxHpPerUnit) || deployment.maxHpPerUnit <= 0) {
      errors.push(`deployment ${deployment.id} maxHpPerUnit is invalid`)
    }
    if (!finite(deployment.attackRange) || deployment.attackRange < 0) {
      errors.push(`deployment ${deployment.id} attackRange is invalid`)
    }
    if (!Number.isInteger(deployment.attackIntervalTicks) || deployment.attackIntervalTicks <= 0) {
      errors.push(`deployment ${deployment.id} attackIntervalTicks must be positive`)
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
    castleHp: plan.battlefield.castleMaxHp,
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
  towerAttacks(plan, state)
  squadAttacks(plan, state)
  enemyActions(plan, state)
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
    ? 'victory'
    : terminalReason === 'castle-destroyed'
      ? 'defeat'
      : terminalReason === 'aborted'
        ? 'aborted'
        : 'error'
  const enemiesSpawned = Object.values(state.spawnedByGroup).reduce((sum, count) => sum + count, 0)
  return {
    kind: 'td',
    planId: plan.id,
    planDigest: digestTdValue(plan),
    seed,
    outcome,
    terminalReason,
    ticks: state.tick,
    castleHp: state.castleHp,
    castleMaxHp: plan.battlefield.castleMaxHp,
    enemiesSpawned,
    enemiesDefeated: state.enemies.filter(enemy => enemy.hp <= 0).length,
    deployments,
    buildResourcesRemaining: state.buildResources,
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
  const immutableCommands = cloneJson(commandLog)
    .map((command, index) => ({ command, index }))
    .sort((left, right) => left.command.tick - right.command.tick || left.index - right.index)
    .map(entry => entry.command)
  const state = createTdSimulation(plan, seed)
  let commandIndex = 0
  while (!state.terminalReason) {
    const commands: TdCommand[] = []
    while (immutableCommands[commandIndex]?.tick === state.tick) {
      commands.push(immutableCommands[commandIndex])
      commandIndex += 1
    }
    if (immutableCommands[commandIndex]?.tick < state.tick) {
      state.commandErrors.push({
        tick: state.tick,
        command: immutableCommands[commandIndex],
        message: `Command log is not monotonic at tick ${state.tick}.`,
      })
      state.terminalReason = 'invalid-command'
      break
    }
    stepTdSimulation(plan, state, commands)
  }
  return resultFromState(plan, seed, immutableCommands, state)
}

export function abortTdBattle(
  plan: TdBattlePlan,
  seed: string | number,
  commandLog: readonly TdCommand[] = [],
): TdBattleResult {
  const state = createTdSimulation(plan, seed)
  state.terminalReason = 'aborted'
  return resultFromState(plan, seed, commandLog, state)
}
