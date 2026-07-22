import { createEmpiresRngState, nextEmpiresRandom } from '../rng'
import { EMPIRES_STABILIZATION_BUDGETS } from '../stabilization'
import { digestTdValue } from '../td/engine'
import type {
  ClashAbilityDefinition,
  ClashCellState,
  ClashCommand,
  ClashCorpseState,
  ClashDeploymentResult,
  ClashFieldVariantDefinition,
  ClashLogEntry,
  ClashPassiveDefinition,
  ClashPlan,
  ClashPlanUnit,
  ClashRegionModifierDefinition,
  ClashResult,
  ClashRulesIdentity,
  ClashSide,
  ClashSimulationState,
  ClashStatusDefinition,
  ClashTerrainDefinition,
  ClashUnitDefinition,
  ClashUnitState,
  EmpiresClashConfig,
} from './types'

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function opposite(side: ClashSide): ClashSide {
  return side === 'attacker' ? 'defender' : 'attacker'
}

function finiteNonNegative(value: number): boolean {
  return Number.isFinite(value) && value >= 0
}

function positiveInteger(value: number): boolean {
  return Number.isInteger(value) && value > 0
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function nonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

function validSide(value: unknown): value is ClashSide {
  return value === 'attacker' || value === 'defender'
}

export function createClashRulesIdentity(
  configSchemaVersion: number,
  clash: EmpiresClashConfig,
  extraRules?: unknown,
): ClashRulesIdentity {
  return {
    configSchemaVersion,
    rulesDigest: digestTdValue({
      clash,
      ...(extraRules === undefined ? {} : { extraRules }),
    }),
  }
}

export function validateClashConfig(config: EmpiresClashConfig): string[] {
  const errors: string[] = []
  if (typeof config.enabled !== 'boolean') errors.push('enabled must be boolean')
  if (!positiveInteger(config.resultLogLimit)) errors.push('resultLogLimit must be positive')
  if (!positiveInteger(config.maxTurns)) errors.push('maxTurns must be positive')
  if (!positiveInteger(config.maxCommands)) errors.push('maxCommands must be positive')
  if (config.resultLogLimit > EMPIRES_STABILIZATION_BUDGETS.maxResultRetention) {
    errors.push('resultLogLimit exceeds the shipped safety ceiling')
  }
  if (config.maxTurns > EMPIRES_STABILIZATION_BUDGETS.maxCommands) {
    errors.push('maxTurns exceeds the shipped safety ceiling')
  }
  if (config.maxCommands > EMPIRES_STABILIZATION_BUDGETS.maxCommands) {
    errors.push('maxCommands exceeds the shipped safety ceiling')
  }
  if (!['attacker', 'defender'].includes(config.placementFirstSide)) {
    errors.push('placementFirstSide is invalid')
  }
  if (!['attacker', 'defender'].includes(config.betweenClashesFirstSide)) {
    errors.push('betweenClashesFirstSide is invalid')
  }
  if (!['attacker-first', 'defender-first'].includes(config.speedTieRule)) {
    errors.push('speedTieRule is invalid')
  }
  if (!['attacker', 'defender'].includes(config.turnCapTieWinner)) {
    errors.push('turnCapTieWinner is invalid')
  }
  if (config.victoryRule !== 'elimination') errors.push('victoryRule is unsupported')

  const uniqueIds = <T extends { id: string }>(values: readonly T[], path: string): Set<string> => {
    const ids = new Set<string>()
    for (const value of values) {
      if (!value.id?.trim()) errors.push(`${path} has an empty id`)
      else if (ids.has(value.id)) errors.push(`${path} repeats ${value.id}`)
      ids.add(value.id)
    }
    return ids
  }
  const fieldIds = uniqueIds(config.fieldVariants, 'fieldVariants')
  const statusIds = uniqueIds(config.statuses, 'statuses')
  const terrainIds = uniqueIds(config.terrain, 'terrain')
  uniqueIds(config.regions, 'regions')
  const unitIds = uniqueIds(config.roster, 'roster')
  uniqueIds(config.assaultRoutes, 'assaultRoutes')
  if (!fieldIds.has(config.defaultFieldVariantId)) {
    errors.push('defaultFieldVariantId is dangling')
  }
  for (const field of config.fieldVariants) {
    if (!positiveInteger(field.columns) || !positiveInteger(field.rowsPerSide)
      || !Number.isInteger(field.reinforcementRows) || field.reinforcementRows < 0
      || field.reinforcementRows >= field.rowsPerSide
      || !positiveInteger(field.unitCountMultiplier)) {
      errors.push(`field ${field.id} has invalid dimensions`)
    }
    const occupiedTerrainCells = new Set<string>()
    for (const cell of field.terrainCellIds) {
      const key = `${cell.side}:${cell.row}:${cell.column}`
      if (occupiedTerrainCells.has(key)) errors.push(`field ${field.id} repeats terrain cell ${key}`)
      occupiedTerrainCells.add(key)
      if (!terrainIds.has(cell.terrainId)
        || !Number.isInteger(cell.row) || cell.row < 0 || cell.row >= field.rowsPerSide
        || !Number.isInteger(cell.column) || cell.column < 0 || cell.column >= field.columns) {
        errors.push(`field ${field.id} has invalid terrain cell`)
      }
    }
  }
  for (const status of config.statuses) {
    if (!status.name?.trim()) errors.push(`status ${status.id} needs a name`)
    if (status.damagePerTurn !== undefined && !finiteNonNegative(status.damagePerTurn)) {
      errors.push(`status ${status.id} has invalid damagePerTurn`)
    }
    if (status.durationTurns !== undefined && status.durationTurns !== null
      && !positiveInteger(status.durationTurns)) errors.push(`status ${status.id} has invalid duration`)
  }
  for (const unit of config.roster) {
    const missingStats = unit.attack === null || unit.maxHp === null || unit.speed === null
    if (!unit.name?.trim()
      || !missingStats && (!Number.isInteger(unit.attack!) || !Number.isInteger(unit.maxHp!)
        || !Number.isInteger(unit.speed!) || !finiteNonNegative(unit.attack!)
        || !finiteNonNegative(unit.maxHp!) || !finiteNonNegative(unit.speed!)
        || unit.maxHp! <= 0 || unit.attack! > 9 || unit.maxHp! > 9 || unit.speed! > 9)
      || missingStats && !unit.deferredReason && !unit.reviewReason
      || unit.regions.length === 0
      || !Array.isArray(unit.ranks) || !Array.isArray(unit.acquisitionTags)) {
      errors.push(`unit ${unit.id} has invalid identity or stats`)
    }
    if (unit.sourceMessageIds.length === 0) errors.push(`unit ${unit.id} has no source message`)
    for (const passive of unit.passives) {
      if (!passive.id?.trim() || !passive.name?.trim() || !passive.description?.trim()) {
        errors.push(`unit ${unit.id} has an incomplete passive`)
      }
      if (passive.statusId && !statusIds.has(passive.statusId)) {
        errors.push(`unit ${unit.id} passive ${passive.id} references unknown status`)
      }
    }
    for (const ability of unit.abilities) {
      if (!ability.id?.trim() || !ability.name?.trim() || !positiveInteger(ability.charges)
        || !Number.isInteger(ability.reloadTurns) || ability.reloadTurns < 0) {
        errors.push(`unit ${unit.id} has an invalid ability`)
      }
      if (ability.statusId && !statusIds.has(ability.statusId)) {
        errors.push(`unit ${unit.id} ability ${ability.id} references unknown status`)
      }
      if (ability.spawnUnitId && !unitIds.has(ability.spawnUnitId)) {
        errors.push(`unit ${unit.id} ability ${ability.id} references unknown spawn unit`)
      }
    }
    if (unit.deferredReason !== undefined && !unit.deferredReason.trim()) {
      errors.push(`unit ${unit.id} has an empty deferredReason`)
    }
    if (unit.reviewReason !== undefined && !unit.reviewReason.trim()) {
      errors.push(`unit ${unit.id} has an empty reviewReason`)
    }
  }
  const morale = config.morale
  if (!Number.isFinite(morale.minimum) || !Number.isFinite(morale.maximum)
    || morale.minimum > morale.maximum || !positiveInteger(morale.positiveActivationCharges)
    || !positiveInteger(morale.neutralActivationCharges)
    || !positiveInteger(morale.negativeActivationCooldownTurns)) {
    errors.push('morale rules are invalid')
  }
  for (const [key, value] of Object.entries(config.settlement)) {
    if (!Number.isInteger(value)) errors.push(`settlement.${key} must be an integer`)
  }
  for (const route of config.assaultRoutes) {
    if (!['campaign', 'expedition'].includes(route.sourceKind)) {
      errors.push(`assault route ${route.id} has an invalid sourceKind`)
    }
    if (!route.sourceId?.trim() || !['td', 'clash'].includes(route.battleMode)) {
      errors.push(`assault route ${route.id} has an invalid source or battleMode`)
    }
    if (route.clashVariantId !== null && !fieldIds.has(route.clashVariantId)) {
      errors.push(`assault route ${route.id} references unknown Clash field`)
    }
    if (route.battleMode === 'clash' && route.clashVariantId === null) {
      errors.push(`assault route ${route.id} needs a Clash field`)
    }
    if (route.deferredReason !== undefined && !route.deferredReason.trim()) {
      errors.push(`assault route ${route.id} has an empty deferredReason`)
    }
  }
  if (config.enabled) {
    errors.push('enabled clash requires an authored campaign roster mapping and launch contract')
    if (config.fieldVariants.length === 0 || config.statuses.length === 0
      || config.terrain.length === 0 || config.regions.length === 0 || config.roster.length === 0) {
      errors.push('enabled clash requires complete catalogs')
    }
    const defaultField = config.fieldVariants.find(field => field.id === config.defaultFieldVariantId)
    const liveFields = config.fieldVariants.filter(field => !field.deferredReason)
    const liveRegions = config.regions.filter(region => !region.deferredReason)
    const liveUnits = config.roster.filter(unit => !unit.deferredReason && !unit.reviewReason)
    if (defaultField?.deferredReason) errors.push('enabled clash default field cannot be deferred')
    if (liveFields.length === 0 || liveRegions.length === 0 || liveUnits.length === 0) {
      errors.push('enabled clash requires a reachable live field, region, and unit')
    }
    if (!config.assaultRoutes.some(route => route.battleMode === 'clash' && !route.deferredReason)) {
      errors.push('enabled clash requires a live Clash assault route')
    }
    const statusById = new Map(config.statuses.map(status => [status.id, status]))
    const unitById = new Map(config.roster.map(unit => [unit.id, unit]))
    const terrainById = new Map(config.terrain.map(terrain => [terrain.id, terrain]))
    for (const field of liveFields) {
      for (const cell of field.terrainCellIds) {
        if (terrainById.get(cell.terrainId)?.deferredReason) {
          errors.push(`live field ${field.id} references deferred terrain ${cell.terrainId}`)
        }
      }
    }
    for (const unit of liveUnits) {
      for (const statusId of [
        ...unit.passives.map(item => item.statusId),
        ...unit.abilities.map(item => item.statusId),
      ].filter((value): value is string => Boolean(value))) {
        if (statusById.get(statusId)?.deferredReason) {
          errors.push(`live unit ${unit.id} references deferred status ${statusId}`)
        }
      }
      for (const ability of unit.abilities) {
        if (ability.spawnUnitId) {
          const spawned = unitById.get(ability.spawnUnitId)
          if (spawned?.deferredReason || spawned?.reviewReason) {
            errors.push(`live unit ${unit.id} references unavailable spawn ${ability.spawnUnitId}`)
          }
        }
      }
    }
    const liveStatusKinds = new Set(config.statuses
      .filter(status => !status.deferredReason)
      .map(status => status.kind))
    if ((liveStatusKinds.has('scorpion-poison') || liveStatusKinds.has('corpse-centipede-poison'))
      && !liveStatusKinds.has('paralysis')) {
      errors.push('live poison statuses require a live paralysis status')
    }
  }
  return errors
}

export function createClashPlan(input: {
  id: string
  sessionId: string
  rulesIdentity: ClashRulesIdentity
  config: EmpiresClashConfig
  field: ClashFieldVariantDefinition
  region: ClashRegionModifierDefinition
  roster: ClashPlanUnit[]
  initialMorale?: Partial<Record<ClashSide, number>>
}): ClashPlan {
  const definitionIds = new Set(input.roster.map(unit => unit.definitionId))
  let addedDependency = true
  while (addedDependency) {
    addedDependency = false
    for (const definition of input.config.roster) {
      if (!definitionIds.has(definition.id)) continue
      for (const ability of definition.abilities) {
        if (ability.spawnUnitId && !definitionIds.has(ability.spawnUnitId)) {
          definitionIds.add(ability.spawnUnitId)
          addedDependency = true
        }
      }
    }
  }
  const units = input.config.roster
    .filter(unit => definitionIds.has(unit.id))
    .map(cloneJson)
    .sort((left, right) => stableCompare(left.id, right.id))
  return {
    id: input.id,
    sessionId: input.sessionId,
    rulesIdentity: cloneJson(input.rulesIdentity),
    field: cloneJson(input.field),
    region: cloneJson(input.region),
    units,
    roster: cloneJson(input.roster).sort((left, right) => stableCompare(left.instanceId, right.instanceId)),
    initialMorale: {
      attacker: input.initialMorale?.attacker ?? 0,
      defender: input.initialMorale?.defender ?? 0,
    },
    maxTurns: input.config.maxTurns,
    maxCommands: input.config.maxCommands,
    resultLogLimit: input.config.resultLogLimit,
    placementFirstSide: input.config.placementFirstSide,
    betweenClashesFirstSide: input.config.betweenClashesFirstSide,
    speedTieRule: input.config.speedTieRule,
    turnCapTieWinner: input.config.turnCapTieWinner,
    victoryRule: input.config.victoryRule,
    corpseBlocksAdvance: input.config.corpseBlocksAdvance,
    onePlacementPerSideBetweenClashes: input.config.onePlacementPerSideBetweenClashes,
    statuses: cloneJson(input.config.statuses),
    terrain: cloneJson(input.config.terrain),
    morale: cloneJson(input.config.morale),
  }
}

export function validateClashPlan(plan: ClashPlan): string[] {
  const errors: string[] = []
  if (!isRecord(plan)) return ['plan must be an object']
  if (!isRecord(plan.field) || !isRecord(plan.region) || !isRecord(plan.rulesIdentity)
    || !isRecord(plan.initialMorale) || !isRecord(plan.morale)
    || !Array.isArray(plan.units) || !Array.isArray(plan.roster)
    || !Array.isArray(plan.statuses) || !Array.isArray(plan.terrain)
    || !Array.isArray(plan.field.terrainCellIds)) {
    return ['plan shape is malformed']
  }
  if (!nonEmptyString(plan.id) || !nonEmptyString(plan.sessionId)) errors.push('plan identity is missing')
  if (!positiveInteger(plan.maxTurns) || !positiveInteger(plan.maxCommands)
    || !positiveInteger(plan.resultLogLimit)) {
    errors.push('plan budgets are invalid')
  }
  if (plan.maxTurns > EMPIRES_STABILIZATION_BUDGETS.maxCommands
    || plan.maxCommands > EMPIRES_STABILIZATION_BUDGETS.maxCommands
    || plan.resultLogLimit > EMPIRES_STABILIZATION_BUDGETS.maxResultRetention) {
    errors.push('plan budget exceeds the shipped safety ceiling')
  }
  if (!positiveInteger(plan.field.columns) || !positiveInteger(plan.field.rowsPerSide)
    || !Number.isInteger(plan.field.reinforcementRows) || plan.field.reinforcementRows < 0
    || plan.field.reinforcementRows >= plan.field.rowsPerSide
    || !positiveInteger(plan.field.unitCountMultiplier)
    || !nonEmptyString(plan.field.id) || !nonEmptyString(plan.field.name)) {
    errors.push('field dimensions are invalid')
  }
  if (plan.field.columns * plan.field.rowsPerSide * 2 > EMPIRES_STABILIZATION_BUDGETS.maxBoardCells
    || plan.field.terrainCellIds.length > EMPIRES_STABILIZATION_BUDGETS.maxBoardCells
    || plan.units.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
    || plan.statuses.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
    || plan.terrain.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
    || plan.roster.length > EMPIRES_STABILIZATION_BUDGETS.maxRosterUnitInstances) {
    errors.push('plan component or actor count exceeds the shipped safety ceiling')
  }
  if (plan.field.deferredReason !== undefined) {
    if (nonEmptyString(plan.field.deferredReason)) errors.push(`plan field ${plan.field.id} is deferred`)
    else errors.push('plan field has an empty deferred reason')
  }
  if (!nonEmptyString(plan.region.id) || !nonEmptyString(plan.region.name)
    || !Number.isFinite(plan.region.speedDelta)
    || !finiteNonNegative(plan.region.supplyMultiplier)
    || !Number.isInteger(plan.region.imperialCountBonus) || plan.region.imperialCountBonus < 0
    || !['cold', 'temperate', 'warm', 'hot'].includes(plan.region.temperature)
    || !['dry', 'neutral', 'humid'].includes(plan.region.humidity)
    || typeof plan.region.heatingRequired !== 'boolean') {
    errors.push('plan region is invalid')
  }
  if (plan.region.deferredReason !== undefined) {
    if (nonEmptyString(plan.region.deferredReason)) errors.push(`plan region ${plan.region.id} is deferred`)
    else errors.push('plan region has an empty deferred reason')
  }
  if (!plan.rulesIdentity || !positiveInteger(plan.rulesIdentity.configSchemaVersion)
    || !nonEmptyString(plan.rulesIdentity.rulesDigest)) errors.push('rules identity is invalid')
  if (!validSide(plan.placementFirstSide) || !validSide(plan.betweenClashesFirstSide)
    || !['attacker-first', 'defender-first'].includes(plan.speedTieRule)
    || !validSide(plan.turnCapTieWinner) || plan.victoryRule !== 'elimination'
    || typeof plan.corpseBlocksAdvance !== 'boolean'
    || typeof plan.onePlacementPerSideBetweenClashes !== 'boolean') {
    errors.push('plan turn rules are invalid')
  }

  const statusKinds = new Set([
    'bleeding', 'ignite', 'scorpion-poison', 'cobra-poison', 'centipede-poison',
    'karakurt-poison', 'corpse-centipede-poison', 'lhp-toxin', 'neuro-toxin',
    'wither', 'freeze', 'stun', 'dodge', 'paralysis', 'disarm', 'rage',
  ])
  const terrainKinds = new Set([
    'high-ground', 'healing-mushrooms', 'acid', 'cordyceps', 'trap', 'fog',
  ])
  const passiveKinds = new Set([
    'shield', 'ranged', 'legion', 'reach', 'adjacency-poke', 'anti-cavalry',
    'cavalry', 'heavy-armor', 'status-on-hit', 'status-immunity', 'heal', 'dodge',
    'retaliate', 'multi-strike', 'damage-modifier', 'damage-cap', 'reflect',
    'disarm', 'morale', 'climate-stat', 'corpse', 'spawn', 'first-strike',
    'terrain-immunity', 'death', 'kill-growth', 'row-buff', 'row-damage-share',
    'campaign',
  ])
  const abilityKinds = new Set([
    'damage', 'status', 'heal-full', 'cleanse', 'morale', 'cavalry-charge',
    'move-to-front', 'spawn',
  ])
  const abilityTargets = new Set([
    'self', 'ally', 'enemy', 'cell', 'row', 'column', 'all-enemies',
  ])
  const rankKinds = new Set([
    'elite', 'legend', 'hero', 'incredible', 'limited', 'convict', 'creature',
    'perst', 'one-of-kind',
  ])
  const costBands = new Set([
    null, 'cheapest', 'very-cheap', 'cheap', 'medium', 'expensive', 'very-expensive',
  ])

  const statusIds = new Set<string>()
  const statusById = new Map<string, ClashStatusDefinition>()
  for (const rawStatus of plan.statuses as unknown[]) {
    if (!isRecord(rawStatus)) {
      errors.push('plan has a malformed status')
      continue
    }
    const status = rawStatus as unknown as ClashStatusDefinition
    if (!nonEmptyString(status.id) || statusIds.has(status.id)) {
      errors.push(`duplicate or empty status ${String(status.id ?? '')}`)
      continue
    }
    statusIds.add(status.id)
    statusById.set(status.id, status)
    if (!nonEmptyString(status.name) || !statusKinds.has(status.kind)
      || typeof status.stacks !== 'boolean') errors.push(`status ${status.id} is invalid`)
    if (status.damagePerTurn !== undefined && !finiteNonNegative(status.damagePerTurn)) {
      errors.push(`status ${status.id} has invalid damage`)
    }
    if (status.durationTurns !== undefined && status.durationTurns !== null
      && !positiveInteger(status.durationTurns)) errors.push(`status ${status.id} has invalid duration`)
    for (const value of [status.attackDivisor, status.speedDivisor]) {
      if (value !== undefined && (!Number.isFinite(value) || value <= 0)) {
        errors.push(`status ${status.id} has an invalid divisor`)
      }
    }
    for (const value of [status.thresholdHpExclusive, status.delayedDeathTurns]) {
      if (value !== undefined && !positiveInteger(value)) {
        errors.push(`status ${status.id} has an invalid threshold or delay`)
      }
    }
    for (const value of [status.bypassesShields, status.clearsShields,
      status.wakesOnDamage, status.clearsRage]) {
      if (value !== undefined && typeof value !== 'boolean') {
        errors.push(`status ${status.id} has an invalid flag`)
      }
    }
    if (status.deferredReason !== undefined && !nonEmptyString(status.deferredReason)) {
      errors.push(`status ${status.id} has an empty deferred reason`)
    }
  }

  const terrainIds = new Set<string>()
  const terrainById = new Map<string, ClashTerrainDefinition>()
  for (const rawTerrain of plan.terrain as unknown[]) {
    if (!isRecord(rawTerrain)) {
      errors.push('plan has malformed terrain')
      continue
    }
    const terrain = rawTerrain as unknown as ClashTerrainDefinition
    if (!nonEmptyString(terrain.id) || terrainIds.has(terrain.id)) {
      errors.push(`duplicate or empty terrain ${String(terrain.id ?? '')}`)
      continue
    }
    terrainIds.add(terrain.id)
    terrainById.set(terrain.id, terrain)
    if (!nonEmptyString(terrain.name) || !terrainKinds.has(terrain.kind)) {
      errors.push(`terrain ${terrain.id} is invalid`)
    }
    if (terrain.speedDelta !== undefined && !Number.isFinite(terrain.speedDelta)) {
      errors.push(`terrain ${terrain.id} has invalid speed`)
    }
    for (const value of [terrain.durationTurns, terrain.archerCapacity]) {
      if (value !== undefined && !positiveInteger(value)) {
        errors.push(`terrain ${terrain.id} has an invalid duration or capacity`)
      }
    }
    for (const value of [terrain.healingPerTurn, terrain.damage]) {
      if (value !== undefined && !finiteNonNegative(value)) {
        errors.push(`terrain ${terrain.id} has invalid healing or damage`)
      }
    }
    if (terrain.maxHpMultiplier !== undefined
      && (!Number.isFinite(terrain.maxHpMultiplier) || terrain.maxHpMultiplier <= 0)) {
      errors.push(`terrain ${terrain.id} has an invalid maxHpMultiplier`)
    }
    for (const value of [terrain.duplicateActivations, terrain.hidesEnemyCell]) {
      if (value !== undefined && typeof value !== 'boolean') {
        errors.push(`terrain ${terrain.id} has an invalid flag`)
      }
    }
    if (terrain.deferredReason !== undefined && !nonEmptyString(terrain.deferredReason)) {
      errors.push(`terrain ${terrain.id} has an empty deferred reason`)
    }
  }

  const definitionIds = new Set<string>()
  const validUnitShapes: ClashUnitDefinition[] = []
  for (const rawUnit of plan.units as unknown[]) {
    if (!isRecord(rawUnit)) {
      errors.push('plan has a malformed unit definition')
      continue
    }
    const unit = rawUnit as unknown as ClashUnitDefinition
    if (!nonEmptyString(unit.id) || definitionIds.has(unit.id)) {
      errors.push(`duplicate unit definition ${String(unit.id ?? '')}`)
      continue
    }
    definitionIds.add(unit.id)
    if (!nonEmptyString(unit.name) || !nonEmptyString(unit.faction)
      || !Array.isArray(unit.regions) || unit.regions.length === 0
      || unit.regions.some(value => !nonEmptyString(value))
      || !Array.isArray(unit.ranks) || !Array.isArray(unit.acquisitionTags)
      || !Array.isArray(unit.tags) || !Array.isArray(unit.sourceMessageIds)
      || unit.ranks.some(value => !rankKinds.has(value))
      || unit.acquisitionTags.some(value => !nonEmptyString(value))
      || unit.tags.some(value => !nonEmptyString(value))
      || !costBands.has(unit.cost)
      || unit.sourceMessageIds.length === 0
      || unit.sourceMessageIds.some(value => !nonEmptyString(value))
      || !Array.isArray(unit.passives) || !Array.isArray(unit.abilities)) {
      errors.push(`unit definition ${unit.id} has an invalid shape`)
      continue
    }
    validUnitShapes.push(unit)
    if (!Number.isInteger(unit.attack) || !Number.isInteger(unit.maxHp) || !Number.isInteger(unit.speed)
      || unit.attack! < 0 || unit.maxHp! <= 0 || unit.speed! < 0
      || unit.attack! > 9 || unit.maxHp! > 9 || unit.speed! > 9) {
      errors.push(`unit definition ${unit.id} has invalid stats`)
    }
    if (unit.deferredReason || unit.reviewReason) {
      errors.push(`plan includes unavailable unit ${unit.id}`)
    }
    if (unit.limitPerGame !== undefined && !positiveInteger(unit.limitPerGame)) {
      errors.push(`unit definition ${unit.id} has invalid limitPerGame`)
    }
    if (unit.hireOnce !== undefined && typeof unit.hireOnce !== 'boolean') {
      errors.push(`unit definition ${unit.id} has invalid hireOnce`)
    }
    if (unit.passives.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems
      || unit.abilities.length > EMPIRES_STABILIZATION_BUDGETS.maxPlanItems) {
      errors.push(`unit definition ${unit.id} exceeds the component safety ceiling`)
    }
    const passiveIds = new Set<string>()
    for (const rawPassive of unit.passives as unknown[]) {
      if (!isRecord(rawPassive)) {
        errors.push(`unit ${unit.id} has a malformed passive`)
        continue
      }
      const passive = rawPassive as unknown as ClashPassiveDefinition
      if (!nonEmptyString(passive.id) || passiveIds.has(passive.id)
        || !nonEmptyString(passive.name) || !nonEmptyString(passive.description)
        || !passiveKinds.has(passive.kind)
        || !['weapon', 'armor', 'shield', 'unit'].includes(passive.category)) {
        errors.push(`unit ${unit.id} has an invalid or duplicate passive`)
        continue
      }
      passiveIds.add(passive.id)
      for (const value of [passive.value, passive.threshold, passive.multiplier]) {
        if (value !== undefined && !Number.isFinite(value)) {
          errors.push(`passive ${passive.id} has an invalid numeric value`)
        }
      }
      for (const value of [passive.charges, passive.reloadTurns, passive.range, passive.durationTurns]) {
        if (value !== undefined && (!Number.isInteger(value) || value < 0)) {
          errors.push(`passive ${passive.id} has an invalid counter`)
        }
      }
      if (passive.statusId !== undefined && !nonEmptyString(passive.statusId)) {
        errors.push(`passive ${passive.id} has an invalid status id`)
      }
      if (passive.targetTag !== undefined && !nonEmptyString(passive.targetTag)) {
        errors.push(`passive ${passive.id} has an invalid target tag`)
      }
      if (passive.area !== undefined
        && !['target', 'adjacent', 'row', 'column', 'all'].includes(passive.area)) {
        errors.push(`passive ${passive.id} has an invalid area`)
      }
      for (const value of [passive.hidden, passive.bypassesShields]) {
        if (value !== undefined && typeof value !== 'boolean') {
          errors.push(`passive ${passive.id} has an invalid flag`)
        }
      }
    }
    const abilityIds = new Set<string>()
    for (const rawAbility of unit.abilities as unknown[]) {
      if (!isRecord(rawAbility)) {
        errors.push(`unit ${unit.id} has a malformed ability`)
        continue
      }
      const ability = rawAbility as unknown as ClashAbilityDefinition
      if (!nonEmptyString(ability.id) || abilityIds.has(ability.id)
        || !nonEmptyString(ability.name) || !abilityKinds.has(ability.kind)
        || !abilityTargets.has(ability.target) || !positiveInteger(ability.charges)
        || !Number.isInteger(ability.reloadTurns) || ability.reloadTurns < 0) {
        errors.push(`unit ${unit.id} has an invalid or duplicate ability`)
        continue
      }
      abilityIds.add(ability.id)
      if (ability.value !== undefined && !Number.isFinite(ability.value)) {
        errors.push(`ability ${ability.id} has an invalid value`)
      }
      if (ability.durationTurns !== undefined && !positiveInteger(ability.durationTurns)) {
        errors.push(`ability ${ability.id} has an invalid duration`)
      }
      if (ability.statusId !== undefined && !nonEmptyString(ability.statusId)) {
        errors.push(`ability ${ability.id} has an invalid status id`)
      }
      if (ability.spawnUnitId !== undefined && !nonEmptyString(ability.spawnUnitId)) {
        errors.push(`ability ${ability.id} has an invalid spawn id`)
      }
      if (ability.area !== undefined
        && !['target', 'adjacent', '2x2', 'row', 'column', 'all'].includes(ability.area)) {
        errors.push(`ability ${ability.id} has an invalid area`)
      }
      for (const value of [ability.ignoresShields, ability.affectsAllies]) {
        if (value !== undefined && typeof value !== 'boolean') {
          errors.push(`ability ${ability.id} has an invalid flag`)
        }
      }
    }
  }
  const instanceIds = new Set<string>()
  const sideCounts: Record<ClashSide, number> = { attacker: 0, defender: 0 }
  for (const rawUnit of plan.roster as unknown[]) {
    if (!isRecord(rawUnit)) {
      errors.push('plan has a malformed roster instance')
      continue
    }
    const unit = rawUnit as unknown as ClashPlanUnit
    if (!nonEmptyString(unit.instanceId) || instanceIds.has(unit.instanceId)) {
      errors.push(`duplicate or empty unit instance ${String(unit.instanceId ?? '')}`)
      continue
    }
    instanceIds.add(unit.instanceId)
    if (!nonEmptyString(unit.definitionId) || !definitionIds.has(unit.definitionId)) {
      errors.push(`unknown definition ${String(unit.definitionId ?? '')}`)
    }
    if (!validSide(unit.side)) {
      errors.push(`unit instance ${unit.instanceId} has an invalid side`)
      continue
    }
    sideCounts[unit.side] += 1
    for (const value of [unit.campaignUnitInstanceId, unit.cityId, unit.cohortId, unit.unitId]) {
      if (value !== undefined && !nonEmptyString(value)) {
        errors.push(`unit instance ${unit.instanceId} has an invalid campaign identity`)
      }
    }
  }
  if (sideCounts.attacker === 0 || sideCounts.defender === 0) {
    errors.push('both clash sides need at least one unit')
  }
  const occupiedTerrainCells = new Set<string>()
  for (const rawCell of plan.field.terrainCellIds as unknown[]) {
    if (!isRecord(rawCell)) {
      errors.push('field has a malformed terrain cell')
      continue
    }
    const cell = rawCell as unknown as ClashFieldVariantDefinition['terrainCellIds'][number]
    const key = `${String(cell.side)}:${String(cell.row)}:${String(cell.column)}`
    if (occupiedTerrainCells.has(key)) errors.push(`field repeats terrain cell ${key}`)
    occupiedTerrainCells.add(key)
    if (!validSide(cell.side) || !Number.isInteger(cell.row) || cell.row < 0
      || cell.row >= plan.field.rowsPerSide || !Number.isInteger(cell.column) || cell.column < 0
      || cell.column >= plan.field.columns || !nonEmptyString(cell.terrainId)) {
      errors.push('field has an invalid terrain cell')
      continue
    }
    const terrain = terrainById.get(cell.terrainId)
    if (!terrain) errors.push(`field references unknown terrain ${cell.terrainId}`)
    else if (terrain.deferredReason) errors.push(`field references deferred terrain ${cell.terrainId}`)
  }
  for (const unit of validUnitShapes) {
    for (const passive of unit.passives) {
      if (passive.statusId && !statusIds.has(passive.statusId)) {
        errors.push(`passive ${passive.id} references unknown status`)
      } else if (passive.statusId && statusById.get(passive.statusId)?.deferredReason) {
        errors.push(`passive ${passive.id} references deferred status`)
      }
    }
    for (const ability of unit.abilities) {
      if (ability.statusId && !statusIds.has(ability.statusId)) {
        errors.push(`ability ${ability.id} references unknown status`)
      } else if (ability.statusId && statusById.get(ability.statusId)?.deferredReason) {
        errors.push(`ability ${ability.id} references deferred status`)
      }
      if (ability.spawnUnitId && !definitionIds.has(ability.spawnUnitId)) {
        errors.push(`ability ${ability.id} references unavailable spawn unit`)
      }
    }
  }
  const morale = plan.morale
  if (!Number.isFinite(morale.positiveThresholdExclusive)
    || !Number.isFinite(morale.negativeThresholdExclusive)
    || !positiveInteger(morale.positiveActivationCharges)
    || !positiveInteger(morale.neutralActivationCharges)
    || !positiveInteger(morale.negativeActivationCooldownTurns)
    || !Number.isFinite(morale.minimum) || !Number.isFinite(morale.maximum)
    || morale.minimum > morale.maximum
    || morale.negativeThresholdExclusive > morale.positiveThresholdExclusive) {
    errors.push('plan morale rules are invalid')
  }
  if (!validSide(plan.placementFirstSide) || !validSide(plan.betweenClashesFirstSide)) {
    errors.push('plan side order is invalid')
  }
  for (const side of ['attacker', 'defender'] as const) {
    const value = plan.initialMorale[side]
    if (!Number.isFinite(value) || value < plan.morale.minimum || value > plan.morale.maximum) {
      errors.push('initial morale is outside configured bounds')
    }
  }
  return errors
}

function passive(definition: ClashUnitDefinition, kind: ClashPassiveDefinition['kind']): ClashPassiveDefinition[] {
  return definition.passives.filter(candidate => candidate.kind === kind)
}

function definitionMap(plan: ClashPlan): Map<string, ClashUnitDefinition> {
  return new Map(plan.units.map(unit => [unit.id, unit]))
}

function statusMap(plan: ClashPlan): Map<string, ClashStatusDefinition> {
  return new Map(plan.statuses.map(status => [status.id, status]))
}

function terrainMap(plan: ClashPlan): Map<string, ClashTerrainDefinition> {
  return new Map(plan.terrain.map(terrain => [terrain.id, terrain]))
}

function cellKey(side: ClashSide, row: number, column: number): string {
  return `${side}:${row}:${column}`
}

function findCell(
  state: ClashSimulationState,
  side: ClashSide,
  row: number,
  column: number,
): ClashCellState | undefined {
  return state.cells.find(cell => cell.side === side && cell.row === row && cell.column === column)
}

function unitDefinition(
  definitions: Map<string, ClashUnitDefinition>,
  unit: ClashUnitState,
): ClashUnitDefinition {
  const definition = definitions.get(unit.definitionId)
  if (!definition) throw new Error(`Unknown clash unit definition ${unit.definitionId}`)
  return definition
}

function appendLog(
  state: ClashSimulationState,
  kind: ClashLogEntry['kind'],
  message: string,
  unitInstanceIds: string[] = [],
): void {
  state.log.push({
    sequence: state.log.length + 1,
    turn: state.turn,
    kind,
    message,
    unitInstanceIds: [...unitInstanceIds],
  })
}

function sidePlacementTarget(plan: ClashPlan, state: ClashSimulationState, side: ClashSide): number {
  const capacity = plan.field.columns * (plan.field.rowsPerSide - plan.field.reinforcementRows)
  const rosterCount = Object.values(state.units).filter(unit => unit.side === side).length
  return Math.min(capacity, rosterCount)
}

function setupComplete(plan: ClashPlan, state: ClashSimulationState, side: ClashSide): boolean {
  return Object.values(state.units).filter(unit => unit.side === side && unit.deployed).length
    >= sidePlacementTarget(plan, state, side)
}

function nextSetupSide(plan: ClashPlan, state: ClashSimulationState, current: ClashSide): ClashSide | null {
  const candidate = opposite(current)
  if (!setupComplete(plan, state, candidate)) return candidate
  if (!setupComplete(plan, state, current)) return current
  return null
}

function firstFillRow(plan: ClashPlan, state: ClashSimulationState, side: ClashSide): number | null {
  const rows = plan.field.rowsPerSide - plan.field.reinforcementRows
  for (let row = 0; row < rows; row += 1) {
    if (state.cells.some(cell => cell.side === side && cell.row === row && cell.unitInstanceId === null)) {
      return row
    }
  }
  return null
}

export function createInitialClashState(plan: ClashPlan, seed: string | number): ClashSimulationState {
  const errors = validateClashPlan(plan)
  if (errors.length > 0) throw new Error(`Invalid Clash plan: ${errors.join('; ')}`)
  const definitions = definitionMap(plan)
  const terrainByCell = new Map(plan.field.terrainCellIds.map(item => [
    cellKey(item.side, item.row, item.column),
    item.terrainId,
  ]))
  const cells: ClashCellState[] = []
  for (const side of ['attacker', 'defender'] as const) {
    for (let row = 0; row < plan.field.rowsPerSide; row += 1) {
      for (let column = 0; column < plan.field.columns; column += 1) {
        cells.push({
          side,
          row,
          column,
          unitInstanceId: null,
          corpseIds: [],
          terrainId: terrainByCell.get(cellKey(side, row, column)) ?? null,
        })
      }
    }
  }
  const units: Record<string, ClashUnitState> = {}
  for (const item of plan.roster) {
    const definition = definitions.get(item.definitionId)!
    const shieldCharges = passive(definition, 'shield')
      .reduce((total, rule) => total + Math.max(0, rule.charges ?? 0), 0)
    const dodgeCharges = passive(definition, 'dodge')
      .reduce((total, rule) => total + Math.max(0, rule.charges ?? 0), 0)
    const positiveMoraleBonus = plan.initialMorale[item.side] > plan.morale.positiveThresholdExclusive
      ? 1
      : 0
    units[item.instanceId] = {
      instanceId: item.instanceId,
      definitionId: item.definitionId,
      side: item.side,
      row: null,
      column: null,
      hp: definition.maxHp!,
      maxHp: definition.maxHp!,
      attackDelta: 0,
      speedDelta: 0,
      shieldCharges,
      dodgeCharges,
      statuses: [],
      passiveCharges: Object.fromEntries(definition.passives
        .filter(rule => rule.charges !== undefined)
        .map(rule => [rule.id, Math.max(0, rule.charges ?? 0)])),
      abilityCharges: Object.fromEntries(definition.abilities.map(ability => (
        [ability.id, ability.charges + positiveMoraleBonus]
      ))),
      abilityReadyClash: Object.fromEntries(definition.abilities.map(ability => [ability.id, 0])),
      rangedReadyClash: 0,
      hiddenPassiveIdsRevealed: [],
      killCount: 0,
      alive: true,
      deployed: false,
    }
  }
  return {
    planId: plan.id,
    seed,
    rng: createEmpiresRngState(seed),
    turn: 0,
    clashNumber: 0,
    phase: 'placement',
    expectedSide: plan.placementFirstSide,
    units,
    cells,
    corpses: [],
    morale: cloneJson(plan.initialMorale),
    betweenClashes: {
      attacker: { activationCount: 0, placementUsed: false, ended: false },
      defender: { activationCount: 0, placementUsed: false, ended: false },
    },
    commandLog: [],
    log: [],
    outcome: null,
    winner: null,
    terminalReason: null,
    error: null,
  }
}

function terrainAt(
  plan: ClashPlan,
  state: ClashSimulationState,
  unit: ClashUnitState,
): ClashTerrainDefinition | null {
  if (unit.row === null || unit.column === null) return null
  const cell = findCell(state, unit.side, unit.row, unit.column)
  if (!cell?.terrainId) return null
  return terrainMap(plan).get(cell.terrainId) ?? null
}

function hasStatusKind(
  plan: ClashPlan,
  unit: ClashUnitState,
  kind: ClashStatusDefinition['kind'],
): boolean {
  const statuses = statusMap(plan)
  return unit.statuses.some(status => statuses.get(status.statusId)?.kind === kind)
}

function weaponDisabled(plan: ClashPlan, unit: ClashUnitState): boolean {
  return hasStatusKind(plan, unit, 'disarm')
}

function revealPassive(
  state: ClashSimulationState,
  unit: ClashUnitState,
  rule: ClashPassiveDefinition,
): void {
  if (!rule.hidden || unit.hiddenPassiveIdsRevealed.includes(rule.id)) return
  unit.hiddenPassiveIdsRevealed.push(rule.id)
  unit.hiddenPassiveIdsRevealed.sort(stableCompare)
  appendLog(state, 'system', `Раскрыта скрытая пассивка «${rule.name}».`, [unit.instanceId])
}

function activePassives(
  plan: ClashPlan,
  definition: ClashUnitDefinition,
  unit: ClashUnitState,
): ClashPassiveDefinition[] {
  return definition.passives.filter(rule => rule.category !== 'weapon' || !weaponDisabled(plan, unit))
}

function rowHasLegion(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  unit: ClashUnitState,
): boolean {
  if (unit.row === null) return false
  const rowUnits = Object.values(state.units).filter(candidate => (
    candidate.alive && candidate.deployed && candidate.side === unit.side && candidate.row === unit.row
  ))
  return rowUnits.length === plan.field.columns && rowUnits.every(candidate => (
    passive(unitDefinition(definitions, candidate), 'legion').length > 0
  ))
}

function effectiveSpeed(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  unit: ClashUnitState,
): number {
  if (!unit.alive || !unit.deployed) return 0
  if (hasStatusKind(plan, unit, 'freeze') || hasStatusKind(plan, unit, 'stun')
    || hasStatusKind(plan, unit, 'paralysis') || hasStatusKind(plan, unit, 'neuro-toxin')) return 0
  const definition = unitDefinition(definitions, unit)
  let bonus = unit.speedDelta + plan.region.speedDelta
  const terrain = terrainAt(plan, state, unit)
  if (terrain?.kind === 'high-ground') bonus += terrain.speedDelta ?? 1
  if (rowHasLegion(plan, state, definitions, unit)) bonus += 2
  for (const rule of activePassives(plan, definition, unit).filter(item => item.kind === 'climate-stat')) {
    if (rule.targetTag === plan.region.humidity || rule.targetTag === plan.region.temperature) {
      bonus += rule.value ?? 0
    }
  }
  if (passive(definition, 'heavy-armor').length > 0) bonus = Math.min(0, bonus)
  let speed = Math.max(0, definition.speed! + bonus)
  for (const status of unit.statuses) {
    const rule = statusMap(plan).get(status.statusId)
    if (rule?.speedDivisor && rule.speedDivisor > 0) speed = Math.ceil(speed / rule.speedDivisor)
  }
  return speed
}

function effectiveAttack(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  unit: ClashUnitState,
): number {
  if (!unit.alive || !unit.deployed) return 0
  const definition = unitDefinition(definitions, unit)
  let attack = Math.max(0, definition.attack! + unit.attackDelta)
  for (const status of unit.statuses) {
    const rule = statusMap(plan).get(status.statusId)
    if (rule?.attackDivisor && rule.attackDivisor > 0) attack = Math.ceil(attack / rule.attackDivisor)
    if (rule?.kind === 'wither') attack = Math.max(0, attack - 1)
  }
  for (const rule of activePassives(plan, definition, unit).filter(item => item.kind === 'row-buff')) {
    attack += rule.value ?? 0
  }
  return attack
}

function removeExpiredStatuses(unit: ClashUnitState): void {
  unit.statuses = unit.statuses.filter(status => status.remainingTurns === null || status.remainingTurns > 0)
}

function applyStatus(
  plan: ClashPlan,
  state: ClashSimulationState,
  target: ClashUnitState,
  statusId: string,
  sourceUnitInstanceId: string | null,
  durationOverride?: number,
): void {
  const rule = statusMap(plan).get(statusId)
  if (!rule || !target.alive) return
  const definition = definitionMap(plan).get(target.definitionId)!
  const immune = activePassives(plan, definition, target)
    .some(passiveRule => passiveRule.kind === 'status-immunity'
      && (!passiveRule.statusId || passiveRule.statusId === statusId || passiveRule.targetTag === rule.kind))
  if (immune) return
  if (rule.clearsShields) target.shieldCharges = 0
  if (rule.clearsRage) {
    target.statuses = target.statuses.filter(status => statusMap(plan).get(status.statusId)?.kind !== 'rage')
  }
  const existing = target.statuses.find(status => status.statusId === statusId)
  const duration = durationOverride ?? rule.durationTurns ?? null
  if (existing) {
    if (rule.stacks) existing.stacks += 1
    if (duration !== null) existing.remainingTurns = Math.max(existing.remainingTurns ?? 0, duration)
    existing.appliedClash = state.clashNumber
  } else {
    target.statuses.push({
      id: `${statusId}:${target.instanceId}:${state.turn}:${state.log.length + 1}`,
      statusId,
      sourceUnitInstanceId,
      stacks: 1,
      remainingTurns: duration,
      appliedHp: target.hp,
      appliedClash: state.clashNumber,
    })
  }
  appendLog(state, 'status', `${target.instanceId}: ${rule.name}.`, [target.instanceId])
  if (rule.kind === 'corpse-centipede-poison') {
    applyStatus(plan, state, target, 'paralysis', sourceUnitInstanceId)
  }
}

function burnCorpse(state: ClashSimulationState, corpse: ClashCorpseState): void {
  corpse.burned = true
  const cell = findCell(state, corpse.side, corpse.row, corpse.column)
  if (cell) cell.corpseIds = cell.corpseIds.filter(id => id !== corpse.id)
}

function killUnit(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  unit: ClashUnitState,
  source: ClashUnitState | null,
  options: { burned?: boolean; decomposed?: boolean } = {},
): void {
  if (!unit.alive) return
  unit.alive = false
  unit.hp = 0
  const row = unit.row
  const column = unit.column
  if (row !== null && column !== null) {
    const cell = findCell(state, unit.side, row, column)
    if (cell?.unitInstanceId === unit.instanceId) cell.unitInstanceId = null
    const corpse: ClashCorpseState = {
      id: `corpse:${unit.instanceId}:${state.turn}:${state.corpses.length + 1}`,
      unitInstanceId: unit.instanceId,
      definitionId: unit.definitionId,
      side: unit.side,
      row,
      column,
      createdTurn: state.turn,
      burned: Boolean(options.burned),
      decomposed: Boolean(options.decomposed),
    }
    state.corpses.push(corpse)
    if (!corpse.burned && !corpse.decomposed && cell) cell.corpseIds.push(corpse.id)
  }
  appendLog(state, 'attack', `${unit.instanceId} выбыл.`, [unit.instanceId])
  if (source) {
    source.killCount += 1
    const sourceDefinition = unitDefinition(definitions, source)
    for (const rule of activePassives(plan, sourceDefinition, source).filter(item => item.kind === 'kill-growth')) {
      revealPassive(state, source, rule)
      source.attackDelta += rule.value ?? 0
      source.speedDelta += rule.value ?? 0
    }
  }
  const definition = unitDefinition(definitions, unit)
  for (const rule of activePassives(plan, definition, unit).filter(item => item.kind === 'death')) {
    revealPassive(state, unit, rule)
    if (rule.statusId && row !== null && column !== null) {
      for (const target of Object.values(state.units).filter(candidate => (
        candidate.alive && candidate.deployed && candidate.row !== null && candidate.column !== null
        && Math.abs(candidate.row - row) <= 1 && Math.abs(candidate.column - column) <= 1
      )).sort((left, right) => stableCompare(left.instanceId, right.instanceId))) {
        applyStatus(plan, state, target, rule.statusId, unit.instanceId, rule.durationTurns)
      }
    }
  }
}

function dealDamage(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  source: ClashUnitState | null,
  target: ClashUnitState,
  rawDamage: number,
  options: {
    arrow?: boolean
    area?: boolean
    bypassesShields?: boolean
    burned?: boolean
    decomposed?: boolean
  } = {},
): number {
  if (!target.alive || rawDamage <= 0) return 0
  const targetDefinition = unitDefinition(definitions, target)
  const targetPassives = activePassives(plan, targetDefinition, target)
  const dodgePassives = targetPassives.filter(rule => rule.kind === 'dodge')
  const statusDodge = hasStatusKind(plan, target, 'dodge')
  if (options.area && (dodgePassives.length > 0 || statusDodge)) {
    for (const rule of dodgePassives) revealPassive(state, target, rule)
    killUnit(plan, state, definitions, target, source, options)
    return target.maxHp
  }
  if (!options.bypassesShields) {
    const arrowShield = options.arrow && targetPassives.some(rule => (
      rule.kind === 'shield' && rule.targetTag === 'ordinary-arrows'
    ))
    if (arrowShield) {
      for (const rule of targetPassives.filter(candidate => (
        candidate.kind === 'shield' && candidate.targetTag === 'ordinary-arrows'
      ))) revealPassive(state, target, rule)
      appendLog(state, 'attack', `${target.instanceId} поглотил обычную стрелу щитом.`, [target.instanceId])
      return 0
    }
    if (target.shieldCharges > 0) {
      for (const rule of targetPassives.filter(candidate => candidate.kind === 'shield')) {
        revealPassive(state, target, rule)
      }
      target.shieldCharges -= 1
      appendLog(state, 'attack', `${target.instanceId} блокировал атаку щитом.`, [target.instanceId])
      return 0
    }
  }
  let damage = rawDamage
  if (!options.area && (target.dodgeCharges > 0 || statusDodge)) {
    for (const rule of dodgePassives) revealPassive(state, target, rule)
    if (target.dodgeCharges > 0) target.dodgeCharges -= 1
    damage = Math.min(1, damage)
  }
  for (const rule of targetPassives.filter(item => item.kind === 'damage-cap')) {
    revealPassive(state, target, rule)
    damage = Math.min(damage, rule.value ?? damage)
  }
  for (const rule of targetPassives.filter(item => item.kind === 'damage-modifier' && item.multiplier)) {
    revealPassive(state, target, rule)
    damage = Math.max(0, Math.ceil(damage * (rule.multiplier ?? 1)))
  }
  damage = Math.max(0, damage)
  target.hp = Math.max(0, target.hp - damage)
  if (damage > 0) {
    target.statuses = target.statuses.filter(status => (
      statusMap(plan).get(status.statusId)?.kind !== 'neuro-toxin'
      || status.remainingTurns === null
      || status.remainingTurns <= 0
    ))
    appendLog(state, 'attack', `${source?.instanceId ?? 'Эффект'} → ${target.instanceId}: ${damage}.`, [
      ...(source ? [source.instanceId] : []),
      target.instanceId,
    ])
  }
  if (target.hp <= 0) killUnit(plan, state, definitions, target, source, options)
  return damage
}

function applyTurnStartStatuses(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
): void {
  const statuses = statusMap(plan)
  const units = Object.values(state.units)
    .filter(unit => unit.alive && unit.deployed)
    .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  for (const unit of units) {
    if (!unit.alive) continue
    const definition = unitDefinition(definitions, unit)
    for (const rule of activePassives(plan, definition, unit).filter(item => item.kind === 'heal')) {
      unit.hp = Math.min(unit.maxHp, unit.hp + (rule.value ?? 0))
    }
    const terrain = terrainAt(plan, state, unit)
    if (terrain?.kind === 'healing-mushrooms') {
      unit.hp = Math.min(unit.maxHp, unit.hp + (terrain.healingPerTurn ?? 0))
    }
    for (const active of [...unit.statuses]) {
      const rule = statuses.get(active.statusId)
      if (!rule) continue
      if (rule.kind === 'bleeding' || rule.kind === 'ignite') {
        dealDamage(plan, state, definitions, null, unit, (rule.damagePerTurn ?? 0) * active.stacks, {
          bypassesShields: rule.bypassesShields,
          burned: rule.kind === 'ignite',
        })
      } else if (rule.kind === 'cobra-poison' && active.remainingTurns !== null
        && active.remainingTurns <= 1) {
        killUnit(plan, state, definitions, unit, null)
      } else if (rule.kind === 'karakurt-poison' && active.remainingTurns !== null
        && active.remainingTurns <= 1) {
        killUnit(plan, state, definitions, unit, null)
      } else if (rule.kind === 'lhp-toxin') {
        const damage = Math.ceil(Math.max(0, unit.maxHp - unit.hp) / 2)
        dealDamage(plan, state, definitions, null, unit, damage, {
          bypassesShields: true,
          decomposed: damage >= unit.hp,
        })
      } else if (rule.kind === 'scorpion-poison'
        && unit.hp < (rule.thresholdHpExclusive ?? 5)) {
        applyStatus(plan, state, unit, 'paralysis', active.sourceUnitInstanceId)
      } else if (rule.kind === 'corpse-centipede-poison' && unit.hp <= active.appliedHp / 2) {
        active.remainingTurns = 0
        for (const linked of unit.statuses) {
          if (statuses.get(linked.statusId)?.kind === 'paralysis'
            && linked.sourceUnitInstanceId === active.sourceUnitInstanceId) {
            linked.remainingTurns = 0
          }
        }
      }
    }
    removeExpiredStatuses(unit)
  }
}

function finishClashStatusDurations(state: ClashSimulationState): void {
  const units = Object.values(state.units)
    .filter(unit => unit.alive && unit.deployed)
    .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  for (const unit of units) {
    for (const active of unit.statuses) {
      if (active.remainingTurns !== null && active.appliedClash < state.clashNumber) {
        active.remainingTurns -= 1
      }
    }
    removeExpiredStatuses(unit)
  }
}

function frontUnit(
  state: ClashSimulationState,
  side: ClashSide,
  column: number,
): ClashUnitState | null {
  return Object.values(state.units)
    .filter(unit => unit.alive && unit.deployed && unit.side === side && unit.column === column
      && unit.row !== null)
    .sort((left, right) => (left.row! - right.row!)
      || stableCompare(left.instanceId, right.instanceId))[0] ?? null
}

function rangedSupport(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  front: ClashUnitState,
): Array<{ source: ClashUnitState; damage: number; reloadTurns: number; rule: ClashPassiveDefinition }> {
  const supporters = Object.values(state.units)
    .filter(unit => unit.alive && unit.deployed && unit.side === front.side
      && unit.column === front.column && unit.row !== null && front.row !== null && unit.row > front.row)
    .sort((left, right) => (left.row! - right.row!) || stableCompare(left.instanceId, right.instanceId))
  const shots: Array<{
    source: ClashUnitState
    damage: number
    reloadTurns: number
    rule: ClashPassiveDefinition
  }> = []
  for (const supporter of supporters) {
    if (supporter.rangedReadyClash > state.clashNumber
      || effectiveSpeed(plan, state, definitions, supporter) <= 0) continue
    const definition = unitDefinition(definitions, supporter)
    const rule = activePassives(plan, definition, supporter).find(item => item.kind === 'ranged')
    if (!rule) continue
    shots.push({
      source: supporter,
      damage: rule.value ?? effectiveAttack(plan, state, definitions, supporter),
      reloadTurns: Math.max(0, rule.reloadTurns ?? 0),
      rule,
    })
  }
  return shots
}

function attackUnit(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  attacker: ClashUnitState,
  target: ClashUnitState,
  options: {
    rangedSupport?: Array<{
      source: ClashUnitState
      damage: number
      reloadTurns: number
      rule: ClashPassiveDefinition
    }>
  } = {},
): void {
  if (!attacker.alive || !target.alive || effectiveSpeed(plan, state, definitions, attacker) <= 0) return
  const attackerDefinition = unitDefinition(definitions, attacker)
  const attackerPassives = activePassives(plan, attackerDefinition, attacker)
  const targetDefinition = unitDefinition(definitions, target)
  const antiCavalry = attackerPassives.find(rule => rule.kind === 'anti-cavalry')
  if (antiCavalry && targetDefinition.tags.includes('cavalry')) {
    revealPassive(state, attacker, antiCavalry)
    killUnit(plan, state, definitions, target, attacker)
    return
  }
  let damage = effectiveAttack(plan, state, definitions, attacker)
  for (const rule of attackerPassives.filter(item => item.kind === 'damage-modifier')) {
    const targetSpeed = effectiveSpeed(plan, state, definitions, target)
    const condition = rule.targetTag
      ? targetDefinition.tags.includes(rule.targetTag)
      : rule.threshold === undefined || targetSpeed >= rule.threshold
    if (!condition) continue
    revealPassive(state, attacker, rule)
    if (rule.multiplier !== undefined) damage = Math.ceil(damage * rule.multiplier)
    damage += rule.value ?? 0
  }
  const breakShield = attackerPassives.find(rule => rule.kind === 'shield' && rule.targetTag === 'break-enemy')
  if (breakShield && target.shieldCharges > 0) {
    revealPassive(state, attacker, breakShield)
    target.shieldCharges = 0
    dealDamage(plan, state, definitions, attacker, target, breakShield.value ?? 1, { bypassesShields: true })
    if (!target.alive) return
  }
  const strikes = Math.max(1, attackerPassives
    .filter(rule => rule.kind === 'multi-strike')
    .reduce((maximum, rule) => Math.max(maximum, rule.value ?? 1), 1))
  for (let strike = 0; strike < strikes && attacker.alive && target.alive; strike += 1) {
    const dealt = dealDamage(plan, state, definitions, attacker, target, damage, {
      bypassesShields: attackerPassives.some(rule => rule.bypassesShields),
    })
    if (dealt > 0) {
      for (const rule of attackerPassives.filter(item => item.kind === 'status-on-hit' && item.statusId)) {
        const charges = attacker.passiveCharges[rule.id]
        if (charges !== undefined && charges <= 0) continue
        revealPassive(state, attacker, rule)
        applyStatus(plan, state, target, rule.statusId!, attacker.instanceId, rule.durationTurns)
        if (charges !== undefined) attacker.passiveCharges[rule.id] = charges - 1
      }
      for (const rule of attackerPassives.filter(item => item.kind === 'disarm')) {
        if (rule.threshold === undefined || effectiveAttack(plan, state, definitions, target) < rule.threshold) {
          revealPassive(state, attacker, rule)
          applyStatus(plan, state, target, rule.statusId ?? 'disarm', attacker.instanceId, rule.durationTurns)
        }
      }
      for (const rule of activePassives(plan, targetDefinition, target).filter(item => item.kind === 'reflect')) {
        revealPassive(state, target, rule)
        dealDamage(plan, state, definitions, target, attacker, Math.ceil(dealt * (rule.multiplier ?? 1)), {
          bypassesShields: true,
        })
      }
    }
  }
  for (const shot of options.rangedSupport ?? []) {
    if (!target.alive) break
    revealPassive(state, shot.source, shot.rule)
    shot.source.rangedReadyClash = state.clashNumber + shot.reloadTurns + 1
    dealDamage(plan, state, definitions, shot.source, target, shot.damage, { arrow: true })
  }
}

function advanceColumn(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  side: ClashSide,
  column: number,
): void {
  const units = Object.values(state.units)
    .filter(unit => unit.alive && unit.deployed && unit.side === side && unit.column === column
      && unit.row !== null)
    .sort((left, right) => (left.row! - right.row!) || stableCompare(left.instanceId, right.instanceId))
  if (units.length === 0) return
  for (const unit of units) {
    if (unit.row === null || unit.row === 0) continue
    const targetRow = unit.row - 1
    const target = findCell(state, side, targetRow, column)
    const source = findCell(state, side, unit.row, column)
    if (!target || target.unitInstanceId !== null) continue
    if (plan.corpseBlocksAdvance && target.corpseIds.length > 0) continue
    if (effectiveSpeed(plan, state, definitions, unit) <= 0) continue
    if (source?.unitInstanceId === unit.instanceId) source.unitInstanceId = null
    target.unitInstanceId = unit.instanceId
    unit.row = targetRow
    appendLog(state, 'advance', `${unit.instanceId} продвинулся вперёд.`, [unit.instanceId])
  }
}

function remainingSideUnits(state: ClashSimulationState, side: ClashSide): ClashUnitState[] {
  return Object.values(state.units).filter(unit => unit.side === side && unit.alive)
}

function settleWinner(
  state: ClashSimulationState,
  winner: ClashSide | null,
  terminalReason: 'elimination' | 'turn-cap' | 'aborted',
): void {
  state.phase = 'finished'
  state.expectedSide = null
  state.winner = winner
  state.terminalReason = terminalReason
  state.outcome = terminalReason === 'aborted'
    ? 'aborted'
    : winner === 'attacker'
      ? 'victory'
      : 'defeat'
}

function checkElimination(state: ClashSimulationState): boolean {
  const attackers = remainingSideUnits(state, 'attacker')
  const defenders = remainingSideUnits(state, 'defender')
  if (attackers.length > 0 && defenders.length > 0) return false
  if (attackers.length === 0 && defenders.length === 0) settleWinner(state, 'defender', 'elimination')
  else settleWinner(state, attackers.length > 0 ? 'attacker' : 'defender', 'elimination')
  return true
}

function resolveClashRound(plan: ClashPlan, state: ClashSimulationState): void {
  const definitions = definitionMap(plan)
  state.clashNumber += 1
  appendLog(state, 'system', `Клэш ${state.clashNumber}.`)
  applyTurnStartStatuses(plan, state, definitions)
  if (checkElimination(state)) return
  for (let column = 0; column < plan.field.columns; column += 1) {
    const attacker = frontUnit(state, 'attacker', column)
    const defender = frontUnit(state, 'defender', column)
    if (!attacker || !defender) continue
    const attackerSpeed = effectiveSpeed(plan, state, definitions, attacker)
    const defenderSpeed = effectiveSpeed(plan, state, definitions, defender)
    const first = attackerSpeed === defenderSpeed
      ? plan.speedTieRule === 'attacker-first' ? attacker : defender
      : attackerSpeed > defenderSpeed ? attacker : defender
    const second = first.instanceId === attacker.instanceId ? defender : attacker
    const firstSupport = rangedSupport(plan, state, definitions, first)
    attackUnit(plan, state, definitions, first, second, { rangedSupport: firstSupport })
    if (second.alive) {
      const secondSupport = rangedSupport(plan, state, definitions, second)
      attackUnit(plan, state, definitions, second, first, { rangedSupport: secondSupport })
    }
  }
  for (const side of ['attacker', 'defender'] as const) {
    for (let column = 0; column < plan.field.columns; column += 1) {
      advanceColumn(plan, state, definitions, side, column)
    }
  }
  finishClashStatusDurations(state)
  if (checkElimination(state)) return
  state.phase = 'between-clashes'
  state.expectedSide = plan.betweenClashesFirstSide
  state.betweenClashes = {
    attacker: { activationCount: 0, placementUsed: false, ended: false },
    defender: { activationCount: 0, placementUsed: false, ended: false },
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

function targetUnitsForAbility(
  state: ClashSimulationState,
  source: ClashUnitState,
  ability: ClashAbilityDefinition,
  command: Extract<ClashCommand, { kind: 'activate' }>,
): ClashUnitState[] {
  const target = command.targetUnitInstanceId ? state.units[command.targetUnitInstanceId] : null
  if (ability.target === 'self') return [source]
  if (ability.target === 'ally') {
    return target?.alive && target.deployed && target.side === source.side ? [target] : []
  }
  if (ability.target === 'enemy') {
    return target?.alive && target.deployed && target.side === opposite(source.side) ? [target] : []
  }
  if (ability.target === 'all-enemies') {
    return Object.values(state.units)
      .filter(unit => unit.alive && unit.deployed && unit.side !== source.side)
      .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  }
  if (ability.target === 'row') {
    return Object.values(state.units)
      .filter(unit => unit.alive && unit.deployed
        && unit.side === (command.targetSide ?? opposite(source.side))
        && unit.row === command.targetRow)
      .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  }
  if (ability.target === 'column') {
    return Object.values(state.units)
      .filter(unit => unit.alive && unit.deployed
        && unit.side === (command.targetSide ?? opposite(source.side))
        && unit.column === command.targetColumn)
      .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  }
  if (ability.target === 'cell') {
    return Object.values(state.units)
      .filter(unit => unit.alive && unit.deployed
        && unit.side === command.targetSide && unit.row === command.targetRow
        && unit.column === command.targetColumn)
      .sort((left, right) => stableCompare(left.instanceId, right.instanceId))
  }
  return []
}

function applyAbilityEffect(
  plan: ClashPlan,
  state: ClashSimulationState,
  definitions: Map<string, ClashUnitDefinition>,
  source: ClashUnitState,
  ability: ClashAbilityDefinition,
  command: Extract<ClashCommand, { kind: 'activate' }>,
): boolean {
  const targets = targetUnitsForAbility(state, source, ability, command)
  if (ability.kind !== 'morale' && ability.kind !== 'cavalry-charge'
    && ability.kind !== 'spawn' && targets.length === 0) return false
  if (ability.kind === 'damage') {
    for (const target of targets) {
      dealDamage(plan, state, definitions, source, target, ability.value ?? effectiveAttack(plan, state, definitions, source), {
        area: ability.area !== undefined && ability.area !== 'target',
        bypassesShields: ability.ignoresShields,
      })
    }
  } else if (ability.kind === 'status' && ability.statusId) {
    for (const target of targets) {
      applyStatus(plan, state, target, ability.statusId, source.instanceId, ability.durationTurns)
    }
  } else if (ability.kind === 'heal-full') {
    for (const target of targets) target.hp = target.maxHp
  } else if (ability.kind === 'cleanse') {
    for (const target of targets) target.statuses = []
  } else if (ability.kind === 'morale') {
    const side = command.targetSide ?? source.side
    state.morale[side] = Math.max(plan.morale.minimum, Math.min(
      plan.morale.maximum,
      state.morale[side] + (ability.value ?? 0),
    ))
  } else if (ability.kind === 'move-to-front') {
    if (source.column === null) return false
    while (source.row !== null && source.row > 0) {
      const target = findCell(state, source.side, source.row - 1, source.column)
      if (!target || target.unitInstanceId
        || (plan.corpseBlocksAdvance && target.corpseIds.length > 0)) break
      const current = findCell(state, source.side, source.row, source.column)
      if (current) current.unitInstanceId = null
      target.unitInstanceId = source.instanceId
      source.row -= 1
    }
  } else if (ability.kind === 'cavalry-charge') {
    const row = command.targetRow
    if (row === undefined) return false
    const crossed = Object.values(state.units)
      .filter(unit => unit.alive && unit.deployed && unit.row === row && unit.instanceId !== source.instanceId)
      .sort((left, right) => (left.column ?? 0) - (right.column ?? 0)
        || stableCompare(left.instanceId, right.instanceId))
    for (const target of crossed) {
      if (!source.alive) break
      dealDamage(plan, state, definitions, source, target, effectiveAttack(plan, state, definitions, source), {
        bypassesShields: ability.ignoresShields,
      })
      dealDamage(plan, state, definitions, null, source, 1, { bypassesShields: true })
    }
  } else if (ability.kind === 'spawn' && ability.spawnUnitId) {
    const cell = findCell(
      state,
      command.targetSide ?? source.side,
      command.targetRow ?? source.row ?? 0,
      command.targetColumn ?? source.column ?? 0,
    )
    const definition = definitions.get(ability.spawnUnitId)
    if (!cell || cell.unitInstanceId
      || (plan.corpseBlocksAdvance && cell.corpseIds.length > 0) || !definition) return false
    const instanceId = `${source.instanceId}:${ability.id}:${state.turn}:${Math.floor(nextEmpiresRandom(state.rng) * 1_000_000)}`
    state.units[instanceId] = {
      instanceId,
      definitionId: definition.id,
      side: source.side,
      row: cell.row,
      column: cell.column,
      hp: definition.maxHp!,
      maxHp: definition.maxHp!,
      attackDelta: 0,
      speedDelta: 0,
      shieldCharges: 0,
      dodgeCharges: 0,
      statuses: [],
      passiveCharges: Object.fromEntries(definition.passives
        .filter(rule => rule.charges !== undefined)
        .map(rule => [rule.id, Math.max(0, rule.charges ?? 0)])),
      abilityCharges: Object.fromEntries(definition.abilities.map(item => [item.id, item.charges])),
      abilityReadyClash: Object.fromEntries(definition.abilities.map(item => [item.id, 0])),
      rangedReadyClash: 0,
      hiddenPassiveIdsRevealed: [],
      killCount: 0,
      alive: true,
      deployed: true,
    }
    cell.unitInstanceId = instanceId
  }
  return true
}

function placeUnit(
  plan: ClashPlan,
  state: ClashSimulationState,
  command: Extract<ClashCommand, { kind: 'place' }>,
): string | null {
  const unit = state.units[command.unitInstanceId]
  if (!unit || !unit.alive || unit.deployed || unit.side !== command.side) {
    return 'Unit is not an available reserve for that side.'
  }
  const cell = findCell(state, command.side, command.row, command.column)
  if (!cell || cell.unitInstanceId !== null
    || (plan.corpseBlocksAdvance && cell.corpseIds.length > 0)) return 'Placement cell is unavailable.'
  if (state.phase === 'placement') {
    if (state.expectedSide !== command.side) return 'It is the other side’s placement turn.'
    const row = firstFillRow(plan, state, command.side)
    if (row === null || command.row !== row) return 'Placement must fill the nearest unfinished row.'
  } else if (state.phase === 'between-clashes') {
    if (state.expectedSide !== command.side) return 'It is the other side’s between-clash turn.'
    if (state.betweenClashes[command.side].ended) return 'That side already ended its action window.'
    if (plan.onePlacementPerSideBetweenClashes && state.betweenClashes[command.side].placementUsed) {
      return 'That side already placed one reinforcement between clashes.'
    }
  } else return 'Units can only be placed during placement or between clashes.'
  cell.unitInstanceId = unit.instanceId
  unit.deployed = true
  unit.row = command.row
  unit.column = command.column
  const terrain = terrainMap(plan).get(cell.terrainId ?? '')
  if (terrain?.kind === 'acid') {
    unit.maxHp = Math.max(1, Math.ceil(unit.maxHp * (terrain.maxHpMultiplier ?? 0.5)))
    unit.hp = Math.min(unit.hp, unit.maxHp)
  } else if (terrain?.kind === 'trap') {
    const definitions = definitionMap(plan)
    dealDamage(plan, state, definitions, null, unit, terrain.damage ?? 0, { bypassesShields: true })
  }
  appendLog(state, 'placement', `${unit.instanceId} выставлен (${command.row}, ${command.column}).`, [unit.instanceId])
  if (state.phase === 'placement') {
    state.expectedSide = nextSetupSide(plan, state, command.side)
    if (state.expectedSide === null) state.phase = 'clash-ready'
  } else state.betweenClashes[command.side].placementUsed = true
  return null
}

function finishBetweenTurn(plan: ClashPlan, state: ClashSimulationState, side: ClashSide): string | null {
  if (state.phase !== 'between-clashes' || state.expectedSide !== side) {
    return 'It is not that side’s between-clash turn.'
  }
  if (state.betweenClashes[side].ended) return 'That side already ended its action window.'
  state.betweenClashes[side].ended = true
  const other = opposite(side)
  if (!state.betweenClashes[other].ended) state.expectedSide = other
  else {
    state.phase = 'clash-ready'
    state.expectedSide = null
  }
  return null
}

function applyActivation(
  plan: ClashPlan,
  state: ClashSimulationState,
  command: Extract<ClashCommand, { kind: 'activate' }>,
): string | null {
  if (state.phase !== 'between-clashes' || state.expectedSide !== command.side) {
    return 'It is not that side’s activation window.'
  }
  const window = state.betweenClashes[command.side]
  if (window.ended) return 'That side already ended its action window.'
  if (window.activationCount >= activationLimit(plan, state, command.side)) {
    return 'Side morale does not permit another activation now.'
  }
  const unit = state.units[command.unitInstanceId]
  if (!unit || !unit.alive || !unit.deployed || unit.side !== command.side) {
    return 'Activating unit is unavailable.'
  }
  const definitions = definitionMap(plan)
  const definition = unitDefinition(definitions, unit)
  const ability = definition.abilities.find(candidate => candidate.id === command.abilityId)
  if (!ability) return 'Unknown unit ability.'
  if ((unit.abilityCharges[ability.id] ?? 0) <= 0) return 'That ability has no charges left.'
  if ((unit.abilityReadyClash[ability.id] ?? 0) > state.clashNumber) return 'That ability is reloading.'
  const repeats = terrainAt(plan, state, unit)?.duplicateActivations
    && definition.tags.includes('archer')
    ? 2
    : 1
  for (let repeat = 0; repeat < repeats; repeat += 1) {
    if (!applyAbilityEffect(plan, state, definitions, unit, ability, command)) {
      if (repeat === 0) return 'Ability target is invalid.'
      break
    }
  }
  unit.abilityCharges[ability.id] -= 1
  unit.abilityReadyClash[ability.id] = ability.reloadTurns === 0
    ? state.clashNumber
    : state.clashNumber + ability.reloadTurns + 1
  window.activationCount += 1
  appendLog(state, 'activation', `${unit.instanceId}: ${ability.name}.`, [unit.instanceId])
  checkElimination(state)
  return null
}

function terminateAtCap(plan: ClashPlan, state: ClashSimulationState): void {
  const hp = (side: ClashSide) => remainingSideUnits(state, side)
    .reduce((total, unit) => total + unit.hp, 0)
  const attackerHp = hp('attacker')
  const defenderHp = hp('defender')
  settleWinner(
    state,
    attackerHp === defenderHp
      ? plan.turnCapTieWinner
      : attackerHp > defenderHp ? 'attacker' : 'defender',
    'turn-cap',
  )
  appendLog(state, 'system', 'Сработал ограничитель ходов; преимущество определено по оставшемуся ХП.')
}

/** The only production state-transition path for Clash commands. */
export function applyClashCommand(
  plan: ClashPlan,
  previous: ClashSimulationState,
  command: ClashCommand,
): ClashSimulationState {
  const state = cloneJson(previous)
  if (state.phase === 'finished') return { ...state, error: 'Clash is already finished.' }
  if (command.turn !== state.turn + 1) return { ...state, error: 'Command turn is stale or skipped.' }
  if (state.commandLog.length >= plan.maxCommands) {
    terminateAtCap(plan, state)
    return state
  }
  state.turn = command.turn
  state.error = null
  let error: string | null = null
  if (command.kind === 'place') error = placeUnit(plan, state, command)
  else if (command.kind === 'activate') error = applyActivation(plan, state, command)
  else if (command.kind === 'end-between-clash') error = finishBetweenTurn(plan, state, command.side)
  else if (command.kind === 'resolve-clash') {
    if (state.phase !== 'clash-ready') error = 'The board is not ready for a clash.'
    else resolveClashRound(plan, state)
  } else error = 'Unknown Clash command kind.'
  if (error) return { ...cloneJson(previous), error }
  state.commandLog.push(cloneJson(command))
  if (state.phase !== 'finished'
    && (state.turn >= plan.maxTurns || state.commandLog.length >= plan.maxCommands)) terminateAtCap(plan, state)
  return state
}

function deploymentResults(plan: ClashPlan, state: ClashSimulationState): ClashDeploymentResult[] {
  return plan.roster.map((item) => {
    const unit = state.units[item.instanceId]
    return {
      unitInstanceId: item.instanceId,
      side: item.side,
      campaignUnitInstanceId: item.campaignUnitInstanceId ?? null,
      cityId: item.cityId ?? null,
      cohortId: item.cohortId ?? null,
      unitId: item.unitId ?? null,
      deployed: unit?.deployed ? 1 : 0,
      survived: unit?.deployed && unit.alive ? 1 : 0,
      healthRatio: unit?.deployed && unit.alive ? unit.hp / Math.max(1, unit.maxHp) : 0,
    }
  }).filter(item => item.deployed > 0)
}

export function clashResultFromState(plan: ClashPlan, state: ClashSimulationState): ClashResult | null {
  if (state.phase !== 'finished' || !state.outcome || !state.terminalReason) return null
  const revealedPassiveIds = Object.values(state.units)
    .flatMap(unit => unit.hiddenPassiveIdsRevealed)
    .sort(stableCompare)
  return {
    kind: 'clash',
    sessionId: plan.sessionId,
    planId: plan.id,
    planDigest: digestTdValue(plan),
    rulesIdentity: cloneJson(plan.rulesIdentity),
    seed: state.seed,
    outcome: state.outcome,
    winner: state.winner,
    terminalReason: state.terminalReason,
    turns: state.turn,
    clashes: state.clashNumber,
    turnLog: cloneJson(state.commandLog),
    commandDigest: digestTdValue(state.commandLog),
    deployments: deploymentResults(plan, state),
    finalMorale: cloneJson(state.morale),
    revealedPassiveIds,
    log: cloneJson(state.log.slice(-plan.resultLogLimit)),
    error: state.error,
  }
}

export function replayClashState(
  plan: ClashPlan,
  seed: string | number,
  commandLog: readonly ClashCommand[],
): ClashSimulationState {
  let state = createInitialClashState(plan, seed)
  for (const command of commandLog) {
    state = applyClashCommand(plan, state, command)
    if (state.error) break
    if (state.phase === 'finished') break
  }
  return state
}

export function replayClash(
  plan: ClashPlan,
  seed: string | number,
  commandLog: readonly ClashCommand[],
): ClashResult | ClashSimulationState {
  const state = replayClashState(plan, seed, commandLog)
  return clashResultFromState(plan, state) ?? state
}

export function resolveClash(
  plan: ClashPlan,
  seed: string | number,
  commandLog: readonly ClashCommand[],
): ClashResult {
  const replayed = replayClash(plan, seed, commandLog)
  if ('kind' in replayed && replayed.kind === 'clash') return replayed
  throw new Error(replayed.error ?? 'Clash command log did not reach a terminal state.')
}

export function abortClash(
  plan: ClashPlan,
  seed: string | number,
  commandLog: readonly ClashCommand[] = [],
  abortTurn?: number,
): ClashResult {
  const replayed = replayClash(plan, seed, commandLog)
  if ('kind' in replayed && replayed.kind === 'clash') return replayed
  const state = cloneJson(replayed)
  if (abortTurn !== undefined && abortTurn !== state.turn) {
    state.error = 'Abort turn does not match the replayed Clash turn.'
  }
  settleWinner(state, null, 'aborted')
  return clashResultFromState(plan, state)!
}
