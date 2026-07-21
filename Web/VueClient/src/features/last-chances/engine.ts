import {
  cloneLastChancesConfig,
  DEFAULT_LAST_CHANCES_BAND_TICK,
  DEFAULT_LAST_CHANCES_GATE_TICK,
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
  DualSenseControlRecognizer,
  MylorikControlRecognizer,
  physicalClusterToRuntimeHand,
  runtimeHandToPhysicalCluster,
  type LastChancesControlSource,
  type LastChancesSemanticInputEvent,
} from './control-schemes'
import {
  createLastChancesGamepadAdapter,
  type LastChancesGamepadAdapter,
  type LastChancesGamepadReading,
} from './gamepad'
import { buildLastChancesPlan } from './plan'
import type { LastChancesFeedbackPreferences } from './preferences'
import { resolveLastChancesSpearChargeVisual } from './spear-visuals'
import {
  DualSenseFeedbackController,
  type LastChancesHapticGamepadLike,
} from './feedback'
import { createLastChancesDualSenseEnhancedOutput } from './dualsense-serializers'
import { createLastChancesRng, lastChancesRandomInt } from './rng'
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
  LastChancesAdaptiveTriggerProfileDefinition,
  LastChancesArenaEdge,
  LastChancesArtifactDefinition,
  LastChancesAttackDefinition,
  LastChancesAttackBehavior,
  LastChancesAttackSetControlDefinition,
  LastChancesAugment,
  LastChancesConfig,
  LastChancesControlContext,
  LastChancesControlRoleSnapshot,
  LastChancesControlScheme,
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
  LastChancesGateTickDefinition,
  LastChancesGesture,
  LastChancesGestureInputSnapshot,
  LastChancesGestureResolution,
  LastChancesGestureSnapshot,
  LastChancesHand,
  LastChancesHitEffectDefinition,
  LastChancesHazardDefinition,
  LastChancesMoveQuestSnapshot,
  LastChancesBossHoleDefinition,
  LastChancesBossAltarDefinition,
  LastChancesOutfitDefinition,
  LastChancesZoneShape,
  LastChancesInteractionChoice,
  LastChancesInteractionSnapshot,
  LastChancesLoadoutDefinition,
  LastChancesObstacleDefinition,
  LastChancesPhase,
  LastChancesPlanNode,
  LastChancesResolvedWeapon,
  LastChancesSnapshot,
  LastChancesSemanticControlCue,
  LastChancesStats,
  LastChancesTactileProfile,
  LastChancesTurretDefinition,
  LastChancesVector,
} from './types'

export interface LastChancesEngineOptions {
  controlScheme?: LastChancesControlScheme
  feedbackPreferences?: LastChancesFeedbackPreferences
  /** Explicit opt-in fixture used by browser/manual control-routing QA. */
  qaFixture?: 'controls'
}

/** Base under a partial baseTrigger/detent merge; all-zero = fully relaxed trigger. */
const RELAXED_TRIGGER_PROFILE: LastChancesAdaptiveTriggerProfileDefinition = {
  startPosition: 0,
  endPosition: 0,
  resistance: 0,
  force: 0,
  transitionMs: 0,
  effectMs: 0,
  magnitude: 0,
}

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
  /** Last player hit; kills are attributed to it for the move-unlock quests. */
  lastPlayerHit: { hand: LastChancesHand, gesture: LastChancesGesture } | null
  /** Per-hand gestures that have landed on this enemy; the elite combo quest reads it on death. */
  gestureHits: Record<LastChancesHand, Set<LastChancesGesture>>
  /** An opened target touched by Oberhaw refreshes its cooldown on any later death. */
  swordExecutionMarked: boolean
  /** Swarm cockroaches spawn outside the arena and skip the bounds clamp until fully inside. */
  entering: boolean
  motherRetreatsTriggered: number
  motherRetreat: RuntimeMotherRetreat | null
}

interface RuntimeMotherRetreat {
  stage: 'approaching' | 'hidden'
  entranceHoleId: string
  exitHoleId: string
  detonateAtMs: number
}

interface RuntimeGroundWeapon {
  id: string
  weaponId: string
  augment: LastChancesAugment
  position: LastChancesVector
}

interface RuntimeRewardChest {
  position: LastChancesVector
  opened: boolean
}

interface RuntimeZoneAttack {
  shape: LastChancesZoneShape
  center: LastChancesVector
  size: number
  rotationRadians: number
  spawnedAtMs: number
  detonateAtMs: number
  damageMaxHpRatio: number
  sourceName: string
}

interface RuntimeSwarmSpawner {
  definition: LastChancesEnemyDefinition
  edges: [LastChancesArenaEdge, LastChancesArenaEdge]
  remaining: number
  total: number
  spawnedCount: number
  nextSpawnAtMs: number
  rng: () => number
  infinite: boolean
}

interface RuntimeTurret {
  definition: LastChancesTurretDefinition
  facing: LastChancesVector
  disabled: boolean
  seesPlayer: boolean
  fireCooldownMs: number
}

interface RuntimeHoleStrike {
  holeId: string
  center: LastChancesVector
  radius: number
  damageMaxHpRatio: number
  spawnedAtMs: number
  detonateAtMs: number
  sourceName: string
}

interface RuntimeBossCheckpoint {
  nodeId: string
  attemptPath: string[]
  loadout: LastChancesLoadoutDefinition | null
}

interface HandMoveQuestState {
  unlocked: Record<LastChancesGesture, boolean>
  /** Earned unlocks that activate on the next room entry. */
  pendingUnlocks: LastChancesGesture[]
  roomKills: { tap: number, hold: number }
  tapQuestDone: boolean
  holdQuestDone: boolean
  comboQuestDone: boolean
}

const MOVE_QUEST_KILLS_REQUIRED = 2
const MOVE_QUEST_COMBO_GESTURES: readonly LastChancesGesture[] = [
  'tap',
  'hold',
  'doubleTap',
  'holdThenDoubleTap',
]

function createHandMoveQuestState(unlockAll = false): HandMoveQuestState {
  return {
    unlocked: {
      tap: true,
      hold: true,
      doubleTap: unlockAll,
      doubleTapHold: unlockAll,
      holdThenDoubleTap: unlockAll,
    },
    pendingUnlocks: [],
    roomKills: { tap: 0, hold: 0 },
    tapQuestDone: unlockAll,
    holdQuestDone: unlockAll,
    comboQuestDone: unlockAll,
  }
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
  gesture?: LastChancesGesture
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
  gesture?: LastChancesGesture
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
  gesture?: LastChancesGesture
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
  /** Horizontal pointer travel matching a direction-assisted basic sweep. */
  matchingAimMotionPx: number
  /** Current nonlinear direction-assisted damage bonus. */
  motionDamageBonus: number
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
  /** Nonlinear Sword impact strength, 0–1. */
  intensity?: number
}

interface RuntimeColliderTrace {
  collider: LastChancesRuntimeCollider
  color: string
  remainingMs: number
  totalMs: number
}

interface RuntimeSpearSpriteLayout {
  center: LastChancesVector
  axis: LastChancesVector
  perpendicular: LastChancesVector
  width: number
  height: number
  pivotRatio: number
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
  targetPosition?: LastChancesVector
  targetEnemyId?: string | null
}

interface RuntimeSwordAdvance {
  weaponId: string
  direction: LastChancesVector
  remainingDistance: number
  speed: number
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
  perfectTimingMs: number
  fatigueMs: number
  roomTimingMisses: number
  consecutiveTimingMisses: number
  fatigueTriggeredByTapAtMs: number
  unterhauDueAtMs: number
  unterhauTargetId: string | null
  unterhauTargetPosition: LastChancesVector | null
  unterhauPrimed: boolean
  lastMotionDamageBonus: number
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
  gesture?: LastChancesGesture
  storedDot?: LastChancesStoredDot | null
  distance?: number
  damageMultiplier?: number
  impactIntensity?: number
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

/** Zone-local vertices before the zone's own rotation is applied. */
function zoneShapeLocalVertices(zone: { shape: LastChancesZoneShape, size: number }): LastChancesVector[] {
  if (zone.shape === 'square') {
    return [
      { x: -zone.size, y: -zone.size },
      { x: zone.size, y: -zone.size },
      { x: zone.size, y: zone.size },
      { x: -zone.size, y: zone.size },
    ]
  }
  // Equilateral triangle with circumradius `size`.
  return [0, 1, 2].map((step) => {
    const angle = -Math.PI / 2 + step * (Math.PI * 2 / 3)
    return { x: Math.cos(angle) * zone.size, y: Math.sin(angle) * zone.size }
  })
}

function pointToSegmentDistanceSquared(
  point: LastChancesVector,
  start: LastChancesVector,
  end: LastChancesVector,
): number {
  const segment = { x: end.x - start.x, y: end.y - start.y }
  const lengthSquared = segment.x * segment.x + segment.y * segment.y
  const t = lengthSquared <= EPSILON
    ? 0
    : clamp(((point.x - start.x) * segment.x + (point.y - start.y) * segment.y) / lengthSquared, 0, 1)
  return distanceSquared(point, { x: start.x + segment.x * t, y: start.y + segment.y * t })
}

function circleOverlapsConvexPolygon(
  center: LastChancesVector,
  radius: number,
  vertices: LastChancesVector[],
): boolean {
  let inside = true
  for (let index = 0; index < vertices.length; index += 1) {
    const start = vertices[index]
    const end = vertices[(index + 1) % vertices.length]
    const cross = (end.x - start.x) * (center.y - start.y) - (end.y - start.y) * (center.x - start.x)
    if (cross < 0) inside = false
    if (pointToSegmentDistanceSquared(center, start, end) <= radius * radius) return true
  }
  return inside
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

function isCockroachDefinition(definition: LastChancesEnemyDefinition): boolean {
  return definition.swarm !== undefined
    && definition.maxHp === 1
    && definition.attackDamage === 1
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

function colliderOrigin(collider: LastChancesRuntimeCollider): LastChancesVector {
  if (collider.shape === 'sector') return collider.origin
  if (collider.shape === 'circle') return collider.center
  if (collider.shape === 'sweep') return collider.pivot
  return collider.start
}

export class LastChancesEngine {
  readonly config: LastChancesConfig

  private readonly canvas: HTMLCanvasElement
  private readonly context: CanvasRenderingContext2D
  private readonly callbacks: LastChancesEngineCallbacks
  private readonly enemyDefinitions: Map<string, LastChancesEnemyDefinition>
  private readonly weapons = new Map<LastChancesHand, LastChancesResolvedWeapon>()
  private readonly gestures: LastChancesGestureRecognizer
  private readonly mylorikControls: MylorikControlRecognizer
  private readonly dualSenseControls: DualSenseControlRecognizer
  private readonly pressedKeys = new Set<string>()
  private readonly cooldownEnds = new Map<string, number>()
  private readonly tapCombos: Record<LastChancesHand, RuntimeTapCombo> = {
    left: { step: 0, expiresAtMs: 0 },
    right: { step: 0, expiresAtMs: 0 },
  }
  private readonly gamepadButtons: Record<LastChancesHand, boolean> = { left: false, right: false }
  private gamepadAdapter: LastChancesGamepadAdapter
  private controlSchemeValue: LastChancesControlScheme
  private readonly qaControlsFixture: boolean
  private controlCue: LastChancesSemanticControlCue | null = null
  private feedbackPreferences: LastChancesFeedbackPreferences
  private feedbackController: DualSenseFeedbackController

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
  private groundWeapons: RuntimeGroundWeapon[] = []
  private rewardChest: RuntimeRewardChest | null = null
  private nextGroundWeaponId = 1
  private ninjaDashReadyAtMs = 0
  /** The page-owned route overlay suppresses cleared-room exploration while it is visible. */
  private routeMapVisible = true
  private currentNode: LastChancesPlanNode | null = null
  private availableNodeIds: string[] = []
  private attemptPath: string[] = []
  private deathReason: string | null = null
  private enemies: RuntimeEnemy[] = []
  private zoneAttacks: RuntimeZoneAttack[] = []
  private holeStrikes: RuntimeHoleStrike[] = []
  private swarmSpawner: RuntimeSwarmSpawner | null = null
  private turrets: RuntimeTurret[] = []
  private turretAlarmMs = 0
  private altarPromptActive = false
  private bossCheckpoint: RuntimeBossCheckpoint | null = null
  private cockroachesExtinct = false
  private moveQuests: Record<LastChancesHand, HandMoveQuestState> = {
    left: createHandMoveQuestState(),
    right: createHandMoveQuestState(),
  }
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
  private activeSwordAdvance: RuntimeSwordAdvance | null = null
  private nextProjectileId = 1
  private lastGesture: LastChancesGestureSnapshot | null = null
  private player: RuntimePlayer
  private touchMove: LastChancesVector = { x: 0, y: 0 }
  private touchAim: LastChancesVector = { x: 0, y: 0 }
  private gamepadMove: LastChancesVector = { x: 0, y: 0 }
  private gamepadAim: LastChancesVector = { x: 0, y: 0 }
  /** Keeps a centered right stick from handing aim back to an older pointer position. */
  private retainedGamepadAim: LastChancesVector | null = null
  private pointerAim: LastChancesVector = { x: 1, y: 0 }
  private pointerClientX: number | null = null
  private pointerDeltaX = 0
  private readonly immediateSwordInput: Record<LastChancesHand, {
    firstTapExecuted: boolean
    oberhauExecuted: boolean
    unterhauExecuted: boolean
  }> = {
    left: { firstTapExecuted: false, oberhauExecuted: false, unterhauExecuted: false },
    right: { firstTapExecuted: false, oberhauExecuted: false, unterhauExecuted: false },
  }
  private selectedNodeId: string | null = null
  private selectedInteractionChoiceId: string | null = null
  private gamepadMenuAxisEngaged = false
  private gamepadChoiceAxisEngaged = false
  private gamepadNeedsReseed = false
  private activeMobilityPhysicalHand: LastChancesHand | null = null
  private keyboardMobilityStartedAt = 0
  private keyboardMobilityCommitted = false
  private readonly keyboardDualSenseTriggers: Record<LastChancesHand, {
    down: boolean
    startedAt: number
    gateIndex: number
  }> = {
    left: { down: false, startedAt: 0, gateIndex: 0 },
    right: { down: false, startedAt: 0, gateIndex: 0 },
  }
  private readonly gamepadControlButtons = {
    leftStrike: false,
    rightStrike: false,
    leftTechnique: false,
    rightTechnique: false,
    mobility: false,
    interact: false,
    circle: false,
    cross: false,
    options: false,
    dpadUp: false,
    dpadDown: false,
    dpadLeft: false,
    dpadRight: false,
  }
  private readonly gamepadTriggerValues: Record<LastChancesHand, number> = { left: 0, right: 0 }
  private readonly feedbackChargeBandIds: Record<LastChancesHand, string | null> = {
    left: null,
    right: null,
  }
  /** Active per-physical-hand trigger detent (the current node's merged profile); null = resting. */
  private readonly triggerDetents: Record<LastChancesHand, LastChancesAdaptiveTriggerProfileDefinition | null> = {
    left: null,
    right: null,
  }
  /** Living spider-knife escape-burst scheduler; null while no wriggling weapon is equipped. */
  private spiderWriggle: { rng: () => number, nextAtMs: number } | null = null
  /** Runtime availability edges already advertised to the DualSense feedback layer. */
  private continuationFeedbackWindowKeys = new Set<string>()
  /** Successful hits may open data-authored generic continuation windows. */
  private readonly feedbackHitWindowEnds: Record<LastChancesHand, number> = {
    left: Number.NEGATIVE_INFINITY,
    right: Number.NEGATIVE_INFINITY,
  }
  private gamepadState: LastChancesGamepadSnapshot
  private cssWidth = 800
  private cssHeight = 600
  private dpr = 1
  private frameNowMs = 0
  private spearImage: HTMLImageElement | null = null
  private readonly resizeObserver: ResizeObserver | null

  constructor(
    canvas: HTMLCanvasElement,
    config: LastChancesConfig,
    callbacks: LastChancesEngineCallbacks = {},
    options: LastChancesEngineOptions = {},
  ) {
    const migratedConfig = migrateLastChancesConfig(config) as LastChancesConfig
    const validation = validateLastChancesConfig(migratedConfig)
    if (!validation.valid) throw new LastChancesConfigError('Invalid 99LC engine config', validation.errors)
    const context = canvas.getContext('2d')
    if (!context) throw new Error('99LC requires a Canvas 2D rendering context')

    this.canvas = canvas
    this.context = context
    if (typeof Image !== 'undefined') {
      this.spearImage = new Image()
      this.spearImage.decoding = 'async'
      this.spearImage.onload = () => {
        if (!this.destroyed) this.render()
      }
      this.spearImage.src = '/99lc/twohand-spear.png'
    }
    this.callbacks = callbacks
    this.config = cloneLastChancesConfig(migratedConfig)
    if (!this.config.input.mylorik || !this.config.input.dualsense) {
      throw new Error('99LC schema v4 control definitions are required')
    }
    this.controlSchemeValue = options.controlScheme ?? 'legacy'
    this.qaControlsFixture = options.qaFixture === 'controls'
    if (this.qaControlsFixture || this.config.progression.moveQuestsEnabled === false) {
      this.moveQuests = {
        left: createHandMoveQuestState(true),
        right: createHandMoveQuestState(true),
      }
    }
    this.feedbackPreferences = options.feedbackPreferences ?? { mode: 'full', intensity: 1 }
    this.feedbackController = new DualSenseFeedbackController(
      this.config.input.dualsense.feedback,
      this.feedbackPreferences,
      { enhanced: createLastChancesDualSenseEnhancedOutput() },
    )
    this.gamepadAdapter = createLastChancesGamepadAdapter({
      deadZone: this.config.input.gamepadDeadZone,
      leftButton: this.config.input.gamepadLeftButton,
      rightButton: this.config.input.gamepadRightButton,
      analogTriggerThreshold: this.config.input.dualsense.activationThreshold,
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
      this.handleLegacyGestureResolution(resolution)
    })
    this.mylorikControls = new MylorikControlRecognizer(
      this.config.input.mylorik,
      event => this.handleSemanticInput(event),
    )
    this.dualSenseControls = new DualSenseControlRecognizer(
      this.config.input.dualsense,
      event => this.handleSemanticInput(event),
    )
    this.availableNodeIds = this.plan.tiers[0].map(node => node.id)
    this.selectedNodeId = this.controlSchemeValue === 'dualsense'
      ? null
      : this.availableNodeIds[0] ?? null
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

  get controlScheme(): LastChancesControlScheme {
    return this.controlSchemeValue
  }

  setControlScheme(scheme: LastChancesControlScheme): void {
    if (scheme === this.controlSchemeValue || this.destroyed) return
    this.cleanupControlInputsForReplacement()
    this.controlSchemeValue = scheme
    this.pushTriggerBaseline()
    if (scheme === 'dualsense' && this.phase === 'planning') this.selectedNodeId = null
    if (scheme !== 'dualsense' && this.phase === 'planning' && !this.selectedNodeId) {
      this.selectedNodeId = this.availableNodeIds[0] ?? null
    }
    this.controlCue = {
      hand: null,
      intent: null,
      state: 'ready',
      gesture: null,
      label: scheme === 'legacy' ? 'DeepList' : scheme === 'mylorik' ? 'mylorik' : 'DualSense',
      tactileProfile: 'click',
      atMs: this.elapsedMs,
    }
    this.render()
    this.emitSnapshot(true)
  }

  setFeedbackPreferences(preferences: LastChancesFeedbackPreferences): void {
    this.feedbackPreferences = {
      mode: preferences.mode,
      intensity: clamp(preferences.intensity, 0, 1),
    }
    void this.feedbackController.setPreferences(this.feedbackPreferences)
    this.emitSnapshot(true)
  }

  setRouteMapVisible(visible: boolean): void {
    this.routeMapVisible = visible
    if (visible) this.cleanupControlInputs(false)
    this.render()
  }

  async enableDualSenseFeatures(): Promise<boolean> {
    const enabled = await this.feedbackController.enableEnhancedFeatures()
    if (enabled) this.pushTriggerBaseline()
    this.emitSnapshot(true)
    return enabled
  }

  /** The equipped set's resting trigger block for a physical hand, or null to relax it. */
  private restingTriggerProfile(
    physicalHand: LastChancesHand,
  ): LastChancesAdaptiveTriggerProfileDefinition | null {
    if (this.controlSchemeValue !== 'dualsense') return null
    const baseTrigger = this.weapons.get(physicalClusterToRuntimeHand(physicalHand))
      ?.controls?.dualsense.haptics?.baseTrigger
    return baseTrigger ? { ...RELAXED_TRIGGER_PROFILE, ...baseTrigger } : null
  }

  /**
   * Persistent Tier-2 trigger state: the active node's detent when a combo is
   * in flight, else the weapon's resting block — so gates are physically felt
   * between effects, not only while one plays.
   */
  private pushTriggerBaseline(): void {
    void this.feedbackController.setTriggerBaseline({
      left: this.triggerDetents.left ?? this.restingTriggerProfile('left'),
      right: this.triggerDetents.right ?? this.restingTriggerProfile('right'),
    })
  }

  /** Moves a hand's detent to the entered node's merged profile ("moving detent" gearbox feel). */
  private armTriggerDetent(
    physicalHand: LastChancesHand,
    profile: LastChancesTactileProfile,
    adaptiveOverride?: Partial<LastChancesAdaptiveTriggerProfileDefinition>,
  ): void {
    const base = this.config.input.dualsense?.feedback.profiles[profile]
    if (!base) return
    this.triggerDetents[physicalHand] = { ...base, ...adaptiveOverride }
    this.pushTriggerBaseline()
  }

  private releaseTriggerDetent(physicalHand: LastChancesHand): void {
    if (!this.triggerDetents[physicalHand]) return
    this.triggerDetents[physicalHand] = null
    this.pushTriggerBaseline()
  }

  disableEnhancedFeedback(): void {
    void this.feedbackController.disableEnhancedFeatures().finally(() => this.emitSnapshot(true))
  }

  /**
   * Applies only the control-recognition definition. Combat/run data in the supplied
   * Builder draft is intentionally ignored so this operation cannot reset the attempt.
   */
  applyControlDefinition(nextConfig: LastChancesConfig): boolean {
    const migrated = migrateLastChancesConfig(nextConfig) as LastChancesConfig
    const validation = validateLastChancesConfig(migrated)
    if (!validation.valid || !migrated.input.mylorik || !migrated.input.dualsense) return false
    this.cleanupControlInputsForReplacement()
    this.config.input = JSON.parse(JSON.stringify(migrated.input)) as LastChancesConfig['input']
    const nextWeapons = new Map(migrated.weapons.map(weapon => [weapon.id, weapon]))
    for (const weapon of this.config.weapons) {
      const next = nextWeapons.get(weapon.id)
      weapon.controls = next?.controls
        ? JSON.parse(JSON.stringify(next.controls)) as typeof next.controls
        : undefined
    }
    this.gestures.updateTimings(this.config.input)
    this.mylorikControls.updateConfig(this.config.input.mylorik)
    this.dualSenseControls.updateConfig(this.config.input.dualsense)
    const previousFeedbackController = this.feedbackController
    const feedbackCleanup = previousFeedbackController.dispose()
    const nextFeedbackController = new DualSenseFeedbackController(
      this.config.input.dualsense.feedback,
      this.feedbackPreferences,
      { enhanced: createLastChancesDualSenseEnhancedOutput() },
    )
    nextFeedbackController.waitForOutputBarrier(feedbackCleanup)
    this.feedbackController = nextFeedbackController
    this.gamepadAdapter = createLastChancesGamepadAdapter({
      deadZone: this.config.input.gamepadDeadZone,
      leftButton: this.config.input.gamepadLeftButton,
      rightButton: this.config.input.gamepadRightButton,
      analogTriggerThreshold: this.config.input.dualsense.activationThreshold,
    })
    this.rebuildWeapons()
    this.emitSnapshot(true)
    return true
  }

  destroy(): void {
    if (this.destroyed) return
    this.destroyed = true
    this.started = false
    if (this.frameId !== null) cancelAnimationFrame(this.frameId)
    this.frameId = null
    this.resizeObserver?.disconnect()
    this.detachEvents()
    this.cleanupControlInputs(false)
    this.activeAreas = []
    this.heldChannels.clear()
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.provisionalParry = null
    void this.feedbackController.dispose()
  }

  setPaused(paused: boolean): void {
    if (this.paused === paused || this.destroyed || (this.altarPromptActive && !paused)) return
    if (paused) {
      this.commitHeldChannels()
      this.commitOrCancelProvisionalParry()
    }
    this.paused = paused
    this.lastFrameMs = performance.now()
    if (paused) {
      this.cleanupControlInputs(false)
    }
    this.render()
    this.emitSnapshot(true)
  }

  chooseNode(nodeId: string): boolean {
    if (this.phase !== 'planning' || this.paused || !this.availableNodeIds.includes(nodeId)) return false
    const node = this.plan.nodes.find(candidate => candidate.id === nodeId)
    if (!node) return false
    this.currentNode = node
    this.routeMapVisible = false
    this.attemptPath.push(node.id)
    this.availableNodeIds = []
    this.selectedNodeId = null
    this.selectedInteractionChoiceId = null
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
    this.activeSwordAdvance = null
    this.roomElapsedMs = 0
    this.hazardHitCycles.clear()
    this.hazardSuppressedUntil.clear()
    this.interactionResolved = false
    this.rewardChest = null
    this.cooldownEnds.clear()
    this.resetTapCombos()
    this.gestures.reset()
    for (const hand of LAST_CHANCES_HANDS) this.resetImmediateSwordInput(hand)
    this.mylorikControls.reset()
    this.dualSenseControls.reset()
    for (const hand of LAST_CHANCES_HANDS) {
      const quest = this.moveQuests[hand]
      for (const gesture of quest.pendingUnlocks) quest.unlocked[gesture] = true
      quest.pendingUnlocks = []
      quest.roomKills = { tap: 0, hold: 0 }
    }
    for (const weapon of this.weapons.values()) {
      if (weapon.trait !== 'swordRhythm') continue
      const state = this.weaponState(weapon)
      state.lastTapAtMs = Number.NEGATIVE_INFINITY
      state.rhythm = 'idle'
      state.perfectTimingMs = 0
      state.fatigueMs = 0
      state.roomTimingMisses = 0
      state.consecutiveTimingMisses = 0
      state.fatigueTriggeredByTapAtMs = Number.NEGATIVE_INFINITY
    }
    this.zoneAttacks = []
    this.holeStrikes = []
    this.swarmSpawner = null
    this.turretAlarmMs = 0
    this.turrets = node.turrets.map((definition) => {
      const angle = definition.facingDegrees * Math.PI / 180
      return {
        definition,
        facing: { x: Math.cos(angle), y: Math.sin(angle) },
        disabled: false,
        seesPlayer: false,
        fireCooldownMs: 0,
      }
    })
    this.groundWeapons = []
    this.ninjaDashReadyAtMs = 0
    const swarmDefinition = node.swarm
      ? this.enemyDefinitions.get(node.swarm.definitionId)
      : undefined
    if (node.swarm && swarmDefinition?.swarm
      && !(this.cockroachesExtinct && isCockroachDefinition(swarmDefinition))) {
      this.swarmSpawner = {
        definition: swarmDefinition,
        edges: node.swarm.edges,
        remaining: swarmDefinition.swarm.total,
        total: swarmDefinition.swarm.total,
        spawnedCount: 0,
        nextSpawnAtMs: 0,
        rng: createLastChancesRng(`${node.seed}:swarm`),
        infinite: node.swarm.infinite === true,
      }
    }
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
        lastPlayerHit: null,
        gestureHits: { left: new Set(), right: new Set() },
        swordExecutionMarked: false,
        entering: false,
        motherRetreatsTriggered: 0,
        motherRetreat: null,
      }
    })
    this.phase = 'playing'
    this.deathReason = null
    this.altarPromptActive = node.altar !== null
    if (this.altarPromptActive) this.paused = true
    if (this.enemies.length === 0 && !this.swarmSpawner
      && this.turrets.every(turret => turret.disabled)) this.completeRoom()
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
    this.rewardChest = null
    this.finishRoomTransition()
    this.render()
    this.emitSnapshot(true)
    return true
  }

  resolveAltar(accept: boolean): boolean {
    const altar = this.currentNode?.altar
    if (!this.altarPromptActive || !altar || this.phase !== 'playing') return false
    if (accept) {
      if (this.chances < altar.chanceCost) return false
      this.chances -= altar.chanceCost
      this.bossCheckpoint = {
        nodeId: this.currentNode!.id,
        attemptPath: [...this.attemptPath],
        loadout: this.activeLoadout ? { ...this.activeLoadout } : null,
      }
    }
    this.altarPromptActive = false
    this.paused = false
    this.lastFrameMs = performance.now()
    this.render()
    this.emitSnapshot(true)
    return true
  }

  interact(): boolean {
    if (!this.canExploreRoom() || this.paused || !this.activeLoadout) return false
    const turret = this.nearestActiveTurret()
    if (turret && this.turretAlarmMs <= 0) {
      turret.disabled = true
      turret.seesPlayer = false
      if (this.turrets.every(candidate => candidate.disabled)
        && this.enemies.every(enemy => enemy.state === 'dead')
        && !this.swarmSpawner) this.completeRoom()
      this.emitSnapshot(true)
      return true
    }
    const groundWeapon = this.nearestGroundWeapon()
    if (groundWeapon) return this.pickUpGroundWeapon(groundWeapon)
    const rewardChest = this.nearbyRewardChest()
    if (rewardChest) {
      rewardChest.opened = true
      this.phase = 'interaction'
      this.selectedInteractionChoiceId = null
      this.cleanupControlInputs(false)
      this.emitSnapshot(true)
      return true
    }
    if (this.phase !== 'playing') return false
    const spider = this.capturableKnifeSpider()
    if (!spider) return false
    const heldRight = this.weapons.get('right')
    if (heldRight) this.dropRightHandWeapon(spider.position, heldRight)
    spider.state = 'dead'
    this.activeLoadout = this.normalizeLoadoutAugments({
      ...this.activeLoadout,
      secondaryWeaponId: 'secondary-spider-knife',
      secondaryAugment: 'none',
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
    const checkpoint = this.bossCheckpoint
    this.bossCheckpoint = null
    this.resetAttempt()
    if (checkpoint) {
      this.activeLoadout = checkpoint.loadout ? { ...checkpoint.loadout } : null
      this.rebuildWeapons()
      this.attemptPath = checkpoint.attemptPath.slice(0, -1)
      this.availableNodeIds = [checkpoint.nodeId]
      this.selectedNodeId = checkpoint.nodeId
      this.chooseNode(checkpoint.nodeId)
    }
    this.render()
    this.emitSnapshot(true)
    return true
  }

  newGeneration(seedOverride?: string | number): LastChancesGamePlan {
    this.generation += 1
    this.plan = buildLastChancesPlan(this.config, this.generation, seedOverride)
    this.chances = this.config.chances
    this.totalDeaths = 0
    this.bossCheckpoint = null
    this.cockroachesExtinct = false
    this.corpseBoundPrimaryWeaponId = null
    this.nextGroundWeaponId = 1
    this.elapsedMs = 0
    this.lastSnapshotAt = Number.NEGATIVE_INFINITY
    this.nextProjectileId = 1
    this.generationBaseStats = copyStats(this.config.player.baseStats)
    this.player.stats = copyStats(this.generationBaseStats)
    this.moveQuests = {
      left: createHandMoveQuestState(
        this.qaControlsFixture || this.config.progression.moveQuestsEnabled === false,
      ),
      right: createHandMoveQuestState(
        this.qaControlsFixture || this.config.progression.moveQuestsEnabled === false,
      ),
    }
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
    if (vectorLength(this.touchAim) > this.config.input.aimDeadZone) {
      this.retainedGamepadAim = null
    }
  }

  press(hand: LastChancesHand): void {
    if (!this.canUseRoomActions() || this.paused || this.destroyed) return
    const now = performance.now()
    this.gestures.press(hand, now)
    const weapon = this.weapons.get(hand)
    if (weapon?.trait !== 'swordRhythm') return
    const input = this.gestures.snapshot(hand, now)
    const immediate = this.immediateSwordInput[hand]
    if (input.sequence === 'first') {
      immediate.firstTapExecuted = this.gestureReady(hand, 'tap')
      immediate.oberhauExecuted = false
      immediate.unterhauExecuted = false
      if (immediate.firstTapExecuted) {
        this.performAttack({ hand, gesture: 'tap', atMs: this.elapsedMs, heldMs: 0, firstHoldMs: 0 })
      }
      return
    }
    if (input.sequence === 'secondTap') {
      immediate.oberhauExecuted = this.gestureReady(hand, 'doubleTap')
      immediate.unterhauExecuted = false
      if (immediate.oberhauExecuted) {
        this.performAttack({ hand, gesture: 'doubleTap', atMs: this.elapsedMs, heldMs: 0, firstHoldMs: 0 })
      }
    }
  }

  release(hand: LastChancesHand): void {
    if (this.destroyed) return
    const now = performance.now()
    const input = this.gestures.snapshot(hand, now)
    if (this.canUseRoomActions()
      && !this.paused
      && input.pressed
      && input.sequence === 'first'
      && input.heldMs < this.config.input.holdMs) {
      this.beginProvisionalTapParry(hand)
    }
    this.gestures.release(hand, now)
  }

  private handleLegacyGestureResolution(resolution: LastChancesGestureResolution): void {
    const weapon = this.weapons.get(resolution.hand)
    const immediate = this.immediateSwordInput[resolution.hand]
    if (weapon?.trait === 'swordRhythm') {
      if (resolution.gesture === 'tap' && immediate.firstTapExecuted) {
        this.resetImmediateSwordInput(resolution.hand)
        return
      }
      if (resolution.gesture === 'doubleTap' && immediate.oberhauExecuted) {
        this.cancelPendingUnterhau(weapon)
        this.resetImmediateSwordInput(resolution.hand)
        return
      }
      if (resolution.gesture === 'doubleTapHold' && immediate.oberhauExecuted) {
        if (!immediate.unterhauExecuted
          && resolution.heldMs >= tuningValue(weapon, 'unterhauHoldMs', 1000)) {
          immediate.unterhauExecuted = this.executePendingUnterhau(resolution.hand)
        }
        if (!immediate.unterhauExecuted) this.cancelPendingUnterhau(weapon)
        this.resetImmediateSwordInput(resolution.hand)
        return
      }
    }
    this.resetImmediateSwordInput(resolution.hand)
    this.performAttack(resolution)
  }

  private resetImmediateSwordInput(hand: LastChancesHand): void {
    this.immediateSwordInput[hand] = {
      firstTapExecuted: false,
      oberhauExecuted: false,
      unterhauExecuted: false,
    }
  }

  private updateImmediateSwordInputs(now: number): void {
    for (const hand of LAST_CHANCES_HANDS) {
      const immediate = this.immediateSwordInput[hand]
      const weapon = this.weapons.get(hand)
      if (weapon?.trait !== 'swordRhythm') continue
      const holdGateMs = tuningValue(weapon, 'unterhauHoldMs', 1000)
      if (immediate.oberhauExecuted && !immediate.unterhauExecuted) {
        const input = this.gestures.snapshot(hand, now)
        if (input.pressed && input.sequence === 'secondTap' && input.heldMs >= holdGateMs) {
          immediate.unterhauExecuted = this.executePendingUnterhau(hand)
        }
      }
      if (this.controlSchemeValue !== 'dualsense') continue
      const trigger = this.dualSenseControls.snapshot(runtimeHandToPhysicalCluster(hand), now)
      if (trigger.active && trigger.nodeId === 'doubleTapHold' && trigger.heldMs >= holdGateMs) {
        this.executePendingUnterhau(hand)
      }
    }
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
    if (this.pointerClientX !== null) this.pointerDeltaX += clientX - this.pointerClientX
    this.pointerClientX = clientX
    this.retainedGamepadAim = null
  }

  private readonly tick = (frameMs: number): void => {
    if (!this.started || this.destroyed) return
    const deltaMs = clamp(frameMs - this.lastFrameMs, 0, 50)
    this.lastFrameMs = frameMs
    this.frameNowMs = frameMs
    this.pollGamepad()
    this.gestures.update(frameMs)
    this.mylorikControls.update(frameMs)
    this.updateKeyboardDualSenseTriggers(frameMs)
    this.dualSenseControls.update(frameMs, physicalHand => (
      this.weapons.get(physicalClusterToRuntimeHand(physicalHand))?.controls
    ))
    this.updateImmediateSwordInputs(frameMs)
    if (!this.paused) {
      this.elapsedMs += deltaMs
      if (this.phase === 'playing') this.update(deltaMs / 1000, deltaMs)
      else if (this.canExploreRoom()) this.updateClearedRoom(deltaMs / 1000, deltaMs)
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
      state.perfectTimingMs = Math.max(0, state.perfectTimingMs - deltaMs)
      state.fatigueMs = Math.max(0, state.fatigueMs - deltaMs)
      if (state.unterhauDueAtMs > 0
        && this.elapsedMs >= state.unterhauDueAtMs
        && !this.delayedAttacks.some(delayed => delayed.attack.behavior === 'swordFollowUp')) {
        state.unterhauDueAtMs = 0
        state.unterhauTargetId = null
        state.unterhauTargetPosition = null
        state.unterhauPrimed = false
      }
    }
    this.updateHeldWeaponMechanics(deltaMs)
    this.updateSpiderKnifeWriggle()
    this.updateDelayedAttacks(deltaMs)
    this.updateDelayedRecoveries(deltaMs)
    this.updateSwordAdvance(deltaSeconds)
    this.updatePlayer(deltaSeconds)
    this.updateTurrets(deltaSeconds, deltaMs)
    this.updateProjectiles(deltaSeconds, deltaMs)
    this.updateSwarmSpawner()
    this.updateEnemies(deltaSeconds, deltaMs)
    this.updateHoleStrikes()
    this.updateZoneAttacks()
    this.updateActiveAreas(deltaMs)
    this.updateHazards(deltaSeconds)
    this.updateMentalHealth(deltaSeconds)
    this.syncContinuationFeedback()
    this.effects.forEach(effect => effect.remainingMs -= deltaMs)
    this.effects = this.effects.filter(effect => effect.remainingMs > 0)
    this.traces.forEach(trace => { trace.remainingMs -= deltaMs })
    this.traces = this.traces.filter(trace => trace.remainingMs > 0)
    this.pointerDeltaX = 0
    if (this.phase === 'playing'
      && (this.swarmSpawner?.remaining ?? 0) <= 0
      && this.enemies.every(enemy => enemy.state === 'dead')
      && this.turrets.every(turret => turret.disabled)) this.completeRoom()
  }

  private canExploreRoom(): boolean {
    return this.phase === 'playing'
      || (this.phase === 'planning' && this.currentNode !== null && !this.routeMapVisible)
  }

  private canUseRoomActions(): boolean {
    return this.canExploreRoom() && !this.routeMapVisible
  }

  private updateClearedRoom(deltaSeconds: number, deltaMs: number): void {
    this.player.invulnerableMs = Math.max(0, this.player.invulnerableMs - deltaMs)
    this.player.rootMs = Math.max(0, this.player.rootMs - deltaMs)
    this.player.recoveryMs = Math.max(0, this.player.recoveryMs - deltaMs)
    for (const state of this.weaponStates.values()) {
      state.recoveryMs = Math.max(0, state.recoveryMs - deltaMs)
      state.perfectTimingMs = Math.max(0, state.perfectTimingMs - deltaMs)
      state.fatigueMs = Math.max(0, state.fatigueMs - deltaMs)
    }
    this.updateHeldWeaponMechanics(deltaMs)
    this.updateDelayedAttacks(deltaMs)
    this.updateDelayedRecoveries(deltaMs)
    this.updateSwordAdvance(deltaSeconds)
    this.updatePlayer(deltaSeconds)
    this.updateProjectiles(deltaSeconds, deltaMs)
    this.updateActiveAreas(deltaMs)
    this.syncContinuationFeedback()
    this.effects.forEach(effect => { effect.remainingMs -= deltaMs })
    this.effects = this.effects.filter(effect => effect.remainingMs > 0)
    this.traces.forEach(trace => { trace.remainingMs -= deltaMs })
    this.traces = this.traces.filter(trace => trace.remainingMs > 0)
    this.pointerDeltaX = 0
  }

  private updateDelayedAttacks(deltaMs: number): void {
    const ready: RuntimeDelayedAttack[] = []
    for (const delayed of this.delayedAttacks) {
      delayed.remainingMs = Math.max(0, delayed.remainingMs - deltaMs)
      if (delayed.remainingMs <= 0) ready.push(delayed)
    }
    this.delayedAttacks = this.delayedAttacks.filter(delayed => delayed.remainingMs > 0)
    if (!this.canUseRoomActions()) return
    for (const delayed of ready) {
      if (delayed.attack.behavior === 'swordFollowUp') this.executeSwordFollowUp(delayed)
      else this.executeAttack(
        delayed.attack,
        this.resolveDelayedAttackDirection(delayed),
        delayed.context,
      )
      if (delayed.attack.behavior === 'swordFollowUp') {
        const state = this.weaponState(delayed.context.weapon)
        state.unterhauDueAtMs = 0
        state.unterhauTargetId = null
        state.unterhauTargetPosition = null
        state.unterhauPrimed = false
      }
    }
  }

  private resolveDelayedAttackDirection(delayed: RuntimeDelayedAttack): LastChancesVector {
    if (delayed.attack.behavior !== 'swordFollowUp') return delayed.direction
    return normalize(this.player.aim, delayed.direction)
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

  private updateSwordAdvance(deltaSeconds: number): void {
    const advance = this.activeSwordAdvance
    if (!advance || advance.remainingDistance <= 0) return
    const travel = Math.min(advance.remainingDistance, advance.speed * deltaSeconds)
    this.moveCircle(this.player.position, {
      x: advance.direction.x * travel,
      y: advance.direction.y * travel,
    }, this.config.player.radius)
    advance.remainingDistance -= travel
    if (advance.remainingDistance <= EPSILON) this.activeSwordAdvance = null
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
        }, this.activeDash.direction, this.activeDash.weaponId, this.activeDash.hand,
        null, false, this.activeDash.gesture)
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
          this.attackColliderHitsCircle(
            collider,
            enemy.position,
            enemy.definition.radius,
            this.activeDash!.attack,
            dashStart,
          )
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
            gesture: this.activeDash.gesture,
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
            false,
            finishedDash.gesture,
          )
        }
      }
      return
    }

    const movement = this.player.rootMs > 0 || this.player.recoveryMs > 0
      ? { x: 0, y: 0 }
      : this.resolveMovement()
    this.moveCircle(this.player.position, {
      x: movement.x * this.effectivePlayerStats().moveSpeed * deltaSeconds,
      y: movement.y * this.effectivePlayerStats().moveSpeed * deltaSeconds,
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
      const hitObstacle = projectile.attack?.collider?.passesThroughWalls !== true
        && (arena?.obstacles.some(obstacle => (
          segmentHitsObstacle(projectileStart, projectile.position, obstacle, projectile.radius)
        )) ?? false)
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
        if (!this.attackColliderHitsCircle(
          sweptCollider,
          enemy.position,
          enemy.definition.radius,
          projectile.attack,
          projectileStart,
        )) continue
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
          gesture: projectile.gesture,
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

  private updateSwarmSpawner(): void {
    const spawner = this.swarmSpawner
    const node = this.currentNode
    if (!spawner || (!spawner.infinite && spawner.remaining <= 0) || !node) return
    const swarm = spawner.definition.swarm
    if (!swarm) return
    while ((spawner.infinite || spawner.remaining > 0)
      && this.roomElapsedMs >= spawner.nextSpawnAtMs) {
      this.spawnSwarmCockroach(spawner, node)
      spawner.spawnedCount += 1
      if (!spawner.infinite) spawner.remaining -= 1
      // The initial burst pours out in one tick; afterwards one cockroach per interval.
      if (spawner.spawnedCount >= swarm.initialBurst) {
        spawner.nextSpawnAtMs = this.roomElapsedMs + swarm.spawnIntervalMs
      }
    }
  }

  private updateTurrets(deltaSeconds: number, deltaMs: number): void {
    const node = this.currentNode
    if (!node || this.turrets.length === 0) return
    for (const turret of this.turrets) {
      if (turret.disabled) {
        turret.seesPlayer = false
        continue
      }
      const rotation = turret.definition.rotationDegreesPerSecond * Math.PI / 180 * deltaSeconds
      const angle = Math.atan2(turret.facing.y, turret.facing.x) + rotation
      turret.facing = { x: Math.cos(angle), y: Math.sin(angle) }
      turret.seesPlayer = this.turretCanSeePlayer(turret, node)
      turret.fireCooldownMs = Math.max(0, turret.fireCooldownMs - deltaMs)
    }
    if (this.turrets.some(turret => !turret.disabled && turret.seesPlayer)) {
      this.turretAlarmMs = Math.max(this.turretAlarmMs, 650)
    } else {
      this.turretAlarmMs = Math.max(0, this.turretAlarmMs - deltaMs)
    }
    if (this.turretAlarmMs <= 0) return
    for (const turret of this.turrets) {
      if (turret.disabled) continue
      const direction = normalize({
        x: this.player.position.x - turret.definition.position.x,
        y: this.player.position.y - turret.definition.position.y,
      }, turret.facing)
      turret.facing = direction
      if (turret.fireCooldownMs > 0) continue
      const radius = turret.definition.projectileRadius
      const speed = turret.definition.projectileSpeed
      this.projectiles.push({
        id: this.nextProjectileId++,
        position: {
          x: turret.definition.position.x + direction.x * (radius + 18),
          y: turret.definition.position.y + direction.y * (radius + 18),
        },
        velocity: { x: direction.x * speed, y: direction.y * speed },
        radius,
        damage: turret.definition.damage,
        knockback: 0,
        remainingDistance: turret.definition.visionRange,
        remainingMs: turret.definition.visionRange / speed * 1000,
        remainingHits: 1,
        hitIds: new Set(),
        color: turret.definition.color,
        source: 'enemy',
        sourceName: turret.definition.name,
      })
      turret.fireCooldownMs = turret.definition.fireIntervalMs
    }
  }

  private turretCanSeePlayer(turret: RuntimeTurret, node: LastChancesPlanNode): boolean {
    const toPlayer = {
      x: this.player.position.x - turret.definition.position.x,
      y: this.player.position.y - turret.definition.position.y,
    }
    const distance = vectorLength(toPlayer)
    if (distance > turret.definition.visionRange) return false
    const direction = normalize(toPlayer)
    const dot = direction.x * turret.facing.x + direction.y * turret.facing.y
    if (dot < Math.cos(turret.definition.visionAngleDegrees * Math.PI / 360)) return false
    return !node.arena.obstacles.some(obstacle => (
      segmentHitsObstacle(turret.definition.position, this.player.position, obstacle)
    ))
  }

  private spawnSwarmCockroach(spawner: RuntimeSwarmSpawner, node: LastChancesPlanNode): void {
    const definition = spawner.definition
    const edge = spawner.edges[spawner.spawnedCount % 2]
    const margin = definition.radius * 2 + 6
    const roll = spawner.rng()
    const position: LastChancesVector = edge === 'top'
      ? { x: roll * node.arena.width, y: -margin }
      : edge === 'bottom'
        ? { x: roll * node.arena.width, y: node.arena.height + margin }
        : edge === 'left'
          ? { x: -margin, y: roll * node.arena.height }
          : { x: node.arena.width + margin, y: roll * node.arena.height }
    this.enemies.push({
      id: `${node.id}-swarm-${spawner.spawnedCount + 1}`,
      definition,
      position,
      facing: normalize({
        x: this.player.position.x - position.x,
        y: this.player.position.y - position.y,
      }, { x: 1, y: 0 }),
      hp: definition.maxHp,
      state: 'chasing',
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
      lastPlayerHit: null,
      gestureHits: { left: new Set(), right: new Set() },
      swordExecutionMarked: false,
      entering: true,
      motherRetreatsTriggered: 0,
      motherRetreat: null,
    })
  }

  private moveEnemy(enemy: RuntimeEnemy, delta: LastChancesVector): void {
    if (enemy.entering && this.currentNode) {
      enemy.position.x += delta.x
      enemy.position.y += delta.y
      const arena = this.currentNode.arena
      const radius = enemy.definition.radius
      if (enemy.position.x >= radius && enemy.position.x <= arena.width - radius
        && enemy.position.y >= radius && enemy.position.y <= arena.height - radius) {
        enemy.entering = false
      }
      return
    }
    if (enemy.definition.cockroachMother && this.currentNode) {
      enemy.position.x = clamp(
        enemy.position.x + delta.x,
        enemy.definition.radius,
        this.currentNode.arena.width - enemy.definition.radius,
      )
      enemy.position.y = clamp(
        enemy.position.y + delta.y,
        enemy.definition.radius,
        this.currentNode.arena.height - enemy.definition.radius,
      )
      return
    }
    this.moveCircle(enemy.position, delta, enemy.definition.radius)
  }

  private updateEnemies(deltaSeconds: number, deltaMs: number): void {
    let queuedAttackerActive = this.enemies.some((enemy) => {
      const role = this.enemyCombatProfile(enemy).role
      return enemy.state === 'attacking' && role !== 'creep' && role !== 'cockroach'
    })
    for (const enemy of this.enemies) {
      if (enemy.hp <= 0) continue
      if (this.updateCockroachMotherRetreat(enemy, deltaSeconds)) continue
      updateLastChancesStatuses(enemy.statuses, deltaMs, amount => {
        const hpBeforeTick = enemy.hp
        enemy.hp = Math.max(0, enemy.hp - amount)
        this.applyCockroachMotherHealthGate(enemy)
        this.restoreFromLifesteal(hpBeforeTick - enemy.hp)
        if (enemy.hp <= 0) this.finishEnemyDeath(enemy)
      })
      if (enemy.statuses.unstoppableMs > 0) this.clearEnemyControlStatuses(enemy)
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
      const mayUseQueue = profile.role === 'creep'
        || profile.role === 'cockroach'
        || !queuedAttackerActive
      if (distance <= profile.attackRange
        && enemy.attackCooldownMs <= 0
        && enemy.statuses.disarmMs <= 0
        && mayUseQueue) {
        this.startEnemyAttack(enemy, profile)
        if (profile.role !== 'creep' && profile.role !== 'cockroach') queuedAttackerActive = true
        continue
      }
      const desiredDistance = profile.attackRange
        * (enemy.definition.preferredAttackRangeRatio ?? 0.72)
      if (distance > desiredDistance) {
        this.moveEnemy(enemy, {
          x: enemy.facing.x * enemy.definition.moveSpeed * deltaSeconds,
          y: enemy.facing.y * enemy.definition.moveSpeed * deltaSeconds,
        })
        if (enemy.statuses.slowMultiplier < 1) {
          const correction = 1 - enemy.statuses.slowMultiplier
          this.moveEnemy(enemy, {
            x: -enemy.facing.x * enemy.definition.moveSpeed * deltaSeconds * correction,
            y: -enemy.facing.y * enemy.definition.moveSpeed * deltaSeconds * correction,
          })
        }
      }
    }
  }

  private updateCockroachMotherRetreat(enemy: RuntimeEnemy, deltaSeconds: number): boolean {
    const retreat = enemy.motherRetreat
    const mother = enemy.definition.cockroachMother
    const node = this.currentNode
    if (!retreat || !mother || !node) return false
    const entrance = node.bossHoles.find(hole => hole.id === retreat.entranceHoleId)
    const exit = node.bossHoles.find(hole => hole.id === retreat.exitHoleId)
    if (!entrance || !exit) {
      enemy.motherRetreat = null
      return false
    }
    if (retreat.stage === 'approaching') {
      const offset = {
        x: entrance.position.x - enemy.position.x,
        y: entrance.position.y - enemy.position.y,
      }
      const distance = vectorLength(offset)
      const direction = normalize(offset, enemy.facing)
      enemy.facing = direction
      const travel = Math.min(distance, mother.retreatSpeed * deltaSeconds)
      this.moveEnemy(enemy, { x: direction.x * travel, y: direction.y * travel })
      if (distance > enemy.definition.radius * 0.55) return true
      enemy.position = { ...entrance.position }
      retreat.stage = 'hidden'
      retreat.detonateAtMs = this.elapsedMs + mother.hideMs
      this.holeStrikes.push({
        holeId: exit.id,
        center: { ...exit.position },
        radius: mother.blastRadius,
        damageMaxHpRatio: mother.blastDamageMaxHpRatio,
        spawnedAtMs: this.elapsedMs,
        detonateAtMs: retreat.detonateAtMs,
        sourceName: `${enemy.definition.name}: удар из норы`,
      })
      return true
    }
    if (this.elapsedMs < retreat.detonateAtMs) return true
    enemy.position = { ...exit.position }
    enemy.facing = normalize({
      x: this.player.position.x - exit.position.x,
      y: this.player.position.y - exit.position.y,
    }, enemy.facing)
    enemy.attackCooldownMs = Math.max(enemy.attackCooldownMs, 650)
    enemy.state = 'chasing'
    enemy.motherRetreat = null
    return true
  }

  private applyCockroachMotherHealthGate(enemy: RuntimeEnemy): void {
    const mother = enemy.definition.cockroachMother
    const node = this.currentNode
    if (!mother || !node || enemy.motherRetreat) return
    const threshold = mother.retreatHealthRatios[enemy.motherRetreatsTriggered]
    if (threshold === undefined || enemy.hp > enemy.definition.maxHp * threshold) return
    const holes = node.bossHoles
    if (holes.length === 0) return
    enemy.hp = Math.max(enemy.hp, enemy.definition.maxHp * threshold)
    const entrance = [...holes].sort((left, right) => (
      distanceSquared(enemy.position, left.position) - distanceSquared(enemy.position, right.position)
    ))[0]
    const exit = holes.find(hole => hole.id === entrance.linkedHoleId)
    if (!exit) return
    enemy.motherRetreatsTriggered += 1
    enemy.motherRetreat = {
      stage: 'approaching',
      entranceHoleId: entrance.id,
      exitHoleId: exit.id,
      detonateAtMs: 0,
    }
    enemy.state = 'chasing'
    enemy.attackWindupMs = 0
    enemy.lockedAttackDirection = null
    enemy.leapRemainingDistance = 0
    enemy.statuses.stunMs = 0
    enemy.statuses.boundMs = 0
  }

  private updateHoleStrikes(): void {
    if (this.holeStrikes.length === 0) return
    const pending: RuntimeHoleStrike[] = []
    for (const strike of this.holeStrikes) {
      if (this.elapsedMs < strike.detonateAtMs) {
        pending.push(strike)
        continue
      }
      const hitRadius = strike.radius + this.config.player.radius
      if (distanceSquared(this.player.position, strike.center) <= hitRadius * hitRadius) {
        this.damagePlayerPure(
          this.player.stats.maxHp * strike.damageMaxHpRatio,
          strike.sourceName,
        )
      }
    }
    this.holeStrikes = pending
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
    if (profile.attackKind === 'zone') {
      // The telegraphed ground zone is the enemy's only damage source — no contact damage.
      this.spawnZoneAttack(enemy)
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

  /**
   * The living spider-knife tries to escape: random alternating left/right
   * motor bursts whose frequency and strength escalate as its durability
   * drains (it panics as it dies). Ambient-only — priority sits below every
   * deliberate combat cue, and any guard failing stops the scheduler at once.
   */
  private updateSpiderKnifeWriggle(): void {
    const wriggleWeapon = [...this.weapons.values()].find(candidate => (
      candidate.controls?.dualsense.haptics?.wriggle
    ))
    const active = this.controlSchemeValue === 'dualsense'
      && this.canUseRoomActions()
      && !this.paused
      && this.feedbackPreferences.mode === 'full'
      && wriggleWeapon !== undefined
    if (!active) {
      this.spiderWriggle = null
      return
    }
    const wriggle = wriggleWeapon.controls!.dualsense.haptics!.wriggle!
    const state = this.weaponState(wriggleWeapon)
    const durabilityFraction = state.maxResource > 0
      ? clamp(state.resource / state.maxResource, 0, 1)
      : 0
    const panic = 1 - Math.pow(durabilityFraction, wriggle.curveExponent)
    const nextInterval = (rng: () => number): number => {
      const calm = wriggle.calmIntervalMs[0]
        + rng() * (wriggle.calmIntervalMs[1] - wriggle.calmIntervalMs[0])
      const panicked = wriggle.panicIntervalMs[0]
        + rng() * (wriggle.panicIntervalMs[1] - wriggle.panicIntervalMs[0])
      return calm + (panicked - calm) * panic
    }
    if (!this.spiderWriggle) {
      const rng = createLastChancesRng(`${this.config.seed}:wriggle:${this.generation}`)
      this.spiderWriggle = { rng, nextAtMs: this.elapsedMs + nextInterval(rng) }
      return
    }
    if (this.elapsedMs < this.spiderWriggle.nextAtMs) return
    const rng = this.spiderWriggle.rng
    const magnitude = wriggle.calmMagnitude
      + (wriggle.panicMagnitude - wriggle.calmMagnitude) * panic
    const pulses = lastChancesRandomInt(rng, wriggle.pulsesPerBurst[0], wriggle.pulsesPerBurst[1])
    const startLeft = rng() < 0.5
    this.feedbackController.emit({
      state: 'wriggle',
      profile: 'click',
      pattern: Array.from({ length: pulses }, (_, pulse) => ({
        delayMs: pulse * (wriggle.pulseMs + 30),
        durationMs: wriggle.pulseMs,
        magnitude,
        hand: (pulse % 2 === 0) === startLeft ? 'left' as const : 'right' as const,
      })),
    })
    this.spiderWriggle.nextAtMs = this.elapsedMs + nextInterval(rng)
  }

  private updateMentalHealth(deltaSeconds: number): void {
    const pressure = Math.min(
      this.config.mentalHealth.maxPressurePerSecond,
      this.enemies.reduce((sum, enemy) => (
        enemy.motherRetreat?.stage === 'hidden'
          ? sum
          : enemy.state === 'noticing' || enemy.state === 'alerted'
          || enemy.state === 'chasing' || enemy.state === 'attacking'
          ? sum + enemy.definition.mentalPressurePerSecond
          : sum
      ), 0),
    )
    if (pressure > 0) {
      this.damagePlayerMental(pressure * deltaSeconds)
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
        this.damagePlayerMental(hazard.mentalDamagePerSecond * deltaSeconds)
        if (this.player.mentalHealth <= 0) this.killPlayer(`Mental health collapsed in ${hazard.name}`)
        continue
      }
      const cycle = Math.floor((this.roomElapsedMs + hazard.phaseOffsetMs) / hazard.cycleMs)
      if (this.hazardHitCycles.get(hazard.id) === cycle) continue
      this.hazardHitCycles.set(hazard.id, cycle)
      this.damagePlayer(hazard.damage, hazard.name)
    }
  }

  private handleSemanticInput(
    event: LastChancesSemanticInputEvent,
  ): 'handled' | 'buffer' | 'blocked' | 'observe' {
    if (event.scheme !== this.controlSchemeValue || !this.canUseRoomActions() || this.paused) {
      return 'blocked'
    }
    const weapon = this.weapons.get(event.hand)
    const controls = weapon?.controls
    if (!weapon || !controls) return this.blockSemanticInput(event, null)

    let gesture: LastChancesGesture | undefined
    let tactileProfile = event.tactileProfile ?? 'click'
    let adaptiveOverride: LastChancesAttackSetControlDefinition['dualsense']['nodes'][number]['adaptiveOverride']
    let entryTick: LastChancesGateTickDefinition | null | undefined
    if (event.scheme === 'dualsense') {
      if (event.intent === 'strike') {
        gesture = controls.dualsense.instantGesture
      } else if (event.gesture && event.nodeId) {
        const node = controls.dualsense.nodes.find(candidate => (
          candidate.id === event.nodeId && candidate.gesture === event.gesture
        ))
        if (node) {
          const requiredBand = node.requiredChargeBandId
            ? weapon.attacks.hold.charge?.bands.find(band => band.id === node.requiredChargeBandId)
            : undefined
          const chargeContextAvailable = node.requiredChargeBandId === undefined
            || (requiredBand !== undefined && event.heldMs >= requiredBand.minMs)
          const contextAvailable = event.armed === true || (
            (node.entryContext === 'neutral'
              ? this.controlContextActive(event.hand, 'neutral')
              : node.entryContext === 'continuation'
              || this.controlContextActive(event.hand, node.entryContext))
            && chargeContextAvailable
          )
          if (event.probe) {
            return contextAvailable && this.gestureReady(event.hand, node.gesture)
              ? 'handled'
              : 'observe'
          }
          if (!contextAvailable) return event.commit
            ? this.blockSemanticInput(event, node.gesture)
            : 'observe'
          gesture = event.gesture
          adaptiveOverride = node.adaptiveOverride
          entryTick = node.entryTick
        }
      } else if (event.preGate && event.gesture
        && controls.dualsense.preGateGesture === event.gesture) {
        // Quick release before any combo node: the authored "click before the
        // gate" action (e.g. the spear's distance poke).
        gesture = event.gesture
      } else if (event.source === 'keyboard' && event.intent === 'mobility') {
        gesture = controls.mylorik.activations
          .filter(activation => activation.intent === 'mobility' && activation.phase === event.phase)
          .filter(activation => !activation.context || this.controlContextActive(event.hand, activation.context))
          .sort((left, right) => right.priority - left.priority)[0]?.gesture
      }
    } else {
      const candidates = controls.mylorik.activations
        .filter(activation => activation.intent === event.intent && activation.phase === event.phase)
        .filter(activation => {
          if (!activation.context) return true
          if (event.context && event.context !== 'continuation'
            && event.context !== activation.context) return false
          return activation.context === event.context
            || this.controlContextActive(event.hand, activation.context)
        })
        .sort((left, right) => right.priority - left.priority)
      const selected = event.context === undefined
        ? candidates.find(candidate => candidate.context !== undefined)
          ?? candidates.find(candidate => candidate.context === undefined)
        : candidates.find(candidate => candidate.context !== undefined)
      gesture = selected?.gesture
    }

    if (!gesture) {
      const awaitingMylorikHold = event.scheme === 'mylorik'
        && event.phase === 'press'
        && controls.mylorik.activations.some(activation => (
          activation.intent === event.intent && activation.phase === 'hold'
        ))
      if (awaitingMylorikHold || !event.commit) return event.scheme === 'dualsense'
        ? 'handled'
        : 'observe'
      return this.blockSemanticInput(event, null)
    }
    const attack = weapon.attacks[gesture]
    tactileProfile = event.tactileProfile ?? (gesture === 'hold' ? 'ramp' : 'click')
    const tensionActive = tactileProfile === 'tension'
      || event.context === 'grapple'
      || event.context === 'tether'
    if (!event.commit) {
      const state = tensionActive
        ? 'tension' as const
        : event.context === 'continuation'
          ? 'continuation' as const
          : event.phase === 'hold' ? 'charge' as const : 'ready' as const
      this.controlCue = {
        hand: event.hand,
        intent: event.intent,
        state,
        gesture,
        label: attack.name,
        tactileProfile,
        atMs: this.elapsedMs,
      }
      if (event.scheme === 'dualsense') {
        // One short tick per gate crossed (the recognizer only emits on node
        // advance); the trigger value never scales the motors. Tension and
        // authored-silent nodes keep only the adaptive-trigger block.
        if (event.nodeId) this.armTriggerDetent(event.physicalHand, tactileProfile, adaptiveOverride)
        const tick = tensionActive || entryTick === null
          ? null
          : entryTick ?? controls.dualsense.haptics?.gateTick ?? DEFAULT_LAST_CHANCES_GATE_TICK
        this.feedbackController.emit({
          state: tensionActive ? 'tension' : state === 'ready' ? 'charge' : state,
          profile: tactileProfile,
          hand: event.physicalHand,
          tick,
          adaptiveOverride,
        })
      }
      return event.scheme === 'dualsense' || event.probe ? 'handled' : 'observe'
    }

    if (!this.gestureReady(event.hand, gesture)) {
      const cooldown = Math.max(
        0,
        (this.cooldownEnds.get(cooldownKey(event.hand, gesture)) ?? 0) - this.elapsedMs,
      )
      const recovery = Math.max(
        this.player.recoveryMs,
        this.weaponState(weapon).recoveryMs,
        (this.weaponActionEnds.get(weapon.id) ?? 0) - this.elapsedMs,
      )
      const bufferWindow = this.config.input.mylorik?.bufferMs ?? 0
      const unlocked = this.moveQuests[event.hand].unlocked[gesture]
      const enabled = attack.enabled !== false && attack.behavior !== 'disabled'
      if (event.scheme === 'mylorik' && enabled && unlocked
        && Math.max(cooldown, recovery) > 0
        && Math.max(cooldown, recovery) <= bufferWindow) return 'buffer'
      return this.blockSemanticInput(event, gesture)
    }

    const chargedAttack = attackWithLastChancesAugment(attack, weapon)
    const chargedHeldMs = gesture === 'holdThenDoubleTap'
      ? event.heldMs
      : gesture === 'hold' || gesture === 'doubleTapHold' ? event.heldMs : 0
    if (attack.charge && !resolveLastChancesChargedAttack(chargedAttack, chargedHeldMs).band) {
      return this.blockSemanticInput(event, gesture)
    }

    const state = tensionActive
      ? 'tension' as const
      : event.context === 'continuation' ? 'continuation' as const : 'ready' as const
    this.controlCue = {
      hand: event.hand,
      intent: event.intent,
      state,
      gesture,
      label: attack.name,
      tactileProfile,
      atMs: this.elapsedMs,
    }
    if (event.scheme === 'dualsense') {
      // Committed actions play the weapon's authored rumble signature so each
      // weapon is recognizable by touch. Tension commits (channel/grip starts)
      // stay adaptive-trigger-only — the spool feel, not a rumble.
      if (event.nodeId) this.armTriggerDetent(event.physicalHand, tactileProfile, adaptiveOverride)
      const commitPattern = controls.dualsense.haptics?.commitPattern
      this.feedbackController.emit({
        state,
        profile: tactileProfile,
        hand: event.physicalHand,
        adaptiveOverride,
        ...(tensionActive
          ? { tick: null }
          : commitPattern && commitPattern.length > 0 ? { pattern: commitPattern } : {}),
      })
    }
    const startsDualSenseChannel = event.scheme === 'dualsense'
      && event.phase === 'hold'
      && gesture === 'hold'
      && ['spearStance', 'axeSpin', 'spiderFlurry'].includes(attack.behavior ?? '')
    if (startsDualSenseChannel) return 'handled'
    this.performAttack({
      hand: event.hand,
      gesture,
      atMs: this.elapsedMs,
      heldMs: event.heldMs,
      firstHoldMs: event.heldMs,
    })
    return 'handled'
  }

  private blockSemanticInput(
    event: LastChancesSemanticInputEvent,
    gesture: LastChancesGesture | null,
  ): 'blocked' {
    const weapon = this.weapons.get(event.hand)
    this.controlCue = {
      hand: event.hand,
      intent: event.intent,
      state: 'blocked',
      gesture,
      label: gesture && weapon ? weapon.attacks[gesture].name : 'Action unavailable',
      tactileProfile: 'blocked',
      atMs: this.elapsedMs,
    }
    if (event.scheme === 'dualsense') {
      this.feedbackController.emit({
        state: 'blocked',
        profile: 'blocked',
        hand: event.physicalHand,
      })
    }
    this.emitSnapshot(true)
    return 'blocked'
  }

  private controlContextActive(
    hand: LastChancesHand,
    context: LastChancesControlContext,
  ): boolean {
    const weapon = this.weapons.get(hand)
    if (!weapon) return false
    const channel = this.heldChannels.get(hand)
    const handChannel = channel?.weaponId === weapon.id ? channel : undefined
    const activeBehavior = handChannel?.attack.behavior
    const state = this.weaponState(weapon)
    if (context === 'neutral') {
      return state.boundEnemyId === null
        && !(this.activeDash?.hand === hand && this.activeDash.weaponId === weapon.id)
        && handChannel === undefined
        && !this.activeAreas.some(area => area.hand === hand && area.weaponId === weapon.id && (
          area.attack.behavior === 'axeSpin'
          || area.attack.behavior === 'spearSpin'
          || area.attack.behavior === 'chainSpin'
          || area.attack.behavior === 'spiderFlurry'
        ))
    }
    if (context === 'opening') {
      const hasOpeningRoute = weapon.controls?.mylorik.activations.some(activation => (
        activation.context === 'opening'
      )) || weapon.controls?.dualsense.nodes.some(node => node.entryContext === 'opening')
      return hasOpeningRoute === true
        && this.enemies.some(enemy => enemy.state !== 'dead' && enemy.statuses.openingMs > 0)
    }
    if (context === 'grapple' || context === 'tether') return state.boundEnemyId !== null
    if (context === 'dash') {
      return this.activeDash?.hand === hand && this.activeDash.weaponId === weapon.id
    }
    if (context === 'spin') return activeBehavior === 'axeSpin'
      || activeBehavior === 'spearSpin'
      || activeBehavior === 'chainSpin'
      || this.activeAreas.some(area => area.hand === hand && area.weaponId === weapon.id && (
        area.attack.behavior === 'axeSpin'
        || area.attack.behavior === 'spearSpin'
        || area.attack.behavior === 'chainSpin'
      ))
    if (context === 'stance') return activeBehavior === 'spearStance'
    if (context === 'flurry') return activeBehavior === 'spiderFlurry'
    if (context === 'continuation') {
      return state.boundEnemyId !== null
        || (weapon.trait === 'swordRhythm' && state.unterhauDueAtMs > this.elapsedMs)
        || (this.activeDash?.hand === hand && this.activeDash.weaponId === weapon.id)
        || handChannel !== undefined
    }
    return false
  }

  /**
   * Maps authored control contexts onto concrete runtime windows. The keys are
   * deliberately semantic (not attack IDs), so polling can observe a window for
   * many frames without replaying its opening cue.
   */
  private continuationFeedbackSourceKeys(
    hand: LastChancesHand,
    context: LastChancesControlContext,
  ): string[] {
    if (context === 'neutral') return []
    const weapon = this.weapons.get(hand)
    if (!weapon) return []
    const accepts = (candidate: LastChancesControlContext): boolean => (
      context === 'continuation' || context === candidate
    )
    const keys = new Set<string>()

    if (accepts('dash')
      && this.activeDash?.hand === hand
      && this.activeDash.weaponId === weapon.id) {
      keys.add(`${hand}:dash`)
    }

    const addArea = (behavior: LastChancesAttackBehavior | undefined): void => {
      const areaContext: LastChancesControlContext | null = behavior === 'spearStance'
        ? 'stance'
        : behavior === 'spiderFlurry'
          ? 'flurry'
          : behavior === 'axeSpin' || behavior === 'spearSpin' || behavior === 'chainSpin'
            ? 'spin'
            : null
      if (areaContext && accepts(areaContext)) keys.add(`${hand}:channel:${areaContext}`)
    }
    addArea(this.heldChannels.get(hand)?.attack.behavior)
    for (const area of this.activeAreas) {
      if (area.hand === hand && area.weaponId === weapon.id) addArea(area.attack.behavior)
    }

    const state = this.weaponState(weapon)
    if (context === 'continuation'
      && weapon.trait === 'swordRhythm'
      && state.unterhauDueAtMs > this.elapsedMs) {
      keys.add(`${hand}:unterhau`)
    }
    if ((accepts('grapple') || accepts('tether')) && state.boundEnemyId !== null) {
      keys.add(`${hand}:bound`)
    }
    if (context === 'opening'
      && this.enemies.some(enemy => enemy.state !== 'dead' && enemy.statuses.openingMs > 0)) {
      keys.add(`${hand}:opening`)
    }
    if (context === 'continuation' && this.feedbackHitWindowEnds[hand] > this.elapsedMs) {
      keys.add(`${hand}:hit`)
    }
    return [...keys]
  }

  private continuationFeedbackNodeReady(
    hand: LastChancesHand,
    node: LastChancesAttackSetControlDefinition['dualsense']['nodes'][number],
  ): boolean {
    const weapon = this.weapons.get(hand)
    if (!weapon) return false
    const attack = weapon.attacks[node.gesture]
    return attack.enabled !== false
      && attack.behavior !== 'disabled'
      && this.moveQuests[hand].unlocked[node.gesture]
      && this.gestureReady(hand, node.gesture)
  }

  private markSuccessfulHitFeedbackWindow(hand: LastChancesHand): void {
    const controls = this.weapons.get(hand)?.controls
    const durationMs = Math.max(0, ...(controls?.dualsense.nodes
      .filter(node => node.entryContext === 'continuation')
      .map(node => node.expiryMs) ?? []))
    if (durationMs <= 0) return
    this.feedbackHitWindowEnds[hand] = Math.max(
      this.feedbackHitWindowEnds[hand],
      this.elapsedMs + durationMs,
    )
  }

  /** Emits availability feedback only on a false-to-true runtime window edge. */
  private syncContinuationFeedback(): void {
    const available = new Map<string, {
      hand: LastChancesHand
      node: LastChancesAttackSetControlDefinition['dualsense']['nodes'][number]
    }>()
    if (this.controlSchemeValue === 'dualsense' && this.canUseRoomActions() && !this.paused) {
      for (const hand of LAST_CHANCES_HANDS) {
        const controls = this.weapons.get(hand)?.controls
        if (!controls) continue
        for (const node of controls.dualsense.nodes) {
          if (!this.continuationFeedbackNodeReady(hand, node)) continue
          for (const key of this.continuationFeedbackSourceKeys(hand, node.entryContext)) {
            const previous = available.get(key)
            if (!previous || node.activationThreshold < previous.node.activationThreshold) {
              available.set(key, { hand, node })
            }
          }
        }
      }
    }

    for (const [key, { hand, node }] of available) {
      if (this.continuationFeedbackWindowKeys.has(key)) continue
      const weapon = this.weapons.get(hand)
      if (!weapon) continue
      this.controlCue = {
        hand,
        intent: 'technique',
        state: 'continuation',
        gesture: node.gesture,
        label: weapon.attacks[node.gesture].name,
        tactileProfile: 'followUp',
        atMs: this.elapsedMs,
      }
      this.feedbackController.emit({
        state: 'continuation',
        profile: 'followUp',
        hand: runtimeHandToPhysicalCluster(hand),
        adaptiveOverride: node.adaptiveOverride,
      })
    }
    this.continuationFeedbackWindowKeys = new Set(available.keys())
  }

  private resetContinuationFeedbackTracking(): void {
    this.continuationFeedbackWindowKeys.clear()
    this.feedbackHitWindowEnds.left = Number.NEGATIVE_INFINITY
    this.feedbackHitWindowEnds.right = Number.NEGATIVE_INFINITY
  }

  private gestureReady(hand: LastChancesHand, gesture: LastChancesGesture): boolean {
    const weapon = this.weapons.get(hand)
    if (!weapon) return false
    const attack = weapon.attacks[gesture]
    if (attack.enabled === false || attack.behavior === 'disabled') return false
    if (!this.moveQuests[hand].unlocked[gesture]) return false
    const state = this.weaponState(weapon)
    if (state.resource <= 0 && weapon.resource?.kind !== 'rhythm') return false
    if (this.provisionalParry
      && this.provisionalParry.weaponId === weapon.id
      && this.provisionalParry.hand !== hand) return false
    const behavior = attack.behavior
    const swordMorph = weapon.trait === 'swordRhythm'
      && (behavior === 'swordOpening' || behavior === 'swordFollowUp')
      && this.activeAreas.some(area => (
        area.weaponId === weapon.id && area.attack.behavior === 'swordRhythm'
      ))
    const contextualContinuation = (behavior === 'clawDeepStrike'
      && this.activeDash?.hand === hand && this.activeDash.weaponId === weapon.id)
      || (behavior === 'axeThrow' && state.boundEnemyId !== null)
      || (behavior === 'chainBind' && state.boundEnemyId !== null)
      || (behavior === 'chainThrow' && this.activeAreas.some(area => (
        area.hand === hand && area.attack.behavior === 'chainSpin'
      )))
      || (behavior === 'spiderTwist' && this.heldChannels.get(hand)?.attack.behavior === 'spiderFlurry')
      || (behavior === 'spiderThrow' && this.heldChannels.get(hand)?.attack.behavior === 'spiderFlurry')
      || (behavior === 'poleVault' && this.heldChannels.get(hand)?.attack.behavior === 'spearStance')
      || (behavior === 'axeLeap' && this.heldChannels.get(hand)?.attack.behavior === 'axeSpin')
      || (behavior === 'swordFollowUp' && state.unterhauDueAtMs > 0)
      || swordMorph
    if ((this.weaponActionEnds.get(weapon.id) ?? 0) > this.elapsedMs
      && !contextualContinuation) return false
    const otherHandUsesWeapon = [...this.heldChannels.entries()].some(([channelHand, area]) => (
      channelHand !== hand && area.weaponId === weapon.id
    ))
    if (otherHandUsesWeapon) return false
    const axeRecoveryCancel = weapon.trait === 'axeHookRecovery'
      && gesture === 'tap'
      && state.recoveryMs > 0
    if ((this.player.recoveryMs > 0 || state.recoveryMs > 0)
      && !axeRecoveryCancel && !contextualContinuation) {
      return false
    }
    if (weapon.trait === 'swordRhythm' && gesture === 'tap' && state.fatigueMs > 0) return false
    if (weapon.trait === 'swordRhythm' && gesture === 'doubleTap') {
      return (this.cooldownEnds.get(cooldownKey(hand, 'doubleTap')) ?? 0) <= this.elapsedMs
    }
    if (weapon.trait === 'swordRhythm' && gesture === 'doubleTapHold') {
      const unterhauReady = (this.cooldownEnds.get(cooldownKey(hand, 'doubleTapHold')) ?? 0)
        <= this.elapsedMs
      return state.unterhauDueAtMs > 0 && unterhauReady
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
    const parryHand = this.provisionalParry?.hand ?? (() => {
      const gesture = this.lastGesture
      const attack = gesture ? this.weapons.get(gesture.hand)?.attacks[gesture.gesture] : undefined
      return attack && PLAYER_PARRY_BEHAVIORS.has(attack.behavior ?? 'standard')
        ? gesture?.hand ?? null
        : null
    })()
    this.consumeActiveParry()
    this.finishEnemyAttack(enemy, profile)
    enemy.attackCooldownMs += profile.parryWindowMs
    enemy.revealedMs = Math.max(enemy.revealedMs, 1200)
    if (this.controlSchemeValue === 'dualsense' && parryHand) {
      this.feedbackController.emit({
        state: 'impact',
        profile: 'impact',
        hand: runtimeHandToPhysicalCluster(parryHand),
        strength: 1,
      })
    }
    return true
  }

  private performAttack(resolution: LastChancesGestureResolution): void {
    if (!this.canUseRoomActions() || this.paused || !this.currentNode) return
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
    if (weapon.trait === 'swordRhythm'
      && gesture === 'doubleTapHold'
      && state.unterhauDueAtMs > 0) {
      if (resolution.heldMs < tuningValue(weapon, 'unterhauHoldMs', 1000)) return
      this.executePendingUnterhau(hand)
      return
    }
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
    if (sourceAttack.behavior === 'clawDeepStrike'
      && this.activeDash?.hand === hand && this.activeDash.weaponId === weapon.id) {
      this.activeDash = null
      this.weaponActionEnds.delete(weapon.id)
    }
    const chargeHeldMs = gesture === 'holdThenDoubleTap'
      ? resolution.firstHoldMs
      : gesture === 'hold' || gesture === 'doubleTapHold' ? resolution.heldMs : 0
    const charged = resolveLastChancesChargedAttack(augmented, chargeHeldMs)
    if (sourceAttack.charge && !charged.band) return
    const attack = charged.attack
    if (weapon.trait === 'axeHookRecovery' && gesture === 'tap') {
      state.lastMotionDamageBonus = 0
    }
    if ((attack.behavior === 'spiderTwist'
      || attack.behavior === 'spiderThrow'
      || attack.behavior === 'poleVault'
      || attack.behavior === 'axeLeap') && this.heldChannels.has(hand)) {
      this.stopHeldChannel(hand)
    }
    if (isAxeRecoveryCancel) {
      state.recoveryMs = 0
      this.player.recoveryMs = 0
      attack.damage *= tuningValue(weapon, 'recoveryCancelDamageMultiplier', 1.35)
    }
    if (weapon.trait === 'swordRhythm' && gesture === 'tap') {
      if (!this.applySwordRhythm(state, attack, hand)) return
    }
    if (weapon.trait === 'swordRhythm'
      && (gesture === 'doubleTap' || gesture === 'doubleTapHold')) {
      this.morphSwordAttack(weapon)
    }
    if (gesture !== 'tap') {
      const cooldownAttack = weapon.trait === 'swordRhythm' && gesture === 'doubleTapHold'
        ? weapon.attacks.doubleTap
        : attack
      this.cooldownEnds.set(
        weapon.trait === 'swordRhythm' && gesture === 'doubleTapHold'
          ? cooldownKey(hand, 'doubleTap')
          : key,
        this.elapsedMs + cooldownAttack.cooldownMs,
      )
    }
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
      this.prepareSwordOberhau(weapon, state, hand, openingAttack, direction)
      this.executeAttack(openingAttack, direction, {
        ...context,
        gesture: 'doubleTap',
      })
      this.scheduleRecovery(weapon.id, openingAttack.recoveryMs, openingAttack.durationMs)
      if (resolution.heldMs >= tuningValue(weapon, 'unterhauHoldMs', 1000)) {
        this.queueSwordUnterhau(context, attack, direction, openingAttack.durationMs)
      } else {
        this.cancelPendingUnterhau(weapon)
      }
      return
    }

    if (attack.behavior === 'swordOpening') {
      this.prepareSwordOberhau(weapon, state, hand, attack, direction)
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
      false,
      context.gesture,
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
      gesture: context.gesture,
      storedDot: context.storedDot,
      ...(attack.behavior === 'spearRelease' && context.chargeBandId === 'late'
        ? { carriedIds: new Set<string>() }
        : {}),
    })
    this.nextProjectileId += 1
    if (attack.behavior !== 'spearRelease') this.addEffect('hit', attack, direction)
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
      gesture: context.gesture,
      storedDot: context.storedDot,
      landingBurst: attack.behavior === 'axeLeap' || attack.behavior === 'clawDash',
      trailAccumulatorMs: 0,
      elapsedMs: 0,
    }
    this.syncContinuationFeedback()
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
      false,
      context.gesture,
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
    gesture?: LastChancesGesture,
  ): void {
    const area = this.createActiveArea(kind, attack, direction, weaponId, hand, storedDot, channel, gesture)
    this.addColliderTrace(this.activeAreaCollider(area), attack)
    if (attack.behavior === 'axeGrapple' || attack.behavior === 'axeThrow') {
      this.latchAxeTarget(area)
    }
    else if (attack.behavior !== 'swordRhythm') this.applyActiveAreaHits(area)
    if (area.remainingMs > 0 && area.remainingHits > 0) this.activeAreas.push(area)
    this.syncContinuationFeedback()
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
        && this.attackColliderHitsCircle(
          collider,
          enemy.position,
          enemy.definition.radius,
          area.attack,
          area.origin,
        ))
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
        if (area.attack.behavior === 'axeSwing'
          && Math.abs(this.pointerDeltaX) > 0
          && Math.sign(this.pointerDeltaX) === rotationSign) {
          area.matchingAimMotionPx += Math.abs(this.pointerDeltaX)
          const weapon = this.weapons.get(area.hand)
          const progress = clamp(
            area.matchingAimMotionPx
              / Math.max(1, tuningValue(weapon, 'mouseMotionForMaxBonusPx', 160)),
            0,
            1,
          )
          area.motionDamageBonus = progress
            * tuningValue(weapon, 'mouseDamageBonusMax', 0.25)
          const state = weapon ? this.weaponState(weapon) : null
          if (state) state.lastMotionDamageBonus = area.motionDamageBonus
        }
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
      if (!this.attackColliderHitsCircle(
        collider,
        enemy.position,
        enemy.definition.radius,
        area.attack,
        area.origin,
      )) continue
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
        gesture: area.gesture,
        storedDot: area.storedDot,
        distance: vectorLength(toEnemy),
        damageMultiplier: 1 + area.motionDamageBonus,
        impactIntensity: area.attack.behavior === 'swordRhythm' ? 0 : undefined,
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
      const mediumAttack = {
        ...attack,
        kind: 'projectile' as const,
        pierce: 0,
        hitEffects: [
          ...(attack.hitEffects ?? []),
          { status: 'stun' as const, durationMs: 1000 },
        ],
      }
      this.performProjectile(
        mediumAttack,
        this.spearReleaseDirection(mediumAttack, direction, context.chargeBandId),
        context,
      )
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

  private spearReleaseDirection(
    attack: LastChancesAttackDefinition,
    fallback: LastChancesVector,
    chargeBandId: string | undefined,
  ): LastChancesVector {
    if (chargeBandId !== 'middle') return normalize(fallback)
    const target = this.enemies
      .filter(enemy => enemy.state !== 'dead')
      .map(enemy => ({
        enemy,
        distance: Math.sqrt(distanceSquared(this.player.position, enemy.position)),
      }))
      .filter(candidate => candidate.distance >= Math.max(80, attack.range * 0.25)
        && candidate.distance <= attack.range)
      .sort((left, right) => left.distance - right.distance)[0]?.enemy
    return target
      ? normalize({
          x: target.position.x - this.player.position.x,
          y: target.position.y - this.player.position.y,
        }, fallback)
      : normalize(fallback)
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

  private cancelHeldChannels(): void {
    if (this.heldChannels.size === 0) return
    const heldAreas = new Set(this.heldChannels.values())
    this.activeAreas = this.activeAreas.filter(area => !heldAreas.has(area))
    this.heldChannels.clear()
  }

  private controlInputSnapshot(
    hand: LastChancesHand,
    atMs: number,
  ): LastChancesGestureInputSnapshot {
    const legacy = this.gestures.snapshot(hand, atMs)
    // Touch is always DeepList, including while another scheme is selected.
    if (this.controlSchemeValue === 'legacy' || legacy.pressed || legacy.candidateGesture) return legacy
    const weapon = this.weapons.get(hand)
    const controls = weapon?.controls
    if (this.controlSchemeValue === 'mylorik') {
      const state = this.mylorikControls.snapshot(runtimeHandToPhysicalCluster(hand), atMs)
      const intent = state.mobilityPressed ? 'mobility' : 'technique'
      const phase = state.mobilityPressed
        ? state.mobilityHeldMs >= (this.config.input.mylorik?.techniqueHoldMs ?? 0)
          ? 'hold'
          : 'press'
        : state.techniqueArmed ? 'hold' : 'tap'
      const candidate = controls?.mylorik.activations
        .filter(activation => activation.intent === intent && activation.phase === phase)
        .sort((left, right) => right.priority - left.priority)[0]?.gesture ?? null
      const heldMs = state.mobilityPressed ? state.mobilityHeldMs : state.techniqueHeldMs
      const pressed = state.mobilityPressed || state.techniquePressed
      return {
        hand,
        phase: pressed ? 'pressing' : 'idle',
        pressed,
        progress: pressed
          ? clamp(heldMs / Math.max(1, this.config.input.mylorik?.techniqueHoldMs ?? 1), 0, 1)
          : 0,
        remainingMs: pressed
          ? Math.max(0, (this.config.input.mylorik?.techniqueHoldMs ?? 0) - heldMs)
          : 0,
        heldMs,
        sequence: pressed ? 'first' : null,
        candidateGesture: candidate,
        pendingChargeMs: heldMs,
      }
    }
    const state = this.dualSenseControls.snapshot(runtimeHandToPhysicalCluster(hand), atMs)
    const node = controls?.dualsense.nodes.find(candidate => candidate.id === state.nodeId)
    return {
      hand,
      phase: state.active ? 'pressing' : 'idle',
      pressed: state.active,
      progress: state.value,
      remainingMs: 0,
      heldMs: state.heldMs,
      sequence: state.active ? 'first' : null,
      candidateGesture: node?.gesture ?? null,
      pendingChargeMs: state.heldMs,
    }
  }

  private updateHeldWeaponMechanics(deltaMs: number): void {
    const now = this.frameNowMs || performance.now()
    for (const hand of LAST_CHANCES_HANDS) {
      const weapon = this.weapons.get(hand)
      if (!weapon) {
        this.stopHeldChannel(hand)
        this.releaseTriggerDetent(runtimeHandToPhysicalCluster(hand))
        continue
      }
      const input = this.controlInputSnapshot(hand, now)
      if (!input.pressed) this.releaseTriggerDetent(runtimeHandToPhysicalCluster(hand))
      const candidateAttack = input.candidateGesture
        ? weapon.attacks[input.candidateGesture]
        : null
      const activeBand = input.pressed && candidateAttack?.charge
        ? [...candidateAttack.charge.bands]
          .reverse()
          .find(band => input.heldMs >= band.minMs)
        : undefined
      if (this.controlSchemeValue === 'dualsense'
        && activeBand && activeBand.id !== this.feedbackChargeBandIds[hand]) {
        this.feedbackChargeBandIds[hand] = activeBand.id
        const bandIndex = candidateAttack?.charge?.bands.findIndex(band => band.id === activeBand.id) ?? 0
        const profile = bandIndex <= 0
          ? 'bandLight' as const
          : bandIndex === 1 ? 'bandMedium' as const : 'bandStrong' as const
        // Pulse-count coding: band N is N short pulses, so charge depth is
        // readable by touch on both tiers instead of a single stronger buzz.
        const bandTick = weapon.controls?.dualsense.haptics?.bandTick
          ?? DEFAULT_LAST_CHANCES_BAND_TICK
        this.feedbackController.emit({
          state: 'charge',
          profile,
          hand: runtimeHandToPhysicalCluster(hand),
          pattern: Array.from({ length: bandIndex + 1 }, (_, pulse) => ({
            delayMs: pulse * (bandTick.pulseMs + bandTick.gapMs),
            durationMs: bandTick.pulseMs,
            magnitude: Math.min(1, bandTick.magnitude + bandIndex * bandTick.magnitudeStep),
          })),
        })
      } else if (!activeBand) {
        this.feedbackChargeBandIds[hand] = null
      }
      const holdAttack = weapon.attacks.hold
      if (input.pressed
        && input.sequence === 'secondTap'
        && input.heldMs >= this.config.input.holdMs
        && this.gestureReady(hand, 'doubleTapHold')
        && weapon.attacks.doubleTapHold.behavior === 'spearKick') {
        this.player.armorMultiplier = 2
        this.player.armorMultiplierMs = Math.max(this.player.armorMultiplierMs, deltaMs + 80)
      }
      const existing = this.heldChannels.get(hand)
      const channelBehavior = holdAttack.behavior
      const classifiedAsHold = this.controlSchemeValue === 'dualsense'
        ? input.candidateGesture === 'hold' || existing !== undefined
        : input.sequence === 'first' && input.heldMs >= (
            this.controlSchemeValue === 'mylorik'
              ? this.config.input.mylorik.techniqueHoldMs
              : this.config.input.holdMs
          )
      const channelEligible = input.pressed
        && classifiedAsHold
        && this.gestureReady(hand, 'hold')
        && ['spearStance', 'axeSpin', 'spiderFlurry'].includes(channelBehavior ?? '')
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
        'hold',
      )
      this.activeAreas.push(area)
      this.heldChannels.set(hand, area)
      this.syncContinuationFeedback()
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
        perfectTimingMs: 0,
        fatigueMs: 0,
        roomTimingMisses: 0,
        consecutiveTimingMisses: 0,
        fatigueTriggeredByTapAtMs: Number.NEGATIVE_INFINITY,
        unterhauDueAtMs: 0,
        unterhauTargetId: null,
        unterhauTargetPosition: null,
        unterhauPrimed: false,
        lastMotionDamageBonus: 0,
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
    _attack: LastChancesAttackDefinition,
    hand: LastChancesHand,
  ): boolean {
    const weapon = this.weapons.get(hand)
    const interval = this.elapsedMs - state.lastTapAtMs
    state.lastTapAtMs = this.elapsedMs
    const perfectStartMs = tuningValue(weapon, 'rhythmPerfectStartMs', 500)
    const perfectEndMs = Math.max(
      perfectStartMs,
      tuningValue(weapon, 'rhythmPerfectEndMs', 600),
    )
    const firstTap = interval === Number.POSITIVE_INFINITY
    const missedTiming = !firstTap && (interval < perfectStartMs || interval > perfectEndMs)
    state.fatigueTriggeredByTapAtMs = Number.NEGATIVE_INFINITY
    if (interval < perfectStartMs) state.rhythm = 'early'
    else if (interval <= perfectEndMs) {
      state.rhythm = 'good'
      state.perfectTimingMs = tuningValue(weapon, 'rhythmPerfectFeedbackMs', 480)
    } else state.rhythm = firstTap ? 'idle' : 'late'

    if (missedTiming) {
      state.roomTimingMisses += 1
      state.consecutiveTimingMisses += 1
      const missesPerRoom = Math.max(
        1,
        Math.round(tuningValue(weapon, 'rhythmMissesPerRoomBeforeFatigue', 3)),
      )
      const consecutiveMisses = Math.max(
        1,
        Math.round(tuningValue(weapon, 'rhythmConsecutiveMissesBeforeFatigue', 2)),
      )
      if (state.roomTimingMisses >= missesPerRoom
        || state.consecutiveTimingMisses >= consecutiveMisses) {
        state.fatigueMs = Math.max(
          state.fatigueMs,
          tuningValue(weapon, 'rhythmFatigueMs', 2000),
        )
        state.roomTimingMisses = 0
        state.consecutiveTimingMisses = 0
        state.fatigueTriggeredByTapAtMs = this.elapsedMs
        this.tapCombos[hand].step = 0
      }
    } else if (!firstTap) {
      state.consecutiveTimingMisses = 0
    }
    this.activeSwordAdvance = {
      weaponId: weapon?.id ?? 'hybrid-sword',
      direction: normalize(this.player.aim),
      remainingDistance: tuningValue(weapon, 'advanceDistance', 14.4),
      speed: Math.max(1, tuningValue(weapon, 'advanceSpeed', 144)),
    }
    return true
  }

  private morphSwordAttack(weapon: LastChancesResolvedWeapon): void {
    const weaponId = weapon.id
    const morphing = this.activeAreas.filter(area => (
      area.weaponId === weaponId && area.attack.behavior === 'swordRhythm'
    ))
    for (const area of morphing) {
      this.addColliderTrace(this.activeAreaCollider(area), {
        ...area.attack,
        color: '#d9f1ff',
      })
    }
    if (morphing.length > 0) {
      this.activeAreas = this.activeAreas.filter(area => !morphing.includes(area))
    }
    if (this.activeSwordAdvance?.weaponId === weaponId) this.activeSwordAdvance = null
    this.weaponActionEnds.delete(weaponId)
    const state = this.weaponState(weapon)
    const withinDoubleTap = this.elapsedMs - state.lastTapAtMs <= this.config.input.doubleTapMs
    if (withinDoubleTap && state.fatigueTriggeredByTapAtMs === state.lastTapAtMs) {
      state.fatigueMs = 0
      state.fatigueTriggeredByTapAtMs = Number.NEGATIVE_INFINITY
    }
  }

  private prepareSwordOberhau(
    weapon: LastChancesResolvedWeapon,
    state: RuntimeWeaponState,
    hand: LastChancesHand,
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
  ): void {
    state.lastTapAtMs = Number.NEGATIVE_INFINITY
    state.rhythm = 'idle'
    state.perfectTimingMs = 0
    state.unterhauDueAtMs = this.elapsedMs + tuningValue(
      weapon,
      'unterhauHoldMs',
      1000,
    )
    if ((this.cooldownEnds.get(cooldownKey(hand, 'doubleTapHold')) ?? 0) > this.elapsedMs) {
      state.unterhauDueAtMs = 0
    }
    state.unterhauTargetId = null
    state.unterhauTargetPosition = {
      x: this.player.position.x + direction.x * attack.range,
      y: this.player.position.y + direction.y * attack.range,
    }
    state.unterhauPrimed = false
  }

  private cancelPendingUnterhau(weapon: LastChancesResolvedWeapon): void {
    const state = this.weaponState(weapon)
    state.unterhauDueAtMs = 0
    state.unterhauTargetId = null
    state.unterhauTargetPosition = null
    state.unterhauPrimed = false
  }

  private queueSwordUnterhau(
    context: AttackExecutionContext,
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    delayMs: number,
  ): void {
    const state = this.weaponState(context.weapon)
    const delayed: RuntimeDelayedAttack = {
      remainingMs: Math.max(0, delayMs),
      attack,
      direction: { ...direction },
      context: { ...context, gesture: 'doubleTapHold' },
      targetPosition: state.unterhauTargetPosition ? { ...state.unterhauTargetPosition } : undefined,
      targetEnemyId: state.unterhauTargetId,
    }
    const actionDurationMs = Math.max(0, delayMs) + attack.durationMs + (attack.lingerMs ?? 0)
    const unterhauCooldownMs = Math.max(
      attack.cooldownMs,
      context.weapon.attacks.doubleTap.cooldownMs
        * tuningValue(context.weapon, 'unterhauCooldownMultiplier', 3),
    )
    this.cooldownEnds.set(
      cooldownKey(context.hand, 'doubleTapHold'),
      this.elapsedMs + unterhauCooldownMs,
    )
    this.weaponActionEnds.set(context.weapon.id, this.elapsedMs + actionDurationMs)
    this.scheduleRecovery(context.weapon.id, attack.recoveryMs, actionDurationMs)
    if (delayed.remainingMs <= 0) {
      this.executeSwordFollowUp(delayed)
      state.unterhauDueAtMs = 0
      state.unterhauTargetId = null
      state.unterhauTargetPosition = null
      state.unterhauPrimed = false
      return
    }
    this.delayedAttacks.push(delayed)
  }

  private executeSwordFollowUp(delayed: RuntimeDelayedAttack): void {
    const weapon = delayed.context.weapon
    const maximumRange = weapon.attacks.doubleTap.range
    let automaticTarget: RuntimeEnemy | null = null
    if (delayed.targetEnemyId) {
      const originalTarget = this.enemies.find(enemy => enemy.id === delayed.targetEnemyId)
      if (originalTarget?.state !== 'dead'
        && Math.sqrt(distanceSquared(this.player.position, originalTarget.position))
          <= maximumRange + originalTarget.definition.radius) {
        automaticTarget = originalTarget
      } else {
        automaticTarget = this.enemies
          .filter(enemy => enemy.state !== 'dead'
            && Math.sqrt(distanceSquared(this.player.position, enemy.position))
              <= maximumRange + enemy.definition.radius)
          .sort((left, right) => (
            distanceSquared(this.player.position, left.position)
              - distanceSquared(this.player.position, right.position)
            || left.id.localeCompare(right.id)
          ))[0] ?? null
      }
    }
    if (!automaticTarget) {
      this.executeAttack(
        delayed.attack,
        this.resolveDelayedAttackDirection(delayed),
        delayed.context,
      )
      return
    }
    const direction = normalize({
      x: automaticTarget.position.x - this.player.position.x,
      y: automaticTarget.position.y - this.player.position.y,
    }, delayed.direction)
    this.addColliderTrace(
      resolveAttackCollider(this.player.position, direction, delayed.attack),
      delayed.attack,
    )
    this.addEffect('melee', delayed.attack, direction)
    this.damageEnemy(
      automaticTarget,
      delayed.attack,
      delayed.attack.knockback,
      direction,
      {
        weaponId: weapon.id,
        hand: delayed.context.hand,
        gesture: 'doubleTapHold',
        storedDot: delayed.context.storedDot,
        distance: Math.sqrt(distanceSquared(this.player.position, automaticTarget.position)),
      },
    )
  }

  private executePendingUnterhau(hand: LastChancesHand): boolean {
    const weapon = this.weapons.get(hand)
    if (weapon?.trait !== 'swordRhythm') return false
    const state = this.weaponState(weapon)
    if (state.unterhauDueAtMs <= 0) return false
    const attack = attackWithLastChancesAugment(weapon.attacks.doubleTapHold, weapon)
    const direction = normalize(this.player.aim)
    const resolution: LastChancesGestureResolution = {
      hand,
      gesture: 'doubleTapHold',
      atMs: this.elapsedMs,
      heldMs: tuningValue(weapon, 'unterhauHoldMs', 1000),
      firstHoldMs: 0,
    }
    this.lastGesture = {
      hand,
      gesture: 'doubleTapHold',
      attackName: attack.name,
      atMs: this.elapsedMs,
    }
    this.queueSwordUnterhau({
      weapon,
      hand,
      gesture: 'doubleTapHold',
      resolution,
      storedDot: null,
    }, attack, direction, 0)
    return true
  }

  private applySwordStagger(
    enemy: RuntimeEnemy,
    weapon: LastChancesResolvedWeapon,
  ): void {
    if (weapon.staggerEnabled === false || enemy.statuses.unstoppableMs > 0) return
    const staggerMs = Math.max(0, tuningValue(weapon, 'staggerDurationMs', 500))
    if (staggerMs <= 0) return
    const role = this.enemyCombatProfile(enemy).role
    enemy.statuses.stunMs = Math.max(enemy.statuses.stunMs, staggerMs)
    if (role !== 'elite' && role !== 'boss') return
    enemy.statuses.staggerAccumulatedMs += staggerMs
    if (enemy.statuses.staggerAccumulatedMs
      < tuningValue(weapon, 'unstoppableThresholdMs', 3000)) return
    enemy.statuses.staggerAccumulatedMs = 0
    enemy.statuses.unstoppableMs = Math.max(
      enemy.statuses.unstoppableMs,
      tuningValue(weapon, 'unstoppableDurationMs', 5000),
    )
    this.clearEnemyControlStatuses(enemy)
  }

  private clearEnemyControlStatuses(enemy: RuntimeEnemy): void {
    enemy.statuses.stunMs = 0
    enemy.statuses.disarmMs = 0
    enemy.statuses.slowMs = 0
    enemy.statuses.slowMultiplier = 1
    enemy.statuses.attackSlowMs = 0
    enemy.statuses.attackSlowMultiplier = 1
    enemy.statuses.boundMs = 0
  }

  private addSwordImpact(
    enemy: RuntimeEnemy,
    direction: LastChancesVector,
    attack: LastChancesAttackDefinition,
    intensity: number,
  ): void {
    this.effects.push({
      kind: 'hit',
      position: { ...enemy.position },
      direction: normalize(direction),
      range: 24 + 52 * clamp(intensity, 0, 1),
      radius: attack.radius,
      arcDegrees: attack.arcDegrees,
      color: intensity >= 0.85 ? '#fff0a8' : attack.color,
      remainingMs: 150 + 230 * clamp(intensity, 0, 1),
      totalMs: 150 + 230 * clamp(intensity, 0, 1),
      intensity: clamp(intensity, 0, 1),
    })
  }

  private createActiveArea(
    kind: RuntimeActiveArea['kind'],
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    weaponId: string,
    hand: LastChancesHand,
    storedDot: LastChancesStoredDot | null,
    channel: boolean,
    gesture?: LastChancesGesture,
  ): RuntimeActiveArea {
    return {
      kind,
      origin: { ...this.player.position },
      direction: { ...direction },
      attack: { ...attack },
      baseDamage: attack.damage,
      weaponId,
      hand,
      gesture,
      remainingMs: attack.durationMs + (attack.lingerMs ?? 0),
      totalMs: attack.durationMs + (attack.lingerMs ?? 0),
      hitIds: new Map(),
      remainingHits: (attack.pierce + 1) * Math.max(1, attack.repeatHits ?? 1),
      storedDot,
      sweepDegrees: attack.behavior === 'swordRhythm'
        ? -(attack.collider?.rotationDegrees ?? 0) / 2
        : 0,
      traceAccumulatorMs: Number.POSITIVE_INFINITY,
      channel,
      authoredRepeatHits: Math.max(1, attack.repeatHits ?? 1),
      baseRepeatIntervalMs: Math.max(
        1,
        attack.repeatIntervalMs ?? attack.collider?.tickMs ?? 120,
      ),
      rotationAssisted: false,
      latchedIds: new Set(),
      matchingAimMotionPx: 0,
      motionDamageBonus: 0,
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
    if (enemy.state === 'dead' || enemy.motherRetreat) return
    enemy.revealedMs = 900
    if (options.hand && options.gesture) {
      enemy.lastPlayerHit = { hand: options.hand, gesture: options.gesture }
      if (enemy.definition.role === 'elite') enemy.gestureHits[options.hand].add(options.gesture)
    }
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
        multiplier *= tuningValue(attack, 'executionDamageMultiplier', 3.25)
        enemy.statuses.openingMs = 0
        enemy.swordExecutionMarked = true
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

    if (weapon?.trait === 'swordRhythm' && !this.weapons.has('right')) {
      multiplier *= tuningValue(weapon, 'emptyOffhandDamageMultiplier', 1.5)
    }
    multiplier *= options.damageMultiplier ?? 1
    const scaledDamage = attack.damage * multiplier * this.player.stats.attackPower / 100
    const armor = attack.damageType === 'true'
      ? 0
      : Math.max(0, enemy.definition.armor ?? 0)
        - enemy.statuses.armorBreak
    const hpBeforeHit = enemy.hp
    enemy.hp = Math.max(0, enemy.hp - Math.max(0, scaledDamage - Math.max(0, armor)))
    this.applyCockroachMotherHealthGate(enemy)
    const damageDealt = hpBeforeHit - enemy.hp
    this.restoreFromLifesteal(damageDealt)
    if (damageDealt > 0 && attack.behavior === 'swordOpening' && state
      && state.unterhauDueAtMs > 0
      && state.unterhauTargetId === null) {
      state.unterhauTargetId = enemy.id
      state.unterhauTargetPosition = { ...enemy.position }
      state.unterhauPrimed = true
    }
    if (damageDealt > 0 && attack.behavior === 'swordRhythm') {
      this.addSwordImpact(enemy, direction, attack, options.impactIntensity ?? 0)
    }
    if (damageDealt > 0 && options.hand) {
      this.markSuccessfulHitFeedbackWindow(options.hand)
      if (this.controlSchemeValue === 'dualsense') {
        this.feedbackController.emit({
          state: 'impact',
          profile: 'impact',
          hand: runtimeHandToPhysicalCluster(options.hand),
          strength: criticalHit ? 1 : 0.7,
        })
      }
    }
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
    if (weapon?.trait === 'swordRhythm' && attack.behavior === 'swordRhythm') {
      this.applySwordStagger(enemy, weapon)
    }
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
    this.syncContinuationFeedback()
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
    if (enemy.definition.role === 'boss' || enemy.definition.bossPhases) {
      if (this.bossCheckpoint?.nodeId === this.currentNode?.id) this.bossCheckpoint = null
    }
    if (enemy.definition.cockroachMother) {
      this.cockroachesExtinct = true
      this.swarmSpawner = null
      this.holeStrikes = []
      for (const candidate of this.enemies) {
        if (candidate !== enemy && isCockroachDefinition(candidate.definition)) {
          candidate.state = 'dead'
          candidate.hp = 0
        }
      }
      for (const node of this.plan.nodes) {
        if (!node.swarm) continue
        const definition = this.enemyDefinitions.get(node.swarm.definitionId)
        if (definition && isCockroachDefinition(definition)) node.swarm = null
      }
      this.emitPlan()
    }
    this.recordMoveQuestKill(enemy)
    this.player.mentalHealth = Math.min(
      this.player.stats.maxMentalHealth,
      this.player.mentalHealth + this.config.mentalHealth.restoreOnKill,
    )
    for (const state of this.weaponStates.values()) {
      if (state.boundEnemyId !== enemy.id) continue
      state.boundEnemyId = null
      state.resource = state.maxResource
    }
    if (enemy.swordExecutionMarked) {
      for (const hand of LAST_CHANCES_HANDS) {
        if (this.weapons.get(hand)?.trait !== 'swordRhythm') continue
        this.cooldownEnds.delete(cooldownKey(hand, 'doubleTap'))
        this.cooldownEnds.delete(cooldownKey(hand, 'doubleTapHold'))
      }
    }
  }

  /** Kills attribute to the last player hit; deaths from lingering DoTs count for that gesture too. */
  private recordMoveQuestKill(enemy: RuntimeEnemy): void {
    const hit = enemy.lastPlayerHit
    if (hit && (hit.gesture === 'tap' || hit.gesture === 'hold')) {
      const quest = this.moveQuests[hit.hand]
      const questDone = hit.gesture === 'tap' ? quest.tapQuestDone : quest.holdQuestDone
      if (!questDone) {
        quest.roomKills[hit.gesture] += 1
        if (quest.roomKills[hit.gesture] >= MOVE_QUEST_KILLS_REQUIRED) {
          if (hit.gesture === 'tap') {
            quest.tapQuestDone = true
            this.queueMoveUnlock(hit.hand, 'doubleTap')
          } else {
            quest.holdQuestDone = true
            this.queueMoveUnlock(hit.hand, 'holdThenDoubleTap')
          }
        }
      }
    }
    if (enemy.definition.role !== 'elite') return
    for (const hand of LAST_CHANCES_HANDS) {
      const quest = this.moveQuests[hand]
      if (!this.moveQuestPrerequisitesDone(hand) || quest.comboQuestDone) continue
      const required = this.comboQuestGestures(hand)
      if (required.length === 0) continue
      if (!required.every(gesture => enemy.gestureHits[hand].has(gesture))) continue
      quest.comboQuestDone = true
      // The final reward opens immediately — the designer scoped "next room" to the kill quests only.
      quest.unlocked.doubleTapHold = true
    }
  }

  private queueMoveUnlock(hand: LastChancesHand, gesture: LastChancesGesture): void {
    const quest = this.moveQuests[hand]
    if (quest.unlocked[gesture] || quest.pendingUnlocks.includes(gesture)) return
    quest.pendingUnlocks.push(gesture)
  }

  /**
   * The elite combo quest requires every quest-tracked gesture the current
   * weapon can actually perform, so a designer-disabled slot cannot soft-lock it.
   */
  private comboQuestGestures(hand: LastChancesHand): LastChancesGesture[] {
    const weapon = this.weapons.get(hand)
    if (!weapon) return []
    return MOVE_QUEST_COMBO_GESTURES.filter((gesture) => {
      const attack = weapon.attacks[gesture]
      return this.moveQuests[hand].unlocked[gesture]
        && attack.enabled !== false
        && attack.behavior !== 'disabled'
    })
  }

  private moveQuestPrerequisitesDone(hand: LastChancesHand): boolean {
    const weapon = this.weapons.get(hand)
    if (!weapon) return false
    const quest = this.moveQuests[hand]
    const attackAvailable = (gesture: 'tap' | 'hold'): boolean => {
      const attack = weapon.attacks[gesture]
      return attack.enabled !== false && attack.behavior !== 'disabled'
    }
    return (!attackAvailable('tap') || quest.tapQuestDone)
      && (!attackAvailable('hold') || quest.holdQuestDone)
  }

  private damagePlayer(rawDamage: number, source: string): void {
    if (this.player.invulnerableMs > 0 || this.phase !== 'playing') return
    const damage = Math.max(
      1,
      rawDamage - this.effectivePlayerStats().armor * this.player.armorMultiplier,
    )
    this.player.hp = Math.max(0, this.player.hp - damage)
    this.player.invulnerableMs = this.config.player.invulnerabilityMs
    if (this.player.hp <= 0) this.killPlayer(`Killed by ${source}`)
  }

  /** Pure damage ignores armor; invulnerability frames still apply. */
  private damagePlayerPure(rawDamage: number, source: string): void {
    if (this.player.invulnerableMs > 0 || this.phase !== 'playing') return
    this.player.hp = Math.max(0, this.player.hp - Math.max(1, rawDamage))
    this.player.invulnerableMs = this.config.player.invulnerabilityMs
    if (this.player.hp <= 0) this.killPlayer(`Killed by ${source}`)
  }

  private activeArtifact(): LastChancesArtifactDefinition | null {
    const id = this.activeLoadout?.artifactId
    return id ? this.config.artifacts?.find(artifact => artifact.id === id) ?? null : null
  }

  private activeOutfit(): LastChancesOutfitDefinition | null {
    const id = this.activeLoadout?.outfitId
    return id ? this.config.outfits?.find(outfit => outfit.id === id) ?? null : null
  }

  private effectivePlayerStats(): LastChancesStats {
    const outfit = this.activeOutfit()
    return {
      ...this.player.stats,
      moveSpeed: this.player.stats.moveSpeed * (outfit?.moveSpeedMultiplier ?? 1),
      armor: this.player.stats.armor + (outfit?.armorBonus ?? 0),
    }
  }

  private damagePlayerMental(rawDamage: number): void {
    const reduction = clamp(this.activeArtifact()?.mentalDamageReduction ?? 0, 0, 1)
    this.player.mentalHealth = Math.max(
      0,
      this.player.mentalHealth - Math.max(0, rawDamage) * (1 - reduction),
    )
  }

  private restoreFromLifesteal(damageDealt: number): void {
    const ratio = clamp(this.activeArtifact()?.lifestealRatio ?? 0, 0, 1)
    if (ratio <= 0 || damageDealt <= 0 || this.player.hp <= 0) return
    this.player.hp = Math.min(
      this.player.stats.maxHp,
      this.player.hp + damageDealt * ratio,
    )
  }

  private spawnZoneAttack(enemy: RuntimeEnemy): void {
    const zone = enemy.definition.zone
    if (!zone || zone.shapes.length === 0) return
    const rng = createLastChancesRng(
      `${this.currentNode?.seed ?? 0}:zone:${enemy.id}:${Math.floor(this.elapsedMs)}`,
    )
    this.zoneAttacks.push({
      shape: zone.shapes[Math.floor(rng() * zone.shapes.length)],
      center: { ...this.player.position },
      size: zone.size,
      rotationRadians: rng() * Math.PI * 2,
      spawnedAtMs: this.elapsedMs,
      detonateAtMs: this.elapsedMs + zone.escapeMs,
      damageMaxHpRatio: zone.damageMaxHpRatio,
      sourceName: enemy.definition.name,
    })
  }

  private updateZoneAttacks(): void {
    if (this.zoneAttacks.length === 0) return
    const pending: RuntimeZoneAttack[] = []
    for (const zone of this.zoneAttacks) {
      if (this.elapsedMs < zone.detonateAtMs) {
        pending.push(zone)
        continue
      }
      if (this.playerInsideZone(zone)) {
        this.damagePlayerPure(this.player.stats.maxHp * zone.damageMaxHpRatio, zone.sourceName)
      }
    }
    this.zoneAttacks = pending
  }

  private playerInsideZone(zone: RuntimeZoneAttack): boolean {
    // Test the player circle in the zone's local (unrotated) frame.
    const local = rotateVector({
      x: this.player.position.x - zone.center.x,
      y: this.player.position.y - zone.center.y,
    }, -zone.rotationRadians)
    const radius = this.config.player.radius
    if (zone.shape === 'circle') {
      return local.x * local.x + local.y * local.y <= (zone.size + radius) ** 2
    }
    const vertices = zoneShapeLocalVertices(zone)
    return circleOverlapsConvexPolygon(local, radius, vertices)
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
    this.clearCombatTransients()
    this.deathReason = reason
    this.phase = this.chances > 0 ? 'dead' : 'outOfChances'
    this.emitSnapshot(true)
  }

  private completeRoom(): void {
    if (!this.currentNode || this.phase !== 'playing') return
    this.clearCombatTransients()
    if (this.currentNode.interaction && !this.interactionResolved) {
      this.phase = 'planning'
      this.routeMapVisible = false
      this.availableNodeIds = []
      this.selectedNodeId = null
      this.selectedInteractionChoiceId = null
      this.rewardChest = {
        position: this.rewardChestPosition(),
        opened: false,
      }
      this.emitSnapshot(true)
      return
    }
    this.finishRoomTransition()
    this.emitSnapshot(true)
  }

  private clearCombatTransients(): void {
    this.projectiles = []
    this.holeStrikes = []
    this.activeDash = null
    this.activeSwordAdvance = null
    this.activeAreas = []
    this.heldChannels.clear()
    this.effects = []
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.provisionalParry = null
    for (const hand of LAST_CHANCES_HANDS) this.resetImmediateSwordInput(hand)
    this.cleanupControlInputs(false)
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
    this.rewardChest = null
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
      this.routeMapVisible = false
      this.availableNodeIds = [...this.currentNode.nextNodeIds]
      this.selectedNodeId = this.controlSchemeValue === 'dualsense'
        ? null
        : this.availableNodeIds[0] ?? null
    }
    this.selectedInteractionChoiceId = null
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
      || effect.secondaryWeaponId !== undefined
      || effect.artifactId !== undefined
      || effect.outfitId !== undefined)) {
      this.activeLoadout = this.normalizeLoadoutAugments({
        ...this.activeLoadout,
        primaryWeaponId: effect.primaryWeaponId ?? this.activeLoadout.primaryWeaponId,
        secondaryWeaponId: effect.secondaryWeaponId !== undefined
          ? effect.secondaryWeaponId
          : this.activeLoadout.secondaryWeaponId,
        artifactId: effect.artifactId !== undefined
          ? effect.artifactId
          : this.activeLoadout.artifactId,
        outfitId: effect.outfitId !== undefined
          ? effect.outfitId
          : this.activeLoadout.outfitId,
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
    this.triggerDetents.left = null
    this.triggerDetents.right = null
    this.pushTriggerBaseline()
  }

  private normalizeLoadoutAugments(
    loadout: LastChancesLoadoutDefinition,
  ): LastChancesLoadoutDefinition {
    const normalized = { ...loadout }
    const catalog = new Map(this.config.weapons.map(weapon => [weapon.id, weapon]))
    const primary = normalized.primaryWeaponId
      ? catalog.get(normalized.primaryWeaponId)
      : undefined
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
    this.routeMapVisible = true
    this.paused = false
    this.currentNode = null
    this.availableNodeIds = this.plan.tiers[0].map(node => node.id)
    this.selectedNodeId = this.controlSchemeValue === 'dualsense'
      ? null
      : this.availableNodeIds[0] ?? null
    this.selectedInteractionChoiceId = null
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
    this.zoneAttacks = []
    this.holeStrikes = []
    this.swarmSpawner = null
    this.turrets = []
    this.turretAlarmMs = 0
    this.altarPromptActive = false
    this.groundWeapons = []
    this.ninjaDashReadyAtMs = 0
    // Quest unlocks and completed quests survive the death; only room-scoped counters reset.
    for (const hand of LAST_CHANCES_HANDS) {
      this.moveQuests[hand].roomKills = { tap: 0, hold: 0 }
    }
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
    this.activeSwordAdvance = null
    this.roomElapsedMs = 0
    this.hazardHitCycles.clear()
    this.hazardSuppressedUntil.clear()
    this.interactionResolved = false
    this.rewardChest = null
    this.cooldownEnds.clear()
    this.resetTapCombos()
    this.gestures.reset()
    for (const hand of LAST_CHANCES_HANDS) this.resetImmediateSwordInput(hand)
    this.pressedKeys.clear()
    this.touchMove = { x: 0, y: 0 }
    this.touchAim = { x: 0, y: 0 }
    this.gamepadMove = { x: 0, y: 0 }
    this.gamepadAim = { x: 0, y: 0 }
    this.retainedGamepadAim = null
    this.pointerClientX = null
    this.pointerDeltaX = 0
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

  private addGroundWeapon(
    weaponId: string,
    augment: LastChancesAugment | undefined,
    position: LastChancesVector,
  ): void {
    if (!this.config.weapons.some(weapon => weapon.id === weaponId)) return
    this.groundWeapons.push({
      id: `ground-weapon-${this.nextGroundWeaponId++}`,
      weaponId,
      augment: augment ?? 'none',
      position: { ...position },
    })
  }

  private dropRightHandWeapon(
    position: LastChancesVector,
    heldRight: LastChancesResolvedWeapon,
  ): void {
    if (!this.activeLoadout) return
    if (this.activeLoadout.secondaryWeaponId === heldRight.id) {
      this.addGroundWeapon(heldRight.id, this.activeLoadout.secondaryAugment, position)
      this.activeLoadout = {
        ...this.activeLoadout,
        secondaryWeaponId: null,
        secondaryAugment: 'none',
      }
      return
    }
    if (this.activeLoadout.primaryWeaponId === heldRight.id) {
      this.addGroundWeapon(heldRight.id, this.activeLoadout.primaryAugment, position)
      this.activeLoadout = {
        ...this.activeLoadout,
        primaryWeaponId: null,
        primaryAugment: 'none',
      }
    }
  }

  private nearestGroundWeapon(): RuntimeGroundWeapon | null {
    return this.groundWeapons
      .map(weapon => ({
        weapon,
        distance: Math.sqrt(distanceSquared(this.player.position, weapon.position)),
      }))
      .filter(candidate => candidate.distance <= 105)
      .sort((left, right) => left.distance - right.distance)[0]?.weapon ?? null
  }

  private nearestActiveTurret(): RuntimeTurret | null {
    return this.turrets
      .filter(turret => !turret.disabled)
      .map(turret => ({
        turret,
        distance: Math.sqrt(distanceSquared(
          this.player.position,
          turret.definition.position,
        )),
      }))
      .filter(candidate => candidate.distance <= candidate.turret.definition.interactionRange)
      .sort((left, right) => left.distance - right.distance)[0]?.turret ?? null
  }

  private nearbyRewardChest(): RuntimeRewardChest | null {
    if (!this.rewardChest || this.rewardChest.opened) return null
    return Math.sqrt(distanceSquared(this.player.position, this.rewardChest.position)) <= 115
      ? this.rewardChest
      : null
  }

  private rewardChestPosition(): LastChancesVector {
    if (!this.currentNode) return { ...this.player.position }
    const arena = this.currentNode.arena
    const candidates: LastChancesVector[] = [
      { x: arena.width * 0.72, y: arena.height * 0.5 },
      { x: arena.width * 0.58, y: arena.height * 0.28 },
      { x: arena.width * 0.58, y: arena.height * 0.72 },
      { x: arena.width * 0.45, y: arena.height * 0.5 },
    ]
    const radius = 26
    return candidates.find(candidate => (
      candidate.x >= radius
      && candidate.x <= arena.width - radius
      && candidate.y >= radius
      && candidate.y <= arena.height - radius
      && !arena.obstacles.some(obstacle => pointHitsObstacle(candidate, radius, obstacle))
    )) ?? { ...this.player.position }
  }

  private pickUpGroundWeapon(groundWeapon: RuntimeGroundWeapon): boolean {
    if (!this.activeLoadout) return false
    const definition = this.config.weapons.find(weapon => weapon.id === groundWeapon.weaponId)
    if (!definition) return false
    const pickedIndex = this.groundWeapons.findIndex(weapon => weapon.id === groundWeapon.id)
    if (pickedIndex < 0) return false
    this.groundWeapons.splice(pickedIndex, 1)
    const next = { ...this.activeLoadout }
    const mode = definition.equipMode ?? (definition.hand === 'right' ? 'secondaryOnly' : 'primaryOnly')
    if (mode === 'secondaryOnly') {
      if (next.secondaryWeaponId) {
        this.addGroundWeapon(next.secondaryWeaponId, next.secondaryAugment, groundWeapon.position)
      }
      next.secondaryWeaponId = definition.id
      next.secondaryAugment = groundWeapon.augment
    } else {
      if (next.primaryWeaponId) {
        this.addGroundWeapon(next.primaryWeaponId, next.primaryAugment, groundWeapon.position)
      }
      next.primaryWeaponId = definition.id
      next.primaryAugment = groundWeapon.augment
      if (mode === 'twoHanded') {
        if (next.secondaryWeaponId) {
          this.addGroundWeapon(next.secondaryWeaponId, next.secondaryAugment, groundWeapon.position)
        }
        next.secondaryWeaponId = null
        next.secondaryAugment = next.primaryAugment
      }
    }
    this.activeLoadout = this.normalizeLoadoutAugments(next)
    this.rebuildWeapons()
    this.cooldownEnds.clear()
    this.resetTapCombos()
    this.lastGesture = {
      hand: mode === 'secondaryOnly' ? 'right' : 'left',
      gesture: 'tap',
      attackName: `${definition.name} подобран`,
      atMs: this.elapsedMs,
    }
    this.emitSnapshot(true)
    return true
  }

  private startEmptyRightHandDash(): boolean {
    const dash = this.activeOutfit()?.emptyRightHandDash
    if (!dash || !this.canExploreRoom() || this.routeMapVisible || this.paused
      || this.weapons.has('right') || this.activeDash || this.elapsedMs < this.ninjaDashReadyAtMs) {
      return false
    }
    const direction = normalize(this.player.aim, { x: 1, y: 0 })
    const attack: LastChancesAttackDefinition = {
      name: 'Рывок одежды ниндзя',
      kind: 'dash',
      behavior: 'poleVault',
      damage: 0,
      cooldownMs: dash.cooldownMs,
      range: dash.distance,
      radius: this.config.player.radius,
      arcDegrees: 0,
      durationMs: dash.durationMs,
      projectileSpeed: 0,
      pierce: 0,
      knockback: 0,
      color: '#718aa2',
      collider: { shape: 'capsule', width: this.config.player.radius * 2, traceMs: 260 },
      invulnerabilityMs: dash.durationMs,
    }
    this.activeDash = {
      origin: { ...this.player.position },
      direction,
      remainingDistance: dash.distance,
      speed: dash.distance / Math.max(0.08, dash.durationMs / 1000),
      damage: 0,
      radius: this.config.player.radius,
      knockback: 0,
      hitIds: new Set(),
      hitRecords: new Map(),
      remainingHits: 0,
      color: attack.color,
      attack,
      weaponId: this.activeOutfit()?.id ?? 'ninja-outfit',
      hand: 'right',
      storedDot: null,
      landingBurst: false,
      trailAccumulatorMs: 0,
      elapsedMs: 0,
    }
    this.player.invulnerableMs = Math.max(this.player.invulnerableMs, dash.durationMs)
    this.ninjaDashReadyAtMs = this.elapsedMs + dash.cooldownMs
    this.addEffect('dash', attack, direction)
    return true
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

  private attackPathBlocked(start: LastChancesVector, end: LastChancesVector): boolean {
    return this.currentNode?.arena.obstacles.some(obstacle => (
      segmentHitsObstacle(start, end, obstacle)
    )) ?? false
  }

  private attackColliderHitsCircle(
    collider: LastChancesRuntimeCollider,
    target: LastChancesVector,
    radius: number,
    attack: LastChancesAttackDefinition | undefined,
    origin = colliderOrigin(collider),
  ): boolean {
    if (!colliderHitsCircle(collider, target, radius)) return false
    return attack?.collider?.passesThroughWalls === true
      || !this.attackPathBlocked(origin, target)
  }

  private circleCanOccupy(position: LastChancesVector, radius: number): boolean {
    if (!this.currentNode) return false
    const arena = this.currentNode.arena
    return position.x >= radius
      && position.y >= radius
      && position.x <= arena.width - radius
      && position.y <= arena.height - radius
      && !arena.obstacles.some(obstacle => pointHitsObstacle(position, radius, obstacle))
  }

  private maximumSafeMovementFraction(
    position: LastChancesVector,
    delta: LastChancesVector,
    radius: number,
  ): number {
    if (vectorLength(delta) <= EPSILON) return 0
    const candidate = {
      x: position.x + delta.x,
      y: position.y + delta.y,
    }
    if (this.circleCanOccupy(candidate, radius)) return 1
    let safe = 0
    let blocked = 1
    for (let iteration = 0; iteration < 14; iteration += 1) {
      const fraction = (safe + blocked) / 2
      const probe = {
        x: position.x + delta.x * fraction,
        y: position.y + delta.y * fraction,
      }
      if (this.circleCanOccupy(probe, radius)) safe = fraction
      else blocked = fraction
    }
    return safe
  }

  /**
   * Advances a circle in short continuous steps. At contact, the unresolved
   * distance is projected onto a free wall tangent and keeps its full length,
   * matching the no-slowdown wall sliding used by character controllers.
   */
  private moveCircle(position: LastChancesVector, delta: LastChancesVector, radius: number): void {
    if (!this.currentNode) return
    const distance = vectorLength(delta)
    if (distance <= EPSILON) return
    const maximumStep = Math.max(1, Math.min(4, radius * 0.25))
    const steps = Math.max(1, Math.ceil(distance / maximumStep))
    const step = { x: delta.x / steps, y: delta.y / steps }
    const stepDistance = distance / steps

    for (let index = 0; index < steps; index += 1) {
      const safeFraction = this.maximumSafeMovementFraction(position, step, radius)
      position.x += step.x * safeFraction
      position.y += step.y * safeFraction
      if (safeFraction >= 1 - EPSILON) continue

      const slideDistance = stepDistance * (1 - safeFraction)
      const slides: Array<{ delta: LastChancesVector, distance: number, alignment: number }> = []
      for (const axis of ['x', 'y'] as const) {
        if (Math.abs(step[axis]) <= EPSILON) continue
        const slide = {
          x: axis === 'x' ? Math.sign(step.x) * slideDistance : 0,
          y: axis === 'y' ? Math.sign(step.y) * slideDistance : 0,
        }
        const fraction = this.maximumSafeMovementFraction(position, slide, radius)
        const resolved = { x: slide.x * fraction, y: slide.y * fraction }
        slides.push({
          delta: resolved,
          distance: vectorLength(resolved),
          alignment: Math.abs(step[axis]),
        })
      }
      const best = slides.sort((left, right) => (
        right.distance - left.distance || right.alignment - left.alignment
      ))[0]
      if (!best || best.distance <= EPSILON) continue
      position.x += best.delta.x
      position.y += best.delta.y
    }
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
    if (this.retainedGamepadAim) return this.retainedGamepadAim
    if (vectorLength(this.touchAim) > this.config.input.aimDeadZone) return this.touchAim
    return this.pointerAim
  }

  private pollGamepad(): void {
    const hidGamepad = this.feedbackController.hidGamepadSnapshot()
    if (hidGamepad) {
      // A Bluetooth pad in extended report mode (M118) freezes its Gamepad-API
      // entry, so the WebHID reading replaces the list entirely — the frozen
      // pad must not win arbitration, and Tier-1 rumble stays parked while the
      // enhanced output owns feedback.
      void this.feedbackController.setGamepad(null)
      this.applyGamepadReading(this.gamepadAdapter.poll([hidGamepad]))
      return
    }
    if (typeof navigator === 'undefined' || typeof navigator.getGamepads !== 'function') {
      void this.feedbackController.setGamepad(null)
      this.applyGamepadReading(null)
      return
    }
    const gamepads = Array.from(navigator.getGamepads())
    const reading = this.gamepadAdapter.poll(gamepads)
    const active = reading.activeIndex === null
      ? null
      : gamepads.find(gamepad => gamepad?.index === reading.activeIndex) ?? null
    void this.feedbackController.setGamepad(active as LastChancesHapticGamepadLike | null)
    this.applyGamepadReading(reading)
  }

  private applyGamepadReading(reading: LastChancesGamepadReading | null): void {
    const previousActiveIndex = this.gamepadState.activeIndex
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
    const activePadChanged = previousActiveIndex !== nextState.activeIndex
    this.gamepadState = nextState
    this.gamepadMove = reading?.move ?? { x: 0, y: 0 }
    this.gamepadAim = reading?.aim ?? { x: 0, y: 0 }
    if (!nextState.connected || activePadChanged) this.retainedGamepadAim = null
    if (nextState.connected && vectorLength(this.gamepadAim) > this.config.input.aimDeadZone) {
      this.retainedGamepadAim = { ...this.gamepadAim }
    }

    const buttonAt = (index: number | undefined): boolean => {
      if (index === undefined) return false
      const button = reading?.canonicalButtons[index]
      return button?.pressed === true || (button?.value ?? 0) >= 0.5
    }
    const valueAt = (index: number | undefined): number => {
      if (index === undefined) return 0
      const value = reading?.canonicalButtons[index]?.value ?? 0
      return clamp(value, 0, 1)
    }
    if (activePadChanged || this.gamepadNeedsReseed) {
      if (activePadChanged && previousActiveIndex !== null) {
        this.commitHeldChannels()
        this.commitOrCancelProvisionalParry()
        this.gestures.reset()
        this.mylorikControls.reset()
        this.dualSenseControls.reset()
      }
      const mylorik = this.config.input.mylorik
      const dualsense = this.config.input.dualsense
      this.gamepadButtons.left = buttonAt(this.config.input.gamepadLeftButton)
      this.gamepadButtons.right = buttonAt(this.config.input.gamepadRightButton)
      this.gamepadControlButtons.leftStrike = buttonAt(
        this.controlSchemeValue === 'dualsense' ? dualsense?.gamepad.leftBumper : mylorik?.gamepad.leftBumper,
      )
      this.gamepadControlButtons.rightStrike = buttonAt(
        this.controlSchemeValue === 'dualsense' ? dualsense?.gamepad.rightBumper : mylorik?.gamepad.rightBumper,
      )
      this.gamepadControlButtons.leftTechnique = buttonAt(mylorik?.gamepad.leftTrigger)
      this.gamepadControlButtons.rightTechnique = buttonAt(mylorik?.gamepad.rightTrigger)
      this.gamepadControlButtons.mobility = buttonAt(mylorik?.gamepad.mobilityButton)
      this.gamepadControlButtons.interact = buttonAt(mylorik?.gamepad.interactButton)
      this.gamepadControlButtons.circle = buttonAt(dualsense?.gamepad.circle)
      this.gamepadControlButtons.cross = buttonAt(dualsense?.gamepad.cross)
      this.gamepadControlButtons.options = buttonAt(dualsense?.gamepad.options)
      this.gamepadControlButtons.dpadUp = reading?.buttons.dpadUp ?? false
      this.gamepadControlButtons.dpadDown = reading?.buttons.dpadDown ?? false
      this.gamepadControlButtons.dpadLeft = reading?.buttons.dpadLeft ?? false
      this.gamepadControlButtons.dpadRight = reading?.buttons.dpadRight ?? false
      this.gamepadTriggerValues.left = valueAt(dualsense?.gamepad.leftTrigger)
      this.gamepadTriggerValues.right = valueAt(dualsense?.gamepad.rightTrigger)
      this.gamepadNeedsReseed = false
      if (metadataChanged) this.emitSnapshot(true)
      return
    }

    const nextButtons: Record<LastChancesHand, boolean> = {
      left: buttonAt(this.config.input.gamepadLeftButton),
      right: buttonAt(this.config.input.gamepadRightButton),
    }
    const leftPressed = nextButtons.left && !this.gamepadButtons.left
    const rightPressed = nextButtons.right && !this.gamepadButtons.right
    const mylorik = this.config.input.mylorik
    const dualsense = this.config.input.dualsense
    const leftStrike = buttonAt(
      this.controlSchemeValue === 'dualsense' ? dualsense?.gamepad.leftBumper : mylorik?.gamepad.leftBumper,
    )
    const rightStrike = buttonAt(
      this.controlSchemeValue === 'dualsense' ? dualsense?.gamepad.rightBumper : mylorik?.gamepad.rightBumper,
    )
    const leftStrikePressed = leftStrike && !this.gamepadControlButtons.leftStrike
    const rightStrikePressed = rightStrike && !this.gamepadControlButtons.rightStrike
    const leftTechnique = buttonAt(mylorik?.gamepad.leftTrigger)
    const rightTechnique = buttonAt(mylorik?.gamepad.rightTrigger)
    const mobility = buttonAt(mylorik?.gamepad.mobilityButton)
    const interact = buttonAt(mylorik?.gamepad.interactButton)
    const cross = buttonAt(dualsense?.gamepad.cross)
    const circle = buttonAt(dualsense?.gamepad.circle)
    const options = buttonAt(dualsense?.gamepad.options)
    const crossPressed = cross && !this.gamepadControlButtons.cross
    const circlePressed = circle && !this.gamepadControlButtons.circle
    const optionsPressed = options && !this.gamepadControlButtons.options
    const dpadMoved = (reading?.buttons.dpadRight && !this.gamepadControlButtons.dpadRight)
      || (reading?.buttons.dpadDown && !this.gamepadControlButtons.dpadDown)
      ? 1
      : (reading?.buttons.dpadLeft && !this.gamepadControlButtons.dpadLeft)
        || (reading?.buttons.dpadUp && !this.gamepadControlButtons.dpadUp) ? -1 : 0
    const now = performance.now()

    if (optionsPressed) {
      const handled = this.callbacks.onUiCommand?.('pause') ?? false
      if (!handled && this.phase === 'playing') this.setPaused(!this.paused)
    }
    if (circlePressed && this.controlSchemeValue === 'dualsense') {
      const handled = this.callbacks.onUiCommand?.('back') ?? false
      if (!handled && this.phase !== 'playing') this.blockSemanticInput({
        scheme: 'dualsense',
        physicalHand: 'left',
        hand: 'right',
        intent: 'mobility',
        phase: 'press',
        source: 'gamepad',
        atMs: now,
        heldMs: 0,
        value: 1,
        commit: true,
      }, null)
    }

    if (this.phase === 'planning' && this.routeMapVisible && !this.paused) {
      const menuAxis = reading?.move ?? { x: 0, y: 0 }
      const dominantAxis = Math.abs(menuAxis.x) >= Math.abs(menuAxis.y) ? menuAxis.x : menuAxis.y
      if (Math.abs(dominantAxis) >= 0.55 && !this.gamepadMenuAxisEngaged) {
        this.cycleSelectedNode(dominantAxis > 0 ? 1 : -1)
        this.gamepadMenuAxisEngaged = true
      } else if (Math.abs(dominantAxis) <= 0.25) {
        this.gamepadMenuAxisEngaged = false
      }
      if (dpadMoved !== 0) this.cycleSelectedNode(dpadMoved)
      if (this.controlSchemeValue === 'dualsense') {
        if (crossPressed) {
          const handled = this.callbacks.onUiCommand?.('confirm') ?? false
          if (!handled && this.selectedNodeId) this.chooseNode(this.selectedNodeId)
        }
      } else {
        if (leftPressed) this.cycleSelectedNode(1)
        if (rightPressed && this.selectedNodeId) this.chooseNode(this.selectedNodeId)
      }
    } else if (!this.paused && this.canUseRoomActions()) {
      if (this.controlSchemeValue === 'legacy') {
        const interactionChord = leftPressed && rightPressed && this.interact()
        if (!interactionChord) {
          for (const hand of LAST_CHANCES_HANDS) {
            const pressed = nextButtons[hand]
            if (pressed && !this.gamepadButtons[hand]) this.press(hand)
            if (!pressed && this.gamepadButtons[hand]) this.release(hand)
          }
        }
      } else if (this.controlSchemeValue === 'mylorik') {
        if (leftStrikePressed) this.mylorikControls.pressStrike('left', now, 'gamepad')
        if (rightStrikePressed) this.mylorikControls.pressStrike('right', now, 'gamepad')
        if (leftTechnique && !this.gamepadControlButtons.leftTechnique) {
          this.mylorikControls.pressTechnique('left', now, 'gamepad')
        }
        if (!leftTechnique && this.gamepadControlButtons.leftTechnique) {
          this.mylorikControls.releaseTechnique('left', now, 'gamepad')
        }
        if (rightTechnique && !this.gamepadControlButtons.rightTechnique) {
          this.mylorikControls.pressTechnique('right', now, 'gamepad')
        }
        if (!rightTechnique && this.gamepadControlButtons.rightTechnique) {
          this.mylorikControls.releaseTechnique('right', now, 'gamepad')
        }
        if (mobility && !this.gamepadControlButtons.mobility) {
          this.activeMobilityPhysicalHand = this.selectMobilityPhysicalHand(now)
          if (this.activeMobilityPhysicalHand) {
            this.mylorikControls.pressMobility(this.activeMobilityPhysicalHand, now, 'gamepad')
          }
        }
        if (!mobility && this.gamepadControlButtons.mobility && this.activeMobilityPhysicalHand) {
          this.mylorikControls.releaseMobility(this.activeMobilityPhysicalHand, now, 'gamepad')
          this.activeMobilityPhysicalHand = null
        }
        if (interact && !this.gamepadControlButtons.interact) this.interact()
      } else {
        if (leftStrikePressed) this.dualSenseControls.pressBumper('left', now, 'gamepad')
        if (rightStrikePressed) this.dualSenseControls.pressBumper('right', now, 'gamepad')
        this.dualSenseControls.updateTrigger(
          'left',
          valueAt(dualsense?.gamepad.leftTrigger),
          now,
          this.weapons.get(physicalClusterToRuntimeHand('left'))?.controls,
          'gamepad',
        )
        this.dualSenseControls.updateTrigger(
          'right',
          valueAt(dualsense?.gamepad.rightTrigger),
          now,
          this.weapons.get(physicalClusterToRuntimeHand('right'))?.controls,
          'gamepad',
        )
        if (crossPressed) this.interact()
        // Circle is intentionally a combat no-op under DualSense.
      }
    } else if (!this.paused && this.phase === 'interaction') {
      if (this.controlSchemeValue === 'dualsense') {
        const menuAxis = reading?.move ?? { x: 0, y: 0 }
        const dominantAxis = Math.abs(menuAxis.x) >= Math.abs(menuAxis.y) ? menuAxis.x : menuAxis.y
        if ((Math.abs(dominantAxis) >= 0.55 && !this.gamepadChoiceAxisEngaged) || dpadMoved !== 0) {
          this.cycleSelectedInteractionChoice(dpadMoved || (dominantAxis > 0 ? 1 : -1))
          this.gamepadChoiceAxisEngaged = true
        } else if (Math.abs(dominantAxis) <= 0.25) this.gamepadChoiceAxisEngaged = false
        if (crossPressed && this.selectedInteractionChoiceId) {
          this.chooseInteraction(this.selectedInteractionChoiceId)
        }
      } else if (rightPressed) {
        const choice = this.currentNode?.interaction?.choices.find(candidate => (
          this.interactionChoiceAvailable(candidate)
        ))
        if (choice) this.chooseInteraction(choice.id)
      }
    } else if (!this.paused) {
      const confirmPressed = this.controlSchemeValue === 'dualsense' ? crossPressed : rightPressed
      if (confirmPressed) {
        const handled = this.controlSchemeValue === 'dualsense'
          ? this.callbacks.onUiCommand?.('confirm') ?? false
          : false
        if (!handled) {
          if (this.phase === 'dead') this.retryAttempt()
          if (this.phase === 'won' || this.phase === 'outOfChances') this.newGeneration()
        }
      }
    }

    this.gamepadButtons.left = nextButtons.left
    this.gamepadButtons.right = nextButtons.right
    this.gamepadControlButtons.leftStrike = leftStrike
    this.gamepadControlButtons.rightStrike = rightStrike
    this.gamepadControlButtons.leftTechnique = leftTechnique
    this.gamepadControlButtons.rightTechnique = rightTechnique
    this.gamepadControlButtons.mobility = mobility
    this.gamepadControlButtons.interact = interact
    this.gamepadControlButtons.circle = circle
    this.gamepadControlButtons.cross = cross
    this.gamepadControlButtons.options = options
    this.gamepadControlButtons.dpadUp = reading?.buttons.dpadUp ?? false
    this.gamepadControlButtons.dpadDown = reading?.buttons.dpadDown ?? false
    this.gamepadControlButtons.dpadLeft = reading?.buttons.dpadLeft ?? false
    this.gamepadControlButtons.dpadRight = reading?.buttons.dpadRight ?? false
    this.gamepadTriggerValues.left = valueAt(dualsense?.gamepad.leftTrigger)
    this.gamepadTriggerValues.right = valueAt(dualsense?.gamepad.rightTrigger)
    if (metadataChanged) this.emitSnapshot(true)
  }

  private cycleSelectedNode(direction: number): void {
    if (this.availableNodeIds.length === 0) return
    const currentIndex = this.selectedNodeId === null ? -1 : this.availableNodeIds.indexOf(this.selectedNodeId)
    const baseIndex = currentIndex < 0 ? (direction > 0 ? -1 : 0) : currentIndex
    const nextIndex = (baseIndex + direction + this.availableNodeIds.length)
      % this.availableNodeIds.length
    this.selectedNodeId = this.availableNodeIds[nextIndex]
    this.emitSnapshot(true)
  }

  private cycleSelectedInteractionChoice(direction: number): void {
    const choices = this.currentNode?.interaction?.choices
      .filter(choice => this.interactionChoiceAvailable(choice)) ?? []
    if (choices.length === 0) return
    const currentIndex = this.selectedInteractionChoiceId === null
      ? -1
      : choices.findIndex(choice => choice.id === this.selectedInteractionChoiceId)
    const baseIndex = currentIndex < 0 ? (direction > 0 ? -1 : 0) : currentIndex
    const nextIndex = (baseIndex + direction + choices.length) % choices.length
    this.selectedInteractionChoiceId = choices[nextIndex].id
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

  private selectMobilityPhysicalHand(atMs: number): LastChancesHand | null {
    const candidates = (['left', 'right'] as const).flatMap((physicalHand) => {
      const hand = physicalClusterToRuntimeHand(physicalHand)
      const weapon = this.weapons.get(hand)
      const techniqueArmed = this.mylorikControls.snapshot(physicalHand, atMs).techniqueArmed
      return (weapon?.controls?.mylorik.activations ?? [])
        .filter(activation => activation.intent === 'mobility')
        .filter(activation => !activation.context
          || this.controlContextActive(hand, activation.context)
          || (activation.context === 'continuation' && techniqueArmed))
        .map(activation => ({ physicalHand, hand, activation }))
    }).sort((left, right) => right.activation.priority - left.activation.priority)
    const ready = candidates.find(candidate => this.gestureReady(
      candidate.hand,
      candidate.activation.gesture,
    ))
    return (ready ?? candidates[0])?.physicalHand ?? null
  }

  private updateKeyboardDualSenseTriggers(atMs: number): void {
    if (this.controlSchemeValue !== 'dualsense'
      || !this.canUseRoomActions()
      || this.paused
      || this.destroyed) return
    const holdThreshold = this.config.input.mylorik?.techniqueHoldMs ?? this.config.input.holdMs
    const continuationStep = this.config.input.mylorik?.continuationWindowMs ?? 480
    for (const physicalHand of ['left', 'right'] as const) {
      const state = this.keyboardDualSenseTriggers[physicalHand]
      if (!state.down) continue
      const controls = this.weapons.get(physicalClusterToRuntimeHand(physicalHand))?.controls
      // Anchor the walk at the activation threshold: when the first node sits
      // on a deeper gate (pre-gate weapons like the spear), the held key must
      // still climb through that node's gate instead of skipping past it.
      const activation = this.config.input.dualsense?.activationThreshold ?? 0.22
      const gates = [activation, ...[...new Set(controls?.dualsense.nodes
        .map(node => node.activationThreshold) ?? [])]
        .filter(gate => gate > activation)
        .sort((left, right) => left - right)]
      while (state.gateIndex + 1 < gates.length) {
        const nextIndex = state.gateIndex + 1
        const nextAt = holdThreshold + Math.max(0, nextIndex - 1) * continuationStep
        if (atMs - state.startedAt < nextAt) break
        state.gateIndex = nextIndex
        this.dualSenseControls.updateTrigger(
          physicalHand,
          gates[nextIndex],
          atMs,
          controls,
          'keyboard',
        )
      }
    }
  }

  private readonly handleKeyDown = (event: KeyboardEvent): void => {
    const activeControl = this.canUseRoomActions() && !this.paused
    const movementControl = this.canExploreRoom() && !this.routeMapVisible && !this.paused
    const semanticKeyboard = this.controlSchemeValue === 'legacy'
      ? null
      : this.controlSchemeValue === 'mylorik'
        ? this.config.input.mylorik?.keyboard
        : this.config.input.dualsense?.keyboard
    const isAttackKey = semanticKeyboard
      ? semanticKeyboard.leftTechniqueKeys.includes(event.code)
        || semanticKeyboard.rightTechniqueKeys.includes(event.code)
        || semanticKeyboard.mobilityKeys.includes(event.code)
      : this.config.input.leftKeys.includes(event.code)
        || this.config.input.rightKeys.includes(event.code)
    const isInteractionKey = semanticKeyboard
      ? semanticKeyboard.interactKeys.includes(event.code)
      : event.code === 'KeyE'
    if ((movementControl && MOVEMENT_KEYS.has(event.code))
      || (activeControl && (isAttackKey || isInteractionKey))) {
      event.preventDefault()
    }
    if (movementControl && MOVEMENT_KEYS.has(event.code)) this.pressedKeys.add(event.code)
    if (movementControl && isInteractionKey && !event.repeat) {
      if (this.interact()) return
    }
    if (!activeControl) return
    if (event.repeat) return
    if (activeControl && isInteractionKey) {
      if (this.interact()) return
    }
    if (!semanticKeyboard) {
      if (this.config.input.leftKeys.includes(event.code)) this.press('left')
      if (this.config.input.rightKeys.includes(event.code)) this.press('right')
      return
    }
    const now = performance.now()
    if (semanticKeyboard.leftTechniqueKeys.includes(event.code)) {
      if (this.controlSchemeValue === 'dualsense') {
        this.keyboardDualSenseTriggers.left = { down: true, startedAt: now, gateIndex: 0 }
        this.dualSenseControls.updateTrigger(
          'left',
          this.config.input.dualsense?.activationThreshold ?? 0.22,
          now,
          this.weapons.get(physicalClusterToRuntimeHand('left'))?.controls,
          'keyboard',
        )
      } else this.mylorikControls.pressTechnique('left', now, 'keyboard')
    }
    if (semanticKeyboard.rightTechniqueKeys.includes(event.code)) {
      if (this.controlSchemeValue === 'dualsense') {
        this.keyboardDualSenseTriggers.right = { down: true, startedAt: now, gateIndex: 0 }
        this.dualSenseControls.updateTrigger(
          'right',
          this.config.input.dualsense?.activationThreshold ?? 0.22,
          now,
          this.weapons.get(physicalClusterToRuntimeHand('right'))?.controls,
          'keyboard',
        )
      } else this.mylorikControls.pressTechnique('right', now, 'keyboard')
    }
    if (semanticKeyboard.mobilityKeys.includes(event.code)) {
      const hand = this.selectMobilityPhysicalHand()
      this.activeMobilityPhysicalHand = hand
      this.keyboardMobilityStartedAt = now
      this.keyboardMobilityCommitted = false
      if (hand) {
        if (this.controlSchemeValue === 'dualsense') {
          this.keyboardMobilityCommitted = this.handleSemanticInput({
            scheme: 'dualsense',
            physicalHand: hand,
            hand: physicalClusterToRuntimeHand(hand),
            intent: 'mobility',
            phase: 'press',
            source: 'keyboard',
            atMs: now,
            heldMs: 0,
            value: 1,
            commit: true,
          }) === 'handled'
        } else this.mylorikControls.pressMobility(hand, now, 'keyboard')
      }
    }
  }

  private readonly handleKeyUp = (event: KeyboardEvent): void => {
    this.pressedKeys.delete(event.code)
    if (!this.canUseRoomActions() || this.paused || this.destroyed) return
    if (this.controlSchemeValue === 'legacy') {
      if (this.config.input.leftKeys.includes(event.code)) this.release('left')
      if (this.config.input.rightKeys.includes(event.code)) this.release('right')
      return
    }
    const keyboard = this.controlSchemeValue === 'mylorik'
      ? this.config.input.mylorik?.keyboard
      : this.config.input.dualsense?.keyboard
    if (!keyboard) return
    const now = performance.now()
    if (keyboard.leftTechniqueKeys.includes(event.code)) {
      if (this.controlSchemeValue === 'dualsense') {
        this.keyboardDualSenseTriggers.left = { down: false, startedAt: 0, gateIndex: 0 }
        this.dualSenseControls.updateTrigger(
          'left',
          0,
          now,
          this.weapons.get(physicalClusterToRuntimeHand('left'))?.controls,
          'keyboard',
        )
      } else this.mylorikControls.releaseTechnique('left', now, 'keyboard')
    }
    if (keyboard.rightTechniqueKeys.includes(event.code)) {
      if (this.controlSchemeValue === 'dualsense') {
        this.keyboardDualSenseTriggers.right = { down: false, startedAt: 0, gateIndex: 0 }
        this.dualSenseControls.updateTrigger(
          'right',
          0,
          now,
          this.weapons.get(physicalClusterToRuntimeHand('right'))?.controls,
          'keyboard',
        )
      } else this.mylorikControls.releaseTechnique('right', now, 'keyboard')
    }
    if (keyboard.mobilityKeys.includes(event.code)) {
      if (this.activeMobilityPhysicalHand) {
        if (this.controlSchemeValue === 'dualsense') {
          if (!this.keyboardMobilityCommitted) {
            const heldMs = Math.max(0, now - this.keyboardMobilityStartedAt)
            this.handleSemanticInput({
              scheme: 'dualsense',
              physicalHand: this.activeMobilityPhysicalHand,
              hand: physicalClusterToRuntimeHand(this.activeMobilityPhysicalHand),
              intent: 'mobility',
              phase: heldMs >= (this.config.input.mylorik?.techniqueHoldMs ?? 0)
                ? 'hold'
                : 'release',
              source: 'keyboard',
              atMs: now,
              heldMs,
              value: 0,
              commit: true,
            })
          }
        } else {
          this.mylorikControls.releaseMobility(this.activeMobilityPhysicalHand, now, 'keyboard')
        }
      }
      this.activeMobilityPhysicalHand = null
      this.keyboardMobilityStartedAt = 0
      this.keyboardMobilityCommitted = false
    }
  }

  private cleanupControlInputs(settleHeld: boolean): void {
    if (settleHeld) {
      this.commitHeldChannels()
      this.commitOrCancelProvisionalParry()
    }
    this.pressedKeys.clear()
    this.touchMove = { x: 0, y: 0 }
    this.gamepadMove = { x: 0, y: 0 }
    this.gamepadAim = { x: 0, y: 0 }
    this.retainedGamepadAim = null
    this.gamepadButtons.left = false
    this.gamepadButtons.right = false
    for (const key of Object.keys(this.gamepadControlButtons) as Array<keyof typeof this.gamepadControlButtons>) {
      this.gamepadControlButtons[key] = false
    }
    this.gamepadTriggerValues.left = 0
    this.gamepadTriggerValues.right = 0
    this.feedbackChargeBandIds.left = null
    this.feedbackChargeBandIds.right = null
    this.resetContinuationFeedbackTracking()
    this.gamepadMenuAxisEngaged = false
    this.gamepadChoiceAxisEngaged = false
    this.gamepadNeedsReseed = true
    this.activeMobilityPhysicalHand = null
    this.keyboardMobilityStartedAt = 0
    this.keyboardMobilityCommitted = false
    this.keyboardDualSenseTriggers.left = { down: false, startedAt: 0, gateIndex: 0 }
    this.keyboardDualSenseTriggers.right = { down: false, startedAt: 0, gateIndex: 0 }
    this.gestures.reset()
    this.mylorikControls.reset()
    this.dualSenseControls.reset()
    this.triggerDetents.left = null
    this.triggerDetents.right = null
    this.spiderWriggle = null
    void this.feedbackController.neutralize()
  }

  private cleanupControlInputsForReplacement(): void {
    this.cancelHeldChannels()
    this.commitOrCancelProvisionalParry()
    this.cleanupControlInputs(false)
  }

  private readonly handleBlur = (): void => {
    this.cleanupControlInputs(true)
  }

  private readonly handlePointerMove = (event: PointerEvent): void => {
    this.pointerMove(event.clientX, event.clientY)
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (event.pointerType === 'touch') return
    this.canvas.setPointerCapture?.(event.pointerId)
    if (event.button === 2 && !this.weapons.has('right') && this.startEmptyRightHandDash()) return
    if (this.controlSchemeValue === 'legacy') {
      if (event.button === 0) this.press('left')
      if (event.button === 2) this.press('right')
      return
    }
    const keyboard = this.controlSchemeValue === 'mylorik'
      ? this.config.input.mylorik?.keyboard
      : this.config.input.dualsense?.keyboard
    const physicalHand = event.button === keyboard?.leftStrikeMouseButton
      ? 'left'
      : event.button === keyboard?.rightStrikeMouseButton ? 'right' : null
    if (!physicalHand) return
    if (this.controlSchemeValue === 'mylorik') {
      this.mylorikControls.pressStrike(physicalHand, performance.now(), 'pointer')
    } else {
      this.dualSenseControls.pressBumper(physicalHand, performance.now(), 'pointer')
    }
  }

  private readonly handlePointerUp = (event: PointerEvent): void => {
    if (event.pointerType === 'touch') return
    if (this.controlSchemeValue !== 'legacy') return
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
    const sidePadding = clamp(this.cssWidth * 0.018, 12, 24)
    const top = clamp(this.cssHeight * 0.12, 64, 84)
    const bottomPadding = clamp(this.cssHeight * 0.035, 16, 28)
    const diamondWidth = Math.max(1, this.cssWidth - sidePadding * 2)
    const availableHeight = Math.max(1, this.cssHeight - top - bottomPadding)
    const diamondHeight = Math.max(1, Math.min(availableHeight, diamondWidth / 1.55))
    return {
      centerX: this.cssWidth / 2,
      top,
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

  private controlRoleSnapshots(): LastChancesControlRoleSnapshot[] {
    if (this.controlSchemeValue === 'legacy') return []
    return LAST_CHANCES_HANDS.flatMap((hand) => {
      const weapon = this.weapons.get(hand)
      const controls = weapon?.controls
      if (!weapon || !controls) return []
      if (this.controlSchemeValue === 'mylorik') {
        const instant = controls.mylorik.activations
          .filter(activation => activation.intent === 'strike' && activation.phase === 'press')
          .sort((left, right) => right.priority - left.priority)[0]
        const contextual = controls.mylorik.activations
          .filter(activation => activation.context !== undefined)
          .filter(activation => this.continuationFeedbackSourceKeys(
            hand,
            activation.context!,
          ).length > 0)
          .filter(activation => this.gestureReady(hand, activation.gesture))
          .sort((left, right) => right.priority - left.priority)[0]
        return [{
          hand,
          instantMove: instant ? weapon.attacks[instant.gesture].name : controls.role,
          techniqueOrTrigger: controls.role,
          nextGate: contextual ? weapon.attacks[contextual.gesture].name : null,
        }]
      }
      const physicalHand = runtimeHandToPhysicalCluster(hand)
      const trigger = this.dualSenseControls.snapshot(physicalHand, performance.now())
      const activeNode = controls.dualsense.nodes.find(node => node.id === trigger.nodeId)
      const firstNode = controls.dualsense.nodes.find(node => (
        node.id === controls.dualsense.startNodeId
      )) ?? controls.dualsense.nodes[0]
      const nextNodes = activeNode?.next
        .map(id => controls.dualsense.nodes.find(node => node.id === id))
        .filter((node): node is NonNullable<typeof node> => node !== undefined)
        .sort((left, right) => left.activationThreshold - right.activationThreshold) ?? []
      const contextReady = (node: typeof nextNodes[number]): boolean => {
        const requiredBand = node.requiredChargeBandId
          ? weapon.attacks.hold.charge?.bands.find(band => band.id === node.requiredChargeBandId)
          : undefined
        const bandReady = node.requiredChargeBandId === undefined
          || (requiredBand !== undefined && trigger.heldMs >= requiredBand.minMs)
        if (!bandReady) return false
        return node.entryContext === 'continuation'
          || this.controlContextActive(hand, node.entryContext)
      }
      const contextualNode = controls.dualsense.nodes
        .filter(node => node.entryContext !== 'neutral')
        .filter(node => this.continuationFeedbackSourceKeys(hand, node.entryContext).length > 0)
        .filter(node => this.continuationFeedbackNodeReady(hand, node))
        .sort((left, right) => left.activationThreshold - right.activationThreshold)[0]
      const nextNode = activeNode
        ? nextNodes.find(contextReady) ?? nextNodes[0]
        : contextualNode ?? firstNode
      return [{
        hand,
        instantMove: weapon.attacks[controls.dualsense.instantGesture].name,
        techniqueOrTrigger: controls.dualsense.triggerRole || controls.role,
        nextGate: nextNode ? weapon.attacks[nextNode.gesture].name : null,
      }]
    })
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
      { status: 'unstoppable', remainingMs: enemy.statuses.unstoppableMs, magnitude: 1 },
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
        visible: enemy.motherRetreat?.stage === 'hidden' ? false : this.enemyVisible(enemy),
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
    const gestureInputs = LAST_CHANCES_HANDS.map(hand => this.controlInputSnapshot(hand, now))
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
        perfectTimingMs: state.perfectTimingMs,
        fatigueMs: state.fatigueMs,
        unterhauWindowMs: Math.max(0, state.unterhauDueAtMs - this.elapsedMs),
        unterhauPrimed: state.unterhauPrimed,
        motionDamageBonus: state.lastMotionDamageBonus,
      }]
    })
    const moveQuests: LastChancesMoveQuestSnapshot[] = this.config.progression.moveQuestsEnabled === false
      ? []
      : LAST_CHANCES_HANDS.map((hand) => {
      const quest = this.moveQuests[hand]
      const required = this.comboQuestGestures(hand)
      let bestComboHits: LastChancesGesture[] = []
      for (const enemy of this.enemies) {
        if (enemy.state === 'dead' || enemy.definition.role !== 'elite') continue
        const hits = required.filter(gesture => enemy.gestureHits[hand].has(gesture))
        if (hits.length > bestComboHits.length) bestComboHits = hits
      }
      return {
        hand,
        unlocked: { ...quest.unlocked },
        pendingUnlocks: [...quest.pendingUnlocks],
        roomKills: { ...quest.roomKills },
        killsRequired: MOVE_QUEST_KILLS_REQUIRED,
        tapQuestDone: quest.tapQuestDone,
        holdQuestDone: quest.holdQuestDone,
        comboQuestAvailable: this.moveQuestPrerequisitesDone(hand) && !quest.comboQuestDone,
        comboQuestDone: quest.comboQuestDone,
        comboGesturesHit: bestComboHits,
        comboGesturesRequired: required,
      }
      })
    const effectiveStats = this.effectivePlayerStats()
    const groundWeapon = this.nearestGroundWeapon()
    const rewardChest = this.nearbyRewardChest()
    const nearbyTurret = this.nearestActiveTurret()
    const groundWeaponName = groundWeapon
      ? this.config.weapons.find(weapon => weapon.id === groundWeapon.weaponId)?.name
      : null
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
        stats: copyStats(effectiveStats),
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
      moveQuests,
      groundWeapons: this.groundWeapons.map(weapon => ({
        id: weapon.id,
        weaponId: weapon.weaponId,
        name: this.config.weapons.find(definition => definition.id === weapon.weaponId)?.name
          ?? weapon.weaponId,
        position: { ...weapon.position },
      })),
      swarm: this.swarmSpawner
        ? {
            definitionId: this.swarmSpawner.definition.id,
            remaining: this.swarmSpawner.remaining,
            total: this.swarmSpawner.total,
            infinite: this.swarmSpawner.infinite,
          }
        : null,
      turrets: this.turrets.map(turret => ({
        id: turret.definition.id,
        name: turret.definition.name,
        position: { ...turret.definition.position },
        facing: { ...turret.facing },
        disabled: turret.disabled,
        seesPlayer: turret.seesPlayer,
      })),
      turretAlarm: this.turretAlarmMs > 0,
      altarPrompt: this.altarPromptActive && this.currentNode?.altar
        ? {
            prompt: this.currentNode.altar.prompt,
            chanceCost: this.currentNode.altar.chanceCost,
            available: this.chances >= this.currentNode.altar.chanceCost,
          }
        : null,
      cockroachesExtinct: this.cockroachesExtinct,
      interactionPrompt: nearbyTurret && this.turretAlarmMs <= 0
        ? `${this.controlSchemeValue === 'dualsense' ? 'E / Cross' : 'E'}: отключить ${nearbyTurret.definition.name}`
        : groundWeapon && groundWeaponName
        ? `${this.controlSchemeValue === 'dualsense' ? 'E / Cross' : 'E'}: подобрать ${groundWeaponName}`
        : rewardChest
        ? `${this.controlSchemeValue === 'dualsense' ? 'E / Cross' : 'E'}: открыть сундук с наградой`
        : this.capturableKnifeSpider()
        ? this.controlSchemeValue === 'legacy'
          ? 'E / обе кнопки: схватить Нож-паука со спины'
          : this.controlSchemeValue === 'mylorik'
            ? 'E / Cross: схватить Нож-паука со спины'
            : 'E / Cross: схватить Нож-паука со спины'
        : null,
      controlScheme: this.controlSchemeValue,
      controlCue: this.controlCue ? { ...this.controlCue } : null,
      controlRoles: this.controlRoleSnapshots(),
      feedback: this.feedbackController.snapshot(),
      gamepad: { ...this.gamepadState },
      selectedNodeId: this.selectedNodeId,
      selectedInteractionChoiceId: this.selectedInteractionChoiceId,
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
    this.renderSwordRhythmOverlay()
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

  private renderSwordRhythmOverlay(): void {
    if (!this.canUseRoomActions() || this.paused || !this.currentNode) return
    const weapon = [...this.weapons.values()].find(candidate => candidate.trait === 'swordRhythm')
    if (!weapon) return
    const state = this.weaponState(weapon)
    if (!Number.isFinite(state.lastTapAtMs)) return
    const interval = Math.max(0, this.elapsedMs - state.lastTapAtMs)
    const perfectStartMs = Math.max(1, tuningValue(weapon, 'rhythmPerfectStartMs', 500))
    const perfectEndMs = Math.max(
      perfectStartMs,
      tuningValue(weapon, 'rhythmPerfectEndMs', 600),
    )
    const visibleMs = Math.max(
      perfectEndMs,
      tuningValue(weapon, 'rhythmOverlayVisibleMs', 1050),
    )
    const activeSwordArea = this.activeAreas.find(area => (
      area.weaponId === weapon.id && area.attack.behavior === 'swordRhythm'
    ))
    if (interval > visibleMs && !activeSwordArea && state.perfectTimingMs <= 0) return

    const context = this.context
    const playerPoint = this.worldToScreen(this.player.position, this.currentNode)
    const playerRadius = Math.max(
      8,
      this.config.player.radius * this.entityScale(this.currentNode) * 1.55,
    )
    const centerX = playerPoint.x
    const centerY = playerPoint.y - playerRadius
    const circleRadius = clamp(this.cssWidth * 0.022, 18, 28) * 0.9
    const halfSpan = clamp(this.cssWidth * 0.23, 110, 240) * 0.9
    const progress = clamp(interval / perfectStartMs, 0, 1)
    const inPerfectZone = interval >= perfectStartMs && interval <= perfectEndMs
    const feedbackTotal = Math.max(1, tuningValue(weapon, 'rhythmPerfectFeedbackMs', 480))
    const feedbackProgress = clamp(state.perfectTimingMs / feedbackTotal, 0, 1)
    const bounce = feedbackProgress > 0
      ? 1 + Math.sin((1 - feedbackProgress) * Math.PI) * 0.24
      : 1
    const opacity = interval <= visibleMs
      ? clamp(1 - Math.max(0, interval - perfectEndMs) / Math.max(1, visibleMs - perfectEndMs), 0.18, 1)
      : feedbackProgress
    const movingLeft = centerX - halfSpan + (halfSpan - circleRadius) * progress
    const movingRight = centerX + halfSpan - (halfSpan - circleRadius) * progress

    context.save()
    context.globalAlpha = opacity * 0.78 * 0.8
    context.lineCap = 'round'
    context.strokeStyle = 'rgba(228, 235, 239, .24)'
    context.lineWidth = 4
    context.beginPath()
    context.moveTo(centerX - halfSpan, centerY)
    context.lineTo(centerX - circleRadius, centerY)
    context.moveTo(centerX + circleRadius, centerY)
    context.lineTo(centerX + halfSpan, centerY)
    context.stroke()

    const lineColor = inPerfectZone || feedbackProgress > 0 ? '#f6cf68' : '#b9d8df'
    context.strokeStyle = lineColor
    context.shadowColor = lineColor
    context.shadowBlur = 10 + feedbackProgress * 16
    context.lineWidth = 5
    const streakLength = 34
    context.beginPath()
    context.moveTo(Math.max(centerX - halfSpan, movingLeft - streakLength), centerY)
    context.lineTo(movingLeft, centerY)
    context.moveTo(Math.min(centerX + halfSpan, movingRight + streakLength), centerY)
    context.lineTo(movingRight, centerY)
    context.stroke()

    context.translate(centerX, centerY)
    context.scale(bounce, bounce)
    context.beginPath()
    context.arc(0, 0, circleRadius, 0, Math.PI * 2)
    context.fillStyle = 'rgba(10, 12, 14, .58)'
    context.fill()
    context.strokeStyle = lineColor
    context.lineWidth = feedbackProgress > 0 ? 5 : 3
    context.stroke()
    if (feedbackProgress > 0) {
      context.beginPath()
      context.arc(0, 0, circleRadius * (1.25 + (1 - feedbackProgress) * 0.45), 0, Math.PI * 2)
      context.globalAlpha = opacity * feedbackProgress * 0.6
      context.strokeStyle = '#ffe8a3'
      context.lineWidth = 3
      context.stroke()
    }
    context.restore()

  }

  private renderArena(node: LastChancesPlanNode): void {
    this.renderFloor(node)
    for (const hole of node.bossHoles) this.renderBossHole(hole, node)
    for (const hazard of node.arena.hazards) this.renderHazard(hazard, node)
    for (const zone of this.zoneAttacks) this.renderZoneAttack(zone, node)
    for (const strike of this.holeStrikes) this.renderHoleStrike(strike, node)
    for (const turret of this.turrets) this.renderTurretVision(turret, node)
    for (const enemy of this.enemies) this.renderVision(enemy, node)
    this.renderSpearChargePreview(node)
    const items: Array<{ depth: number, draw: () => void }> = []
    for (const obstacle of node.arena.obstacles) {
      items.push({
        depth: obstacle.x + obstacle.width + obstacle.y + obstacle.height,
        draw: () => this.renderObstacle(obstacle, node),
      })
    }
    for (const enemy of this.enemies) {
      if (enemy.state !== 'dead' && enemy.motherRetreat?.stage !== 'hidden') {
        items.push({ depth: enemy.position.x + enemy.position.y, draw: () => this.renderEnemy(enemy, node) })
      }
    }
    for (const turret of this.turrets) {
      items.push({
        depth: turret.definition.position.x + turret.definition.position.y,
        draw: () => this.renderTurret(turret, node),
      })
    }
    if (node.altar) {
      items.push({
        depth: node.altar.position.x + node.altar.position.y,
        draw: () => this.renderBossAltar(node.altar as LastChancesBossAltarDefinition, node),
      })
    }
    for (const projectile of this.projectiles) {
      items.push({
        depth: projectile.position.x + projectile.position.y,
        draw: () => this.renderProjectile(projectile, node),
      })
    }
    for (const weapon of this.groundWeapons) {
      items.push({
        depth: weapon.position.x + weapon.position.y,
        draw: () => this.renderGroundWeapon(weapon, node),
      })
    }
    if (this.rewardChest) {
      items.push({
        depth: this.rewardChest.position.x + this.rewardChest.position.y,
        draw: () => this.renderRewardChest(this.rewardChest as RuntimeRewardChest, node),
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

  private renderBossHole(hole: LastChancesBossHoleDefinition, node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(hole.position, node)
    const radius = Math.max(10, 34 * this.entityScale(node))
    context.save()
    context.translate(point.x, point.y)
    context.scale(1, 0.48)
    context.beginPath()
    if (hole.shape === 'circle') context.arc(0, 0, radius, 0, Math.PI * 2)
    else if (hole.shape === 'square') context.rect(-radius, -radius, radius * 2, radius * 2)
    else if (hole.shape === 'triangle') {
      context.moveTo(0, -radius * 1.25)
      context.lineTo(radius * 1.15, radius)
      context.lineTo(-radius * 1.15, radius)
      context.closePath()
    } else {
      context.moveTo(0, -radius * 1.3)
      context.lineTo(radius * 1.15, 0)
      context.lineTo(0, radius * 1.3)
      context.lineTo(-radius * 1.15, 0)
      context.closePath()
    }
    context.fillStyle = '#050405'
    context.shadowColor = hole.color
    context.shadowBlur = 10
    context.fill()
    context.strokeStyle = hole.color
    context.lineWidth = 2.5
    context.stroke()
    context.restore()
  }

  private renderHoleStrike(strike: RuntimeHoleStrike, node: LastChancesPlanNode): void {
    const context = this.context
    const duration = Math.max(1, strike.detonateAtMs - strike.spawnedAtMs)
    const urgency = clamp(1 - (strike.detonateAtMs - this.elapsedMs) / duration, 0, 1)
    const points = Array.from({ length: 40 }, (_, index) => {
      const angle = index / 40 * Math.PI * 2
      return this.worldToScreen({
        x: strike.center.x + Math.cos(angle) * strike.radius,
        y: strike.center.y + Math.sin(angle) * strike.radius,
      }, node)
    })
    context.save()
    context.beginPath()
    points.forEach((point, index) => index === 0
      ? context.moveTo(point.x, point.y)
      : context.lineTo(point.x, point.y))
    context.closePath()
    context.fillStyle = `rgba(209, 37, 28, ${0.08 + urgency * 0.22})`
    context.fill()
    context.strokeStyle = urgency > 0.72 ? '#fff0d5' : '#ef493d'
    context.lineWidth = 2 + urgency * 5
    context.setLineDash([12, 8])
    context.stroke()
    context.setLineDash([])
    const point = this.worldToScreen(strike.center, node)
    context.font = '900 11px system-ui'
    context.textAlign = 'center'
    context.fillStyle = '#ffb4a7'
    context.fillText(`${Math.max(0, (strike.detonateAtMs - this.elapsedMs) / 1000).toFixed(1)} сек`, point.x, point.y - 28)
    context.restore()
  }

  private renderTurretVision(turret: RuntimeTurret, node: LastChancesPlanNode): void {
    if (turret.disabled) return
    const context = this.context
    const origin = this.worldToScreen(turret.definition.position, node)
    const facingAngle = Math.atan2(turret.facing.y, turret.facing.x)
    const halfArc = turret.definition.visionAngleDegrees * Math.PI / 360
    context.beginPath()
    context.moveTo(origin.x, origin.y)
    for (let step = 0; step <= 18; step += 1) {
      const angle = facingAngle - halfArc + halfArc * 2 * step / 18
      const point = this.worldToScreen({
        x: turret.definition.position.x + Math.cos(angle) * turret.definition.visionRange,
        y: turret.definition.position.y + Math.sin(angle) * turret.definition.visionRange,
      }, node)
      context.lineTo(point.x, point.y)
    }
    context.closePath()
    context.fillStyle = this.turretAlarmMs > 0
      ? 'rgba(255, 36, 42, .2)'
      : 'rgba(244, 196, 91, .07)'
    context.fill()
  }

  private renderTurret(turret: RuntimeTurret, node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(turret.definition.position, node)
    const aim = this.worldToScreen({
      x: turret.definition.position.x + turret.facing.x * 48,
      y: turret.definition.position.y + turret.facing.y * 48,
    }, node)
    const scale = Math.max(8, 20 * this.entityScale(node))
    context.save()
    context.beginPath()
    context.arc(point.x, point.y - scale * 0.65, scale, 0, Math.PI * 2)
    context.fillStyle = turret.disabled ? '#343638' : '#555d61'
    context.strokeStyle = turret.disabled ? '#72787a' : turret.definition.color
    context.lineWidth = 2.5
    context.fill()
    context.stroke()
    context.beginPath()
    context.moveTo(point.x, point.y - scale * 0.65)
    context.lineTo(aim.x, aim.y - scale * 0.65)
    context.strokeStyle = turret.disabled ? '#606466' : turret.definition.color
    context.lineWidth = scale * 0.48
    context.stroke()
    if (turret.disabled) {
      context.font = `900 ${scale * 1.45}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = '#a8b0b1'
      context.fillText('×', point.x, point.y - scale * 0.2)
    } else if (this.nearestActiveTurret()?.definition.id === turret.definition.id
      && this.turretAlarmMs <= 0) {
      context.font = '800 9px system-ui'
      context.textAlign = 'center'
      context.fillStyle = '#a8f1ce'
      context.fillText('E · ОТКЛЮЧИТЬ', point.x, point.y - scale * 2.35)
    }
    context.restore()
  }

  private renderBossAltar(altar: LastChancesBossAltarDefinition, node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(altar.position, node)
    const scale = Math.max(9, 26 * this.entityScale(node))
    context.save()
    context.translate(point.x, point.y - scale)
    context.shadowColor = '#d89b66'
    context.shadowBlur = 14
    context.fillStyle = '#34241f'
    context.strokeStyle = '#d89b66'
    context.lineWidth = 2
    context.beginPath()
    context.moveTo(-scale * 0.9, scale)
    context.lineTo(-scale * 0.55, -scale * 0.65)
    context.lineTo(scale * 0.55, -scale * 0.65)
    context.lineTo(scale * 0.9, scale)
    context.closePath()
    context.fill()
    context.stroke()
    context.beginPath()
    context.arc(0, -scale * 0.8, scale * 0.28, 0, Math.PI * 2)
    context.fillStyle = '#f2c278'
    context.fill()
    context.restore()
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

  private renderZoneAttack(zone: RuntimeZoneAttack, node: LastChancesPlanNode): void {
    const context = this.context
    const totalMs = Math.max(1, zone.detonateAtMs - zone.spawnedAtMs)
    const urgency = clamp(1 - (zone.detonateAtMs - this.elapsedMs) / totalMs, 0, 1)
    const worldPoints: LastChancesVector[] = zone.shape === 'circle'
      ? Array.from({ length: 28 }, (_, index) => {
          const angle = (index / 28) * Math.PI * 2
          return {
            x: zone.center.x + Math.cos(angle) * zone.size,
            y: zone.center.y + Math.sin(angle) * zone.size,
          }
        })
      : zoneShapeLocalVertices(zone).map((vertex) => {
          const rotated = rotateVector(vertex, zone.rotationRadians)
          return { x: zone.center.x + rotated.x, y: zone.center.y + rotated.y }
        })
    context.save()
    context.beginPath()
    worldPoints.forEach((worldPoint, index) => {
      const point = this.worldToScreen(worldPoint, node)
      if (index === 0) context.moveTo(point.x, point.y)
      else context.lineTo(point.x, point.y)
    })
    context.closePath()
    const pulse = urgency > 0.6 ? 0.12 * Math.sin(this.elapsedMs / 45) : 0
    context.globalAlpha = clamp(0.18 + 0.42 * urgency + pulse, 0, 1)
    context.fillStyle = '#ff4a3c'
    context.fill()
    context.globalAlpha = clamp(0.55 + 0.45 * urgency, 0, 1)
    context.strokeStyle = urgency > 0.6 ? '#ffd9c8' : '#ff4a3c'
    context.lineWidth = 2 + urgency * 2
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

  private renderRewardChest(chest: RuntimeRewardChest, node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(chest.position, node)
    const scale = Math.max(8, 24 * this.entityScale(node))
    const glow = 0.55 + Math.sin(this.elapsedMs / 180) * 0.15
    context.save()
    context.translate(point.x, point.y - scale * 0.55)
    context.shadowColor = '#f0c96d'
    context.shadowBlur = chest.opened ? 8 : 14 + glow * 8
    context.fillStyle = chest.opened ? '#65513a' : '#8b6838'
    context.strokeStyle = chest.opened ? '#ad956d' : '#f0c96d'
    context.lineWidth = 2
    context.beginPath()
    context.roundRect(-scale * 1.2, -scale * 0.45, scale * 2.4, scale * 1.2, scale * 0.18)
    context.fill()
    context.stroke()
    context.beginPath()
    context.arc(0, -scale * 0.35, scale * 1.05, Math.PI, 0)
    context.fillStyle = chest.opened ? '#4f4234' : '#a57d42'
    context.fill()
    context.stroke()
    context.fillStyle = '#e9d28c'
    context.fillRect(-scale * 0.12, scale * 0.05, scale * 0.24, scale * 0.38)
    context.restore()
  }

  private renderVision(enemy: RuntimeEnemy, node: LastChancesPlanNode): void {
    if (!this.enemyVisible(enemy)
      || enemy.state === 'dead'
      || enemy.motherRetreat?.stage === 'hidden'
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
    const profile = this.enemyCombatProfile(enemy)
    const radius = isCockroachDefinition(enemy.definition)
      ? Math.max(2.33, enemy.definition.radius * this.entityScale(node) * 1.45 / 3)
      : profile.role === 'creep'
        ? Math.max(8, enemy.definition.radius * this.entityScale(node) * 1.62)
      : Math.max(7, enemy.definition.radius * this.entityScale(node) * 1.45)
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
    if (enemy.statuses.unstoppableMs > 0) {
      const pulse = 0.5 + Math.sin(this.elapsedMs / 110 + enemy.position.x) * 0.5
      context.beginPath()
      context.ellipse(
        point.x,
        point.y - radius * 0.8,
        radius * (1.45 + pulse * 0.08),
        radius * (1.75 + pulse * 0.1),
        0,
        0,
        Math.PI * 2,
      )
      context.fillStyle = 'rgba(104, 196, 255, .11)'
      context.fill()
      context.strokeStyle = `rgba(151, 220, 255, ${0.58 + pulse * 0.32})`
      context.lineWidth = 2.5 + pulse * 1.5
      context.shadowColor = '#78cfff'
      context.shadowBlur = 12 + pulse * 10
      context.stroke()
      context.shadowBlur = 0
      context.font = `900 ${Math.max(8, radius * 0.58)}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = '#bceaff'
      context.fillText('НЕУДЕРЖИМОСТЬ', point.x, point.y - radius * 3.55)
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
      [enemy.statuses.unstoppableMs > 0, '#9edfff', 'U'],
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
    if (enemy.definition.cockroachMother) {
      for (const side of [-1, 1]) {
        for (let leg = 0; leg < 3; leg += 1) {
          const y = point.y - radius * (1.35 - leg * 0.48)
          context.moveTo(point.x + side * radius * 0.55, y)
          context.lineTo(point.x + side * radius * 1.55, y + (leg - 1) * radius * 0.42)
        }
      }
      context.strokeStyle = enemy.definition.color
      context.lineWidth = Math.max(2, radius * 0.13)
      context.stroke()
      context.beginPath()
      context.ellipse(point.x, point.y - radius * 0.85, radius * 0.72, radius * 1.35, 0, 0, Math.PI * 2)
      context.moveTo(point.x - radius * 0.62, point.y - radius * 1.25)
      context.ellipse(point.x - radius * 0.48, point.y - radius * 1.1, radius * 0.65, radius * 1.05, -0.45, 0, Math.PI * 2)
      context.moveTo(point.x + radius * 0.62, point.y - radius * 1.25)
      context.ellipse(point.x + radius * 0.48, point.y - radius * 1.1, radius * 0.65, radius * 1.05, 0.45, 0, Math.PI * 2)
    } else if (enemy.definition.id === 'spider-knife') {
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
    if (!this.primarySpearWeapon()) {
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
    const fatigueMs = Math.max(
      0,
      ...[...this.weapons.values()]
        .filter(weapon => weapon.trait === 'swordRhythm')
        .map(weapon => this.weaponState(weapon).fatigueMs),
    )
    if (fatigueMs > 0) {
      const pulse = 0.5 + Math.sin(this.elapsedMs / 85) * 0.5
      context.globalAlpha = 0.72 + pulse * 0.2
      context.strokeStyle = '#d77b75'
      context.lineWidth = 2
      for (const side of [-1, 1]) {
        context.beginPath()
        context.arc(
          point.x + side * radius * (1.25 + pulse * 0.2),
          point.y - radius * (1.4 + pulse * 0.15),
          radius * (0.35 + pulse * 0.15),
          Math.PI * 0.1,
          Math.PI * 0.9,
        )
        context.stroke()
      }
      context.font = `900 ${Math.max(9, radius * 0.62)}px system-ui`
      context.textAlign = 'center'
      context.fillStyle = '#efaaa2'
      context.fillText('УСТАЛОСТЬ', point.x, point.y - radius * 3)
    }
    context.restore()
    this.renderHeldSpear(node, point, radius)
  }

  private renderGroundWeapon(weapon: RuntimeGroundWeapon, node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(weapon.position, node)
    const nearby = this.nearestGroundWeapon()?.id === weapon.id
    context.save()
    context.translate(point.x, point.y - 5)
    context.rotate(-0.38)
    context.shadowColor = nearby ? '#f0cf79' : '#8da1ad'
    context.shadowBlur = nearby ? 18 : 8
    context.strokeStyle = nearby ? '#f4d98d' : '#b9c4c8'
    context.lineWidth = nearby ? 4 : 3
    context.beginPath()
    context.moveTo(-12, 5)
    context.lineTo(13, -6)
    context.stroke()
    context.fillStyle = '#66533b'
    context.fillRect(-15, 2, 7, 5)
    context.restore()
  }

  private renderProjectile(projectile: RuntimeProjectile, node: LastChancesPlanNode): void {
    const point = this.worldToScreen(projectile.position, node)
    const radius = Math.max(3, projectile.radius * this.entityScale(node) * 1.6)
    if (projectile.source === 'player'
      && projectile.weaponId === 'twohand-spear'
      && projectile.attack?.behavior === 'spearRelease') {
      const direction = normalize(projectile.velocity)
      const layout = this.spearSpriteLayout(
        node,
        { x: point.x, y: point.y - radius * 0.7 },
        direction,
        Math.max(8, this.config.player.radius * this.entityScale(node) * 1.55),
      )
      const context = this.context
      context.save()
      context.globalAlpha = 0.5
      context.strokeStyle = '#ff3347'
      context.shadowColor = '#ff3347'
      context.shadowBlur = 12
      context.lineWidth = Math.max(2, radius * 0.35)
      context.beginPath()
      context.moveTo(
        layout.center.x - layout.axis.x * layout.width * 0.58,
        layout.center.y - layout.axis.y * layout.width * 0.58,
      )
      context.lineTo(
        layout.center.x - layout.axis.x * layout.width * 0.2,
        layout.center.y - layout.axis.y * layout.width * 0.2,
      )
      context.stroke()
      context.restore()
      this.drawSpearSprite(layout)
      return
    }
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

  private primarySpearWeapon(): LastChancesResolvedWeapon | null {
    const weapon = this.weapons.get('left')
    return weapon?.trait === 'spearDistance'
      && weapon.attacks.hold.behavior === 'spearRelease'
      ? weapon
      : null
  }

  private spearChargePresentation(atMs: number): {
    weapon: LastChancesResolvedWeapon
    heldMs: number
    visual: NonNullable<ReturnType<typeof resolveLastChancesSpearChargeVisual>>
  } | null {
    const weapon = this.primarySpearWeapon()
    if (!weapon || !this.gestureReady('left', 'hold')) return null
    const input = this.controlInputSnapshot('left', atMs)
    const isDeeperBranch = input.candidateGesture === 'doubleTapHold'
      || input.candidateGesture === 'holdThenDoubleTap'
    const holdingFirstPress = input.pressed && input.sequence === 'first' && !isDeeperBranch
    const awaitingRelease = input.phase === 'holdFollowUpWindow'
    if (!holdingFirstPress && !awaitingRelease) return null
    const heldMs = awaitingRelease ? input.pendingChargeMs : input.heldMs
    const visual = resolveLastChancesSpearChargeVisual(weapon.attacks.hold, heldMs)
    return visual ? { weapon, heldMs, visual } : null
  }

  private renderSpearChargePreview(node: LastChancesPlanNode): void {
    if (this.paused || !this.canUseRoomActions()) return
    const presentation = this.spearChargePresentation(this.frameNowMs || performance.now())
    if (!presentation) return
    const sourceAttack = attackWithLastChancesAugment(
      presentation.weapon.attacks.hold,
      presentation.weapon,
    )
    const previewHeldMs = Math.max(
      presentation.heldMs,
      presentation.visual.previewBand.minMs,
    )
    const charged = resolveLastChancesChargedAttack(sourceAttack, previewHeldMs)
    if (!charged.band) return
    const direction = this.spearReleaseDirection(
      charged.attack,
      this.player.aim,
      charged.band.id,
    )
    let collider: LastChancesRuntimeCollider
    if (charged.band.id === 'early') {
      collider = resolveAttackCollider(
        this.player.position,
        direction,
        {
          ...charged.attack,
          kind: 'melee',
          collider: {
            ...(charged.attack.collider ?? { traceMs: 900 }),
            shape: 'sector',
            innerRange: 52,
          },
        },
      )
    } else {
      const projectileRadius = Math.max(
        charged.attack.radius,
        (charged.attack.collider?.width ?? 0) / 2,
      )
      const spawnOffset = Math.max(0, tuningValue(charged.attack, 'projectileSpawnOffset', 0))
      const startDistance = this.config.player.radius + projectileRadius + 2 + spawnOffset
      const start = {
        x: this.player.position.x + direction.x * startDistance,
        y: this.player.position.y + direction.y * startDistance,
      }
      const travel = this.spearPreviewTravelDistance(
        node,
        start,
        direction,
        Math.max(0, charged.attack.range - spawnOffset),
        projectileRadius,
      )
      collider = {
        shape: 'capsule',
        start,
        end: {
          x: start.x + direction.x * travel,
          y: start.y + direction.y * travel,
        },
        radius: projectileRadius,
      }
    }

    const context = this.context
    const alpha = presentation.visual.armed ? 0.82 : 0.36
    const pulse = 0.78 + Math.sin(this.elapsedMs / 115) * 0.12
    context.save()
    context.setLineDash([9, 7])
    context.lineCap = 'round'
    context.lineJoin = 'round'
    context.globalAlpha = alpha * pulse
    context.strokeStyle = presentation.visual.previewBand.color
    context.fillStyle = presentation.visual.previewBand.color
    context.shadowColor = presentation.visual.previewBand.color
    context.shadowBlur = presentation.visual.armed ? 12 : 5
    context.lineWidth = presentation.visual.armed ? 2.6 : 1.7
    for (const path of colliderTracePath(collider)) {
      if (path.points.length === 0) continue
      context.beginPath()
      path.points.forEach((world, index) => {
        const point = this.worldToScreen(world, node)
        if (index === 0) context.moveTo(point.x, point.y)
        else context.lineTo(point.x, point.y)
      })
      if (path.closed) context.closePath()
      context.stroke()
      if (path.closed) {
        context.globalAlpha = alpha * 0.045
        context.fill()
        context.globalAlpha = alpha * pulse
      }
    }
    if (collider.shape === 'capsule') {
      const start = this.worldToScreen(collider.start, node)
      const end = this.worldToScreen(collider.end, node)
      const axis = normalize({ x: end.x - start.x, y: end.y - start.y })
      const perpendicular = { x: -axis.y, y: axis.x }
      const arrowSize = presentation.visual.stage === 'late' ? 12 : 9
      context.beginPath()
      context.moveTo(start.x, start.y)
      context.lineTo(end.x, end.y)
      context.moveTo(end.x, end.y)
      context.lineTo(
        end.x - axis.x * arrowSize + perpendicular.x * arrowSize * 0.55,
        end.y - axis.y * arrowSize + perpendicular.y * arrowSize * 0.55,
      )
      context.moveTo(end.x, end.y)
      context.lineTo(
        end.x - axis.x * arrowSize - perpendicular.x * arrowSize * 0.55,
        end.y - axis.y * arrowSize - perpendicular.y * arrowSize * 0.55,
      )
      context.stroke()
    }
    context.restore()
  }

  private spearPreviewTravelDistance(
    node: LastChancesPlanNode,
    start: LastChancesVector,
    direction: LastChancesVector,
    requestedTravel: number,
    radius: number,
  ): number {
    let boundaryTravel = requestedTravel
    const axisLimit = (
      position: number,
      component: number,
      extent: number,
    ): number => {
      if (component > EPSILON) return (extent - radius - position) / component
      if (component < -EPSILON) return (radius - position) / component
      return Number.POSITIVE_INFINITY
    }
    boundaryTravel = Math.min(
      boundaryTravel,
      axisLimit(start.x, direction.x, node.arena.width),
      axisLimit(start.y, direction.y, node.arena.height),
    )
    boundaryTravel = clamp(boundaryTravel, 0, requestedTravel)
    const pointAt = (distance: number): LastChancesVector => ({
      x: start.x + direction.x * distance,
      y: start.y + direction.y * distance,
    })
    const hitsObstacleAt = (distance: number): boolean => node.arena.obstacles.some(obstacle => (
      segmentHitsObstacle(start, pointAt(distance), obstacle, radius)
    ))
    if (!hitsObstacleAt(boundaryTravel)) return boundaryTravel
    let safe = 0
    let blocked = boundaryTravel
    for (let step = 0; step < 16; step += 1) {
      const candidate = (safe + blocked) / 2
      if (hitsObstacleAt(candidate)) blocked = candidate
      else safe = candidate
    }
    return safe
  }

  private spearSpriteLayout(
    node: LastChancesPlanNode,
    center: LastChancesVector,
    direction: LastChancesVector,
    playerRadius: number,
    verticalTilt = 0,
  ): RuntimeSpearSpriteLayout {
    const origin = this.worldToScreen(this.player.position, node)
    const projected = this.worldToScreen({
      x: this.player.position.x + direction.x * 100,
      y: this.player.position.y + direction.y * 100,
    }, node)
    const projection = { x: projected.x - origin.x, y: projected.y - origin.y }
    const pixelsPerWorldUnit = Math.max(0.01, vectorLength(projection) / 100)
    const baseAxis = normalize(projection)
    const axis = normalize({ x: baseAxis.x, y: baseAxis.y + verticalTilt }, baseAxis)
    const pivotRatio = 0.43
    const idleFrontReach = this.primarySpearWeapon()?.attacks.tap.range ?? 176
    const authoredWidth = idleFrontReach / (1 - pivotRatio) * pixelsPerWorldUnit
    const width = clamp(authoredWidth, playerRadius * 5.6, playerRadius * 13.5)
    const imageAspect = this.spearImage && this.spearImage.naturalWidth > 0
      ? this.spearImage.naturalHeight / this.spearImage.naturalWidth
      : 0.22
    return {
      center,
      axis,
      perpendicular: { x: -axis.y, y: axis.x },
      width,
      height: width * imageAspect,
      pivotRatio,
    }
  }

  private drawSpearSprite(layout: RuntimeSpearSpriteLayout): void {
    const context = this.context
    const angle = Math.atan2(layout.axis.y, layout.axis.x)
    context.save()
    context.translate(layout.center.x, layout.center.y)
    context.rotate(angle)
    context.shadowColor = '#ff263c'
    context.shadowBlur = Math.max(7, layout.height * 0.34)
    if (this.spearImage?.complete && this.spearImage.naturalWidth > 0) {
      context.drawImage(
        this.spearImage,
        -layout.width * layout.pivotRatio,
        -layout.height / 2,
        layout.width,
        layout.height,
      )
    } else {
      const rear = -layout.width * layout.pivotRatio
      const tip = layout.width * (1 - layout.pivotRatio)
      context.strokeStyle = '#ff3048'
      context.lineWidth = Math.max(3, layout.height * 0.08)
      context.beginPath()
      context.moveTo(rear, 0)
      context.lineTo(tip, 0)
      context.stroke()
      context.fillStyle = '#d8dce0'
      context.beginPath()
      context.moveTo(tip, 0)
      context.lineTo(tip - layout.height * 0.38, -layout.height * 0.12)
      context.lineTo(tip - layout.height * 0.38, layout.height * 0.12)
      context.closePath()
      context.fill()
    }
    context.restore()
  }

  private renderHeldSpear(
    node: LastChancesPlanNode,
    playerPoint: LastChancesVector,
    playerRadius: number,
  ): void {
    const spear = this.primarySpearWeapon()
    if (!spear || this.projectiles.some(projectile => (
      projectile.weaponId === spear.id && projectile.attack?.behavior === 'spearRelease'
    ))) return

    let direction = normalize(this.player.aim)
    let forwardWorld = 0
    let liftRadii = 0
    let forwardRadii = 0
    let verticalTilt = 0
    const activeArea = [...this.activeAreas]
      .reverse()
      .find(area => area.weaponId === spear.id)
    const activeDash = this.activeDash?.weaponId === spear.id ? this.activeDash : null
    if (activeArea) {
      const progress = clamp(
        1 - activeArea.remainingMs / Math.max(1, activeArea.totalMs),
        0,
        1,
      )
      const shape = activeArea.attack.collider?.shape
        ?? (activeArea.kind === 'burst' ? 'circle' : 'sector')
      if (shape === 'sweep') {
        direction = rotateVector(activeArea.direction, activeArea.sweepDegrees * Math.PI / 180)
      } else if (shape === 'sector') {
        const eased = 0.5 - Math.cos(progress * Math.PI) / 2
        const arc = Math.min(170, activeArea.attack.arcDegrees)
        direction = rotateVector(
          activeArea.direction,
          (-arc / 2 + arc * eased) * Math.PI / 180,
        )
      } else if (shape === 'capsule') {
        direction = normalize(activeArea.direction)
        const attackTravel = Math.max(
          24,
          activeArea.attack.range - (activeArea.attack.collider?.innerRange ?? 0),
        )
        forwardWorld = Math.sin(progress * Math.PI) * Math.min(72, attackTravel * 0.36)
      }
    } else if (activeDash) {
      direction = normalize(activeDash.direction)
      const progress = clamp(activeDash.elapsedMs / Math.max(1, activeDash.attack.durationMs), 0, 1)
      if (activeDash.attack.behavior === 'poleVault') {
        liftRadii = Math.sin(progress * Math.PI) * 2.25
        direction = rotateVector(direction, -Math.sin(progress * Math.PI) * Math.PI * 0.46)
      }
    } else {
      const charge = this.spearChargePresentation(this.frameNowMs || performance.now())
      if (charge) {
        liftRadii = charge.visual.liftRadii
        forwardRadii = charge.visual.forwardRadii
        verticalTilt = charge.visual.verticalTilt
      }
    }

    const ordinaryCenter = { x: playerPoint.x, y: playerPoint.y - playerRadius }
    const baseLayout = this.spearSpriteLayout(node, ordinaryCenter, direction, playerRadius)
    const center = {
      x: ordinaryCenter.x + baseLayout.axis.x * forwardRadii * playerRadius,
      y: ordinaryCenter.y + baseLayout.axis.y * forwardRadii * playerRadius
        - liftRadii * playerRadius,
    }
    if (forwardWorld > 0) {
      const origin = this.worldToScreen(this.player.position, node)
      const forward = this.worldToScreen({
        x: this.player.position.x + direction.x * forwardWorld,
        y: this.player.position.y + direction.y * forwardWorld,
      }, node)
      center.x += forward.x - origin.x
      center.y += forward.y - origin.y
    }
    const layout = this.spearSpriteLayout(node, center, direction, playerRadius, verticalTilt)
    const context = this.context
    const gripSpacing = playerRadius * 0.34
    const firstGrip = {
      x: center.x - layout.axis.x * gripSpacing,
      y: center.y - layout.axis.y * gripSpacing,
    }
    const secondGrip = {
      x: center.x + layout.axis.x * gripSpacing,
      y: center.y + layout.axis.y * gripSpacing,
    }
    if (liftRadii > 0.08) {
      context.save()
      context.strokeStyle = this.config.renderer.playerAccent
      context.lineWidth = Math.max(3, playerRadius * 0.28)
      context.lineCap = 'round'
      context.beginPath()
      context.moveTo(
        ordinaryCenter.x - layout.perpendicular.x * playerRadius * 0.28,
        ordinaryCenter.y - layout.perpendicular.y * playerRadius * 0.28,
      )
      context.lineTo(firstGrip.x, firstGrip.y)
      context.moveTo(
        ordinaryCenter.x + layout.perpendicular.x * playerRadius * 0.28,
        ordinaryCenter.y + layout.perpendicular.y * playerRadius * 0.28,
      )
      context.lineTo(secondGrip.x, secondGrip.y)
      context.stroke()
      context.restore()
    }
    this.drawSpearSprite(layout)
    context.save()
    context.fillStyle = this.config.renderer.player
    context.strokeStyle = this.config.renderer.playerAccent
    context.lineWidth = 1.5
    for (const grip of [firstGrip, secondGrip]) {
      context.beginPath()
      context.arc(grip.x, grip.y, Math.max(2.5, playerRadius * 0.2), 0, Math.PI * 2)
      context.fill()
      context.stroke()
    }
    context.restore()
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
      const input = this.controlInputSnapshot(hand, now)
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
    if (effect.kind === 'hit' && effect.intensity !== undefined) {
      const impact = Math.pow(clamp(effect.intensity, 0, 1), 0.7)
      context.shadowColor = effect.color
      context.shadowBlur = 8 + impact * 28
      context.beginPath()
      context.arc(origin.x, origin.y, 3 + impact * 13 * (1 - progress * 0.5), 0, Math.PI * 2)
      context.strokeStyle = impact > 0.78 ? '#fff3b0' : effect.color
      context.lineWidth = 1.5 + impact * 5
      context.stroke()
      const streaks = 2 + Math.round(impact * 7)
      const normal = { x: -effect.direction.y, y: effect.direction.x }
      for (let index = 0; index < streaks; index += 1) {
        const lane = index - (streaks - 1) / 2
        const spread = lane * (2.5 + impact * 2)
        const length = effect.range * (0.45 + impact * (0.45 + (index % 3) * 0.08))
        const start = this.worldToScreen({
          x: effect.position.x - effect.direction.x * length * 0.65 + normal.x * spread,
          y: effect.position.y - effect.direction.y * length * 0.65 + normal.y * spread,
        }, node)
        const finish = this.worldToScreen({
          x: effect.position.x + effect.direction.x * length * 0.35 + normal.x * spread,
          y: effect.position.y + effect.direction.y * length * 0.35 + normal.y * spread,
        }, node)
        context.beginPath()
        context.moveTo(start.x, start.y)
        context.lineTo(finish.x, finish.y)
        context.globalAlpha = alpha * (0.2 + impact * 0.8)
        context.lineWidth = 0.7 + impact * 2.2
        context.stroke()
      }
    } else if (effect.kind === 'burst') {
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
    const effectiveStats = this.effectivePlayerStats()
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
    context.fillText(`ATK ${Math.round(effectiveStats.attackPower)}%`, 22, 86)
    if (this.currentNode) {
      context.textAlign = 'right'
      context.fillStyle = 'rgba(255,255,255,.66)'
      context.fillText(`TIER ${this.currentNode.tierIndex + 1} · ${this.currentNode.roomName}`, this.cssWidth - 22, 30)
    }
  }

  private renderOverlay(): void {
    if ((this.phase === 'playing' && !this.paused)
      || (this.phase === 'planning' && this.currentNode && !this.routeMapVisible)) return
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
