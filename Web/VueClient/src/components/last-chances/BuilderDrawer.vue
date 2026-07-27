<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import {
  Braces,
  CheckCircle2,
  Download,
  Eraser,
  FlaskConical,
  Gauge,
  RotateCcw,
  Save,
  SlidersHorizontal,
  Upload,
  X,
  XCircle,
  Zap,
} from 'lucide-vue-next'
import {
  cloneLastChancesConfig,
  LAST_CHANCES_AUGMENTS,
  LAST_CHANCES_COLLIDER_SHAPES,
  LAST_CHANCES_GESTURES,
  LAST_CHANCES_TACTILE_PROFILES,
  migrateLastChancesConfig,
  type LastChancesAttackBehavior,
  type LastChancesAttackDefinition,
  type LastChancesAttackSetControlDefinition,
  type LastChancesAugment,
  type LastChancesConfig,
  type LastChancesDualSenseComboNodeDefinition,
  type LastChancesGesture,
  type LastChancesMylorikActivationDefinition,
  type LastChancesTactileProfile,
  type LastChancesWeaponDefinition,
  validateLastChancesConfig,
} from '../../features/last-chances'
import type { LastChancesLocale } from './RunMapOverlay.vue'

const props = defineProps<{
  open: boolean
  locale: LastChancesLocale
  config: LastChancesConfig | null
}>()

const emit = defineEmits<{
  close: []
  apply: [config: LastChancesConfig]
  applyControls: [config: LastChancesConfig]
  save: [config: LastChancesConfig]
  clear: []
}>()

type BuilderTab = 'quick' | 'json'

const copy = {
  en: {
    eyebrow: 'Prototype workbench',
    title: '99LC Builder',
    subtitle: 'Tune the current definition without recompiling the game.',
    close: 'Close builder',
    quick: 'Quick tune',
    json: 'Raw JSON',
    valid: 'Definition valid',
    invalid: 'Needs attention',
    noConfig: 'The definition is still loading.',
    validate: 'Validate',
    apply: 'Apply & start fresh generation',
    applyControls: 'Apply control tuning live',
    save: 'Save browser override',
    clear: 'Clear override',
    import: 'Import JSON',
    export: 'Export JSON',
    imported: 'Imported definition is ready to apply.',
    exported: 'Definition exported.',
    applied: 'Definition applied to a fresh generation.',
    saved: 'Browser override saved; the current attempt is unchanged.',
    cleared: 'Browser override cleared; the current attempt is unchanged.',
    parseError: 'JSON cannot be parsed',
    validationErrors: 'Validation errors',
    jsonHelp: 'Edit the complete runtime definition. Validate before applying.',
    player: 'Player',
    playerHelp: 'Starting body, mind and movement values',
    chances: 'Chances & erosion',
    chancesHelp: 'Death cost and permanent loss for the selected tier',
    input: 'Controls & timing',
    inputHelp: 'DeepList, mylorik and DualSense recognition values',
    deepList: 'DeepList',
    mylorik: 'mylorik',
    dualSense: 'DualSense',
    attack: 'Selected attack',
    attackHelp: 'One weapon gesture at a time',
    moveDescription: 'In-game move description',
    axeSettings: 'Two-handed Axe',
    axeSettingsHelp: 'Direction-assisted basic tap damage',
    axeMotionBonus: 'Maximum mouse bonus',
    axeMotionPixels: 'Mouse travel for maximum (px)',
    swordSettings: 'Mercenary Sword',
    swordSettingsHelp: 'Rhythm, fatigue, advance, stagger and follow-up tuning',
    swordStaggerEnabled: 'Zornhaw stagger and Unstoppable enabled',
    swordPerfectStart: 'Ideal timing starts (ms)',
    swordPerfectEnd: 'Perfect timing ends (ms)',
    swordFatigue: 'Fatigue duration (ms)',
    swordRoomMisses: 'Misses per room before fatigue',
    swordConsecutiveMisses: 'Consecutive misses before fatigue',
    swordAdvanceDistance: 'Advance distance',
    swordAdvanceSpeed: 'Advance speed',
    swordStagger: 'Stagger per hit (ms)',
    swordStaggerThreshold: 'Unstoppable threshold (ms)',
    swordUnstoppable: 'Unstoppable duration (ms)',
    swordUnterhauHold: 'Unterhaw hold (ms)',
    swordUnterhauCooldown: 'Unterhaw cooldown multiplier',
    swordEmptyOffhand: 'Empty off-hand damage multiplier',
    loadout: 'Starting loadout',
    loadoutHelp: 'Select the two active slots and their augment symbols',
    moveQuestsEnabled: 'Move-unlock quests enabled',
    moveQuestKills: 'Kills per move quest',
    sameTierSacrifice: 'Same-tier health retained',
    attackStopsMovement: 'Attacks stop movement immediately',
    minimumParry: 'Minimum parry window (ms)',
    revealOnParry: 'Enemy reveal after parry (ms)',
    revealOnHit: 'Enemy reveal after hit (ms)',
    minimumDamage: 'Minimum damage taken',
    enemy: 'Selected enemy',
    enemyHelp: 'Awareness, pursuit and attack tuning',
    weapon: 'Weapon',
    primaryWeapon: 'Primary weapon',
    secondaryWeapon: 'Secondary weapon',
    noSecondary: 'Empty secondary slot',
    noPrimary: 'Empty primary slot',
    artifact: 'Artifact',
    noArtifact: 'No artifact',
    outfit: 'Outfit',
    noOutfit: 'No outfit',
    primaryAugment: 'Primary augment',
    secondaryAugment: 'Secondary augment',
    attackSet: 'Input set',
    primarySet: 'Primary input',
    secondarySet: 'Secondary input',
    gesture: 'Gesture',
    enemySelect: 'Enemy',
    tier: 'Tier',
    maxHp: 'Physical health',
    maxMental: 'Mental health',
    maxStamina: 'Stamina',
    moveSpeed: 'Move speed',
    acceleration: 'Acceleration to full speed (ms)',
    deceleration: 'Braking to rest (ms)',
    armor: 'Armor',
    attackPower: 'Attack power',
    radius: 'Body radius',
    invulnerability: 'Hit immunity (ms)',
    chanceCount: 'Starting Chances',
    deathCost: 'Chances lost on death',
    erosionHp: 'Health erosion',
    erosionMental: 'Mental erosion',
    erosionSpeed: 'Speed erosion',
    erosionArmor: 'Armor erosion',
    erosionAttack: 'Attack erosion',
    doubleTap: 'Double-tap window (ms)',
    tapCombo: 'Basic-combo continuation window (ms)',
    hold: 'Hold threshold (ms)',
    holdMax: 'Hold combo limit (ms)',
    holdDouble: 'Hold follow-up tap window (ms)',
    aimDeadZone: 'Aim dead zone',
    actionDirectionDeadZone: 'Action-direction dead zone',
    gamepadDeadZone: 'Gamepad dead zone',
    gamepadLeftButton: 'Primary button index',
    gamepadRightButton: 'Secondary button index',
    techniqueHold: 'Technique hold threshold (ms)',
    inputBuffer: 'One-intent buffer (ms)',
    continuationWindow: 'Continuation window (ms)',
    activationThreshold: 'Trigger activation',
    releaseThreshold: 'Trigger release',
    hysteresis: 'Trigger hysteresis',
    shallowGate: 'Shallow gate',
    mediumGate: 'Medium gate',
    deepGate: 'Deep gate',
    finalGate: 'Final gate',
    feedbackCaps: 'Feedback safety caps',
    maxMagnitude: 'Maximum magnitude',
    maxDuration: 'Maximum effect (ms)',
    blockedRepeat: 'Blocked-cue interval (ms)',
    tactileProfile: 'Adaptive profile',
    profileStart: 'Start position',
    profileEnd: 'End position',
    profileResistance: 'Resistance',
    profileForce: 'Force',
    profileTransition: 'Transition (ms)',
    profileDuration: 'Effect duration (ms)',
    profileMagnitude: 'Magnitude',
    controlRecord: 'Selected weapon control record',
    noControlRecord: 'This input set has no semantic control record.',
    comboNode: 'Combo node',
    nodeThreshold: 'Activation gate',
    nodeExpiry: 'Cancel / expiry (ms)',
    adaptiveOverride: 'Override adaptive profile',
    damage: 'Damage',
    cooldown: 'Cooldown (ms)',
    tapNoCooldown: 'Basic tap is always cooldown-free.',
    range: 'Range',
    attackRadius: 'Hitbox padding',
    arc: 'Arc (degrees)',
    duration: 'Duration (ms)',
    projectileSpeed: 'Projectile speed',
    pierce: 'Pierce count',
    knockback: 'Knockback',
    enabled: 'Gesture enabled',
    attackColor: 'Gesture / trace color',
    colliderShape: 'Collider shape',
    noCollider: 'No collider',
    innerRange: 'Inner dead zone',
    strictInnerRange: 'Exclude overlapping bodies',
    passesThroughWalls: 'Passes through walls',
    colliderWidth: 'Collider width',
    traceMs: 'Trace fade (ms)',
    chargeEnabled: 'Charge bands enabled',
    charge: 'Charge',
    chargeMax: 'Full charge (ms)',
    chargeBands: 'Charge bands',
    bandLabel: 'Band label',
    bandMin: 'Starts at (ms)',
    bandColor: 'Band color',
    bandDamage: 'Damage ×',
    bandRange: 'Range ×',
    bandKnockback: 'Knockback ×',
    addBand: 'Add band',
    removeBand: 'Remove band',
    augmentNames: {
      none: 'No augment',
      bleed: 'Bleed symbol',
      poison: 'Poison symbol',
      fire: 'Fire symbol',
      chemical: 'Chemical symbol',
      ouroborosAcid: 'Ouroboros Acid',
    },
    enemyHp: 'Health',
    enemyArmor: 'Armor',
    enemyDodge: 'Dodge chance',
    enemyRadius: 'Hit radius',
    enemySpeed: 'Move speed',
    enemyIdleTurn: 'Idle turn speed (rad/sec)',
    visionRange: 'Vision range',
    visionAngle: 'Vision angle',
    notice: 'Notice delay (ms)',
    alertPause: 'Alert pause (ms)',
    enemyAttackRange: 'Attack range',
    preferredAttackRange: 'Preferred range ratio',
    enemyDamage: 'Attack damage',
    enemyCooldown: 'Attack cooldown (ms)',
    windup: 'Attack wind-up (ms)',
    mentalPressure: 'Mental pressure / sec',
    knifeSpiderSettings: 'Knife-spider generation',
    knifeSpiderVersion: 'Behavior version',
    knifeSpiderV1: 'v1 · legacy leap',
    knifeSpiderV2: 'v2 · zigzag, orbit and reflection',
    reflectedDamage: 'Reflected damage ×',
    reflectedSpeed: 'Reflected speed ×',
    quickCapture: 'Normal pickup window (ms)',
    embeddedCapture: 'Embedded pickup window (ms)',
    reflectionSelfDamage: 'HP lost on reflection',
    impactSelfDamage: 'HP lost on impact',
    evadeChance: 'Attack evade chance',
    orbitDistance: 'Orbit distance',
    leapTriggerDistance: 'Leap trigger distance',
    gestureNames: {
      tap: 'Tap',
      doubleTap: 'Double tap',
      doubleTapHold: 'Double + hold',
      hold: 'Hold',
      holdThenDoubleTap: 'Hold + tap',
    },
  },
  ru: {
    eyebrow: 'Мастерская прототипа',
    title: 'Конструктор 99LC',
    subtitle: 'Настраивайте текущую конфигурацию без пересборки игры.',
    close: 'Закрыть конструктор',
    quick: 'Быстрая настройка',
    json: 'JSON целиком',
    valid: 'Конфигурация корректна',
    invalid: 'Нужны исправления',
    noConfig: 'Конфигурация ещё загружается.',
    validate: 'Проверить',
    apply: 'Применить в новой генерации',
    applyControls: 'Применить управление без перезапуска',
    save: 'Сохранить в браузере',
    clear: 'Очистить замену',
    import: 'Импорт JSON',
    export: 'Экспорт JSON',
    imported: 'Импортированная конфигурация готова к применению.',
    exported: 'Конфигурация экспортирована.',
    applied: 'Конфигурация применена в новой генерации.',
    saved: 'Замена сохранена в браузере; текущая попытка не изменена.',
    cleared: 'Замена в браузере очищена; текущая попытка не изменена.',
    parseError: 'JSON не удалось разобрать',
    validationErrors: 'Ошибки проверки',
    jsonHelp: 'Редактируйте полную конфигурацию. Проверьте её перед применением.',
    player: 'Игрок',
    playerHelp: 'Начальные параметры тела, рассудка и движения',
    chances: 'Шансы и истощение',
    chancesHelp: 'Цена смерти и постоянные потери выбранного уровня',
    input: 'Управление и тайминги',
    inputHelp: 'Распознавание DeepList, mylorik и DualSense',
    deepList: 'DeepList',
    mylorik: 'mylorik',
    dualSense: 'DualSense',
    attack: 'Выбранная атака',
    attackHelp: 'Один жест оружия за раз',
    moveDescription: 'Игровое описание мува',
    axeSettings: 'Двуручная секира',
    axeSettingsHelp: 'Усиление обычного тапа движением прицела по направлению взмаха',
    axeMotionBonus: 'Максимальный бонус мыши',
    axeMotionPixels: 'Путь мыши для максимума (px)',
    swordSettings: 'Меч наемника',
    swordSettingsHelp: 'Ритм, усталость, наступание, стаггер и продолжение',
    swordStaggerEnabled: 'Стаггер Zornhaw и Неудержимость включены',
    swordPerfectStart: 'Начало идеального тайминга (мс)',
    swordPerfectEnd: 'Конец идеального тайминга (мс)',
    swordFatigue: 'Длительность усталости (мс)',
    swordRoomMisses: 'Промахов за комнату до усталости',
    swordConsecutiveMisses: 'Промахов подряд до усталости',
    swordAdvanceDistance: 'Дистанция наступания',
    swordAdvanceSpeed: 'Скорость наступания',
    swordStagger: 'Стаггер за удар (мс)',
    swordStaggerThreshold: 'Порог Неудержимости (мс)',
    swordUnstoppable: 'Длительность Неудержимости (мс)',
    swordUnterhauHold: 'Удержание Unterhaw (мс)',
    swordUnterhauCooldown: 'Множитель отката Unterhaw',
    swordEmptyOffhand: 'Множитель урона с пустой второй рукой',
    loadout: 'Стартовая экипировка',
    loadoutHelp: 'Выберите два активных слота и символы-аугментации',
    moveQuestsEnabled: 'Квесты для открытия мувов включены',
    moveQuestKills: 'Убийств на квест мува',
    sameTierSacrifice: 'Здоровье после бокового пути',
    attackStopsMovement: 'Атаки мгновенно останавливают движение',
    minimumParry: 'Минимальное окно парирования (мс)',
    revealOnParry: 'Раскрытие врага после парирования (мс)',
    revealOnHit: 'Раскрытие врага после удара (мс)',
    minimumDamage: 'Минимальный получаемый урон',
    enemy: 'Выбранный враг',
    enemyHelp: 'Обнаружение, преследование и настройка атак',
    weapon: 'Оружие',
    primaryWeapon: 'Основное оружие',
    secondaryWeapon: 'Вторичное оружие',
    noSecondary: 'Пустой вторичный слот',
    noPrimary: 'Пустой основной слот',
    artifact: 'Артефакт',
    noArtifact: 'Без артефакта',
    outfit: 'Одежда',
    noOutfit: 'Без одежды',
    primaryAugment: 'Аугментация основного',
    secondaryAugment: 'Аугментация вторичного',
    attackSet: 'Набор ввода',
    primarySet: 'Основная кнопка',
    secondarySet: 'Вторая кнопка',
    gesture: 'Жест',
    enemySelect: 'Враг',
    tier: 'Уровень',
    maxHp: 'Физическое здоровье',
    maxMental: 'Ментальное здоровье',
    maxStamina: 'Стамина',
    moveSpeed: 'Скорость движения',
    acceleration: 'Разгон до полной скорости (мс)',
    deceleration: 'Торможение до остановки (мс)',
    armor: 'Броня',
    attackPower: 'Сила атаки',
    radius: 'Радиус тела',
    invulnerability: 'Неуязвимость после удара (мс)',
    chanceCount: 'Начальные Шансы',
    deathCost: 'Шансов за смерть',
    erosionHp: 'Истощение здоровья',
    erosionMental: 'Истощение рассудка',
    erosionSpeed: 'Истощение скорости',
    erosionArmor: 'Истощение брони',
    erosionAttack: 'Истощение атаки',
    doubleTap: 'Окно двойного нажатия (мс)',
    tapCombo: 'Окно продолжения базового комбо (мс)',
    hold: 'Порог задержки (мс)',
    holdMax: 'Предел задержки для комбинации (мс)',
    holdDouble: 'Окно повтора после задержки (мс)',
    aimDeadZone: 'Мёртвая зона прицела',
    actionDirectionDeadZone: 'Мёртвая зона направления мува',
    gamepadDeadZone: 'Мёртвая зона геймпада',
    gamepadLeftButton: 'Индекс основной кнопки',
    gamepadRightButton: 'Индекс вторичной кнопки',
    techniqueHold: 'Порог задержки техники (мс)',
    inputBuffer: 'Буфер одного намерения (мс)',
    continuationWindow: 'Окно продолжения (мс)',
    activationThreshold: 'Активация триггера',
    releaseThreshold: 'Отпускание триггера',
    hysteresis: 'Гистерезис триггера',
    shallowGate: 'Неглубокий гейт',
    mediumGate: 'Средний гейт',
    deepGate: 'Глубокий гейт',
    finalGate: 'Финальный гейт',
    feedbackCaps: 'Безопасные пределы отклика',
    maxMagnitude: 'Максимальная сила',
    maxDuration: 'Максимальный эффект (мс)',
    blockedRepeat: 'Интервал блок-сигнала (мс)',
    tactileProfile: 'Адаптивный профиль',
    profileStart: 'Начальная позиция',
    profileEnd: 'Конечная позиция',
    profileResistance: 'Сопротивление',
    profileForce: 'Сила',
    profileTransition: 'Переход (мс)',
    profileDuration: 'Длительность (мс)',
    profileMagnitude: 'Интенсивность',
    controlRecord: 'Запись управления выбранного оружия',
    noControlRecord: 'Для этого набора нет семантической записи управления.',
    comboNode: 'Нода комбо',
    nodeThreshold: 'Гейт активации',
    nodeExpiry: 'Отмена / истечение (мс)',
    adaptiveOverride: 'Переопределить адаптивный профиль',
    damage: 'Урон',
    cooldown: 'Откат (мс)',
    tapNoCooldown: 'У базового нажатия отката нет.',
    range: 'Дальность',
    attackRadius: 'Допуск хитбокса',
    arc: 'Дуга (градусы)',
    duration: 'Длительность (мс)',
    projectileSpeed: 'Скорость снаряда',
    pierce: 'Пробиваемые цели',
    knockback: 'Отбрасывание',
    enabled: 'Жест включён',
    attackColor: 'Цвет жеста / трассировки',
    colliderShape: 'Форма коллайдера',
    noCollider: 'Без коллайдера',
    innerRange: 'Внутренняя мёртвая зона',
    strictInnerRange: 'Не задевать тела в мёртвой зоне',
    passesThroughWalls: 'Проходит через стены',
    colliderWidth: 'Ширина коллайдера',
    traceMs: 'Затухание следа (мс)',
    chargeEnabled: 'Сектора зарядки включены',
    charge: 'Заряд',
    chargeMax: 'Полный заряд (мс)',
    chargeBands: 'Сектора зарядки',
    bandLabel: 'Название сектора',
    bandMin: 'Начало (мс)',
    bandColor: 'Цвет сектора',
    bandDamage: 'Урон ×',
    bandRange: 'Дальность ×',
    bandKnockback: 'Отбрасывание ×',
    addBand: 'Добавить сектор',
    removeBand: 'Удалить сектор',
    augmentNames: {
      none: 'Без аугментации',
      bleed: 'Символ кровотечения',
      poison: 'Символ яда',
      fire: 'Символ огня',
      chemical: 'Символ химикатов',
      ouroborosAcid: 'Кислота Уробороса',
    },
    enemyHp: 'Здоровье',
    enemyArmor: 'Броня',
    enemyDodge: 'Шанс уклонения',
    enemyRadius: 'Радиус попадания',
    enemySpeed: 'Скорость движения',
    enemyIdleTurn: 'Скорость поворота в покое (рад/с)',
    visionRange: 'Дальность зрения',
    visionAngle: 'Угол зрения',
    notice: 'Задержка обнаружения (мс)',
    alertPause: 'Пауза после тревоги (мс)',
    enemyAttackRange: 'Дальность атаки',
    preferredAttackRange: 'Доля предпочитаемой дистанции',
    enemyDamage: 'Урон атаки',
    enemyCooldown: 'Откат атаки (мс)',
    windup: 'Подготовка атаки (мс)',
    mentalPressure: 'Давление на рассудок / сек',
    knifeSpiderSettings: 'Поколение Ножа-паука',
    knifeSpiderVersion: 'Версия поведения',
    knifeSpiderV1: 'v1 · старый прыжок',
    knifeSpiderV2: 'v2 · зигзаги, орбита и отбивание',
    reflectedDamage: 'Урон отбитого ножа ×',
    reflectedSpeed: 'Скорость отбитого ножа ×',
    quickCapture: 'Обычное окно подбора (мс)',
    embeddedCapture: 'Окно подбора в препятствии (мс)',
    reflectionSelfDamage: 'Потеря HP при отбивании',
    impactSelfDamage: 'Потеря HP при столкновении',
    evadeChance: 'Шанс уклониться от атаки',
    orbitDistance: 'Дистанция кружения',
    leapTriggerDistance: 'Дистанция начала прыжка',
    gestureNames: {
      tap: 'Нажатие',
      doubleTap: 'Двойное нажатие',
      doubleTapHold: 'Двойное + задержка',
      hold: 'Задержка',
      holdThenDoubleTap: 'Задержка + нажатие',
    },
  },
} as const

const t = computed(() => copy[props.locale])
const tab = ref<BuilderTab>('quick')
const draft = ref<LastChancesConfig | null>(null)
const rawJson = ref('')
const rawError = ref('')
const notice = ref('')
const selectedWeaponIndex = ref(0)
const selectedAttackSet = ref<'primary' | 'secondary'>('primary')
const selectedGesture = ref<LastChancesGesture>('tap')
const selectedEnemyIndex = ref(0)
const selectedTierIndex = ref(0)
const selectedTactileProfile = ref<LastChancesTactileProfile>('click')
const fileInput = ref<HTMLInputElement | null>(null)
let syncingRaw = false
const attackBehaviorBeforeDisable = new WeakMap<LastChancesAttackDefinition, LastChancesAttackBehavior>()
type DisabledControlBinding = {
  activations: LastChancesMylorikActivationDefinition[]
  nodes: Array<{ index: number; node: LastChancesDualSenseComboNodeDefinition }>
  predecessorNext: Array<{ id: string; next: string[] }>
  startNodeId: string | null
}
const controlBindingBeforeDisable = new WeakMap<LastChancesAttackDefinition, DisabledControlBinding>()

type DualSenseGateName = 'shallow' | 'medium' | 'deep' | 'final'

function updateDualSenseGate(gate: DualSenseGateName, event: Event) {
  const config = draft.value
  const dualSense = config?.input.dualsense
  const value = Number((event.target as HTMLInputElement).value)
  if (!config || !dualSense || !Number.isFinite(value)) return

  const previous = dualSense.gatePositions[gate]
  const delta = value - previous
  for (const weapon of config.weapons) {
    const records = [weapon.controls?.primary, weapon.controls?.secondary]
    for (const record of records) {
      for (const node of record?.dualsense.nodes ?? []) {
        if (node.activationThreshold === previous) node.activationThreshold = value
      }
      for (const tick of record?.dualsense.haptics?.depthTicks ?? []) {
        if (Math.abs(tick.position - value) < 0.03) {
          tick.position = Math.min(1, Math.max(0, tick.position + delta))
        }
      }
    }
  }
  dualSense.gatePositions[gate] = value
}

const validation = computed(() => draft.value
  ? validateLastChancesConfig(draft.value)
  : { valid: false, errors: [t.value.noConfig] })
const selectedWeapon = computed(() => draft.value?.weapons[selectedWeaponIndex.value] ?? null)
const selectedAttack = computed(() => {
  const weapon = selectedWeapon.value
  if (!weapon) return null
  const attacks = selectedAttackSet.value === 'secondary'
    ? weapon.secondaryAttacks
    : weapon.attacks
  return attacks?.[selectedGesture.value] ?? null
})
const selectedEnemy = computed(() => draft.value?.enemies[selectedEnemyIndex.value] ?? null)
const selectedTier = computed(() => draft.value?.progression.tiers[selectedTierIndex.value] ?? null)
const selectedAdaptiveProfile = computed(() => (
  draft.value?.input.dualsense?.feedback.profiles[selectedTactileProfile.value] ?? null
))
const selectedControlDefinition = computed(() => {
  const controls = selectedWeapon.value?.controls
  if (!controls) return null
  return selectedAttackSet.value === 'secondary'
    ? controls.secondary ?? null
    : controls.primary
})
const primaryLoadoutWeapons = computed(() => draft.value?.weapons.filter(weapon => (
  (weapon.equipMode ?? (weapon.hand === 'right' ? 'secondaryOnly' : 'primaryOnly')) !== 'secondaryOnly'
)) ?? [])
const secondaryLoadoutWeapons = computed(() => draft.value?.weapons.filter((weapon) => {
  const mode = weapon.equipMode ?? (weapon.hand === 'right' ? 'secondaryOnly' : 'primaryOnly')
  return mode === 'secondaryOnly' || mode === 'eitherHand'
}) ?? [])
const selectedPrimaryLoadoutWeapon = computed(() => draft.value?.weapons.find(
  weapon => weapon.id === draft.value?.loadout?.primaryWeaponId,
) ?? null)
const selectedSecondaryLoadoutWeapon = computed(() => draft.value?.weapons.find(
  weapon => weapon.id === draft.value?.loadout?.secondaryWeaponId,
) ?? null)
function supportedAugments(weapon: LastChancesWeaponDefinition | null): LastChancesAugment[] {
  return LAST_CHANCES_AUGMENTS.filter(augment => (
    augment === 'none' || augment === 'ouroborosAcid'
      || weapon?.augmentHooks?.[augment] !== undefined
  ))
}
const primaryAugmentOptions = computed(() => supportedAugments(selectedPrimaryLoadoutWeapon.value))
const secondaryAugmentOptions = computed(() => secondaryAugmentInherited.value
  ? primaryAugmentOptions.value
  : supportedAugments(selectedSecondaryLoadoutWeapon.value))
const secondarySlotLocked = computed(() => (
  selectedPrimaryLoadoutWeapon.value?.equipMode === 'twoHanded'
))
const secondaryAugmentInherited = computed(() => {
  const mode = selectedPrimaryLoadoutWeapon.value?.equipMode
  return mode === 'twoHanded'
    || (mode === 'hybrid' && !draft.value?.loadout?.secondaryWeaponId)
})

function loadDraft(config: LastChancesConfig) {
  draft.value = cloneLastChancesConfig(config)
  draft.value.progression.moveQuestsEnabled ??= true
  const knifeSpider = draft.value.enemies.find(enemy => enemy.id === 'spider-knife')
  if (knifeSpider) {
    knifeSpider.tuning ??= {}
    const defaults: Record<string, number> = {
      behaviorVersion: 2,
      reflectedDamageMultiplier: 4,
      reflectedSpeedMultiplier: 1.45,
      quickCaptureWindowMs: 170,
      embeddedCaptureWindowMs: 2200,
      reflectionSelfDamageRatio: 0.1,
      impactSelfDamageRatio: 0.1,
      evadeChance: 0.68,
      orbitDistance: 82,
      leapTriggerDistance: 220,
    }
    for (const [key, value] of Object.entries(defaults)) knifeSpider.tuning[key] ??= value
  }
  syncingRaw = true
  rawJson.value = JSON.stringify(draft.value, null, 2)
  rawError.value = ''
  notice.value = ''
  syncingRaw = false
  selectedWeaponIndex.value = Math.min(selectedWeaponIndex.value, Math.max(0, config.weapons.length - 1))
  if (!config.weapons[selectedWeaponIndex.value]?.secondaryAttacks
    || config.weapons[selectedWeaponIndex.value]?.primaryHandOnly === true) {
    selectedAttackSet.value = 'primary'
  }
  selectedEnemyIndex.value = Math.min(selectedEnemyIndex.value, Math.max(0, config.enemies.length - 1))
  selectedTierIndex.value = Math.min(selectedTierIndex.value, Math.max(0, config.progression.tiers.length - 1))
  normalizeLoadoutAugments()
}

watch(selectedWeapon, (weapon) => {
  if (!weapon?.secondaryAttacks || weapon.primaryHandOnly === true) selectedAttackSet.value = 'primary'
})

watch(() => [
  draft.value?.loadout?.primaryAugment,
  draft.value?.loadout?.secondaryWeaponId,
  selectedPrimaryLoadoutWeapon.value?.id,
], normalizeLoadoutAugments)

function normalizeLoadoutForPrimary() {
  if (!draft.value?.loadout) return
  if (secondarySlotLocked.value) draft.value.loadout.secondaryWeaponId = null
  normalizeLoadoutAugments()
}

function normalizeSecondaryAugment() {
  if (!draft.value?.loadout || !secondaryAugmentInherited.value) return
  draft.value.loadout.secondaryAugment = draft.value.loadout.primaryAugment
}

function normalizeLoadoutAugments() {
  const loadout = draft.value?.loadout
  if (!loadout) return
  if (!primaryAugmentOptions.value.includes(loadout.primaryAugment ?? 'none')) {
    loadout.primaryAugment = 'none'
  }
  if (secondaryAugmentInherited.value) {
    loadout.secondaryAugment = loadout.primaryAugment
    return
  }
  if (!secondaryAugmentOptions.value.includes(loadout.secondaryAugment ?? 'none')) {
    loadout.secondaryAugment = 'none'
  }
}

function setAttackEnabled(event: Event) {
  const attack = selectedAttack.value
  if (!attack) return
  const enabled = (event.target as HTMLInputElement).checked
  if (!enabled) {
    if (selectedGesture.value === 'tap') return
    if (attack.behavior && attack.behavior !== 'disabled') {
      attackBehaviorBeforeDisable.set(attack, attack.behavior)
    }
    removeSelectedControlBinding(attack)
    attack.enabled = false
    attack.behavior = 'disabled'
    return
  }
  attack.enabled = true
  if (attack.behavior === 'disabled') {
    attack.behavior = attackBehaviorBeforeDisable.get(attack) ?? 'standard'
  }
  attack.collider ??= {
    shape: attack.kind === 'melee'
      ? 'sector'
      : attack.kind === 'burst' ? 'circle' : 'capsule',
    traceMs: 600,
    passesThroughWalls: false,
  }
  restoreSelectedControlBinding(attack)
}

function removeSelectedControlBinding(attack: LastChancesAttackDefinition) {
  const controls = selectedControlDefinition.value
  if (!controls) return
  const gesture = selectedGesture.value
  const activations = controls.mylorik.activations
    .filter(activation => activation.gesture === gesture)
    .map(activation => ({ ...activation }))
  controls.mylorik.activations = controls.mylorik.activations
    .filter(activation => activation.gesture !== gesture)

  const removedIds = new Set(
    controls.dualsense.nodes
      .filter(node => node.gesture === gesture)
      .map(node => node.id),
  )
  const nodes = controls.dualsense.nodes.flatMap((node, index) => (
    removedIds.has(node.id)
      ? [{ index, node: JSON.parse(JSON.stringify(node)) as LastChancesDualSenseComboNodeDefinition }]
      : []
  ))
  const predecessorNext = controls.dualsense.nodes.flatMap(node => (
    node.next.some(id => removedIds.has(id))
      ? [{ id: node.id, next: [...node.next] }]
      : []
  ))
  const removedNodes = new Map(nodes.map(entry => [entry.node.id, entry.node]))
  const expandNext = (id: string, seen = new Set<string>()): string[] => {
    if (!removedIds.has(id)) return [id]
    if (seen.has(id)) return []
    seen.add(id)
    return removedNodes.get(id)?.next.flatMap(next => expandNext(next, new Set(seen))) ?? []
  }
  controls.dualsense.nodes = controls.dualsense.nodes
    .filter(node => !removedIds.has(node.id))
    .map(node => ({
      ...node,
      next: [...new Set(node.next.flatMap(id => expandNext(id)))],
    }))
  const startNodeId = controls.dualsense.startNodeId
  if (startNodeId && removedIds.has(startNodeId)) {
    controls.dualsense.startNodeId = expandNext(startNodeId)[0]
      ?? controls.dualsense.nodes[0]?.id
      ?? null
  }
  controlBindingBeforeDisable.set(attack, {
    activations,
    nodes,
    predecessorNext,
    startNodeId,
  })
}

function genericActivation(
  controls: LastChancesAttackSetControlDefinition,
  gesture: LastChancesGesture,
): LastChancesMylorikActivationDefinition {
  const base = gesture === 'doubleTap'
    ? { intent: 'technique' as const, phase: 'tap' as const }
    : gesture === 'hold'
      ? { intent: 'technique' as const, phase: 'hold' as const }
      : gesture === 'holdThenDoubleTap'
        ? { intent: 'mobility' as const, phase: 'press' as const, context: 'continuation' as const }
        : { intent: 'technique' as const, phase: 'hold' as const, context: 'continuation' as const }
  let priority = 50
  while (controls.mylorik.activations.some(activation => (
    activation.intent === base.intent
    && activation.phase === base.phase
    && activation.context === ('context' in base ? base.context : undefined)
    && activation.priority === priority
  ))) priority -= 1
  return { gesture, ...base, priority }
}

function genericDualSenseNode(
  controls: LastChancesAttackSetControlDefinition,
  gesture: LastChancesGesture,
): LastChancesDualSenseComboNodeDefinition {
  const gatePositions = draft.value?.input.dualsense?.gatePositions
  const threshold = gesture === 'doubleTap'
    ? gatePositions?.shallow
    : gesture === 'hold'
      ? gatePositions?.medium
      : gesture === 'doubleTapHold' ? gatePositions?.deep : gatePositions?.final
  const usedIds = new Set(controls.dualsense.nodes.map(node => node.id))
  let id = gesture
  let suffix = 2
  while (usedIds.has(id)) id = `${gesture}-${suffix++}`
  return {
    id,
    gesture,
    entryContext: 'neutral',
    activationThreshold: threshold ?? 0.48,
    dispatch: 'release',
    holdBehavior: gesture === 'hold' || gesture === 'doubleTapHold' ? 'charge' : 'none',
    releaseBehavior: 'dispatch',
    next: [],
    cancel: 'release',
    expiryMs: Math.max(1, draft.value?.input.mylorik?.continuationWindowMs ?? 480),
    tactileProfile: gesture === 'hold' || gesture === 'doubleTapHold' ? 'ramp' : 'click',
  }
}

function restoreSelectedControlBinding(attack: LastChancesAttackDefinition) {
  const controls = selectedControlDefinition.value
  if (!controls) return
  const gesture = selectedGesture.value
  const stored = controlBindingBeforeDisable.get(attack)
  const activations = stored?.activations.length
    ? stored.activations.map(activation => ({ ...activation }))
    : [genericActivation(controls, gesture)]
  controls.mylorik.activations.push(...activations)

  if (stored?.nodes.length) {
    const chargeBands = attack.charge?.bands ?? []
    const chargeBandIds = new Set(chargeBands.map(band => band.id))
    let fallbackBandIndex = 0
    for (const entry of [...stored.nodes].sort((left, right) => left.index - right.index)) {
      const restoredNode = JSON.parse(
        JSON.stringify(entry.node),
      ) as LastChancesDualSenseComboNodeDefinition
      if (restoredNode.chargeBandOverrideId
        && !chargeBandIds.has(restoredNode.chargeBandOverrideId)) {
        const fallback = chargeBands[Math.min(fallbackBandIndex, chargeBands.length - 1)]
        if (fallback) restoredNode.chargeBandOverrideId = fallback.id
        else delete restoredNode.chargeBandOverrideId
      }
      if (restoredNode.chargeBandOverrideId) fallbackBandIndex += 1
      controls.dualsense.nodes.splice(
        Math.min(entry.index, controls.dualsense.nodes.length),
        0,
        restoredNode,
      )
    }
    const validIds = new Set(controls.dualsense.nodes.map(node => node.id))
    controls.dualsense.nodes.forEach((node) => {
      node.next = node.next.filter(id => validIds.has(id))
    })
    stored.predecessorNext.forEach((predecessor) => {
      const node = controls.dualsense.nodes.find(candidate => candidate.id === predecessor.id)
      if (node) node.next = predecessor.next.filter(id => validIds.has(id))
    })
    if (stored.startNodeId && validIds.has(stored.startNodeId)) {
      controls.dualsense.startNodeId = stored.startNodeId
    }
    return
  }

  const node = genericDualSenseNode(controls, gesture)
  const previous = controls.dualsense.nodes.at(-1)
  if (previous && !previous.next.includes(node.id)) previous.next.push(node.id)
  controls.dualsense.nodes.push(node)
  controls.dualsense.startNodeId ??= node.id
}

function setColliderShape(event: Event) {
  const current = selectedAttack.value
  if (!current) return
  const shape = (event.target as HTMLSelectElement).value
  if (!shape) {
    delete current.collider
    return
  }
  current.collider = {
    ...(current.collider ?? { traceMs: 600, passesThroughWalls: false }),
    shape: shape as typeof LAST_CHANCES_COLLIDER_SHAPES[number],
  }
}

function toggleCharge(event: Event) {
  const current = selectedAttack.value
  if (!current) return
  if (!(event.target as HTMLInputElement).checked) {
    delete current.charge
    return
  }
  current.charge ??= {
    maxMs: Math.max(draft.value?.input.holdMaxMs ?? 1000, draft.value?.input.holdMs ?? 650),
    bands: [{
      id: 'charge-1',
      label: t.value.charge,
      minMs: draft.value?.input.holdMs ?? 650,
      color: current.color,
    }],
  }
}

function addChargeBand() {
  const current = selectedAttack.value
  if (!current?.charge) return
  const previous = current.charge.bands.at(-1)
  const minMs = Math.max((previous?.minMs ?? 0) + 100, draft.value?.input.holdMs ?? 650)
  if (minMs > current.charge.maxMs) current.charge.maxMs = minMs
  const usedIds = new Set(current.charge.bands.map(band => band.id))
  let suffix = 1
  while (usedIds.has(`charge-${suffix}`)) suffix += 1
  current.charge.bands.push({
    id: `charge-${suffix}`,
    label: `${t.value.charge} ${suffix}`,
    minMs,
    color: current.color,
  })
}

function removeChargeBand(index: number) {
  selectedAttack.value?.charge?.bands.splice(index, 1)
}

function augmentLabel(augment: LastChancesAugment): string {
  return t.value.augmentNames[augment]
}

watch(() => props.config, (config) => {
  if (config) loadDraft(config)
}, { immediate: true })

watch(draft, (value) => {
  if (!value || syncingRaw || tab.value === 'json') return
  rawJson.value = JSON.stringify(value, null, 2)
}, { deep: true })

function parseRaw(): LastChancesConfig | null {
  try {
    const value = migrateLastChancesConfig(
      JSON.parse(rawJson.value) as unknown,
      props.config ?? undefined,
    )
    const result = validateLastChancesConfig(value)
    if (!result.valid) {
      rawError.value = result.errors.join('\n')
      return null
    }
    rawError.value = ''
    return value as LastChancesConfig
  } catch (error) {
    rawError.value = `${t.value.parseError}: ${error instanceof Error ? error.message : String(error)}`
    return null
  }
}

function validateRaw() {
  const value = parseRaw()
  if (!value) return
  syncingRaw = true
  draft.value = cloneLastChancesConfig(value)
  rawJson.value = JSON.stringify(value, null, 2)
  syncingRaw = false
  notice.value = t.value.valid
}

function switchTab(nextTab: BuilderTab) {
  if (tab.value === 'json' && nextTab === 'quick') {
    const value = parseRaw()
    if (!value) return
    syncingRaw = true
    draft.value = cloneLastChancesConfig(value)
    rawJson.value = JSON.stringify(value, null, 2)
    syncingRaw = false
  }
  if (nextTab === 'json' && draft.value) rawJson.value = JSON.stringify(draft.value, null, 2)
  tab.value = nextTab
}

function currentValidDraft(): LastChancesConfig | null {
  if (tab.value === 'json') return parseRaw()
  if (!draft.value) return null
  const result = validateLastChancesConfig(draft.value)
  if (!result.valid) {
    rawError.value = result.errors.join('\n')
    return null
  }
  rawError.value = ''
  return cloneLastChancesConfig(draft.value)
}

function apply() {
  const value = currentValidDraft()
  if (!value) return
  emit('apply', value)
  notice.value = t.value.applied
}

function applyControls() {
  const value = currentValidDraft()
  if (!value) return
  emit('applyControls', value)
  notice.value = t.value.applyControls
}

function nodeAttackName(node: LastChancesDualSenseComboNodeDefinition): string {
  const weapon = selectedWeapon.value
  const attacks = selectedAttackSet.value === 'secondary'
    ? weapon?.secondaryAttacks
    : weapon?.attacks
  return attacks?.[node.gesture]?.name ?? node.gesture
}

function toggleNodeAdaptiveOverride(
  node: LastChancesDualSenseComboNodeDefinition,
  event: Event,
) {
  const enabled = (event.target as HTMLInputElement).checked
  if (!enabled) {
    delete node.adaptiveOverride
    return
  }
  const profile = draft.value?.input.dualsense?.feedback.profiles[node.tactileProfile]
  node.adaptiveOverride = profile ? { ...profile } : {}
}

function save() {
  const value = currentValidDraft()
  if (!value) return
  emit('save', value)
  notice.value = t.value.saved
}

function clearOverride() {
  emit('clear')
  notice.value = t.value.cleared
}

async function importJson(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  rawJson.value = await file.text()
  tab.value = 'json'
  const value = parseRaw()
  if (value) {
    syncingRaw = true
    draft.value = cloneLastChancesConfig(value)
    rawJson.value = JSON.stringify(value, null, 2)
    syncingRaw = false
    notice.value = t.value.imported
  }
  input.value = ''
  await nextTick()
}

function exportJson() {
  const value = currentValidDraft()
  if (!value) return
  const blob = new Blob([`${JSON.stringify(value, null, 2)}\n`], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `99lc-${value.seed || 'config'}.json`
  link.click()
  URL.revokeObjectURL(url)
  notice.value = t.value.exported
}
</script>

<template>
  <Transition name="lc-builder-backdrop">
    <div v-if="open" class="lc-builder-backdrop" @click.self="emit('close')">
      <aside class="lc-builder" role="dialog" aria-modal="true" :aria-label="t.title" @keydown.esc="emit('close')">
        <header class="lc-builder-header">
          <div class="lc-builder-brand"><FlaskConical :size="20" aria-hidden="true" /></div>
          <div>
            <p>{{ t.eyebrow }}</p>
            <h2>{{ t.title }}</h2>
            <span>{{ t.subtitle }}</span>
          </div>
          <button type="button" :aria-label="t.close" :title="t.close" @click="emit('close')">
            <X :size="20" aria-hidden="true" />
          </button>
        </header>

        <template v-if="draft">
          <div class="lc-builder-tabs" role="tablist">
            <button
              type="button"
              role="tab"
              :aria-selected="tab === 'quick'"
              :class="{ active: tab === 'quick' }"
              @click="switchTab('quick')"
            >
              <SlidersHorizontal :size="15" aria-hidden="true" />{{ t.quick }}
            </button>
            <button
              type="button"
              role="tab"
              :aria-selected="tab === 'json'"
              :class="{ active: tab === 'json' }"
              @click="switchTab('json')"
            >
              <Braces :size="15" aria-hidden="true" />{{ t.json }}
            </button>
          </div>

          <div class="lc-builder-status" :class="validation.valid && !rawError ? 'is-valid' : 'is-invalid'" aria-live="polite">
            <CheckCircle2 v-if="validation.valid && !rawError" :size="15" aria-hidden="true" />
            <XCircle v-else :size="15" aria-hidden="true" />
            <span>{{ validation.valid && !rawError ? t.valid : t.invalid }}</span>
            <small>{{ draft.seed }}</small>
          </div>

          <div class="lc-builder-body">
            <div v-if="tab === 'quick'" class="lc-quick-fields">
              <fieldset>
                <legend><span><Gauge :size="15" aria-hidden="true" />{{ t.player }}</span><small>{{ t.playerHelp }}</small></legend>
                <div class="lc-fields-grid">
                  <label>{{ t.maxHp }}<input v-model.number="draft.player.baseStats.maxHp" type="number" min="1" step="1" /></label>
                  <label>{{ t.maxMental }}<input v-model.number="draft.player.baseStats.maxMentalHealth" type="number" min="1" step="1" /></label>
                  <label>{{ t.maxStamina }}<input v-model.number="draft.player.baseStats.maxStamina" type="number" min="1" step="1" /></label>
                  <label>{{ t.moveSpeed }}<input v-model.number="draft.player.baseStats.moveSpeed" type="number" min="1" step="1" /></label>
                  <label>{{ t.acceleration }}<input v-model.number="draft.player.accelerationMs" type="number" min="1" step="5" /></label>
                  <label>{{ t.deceleration }}<input v-model.number="draft.player.decelerationMs" type="number" min="1" step="5" /></label>
                  <label>{{ t.armor }}<input v-model.number="draft.player.baseStats.armor" type="number" min="0" step="1" /></label>
                  <label>{{ t.attackPower }}<input v-model.number="draft.player.baseStats.attackPower" type="number" min="1" step="1" /></label>
                  <label>{{ t.radius }}<input v-model.number="draft.player.radius" type="number" min="1" step="1" /></label>
                  <label>{{ t.invulnerability }}<input v-model.number="draft.player.invulnerabilityMs" type="number" min="0" step="25" /></label>
                  <label class="lc-check-field">
                    <input v-model="draft.combat.attackStopsMovement" type="checkbox" />
                    <span>{{ t.attackStopsMovement }}</span>
                  </label>
                  <label>{{ t.minimumParry }}<input v-model.number="draft.combat.minimumPlayerParryMs" type="number" min="1" step="10" /></label>
                  <label>{{ t.revealOnParry }}<input v-model.number="draft.combat.enemyRevealOnParryMs" type="number" min="0" step="50" /></label>
                  <label>{{ t.revealOnHit }}<input v-model.number="draft.combat.enemyRevealOnHitMs" type="number" min="0" step="50" /></label>
                  <label>{{ t.minimumDamage }}<input v-model.number="draft.combat.minimumPlayerDamageTaken" type="number" min="0" step="0.25" /></label>
                </div>
              </fieldset>

              <fieldset>
                <legend><span><RotateCcw :size="15" aria-hidden="true" />{{ t.chances }}</span><small>{{ t.chancesHelp }}</small></legend>
                <div class="lc-fields-grid">
                  <label>{{ t.chanceCount }}<input v-model.number="draft.chances" type="number" min="1" step="1" /></label>
                  <label class="lc-check-field">
                    <input v-model="draft.progression.moveQuestsEnabled" type="checkbox" />
                    <span>{{ t.moveQuestsEnabled }}</span>
                  </label>
                  <label>{{ t.moveQuestKills }}<input v-model.number="draft.progression.moveQuestKillsRequired" type="number" min="1" step="1" /></label>
                  <label>{{ t.sameTierSacrifice }}<input v-model.number="draft.progression.sameTierSacrificeRatio" type="number" min="0.01" max="1" step="0.05" /></label>
                  <label>{{ t.tier }}
                    <select v-model.number="selectedTierIndex">
                      <option v-for="(tier, index) in draft.progression.tiers" :key="tier.id" :value="index">{{ index + 1 }} · {{ tier.label }}</option>
                    </select>
                  </label>
                  <template v-if="selectedTier">
                    <label>{{ t.deathCost }}<input v-model.number="selectedTier.deathCost" type="number" min="1" step="1" /></label>
                    <label>{{ t.erosionHp }}<input v-model.number="selectedTier.erosion.maxHp" type="number" min="0" step="1" /></label>
                    <label>{{ t.erosionMental }}<input v-model.number="selectedTier.erosion.maxMentalHealth" type="number" min="0" step="1" /></label>
                    <label>{{ t.erosionSpeed }}<input v-model.number="selectedTier.erosion.moveSpeed" type="number" min="0" step="1" /></label>
                    <label>{{ t.erosionArmor }}<input v-model.number="selectedTier.erosion.armor" type="number" min="0" step="1" /></label>
                    <label>{{ t.erosionAttack }}<input v-model.number="selectedTier.erosion.attackPower" type="number" min="0" step="1" /></label>
                  </template>
                </div>
              </fieldset>

              <fieldset>
                <legend><span><Zap :size="15" aria-hidden="true" />{{ t.input }}</span><small>{{ t.inputHelp }}</small></legend>
                <section class="lc-control-tuning">
                  <h3>{{ t.deepList }}</h3>
                  <div class="lc-fields-grid">
                    <label>{{ t.doubleTap }}<input v-model.number="draft.input.doubleTapMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.tapCombo }}<input v-model.number="draft.input.tapComboWindowMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.hold }}<input v-model.number="draft.input.holdMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.holdMax }}<input v-model.number="draft.input.holdMaxMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.holdDouble }}<input v-model.number="draft.input.holdThenDoubleTapWindowMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.aimDeadZone }}<input v-model.number="draft.input.aimDeadZone" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.actionDirectionDeadZone }}<input v-model.number="draft.input.actionDirectionDeadZone" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.gamepadDeadZone }}<input v-model.number="draft.input.gamepadDeadZone" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.gamepadLeftButton }}<input v-model.number="draft.input.gamepadLeftButton" type="number" min="0" max="31" step="1" /></label>
                    <label>{{ t.gamepadRightButton }}<input v-model.number="draft.input.gamepadRightButton" type="number" min="0" max="31" step="1" /></label>
                  </div>
                </section>

                <section v-if="draft.input.mylorik" class="lc-control-tuning">
                  <h3>{{ t.mylorik }}</h3>
                  <div class="lc-fields-grid">
                    <label>{{ t.techniqueHold }}<input v-model.number="draft.input.mylorik.techniqueHoldMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.inputBuffer }}<input v-model.number="draft.input.mylorik.bufferMs" type="number" min="0" step="10" /></label>
                    <label>{{ t.continuationWindow }}<input v-model.number="draft.input.mylorik.continuationWindowMs" type="number" min="1" step="10" /></label>
                  </div>
                </section>

                <section v-if="draft.input.dualsense" class="lc-control-tuning">
                  <h3>{{ t.dualSense }}</h3>
                  <div class="lc-fields-grid">
                    <label>{{ t.activationThreshold }}<input v-model.number="draft.input.dualsense.activationThreshold" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.releaseThreshold }}<input v-model.number="draft.input.dualsense.releaseThreshold" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.hysteresis }}<input v-model.number="draft.input.dualsense.hysteresis" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.shallowGate }}<input :value="draft.input.dualsense.gatePositions.shallow" type="number" min="0" max="1" step="0.01" @input="updateDualSenseGate('shallow', $event)" /></label>
                    <label>{{ t.mediumGate }}<input :value="draft.input.dualsense.gatePositions.medium" type="number" min="0" max="1" step="0.01" @input="updateDualSenseGate('medium', $event)" /></label>
                    <label>{{ t.deepGate }}<input :value="draft.input.dualsense.gatePositions.deep" type="number" min="0" max="1" step="0.01" @input="updateDualSenseGate('deep', $event)" /></label>
                    <label>{{ t.finalGate }}<input :value="draft.input.dualsense.gatePositions.final" type="number" min="0" max="1" step="0.01" @input="updateDualSenseGate('final', $event)" /></label>
                  </div>
                  <h4>{{ t.feedbackCaps }}</h4>
                  <div class="lc-fields-grid">
                    <label>{{ t.maxMagnitude }}<input v-model.number="draft.input.dualsense.feedback.maxMagnitude" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.maxDuration }}<input v-model.number="draft.input.dualsense.feedback.maxDurationMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.blockedRepeat }}<input v-model.number="draft.input.dualsense.feedback.blockedRepeatMs" type="number" min="0" step="10" /></label>
                  </div>
                  <div class="lc-profile-picker">
                    <label>{{ t.tactileProfile }}
                      <select v-model="selectedTactileProfile">
                        <option v-for="profile in LAST_CHANCES_TACTILE_PROFILES" :key="profile" :value="profile">{{ profile }}</option>
                      </select>
                    </label>
                  </div>
                  <div v-if="selectedAdaptiveProfile" class="lc-fields-grid">
                    <label>{{ t.profileStart }}<input v-model.number="selectedAdaptiveProfile.startPosition" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.profileEnd }}<input v-model.number="selectedAdaptiveProfile.endPosition" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.profileResistance }}<input v-model.number="selectedAdaptiveProfile.resistance" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.profileForce }}<input v-model.number="selectedAdaptiveProfile.force" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.profileTransition }}<input v-model.number="selectedAdaptiveProfile.transitionMs" type="number" min="0" step="10" /></label>
                    <label>{{ t.profileDuration }}<input v-model.number="selectedAdaptiveProfile.effectMs" type="number" min="0" step="10" /></label>
                    <label>{{ t.profileMagnitude }}<input v-model.number="selectedAdaptiveProfile.magnitude" type="number" min="0" max="1" step="0.01" /></label>
                  </div>
                  <div class="lc-control-record-editor">
                    <h4>
                      {{ t.controlRecord }}
                      <template v-if="selectedWeapon">
                        · {{ selectedWeapon.name }} · {{ selectedAttackSet === 'primary' ? t.primarySet : t.secondarySet }}
                      </template>
                    </h4>
                    <p v-if="!selectedControlDefinition">{{ t.noControlRecord }}</p>
                    <article
                      v-for="node in selectedControlDefinition?.dualsense.nodes ?? []"
                      :key="node.id"
                      class="lc-combo-node-editor"
                    >
                      <header>
                        <strong>{{ t.comboNode }} · {{ node.id }}</strong>
                        <span>{{ nodeAttackName(node) }} · {{ node.tactileProfile }}</span>
                      </header>
                      <div class="lc-fields-grid">
                        <label>{{ t.nodeThreshold }}<input v-model.number="node.activationThreshold" type="number" min="0" max="1" step="0.01" /></label>
                        <label>{{ t.nodeExpiry }}<input v-model.number="node.expiryMs" type="number" min="0" step="10" /></label>
                      </div>
                      <label class="lc-check-field">
                        <input
                          type="checkbox"
                          :checked="!!node.adaptiveOverride"
                          @change="toggleNodeAdaptiveOverride(node, $event)"
                        />
                        <span>{{ t.adaptiveOverride }}</span>
                      </label>
                      <div v-if="node.adaptiveOverride" class="lc-fields-grid">
                        <label>{{ t.profileStart }}<input v-model.number="node.adaptiveOverride.startPosition" type="number" min="0" max="1" step="0.01" /></label>
                        <label>{{ t.profileEnd }}<input v-model.number="node.adaptiveOverride.endPosition" type="number" min="0" max="1" step="0.01" /></label>
                        <label>{{ t.profileResistance }}<input v-model.number="node.adaptiveOverride.resistance" type="number" min="0" max="1" step="0.01" /></label>
                        <label>{{ t.profileForce }}<input v-model.number="node.adaptiveOverride.force" type="number" min="0" max="1" step="0.01" /></label>
                        <label>{{ t.profileTransition }}<input v-model.number="node.adaptiveOverride.transitionMs" type="number" min="0" step="10" /></label>
                        <label>{{ t.profileDuration }}<input v-model.number="node.adaptiveOverride.effectMs" type="number" min="0" step="10" /></label>
                        <label>{{ t.profileMagnitude }}<input v-model.number="node.adaptiveOverride.magnitude" type="number" min="0" max="1" step="0.01" /></label>
                      </div>
                    </article>
                  </div>
                </section>
              </fieldset>

              <fieldset v-if="draft.loadout">
                <legend><span><SlidersHorizontal :size="15" aria-hidden="true" />{{ t.loadout }}</span><small>{{ t.loadoutHelp }}</small></legend>
                <div class="lc-fields-grid">
                  <label>{{ t.primaryWeapon }}
                    <select
                      v-model="draft.loadout.primaryWeaponId"
                      data-testid="builder-primary-loadout"
                      @change="normalizeLoadoutForPrimary"
                    >
                      <option :value="null">{{ t.noPrimary }}</option>
                      <option v-for="weapon in primaryLoadoutWeapons" :key="weapon.id" :value="weapon.id">{{ weapon.name }}</option>
                    </select>
                  </label>
                  <label>{{ t.secondaryWeapon }}
                    <select
                      v-model="draft.loadout.secondaryWeaponId"
                      data-testid="builder-secondary-loadout"
                      :disabled="secondarySlotLocked"
                      @change="normalizeLoadoutAugments"
                    >
                      <option :value="null">{{ t.noSecondary }}</option>
                      <option v-for="weapon in secondaryLoadoutWeapons" :key="weapon.id" :value="weapon.id">{{ weapon.name }}</option>
                    </select>
                  </label>
                  <label>{{ t.primaryAugment }}
                    <select v-model="draft.loadout.primaryAugment" @change="normalizeLoadoutAugments">
                      <option v-for="augment in primaryAugmentOptions" :key="augment" :value="augment">{{ augmentLabel(augment) }}</option>
                    </select>
                  </label>
                  <label>{{ t.secondaryAugment }}
                    <select v-model="draft.loadout.secondaryAugment" :disabled="secondaryAugmentInherited || !draft.loadout.secondaryWeaponId">
                      <option v-for="augment in secondaryAugmentOptions" :key="augment" :value="augment">{{ augmentLabel(augment) }}</option>
                    </select>
                  </label>
                  <label>{{ t.artifact }}
                    <select v-model="draft.loadout.artifactId">
                      <option :value="null">{{ t.noArtifact }}</option>
                      <option v-for="artifact in draft.artifacts ?? []" :key="artifact.id" :value="artifact.id">{{ artifact.name }}</option>
                    </select>
                  </label>
                  <label>{{ t.outfit }}
                    <select v-model="draft.loadout.outfitId">
                      <option :value="null">{{ t.noOutfit }}</option>
                      <option v-for="outfit in draft.outfits ?? []" :key="outfit.id" :value="outfit.id">{{ outfit.name }}</option>
                    </select>
                  </label>
                </div>
              </fieldset>

              <fieldset>
                <legend><span><SlidersHorizontal :size="15" aria-hidden="true" />{{ t.attack }}</span><small>{{ t.attackHelp }}</small></legend>
                <div class="lc-fields-grid lc-select-row">
                  <label>{{ t.weapon }}
                    <select v-model.number="selectedWeaponIndex">
                      <option v-for="(weapon, index) in draft.weapons" :key="weapon.id" :value="index">{{ weapon.name }}</option>
                    </select>
                  </label>
                  <label v-if="selectedWeapon?.secondaryAttacks && selectedWeapon.primaryHandOnly !== true">{{ t.attackSet }}
                    <select v-model="selectedAttackSet">
                      <option value="primary">{{ t.primarySet }}</option>
                      <option value="secondary">{{ t.secondarySet }}</option>
                    </select>
                  </label>
                  <label>{{ t.gesture }}
                    <select v-model="selectedGesture">
                      <option v-for="gesture in LAST_CHANCES_GESTURES" :key="gesture" :value="gesture">{{ t.gestureNames[gesture] }}</option>
                    </select>
                  </label>
                </div>
                <div v-if="selectedAttack" class="lc-fields-grid">
                  <label class="lc-wide-field">{{ t.moveDescription }}<input v-model="selectedAttack.description" type="text" /></label>
                  <label class="lc-check-field">
                    <input
                      type="checkbox"
                      :checked="selectedAttack.enabled !== false && selectedAttack.behavior !== 'disabled'"
                      :disabled="selectedGesture === 'tap'"
                      @change="setAttackEnabled"
                    />
                    <span>{{ t.enabled }}</span>
                  </label>
                  <label>{{ t.attackColor }}<input v-model="selectedAttack.color" type="color" /></label>
                  <label>{{ t.damage }}<input v-model.number="selectedAttack.damage" type="number" min="0" step="1" /></label>
                  <label :title="selectedGesture === 'tap' ? t.tapNoCooldown : undefined">
                    {{ t.cooldown }}
                    <input v-model.number="selectedAttack.cooldownMs" type="number" min="0" step="25" :disabled="selectedGesture === 'tap'" />
                    <small v-if="selectedGesture === 'tap'">{{ t.tapNoCooldown }}</small>
                  </label>
                  <label>{{ t.range }}<input v-model.number="selectedAttack.range" type="number" min="0" step="1" /></label>
                  <label>{{ t.attackRadius }}<input v-model.number="selectedAttack.radius" type="number" min="0" step="1" /></label>
                  <label>{{ t.arc }}<input v-model.number="selectedAttack.arcDegrees" type="number" min="0" step="1" /></label>
                  <label>{{ t.duration }}<input v-model.number="selectedAttack.durationMs" type="number" min="0" step="25" /></label>
                  <label>{{ t.projectileSpeed }}<input v-model.number="selectedAttack.projectileSpeed" type="number" min="0" step="1" /></label>
                  <label>{{ t.pierce }}<input v-model.number="selectedAttack.pierce" type="number" min="0" step="1" /></label>
                  <label>{{ t.knockback }}<input v-model.number="selectedAttack.knockback" type="number" min="0" step="1" /></label>
                  <label>{{ t.colliderShape }}
                    <select :value="selectedAttack.collider?.shape ?? ''" @change="setColliderShape">
                      <option value="">{{ t.noCollider }}</option>
                      <option v-for="shape in LAST_CHANCES_COLLIDER_SHAPES" :key="shape" :value="shape">{{ shape }}</option>
                    </select>
                  </label>
                  <template v-if="selectedAttack.collider">
                    <label>{{ t.innerRange }}<input v-model.number="selectedAttack.collider.innerRange" type="number" min="0" step="1" /></label>
                    <label class="lc-check-field">
                      <input v-model="selectedAttack.collider.strictInnerRange" type="checkbox" />
                      <span>{{ t.strictInnerRange }}</span>
                    </label>
                    <label class="lc-check-field">
                      <input v-model="selectedAttack.collider.passesThroughWalls" type="checkbox" />
                      <span>{{ t.passesThroughWalls }}</span>
                    </label>
                    <label>{{ t.colliderWidth }}<input v-model.number="selectedAttack.collider.width" type="number" min="0" step="1" /></label>
                    <label>{{ t.traceMs }}<input v-model.number="selectedAttack.collider.traceMs" type="number" min="0" step="25" /></label>
                  </template>
                  <label class="lc-check-field">
                    <input type="checkbox" :checked="!!selectedAttack.charge" @change="toggleCharge" />
                    <span>{{ t.chargeEnabled }}</span>
                  </label>
                </div>
                <section v-if="selectedWeapon?.id === 'twohand-axe' && selectedWeapon.tuning" class="lc-control-tuning">
                  <h3>{{ t.axeSettings }}</h3>
                  <small>{{ t.axeSettingsHelp }}</small>
                  <div class="lc-control-grid-fields">
                    <label>{{ t.axeMotionBonus }}<input v-model.number="selectedWeapon.tuning.mouseDamageBonusMax" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.axeMotionPixels }}<input v-model.number="selectedWeapon.tuning.mouseMotionForMaxBonusPx" type="number" min="1" step="5" /></label>
                  </div>
                </section>

                <section v-if="selectedWeapon?.id === 'hybrid-sword' && selectedWeapon.tuning" class="lc-control-tuning">
                  <h3>{{ t.swordSettings }}</h3>
                  <small>{{ t.swordSettingsHelp }}</small>
                  <div class="lc-fields-grid">
                    <label class="lc-check-field">
                      <input v-model="selectedWeapon.staggerEnabled" type="checkbox" />
                      <span>{{ t.swordStaggerEnabled }}</span>
                    </label>
                    <label>{{ t.swordPerfectStart }}<input v-model.number="selectedWeapon.tuning.rhythmPerfectStartMs" type="number" min="1" step="25" /></label>
                    <label>{{ t.swordPerfectEnd }}<input v-model.number="selectedWeapon.tuning.rhythmPerfectEndMs" type="number" min="1" step="25" /></label>
                    <label>{{ t.swordFatigue }}<input v-model.number="selectedWeapon.tuning.rhythmFatigueMs" type="number" min="0" step="100" /></label>
                    <label>{{ t.swordRoomMisses }}<input v-model.number="selectedWeapon.tuning.rhythmMissesPerRoomBeforeFatigue" type="number" min="1" step="1" /></label>
                    <label>{{ t.swordConsecutiveMisses }}<input v-model.number="selectedWeapon.tuning.rhythmConsecutiveMissesBeforeFatigue" type="number" min="1" step="1" /></label>
                    <label>{{ t.swordAdvanceDistance }}<input v-model.number="selectedWeapon.tuning.advanceDistance" type="number" min="0" step="1" /></label>
                    <label>{{ t.swordAdvanceSpeed }}<input v-model.number="selectedWeapon.tuning.advanceSpeed" type="number" min="1" step="5" /></label>
                    <label>{{ t.swordStagger }}<input v-model.number="selectedWeapon.tuning.staggerDurationMs" type="number" min="0" step="50" /></label>
                    <label>{{ t.swordStaggerThreshold }}<input v-model.number="selectedWeapon.tuning.unstoppableThresholdMs" type="number" min="1" step="100" /></label>
                    <label>{{ t.swordUnstoppable }}<input v-model.number="selectedWeapon.tuning.unstoppableDurationMs" type="number" min="0" step="100" /></label>
                    <label>{{ t.swordUnterhauHold }}<input v-model.number="selectedWeapon.tuning.unterhauHoldMs" type="number" min="1" step="50" /></label>
                    <label>{{ t.swordUnterhauCooldown }}<input v-model.number="selectedWeapon.tuning.unterhauCooldownMultiplier" type="number" min="1" step="0.5" /></label>
                    <label>{{ t.swordEmptyOffhand }}<input v-model.number="selectedWeapon.tuning.emptyOffhandDamageMultiplier" type="number" min="1" step="0.05" /></label>
                  </div>
                </section>
                <div v-if="selectedAttack?.charge" class="lc-charge-editor">
                  <div class="lc-fields-grid">
                    <label>{{ t.chargeMax }}<input v-model.number="selectedAttack.charge.maxMs" type="number" min="1" step="25" /></label>
                  </div>
                  <div class="lc-band-heading">
                    <strong>{{ t.chargeBands }}</strong>
                    <button type="button" @click="addChargeBand">{{ t.addBand }}</button>
                  </div>
                  <div
                    v-for="(band, bandIndex) in selectedAttack.charge.bands"
                    :key="band.id"
                    class="lc-band-row"
                  >
                    <label>{{ t.bandLabel }}<input v-model="band.label" type="text" /></label>
                    <label>{{ t.bandMin }}<input v-model.number="band.minMs" type="number" min="0" step="25" /></label>
                    <label>{{ t.bandColor }}<input v-model="band.color" type="color" /></label>
                    <label>{{ t.bandDamage }}<input v-model.number="band.damageMultiplier" type="number" min="0" step="0.05" /></label>
                    <label>{{ t.bandRange }}<input v-model.number="band.rangeMultiplier" type="number" min="0" step="0.05" /></label>
                    <label>{{ t.bandKnockback }}<input v-model.number="band.knockbackMultiplier" type="number" min="0" step="0.05" /></label>
                    <button type="button" class="is-danger" :aria-label="t.removeBand" @click="removeChargeBand(bandIndex)">×</button>
                  </div>
                </div>
              </fieldset>

              <fieldset>
                <legend><span><FlaskConical :size="15" aria-hidden="true" />{{ t.enemy }}</span><small>{{ t.enemyHelp }}</small></legend>
                <div class="lc-fields-grid lc-select-row">
                  <label>{{ t.enemySelect }}
                    <select v-model.number="selectedEnemyIndex">
                      <option v-for="(enemy, index) in draft.enemies" :key="enemy.id" :value="index">{{ enemy.name }}</option>
                    </select>
                  </label>
                </div>
                <div v-if="selectedEnemy" class="lc-fields-grid">
                  <label>{{ t.enemyHp }}<input v-model.number="selectedEnemy.maxHp" type="number" min="1" step="1" /></label>
                  <label>{{ t.enemyArmor }}<input v-model.number="selectedEnemy.armor" type="number" min="0" step="1" /></label>
                  <label>{{ t.enemyDodge }}<input v-model.number="selectedEnemy.dodge" type="number" min="0" max="1" step="0.01" /></label>
                  <label>{{ t.enemyRadius }}<input v-model.number="selectedEnemy.radius" type="number" min="1" step="1" /></label>
                  <label>{{ t.enemySpeed }}<input v-model.number="selectedEnemy.moveSpeed" type="number" min="1" step="1" /></label>
                  <label>{{ t.enemyIdleTurn }}<input v-model.number="selectedEnemy.idleTurnRadiansPerSecond" type="number" min="0" step="0.01" /></label>
                  <label>{{ t.visionRange }}<input v-model.number="selectedEnemy.visionRange" type="number" min="1" step="1" /></label>
                  <label>{{ t.visionAngle }}<input v-model.number="selectedEnemy.visionAngleDegrees" type="number" min="0" step="1" /></label>
                  <label>{{ t.notice }}<input v-model.number="selectedEnemy.noticeMs" type="number" min="1" step="25" /></label>
                  <label>{{ t.alertPause }}<input v-model.number="selectedEnemy.alertPauseMs" type="number" min="1" step="25" /></label>
                  <label>{{ t.enemyAttackRange }}<input v-model.number="selectedEnemy.attackRange" type="number" min="1" step="1" /></label>
                  <label>{{ t.preferredAttackRange }}<input v-model.number="selectedEnemy.preferredAttackRangeRatio" type="number" min="0.01" max="1" step="0.01" /></label>
                  <label>{{ t.enemyDamage }}<input v-model.number="selectedEnemy.attackDamage" type="number" min="0" step="1" /></label>
                  <label>{{ t.enemyCooldown }}<input v-model.number="selectedEnemy.attackCooldownMs" type="number" min="1" step="25" /></label>
                  <label>{{ t.windup }}<input v-model.number="selectedEnemy.attackWindupMs" type="number" min="1" step="25" /></label>
                  <label>{{ t.mentalPressure }}<input v-model.number="selectedEnemy.mentalPressurePerSecond" type="number" min="0" step="0.1" /></label>
                </div>
                <section
                  v-if="selectedEnemy?.id === 'spider-knife' && selectedEnemy.tuning"
                  class="lc-control-tuning"
                  data-testid="knife-spider-version-settings"
                >
                  <h3>{{ t.knifeSpiderSettings }}</h3>
                  <div class="lc-fields-grid">
                    <label>{{ t.knifeSpiderVersion }}
                      <select v-model.number="selectedEnemy.tuning.behaviorVersion">
                        <option :value="1">{{ t.knifeSpiderV1 }}</option>
                        <option :value="2">{{ t.knifeSpiderV2 }}</option>
                      </select>
                    </label>
                    <label>{{ t.reflectedDamage }}<input v-model.number="selectedEnemy.tuning.reflectedDamageMultiplier" type="number" min="1" step="0.25" /></label>
                    <label>{{ t.reflectedSpeed }}<input v-model.number="selectedEnemy.tuning.reflectedSpeedMultiplier" type="number" min="0.1" step="0.05" /></label>
                    <label>{{ t.quickCapture }}<input v-model.number="selectedEnemy.tuning.quickCaptureWindowMs" type="number" min="1" step="10" /></label>
                    <label>{{ t.embeddedCapture }}<input v-model.number="selectedEnemy.tuning.embeddedCaptureWindowMs" type="number" min="1" step="50" /></label>
                    <label>{{ t.reflectionSelfDamage }}<input v-model.number="selectedEnemy.tuning.reflectionSelfDamageRatio" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.impactSelfDamage }}<input v-model.number="selectedEnemy.tuning.impactSelfDamageRatio" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.evadeChance }}<input v-model.number="selectedEnemy.tuning.evadeChance" type="number" min="0" max="1" step="0.01" /></label>
                    <label>{{ t.orbitDistance }}<input v-model.number="selectedEnemy.tuning.orbitDistance" type="number" min="1" step="1" /></label>
                    <label>{{ t.leapTriggerDistance }}<input v-model.number="selectedEnemy.tuning.leapTriggerDistance" type="number" min="1" step="1" /></label>
                  </div>
                </section>
              </fieldset>
            </div>

            <div v-else class="lc-json-editor">
              <p>{{ t.jsonHelp }}</p>
              <textarea v-model="rawJson" spellcheck="false" aria-label="99LC JSON configuration" />
              <button class="lc-inline-action" type="button" @click="validateRaw">
                <CheckCircle2 :size="14" aria-hidden="true" />{{ t.validate }}
              </button>
            </div>

            <div v-if="rawError || !validation.valid" class="lc-validation-errors" role="alert">
              <strong>{{ t.validationErrors }}</strong>
              <pre>{{ rawError || validation.errors.join('\n') }}</pre>
            </div>
            <p v-if="notice" class="lc-builder-notice" aria-live="polite">{{ notice }}</p>
          </div>

          <footer class="lc-builder-footer">
            <div class="lc-builder-file-actions">
              <input ref="fileInput" class="sr-only" type="file" accept="application/json,.json" @change="importJson" />
              <button type="button" @click="fileInput?.click()"><Upload :size="14" aria-hidden="true" />{{ t.import }}</button>
              <button type="button" @click="exportJson"><Download :size="14" aria-hidden="true" />{{ t.export }}</button>
              <button type="button" class="is-danger" @click="clearOverride"><Eraser :size="14" aria-hidden="true" />{{ t.clear }}</button>
            </div>
            <div class="lc-builder-apply-actions">
              <button type="button" :disabled="!validation.valid || !!rawError" @click="save"><Save :size="14" aria-hidden="true" />{{ t.save }}</button>
              <button type="button" class="is-control-live" :disabled="!validation.valid || !!rawError" @click="applyControls"><Zap :size="14" aria-hidden="true" />{{ t.applyControls }}</button>
              <button type="button" class="is-primary" :disabled="!validation.valid || !!rawError" @click="apply"><RotateCcw :size="14" aria-hidden="true" />{{ t.apply }}</button>
            </div>
          </footer>
        </template>

        <p v-else class="lc-builder-empty">{{ t.noConfig }}</p>
      </aside>
    </div>
  </Transition>
</template>

<style scoped>
.lc-builder-backdrop {
  position: fixed;
  z-index: 4500;
  inset: 0;
  display: flex;
  justify-content: flex-end;
  color: #e7e5df;
  background: rgba(2, 3, 4, 0.72);
  backdrop-filter: blur(6px);
}

.lc-builder {
  width: min(44rem, 100%);
  height: 100dvh;
  display: grid;
  grid-template-rows: auto auto auto minmax(0, 1fr) auto;
  border-left: 1px solid rgba(206, 185, 137, 0.17);
  background:
    radial-gradient(circle at 100% 0, rgba(118, 35, 42, 0.15), transparent 30%),
    #101314;
  box-shadow: -2rem 0 5rem rgba(0, 0, 0, 0.58);
}

.lc-builder-header {
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem 1.15rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.lc-builder-brand { width: 2.5rem; height: 2.5rem; display: grid; place-items: center; border: 1px solid rgba(204, 166, 86, 0.3); border-radius: 0.65rem; color: #c7a557; background: rgba(203, 164, 83, 0.07); }
.lc-builder-header p { margin: 0; color: #a6484e; font-size: 0.58rem; font-weight: 800; letter-spacing: 0.16em; text-transform: uppercase; }
.lc-builder-header h2 { margin: 0.08rem 0; color: #f1ece1; font: 600 1.25rem/1 Georgia, serif; }
.lc-builder-header span { color: #747a77; font-size: 0.62rem; }
.lc-builder-header > button { width: 2.25rem; height: 2.25rem; display: grid; place-items: center; border: 1px solid rgba(255, 255, 255, 0.08); border-radius: 50%; color: #8d918f; background: transparent; }

.lc-builder-tabs { display: grid; grid-template-columns: 1fr 1fr; padding: 0.65rem 1rem 0; }
.lc-builder-tabs button { min-height: 2.35rem; display: inline-flex; align-items: center; justify-content: center; gap: 0.4rem; border: 0; border-bottom: 1px solid rgba(255, 255, 255, 0.08); color: #707572; background: transparent; font-size: 0.65rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
.lc-builder-tabs button.active { color: #dcc98e; border-bottom-color: #af8538; }

.lc-builder-status { display: flex; align-items: center; gap: 0.4rem; margin: 0.55rem 1rem 0; padding: 0.45rem 0.6rem; border: 1px solid rgba(255, 255, 255, 0.06); border-radius: 0.45rem; font-size: 0.6rem; }
.lc-builder-status.is-valid { color: #9aa87a; background: rgba(97, 119, 68, 0.08); }
.lc-builder-status.is-invalid { color: #cf7479; background: rgba(151, 54, 62, 0.1); }
.lc-builder-status span { font-weight: 800; text-transform: uppercase; }
.lc-builder-status small { margin-left: auto; color: #606562; font: 600 0.55rem/1 var(--font-mono, monospace); }

.lc-builder-body { min-height: 0; overflow-y: auto; padding: 0.7rem 1rem 1.2rem; }
.lc-quick-fields { display: grid; gap: 0.75rem; }
fieldset { min-width: 0; margin: 0; padding: 0.75rem; border: 1px solid rgba(255, 255, 255, 0.065); border-radius: 0.6rem; background: rgba(255, 255, 255, 0.015); }
legend { width: 100%; display: flex; align-items: end; justify-content: space-between; gap: 1rem; padding: 0 0.15rem 0.45rem; }
legend span { display: inline-flex; align-items: center; gap: 0.4rem; color: #d8d3c9; font-size: 0.66rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
legend small { color: #656a67; font-size: 0.55rem; text-align: right; }

.lc-fields-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.55rem; }
.lc-fields-grid label { min-width: 0; display: grid; align-content: end; gap: 0.25rem; color: #747a77; font-size: 0.54rem; font-weight: 700; line-height: 1.2; }
.lc-fields-grid input,
.lc-fields-grid select,
.lc-band-row input { width: 100%; min-height: 2rem; padding: 0.38rem 0.45rem; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 0.38rem; outline: none; color: #e1ded5; background: #0b0e0f; font: 600 0.66rem/1.2 var(--font-mono, monospace); color-scheme: dark; }
.lc-fields-grid input:focus,
.lc-fields-grid select:focus,
.lc-band-row input:focus { border-color: rgba(208, 172, 92, 0.6); box-shadow: 0 0 0 2px rgba(208, 172, 92, 0.09); }
.lc-fields-grid input[type="color"],
.lc-band-row input[type="color"] { min-height: 2rem; padding: 0.18rem; cursor: pointer; }
.lc-check-field { grid-template-columns: auto 1fr; align-items: center; align-content: center !important; gap: 0.45rem !important; min-height: 2rem; padding: 0.35rem 0.45rem; border: 1px solid rgba(255, 255, 255, 0.07); border-radius: 0.38rem; background: rgba(255, 255, 255, 0.018); }
.lc-check-field input { width: 1rem; min-height: 1rem; margin: 0; padding: 0; accent-color: #c8a45e; }
.lc-check-field span { color: #b8b7b1; font-size: 0.58rem; }
.lc-select-row { grid-template-columns: repeat(2, minmax(0, 1fr)); margin-bottom: 0.55rem; }
.lc-control-tuning { display: grid; gap: 0.55rem; padding: 0.55rem 0; border-top: 1px solid rgba(255, 255, 255, 0.045); }
.lc-control-tuning:first-of-type { padding-top: 0; border-top: 0; }
.lc-control-tuning h3,
.lc-control-tuning h4 { margin: 0; color: #b9aa89; font-size: 0.57rem; letter-spacing: 0.08em; text-transform: uppercase; }
.lc-control-tuning h4 { margin-top: 0.15rem; color: #80768c; font-size: 0.51rem; }
.lc-wide-field { grid-column: 1 / -1; }
.lc-profile-picker { display: grid; grid-template-columns: minmax(9rem, 14rem); }
.lc-profile-picker label { display: grid; gap: 0.25rem; color: #747a77; font-size: 0.54rem; font-weight: 700; }
.lc-profile-picker select { width: 100%; min-height: 2rem; padding: 0.38rem 0.45rem; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 0.38rem; outline: none; color: #e1ded5; background: #0b0e0f; font: 600 0.66rem/1.2 var(--font-mono, monospace); color-scheme: dark; }
.lc-control-record-editor { display: grid; gap: 0.45rem; padding-top: 0.5rem; border-top: 1px solid rgba(255, 255, 255, 0.045); }
.lc-control-record-editor > h4 { line-height: 1.4; }
.lc-control-record-editor > p { margin: 0; color: #676b68; font-size: 0.54rem; }
.lc-combo-node-editor { display: grid; gap: 0.45rem; padding: 0.5rem; border: 1px solid rgba(157, 125, 195, 0.12); border-radius: 0.4rem; background: rgba(83, 58, 104, 0.035); }
.lc-combo-node-editor > header { display: flex; align-items: baseline; justify-content: space-between; gap: 0.5rem; }
.lc-combo-node-editor > header strong { color: #a79aac; font-size: 0.52rem; }
.lc-combo-node-editor > header span { overflow: hidden; color: #66616b; font-size: 0.48rem; text-overflow: ellipsis; white-space: nowrap; }

.lc-charge-editor { display: grid; gap: 0.5rem; margin-top: 0.65rem; padding-top: 0.65rem; border-top: 1px solid rgba(255, 255, 255, 0.055); }
.lc-band-heading { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; }
.lc-band-heading strong { color: #c9c3b7; font-size: 0.58rem; letter-spacing: 0.07em; text-transform: uppercase; }
.lc-band-heading button,
.lc-band-row > button { min-height: 1.8rem; padding: 0.3rem 0.48rem; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 0.35rem; color: #aaa; background: rgba(255, 255, 255, 0.025); font-size: 0.52rem; }
.lc-band-row { position: relative; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.42rem; padding: 0.5rem 2.35rem 0.5rem 0.5rem; border: 1px solid rgba(255, 255, 255, 0.055); border-radius: 0.45rem; background: rgba(255, 255, 255, 0.012); }
.lc-band-row label { min-width: 0; display: grid; gap: 0.22rem; color: #747a77; font-size: 0.5rem; font-weight: 700; }
.lc-band-row > button { position: absolute; right: 0.45rem; top: 0.45rem; width: 1.45rem; padding: 0; color: #bd7075; }

.lc-json-editor { min-height: 100%; display: grid; grid-template-rows: auto minmax(24rem, 1fr) auto; gap: 0.55rem; }
.lc-json-editor p { margin: 0; color: #747a77; font-size: 0.62rem; }
.lc-json-editor textarea { width: 100%; min-height: 30rem; resize: vertical; padding: 0.8rem; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 0.55rem; outline: none; color: #bfc6b8; background: #080a0b; font: 0.66rem/1.55 var(--font-mono, monospace); tab-size: 2; }
.lc-json-editor textarea:focus { border-color: rgba(207, 171, 91, 0.45); }
.lc-inline-action { justify-self: end; display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.4rem 0.65rem; border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 0.4rem; color: #b8b9b2; background: rgba(255, 255, 255, 0.03); font-size: 0.6rem; }

.lc-validation-errors { margin-top: 0.65rem; padding: 0.65rem; border: 1px solid rgba(194, 75, 83, 0.25); border-radius: 0.45rem; color: #ca7379; background: rgba(131, 39, 47, 0.08); }
.lc-validation-errors strong { display: block; margin-bottom: 0.25rem; font-size: 0.58rem; letter-spacing: 0.08em; text-transform: uppercase; }
.lc-validation-errors pre { max-height: 9rem; margin: 0; overflow: auto; white-space: pre-wrap; font: 0.55rem/1.5 var(--font-mono, monospace); }
.lc-builder-notice { margin: 0.65rem 0 0; color: #a3a77d; font-size: 0.6rem; text-align: center; }

.lc-builder-footer { display: grid; gap: 0.45rem; padding: 0.75rem 1rem max(0.75rem, env(safe-area-inset-bottom)); border-top: 1px solid rgba(255, 255, 255, 0.065); background: rgba(6, 8, 9, 0.92); }
.lc-builder-footer > div { display: flex; flex-wrap: wrap; gap: 0.4rem; }
.lc-builder-footer button { min-height: 2rem; display: inline-flex; align-items: center; justify-content: center; gap: 0.35rem; padding: 0.35rem 0.58rem; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 0.4rem; color: #aaada7; background: rgba(255, 255, 255, 0.025); font-size: 0.57rem; font-weight: 700; }
.lc-builder-footer button:hover:not(:disabled) { color: #eeeae0; border-color: rgba(255, 255, 255, 0.22); }
.lc-builder-footer button.is-danger { color: #bd7075; }
.lc-builder-apply-actions { display: grid !important; grid-template-columns: 0.9fr 1.1fr 1.2fr; }
.lc-builder-apply-actions button { min-height: 2.5rem; }
.lc-builder-apply-actions button.is-control-live { color: #c7b6d9; border-color: rgba(159, 120, 194, 0.28); background: rgba(112, 74, 145, 0.08); }
.lc-builder-apply-actions button.is-primary { color: #14120d; border-color: #c5a357; background: linear-gradient(135deg, #d0b16b, #9c722d); }
.lc-builder-apply-actions button:disabled { opacity: 0.35; cursor: not-allowed; }
.lc-builder-empty { place-self: center; color: #777c79; }

.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

.lc-builder-backdrop-enter-active,
.lc-builder-backdrop-leave-active { transition: opacity 0.18s ease; }
.lc-builder-backdrop-enter-active .lc-builder,
.lc-builder-backdrop-leave-active .lc-builder { transition: transform 0.24s ease; }
.lc-builder-backdrop-enter-from,
.lc-builder-backdrop-leave-to { opacity: 0; }
.lc-builder-backdrop-enter-from .lc-builder,
.lc-builder-backdrop-leave-to .lc-builder { transform: translateX(100%); }

@media (max-width: 660px) {
  .lc-builder { width: 100%; border-left: 0; }
  .lc-builder-header { padding: 0.75rem; }
  .lc-builder-header span { display: none; }
  .lc-fields-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .lc-band-row { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  legend small { display: none; }
  .lc-builder-footer { padding-inline: 0.7rem; }
  .lc-builder-footer button { flex: 1; }
}

@media (prefers-reduced-motion: reduce) {
  .lc-builder-backdrop-enter-active,
  .lc-builder-backdrop-leave-active,
  .lc-builder-backdrop-enter-active .lc-builder,
  .lc-builder-backdrop-leave-active .lc-builder { transition: none; }
}
</style>
