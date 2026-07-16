import {
  LAST_CHANCES_ATTACK_KINDS,
  LAST_CHANCES_EQUIP_MODES,
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

const LEGACY_SPAWN_RELOCATIONS: Record<string, Record<string, { x: number, y: number }>> = {
  'chest-gallery': {
    '730:160': { x: 780, y: 160 },
    '720:550': { x: 785, y: 550 },
    '425:345': { x: 350, y: 345 },
  },
  'wrong-shadow-event': {
    '570:595': { x: 650, y: 595 },
    '420:345': { x: 420, y: 370 },
  },
}

export class LastChancesConfigError extends Error {
  readonly errors: string[]

  constructor(message: string, errors: string[]) {
    super(`${message}: ${errors.join('; ')}`)
    this.name = 'LastChancesConfigError'
    this.errors = errors
  }
}

/**
 * Repairs the concrete schema-v1 definition that shipped before geometry and
 * cooldown invariants were enforced. Schema v2 is never rewritten here.
 */
export function migrateLastChancesConfig(value: unknown): unknown {
  if (typeof value !== 'object' || value === null || Array.isArray(value)
    || (value as UnknownRecord).schemaVersion !== 1) return value
  const migrated = JSON.parse(JSON.stringify(value)) as UnknownRecord

  if (Array.isArray(migrated.weapons)) {
    migrated.weapons.forEach((weaponValue) => {
      if (typeof weaponValue !== 'object' || weaponValue === null || Array.isArray(weaponValue)) return
      const weapon = weaponValue as UnknownRecord
      for (const attackSetKey of ['attacks', 'secondaryAttacks'] as const) {
        const attackSet = weapon[attackSetKey]
        if (typeof attackSet !== 'object' || attackSet === null || Array.isArray(attackSet)) continue
        const tap = (attackSet as UnknownRecord).tap
        if (typeof tap === 'object' && tap !== null && !Array.isArray(tap)) {
          (tap as UnknownRecord).cooldownMs = 0
        }
      }
      for (const comboKey of ['tapCombo', 'secondaryTapCombo'] as const) {
        const combo = weapon[comboKey]
        if (!Array.isArray(combo)) continue
        combo.forEach((attack) => {
          if (typeof attack === 'object' && attack !== null && !Array.isArray(attack)) {
            (attack as UnknownRecord).cooldownMs = 0
          }
        })
      }
    })
  }

  const { globalEnemyRadius, roomEnemyRadii } = eligibleEnemySpawnRadii(migrated)
  const player = typeof migrated.player === 'object' && migrated.player !== null
    && !Array.isArray(migrated.player) ? migrated.player as UnknownRecord : null
  const playerRadius = player ? Math.max(0, finiteRecordNumber(player, 'radius') ?? 0) : 0

  if (Array.isArray(migrated.rooms)) {
    migrated.rooms.forEach((roomValue) => {
      if (typeof roomValue !== 'object' || roomValue === null || Array.isArray(roomValue)) return
      const room = roomValue as UnknownRecord
      if (typeof room.id !== 'string' || !Array.isArray(room.enemySpawns)) return
      const relocations = LEGACY_SPAWN_RELOCATIONS[room.id]
      if (!relocations) return
      const enemyRadius = roomEnemyRadii.get(room.id) ?? globalEnemyRadius
      room.enemySpawns = room.enemySpawns.map((spawnValue) => {
        if (typeof spawnValue !== 'object' || spawnValue === null || Array.isArray(spawnValue)) return spawnValue
        const spawn = spawnValue as UnknownRecord
        const replacement = relocations[`${spawn.x}:${spawn.y}`]
        if (!replacement
          || spawnFitsRoomGeometry(spawn, enemyRadius, room, playerRadius)
          || !spawnFitsRoomGeometry(replacement, enemyRadius, room, playerRadius)) return spawnValue
        return { ...replacement }
      })
    })
  }

  return migrated
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

function validateAttackSet(value: unknown, path: string, errors: string[]): void {
  const attacks = asRecord(value, path, errors)
  if (!attacks) return
  for (const gesture of LAST_CHANCES_GESTURES) {
    validateAttack(attacks[gesture], `${path}.${gesture}`, errors)
  }
  const tap = attacks.tap
  if (typeof tap === 'object' && tap !== null && !Array.isArray(tap)
    && (tap as UnknownRecord).cooldownMs !== 0) {
    errors.push(`${path}.tap.cooldownMs must be 0 because basic taps have no cooldown`)
  }
}

function validateTapCombo(
  value: unknown,
  path: string,
  errors: string[],
  required: boolean,
): void {
  if (value === undefined && !required) return
  if (!Array.isArray(value) || value.length < 1) {
    errors.push(`${path} must contain at least one basic-combo follow-up`)
    return
  }
  value.forEach((attack, index) => {
    const attackPath = `${path}[${index}]`
    validateAttack(attack, attackPath, errors)
    if (typeof attack === 'object' && attack !== null && !Array.isArray(attack)
      && (attack as UnknownRecord).cooldownMs !== 0) {
      errors.push(`${attackPath}.cooldownMs must be 0 because basic taps have no cooldown`)
    }
  })
}

function validateRooms(value: unknown, errors: string[], requireSpawnLayouts: boolean): Set<string> {
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
    if (room.enemySpawns !== undefined && (!Array.isArray(room.enemySpawns) || room.enemySpawns.length === 0)) {
      errors.push(`${path}.enemySpawns must be a non-empty array when provided`)
    } else if (Array.isArray(room.enemySpawns)) {
      room.enemySpawns.forEach((spawn, spawnIndex) => {
        validateVector(spawn, `${path}.enemySpawns[${spawnIndex}]`, errors)
      })
    }
    if (requireSpawnLayouts && room.spawnLayouts === undefined) {
      errors.push(`${path}.spawnLayouts is required by schemaVersion 2`)
    }
    if (room.spawnLayouts !== undefined && (!Array.isArray(room.spawnLayouts) || room.spawnLayouts.length < 2)) {
      errors.push(`${path}.spawnLayouts must contain at least two named layouts when provided`)
    } else if (Array.isArray(room.spawnLayouts)) {
      const layoutIds = new Set<string>()
      room.spawnLayouts.forEach((layoutValue, layoutIndex) => {
        const layoutPath = `${path}.spawnLayouts[${layoutIndex}]`
        const layout = asRecord(layoutValue, layoutPath, errors)
        if (!layout) return
        requireString(layout, 'id', layoutPath, errors)
        requireString(layout, 'name', layoutPath, errors)
        if (!Array.isArray(layout.enemySpawns) || layout.enemySpawns.length === 0) {
          errors.push(`${layoutPath}.enemySpawns must be a non-empty array`)
        } else {
          layout.enemySpawns.forEach((spawn, spawnIndex) => {
            validateVector(spawn, `${layoutPath}.enemySpawns[${spawnIndex}]`, errors)
          })
        }
        if (typeof layout.id === 'string') {
          if (layoutIds.has(layout.id)) errors.push(`${layoutPath}.id duplicates ${layout.id}`)
          layoutIds.add(layout.id)
        }
      })
    }
    if (room.enemySpawns === undefined && room.spawnLayouts === undefined) {
      errors.push(`${path} must define enemySpawns or spawnLayouts`)
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

function validateEnemies(value: unknown, errors: string[], schemaVersion: number): Set<string> {
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
    if (schemaVersion === 2 || enemy.idleTurnRadiansPerSecond !== undefined) {
      requireNumber(enemy, 'idleTurnRadiansPerSecond', path, errors)
    }
    if (schemaVersion === 2 || enemy.preferredAttackRangeRatio !== undefined) {
      requirePositiveNumber(enemy, 'preferredAttackRangeRatio', path, errors)
      if (typeof enemy.preferredAttackRangeRatio === 'number'
        && enemy.preferredAttackRangeRatio > 1) {
        errors.push(`${path}.preferredAttackRangeRatio must be <= 1`)
      }
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

function inferredEquipMode(weapon: UnknownRecord): string {
  if (typeof weapon.equipMode === 'string') return weapon.equipMode
  return weapon.hand === 'right' ? 'secondaryOnly' : 'primaryOnly'
}

function validateWeapons(
  value: unknown,
  loadoutValue: unknown,
  errors: string[],
  schemaVersion: number,
): void {
  if (!Array.isArray(value) || value.length === 0) {
    errors.push('weapons must be a non-empty array')
    return
  }
  const hasLoadout = loadoutValue !== undefined
  const ids = new Set<string>()
  const hands = new Set<string>()
  const catalog = new Map<string, UnknownRecord>()
  value.forEach((item, index) => {
    const path = `weapons[${index}]`
    const weapon = asRecord(item, path, errors)
    if (!weapon) return
    requireString(weapon, 'id', path, errors)
    requireString(weapon, 'name', path, errors)
    if (weapon.hand !== undefined
      && !LAST_CHANCES_HANDS.includes(weapon.hand as typeof LAST_CHANCES_HANDS[number])) {
      errors.push(`${path}.hand must be left or right`)
    }
    if (schemaVersion === 2 && weapon.hand !== undefined) {
      errors.push(`${path}.hand is legacy-only; schemaVersion 2 uses loadout and equipMode`)
    }
    if (weapon.equipMode !== undefined
      && !LAST_CHANCES_EQUIP_MODES.includes(weapon.equipMode as typeof LAST_CHANCES_EQUIP_MODES[number])) {
      errors.push(`${path}.equipMode must be one of ${LAST_CHANCES_EQUIP_MODES.join(', ')}`)
    }
    if (schemaVersion === 2 && weapon.equipMode === undefined) {
      errors.push(`${path}.equipMode is required by schemaVersion 2`)
    }
    validateAttackSet(weapon.attacks, `${path}.attacks`, errors)
    validateTapCombo(weapon.tapCombo, `${path}.tapCombo`, errors, schemaVersion === 2)
    if (weapon.secondaryAttacks !== undefined) {
      validateAttackSet(weapon.secondaryAttacks, `${path}.secondaryAttacks`, errors)
    }
    const equipMode = inferredEquipMode(weapon)
    if ((equipMode === 'twoHanded' || equipMode === 'hybrid') && weapon.secondaryAttacks === undefined) {
      errors.push(`${path}.secondaryAttacks is required for ${equipMode} equipment`)
    }
    validateTapCombo(
      weapon.secondaryTapCombo,
      `${path}.secondaryTapCombo`,
      errors,
      schemaVersion === 2 && (equipMode === 'twoHanded' || equipMode === 'hybrid'),
    )
    if (typeof weapon.id === 'string') {
      if (ids.has(weapon.id)) errors.push(`${path}.id duplicates ${weapon.id}`)
      ids.add(weapon.id)
      catalog.set(weapon.id, weapon)
    }
    if (!hasLoadout && typeof weapon.hand === 'string') {
      if (hands.has(weapon.hand)) errors.push(`${path}.hand duplicates ${weapon.hand}`)
      hands.add(weapon.hand)
    }
  })

  if (!hasLoadout) {
    if (schemaVersion === 2) {
      errors.push('loadout is required by schemaVersion 2')
    } else {
      if (value.length !== 2) errors.push('legacy weapons without loadout must contain exactly two definitions')
      for (const hand of LAST_CHANCES_HANDS) {
        if (!hands.has(hand)) errors.push(`weapons must define the ${hand} hand`)
      }
    }
    return
  }

  const loadout = asRecord(loadoutValue, 'loadout', errors)
  if (!loadout) return
  requireString(loadout, 'primaryWeaponId', 'loadout', errors)
  if (loadout.secondaryWeaponId !== null
    && (typeof loadout.secondaryWeaponId !== 'string' || loadout.secondaryWeaponId.trim().length === 0)) {
    errors.push('loadout.secondaryWeaponId must be a non-empty string or null')
  }
  if (typeof loadout.primaryWeaponId !== 'string') return
  const primary = catalog.get(loadout.primaryWeaponId)
  if (!primary) {
    errors.push(`loadout.primaryWeaponId references unknown weapon ${loadout.primaryWeaponId}`)
    return
  }
  const primaryMode = inferredEquipMode(primary)
  if (primaryMode === 'secondaryOnly') {
    errors.push('loadout.primaryWeaponId cannot equip a secondaryOnly weapon')
  }

  const secondaryId = typeof loadout.secondaryWeaponId === 'string'
    ? loadout.secondaryWeaponId
    : null
  if (primaryMode === 'twoHanded' && secondaryId) {
    errors.push('loadout.secondaryWeaponId must be null while a twoHanded weapon is equipped')
  }
  if (!secondaryId) return

  const secondary = catalog.get(secondaryId)
  if (!secondary) {
    errors.push(`loadout.secondaryWeaponId references unknown weapon ${secondaryId}`)
    return
  }
  const secondaryMode = inferredEquipMode(secondary)
  if (secondaryMode !== 'secondaryOnly' && secondaryMode !== 'eitherHand') {
    errors.push('loadout.secondaryWeaponId must equip a secondaryOnly or eitherHand weapon')
  }
  if (secondaryId === loadout.primaryWeaponId && primaryMode !== 'eitherHand') {
    errors.push('only eitherHand weapons may be equipped in both hands')
  }
}

function finiteRecordNumber(record: UnknownRecord, key: string): number | null {
  const value = record[key]
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

function pointOverlapsObstacle(
  point: UnknownRecord,
  radius: number,
  obstacle: UnknownRecord,
): boolean {
  const x = finiteRecordNumber(point, 'x')
  const y = finiteRecordNumber(point, 'y')
  const obstacleX = finiteRecordNumber(obstacle, 'x')
  const obstacleY = finiteRecordNumber(obstacle, 'y')
  const width = finiteRecordNumber(obstacle, 'width')
  const height = finiteRecordNumber(obstacle, 'height')
  if (x === null || y === null || obstacleX === null || obstacleY === null
    || width === null || height === null) return false
  const nearestX = Math.max(obstacleX, Math.min(obstacleX + width, x))
  const nearestY = Math.max(obstacleY, Math.min(obstacleY + height, y))
  return Math.hypot(x - nearestX, y - nearestY) < radius
}

function eligibleEnemySpawnRadii(root: UnknownRecord): {
  globalEnemyRadius: number
  roomEnemyRadii: Map<string, number>
} {
  const enemyRadii = new Map<string, number>()
  if (Array.isArray(root.enemies)) {
    root.enemies.forEach((enemyValue) => {
      if (typeof enemyValue !== 'object' || enemyValue === null || Array.isArray(enemyValue)) return
      const enemy = enemyValue as UnknownRecord
      const radius = finiteRecordNumber(enemy, 'radius')
      if (typeof enemy.id === 'string' && radius !== null) enemyRadii.set(enemy.id, radius)
    })
  }

  const roomEnemyRadii = new Map<string, number>()
  const progression = typeof root.progression === 'object' && root.progression !== null
    && !Array.isArray(root.progression) ? root.progression as UnknownRecord : null
  if (progression && Array.isArray(progression.tiers)) {
    progression.tiers.forEach((tierValue) => {
      if (typeof tierValue !== 'object' || tierValue === null || Array.isArray(tierValue)) return
      const tier = tierValue as UnknownRecord
      if (!Array.isArray(tier.roomTemplateIds) || !Array.isArray(tier.enemyPool)) return
      const radius = Math.max(0, ...tier.enemyPool.flatMap((poolValue) => {
        if (typeof poolValue !== 'object' || poolValue === null || Array.isArray(poolValue)) return []
        const enemyId = (poolValue as UnknownRecord).enemyId
        return typeof enemyId === 'string' && enemyRadii.has(enemyId)
          ? [enemyRadii.get(enemyId) as number]
          : []
      }))
      tier.roomTemplateIds.forEach((roomId) => {
        if (typeof roomId !== 'string') return
        roomEnemyRadii.set(roomId, Math.max(roomEnemyRadii.get(roomId) ?? 0, radius))
      })
    })
  }

  return {
    globalEnemyRadius: Math.max(0, ...enemyRadii.values()),
    roomEnemyRadii,
  }
}

function spawnFitsRoomGeometry(
  point: UnknownRecord,
  radius: number,
  room: UnknownRecord,
  playerRadius: number,
): boolean {
  const x = finiteRecordNumber(point, 'x')
  const y = finiteRecordNumber(point, 'y')
  const width = finiteRecordNumber(room, 'width')
  const height = finiteRecordNumber(room, 'height')
  if (x === null || y === null || width === null || height === null
    || x < radius || x > width - radius || y < radius || y > height - radius) return false
  if (Array.isArray(room.obstacles) && room.obstacles.some((obstacleValue) => {
    return typeof obstacleValue === 'object' && obstacleValue !== null && !Array.isArray(obstacleValue)
      && pointOverlapsObstacle(point, radius, obstacleValue as UnknownRecord)
  })) return false
  if (typeof room.playerSpawn !== 'object' || room.playerSpawn === null
    || Array.isArray(room.playerSpawn)) return true
  const playerSpawn = room.playerSpawn as UnknownRecord
  const playerX = finiteRecordNumber(playerSpawn, 'x')
  const playerY = finiteRecordNumber(playerSpawn, 'y')
  return playerX === null || playerY === null
    || Math.hypot(x - playerX, y - playerY) >= radius + playerRadius
}

function validateSpawnPoint(
  value: unknown,
  path: string,
  radius: number,
  room: UnknownRecord,
  errors: string[],
): void {
  const point = typeof value === 'object' && value !== null && !Array.isArray(value)
    ? value as UnknownRecord
    : null
  if (!point) return
  const x = finiteRecordNumber(point, 'x')
  const y = finiteRecordNumber(point, 'y')
  const width = finiteRecordNumber(room, 'width')
  const height = finiteRecordNumber(room, 'height')
  if (x === null || y === null || width === null || height === null) return
  if (x < radius || x > width - radius || y < radius || y > height - radius) {
    errors.push(`${path} must fit inside its room with radius ${radius}`)
  }
  if (!Array.isArray(room.obstacles)) return
  room.obstacles.forEach((obstacleValue, obstacleIndex) => {
    if (typeof obstacleValue !== 'object' || obstacleValue === null || Array.isArray(obstacleValue)) return
    if (pointOverlapsObstacle(point, radius, obstacleValue as UnknownRecord)) {
      errors.push(`${path} overlaps room obstacle ${obstacleIndex} with radius ${radius}`)
    }
  })
}

function roomSpawnCollections(
  room: UnknownRecord,
  roomPath: string,
): Array<{ path: string, spawns: unknown[] }> {
  if (Array.isArray(room.spawnLayouts) && room.spawnLayouts.length > 0) {
    return room.spawnLayouts.flatMap((layoutValue, layoutIndex) => {
      if (typeof layoutValue !== 'object' || layoutValue === null || Array.isArray(layoutValue)) return []
      const layout = layoutValue as UnknownRecord
      return Array.isArray(layout.enemySpawns)
        ? [{ path: `${roomPath}.spawnLayouts[${layoutIndex}].enemySpawns`, spawns: layout.enemySpawns }]
        : []
    })
  }
  return Array.isArray(room.enemySpawns)
    ? [{ path: `${roomPath}.enemySpawns`, spawns: room.enemySpawns }]
    : []
}

function validateSpawnSpacing(
  spawns: unknown[],
  path: string,
  radius: number,
  errors: string[],
): void {
  for (let firstIndex = 0; firstIndex < spawns.length; firstIndex += 1) {
    const first = spawns[firstIndex]
    if (typeof first !== 'object' || first === null || Array.isArray(first)) continue
    const firstX = finiteRecordNumber(first as UnknownRecord, 'x')
    const firstY = finiteRecordNumber(first as UnknownRecord, 'y')
    if (firstX === null || firstY === null) continue
    for (let secondIndex = firstIndex + 1; secondIndex < spawns.length; secondIndex += 1) {
      const second = spawns[secondIndex]
      if (typeof second !== 'object' || second === null || Array.isArray(second)) continue
      const secondX = finiteRecordNumber(second as UnknownRecord, 'x')
      const secondY = finiteRecordNumber(second as UnknownRecord, 'y')
      if (secondX === null || secondY === null) continue
      if (Math.hypot(firstX - secondX, firstY - secondY) < radius * 2) {
        errors.push(`${path}[${firstIndex}] overlaps spawn ${secondIndex} with radius ${radius}`)
      }
    }
  }
}

function validateSpawnGeometry(root: UnknownRecord, errors: string[]): void {
  if (!Array.isArray(root.rooms) || !Array.isArray(root.enemies)) return
  const { globalEnemyRadius, roomEnemyRadii } = eligibleEnemySpawnRadii(root)
  const roomEnemyCounts = new Map<string, number>()
  const progression = typeof root.progression === 'object' && root.progression !== null
    && !Array.isArray(root.progression) ? root.progression as UnknownRecord : null
  if (progression && Array.isArray(progression.tiers)) {
    progression.tiers.forEach((tierValue) => {
      if (typeof tierValue !== 'object' || tierValue === null || Array.isArray(tierValue)) return
      const tier = tierValue as UnknownRecord
      if (!Array.isArray(tier.roomTemplateIds) || !Array.isArray(tier.enemyPool)) return
      tier.roomTemplateIds.forEach((roomId) => {
        if (typeof roomId !== 'string') return
        const maximumCount = Array.isArray(tier.enemyCount) && Number.isInteger(tier.enemyCount[1])
          ? tier.enemyCount[1] as number
          : 0
        roomEnemyCounts.set(roomId, Math.max(roomEnemyCounts.get(roomId) ?? 0, maximumCount))
      })
    })
  }
  const player = typeof root.player === 'object' && root.player !== null && !Array.isArray(root.player)
    ? root.player as UnknownRecord : null
  const playerRadius = player && typeof player.radius === 'number' && Number.isFinite(player.radius)
    ? player.radius
    : 0

  root.rooms.forEach((roomValue, roomIndex) => {
    if (typeof roomValue !== 'object' || roomValue === null || Array.isArray(roomValue)) return
    const room = roomValue as UnknownRecord
    const roomPath = `rooms[${roomIndex}]`
    const roomWidth = finiteRecordNumber(room, 'width')
    const roomHeight = finiteRecordNumber(room, 'height')
    if (roomWidth !== null && roomHeight !== null && Array.isArray(room.obstacles)) {
      room.obstacles.forEach((obstacleValue, obstacleIndex) => {
        if (typeof obstacleValue !== 'object' || obstacleValue === null || Array.isArray(obstacleValue)) return
        const obstacle = obstacleValue as UnknownRecord
        const x = finiteRecordNumber(obstacle, 'x')
        const y = finiteRecordNumber(obstacle, 'y')
        const width = finiteRecordNumber(obstacle, 'width')
        const height = finiteRecordNumber(obstacle, 'height')
        if (x !== null && y !== null && width !== null && height !== null
          && (x + width > roomWidth || y + height > roomHeight)) {
          errors.push(`${roomPath}.obstacles[${obstacleIndex}] must fit inside its room`)
        }
      })
    }
    validateSpawnPoint(room.playerSpawn, `${roomPath}.playerSpawn`, playerRadius, room, errors)
    const enemyRadius = typeof room.id === 'string'
      ? roomEnemyRadii.get(room.id) ?? globalEnemyRadius
      : globalEnemyRadius
    const requiredCount = typeof room.id === 'string' ? roomEnemyCounts.get(room.id) ?? 0 : 0
    roomSpawnCollections(room, roomPath).forEach((collection) => {
      if (collection.spawns.length < requiredCount) {
        errors.push(`${collection.path} needs at least ${requiredCount} points for eligible tiers`)
      }
      collection.spawns.forEach((spawn, spawnIndex) => {
        validateSpawnPoint(spawn, `${collection.path}[${spawnIndex}]`, enemyRadius, room, errors)
        if (typeof spawn !== 'object' || spawn === null || Array.isArray(spawn)
          || typeof room.playerSpawn !== 'object' || room.playerSpawn === null
          || Array.isArray(room.playerSpawn)) return
        const spawnX = finiteRecordNumber(spawn as UnknownRecord, 'x')
        const spawnY = finiteRecordNumber(spawn as UnknownRecord, 'y')
        const playerX = finiteRecordNumber(room.playerSpawn as UnknownRecord, 'x')
        const playerY = finiteRecordNumber(room.playerSpawn as UnknownRecord, 'y')
        if (spawnX !== null && spawnY !== null && playerX !== null && playerY !== null
          && Math.hypot(spawnX - playerX, spawnY - playerY) < playerRadius + enemyRadius) {
          errors.push(
            `${collection.path}[${spawnIndex}] overlaps playerSpawn with combined radius ${playerRadius + enemyRadius}`,
          )
        }
      })
      validateSpawnSpacing(collection.spawns, collection.path, enemyRadius, errors)
    })
  })
}

function validateGraphCapacity(root: UnknownRecord, errors: string[]): void {
  const graph = typeof root.graph === 'object' && root.graph !== null && !Array.isArray(root.graph)
    ? root.graph as UnknownRecord : null
  const progression = typeof root.progression === 'object' && root.progression !== null
    && !Array.isArray(root.progression) ? root.progression as UnknownRecord : null
  if (!graph || !progression || !Array.isArray(progression.tiers)
    || !Number.isInteger(graph.choicesPerNode) || (graph.choicesPerNode as number) < 1) return
  for (let index = 0; index < progression.tiers.length - 1; index += 1) {
    const currentValue = progression.tiers[index]
    const nextValue = progression.tiers[index + 1]
    if (typeof currentValue !== 'object' || currentValue === null || Array.isArray(currentValue)
      || typeof nextValue !== 'object' || nextValue === null || Array.isArray(nextValue)) continue
    const currentCount = (currentValue as UnknownRecord).nodeCount
    const nextCount = (nextValue as UnknownRecord).nodeCount
    if (!Number.isInteger(currentCount) || !Number.isInteger(nextCount)) continue
    if ((currentCount as number) * (graph.choicesPerNode as number) < (nextCount as number)) {
      errors.push(`graph.choicesPerNode cannot connect every progression.tiers[${index + 1}] node`)
    }
  }
}

export function validateLastChancesConfig(value: unknown): LastChancesConfigValidation {
  const errors: string[] = []
  const root = asRecord(value, 'config', errors)
  if (!root) return { valid: false, errors }

  if (root.schemaVersion !== 1 && root.schemaVersion !== 2) {
    errors.push('schemaVersion must be 1 or 2')
  }
  const schemaVersion = root.schemaVersion === 2 ? 2 : 1
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
    if (schemaVersion === 2 || input.tapComboWindowMs !== undefined) {
      requirePositiveNumber(input, 'tapComboWindowMs', 'input', errors)
    }
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
  const roomIds = validateRooms(root.rooms, errors, schemaVersion === 2)
  const enemyIds = validateEnemies(root.enemies, errors, schemaVersion)
  if (progression) {
    requireNumber(progression, 'roomHpRecovery', 'progression', errors)
    requireNumber(progression, 'roomMentalRecovery', 'progression', errors)
    validateTiers(progression.tiers, roomIds, enemyIds, errors)
  }
  validateGraphCapacity(root, errors)
  validateSpawnGeometry(root, errors)
  validateWeapons(root.weapons, root.loadout, errors, schemaVersion)

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
  const migrated = migrateLastChancesConfig(value)
  const validation = validateLastChancesConfig(migrated)
  if (!validation.valid) throw new LastChancesConfigError(`Invalid 99LC config from ${source}`, validation.errors)
  return cloneLastChancesConfig(migrated as LastChancesConfig)
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
