<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, triggerRef } from 'vue'
import { PackageCheck, Pause, Play, RotateCw, TriangleAlert, X } from 'lucide-vue-next'
import {
  consumeInventoryFrameTime,
  createInventorySimulation,
  inventoryCommandDisabledReason,
  inventoryItemCells,
  replayInventory,
  stepInventorySimulation,
} from '../../features/empires-endgame/inventory/engine'
import { resolveInventoryWithPolicy } from '../../features/empires-endgame/inventory/qa'
import type {
  InventoryCommand,
  InventoryMove,
  InventoryResult,
  InventorySimulationState,
} from '../../features/empires-endgame/inventory/types'
import type { EmpiresInventoryMinigameSession } from '../../features/empires-endgame/types'

const props = defineProps<{
  session: EmpiresInventoryMinigameSession
  qaMode?: boolean
}>()

const emit = defineEmits<{
  resolved: [result: InventoryResult]
  abort: [commandLog: InventoryCommand[], abortTick: number]
}>()

const state = shallowRef<InventorySimulationState>(createInventorySimulation(props.session.plan, props.session.seed))
const paused = ref(true)
const abortOpen = ref(false)
const emitted = ref(false)
let frameHandle = 0
let lastFrame: number | null = null
let accumulatorMs = 0

const activeCells = computed(() => state.value.activeItem
  ? inventoryItemCells(props.session.plan, state.value.activeItem)
  : [])
const cartTop = computed(() => props.session.plan.board.height - props.session.plan.board.cartHeight)
const boardStyle = computed(() => ({
  '--inventory-columns': String(props.session.plan.board.width),
  '--inventory-rows': String(props.session.plan.board.height),
  '--inventory-cart-top': String(cartTop.value + 1),
  '--inventory-aspect': `${props.session.plan.board.width} / ${props.session.plan.board.height}`,
}))
const activeInstance = computed(() => props.session.plan.itemInstances.find(item => (
  item.id === state.value.activeItem?.instanceId
)) ?? null)
const activeDefinition = computed(() => props.session.plan.itemDefinitions.find(item => (
  item.id === state.value.activeItem?.definitionId
)) ?? null)
const nextInstance = computed(() => props.session.plan.itemInstances.find(item => (
  item.id === state.value.queueItemInstanceIds[0]
)) ?? null)
const nextDefinition = computed(() => props.session.plan.itemDefinitions.find(item => (
  item.id === nextInstance.value?.definitionId
)) ?? null)
const packedAmount = computed(() => state.value.placements.reduce((total, placement) => total + placement.amount, 0))
const efficiency = computed(() => props.session.plan.eligibleProvisionAmount > 0
  ? Math.round(packedAmount.value / props.session.plan.eligibleProvisionAmount * 100)
  : 100)
const elapsedSeconds = computed(() => Math.floor(state.value.tick * props.session.plan.tickMs / 1000))

function command(value: Omit<InventoryCommand, 'tick' | 'sequence' | 'sessionId' | 'planId'>) {
  if (paused.value || state.value.terminalReason) return
  const next = {
    tick: state.value.tick,
    sequence: state.value.commandLog.length,
    sessionId: props.session.id,
    planId: props.session.plan.id,
    ...value,
  } as InventoryCommand
  if (inventoryCommandDisabledReason(props.session.plan, state.value, next)) return
  stepInventorySimulation(props.session.plan, state.value, [next])
  triggerRef(state)
  settleIfTerminal()
}

function move(direction: InventoryMove) {
  command({ kind: 'move', direction })
}

function settleIfTerminal() {
  if (!state.value.terminalReason || emitted.value) return
  emitted.value = true
  paused.value = true
  emit('resolved', replayInventory(props.session.plan, props.session.seed, state.value.commandLog))
}

function tick(timestamp: number) {
  if (lastFrame === null) lastFrame = timestamp
  if (!paused.value && !state.value.terminalReason) {
    const clock = consumeInventoryFrameTime(
      accumulatorMs,
      timestamp - lastFrame,
      props.session.plan.tickMs,
      props.session.plan.maxCatchUpTicksPerFrame,
    )
    accumulatorMs = clock.accumulatorMs
    for (let index = 0; index < clock.ticks && !state.value.terminalReason; index += 1) {
      stepInventorySimulation(props.session.plan, state.value)
    }
    if (clock.ticks > 0) triggerRef(state)
    settleIfTerminal()
  }
  lastFrame = timestamp
  frameHandle = requestAnimationFrame(tick)
}

function togglePause() {
  paused.value = !paused.value
  lastFrame = null
}

function onKeydown(event: KeyboardEvent) {
  if (event.target instanceof HTMLInputElement || event.target instanceof HTMLSelectElement) return
  if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
    event.preventDefault()
    move(event.key === 'ArrowLeft' ? 'left' : 'right')
  } else if (event.code === 'Space') {
    event.preventDefault()
    command({ kind: 'rotate' })
  } else if (event.key === 'ArrowDown' || event.key === 'Enter') {
    event.preventDefault()
    command({ kind: 'place' })
  } else if (event.key.toLowerCase() === 'p') {
    togglePause()
  } else if (event.key === 'Escape') {
    abortOpen.value = true
  }
}

function confirmAbort() {
  paused.value = true
  abortOpen.value = false
  emit('abort', structuredClone(state.value.commandLog), state.value.tick)
}

function qaResolve() {
  if (!props.qaMode || emitted.value) return
  emitted.value = true
  paused.value = true
  emit('resolved', resolveInventoryWithPolicy(props.session.plan, props.session.seed, 'spread'))
}

function onVisibilityChange() {
  if (document.hidden) paused.value = true
  lastFrame = null
}

onMounted(async () => {
  await nextTick()
  window.addEventListener('keydown', onKeydown)
  document.addEventListener('visibilitychange', onVisibilityChange)
  frameHandle = requestAnimationFrame(tick)
})

onUnmounted(() => {
  cancelAnimationFrame(frameHandle)
  window.removeEventListener('keydown', onKeydown)
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>

<template>
  <section class="inventory" data-testid="inventory-packing" aria-labelledby="inventory-title">
    <header class="inventory__header">
      <div>
        <span>Экспедиция · упаковка тележки</span>
        <h2 id="inventory-title"><PackageCheck :size="25" /> Провизия в дорогу</h2>
        <p>В тележку уедут только уложенные вещи. Остальное останется в исходном регионе.</p>
      </div>
      <dl aria-live="polite">
        <div><dt>Счёт</dt><dd data-testid="inventory-score">{{ state.score }}</dd></div>
        <div><dt>Эффективность</dt><dd>{{ efficiency }}%</dd></div>
        <div><dt>Провизия</dt><dd>{{ packedAmount }}/{{ session.plan.eligibleProvisionAmount }}</dd></div>
        <div><dt>Время</dt><dd>{{ elapsedSeconds }} сек.</dd></div>
      </dl>
    </header>

    <div v-if="state.terminalReason === 'cart-overflow'" class="inventory__warning" role="alert">
      <TriangleAlert :size="20" /> Тележка переполнена: уже уложенные вещи сохраняют результат, остальные останутся дома.
    </div>

    <div class="inventory__layout">
      <div
        class="inventory__board"
        :style="boardStyle"
        role="img"
        :aria-label="`Поле ${session.plan.board.width} на ${session.plan.board.height}; тележка занимает нижние ${session.plan.board.cartHeight} рядов; уложено ${state.placements.length} вещей`"
      >
        <i class="cart-zone" aria-hidden="true" />
        <template v-for="placement in state.placements" :key="placement.instanceId">
          <i
            v-for="cell in placement.cells"
            :key="`${placement.instanceId}:${cell.x}:${cell.y}`"
            class="cell cell--packed"
            :style="{ '--cell-x': cell.x + 1, '--cell-y': cell.y + 1 }"
          />
        </template>
        <i
          v-for="cell in activeCells"
          :key="`active:${cell.x}:${cell.y}`"
          class="cell cell--active"
          :style="{ '--cell-x': cell.x + 1, '--cell-y': cell.y + 1 }"
        />
      </div>

      <aside class="inventory__controls" aria-label="Управление упаковкой">
        <div class="item-card">
          <small>Падает сейчас</small>
          <strong>{{ activeDefinition?.name ?? 'ожидание' }}</strong>
          <span v-if="activeInstance">{{ activeInstance.amount }} провизии · вес {{ activeDefinition?.weight }}</span>
        </div>
        <div class="item-card item-card--next">
          <small>Следующая вещь</small>
          <strong>{{ nextDefinition?.name ?? 'нет' }}</strong>
          <span v-if="nextInstance">{{ nextInstance.amount }} провизии</span>
        </div>

        <button type="button" data-testid="inventory-pause" @click="togglePause">
          <Play v-if="paused" :size="17" /><Pause v-else :size="17" /> {{ paused ? 'Начать' : 'Пауза' }} (P)
        </button>
        <div class="inventory__buttons">
          <button type="button" aria-label="Сдвинуть влево" data-testid="inventory-left" @click="move('left')">←</button>
          <button type="button" aria-label="Повернуть вещь" data-testid="inventory-rotate" @click="command({ kind: 'rotate' })"><RotateCw :size="18" /></button>
          <button type="button" aria-label="Сдвинуть вправо" data-testid="inventory-right" @click="move('right')">→</button>
        </div>
        <button type="button" data-testid="inventory-place" @click="command({ kind: 'place' })">Уложить в тележку (Enter/↓)</button>
        <small>←/→ — сдвиг · Space — поворот · Enter или ↓ — мгновенно уложить.</small>

        <button v-if="qaMode" type="button" data-testid="inventory-fast-resolve" @click="qaResolve">QA: быстро упаковать</button>
        <button class="danger" type="button" data-testid="inventory-abort" @click="abortOpen = true"><X :size="16" /> Прервать экспедицию (Esc)</button>
      </aside>
    </div>

    <details class="inventory__accessible-state">
      <summary>Текстовое состояние тележки</summary>
      <p>Тик {{ state.tick }}; уложено {{ state.placements.length }}; осталось {{ state.queueItemInstanceIds.length + (state.activeItem ? 1 : 0) }}.</p>
      <ul>
        <li v-for="placement in state.placements" :key="placement.instanceId">
          {{ placement.instanceId }}: {{ placement.amount }} провизии, координаты {{ placement.cells.map(cell => `${cell.x}:${cell.y}`).join(', ') }}.
        </li>
      </ul>
    </details>

    <div v-if="abortOpen" class="inventory__dialog" role="dialog" aria-modal="true" aria-labelledby="inventory-abort-title">
      <div>
        <h3 id="inventory-abort-title">Прервать упаковку и экспедицию?</h3>
        <p>Подготовительные дни уже потрачены. Провизия не уйдёт, но эта попытка будет записана как прерванная.</p>
        <button type="button" data-testid="inventory-confirm-abort" @click="confirmAbort">Прервать экспедицию</button>
        <button type="button" @click="abortOpen = false">Продолжить упаковку</button>
      </div>
    </div>
  </section>
</template>

<style scoped>
.inventory { display:grid; gap:14px; width:min(1080px,100%); margin:0 auto; padding:18px; color:#f5ead1; }
.inventory__header { display:flex; flex-wrap:wrap; justify-content:space-between; gap:14px; padding:16px; border:1px solid rgba(211,170,88,.38); background:rgba(31,24,13,.94); }
.inventory__header span { color:#e1b85e; font:700 .65rem/1.3 var(--font-mono,monospace); letter-spacing:.13em; text-transform:uppercase; }
.inventory__header h2 { display:flex; align-items:center; gap:9px; margin:5px 0; }.inventory__header p { max-width:620px; margin:0; color:rgba(245,234,209,.68); }
.inventory__header dl { display:grid; grid-template-columns:repeat(2,minmax(95px,1fr)); gap:6px; margin:0; }.inventory__header dl div { padding:7px 10px; background:rgba(0,0,0,.22); }.inventory__header dt { color:#aa9670; font-size:.58rem; }.inventory__header dd { margin:3px 0 0; color:#f0c96e; font:800 .9rem/1 var(--font-mono,monospace); }
.inventory__warning { display:flex; align-items:center; gap:8px; padding:11px 14px; border:1px solid #e27350; background:rgba(93,31,18,.8); }
.inventory__layout { display:grid; grid-template-columns:minmax(300px,1fr) minmax(240px,320px); gap:14px; align-items:start; }
.inventory__board { --size:min(64vw,620px); position:relative; display:grid; grid-template-columns:repeat(var(--inventory-columns),1fr); grid-template-rows:repeat(var(--inventory-rows),1fr); width:var(--size); max-width:100%; aspect-ratio:var(--inventory-aspect); overflow:hidden; border:2px solid #8e7035; background:linear-gradient(rgba(232,197,123,.07) 1px,transparent 1px),linear-gradient(90deg,rgba(232,197,123,.07) 1px,transparent 1px),#15120c; background-size:calc(100%/var(--inventory-columns)) calc(100%/var(--inventory-rows)); }
.cart-zone { z-index:0; grid-column:1/-1; grid-row:var(--inventory-cart-top)/-1; border:3px solid #9f7840; border-top-color:#d1a34d; background:linear-gradient(90deg,rgba(131,86,35,.24),rgba(205,143,53,.12)); box-shadow:inset 0 0 30px rgba(225,161,63,.12); }
.cell { z-index:2; grid-column:var(--cell-x); grid-row:var(--cell-y); min-width:0; min-height:0; border:1px solid rgba(31,20,7,.72); box-shadow:inset 0 0 5px rgba(255,255,255,.22); }.cell--packed { background:#a66d2c; }.cell--active { z-index:3; outline:1px solid #fff0b0; outline-offset:-2px; background:#dbac4e; }
.inventory__controls { display:grid; gap:9px; padding:14px; border:1px solid rgba(211,170,88,.3); background:rgba(28,22,13,.94); }.item-card { display:grid; gap:3px; padding:10px; border:1px solid rgba(232,196,119,.16); background:rgba(0,0,0,.18); }.item-card small { color:#b7a179; }.item-card strong { color:#f1cc78; }.item-card span { font-size:.65rem; color:rgba(245,234,209,.62); }.item-card--next { opacity:.72; }
.inventory button { min-height:39px; padding:7px 10px; border:1px solid rgba(224,184,99,.5); color:inherit; background:rgba(112,74,27,.72); cursor:pointer; }.inventory button:focus-visible { outline:2px solid #fff0a8; outline-offset:2px; }.inventory button:disabled { opacity:.45; cursor:not-allowed; }
.inventory__buttons { display:grid; grid-template-columns:repeat(3,1fr); gap:5px; }.inventory__buttons button { display:grid; place-items:center; font-size:1.15rem; }.inventory__controls > small { color:#ad9b78; line-height:1.4; }.danger { border-color:rgba(238,97,73,.65)!important; background:rgba(105,29,21,.65)!important; }
.inventory__accessible-state { padding:10px 13px; border:1px solid rgba(211,170,88,.22); }.inventory__accessible-state ul { max-height:130px; overflow:auto; }
.inventory__dialog { position:fixed; inset:0; z-index:90; display:grid; place-items:center; padding:20px; background:rgba(0,0,0,.74); }.inventory__dialog>div { display:grid; gap:10px; width:min(450px,100%); padding:22px; border:1px solid #d6725d; background:#241b10; }.inventory__dialog h3,.inventory__dialog p { margin:0; }
@media(max-width:800px){.inventory__layout{grid-template-columns:1fr}.inventory__board{--size:min(90vw,560px)}.inventory{padding:9px}}
</style>
