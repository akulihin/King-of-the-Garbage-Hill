<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import {
  AlertTriangle,
  Check,
  Minus,
  Pencil,
  Plus,
  Save,
  ShieldCheck,
  Trash2,
  UserRoundX,
  Users,
  X,
} from 'lucide-vue-next'

interface PopulationCategoryView {
  id: string
  name: string
  amount: number
  color?: string
}

const props = withDefaults(defineProps<{
  open?: boolean
  cityName: string
  total: number
  nonWorking: number
  loyalty: number
  categories: PopulationCategoryView[]
  editorMode?: boolean
}>(), {
  open: true,
  editorMode: false,
})

const emit = defineEmits<{
  save: [categories: PopulationCategoryView[]]
  close: []
}>()

const palette = ['#75ad62', '#c987a8', '#6ca9c8', '#ad79b8', '#c4a45f', '#789b8b', '#bd766d']
const dialog = ref<HTMLElement | null>(null)
const draftCategories = ref<PopulationCategoryView[]>([])
let previouslyFocused: HTMLElement | null = null

const workingPopulation = computed(() => Math.max(0, props.total - props.nonWorking))
const distributedTotal = computed(() => draftCategories.value.reduce((sum, category) => sum + finiteAmount(category.amount), 0))
const unassignedPopulation = computed(() => Math.max(0, workingPopulation.value - distributedTotal.value))
const overAssignedPopulation = computed(() => Math.max(0, distributedTotal.value - workingPopulation.value))
const nonWorkingPercent = computed(() => props.total <= 0 ? 0 : Math.min(100, Math.max(0, props.nonWorking / props.total * 100)))
const loyaltyPercent = computed(() => (Math.max(-9, Math.min(9, props.loyalty)) + 9) / 18 * 100)
const disloyalPercent = computed(() => props.loyalty >= 0 ? 0 : Math.min(100, Math.abs(props.loyalty) / 9 * 100))
const loyaltyLabel = computed(() => {
  if (props.loyalty <= -6) return 'Опасное недовольство'
  if (props.loyalty < 0) return 'Нелояльность'
  if (props.loyalty === 0) return 'Нейтрально'
  if (props.loyalty >= 6) return 'Преданы городу'
  return 'Лояльны'
})
const draftIsValid = computed(() => draftCategories.value.every(category => (
  category.name.trim().length > 0
  && Number.isFinite(Number(category.amount))
  && Number(category.amount) >= 0
)))
const displaySegments = computed(() => {
  const segments = draftCategories.value
    .filter(category => finiteAmount(category.amount) > 0)
    .map((category, index) => ({
      ...category,
      amount: finiteAmount(category.amount),
      color: category.color || palette[index % palette.length],
    }))
  if (unassignedPopulation.value > 0) {
    segments.push({ id: '__unassigned', name: 'Не распределены', amount: unassignedPopulation.value, color: '#706f68' })
  }
  return segments
})

watch(() => props.categories, categories => {
  draftCategories.value = categories.map(category => ({ ...category }))
}, { deep: true, immediate: true })

watch(() => props.open, async open => {
  if (open) {
    previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
    await nextTick()
    dialog.value?.focus()
    return
  }
  previouslyFocused?.focus()
  previouslyFocused = null
}, { immediate: true })

onBeforeUnmount(() => previouslyFocused?.focus())

function finiteAmount(value: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) ? Math.max(0, parsed) : 0
}

function formatNumber(value: number) {
  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 }).format(value)
}

function segmentWidth(amount: number) {
  if (workingPopulation.value <= 0) return '0%'
  return `${Math.min(100, Math.max(1.5, amount / workingPopulation.value * 100))}%`
}

function addCategory() {
  const index = draftCategories.value.length
  draftCategories.value.push({
    id: `population-${Date.now()}-${index}`,
    name: `Новая группа ${index + 1}`,
    amount: 0,
    color: palette[index % palette.length],
  })
}

function removeCategory(index: number) {
  draftCategories.value.splice(index, 1)
}

function saveDraft() {
  if (!draftIsValid.value) return
  emit('save', draftCategories.value.map(category => ({
    ...category,
    name: category.name.trim(),
    amount: finiteAmount(category.amount),
  })))
}

function requestClose() {
  emit('close')
}

function trapFocus(event: KeyboardEvent) {
  if (!dialog.value) return
  const focusable = Array.from(dialog.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )).filter(element => !element.hidden)
  if (!focusable.length) {
    event.preventDefault()
    dialog.value.focus()
    return
  }
  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}
</script>

<template>
  <Teleport to="body">
    <Transition name="population-dialog">
      <div v-if="open" class="population-backdrop" @click.self="requestClose">
        <section
          ref="dialog"
          class="population-dialog"
          role="dialog"
          aria-modal="true"
          aria-labelledby="population-dialog-title"
          tabindex="-1"
          @keydown.esc.stop="requestClose"
          @keydown.tab="trapFocus"
        >
          <header class="dialog-header">
            <div class="title-icon"><Users :size="21" aria-hidden="true" /></div>
            <div>
              <span>{{ editorMode ? 'Редактор населения' : 'Перепись города' }}</span>
              <h2 id="population-dialog-title">{{ cityName }}</h2>
            </div>
            <button type="button" class="close-button" aria-label="Закрыть окно населения" @click="requestClose">
              <X :size="18" />
            </button>
          </header>

          <div class="summary-grid">
            <div>
              <Users :size="17" />
              <span>Всего жителей</span>
              <strong>{{ formatNumber(total) }}</strong>
            </div>
            <div>
              <UserRoundX :size="17" />
              <span>Нетрудоспособные</span>
              <strong>{{ formatNumber(nonWorking) }}</strong>
            </div>
            <div :class="{ danger: loyalty < 0, positive: loyalty > 0 }">
              <ShieldCheck :size="17" />
              <span>Лояльность</span>
              <strong>{{ loyalty > 0 ? '+' : '' }}{{ loyalty }}</strong>
              <small>{{ loyaltyLabel }}</small>
            </div>
          </div>

          <div class="distribution-card">
            <div class="distribution-heading">
              <div>
                <span>Распределение жителей</span>
                <strong>{{ formatNumber(distributedTotal) }} из {{ formatNumber(workingPopulation) }} трудоспособных</strong>
              </div>
              <span v-if="editorMode" class="editable-badge"><Pencil :size="12" /> редактируется</span>
            </div>

            <div class="loyalty-scale" :class="{ negative: loyalty < 0, positive: loyalty > 0 }">
              <span>−9</span>
              <div class="loyalty-track">
                <i class="loyalty-zero" aria-hidden="true" />
                <b :style="{ left: `${loyaltyPercent}%` }" :title="`Лояльность: ${loyalty}`" />
              </div>
              <span>+9</span>
            </div>

            <div class="population-bars" aria-label="Схема распределения населения">
              <span class="bar-label">Работающие</span>
              <div class="working-bar">
                <div
                  v-for="segment in displaySegments"
                  :key="segment.id"
                  class="population-segment"
                  :class="{ unassigned: segment.id === '__unassigned' }"
                  :style="{ width: segmentWidth(segment.amount), '--segment-color': segment.color }"
                  :title="`${segment.name}: ${formatNumber(segment.amount)}`"
                >
                  <strong>{{ segment.name }}</strong>
                  <small>{{ formatNumber(segment.amount) }}</small>
                </div>
                <div v-if="disloyalPercent > 0" class="disloyal-overlay" :style="{ width: `${disloyalPercent}%` }">
                  нелояльные
                </div>
              </div>

              <span class="bar-label">Нетрудоспособные</span>
              <div class="nonworking-track">
                <div class="nonworking-segment" :style="{ width: `${nonWorkingPercent}%` }">
                  <span>{{ formatNumber(nonWorking) }}</span>
                </div>
                <div class="working-remainder" :style="{ width: `${100 - nonWorkingPercent}%` }" />
              </div>
            </div>

            <div v-if="overAssignedPopulation > 0" class="allocation-warning" role="status">
              <AlertTriangle :size="15" />
              Распределено на {{ formatNumber(overAssignedPopulation) }} больше доступной рабочей силы.
            </div>
            <div v-else-if="unassignedPopulation > 0" class="allocation-note" role="status">
              <Minus :size="14" />
              {{ formatNumber(unassignedPopulation) }} трудоспособных жителей пока не распределены.
            </div>
          </div>

          <div class="category-list" :class="{ editable: editorMode }">
            <div class="category-list-heading">
              <div>
                <span>Сословия и занятость</span>
                <small>{{ editorMode ? 'Названия и стартовые значения попадут в конфигурацию.' : 'Текущее распределение рабочей силы.' }}</small>
              </div>
              <button v-if="editorMode" type="button" @click="addCategory"><Plus :size="14" /> Добавить</button>
            </div>

            <div v-for="(category, index) in draftCategories" :key="category.id" class="category-row">
              <span class="category-color" :style="{ background: category.color || palette[index % palette.length] }" />
              <template v-if="editorMode">
                <label>
                  <span class="sr-only">Название категории {{ index + 1 }}</span>
                  <input v-model="category.name" type="text" maxlength="48" :aria-label="`Название категории ${index + 1}`" />
                </label>
                <label class="amount-field">
                  <span class="sr-only">Количество жителей в категории {{ category.name }}</span>
                  <input v-model.number="category.amount" type="number" min="0" step="1000" :aria-label="`Количество: ${category.name}`" />
                  <span>чел.</span>
                </label>
                <button type="button" class="remove-button" :aria-label="`Удалить категорию ${category.name}`" @click="removeCategory(index)">
                  <Trash2 :size="15" />
                </button>
              </template>
              <template v-else>
                <strong>{{ category.name }}</strong>
                <span class="category-value">{{ formatNumber(category.amount) }}</span>
              </template>
            </div>

            <div v-if="!draftCategories.length" class="empty-categories">
              <Users :size="24" />
              <span>{{ editorMode ? 'Добавьте первое сословие.' : 'Распределение ещё не задано.' }}</span>
            </div>
          </div>

          <footer class="dialog-footer">
            <p>
              Эффективная рабочая сила зависит от лояльности; при нехватке людей производственные уровни отключаются.
            </p>
            <div>
              <button type="button" class="secondary-button" @click="requestClose">Закрыть</button>
              <button v-if="editorMode" type="button" class="save-button" :disabled="!draftIsValid" @click="saveDraft">
                <Save :size="15" /> Сохранить распределение
              </button>
              <span v-else class="read-only-status"><Check :size="14" /> Только просмотр</span>
            </div>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.population-backdrop {
  position: fixed;
  z-index: 1200;
  display: grid;
  inset: 0;
  place-items: center;
  overflow-y: auto;
  padding: 24px;
  background: rgba(5, 7, 5, 0.78);
  backdrop-filter: blur(8px);
}

.population-dialog {
  --dialog-gold: #c9aa67;
  width: min(920px, 100%);
  max-height: min(860px, calc(100vh - 48px));
  overflow: auto;
  border: 1px solid rgba(226, 204, 158, 0.23);
  border-radius: 18px;
  color: #eee5d1;
  background: linear-gradient(160deg, #1c1f18, #11140f 68%);
  box-shadow: 0 32px 100px rgba(0, 0, 0, 0.62);
}
.population-dialog:focus { outline: none; }
.population-dialog:focus-visible { outline: 2px solid var(--dialog-gold); outline-offset: 3px; }

.dialog-header { position: sticky; z-index: 4; top: 0; display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 12px; padding: 16px 19px; border-bottom: 1px solid rgba(226, 204, 158, 0.15); background: rgba(27, 29, 23, 0.96); backdrop-filter: blur(12px); }
.title-icon { display: grid; width: 38px; height: 38px; place-items: center; border: 1px solid rgba(213, 185, 124, 0.25); border-radius: 10px; color: #e2c982; background: rgba(201, 170, 103, 0.08); }
.dialog-header span { color: var(--dialog-gold); font: 800 0.58rem/1 var(--font-mono, monospace); letter-spacing: 0.12em; text-transform: uppercase; }
.dialog-header h2 { margin: 4px 0 0; color: #f5ead3; font: 700 1.35rem/1 Georgia, serif; }
.close-button { display: grid; width: 36px; height: 36px; place-items: center; border: 1px solid rgba(226, 204, 158, 0.14); border-radius: 9px; color: rgba(238, 229, 209, 0.62); background: rgba(255, 255, 255, 0.03); cursor: pointer; }
.close-button:hover { border-color: rgba(226, 204, 158, 0.38); color: #fff2d9; }

.summary-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); border-bottom: 1px solid rgba(226, 204, 158, 0.12); }
.summary-grid > div { display: grid; min-height: 78px; grid-template-columns: 24px 1fr auto; grid-template-rows: auto auto; align-items: center; gap: 3px 7px; padding: 12px 16px; border-right: 1px solid rgba(226, 204, 158, 0.12); }
.summary-grid > div:last-child { border-right: 0; }
.summary-grid svg { grid-row: 1 / 3; color: #cdb473; }
.summary-grid span { color: rgba(238, 229, 209, 0.5); font: 800 0.57rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.summary-grid strong { grid-column: 3; grid-row: 1 / 3; font: 800 1.15rem/1 var(--font-mono, monospace); }
.summary-grid small { grid-column: 2; color: rgba(238, 229, 209, 0.4); font-size: 0.61rem; }
.summary-grid .danger strong, .summary-grid .danger svg { color: #de7d70; }
.summary-grid .positive strong, .summary-grid .positive svg { color: #91c88c; }

.distribution-card { margin: 18px; padding: 16px; border: 1px solid rgba(226, 204, 158, 0.14); border-radius: 13px; background: rgba(8, 10, 8, 0.3); }
.distribution-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
.distribution-heading > div { display: grid; gap: 4px; }
.distribution-heading > div span { color: var(--dialog-gold); font: 800 0.58rem/1 var(--font-mono, monospace); letter-spacing: 0.1em; text-transform: uppercase; }
.distribution-heading > div strong { color: #e8ddc7; font-size: 0.72rem; }
.editable-badge { display: inline-flex; align-items: center; gap: 4px; padding: 5px 7px; border-radius: 999px; color: #9dd8d5; background: rgba(71, 145, 143, 0.12); font: 800 0.53rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.loyalty-scale { display: grid; grid-template-columns: 22px 1fr 22px; align-items: center; gap: 7px; margin: 16px 0 14px; color: rgba(238, 229, 209, 0.35); font: 700 0.54rem/1 var(--font-mono, monospace); }
.loyalty-track { position: relative; height: 6px; border-radius: 99px; background: linear-gradient(90deg, #a64d46, #777168 50%, #6ba36d); }
.loyalty-zero { position: absolute; top: -3px; bottom: -3px; left: 50%; width: 1px; background: rgba(255, 255, 255, 0.55); }
.loyalty-track b { position: absolute; top: 50%; width: 13px; height: 13px; border: 2px solid #181a15; border-radius: 50%; background: #f2db9b; box-shadow: 0 0 0 2px rgba(242, 219, 155, 0.25); transform: translate(-50%, -50%); }

.population-bars { display: grid; grid-template-columns: 104px minmax(0, 1fr); gap: 8px 12px; align-items: stretch; }
.bar-label { align-self: center; color: rgba(238, 229, 209, 0.52); font: 800 0.57rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.working-bar { position: relative; display: flex; min-height: 78px; overflow: hidden; border: 1px solid rgba(226, 204, 158, 0.16); border-radius: 9px; background: #292b25; }
.population-segment { position: relative; display: grid; min-width: 34px; align-content: center; justify-items: center; overflow: hidden; padding: 8px 5px; border-right: 1px solid rgba(16, 18, 14, 0.38); color: #10130f; text-align: center; background: var(--segment-color); }
.population-segment::after { content: ''; position: absolute; inset: 0; background: linear-gradient(rgba(255, 255, 255, 0.12), transparent 35%, rgba(0, 0, 0, 0.08)); pointer-events: none; }
.population-segment strong, .population-segment small { position: relative; z-index: 1; max-width: 100%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.population-segment strong { font-size: 0.68rem; }
.population-segment small { margin-top: 3px; font: 700 0.56rem/1 var(--font-mono, monospace); opacity: 0.76; }
.population-segment.unassigned { color: #ded9cc; background: repeating-linear-gradient(135deg, #585a54 0 8px, #4c4e48 8px 16px); }
.disloyal-overlay { position: absolute; z-index: 2; top: 0; right: 0; display: flex; height: 16px; align-items: center; justify-content: center; overflow: hidden; color: #e4dfd6; background: rgba(69, 68, 65, 0.78); font: 800 0.49rem/1 var(--font-mono, monospace); letter-spacing: 0.08em; text-transform: uppercase; }
.nonworking-track { display: flex; height: 42px; overflow: hidden; border: 1px solid rgba(226, 204, 158, 0.12); border-radius: 8px; background: #292b25; }
.nonworking-segment { display: grid; min-width: 2px; place-items: center; color: #d7d3c8; background: #080908; }
.nonworking-segment span { overflow: hidden; font: 700 0.58rem/1 var(--font-mono, monospace); text-overflow: ellipsis; }
.working-remainder { background: rgba(107, 137, 104, 0.14); }
.allocation-warning, .allocation-note { display: flex; align-items: center; gap: 6px; margin-top: 12px; padding: 7px 9px; border-radius: 7px; font-size: 0.64rem; }
.allocation-warning { color: #e2a397; background: rgba(174, 77, 64, 0.12); }
.allocation-note { color: #cabb93; background: rgba(201, 170, 103, 0.08); }

.category-list { display: grid; gap: 6px; margin: 0 18px 18px; }
.category-list-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 12px; margin-bottom: 4px; }
.category-list-heading > div { display: grid; gap: 4px; }
.category-list-heading span { color: var(--dialog-gold); font: 800 0.58rem/1 var(--font-mono, monospace); letter-spacing: 0.1em; text-transform: uppercase; }
.category-list-heading small { color: rgba(238, 229, 209, 0.42); font-size: 0.63rem; }
.category-list-heading button { display: inline-flex; align-items: center; gap: 5px; padding: 7px 9px; border: 1px solid rgba(124, 190, 187, 0.3); border-radius: 7px; color: #a9dedb; background: rgba(71, 145, 143, 0.1); cursor: pointer; font-size: 0.62rem; font-weight: 800; }
.category-row { display: grid; min-height: 46px; grid-template-columns: 9px minmax(0, 1fr) auto; align-items: center; gap: 10px; padding: 7px 10px; border: 1px solid rgba(226, 204, 158, 0.1); border-radius: 8px; background: rgba(255, 255, 255, 0.022); }
.category-row:focus-within { border-color: rgba(201, 170, 103, 0.45); }
.category-color { width: 8px; height: 28px; border-radius: 99px; }
.category-row > strong { font-size: 0.72rem; }
.category-value { color: #e3d1a1; font: 800 0.68rem/1 var(--font-mono, monospace); }
.editable .category-row { grid-template-columns: 9px minmax(120px, 1fr) minmax(160px, 0.65fr) 34px; }
.category-row label { min-width: 0; }
.category-row input { width: 100%; min-width: 0; height: 32px; padding: 0 9px; border: 1px solid rgba(226, 204, 158, 0.13); border-radius: 6px; color: #eee5d1; background: rgba(6, 8, 6, 0.35); font-size: 0.7rem; }
.category-row input:focus { outline: 1px solid var(--dialog-gold); border-color: var(--dialog-gold); }
.amount-field { position: relative; }
.amount-field input { padding-right: 38px; text-align: right; }
.amount-field > span:last-child { position: absolute; top: 50%; right: 8px; color: rgba(238, 229, 209, 0.38); font: 700 0.52rem/1 var(--font-mono, monospace); transform: translateY(-50%); pointer-events: none; }
.remove-button { display: grid; width: 32px; height: 32px; place-items: center; border: 1px solid rgba(193, 104, 91, 0.2); border-radius: 7px; color: #c9867b; background: rgba(151, 65, 52, 0.08); cursor: pointer; }
.remove-button:hover { border-color: rgba(218, 125, 111, 0.5); color: #efafa3; }
.empty-categories { display: grid; min-height: 92px; place-content: center; justify-items: center; gap: 7px; border: 1px dashed rgba(226, 204, 158, 0.13); border-radius: 9px; color: rgba(238, 229, 209, 0.38); font-size: 0.67rem; }

.dialog-footer { display: flex; align-items: center; justify-content: space-between; gap: 20px; padding: 14px 18px; border-top: 1px solid rgba(226, 204, 158, 0.13); background: rgba(7, 9, 7, 0.28); }
.dialog-footer p { max-width: 480px; margin: 0; color: rgba(238, 229, 209, 0.4); font-size: 0.62rem; line-height: 1.45; }
.dialog-footer > div { display: flex; align-items: center; gap: 7px; }
.dialog-footer button { min-height: 36px; padding: 0 12px; border-radius: 7px; cursor: pointer; font-size: 0.65rem; font-weight: 800; }
.secondary-button { border: 1px solid rgba(226, 204, 158, 0.18); color: #d9cfbb; background: rgba(255, 255, 255, 0.035); }
.save-button { display: inline-flex; align-items: center; gap: 6px; border: 1px solid #af9152; color: #201a0f; background: linear-gradient(#dec47f, #b4934f); }
.save-button:disabled { opacity: 0.42; cursor: not-allowed; }
.read-only-status { display: inline-flex; align-items: center; gap: 5px; color: #96c895; font: 800 0.58rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; margin: -1px; padding: 0; border: 0; clip: rect(0, 0, 0, 0); white-space: nowrap; }

.population-dialog-enter-active, .population-dialog-leave-active { transition: opacity 150ms ease; }
.population-dialog-enter-active .population-dialog, .population-dialog-leave-active .population-dialog { transition: transform 170ms ease, opacity 150ms ease; }
.population-dialog-enter-from, .population-dialog-leave-to { opacity: 0; }
.population-dialog-enter-from .population-dialog, .population-dialog-leave-to .population-dialog { opacity: 0; transform: translateY(12px) scale(0.985); }

@media (max-width: 700px) {
  .population-backdrop { align-items: end; padding: 0; }
  .population-dialog { width: 100%; max-height: 94vh; border-right: 0; border-bottom: 0; border-left: 0; border-radius: 16px 16px 0 0; }
  .summary-grid { grid-template-columns: 1fr; }
  .summary-grid > div { min-height: 58px; border-right: 0; border-bottom: 1px solid rgba(226, 204, 158, 0.1); }
  .summary-grid > div:last-child { border-bottom: 0; }
  .population-bars { grid-template-columns: 1fr; gap: 6px; }
  .bar-label { margin-top: 4px; }
  .working-bar { min-height: 92px; }
  .population-segment strong { writing-mode: vertical-rl; transform: rotate(180deg); }
  .population-segment small { display: none; }
  .editable .category-row { grid-template-columns: 8px minmax(0, 1fr) 34px; }
  .editable .category-row .amount-field { grid-column: 2 / 3; }
  .editable .category-row .remove-button { grid-column: 3; grid-row: 1 / 3; }
  .dialog-footer { align-items: stretch; flex-direction: column; }
  .dialog-footer > div { justify-content: flex-end; }
}

@media (prefers-reduced-motion: reduce) {
  .population-dialog-enter-active, .population-dialog-leave-active,
  .population-dialog-enter-active .population-dialog, .population-dialog-leave-active .population-dialog { transition: none; }
}
</style>
