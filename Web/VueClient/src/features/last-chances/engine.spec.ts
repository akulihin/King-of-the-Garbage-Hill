import { describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import { cloneLastChancesConfig } from './config'
import { LastChancesEngine } from './engine'
import { attackWithLastChancesAugment } from './weapon-runtime'
import {
  applyLastChancesStatusEffects,
  type LastChancesRuntimeStatuses,
  type LastChancesStoredDot,
} from './statuses'
import type {
  LastChancesAttackDefinition,
  LastChancesConfig,
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
  }>
  activeDash: {
    attack: LastChancesAttackDefinition
  } | null
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
      storedDot?: LastChancesStoredDot | null
      distance?: number
    },
  ) => void
  elapsedMs: number
  enemies: RuntimeEnemy[]
  effects: unknown[]
  finishEnemyDeath: (enemy: RuntimeEnemy) => void
  gestures: {
    press: (hand: LastChancesHand, atMs: number) => void
    reset: () => void
    update: (atMs: number) => void
  }
  heldChannels: Map<LastChancesHand, EngineTestAccess['activeAreas'][number]>
  killPlayer: (reason: string) => void
  performAttack: (resolution: LastChancesGestureResolution) => void
  performDash: (
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: RuntimeAttackContext,
  ) => void
  player: {
    position: LastChancesVector
    aim: LastChancesVector
    invulnerableMs: number
    recoveryMs: number
    rootMs: number
    parryMs: number
    armorMultiplier: number
    armorMultiplierMs: number
  }
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
  updateActiveAreas: (deltaMs: number) => void
  updateDelayedAttacks: (deltaMs: number) => void
  updateDelayedRecoveries: (deltaMs: number) => void
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

function startCombat(config: LastChancesConfig): {
  engine: LastChancesEngine
  access: EngineTestAccess
} {
  const engine = new LastChancesEngine(makeCanvas(), config)
  const access = engine as unknown as EngineTestAccess
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

describe('99LC engine attempt lifecycle', () => {
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
      placeEnemy(access, target, 20)
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

  it('classifies sword rhythm and consumes a three-hit opening for the critical Oberhau', () => {
    const config = combatConfig('hybrid-sword', null, 'curator-shadow', 1)
    const { engine, access } = startCombat(config)
    const sword = weapon(config, 'hybrid-sword')
    const target = access.enemies[0]

    try {
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')?.rhythm).toBe('idle')

      access.elapsedMs += 300
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')?.rhythm).toBe('good')

      access.elapsedMs += 100
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')).toMatchObject({
        rhythm: 'early',
        recoveryMs: 900,
      })

      access.weaponStates.get('hybrid-sword')!.recoveryMs = 0
      access.player.recoveryMs = 0
      access.elapsedMs += 700
      access.performAttack(resolution('left', 'tap'))
      expect(access.weaponStates.get('hybrid-sword')?.rhythm).toBe('late')

      const cleanTap = { ...sword.attacks.tap, hitEffects: [] }
      target.hp = target.definition.maxHp
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
        sword.attacks.doubleTap.damage * 2 - (target.definition.armor ?? 0),
      )
      expect(target.statuses.openingMs).toBe(0)
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
      access.performAttack(resolution('left', 'doubleTapHold', 700))

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
