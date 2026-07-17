import type {
  CombatArmorProfile,
  CombatEquipmentDefinition,
  CombatWeaponProfile,
} from './combat/types'
import type {
  EmpiresUnitDefinition,
  EmpiresUnitLoadoutDefinition,
} from './types'
import type { TdEquipmentCost } from './td/types'

export interface ResolvedEmpiresUnitLoadout {
  id: string
  weaponEquipmentId?: string
  defenseEquipmentId?: string
  weapon: CombatWeaponProfile | null
  armor: CombatArmorProfile | null
  equipmentCosts: TdEquipmentCost[]
}

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function stableCompare(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0
}

function aggregateCosts(costs: readonly TdEquipmentCost[]): TdEquipmentCost[] {
  const totals = new Map<string, number>()
  for (const cost of costs) totals.set(cost.equipmentId, (totals.get(cost.equipmentId) ?? 0) + cost.amount)
  return [...totals]
    .sort(([left], [right]) => stableCompare(left, right))
    .map(([equipmentId, amount]) => ({ equipmentId, amount }))
}

function equipmentAvailable(
  definition: CombatEquipmentDefinition | undefined,
  researchedTechnologyIds: ReadonlySet<string>,
): definition is CombatEquipmentDefinition {
  return Boolean(
    definition
    && !definition.deferredReason
    && (!definition.technologyId || researchedTechnologyIds.has(definition.technologyId)),
  )
}

function loadoutCanEquip(
  baseCosts: readonly TdEquipmentCost[],
  loadout: EmpiresUnitLoadoutDefinition,
  equipment: ReadonlyMap<string, CombatEquipmentDefinition>,
  researchedTechnologyIds: ReadonlySet<string>,
  stock: Readonly<Record<string, number>>,
  count: number,
): boolean {
  const weapon = equipment.get(loadout.weaponEquipmentId)
  const defense = loadout.defenseEquipmentId
    ? equipment.get(loadout.defenseEquipmentId)
    : undefined
  if (!equipmentAvailable(weapon, researchedTechnologyIds) || weapon.kind !== 'weapon') return false
  if (loadout.defenseEquipmentId
    && (!equipmentAvailable(defense, researchedTechnologyIds) || defense.kind === 'weapon')) return false
  return aggregateCosts([...baseCosts, ...loadout.equipmentCosts]).every(cost => (
    (stock[cost.equipmentId] ?? 0) + Number.EPSILON >= cost.amount * count
  ))
}

export function resolveEmpiresUnitLoadout(
  unit: EmpiresUnitDefinition,
  equipmentDefinitions: readonly CombatEquipmentDefinition[],
  researchedTechnologyIds: readonly string[],
  stock: Readonly<Record<string, number>>,
  count: number,
): ResolvedEmpiresUnitLoadout {
  if (!unit.td) {
    return {
      id: 'default',
      weapon: null,
      armor: null,
      equipmentCosts: aggregateCosts(unit.equipmentCosts ?? []),
    }
  }
  const equipment = new Map(equipmentDefinitions.map(definition => [definition.id, definition]))
  const researched = new Set(researchedTechnologyIds)
  const loadout = [...(unit.loadouts ?? [])]
    .sort((left, right) => right.priority - left.priority || stableCompare(left.id, right.id))
    .find(candidate => loadoutCanEquip(
      unit.equipmentCosts ?? [],
      candidate,
      equipment,
      researched,
      stock,
      count,
    ))
  if ((unit.loadouts?.length ?? 0) > 0 && !loadout) {
    throw new Error(`Unit ${unit.id} has no produced loadout available.`)
  }
  const weaponEquipmentId = loadout?.weaponEquipmentId ?? unit.td.weaponEquipmentId
  const defenseEquipmentId = loadout?.defenseEquipmentId ?? unit.td.armorEquipmentId
  const weapon = equipment.get(weaponEquipmentId)
  const defense = defenseEquipmentId ? equipment.get(defenseEquipmentId) : undefined
  if (!equipmentAvailable(weapon, researched) || weapon.kind !== 'weapon') {
    throw new Error(`Unit ${unit.id} has no available weapon ${weaponEquipmentId}.`)
  }
  if (defenseEquipmentId
    && (!equipmentAvailable(defense, researched) || defense.kind === 'weapon')) {
    throw new Error(`Unit ${unit.id} has no available defense ${defenseEquipmentId}.`)
  }
  return {
    id: loadout?.id ?? 'default',
    weaponEquipmentId,
    ...(defenseEquipmentId ? { defenseEquipmentId } : {}),
    weapon: cloneJson(weapon.profile as CombatWeaponProfile),
    armor: defense ? cloneJson(defense.profile as CombatArmorProfile) : null,
    equipmentCosts: aggregateCosts([
      ...(unit.equipmentCosts ?? []),
      ...(loadout?.equipmentCosts ?? []),
    ]),
  }
}
