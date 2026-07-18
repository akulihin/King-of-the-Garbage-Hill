import type {
  LastChancesConfig,
  LastChancesEquipMode,
  LastChancesHand,
  LastChancesAugment,
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
  augment?: LastChancesAugment,
): LastChancesResolvedWeapon {
  const attacks = useSecondaryAttacks && weapon.secondaryAttacks
    ? weapon.secondaryAttacks
    : weapon.attacks
  const authoredTapCombo = useSecondaryAttacks
    ? weapon.secondaryTapCombo
    : weapon.tapCombo
  const controls = useSecondaryAttacks
    ? weapon.controls?.secondary
    : weapon.controls?.primary
  return {
    id: weapon.id,
    name: weapon.name,
    hand,
    attacks,
    tapCombo: [attacks.tap, ...(authoredTapCombo ?? [])],
    ...(weapon.trait ? { trait: weapon.trait } : {}),
    ...(weapon.staggerEnabled === undefined ? {} : { staggerEnabled: weapon.staggerEnabled }),
    ...(weapon.resource ? { resource: { ...weapon.resource } } : {}),
    augment: augment ?? weapon.defaultAugment ?? 'none',
    ...(weapon.augmentHooks
      ? { augmentHooks: JSON.parse(JSON.stringify(weapon.augmentHooks)) as typeof weapon.augmentHooks }
      : {}),
    ...(weapon.tuning ? { tuning: { ...weapon.tuning } } : {}),
    ...(controls
      ? { controls: JSON.parse(JSON.stringify(controls)) as typeof controls }
      : {}),
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
  const primaryId = config.loadout.primaryWeaponId
  const primary = primaryId ? catalog.get(primaryId) : undefined
  const supplementalId = config.loadout.secondaryWeaponId
  const supplemental = supplementalId ? catalog.get(supplementalId) : undefined
  if (!primary) {
    return {
      left: null,
      right: supplemental
        ? resolvedWeapon(supplemental, 'right', false, config.loadout.secondaryAugment)
        : null,
    }
  }

  const primaryMode = lastChancesEquipMode(primary)
  const left = resolvedWeapon(primary, 'left', false, config.loadout.primaryAugment)
  if (primaryMode === 'twoHanded') {
    return {
      left,
      right: resolvedWeapon(
        primary,
        'right',
        true,
        // Both inputs are still the same physical weapon. A stale secondary-slot
        // augment must not silently alter its second input while that slot is empty.
        config.loadout.primaryAugment,
      ),
    }
  }

  return {
    left,
    right: supplemental
      ? resolvedWeapon(supplemental, 'right', false, config.loadout.secondaryAugment)
      : null,
  }
}
