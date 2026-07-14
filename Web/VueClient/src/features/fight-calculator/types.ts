export const DAMAGE_TYPES = ['Ударное', 'Дробящее', 'Рубящее', 'Режущее', 'Колющее'] as const

export type DamageType = typeof DAMAGE_TYPES[number]
export type DamageValues = Record<DamageType, number>
export type ArmorSlot = 'helmet' | 'mail' | 'padding' | 'plate'
export type TeamId = 1 | 2

export interface WeaponDefinition {
  id: string
  name: string
  category: string
  attacks: DamageValues
  defense: number
  disarm: number
  antiShield: number
  speed: number
  rangeMin: number
  rangeMax: number
  handsMin: number
  handsMax: number
  durability: number
  fatigue: number
  isCustom?: boolean
}

export interface ArmorDefinition {
  id: string
  name: string
  category: string
  slot: ArmorSlot
  resists: DamageValues
  hp: number
  weight: number
  ergonomics: number
  heaviness: number
  isCustom?: boolean
}

export interface TalentDefinition {
  id: string
  name: string
  strength: number
  hp: number
  speed: number
  isCustom?: boolean
}

export interface UnitConfig {
  id: string
  enabled: boolean
  name: string
  primaryWeaponId: string
  secondaryWeaponId: string
  daggerWeaponId: string
  helmetId: string
  mailId: string
  paddingId: string
  plateId: string
  talentIds: string[]
  mastery: boolean
  baseHp: number
  baseMoveSpeed: number
}

export interface FightBalance {
  baseDamage: number
  initialFatigue: number
  armorFatigueFactor: number
  heavinessSpeedPenalty: number
  minMoveSpeed: number
  postDamageDelayMultiplier: number
  stunSeconds: number
  crushKnockoutChance: number
  disarmChance: number
  bleedDamage: number
  bleedIntervalSeconds: number
  durabilityLossMin: number
  durabilityLossMax: number
  maxCollisionSeconds: number
  maxCollisionEvents: number
}

export interface CalculatorProfile {
  id: string
  name: string
  team1: UnitConfig[]
  team2: UnitConfig[]
  balance: FightBalance
  weapons: WeaponDefinition[]
  armors: ArmorDefinition[]
  talents: TalentDefinition[]
}

export type CollisionStepKind =
  | 'movement'
  | 'attack'
  | 'block'
  | 'damage'
  | 'effect'
  | 'weapon'
  | 'bleed'
  | 'result'

export interface CollisionStepSnapshot {
  actorHp: number
  targetHp: number
  actorFatigue: number
  targetFatigue: number
  actorDurability: number | null
  targetDurability: number | null
  actorResists: DamageValues
  targetResists: DamageValues
}

export interface CollisionStep {
  index: number
  time: number
  kind: CollisionStepKind
  actorName: string
  targetName: string
  weaponName?: string
  technique?: DamageType
  penetration?: number
  resistance?: number
  damage?: number
  message: string
  snapshot: CollisionStepSnapshot
}

export interface CollisionSummary {
  id: string
  order: number
  phase: 'mirror' | 'fallback' | 'survivors'
  team1UnitId: string
  team2UnitId: string
  team1Name: string
  team2Name: string
  winnerTeam: TeamId | null
  winnerUnitId: string | null
  winnerName: string | null
  duration: number
  steps: CollisionStep[]
}

export interface BattleSurvivor {
  unitId: string
  team: TeamId
  name: string
  hp: number
  maxHp: number
  fatigue: number
  weaponName: string | null
}

export interface BattleResult {
  seed: number
  winnerTeam: TeamId | null
  survivors: BattleSurvivor[]
  collisions: CollisionSummary[]
  message: string
}
