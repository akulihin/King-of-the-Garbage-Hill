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
  lastChancesCenteredFanOffsets,
  reflectLastChancesVector,
  resolveLastChancesBowCharge,
  resolveLastChancesBowCadencePose,
  sweepLastChancesCircleAgainstArena,
  sweepLastChancesCircleAgainstCircle,
} from './bow-runtime'
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
import {
  LAST_CHANCES_GESTURES,
  LAST_CHANCES_HANDS,
  LAST_CHANCES_OUROBOROS_ITEMS,
} from './types'
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
  LastChancesColliderDefinition,
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
  LastChancesEventLogEntry,
  LastChancesFeedbackPulseDefinition,
  LastChancesHand,
  LastChancesHitEffectDefinition,
  LastChancesHazardDefinition,
  LastChancesMoveQuestSnapshot,
  LastChancesBossHoleDefinition,
  LastChancesBossAltarDefinition,
  LastChancesOutfitDefinition,
  LastChancesOuroborosItem,
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
  LastChancesStatErosion,
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

const DEFAULT_ARMED_INVITATION: LastChancesFeedbackPulseDefinition[] = [
  { delayMs: 0, durationMs: 35, magnitude: 0.58 },
  { delayMs: 90, durationMs: 35, magnitude: 0.58 },
]

interface RuntimePlayer {
  position: LastChancesVector
  aim: LastChancesVector
  hp: number
  mentalHealth: number
  stamina: number
  stats: LastChancesStats
  invulnerableMs: number
  rootMs: number
  recoveryMs: number
  parryMs: number
  armorMultiplier: number
  armorMultiplierMs: number
}

interface RuntimeKnifeSpiderV2 {
  attackMode: 'leap' | 'strike' | null
  flightVelocity: LastChancesVector | null
  reflected: boolean
  embedded: boolean
  orbitDirection: -1 | 1
  decisionMs: number
  evadeMs: number
  evadeDirection: LastChancesVector
  lastReactedAttackAtMs: number
  rng: () => number
}

/**
 * The Invisible wolf hunts instead of patrolling, so unlike the Knife-spider it owns its whole
 * lifecycle — including its own visibility. `phase` is the authority on what the wolf is doing;
 * `RuntimeEnemy.state` is driven from it, and is deliberately held at `idle` while hidden so the
 * shared `enemyVisible` rule keeps the wolf off the screen.
 */
interface RuntimeInvisibleWolf {
  phase: 'unaware' | 'stalking' | 'closing' | 'lunging' | 'recovering' | 'exposed'
  orbitDirection: -1 | 1
  /** Countdown to the next orbit-direction reroll. */
  decisionMs: number
  /** How long the wolf has held a position behind the player's aim. */
  patienceMs: number
  /** Time left before a wolf that has finished a lunge may vanish again. */
  rehideMs: number
  rng: () => number
}

interface RuntimeEnemy {
  id: string
  definition: LastChancesEnemyDefinition
  position: LastChancesVector
  /** Actual last-frame world velocity; stance impact math must not infer motion from facing. */
  velocity: LastChancesVector
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
  /** Null is the complete legacy v1 path; v2 owns its locomotion and flight state here. */
  knifeSpiderV2: RuntimeKnifeSpiderV2 | null
  /** Null is the legacy v1 wolf walking the shared elite path; v2 owns the stalk cycle here. */
  invisibleWolf: RuntimeInvisibleWolf | null
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

interface RuntimeGroundOuroboros {
  id: string
  items: LastChancesOuroborosItem[]
  position: LastChancesVector
  source: 'room' | 'corpse'
  nodeId: string
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
  moveQuests: Record<LastChancesHand, HandMoveQuestState>
  staminaCostStacks: number
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

function copyMoveQuests(
  source: Record<LastChancesHand, HandMoveQuestState>,
): Record<LastChancesHand, HandMoveQuestState> {
  return {
    left: {
      ...source.left,
      unlocked: { ...source.left.unlocked },
      pendingUnlocks: [...source.left.pendingUnlocks],
      roomKills: { ...source.left.roomKills },
    },
    right: {
      ...source.right,
      unlocked: { ...source.right.unlocked },
      pendingUnlocks: [...source.right.pendingUnlocks],
      roomKills: { ...source.right.roomKills },
    },
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
  /**
   * Definition id of whatever fired this, or `player`. The renderer picks the shot's model from
   * it — `sourceName` is a display name and must not be matched on.
   */
  sourceId: string
  attack?: LastChancesAttackDefinition
  weaponId?: string
  hand?: LastChancesHand
  gesture?: LastChancesGesture
  carriedIds?: Set<string>
  storedDot?: LastChancesStoredDot | null
  /** Longbow arrows survive impact; scatter arrows may reflect from one surface first. */
  persistentArrow?: boolean
  chemicalArrow?: boolean
  ricochetsRemaining?: number
}

interface RuntimeEmbeddedArrow {
  id: number
  position: LastChancesVector
  direction: LastChancesVector
  attachment: 'enemy' | 'obstacle' | 'boundary' | 'floor'
  enemyId: string | null
  /** Offset from a living enemy's centre, kept in world coordinates because enemies do not rotate. */
  enemyOffset: LastChancesVector | null
  color: string
  chemical: boolean
  /** Ignition chars the arrow but never removes it. */
  exploded: boolean
  embeddedAtMs: number
  nextChemicalTickAtMs: number
  /** Rain arrows keep their steep screen-space planting pose after the tip reaches the floor. */
  planted?: boolean
}

interface RuntimeFallingArrow {
  id: number
  target: LastChancesVector
  direction: LastChancesVector
  remainingMs: number
  totalMs: number
  attack: LastChancesAttackDefinition
  weaponId: string
  hand: LastChancesHand
  gesture: LastChancesGesture
  chemical: boolean
}

interface RuntimeBowChannel {
  hand: LastChancesHand
  weaponId: string
  gesture: 'doubleTapHold' | 'hold'
  behavior: 'bowRapidFire' | 'bowRain'
  attack: LastChancesAttackDefinition
  elapsedMs: number
  shotAccumulatorMs: number
  staminaAccumulatorMs: number
  /** DOM/gamepad input clock through which this channel has already been charged and emitted. */
  lastSettledAtInputMs: number
}

interface RuntimeBowDrawDebit {
  active: boolean
  accruedMs: number
  spent: number
  exhausted: boolean
}

interface RuntimeBowShotPresentation {
  atMs: number
  direction: LastChancesVector
  goldenUntilMs: number
}

interface RuntimeReleasedBowDraw {
  hand: LastChancesHand
  heldMs: number
  direction: LastChancesVector
  golden: boolean
  releasedAtMs: number
}

interface RuntimeBowResponseWindow {
  weaponId: string
  startedAtInputMs: number
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
  /**
   * «Прорыв» only. A breakthrough is *held*, not charged: it is time-budgeted instead of
   * distance-budgeted, steers with the cursor, ramps its speed and coasts to a stop when the
   * button comes up. Every other dash leaves this undefined and keeps the constant-speed path.
  */
  ram?: RuntimeBreakthrough
  /** Five authored phases of the spear-v2 Olympic pole vault. */
  poleVault?: RuntimePoleVault
  /** Airborne height inherited when «Прыжок» extends a live «Уворот». */
  bowLiftStartRadii?: number
}

interface RuntimePoleVault {
  runMs: number
  plantMs: number
  riseMs: number
  flightMs: number
  landMs: number
  runDistanceRatio: number
}

interface RuntimeBreakthrough {
  runMs: number
  /** Elapsed stamp of the release (or of the cap/exhaustion that stood in for one). */
  releasedAtMs: number | null
  speed: number
  staminaAccumulatorMs: number
  /** Kept apart from the dash's shared trail timer so an augment's trail cannot starve it. */
  streakAccumulatorMs: number
  /** Highest speed tier already announced, so each escalation cue fires exactly once. */
  tierCued: number
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
  /** Stance channels pay recurring stamina without touching the global combo clock. */
  channelStaminaAccumulatorMs: number
  /** Current tip speed created by turning the held spear, in world units per second. */
  stanceCutSpeed: number
  /** Actual world velocity of the held spear tip, including running and cursor motion. */
  stanceTipVelocity: LastChancesVector
}

interface RuntimeEffect {
  /** `shock` is the hole-strike blast: an expanding world-space ring plus an opening flash. */
  kind: 'melee' | 'burst' | 'dash' | 'hit' | 'shock'
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
  /** «Ответ» must loose only after its owning bow jump has actually landed. */
  waitForBowDashWeaponId?: string
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
  /**
   * Absolute deadline for a primed Unterhau. It is deliberately later than `unterhauDueAtMs`:
   * the due stamp is anchored to the previous frame's `elapsedMs` while the hold gate measures
   * real time from the press, so an expiry sharing the same stamp always won the race and the
   * follow-up could never fire while the room's update loop was running (M134).
   */
  unterhauExpiresAtMs: number
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
  projectileKnockback: number
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

/** One room's world→screen basis. `scaleX`/`scaleY` apply to both world axes alike. */
interface IsometricProjection {
  originX: number
  top: number
  scaleX: number
  scaleY: number
}

const LAST_CHANCES_EVENT_LOG_LIMIT = 8

const MOVEMENT_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowLeft', 'ArrowDown', 'ArrowRight'])
const PLAYER_PARRY_BEHAVIORS = new Set<LastChancesAttackBehavior>([
  'parry',
  'chainStrike',
  'axeParry',
  'katanaParry',
])
const EPSILON = 0.000001
/**
 * Двуручное копьё v2 keeps the замах and the overhead spin of the original lance, so both
 * spear generations have to answer the shared release/spin rules — projectile carry, wall
 * pinning, the spin's fog suppression, headshot bonus and continuation context.
 */
function isSpearReleaseBehavior(behavior: LastChancesAttackBehavior | undefined): boolean {
  return behavior === 'spearRelease' || behavior === 'spearReleaseV2'
}

function isSpearSpinBehavior(behavior: LastChancesAttackBehavior | undefined): boolean {
  return behavior === 'spearSpin' || behavior === 'spearOverheadSpin'
}

/**
 * v2 is identified by the behavior of its tap rather than by `trait` or `id`, because both
 * hands of a two-handed weapon share those and the reworked rules are primary-hand only.
 */
function isSpearV2Primary(weapon: LastChancesResolvedWeapon | undefined | null): boolean {
  return weapon?.attacks.tap.behavior === 'spearHunt'
}

function isSpearV2Secondary(weapon: LastChancesResolvedWeapon | undefined | null): boolean {
  return weapon?.id === 'twohand-spear-v2'
    && weapon.attacks.tap.behavior === 'parry'
    && weapon.attacks.hold.behavior === 'spearStance'
}

function isLongbowPrimary(weapon: LastChancesResolvedWeapon | undefined | null): boolean {
  return weapon?.trait === 'longbowPersistence'
    && weapon.attacks.tap.behavior === 'bowShot'
}

function isLongbowSecondary(weapon: LastChancesResolvedWeapon | undefined | null): boolean {
  return weapon?.trait === 'longbowPersistence'
    && weapon.attacks.tap.behavior === 'bowDodge'
}

function isLongbowArrowBehavior(behavior: LastChancesAttackBehavior | undefined): boolean {
  return behavior === 'bowShot'
    || behavior === 'bowDoubleShot'
    || behavior === 'bowRapidFire'
    || behavior === 'bowDraw'
    || behavior === 'bowScatter'
    || behavior === 'bowRiposte'
    || behavior === 'bowRain'
}

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

function moveNumberTowards(current: number, target: number, maximumDelta: number): number {
  if (Math.abs(target - current) <= maximumDelta) return target
  return current + Math.sign(target - current) * maximumDelta
}

/** Pointer Events' `buttons` bitfield does not use the same ordering as `button`. */
function pointerButtonMask(button: number): number {
  if (button === 0) return 1
  if (button === 1) return 4
  if (button === 2) return 2
  if (button === 3) return 8
  if (button === 4) return 16
  if (button === 5) return 32
  return 0
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

function isKnifeSpiderV2Definition(definition: LastChancesEnemyDefinition): boolean {
  return definition.id === 'spider-knife'
    && tuningValue(definition, 'behaviorVersion', 2) >= 2
}

/** `behaviorVersion: 1` restores the shared elite path the wolf used before the stalk cycle. */
function isInvisibleWolfDefinition(definition: LastChancesEnemyDefinition): boolean {
  return definition.id === 'invisible-wolf'
    && tuningValue(definition, 'behaviorVersion', 2) >= 2
}

/**
 * Mixes a `#rrggbb` definition colour toward black (negative) or white (positive). The hand-drawn
 * enemy bodies use it for depth — far limbs, shaded undersides, lit edges — so every model stays
 * keyed to the one colour the Builder exposes. Anything that is not a plain hex passes through.
 */
function shadeEnemyColor(color: string, amount: number): string {
  if (!/^#[0-9a-f]{6}$/i.test(color)) return color
  const target = amount < 0 ? 0 : 255
  const weight = Math.min(1, Math.abs(amount))
  const channel = (offset: number): number => {
    const value = Number.parseInt(color.slice(offset, offset + 2), 16)
    return Math.round(value + (target - value) * weight)
  }
  return `#${[1, 3, 5].map(offset => channel(offset).toString(16).padStart(2, '0')).join('')}`
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
  /**
   * Двуручное копьё v2: a landed «Прокол» opens a fixed window in which every tap re-fires the
   * thrust instead of walking the tap chain. The window is stamped once and never extended, so
   * the ceiling on a single burst is mashing speed against stamina, not endurance.
   */
  private readonly pierceMash: Record<LastChancesHand, { expiresAtMs: number; hits: number }> = {
    left: { expiresAtMs: 0, hits: 0 },
    right: { expiresAtMs: 0, hits: 0 },
  }
  private readonly gamepadButtons: Record<LastChancesHand, boolean> = { left: false, right: false }
  /** Hand and elapsed stamp of the last action that actually executed, for the stamina refunds. */
  private lastAttackHand: LastChancesHand | null = null
  private lastAttackAtMs = Number.NEGATIVE_INFINITY
  private staminaRefusedAtMs: number | null = null
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
  private staminaCostStacks = 0
  private voluntaryChanceSpendProgress = 0
  private activeLoadout: LastChancesLoadoutDefinition | null
  private corpseBoundPrimaryWeaponId: string | null = null
  private groundWeapons: RuntimeGroundWeapon[] = []
  private groundOuroboros: RuntimeGroundOuroboros[] = []
  private readonly ouroborosCorpses = new Map<string, RuntimeGroundOuroboros[]>()
  private ouroborosDiscovered: Record<LastChancesOuroborosItem, boolean> = {
    fang: false,
    acid: false,
    scale: false,
  }
  private ouroborosEquipped: Record<LastChancesOuroborosItem, boolean> = {
    fang: false,
    acid: false,
    scale: false,
  }
  private ouroborosFangKillStacks = 0
  private ouroborosAcidChancesSpent = 0
  private readonly ouroborosScaleRoomStacks = new Map<string, number>()
  private nextGroundOuroborosId = 1
  /** Run feed shown in the telemetry sidebar; newest last, capped. */
  private eventLog: LastChancesEventLogEntry[] = []
  private nextEventLogId = 1
  private rewardChest: RuntimeRewardChest | null = null
  private nextGroundWeaponId = 1
  private ninjaDashReadyAtMs = 0
  /** The page-owned route overlay suppresses cleared-room exploration while it is visible. */
  private routeMapVisible = true
  private currentNode: LastChancesPlanNode | null = null
  private availableNodeIds: string[] = []
  private sacrificeNodeIds: string[] = []
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
  /** Arrow decals belong to the room, not to the short-lived projectile/effect pools. */
  private embeddedArrows: RuntimeEmbeddedArrow[] = []
  private fallingArrows: RuntimeFallingArrow[] = []
  private readonly bowChannels = new Map<LastChancesHand, RuntimeBowChannel>()
  /** A capped/exhausted channel cannot restart until its controlling input is released. */
  private readonly bowChannelConsumedUntilRelease: Record<LastChancesHand, boolean> = {
    left: false,
    right: false,
  }
  /** Release edge that must not be allowed to reopen a channel through its later classifier. */
  private readonly bowChannelReleasedAtInputMs: Record<LastChancesHand, number> = {
    left: Number.NEGATIVE_INFINITY,
    right: Number.NEGATIVE_INFINITY,
  }
  private readonly bowDrawDebits: Record<LastChancesHand, RuntimeBowDrawDebit> = {
    left: { active: false, accruedMs: 0, spent: 0, exhausted: false },
    right: { active: false, accruedMs: 0, spent: 0, exhausted: false },
  }
  /**
   * A semantic continuation can consume/refund Натяг while its physical controls are still
   * reported as held for the rest of the frame. Keep that completed draw closed until release,
   * otherwise the same historical hold time is charged a second time.
   */
  private readonly bowDrawConsumedUntilRelease: Record<LastChancesHand, boolean> = {
    left: false,
    right: false,
  }
  private readonly bowLastShotDirections: Record<LastChancesHand, LastChancesVector> = {
    left: { x: 1, y: 0 },
    right: { x: 1, y: 0 },
  }
  /** World-space destination of the first «Шот», so «Шот-шот» converges after any displacement. */
  private readonly bowLastShotTargets: Record<LastChancesHand, LastChancesVector | null> = {
    left: null,
    right: null,
  }
  private readonly bowLastShotAtMs: Record<LastChancesHand, number> = {
    left: Number.NEGATIVE_INFINITY,
    right: Number.NEGATIVE_INFINITY,
  }
  private readonly bowLastShotWeaponIds: Record<LastChancesHand, string | null> = {
    left: null,
    right: null,
  }
  private readonly bowDoubleShotAtMs: Record<LastChancesHand, number> = {
    left: Number.NEGATIVE_INFINITY,
    right: Number.NEGATIVE_INFINITY,
  }
  private readonly bowDoubleShotWeaponIds: Record<LastChancesHand, string | null> = {
    left: null,
    right: null,
  }
  private readonly bowResponseWindows: Record<LastChancesHand, RuntimeBowResponseWindow | null> = {
    left: null,
    right: null,
  }
  /** DeepList releases a hold before classifying its following tap; keep «Огонь!» reachable. */
  private readonly bowRainReleasedAtInputMs: Record<LastChancesHand, number> = {
    left: Number.NEGATIVE_INFINITY,
    right: Number.NEGATIVE_INFINITY,
  }
  private bowShotPresentation: RuntimeBowShotPresentation = {
    atMs: Number.NEGATIVE_INFINITY,
    direction: { x: 1, y: 0 },
    goldenUntilMs: Number.NEGATIVE_INFINITY,
  }
  private releasedBowDraw: RuntimeReleasedBowDraw | null = null
  private bowRainTarget: LastChancesVector | null = null
  private activeAreas: RuntimeActiveArea[] = []
  private effects: RuntimeEffect[] = []
  private traces: RuntimeColliderTrace[] = []
  private delayedAttacks: RuntimeDelayedAttack[] = []
  private delayedRecoveries: RuntimeDelayedRecovery[] = []
  private readonly weaponStates = new Map<string, RuntimeWeaponState>()
  private readonly heldChannels = new Map<LastChancesHand, RuntimeActiveArea>()
  private readonly weaponActionEnds = new Map<string, number>()
  private activeParryCollider: LastChancesRuntimeCollider | null = null
  private activeParryAttack: LastChancesAttackDefinition | null = null
  private provisionalParry: RuntimeProvisionalParry | null = null
  private roomElapsedMs = 0
  private readonly hazardHitCycles = new Map<string, number>()
  private readonly hazardSuppressedUntil = new Map<string, number>()
  private interactionResolved = false
  /**
   * The apartment always lends the Mercenary Sword for the reflection lesson. The authored
   * Builder loadout is parked here and restored as soon as the opening room is cleared.
   */
  private postPrologueLoadout: LastChancesLoadoutDefinition | null = null
  private prologueLoadoutForced = false
  private knifeSpiderTutorialPhase: 'pending' | 'slowing' | 'frozen' | 'resuming' | 'complete' = 'pending'
  private knifeSpiderTutorialResumeMs = 0
  private resolvingKnifeSpiderTutorialParry = false
  private activeDash: RuntimeDash | null = null
  private activeSwordAdvance: RuntimeSwordAdvance | null = null
  private nextProjectileId = 1
  private lastGesture: LastChancesGestureSnapshot | null = null
  private player: RuntimePlayer
  /** Ordinary walking velocity; dashes/forced movement remain separate authored actions. */
  private movementVelocity: LastChancesVector = { x: 0, y: 0 }
  /** Head-on wall slide already committed to, so abutting obstacles cannot make it oscillate. */
  private wallSlideMemo: { axis: 'x' | 'y', sign: number } | null = null
  private touchMove: LastChancesVector = { x: 0, y: 0 }
  private touchAim: LastChancesVector = { x: 0, y: 0 }
  private gamepadMove: LastChancesVector = { x: 0, y: 0 }
  private gamepadAim: LastChancesVector = { x: 0, y: 0 }
  /** Keeps a centered right stick from handing aim back to an older pointer position. */
  private retainedGamepadAim: LastChancesVector | null = null
  private pointerAim: LastChancesVector = { x: 1, y: 0 }
  /** Exact cursor position retained for the longbow's world-space rain target. */
  private pointerWorldTarget: LastChancesVector | null = null
  private pointerClientX: number | null = null
  private pointerDeltaX = 0
  /** Reconciled from PointerEvent.buttons so chorded LMB/RMB edges remain independent. */
  private readonly pressedPointerButtons = new Set<number>()
  private activeAttackPointerId: number | null = null
  private suppressPointerButtonsUntilRelease = false
  /**
   * Weapons that answer on the press edge instead of waiting out the double-tap window record
   * what they already fired here, so the recognizer's deferred resolution can be swallowed as a
   * duplicate. Shared by the Mercenary Sword's rhythm and Двуручное копьё v2's «Моментальная»
   * Охота — both trade the 260 ms `doubleTapMs` wait for zero-latency response.
   */
  private readonly immediateHandInput: Record<LastChancesHand, {
    tapExecuted: boolean
    doubleTapExecuted: boolean
    unterhauExecuted: boolean
    breakthroughStarted: boolean
    bowChannelStarted: boolean
    bowFollowUpExecuted: boolean
    bowDrawExecuted: boolean
  }> = {
    left: {
      tapExecuted: false,
      doubleTapExecuted: false,
      unterhauExecuted: false,
      breakthroughStarted: false,
      bowChannelStarted: false,
      bowFollowUpExecuted: false,
      bowDrawExecuted: false,
    },
    right: {
      tapExecuted: false,
      doubleTapExecuted: false,
      unterhauExecuted: false,
      breakthroughStarted: false,
      bowChannelStarted: false,
      bowFollowUpExecuted: false,
      bowDrawExecuted: false,
    },
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
  /** Armed-pocket telegraph loops, keyed by the physical trigger that owns the pocket. */
  private readonly dualSenseTelegraphs: Record<
    LastChancesHand,
    { nodeId: string, nextAtMs: number } | null
  > = { left: null, right: null }
  /** Null until observed; true means the Fang's five-second trigger wall is engaged. */
  private readonly fangCooldownActive: Record<LastChancesHand, boolean | null> = {
    left: null,
    right: null,
  }
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
  private longbowImage: HTMLImageElement | null = null
  private knifeSpiderV2Image: HTMLImageElement | null = null
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
      this.longbowImage = new Image()
      this.longbowImage.decoding = 'async'
      this.longbowImage.onload = () => {
        if (!this.destroyed) this.render()
      }
      this.longbowImage.src = '/99lc/longbow.png'
      this.knifeSpiderV2Image = new Image()
      this.knifeSpiderV2Image.decoding = 'async'
      this.knifeSpiderV2Image.onload = () => {
        if (!this.destroyed) this.render()
      }
      this.knifeSpiderV2Image.src = '/99lc/spider-knife-v2.png'
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
      stamina: baseStats.maxStamina,
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
    }, (hand) => {
      const weapon = this.weapons.get(hand)
      if (isSpearV2Primary(weapon) || isLongbowPrimary(weapon)) {
        return weapon.attacks.hold.charge?.bands[0]?.minMs ?? this.config.input.holdMs
      }
      return this.config.input.holdMs
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
    if (this.feedbackPreferences.mode !== 'full') {
      this.dualSenseTelegraphs.left = null
      this.dualSenseTelegraphs.right = null
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
    const hand = physicalClusterToRuntimeHand(physicalHand)
    const weapon = this.weapons.get(hand)
    if (weapon?.trait === 'ouroborosFang'
      && (this.cooldownEnds.get(cooldownKey(hand, 'tap')) ?? 0) <= this.elapsedMs) {
      return null
    }
    const baseTrigger = weapon?.controls?.dualsense.haptics?.baseTrigger
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
    this.activeParryAttack = null
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
    if (this.sacrificeNodeIds.includes(nodeId)) {
      const ratio = this.config.progression.sameTierSacrificeRatio
      this.player.hp *= ratio
      this.player.mentalHealth *= ratio
      this.addEventLog(
        `Боковой путь: здоровье и рассудок оставлены на ${Math.round(ratio * 100)}%`,
      )
    }
    this.currentNode = node
    if (node.roomTemplateId === 'false-apartment') this.forcePrologueSwordLoadout()
    this.routeMapVisible = false
    this.attemptPath.push(node.id)
    this.availableNodeIds = []
    this.sacrificeNodeIds = []
    this.selectedNodeId = null
    this.selectedInteractionChoiceId = null
    this.player.position = { ...node.arena.playerSpawn }
    this.player.aim = { ...this.pointerAim }
    this.pointerWorldTarget = null
    this.clearCombatTransients()
    this.projectiles = []
    this.activeAreas = []
    this.effects = []
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.activeParryAttack = null
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
    this.resetStaminaChain()
    this.gestures.reset()
    for (const hand of LAST_CHANCES_HANDS) this.resetImmediateHandInput(hand)
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
    this.groundOuroboros = (this.ouroborosCorpses.get(node.id) ?? [])
      .map(pickup => ({ ...pickup, items: [...pickup.items], position: { ...pickup.position } }))
    if (node.ouroborosPickup && !this.ouroborosDiscovered[node.ouroborosPickup.item]) {
      this.groundOuroboros.push({
        id: `ouroboros-room-${node.id}-${node.ouroborosPickup.item}`,
        items: [node.ouroborosPickup.item],
        position: { ...node.ouroborosPickup.position },
        source: 'room',
        nodeId: node.id,
      })
    }
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
        velocity: { x: 0, y: 0 },
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
        knifeSpiderV2: isKnifeSpiderV2Definition(definition)
          ? {
              attackMode: null,
              flightVelocity: null,
              reflected: false,
              embedded: false,
              orbitDirection: rng() < 0.5 ? -1 : 1,
              decisionMs: 0,
              evadeMs: 0,
              evadeDirection: { x: 0, y: 0 },
              lastReactedAttackAtMs: Number.NEGATIVE_INFINITY,
              rng: createLastChancesRng(`${node.seed}:${enemy.id}:knife-spider-v2`),
            }
          : null,
        invisibleWolf: isInvisibleWolfDefinition(definition)
          ? {
              phase: 'unaware',
              orbitDirection: rng() < 0.5 ? -1 : 1,
              decisionMs: 0,
              patienceMs: 0,
              rehideMs: 0,
              rng: createLastChancesRng(`${node.seed}:${enemy.id}:invisible-wolf`),
            }
          : null,
      }
    })
    if (node.roomTemplateId === 'false-apartment') this.startKnifeSpiderPrologue()
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
      this.spendVoluntaryChances(altar.chanceCost)
      this.bossCheckpoint = {
        nodeId: this.currentNode!.id,
        attemptPath: [...this.attemptPath],
        loadout: this.activeLoadout ? { ...this.activeLoadout } : null,
        moveQuests: copyMoveQuests(this.moveQuests),
        staminaCostStacks: this.staminaCostStacks,
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
    const ouroboros = this.nearestGroundOuroboros()
    if (ouroboros) return this.pickUpGroundOuroboros(ouroboros)
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

  performKnifeSpiderTutorialParry(): boolean {
    if (this.knifeSpiderTutorialPhase !== 'frozen'
      || this.phase !== 'playing'
      || this.paused
      || this.weapons.get('left')?.id !== 'hybrid-sword') return false
    this.resolvingKnifeSpiderTutorialParry = true
    try {
      this.performAttack({
        hand: 'left',
        gesture: 'tap',
        atMs: this.elapsedMs,
        heldMs: 0,
        firstHoldMs: 0,
      })
    } finally {
      this.resolvingKnifeSpiderTutorialParry = false
    }
    // Mercenary Sword sweeps are traced over time in normal combat. The frozen
    // tutorial frame cannot advance that trace, so resolve its opening sample now.
    this.updateActiveAreas(0)
    if (this.knifeSpiderTutorialPhase === 'frozen') {
      const spider = this.enemies.find(enemy => (
        enemy.knifeSpiderV2?.attackMode === 'leap'
        && enemy.knifeSpiderV2.flightVelocity
      ))
      if (spider) this.reflectKnifeSpiderV2(spider, this.player.aim)
    }
    return this.knifeSpiderTutorialPhase === 'resuming'
  }

  retryAttempt(): boolean {
    if (this.phase !== 'dead' || this.chances <= 0) return false
    const checkpoint = this.bossCheckpoint
    this.bossCheckpoint = null
    this.resetAttempt()
    if (checkpoint) {
      this.activeLoadout = checkpoint.loadout ? { ...checkpoint.loadout } : null
      this.moveQuests = copyMoveQuests(checkpoint.moveQuests)
      this.staminaCostStacks = checkpoint.staminaCostStacks
      this.rebuildWeapons()
      this.attemptPath = checkpoint.attemptPath.slice(0, -1)
      this.availableNodeIds = [checkpoint.nodeId]
      this.sacrificeNodeIds = []
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
    this.groundOuroboros = []
    this.ouroborosCorpses.clear()
    this.eventLog = []
    this.ouroborosDiscovered = { fang: false, acid: false, scale: false }
    this.ouroborosEquipped = { fang: false, acid: false, scale: false }
    this.ouroborosFangKillStacks = 0
    this.ouroborosAcidChancesSpent = 0
    this.voluntaryChanceSpendProgress = 0
    this.ouroborosScaleRoomStacks.clear()
    this.nextGroundOuroborosId = 1
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
    if (this.config.combat.attackStopsMovement && this.weapons.has(hand)) {
      this.movementVelocity = { x: 0, y: 0 }
    }
    const now = performance.now()
    this.bowChannelReleasedAtInputMs[hand] = Number.NEGATIVE_INFINITY
    this.bowChannelConsumedUntilRelease[hand] = false
    this.gestures.press(hand, now)
    const weapon = this.weapons.get(hand)
    // Spear-v2 support input is a real press-edge parry. The same provisional guard is
    // deliberately allowed to morph into the second-press shove/kick instead of waiting for
    // DeepList's tap classifier.
    if (isSpearV2Secondary(weapon)) this.beginProvisionalTapParry(hand)
    const longbow = isLongbowPrimary(weapon) || isLongbowSecondary(weapon)
    const instant = weapon?.trait === 'swordRhythm' || isSpearV2Primary(weapon)
    if (!instant && !longbow) return
    const input = this.gestures.snapshot(hand, now)
    const immediate = this.immediateHandInput[hand]
    const releasedRainFollowUp = isLongbowSecondary(weapon)
      && now >= this.bowRainReleasedAtInputMs[hand]
      && now - this.bowRainReleasedAtInputMs[hand]
        <= this.config.input.holdThenDoubleTapWindowMs + 120
    if (releasedRainFollowUp) {
      // A full two-second Rain outlives the generic recognizer's holdMaxMs and therefore has
      // no pending `afterHoldTap` sequence. Consume its physical follow-up on the press edge;
      // the matching release is swallowed below so it cannot turn into an accidental Dodge.
      immediate.bowFollowUpExecuted = true
      this.performAttack({
        hand,
        gesture: 'holdThenDoubleTap',
        atMs: now,
        heldMs: 0,
        firstHoldMs: input.pendingChargeMs,
      })
      this.bowRainReleasedAtInputMs[hand] = Number.NEGATIVE_INFINITY
      return
    }
    if (input.sequence === 'first') {
      if (isLongbowPrimary(weapon)) this.bowDrawConsumedUntilRelease[hand] = false
      immediate.tapExecuted = false
      immediate.doubleTapExecuted = false
      immediate.unterhauExecuted = false
      immediate.breakthroughStarted = false
      immediate.bowChannelStarted = false
      immediate.bowFollowUpExecuted = false
      immediate.bowDrawExecuted = false
      if (isLongbowPrimary(weapon)) this.resetBowDrawDebit(hand)
      // A bow cannot fire on the press edge: that would make every Натяг begin with an
      // unintended Шот. Its short first release is committed below, while the second press
      // remains instant so Шот-шот and Прыжок feel immediate.
      if (longbow) return
      immediate.tapExecuted = this.gestureReady(hand, 'tap')
      if (immediate.tapExecuted) {
        this.performAttack({ hand, gesture: 'tap', atMs: this.elapsedMs, heldMs: 0, firstHoldMs: 0 })
      }
      return
    }
    if (input.sequence === 'secondTap') {
      immediate.doubleTapExecuted = (!longbow || immediate.tapExecuted)
        && this.gestureReady(hand, 'doubleTap')
      immediate.unterhauExecuted = false
      immediate.breakthroughStarted = false
      immediate.bowChannelStarted = false
      immediate.bowFollowUpExecuted = false
      immediate.bowDrawExecuted = false
      if (immediate.doubleTapExecuted) {
        this.performAttack({
          hand,
          gesture: 'doubleTap',
          atMs: longbow ? now : this.elapsedMs,
          heldMs: 0,
          firstHoldMs: 0,
        })
      }
    }
    // 'afterHoldTap' is deliberately left to the recognizer: «Акали» has to resolve on release
    // so it can read `firstHoldMs` and pick its charge band.
  }

  release(hand: LastChancesHand): void {
    if (this.destroyed) return
    const now = performance.now()
    const input = this.gestures.snapshot(hand, now)
    const weapon = this.weapons.get(hand)
    const immediate = this.immediateHandInput[hand]
    if (isLongbowSecondary(weapon) && immediate.bowFollowUpExecuted) {
      this.bowChannelConsumedUntilRelease[hand] = false
      this.gestures.cancel(hand)
      this.resetImmediateHandInput(hand)
      return
    }
    if (this.canUseRoomActions() && !this.paused && input.pressed) {
      const longbow = isLongbowPrimary(weapon) || isLongbowSecondary(weapon)
      const bowHoldThresholdMs = isLongbowPrimary(weapon)
        ? weapon.attacks.hold.charge?.bands[0]?.minMs ?? this.config.input.holdMs
        : this.config.input.holdMs
      if (longbow
        && input.sequence === 'first'
        && input.heldMs < bowHoldThresholdMs
        && !immediate.tapExecuted) {
        if (isLongbowPrimary(weapon)) {
          this.accrueBowDrawDebit(hand, weapon.attacks.hold, input.heldMs)
        }
        immediate.tapExecuted = this.gestureReady(hand, 'tap')
        if (immediate.tapExecuted) {
          this.performAttack({
            hand,
            gesture: 'tap',
            atMs: now,
            heldMs: input.heldMs,
            firstHoldMs: 0,
          })
        } else if (isLongbowPrimary(weapon)) {
          this.consumeBowDrawDebit(hand, true)
        }
      }
      if (isLongbowPrimary(weapon)
        && input.sequence === 'first'
        && input.heldMs >= bowHoldThresholdMs
        && !immediate.bowDrawExecuted) {
        immediate.bowDrawExecuted = this.gestureReady(hand, 'hold')
        if (immediate.bowDrawExecuted) {
          // Close the time between the last animation frame and this physical release before
          // resolving power. A backgrounded tab therefore cannot gain unpaid draw strength.
          this.accrueBowDrawDebit(hand, weapon.attacks.hold, input.heldMs)
          this.performAttack({
            hand,
            gesture: 'hold',
            atMs: now,
            heldMs: input.heldMs,
            firstHoldMs: 0,
          })
        } else {
          // Tentative per-ms draw drain must not survive a release the cooldown/recovery refused.
          this.consumeBowDrawDebit(hand, true)
        }
      }
      if (isLongbowSecondary(weapon)
        && input.sequence === 'secondTap'
        && immediate.doubleTapExecuted
        && !immediate.bowFollowUpExecuted) {
        const response = weapon.attacks.doubleTapHold
        const goldStartMs = tuningValue(response, 'goldStartMs', 470)
        if (input.heldMs >= goldStartMs) {
          immediate.bowFollowUpExecuted = this.gestureReady(hand, 'doubleTapHold')
          if (immediate.bowFollowUpExecuted) {
            this.performAttack({
              hand,
              gesture: 'doubleTapHold',
              atMs: now,
              heldMs: input.heldMs,
              firstHoldMs: 0,
            })
          }
        } else this.bowResponseWindows[hand] = null
      }
    }
    if (this.canUseRoomActions()
      && !this.paused
      && input.pressed
      && input.sequence === 'first'
      && input.heldMs < this.config.input.holdMs
      && this.provisionalParry?.hand !== hand) {
      this.beginProvisionalTapParry(hand)
    }
    // Channels own locomotion through `bowChannels`, not a residual root timer. Remove them on
    // the release edge so Чреда/Обстрел stop firing and return movement in this same input turn.
    if (input.pressed && weapon?.trait === 'longbowPersistence') {
      this.settleBowChannelRelease(hand, now, input.heldMs, input.sequence)
    } else if (this.bowChannels.has(hand)) {
      this.stopBowChannel(hand, true)
    }
    this.gestures.release(hand, now)
  }

  private handleLegacyGestureResolution(resolution: LastChancesGestureResolution): void {
    const weapon = this.weapons.get(resolution.hand)
    const immediate = this.immediateHandInput[resolution.hand]
    if (weapon?.trait === 'swordRhythm') {
      if (resolution.gesture === 'tap' && immediate.tapExecuted) {
        this.resetImmediateHandInput(resolution.hand)
        return
      }
      if (resolution.gesture === 'doubleTap' && immediate.doubleTapExecuted) {
        this.cancelPendingUnterhau(weapon)
        this.resetImmediateHandInput(resolution.hand)
        return
      }
      if (resolution.gesture === 'doubleTapHold' && immediate.doubleTapExecuted) {
        if (!immediate.unterhauExecuted
          && resolution.heldMs >= tuningValue(weapon, 'unterhauHoldMs', 1000)) {
          immediate.unterhauExecuted = this.executePendingUnterhau(resolution.hand)
        }
        if (!immediate.unterhauExecuted) this.cancelPendingUnterhau(weapon)
        this.resetImmediateHandInput(resolution.hand)
        return
      }
    }
    if (isSpearV2Primary(weapon)) {
      // «Прорыв» is started by the frame poll well below `holdMs`, so a short run releases as
      // `doubleTap` and a long one as `doubleTapHold`. Both have to be swallowed or the player
      // eats a second «Прокол» at the end of every breakthrough.
      const alreadyRun = resolution.gesture === 'tap'
        ? immediate.tapExecuted
        : resolution.gesture === 'doubleTap' || resolution.gesture === 'doubleTapHold'
          ? immediate.doubleTapExecuted || immediate.breakthroughStarted
          : false
      if (alreadyRun) {
        this.resetImmediateHandInput(resolution.hand)
        return
      }
    }
    if (isLongbowPrimary(weapon) || isLongbowSecondary(weapon)) {
      const alreadyRun = resolution.gesture === 'tap'
        ? immediate.tapExecuted
        : resolution.gesture === 'doubleTap'
          ? immediate.doubleTapExecuted
          : resolution.gesture === 'doubleTapHold'
            ? immediate.bowChannelStarted
              || immediate.bowFollowUpExecuted
              || (isLongbowSecondary(weapon) && immediate.doubleTapExecuted)
            : resolution.gesture === 'hold'
              ? immediate.bowChannelStarted || immediate.bowDrawExecuted
              : false
      if (alreadyRun) {
        this.resetImmediateHandInput(resolution.hand)
        return
      }
    }
    this.resetImmediateHandInput(resolution.hand)
    this.performAttack(resolution)
  }

  private resetImmediateHandInput(hand: LastChancesHand): void {
    this.immediateHandInput[hand] = {
      tapExecuted: false,
      doubleTapExecuted: false,
      unterhauExecuted: false,
      breakthroughStarted: false,
      bowChannelStarted: false,
      bowFollowUpExecuted: false,
      bowDrawExecuted: false,
    }
  }

  private updateImmediateSwordInputs(now: number): void {
    for (const hand of LAST_CHANCES_HANDS) {
      const immediate = this.immediateHandInput[hand]
      const weapon = this.weapons.get(hand)
      if (weapon?.trait !== 'swordRhythm') continue
      const holdGateMs = tuningValue(weapon, 'unterhauHoldMs', 1000)
      if (immediate.doubleTapExecuted && !immediate.unterhauExecuted) {
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
    this.pointerWorldTarget = { ...world }
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
    const unscaledDeltaMs = clamp(frameMs - this.lastFrameMs, 0, 50)
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
      this.advanceKnifeSpiderTutorial(unscaledDeltaMs)
      const deltaMs = unscaledDeltaMs * this.knifeSpiderTutorialTimeScale()
      this.elapsedMs += deltaMs
      if (this.phase === 'playing') this.update(deltaMs / 1000, deltaMs)
      else if (this.canExploreRoom()) this.updateClearedRoom(deltaMs / 1000, deltaMs)
      else if (this.phase === 'won') this.updateWonBowArrows(deltaMs / 1000, deltaMs)
    }
    this.render()
    this.emitSnapshot(false)
    this.frameId = requestAnimationFrame(this.tick)
  }

  private advanceKnifeSpiderTutorial(deltaMs: number): void {
    if (this.knifeSpiderTutorialPhase !== 'resuming') return
    this.knifeSpiderTutorialResumeMs += deltaMs
    const durationMs = tuningValue(
      this.enemyDefinitions.get('spider-knife'),
      'tutorialResumeMs',
      900,
    )
    if (this.knifeSpiderTutorialResumeMs < durationMs) return
    this.knifeSpiderTutorialPhase = 'complete'
    this.knifeSpiderTutorialResumeMs = durationMs
  }

  private knifeSpiderTutorialTimeScale(): number {
    const definition = this.enemyDefinitions.get('spider-knife')
    if (!definition || !isKnifeSpiderV2Definition(definition)) return 1
    if (this.knifeSpiderTutorialPhase === 'frozen') return 0
    if (this.knifeSpiderTutorialPhase === 'resuming') {
      const durationMs = Math.max(1, tuningValue(definition, 'tutorialResumeMs', 900))
      const progress = clamp(this.knifeSpiderTutorialResumeMs / durationMs, 0, 1)
      return 0.08 + (1 - Math.cos(progress * Math.PI)) * 0.46
    }
    if (this.knifeSpiderTutorialPhase !== 'slowing') return 1
    const spider = this.enemies.find(enemy => enemy.knifeSpiderV2?.attackMode === 'leap')
    if (!spider) return 1
    const distance = Math.sqrt(distanceSquared(spider.position, this.player.position))
    const freezeDistance = tuningValue(definition, 'tutorialFreezeDistance', 112)
    const slowDistance = Math.max(
      freezeDistance + 1,
      tuningValue(definition, 'tutorialSlowDistance', 330),
    )
    const progress = clamp((slowDistance - distance) / (slowDistance - freezeDistance), 0, 1)
    return 1 - progress * 0.9
  }

  private update(deltaSeconds: number, deltaMs: number): void {
    this.roomElapsedMs += deltaMs
    this.player.invulnerableMs = Math.max(0, this.player.invulnerableMs - deltaMs)
    this.player.rootMs = Math.max(0, this.player.rootMs - deltaMs)
    this.player.recoveryMs = Math.max(0, this.player.recoveryMs - deltaMs)
    this.player.parryMs = Math.max(0, this.player.parryMs - deltaMs)
    if (this.player.parryMs <= 0) {
      this.activeParryCollider = null
      this.activeParryAttack = null
    }
    this.player.armorMultiplierMs = Math.max(0, this.player.armorMultiplierMs - deltaMs)
    if (this.player.armorMultiplierMs <= 0) this.player.armorMultiplier = 1
    for (const state of this.weaponStates.values()) {
      state.recoveryMs = Math.max(0, state.recoveryMs - deltaMs)
      state.perfectTimingMs = Math.max(0, state.perfectTimingMs - deltaMs)
      state.fatigueMs = Math.max(0, state.fatigueMs - deltaMs)
      this.expirePendingUnterhau(state)
    }
    this.updateHeldWeaponMechanics(deltaMs)
    this.updateBowHeldMechanics(deltaMs)
    this.updateSpiderKnifeWriggle()
    this.updateDualSenseTelegraphs()
    this.updateDelayedAttacks(deltaMs)
    this.updateDelayedRecoveries(deltaMs)
    this.updateSwordAdvance(deltaSeconds)
    this.updatePlayer(deltaSeconds)
    this.flushBowLandingAttacks()
    this.updateTurrets(deltaSeconds, deltaMs)
    this.updateProjectiles(deltaSeconds, deltaMs)
    this.updateFallingArrows(deltaMs)
    this.updateSwarmSpawner()
    const enemyPositionsBeforeMovement = new Map(
      this.enemies.map(enemy => [enemy.id, { ...enemy.position }]),
    )
    this.updateEnemies(deltaSeconds, deltaMs)
    for (const enemy of this.enemies) {
      const previous = enemyPositionsBeforeMovement.get(enemy.id)
      enemy.velocity = previous
        ? {
            x: (enemy.position.x - previous.x) / Math.max(EPSILON, deltaSeconds),
            y: (enemy.position.y - previous.y) / Math.max(EPSILON, deltaSeconds),
          }
        : { x: 0, y: 0 }
    }
    this.updateEmbeddedArrows()
    this.updateArrowChemicalPools()
    this.updateHoleStrikes()
    this.updateZoneAttacks()
    this.updateActiveAreas(deltaMs)
    this.updateHazards(deltaSeconds)
    this.updateMentalHealth(deltaSeconds)
    this.updateStamina(deltaSeconds)
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
      this.expirePendingUnterhau(state)
    }
    this.updateHeldWeaponMechanics(deltaMs)
    this.updateBowHeldMechanics(deltaMs)
    this.updateDualSenseTelegraphs()
    this.updateDelayedAttacks(deltaMs)
    this.updateDelayedRecoveries(deltaMs)
    this.updateSwordAdvance(deltaSeconds)
    this.updatePlayer(deltaSeconds)
    this.flushBowLandingAttacks()
    this.updateProjectiles(deltaSeconds, deltaMs)
    this.updateFallingArrows(deltaMs)
    this.updateEmbeddedArrows()
    this.updateArrowChemicalPools()
    this.updateActiveAreas(deltaMs)
    this.updateStamina(deltaSeconds)
    this.syncContinuationFeedback()
    this.effects.forEach(effect => { effect.remainingMs -= deltaMs })
    this.effects = this.effects.filter(effect => effect.remainingMs > 0)
    this.traces.forEach(trace => { trace.remainingMs -= deltaMs })
    this.traces = this.traces.filter(trace => trace.remainingMs > 0)
    this.pointerDeltaX = 0
  }

  /** The victory overlay freezes combat, but every already-loosed arrow still finishes its flight. */
  private updateWonBowArrows(deltaSeconds: number, deltaMs: number): void {
    if (this.projectiles.some(projectile => projectile.persistentArrow)) {
      this.updateProjectiles(deltaSeconds, deltaMs)
    }
    if (this.fallingArrows.length > 0) this.updateFallingArrows(deltaMs)
    if (this.embeddedArrows.length > 0) {
      this.updateEmbeddedArrows()
      this.updateArrowChemicalPools()
    }
    this.effects.forEach(effect => { effect.remainingMs -= deltaMs })
    this.effects = this.effects.filter(effect => effect.remainingMs > 0)
  }

  private updateDelayedAttacks(deltaMs: number): void {
    const ready: RuntimeDelayedAttack[] = []
    for (const delayed of this.delayedAttacks) {
      if (delayed.waitForBowDashWeaponId
        && this.activeDash?.weaponId === delayed.waitForBowDashWeaponId) continue
      delayed.remainingMs = Math.max(0, delayed.remainingMs - deltaMs)
      if (delayed.remainingMs <= 0) ready.push(delayed)
    }
    const readySet = new Set(ready)
    this.delayedAttacks = this.delayedAttacks.filter(delayed => !readySet.has(delayed))
    if (!this.canUseRoomActions()) return
    for (const delayed of ready) this.executeDelayedAttack(delayed)
  }

  private executeDelayedAttack(delayed: RuntimeDelayedAttack): void {
    if (delayed.attack.behavior === 'swordFollowUp') this.executeSwordFollowUp(delayed)
    else this.executeAttack(
      delayed.attack,
      this.resolveDelayedAttackDirection(delayed),
      delayed.context,
    )
    if (delayed.attack.behavior !== 'swordFollowUp') return
    const state = this.weaponState(delayed.context.weapon)
    state.unterhauDueAtMs = 0
    state.unterhauTargetId = null
    state.unterhauTargetPosition = null
    state.unterhauPrimed = false
  }

  /** Fires a queued «Ответ» from the first post-landing position, never the final airborne frame. */
  private flushBowLandingAttacks(): void {
    if (!this.canUseRoomActions()) return
    const ready = this.delayedAttacks.filter(delayed => (
      delayed.waitForBowDashWeaponId !== undefined
      && this.activeDash?.weaponId !== delayed.waitForBowDashWeaponId
    ))
    if (ready.length === 0) return
    const readySet = new Set(ready)
    this.delayedAttacks = this.delayedAttacks.filter(delayed => !readySet.has(delayed))
    for (const delayed of ready) this.executeDelayedAttack(delayed)
  }

  private resolveDelayedAttackDirection(delayed: RuntimeDelayedAttack): LastChancesVector {
    if (delayed.attack.behavior === 'swordFollowUp'
      || delayed.waitForBowDashWeaponId !== undefined) {
      return normalize(this.player.aim, delayed.direction)
    }
    return delayed.direction
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

  private poleVaultDistanceAt(dash: RuntimeDash, elapsedMs: number): number {
    const vault = dash.poleVault
    if (!vault) return 0
    const runEnd = vault.runMs
    const plantEnd = runEnd + vault.plantMs
    const totalMs = Math.max(
      1,
      runEnd + vault.plantMs + vault.riseMs + vault.flightMs + vault.landMs,
    )
    const clampedMs = clamp(elapsedMs, 0, totalMs)
    const runDistance = dash.attack.range * vault.runDistanceRatio
    const smooth = (progress: number): number => {
      const value = clamp(progress, 0, 1)
      return value * value * (3 - 2 * value)
    }
    if (clampedMs <= runEnd) {
      return runDistance * smooth(clampedMs / Math.max(1, runEnd))
    }
    if (clampedMs <= plantEnd) return runDistance
    const flightProgress = (clampedMs - plantEnd) / Math.max(1, totalMs - plantEnd)
    return runDistance + (dash.attack.range - runDistance) * smooth(flightProgress)
  }

  private poleVaultLiftRadii(dash: RuntimeDash): number {
    const vault = dash.poleVault
    if (!vault) return 0
    const airStart = vault.runMs + vault.plantMs
    const riseEnd = airStart + vault.riseMs
    const flightEnd = riseEnd + vault.flightMs
    const total = flightEnd + vault.landMs
    const elapsed = clamp(dash.elapsedMs, 0, total)
    const height = Math.max(0, tuningValue(dash.attack, 'trajectoryHeightRadii', 4.2))
    if (elapsed <= airStart) return 0
    if (elapsed <= riseEnd) {
      return height * Math.sin((elapsed - airStart) / Math.max(1, vault.riseMs) * Math.PI / 2)
    }
    if (elapsed <= flightEnd) {
      const progress = (elapsed - riseEnd) / Math.max(1, vault.flightMs)
      return height * (1 - progress * progress * 0.7)
    }
    return height * 0.3 * (
      1 - (elapsed - flightEnd) / Math.max(1, vault.landMs)
    )
  }

  private bowDashLiftRadii(dash: RuntimeDash): number {
    if (dash.attack.behavior !== 'bowDodge' && dash.attack.behavior !== 'bowJump') return 0
    const progress = clamp(
      dash.elapsedMs / Math.max(1, dash.attack.durationMs),
      0,
      1,
    )
    if (dash.attack.behavior === 'bowDodge') {
      return Math.sin(progress * Math.PI) * 0.38
    }
    const peakRadii = 1.15
    const inheritedLift = clamp(dash.bowLiftStartRadii ?? 0, 0, peakRadii)
    return progress <= 0.5
      ? inheritedLift + (peakRadii - inheritedLift) * Math.sin(progress * Math.PI)
      : peakRadii * Math.sin(progress * Math.PI)
  }

  private updatePoleVault(dash: RuntimeDash, deltaMs: number): number {
    const vault = dash.poleVault
    if (!vault) return 0
    const previousDistance = this.poleVaultDistanceAt(dash, dash.elapsedMs - deltaMs)
    const currentDistance = this.poleVaultDistanceAt(dash, dash.elapsedMs)
    const airStart = vault.runMs + vault.plantMs
    const totalMs = airStart + vault.riseMs + vault.flightMs + vault.landMs
    if (dash.elapsedMs > airStart && dash.elapsedMs < totalMs) {
      this.player.invulnerableMs = Math.max(this.player.invulnerableMs, deltaMs + 80)
    }
    return Math.max(0, currentDistance - previousDistance)
  }

  private updatePlayer(deltaSeconds: number): void {
    const aim = this.resolveAim()
    if (vectorLength(aim) > this.config.input.aimDeadZone) this.player.aim = normalize(aim, this.player.aim)
    if (this.knifeSpiderTutorialPhase === 'slowing'
      || this.knifeSpiderTutorialPhase === 'frozen') {
      this.movementVelocity = { x: 0, y: 0 }
      return
    }
    if (this.activeDash) {
      // A dash owns the body completely. Do not resume a stale pre-dash walking vector when it
      // ends, especially if its input was released while the dash branch was active.
      this.movementVelocity = { x: 0, y: 0 }
      const deltaMs = deltaSeconds * 1000
      this.activeDash.trailAccumulatorMs += deltaMs
      this.activeDash.elapsedMs += deltaMs
      if (this.activeDash.ram) this.updateBreakthrough(this.activeDash, deltaMs)
      const dashStartDelayMs = tuningValue(this.activeDash.attack, 'dashStartDelayMs', 0)
      // A breakthrough spends time, not a distance budget, so it is never clamped by one.
      const travel = this.activeDash.ram
        ? this.activeDash.speed * deltaSeconds
        : this.activeDash.poleVault
          ? this.updatePoleVault(this.activeDash, deltaMs)
          : this.activeDash.elapsedMs > dashStartDelayMs
          ? Math.min(this.activeDash.remainingDistance, this.activeDash.speed * deltaSeconds)
          : 0
      const dashStart = { ...this.player.position }
      const vaultAirStart = this.activeDash.poleVault
        ? this.activeDash.poleVault.runMs + this.activeDash.poleVault.plantMs
        : Number.POSITIVE_INFINITY
      if (this.activeDash.poleVault && this.activeDash.elapsedMs > vaultAirStart) {
        const distance = this.poleVaultDistanceAt(this.activeDash, this.activeDash.elapsedMs)
        const arena = this.currentNode?.arena
        this.player.position = {
          x: arena
            ? clamp(
                this.activeDash.origin.x + this.activeDash.direction.x * distance,
                this.config.player.radius,
                arena.width - this.config.player.radius,
              )
            : this.activeDash.origin.x + this.activeDash.direction.x * distance,
          y: arena
            ? clamp(
                this.activeDash.origin.y + this.activeDash.direction.y * distance,
                this.config.player.radius,
                arena.height - this.config.player.radius,
              )
            : this.activeDash.origin.y + this.activeDash.direction.y * distance,
        }
      } else {
        this.moveCircle(this.player.position, {
          x: this.activeDash.direction.x * travel,
          y: this.activeDash.direction.y * travel,
        }, this.config.player.radius)
      }
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
          damage: Math.max(
            0,
            tuningValue(this.activeDash.attack, 'chemicalTrailDamage', 0),
          ),
          range: Math.max(
            tuningValue(this.activeDash.attack, 'chemicalTrailMinimumRadius', 28),
            this.activeDash.attack.radius
              * tuningValue(this.activeDash.attack, 'chemicalTrailRadiusMultiplier', 1.8),
          ),
          radius: tuningValue(this.activeDash.attack, 'chemicalTrailColliderRadius', 8),
          arcDegrees: 360,
          durationMs: tuningValue(this.activeDash.attack, 'chemicalTrailDurationMs', 900),
          lingerMs: tuningValue(this.activeDash.attack, 'chemicalTrailLingerMs', 500),
          pierce: Math.max(
            0,
            Math.round(tuningValue(this.activeDash.attack, 'chemicalTrailPierce', 20)),
          ),
          knockback: Math.max(
            0,
            tuningValue(this.activeDash.attack, 'chemicalTrailKnockback', 0),
          ),
          collider: {
            shape: 'circle',
            traceMs: 850,
            followsPlayer: false,
          },
          hitEffects: chemicalEffects,
        }, this.activeDash.direction, this.activeDash.weaponId, this.activeDash.hand,
        null, false, this.activeDash.gesture)
      }
      if (!this.activeDash.ram) this.activeDash.remainingDistance -= travel
      for (const enemy of this.enemies) {
        if (this.activeDash.remainingHits <= 0) break
        if (enemy.state === 'dead') continue
        const isFlurry = this.activeDash.attack.behavior === 'katanaFlurry'
        // A breakthrough can run over the same body several times across two seconds, so it
        // shares the flurry's interval-gated re-hits without sharing its dodge rule.
        const repeatsHits = isFlurry || this.activeDash.ram !== undefined
        const previous = this.activeDash.hitRecords.get(enemy.id)
        const repeatHits = Math.max(1, this.activeDash.attack.repeatHits ?? 1)
        const repeatInterval = Math.max(1, this.activeDash.attack.repeatIntervalMs ?? 120)
        if (repeatsHits) {
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
        if (!repeatsHits) this.activeDash.hitIds.add(enemy.id)
        this.activeDash.remainingHits -= 1
        if (isFlurry
          && (enemy.definition.dodge ?? 0)
            >= tuningValue(this.activeDash.attack, 'dodgeThreshold', 0.25)
          && nextHit % Math.max(
            1,
            Math.round(tuningValue(this.activeDash.attack, 'dodgeEveryHits', 2)),
          ) === 0) continue
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
      const dashFinished = this.activeDash.ram
        ? this.activeDash.ram.releasedAtMs !== null
          && this.activeDash.speed <= tuningValue(this.activeDash.attack, 'stopSpeed', 8)
        : this.activeDash.remainingDistance <= EPSILON
      if (dashFinished) {
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

    if (this.player.rootMs > 0 || this.player.recoveryMs > 0 || this.bowChannels.size > 0) {
      // Root/recovery are combat locks, not released movement input: they stop immediately.
      this.movementVelocity = { x: 0, y: 0 }
    } else {
      const movement = this.resolveMovement()
      const speed = this.effectivePlayerStats().moveSpeed
      const inputMagnitude = vectorLength(movement)
      const currentMagnitude = vectorLength(this.movementVelocity)
      const targetMagnitude = inputMagnitude * speed
      if (inputMagnitude > EPSILON) {
        const transitionMs = targetMagnitude >= currentMagnitude
          ? this.config.player.accelerationMs
          : this.config.player.decelerationMs
        const nextMagnitude = moveNumberTowards(
          currentMagnitude,
          targetMagnitude,
          speed * deltaSeconds * 1000 / transitionMs,
        )
        // Turning has no authored inertia: a fresh direction takes control immediately even
        // during braking, matching the original prototype correction request.
        const direction = normalize(movement)
        this.movementVelocity = {
          x: direction.x * nextMagnitude,
          y: direction.y * nextMagnitude,
        }
      } else {
        const nextMagnitude = moveNumberTowards(
          currentMagnitude,
          0,
          speed * deltaSeconds * 1000 / this.config.player.decelerationMs,
        )
        if (nextMagnitude <= EPSILON) {
          this.movementVelocity = { x: 0, y: 0 }
        } else {
          const share = currentMagnitude > EPSILON ? nextMagnitude / currentMagnitude : 0
          this.movementVelocity = {
            x: this.movementVelocity.x * share,
            y: this.movementVelocity.y * share,
          }
        }
      }
    }
    this.moveCircle(this.player.position, {
      x: this.movementVelocity.x * deltaSeconds,
      y: this.movementVelocity.y * deltaSeconds,
    }, this.config.player.radius, { wallSlide: true })
  }

  /**
   * One frame of «Прорыв». The run opens at half the player's base speed, snaps to full over
   * `rampToBaseMs`, then keeps climbing to double over `rampToMaxMs` — so holding longer is
   * rewarded rather than merely tolerated. It lasts exactly as long as the button is held
   * (capped at `maxRunMs`); releasing does not stop it, it coasts down over `decelerationMs`.
   */
  private updateBreakthrough(dash: RuntimeDash, deltaMs: number): void {
    const ram = dash.ram
    if (!ram) return
    const attack = dash.attack
    const base = this.effectivePlayerStats().moveSpeed
    const startMultiplier = tuningValue(attack, 'startSpeedMultiplier', 0.5)
    const peakMultiplier = tuningValue(attack, 'peakSpeedMultiplier', 2)
    const rampToBaseMs = Math.max(1, tuningValue(attack, 'rampToBaseMs', 250))
    const rampToMaxMs = Math.max(1, tuningValue(attack, 'rampToMaxMs', 1750))
    const maxRunMs = Math.max(0, tuningValue(attack, 'maxRunMs', 2000))

    // Steering is what makes this a charge rather than a dash: `player.aim` was refreshed from
    // the pointer/right stick at the top of updatePlayer, so the run simply follows the cursor.
    dash.direction = normalize(this.player.aim, dash.direction)

    if (ram.releasedAtMs === null) {
      ram.runMs += deltaMs
      const held = Math.min(ram.runMs, maxRunMs)
      ram.speed = base * (held <= rampToBaseMs
        ? startMultiplier + (1 - startMultiplier) * (held / rampToBaseMs)
        : 1 + (peakMultiplier - 1) * Math.min(1, (held - rampToBaseMs) / rampToMaxMs))
      // 1 stamina per 0.1 s, debited directly instead of through settleStaminaForAttack, which
      // would refund combo/hand-alternation stamina on every tick and reset the chain stamps.
      const tickMs = Math.max(1, tuningValue(attack, 'staminaPerTickMs', 100))
      const tickCost = Math.max(0, tuningValue(attack, 'staminaPerTick', 1))
        * this.staminaCostMultiplier()
      ram.staminaAccumulatorMs += deltaMs
      while (ram.staminaAccumulatorMs >= tickMs) {
        ram.staminaAccumulatorMs -= tickMs
        this.player.stamina = Math.max(0, this.player.stamina - tickCost)
      }
      if (ram.runMs >= maxRunMs) {
        ram.releasedAtMs = this.elapsedMs
      } else if (tickCost > 0 && this.player.stamina <= 0) {
        ram.releasedAtMs = this.elapsedMs
        this.refuseForStamina(dash.hand)
      }
    } else {
      const decelerationMs = Math.max(1, tuningValue(attack, 'decelerationMs', 300))
      ram.speed = Math.max(0, ram.speed - base * peakMultiplier * (deltaMs / decelerationMs))
    }
    dash.speed = ram.speed

    // Escalation. There is no camera or audio in 99LC, so "more epic the longer it runs" is
    // carried entirely by the trail: longer streaks every frame, and a shockwave each time the
    // run crosses a whole speed tier.
    const tier = Math.floor(ram.speed / base)
    if (tier > ram.tierCued) {
      ram.tierCued = tier
      this.addEffect(
        'shock',
        { ...attack, durationMs: 320, range: attack.radius * (2 + tier) },
        dash.direction,
      )
    }
    ram.streakAccumulatorMs += deltaMs
    const streakIntervalMs = Math.max(16, tuningValue(attack, 'streakIntervalMs', 90))
    if (ram.streakAccumulatorMs >= streakIntervalMs) {
      ram.streakAccumulatorMs = 0
      const intensity = this.breakthroughIntensity(dash)
      this.addEffect(
        'dash',
        { ...attack, durationMs: 220, range: attack.range * (0.4 + intensity) },
        dash.direction,
      )
    }
  }

  /**
   * Watches the still-down second press of a double-tap and turns it into «Прорыв» once it has
   * outlasted `breakthroughHoldMs` — a deliberately snappier gate (~200 ms) than the global
   * `input.holdMs`, so the freeze in the «Прокол» pose reads as one beat rather than a pause.
   * The same poll returns the run when the button comes up.
   */
  private updateBreakthroughInput(
    hand: LastChancesHand,
    input: LastChancesGestureInputSnapshot,
    now: number,
  ): void {
    const running = this.activeDash?.ram && this.activeDash.hand === hand
      ? this.activeDash
      : null
    if (running?.ram && !input.pressed && running.ram.releasedAtMs === null) {
      running.ram.releasedAtMs = this.elapsedMs
    }
    if (running) return
    const immediate = this.immediateHandInput[hand]
    if (immediate.breakthroughStarted) return
    // Each scheme expresses "the second press is still down" differently: DeepList has a literal
    // second tap, mylorik a continuation press, DualSense a held trigger past its gate.
    const holdingSecond = this.controlSchemeValue === 'dualsense'
      ? (() => {
          const trigger = this.dualSenseControls.snapshot(runtimeHandToPhysicalCluster(hand), now)
          return trigger.active && trigger.nodeId === 'doubleTapHold'
        })()
      : this.controlSchemeValue === 'mylorik'
        ? input.pressed && input.candidateGesture === 'doubleTapHold'
        : input.pressed && input.sequence === 'secondTap'
    if (!holdingSecond) return
    const gateMs = tuningValue(
      this.weapons.get(hand)?.attacks.doubleTapHold,
      'breakthroughHoldMs',
      200,
    )
    if (input.heldMs < gateMs) {
      // Freeze the interrupted thrust at maximum extension. Its hitbox and blue trace therefore
      // remain the same object right up to the frame in which the run takes ownership.
      for (const area of this.activeAreas) {
        if (area.hand === hand && area.attack.behavior === 'spearPierce') {
          area.remainingMs = Math.max(area.remainingMs, area.totalMs / 2)
        }
      }
      return
    }
    immediate.breakthroughStarted = this.startBreakthrough(hand)
  }

  /** How far into the 0.5×…2× ramp the run currently is, for the escalating visuals. */
  private breakthroughIntensity(dash: RuntimeDash): number {
    const base = Math.max(1, this.effectivePlayerStats().moveSpeed)
    const startMultiplier = tuningValue(dash.attack, 'startSpeedMultiplier', 0.5)
    const peakMultiplier = tuningValue(dash.attack, 'peakSpeedMultiplier', 2)
    const span = Math.max(EPSILON, peakMultiplier - startMultiplier)
    return clamp((dash.speed / base - startMultiplier) / span, 0, 1)
  }

  /**
   * The «Морф»: the «Прокол» still standing in front of the player is interrupted and, without
   * finishing, becomes the run. Routed through `performAttack` rather than straight to
   * `performDash` so the breakthrough still pays stamina, stamps its cooldown, books recovery
   * and answers the move-quest gate exactly like a normally dispatched action.
   */
  private startBreakthrough(hand: LastChancesHand): boolean {
    const weapon = this.weapons.get(hand)
    if (!weapon || weapon.attacks.doubleTapHold.behavior !== 'spearBreakthrough') return false
    this.morphIntoPierce(weapon, hand)
    this.closePierceMash(hand)
    this.performAttack({
      hand,
      gesture: 'doubleTapHold',
      atMs: this.elapsedMs,
      heldMs: tuningValue(weapon.attacks.doubleTapHold, 'breakthroughHoldMs', 200),
      firstHoldMs: 0,
    })
    return this.activeDash?.ram !== undefined && this.activeDash.hand === hand
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
      if (projectile.persistentArrow && projectile.source === 'player' && this.currentNode) {
        this.updateLongbowProjectile(projectile, deltaSeconds, deltaMs)
        continue
      }
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
          projectile.remainingDistance = Math.max(
            projectile.remainingDistance,
            Math.max(
              0,
              tuningValue(reflectingSpin.attack, 'reflectedProjectileMinimumRange', 260),
            ),
          )
          projectile.remainingHits = Math.max(
            1,
            Math.round(tuningValue(
              reflectingSpin.attack,
              'reflectedProjectilePierce',
              reflectingSpin.attack.pierce,
            )) + 1,
          )
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
            projectile.remainingDistance = Math.max(
              projectile.remainingDistance,
              Math.max(
                0,
                tuningValue(this.activeParryAttack ?? undefined, 'reflectedProjectileMinimumRange', 220),
              ),
            )
            projectile.remainingHits = Math.max(
              1,
              Math.round(tuningValue(
                this.activeParryAttack ?? undefined,
                'reflectedProjectilePierce',
                0,
              )) + 1,
            )
            projectile.hitIds.clear()
            projectile.sourceName = 'Parried projectile'
            continue
          }
          projectile.remainingHits = 0
          const landed = this.damagePlayer(projectile.damage, projectile.sourceName)
          if (landed && projectile.knockback > 0 && this.phase === 'playing') {
            const direction = normalize(projectile.velocity)
            this.moveCircle(
              this.player.position,
              {
                x: direction.x * projectile.knockback,
                y: direction.y * projectile.knockback,
              },
              this.config.player.radius,
            )
          }
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
        if (isSpearReleaseBehavior(attack.behavior)
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

  /**
   * Longbow arrows use continuous time-of-impact instead of the generic frame-end collision.
   * That gives the permanent decal the real first-contact point and supplies the surface normal
   * needed by Множественный залп's single authored ricochet.
   */
  private updateLongbowProjectile(
    projectile: RuntimeProjectile,
    deltaSeconds: number,
    deltaMs: number,
  ): void {
    const node = this.currentNode
    if (!node) return
    const start = { ...projectile.position }
    const speed = vectorLength(projectile.velocity)
    const lifetimeFraction = deltaMs > 0
      ? clamp(projectile.remainingMs / deltaMs, 0, 1)
      : 1
    const activeFrameMs = Math.max(0, deltaMs * lifetimeFraction)
    const requestedDistance = speed * deltaSeconds * lifetimeFraction
    const stepDistance = Math.max(0, Math.min(projectile.remainingDistance, requestedDistance))
    const direction = normalize(projectile.velocity)
    const end = {
      x: start.x + direction.x * stepDistance,
      y: start.y + direction.y * stepDistance,
    }
    const sweptCollider: LastChancesRuntimeCollider = {
      shape: 'capsule',
      start,
      end,
      radius: projectile.radius,
    }
    if (projectile.attack) this.addColliderTrace(sweptCollider, projectile.attack)

    const surfaceImpact = projectile.attack?.collider?.passesThroughWalls === true
      ? null
      : sweepLastChancesCircleAgainstArena(start, end, projectile.radius, node.arena)
    let targetImpact: {
      t: number
      point: LastChancesVector
      enemy: RuntimeEnemy
    } | null = null
    for (const enemy of this.enemies) {
      if (enemy.state === 'dead' || projectile.hitIds.has(enemy.id)) continue
      const impact = sweepLastChancesCircleAgainstCircle(
        start,
        end,
        projectile.radius,
        enemy.position,
        enemy.definition.radius,
      )
      if (!impact || (targetImpact && impact.t >= targetImpact.t - EPSILON)) continue
      targetImpact = {
        t: impact.t,
        point: impact.point,
        enemy,
      }
    }

    if (targetImpact
      && (!surfaceImpact || targetImpact.t < surfaceImpact.t - EPSILON)) {
      const traveled = vectorLength({
        x: targetImpact.point.x - start.x,
        y: targetImpact.point.y - start.y,
      })
      projectile.position = { ...targetImpact.point }
      projectile.remainingDistance = Math.max(0, projectile.remainingDistance - traveled)
      const elapsedAtImpactMs = requestedDistance > EPSILON
        ? activeFrameMs * clamp(traveled / requestedDistance, 0, 1)
        : 0
      projectile.remainingMs = Math.max(
        0,
        projectile.remainingMs - elapsedAtImpactMs,
      )
      projectile.hitIds.add(targetImpact.enemy.id)
      projectile.remainingHits = 0
      const attack = projectile.attack ?? {
        name: projectile.sourceName,
        kind: 'projectile',
        behavior: 'bowShot',
        damage: projectile.damage,
        cooldownMs: 0,
        range: projectile.remainingDistance,
        radius: projectile.radius,
        arcDegrees: 0,
        durationMs: projectile.remainingMs,
        projectileSpeed: speed,
        pierce: 0,
        knockback: projectile.knockback,
        color: projectile.color,
      }
      this.tryParryEnemy(targetImpact.enemy, attack)
      this.damageEnemy(targetImpact.enemy, attack, projectile.knockback, direction, {
        weaponId: projectile.weaponId,
        hand: projectile.hand,
        gesture: projectile.gesture,
        storedDot: projectile.storedDot,
      })
      this.embedArrow({
        id: projectile.id,
        position: targetImpact.point,
        direction,
        attachment: 'enemy',
        enemy: targetImpact.enemy,
        color: projectile.color,
        chemical: projectile.chemicalArrow === true,
      })
      this.effects.push({
        kind: 'hit',
        position: { ...targetImpact.point },
        direction,
        range: projectile.radius * 4,
        radius: projectile.radius * 2,
        arcDegrees: 32,
        color: projectile.color,
        remainingMs: 180,
        totalMs: 180,
      })
      return
    }

    if (surfaceImpact) {
      const traveled = vectorLength({
        x: surfaceImpact.point.x - start.x,
        y: surfaceImpact.point.y - start.y,
      })
      projectile.remainingDistance = Math.max(0, projectile.remainingDistance - traveled)
      const elapsedAtImpactMs = requestedDistance > EPSILON
        ? activeFrameMs * clamp(traveled / requestedDistance, 0, 1)
        : 0
      projectile.remainingMs = Math.max(
        0,
        projectile.remainingMs - elapsedAtImpactMs,
      )
      if ((projectile.ricochetsRemaining ?? 0) > 0) {
        const reflected = reflectLastChancesVector(projectile.velocity, surfaceImpact.normal)
        const reflectedDirection = normalize(reflected, {
          x: -direction.x,
          y: -direction.y,
        })
        projectile.velocity = reflected
        projectile.ricochetsRemaining = Math.max(0, (projectile.ricochetsRemaining ?? 0) - 1)
        projectile.position = {
          x: surfaceImpact.point.x + reflectedDirection.x * 0.08,
          y: surfaceImpact.point.y + reflectedDirection.y * 0.08,
        }
        this.effects.push({
          kind: 'shock',
          position: { ...surfaceImpact.point },
          direction: reflectedDirection,
          range: 18,
          radius: 8,
          arcDegrees: 54,
          color: '#ffd76a',
          remainingMs: 180,
          totalMs: 180,
        })
        if (projectile.remainingDistance <= EPSILON || projectile.remainingMs <= EPSILON) {
          projectile.position = { ...surfaceImpact.point }
          projectile.remainingHits = 0
          this.embedArrow({
            id: projectile.id,
            position: surfaceImpact.point,
            direction,
            attachment: surfaceImpact.kind,
            enemy: null,
            color: projectile.color,
            chemical: projectile.chemicalArrow === true,
          })
        }
        return
      }
      projectile.position = { ...surfaceImpact.point }
      projectile.remainingHits = 0
      this.embedArrow({
        id: projectile.id,
        position: surfaceImpact.point,
        direction,
        attachment: surfaceImpact.kind,
        enemy: null,
        color: projectile.color,
        chemical: projectile.chemicalArrow === true,
      })
      return
    }

    projectile.position = end
    projectile.remainingDistance = Math.max(0, projectile.remainingDistance - stepDistance)
    projectile.remainingMs = Math.max(0, projectile.remainingMs - activeFrameMs)
    if (projectile.remainingDistance > EPSILON && projectile.remainingMs > EPSILON) return
    projectile.remainingHits = 0
    this.embedArrow({
      id: projectile.id,
      position: end,
      direction,
      attachment: 'floor',
      enemy: null,
      color: projectile.color,
      chemical: projectile.chemicalArrow === true,
    })
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
      this.turretAlarmMs = Math.max(this.turretAlarmMs, node.turretAlarmHoldMs)
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
      const spawnOffset = Math.max(0, turret.definition.projectileSpawnOffset ?? 18)
      this.projectiles.push({
        id: this.nextProjectileId++,
        position: {
          x: turret.definition.position.x + direction.x * (radius + spawnOffset),
          y: turret.definition.position.y + direction.y * (radius + spawnOffset),
        },
        velocity: { x: direction.x * speed, y: direction.y * speed },
        radius,
        damage: turret.definition.damage,
        knockback: Math.max(0, turret.definition.projectileKnockback ?? 0),
        remainingDistance: turret.definition.visionRange,
        remainingMs: turret.definition.visionRange / speed * 1000,
        remainingHits: 1,
        hitIds: new Set(),
        color: turret.definition.color,
        source: 'enemy',
        sourceName: turret.definition.name,
        sourceId: turret.definition.id,
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
      velocity: { x: 0, y: 0 },
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
      knifeSpiderV2: null,
      invisibleWolf: null,
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
        enemy.hp = Math.max(0, enemy.hp - amount * this.ouroborosDamageMultiplier())
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

      // The wolf owns its whole lifecycle, visibility included, so it intercepts before the
      // shared notice/alert path instead of hooking in after it the way the spider does.
      if (enemy.invisibleWolf) {
        const startedAttack = this.updateInvisibleWolf(
          enemy,
          profile,
          toPlayer,
          distance,
          deltaSeconds,
          deltaMs,
          !queuedAttackerActive,
        )
        if (startedAttack) queuedAttackerActive = true
        continue
      }

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

      if (enemy.knifeSpiderV2) {
        this.updateKnifeSpiderV2(enemy, profile, toPlayer, distance, deltaSeconds, deltaMs)
        continue
      }

      const mayUseQueue = profile.role === 'creep'
        || profile.role === 'cockroach'
        || !queuedAttackerActive
      const startedAttack = this.updateStandardEnemy(
        enemy,
        profile,
        toPlayer,
        distance,
        deltaSeconds,
        deltaMs,
        mayUseQueue,
      )
      if (startedAttack && profile.role !== 'creep' && profile.role !== 'cockroach') {
        queuedAttackerActive = true
      }
    }
  }

  /**
   * The shared standard/elite path: resolve an attack already under way, otherwise close to the
   * preferred range and start one when the attack queue allows it. Returns whether this tick
   * started an attack so the caller can keep the one-attacker-at-a-time queue honest.
   */
  private updateStandardEnemy(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    toPlayer: LastChancesVector,
    distance: number,
    deltaSeconds: number,
    deltaMs: number,
    mayUseQueue: boolean,
  ): boolean {
    if (enemy.state === 'attacking') {
      if (!(profile.attackKind === 'leap' && enemy.lockedAttackDirection)) {
        enemy.facing = normalize(toPlayer, enemy.facing)
      }
      this.updateEnemyAttack(enemy, profile, deltaSeconds, deltaMs, distance)
      return false
    }

    enemy.facing = normalize(toPlayer, enemy.facing)
    if (distance <= profile.attackRange
      && enemy.attackCooldownMs <= 0
      && enemy.statuses.disarmMs <= 0
      && mayUseQueue) {
      this.startEnemyAttack(enemy, profile)
      return true
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
    return false
  }

  /**
   * How squarely the player's aim points at the wolf: 1 is dead ahead, −1 is directly behind.
   * The whole stalk cycle is written against this one number, so "turn around" is always the
   * answer to the wolf no matter where it is standing.
   */
  private invisibleWolfAimDot(enemy: RuntimeEnemy): number {
    const aim = normalize(this.player.aim, { x: 1, y: 0 })
    const toWolf = normalize({
      x: enemy.position.x - this.player.position.x,
      y: enemy.position.y - this.player.position.y,
    }, aim)
    return toWolf.x * aim.x + toWolf.y * aim.y
  }

  private moveInvisibleWolf(
    enemy: RuntimeEnemy,
    direction: LastChancesVector,
    deltaSeconds: number,
    speedMultiplier: number,
  ): void {
    const speed = enemy.definition.moveSpeed * speedMultiplier * enemy.statuses.slowMultiplier
    this.moveEnemy(enemy, {
      x: direction.x * speed * deltaSeconds,
      y: direction.y * speed * deltaSeconds,
    })
  }

  /** Walks the wolf toward the point behind the player's aim it wants to wait at. */
  private moveInvisibleWolfToStalkPost(
    enemy: RuntimeEnemy,
    wolf: RuntimeInvisibleWolf,
    deltaSeconds: number,
  ): void {
    const aim = normalize(this.player.aim, { x: 1, y: 0 })
    const angle = Math.atan2(aim.y, aim.x)
      + Math.PI
      + wolf.orbitDirection * tuningValue(enemy.definition, 'stalkWobbleRadians', 0.55)
    const radius = tuningValue(enemy.definition, 'stalkRadius', 260)
    const toPost = {
      x: this.player.position.x + Math.cos(angle) * radius - enemy.position.x,
      y: this.player.position.y + Math.sin(angle) * radius - enemy.position.y,
    }
    if (vectorLength(toPost) <= enemy.definition.radius) return
    this.moveInvisibleWolf(
      enemy,
      normalize(toPost, enemy.facing),
      deltaSeconds,
      tuningValue(enemy.definition, 'stalkSpeedMultiplier', 0.82),
    )
  }

  /**
   * The Invisible wolf hunts rather than patrols. It never enters `alerted` — it simply stops
   * being anywhere the player is looking, waits behind their aim, and commits to one telegraphed
   * pounce. Two rules keep it fair: turning to face it cancels an approach, and any landed hit
   * pins it on screen until the reveal expires. Returns whether it started an attack this tick.
   */
  private updateInvisibleWolf(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    toPlayer: LastChancesVector,
    distance: number,
    deltaSeconds: number,
    deltaMs: number,
    mayUseQueue: boolean,
  ): boolean {
    const wolf = enemy.invisibleWolf
    if (!wolf) return false
    wolf.decisionMs -= deltaMs
    wolf.rehideMs = Math.max(0, wolf.rehideMs - deltaMs)
    const rehideDelayMs = tuningValue(enemy.definition, 'rehideDelayMs', 1400)

    // A landed hit, a parry or a pin drops the wolf out of the hunt: while the reveal lasts it
    // fights as a plain elite and cannot vanish.
    if (enemy.revealedMs > 0 && wolf.phase !== 'lunging') {
      wolf.phase = 'exposed'
      wolf.patienceMs = 0
      if (enemy.state !== 'attacking') enemy.state = 'chasing'
    }

    if (wolf.phase === 'unaware') {
      const angle = Math.atan2(enemy.facing.y, enemy.facing.x)
        + deltaSeconds * (enemy.definition.idleTurnRadiansPerSecond ?? 0.28)
      enemy.facing = { x: Math.cos(angle), y: Math.sin(angle) }
      if (!this.enemyCanSeePlayer(enemy)) {
        enemy.state = 'idle'
        enemy.noticeMs = 0
        return false
      }
      enemy.state = 'noticing'
      enemy.noticeMs += deltaMs
      enemy.facing = normalize(toPlayer, enemy.facing)
      if (enemy.noticeMs >= enemy.definition.noticeMs) {
        // No `alerted` beat and no `!!` marker: the wolf does not announce that it has seen you.
        wolf.phase = 'stalking'
        wolf.patienceMs = 0
        enemy.state = 'idle'
        enemy.noticeMs = 0
      }
      return false
    }

    if (wolf.phase === 'exposed') {
      if (enemy.revealedMs <= 0 && enemy.state !== 'attacking') {
        wolf.phase = 'recovering'
        wolf.rehideMs = rehideDelayMs
      }
      return this.updateStandardEnemy(
        enemy, profile, toPlayer, distance, deltaSeconds, deltaMs, mayUseQueue,
      )
    }

    if (wolf.phase === 'lunging') {
      if (enemy.state === 'attacking') {
        this.updateStandardEnemy(
          enemy, profile, toPlayer, distance, deltaSeconds, deltaMs, mayUseQueue,
        )
        return false
      }
      // `finishEnemyAttack` leaves the wolf chasing, so the pounce is followed by a window where
      // it is visible and backing off — that window is the player's turn.
      wolf.phase = enemy.revealedMs > 0 ? 'exposed' : 'recovering'
      wolf.rehideMs = rehideDelayMs
    }

    if (wolf.phase === 'recovering') {
      enemy.state = 'chasing'
      enemy.facing = normalize(toPlayer, enemy.facing)
      if (distance < tuningValue(enemy.definition, 'stalkRadius', 260)) {
        this.moveInvisibleWolf(
          enemy,
          { x: -enemy.facing.x, y: -enemy.facing.y },
          deltaSeconds,
          tuningValue(enemy.definition, 'retreatSpeedMultiplier', 1.1),
        )
      }
      if (wolf.rehideMs <= 0 && enemy.revealedMs <= 0) {
        wolf.phase = 'stalking'
        wolf.patienceMs = 0
        enemy.state = 'idle'
      }
      return false
    }

    const aimDot = this.invisibleWolfAimDot(enemy)
    const frontDotAbort = tuningValue(enemy.definition, 'frontDotAbort', 0.25)

    if (wolf.phase === 'closing') {
      enemy.state = 'idle'
      enemy.facing = normalize(toPlayer, enemy.facing)
      if (aimDot > frontDotAbort) {
        // Turning toward the wolf always calls off the approach. This is the counterplay.
        wolf.phase = 'stalking'
        wolf.patienceMs = 0
        return false
      }
      if (distance <= profile.attackRange
        && enemy.attackCooldownMs <= 0
        && enemy.statuses.disarmMs <= 0
        && mayUseQueue) {
        wolf.phase = 'lunging'
        this.startEnemyAttack(enemy, profile)
        return true
      }
      const holdDistance = profile.attackRange
        * (enemy.definition.preferredAttackRangeRatio ?? 0.72)
      if (distance > holdDistance) {
        this.moveInvisibleWolf(
          enemy,
          enemy.facing,
          deltaSeconds,
          tuningValue(enemy.definition, 'closeSpeedMultiplier', 1.35),
        )
      }
      return false
    }

    enemy.state = 'idle'
    enemy.facing = normalize(toPlayer, enemy.facing)
    if (wolf.decisionMs <= 0) {
      const minimum = tuningValue(enemy.definition, 'orbitDirectionMinMs', 420)
      const maximum = Math.max(minimum, tuningValue(enemy.definition, 'orbitDirectionMaxMs', 1100))
      wolf.decisionMs = minimum + (maximum - minimum) * wolf.rng()
      // Only reroll the side it circles from when the player is looking its way; otherwise it
      // would abandon a good rear position for no reason.
      if (aimDot > frontDotAbort) wolf.orbitDirection = wolf.orbitDirection === 1 ? -1 : 1
    }
    this.moveInvisibleWolfToStalkPost(enemy, wolf, deltaSeconds)
    if (aimDot > frontDotAbort) {
      wolf.patienceMs = 0
    } else if (aimDot < tuningValue(enemy.definition, 'rearDotMaximum', -0.15)) {
      wolf.patienceMs += deltaMs
    }
    if (wolf.patienceMs >= tuningValue(enemy.definition, 'stalkPatienceMs', 900)
      && !this.attackPathBlocked(enemy.position, this.player.position)) {
      wolf.phase = 'closing'
    }
    return false
  }

  private updateKnifeSpiderV2(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    toPlayer: LastChancesVector,
    distance: number,
    deltaSeconds: number,
    deltaMs: number,
  ): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    if (spider.embedded) spider.embedded = false

    if (enemy.state === 'attacking' && spider.attackMode === 'leap') {
      this.updateKnifeSpiderV2Leap(enemy, profile, deltaSeconds, deltaMs)
      return
    }
    if (enemy.state === 'attacking' && spider.attackMode === 'strike') {
      this.moveKnifeSpiderOrbit(enemy, toPlayer, distance, deltaSeconds, deltaMs)
      enemy.attackWindupMs = Math.max(0, enemy.attackWindupMs - deltaMs)
      if (enemy.attackWindupMs > 0) return
      const strikeReach = tuningValue(enemy.definition, 'orbitStrikeRange', 76)
        + this.config.player.radius
      if (distance <= strikeReach) {
        if (this.playerParryCovers(enemy.position, enemy.definition.radius)) {
          this.consumeActiveParry()
          enemy.revealedMs = Math.max(
            enemy.revealedMs,
            this.config.combat.enemyRevealOnParryMs,
          )
        } else {
          this.damagePlayer(
            tuningValue(enemy.definition, 'orbitStrikeDamage', profile.attackDamage * 0.72),
            `${enemy.definition.name}: удар с орбиты`,
          )
        }
      }
      this.finishKnifeSpiderV2Strike(enemy)
      return
    }

    enemy.state = 'chasing'
    enemy.facing = normalize(toPlayer, enemy.facing)
    this.tryStartKnifeSpiderV2Evade(enemy, distance)
    if (spider.evadeMs > 0) {
      spider.evadeMs = Math.max(0, spider.evadeMs - deltaMs)
      const behindPlayer = {
        x: this.player.position.x
          - this.player.aim.x * tuningValue(enemy.definition, 'orbitDistance', 82)
          - enemy.position.x,
        y: this.player.position.y
          - this.player.aim.y * tuningValue(enemy.definition, 'orbitDistance', 82)
          - enemy.position.y,
      }
      const direction = normalize({
        x: spider.evadeDirection.x * 1.35 + normalize(behindPlayer).x * 0.42,
        y: spider.evadeDirection.y * 1.35 + normalize(behindPlayer).y * 0.42,
      }, spider.evadeDirection)
      enemy.facing = direction
      this.moveKnifeSpider(
        enemy,
        direction,
        deltaSeconds,
        tuningValue(enemy.definition, 'evadeSpeedMultiplier', 1.42),
      )
      return
    }

    const leapTriggerDistance = tuningValue(enemy.definition, 'leapTriggerDistance', 220)
    if (distance >= leapTriggerDistance
      && enemy.attackCooldownMs <= 0
      && this.knifeSpiderLeapPathClear(enemy)) {
      this.startKnifeSpiderV2Leap(enemy, profile)
      return
    }

    const orbitDistance = tuningValue(enemy.definition, 'orbitDistance', 82)
    if (distance > orbitDistance * 1.28) {
      const perpendicular = { x: -enemy.facing.y, y: enemy.facing.x }
      const zigzag = Math.sin(
        this.roomElapsedMs / Math.max(80, tuningValue(enemy.definition, 'zigzagPeriodMs', 260))
          + enemy.position.x * 0.019
          + enemy.position.y * 0.013,
      ) * tuningValue(enemy.definition, 'zigzagAmplitude', 0.92)
      const direction = normalize({
        x: enemy.facing.x + perpendicular.x * zigzag,
        y: enemy.facing.y + perpendicular.y * zigzag,
      }, enemy.facing)
      enemy.facing = direction
      this.moveKnifeSpider(enemy, direction, deltaSeconds, 1)
      return
    }

    this.moveKnifeSpiderOrbit(enemy, toPlayer, distance, deltaSeconds, deltaMs)
    if (enemy.attackCooldownMs <= 0) this.startKnifeSpiderV2Strike(enemy)
  }

  private moveKnifeSpider(
    enemy: RuntimeEnemy,
    direction: LastChancesVector,
    deltaSeconds: number,
    speedMultiplier: number,
  ): void {
    const slowMultiplier = enemy.statuses.slowMultiplier
    const speed = enemy.definition.moveSpeed
      * tuningValue(enemy.definition, 'v2MoveSpeedMultiplier', 1.65)
      * speedMultiplier
      * slowMultiplier
    this.moveEnemy(enemy, {
      x: direction.x * speed * deltaSeconds,
      y: direction.y * speed * deltaSeconds,
    })
  }

  private moveKnifeSpiderOrbit(
    enemy: RuntimeEnemy,
    toPlayer: LastChancesVector,
    distance: number,
    deltaSeconds: number,
    deltaMs: number,
  ): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    spider.decisionMs -= deltaMs
    if (spider.decisionMs <= 0) {
      if (spider.rng() < tuningValue(enemy.definition, 'orbitDirectionChangeChance', 0.72)) {
        spider.orbitDirection = spider.orbitDirection === 1 ? -1 : 1
      }
      const minimum = tuningValue(enemy.definition, 'orbitDirectionMinMs', 220)
      const maximum = Math.max(
        minimum,
        tuningValue(enemy.definition, 'orbitDirectionMaxMs', 680),
      )
      spider.decisionMs = minimum + (maximum - minimum) * spider.rng()
    }
    const radial = normalize(toPlayer, enemy.facing)
    const tangent = {
      x: -radial.y * spider.orbitDirection,
      y: radial.x * spider.orbitDirection,
    }
    const desiredDistance = tuningValue(enemy.definition, 'orbitDistance', 82)
    const radialCorrection = clamp(
      (distance - desiredDistance) / Math.max(1, desiredDistance),
      -0.78,
      0.78,
    )
    const direction = normalize({
      x: tangent.x + radial.x * radialCorrection * 1.35,
      y: tangent.y + radial.y * radialCorrection * 1.35,
    }, tangent)
    enemy.facing = radial
    this.moveKnifeSpider(
      enemy,
      direction,
      deltaSeconds,
      tuningValue(enemy.definition, 'orbitSpeedMultiplier', 1.08),
    )
  }

  private tryStartKnifeSpiderV2Evade(enemy: RuntimeEnemy, distance: number): void {
    const spider = enemy.knifeSpiderV2
    const attack = this.lastGesture
    if (!spider || !attack || attack.atMs <= spider.lastReactedAttackAtMs) return
    spider.lastReactedAttackAtMs = attack.atMs
    if (distance > tuningValue(enemy.definition, 'evadeDetectionRange', 330)
      || spider.rng() > tuningValue(enemy.definition, 'evadeChance', 0.68)) return
    const trajectory = normalize(this.player.aim)
    const offset = {
      x: enemy.position.x - this.player.position.x,
      y: enemy.position.y - this.player.position.y,
    }
    const side = trajectory.x * offset.y - trajectory.y * offset.x >= 0 ? 1 : -1
    const lateral = {
      x: -trajectory.y * side,
      y: trajectory.x * side,
    }
    const away = normalize(offset, lateral)
    spider.evadeDirection = normalize({
      x: lateral.x * 1.2 + away.x * 0.38,
      y: lateral.y * 1.2 + away.y * 0.38,
    }, lateral)
    spider.evadeMs = tuningValue(enemy.definition, 'evadeDurationMs', 245)
  }

  /**
   * The authored opening begins on an already-airborne Knife-spider. Put it at the start of the
   * slow-motion band on the nearest clear ray around the player, then launch immediately without
   * notice, chase or wind-up frames.
   */
  private startKnifeSpiderPrologue(): void {
    if (this.knifeSpiderTutorialPhase !== 'pending') return
    const enemy = this.enemies.find(candidate => candidate.knifeSpiderV2 !== null)
    if (!enemy || !this.currentNode) return
    const launchDistance = tuningValue(enemy.definition, 'tutorialSlowDistance', 330)
    const baseDirection = normalize({
      x: enemy.position.x - this.player.position.x,
      y: enemy.position.y - this.player.position.y,
    })
    const offsets = [
      0,
      -Math.PI / 8,
      Math.PI / 8,
      -Math.PI / 4,
      Math.PI / 4,
      -Math.PI * 3 / 8,
      Math.PI * 3 / 8,
      -Math.PI / 2,
      Math.PI / 2,
      Math.PI,
    ]
    const launchPosition = offsets
      .map(offset => rotateVector(baseDirection, offset))
      .map(direction => ({
        x: this.player.position.x + direction.x * launchDistance,
        y: this.player.position.y + direction.y * launchDistance,
      }))
      .find(position => (
        this.circleCanOccupy(position, enemy.definition.radius)
        && !this.currentNode!.arena.obstacles.some(obstacle => (
          segmentHitsObstacle(
            position,
            this.player.position,
            obstacle,
            enemy.definition.radius,
          )
        ))
      ))
    if (!launchPosition) return
    enemy.position = launchPosition
    const profile = this.enemyCombatProfile(enemy)
    this.startKnifeSpiderV2Leap(enemy, profile)
    enemy.lockedAttackDirection = normalize({
      x: this.player.position.x - enemy.position.x,
      y: this.player.position.y - enemy.position.y,
    }, enemy.facing)
    enemy.attackWindupMs = 0
    this.launchKnifeSpiderV2(enemy)
  }

  private knifeSpiderLeapPathClear(enemy: RuntimeEnemy): boolean {
    const clearance = tuningValue(enemy.definition, 'leapMobClearance', 12)
    if (this.currentNode?.arena.obstacles.some(obstacle => (
      segmentHitsObstacle(
        enemy.position,
        this.player.position,
        obstacle,
        enemy.definition.radius,
      )
    ))) return false
    return !this.enemies.some((candidate) => {
      if (candidate === enemy || candidate.state === 'dead' || candidate.motherRetreat) return false
      const radius = enemy.definition.radius + candidate.definition.radius + clearance
      return pointToSegmentDistanceSquared(
        candidate.position,
        enemy.position,
        this.player.position,
      ) <= radius * radius
    })
  }

  private startKnifeSpiderV2Strike(enemy: RuntimeEnemy): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    spider.attackMode = 'strike'
    enemy.state = 'attacking'
    enemy.attackWindupMs = tuningValue(enemy.definition, 'orbitStrikeWindupMs', 175)
  }

  private finishKnifeSpiderV2Strike(enemy: RuntimeEnemy): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    spider.attackMode = null
    enemy.attackWindupMs = 0
    enemy.attackCooldownMs = tuningValue(
      enemy.definition,
      'orbitStrikeCooldownMs',
      760,
    )
    if (enemy.state !== 'dead') enemy.state = 'chasing'
  }

  private startKnifeSpiderV2Leap(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
  ): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    spider.attackMode = 'leap'
    spider.flightVelocity = null
    spider.reflected = false
    spider.embedded = false
    enemy.state = 'attacking'
    enemy.attackWindupMs = profile.attackWindupMs
    enemy.lockedAttackDirection = null
    enemy.leapRemainingDistance = 0
    enemy.leapSpeed = 0
    enemy.leapHit = false
  }

  private launchKnifeSpiderV2(enemy: RuntimeEnemy): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    const direction = enemy.lockedAttackDirection ?? normalize({
      x: this.player.position.x - enemy.position.x,
      y: this.player.position.y - enemy.position.y,
    }, enemy.facing)
    const leapDistance = tuningValue(enemy.definition, 'v2LeapDistance', 520)
    const leapDurationMs = tuningValue(enemy.definition, 'v2LeapDurationMs', 290)
    const durationSeconds = Math.max(0.08, leapDurationMs / 1000)
    enemy.lockedAttackDirection = direction
    enemy.facing = direction
    enemy.leapRemainingDistance = leapDistance
    enemy.leapSpeed = leapDistance / durationSeconds
    spider.flightVelocity = {
      x: direction.x * enemy.leapSpeed,
      y: direction.y * enemy.leapSpeed,
    }
    if (this.currentNode?.roomTemplateId === 'false-apartment'
      && this.knifeSpiderTutorialPhase === 'pending') {
      this.knifeSpiderTutorialPhase = 'slowing'
    }
  }

  private updateKnifeSpiderV2Leap(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    deltaSeconds: number,
    deltaMs: number,
  ): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    if (spider.flightVelocity) {
      this.updateKnifeSpiderV2Flight(enemy, profile, deltaSeconds)
      return
    }
    enemy.attackWindupMs = Math.max(0, enemy.attackWindupMs - deltaMs)
    if (enemy.attackWindupMs <= profile.targetLockMs && !enemy.lockedAttackDirection) {
      enemy.lockedAttackDirection = normalize({
        x: this.player.position.x - enemy.position.x,
        y: this.player.position.y - enemy.position.y,
      }, enemy.facing)
    }
    if (enemy.attackWindupMs > 0) return
    this.launchKnifeSpiderV2(enemy)
  }

  private updateKnifeSpiderV2Flight(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    deltaSeconds: number,
  ): void {
    const spider = enemy.knifeSpiderV2
    if (!spider?.flightVelocity || this.knifeSpiderTutorialPhase === 'frozen') return
    const speed = vectorLength(spider.flightVelocity)
    let travel = Math.min(enemy.leapRemainingDistance, speed * deltaSeconds)
    const maximumStep = Math.max(2, enemy.definition.radius * 0.24)
    while (travel > EPSILON && enemy.state !== 'dead') {
      const stepDistance = Math.min(travel, maximumStep)
      const direction = normalize(spider.flightVelocity, enemy.facing)
      const candidate = {
        x: enemy.position.x + direction.x * stepDistance,
        y: enemy.position.y + direction.y * stepDistance,
      }
      if (!this.circleCanOccupy(candidate, enemy.definition.radius)) {
        this.embedKnifeSpiderV2(enemy, profile, 'препятствие')
        return
      }
      const collidedEnemy = this.enemies.find((target) => {
        if (target === enemy || target.state === 'dead' || target.motherRetreat) return false
        const radii = enemy.definition.radius + target.definition.radius
        return distanceSquared(candidate, target.position) <= radii * radii
      })
      if (collidedEnemy) {
        enemy.position = candidate
        this.damageEnemyFromKnifeSpiderFlight(enemy, collidedEnemy, profile)
        this.embedKnifeSpiderV2(enemy, profile, collidedEnemy.definition.name)
        return
      }

      enemy.position = candidate
      enemy.facing = direction
      enemy.leapRemainingDistance = Math.max(0, enemy.leapRemainingDistance - stepDistance)
      travel -= stepDistance

      const playerHitRange = enemy.definition.radius + this.config.player.radius
      if (!enemy.leapHit
        && distanceSquared(enemy.position, this.player.position) <= playerHitRange * playerHitRange) {
        if (this.playerParryCovers(enemy.position, enemy.definition.radius)) {
          this.consumeActiveParry()
          this.reflectKnifeSpiderV2(enemy, this.player.aim)
          return
        }
        enemy.leapHit = true
        this.damagePlayer(profile.attackDamage, enemy.definition.name)
        this.damageKnifeSpiderV2OnImpact(enemy)
        this.finishKnifeSpiderV2Flight(
          enemy,
          profile,
          tuningValue(enemy.definition, 'quickCaptureWindowMs', 170),
          false,
        )
        return
      }

      if (this.knifeSpiderTutorialPhase === 'slowing'
        && Math.sqrt(distanceSquared(enemy.position, this.player.position))
          <= tuningValue(enemy.definition, 'tutorialFreezeDistance', 112)) {
        this.knifeSpiderTutorialPhase = 'frozen'
        this.emitSnapshot(true)
        return
      }
    }
    if (enemy.leapRemainingDistance <= EPSILON) {
      this.finishKnifeSpiderV2Flight(
        enemy,
        profile,
        tuningValue(enemy.definition, 'quickCaptureWindowMs', 170),
        false,
      )
    }
  }

  private damageEnemyFromKnifeSpiderFlight(
    spiderEnemy: RuntimeEnemy,
    target: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
  ): void {
    const reflected = spiderEnemy.knifeSpiderV2?.reflected === true
    const multiplier = reflected
      ? tuningValue(spiderEnemy.definition, 'reflectedDamageMultiplier', 4)
      : 1
    const armor = Math.max(0, target.definition.armor ?? 0) - target.statuses.armorBreak
    const damage = Math.max(0, profile.attackDamage * multiplier - Math.max(0, armor))
    target.hp = Math.max(0, target.hp - damage)
    target.revealedMs = Math.max(target.revealedMs, this.config.combat.enemyRevealOnHitMs)
    if (target.state === 'idle') target.state = 'chasing'
    if (target.hp <= 0) this.finishEnemyDeath(target)
  }

  private damageKnifeSpiderV2OnImpact(enemy: RuntimeEnemy): void {
    enemy.hp = Math.max(
      0,
      enemy.hp - enemy.definition.maxHp
        * tuningValue(enemy.definition, 'impactSelfDamageRatio', 0.1),
    )
    if (enemy.hp <= 0) this.finishEnemyDeath(enemy)
  }

  private embedKnifeSpiderV2(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    impactName: string,
  ): void {
    this.damageKnifeSpiderV2OnImpact(enemy)
    this.effects.push({
      kind: 'hit',
      position: { ...enemy.position },
      direction: normalize(enemy.knifeSpiderV2?.flightVelocity ?? enemy.facing),
      range: 62,
      radius: enemy.definition.radius * 1.7,
      arcDegrees: 360,
      color: enemy.knifeSpiderV2?.reflected ? '#ff5a47' : '#d3b765',
      remainingMs: 360,
      totalMs: 360,
      intensity: enemy.knifeSpiderV2?.reflected ? 1 : 0.7,
    })
    this.addEventLog(`Нож-паук вонзился: ${impactName}`)
    this.finishKnifeSpiderV2Flight(
      enemy,
      profile,
      tuningValue(enemy.definition, 'embeddedCaptureWindowMs', 2200),
      true,
    )
  }

  private finishKnifeSpiderV2Flight(
    enemy: RuntimeEnemy,
    profile: RuntimeEnemyCombatProfile,
    captureWindowMs: number,
    embedded: boolean,
  ): void {
    const spider = enemy.knifeSpiderV2
    if (!spider) return
    spider.attackMode = null
    spider.flightVelocity = null
    spider.reflected = false
    spider.embedded = embedded
    enemy.attackCooldownMs = profile.attackCooldownMs
    enemy.attackWindupMs = 0
    enemy.lockedAttackDirection = null
    enemy.leapRemainingDistance = 0
    enemy.leapSpeed = 0
    enemy.leapHit = false
    if (enemy.state === 'dead') return
    enemy.state = 'chasing'
    enemy.captureWindowMs = captureWindowMs
    enemy.statuses.stunMs = Math.max(enemy.statuses.stunMs, captureWindowMs)
  }

  private reflectKnifeSpiderV2(
    enemy: RuntimeEnemy,
    attackDirection: LastChancesVector,
  ): boolean {
    const spider = enemy.knifeSpiderV2
    if (!spider?.flightVelocity || enemy.state === 'dead') return false
    const incoming = normalize(spider.flightVelocity, enemy.facing)
    const contactNormal = normalize({
      x: enemy.position.x - this.player.position.x,
      y: enemy.position.y - this.player.position.y,
    }, { x: -incoming.x, y: -incoming.y })
    const normalDot = incoming.x * contactNormal.x + incoming.y * contactNormal.y
    const reflected = normalize({
      x: incoming.x - 2 * normalDot * contactNormal.x,
      y: incoming.y - 2 * normalDot * contactNormal.y,
    }, contactNormal)
    const swing = normalize(attackDirection, contactNormal)
    let outgoing = normalize({
      x: reflected.x * 0.78 + contactNormal.x * 0.95 + swing.x * 0.55,
      y: reflected.y * 0.78 + contactNormal.y * 0.95 + swing.y * 0.55,
    }, contactNormal)
    if (outgoing.x * contactNormal.x + outgoing.y * contactNormal.y < 0.25) {
      outgoing = normalize({
        x: outgoing.x + contactNormal.x,
        y: outgoing.y + contactNormal.y,
      }, contactNormal)
    }
    const reflectedSpeed = Math.max(
      tuningValue(enemy.definition, 'reflectedMinimumSpeed', 1250),
      vectorLength(spider.flightVelocity)
        * tuningValue(enemy.definition, 'reflectedSpeedMultiplier', 1.45),
    )
    spider.flightVelocity = {
      x: outgoing.x * reflectedSpeed,
      y: outgoing.y * reflectedSpeed,
    }
    spider.reflected = true
    spider.embedded = false
    enemy.facing = outgoing
    enemy.lockedAttackDirection = outgoing
    enemy.leapSpeed = reflectedSpeed
    enemy.leapRemainingDistance = Math.max(
      enemy.leapRemainingDistance,
      tuningValue(enemy.definition, 'reflectedFlightDistance', 620),
    )
    enemy.hp = Math.max(
      0,
      enemy.hp - enemy.definition.maxHp
        * tuningValue(enemy.definition, 'reflectionSelfDamageRatio', 0.1),
    )
    this.effects.push({
      kind: 'shock',
      position: { ...enemy.position },
      direction: outgoing,
      range: 96,
      radius: enemy.definition.radius * 2,
      arcDegrees: 115,
      color: '#fff0b3',
      remainingMs: 420,
      totalMs: 420,
      intensity: 1,
    })
    this.addEventLog('Нож-паук отбит и сменил траекторию')
    if (this.knifeSpiderTutorialPhase === 'frozen'
      || this.knifeSpiderTutorialPhase === 'slowing') {
      this.knifeSpiderTutorialPhase = 'resuming'
      this.knifeSpiderTutorialResumeMs = 0
    }
    if (enemy.hp <= 0) this.finishEnemyDeath(enemy)
    return true
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
      if (distance > enemy.definition.radius * (mother.entranceRadiusRatio ?? 0.55)) return true
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
    enemy.attackCooldownMs = Math.max(enemy.attackCooldownMs, mother.exitRecoveryMs ?? 650)
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
    const linked = holes.find(hole => hole.id === entrance.linkedHoleId)
    if (!linked) return
    // The entry hole is only transit. Her untelegraphed strike always comes from its memorized
    // linked partner, so watching the shape/color pairing is the player's reliable counterplay.
    const exit = linked
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
      // The blast is invisible until it lands, so the shockwave is the player's only read on
      // where it came from and how far it reached.
      this.effects.push({
        kind: 'shock',
        position: { ...strike.center },
        direction: { x: 1, y: 0 },
        range: strike.radius,
        radius: strike.radius,
        arcDegrees: 360,
        color: '#ef493d',
        remainingMs: 440,
        totalMs: 440,
        intensity: 1,
      })
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
      projectileKnockback: source.projectileKnockback ?? definition.projectileKnockback ?? 0,
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
    if (enemy.knifeSpiderV2) {
      enemy.knifeSpiderV2.attackMode = null
      enemy.knifeSpiderV2.flightVelocity = null
      enemy.knifeSpiderV2.reflected = false
      enemy.attackCooldownMs = profile.attackCooldownMs
      enemy.attackWindupMs = 0
      enemy.lockedAttackDirection = null
      enemy.leapRemainingDistance = 0
      enemy.leapSpeed = 0
      enemy.leapHit = false
      if (enemy.state !== 'dead') enemy.state = 'chasing'
      return
    }
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
      knockback: Math.max(0, profile.projectileKnockback),
      remainingDistance: profile.attackRange,
      remainingMs: profile.attackRange / speed * 1000,
      remainingHits: 1,
      hitIds: new Set(),
      color: enemy.definition.color,
      source: 'enemy',
      sourceName: enemy.definition.name,
      sourceId: enemy.definition.id,
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
    const wriggleEntry = [...this.weapons.entries()].find(([, candidate]) => (
      candidate.controls?.dualsense.haptics?.wriggle
    ))
    const wriggleHand = wriggleEntry?.[0]
    const wriggleWeapon = wriggleEntry?.[1]
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
    const physicalHand = wriggleHand ? runtimeHandToPhysicalCluster(wriggleHand) : null
    const trigger = physicalHand
      ? this.dualSenseControls.snapshot(physicalHand, this.frameNowMs || performance.now())
      : null
    const armedNode = trigger?.armedNodeId
      ? wriggleWeapon.controls?.dualsense.nodes.find(node => node.id === trigger.armedNodeId)
      : undefined
    const throwArmed = armedNode?.next.some((nodeId) => {
      const branch = wriggleWeapon.controls?.dualsense.nodes.find(node => node.id === nodeId)
      return branch?.entryRequiresArmed === true
        && wriggleWeapon.attacks[branch.gesture].behavior === 'spiderThrow'
    }) === true
    const panic = throwArmed
      ? 1
      : 1 - Math.pow(durabilityFraction, wriggle.curveExponent)
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
    if (throwArmed
      && this.spiderWriggle.nextAtMs > this.elapsedMs + wriggle.panicIntervalMs[1]) {
      const rng = this.spiderWriggle.rng
      this.spiderWriggle.nextAtMs = this.elapsedMs
        + wriggle.panicIntervalMs[0]
        + rng() * (wriggle.panicIntervalMs[1] - wriggle.panicIntervalMs[0])
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

  private armedTelegraphPattern(
    weapon: LastChancesResolvedWeapon,
    node: LastChancesAttackSetControlDefinition['dualsense']['nodes'][number],
    heldMs: number,
  ): LastChancesFeedbackPulseDefinition[] | null {
    if (node.holdBehavior === 'channel'
      || node.tactileProfile === 'tension'
      || node.entryTick === null) return null
    if (node.telegraph?.length) return node.telegraph
    const commitPattern = weapon.controls?.dualsense.haptics?.commitPattern
    if (commitPattern?.length) {
      return commitPattern.map(pulse => ({
        ...pulse,
        magnitude: pulse.magnitude * 0.4,
      }))
    }
    const attack = weapon.attacks[node.gesture]
    const charge = attack.charge
    if (!charge) return null
    const band = resolveLastChancesChargedAttack(
      attack,
      heldMs,
      node.chargeBandOverrideId,
    ).band
    if (!band) return null
    const bandIndex = charge.bands.findIndex(candidate => candidate.id === band.id)
    const bandTick = weapon.controls?.dualsense.haptics?.bandTick
      ?? DEFAULT_LAST_CHANCES_BAND_TICK
    return Array.from({ length: bandIndex + 1 }, (_, pulse) => ({
      delayMs: pulse * (bandTick.pulseMs + bandTick.gapMs),
      durationMs: bandTick.pulseMs,
      magnitude: Math.min(1, bandTick.magnitude + bandIndex * bandTick.magnitudeStep),
    }))
  }

  private emitArmedTelegraph(
    physicalHand: LastChancesHand,
    weapon: LastChancesResolvedWeapon,
    node: LastChancesAttackSetControlDefinition['dualsense']['nodes'][number],
    heldMs: number,
  ): void {
    const pattern = this.armedTelegraphPattern(weapon, node, heldMs)
    if (!pattern?.length) return
    this.feedbackController.emit({
      state: 'telegraph',
      profile: node.tactileProfile,
      hand: physicalHand,
      pattern,
      adaptiveOverride: node.adaptiveOverride,
    })
  }

  private startDualSenseTelegraph(
    event: LastChancesSemanticInputEvent,
    weapon: LastChancesResolvedWeapon,
    controls: LastChancesAttackSetControlDefinition,
    node: LastChancesAttackSetControlDefinition['dualsense']['nodes'][number],
  ): void {
    const armedBranches = node.next
      .map(nodeId => controls.dualsense.nodes.find(candidate => candidate.id === nodeId))
      .filter((candidate): candidate is NonNullable<typeof candidate> => (
        candidate?.entryRequiresArmed === true
      ))
    this.armTriggerDetent(event.physicalHand, node.tactileProfile, {
      ...node.adaptiveOverride,
      ...node.armedTriggerOverride,
    })
    const periodMs = this.config.input.dualsense?.telegraphPeriodMs ?? 900
    const now = event.atMs
    if (armedBranches.length > 0) {
      this.feedbackController.emit({
        state: 'continuation',
        profile: 'followUp',
        hand: event.physicalHand,
        pattern: node.armedCue ?? DEFAULT_ARMED_INVITATION,
        adaptiveOverride: node.armedTriggerOverride,
      })
      this.dualSenseTelegraphs[event.physicalHand] = {
        nodeId: node.id,
        nextAtMs: now + periodMs,
      }
      return
    }
    this.emitArmedTelegraph(event.physicalHand, weapon, node, event.heldMs)
    this.dualSenseTelegraphs[event.physicalHand] = {
      nodeId: node.id,
      nextAtMs: now + periodMs,
    }
  }

  private updateDualSenseTelegraphs(): void {
    const active = this.controlSchemeValue === 'dualsense'
      && this.canUseRoomActions()
      && !this.paused
      && this.feedbackPreferences.mode === 'full'
    if (!active) {
      this.dualSenseTelegraphs.left = null
      this.dualSenseTelegraphs.right = null
      return
    }
    const now = this.frameNowMs || performance.now()
    for (const physicalHand of LAST_CHANCES_HANDS) {
      const loop = this.dualSenseTelegraphs[physicalHand]
      if (!loop) continue
      const trigger = this.dualSenseControls.snapshot(physicalHand, now)
      if (trigger.armedNodeId !== loop.nodeId) {
        this.dualSenseTelegraphs[physicalHand] = null
        continue
      }
      const weapon = this.weapons.get(physicalClusterToRuntimeHand(physicalHand))
      const node = weapon?.controls?.dualsense.nodes.find(candidate => candidate.id === loop.nodeId)
      if (!weapon || !node) {
        this.dualSenseTelegraphs[physicalHand] = null
        continue
      }
      if (now < loop.nextAtMs) continue
      this.emitArmedTelegraph(physicalHand, weapon, node, trigger.heldMs)
      loop.nextAtMs = now + (this.config.input.dualsense?.telegraphPeriodMs ?? 900)
    }
  }

  /**
   * An enemy exerts pressure once it has noticed the player and until it dies or loses them.
   * A hidden Cockroach Mother is deliberately excluded: she is off the board while retreating.
   * This is the shared definition of "in combat" for both mental health and stamina.
   */
  private combatPressurePerSecond(): number {
    return Math.min(
      this.config.mentalHealth.maxPressurePerSecond,
      this.enemies.reduce((sum, enemy) => (
        enemy.motherRetreat?.stage === 'hidden'
          ? sum
          : this.invisibleWolfCommitted(enemy)
          || enemy.state === 'noticing' || enemy.state === 'alerted'
          || enemy.state === 'chasing' || enemy.state === 'attacking'
          ? sum + enemy.definition.mentalPressurePerSecond
          : sum
      ), 0),
    )
  }

  /**
   * A wolf that has committed to a run at the player is in combat even though its `state` is held
   * at `idle` to keep it hidden. Without this it would press the player's mind *less* than the
   * pre-stalker wolf did, because it spends nearly all of a fight unseen. Mere `stalking` does not
   * count: that phase is the wolf's equivalent of not having noticed you yet.
   */
  private invisibleWolfCommitted(enemy: RuntimeEnemy): boolean {
    const phase = enemy.invisibleWolf?.phase
    return phase === 'closing' || phase === 'lunging'
      || phase === 'recovering' || phase === 'exposed'
  }

  private updateMentalHealth(deltaSeconds: number): void {
    const pressure = this.combatPressurePerSecond()
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

  /** Stamina regenerated per second right now, including equipment. */
  private staminaRegenPerSecond(): number {
    const stamina = this.config.stamina
    const base = this.combatPressurePerSecond() > 0
      ? stamina.regenPerSecond
      : stamina.outOfCombatRegenPerSecond
    return this.scaleStaminaGain(base + (this.activeArtifact()?.staminaRegenPerSecond ?? 0))
  }

  /**
   * The stamina outfit doubles restoration "from any source", so every gain — passive
   * regeneration and the alternation/combo attack refunds alike — goes through here.
   */
  private scaleStaminaGain(amount: number): number {
    return amount * Math.max(0, this.activeOutfit()?.staminaRegenMultiplier ?? 1)
  }

  private updateStamina(deltaSeconds: number): void {
    this.player.stamina = clamp(
      this.player.stamina + this.staminaRegenPerSecond() * deltaSeconds,
      0,
      this.effectivePlayerStats().maxStamina,
    )
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
    if (event.inputReleased) {
      this.settleBowChannelRelease(event.hand, event.atMs, event.heldMs, null)
      return 'observe'
    }
    const weapon = this.weapons.get(event.hand)
    const controls = weapon?.controls
    if (!weapon || !controls) return this.blockSemanticInput(event, null)
    if (event.scheme === 'dualsense' && event.depthTickIndex !== undefined) {
      const depthTick = controls.dualsense.haptics?.depthTicks?.[event.depthTickIndex]
      if (depthTick) {
        this.feedbackController.emit({
          state: 'charge',
          profile: 'click',
          hand: event.physicalHand,
          tick: depthTick.tick,
        })
      }
      return 'handled'
    }

    let gesture: LastChancesGesture | undefined
    let tactileProfile = event.tactileProfile ?? 'click'
    let chargeBandOverrideId: string | undefined
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
          if (event.phase === 'arm') {
            this.startDualSenseTelegraph(event, weapon, controls, node)
            return 'handled'
          }
          if (!event.probe
            && isLongbowPrimary(weapon)
            && (node.gesture === 'hold' || node.gesture === 'holdThenDoubleTap')) {
            this.accrueBowDrawDebit(event.hand, weapon.attacks.hold, event.heldMs)
          }
          const requiredBand = node.requiredChargeBandId
            ? weapon.attacks.hold.charge?.bands.find(band => band.id === node.requiredChargeBandId)
            : undefined
          const chargeContextAvailable = node.requiredChargeBandId === undefined
            || (requiredBand !== undefined && event.heldMs >= requiredBand.minMs)
          const nodeAttack = weapon.attacks[node.gesture]
          const nodeChargeAvailable = nodeAttack.charge === undefined
            || resolveLastChancesChargedAttack(
              nodeAttack,
              event.heldMs,
              node.chargeBandOverrideId,
            ).band !== null
          const baseContextAvailable = node.entryContext === 'neutral'
              ? this.controlContextActive(event.hand, 'neutral')
              : node.entryContext === 'continuation'
              || this.controlContextActive(event.hand, node.entryContext)
          const armedAvailable = node.entryRequiresArmed !== true || event.armed === true
          const contextAvailable = armedAvailable
            && baseContextAvailable
            && chargeContextAvailable
            && nodeChargeAvailable
      if (event.probe) {
            return contextAvailable && this.gestureReady(event.hand, node.gesture)
              ? 'handled'
              : 'observe'
          }
          if (!contextAvailable) return event.commit
            ? this.blockSemanticInput(event, node.gesture)
            : 'observe'
          gesture = event.gesture
          chargeBandOverrideId = node.chargeBandOverrideId
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
    if (event.atMs <= this.bowChannelReleasedAtInputMs[event.hand] + 0.5
      && (attack.behavior === 'bowRapidFire' || attack.behavior === 'bowRain')) {
      // The physical release edge already retroactively settled and closed this channel.
      // Consume the recognizer's later classifier without a false blocked cue or a reopen.
      return 'handled'
    }
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
        if (!event.coalesced) {
          this.feedbackController.emit({
            state: tensionActive ? 'tension' : state === 'ready' ? 'charge' : state,
            profile: tactileProfile,
            hand: event.physicalHand,
            tick,
            adaptiveOverride,
          })
        }
      }
      const immediateMylorikContinuation = event.scheme === 'mylorik'
        && event.context === 'continuation'
        && event.phase === 'press'
        && controls.mylorik.activations.some(activation => (
          activation.gesture === gesture
          && activation.context === 'continuation'
          && activation.continuationDispatch === 'press'
        ))
      if (immediateMylorikContinuation) {
        return this.gestureReady(event.hand, gesture) ? 'handled' : 'observe'
      }
      if (event.scheme === 'mylorik' && event.probe) {
        return this.gestureReady(event.hand, gesture) ? 'handled' : 'observe'
      }
      return event.scheme === 'dualsense' || event.probe ? 'handled' : 'observe'
    }

    if (isLongbowPrimary(weapon) && (gesture === 'hold' || gesture === 'holdThenDoubleTap')) {
      this.accrueBowDrawDebit(event.hand, weapon.attacks.hold, event.heldMs)
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
      const paidLongbowRelease = isLongbowPrimary(weapon)
        && (gesture === 'hold' || gesture === 'holdThenDoubleTap')
      if (event.scheme === 'mylorik' && !paidLongbowRelease && enabled && unlocked
        && Math.max(cooldown, recovery) > 0
        && Math.max(cooldown, recovery) <= bufferWindow) return 'buffer'
      return this.blockSemanticInput(event, gesture)
    }

    const chargedAttack = attackWithLastChancesAugment(attack, weapon)
    const chargedHeldMs = gesture === 'holdThenDoubleTap'
      ? event.heldMs
      : gesture === 'hold' || gesture === 'doubleTapHold' ? event.heldMs : 0
    if (attack.charge && !resolveLastChancesChargedAttack(
      chargedAttack,
      chargedHeldMs,
      chargeBandOverrideId,
    ).band) {
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
      const baseCommitPattern = controls.dualsense.haptics?.commitPattern
      const fangTailCount = weapon.trait === 'ouroborosFang' && gesture === 'tap'
        ? Math.min(4, Math.floor(this.ouroborosFangKillStacks / 5))
        : 0
      const basePatternEnd = baseCommitPattern?.reduce(
        (end, pulse) => Math.max(end, pulse.delayMs + pulse.durationMs),
        0,
      ) ?? 0
      const commitPattern = baseCommitPattern && fangTailCount > 0
        ? [
            ...baseCommitPattern,
            ...Array.from({ length: fangTailCount }, (_, index) => ({
              delayMs: basePatternEnd + 55 + index * 75,
              durationMs: 28,
              magnitude: 0.3 + index * 0.04,
            })),
          ]
        : baseCommitPattern
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
      && ['chainHook', 'spearStance', 'axeSpin', 'spiderFlurry'].includes(attack.behavior ?? '')
    if (startsDualSenseChannel) return 'handled'
    this.performAttack({
      hand: event.hand,
      gesture,
      atMs: event.atMs,
      heldMs: event.heldMs,
      firstHoldMs: event.heldMs,
      ...(chargeBandOverrideId ? { minChargeBandId: chargeBandOverrideId } : {}),
    })
    return 'handled'
  }

  private blockSemanticInput(
    event: LastChancesSemanticInputEvent,
    gesture: LastChancesGesture | null,
  ): 'blocked' {
    const weapon = this.weapons.get(event.hand)
    if (event.commit && isLongbowPrimary(weapon) && this.bowDrawDebits[event.hand].active) {
      this.consumeBowDrawDebit(event.hand, true)
    }
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
          || isSpearSpinBehavior(area.attack.behavior)
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
      || isSpearSpinBehavior(activeBehavior)
      || activeBehavior === 'chainSpin'
      || this.activeAreas.some(area => area.hand === hand && area.weaponId === weapon.id && (
        area.attack.behavior === 'axeSpin'
        || isSpearSpinBehavior(area.attack.behavior)
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
          : behavior === 'axeSpin' || isSpearSpinBehavior(behavior) || behavior === 'chainSpin'
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

  private bowFirstShotReady(hand: LastChancesHand, weaponId: string): boolean {
    return this.bowLastShotTargets[hand] !== null
      && this.bowLastShotWeaponIds[hand] === weaponId
      && this.elapsedMs - this.bowLastShotAtMs[hand] <= this.config.input.doubleTapMs + 120
  }

  private bowRapidPrefixReady(hand: LastChancesHand, weaponId: string): boolean {
    return this.bowDoubleShotWeaponIds[hand] === weaponId
      && this.elapsedMs - this.bowDoubleShotAtMs[hand]
        <= this.config.input.doubleTapMs + this.config.input.holdMs + 120
  }

  private bowRapidFirstShotReady(hand: LastChancesHand, weaponId: string): boolean {
    const schemeHoldMs = Math.max(
      this.config.input.holdMs,
      this.config.input.mylorik?.techniqueHoldMs ?? 0,
    )
    return this.bowLastShotTargets[hand] !== null
      && this.bowLastShotWeaponIds[hand] === weaponId
      && this.elapsedMs - this.bowLastShotAtMs[hand]
        <= this.config.input.doubleTapMs + schemeHoldMs + 120
  }

  private gestureReady(hand: LastChancesHand, gesture: LastChancesGesture): boolean {
    const weapon = this.weapons.get(hand)
    if (!weapon) return false
    const attack = weapon.attacks[gesture]
    if (attack.enabled === false || attack.behavior === 'disabled') return false
    // The Sword's Unterhau is its signature follow-up and ships unlocked; every other weapon
    // still earns `doubleTapHold` from the elite combo quest.
    const questExempt = weapon.trait === 'swordRhythm' && gesture === 'doubleTapHold'
    if (!questExempt && !this.moveQuests[hand].unlocked[gesture]) return false
    const state = this.weaponState(weapon)
    if (state.resource <= 0 && weapon.resource?.kind !== 'rhythm') return false
    if (this.provisionalParry
      && this.provisionalParry.weaponId === weapon.id
      && this.provisionalParry.hand !== hand) return false
    const behavior = attack.behavior
    if (weapon.trait === 'longbowPersistence') {
      if (behavior === 'bowDoubleShot') {
        if (!this.bowFirstShotReady(hand, weapon.id)) return false
      }
      if (behavior === 'bowRapidFire') {
        if (!this.bowRapidFirstShotReady(hand, weapon.id)
          && !this.bowRapidPrefixReady(hand, weapon.id)) return false
      }
      if (behavior === 'bowJump') {
        if (this.activeDash?.weaponId !== weapon.id
          || this.activeDash.hand !== hand
          || this.activeDash.attack.behavior !== 'bowDodge') return false
      }
      if (behavior === 'bowRiposte') {
        const response = this.bowResponseWindows[hand]
        if (!response || response.weaponId !== weapon.id) return false
      }
      if (behavior === 'bowScatter') {
        const draw = weapon.attacks.hold
        const goldStartMs = tuningValue(draw, 'goldStartMs', 670)
        const goldEndMs = Math.max(goldStartMs, tuningValue(draw, 'goldEndMs', 760))
        const released = this.releasedBowDraw?.hand === hand
          && this.releasedBowDraw.golden
          && this.elapsedMs - this.releasedBowDraw.releasedAtMs
            <= this.config.input.holdThenDoubleTapWindowMs + 120
        const debit = this.bowDrawDebits[hand]
        const activeGoldenDraw = debit.active
          && debit.accruedMs >= goldStartMs
          && debit.accruedMs <= goldEndMs
        if (!released && !activeGoldenDraw) return false
      }
    }
    const swordMorph = weapon.trait === 'swordRhythm'
      && (behavior === 'swordOpening' || behavior === 'swordFollowUp')
      && this.activeAreas.some(area => (
        area.weaponId === weapon.id && area.attack.behavior === 'swordRhythm'
      ))
    const bowActionContinuation = weapon.trait === 'longbowPersistence'
      && (
        behavior === 'bowDodge'
        || behavior === 'bowRapidFire'
        || (behavior === 'bowJump'
          && this.activeDash?.weaponId === weapon.id)
        || (behavior === 'bowRiposte'
          && this.bowResponseWindows[hand]?.weaponId === weapon.id)
        || (behavior === 'bowScatter'
          && (this.releasedBowDraw?.hand === hand || this.bowDrawDebits[hand].active))
        || (behavior === 'bowIgnite'
          && (
            this.bowChannels.get(hand)?.behavior === 'bowRain'
            || performance.now() - this.bowRainReleasedAtInputMs[hand]
              <= this.config.input.holdThenDoubleTapWindowMs + 120
          ))
      )
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
      // «Прорыв» morphs out of a «Прокол» that is still live, so the thrust's own action lock
      // must not be what refuses it — the same allowance the sword morph gets.
      || (behavior === 'spearBreakthrough' && this.activeAreas.some(area => (
        area.hand === hand && area.weaponId === weapon.id && area.attack.behavior === 'spearPierce'
      )))
      || bowActionContinuation
      || swordMorph
    if ((this.weaponActionEnds.get(weapon.id) ?? 0) > this.elapsedMs
      && !contextualContinuation) return false
    const otherHandUsesWeapon = [...this.heldChannels.entries()].some(([channelHand, area]) => (
      channelHand !== hand && area.weaponId === weapon.id
    ))
    const otherHandUsesBowChannel = [...this.bowChannels.values()].some(channel => (
      channel.hand !== hand && channel.weaponId === weapon.id
    ))
    const bowMobilityDuringOtherChannel = weapon.trait === 'longbowPersistence'
      && ['bowDodge', 'bowJump', 'bowRiposte'].includes(behavior ?? '')
    if (otherHandUsesWeapon || (otherHandUsesBowChannel && !bowMobilityDuringOtherChannel)) {
      return false
    }
    const axeRecoveryCancel = weapon.trait === 'axeHookRecovery'
      && gesture === 'tap'
      && state.recoveryMs > 0
    if ((this.player.recoveryMs > 0 || state.recoveryMs > 0)
      && !axeRecoveryCancel && !contextualContinuation) {
      return false
    }
    if (weapon.trait === 'swordRhythm' && gesture === 'doubleTap') {
      return (this.cooldownEnds.get(cooldownKey(hand, 'doubleTap')) ?? 0) <= this.elapsedMs
    }
    if (weapon.trait === 'swordRhythm' && gesture === 'doubleTapHold') {
      const unterhauReady = (this.cooldownEnds.get(cooldownKey(hand, 'doubleTapHold')) ?? 0)
        <= this.elapsedMs
      return state.unterhauDueAtMs > 0 && unterhauReady
    }
    if (gesture === 'tap' && attack.cooldownMs > 0) {
      return (this.cooldownEnds.get(cooldownKey(hand, gesture)) ?? 0) <= this.elapsedMs
    }
    return gesture === 'tap'
      || (this.cooldownEnds.get(cooldownKey(hand, gesture)) ?? 0) <= this.elapsedMs
  }

  private sidewaysAttackCollider(
    origin: LastChancesVector,
    direction: LastChancesVector,
    attack: LastChancesAttackDefinition,
  ): LastChancesRuntimeCollider | null {
    const aim = normalize(direction)
    const sidewaysHalfWidth = Math.max(0, tuningValue(attack, 'sidewaysHalfWidth', 0))
    if (sidewaysHalfWidth <= 0) return null
    const perpendicular = { x: -aim.y, y: aim.x }
    const center = {
      x: origin.x + aim.x
        * tuningValue(attack, 'sidewaysForwardOffset', this.config.player.radius),
      y: origin.y + aim.y
        * tuningValue(attack, 'sidewaysForwardOffset', this.config.player.radius),
    }
    return {
      shape: 'capsule',
      start: {
        x: center.x - perpendicular.x * sidewaysHalfWidth,
        y: center.y - perpendicular.y * sidewaysHalfWidth,
      },
      end: {
        x: center.x + perpendicular.x * sidewaysHalfWidth,
        y: center.y + perpendicular.y * sidewaysHalfWidth,
      },
      radius: Math.max(1, (attack.collider?.width ?? attack.radius * 2) / 2),
    }
  }

  private activatePlayerParry(
    attack: LastChancesAttackDefinition,
    minimumDurationMs = 0,
  ): void {
    this.player.parryMs = Math.max(
      this.player.parryMs,
      Math.max(
        this.config.combat.minimumPlayerParryMs,
        minimumDurationMs,
        attack.durationMs + (attack.lingerMs ?? 0),
      ),
    )
    this.activeParryCollider = this.sidewaysAttackCollider(
      this.player.position,
      this.player.aim,
      attack,
    ) ?? resolveAttackCollider(
      this.player.position,
      normalize(this.player.aim),
      attack,
    )
    this.activeParryAttack = attack
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
    this.activeParryAttack = null
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
    enemy.revealedMs = Math.max(
      enemy.revealedMs,
      this.config.combat.enemyRevealOnParryMs,
    )
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

  /**
   * One held second tap has three release outcomes. The early band is the same broad shove as a
   * plain double tap; only the two deeper bands become the narrow kick collider. A numeric stage
   * is stamped into runtime tuning so knockback placement can distinguish the ordinary and
   * strong kicks without adding schema-only fields.
   */
  private resolveSpearKickOutcome(
    attack: LastChancesAttackDefinition,
    chargeBandId: string | undefined,
  ): LastChancesAttackDefinition {
    if (attack.behavior !== 'spearKick') return attack
    const stage = chargeBandId === 'strong-kick' ? 2 : chargeBandId === 'kick' ? 1 : 0
    const hitEffects = (attack.hitEffects ?? []).map(effect => effect.status === 'stun'
      ? {
          ...effect,
          durationMs: stage === 2
            ? tuningValue(attack, 'strongKickImmobilizeMs', 1500)
            : 1000,
        }
      : { ...effect })
    if (stage === 0) {
      return {
        ...attack,
        name: 'Толчок',
        behavior: 'spearShove',
        kind: 'burst',
        collider: {
          ...(attack.collider ?? { traceMs: 620 }),
          shape: 'sector',
          width: Math.max(92, attack.collider?.width ?? 0),
          strictInnerRange: false,
        },
        hitEffects,
        tuning: { ...attack.tuning, chargeStage: stage },
      }
    }
    return {
      ...attack,
      name: 'Пинок',
      hitEffects,
      tuning: { ...attack.tuning, chargeStage: stage },
    }
  }

  private performAttack(resolution: LastChancesGestureResolution): void {
    if (!this.canUseRoomActions() || this.paused || !this.currentNode) return
    const { hand, gesture } = resolution
    if (this.knifeSpiderTutorialPhase === 'frozen'
      && !this.resolvingKnifeSpiderTutorialParry) {
      if (hand === 'left' && gesture === 'tap') this.performKnifeSpiderTutorialParry()
      return
    }
    const weapon = this.weapons.get(hand)
    if (!weapon) return
    const provisional = this.provisionalParry?.hand === hand
      ? this.provisionalParry
      : null
    if (provisional && gesture !== 'tap') {
      if (isSpearV2Secondary(weapon)
        && (gesture === 'doubleTap' || gesture === 'doubleTapHold')) {
        // This is the authored morph: keep the shaft raised and discard only provisional
        // bookkeeping. Even a successful reflection may continue into the committed follow-up.
        this.provisionalParry = null
        if (gesture === 'doubleTap') {
          this.player.parryMs = Math.max(
            this.player.parryMs,
            tuningValue(weapon.attacks.doubleTap, 'windupMs', 250),
          )
        }
      } else if (provisional.consumed) {
        this.commitOrCancelProvisionalParry()
        return
      } else this.cancelProvisionalParry()
    }
    // Ahead of gestureReady/stamina/combo handling, and downstream of all three control schemes,
    // so a tap inside the «Прокол» window becomes another «Прокол» no matter how it was produced.
    if (gesture === 'tap' && isSpearV2Primary(weapon) && this.pierceMashActive(hand)) {
      this.morphIntoPierce(weapon, hand)
      this.performAttack({ ...resolution, gesture: 'doubleTap' })
      return
    }
    const state = this.weaponState(weapon)
    if (!this.gestureReady(hand, gesture)) return
    const responseWindow = this.bowResponseWindows[hand]
    const responseHeldMs = weapon.attacks[gesture].behavior === 'bowRiposte' && responseWindow
      ? Math.max(0, resolution.atMs - responseWindow.startedAtInputMs)
      : resolution.heldMs
    if (weapon.attacks[gesture].behavior === 'bowRiposte'
      && responseHeldMs < tuningValue(weapon.attacks[gesture], 'goldStartMs', 470)) {
      // Releasing before the response pocket is literally "nothing": no shot, cooldown,
      // stamina debit or fresh invulnerability.
      this.bowResponseWindows[hand] = null
      return
    }
    if (weapon.trait === 'swordRhythm'
      && gesture === 'doubleTapHold'
      && state.unterhauDueAtMs > 0) {
      if (resolution.heldMs < tuningValue(weapon, 'unterhauHoldMs', 1000)) return
      this.executePendingUnterhau(hand)
      return
    }
    // Resolved up here so the stamina gate can price the charge band the release will
    // actually land in; it depends only on `resolution`, never on the combo cursor.
    const rawChargeHeldMs = gesture === 'holdThenDoubleTap'
      ? resolution.firstHoldMs
      : gesture === 'hold' || gesture === 'doubleTapHold' ? resolution.heldMs : 0
    const drawDebit = this.bowDrawDebits[hand]
    const gestureBehavior = weapon.attacks[gesture].behavior
    const chargeUsesPaidBowDraw = (gesture === 'hold' && gestureBehavior === 'bowDraw')
      || (gesture === 'holdThenDoubleTap' && gestureBehavior === 'bowScatter')
    const chargeHeldMs = chargeUsesPaidBowDraw
      && drawDebit.exhausted
      ? Math.min(rawChargeHeldMs, drawDebit.accruedMs)
      : rawChargeHeldMs
    const semanticBowScatter = gestureBehavior === 'bowScatter'
      && this.releasedBowDraw?.hand !== hand
    const scatterDraw = semanticBowScatter ? weapon.attacks.hold : null
    const scatterGoldStartMs = scatterDraw ? tuningValue(scatterDraw, 'goldStartMs', 670) : 0
    const scatterGoldEndMs = scatterDraw
      ? Math.max(scatterGoldStartMs, tuningValue(scatterDraw, 'goldEndMs', 760))
      : 0
    const semanticBowScatterGolden = semanticBowScatter
      && chargeHeldMs >= scatterGoldStartMs
      && chargeHeldMs <= scatterGoldEndMs
    const tentativeBowDrawRefund = drawDebit.active
      && ['bowShot', 'bowDoubleShot', 'bowRapidFire'].includes(gestureBehavior ?? '')
    if (gestureBehavior === 'bowScatter') {
      const released = this.releasedBowDraw
      const releasedHeldMs = released?.hand === hand ? released.heldMs : chargeHeldMs
      const draw = weapon.attacks.hold
      const goldStartMs = tuningValue(draw, 'goldStartMs', 670)
      const goldEndMs = Math.max(goldStartMs, tuningValue(draw, 'goldEndMs', 760))
      if (releasedHeldMs < goldStartMs || releasedHeldMs > goldEndMs) return
    }
    // Before advanceTapCombo and every cooldown/rhythm mutation, so a refusal changes nothing.
    const bowRapidNeedsSecondShot = gestureBehavior === 'bowRapidFire'
      && !this.bowRapidPrefixReady(hand, weapon.id)
    const bowRapidSecondShotCost = bowRapidNeedsSecondShot
      ? this.staminaCostFor(weapon, hand, 'doubleTap')
      : 0
    const staminaCost = this.staminaCostFor(
      weapon,
      hand,
      gesture,
      chargeHeldMs,
      resolution.minChargeBandId,
    ) + bowRapidSecondShotCost
    const staminaAvailableForAction = this.player.stamina
      + (semanticBowScatterGolden ? drawDebit.spent : 0)
      + (tentativeBowDrawRefund ? drawDebit.spent : 0)
    if (staminaCost > 0 && staminaAvailableForAction + EPSILON < staminaCost) {
      if (tentativeBowDrawRefund || semanticBowScatterGolden) {
        this.consumeBowDrawDebit(hand, true)
      }
      this.refuseForStamina(hand)
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
    const bowDrawGolden = sourceAttack.behavior === 'bowDraw'
      && chargeHeldMs >= tuningValue(sourceAttack, 'goldStartMs', 670)
      && chargeHeldMs <= tuningValue(sourceAttack, 'goldEndMs', 760)
    const chargeFloor = bowDrawGolden
      ? sourceAttack.charge?.bands.at(-1)?.id
      : resolution.minChargeBandId
    const charged = sourceAttack.behavior === 'bowDraw'
      ? this.resolveBowDrawAttack(augmented, chargeHeldMs, bowDrawGolden)
      : resolveLastChancesChargedAttack(
          augmented,
          chargeHeldMs,
          chargeFloor,
        )
    if (sourceAttack.charge && !charged.band) {
      if (sourceAttack.behavior === 'bowDraw') {
        this.consumeBowDrawDebit(hand, false)
        this.refuseForStamina(hand)
      }
      return
    }
    const attack = isSpearV2Secondary(weapon)
      ? this.resolveSpearKickOutcome(charged.attack, charged.band?.id)
      : charged.attack
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
    if (gesture !== 'tap' || attack.cooldownMs > 0) {
      const cooldownAttack = weapon.trait === 'swordRhythm' && gesture === 'doubleTapHold'
        ? weapon.attacks.doubleTap
        : weapon.trait === 'ouroborosFang' && gesture === 'tap'
          ? weapon.attacks.tap
        : attack
      this.cooldownEnds.set(
        weapon.trait === 'swordRhythm' && gesture === 'doubleTapHold'
          ? cooldownKey(hand, 'doubleTap')
          : key,
        bowDrawGolden && gesture === 'hold'
          ? this.elapsedMs
          : this.elapsedMs + cooldownAttack.cooldownMs,
      )
    }
    if (this.config.combat.attackStopsMovement) {
      this.movementVelocity = { x: 0, y: 0 }
    }
    if (semanticBowScatterGolden) {
      this.consumeBowDrawDebit(hand, true)
      this.cooldownEnds.set(cooldownKey(hand, 'hold'), this.elapsedMs)
      this.bowShotPresentation.goldenUntilMs = this.elapsedMs + 700
    }
    if (tentativeBowDrawRefund) this.consumeBowDrawDebit(hand, true)
    const directBowStaminaAction = sourceAttack.behavior === 'bowDraw'
      || sourceAttack.behavior === 'bowRapidFire'
      || sourceAttack.behavior === 'bowRain'
    if (bowRapidNeedsSecondShot) {
      this.settleStaminaForAttack(hand, bowRapidSecondShotCost)
    } else if (!directBowStaminaAction) {
      this.settleStaminaForAttack(hand, staminaCost)
    }
    if (sourceAttack.behavior === 'bowDraw') {
      this.consumeBowDrawDebit(hand, bowDrawGolden)
      if (bowDrawGolden) {
        this.bowShotPresentation.goldenUntilMs = this.elapsedMs + 700
      }
    }
    this.lastGesture = {
      hand,
      gesture,
      attackName: attack.name,
      atMs: this.elapsedMs,
      ...(comboStep === undefined ? {} : { comboStep }),
    }
    let direction = normalize(this.player.aim)
    if (attack.behavior === 'bowScatter' && this.releasedBowDraw?.hand === hand) {
      direction = { ...this.releasedBowDraw.direction }
    }
    const movement = this.resolveMovement()
    if (['katanaHop', 'katanaHopSlash'].includes(attack.behavior ?? '')
      && vectorLength(movement) > this.config.input.actionDirectionDeadZone) {
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
    if (sourceAttack.behavior === 'bowDraw') {
      // Semantic controller routes do not pass through the legacy `release()` edge. Record the
      // loosed main arrow here as the scheme-independent authority, so their golden follow-up
      // adds only the scatter instead of duplicating the maximum-draw arrow.
      this.releasedBowDraw = {
        hand,
        heldMs: chargeHeldMs,
        direction: { ...direction },
        golden: bowDrawGolden,
        releasedAtMs: this.elapsedMs,
      }
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
    if (attack.invulnerabilityMs && attack.behavior !== 'bowRiposte') {
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
    if (attack.behavior === 'spearPierce') {
      if (!this.pierceMashActive(hand)) {
        this.pierceMash[hand].expiresAtMs = this.elapsedMs
          + Math.max(0, tuningValue(attack, 'mashWindowMs', 2000))
        this.pierceMash[hand].hits = 0
        // Whatever the chain was mid-way through, the burst restarts it at Охота afterwards.
        this.tapCombos[hand].step = 0
        this.tapCombos[hand].expiresAtMs = 0
      }
      this.pierceMash[hand].hits += 1
    }
    if (isSpearSpinBehavior(attack.behavior)) {
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

    const windupMs = attack.behavior === 'spearShove'
      ? Math.max(0, tuningValue(attack, 'windupMs', 0))
      : 0
    const actionDurationMs = windupMs + attack.durationMs + (attack.lingerMs ?? 0)
    const commitsTapParry = gesture === 'tap'
      && PLAYER_PARRY_BEHAVIORS.has(attack.behavior ?? 'standard')
    const bowReleaseUsesCooldownOnly = weapon.trait === 'longbowPersistence'
      && ['bowDoubleShot', 'bowDraw', 'bowScatter'].includes(attack.behavior ?? '')
    if ((gesture !== 'tap' || commitsTapParry) && !bowReleaseUsesCooldownOnly) {
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
    if (windupMs > 0) {
      const parry = attackWithLastChancesAugment(weapon.attacks.tap, weapon)
      this.activatePlayerParry(parry, windupMs)
      if (this.activeParryCollider) this.addColliderTrace(this.activeParryCollider, parry)
      this.delayedAttacks.push({
        remainingMs: windupMs,
        attack,
        direction: { ...direction },
        context,
      })
    } else this.executeAttack(attack, direction, context)
    if (weapon.trait === 'spiderDurability') {
      const useCost = attack.consumeAllResource
        ? state.maxResource
        : attack.resourceCost ?? tuningValue(weapon, 'durabilityPerUse', 2)
      this.spendWeaponResource(weapon.id, useCost)
    }
  }

  /**
   * The action a gesture would run, resolved without mutating the tap-combo step. Affordability
   * has to be known before `advanceTapCombo` so a refusal cannot corrupt the chain.
   */
  private peekSourceAttack(
    weapon: LastChancesResolvedWeapon,
    hand: LastChancesHand,
    gesture: LastChancesGesture,
  ): LastChancesAttackDefinition {
    if (gesture !== 'tap') return weapon.attacks[gesture]
    const combo = this.tapCombos[hand]
    const step = combo.step > 0 && this.elapsedMs <= combo.expiresAtMs ? combo.step + 1 : 1
    return weapon.tapCombo[(step - 1) % weapon.tapCombo.length]
  }

  /**
   * Stamina debited by one action. Fatigue does not stop the weapon any more — it makes every
   * swing ruinously expensive instead, which is the Mercenary Sword's whole rhythm pressure.
   *
   * Charged actions are priced through their band, so a weapon can make a deeper wind-up cost
   * more (Двуручное копьё v2 climbs 10/15/20 across the замах). The band resolved here is
   * provably the one `performAttack` executes: for every charged gesture `peekSourceAttack`
   * returns the very object `sourceAttack` uses, and it is handed the same `chargeHeldMs`.
   */
  private staminaCostFor(
    weapon: LastChancesResolvedWeapon,
    hand: LastChancesHand,
    gesture: LastChancesGesture,
    chargeHeldMs = 0,
    minChargeBandId?: string,
  ): number {
    const peeked = this.peekSourceAttack(weapon, hand, gesture)
    const attack = peeked.charge
      ? resolveLastChancesChargedAttack(peeked, chargeHeldMs, minChargeBandId).attack
      : peeked
    const base = Math.max(0, attack.staminaCost ?? this.config.stamina.attackCost)
    const fatigued = this.weaponState(weapon).fatigueMs > 0
    const fatigueAdjusted = fatigued
      ? base * Math.max(1, tuningValue(weapon, 'fatigueStaminaMultiplier', 10))
      : base
    return fatigueAdjusted * this.staminaCostMultiplier()
  }

  /**
   * Longbow power is continuous even though charge bands still gate the first legal release and
   * label milestones. The final band's authored numbers define the 1-second maximum.
   */
  private resolveBowDrawAttack(
    source: LastChancesAttackDefinition,
    heldMs: number,
    forceMaximum: boolean,
  ): ReturnType<typeof resolveLastChancesChargedAttack> {
    const charge = source.charge
    if (!charge) return resolveLastChancesChargedAttack(source, heldMs)
    const maximumBand = [...charge.bands].sort((left, right) => left.minMs - right.minMs).at(-1)
    const gated = resolveLastChancesChargedAttack(
      source,
      forceMaximum ? charge.maxMs : heldMs,
      forceMaximum ? maximumBand?.id : undefined,
    )
    if (!gated.band || !maximumBand) return gated
    const maximum = resolveLastChancesChargedAttack(source, charge.maxMs, maximumBand.id).attack
    const progress = forceMaximum
      ? 1
      : resolveLastChancesBowCharge(source, heldMs).powerProgress
    const interpolate = (start: number, end: number): number => start + (end - start) * progress
    return {
      ...gated,
      attack: {
        ...(forceMaximum ? maximum : source),
        damage: interpolate(source.damage, maximum.damage),
        range: interpolate(source.range, maximum.range),
        radius: interpolate(source.radius, maximum.radius),
        projectileSpeed: interpolate(source.projectileSpeed, maximum.projectileSpeed),
        knockback: interpolate(source.knockback, maximum.knockback),
        durationMs: interpolate(source.durationMs, maximum.durationMs),
      },
      heldMs: Math.min(Math.max(0, heldMs), charge.maxMs),
      chargeProgress: progress,
    }
  }

  private staminaCostMultiplier(): number {
    return 1 + this.staminaCostStacks * this.config.progression.staminaCostIncreasePerRoom
  }

  /** A cleared room adds at least one stack, rising to one per full active-combat interval. */
  private completedRoomStaminaCostStacks(): number {
    return Math.max(
      1,
      Math.floor(this.roomElapsedMs / this.config.progression.staminaCostIncreaseIntervalMs),
    )
  }

  private canAffordStamina(cost: number): boolean {
    return cost <= 0 || this.player.stamina >= cost
  }

  /** An unaffordable action does not happen at all; the HUD blinks the bar instead. */
  private refuseForStamina(hand: LastChancesHand): void {
    this.staminaRefusedAtMs = this.elapsedMs
    this.feedbackController.emit({ state: 'blocked', profile: 'blocked', hand })
    this.emitSnapshot(true)
  }

  /**
   * Charges an executed action and pays back the skill refunds. Alternating hands and continuing
   * a chain both refund, and they stack; the debit itself is never waived, so a chained tap is a
   * wash rather than free.
   */
  private settleStaminaForAttack(hand: LastChancesHand, cost: number): void {
    const stamina = this.config.stamina
    const chained = this.lastAttackHand !== null
      && this.elapsedMs - this.lastAttackAtMs <= stamina.comboWindowMs
    let restore = chained ? stamina.comboRestore : 0
    if (this.lastAttackHand !== null && this.lastAttackHand !== hand) {
      restore += stamina.handAlternationRestore
    }
    this.player.stamina = clamp(
      this.player.stamina - cost + this.scaleStaminaGain(restore),
      0,
      this.effectivePlayerStats().maxStamina,
    )
    this.lastAttackHand = hand
    this.lastAttackAtMs = this.elapsedMs
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

  /** A chain cannot survive a room change or a death; the next action starts a fresh one. */
  private resetStaminaChain(): void {
    this.lastAttackHand = null
    this.lastAttackAtMs = Number.NEGATIVE_INFINITY
    this.staminaRefusedAtMs = null
  }

  private resetTapCombos(): void {
    for (const hand of LAST_CHANCES_HANDS) {
      this.tapCombos[hand].step = 0
      this.tapCombos[hand].expiresAtMs = 0
      this.closePierceMash(hand)
    }
  }

  /** Ends the «Прокол» mash window; the next tap walks the ordinary Охота chain again. */
  private closePierceMash(hand: LastChancesHand): void {
    this.pierceMash[hand].expiresAtMs = 0
    this.pierceMash[hand].hits = 0
  }

  private pierceMashActive(hand: LastChancesHand): boolean {
    return this.pierceMash[hand].expiresAtMs > this.elapsedMs
  }

  private executeAttack(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    if (attack.behavior === 'bowRapidFire' || attack.behavior === 'bowRain') {
      if (context.resolution.atMs
        <= this.bowChannelReleasedAtInputMs[context.hand] + 0.5) return
      if (attack.behavior === 'bowRapidFire'
        && !this.bowRapidPrefixReady(context.hand, context.weapon.id)) {
        if (!this.bowRapidFirstShotReady(context.hand, context.weapon.id)) return
        const secondShot = attackWithLastChancesAugment(
          context.weapon.attacks.doubleTap,
          context.weapon,
        )
        this.executeAttack(secondShot, direction, {
          ...context,
          gesture: 'doubleTap',
        })
      }
      const started = this.startBowChannel(
        context.hand,
        context.weapon,
        context.gesture as 'doubleTapHold' | 'hold',
        attack,
        context.resolution.atMs,
      )
      this.immediateHandInput[context.hand].bowChannelStarted = started
      return
    }
    if (attack.behavior === 'bowScatter') {
      this.performBowScatter(attack, direction, context)
      return
    }
    if (attack.behavior === 'bowDodge') {
      this.performDash(attack, this.bowMobilityDirection(direction), context)
      return
    }
    if (attack.behavior === 'bowJump') {
      this.performBowJump(attack, this.bowMobilityDirection(direction), context)
      return
    }
    if (attack.behavior === 'bowRiposte') {
      this.performBowRiposte(attack, direction, context)
      return
    }
    if (attack.behavior === 'bowIgnite') {
      this.performBowIgnition(attack, context)
      return
    }
    if (attack.behavior === 'bowDoubleShot') {
      const target = this.bowLastShotTargets[context.hand]
      const retainedDirection = target
        ? normalize({
            x: target.x - this.player.position.x,
            y: target.y - this.player.position.y,
          }, this.bowLastShotDirections[context.hand] ?? direction)
        : this.bowLastShotDirections[context.hand] ?? direction
      this.performProjectile(
        attack,
        retainedDirection,
        context,
      )
      this.bowDoubleShotWeaponIds[context.hand] = context.weapon.id
      this.bowDoubleShotAtMs[context.hand] = this.elapsedMs
      this.bowLastShotTargets[context.hand] = null
      this.bowLastShotWeaponIds[context.hand] = null
      this.bowLastShotAtMs[context.hand] = Number.NEGATIVE_INFINITY
      return
    }
    if (attack.behavior === 'spearRelease') {
      this.performSpearRelease(attack, direction, context)
      return
    }
    if (attack.behavior === 'spearReleaseV2') {
      this.performSpearReleaseV2(attack, direction, context)
      return
    }
    if (attack.behavior === 'spearOverheadSpin') {
      this.performSpearOverheadSpin(attack, direction, context)
      return
    }
    if (attack.kind === 'melee') this.performMelee(attack, direction, context)
    if (attack.kind === 'projectile') this.performProjectile(attack, direction, context)
    if (attack.kind === 'dash') this.performDash(attack, direction, context)
    if (attack.kind === 'burst') this.performBurst(attack, direction, context)
  }

  private bowMobilityDirection(fallback: LastChancesVector): LastChancesVector {
    const movement = this.resolveMovement()
    return vectorLength(movement) > this.config.input.actionDirectionDeadZone
      ? normalize(movement, fallback)
      : normalize(fallback)
  }

  private performBowJump(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    const dash = this.activeDash
    if (!dash
      || dash.weaponId !== context.weapon.id
      || dash.hand !== context.hand
      || dash.attack.behavior !== 'bowDodge') return
    const inheritedLift = this.bowDashLiftRadii(dash)
    const seconds = Math.max(0.08, attack.durationMs / 1000)
    dash.direction = normalize(direction, dash.direction)
    dash.remainingDistance += attack.range
    // The entire extended path must finish inside the authored jump i-frame duration.
    dash.speed = Math.max(1, dash.remainingDistance / seconds)
    dash.attack = { ...attack }
    dash.bowLiftStartRadii = inheritedLift
    dash.color = attack.color
    dash.elapsedMs = 0
    dash.remainingHits = 0
    dash.hitIds.clear()
    dash.hitRecords.clear()
    this.bowResponseWindows[context.hand] = {
      weaponId: context.weapon.id,
      startedAtInputMs: context.resolution.atMs,
    }
    this.effects.push({
      kind: 'dash',
      position: { ...this.player.position },
      direction: dash.direction,
      range: attack.range,
      radius: attack.radius,
      arcDegrees: attack.arcDegrees,
      color: attack.color,
      remainingMs: Math.max(180, attack.durationMs),
      totalMs: Math.max(180, attack.durationMs),
    })
  }

  private performBowRiposte(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    const response = this.bowResponseWindows[context.hand]
    const heldMs = response
      ? Math.max(0, context.resolution.atMs - response.startedAtInputMs)
      : context.resolution.heldMs
    const goldStartMs = tuningValue(attack, 'goldStartMs', 470)
    const goldEndMs = Math.max(goldStartMs, tuningValue(attack, 'goldEndMs', 530))
    const lateShotMs = Math.max(goldEndMs, tuningValue(attack, 'lateShotMs', goldEndMs))
    if (heldMs < goldStartMs) return
    const primary = this.weapons.get('left')
    if (!isLongbowPrimary(primary)) return
    const golden = heldMs <= goldEndMs
    if (!golden && heldMs < lateShotMs) return
    let shot: LastChancesAttackDefinition
    if (golden) {
      const source = attackWithLastChancesAugment(primary.attacks.hold, primary)
      shot = this.resolveBowDrawAttack(source, source.charge?.maxMs ?? heldMs, true).attack
      shot = {
        ...shot,
        name: 'Ответ · золотой Залп',
        behavior: 'bowDraw',
        damage: shot.damage * tuningValue(attack, 'goldDamageMultiplier', 1.22),
        range: shot.range * tuningValue(attack, 'goldRangeMultiplier', 1.12),
        color: '#ffd76a',
      }
      this.bowShotPresentation.goldenUntilMs = this.elapsedMs + 700
    } else {
      shot = attackWithLastChancesAugment(primary.attacks.tap, primary)
      shot = { ...shot, name: 'Ответ · Шот', behavior: 'bowShot' }
    }
    const delayed: RuntimeDelayedAttack = {
      remainingMs: 0,
      attack: shot,
      direction: normalize(this.player.aim, direction),
      context: {
        weapon: primary,
        hand: 'left',
        gesture: 'doubleTapHold',
        resolution: context.resolution,
        storedDot: null,
      },
    }
    this.bowResponseWindows[context.hand] = null
    if (this.activeDash?.weaponId === context.weapon.id
      && this.activeDash.hand === context.hand
      && this.activeDash.attack.behavior === 'bowJump') {
      delayed.waitForBowDashWeaponId = context.weapon.id
      this.delayedAttacks.push(delayed)
    } else {
      this.executeDelayedAttack(delayed)
    }
  }

  private performBowScatter(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    const primary = this.weapons.get('left')
    if (!isLongbowPrimary(primary)) return
    const released = this.releasedBowDraw?.hand === context.hand
      && this.elapsedMs - this.releasedBowDraw.releasedAtMs
        <= this.config.input.holdThenDoubleTapWindowMs + 120
      ? this.releasedBowDraw
      : null
    const volleyDirection = normalize(released?.direction ?? direction)
    // Mouse/legacy input already loosed the fully charged main arrow on release. Semantic
    // dispatch can arrive without that edge, so retain one fallback main shot without ever
    // duplicating the release arrow.
    if (!released) {
      const draw = attackWithLastChancesAugment(primary.attacks.hold, primary)
      const finalBand = draw.charge?.bands.at(-1)
      const main = resolveLastChancesChargedAttack(
        draw,
        draw.charge?.maxMs ?? context.resolution.firstHoldMs,
        finalBand?.id,
      ).attack
      this.performProjectile({ ...main, behavior: 'bowDraw' }, volleyDirection, {
        ...context,
        weapon: primary,
        hand: 'left',
        gesture: 'holdThenDoubleTap',
      })
    }

    const count = Math.max(1, Math.round(tuningValue(attack, 'scatterCount', 7)))
    const offsets = lastChancesCenteredFanOffsets(
      count,
      Math.max(0, tuningValue(attack, 'fanDegrees', 52)),
    )
    for (const offset of offsets) {
      this.performProjectile(
        {
          ...attack,
          kind: 'projectile',
          behavior: 'bowScatter',
          pierce: 0,
        },
        rotateVector(volleyDirection, offset),
        context,
      )
    }
    // Seven projectile spawns all update the shared shot presentation. Restore the authored
    // centre line so the hands and bow recoil along the volley, not along its last fan edge.
    this.bowShotPresentation = {
      atMs: this.elapsedMs,
      direction: { ...volleyDirection },
      goldenUntilMs: this.elapsedMs + 700,
    }
    this.releasedBowDraw = null
  }

  private performBowIgnition(
    attack: LastChancesAttackDefinition,
    context: AttackExecutionContext,
  ): void {
    this.bowRainReleasedAtInputMs[context.hand] = Number.NEGATIVE_INFINITY
    const ordinaryRadius = Math.max(1, tuningValue(attack, 'ordinaryRadius', 54))
    const ordinaryDamage = Math.max(0, tuningValue(attack, 'ordinaryDamage', 18))
    const chemicalRadius = Math.max(ordinaryRadius, tuningValue(attack, 'chemicalRadius', 126))
    const chemicalDamage = Math.max(ordinaryDamage, tuningValue(attack, 'chemicalDamage', 48))
    const chemicalSelfDamage = Math.max(0, tuningValue(attack, 'chemicalSelfDamage', 34))
    for (const arrow of this.embeddedArrows) {
      if (arrow.exploded) continue
      const chemical = arrow.chemical
      const radius = chemical ? chemicalRadius : ordinaryRadius
      const damage = chemical ? chemicalDamage : ordinaryDamage
      const explosionAttack: LastChancesAttackDefinition = {
        ...attack,
        name: chemical ? 'Огонь! · химический взрыв' : 'Огонь! · искры',
        behavior: 'bowIgnite',
        kind: 'burst',
        damage,
        radius,
        range: radius,
        hitEffects: [{
          status: 'burn',
          durationMs: chemical ? 5200 : 3200,
          stacks: chemical ? 2 : 1,
          tickDamage: chemical ? 2.4 : 1.2,
          tickMs: 600,
          refresh: 'stack',
        }],
      }
      for (const enemy of this.enemies) {
        if (enemy.state === 'dead') continue
        const reach = radius + enemy.definition.radius
        if (distanceSquared(arrow.position, enemy.position) > reach * reach) continue
        const blastDirection = normalize({
          x: enemy.position.x - arrow.position.x,
          y: enemy.position.y - arrow.position.y,
        }, arrow.direction)
        this.damageEnemy(enemy, explosionAttack, attack.knockback, blastDirection, {
          weaponId: context.weapon.id,
          hand: context.hand,
          gesture: context.gesture,
          distance: Math.sqrt(distanceSquared(this.player.position, enemy.position)),
        })
      }
      if (chemical
        && distanceSquared(arrow.position, this.player.position)
          <= (radius + this.config.player.radius) ** 2) {
        this.damagePlayerUnavoidable(chemicalSelfDamage, 'Химический взрыв стрелы')
      }
      arrow.exploded = true
      arrow.chemical = false
      this.effects.push({
        kind: 'shock',
        position: { ...arrow.position },
        direction: arrow.direction,
        range: radius,
        radius,
        arcDegrees: 360,
        color: chemical ? '#ff8a3d' : '#ffcf66',
        remainingMs: chemical ? 620 : 360,
        totalMs: chemical ? 620 : 360,
      })
    }
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
    direction = normalize(direction)
    const speed = Math.max(1, attack.projectileSpeed)
    const projectileRadius = Math.max(attack.radius, (attack.collider?.width ?? 0) / 2)
    const projectileSpawnOffset = Math.max(
      0,
      tuningValue(attack, 'projectileSpawnOffset', 0),
    )
    const projectileId = this.nextProjectileId
    const persistentArrow = context.weapon.trait === 'longbowPersistence'
      && isLongbowArrowBehavior(attack.behavior)
    const authoredSpawn = {
      x: this.player.position.x + direction.x
        * (this.config.player.radius + projectileRadius + 2 + projectileSpawnOffset),
      y: this.player.position.y + direction.y
        * (this.config.player.radius + projectileRadius + 2 + projectileSpawnOffset),
    }
    // A longbow muzzle can extend through a nearby wall even though the player remains on
    // its valid side. Start the arrow at the first swept surface instead of beyond it: the
    // first projectile update can then embed there or reflect a scatter arrow back into play.
    // Generic projectiles retain their authored spawn position and legacy collision contract.
    const muzzleImpact = persistentArrow
      && attack.collider?.passesThroughWalls !== true
      && this.currentNode
      ? sweepLastChancesCircleAgainstArena(
          this.player.position,
          authoredSpawn,
          projectileRadius,
          this.currentNode.arena,
        )
      : null
    const spawnPosition = muzzleImpact?.point ?? authoredSpawn
    this.projectiles.push({
      id: projectileId,
      position: spawnPosition,
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
      sourceId: 'player',
      attack: { ...attack },
      weaponId: context.weapon.id,
      hand: context.hand,
      gesture: context.gesture,
      storedDot: context.storedDot,
      ...(persistentArrow
        ? {
            persistentArrow: true,
            chemicalArrow: context.weapon.augment === 'chemical',
            ricochetsRemaining: Math.max(
              0,
              Math.round(tuningValue(attack, 'ricochets', 0)),
            ),
          }
        : {}),
      ...(isSpearReleaseBehavior(attack.behavior) && context.chargeBandId === 'late'
        ? { carriedIds: new Set<string>() }
        : {}),
    })
    this.nextProjectileId += 1
    if (persistentArrow) {
      this.bowLastShotDirections[context.hand] = { ...direction }
      if (attack.behavior === 'bowShot') {
        this.bowLastShotTargets[context.hand] = this.bowAimTarget(attack.range, projectileRadius)
        this.bowLastShotWeaponIds[context.hand] = context.weapon.id
        this.bowLastShotAtMs[context.hand] = this.elapsedMs
      }
      this.bowShotPresentation = {
        atMs: this.elapsedMs,
        direction: { ...direction },
        goldenUntilMs: this.bowShotPresentation.goldenUntilMs,
      }
    }
    if (!isSpearReleaseBehavior(attack.behavior)) this.addEffect('hit', attack, direction)
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
            attack.behavior === 'katanaFlurry' || attack.behavior === 'spearBreakthrough'
              ? Math.max(1, attack.repeatHits ?? 1)
              : 1
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
    if (attack.behavior === 'spearBreakthrough') {
      this.activeDash.ram = {
        runMs: 0,
        releasedAtMs: null,
        speed: this.effectivePlayerStats().moveSpeed
          * tuningValue(attack, 'startSpeedMultiplier', 0.5),
        staminaAccumulatorMs: 0,
        streakAccumulatorMs: 0,
        tierCued: 0,
      }
      this.activeDash.speed = this.activeDash.ram.speed
    }
    if (attack.behavior === 'poleVault' && isSpearV2Secondary(context.weapon)) {
      const travelDistance = this.currentNode
        ? this.poleVaultTravelDistance(
            this.currentNode,
            this.player.position,
            direction,
            attack.range,
            this.config.player.radius,
          )
        : attack.range
      this.activeDash.attack.range = travelDistance
      this.activeDash.remainingDistance = travelDistance
      this.activeDash.speed = travelDistance / durationSeconds
      this.activeDash.poleVault = {
        runMs: Math.max(0, tuningValue(attack, 'runMs', 180)),
        plantMs: Math.max(0, tuningValue(attack, 'plantMs', 120)),
        riseMs: Math.max(1, tuningValue(attack, 'riseMs', 180)),
        flightMs: Math.max(1, tuningValue(attack, 'flightMs', 420)),
        landMs: Math.max(1, tuningValue(attack, 'landMs', 150)),
        runDistanceRatio: clamp(tuningValue(attack, 'runDistanceRatio', 0.18), 0, 0.8),
      }
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
      let stanceSweepColliders: LastChancesRuntimeCollider[] = []
      let facingTurnRadians = 0
      if (area.attack.collider?.followsPlayer) {
        const previousOrigin = { ...area.origin }
        const previousDirection = normalize(area.direction)
        const nextDirection = normalize(this.player.aim, previousDirection)
        facingTurnRadians = Math.atan2(
          previousDirection.x * nextDirection.y - previousDirection.y * nextDirection.x,
          previousDirection.x * nextDirection.x + previousDirection.y * nextDirection.y,
        )
        area.origin = { ...this.player.position }
        area.direction = nextDirection
        if (area.attack.behavior === 'spearStance') {
          const previousCollider = resolveAttackCollider(
            previousOrigin,
            previousDirection,
            area.attack,
          )
          const currentCollider = resolveAttackCollider(
            area.origin,
            nextDirection,
            area.attack,
          )
          const previousTip = previousCollider.shape === 'capsule'
            ? previousCollider.end
            : previousOrigin
          const currentTip = currentCollider.shape === 'capsule'
            ? currentCollider.end
            : area.origin
          area.stanceTipVelocity = {
            x: (currentTip.x - previousTip.x) / Math.max(EPSILON, deltaMs / 1000),
            y: (currentTip.y - previousTip.y) / Math.max(EPSILON, deltaMs / 1000),
          }
          area.stanceCutSpeed = Math.abs(
            area.stanceTipVelocity.x * -nextDirection.y
              + area.stanceTipVelocity.y * nextDirection.x,
          )
          stanceSweepColliders = this.spearStanceSweepColliders(
            previousOrigin,
            previousDirection,
            area.origin,
            nextDirection,
            area.attack,
          )
        }
      }
      if (area.channel && area.attack.behavior === 'spearStance') {
        const tickMs = Math.max(1, tuningValue(area.attack, 'staminaPerTickMs', 100))
        const tickCost = Math.max(0, tuningValue(area.attack, 'staminaPerTick', 2))
          * this.staminaCostMultiplier()
        area.channelStaminaAccumulatorMs += deltaMs
        while (area.channelStaminaAccumulatorMs >= tickMs && this.player.stamina > 0) {
          area.channelStaminaAccumulatorMs -= tickMs
          this.player.stamina = Math.max(0, this.player.stamina - tickCost)
        }
        if (tickCost > 0 && this.player.stamina <= 0) {
          area.remainingMs = 0
          if (this.heldChannels.get(area.hand) === area) this.heldChannels.delete(area.hand)
          this.refuseForStamina(area.hand)
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
        const matchingMovement = Math.abs(movementTurn) > Math.max(
          0,
          tuningValue(area.attack, 'assistMovementThreshold', 0.2),
        )
          && Math.sign(movementTurn) === rotationSign
        const matchingFacingTurn = Math.abs(facingTurnRadians) > Math.max(
          0,
          tuningValue(area.attack, 'assistFacingTurnRadians', 0.012),
        )
          && Math.sign(facingTurnRadians) === rotationSign
        if (rotationCanBeAssisted && (matchingMovement || matchingFacingTurn)) {
          area.rotationAssisted = true
        }
        const assist = rotationCanBeAssisted && area.rotationAssisted
          ? Math.max(1, tuningValue(area.attack, 'assistMultiplier', 2))
          : 1
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
        if (stanceSweepColliders.length > 0) {
          for (const collider of stanceSweepColliders) {
            this.addColliderTrace(collider, area.attack)
            this.applyActiveAreaHits(area, collider)
          }
          area.traceAccumulatorMs = 0
        } else if (area.traceAccumulatorMs >= 45) {
          area.traceAccumulatorMs = 0
          this.addColliderTrace(this.activeAreaCollider(area), area.attack)
          this.applyActiveAreaHits(area)
        } else {
          this.applyActiveAreaHits(area)
        }
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
        && nextHit % Math.max(
          1,
          Math.round(tuningValue(area.attack, 'dodgeEveryHits', 2)),
        ) === 0) {
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
      if (isSpearSpinBehavior(area.attack.behavior)) {
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
      const stanceDamageMultiplier = area.attack.behavior === 'spearStance'
        ? this.spearStanceDamageMultiplier(area, enemy)
        : 1
      this.damageEnemy(enemy, resolvedAttack, area.attack.knockback, knockbackDirection, {
        weaponId: area.weaponId,
        hand: area.hand,
        gesture: area.gesture,
        storedDot: area.storedDot,
        distance: vectorLength(toEnemy),
        damageMultiplier: (1 + area.motionDamageBonus) * stanceDamageMultiplier,
        impactIntensity: area.attack.behavior === 'swordRhythm' ? 0 : undefined,
      })
    }
  }

  private spearStanceDamageMultiplier(
    area: RuntimeActiveArea,
    enemy: RuntimeEnemy,
  ): number {
    const direction = normalize(area.direction)
    const relativeVelocity = {
      x: area.stanceTipVelocity.x - enemy.velocity.x,
      y: area.stanceTipVelocity.y - enemy.velocity.y,
    }
    const closingSpeed = Math.max(
      0,
      relativeVelocity.x * direction.x + relativeVelocity.y * direction.y,
    )
    const cuttingSpeed = Math.abs(
      relativeVelocity.x * -direction.y + relativeVelocity.y * direction.x,
    )
    const stationary = Math.max(0, tuningValue(area.attack, 'stationaryDamageMultiplier', 0.3))
    const piercing = stationary + closingSpeed
      / Math.max(1, tuningValue(area.attack, 'pierceReferenceSpeed', 420))
      * Math.max(0, tuningValue(area.attack, 'pierceSpeedDamageMultiplier', 1.8))
    const cutting = stationary + Math.max(area.stanceCutSpeed, cuttingSpeed)
      / Math.max(1, tuningValue(area.attack, 'cutReferenceSpeed', 900))
      * Math.max(0, tuningValue(area.attack, 'cutSpeedDamageMultiplier', 0.95))
    return Math.max(stationary, piercing, cutting)
  }

  /**
   * The stance tip may cross a target between rendered aim samples. Sweep both ends of the
   * short tip capsule so collision and the visible trace cover that complete cutting motion.
   */
  private spearStanceSweepColliders(
    previousOrigin: LastChancesVector,
    previousDirection: LastChancesVector,
    currentOrigin: LastChancesVector,
    currentDirection: LastChancesVector,
    attack: LastChancesAttackDefinition,
  ): LastChancesRuntimeCollider[] {
    const previous = resolveAttackCollider(previousOrigin, previousDirection, attack)
    const current = resolveAttackCollider(currentOrigin, currentDirection, attack)
    if (previous.shape !== 'capsule' || current.shape !== 'capsule') return [current]
    return [
      previous,
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
      current,
    ]
  }

  private activeAreaCollider(
    area: RuntimeActiveArea,
    sweepDegrees = area.sweepDegrees,
  ): LastChancesRuntimeCollider {
    const sideways = this.sidewaysAttackCollider(area.origin, area.direction, area.attack)
    if (sideways) return sideways
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
    const direction = area.attack.behavior === 'chainThrow'
      && vectorLength(movement) > this.config.input.actionDirectionDeadZone
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
          innerRange: tuningValue(attack, 'earlyInnerRange', 52),
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
          {
            status: 'stun' as const,
            durationMs: tuningValue(attack, 'middleStunMs', 1000),
          },
        ],
      }
      this.performProjectile(
        mediumAttack,
        this.spearReleaseDirection(mediumAttack, direction, context.chargeBandId),
        context,
      )
      return
    }
    this.performProjectile({ ...attack, kind: 'projectile' }, direction, context)
  }

  /**
   * The v2 замах. Its middle and late releases are the original lance's — throw-and-stun, then
   * the piercing wall-pin — but the early release is no longer the wide slash. That slash moved
   * onto the early «Акали»; a quick release now plants the spear into the ground in front of the
   * player as «Заколоть»: a small, close volume that hits far harder than the sweep it replaced.
   *
   * Note the inverted inner range. Every other spear action protects its shaft with a 52-unit
   * dead zone; «Заколоть» is explicitly a point-blank stab, so that exclusion is lifted here.
   */
  private performSpearReleaseV2(
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
          innerRange: tuningValue(attack, 'earlyInnerRange', 0),
          strictInnerRange: false,
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
          {
            status: 'stun' as const,
            durationMs: tuningValue(attack, 'middleStunMs', 1000),
          },
        ],
      }
      this.performProjectile(
        mediumAttack,
        this.spearReleaseDirection(mediumAttack, direction, context.chargeBandId),
        context,
      )
      return
    }
    this.performProjectile({ ...attack, kind: 'projectile' }, direction, context)
  }

  /**
   * «Акали». The charged bands are the original overhead spin; the new `spin-early` band is the
   * wide rassekatel inherited from v1's early замах, so a follow-up tap is finally worth
   * something during the quick-release window — at a fraction of a full spin's output.
   */
  private performSpearOverheadSpin(
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    context: AttackExecutionContext,
  ): void {
    if (context.chargeBandId === 'spin-early') {
      this.performMelee({
        ...attack,
        kind: 'melee',
        collider: {
          ...(attack.collider ?? { traceMs: 900 }),
          shape: 'sector',
          innerRange: tuningValue(attack, 'spinEarlyInnerRange', 52),
          strictInnerRange: true,
          rotationDegrees: undefined,
          followsPlayer: false,
        },
      }, direction, context)
      return
    }
    this.performBurst(attack, direction, context)
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
      .filter(candidate => candidate.distance >= Math.max(
        tuningValue(attack, 'middleTargetMinimumRange', 80),
        attack.range * tuningValue(attack, 'middleTargetMinimumRangeRatio', 0.25),
      )
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
    const now = performance.now()
    for (const channel of [...this.bowChannels.values()]) {
      this.settleBowChannelToInputTime(channel.hand, now)
    }
    this.stopAllBowChannels(true)
  }

  private cancelHeldChannels(): void {
    if (this.heldChannels.size > 0) {
      const heldAreas = new Set(this.heldChannels.values())
      this.activeAreas = this.activeAreas.filter(area => !heldAreas.has(area))
      this.heldChannels.clear()
    }
    this.stopAllBowChannels(false)
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
      const pressEdgeContinuation = state.techniquePressCommitted
        && this.bowResponseWindows[hand]?.weaponId === weapon?.id
      const continuation = state.techniqueContinuationPressed || pressEdgeContinuation
      const intent = continuation ? 'strike' : state.mobilityPressed ? 'mobility' : 'technique'
      const phase = continuation
        ? 'press'
        : state.mobilityPressed
        ? state.mobilityHeldMs >= (this.config.input.mylorik?.techniqueHoldMs ?? 0)
          ? 'hold'
          : 'press'
        : state.techniqueArmed ? 'hold' : 'tap'
      const candidate = controls?.mylorik.activations
        .filter(activation => activation.intent === intent)
        .filter(activation => continuation
          ? activation.context === 'continuation'
          : activation.phase === phase && activation.context === undefined)
        .sort((left, right) => right.priority - left.priority)[0]?.gesture ?? null
      const heldMs = continuation
        ? pressEdgeContinuation ? state.techniqueHeldMs : state.techniqueContinuationHeldMs
        : state.mobilityPressed ? state.mobilityHeldMs : state.techniqueHeldMs
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
        sequence: pressed ? continuation ? 'secondTap' : 'first' : null,
        candidateGesture: candidate,
        pendingChargeMs: heldMs,
      }
    }
    const state = this.dualSenseControls.snapshot(runtimeHandToPhysicalCluster(hand), atMs)
    const node = controls?.dualsense.nodes.find(candidate => candidate.id === state.nodeId)
    const sequence = node?.gesture === 'doubleTap' || node?.gesture === 'doubleTapHold'
      ? 'secondTap'
      : 'first'
    return {
      hand,
      phase: state.active ? 'pressing' : 'idle',
      pressed: state.active,
      progress: state.value,
      remainingMs: 0,
      heldMs: state.heldMs,
      sequence: state.active ? sequence : null,
      candidateGesture: node?.gesture ?? null,
      pendingChargeMs: state.heldMs,
    }
  }

  private updateFangCooldownFeedback(): void {
    if (this.controlSchemeValue !== 'dualsense') {
      this.fangCooldownActive.left = null
      this.fangCooldownActive.right = null
      return
    }
    for (const physicalHand of LAST_CHANCES_HANDS) {
      const hand = physicalClusterToRuntimeHand(physicalHand)
      const weapon = this.weapons.get(hand)
      if (weapon?.trait !== 'ouroborosFang') {
        this.fangCooldownActive[physicalHand] = null
        continue
      }
      const cooldownActive = (this.cooldownEnds.get(cooldownKey(hand, 'tap')) ?? 0)
        > this.elapsedMs
      const previous = this.fangCooldownActive[physicalHand]
      this.fangCooldownActive[physicalHand] = cooldownActive
      if (previous === cooldownActive) continue
      if (previous === null) {
        if (cooldownActive) this.pushTriggerBaseline()
        continue
      }
      this.pushTriggerBaseline()
      if (previous && !cooldownActive) {
        this.feedbackController.emit({
          state: 'ready',
          profile: 'click',
          hand: physicalHand,
          tick: { durationMs: 28, magnitude: 0.18 },
        })
      }
    }
  }

  private updateHeldWeaponMechanics(deltaMs: number): void {
    const now = this.frameNowMs || performance.now()
    this.updateFangCooldownFeedback()
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
      const activeNode = this.controlSchemeValue === 'dualsense'
        ? weapon.controls?.dualsense.nodes.find((node) => (
            node.id === this.dualSenseControls
              .snapshot(runtimeHandToPhysicalCluster(hand), now).nodeId
          ))
        : undefined
      const activeBand = input.pressed && candidateAttack?.charge
        ? resolveLastChancesChargedAttack(
            candidateAttack,
            input.heldMs,
            activeNode?.chargeBandOverrideId,
          ).band ?? undefined
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
        && (input.sequence === 'secondTap' || input.candidateGesture === 'doubleTapHold')
        && this.gestureReady(hand, 'doubleTapHold')
        && weapon.attacks.doubleTapHold.behavior === 'spearKick') {
        this.player.armorMultiplier = tuningValue(
          weapon.attacks.doubleTapHold,
          'armorMultiplier',
          2,
        )
        this.player.armorMultiplierMs = Math.max(this.player.armorMultiplierMs, deltaMs + 80)
      }
      if (weapon.attacks.doubleTapHold.behavior === 'spearBreakthrough') {
        this.updateBreakthroughInput(hand, input, now)
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
        && (channelBehavior !== 'spearStance' || this.player.stamina > 0)
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

  /**
   * Longbow holds are real-time verbs rather than release-only attacks. Чреда and Обстрел emit
   * arrows while the button is down; Натяг accrues its exact stamina debit so the golden window
   * can refund precisely what this one draw consumed.
   */
  private bowDrawInputActive(
    hand: LastChancesHand,
    input: LastChancesGestureInputSnapshot,
  ): boolean {
    const schemeOwnsDrawRoute = this.controlSchemeValue !== 'dualsense'
      || input.candidateGesture === 'hold'
      || input.candidateGesture === 'holdThenDoubleTap'
    return input.pressed
      && !this.bowDrawConsumedUntilRelease[hand]
      && input.sequence === 'first'
      && schemeOwnsDrawRoute
      && input.candidateGesture !== 'doubleTapHold'
      && input.candidateGesture !== 'holdThenDoubleTap'
  }

  private updateBowHeldMechanics(_deltaMs: number): void {
    const now = this.frameNowMs || performance.now()
    if (this.releasedBowDraw
      && this.elapsedMs - this.releasedBowDraw.releasedAtMs
        > this.config.input.holdThenDoubleTapWindowMs + 120) {
      this.releasedBowDraw = null
    }
    for (const hand of LAST_CHANCES_HANDS) {
      const weapon = this.weapons.get(hand)
      if (!weapon || weapon.trait !== 'longbowPersistence') {
        this.stopBowChannel(hand, false)
        this.resetBowDrawDebit(hand)
        continue
      }
      const input = this.controlInputSnapshot(hand, now)
      if (!input.pressed) {
        this.bowDrawConsumedUntilRelease[hand] = false
        this.bowChannelConsumedUntilRelease[hand] = false
      }
      let existing = this.bowChannels.get(hand)
      if (existing) {
        this.settleBowChannelToInputTime(hand, now)
        existing = this.bowChannels.get(hand)
      }
      const stillHoldingExisting = existing
        && input.pressed
        && (existing.gesture === 'doubleTapHold'
          ? input.sequence === 'secondTap'
            || input.candidateGesture === 'doubleTapHold'
          : input.sequence === 'first'
            || input.candidateGesture === 'hold')
      if (existing && !stillHoldingExisting) this.stopBowChannel(hand, true)

      if (isLongbowPrimary(weapon)) {
        const rapid = weapon.attacks.doubleTapHold
        const rapidGateMs = Math.max(0, tuningValue(rapid, 'channelStartMs', 220))
        const rapidHeld = input.pressed
          && (input.sequence === 'secondTap' || input.candidateGesture === 'doubleTapHold')
          && input.heldMs >= rapidGateMs
        if (rapidHeld
          && !this.bowChannels.has(hand)
          && !this.bowChannelConsumedUntilRelease[hand]
          && this.gestureReady(hand, 'doubleTapHold')) {
          this.immediateHandInput[hand].bowChannelStarted = this.startBowChannel(
            hand,
            weapon,
            'doubleTapHold',
            rapid,
            now - Math.max(0, input.heldMs - rapidGateMs),
          )
        }

        const drawing = this.bowDrawInputActive(hand, input)
        if (drawing) {
          this.accrueBowDrawDebit(hand, weapon.attacks.hold, input.heldMs)
        } else {
          this.bowDrawDebits[hand].active = false
        }
      }

      if (isLongbowSecondary(weapon)) {
        const rain = weapon.attacks.hold
        const rainHeld = input.pressed
          && (input.sequence === 'first' || input.candidateGesture === 'hold')
          && this.bowResponseWindows[hand] === null
          && input.heldMs >= this.config.input.holdMs
        if (rainHeld
          && !this.bowChannels.has(hand)
          && !this.bowChannelConsumedUntilRelease[hand]
          && this.gestureReady(hand, 'hold')) {
          this.immediateHandInput[hand].bowChannelStarted = this.startBowChannel(
            hand,
            weapon,
            'hold',
            rain,
            now - Math.max(0, input.heldMs - this.config.input.holdMs),
          )
        }
      }
    }
    // A hold gate may have been crossed between animation frames; settle that exact tail now.
    for (const channel of [...this.bowChannels.values()]) {
      this.settleBowChannelToInputTime(channel.hand, now)
    }
  }

  private startBowChannel(
    hand: LastChancesHand,
    weapon: LastChancesResolvedWeapon,
    gesture: RuntimeBowChannel['gesture'],
    sourceAttack: LastChancesAttackDefinition,
    startedAtInputMs = this.frameNowMs || performance.now(),
  ): boolean {
    if (this.bowChannels.has(hand) || this.bowChannelConsumedUntilRelease[hand]) return false
    const attack = attackWithLastChancesAugment(sourceAttack, weapon)
    if (attack.behavior === 'bowRain') {
      this.bowRainReleasedAtInputMs[hand] = Number.NEGATIVE_INFINITY
    }
    this.bowChannels.set(hand, {
      hand,
      weaponId: weapon.id,
      gesture,
      behavior: attack.behavior === 'bowRain' ? 'bowRain' : 'bowRapidFire',
      attack,
      elapsedMs: 0,
      shotAccumulatorMs: 0,
      staminaAccumulatorMs: 0,
      lastSettledAtInputMs: startedAtInputMs,
    })
    this.lastGesture = {
      hand,
      gesture,
      attackName: attack.name,
      atMs: this.elapsedMs,
    }
    if (attack.behavior === 'bowRapidFire') {
      this.bowDoubleShotWeaponIds[hand] = null
      this.bowDoubleShotAtMs[hand] = Number.NEGATIVE_INFINITY
    }
    return true
  }

  private updateBowChannels(deltaMs: number): void {
    for (const channel of [...this.bowChannels.values()]) {
      channel.lastSettledAtInputMs += Math.max(0, deltaMs)
      this.advanceBowChannel(channel, deltaMs)
    }
  }

  private settleBowChannelToInputTime(hand: LastChancesHand, atMs: number): void {
    const channel = this.bowChannels.get(hand)
    if (!channel) return
    const deltaMs = Math.max(0, atMs - channel.lastSettledAtInputMs)
    channel.lastSettledAtInputMs = Math.max(channel.lastSettledAtInputMs, atMs)
    if (deltaMs > 0) this.advanceBowChannel(channel, deltaMs)
  }

  /**
   * A hold can cross its gate and release entirely between animation frames. Materialize that
   * paid interval before stopping, then guard the release classifier from reopening the channel.
   */
  private settleBowChannelRelease(
    hand: LastChancesHand,
    atMs: number,
    heldMs: number,
    sequence: LastChancesGestureInputSnapshot['sequence'],
  ): void {
    const weapon = this.weapons.get(hand)
    if (weapon?.trait !== 'longbowPersistence') return
    // A capped or stamina-exhausted Rain has already removed its live channel while the
    // physical button is still down. Its consumed latch must preserve the same Hold + tap
    // continuation entitlement until this release edge.
    let releasedRain = this.bowChannels.get(hand)?.behavior === 'bowRain'
      || (isLongbowSecondary(weapon) && this.bowChannelConsumedUntilRelease[hand])
    this.bowChannelReleasedAtInputMs[hand] = Math.max(
      this.bowChannelReleasedAtInputMs[hand],
      atMs,
    )
    if (!this.bowChannels.has(hand)) {
      if (isLongbowSecondary(weapon)
        && this.bowResponseWindows[hand] === null
        && heldMs >= this.config.input.holdMs
        && !this.bowChannelConsumedUntilRelease[hand]
        && this.gestureReady(hand, 'hold')) {
        this.startBowChannel(
          hand,
          weapon,
          'hold',
          weapon.attacks.hold,
          atMs - Math.max(0, heldMs - this.config.input.holdMs),
        )
        releasedRain = this.bowChannels.get(hand)?.behavior === 'bowRain'
      } else if (isLongbowPrimary(weapon)) {
        const rapid = weapon.attacks.doubleTapHold
        const gateMs = Math.max(0, tuningValue(rapid, 'channelStartMs', 220))
        if (sequence === 'secondTap'
          && heldMs >= gateMs
          && this.gestureReady(hand, 'doubleTapHold')) {
          this.startBowChannel(
            hand,
            weapon,
            'doubleTapHold',
            rapid,
            atMs - Math.max(0, heldMs - gateMs),
          )
        }
      }
    }
    this.settleBowChannelToInputTime(hand, atMs)
    this.stopBowChannel(hand, true)
    if (releasedRain) this.bowRainReleasedAtInputMs[hand] = atMs
    this.bowChannelConsumedUntilRelease[hand] = false
  }

  private advanceBowChannel(channel: RuntimeBowChannel, deltaMs: number): void {
    const weapon = this.weapons.get(channel.hand)
    if (!weapon || weapon.id !== channel.weaponId) {
      this.stopBowChannel(channel.hand, false)
      return
    }
    if (channel.behavior === 'bowRain') {
      this.bowRainTarget = this.bowAimTarget(
        channel.attack.range,
        Math.max(1, tuningValue(channel.attack, 'zoneRadius', 105)),
      )
    }
    const tickMs = Math.max(1, tuningValue(channel.attack, 'staminaTickMs', 100))
    const tickCost = Math.max(0, tuningValue(
      channel.attack,
      'staminaPerTick',
      channel.behavior === 'bowRain' ? 6 : 5,
    )) * this.staminaCostMultiplier()
    const maximumMs = Math.max(1, tuningValue(channel.attack, 'channelMaxMs', 2000))
    const requestedMs = Math.min(deltaMs, Math.max(0, maximumMs - channel.elapsedMs))
    const staminaPerMs = tickCost / tickMs
    const affordableMs = staminaPerMs > EPSILON
      ? this.player.stamina / staminaPerMs
      : requestedMs
    const activeMs = Math.min(requestedMs, affordableMs)
    if (activeMs > 0) {
      const spent = activeMs * staminaPerMs
      this.player.stamina = Math.max(0, this.player.stamina - spent)
      channel.elapsedMs += activeMs
      channel.shotAccumulatorMs += activeMs
      channel.staminaAccumulatorMs += activeMs
    }

    const intervalMs = Math.max(1, tuningValue(
      channel.attack,
      channel.behavior === 'bowRain' ? 'arrowIntervalMs' : 'shotIntervalMs',
      channel.behavior === 'bowRain' ? 145 : 120,
    ))
    while (channel.shotAccumulatorMs >= intervalMs) {
      channel.shotAccumulatorMs -= intervalMs
      if (channel.behavior === 'bowRain') {
        this.spawnBowRainArrow(channel, weapon)
      } else {
        const context: AttackExecutionContext = {
          weapon,
          hand: channel.hand,
          gesture: channel.gesture,
          resolution: {
            hand: channel.hand,
            gesture: channel.gesture,
            atMs: this.elapsedMs,
            heldMs: channel.elapsedMs,
            firstHoldMs: 0,
          },
          storedDot: null,
        }
        this.performProjectile(channel.attack, normalize(this.player.aim), context)
      }
    }

    const exhausted = activeMs + EPSILON < requestedMs
    if (exhausted) {
      this.player.stamina = 0
      this.stopBowChannel(channel.hand, true)
      this.refuseForStamina(channel.hand)
    } else if (channel.elapsedMs >= maximumMs) {
      this.stopBowChannel(channel.hand, true)
    }
  }

  private stopBowChannel(hand: LastChancesHand, commit: boolean): void {
    const channel = this.bowChannels.get(hand)
    if (!channel) return
    this.bowChannels.delete(hand)
    if (commit) this.bowChannelConsumedUntilRelease[hand] = true
    if (channel.behavior === 'bowRain') this.bowRainTarget = null
    if (!commit) return
    this.cooldownEnds.set(
      cooldownKey(hand, channel.gesture),
      Math.max(
        this.cooldownEnds.get(cooldownKey(hand, channel.gesture)) ?? 0,
        this.elapsedMs + channel.attack.cooldownMs,
      ),
    )
    this.weaponActionEnds.delete(channel.weaponId)
    this.scheduleRecovery(channel.weaponId, channel.attack.recoveryMs, 0)
  }

  private stopAllBowChannels(commit: boolean): void {
    for (const hand of [...this.bowChannels.keys()]) this.stopBowChannel(hand, commit)
  }

  private resetBowDrawDebit(hand: LastChancesHand): void {
    this.bowDrawDebits[hand] = {
      active: false,
      accruedMs: 0,
      spent: 0,
      exhausted: false,
    }
  }

  /** Debits every newly observed millisecond once, including the physical release-frame tail. */
  private accrueBowDrawDebit(
    hand: LastChancesHand,
    attack: LastChancesAttackDefinition,
    heldMs: number,
  ): void {
    const debit = this.bowDrawDebits[hand]
    const maximumHoldMs = Math.max(
      attack.charge?.maxMs ?? 1000,
      tuningValue(attack, 'drawMaxHoldMs', 2000),
    )
    const accruedMs = Math.min(Math.max(0, heldMs), maximumHoldMs)
    const newMs = Math.max(0, accruedMs - debit.accruedMs)
    const staminaPerMs = Math.max(0, tuningValue(attack, 'staminaPerMs', 0.04))
    const effectiveRate = staminaPerMs * this.staminaCostMultiplier()
    const requested = newMs * effectiveRate
    const spent = Math.min(this.player.stamina, requested)
    const paidMs = effectiveRate > EPSILON ? spent / effectiveRate : newMs
    debit.active = true
    debit.accruedMs = Math.min(accruedMs, debit.accruedMs + paidMs)
    debit.spent += spent
    debit.exhausted = debit.exhausted || spent + EPSILON < requested
    this.player.stamina = Math.max(0, this.player.stamina - spent)
  }

  private consumeBowDrawDebit(hand: LastChancesHand, refund: boolean): number {
    const debit = this.bowDrawDebits[hand]
    const spent = debit.spent
    if (refund && spent > 0) {
      this.player.stamina = clamp(
        this.player.stamina + spent,
        0,
        this.effectivePlayerStats().maxStamina,
      )
    }
    this.resetBowDrawDebit(hand)
    this.bowDrawConsumedUntilRelease[hand] = true
    return spent
  }

  private bowAimTarget(range: number, radius = 0): LastChancesVector {
    const arena = this.currentNode?.arena
    const pointerOwnsAim = !this.retainedGamepadAim
      && vectorLength(this.gamepadAim) <= this.config.input.aimDeadZone
      && vectorLength(this.touchAim) <= this.config.input.aimDeadZone
      && this.pointerWorldTarget !== null
    let target = pointerOwnsAim
      ? { ...(this.pointerWorldTarget as LastChancesVector) }
      : {
          x: this.player.position.x + this.player.aim.x * range,
          y: this.player.position.y + this.player.aim.y * range,
        }
    const offset = {
      x: target.x - this.player.position.x,
      y: target.y - this.player.position.y,
    }
    if (vectorLength(offset) > range) {
      const direction = normalize(offset, this.player.aim)
      target = {
        x: this.player.position.x + direction.x * range,
        y: this.player.position.y + direction.y * range,
      }
    }
    if (!arena) return target
    return {
      x: clamp(target.x, radius, arena.width - radius),
      y: clamp(target.y, radius, arena.height - radius),
    }
  }

  private spawnBowRainArrow(
    channel: RuntimeBowChannel,
    weapon: LastChancesResolvedWeapon,
  ): void {
    const zoneRadius = Math.max(1, tuningValue(channel.attack, 'zoneRadius', 105))
    const center = this.bowRainTarget ?? this.bowAimTarget(channel.attack.range, zoneRadius)
    this.bowRainTarget = center
    const rng = createLastChancesRng(
      `${this.currentNode?.seed ?? 0}:bow-rain:${this.nextProjectileId}:${Math.floor(channel.elapsedMs)}`,
    )
    const angle = rng() * Math.PI * 2
    const distance = Math.sqrt(rng()) * zoneRadius
    const target = this.currentNode
      ? {
          x: clamp(
            center.x + Math.cos(angle) * distance,
            2,
            this.currentNode.arena.width - 2,
          ),
          y: clamp(
            center.y + Math.sin(angle) * distance,
            2,
            this.currentNode.arena.height - 2,
          ),
        }
      : center
    const fallMs = Math.max(120, tuningValue(channel.attack, 'fallMs', 520))
    this.fallingArrows.push({
      id: this.nextProjectileId++,
      target,
      direction: normalize({
        x: Math.cos(angle) * 0.18 + this.player.aim.x,
        y: Math.sin(angle) * 0.18 + this.player.aim.y,
      }),
      remainingMs: fallMs,
      totalMs: fallMs,
      attack: { ...channel.attack },
      weaponId: weapon.id,
      hand: channel.hand,
      gesture: channel.gesture,
      chemical: weapon.augment === 'chemical',
    })
  }

  private updateFallingArrows(deltaMs: number): void {
    const landed: RuntimeFallingArrow[] = []
    for (const arrow of this.fallingArrows) {
      arrow.remainingMs = Math.max(0, arrow.remainingMs - deltaMs)
      if (arrow.remainingMs <= 0) landed.push(arrow)
    }
    this.fallingArrows = this.fallingArrows.filter(arrow => arrow.remainingMs > 0)
    for (const arrow of landed) {
      const impactRadius = Math.max(1, tuningValue(arrow.attack, 'arrowImpactRadius', 10))
      for (const enemy of this.enemies) {
        if (enemy.state === 'dead') continue
        const reach = impactRadius + enemy.definition.radius
        if (distanceSquared(arrow.target, enemy.position) > reach * reach) continue
        this.damageEnemy(enemy, arrow.attack, arrow.attack.knockback, arrow.direction, {
          weaponId: arrow.weaponId,
          hand: arrow.hand,
          gesture: arrow.gesture,
          distance: Math.sqrt(distanceSquared(this.player.position, enemy.position)),
        })
      }
      this.embedArrow({
        id: arrow.id,
        position: arrow.target,
        direction: arrow.direction,
        attachment: 'floor',
        enemy: null,
        color: arrow.attack.color,
        chemical: arrow.chemical,
        planted: true,
      })
      this.effects.push({
        kind: 'hit',
        position: { ...arrow.target },
        direction: arrow.direction,
        range: impactRadius * 2,
        radius: impactRadius,
        arcDegrees: 360,
        color: arrow.attack.color,
        remainingMs: 180,
        totalMs: 180,
      })
    }
  }

  private updateEmbeddedArrows(): void {
    for (const arrow of this.embeddedArrows) {
      if (arrow.attachment !== 'enemy' || !arrow.enemyId || !arrow.enemyOffset) continue
      const enemy = this.enemies.find(candidate => candidate.id === arrow.enemyId)
      if (!enemy || enemy.state === 'dead') {
        arrow.attachment = 'floor'
        arrow.enemyId = null
        arrow.enemyOffset = null
        continue
      }
      arrow.position = {
        x: enemy.position.x + arrow.enemyOffset.x,
        y: enemy.position.y + arrow.enemyOffset.y,
      }
    }
  }

  private updateArrowChemicalPools(): void {
    for (const arrow of this.embeddedArrows) {
      if (!arrow.chemical || arrow.exploded || this.elapsedMs < arrow.nextChemicalTickAtMs) continue
      arrow.nextChemicalTickAtMs = this.elapsedMs + 650
      const radius = 46
      for (const enemy of this.enemies) {
        if (enemy.state === 'dead') continue
        const reach = radius + enemy.definition.radius
        if (distanceSquared(arrow.position, enemy.position) > reach * reach) continue
        applyLastChancesStatusEffects(enemy.statuses, [{
          status: 'chemical',
          durationMs: 2200,
          stacks: 1,
          tickDamage: 1.25,
          tickMs: 500,
          refresh: 'refresh',
        }], () => 0)
      }
    }
  }

  private embedArrow(options: {
    id: number
    position: LastChancesVector
    direction: LastChancesVector
    attachment: RuntimeEmbeddedArrow['attachment']
    enemy: RuntimeEnemy | null
    color: string
    chemical: boolean
    planted?: boolean
  }): void {
    const direction = normalize(options.direction)
    const position = { ...options.position }
    const enemyOffset = options.enemy
      ? {
          x: position.x - options.enemy.position.x,
          y: position.y - options.enemy.position.y,
        }
      : null
    this.embeddedArrows.push({
      id: options.id,
      position,
      direction,
      attachment: options.enemy ? 'enemy' : options.attachment,
      enemyId: options.enemy?.id ?? null,
      enemyOffset,
      color: options.color,
      chemical: options.chemical,
      exploded: false,
      embeddedAtMs: this.elapsedMs,
      nextChemicalTickAtMs: this.elapsedMs,
      ...(options.planted ? { planted: true } : {}),
    })
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
        unterhauExpiresAtMs: 0,
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
    // Only rushing the rhythm tires the arm. Waiting longer than the perfect window still reads
    // as 'late' for the overlay, but a patient player is never punished for it (M136).
    const missedTiming = !firstTap && interval < perfectStartMs
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

  /**
   * Clears the way for the next «Прокол» of a mash burst. Like the sword morph, the thrust that
   * is still live is interrupted rather than allowed to finish — that is what lets mashing faster
   * than the 165 ms swing actually land more thrusts instead of being rate-capped by it.
   *
   * The double-tap cooldown is dropped here and immediately re-stamped by `performAttack`, so the
   * last thrust of a burst leaves an ordinary cooldown behind and `gestureReady` needs no change.
   */
  private morphIntoPierce(weapon: LastChancesResolvedWeapon, hand: LastChancesHand): void {
    const morphing = this.activeAreas.filter(area => (
      area.weaponId === weapon.id
      && area.hand === hand
      && area.attack.behavior === 'spearPierce'
    ))
    for (const area of morphing) {
      this.addColliderTrace(this.activeAreaCollider(area), { ...area.attack, color: '#9fe4ff' })
    }
    if (morphing.length > 0) {
      this.activeAreas = this.activeAreas.filter(area => !morphing.includes(area))
    }
    this.weaponActionEnds.delete(weapon.id)
    this.cooldownEnds.delete(cooldownKey(hand, 'doubleTap'))
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
    state.unterhauExpiresAtMs = state.unterhauDueAtMs
      + Math.max(1, tuningValue(weapon, 'unterhauWindowMs', 900))
    if ((this.cooldownEnds.get(cooldownKey(hand, 'doubleTapHold')) ?? 0) > this.elapsedMs) {
      state.unterhauDueAtMs = 0
      state.unterhauExpiresAtMs = 0
    }
    state.unterhauTargetId = null
    state.unterhauTargetPosition = {
      x: this.player.position.x + direction.x * attack.range,
      y: this.player.position.y + direction.y * attack.range,
    }
    state.unterhauPrimed = false
  }

  private cancelPendingUnterhau(weapon: LastChancesResolvedWeapon): void {
    this.disarmPendingUnterhau(this.weaponState(weapon))
  }

  private disarmPendingUnterhau(state: RuntimeWeaponState): void {
    state.unterhauDueAtMs = 0
    state.unterhauExpiresAtMs = 0
    state.unterhauTargetId = null
    state.unterhauTargetPosition = null
    state.unterhauPrimed = false
  }

  /**
   * Drops a primed follow-up once its window has closed. It must key off `unterhauExpiresAtMs`,
   * never the due stamp: the due stamp shares its clock with the hold gate and would always
   * disarm the follow-up a frame before the player could reach it.
   */
  private expirePendingUnterhau(state: RuntimeWeaponState): void {
    if (state.unterhauDueAtMs <= 0) return
    if (this.elapsedMs < state.unterhauExpiresAtMs) return
    if (this.delayedAttacks.some(delayed => delayed.attack.behavior === 'swordFollowUp')) return
    this.disarmPendingUnterhau(state)
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
      this.disarmPendingUnterhau(state)
      return
    }
    this.delayedAttacks.push(delayed)
  }

  /**
   * The Unterhau swings for real in all three cases — at the Oberhau victim while it lives, at
   * the nearest living enemy once it does not, and along the aim when the room is empty. Every
   * one of them goes through `executeAttack`, so the follow-up always spawns its authored sweep
   * collider instead of the invisible direct damage it used to apply to a live target (M135).
   */
  private executeSwordFollowUp(delayed: RuntimeDelayedAttack): void {
    const maximumRange = delayed.attack.range
    const inReach = (enemy: RuntimeEnemy): boolean => (
      Math.sqrt(distanceSquared(this.player.position, enemy.position))
        <= maximumRange + enemy.definition.radius
    )
    const originalTarget = delayed.targetEnemyId
      ? this.enemies.find(enemy => (
        enemy.id === delayed.targetEnemyId && enemy.state !== 'dead'
      )) ?? null
      : null
    const automaticTarget = originalTarget && inReach(originalTarget)
      ? originalTarget
      : this.enemies
        .filter(enemy => enemy.state !== 'dead' && inReach(enemy))
        .sort((left, right) => (
          distanceSquared(this.player.position, left.position)
            - distanceSquared(this.player.position, right.position)
          || left.id.localeCompare(right.id)
        ))[0] ?? null
    const direction = automaticTarget
      ? normalize({
        x: automaticTarget.position.x - this.player.position.x,
        y: automaticTarget.position.y - this.player.position.y,
      }, delayed.direction)
      : this.resolveDelayedAttackDirection(delayed)
    this.executeAttack(delayed.attack, direction, delayed.context)
  }

  private executePendingUnterhau(hand: LastChancesHand): boolean {
    const weapon = this.weapons.get(hand)
    if (weapon?.trait !== 'swordRhythm') return false
    const state = this.weaponState(weapon)
    if (state.unterhauDueAtMs <= 0) return false
    // This path bypasses performAttack, so it pays for itself.
    const staminaCost = this.staminaCostFor(
      weapon,
      hand,
      'doubleTapHold',
      tuningValue(weapon, 'unterhauHoldMs', 1000),
    )
    if (!this.canAffordStamina(staminaCost)) {
      this.refuseForStamina(hand)
      return false
    }
    const attack = attackWithLastChancesAugment(weapon.attacks.doubleTapHold, weapon)
    const direction = normalize(this.player.aim)
    const resolution: LastChancesGestureResolution = {
      hand,
      gesture: 'doubleTapHold',
      atMs: this.elapsedMs,
      heldMs: tuningValue(weapon, 'unterhauHoldMs', 1000),
      firstHoldMs: 0,
    }
    this.settleStaminaForAttack(hand, staminaCost)
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
      channelStaminaAccumulatorMs: 0,
      stanceCutSpeed: 0,
      stanceTipVelocity: { x: 0, y: 0 },
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
      enemy.revealedMs = tuningValue(
        attack,
        'interruptRevealMs',
        this.config.combat.enemyRevealOnParryMs,
      )
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
    enemy.revealedMs = this.config.combat.enemyRevealOnParryMs
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
    if (options.hand
      && enemy.knifeSpiderV2?.attackMode === 'leap'
      && enemy.knifeSpiderV2.flightVelocity
      && this.reflectKnifeSpiderV2(enemy, direction)) {
      this.markSuccessfulHitFeedbackWindow(options.hand)
      if (this.controlSchemeValue === 'dualsense') {
        this.feedbackController.emit({
          state: 'impact',
          profile: 'impact',
          hand: runtimeHandToPhysicalCluster(options.hand),
          strength: 1,
        })
      }
      return
    }
    enemy.revealedMs = this.config.combat.enemyRevealOnHitMs
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
        multiplier *= tuningValue(attack, 'unmarkedDamageMultiplier', 0.55)
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
    multiplier *= this.ouroborosDamageMultiplier()
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
        const bleedEveryHits = Math.max(
          1,
          Math.round(tuningValue(weapon, 'bleedEveryHits', 2)),
        )
        state.resource = (state.basicHits + 1) % bleedEveryHits === 0
          ? state.maxResource
          : 0
        const dualRhythm = state.lastHitHand !== null
          && state.lastHitHand !== options.hand
          && this.elapsedMs - state.lastHitAtMs
            <= tuningValue(weapon, 'alternatingHitWindowMs', 360)
        if (state.basicHits % bleedEveryHits === 0) {
          applyLastChancesStatusEffects(enemy.statuses, [{
            status: 'bleed',
            durationMs: Math.max(
              0,
              tuningValue(weapon, 'parityBleedDurationMs', 5000),
            ),
            stacks: Math.max(
              1,
              Math.round(tuningValue(weapon, 'parityBleedStacks', 1)),
            ),
            tickDamage: Math.max(
              0,
              tuningValue(weapon, 'parityBleedTickDamage', 1.1),
            ),
            tickMs: Math.max(
              1,
              tuningValue(weapon, 'parityBleedTickMs', 500),
            ),
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
      const pullDirection = vectorLength(movement) > this.config.input.actionDirectionDeadZone
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
      const chargeStage = Math.round(tuningValue(attack, 'chargeStage', -1))
      const baseTargetDistance = Math.max(
        attack.sweetSpot?.minRange
          ?? attack.range * tuningValue(attack, 'targetRangeRatio', 0.72),
        tuningValue(attack, 'minimumTargetRange', 80),
      )
      const targetDistance = attack.behavior === 'spearKick'
        ? chargeStage >= 2
          ? tuningValue(attack, 'strongKickTargetDistance', 176)
          : chargeStage === 1
            ? tuningValue(attack, 'kickTargetDistance', 144)
            : Math.max(
                baseTargetDistance,
                attack.knockback * tuningValue(attack, 'targetDistancePerKnockback', 1),
              )
        : tuningValue(attack, 'shoveTargetDistance', baseTargetDistance)
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
    } else if (attack.behavior === 'spearBreakthrough') {
      // A breakthrough barges through rather than punting away: bodies are thrown out to the
      // side the player passed them on, so the lane ahead of the run clears instead of filling.
      const lateral = { x: -direction.y, y: direction.x }
      const offset = {
        x: enemy.position.x - this.player.position.x,
        y: enemy.position.y - this.player.position.y,
      }
      const side = lateral.x * offset.x + lateral.y * offset.y >= 0 ? 1 : -1
      const distance = Math.max(
        tuningValue(attack, 'shoveDistance', 46),
        knockback * tuningValue(attack, 'shoveDistancePerKnockback', 0.5),
      )
      this.moveCircle(
        enemy.position,
        { x: lateral.x * side * distance, y: lateral.y * side * distance },
        enemy.definition.radius,
      )
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
    const role = enemy.definition.role ?? 'standard'
    if (this.ouroborosEquipped.fang && role !== 'creep' && role !== 'cockroach') {
      this.ouroborosFangKillStacks += 1
    }
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
        if (quest.roomKills[hit.gesture] >= this.config.progression.moveQuestKillsRequired) {
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

  private damagePlayer(rawDamage: number, source: string): boolean {
    if (this.player.invulnerableMs > 0 || this.phase !== 'playing') return false
    const afterArmor = Math.max(
      this.config.combat.minimumPlayerDamageTaken,
      rawDamage - this.effectivePlayerStats().armor * this.player.armorMultiplier,
    )
    const damage = afterArmor * Math.max(0, 1 - this.ouroborosRoomDamageReduction())
    this.player.hp = Math.max(0, this.player.hp - damage)
    this.player.invulnerableMs = this.config.player.invulnerabilityMs
    if (this.player.hp <= 0) this.killPlayer(`Killed by ${source}`)
    return true
  }

  /**
   * Chemical arrows explicitly threaten their archer. They ignore dodge/jump and shared damage
   * i-frames, and simultaneous arrows each contribute their own blast; armor still mitigates.
   */
  private damagePlayerUnavoidable(rawDamage: number, source: string): boolean {
    if (!this.canExploreRoom()) return false
    const afterArmor = Math.max(
      this.config.combat.minimumPlayerDamageTaken,
      rawDamage - this.effectivePlayerStats().armor * this.player.armorMultiplier,
    )
    const damage = afterArmor * Math.max(0, 1 - this.ouroborosRoomDamageReduction())
    this.player.hp = Math.max(0, this.player.hp - damage)
    if (this.player.hp <= 0) this.killPlayer(`Killed by ${source}`, true)
    return true
  }

  /** Pure damage ignores armor; invulnerability frames still apply. */
  private damagePlayerPure(rawDamage: number, source: string): void {
    if (this.player.invulnerableMs > 0 || this.phase !== 'playing') return
    const damage = Math.max(this.config.combat.minimumPlayerDamageTaken, rawDamage)
      * Math.max(0, 1 - this.ouroborosRoomDamageReduction())
    this.player.hp = Math.max(0, this.player.hp - damage)
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
    const artifact = this.activeArtifact()
    return {
      ...this.player.stats,
      maxStamina: this.player.stats.maxStamina * Math.max(0, artifact?.maxStaminaMultiplier ?? 1),
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
    const set = this.config.ouroborosSet
    const ouroborosRatio = this.ouroborosEquipped.acid && set
      ? this.ouroborosAcidChancesSpent * set.acidLifestealPerChance
      : 0
    const ratio = Math.max(0, this.activeArtifact()?.lifestealRatio ?? 0) + ouroborosRatio
    if (ratio <= 0 || damageDealt <= 0 || this.player.hp <= 0) return
    this.player.hp = Math.min(
      this.player.stats.maxHp,
      this.player.hp + damageDealt * ratio,
    )
  }

  private ouroborosDamageMultiplier(): number {
    const set = this.config.ouroborosSet
    return this.ouroborosEquipped.fang && set
      ? 1 + this.ouroborosFangKillStacks * set.fangDamagePerKill
      : 1
  }

  private ouroborosRoomDamageReduction(): number {
    const set = this.config.ouroborosSet
    if (!this.ouroborosEquipped.scale || !set || !this.currentNode) return 0
    return (this.ouroborosScaleRoomStacks.get(this.currentNode.id) ?? 0)
      * set.scaleDamageReductionPerPickup
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

  private loadoutWithoutOuroboros(
    loadout: LastChancesLoadoutDefinition | null,
  ): LastChancesLoadoutDefinition | null {
    const set = this.config.ouroborosSet
    if (!loadout || !set) return loadout ? { ...loadout } : null
    const next = { ...loadout }
    if (next.secondaryWeaponId === set.fangWeaponId) {
      next.secondaryWeaponId = null
      next.secondaryAugment = 'none'
    }
    if (next.primaryAugment === set.acidAugment) next.primaryAugment = 'none'
    if (next.secondaryAugment === set.acidAugment) next.secondaryAugment = 'none'
    if (next.outfitId === set.scaleOutfitId) next.outfitId = null
    return next
  }

  private dropEquippedOuroborosOnDeath(): void {
    if (!this.currentNode || !this.config.ouroborosSet) return
    const items = LAST_CHANCES_OUROBOROS_ITEMS.filter(item => this.ouroborosEquipped[item])
    if (items.length === 0) return
    const corpse: RuntimeGroundOuroboros = {
      id: `ouroboros-corpse-${this.nextGroundOuroborosId++}`,
      items,
      position: { ...this.player.position },
      source: 'corpse',
      nodeId: this.currentNode.id,
    }
    const corpses = this.ouroborosCorpses.get(this.currentNode.id) ?? []
    corpses.push(corpse)
    this.ouroborosCorpses.set(this.currentNode.id, corpses)
    for (const item of items) this.ouroborosEquipped[item] = false
    this.activeLoadout = this.loadoutWithoutOuroboros(this.activeLoadout)
    if (this.bossCheckpoint) {
      this.bossCheckpoint.loadout = this.loadoutWithoutOuroboros(this.bossCheckpoint.loadout)
    }
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

  /**
   * Voluntary Chance costs hurt the current body as soon as their running total crosses a
   * configured erosion step. The remainder persists across deaths because Chances themselves do.
   */
  private spendVoluntaryChances(amount: number): void {
    const spent = Math.min(this.chances, Math.max(0, amount))
    if (spent <= 0) return
    this.chances -= spent
    this.voluntaryChanceSpendProgress += spent
    const step = this.config.progression.chanceErosionStep
    const tierIndex = this.currentNode?.tierIndex ?? 0
    const erosion = this.config.progression.tiers[tierIndex]?.erosion
    if (!erosion) return
    while (this.voluntaryChanceSpendProgress >= step) {
      this.voluntaryChanceSpendProgress -= step
      this.applyStatErosion(erosion, false)
    }
  }

  private applyStatErosion(erosion: LastChancesStatErosion, resetCurrentStats: boolean): void {
    const erode = (stats: LastChancesStats): LastChancesStats => ({
      maxHp: Math.max(1, stats.maxHp - erosion.maxHp),
      maxMentalHealth: Math.max(1, stats.maxMentalHealth - erosion.maxMentalHealth),
      maxStamina: Math.max(1, stats.maxStamina - erosion.maxStamina),
      attackPower: Math.max(1, stats.attackPower - erosion.attackPower),
      moveSpeed: Math.max(1, stats.moveSpeed - erosion.moveSpeed),
      armor: Math.max(0, stats.armor - erosion.armor),
    })
    this.generationBaseStats = erode(this.generationBaseStats)
    this.player.stats = resetCurrentStats
      ? copyStats(this.generationBaseStats)
      : erode(this.player.stats)
    const effectiveStats = this.effectivePlayerStats()
    this.player.hp = Math.min(this.player.hp, effectiveStats.maxHp)
    this.player.mentalHealth = Math.min(
      this.player.mentalHealth,
      effectiveStats.maxMentalHealth,
    )
    this.player.stamina = Math.min(this.player.stamina, effectiveStats.maxStamina)
  }

  private killPlayer(reason: string, allowClearedRoomDeath = false): void {
    if (this.phase !== 'playing' && !(allowClearedRoomDeath && this.canExploreRoom())) return
    this.dropEquippedOuroborosOnDeath()
    const activePrimary = this.activeLoadout
      ? this.config.weapons.find(weapon => weapon.id === this.activeLoadout?.primaryWeaponId)
      : null
    if (activePrimary?.corpseBound) this.corpseBoundPrimaryWeaponId = activePrimary.id
    const tierIndex = this.currentNode?.tierIndex ?? 0
    const tier = this.config.progression.tiers[tierIndex]
    this.chances = Math.max(0, this.chances - tier.deathCost)
    this.totalDeaths += 1
    this.applyStatErosion(tier.erosion, true)
    this.clearCombatTransients()
    this.deathReason = reason
    this.phase = this.chances > 0 ? 'dead' : 'outOfChances'
    this.emitSnapshot(true)
  }

  private completeRoom(): void {
    if (!this.currentNode || this.phase !== 'playing') return
    if (this.currentNode.roomTemplateId === 'false-apartment') this.restorePostPrologueLoadout()
    // Arrow impacts are room scenery. Keep both embedded arrows and already-loosed arrows while
    // the player walks the cleared arena; choosing the next node owns their eventual cleanup.
    this.clearCombatTransients(true)
    if (this.currentNode.interaction && !this.interactionResolved) {
      this.phase = 'planning'
      this.routeMapVisible = false
      this.availableNodeIds = []
      this.sacrificeNodeIds = []
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

  private clearCombatTransients(preserveBowArrows = false): void {
    this.stopAllBowChannels(false)
    this.projectiles = preserveBowArrows
      ? this.projectiles.filter(projectile => projectile.persistentArrow)
      : []
    if (!preserveBowArrows) {
      this.embeddedArrows = []
      this.fallingArrows = []
    }
    this.releasedBowDraw = null
    this.bowRainTarget = null
    this.bowShotPresentation = {
      atMs: Number.NEGATIVE_INFINITY,
      direction: { x: 1, y: 0 },
      goldenUntilMs: Number.NEGATIVE_INFINITY,
    }
    for (const hand of LAST_CHANCES_HANDS) {
      this.resetBowDrawDebit(hand)
      this.bowChannelReleasedAtInputMs[hand] = Number.NEGATIVE_INFINITY
      this.bowChannelConsumedUntilRelease[hand] = false
      this.bowDrawConsumedUntilRelease[hand] = false
      this.bowLastShotTargets[hand] = null
      this.bowLastShotWeaponIds[hand] = null
      this.bowLastShotAtMs[hand] = Number.NEGATIVE_INFINITY
      this.bowDoubleShotWeaponIds[hand] = null
      this.bowDoubleShotAtMs[hand] = Number.NEGATIVE_INFINITY
      this.bowResponseWindows[hand] = null
      this.bowRainReleasedAtInputMs[hand] = Number.NEGATIVE_INFINITY
    }
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
    this.activeParryAttack = null
    this.provisionalParry = null
    for (const hand of LAST_CHANCES_HANDS) this.resetImmediateHandInput(hand)
    this.cleanupControlInputs(false)
    this.resetTapCombos()
    this.resetStaminaChain()
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
    this.staminaCostStacks = Math.min(
      this.config.progression.maxStaminaCostStacks,
      this.staminaCostStacks + this.completedRoomStaminaCostStacks(),
    )
    this.rewardChest = null
    if (this.currentNode.tierIndex >= this.plan.tiers.length - 1) {
      this.phase = 'won'
      this.availableNodeIds = []
      this.sacrificeNodeIds = []
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
      this.sacrificeNodeIds = this.sameTierSacrificeNeighbors(this.currentNode)
      this.availableNodeIds = [
        ...this.currentNode.nextNodeIds,
        ...this.sacrificeNodeIds,
      ]
      this.selectedNodeId = this.controlSchemeValue === 'dualsense'
        ? null
        : this.availableNodeIds[0] ?? null
    }
    this.selectedInteractionChoiceId = null
  }

  /** Ordered nodes in one tier are the neighboring rooms shown horizontally by the run diagram. */
  private sameTierSacrificeNeighbors(node: LastChancesPlanNode): string[] {
    if (node.tierKind === 'boss') return []
    const tier = this.plan.tiers[node.tierIndex] ?? []
    const index = tier.findIndex(candidate => candidate.id === node.id)
    if (index < 0) return []
    const visited = new Set(this.attemptPath)
    return [tier[index - 1], tier[index + 1]]
      .filter((candidate): candidate is LastChancesPlanNode => candidate !== undefined)
      .filter(candidate => !visited.has(candidate.id))
      .map(candidate => candidate.id)
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
    this.spendVoluntaryChances(effect.chanceCost ?? 0)
    if (effect.stats) {
      this.player.stats = {
        maxHp: Math.max(1, this.player.stats.maxHp + (effect.stats.maxHp ?? 0)),
        maxMentalHealth: Math.max(
          1,
          this.player.stats.maxMentalHealth + (effect.stats.maxMentalHealth ?? 0),
        ),
        maxStamina: Math.max(1, this.player.stats.maxStamina + (effect.stats.maxStamina ?? 0)),
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

  private forcePrologueSwordLoadout(): void {
    if (this.prologueLoadoutForced
      || !this.config.weapons.some(weapon => weapon.id === 'hybrid-sword')) return
    this.postPrologueLoadout = this.activeLoadout ? { ...this.activeLoadout } : null
    this.prologueLoadoutForced = true
    this.activeLoadout = this.normalizeLoadoutAugments({
      primaryWeaponId: 'hybrid-sword',
      secondaryWeaponId: null,
      primaryAugment: 'none',
      secondaryAugment: 'none',
      artifactId: this.activeLoadout?.artifactId ?? null,
      outfitId: this.activeLoadout?.outfitId ?? null,
    })
    this.weaponStates.clear()
    this.rebuildWeapons()
  }

  private restorePostPrologueLoadout(): void {
    if (!this.prologueLoadoutForced) return
    this.activeLoadout = this.postPrologueLoadout
      ? this.normalizeLoadoutAugments({ ...this.postPrologueLoadout })
      : null
    this.postPrologueLoadout = null
    this.prologueLoadoutForced = false
    this.weaponStates.clear()
    this.rebuildWeapons()
    this.cooldownEnds.clear()
    this.resetTapCombos()
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
      augment && augment !== 'none'
        && (augment === this.config.ouroborosSet?.acidAugment || weapon?.augmentHooks?.[augment])
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
    this.sacrificeNodeIds = []
    this.selectedNodeId = this.controlSchemeValue === 'dualsense'
      ? null
      : this.availableNodeIds[0] ?? null
    this.selectedInteractionChoiceId = null
    this.gamepadMenuAxisEngaged = false
    this.attemptPath = []
    this.deathReason = null
    this.lastGesture = null
    this.postPrologueLoadout = null
    this.prologueLoadoutForced = false
    this.staminaCostStacks = 0
    if (this.knifeSpiderTutorialPhase !== 'complete') {
      this.knifeSpiderTutorialPhase = 'pending'
      this.knifeSpiderTutorialResumeMs = 0
    }
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
    this.groundOuroboros = []
    this.ninjaDashReadyAtMs = 0
    // A retry preserves the generated world, not attempt progression. Rebuild the same fresh
    // quest gate used at generation start so death cannot become an upgrade shortcut.
    this.moveQuests = {
      left: createHandMoveQuestState(
        this.qaControlsFixture || this.config.progression.moveQuestsEnabled === false,
      ),
      right: createHandMoveQuestState(
        this.qaControlsFixture || this.config.progression.moveQuestsEnabled === false,
      ),
    }
    this.stopAllBowChannels(false)
    this.projectiles = []
    this.embeddedArrows = []
    this.fallingArrows = []
    this.releasedBowDraw = null
    this.bowRainTarget = null
    this.bowShotPresentation = {
      atMs: Number.NEGATIVE_INFINITY,
      direction: { x: 1, y: 0 },
      goldenUntilMs: Number.NEGATIVE_INFINITY,
    }
    for (const hand of LAST_CHANCES_HANDS) this.resetBowDrawDebit(hand)
    this.activeAreas = []
    this.effects = []
    this.traces = []
    this.delayedAttacks = []
    this.delayedRecoveries = []
    this.weaponActionEnds.clear()
    this.activeParryCollider = null
    this.activeParryAttack = null
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
    this.resetStaminaChain()
    this.gestures.reset()
    for (const hand of LAST_CHANCES_HANDS) this.resetImmediateHandInput(hand)
    this.pressedKeys.clear()
    this.touchMove = { x: 0, y: 0 }
    this.touchAim = { x: 0, y: 0 }
    this.gamepadMove = { x: 0, y: 0 }
    this.gamepadAim = { x: 0, y: 0 }
    this.retainedGamepadAim = null
    this.pointerWorldTarget = null
    this.pointerClientX = null
    this.pointerDeltaX = 0
    this.player.position = { x: 0, y: 0 }
    this.player.aim = { x: 1, y: 0 }
    this.player.stats = copyStats(this.generationBaseStats)
    this.player.hp = this.player.stats.maxHp
    this.player.mentalHealth = this.player.stats.maxMentalHealth
    this.player.stamina = this.effectivePlayerStats().maxStamina
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

  private addEventLog(text: string): void {
    this.eventLog.push({ id: `event-${this.nextEventLogId}`, text, atMs: this.elapsedMs })
    this.nextEventLogId += 1
    if (this.eventLog.length > LAST_CHANCES_EVENT_LOG_LIMIT) {
      this.eventLog.splice(0, this.eventLog.length - LAST_CHANCES_EVENT_LOG_LIMIT)
    }
  }

  private ouroborosItemName(item: LastChancesOuroborosItem): string {
    const set = this.config.ouroborosSet
    if (item === 'fang') {
      return this.config.weapons.find(weapon => weapon.id === set?.fangWeaponId)?.name
        ?? 'Клык Уробороса'
    }
    if (item === 'scale') {
      return this.config.outfits?.find(outfit => outfit.id === set?.scaleOutfitId)?.name
        ?? 'Чешуя Уробороса'
    }
    return 'Кислота Уробороса'
  }

  private ouroborosPickupCost(pickup: RuntimeGroundOuroboros): number {
    const costs = this.config.ouroborosSet?.chanceCosts
    return costs
      ? pickup.items.reduce((total, item) => total + costs[item], 0)
      : 0
  }

  private nearestGroundOuroboros(): RuntimeGroundOuroboros | null {
    return this.groundOuroboros
      .map(pickup => ({
        pickup,
        distance: Math.sqrt(distanceSquared(this.player.position, pickup.position)),
      }))
      .filter(candidate => candidate.distance <= 105)
      .sort((left, right) => left.distance - right.distance)[0]?.pickup ?? null
  }

  private pickUpGroundOuroboros(pickup: RuntimeGroundOuroboros): boolean {
    const set = this.config.ouroborosSet
    if (!set || !this.currentNode || !this.activeLoadout) return false
    const index = this.groundOuroboros.findIndex(candidate => candidate.id === pickup.id)
    if (index < 0) return false
    const cost = this.ouroborosPickupCost(pickup)
    if (this.chances < cost) return false

    const hadFullSet = LAST_CHANCES_OUROBOROS_ITEMS.every(item => this.ouroborosEquipped[item])
    this.spendVoluntaryChances(cost)
    for (const item of pickup.items) {
      // One feed line per piece; the set's own name is withheld until the set is whole.
      // Phrased impersonally so the item names keep their own grammatical gender.
      this.addEventLog(`Подобрано: ${this.ouroborosItemName(item)}`)
      this.ouroborosDiscovered[item] = true
      this.ouroborosEquipped[item] = true
      if (item === 'acid') this.ouroborosAcidChancesSpent += set.chanceCosts.acid
      if (item === 'scale') {
        this.ouroborosScaleRoomStacks.set(
          this.currentNode.id,
          (this.ouroborosScaleRoomStacks.get(this.currentNode.id) ?? 0) + 1,
        )
      }
    }

    this.groundOuroboros.splice(index, 1)
    if (pickup.source === 'corpse') {
      const remaining = (this.ouroborosCorpses.get(pickup.nodeId) ?? [])
        .filter(candidate => candidate.id !== pickup.id)
      if (remaining.length > 0) this.ouroborosCorpses.set(pickup.nodeId, remaining)
      else this.ouroborosCorpses.delete(pickup.nodeId)
    }

    const next = { ...this.activeLoadout }
    if (pickup.items.includes('fang')) {
      const primary = next.primaryWeaponId
        ? this.config.weapons.find(weapon => weapon.id === next.primaryWeaponId)
        : null
      if (primary?.equipMode === 'twoHanded') {
        this.addGroundWeapon(primary.id, next.primaryAugment, pickup.position)
        next.primaryWeaponId = null
        next.primaryAugment = 'none'
      }
      if (next.secondaryWeaponId && next.secondaryWeaponId !== set.fangWeaponId) {
        this.addGroundWeapon(next.secondaryWeaponId, next.secondaryAugment, pickup.position)
      }
      next.secondaryWeaponId = set.fangWeaponId
      next.secondaryAugment = 'none'
    }
    if (pickup.items.includes('scale')) next.outfitId = set.scaleOutfitId
    if (this.ouroborosEquipped.acid) {
      if (next.primaryAugment === set.acidAugment) next.primaryAugment = 'none'
      if (next.secondaryAugment === set.acidAugment) next.secondaryAugment = 'none'
      if (this.ouroborosEquipped.fang && next.secondaryWeaponId === set.fangWeaponId) {
        next.secondaryAugment = set.acidAugment
      } else if (next.primaryWeaponId) {
        next.primaryAugment = set.acidAugment
      } else if (next.secondaryWeaponId) {
        next.secondaryAugment = set.acidAugment
      }
    }
    this.activeLoadout = this.normalizeLoadoutAugments(next)
    if (!hadFullSet && LAST_CHANCES_OUROBOROS_ITEMS.every(item => this.ouroborosEquipped[item])) {
      this.addEventLog(`${set.name} собран`)
    }
    this.rebuildWeapons()
    this.cooldownEnds.clear()
    this.resetTapCombos()
    this.lastGesture = {
      hand: pickup.items.includes('fang') ? 'right' : 'left',
      gesture: 'tap',
      attackName: pickup.items.map(item => this.ouroborosItemName(item)).join(' · '),
      atMs: this.elapsedMs,
    }
    this.emitSnapshot(true)
    return true
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
        return { enemy, distance, facingDot, v2: enemy.knifeSpiderV2 !== null }
      })
      .filter(candidate => (
        candidate.distance <= tuningValue(candidate.enemy.definition, 'captureDistance', 105)
        && (candidate.v2
          || candidate.facingDot
            < tuningValue(candidate.enemy.definition, 'captureRearDotMaximum', -0.2))
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
   *
   * `wallSlide` additionally resolves a head-on step — one that leaves no tangent at all — by
   * sliding along the blocking surface toward its nearest end. It is opt-in because only
   * ordinary walking wants it: a dash, a knockback or a steered enemy must land against the
   * wall it hit rather than drift sideways off it.
   */
  private moveCircle(
    position: LastChancesVector,
    delta: LastChancesVector,
    radius: number,
    options: { wallSlide?: boolean } = {},
  ): void {
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
      if (safeFraction >= 1 - EPSILON) {
        // Only the walking path owns the memo; an enemy stepping freely must not clear it.
        if (options.wallSlide) this.wallSlideMemo = null
        continue
      }

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
      if (best && best.distance > EPSILON) {
        position.x += best.delta.x
        position.y += best.delta.y
        continue
      }
      if (options.wallSlide && this.slideAlongBlockingWall(position, step, radius, slideDistance)) continue
      this.applyCornerCorrection(position, step, radius, slideDistance)
    }
  }

  /**
   * Walking straight at a wall leaves no tangential input to preserve, so the axis search above
   * finds nothing and the body would stop. Instead, slide along the blocking surface toward
   * whichever of its two ends is nearer, at the full unresolved step length — the character
   * rounds the obstacle by itself rather than freezing until the player steers away. The arena
   * boundary counts as a wall whose ends are the arena's corners.
   */
  private slideAlongBlockingWall(
    position: LastChancesVector,
    step: LastChancesVector,
    radius: number,
    slideDistance: number,
  ): boolean {
    const arena = this.currentNode?.arena
    if (!arena || slideDistance <= EPSILON) return false
    const forward = normalize(step)
    const normalAxis = Math.abs(step.x) >= Math.abs(step.y) ? 'x' : 'y'
    const tangentAxis = normalAxis === 'x' ? 'y' : 'x'
    // The body rests a binary-search residue short of true contact, so the probe needs a real
    // margin to reach inside what stopped it. A step is at most 4 units, far thinner than any
    // authored obstacle, so it cannot skip through one.
    const reach = Math.max(slideDistance, radius * 0.05)
    const probe = { x: position.x + forward.x * reach, y: position.y + forward.y * reach }

    // The wall's reachable extent along the tangent, inflated by the radius so both ends are
    // centre positions the body can actually occupy.
    let ends: { low: number, high: number } | null = null
    const obstacle = arena.obstacles.find(candidate => pointHitsObstacle(probe, radius, candidate))
    if (obstacle) {
      const length = tangentAxis === 'x' ? obstacle.width : obstacle.height
      ends = { low: obstacle[tangentAxis] - radius, high: obstacle[tangentAxis] + length + radius }
    } else {
      const extent = normalAxis === 'x' ? arena.width : arena.height
      if (probe[normalAxis] < radius || probe[normalAxis] > extent - radius) {
        const span = tangentAxis === 'x' ? arena.width : arena.height
        ends = { low: radius, high: span - radius }
      }
    }
    if (!ends) return false

    // Nearest end wins, but a direction already committed to keeps priority while it stays
    // free: abutting obstacles can disagree about which end is closer from one step to the next.
    const nearestSign = position[tangentAxis] - ends.low <= ends.high - position[tangentAxis] ? -1 : 1
    const remembered = this.wallSlideMemo?.axis === tangentAxis ? this.wallSlideMemo.sign : null
    for (const sign of remembered !== null ? [remembered, -remembered] : [nearestSign, -nearestSign]) {
      const slide = {
        x: tangentAxis === 'x' ? sign * slideDistance : 0,
        y: tangentAxis === 'y' ? sign * slideDistance : 0,
      }
      const fraction = this.maximumSafeMovementFraction(position, slide, radius)
      if (fraction <= EPSILON) continue
      position.x += slide.x * fraction
      position.y += slide.y * fraction
      this.wallSlideMemo = { axis: tangentAxis, sign }
      return true
    }
    return false
  }

  /**
   * Head-on contact leaves no wall tangent to slide along, so walking straight at an obstacle
   * would stick. When the blocked point is near an edge of what is blocking it, nudge sideways
   * and continue forward — the classic corner cut. Dead-centre on a long wall finds no free
   * side and still stops, which is the correct world-space collision behaviour.
   */
  private applyCornerCorrection(
    position: LastChancesVector,
    step: LastChancesVector,
    radius: number,
    slideDistance: number,
  ): void {
    const forward = normalize(step)
    if (vectorLength(forward) <= EPSILON) return
    const normal = { x: -forward.y, y: forward.x }
    const offsets = [0.3, 0.55, 0.8].map(share => radius * share)
    for (const offset of offsets) {
      for (const side of [1, -1]) {
        const nudge = {
          x: normal.x * offset * side,
          y: normal.y * offset * side,
        }
        const probe = { x: position.x + nudge.x, y: position.y + nudge.y }
        if (!this.circleCanOccupy(probe, radius)) continue
        const forwardStep = { x: forward.x * slideDistance, y: forward.y * slideDistance }
        if (this.maximumSafeMovementFraction(probe, forwardStep, radius) <= EPSILON) continue
        // Spend the blocked step on the sideways nudge, capped so the correction can never
        // move faster than the movement it replaces.
        const travel = Math.min(offset, slideDistance)
        position.x += normal.x * travel * side
        position.y += normal.y * travel * side
        return
      }
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
    this.canvas.addEventListener('pointercancel', this.handlePointerCancel)
    this.canvas.addEventListener('lostpointercapture', this.handleLostPointerCapture)
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
    this.canvas.removeEventListener('pointercancel', this.handlePointerCancel)
    this.canvas.removeEventListener('lostpointercapture', this.handleLostPointerCapture)
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
    this.movementVelocity = { x: 0, y: 0 }
    this.wallSlideMemo = null
    this.touchMove = { x: 0, y: 0 }
    this.gamepadMove = { x: 0, y: 0 }
    this.gamepadAim = { x: 0, y: 0 }
    this.retainedGamepadAim = null
    this.suppressPointerButtonsUntilRelease = this.pressedPointerButtons.size > 0
    this.pressedPointerButtons.clear()
    this.activeAttackPointerId = null
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
    this.resetImmediateHandInput('left')
    this.resetImmediateHandInput('right')
    this.mylorikControls.reset()
    this.dualSenseControls.reset()
    for (const hand of LAST_CHANCES_HANDS) {
      if (this.bowDrawDebits[hand].active || this.bowDrawDebits[hand].spent > 0) {
        this.consumeBowDrawDebit(hand, true)
      }
      this.bowDrawConsumedUntilRelease[hand] = false
      this.bowResponseWindows[hand] = null
      this.bowRainReleasedAtInputMs[hand] = Number.NEGATIVE_INFINITY
      this.bowChannelConsumedUntilRelease[hand] = false
    }
    this.triggerDetents.left = null
    this.triggerDetents.right = null
    this.spiderWriggle = null
    this.dualSenseTelegraphs.left = null
    this.dualSenseTelegraphs.right = null
    this.fangCooldownActive.left = null
    this.fangCooldownActive.right = null
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
    if (event.pointerType === 'touch' || event.pointerId !== this.activeAttackPointerId) return
    this.syncPointerButtons(event.buttons)
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (event.pointerType === 'touch') return
    this.suppressPointerButtonsUntilRelease = false
    this.activeAttackPointerId = event.pointerId
    this.canvas.setPointerCapture?.(event.pointerId)
    this.syncPointerButtons(event.buttons || pointerButtonMask(event.button))
  }

  private relevantPointerButtons(): number[] {
    if (this.controlSchemeValue === 'legacy') return [0, 2]
    const keyboard = this.controlSchemeValue === 'mylorik'
      ? this.config.input.mylorik?.keyboard
      : this.config.input.dualsense?.keyboard
    const buttons = new Set<number>()
    if (Number.isInteger(keyboard?.leftStrikeMouseButton)) {
      buttons.add(keyboard!.leftStrikeMouseButton)
    }
    if (Number.isInteger(keyboard?.rightStrikeMouseButton)) {
      buttons.add(keyboard!.rightStrikeMouseButton)
    }
    if (!this.weapons.has('right')) buttons.add(2)
    return [...buttons]
  }

  private pressPointerButton(button: number): void {
    if (this.pressedPointerButtons.has(button)) return
    this.pressedPointerButtons.add(button)
    if (button === 2 && !this.weapons.has('right') && this.startEmptyRightHandDash()) return
    if (this.controlSchemeValue === 'legacy') {
      if (button === 0) this.press('left')
      if (button === 2) this.press('right')
      return
    }
    const keyboard = this.controlSchemeValue === 'mylorik'
      ? this.config.input.mylorik?.keyboard
      : this.config.input.dualsense?.keyboard
    const physicalHand = button === keyboard?.leftStrikeMouseButton
      ? 'left'
      : button === keyboard?.rightStrikeMouseButton ? 'right' : null
    if (!physicalHand) return
    if (this.controlSchemeValue === 'mylorik') {
      this.mylorikControls.pressStrike(physicalHand, performance.now(), 'pointer')
    } else {
      this.dualSenseControls.pressBumper(physicalHand, performance.now(), 'pointer')
    }
  }

  private readonly handlePointerUp = (event: PointerEvent): void => {
    if (event.pointerType === 'touch' || event.pointerId !== this.activeAttackPointerId) return
    this.syncPointerButtons(event.buttons)
    if (event.buttons === 0) this.activeAttackPointerId = null
  }

  private releasePointerButton(button: number): void {
    if (!this.pressedPointerButtons.delete(button)) return
    if (this.controlSchemeValue !== 'legacy') return
    if (button === 0) this.release('left')
    if (button === 2) this.release('right')
  }

  private syncPointerButtons(buttons: number): void {
    if (this.suppressPointerButtonsUntilRelease) {
      if (buttons === 0) this.suppressPointerButtonsUntilRelease = false
      return
    }
    for (const button of this.relevantPointerButtons()) {
      if ((buttons & pointerButtonMask(button)) !== 0) this.pressPointerButton(button)
      else this.releasePointerButton(button)
    }
  }

  private cancelPointerButtons(): void {
    if (this.controlSchemeValue === 'legacy') {
      for (const button of this.pressedPointerButtons) {
        const hand = button === 0 ? 'left' : button === 2 ? 'right' : null
        if (!hand) continue
        this.gestures.cancel(hand)
        this.resetImmediateHandInput(hand)
      }
    }
    this.pressedPointerButtons.clear()
    this.activeAttackPointerId = null
    this.suppressPointerButtonsUntilRelease = true
  }

  private readonly handlePointerCancel = (event: PointerEvent): void => {
    if (event.pointerType === 'touch' || event.pointerId !== this.activeAttackPointerId) return
    this.cancelPointerButtons()
  }

  private readonly handleLostPointerCapture = (event: PointerEvent): void => {
    if (event.pointerId !== this.activeAttackPointerId || this.pressedPointerButtons.size === 0) return
    this.cancelPointerButtons()
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

  /**
   * The world→screen basis, shared by every projected drawing call and by `entityScale`.
   * Both world axes use the SAME scale, so one world unit covers the same screen distance
   * whichever way the body walks — normalizing X and Y against their own room dimension made
   * movement along Y of a 16:9 room 1.78x faster on screen than movement along X. The room is
   * therefore drawn to scale as an elongated rhombus rather than a symmetric diamond, while
   * still spanning exactly the fitted `diamondWidth` x `diamondHeight` box.
   */
  private projection(node: LastChancesPlanNode): IsometricProjection {
    const layout = this.layout()
    const span = node.arena.width + node.arena.height
    const scaleX = layout.diamondWidth / span
    return {
      // World (0,0) sits left of centre exactly as far as the room is wider than it is tall.
      originX: layout.centerX - (node.arena.width - node.arena.height) * scaleX / 2,
      top: layout.top,
      scaleX,
      scaleY: layout.diamondHeight / span,
    }
  }

  private worldToScreen(point: LastChancesVector, node: LastChancesPlanNode): LastChancesVector {
    const projection = this.projection(node)
    return {
      x: projection.originX + (point.x - point.y) * projection.scaleX,
      y: projection.top + (point.x + point.y) * projection.scaleY,
    }
  }

  private screenToWorld(point: LastChancesVector, node: LastChancesPlanNode): LastChancesVector {
    const projection = this.projection(node)
    const difference = (point.x - projection.originX) / projection.scaleX
    const sum = (point.y - projection.top) / projection.scaleY
    return {
      x: clamp((difference + sum) / 2, 0, node.arena.width),
      y: clamp((sum - difference) / 2, 0, node.arena.height),
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
        const attack = weapon.attacks[node.gesture]
        const nodeChargeReady = attack.charge === undefined
          || resolveLastChancesChargedAttack(
            attack,
            trigger.heldMs,
            node.chargeBandOverrideId,
          ).band !== null
        const armedReady = node.entryRequiresArmed !== true
          || trigger.armedNodeId === trigger.nodeId
        if (!bandReady || !nodeChargeReady || !armedReady) return false
        return node.entryContext === 'continuation' || node.entryRequiresArmed === true
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
        const tapHasCooldown = gesture === 'tap' && weapon.attacks.tap.cooldownMs > 0
        const totalMs = gesture === 'tap' && !tapHasCooldown ? 0 : weapon.attacks[gesture].cooldownMs
        const remainingMs = gesture === 'tap' && !tapHasCooldown
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
        killsRequired: this.config.progression.moveQuestKillsRequired,
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
    const groundOuroboros = this.nearestGroundOuroboros()
    const rewardChest = this.nearbyRewardChest()
    const nearbyTurret = this.nearestActiveTurret()
    const capturableSpider = this.capturableKnifeSpider()
    const groundWeaponName = groundWeapon
      ? this.config.weapons.find(weapon => weapon.id === groundWeapon.weaponId)?.name
      : null
    const ouroborosCost = groundOuroboros
      ? this.ouroborosPickupCost(groundOuroboros)
      : 0
    const ouroborosNames = groundOuroboros
      ? groundOuroboros.items.map(item => this.ouroborosItemName(item)).join(' · ')
      : null
    const ouroborosSet = this.config.ouroborosSet
    const roomScaleStacks = this.currentNode
      ? this.ouroborosScaleRoomStacks.get(this.currentNode.id) ?? 0
      : 0
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
      sacrificeNodeIds: [...this.sacrificeNodeIds],
      deathReason: this.deathReason,
      player: {
        position: { ...this.player.position },
        aim: { ...this.player.aim },
        hp: this.player.hp,
        mentalHealth: this.player.mentalHealth,
        stamina: this.player.stamina,
        staminaCostStacks: this.staminaCostStacks,
        staminaCostMultiplier: this.staminaCostMultiplier(),
        stats: copyStats(effectiveStats),
        invulnerableForMs: this.player.invulnerableMs,
        armorMultiplier: this.player.armorMultiplier,
        armorMultiplierForMs: this.player.armorMultiplierMs,
        staminaRefusedAtMs: this.staminaRefusedAtMs,
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
      groundOuroboros: this.groundOuroboros.map(pickup => ({
        id: pickup.id,
        items: [...pickup.items],
        position: { ...pickup.position },
        chanceCost: this.ouroborosPickupCost(pickup),
        affordable: this.chances >= this.ouroborosPickupCost(pickup),
      })),
      ouroboros: ouroborosSet
        ? {
            equipped: { ...this.ouroborosEquipped },
            discovered: { ...this.ouroborosDiscovered },
            fangKillStacks: this.ouroborosFangKillStacks,
            damageBonusPercent: this.ouroborosFangKillStacks
              * ouroborosSet.fangDamagePerKill * 100,
            acidChancesSpent: this.ouroborosAcidChancesSpent,
            lifestealPercent: this.ouroborosAcidChancesSpent
              * ouroborosSet.acidLifestealPerChance * 100,
            roomScaleStacks,
            damageReductionPercent: roomScaleStacks
              * ouroborosSet.scaleDamageReductionPerPickup * 100,
            fullSet: LAST_CHANCES_OUROBOROS_ITEMS.every(item => this.ouroborosEquipped[item]),
          }
        : null,
      events: this.eventLog.map(entry => ({ ...entry })),
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
        : groundOuroboros && ouroborosNames
        ? `${this.controlSchemeValue === 'dualsense' ? 'E / Cross' : 'E'}: подобрать ${ouroborosNames} (−${ouroborosCost} Шансов)${this.chances < ouroborosCost ? ' · недостаточно Шансов' : ''}`
        : rewardChest
        ? `${this.controlSchemeValue === 'dualsense' ? 'E / Cross' : 'E'}: открыть сундук с наградой`
        : capturableSpider
        ? this.controlSchemeValue === 'legacy'
          ? capturableSpider.knifeSpiderV2
            ? 'E / обе кнопки: схватить обездвиженного Нож-паука'
            : 'E / обе кнопки: схватить Нож-паука со спины'
          : this.controlSchemeValue === 'mylorik'
            ? `E / Cross: схватить ${capturableSpider.knifeSpiderV2 ? 'обездвиженного Нож-паука' : 'Нож-паука со спины'}`
            : `E / Cross: схватить ${capturableSpider.knifeSpiderV2 ? 'обездвиженного Нож-паука' : 'Нож-паука со спины'}`
        : null,
      knifeSpiderTutorial: ['slowing', 'frozen', 'resuming'].includes(
        this.knifeSpiderTutorialPhase,
      )
        ? {
            phase: this.knifeSpiderTutorialPhase as 'slowing' | 'frozen' | 'resuming',
            timeScale: this.knifeSpiderTutorialTimeScale(),
            parryBinding: this.controlSchemeValue === 'dualsense'
              ? 'R1'
              : this.controlSchemeValue === 'mylorik'
                ? 'ПКМ / R1'
                : `ЛКМ / ${this.config.input.leftKeys[0]?.replace(/^Key/, '') ?? 'J'} / L1`,
          }
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
    for (const arrow of this.embeddedArrows) {
      if (arrow.chemical && !arrow.exploded) this.renderArrowChemicalPool(arrow, node)
    }
    this.renderBowRainZone(node)
    // Pending hole strikes are deliberately not drawn: the player must not learn which hole
    // answers before it goes off. The blast itself is shown by a 'shock' effect on detonation.
    for (const turret of this.turrets) this.renderTurretVision(turret, node)
    for (const enemy of this.enemies) this.renderVision(enemy, node)
    this.renderSpearChargePreview(node)
    this.renderSpearFollowUpPreview(node)
    this.renderPoleVaultTrajectoryPreview(node)
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
    for (const arrow of this.embeddedArrows) {
      const attachedEnemy = arrow.enemyId
        ? this.enemies.find(enemy => enemy.id === arrow.enemyId)
        : null
      items.push({
        depth: attachedEnemy
          ? attachedEnemy.position.x + attachedEnemy.position.y + 0.05
          : arrow.position.x + arrow.position.y + 0.02,
        draw: () => this.renderEmbeddedArrow(arrow, node),
      })
    }
    for (const arrow of this.fallingArrows) {
      items.push({
        depth: arrow.target.x + arrow.target.y + 0.01,
        draw: () => this.renderFallingArrow(arrow, node),
      })
    }
    for (const weapon of this.groundWeapons) {
      items.push({
        depth: weapon.position.x + weapon.position.y,
        draw: () => this.renderGroundWeapon(weapon, node),
      })
    }
    for (const pickup of this.groundOuroboros) {
      items.push({
        depth: pickup.position.x + pickup.position.y,
        draw: () => this.renderGroundOuroboros(pickup, node),
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
    this.renderPierceMashCue(node)
    this.renderActionCues(node)
    this.renderBowChargeCue(node)
  }

  /**
   * The «Прокол» mash invitation. It has to read at a glance and from the corner of the eye,
   * because the whole window is two seconds long: a ring that closes as the window drains, a
   * fast pulse that sets the mashing tempo, and a running count of thrusts already landed.
   */
  private renderPierceMashCue(node: LastChancesPlanNode): void {
    if (this.paused || !this.canUseRoomActions()) return
    const hand = LAST_CHANCES_HANDS.find(candidate => this.pierceMashActive(candidate))
    if (!hand) return
    const mash = this.pierceMash[hand]
    const weapon = this.weapons.get(hand)
    const windowMs = Math.max(
      1,
      tuningValue(weapon?.attacks.doubleTap, 'mashWindowMs', 2000),
    )
    const remaining = clamp((mash.expiresAtMs - this.elapsedMs) / windowMs, 0, 1)
    const point = this.worldToScreen(this.player.position, node)
    const radius = Math.max(10, this.config.player.radius * this.entityScale(node))
    const pulse = 0.5 + Math.sin(this.elapsedMs / 90) * 0.5
    const color = LAST_CHANCES_GESTURE_COLORS.doubleTap
    const context = this.context
    context.save()
    context.lineCap = 'round'
    // Draining ring: how much of the window is left.
    context.globalAlpha = 0.85
    context.strokeStyle = color
    context.lineWidth = Math.max(2.5, radius * 0.17)
    context.beginPath()
    context.arc(
      point.x,
      point.y,
      radius * 1.62,
      -Math.PI / 2,
      -Math.PI / 2 + Math.PI * 2 * remaining,
    )
    context.stroke()
    // Tempo ring: the beat to mash at.
    context.globalAlpha = 0.2 + pulse * 0.45
    context.lineWidth = Math.max(1.5, radius * 0.09)
    context.beginPath()
    context.arc(point.x, point.y, radius * (1.9 + pulse * 0.45), 0, Math.PI * 2)
    context.stroke()
    context.globalAlpha = 0.72 + pulse * 0.28
    context.fillStyle = color
    context.textAlign = 'center'
    context.font = `900 ${Math.max(10, radius * 0.72)}px system-ui`
    context.fillText('БЕЙ!', point.x, point.y - radius * 2.5)
    if (mash.hits > 1) {
      context.globalAlpha = 0.9
      context.font = `900 ${Math.max(8, radius * 0.5)}px system-ui`
      context.fillText(`×${mash.hits}`, point.x, point.y - radius * 1.85)
    }
    context.restore()
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
    // Ragged lip of torn floor, then the dark, then a rim that breathes. The authored shape stays
    // exact — the player is expected to learn which shape answers which, so it cannot wobble.
    context.save()
    context.scale(1.16, 1.16)
    context.fillStyle = 'rgba(38, 30, 34, .9)'
    context.fill()
    context.restore()
    context.fillStyle = '#050405'
    context.shadowColor = hole.color
    context.shadowBlur = 10
    context.fill()
    // Throat: a few strands crossing the dark so the hole reads as a passage, not a decal.
    context.save()
    context.clip()
    context.strokeStyle = 'rgba(120, 80, 92, .5)'
    context.lineWidth = 1.2
    for (let strand = 0; strand < 3; strand += 1) {
      const offset = radius * (-0.5 + strand * 0.5)
      context.beginPath()
      context.moveTo(offset, -radius * 1.4)
      context.quadraticCurveTo(offset + radius * 0.3, 0, offset, radius * 1.4)
      context.stroke()
    }
    context.restore()
    context.strokeStyle = hole.color
    context.lineWidth = 2.5 + Math.sin(this.elapsedMs / 620 + hole.position.x) * 0.6
    context.stroke()
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
    const headY = point.y - scale * 0.65
    const barrelAngle = Math.atan2(aim.y - point.y, aim.x - point.x)
    context.save()
    // Squat plinth under the housing, so a turret reads as bolted down rather than floating.
    context.beginPath()
    context.ellipse(point.x, point.y, scale * 0.92, scale * 0.4, 0, 0, Math.PI * 2)
    context.fillStyle = turret.disabled ? '#2b2d2f' : '#3c4245'
    context.fill()
    // Barrel first, so the housing sits over its root.
    context.save()
    context.translate(point.x, headY)
    context.rotate(barrelAngle)
    context.beginPath()
    context.roundRect(0, -scale * 0.24, scale * 2.1, scale * 0.48, scale * 0.1)
    context.fillStyle = turret.disabled ? '#4a4e50' : shadeEnemyColor(turret.definition.color, -0.45)
    context.fill()
    context.beginPath()
    context.roundRect(scale * 1.85, -scale * 0.34, scale * 0.36, scale * 0.68, scale * 0.1)
    context.fillStyle = turret.disabled ? '#5c6062' : turret.definition.color
    context.fill()
    context.restore()
    // Housing carrying the turret's own shape — the same four shapes the boss holes use, and the
    // only thing that tells «Круг» from «Ромб» at a glance.
    context.beginPath()
    if (turret.definition.id === 'turret-square') {
      context.rect(point.x - scale * 0.82, headY - scale * 0.82, scale * 1.64, scale * 1.64)
    } else if (turret.definition.id === 'turret-triangle') {
      context.moveTo(point.x, headY - scale)
      context.lineTo(point.x + scale * 0.92, headY + scale * 0.72)
      context.lineTo(point.x - scale * 0.92, headY + scale * 0.72)
      context.closePath()
    } else if (turret.definition.id === 'turret-diamond') {
      context.moveTo(point.x, headY - scale * 1.08)
      context.lineTo(point.x + scale * 0.94, headY)
      context.lineTo(point.x, headY + scale * 1.08)
      context.lineTo(point.x - scale * 0.94, headY)
      context.closePath()
    } else {
      context.arc(point.x, headY, scale, 0, Math.PI * 2)
    }
    context.fillStyle = turret.disabled ? '#343638' : '#555d61'
    context.strokeStyle = turret.disabled ? '#72787a' : turret.definition.color
    context.lineWidth = 2.5
    context.fill()
    context.stroke()
    // Lens, lit only while the turret is still watching.
    context.beginPath()
    context.arc(
      point.x + Math.cos(barrelAngle) * scale * 0.34,
      headY + Math.sin(barrelAngle) * scale * 0.34,
      scale * 0.3,
      0,
      Math.PI * 2,
    )
    context.fillStyle = turret.disabled
      ? '#42474a'
      : turret.seesPlayer
        ? '#ffe9c4'
        : shadeEnemyColor(turret.definition.color, -0.2)
    context.fill()
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

  /**
   * The two hazard kinds now look like what they do instead of sharing one coloured rectangle:
   * spikes are a bed of blades that rise out of the floor on their active beat and sit nearly
   * flush between beats, and mental fog is a drift of soft shapes that thickens when it bites.
   * Both are clipped to the authored footprint, which is what the damage test actually uses, so
   * the drawing can never promise a safe pixel that is not safe.
   */
  private renderHazard(hazard: LastChancesHazardDefinition, node: LastChancesPlanNode): void {
    const context = this.context
    const active = this.hazardActive(hazard)
    const points = [
      this.worldToScreen({ x: hazard.x, y: hazard.y }, node),
      this.worldToScreen({ x: hazard.x + hazard.width, y: hazard.y }, node),
      this.worldToScreen({ x: hazard.x + hazard.width, y: hazard.y + hazard.height }, node),
      this.worldToScreen({ x: hazard.x, y: hazard.y + hazard.height }, node),
    ]
    const trace = (): void => {
      context.beginPath()
      points.forEach((point, index) => index === 0
        ? context.moveTo(point.x, point.y)
        : context.lineTo(point.x, point.y))
      context.closePath()
    }
    const spanX = Math.abs(points[1].x - points[0].x) + Math.abs(points[3].x - points[0].x)
    const spanY = Math.abs(points[1].y - points[0].y) + Math.abs(points[3].y - points[0].y)
    const size = Math.max(6, Math.min(spanX, spanY) * 0.16)

    context.save()
    context.globalAlpha = active ? 0.48 : 0.12
    trace()
    context.fillStyle = hazard.color
    context.fill()

    trace()
    context.clip()
    if (hazard.kind === 'spikes') {
      // Blades on a grid, tall while the row is live and barely proud of the floor between beats.
      const height = size * (active ? 1.5 : 0.32)
      context.globalAlpha = active ? 0.95 : 0.4
      context.fillStyle = shadeEnemyColor(hazard.color, active ? 0.35 : -0.1)
      for (let row = 0; row < 4; row += 1) {
        for (let column = 0; column < 6; column += 1) {
          const alongX = (column + 0.5) / 6
          const alongY = (row + 0.5) / 4
          const spikeX = points[0].x
            + (points[1].x - points[0].x) * alongX
            + (points[3].x - points[0].x) * alongY
          const spikeY = points[0].y
            + (points[1].y - points[0].y) * alongX
            + (points[3].y - points[0].y) * alongY
          context.beginPath()
          context.moveTo(spikeX - size * 0.28, spikeY)
          context.lineTo(spikeX, spikeY - height)
          context.lineTo(spikeX + size * 0.28, spikeY)
          context.closePath()
          context.fill()
        }
      }
    } else {
      // Fog: overlapping soft shapes drifting along the field, denser while it is biting.
      context.globalAlpha = active ? 0.42 : 0.16
      context.fillStyle = shadeEnemyColor(hazard.color, 0.28)
      for (let blob = 0; blob < 7; blob += 1) {
        const drift = this.elapsedMs / (2600 + blob * 340) + blob * 1.7
        const alongX = (Math.sin(drift) * 0.5 + 0.5) * 0.86 + 0.07
        const alongY = (Math.cos(drift * 0.7 + blob) * 0.5 + 0.5) * 0.8 + 0.1
        context.beginPath()
        context.ellipse(
          points[0].x + (points[1].x - points[0].x) * alongX + (points[3].x - points[0].x) * alongY,
          points[0].y + (points[1].y - points[0].y) * alongX + (points[3].y - points[0].y) * alongY,
          size * (1.1 + Math.sin(drift * 1.3) * 0.25),
          size * 0.5,
          0,
          0,
          Math.PI * 2,
        )
        context.fill()
      }
    }
    context.restore()

    context.save()
    context.globalAlpha = active ? 0.48 : 0.12
    trace()
    context.strokeStyle = hazard.color
    context.lineWidth = active ? 2.5 : 1
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

  /**
   * The walls are alive, and this is the only place that says so. Their footprint is load-bearing
   * — movement, line of sight and every collider test use the authored rectangle — so nothing here
   * moves the silhouette by a pixel. The life is entirely inside it: a slow breath that swells the
   * lit face, veins that creep across the top, and ribs banding the front. Vein layout is hashed
   * from the obstacle's own coordinates, so a wall looks the same every frame and every run
   * instead of crawling.
   */
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
    const tracePolygon = (points: LastChancesVector[]): void => {
      context.beginPath()
      points.forEach((point, index) => index === 0
        ? context.moveTo(point.x, point.y)
        : context.lineTo(point.x, point.y))
      context.closePath()
    }
    const polygon = (points: LastChancesVector[], fill: string): void => {
      tracePolygon(points)
      context.fillStyle = fill
      context.fill()
    }
    /** Stable per-wall noise in 0..1, so veins never crawl between frames. */
    const hash = (index: number): number => {
      const value = Math.sin(
        (obstacle.x + 1) * 12.9898 + (obstacle.y + 1) * 78.233 + index * 37.719,
      ) * 43758.5453
      return value - Math.floor(value)
    }
    const breath = 0.5 + Math.sin(this.elapsedMs / 1500 + obstacle.x * 0.01 + obstacle.y * 0.013) * 0.5
    const lit = [top[1], base[1], base[2], top[2]]
    const dark = [top[2], base[2], base[3], top[3]]

    polygon(lit, this.config.renderer.obstacleSide)
    polygon(dark, 'rgba(25, 22, 32, .96)')

    // Ribs across the lit face, banding it like something with a ribcage.
    if (elevation > 4) {
      context.save()
      tracePolygon(lit)
      context.clip()
      context.strokeStyle = `rgba(12, 10, 16, ${0.2 + breath * 0.12})`
      context.lineWidth = Math.max(1, elevation * 0.055)
      for (let rib = 1; rib < 5; rib += 1) {
        const ribShare = rib / 5
        context.beginPath()
        context.moveTo(top[1].x, top[1].y + elevation * ribShare)
        context.lineTo(top[2].x, top[2].y + elevation * ribShare)
        context.stroke()
      }
      context.restore()
    }

    polygon(top, this.config.renderer.obstacleTop)

    // Veins on the top face: each creeps from the centre to a hashed point on the rim, kinked
    // once on the way, and brightens with the breath.
    context.save()
    tracePolygon(top)
    context.clip()
    const centre = {
      x: (top[0].x + top[2].x) / 2,
      y: (top[0].y + top[2].y) / 2,
    }
    context.strokeStyle = `rgba(196, 122, 132, ${0.16 + breath * 0.24})`
    context.lineWidth = Math.max(1, elevation * 0.05)
    for (let vein = 0; vein < 5; vein += 1) {
      const corner = top[Math.floor(hash(vein) * 4) % 4]
      const along = 0.25 + hash(vein + 20) * 0.7
      const kink = 0.35 + hash(vein + 40) * 0.3
      const tip = {
        x: centre.x + (corner.x - centre.x) * along,
        y: centre.y + (corner.y - centre.y) * along,
      }
      context.beginPath()
      context.moveTo(centre.x, centre.y)
      context.quadraticCurveTo(
        centre.x + (tip.x - centre.x) * kink + (hash(vein + 60) - 0.5) * elevation * 0.5,
        centre.y + (tip.y - centre.y) * kink + (hash(vein + 80) - 0.5) * elevation * 0.5,
        tip.x,
        tip.y,
      )
      context.stroke()
    }
    // A wet swell of light that rises and falls with the breath.
    context.globalAlpha = 0.06 + breath * 0.1
    context.beginPath()
    context.ellipse(
      centre.x,
      centre.y,
      Math.abs(top[2].x - top[0].x) * 0.34,
      Math.abs(top[2].y - top[0].y) * 0.34,
      0,
      0,
      Math.PI * 2,
    )
    context.fillStyle = '#e8d5cf'
    context.fill()
    context.restore()

    tracePolygon(top)
    context.strokeStyle = `rgba(255,255,255,${0.08 + breath * 0.06})`
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
    return this.projection(node).scaleX
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
    // Walking to a hole makes her untouchable (see damageEnemy); rendering her half-phased is
    // the only tell the player gets that hits will not land.
    context.globalAlpha = enemy.motherRetreat?.stage === 'approaching'
      ? 0.3
      : visible ? 1 : enemy.state === 'noticing' ? 0.18 : 0.07
    context.save()
    context.translate(point.x, point.y)
    context.scale(1, 0.46)
    context.beginPath()
    context.arc(0, 4, radius * 1.05, 0, Math.PI * 2)
    context.fillStyle = 'rgba(0,0,0,.38)'
    context.fill()
    context.restore()
    this.renderEnemyBody(enemy, point, radius, node)
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
        const leapDistance = enemy.knifeSpiderV2
          ? tuningValue(enemy.definition, 'v2LeapDistance', 520)
          : profile.leapDistance
        const target = this.worldToScreen({
          x: enemy.position.x + enemy.lockedAttackDirection.x * leapDistance,
          y: enemy.position.y + enemy.lockedAttackDirection.y * leapDistance,
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
    if (this.activeAreas.some(area => isSpearSpinBehavior(area.attack.behavior))) {
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

  private renderEnemyBody(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    context.beginPath()
    if (enemy.definition.cockroachMother) {
      this.renderCockroachMother(enemy, point, radius, node)
      return
    } else if (isCockroachDefinition(enemy.definition)) {
      this.renderSwarmCockroach(enemy, point, radius, node)
      return
    } else if (enemy.knifeSpiderV2
      && this.knifeSpiderV2Image?.complete
      && this.knifeSpiderV2Image.naturalWidth > 0) {
      const facingPoint = this.worldToScreen({
        x: enemy.position.x + enemy.facing.x,
        y: enemy.position.y + enemy.facing.y,
      }, node)
      const screenAngle = Math.atan2(
        facingPoint.y - point.y,
        facingPoint.x - point.x,
      )
      const pulse = enemy.knifeSpiderV2.flightVelocity
        ? 1.08
        : 1 + Math.sin(this.elapsedMs / 82 + enemy.position.x) * 0.025
      const size = radius * 4.7
      context.save()
      context.translate(point.x, point.y - radius * 0.9)
      context.rotate(screenAngle + Math.PI / 2)
      context.scale(pulse, pulse)
      if (enemy.knifeSpiderV2.reflected) {
        context.shadowColor = '#ff3f35'
        context.shadowBlur = radius * 1.4
      }
      context.drawImage(this.knifeSpiderV2Image, -size / 2, -size / 2, size, size)
      context.restore()
      context.beginPath()
      context.ellipse(
        point.x,
        point.y - radius * 0.82,
        radius * 0.72,
        radius * 1.2,
        screenAngle + Math.PI / 2,
        0,
        Math.PI * 2,
      )
      return
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
      this.renderInvisibleWolf(enemy, point, radius, node)
      return
    } else if (enemy.definition.id === 'servant') {
      this.renderServant(enemy, point, radius, node)
      return
    } else if (enemy.definition.id === 'running-stapler') {
      this.renderRunningStapler(enemy, point, radius, node)
      return
    } else if (enemy.definition.id === 'guard') {
      this.renderGuard(enemy, point, radius, node)
      return
    } else if (enemy.definition.id === 'chimera') {
      this.renderChimera(enemy, point, radius, node)
      return
    } else if (enemy.definition.id === 'colossus') {
      this.renderColossus(enemy, point, radius, node)
      return
    } else if (enemy.definition.id === 'curator-shadow') {
      this.renderCuratorShadow(enemy, point, radius, node)
      return
    } else if (enemy.definition.id === 'infinite-cube') {
      context.rect(point.x - radius, point.y - radius * 1.8, radius * 2, radius * 2)
    } else {
      context.arc(point.x, point.y - radius * 0.8, radius, 0, Math.PI * 2)
    }
    context.fillStyle = enemy.definition.color
    context.fill()
  }

  /** 0 → 1 across an enemy's wind-up and 0 whenever it is not winding up; drives attack poses. */
  private enemyWindupProgress(enemy: RuntimeEnemy): number {
    if (enemy.state !== 'attacking') return 0
    const profile = this.enemyCombatProfile(enemy)
    return 1 - clamp(enemy.attackWindupMs / Math.max(1, profile.attackWindupMs), 0, 1)
  }

  /** True while an enemy is closing on the player, so a model can pick a moving pose. */
  private enemyIsMoving(enemy: RuntimeEnemy): boolean {
    return enemy.state === 'chasing' || enemy.state === 'attacking'
  }

  /**
   * Every hand-drawn standing enemy is a billboard: it stands on its ground point and mirrors by
   * the sign of its screen-space facing instead of rotating, because the arena is drawn
   * isometrically and a rotated upright body would read as falling over. Inside `draw` the origin
   * is the ground point, +x is forward and −y is up, all in units the caller scales by `radius`.
   * Bugs are drawn from above by `drawEnemyTopDown` instead.
   *
   * `draw` must finish by leaving its silhouette as the current path and neither filling nor
   * stroking it. Path coordinates are resolved against the transform in force when each segment is
   * added, so the silhouette survives the `restore` here and `renderEnemy` can stroke the real
   * outline of the body — white normally, thick red through a wind-up.
   */
  private drawEnemyBillboard(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    node: LastChancesPlanNode,
    draw: () => void,
  ): void {
    const facingPoint = this.worldToScreen({
      x: enemy.position.x + enemy.facing.x,
      y: enemy.position.y + enemy.facing.y,
    }, node)
    const context = this.context
    context.save()
    context.translate(point.x, point.y)
    context.scale(facingPoint.x >= point.x ? 1 : -1, 1)
    context.lineJoin = 'round'
    context.lineCap = 'round'
    draw()
    context.restore()
  }

  /**
   * Insects are seen from above and really do rotate, the same convention the Knife-spider sprite
   * uses. Inside `draw` the origin is the body's centre and the creature points along −y. The same
   * "leave your silhouette as the current path" rule as `drawEnemyBillboard` applies.
   */
  private drawEnemyTopDown(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
    draw: () => void,
  ): void {
    const facingPoint = this.worldToScreen({
      x: enemy.position.x + enemy.facing.x,
      y: enemy.position.y + enemy.facing.y,
    }, node)
    const context = this.context
    context.save()
    context.translate(point.x, point.y - radius * 0.85)
    context.rotate(Math.atan2(facingPoint.y - point.y, facingPoint.x - point.x) + Math.PI / 2)
    context.lineJoin = 'round'
    context.lineCap = 'round'
    draw()
    context.restore()
  }

  /**
   * The wolf is an upright billboard mirrored by its screen-space facing, not a rotated top-down
   * sprite like the Knife-spider. It draws its own body — filled while it can be seen, and while
   * hidden replaced by a doubled refraction outline plus eye glints that only catch the light when
   * the wolf is looking at the player. That is the whole tell budget: a player who turns around
   * can find it, a player who never turns cannot.
   *
   * Leaves a torso ellipse as the current path so the caller's outline (red during a windup) still
   * traces the animal, matching the Knife-spider branch's contract.
   */
  private renderInvisibleWolf(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const facingPoint = this.worldToScreen({
      x: enemy.position.x + enemy.facing.x,
      y: enemy.position.y + enemy.facing.y,
    }, node)
    const side = facingPoint.x >= point.x ? 1 : -1
    const phase = enemy.invisibleWolf?.phase ?? null
    const hidden = !this.enemyVisible(enemy)
    const crouched = phase === null
      ? enemy.state === 'idle' || enemy.state === 'noticing'
      : phase === 'unaware' || phase === 'stalking' || phase === 'closing'
    const windup = this.enemyWindupProgress(enemy)
    // The haunches compress through the first three quarters of the windup, then everything
    // unspools forward — the pounce is readable before it lands.
    const coil = Math.min(1, windup / 0.75)
    const spring = Math.max(0, (windup - 0.75) / 0.25)

    // Everything below is in units of `radius`, measured from the wolf's ground point, with +x
    // forward. A standing wolf is about two radii tall and three long including the head; the
    // stalking crouch takes a quarter of that height out of the legs, not out of the body.
    const backY = -radius * 1.82 * (crouched ? 0.78 : 1) + radius * 0.2 * coil
    const bellyY = backY + radius * 0.78
    const shoulderX = radius * 0.74
    const hipX = -radius * 0.86
    const lunge = radius * 0.55 * spring
    const headX = shoulderX + radius * 0.66 + lunge
    const headY = crouched || coil > 0 ? bellyY - radius * 0.1 : backY - radius * 0.34
    const gaitPeriodMs = crouched ? 640 : 320
    const gait = this.elapsedMs / gaitPeriodMs + enemy.position.x

    const traceLeg = (
      anchorX: number,
      anchorY: number,
      kneeBend: number,
      legPhase: number,
      alpha: number,
    ): void => {
      const swing = Math.sin(gait + legPhase)
      const reach = radius * 0.42 * spring
      context.save()
      context.globalAlpha = context.globalAlpha * alpha
      context.beginPath()
      context.moveTo(anchorX, anchorY)
      context.lineTo(
        anchorX + kneeBend * radius + swing * radius * 0.12 + reach * 0.6,
        anchorY * 0.44,
      )
      context.lineTo(
        anchorX + swing * radius * 0.34 + reach,
        -Math.max(0, swing) * radius * 0.13,
      )
      context.stroke()
      context.restore()
    }

    const traceLegs = (): void => {
      // Diagonal pairs; the far side is faded so the animal reads as a body with depth. Front
      // knees fold back, rear hocks fold forward, which is what makes a canine silhouette legible.
      traceLeg(shoulderX - radius * 0.28, bellyY - radius * 0.06, -0.08, Math.PI, 0.45)
      traceLeg(hipX - radius * 0.04, bellyY - radius * 0.12, 0.12, 0, 0.45)
      traceLeg(shoulderX - radius * 0.02, bellyY - radius * 0.06, -0.08, 0, 1)
      traceLeg(hipX + radius * 0.2, bellyY - radius * 0.12, 0.12, Math.PI, 1)
    }

    const traceTail = (): void => {
      const droop = crouched ? 1 : 0.4
      context.beginPath()
      context.moveTo(hipX + radius * 0.04, backY + radius * 0.22)
      context.quadraticCurveTo(
        hipX - radius * 0.56,
        backY + radius * (0.18 + droop * 0.55),
        hipX - radius * 1.02,
        backY + radius * (0.1 + droop * 1.15),
      )
      context.stroke()
    }

    const traceBody = (): void => {
      // Rump, back line dipping behind the withers, deep chest, tucked belly, heavy haunch.
      context.moveTo(hipX, backY + radius * 0.2)
      context.quadraticCurveTo(
        -radius * 0.1,
        backY - radius * 0.06,
        shoulderX + radius * 0.06,
        backY + radius * 0.08,
      )
      context.quadraticCurveTo(
        shoulderX + radius * 0.44,
        backY + radius * 0.4,
        shoulderX + radius * 0.16,
        bellyY + radius * 0.08,
      )
      context.quadraticCurveTo(
        radius * 0.06,
        bellyY - radius * 0.06,
        hipX + radius * 0.32,
        bellyY,
      )
      context.quadraticCurveTo(
        hipX - radius * 0.2,
        bellyY - radius * 0.16,
        hipX,
        backY + radius * 0.2,
      )
      context.closePath()
      // Neck, slung low out of the shoulders in the stalking pose.
      context.moveTo(shoulderX - radius * 0.06, backY + radius * 0.04)
      context.lineTo(headX - radius * 0.22, headY - radius * 0.24)
      context.lineTo(headX - radius * 0.16, headY + radius * 0.28)
      context.lineTo(shoulderX - radius * 0.12, backY + radius * 0.46)
      context.closePath()
      // Skull tapering into the muzzle.
      context.moveTo(headX - radius * 0.26, headY - radius * 0.3)
      context.lineTo(headX + radius * 0.26, headY - radius * 0.16)
      context.lineTo(headX + radius * 0.78, headY + radius * 0.1)
      context.lineTo(headX + radius * 0.72, headY + radius * 0.26)
      context.lineTo(headX + radius * 0.1, headY + radius * 0.34)
      context.lineTo(headX - radius * 0.26, headY + radius * 0.22)
      context.closePath()
      for (const ear of [-0.18, 0.06]) {
        context.moveTo(headX + radius * ear, headY - radius * 0.26)
        context.lineTo(headX + radius * (ear + 0.12), headY - radius * 0.78)
        context.lineTo(headX + radius * (ear + 0.24), headY - radius * 0.2)
        context.closePath()
      }
    }

    const traceEyes = (): void => {
      for (const [offsetX, offsetY, size] of [[0.26, -0.06, 0.095], [0.12, -0.12, 0.07]]) {
        context.beginPath()
        context.arc(
          headX + radius * offsetX,
          headY + radius * offsetY,
          Math.max(0.9, radius * size),
          0,
          Math.PI * 2,
        )
        context.fill()
      }
    }

    const toPlayer = normalize({
      x: this.player.position.x - enemy.position.x,
      y: this.player.position.y - enemy.position.y,
    }, enemy.facing)
    const lookingAtPlayer = toPlayer.x * enemy.facing.x + toPlayer.y * enemy.facing.y
      > tuningValue(enemy.definition, 'glintDot', 0.55)

    context.save()
    context.translate(point.x, point.y)
    context.scale(side, 1)
    context.lineJoin = 'round'
    context.lineCap = 'round'
    if (hidden) {
      // Refraction, not fade: the caller's blanket 7% alpha is replaced by a doubled outline whose
      // strength climbs as the wolf commits to a run.
      context.globalAlpha = 1
      const pulse = 0.5 + Math.sin(this.elapsedMs / 520 + enemy.position.y) * 0.5
      const closeness = phase === 'closing'
        ? clamp(
            1 - vectorLength({
              x: this.player.position.x - enemy.position.x,
              y: this.player.position.y - enemy.position.y,
            }) / Math.max(1, tuningValue(enemy.definition, 'stalkRadius', 260)),
            0,
            1,
          )
        : 0
      const outlineAlpha = phase === 'closing'
        ? 0.16 + closeness * 0.29
        : 0.1 + pulse * 0.06
      context.strokeStyle = '#dbeaf4'
      context.lineWidth = Math.max(1, radius * 0.11)
      for (const [offsetX, offsetY, alpha] of [[0, 0, 1], [radius * 0.09, -radius * 0.05, 0.55]]) {
        context.save()
        context.translate(offsetX, offsetY)
        context.globalAlpha = outlineAlpha * alpha
        context.beginPath()
        traceBody()
        context.stroke()
        traceLegs()
        traceTail()
        context.restore()
      }
      if (lookingAtPlayer) {
        context.globalAlpha = 0.34 + pulse * 0.2
        context.fillStyle = '#f4f8ff'
        traceEyes()
      }
    } else {
      context.strokeStyle = enemy.definition.color
      context.lineWidth = Math.max(1.4, radius * 0.13)
      traceLegs()
      traceTail()
      context.beginPath()
      traceBody()
      context.fillStyle = enemy.definition.color
      context.fill()
      context.globalAlpha = context.globalAlpha * 0.85
      context.fillStyle = '#f7d97a'
      traceEyes()
    }
    // Left current so the caller's outline traces the real animal; see `drawEnemyBillboard`.
    context.beginPath()
    traceBody()
    context.restore()
  }

  /**
   * Слуга: a household drudge that never straightens up. A long apron hangs from stooped
   * shoulders to a hem that sways with its walk, the head bows so far forward that it reads as
   * faceless, and both arms swing loose. Winding up leans the whole body further over the player.
   */
  private renderServant(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const windup = this.enemyWindupProgress(enemy)
    const gait = this.elapsedMs / (this.enemyIsMoving(enemy) ? 330 : 950) + enemy.position.x
    const sway = Math.sin(gait)
    const stoop = radius * (0.14 + windup * 0.22)
    const shoulderY = -radius * 1.58
    const hemY = -radius * 0.02

    const traceApron = (): void => {
      context.moveTo(stoop - radius * 0.3, shoulderY + radius * 0.06)
      context.quadraticCurveTo(stoop, shoulderY - radius * 0.06, stoop + radius * 0.3, shoulderY + radius * 0.04)
      context.quadraticCurveTo(radius * 0.5, hemY - radius * 0.8, radius * (0.46 + sway * 0.05), hemY)
      context.lineTo(radius * (-0.46 + sway * 0.05), hemY)
      context.quadraticCurveTo(
        radius * -0.48,
        hemY - radius * 0.84,
        stoop - radius * 0.3,
        shoulderY + radius * 0.06,
      )
      context.closePath()
    }
    const arm = (phase: number): void => {
      const swing = Math.sin(gait + phase)
      context.beginPath()
      context.moveTo(stoop + radius * 0.18, shoulderY + radius * 0.14)
      context.quadraticCurveTo(
        stoop + radius * (0.4 + swing * 0.1),
        shoulderY + radius * 0.62,
        stoop + radius * (0.3 + swing * 0.24 + windup * 0.34),
        shoulderY + radius * (1.06 - windup * 0.44),
      )
      context.stroke()
    }

    this.drawEnemyBillboard(enemy, point, node, () => {
      context.lineWidth = Math.max(1.4, radius * 0.13)
      // Far arm goes behind the apron, near arm in front, so the walk cycle stays legible.
      context.strokeStyle = shadeEnemyColor(color, -0.36)
      arm(Math.PI)
      context.beginPath()
      traceApron()
      context.fillStyle = color
      context.fill()
      context.strokeStyle = shadeEnemyColor(color, -0.1)
      arm(0)
      // Short neck and a head bowed so far forward it reads as faceless.
      context.beginPath()
      context.moveTo(stoop - radius * 0.1, shoulderY + radius * 0.04)
      context.lineTo(stoop + radius * 0.14, shoulderY + radius * 0.02)
      context.lineTo(stoop + radius * 0.16, shoulderY - radius * 0.18)
      context.lineTo(stoop - radius * 0.06, shoulderY - radius * 0.16)
      context.closePath()
      context.fillStyle = shadeEnemyColor(color, -0.3)
      context.fill()
      context.beginPath()
      context.ellipse(
        stoop + radius * 0.14,
        shoulderY - radius * 0.3,
        radius * 0.24,
        radius * 0.2,
        0.42,
        0,
        Math.PI * 2,
      )
      context.fillStyle = shadeEnemyColor(color, -0.22)
      context.fill()
      context.beginPath()
      traceApron()
    })
  }

  /**
   * Бегущий степлер: the office stapler that came off the desk. Its base plate rides on six stubby
   * legs, the hinged upper jaw gapes wider the further its shot is wound up, and a pale staple sits
   * in the throat where the shot comes from.
   */
  private renderRunningStapler(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const windup = this.enemyWindupProgress(enemy)
    const gait = this.elapsedMs / 190 + enemy.position.x
    const gape = 0.12 + windup * 0.5
    const baseY = -radius * 0.62
    const hingeX = -radius * 0.92

    this.drawEnemyBillboard(enemy, point, node, () => {
      // Six stubby legs under the base plate, alternating in two tripods.
      context.strokeStyle = shadeEnemyColor(color, -0.35)
      context.lineWidth = Math.max(1.2, radius * 0.11)
      for (let leg = 0; leg < 3; leg += 1) {
        const legX = radius * (-0.66 + leg * 0.62)
        const swing = Math.sin(gait + leg * 2.1)
        context.beginPath()
        context.moveTo(legX, baseY + radius * 0.16)
        context.lineTo(legX + swing * radius * 0.16, -radius * 0.02)
        context.stroke()
      }
      // Base plate.
      context.beginPath()
      context.roundRect(hingeX, baseY, radius * 1.9, radius * 0.42, radius * 0.12)
      context.fillStyle = shadeEnemyColor(color, -0.18)
      context.fill()
      // Hinged upper jaw, opening toward the target.
      context.save()
      context.translate(hingeX + radius * 0.1, baseY - radius * 0.04)
      context.rotate(-gape)
      context.beginPath()
      context.roundRect(0, -radius * 0.46, radius * 1.78, radius * 0.5, radius * 0.14)
      context.fillStyle = color
      context.fill()
      // The staple waiting in the throat.
      context.beginPath()
      context.moveTo(radius * 1.42, -radius * 0.06)
      context.lineTo(radius * 1.62, -radius * 0.06)
      context.lineTo(radius * 1.62, radius * 0.16)
      context.strokeStyle = shadeEnemyColor(color, 0.62)
      context.lineWidth = Math.max(1, radius * 0.1)
      context.stroke()
      context.restore()
      // Only the base plate is outlined: the jaw swings, so no fixed box could follow it.
      context.beginPath()
      context.roundRect(hingeX, baseY, radius * 1.9, radius * 0.42, radius * 0.12)
    })
  }

  /**
   * Стражник: the only enemy that stands up straight. Armour plating, a helmet reduced to a visor
   * slit, and a slab shield carried between it and the player — the shield drops and the mace arm
   * comes up over the wind-up, which is what makes its swing readable.
   */
  private renderGuard(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const windup = this.enemyWindupProgress(enemy)
    const gait = this.elapsedMs / (this.enemyIsMoving(enemy) ? 340 : 1100) + enemy.position.x
    const shoulderY = -radius * 1.5
    const hipY = -radius * 0.78

    const traceCuirass = (): void => {
      context.moveTo(-radius * 0.42, shoulderY + radius * 0.08)
      context.quadraticCurveTo(0, shoulderY - radius * 0.16, radius * 0.42, shoulderY + radius * 0.08)
      context.lineTo(radius * 0.3, hipY)
      context.lineTo(-radius * 0.3, hipY)
      context.closePath()
    }

    this.drawEnemyBillboard(enemy, point, node, () => {
      // Legs.
      context.strokeStyle = shadeEnemyColor(color, -0.3)
      context.lineWidth = Math.max(2, radius * 0.18)
      for (const [legX, phase] of [[-0.2, 0], [0.18, Math.PI]] as const) {
        const swing = Math.sin(gait + phase)
        context.beginPath()
        context.moveTo(radius * legX, hipY)
        context.lineTo(radius * (legX + swing * 0.18), -radius * 0.04)
        context.stroke()
      }
      // Mace arm, raised behind the head through the wind-up.
      const maceX = radius * (-0.44 - windup * 0.12)
      const maceY = shoulderY + radius * (0.5 - windup * 1.02)
      context.strokeStyle = shadeEnemyColor(color, -0.16)
      context.lineWidth = Math.max(1.6, radius * 0.15)
      context.beginPath()
      context.moveTo(-radius * 0.2, shoulderY + radius * 0.16)
      context.lineTo(maceX, maceY)
      context.stroke()
      context.beginPath()
      context.arc(maceX, maceY, Math.max(1.5, radius * 0.17), 0, Math.PI * 2)
      context.fillStyle = shadeEnemyColor(color, -0.36)
      context.fill()
      // Cuirass: broad shoulders tapering to the belt.
      context.beginPath()
      traceCuirass()
      context.fillStyle = color
      context.fill()
      // Helmet, clear of the shoulder line, with a single lit visor slit.
      context.beginPath()
      context.roundRect(
        -radius * 0.24,
        shoulderY - radius * 0.66,
        radius * 0.52,
        radius * 0.58,
        radius * 0.15,
      )
      context.fillStyle = shadeEnemyColor(color, -0.28)
      context.fill()
      context.beginPath()
      context.rect(-radius * 0.12, shoulderY - radius * 0.46, radius * 0.36, radius * 0.09)
      context.fillStyle = shadeEnemyColor(color, 0.6)
      context.fill()
      // Slab shield on the near arm, dropping out of the way as the swing is loaded.
      context.save()
      context.translate(radius * (0.46 + windup * 0.14), shoulderY + radius * (0.62 + windup * 0.44))
      context.rotate(windup * 0.75)
      context.beginPath()
      context.roundRect(-radius * 0.2, -radius * 0.52, radius * 0.4, radius * 1.04, radius * 0.13)
      context.fillStyle = shadeEnemyColor(color, 0.14)
      context.fill()
      context.strokeStyle = shadeEnemyColor(color, -0.42)
      context.lineWidth = Math.max(1, radius * 0.07)
      context.stroke()
      context.restore()
      context.beginPath()
      traceCuirass()
    })
  }

  /**
   * Химера: a body assembled out of parts that never belonged together. Nothing about it is
   * symmetrical — the forelegs are long and the hind legs stunted, a ridge of spines runs the
   * wrong way down the back, and it carries two heads on two necks. Both heads snap forward
   * together on a wind-up, which is the only moment the thing looks like it has one intent.
   */
  private renderChimera(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const windup = this.enemyWindupProgress(enemy)
    const gait = this.elapsedMs / (this.enemyIsMoving(enemy) ? 380 : 900) + enemy.position.x
    const backY = -radius * 1.32
    const bellyY = backY + radius * 0.64
    const snap = radius * 0.42 * windup

    const traceTorso = (): void => {
      context.moveTo(-radius * 0.82, backY + radius * 0.3)
      context.quadraticCurveTo(-radius * 0.1, backY - radius * 0.1, radius * 0.78, backY + radius * 0.04)
      context.quadraticCurveTo(radius * 1.06, bellyY - radius * 0.1, radius * 0.6, bellyY + radius * 0.06)
      context.quadraticCurveTo(0, bellyY + radius * 0.12, -radius * 0.62, bellyY - radius * 0.06)
      context.quadraticCurveTo(-radius * 0.98, bellyY - radius * 0.3, -radius * 0.82, backY + radius * 0.3)
      context.closePath()
    }

    this.drawEnemyBillboard(enemy, point, node, () => {
      // Mismatched legs: long thin forelegs, short heavy hind legs.
      for (const [legX, length, thickness, phase, shade] of [
        [0.46, 1, 0.13, Math.PI, -0.34],
        [-0.5, 0.74, 0.2, 0, -0.34],
        [0.58, 1, 0.15, 0, -0.06],
        [-0.62, 0.74, 0.23, Math.PI, -0.06],
      ] as const) {
        const swing = Math.sin(gait + phase)
        context.strokeStyle = shadeEnemyColor(color, shade)
        context.lineWidth = Math.max(1.4, radius * thickness)
        context.beginPath()
        context.moveTo(radius * legX, bellyY - radius * 0.08)
        context.lineTo(radius * (legX + swing * 0.12), bellyY * (1 - length) - radius * 0.02)
        context.stroke()
      }
      // Lumpy torso, heavier at the shoulders than at the hips.
      context.beginPath()
      traceTorso()
      context.fillStyle = color
      context.fill()
      // Spines, growing the wrong way along the back.
      context.strokeStyle = shadeEnemyColor(color, -0.42)
      context.lineWidth = Math.max(1, radius * 0.08)
      for (let spine = 0; spine < 5; spine += 1) {
        const spineX = radius * (-0.62 + spine * 0.3)
        context.beginPath()
        context.moveTo(spineX, backY + radius * 0.12)
        context.lineTo(spineX - radius * 0.16, backY - radius * (0.16 + spine * 0.04))
        context.stroke()
      }
      // Lower head: round, drooping off a slack neck.
      context.strokeStyle = shadeEnemyColor(color, -0.2)
      context.lineWidth = Math.max(1.6, radius * 0.19)
      context.beginPath()
      context.moveTo(radius * 0.6, backY + radius * 0.2)
      context.lineTo(radius * 0.98 + snap, backY + radius * 0.56)
      context.stroke()
      context.beginPath()
      context.arc(radius * 1.1 + snap, backY + radius * 0.62, radius * 0.24, 0, Math.PI * 2)
      context.fillStyle = shadeEnemyColor(color, -0.2)
      context.fill()
      // Upper head: a long wedge on a raised neck.
      context.strokeStyle = shadeEnemyColor(color, 0.06)
      context.lineWidth = Math.max(1.6, radius * 0.22)
      context.beginPath()
      context.moveTo(radius * 0.66, backY + radius * 0.12)
      context.lineTo(radius * 0.94 + snap, backY - radius * 0.34)
      context.stroke()
      context.beginPath()
      context.moveTo(radius * 0.74 + snap, backY - radius * 0.52)
      context.lineTo(radius * 1.5 + snap, backY - radius * 0.26)
      context.lineTo(radius * 1.42 + snap, backY - radius * 0.08)
      context.lineTo(radius * 0.78 + snap, backY - radius * 0.14)
      context.closePath()
      context.fillStyle = shadeEnemyColor(color, 0.06)
      context.fill()
      // One eye each, deliberately different sizes.
      context.fillStyle = '#f2d46d'
      for (const [eyeX, eyeY, eyeR] of [
        [1.0, -0.38, 0.09],
        [1.16, 0.58, 0.06],
      ] as const) {
        context.beginPath()
        context.arc(radius * eyeX + snap, backY + radius * eyeY, Math.max(0.9, radius * eyeR), 0, Math.PI * 2)
        context.fill()
      }
      context.beginPath()
      traceTorso()
    })
  }

  /**
   * Исполин: a slab of a creature whose arms reach the floor and whose head is buried between its
   * shoulders. It deals no contact damage at all, so the model exists to telegraph the one thing
   * it does — over the wind-up both arms come up over the head, and they are what falls on the
   * ground zone.
   */
  private renderColossus(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const windup = this.enemyWindupProgress(enemy)
    const breath = Math.sin(this.elapsedMs / 900 + enemy.position.x) * 0.02
    const shoulderY = -radius * (1.34 + breath)
    const hipY = -radius * 0.5

    const traceTorso = (): void => {
      context.moveTo(-radius * 0.72, shoulderY + radius * 0.24)
      context.quadraticCurveTo(0, shoulderY - radius * 0.28, radius * 0.72, shoulderY + radius * 0.24)
      context.quadraticCurveTo(radius * 0.66, hipY - radius * 0.1, radius * 0.4, hipY)
      context.lineTo(-radius * 0.44, hipY)
      context.quadraticCurveTo(-radius * 0.7, hipY - radius * 0.1, -radius * 0.72, shoulderY + radius * 0.24)
      context.closePath()
    }

    this.drawEnemyBillboard(enemy, point, node, () => {
      // Two short, very thick legs.
      context.strokeStyle = shadeEnemyColor(color, -0.34)
      context.lineWidth = Math.max(3, radius * 0.34)
      for (const legX of [-0.3, 0.26]) {
        context.beginPath()
        context.moveTo(radius * legX, hipY)
        context.lineTo(radius * legX, -radius * 0.06)
        context.stroke()
      }
      // Arms: hanging past the knees at rest, swung over the head at the top of the wind-up.
      context.lineWidth = Math.max(3, radius * 0.28)
      for (const [armX, shade] of [[-0.52, -0.28], [0.5, -0.05]] as const) {
        const elbowY = shoulderY + radius * (0.62 - windup * 1.3)
        const handY = shoulderY + radius * (1.2 - windup * 2.1)
        context.strokeStyle = shadeEnemyColor(color, shade)
        context.beginPath()
        context.moveTo(radius * armX, shoulderY + radius * 0.16)
        context.quadraticCurveTo(
          radius * (armX * 1.5 - windup * 0.2),
          elbowY,
          radius * (armX * 1.1 + windup * 0.3),
          handY,
        )
        context.stroke()
      }
      // Torso: a boulder wider at the shoulders than anywhere else.
      context.beginPath()
      traceTorso()
      context.fillStyle = color
      context.fill()
      // Head, sunk so deep between the shoulders it is nearly swallowed.
      context.beginPath()
      context.ellipse(
        radius * 0.16,
        shoulderY - radius * 0.16,
        radius * 0.26,
        radius * 0.22,
        0,
        0,
        Math.PI * 2,
      )
      context.fillStyle = shadeEnemyColor(color, -0.4)
      context.fill()
      context.beginPath()
      context.arc(radius * 0.24, shoulderY - radius * 0.18, Math.max(0.9, radius * 0.06), 0, Math.PI * 2)
      context.fillStyle = '#f2d46d'
      context.fill()
      context.beginPath()
      traceTorso()
    })
  }

  /**
   * Тень Куратора: a tall figure that is mostly absence. The robe never resolves into legs — its
   * hem frays into smoke that moves on its own — and the head is a blank dome with one pale slit.
   * Its arms are posed from the live boss phase, so the silhouette changes with the fight: down
   * while it watches the door, one arm raised while it reads the archive, both thrown wide before
   * the leap.
   */
  private renderCuratorShadow(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const windup = this.enemyWindupProgress(enemy)
    const profile = this.enemyCombatProfile(enemy)
    const drift = this.elapsedMs / 620 + enemy.position.x
    const shoulderY = -radius * 1.78
    const hemY = -radius * 0.04
    // The three shipped phases read as three postures; anything else falls back to the first.
    const stance = profile.attackKind === 'leap' ? 2 : profile.attackKind === 'projectile' ? 1 : 0

    const traceRobe = (): void => {
      context.moveTo(-radius * 0.36, shoulderY)
      context.quadraticCurveTo(radius * 0.66, shoulderY + radius * 0.9, radius * 0.94, hemY - radius * 0.3)
      for (let step = 0; step <= 8; step += 1) {
        const frayX = radius * (0.94 - step * 0.24)
        const fray = Math.sin(drift + step * 1.3) * radius * 0.13
        context.lineTo(frayX, hemY + fray * (step % 2 === 0 ? 1 : 0.4))
      }
      context.quadraticCurveTo(-radius * 0.8, shoulderY + radius * 0.9, -radius * 0.36, shoulderY)
      context.closePath()
    }

    this.drawEnemyBillboard(enemy, point, node, () => {
      // Robe, from narrow shoulders down to a hem that never touches the floor cleanly.
      context.beginPath()
      traceRobe()
      context.fillStyle = color
      context.fill()
      // Arms, posed from the phase.
      context.strokeStyle = shadeEnemyColor(color, 0.14)
      context.lineWidth = Math.max(1.8, radius * 0.13)
      const arms: Array<[number, number, number, number]> = stance === 0
        ? [[-0.3, 0.2, -0.42, 1], [0.32, 0.2, 0.46, 1]]
        : stance === 1
          ? [[-0.3, 0.2, -0.34, 0.9], [0.32, 0.1, 0.86, -0.5 - windup * 0.3]]
          : [[-0.3, 0.1, -1.06, -0.1 - windup * 0.4], [0.32, 0.1, 1.1, -0.1 - windup * 0.4]]
      for (const [fromX, fromY, toX, toY] of arms) {
        context.beginPath()
        context.moveTo(radius * fromX, shoulderY + radius * fromY)
        context.quadraticCurveTo(
          radius * (fromX + toX) * 0.9,
          shoulderY + radius * (fromY + toY) * 0.5,
          radius * toX,
          shoulderY + radius * toY,
        )
        context.stroke()
      }
      // Blank dome of a head with a single lit slit where a face should be.
      context.beginPath()
      context.ellipse(0, shoulderY - radius * 0.22, radius * 0.27, radius * 0.34, 0, 0, Math.PI * 2)
      context.fillStyle = shadeEnemyColor(color, -0.4)
      context.fill()
      context.beginPath()
      context.roundRect(-radius * 0.17, shoulderY - radius * 0.28, radius * 0.36, radius * 0.07, radius * 0.035)
      context.fillStyle = shadeEnemyColor(color, 0.72)
      context.fill()
      context.beginPath()
      traceRobe()
    })
  }

  /**
   * Мать тараканов: the same insect as her brood, at twenty times the size and seen from the same
   * overhead angle. Head with working mandibles and antennae, a pronotum shield over the thorax,
   * seamed wing cases, and a banded abdomen heavy with the next hundred. Her six legs run a real
   * alternating-tripod gait rather than sitting still.
   */
  private renderCockroachMother(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const gait = this.elapsedMs / (this.enemyIsMoving(enemy) ? 190 : 620) + enemy.position.x
    const bite = Math.max(0, Math.sin(this.elapsedMs / 240)) * this.enemyWindupProgress(enemy)

    this.drawEnemyTopDown(enemy, point, radius, node, () => {
      // Six legs, three per side, in two alternating tripods.
      context.strokeStyle = shadeEnemyColor(color, -0.34)
      context.lineWidth = Math.max(1.6, radius * 0.09)
      for (const side of [-1, 1]) {
        for (let leg = 0; leg < 3; leg += 1) {
          const swing = Math.sin(gait + (leg + (side > 0 ? 1 : 0)) * Math.PI) * 0.2
          const rootY = radius * (-0.72 + leg * 0.44)
          context.beginPath()
          context.moveTo(side * radius * 0.42, rootY)
          context.lineTo(side * radius * 0.94, rootY + radius * (0.2 + swing))
          context.lineTo(side * radius * 1.36, rootY - radius * (0.24 - swing))
          context.stroke()
        }
      }
      // Two antennae sweeping ahead of her.
      context.lineWidth = Math.max(1.2, radius * 0.05)
      for (const side of [-1, 1]) {
        const sweep = Math.sin(gait * 0.5 + (side > 0 ? 1.7 : 0)) * 0.3
        context.beginPath()
        context.moveTo(side * radius * 0.16, -radius * 1.08)
        context.quadraticCurveTo(
          side * radius * (0.7 + sweep),
          -radius * 1.7,
          side * radius * (0.4 + sweep * 1.6),
          -radius * 2.16,
        )
        context.stroke()
      }
      // Abdomen under the wing cases, then the broad pronotum shield over the thorax.
      context.beginPath()
      context.ellipse(0, radius * 0.32, radius * 0.8, radius * 1.12, 0, 0, Math.PI * 2)
      context.fillStyle = color
      context.fill()
      context.beginPath()
      context.ellipse(0, -radius * 0.62, radius * 0.82, radius * 0.6, 0, 0, Math.PI * 2)
      context.fillStyle = shadeEnemyColor(color, -0.2)
      context.fill()
      // Wing-case seam down the back and the abdominal bands showing past it.
      context.strokeStyle = shadeEnemyColor(color, -0.44)
      context.lineWidth = Math.max(1, radius * 0.05)
      context.beginPath()
      context.moveTo(0, -radius * 0.4)
      context.lineTo(0, radius * 1.24)
      context.stroke()
      for (let band = 0; band < 3; band += 1) {
        const bandY = radius * (0.62 + band * 0.26)
        context.beginPath()
        context.moveTo(-radius * 0.6, bandY)
        context.quadraticCurveTo(0, bandY + radius * 0.16, radius * 0.6, bandY)
        context.stroke()
      }
      // Head and mandibles, which open while she winds up.
      context.beginPath()
      context.ellipse(0, -radius * 1.16, radius * 0.42, radius * 0.32, 0, 0, Math.PI * 2)
      context.fillStyle = shadeEnemyColor(color, -0.34)
      context.fill()
      context.strokeStyle = shadeEnemyColor(color, -0.52)
      context.lineWidth = Math.max(1.2, radius * 0.07)
      for (const side of [-1, 1]) {
        context.beginPath()
        context.moveTo(side * radius * 0.22, -radius * 1.26)
        context.lineTo(side * radius * (0.26 + bite * 0.36), -radius * 1.62)
        context.stroke()
      }
      context.beginPath()
      context.ellipse(0, -radius * 0.06, radius * 0.84, radius * 1.5, 0, 0, Math.PI * 2)
    })
  }

  /**
   * Таракан: the brood, drawn from above like their mother but reduced to what still reads at a
   * few pixels — a seamed body, two antennae and six legs on one alternating gait. Everything is
   * clamped to a minimum stroke so a hundred of them stay countable instead of turning to mush.
   */
  private renderSwarmCockroach(
    enemy: RuntimeEnemy,
    point: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const color = enemy.definition.color
    const gait = this.elapsedMs / 110 + enemy.position.x

    this.drawEnemyTopDown(enemy, point, radius, node, () => {
      context.strokeStyle = shadeEnemyColor(color, -0.3)
      context.lineWidth = Math.max(0.7, radius * 0.16)
      for (const side of [-1, 1]) {
        for (let leg = 0; leg < 3; leg += 1) {
          const swing = Math.sin(gait + (leg + (side > 0 ? 1 : 0)) * Math.PI) * 0.3
          const rootY = radius * (-0.5 + leg * 0.5)
          context.beginPath()
          context.moveTo(side * radius * 0.4, rootY)
          context.lineTo(side * radius * 1.15, rootY + radius * (0.3 + swing))
          context.stroke()
        }
        context.beginPath()
        context.moveTo(side * radius * 0.2, -radius * 0.9)
        context.lineTo(side * radius * 0.62, -radius * 1.75)
        context.stroke()
      }
      context.beginPath()
      context.ellipse(0, 0, radius * 0.72, radius * 1.15, 0, 0, Math.PI * 2)
      context.fillStyle = color
      context.fill()
      context.beginPath()
      context.moveTo(0, -radius * 0.5)
      context.lineTo(0, radius * 1)
      context.strokeStyle = shadeEnemyColor(color, -0.45)
      context.lineWidth = Math.max(0.6, radius * 0.12)
      context.stroke()
      context.beginPath()
      context.ellipse(0, 0, radius * 0.72, radius * 1.15, 0, 0, Math.PI * 2)
    })
  }

  private renderPlayer(node: LastChancesPlanNode): void {
    const context = this.context
    const groundPoint = this.worldToScreen(this.player.position, node)
    const radius = Math.max(8, this.config.player.radius * this.entityScale(node) * 1.55)
    const vaultLiftRadii = this.activeDash
      ? this.activeDash.poleVault
        ? this.poleVaultLiftRadii(this.activeDash)
        : this.bowDashLiftRadii(this.activeDash)
      : 0
    const point = {
      x: groundPoint.x,
      y: groundPoint.y - vaultLiftRadii * radius,
    }
    context.save()
    context.translate(groundPoint.x, groundPoint.y)
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
    const fullOuroborosSet = this.config.ouroborosSet
      && LAST_CHANCES_OUROBOROS_ITEMS.every(item => this.ouroborosEquipped[item])
    if (fullOuroborosSet) {
      this.renderOuroborosIcon(point.x, point.y - radius, radius)
    } else {
      context.beginPath()
      context.arc(point.x, point.y - radius, radius, 0, Math.PI * 2)
      context.fillStyle = this.config.renderer.player
      context.fill()
      context.strokeStyle = this.player.invulnerableMs > 0
        ? '#ffffff'
        : this.config.renderer.playerAccent
      context.lineWidth = 3
      context.stroke()
    }
    if (!this.primarySpearWeapon() && !this.primaryLongbowWeapon()) {
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
    this.renderHeldLongbow(node, point, radius)
  }

  private renderOuroborosIcon(x: number, y: number, radius: number): void {
    const context = this.context
    context.save()
    context.beginPath()
    context.arc(x, y, radius, 0, Math.PI * 2)
    context.fillStyle = '#10180f'
    context.fill()
    context.strokeStyle = '#d6b85f'
    context.lineWidth = Math.max(2, radius * 0.12)
    context.stroke()

    context.lineCap = 'round'
    context.beginPath()
    context.arc(x, y, radius * 0.67, -0.2, Math.PI * 1.72)
    context.strokeStyle = '#77a84d'
    context.lineWidth = radius * 0.34
    context.shadowColor = '#9ed36a'
    context.shadowBlur = radius * 0.35
    context.stroke()
    context.shadowBlur = 0

    for (let index = 0; index < 8; index += 1) {
      const angle = 0.2 + index * Math.PI * 0.21
      context.beginPath()
      context.arc(
        x + Math.cos(angle) * radius * 0.67,
        y + Math.sin(angle) * radius * 0.67,
        Math.max(1.2, radius * 0.055),
        0,
        Math.PI * 2,
      )
      context.fillStyle = '#d7c56f'
      context.fill()
    }

    const headX = x + Math.cos(-0.2) * radius * 0.67
    const headY = y + Math.sin(-0.2) * radius * 0.67
    context.save()
    context.translate(headX, headY)
    context.rotate(Math.PI * 0.42)
    context.beginPath()
    context.moveTo(radius * 0.28, 0)
    context.lineTo(-radius * 0.18, -radius * 0.24)
    context.lineTo(-radius * 0.22, radius * 0.24)
    context.closePath()
    context.fillStyle = '#98c963'
    context.fill()
    context.beginPath()
    context.arc(radius * 0.02, -radius * 0.08, Math.max(1, radius * 0.045), 0, Math.PI * 2)
    context.fillStyle = '#e45045'
    context.fill()
    context.restore()

    context.beginPath()
    context.arc(x, y, radius * 0.26, 0, Math.PI * 2)
    context.strokeStyle = '#d6b85f'
    context.lineWidth = Math.max(1.5, radius * 0.07)
    context.stroke()
    context.restore()
  }

  /**
   * A weapon lying on the floor is drawn as itself, so the player can tell a dropped Axe from a
   * dropped Katana without walking onto it. Everything is laid along one tilted axis and lit
   * brighter when it is the pickup the interact key would actually take.
   */
  private renderGroundWeapon(weapon: RuntimeGroundWeapon, node: LastChancesPlanNode): void {
    const context = this.context
    const point = this.worldToScreen(weapon.position, node)
    const nearby = this.nearestGroundWeapon()?.id === weapon.id
    const metal = nearby ? '#f4d98d' : '#b9c4c8'
    const grip = nearby ? '#8a6b3f' : '#66533b'
    const scale = nearby ? 1.12 : 1
    context.save()
    context.translate(point.x, point.y - 5)
    context.rotate(-0.38)
    context.scale(scale, scale)
    context.lineJoin = 'round'
    context.lineCap = 'round'
    context.shadowColor = nearby ? '#f0cf79' : '#8da1ad'
    context.shadowBlur = nearby ? 18 : 8
    context.strokeStyle = metal
    context.fillStyle = metal
    context.lineWidth = nearby ? 4 : 3

    if (weapon.weaponId === 'twohand-spear' || weapon.weaponId === 'twohand-spear-v2') {
      context.beginPath()
      context.moveTo(-19, 4)
      context.lineTo(14, -6)
      context.stroke()
      context.beginPath()
      context.moveTo(14, -6)
      context.lineTo(23, -9)
      context.lineTo(15, -1)
      context.closePath()
      context.fill()
    } else if (weapon.weaponId === 'twohand-bow') {
      if (this.longbowImage?.complete && this.longbowImage.naturalWidth > 0) {
        context.drawImage(this.longbowImage, -25, -8, 50, 16)
      } else {
        context.strokeStyle = '#9a6335'
        context.lineWidth = nearby ? 4 : 3
        context.beginPath()
        context.moveTo(-22, 0)
        context.quadraticCurveTo(-10, -11, 0, 0)
        context.quadraticCurveTo(10, 11, 22, 0)
        context.stroke()
        context.strokeStyle = '#d9d4c4'
        context.lineWidth = 1
        context.beginPath()
        context.moveTo(-22, 0)
        context.lineTo(0, 0)
        context.lineTo(22, 0)
        context.stroke()
      }
    } else if (weapon.weaponId === 'secondary-chain') {
      context.lineWidth = nearby ? 3 : 2.2
      for (let link = 0; link < 5; link += 1) {
        context.beginPath()
        context.ellipse(-16 + link * 8, 3 - link * 2.4, 4.4, 2.8, -0.38, 0, Math.PI * 2)
        context.stroke()
      }
    } else if (weapon.weaponId === 'either-claws') {
      for (const offset of [-4, 0, 4]) {
        context.beginPath()
        context.moveTo(-10 + offset, 7)
        context.quadraticCurveTo(4 + offset, 2, 13 + offset, -7)
        context.stroke()
      }
    } else if (weapon.weaponId === 'secondary-spider-knife') {
      context.beginPath()
      context.moveTo(-12, 6)
      context.lineTo(12, -5)
      context.lineTo(6, 2)
      context.closePath()
      context.fill()
      context.lineWidth = 1.6
      for (const side of [-1, 1]) {
        context.beginPath()
        context.moveTo(-6, 3)
        context.lineTo(-11, 3 + side * 8)
        context.stroke()
      }
    } else if (weapon.weaponId === 'twohand-axe') {
      context.beginPath()
      context.moveTo(-17, 6)
      context.lineTo(12, -5)
      context.stroke()
      context.beginPath()
      context.moveTo(4, -1)
      context.quadraticCurveTo(16, -14, 20, -2)
      context.quadraticCurveTo(12, 1, 6, 3)
      context.closePath()
      context.fill()
    } else if (weapon.weaponId === 'twohand-katana') {
      context.beginPath()
      context.moveTo(-14, 7)
      context.quadraticCurveTo(2, -1, 19, -10)
      context.stroke()
      context.lineWidth = 2
      context.strokeStyle = grip
      context.beginPath()
      context.moveTo(-14, 7)
      context.lineTo(-20, 9)
      context.stroke()
    } else if (weapon.weaponId === 'secondary-ouroboros-fang') {
      context.beginPath()
      context.moveTo(-11, 8)
      context.quadraticCurveTo(6, 4, 14, -9)
      context.quadraticCurveTo(4, -1, -8, 3)
      context.closePath()
      context.fill()
    } else {
      // Меч наемника and anything the Builder adds: a straight blade with a crossguard.
      context.beginPath()
      context.moveTo(-12, 5)
      context.lineTo(15, -7)
      context.stroke()
      context.lineWidth = 2.4
      context.beginPath()
      context.moveTo(-11, 0)
      context.lineTo(-7, 9)
      context.stroke()
    }

    context.shadowBlur = 0
    context.fillStyle = grip
    context.fillRect(-19, 2, 7, 5)
    context.restore()
  }

  private renderGroundOuroboros(
    pickup: RuntimeGroundOuroboros,
    node: LastChancesPlanNode,
  ): void {
    const point = this.worldToScreen(pickup.position, node)
    const nearby = this.nearestGroundOuroboros()?.id === pickup.id
    const radius = nearby ? 16 : 13
    const context = this.context
    context.save()
    context.shadowColor = nearby ? '#a7df71' : '#6f8f50'
    context.shadowBlur = nearby ? 22 : 10
    this.renderOuroborosIcon(point.x, point.y - radius, radius)
    if (pickup.items.length > 1) {
      context.beginPath()
      context.arc(point.x + radius * 0.8, point.y - radius * 1.8, radius * 0.48, 0, Math.PI * 2)
      context.fillStyle = '#171c13'
      context.fill()
      context.strokeStyle = '#d6b85f'
      context.lineWidth = 1.5
      context.stroke()
      context.font = `800 ${Math.max(9, radius * 0.72)}px system-ui`
      context.textAlign = 'center'
      context.textBaseline = 'middle'
      context.fillStyle = '#eef5d8'
      context.fillText(String(pickup.items.length), point.x + radius * 0.8, point.y - radius * 1.8)
    }
    context.restore()
  }

  private renderProjectile(projectile: RuntimeProjectile, node: LastChancesPlanNode): void {
    const point = this.worldToScreen(projectile.position, node)
    const radius = Math.max(3, projectile.radius * this.entityScale(node) * 1.6)
    if (projectile.persistentArrow) {
      this.drawBowArrow(
        point,
        projectile.velocity,
        node,
        Math.max(0.72, radius / 4),
        projectile.color,
        projectile.chemicalArrow === true,
        false,
        false,
      )
      return
    }
    if (projectile.source === 'player'
      && projectile.weaponId === this.primarySpearWeapon()?.id
      && isSpearReleaseBehavior(projectile.attack?.behavior)) {
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
    // Everything else is drawn as the thing that was actually fired, laid along its screen-space
    // travel direction. An incoming shot is marked by a red halo rather than a red outline: the
    // modelled shapes are line work, and outlining them would erase the shape itself.
    const context = this.context
    const ahead = this.worldToScreen({
      x: projectile.position.x + projectile.velocity.x,
      y: projectile.position.y + projectile.velocity.y,
    }, node)
    const travel = Math.atan2(ahead.y - point.y, ahead.x - point.x)
    const spin = this.elapsedMs / 70 + projectile.id
    context.save()
    context.translate(point.x, point.y - radius)
    context.rotate(travel)
    context.lineJoin = 'round'
    context.lineCap = 'round'
    context.shadowColor = projectile.source === 'enemy' ? '#ff5964' : projectile.color
    context.shadowBlur = projectile.source === 'enemy' ? 14 : 12
    context.fillStyle = projectile.color
    context.strokeStyle = projectile.color

    if (projectile.sourceId === 'running-stapler') {
      // A staple, tumbling end over end.
      context.rotate(spin)
      context.lineWidth = Math.max(1.4, radius * 0.5)
      context.beginPath()
      context.moveTo(-radius * 0.8, radius * 0.95)
      context.lineTo(-radius * 0.8, -radius * 0.75)
      context.lineTo(radius * 0.8, -radius * 0.75)
      context.lineTo(radius * 0.8, radius * 0.95)
      context.stroke()
    } else if (projectile.sourceId === 'curator-shadow') {
      // A page out of the archive, fluttering edge-on as it flies.
      const flutter = 0.28 + Math.abs(Math.sin(this.elapsedMs / 90 + projectile.id)) * 0.72
      context.save()
      context.scale(1, flutter)
      context.beginPath()
      context.roundRect(-radius * 1.1, -radius * 1.3, radius * 2.2, radius * 2.6, radius * 0.2)
      context.fill()
      context.strokeStyle = shadeEnemyColor(projectile.color, -0.45)
      context.lineWidth = Math.max(0.8, radius * 0.16)
      for (let line = 0; line < 3; line += 1) {
        const lineY = radius * (-0.6 + line * 0.6)
        context.beginPath()
        context.moveTo(-radius * 0.7, lineY)
        context.lineTo(radius * (line === 2 ? 0.2 : 0.7), lineY)
        context.stroke()
      }
      context.restore()
    } else if (projectile.sourceId.startsWith('turret-')) {
      // A turret bolt: head, shaft and two fins.
      context.beginPath()
      context.moveTo(radius * 1.6, 0)
      context.lineTo(radius * 0.2, -radius * 0.6)
      context.lineTo(-radius * 1.2, -radius * 0.28)
      context.lineTo(-radius * 1.2, radius * 0.28)
      context.lineTo(radius * 0.2, radius * 0.6)
      context.closePath()
      context.fill()
      context.lineWidth = Math.max(1, radius * 0.22)
      for (const side of [-1, 1]) {
        context.beginPath()
        context.moveTo(-radius * 0.9, side * radius * 0.24)
        context.lineTo(-radius * 1.7, side * radius * 0.9)
        context.stroke()
      }
    } else if (projectile.source === 'player') {
      // A thrown shard, longer than it is wide so its heading is unmistakable.
      context.beginPath()
      context.moveTo(radius * 1.9, 0)
      context.lineTo(0, -radius * 0.8)
      context.lineTo(-radius * 1.1, 0)
      context.lineTo(0, radius * 0.8)
      context.closePath()
      context.fill()
    } else {
      // Anything the Builder invents keeps the original ball-and-red-ring presentation.
      context.beginPath()
      context.arc(0, 0, radius, 0, Math.PI * 2)
      context.fill()
      if (projectile.source === 'enemy') {
        context.shadowBlur = 0
        context.strokeStyle = '#ff5964'
        context.lineWidth = 2
        context.stroke()
      }
    }
    context.restore()
    context.shadowBlur = 0
  }

  /** Detailed persistent arrow shared by flight, enemy/wall embeds and the overhead rain. */
  private drawBowArrow(
    point: LastChancesVector,
    worldDirection: LastChancesVector,
    node: LastChancesPlanNode,
    scale: number,
    color: string,
    chemical: boolean,
    exploded: boolean,
    tipAnchored: boolean,
    screenDirection?: LastChancesVector,
  ): void {
    const projectionOrigin = this.worldToScreen({ x: 0, y: 0 }, node)
    const projectionAhead = this.worldToScreen({
      x: worldDirection.x * 100,
      y: worldDirection.y * 100,
    }, node)
    const direction = screenDirection
      ? normalize(screenDirection)
      : normalize({
          x: projectionAhead.x - projectionOrigin.x,
          y: projectionAhead.y - projectionOrigin.y,
        })
    const angle = Math.atan2(direction.y, direction.x)
    const length = 38 * scale
    const tip = tipAnchored ? 0 : length * 0.48
    const rear = tip - length
    const context = this.context
    context.save()
    context.translate(point.x, point.y)
    context.rotate(angle)
    context.lineCap = 'round'
    context.lineJoin = 'round'
    context.shadowColor = chemical
      ? '#83ff54'
      : exploded ? '#ff7d32' : color
    context.shadowBlur = chemical ? 11 * scale : exploded ? 7 * scale : 4 * scale

    // Dark under-stroke keeps the ash shaft readable over both pale floor and chemical pools.
    context.strokeStyle = exploded ? '#24150f' : '#3b2717'
    context.lineWidth = Math.max(2, 3.2 * scale)
    context.beginPath()
    context.moveTo(rear, 0)
    context.lineTo(tip - 2.5 * scale, 0)
    context.stroke()
    context.strokeStyle = exploded ? '#6b3020' : '#b98246'
    context.lineWidth = Math.max(0.9, 1.35 * scale)
    context.beginPath()
    context.moveTo(rear, 0)
    context.lineTo(tip - 2.2 * scale, 0)
    context.stroke()

    // Forged bodkin head.
    context.fillStyle = exploded ? '#3b302a' : '#dbe2df'
    context.strokeStyle = exploded ? '#17110e' : '#6e7778'
    context.lineWidth = Math.max(0.7, scale)
    context.beginPath()
    context.moveTo(tip + 4.8 * scale, 0)
    context.lineTo(tip - 3.2 * scale, -3.2 * scale)
    context.lineTo(tip - 1.8 * scale, 0)
    context.lineTo(tip - 3.2 * scale, 3.2 * scale)
    context.closePath()
    context.fill()
    context.stroke()

    // Two differently lit feathers and the cut nock make each stuck arrow remain identifiable.
    const featherColor = chemical ? '#baff8c' : exploded ? '#6e3024' : '#e5d7c2'
    context.fillStyle = featherColor
    for (const side of [-1, 1]) {
      context.beginPath()
      context.moveTo(rear + 2 * scale, 0)
      context.quadraticCurveTo(
        rear + 8.5 * scale,
        side * 4.2 * scale,
        rear + 13.5 * scale,
        side * 1.2 * scale,
      )
      context.lineTo(rear + 12 * scale, 0)
      context.closePath()
      context.fill()
    }
    context.strokeStyle = '#d2b78a'
    context.lineWidth = Math.max(0.8, scale)
    context.beginPath()
    context.moveTo(rear - 1.8 * scale, -2.1 * scale)
    context.lineTo(rear, 0)
    context.lineTo(rear - 1.8 * scale, 2.1 * scale)
    context.stroke()

    if (chemical) {
      context.globalAlpha = 0.8
      context.fillStyle = '#9dff63'
      for (let bubble = 0; bubble < 3; bubble += 1) {
        const pulse = Math.sin(this.elapsedMs / 130 + bubble * 2.1 + point.x) * 0.8
        context.beginPath()
        context.arc(
          rear + (9 + bubble * 8) * scale,
          pulse * 2.5 * scale,
          (1.1 + bubble * 0.25) * scale,
          0,
          Math.PI * 2,
        )
        context.fill()
      }
    }
    context.restore()
  }

  private renderEmbeddedArrow(
    arrow: RuntimeEmbeddedArrow,
    node: LastChancesPlanNode,
  ): void {
    const point = this.worldToScreen(arrow.position, node)
    point.y -= arrow.attachment === 'enemy'
      ? Math.max(5, 9 * this.entityScale(node))
      : Math.max(1, 2 * this.entityScale(node))
    this.drawBowArrow(
      point,
      arrow.direction,
      node,
      arrow.attachment === 'enemy' ? 0.98 : 0.88,
      arrow.color,
      arrow.chemical,
      arrow.exploded,
      true,
      arrow.planted ? { x: arrow.direction.x * 0.12, y: 1 } : undefined,
    )
  }

  private renderFallingArrow(
    arrow: RuntimeFallingArrow,
    node: LastChancesPlanNode,
  ): void {
    const progress = 1 - clamp(arrow.remainingMs / Math.max(1, arrow.totalMs), 0, 1)
    const target = this.worldToScreen(arrow.target, node)
    const height = (1 - progress) * 155 + Math.sin(progress * Math.PI) * 22
    const point = {
      x: target.x - arrow.direction.x * (1 - progress) * 34,
      y: target.y - height,
    }
    const context = this.context
    context.save()
    context.translate(target.x, target.y)
    context.scale(1, 0.42)
    context.beginPath()
    context.ellipse(0, 0, 9 + progress * 7, 5 + progress * 3, 0, 0, Math.PI * 2)
    context.fillStyle = `rgba(0,0,0,${0.12 + progress * 0.3})`
    context.fill()
    context.restore()
    this.drawBowArrow(
      point,
      { x: arrow.direction.x * 0.18, y: 1 + Math.abs(arrow.direction.y) * 0.12 },
      node,
      0.86,
      arrow.attack.color,
      arrow.chemical,
      false,
      true,
      { x: arrow.direction.x * 0.12, y: 1 },
    )
  }

  private renderArrowChemicalPool(
    arrow: RuntimeEmbeddedArrow,
    node: LastChancesPlanNode,
  ): void {
    const context = this.context
    const center = this.worldToScreen(arrow.position, node)
    const pulse = 0.94 + Math.sin(this.elapsedMs / 310 + arrow.id) * 0.06
    context.save()
    context.globalAlpha = 0.2
    context.fillStyle = '#55e83e'
    context.strokeStyle = '#a7ff67'
    context.shadowColor = '#63ff45'
    context.shadowBlur = 13
    context.lineWidth = 1.5
    this.traceProjectedCircle(arrow.position, 46 * pulse, node, 32)
    context.fill()
    context.globalAlpha = 0.62
    context.stroke()
    context.fillStyle = '#c9ff83'
    context.shadowBlur = 7
    for (let bubble = 0; bubble < 4; bubble += 1) {
      const angle = arrow.id * 1.71 + bubble * Math.PI / 2 + this.elapsedMs / 900
      context.beginPath()
      context.arc(
        center.x + Math.cos(angle) * (10 + bubble * 3),
        center.y + Math.sin(angle) * (4 + bubble),
        1.5 + (bubble % 2),
        0,
        Math.PI * 2,
      )
      context.fill()
    }
    context.restore()
  }

  private renderBowRainZone(node: LastChancesPlanNode): void {
    const channel = [...this.bowChannels.values()]
      .find(candidate => candidate.behavior === 'bowRain')
    if (!channel || !this.bowRainTarget) return
    const radius = Math.max(1, tuningValue(channel.attack, 'zoneRadius', 105))
    const context = this.context
    const center = this.worldToScreen(this.bowRainTarget, node)
    const pulse = 0.5 + Math.sin(this.elapsedMs / 105) * 0.5
    context.save()
    context.setLineDash([9, 7])
    context.fillStyle = 'rgba(180, 214, 224, .055)'
    context.strokeStyle = '#d8f2f5'
    context.shadowColor = '#c7eef2'
    context.shadowBlur = 8 + pulse * 8
    context.lineWidth = 2.2
    this.traceProjectedCircle(this.bowRainTarget, radius, node, 40)
    context.fill()
    context.stroke()
    context.setLineDash([])
    context.globalAlpha = 0.58 + pulse * 0.28
    context.beginPath()
    context.moveTo(center.x - 9, center.y)
    context.lineTo(center.x + 9, center.y)
    context.moveTo(center.x, center.y - 6)
    context.lineTo(center.x, center.y + 6)
    context.stroke()
    context.restore()
  }

  private primaryLongbowWeapon(): LastChancesResolvedWeapon | null {
    const weapon = this.weapons.get('left')
    return isLongbowPrimary(weapon) ? weapon : null
  }

  /**
   * The generated wood-and-leather body stays crisp at game scale; string, hands and the nocked
   * arrow are procedural so they can follow the real input clock on every frame.
   */
  private renderHeldLongbow(
    node: LastChancesPlanNode,
    playerPoint: LastChancesVector,
    playerRadius: number,
  ): void {
    const weapon = this.primaryLongbowWeapon()
    if (!weapon) return
    const now = this.frameNowMs || performance.now()
    const primaryInput = this.controlInputSnapshot('left', now)
    const responseInput = this.controlInputSnapshot('right', now)
    const rapid = this.bowChannels.get('left')
    const rain = this.bowChannels.get('right')
    let drawProgress = 0.08
    if (this.bowDrawInputActive('left', primaryInput)) {
      const debit = this.bowDrawDebits.left
      const visualHeldMs = debit.exhausted
        ? Math.min(primaryInput.heldMs, debit.accruedMs)
        : primaryInput.heldMs
      drawProgress = resolveLastChancesBowCharge(
        weapon.attacks.hold,
        visualHeldMs,
      ).powerProgress
    } else if (rapid?.behavior === 'bowRapidFire') {
      const interval = Math.max(1, tuningValue(rapid.attack, 'shotIntervalMs', 120))
      drawProgress = 0.25 + 0.7 * clamp(rapid.shotAccumulatorMs / interval, 0, 1)
    } else if (rain?.behavior === 'bowRain') {
      const interval = Math.max(1, tuningValue(rain.attack, 'arrowIntervalMs', 145))
      drawProgress = 0.42 + 0.52 * clamp(rain.shotAccumulatorMs / interval, 0, 1)
    } else if (responseInput.pressed && this.bowResponseWindows.right) {
      const responseHeldMs = Math.max(
        0,
        now - this.bowResponseWindows.right.startedAtInputMs,
      )
      drawProgress = clamp(
        responseHeldMs / Math.max(
          1,
          tuningValue(weapon.secondaryAttacks?.doubleTapHold, 'goldEndMs', 530),
        ),
        0,
        1,
      )
    }
    const shotAge = this.elapsedMs - this.bowShotPresentation.atMs
    const cadence = rapid?.behavior === 'bowRapidFire'
      ? resolveLastChancesBowCadencePose(
          rapid.shotAccumulatorMs,
          Math.max(1, tuningValue(rapid.attack, 'shotIntervalMs', 120)),
          shotAge,
        )
      : null
    if (cadence) drawProgress = cadence.drawProgress
    const ordinaryRecoiling = !cadence && shotAge >= 0 && shotAge <= 170
    const recoiling = cadence
      ? shotAge >= 0 && shotAge <= cadence.recoilDurationMs
      : ordinaryRecoiling
    const recoil = cadence?.recoil
      ?? (ordinaryRecoiling ? Math.sin(clamp(shotAge / 170, 0, 1) * Math.PI) : 0)
    if (ordinaryRecoiling) drawProgress *= 0.15

    let aim = normalize(this.player.aim)
    if (recoiling) {
      aim = normalize(this.bowShotPresentation.direction, aim)
    }
    const origin = this.worldToScreen(this.player.position, node)
    const ahead = this.worldToScreen({
      x: this.player.position.x + aim.x * 100,
      y: this.player.position.y + aim.y * 100,
    }, node)
    // Обстрел fires over the hero's head; the ground cursor is represented by its own zone.
    const screenAim = rain
      ? { x: 0, y: -1 }
      : normalize({ x: ahead.x - origin.x, y: ahead.y - origin.y })
    const aimAngle = Math.atan2(screenAim.y, screenAim.x)
    const bowAngle = aimAngle + Math.PI / 2
    const forward = playerRadius * (0.42 - recoil * 0.2)
    const center = {
      x: playerPoint.x + screenAim.x * forward,
      y: playerPoint.y - playerRadius + screenAim.y * forward - (rain ? playerRadius * 0.55 : 0),
    }
    const width = playerRadius * 4.25
    const imageAspect = this.longbowImage && this.longbowImage.naturalWidth > 0
      ? this.longbowImage.naturalHeight / this.longbowImage.naturalWidth
      : 0.31
    const height = width * imageAspect
    const pull = width * (0.015 + drawProgress * 0.23)
    const localToScreen = (x: number, y: number): LastChancesVector => ({
      x: center.x + Math.cos(bowAngle) * x - Math.sin(bowAngle) * y,
      y: center.y + Math.sin(bowAngle) * x + Math.cos(bowAngle) * y,
    })
    const grip = localToScreen(0, 0)
    const nock = localToScreen(0, pull)
    const shoulder = {
      x: playerPoint.x,
      y: playerPoint.y - playerRadius,
    }
    const context = this.context

    context.save()
    context.strokeStyle = this.config.renderer.playerAccent
    context.lineWidth = Math.max(3, playerRadius * 0.25)
    context.lineCap = 'round'
    context.beginPath()
    context.moveTo(shoulder.x - screenAim.y * playerRadius * 0.2, shoulder.y)
    context.lineTo(grip.x, grip.y)
    context.moveTo(shoulder.x + screenAim.y * playerRadius * 0.2, shoulder.y)
    context.lineTo(nock.x, nock.y)
    context.stroke()
    context.restore()

    context.save()
    context.translate(center.x, center.y)
    context.rotate(bowAngle)
    context.shadowColor = weapon.augment === 'chemical' ? '#78ff54' : '#ff263c'
    context.shadowBlur = Math.max(9, height * 0.38)
    if (this.longbowImage?.complete && this.longbowImage.naturalWidth > 0) {
      context.drawImage(this.longbowImage, -width / 2, -height / 2, width, height)
    } else {
      const woodGradient = context.createLinearGradient(-width / 2, 0, width / 2, 0)
      woodGradient.addColorStop(0, '#5d321c')
      woodGradient.addColorStop(0.5, '#d39a54')
      woodGradient.addColorStop(1, '#5d321c')
      context.strokeStyle = woodGradient
      context.lineWidth = Math.max(5, height * 0.18)
      context.beginPath()
      context.moveTo(-width * 0.48, 0)
      context.quadraticCurveTo(-width * 0.28, -height * 0.72, 0, 0)
      context.quadraticCurveTo(width * 0.28, height * 0.72, width * 0.48, 0)
      context.stroke()
    }

    // Dynamic bowstring and nocked arrow. Local +Y is backwards along the shot axis.
    context.shadowBlur = 0
    context.strokeStyle = '#e7dfcb'
    context.lineWidth = Math.max(1.1, playerRadius * 0.07)
    context.beginPath()
    context.moveTo(-width * 0.47, 0)
    context.lineTo(0, pull)
    context.lineTo(width * 0.47, 0)
    context.stroke()
    if (drawProgress > 0.12 || primaryInput.pressed || rapid || rain || responseInput.pressed) {
      const arrowLength = width * 0.62
      context.strokeStyle = '#b98246'
      context.lineWidth = Math.max(1.6, playerRadius * 0.09)
      context.beginPath()
      context.moveTo(0, pull + width * 0.16)
      context.lineTo(0, -arrowLength)
      context.stroke()
      context.fillStyle = this.elapsedMs < this.bowShotPresentation.goldenUntilMs
        ? '#ffe28a'
        : '#dfe5e2'
      context.beginPath()
      context.moveTo(0, -arrowLength - playerRadius * 0.32)
      context.lineTo(-playerRadius * 0.18, -arrowLength + playerRadius * 0.08)
      context.lineTo(playerRadius * 0.18, -arrowLength + playerRadius * 0.08)
      context.closePath()
      context.fill()
    }
    context.restore()

    context.save()
    context.fillStyle = this.config.renderer.player
    context.strokeStyle = this.config.renderer.playerAccent
    context.lineWidth = 1.5
    for (const handPoint of [grip, nock]) {
      context.beginPath()
      context.arc(handPoint.x, handPoint.y, Math.max(2.5, playerRadius * 0.18), 0, Math.PI * 2)
      context.fill()
      context.stroke()
    }
    context.restore()
  }

  /** Linear precision cue: two golden ticks, the authored gold segment and a live arrow sigil. */
  private renderBowChargeCue(node: LastChancesPlanNode): void {
    const weapon = this.primaryLongbowWeapon()
    if (!weapon || this.paused || !this.canUseRoomActions()) return
    const now = this.frameNowMs || performance.now()
    const drawInput = this.controlInputSnapshot('left', now)
    const responseInput = this.controlInputSnapshot('right', now)
    let heldMs = 0
    let maximumMs = 0
    let goldStartMs = 0
    let goldEndMs = 0
    let label = ''
    let inGoldenWindow = false
    if (this.bowDrawInputActive('left', drawInput)) {
      const draw = weapon.attacks.hold
      const debit = this.bowDrawDebits.left
      heldMs = debit.exhausted
        ? Math.min(drawInput.heldMs, debit.accruedMs)
        : drawInput.heldMs
      const charge = resolveLastChancesBowCharge(draw, heldMs)
      maximumMs = draw.charge?.maxMs ?? 1000
      goldStartMs = tuningValue(draw, 'goldStartMs', 670)
      goldEndMs = tuningValue(draw, 'goldEndMs', 760)
      inGoldenWindow = charge.inGoldenWindow
      label = 'НАТЯГ'
    } else if (responseInput.pressed && this.bowResponseWindows.right) {
      const response = weapon.secondaryAttacks?.doubleTapHold
      if (!response) return
      heldMs = Math.max(0, now - this.bowResponseWindows.right.startedAtInputMs)
      maximumMs = Math.max(1, response.durationMs)
      goldStartMs = tuningValue(response, 'goldStartMs', 470)
      goldEndMs = tuningValue(response, 'goldEndMs', 530)
      inGoldenWindow = heldMs >= goldStartMs && heldMs <= goldEndMs
      label = 'ОТВЕТ'
    } else if (this.elapsedMs >= this.bowShotPresentation.goldenUntilMs) return

    const playerPoint = this.worldToScreen(this.player.position, node)
    const width = 132
    const y = playerPoint.y - Math.max(72, this.config.player.radius * this.entityScale(node) * 4.5)
    const progress = maximumMs > 0 ? clamp(heldMs / maximumMs, 0, 1) : 1
    const goldStart = maximumMs > 0 ? clamp(goldStartMs / maximumMs, 0, 1) : 0.67
    const goldEnd = maximumMs > 0 ? clamp(goldEndMs / maximumMs, 0, 1) : 0.76
    const x = playerPoint.x - width / 2
    const context = this.context
    context.save()
    context.lineCap = 'round'
    context.strokeStyle = 'rgba(228,235,239,.28)'
    context.lineWidth = 7
    context.beginPath()
    context.moveTo(x, y)
    context.lineTo(x + width, y)
    context.stroke()
    context.strokeStyle = inGoldenWindow ? '#ffe07b' : '#d9edf0'
    context.shadowColor = inGoldenWindow ? '#ffd54e' : '#b8e6ec'
    context.shadowBlur = inGoldenWindow ? 18 : 7
    context.lineWidth = 4
    context.beginPath()
    context.moveTo(x, y)
    context.lineTo(x + width * progress, y)
    context.stroke()
    const revealGoldenSegment = label !== 'НАТЯГ' || progress >= 2 / 3
    if (revealGoldenSegment) {
      context.strokeStyle = '#ffc83f'
      context.lineWidth = 5
      context.beginPath()
      context.moveTo(x + width * goldStart, y)
      context.lineTo(x + width * goldEnd, y)
      context.stroke()
      context.lineWidth = 2
      for (const marker of [goldStart, goldEnd]) {
        context.beginPath()
        context.moveTo(x + width * marker, y - 7)
        context.lineTo(x + width * marker, y + 7)
        context.stroke()
      }
    }
    context.fillStyle = inGoldenWindow ? '#ffe486' : '#dbe7e4'
    context.font = '800 10px system-ui'
    context.textAlign = 'center'
    context.fillText(label || 'ТОЧНО!', playerPoint.x, y - 13)
    context.restore()

    const iconPoint = { x: playerPoint.x, y: y - 30 }
    this.drawBowArrow(
      iconPoint,
      { x: 0, y: -1 },
      node,
      0.68,
      inGoldenWindow ? '#ffe07b' : '#e5eee9',
      false,
      false,
      false,
    )
    const success = inGoldenWindow || this.elapsedMs < this.bowShotPresentation.goldenUntilMs
    const fireCue = success || revealGoldenSegment
    if (fireCue) {
      const pulse = 0.5 + Math.sin(this.elapsedMs / 70) * 0.5
      context.save()
      context.globalAlpha = (success ? 0.6 : 0.34) + pulse * (success ? 0.3 : 0.16)
      context.fillStyle = success ? '#ffcf45' : '#ff7a2f'
      context.shadowColor = success ? '#ffd34d' : '#ff7a2f'
      context.shadowBlur = success ? 18 : 10
      context.beginPath()
      context.moveTo(iconPoint.x - 5, iconPoint.y + 9)
      context.quadraticCurveTo(iconPoint.x - 10, iconPoint.y, iconPoint.x, iconPoint.y - 12)
      context.quadraticCurveTo(iconPoint.x + 11, iconPoint.y, iconPoint.x + 4, iconPoint.y + 10)
      context.closePath()
      context.fill()
      context.restore()
    }
  }

  private primarySpearWeapon(): LastChancesResolvedWeapon | null {
    const weapon = this.weapons.get('left')
    return weapon?.trait === 'spearDistance'
      && isSpearReleaseBehavior(weapon.attacks.hold.behavior)
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
    // v1's early release is the wide rassekatel behind its 52-unit shaft guard; v2's is
    // «Заколоть», a point-blank plant, so its preview drops the guard exactly as the strike does.
    const meleeCollider = charged.band.id !== 'early'
      ? null
      : presentation.weapon.attacks.hold.behavior === 'spearReleaseV2'
        ? {
            innerRange: tuningValue(charged.attack, 'earlyInnerRange', 0),
            strictInnerRange: false,
          }
        : { innerRange: tuningValue(charged.attack, 'earlyInnerRange', 52) }
    const collider = this.spearPreviewCollider(node, charged.attack, direction, meleeCollider)
    this.strokeSpearPreview(
      node,
      collider,
      presentation.visual.previewBand.color,
      presentation.visual.armed,
      presentation.visual.stage === 'late' ? 12 : 9,
    )
  }

  /**
   * Двуручное копьё v2 only: «Акали»'s own dotted volume, drawn alongside the замах's rather
   * than instead of it. The whole point of the rework is that a single wind-up now telegraphs
   * both of its outcomes at once, told apart by colour — замах in its warm band colours,
   * «Акали» in the spin's greens.
   */
  private renderSpearFollowUpPreview(node: LastChancesPlanNode): void {
    if (this.paused || !this.canUseRoomActions()) return
    const weapon = this.primarySpearWeapon()
    if (!weapon || !isSpearV2Primary(weapon)) return
    if (!this.gestureReady('left', 'holdThenDoubleTap')) return
    const presentation = this.spearChargePresentation(this.frameNowMs || performance.now())
    if (!presentation) return
    const followUp = attackWithLastChancesAugment(weapon.attacks.holdThenDoubleTap, weapon)
    const charged = resolveLastChancesChargedAttack(followUp, presentation.heldMs)
    // Unlike the замах preview the held time is not rounded up to the next band: the follow-up
    // simply is not available yet below its first band, and the preview should say so.
    if (!charged.band) return
    const direction = normalize(this.player.aim)
    const collider: LastChancesRuntimeCollider = charged.band.id === 'spin-early'
      ? this.spearPreviewCollider(node, charged.attack, direction, {
          innerRange: tuningValue(charged.attack, 'spinEarlyInnerRange', 52),
          strictInnerRange: true,
          rotationDegrees: undefined,
        })
      : {
          // A 360° sweep resolves to one bar at its current angle, which telegraphs nothing.
          // The ring the spin will carve is the honest preview of it.
          shape: 'circle',
          center: { ...this.player.position },
          innerRadius: Math.max(0, charged.attack.collider?.innerRange ?? 0),
          outerRadius: Math.max(charged.attack.range, charged.attack.radius),
        }
    this.strokeSpearPreview(node, collider, charged.band.color, true, 9)
  }

  private renderPoleVaultTrajectoryPreview(node: LastChancesPlanNode): void {
    if (this.paused || !this.canUseRoomActions()) return
    const secondary = this.weapons.get('right')
    if (!isSpearV2Secondary(secondary)) return
    const stance = this.heldChannels.get('right')
    const activeVault = this.activeDash?.weaponId === secondary.id
      && this.activeDash.poleVault
      ? this.activeDash
      : null
    const selected = activeVault !== null
      && activeVault.elapsedMs <= tuningValue(
        activeVault.attack,
        'trajectoryFlashMs',
        180,
      )
    if (!selected && stance?.attack.behavior !== 'spearStance') return
    if (!selected && !this.gestureReady('right', 'holdThenDoubleTap')) return

    const attack = activeVault?.attack ?? secondary.attacks.holdThenDoubleTap
    const direction = activeVault?.direction ?? normalize(this.player.aim)
    const originWorld = activeVault?.origin ?? this.player.position
    const travel = activeVault
      ? activeVault.attack.range
      : this.poleVaultTravelDistance(
          node,
          originWorld,
          direction,
          attack.range,
          this.config.player.radius,
        )
    const renderedRadius = Math.max(
      8,
      this.config.player.radius * this.entityScale(node) * 1.55,
    )
    const height = tuningValue(attack, 'trajectoryHeightRadii', 4.2) * renderedRadius
    const context = this.context
    context.save()
    context.globalAlpha = selected ? 0.96 : 0.34
    context.strokeStyle = selected ? '#dcfff0' : '#6ee7a8'
    context.shadowColor = '#6ee7a8'
    context.shadowBlur = selected ? 20 : 7
    context.lineWidth = selected ? 3.6 : 2
    context.lineCap = 'round'
    context.setLineDash(selected ? [5, 4] : [3, 9])
    context.beginPath()
    const points = 28
    for (let index = 0; index <= points; index += 1) {
      const progress = index / points
      const world = {
        x: originWorld.x + direction.x * travel * progress,
        y: originWorld.y + direction.y * travel * progress,
      }
      const point = this.worldToScreen(world, node)
      const runDistanceRatio = clamp(tuningValue(attack, 'runDistanceRatio', 0.18), 0, 0.8)
      const flightProgress = clamp(
        (progress - runDistanceRatio) / Math.max(EPSILON, 1 - runDistanceRatio),
        0,
        1,
      )
      point.y -= Math.sin(flightProgress * Math.PI) * height
      if (index === 0) context.moveTo(point.x, point.y)
      else context.lineTo(point.x, point.y)
    }
    context.stroke()
    context.restore()
  }

  /**
   * The volume a spear release would occupy. Melee bands preview the sector they will swing;
   * everything else previews the projectile's ray, cut short by whatever wall would stop it.
   */
  private spearPreviewCollider(
    node: LastChancesPlanNode,
    attack: LastChancesAttackDefinition,
    direction: LastChancesVector,
    meleeCollider: Partial<LastChancesColliderDefinition> | null,
  ): LastChancesRuntimeCollider {
    if (meleeCollider) {
      return resolveAttackCollider(this.player.position, direction, {
        ...attack,
        kind: 'melee',
        collider: {
          ...(attack.collider ?? { traceMs: 900 }),
          shape: 'sector',
          ...meleeCollider,
        },
      })
    }
    const projectileRadius = Math.max(attack.radius, (attack.collider?.width ?? 0) / 2)
    const spawnOffset = Math.max(0, tuningValue(attack, 'projectileSpawnOffset', 0))
    const startDistance = this.config.player.radius + projectileRadius + 2 + spawnOffset
    const start = {
      x: this.player.position.x + direction.x * startDistance,
      y: this.player.position.y + direction.y * startDistance,
    }
    const travel = this.spearPreviewTravelDistance(
      node,
      start,
      direction,
      Math.max(0, attack.range - spawnOffset),
      projectileRadius,
    )
    return {
      shape: 'capsule',
      start,
      end: { x: start.x + direction.x * travel, y: start.y + direction.y * travel },
      radius: projectileRadius,
    }
  }

  /** Draws one dotted telegraph. Kept separate so several can share the frame. */
  private strokeSpearPreview(
    node: LastChancesPlanNode,
    collider: LastChancesRuntimeCollider,
    color: string,
    armed: boolean,
    arrowSize: number,
  ): void {
    const context = this.context
    const alpha = armed ? 0.82 : 0.36
    const pulse = 0.78 + Math.sin(this.elapsedMs / 115) * 0.12
    context.save()
    context.setLineDash([9, 7])
    context.lineCap = 'round'
    context.lineJoin = 'round'
    context.globalAlpha = alpha * pulse
    context.strokeStyle = color
    context.fillStyle = color
    context.shadowColor = color
    context.shadowBlur = armed ? 12 : 5
    context.lineWidth = armed ? 2.6 : 1.7
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

  /**
   * A pole vault is blocked by the arena edge and by its landing footprint, not by obstacles
   * crossed while airborne. If the authored endpoint is occupied, walk the endpoint backwards
   * until the furthest collision-free landing is found; intermediate walls remain jumpable.
   */
  private poleVaultTravelDistance(
    node: LastChancesPlanNode,
    start: LastChancesVector,
    direction: LastChancesVector,
    requestedTravel: number,
    radius: number,
  ): number {
    const axisLimit = (
      position: number,
      component: number,
      extent: number,
    ): number => {
      if (component > EPSILON) return (extent - radius - position) / component
      if (component < -EPSILON) return (radius - position) / component
      return Number.POSITIVE_INFINITY
    }
    const boundaryTravel = clamp(Math.min(
      requestedTravel,
      axisLimit(start.x, direction.x, node.arena.width),
      axisLimit(start.y, direction.y, node.arena.height),
    ), 0, requestedTravel)
    const pointAt = (distance: number): LastChancesVector => ({
      x: start.x + direction.x * distance,
      y: start.y + direction.y * distance,
    })
    const canLandAt = (distance: number): boolean => {
      const point = pointAt(distance)
      return point.x >= radius
        && point.x <= node.arena.width - radius
        && point.y >= radius
        && point.y <= node.arena.height - radius
        && !node.arena.obstacles.some(obstacle => pointHitsObstacle(point, radius, obstacle))
    }
    if (canLandAt(boundaryTravel)) return boundaryTravel
    const step = Math.max(2, radius * 0.25)
    for (let distance = boundaryTravel - step; distance > 0; distance -= step) {
      if (canLandAt(distance)) return distance
    }
    return 0
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

  /**
   * Pole-vault presentation follows the real readable phases: lower during the run, plant the
   * tip at one fixed ground point, climb around that point, release into flight, then recover to
   * guard on landing. The planted segment is anchored independently of the airborne player.
   */
  private renderPoleVaultSpear(
    node: LastChancesPlanNode,
    playerPoint: LastChancesVector,
    playerRadius: number,
    dash: RuntimeDash,
  ): void {
    const vault = dash.poleVault
    if (!vault) return
    const runEnd = vault.runMs
    const plantEnd = runEnd + vault.plantMs
    const riseEnd = plantEnd + vault.riseMs
    const flightEnd = riseEnd + vault.flightMs
    const totalMs = flightEnd + vault.landMs
    const elapsed = clamp(dash.elapsedMs, 0, totalMs)
    const direction = normalize(dash.direction)
    const ordinaryCenter = { x: playerPoint.x, y: playerPoint.y - playerRadius }
    let layout = this.spearSpriteLayout(node, ordinaryCenter, direction, playerRadius)
    let gripSpacing = playerRadius * 0.4
    let drawWideArms = false

    if (elapsed < runEnd) {
      const progress = elapsed / Math.max(1, runEnd)
      const running = this.spearSpriteLayout(
        node,
        ordinaryCenter,
        direction,
        playerRadius,
        0.12 + progress * 0.38,
      )
      layout = {
        ...running,
        center: {
          x: running.center.x + running.axis.x * playerRadius * (0.15 + progress * 0.3),
          y: running.center.y + running.axis.y * playerRadius * (0.15 + progress * 0.3),
        },
      }
      gripSpacing = playerRadius * 0.55
    } else if (elapsed < riseEnd) {
      const plantWorld = {
        x: dash.origin.x + direction.x * dash.attack.range * vault.runDistanceRatio,
        y: dash.origin.y + direction.y * dash.attack.range * vault.runDistanceRatio,
      }
      const tip = this.worldToScreen(plantWorld, node)
      const riseProgress = elapsed <= plantEnd
        ? 0
        : (elapsed - plantEnd) / Math.max(1, vault.riseMs)
      const hands = {
        x: ordinaryCenter.x,
        y: ordinaryCenter.y - playerRadius * (0.55 + riseProgress * 0.75),
      }
      const axis = normalize({
        x: tip.x - hands.x,
        y: tip.y - hands.y,
      }, layout.axis)
      layout = {
        ...layout,
        center: {
          x: tip.x - axis.x * layout.width * (1 - layout.pivotRatio),
          y: tip.y - axis.y * layout.width * (1 - layout.pivotRatio),
        },
        axis,
        perpendicular: { x: -axis.y, y: axis.x },
      }
      gripSpacing = playerRadius * 0.9
      drawWideArms = true
    } else {
      const recoveryProgress = elapsed <= flightEnd
        ? clamp((elapsed - riseEnd) / Math.max(1, vault.flightMs), 0, 1)
        : clamp((elapsed - flightEnd) / Math.max(1, vault.landMs), 0, 1)
      const released = this.spearSpriteLayout(
        node,
        ordinaryCenter,
        direction,
        playerRadius,
        elapsed <= flightEnd
          ? -0.62 + recoveryProgress * 0.32
          : -0.3 + recoveryProgress * 0.3,
      )
      layout = {
        ...released,
        center: {
          x: released.center.x - released.axis.x * playerRadius * (0.35 - recoveryProgress * 0.2),
          y: released.center.y - playerRadius * (0.5 - recoveryProgress * 0.35),
        },
      }
      gripSpacing = playerRadius * (0.72 - recoveryProgress * 0.32)
      drawWideArms = true
    }

    const firstGrip = {
      x: layout.center.x - layout.axis.x * gripSpacing,
      y: layout.center.y - layout.axis.y * gripSpacing,
    }
    const secondGrip = {
      x: layout.center.x + layout.axis.x * gripSpacing,
      y: layout.center.y + layout.axis.y * gripSpacing,
    }
    const context = this.context
    if (drawWideArms) {
      context.save()
      context.strokeStyle = this.config.renderer.playerAccent
      context.lineWidth = Math.max(3, playerRadius * 0.28)
      context.lineCap = 'round'
      context.beginPath()
      context.moveTo(ordinaryCenter.x - playerRadius * 0.28, ordinaryCenter.y)
      context.lineTo(firstGrip.x, firstGrip.y)
      context.moveTo(ordinaryCenter.x + playerRadius * 0.28, ordinaryCenter.y)
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

  private renderHeldSpear(
    node: LastChancesPlanNode,
    playerPoint: LastChancesVector,
    playerRadius: number,
  ): void {
    const spear = this.primarySpearWeapon()
    if (!spear || this.projectiles.some(projectile => (
      projectile.weaponId === spear.id && isSpearReleaseBehavior(projectile.attack?.behavior)
    ))) return
    const spearDash = this.activeDash?.weaponId === spear.id ? this.activeDash : null
    if (spearDash?.poleVault) {
      this.renderPoleVaultSpear(node, playerPoint, playerRadius, spearDash)
      return
    }

    let direction = normalize(this.player.aim)
    let forwardDirection = direction
    let forwardWorld = 0
    let liftRadii = 0
    let forwardRadii = 0
    let verticalTilt = 0
    let wideGripPose = false
    const activeArea = [...this.activeAreas]
      .reverse()
      .find(area => area.weaponId === spear.id)
    const activeDash = this.activeDash?.weaponId === spear.id ? this.activeDash : null
    if (activeDash?.ram) {
      // «Прорыв» holds the «Прокол» pose it morphed out of for the whole run, and drives the
      // spear further forward as the charge builds — the taran silhouette.
      direction = normalize(activeDash.direction)
      forwardDirection = direction
      const intensity = this.breakthroughIntensity(activeDash)
      forwardRadii = 0.3 + 0.7 * intensity
      liftRadii = 0.05 * Math.sin(this.elapsedMs / 70) * intensity
    } else if (activeArea) {
      const progress = clamp(
        1 - activeArea.remainingMs / Math.max(1, activeArea.totalMs),
        0,
        1,
      )
      const shape = activeArea.attack.collider?.shape
        ?? (activeArea.kind === 'burst' ? 'circle' : 'sector')
      if (activeArea.attack.behavior === 'parry') {
        forwardDirection = normalize(activeArea.direction)
        direction = rotateVector(forwardDirection, Math.PI / 2)
        forwardRadii = 0.42
        wideGripPose = true
      } else if (activeArea.attack.behavior === 'spearShove') {
        forwardDirection = normalize(activeArea.direction)
        direction = rotateVector(forwardDirection, Math.PI / 2)
        forwardWorld = Math.sin(progress * Math.PI) * Math.min(58, activeArea.attack.range * 0.5)
        wideGripPose = true
      } else if (activeArea.attack.behavior === 'spearKick') {
        forwardDirection = normalize(activeArea.direction)
        direction = rotateVector(forwardDirection, Math.PI / 2)
        forwardRadii = 0.28
        wideGripPose = true
      } else if (activeArea.attack.behavior === 'spearStance') {
        direction = normalize(activeArea.direction)
        forwardDirection = direction
        forwardRadii = 0.48
      } else if (shape === 'sweep' && activeArea.attack.behavior === 'spearOverheadSpin') {
        // v1 yaws the shaft around the body like a lawnmower. «Акали» instead holds it up and
        // spins it overhead, so the pose lifts clear of the grip and stops tilting.
        direction = rotateVector(activeArea.direction, activeArea.sweepDegrees * Math.PI / 180)
        liftRadii = 2.3
        forwardRadii = 0
        verticalTilt = 0
      } else if (shape === 'sweep') {
        direction = rotateVector(activeArea.direction, activeArea.sweepDegrees * Math.PI / 180)
      } else if (shape === 'sector' && activeArea.attack.behavior === 'spearReleaseV2') {
        // «Заколоть» does not sweep: the spear drives forward and pitches tip-down into the
        // ground, so it borrows the thrust's forward push instead of the slash arc.
        direction = normalize(activeArea.direction)
        const attackTravel = Math.max(24, activeArea.attack.range)
        forwardWorld = Math.sin(progress * Math.PI) * Math.min(48, attackTravel * 0.4)
        verticalTilt = 0.42 + 0.28 * progress
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
      forwardDirection = direction
      if (activeDash.poleVault) {
        const vault = activeDash.poleVault
        const plantStart = vault.runMs
        const airStart = plantStart + vault.plantMs
        if (activeDash.elapsedMs < plantStart) {
          forwardRadii = 0.28
        } else if (activeDash.elapsedMs < airStart) {
          forwardRadii = 0.72
          verticalTilt = 1.55
        } else {
          const airProgress = clamp(
            (activeDash.elapsedMs - airStart)
              / Math.max(1, vault.riseMs + vault.flightMs + vault.landMs),
            0,
            1,
          )
          forwardRadii = 0.35 - airProgress * 0.45
          verticalTilt = 1.1 - airProgress * 1.65
        }
      }
    } else {
      const now = this.frameNowMs || performance.now()
      const secondary = this.weapons.get('right')
      const secondaryInput = isSpearV2Secondary(secondary)
        ? this.controlInputSnapshot('right', now)
        : null
      const delayedShove = this.delayedAttacks.some(delayed => (
        delayed.context.hand === 'right'
        && delayed.context.weapon.id === spear.id
        && delayed.attack.behavior === 'spearShove'
      ))
      const chargingKick = secondaryInput?.pressed === true
        && (secondaryInput.sequence === 'secondTap'
          || secondaryInput.candidateGesture === 'doubleTapHold')
        && secondaryInput.heldMs >= this.config.input.holdMs
      if (isSpearV2Secondary(secondary)
        && (this.activeParryAttack?.behavior === 'parry' || delayedShove || chargingKick)) {
        forwardDirection = normalize(this.player.aim)
        direction = rotateVector(forwardDirection, Math.PI / 2)
        forwardRadii = delayedShove ? 0.48 : 0.42
        wideGripPose = true
      } else {
        const charge = this.spearChargePresentation(now)
        if (!charge) {
          forwardDirection = direction
        } else {
          liftRadii = charge.visual.liftRadii
          forwardRadii = charge.visual.forwardRadii
          verticalTilt = charge.visual.verticalTilt
        }
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
        x: this.player.position.x + forwardDirection.x * forwardWorld,
        y: this.player.position.y + forwardDirection.y * forwardWorld,
      }, node)
      center.x += forward.x - origin.x
      center.y += forward.y - origin.y
    }
    const layout = this.spearSpriteLayout(node, center, direction, playerRadius, verticalTilt)
    const context = this.context
    const gripSpacing = playerRadius * (wideGripPose ? 0.9 : 0.34)
    const firstGrip = {
      x: center.x - layout.axis.x * gripSpacing,
      y: center.y - layout.axis.y * gripSpacing,
    }
    const secondGrip = {
      x: center.x + layout.axis.x * gripSpacing,
      y: center.y + layout.axis.y * gripSpacing,
    }
    if (liftRadii > 0.08 || wideGripPose) {
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

  private traceProjectedCircle(
    center: LastChancesVector,
    radius: number,
    node: LastChancesPlanNode,
    steps = 40,
  ): void {
    const context = this.context
    context.beginPath()
    for (let index = 0; index < steps; index += 1) {
      const angle = index / steps * Math.PI * 2
      const point = this.worldToScreen({
        x: center.x + Math.cos(angle) * radius,
        y: center.y + Math.sin(angle) * radius,
      }, node)
      if (index === 0) context.moveTo(point.x, point.y)
      else context.lineTo(point.x, point.y)
    }
    context.closePath()
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
    } else if (effect.kind === 'shock') {
      const eased = 1 - (1 - progress) ** 3
      const worldRadius = effect.range * eased
      // Opening flash: a filled disc that collapses over the first quarter of the effect.
      if (progress < 0.25) {
        context.save()
        context.globalAlpha = alpha * (1 - progress / 0.25) * 0.5
        this.traceProjectedCircle(effect.position, effect.range * (0.2 + progress), node)
        context.fillStyle = '#ffd9c8'
        context.fill()
        context.restore()
      }
      context.shadowColor = effect.color
      context.shadowBlur = 18 * alpha
      context.lineWidth = 3 + 9 * alpha
      this.traceProjectedCircle(effect.position, worldRadius, node)
      context.stroke()
      const spokes = 10
      for (let index = 0; index < spokes; index += 1) {
        const angle = (index / spokes) * Math.PI * 2
        const start = this.worldToScreen({
          x: effect.position.x + Math.cos(angle) * worldRadius * 0.7,
          y: effect.position.y + Math.sin(angle) * worldRadius * 0.7,
        }, node)
        const finish = this.worldToScreen({
          x: effect.position.x + Math.cos(angle) * worldRadius,
          y: effect.position.y + Math.sin(angle) * worldRadius,
        }, node)
        context.beginPath()
        context.moveTo(start.x, start.y)
        context.lineTo(finish.x, finish.y)
        context.lineWidth = 1 + 3 * alpha
        context.stroke()
      }
    } else if (effect.kind === 'burst') {
      const worldRadius = effect.range * progress
      if (effect.arcDegrees >= 360) {
        this.traceProjectedCircle(effect.position, worldRadius, node)
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
    corners.forEach((point, index) => index === 0
      ? context.moveTo(point.x, point.y)
      : context.lineTo(point.x, point.y))
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
    context.fillStyle = this.config.renderer.stamina
    context.fillText(
      `STAM ${Math.ceil(this.player.stamina)} / ${Math.ceil(effectiveStats.maxStamina)}`,
      22,
      86,
    )
    context.fillStyle = 'rgba(255,255,255,.54)'
    context.fillText(`ATK ${Math.round(effectiveStats.attackPower)}%`, 22, 104)
    if (this.currentNode) {
      context.textAlign = 'right'
      context.fillStyle = 'rgba(255,255,255,.66)'
      const tier = this.config.progression.tiers[this.currentNode.tierIndex]
      const tierLabel = tier?.id === 'opening'
        ? 'PROLOGUE'
        : tier?.kind === 'boss'
          ? 'BOSS'
          : `TIER ${this.currentNode.tierIndex
            + (this.config.progression.tiers[0]?.id === 'opening' ? 0 : 1)}`
      context.fillText(`${tierLabel} · ${this.currentNode.roomName}`, this.cssWidth - 22, 30)
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
