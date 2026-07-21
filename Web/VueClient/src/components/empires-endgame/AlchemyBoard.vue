<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, triggerRef } from 'vue'
import { FlaskConical, Pause, Play, RotateCw, TriangleAlert, X } from 'lucide-vue-next'
import {
  alchemyCommandDisabledReason,
  alchemyPieceCells,
  consumeAlchemyFrameTime,
  createAlchemySimulation,
  replayAlchemy,
  stepAlchemySimulation,
} from '../../features/empires-endgame/alchemy/engine'
import {
  resolveAlchemyExplosionFixture,
  resolveAlchemyWithPolicy,
} from '../../features/empires-endgame/alchemy/qa'
import type {
  AlchemyColor,
  AlchemyCommand,
  AlchemyMove,
  AlchemyResult,
  AlchemySimulationState,
} from '../../features/empires-endgame/alchemy/types'
import type { EmpiresAlchemyMinigameSession } from '../../features/empires-endgame/types'
import MinigameAbortDialog from './MinigameAbortDialog.vue'

const props = defineProps<{
  session: EmpiresAlchemyMinigameSession
  qaMode?: boolean
}>()

const emit = defineEmits<{
  resolved: [result: AlchemyResult]
  abort: [commandLog: AlchemyCommand[], abortTick: number]
}>()

const state = shallowRef<AlchemySimulationState>(createAlchemySimulation(props.session.plan, props.session.seed))
const paused = ref(true)
const reagentOpen = ref(false)
const abortOpen = ref(false)
const emitted = ref(false)
let frameHandle = 0
let lastFrame: number | null = null
let accumulatorMs = 0
let pausedBeforeAbort = true

const controlled = computed(() => state.value.activePieces.find(piece => piece.id === state.value.controlledPieceId) ?? null)
const activeCells = computed(() => state.value.activePieces.flatMap(piece => (
  alchemyPieceCells(props.session.plan, piece).map(cell => ({ ...cell, pieceId: piece.id }))
)))
const targetKeys = computed(() => new Set(props.session.plan.recipe.targetCells.map(cell => `${cell.x}:${cell.y}`)))
const boardStyle = computed(() => ({
  '--alchemy-columns': String(props.session.plan.board.width),
  '--alchemy-rows': String(props.session.plan.board.height),
}))
const progress = computed(() => {
  const occupied = new Set(state.value.construction.map(cell => `${cell.x}:${cell.y}`))
  const matched = props.session.plan.recipe.targetCells.filter(cell => occupied.has(`${cell.x}:${cell.y}`)).length
  return { matched, total: props.session.plan.recipe.targetCells.length }
})
const warning = computed(() => state.value.speedPercent >= props.session.plan.acceleration.explosionThresholdPercent * .85)

function command(value: Omit<AlchemyCommand, 'tick' | 'sequence' | 'sessionId' | 'planId'>) {
  if (paused.value || abortOpen.value || state.value.terminalReason) return
  const next = {
    tick: state.value.tick,
    sequence: state.value.commandLog.length,
    sessionId: props.session.id,
    planId: props.session.plan.id,
    ...value,
  } as AlchemyCommand
  const reason = alchemyCommandDisabledReason(props.session.plan, state.value, next)
  if (reason) return
  stepAlchemySimulation(props.session.plan, state.value, [next])
  triggerRef(state)
  settleIfTerminal()
}

function move(direction: AlchemyMove) {
  command({ kind: 'move', direction })
}

function removeColor(color: Exclude<AlchemyColor, 'gray'>) {
  command({ kind: 'remove-color', color })
}

function settleIfTerminal() {
  if (!state.value.terminalReason || emitted.value) return
  emitted.value = true
  paused.value = true
  emit('resolved', replayAlchemy(props.session.plan, props.session.seed, state.value.commandLog))
}

function tick(timestamp: number) {
  if (lastFrame === null) lastFrame = timestamp
  if (!paused.value && !state.value.terminalReason) {
    const clock = consumeAlchemyFrameTime(
      accumulatorMs,
      timestamp - lastFrame,
      props.session.plan.tickMs,
      props.session.plan.maxCatchUpTicksPerFrame,
    )
    accumulatorMs = clock.accumulatorMs
    for (let index = 0; index < clock.ticks && !state.value.terminalReason; index += 1) {
      stepAlchemySimulation(props.session.plan, state.value)
    }
    if (clock.ticks > 0) triggerRef(state)
    settleIfTerminal()
  }
  lastFrame = timestamp
  frameHandle = requestAnimationFrame(tick)
}

function resetFrameClock() {
  lastFrame = null
  accumulatorMs = 0
}

function togglePause() {
  if (abortOpen.value) return
  paused.value = !paused.value
  resetFrameClock()
}

function toggleReagents() {
  if (abortOpen.value) return
  reagentOpen.value = !reagentOpen.value
  if (reagentOpen.value) paused.value = true
  resetFrameClock()
}

function isInteractiveTarget(target: EventTarget | null): boolean {
  return target instanceof Element && Boolean(target.closest(
    'button, input, select, textarea, a[href], summary, [contenteditable]:not([contenteditable="false"]), [role="button"], [role="link"]',
  ))
}

function onKeydown(event: KeyboardEvent) {
  if (event.defaultPrevented || abortOpen.value) return
  if (event.ctrlKey || event.metaKey || event.altKey) return
  if (event.key === 'Escape') {
    event.preventDefault()
    openAbort()
    return
  }
  if (isInteractiveTarget(event.target)) return
  const directions: Partial<Record<string, AlchemyMove>> = {
    ArrowUp: 'up', ArrowRight: 'right', ArrowDown: 'down', ArrowLeft: 'left',
  }
  if (directions[event.key]) {
    event.preventDefault()
    move(directions[event.key]!)
  } else if (event.code === 'Space') {
    event.preventDefault()
    command({ kind: 'rotate' })
  } else if (event.key === 'Enter' && !event.repeat) {
    toggleReagents()
  } else if (event.key.toLowerCase() === 'p' && !event.repeat) {
    togglePause()
  }
}

function openAbort() {
  if (abortOpen.value || emitted.value) return
  pausedBeforeAbort = paused.value
  paused.value = true
  resetFrameClock()
  abortOpen.value = true
}

function cancelAbort() {
  abortOpen.value = false
  paused.value = pausedBeforeAbort
  resetFrameClock()
}

function confirmAbort() {
  paused.value = true
  resetFrameClock()
  abortOpen.value = false
  emit('abort', structuredClone(state.value.commandLog), state.value.tick)
}

function qaResolve(outcome: 'success' | 'explosion') {
  if (!props.qaMode || emitted.value || abortOpen.value) return
  emitted.value = true
  paused.value = true
  emit('resolved', outcome === 'explosion'
    ? resolveAlchemyExplosionFixture(props.session.plan, props.session.seed)
    : resolveAlchemyWithPolicy(props.session.plan, props.session.seed, 'greedy'))
}

function onVisibilityChange() {
  if (document.hidden) paused.value = true
  resetFrameClock()
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
  <section class="alchemy" data-testid="alchemy-board" aria-labelledby="alchemy-title">
    <header class="alchemy__header">
      <div>
        <span>{{ session.plan.recipe.mode === 'assembly' ? 'Сбор' : 'Разбор' }} · лаборатория</span>
        <h2 id="alchemy-title"><FlaskConical :size="24" /> {{ session.plan.recipe.name }}</h2>
        <p>{{ session.plan.recipe.description }}</p>
      </div>
      <div class="alchemy__metrics" aria-live="polite">
        <b data-testid="alchemy-speed">{{ state.speedPercent }}%</b>
        <small>ускорение · взрыв {{ session.plan.acceleration.explosionBoundary === 'above' ? '>' : '≥' }} {{ session.plan.acceleration.explosionThresholdPercent }}%</small>
        <strong>{{ progress.matched }}/{{ progress.total }} ячеек цели</strong>
      </div>
    </header>

    <div v-if="warning" class="alchemy__warning" role="alert" data-testid="alchemy-explosion-warning">
      <TriangleAlert :size="20" /> Критическое ускорение: взрыв запустит эпидемию у лаборатории.
    </div>

    <div class="alchemy__layout">
      <div
        class="alchemy__board"
        :style="boardStyle"
        role="img"
        tabindex="0"
        data-testid="alchemy-keyboard-surface"
        aria-describedby="alchemy-keyboard-help"
        :aria-label="`Поле ${session.plan.board.width} на ${session.plan.board.height}; активных фигур ${state.activePieces.length}; построено ячеек ${state.construction.length}`"
      >
        <i
          v-for="target in session.plan.recipe.targetCells"
          :key="`target:${target.x}:${target.y}`"
          class="cell cell--target"
          :style="{ '--cell-x': target.x + 1, '--cell-y': target.y + 1 }"
        />
        <i
          v-for="cell in state.construction"
          :key="`built:${cell.x}:${cell.y}`"
          class="cell cell--built"
          :class="`cell--${cell.color}`"
          :data-target="targetKeys.has(`${cell.x}:${cell.y}`)"
          :style="{ '--cell-x': cell.x + 1, '--cell-y': cell.y + 1 }"
        />
        <i
          v-for="cell in activeCells"
          :key="`active:${cell.pieceId}:${cell.x}:${cell.y}`"
          class="cell cell--active"
          :class="[`cell--${cell.color}`, { 'cell--controlled': cell.pieceId === state.controlledPieceId }]"
          :style="{ '--cell-x': cell.x + 1, '--cell-y': cell.y + 1 }"
        />
      </div>

      <aside class="alchemy__controls" aria-label="Управление Алхимией">
        <p>
          Ближайшая фигура: <b>{{ controlled?.id ?? 'ожидание' }}</b>
          <small v-if="controlled">сторона {{ controlled.side }} · цвет {{ controlled.color }}</small>
        </p>
        <button type="button" data-testid="alchemy-pause" @click="togglePause">
          <Play v-if="paused" :size="17" /><Pause v-else :size="17" /> {{ paused ? 'Продолжить' : 'Пауза' }} (P)
        </button>
        <div class="direction-pad">
          <button type="button" aria-label="Вверх" data-testid="alchemy-move-up" @click="move('up')">↑</button>
          <button type="button" aria-label="Влево" @click="move('left')">←</button>
          <button type="button" aria-label="Повернуть" data-testid="alchemy-rotate" @click="command({ kind: 'rotate' })"><RotateCw :size="17" /></button>
          <button type="button" aria-label="Вправо" @click="move('right')">→</button>
          <button type="button" aria-label="Вниз" @click="move('down')">↓</button>
        </div>
        <small id="alchemy-keyboard-help">Стрелки — движение; внутрь ×{{ session.plan.spawn.inwardSpeedMultiplier }}. Space — поворот. Назад к краю нельзя.</small>

        <button type="button" data-testid="alchemy-reagents" @click="toggleReagents">Реагенты (Enter)</button>
        <div v-if="reagentOpen" class="reagents" data-testid="alchemy-reagent-panel">
          <strong>Удалить цвет · {{ state.reagentCharges.removeColor }}</strong>
          <div>
            <button v-for="color in session.plan.colors" :key="color" type="button" :aria-label="`Удалить ${color}`" :class="`reagent--${color}`" @click="removeColor(color)">{{ color }}</button>
          </div>
          <button type="button" :disabled="!controlled || state.reagentCharges.addGray <= 0" @click="controlled && command({ kind: 'add-gray', pieceId: controlled.id })">
            Сделать фигуру серой · {{ state.reagentCharges.addGray }}
          </button>
          <button type="button" :disabled="state.reagentCharges.resetAcceleration <= 0" @click="command({ kind: 'reset-acceleration' })">
            Сбросить ускорение · {{ state.reagentCharges.resetAcceleration }}
          </button>
        </div>

        <div v-if="qaMode" class="qa-actions" aria-label="QA действия">
          <button type="button" data-testid="alchemy-qa-success" @click="qaResolve('success')">QA: до результата</button>
          <button type="button" data-testid="alchemy-qa-explosion" @click="qaResolve('explosion')">QA: взрыв</button>
        </div>
        <button class="danger" type="button" data-testid="alchemy-abort" @click="openAbort"><X :size="16" /> Прервать опыт (Esc)</button>
      </aside>
    </div>

    <details class="alchemy__accessible-state" data-testid="alchemy-text-state">
      <summary>Текстовое состояние поля</summary>
      <p>Тик {{ state.tick }}; фигур установлено {{ state.settledPieces }}; активных {{ state.activePieces.length }}.</p>
      <ul>
        <li v-for="piece in state.activePieces" :key="piece.id">
          {{ piece.id }}: {{ piece.side }}, {{ piece.color }}, координата {{ piece.anchor.x }}:{{ piece.anchor.y }}, поворот {{ piece.rotation }}.
        </li>
      </ul>
    </details>

    <MinigameAbortDialog
      v-if="abortOpen"
      id-prefix="alchemy-abort"
      title="Прервать лабораторную сессию?"
      description="Потраченные на опыт дни не возвращаются; награды не применяются."
      confirm-label="Прервать"
      continue-label="Продолжить опыт"
      confirm-test-id="alchemy-confirm-abort"
      continue-test-id="alchemy-continue"
      @confirm="confirmAbort"
      @cancel="cancelAbort"
    />
  </section>
</template>

<style scoped>
.alchemy { display: grid; gap: 14px; width: min(1120px, 100%); margin: 0 auto; padding: 18px; color: #edf5e6; }
.alchemy__header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 14px; padding: 16px; border: 1px solid rgba(119, 210, 157, .35); background: rgba(9, 31, 26, .92); }
.alchemy__header span { color: #8ed7af; font: 700 .65rem/1.3 var(--font-mono, monospace); text-transform: uppercase; letter-spacing: .13em; }
.alchemy__header h2 { display: flex; align-items: center; gap: 9px; margin: 5px 0; }
.alchemy__header p { max-width: 650px; margin: 0; color: rgba(237, 245, 230, .72); }
.alchemy__metrics { display: grid; align-content: center; min-width: 190px; text-align: right; }
.alchemy__metrics b { color: #bdfc75; font: 800 2rem/1 var(--font-mono, monospace); }
.alchemy__metrics small { color: #9fb5a8; }
.alchemy__warning { display: flex; align-items: center; gap: 8px; padding: 11px 14px; border: 1px solid #f06c4f; background: rgba(104, 26, 19, .78); }
.alchemy__layout { display: grid; grid-template-columns: minmax(280px, 1fr) minmax(230px, 310px); gap: 14px; align-items: start; }
.alchemy__board { --size: min(72vw, 680px); position: relative; display: grid; grid-template-columns: repeat(var(--alchemy-columns), 1fr); grid-template-rows: repeat(var(--alchemy-rows), 1fr); width: var(--size); max-width: 100%; aspect-ratio: 1; overflow: hidden; border: 2px solid #5f9e7d; background: linear-gradient(rgba(110, 190, 150, .06) 1px, transparent 1px), linear-gradient(90deg, rgba(110, 190, 150, .06) 1px, transparent 1px), #071713; background-size: calc(100% / var(--alchemy-columns)) calc(100% / var(--alchemy-rows)); }
.cell { grid-column: var(--cell-x); grid-row: var(--cell-y); min-width: 0; min-height: 0; }
.cell--target { border: 1px dashed rgba(224, 255, 217, .5); background: rgba(173, 228, 177, .09); }
.cell--built, .cell--active { z-index: 2; border: 1px solid rgba(0, 0, 0, .55); box-shadow: inset 0 0 4px rgba(255, 255, 255, .28); }
.cell--active { z-index: 3; opacity: .78; }
.cell--controlled { outline: 1px solid #fff; outline-offset: -1px; filter: brightness(1.35); }
.cell--red, .reagent--red { background: #d94b4b; }.cell--yellow, .reagent--yellow { background: #e8c84d; color: #19170a; }.cell--blue, .reagent--blue { background: #4788de; }.cell--green, .reagent--green { background: #48a868; }.cell--gray { background: #9ca5a3; }
.alchemy__controls { display: grid; gap: 9px; padding: 14px; border: 1px solid rgba(119, 210, 157, .28); background: rgba(8, 28, 23, .92); }
.alchemy__controls p { display: grid; margin: 0; }.alchemy__controls p small { color: #a2b8ac; }
.alchemy button { min-height: 39px; border: 1px solid rgba(126, 220, 166, .42); background: rgba(26, 84, 61, .72); color: inherit; padding: 7px 10px; cursor: pointer; }
.alchemy button:focus-visible { outline: 2px solid #c9ff82; outline-offset: 2px; }.alchemy button:disabled { opacity: .45; cursor: not-allowed; }
.direction-pad { display: grid; grid-template-columns: repeat(3, 1fr); gap: 5px; }.direction-pad button:first-child { grid-column: 2; }.direction-pad button:last-child { grid-column: 2; }.direction-pad button { display: grid; place-items: center; font-size: 1.1rem; }
.reagents, .qa-actions { display: grid; gap: 7px; padding: 9px; border: 1px solid rgba(255,255,255,.1); }.reagents > div { display: grid; grid-template-columns: repeat(2, 1fr); gap: 5px; }.reagents > div button { text-transform: capitalize; }
.danger { border-color: rgba(238, 97, 73, .65) !important; background: rgba(105, 29, 21, .65) !important; }.alchemy__accessible-state { padding: 10px 13px; border: 1px solid rgba(119, 210, 157, .2); }.alchemy__accessible-state ul { max-height: 130px; overflow: auto; }
@media (max-width: 820px) { .alchemy__layout { grid-template-columns: 1fr; }.alchemy__board { --size: min(92vw, 620px); }.alchemy__metrics { text-align: left; }.alchemy { padding: 9px; } }
</style>
