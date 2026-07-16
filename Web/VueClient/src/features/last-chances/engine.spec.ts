import { describe, expect, it, vi } from 'vitest'
import defaultConfigJson from '../../../public/99lc/game-config.json'
import { cloneLastChancesConfig } from './config'
import { LastChancesEngine } from './engine'
import type {
  LastChancesAttackDefinition,
  LastChancesConfig,
  LastChancesGamePlan,
  LastChancesGesture,
  LastChancesHand,
  LastChancesSnapshot,
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

type EngineTestAccess = {
  enemies: Array<{
    id: string
    definition: { maxHp: number, radius: number, attackCooldownMs: number }
    position: { x: number, y: number }
    hp: number
    state: 'idle' | 'noticing' | 'alerted' | 'chasing' | 'attacking' | 'dead'
    attackCooldownMs: number
    attackWindupMs: number
    lockedAttackDirection: { x: number, y: number } | null
  }>
  createSnapshot: () => LastChancesSnapshot
  elapsedMs: number
  handleBlur: () => void
  killPlayer: (reason: string) => void
  player: { position: { x: number, y: number } }
  performDash: (attack: LastChancesAttackDefinition, direction: { x: number, y: number }) => void
  performAttack: (hand: LastChancesHand, gesture: LastChancesGesture) => void
  startActiveArea: (
    kind: 'melee' | 'burst',
    attack: LastChancesAttackDefinition,
    direction: { x: number, y: number },
  ) => void
  updateActiveAreas: (deltaMs: number) => void
  updateEnemies: (deltaSeconds: number, deltaMs: number) => void
  updatePlayer: (deltaSeconds: number) => void
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

      const testAccess = engine as unknown as { killPlayer: (reason: string) => void }
      testAccess.killPlayer('Prototype test')
      const dead = snapshots.at(-1) as LastChancesSnapshot
      expect(dead.phase).toBe('dead')
      expect(dead.chances).toBe(defaultConfig.chances - defaultConfig.progression.tiers[0].deathCost)
      expect(dead.player.stats.maxHp).toBe(
        defaultConfig.player.baseStats.maxHp - defaultConfig.progression.tiers[0].erosion.maxHp,
      )
      expect(dead.player.stats.attackPower).toBe(
        defaultConfig.player.baseStats.attackPower - defaultConfig.progression.tiers[0].erosion.attackPower,
      )

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

  it('lets a connected standard gamepad select and enter the opening route', () => {
    const axes = [0, 0, 0, 0]
    const buttons = Array.from({ length: 16 }, () => ({ pressed: false, value: 0 }))
    const gamepad = {
      axes,
      buttons,
      connected: true,
      id: 'DualSense Wireless Controller',
      index: 0,
      mapping: 'standard',
    }
    vi.stubGlobal('navigator', { getGamepads: () => [gamepad] })
    const snapshots: LastChancesSnapshot[] = []
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig, {
      onSnapshot: snapshot => snapshots.push(snapshot),
    })
    const testAccess = engine as unknown as { pollGamepad: () => void }

    try {
      const opening = snapshots.at(-1) as LastChancesSnapshot
      expect(opening.availableNodeIds.length).toBeGreaterThan(1)
      expect(opening.selectedNodeId).toBe(opening.availableNodeIds[0])

      axes[0] = 1
      testAccess.pollGamepad()
      const cycled = snapshots.at(-1) as LastChancesSnapshot
      expect(cycled.selectedNodeId).toBe(opening.availableNodeIds[1])
      expect(cycled.gamepad).toMatchObject({
        connected: true,
        id: 'DualSense Wireless Controller',
        profile: 'standard',
      })

      axes[0] = 0
      testAccess.pollGamepad()
      buttons[0] = { pressed: true, value: 1 }
      testAccess.pollGamepad()

      const entered = snapshots.at(-1) as LastChancesSnapshot
      expect(entered.phase).toBe('playing')
      expect(entered.currentNodeId).toBe(opening.availableNodeIds[1])
      expect(entered.selectedNodeId).toBeNull()
    } finally {
      engine.destroy()
      vi.unstubAllGlobals()
      vi.restoreAllMocks()
    }
  })

  it('keeps basic taps cooldown-free and preserves their combo through specials and control loss', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const primary = config.weapons.find(weapon => weapon.id === config.loadout?.primaryWeaponId)!
    const comboNames = [primary.attacks.tap, ...(primary.tapCombo ?? [])].map(attack => attack.name)
    const engine = new LastChancesEngine(makeCanvas(), config)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      const opening = testAccess.createSnapshot().availableNodeIds[0]
      expect(engine.chooseNode(opening)).toBe(true)

      testAccess.performAttack('left', 'tap')
      expect(testAccess.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'tap',
        attackName: comboNames[0],
        comboStep: 1,
      })
      testAccess.performAttack('left', 'tap')
      expect(testAccess.createSnapshot().lastGesture).toMatchObject({
        attackName: comboNames[1],
        comboStep: 2,
      })

      testAccess.performAttack('left', 'doubleTap')
      expect(testAccess.createSnapshot().lastGesture).toMatchObject({
        hand: 'left',
        gesture: 'doubleTap',
      })
      expect(testAccess.createSnapshot().lastGesture?.comboStep).toBeUndefined()

      testAccess.handleBlur()
      engine.setPaused(true)
      engine.setPaused(false)
      testAccess.elapsedMs += Math.floor((config.input.tapComboWindowMs as number) / 2)
      testAccess.performAttack('left', 'tap')
      expect(testAccess.createSnapshot().lastGesture).toMatchObject({
        attackName: comboNames[2],
        comboStep: 3,
      })
      testAccess.performAttack('left', 'tap')
      expect(testAccess.createSnapshot().lastGesture).toMatchObject({
        attackName: comboNames[0],
        comboStep: 4,
      })

      const tapCooldown = testAccess.createSnapshot().cooldowns.find(cooldown => (
        cooldown.hand === 'left' && cooldown.gesture === 'tap'
      ))
      expect(tapCooldown).toEqual({ hand: 'left', gesture: 'tap', remainingMs: 0, totalMs: 0 })

      testAccess.elapsedMs += (config.input.tapComboWindowMs as number) + 1
      testAccess.performAttack('left', 'tap')
      expect(testAccess.createSnapshot().lastGesture).toMatchObject({
        attackName: comboNames[0],
        comboStep: 1,
      })
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('spends Chances on room offers, resets ordinary loot on death, and recovers corpse-bound weapons', () => {
    const ordinaryConfig = cloneLastChancesConfig(defaultConfig)
    ordinaryConfig.progression.tiers[0].enemyCount = [0, 0]
    ordinaryConfig.progression.tiers[0].roomTemplateIds = ['merchant-crossing']
    const ordinaryEngine = new LastChancesEngine(makeCanvas(), ordinaryConfig)
    const ordinaryAccess = ordinaryEngine as unknown as EngineTestAccess

    try {
      expect(ordinaryEngine.chooseNode(ordinaryAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      expect(ordinaryAccess.createSnapshot().phase).toBe('interaction')
      expect(ordinaryEngine.chooseInteraction('buy-bow')).toBe(true)
      expect(ordinaryAccess.createSnapshot()).toMatchObject({
        chances: ordinaryConfig.chances - 3,
        loadout: { primaryWeaponId: 'twohand-bow', secondaryWeaponId: null },
      })

      expect(ordinaryEngine.chooseNode(ordinaryAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      ordinaryAccess.killPlayer('Loot reset test')
      expect(ordinaryEngine.retryAttempt()).toBe(true)
      expect(ordinaryAccess.createSnapshot().loadout).toEqual(ordinaryConfig.loadout)
    } finally {
      ordinaryEngine.destroy()
      vi.restoreAllMocks()
    }

    const corpseConfig = cloneLastChancesConfig(defaultConfig)
    corpseConfig.progression.tiers[0].enemyCount = [0, 0]
    corpseConfig.progression.tiers[0].roomTemplateIds = ['chest-gallery']
    const corpseEngine = new LastChancesEngine(makeCanvas(), corpseConfig)
    const corpseAccess = corpseEngine as unknown as EngineTestAccess

    try {
      expect(corpseEngine.chooseNode(corpseAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      expect(corpseEngine.chooseInteraction('claim-corpse-sword')).toBe(true)
      expect(corpseAccess.createSnapshot().loadout).toEqual({
        primaryWeaponId: 'corpse-sword',
        secondaryWeaponId: null,
      })

      expect(corpseEngine.chooseNode(corpseAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      corpseAccess.killPlayer('Corpse recovery test')
      expect(corpseEngine.retryAttempt()).toBe(true)
      expect(corpseAccess.createSnapshot().loadout).toEqual({
        primaryWeaponId: 'corpse-sword',
        secondaryWeaponId: null,
      })
    } finally {
      corpseEngine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('queues standard attackers and lets a timed weapon collision parry the active one', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    config.progression.tiers[0].enemyCount = [2, 2]
    config.progression.tiers[0].enemyPool = [{ enemyId: 'guard', weight: 1 }]
    config.progression.tiers[0].roomTemplateIds = ['combat-hall']
    const engine = new LastChancesEngine(makeCanvas(), config)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      testAccess.enemies.forEach((enemy) => {
        enemy.state = 'chasing'
        enemy.attackCooldownMs = 0
        enemy.position = {
          x: testAccess.player.position.x + 45,
          y: testAccess.player.position.y,
        }
      })

      testAccess.updateEnemies(0, 16)
      const attackers = testAccess.enemies.filter(enemy => enemy.state === 'attacking')
      expect(attackers).toHaveLength(1)

      attackers[0].attackWindupMs = 100
      testAccess.performAttack('left', 'tap')
      expect(attackers[0].state).toBe('chasing')
      expect(attackers[0].attackCooldownMs).toBeGreaterThan(
        attackers[0].definition.attackCooldownMs,
      )
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('locks the knife-spider leap before travel and exposes authored boss phases', () => {
    const leapConfig = cloneLastChancesConfig(defaultConfig)
    leapConfig.progression.tiers[0].enemyCount = [1, 1]
    leapConfig.progression.tiers[0].enemyPool = [{ enemyId: 'spider-knife', weight: 1 }]
    leapConfig.progression.tiers[0].roomTemplateIds = ['combat-hall']
    const leapEngine = new LastChancesEngine(makeCanvas(), leapConfig)
    const leapAccess = leapEngine as unknown as EngineTestAccess

    try {
      expect(leapEngine.chooseNode(leapAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const spider = leapAccess.enemies[0]
      spider.state = 'chasing'
      spider.position = {
        x: leapAccess.player.position.x + 200,
        y: leapAccess.player.position.y,
      }
      leapAccess.updateEnemies(0, 1)
      leapAccess.updateEnemies(0, 520)
      expect(spider.lockedAttackDirection).toMatchObject({ x: -1, y: 0 })

      leapAccess.player.position.y += 180
      leapAccess.updateEnemies(0, 160)
      const beforeTravel = { ...spider.position }
      leapAccess.updateEnemies(0.1, 100)
      expect(spider.position.x).toBeLessThan(beforeTravel.x)
      expect(spider.position.y).toBeCloseTo(beforeTravel.y, 5)
    } finally {
      leapEngine.destroy()
      vi.restoreAllMocks()
    }

    const bossConfig = cloneLastChancesConfig(defaultConfig)
    bossConfig.progression.tiers[0].enemyCount = [1, 1]
    bossConfig.progression.tiers[0].enemyPool = [{ enemyId: 'curator-shadow', weight: 1 }]
    bossConfig.progression.tiers[0].roomTemplateIds = ['curator-threshold']
    const bossEngine = new LastChancesEngine(makeCanvas(), bossConfig)
    const bossAccess = bossEngine as unknown as EngineTestAccess

    try {
      expect(bossEngine.chooseNode(bossAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const boss = bossAccess.enemies[0]
      boss.hp = boss.definition.maxHp * 0.5
      expect(bossAccess.createSnapshot().enemies[0]).toMatchObject({
        phaseName: 'Архив чужих смертей',
        attackKind: 'projectile',
      })
      boss.hp = boss.definition.maxHp * 0.2
      expect(bossAccess.createSnapshot().enemies[0]).toMatchObject({
        phaseName: 'Тьма перед пробуждением',
        attackKind: 'leap',
      })
    } finally {
      bossEngine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('keeps the invisible wolf hidden until it becomes alerted', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    config.progression.tiers[0].enemyCount = [1, 1]
    config.progression.tiers[0].enemyPool = [{ enemyId: 'invisible-wolf', weight: 1 }]
    config.progression.tiers[0].roomTemplateIds = ['combat-hall']
    const engine = new LastChancesEngine(makeCanvas(), config)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      expect(testAccess.createSnapshot().enemies[0].visible).toBe(false)
      testAccess.enemies[0].state = 'alerted'
      expect(testAccess.createSnapshot().enemies[0].visible).toBe(true)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('uses the resolved secondary attack set for a two-handed weapon', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    const twoHanded = config.weapons[0]
    const secondaryAttacks = config.weapons[1].attacks
    twoHanded.id = 'test-two-handed'
    twoHanded.equipMode = 'twoHanded'
    delete twoHanded.hand
    secondaryAttacks.tap.name = 'Resolved rear-hand tap'
    twoHanded.secondaryAttacks = secondaryAttacks
    config.rooms.forEach(room => { delete room.interaction })
    config.weapons = [twoHanded]
    config.loadout = { primaryWeaponId: twoHanded.id, secondaryWeaponId: null }
    const engine = new LastChancesEngine(makeCanvas(), config)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      testAccess.performAttack('right', 'tap')

      expect(testAccess.createSnapshot().lastGesture).toMatchObject({
        hand: 'right',
        gesture: 'tap',
        attackName: 'Resolved rear-hand tap',
        comboStep: 1,
      })
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('keeps melee and burst areas active for their duration and damages each target once', () => {
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const [entering, late] = testAccess.enemies
      const attack = {
        ...defaultConfig.weapons[0].attacks.tap,
        kind: 'burst' as const,
        damage: 5,
        range: 30,
        radius: 5,
        durationMs: 100,
        knockback: 0,
        pierce: 8,
      }
      entering.position = { x: testAccess.player.position.x + 200, y: testAccess.player.position.y }
      late.position = { x: testAccess.player.position.x + 220, y: testAccess.player.position.y }
      const enteringHp = entering.hp
      const lateHp = late.hp

      testAccess.startActiveArea('burst', attack, { x: 1, y: 0 })
      entering.position = {
        x: testAccess.player.position.x + attack.range * 0.5 + attack.radius + entering.definition.radius - 1,
        y: testAccess.player.position.y,
      }
      testAccess.updateActiveAreas(50)
      expect(entering.hp).toBe(enteringHp - attack.damage)
      testAccess.updateActiveAreas(10)
      expect(entering.hp).toBe(enteringHp - attack.damage)

      testAccess.updateActiveAreas(40)
      testAccess.startActiveArea('burst', attack, { x: 1, y: 0 })
      testAccess.updateActiveAreas(101)
      late.position = {
        x: testAccess.player.position.x + attack.range,
        y: testAccess.player.position.y,
      }
      testAccess.updateActiveAreas(1)
      expect(late.hp).toBe(lateHp)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('expands burst collision over its authored duration', () => {
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const target = testAccess.enemies[0]
      const attack = {
        ...defaultConfig.weapons[0].attacks.tap,
        kind: 'burst' as const,
        damage: 5,
        range: 80,
        radius: 0,
        arcDegrees: 360,
        durationMs: 100,
        knockback: 0,
        pierce: 0,
      }
      target.position = {
        x: testAccess.player.position.x + attack.range + target.definition.radius - 1,
        y: testAccess.player.position.y,
      }
      const initialHp = target.hp

      testAccess.startActiveArea('burst', attack, { x: 1, y: 0 })
      expect(target.hp).toBe(initialHp)
      testAccess.updateActiveAreas(49)
      expect(target.hp).toBe(initialHp)
      testAccess.updateActiveAreas(51)
      expect(target.hp).toBe(initialHp - attack.damage)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('honors authored facing arcs for directional bursts', () => {
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const [front, behind] = testAccess.enemies
      front.position = { x: testAccess.player.position.x + 30, y: testAccess.player.position.y }
      behind.position = { x: testAccess.player.position.x - 30, y: testAccess.player.position.y }
      const frontHp = front.hp
      const behindHp = behind.hp
      const attack = {
        ...defaultConfig.weapons[0].attacks.tap,
        kind: 'burst' as const,
        damage: 5,
        range: 50,
        radius: 5,
        arcDegrees: 150,
        durationMs: 0,
        knockback: 0,
        pierce: 1,
      }

      testAccess.startActiveArea('burst', attack, { x: 1, y: 0 })

      expect(front.hp).toBe(frontHp - attack.damage)
      expect(behind.hp).toBe(behindHp)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('limits active-area targets to pierce plus one', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    config.progression.tiers[0].enemyCount = [3, 3]
    const engine = new LastChancesEngine(makeCanvas(), config)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const initialHp = testAccess.enemies.map(enemy => enemy.hp)
      testAccess.enemies.forEach((enemy) => {
        enemy.position = { x: testAccess.player.position.x + 20, y: testAccess.player.position.y }
      })
      testAccess.startActiveArea('burst', {
        ...config.weapons[0].attacks.tap,
        kind: 'burst',
        damage: 5,
        range: 50,
        radius: 10,
        arcDegrees: 360,
        durationMs: 0,
        knockback: 0,
        pierce: 1,
      }, { x: 1, y: 0 })

      expect(testAccess.enemies.filter((enemy, index) => enemy.hp < initialHp[index])).toHaveLength(2)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('limits dash targets to pierce plus one without stopping travel', () => {
    const config = cloneLastChancesConfig(defaultConfig)
    config.progression.tiers[0].enemyCount = [3, 3]
    const engine = new LastChancesEngine(makeCanvas(), config)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const initialHp = testAccess.enemies.map(enemy => enemy.hp)
      testAccess.enemies.forEach((enemy) => {
        enemy.position = { x: testAccess.player.position.x + 10, y: testAccess.player.position.y }
      })
      testAccess.performDash({
        ...config.weapons[0].attacks.tap,
        kind: 'dash',
        damage: 5,
        range: 50,
        radius: 100,
        durationMs: 1000,
        knockback: 0,
        pierce: 0,
      }, { x: 1, y: 0 })
      const beforeX = testAccess.player.position.x
      testAccess.updatePlayer(0.016)

      expect(testAccess.enemies.filter((enemy, index) => enemy.hp < initialHp[index])).toHaveLength(1)
      expect(testAccess.player.position.x).toBeGreaterThan(beforeX)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })

  it('uses authored melee radius as target collision padding', () => {
    const engine = new LastChancesEngine(makeCanvas(), defaultConfig)
    const testAccess = engine as unknown as EngineTestAccess

    try {
      expect(engine.chooseNode(testAccess.createSnapshot().availableNodeIds[0])).toBe(true)
      const target = testAccess.enemies[0]
      const attack = {
        ...defaultConfig.weapons[0].attacks.tap,
        kind: 'melee' as const,
        damage: 5,
        range: 30,
        radius: 0,
        arcDegrees: 360,
        durationMs: 0,
        knockback: 0,
      }
      target.position = {
        x: testAccess.player.position.x + attack.range + target.definition.radius + 5,
        y: testAccess.player.position.y,
      }
      const initialHp = target.hp

      testAccess.startActiveArea('melee', attack, { x: 1, y: 0 })
      expect(target.hp).toBe(initialHp)
      testAccess.startActiveArea('melee', { ...attack, radius: 6 }, { x: 1, y: 0 })
      expect(target.hp).toBe(initialHp - attack.damage)
    } finally {
      engine.destroy()
      vi.restoreAllMocks()
    }
  })
})
