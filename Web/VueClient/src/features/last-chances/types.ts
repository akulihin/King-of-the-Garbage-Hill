export const LAST_CHANCES_HANDS = ['left', 'right'] as const
export const LAST_CHANCES_GESTURES = [
  'tap',
  'doubleTap',
  'doubleTapHold',
  'hold',
  'holdThenDoubleTap',
] as const
export const LAST_CHANCES_ATTACK_KINDS = ['melee', 'projectile', 'dash', 'burst'] as const
export const LAST_CHANCES_ENEMY_ROLES = ['creep', 'standard', 'elite', 'boss'] as const
export const LAST_CHANCES_ENEMY_ATTACK_KINDS = ['melee', 'leap', 'projectile', 'heavy'] as const
export const LAST_CHANCES_HAZARD_KINDS = ['spikes', 'mentalFog'] as const
export const LAST_CHANCES_EQUIP_MODES = [
  'twoHanded',
  'eitherHand',
  'primaryOnly',
  'secondaryOnly',
  'hybrid',
] as const

export type LastChancesHand = typeof LAST_CHANCES_HANDS[number]
export type LastChancesGesture = typeof LAST_CHANCES_GESTURES[number]
export type LastChancesAttackKind = typeof LAST_CHANCES_ATTACK_KINDS[number]
export type LastChancesEnemyRole = typeof LAST_CHANCES_ENEMY_ROLES[number]
export type LastChancesEnemyAttackKind = typeof LAST_CHANCES_ENEMY_ATTACK_KINDS[number]
export type LastChancesHazardKind = typeof LAST_CHANCES_HAZARD_KINDS[number]
export type LastChancesEquipMode = typeof LAST_CHANCES_EQUIP_MODES[number]
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
export type LastChancesGamepadProfile = 'standard' | 'sony-raw' | 'generic'
export type LastChancesGamepadStatus = 'unsupported' | 'disconnected' | 'idle' | 'active'

export interface LastChancesVector {
  x: number
  y: number
}

export interface LastChancesStats {
  maxHp: number
  maxMentalHealth: number
  attackPower: number
  moveSpeed: number
  armor: number
}

export interface LastChancesStatErosion {
  maxHp: number
  maxMentalHealth: number
  attackPower: number
  moveSpeed: number
  armor: number
}

export interface LastChancesAttackDefinition {
  name: string
  kind: LastChancesAttackKind
  damage: number
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
  attacks: Record<LastChancesGesture, LastChancesAttackDefinition>
  /** Follow-up basic strikes after attacks.tap, advanced cyclically inside the combo window. */
  tapCombo?: LastChancesAttackDefinition[]
  /** The second input set used while a two-handed or unsupplemented hybrid weapon occupies both hands. */
  secondaryAttacks?: Record<LastChancesGesture, LastChancesAttackDefinition>
  /** Follow-ups after secondaryAttacks.tap. */
  secondaryTapCombo?: LastChancesAttackDefinition[]
}

export interface LastChancesLoadoutDefinition {
  primaryWeaponId: string
  /** Null leaves an ordinary off-hand empty; two-handed and unsupplemented hybrid weapons fill it themselves. */
  secondaryWeaponId: string | null
}

export interface LastChancesResolvedWeapon {
  id: string
  name: string
  hand: LastChancesHand
  attacks: Record<LastChancesGesture, LastChancesAttackDefinition>
  tapCombo: LastChancesAttackDefinition[]
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
  leapDistance?: number
  leapDurationMs?: number
  targetLockMs?: number
  parryWindowMs?: number
}

export interface LastChancesEnemyDefinition {
  id: string
  name: string
  maxHp: number
  radius: number
  moveSpeed: number
  /** Schema-v2 authored idle facing rotation; v1 falls back to the prototype default. */
  idleTurnRadiansPerSecond?: number
  visionRange: number
  visionAngleDegrees: number
  noticeMs: number
  alertPauseMs: number
  attackRange: number
  /** Distance the chaser tries to retain as a fraction of attackRange. */
  preferredAttackRangeRatio?: number
  /** Combat role controls attack queuing: creeps ignore the shared queue. */
  role?: LastChancesEnemyRole
  attackKind?: LastChancesEnemyAttackKind
  attackRadius?: number
  projectileSpeed?: number
  leapDistance?: number
  leapDurationMs?: number
  targetLockMs?: number
  parryWindowMs?: number
  invisibleUntilAlerted?: boolean
  bossPhases?: LastChancesEnemyBossPhaseDefinition[]
  attackDamage: number
  attackCooldownMs: number
  attackWindupMs: number
  mentalPressurePerSecond: number
  color: string
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
  roomTemplateIds: string[]
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

export interface LastChancesConfig {
  schemaVersion: 1 | 2
  title: string
  seed: string
  chances: number
  graph: {
    choicesPerNode: number
    generationSeedStep: number
  }
  input: {
    doubleTapMs: number
    /** Basic-tap combo progress resets after this much game time without another tap. */
    tapComboWindowMs?: number
    holdMs: number
    holdMaxMs: number
    holdThenDoubleTapWindowMs: number
    aimDeadZone: number
    gamepadDeadZone: number
    gamepadLeftButton: number
    gamepadRightButton: number
    leftKeys: string[]
    rightKeys: string[]
  }
  player: {
    radius: number
    invulnerabilityMs: number
    baseStats: LastChancesStats
  }
  mentalHealth: {
    calmRecoveryPerSecond: number
    restoreOnKill: number
    maxPressurePerSecond: number
  }
  progression: {
    roomHpRecovery: number
    roomMentalRecovery: number
    tiers: LastChancesTierDefinition[]
  }
  rooms: LastChancesRoomTemplate[]
  enemies: LastChancesEnemyDefinition[]
  weapons: LastChancesWeaponDefinition[]
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
  enemies: LastChancesPlanEnemy[]
  nextNodeIds: string[]
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
  stats: LastChancesStats
  invulnerableForMs: number
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
  gamepad: LastChancesGamepadSnapshot
  selectedNodeId: string | null
}

export interface LastChancesEngineCallbacks {
  onSnapshot?: (snapshot: LastChancesSnapshot) => void
  onPlan?: (plan: LastChancesGamePlan) => void
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
