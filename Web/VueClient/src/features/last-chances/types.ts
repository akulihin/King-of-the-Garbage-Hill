export const LAST_CHANCES_HANDS = ['left', 'right'] as const
export const LAST_CHANCES_GESTURES = [
  'tap',
  'doubleTap',
  'doubleTapHold',
  'hold',
  'holdThenDoubleTap',
] as const
export const LAST_CHANCES_ATTACK_KINDS = ['melee', 'projectile', 'dash', 'burst'] as const
export const LAST_CHANCES_ATTACK_BEHAVIORS = [
  'standard',
  'parry',
  'spearRam',
  'spearRelease',
  'spearSpin',
  'spearShove',
  'spearKick',
  'spearStance',
  'poleVault',
  // Двуручное копьё v2. `spearHunt` on `attacks.tap` is also the weapon-set
  // discriminator (`isSpearV2Primary`): it must stay per-attack-set, because a
  // weapon-level trait/tuning check would leak onto the shared secondary hand.
  'spearHunt',
  'spearPierce',
  'spearBreakthrough',
  'spearReleaseV2',
  'spearOverheadSpin',
  'chainStrike',
  'chainSpin',
  'chainThrow',
  'chainHook',
  'chainBind',
  'clawSlash',
  'clawRend',
  'clawDisarm',
  'clawDash',
  'clawDeepStrike',
  'spiderSlash',
  'spiderImpale',
  'spiderFlurry',
  'spiderThrow',
  'spiderTwist',
  'axeSwing',
  'axeGrapple',
  'axeThrow',
  'axeParry',
  'axeSpin',
  'axeLeap',
  'katanaCombo',
  'katanaOverhead',
  'katanaFlurry',
  'katanaCharge',
  'katanaDance',
  'katanaParry',
  'katanaHop',
  'katanaHopSlash',
  'katanaIaido',
  'katanaFlash',
  'swordRhythm',
  'swordOpening',
  'swordFollowUp',
  'bowShot',
  'bowDoubleShot',
  'bowRapidFire',
  'bowDraw',
  'bowScatter',
  'bowDodge',
  'bowJump',
  'bowRiposte',
  'bowRain',
  'bowIgnite',
  'disabled',
] as const
export const LAST_CHANCES_COLLIDER_SHAPES = ['sector', 'capsule', 'circle', 'sweep'] as const
export const LAST_CHANCES_DAMAGE_TYPES = ['physical', 'true'] as const
export const LAST_CHANCES_STATUS_KINDS = [
  'bleed',
  'poison',
  'burn',
  'chemical',
  'stun',
  'microstun',
  'disarm',
  'slow',
  'attackSlow',
  'healingBlocked',
  'armorBreak',
  'opening',
  'bound',
  'unstoppable',
] as const
export const LAST_CHANCES_STATUS_REFRESH_MODES = ['refresh', 'extend', 'stack', 'replace'] as const
export const LAST_CHANCES_WEAPON_TRAITS = [
  'spearDistance',
  'chainDotCarrier',
  'clawParity',
  'spiderDurability',
  'axeHookRecovery',
  'katanaFlow',
  'swordRhythm',
  'ouroborosFang',
  'longbowPersistence',
] as const
export const LAST_CHANCES_WEAPON_RESOURCE_KINDS = ['chain', 'durability', 'rhythm'] as const
export const LAST_CHANCES_AUGMENTS = [
  'none',
  'bleed',
  'poison',
  'fire',
  'chemical',
  'ouroborosAcid',
] as const
export const LAST_CHANCES_OUROBOROS_ITEMS = ['fang', 'acid', 'scale'] as const
export const LAST_CHANCES_ENEMY_ROLES = ['creep', 'cockroach', 'standard', 'elite', 'boss'] as const
export const LAST_CHANCES_ENEMY_ATTACK_KINDS = ['melee', 'leap', 'projectile', 'heavy', 'zone'] as const
export const LAST_CHANCES_ZONE_SHAPES = ['circle', 'square', 'triangle'] as const
export const LAST_CHANCES_ARENA_EDGES = ['top', 'bottom', 'left', 'right'] as const
export const LAST_CHANCES_BOSS_HOLE_SHAPES = ['circle', 'square', 'triangle', 'diamond'] as const
export const LAST_CHANCES_HAZARD_KINDS = ['spikes', 'mentalFog'] as const
export const LAST_CHANCES_EQUIP_MODES = [
  'twoHanded',
  'eitherHand',
  'primaryOnly',
  'secondaryOnly',
  'hybrid',
] as const
export const LAST_CHANCES_CONTROL_SCHEMES = ['legacy', 'mylorik', 'dualsense'] as const
export const LAST_CHANCES_CONTROL_INTENTS = ['strike', 'technique', 'mobility'] as const
export const LAST_CHANCES_CONTROL_PHASES = ['press', 'tap', 'hold', 'arm', 'release'] as const
export const LAST_CHANCES_CONTROL_CONTEXTS = [
  'neutral',
  'continuation',
  'opening',
  'grapple',
  'tether',
  'spin',
  'dash',
  'stance',
  'flurry',
] as const
export const LAST_CHANCES_TACTILE_PROFILES = [
  'click',
  'ramp',
  'bandLight',
  'bandMedium',
  'bandStrong',
  'gate',
  'followUp',
  'blocked',
  'impact',
  'tension',
] as const
export const LAST_CHANCES_FEEDBACK_MODES = ['off', 'reduced', 'full'] as const
export const LAST_CHANCES_FEEDBACK_STATES = [
  'ready',
  'charge',
  'continuation',
  'tension',
  'blocked',
  'impact',
  'telegraph',
  'wriggle',
] as const

export type LastChancesHand = typeof LAST_CHANCES_HANDS[number]
export type LastChancesGesture = typeof LAST_CHANCES_GESTURES[number]
export type LastChancesAttackKind = typeof LAST_CHANCES_ATTACK_KINDS[number]
export type LastChancesAttackBehavior = typeof LAST_CHANCES_ATTACK_BEHAVIORS[number]
export type LastChancesColliderShape = typeof LAST_CHANCES_COLLIDER_SHAPES[number]
export type LastChancesDamageType = typeof LAST_CHANCES_DAMAGE_TYPES[number]
export type LastChancesStatusKind = typeof LAST_CHANCES_STATUS_KINDS[number]
export type LastChancesStatusRefreshMode = typeof LAST_CHANCES_STATUS_REFRESH_MODES[number]
export type LastChancesWeaponTrait = typeof LAST_CHANCES_WEAPON_TRAITS[number]
export type LastChancesWeaponResourceKind = typeof LAST_CHANCES_WEAPON_RESOURCE_KINDS[number]
export type LastChancesAugment = typeof LAST_CHANCES_AUGMENTS[number]
export type LastChancesOuroborosItem = typeof LAST_CHANCES_OUROBOROS_ITEMS[number]
export type LastChancesEnemyRole = typeof LAST_CHANCES_ENEMY_ROLES[number]
export type LastChancesEnemyAttackKind = typeof LAST_CHANCES_ENEMY_ATTACK_KINDS[number]
export type LastChancesZoneShape = typeof LAST_CHANCES_ZONE_SHAPES[number]
export type LastChancesArenaEdge = typeof LAST_CHANCES_ARENA_EDGES[number]
export type LastChancesBossHoleShape = typeof LAST_CHANCES_BOSS_HOLE_SHAPES[number]
export type LastChancesHazardKind = typeof LAST_CHANCES_HAZARD_KINDS[number]
export type LastChancesEquipMode = typeof LAST_CHANCES_EQUIP_MODES[number]
export type LastChancesControlScheme = typeof LAST_CHANCES_CONTROL_SCHEMES[number]
export type LastChancesControlIntent = typeof LAST_CHANCES_CONTROL_INTENTS[number]
export type LastChancesControlPhase = typeof LAST_CHANCES_CONTROL_PHASES[number]
export type LastChancesControlContext = typeof LAST_CHANCES_CONTROL_CONTEXTS[number]
export type LastChancesTactileProfile = typeof LAST_CHANCES_TACTILE_PROFILES[number]
export type LastChancesFeedbackMode = typeof LAST_CHANCES_FEEDBACK_MODES[number]
export type LastChancesFeedbackState = typeof LAST_CHANCES_FEEDBACK_STATES[number]
export type LastChancesFeedbackTier = 0 | 1 | 2
export type LastChancesPhase = 'planning' | 'playing' | 'interaction' | 'dead' | 'won' | 'outOfChances'
export type LastChancesEnemyState = 'idle' | 'noticing' | 'alerted' | 'chasing' | 'attacking' | 'dead'
export type LastChancesRoomArchetype =
  | 'combat'
  | 'chest'
  | 'rest'
  | 'event'
  | 'merchant'
  | 'trap'
  | 'puzzle'
export type LastChancesGestureInputPhase =
  | 'idle'
  | 'pressing'
  | 'doubleTapWindow'
  | 'secondPress'
  | 'holdFollowUpWindow'
  | 'holdFollowUp'
export type LastChancesGestureSequence = 'first' | 'secondTap' | 'afterHoldTap'
export type LastChancesGamepadProfile = 'standard' | 'sony-raw' | 'generic'
export type LastChancesGamepadStatus = 'unsupported' | 'disconnected' | 'idle' | 'active'

export interface LastChancesVector {
  x: number
  y: number
}

export interface LastChancesStats {
  maxHp: number
  maxMentalHealth: number
  maxStamina: number
  attackPower: number
  moveSpeed: number
  armor: number
}

export interface LastChancesStatErosion {
  maxHp: number
  maxMentalHealth: number
  maxStamina: number
  attackPower: number
  moveSpeed: number
  armor: number
}

/**
 * Stamina is the yellow bar under physical health. Every action debits it, skilled play refunds
 * part of the debit, and disengaging refills it quickly. An action that cannot be paid for is
 * refused outright rather than executed at a discount.
 */
export interface LastChancesStaminaDefinition {
  /** Points regained per second while at least one enemy is hunting the player. */
  regenPerSecond: number
  /** Points regained per second while nothing is hunting the player. */
  outOfCombatRegenPerSecond: number
  /** Default debit for an action without its own `staminaCost`. */
  attackCost: number
  /** Refund when the acting hand differs from the previous action's hand. */
  handAlternationRestore: number
  /** Refund for every action that continues an unbroken chain. */
  comboRestore: number
  /** Maximum gap between two actions that still counts as one chain. */
  comboWindowMs: number
}

export interface LastChancesColliderDefinition {
  shape: LastChancesColliderShape
  innerRange?: number
  /** Reject targets whose body overlaps the protected inner radius, not only centers fully inside it. */
  strictInnerRange?: boolean
  /** Explicit opt-out from the default rule that solid room obstacles block attacks. */
  passesThroughWalls?: boolean
  width?: number
  traceMs: number
  followsPlayer?: boolean
  tickMs?: number
  rotationDegrees?: number
}

export interface LastChancesAttackOverrides {
  damage?: number
  cooldownMs?: number
  range?: number
  radius?: number
  arcDegrees?: number
  durationMs?: number
  lingerMs?: number
  projectileSpeed?: number
  pierce?: number
  knockback?: number
  recoveryMs?: number
  rootMs?: number
  invulnerabilityMs?: number
  repeatHits?: number
  repeatIntervalMs?: number
  /** Per-band stamina price, resolved before the action is paid for. */
  staminaCost?: number
}

export interface LastChancesChargeBandDefinition {
  id: string
  label: string
  minMs: number
  color: string
  damageMultiplier?: number
  rangeMultiplier?: number
  knockbackMultiplier?: number
  durationMultiplier?: number
  speedMultiplier?: number
  overrides?: LastChancesAttackOverrides
}

export interface LastChancesChargeDefinition {
  maxMs: number
  bands: LastChancesChargeBandDefinition[]
}

export interface LastChancesHitEffectDefinition {
  status: LastChancesStatusKind
  durationMs: number
  stacks?: number
  chance?: number
  magnitude?: number
  tickDamage?: number
  tickMs?: number
  refresh?: LastChancesStatusRefreshMode
}

export interface LastChancesSweetSpotDefinition {
  minRange: number
  maxRange?: number
  damageMultiplier: number
  knockbackMultiplier?: number
  criticalMultiplier?: number
}

export interface LastChancesWeaponResourceDefinition {
  kind: LastChancesWeaponResourceKind
  max: number
  initial: number
  label?: string
  color?: string
}

export interface LastChancesAugmentHookDefinition {
  /** Only these authored actions receive the symbol effect; omit for a weapon-wide hook. */
  behaviors?: LastChancesAttackBehavior[]
  damageMultiplier?: number
  hitEffects?: LastChancesHitEffectDefinition[]
}

export interface LastChancesAttackDefinition {
  name: string
  /** Player-facing move copy shown in the gesture-memory panel. */
  description?: string
  kind: LastChancesAttackKind
  /** Schema-v3 actions may explicitly disable an un-authored gesture slot. */
  enabled?: boolean
  /** Runtime behavior hook. Required for every schema-v3 action. */
  behavior?: LastChancesAttackBehavior
  damage: number
  damageType?: LastChancesDamageType
  cooldownMs: number
  range: number
  radius: number
  arcDegrees: number
  durationMs: number
  /** Extra time that a melee/burst hitbox remains dangerous after its authored swing. */
  lingerMs?: number
  projectileSpeed: number
  pierce: number
  knockback: number
  color: string
  collider?: LastChancesColliderDefinition
  charge?: LastChancesChargeDefinition
  hitEffects?: LastChancesHitEffectDefinition[]
  recoveryMs?: number
  rootMs?: number
  invulnerabilityMs?: number
  repeatHits?: number
  repeatIntervalMs?: number
  cooldownRefundMs?: number
  resetCooldownOnKill?: boolean
  sweetSpot?: LastChancesSweetSpotDefinition
  /** Behavior-specific numeric knobs kept in JSON so prototype tuning does not require a rebuild. */
  tuning?: Record<string, number>
  resourceCost?: number
  consumeAllResource?: boolean
  /** Stamina spent by this action. Defaults to the global `stamina.attackCost`. */
  staminaCost?: number
}

export interface LastChancesMylorikActivationDefinition {
  gesture: LastChancesGesture
  intent: LastChancesControlIntent
  phase: LastChancesControlPhase
  /** Omitted means the activation is unconditional for its intent and phase. */
  context?: LastChancesControlContext
  /**
   * Physical edge that commits an armed strike continuation. Omitted definitions preserve
   * the legacy release commit; channels can opt into the press edge explicitly.
   */
  continuationDispatch?: 'press' | 'release'
  priority: number
  /** Enabled legacy slots may opt out only with an explicit designer-facing reason. */
  legacyOnlyReason?: string
}

export interface LastChancesDualSenseComboNodeDefinition {
  id: string
  gesture: LastChancesGesture
  entryContext: LastChancesControlContext
  activationThreshold: number
  dispatch: 'press' | 'release'
  holdBehavior: 'none' | 'charge' | 'channel'
  releaseBehavior: 'dispatch' | 'cancel'
  next: string[]
  cancel: 'release' | 'expiry'
  expiryMs: number
  tactileProfile: LastChancesTactileProfile
  /** Existing hold-charge band that must be armed before this branch becomes legal. */
  requiredChargeBandId?: string
  /** Dwell time in this pocket before its release outcome is telegraphed. */
  armMs?: number
  /** Clock used by armMs; `node` is the default pocket dwell, `input` starts at physical pull. */
  armClock?: 'node' | 'input'
  /** This branch is legal only while the current pocket is armed. */
  entryRequiresArmed?: boolean
  /** Armed-loop rumble; omitted nodes fall back to their commit or charge-band signature. */
  telegraph?: LastChancesFeedbackPulseDefinition[]
  /** One-shot push-through invitation; omitted push branches use the default double knock. */
  armedCue?: LastChancesFeedbackPulseDefinition[]
  /** Softer persistent detent applied after the pocket arms. */
  armedTriggerOverride?: Partial<LastChancesAdaptiveTriggerProfileDefinition>
  /** Minimum charge band selected for this node's gesture regardless of elapsed hold time. */
  chargeBandOverrideId?: string
  adaptiveOverride?: Partial<LastChancesAdaptiveTriggerProfileDefinition>
  /**
   * Motor tick on entering this node; explicit null makes the entry
   * adaptive-trigger-only. Omitted nodes fall back to the set's gateTick.
   */
  entryTick?: LastChancesGateTickDefinition | null
}

/** Short single motor tick marking a discrete state change. */
export interface LastChancesGateTickDefinition {
  durationMs: number
  magnitude: number
}

/** Charge-band pulse-count coding: band N plays N pulses of rising magnitude. */
export interface LastChancesBandTickDefinition {
  pulseMs: number
  gapMs: number
  magnitude: number
  magnitudeStep: number
}

/** Living-weapon escape bursts; intervals/magnitudes lerp calm→panic as durability drains. */
export interface LastChancesWeaponWriggleDefinition {
  calmIntervalMs: [number, number]
  panicIntervalMs: [number, number]
  calmMagnitude: number
  panicMagnitude: number
  pulseMs: number
  pulsesPerBurst: [number, number]
  curveExponent: number
}

/** Per-attack-set DualSense haptic personality; every field falls back to a global default. */
export interface LastChancesWeaponHapticsDefinition {
  /** Persistent resting trigger block while this set is armed (position/resistance/force fields). */
  baseTrigger?: Partial<LastChancesAdaptiveTriggerProfileDefinition>
  /** Default motor tick on gate entry; nodes may override via entryTick. */
  gateTick?: LastChancesGateTickDefinition
  bandTick?: LastChancesBandTickDefinition
  /** Rumble signature played when a committed action fires. */
  commitPattern?: LastChancesFeedbackPulseDefinition[]
  /** Feedback-only rising-edge ruler marks between gameplay gates. */
  depthTicks?: Array<{
    position: number
    tick: LastChancesGateTickDefinition
  }>
  wriggle?: LastChancesWeaponWriggleDefinition
}

export interface LastChancesAttackSetControlDefinition {
  /** Short player-facing description of the physical trigger/bumper role. */
  role: string
  mylorik: {
    activations: LastChancesMylorikActivationDefinition[]
  }
  dualsense: {
    instantGesture: LastChancesGesture
    triggerRole: string
    /**
     * Backward-compatible first root. Multi-route sets additionally list every root in
     * startNodeIds, keeping this value as their first entry for older definitions/tools.
     */
    startNodeId: string | null
    startNodeIds?: string[]
    nodes: LastChancesDualSenseComboNodeDefinition[]
    haptics?: LastChancesWeaponHapticsDefinition
    /**
     * Quick action fired when a pull releases before reaching any combo node:
     * the "click before the gate" (e.g. the spear's distance poke). Routes its
     * gesture instead of a node.
     */
    preGateGesture?: LastChancesGesture
  }
}

export interface LastChancesWeaponControlDefinition {
  primary: LastChancesAttackSetControlDefinition
  secondary?: LastChancesAttackSetControlDefinition
}

export interface LastChancesWeaponDefinition {
  id: string
  name: string
  /** Legacy equipped slot. New definitions use equipMode plus config.loadout. */
  hand?: LastChancesHand
  equipMode?: LastChancesEquipMode
  description?: string
  /** Chances paid when this weapon is claimed from an interaction. */
  chanceCost?: number
  /** Marks the exported concept whose weapon remains associated with the place of death. */
  corpseBound?: boolean
  trait?: LastChancesWeaponTrait
  /** A hybrid may accept an off-hand item without mirroring its own moves into an empty off-hand. */
  primaryHandOnly?: boolean
  /** Designer switch for the Sword's stagger/Unstoppable subsystem. */
  staggerEnabled?: boolean
  resource?: LastChancesWeaponResourceDefinition
  defaultAugment?: LastChancesAugment
  augmentHooks?: Partial<Record<LastChancesAugment, LastChancesAugmentHookDefinition>>
  /** Trait-level numeric knobs shared by both resolved hands. */
  tuning?: Record<string, number>
  attacks: Record<LastChancesGesture, LastChancesAttackDefinition>
  /** Follow-up basic strikes after attacks.tap, advanced cyclically inside the combo window. */
  tapCombo?: LastChancesAttackDefinition[]
  /** The second input set used while a two-handed or unsupplemented hybrid weapon occupies both hands. */
  secondaryAttacks?: Record<LastChancesGesture, LastChancesAttackDefinition>
  /** Follow-ups after secondaryAttacks.tap. */
  secondaryTapCombo?: LastChancesAttackDefinition[]
  /** Schema-v4 semantic bindings; gameplay attacks remain the stable gesture slots above. */
  controls?: LastChancesWeaponControlDefinition
}

export interface LastChancesLoadoutDefinition {
  /** Runtime pickups may leave the primary hand empty even though shipped starting loadouts do not. */
  primaryWeaponId: string | null
  /** Null leaves the off-hand empty; only a two-handed weapon mirrors its second input there. */
  secondaryWeaponId: string | null
  primaryAugment?: LastChancesAugment
  secondaryAugment?: LastChancesAugment
  artifactId?: string | null
  outfitId?: string | null
}

export interface LastChancesArtifactDefinition {
  id: string
  name: string
  description: string
  /** Fraction of combat mental damage prevented, from 0 through 1. */
  mentalDamageReduction?: number
  /** Fraction of actual damage dealt restored as physical health, from 0 through 1. */
  lifestealRatio?: number
  /** Multiplier applied to the player's maximum stamina, above 0. */
  maxStaminaMultiplier?: number
  /** Flat stamina points added to every stamina regeneration second. */
  staminaRegenPerSecond?: number
}

export interface LastChancesOutfitDefinition {
  id: string
  name: string
  description: string
  armorBonus: number
  moveSpeedMultiplier: number
  emptyRightHandDash?: {
    distance: number
    durationMs: number
    cooldownMs: number
  }
  /** Multiplier applied to stamina restored from any source — regeneration and attack rewards alike. */
  staminaRegenMultiplier?: number
}

export interface LastChancesOuroborosSetDefinition {
  name: string
  fangWeaponId: string
  acidAugment: LastChancesAugment
  scaleOutfitId: string
  chanceCosts: Record<LastChancesOuroborosItem, number>
  /** Additive outgoing-damage fraction earned per qualifying finishing blow. */
  fangDamagePerKill: number
  /** Lifesteal fraction gained for every Chance ever spent on Acid pickups. */
  acidLifestealPerChance: number
  /** Additive incoming-damage reduction earned per Scale pickup in the same room. */
  scaleDamageReductionPerPickup: number
}

export interface LastChancesOuroborosPickupDefinition {
  item: LastChancesOuroborosItem
  position: LastChancesVector
}

export interface LastChancesResolvedWeapon {
  id: string
  name: string
  hand: LastChancesHand
  attacks: Record<LastChancesGesture, LastChancesAttackDefinition>
  tapCombo: LastChancesAttackDefinition[]
  trait?: LastChancesWeaponTrait
  staggerEnabled?: boolean
  resource?: LastChancesWeaponResourceDefinition
  augment: LastChancesAugment
  augmentHooks?: Partial<Record<LastChancesAugment, LastChancesAugmentHookDefinition>>
  tuning?: Record<string, number>
  controls?: LastChancesAttackSetControlDefinition
}

export interface LastChancesEnemyBossPhaseDefinition {
  name: string
  minimumHealthRatio: number
  attackKind: LastChancesEnemyAttackKind
  attackRange: number
  attackRadius: number
  attackDamage: number
  attackCooldownMs: number
  attackWindupMs: number
  projectileSpeed?: number
  projectileKnockback?: number
  leapDistance?: number
  leapDurationMs?: number
  targetLockMs?: number
  parryWindowMs?: number
}

export interface LastChancesEnemyZoneDefinition {
  /** Shape of the telegraphed ground zone is picked randomly per cast. */
  shapes: LastChancesZoneShape[]
  /** Time the player has to leave the zone before it detonates. */
  escapeMs: number
  /** Pure damage (ignores armor) as a fraction of the player's max HP. */
  damageMaxHpRatio: number
  /** Circle radius / half-extent of square and triangle. */
  size: number
}

export interface LastChancesEnemySwarmDefinition {
  /** Total cockroaches the ordinary room event feeds in; they accumulate if not killed. */
  total: number
  /** Cockroaches released immediately when the room starts. */
  initialBurst: number
  spawnIntervalMs: number
}

export interface LastChancesCockroachMotherDefinition {
  /** Remaining-health gates that force a retreat through the linked boss holes. */
  retreatHealthRatios: number[]
  retreatSpeed: number
  /** Distance from the entrance hole, as a fraction of body radius, at which she vanishes. */
  entranceRadiusRatio?: number
  hideMs: number
  /** Attack recovery applied after she emerges from the striking hole. */
  exitRecoveryMs?: number
  /** Visual shockwave reach; damage itself covers the arena outside the opposite safe corner. */
  blastRadius: number
  /** Player-center radius around the diagonally opposite hole that escapes the blast. */
  safeCornerRadius: number
  /** Pure blast damage as a fraction of the player's maximum HP. */
  blastDamageMaxHpRatio: number
}

export interface LastChancesEnemyDefinition {
  id: string
  name: string
  maxHp: number
  radius: number
  moveSpeed: number
  armor?: number
  dodge?: number
  /** Schema-v2 authored idle facing rotation; v1 falls back to the prototype default. */
  idleTurnRadiansPerSecond?: number
  visionRange: number
  visionAngleDegrees: number
  noticeMs: number
  alertPauseMs: number
  attackRange: number
  /** Distance the chaser tries to retain as a fraction of attackRange. */
  preferredAttackRangeRatio?: number
  /** Combat role controls attack queuing: creeps and cockroaches ignore the shared queue. */
  role?: LastChancesEnemyRole
  attackKind?: LastChancesEnemyAttackKind
  attackRadius?: number
  projectileSpeed?: number
  projectileKnockback?: number
  leapDistance?: number
  leapDurationMs?: number
  targetLockMs?: number
  parryWindowMs?: number
  invisibleUntilAlerted?: boolean
  bossPhases?: LastChancesEnemyBossPhaseDefinition[]
  /** Required when attackKind is 'zone': the telegraphed ground zone is the enemy's only damage source. */
  zone?: LastChancesEnemyZoneDefinition
  /** Marks a swarm-event enemy: a rolled plan slot becomes a trickle of `total` cockroaches from two map edges. */
  swarm?: LastChancesEnemySwarmDefinition
  /** Adds the staged linked-hole attack used by the optional Mother of Cockroaches boss. */
  cockroachMother?: LastChancesCockroachMotherDefinition
  attackDamage: number
  attackCooldownMs: number
  attackWindupMs: number
  mentalPressurePerSecond: number
  color: string
  /** Enemy-specific prototype knobs such as Knife-spider capture/self-damage values. */
  tuning?: Record<string, number>
}

export interface LastChancesEnemyPoolEntry {
  enemyId: string
  weight: number
}

export interface LastChancesTierDefinition {
  id: string
  label: string
  kind: 'normal' | 'boss'
  nodeCount: number
  enemyCount: [number, number]
  deathCost: number
  erosion: LastChancesStatErosion
  enemyPool: LastChancesEnemyPoolEntry[]
  /** Each ID is assigned to one route node and reserves one slot inside that room's enemyCount. */
  guaranteedEnemyIds?: string[]
  roomTemplateIds: string[]
  /** Each room ID is assigned to one route node, so a special room exists but remains bypassable. */
  guaranteedRoomTemplateIds?: string[]
  accent: string
}

export interface LastChancesObstacleDefinition {
  x: number
  y: number
  width: number
  height: number
  elevation: number
}

export interface LastChancesHazardDefinition {
  id: string
  name: string
  kind: LastChancesHazardKind
  x: number
  y: number
  width: number
  height: number
  damage: number
  mentalDamagePerSecond: number
  cycleMs: number
  activeMs: number
  phaseOffsetMs: number
  color: string
}

export interface LastChancesInteractionEffect {
  chanceCost?: number
  hp?: number
  mentalHealth?: number
  stats?: Partial<LastChancesStats>
  primaryWeaponId?: string
  secondaryWeaponId?: string | null
  artifactId?: string | null
  outfitId?: string | null
}

export interface LastChancesInteractionChoice {
  id: string
  label: string
  description: string
  effect: LastChancesInteractionEffect
}

export interface LastChancesRoomInteractionDefinition {
  title: string
  body: string
  choices: LastChancesInteractionChoice[]
}

export interface LastChancesFixedEncounterDefinition {
  /** Fixed placed enemies replace the tier's random enemy roll. */
  enemyIds: string[]
  /** Optional swarm event that replaces the tier's random swarm roll. */
  swarmEnemyId?: string
  /** The swarm keeps spawning until the room's boss dies. */
  infiniteSwarm?: boolean
}

export interface LastChancesTurretDefinition {
  id: string
  name: string
  position: LastChancesVector
  facingDegrees: number
  rotationDegreesPerSecond: number
  visionRange: number
  visionAngleDegrees: number
  interactionRange: number
  fireIntervalMs: number
  projectileSpeed: number
  projectileRadius: number
  projectileSpawnOffset?: number
  projectileKnockback?: number
  damage: number
  color: string
}

export interface LastChancesBossHoleDefinition {
  id: string
  shape: LastChancesBossHoleShape
  position: LastChancesVector
  linkedHoleId: string
  color: string
}

export interface LastChancesBossAltarDefinition {
  position: LastChancesVector
  chanceCost: number
  prompt: string
}

export interface LastChancesSpawnLayoutDefinition {
  id: string
  name: string
  enemySpawns: LastChancesVector[]
}

export interface LastChancesRoomTemplate {
  id: string
  name: string
  archetype: LastChancesRoomArchetype
  width: number
  height: number
  playerSpawn: LastChancesVector
  /** Schema-v1 fallback. New rooms author at least two named deterministic layouts. */
  enemySpawns?: LastChancesVector[]
  spawnLayouts?: LastChancesSpawnLayoutDefinition[]
  obstacles: LastChancesObstacleDefinition[]
  hazards?: LastChancesHazardDefinition[]
  interaction?: LastChancesRoomInteractionDefinition
  encounter?: LastChancesFixedEncounterDefinition
  /** Shared alarm memory after any linked turret sees the player. */
  turretAlarmHoldMs?: number
  turrets?: LastChancesTurretDefinition[]
  bossHoles?: LastChancesBossHoleDefinition[]
  altar?: LastChancesBossAltarDefinition
  ouroborosPickup?: LastChancesOuroborosPickupDefinition
}

export interface LastChancesStoryPage {
  speaker?: string
  text: string
}

export interface LastChancesNarrativeDefinition {
  prologue: LastChancesStoryPage[]
  victory: LastChancesStoryPage[]
  exhaustedVictory: LastChancesStoryPage[]
  exhaustedDeathThreshold: number
}

export interface LastChancesMylorikInputDefinition {
  techniqueHoldMs: number
  bufferMs: number
  continuationWindowMs: number
  gamepad: {
    leftBumper: number
    rightBumper: number
    leftTrigger: number
    rightTrigger: number
    mobilityButton: number
    interactButton: number
  }
  keyboard: {
    leftTechniqueKeys: string[]
    rightTechniqueKeys: string[]
    mobilityKeys: string[]
    interactKeys: string[]
    leftStrikeMouseButton: number
    rightStrikeMouseButton: number
  }
}

export interface LastChancesAdaptiveTriggerProfileDefinition {
  startPosition: number
  endPosition: number
  resistance: number
  force: number
  transitionMs: number
  effectMs: number
  magnitude: number
}

/** One motor pulse inside a multi-pulse rumble pattern; delays count from pattern start. */
export interface LastChancesFeedbackPulseDefinition {
  delayMs: number
  durationMs: number
  magnitude: number
  /** Omitted pulses inherit the emitting event's hand. */
  hand?: LastChancesHand | 'both'
}

export interface LastChancesDualSenseInputDefinition {
  activationThreshold: number
  releaseThreshold: number
  hysteresis: number
  /** Default dwell time before the active trigger pocket arms. */
  armMs?: number
  /** Repeat interval for an armed pocket's discrete telegraph pattern. */
  telegraphPeriodMs?: number
  gamepad: {
    leftBumper: number
    rightBumper: number
    leftTrigger: number
    rightTrigger: number
    circle: number
    cross: number
    options: number
  }
  keyboard: LastChancesMylorikInputDefinition['keyboard']
  gatePositions: {
    shallow: number
    medium: number
    deep: number
    final: number
  }
  feedback: {
    maxMagnitude: number
    maxDurationMs: number
    blockedRepeatMs: number
    profiles: Record<LastChancesTactileProfile, LastChancesAdaptiveTriggerProfileDefinition>
  }
}

export interface LastChancesInputDefinition {
  /** DeepList gesture recognizer and its complete legacy bindings. */
  doubleTapMs: number
  /** Basic-tap combo progress resets after this much game time without another tap. */
  tapComboWindowMs?: number
  holdMs: number
  holdMaxMs: number
  holdThenDoubleTapWindowMs: number
  aimDeadZone: number
  /** Minimum movement-stick magnitude used when an action redirects itself from movement. */
  actionDirectionDeadZone: number
  gamepadDeadZone: number
  gamepadLeftButton: number
  gamepadRightButton: number
  leftKeys: string[]
  rightKeys: string[]
  /** Schema-v4 semantic control recognizers. */
  mylorik?: LastChancesMylorikInputDefinition
  dualsense?: LastChancesDualSenseInputDefinition
}

export interface LastChancesCombatDefinition {
  /** Every committed attack removes residual walking velocity before its first frame. */
  attackStopsMovement: boolean
  minimumPlayerParryMs: number
  enemyRevealOnParryMs: number
  enemyRevealOnHitMs: number
  /** Damage remaining after armor can never fall below this value. */
  minimumPlayerDamageTaken: number
}

export interface LastChancesConfig {
  schemaVersion: 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15
  title: string
  seed: string
  chances: number
  graph: {
    choicesPerNode: number
    generationSeedStep: number
  }
  input: LastChancesInputDefinition
  player: {
    radius: number
    invulnerabilityMs: number
    /** Time to accelerate from rest to the current maximum movement speed. */
    accelerationMs: number
    /** Time to brake from the current maximum movement speed to rest after releasing input. */
    decelerationMs: number
    baseStats: LastChancesStats
  }
  mentalHealth: {
    calmRecoveryPerSecond: number
    restoreOnKill: number
    maxPressurePerSecond: number
  }
  combat: LastChancesCombatDefinition
  stamina: LastChancesStaminaDefinition
  progression: {
    roomHpRecovery: number
    roomMentalRecovery: number
    /** Defaults to true for older definitions. False unlocks every move and hides move quests. */
    moveQuestsEnabled?: boolean
    /** Kills with tap/hold required to queue that move quest's next-room unlock. */
    moveQuestKillsRequired: number
    /** Current HP and mind retained when entering an unvisited neighboring room in the same tier. */
    sameTierSacrificeRatio: number
    /** Multiplicative stamina-cost increase granted by every room/time stack in the current attempt. */
    staminaCostIncreasePerRoom: number
    /** Active combat time required to add one extra stamina-cost stack after a cleared room. */
    staminaCostIncreaseIntervalMs: number
    /** Maximum number of combined room/time stamina-cost stacks carried by the current attempt. */
    maxStaminaCostStacks: number
    /** Voluntarily spent Chances required to apply one immediate current-tier stat erosion. */
    chanceErosionStep: number
    tiers: LastChancesTierDefinition[]
  }
  rooms: LastChancesRoomTemplate[]
  enemies: LastChancesEnemyDefinition[]
  weapons: LastChancesWeaponDefinition[]
  artifacts?: LastChancesArtifactDefinition[]
  outfits?: LastChancesOutfitDefinition[]
  ouroborosSet?: LastChancesOuroborosSetDefinition
  /** Optional catalog-backed equipment selection. Omitted schema-v1 definitions retain their legacy hand slots. */
  loadout?: LastChancesLoadoutDefinition
  narrative?: LastChancesNarrativeDefinition
  renderer: {
    maxDpr: number
    snapshotHz: number
    floorGridSize: number
    background: string
    floor: string
    floorGrid: string
    obstacleTop: string
    obstacleSide: string
    player: string
    playerAccent: string
    mental: string
    stamina: string
  }
}

export interface LastChancesPlanEnemy {
  id: string
  definitionId: string
  position: LastChancesVector
}

export interface LastChancesPlanNode {
  id: string
  tierIndex: number
  tierId: string
  tierKind: 'normal' | 'boss'
  label: string
  accent: string
  roomTemplateId: string
  spawnLayoutId: string
  roomName: string
  roomArchetype: LastChancesRoomArchetype
  seed: number
  arena: {
    width: number
    height: number
    playerSpawn: LastChancesVector
    obstacles: LastChancesObstacleDefinition[]
    hazards: LastChancesHazardDefinition[]
  }
  interaction: LastChancesRoomInteractionDefinition | null
  turretAlarmHoldMs: number
  turrets: LastChancesTurretDefinition[]
  bossHoles: LastChancesBossHoleDefinition[]
  altar: LastChancesBossAltarDefinition | null
  ouroborosPickup: LastChancesOuroborosPickupDefinition | null
  enemies: LastChancesPlanEnemy[]
  /** Present when the enemy roll produced a cockroach event; the swarm replaces its rolled slots. */
  swarm: LastChancesPlanSwarm | null
  nextNodeIds: string[]
}

export interface LastChancesPlanSwarm {
  definitionId: string
  /** Two distinct arena edges the cockroaches run in from. */
  edges: [LastChancesArenaEdge, LastChancesArenaEdge]
  infinite?: boolean
}

export interface LastChancesGamePlan {
  generation: number
  seed: string
  tiers: LastChancesPlanNode[][]
  nodes: LastChancesPlanNode[]
}

export interface LastChancesPlayerSnapshot {
  position: LastChancesVector
  aim: LastChancesVector
  hp: number
  mentalHealth: number
  stamina: number
  /** Completed-room and full combat-interval stacks that currently increase stamina expenditure. */
  staminaCostStacks: number
  /** Current multiplier applied to every stamina expenditure. */
  staminaCostMultiplier: number
  stats: LastChancesStats
  invulnerableForMs: number
  armorMultiplier?: number
  armorMultiplierForMs?: number
  /**
   * Elapsed timestamp of the most recent action refused for missing stamina, so the HUD can
   * blink the bar. Null until an action is actually refused.
   */
  staminaRefusedAtMs?: number | null
}

export interface LastChancesEnemySnapshot {
  id: string
  definitionId: string
  name: string
  position: LastChancesVector
  facing: LastChancesVector
  hp: number
  maxHp: number
  state: LastChancesEnemyState
  noticeProgress: number
  attackCooldownMs: number
  role: LastChancesEnemyRole
  attackKind: LastChancesEnemyAttackKind
  attackWindupProgress: number
  parryWindowOpen: boolean
  phaseName: string | null
  visible: boolean
  captureAvailable: boolean
  statuses: Array<{
    status: LastChancesStatusKind
    remainingMs: number
    stacks: number
    magnitude: number
  }>
}

export interface LastChancesProjectileSnapshot {
  id: number
  position: LastChancesVector
  radius: number
  color: string
  source: 'player' | 'enemy'
}

export interface LastChancesHazardSnapshot {
  id: string
  name: string
  kind: LastChancesHazardKind
  active: boolean
}

export interface LastChancesInteractionSnapshot {
  title: string
  body: string
  choices: Array<LastChancesInteractionChoice & {
    available: boolean
  }>
}

export interface LastChancesCooldownSnapshot {
  hand: LastChancesHand
  gesture: LastChancesGesture
  remainingMs: number
  totalMs: number
  /** Authoritative runtime availability after cooldown, recovery, resource and slot checks. */
  ready: boolean
}

export interface LastChancesGestureSnapshot {
  hand: LastChancesHand
  gesture: LastChancesGesture
  attackName: string
  atMs: number
  /** One-based basic-combo step. Special gestures intentionally leave it undefined. */
  comboStep?: number
}

export interface LastChancesGestureInputSnapshot {
  hand: LastChancesHand
  phase: LastChancesGestureInputPhase
  pressed: boolean
  progress: number
  remainingMs: number
  heldMs: number
  sequence: LastChancesGestureSequence | null
  candidateGesture: LastChancesGesture | null
  pendingChargeMs: number
}

export interface LastChancesGestureResolution {
  hand: LastChancesHand
  gesture: LastChancesGesture
  atMs: number
  /** Duration of the press that completed the gesture. */
  heldMs: number
  /** Duration of the first press; differs from heldMs for multi-press gestures. */
  firstHoldMs: number
  /** DualSense-only depth floor; absent for DeepList and mylorik. */
  minChargeBandId?: string
}

export interface LastChancesChargeBandSnapshot {
  id: string
  label: string
  minMs: number
  color: string
  active: boolean
}

export interface LastChancesHandActionCue {
  hand: LastChancesHand
  weaponId: string
  phase: 'idle' | 'candidate' | 'charging' | 'armed' | 'recovery'
  gesture: LastChancesGesture | null
  color: string
  heldMs: number
  chargeProgress: number
  chargeMaxMs: number
  chargeBands: LastChancesChargeBandSnapshot[]
  recoveryMs: number
}

export interface LastChancesWeaponStateSnapshot {
  weaponId: string
  hand: LastChancesHand
  resourceKind: LastChancesWeaponResourceKind | null
  resource: number
  maxResource: number
  resourceLabel: string | null
  resourceColor: string | null
  storedDot: Exclude<LastChancesStatusKind, 'bleed'> | null
  rhythm: 'idle' | 'early' | 'good' | 'late'
  recoveryMs: number
  /** Remaining visual confirmation after a 500–600 ms Sword rhythm hit. */
  perfectTimingMs: number
  /** Remaining fatigue that blocks only the Mercenary Sword's ordinary tap. */
  fatigueMs: number
  /** Time until a held second input resolves Unterhaw. */
  unterhauWindowMs: number
  /** Oberhaw connected, so the otherwise dim Unterhaw menu row should glow. */
  unterhauPrimed: boolean
  /** Mouse-motion damage bonus earned by the current/last assisted basic sweep. */
  motionDamageBonus: number
}

export interface LastChancesMoveQuestSnapshot {
  hand: LastChancesHand
  unlocked: Record<LastChancesGesture, boolean>
  /** Unlocks earned this room; they activate on the next room entry. */
  pendingUnlocks: LastChancesGesture[]
  roomKills: { tap: number; hold: number }
  killsRequired: number
  tapQuestDone: boolean
  holdQuestDone: boolean
  comboQuestAvailable: boolean
  comboQuestDone: boolean
  /** Best progress among living elites: gestures this hand already landed on that elite. */
  comboGesturesHit: LastChancesGesture[]
  comboGesturesRequired: LastChancesGesture[]
}

export interface LastChancesSwarmSnapshot {
  definitionId: string
  /** Cockroaches still queued outside the map; null means the boss-fed stream is infinite. */
  infinite: boolean
  remaining: number
  total: number
}

export interface LastChancesTurretSnapshot {
  id: string
  name: string
  position: LastChancesVector
  facing: LastChancesVector
  disabled: boolean
  seesPlayer: boolean
}

export interface LastChancesAltarPromptSnapshot {
  prompt: string
  chanceCost: number
  available: boolean
}

export interface LastChancesGroundWeaponSnapshot {
  id: string
  weaponId: string
  name: string
  position: LastChancesVector
}

export interface LastChancesGroundOuroborosSnapshot {
  id: string
  items: LastChancesOuroborosItem[]
  position: LastChancesVector
  chanceCost: number
  affordable: boolean
}

export interface LastChancesOuroborosSnapshot {
  equipped: Record<LastChancesOuroborosItem, boolean>
  discovered: Record<LastChancesOuroborosItem, boolean>
  fangKillStacks: number
  damageBonusPercent: number
  acidChancesSpent: number
  lifestealPercent: number
  roomScaleStacks: number
  damageReductionPercent: number
  fullSet: boolean
}

export interface LastChancesGamepadSnapshot {
  supported: boolean
  connected: boolean
  status: LastChancesGamepadStatus
  activeIndex: number | null
  connectedCount: number
  id: string | null
  mapping: string | null
  profile: LastChancesGamepadProfile | null
}

export interface LastChancesSemanticControlCue {
  hand: LastChancesHand | null
  intent: LastChancesControlIntent | null
  state: LastChancesFeedbackState
  gesture: LastChancesGesture | null
  label: string
  tactileProfile: LastChancesTactileProfile | null
  atMs: number
}

export interface LastChancesControlRoleSnapshot {
  hand: LastChancesHand
  instantMove: string
  techniqueOrTrigger: string
  nextGate: string | null
}

export interface LastChancesFeedbackSnapshot {
  tier: LastChancesFeedbackTier
  status: 'controls-only' | 'vibration' | 'enhanced' | 'unavailable' | 'error'
  mode: LastChancesFeedbackMode
  intensity: number
  reducedHaptics: boolean
  permission: 'not-requested' | 'granted' | 'denied' | 'unavailable'
  message: string | null
}

export interface LastChancesKnifeSpiderTutorialSnapshot {
  phase: 'slowing' | 'frozen' | 'resuming'
  timeScale: number
  /** Human-readable binding for the forced Mercenary Sword left tap. */
  parryBinding: string
}

/** One line in the run feed. `atMs` is engine time, used to fade older lines out. */
export interface LastChancesEventLogEntry {
  id: string
  text: string
  atMs: number
}

export interface LastChancesSnapshot {
  phase: LastChancesPhase
  paused: boolean
  generation: number
  chances: number
  totalDeaths: number
  elapsedMs: number
  currentNodeId: string | null
  currentTierIndex: number | null
  attemptPath: string[]
  availableNodeIds: string[]
  /** Available same-tier nodes whose entry consumes the configured body/mind sacrifice. */
  sacrificeNodeIds: string[]
  deathReason: string | null
  player: LastChancesPlayerSnapshot
  enemies: LastChancesEnemySnapshot[]
  projectiles: LastChancesProjectileSnapshot[]
  hazards: LastChancesHazardSnapshot[]
  interaction: LastChancesInteractionSnapshot | null
  loadout: LastChancesLoadoutDefinition | null
  cooldowns: LastChancesCooldownSnapshot[]
  lastGesture: LastChancesGestureSnapshot | null
  gestureInputs: LastChancesGestureInputSnapshot[]
  actionCues: LastChancesHandActionCue[]
  weaponStates: LastChancesWeaponStateSnapshot[]
  moveQuests: LastChancesMoveQuestSnapshot[]
  groundWeapons: LastChancesGroundWeaponSnapshot[]
  groundOuroboros: LastChancesGroundOuroborosSnapshot[]
  ouroboros: LastChancesOuroborosSnapshot | null
  events: LastChancesEventLogEntry[]
  swarm: LastChancesSwarmSnapshot | null
  turrets: LastChancesTurretSnapshot[]
  turretAlarm: boolean
  altarPrompt: LastChancesAltarPromptSnapshot | null
  cockroachesExtinct: boolean
  interactionPrompt: string | null
  knifeSpiderTutorial: LastChancesKnifeSpiderTutorialSnapshot | null
  controlScheme: LastChancesControlScheme
  controlCue: LastChancesSemanticControlCue | null
  controlRoles: LastChancesControlRoleSnapshot[]
  feedback: LastChancesFeedbackSnapshot
  gamepad: LastChancesGamepadSnapshot
  selectedNodeId: string | null
  selectedInteractionChoiceId: string | null
}

export interface LastChancesEngineCallbacks {
  onSnapshot?: (snapshot: LastChancesSnapshot) => void
  onPlan?: (plan: LastChancesGamePlan) => void
  /** Page-owned story/overlay bridge for controller-only confirmation and back. */
  onUiCommand?: (command: 'confirm' | 'back' | 'pause') => boolean
}

export interface LastChancesConfigValidation {
  valid: boolean
  errors: string[]
}

export interface LoadLastChancesConfigOptions {
  url?: string
  signal?: AbortSignal
  useBrowserOverride?: boolean
}
