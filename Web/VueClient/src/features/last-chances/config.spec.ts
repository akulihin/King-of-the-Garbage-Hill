import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import {
  clearLastChancesConfig,
  cloneLastChancesConfig,
  loadLastChancesConfig,
  saveLastChancesConfig,
  validateLastChancesConfig,
} from './config'
import { resolveLastChancesLoadout } from './equipment'
import { buildLastChancesPlan } from './plan'
import type {
  LastChancesConfig,
  LastChancesEquipMode,
  LastChancesWeaponDefinition,
} from './types'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

function makeWeapon(
  id: string,
  equipMode: LastChancesEquipMode,
  sourceIndex = 0,
): LastChancesWeaponDefinition {
  const weapon = cloneLastChancesConfig(defaultConfig).weapons[sourceIndex] as LastChancesWeaponDefinition
  weapon.id = id
  weapon.name = id
  weapon.equipMode = equipMode
  delete weapon.hand
  delete weapon.secondaryAttacks
  return weapon
}

function secondaryAttacks(prefix: string) {
  const attacks = cloneLastChancesConfig(defaultConfig).weapons[1].attacks
  for (const [gesture, attack] of Object.entries(attacks)) attack.name = `${prefix}:${gesture}`
  return attacks
}

function removeRoomInteractions(config: LastChancesConfig): void {
  config.rooms.forEach(room => { delete room.interaction })
}

function previousShippedSchemaV1Config(): LastChancesConfig {
  const config = cloneLastChancesConfig(defaultConfig)
  config.schemaVersion = 1
  delete config.input.tapComboWindowMs
  delete config.loadout
  removeRoomInteractions(config)
  const previousSpawns: Record<string, Array<{ x: number, y: number }>> = {
    'combat-hall': [
      { x: 700, y: 170 }, { x: 805, y: 335 }, { x: 690, y: 520 }, { x: 510, y: 120 },
      { x: 520, y: 560 }, { x: 370, y: 325 }, { x: 835, y: 95 }, { x: 850, y: 580 },
    ],
    'chest-gallery': [
      { x: 730, y: 160 }, { x: 790, y: 345 }, { x: 720, y: 550 }, { x: 540, y: 145 },
      { x: 555, y: 545 }, { x: 425, y: 345 }, { x: 820, y: 85 }, { x: 825, y: 620 },
    ],
    'rest-conservatory': [
      { x: 770, y: 170 }, { x: 855, y: 355 }, { x: 770, y: 555 }, { x: 580, y: 120 },
      { x: 590, y: 605 }, { x: 430, y: 360 }, { x: 875, y: 95 }, { x: 875, y: 625 },
    ],
    'wrong-shadow-event': [
      { x: 755, y: 145 }, { x: 840, y: 345 }, { x: 755, y: 560 }, { x: 560, y: 105 },
      { x: 570, y: 595 }, { x: 420, y: 345 }, { x: 875, y: 85 }, { x: 875, y: 620 },
    ],
    'curator-threshold': [
      { x: 820, y: 380 }, { x: 900, y: 200 }, { x: 900, y: 560 },
      { x: 690, y: 140 }, { x: 690, y: 620 }, { x: 530, y: 380 },
    ],
  }
  config.rooms.forEach((room) => {
    room.enemySpawns = previousSpawns[room.id] ?? room.spawnLayouts?.[0].enemySpawns
    delete room.spawnLayouts
  })
  config.enemies.forEach((enemy) => {
    delete enemy.idleTurnRadiansPerSecond
    delete enemy.preferredAttackRangeRatio
  })
  config.weapons = config.weapons.slice(0, 2)
  config.weapons.forEach((weapon, index) => {
    weapon.hand = index === 0 ? 'left' : 'right'
    delete weapon.equipMode
    delete weapon.tapCombo
    delete weapon.secondaryAttacks
    delete weapon.secondaryTapCombo
  })
  config.weapons[0].attacks.tap.cooldownMs = 280
  config.weapons[1].attacks.tap.cooldownMs = 210
  return config
}

function schema3Config(): LastChancesConfig {
  const config = cloneLastChancesConfig(defaultConfig)
  config.schemaVersion = 3
  config.weapons.forEach((weapon) => {
    weapon.trait ??= 'spearDistance'
    const attackCollections = [
      Object.values(weapon.attacks),
      ...(weapon.tapCombo ? [weapon.tapCombo] : []),
      ...(weapon.secondaryAttacks ? [Object.values(weapon.secondaryAttacks)] : []),
      ...(weapon.secondaryTapCombo ? [weapon.secondaryTapCombo] : []),
    ]
    attackCollections.flat().forEach((attack) => {
      attack.behavior ??= 'standard'
      attack.collider ??= { shape: 'sector', traceMs: 180 }
    })
  })
  return config
}

describe('99LC config and deterministic plan', () => {
  beforeEach(() => clearLastChancesConfig())
  afterEach(() => { vi.unstubAllGlobals() })

  it('accepts the shipped builder config and its terminal boss tier', () => {
    const result = validateLastChancesConfig(defaultConfig)

    expect(result.errors).toEqual([])
    expect(defaultConfig.schemaVersion).toBe(3)
    expect(defaultConfig.chances).toBe(99)
    expect(defaultConfig.rooms.every(room => (room.spawnLayouts?.length ?? 0) >= 2)).toBe(true)
    expect(defaultConfig.progression.tiers).toHaveLength(7)
    expect(defaultConfig.progression.tiers.slice(0, 6).every(tier => tier.kind === 'normal')).toBe(true)
    expect(defaultConfig.progression.tiers[defaultConfig.progression.tiers.length - 1]?.kind).toBe('boss')
    expect(defaultConfig.progression.tiers.every(tier => tier.deathCost === 1)).toBe(true)
    const loadout = resolveLastChancesLoadout(defaultConfig)
    const shippedSpear = defaultConfig.weapons.find(weapon => weapon.id === 'twohand-spear')
    expect(shippedSpear?.equipMode).toBe('twoHanded')
    expect(shippedSpear?.trait).toBe('spearDistance')
    expect(shippedSpear?.secondaryAttacks).toBeDefined()
    expect(shippedSpear?.secondaryTapCombo?.length).toBeGreaterThanOrEqual(1)
    expect(loadout.left?.tapCombo.length).toBeGreaterThanOrEqual(2)
    expect(loadout.right?.tapCombo.length).toBeGreaterThanOrEqual(2)
    expect(new Set(loadout.left?.tapCombo.map(attack => attack.name)).size).toBe(loadout.left?.tapCombo.length)
    expect(new Set(loadout.right?.tapCombo.map(attack => attack.name)).size).toBe(loadout.right?.tapCombo.length)
    const unsupplemented = cloneLastChancesConfig(defaultConfig)
    unsupplemented.loadout!.secondaryWeaponId = null
    expect(resolveLastChancesLoadout(unsupplemented)).toMatchObject({
      left: { id: 'twohand-spear', hand: 'left', augment: 'none' },
      right: { id: 'twohand-spear', hand: 'right', augment: 'none' },
    })
    expect(defaultConfig.enemies.map(enemy => enemy.name)).toEqual(expect.arrayContaining([
      'Слуга',
      'Бегущий степлер',
      'Стражник',
      'Химера',
      'Нож-паук',
      'Невидимый волк',
      'Тень Куратора',
    ]))
    expect(defaultConfig.rooms.filter(room => room.interaction)).toHaveLength(5)
    expect(defaultConfig.rooms.flatMap(room => room.hazards ?? [])).toHaveLength(4)
    expect(defaultConfig.weapons.map(weapon => weapon.id)).toEqual([
      'twohand-spear',
      'secondary-chain',
      'either-claws',
      'secondary-spider-knife',
      'twohand-axe',
      'twohand-katana',
      'hybrid-sword',
    ])
  })

  it('rebuilds identical rooms, enemies, and links for the same generation', () => {
    const first = buildLastChancesPlan(defaultConfig, 3)
    const retry = buildLastChancesPlan(defaultConfig, 3)
    const nextGeneration = buildLastChancesPlan(defaultConfig, 4)

    expect(retry).toEqual(first)
    expect(nextGeneration).not.toEqual(first)
    expect(first.tiers).toHaveLength(defaultConfig.progression.tiers.length)
    expect(first.tiers[first.tiers.length - 1]).toHaveLength(1)

    for (let tierIndex = 0; tierIndex < first.tiers.length - 1; tierIndex += 1) {
      const currentTier = first.tiers[tierIndex]
      const nextTier = first.tiers[tierIndex + 1]
      expect(currentTier.every(node => node.nextNodeIds.length <= defaultConfig.graph.choicesPerNode)).toBe(true)
      expect(currentTier.every(node => new Set(node.nextNodeIds).size === node.nextNodeIds.length)).toBe(true)
      expect(new Set(currentTier.flatMap(node => node.nextNodeIds))).toEqual(new Set(nextTier.map(node => node.id)))
    }
    for (const node of first.nodes) {
      const room = defaultConfig.rooms.find(candidate => candidate.id === node.roomTemplateId)
      const layout = room?.spawnLayouts?.find(candidate => candidate.id === node.spawnLayoutId)
      expect(layout).toBeTruthy()
      expect(new Set(node.enemies.map(enemy => `${enemy.position.x}:${enemy.position.y}`)).size)
        .toBe(node.enemies.length)
      for (const enemy of node.enemies) {
        expect(layout?.enemySpawns).toContainEqual(enemy.position)
      }
    }
  })

  it('rejects a graph whose choice cap cannot cover the next tier', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.progression.tiers[0].nodeCount = 1
    invalid.graph.choicesPerNode = 2

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'graph.choicesPerNode cannot connect every progression.tiers[1] node',
    )
  })

  it('selects more than the first authored spawn layout across generations', () => {
    const seenByRoom = new Map<string, Set<string>>()
    for (let generation = 1; generation <= 24; generation += 1) {
      for (const node of buildLastChancesPlan(defaultConfig, generation).nodes) {
        const seen = seenByRoom.get(node.roomTemplateId) ?? new Set<string>()
        seen.add(node.spawnLayoutId)
        seenByRoom.set(node.roomTemplateId, seen)
      }
    }

    expect(defaultConfig.rooms.some((room) => {
      const expected = new Set(room.spawnLayouts?.map(layout => layout.id))
      const seen = seenByRoom.get(room.id) ?? new Set<string>()
      return expected.size > 1 && [...expected].every(layoutId => seen.has(layoutId))
    })).toBe(true)
  })

  it('rejects authored player and enemy spawns that overlap room geometry', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const roomIndex = invalid.rooms.findIndex(room => room.id === 'chest-gallery')
    invalid.rooms[roomIndex].spawnLayouts![0].enemySpawns[0] = { x: 675, y: 120 }
    invalid.rooms[roomIndex].playerSpawn = { x: 445, y: 330 }

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      expect.stringContaining(`rooms[${roomIndex}].spawnLayouts[0].enemySpawns[0] overlaps room obstacle 1`),
      expect.stringContaining(`rooms[${roomIndex}].playerSpawn overlaps room obstacle 0`),
    ]))
  })

  it('rejects enemy layout points that overlap the player spawn circles', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.rooms[0].spawnLayouts![0].enemySpawns[0] = { ...invalid.rooms[0].playerSpawn }

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'rooms[0].spawnLayouts[0].enemySpawns[0] overlaps playerSpawn with combined radius 42',
    )
  })

  it('rejects a named spawn layout that cannot hold its tier maximum', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    const roomIndex = invalid.rooms.findIndex(room => room.id === 'rest-conservatory')
    invalid.rooms[roomIndex].spawnLayouts![0].enemySpawns.length = 6

    expect(validateLastChancesConfig(invalid).errors).toContain(
      `rooms[${roomIndex}].spawnLayouts[0].enemySpawns needs at least 7 points for eligible tiers`,
    )
  })

  it('clones deeply and loads a validated browser builder override', async () => {
    const override = cloneLastChancesConfig(defaultConfig)
    override.seed = 'builder-override'
    override.progression.tiers[0].deathCost = 2
    saveLastChancesConfig(override)

    const loaded = await loadLastChancesConfig({ url: '/fetch-must-not-run.json' })

    expect(loaded.seed).toBe('builder-override')
    expect(loaded.progression.tiers[0].deathCost).toBe(2)
    loaded.progression.tiers[0].deathCost = 7
    expect(override.progression.tiers[0].deathCost).toBe(2)
  })

  it('rejects a progression without a terminal boss component', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.progression.tiers[invalid.progression.tiers.length - 1].kind = 'normal'

    expect(validateLastChancesConfig(invalid).errors).toContain(
      'progression.tiers must end with a boss tier',
    )
  })

  it('fetches runtime JSON without using the HTTP cache', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => defaultConfig,
    })
    vi.stubGlobal('fetch', fetchMock)

    await loadLastChancesConfig({ url: '/99lc/test-config.json', useBrowserOverride: false })

    expect(fetchMock).toHaveBeenCalledWith('/99lc/test-config.json', {
      cache: 'no-store',
      signal: undefined,
    })
  })

  it('keeps schema-v1 hand slots, enemySpawns, and single basic taps backward-compatible', () => {
    const legacy = cloneLastChancesConfig(defaultConfig)
    legacy.schemaVersion = 1
    delete legacy.loadout
    delete legacy.input.tapComboWindowMs
    for (const room of legacy.rooms) {
      room.enemySpawns = room.spawnLayouts![0].enemySpawns
      delete room.spawnLayouts
    }
    for (const enemy of legacy.enemies) {
      delete enemy.idleTurnRadiansPerSecond
      delete enemy.preferredAttackRangeRatio
    }
    removeRoomInteractions(legacy)
    legacy.weapons = legacy.weapons.slice(0, 2)
    legacy.weapons.forEach((weapon, index) => {
      weapon.hand = index === 0 ? 'left' : 'right'
      delete weapon.equipMode
      delete weapon.tapCombo
      delete weapon.secondaryAttacks
      delete weapon.secondaryTapCombo
    })

    expect(validateLastChancesConfig(legacy).errors).toEqual([])
    expect(resolveLastChancesLoadout(legacy)).toMatchObject({
      left: { id: legacy.weapons[0].id, hand: 'left' },
      right: { id: legacy.weapons[1].id, hand: 'right' },
    })
  })

  it('migrates the previously shipped schema-v1 browser override before strict validation', async () => {
    const previous = previousShippedSchemaV1Config()
    expect(validateLastChancesConfig(previous).valid).toBe(false)
    window.localStorage.setItem('99lc:game-config', JSON.stringify(previous))

    const migrated = await loadLastChancesConfig({ url: '/fetch-must-not-run.json' })

    expect(migrated.schemaVersion).toBe(1)
    expect(migrated.weapons.map(weapon => weapon.attacks.tap.cooldownMs)).toEqual([0, 0])
    expect(migrated.rooms.find(room => room.id === 'chest-gallery')?.enemySpawns).toContainEqual({ x: 780, y: 160 })
    expect(migrated.rooms.find(room => room.id === 'wrong-shadow-event')?.enemySpawns).toContainEqual({ x: 420, y: 370 })
    expect(validateLastChancesConfig(migrated).errors).toEqual([])
  })

  it('keeps a safe legacy spawn when customized geometry makes its replacement unsafe', async () => {
    const previous = previousShippedSchemaV1Config()
    const chestGallery = previous.rooms.find(room => room.id === 'chest-gallery')!
    Object.assign(chestGallery.obstacles[1], { x: 775, y: 130, width: 20, height: 60 })
    window.localStorage.setItem('99lc:game-config', JSON.stringify(previous))

    const migrated = await loadLastChancesConfig({ url: '/fetch-must-not-run.json' })
    const spawns = migrated.rooms.find(room => room.id === 'chest-gallery')?.enemySpawns

    expect(spawns).toContainEqual({ x: 730, y: 160 })
    expect(spawns).not.toContainEqual({ x: 780, y: 160 })
    expect(spawns).toContainEqual({ x: 785, y: 550 })
    expect(spawns).toContainEqual({ x: 350, y: 345 })
    expect(validateLastChancesConfig(migrated).errors).toEqual([])
  })

  it('upgrades a saved schema-v2 override while preserving run tuning and adopting the schema-v3 arsenal', async () => {
    const legacy = cloneLastChancesConfig(defaultConfig)
    legacy.schemaVersion = 2
    legacy.seed = 'saved-schema-v2-run'
    legacy.player.baseStats.attackPower = 137
    legacy.enemies[0].moveSpeed = 73
    const renamedWeapons = new Map<string, string>()
    legacy.weapons.forEach((weapon) => {
      const previousId = weapon.id
      weapon.id = `legacy-${previousId}`
      renamedWeapons.set(previousId, weapon.id)
      delete weapon.trait
    })
    legacy.loadout!.primaryWeaponId = renamedWeapons.get(legacy.loadout!.primaryWeaponId)!
    legacy.loadout!.secondaryWeaponId = legacy.loadout!.secondaryWeaponId
      ? renamedWeapons.get(legacy.loadout!.secondaryWeaponId) ?? null
      : null
    legacy.rooms.forEach((room) => {
      room.interaction?.choices.forEach((choice) => {
        if (choice.effect.primaryWeaponId) {
          choice.effect.primaryWeaponId = renamedWeapons.get(choice.effect.primaryWeaponId)
        }
        if (choice.effect.secondaryWeaponId) {
          choice.effect.secondaryWeaponId = renamedWeapons.get(choice.effect.secondaryWeaponId)
        }
      })
    })
    expect(validateLastChancesConfig(legacy).errors).toEqual([])
    window.localStorage.setItem('99lc:game-config', JSON.stringify(legacy))
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => defaultConfig,
    })
    vi.stubGlobal('fetch', fetchMock)

    const migrated = await loadLastChancesConfig({ url: '/99lc/schema-v3.json' })

    expect(fetchMock).toHaveBeenCalledWith('/99lc/schema-v3.json', {
      cache: 'no-store',
      signal: undefined,
    })
    expect(migrated).toMatchObject({
      schemaVersion: 3,
      seed: 'saved-schema-v2-run',
      player: { baseStats: { attackPower: 137 } },
      loadout: defaultConfig.loadout,
    })
    expect(migrated.enemies[0].moveSpeed).toBe(73)
    expect(migrated.weapons.map(weapon => weapon.id)).toEqual(
      defaultConfig.weapons.map(weapon => weapon.id),
    )
    expect(migrated.rooms.find(room => room.id === 'merchant-crossing')?.interaction?.choices)
      .toEqual(expect.arrayContaining([
        expect.objectContaining({
          id: 'buy-chain',
          effect: expect.objectContaining({ secondaryWeaponId: 'secondary-chain' }),
        }),
      ]))
    expect(validateLastChancesConfig(migrated).errors).toEqual([])
  })

  it('requires loadouts, named layouts, and authored tap sequences in schema v2', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.schemaVersion = 2
    delete invalid.loadout
    delete invalid.input.tapComboWindowMs
    invalid.rooms[0].enemySpawns = invalid.rooms[0].spawnLayouts![0].enemySpawns
    delete invalid.rooms[0].spawnLayouts
    delete invalid.weapons[0].tapCombo

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      'loadout is required by schemaVersion 2',
      'input.tapComboWindowMs must be a finite number > 0',
      'rooms[0].spawnLayouts is required by schemaVersion 2 or newer',
      'weapons[0].tapCombo must contain at least one basic-combo follow-up',
    ]))
  })

  it('rejects cooldowns on basic taps and authored combo steps', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.weapons[0].attacks.tap.cooldownMs = 1
    invalid.weapons[1].tapCombo![0].cooldownMs = 25

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      'weapons[0].attacks.tap.cooldownMs must be 0 because basic taps have no cooldown',
      'weapons[1].tapCombo[0].cooldownMs must be 0 because basic taps have no cooldown',
    ]))
  })

  it('validates authored enemy idle turning and preferred attack distance', () => {
    const invalid = cloneLastChancesConfig(defaultConfig)
    invalid.enemies[0].idleTurnRadiansPerSecond = -0.1
    invalid.enemies[1].preferredAttackRangeRatio = 1.01

    expect(validateLastChancesConfig(invalid).errors).toEqual(expect.arrayContaining([
      'enemies[0].idleTurnRadiansPerSecond must be a finite number >= 0',
      'enemies[1].preferredAttackRangeRatio must be <= 1',
    ]))
  })

  describe('schema-v3 combat actions', () => {
    it('accepts typed colliders, charge bands, statuses, resources, and augments', () => {
      const config = schema3Config()
      const weapon = config.weapons[0]
      const attack = weapon.attacks.hold
      weapon.resource = { kind: 'chain', max: 1, initial: 0, label: 'Stored DOT', color: '#86d981' }
      weapon.defaultAugment = 'poison'
      weapon.augmentHooks = {
        poison: {
          behaviors: ['spearRelease'],
          damageMultiplier: 1.2,
          hitEffects: [{
            status: 'poison',
            durationMs: 4200,
            stacks: 2,
            chance: 0.75,
            tickDamage: 3,
            tickMs: 700,
            refresh: 'stack',
          }],
        },
      }
      attack.collider = {
        shape: 'sweep',
        innerRange: 35,
        width: 22,
        traceMs: 360,
        followsPlayer: true,
        tickMs: 120,
        rotationDegrees: -110,
      }
      attack.charge = {
        maxMs: 1800,
        bands: [
          { id: 'early', label: 'Early', minMs: 0, color: '#9bdc7b', damageMultiplier: 0.9 },
          {
            id: 'late',
            label: 'Late',
            minMs: 900,
            color: '#ff9e57',
            rangeMultiplier: 1.5,
            overrides: { pierce: 4, recoveryMs: 250 },
          },
        ],
      }
      attack.hitEffects = [{ status: 'microstun', durationMs: 160, refresh: 'refresh' }]
      attack.repeatHits = 3
      attack.repeatIntervalMs = 140
      attack.sweetSpot = {
        minRange: 90,
        maxRange: 170,
        damageMultiplier: 1.6,
        knockbackMultiplier: 1.4,
        criticalMultiplier: 2,
      }
      config.loadout!.primaryAugment = 'poison'

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).left).toMatchObject({
        trait: weapon.trait,
        resource: weapon.resource,
        augment: 'poison',
      })
    })

    it('accepts an explicitly disabled un-authored gesture', () => {
      const config = schema3Config()
      const attack = config.weapons[0].attacks.holdThenDoubleTap
      attack.enabled = false
      attack.behavior = 'disabled'
      delete attack.collider

      expect(validateLastChancesConfig(config).errors).toEqual([])
    })

    it('rejects malformed disabled actions, colliders, charge bands, and statuses', () => {
      const config = schema3Config()
      const attack = config.weapons[0].attacks.hold
      attack.enabled = false
      attack.behavior = 'standard'
      attack.collider = {
        shape: 'capsule',
        width: 0,
        traceMs: -1,
        tickMs: 0,
      }
      attack.charge = {
        maxMs: 800,
        bands: [
          { id: 'late', label: 'Late', minMs: 600, color: '#fff' },
          { id: 'early', label: 'Early', minMs: 300, color: '#fff' },
          { id: 'too-late', label: 'Too late', minMs: 900, color: '#fff' },
        ],
      }
      attack.hitEffects = [{
        status: 'bleed',
        durationMs: 0,
        chance: 1.1,
        tickMs: 0,
      }]
      attack.repeatHits = 2
      delete attack.repeatIntervalMs

      expect(validateLastChancesConfig(config).errors).toEqual(expect.arrayContaining([
        'weapons[0].attacks.hold.behavior must be disabled when enabled is false',
        'weapons[0].attacks.hold.collider.traceMs must be a finite number >= 0',
        'weapons[0].attacks.hold.collider.width must be a finite number > 0',
        'weapons[0].attacks.hold.collider.tickMs must be a finite number > 0',
        'weapons[0].attacks.hold.charge.bands[1].minMs must be strictly greater than the previous charge band',
        'weapons[0].attacks.hold.charge.bands[2].minMs must be <= weapons[0].attacks.hold.charge.maxMs',
        'weapons[0].attacks.hold.hitEffects[0].durationMs must be a finite number > 0',
        'weapons[0].attacks.hold.hitEffects[0].chance must be <= 1',
        'weapons[0].attacks.hold.hitEffects[0].tickMs must be a finite number > 0',
        'weapons[0].attacks.hold.repeatIntervalMs is required when repeatHits is greater than 1',
      ]))
    })

    it('rejects malformed weapon resources, traits, and augment hooks', () => {
      const config = schema3Config()
      const weapon = config.weapons[0]
      ;(weapon as unknown as { trait: string }).trait = 'unknownTrait'
      weapon.resource = { kind: 'durability', max: 10, initial: 11 }
      ;(config.loadout as unknown as { primaryAugment: string }).primaryAugment = 'ice'
      weapon.augmentHooks = {
        poison: {
          behaviors: ['not-a-behavior' as never],
          damageMultiplier: -0.1,
          hitEffects: [{ status: 'poison', durationMs: 0 }],
        },
      }

      expect(validateLastChancesConfig(config).errors).toEqual(expect.arrayContaining([
        `weapons[0].trait must be one of spearDistance, chainDotCarrier, clawParity, spiderDurability, axeHookRecovery, katanaFlow, swordRhythm`,
        'weapons[0].resource.initial must be <= max',
        'weapons[0].augmentHooks.poison.behaviors[0] uses unknown behavior not-a-behavior',
        'weapons[0].augmentHooks.poison.damageMultiplier must be a finite number >= 0',
        'weapons[0].augmentHooks.poison.hitEffects[0].durationMs must be a finite number > 0',
        'loadout.primaryAugment must be one of none, bleed, poison, fire, chemical',
      ]))
    })
  })

  describe('catalog equipment modes', () => {
    it('resolves a two-handed weapon into ten attacks and rejects a supplemental weapon', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      const weapon = makeWeapon('greatblade', 'twoHanded')
      weapon.secondaryAttacks = secondaryAttacks('greatblade-secondary')
      weapon.augmentHooks = { fire: { damageMultiplier: 1.1 } }
      const supplemental = makeWeapon('sidearm', 'secondaryOnly', 1)
      removeRoomInteractions(config)
      config.weapons = [weapon, supplemental]
      config.loadout = {
        primaryWeaponId: weapon.id,
        secondaryWeaponId: null,
        primaryAugment: 'fire',
        secondaryAugment: 'fire',
      }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config)).toMatchObject({
        left: {
          id: weapon.id,
          hand: 'left',
          augment: 'fire',
          attacks: { tap: { name: weapon.attacks.tap.name } },
        },
        right: {
          id: weapon.id,
          hand: 'right',
          augment: 'fire',
          attacks: { tap: { name: 'greatblade-secondary:tap' } },
        },
      })

      config.loadout.secondaryWeaponId = supplemental.id
      expect(validateLastChancesConfig(config).errors).toContain(
        'loadout.secondaryWeaponId must be null while a twoHanded weapon is equipped',
      )
    })

    it('allows an either-hand weapon in one slot or duplicated into both slots', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      const weapon = makeWeapon('shortsword', 'eitherHand')
      removeRoomInteractions(config)
      config.weapons = [weapon]
      config.loadout = { primaryWeaponId: weapon.id, secondaryWeaponId: weapon.id }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config)).toMatchObject({
        left: { id: weapon.id, hand: 'left' },
        right: { id: weapon.id, hand: 'right' },
      })

      config.loadout.secondaryWeaponId = null
      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toBeNull()
    })

    it('pairs primary-only and secondary-only weapons while permitting an intentionally empty slot', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      const primary = makeWeapon('spear', 'primaryOnly')
      const secondary = makeWeapon('knife', 'secondaryOnly', 1)
      removeRoomInteractions(config)
      config.weapons = [primary, secondary]
      config.loadout = { primaryWeaponId: primary.id, secondaryWeaponId: secondary.id }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config)).toMatchObject({
        left: { id: primary.id, hand: 'left' },
        right: { id: secondary.id, hand: 'right' },
      })

      config.loadout.secondaryWeaponId = null
      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toBeNull()
    })

    it('switches a hybrid between its own second attack set and a supplemental weapon', () => {
      const config = cloneLastChancesConfig(defaultConfig)
      const hybrid = makeWeapon('wand-blade', 'hybrid')
      hybrid.secondaryAttacks = secondaryAttacks('hybrid-secondary')
      hybrid.augmentHooks = { chemical: { damageMultiplier: 1.1 } }
      const supplemental = makeWeapon('shield', 'secondaryOnly', 1)
      supplemental.attacks.tap.name = 'shield-tap'
      supplemental.augmentHooks = { bleed: { damageMultiplier: 1.05 } }
      removeRoomInteractions(config)
      config.weapons = [hybrid, supplemental]
      config.loadout = {
        primaryWeaponId: hybrid.id,
        secondaryWeaponId: null,
        primaryAugment: 'chemical',
        secondaryAugment: 'bleed',
      }

      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toMatchObject({
        id: hybrid.id,
        hand: 'right',
        augment: 'chemical',
        attacks: { tap: { name: 'hybrid-secondary:tap' } },
      })

      config.loadout.secondaryWeaponId = supplemental.id
      expect(validateLastChancesConfig(config).errors).toEqual([])
      expect(resolveLastChancesLoadout(config).right).toMatchObject({
        id: supplemental.id,
        hand: 'right',
        attacks: { tap: { name: 'shield-tap' } },
      })
    })
  })
})
