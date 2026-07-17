import type {
  CombatArmorProfile,
  CombatCounterMatch,
  CombatCounterResult,
  CombatCounterRule,
  CombatDamageBreakdown,
  CombatDamageRules,
  CombatDamageSelectionReason,
  CombatDamageTypeId,
  CombatWeaponProfile,
} from './types'

interface CombatDamageSelection {
  damageTypeId: CombatDamageTypeId
  reason: CombatDamageSelectionReason
}

function profileDamageTypeIds(
  weapon: CombatWeaponProfile,
  rules: CombatDamageRules,
): CombatDamageTypeId[] {
  return rules.damageTypes
    .map(definition => definition.id)
    .filter(damageTypeId => weapon.damageLevels[damageTypeId] !== undefined)
}

function damageLevel(weapon: CombatWeaponProfile, damageTypeId: CombatDamageTypeId): number {
  return weapon.damageLevels[damageTypeId] ?? 0
}

function selectionRank(
  damageTypeId: CombatDamageTypeId,
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile,
  rules: CombatDamageRules,
): number {
  const matches = counterMatches(weapon, armor, [damageTypeId], rules)
  if (matches.some(match => match.counteredSide === 'armor')) return 2
  if (matches.some(match => match.counteredSide === 'weapon')) return 0
  return 1
}

function selectDamageType(
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile | null,
  rules: CombatDamageRules,
): CombatDamageSelection {
  const availableTypeIds = profileDamageTypeIds(weapon, rules)
  if (availableTypeIds.length === 0) {
    throw new Error('Combat weapon profile has no configured damage type.')
  }

  if (!armor) {
    const unarmoredPriority = rules.damageTypes.find(definition =>
      definition.autoPriority === 'unarmored'
      && weapon.damageLevels[definition.id] !== undefined)
    if (unarmoredPriority) {
      return { damageTypeId: unarmoredPriority.id, reason: 'unarmoredPriority' }
    }
  }
  else {
    const armorOvermatch = rules.damageTypes.find(definition =>
      definition.autoPriority === 'armorOvermatch'
      && weapon.damageLevels[definition.id] !== undefined
      && damageLevel(weapon, definition.id) > armor.level)
    if (armorOvermatch) {
      return { damageTypeId: armorOvermatch.id, reason: 'armorOvermatchPriority' }
    }
  }

  let chosenTypeId = availableTypeIds[0]
  for (const candidateTypeId of availableTypeIds.slice(1)) {
    const candidateRank = armor
      ? selectionRank(candidateTypeId, weapon, armor, rules)
      : 1
    const chosenRank = armor
      ? selectionRank(chosenTypeId, weapon, armor, rules)
      : 1
    if (
      candidateRank > chosenRank
      || (
        candidateRank === chosenRank
        && damageLevel(weapon, candidateTypeId) > damageLevel(weapon, chosenTypeId)
      )
    ) {
      chosenTypeId = candidateTypeId
    }
  }

  return { damageTypeId: chosenTypeId, reason: 'bestApplicableType' }
}

export function autoSelectDamageType(
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile | null,
  rules: CombatDamageRules,
): CombatDamageTypeId {
  return selectDamageType(weapon, armor, rules).damageTypeId
}

function matchedDamageTypeIds(
  weapon: CombatWeaponProfile,
  selectedDamageTypeId: CombatDamageTypeId,
  rules: CombatDamageRules,
): CombatDamageTypeId[] {
  return weapon.mixed || weapon.twoTyped
    ? profileDamageTypeIds(weapon, rules)
    : [selectedDamageTypeId]
}

function counterMatch(
  rule: CombatCounterRule,
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile,
  damageTypeIds: readonly CombatDamageTypeId[],
  ignoresArmorCounter: boolean,
): CombatCounterMatch | null {
  if (rule.kind === 'damageTypeCountersArmor') {
    return rule.armorClassId === armor.classId && damageTypeIds.includes(rule.damageTypeId)
      ? { ruleId: rule.id, ruleKind: rule.kind, counteredSide: 'armor', blocksDamage: false }
      : null
  }
  if (rule.kind === 'armorCountersDamageType') {
    return !weapon.mixed
      && !ignoresArmorCounter
      && rule.armorClassId === armor.classId
      && damageTypeIds.includes(rule.damageTypeId)
      ? { ruleId: rule.id, ruleKind: rule.kind, counteredSide: 'weapon', blocksDamage: false }
      : null
  }
  if (rule.kind === 'damageTypeOvermatchesArmor') {
    return damageTypeIds.includes(rule.damageTypeId)
      && damageLevel(weapon, rule.damageTypeId) > armor.level
      ? { ruleId: rule.id, ruleKind: rule.kind, counteredSide: 'armor', blocksDamage: false }
      : null
  }
  if (rule.kind === 'weaponTagCountersArmor') {
    return rule.armorClassId === armor.classId && weapon.tags.includes(rule.weaponTag)
      ? { ruleId: rule.id, ruleKind: rule.kind, counteredSide: 'armor', blocksDamage: false }
      : null
  }
  if (rule.kind === 'weaponTagCountersAllArmor') {
    return weapon.tags.includes(rule.weaponTag)
      ? { ruleId: rule.id, ruleKind: rule.kind, counteredSide: 'armor', blocksDamage: false }
      : null
  }
  if (rule.kind === 'weaponTagIgnoresArmorCounter') return null
  return !weapon.mixed
    && !ignoresArmorCounter
    && rule.armorClassId === armor.classId
    && weapon.tags.includes(rule.weaponTag)
    ? { ruleId: rule.id, ruleKind: rule.kind, counteredSide: 'weapon', blocksDamage: true }
    : null
}

function counterMatches(
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile,
  damageTypeIds: readonly CombatDamageTypeId[],
  rules: CombatDamageRules,
): CombatCounterMatch[] {
  const ignoresArmorCounter = rules.counterRules.some(rule =>
    rule.kind === 'weaponTagIgnoresArmorCounter'
    && rule.armorClassId === armor.classId
    && weapon.tags.includes(rule.weaponTag))
  return rules.counterRules.flatMap((rule) => {
    const match = counterMatch(rule, weapon, armor, damageTypeIds, ignoresArmorCounter)
    return match ? [match] : []
  })
}

export function isCountered(
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile | null,
  selectedDamageTypeId: CombatDamageTypeId,
  rules: CombatDamageRules,
): CombatCounterResult {
  if (!armor) {
    return {
      isCountered: false,
      matches: [],
      weaponPassivesDisabled: false,
      armorPassivesDisabled: false,
      damageBlocked: false,
    }
  }

  const damageTypeIds = matchedDamageTypeIds(weapon, selectedDamageTypeId, rules)
  const matches = counterMatches(weapon, armor, damageTypeIds, rules)
  const weaponPassivesDisabled = matches.some(match => match.counteredSide === 'weapon')
  const armorPassivesDisabled = matches.some(match => match.counteredSide === 'armor')

  return {
    isCountered: matches.length > 0,
    matches,
    weaponPassivesDisabled,
    armorPassivesDisabled,
    damageBlocked: matches.some(match => match.blocksDamage),
  }
}

export function resolveDamage(
  weapon: CombatWeaponProfile,
  armor: CombatArmorProfile | null,
  rules: CombatDamageRules,
): CombatDamageBreakdown {
  const selection = selectDamageType(weapon, armor, rules)
  const rawDamage = damageLevel(weapon, selection.damageTypeId)
  const counter = isCountered(weapon, armor, selection.damageTypeId, rules)

  return {
    chosenType: selection.damageTypeId,
    selectionReason: selection.reason,
    rawDamage,
    armor: armor
      ? { classId: armor.classId, level: armor.level, tags: armor.tags ? [...armor.tags] : undefined }
      : null,
    counterRules: counter.matches,
    passivesDisabled: counter.weaponPassivesDisabled || counter.armorPassivesDisabled,
    weaponPassivesDisabled: counter.weaponPassivesDisabled,
    armorPassivesDisabled: counter.armorPassivesDisabled,
    finalDamage: counter.damageBlocked ? 0 : rawDamage,
  }
}
