<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'

const store = useBattleshipStore()

const phase = computed(() => store.phase)
const isMyTurn = computed(() => store.isMyTurn)

// ── Turn transition sweep ───────────────────────────────
const turnTransitionActive = ref(false)

watch(isMyTurn, (val) => {
  if (val && (phase.value === 'Combat' || phase.value === 'Boarding')) {
    turnTransitionActive.value = true
    setTimeout(() => { turnTransitionActive.value = false }, 800)
  }
})

// ── Boarding cinematic ──────────────────────────────────
const boardingCinematicActive = ref(false)

watch(phase, (val) => {
  if (val === 'Boarding') {
    boardingCinematicActive.value = true
    setTimeout(() => { boardingCinematicActive.value = false }, 1200)
  }
})

defineExpose({ boardingCinematicActive })

// ── Kill streak ─────────────────────────────────────────
const killStreakLabel = computed(() => {
  const k = store.killStreakDisplay
  if (k < 2) return null
  if (k === 2) return 'Двойное попадание!'
  if (k === 3) return 'Тройное попадание!'
  if (k === 4) return 'ЧЕТВЁРКА!'
  return `${k}x КОМБО!`
})

const phaseOverlayText = computed(() => {
  return phase.value === 'Combat' ? 'К бою!'
    : phase.value === 'Boarding' ? 'Абордаж!'
    : phase.value === 'ShipPlacement' ? 'Расстановка'
    : phase.value === 'GameOver' ? 'Конец игры'
    : phase.value
})
</script>

<template>
  <!-- Kill Streak Popup -->
  <Transition name="streak-pop">
    <div v-if="killStreakLabel" class="kill-streak" :key="store.killStreakDisplay"
      :class="{ 'streak-2x': store.killStreakDisplay >= 2, 'streak-3x': store.killStreakDisplay >= 3, 'streak-4x': store.killStreakDisplay >= 4 }">
      {{ killStreakLabel }}
    </div>
  </Transition>

  <!-- Kill Streak Screen Flash -->
  <Transition name="streak-flash">
    <div v-if="store.killStreakDisplay >= 4" class="streak-screen-flash" :key="'flash-' + store.killStreakDisplay" />
  </Transition>

  <!-- Phase Transition Overlay -->
  <Transition name="phase-transition">
    <div v-if="store.phaseTransitionActive" class="phase-overlay" :key="phase">
      <div class="phase-overlay-text">{{ phaseOverlayText }}</div>
    </div>
  </Transition>

  <!-- Turn Transition Sweep -->
  <Transition name="turn-sweep">
    <div v-if="turnTransitionActive" class="turn-sweep-overlay" :key="'turn-' + store.turnNumber">
      <div class="turn-sweep-band"></div>
      <div class="turn-sweep-text">ВАШ ХОД!</div>
    </div>
  </Transition>

  <!-- Boarding Cinematic -->
  <Transition name="boarding-cine">
    <div v-if="boardingCinematicActive" class="boarding-cine-overlay">
      <div class="boarding-cine-slash"></div>
      <div class="boarding-cine-text">АБОРДАЖ!</div>
    </div>
  </Transition>
</template>

<style scoped>
/* ═══════ Kill streak ═══════ */
.kill-streak {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 50;
  font-size: 2rem;
  font-weight: 900;
  color: var(--accent-gold);
  text-shadow: 0 0 20px color-mix(in srgb, var(--accent-gold) 60%, transparent), 0 2px 8px rgba(0, 0, 0, 0.5);
  pointer-events: none;
  animation: streak-appear 0.6s ease-out forwards;
  white-space: nowrap;
}
@keyframes streak-appear {
  0% { transform: translate(-50%, -50%) scale(0.3); opacity: 0; }
  40% { transform: translate(-50%, -50%) scale(1.2); opacity: 1; }
  100% { transform: translate(-50%, -50%) scale(1); opacity: 0.9; }
}
.streak-pop-enter-active { animation: streak-appear 0.6s ease-out; }
.streak-pop-leave-active { transition: opacity 0.3s ease-out; }
.streak-pop-leave-to { opacity: 0; }
.streak-2x { font-size: 2rem; }
.streak-3x { font-size: 2.5rem; color: var(--accent-orange); text-shadow: 0 0 25px color-mix(in srgb, var(--accent-orange) 60%, transparent), 0 2px 8px rgba(0, 0, 0, 0.5); }
.streak-4x { font-size: 3rem; color: var(--accent-red); text-shadow: 0 0 30px color-mix(in srgb, var(--accent-red) 80%, transparent), 0 0 60px color-mix(in srgb, var(--accent-red) 40%, transparent), 0 2px 8px rgba(0, 0, 0, 0.5); }

.streak-screen-flash {
  position: fixed;
  inset: 0;
  background: rgba(255, 255, 255, 0.15);
  pointer-events: none;
  z-index: 190;
}
.streak-flash-enter-active { transition: opacity 50ms ease-out; }
.streak-flash-leave-active { transition: opacity 100ms ease-out; }
.streak-flash-enter-from, .streak-flash-leave-to { opacity: 0; }

/* ═══════ Phase transition overlay ═══════ */
.phase-overlay {
  position: fixed;
  inset: 0;
  z-index: 200;
  display: flex;
  align-items: center;
  justify-content: center;
  background: color-mix(in srgb, var(--bg-primary) 90%, transparent);
  pointer-events: none;
}
.phase-overlay-text {
  font-size: 3rem;
  font-weight: 900;
  color: var(--accent-gold);
  text-shadow: 0 0 40px color-mix(in srgb, var(--accent-gold) 50%, transparent);
  animation: phase-text-zoom 1.2s ease-out forwards;
}
@keyframes phase-text-zoom {
  0% { transform: scale(0.3); opacity: 0; }
  30% { transform: scale(1.15); opacity: 1; }
  60% { transform: scale(1); }
  100% { transform: scale(1); opacity: 0; }
}
.phase-transition-enter-active { animation: phase-overlay-in 0.4s ease-out; }
.phase-transition-leave-active { animation: phase-overlay-out 0.8s ease-in forwards; }
@keyframes phase-overlay-in { from { opacity: 0; } to { opacity: 1; } }
@keyframes phase-overlay-out { from { opacity: 1; } to { opacity: 0; } }

/* ═══════ Turn transition sweep ═══════ */
.turn-sweep-overlay {
  position: fixed;
  inset: 0;
  z-index: 180;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
  overflow: hidden;
}
.turn-sweep-band {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent, color-mix(in srgb, var(--accent-gold) 14%, transparent), transparent);
  animation: sweep-slide 600ms ease-out forwards;
}
@keyframes sweep-slide {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}
.turn-sweep-text {
  font-size: 2rem;
  font-weight: 900;
  color: var(--accent-gold);
  text-shadow: 0 0 20px color-mix(in srgb, var(--accent-gold) 50%, transparent);
  animation: sweep-text 800ms ease-out forwards;
}
@keyframes sweep-text {
  0% { filter: blur(8px); opacity: 0; }
  30% { filter: blur(0); opacity: 1; }
  70% { opacity: 1; }
  100% { opacity: 0; }
}
.turn-sweep-enter-active { animation: phase-overlay-in 0.2s ease-out; }
.turn-sweep-leave-active { transition: opacity 0.3s ease-out; }
.turn-sweep-leave-to { opacity: 0; }

/* ═══════ Boarding cinematic ═══════ */
.boarding-cine-overlay {
  position: fixed;
  inset: 0;
  z-index: 180;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
  overflow: hidden;
}
.boarding-cine-slash {
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, transparent 40%, color-mix(in srgb, var(--accent-red) 30%, transparent) 48%, color-mix(in srgb, var(--accent-red) 50%, transparent) 50%, color-mix(in srgb, var(--accent-red) 30%, transparent) 52%, transparent 60%);
  animation: boarding-slash 300ms 200ms ease-out both;
}
@keyframes boarding-slash {
  0% { transform: translateX(-100%) translateY(-100%); opacity: 0; }
  50% { opacity: 1; }
  100% { transform: translateX(100%) translateY(100%); opacity: 0; }
}
.boarding-cine-text {
  font-size: 3rem;
  font-weight: 900;
  color: var(--accent-red);
  text-shadow: 0 0 30px color-mix(in srgb, var(--accent-red) 60%, transparent);
  animation: boarding-text-pop 1200ms ease-out forwards;
}
@keyframes boarding-text-pop {
  0% { transform: scale(0.3); opacity: 0; }
  30% { transform: scale(1.1); opacity: 1; }
  60% { transform: scale(1); opacity: 1; }
  100% { opacity: 0; }
}
.boarding-cine-enter-active { animation: phase-overlay-in 0.2s ease-out; }
.boarding-cine-leave-active { transition: opacity 0.3s ease-out; }
.boarding-cine-leave-to { opacity: 0; }

@media (max-width: 480px) {
  .kill-streak { font-size: 1.5rem; }
}
</style>
