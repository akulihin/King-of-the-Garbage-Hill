import { DAMAGE_TYPES } from './types'
import type {
  ArmorDefinition,
  BattleResult,
  BattleSurvivor,
  CalculatorProfile,
  CollisionStep,
  CollisionStepKind,
  CollisionSummary,
  DamageType,
  DamageValues,
  TeamId,
  UnitConfig,
  WeaponDefinition,
} from './types'

interface RuntimeWeapon {
  definition: WeaponDefinition
  durability: number
  infinite: boolean
  slotName: 'Основное' | 'Вторичное' | 'Кинжал'
}

interface RuntimeUnit {
  config: UnitConfig
  team: TeamId
  hp: number
  maxHp: number
  fatigue: number
  strength: number
  moveSpeed: number
  heaviness: number
  armorMax: DamageValues
  armorCurrent: DamageValues
  weapons: RuntimeWeapon[]
  activeWeaponIndex: number
  bleedStacks: number
}

interface CollisionContext {
  steps: CollisionStep[]
  rng: () => number
  profile: CalculatorProfile
  time: number
  bleedAtA: number
  bleedAtB: number
}

interface Pairing {
  team1: RuntimeUnit
  team2: RuntimeUnit
  phase: CollisionSummary['phase']
}

export interface UnitPreview {
  hp: number
  moveSpeed: number
  heaviness: number
  weaponName: string | null
  armorCount: number
}

const EPSILON = 0.000001

function emptyDamage(): DamageValues {
  return { Ударное: 0, Дробящее: 0, Рубящее: 0, Режущее: 0, Колющее: 0 }
}

function cloneDamage(value: DamageValues): DamageValues {
  return { ...value }
}

function addDamage(target: DamageValues, source: DamageValues): void {
  for (const type of DAMAGE_TYPES) target[type] += source[type]
}

function round(value: number, precision = 2): number {
  const factor = 10 ** precision
  return Math.round((value + Number.EPSILON) * factor) / factor
}

function createRng(seed: number): () => number {
  let state = seed >>> 0
  return () => {
    state += 0x6D2B79F5
    let value = state
    value = Math.imul(value ^ (value >>> 15), value | 1)
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61)
    return ((value ^ (value >>> 14)) >>> 0) / 4294967296
  }
}

function shuffle<T>(items: T[], rng: () => number): T[] {
  const result = [...items]
  for (let index = result.length - 1; index > 0; index -= 1) {
    const other = Math.floor(rng() * (index + 1))
    ;[result[index], result[other]] = [result[other], result[index]]
  }
  return result
}

function selectedArmors(unit: UnitConfig, armors: ArmorDefinition[]): ArmorDefinition[] {
  const ids = [unit.helmetId, unit.mailId, unit.paddingId, unit.plateId].filter(Boolean)
  return ids
    .map(id => armors.find(item => item.id === id))
    .filter((item): item is ArmorDefinition => item !== undefined)
}

function buildRuntimeUnit(unit: UnitConfig, team: TeamId, profile: CalculatorProfile): RuntimeUnit {
  const talents = unit.talentIds
    .map(id => profile.talents.find(talent => talent.id === id))
    .filter((talent): talent is NonNullable<typeof talent> => talent !== undefined)
  const armors = selectedArmors(unit, profile.armors)
  const armorResists = emptyDamage()
  for (const item of armors) addDamage(armorResists, item.resists)

  const strength = talents.reduce((total, talent) => total + talent.strength, 0)
  const armorHp = armors.reduce((total, item) => total + item.hp, 0)
  const talentHp = talents.reduce((total, talent) => total + talent.hp, 0)
  const heaviness = armors.reduce((total, item) => total + item.heaviness, 0)
  const talentSpeed = talents.reduce((total, talent) => total + talent.speed, 0)
  const maxHp = Math.max(1, unit.baseHp + armorHp + talentHp)
  const moveSpeed = Math.max(
    profile.balance.minMoveSpeed,
    unit.baseMoveSpeed + talentSpeed - heaviness * profile.balance.heavinessSpeedPenalty,
  )

  const weaponSlots: Array<[string, RuntimeWeapon['slotName'], boolean]> = [
    [unit.primaryWeaponId, 'Основное', false],
    [unit.secondaryWeaponId, 'Вторичное', false],
    [unit.daggerWeaponId, 'Кинжал', true],
  ]
  const weapons = weaponSlots.flatMap(([id, slotName, infinite]) => {
    const definition = profile.weapons.find(item => item.id === id)
    if (!definition) return []
    return [{ definition, durability: definition.durability, infinite, slotName }]
  })

  return {
    config: unit,
    team,
    hp: maxHp,
    maxHp,
    fatigue: profile.balance.initialFatigue,
    strength,
    moveSpeed,
    heaviness,
    armorMax: cloneDamage(armorResists),
    armorCurrent: cloneDamage(armorResists),
    weapons,
    activeWeaponIndex: 0,
    bleedStacks: 0,
  }
}

function currentWeapon(unit: RuntimeUnit): RuntimeWeapon | null {
  while (unit.activeWeaponIndex < unit.weapons.length) {
    const weapon = unit.weapons[unit.activeWeaponIndex]
    if (weapon.infinite || weapon.durability > 0) return weapon
    unit.activeWeaponIndex += 1
  }
  return null
}

function effectiveResists(unit: RuntimeUnit): DamageValues {
  const result = cloneDamage(unit.armorCurrent)
  const defense = currentWeapon(unit)?.definition.defense ?? 0
  for (const type of DAMAGE_TYPES) result[type] += defense
  return result
}

function attackValues(unit: RuntimeUnit): DamageValues {
  const weapon = currentWeapon(unit)
  if (!weapon) return emptyDamage()
  const values = cloneDamage(weapon.definition.attacks)
  values.Ударное += unit.strength
  values.Дробящее += unit.strength
  values.Рубящее += unit.strength
  return values
}

function attackRate(unit: RuntimeUnit): number {
  const weapon = currentWeapon(unit)
  if (!weapon) return 0
  const masteryFactor = unit.config.mastery ? 1 : 0.5
  return Math.max(0, weapon.definition.speed * masteryFactor / Math.max(0.01, unit.fatigue))
}

function attackInterval(unit: RuntimeUnit): number {
  const rate = attackRate(unit)
  return rate > 0 ? 1 / rate : Number.POSITIVE_INFINITY
}

function techniqueFor(attacker: RuntimeUnit, target: RuntimeUnit): DamageType {
  const attacks = attackValues(attacker)
  const resists = effectiveResists(target)
  if (attacks.Режущее > 0 && attacks.Режущее >= resists.Режущее) return 'Режущее'
  return DAMAGE_TYPES.reduce((best, type) =>
    attacks[type] - resists[type] > attacks[best] - resists[best] ? type : best,
  'Ударное')
}

function snapshot(actor: RuntimeUnit, target: RuntimeUnit): CollisionStep['snapshot'] {
  return {
    actorHp: round(actor.hp),
    targetHp: round(target.hp),
    actorFatigue: round(actor.fatigue),
    targetFatigue: round(target.fatigue),
    actorDurability: currentWeapon(actor)?.infinite ? null : round(currentWeapon(actor)?.durability ?? 0),
    targetDurability: currentWeapon(target)?.infinite ? null : round(currentWeapon(target)?.durability ?? 0),
    actorResists: effectiveResists(actor),
    targetResists: effectiveResists(target),
  }
}

function addStep(
  context: CollisionContext,
  actor: RuntimeUnit,
  target: RuntimeUnit,
  kind: CollisionStepKind,
  message: string,
  details: Partial<Pick<CollisionStep, 'weaponName' | 'technique' | 'penetration' | 'resistance' | 'damage'>> = {},
): void {
  context.steps.push({
    index: context.steps.length + 1,
    time: round(context.time, 3),
    kind,
    actorName: actor.config.name,
    targetName: target.config.name,
    message,
    snapshot: snapshot(actor, target),
    ...details,
  })
}

function degradeArmor(unit: RuntimeUnit): void {
  for (const type of DAMAGE_TYPES) unit.armorCurrent[type] = Math.max(0, unit.armorCurrent[type] - 1)
}

function resetArmorAfterPenetration(unit: RuntimeUnit): void {
  for (const type of DAMAGE_TYPES) {
    unit.armorMax[type] = Math.max(0, unit.armorMax[type] - 1)
    unit.armorCurrent[type] = unit.armorMax[type]
  }
}

function damageWeapon(
  unit: RuntimeUnit,
  target: RuntimeUnit,
  context: CollisionContext,
  forced = false,
): boolean {
  const weapon = currentWeapon(unit)
  if (!weapon || weapon.infinite) return false
  const { durabilityLossMin, durabilityLossMax } = context.profile.balance
  const loss = forced
    ? weapon.durability
    : durabilityLossMin + context.rng() * Math.max(0, durabilityLossMax - durabilityLossMin)
  weapon.durability = Math.max(0, weapon.durability - loss)
  if (weapon.durability > 0) return false

  const brokenName = weapon.definition.name
  unit.activeWeaponIndex += 1
  const replacement = currentWeapon(unit)
  addStep(
    context,
    unit,
    target,
    'weapon',
    replacement
      ? `${brokenName} потеряно. ${unit.config.name} переключается на ${replacement.definition.name}.`
      : `${brokenName} потеряно. У ${unit.config.name} больше нет оружия.`,
    { weaponName: brokenName },
  )
  return true
}

function processBleed(
  victim: RuntimeUnit,
  opponent: RuntimeUnit,
  context: CollisionContext,
): void {
  const damage = victim.bleedStacks * context.profile.balance.bleedDamage
  victim.hp = Math.max(0, victim.hp - damage)
  addStep(
    context,
    opponent,
    victim,
    'bleed',
    `${victim.config.name} получает ${round(damage)} урона от кровотечения (${victim.bleedStacks} ст.).`,
    { damage },
  )
}

function resolveCollision(
  team1: RuntimeUnit,
  team2: RuntimeUnit,
  phase: CollisionSummary['phase'],
  order: number,
  profile: CalculatorProfile,
  rng: () => number,
): CollisionSummary {
  const context: CollisionContext = {
    steps: [],
    rng,
    profile,
    time: 0,
    bleedAtA: team1.bleedStacks > 0 ? profile.balance.bleedIntervalSeconds : Number.POSITIVE_INFINITY,
    bleedAtB: team2.bleedStacks > 0 ? profile.balance.bleedIntervalSeconds : Number.POSITIVE_INFINITY,
  }
  let nextAttackA = Number.POSITIVE_INFINITY
  let nextAttackB = Number.POSITIVE_INFINITY
  let disabledUntilA = 0
  let disabledUntilB = 0
  const reachA = currentWeapon(team1)?.definition.rangeMax ?? 0
  const reachB = currentWeapon(team2)?.definition.rangeMax ?? 0
  const gap = Math.abs(reachA - reachB)
  let rangedLeader: TeamId | null = null
  let approachTime = 0

  if (reachA > reachB) {
    rangedLeader = 1
    approachTime = gap / team2.moveSpeed
    nextAttackA = attackInterval(team1)
    nextAttackB = approachTime
  }
  else if (reachB > reachA) {
    rangedLeader = 2
    approachTime = gap / team1.moveSpeed
    nextAttackB = attackInterval(team2)
    nextAttackA = approachTime
  }
  else {
    const rateA = attackRate(team1)
    const rateB = attackRate(team2)
    const team1First = rateA === rateB ? rng() < 0.5 : rateA > rateB
    nextAttackA = team1First ? 0 : attackInterval(team1)
    nextAttackB = team1First ? attackInterval(team2) : 0
  }

  addStep(
    context,
    reachA >= reachB ? team1 : team2,
    reachA >= reachB ? team2 : team1,
    'movement',
    gap > 0
      ? `Разница дистанции ${round(gap)} м. Сближение занимает ${round(approachTime)} сек.; дальнобойный боец атакует только за полные интервалы оружия.`
      : 'Дистанция оружия равна: инициатива определяется скоростью оружия.',
  )

  const handleAttack = (attacker: RuntimeUnit, target: RuntimeUnit, attackerIsA: boolean): void => {
    const weapon = currentWeapon(attacker)
    if (!weapon) {
      if (attackerIsA) nextAttackA = Number.POSITIVE_INFINITY
      else nextAttackB = Number.POSITIVE_INFINITY
      return
    }

    const technique = techniqueFor(attacker, target)
    const penetration = attackValues(attacker)[technique]
    const resistance = effectiveResists(target)[technique]
    const penetrated = penetration >= resistance
    let delayMultiplier = 1
    let damage = 0

    if (!penetrated) {
      degradeArmor(target)
      addStep(
        context,
        attacker,
        target,
        'block',
        `${attacker.config.name}: ${technique.toLowerCase()} удар ${penetration} против резиста ${resistance}. Броня блокирует удар и теряет 1 ко всем резистам.`,
        { weaponName: weapon.definition.name, technique, penetration, resistance, damage: 0 },
      )
    }
    else {
      damage = profile.balance.baseDamage * (technique === 'Колющее' ? 2 : 1)
      target.hp = Math.max(0, target.hp - damage)
      resetArmorAfterPenetration(target)
      addStep(
        context,
        attacker,
        target,
        'damage',
        `${attacker.config.name}: ${technique.toLowerCase()} удар ${penetration} пробивает резист ${resistance} и наносит ${round(damage)} урона.`,
        { weaponName: weapon.definition.name, technique, penetration, resistance, damage },
      )

      if (technique !== 'Режущее') delayMultiplier = profile.balance.postDamageDelayMultiplier
      if (technique === 'Ударное') {
        if (attackerIsA) disabledUntilB = Math.max(disabledUntilB, context.time + profile.balance.stunSeconds)
        else disabledUntilA = Math.max(disabledUntilA, context.time + profile.balance.stunSeconds)
        addStep(context, attacker, target, 'effect', `${target.config.name} оглушён на ${profile.balance.stunSeconds} сек.`, { technique })
      }
      else if (technique === 'Дробящее' && rng() < profile.balance.crushKnockoutChance) {
        target.hp = 0
        addStep(context, attacker, target, 'effect', `${target.config.name} выведен из боя дробящим эффектом.`, { technique })
      }
      else if (technique === 'Рубящее') {
        const targetDelay = attackInterval(target)
        if (attackerIsA) nextAttackB = context.time + targetDelay
        else nextAttackA = context.time + targetDelay
        addStep(context, attacker, target, 'effect', `Замах ${target.config.name} отменён.`, { technique })
        if (rng() < profile.balance.disarmChance) {
          damageWeapon(target, attacker, context, true)
          addStep(context, attacker, target, 'effect', `${target.config.name} получает Дизарм.`, { technique })
        }
      }
      else if (technique === 'Режущее') {
        target.bleedStacks += 1
        const nextTick = context.time + profile.balance.bleedIntervalSeconds
        if (attackerIsA) context.bleedAtB = Math.min(context.bleedAtB, nextTick)
        else context.bleedAtA = Math.min(context.bleedAtA, nextTick)
        addStep(context, attacker, target, 'effect', `Кровотечение ${target.config.name}: ${target.bleedStacks} ст.`, { technique })
      }
    }

    const cuttingSuccess = penetrated && technique === 'Режущее'
    if (!cuttingSuccess) {
      attacker.fatigue += weapon.definition.fatigue + attacker.heaviness * profile.balance.armorFatigueFactor
    }
    damageWeapon(attacker, target, context)
    const nextInterval = attackInterval(attacker) * delayMultiplier
    if (attackerIsA) nextAttackA = context.time + nextInterval
    else nextAttackB = context.time + nextInterval
  }

  while (
    team1.hp > 0
    && team2.hp > 0
    && context.time <= profile.balance.maxCollisionSeconds
    && context.steps.length < profile.balance.maxCollisionEvents
  ) {
    const nextEvent = Math.min(nextAttackA, nextAttackB, context.bleedAtA, context.bleedAtB)
    if (!Number.isFinite(nextEvent) || nextEvent > profile.balance.maxCollisionSeconds) break
    context.time = nextEvent

    let bleedProcessed = false
    if (Math.abs(context.bleedAtA - nextEvent) < EPSILON) {
      processBleed(team1, team2, context)
      context.bleedAtA += profile.balance.bleedIntervalSeconds
      bleedProcessed = true
    }
    if (Math.abs(context.bleedAtB - nextEvent) < EPSILON) {
      processBleed(team2, team1, context)
      context.bleedAtB += profile.balance.bleedIntervalSeconds
      bleedProcessed = true
    }
    if (team1.hp <= 0 || team2.hp <= 0) break
    if (bleedProcessed) continue

    const simultaneousAttacks = Math.abs(nextAttackA - nextAttackB) < EPSILON
    const team2HasOpeningTiePriority = simultaneousAttacks
      && rangedLeader === 2
      && context.time <= approachTime + EPSILON
    if (nextAttackA < nextAttackB || (simultaneousAttacks && !team2HasOpeningTiePriority)) {
      if (disabledUntilA > context.time) nextAttackA = disabledUntilA
      else handleAttack(team1, team2, true)
    }
    else if (disabledUntilB > context.time) nextAttackB = disabledUntilB
    else handleAttack(team2, team1, false)
  }

  let winner: RuntimeUnit | null = null
  if (team1.hp > 0 && team2.hp <= 0) winner = team1
  else if (team2.hp > 0 && team1.hp <= 0) winner = team2
  else if (team1.hp > 0 && team2.hp > 0) {
    const ratio1 = team1.hp / team1.maxHp
    const ratio2 = team2.hp / team2.maxHp
    if (Math.abs(ratio1 - ratio2) > EPSILON) winner = ratio1 > ratio2 ? team1 : team2
  }

  addStep(
    context,
    winner ?? team1,
    winner ? (winner === team1 ? team2 : team1) : team2,
    'result',
    winner
      ? `${winner.config.name} побеждает и сохраняет ${round(winner.hp)} ХП.`
      : `Столкновение ${team1.config.name} и ${team2.config.name} завершилось без победителя.`,
  )

  return {
    id: `collision-${order}`,
    order,
    phase,
    team1UnitId: team1.config.id,
    team2UnitId: team2.config.id,
    team1Name: team1.config.name,
    team2Name: team2.config.name,
    winnerTeam: winner?.team ?? null,
    winnerUnitId: winner?.config.id ?? null,
    winnerName: winner?.config.name ?? null,
    duration: round(context.time, 3),
    steps: context.steps,
  }
}

function survivorFrom(unit: RuntimeUnit): BattleSurvivor {
  return {
    unitId: unit.config.id,
    team: unit.team,
    name: unit.config.name,
    hp: round(unit.hp),
    maxHp: round(unit.maxHp),
    fatigue: round(unit.fatigue),
    weaponName: currentWeapon(unit)?.definition.name ?? null,
  }
}

export function getUnitPreview(unit: UnitConfig, team: TeamId, profile: CalculatorProfile): UnitPreview {
  const runtime = buildRuntimeUnit(unit, team, profile)
  return {
    hp: runtime.maxHp,
    moveSpeed: round(runtime.moveSpeed),
    heaviness: round(runtime.heaviness),
    weaponName: currentWeapon(runtime)?.definition.name ?? null,
    armorCount: selectedArmors(unit, profile.armors).length,
  }
}

export function runFight(profile: CalculatorProfile, requestedSeed = Date.now()): BattleResult {
  const seed = Math.abs(Math.trunc(requestedSeed)) || 1
  const rng = createRng(seed)
  const runtime1 = profile.team1.map(unit => unit.enabled ? buildRuntimeUnit(unit, 1, profile) : null)
  const runtime2 = profile.team2.map(unit => unit.enabled ? buildRuntimeUnit(unit, 2, profile) : null)
  const used1 = new Set<string>()
  const used2 = new Set<string>()
  const pairings: Pairing[] = []

  for (let index = 0; index < Math.max(runtime1.length, runtime2.length); index += 1) {
    const unit1 = runtime1[index]
    const unit2 = runtime2[index]
    if (!unit1 || !unit2) continue
    pairings.push({ team1: unit1, team2: unit2, phase: 'mirror' })
    used1.add(unit1.config.id)
    used2.add(unit2.config.id)
  }

  const remaining1 = shuffle(runtime1.filter((unit): unit is RuntimeUnit => unit !== null && !used1.has(unit.config.id)), rng)
  const remaining2 = shuffle(runtime2.filter((unit): unit is RuntimeUnit => unit !== null && !used2.has(unit.config.id)), rng)
  while (remaining1.length > 0 && remaining2.length > 0) {
    pairings.push({ team1: remaining1.shift()!, team2: remaining2.shift()!, phase: 'fallback' })
  }

  const queue1 = remaining1
  const queue2 = remaining2
  const collisions: CollisionSummary[] = []
  for (const pairing of pairings) {
    const collision = resolveCollision(pairing.team1, pairing.team2, pairing.phase, collisions.length + 1, profile, rng)
    collisions.push(collision)
    if (collision.winnerTeam === 1) queue1.push(pairing.team1)
    else if (collision.winnerTeam === 2) queue2.push(pairing.team2)
  }

  while (queue1.length > 0 && queue2.length > 0) {
    const unit1 = queue1.splice(Math.floor(rng() * queue1.length), 1)[0]
    const unit2 = queue2.splice(Math.floor(rng() * queue2.length), 1)[0]
    const collision = resolveCollision(unit1, unit2, 'survivors', collisions.length + 1, profile, rng)
    collisions.push(collision)
    if (collision.winnerTeam === 1) queue1.push(unit1)
    else if (collision.winnerTeam === 2) queue2.push(unit2)
  }

  const survivors = [...queue1, ...queue2].filter(unit => unit.hp > 0).map(survivorFrom)
  const winnerTeam: TeamId | null = queue1.length > 0 && queue2.length === 0
    ? 1
    : queue2.length > 0 && queue1.length === 0
      ? 2
      : null

  return {
    seed,
    winnerTeam,
    survivors,
    collisions,
    message: winnerTeam
      ? `Команда ${winnerTeam} победила. Выживших: ${survivors.length}.`
      : 'Бой завершился без победителя.',
  }
}
