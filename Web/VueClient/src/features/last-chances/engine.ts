import {
  cloneLastChancesConfig,
  LastChancesConfigError,
  migrateLastChancesConfig,
  validateLastChancesConfig,
} from './config'
import {
  colliderHitsCircle,
  colliderHitsSweptCircle,
  colliderTracePath,
  resolveAttackCollider,
  type LastChancesRuntimeCollider,
} from './colliders'
import { resolveLastChancesLoadout } from './equipment'
import { LastChancesGestureRecognizer } from './gestures'
import {
  createLastChancesGamepadAdapter,
  type LastChancesGamepadAdapter,
  type LastChancesGamepadReading,
} from './gamepad'
import { buildLastChancesPlan } from './plan'
import { createLastChancesRng } from './rng'
import {
  applyLastChancesStatusEffects,
  captureLastChancesDot,
  consumeLastChancesBleed,
  createLastChancesStatuses,
  refreshLastChancesBleed,
  spreadLastChancesDot,
  updateLastChancesStatuses,
  type LastChancesRuntimeStatuses,
  type LastChancesStoredDot,
} from './statuses'
import { LAST_CHANCES_GESTURES, LAST_CHANCES_HANDS } from './types'
import {
  attackWithLastChancesAugment,
  LAST_CHANCES_GESTURE_COLORS,
  resolveLastChancesChargedAttack,
} from './weapon-runtime'
import type {
  LastChancesAttackDefinition,
  LastChancesAttackBehavior,
  LastChancesAugment,
  LastChancesConfig,
  LastChancesCooldownSnapshot,
  LastChancesEnemyAttackKind,
  LastChancesEnemyBossPhaseDefinition,
  LastChancesEnemyDefinition,
  LastChancesEnemyRole,
  LastChancesEnemySnapshot,
  LastChancesEnemyState,
  LastChancesEngineCallbacks,
  LastChancesGamePlan,
  LastChancesGamepadSnapshot,
  LastChancesGesture,
  LastChancesGestureResolution,
  LastChancesGestureSnapshot,
  LastChancesHand,
  LastChancesHitEffectDefinition,
  LastChancesHazardDefinition,
  LastChancesInteractionChoice,
  LastChancesInteractionSnapshot,
  LastChancesLoadoutDefinition,
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
  rootMs: number
  recoveryMs: number
  parryMs: number
  armorMultiplier: number
  armorMultiplierMs: number
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
  lockedAttackDirection: LastChancesVector | null
  leapRemainingDistance: number
  leapSpeed: number
  leapHit: boolean
  revealedMs: number
  captureWindowMs: number
  criticalHitMs: number
  statuses: LastChancesRuntimeStatuses
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
  source: 'player' | 'enemy'
  sourceName: string
  attack?: LastChancesAttackDefinition
  weaponId?: string
  hand?: LastChancesHand
  carriedIds?: Set<string>
  storedDot?: LastChancesStoredDot | null
}

interface RuntimeDash {
  origin: LastChancesVector
  direction: LastChancesVector
  remainingDistance: number
  speed: number
  damage: number
  radius: number
  knockback: number
  hitIds: Set<string>
  hitRecords: Map<string, { lastAtMs: number, hits: number }>
  remainingHits: number
  color: string
  attack: LastChancesAttackDefinition
  weaponId: string
  hand: LastChancesHand
  storedDot: LastChancesStoredDot | null
  landingBurst: boolean
  trailAccumulatorMs: number
  elapsedMs: number
}

interface RuntimeActiveArea {
  kind: 'melee' | 'burst'
  origin: LastChancesVector
  direction: LastChancesVector
  attack: LastChancesAttackDefinition
  baseDamage: number
  weaponId: string
  hand: LastChancesHand
  remainingMs: number
  totalMs: number
  hitIds: Map<string, { lastAtMs: number, hits: number }>
  remainingHits: number
  storedDot: LastChancesStoredDot | null
  sweepDegrees: number
  traceAccumulatorMs: number
  channel: boolean
  authoredRepeatHits: number
  baseRepeatIntervalMs: number
  rotationAssisted: boolean
  latchedIds: Set<string>
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

interface RuntimeColliderTrace {
  collider: LastChancesRuntimeCollider
  color: string
  remainingMs: number
  totalMs: number
}

interface RuntimeTapCombo {
  step: number
  expiresAtMs: number
}

interface RuntimeDelayedAttack {
  remainingMs: number
  attack: LastChancesAttackDefinition
  direction: LastChancesVector
  context: AttackExecutionContext
}

interface RuntimeDelayedRecovery {
  weaponId: string
  remainingMs: number
  recoveryMs: number
}

interface RuntimeProvisionalParry {
  hand: LastChancesHand
  weaponId: string
  attack: LastChancesAttackDefinition
  consumed: boolean
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
  basicHits: number
  spinInertiaDirection: LastChancesVector | null
}

interface AttackExecutionContext {
  weapon: LastChancesResolvedWeapon
  hand: LastChancesHand
  gesture: LastChancesGesture
  resolution: LastChancesGestureResolution
  comboStep?: number
  chargeBandId?: string
  storedDot: LastChancesStoredDot | null
}

interface DamageEnemyOptions {
  weaponId?: string
  hand?: LastChancesHand
  storedDot?: LastChancesStoredDot | null
  distance?: number
}

interface RuntimeEnemyCombatProfile {
  phaseName: string | null
  role: LastChancesEnemyRole
  attackKind: LastChancesEnemyAttackKind
  attackRange: number
  attackRadius: number
  attackDamage: number
  attackCooldownMs: number
  attackWindupMs: number
  projectileSpeed: number
  leapDistance: number
  leapDurationMs: number
  targetLockMs: number
  parryWindowMs: number
}

interface IsometricLayout {
  centerX: number
  top: number
  diamondWidth: number
  diamondHeight: number
}

const MOVEMENT_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowLeft', 'ArrowDown', 'ArrowRight'])
const PLAYER_PARRY_BEHAVIORS = new Set<LastChancesAttackBehavior>([
  'parry',
  'chainStrike',
  'axeParry',
  'katanaParry',
])
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

function rotateVector(value: LastChancesVector, radians: number): LastChancesVector {
  const cosine = Math.cos(radians)
  const sine = Math.sin(radians)
  return {
    x: value.x * cosine - value.y * sine,
    y: value.x * sine + value.y * cosine,
  }
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

function tuningValue(
  source: { tuning?: Record<string, number> } | undefined,
  key: string,
  fallback: number,
): number {
  const value = source?.tuning?.[key]
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
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
  radius = 0,
): boolean {
  const direction = { x: end.x - start.x, y: end.y - start.y }
  let minimum = 0
  let maximum = 1
  for (const axis of ['x', 'y'] as const) {
    const origin = start[axis]
    const delta = direction[axis]
    const low = obstacle[axis] - radius
    const high = obstacle[axis] + (axis === 'x' ? obstacle.width : obstacle.height) + radius
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
  private readonly weapons = new Map<LastChancesHand, LastChancesResolvedWeapon>()
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
  private totalDeaths = 0
  private generationBaseStats: LastChancesStats
  private activeLoadout: LastChancesLoadoutDefinition | null
  private corpseBoundPrimaryWeaponId: string | null = null
  private currentNode: LastChancesPlanNode | null = null
  private availableNodeIds: string[] = []
  private attemptPath: string[] = []
  private deathReason: string | null = null
  private enemies: RuntimeEnemy[] = []
  private projectiles: RuntimeProjectile[] = []
  private activeAreas: RuntimeActiveArea[] = []
  private effects: RuntimeEffect[] = []
  private traces: RuntimeColliderTrace[] = []
  private delayedAttacks: RuntimeDelayedAttack[] = []
  private delayedRecoveries: RuntimeDelayedRecovery[] = []
  private readonly weaponStates = new Map<string, RuntimeWeaponState>()
  private readonly heldChannels = new Map<LastChancesHand, RuntimeActiveArea>()
  private readonly weaponActionEnds = new Map<string, number>()
  private activeParryCollider: LastChancesRuntimeCollider | null = null
  private provisionalParry: RuntimeProvisionalParry | null = null
  private roomElapsedMs = 0
  private readonly hazardHitCycles = new Map<string, number>()
  private readonly hazardSuppressedUntil = new Map<string, number>()
  private interactionResolved = false
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
  private frameNowMs = 0
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
    this.activeLoadout = this.config.loadout ? { ...this.config.loadout } : null
    this.rebuildWeapons()
    this.plan = buildLastChancesPlan(this.config, this.generation)
    const baseStats = copyStats(this.config.player.baseStats)
    this.generationBaseStats = copyStats(baseStats)
    this.player = {
      position: { x: 0, y: 0 },
      aim: { x: 1, y: 0 },
      hp: baseStats.maxHp,
      mentalHealth: baseStats.maxMentalHealth,
      stats: baseStats,
      invulnerableMs: 0,
      rootMs: 0,
      recoveryMs: 0,
      parryMs: 0,
      armorMultiplier: 1,
      armorMultiplierMs: 0,
    }
    this.gestures = new LastChancesGestureRecognizer(this.config.input, (resolution) => {
      this.performAttack(resolution)
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
    this.heldChannels.clear()
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.provisionalParry = null
  }

  setPaused(paused: boolean): void {
    if (this.paused === paused || this.destroyed) return
    if (paused) {
      this.commitHeldChannels()
      this.commitOrCancelProvisionalParry()
    }
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
    this.clearCombatTransients()
    this.projectiles = []
    this.activeAreas = []
    this.effects = []
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.provisionalParry = null
    this.heldChannels.clear()
    this.activeDash = null
    this.roomElapsedMs = 0
    this.hazardHitCycles.clear()
    this.hazardSuppressedUntil.clear()
    this.interactionResolved = false
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
        lockedAttackDirection: null,
        leapRemainingDistance: 0,
        leapSpeed: 0,
        leapHit: false,
        revealedMs: 0,
        captureWindowMs: 0,
        criticalHitMs: 0,
        statuses: createLastChancesStatuses(),
      }
    })
    this.phase = 'playing'
    this.deathReason = null
    if (this.enemies.length === 0) this.completeRoom()
    this.render()
    this.emitSnapshot(true)
    return true
  }

  chooseInteraction(choiceId: string): boolean {
    if (this.phase !== 'interaction' || !this.currentNode?.interaction || this.interactionResolved) return false
    const choice = this.currentNode.interaction.choices.find(candidate => candidate.id === choiceId)
    if (!choice || !this.interactionChoiceAvailable(choice)) return false
    this.applyInteractionChoice(choice)
    this.interactionResolved = true
    this.finishRoomTransition()
    this.render()
    this.emitSnapshot(true)
    return true
  }

  interact(): boolean {
    if (this.phase !== 'playing' || this.paused || !this.activeLoadout) return false
    const spider = this.capturableKnifeSpider()
    if (!spider) return false
    const candidateConfig = cloneLastChancesConfig(this.config)
    candidateConfig.loadout = this.normalizeLoadoutAugments({
      ...this.activeLoadout,
      secondaryWeaponId: 'secondary-spider-knife',
    })
    const resolved = resolveLastChancesLoadout(candidateConfig)
    if (resolved.right?.id !== 'secondary-spider-knife') return false
    spider.state = 'dead'
    this.activeLoadout = this.normalizeLoadoutAugments({
      ...this.activeLoadout,
      secondaryWeaponId: 'secondary-spider-knife',
    })
    this.weaponStates.delete('secondary-spider-knife')
    this.rebuildWeapons()
    this.cooldownEnds.clear()
    this.resetTapCombos()
    this.lastGesture = {
      hand: 'right',
      gesture: 'tap',
      attackName: 'Нож-паук схвачен со спины',
      atMs: this.elapsedMs,
    }
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
    this.totalDeaths = 0
    this.corpseBoundPrimaryWeaponId = null
    this.elapsedMs = 0
    this.lastSnapshotAt = Number.NEGATIVE_INFINITY
    this.nextProjectileId = 1
    this.generationBaseStats = copyStats(this.config.player.baseStats)
    this.player.stats = copyStats(this.generationBaseStats)
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
    const now = performance.now()
    const input = this.gestures.snapshot(hand, now)
    if (this.phase === 'playing'
      && !this.paused
      && input.pressed
      && input.sequence === 'first'
      && input.heldMs < this.config.input.holdMs) {
      this.beginProvisionalTapParry(hand)
    }
    this.gestures.release(hand, now)
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
    this.frameNowMs = frameMs
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
    this.roomElapsedMs += deltaMs
    this.player.invulnerableMs = Math.max(0, this.player.invulnerableMs - deltaMs)
    this.player.rootMs = Math.max(0, this.player.rootMs - deltaMs)
    this.player.recoveryMs = Math.max(0, this.player.recoveryMs - deltaMs)
    this.player.parryMs = Math.max(0, this.player.parryMs - deltaMs)
    if (this.player.parryMs <= 0) this.activeParryCollider = null
    this.player.armorMultiplierMs = Math.max(0, this.player.armorMultiplierMs - deltaMs)
    if (this.player.armorMultiplierMs <= 0) this.player.armorMultiplier = 1
    for (const state of this.weaponStates.values()) {
      state.recoveryMs = Math.max(0, state.recoveryMs - deltaMs)
    }
    this.updateHeldWeaponMechanics(deltaMs)
    this.updateDelayedAttacks(deltaMs)
    this.updateDelayedRecoveries(deltaMs)
    this.updatePlayer(deltaSeconds)
    this.updateProjectiles(deltaSeconds, deltaMs)
    this.updateEnemies(deltaSeconds, deltaMs)
    this.updateActiveAreas(deltaMs)
    this.updateHazards(deltaSeconds)
    this.updateMentalHealth(deltaSeconds)
    this.effects.forEach(effect => effect.remainingMs -= deltaMs)
    this.effects = this.effects.filter(effect => effect.remainingMs > 0)
    this.traces.forEach(trace => { trace.remainingMs -= deltaMs })
    this.traces = this.traces.filter(trace => trace.remainingMs > 0)
    if (this.phase === 'playing' && this.enemies.every(enemy => enemy.state === 'dead')) this.completeRoom()
  }

  private updateDelayedAttacks(deltaMs: number): void {
    const ready: RuntimeDelayedAttack[] = []
    for (const delayed of this.delayedAttacks) {
      delayed.remainingMs = Math.max(0, delayed.remainingMs - deltaMs)
      if (delayed.remainingMs <= 0) ready.push(delayed)
    }
    this.delayedAttacks = this.delayedAttacks.filter(delayed => delayed.remainingMs > 0)
    if (this.phase !== 'playing') return
    for (const delayed of ready) {
      this.executeAttack(delayed.attack, delayed.direction, delayed.context)
    }
  }

  private updateDelayedRecoveries(deltaMs: number): void {
    const ready: RuntimeDelayedRecovery[] = []
    for (const delayed of this.delayedRecoveries) {
      delayed.remainingMs = Math.max(0, delayed.remainingMs - deltaMs)
      if (delayed.remainingMs <= 0) ready.push(delayed)
    }
    this.delayedRecoveries = this.delayedRecoveries.filter(delayed => delayed.remainingMs > 0)
    for (const delayed of ready) this.beginRecovery(delayed.weaponId, delayed.recoveryMs)
  }

  private beginRecovery(weaponId: string, recoveryMs: number): void {
    if (recoveryMs <= 0) return
    const state = this.weaponStates.get(weaponId)
    if (state) state.recoveryMs = Math.max(state.recoveryMs, recoveryMs)
    this.player.recoveryMs = Math.max(this.player.recoveryMs, recoveryMs)
  }

  private scheduleRecovery(
    weaponId: string,
    recoveryMs: number | undefined,
    delayMs: number,
  ): void {
    if (!recoveryMs || recoveryMs <= 0) return
    if (delayMs <= 0) {
      this.beginRecovery(weaponId, recoveryMs)
      return
    }
    this.delayedRecoveries.push({
      weaponId,
      remainingMs: delayMs,
      recoveryMs,
    })
  }

  private updatePlayer(deltaSeconds: number): void {
    const aim = this.resolveAim()
    if (vectorLength(aim) > this.config.input.aimDeadZone) this.player.aim = normalize(aim, this.player.aim)
    if (this.activeDash) {
      const deltaMs = deltaSeconds * 1000
      this.activeDash.trailAccumulatorMs += deltaMs
      this.activeDash.elapsedMs += deltaMs
      const dashStartDelayMs = tuningValue(this.activeDash.attack, 'dashStartDelayMs', 0)
      const travel = this.activeDash.elapsedMs > dashStartDelayMs
        ? Math.min(this.activeDash.remainingDistance, this.activeDash.speed * deltaSeconds)
        : 0
      const dashStart = { ...this.player.position }
      this.moveCircle(this.player.position, {
        x: this.activeDash.direction.x * travel,
        y: this.activeDash.direction.y * travel,
      }, this.config.player.radius)
      const dashColliders = this.dashStepColliders(
        this.activeDash,
        dashStart,
        this.player.position,
        deltaMs,
      )
      dashColliders.forEach(collider => this.addColliderTrace(collider, this.activeDash!.attack))
      const chemicalEffects = (this.activeDash.attack.hitEffects ?? [])
        .filter(effect => effect.status === 'chemical')
      const chemicalTrailIntervalMs = tuningValue(
        this.activeDash.attack,
        'chemicalTrailIntervalMs',
        90,
      )
      if (chemicalEffects.length > 0
        && this.activeDash.trailAccumulatorMs >= chemicalTrailIntervalMs) {
        this.activeDash.trailAccumulatorMs = 0
        this.startActiveArea('burst', {
          ...this.activeDash.attack,
          name: `${this.activeDash.attack.name} · химический след`,
          kind: 'burst',
          behavior: 'standard',
          damage: 0,
          range: Math.max(
            tuningValue(this.activeDash.attack, 'chemicalTrailMinimumRadius', 28),
            this.activeDash.attack.radius
              * tuningValue(this.activeDash.attack, 'chemicalTrailRadiusMultiplier', 1.8),
          ),
          radius: tuningValue(this.activeDash.attack, 'chemicalTrailColliderRadius', 8),
          arcDegrees: 360,
          durationMs: tuningValue(this.activeDash.attack, 'chemicalTrailDurationMs', 900),
          lingerMs: tuningValue(this.activeDash.attack, 'chemicalTrailLingerMs', 500),
          pierce: 20,
          knockback: 0,
          collider: {
            shape: 'circle',
            traceMs: 850,
            followsPlayer: false,
          },
          hitEffects: chemicalEffects,
        }, this.activeDash.direction, this.activeDash.weaponId, this.activeDash.hand)
      }
      this.activeDash.remainingDistance -= travel
      for (const enemy of this.enemies) {
        if (this.activeDash.remainingHits <= 0) break
        if (enemy.state === 'dead') continue
        const isFlurry = this.activeDash.attack.behavior === 'katanaFlurry'
        const previous = this.activeDash.hitRecords.get(enemy.id)
        const repeatHits = Math.max(1, this.activeDash.attack.repeatHits ?? 1)
        const repeatInterval = Math.max(1, this.activeDash.attack.repeatIntervalMs ?? 120)
        if (isFlurry) {
          if (previous && (previous.hits >= repeatHits
            || this.activeDash.elapsedMs - previous.lastAtMs < repeatInterval)) continue
        } else if (this.activeDash.hitIds.has(enemy.id)) {
          continue
        }
        if (!dashColliders.some(collider => (
          colliderHitsCircle(collider, enemy.position, enemy.definition.radius)
        ))) continue
        const nextHit = (previous?.hits ?? 0) + 1
        this.activeDash.hitRecords.set(enemy.id, {
          lastAtMs: this.activeDash.elapsedMs,
          hits: nextHit,
        })
        if (!isFlurry) this.activeDash.hitIds.add(enemy.id)
        this.activeDash.remainingHits -= 1
        if (isFlurry
          && (enemy.definition.dodge ?? 0)
            >= tuningValue(this.activeDash.attack, 'dodgeThreshold', 0.25)
          && nextHit % 2 === 0) continue
        this.tryParryEnemy(enemy, this.activeDash.attack)
        this.damageEnemy(
          enemy,
          this.activeDash.attack,
          this.activeDash.knockback,
          this.activeDash.direction,
          {
            weaponId: this.activeDash.weaponId,
            hand: this.activeDash.hand,
            storedDot: this.activeDash.storedDot,
          },
        )
      }
      if (this.activeDash.remainingDistance <= EPSILON) {
        const finishedDash = this.activeDash
        this.activeDash = null
        if (finishedDash.landingBurst) {
          const clawScratch = finishedDash.attack.behavior === 'clawDash'
          const landingAttack = {
            ...finishedDash.attack,
            name: `${finishedDash.attack.name} · ${clawScratch ? 'финальная царапина' : 'приземление'}`,
            kind: (clawScratch ? 'melee' : 'burst') as 'melee' | 'burst',
            damage: tuningValue(
              finishedDash.attack,
              'landingDamage',
              finishedDash.attack.damage,
            ),
            range: tuningValue(finishedDash.attack, 'landingRange', clawScratch ? 82 : 85),
            radius: tuningValue(finishedDash.attack, 'landingColliderRadius', 24),
            arcDegrees: tuningValue(
              finishedDash.attack,
              'landingArcDegrees',
              clawScratch ? 110 : 360,
            ),
            durationMs: tuningValue(finishedDash.attack, 'landingDurationMs', 220),
            knockback: tuningValue(
              finishedDash.attack,
              'landingKnockback',
              finishedDash.attack.knockback,
            ),
            collider: {
              shape: (clawScratch ? 'sector' : 'circle') as 'sector' | 'circle',
              traceMs: tuningValue(finishedDash.attack, 'landingTraceMs', 1100),
              followsPlayer: false,
            },
            behavior: 'standard' as const,
          }
          this.startActiveArea(
            clawScratch ? 'melee' : 'burst',
            landingAttack,
            finishedDash.direction,
            finishedDash.weaponId,
            finishedDash.hand,
            finishedDash.storedDot,
          )
        }
      }
      return
    }

    const movement = this.player.rootMs > 0 || this.player.recoveryMs > 0
      ? { x: 0, y: 0 }
      : this.resolveMovement()
    this.moveCircle(this.player.position, {
      x: movement.x * this.player.stats.moveSpeed * deltaSeconds,
      y: movement.y * this.player.stats.moveSpeed * deltaSeconds,
    }, this.config.player.radius)
  }

  /**
   * Resolves the exact damaging volume swept during one dash step. Collision
   * and the fading trace consume this same list, so neither can lead the other.
   */
  private dashStepColliders(
    dash: RuntimeDash,
    previousPosition: LastChancesVector,
    currentPosition: LastChancesVector,
    deltaMs: number,
  ): LastChancesRuntimeCollider[] {
    const definition = dash.attack.collider
    if (definition?.shape === 'sweep') {
      const authoredRotation = definition.rotationDegrees ?? 0
      const totalMs = Math.max(1, dash.attack.durationMs)
      const currentProgress = clamp(dash.elapsedMs / totalMs, 0, 1)
      const previousProgress = clamp((dash.elapsedMs - deltaMs) / totalMs, 0, 1)
      const previous = resolveAttackCollider(
        previousPosition,
        dash.direction,
        dash.attack,
        1,
        -authoredRotation * (1 - previousProgress),
      )
      const current = resolveAttackCollider(
        currentPosition,
        dash.direction,
        dash.attack,
        1,
        -authoredRotation * (1 - currentProgress),
      )
      if (previous.shape !== 'sweep' || current.shape !== 'sweep') return [current]
      return [
        previous,
        current,
        {
          shape: 'capsule',
          start: previous.start,
          end: current.start,
          radius: Math.max(previous.radius, current.radius),
        },
        {
          shape: 'capsule',
          start: previous.end,
          end: current.end,
          radius: Math.max(previous.radius, current.radius),
        },
      ]
    }

    const direction = normalize(dash.direction)
    const radius = Math.max(
      dash.radius,
      Math.max(0, definition?.width ?? dash.attack.radius * 2) / 2,
    )
    const authoredOffset = Math.max(0, definition?.innerRange ?? 0)
    const offset = tuningValue(dash.attack, 'dashColliderOffset', authoredOffset)
    const dashStartDelayMs = tuningValue(dash.attack, 'dashStartDelayMs', 0)
    const waitingToTravel = dash.elapsedMs <= dashStartDelayMs
    const length = Math.max(
      0,
      waitingToTravel
        ? tuningValue(
            dash.attack,
            'stationaryColliderLength',
            Math.max(radius * 2, dash.attack.radius * 2),
          )
        : tuningValue(
            dash.attack,
            'dashColliderLength',
            Math.max(radius * 2, dash.attack.radius * 2),
          ),
    )
    const backtrack = waitingToTravel
      ? 0
      : Math.max(0, tuningValue(dash.attack, 'dashColliderBacktrack', 0))
    return [{
      shape: 'capsule',
      start: {
        x: previousPosition.x + direction.x * (offset - backtrack),
        y: previousPosition.y + direction.y * (offset - backtrack),
      },
      end: {
        x: currentPosition.x + direction.x * (offset + length),
        y: currentPosition.y + direction.y * (offset + length),
      },
      radius,
      ...(definition?.strictInnerRange && offset > 0
        ? {
            innerExclusion: {
              shape: 'circle' as const,
              center: { ...currentPosition },
              innerRadius: 0,
              outerRadius: offset,
            },
          }
        : {}),
    }]
  }

  private updateProjectiles(deltaSeconds: number, deltaMs: number): void {
    for (const projectile of this.projectiles) {
      const projectileStart = { ...projectile.position }
      const travel = vectorLength(projectile.velocity) * deltaSeconds
      projectile.position.x += projectile.velocity.x * deltaSeconds
      projectile.position.y += projectile.velocity.y * deltaSeconds
      projectile.remainingDistance -= travel
      projectile.remainingMs -= deltaMs
      const sweptCollider: LastChancesRuntimeCollider = {
        shape: 'capsule',
        start: projectileStart,
        end: { ...projectile.position },
        radius: projectile.radius,
      }
      if (projectile.source === 'player' && projectile.attack) {
        this.addColliderTrace(sweptCollider, projectile.attack)
      }
      const arena = this.currentNode?.arena
      const hitObstacle = arena?.obstacles.some(obstacle => (
        segmentHitsObstacle(projectileStart, projectile.position, obstacle, projectile.radius)
      )) ?? false
      const hitBoundary = !!arena && (
        projectile.position.x - projectile.radius <= 0
        || projectile.position.y - projectile.radius <= 0
        || projectile.position.x + projectile.radius >= arena.width
        || projectile.position.y + projectile.radius >= arena.height
      )
      if (hitObstacle || hitBoundary) {
        projectile.position = hitBoundary && arena
          ? {
              x: clamp(projectile.position.x, projectile.radius, arena.width - projectile.radius),
              y: clamp(projectile.position.y, projectile.radius, arena.height - projectile.radius),
            }
          : projectileStart
        if (projectile.carriedIds?.size) this.pinProjectileTargets(projectile)
        projectile.remainingHits = 0
        continue
      }
      if (projectile.carriedIds?.size) {
        for (const enemyId of projectile.carriedIds) {
          const carried = this.enemies.find(enemy => enemy.id === enemyId && enemy.state !== 'dead')
          if (!carried) continue
          carried.position = { ...projectile.position }
          carried.statuses.stunMs = Math.max(carried.statuses.stunMs, 80)
        }
      }
      if (projectile.source === 'enemy') {
        const reflectingSpin = this.activeAreas.find(area => (
          area.attack.behavior === 'axeSpin'
          && colliderHitsSweptCircle(
            this.activeAreaCollider(area),
            projectileStart,
            projectile.position,
            projectile.radius,
          )
        ))
        if (reflectingSpin) {
          projectile.source = 'player'
          projectile.velocity.x *= -1
          projectile.velocity.y *= -1
          projectile.remainingDistance = Math.max(projectile.remainingDistance, 260)
          projectile.remainingHits = Math.max(1, reflectingSpin.attack.pierce + 1)
          projectile.hitIds.clear()
          projectile.sourceName = 'Axe-reflected projectile'
          projectile.attack = { ...reflectingSpin.attack, kind: 'projectile' }
          projectile.weaponId = reflectingSpin.weaponId
          projectile.hand = reflectingSpin.hand
          continue
        }
        if (colliderHitsCircle(sweptCollider, this.player.position, this.config.player.radius)) {
          if (this.playerParryCoversSweep(
            projectileStart,
            projectile.position,
            projectile.radius,
          )) {
            this.consumeActiveParry()
            projectile.source = 'player'
            projectile.velocity.x *= -1
            projectile.velocity.y *= -1
            projectile.remainingDistance = Math.max(projectile.remainingDistance, 220)
            projectile.remainingHits = 1
            projectile.hitIds.clear()
            projectile.sourceName = 'Parried projectile'
            continue
          }
          projectile.remainingHits = 0
          this.damagePlayer(projectile.damage, projectile.sourceName)
        }
        continue
      }
      for (const enemy of this.enemies) {
        if (enemy.state === 'dead' || projectile.hitIds.has(enemy.id)) continue
        if (!colliderHitsCircle(sweptCollider, enemy.position, enemy.definition.radius)) continue
        projectile.hitIds.add(enemy.id)
        projectile.remainingHits -= 1
        const direction = normalize(projectile.velocity)
        const attack = projectile.attack ?? {
          name: projectile.sourceName,
          kind: 'projectile',
          behavior: 'standard',
          damage: projectile.damage,
          cooldownMs: 0,
          range: projectile.remainingDistance,
          radius: projectile.radius,
          arcDegrees: 0,
          durationMs: projectile.remainingMs,
          projectileSpeed: vectorLength(projectile.velocity),
          pierce: projectile.remainingHits,
          knockback: projectile.knockback,
          color: projectile.color,
        }
        this.tryParryEnemy(enemy, attack)
        this.damageEnemy(enemy, attack, projectile.knockback, direction, {
          weaponId: projectile.weaponId,
          hand: projectile.hand,
          storedDot: projectile.storedDot,
        })
        if (attack.behavior === 'spearRelease'
          && projectile.carriedIds
          && enemy.hp > 0) {
          projectile.carriedIds.add(enemy.id)
        }
        if (projectile.remainingHits <= 0) break
      }
    }
    for (const projectile of this.projectiles) {
      if ((projectile.remainingDistance <= 0 || projectile.remainingMs <= 0)
        && projectile.carriedIds?.size) projectile.carriedIds.clear()
    }
    this.projectiles = this.projectiles.filter(projectile => (
      projectile.remainingDistance > 0 && projectile.remainingMs > 0 && projectile.remainingHits > 0
    ))
  }

  private updateEnemies(deltaSeconds: number, deltaMs: number): void {
    let queuedAttackerActive = this.enemies.some((enemy) => {
      return enemy.state === 'attacking' && this.enemyCombatProfile(enemy).role !== 'creep'
    })
    for (const enemy of this.enemies) {
      if (enemy.hp <= 0) continue
      updateLastChancesStatuses(enemy.statuses, deltaMs, amount => {
        enemy.hp = Math.max(0, enemy.hp - amount)
        if (enemy.hp <= 0) this.finishEnemyDeath(enemy)
      })
      if (enemy.state === 'dead') continue
      enemy.revealedMs = Math.max(0, enemy.revealedMs - deltaMs)
      enemy.captureWindowMs = Math.max(0, enemy.captureWindowMs - deltaMs)
      enemy.criticalHitMs = Math.max(0, enemy.criticalHitMs - deltaMs)
      enemy.attackCooldownMs = Math.max(
        0,
        enemy.attackCooldownMs - deltaMs / Math.max(1, enemy.statuses.attackSlowMultiplier),
      )
      if (enemy.captureWindowMs > 0 || enemy.statuses.stunMs > 0) continue
      const toPlayer = {
        x: this.player.position.x - enemy.position.x,
        y: this.player.position.y - enemy.position.y,
      }
      const distance = vectorLength(toPlayer)
      const profile = this.enemyCombatProfile(enemy)

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

      if (enemy.state === 'attacking') {
        if (!(profile.attackKind === 'leap' && enemy.lockedAttackDirection)) {
          enemy.facing = normalize(toPlayer, enemy.facing)
        }
        this.updateEnemyAttack(enemy, profile, deltaSeconds, deltaMs, distance)
        continue
      }

      enemy.facing = normalize(toPlayer, enemy.facing)
      const mayUseQueue = profile.role === 'creep' || !queuedAttackerActive
      if (distance <= profile.attackRange
        && enemy.attackCooldownMs <= 0
        && enemy.statuses.disarmMs <= 0
        && mayUseQueue) {
        this.startEnemyAttack(enemy, profile)
        if (profile.role !== 'creep') queuedAttackerActive = true
        continue
      }
      const desiredDistance = profile.attackRange
        * (enemy.definition.preferredAttackRangeRatio ?? 0.72)
      if (distance > desiredDistance) {
        this.moveCircle(enemy.position, {
          x: enemy.facing.x * enemy.definition.moveSpeed * deltaSeconds,
          y: enemy.facing.y * enemy.definition.moveSpeed * deltaSeconds,
        }, enemy.definition.radius)
        if (enemy.statuses.slowMultiplier < 1) {
          const correction = 1 - enemy.statuses.slowMultiplier
          this.moveCircle(enemy.position, {
            x: -enemy.facing.x * enemy.definition.moveSpeed * deltaSeconds * correction,
            y: -enemy.facing.y * enemy.definition.moveSpeed * deltaSeconds * correction,
          }, enemy.definition.radius)
        }
      }
    }
  }

  private enemyCombatProfile(enemy: RuntimeEnemy): RuntimeEnemyCombatProfile {
    const definition = enemy.definition
    const healthRatio = enemy.hp / Math.max(1, definition.maxHp)
    const phase = definition.bossPhases
      ? [...definition.bossPhases]
          .sort((a, b) => b.minimumHealthRatio - a.minimumHealthRatio)
          .find(candidate => healthRatio >= candidate.minimumHealthRatio)
      : undefined
    const source: Partial<LastChancesEnemyBossPhaseDefinition> = phase ?? {}
    const attackWindupMs = source.attackWindupMs ?? definition.attackWindupMs
    return {
      phaseName: phase?.name ?? null,
      role: definition.role ?? (definition.bossPhases ? 'boss' : 'standard'),
      attackKind: source.attackKind ?? definition.attackKind ?? 'melee',
      attackRange: source.attackRange ?? definition.attackRange,
      attackRadius: source.attackRadius ?? definition.attackRadius ?? 0,
      attackDamage: source.attackDamage ?? definition.attackDamage,
      attackCooldownMs: source.attackCooldownMs ?? definition.attackCooldownMs,
      attackWindupMs,
      projectileSpeed: source.projectileSpeed ?? definition.projectileSpeed ?? 300,
      leapDistance: source.leapDistance ?? definition.leapDistance ?? definition.attackRange,
      leapDurationMs: source.leapDurationMs ?? definition.leapDurationMs ?? 320,
      targetLockMs: source.targetLockMs ?? definition.targetLockMs ?? Math.min(220, attackWindupMs),
      parryWindowMs: source.parryWindowMs ?? definition.parryWindowMs ?? Math.min(180, attackWindupMs),
    }
  }

  private enemyVisible(enemy: RuntimeEnemy): boolean {
    return !enemy.definition.invisibleUntilAlerted
      || enemy.revealedMs > 0
      || enemy.state === 'alerted'
      || enemy.state === 'chasing'
      || enemy.state === 'attacking'
      || enemy.state === 'dead'
  }

  private startEnemyAttack(enemy: RuntimeEnemy, profile: RuntimeEnemyCombatProfile): void {
    enemy.state = 'attacking'
    enemy.attackWindupMs = profile.attackWindupMs
    enemy.lockedAttackDirection = null
    enemy.leapRemainingDistance = 0
    enemy.leapSpeed = 0
    enemy.leapHit = false
  }

  private updateEnemyAttack(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    deltaSeconds: number,
    deltaMs: number,
    distance: number,
  ): void {
    if (profile.attackKind === 'leap' && enemy.leapRemainingDistance > 0) {
      const travel = Math.min(enemy.leapRemainingDistance, enemy.leapSpeed * deltaSeconds)
      const direction = enemy.lockedAttackDirection ?? enemy.facing
      this.moveCircle(enemy.position, { x: direction.x * travel, y: direction.y * travel }, enemy.definition.radius)
      enemy.leapRemainingDistance -= travel
      const hitRange = enemy.definition.radius + this.config.player.radius
      if (!enemy.leapHit && distanceSquared(enemy.position, this.player.position) <= hitRange * hitRange) {
        enemy.leapHit = true
        if (this.parryIncomingEnemy(enemy, profile)) return
        this.damagePlayer(profile.attackDamage, enemy.definition.name)
        if (enemy.definition.id === 'spider-knife') {
          enemy.hp = Math.max(0, enemy.hp - Math.max(
            1,
            profile.attackDamage * tuningValue(enemy.definition, 'selfDamageRatio', 0.18),
          ))
          if (enemy.hp <= 0) this.finishEnemyDeath(enemy)
        }
      }
      if (enemy.leapRemainingDistance <= EPSILON) this.finishEnemyAttack(enemy, profile)
      return
    }

    enemy.attackWindupMs = Math.max(0, enemy.attackWindupMs - deltaMs)
    if (profile.attackKind === 'leap'
      && enemy.attackWindupMs <= profile.targetLockMs
      && !enemy.lockedAttackDirection) {
      enemy.lockedAttackDirection = normalize({
        x: this.player.position.x - enemy.position.x,
        y: this.player.position.y - enemy.position.y,
      }, enemy.facing)
    }
    if (enemy.attackWindupMs > 0) return

    if (profile.attackKind === 'leap') {
      const durationSeconds = Math.max(0.08, profile.leapDurationMs / 1000)
      enemy.lockedAttackDirection ??= enemy.facing
      enemy.leapRemainingDistance = profile.leapDistance
      enemy.leapSpeed = profile.leapDistance / durationSeconds
      return
    }
    if (profile.attackKind === 'projectile') {
      this.spawnEnemyProjectile(enemy, profile)
      this.finishEnemyAttack(enemy, profile)
      return
    }
    const reach = profile.attackRange + this.config.player.radius
      + (profile.attackKind === 'heavy' ? profile.attackRadius : 0)
    if (distance <= reach) {
      if (this.parryIncomingEnemy(enemy, profile)) return
      this.damagePlayer(profile.attackDamage, enemy.definition.name)
      if (enemy.definition.id === 'spider-knife') {
        enemy.hp = Math.max(0, enemy.hp - Math.max(
          1,
          profile.attackDamage * tuningValue(enemy.definition, 'selfDamageRatio', 0.18),
        ))
        if (enemy.hp <= 0) this.finishEnemyDeath(enemy)
      }
    }
    this.finishEnemyAttack(enemy, profile)
  }

  private finishEnemyAttack(enemy: RuntimeEnemy, profile: RuntimeEnemyCombatProfile): void {
    const missedKnifeSpiderLeap = enemy.definition.id === 'spider-knife'
      && profile.attackKind === 'leap'
      && enemy.leapRemainingDistance <= EPSILON
      && !enemy.leapHit
    enemy.attackCooldownMs = profile.attackCooldownMs
    enemy.attackWindupMs = 0
    enemy.lockedAttackDirection = null
    enemy.leapRemainingDistance = 0
    enemy.leapSpeed = 0
    enemy.leapHit = false
    if (enemy.state !== 'dead') {
      enemy.state = 'chasing'
      if (missedKnifeSpiderLeap) {
        const captureWindowMs = tuningValue(enemy.definition, 'captureWindowMs', 1200)
        enemy.captureWindowMs = captureWindowMs
        enemy.statuses.stunMs = Math.max(
          enemy.statuses.stunMs,
          tuningValue(enemy.definition, 'captureStunMs', captureWindowMs),
        )
      }
    }
  }

  private spawnEnemyProjectile(enemy: RuntimeEnemy, profile: RuntimeEnemyCombatProfile): void {
    const direction = normalize({
      x: this.player.position.x - enemy.position.x,
      y: this.player.position.y - enemy.position.y,
    }, enemy.facing)
    const radius = Math.max(5, profile.attackRadius)
    const speed = Math.max(1, profile.projectileSpeed)
    this.projectiles.push({
      id: this.nextProjectileId,
      position: {
        x: enemy.position.x + direction.x * (enemy.definition.radius + radius + 2),
        y: enemy.position.y + direction.y * (enemy.definition.radius + radius + 2),
      },
      velocity: { x: direction.x * speed, y: direction.y * speed },
      radius,
      damage: profile.attackDamage,
      knockback: 0,
      remainingDistance: profile.attackRange,
      remainingMs: profile.attackRange / speed * 1000,
      remainingHits: 1,
      hitIds: new Set(),
      color: enemy.definition.color,
      source: 'enemy',
      sourceName: enemy.definition.name,
    })
    this.nextProjectileId += 1
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

  private hazardActive(hazard: LastChancesHazardDefinition): boolean {
    if ((this.hazardSuppressedUntil.get(hazard.id) ?? 0) > this.elapsedMs) return false
    const phase = (this.roomElapsedMs + hazard.phaseOffsetMs) % hazard.cycleMs
    return phase < hazard.activeMs
  }

  private playerTouchesHazard(hazard: LastChancesHazardDefinition): boolean {
    const nearestX = clamp(this.player.position.x, hazard.x, hazard.x + hazard.width)
    const nearestY = clamp(this.player.position.y, hazard.y, hazard.y + hazard.height)
    return distanceSquared(this.player.position, { x: nearestX, y: nearestY })
      <= this.config.player.radius * this.config.player.radius
  }

  private updateHazards(deltaSeconds: number): void {
    if (!this.currentNode) return
    for (const hazard of this.currentNode.arena.hazards) {
      if (!this.hazardActive(hazard) || !this.playerTouchesHazard(hazard)) continue
      if (hazard.kind === 'mentalFog') {
        this.player.mentalHealth = Math.max(
          0,
          this.player.mentalHealth - hazard.mentalDamagePerSecond * deltaSeconds,
        )
        if (this.player.mentalHealth <= 0) this.killPlayer(`Mental health collapsed in ${hazard.name}`)
        continue
      }
      const cycle = Math.floor((this.roomElapsedMs + hazard.phaseOffsetMs) / hazard.cycleMs)
      if (this.hazardHitCycles.get(hazard.id) === cycle) continue
      this.hazardHitCycles.set(hazard.id, cycle)
      this.damagePlayer(hazard.damage, hazard.name)
    }
  }

  private gestureReady(hand: LastChancesHand, gesture: LastChancesGesture): boolean {
    const weapon = this.weapons.get(hand)
    if (!weapon) return false
    const attack = weapon.attacks[gesture]
    if (attack.enabled === false || attack.behavior === 'disabled') return false
    const state = this.weaponState(weapon)
    if (state.resource <= 0 && weapon.resource?.kind !== 'rhythm') return false
    if (this.provisionalParry
      && this.provisionalParry.weaponId === weapon.id
      && this.provisionalParry.hand !== hand) return false
    if ((this.weaponActionEnds.get(weapon.id) ?? 0) > this.elapsedMs) return false
    const otherHandUsesWeapon = [...this.heldChannels.entries()].some(([channelHand, area]) => (
      channelHand !== hand && area.weaponId === weapon.id
    ))
    if (otherHandUsesWeapon) return false
    const axeRecoveryCancel = weapon.trait === 'axeHookRecovery'
      && gesture === 'tap'
      && state.recoveryMs > 0
    if ((this.player.recoveryMs > 0 || state.recoveryMs > 0) && !axeRecoveryCancel) {
      return false
    }
    return gesture === 'tap'
      || (this.cooldownEnds.get(cooldownKey(hand, gesture)) ?? 0) <= this.elapsedMs
  }

  private activatePlayerParry(
    attack: LastChancesAttackDefinition,
    minimumDurationMs = 0,
  ): void {
    this.player.parryMs = Math.max(
      this.player.parryMs,
      Math.max(180, minimumDurationMs, attack.durationMs + (attack.lingerMs ?? 0)),
    )
    this.activeParryCollider = resolveAttackCollider(
      this.player.position,
      normalize(this.player.aim),
      attack,
    )
  }

  private beginProvisionalTapParry(hand: LastChancesHand): void {
    const weapon = this.weapons.get(hand)
    if (!weapon || !this.gestureReady(hand, 'tap')) return
    const attack = attackWithLastChancesAugment(weapon.attacks.tap, weapon)
    if (!PLAYER_PARRY_BEHAVIORS.has(attack.behavior ?? 'standard')) return
    this.cancelProvisionalParry()
    this.provisionalParry = {
      hand,
      weaponId: weapon.id,
      attack,
      consumed: false,
    }
    this.activatePlayerParry(attack, this.config.input.doubleTapMs)
    if (this.activeParryCollider) this.addColliderTrace(this.activeParryCollider, attack)

    for (const enemy of this.enemies) {
      if (enemy.state !== 'attacking'
        || enemy.leapRemainingDistance > 0
        || enemy.attackWindupMs <= 0) continue
      const profile = this.enemyCombatProfile(enemy)
      if (enemy.attackWindupMs > profile.parryWindowMs) continue
      if (this.parryIncomingEnemy(enemy, profile)) break
    }
  }

  private cancelProvisionalParry(): void {
    if (!this.provisionalParry) return
    this.provisionalParry = null
    this.player.parryMs = 0
    this.activeParryCollider = null
  }

  private commitOrCancelProvisionalParry(): void {
    const provisional = this.provisionalParry
    if (!provisional) return
    if (!provisional.consumed) {
      this.cancelProvisionalParry()
      return
    }
    const actionDurationMs = Math.max(
      this.player.parryMs,
      provisional.attack.durationMs + (provisional.attack.lingerMs ?? 0),
    )
    this.weaponActionEnds.set(
      provisional.weaponId,
      Math.max(
        this.weaponActionEnds.get(provisional.weaponId) ?? 0,
        this.elapsedMs + actionDurationMs,
      ),
    )
    this.scheduleRecovery(
      provisional.weaponId,
      provisional.attack.recoveryMs,
      actionDurationMs,
    )
    this.lastGesture = {
      hand: provisional.hand,
      gesture: 'tap',
      attackName: provisional.attack.name,
      atMs: this.elapsedMs,
    }
    this.provisionalParry = null
  }

  private consumeActiveParry(): void {
    if (this.provisionalParry) this.provisionalParry.consumed = true
  }

  private playerParryCovers(point: LastChancesVector, radius: number): boolean {
    return this.player.parryMs > 0
      && this.activeParryCollider !== null
      && colliderHitsCircle(this.activeParryCollider, point, radius)
  }

  private playerParryCoversSweep(
    start: LastChancesVector,
    end: LastChancesVector,
    radius: number,
  ): boolean {
    return this.player.parryMs > 0
      && this.activeParryCollider !== null
      && colliderHitsSweptCircle(this.activeParryCollider, start, end, radius)
  }

  private parryIncomingEnemy(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
  ): boolean {
    if (!this.playerParryCovers(enemy.position, enemy.definition.radius)) return false
    this.consumeActiveParry()
    this.finishEnemyAttack(enemy, profile)
    enemy.attackCooldownMs += profile.parryWindowMs
    enemy.revealedMs = Math.max(enemy.revealedMs, 1200)
    return true
  }

  private performAttack(resolution: LastChancesGestureResolution): void {
    if (this.phase !== 'playing' || this.paused || !this.currentNode) return
    const { hand, gesture } = resolution
    const weapon = this.weapons.get(hand)
    if (!weapon) return
    const provisional = this.provisionalParry?.hand === hand
      ? this.provisionalParry
      : null
    if (provisional && gesture !== 'tap') {
      if (provisional.consumed) {
        this.commitOrCancelProvisionalParry()
        return
      }
      this.cancelProvisionalParry()
    }
    const state = this.weaponState(weapon)
    if (!this.gestureReady(hand, gesture)) return
    const isAxeRecoveryCancel = weapon.trait === 'axeHookRecovery'
      && gesture === 'tap'
      && state.recoveryMs > 0
    const key = cooldownKey(hand, gesture)
    const comboStep = gesture === 'tap' ? this.advanceTapCombo(hand) : undefined
    const sourceAttack = comboStep === undefined
      ? weapon.attacks[gesture]
      : weapon.tapCombo[(comboStep - 1) % weapon.tapCombo.length]
    if (sourceAttack.enabled === false || sourceAttack.behavior === 'disabled') return
    const augmented = attackWithLastChancesAugment(sourceAttack, weapon)
    const chargeHeldMs = gesture === 'holdThenDoubleTap'
      ? resolution.firstHoldMs
      : gesture === 'hold' || gesture === 'doubleTapHold' ? resolution.heldMs : 0
    const charged = resolveLastChancesChargedAttack(augmented, chargeHeldMs)
    if (sourceAttack.charge && !charged.band) return
    const attack = charged.attack
    if (isAxeRecoveryCancel) {
      state.recoveryMs = 0
      this.player.recoveryMs = 0
      attack.damage *= tuningValue(weapon, 'recoveryCancelDamageMultiplier', 1.35)
    }
    if (weapon.trait === 'swordRhythm' && gesture === 'tap') {
      this.applySwordRhythm(state, attack, hand)
    }
    if (gesture !== 'tap') this.cooldownEnds.set(key, this.elapsedMs + attack.cooldownMs)
    this.lastGesture = {
      hand,
      gesture,
      attackName: attack.name,
      atMs: this.elapsedMs,
      ...(comboStep === undefined ? {} : { comboStep }),
    }
    let direction = normalize(this.player.aim)
    const movement = this.resolveMovement()
    if (['katanaHop', 'katanaHopSlash'].includes(attack.behavior ?? '')
      && vectorLength(movement) > 0.2) {
      direction = normalize(movement, direction)
    }
    if (attack.behavior === 'axeLeap') {
      const rotationSign = Math.sign(weapon.attacks.hold.collider?.rotationDegrees ?? 360) || 1
      const fallbackInertia = rotationSign > 0
        ? { x: -direction.y, y: direction.x }
        : { x: direction.y, y: -direction.x }
      direction = normalize(state.spinInertiaDirection ?? fallbackInertia, fallbackInertia)
      state.spinInertiaDirection = null
    }
    const storedDot = weapon.trait === 'chainDotCarrier' && state.storedDot
      ? { ...state.storedDot }
      : null
    if (storedDot) state.storedDot = null
    const context: AttackExecutionContext = {
      weapon,
      hand,
      gesture,
      resolution,
      ...(comboStep === undefined ? {} : { comboStep }),
      ...(charged.band ? { chargeBandId: charged.band.id } : {}),
      storedDot,
    }

    if (attack.rootMs) this.player.rootMs = Math.max(this.player.rootMs, attack.rootMs)
    if (attack.invulnerabilityMs) {
      this.player.invulnerableMs = Math.max(
        this.player.invulnerableMs,
        attack.invulnerabilityMs,
        attack.durationMs,
      )
    }
    if (PLAYER_PARRY_BEHAVIORS.has(attack.behavior ?? 'standard')) {
      this.provisionalParry = null
      this.activatePlayerParry(attack)
    }
    if (attack.behavior === 'spearKick') {
      this.player.armorMultiplier = 2
      this.player.armorMultiplierMs = Math.max(this.player.armorMultiplierMs, 500)
    }
    if (attack.behavior === 'spearSpin') {
      for (const hazard of this.currentNode.arena.hazards) {
        if (hazard.kind === 'mentalFog') {
          this.hazardSuppressedUntil.set(
            hazard.id,
            this.elapsedMs + tuningValue(attack, 'fogSuppressionMs', 2500),
          )
        }
      }
    }
    const heldChannel = gesture === 'hold'
      && ['spearStance', 'axeSpin', 'spiderFlurry'].includes(attack.behavior ?? '')
    if (heldChannel) {
      this.stopHeldChannel(hand)
      this.scheduleRecovery(weapon.id, attack.recoveryMs, 0)
      this.preserveAxeComboThrough(
        weapon,
        (attack.recoveryMs ?? 0) + (this.config.input.tapComboWindowMs ?? 900),
      )
      if (weapon.trait === 'spiderDurability') {
        this.spendWeaponResource(
          weapon.id,
          attack.resourceCost ?? tuningValue(weapon, 'durabilityPerUse', 2),
        )
      }
      return
    }
    if (attack.behavior === 'swordFollowUp') {
      const openingAttack = attackWithLastChancesAugment(weapon.attacks.doubleTap, weapon)
      const followUpDelayMs = tuningValue(attack, 'followUpDelayMs', openingAttack.durationMs)
      const actionDurationMs = followUpDelayMs + attack.durationMs + (attack.lingerMs ?? 0)
      this.weaponActionEnds.set(weapon.id, this.elapsedMs + actionDurationMs)
      this.scheduleRecovery(weapon.id, attack.recoveryMs, actionDurationMs)
      this.executeAttack(openingAttack, direction, {
        ...context,
        gesture: 'doubleTap',
      })
      this.delayedAttacks.push({
        remainingMs: followUpDelayMs,
        attack,
        direction: { ...direction },
        context,
      })
      return
    }

    const actionDurationMs = attack.durationMs + (attack.lingerMs ?? 0)
    const commitsTapParry = gesture === 'tap'
      && PLAYER_PARRY_BEHAVIORS.has(attack.behavior ?? 'standard')
    if (gesture !== 'tap' || commitsTapParry) {
      this.weaponActionEnds.set(weapon.id, this.elapsedMs + actionDurationMs)
    }
    this.scheduleRecovery(weapon.id, attack.recoveryMs, actionDurationMs)
    if (gesture !== 'tap') {
      this.preserveAxeComboThrough(
        weapon,
        actionDurationMs + (attack.recoveryMs ?? 0)
          + (this.config.input.tapComboWindowMs ?? 900),
      )
    }
    this.executeAttack(attack, direction, context)
    if (weapon.trait === 'spiderDurability') {
      const useCost = attack.consumeAllResource
        ? state.maxResource
        : attack.resourceCost ?? tuningValue(weapon, 'durabilityPerUse', 2)
      this.spendWeaponResource(weapon.id, useCost)
    }
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

  private preserveAxeComboThrough(
    weapon: LastChancesResolvedWeapon,
    durationMs: number,
  ): void {
    if (weapon.trait !== 'axeHookRecovery') return
    for (const hand of LAST_CHANCES_HANDS) {
      const resolved = this.weapons.get(hand)
      if (resolved?.id !== weapon.id || resolved.attacks.tap.behavior !== 'axeSwing') continue
      const combo = this.tapCombos[hand]
      if (combo.step <= 0) continue
      combo.expiresAtMs = Math.max(combo.expiresAtMs, this.elapsedMs + durationMs)
    }
  }

  private resetTapCombos(): void {
    for (const hand of LAST_CHANCES_HANDS) {
      this.tapCombos[hand].step = 0
      this.tapCombos[hand].expiresAtMs = 0
    }
  }

  private executeAttack(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    if (attack.behavior === 'spearRelease') {
      this.performSpearRelease(attack, direction, context)
      return
    }
    if (attack.kind === 'melee') this.performMelee(attack, direction, context)
    if (attack.kind === 'projectile') this.performProjectile(attack, direction, context)
    if (attack.kind === 'dash') this.performDash(attack, direction, context)
    if (attack.kind === 'burst') this.performBurst(attack, direction, context)
  }

  private performMelee(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    this.startActiveArea(
      'melee',
      attack,
      direction,
      context.weapon.id,
      context.hand,
      context.storedDot,
    )
    this.addEffect('melee', attack, direction)
  }

  private performProjectile(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    const speed = Math.max(1, attack.projectileSpeed)
    const projectileRadius = Math.max(attack.radius, (attack.collider?.width ?? 0) / 2)
    const projectileSpawnOffset = Math.max(
      0,
      tuningValue(attack, 'projectileSpawnOffset', 0),
    )
    this.projectiles.push({
      id: this.nextProjectileId,
      position: {
        x: this.player.position.x + direction.x
          * (this.config.player.radius + projectileRadius + 2 + projectileSpawnOffset),
        y: this.player.position.y + direction.y
          * (this.config.player.radius + projectileRadius + 2 + projectileSpawnOffset),
      },
      velocity: { x: direction.x * speed, y: direction.y * speed },
      radius: projectileRadius,
      damage: attack.damage,
      knockback: attack.knockback,
      remainingDistance: Math.max(0, attack.range - projectileSpawnOffset),
      remainingMs: attack.durationMs > 0 ? attack.durationMs : (attack.range / speed) * 1000,
      remainingHits: attack.pierce + 1,
      hitIds: new Set(),
      color: attack.color,
      source: 'player',
      sourceName: 'Player',
      attack: { ...attack },
      weaponId: context.weapon.id,
      hand: context.hand,
      storedDot: context.storedDot,
      ...(attack.behavior === 'spearRelease' && context.chargeBandId === 'late'
        ? { carriedIds: new Set<string>() }
        : {}),
    })
    this.nextProjectileId += 1
    this.addEffect('hit', attack, direction)
  }

  private performDash(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    const dashStartDelayMs = Math.max(0, tuningValue(attack, 'dashStartDelayMs', 0))
    const durationSeconds = Math.max(0.08, (attack.durationMs - dashStartDelayMs) / 1000)
    const dashRadius = Math.max(attack.radius, (attack.collider?.width ?? 0) / 2)
    const traversalOnly = attack.behavior === 'poleVault'
      || attack.behavior === 'axeLeap'
      || attack.behavior === 'clawDash'
    this.activeDash = {
      origin: { ...this.player.position },
      direction,
      remainingDistance: attack.range,
      speed: attack.range / durationSeconds,
      damage: attack.damage,
      radius: dashRadius,
      knockback: attack.knockback,
      hitIds: new Set(),
      hitRecords: new Map(),
      remainingHits: traversalOnly
        ? 0
        : (attack.pierce + 1) * (
            attack.behavior === 'katanaFlurry' ? Math.max(1, attack.repeatHits ?? 1) : 1
          ),
      color: attack.color,
      attack: { ...attack },
      weaponId: context.weapon.id,
      hand: context.hand,
      storedDot: context.storedDot,
      landingBurst: attack.behavior === 'axeLeap' || attack.behavior === 'clawDash',
      trailAccumulatorMs: 0,
      elapsedMs: 0,
    }
    this.addEffect('dash', attack, direction)
  }

  private performBurst(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    this.startActiveArea(
      'burst',
      attack,
      direction,
      context.weapon.id,
      context.hand,
      context.storedDot,
    )
    this.addEffect('burst', attack, direction)
  }

  private startActiveArea(
    kind: RuntimeActiveArea['kind'],
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    weaponId = this.weapons.get('left')?.id ?? 'unknown',
    hand: LastChancesHand = 'left',
    storedDot: LastChancesStoredDot | null = null,
    channel = false,
  ): void {
    const area = this.createActiveArea(kind, attack, direction, weaponId, hand, storedDot, channel)
    this.addColliderTrace(this.activeAreaCollider(area), attack)
    if (attack.behavior === 'axeGrapple' || attack.behavior === 'axeThrow') {
      this.latchAxeTarget(area)
    }
    else this.applyActiveAreaHits(area)
    if (area.remainingMs > 0 && area.remainingHits > 0) this.activeAreas.push(area)
  }

  private latchAxeTarget(area: RuntimeActiveArea): void {
    const authoredRotation = area.attack.collider?.shape === 'sweep'
      ? area.attack.collider.rotationDegrees ?? 0
      : 0
    const collider = resolveAttackCollider(
      area.origin,
      area.direction,
      area.attack,
      1,
      -authoredRotation,
    )
    const target = this.enemies
      .filter(enemy => enemy.state !== 'dead'
        && colliderHitsCircle(collider, enemy.position, enemy.definition.radius))
      .sort((left, right) => (
        distanceSquared(area.origin, left.position) - distanceSquared(area.origin, right.position)
        || left.id.localeCompare(right.id)
      ))[0]
    if (!target) return
    area.latchedIds.add(target.id)
    target.statuses.stunMs = Math.max(
      target.statuses.stunMs,
      tuningValue(area.attack, 'grappleStunMs', area.totalMs),
    )
  }

  private updateActiveAreas(deltaMs: number): void {
    const completed: RuntimeActiveArea[] = []
    for (const area of this.activeAreas) {
      const previousSweepDegrees = area.sweepDegrees
      let facingTurnRadians = 0
      if (area.attack.collider?.followsPlayer) {
        const previousDirection = normalize(area.direction)
        const nextDirection = normalize(this.player.aim, previousDirection)
        facingTurnRadians = Math.atan2(
          previousDirection.x * nextDirection.y - previousDirection.y * nextDirection.x,
          previousDirection.x * nextDirection.x + previousDirection.y * nextDirection.y,
        )
        area.origin = { ...this.player.position }
        area.direction = nextDirection
        if (area.attack.behavior === 'spearStance') {
          const facingChange = previousDirection.x * nextDirection.x + previousDirection.y * nextDirection.y
          area.attack.damage = area.baseDamage * (
            facingChange < tuningValue(area.attack, 'turnDotThreshold', 0.995)
              ? tuningValue(area.attack, 'turnDamageMultiplier', 2.2)
              : tuningValue(area.attack, 'contactDamageMultiplier', 0.38)
          )
        }
      }
      if ((area.attack.behavior === 'axeGrapple' || area.attack.behavior === 'axeThrow')
        && area.latchedIds.size > 0) {
        const holdDistance = Math.min(
          tuningValue(area.attack, 'holdDistanceMax', 82),
          Math.max(
            tuningValue(area.attack, 'holdDistanceMin', 42),
            area.attack.range * tuningValue(area.attack, 'holdDistanceRatio', 0.62),
          ),
        )
        for (const enemyId of area.latchedIds) {
          const enemy = this.enemies.find(candidate => candidate.id === enemyId && candidate.state !== 'dead')
          if (!enemy) continue
          const target = {
            x: area.origin.x + area.direction.x * holdDistance,
            y: area.origin.y + area.direction.y * holdDistance,
          }
          this.moveCircle(enemy.position, {
            x: target.x - enemy.position.x,
            y: target.y - enemy.position.y,
          }, enemy.definition.radius)
          enemy.statuses.stunMs = Math.max(enemy.statuses.stunMs, deltaMs + 80)
        }
      }
      if (area.attack.collider?.shape === 'sweep') {
        const movement = this.resolveMovement()
        const authoredRotation = area.attack.collider.rotationDegrees ?? 360
        const rotationSign = Math.sign(authoredRotation) || 1
        const movementTurn = area.direction.x * movement.y - area.direction.y * movement.x
        const rotationCanBeAssisted = ['chainSpin', 'axeSpin'].includes(area.attack.behavior ?? '')
        const matchingMovement = Math.abs(movementTurn) > 0.2
          && Math.sign(movementTurn) === rotationSign
        const matchingFacingTurn = Math.abs(facingTurnRadians) > 0.012
          && Math.sign(facingTurnRadians) === rotationSign
        if (rotationCanBeAssisted && (matchingMovement || matchingFacingTurn)) {
          area.rotationAssisted = true
        }
        const assist = rotationCanBeAssisted && area.rotationAssisted ? 2 : 1
        if (area.attack.behavior === 'chainSpin') {
          area.attack.repeatHits = area.rotationAssisted ? area.authoredRepeatHits : 1
        }
        if (area.attack.behavior === 'axeSpin') {
          area.attack.repeatIntervalMs = area.baseRepeatIntervalMs / assist
        }
        const degreesPerMs = area.channel
          ? Math.sign(authoredRotation || 1)
            * tuningValue(area.attack, 'channelDegreesPerMs', 0.42)
          : authoredRotation / Math.max(1, area.totalMs)
        area.sweepDegrees += deltaMs * degreesPerMs * assist
      }
      if (area.attack.behavior === 'spiderFlurry' && area.channel) {
        const channelElapsedMs = Math.max(0, area.totalMs - area.remainingMs)
        area.attack.repeatIntervalMs = Math.max(
          tuningValue(area.attack, 'minimumRepeatIntervalMs', 55),
          area.baseRepeatIntervalMs - channelElapsedMs
            * tuningValue(area.attack, 'tempoGainPerMs', 0.06),
        )
      }
      area.remainingMs = Math.max(0, area.remainingMs - deltaMs)
      area.traceAccumulatorMs += deltaMs
      if (area.attack.collider?.shape === 'sweep') {
        const angularTravel = area.sweepDegrees - previousSweepDegrees
        const reach = Math.max(1, area.attack.range)
        const width = Math.max(1, area.attack.collider.width ?? area.attack.radius * 2)
        const geometricStep = 2 * Math.asin(Math.min(1, width / (reach * 2))) * 180 / Math.PI
        const maximumStepDegrees = clamp(geometricStep, 3, 12)
        const steps = Math.max(1, Math.ceil(Math.abs(angularTravel) / maximumStepDegrees))
        for (let step = 1; step <= steps && area.remainingHits > 0; step += 1) {
          const sweepDegrees = previousSweepDegrees + angularTravel * step / steps
          const collider = this.activeAreaCollider(area, sweepDegrees)
          this.addColliderTrace(collider, area.attack)
          this.applyActiveAreaHits(area, collider)
        }
        area.traceAccumulatorMs = 0
      } else {
        if (area.traceAccumulatorMs >= 45) {
          area.traceAccumulatorMs = 0
          this.addColliderTrace(this.activeAreaCollider(area), area.attack)
        }
        this.applyActiveAreaHits(area)
      }
      if (area.remainingMs <= 0 || area.remainingHits <= 0) completed.push(area)
    }
    this.activeAreas = this.activeAreas.filter(area => area.remainingMs > 0 && area.remainingHits > 0)
    completed.forEach(area => this.finishActiveArea(area))
  }

  private applyActiveAreaHits(
    area: RuntimeActiveArea,
    collider = this.activeAreaCollider(area),
  ): void {
    const isAxeLatch = area.attack.behavior === 'axeGrapple'
      || area.attack.behavior === 'axeThrow'
    if (isAxeLatch && area.remainingMs > 0) return
    const repeatInterval = Math.max(1, area.attack.repeatIntervalMs ?? area.attack.collider?.tickMs ?? 120)
    const repeatHits = Math.max(1, area.attack.repeatHits ?? 1)
    for (const enemy of this.enemies) {
      if (area.remainingHits <= 0) break
      if (enemy.state === 'dead') continue
      if (isAxeLatch && !area.latchedIds.has(enemy.id)) continue
      const previous = area.hitIds.get(enemy.id)
      if (previous && (previous.hits >= repeatHits
        || this.elapsedMs - previous.lastAtMs < repeatInterval)) continue
      if (!colliderHitsCircle(collider, enemy.position, enemy.definition.radius)) continue
      const nextHit = (previous?.hits ?? 0) + 1
      if (area.attack.behavior === 'katanaFlurry'
        && (enemy.definition.dodge ?? 0) >= tuningValue(area.attack, 'dodgeThreshold', 0.25)
        && nextHit % 2 === 0) {
        area.hitIds.set(enemy.id, { lastAtMs: this.elapsedMs, hits: nextHit })
        area.remainingHits -= 1
        continue
      }
      area.hitIds.set(enemy.id, {
        lastAtMs: this.elapsedMs,
        hits: nextHit,
      })
      area.remainingHits -= 1
      this.tryParryEnemy(enemy, area.attack)
      let resolvedAttack = area.attack
      if (area.attack.behavior === 'spearSpin') {
        const headPoint = {
          x: enemy.position.x + enemy.facing.x * enemy.definition.radius
            * tuningValue(area.attack, 'headshotOffsetRatio', 0.55),
          y: enemy.position.y + enemy.facing.y * enemy.definition.radius
            * tuningValue(area.attack, 'headshotOffsetRatio', 0.55),
        }
        const headRadius = enemy.definition.radius
          * tuningValue(area.attack, 'headshotRadiusRatio', 0.32)
        if (colliderHitsCircle(collider, headPoint, headRadius)) {
          resolvedAttack = {
            ...area.attack,
            damage: area.attack.damage
              * tuningValue(area.attack, 'headshotDamageMultiplier', 1.65),
            color: '#fff0a8',
          }
          enemy.criticalHitMs = Math.max(
            enemy.criticalHitMs,
            tuningValue(area.attack, 'headshotCueMs', 650),
          )
          this.addColliderTrace(collider, resolvedAttack)
        }
      }
      const toEnemy = {
        x: enemy.position.x - area.origin.x,
        y: enemy.position.y - area.origin.y,
      }
      const knockbackDirection = area.kind === 'burst' || area.attack.collider?.shape === 'circle'
        ? normalize(toEnemy, area.direction)
        : area.direction
      this.damageEnemy(enemy, resolvedAttack, area.attack.knockback, knockbackDirection, {
        weaponId: area.weaponId,
        hand: area.hand,
        storedDot: area.storedDot,
        distance: vectorLength(toEnemy),
      })
    }
  }

  private activeAreaCollider(
    area: RuntimeActiveArea,
    sweepDegrees = area.sweepDegrees,
  ): LastChancesRuntimeCollider {
    const expandsFromOrigin = area.kind === 'burst'
      && (area.attack.collider?.shape ?? 'circle') === 'circle'
    const progress = expandsFromOrigin && area.totalMs > 0
      ? clamp(1 - area.remainingMs / area.totalMs, 0, 1)
      : 1
    const authoredRotation = area.attack.collider?.shape === 'sweep'
      ? area.attack.collider.rotationDegrees ?? 0
      : 0
    return resolveAttackCollider(
      area.origin,
      area.direction,
      area.attack,
      progress,
      sweepDegrees - authoredRotation,
    )
  }

  private addColliderTrace(
    collider: LastChancesRuntimeCollider,
    attack: LastChancesAttackDefinition,
  ): void {
    const duration = Math.max(120, attack.collider?.traceMs ?? 320)
    this.traces.push({
      collider: JSON.parse(JSON.stringify(collider)) as LastChancesRuntimeCollider,
      color: attack.color,
      remainingMs: duration,
      totalMs: duration,
    })
    if (this.traces.length > 80) this.traces.splice(0, this.traces.length - 80)
  }

  private finishActiveArea(area: RuntimeActiveArea): void {
    if (area.attack.behavior !== 'chainThrow' && area.attack.behavior !== 'axeThrow') return
    const movement = this.resolveMovement()
    const direction = area.attack.behavior === 'chainThrow' && vectorLength(movement) > 0.2
      ? normalize(movement)
      : normalize(this.player.aim)
    for (const enemyId of area.hitIds.keys()) {
      const enemy = this.enemies.find(candidate => candidate.id === enemyId && candidate.state !== 'dead')
      if (!enemy) continue
      this.moveCircle(
        enemy.position,
        {
          x: direction.x * Math.max(
            tuningValue(area.attack, 'minimumThrowDistance', 80),
            area.attack.knockback,
          ),
          y: direction.y * Math.max(
            tuningValue(area.attack, 'minimumThrowDistance', 80),
            area.attack.knockback,
          ),
        },
        enemy.definition.radius,
      )
      enemy.statuses.stunMs = Math.max(
        enemy.statuses.stunMs,
        tuningValue(area.attack, 'throwStunMs', 450),
      )
    }
  }

  private performSpearRelease(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    if (context.chargeBandId === 'early') {
      this.performMelee({
        ...attack,
        kind: 'melee',
        collider: {
          ...(attack.collider ?? { traceMs: 900 }),
          shape: 'sector',
          innerRange: 52,
        },
      }, direction, context)
      return
    }
    if (context.chargeBandId === 'middle') {
      const target = this.enemies
        .filter(enemy => enemy.state !== 'dead')
        .map(enemy => ({
          enemy,
          distance: Math.sqrt(distanceSquared(this.player.position, enemy.position)),
        }))
        .filter(candidate => candidate.distance >= Math.max(80, attack.range * 0.25)
          && candidate.distance <= attack.range)
        .sort((left, right) => left.distance - right.distance)[0]?.enemy
      const aimed = target
        ? normalize({
            x: target.position.x - this.player.position.x,
            y: target.position.y - this.player.position.y,
          }, direction)
        : direction
      const mediumAttack = {
        ...attack,
        kind: 'projectile' as const,
        pierce: 0,
        hitEffects: [
          ...(attack.hitEffects ?? []),
          { status: 'stun' as const, durationMs: 1000 },
        ],
      }
      this.performProjectile(mediumAttack, aimed, context)
      return
    }
    this.performProjectile(
      {
        ...attack,
        kind: 'projectile',
        pierce: Math.max(attack.pierce, 8),
      },
      direction,
      context,
    )
  }

  private pinProjectileTargets(projectile: RuntimeProjectile): void {
    if (!projectile.carriedIds) return
    const pinDurationMs = tuningValue(projectile.attack, 'wallPinMs', 1800)
    const arena = this.currentNode?.arena
    const direction = normalize(projectile.velocity)
    for (const enemyId of projectile.carriedIds) {
      const enemy = this.enemies.find(candidate => candidate.id === enemyId && candidate.state !== 'dead')
      if (!enemy) continue
      if (arena) {
        const previousPosition = { ...enemy.position }
        const stepDistance = Math.max(3, enemy.definition.radius / 4)
        let candidate = {
          x: clamp(projectile.position.x, enemy.definition.radius, arena.width - enemy.definition.radius),
          y: clamp(projectile.position.y, enemy.definition.radius, arena.height - enemy.definition.radius),
        }
        let foundSafePosition = false
        for (let step = 0; step < 64; step += 1) {
          const insideObstacle = arena.obstacles.some(obstacle => (
            pointHitsObstacle(candidate, enemy.definition.radius, obstacle)
          ))
          if (!insideObstacle) {
            foundSafePosition = true
            break
          }
          candidate = {
            x: clamp(
              candidate.x - direction.x * stepDistance,
              enemy.definition.radius,
              arena.width - enemy.definition.radius,
            ),
            y: clamp(
              candidate.y - direction.y * stepDistance,
              enemy.definition.radius,
              arena.height - enemy.definition.radius,
            ),
          }
        }
        enemy.position = foundSafePosition ? candidate : previousPosition
      }
      enemy.statuses.stunMs = Math.max(enemy.statuses.stunMs, pinDurationMs)
      enemy.statuses.disarmMs = Math.max(enemy.statuses.disarmMs, pinDurationMs)
      enemy.revealedMs = Math.max(enemy.revealedMs, pinDurationMs)
    }
    projectile.carriedIds.clear()
  }

  private stopHeldChannel(hand: LastChancesHand): void {
    const existing = this.heldChannels.get(hand)
    if (!existing) return
    if (existing.attack.behavior === 'axeSpin') {
      const state = this.weaponStates.get(existing.weaponId)
      if (state) {
        const rotationDegrees = existing.sweepDegrees
        const radial = rotateVector(
          normalize(existing.direction),
          rotationDegrees * Math.PI / 180,
        )
        const rotationSign = Math.sign(existing.attack.collider?.rotationDegrees ?? 360) || 1
        state.spinInertiaDirection = normalize(rotationSign > 0
          ? { x: -radial.y, y: radial.x }
          : { x: radial.y, y: -radial.x })
      }
    }
    existing.remainingMs = 0
    this.activeAreas = this.activeAreas.filter(area => area !== existing)
    this.heldChannels.delete(hand)
  }

  private commitHeldChannels(): void {
    for (const [hand, area] of [...this.heldChannels.entries()]) {
      const weapon = this.weapons.get(hand)
      if (!weapon || weapon.id !== area.weaponId) {
        this.stopHeldChannel(hand)
        continue
      }
      const attack = weapon.attacks.hold
      this.cooldownEnds.set(
        cooldownKey(hand, 'hold'),
        this.elapsedMs + attack.cooldownMs,
      )
      this.lastGesture = {
        hand,
        gesture: 'hold',
        attackName: attack.name,
        atMs: this.elapsedMs,
      }
      this.stopHeldChannel(hand)
      this.scheduleRecovery(weapon.id, attack.recoveryMs, 0)
      this.preserveAxeComboThrough(
        weapon,
        (attack.recoveryMs ?? 0) + (this.config.input.tapComboWindowMs ?? 900),
      )
      if (weapon.trait === 'spiderDurability') {
        this.spendWeaponResource(
          weapon.id,
          attack.resourceCost ?? tuningValue(weapon, 'durabilityPerUse', 2),
        )
      }
    }
  }

  private updateHeldWeaponMechanics(deltaMs: number): void {
    const now = this.frameNowMs || performance.now()
    for (const hand of LAST_CHANCES_HANDS) {
      const weapon = this.weapons.get(hand)
      if (!weapon) {
        this.stopHeldChannel(hand)
        continue
      }
      const input = this.gestures.snapshot(hand, now)
      const holdAttack = weapon.attacks.hold
      if (input.pressed
        && input.sequence === 'secondTap'
        && input.heldMs >= this.config.input.holdMs
        && this.gestureReady(hand, 'doubleTapHold')
        && weapon.attacks.doubleTapHold.behavior === 'spearKick') {
        this.player.armorMultiplier = 2
        this.player.armorMultiplierMs = Math.max(this.player.armorMultiplierMs, deltaMs + 80)
      }
      const channelBehavior = holdAttack.behavior
      const channelEligible = input.pressed
        && input.sequence === 'first'
        && input.heldMs >= this.config.input.holdMs
        && this.gestureReady(hand, 'hold')
        && ['spearStance', 'axeSpin', 'spiderFlurry'].includes(channelBehavior ?? '')
      const existing = this.heldChannels.get(hand)
      if (channelEligible && channelBehavior === 'axeSpin') {
        this.player.parryMs = Math.max(this.player.parryMs, deltaMs + 80)
      }
      if (!channelEligible) {
        if (existing) this.stopHeldChannel(hand)
        continue
      }
      if (existing && this.activeAreas.includes(existing)) continue
      if (existing) this.heldChannels.delete(hand)
      const channelAttack: LastChancesAttackDefinition = {
        ...attackWithLastChancesAugment(holdAttack, weapon),
        durationMs: 60_000,
        lingerMs: 0,
        repeatHits: Math.max(32, holdAttack.repeatHits ?? 1),
        repeatIntervalMs: holdAttack.repeatIntervalMs ?? holdAttack.collider?.tickMs ?? 180,
      }
      const area = this.createActiveArea(
        channelAttack.kind === 'burst' ? 'burst' : 'melee',
        channelAttack,
        this.player.aim,
        weapon.id,
        hand,
        null,
        true,
      )
      this.activeAreas.push(area)
      this.heldChannels.set(hand, area)
    }
  }

  private weaponState(weapon: LastChancesResolvedWeapon): RuntimeWeaponState {
    let state = this.weaponStates.get(weapon.id)
    if (!state) {
      const maximum = Math.max(1, weapon.resource?.max ?? 1)
      state = {
        resource: clamp(weapon.resource?.initial ?? maximum, 0, maximum),
        maxResource: maximum,
        storedDot: null,
        boundEnemyId: null,
        successfulHits: 0,
        lastHitHand: null,
        lastHitAtMs: Number.NEGATIVE_INFINITY,
        recoveryMs: 0,
        lastTapAtMs: Number.NEGATIVE_INFINITY,
        rhythm: 'idle',
        basicHits: 0,
        spinInertiaDirection: null,
      }
      this.weaponStates.set(weapon.id, state)
    }
    return state
  }

  private spendWeaponResource(weaponId: string, amount: number): void {
    const state = this.weaponStates.get(weaponId)
    if (!state) return
    state.resource = Math.max(0, state.resource - amount)
    if (state.resource > 0) return
    if (this.activeLoadout?.secondaryWeaponId === weaponId) {
      this.activeLoadout = { ...this.activeLoadout, secondaryWeaponId: null }
      this.rebuildWeapons()
    }
  }

  private applySwordRhythm(
    state: RuntimeWeaponState,
    attack: LastChancesAttackDefinition,
    hand: LastChancesHand,
  ): void {
    const weapon = this.weapons.get(hand)
    const interval = this.elapsedMs - state.lastTapAtMs
    state.lastTapAtMs = this.elapsedMs
    if (interval < tuningValue(weapon, 'rhythmEarlyMs', 170)) {
      state.rhythm = 'early'
      const fatigueMs = tuningValue(weapon, 'rhythmFatigueMs', 900)
      state.recoveryMs = Math.max(state.recoveryMs, fatigueMs)
      this.player.recoveryMs = Math.max(this.player.recoveryMs, fatigueMs)
      this.tapCombos[hand].step = 0
      return
    }
    if (interval <= tuningValue(weapon, 'rhythmGoodMaxMs', 540)) {
      state.rhythm = 'good'
      attack.knockback += tuningValue(weapon, 'rhythmKnockbackBonus', 18)
      attack.hitEffects = [
        ...(attack.hitEffects ?? []),
        {
          status: 'microstun',
          durationMs: tuningValue(weapon, 'rhythmMicrostunMs', 180),
        },
      ]
      this.moveCircle(
        this.player.position,
        {
          x: this.player.aim.x * tuningValue(weapon, 'rhythmStepDistance', 18),
          y: this.player.aim.y * tuningValue(weapon, 'rhythmStepDistance', 18),
        },
        this.config.player.radius,
      )
      return
    }
    state.rhythm = interval === Number.POSITIVE_INFINITY ? 'idle' : 'late'
  }

  private createActiveArea(
    kind: RuntimeActiveArea['kind'],
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    weaponId: string,
    hand: LastChancesHand,
    storedDot: LastChancesStoredDot | null,
    channel: boolean,
  ): RuntimeActiveArea {
    return {
      kind,
      origin: { ...this.player.position },
      direction: { ...direction },
      attack: { ...attack },
      baseDamage: attack.damage,
      weaponId,
      hand,
      remainingMs: attack.durationMs + (attack.lingerMs ?? 0),
      totalMs: attack.durationMs + (attack.lingerMs ?? 0),
      hitIds: new Map(),
      remainingHits: (attack.pierce + 1) * Math.max(1, attack.repeatHits ?? 1),
      storedDot,
      sweepDegrees: 0,
      traceAccumulatorMs: Number.POSITIVE_INFINITY,
      channel,
      authoredRepeatHits: Math.max(1, attack.repeatHits ?? 1),
      baseRepeatIntervalMs: Math.max(
        1,
        attack.repeatIntervalMs ?? attack.collider?.tickMs ?? 120,
      ),
      rotationAssisted: false,
      latchedIds: new Set(),
    }
  }

  private addEffect(
    kind: RuntimeEffect['kind'],
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
  ): void {
    const duration = kind === 'melee' || kind === 'burst'
      ? Math.max(1, attack.durationMs + (attack.lingerMs ?? 0))
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

  private tryParryEnemy(
    enemy: RuntimeEnemy,
    attack: LastChancesAttackDefinition,
  ): boolean {
    if (attack.behavior === 'katanaOverhead') {
      if (enemy.state !== 'attacking'
        || enemy.leapRemainingDistance > 0
        || enemy.attackWindupMs <= 0) return false
      const profile = this.enemyCombatProfile(enemy)
      this.finishEnemyAttack(enemy, profile)
      enemy.revealedMs = tuningValue(attack, 'interruptRevealMs', 1200)
      return true
    }
    if (![
      'parry',
      'chainStrike',
      'axeParry',
      'axeSpin',
      'katanaParry',
    ].includes(attack.behavior ?? '')) return false
    if (enemy.state !== 'attacking' || enemy.leapRemainingDistance > 0) return false
    const profile = this.enemyCombatProfile(enemy)
    if (enemy.attackWindupMs <= 0 || enemy.attackWindupMs > profile.parryWindowMs) return false
    this.finishEnemyAttack(enemy, profile)
    enemy.attackCooldownMs += profile.parryWindowMs
    enemy.revealedMs = 1200
    return true
  }

  private damageEnemy(
    enemy: RuntimeEnemy,
    attack: LastChancesAttackDefinition,
    knockback: number,
    direction: LastChancesVector,
    options: DamageEnemyOptions = {},
  ): void {
    if (enemy.state === 'dead') return
    enemy.revealedMs = 900
    const weapon = options.hand ? this.weapons.get(options.hand) : undefined
    const state = weapon ? this.weaponState(weapon) : null
    const capturedDot = weapon?.trait === 'chainDotCarrier' && !options.storedDot
      ? captureLastChancesDot(enemy.statuses)
      : null
    let hitEffects = attack.hitEffects?.map(effect => ({ ...effect }))
    let multiplier = 1
    let criticalHit = false
    const distance = options.distance
      ?? Math.sqrt(distanceSquared(this.player.position, enemy.position))
    if (attack.sweetSpot
      && distance >= attack.sweetSpot.minRange
      && (attack.sweetSpot.maxRange === undefined || distance <= attack.sweetSpot.maxRange)) {
      multiplier *= attack.sweetSpot.damageMultiplier
      knockback *= attack.sweetSpot.knockbackMultiplier ?? 1
      criticalHit = attack.sweetSpot.damageMultiplier > 1
    }
    if (attack.behavior === 'swordOpening') {
      if (enemy.statuses.openingMs > 0) {
        multiplier *= attack.sweetSpot?.criticalMultiplier ?? 2
        enemy.statuses.openingMs = 0
        criticalHit = true
      } else {
        multiplier *= 0.55
      }
    }
    if (attack.behavior === 'clawDeepStrike') {
      multiplier *= tuningValue(attack, 'criticalMultiplier', 1.65)
      criticalHit = true
      if (weapon?.augment === 'poison' && enemy.statuses.dots.bleed.stacks > 0) {
        hitEffects = hitEffects?.map(effect => effect.status === 'poison'
          ? {
              ...effect,
              tickDamage: (effect.tickDamage ?? 1)
                * tuningValue(attack, 'bleedingPoisonDamageMultiplier', 1.8),
              durationMs: effect.durationMs
                * tuningValue(attack, 'bleedingPoisonDurationMultiplier', 1.25),
            }
          : effect)
      }
    }
    if (attack.behavior === 'katanaCharge' && weapon?.augment === 'bleed') {
      multiplier += consumeLastChancesBleed(enemy.statuses) / Math.max(1, attack.damage)
    }
    if (attack.behavior === 'katanaCombo'
      && attack.rootMs
      && enemy.hp / Math.max(1, enemy.definition.maxHp)
        <= tuningValue(attack, 'executeHealthRatio', 0.35)) {
      multiplier *= tuningValue(attack, 'executeDamageMultiplier', 1.8)
      criticalHit = true
    }
    if (attack.behavior === 'spiderImpale' || attack.behavior === 'katanaOverhead') {
      criticalHit = true
    }
    if (attack.behavior === 'katanaDance') {
      refreshLastChancesBleed(
        enemy.statuses,
        tuningValue(attack, 'bleedRefreshMs', 6000),
      )
    }
    if (attack.behavior === 'spiderTwist') {
      refreshLastChancesBleed(
        enemy.statuses,
        tuningValue(attack, 'bleedRefreshMs', 6500),
        tuningValue(attack, 'bleedStackMultiplier', 1.5),
      )
    }

    const scaledDamage = attack.damage * multiplier * this.player.stats.attackPower / 100
    const armor = attack.damageType === 'true'
      ? 0
      : Math.max(0, enemy.definition.armor ?? 0)
        - enemy.statuses.armorBreak
    const hpBeforeHit = enemy.hp
    enemy.hp = Math.max(0, enemy.hp - Math.max(0, scaledDamage - Math.max(0, armor)))
    const damageDealt = hpBeforeHit - enemy.hp
    if (criticalHit && damageDealt > 0) {
      enemy.criticalHitMs = Math.max(
        enemy.criticalHitMs,
        tuningValue(attack, 'criticalCueMs', 650),
      )
    }
    const chainWeapon = [...this.weapons.values()].find(candidate => (
      candidate.trait === 'chainDotCarrier'
    ))
    if ((enemy.definition.role === 'boss' || enemy.definition.bossPhases)
      && damageDealt >= enemy.definition.maxHp
        * tuningValue(chainWeapon, 'bossRecoveryDamageRatio', 0.18)) {
      let recoveredChain = false
      for (const chainState of this.weaponStates.values()) {
        if (chainState.boundEnemyId !== enemy.id) continue
        chainState.boundEnemyId = null
        chainState.resource = chainState.maxResource
        recoveredChain = true
      }
      if (recoveredChain) {
        enemy.statuses.stunMs = Math.max(
          enemy.statuses.stunMs,
          tuningValue(chainWeapon, 'bossRecoveryStunMs', 700),
        )
      }
    }
    const hitRng = createLastChancesRng(
      `${this.currentNode?.seed ?? 0}:${Math.floor(this.elapsedMs)}:${enemy.id}:${attack.name}:${state?.successfulHits ?? 0}`,
    )
    applyLastChancesStatusEffects(enemy.statuses, hitEffects, hitRng)
    if (attack.behavior === 'katanaHopSlash' && weapon?.augment === 'bleed') {
      refreshLastChancesBleed(
        enemy.statuses,
        tuningValue(attack, 'bleedRefreshMs', 6200),
        tuningValue(attack, 'bleedStackMultiplier', 2),
      )
    }
    if (enemy.state === 'attacking'
      && (enemy.statuses.stunMs > 0 || enemy.statuses.disarmMs > 0)) {
      this.finishEnemyAttack(enemy, this.enemyCombatProfile(enemy))
    }
    if (options.storedDot) spreadLastChancesDot(enemy.statuses, options.storedDot)
    if (capturedDot && state && !state.storedDot) state.storedDot = capturedDot

    if (state) {
      state.successfulHits += 1
      if (attack.behavior === 'clawSlash' || attack.behavior === 'swordRhythm') {
        state.basicHits += 1
      }
      if (weapon?.trait === 'clawParity'
        && attack.behavior === 'clawSlash'
        && attack.damage > 0) {
        state.resource = state.basicHits % 2 === 1 ? state.maxResource : 0
        const dualRhythm = state.lastHitHand !== null
          && state.lastHitHand !== options.hand
          && this.elapsedMs - state.lastHitAtMs
            <= tuningValue(weapon, 'alternatingHitWindowMs', 360)
        if (state.basicHits % Math.max(
          1,
          Math.round(tuningValue(weapon, 'bleedEveryHits', 2)),
        ) === 0) {
          applyLastChancesStatusEffects(enemy.statuses, [{
            status: 'bleed',
            durationMs: 5000,
            stacks: 1,
            tickDamage: 1.1,
            tickMs: 500,
            refresh: 'stack',
          }], () => 0)
        }
        if (dualRhythm) {
          enemy.statuses.stunMs = Math.max(
            enemy.statuses.stunMs,
            tuningValue(weapon, 'alternatingMicrostunMs', 90),
          )
        }
        state.lastHitHand = options.hand ?? null
        state.lastHitAtMs = this.elapsedMs
      }
      if (weapon?.trait === 'axeHookRecovery') {
        const towardPlayer = normalize({
          x: this.player.position.x - enemy.position.x,
          y: this.player.position.y - enemy.position.y,
        })
        this.moveCircle(enemy.position, {
          x: towardPlayer.x * tuningValue(weapon, 'hookPullDistance', 16),
          y: towardPlayer.y * tuningValue(weapon, 'hookPullDistance', 16),
        }, enemy.definition.radius)
      }
      if (weapon?.trait === 'katanaFlow' && attack.cooldownRefundMs) {
        const currentCooldownKey = options.hand && this.lastGesture
          ? cooldownKey(options.hand, this.lastGesture.gesture)
          : null
        for (const [key, end] of this.cooldownEnds) {
          const cooldownHand = key.startsWith('right:') ? 'right' : 'left'
          if (key !== currentCooldownKey
            && this.weapons.get(cooldownHand)?.id === weapon.id
            && end > this.elapsedMs) {
            this.cooldownEnds.set(key, Math.max(this.elapsedMs, end - attack.cooldownRefundMs))
          }
        }
      }
      if (weapon?.trait === 'swordRhythm' && attack.behavior === 'swordRhythm') {
        const openingEveryHits = Math.max(
          1,
          Math.round(tuningValue(weapon, 'openingEveryHits', 3)),
        )
        if (state.basicHits % openingEveryHits === 0) {
          enemy.statuses.openingMs = Math.max(
            enemy.statuses.openingMs,
            tuningValue(weapon, 'openingDurationMs', 1600),
          )
        }
      }
      if (weapon?.trait === 'spiderDurability') {
        this.spendWeaponResource(
          weapon.id,
          tuningValue(weapon, 'durabilityPerHit', 1),
        )
      }
      if (attack.behavior === 'chainBind') {
        state.boundEnemyId = enemy.id
        state.resource = 0
      }
    }

    if (attack.behavior === 'chainHook') {
      const movement = this.resolveMovement()
      const pullDirection = vectorLength(movement) > 0.2
        ? normalize(movement)
        : normalize({
            x: this.player.position.x - enemy.position.x,
            y: this.player.position.y - enemy.position.y,
          })
      this.moveCircle(enemy.position, {
        x: pullDirection.x * Math.max(tuningValue(attack, 'minimumPullDistance', 40), knockback),
        y: pullDirection.y * Math.max(tuningValue(attack, 'minimumPullDistance', 40), knockback),
      }, enemy.definition.radius)
    } else if (attack.behavior === 'spearShove' || attack.behavior === 'spearKick') {
      const baseTargetDistance = Math.max(
        attack.sweetSpot?.minRange
          ?? attack.range * tuningValue(attack, 'targetRangeRatio', 0.72),
        tuningValue(attack, 'minimumTargetRange', 80),
      )
      const targetDistance = attack.behavior === 'spearKick'
        ? Math.max(
            baseTargetDistance,
            attack.knockback * tuningValue(attack, 'targetDistancePerKnockback', 1),
          )
        : baseTargetDistance
      const target = {
        x: clamp(
          this.player.position.x + direction.x * targetDistance,
          enemy.definition.radius,
          (this.currentNode?.arena.width ?? targetDistance) - enemy.definition.radius,
        ),
        y: clamp(
          this.player.position.y + direction.y * targetDistance,
          enemy.definition.radius,
          (this.currentNode?.arena.height ?? targetDistance) - enemy.definition.radius,
        ),
      }
      this.moveCircle(enemy.position, {
        x: target.x - enemy.position.x,
        y: target.y - enemy.position.y,
      }, enemy.definition.radius)
    } else if (knockback > 0) {
      this.moveCircle(
        enemy.position,
        { x: direction.x * knockback, y: direction.y * knockback },
        enemy.definition.radius,
      )
    }
    if (enemy.hp > 0) {
      if (enemy.state === 'idle') enemy.state = 'chasing'
      return
    }
    const killed = true
    this.finishEnemyDeath(enemy)
    if (killed && attack.resetCooldownOnKill && options.hand) {
      const source = this.weapons.get(options.hand)
      const resetGesture = source
        ? LAST_CHANCES_GESTURES.find(gesture => {
            const candidate = source.attacks[gesture]
            return candidate.behavior === attack.behavior && candidate.name === attack.name
          })
        : undefined
      if (resetGesture) this.cooldownEnds.delete(cooldownKey(options.hand, resetGesture))
    }
  }

  private finishEnemyDeath(enemy: RuntimeEnemy): void {
    if (enemy.state === 'dead') return
    enemy.state = 'dead'
    this.player.mentalHealth = Math.min(
      this.player.stats.maxMentalHealth,
      this.player.mentalHealth + this.config.mentalHealth.restoreOnKill,
    )
    for (const state of this.weaponStates.values()) {
      if (state.boundEnemyId !== enemy.id) continue
      state.boundEnemyId = null
      state.resource = state.maxResource
    }
  }

  private damagePlayer(rawDamage: number, source: string): void {
    if (this.player.invulnerableMs > 0 || this.phase !== 'playing') return
    const damage = Math.max(1, rawDamage - this.player.stats.armor * this.player.armorMultiplier)
    this.player.hp = Math.max(0, this.player.hp - damage)
    this.player.invulnerableMs = this.config.player.invulnerabilityMs
    if (this.player.hp <= 0) this.killPlayer(`Killed by ${source}`)
  }

  private killPlayer(reason: string): void {
    if (this.phase !== 'playing') return
    const activePrimary = this.activeLoadout
      ? this.config.weapons.find(weapon => weapon.id === this.activeLoadout?.primaryWeaponId)
      : null
    if (activePrimary?.corpseBound) this.corpseBoundPrimaryWeaponId = activePrimary.id
    const tierIndex = this.currentNode?.tierIndex ?? 0
    const tier = this.config.progression.tiers[tierIndex]
    this.chances = Math.max(0, this.chances - tier.deathCost)
    this.totalDeaths += 1
    const erosion = tier.erosion
    this.generationBaseStats = {
      maxHp: Math.max(1, this.generationBaseStats.maxHp - erosion.maxHp),
      maxMentalHealth: Math.max(1, this.generationBaseStats.maxMentalHealth - erosion.maxMentalHealth),
      attackPower: Math.max(1, this.generationBaseStats.attackPower - erosion.attackPower),
      moveSpeed: Math.max(1, this.generationBaseStats.moveSpeed - erosion.moveSpeed),
      armor: Math.max(0, this.generationBaseStats.armor - erosion.armor),
    }
    this.player.stats = copyStats(this.generationBaseStats)
    this.activeDash = null
    this.activeAreas = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.provisionalParry = null
    this.deathReason = reason
    this.phase = this.chances > 0 ? 'dead' : 'outOfChances'
    this.emitSnapshot(true)
  }

  private completeRoom(): void {
    if (!this.currentNode || this.phase !== 'playing') return
    this.clearCombatTransients()
    if (this.currentNode.interaction && !this.interactionResolved) {
      this.phase = 'interaction'
      this.emitSnapshot(true)
      return
    }
    this.finishRoomTransition()
    this.emitSnapshot(true)
  }

  private clearCombatTransients(): void {
    this.projectiles = []
    this.activeDash = null
    this.activeAreas = []
    this.heldChannels.clear()
    this.effects = []
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.provisionalParry = null
    this.gestures.reset()
    this.resetTapCombos()
    this.player.invulnerableMs = 0
    this.player.rootMs = 0
    this.player.recoveryMs = 0
    this.player.parryMs = 0
    this.player.armorMultiplier = 1
    this.player.armorMultiplierMs = 0
    for (const state of this.weaponStates.values()) state.recoveryMs = 0
  }

  private finishRoomTransition(): void {
    if (!this.currentNode) return
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
  }

  private interactionChoiceAvailable(choice: LastChancesInteractionChoice): boolean {
    if (this.chances < (choice.effect.chanceCost ?? 0)) return false
    if (!this.activeLoadout || (choice.effect.primaryWeaponId === undefined
      && choice.effect.secondaryWeaponId === undefined)) return true
    const candidate = this.normalizeLoadoutAugments({
      ...this.activeLoadout,
      primaryWeaponId: choice.effect.primaryWeaponId ?? this.activeLoadout.primaryWeaponId,
      secondaryWeaponId: choice.effect.secondaryWeaponId !== undefined
        ? choice.effect.secondaryWeaponId
        : this.activeLoadout.secondaryWeaponId,
    })
    const candidateConfig = cloneLastChancesConfig(this.config)
    candidateConfig.loadout = candidate
    const resolved = resolveLastChancesLoadout(candidateConfig)
    return resolved.left?.id === candidate.primaryWeaponId
      && (candidate.secondaryWeaponId === null || resolved.right?.id === candidate.secondaryWeaponId)
  }

  private applyInteractionChoice(choice: LastChancesInteractionChoice): void {
    const effect = choice.effect
    this.chances = Math.max(0, this.chances - (effect.chanceCost ?? 0))
    if (effect.stats) {
      this.player.stats = {
        maxHp: Math.max(1, this.player.stats.maxHp + (effect.stats.maxHp ?? 0)),
        maxMentalHealth: Math.max(
          1,
          this.player.stats.maxMentalHealth + (effect.stats.maxMentalHealth ?? 0),
        ),
        attackPower: Math.max(1, this.player.stats.attackPower + (effect.stats.attackPower ?? 0)),
        moveSpeed: Math.max(1, this.player.stats.moveSpeed + (effect.stats.moveSpeed ?? 0)),
        armor: Math.max(0, this.player.stats.armor + (effect.stats.armor ?? 0)),
      }
    }
    this.player.hp = clamp(
      this.player.hp + (effect.hp ?? 0),
      1,
      this.player.stats.maxHp,
    )
    this.player.mentalHealth = clamp(
      this.player.mentalHealth + (effect.mentalHealth ?? 0),
      1,
      this.player.stats.maxMentalHealth,
    )
    if (this.activeLoadout && (effect.primaryWeaponId !== undefined
      || effect.secondaryWeaponId !== undefined)) {
      this.activeLoadout = this.normalizeLoadoutAugments({
        ...this.activeLoadout,
        primaryWeaponId: effect.primaryWeaponId ?? this.activeLoadout.primaryWeaponId,
        secondaryWeaponId: effect.secondaryWeaponId !== undefined
          ? effect.secondaryWeaponId
          : this.activeLoadout.secondaryWeaponId,
      })
      this.rebuildWeapons()
      this.cooldownEnds.clear()
      this.resetTapCombos()
    }
  }

  private rebuildWeapons(): void {
    this.weapons.clear()
    const loadoutConfig = cloneLastChancesConfig(this.config)
    if (this.activeLoadout) loadoutConfig.loadout = { ...this.activeLoadout }
    const loadout = resolveLastChancesLoadout(loadoutConfig)
    if (loadout.left) {
      this.weapons.set('left', loadout.left)
      this.weaponState(loadout.left)
    }
    if (loadout.right) {
      this.weapons.set('right', loadout.right)
      this.weaponState(loadout.right)
    }
    for (const hand of LAST_CHANCES_HANDS) {
      const channel = this.heldChannels.get(hand)
      if (channel && this.weapons.get(hand)?.id !== channel.weaponId) {
        this.stopHeldChannel(hand)
      }
    }
  }

  private normalizeLoadoutAugments(
    loadout: LastChancesLoadoutDefinition,
  ): LastChancesLoadoutDefinition {
    const normalized = { ...loadout }
    const catalog = new Map(this.config.weapons.map(weapon => [weapon.id, weapon]))
    const primary = catalog.get(normalized.primaryWeaponId)
    const supports = (
      weapon: typeof primary,
      augment: LastChancesAugment | undefined,
    ): LastChancesAugment => (
      augment && augment !== 'none' && weapon?.augmentHooks?.[augment]
        ? augment
        : 'none'
    )
    normalized.primaryAugment = supports(primary, normalized.primaryAugment)
    const primaryMode = primary?.equipMode
    if (primaryMode === 'twoHanded'
      || (primaryMode === 'hybrid' && !normalized.secondaryWeaponId)) {
      normalized.secondaryAugment = normalized.primaryAugment
      return normalized
    }
    const secondary = normalized.secondaryWeaponId
      ? catalog.get(normalized.secondaryWeaponId)
      : undefined
    normalized.secondaryAugment = supports(secondary, normalized.secondaryAugment)
    return normalized
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
    this.activeLoadout = this.config.loadout ? { ...this.config.loadout } : null
    if (this.activeLoadout && this.corpseBoundPrimaryWeaponId) {
      this.activeLoadout = {
        ...this.activeLoadout,
        primaryWeaponId: this.corpseBoundPrimaryWeaponId,
        secondaryWeaponId: null,
      }
    }
    if (this.activeLoadout) {
      this.activeLoadout = this.normalizeLoadoutAugments(this.activeLoadout)
    }
    this.weaponStates.clear()
    this.rebuildWeapons()
    this.enemies = []
    this.projectiles = []
    this.activeAreas = []
    this.effects = []
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.provisionalParry = null
    this.heldChannels.clear()
    this.activeDash = null
    this.roomElapsedMs = 0
    this.hazardHitCycles.clear()
    this.hazardSuppressedUntil.clear()
    this.interactionResolved = false
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
    this.player.stats = copyStats(this.generationBaseStats)
    this.player.hp = this.player.stats.maxHp
    this.player.mentalHealth = this.player.stats.maxMentalHealth
    this.player.invulnerableMs = 0
    this.player.rootMs = 0
    this.player.recoveryMs = 0
    this.player.parryMs = 0
    this.player.armorMultiplier = 1
    this.player.armorMultiplierMs = 0
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

  private capturableKnifeSpider(): RuntimeEnemy | null {
    const candidates = this.enemies
      .filter(enemy => enemy.definition.id === 'spider-knife'
        && enemy.state !== 'dead'
        && enemy.captureWindowMs > 0)
      .map(enemy => {
        const toPlayer = {
          x: this.player.position.x - enemy.position.x,
          y: this.player.position.y - enemy.position.y,
        }
        const distance = vectorLength(toPlayer)
        const behind = normalize(toPlayer, { x: -enemy.facing.x, y: -enemy.facing.y })
        const facingDot = behind.x * enemy.facing.x + behind.y * enemy.facing.y
        return { enemy, distance, facingDot }
      })
      .filter(candidate => (
        candidate.distance <= tuningValue(candidate.enemy.definition, 'captureDistance', 105)
        && candidate.facingDot
          < tuningValue(candidate.enemy.definition, 'captureRearDotMaximum', -0.2)
      ))
      .sort((left, right) => left.distance - right.distance)
    return candidates[0]?.enemy ?? null
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
      const interactionChord = leftPressed && rightPressed && this.interact()
      if (!interactionChord) {
        for (const hand of LAST_CHANCES_HANDS) {
          const pressed = nextButtons[hand]
          if (pressed && !this.gamepadButtons[hand]) this.press(hand)
          if (!pressed && this.gamepadButtons[hand]) this.release(hand)
        }
      }
    } else if (rightPressed && !this.paused && this.phase === 'interaction') {
      const choice = this.currentNode?.interaction?.choices.find(candidate => (
        this.interactionChoiceAvailable(candidate)
      ))
      if (choice) this.chooseInteraction(choice.id)
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
    const isInteractionKey = event.code === 'KeyE'
    if (activeControl && (MOVEMENT_KEYS.has(event.code) || isAttackKey || isInteractionKey)) {
      event.preventDefault()
    }
    if (activeControl && MOVEMENT_KEYS.has(event.code)) this.pressedKeys.add(event.code)
    if (event.repeat) return
    if (activeControl && isInteractionKey) {
      this.interact()
      return
    }
    if (this.config.input.leftKeys.includes(event.code)) this.press('left')
    if (this.config.input.rightKeys.includes(event.code)) this.press('right')
  }

  private readonly handleKeyUp = (event: KeyboardEvent): void => {
    this.pressedKeys.delete(event.code)
    if (this.config.input.leftKeys.includes(event.code)) this.release('left')
    if (this.config.input.rightKeys.includes(event.code)) this.release('right')
  }

  private readonly handleBlur = (): void => {
    this.commitHeldChannels()
    this.commitOrCancelProvisionalParry()
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

  private interactionSnapshot(): LastChancesInteractionSnapshot | null {
    if (this.phase !== 'interaction' || !this.currentNode?.interaction) return null
    return {
      title: this.currentNode.interaction.title,
      body: this.currentNode.interaction.body,
      choices: this.currentNode.interaction.choices.map(choice => ({
        ...(JSON.parse(JSON.stringify(choice)) as LastChancesInteractionChoice),
        available: this.interactionChoiceAvailable(choice),
      })),
    }
  }

  private enemyStatusSnapshot(enemy: RuntimeEnemy): LastChancesEnemySnapshot['statuses'] {
    const result: LastChancesEnemySnapshot['statuses'] = []
    for (const dot of Object.values(enemy.statuses.dots)) {
      if (dot.remainingMs <= 0 || dot.stacks <= 0) continue
      result.push({
        status: dot.kind,
        remainingMs: dot.remainingMs,
        stacks: dot.stacks,
        magnitude: dot.tickDamage,
      })
    }
    const timed: Array<{
      status: LastChancesEnemySnapshot['statuses'][number]['status']
      remainingMs: number
      magnitude: number
    }> = [
      { status: 'stun', remainingMs: enemy.statuses.stunMs, magnitude: 1 },
      { status: 'disarm', remainingMs: enemy.statuses.disarmMs, magnitude: 1 },
      { status: 'healingBlocked', remainingMs: enemy.statuses.antiHealMs, magnitude: 1 },
      { status: 'armorBreak', remainingMs: enemy.statuses.armorBreakMs, magnitude: enemy.statuses.armorBreak },
      { status: 'slow', remainingMs: enemy.statuses.slowMs, magnitude: enemy.statuses.slowMultiplier },
      {
        status: 'attackSlow',
        remainingMs: enemy.statuses.attackSlowMs,
        magnitude: enemy.statuses.attackSlowMultiplier,
      },
      { status: 'opening', remainingMs: enemy.statuses.openingMs, magnitude: 1 },
      { status: 'bound', remainingMs: enemy.statuses.boundMs, magnitude: 1 },
    ]
    timed.forEach((entry) => {
      if (entry.remainingMs <= 0) return
      result.push({ ...entry, stacks: 1 })
    })
    return result
  }

  private createSnapshot(): LastChancesSnapshot {
    const now = performance.now()
    const enemies: LastChancesEnemySnapshot[] = this.enemies.map((enemy) => {
      const profile = this.enemyCombatProfile(enemy)
      return {
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
        role: profile.role,
        attackKind: profile.attackKind,
        attackWindupProgress: enemy.state === 'attacking' && enemy.leapRemainingDistance <= 0
          ? 1 - clamp(enemy.attackWindupMs / Math.max(1, profile.attackWindupMs), 0, 1)
          : 0,
        parryWindowOpen: enemy.state === 'attacking'
          && enemy.leapRemainingDistance <= 0
          && enemy.attackWindupMs > 0
          && enemy.attackWindupMs <= profile.parryWindowMs,
        phaseName: profile.phaseName,
        visible: this.enemyVisible(enemy),
        captureAvailable: enemy.captureWindowMs > 0 && this.capturableKnifeSpider()?.id === enemy.id,
        statuses: this.enemyStatusSnapshot(enemy),
      }
    })
    const cooldowns: LastChancesCooldownSnapshot[] = []
    for (const hand of LAST_CHANCES_HANDS) {
      const weapon = this.weapons.get(hand)
      if (!weapon) continue
      for (const gesture of LAST_CHANCES_GESTURES) {
        const totalMs = gesture === 'tap' ? 0 : weapon.attacks[gesture].cooldownMs
        const remainingMs = gesture === 'tap'
          ? 0
          : Math.max(0, (this.cooldownEnds.get(cooldownKey(hand, gesture)) ?? 0) - this.elapsedMs)
        cooldowns.push({
          hand,
          gesture,
          remainingMs,
          totalMs,
          ready: this.gestureReady(hand, gesture),
        })
      }
    }
    const gestureInputs = LAST_CHANCES_HANDS.map(hand => this.gestures.snapshot(hand, now))
    const actionCues = LAST_CHANCES_HANDS.flatMap((hand) => {
      const weapon = this.weapons.get(hand)
      if (!weapon) return []
      const input = gestureInputs.find(candidate => candidate.hand === hand)
      const gesture = input?.candidateGesture ?? null
      const followUpBecameHold = input?.pressed
        && input.sequence === 'afterHoldTap'
        && input.heldMs >= this.config.input.holdMs
      const firstHoldChargeGesture: LastChancesGesture = weapon.attacks.hold.charge
        ? 'hold'
        : weapon.attacks.holdThenDoubleTap.charge ? 'holdThenDoubleTap' : 'hold'
      const chargeGesture = input?.pressed && input.sequence === 'first'
        ? firstHoldChargeGesture
        : input?.pressed && input.sequence === 'secondTap'
          ? 'doubleTapHold'
          : followUpBecameHold
            ? 'hold'
          : input?.phase === 'holdFollowUpWindow' || input?.sequence === 'afterHoldTap'
            ? 'holdThenDoubleTap'
            : gesture
      const attack = chargeGesture ? weapon.attacks[chargeGesture] : null
      const heldMs = followUpBecameHold
        ? input?.heldMs ?? 0
        : input?.sequence === 'afterHoldTap'
        ? input.pendingChargeMs
        : input?.pressed ? input.heldMs : input?.pendingChargeMs ?? 0
      const state = this.weaponState(weapon)
      const chargeMax = attack?.charge?.maxMs ?? 0
      const recoveryMs = Math.max(
        state.recoveryMs,
        this.player.recoveryMs,
        (this.weaponActionEnds.get(weapon.id) ?? 0) - this.elapsedMs,
      )
      return [{
        hand,
        weaponId: weapon.id,
        phase: recoveryMs > 0
          ? 'recovery' as const
          : !gesture
            ? 'idle' as const
            : attack?.charge && heldMs > 0
              ? heldMs >= attack.charge.maxMs ? 'armed' as const : 'charging' as const
              : 'candidate' as const,
        gesture,
        color: gesture ? LAST_CHANCES_GESTURE_COLORS[gesture] : '#66706c',
        heldMs,
        chargeProgress: chargeMax > 0 ? clamp(heldMs / chargeMax, 0, 1) : 0,
        chargeMaxMs: chargeMax,
        chargeBands: (attack?.charge?.bands ?? []).map((band, index, bands) => ({
          id: band.id,
          label: band.label,
          minMs: band.minMs,
          color: band.color,
          active: heldMs >= band.minMs
            && (bands[index + 1] === undefined || heldMs < bands[index + 1].minMs),
        })),
        recoveryMs,
      }]
    })
    const weaponStates = LAST_CHANCES_HANDS.flatMap((hand) => {
      const weapon = this.weapons.get(hand)
      if (!weapon) return []
      const state = this.weaponState(weapon)
      return [{
        weaponId: weapon.id,
        hand,
        resourceKind: weapon.resource?.kind ?? null,
        resource: state.resource,
        maxResource: state.maxResource,
        resourceLabel: weapon.resource?.label ?? null,
        resourceColor: weapon.resource?.color ?? null,
        storedDot: state.storedDot?.kind ?? null,
        rhythm: state.rhythm,
        recoveryMs: state.recoveryMs,
      }]
    })
    return {
      phase: this.phase,
      paused: this.paused,
      generation: this.generation,
      chances: this.chances,
      totalDeaths: this.totalDeaths,
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
        armorMultiplier: this.player.armorMultiplier,
        armorMultiplierForMs: this.player.armorMultiplierMs,
      },
      enemies,
      projectiles: this.projectiles.map(projectile => ({
        id: projectile.id,
        position: { ...projectile.position },
        radius: projectile.radius,
        color: projectile.color,
        source: projectile.source,
      })),
      hazards: (this.currentNode?.arena.hazards ?? []).map(hazard => ({
        id: hazard.id,
        name: hazard.name,
        kind: hazard.kind,
        active: this.hazardActive(hazard),
      })),
      interaction: this.interactionSnapshot(),
      loadout: this.activeLoadout ? { ...this.activeLoadout } : null,
      cooldowns,
      lastGesture: this.lastGesture ? { ...this.lastGesture } : null,
      gestureInputs,
      actionCues,
      weaponStates,
      interactionPrompt: this.capturableKnifeSpider()
        ? 'E / обе кнопки: схватить Нож-паука со спины'
        : null,
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
    for (const hazard of node.arena.hazards) this.renderHazard(hazard, node)
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
    for (const trace of this.traces) this.renderColliderTrace(trace, node)
    for (const effect of this.effects) this.renderEffect(effect, node)
    this.renderActionCues(node)
  }

  private renderHazard(hazard: LastChancesHazardDefinition, node: LastChancesPlanNode): void {
    const context = this.context
    const points = [
      this.worldToScreen({ x: hazard.x, y: hazard.y }, node),
      this.worldToScreen({ x: hazard.x + hazard.width, y: hazard.y }, node),
      this.worldToScreen({ x: hazard.x + hazard.width, y: hazard.y + hazard.height }, node),
      this.worldToScreen({ x: hazard.x, y: hazard.y + hazard.height }, node),
    ]
    context.save()
    context.globalAlpha = this.hazardActive(hazard) ? 0.48 : 0.12
    context.beginPath()
    points.forEach((point, index) => index === 0
      ? context.moveTo(point.x, point.y)
      : context.lineTo(point.x, point.y))
    context.closePath()
    context.fillStyle = hazard.color
    context.fill()
    context.strokeStyle = hazard.color
    context.lineWidth = this.hazardActive(hazard) ? 2.5 : 1
    context.stroke()
    context.restore()
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
    if (!this.enemyVisible(enemy)
      || enemy.state === 'dead'
      || enemy.state === 'chasing'
      || enemy.state === 'attacking') return
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
    const profile = this.enemyCombatProfile(enemy)
    const visible = this.enemyVisible(enemy)
    context.save()
    context.globalAlpha = visible ? 1 : enemy.state === 'noticing' ? 0.18 : 0.07
    context.save()
    context.translate(point.x, point.y)
    context.scale(1, 0.46)
    context.beginPath()
    context.arc(0, 4, radius * 1.05, 0, Math.PI * 2)
    context.fillStyle = 'rgba(0,0,0,.38)'
    context.fill()
    context.restore()
    this.renderEnemyBody(enemy, point, radius)
    context.strokeStyle = enemy.state === 'attacking' ? '#ff4b4b' : 'rgba(255,255,255,.3)'
    context.lineWidth = enemy.state === 'attacking' ? 3 : 1
    context.stroke()

    if (enemy.state === 'attacking') {
      const windup = 1 - clamp(enemy.attackWindupMs / Math.max(1, profile.attackWindupMs), 0, 1)
      context.beginPath()
      context.arc(point.x, point.y - radius * 0.8, radius * (1.35 + windup * 0.65), 0, Math.PI * 2)
      context.strokeStyle = enemy.attackWindupMs <= profile.parryWindowMs && enemy.attackWindupMs > 0
        ? '#9cf2df'
        : '#ff5964'
      context.lineWidth = 1.5 + windup * 2
      context.stroke()
      if (profile.attackKind === 'leap' && enemy.lockedAttackDirection) {
        const target = this.worldToScreen({
          x: enemy.position.x + enemy.lockedAttackDirection.x * profile.leapDistance,
          y: enemy.position.y + enemy.lockedAttackDirection.y * profile.leapDistance,
        }, node)
        context.beginPath()
        context.moveTo(point.x, point.y - radius)
        context.lineTo(target.x, target.y - radius)
        context.setLineDash([5, 5])
        context.strokeStyle = 'rgba(255, 89, 100, .7)'
        context.stroke()
        context.setLineDash([])
      }
    }

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
    if (profile.phaseName) {
      context.font = `600 ${Math.max(8, radius * 0.62)}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = '#c7a56b'
      context.fillText(profile.phaseName, point.x, point.y + radius * 1.15)
    }
    if (enemy.statuses.openingMs > 0) {
      context.save()
      context.translate(point.x, point.y - radius * 3.1)
      context.rotate(Math.PI / 4)
      context.fillStyle = '#66d6ff'
      context.fillRect(-4, -4, 8, 8)
      context.restore()
    }
    if (this.activeAreas.some(area => area.attack.behavior === 'spearSpin')) {
      const headWorld = {
        x: enemy.position.x + enemy.facing.x * enemy.definition.radius * 0.55,
        y: enemy.position.y + enemy.facing.y * enemy.definition.radius * 0.55,
      }
      const head = this.worldToScreen(headWorld, node)
      context.beginPath()
      context.arc(head.x, head.y - radius * 0.8, Math.max(3, radius * 0.34), 0, Math.PI * 2)
      context.strokeStyle = '#fff0a8'
      context.lineWidth = 1.5
      context.stroke()
    }
    if (enemy.criticalHitMs > 0) {
      context.font = `900 ${Math.max(9, radius * 0.68)}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = '#fff0a8'
      context.fillText('CRIT', point.x, point.y - radius * 3.7)
    }
    if (enemy.captureWindowMs > 0) {
      context.font = `800 ${Math.max(10, radius * 0.75)}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = '#64e6a5'
      context.fillText('E · СХВАТИТЬ', point.x, point.y - radius * 3.35)
    }
    const statusMarkers: Array<[boolean, string, string]> = [
      [enemy.statuses.dots.bleed.stacks > 0, '#d34b58', 'B'],
      [enemy.statuses.dots.poison.stacks > 0, '#8ccb62', 'P'],
      [enemy.statuses.dots.burn.stacks > 0, '#ff8d4b', 'F'],
      [enemy.statuses.dots.chemical.stacks > 0, '#65d7ce', 'C'],
      [enemy.statuses.stunMs > 0, '#f2d46d', '★'],
      [enemy.statuses.disarmMs > 0, '#b8c3ca', 'D'],
      [enemy.statuses.antiHealMs > 0, '#f47bb4', 'H'],
      [enemy.statuses.armorBreakMs > 0, '#d49a70', 'A'],
      [enemy.statuses.slowMs > 0, '#75aef2', 'M'],
      [enemy.statuses.attackSlowMs > 0, '#ad8df2', 'R'],
      [enemy.statuses.boundMs > 0, '#9cc8ff', '⛓'],
    ]
    let statusX = point.x - (statusMarkers.filter(([active]) => active).length - 1) * 5
    for (const [active, color, label] of statusMarkers) {
      if (!active) continue
      context.beginPath()
      context.arc(statusX, point.y + radius * 1.48, 4.4, 0, Math.PI * 2)
      context.fillStyle = color
      context.fill()
      context.font = '700 5px system-ui'
      context.textAlign = 'center'
      context.textBaseline = 'middle'
      context.fillStyle = '#101313'
      context.fillText(label, statusX, point.y + radius * 1.48 + 0.4)
      statusX += 10
    }
    context.restore()
  }

  private renderEnemyBody(enemy: RuntimeEnemy, point: LastChancesVector, radius: number): void {
    const context = this.context
    context.beginPath()
    if (enemy.definition.id === 'spider-knife') {
      for (const side of [-1, 1]) {
        for (let leg = 0; leg < 3; leg += 1) {
          const y = point.y - radius * (1.2 - leg * 0.42)
          context.moveTo(point.x + side * radius * 0.35, y)
          context.lineTo(point.x + side * radius * 1.45, y + (leg - 1) * radius * 0.35)
        }
      }
      context.strokeStyle = enemy.definition.color
      context.lineWidth = Math.max(1.5, radius * 0.18)
      context.stroke()
      context.beginPath()
      context.moveTo(point.x, point.y - radius * 2)
      context.lineTo(point.x + radius * 0.58, point.y - radius * 0.65)
      context.lineTo(point.x, point.y + radius * 0.25)
      context.lineTo(point.x - radius * 0.58, point.y - radius * 0.65)
      context.closePath()
    } else if (enemy.definition.id === 'invisible-wolf') {
      context.moveTo(point.x - radius, point.y - radius * 0.35)
      context.lineTo(point.x - radius * 0.45, point.y - radius * 1.85)
      context.lineTo(point.x, point.y - radius * 1.25)
      context.lineTo(point.x + radius * 0.45, point.y - radius * 1.85)
      context.lineTo(point.x + radius, point.y - radius * 0.35)
      context.lineTo(point.x, point.y + radius * 0.15)
      context.closePath()
    } else if (enemy.definition.id === 'running-stapler') {
      context.roundRect(
        point.x - radius * 1.2,
        point.y - radius * 1.45,
        radius * 2.4,
        radius * 1.25,
        radius * 0.24,
      )
    } else if (enemy.definition.id === 'infinite-cube') {
      context.rect(point.x - radius, point.y - radius * 1.8, radius * 2, radius * 2)
    } else {
      context.arc(point.x, point.y - radius * 0.8, radius, 0, Math.PI * 2)
    }
    context.fillStyle = enemy.definition.color
    context.fill()
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
    context.save()
    if (this.player.invulnerableMs > 0) {
      context.globalAlpha = 0.32
      context.shadowColor = '#bffcff'
      context.shadowBlur = 16
    }
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
    if (this.player.armorMultiplier > 1 && this.player.armorMultiplierMs > 0) {
      context.beginPath()
      context.arc(point.x, point.y - radius, radius * 1.45, Math.PI * 0.15, Math.PI * 0.85)
      context.strokeStyle = '#9cc8ff'
      context.lineWidth = 4
      context.stroke()
      context.font = `800 ${Math.max(8, radius * 0.65)}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = '#c9e4ff'
      context.fillText(
        `ARMOR ×${this.player.armorMultiplier.toFixed(0)}`,
        point.x,
        point.y - radius * 2.65,
      )
    }
    context.restore()
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
    if (projectile.source === 'enemy') {
      this.context.strokeStyle = '#ff5964'
      this.context.lineWidth = 2
      this.context.stroke()
    }
    this.context.shadowBlur = 0
  }

  private renderColliderTrace(
    trace: RuntimeColliderTrace,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const alpha = clamp(trace.remainingMs / Math.max(1, trace.totalMs), 0, 1)
    context.save()
    context.globalAlpha = alpha * 0.62
    context.strokeStyle = trace.color
    context.fillStyle = trace.color
    context.lineWidth = 1.2 + alpha * 2.4
    context.shadowColor = trace.color
    context.shadowBlur = 8 * alpha
    for (const path of colliderTracePath(trace.collider)) {
      if (path.points.length === 0) continue
      context.beginPath()
      path.points.forEach((world, index) => {
        const point = this.worldToScreen(world, node)
        if (index === 0) context.moveTo(point.x, point.y)
        else context.lineTo(point.x, point.y)
      })
      if (path.closed) context.closePath()
      context.stroke()
      context.globalAlpha = alpha * 0.08
      if (path.closed) context.fill()
      context.globalAlpha = alpha * 0.62
    }
    context.restore()
  }

  private renderActionCues(node: LastChancesPlanNode): void {
    const context = this.context
    const origin = this.worldToScreen(this.player.position, node)
    const now = this.frameNowMs || performance.now()
    for (const [handIndex, hand] of LAST_CHANCES_HANDS.entries()) {
      const weapon = this.weapons.get(hand)
      if (!weapon) continue
      const input = this.gestures.snapshot(hand, now)
      const candidate = input.candidateGesture
      const followUpBecameHold = input.pressed
        && input.sequence === 'afterHoldTap'
        && input.heldMs >= this.config.input.holdMs
      const firstHoldChargeGesture: LastChancesGesture = weapon.attacks.hold.charge
        ? 'hold'
        : weapon.attacks.holdThenDoubleTap.charge ? 'holdThenDoubleTap' : 'hold'
      const chargeGesture = input.pressed && input.sequence === 'first'
        ? firstHoldChargeGesture
        : input.pressed && input.sequence === 'secondTap'
          ? 'doubleTapHold'
          : followUpBecameHold
            ? 'hold'
          : input.phase === 'holdFollowUpWindow' || input.sequence === 'afterHoldTap'
            ? 'holdThenDoubleTap'
            : candidate
      const attack = chargeGesture ? weapon.attacks[chargeGesture] : null
      const heldMs = followUpBecameHold
        ? input.heldMs
        : input.sequence === 'afterHoldTap'
        ? input.pendingChargeMs
        : input.pressed ? input.heldMs : input.pendingChargeMs
      const baseRadius = handIndex === 0 ? 32 : 25
      const centerY = origin.y - 30

      context.save()
      context.lineCap = 'round'
      if (attack?.charge && (input.pressed || input.phase === 'holdFollowUpWindow')) {
        const bands = [...attack.charge.bands].sort((left, right) => left.minMs - right.minMs)
        bands.forEach((band, index) => {
          const nextMs = bands[index + 1]?.minMs ?? attack.charge?.maxMs ?? band.minMs
          const start = -Math.PI / 2 + band.minMs / attack.charge!.maxMs * Math.PI * 2
          const end = -Math.PI / 2 + nextMs / attack.charge!.maxMs * Math.PI * 2 - 0.035
          context.beginPath()
          context.arc(origin.x, centerY, baseRadius, start, end)
          context.strokeStyle = band.color
          context.globalAlpha = heldMs >= band.minMs ? 0.95 : 0.22
          context.lineWidth = heldMs >= band.minMs ? 5 : 3
          context.stroke()
        })
        const progress = clamp(heldMs / attack.charge.maxMs, 0, 1)
        context.beginPath()
        context.arc(origin.x, centerY, baseRadius + 5, -Math.PI / 2, -Math.PI / 2 + progress * Math.PI * 2)
        context.strokeStyle = candidate ? LAST_CHANCES_GESTURE_COLORS[candidate] : '#fff'
        context.globalAlpha = 0.9
        context.lineWidth = 2
        context.stroke()
      }

      LAST_CHANCES_GESTURES.forEach((gesture, index) => {
        const angle = -Math.PI / 2 + index / LAST_CHANCES_GESTURES.length * Math.PI * 2
        const pipRadius = baseRadius + 11
        const x = origin.x + Math.cos(angle) * pipRadius
        const y = centerY + Math.sin(angle) * pipRadius
        const ready = this.gestureReady(hand, gesture)
        context.beginPath()
        context.arc(x, y, candidate === gesture ? 4.2 : 2.7, 0, Math.PI * 2)
        context.fillStyle = LAST_CHANCES_GESTURE_COLORS[gesture]
        context.globalAlpha = ready ? candidate === gesture ? 1 : 0.72 : 0.13
        context.fill()
        if (candidate === gesture) {
          context.strokeStyle = '#fff'
          context.lineWidth = 1
          context.stroke()
        }
      })
      context.restore()
    }
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
    } else if (this.phase === 'interaction') {
      title = 'THE ROOM OFFERS A CHOICE'
      subtitle = this.currentNode?.interaction?.title ?? 'Choose what the attempt carries forward'
    }
    context.fillText(title, this.cssWidth / 2, centerY - 8)
    context.font = '500 13px system-ui'
    context.fillStyle = 'rgba(255,255,255,.64)'
    context.fillText(subtitle, this.cssWidth / 2, centerY + 22)
  }
}
