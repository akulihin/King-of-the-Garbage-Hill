import {
  cloneLastChancesConfig,
  LastChancesConfigError,
  migrateLastChancesConfig,
  validateLastChancesConfig,
} from './config'
import { resolveLastChancesLoadout } from './equipment'
import { LastChancesGestureRecognizer } from './gestures'
import {
  createLastChancesGamepadAdapter,
  type LastChancesGamepadAdapter,
  type LastChancesGamepadReading,
} from './gamepad'
import { buildLastChancesPlan } from './plan'
import { createLastChancesRng } from './rng'
import { LAST_CHANCES_GESTURES, LAST_CHANCES_HANDS } from './types'
import type {
  LastChancesAttackDefinition,
  LastChancesConfig,
  LastChancesCooldownSnapshot,
  LastChancesEnemyDefinition,
  LastChancesEnemySnapshot,
  LastChancesEnemyState,
  LastChancesEngineCallbacks,
  LastChancesGamePlan,
  LastChancesGamepadSnapshot,
  LastChancesGesture,
  LastChancesGestureSnapshot,
  LastChancesHand,
  LastChancesObstacleDefinition,
  LastChancesPhase,
  LastChancesPlanNode,
  LastChancesResolvedWeapon,
  LastChancesSnapshot,
  LastChancesStats,
  LastChancesVector,
} from './types'

interface RuntimePlayer {
  position: LastChancesVector
  aim: LastChancesVector
  hp: number
  mentalHealth: number
  stats: LastChancesStats
  invulnerableMs: number
}

interface RuntimeEnemy {
  id: string
  definition: LastChancesEnemyDefinition
  position: LastChancesVector
  facing: LastChancesVector
  hp: number
  state: LastChancesEnemyState
  noticeMs: number
  alertMs: number
  attackCooldownMs: number
  attackWindupMs: number
}

interface RuntimeProjectile {
  id: number
  position: LastChancesVector
  velocity: LastChancesVector
  radius: number
  damage: number
  knockback: number
  remainingDistance: number
  remainingMs: number
  remainingHits: number
  hitIds: Set<string>
  color: string
}

interface RuntimeDash {
  direction: LastChancesVector
  remainingDistance: number
  speed: number
  damage: number
  radius: number
  knockback: number
  hitIds: Set<string>
  remainingHits: number
  color: string
}

interface RuntimeActiveArea {
  kind: 'melee' | 'burst'
  origin: LastChancesVector
  direction: LastChancesVector
  damage: number
  knockback: number
  range: number
  radius: number
  arcDegrees: number
  remainingMs: number
  totalMs: number
  hitIds: Set<string>
  remainingHits: number
}

interface RuntimeEffect {
  kind: 'melee' | 'burst' | 'dash' | 'hit'
  position: LastChancesVector
  direction: LastChancesVector
  range: number
  radius: number
  arcDegrees: number
  color: string
  remainingMs: number
  totalMs: number
}

interface RuntimeTapCombo {
  step: number
  expiresAtMs: number
}

interface IsometricLayout {
  centerX: number
  top: number
  diamondWidth: number
  diamondHeight: number
}

const MOVEMENT_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowLeft', 'ArrowDown', 'ArrowRight'])
const EPSILON = 0.000001

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.max(minimum, Math.min(maximum, value))
}

function vectorLength(value: LastChancesVector): number {
  return Math.hypot(value.x, value.y)
}

function normalize(value: LastChancesVector, fallback: LastChancesVector = { x: 1, y: 0 }): LastChancesVector {
  const length = vectorLength(value)
  if (length < EPSILON) return { ...fallback }
  return { x: value.x / length, y: value.y / length }
}

function normalizeInput(x: number, y: number): LastChancesVector {
  const length = Math.hypot(x, y)
  if (length <= 1) return { x, y }
  return { x: x / length, y: y / length }
}

function distanceSquared(a: LastChancesVector, b: LastChancesVector): number {
  const x = a.x - b.x
  const y = a.y - b.y
  return x * x + y * y
}

function copyStats(stats: LastChancesStats): LastChancesStats {
  return { ...stats }
}

function cooldownKey(hand: LastChancesHand, gesture: LastChancesGesture): string {
  return `${hand}:${gesture}`
}

function pointHitsObstacle(
  point: LastChancesVector,
  radius: number,
  obstacle: LastChancesObstacleDefinition,
): boolean {
  const nearestX = clamp(point.x, obstacle.x, obstacle.x + obstacle.width)
  const nearestY = clamp(point.y, obstacle.y, obstacle.y + obstacle.height)
  return distanceSquared(point, { x: nearestX, y: nearestY }) < radius * radius
}

function segmentHitsObstacle(
  start: LastChancesVector,
  end: LastChancesVector,
  obstacle: LastChancesObstacleDefinition,
): boolean {
  const direction = { x: end.x - start.x, y: end.y - start.y }
  let minimum = 0
  let maximum = 1
  for (const axis of ['x', 'y'] as const) {
    const origin = start[axis]
    const delta = direction[axis]
    const low = obstacle[axis]
    const high = low + (axis === 'x' ? obstacle.width : obstacle.height)
    if (Math.abs(delta) < EPSILON) {
      if (origin < low || origin > high) return false
      continue
    }
    const first = (low - origin) / delta
    const second = (high - origin) / delta
    minimum = Math.max(minimum, Math.min(first, second))
    maximum = Math.min(maximum, Math.max(first, second))
    if (minimum > maximum) return false
  }
  return true
}

export class LastChancesEngine {
  readonly config: LastChancesConfig

  private readonly canvas: HTMLCanvasElement
  private readonly context: CanvasRenderingContext2D
  private readonly callbacks: LastChancesEngineCallbacks
  private readonly enemyDefinitions: Map<string, LastChancesEnemyDefinition>
  private readonly weapons: Map<LastChancesHand, LastChancesResolvedWeapon>
  private readonly gestures: LastChancesGestureRecognizer
  private readonly pressedKeys = new Set<string>()
  private readonly cooldownEnds = new Map<string, number>()
  private readonly tapCombos: Record<LastChancesHand, RuntimeTapCombo> = {
    left: { step: 0, expiresAtMs: 0 },
    right: { step: 0, expiresAtMs: 0 },
  }
  private readonly gamepadButtons: Record<LastChancesHand, boolean> = { left: false, right: false }
  private readonly gamepadAdapter: LastChancesGamepadAdapter

  private plan: LastChancesGamePlan
  private generation = 1
  private phase: LastChancesPhase = 'planning'
  private paused = false
  private started = false
  private destroyed = false
  private frameId: number | null = null
  private lastFrameMs = 0
  private elapsedMs = 0
  private lastSnapshotAt = Number.NEGATIVE_INFINITY
  private chances: number
  private currentNode: LastChancesPlanNode | null = null
  private availableNodeIds: string[] = []
  private attemptPath: string[] = []
  private deathReason: string | null = null
  private enemies: RuntimeEnemy[] = []
  private projectiles: RuntimeProjectile[] = []
  private activeAreas: RuntimeActiveArea[] = []
  private effects: RuntimeEffect[] = []
  private activeDash: RuntimeDash | null = null
  private nextProjectileId = 1
  private lastGesture: LastChancesGestureSnapshot | null = null
  private player: RuntimePlayer
  private touchMove: LastChancesVector = { x: 0, y: 0 }
  private touchAim: LastChancesVector = { x: 0, y: 0 }
  private gamepadMove: LastChancesVector = { x: 0, y: 0 }
  private gamepadAim: LastChancesVector = { x: 0, y: 0 }
  private pointerAim: LastChancesVector = { x: 1, y: 0 }
  private selectedNodeId: string | null = null
  private gamepadMenuAxisEngaged = false
  private gamepadState: LastChancesGamepadSnapshot
  private cssWidth = 800
  private cssHeight = 600
  private dpr = 1
  private readonly resizeObserver: ResizeObserver | null

  constructor(
    canvas: HTMLCanvasElement,
    config: LastChancesConfig,
    callbacks: LastChancesEngineCallbacks = {},
  ) {
    const migratedConfig = migrateLastChancesConfig(config) as LastChancesConfig
    const validation = validateLastChancesConfig(migratedConfig)
    if (!validation.valid) throw new LastChancesConfigError('Invalid 99LC engine config', validation.errors)
    const context = canvas.getContext('2d')
    if (!context) throw new Error('99LC requires a Canvas 2D rendering context')

    this.canvas = canvas
    this.context = context
    this.callbacks = callbacks
    this.config = cloneLastChancesConfig(migratedConfig)
    this.gamepadAdapter = createLastChancesGamepadAdapter({
      deadZone: this.config.input.gamepadDeadZone,
      leftButton: this.config.input.gamepadLeftButton,
      rightButton: this.config.input.gamepadRightButton,
    })
    const gamepadSupported = typeof navigator !== 'undefined' && typeof navigator.getGamepads === 'function'
    this.gamepadState = {
      supported: gamepadSupported,
      connected: false,
      status: gamepadSupported ? 'disconnected' : 'unsupported',
      activeIndex: null,
      connectedCount: 0,
      id: null,
      mapping: null,
      profile: null,
    }
    this.chances = this.config.chances
    this.enemyDefinitions = new Map(this.config.enemies.map(enemy => [enemy.id, enemy]))
    const loadout = resolveLastChancesLoadout(this.config)
    this.weapons = new Map()
    if (loadout.left) this.weapons.set('left', loadout.left)
    if (loadout.right) this.weapons.set('right', loadout.right)
    this.plan = buildLastChancesPlan(this.config, this.generation)
    const baseStats = copyStats(this.config.player.baseStats)
    this.player = {
      position: { x: 0, y: 0 },
      aim: { x: 1, y: 0 },
      hp: baseStats.maxHp,
      mentalHealth: baseStats.maxMentalHealth,
      stats: baseStats,
      invulnerableMs: 0,
    }
    this.gestures = new LastChancesGestureRecognizer(this.config.input, (hand, gesture) => {
      this.performAttack(hand, gesture)
    })
    this.availableNodeIds = this.plan.tiers[0].map(node => node.id)
    this.selectedNodeId = this.availableNodeIds[0] ?? null
    this.attachEvents()
    this.resizeObserver = typeof ResizeObserver === 'undefined'
      ? null
      : new ResizeObserver(() => this.resize())
    this.resizeObserver?.observe(this.canvas)
    this.resize()
    this.render()
    this.emitPlan()
    this.emitSnapshot(true)
  }

  start(): void {
    if (this.started || this.destroyed) return
    this.started = true
    this.lastFrameMs = performance.now()
    this.frameId = requestAnimationFrame(this.tick)
  }

  destroy(): void {
    if (this.destroyed) return
    this.destroyed = true
    this.started = false
    if (this.frameId !== null) cancelAnimationFrame(this.frameId)
    this.frameId = null
    this.resizeObserver?.disconnect()
    this.detachEvents()
    this.pressedKeys.clear()
    this.gestures.reset()
    this.activeAreas = []
  }

  setPaused(paused: boolean): void {
    if (this.paused === paused || this.destroyed) return
    this.paused = paused
    this.lastFrameMs = performance.now()
    if (paused) {
      this.gestures.reset()
      this.gamepadButtons.left = false
      this.gamepadButtons.right = false
    }
    this.render()
    this.emitSnapshot(true)
  }

  chooseNode(nodeId: string): boolean {
    if (this.phase !== 'planning' || this.paused || !this.availableNodeIds.includes(nodeId)) return false
    const node = this.plan.nodes.find(candidate => candidate.id === nodeId)
    if (!node) return false
    this.currentNode = node
    this.attemptPath.push(node.id)
    this.availableNodeIds = []
    this.selectedNodeId = null
    this.player.position = { ...node.arena.playerSpawn }
    this.player.aim = { ...this.pointerAim }
    this.projectiles = []
    this.activeAreas = []
    this.effects = []
    this.activeDash = null
    this.cooldownEnds.clear()
    this.resetTapCombos()
    this.gestures.reset()
    this.enemies = node.enemies.map((enemy) => {
      const definition = this.enemyDefinitions.get(enemy.definitionId) as LastChancesEnemyDefinition
      const rng = createLastChancesRng(`${node.seed}:${enemy.id}:facing`)
      const angle = rng() * Math.PI * 2
      return {
        id: enemy.id,
        definition,
        position: { ...enemy.position },
        facing: { x: Math.cos(angle), y: Math.sin(angle) },
        hp: definition.maxHp,
        state: 'idle',
        noticeMs: 0,
        alertMs: 0,
        attackCooldownMs: 0,
        attackWindupMs: 0,
      }
    })
    this.phase = 'playing'
    this.deathReason = null
    if (this.enemies.length === 0) this.completeRoom()
    this.render()
    this.emitSnapshot(true)
    return true
  }

  retryAttempt(): boolean {
    if (this.phase !== 'dead' || this.chances <= 0) return false
    this.resetAttempt()
    this.render()
    this.emitSnapshot(true)
    return true
  }

  newGeneration(seedOverride?: string | number): LastChancesGamePlan {
    this.generation += 1
    this.plan = buildLastChancesPlan(this.config, this.generation, seedOverride)
    this.chances = this.config.chances
    this.elapsedMs = 0
    this.lastSnapshotAt = Number.NEGATIVE_INFINITY
    this.nextProjectileId = 1
    this.player.stats = copyStats(this.config.player.baseStats)
    this.resetAttempt()
    this.emitPlan()
    this.render()
    this.emitSnapshot(true)
    return JSON.parse(JSON.stringify(this.plan)) as LastChancesGamePlan
  }

  setTouchMove(x: number, y: number): void {
    this.touchMove = normalizeInput(x, y)
  }

  setTouchAim(x: number, y: number): void {
    this.touchAim = normalizeInput(x, y)
  }

  press(hand: LastChancesHand): void {
    if (this.phase !== 'playing' || this.paused || this.destroyed) return
    this.gestures.press(hand, performance.now())
  }

  release(hand: LastChancesHand): void {
    if (this.destroyed) return
    this.gestures.release(hand, performance.now())
  }

  pointerMove(clientX: number, clientY: number): void {
    if (!this.currentNode) return
    const bounds = this.canvas.getBoundingClientRect()
    const canvasPoint = {
      x: (clientX - bounds.left) * (this.cssWidth / Math.max(1, bounds.width)),
      y: (clientY - bounds.top) * (this.cssHeight / Math.max(1, bounds.height)),
    }
    const world = this.screenToWorld(canvasPoint, this.currentNode)
    this.pointerAim = normalize({
      x: world.x - this.player.position.x,
      y: world.y - this.player.position.y,
    }, this.pointerAim)
  }

  private readonly tick = (frameMs: number): void => {
    if (!this.started || this.destroyed) return
    const deltaMs = clamp(frameMs - this.lastFrameMs, 0, 50)
    this.lastFrameMs = frameMs
    this.pollGamepad()
    this.gestures.update(frameMs)
    if (!this.paused) {
      this.elapsedMs += deltaMs
      if (this.phase === 'playing') this.update(deltaMs / 1000, deltaMs)
    }
    this.render()
    this.emitSnapshot(false)
    this.frameId = requestAnimationFrame(this.tick)
  }

  private update(deltaSeconds: number, deltaMs: number): void {
    this.player.invulnerableMs = Math.max(0, this.player.invulnerableMs - deltaMs)
    this.updatePlayer(deltaSeconds)
    this.updateProjectiles(deltaSeconds, deltaMs)
    this.updateEnemies(deltaSeconds, deltaMs)
    this.updateActiveAreas(deltaMs)
    this.updateMentalHealth(deltaSeconds)
    this.effects.forEach(effect => effect.remainingMs -= deltaMs)
    this.effects = this.effects.filter(effect => effect.remainingMs > 0)
    if (this.phase === 'playing' && this.enemies.every(enemy => enemy.state === 'dead')) this.completeRoom()
  }

  private updatePlayer(deltaSeconds: number): void {
    const aim = this.resolveAim()
    if (vectorLength(aim) > this.config.input.aimDeadZone) this.player.aim = normalize(aim, this.player.aim)
    if (this.activeDash) {
      const travel = Math.min(this.activeDash.remainingDistance, this.activeDash.speed * deltaSeconds)
      this.moveCircle(this.player.position, {
        x: this.activeDash.direction.x * travel,
        y: this.activeDash.direction.y * travel,
      }, this.config.player.radius)
      this.activeDash.remainingDistance -= travel
      for (const enemy of this.enemies) {
        if (this.activeDash.remainingHits <= 0) break
        if (enemy.state === 'dead' || this.activeDash.hitIds.has(enemy.id)) continue
        const hitRange = this.config.player.radius + enemy.definition.radius + this.activeDash.radius
        if (distanceSquared(this.player.position, enemy.position) <= hitRange * hitRange) {
          this.activeDash.hitIds.add(enemy.id)
          this.activeDash.remainingHits -= 1
          this.damageEnemy(enemy, this.activeDash.damage, this.activeDash.knockback, this.activeDash.direction)
        }
      }
      if (this.activeDash.remainingDistance <= EPSILON) this.activeDash = null
      return
    }

    const movement = this.resolveMovement()
    this.moveCircle(this.player.position, {
      x: movement.x * this.player.stats.moveSpeed * deltaSeconds,
      y: movement.y * this.player.stats.moveSpeed * deltaSeconds,
    }, this.config.player.radius)
  }

  private updateProjectiles(deltaSeconds: number, deltaMs: number): void {
    for (const projectile of this.projectiles) {
      const travel = vectorLength(projectile.velocity) * deltaSeconds
      projectile.position.x += projectile.velocity.x * deltaSeconds
      projectile.position.y += projectile.velocity.y * deltaSeconds
      projectile.remainingDistance -= travel
      projectile.remainingMs -= deltaMs
      if (this.currentNode?.arena.obstacles.some(obstacle => (
        pointHitsObstacle(projectile.position, projectile.radius, obstacle)
      ))) {
        projectile.remainingHits = 0
        continue
      }
      for (const enemy of this.enemies) {
        if (enemy.state === 'dead' || projectile.hitIds.has(enemy.id)) continue
        const hitRange = projectile.radius + enemy.definition.radius
        if (distanceSquared(projectile.position, enemy.position) > hitRange * hitRange) continue
        projectile.hitIds.add(enemy.id)
        projectile.remainingHits -= 1
        const direction = normalize(projectile.velocity)
        this.damageEnemy(enemy, projectile.damage, projectile.knockback, direction)
        if (projectile.remainingHits <= 0) break
      }
    }
    this.projectiles = this.projectiles.filter(projectile => (
      projectile.remainingDistance > 0 && projectile.remainingMs > 0 && projectile.remainingHits > 0
    ))
  }

  private updateEnemies(deltaSeconds: number, deltaMs: number): void {
    for (const enemy of this.enemies) {
      if (enemy.state === 'dead') continue
      enemy.attackCooldownMs = Math.max(0, enemy.attackCooldownMs - deltaMs)
      const toPlayer = {
        x: this.player.position.x - enemy.position.x,
        y: this.player.position.y - enemy.position.y,
      }
      const distance = vectorLength(toPlayer)

      if (enemy.state === 'idle') {
        const angle = Math.atan2(enemy.facing.y, enemy.facing.x)
          + deltaSeconds * (enemy.definition.idleTurnRadiansPerSecond ?? 0.28)
        enemy.facing = { x: Math.cos(angle), y: Math.sin(angle) }
      }
      const seesPlayer = this.enemyCanSeePlayer(enemy)
      if (enemy.state === 'idle' || enemy.state === 'noticing') {
        if (!seesPlayer) {
          enemy.state = 'idle'
          enemy.noticeMs = 0
          continue
        }
        enemy.state = 'noticing'
        enemy.noticeMs += deltaMs
        enemy.facing = normalize(toPlayer, enemy.facing)
        if (enemy.noticeMs >= enemy.definition.noticeMs) {
          enemy.state = 'alerted'
          enemy.alertMs = enemy.definition.alertPauseMs
        }
        continue
      }

      if (enemy.state === 'alerted') {
        enemy.facing = normalize(toPlayer, enemy.facing)
        enemy.alertMs -= deltaMs
        if (enemy.alertMs <= 0) enemy.state = 'chasing'
        continue
      }

      enemy.facing = normalize(toPlayer, enemy.facing)
      if (enemy.state === 'attacking') {
        enemy.attackWindupMs -= deltaMs
        if (enemy.attackWindupMs <= 0) {
          if (distance <= enemy.definition.attackRange + this.config.player.radius) {
            this.damagePlayer(enemy.definition.attackDamage, enemy.definition.name)
          }
          enemy.attackCooldownMs = enemy.definition.attackCooldownMs
          enemy.state = 'chasing'
        }
        continue
      }

      if (distance <= enemy.definition.attackRange && enemy.attackCooldownMs <= 0) {
        enemy.state = 'attacking'
        enemy.attackWindupMs = enemy.definition.attackWindupMs
        continue
      }
      const desiredDistance = enemy.definition.attackRange
        * (enemy.definition.preferredAttackRangeRatio ?? 0.72)
      if (distance > desiredDistance) {
        this.moveCircle(enemy.position, {
          x: enemy.facing.x * enemy.definition.moveSpeed * deltaSeconds,
          y: enemy.facing.y * enemy.definition.moveSpeed * deltaSeconds,
        }, enemy.definition.radius)
      }
    }
  }

  private updateMentalHealth(deltaSeconds: number): void {
    const pressure = Math.min(
      this.config.mentalHealth.maxPressurePerSecond,
      this.enemies.reduce((sum, enemy) => (
        enemy.state === 'noticing' || enemy.state === 'alerted'
          || enemy.state === 'chasing' || enemy.state === 'attacking'
          ? sum + enemy.definition.mentalPressurePerSecond
          : sum
      ), 0),
    )
    if (pressure > 0) {
      this.player.mentalHealth = Math.max(0, this.player.mentalHealth - pressure * deltaSeconds)
    } else {
      this.player.mentalHealth = Math.min(
        this.player.stats.maxMentalHealth,
        this.player.mentalHealth + this.config.mentalHealth.calmRecoveryPerSecond * deltaSeconds,
      )
    }
    if (this.player.mentalHealth <= 0) this.killPlayer('Mental health collapsed')
  }

  private performAttack(hand: LastChancesHand, gesture: LastChancesGesture): void {
    if (this.phase !== 'playing' || this.paused || !this.currentNode) return
    const weapon = this.weapons.get(hand)
    if (!weapon) return
    const key = cooldownKey(hand, gesture)
    if (gesture !== 'tap') {
      if ((this.cooldownEnds.get(key) ?? 0) > this.elapsedMs) return
    }
    const comboStep = gesture === 'tap' ? this.advanceTapCombo(hand) : undefined
    const attack = comboStep === undefined
      ? weapon.attacks[gesture]
      : weapon.tapCombo[(comboStep - 1) % weapon.tapCombo.length]
    if (gesture !== 'tap') this.cooldownEnds.set(key, this.elapsedMs + attack.cooldownMs)
    this.lastGesture = {
      hand,
      gesture,
      attackName: attack.name,
      atMs: this.elapsedMs,
      ...(comboStep === undefined ? {} : { comboStep }),
    }
    const direction = normalize(this.player.aim)

    if (attack.kind === 'melee') this.performMelee(attack, direction)
    if (attack.kind === 'projectile') this.performProjectile(attack, direction)
    if (attack.kind === 'dash') this.performDash(attack, direction)
    if (attack.kind === 'burst') this.performBurst(attack, direction)
  }

  private advanceTapCombo(hand: LastChancesHand): number {
    const combo = this.tapCombos[hand]
    combo.step = combo.step > 0 && this.elapsedMs <= combo.expiresAtMs
      ? combo.step + 1
      : 1
    combo.expiresAtMs = this.elapsedMs + (
      this.config.input.tapComboWindowMs
      ?? Math.max(600, this.config.input.doubleTapMs * 2)
    )
    return combo.step
  }

  private resetTapCombos(): void {
    for (const hand of LAST_CHANCES_HANDS) {
      this.tapCombos[hand].step = 0
      this.tapCombos[hand].expiresAtMs = 0
    }
  }

  private performMelee(attack: LastChancesAttackDefinition, direction: LastChancesVector): void {
    this.startActiveArea('melee', attack, direction)
    this.addEffect('melee', attack, direction)
  }

  private performProjectile(attack: LastChancesAttackDefinition, direction: LastChancesVector): void {
    const speed = Math.max(1, attack.projectileSpeed)
    this.projectiles.push({
      id: this.nextProjectileId,
      position: {
        x: this.player.position.x + direction.x * (this.config.player.radius + attack.radius + 2),
        y: this.player.position.y + direction.y * (this.config.player.radius + attack.radius + 2),
      },
      velocity: { x: direction.x * speed, y: direction.y * speed },
      radius: attack.radius,
      damage: attack.damage,
      knockback: attack.knockback,
      remainingDistance: attack.range,
      remainingMs: attack.durationMs > 0 ? attack.durationMs : (attack.range / speed) * 1000,
      remainingHits: attack.pierce + 1,
      hitIds: new Set(),
      color: attack.color,
    })
    this.nextProjectileId += 1
    this.addEffect('hit', attack, direction)
  }

  private performDash(attack: LastChancesAttackDefinition, direction: LastChancesVector): void {
    const durationSeconds = Math.max(0.08, attack.durationMs / 1000)
    this.activeDash = {
      direction,
      remainingDistance: attack.range,
      speed: attack.range / durationSeconds,
      damage: attack.damage,
      radius: attack.radius,
      knockback: attack.knockback,
      hitIds: new Set(),
      remainingHits: attack.pierce + 1,
      color: attack.color,
    }
    this.player.invulnerableMs = Math.max(this.player.invulnerableMs, attack.durationMs)
    this.addEffect('dash', attack, direction)
  }

  private performBurst(attack: LastChancesAttackDefinition, direction: LastChancesVector): void {
    this.startActiveArea('burst', attack, direction)
    this.addEffect('burst', attack, direction)
  }

  private startActiveArea(
    kind: RuntimeActiveArea['kind'],
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
  ): void {
    const area: RuntimeActiveArea = {
      kind,
      origin: { ...this.player.position },
      direction: { ...direction },
      damage: attack.damage,
      knockback: attack.knockback,
      range: attack.range,
      radius: attack.radius,
      arcDegrees: attack.arcDegrees,
      remainingMs: attack.durationMs,
      totalMs: attack.durationMs,
      hitIds: new Set(),
      remainingHits: attack.pierce + 1,
    }
    this.applyActiveAreaHits(area)
    if (area.remainingMs > 0 && area.remainingHits > 0) this.activeAreas.push(area)
  }

  private updateActiveAreas(deltaMs: number): void {
    for (const area of this.activeAreas) {
      area.remainingMs = Math.max(0, area.remainingMs - deltaMs)
      this.applyActiveAreaHits(area)
    }
    this.activeAreas = this.activeAreas.filter(area => area.remainingMs > 0 && area.remainingHits > 0)
  }

  private applyActiveAreaHits(area: RuntimeActiveArea): void {
    for (const enemy of this.enemies) {
      if (area.remainingHits <= 0) break
      if (enemy.state === 'dead' || area.hitIds.has(enemy.id)) continue
      const toEnemy = {
        x: enemy.position.x - area.origin.x,
        y: enemy.position.y - area.origin.y,
      }
      const authoredReach = area.kind === 'burst' && area.totalMs > 0
        ? area.range * clamp(1 - area.remainingMs / area.totalMs, 0, 1)
        : area.range
      const hitRange = authoredReach + area.radius + enemy.definition.radius
      if (distanceSquared(area.origin, enemy.position) > hitRange * hitRange) continue
      if (area.arcDegrees < 360) {
        const unit = normalize(toEnemy)
        const dot = unit.x * area.direction.x + unit.y * area.direction.y
        if (dot < Math.cos(area.arcDegrees * Math.PI / 360)) continue
      }
      area.hitIds.add(enemy.id)
      area.remainingHits -= 1
      const knockbackDirection = area.kind === 'burst'
        ? normalize(toEnemy, area.direction)
        : area.direction
      this.damageEnemy(enemy, area.damage, area.knockback, knockbackDirection)
    }
  }

  private addEffect(
    kind: RuntimeEffect['kind'],
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
  ): void {
    const duration = kind === 'melee' || kind === 'burst'
      ? Math.max(1, attack.durationMs)
      : Math.max(160, attack.durationMs)
    this.effects.push({
      kind,
      position: { ...this.player.position },
      direction: { ...direction },
      range: attack.range,
      radius: attack.radius,
      arcDegrees: attack.arcDegrees,
      color: attack.color,
      remainingMs: duration,
      totalMs: duration,
    })
  }

  private damageEnemy(
    enemy: RuntimeEnemy,
    damage: number,
    knockback: number,
    direction: LastChancesVector,
  ): void {
    if (enemy.state === 'dead') return
    const scaledDamage = damage * this.player.stats.attackPower / 100
    enemy.hp = Math.max(0, enemy.hp - scaledDamage)
    if (knockback > 0) {
      this.moveCircle(enemy.position, { x: direction.x * knockback, y: direction.y * knockback }, enemy.definition.radius)
    }
    if (enemy.hp > 0) {
      if (enemy.state === 'idle') enemy.state = 'chasing'
      return
    }
    enemy.state = 'dead'
    this.player.mentalHealth = Math.min(
      this.player.stats.maxMentalHealth,
      this.player.mentalHealth + this.config.mentalHealth.restoreOnKill,
    )
  }

  private damagePlayer(rawDamage: number, source: string): void {
    if (this.player.invulnerableMs > 0 || this.phase !== 'playing') return
    const damage = Math.max(1, rawDamage - this.player.stats.armor)
    this.player.hp = Math.max(0, this.player.hp - damage)
    this.player.invulnerableMs = this.config.player.invulnerabilityMs
    if (this.player.hp <= 0) this.killPlayer(`Killed by ${source}`)
  }

  private killPlayer(reason: string): void {
    if (this.phase !== 'playing') return
    const tierIndex = this.currentNode?.tierIndex ?? 0
    const tier = this.config.progression.tiers[tierIndex]
    this.chances = Math.max(0, this.chances - tier.deathCost)
    const erosion = tier.erosion
    this.player.stats = {
      maxHp: Math.max(1, this.player.stats.maxHp - erosion.maxHp),
      maxMentalHealth: Math.max(1, this.player.stats.maxMentalHealth - erosion.maxMentalHealth),
      attackPower: Math.max(1, this.player.stats.attackPower - erosion.attackPower),
      moveSpeed: Math.max(1, this.player.stats.moveSpeed - erosion.moveSpeed),
      armor: Math.max(0, this.player.stats.armor - erosion.armor),
    }
    this.activeDash = null
    this.activeAreas = []
    this.deathReason = reason
    this.phase = this.chances > 0 ? 'dead' : 'outOfChances'
    this.emitSnapshot(true)
  }

  private completeRoom(): void {
    if (!this.currentNode || this.phase !== 'playing') return
    if (this.currentNode.tierIndex >= this.plan.tiers.length - 1) {
      this.phase = 'won'
      this.availableNodeIds = []
      this.selectedNodeId = null
    } else {
      this.player.hp = Math.min(
        this.player.stats.maxHp,
        this.player.hp + this.config.progression.roomHpRecovery,
      )
      this.player.mentalHealth = Math.min(
        this.player.stats.maxMentalHealth,
        this.player.mentalHealth + this.config.progression.roomMentalRecovery,
      )
      this.phase = 'planning'
      this.availableNodeIds = [...this.currentNode.nextNodeIds]
      this.selectedNodeId = this.availableNodeIds[0] ?? null
    }
    this.projectiles = []
    this.activeDash = null
    this.activeAreas = []
    this.gestures.reset()
    this.resetTapCombos()
    this.emitSnapshot(true)
  }

  private resetAttempt(): void {
    this.phase = 'planning'
    this.paused = false
    this.currentNode = null
    this.availableNodeIds = this.plan.tiers[0].map(node => node.id)
    this.selectedNodeId = this.availableNodeIds[0] ?? null
    this.gamepadMenuAxisEngaged = false
    this.attemptPath = []
    this.deathReason = null
    this.lastGesture = null
    this.enemies = []
    this.projectiles = []
    this.activeAreas = []
    this.effects = []
    this.activeDash = null
    this.cooldownEnds.clear()
    this.resetTapCombos()
    this.gestures.reset()
    this.pressedKeys.clear()
    this.touchMove = { x: 0, y: 0 }
    this.touchAim = { x: 0, y: 0 }
    this.gamepadMove = { x: 0, y: 0 }
    this.gamepadAim = { x: 0, y: 0 }
    this.player.position = { x: 0, y: 0 }
    this.player.aim = { x: 1, y: 0 }
    this.player.hp = this.player.stats.maxHp
    this.player.mentalHealth = this.player.stats.maxMentalHealth
    this.player.invulnerableMs = 0
  }

  private enemyCanSeePlayer(enemy: RuntimeEnemy): boolean {
    if (!this.currentNode) return false
    const toPlayer = {
      x: this.player.position.x - enemy.position.x,
      y: this.player.position.y - enemy.position.y,
    }
    const distance = vectorLength(toPlayer)
    if (distance > enemy.definition.visionRange) return false
    const direction = normalize(toPlayer)
    const dot = direction.x * enemy.facing.x + direction.y * enemy.facing.y
    if (dot < Math.cos(enemy.definition.visionAngleDegrees * Math.PI / 360)) return false
    return !this.currentNode.arena.obstacles.some(obstacle => (
      segmentHitsObstacle(enemy.position, this.player.position, obstacle)
    ))
  }

  private moveCircle(position: LastChancesVector, delta: LastChancesVector, radius: number): void {
    if (!this.currentNode) return
    const arena = this.currentNode.arena
    const previousX = position.x
    position.x = clamp(position.x + delta.x, radius, arena.width - radius)
    if (arena.obstacles.some(obstacle => pointHitsObstacle(position, radius, obstacle))) position.x = previousX
    const previousY = position.y
    position.y = clamp(position.y + delta.y, radius, arena.height - radius)
    if (arena.obstacles.some(obstacle => pointHitsObstacle(position, radius, obstacle))) position.y = previousY
  }

  private resolveMovement(): LastChancesVector {
    let x = 0
    let y = 0
    if (this.pressedKeys.has('KeyA') || this.pressedKeys.has('ArrowLeft')) x -= 1
    if (this.pressedKeys.has('KeyD') || this.pressedKeys.has('ArrowRight')) x += 1
    if (this.pressedKeys.has('KeyW') || this.pressedKeys.has('ArrowUp')) y -= 1
    if (this.pressedKeys.has('KeyS') || this.pressedKeys.has('ArrowDown')) y += 1
    x += this.touchMove.x + this.gamepadMove.x
    y += this.touchMove.y + this.gamepadMove.y
    return normalizeInput(x, y)
  }

  private resolveAim(): LastChancesVector {
    if (vectorLength(this.gamepadAim) > this.config.input.aimDeadZone) return this.gamepadAim
    if (vectorLength(this.touchAim) > this.config.input.aimDeadZone) return this.touchAim
    return this.pointerAim
  }

  private pollGamepad(): void {
    if (typeof navigator === 'undefined' || typeof navigator.getGamepads !== 'function') {
      this.applyGamepadReading(null)
      return
    }
    this.applyGamepadReading(this.gamepadAdapter.poll(Array.from(navigator.getGamepads())))
  }

  private applyGamepadReading(reading: LastChancesGamepadReading | null): void {
    const nextState: LastChancesGamepadSnapshot = reading
      ? {
          supported: true,
          connected: reading.activeIndex !== null,
          status: reading.status,
          activeIndex: reading.activeIndex,
          connectedCount: reading.connectedCount,
          id: reading.id,
          mapping: reading.mapping,
          profile: reading.profile,
        }
      : {
          supported: false,
          connected: false,
          status: 'unsupported',
          activeIndex: null,
          connectedCount: 0,
          id: null,
          mapping: null,
          profile: null,
        }
    const metadataChanged = JSON.stringify(nextState) !== JSON.stringify(this.gamepadState)
    this.gamepadState = nextState
    this.gamepadMove = reading?.move ?? { x: 0, y: 0 }
    this.gamepadAim = reading?.aim ?? { x: 0, y: 0 }

    const nextButtons: Record<LastChancesHand, boolean> = reading?.buttons ?? { left: false, right: false }
    const leftPressed = nextButtons.left && !this.gamepadButtons.left
    const rightPressed = nextButtons.right && !this.gamepadButtons.right

    if (this.phase === 'planning' && !this.paused) {
      const menuAxis = reading?.move ?? { x: 0, y: 0 }
      const dominantAxis = Math.abs(menuAxis.x) >= Math.abs(menuAxis.y) ? menuAxis.x : menuAxis.y
      if (Math.abs(dominantAxis) >= 0.55 && !this.gamepadMenuAxisEngaged) {
        this.cycleSelectedNode(dominantAxis > 0 ? 1 : -1)
        this.gamepadMenuAxisEngaged = true
      } else if (Math.abs(dominantAxis) <= 0.25) {
        this.gamepadMenuAxisEngaged = false
      }
      if (leftPressed) this.cycleSelectedNode(1)
      if (rightPressed && this.selectedNodeId) this.chooseNode(this.selectedNodeId)
    } else if (!this.paused && this.phase === 'playing') {
      for (const hand of LAST_CHANCES_HANDS) {
        const pressed = nextButtons[hand]
        if (pressed && !this.gamepadButtons[hand]) this.press(hand)
        if (!pressed && this.gamepadButtons[hand]) this.release(hand)
      }
    } else if (rightPressed && !this.paused) {
      if (this.phase === 'dead') this.retryAttempt()
      if (this.phase === 'won' || this.phase === 'outOfChances') this.newGeneration()
    }

    this.gamepadButtons.left = nextButtons.left
    this.gamepadButtons.right = nextButtons.right
    if (metadataChanged) this.emitSnapshot(true)
  }

  private cycleSelectedNode(direction: number): void {
    if (this.availableNodeIds.length === 0) return
    const currentIndex = this.selectedNodeId === null ? -1 : this.availableNodeIds.indexOf(this.selectedNodeId)
    const nextIndex = (Math.max(0, currentIndex) + direction + this.availableNodeIds.length)
      % this.availableNodeIds.length
    this.selectedNodeId = this.availableNodeIds[nextIndex]
    this.emitSnapshot(true)
  }

  private attachEvents(): void {
    window.addEventListener('resize', this.resize)
    window.addEventListener('keydown', this.handleKeyDown)
    window.addEventListener('keyup', this.handleKeyUp)
    window.addEventListener('blur', this.handleBlur)
    this.canvas.addEventListener('pointermove', this.handlePointerMove)
    this.canvas.addEventListener('pointerdown', this.handlePointerDown)
    this.canvas.addEventListener('pointerup', this.handlePointerUp)
    this.canvas.addEventListener('pointercancel', this.handlePointerUp)
    this.canvas.addEventListener('contextmenu', this.handleContextMenu)
  }

  private detachEvents(): void {
    window.removeEventListener('resize', this.resize)
    window.removeEventListener('keydown', this.handleKeyDown)
    window.removeEventListener('keyup', this.handleKeyUp)
    window.removeEventListener('blur', this.handleBlur)
    this.canvas.removeEventListener('pointermove', this.handlePointerMove)
    this.canvas.removeEventListener('pointerdown', this.handlePointerDown)
    this.canvas.removeEventListener('pointerup', this.handlePointerUp)
    this.canvas.removeEventListener('pointercancel', this.handlePointerUp)
    this.canvas.removeEventListener('contextmenu', this.handleContextMenu)
  }

  private readonly handleKeyDown = (event: KeyboardEvent): void => {
    const activeControl = this.phase === 'playing' && !this.paused
    const isAttackKey = this.config.input.leftKeys.includes(event.code)
      || this.config.input.rightKeys.includes(event.code)
    if (activeControl && (MOVEMENT_KEYS.has(event.code) || isAttackKey)) event.preventDefault()
    if (activeControl && MOVEMENT_KEYS.has(event.code)) this.pressedKeys.add(event.code)
    if (event.repeat) return
    if (this.config.input.leftKeys.includes(event.code)) this.press('left')
    if (this.config.input.rightKeys.includes(event.code)) this.press('right')
  }

  private readonly handleKeyUp = (event: KeyboardEvent): void => {
    this.pressedKeys.delete(event.code)
    if (this.config.input.leftKeys.includes(event.code)) this.release('left')
    if (this.config.input.rightKeys.includes(event.code)) this.release('right')
  }

  private readonly handleBlur = (): void => {
    this.pressedKeys.clear()
    this.touchMove = { x: 0, y: 0 }
    this.gamepadMove = { x: 0, y: 0 }
    this.gamepadAim = { x: 0, y: 0 }
    this.gamepadButtons.left = false
    this.gamepadButtons.right = false
    this.gamepadMenuAxisEngaged = false
    this.gestures.reset()
  }

  private readonly handlePointerMove = (event: PointerEvent): void => {
    this.pointerMove(event.clientX, event.clientY)
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (event.pointerType === 'touch') return
    this.canvas.setPointerCapture?.(event.pointerId)
    if (event.button === 0) this.press('left')
    if (event.button === 2) this.press('right')
  }

  private readonly handlePointerUp = (event: PointerEvent): void => {
    if (event.pointerType === 'touch') return
    if (event.button === 0) this.release('left')
    if (event.button === 2) this.release('right')
  }

  private readonly handleContextMenu = (event: MouseEvent): void => {
    event.preventDefault()
  }

  private readonly resize = (): void => {
    const bounds = this.canvas.getBoundingClientRect()
    this.cssWidth = Math.max(1, Math.round(bounds.width || this.canvas.clientWidth || 800))
    this.cssHeight = Math.max(1, Math.round(bounds.height || this.canvas.clientHeight || 600))
    this.dpr = Math.min(this.config.renderer.maxDpr, window.devicePixelRatio || 1)
    const width = Math.round(this.cssWidth * this.dpr)
    const height = Math.round(this.cssHeight * this.dpr)
    if (this.canvas.width !== width) this.canvas.width = width
    if (this.canvas.height !== height) this.canvas.height = height
    this.render()
  }

  private layout(): IsometricLayout {
    const maximumWidth = this.cssWidth * 0.88
    const maximumHeight = this.cssHeight * 0.68
    const diamondWidth = Math.max(1, Math.min(maximumWidth, maximumHeight / 0.52))
    const diamondHeight = diamondWidth * 0.52
    return {
      centerX: this.cssWidth / 2,
      top: Math.max(Math.min(76, this.cssHeight * 0.14), (this.cssHeight - diamondHeight) / 2),
      diamondWidth,
      diamondHeight,
    }
  }

  private worldToScreen(point: LastChancesVector, node: LastChancesPlanNode): LastChancesVector {
    const layout = this.layout()
    const u = point.x / node.arena.width
    const v = point.y / node.arena.height
    return {
      x: layout.centerX + (u - v) * layout.diamondWidth / 2,
      y: layout.top + (u + v) * layout.diamondHeight / 2,
    }
  }

  private screenToWorld(point: LastChancesVector, node: LastChancesPlanNode): LastChancesVector {
    const layout = this.layout()
    const difference = (point.x - layout.centerX) / (layout.diamondWidth / 2)
    const sum = (point.y - layout.top) / (layout.diamondHeight / 2)
    return {
      x: clamp((difference + sum) / 2, 0, 1) * node.arena.width,
      y: clamp((sum - difference) / 2, 0, 1) * node.arena.height,
    }
  }

  private emitPlan(): void {
    if (!this.callbacks.onPlan) return
    this.callbacks.onPlan(JSON.parse(JSON.stringify(this.plan)) as LastChancesGamePlan)
  }

  private emitSnapshot(force: boolean): void {
    if (!this.callbacks.onSnapshot) return
    const interval = 1000 / this.config.renderer.snapshotHz
    if (!force && this.elapsedMs - this.lastSnapshotAt < interval) return
    this.lastSnapshotAt = this.elapsedMs
    this.callbacks.onSnapshot(this.createSnapshot())
  }

  private createSnapshot(): LastChancesSnapshot {
    const now = performance.now()
    const enemies: LastChancesEnemySnapshot[] = this.enemies.map(enemy => ({
      id: enemy.id,
      definitionId: enemy.definition.id,
      name: enemy.definition.name,
      position: { ...enemy.position },
      facing: { ...enemy.facing },
      hp: enemy.hp,
      maxHp: enemy.definition.maxHp,
      state: enemy.state,
      noticeProgress: enemy.state === 'noticing'
        ? clamp(enemy.noticeMs / enemy.definition.noticeMs, 0, 1)
        : enemy.state === 'alerted' || enemy.state === 'chasing' || enemy.state === 'attacking' ? 1 : 0,
      attackCooldownMs: enemy.attackCooldownMs,
    }))
    const cooldowns: LastChancesCooldownSnapshot[] = []
    for (const hand of LAST_CHANCES_HANDS) {
      const weapon = this.weapons.get(hand)
      if (!weapon) continue
      for (const gesture of LAST_CHANCES_GESTURES) {
        const totalMs = gesture === 'tap' ? 0 : weapon.attacks[gesture].cooldownMs
        const remainingMs = gesture === 'tap'
          ? 0
          : Math.max(0, (this.cooldownEnds.get(cooldownKey(hand, gesture)) ?? 0) - this.elapsedMs)
        cooldowns.push({ hand, gesture, remainingMs, totalMs })
      }
    }
    return {
      phase: this.phase,
      paused: this.paused,
      generation: this.generation,
      chances: this.chances,
      elapsedMs: this.elapsedMs,
      currentNodeId: this.currentNode?.id ?? null,
      currentTierIndex: this.currentNode?.tierIndex ?? null,
      attemptPath: [...this.attemptPath],
      availableNodeIds: [...this.availableNodeIds],
      deathReason: this.deathReason,
      player: {
        position: { ...this.player.position },
        aim: { ...this.player.aim },
        hp: this.player.hp,
        mentalHealth: this.player.mentalHealth,
        stats: copyStats(this.player.stats),
        invulnerableForMs: this.player.invulnerableMs,
      },
      enemies,
      projectiles: this.projectiles.map(projectile => ({
        id: projectile.id,
        position: { ...projectile.position },
        radius: projectile.radius,
        color: projectile.color,
      })),
      cooldowns,
      lastGesture: this.lastGesture ? { ...this.lastGesture } : null,
      gestureInputs: LAST_CHANCES_HANDS.map(hand => this.gestures.snapshot(hand, now)),
      gamepad: { ...this.gamepadState },
      selectedNodeId: this.selectedNodeId,
    }
  }

  private render(): void {
    const context = this.context
    context.setTransform(this.dpr, 0, 0, this.dpr, 0, 0)
    context.clearRect(0, 0, this.cssWidth, this.cssHeight)
    context.fillStyle = this.config.renderer.background
    context.fillRect(0, 0, this.cssWidth, this.cssHeight)
    this.renderAtmosphere()
    if (this.currentNode) this.renderArena(this.currentNode)
    else this.renderEmptyPlan()
    this.renderHud()
    this.renderOverlay()
  }

  private renderAtmosphere(): void {
    const gradient = this.context.createRadialGradient(
      this.cssWidth / 2,
      this.cssHeight * 0.48,
      0,
      this.cssWidth / 2,
      this.cssHeight * 0.48,
      Math.max(this.cssWidth, this.cssHeight) * 0.72,
    )
    gradient.addColorStop(0, 'rgba(112, 82, 112, 0.17)')
    gradient.addColorStop(0.52, 'rgba(21, 17, 30, 0.1)')
    gradient.addColorStop(1, 'rgba(0, 0, 0, 0.52)')
    this.context.fillStyle = gradient
    this.context.fillRect(0, 0, this.cssWidth, this.cssHeight)
  }

  private renderArena(node: LastChancesPlanNode): void {
    this.renderFloor(node)
    for (const enemy of this.enemies) this.renderVision(enemy, node)
    const items: Array<{ depth: number, draw: () => void }> = []
    for (const obstacle of node.arena.obstacles) {
      items.push({
        depth: obstacle.x + obstacle.width + obstacle.y + obstacle.height,
        draw: () => this.renderObstacle(obstacle, node),
      })
    }
    for (const enemy of this.enemies) {
      if (enemy.state !== 'dead') {
        items.push({ depth: enemy.position.x + enemy.position.y, draw: () => this.renderEnemy(enemy, node) })
      }
    }
    for (const projectile of this.projectiles) {
      items.push({
        depth: projectile.position.x + projectile.position.y,
        draw: () => this.renderProjectile(projectile, node),
      })
    }
    items.push({
      depth: this.player.position.x + this.player.position.y,
      draw: () => this.renderPlayer(node),
    })
    items.sort((a, b) => a.depth - b.depth).forEach(item => item.draw())
    for (const effect of this.effects) this.renderEffect(effect, node)
  }

  private renderFloor(node: LastChancesPlanNode): void {
    const context = this.context
    const corners = [
      this.worldToScreen({ x: 0, y: 0 }, node),
      this.worldToScreen({ x: node.arena.width, y: 0 }, node),
      this.worldToScreen({ x: node.arena.width, y: node.arena.height }, node),
      this.worldToScreen({ x: 0, y: node.arena.height }, node),
    ]
    context.beginPath()
    corners.forEach((corner, index) => index === 0 ? context.moveTo(corner.x, corner.y) : context.lineTo(corner.x, corner.y))
    context.closePath()
    context.fillStyle = this.config.renderer.floor
    context.fill()
    context.strokeStyle = this.currentNode?.accent ?? 'rgba(255,255,255,.2)'
    context.lineWidth = 2
    context.stroke()

    context.strokeStyle = this.config.renderer.floorGrid
    context.lineWidth = 1
    const grid = this.config.renderer.floorGridSize
    for (let x = grid; x < node.arena.width; x += grid) {
      const start = this.worldToScreen({ x, y: 0 }, node)
      const end = this.worldToScreen({ x, y: node.arena.height }, node)
      context.beginPath()
      context.moveTo(start.x, start.y)
      context.lineTo(end.x, end.y)
      context.stroke()
    }
    for (let y = grid; y < node.arena.height; y += grid) {
      const start = this.worldToScreen({ x: 0, y }, node)
      const end = this.worldToScreen({ x: node.arena.width, y }, node)
      context.beginPath()
      context.moveTo(start.x, start.y)
      context.lineTo(end.x, end.y)
      context.stroke()
    }
  }

  private renderObstacle(obstacle: LastChancesObstacleDefinition, node: LastChancesPlanNode): void {
    const context = this.context
    const base = [
      this.worldToScreen({ x: obstacle.x, y: obstacle.y }, node),
      this.worldToScreen({ x: obstacle.x + obstacle.width, y: obstacle.y }, node),
      this.worldToScreen({ x: obstacle.x + obstacle.width, y: obstacle.y + obstacle.height }, node),
      this.worldToScreen({ x: obstacle.x, y: obstacle.y + obstacle.height }, node),
    ]
    const elevation = obstacle.elevation * this.layout().diamondHeight / node.arena.height * 0.72
    const top = base.map(point => ({ x: point.x, y: point.y - elevation }))
    const polygon = (points: LastChancesVector[], fill: string): void => {
      context.beginPath()
      points.forEach((point, index) => index === 0 ? context.moveTo(point.x, point.y) : context.lineTo(point.x, point.y))
      context.closePath()
      context.fillStyle = fill
      context.fill()
    }
    polygon([top[1], base[1], base[2], top[2]], this.config.renderer.obstacleSide)
    polygon([top[2], base[2], base[3], top[3]], 'rgba(25, 22, 32, .96)')
    polygon(top, this.config.renderer.obstacleTop)
    context.strokeStyle = 'rgba(255,255,255,.08)'
    context.stroke()
  }

  private renderVision(enemy: RuntimeEnemy, node: LastChancesPlanNode): void {
    if (enemy.state === 'dead' || enemy.state === 'chasing' || enemy.state === 'attacking') return
    const context = this.context
    const origin = this.worldToScreen(enemy.position, node)
    const facingAngle = Math.atan2(enemy.facing.y, enemy.facing.x)
    const halfArc = enemy.definition.visionAngleDegrees * Math.PI / 360
    context.beginPath()
    context.moveTo(origin.x, origin.y)
    const steps = 16
    for (let step = 0; step <= steps; step += 1) {
      const angle = facingAngle - halfArc + (halfArc * 2 * step) / steps
      const world = {
        x: enemy.position.x + Math.cos(angle) * enemy.definition.visionRange,
        y: enemy.position.y + Math.sin(angle) * enemy.definition.visionRange,
      }
      const point = this.worldToScreen(world, node)
      context.lineTo(point.x, point.y)
    }
    context.closePath()
    context.fillStyle = enemy.state === 'alerted'
      ? 'rgba(255, 57, 67, .22)'
      : enemy.state === 'noticing' ? 'rgba(255, 91, 84, .16)' : 'rgba(237, 214, 144, .055)'
    context.fill()
  }

  private entityScale(node: LastChancesPlanNode): number {
    return this.layout().diamondWidth / (node.arena.width + node.arena.height)
  }

  private renderEnemy(enemy: RuntimeEnemy, node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(enemy.position, node)
    const radius = Math.max(7, enemy.definition.radius * this.entityScale(node) * 1.45)
    context.save()
    context.translate(point.x, point.y)
    context.scale(1, 0.46)
    context.beginPath()
    context.arc(0, 4, radius * 1.05, 0, Math.PI * 2)
    context.fillStyle = 'rgba(0,0,0,.38)'
    context.fill()
    context.restore()
    context.beginPath()
    context.arc(point.x, point.y - radius * 0.8, radius, 0, Math.PI * 2)
    context.fillStyle = enemy.definition.color
    context.fill()
    context.strokeStyle = enemy.state === 'attacking' ? '#ff4b4b' : 'rgba(255,255,255,.3)'
    context.lineWidth = enemy.state === 'attacking' ? 3 : 1
    context.stroke()

    const barWidth = radius * 2.5
    context.fillStyle = 'rgba(0,0,0,.7)'
    context.fillRect(point.x - barWidth / 2, point.y - radius * 2.25, barWidth, 3)
    context.fillStyle = '#cf4a55'
    context.fillRect(point.x - barWidth / 2, point.y - radius * 2.25, barWidth * enemy.hp / enemy.definition.maxHp, 3)
    if (enemy.state === 'noticing' || enemy.state === 'alerted') {
      context.font = `700 ${Math.max(16, radius * 1.4)}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = enemy.state === 'alerted' ? '#ff5964' : '#ffd36a'
      context.fillText(enemy.state === 'alerted' ? '!!' : '!', point.x, point.y - radius * 2.65)
    }
  }

  private renderPlayer(node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(this.player.position, node)
    const radius = Math.max(8, this.config.player.radius * this.entityScale(node) * 1.55)
    context.save()
    context.translate(point.x, point.y)
    context.scale(1, 0.44)
    context.beginPath()
    context.arc(0, 4, radius * 1.2, 0, Math.PI * 2)
    context.fillStyle = 'rgba(0,0,0,.42)'
    context.fill()
    context.restore()
    context.beginPath()
    context.arc(point.x, point.y - radius, radius, 0, Math.PI * 2)
    context.fillStyle = this.config.renderer.player
    context.fill()
    context.strokeStyle = this.player.invulnerableMs > 0 ? '#ffffff' : this.config.renderer.playerAccent
    context.lineWidth = 3
    context.stroke()
    const aimEnd = this.worldToScreen({
      x: this.player.position.x + this.player.aim.x * 74,
      y: this.player.position.y + this.player.aim.y * 74,
    }, node)
    context.beginPath()
    context.moveTo(point.x, point.y - radius)
    context.lineTo(aimEnd.x, aimEnd.y - radius)
    context.strokeStyle = this.config.renderer.playerAccent
    context.lineWidth = 2
    context.stroke()
  }

  private renderProjectile(projectile: RuntimeProjectile, node: LastChancesPlanNode): void {
    const point = this.worldToScreen(projectile.position, node)
    const radius = Math.max(3, projectile.radius * this.entityScale(node) * 1.6)
    this.context.beginPath()
    this.context.arc(point.x, point.y - radius, radius, 0, Math.PI * 2)
    this.context.fillStyle = projectile.color
    this.context.shadowColor = projectile.color
    this.context.shadowBlur = 12
    this.context.fill()
    this.context.shadowBlur = 0
  }

  private renderEffect(effect: RuntimeEffect, node: LastChancesPlanNode): void {
    const context = this.context
    const progress = 1 - effect.remainingMs / effect.totalMs
    const alpha = clamp(1 - progress, 0, 1)
    const origin = this.worldToScreen(effect.position, node)
    const end = this.worldToScreen({
      x: effect.position.x + effect.direction.x * effect.range,
      y: effect.position.y + effect.direction.y * effect.range,
    }, node)
    context.save()
    context.globalAlpha = alpha
    context.strokeStyle = effect.color
    context.fillStyle = effect.color
    context.lineWidth = 2 + (1 - progress) * 5
    if (effect.kind === 'burst') {
      const worldRadius = effect.range * progress
      if (effect.arcDegrees >= 360) {
        const screenRadius = Math.max(2, worldRadius * this.entityScale(node))
        context.beginPath()
        context.ellipse(origin.x, origin.y, screenRadius * 1.8, screenRadius * 0.8, 0, 0, Math.PI * 2)
        context.stroke()
      } else {
        const facing = Math.atan2(effect.direction.y, effect.direction.x)
        const halfArc = effect.arcDegrees * Math.PI / 360
        context.beginPath()
        for (let step = 0; step <= 18; step += 1) {
          const angle = facing - halfArc + (halfArc * 2 * step) / 18
          const point = this.worldToScreen({
            x: effect.position.x + Math.cos(angle) * worldRadius,
            y: effect.position.y + Math.sin(angle) * worldRadius,
          }, node)
          if (step === 0) context.moveTo(point.x, point.y)
          else context.lineTo(point.x, point.y)
        }
        context.stroke()
      }
    } else {
      context.beginPath()
      context.moveTo(origin.x, origin.y)
      context.lineTo(end.x, end.y)
      context.stroke()
    }
    context.restore()
  }

  private renderEmptyPlan(): void {
    const context = this.context
    const layout = this.layout()
    const corners = [
      { x: layout.centerX, y: layout.top },
      { x: layout.centerX + layout.diamondWidth / 2, y: layout.top + layout.diamondHeight / 2 },
      { x: layout.centerX, y: layout.top + layout.diamondHeight },
      { x: layout.centerX - layout.diamondWidth / 2, y: layout.top + layout.diamondHeight / 2 },
    ]
    context.beginPath()
    corners.forEach((point, index) => index === 0 ? context.moveTo(point.x, point.y) : context.lineTo(point.x, point.y))
    context.closePath()
    context.fillStyle = this.config.renderer.floor
    context.fill()
    context.strokeStyle = 'rgba(255,255,255,.14)'
    context.stroke()
  }

  private renderHud(): void {
    const context = this.context
    context.textAlign = 'left'
    context.fillStyle = 'rgba(255,255,255,.9)'
    context.font = '700 15px system-ui'
    context.fillText(`${this.chances} CHANCES`, 22, 30)
    context.font = '500 12px system-ui'
    context.fillStyle = 'rgba(255,255,255,.65)'
    context.fillText(`HP ${Math.ceil(this.player.hp)} / ${Math.ceil(this.player.stats.maxHp)}`, 22, 50)
    context.fillStyle = this.config.renderer.mental
    context.fillText(
      `MIND ${Math.ceil(this.player.mentalHealth)} / ${Math.ceil(this.player.stats.maxMentalHealth)}`,
      22,
      68,
    )
    context.fillStyle = 'rgba(255,255,255,.54)'
    context.fillText(`ATK ${Math.round(this.player.stats.attackPower)}%`, 22, 86)
    if (this.currentNode) {
      context.textAlign = 'right'
      context.fillStyle = 'rgba(255,255,255,.66)'
      context.fillText(`TIER ${this.currentNode.tierIndex + 1} · ${this.currentNode.roomName}`, this.cssWidth - 22, 30)
    }
  }

  private renderOverlay(): void {
    if (this.phase === 'playing' && !this.paused) return
    const context = this.context
    const centerY = this.cssHeight * 0.48
    context.fillStyle = 'rgba(4, 3, 8, .52)'
    context.fillRect(0, centerY - 62, this.cssWidth, 124)
    context.textAlign = 'center'
    context.fillStyle = '#f4edf5'
    context.font = `700 ${clamp(this.cssWidth * 0.035, 20, 34)}px system-ui`
    let title = 'CHOOSE THE NEXT ROOM'
    let subtitle = `${this.availableNodeIds.length} deterministic paths available`
    if (this.paused) {
      title = 'PAUSED'
      subtitle = 'The room is holding its breath'
    } else if (this.phase === 'dead') {
      title = 'ANOTHER CHANCE'
      subtitle = this.deathReason ?? 'The attempt ended'
    } else if (this.phase === 'outOfChances') {
      title = 'NO CHANCES REMAIN'
      subtitle = this.deathReason ?? 'Begin a new generation'
    } else if (this.phase === 'won') {
      title = 'THE TERMINAL TIER IS CLEARED'
      subtitle = 'The Curator is watching the next generation'
    }
    context.fillText(title, this.cssWidth / 2, centerY - 8)
    context.font = '500 13px system-ui'
    context.fillStyle = 'rgba(255,255,255,.64)'
    context.fillText(subtitle, this.cssWidth / 2, centerY + 22)
  }
}
