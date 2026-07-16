import type {
  LastChancesConfig,
  LastChancesEquipMode,
  LastChancesHand,
  LastChancesResolvedWeapon,
  LastChancesWeaponDefinition,
} from './types'

export interface LastChancesResolvedLoadout {
  left: LastChancesResolvedWeapon | null
  right: LastChancesResolvedWeapon | null
}

export function lastChancesEquipMode(weapon: LastChancesWeaponDefinition): LastChancesEquipMode {
  if (weapon.equipMode) return weapon.equipMode
  return weapon.hand === 'right' ? 'secondaryOnly' : 'primaryOnly'
}

function resolvedWeapon(
  weapon: LastChancesWeaponDefinition,
  hand: LastChancesHand,
  useSecondaryAttacks = false,
): LastChancesResolvedWeapon {
  const attacks = useSecondaryAttacks && weapon.secondaryAttacks
    ? weapon.secondaryAttacks
    : weapon.attacks
  const authoredTapCombo = useSecondaryAttacks
    ? weapon.secondaryTapCombo
    : weapon.tapCombo
  return {
    id: weapon.id,
    name: weapon.name,
    hand,
    attacks,
    tapCombo: [attacks.tap, ...(authoredTapCombo ?? [])],
  }
}

/**
 * Resolves the two active input sets. Definitions without a loadout retain the original
 * schema-v1 behavior, so saved browser overrides continue to work unchanged.
 */
export function resolveLastChancesLoadout(config: LastChancesConfig): LastChancesResolvedLoadout {
  if (!config.loadout) {
    const left = config.weapons.find(weapon => weapon.hand === 'left')
    const right = config.weapons.find(weapon => weapon.hand === 'right')
    return {
      left: left ? resolvedWeapon(left, 'left') : null,
      right: right ? resolvedWeapon(right, 'right') : null,
    }
  }

  const catalog = new Map(config.weapons.map(weapon => [weapon.id, weapon]))
  const primary = catalog.get(config.loadout.primaryWeaponId)
  const supplementalId = config.loadout.secondaryWeaponId
  const supplemental = supplementalId ? catalog.get(supplementalId) : undefined
  if (!primary) return { left: null, right: null }

  const primaryMode = lastChancesEquipMode(primary)
  const left = resolvedWeapon(primary, 'left')
  if (primaryMode === 'twoHanded' || (primaryMode === 'hybrid' && !supplemental)) {
    return {
      left,
      right: resolvedWeapon(primary, 'right', true),
    }
  }

  return {
    left,
    right: supplemental ? resolvedWeapon(supplemental, 'right') : null,
  }
}
