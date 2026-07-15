export const LAST_CHANCES_HANDS = ['left', 'right'] as const
export const LAST_CHANCES_GESTURES = [
  'tap',
  'doubleTap',
  'doubleTapHold',
  'hold',
  'holdThenDoubleTap',
] as const
export const LAST_CHANCES_ATTACK_KINDS = ['melee', 'projectile', 'dash', 'burst'] as const

export type LastChancesHand = typeof LAST_CHANCES_HANDS[number]
export type LastChancesGesture = typeof LAST_CHANCES_GESTURES[number]
export type LastChancesAttackKind = typeof LAST_CHANCES_ATTACK_KINDS[number]
export type LastChancesPhase = 'planning' | 'playing' | 'dead' | 'won' | 'outOfChances'
export type LastChancesEnemyState = 'idle' | 'noticing' | 'alerted' | 'chasing' | 'attacking' | 'dead'
export type LastChancesRoomArchetype = 'combat' | 'chest' | 'rest' | 'event'

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
  projectileSpeed: number
  pierce: number
  knockback: number
  color: string
}

export interface LastChancesWeaponDefinition {
  id: string
  name: string
  hand: LastChancesHand
  attacks: Record<LastChancesGesture, LastChancesAttackDefinition>
}

export interface LastChancesEnemyDefinition {
  id: string
  name: string
  maxHp: number
  radius: number
  moveSpeed: number
  visionRange: number
  visionAngleDegrees: number
  noticeMs: number
  alertPauseMs: number
  attackRange: number
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

export interface LastChancesRoomTemplate {
  id: string
  name: string
  archetype: LastChancesRoomArchetype
  width: number
  height: number
  playerSpawn: LastChancesVector
  enemySpawns: LastChancesVector[]
  obstacles: LastChancesObstacleDefinition[]
}

export interface LastChancesConfig {
  schemaVersion: 1
  title: string
  seed: string
  chances: number
  graph: {
    choicesPerNode: number
    generationSeedStep: number
  }
  input: {
    doubleTapMs: number
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
  roomName: string
  roomArchetype: LastChancesRoomArchetype
  seed: number
  arena: {
    width: number
    height: number
    playerSpawn: LastChancesVector
    obstacles: LastChancesObstacleDefinition[]
  }
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
}

export interface LastChancesProjectileSnapshot {
  id: number
  position: LastChancesVector
  radius: number
  color: string
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
}

export interface LastChancesSnapshot {
  phase: LastChancesPhase
  paused: boolean
  generation: number
  chances: number
  elapsedMs: number
  currentNodeId: string | null
  currentTierIndex: number | null
  attemptPath: string[]
  availableNodeIds: string[]
  deathReason: string | null
  player: LastChancesPlayerSnapshot
  enemies: LastChancesEnemySnapshot[]
  projectiles: LastChancesProjectileSnapshot[]
  cooldowns: LastChancesCooldownSnapshot[]
  lastGesture: LastChancesGestureSnapshot | null
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
