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
  LAST_CHANCES_GESTURES,
  migrateLastChancesConfig,
  type LastChancesConfig,
  type LastChancesGesture,
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
    input: 'Input timing',
    inputHelp: 'Windows used by both hands and every control method',
    attack: 'Selected attack',
    attackHelp: 'One weapon gesture at a time',
    enemy: 'Selected enemy',
    enemyHelp: 'Awareness, pursuit and attack tuning',
    weapon: 'Weapon',
    gesture: 'Gesture',
    enemySelect: 'Enemy',
    tier: 'Tier',
    maxHp: 'Physical health',
    maxMental: 'Mental health',
    moveSpeed: 'Move speed',
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
    gamepadDeadZone: 'Gamepad dead zone',
    gamepadLeftButton: 'Primary button index',
    gamepadRightButton: 'Secondary button index',
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
    enemyHp: 'Health',
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
    input: 'Тайминги управления',
    inputHelp: 'Окна для обеих рук и всех способов управления',
    attack: 'Выбранная атака',
    attackHelp: 'Один жест оружия за раз',
    enemy: 'Выбранный враг',
    enemyHelp: 'Обнаружение, преследование и настройка атак',
    weapon: 'Оружие',
    gesture: 'Жест',
    enemySelect: 'Враг',
    tier: 'Уровень',
    maxHp: 'Физическое здоровье',
    maxMental: 'Ментальное здоровье',
    moveSpeed: 'Скорость движения',
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
    gamepadDeadZone: 'Мёртвая зона геймпада',
    gamepadLeftButton: 'Индекс основной кнопки',
    gamepadRightButton: 'Индекс вторичной кнопки',
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
    enemyHp: 'Здоровье',
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
const selectedGesture = ref<LastChancesGesture>('tap')
const selectedEnemyIndex = ref(0)
const selectedTierIndex = ref(0)
const fileInput = ref<HTMLInputElement | null>(null)
let syncingRaw = false

const validation = computed(() => draft.value
  ? validateLastChancesConfig(draft.value)
  : { valid: false, errors: [t.value.noConfig] })
const selectedWeapon = computed(() => draft.value?.weapons[selectedWeaponIndex.value] ?? null)
const selectedAttack = computed(() => selectedWeapon.value?.attacks[selectedGesture.value] ?? null)
const selectedEnemy = computed(() => draft.value?.enemies[selectedEnemyIndex.value] ?? null)
const selectedTier = computed(() => draft.value?.progression.tiers[selectedTierIndex.value] ?? null)

function loadDraft(config: LastChancesConfig) {
  draft.value = cloneLastChancesConfig(config)
  syncingRaw = true
  rawJson.value = JSON.stringify(draft.value, null, 2)
  rawError.value = ''
  notice.value = ''
  syncingRaw = false
  selectedWeaponIndex.value = Math.min(selectedWeaponIndex.value, Math.max(0, config.weapons.length - 1))
  selectedEnemyIndex.value = Math.min(selectedEnemyIndex.value, Math.max(0, config.enemies.length - 1))
  selectedTierIndex.value = Math.min(selectedTierIndex.value, Math.max(0, config.progression.tiers.length - 1))
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
    const value = migrateLastChancesConfig(JSON.parse(rawJson.value) as unknown)
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
                  <label>{{ t.moveSpeed }}<input v-model.number="draft.player.baseStats.moveSpeed" type="number" min="1" step="1" /></label>
                  <label>{{ t.armor }}<input v-model.number="draft.player.baseStats.armor" type="number" min="0" step="1" /></label>
                  <label>{{ t.attackPower }}<input v-model.number="draft.player.baseStats.attackPower" type="number" min="1" step="1" /></label>
                  <label>{{ t.radius }}<input v-model.number="draft.player.radius" type="number" min="1" step="1" /></label>
                  <label>{{ t.invulnerability }}<input v-model.number="draft.player.invulnerabilityMs" type="number" min="0" step="25" /></label>
                </div>
              </fieldset>

              <fieldset>
                <legend><span><RotateCcw :size="15" aria-hidden="true" />{{ t.chances }}</span><small>{{ t.chancesHelp }}</small></legend>
                <div class="lc-fields-grid">
                  <label>{{ t.chanceCount }}<input v-model.number="draft.chances" type="number" min="1" step="1" /></label>
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
                <div class="lc-fields-grid">
                  <label>{{ t.doubleTap }}<input v-model.number="draft.input.doubleTapMs" type="number" min="1" step="10" /></label>
                  <label>{{ t.tapCombo }}<input v-model.number="draft.input.tapComboWindowMs" type="number" min="1" step="10" /></label>
                  <label>{{ t.hold }}<input v-model.number="draft.input.holdMs" type="number" min="1" step="10" /></label>
                  <label>{{ t.holdMax }}<input v-model.number="draft.input.holdMaxMs" type="number" min="1" step="10" /></label>
                  <label>{{ t.holdDouble }}<input v-model.number="draft.input.holdThenDoubleTapWindowMs" type="number" min="1" step="10" /></label>
                  <label>{{ t.aimDeadZone }}<input v-model.number="draft.input.aimDeadZone" type="number" min="0" max="1" step="0.01" /></label>
                  <label>{{ t.gamepadDeadZone }}<input v-model.number="draft.input.gamepadDeadZone" type="number" min="0" max="1" step="0.01" /></label>
                  <label>{{ t.gamepadLeftButton }}<input v-model.number="draft.input.gamepadLeftButton" type="number" min="0" max="31" step="1" /></label>
                  <label>{{ t.gamepadRightButton }}<input v-model.number="draft.input.gamepadRightButton" type="number" min="0" max="31" step="1" /></label>
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
                  <label>{{ t.gesture }}
                    <select v-model="selectedGesture">
                      <option v-for="gesture in LAST_CHANCES_GESTURES" :key="gesture" :value="gesture">{{ t.gestureNames[gesture] }}</option>
                    </select>
                  </label>
                </div>
                <div v-if="selectedAttack" class="lc-fields-grid">
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
.lc-fields-grid select { width: 100%; min-height: 2rem; padding: 0.38rem 0.45rem; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 0.38rem; outline: none; color: #e1ded5; background: #0b0e0f; font: 600 0.66rem/1.2 var(--font-mono, monospace); color-scheme: dark; }
.lc-fields-grid input:focus,
.lc-fields-grid select:focus { border-color: rgba(208, 172, 92, 0.6); box-shadow: 0 0 0 2px rgba(208, 172, 92, 0.09); }
.lc-select-row { grid-template-columns: repeat(2, minmax(0, 1fr)); margin-bottom: 0.55rem; }

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
.lc-builder-apply-actions { display: grid !important; grid-template-columns: 1fr 1fr; }
.lc-builder-apply-actions button { min-height: 2.5rem; }
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
