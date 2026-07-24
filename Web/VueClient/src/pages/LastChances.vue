<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, shallowRef } from 'vue'
import {
  Activity,
  Brain,
  ChevronRight,
  CircleDotDashed,
  Dices,
  Eye,
  Footprints,
  Gamepad2,
  HeartPulse,
  Keyboard,
  Map as MapIcon,
  Maximize2,
  Minimize2,
  MousePointer2,
  Pause,
  Play,
  RefreshCw,
  RotateCcw,
  Settings2,
  Shield,
  Skull,
  Smartphone,
  Sparkles,
  Swords,
  Target,
  TriangleAlert,
  Trophy,
} from 'lucide-vue-next'
import { currentLocale } from '../i18n'
import {
  clearLastChancesConfig as clearLastChancesConfigOverride,
  cloneLastChancesConfig,
  LastChancesEngine,
  LAST_CHANCES_GESTURES,
  loadLastChancesConfig,
  resolveLastChancesLoadout,
  saveLastChancesConfig as saveLastChancesConfigOverride,
  type LastChancesConfig,
  type LastChancesControlScheme,
  type LastChancesFeedbackPreferences,
  type LastChancesGamePlan,
  type LastChancesHand,
  type LastChancesResolvedWeapon,
  type LastChancesSnapshot,
  type LastChancesStoryPage,
} from '../features/last-chances'
import {
  loadLastChancesControlScheme,
  loadLastChancesFeedbackPreferences,
  saveLastChancesControlScheme,
  saveLastChancesFeedbackPreferences,
} from '../features/last-chances/preferences'
import BuilderDrawer from '../components/last-chances/BuilderDrawer.vue'
import RunMapOverlay, {
  type LastChancesLocale,
  type RunMapEdge,
  type RunMapNode,
} from '../components/last-chances/RunMapOverlay.vue'
import TouchControls, { type AttackHand } from '../components/last-chances/TouchControls.vue'
import WeaponCooldowns, {
  type WeaponCooldown,
} from '../components/last-chances/WeaponCooldowns.vue'

const copy = {
  en: {
    eyebrow: 'First playable prototype · deterministic memory',
    title: '99 Last Chances',
    subtitle: 'Every death costs Chances. Every retry remembers the same rooms. Your body does not recover so easily.',
    online: 'Simulation live',
    waiting: 'Waiting for a route',
    map: 'Run map',
    routeReady: 'Route map ready',
    fullscreen: 'Full screen',
    exitFullscreen: 'Exit full screen',
    pause: 'Pause',
    resume: 'Resume',
    retry: 'Retry attempt',
    newGeneration: 'New generation',
    builder: 'Builder',
    reload: 'Try loading again',
    restoreDefault: 'Restore server definition',
    loading: 'Assembling the remembered rooms…',
    loadError: 'The prototype definition could not be loaded.',
    loadingHint: 'Reading the runtime JSON and preparing the first generation.',
    physical: 'Physical health',
    mental: 'Mental health',
    chances: 'Chances remaining',
    chanceSingular: 'Chance',
    chancePlural: 'Chances',
    nextDeath: 'Next death costs',
    tier: 'Tier',
    room: 'Room',
    enemies: 'Enemies',
    seed: 'Seed',
    generation: 'Generation',
    noRoom: 'Route selection',
    noEnemies: 'Room quiet',
    statErosion: 'Stat erosion',
    statErosionHelp: 'Permanent losses carried through this generation',
    noErosion: 'No erosion yet',
    hpLost: 'Max HP',
    mindLost: 'Max mind',
    speedLost: 'Speed',
    armorLost: 'Armor',
    attackLost: 'Attack',
    armor: 'Armor',
    speed: 'Speed',
    currentThreat: 'Current threat',
    calm: 'No enemy has noticed you',
    noticed: 'Something has seen you',
    recognized: 'Gesture recognized',
    combo: 'Combo',
    stageLabel: '99 Last Chances isometric combat arena',
    controls: 'Control language',
    controlsHelp: 'Gamepad, keyboard, and mouse follow the selected scheme; touch always uses DeepList.',
    controlScheme: 'Control scheme',
    controlSchemeChanged: 'Control scheme changed to',
    schemeSummaries: {
      legacy: 'Five DeepList gestures per hand with the original timing windows.',
      mylorik: 'Immediate strikes, tap/hold techniques, Mobility and Interact.',
      dualsense: 'Shoulder-only combat with instant bumpers and analog combo triggers.',
    },
    keyboard: 'Keyboard',
    keyboardMove: 'WASD / arrows move',
    keyboardAttack: 'attack with',
    keyboardInteract: 'E interacts',
    mouse: 'Mouse',
    mouseAim: 'Move pointer to aim',
    mouseAttack: 'Left / right button gestures',
    gamepad: 'Gamepad',
    gamepadMove: 'Left stick moves · right stick aims',
    gamepadAttack: 'Buttons',
    gamepadInteract: 'press both attack buttons together to interact',
    gamepadConnected: 'Connected',
    gamepadDisconnected: 'Press a controller button to connect',
    gamepadUnsupported: 'This browser does not expose the Gamepad API',
    gamepadMenu: 'Map: stick / L1 cycles · R1 selects',
    touch: 'Touch',
    touchMove: 'Left stick moves · aim pad aims',
    touchAttack: 'Use either hand button for gestures',
    touchLegacy: 'Touch always uses DeepList',
    strike: 'Strike',
    technique: 'Technique',
    mobility: 'Mobility',
    press: 'press',
    release: 'release',
    techniqueTap: 'Technique tap',
    techniqueHold: 'Technique hold / charge',
    instantMoves: 'Instant bumper moves',
    comboTriggers: 'Analog combo triggers',
    back: 'Back',
    interactConfirm: 'Interact / Confirm',
    feedback: 'DualSense feedback',
    dualSenseActive: 'DualSense controls active',
    feedbackMode: 'Feedback mode',
    feedbackIntensity: 'Intensity',
    controlsOnly: 'Controls only',
    vibration: 'Vibration',
    enhanced: 'Adaptive triggers + vibration',
    feedbackUnavailable: 'Feedback unavailable',
    feedbackError: 'Feedback error',
    enableDualSense: 'Enable DualSense features',
    disableEnhanced: 'Disable enhanced feedback',
    dualSenseEnabled: 'DualSense feature request completed.',
    dualSenseFailed: 'Enhanced DualSense feedback could not be enabled; controls remain active.',
    dualSenseUnsupported: 'WebHID is unavailable in this browser or without HTTPS; controls remain active.',
    tactileLegend: 'Tactile legend',
    tactileLegendItems: [
      'Light click · step accepted',
      'Rising ramp · charge',
      'Band tick · release armed',
      'Firm gate · continuation',
      'Double pulse · follow-up',
      'Dull pulse · blocked',
      'Sharp impact · contact',
    ],
    controlStates: {
      ready: 'Ready',
      charge: 'Charge',
      continuation: 'Continuation ready',
      tension: 'Tension',
      blocked: 'Blocked',
      impact: 'Impact',
    },
    feedbackUpdated: 'Feedback preference updated.',
    controlsApplied: 'Control tuning applied without restarting the attempt.',
    controlsRejected: 'Control tuning could not be applied to the current attempt.',
    interact: 'Interact',
    gestureGuide: 'Five gestures per hand',
    tap: 'Tap',
    doubleTap: 'Double tap',
    doubleTapHold: 'Double tap + hold',
    hold: 'Hold / release',
    holdThenDoubleTap: 'Hold + tap',
    pauseTitle: 'The room holds its breath',
    pauseBody: 'Nothing advances while you are paused.',
    deathEyebrow: 'A Chance was spent',
    deathTitle: 'Death remembers you',
    deathBody: 'The same generation waits at the beginning, but your eroded stats remain.',
    gameOverEyebrow: 'The ninety-ninth ending',
    gameOverTitle: 'No Chances remain',
    gameOverBody: 'This generation is closed. Build another and begin with a whole body.',
    victoryEyebrow: 'Terminal tier cleared',
    victoryTitle: 'You reached the other side',
    victoryBody: 'The route is complete. The next generation will not be so familiar.',
    interactionEyebrow: 'The room remembers the offer',
    interactionChoose: 'Take this outcome',
    interactionUnavailable: 'Unavailable for the current loadout or Chances',
    events: 'Log',
    eventsEmpty: 'Nothing has happened yet',
    interactionCost: 'Cost',
    altarEyebrow: 'Boss altar',
    altarAccept: 'Sacrifice 5 Chances',
    altarDecline: 'Enter without saving',
    altarUnavailable: 'Not enough Chances for the sacrifice',
    turrets: 'turrets',
    storyContinue: 'Continue',
    storyBegin: 'Enter the remembered run',
    storyClose: 'Close the memory',
    deaths: 'Deaths remembered',
    beginAgain: 'Begin a new generation',
    chooseRoute: 'Choose the next room',
    killedBy: 'Killed by',
    mindCollapsed: 'Your mental health collapsed',
    attemptEnded: 'The attempt ended',
    saved: 'Browser override saved; the current attempt is unchanged.',
    cleared: 'Browser override cleared; the current attempt is unchanged.',
    applied: 'Builder definition applied to a fresh generation.',
    questsTitle: 'Move quests',
    questHandLeft: 'Left hand',
    questHandRight: 'Right hand',
    questTapTask: 'Two tap kills in one room',
    questHoldTask: 'Two hold kills in one room',
    questComboTask: 'Hit an elite with every unlocked move',
    questKillsWith: 'Two kills in one room with',
    questEliteRoutes: 'Hit an elite with every unlocked route',
    questNextRoom: 'Next room:',
    questSwarm: 'Cockroaches incoming',
  },
  ru: {
    eyebrow: 'Первый игровой прототип · детерминированная память',
    title: '99 Last Chances',
    subtitle: 'Каждая смерть отнимает Шансы. Каждая попытка помнит те же комнаты. А ваше тело восстанавливается не так легко.',
    online: 'Симуляция запущена',
    waiting: 'Ожидание маршрута',
    map: 'Карта забега',
    routeReady: 'Карта маршрута готова',
    fullscreen: 'На весь экран',
    exitFullscreen: 'Выйти из полного экрана',
    pause: 'Пауза',
    resume: 'Продолжить',
    retry: 'Повторить попытку',
    newGeneration: 'Новая генерация',
    builder: 'Конструктор',
    reload: 'Попробовать загрузить снова',
    restoreDefault: 'Восстановить конфигурацию сервера',
    loading: 'Собираем запомненные комнаты…',
    loadError: 'Не удалось загрузить конфигурацию прототипа.',
    loadingHint: 'Читаем JSON и готовим первую генерацию.',
    physical: 'Физическое здоровье',
    mental: 'Ментальное здоровье',
    chances: 'Осталось Шансов',
    chanceSingular: 'Шанс',
    chancePlural: 'Шансов',
    nextDeath: 'Следующая смерть стоит',
    tier: 'Уровень',
    room: 'Комната',
    enemies: 'Враги',
    seed: 'Сид',
    generation: 'Генерация',
    noRoom: 'Выбор маршрута',
    noEnemies: 'В комнате тихо',
    statErosion: 'Истощение параметров',
    statErosionHelp: 'Постоянные потери в этой генерации',
    noErosion: 'Истощения пока нет',
    hpLost: 'Макс. здоровье',
    mindLost: 'Макс. рассудок',
    speedLost: 'Скорость',
    armorLost: 'Броня',
    attackLost: 'Атака',
    armor: 'Броня',
    speed: 'Скорость',
    currentThreat: 'Текущая угроза',
    calm: 'Ни один враг вас не заметил',
    noticed: 'Что-то вас увидело',
    recognized: 'Распознан жест',
    combo: 'Комбо',
    stageLabel: 'Изометрическая боевая арена 99 Last Chances',
    controls: 'Язык управления',
    controlsHelp: 'Все устройства используют одну систему жестов для двух рук.',
    controlScheme: 'Схема управления',
    controlSchemeChanged: 'Схема управления изменена на',
    schemeSummaries: {
      legacy: 'Пять жестов DeepList для каждой руки с исходными окнами распознавания.',
      mylorik: 'Мгновенные удары, техники по нажатию/задержке, Mobility и Interact.',
      dualsense: 'Бой только плечевыми кнопками: мгновенные бамперы и аналоговые комбо-триггеры.',
    },
    keyboard: 'Клавиатура',
    keyboardMove: 'WASD / стрелки — движение',
    keyboardAttack: 'атака клавишами',
    keyboardInteract: 'E — взаимодействие',
    mouse: 'Мышь',
    mouseAim: 'Двигайте указатель для прицела',
    mouseAttack: 'Жесты левой / правой кнопкой',
    gamepad: 'Геймпад',
    gamepadMove: 'Левый стик — движение · правый — прицел',
    gamepadAttack: 'Кнопки',
    gamepadInteract: 'обе кнопки атаки вместе — взаимодействие',
    gamepadConnected: 'Подключён',
    gamepadDisconnected: 'Нажмите кнопку геймпада для подключения',
    gamepadUnsupported: 'Браузер не предоставляет Gamepad API',
    gamepadMenu: 'Карта: стик / L1 — выбор · R1 — вход',
    touch: 'Сенсорный экран',
    touchMove: 'Левый стик — движение · площадка — прицел',
    touchAttack: 'Используйте кнопки обеих рук для жестов',
    touchLegacy: 'Сенсорное управление всегда использует DeepList',
    strike: 'Удар',
    technique: 'Техника',
    mobility: 'Mobility',
    press: 'нажатие',
    release: 'отпускание',
    techniqueTap: 'Техника по нажатию',
    techniqueHold: 'Техника с задержкой / зарядом',
    instantMoves: 'Мгновенные мувы бамперами',
    comboTriggers: 'Аналоговые комбо-триггеры',
    back: 'Назад',
    interactConfirm: 'Взаимодействие / Подтвердить',
    feedback: 'Отклик DualSense',
    dualSenseActive: 'Управление DualSense активно',
    feedbackMode: 'Режим отклика',
    feedbackIntensity: 'Интенсивность',
    controlsOnly: 'Только управление',
    vibration: 'Вибрация',
    enhanced: 'Адаптивные триггеры + вибрация',
    feedbackUnavailable: 'Отклик недоступен',
    feedbackError: 'Ошибка отклика',
    enableDualSense: 'Включить функции DualSense',
    disableEnhanced: 'Отключить расширенный отклик',
    dualSenseEnabled: 'Запрос функций DualSense завершён.',
    dualSenseFailed: 'Расширенный отклик DualSense не включён; управление продолжает работать.',
    dualSenseUnsupported: 'WebHID недоступен в этом браузере или без HTTPS; управление продолжает работать.',
    tactileLegend: 'Тактильная легенда',
    tactileLegendItems: [
      'Лёгкий клик · шаг принят',
      'Нарастающий упор · заряд',
      'Щелчок сектора · отпускание готово',
      'Твёрдый гейт · доступно продолжение',
      'Двойной импульс · окно фоллоу-апа',
      'Глухой импульс · заблокировано',
      'Резкий удар · контакт',
    ],
    controlStates: {
      ready: 'Готово',
      charge: 'Заряд',
      continuation: 'Продолжение готово',
      tension: 'Натяжение',
      blocked: 'Заблокировано',
      impact: 'Контакт',
    },
    feedbackUpdated: 'Настройка отклика обновлена.',
    controlsApplied: 'Тайминги управления применены без перезапуска попытки.',
    controlsRejected: 'Не удалось применить тайминги к текущей попытке.',
    interact: 'Взаимодействовать',
    gestureGuide: 'Пять жестов для каждой руки',
    tap: 'Нажатие',
    doubleTap: 'Двойное нажатие',
    doubleTapHold: 'Двойное нажатие + задержка',
    hold: 'Задержка / отпускание',
    holdThenDoubleTap: 'Задержка + нажатие',
    pauseTitle: 'Комната затаила дыхание',
    pauseBody: 'Пока игра на паузе, ничто не движется.',
    deathEyebrow: 'Один Шанс потрачен',
    deathTitle: 'Смерть вас запомнила',
    deathBody: 'Та же генерация ждёт в начале, но истощённые параметры останутся с вами.',
    gameOverEyebrow: 'Девяносто девятый финал',
    gameOverTitle: 'Шансов больше нет',
    gameOverBody: 'Эта генерация закрыта. Создайте новую и начните с целым телом.',
    victoryEyebrow: 'Последний уровень зачищен',
    victoryTitle: 'Вы добрались до другой стороны',
    victoryBody: 'Маршрут завершён. Следующая генерация не будет такой знакомой.',
    interactionEyebrow: 'Комната помнит предложение',
    interactionChoose: 'Принять этот исход',
    interactionUnavailable: 'Недоступно с текущей экипировкой или запасом Шансов',
    events: 'Журнал',
    eventsEmpty: 'Пока ничего не произошло',
    interactionCost: 'Цена',
    altarEyebrow: 'Алтарь босса',
    altarAccept: 'Принести жертву 5 Шансов',
    altarDecline: 'Войти без сохранения',
    altarUnavailable: 'Для жертвы не хватает Шансов',
    turrets: 'турелей',
    storyContinue: 'Продолжить',
    storyBegin: 'Войти в запомненный забег',
    storyClose: 'Закрыть воспоминание',
    deaths: 'Запомнено смертей',
    beginAgain: 'Начать новую генерацию',
    chooseRoute: 'Выбрать следующую комнату',
    killedBy: 'Убит врагом',
    mindCollapsed: 'Ваше ментальное здоровье иссякло',
    attemptEnded: 'Попытка завершена',
    saved: 'Замена сохранена в браузере; текущая попытка не изменена.',
    cleared: 'Замена в браузере очищена; текущая попытка не изменена.',
    applied: 'Конфигурация применена в новой генерации.',
    questsTitle: 'Квесты мувов',
    questHandLeft: 'Левая рука',
    questHandRight: 'Правая рука',
    questTapTask: 'Два убийства тапом в одной комнате',
    questHoldTask: 'Два убийства холдом в одной комнате',
    questComboTask: 'Попади по элиту всеми открытыми мувами',
    questKillsWith: 'Два убийства в одной комнате через',
    questEliteRoutes: 'Попадите по элиту всеми открытыми маршрутами',
    questNextRoom: 'Со следующей комнаты:',
    questSwarm: 'Тараканы бегут',
  },
} as const

const CONTROL_SCHEME_OPTIONS: ReadonlyArray<{
  id: LastChancesControlScheme
  label: 'DeepList' | 'mylorik' | 'DualSense'
}> = [
  { id: 'legacy', label: 'DeepList' },
  { id: 'mylorik', label: 'mylorik' },
  { id: 'dualsense', label: 'DualSense' },
]

const FEEDBACK_MODE_OPTIONS: ReadonlyArray<{
  id: LastChancesFeedbackPreferences['mode']
  label: 'Off' | 'Reduced' | 'Full'
}> = [
  { id: 'off', label: 'Off' },
  { id: 'reduced', label: 'Reduced' },
  { id: 'full', label: 'Full' },
]

const qaControlsFixture = typeof window !== 'undefined'
  && new URLSearchParams(window.location.search).get('qa') === '1'
  && new URLSearchParams(window.location.search).get('fixture') === 'controls'

function reducedMotionPreferred(): boolean {
  return typeof window !== 'undefined'
    && typeof window.matchMedia === 'function'
    && window.matchMedia('(prefers-reduced-motion: reduce)').matches
}

const locale = computed<LastChancesLocale>(() => currentLocale.value)
const t = computed(() => copy[locale.value])
const canvas = ref<HTMLCanvasElement | null>(null)
const pageRoot = ref<HTMLElement | null>(null)
const engine = shallowRef<LastChancesEngine | null>(null)
const controlScheme = ref<LastChancesControlScheme>(loadLastChancesControlScheme())
const feedbackPreferences = ref<LastChancesFeedbackPreferences>(
  loadLastChancesFeedbackPreferences(undefined, reducedMotionPreferred()),
)
const config = ref<LastChancesConfig | null>(null)
const plan = ref<LastChancesGamePlan | null>(null)
const snapshot = ref<LastChancesSnapshot | null>(null)
const loading = ref(true)
const loadError = ref('')
const routeMapOpen = ref(false)
const isFullscreen = ref(false)
const builderOpen = ref(false)
const toast = ref('')
const visitedNodeIds = ref(new Set<string>())
const storyPages = ref<LastChancesStoryPage[]>([])
const storyIndex = ref(0)
let loadController: AbortController | null = null
let resumeAfterMap = false
let resumeAfterBuilder = false
let toastTimer: number | null = null

const currentNode = computed(() => plan.value?.nodes.find(node => node.id === snapshot.value?.currentNodeId) ?? null)
const livingEnemies = computed(() => snapshot.value?.enemies.filter(enemy => enemy.state !== 'dead') ?? [])
const alertedEnemies = computed(() => livingEnemies.value.filter(enemy => enemy.state !== 'idle').length)
const hpPercent = computed(() => {
  const player = snapshot.value?.player
  return player ? Math.max(0, Math.min(100, player.hp / Math.max(1, player.stats.maxHp) * 100)) : 0
})
const mentalPercent = computed(() => {
  const player = snapshot.value?.player
  return player ? Math.max(0, Math.min(100, player.mentalHealth / Math.max(1, player.stats.maxMentalHealth) * 100)) : 0
})
const chancePercent = computed(() => snapshot.value && config.value
  ? Math.max(0, Math.min(100, snapshot.value.chances / Math.max(1, config.value.chances) * 100))
  : 100)
/** Newest first, trimmed to what fits the sidebar card. */
const recentEvents = computed(() => [...(snapshot.value?.events ?? [])].reverse().slice(0, 5))
const activeTierIndex = computed(() => snapshot.value?.currentTierIndex ?? 0)
const activeTier = computed(() => config.value?.progression.tiers[activeTierIndex.value] ?? null)
const nextDeathCost = computed(() => activeTier.value?.deathCost ?? 1)
const equippedLoadout = computed(() => {
  if (!config.value) return { left: null, right: null }
  const activeConfig = cloneLastChancesConfig(config.value)
  if (snapshot.value?.loadout) activeConfig.loadout = { ...snapshot.value.loadout }
  return resolveLastChancesLoadout(activeConfig)
})
const leftWeapon = computed(() => equippedLoadout.value.left)
const rightWeapon = computed(() => equippedLoadout.value.right)
const equippedArtifact = computed(() => config.value?.artifacts?.find(
  artifact => artifact.id === snapshot.value?.loadout?.artifactId,
) ?? null)
const equippedOutfit = computed(() => config.value?.outfits?.find(
  outfit => outfit.id === snapshot.value?.loadout?.outfitId,
) ?? null)
const leftActionCue = computed(() => snapshot.value?.actionCues?.find(cue => cue.hand === 'left') ?? null)
const rightActionCue = computed(() => snapshot.value?.actionCues?.find(cue => cue.hand === 'right') ?? null)
const storyPage = computed(() => storyPages.value[storyIndex.value] ?? null)
const storyOpen = computed(() => storyPages.value.length > 0 && !!storyPage.value)
const storyFinalLabel = computed(() => snapshot.value?.phase === 'planning'
  ? t.value.storyBegin
  : t.value.storyClose)
const controlSchemeLabel = computed(() => (
  CONTROL_SCHEME_OPTIONS.find(option => option.id === controlScheme.value)?.label ?? 'DeepList'
))
const controlSchemeSummary = computed(() => t.value.schemeSummaries[controlScheme.value])
const feedbackStatusLabel = computed(() => {
  switch (snapshot.value?.feedback?.status) {
    case 'vibration': return t.value.vibration
    case 'enhanced': return t.value.enhanced
    case 'unavailable': return t.value.feedbackUnavailable
    case 'error': return t.value.feedbackError
    default: return t.value.controlsOnly
  }
})
const feedbackIntensityPercent = computed(() => Math.round(feedbackPreferences.value.intensity * 100))

const activeKeyboardDefinition = computed(() => (
  controlScheme.value === 'dualsense'
    ? config.value?.input.dualsense?.keyboard
    : config.value?.input.mylorik?.keyboard
))

const keyboardActionText = computed(() => {
  if (controlScheme.value === 'legacy') {
    const left = config.value?.input.leftKeys.map(formatKey).join('/') || 'J/Space'
    const right = config.value?.input.rightKeys.map(formatKey).join('/') || 'K/Shift'
    return `${t.value.keyboardAttack} ${left} + ${right} · ${t.value.keyboardInteract}`
  }
  const bindings = activeKeyboardDefinition.value
  const leftTechnique = bindings?.leftTechniqueKeys.map(formatKey).join('/') || 'Q'
  const rightTechnique = bindings?.rightTechniqueKeys.map(formatKey).join('/') || 'E'
  const mobility = bindings?.mobilityKeys.map(formatKey).join('/') || 'Space'
  const interactKey = bindings?.interactKeys.map(formatKey).join('/') || 'E'
  return `LMB/RMB = ${t.value.strike} · ${leftTechnique}/${rightTechnique} = ${t.value.technique} · ${mobility} = ${t.value.mobility} · ${interactKey} = ${t.value.interact}`
})

const mouseActionText = computed(() => controlScheme.value === 'legacy'
  ? t.value.mouseAttack
  : `LMB/RMB = ${t.value.strike}`)

const gamepadBindingLines = computed(() => {
  if (controlScheme.value === 'legacy') {
    return [
      `${t.value.gamepadMove} · ${t.value.gamepadAttack} ${formatGamepadButton(config.value?.input.gamepadLeftButton, 4)} / ${formatGamepadButton(config.value?.input.gamepadRightButton, 5)}`,
      t.value.gamepadInteract,
      t.value.gamepadMenu,
    ]
  }
  if (controlScheme.value === 'mylorik') {
    const gamepad = config.value?.input.mylorik?.gamepad
    return [
      `${t.value.gamepadMove} · ${formatGamepadButton(gamepad?.leftBumper, 4)}/${formatGamepadButton(gamepad?.rightBumper, 5)} = ${t.value.strike}`,
      `${formatGamepadButton(gamepad?.leftTrigger, 6)}/${formatGamepadButton(gamepad?.rightTrigger, 7)} = ${t.value.technique} · ${formatGamepadButton(gamepad?.mobilityButton, 1)} = ${t.value.mobility}`,
      `${formatGamepadButton(gamepad?.interactButton, 0)} = ${t.value.interact} · ${t.value.gamepadMenu}`,
    ]
  }
  const gamepad = config.value?.input.dualsense?.gamepad
  return [
    `${t.value.gamepadMove} · ${formatGamepadButton(gamepad?.leftBumper, 4)}/${formatGamepadButton(gamepad?.rightBumper, 5)} = ${t.value.instantMoves}`,
    `${formatGamepadButton(gamepad?.leftTrigger, 6)}/${formatGamepadButton(gamepad?.rightTrigger, 7)} = ${t.value.comboTriggers}`,
    `${formatGamepadButton(gamepad?.circle, 1)} = ${t.value.back} · ${formatGamepadButton(gamepad?.cross, 0)} = ${t.value.interactConfirm} · Options = ${t.value.pause}`,
  ]
})

const controlGuideItems = computed(() => {
  if (controlScheme.value === 'legacy') {
    return LAST_CHANCES_GESTURES.map(gesture => t.value[gesture])
  }
  if (controlScheme.value === 'mylorik') {
    return [t.value.strike, t.value.techniqueTap, t.value.techniqueHold, t.value.mobility, t.value.interact]
  }
  return [
    `L1/R1 · ${t.value.instantMoves}`,
    `L2/R2 · ${t.value.comboTriggers}`,
    `Circle · ${t.value.back}`,
    `Cross · ${t.value.interactConfirm}`,
    feedbackStatusLabel.value,
  ]
})

const erosionStats = computed(() => {
  if (!config.value || !snapshot.value) return []
  const base = config.value.player.baseStats
  const current = snapshot.value.player.stats
  return [
    { label: t.value.hpLost, value: base.maxHp - current.maxHp, icon: HeartPulse },
    { label: t.value.mindLost, value: base.maxMentalHealth - current.maxMentalHealth, icon: Brain },
    { label: t.value.speedLost, value: base.moveSpeed - current.moveSpeed, icon: Footprints },
    { label: t.value.armorLost, value: base.armor - current.armor, icon: Shield },
    { label: t.value.attackLost, value: base.attackPower - current.attackPower, icon: Swords },
  ].filter(stat => stat.value > 0)
})

function physicalGestureLabel(
  weapon: LastChancesResolvedWeapon,
  gesture: typeof LAST_CHANCES_GESTURES[number],
): string {
  if (controlScheme.value === 'legacy') return t.value[gesture]
  if (controlScheme.value === 'mylorik') {
    const activations = weapon.controls?.mylorik.activations
      .filter(activation => activation.gesture === gesture)
      .sort((left, right) => right.priority - left.priority) ?? []
    if (activations.length === 0) return locale.value === 'ru' ? 'Недоступно' : 'Unavailable'
    const labels = activations.map((activation) => {
      const intent = activation.intent === 'strike'
        ? t.value.strike
        : activation.intent === 'technique' ? t.value.technique : t.value.mobility
      const phase = activation.phase === 'press'
        ? t.value.press
        : activation.phase === 'tap'
          ? t.value.tap
          : activation.phase === 'hold' ? t.value.hold : t.value.release
      return `${intent} · ${phase}`
    })
    return [...new Set(labels)].join(' / ')
  }
  if (weapon.controls?.dualsense.instantGesture === gesture) {
    return `${weapon.hand === 'left' ? 'R1' : 'L1'} · ${t.value.instantMoves}`
  }
  if (weapon.controls?.dualsense.nodes.some(node => node.gesture === gesture)) {
    return `${weapon.hand === 'left' ? 'R2' : 'L2'} · ${weapon.controls.dualsense.triggerRole}`
  }
  return locale.value === 'ru' ? 'Недоступно' : 'Unavailable'
}

const weaponCooldowns = computed<WeaponCooldown[]>(() => {
  if (!config.value) return []
  const weapons = [equippedLoadout.value.left, equippedLoadout.value.right]
    .flatMap(weapon => weapon ? [weapon] : [])
  return weapons.map((weapon) => {
    const cue = snapshot.value?.actionCues?.find(candidate => candidate.hand === weapon.hand)
    const state = snapshot.value?.weaponStates?.find(candidate => (
      candidate.hand === weapon.hand && candidate.weaponId === weapon.id
    ))
    const quest = snapshot.value?.moveQuests?.find(candidate => candidate.hand === weapon.hand)
    const chargeMaxMs = cue?.chargeMaxMs ?? 0
    return {
      hand: weapon.hand === 'left' ? 'primary' as const : 'secondary' as const,
      name: weapon.name,
      input: snapshot.value?.gestureInputs.find(input => input.hand === weapon.hand),
      cue,
      controlCue: snapshot.value?.controlCue?.hand === null
        || snapshot.value?.controlCue?.hand === weapon.hand
        ? snapshot.value?.controlCue ?? undefined
        : undefined,
      controlRole: snapshot.value?.controlRoles?.find(role => role.hand === weapon.hand),
      state,
      chargeMaxMs,
      gestures: LAST_CHANCES_GESTURES.map((gesture) => {
        const attack = weapon.attacks[gesture]
        const cooldown = snapshot.value?.cooldowns.find(item => (
          item.hand === weapon.hand && item.gesture === gesture
        ))
        const lastGesture = snapshot.value?.lastGesture
        const locked = quest ? quest.unlocked[gesture] === false : false
        const enabled = attack.enabled !== false && attack.behavior !== 'disabled' && !locked
        const recoveryMs = Math.max(cue?.recoveryMs ?? 0, state?.recoveryMs ?? 0)
        const ready = enabled && (cooldown?.ready ?? (
          (cooldown?.remainingMs ?? 0) <= 0
          && recoveryMs <= 0
          && cue?.phase !== 'recovery'
        ))
        return {
          key: gesture,
          physicalLabel: physicalGestureLabel(weapon, gesture),
          name: gesture === 'tap'
            ? weapon.tapCombo.map(attack => attack.name).join(' → ')
            : attack.name,
          description: attack.description,
          remainingMs: cooldown?.remainingMs ?? 0,
          totalMs: cooldown?.totalMs ?? attack.cooldownMs,
          enabled,
          ready,
          locked,
          contextDimmed: weapon.trait === 'swordRhythm'
            && gesture === 'doubleTapHold'
            && !state?.unterhauPrimed,
          primed: weapon.trait === 'swordRhythm'
            && gesture === 'doubleTapHold'
            && state?.unterhauPrimed === true,
          color: attack.color,
          active: enabled && (
            (cue?.gesture === gesture
              && ['candidate', 'charging', 'armed'].includes(cue.phase))
            || (!!lastGesture
              && lastGesture.hand === weapon.hand
              && lastGesture.gesture === gesture
              && (snapshot.value?.elapsedMs ?? 0) - lastGesture.atMs < 450)
          ),
        }
      }),
    }
  })
})

const moveQuestPanels = computed(() => {
  const quests = snapshot.value?.moveQuests ?? []
  return quests.map((quest) => {
    const weapon = quest.hand === 'left'
      ? equippedLoadout.value.left
      : equippedLoadout.value.right
    const prompt = (gesture: typeof LAST_CHANCES_GESTURES[number]) => (
      weapon ? physicalGestureLabel(weapon, gesture) : t.value[gesture]
    )
    const items = [
      {
        key: 'tap',
        label: controlScheme.value === 'legacy'
          ? `${t.value.questTapTask} → ${t.value.doubleTap}`
          : `${t.value.questKillsWith} ${prompt('tap')} → ${prompt('doubleTap')}`,
        done: quest.tapQuestDone,
        progress: `${Math.min(quest.roomKills.tap, quest.killsRequired)}/${quest.killsRequired}`,
      },
      {
        key: 'hold',
        label: controlScheme.value === 'legacy'
          ? `${t.value.questHoldTask} → ${t.value.holdThenDoubleTap}`
          : `${t.value.questKillsWith} ${prompt('hold')} → ${prompt('holdThenDoubleTap')}`,
        done: quest.holdQuestDone,
        progress: `${Math.min(quest.roomKills.hold, quest.killsRequired)}/${quest.killsRequired}`,
      },
      ...(quest.tapQuestDone && quest.holdQuestDone
        ? [{
            key: 'combo',
            label: controlScheme.value === 'legacy'
              ? `${t.value.questComboTask} → ${t.value.doubleTapHold}`
              : `${t.value.questEliteRoutes} → ${prompt('doubleTapHold')}`,
            done: quest.comboQuestDone,
            progress: `${quest.comboGesturesHit.length}/${quest.comboGesturesRequired.length}`,
          }]
        : []),
    ]
    return {
      hand: quest.hand,
      title: quest.hand === 'left' ? t.value.questHandLeft : t.value.questHandRight,
      items,
      pending: quest.pendingUnlocks.map(gesture => prompt(gesture)).join(', '),
      allDone: quest.comboQuestDone,
    }
  })
})
const moveQuestsComplete = computed(() => (
  moveQuestPanels.value.length > 0 && moveQuestPanels.value.every(panel => panel.allDone)
))

const gamepadStatusText = computed(() => {
  const gamepad = snapshot.value?.gamepad
  if (!gamepad?.supported) return t.value.gamepadUnsupported
  if (!gamepad.connected) return t.value.gamepadDisconnected
  const padNumber = gamepad.activeIndex === null ? '' : ` #${gamepad.activeIndex + 1}`
  const profile = gamepad.profile ? ` · ${gamepad.profile}` : ''
  return `${t.value.gamepadConnected}${padNumber} · ${gamepad.id ?? t.value.gamepad}${profile}`
})

const runMapNodes = computed<RunMapNode[]>(() => {
  if (!plan.value || !snapshot.value) return []
  const current = snapshot.value.currentNodeId
  const available = new Set(snapshot.value.availableNodeIds)
  const attempt = new Set(snapshot.value.attemptPath)
  return plan.value.nodes.map((node) => {
    let state: RunMapNode['state'] = 'locked'
    if (available.has(node.id)) state = 'available'
    else if (node.id === current && ['playing', 'interaction'].includes(snapshot.value?.phase ?? '')) state = 'current'
    else if (attempt.has(node.id)) state = 'cleared'
    else if (visitedNodeIds.value.has(node.id)) state = 'visited'
    const tier = config.value?.progression.tiers[node.tierIndex]
    return {
      id: node.id,
      name: node.label,
      tier: node.tierIndex + 1,
      kind: tier?.kind === 'boss' || node.altar ? 'boss' : node.roomArchetype,
      state,
    }
  })
})

const runMapEdges = computed<RunMapEdge[]>(() => plan.value?.nodes.flatMap(node => (
  node.nextNodeIds.map(next => ({ from: node.id, to: next }))
)) ?? [])

const phaseOverlay = computed(() => {
  const state = snapshot.value
  if (!state) return null
  if (state.altarPrompt) return null
  if (state.paused && state.phase === 'playing') {
    return { kind: 'paused', eyebrow: t.value.pause, title: t.value.pauseTitle, body: t.value.pauseBody }
  }
  if (state.phase === 'dead') {
    return { kind: 'dead', eyebrow: t.value.deathEyebrow, title: t.value.deathTitle, body: t.value.deathBody }
  }
  if (state.phase === 'outOfChances') {
    return { kind: 'game-over', eyebrow: t.value.gameOverEyebrow, title: t.value.gameOverTitle, body: t.value.gameOverBody }
  }
  if (state.phase === 'won') {
    return { kind: 'victory', eyebrow: t.value.victoryEyebrow, title: t.value.victoryTitle, body: t.value.victoryBody }
  }
  return null
})

const recentGesture = computed(() => {
  const state = snapshot.value
  if (!state?.lastGesture || !['playing', 'planning'].includes(state.phase) || routeMapOpen.value) return null
  return state.elapsedMs - state.lastGesture.atMs < 850 ? state.lastGesture : null
})

function beginStory(pages: LastChancesStoryPage[] | undefined) {
  storyPages.value = pages ? pages.map(page => ({ ...page })) : []
  storyIndex.value = 0
  if (storyPages.value.length > 0) updateRouteMapVisibility(false)
}

function advanceStory() {
  if (!storyOpen.value) return
  if (storyIndex.value < storyPages.value.length - 1) {
    storyIndex.value += 1
    return
  }
  storyPages.value = []
  storyIndex.value = 0
  if (snapshot.value?.phase === 'planning') updateRouteMapVisibility(true)
  void nextTick(() => canvas.value?.focus())
}

function setToast(message: string) {
  toast.value = message
  if (toastTimer !== null) window.clearTimeout(toastTimer)
  toastTimer = window.setTimeout(() => { toast.value = '' }, 3200)
}

function changeControlScheme(event: Event) {
  const next = (event.target as HTMLSelectElement).value as LastChancesControlScheme
  if (!CONTROL_SCHEME_OPTIONS.some(option => option.id === next) || next === controlScheme.value) return
  controlScheme.value = next
  saveLastChancesControlScheme(next)
  engine.value?.setControlScheme(next)
  setToast(`${t.value.controlSchemeChanged} ${controlSchemeLabel.value}.`)
}

function updateFeedbackPreferences() {
  const next = {
    mode: feedbackPreferences.value.mode,
    intensity: Math.max(0, Math.min(1, Number(feedbackPreferences.value.intensity) || 0)),
  } satisfies LastChancesFeedbackPreferences
  feedbackPreferences.value = next
  saveLastChancesFeedbackPreferences(next)
  engine.value?.setFeedbackPreferences(next)
  setToast(t.value.feedbackUpdated)
}

async function enableDualSenseFeatures() {
  if (!engine.value) return
  try {
    const enabled = await engine.value.enableDualSenseFeatures()
    const unsupported = snapshot.value?.feedback?.permission === 'unavailable'
    setToast(enabled
      ? t.value.dualSenseEnabled
      : unsupported ? t.value.dualSenseUnsupported : t.value.dualSenseFailed)
  } catch {
    setToast(t.value.dualSenseFailed)
  }
}

function disableEnhancedFeedback() {
  engine.value?.disableEnhancedFeedback()
  setToast(t.value.controlsOnly)
}

function handleControllerUiCommand(command: 'confirm' | 'back' | 'pause'): boolean {
  if (command === 'confirm') {
    if (!storyOpen.value) return false
    advanceStory()
    return true
  }
  if (command === 'back') {
    if (builderOpen.value) {
      closeBuilder()
      return true
    }
    if (routeMapOpen.value && (snapshot.value?.phase === 'playing'
      || (snapshot.value?.phase === 'planning' && snapshot.value.currentNodeId))) {
      closeMap()
      return true
    }
    return false
  }
  if (snapshot.value?.phase !== 'playing') return false
  togglePause()
  return true
}

function onSnapshot(nextSnapshot: LastChancesSnapshot) {
  const previousPhase = snapshot.value?.phase
  const previousGeneration = snapshot.value?.generation
  if (previousGeneration !== undefined && previousGeneration !== nextSnapshot.generation) {
    visitedNodeIds.value = new Set()
  }
  const nextVisited = new Set(visitedNodeIds.value)
  nextSnapshot.attemptPath.forEach(id => nextVisited.add(id))
  visitedNodeIds.value = nextVisited
  snapshot.value = nextSnapshot
  if (nextSnapshot.phase === 'planning' && nextSnapshot.currentNodeId === null
    && nextSnapshot.availableNodeIds.length > 0 && !storyOpen.value) {
    updateRouteMapVisibility(true)
  }
  if (previousPhase === 'planning' && nextSnapshot.phase === 'playing') {
    updateRouteMapVisibility(false)
    resumeAfterMap = false
    void nextTick(() => canvas.value?.focus())
  }
  if (previousPhase !== 'won' && nextSnapshot.phase === 'won' && config.value?.narrative) {
    const narrative = config.value.narrative
    beginStory(nextSnapshot.totalDeaths >= narrative.exhaustedDeathThreshold
      ? narrative.exhaustedVictory
      : narrative.victory)
  }
  if (previousPhase !== 'outOfChances'
    && nextSnapshot.phase === 'outOfChances'
    && config.value?.narrative) {
    beginStory(config.value.narrative.exhaustedVictory)
  }
}

async function createEngine(nextConfig: LastChancesConfig) {
  engine.value?.destroy()
  engine.value = null
  config.value = cloneLastChancesConfig(nextConfig)
  beginStory(config.value.narrative?.prologue)
  plan.value = null
  snapshot.value = null
  visitedNodeIds.value = new Set()
  await nextTick()
  if (!canvas.value) throw new Error('99LC canvas is unavailable')
  const instance = new LastChancesEngine(canvas.value, config.value, {
    onPlan: nextPlan => { plan.value = nextPlan },
    onSnapshot,
    onUiCommand: handleControllerUiCommand,
  }, {
    controlScheme: controlScheme.value,
    feedbackPreferences: { ...feedbackPreferences.value },
    qaFixture: qaControlsFixture ? 'controls' : undefined,
  })
  engine.value = instance
  instance.start()
  updateRouteMapVisibility(!storyOpen.value)
}

async function loadDefinition(useBrowserOverride = true) {
  loadController?.abort()
  loadController = new AbortController()
  loading.value = true
  loadError.value = ''
  try {
    const loaded = await loadLastChancesConfig({
      signal: loadController.signal,
      useBrowserOverride,
    })
    await createEngine(loaded)
  } catch (error) {
    if (loadController.signal.aborted) return
    loadError.value = error instanceof Error ? error.message : String(error)
  } finally {
    if (!loadController.signal.aborted) loading.value = false
  }
}

function chooseNode(nodeId: string) {
  if (!engine.value?.chooseNode(nodeId)) return
  updateRouteMapVisibility(false)
  resumeAfterMap = false
  void nextTick(() => canvas.value?.focus())
}

function openMap() {
  if (!plan.value || !snapshot.value) return
  resumeAfterMap = snapshot.value.phase === 'playing' && !snapshot.value.paused
  if (resumeAfterMap) engine.value?.setPaused(true)
  updateRouteMapVisibility(true)
}

function closeMap() {
  updateRouteMapVisibility(false)
  if (resumeAfterMap && snapshot.value?.phase === 'playing') engine.value?.setPaused(false)
  resumeAfterMap = false
  void nextTick(() => canvas.value?.focus())
}

function togglePause() {
  if (!snapshot.value || snapshot.value.phase !== 'playing') return
  engine.value?.setPaused(!snapshot.value.paused)
  void nextTick(() => canvas.value?.focus())
}

function retryAttempt() {
  if (!engine.value?.retryAttempt()) return
  updateRouteMapVisibility(snapshot.value?.altarPrompt ? false : true)
}

function newGeneration() {
  beginStory(config.value?.narrative?.prologue)
  engine.value?.newGeneration()
  updateRouteMapVisibility(!storyOpen.value)
  builderOpen.value = false
}

function chooseInteraction(choiceId: string) {
  engine.value?.chooseInteraction(choiceId)
}

function resolveAltar(accept: boolean) {
  if (!engine.value?.resolveAltar(accept)) return
  void nextTick(() => canvas.value?.focus())
}

function openBuilder() {
  resumeAfterBuilder = snapshot.value?.phase === 'playing' && !snapshot.value.paused
  if (resumeAfterBuilder) engine.value?.setPaused(true)
  builderOpen.value = true
}

function closeBuilder() {
  builderOpen.value = false
  if (resumeAfterBuilder && snapshot.value?.phase === 'playing') engine.value?.setPaused(false)
  resumeAfterBuilder = false
  void nextTick(() => canvas.value?.focus())
}

async function applyBuilder(nextConfig: LastChancesConfig) {
  try {
    await createEngine(nextConfig)
    builderOpen.value = false
    resumeAfterBuilder = false
    setToast(t.value.applied)
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : String(error)
  }
}

function applyBuilderControls(nextConfig: LastChancesConfig) {
  if (!engine.value?.applyControlDefinition(nextConfig)) {
    setToast(t.value.controlsRejected)
    return
  }
  if (config.value) {
    const source = cloneLastChancesConfig(nextConfig)
    config.value.input = source.input
    const sourceWeapons = new Map(source.weapons.map(weapon => [weapon.id, weapon]))
    config.value.weapons.forEach((weapon) => {
      const controls = sourceWeapons.get(weapon.id)?.controls
      if (controls) weapon.controls = JSON.parse(JSON.stringify(controls)) as typeof controls
      else delete weapon.controls
    })
  }
  setToast(t.value.controlsApplied)
}

function saveBuilderOverride(nextConfig: LastChancesConfig) {
  try {
    saveLastChancesConfigOverride(nextConfig)
    setToast(t.value.saved)
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : String(error)
  }
}

function clearBuilderOverride() {
  clearLastChancesConfigOverride()
  setToast(t.value.cleared)
}

async function recoverDefinition() {
  clearLastChancesConfigOverride()
  await loadDefinition(false)
  if (!loadError.value) setToast(t.value.cleared)
}

function touchHand(hand: AttackHand): LastChancesHand {
  return hand === 'primary' ? 'left' : 'right'
}

function setTouchMove(x: number, y: number) {
  engine.value?.setTouchMove(x, y)
}

function setTouchAim(x: number, y: number) {
  engine.value?.setTouchAim(x, y)
}

function pressTouch(hand: AttackHand) {
  engine.value?.press(touchHand(hand))
}

function releaseTouch(hand: AttackHand) {
  engine.value?.release(touchHand(hand))
}

function interact() {
  if (!snapshot.value?.interactionPrompt || !engine.value?.interact()) return
  void nextTick(() => canvas.value?.focus())
}

function updateRouteMapVisibility(visible: boolean) {
  routeMapOpen.value = visible
  engine.value?.setRouteMapVisible(visible)
}

function syncFullscreenState() {
  isFullscreen.value = document.fullscreenElement === pageRoot.value
  void nextTick(() => canvas.value?.focus())
}

async function toggleFullscreen() {
  if (!pageRoot.value) return
  try {
    if (document.fullscreenElement === pageRoot.value) await document.exitFullscreen()
    else await pageRoot.value.requestFullscreen({ navigationUI: 'hide' })
  } catch (error) {
    setToast(error instanceof Error ? error.message : String(error))
  }
}

function blockFullscreenContextMenu(event: MouseEvent) {
  if (isFullscreen.value) event.preventDefault()
}

function formatKey(code: string): string {
  if (code.startsWith('Key')) return code.slice(3)
  if (code.startsWith('Digit')) return code.slice(5)
  return code.replace('Arrow', '↑').replace('Space', 'Space')
}

function formatGamepadButton(index: number | undefined, fallback: number): string {
  const resolved = index ?? fallback
  const labels: Record<number, string> = {
    0: '× Cross',
    1: '○ Circle',
    2: '□ Square',
    3: '△ Triangle',
    4: 'L1',
    5: 'R1',
    6: 'L2',
    7: 'R2',
  }
  return labels[resolved] ?? `#${resolved}`
}

function formatNumber(value: number): string {
  return Math.abs(value % 1) < 0.01 ? String(Math.round(value)) : value.toFixed(1)
}

function deathReason(): string {
  const reason = snapshot.value?.deathReason
  if (!reason) return t.value.attemptEnded
  if (reason.startsWith('Mental health collapsed')) return t.value.mindCollapsed
  if (reason.startsWith('Killed by ')) return `${t.value.killedBy} ${reason.slice('Killed by '.length)}`
  return reason
}

onMounted(() => {
  document.addEventListener('fullscreenchange', syncFullscreenState)
  void loadDefinition()
})

onBeforeUnmount(() => {
  loadController?.abort()
  engine.value?.destroy()
  document.removeEventListener('fullscreenchange', syncFullscreenState)
  if (toastTimer !== null) window.clearTimeout(toastTimer)
})
</script>

<template>
  <div
    ref="pageRoot"
    class="lc-page"
    :class="{
      'is-mental-low': mentalPercent < 35,
      'is-critical': hpPercent < 30,
    }"
    @contextmenu="blockFullscreenContextMenu"
  >
    <header class="lc-page-header">
      <div class="lc-title-lockup">
        <div class="lc-title-sigil" aria-hidden="true"><span>99</span><i /></div>
        <div>
          <p>{{ t.eyebrow }}</p>
          <h1>{{ t.title }}</h1>
          <span>{{ t.subtitle }}</span>
        </div>
      </div>

      <div class="lc-header-actions">
        <span class="lc-live-state" :class="{ active: !!snapshot && !loading }">
          <i aria-hidden="true" />{{ snapshot && !loading ? t.online : t.waiting }}
        </span>
        <button
          type="button"
          :disabled="!plan"
          :class="{ 'is-route-ready': snapshot?.phase === 'planning' && !!snapshot.currentNodeId && !routeMapOpen }"
          @click="openMap"
        >
          <MapIcon :size="15" aria-hidden="true" />{{ t.map }}
        </button>
        <button type="button" @click="toggleFullscreen">
          <Minimize2 v-if="isFullscreen" :size="15" aria-hidden="true" />
          <Maximize2 v-else :size="15" aria-hidden="true" />
          {{ isFullscreen ? t.exitFullscreen : t.fullscreen }}
        </button>
        <button type="button" :disabled="snapshot?.phase !== 'playing'" @click="togglePause">
          <Play v-if="snapshot?.paused" :size="15" aria-hidden="true" />
          <Pause v-else :size="15" aria-hidden="true" />
          {{ snapshot?.paused ? t.resume : t.pause }}
        </button>
        <button type="button" :disabled="!engine" @click="newGeneration">
          <Dices :size="15" aria-hidden="true" />{{ t.newGeneration }}
        </button>
        <button type="button" class="is-builder" :disabled="!config" @click="openBuilder">
          <Settings2 :size="15" aria-hidden="true" />{{ t.builder }}
        </button>
      </div>
    </header>

    <main class="lc-cockpit">
      <section class="lc-stage-panel" aria-label="Game stage">
        <div class="lc-stage-screen">
          <canvas ref="canvas" class="lc-canvas" tabindex="0" :aria-label="t.stageLabel" />

          <div v-if="snapshot" class="lc-stage-hud" aria-live="polite">
            <div class="lc-vitals is-physical">
              <div class="lc-vital-label">
                <HeartPulse :size="13" aria-hidden="true" />
                <span>{{ t.physical }}</span>
                <strong>{{ Math.ceil(snapshot.player.hp) }} / {{ Math.ceil(snapshot.player.stats.maxHp) }}</strong>
              </div>
              <span class="lc-vital-track"><i :style="{ width: `${hpPercent}%` }" /></span>
            </div>

            <div class="lc-room-readout">
              <small>{{ t.tier }} {{ (snapshot.currentTierIndex ?? 0) + 1 }} / {{ config?.progression.tiers.length ?? 7 }}</small>
              <strong>{{ currentNode?.roomName ?? t.noRoom }}</strong>
              <span>{{ t.enemies }} · {{ livingEnemies.length }}</span>
            </div>

            <div class="lc-vitals is-mental">
              <div class="lc-vital-label">
                <Brain :size="13" aria-hidden="true" />
                <span>{{ t.mental }}</span>
                <strong>{{ Math.ceil(snapshot.player.mentalHealth) }} / {{ Math.ceil(snapshot.player.stats.maxMentalHealth) }}</strong>
              </div>
              <span class="lc-vital-track"><i :style="{ width: `${mentalPercent}%` }" /></span>
            </div>
          </div>

          <Transition name="lc-gesture-pop">
            <div v-if="recentGesture" :key="recentGesture.atMs" class="lc-gesture-toast">
              <Sparkles :size="13" aria-hidden="true" />
              <span>{{ t.recognized }}</span>
              <strong>{{ recentGesture.attackName }}{{ recentGesture.comboStep ? ` · ${t.combo} ×${recentGesture.comboStep}` : '' }}</strong>
            </div>
          </Transition>

          <Transition name="lc-gesture-pop">
            <div
              v-if="controlScheme !== 'legacy' && snapshot && ['playing', 'planning'].includes(snapshot.phase) && !routeMapOpen && snapshot.controlCue"
              :key="snapshot.controlCue.atMs"
              class="lc-semantic-control-cue"
              :class="`is-${snapshot.controlCue.state}`"
              role="status"
              data-testid="semantic-control-cue"
            >
              <span>{{ t.controlStates[snapshot.controlCue.state] }}</span>
              <strong>{{ snapshot.controlCue.label }}</strong>
              <small v-if="snapshot.controlCue.tactileProfile">
                {{ snapshot.controlCue.tactileProfile }}
              </small>
            </div>
          </Transition>

          <Transition name="lc-gesture-pop">
            <button
              v-if="snapshot?.phase === 'planning' && snapshot.currentNodeId && snapshot.availableNodeIds.length && !routeMapOpen"
              class="lc-route-ready"
              type="button"
              @click="openMap"
            >
              <MapIcon :size="17" aria-hidden="true" />
              <strong>{{ t.routeReady }}</strong>
              <span>{{ t.chooseRoute }}</span>
            </button>
          </Transition>

          <Transition name="lc-gesture-pop">
            <button
              v-if="snapshot?.interactionPrompt"
              class="lc-arena-interaction"
              type="button"
              :disabled="snapshot.paused || !['playing', 'planning'].includes(snapshot.phase)"
              data-testid="interaction-prompt"
              @click="interact"
            >
              <Sparkles :size="15" aria-hidden="true" />
              <span>{{ t.interact }}</span>
              <strong>{{ snapshot.interactionPrompt }}</strong>
            </button>
          </Transition>

          <Transition name="lc-phase-fade">
            <div v-if="snapshot?.altarPrompt" class="lc-interaction-overlay lc-altar-overlay">
              <div class="lc-interaction-card">
                <p>{{ t.altarEyebrow }}</p>
                <h2>{{ snapshot.altarPrompt.prompt }}</h2>
                <span>{{ snapshot.altarPrompt.available ? t.interactionChoose : t.altarUnavailable }}</span>
                <div class="lc-interaction-choices lc-altar-choices">
                  <button
                    type="button"
                    :disabled="!snapshot.altarPrompt.available"
                    data-testid="altar-accept"
                    @click="resolveAltar(true)"
                  >
                    <strong>{{ t.altarAccept }}</strong>
                    <small>{{ t.interactionCost }}: −{{ snapshot.altarPrompt.chanceCost }} {{ t.chancePlural }}</small>
                  </button>
                  <button type="button" data-testid="altar-decline" @click="resolveAltar(false)">
                    <strong>{{ t.altarDecline }}</strong>
                    <small>{{ t.interactionChoose }}</small>
                  </button>
                </div>
              </div>
            </div>
          </Transition>

          <Transition name="lc-phase-fade">
            <div v-if="snapshot?.interaction" class="lc-interaction-overlay">
              <div class="lc-interaction-card">
                <p>{{ t.interactionEyebrow }}</p>
                <h2>{{ snapshot.interaction.title }}</h2>
                <span>{{ snapshot.interaction.body }}</span>
                <div class="lc-interaction-choices">
                  <button
                    v-for="choice in snapshot.interaction.choices"
                    :key="choice.id"
                    type="button"
                    :disabled="!choice.available"
                    :class="{ 'is-controller-selected': snapshot.selectedInteractionChoiceId === choice.id }"
                    :aria-current="snapshot.selectedInteractionChoiceId === choice.id ? 'true' : undefined"
                    @click="chooseInteraction(choice.id)"
                  >
                    <strong>{{ choice.label }}</strong>
                    <span>{{ choice.description }}</span>
                    <small v-if="choice.effect.chanceCost">
                      {{ t.interactionCost }}: −{{ choice.effect.chanceCost }} {{ choice.effect.chanceCost === 1 ? t.chanceSingular : t.chancePlural }}
                    </small>
                    <small v-else>{{ choice.available ? t.interactionChoose : t.interactionUnavailable }}</small>
                  </button>
                </div>
              </div>
            </div>
          </Transition>

          <Transition name="lc-phase-fade">
            <div v-if="phaseOverlay" class="lc-phase-overlay" :class="`is-${phaseOverlay.kind}`">
              <div class="lc-phase-card">
                <Skull v-if="phaseOverlay.kind === 'dead' || phaseOverlay.kind === 'game-over'" :size="30" aria-hidden="true" />
                <Trophy v-else-if="phaseOverlay.kind === 'victory'" :size="30" aria-hidden="true" />
                <Pause v-else :size="30" aria-hidden="true" />
                <p>{{ phaseOverlay.eyebrow }}</p>
                <h2>{{ phaseOverlay.title }}</h2>
                <span v-if="phaseOverlay.kind === 'dead' || phaseOverlay.kind === 'game-over'" class="lc-death-reason">{{ deathReason() }}</span>
                <div v-if="snapshot" class="lc-overlay-chances">
                  <strong>{{ snapshot.chances }}</strong>
                  <span>{{ snapshot.chances === 1 ? t.chanceSingular : t.chancePlural }}</span>
                </div>
                <p class="lc-phase-body">{{ phaseOverlay.body }}</p>
                <div class="lc-phase-actions">
                  <button v-if="phaseOverlay.kind === 'paused'" type="button" class="is-primary" @click="togglePause">
                    <Play :size="15" aria-hidden="true" />{{ t.resume }}
                  </button>
                  <button v-if="phaseOverlay.kind === 'dead'" type="button" class="is-primary" @click="retryAttempt">
                    <RotateCcw :size="15" aria-hidden="true" />{{ t.retry }}
                  </button>
                  <button v-if="phaseOverlay.kind === 'game-over' || phaseOverlay.kind === 'victory'" type="button" class="is-primary" @click="newGeneration">
                    <Dices :size="15" aria-hidden="true" />{{ t.beginAgain }}
                  </button>
                  <button type="button" @click="openBuilder"><Settings2 :size="15" aria-hidden="true" />{{ t.builder }}</button>
                </div>
              </div>
            </div>
          </Transition>

          <Transition name="lc-phase-fade">
            <div v-if="storyOpen && storyPage" class="lc-story-overlay">
              <article class="lc-story-card">
                <small>{{ storyPage.speaker || t.title }}</small>
                <p>{{ storyPage.text }}</p>
                <footer>
                  <span>{{ storyIndex + 1 }} / {{ storyPages.length }}</span>
                  <button type="button" class="is-primary" @click="advanceStory">
                    {{ storyIndex < storyPages.length - 1 ? t.storyContinue : storyFinalLabel }}
                    <ChevronRight :size="15" aria-hidden="true" />
                  </button>
                </footer>
              </article>
            </div>
          </Transition>

          <div v-if="loading || loadError" class="lc-loading-overlay">
            <div class="lc-loading-mark" :class="{ 'is-error': loadError }">
              <TriangleAlert v-if="loadError" :size="25" aria-hidden="true" />
              <CircleDotDashed v-else :size="25" aria-hidden="true" />
            </div>
            <h2>{{ loadError ? t.loadError : t.loading }}</h2>
            <p>{{ loadError || t.loadingHint }}</p>
            <div v-if="loadError" class="lc-loading-actions">
              <button type="button" @click="loadDefinition()"><RefreshCw :size="15" aria-hidden="true" />{{ t.reload }}</button>
              <button type="button" @click="recoverDefinition"><RotateCcw :size="15" aria-hidden="true" />{{ t.restoreDefault }}</button>
            </div>
          </div>

          <TouchControls
            :locale="locale"
            :legacy-label="t.touchLegacy"
            :primary-name="leftWeapon?.name ?? '—'"
            :secondary-name="rightWeapon?.name ?? '—'"
            :primary-cue="leftActionCue"
            :secondary-cue="rightActionCue"
            :primary-available="!!leftWeapon"
            :secondary-available="!!rightWeapon"
            :interaction-prompt="snapshot?.interactionPrompt ?? null"
            :disabled="!snapshot || !['playing', 'planning'].includes(snapshot.phase) || snapshot.paused || routeMapOpen"
            @move="setTouchMove"
            @aim="setTouchAim"
            @press="pressTouch"
            @release="releaseTouch"
            @interact="interact"
          />
        </div>

        <footer class="lc-stage-footer">
          <span><i :class="alertedEnemies || snapshot?.turretAlarm ? 'is-alert' : ''" aria-hidden="true" />{{ t.currentThreat }} · {{ alertedEnemies || snapshot?.turretAlarm ? t.noticed : t.calm }}</span>
          <span v-if="snapshot"><Activity :size="12" aria-hidden="true" />{{ t.speed }} {{ formatNumber(snapshot.player.stats.moveSpeed) }} · {{ t.armor }} {{ formatNumber(snapshot.player.stats.armor) }}</span>
          <span v-if="(snapshot?.player.armorMultiplier ?? 1) > 1">
            <Shield :size="12" aria-hidden="true" />{{ t.armor }} ×{{ snapshot?.player.armorMultiplier }}
            · {{ Math.ceil(snapshot?.player.armorMultiplierForMs ?? 0) }} ms
          </span>
          <span v-if="equippedArtifact || equippedOutfit">
            <Sparkles :size="12" aria-hidden="true" />
            {{ [equippedArtifact?.name, equippedOutfit?.name].filter(Boolean).join(' · ') }}
          </span>
        </footer>
      </section>

      <aside class="lc-telemetry">
        <section class="lc-chance-card">
          <div class="lc-chance-orb" :style="{ '--chance-progress': `${chancePercent * 3.6}deg` }">
            <span>{{ snapshot?.chances ?? config?.chances ?? 99 }}</span>
            <small>99LC</small>
          </div>
          <div>
            <p>{{ t.chances }}</p>
            <strong>{{ t.nextDeath }}</strong>
            <span>−{{ nextDeathCost }} {{ nextDeathCost === 1 ? t.chanceSingular : t.chancePlural }}</span>
          </div>
        </section>

        <section class="lc-event-card" data-testid="event-log">
          <header>
            <CircleDotDashed :size="15" aria-hidden="true" />
            <span>{{ t.events }}</span>
          </header>
          <ol v-if="recentEvents.length" aria-live="polite">
            <li v-for="entry in recentEvents" :key="entry.id">{{ entry.text }}</li>
          </ol>
          <p v-else>{{ t.eventsEmpty }}</p>
        </section>

        <section class="lc-run-card">
          <header :class="{ 'is-route-ready': snapshot?.phase === 'planning' && !!snapshot.currentNodeId && !routeMapOpen }"><MapIcon :size="15" aria-hidden="true" /><span>{{ t.map }}</span><button type="button" @click="openMap">{{ t.chooseRoute }}<ChevronRight :size="12" aria-hidden="true" /></button></header>
          <dl>
            <div><dt>{{ t.tier }}</dt><dd>{{ (snapshot?.currentTierIndex ?? 0) + 1 }} / {{ config?.progression.tiers.length ?? 7 }}</dd></div>
            <div><dt>{{ t.room }}</dt><dd>{{ currentNode?.roomName ?? t.noRoom }}</dd></div>
            <div>
              <dt>{{ t.enemies }}</dt>
              <dd>
                {{ livingEnemies.length }}
                <template v-if="snapshot?.swarm?.infinite"> +∞</template>
                <template v-else-if="snapshot?.swarm?.remaining"> +{{ snapshot.swarm.remaining }}</template>
                <template v-if="snapshot?.turrets?.some(turret => !turret.disabled)">
                  +{{ snapshot.turrets.filter(turret => !turret.disabled).length }} {{ t.turrets }}
                </template>
              </dd>
            </div>
            <div><dt>{{ t.deaths }}</dt><dd>{{ snapshot?.totalDeaths ?? 0 }}</dd></div>
            <div><dt>{{ t.generation }}</dt><dd>#{{ snapshot?.generation ?? 1 }}</dd></div>
            <div class="is-seed"><dt>{{ t.seed }}</dt><dd>{{ plan?.seed ?? config?.seed ?? '—' }}</dd></div>
          </dl>
        </section>

        <section class="lc-erosion-card">
          <header>
            <span><TriangleAlert :size="14" aria-hidden="true" />{{ t.statErosion }}</span>
            <small>{{ t.statErosionHelp }}</small>
          </header>
          <div v-if="erosionStats.length" class="lc-erosion-grid">
            <div v-for="stat in erosionStats" :key="stat.label">
              <component :is="stat.icon" :size="13" aria-hidden="true" />
              <span>{{ stat.label }}</span>
              <strong>−{{ formatNumber(stat.value) }}</strong>
            </div>
          </div>
          <p v-else>{{ t.noErosion }}</p>
        </section>

        <section v-if="moveQuestPanels.length && !moveQuestsComplete" class="lc-quest-card">
          <header><Target :size="14" aria-hidden="true" /><span>{{ t.questsTitle }}</span></header>
          <div v-for="panel in moveQuestPanels" :key="panel.hand" class="lc-quest-hand">
            <strong>{{ panel.title }}</strong>
            <ul>
              <li v-for="item in panel.items" :key="item.key" :class="{ 'is-done': item.done }">
                <span>{{ item.label }}</span>
                <b>{{ item.done ? '✓' : item.progress }}</b>
              </li>
            </ul>
            <small v-if="panel.pending">{{ t.questNextRoom }} {{ panel.pending }}</small>
          </div>
        </section>

        <WeaponCooldowns
          :locale="locale"
          :control-scheme="controlScheme"
          :weapons="weaponCooldowns"
        />
      </aside>
    </main>

    <section class="lc-controls-dock" :aria-label="t.controls">
      <header>
        <div class="lc-control-heading">
          <Eye :size="15" aria-hidden="true" />
          <span>
            <strong>{{ t.controls }}</strong>
            <small>{{ t.controlsHelp }}</small>
          </span>
        </div>
        <div class="lc-control-scheme-field">
          <label for="lc-control-scheme">{{ t.controlScheme }}</label>
          <select
            id="lc-control-scheme"
            :value="controlScheme"
            data-testid="control-scheme-select"
            @change="changeControlScheme"
          >
            <option
              v-for="option in CONTROL_SCHEME_OPTIONS"
              :key="option.id"
              :value="option.id"
            >
              {{ option.label }}
            </option>
          </select>
          <small data-testid="control-scheme-summary">{{ controlSchemeSummary }}</small>
          <small v-if="qaControlsFixture" data-testid="qa-controls-fixture">
            QA fixture · all moves unlocked
          </small>
        </div>
      </header>
      <template v-if="controlScheme === 'dualsense'">
        <div class="lc-feedback-controls">
          <div>
            <strong>{{ t.dualSenseActive }}</strong>
            <span
              :class="`is-${snapshot?.feedback?.status ?? 'controls-only'}`"
              data-testid="dualsense-capability"
            >
              {{ t.feedback }} · Tier {{ snapshot?.feedback?.tier ?? 0 }} · {{ feedbackStatusLabel }}
            </span>
            <small v-if="snapshot?.feedback?.message">{{ snapshot.feedback.message }}</small>
          </div>
          <label>
            <span>{{ t.feedbackMode }}</span>
            <select v-model="feedbackPreferences.mode" @change="updateFeedbackPreferences">
              <option v-for="option in FEEDBACK_MODE_OPTIONS" :key="option.id" :value="option.id">
                {{ option.label }}
              </option>
            </select>
          </label>
          <label class="lc-feedback-intensity">
            <span>{{ t.feedbackIntensity }} · {{ feedbackIntensityPercent }}%</span>
            <input
              v-model.number="feedbackPreferences.intensity"
              type="range"
              min="0"
              max="1"
              step="0.05"
              :disabled="feedbackPreferences.mode === 'off'"
              @change="updateFeedbackPreferences"
            />
          </label>
          <button
            v-if="snapshot?.feedback?.tier !== 2"
            type="button"
            data-testid="enable-dualsense-features"
            @click="enableDualSenseFeatures"
          >
            {{ t.enableDualSense }}
          </button>
          <button
            v-else
            type="button"
            data-testid="disable-dualsense-features"
            @click="disableEnhancedFeedback"
          >
            {{ t.disableEnhanced }}
          </button>
        </div>
        <div class="lc-tactile-legend" data-testid="dualsense-tactile-legend">
          <strong>{{ t.tactileLegend }}</strong>
          <span v-for="item in t.tactileLegendItems" :key="item">{{ item }}</span>
        </div>
      </template>
      <div class="lc-control-grid">
        <article>
          <Keyboard :size="18" aria-hidden="true" />
          <div><strong>{{ t.keyboard }}</strong><span>{{ t.keyboardMove }}</span><small>{{ keyboardActionText }}</small></div>
        </article>
        <article>
          <MousePointer2 :size="18" aria-hidden="true" />
          <div><strong>{{ t.mouse }}</strong><span>{{ t.mouseAim }}</span><small>{{ mouseActionText }}</small></div>
        </article>
        <article
          :class="{ 'is-connected': snapshot?.gamepad.connected }"
          :data-gamepad-status="snapshot?.gamepad.status"
          :data-gamepad-id="snapshot?.gamepad.id"
          :data-gamepad-profile="snapshot?.gamepad.profile"
          aria-live="polite"
        >
          <Gamepad2 :size="18" aria-hidden="true" />
          <div>
            <strong>{{ t.gamepad }}</strong>
            <span :title="gamepadStatusText">{{ gamepadStatusText }}</span>
            <small v-for="line in gamepadBindingLines" :key="line">{{ line }}</small>
            <template v-if="controlScheme !== 'legacy'">
              <small v-for="role in snapshot?.controlRoles ?? []" :key="role.hand" class="lc-control-role">
                {{ role.hand === 'left' ? 'R' : 'L' }} · {{ role.instantMove }} · {{ role.techniqueOrTrigger }}<template v-if="role.nextGate"> → {{ role.nextGate }}</template>
              </small>
            </template>
          </div>
        </article>
        <article>
          <Smartphone :size="18" aria-hidden="true" />
          <div><strong>{{ t.touch }} · DeepList</strong><span>{{ t.touchMove }}</span><small>{{ t.touchLegacy }}</small></div>
        </article>
      </div>
      <div class="lc-gesture-guide" data-testid="control-guide">
        <strong>{{ controlScheme === 'legacy' ? t.gestureGuide : controlSchemeLabel }}</strong>
        <span v-for="item in controlGuideItems" :key="item">
          {{ item }}
        </span>
      </div>
    </section>

    <RunMapOverlay
      :open="routeMapOpen"
      :locale="locale"
      :nodes="runMapNodes"
      :edges="runMapEdges"
      :seed="plan?.seed ?? config?.seed ?? '—'"
      :allow-close="snapshot?.phase === 'playing' || (snapshot?.phase === 'planning' && !!snapshot.currentNodeId)"
      :selected-node-id="snapshot?.selectedNodeId ?? null"
      @choose="chooseNode"
      @close="closeMap"
    />

    <BuilderDrawer
      :open="builderOpen"
      :locale="locale"
      :config="config"
      @close="closeBuilder"
      @apply="applyBuilder"
      @apply-controls="applyBuilderControls"
      @save="saveBuilderOverride"
      @clear="clearBuilderOverride"
    />

    <Transition name="lc-toast">
      <p v-if="toast" class="lc-page-toast" role="status">{{ toast }}</p>
    </Transition>

    <p class="sr-only" aria-live="polite">
      {{ snapshot ? `${t.physical} ${Math.ceil(snapshot.player.hp)}. ${t.mental} ${Math.ceil(snapshot.player.mentalHealth)}. ${t.chances} ${snapshot.chances}.` : '' }}
    </p>
  </div>
</template>

<style scoped>
.lc-page {
  --lc-ink: #e9e6dc;
  --lc-muted: #747a77;
  --lc-line: rgba(224, 219, 202, 0.1);
  --lc-gold: #c9a75e;
  --lc-blood: #a73942;
  position: relative;
  min-width: 0;
  display: grid;
  gap: 0.85rem;
  color: var(--lc-ink);
  isolation: isolate;
}

.lc-page:fullscreen {
  width: 100vw;
  height: 100dvh;
  padding: 0.65rem;
  overflow: auto;
  overscroll-behavior: none;
  background: #07090a;
  touch-action: manipulation;
  user-select: none;
}
.lc-page:fullscreen .lc-cockpit { min-height: 0; }
.lc-page:fullscreen .lc-stage-screen { min-height: clamp(32rem, 68dvh, 58rem); }

.lc-page::before {
  content: '';
  position: fixed;
  z-index: -1;
  inset: 0;
  pointer-events: none;
  background:
    radial-gradient(circle at 18% 10%, rgba(119, 35, 42, 0.11), transparent 28%),
    radial-gradient(circle at 85% 65%, rgba(70, 55, 81, 0.09), transparent 34%);
}

.lc-page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  min-height: 4.6rem;
  padding: 0.65rem 0.8rem;
  border: 1px solid var(--lc-line);
  border-radius: 0.8rem;
  background: linear-gradient(120deg, rgba(255, 255, 255, 0.025), rgba(9, 11, 12, 0.7));
  box-shadow: 0 0.7rem 2rem rgba(0, 0, 0, 0.18);
}

.lc-title-lockup { min-width: 0; display: flex; align-items: center; gap: 0.8rem; }
.lc-title-sigil { position: relative; flex: 0 0 auto; width: 3.1rem; height: 3.1rem; display: grid; place-items: center; border: 1px solid rgba(201, 167, 94, 0.4); border-radius: 50%; color: #dcc481; background: radial-gradient(circle, rgba(135, 95, 32, 0.2), rgba(6, 8, 9, 0.7)); box-shadow: inset 0 0 1.2rem rgba(0, 0, 0, 0.6), 0 0 1rem rgba(184, 139, 57, 0.08); }
.lc-title-sigil span { font: 700 1.05rem/1 Georgia, serif; }
.lc-title-sigil i { position: absolute; inset: -0.25rem; border: 1px dotted rgba(201, 167, 94, 0.2); border-radius: 50%; }
.lc-title-lockup p { margin: 0; color: #9e444a; font-size: 0.55rem; font-weight: 800; letter-spacing: 0.15em; text-transform: uppercase; }
.lc-title-lockup h1 { margin: 0.12rem 0 0; color: #f5f0e6; font: 600 clamp(1.2rem, 2.4vw, 1.75rem)/1 Georgia, 'Times New Roman', serif; letter-spacing: -0.025em; }
.lc-title-lockup > div > span { display: block; max-width: 43rem; margin-top: 0.25rem; overflow: hidden; color: #777c79; font-size: 0.6rem; line-height: 1.35; text-overflow: ellipsis; white-space: nowrap; }

.lc-header-actions { display: flex; align-items: center; justify-content: flex-end; gap: 0.42rem; }
.lc-header-actions button { min-height: 2.1rem; display: inline-flex; align-items: center; justify-content: center; gap: 0.35rem; padding: 0.4rem 0.6rem; border: 1px solid rgba(255, 255, 255, 0.08); border-radius: 0.42rem; color: #a8aba6; background: rgba(255, 255, 255, 0.025); font-size: 0.57rem; font-weight: 750; white-space: nowrap; }
.lc-header-actions button:hover:not(:disabled) { color: #f0ece2; border-color: rgba(255, 255, 255, 0.2); }
.lc-header-actions button.is-builder { color: #d2b66f; border-color: rgba(198, 160, 79, 0.24); background: rgba(163, 121, 42, 0.08); }
.lc-header-actions button.is-route-ready,
.lc-run-card > header.is-route-ready { animation: lc-route-ready 1.35s ease-in-out infinite; }
.lc-header-actions button.is-route-ready { color: #f4d77e; border-color: rgba(236, 195, 91, 0.65); background: rgba(193, 139, 31, 0.16); }
.lc-header-actions button:disabled { opacity: 0.32; cursor: not-allowed; }
.lc-live-state { display: inline-flex; align-items: center; gap: 0.35rem; margin-right: 0.2rem; color: #686d69; font-size: 0.53rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.lc-live-state i { width: 0.42rem; height: 0.42rem; border-radius: 50%; background: #676b68; }
.lc-live-state.active { color: #899575; }
.lc-live-state.active i { background: #8fa06c; box-shadow: 0 0 0.55rem #8fa06c; }

.lc-cockpit { min-width: 0; display: grid; grid-template-columns: minmax(0, 1fr) 19rem; gap: 0.8rem; }
.lc-stage-panel { min-width: 0; overflow: hidden; border: 1px solid var(--lc-line); border-radius: 0.85rem; background: #080a0b; box-shadow: 0 1.5rem 4rem rgba(0, 0, 0, 0.32); }
.lc-stage-screen { position: relative; min-height: clamp(31rem, 61vh, 46rem); overflow: hidden; background: #08080b; }
.lc-route-ready { position: absolute; z-index: 18; left: 50%; bottom: 5.3rem; min-width: 13rem; display: grid; grid-template-columns: auto 1fr; align-items: center; gap: 0.12rem 0.55rem; padding: 0.65rem 0.85rem; transform: translateX(-50%); border: 1px solid rgba(239, 199, 99, 0.72); border-radius: 999px; color: #f3d77e; background: rgba(25, 21, 12, 0.9); box-shadow: 0 0 1.8rem rgba(221, 172, 51, 0.38); animation: lc-route-ready 1.35s ease-in-out infinite; }
.lc-route-ready svg { grid-row: 1 / span 2; }
.lc-route-ready strong { font-size: 0.62rem; text-transform: uppercase; }
.lc-route-ready span { color: #a99562; font-size: 0.5rem; }
.lc-stage-screen::before,
.lc-stage-screen::after { content: ''; position: absolute; z-index: 8; inset: 0; pointer-events: none; }
.lc-stage-screen::before { background: radial-gradient(ellipse at center, transparent 48%, rgba(0, 0, 0, 0.53) 100%); }
.lc-stage-screen::after { opacity: 0.2; background: repeating-linear-gradient(0deg, transparent 0 3px, rgba(0, 0, 0, 0.13) 3px 4px); mix-blend-mode: multiply; }
.lc-canvas { position: absolute; inset: 0; width: 100%; height: 100%; display: block; touch-action: none; outline: none; }
.lc-canvas:focus-visible { box-shadow: inset 0 0 0 2px rgba(214, 180, 102, 0.72); }

.lc-stage-hud { position: absolute; z-index: 12; inset: 0 0 auto; display: grid; grid-template-columns: minmax(9rem, 1fr) auto minmax(9rem, 1fr); align-items: start; gap: 1rem; padding: 0.75rem 1rem 1.4rem; pointer-events: none; background: linear-gradient(#080a0b 0, rgba(8, 10, 11, 0.88) 58%, transparent 100%); }
.lc-vitals { min-width: 0; display: grid; gap: 0.28rem; }
.lc-vital-label { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 0.35rem; }
.lc-vital-label span { color: #8c918d; font-size: 0.53rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
.lc-vital-label strong { color: #dcd9d0; font: 700 0.58rem/1 var(--font-mono, monospace); }
.lc-vital-track { height: 0.36rem; overflow: hidden; border: 1px solid rgba(255, 255, 255, 0.08); border-radius: 999px; background: rgba(255, 255, 255, 0.04); }
.lc-vital-track i { display: block; height: 100%; border-radius: inherit; transition: width 0.22s ease; }
.is-physical { color: #bd6267; }
.is-physical .lc-vital-track i { background: linear-gradient(90deg, #64242b, #c7656a); box-shadow: 0 0 0.65rem rgba(185, 72, 80, 0.48); }
.is-mental { color: #9b86ae; }
.is-mental .lc-vital-track i { margin-left: auto; background: linear-gradient(90deg, #7d6791, #b3a0c5); box-shadow: 0 0 0.65rem rgba(142, 112, 169, 0.42); }
.lc-room-readout { min-width: 9rem; display: grid; justify-items: center; text-align: center; }
.lc-room-readout small { color: #69575a; font-size: 0.48rem; font-weight: 800; letter-spacing: 0.12em; text-transform: uppercase; }
.lc-room-readout strong { max-width: 16rem; overflow: hidden; color: #e6dfd1; font: 600 0.78rem/1.3 Georgia, serif; text-overflow: ellipsis; white-space: nowrap; }
.lc-room-readout span { color: #686d69; font-size: 0.5rem; font-weight: 700; text-transform: uppercase; }

.lc-gesture-toast { position: absolute; z-index: 15; left: 50%; top: 5.1rem; display: grid; grid-template-columns: auto auto; align-items: center; gap: 0.18rem 0.4rem; padding: 0.42rem 0.7rem; transform: translateX(-50%); border: 1px solid rgba(207, 171, 91, 0.28); border-radius: 0.45rem; color: #d6b96f; background: rgba(8, 10, 11, 0.83); box-shadow: 0 0.7rem 1.5rem rgba(0, 0, 0, 0.35); backdrop-filter: blur(6px); }
.lc-gesture-toast span { font-size: 0.48rem; font-weight: 800; letter-spacing: 0.09em; text-transform: uppercase; }
.lc-gesture-toast strong { grid-column: 1 / -1; color: #e3ded1; font-size: 0.62rem; font-weight: 700; text-align: center; }

.lc-semantic-control-cue { position: absolute; z-index: 15; left: 50%; top: 7.7rem; min-width: min(18rem, calc(100% - 2rem)); display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: 0.12rem 0.55rem; padding: 0.4rem 0.65rem; transform: translateX(-50%); border: 1px solid rgba(152, 123, 183, 0.3); border-radius: 0.45rem; color: #aa96bd; background: rgba(10, 8, 13, 0.84); box-shadow: 0 0.7rem 1.5rem rgba(0, 0, 0, 0.28); backdrop-filter: blur(6px); pointer-events: none; }
.lc-semantic-control-cue span { color: #92859e; font-size: 0.45rem; font-weight: 850; letter-spacing: 0.09em; text-transform: uppercase; }
.lc-semantic-control-cue strong { grid-column: 1 / -1; overflow: hidden; color: #e0d9e6; font-size: 0.58rem; text-overflow: ellipsis; white-space: nowrap; }
.lc-semantic-control-cue small { grid-column: 2; grid-row: 1; color: #776681; font: 700 0.43rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.lc-semantic-control-cue.is-charge,
.lc-semantic-control-cue.is-tension { border-color: rgba(205, 166, 78, 0.35); }
.lc-semantic-control-cue.is-blocked { border-color: rgba(199, 85, 92, 0.42); color: #d27f84; }
.lc-semantic-control-cue.is-impact { border-color: rgba(116, 207, 155, 0.42); color: #91d8ae; }

.lc-arena-interaction {
  position: absolute;
  z-index: 16;
  left: 50%;
  bottom: 1.15rem;
  max-width: min(34rem, calc(100% - 2rem));
  display: grid;
  grid-template-columns: auto auto;
  align-items: center;
  justify-content: center;
  gap: 0.12rem 0.42rem;
  padding: 0.5rem 0.8rem;
  transform: translateX(-50%);
  border: 1px solid rgba(110, 231, 168, 0.48);
  border-radius: 0.55rem;
  color: #9ff4c2;
  background: rgba(7, 18, 14, 0.9);
  box-shadow: 0 0 1.4rem rgba(65, 216, 140, 0.18);
  backdrop-filter: blur(7px);
}
.lc-arena-interaction span { font-size: 0.52rem; font-weight: 850; letter-spacing: 0.09em; text-transform: uppercase; }
.lc-arena-interaction strong { grid-column: 1 / -1; overflow: hidden; color: #dcefe5; font-size: 0.58rem; text-overflow: ellipsis; white-space: nowrap; }
.lc-arena-interaction:disabled { opacity: 0.45; }

.lc-phase-overlay,
.lc-interaction-overlay,
.lc-story-overlay,
.lc-loading-overlay { position: absolute; z-index: 30; inset: 0; display: grid; place-items: center; padding: 1rem; background: rgba(4, 5, 6, 0.78); backdrop-filter: blur(5px); }
.lc-interaction-overlay { z-index: 31; background: rgba(4, 5, 6, 0.84); }
.lc-story-overlay { z-index: 34; background: radial-gradient(circle at 50% 28%, rgba(88, 58, 71, 0.2), transparent 45%), rgba(3, 4, 5, 0.93); }
.lc-interaction-card { width: min(42rem, 100%); padding: clamp(1rem, 3vw, 1.8rem); border: 1px solid rgba(201, 167, 94, 0.18); border-radius: 0.9rem; background: #0d1011; box-shadow: 0 1.5rem 4rem rgba(0, 0, 0, 0.58); }
.lc-interaction-card > p { margin: 0; color: #b08d4f; font-size: 0.55rem; font-weight: 800; letter-spacing: 0.15em; text-transform: uppercase; }
.lc-interaction-card h2 { margin: 0.25rem 0; color: #f1ece1; font: 600 clamp(1.2rem, 3vw, 1.8rem)/1.1 Georgia, serif; }
.lc-interaction-card > span { display: block; color: #858986; font-size: 0.68rem; line-height: 1.5; }
.lc-interaction-choices { display: grid; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); gap: 0.55rem; margin-top: 1rem; }
.lc-interaction-choices button { min-height: 7rem; display: grid; align-content: start; gap: 0.3rem; padding: 0.75rem; border: 1px solid rgba(201, 167, 94, 0.16); border-radius: 0.55rem; text-align: left; color: #ded8ca; background: rgba(201, 167, 94, 0.045); }
.lc-interaction-choices button:hover:not(:disabled) { border-color: rgba(214, 181, 105, 0.42); background: rgba(201, 167, 94, 0.09); }
.lc-interaction-choices button.is-controller-selected:not(:disabled) { border-color: #e2bd67; outline: 2px solid rgba(226, 189, 103, 0.52); outline-offset: 2px; background: rgba(201, 167, 94, 0.14); }
.lc-interaction-choices button:disabled { opacity: 0.38; cursor: not-allowed; }
.lc-interaction-choices strong { font: 600 0.82rem/1.25 Georgia, serif; }
.lc-interaction-choices span { color: #777d79; font-size: 0.58rem; line-height: 1.42; }
.lc-interaction-choices small { margin-top: auto; color: #b69855; font-size: 0.52rem; font-weight: 800; text-transform: uppercase; }
.lc-story-card { width: min(38rem, 100%); padding: clamp(1.2rem, 4vw, 2.2rem); border: 1px solid rgba(226, 218, 198, 0.12); border-radius: 0.85rem; background: linear-gradient(145deg, rgba(255, 255, 255, 0.025), rgba(7, 8, 9, 0.97)); box-shadow: 0 2rem 6rem rgba(0, 0, 0, 0.72); }
.lc-story-card > small { color: #a3474e; font-size: 0.55rem; font-weight: 800; letter-spacing: 0.15em; text-transform: uppercase; }
.lc-story-card > p { min-height: 6rem; margin: 0.75rem 0 1.2rem; white-space: pre-line; color: #d7d2c7; font: 500 clamp(0.86rem, 2vw, 1.05rem)/1.7 Georgia, serif; }
.lc-story-card footer { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
.lc-story-card footer span { color: #666c68; font: 700 0.55rem/1 var(--font-mono, monospace); }
.lc-story-card button { min-height: 2.35rem; display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.45rem 0.75rem; border: 1px solid #c7a55d; border-radius: 0.42rem; color: #17130d; background: linear-gradient(135deg, #d5b66d, #9e732d); font-size: 0.6rem; font-weight: 800; }
.lc-phase-card { width: min(30rem, 100%); display: grid; justify-items: center; padding: clamp(1.2rem, 4vw, 2.2rem); border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 0.9rem; text-align: center; background: radial-gradient(circle at 50% 0, rgba(137, 43, 50, 0.17), transparent 45%), #0d1011; box-shadow: 0 1.5rem 4rem rgba(0, 0, 0, 0.55); }
.lc-phase-card > svg { color: #a7474e; }
.lc-phase-overlay.is-victory .lc-phase-card > svg { color: #c8aa62; }
.lc-phase-card > p:first-of-type { margin: 0.55rem 0 0; color: #a94a50; font-size: 0.56rem; font-weight: 800; letter-spacing: 0.16em; text-transform: uppercase; }
.lc-phase-overlay.is-victory .lc-phase-card > p:first-of-type { color: #b79a57; }
.lc-phase-card h2 { margin: 0.25rem 0; color: #f1ece1; font: 600 clamp(1.3rem, 3vw, 2rem)/1.05 Georgia, serif; }
.lc-death-reason { color: #ab7779; font-size: 0.68rem; }
.lc-overlay-chances { display: inline-flex; align-items: baseline; gap: 0.35rem; margin: 0.8rem 0 0.25rem; }
.lc-overlay-chances strong { color: #d6bd79; font: 600 2rem/1 Georgia, serif; }
.lc-overlay-chances span { color: #777b78; font-size: 0.55rem; font-weight: 800; text-transform: uppercase; }
.lc-phase-body { max-width: 23rem; margin: 0.35rem 0 1rem; color: #7b807d; font-size: 0.66rem; line-height: 1.5; }
.lc-phase-actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 0.45rem; }
.lc-phase-actions button,
.lc-loading-overlay button { min-height: 2.3rem; display: inline-flex; align-items: center; gap: 0.35rem; padding: 0.45rem 0.75rem; border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 0.42rem; color: #adafaa; background: rgba(255, 255, 255, 0.03); font-size: 0.6rem; font-weight: 750; }
.lc-phase-actions button.is-primary { color: #17130d; border-color: #c7a55d; background: linear-gradient(135deg, #d5b66d, #9e732d); }

.lc-loading-overlay { z-index: 35; align-content: center; text-align: center; }
.lc-loading-mark { width: 3.4rem; height: 3.4rem; display: grid; place-items: center; border: 1px solid rgba(203, 169, 93, 0.3); border-radius: 50%; color: #c4a35c; animation: lc-loading-turn 2.3s linear infinite; }
.lc-loading-mark.is-error { color: #be5c63; border-color: rgba(190, 70, 79, 0.35); animation: none; }
.lc-loading-overlay h2 { margin: 0.8rem 0 0.2rem; color: #eae5db; font: 600 1.3rem/1.2 Georgia, serif; }
.lc-loading-overlay p { max-width: 35rem; margin: 0 0 0.8rem; color: #777c79; font-size: 0.65rem; overflow-wrap: anywhere; }
.lc-loading-actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 0.45rem; }

.lc-stage-footer { min-height: 2.1rem; display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.35rem 0.7rem; border-top: 1px solid var(--lc-line); color: #646966; background: #0b0e0f; font-size: 0.52rem; font-weight: 700; }
.lc-stage-footer span { display: inline-flex; align-items: center; gap: 0.32rem; }
.lc-stage-footer i { width: 0.42rem; height: 0.42rem; border-radius: 50%; background: #6b7460; }
.lc-stage-footer i.is-alert { background: #bd4e55; box-shadow: 0 0 0.5rem #bd4e55; }

.lc-telemetry { min-width: 0; align-self: start; display: grid; gap: 0.7rem; }
.lc-chance-card,
.lc-event-card,
.lc-run-card,
.lc-erosion-card,
.lc-quest-card { border: 1px solid var(--lc-line); border-radius: 0.7rem; background: linear-gradient(145deg, rgba(255, 255, 255, 0.02), rgba(8, 10, 11, 0.55)); box-shadow: 0 0.7rem 1.5rem rgba(0, 0, 0, 0.17); }
.lc-chance-card { display: grid; grid-template-columns: auto 1fr; align-items: center; gap: 0.8rem; padding: 0.75rem; }
.lc-chance-orb { --chance-progress: 360deg; position: relative; width: 5.1rem; height: 5.1rem; display: grid; place-items: center; align-content: center; border-radius: 50%; background: conic-gradient(#c29f50 var(--chance-progress), rgba(255, 255, 255, 0.055) 0); box-shadow: 0 0 1.8rem rgba(192, 146, 52, 0.08); }
.lc-chance-orb::before { content: ''; position: absolute; inset: 3px; border-radius: inherit; background: radial-gradient(circle at 50% 35%, #1b1a17, #0a0c0d 70%); }
.lc-chance-orb span,
.lc-chance-orb small { position: relative; z-index: 1; }
.lc-chance-orb span { color: #e0ca8e; font: 600 1.65rem/1 Georgia, serif; }
.lc-chance-orb small { margin-top: 0.12rem; color: #6c675a; font: 800 0.48rem/1 var(--font-mono, monospace); letter-spacing: 0.14em; }
.lc-chance-card > div:last-child { display: grid; }
.lc-chance-card p { margin: 0 0 0.3rem; color: #d5d1c8; font-size: 0.66rem; font-weight: 800; text-transform: uppercase; }
.lc-chance-card strong { color: #717673; font-size: 0.53rem; font-weight: 700; }
.lc-chance-card > div:last-child > span { color: #b95a60; font: 700 0.72rem/1.4 var(--font-mono, monospace); }

.lc-event-card { overflow: hidden; padding: 0.65rem; border-color: rgba(126, 166, 79, 0.22); background: radial-gradient(circle at 90% 0, rgba(120, 161, 70, 0.11), transparent 45%), linear-gradient(145deg, rgba(255, 255, 255, 0.02), rgba(8, 10, 11, 0.55)); }
.lc-event-card > header { display: flex; align-items: center; gap: 0.38rem; color: #9cca68; }
.lc-event-card > header span { color: #d7d8bd; font-size: 0.62rem; font-weight: 850; letter-spacing: 0.07em; text-transform: uppercase; }
.lc-event-card > p { margin: 0.45rem 0 0; color: #67705e; font-size: 0.5rem; }
.lc-event-card ol { display: grid; gap: 0.22rem; margin: 0.5rem 0 0; padding: 0; list-style: none; }
.lc-event-card li { padding: 0.3rem 0.42rem; border: 1px solid rgba(144, 177, 89, 0.09); border-radius: 0.38rem; color: #c6c8b8; background: rgba(86, 112, 50, 0.045); font-size: 0.52rem; font-weight: 650; }
/* Newest line reads brightest; older lines recede down the list. */
.lc-event-card li:first-child { color: #e2e6cf; border-color: rgba(159, 197, 98, 0.3); }
.lc-event-card li:nth-child(n + 3) { color: #8b9184; }
.lc-event-card li:nth-child(n + 5) { color: #6b7168; }

.lc-run-card { overflow: hidden; }
.lc-run-card > header { display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 0.4rem; padding: 0.55rem 0.65rem; border-bottom: 1px solid rgba(255, 255, 255, 0.055); color: #b39758; }
.lc-run-card > header span { color: #d1cec5; font-size: 0.63rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
.lc-run-card > header button { display: inline-flex; align-items: center; border: 0; color: #87764d; background: transparent; font-size: 0.49rem; font-weight: 700; }
.lc-run-card dl { display: grid; margin: 0; padding: 0.35rem 0.6rem 0.55rem; }
.lc-run-card dl > div { min-width: 0; display: flex; align-items: baseline; justify-content: space-between; gap: 0.7rem; padding: 0.3rem 0; border-bottom: 1px solid rgba(255, 255, 255, 0.035); }
.lc-run-card dl > div:last-child { border-bottom: 0; }
.lc-run-card dt { color: #666b68; font-size: 0.5rem; font-weight: 800; text-transform: uppercase; }
.lc-run-card dd { max-width: 11rem; margin: 0; overflow: hidden; color: #bfc0ba; font-size: 0.58rem; font-weight: 650; text-align: right; text-overflow: ellipsis; white-space: nowrap; }
.lc-run-card .is-seed dd { color: #80765e; font: 600 0.5rem/1 var(--font-mono, monospace); }

.lc-quest-card { display: grid; gap: 0.45rem; padding: 0.65rem; }
.lc-quest-card > header { display: inline-flex; align-items: center; gap: 0.35rem; color: #8fb3c9; font-size: 0.61rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.lc-quest-hand { display: grid; gap: 0.25rem; }
.lc-quest-hand > strong { color: #a9aba4; font-size: 0.52rem; font-weight: 800; letter-spacing: 0.08em; text-transform: uppercase; }
.lc-quest-hand ul { display: grid; gap: 0.18rem; margin: 0; padding: 0; list-style: none; }
.lc-quest-hand li { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: baseline; gap: 0.4rem; color: #828783; font-size: 0.55rem; }
.lc-quest-hand li b { color: #c7a55d; font: 700 0.53rem/1 var(--font-mono, monospace); }
.lc-quest-hand li.is-done { color: #5d675f; text-decoration: line-through; }
.lc-quest-hand li.is-done b { color: #7fae86; text-decoration: none; }
.lc-quest-hand > small { color: #7b9e6f; font-size: 0.5rem; }

.lc-erosion-card { padding: 0.65rem; }
.lc-erosion-card header { display: grid; gap: 0.08rem; }
.lc-erosion-card header span { display: inline-flex; align-items: center; gap: 0.35rem; color: #ca777b; font-size: 0.61rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.lc-erosion-card header small { color: #5f6461; font-size: 0.5rem; }
.lc-erosion-card > p { margin: 0.6rem 0 0; color: #687062; font-size: 0.58rem; }
.lc-erosion-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.3rem; margin-top: 0.55rem; }
.lc-erosion-grid div { min-width: 0; display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 0.3rem; padding: 0.32rem; border: 1px solid rgba(183, 79, 87, 0.1); border-radius: 0.35rem; color: #936267; background: rgba(134, 45, 53, 0.045); }
.lc-erosion-grid span { overflow: hidden; color: #737774; font-size: 0.48rem; text-overflow: ellipsis; white-space: nowrap; }
.lc-erosion-grid strong { color: #c36d72; font: 700 0.54rem/1 var(--font-mono, monospace); }

.lc-controls-dock { position: relative; z-index: 4200; overflow: hidden; border: 1px solid var(--lc-line); border-radius: 0.75rem; background: rgba(9, 11, 12, 0.94); }
.lc-controls-dock > header { display: grid; grid-template-columns: minmax(0, 1fr) minmax(18rem, 32rem); align-items: center; gap: 1rem; padding: 0.55rem 0.7rem; border-bottom: 1px solid rgba(255, 255, 255, 0.05); }
.lc-control-heading { min-width: 0; display: flex; align-items: center; gap: 0.45rem; color: #c9c5bc; }
.lc-control-heading > span { min-width: 0; display: grid; gap: 0.08rem; }
.lc-control-heading strong { font-size: 0.6rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.lc-control-heading small { overflow: hidden; color: #606562; font-size: 0.5rem; text-overflow: ellipsis; white-space: nowrap; }
.lc-control-scheme-field { min-width: 0; display: grid; grid-template-columns: auto minmax(7rem, 9rem) minmax(0, 1fr); align-items: center; gap: 0.45rem; }
.lc-control-scheme-field label { color: #9c927b; font-size: 0.52rem; font-weight: 800; letter-spacing: 0.06em; text-transform: uppercase; }
.lc-control-scheme-field select,
.lc-feedback-controls select { min-height: 1.9rem; padding: 0.28rem 0.45rem; border: 1px solid rgba(205, 171, 92, 0.22); border-radius: 0.35rem; outline: none; color: #ded8c9; background: #0b0e0f; font: 700 0.58rem/1 var(--font-mono, monospace); color-scheme: dark; }
.lc-control-scheme-field select:focus-visible,
.lc-feedback-controls select:focus-visible { border-color: rgba(218, 186, 111, 0.75); box-shadow: 0 0 0 2px rgba(205, 171, 92, 0.13); }
.lc-control-scheme-field small { overflow: hidden; color: #6d716e; font-size: 0.49rem; line-height: 1.3; text-overflow: ellipsis; white-space: nowrap; }
.lc-feedback-controls { display: grid; grid-template-columns: minmax(11rem, 1fr) auto minmax(10rem, 14rem) auto; align-items: center; gap: 0.65rem; padding: 0.48rem 0.7rem; border-bottom: 1px solid rgba(255, 255, 255, 0.045); background: rgba(65, 48, 85, 0.08); }
.lc-feedback-controls > div { min-width: 0; display: grid; grid-template-columns: auto minmax(0, 1fr); align-items: center; gap: 0.15rem 0.45rem; }
.lc-feedback-controls strong { color: #bbb4c9; font-size: 0.54rem; text-transform: uppercase; }
.lc-feedback-controls > div span { color: #8f92a0; font-size: 0.5rem; }
.lc-feedback-controls > div span.is-enhanced { color: #a9d7bc; }
.lc-feedback-controls > div span.is-error { color: #d28287; }
.lc-feedback-controls > div small { grid-column: 1 / -1; overflow: hidden; color: #676b72; font-size: 0.46rem; text-overflow: ellipsis; white-space: nowrap; }
.lc-feedback-controls label { display: grid; gap: 0.18rem; color: #7b7d84; font-size: 0.47rem; font-weight: 750; }
.lc-feedback-intensity input { width: 100%; accent-color: #9b7cc1; }
.lc-feedback-controls > button { min-height: 2rem; padding: 0.35rem 0.55rem; border: 1px solid rgba(157, 125, 195, 0.24); border-radius: 0.38rem; color: #b5a3ca; background: rgba(113, 78, 147, 0.08); font-size: 0.5rem; font-weight: 750; }
.lc-tactile-legend { display: flex; align-items: center; flex-wrap: wrap; gap: 0.28rem; padding: 0.38rem 0.7rem; border-bottom: 1px solid rgba(255, 255, 255, 0.045); background: rgba(65, 48, 85, 0.045); }
.lc-tactile-legend strong { margin-right: 0.2rem; color: #746b7f; font-size: 0.46rem; font-weight: 850; letter-spacing: 0.08em; text-transform: uppercase; }
.lc-tactile-legend span { padding: 0.16rem 0.3rem; border: 1px solid rgba(157, 125, 195, 0.12); border-radius: 999px; color: #77727d; font-size: 0.43rem; }
.lc-control-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); }
.lc-control-grid article { min-width: 0; display: grid; grid-template-columns: auto 1fr; align-items: center; gap: 0.5rem; padding: 0.55rem 0.7rem; border-right: 1px solid rgba(255, 255, 255, 0.045); color: #8c7650; }
.lc-control-grid article:last-child { border-right: 0; }
.lc-control-grid article div { min-width: 0; display: grid; }
.lc-control-grid strong { color: #b8b7b1; font-size: 0.56rem; font-weight: 800; text-transform: uppercase; }
.lc-control-grid span,
.lc-control-grid small { overflow: hidden; color: #666b68; font-size: 0.49rem; line-height: 1.3; text-overflow: ellipsis; white-space: nowrap; }
.lc-control-grid small { color: #80765e; }
.lc-control-grid small.lc-control-role { color: #9a88ac; }
.lc-gesture-guide { display: flex; align-items: center; justify-content: center; flex-wrap: wrap; gap: 0.35rem; padding: 0.42rem 0.6rem; border-top: 1px solid rgba(255, 255, 255, 0.045); }
.lc-gesture-guide strong { margin-right: 0.25rem; color: #6d716e; font-size: 0.5rem; font-weight: 800; text-transform: uppercase; }
.lc-gesture-guide span { padding: 0.2rem 0.38rem; border: 1px solid rgba(255, 255, 255, 0.065); border-radius: 999px; color: #989a94; background: rgba(255, 255, 255, 0.02); font-size: 0.47rem; font-weight: 650; }

.lc-page-toast { position: fixed; z-index: 5000; left: 50%; bottom: max(1rem, env(safe-area-inset-bottom)); max-width: min(34rem, calc(100vw - 2rem)); margin: 0; padding: 0.65rem 0.9rem; transform: translateX(-50%); border: 1px solid rgba(191, 160, 89, 0.3); border-radius: 0.5rem; color: #d7c594; background: rgba(10, 12, 13, 0.94); box-shadow: 0 1rem 2rem rgba(0, 0, 0, 0.45); font-size: 0.64rem; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

.lc-page.is-mental-low .lc-stage-screen { box-shadow: inset 0 0 5rem rgba(99, 61, 116, 0.12); }
.lc-page.is-critical .lc-stage-screen { box-shadow: inset 0 0 6rem rgba(139, 31, 40, 0.16); }

@keyframes lc-loading-turn { to { transform: rotate(360deg); } }
@keyframes lc-route-ready {
  50% { box-shadow: 0 0 2.4rem rgba(235, 184, 57, 0.56); filter: brightness(1.18); }
}
.lc-gesture-pop-enter-active,
.lc-gesture-pop-leave-active,
.lc-phase-fade-enter-active,
.lc-phase-fade-leave-active,
.lc-toast-enter-active,
.lc-toast-leave-active { transition: opacity 0.18s ease, transform 0.18s ease; }
.lc-gesture-pop-enter-from,
.lc-gesture-pop-leave-to { opacity: 0; transform: translate(-50%, 0.5rem); }
.lc-phase-fade-enter-from,
.lc-phase-fade-leave-to,
.lc-toast-enter-from,
.lc-toast-leave-to { opacity: 0; }

@media (max-width: 1160px) {
  .lc-cockpit { grid-template-columns: 1fr; }
  .lc-telemetry { grid-template-columns: 14rem 1fr 1fr; align-items: start; }
  .lc-telemetry :deep(.lc-cooldowns) { grid-column: 1 / -1; }
  .lc-stage-screen { min-height: clamp(32rem, 67vh, 45rem); }
}

@media (max-width: 860px) {
  .lc-page-header { align-items: flex-start; }
  .lc-title-lockup > div > span { display: none; }
  .lc-live-state { display: none; }
  .lc-header-actions { flex-wrap: wrap; }
  .lc-header-actions button { flex: 1 1 auto; }
  .lc-telemetry { grid-template-columns: 1fr 1fr; }
  .lc-chance-card { grid-row: span 2; }
  .lc-telemetry :deep(.lc-cooldowns) { grid-column: 1 / -1; }
  .lc-control-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .lc-control-grid article:nth-child(2) { border-right: 0; }
  .lc-control-grid article:nth-child(-n+2) { border-bottom: 1px solid rgba(255, 255, 255, 0.045); }
  .lc-controls-dock > header { grid-template-columns: 1fr; gap: 0.5rem; }
  .lc-feedback-controls { grid-template-columns: minmax(0, 1fr) minmax(8rem, auto); }
}

@media (max-width: 620px) {
  .lc-page { gap: 0.45rem; }
  .lc-page-header { display: grid; padding: 0.5rem; }
  .lc-title-sigil { width: 2.5rem; height: 2.5rem; }
  .lc-title-lockup p { font-size: 0.44rem; }
  .lc-title-lockup h1 { font-size: 1.1rem; }
  .lc-header-actions { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); width: 100%; }
  .lc-header-actions button { min-width: 0; padding: 0.35rem 0.2rem; font-size: 0.48rem; }
  .lc-header-actions button svg { display: none; }
  .lc-stage-screen { min-height: 74dvh; }
  .lc-stage-hud { grid-template-columns: 1fr 1fr; gap: 0.35rem 0.7rem; padding: 0.45rem 0.55rem 1.2rem; }
  .lc-room-readout { grid-column: 1 / -1; grid-row: 1; }
  .lc-vital-label span { font-size: 0.43rem; }
  .lc-vital-label strong { font-size: 0.48rem; }
  .lc-gesture-toast { top: 5.6rem; }
  .lc-semantic-control-cue { top: 8.15rem; min-width: min(16rem, calc(100% - 1rem)); }
  .lc-arena-interaction { bottom: 8rem; max-width: calc(100% - 8rem); }
  .lc-stage-footer { display: none; }
  .lc-telemetry { grid-template-columns: 1fr 1fr; gap: 0.4rem; }
  .lc-chance-card { grid-column: 1 / -1; grid-row: auto; }
  .lc-run-card,
  .lc-erosion-card { min-height: 100%; }
  .lc-telemetry :deep(.lc-cooldowns) { grid-column: 1 / -1; }
  .lc-control-scheme-field { grid-template-columns: minmax(6.5rem, auto) minmax(0, 1fr); }
  .lc-control-scheme-field small { grid-column: 1 / -1; white-space: normal; }
  .lc-feedback-controls { grid-template-columns: 1fr; }
  .lc-feedback-controls > button { width: 100%; }
  .lc-control-grid { display: flex; overflow-x: auto; scroll-snap-type: x mandatory; }
  .lc-control-grid article { flex: 0 0 78%; border-bottom: 0 !important; scroll-snap-align: start; }
  .lc-gesture-guide { justify-content: flex-start; }
  .lc-gesture-guide strong { flex-basis: 100%; }
  .lc-phase-card { padding: 1rem; }
}

@media (prefers-reduced-motion: reduce) {
  .lc-loading-mark { animation: none; }
  .lc-vital-track i,
  .lc-gesture-pop-enter-active,
  .lc-gesture-pop-leave-active,
  .lc-phase-fade-enter-active,
  .lc-phase-fade-leave-active,
  .lc-toast-enter-active,
  .lc-toast-leave-active { transition: none; }
}
</style>
