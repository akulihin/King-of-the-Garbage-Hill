import { describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import { cloneLastChancesConfig } from './config'
import { LastChancesEngine } from './engine'
import {
  DualSenseFeedbackController,
  type LastChancesEnhancedFeedbackOutput,
  type LastChancesFeedbackEffect,
} from './feedback'
import type { LastChancesGamepadReading } from './gamepad'
import { buildLastChancesPlan } from './plan'
import { attackWithLastChancesAugment } from './weapon-runtime'
import {
  applyLastChancesStatusEffects,
  type LastChancesRuntimeStatuses,
  type LastChancesStoredDot,
} from './statuses'
import type {
  LastChancesAttackDefinition,
  LastChancesConfig,
  LastChancesControlContext,
  LastChancesEnemyDefinition,
  LastChancesEnemyState,
  LastChancesGamePlan,
  LastChancesGesture,
  LastChancesGestureResolution,
  LastChancesHand,
  LastChancesResolvedWeapon,
  LastChancesSnapshot,
  LastChancesVector,
} from './types'

const defaultConfig = defaultConfigJson as unknown as LastChancesConfig

function makeCanvas(): HTMLCanvasElement {
  const noop = () => undefined
  const gradient = { addColorStop: noop }
  const context = new Proxy({
    createRadialGradient: () => gradient,
  } as Record<PropertyKey, unknown>, {
    get(target, property) {
      return property in target ? target[property] : noop
    },
    set(target, property, value) {
      target[property] = value
      return true
    },
  })
  const canvas = document.createElement('canvas')
  Object.defineProperty(canvas, 'getContext', { value: () => context })
  Object.defineProperty(canvas, 'getBoundingClientRect', {
    value: () => ({
      x: 0,
      y: 0,
      top: 0,
      left: 0,
      right: 960,
      bottom: 640,
      width: 960,
      height: 640,
      toJSON: noop,
    }),
  })
  return canvas
}

interface RuntimeWeaponState {
  resource: number
  maxResource: number
  storedDot: LastChancesStoredDot | null
  boundEnemyId: string | null
  successfulHits: number
  lastHitHand: LastChancesHand | null
  lastHitAtMs: number
  recoveryMs: number
  lastTapAtMs: number
  rhythm: 'idle' | 'early' | 'good' | 'late'
  perfectTimingMs: number
  fatigueMs: number
  unterhauDueAtMs: number
  unterhauTargetId: string | null
  unterhauTargetPosition: LastChancesVector | null
  unterhauPrimed: boolean
  lastMotionDamageBonus: number
}

interface RuntimeEnemy {
  id: string
  definition: LastChancesEnemyDefinition & { armor?: number }
  position: LastChancesVector
  facing: LastChancesVector
  hp: number
  state: LastChancesEnemyState
  captureWindowMs: number
  statuses: LastChancesRuntimeStatuses
  attackCooldownMs: number
  attackWindupMs: number
  lockedAttackDirection: LastChancesVector | null
  leapRemainingDistance: number
  leapSpeed: number
  leapHit: boolean
  criticalHitMs: number
  lastPlayerHit: { hand: LastChancesHand, gesture: LastChancesGesture } | null
  gestureHits: Record<LastChancesHand, Set<LastChancesGesture>>
  entering: boolean
  swordExecutionMarked: boolean
}

interface RuntimeAttackContext {
  weapon: LastChancesResolvedWeapon
  hand: LastChancesHand
  gesture: LastChancesGesture
  resolution: LastChancesGestureResolution
  comboStep?: number
  chargeBandId?: string
  storedDot: LastChancesStoredDot | null
}

type EngineTestAccess = {
  activeAreas: Array<{
    attack: LastChancesAttackDefinition
    remainingMs: number
    storedDot: LastChancesStoredDot | null
    weaponId: string
    rotationAssisted: boolean
    sweepDegrees: number
    authoredRepeatHits: number
    swordMotionDamageBonus: number
    swordMatchingMousePx: number
  }>
  activeDash: {
    attack: LastChancesAttackDefinition
  } | null
  activeLoadout: {
    primaryWeaponId: string | null
    secondaryWeaponId: string | null
    artifactId?: string | null
    outfitId?: string | null
  } | null
  groundWeapons: Array<{ id: string, weaponId: string, position: LastChancesVector }>
  applyInteractionChoice: (choice: {
    id: string
    title: string
    description: string
    effect: {
      primaryWeaponId?: string
      secondaryWeaponId?: string | null
    }
  }) => void
  cooldownEnds: Map<string, number>
  canExploreRoom: () => boolean
  controlContextActive: (hand: LastChancesHand, context: LastChancesControlContext) => boolean
  applyGamepadReading: (reading: LastChancesGamepadReading | null) => void
  createSnapshot: () => LastChancesSnapshot
  delayedAttacks: Array<{
    remainingMs: number
    attack: LastChancesAttackDefinition
  }>
  damageEnemy: (
    enemy: RuntimeEnemy,
    attack: LastChancesAttackDefinition,
    knockback: number,
    direction: LastChancesVector,
    options?: {
      weaponId?: string
      hand?: LastChancesHand
      gesture?: LastChancesGesture
      storedDot?: LastChancesStoredDot | null
      distance?: number
      damageMultiplier?: number
      impactIntensity?: number
    },
  ) => void
  elapsedMs: number
  frameNowMs: number
  enemies: RuntimeEnemy[]
  effects: unknown[]
  feedbackController: DualSenseFeedbackController
  finishEnemyDeath: (enemy: RuntimeEnemy) => void
  damagePlayerMental: (damage: number) => void
  startEmptyRightHandDash: () => boolean
  gestures: {
    press: (hand: LastChancesHand, atMs: number) => void
    reset: () => void
    update: (atMs: number) => void
  }
  mylorikControls: {
    pressStrike: (hand: LastChancesHand, atMs: number, source: 'gamepad' | 'keyboard' | 'pointer') => void
    pressTechnique: (hand: LastChancesHand, atMs: number, source: 'gamepad' | 'keyboard' | 'pointer') => void
    releaseTechnique: (hand: LastChancesHand, atMs: number, source: 'gamepad' | 'keyboard' | 'pointer') => void
    pressMobility: (hand: LastChancesHand, atMs: number, source: 'gamepad' | 'keyboard' | 'pointer') => void
    releaseMobility: (hand: LastChancesHand, atMs: number, source: 'gamepad' | 'keyboard' | 'pointer') => void
    snapshot: (hand: LastChancesHand, atMs: number) => {
      techniquePressed: boolean
      mobilityPressed: boolean
      buffered: boolean
    }
  }
  dualSenseControls: {
    updateTrigger: (
      hand: LastChancesHand,
      value: number,
      atMs: number,
      controls: LastChancesResolvedWeapon['controls'],
      source: 'gamepad' | 'keyboard' | 'pointer',
    ) => void
    pressBumper: (hand: LastChancesHand, atMs: number, source: 'gamepad' | 'keyboard' | 'pointer') => void
    snapshot: (hand: LastChancesHand, atMs: number) => {
      active: boolean
      nodeId: string | null
    }
  }
  activeMobilityPhysicalHand: LastChancesHand | null
  keyboardDualSenseTriggers: Record<LastChancesHand, {
    down: boolean
    startedAt: number
    gateIndex: number
  }>
  heldChannels: Map<LastChancesHand, EngineTestAccess['activeAreas'][number]>
  killPlayer: (reason: string) => void
  moveQuests: Record<LastChancesHand, {
    unlocked: Record<LastChancesGesture, boolean>
    pendingUnlocks: LastChancesGesture[]
    roomKills: { tap: number, hold: number }
    tapQuestDone: boolean
    holdQuestDone: boolean
    comboQuestDone: boolean
  }>
  performAttack: (resolution: LastChancesGestureResolution) => void
  performDash: (
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: RuntimeAttackContext,
  ) => void
  player: {
    position: LastChancesVector
    aim: LastChancesVector
    hp: number
    mentalHealth: number
    invulnerableMs: number
    recoveryMs: number
    rootMs: number
    parryMs: number
    armorMultiplier: number
    armorMultiplierMs: number
    stats: { maxHp: number, maxMentalHealth: number, moveSpeed: number, armor: number }
  }
  pointerAim: LastChancesVector
  pointerDeltaX: number
  roomElapsedMs: number
  selectMobilityPhysicalHand: (atMs: number) => LastChancesHand | null
  tapCombos: Record<LastChancesHand, { step: number, expiresAtMs: number }>
  spawnZoneAttack: (enemy: RuntimeEnemy) => void
  swarmSpawner: {
    remaining: number
    total: number
    spawnedCount: number
    nextSpawnAtMs: number
    edges: [string, string]
  } | null
  updateSwarmSpawner: () => void
  updateZoneAttacks: () => void
  zoneAttacks: Array<{
    shape: string
    center: LastChancesVector
    size: number
    detonateAtMs: number
  }>
  projectiles: Array<{
    attack?: LastChancesAttackDefinition
    remainingHits: number
    carriedIds?: Set<string>
  }>
  startActiveArea: (
    kind: 'melee' | 'burst',
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    weaponId?: string,
    hand?: LastChancesHand,
    storedDot?: LastChancesStoredDot | null,
    channel?: boolean,
  ) => void
  traces: unknown[]
  update: (deltaSeconds: number, deltaMs: number) => void
  updateClearedRoom: (deltaSeconds: number, deltaMs: number) => void
  updateActiveAreas: (deltaMs: number) => void
  updateDelayedAttacks: (deltaMs: number) => void
  updateDelayedRecoveries: (deltaMs: number) => void
  updateEnemies: (deltaSeconds: number, deltaMs: number) => void
  updateHeldWeaponMechanics: (deltaMs: number) => void
  updateKeyboardDualSenseTriggers: (atMs: number) => void
  updatePlayer: (deltaSeconds: number) => void
  updateProjectiles: (deltaSeconds: number, deltaMs: number) => void
  weaponStates: Map<string, RuntimeWeaponState>
  weapons: Map<LastChancesHand, LastChancesResolvedWeapon>
}

function resolution(
  hand: LastChancesHand,
  gesture: LastChancesGesture,
  heldMs = 0,
  firstHoldMs = heldMs,
): LastChancesGestureResolution {
  return { hand, gesture, atMs: heldMs, heldMs, firstHoldMs }
}

function combatConfig(
  primaryWeaponId: string,
  secondaryWeaponId: string | null,
  enemyId = 'guard',
  enemyCount = 2,
): LastChancesConfig {
  const config = cloneLastChancesConfig(defaultConfig)
  config.loadout = {
    primaryWeaponId,
    secondaryWeaponId,
    primaryAugment: 'none',
    secondaryAugment: 'none',
  }
  config.progression.tiers[0].enemyCount = [enemyCount, enemyCount]
  config.progression.tiers[0].enemyPool = [{ enemyId, weight: 1 }]
  config.progression.tiers[0].roomTemplateIds = ['combat-hall']
  return config
}

function unlockAllMoves(access: EngineTestAccess): void {
  for (const hand of ['left', 'right'] as const) {
    for (const gesture of Object.keys(access.moveQuests[hand].unlocked) as LastChancesGesture[]) {
      access.moveQuests[hand].unlocked[gesture] = true
    }
  }
}

function startCombat(config: LastChancesConfig, options: { unlockMoves?: boolean } = {}): {
  engine: LastChancesEngine
  access: EngineTestAccess
} {
  const engine = new LastChancesEngine(makeCanvas(), config)
  const access = engine as unknown as EngineTestAccess
  // Weapon-mechanics tests opt out of the move-unlock quest chain by default.
  if (options.unlockMoves !== false) unlockAllMoves(access)
  const opening = access.createSnapshot().availableNodeIds[0]
  expect(opening).toBeTruthy()
  expect(engine.chooseNode(opening)).toBe(true)
  access.player.aim = { x: 1, y: 0 }
  return { engine, access }
}

function placeEnemy(
  access: EngineTestAccess,
  enemy: RuntimeEnemy,
  distance: number,
  verticalOffset = 0,
): void {
  enemy.position = {
    x: access.player.position.x + distance,
    y: access.player.position.y + verticalOffset,
  }
}

function weapon(config: LastChancesConfig, id: string) {
  return config.weapons.find(candidate => candidate.id === id)!
}

function gamepadReading(
  pressedIndexes: number[] = [],
  values: Partial<Record<number, number>> = {},
  activeIndex = 0,
): LastChancesGamepadReading {
  const canonicalButtons = Array.from({ length: 16 }, (_, index) => {
    const value = values[index] ?? (pressedIndexes.includes(index) ? 1 : 0)
    return { pressed: pressedIndexes.includes(index), value }
  })
  const pressed = (index: number) => canonicalButtons[index]?.pressed === true
    || (canonicalButtons[index]?.value ?? 0) >= 0.5
  return {
    status: pressedIndexes.length > 0 || Object.values(values).some(value => (value ?? 0) > 0)
      ? 'active'
      : 'idle',
    activeIndex,
    connectedCount: 1,
    id: 'Engine test pad',
    mapping: 'standard',
    profile: 'standard',
    meaningfulInput: true,
    axes: [0, 0, 0, 0],
    move: { x: 0, y: 0 },
    aim: { x: 0, y: 0 },
    buttons: {
      left: pressed(4),
      right: pressed(5),
      l1: pressed(4),
      r1: pressed(5),
      circle: pressed(1),
      cross: pressed(0),
      options: pressed(9),
      dpadUp: pressed(12),
      dpadDown: pressed(13),
      dpadLeft: pressed(14),
      dpadRight: pressed(15),
    },
    triggers: { left: canonicalButtons[6]?.value ?? 0, right: canonicalButtons[7]?.value ?? 0 },
    canonicalButtons,
    sourceButtonIndexes: { left: 4, right: 5 },
  }
}

function driveDualSenseTrigger(
  access: EngineTestAccess,
  physicalHand: LastChancesHand,
  value: number,
  atMs: number,
): void {
  access.elapsedMs = atMs
  access.frameNowMs = Math.max(1, atMs)
  const runtimeHand = physicalHand === 'left' ? 'right' : 'left'
  access.dualSenseControls.updateTrigger(
    physicalHand,
    value,
    atMs,
    access.weapons.get(runtimeHand)?.controls,
    'gamepad',
  )
}

describe('99LC engine attempt lifecycle', () => {
  it('unlocks every move only for the explicit controls QA fixture', () => {
    const normal = new LastChancesEngine(makeCanvas(), defaultConfig)
    const qa = new LastChancesEngine(makeCanvas(), defaultConfig, {}, { qaFixture: 'controls' })

    try {
      const normalSnapshot = (normal as unknown as EngineTestAccess).createSnapshot()
      expect(normalSnapshot.moveQuests.every(quest => (
        quest.unlocked.doubleTap === false
        && quest.unlocked.doubleTapHold === false
        && quest.unlocked.holdThenDoubleTap === false
      ))).toBe(true)

      const expectFullyUnlocked = (snapshot: LastChancesSnapshot) => {
        expect(snapshot.moveQuests.every(quest => (
          Object.values(quest.unlocked).every(Boolean)
          && quest.tapQuestDone
          && quest.holdQuestDone
          && quest.comboQuestDone
        ))).toBe(true)
      }
      expectFullyUnlocked((qa as unknown as EngineTestAccess).createSnapshot())
      qa.newGeneration()
      expectFullyUnlocked((qa as unknown as EngineTestAccess).createSnapshot())
    } finally {
      normal.destroy()
      qa.destroy()
    }
  })

  it('unlocks every move and omits quest UI state when move quests are disabled', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    config.progression.moveQuestsEnabled = false
    const engine = new LastChancesEngine(makeCanvas(), config)
    const access = engine as unknown as EngineTestAccess

    try {
      expect(access.createSnapshot().moveQuests).toEqual([])
      expect(Object.values(access.moveQuests.left.unlocked).every(Boolean)).toBe(true)
      const opening = access.createSnapshot().availableNodeIds[0]
      expect(engine.chooseNode(opening)).toBe(true)
      expect(Object.values(access.moveQuests.right.unlocked).every(Boolean)).toBe(true)
      engine.newGeneration()
      expect(access.createSnapshot().moveQuests).toEqual([])
      expect(Object.values(access.moveQuests.right.unlocked).every(Boolean)).toBe(true)
    } finally {
      engine.destroy()
    }
  })

  it('applies artifact and outfit passives to combat damage, healing, armor, and speed', () => {
    const config = combatConfig('hybrid-sword', null, 'guard', 1)
    config.loadout!.artifactId = 'blood-idol'
    config.loadout!.outfitId = 'knight-armor'
    const { engine, access } = startCombat(config)

    try {
      const snapshot = access.createSnapshot()
      expect(snapshot.player.stats.armor).toBe(config.player.baseStats.armor + 14)
      expect(snapshot.player.stats.moveSpeed).toBeCloseTo(config.player.baseStats.moveSpeed * 0.84)

      const target = access.enemies[0]
      target.definition.armor = 0
      target.hp = 500
      target.definition.maxHp = 500
      access.player.hp = 50
      const attack = weapon(config, 'hybrid-sword').attacks.tap
      access.damageEnemy(target, attack, 0, { x: 1, y: 0 }, {
        hand: 'left',
        gesture: 'tap',
      })
      const damageDealt = 500 - target.hp
      expect(access.player.hp).toBeCloseTo(50 + damageDealt * 0.2)

      access.activeLoadout!.artifactId = 'mind-anchor'
      access.player.mentalHealth = 100
      access.damagePlayerMental(10)
      expect(access.player.mentalHealth).toBe(94)
    } finally {
      engine.destroy()
    }
  })

  it('lets ninja clothing dash on right click semantics only with an empty right hand', () => {
    const config = combatConfig('either-claws', null, 'guard', 1)
    config.loadout!.outfitId = 'ninja-clothes'
    const { engine, access } = startCombat(config)

    try {
      expect(access.weapons.has('right')).toBe(false)
      expect(access.startEmptyRightHandDash()).toBe(true)
      expect(access.activeDash?.attack.name).toBe('Рывок одежды ниндзя')
      expect(access.startEmptyRightHandDash()).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('retries the same generated room while retaining Chance cost and stat erosion', () => {
    const snapshots: LastChancesSnapshot[] = []
    let plan: LastChancesGamePlan | null = null
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig, {
      onPlan: nextPlan => { plan = nextPlan },
      onSnapshot: snapshot => snapshots.push(snapshot),
    })

    try {
      const firstNodeId = snapshots.at(-1)?.availableNodeIds[0]
      expect(firstNodeId).toBeTruthy()
      expect(engine.chooseNode(firstNodeId as string)).toBe(true)
      const firstRoom = snapshots.at(-1) as LastChancesSnapshot
      const firstEnemies = firstRoom.enemies.map(enemy => ({
        id: enemy.id,
        definitionId: enemy.definitionId,
        position: enemy.position,
        facing: enemy.facing,
      }))
      const firstPlan = JSON.stringify(plan)
      const access = engine as unknown as EngineTestAccess

      access.killPlayer('Prototype test')
      const dead = snapshots.at(-1) as LastChancesSnapshot
      expect(dead.phase).toBe('dead')
      expect(dead.chances).toBe(defaultConfig.chances - defaultConfig.progression.tiers[0].deathCost)

      expect(engine.retryAttempt()).toBe(true)
      expect(engine.chooseNode(firstNodeId as string)).toBe(true)
      const retriedRoom = snapshots.at(-1) as LastChancesSnapshot
      expect(retriedRoom.enemies.map(enemy => ({
        id: enemy.id,
        definitionId: enemy.definitionId,
        position: enemy.position,
        facing: enemy.facing,
      }))).toEqual(firstEnemies)
      expect(JSON.stringify(plan)).toBe(firstPlan)
      expect(retriedRoom.player.stats).toEqual(dead.player.stats)
      expect(retriedRoom.player.hp).toBe(dead.player.stats.maxHp)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('keeps the cleared room explorable until the route map is opened', () => {
    const config = combatConfig('hybrid-sword', null, 'guard', 1)
    const { engine, access } = startCombat(config)

    try {
      access.enemies[0].state = 'dead'
      access.update(0.016, 16)
      expect(access.createSnapshot().phase).toBe('planning')
      expect(access.canExploreRoom()).toBe(true)

      const beforeX = access.player.position.x
      engine.setTouchMove(1, 0)
      access.updateClearedRoom(0.25, 250)
      expect(access.player.position.x).toBeGreaterThan(beforeX)

      engine.setRouteMapVisible(true)
      expect(access.canExploreRoom()).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('accepts the gesture-resolution API and keeps ordinary tap chains cooldown-free', () => {
    const config = combatConfig('twohand-spear', null)
    const { engine, access } = startCombat(config)
    const spear = weapon(config, 'twohand-spear')

    try {
      access.performAttack(resolution('left', 'tap'))
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'tap',
        attackName: spear.attacks.tap.name,
        comboStep: 1,
      })
      access.elapsedMs += 300
      access.performAttack(resolution('left', 'tap'))
      expect(access.createSnapshot().lastGesture).toMatchObject({
        attackName: spear.tapCombo![0].name,
        comboStep: 2,
      })
      expect(access.createSnapshot().cooldowns.find(entry => (
        entry.hand === 'left' && entry.gesture === 'tap'
      ))).toEqual({
        hand: 'left',
        gesture: 'tap',
        remainingMs: 0,
        totalMs: 0,
        ready: true,
      })
    } finally {
      engine.destroy()
    }
  })
})

describe('99LC seven-weapon mechanics', () => {
  it('keeps the spear dead zone harmless, boosts the sweet spot, and leaves collider traces', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 2)
    const { engine, access } = startCombat(config)
    const attack = weapon(config, 'twohand-spear').attacks.tap
    const [target, spare] = access.enemies
    placeEnemy(access, spare, 520, 160)

    try {
      placeEnemy(
        access,
        target,
        config.player.radius + target.definition.radius,
      )
      const initialHp = target.hp
      access.startActiveArea('melee', attack, { x: 1, y: 0 }, 'twohand-spear', 'left')
      expect(target.hp).toBe(initialHp)
      access.activeAreas = []

      placeEnemy(access, target, 150)
      access.startActiveArea('melee', attack, { x: 1, y: 0 }, 'twohand-spear', 'left')
      expect(initialHp - target.hp).toBeCloseTo(
        attack.damage * attack.sweetSpot!.damageMultiplier - (target.definition.armor ?? 0),
      )
      access.updateActiveAreas(50)
      expect(access.traces.length).toBeGreaterThan(0)
    } finally {
      engine.destroy()
    }
  })

  it('places a fully charged spear kick target into the primary critical band', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]

    try {
      placeEnemy(access, target, 70)
      access.performAttack(resolution('right', 'doubleTapHold', 1100))
      const distance = Math.sqrt(
        (target.position.x - access.player.position.x) ** 2
          + (target.position.y - access.player.position.y) ** 2,
      )

      expect(distance).toBeGreaterThanOrEqual(140)
      expect(distance).toBeCloseTo(108 * 1.55)
    } finally {
      engine.destroy()
    }
  })

  it('pins a late-spear carried target safely against the room perimeter', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const room = config.rooms.find(candidate => candidate.id === 'combat-hall')!
    room.obstacles = []
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]

    try {
      target.definition.maxHp = 500
      target.definition.armor = 0
      target.hp = 500
      placeEnemy(access, target, 150)
      access.performAttack(resolution('left', 'hold', 1750))

      for (let step = 0; step < 24 && access.projectiles.length > 0; step += 1) {
        access.updateProjectiles(0.08, 80)
      }

      expect(access.projectiles).toHaveLength(0)
      expect(target.position.x).toBeLessThanOrEqual(room.width - target.definition.radius)
      expect(target.position.y).toBeGreaterThanOrEqual(target.definition.radius)
      expect(target.position.y).toBeLessThanOrEqual(room.height - target.definition.radius)
      expect(target.statuses.stunMs).toBeGreaterThanOrEqual(1800)
      expect(target.statuses.disarmMs).toBeGreaterThanOrEqual(1800)
    } finally {
      engine.destroy()
    }
  })

  it('dispatches early, middle, and late spear releases to their distinct colliders', () => {
    const casts = [700, 1200, 1750].map((heldMs) => {
      const config = combatConfig('twohand-spear', null, 'guard', 1)
      const started = startCombat(config)
      started.access.performAttack(resolution('left', 'hold', heldMs))
      return started
    })

    try {
      const [early, middle, late] = casts.map(cast => cast.access)
      expect(early.activeAreas.at(-1)?.attack).toMatchObject({
        kind: 'melee',
        collider: expect.objectContaining({ shape: 'sector', innerRange: 52 }),
      })
      expect(early.projectiles).toHaveLength(0)

      expect(middle.projectiles).toHaveLength(1)
      expect(middle.projectiles[0]).toMatchObject({
        remainingHits: 1,
        attack: expect.objectContaining({
          kind: 'projectile',
          hitEffects: expect.arrayContaining([
            expect.objectContaining({ status: 'stun', durationMs: 1000 }),
          ]),
        }),
      })
      expect(middle.projectiles[0].carriedIds).toBeUndefined()

      expect(late.projectiles).toHaveLength(1)
      expect(late.projectiles[0].remainingHits).toBeGreaterThanOrEqual(9)
      expect(late.projectiles[0].carriedIds).toBeInstanceOf(Set)
    } finally {
      casts.forEach(cast => cast.engine.destroy())
    }
  })

  it('arms the spear spin only after the middle release sector', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)

    try {
      access.performAttack(resolution('left', 'holdThenDoubleTap', 80, 900))
      expect(access.activeAreas).toHaveLength(0)
      expect(access.cooldownEnds.has('left:holdThenDoubleTap')).toBe(false)

      access.performAttack(resolution('left', 'holdThenDoubleTap', 80, 1200))
      expect(access.activeAreas.at(-1)?.attack.behavior).toBe('spearSpin')
      expect(access.cooldownEnds.has('left:holdThenDoubleTap')).toBe(true)
    } finally {
      engine.destroy()
    }
  })

  it('ends a held stance on release without spawning a duplicate post-release hitbox', () => {
    const config = combatConfig('twohand-axe', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const right = access.weapons.get('right')!
    const attack = right.attacks.hold

    try {
      access.startActiveArea('burst', attack, { x: 1, y: 0 }, right.id, 'right', null, true)
      const channel = access.activeAreas.at(-1)!
      access.heldChannels.set('right', channel)
      expect(access.activeAreas).toHaveLength(1)

      access.performAttack(resolution('right', 'hold', 1200))
      expect(access.heldChannels.has('right')).toBe(false)
      expect(access.activeAreas).toHaveLength(0)
      expect(access.cooldownEnds.has('right:hold')).toBe(true)
    } finally {
      engine.destroy()
    }
  })

  it('grants authored invulnerability through the full charged action duration', () => {
    const claws = startCombat(combatConfig('either-claws', null, 'guard', 1))
    const spear = startCombat(combatConfig('twohand-spear', null, 'guard', 1))
    const axe = startCombat(combatConfig('twohand-axe', null, 'guard', 1))
    const katana = startCombat(combatConfig('twohand-katana', null, 'guard', 1))

    try {
      claws.access.performAttack(resolution('left', 'hold', 1100))
      expect(claws.access.player.invulnerableMs).toBe(0)

      spear.access.performAttack(resolution('right', 'holdThenDoubleTap', 80, 900))
      expect(spear.access.player.invulnerableMs).toBe(720)
      expect(spear.access.createSnapshot().player.invulnerableForMs).toBe(720)

      axe.access.performAttack(resolution('right', 'holdThenDoubleTap', 80, 1600))
      expect(axe.access.player.invulnerableMs).toBe(984)

      katana.access.performAttack(resolution('left', 'holdThenDoubleTap', 80, 900))
      expect(katana.access.player.invulnerableMs).toBe(760)
    } finally {
      claws.engine.destroy()
      spear.engine.destroy()
      axe.engine.destroy()
      katana.engine.destroy()
    }
  })

  it('interrupts enemy windups only with authored parry or interrupt actions', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const spear = weapon(config, 'twohand-spear')
    const target = access.enemies[0]

    try {
      placeEnemy(access, target, 100)
      target.state = 'attacking'
      target.attackWindupMs = 100
      access.startActiveArea('melee', spear.attacks.tap, { x: 1, y: 0 }, spear.id, 'left')
      expect(target.state).toBe('attacking')

      access.activeAreas = []
      placeEnemy(access, target, 70)
      target.state = 'attacking'
      target.attackWindupMs = 100
      access.startActiveArea(
        'melee',
        spear.secondaryAttacks!.tap,
        { x: 1, y: 0 },
        spear.id,
        'right',
      )
      expect(target.state).toBe('chasing')
    } finally {
      engine.destroy()
    }
  })

  it('opens parry on short release and rejects a compound gesture only after it parries', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]
    let now = 1000
    vi.spyOn(performance, 'now').mockImplementation(() => now)

    try {
      placeEnemy(access, target, 70)
      target.state = 'attacking'
      target.attackWindupMs = 100

      engine.press('right')
      expect(target.state).toBe('attacking')
      expect(access.player.parryMs).toBe(0)

      now += 80
      engine.release('right')
      expect(target.state).toBe('chasing')
      expect(access.player.parryMs).toBeGreaterThan(0)
      expect(access.traces.length).toBeGreaterThan(0)

      now += 70
      engine.press('right')
      now += 60
      engine.release('right')
      expect(access.createSnapshot().lastGesture?.gesture).toBe('tap')
      expect(access.cooldownEnds.has('right:doubleTap')).toBe(false)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('cancels an unused provisional parry when the tap becomes a compound gesture', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    let now = 1000
    vi.spyOn(performance, 'now').mockImplementation(() => now)

    try {
      engine.press('right')
      now += 70
      engine.release('right')
      expect(access.player.parryMs).toBeGreaterThan(0)

      now += 60
      engine.press('right')
      now += 50
      engine.release('right')

      expect(access.createSnapshot().lastGesture?.gesture).toBe('doubleTap')
      expect(access.player.parryMs).toBe(0)
      expect(access.cooldownEnds.has('right:doubleTap')).toBe(true)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('keeps pole-vault traversal non-damaging along the full invulnerable path', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]
    const right = access.weapons.get('right')!
    const attack = right.attacks.holdThenDoubleTap

    try {
      target.definition.maxHp = 500
      target.definition.armor = 0
      target.hp = 500
      placeEnemy(access, target, 120)
      access.performDash(attack, { x: 1, y: 0 }, {
        weapon: right,
        hand: 'right',
        gesture: 'holdThenDoubleTap',
        resolution: resolution('right', 'holdThenDoubleTap', 80, 900),
        storedDot: null,
      })

      access.updatePlayer(1)

      expect(access.activeDash).toBeNull()
      expect(target.hp).toBe(500)
      expect(access.activeAreas).toHaveLength(0)
    } finally {
      engine.destroy()
    }
  })

  it('keeps the axe leap harmless in flight and applies its damage at landing only', () => {
    const config = combatConfig('twohand-axe', null, 'guard', 2)
    const { engine, access } = startCombat(config)
    const [pathTarget, landingTarget] = access.enemies
    const right = access.weapons.get('right')!
    const attack = right.attacks.holdThenDoubleTap

    try {
      for (const target of [pathTarget, landingTarget]) {
        target.definition.maxHp = 500
        target.definition.armor = 0
        target.hp = 500
      }
      placeEnemy(access, pathTarget, 90)
      placeEnemy(access, landingTarget, attack.range)
      access.performDash(attack, { x: 1, y: 0 }, {
        weapon: right,
        hand: 'right',
        gesture: 'holdThenDoubleTap',
        resolution: resolution('right', 'holdThenDoubleTap', 80, 900),
        storedDot: null,
      })

      access.updatePlayer(1)

      expect(access.activeDash).toBeNull()
      expect(pathTarget.hp).toBe(500)
      expect(landingTarget.hp).toBeLessThan(500)
    } finally {
      engine.destroy()
    }
  })

  it('keeps the charged claw dash traversal harmless and scratches only at its endpoint', () => {
    const config = combatConfig('either-claws', null, 'guard', 2)
    const { engine, access } = startCombat(config)
    const [pathTarget, endpointTarget] = access.enemies
    const attack = access.weapons.get('left')!.attacks.hold
    const chargedRange = attack.range * 1.45

    try {
      for (const target of [pathTarget, endpointTarget]) {
        target.definition.maxHp = 500
        target.definition.armor = 0
        target.hp = 500
      }
      placeEnemy(access, pathTarget, 80)
      placeEnemy(access, endpointTarget, chargedRange)

      access.performAttack(resolution('left', 'hold', 1100))
      access.updatePlayer(1)

      expect(access.activeDash).toBeNull()
      expect(pathTarget.hp).toBe(500)
      expect(endpointTarget.hp).toBeLessThan(500)
      expect(access.activeAreas.at(-1)?.attack.name).toContain('финальная царапина')
    } finally {
      engine.destroy()
    }
  })

  it('preserves the Knife-spider locked leap facing so a miss exposes its rear', () => {
    const config = combatConfig('either-claws', null, 'spider-knife', 1)
    const { engine, access } = startCombat(config)
    const spider = access.enemies[0]

    try {
      spider.position = { x: 250, y: 70 }
      spider.facing = { x: 1, y: 0 }
      spider.state = 'attacking'
      spider.attackWindupMs = 0
      spider.lockedAttackDirection = { x: 1, y: 0 }
      spider.leapRemainingDistance = 100
      spider.leapSpeed = 200
      spider.leapHit = false
      access.player.position = { x: 280, y: 70 }

      access.updateEnemies(0.5, 500)

      expect(spider.captureWindowMs).toBeGreaterThan(0)
      expect(spider.facing).toEqual({ x: 1, y: 0 })
      expect(access.createSnapshot().interactionPrompt).toContain('Нож-паука')
    } finally {
      engine.destroy()
    }
  })

  it('swaps a captured Knife-spider into the right hand and leaves a two-handed weapon on the floor', () => {
    const config = combatConfig('twohand-spear', null, 'spider-knife', 1)
    const { engine, access } = startCombat(config)
    const spider = access.enemies[0]

    try {
      spider.captureWindowMs = 1_200
      spider.facing = { x: 1, y: 0 }
      spider.position = { x: 210, y: 340 }
      access.player.position = { x: 150, y: 340 }

      expect(engine.interact()).toBe(true)
      expect(spider.state).toBe('dead')
      expect(access.activeLoadout).toMatchObject({
        primaryWeaponId: null,
        secondaryWeaponId: 'secondary-spider-knife',
      })
      expect(access.groundWeapons).toHaveLength(1)
      expect(access.groundWeapons[0]).toMatchObject({
        weaponId: 'twohand-spear',
        position: spider.position,
      })
      expect(access.createSnapshot().interactionPrompt).toContain('Двуручное копьё')

      expect(engine.interact()).toBe(true)
      expect(access.activeLoadout).toMatchObject({
        primaryWeaponId: 'twohand-spear',
        secondaryWeaponId: null,
      })
      expect(access.groundWeapons.map(weapon => weapon.weaponId))
        .toEqual(['secondary-spider-knife'])
    } finally {
      engine.destroy()
    }
  })

  it('substeps a low-FPS rotating sweep so targets cannot sit in an angular gap', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]
    const angle = 22.5 * Math.PI / 180
    const distance = 100
    const attack: LastChancesAttackDefinition = {
      ...weapon(config, 'twohand-spear').attacks.tap,
      name: 'Low-FPS sweep fixture',
      damage: 10,
      range: 120,
      durationMs: 100,
      pierce: 3,
      collider: {
        shape: 'sweep',
        innerRange: 70,
        width: 2,
        traceMs: 400,
        rotationDegrees: 90,
      },
    }

    try {
      target.definition.maxHp = 500
      target.definition.armor = 0
      target.hp = 500
      target.position = {
        x: access.player.position.x + Math.cos(angle) * distance,
        y: access.player.position.y + Math.sin(angle) * distance,
      }

      access.startActiveArea('melee', attack, { x: 1, y: 0 }, 'twohand-spear', 'left')
      expect(target.hp).toBe(500)

      access.updateActiveAreas(50)

      expect(target.hp).toBeLessThan(500)
      expect(access.traces.length).toBeGreaterThan(1)
    } finally {
      engine.destroy()
    }
  })

  it('runs the katana flurry as repeated path hits and applies dodge to every second strike', () => {
    const config = combatConfig('twohand-katana', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]

    try {
      target.definition.maxHp = 500
      target.definition.armor = 0
      target.definition.dodge = 0.4
      target.hp = 500
      placeEnemy(access, target, 90)
      access.performAttack(resolution('left', 'doubleTapHold', 700))
      expect(access.activeDash?.attack.behavior).toBe('katanaFlurry')

      for (let step = 0; step < 9; step += 1) access.updatePlayer(0.13)
      expect(500 - target.hp).toBeGreaterThan(config.weapons.find(
        candidate => candidate.id === 'twohand-katana',
      )!.attacks.doubleTapHold.damage)
      expect(500 - target.hp).toBeLessThan(
        config.weapons.find(candidate => candidate.id === 'twohand-katana')!
          .attacks.doubleTapHold.damage * 7,
      )
    } finally {
      engine.destroy()
    }
  })

  it('uses the same swept projectile capsule for traces and fast collision', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]

    try {
      target.definition.maxHp = 500
      target.definition.armor = 0
      target.hp = 500
      placeEnemy(access, target, 160)
      access.performAttack(resolution('left', 'hold', 1750))
      expect(access.projectiles).toHaveLength(1)

      access.updateProjectiles(0.5, 500)
      expect(target.hp).toBeLessThan(500)
      expect(access.traces.length).toBeGreaterThan(0)
    } finally {
      engine.destroy()
    }
  })

  it('publishes the shared CRIT cue for authored critical attacks', () => {
    const config = combatConfig('twohand-spear', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]
    const attacks = [
      weapon(config, 'twohand-spear').attacks.tap,
      weapon(config, 'either-claws').attacks.holdThenDoubleTap,
      weapon(config, 'secondary-spider-knife').attacks.doubleTap,
      weapon(config, 'twohand-katana').attacks.doubleTap,
      weapon(config, 'hybrid-sword').attacks.doubleTap,
    ]

    try {
      target.definition.maxHp = 500
      target.definition.armor = 0
      placeEnemy(access, target, 150)
      for (const attack of attacks) {
        target.hp = 500
        target.criticalHitMs = 0
        target.statuses.openingMs = attack.behavior === 'swordOpening' ? 1000 : 0
        access.damageEnemy(target, attack, 0, { x: 1, y: 0 }, { distance: 150 })
        expect(target.criticalHitMs, attack.name).toBeGreaterThan(0)
      }
    } finally {
      engine.destroy()
    }
  })

  it('stores a non-bleed DOT on the chain, spreads it later, and restores a bound chain on death', () => {
    const config = combatConfig('either-claws', 'secondary-chain', 'guard', 2)
    const { engine, access } = startCombat(config)
    const chain = weapon(config, 'secondary-chain')
    const [source, target] = access.enemies

    try {
      applyLastChancesStatusEffects(source.statuses, [{
        status: 'poison',
        durationMs: 6000,
        stacks: 2,
        tickDamage: 3,
        tickMs: 500,
      }])
      access.damageEnemy(source, chain.attacks.tap, 0, { x: 1, y: 0 }, { hand: 'right' })
      expect(access.createSnapshot().weaponStates.find(state => state.hand === 'right'))
        .toMatchObject({ weaponId: 'secondary-chain', storedDot: 'poison', resource: 1 })

      access.elapsedMs += 60_000
      placeEnemy(access, source, 420, 140)
      placeEnemy(access, target, 50)
      access.performAttack(resolution('right', 'doubleTap'))
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'doubleTap',
      })
      expect(access.activeAreas.at(-1)?.storedDot).toMatchObject({
        kind: 'poison',
        stacks: 2,
      })
      access.updateActiveAreas(1)
      expect(target.statuses.dots.poison).toMatchObject({
        stacks: 2,
        tickDamage: 3,
        remainingMs: 6000,
      })
      expect(access.createSnapshot().weaponStates.find(state => state.hand === 'right')?.storedDot)
        .toBeNull()

      access.damageEnemy(
        target,
        chain.attacks.holdThenDoubleTap,
        0,
        { x: 1, y: 0 },
        { hand: 'right' },
      )
      expect(target.statuses).toMatchObject({
        slowMultiplier: 0.42,
        attackSlowMultiplier: 4,
      })
      expect(access.weaponStates.get('secondary-chain')).toMatchObject({
        resource: 0,
        boundEnemyId: target.id,
      })
      access.finishEnemyDeath(target)
      expect(access.weaponStates.get('secondary-chain')).toMatchObject({
        resource: 1,
        boundEnemyId: null,
      })
    } finally {
      engine.destroy()
    }
  })

  it('unlocks the chain second rotation only when movement assists its authored direction', () => {
    const config = combatConfig('either-claws', 'secondary-chain', 'guard', 1)
    const { engine, access } = startCombat(config)

    try {
      access.performAttack(resolution('right', 'doubleTap'))
      const spin = access.activeAreas.at(-1)!
      access.updateActiveAreas(100)
      const unassistedDegrees = spin.sweepDegrees
      expect(spin.rotationAssisted).toBe(false)
      expect(spin.attack.repeatHits).toBe(1)

      engine.setTouchMove(0, 1)
      access.updateActiveAreas(100)
      expect(spin.rotationAssisted).toBe(true)
      expect(spin.attack.repeatHits).toBe(spin.authoredRepeatHits)
      expect(spin.sweepDegrees - unassistedDegrees).toBeGreaterThan(unassistedDegrees)
    } finally {
      engine.destroy()
    }
  })

  it('shares claw parity across two hands and applies bleed plus microstagger on alternating hits', () => {
    const config = combatConfig('either-claws', 'either-claws', 'guard', 1)
    const { engine, access } = startCombat(config)
    const claws = weapon(config, 'either-claws')
    const target = access.enemies[0]

    try {
      access.elapsedMs = 100
      access.damageEnemy(target, claws.attacks.tap, 0, { x: 1, y: 0 }, { hand: 'left' })
      access.elapsedMs = 300
      access.damageEnemy(target, claws.attacks.tap, 0, { x: 1, y: 0 }, { hand: 'right' })

      expect(access.weaponStates.get('either-claws')).toMatchObject({
        successfulHits: 2,
        lastHitHand: 'right',
      })
      expect(target.statuses.dots.bleed.stacks).toBe(1)
      expect(target.statuses.stunMs).toBeGreaterThanOrEqual(90)
    } finally {
      engine.destroy()
    }
  })

  it('scopes the claw poison Symbol to the deep strike and strengthens only its bleeding target', () => {
    const config = combatConfig('either-claws', null, 'guard', 2)
    config.loadout!.primaryAugment = 'poison'
    const { engine, access } = startCombat(config)
    const [bleeding, clean] = access.enemies

    try {
      placeEnemy(access, bleeding, 60, -12)
      placeEnemy(access, clean, 60, 12)
      applyLastChancesStatusEffects(bleeding.statuses, [{
        status: 'bleed',
        durationMs: 5000,
        stacks: 1,
      }])
      access.performAttack(resolution('left', 'holdThenDoubleTap', 80, 900))

      expect(bleeding.statuses.dots.poison.tickDamage)
        .toBeGreaterThan(clean.statuses.dots.poison.tickDamage)
      expect(clean.statuses.dots.poison.tickDamage).toBe(2.8)
      expect(access.weapons.get('left')!.attacks.tap.hitEffects).toBeUndefined()
    } finally {
      engine.destroy()
    }
  })

  it('captures a missed knife-spider, spends durability, and destroys it on the throw', () => {
    const config = combatConfig('either-claws', null, 'spider-knife', 1)
    const { engine, access } = startCombat(config)
    const spider = access.enemies[0]

    try {
      spider.captureWindowMs = 1200
      spider.facing = { x: 1, y: 0 }
      spider.position = { x: 210, y: 340 }
      access.player.position = { x: 150, y: 340 }
      expect(access.createSnapshot().interactionPrompt).toContain('Нож-паука')
      expect(engine.interact()).toBe(true)
      expect(access.createSnapshot().loadout?.secondaryWeaponId).toBe('secondary-spider-knife')
      expect(access.createSnapshot().weaponStates.find(state => state.hand === 'right'))
        .toMatchObject({ resourceKind: 'durability', resource: 72, maxResource: 72 })

      access.performAttack(resolution('right', 'tap'))
      expect(access.createSnapshot().weaponStates.find(state => state.hand === 'right')?.resource)
        .toBe(70)

      access.performAttack(resolution('right', 'doubleTapHold', 900, 70))
      expect(access.createSnapshot().loadout?.secondaryWeaponId).toBeNull()
      expect(access.createSnapshot().weaponStates.some(state => (
        state.weaponId === 'secondary-spider-knife'
      ))).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('removes a held spider-flurry collider as soon as the living knife breaks', () => {
    const config = combatConfig('either-claws', 'secondary-spider-knife', 'guard', 1)
    const { engine, access } = startCombat(config)
    const spiderKnife = access.weapons.get('right')!
    const target = access.enemies[0]

    try {
      placeEnemy(access, target, 60)
      access.weaponStates.get(spiderKnife.id)!.resource = 2
      access.startActiveArea(
        'melee',
        spiderKnife.attacks.hold,
        { x: 1, y: 0 },
        spiderKnife.id,
        'right',
        null,
        true,
      )
      const channel = access.activeAreas.at(-1)!
      access.heldChannels.set('right', channel)
      expect(access.weaponStates.get(spiderKnife.id)?.resource).toBe(1)

      access.damageEnemy(
        target,
        spiderKnife.attacks.hold,
        0,
        { x: 1, y: 0 },
        { hand: 'right' },
      )
      expect(access.weapons.has('right')).toBe(false)
      expect(access.heldChannels.has('right')).toBe(false)
      expect(access.activeAreas.some(area => area.weaponId === spiderKnife.id)).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('lets an axe tap cancel special recovery and pulls every damaged target inward', () => {
    const config = combatConfig('twohand-axe', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const axe = weapon(config, 'twohand-axe')
    const target = access.enemies[0]

    try {
      access.performAttack(resolution('left', 'tap'))
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'tap',
        comboStep: 1,
      })
      access.elapsedMs += 100
      access.performAttack(resolution('left', 'doubleTap'))
      expect(access.weaponStates.get('twohand-axe')?.recoveryMs).toBe(0)
      expect(access.player.recoveryMs).toBe(0)

      access.elapsedMs += 1100
      access.updateDelayedRecoveries(1100)
      expect(access.weaponStates.get('twohand-axe')?.recoveryMs).toBe(620)
      expect(access.player.recoveryMs).toBe(620)
      access.performAttack(resolution('left', 'tap'))
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'tap',
        comboStep: 2,
      })
      expect(access.weaponStates.get('twohand-axe')?.recoveryMs).toBe(0)
      expect(access.player.recoveryMs).toBe(0)

      placeEnemy(access, target, 180)
      const beforeX = target.position.x
      access.damageEnemy(target, axe.attacks.tap, 0, { x: 1, y: 0 }, { hand: 'left' })
      expect(target.position.x).toBeLessThan(beforeX)
      expect(beforeX - target.position.x).toBeCloseTo(16)
    } finally {
      engine.destroy()
    }
  })

  it('holds the nearest axe target before applying the delayed armor tear', () => {
    const config = combatConfig('twohand-axe', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]

    try {
      placeEnemy(access, target, 70)
      access.performAttack(resolution('left', 'doubleTap'))
      expect(target.statuses.stunMs).toBeGreaterThan(1000)
      expect(target.statuses.armorBreak).toBe(0)

      access.updateActiveAreas(1099)
      expect(target.statuses.armorBreak).toBe(0)
      access.updateActiveAreas(1)
      expect(target.statuses.armorBreak).toBe(18)
      expect(target.statuses.armorBreakMs).toBe(5000)
    } finally {
      engine.destroy()
    }
  })

  it('refunds katana cooldowns on hit and clears the true-damage flash cooldown on a kill', () => {
    const config = combatConfig('twohand-katana', null, 'curator-shadow', 1)
    const { engine, access } = startCombat(config)
    const katana = weapon(config, 'twohand-katana')
    const target = access.enemies[0]

    try {
      access.cooldownEnds.set('left:hold', access.elapsedMs + 1000)
      access.damageEnemy(
        target,
        katana.attacks.doubleTap,
        0,
        { x: 1, y: 0 },
        { hand: 'left' },
      )
      expect(access.cooldownEnds.get('left:hold')).toBe(access.elapsedMs + 780)

      target.definition.armor = 999
      target.hp = 30
      access.cooldownEnds.set('right:doubleTap', access.elapsedMs + 4000)
      access.cooldownEnds.set('right:holdThenDoubleTap', access.elapsedMs + 6200)
      access.damageEnemy(
        target,
        katana.secondaryAttacks!.holdThenDoubleTap,
        0,
        { x: 1, y: 0 },
        { hand: 'right' },
      )
      expect(target.state).toBe('dead')
      expect(access.cooldownEnds.has('right:holdThenDoubleTap')).toBe(false)
      expect(access.cooldownEnds.get('right:doubleTap')).toBe(access.elapsedMs + 3740)
    } finally {
      engine.destroy()
    }
  })

  it('cashes out accumulated bleed with the charged katana only when its bleed Symbol is equipped', () => {
    const plainConfig = combatConfig('twohand-katana', null, 'guard', 1)
    const symbolConfig = combatConfig('twohand-katana', null, 'guard', 1)
    symbolConfig.loadout!.primaryAugment = 'bleed'
    const plain = startCombat(plainConfig)
    const symbol = startCombat(symbolConfig)

    try {
      for (const started of [plain, symbol]) {
        const target = started.access.enemies[0]
        target.definition.maxHp = 500
        target.definition.armor = 0
        target.hp = 500
        placeEnemy(started.access, target, 100)
        applyLastChancesStatusEffects(target.statuses, [{
          status: 'bleed',
          durationMs: 5000,
          stacks: 3,
          tickDamage: 2,
          tickMs: 500,
        }])
        const resolvedKatana = started.access.weapons.get('left')!
        started.access.damageEnemy(
          target,
          attackWithLastChancesAugment(resolvedKatana.attacks.hold, resolvedKatana),
          0,
          { x: 1, y: 0 },
          { hand: 'left' },
        )
      }

      expect(plain.access.enemies[0].statuses.dots.bleed.stacks).toBe(3)
      expect(symbol.access.enemies[0].statuses.dots.bleed.stacks).toBe(0)
      expect(symbol.access.enemies[0].hp).toBeLessThan(plain.access.enemies[0].hp)
    } finally {
      plain.engine.destroy()
      symbol.engine.destroy()
    }
  })

  it('classifies the 500–600 ms sword rhythm and executes an opened target with Oberhaw', () => {
    const config = combatConfig('hybrid-sword', null, 'curator-shadow', 1)
    const { engine, access } = startCombat(config)
    const sword = weapon(config, 'hybrid-sword')
    const target = access.enemies[0]

    try {
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')?.rhythm).toBe('idle')

      access.elapsedMs += 499
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')).toMatchObject({
        rhythm: 'early',
        recoveryMs: 2000,
        fatigueMs: 2000,
      })

      access.weaponStates.get('hybrid-sword')!.recoveryMs = 0
      access.weaponStates.get('hybrid-sword')!.fatigueMs = 0
      access.player.recoveryMs = 0
      access.elapsedMs += 500
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')).toMatchObject({
        rhythm: 'good',
        perfectTimingMs: 480,
      })

      access.elapsedMs += 700
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')?.rhythm).toBe('late')

      const cleanTap = { ...sword.attacks.tap, hitEffects: [] }
      target.definition.maxHp = 500
      target.definition.armor = 0
      target.hp = 500
      target.statuses.openingMs = 0
      access.weaponStates.get('hybrid-sword')!.successfulHits = 0
      for (let hit = 0; hit < 3; hit += 1) {
        access.damageEnemy(target, cleanTap, 0, { x: 1, y: 0 }, { hand: 'left' })
      }
      expect(target.statuses.openingMs).toBe(1600)

      target.hp = 300
      access.damageEnemy(
        target,
        sword.attacks.doubleTap,
        0,
        { x: 1, y: 0 },
        { hand: 'left' },
      )
      expect(300 - target.hp).toBeCloseTo(
        sword.attacks.doubleTap.damage * 3.25 * 1.5,
      )
      expect(target.statuses.openingMs).toBe(0)
      expect(target.swordExecutionMarked).toBe(true)
      access.cooldownEnds.set('left:doubleTap', access.elapsedMs + 2800)
      access.cooldownEnds.set('left:doubleTapHold', access.elapsedMs + 2800)
      access.finishEnemyDeath(target)
      expect(access.cooldownEnds.has('left:doubleTap')).toBe(false)
      expect(access.cooldownEnds.has('left:doubleTapHold')).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('keeps the held sword input as Oberhau followed by the additional Unterhau collider', () => {
    const config = combatConfig('hybrid-sword', null, 'guard', 1)
    const { engine, access } = startCombat(config)
    const target = access.enemies[0]

    try {
      target.definition.maxHp = 500
      target.definition.armor = 0
      target.hp = 500
      target.statuses.openingMs = 1600
      placeEnemy(access, target, 100)
      access.performAttack(resolution('left', 'doubleTapHold', 1000))

      expect(access.activeAreas.map(area => area.attack.behavior)).toContain('swordOpening')
      expect(access.activeAreas.map(area => area.attack.behavior)).not.toContain('swordFollowUp')
      expect(access.delayedAttacks).toHaveLength(1)
      expect(access.delayedAttacks[0].attack.behavior).toBe('swordFollowUp')
      expect(target.statuses.openingMs).toBe(0)
      const hpAfterOberhau = target.hp

      access.updateDelayedAttacks(access.delayedAttacks[0].remainingMs)

      expect(access.activeAreas.map(area => area.attack.behavior)).toContain('swordFollowUp')
      expect(target.hp).toBeLessThan(hpAfterOberhau)
      expect(500 - target.hp).toBeGreaterThan(
        weapon(config, 'hybrid-sword').attacks.doubleTapHold.damage,
      )
    } finally {
      engine.destroy()
    }
  })

  it('alternates Zornhaw sweep directions, applies matching mouse motion and morphs into Oberhaw', () => {
    const config = combatConfig('hybrid-sword', null, 'guard', 1)
    const { engine, access } = startCombat(config)

    try {
      placeEnemy(access, access.enemies[0], 500)
      access.performAttack(resolution('left', 'tap'))
      expect(access.activeAreas.at(-1)?.attack.collider?.rotationDegrees).toBe(118)

      access.pointerDeltaX = 160
      access.updateActiveAreas(10)
      expect(access.activeAreas.at(-1)?.swordMotionDamageBonus).toBeCloseTo(0.25)
      expect(access.weaponStates.get('hybrid-sword')?.lastMotionDamageBonus).toBeCloseTo(0.25)

      access.elapsedMs += 500
      access.performAttack(resolution('left', 'tap'))
      expect(access.activeAreas.at(-1)?.attack.collider?.rotationDegrees).toBe(-118)

      access.performAttack(resolution('left', 'doubleTap'))
      expect(access.activeAreas.some(area => area.attack.behavior === 'swordRhythm')).toBe(false)
      expect(access.activeAreas.some(area => area.attack.behavior === 'swordOpening')).toBe(true)
    } finally {
      engine.destroy()
    }
  })

  it('grants elites Unstoppable after six configured staggers and honors the stagger toggle', () => {
    const config = combatConfig('hybrid-sword', null, 'guard', 1)
    const enabled = startCombat(config)
    const disabledConfig = cloneLastChancesConfig(config)
    weapon(disabledConfig, 'hybrid-sword').staggerEnabled = false
    const disabled = startCombat(disabledConfig)

    try {
      const sword = weapon(config, 'hybrid-sword')
      const cleanTap = { ...sword.attacks.tap, hitEffects: [] }
      const elite = enabled.access.enemies[0]
      elite.definition.role = 'elite'
      elite.definition.maxHp = 1000
      elite.definition.armor = 0
      elite.hp = 1000
      for (let hit = 0; hit < 6; hit += 1) {
        enabled.access.damageEnemy(
          elite,
          cleanTap,
          0,
          { x: 1, y: 0 },
          { hand: 'left', gesture: 'tap' },
        )
      }
      expect(elite.statuses).toMatchObject({
        stunMs: 0,
        staggerAccumulatedMs: 0,
        unstoppableMs: 5000,
      })
      applyLastChancesStatusEffects(elite.statuses, [{ status: 'stun', durationMs: 1000 }])
      expect(elite.statuses.stunMs).toBe(0)

      const ordinary = disabled.access.enemies[0]
      ordinary.definition.armor = 0
      disabled.access.damageEnemy(
        ordinary,
        cleanTap,
        0,
        { x: 1, y: 0 },
        { hand: 'left', gesture: 'tap' },
      )
      expect(ordinary.statuses.stunMs).toBe(0)
    } finally {
      enabled.engine.destroy()
      disabled.engine.destroy()
    }
  })

  it('publishes color-coded charge cues and the contextual knife-spider prompt', () => {
    const spear = startCombat(combatConfig('twohand-spear', null, 'guard', 1))
    const spider = startCombat(combatConfig('either-claws', null, 'spider-knife', 1))

    try {
      const now = performance.now()
      spear.access.gestures.press('left', now - 1200)
      const chargeCue = spear.access.createSnapshot().actionCues.find(cue => cue.hand === 'left')
      expect(chargeCue).toMatchObject({
        weaponId: 'twohand-spear',
        phase: 'charging',
        gesture: 'hold',
      })
      expect(chargeCue!.color).not.toBe('#66706c')
      expect(chargeCue!.chargeProgress).toBeGreaterThan(0.5)
      expect(chargeCue!.chargeMaxMs).toBe(2200)
      expect(chargeCue!.chargeBands.find(band => band.active)?.id).toBe('middle')

      const knifeSpider = spider.access.enemies[0]
      knifeSpider.captureWindowMs = 1200
      knifeSpider.facing = { x: 1, y: 0 }
      knifeSpider.position = { x: 210, y: 340 }
      spider.access.player.position = { x: 150, y: 340 }
      expect(spider.access.createSnapshot()).toMatchObject({
        interactionPrompt: expect.stringContaining('Нож-паука'),
        enemies: [
          expect.objectContaining({
            definitionId: 'spider-knife',
            captureAvailable: true,
          }),
        ],
      })
    } finally {
      spear.engine.destroy()
      spider.engine.destroy()
    }
  })

  it('clears frozen combat transients between rooms and preserves augment selections on equipment changes', () => {
    const config = combatConfig('either-claws', null, 'guard', 1)
    config.loadout!.primaryAugment = 'chemical'
    config.loadout!.secondaryAugment = 'fire'
    const { engine, access } = startCombat(config)

    try {
      access.applyInteractionChoice({
        id: 'equip-chain',
        title: 'Equip chain',
        description: 'Regression fixture',
        effect: { secondaryWeaponId: 'secondary-chain' },
      })
      expect(access.createSnapshot().loadout).toMatchObject({
        primaryWeaponId: 'either-claws',
        secondaryWeaponId: 'secondary-chain',
        primaryAugment: 'chemical',
        secondaryAugment: 'none',
      })

      access.player.invulnerableMs = 900
      access.player.rootMs = 700
      access.player.recoveryMs = 600
      access.player.parryMs = 500
      access.player.armorMultiplier = 2
      access.player.armorMultiplierMs = 800
      access.weaponStates.get('either-claws')!.recoveryMs = 650
      access.effects = [{}]
      access.traces = [{}]
      access.enemies.forEach(enemy => { enemy.state = 'dead' })
      access.update(0, 0)

      expect(access.createSnapshot().phase).toBe('planning')
      expect(access.player).toMatchObject({
        invulnerableMs: 0,
        rootMs: 0,
        recoveryMs: 0,
        parryMs: 0,
        armorMultiplier: 1,
        armorMultiplierMs: 0,
      })
      expect(access.weaponStates.get('either-claws')?.recoveryMs).toBe(0)
      expect(access.effects).toEqual([])
      expect(access.traces).toEqual([])
    } finally {
      engine.destroy()
    }
  })
})

describe('99LC move-unlock quests and elite/swarm rooms', () => {
  it('locks compound gestures until two tap kills in one room unlock the double tap', () => {
    const config = combatConfig('twohand-spear', null, 'servant', 3)
    const { engine, access } = startCombat(config, { unlockMoves: false })

    try {
      const before = access.createSnapshot()
      const leftQuest = before.moveQuests.find(quest => quest.hand === 'left')
      expect(leftQuest?.unlocked).toMatchObject({
        tap: true,
        hold: true,
        doubleTap: false,
        doubleTapHold: false,
        holdThenDoubleTap: false,
      })
      expect(before.cooldowns.find(item => item.hand === 'left' && item.gesture === 'tap')?.ready).toBe(true)
      expect(before.cooldowns.find(item => item.hand === 'left' && item.gesture === 'doubleTap')?.ready).toBe(false)

      const weapon = access.weapons.get('left')!
      const [first, second, third] = access.enemies
      for (const enemy of [first, second]) {
        enemy.hp = 1
        access.damageEnemy(enemy, weapon.attacks.tap, 0, { x: 1, y: 0 }, {
          weaponId: weapon.id,
          hand: 'left',
          gesture: 'tap',
        })
        expect(enemy.state).toBe('dead')
      }
      expect(access.moveQuests.left.tapQuestDone).toBe(true)
      expect(access.moveQuests.left.pendingUnlocks).toContain('doubleTap')
      expect(access.moveQuests.left.unlocked.doubleTap).toBe(false)
      expect(access.moveQuests.right.tapQuestDone).toBe(false)

      third.hp = 1
      access.damageEnemy(third, weapon.attacks.tap, 0, { x: 1, y: 0 }, {
        weaponId: weapon.id,
        hand: 'left',
        gesture: 'tap',
      })
      access.update(0.016, 16)
      const planning = access.createSnapshot()
      expect(planning.phase).toBe('planning')

      expect(engine.chooseNode(planning.availableNodeIds[0])).toBe(true)
      expect(access.moveQuests.left.unlocked.doubleTap).toBe(true)
      expect(access.moveQuests.left.pendingUnlocks).toHaveLength(0)
      expect(access.moveQuests.left.roomKills.tap).toBe(0)
      const after = access.createSnapshot()
      expect(after.cooldowns.find(item => item.hand === 'left' && item.gesture === 'doubleTap')?.ready).toBe(true)
      expect(after.cooldowns.find(item => item.hand === 'left' && item.gesture === 'doubleTapHold')?.ready).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('keeps earned unlocks across a death but resets them on a new generation', () => {
    const config = combatConfig('twohand-spear', null, 'servant', 2)
    const { engine, access } = startCombat(config, { unlockMoves: false })

    try {
      access.moveQuests.left.roomKills.tap = 1
      access.moveQuests.left.tapQuestDone = true
      access.moveQuests.left.unlocked.doubleTap = true
      access.killPlayer('test death')
      expect(access.createSnapshot().phase).toBe('dead')
      expect(engine.retryAttempt()).toBe(true)
      expect(access.moveQuests.left.tapQuestDone).toBe(true)
      expect(access.moveQuests.left.unlocked.doubleTap).toBe(true)
      expect(access.moveQuests.left.roomKills.tap).toBe(0)

      engine.newGeneration()
      expect(access.moveQuests.left.tapQuestDone).toBe(false)
      expect(access.moveQuests.left.unlocked.doubleTap).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('completes the elite combo quest for the hand that landed every unlocked move', () => {
    const config = combatConfig('twohand-spear', null, 'chimera', 1)
    const { engine, access } = startCombat(config, { unlockMoves: false })

    try {
      const quest = access.moveQuests.left
      quest.tapQuestDone = true
      quest.holdQuestDone = true
      quest.unlocked.doubleTap = true
      quest.unlocked.holdThenDoubleTap = true

      const weapon = access.weapons.get('left')!
      const elite = access.enemies[0]
      elite.hp = 5000
      elite.definition.maxHp = 5000
      const required = access.createSnapshot().moveQuests
        .find(candidate => candidate.hand === 'left')!.comboGesturesRequired
      expect(required).toEqual(['tap', 'hold', 'doubleTap', 'holdThenDoubleTap'])

      for (const gesture of required) {
        access.damageEnemy(elite, weapon.attacks.tap, 0, { x: 1, y: 0 }, {
          weaponId: weapon.id,
          hand: 'left',
          gesture,
        })
      }
      expect(access.moveQuests.left.comboQuestDone).toBe(false)

      // The killing blow may come from the other hand; the checklist decides.
      elite.hp = 1
      access.damageEnemy(elite, weapon.attacks.tap, 0, { x: 1, y: 0 }, {
        weaponId: weapon.id,
        hand: 'right',
        gesture: 'tap',
      })
      expect(elite.state).toBe('dead')
      expect(access.moveQuests.left.comboQuestDone).toBe(true)
      expect(access.moveQuests.left.unlocked.doubleTapHold).toBe(true)
      expect(access.moveQuests.right.comboQuestDone).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('detonates the colossus zone as pure max-hp damage only inside the shape', () => {
    const config = combatConfig('twohand-spear', null, 'colossus', 1)
    // The colossus radius needs obstacle-free spawn clearance in this room.
    config.rooms.find(candidate => candidate.id === 'combat-hall')!.obstacles = []
    const { engine, access } = startCombat(config, { unlockMoves: false })

    try {
      const colossus = access.enemies[0]
      expect(colossus.definition.zone).toBeTruthy()

      colossus.state = 'attacking'
      colossus.attackWindupMs = 10
      const hpBefore = access.player.hp
      access.updateEnemies(0.016, 16)
      expect(access.zoneAttacks).toHaveLength(1)
      expect(access.player.hp).toBe(hpBefore)
      expect(colossus.state).toBe('chasing')

      const escaped = access.zoneAttacks[0]
      access.player.position = { x: escaped.center.x + escaped.size + 200, y: escaped.center.y }
      access.elapsedMs = escaped.detonateAtMs + 1
      access.updateZoneAttacks()
      expect(access.zoneAttacks).toHaveLength(0)
      expect(access.player.hp).toBe(hpBefore)

      access.player.stats.armor = 999
      access.player.invulnerableMs = 0
      colossus.state = 'attacking'
      colossus.attackWindupMs = 5
      access.updateEnemies(0.016, 16)
      const zone = access.zoneAttacks[0]
      access.player.position = { ...zone.center }
      access.elapsedMs = zone.detonateAtMs + 1
      access.updateZoneAttacks()
      expect(access.player.hp).toBe(hpBefore - access.player.stats.maxHp * 0.5)
    } finally {
      engine.destroy()
    }
  })

  it('feeds the creep swarm from two distinct edges and holds the room open until it drains', () => {
    const config = combatConfig('twohand-spear', null, 'swarm-creep', 1)
    const { engine, access } = startCombat(config, { unlockMoves: false })

    try {
      expect(access.swarmSpawner).toBeTruthy()
      expect(access.swarmSpawner!.edges[0]).not.toBe(access.swarmSpawner!.edges[1])
      expect(access.enemies).toHaveLength(0)
      expect(access.createSnapshot().phase).toBe('playing')
      expect(access.createSnapshot().swarm).toMatchObject({ definitionId: 'swarm-creep', total: 100 })

      access.updateSwarmSpawner()
      expect(access.enemies).toHaveLength(10)
      expect(access.swarmSpawner!.remaining).toBe(90)
      for (const creep of access.enemies) {
        expect(creep.entering).toBe(true)
        expect(creep.state).toBe('chasing')
      }

      access.roomElapsedMs = 200
      access.updateSwarmSpawner()
      expect(access.enemies).toHaveLength(11)

      for (const creep of access.enemies) creep.state = 'dead'
      access.update(0.001, 1)
      expect(access.createSnapshot().phase).toBe('playing')

      access.swarmSpawner!.remaining = 0
      access.update(0.001, 1)
      expect(access.createSnapshot().phase).toBe('planning')
    } finally {
      engine.destroy()
    }
  })

  it('plans one guaranteed colossus per tier 3-6 node and extracts swarm slots from rolls', () => {
    const plan = buildLastChancesPlan(cloneLastChancesConfig(defaultConfig))
    for (const node of plan.nodes) {
      const colossusCount = node.enemies.filter(enemy => enemy.definitionId === 'colossus').length
      if (node.tierKind === 'normal' && node.tierIndex >= 2 && node.tierIndex <= 5) {
        expect(colossusCount).toBe(1)
      } else {
        expect(colossusCount).toBe(0)
      }
      // Swarm rolls never place a walking enemy; they become the node's swarm event.
      expect(node.enemies.some(enemy => enemy.definitionId === 'swarm-creep')).toBe(false)
      if (node.swarm) {
        expect(node.swarm.definitionId).toBe('swarm-creep')
        expect(node.swarm.edges[0]).not.toBe(node.swarm.edges[1])
      }
    }
  })
})

describe('99LC control-scheme engine boundary', () => {
  it.each(['mylorik', 'dualsense'] as const)(
    'does not arm %s keyboard semantics while planning or paused',
    (scheme) => {
      const config = combatConfig('either-claws', 'secondary-chain')
      const engine = new LastChancesEngine(makeCanvas(), config, {}, { controlScheme: scheme })
      const access = engine as unknown as EngineTestAccess
      unlockAllMoves(access)
      const keyDown = (code: string) => window.dispatchEvent(new KeyboardEvent('keydown', { code }))
      const expectKeyboardClean = () => {
        expect(access.activeMobilityPhysicalHand).toBeNull()
        expect(access.keyboardDualSenseTriggers.left.down).toBe(false)
        expect(access.keyboardDualSenseTriggers.right.down).toBe(false)
        for (const hand of ['left', 'right'] as const) {
          expect(access.mylorikControls.snapshot(hand, performance.now())).toMatchObject({
            techniquePressed: false,
            mobilityPressed: false,
          })
          expect(access.dualSenseControls.snapshot(hand, performance.now()).active).toBe(false)
        }
      }

      try {
        keyDown('KeyQ')
        keyDown('KeyE')
        keyDown('Space')
        expectKeyboardClean()

        const opening = access.createSnapshot().availableNodeIds[0]
        expect(engine.chooseNode(opening)).toBe(true)
        engine.setPaused(true)
        keyDown('KeyQ')
        keyDown('KeyE')
        keyDown('Space')
        expectKeyboardClean()
      } finally {
        engine.destroy()
      }
    },
  )

  it('clears keyboard recognizers on pause, blur, and scheme replacement', () => {
    const { engine, access } = startCombat(combatConfig('either-claws', 'secondary-chain'))
    const key = (type: 'keydown' | 'keyup', code: string) => (
      window.dispatchEvent(new KeyboardEvent(type, { code }))
    )
    const expectAllReleased = () => {
      expect(access.activeMobilityPhysicalHand).toBeNull()
      expect(access.keyboardDualSenseTriggers.left.down).toBe(false)
      expect(access.keyboardDualSenseTriggers.right.down).toBe(false)
      for (const hand of ['left', 'right'] as const) {
        expect(access.mylorikControls.snapshot(hand, performance.now())).toMatchObject({
          techniquePressed: false,
          mobilityPressed: false,
        })
        expect(access.dualSenseControls.snapshot(hand, performance.now()).active).toBe(false)
      }
    }

    try {
      engine.setControlScheme('mylorik')
      key('keydown', 'KeyQ')
      key('keydown', 'Space')
      expect(access.mylorikControls.snapshot('left', performance.now()).techniquePressed).toBe(true)
      expect(access.activeMobilityPhysicalHand).not.toBeNull()
      expect(access.mylorikControls.snapshot(
        access.activeMobilityPhysicalHand!,
        performance.now(),
      ).mobilityPressed).toBe(true)
      engine.setPaused(true)
      expectAllReleased()

      engine.setPaused(false)
      key('keyup', 'KeyQ')
      key('keyup', 'Space')
      key('keydown', 'KeyE')
      expect(access.mylorikControls.snapshot('right', performance.now()).techniquePressed).toBe(true)
      window.dispatchEvent(new Event('blur'))
      expectAllReleased()

      key('keydown', 'KeyQ')
      engine.setControlScheme('dualsense')
      expectAllReleased()
      key('keydown', 'KeyQ')
      expect(access.dualSenseControls.snapshot('left', performance.now()).active).toBe(true)
      engine.setControlScheme('mylorik')
      expectAllReleased()
    } finally {
      engine.destroy()
    }
  })

  it('applies Builder control tuning live without changing the attempt or selected scheme', () => {
    const config = combatConfig('twohand-axe', null)
    const { engine, access } = startCombat(config)
    engine.setControlScheme('mylorik')

    try {
      access.player.hp -= 17
      access.cooldownEnds.set('right:tap', access.elapsedMs + 777)
      access.moveQuests.right.roomKills.tap = 1
      const before = access.createSnapshot()
      const gameplay = (snapshot: LastChancesSnapshot) => ({
        phase: snapshot.phase,
        generation: snapshot.generation,
        chances: snapshot.chances,
        currentNodeId: snapshot.currentNodeId,
        attemptPath: snapshot.attemptPath,
        player: snapshot.player,
        enemies: snapshot.enemies,
        loadout: snapshot.loadout,
        cooldowns: snapshot.cooldowns,
        weaponStates: snapshot.weaponStates,
        moveQuests: snapshot.moveQuests,
      })
      const edited = cloneLastChancesConfig(config)
      Object.assign(edited.input, {
        doubleTapMs: 271,
        tapComboWindowMs: 911,
        holdMs: 661,
        holdMaxMs: 2311,
        holdThenDoubleTapWindowMs: 491,
      })
      edited.input.mylorik!.techniqueHoldMs = 100
      edited.input.mylorik!.bufferMs = 161
      edited.input.mylorik!.continuationWindowMs = 501
      Object.assign(edited.input.dualsense!, {
        activationThreshold: 0.24,
        releaseThreshold: 0.13,
        hysteresis: 0.1,
      })
      const previousGates = { ...edited.input.dualsense!.gatePositions }
      const nextGates = { shallow: 0.25, medium: 0.5, deep: 0.75, final: 0.92 }
      edited.weapons.forEach((definition) => {
        for (const controls of [definition.controls?.primary, definition.controls?.secondary]) {
          controls?.dualsense.nodes.forEach((node) => {
            const gate = (Object.keys(previousGates) as Array<keyof typeof previousGates>)
              .find(candidate => previousGates[candidate] === node.activationThreshold)
            if (gate) node.activationThreshold = nextGates[gate]
          })
        }
      })
      edited.input.dualsense!.gatePositions = nextGates
      Object.assign(edited.input.dualsense!.feedback, {
        maxMagnitude: 0.74,
        maxDurationMs: 940,
        blockedRepeatMs: 250,
      })
      edited.input.dualsense!.feedback.profiles.click = {
        startPosition: 0.19,
        endPosition: 0.31,
        resistance: 0.25,
        force: 0.29,
        transitionMs: 36,
        effectMs: 91,
        magnitude: 0.23,
      }
      const adaptiveOverride = {
        startPosition: 0.17,
        endPosition: 0.32,
        resistance: 0.26,
        force: 0.3,
        transitionMs: 38,
        effectMs: 92,
        magnitude: 0.24,
      }
      weapon(edited, 'twohand-axe').controls!.primary.dualsense.nodes[0].adaptiveOverride = {
        ...adaptiveOverride,
      }

      expect(engine.applyControlDefinition(edited)).toBe(true)
      const after = access.createSnapshot()
      expect(after.controlScheme).toBe('mylorik')
      expect(gameplay(after)).toEqual(gameplay(before))

      const activeConfig = (access as unknown as { config: LastChancesConfig }).config
      expect(activeConfig.input).toMatchObject(edited.input)
      expect((access.gestures as unknown as { timings: object }).timings).toMatchObject({
        doubleTapMs: 271,
        holdMs: 661,
        holdMaxMs: 2311,
        holdThenDoubleTapWindowMs: 491,
      })
      expect((access.mylorikControls as unknown as { timings: object }).timings).toMatchObject({
        techniqueHoldMs: 100,
        bufferMs: 161,
        continuationWindowMs: 501,
      })
      expect((access.dualSenseControls as unknown as { config: object }).config).toMatchObject({
        activationThreshold: 0.24,
        releaseThreshold: 0.13,
        hysteresis: 0.1,
        gatePositions: nextGates,
      })
      expect((access.feedbackController as unknown as { config: object }).config).toMatchObject({
        maxMagnitude: 0.74,
        maxDurationMs: 940,
        blockedRepeatMs: 250,
        profiles: { click: edited.input.dualsense!.feedback.profiles.click },
      })
      expect(access.weapons.get('left')!.controls!.dualsense.nodes[0]).toMatchObject({
        activationThreshold: 0.25,
        adaptiveOverride,
      })

      engine.setControlScheme('dualsense')
      const feedback = vi.spyOn(access.feedbackController, 'emit')
      const editedNode = access.weapons.get('left')!.controls!.dualsense.nodes[0]
      ;(access as unknown as { handleSemanticInput: (event: object) => string })
        .handleSemanticInput({
          scheme: 'dualsense',
          physicalHand: 'right',
          hand: 'left',
          intent: 'technique',
          phase: 'hold',
          context: editedNode.entryContext,
          source: 'gamepad',
          atMs: 50,
          heldMs: 50,
          value: 0.25,
          gesture: editedNode.gesture,
          nodeId: editedNode.id,
          tactileProfile: editedNode.tactileProfile,
          commit: false,
          armed: true,
        })
      expect(feedback).toHaveBeenCalledWith(expect.objectContaining({ adaptiveOverride }))
      engine.setControlScheme('mylorik')

      access.mylorikControls.pressTechnique('left', 0, 'gamepad')
      access.mylorikControls.releaseTechnique('left', 100, 'gamepad')
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'hold',
        attackName: weapon(defaultConfig, 'twohand-axe').secondaryAttacks!.hold.name,
      })
    } finally {
      engine.destroy()
    }
  })

  it('cancels a held Knife-spider channel on scheme switch without settling the action', () => {
    const config = combatConfig('either-claws', 'secondary-spider-knife', 'guard', 1)
    const { engine, access } = startCombat(config)
    const spiderKnife = access.weapons.get('right')!

    try {
      access.startActiveArea(
        'melee',
        spiderKnife.attacks.hold,
        { x: 1, y: 0 },
        spiderKnife.id,
        'right',
        null,
        true,
      )
      const channel = access.activeAreas.at(-1)!
      access.heldChannels.set('right', channel)
      const weaponState = access.weaponStates.get(spiderKnife.id)!
      weaponState.resource = 41
      weaponState.recoveryMs = 23
      access.player.recoveryMs = 19
      access.cooldownEnds.set('right:hold', access.elapsedMs + 777)

      const before = access.createSnapshot()
      const cooldownEnds = [...access.cooldownEnds.entries()]
      const enemyHp = access.enemies.map(enemy => enemy.hp)
      expect(access.heldChannels.get('right')).toBe(channel)

      engine.setControlScheme('mylorik')

      const after = access.createSnapshot()
      expect(access.heldChannels.has('right')).toBe(false)
      expect(access.activeAreas).not.toContain(channel)
      expect(after.controlScheme).toBe('mylorik')
      expect(after.phase).toBe(before.phase)
      expect(after.currentNodeId).toBe(before.currentNodeId)
      expect(after.attemptPath).toEqual(before.attemptPath)
      expect(after.chances).toBe(before.chances)
      expect(after.player).toEqual(before.player)
      expect(after.enemies).toEqual(before.enemies)
      expect(access.enemies.map(enemy => enemy.hp)).toEqual(enemyHp)
      expect([...access.cooldownEnds.entries()]).toEqual(cooldownEnds)
      expect(after.weaponStates).toEqual(before.weaponStates)
      expect(after.moveQuests).toEqual(before.moveQuests)
      expect(weaponState.resource).toBe(41)
      expect(weaponState.recoveryMs).toBe(23)
      expect(access.player.recoveryMs).toBe(19)
      expect(access.cooldownEnds.get('right:hold')).toBe(access.elapsedMs + 777)
      expect(after.lastGesture).toEqual(before.lastGesture)
    } finally {
      engine.destroy()
    }
  })

  it('cancels a held Knife-spider channel on live Builder apply without settling the action', () => {
    const config = combatConfig('either-claws', 'secondary-spider-knife', 'guard', 1)
    const { engine, access } = startCombat(config)
    const spiderKnife = access.weapons.get('right')!
    engine.setControlScheme('dualsense')

    try {
      access.startActiveArea(
        'melee',
        spiderKnife.attacks.hold,
        { x: 1, y: 0 },
        spiderKnife.id,
        'right',
        null,
        true,
      )
      const channel = access.activeAreas.at(-1)!
      access.heldChannels.set('right', channel)
      const weaponState = access.weaponStates.get(spiderKnife.id)!
      weaponState.resource = 37
      weaponState.recoveryMs = 29
      access.player.recoveryMs = 17
      access.cooldownEnds.set('right:hold', access.elapsedMs + 901)
      const before = access.createSnapshot()
      const cooldownEnds = [...access.cooldownEnds.entries()]
      const enemyHp = access.enemies.map(enemy => enemy.hp)
      const edited = cloneLastChancesConfig(config)
      edited.input.mylorik!.techniqueHoldMs += 1

      expect(engine.applyControlDefinition(edited)).toBe(true)

      const after = access.createSnapshot()
      expect(access.heldChannels.has('right')).toBe(false)
      expect(access.activeAreas).not.toContain(channel)
      expect(after.controlScheme).toBe('dualsense')
      expect(after.phase).toBe(before.phase)
      expect(after.currentNodeId).toBe(before.currentNodeId)
      expect(after.attemptPath).toEqual(before.attemptPath)
      expect(after.chances).toBe(before.chances)
      expect(after.player).toEqual(before.player)
      expect(after.enemies).toEqual(before.enemies)
      expect(access.enemies.map(enemy => enemy.hp)).toEqual(enemyHp)
      expect([...access.cooldownEnds.entries()]).toEqual(cooldownEnds)
      expect(after.weaponStates).toEqual(before.weaponStates)
      expect(after.moveQuests).toEqual(before.moveQuests)
      expect(weaponState.resource).toBe(37)
      expect(weaponState.recoveryMs).toBe(29)
      expect(access.player.recoveryMs).toBe(17)
      expect(access.cooldownEnds.get('right:hold')).toBe(access.elapsedMs + 901)
      expect(after.lastGesture).toEqual(before.lastGesture)
    } finally {
      engine.destroy()
    }
  })

  it('hot-switches adapters while preserving the complete live attempt', () => {
    const config = combatConfig('hybrid-sword', 'secondary-chain')
    const engine = new LastChancesEngine(makeCanvas(), config, {}, { controlScheme: 'legacy' })
    const access = engine as unknown as EngineTestAccess
    unlockAllMoves(access)
    const opening = access.createSnapshot().availableNodeIds[0]
    expect(engine.chooseNode(opening)).toBe(true)

    try {
      access.player.hp -= 17
      access.enemies[0].hp -= 9
      access.cooldownEnds.set('left:doubleTap', access.elapsedMs + 777)
      access.weaponStates.get('secondary-chain')!.resource = 0.4
      access.moveQuests.left.roomKills.tap = 1
      const before = access.createSnapshot()
      const gameplay = (snapshot: LastChancesSnapshot) => ({
        phase: snapshot.phase,
        generation: snapshot.generation,
        chances: snapshot.chances,
        currentNodeId: snapshot.currentNodeId,
        attemptPath: snapshot.attemptPath,
        player: snapshot.player,
        enemies: snapshot.enemies,
        loadout: snapshot.loadout,
        cooldowns: snapshot.cooldowns,
        weaponStates: snapshot.weaponStates,
        moveQuests: snapshot.moveQuests,
      })

      engine.press('left')
      engine.setControlScheme('mylorik')
      const mylorik = access.createSnapshot()
      expect(mylorik.controlScheme).toBe('mylorik')
      expect(gameplay(mylorik)).toEqual(gameplay(before))
      expect(mylorik.gestureInputs.every(input => !input.pressed)).toBe(true)

      engine.setControlScheme('dualsense')
      const dualsense = access.createSnapshot()
      expect(dualsense.controlScheme).toBe('dualsense')
      expect(gameplay(dualsense)).toEqual(gameplay(before))
    } finally {
      engine.destroy()
    }
  })

  it('routes mylorik technique holds and armed bumper continuations on trigger release', () => {
    const held = startCombat(combatConfig('twohand-spear', null))
    held.engine.setControlScheme('mylorik')
    try {
      held.access.mylorikControls.pressTechnique('right', 0, 'gamepad')
      held.access.mylorikControls.releaseTechnique('right', 700, 'gamepad')
      expect(held.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'hold',
        attackName: weapon(defaultConfig, 'twohand-spear').attacks.hold.name,
      })
    } finally {
      held.engine.destroy()
    }

    const continuation = startCombat(combatConfig('twohand-spear', null))
    continuation.engine.setControlScheme('mylorik')
    try {
      continuation.access.mylorikControls.pressTechnique('right', 0, 'gamepad')
      continuation.access.mylorikControls.pressStrike('right', 700, 'gamepad')
      expect(continuation.access.createSnapshot().lastGesture).toBeNull()
      continuation.access.mylorikControls.releaseTechnique('right', 1_200, 'gamepad')
      expect(continuation.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTapHold',
        attackName: weapon(defaultConfig, 'twohand-spear').attacks.doubleTapHold.name,
      })
    } finally {
      continuation.engine.destroy()
    }

    const mobility = startCombat(combatConfig('either-claws', null))
    mobility.engine.setControlScheme('mylorik')
    try {
      mobility.access.mylorikControls.pressMobility('right', 0, 'keyboard')
      mobility.access.mylorikControls.releaseMobility('right', 700, 'keyboard')
      expect(mobility.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'hold',
        attackName: weapon(defaultConfig, 'either-claws').attacks.hold.name,
      })
    } finally {
      mobility.engine.destroy()
    }
  })

  it('buffers mylorik only inside the configured 150 ms action window', () => {
    const within = startCombat(combatConfig('either-claws', 'secondary-chain'))
    within.engine.setControlScheme('mylorik')
    try {
      const bufferMs = within.engine.config.input.mylorik!.bufferMs
      expect(bufferMs).toBe(150)
      within.access.elapsedMs = 100
      within.access.cooldownEnds.set('right:doubleTap', within.access.elapsedMs + bufferMs)
      within.access.mylorikControls.pressTechnique('left', 0, 'gamepad')
      within.access.mylorikControls.releaseTechnique('left', 100, 'gamepad')
      expect(within.access.mylorikControls.snapshot('left', 100).buffered).toBe(true)
      expect(within.access.createSnapshot().lastGesture).toBeNull()

      within.access.elapsedMs = 250
      within.access.mylorikControls.update(250)
      expect(within.access.mylorikControls.snapshot('left', 250).buffered).toBe(false)
      expect(within.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'doubleTap',
      })
    } finally {
      within.engine.destroy()
    }

    const outside = startCombat(combatConfig('either-claws', 'secondary-chain'))
    outside.engine.setControlScheme('mylorik')
    try {
      const bufferMs = outside.engine.config.input.mylorik!.bufferMs
      outside.access.elapsedMs = 100
      outside.access.cooldownEnds.set('right:doubleTap', outside.access.elapsedMs + bufferMs + 1)
      outside.access.mylorikControls.pressTechnique('left', 0, 'gamepad')
      outside.access.mylorikControls.releaseTechnique('left', 100, 'gamepad')
      expect(outside.access.mylorikControls.snapshot('left', 100).buffered).toBe(false)
      expect(outside.access.createSnapshot()).toMatchObject({
        lastGesture: null,
        controlCue: { state: 'blocked', gesture: 'doubleTap' },
      })

      outside.access.elapsedMs = 251
      outside.access.mylorikControls.update(251)
      expect(outside.access.createSnapshot().lastGesture).toBeNull()
    } finally {
      outside.engine.destroy()
    }
  })

  it.each(['keyboard', 'gamepad'] as const)(
    'selects the armed physical technique hand for %s mylorik mobility',
    (source) => {
      const { engine, access } = startCombat(combatConfig('twohand-spear', null))
      engine.setControlScheme('mylorik')
      try {
        access.mylorikControls.pressTechnique('right', 0, source)
        expect(access.selectMobilityPhysicalHand(1_200)).toBe('right')
      } finally {
        engine.destroy()
      }
    },
  )

  it('keeps simultaneous bumpers as two strikes and reseeds held edges after a hot switch', () => {
    const { engine, access } = startCombat(combatConfig('either-claws', 'secondary-chain'))
    engine.setControlScheme('mylorik')
    try {
      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(gamepadReading([4, 5]))
      expect(access.tapCombos).toMatchObject({ left: { step: 1 }, right: { step: 1 } })

      engine.setControlScheme('dualsense')
      access.applyGamepadReading(gamepadReading([4, 5]))
      expect(access.tapCombos).toMatchObject({ left: { step: 1 }, right: { step: 1 } })

      access.elapsedMs += 500
      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(gamepadReading([4, 5]))
      expect(access.tapCombos).toMatchObject({ left: { step: 2 }, right: { step: 2 } })
      expect(access.createSnapshot().phase).toBe('playing')
    } finally {
      engine.destroy()
    }
  })

  it('retains the last non-zero right-stick aim until a newer aim source or disconnect', () => {
    const { engine, access } = startCombat(combatConfig('either-claws', null))
    try {
      engine.pointerMove(0, 0)
      const stalePointerAim = { ...access.pointerAim }
      access.applyGamepadReading({
        ...gamepadReading(),
        status: 'active',
        aim: { x: 0, y: 1 },
      })
      access.updatePlayer(0)
      expect(access.player.aim).toEqual({ x: 0, y: 1 })

      access.applyGamepadReading(gamepadReading())
      access.updatePlayer(0)
      expect(access.player.aim).toEqual({ x: 0, y: 1 })
      expect(access.player.aim).not.toEqual(stalePointerAim)

      engine.pointerMove(960, 320)
      access.updatePlayer(0)
      expect(access.player.aim).toEqual(access.pointerAim)

      access.applyGamepadReading({
        ...gamepadReading(),
        status: 'active',
        aim: { x: -1, y: 0 },
      })
      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(null)
      access.updatePlayer(0)
      expect(access.player.aim).toEqual(access.pointerAim)
    } finally {
      engine.destroy()
    }
  })

  it('requires DualSense focus before Cross confirms and never turns held Cross or Circle into combat', () => {
    const config = combatConfig('either-claws', null, 'spider-knife', 1)
    const engine = new LastChancesEngine(makeCanvas(), config, {}, { controlScheme: 'dualsense' })
    const access = engine as unknown as EngineTestAccess
    unlockAllMoves(access)

    try {
      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(gamepadReading([0]))
      expect(access.createSnapshot().phase).toBe('planning')
      expect(access.createSnapshot().selectedNodeId).toBeNull()

      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(gamepadReading([15]))
      expect(access.createSnapshot().selectedNodeId).not.toBeNull()
      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(gamepadReading([0]))
      expect(access.createSnapshot().phase).toBe('playing')

      const spider = access.enemies[0]
      spider.captureWindowMs = 1_200
      spider.facing = { x: 1, y: 0 }
      spider.position = { x: 210, y: 340 }
      access.player.position = { x: 150, y: 340 }
      expect(access.createSnapshot().interactionPrompt).toContain('Нож-паука')

      // Cross is still held from route entry, so the combat phase cannot synthesize capture.
      access.applyGamepadReading(gamepadReading([0]))
      expect(access.createSnapshot().loadout?.secondaryWeaponId).toBeNull()

      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(gamepadReading([1]))
      expect(access.createSnapshot().lastGesture).toBeNull()
      expect(access.createSnapshot().loadout?.secondaryWeaponId).toBeNull()

      access.applyGamepadReading(gamepadReading())
      access.applyGamepadReading(gamepadReading([0]))
      expect(access.createSnapshot().loadout?.secondaryWeaponId).toBe('secondary-spider-knife')
      expect(access.createSnapshot().lastGesture).toMatchObject({
        attackName: 'Нож-паук схвачен со спины',
      })
    } finally {
      engine.destroy()
    }
  })

  it('executes Oberhaw without requiring an opening status', () => {
    const snapshots: LastChancesSnapshot[] = []
    const config = combatConfig('hybrid-sword', null, 'guard', 1)
    const engine = new LastChancesEngine(makeCanvas(), config, {
      onSnapshot: snapshot => snapshots.push(snapshot),
    }, { controlScheme: 'dualsense' })
    const access = engine as unknown as EngineTestAccess
    unlockAllMoves(access)
    const openingNode = access.createSnapshot().availableNodeIds[0]
    expect(engine.chooseNode(openingNode)).toBe(true)

    try {
      driveDualSenseTrigger(access, 'right', 0.22, 0)
      driveDualSenseTrigger(access, 'right', 0, 100)

      expect(snapshots.filter(snapshot => snapshot.controlCue?.state === 'blocked')).toHaveLength(0)
      expect(access.createSnapshot()).toMatchObject({
        lastGesture: {
          hand: 'left',
          gesture: 'doubleTap',
          attackName: weapon(defaultConfig, 'hybrid-sword').attacks.doubleTap.name,
        },
      })
      expect(access.dualSenseControls.snapshot('right', 100)).toMatchObject({
        active: false,
        nodeId: null,
      })
      expect(access.activeAreas.map(area => area.attack.behavior)).toContain('swordOpening')
    } finally {
      engine.destroy()
    }
  })

  it('does not arm or advertise a DualSense node whose action is cooling down', () => {
    const snapshots: LastChancesSnapshot[] = []
    const config = combatConfig('either-claws', null, 'guard', 1)
    const engine = new LastChancesEngine(makeCanvas(), config, {
      onSnapshot: snapshot => snapshots.push(snapshot),
    }, { controlScheme: 'dualsense' })
    const access = engine as unknown as EngineTestAccess
    unlockAllMoves(access)
    const openingNode = access.createSnapshot().availableNodeIds[0]
    expect(engine.chooseNode(openingNode)).toBe(true)

    try {
      access.cooldownEnds.set('left:doubleTap', 900)
      driveDualSenseTrigger(access, 'right', 0.22, 0)
      driveDualSenseTrigger(access, 'right', 0.48, 100)

      expect(snapshots.filter(snapshot => snapshot.controlCue?.state === 'blocked')).toHaveLength(1)
      expect(access.createSnapshot()).toMatchObject({
        lastGesture: null,
        controlCue: { state: 'blocked', hand: 'left', gesture: 'doubleTap', atMs: 0 },
      })
      expect(access.dualSenseControls.snapshot('right', 100)).toMatchObject({
        active: false,
        nodeId: null,
      })
      expect(access.cooldownEnds.get('left:doubleTap')).toBe(900)
    } finally {
      engine.destroy()
    }
  })

  it('isolates Sword opening context from a supplemental Knife-spider hand', () => {
    const { engine, access } = startCombat(
      combatConfig('hybrid-sword', 'secondary-spider-knife', 'guard', 1),
    )
    engine.setControlScheme('dualsense')
    const target = access.enemies[0]
    const spiderState = access.weaponStates.get('secondary-spider-knife')!

    try {
      target.statuses.openingMs = 2_000
      target.lastPlayerHit = { hand: 'left', gesture: 'tap' }
      placeEnemy(access, target, 500)

      expect(access.controlContextActive('left', 'opening')).toBe(false)
      expect(access.controlContextActive('right', 'opening')).toBe(false)
      expect(access.controlContextActive('right', 'neutral')).toBe(true)
      expect(access.controlContextActive('right', 'continuation')).toBe(false)

      driveDualSenseTrigger(access, 'left', 0.22, 0)
      driveDualSenseTrigger(access, 'left', 0, 100)

      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'doubleTap',
        attackName: weapon(defaultConfig, 'secondary-spider-knife').attacks.doubleTap.name,
      })
      expect(spiderState.resource).toBe(70)
      expect(spiderState.resource).toBeGreaterThan(0)
    } finally {
      engine.destroy()
    }
  })

  it('keeps a Sword opening action from spending the supplemental Knife-spider', () => {
    const { engine, access } = startCombat(
      combatConfig('hybrid-sword', 'secondary-spider-knife', 'guard', 1),
    )
    engine.setControlScheme('dualsense')
    const target = access.enemies[0]
    const spiderState = access.weaponStates.get('secondary-spider-knife')!

    try {
      target.statuses.openingMs = 2_000
      target.lastPlayerHit = { hand: 'left', gesture: 'tap' }
      placeEnemy(access, target, 500)
      const resourceBefore = spiderState.resource

      driveDualSenseTrigger(access, 'right', 0.22, 0)
      driveDualSenseTrigger(access, 'right', 0, 100)

      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTap',
        attackName: weapon(defaultConfig, 'hybrid-sword').attacks.doubleTap.name,
      })
      expect(access.createSnapshot().lastGesture?.attackName).not.toBe(
        weapon(defaultConfig, 'secondary-spider-knife').attacks.doubleTapHold.name,
      )
      expect(spiderState.resource).toBe(resourceBefore)
      expect(access.cooldownEnds.has('right:doubleTapHold')).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it.each([
    ['Spear', 'twohand-spear', null, 'right', 'left', 'doubleTap'],
    ['Chain', 'either-claws', 'secondary-chain', 'left', 'right', 'hold'],
    ['Claws', 'either-claws', null, 'right', 'left', 'doubleTap'],
    ['Knife-spider', 'either-claws', 'secondary-spider-knife', 'left', 'right', 'doubleTap'],
    ['Axe', 'twohand-axe', null, 'right', 'left', 'doubleTap'],
    ['Katana', 'twohand-katana', null, 'right', 'left', 'doubleTap'],
    ['Sword', 'hybrid-sword', null, 'right', 'left', 'doubleTap'],
  ] as const)(
    'executes the exact authored %s shallow trigger slot at controls-only Tier 0',
    (_label, primaryId, secondaryId, physicalHand, runtimeHand, expectedGesture) => {
      const { engine, access } = startCombat(combatConfig(primaryId, secondaryId))
      engine.setControlScheme('dualsense')
      try {
        if (primaryId === 'hybrid-sword') access.enemies[0].statuses.openingMs = 2_000
        const equipped = access.weapons.get(runtimeHand)!
        const controls = equipped.controls!
        const startNode = controls.dualsense.nodes.find(node => (
          node.id === controls.dualsense.startNodeId
        ))!
        access.dualSenseControls.updateTrigger(
          physicalHand,
          startNode.activationThreshold,
          0,
          controls,
          'gamepad',
        )
        access.dualSenseControls.updateTrigger(
          physicalHand,
          0,
          1_800,
          controls,
          'gamepad',
        )

        expect(access.createSnapshot().lastGesture).toMatchObject({
          hand: runtimeHand,
          gesture: expectedGesture,
          attackName: equipped.attacks[expectedGesture].name,
        })
        expect(access.createSnapshot().feedback.tier).toBe(0)
      } finally {
        engine.destroy()
      }
    },
  )

  it('keeps resolved gameplay identical between controls-only and fake Tier 2 feedback', async () => {
    const config = combatConfig('twohand-spear', null)
    const tier0 = startCombat(config)
    const tier2 = startCombat(config)
    const playEnhanced = vi.fn(async (_effect: LastChancesFeedbackEffect) => true)
    const enhancedOutput: LastChancesEnhancedFeedbackOutput = {
      capability: () => ({
        tier: 2,
        status: 'enhanced',
        permission: 'granted',
        message: null,
      }),
      play: playEnhanced,
      neutralize: vi.fn(async () => undefined),
      enableEnhancedFeatures: vi.fn(async () => true),
      disableEnhancedFeatures: vi.fn(async () => undefined),
    }
    const fakeTier2 = new DualSenseFeedbackController(
      config.input.dualsense!.feedback,
      { mode: 'full', intensity: 1 },
      { enhanced: enhancedOutput },
    )

    try {
      tier0.engine.setControlScheme('dualsense')
      tier2.engine.setControlScheme('dualsense')
      await tier2.access.feedbackController.dispose()
      tier2.access.feedbackController = fakeTier2

      driveDualSenseTrigger(tier0.access, 'right', 0.22, 0)
      driveDualSenseTrigger(tier0.access, 'right', 0, 1_800)
      driveDualSenseTrigger(tier2.access, 'right', 0.22, 0)
      driveDualSenseTrigger(tier2.access, 'right', 0, 1_800)
      await fakeTier2.flush()

      const expectedAttack = weapon(defaultConfig, 'twohand-spear').attacks.doubleTap.name
      expect(tier0.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTap',
        attackName: expectedAttack,
      })
      expect(tier2.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTap',
        attackName: expectedAttack,
      })
      expect(tier0.access.activeAreas.map(area => area.attack.name)).toEqual([expectedAttack])
      expect(tier2.access.activeAreas.map(area => area.attack.name)).toEqual([expectedAttack])
      expect(playEnhanced).toHaveBeenCalled()
      expect(tier0.access.createSnapshot().feedback.tier).toBe(0)
      expect(tier2.access.createSnapshot().feedback.tier).toBe(2)

      const gameplaySnapshot = (snapshot: LastChancesSnapshot): Partial<LastChancesSnapshot> => {
        const gameplay: Partial<LastChancesSnapshot> = { ...snapshot }
        delete gameplay.feedback
        return gameplay
      }
      expect(gameplaySnapshot(tier2.access.createSnapshot()))
        .toEqual(gameplaySnapshot(tier0.access.createSnapshot()))
      expect(tier2.access.activeAreas).toEqual(tier0.access.activeAreas)
      expect(tier2.access.projectiles).toEqual(tier0.access.projectiles)
      expect(tier2.access.delayedAttacks).toEqual(tier0.access.delayedAttacks)
      expect([...tier2.access.weaponStates]).toEqual([...tier0.access.weaponStates])
    } finally {
      tier0.engine.destroy()
      tier2.engine.destroy()
      await tier0.access.feedbackController.dispose()
      await fakeTier2.dispose()
    }
  })

  it('selects the legal Claws branch at full travel and reserves deep critical for an active dash', () => {
    const neutral = startCombat(combatConfig('either-claws', null))
    neutral.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(neutral.access, 'right', 0.9, 0)
      driveDualSenseTrigger(neutral.access, 'right', 0, 700)
      expect(neutral.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTapHold',
        attackName: weapon(defaultConfig, 'either-claws').attacks.doubleTapHold.name,
      })
    } finally {
      neutral.engine.destroy()
    }

    const dashing = startCombat(combatConfig('either-claws', null))
    dashing.engine.setControlScheme('dualsense')
    try {
      dashing.access.performAttack(resolution('left', 'hold', 700))
      expect(dashing.access.activeDash?.attack.behavior).toBe('clawDash')
      driveDualSenseTrigger(dashing.access, 'right', 0.9, 800)
      expect(dashing.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'either-claws').attacks.holdThenDoubleTap.name,
      })
      expect(dashing.access.activeDash).toBeNull()
    } finally {
      dashing.engine.destroy()
    }
  })

  it('arms Spear spin only from the authored middle charge band', () => {
    const { engine, access } = startCombat(combatConfig('twohand-spear', null))
    engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(access, 'right', 0.9, 0)
      expect(access.createSnapshot().lastGesture).toBeNull()
      driveDualSenseTrigger(access, 'right', 0, 100)
      expect(access.createSnapshot().lastGesture).toBeNull()
      expect(access.createSnapshot().controlCue).toMatchObject({ state: 'blocked' })

      driveDualSenseTrigger(access, 'right', 0.48, 1_000)
      driveDualSenseTrigger(access, 'right', 0.9, 2_125)
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'twohand-spear').attacks.holdThenDoubleTap.name,
      })
    } finally {
      engine.destroy()
    }
  })

  it('advances the DualSense Q/E fallback through every authored digital gate', () => {
    const { engine, access } = startCombat(combatConfig('twohand-spear', null))
    engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(access, 'right', 0.22, 0)
      access.keyboardDualSenseTriggers.right = { down: true, startedAt: 0, gateIndex: 0 }

      access.updateKeyboardDualSenseTriggers(650)
      expect(access.dualSenseControls.snapshot('right', 650).nodeId).toBe('hold')
      access.updateKeyboardDualSenseTriggers(1_130)
      expect(access.dualSenseControls.snapshot('right', 1_130).nodeId).toBe('doubleTapHold')
      access.updateKeyboardDualSenseTriggers(1_610)
      expect(access.dualSenseControls.snapshot('right', 1_610).nodeId).toBe('holdThenDoubleTap')
      expect(access.createSnapshot().lastGesture?.gesture).toBe('holdThenDoubleTap')
    } finally {
      engine.destroy()
    }
  })

  it('keeps Spear stance alive through the L2 vault gate and commits on release', () => {
    const { engine, access } = startCombat(combatConfig('twohand-spear', null))
    engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(access, 'left', 0.48, 0)
      access.updateHeldWeaponMechanics(16)
      expect(access.heldChannels.get('right')?.attack.behavior).toBe('spearStance')

      driveDualSenseTrigger(access, 'left', 0.9, 700)
      driveDualSenseTrigger(access, 'left', 0, 750)
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'twohand-spear').secondaryAttacks!.holdThenDoubleTap.name,
      })
      expect(access.activeDash?.attack.behavior).toBe('poleVault')
      expect(access.heldChannels.has('right')).toBe(false)
    } finally {
      engine.destroy()
    }
  })

  it('commits the mylorik Knife-spider mobility throw on trigger release', () => {
    const { engine, access } = startCombat(combatConfig('either-claws', 'secondary-spider-knife'))
    engine.setControlScheme('mylorik')
    try {
      access.mylorikControls.pressTechnique('left', 0, 'gamepad')
      access.mylorikControls.pressMobility('left', 700, 'gamepad')
      expect(access.createSnapshot().lastGesture).toBeNull()

      access.mylorikControls.releaseTechnique('left', 700, 'gamepad')
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'doubleTapHold',
        attackName: weapon(defaultConfig, 'secondary-spider-knife').attacks.doubleTapHold.name,
      })
      expect(access.weaponStates.get('secondary-spider-knife')?.resource).toBe(0)
    } finally {
      engine.destroy()
    }
  })

  it('locks a gradual DualSense Knife-spider ratchet to twist and reserves throw for a direct pull', () => {
    const twisting = startCombat(combatConfig('either-claws', 'secondary-spider-knife'))
    twisting.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(twisting.access, 'left', 0.22, 0)
      driveDualSenseTrigger(twisting.access, 'left', 0.48, 100)
      twisting.access.updateHeldWeaponMechanics(16)
      expect(twisting.access.heldChannels.get('right')?.attack.behavior).toBe('spiderFlurry')
      driveDualSenseTrigger(twisting.access, 'left', 0.72, 700)
      driveDualSenseTrigger(twisting.access, 'left', 0, 750)
      expect(twisting.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'secondary-spider-knife').attacks.holdThenDoubleTap.name,
      })
      expect(twisting.access.heldChannels.has('right')).toBe(false)
    } finally {
      twisting.engine.destroy()
    }

    const gradual = startCombat(combatConfig('either-claws', 'secondary-spider-knife'))
    gradual.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(gradual.access, 'left', 0.22, 0)
      driveDualSenseTrigger(gradual.access, 'left', 0.48, 100)
      gradual.access.updateHeldWeaponMechanics(16)
      expect(gradual.access.heldChannels.get('right')?.attack.behavior).toBe('spiderFlurry')
      driveDualSenseTrigger(gradual.access, 'left', 0.72, 700)
      driveDualSenseTrigger(gradual.access, 'left', 0.9, 1_250)
      driveDualSenseTrigger(gradual.access, 'left', 0, 1_300)
      expect(gradual.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'secondary-spider-knife').attacks.holdThenDoubleTap.name,
      })
      expect(gradual.access.weaponStates.get('secondary-spider-knife')?.resource).toBeGreaterThan(0)
      expect(gradual.access.heldChannels.has('right')).toBe(false)
    } finally {
      gradual.engine.destroy()
    }

    const direct = startCombat(combatConfig('either-claws', 'secondary-spider-knife'))
    direct.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(direct.access, 'left', 0.9, 0)
      expect(direct.access.heldChannels.has('right')).toBe(false)
      driveDualSenseTrigger(direct.access, 'left', 0, 700)
      expect(direct.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'doubleTapHold',
        attackName: weapon(defaultConfig, 'secondary-spider-knife').attacks.doubleTapHold.name,
      })
      expect(direct.access.weaponStates.get('secondary-spider-knife')?.resource).toBe(0)
    } finally {
      direct.engine.destroy()
    }
  })

  it('executes Chain spin throw, Axe grapple throw, and Axe spin leap through their contexts', () => {
    const chain = startCombat(combatConfig('either-claws', 'secondary-chain'))
    chain.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(chain.access, 'left', 0.48, 0)
      expect(chain.access.activeAreas.some(area => area.attack.behavior === 'chainSpin')).toBe(true)
      driveDualSenseTrigger(chain.access, 'left', 0.72, 100)
      driveDualSenseTrigger(chain.access, 'left', 0, 700)
      expect(chain.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'doubleTapHold',
        attackName: weapon(defaultConfig, 'secondary-chain').attacks.doubleTapHold.name,
      })
    } finally {
      chain.engine.destroy()
    }

    const grapple = startCombat(combatConfig('twohand-axe', null))
    grapple.engine.setControlScheme('dualsense')
    try {
      grapple.access.weaponStates.get('twohand-axe')!.boundEnemyId = grapple.access.enemies[0].id
      driveDualSenseTrigger(grapple.access, 'right', 0.72, 0)
      driveDualSenseTrigger(grapple.access, 'right', 0, 700)
      expect(grapple.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTapHold',
        attackName: weapon(defaultConfig, 'twohand-axe').attacks.doubleTapHold.name,
      })
    } finally {
      grapple.engine.destroy()
    }

    const spin = startCombat(combatConfig('twohand-axe', null))
    spin.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(spin.access, 'left', 0.22, 0)
      spin.access.updateHeldWeaponMechanics(16)
      expect(spin.access.heldChannels.get('right')?.attack.behavior).toBe('axeSpin')
      driveDualSenseTrigger(spin.access, 'left', 0.72, 700)
      driveDualSenseTrigger(spin.access, 'left', 0, 750)
      expect(spin.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'twohand-axe').secondaryAttacks!.holdThenDoubleTap.name,
      })
      expect(spin.access.activeDash?.attack.behavior).toBe('axeLeap')
    } finally {
      spin.engine.destroy()
    }
  })

  it('chooses Katana flurry/hop-slash on a fast pull and charge continuations after arming', () => {
    const primaryFast = startCombat(combatConfig('twohand-katana', null))
    primaryFast.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(primaryFast.access, 'right', 0.9, 0)
      driveDualSenseTrigger(primaryFast.access, 'right', 0, 100)
      expect(primaryFast.access.createSnapshot().lastGesture?.gesture).toBe('doubleTapHold')
    } finally {
      primaryFast.engine.destroy()
    }

    const primaryCharged = startCombat(combatConfig('twohand-katana', null))
    primaryCharged.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(primaryCharged.access, 'right', 0.48, 0)
      driveDualSenseTrigger(primaryCharged.access, 'right', 0.9, 700)
      expect(primaryCharged.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'twohand-katana').attacks.holdThenDoubleTap.name,
      })
    } finally {
      primaryCharged.engine.destroy()
    }

    const secondaryFast = startCombat(combatConfig('twohand-katana', null))
    secondaryFast.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(secondaryFast.access, 'left', 0.9, 0)
      driveDualSenseTrigger(secondaryFast.access, 'left', 0, 100)
      expect(secondaryFast.access.createSnapshot().lastGesture?.gesture).toBe('doubleTapHold')
    } finally {
      secondaryFast.engine.destroy()
    }

    const secondaryCharged = startCombat(combatConfig('twohand-katana', null))
    secondaryCharged.engine.setControlScheme('dualsense')
    try {
      driveDualSenseTrigger(secondaryCharged.access, 'left', 0.48, 0)
      driveDualSenseTrigger(secondaryCharged.access, 'left', 0.9, 700)
      expect(secondaryCharged.access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'twohand-katana').secondaryAttacks!.holdThenDoubleTap.name,
      })
    } finally {
      secondaryCharged.engine.destroy()
    }
  })

  it('keeps every Sword move on the left runtime hand and arms Unterhaw after 1 second', () => {
    const { engine, access } = startCombat(combatConfig('hybrid-sword', null))
    engine.setControlScheme('dualsense')
    try {
      expect(access.weapons.has('right')).toBe(false)
      driveDualSenseTrigger(access, 'right', 0.72, 0)
      driveDualSenseTrigger(access, 'right', 0, 1000)
      const equipped = access.weapons.get('left')!
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTapHold',
        attackName: equipped.attacks.doubleTapHold.name,
      })
      expect(access.delayedAttacks).toHaveLength(1)
    } finally {
      engine.destroy()
    }
  })

  it('advertises a runtime continuation once at window open and keeps its input authoritative', () => {
    const { engine, access } = startCombat(combatConfig('either-claws', null))
    engine.setControlScheme('dualsense')
    const feedback = vi.spyOn(access.feedbackController, 'emit')
    const continuationEvents = () => feedback.mock.calls
      .map(([event]) => event)
      .filter(event => event.state === 'continuation' && event.profile === 'followUp')

    try {
      access.performAttack(resolution('left', 'hold', 700))

      expect(access.activeDash?.attack.behavior).toBe('clawDash')
      expect(continuationEvents()).toEqual([
        expect.objectContaining({ hand: 'right', state: 'continuation', profile: 'followUp' }),
      ])
      expect(access.createSnapshot()).toMatchObject({
        controlCue: {
          hand: 'left',
          state: 'continuation',
          gesture: 'holdThenDoubleTap',
          tactileProfile: 'followUp',
        },
        controlRoles: [expect.objectContaining({
          hand: 'left',
          nextGate: weapon(defaultConfig, 'either-claws').attacks.holdThenDoubleTap.name,
        })],
      })

      access.update(0, 0)
      access.update(0, 0)
      expect(continuationEvents()).toHaveLength(1)

      driveDualSenseTrigger(access, 'right', 0.9, 800)
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'holdThenDoubleTap',
        attackName: weapon(defaultConfig, 'either-claws').attacks.holdThenDoubleTap.name,
      })
      expect(access.activeDash).toBeNull()
      expect(continuationEvents()).toHaveLength(1)
    } finally {
      engine.destroy()
    }
  })

  it('does not leak an enemy opening into another weapon generic continuation', () => {
    const { engine, access } = startCombat(combatConfig('twohand-spear', null))
    engine.setControlScheme('dualsense')
    const feedback = vi.spyOn(access.feedbackController, 'emit')
    try {
      access.enemies[0].statuses.openingMs = 1_000
      access.update(0, 0)

      expect(feedback.mock.calls
        .map(([event]) => event)
        .filter(event => event.state === 'continuation' && event.profile === 'followUp'))
        .toEqual([])
      for (const role of access.createSnapshot().controlRoles) {
        const equipped = access.weapons.get(role.hand)!
        const startNode = equipped.controls!.dualsense.nodes.find(node => (
          node.id === equipped.controls!.dualsense.startNodeId
        ))!
        expect(role.nextGate).toBe(equipped.attacks[startNode.gesture].name)
      }
    } finally {
      engine.destroy()
    }
  })

  it('uses mylorik bindings while surfacing an active contextual follow-up', () => {
    const { engine, access } = startCombat(combatConfig('either-claws', null))
    engine.setControlScheme('mylorik')
    try {
      access.performAttack(resolution('left', 'hold', 700))
      const equipped = access.weapons.get('left')!
      const role = access.createSnapshot().controlRoles.find(candidate => candidate.hand === 'left')

      expect(role).toEqual({
        hand: 'left',
        instantMove: equipped.attacks.tap.name,
        techniqueOrTrigger: equipped.controls!.role,
        nextGate: equipped.attacks.holdThenDoubleTap.name,
      })
      expect(role?.techniqueOrTrigger).not.toBe(equipped.controls!.dualsense.triggerRole)
    } finally {
      engine.destroy()
    }
  })

  it('publishes authored trigger tension without deciding the released action', () => {
    const { engine, access } = startCombat(combatConfig('either-claws', 'secondary-chain'))
    engine.setControlScheme('dualsense')
    const feedback = vi.spyOn(access.feedbackController, 'emit')
    try {
      driveDualSenseTrigger(access, 'left', 0.22, 0)

      expect(access.createSnapshot().controlCue).toMatchObject({
        hand: 'right',
        state: 'tension',
        gesture: 'hold',
        tactileProfile: 'tension',
      })
      expect(feedback.mock.calls
        .map(([event]) => event)
        .filter(event => event.state === 'tension')).toEqual([
          expect.objectContaining({ profile: 'tension', hand: 'left' }),
        ])
      expect(access.createSnapshot().lastGesture).toBeNull()

      driveDualSenseTrigger(access, 'left', 0, 700)
      expect(access.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'hold',
        attackName: weapon(defaultConfig, 'secondary-chain').attacks.hold.name,
      })
    } finally {
      engine.destroy()
    }
  })

  it('emits one sharp impact only after a damage-free parry succeeds', () => {
    const { engine, access } = startCombat(combatConfig('twohand-spear', null, 'guard', 1))
    engine.setControlScheme('dualsense')
    const feedback = vi.spyOn(access.feedbackController, 'emit')
    const target = access.enemies[0]
    let now = 1_000
    vi.spyOn(performance, 'now').mockImplementation(() => now)

    try {
      placeEnemy(access, target, 70)
      target.state = 'attacking'
      target.attackWindupMs = 100

      engine.press('right')
      expect(feedback.mock.calls.some(([event]) => event.state === 'impact')).toBe(false)
      now += 80
      engine.release('right')

      expect(target.state).toBe('chasing')
      expect(access.player.hp).toBe(access.player.stats.maxHp)
      expect(feedback.mock.calls
        .map(([event]) => event)
        .filter(event => event.state === 'impact')).toEqual([
          expect.objectContaining({ profile: 'impact', hand: 'left', strength: 1 }),
        ])
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })
})
