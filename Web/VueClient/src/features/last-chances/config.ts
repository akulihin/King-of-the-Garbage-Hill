import {
  LAST_CHANCES_ATTACK_KINDS,
  LAST_CHANCES_GESTURES,
  LAST_CHANCES_HANDS,
} from './types'
import type {
  LastChancesConfig,
  LastChancesConfigValidation,
  LoadLastChancesConfigOptions,
} from './types'

export const LAST_CHANCES_CONFIG_URL = '/99lc/game-config.json'
export const LAST_CHANCES_CONFIG_STORAGE_KEY = '99lc:game-config'

type UnknownRecord = Record<string, unknown>

export class LastChancesConfigError extends Error {
  readonly errors: string[]

  constructor(message: string, errors: string[]) {
    super(`${message}: ${errors.join('; ')}`)
    this.name = 'LastChancesConfigError'
    this.errors = errors
  }
}

function asRecord(value: unknown, path: string, errors: string[]): UnknownRecord | null {
  if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
    return value as UnknownRecord
  }
  errors.push(`${path} must be an object`)
  return null
}

function requireString(record: UnknownRecord, key: string, path: string, errors: string[]): void {
  if (typeof record[key] !== 'string' || (record[key] as string).trim().length === 0) {
    errors.push(`${path}.${key} must be a non-empty string`)
  }
}

function requireNumber(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
  minimum = 0,
): void {
  const value = record[key]
  if (typeof value !== 'number' || !Number.isFinite(value) || value < minimum) {
    errors.push(`${path}.${key} must be a finite number >= ${minimum}`)
  }
}

function requirePositiveNumber(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
): void {
  const value = record[key]
  if (typeof value !== 'number' || !Number.isFinite(value) || value <= 0) {
    errors.push(`${path}.${key} must be a finite number > 0`)
  }
}

function requireInteger(
  record: UnknownRecord,
  key: string,
  path: string,
  errors: string[],
  minimum = 0,
): void {
  const value = record[key]
  if (!Number.isInteger(value) || (value as number) < minimum) {
    errors.push(`${path}.${key} must be an integer >= ${minimum}`)
  }
}

function requireStringArray(record: UnknownRecord, key: string, path: string, errors: string[]): void {
  const value = record[key]
  if (!Array.isArray(value) || value.length === 0
    || value.some(item => typeof item !== 'string' || item.trim().length === 0)) {
    errors.push(`${path}.${key} must be a non-empty string array`)
  }
}

function validateVector(value: unknown, path: string, errors: string[]): void {
  const record = asRecord(value, path, errors)
  if (!record) return
  requireNumber(record, 'x', path, errors)
  requireNumber(record, 'y', path, errors)
}

function validateStats(value: unknown, path: string, errors: string[], allowZero: boolean): void {
  const record = asRecord(value, path, errors)
  if (!record) return
  if (allowZero) {
    for (const key of ['maxHp', 'maxMentalHealth', 'attackPower', 'moveSpeed', 'armor'] as const) {
      requireNumber(record, key, path, errors)
    }
  } else {
    for (const key of ['maxHp', 'maxMentalHealth', 'attackPower', 'moveSpeed'] as const) {
      requirePositiveNumber(record, key, path, errors)
    }
    requireNumber(record, 'armor', path, errors)
  }
}

function validateAttack(value: unknown, path: string, errors: string[]): void {
  const record = asRecord(value, path, errors)
  if (!record) return
  requireString(record, 'name', path, errors)
  if (!LAST_CHANCES_ATTACK_KINDS.includes(record.kind as typeof LAST_CHANCES_ATTACK_KINDS[number])) {
    errors.push(`${path}.kind must be one of ${LAST_CHANCES_ATTACK_KINDS.join(', ')}`)
  }
  requireNumber(record, 'damage', path, errors)
  requireNumber(record, 'cooldownMs', path, errors)
  requireNumber(record, 'range', path, errors)
  requireNumber(record, 'radius', path, errors)
  requireNumber(record, 'arcDegrees', path, errors)
  requireNumber(record, 'durationMs', path, errors)
  requireNumber(record, 'projectileSpeed', path, errors)
  requireInteger(record, 'pierce', path, errors)
  requireNumber(record, 'knockback', path, errors)
  requireString(record, 'color', path, errors)
}

function validateRooms(value: unknown, errors: string[]): Set<string> {
  const ids = new Set<string>()
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('rooms must be a non-empty array')
    return ids
  }
  value.forEach((item, index) => {
    const path = `rooms[${index}]`
    const room = asRecord(item, path, errors)
    if (!room) return
    requireString(room, 'id', path, errors)
    requireString(room, 'name', path, errors)
    if (!['combat', 'chest', 'rest', 'event'].includes(String(room.archetype))) {
      errors.push(`${path}.archetype must be combat, chest, rest, or event`)
    }
    requirePositiveNumber(room, 'width', path, errors)
    requirePositiveNumber(room, 'height', path, errors)
    validateVector(room.playerSpawn, `${path}.playerSpawn`, errors)
    if (!Array.isArray(room.enemySpawns) || room.enemySpawns.length === 0) {
      errors.push(`${path}.enemySpawns must be a non-empty array`)
    } else {
      room.enemySpawns.forEach((spawn, spawnIndex) => {
        validateVector(spawn, `${path}.enemySpawns[${spawnIndex}]`, errors)
      })
    }
    if (!Array.isArray(room.obstacles)) {
      errors.push(`${path}.obstacles must be an array`)
    } else {
      room.obstacles.forEach((itemObstacle, obstacleIndex) => {
        const obstaclePath = `${path}.obstacles[${obstacleIndex}]`
        const obstacle = asRecord(itemObstacle, obstaclePath, errors)
        if (!obstacle) return
        requireNumber(obstacle, 'x', obstaclePath, errors)
        requireNumber(obstacle, 'y', obstaclePath, errors)
        requirePositiveNumber(obstacle, 'width', obstaclePath, errors)
        requirePositiveNumber(obstacle, 'height', obstaclePath, errors)
        requireNumber(obstacle, 'elevation', obstaclePath, errors)
      })
    }
    if (typeof room.id === 'string') {
      if (ids.has(room.id)) errors.push(`${path}.id duplicates ${room.id}`)
      ids.add(room.id)
    }
  })
  return ids
}

function validateEnemies(value: unknown, errors: string[]): Set<string> {
  const ids = new Set<string>()
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('enemies must be a non-empty array')
    return ids
  }
  value.forEach((item, index) => {
    const path = `enemies[${index}]`
    const enemy = asRecord(item, path, errors)
    if (!enemy) return
    requireString(enemy, 'id', path, errors)
    requireString(enemy, 'name', path, errors)
    for (const key of ['maxHp', 'radius', 'moveSpeed', 'visionRange', 'noticeMs', 'alertPauseMs', 'attackRange',
      'attackCooldownMs', 'attackWindupMs'] as const) {
      requirePositiveNumber(enemy, key, path, errors)
    }
    requireNumber(enemy, 'visionAngleDegrees', path, errors)
    requireNumber(enemy, 'attackDamage', path, errors)
    requireNumber(enemy, 'mentalPressurePerSecond', path, errors)
    requireString(enemy, 'color', path, errors)
    if (typeof enemy.id === 'string') {
      if (ids.has(enemy.id)) errors.push(`${path}.id duplicates ${enemy.id}`)
      ids.add(enemy.id)
    }
  })
  return ids
}

function validateTiers(
  value: unknown,
  roomIds: Set<string>,
  enemyIds: Set<string>,
  errors: string[],
): void {
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('progression.tiers must be a non-empty ordered array')
    return
  }
  const ids = new Set<string>()
  value.forEach((item, index) => {
    const path = `progression.tiers[${index}]`
    const tier = asRecord(item, path, errors)
    if (!tier) return
    requireString(tier, 'id', path, errors)
    requireString(tier, 'label', path, errors)
    if (tier.kind !== 'normal' && tier.kind !== 'boss') {
      errors.push(`${path}.kind must be normal or boss`)
    }
    requireInteger(tier, 'nodeCount', path, errors, 1)
    requireInteger(tier, 'deathCost', path, errors, 1)
    requireString(tier, 'accent', path, errors)
    validateStats(tier.erosion, `${path}.erosion`, errors, true)
    if (!Array.isArray(tier.enemyCount) || tier.enemyCount.length !== 2
      || !tier.enemyCount.every(count => Number.isInteger(count) && count >= 0)
      || (tier.enemyCount[0] as number) > (tier.enemyCount[1] as number)) {
      errors.push(`${path}.enemyCount must be [minimum, maximum] non-negative integers`)
    }
    if (!Array.isArray(tier.enemyPool) || tier.enemyPool.length === 0) {
      errors.push(`${path}.enemyPool must be a non-empty array`)
    } else {
      tier.enemyPool.forEach((poolItem, poolIndex) => {
        const poolPath = `${path}.enemyPool[${poolIndex}]`
        const pool = asRecord(poolItem, poolPath, errors)
        if (!pool) return
        requireString(pool, 'enemyId', poolPath, errors)
        requirePositiveNumber(pool, 'weight', poolPath, errors)
        if (typeof pool.enemyId === 'string' && !enemyIds.has(pool.enemyId)) {
          errors.push(`${poolPath}.enemyId references unknown enemy ${pool.enemyId}`)
        }
      })
    }
    requireStringArray(tier, 'roomTemplateIds', path, errors)
    if (Array.isArray(tier.roomTemplateIds)) {
      tier.roomTemplateIds.forEach((roomId) => {
        if (typeof roomId === 'string' && !roomIds.has(roomId)) {
          errors.push(`${path}.roomTemplateIds references unknown room ${roomId}`)
        }
      })
    }
    if (typeof tier.id === 'string') {
      if (ids.has(tier.id)) errors.push(`${path}.id duplicates ${tier.id}`)
      ids.add(tier.id)
    }
  })
  const terminal = value[value.length - 1]
  if (typeof terminal !== 'object' || terminal === null || Array.isArray(terminal)
    || (terminal as UnknownRecord).kind !== 'boss') {
    errors.push('progression.tiers must end with a boss tier')
  }
}

function validateWeapons(value: unknown, errors: string[]): void {
  if (!Array.isArray(value) || value.length !== 2) {
    errors.push('weapons must contain exactly two weapon definitions')
    return
  }
  const ids = new Set<string>()
  const hands = new Set<string>()
  value.forEach((item, index) => {
    const path = `weapons[${index}]`
    const weapon = asRecord(item, path, errors)
    if (!weapon) return
    requireString(weapon, 'id', path, errors)
    requireString(weapon, 'name', path, errors)
    if (!LAST_CHANCES_HANDS.includes(weapon.hand as typeof LAST_CHANCES_HANDS[number])) {
      errors.push(`${path}.hand must be left or right`)
    }
    const attacks = asRecord(weapon.attacks, `${path}.attacks`, errors)
    if (attacks) {
      for (const gesture of LAST_CHANCES_GESTURES) {
        validateAttack(attacks[gesture], `${path}.attacks.${gesture}`, errors)
      }
    }
    if (typeof weapon.id === 'string') {
      if (ids.has(weapon.id)) errors.push(`${path}.id duplicates ${weapon.id}`)
      ids.add(weapon.id)
    }
    if (typeof weapon.hand === 'string') {
      if (hands.has(weapon.hand)) errors.push(`${path}.hand duplicates ${weapon.hand}`)
      hands.add(weapon.hand)
    }
  })
  for (const hand of LAST_CHANCES_HANDS) {
    if (!hands.has(hand)) errors.push(`weapons must define the ${hand} hand`)
  }
}

export function validateLastChancesConfig(value: unknown): LastChancesConfigValidation {
  const errors: string[] = []
  const root = asRecord(value, 'config', errors)
  if (!root) return { valid: false, errors }

  if (root.schemaVersion !== 1) errors.push('schemaVersion must be 1')
  requireString(root, 'title', 'config', errors)
  requireString(root, 'seed', 'config', errors)
  requireInteger(root, 'chances', 'config', errors, 1)

  const graph = asRecord(root.graph, 'graph', errors)
  if (graph) {
    requireInteger(graph, 'choicesPerNode', 'graph', errors, 1)
    requireInteger(graph, 'generationSeedStep', 'graph', errors, 1)
  }

  const input = asRecord(root.input, 'input', errors)
  if (input) {
    requirePositiveNumber(input, 'doubleTapMs', 'input', errors)
    requirePositiveNumber(input, 'holdMs', 'input', errors)
    requirePositiveNumber(input, 'holdMaxMs', 'input', errors)
    requirePositiveNumber(input, 'holdThenDoubleTapWindowMs', 'input', errors)
    requireNumber(input, 'aimDeadZone', 'input', errors)
    requireNumber(input, 'gamepadDeadZone', 'input', errors)
    requireInteger(input, 'gamepadLeftButton', 'input', errors)
    requireInteger(input, 'gamepadRightButton', 'input', errors)
    requireStringArray(input, 'leftKeys', 'input', errors)
    requireStringArray(input, 'rightKeys', 'input', errors)
    if (typeof input.holdMs === 'number' && typeof input.holdMaxMs === 'number'
      && input.holdMaxMs < input.holdMs) {
      errors.push('input.holdMaxMs must be >= input.holdMs')
    }
  }

  const player = asRecord(root.player, 'player', errors)
  if (player) {
    requirePositiveNumber(player, 'radius', 'player', errors)
    requireNumber(player, 'invulnerabilityMs', 'player', errors)
    validateStats(player.baseStats, 'player.baseStats', errors, false)
  }

  const mentalHealth = asRecord(root.mentalHealth, 'mentalHealth', errors)
  if (mentalHealth) {
    requireNumber(mentalHealth, 'calmRecoveryPerSecond', 'mentalHealth', errors)
    requireNumber(mentalHealth, 'restoreOnKill', 'mentalHealth', errors)
    requireNumber(mentalHealth, 'maxPressurePerSecond', 'mentalHealth', errors)
  }

  const progression = asRecord(root.progression, 'progression', errors)
  const roomIds = validateRooms(root.rooms, errors)
  const enemyIds = validateEnemies(root.enemies, errors)
  if (progression) {
    requireNumber(progression, 'roomHpRecovery', 'progression', errors)
    requireNumber(progression, 'roomMentalRecovery', 'progression', errors)
    validateTiers(progression.tiers, roomIds, enemyIds, errors)
  }
  validateWeapons(root.weapons, errors)

  const renderer = asRecord(root.renderer, 'renderer', errors)
  if (renderer) {
    requirePositiveNumber(renderer, 'maxDpr', 'renderer', errors)
    requirePositiveNumber(renderer, 'snapshotHz', 'renderer', errors)
    requirePositiveNumber(renderer, 'floorGridSize', 'renderer', errors)
    for (const key of ['background', 'floor', 'floorGrid', 'obstacleTop', 'obstacleSide',
      'player', 'playerAccent', 'mental'] as const) {
      requireString(renderer, key, 'renderer', errors)
    }
  }

  return { valid: errors.length === 0, errors }
}

function assertValidConfig(value: unknown, source: string): LastChancesConfig {
  const validation = validateLastChancesConfig(value)
  if (!validation.valid) throw new LastChancesConfigError(`Invalid 99LC config from ${source}`, validation.errors)
  return cloneLastChancesConfig(value as LastChancesConfig)
}

function getBrowserStorage(): Storage | null {
  if (typeof window === 'undefined') return null
  try {
    return window.localStorage
  } catch {
    return null
  }
}

export function cloneLastChancesConfig(config: LastChancesConfig): LastChancesConfig {
  return JSON.parse(JSON.stringify(config)) as LastChancesConfig
}

export function saveLastChancesConfig(config: LastChancesConfig): void {
  const validated = assertValidConfig(config, 'builder')
  const storage = getBrowserStorage()
  if (!storage) throw new Error('Browser storage is unavailable')
  storage.setItem(LAST_CHANCES_CONFIG_STORAGE_KEY, JSON.stringify(validated))
}

export function clearLastChancesConfig(): void {
  getBrowserStorage()?.removeItem(LAST_CHANCES_CONFIG_STORAGE_KEY)
}

export async function loadLastChancesConfig(
  options: LoadLastChancesConfigOptions = {},
): Promise<LastChancesConfig> {
  const useBrowserOverride = options.useBrowserOverride ?? true
  const storage = getBrowserStorage()
  const override = useBrowserOverride ? storage?.getItem(LAST_CHANCES_CONFIG_STORAGE_KEY) : null
  if (override) {
    let value: unknown
    try {
      value = JSON.parse(override) as unknown
    } catch {
      throw new LastChancesConfigError('Invalid 99LC browser override', ['stored value is not valid JSON'])
    }
    return assertValidConfig(value, 'browser override')
  }

  const url = options.url ?? LAST_CHANCES_CONFIG_URL
  const response = await fetch(url, { cache: 'no-store', signal: options.signal })
  if (!response.ok) throw new Error(`Unable to load 99LC config (${response.status} ${response.statusText})`)
  return assertValidConfig(await response.json() as unknown, url)
}
