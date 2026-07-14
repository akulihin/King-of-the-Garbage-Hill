import type {
  ArmorDefinition,
  ArmorSlot,
  CalculatorProfile,
  DamageValues,
  FightBalance,
  TalentDefinition,
  TeamId,
  UnitConfig,
  WeaponDefinition,
} from './types'

function damage(
  impact = 0,
  crushing = 0,
  chopping = 0,
  cutting = 0,
  piercing = 0,
): DamageValues {
  return {
    Ударное: impact,
    Дробящее: crushing,
    Рубящее: chopping,
    Режущее: cutting,
    Колющее: piercing,
  }
}

function weapon(
  id: string,
  name: string,
  category: string,
  attacks: DamageValues,
  defense: number,
  disarm: number,
  antiShield: number,
  speed: number,
  rangeMin: number,
  rangeMax = rangeMin,
  handsMin = 1,
  handsMax = handsMin,
): WeaponDefinition {
  return {
    id,
    name,
    category,
    attacks,
    defense,
    disarm,
    antiShield,
    speed,
    rangeMin,
    rangeMax,
    handsMin,
    handsMax,
    durability: 5,
    fatigue: 0.1,
  }
}

function armor(
  id: string,
  name: string,
  category: string,
  slot: ArmorSlot,
  resists: DamageValues,
  hp: number,
  weight: number,
  ergonomics: number,
  heaviness: number,
): ArmorDefinition {
  return { id, name, category, slot, resists, hp, weight, ergonomics, heaviness }
}

export const BASE_WEAPONS: WeaponDefinition[] = [
  weapon('w01', 'Булава', 'Ударные', damage(3, 2), 1, 0, 0, 4, 0.8),
  weapon('w02', 'Двуручный цеп', 'Ударные', damage(5), 4, 7, 8, 2, 1.2, 1.2, 2),
  weapon('w03', 'Крестовая булова', 'Ударные', damage(2, 5), 1, 3, 0, 4, 0.8),
  weapon('w04', 'Еж', 'Ударные', damage(2, 3, 0, 0, 2), 1, 0, 0, 2, 0.8),
  weapon('w05', 'Клевец', 'Ударные', damage(4, 4, 0, 0, 4), 1, 0, 0, 2, 0.8),
  weapon('w06', 'Двуручный Еж', 'Ударные', damage(4, 6, 0, 0, 4), 1, 0, 0, 1, 1.3, 1.3, 2),
  weapon('w07', 'Двуручный Клевец', 'Ударные', damage(8, 8, 0, 0, 8), 3, 0, 4, 1, 1.5, 1.5, 2),
  weapon('w08', 'Копье лавровое', 'Древковые', damage(0, 0, 2, 2, 3), 2, 0, 0, 5, 1.4, 2, 1, 2),
  weapon('w09', 'Ланцетовидное', 'Древковые', damage(0, 0, 2, 0, 4), 2, 0, 0, 5, 1.4, 2, 1, 2),
  weapon('w10', 'Ромбовидное', 'Древковые', damage(0, 1, 0, 0, 5), 2, 0, 0, 4, 1.4, 2, 1, 2),
  weapon('w11', 'Крестовидное', 'Древковые', damage(0, 3, 0, 0, 6), 2, 0, 0, 3, 1.8, 2.5, 2),
  weapon('w12', 'Шиловидное', 'Древковые', damage(0, 1, 0, 0, 7), 2, 0, 0, 3, 2, 3, 2),
  weapon('w13', 'Алебарда', 'Древковые', damage(0, 5, 5, 0, 5), 4, 3, 6, 2, 1.8, 1.8, 2),
  weapon('w14', 'Полэкс', 'Древковые', damage(5, 6, 2, 0, 4), 7, 4, 5, 2, 1.4, 1.4, 2),
  weapon('w15', 'Стальной топор', 'Рубящие', damage(0, 1, 4, 2), 2, 0, 0, 4, 0.8),
  weapon('w16', 'Бородатый топор', 'Рубящие', damage(0, 2, 5, 3), 3, 5, 3, 5, 0.8),
  weapon('w17', 'Двуручный Бродекс', 'Рубящие', damage(0, 5, 9), 2, 0, 4, 2, 1.1, 1.1, 2),
  weapon('w18', 'Боевой топор', 'Рубящие', damage(0, 3, 6, 1, 3), 2, 0, 2, 3, 0.9),
  weapon('w19', 'Полу-стальной меч', 'Клинковые', damage(0, 0, 2, 3, 1), 2, 1, 0, 6, 0.8),
  weapon('w20', 'Крестовой меч', 'Клинковые', damage(0, 0, 3, 3, 2), 4, 1, 0, 7, 0.9),
  weapon('w21', 'Фальшион', 'Клинковые', damage(0, 0, 5, 5, 1), 2, 0, 0, 6, 0.8),
  weapon('w22', 'Длинный меч', 'Клинковые', damage(1, 1, 4, 3, 3), 6, 2, 0, 5, 1.2, 1.2, 1, 2),
  weapon('w23', 'Эсток', 'Клинковые', damage(2, 0, 0, 0, 7), 4, 0, 0, 4, 1.4, 1.4, 2),
  weapon('w24', 'Бастард', 'Клинковые', damage(2, 2, 2, 2, 5), 7, 2, 0, 5, 1.3, 1.3, 1, 2),
  weapon('w25', 'Великий меч', 'Клинковые', damage(3, 3, 2, 0, 2), 8, 3, 0, 4, 1.5, 1.5, 2),
  weapon('w26', 'Шпага', 'Клинковые', damage(0, 0, 2, 0, 3), 5, 2, 0, 7, 1.3),
  weapon('w27', 'Килидж', 'Скимитар', damage(0, 0, 6, 7), 2, 2, 0, 7, 1),
  weapon('w28', 'Шамшир', 'Скимитар', damage(0, 0, 6, 9), 3, 2, 0, 8, 1),
  weapon('w29', 'Ятаган', 'Скимитар', damage(0, 0, 6, 8, 2), 2, 1, 0, 9, 0.7),
]

export const BASE_ARMORS: ArmorDefinition[] = [
  armor('a01', 'Стеганка', 'Поддоспешник', 'padding', damage(1), 0, 1, 100, 0.5),
  armor('a02', 'Плотная Стеганка', 'Поддоспешник', 'padding', damage(2, 0, 0, 1), 1, 3, 90, 1.578947368),
  armor('a03', 'Дублет', 'Поддоспешник', 'padding', damage(2, 0, 0, 1), 1, 4, 200, 1.333333333),
  armor('a04', 'Толстяк', 'Поддоспешник', 'padding', damage(5, 1, 0, 2), 2, 6, 80, 3.333333333),
  armor('a05', 'Железная Бутсовая кольчуга', 'Кольчуга', 'mail', damage(0, 0, 2, 5, 1), 1, 10, 100, 5),
  armor('a06', 'Клепаная кольчуга', 'Кольчуга', 'mail', damage(0, 0, 3, 5, 2), 1, 10, 100, 5),
  armor('a07', 'Полная кольчуга', 'Кольчуга', 'mail', damage(0, 0, 3, 7, 2), 1, 15, 90, 7.894736842),
  armor('a08', 'Двойная кольчуга', 'Кольчуга', 'mail', damage(0, 0, 4, 7, 3), 2, 20, 80, 11.11111111),
  armor('a09', 'Кольчуга со сталью', 'Кольчуга', 'mail', damage(0, 0, 4, 7, 3), 2, 15, 90, 7.894736842),
  armor('a10', 'Закаленная кольчуга', 'Кольчуга', 'mail', damage(0, 0, 5, 7, 3), 2, 15, 90, 7.894736842),
  armor('a11', 'Кольчужные межлатные вставки из чистой стали', 'Кольчуга', 'mail', damage(0, 0, 2, 5, 2), 1, 5, 120, 2.272727273),
  armor('a12', 'Железный Носатый шлем', 'Шлем', 'helmet', damage(0, 1, 4, 2), 0, 1, 100, 0.5),
  armor('a13', 'Шлем-ведро', 'Шлем', 'helmet', damage(0, 2, 5, 3), 3, 3, 10, 2.727272727),
  armor('a14', 'Железная шляпа', 'Шлем', 'helmet', damage(0, 5, 9), 0, 2, 100, 1),
  armor('a15', 'Дятел', 'Шлем', 'helmet', damage(0, 3, 6, 1, 3), 0, 3, 20, 2.5),
  armor('a16', 'Бородатый шлем', 'Шлем', 'helmet', damage(0, 3, 6, 1, 3), 0, 3, 50, 2),
  armor('a17', 'Армет', 'Шлем', 'helmet', damage(0, 3, 6, 1, 3), 0, 4, 100, 2),
  armor('a18', 'Кавалерийский шлем', 'Шлем', 'helmet', damage(0, 3, 6, 1, 3), 0, 4, 150, 1.6),
  armor('a19', 'Мертвая голова', 'Шлем', 'helmet', damage(0, 3, 6, 1, 3), 0, 17, 400, 3.4),
]

export const BASE_TALENTS: TalentDefinition[] = [
  { id: 't01', name: 'Крепкий', strength: 0, hp: 1, speed: 0 },
  { id: 't02', name: 'Уволень', strength: 1, hp: 1, speed: 0 },
  { id: 't03', name: 'Атлтет', strength: 1, hp: 1, speed: 0 },
  { id: 't04', name: 'Мастер', strength: 0, hp: 3, speed: 0 },
]

export const DEFAULT_BALANCE: FightBalance = {
  baseDamage: 1,
  initialFatigue: 1,
  armorFatigueFactor: 0.01,
  heavinessSpeedPenalty: 0.05,
  minMoveSpeed: 0.1,
  postDamageDelayMultiplier: 2,
  stunSeconds: 2,
  crushKnockoutChance: 0.5,
  disarmChance: 0.5,
  bleedDamage: 1,
  bleedIntervalSeconds: 5,
  durabilityLossMin: 0.1,
  durabilityLossMax: 1,
  maxCollisionSeconds: 300,
  maxCollisionEvents: 500,
}

export function cloneValue<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

export function createProfileId(): string {
  return `fight-profile-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

export function createUnit(team: TeamId, slot: number): UnitConfig {
  const isExample = slot === 0
  return {
    id: `team-${team}-slot-${slot + 1}`,
    enabled: isExample,
    name: isExample ? `Боец ${team === 1 ? 'А' : 'Б'}1` : '',
    primaryWeaponId: isExample ? (team === 1 ? 'w01' : 'w20') : '',
    secondaryWeaponId: '',
    daggerWeaponId: 'w29',
    helmetId: isExample && team === 2 ? 'a13' : '',
    mailId: isExample && team === 1 ? 'a05' : '',
    paddingId: isExample ? (team === 1 ? 'a01' : 'a02') : '',
    plateId: '',
    talentIds: isExample ? [team === 1 ? 't01' : 't03'] : [],
    mastery: false,
    baseHp: 10,
    baseMoveSpeed: 1,
  }
}

export function createDefaultProfile(name = 'Базовый профиль'): CalculatorProfile {
  return {
    id: createProfileId(),
    name,
    team1: Array.from({ length: 6 }, (_, index) => createUnit(1, index)),
    team2: Array.from({ length: 6 }, (_, index) => createUnit(2, index)),
    balance: cloneValue(DEFAULT_BALANCE),
    weapons: cloneValue(BASE_WEAPONS),
    armors: cloneValue(BASE_ARMORS),
    talents: cloneValue(BASE_TALENTS),
  }
}
