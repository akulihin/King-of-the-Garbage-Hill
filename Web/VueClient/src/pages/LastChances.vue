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
  saveLastChancesConfig as saveLastChancesConfigOverride,
  type LastChancesConfig,
  type LastChancesGamePlan,
  type LastChancesHand,
  type LastChancesSnapshot,
} from '../features/last-chances'
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
    stageLabel: '99 Last Chances isometric combat arena',
    controls: 'Control language',
    controlsHelp: 'Every device feeds the same two-hand gesture system.',
    keyboard: 'Keyboard',
    keyboardMove: 'WASD / arrows move',
    keyboardAttack: 'attack with',
    mouse: 'Mouse',
    mouseAim: 'Move pointer to aim',
    mouseAttack: 'Left / right button gestures',
    gamepad: 'Gamepad',
    gamepadMove: 'Left stick moves · right stick aims',
    gamepadAttack: 'Buttons',
    touch: 'Touch',
    touchMove: 'Left stick moves · aim pad aims',
    touchAttack: 'Use either hand button for gestures',
    gestureGuide: 'Five gestures per hand',
    tap: 'Tap',
    doubleTap: 'Double tap',
    doubleTapHold: 'Double tap + hold',
    hold: 'Hold / release',
    holdThenDoubleTap: 'Hold + double tap',
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
    beginAgain: 'Begin a new generation',
    chooseRoute: 'Choose the next room',
    killedBy: 'Killed by',
    mindCollapsed: 'Your mental health collapsed',
    attemptEnded: 'The attempt ended',
    saved: 'Browser override saved.',
    cleared: 'Browser override cleared; server definition restored.',
    applied: 'Builder definition applied to a fresh attempt.',
  },
  ru: {
    eyebrow: 'Первый игровой прототип · детерминированная память',
    title: '99 Last Chances',
    subtitle: 'Каждая смерть отнимает Шансы. Каждая попытка помнит те же комнаты. А ваше тело восстанавливается не так легко.',
    online: 'Симуляция запущена',
    waiting: 'Ожидание маршрута',
    map: 'Карта забега',
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
    stageLabel: 'Изометрическая боевая арена 99 Last Chances',
    controls: 'Язык управления',
    controlsHelp: 'Все устройства используют одну систему жестов для двух рук.',
    keyboard: 'Клавиатура',
    keyboardMove: 'WASD / стрелки — движение',
    keyboardAttack: 'атака клавишами',
    mouse: 'Мышь',
    mouseAim: 'Двигайте указатель для прицела',
    mouseAttack: 'Жесты левой / правой кнопкой',
    gamepad: 'Геймпад',
    gamepadMove: 'Левый стик — движение · правый — прицел',
    gamepadAttack: 'Кнопки',
    touch: 'Сенсорный экран',
    touchMove: 'Левый стик — движение · площадка — прицел',
    touchAttack: 'Используйте кнопки обеих рук для жестов',
    gestureGuide: 'Пять жестов для каждой руки',
    tap: 'Нажатие',
    doubleTap: 'Двойное нажатие',
    doubleTapHold: 'Двойное нажатие + задержка',
    hold: 'Задержка / отпускание',
    holdThenDoubleTap: 'Задержка + двойное нажатие',
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
    beginAgain: 'Начать новую генерацию',
    chooseRoute: 'Выбрать следующую комнату',
    killedBy: 'Убит врагом',
    mindCollapsed: 'Ваше ментальное здоровье иссякло',
    attemptEnded: 'Попытка завершена',
    saved: 'Замена конфигурации сохранена в браузере.',
    cleared: 'Замена очищена; восстановлена конфигурация сервера.',
    applied: 'Конфигурация применена к новой попытке.',
  },
} as const

const locale = computed<LastChancesLocale>(() => currentLocale.value)
const t = computed(() => copy[locale.value])
const canvas = ref<HTMLCanvasElement | null>(null)
const engine = shallowRef<LastChancesEngine | null>(null)
const config = ref<LastChancesConfig | null>(null)
const plan = ref<LastChancesGamePlan | null>(null)
const snapshot = ref<LastChancesSnapshot | null>(null)
const loading = ref(true)
const loadError = ref('')
const routeMapOpen = ref(false)
const builderOpen = ref(false)
const toast = ref('')
const visitedNodeIds = ref(new Set<string>())
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
const activeTierIndex = computed(() => snapshot.value?.currentTierIndex ?? 0)
const activeTier = computed(() => config.value?.progression.tiers[activeTierIndex.value] ?? null)
const nextDeathCost = computed(() => activeTier.value?.deathCost ?? 1)
const leftWeapon = computed(() => config.value?.weapons.find(weapon => weapon.hand === 'left') ?? null)
const rightWeapon = computed(() => config.value?.weapons.find(weapon => weapon.hand === 'right') ?? null)

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

const weaponCooldowns = computed<WeaponCooldown[]>(() => {
  if (!config.value) return []
  return config.value.weapons.map((weapon) => ({
    hand: weapon.hand === 'left' ? 'primary' : 'secondary',
    name: weapon.name,
    gestures: LAST_CHANCES_GESTURES.map((gesture) => {
      const cooldown = snapshot.value?.cooldowns.find(item => item.hand === weapon.hand && item.gesture === gesture)
      const lastGesture = snapshot.value?.lastGesture
      return {
        key: gesture,
        name: weapon.attacks[gesture].name,
        remainingMs: cooldown?.remainingMs ?? 0,
        totalMs: cooldown?.totalMs ?? weapon.attacks[gesture].cooldownMs,
        active: !!lastGesture
          && lastGesture.hand === weapon.hand
          && lastGesture.gesture === gesture
          && (snapshot.value?.elapsedMs ?? 0) - lastGesture.atMs < 450,
      }
    }),
  }))
})

const runMapNodes = computed<RunMapNode[]>(() => {
  if (!plan.value || !snapshot.value) return []
  const current = snapshot.value.currentNodeId
  const available = new Set(snapshot.value.availableNodeIds)
  const attempt = new Set(snapshot.value.attemptPath)
  return plan.value.nodes.map((node) => {
    let state: RunMapNode['state'] = 'locked'
    if (available.has(node.id)) state = 'available'
    else if (node.id === current && snapshot.value?.phase === 'playing') state = 'current'
    else if (attempt.has(node.id)) state = 'cleared'
    else if (visitedNodeIds.value.has(node.id)) state = 'visited'
    const tier = config.value?.progression.tiers[node.tierIndex]
    return {
      id: node.id,
      name: node.label,
      tier: node.tierIndex + 1,
      kind: tier?.kind === 'boss' ? 'boss' : node.roomArchetype,
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
  if (!state?.lastGesture || state.phase !== 'playing') return null
  return state.elapsedMs - state.lastGesture.atMs < 850 ? state.lastGesture : null
})

function setToast(message: string) {
  toast.value = message
  if (toastTimer !== null) window.clearTimeout(toastTimer)
  toastTimer = window.setTimeout(() => { toast.value = '' }, 3200)
}

function onSnapshot(nextSnapshot: LastChancesSnapshot) {
  const previousGeneration = snapshot.value?.generation
  if (previousGeneration !== undefined && previousGeneration !== nextSnapshot.generation) {
    visitedNodeIds.value = new Set()
  }
  const nextVisited = new Set(visitedNodeIds.value)
  nextSnapshot.attemptPath.forEach(id => nextVisited.add(id))
  visitedNodeIds.value = nextVisited
  snapshot.value = nextSnapshot
  if (nextSnapshot.phase === 'planning' && nextSnapshot.availableNodeIds.length > 0) routeMapOpen.value = true
}

async function createEngine(nextConfig: LastChancesConfig, createNewGeneration = false) {
  engine.value?.destroy()
  engine.value = null
  config.value = cloneLastChancesConfig(nextConfig)
  plan.value = null
  snapshot.value = null
  visitedNodeIds.value = new Set()
  await nextTick()
  if (!canvas.value) throw new Error('99LC canvas is unavailable')
  const instance = new LastChancesEngine(canvas.value, config.value, {
    onPlan: nextPlan => { plan.value = nextPlan },
    onSnapshot,
  })
  engine.value = instance
  if (createNewGeneration) instance.newGeneration()
  instance.start()
  routeMapOpen.value = true
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
  routeMapOpen.value = false
  resumeAfterMap = false
  void nextTick(() => canvas.value?.focus())
}

function openMap() {
  if (!plan.value || !snapshot.value) return
  resumeAfterMap = snapshot.value.phase === 'playing' && !snapshot.value.paused
  if (resumeAfterMap) engine.value?.setPaused(true)
  routeMapOpen.value = true
}

function closeMap() {
  routeMapOpen.value = false
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
  routeMapOpen.value = true
}

function newGeneration() {
  engine.value?.newGeneration()
  routeMapOpen.value = true
  builderOpen.value = false
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

async function applyBuilder(nextConfig: LastChancesConfig, restart: boolean) {
  try {
    await createEngine(nextConfig, restart)
    builderOpen.value = false
    resumeAfterBuilder = false
    setToast(t.value.applied)
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : String(error)
  }
}

function saveBuilderOverride(nextConfig: LastChancesConfig) {
  try {
    saveLastChancesConfigOverride(nextConfig)
    setToast(t.value.saved)
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : String(error)
  }
}

async function clearBuilderOverride() {
  clearLastChancesConfigOverride()
  await loadDefinition(false)
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

function formatKey(code: string): string {
  if (code.startsWith('Key')) return code.slice(3)
  if (code.startsWith('Digit')) return code.slice(5)
  return code.replace('Arrow', '↑').replace('Space', 'Space')
}

function formatNumber(value: number): string {
  return Math.abs(value % 1) < 0.01 ? String(Math.round(value)) : value.toFixed(1)
}

function deathReason(): string {
  const reason = snapshot.value?.deathReason
  if (!reason) return t.value.attemptEnded
  if (reason === 'Mental health collapsed') return t.value.mindCollapsed
  if (reason.startsWith('Killed by ')) return `${t.value.killedBy} ${reason.slice('Killed by '.length)}`
  return reason
}

onMounted(() => { void loadDefinition() })

onBeforeUnmount(() => {
  loadController?.abort()
  engine.value?.destroy()
  if (toastTimer !== null) window.clearTimeout(toastTimer)
})
</script>

<template>
  <div
    class="lc-page"
    :class="{
      'is-mental-low': mentalPercent < 35,
      'is-critical': hpPercent < 30,
    }"
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
        <button type="button" :disabled="!plan" @click="openMap">
          <MapIcon :size="15" aria-hidden="true" />{{ t.map }}
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
              <strong>{{ recentGesture.attackName }}</strong>
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
            :primary-name="leftWeapon?.name ?? '—'"
            :secondary-name="rightWeapon?.name ?? '—'"
            :disabled="snapshot?.phase !== 'playing' || snapshot.paused"
            @move="setTouchMove"
            @aim="setTouchAim"
            @press="pressTouch"
            @release="releaseTouch"
          />
        </div>

        <footer class="lc-stage-footer">
          <span><i :class="alertedEnemies ? 'is-alert' : ''" aria-hidden="true" />{{ t.currentThreat }} · {{ alertedEnemies ? t.noticed : t.calm }}</span>
          <span v-if="snapshot"><Activity :size="12" aria-hidden="true" />{{ t.speed }} {{ formatNumber(snapshot.player.stats.moveSpeed) }} · {{ t.armor }} {{ formatNumber(snapshot.player.stats.armor) }}</span>
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

        <section class="lc-run-card">
          <header><MapIcon :size="15" aria-hidden="true" /><span>{{ t.map }}</span><button type="button" @click="openMap">{{ t.chooseRoute }}<ChevronRight :size="12" aria-hidden="true" /></button></header>
          <dl>
            <div><dt>{{ t.tier }}</dt><dd>{{ (snapshot?.currentTierIndex ?? 0) + 1 }} / {{ config?.progression.tiers.length ?? 7 }}</dd></div>
            <div><dt>{{ t.room }}</dt><dd>{{ currentNode?.roomName ?? t.noRoom }}</dd></div>
            <div><dt>{{ t.enemies }}</dt><dd>{{ livingEnemies.length }}</dd></div>
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

        <WeaponCooldowns :locale="locale" :weapons="weaponCooldowns" />
      </aside>
    </main>

    <section class="lc-controls-dock" :aria-label="t.controls">
      <header>
        <div><Eye :size="15" aria-hidden="true" /><span>{{ t.controls }}</span></div>
        <p>{{ t.controlsHelp }}</p>
      </header>
      <div class="lc-control-grid">
        <article>
          <Keyboard :size="18" aria-hidden="true" />
          <div><strong>{{ t.keyboard }}</strong><span>{{ t.keyboardMove }}</span><small>{{ t.keyboardAttack }} {{ config?.input.leftKeys.map(formatKey).join('/') || 'Q' }} + {{ config?.input.rightKeys.map(formatKey).join('/') || 'E' }}</small></div>
        </article>
        <article>
          <MousePointer2 :size="18" aria-hidden="true" />
          <div><strong>{{ t.mouse }}</strong><span>{{ t.mouseAim }}</span><small>{{ t.mouseAttack }}</small></div>
        </article>
        <article>
          <Gamepad2 :size="18" aria-hidden="true" />
          <div><strong>{{ t.gamepad }}</strong><span>{{ t.gamepadMove }}</span><small>{{ t.gamepadAttack }} L{{ config?.input.gamepadLeftButton ?? 4 }} / R{{ config?.input.gamepadRightButton ?? 5 }}</small></div>
        </article>
        <article>
          <Smartphone :size="18" aria-hidden="true" />
          <div><strong>{{ t.touch }}</strong><span>{{ t.touchMove }}</span><small>{{ t.touchAttack }}</small></div>
        </article>
      </div>
      <div class="lc-gesture-guide">
        <strong>{{ t.gestureGuide }}</strong>
        <span v-for="gesture in LAST_CHANCES_GESTURES" :key="gesture">
          {{ t[gesture] }}
        </span>
      </div>
    </section>

    <RunMapOverlay
      :open="routeMapOpen"
      :locale="locale"
      :nodes="runMapNodes"
      :edges="runMapEdges"
      :seed="plan?.seed ?? config?.seed ?? '—'"
      :allow-close="snapshot?.phase === 'playing'"
      @choose="chooseNode"
      @close="closeMap"
    />

    <BuilderDrawer
      :open="builderOpen"
      :locale="locale"
      :config="config"
      @close="closeBuilder"
      @apply="applyBuilder"
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
.lc-header-actions button:disabled { opacity: 0.32; cursor: not-allowed; }
.lc-live-state { display: inline-flex; align-items: center; gap: 0.35rem; margin-right: 0.2rem; color: #686d69; font-size: 0.53rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.lc-live-state i { width: 0.42rem; height: 0.42rem; border-radius: 50%; background: #676b68; }
.lc-live-state.active { color: #899575; }
.lc-live-state.active i { background: #8fa06c; box-shadow: 0 0 0.55rem #8fa06c; }

.lc-cockpit { min-width: 0; display: grid; grid-template-columns: minmax(0, 1fr) 19rem; gap: 0.8rem; }
.lc-stage-panel { min-width: 0; overflow: hidden; border: 1px solid var(--lc-line); border-radius: 0.85rem; background: #080a0b; box-shadow: 0 1.5rem 4rem rgba(0, 0, 0, 0.32); }
.lc-stage-screen { position: relative; min-height: clamp(31rem, 61vh, 46rem); overflow: hidden; background: #08080b; }
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

.lc-phase-overlay,
.lc-loading-overlay { position: absolute; z-index: 30; inset: 0; display: grid; place-items: center; padding: 1rem; background: rgba(4, 5, 6, 0.78); backdrop-filter: blur(5px); }
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
.lc-run-card,
.lc-erosion-card { border: 1px solid var(--lc-line); border-radius: 0.7rem; background: linear-gradient(145deg, rgba(255, 255, 255, 0.02), rgba(8, 10, 11, 0.55)); box-shadow: 0 0.7rem 1.5rem rgba(0, 0, 0, 0.17); }
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

.lc-erosion-card { padding: 0.65rem; }
.lc-erosion-card header { display: grid; gap: 0.08rem; }
.lc-erosion-card header span { display: inline-flex; align-items: center; gap: 0.35rem; color: #ca777b; font-size: 0.61rem; font-weight: 800; letter-spacing: 0.07em; text-transform: uppercase; }
.lc-erosion-card header small { color: #5f6461; font-size: 0.5rem; }
.lc-erosion-card > p { margin: 0.6rem 0 0; color: #687062; font-size: 0.58rem; }
.lc-erosion-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.3rem; margin-top: 0.55rem; }
.lc-erosion-grid div { min-width: 0; display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 0.3rem; padding: 0.32rem; border: 1px solid rgba(183, 79, 87, 0.1); border-radius: 0.35rem; color: #936267; background: rgba(134, 45, 53, 0.045); }
.lc-erosion-grid span { overflow: hidden; color: #737774; font-size: 0.48rem; text-overflow: ellipsis; white-space: nowrap; }
.lc-erosion-grid strong { color: #c36d72; font: 700 0.54rem/1 var(--font-mono, monospace); }

.lc-controls-dock { overflow: hidden; border: 1px solid var(--lc-line); border-radius: 0.75rem; background: rgba(9, 11, 12, 0.6); }
.lc-controls-dock > header { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.5rem 0.7rem; border-bottom: 1px solid rgba(255, 255, 255, 0.05); }
.lc-controls-dock > header div { display: inline-flex; align-items: center; gap: 0.4rem; color: #c9c5bc; }
.lc-controls-dock > header span { font-size: 0.6rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; }
.lc-controls-dock > header p { margin: 0; color: #606562; font-size: 0.52rem; }
.lc-control-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); }
.lc-control-grid article { min-width: 0; display: grid; grid-template-columns: auto 1fr; align-items: center; gap: 0.5rem; padding: 0.55rem 0.7rem; border-right: 1px solid rgba(255, 255, 255, 0.045); color: #8c7650; }
.lc-control-grid article:last-child { border-right: 0; }
.lc-control-grid article div { min-width: 0; display: grid; }
.lc-control-grid strong { color: #b8b7b1; font-size: 0.56rem; font-weight: 800; text-transform: uppercase; }
.lc-control-grid span,
.lc-control-grid small { overflow: hidden; color: #666b68; font-size: 0.49rem; line-height: 1.3; text-overflow: ellipsis; white-space: nowrap; }
.lc-control-grid small { color: #80765e; }
.lc-gesture-guide { display: flex; align-items: center; justify-content: center; flex-wrap: wrap; gap: 0.35rem; padding: 0.42rem 0.6rem; border-top: 1px solid rgba(255, 255, 255, 0.045); }
.lc-gesture-guide strong { margin-right: 0.25rem; color: #6d716e; font-size: 0.5rem; font-weight: 800; text-transform: uppercase; }
.lc-gesture-guide span { padding: 0.2rem 0.38rem; border: 1px solid rgba(255, 255, 255, 0.065); border-radius: 999px; color: #989a94; background: rgba(255, 255, 255, 0.02); font-size: 0.47rem; font-weight: 650; }

.lc-page-toast { position: fixed; z-index: 5000; left: 50%; bottom: max(1rem, env(safe-area-inset-bottom)); max-width: min(34rem, calc(100vw - 2rem)); margin: 0; padding: 0.65rem 0.9rem; transform: translateX(-50%); border: 1px solid rgba(191, 160, 89, 0.3); border-radius: 0.5rem; color: #d7c594; background: rgba(10, 12, 13, 0.94); box-shadow: 0 1rem 2rem rgba(0, 0, 0, 0.45); font-size: 0.64rem; }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

.lc-page.is-mental-low .lc-stage-screen { box-shadow: inset 0 0 5rem rgba(99, 61, 116, 0.12); }
.lc-page.is-critical .lc-stage-screen { box-shadow: inset 0 0 6rem rgba(139, 31, 40, 0.16); }

@keyframes lc-loading-turn { to { transform: rotate(360deg); } }
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
}

@media (max-width: 620px) {
  .lc-page { gap: 0.45rem; }
  .lc-page-header { display: grid; padding: 0.5rem; }
  .lc-title-sigil { width: 2.5rem; height: 2.5rem; }
  .lc-title-lockup p { font-size: 0.44rem; }
  .lc-title-lockup h1 { font-size: 1.1rem; }
  .lc-header-actions { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); width: 100%; }
  .lc-header-actions button { min-width: 0; padding: 0.35rem 0.2rem; font-size: 0.48rem; }
  .lc-header-actions button svg { display: none; }
  .lc-stage-screen { min-height: 74dvh; }
  .lc-stage-hud { grid-template-columns: 1fr 1fr; gap: 0.35rem 0.7rem; padding: 0.45rem 0.55rem 1.2rem; }
  .lc-room-readout { grid-column: 1 / -1; grid-row: 1; }
  .lc-vital-label span { font-size: 0.43rem; }
  .lc-vital-label strong { font-size: 0.48rem; }
  .lc-gesture-toast { top: 5.6rem; }
  .lc-stage-footer { display: none; }
  .lc-telemetry { grid-template-columns: 1fr 1fr; gap: 0.4rem; }
  .lc-chance-card { grid-column: 1 / -1; grid-row: auto; }
  .lc-run-card,
  .lc-erosion-card { min-height: 100%; }
  .lc-telemetry :deep(.lc-cooldowns) { grid-column: 1 / -1; }
  .lc-controls-dock > header { display: block; }
  .lc-controls-dock > header p { margin-top: 0.2rem; }
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
