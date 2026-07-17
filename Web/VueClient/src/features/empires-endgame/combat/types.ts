export type CombatDamageTypeId = string
export type CombatArmorClassId = string

export type CombatDamageAutoPriority = 'unarmored' | 'armorOvermatch'

export interface CombatDamageTypeDefinition {
  id: CombatDamageTypeId
  name: string
  autoPriority?: CombatDamageAutoPriority
}

export interface CombatArmorClassDefinition {
  id: CombatArmorClassId
  name: string
  tags?: string[]
}

export interface CombatWeaponProfile {
  damageLevels: Partial<Record<CombatDamageTypeId, number>>
  tags: string[]
  mixed?: boolean
  twoTyped?: boolean
  passiveIds?: string[]
}

export interface CombatArmorProfile {
  classId: CombatArmorClassId
  level: number
  tags?: string[]
}

export interface CombatEquipmentDefinition {
  id: string
  name: string
  kind: 'weapon' | 'armor' | 'shield'
  profile: CombatWeaponProfile | CombatArmorProfile
  technologyId?: string
  deferredReason?: string
}

interface CombatCounterRuleBase {
  id: string
}

export type CombatCounterRule =
  | CombatCounterRuleBase & {
    kind: 'damageTypeCountersArmor'
    damageTypeId: CombatDamageTypeId
    armorClassId: CombatArmorClassId
  }
  | CombatCounterRuleBase & {
    kind: 'armorCountersDamageType'
    armorClassId: CombatArmorClassId
    damageTypeId: CombatDamageTypeId
  }
  | CombatCounterRuleBase & {
    kind: 'damageTypeOvermatchesArmor'
    damageTypeId: CombatDamageTypeId
  }
  | CombatCounterRuleBase & {
    kind: 'weaponTagCountersArmor'
    weaponTag: string
    armorClassId: CombatArmorClassId
  }
  | CombatCounterRuleBase & {
    kind: 'weaponTagCountersAllArmor'
    weaponTag: string
  }
  | CombatCounterRuleBase & {
    kind: 'armorBlocksWeaponTag'
    armorClassId: CombatArmorClassId
    weaponTag: string
  }
  | CombatCounterRuleBase & {
    kind: 'weaponTagIgnoresArmorCounter'
    weaponTag: string
    armorClassId: CombatArmorClassId
  }

export interface EmpiresCombatConfig {
  enabled: boolean
  damageTypes: CombatDamageTypeDefinition[]
  armorClasses: CombatArmorClassDefinition[]
  counterRules: CombatCounterRule[]
  equipment: CombatEquipmentDefinition[]
}

export type CombatDamageRules = Pick<EmpiresCombatConfig, 'damageTypes' | 'counterRules'>

export type CombatDamageSelectionReason =
  | 'unarmoredPriority'
  | 'armorOvermatchPriority'
  | 'bestApplicableType'

export interface CombatCounterMatch {
  ruleId: string
  ruleKind: CombatCounterRule['kind']
  counteredSide: 'weapon' | 'armor'
  blocksDamage: boolean
}

export interface CombatCounterResult {
  isCountered: boolean
  matches: CombatCounterMatch[]
  weaponPassivesDisabled: boolean
  armorPassivesDisabled: boolean
  damageBlocked: boolean
}

export interface CombatDamageBreakdown {
  chosenType: CombatDamageTypeId
  selectionReason: CombatDamageSelectionReason
  rawDamage: number
  armor: CombatArmorProfile | null
  counterRules: CombatCounterMatch[]
  passivesDisabled: boolean
  weaponPassivesDisabled: boolean
  armorPassivesDisabled: boolean
  finalDamage: number
}
