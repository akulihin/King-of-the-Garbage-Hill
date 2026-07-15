<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { Crosshair, Hand, Move } from 'lucide-vue-next'
import type { LastChancesLocale } from './RunMapOverlay.vue'

export type AttackHand = 'primary' | 'secondary'

const props = withDefaults(defineProps<{
  locale: LastChancesLocale
  primaryName: string
  secondaryName: string
  disabled?: boolean
}>(), {
  disabled: false,
})

const emit = defineEmits<{
  move: [x: number, y: number]
  aim: [x: number, y: number]
  press: [hand: AttackHand]
  release: [hand: AttackHand]
}>()

const copy = {
  en: {
    move: 'Move',
    moveHelp: 'Drag the movement stick',
    aim: 'Aim',
    aimHelp: 'Drag to aim',
    primary: 'Primary gesture',
    secondary: 'Secondary gesture',
  },
  ru: {
    move: 'Движение',
    moveHelp: 'Ведите стик движения',
    aim: 'Прицел',
    aimHelp: 'Ведите пальцем для прицеливания',
    primary: 'Основной жест',
    secondary: 'Вторичный жест',
  },
} as const

const t = computed(() => copy[props.locale])
const movePad = ref<HTMLElement | null>(null)
const aimPad = ref<HTMLElement | null>(null)
const movePointer = ref<number | null>(null)
const aimPointer = ref<number | null>(null)
const moveVector = ref({ x: 0, y: 0 })
const aimVector = ref({ x: 0, y: -1 })
const heldHands = new Set<AttackHand>()

function vectorFromPointer(event: PointerEvent, element: HTMLElement, clampToCircle = true) {
  const rect = element.getBoundingClientRect()
  const radius = Math.max(1, Math.min(rect.width, rect.height) / 2)
  let x = (event.clientX - (rect.left + rect.width / 2)) / radius
  let y = (event.clientY - (rect.top + rect.height / 2)) / radius
  const length = Math.hypot(x, y)
  if (clampToCircle && length > 1) {
    x /= length
    y /= length
  }
  return { x, y }
}

function startMove(event: PointerEvent) {
  if (props.disabled || !movePad.value) return
  event.preventDefault()
  movePointer.value = event.pointerId
  movePad.value.setPointerCapture(event.pointerId)
  updateMove(event)
}

function updateMove(event: PointerEvent) {
  if (event.pointerId !== movePointer.value || !movePad.value) return
  moveVector.value = vectorFromPointer(event, movePad.value)
  emit('move', moveVector.value.x, moveVector.value.y)
}

function stopMove(event?: PointerEvent) {
  if (event && event.pointerId !== movePointer.value) return
  movePointer.value = null
  moveVector.value = { x: 0, y: 0 }
  emit('move', 0, 0)
}

function startAim(event: PointerEvent) {
  if (props.disabled || !aimPad.value) return
  event.preventDefault()
  aimPointer.value = event.pointerId
  aimPad.value.setPointerCapture(event.pointerId)
  updateAim(event)
}

function updateAim(event: PointerEvent) {
  if (event.pointerId !== aimPointer.value || !aimPad.value) return
  const vector = vectorFromPointer(event, aimPad.value)
  if (Math.hypot(vector.x, vector.y) < 0.08) return
  aimVector.value = vector
  emit('aim', vector.x, vector.y)
}

function stopAim(event?: PointerEvent) {
  if (event && event.pointerId !== aimPointer.value) return
  aimPointer.value = null
}

function pressHand(event: PointerEvent, hand: AttackHand) {
  if (props.disabled) return
  event.preventDefault()
  const target = event.currentTarget as HTMLElement
  target.setPointerCapture(event.pointerId)
  if (heldHands.has(hand)) return
  heldHands.add(hand)
  emit('press', hand)
}

function releaseHand(hand: AttackHand) {
  if (!heldHands.delete(hand)) return
  emit('release', hand)
}

onBeforeUnmount(() => {
  stopMove()
  stopAim()
  for (const hand of heldHands) emit('release', hand)
  heldHands.clear()
})
</script>

<template>
  <div class="lc-touch-controls" :class="{ 'is-disabled': disabled }" aria-label="Touch controls">
    <div class="lc-touch-cluster lc-move-cluster">
      <span class="lc-touch-caption"><Move :size="13" aria-hidden="true" />{{ t.move }}</span>
      <div
        ref="movePad"
        class="lc-touch-pad lc-move-pad"
        role="slider"
        tabindex="0"
        :aria-label="t.moveHelp"
        aria-valuemin="-1"
        aria-valuemax="1"
        :aria-valuenow="Math.round(moveVector.x * 100) / 100"
        @pointerdown="startMove"
        @pointermove="updateMove"
        @pointerup="stopMove"
        @pointercancel="stopMove"
        @lostpointercapture="stopMove"
      >
        <span class="lc-pad-rings" aria-hidden="true" />
        <span
          class="lc-stick"
          :style="{ transform: `translate(calc(-50% + ${moveVector.x * 28}px), calc(-50% + ${moveVector.y * 28}px))` }"
          aria-hidden="true"
        >
          <Move :size="21" />
        </span>
      </div>
    </div>

    <div class="lc-touch-cluster lc-action-cluster">
      <div
        ref="aimPad"
        class="lc-touch-pad lc-aim-pad"
        role="slider"
        tabindex="0"
        :aria-label="t.aimHelp"
        aria-valuemin="-1"
        aria-valuemax="1"
        :aria-valuenow="Math.round(aimVector.x * 100) / 100"
        @pointerdown="startAim"
        @pointermove="updateAim"
        @pointerup="stopAim"
        @pointercancel="stopAim"
        @lostpointercapture="stopAim"
      >
        <Crosshair :size="23" aria-hidden="true" />
        <span
          class="lc-aim-pip"
          :style="{ transform: `translate(calc(-50% + ${aimVector.x * 25}px), calc(-50% + ${aimVector.y * 25}px))` }"
          aria-hidden="true"
        />
        <span class="sr-only">{{ t.aim }}</span>
      </div>

      <button
        class="lc-gesture-button is-primary"
        type="button"
        :disabled="disabled"
        :aria-label="`${t.primary}: ${primaryName}`"
        @pointerdown="pressHand($event, 'primary')"
        @pointerup="releaseHand('primary')"
        @pointercancel="releaseHand('primary')"
        @lostpointercapture="releaseHand('primary')"
      >
        <Hand :size="20" aria-hidden="true" />
        <span><small>L</small>{{ primaryName }}</span>
      </button>

      <button
        class="lc-gesture-button is-secondary"
        type="button"
        :disabled="disabled"
        :aria-label="`${t.secondary}: ${secondaryName}`"
        @pointerdown="pressHand($event, 'secondary')"
        @pointerup="releaseHand('secondary')"
        @pointercancel="releaseHand('secondary')"
        @lostpointercapture="releaseHand('secondary')"
      >
        <Hand :size="20" aria-hidden="true" />
        <span><small>R</small>{{ secondaryName }}</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.lc-touch-controls {
  position: absolute;
  z-index: 14;
  inset: auto 0 0;
  display: none;
  justify-content: space-between;
  align-items: end;
  gap: 1rem;
  padding: 0.6rem max(0.65rem, env(safe-area-inset-right)) max(0.7rem, env(safe-area-inset-bottom)) max(0.65rem, env(safe-area-inset-left));
  pointer-events: none;
  user-select: none;
}

.lc-touch-cluster {
  display: flex;
  align-items: end;
  gap: 0.45rem;
  pointer-events: auto;
}

.lc-move-cluster { display: grid; justify-items: center; }
.lc-touch-caption { display: inline-flex; align-items: center; gap: 0.3rem; color: rgba(228, 227, 216, 0.6); font-size: 0.58rem; font-weight: 800; letter-spacing: 0.1em; text-transform: uppercase; text-shadow: 0 1px 4px #000; }

.lc-touch-pad {
  position: relative;
  touch-action: none;
  border: 1px solid rgba(220, 215, 199, 0.23);
  border-radius: 50%;
  background: radial-gradient(circle, rgba(24, 28, 28, 0.56), rgba(6, 8, 9, 0.76));
  box-shadow: inset 0 0 1.5rem rgba(0, 0, 0, 0.6), 0 0.5rem 2rem rgba(0, 0, 0, 0.35);
  backdrop-filter: blur(8px);
}

.lc-move-pad { width: 7.2rem; height: 7.2rem; }
.lc-pad-rings { position: absolute; inset: 20%; border: 1px dashed rgba(226, 216, 187, 0.16); border-radius: inherit; }
.lc-pad-rings::before,
.lc-pad-rings::after { content: ''; position: absolute; background: rgba(226, 216, 187, 0.1); }
.lc-pad-rings::before { width: 1px; inset: -32% auto; left: 50%; }
.lc-pad-rings::after { height: 1px; inset: 50% -32% auto; }

.lc-stick {
  position: absolute;
  left: 50%;
  top: 50%;
  width: 3.1rem;
  height: 3.1rem;
  display: grid;
  place-items: center;
  border: 1px solid rgba(218, 194, 137, 0.43);
  border-radius: 50%;
  color: #d6c39a;
  background: linear-gradient(145deg, rgba(58, 52, 41, 0.92), rgba(16, 18, 18, 0.94));
  box-shadow: 0 0.4rem 1rem rgba(0, 0, 0, 0.5);
  will-change: transform;
}

.lc-aim-pad {
  width: 4.3rem;
  height: 4.3rem;
  display: grid;
  place-items: center;
  color: rgba(224, 211, 181, 0.45);
}

.lc-aim-pip { position: absolute; left: 50%; top: 50%; width: 0.42rem; height: 0.42rem; border: 1px solid #d4ad5e; border-radius: 50%; background: #7b5220; box-shadow: 0 0 0.6rem #d4ad5e; }

.lc-gesture-button {
  width: 4.6rem;
  min-height: 5.2rem;
  display: grid;
  place-items: center;
  align-content: center;
  gap: 0.2rem;
  touch-action: none;
  border: 1px solid rgba(255, 255, 255, 0.14);
  border-radius: 50% 50% 46% 46%;
  color: #dfddd5;
  background: linear-gradient(150deg, rgba(53, 58, 57, 0.88), rgba(12, 15, 15, 0.94));
  box-shadow: 0 0.55rem 1.5rem rgba(0, 0, 0, 0.5), inset 0 1px rgba(255, 255, 255, 0.07);
  backdrop-filter: blur(8px);
}

.lc-gesture-button.is-primary { border-color: rgba(203, 161, 73, 0.45); }
.lc-gesture-button.is-secondary { border-color: rgba(151, 71, 78, 0.54); transform: translateY(-1.25rem); }
.lc-gesture-button:active:not(:disabled) { transform: scale(0.91); filter: brightness(1.35); }
.lc-gesture-button.is-secondary:active:not(:disabled) { transform: translateY(-1.25rem) scale(0.91); }
.lc-gesture-button > span { max-width: 4rem; overflow: hidden; font-size: 0.53rem; font-weight: 700; text-overflow: ellipsis; white-space: nowrap; }
.lc-gesture-button small { display: block; color: #777b77; font: 800 0.46rem/1 var(--font-mono, monospace); }
.lc-touch-controls.is-disabled { opacity: 0.45; }

.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

@media (hover: none), (pointer: coarse), (max-width: 760px) {
  .lc-touch-controls { display: flex; }
}

@media (max-width: 430px) {
  .lc-move-pad { width: 6.2rem; height: 6.2rem; }
  .lc-stick { width: 2.7rem; height: 2.7rem; }
  .lc-aim-pad { width: 3.4rem; height: 3.4rem; }
  .lc-gesture-button { width: 3.9rem; min-height: 4.55rem; }
  .lc-action-cluster { gap: 0.3rem; }
}

@media (prefers-reduced-motion: reduce) {
  .lc-stick,
  .lc-gesture-button { transition: none; }
}
</style>
