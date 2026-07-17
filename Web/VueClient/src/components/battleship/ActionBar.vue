<script setup lang="ts">
import { ref } from 'vue'
import { useTip } from 'src/composables/useTip'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

interface ManeuverableShip {
  id: string
  name: string
  orientation: string
}

interface CursedBoatSummon {
  id: string
  waitingForDirectionChoice: boolean
}

defineProps<{
  maneuverableShips: ManeuverableShip[]
  cursedBoatSummons: CursedBoatSummon[]
  shotResult: { message: string } | null
  shotResultClass: Record<string, boolean>
  isMyTurn: boolean
  canPassBoarding: boolean
}>()

const emit = defineEmits<{
  (e: 'manualMove', shipId: string, direction: string, distance: number): void
  (e: 'setCursedDirection', summonId: string, direction: string): void
  (e: 'passBoarding'): void
}>()

const manualMoveDistance = ref(1)

function emitManualMove(shipId: string, direction: string) {
  emit('manualMove', shipId, direction, manualMoveDistance.value)
}
</script>

<template>
  <div v-if="isMyTurn && canPassBoarding" class="pass-bar bs-bar">
    <span class="action-label">Нет доступного выстрела</span>
    <button class="bs-btn bs-btn--primary" type="button" @click="emit('passBoarding')">
      Двигать призывы и завершить ход
    </button>
  </div>

  <!-- Manual Move (Maneuvering Double) — per-ship activation (ТЗ #21) -->
  <template v-if="isMyTurn">
    <div
      v-for="ship in maneuverableShips"
      :key="ship.id"
      class="maneuver-bar bs-bar"
    >
      <span class="action-label">Маневр: {{ ship.name }}</span>
      <div
        class="bs-seg"
        role="group"
        aria-label="Расстояние маневра"
        @mouseenter="showTip($event, 'Расстояние маневра в клетках')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
      >
        <button
          class="bs-seg-btn"
          type="button"
          :aria-pressed="manualMoveDistance === 1"
          @click="manualMoveDistance = 1"
        >1 клетка</button>
        <button
          class="bs-seg-btn"
          type="button"
          :aria-pressed="manualMoveDistance === 2"
          @click="manualMoveDistance = 2"
        >2 клетки</button>
      </div>
      <template v-if="ship.orientation === 'Horizontal'">
        <button
          class="dir-btn"
          @mouseenter="showTip($event, 'Передвинуть корабль влево')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
          @click="emitManualMove(ship.id, 'Left')"
        >&#x2190;</button>
        <button
          class="dir-btn"
          @mouseenter="showTip($event, 'Передвинуть корабль вправо')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
          @click="emitManualMove(ship.id, 'Right')"
        >&#x2192;</button>
      </template>
      <template v-else>
        <button
          class="dir-btn"
          @mouseenter="showTip($event, 'Передвинуть корабль вверх')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
          @click="emitManualMove(ship.id, 'Up')"
        >&#x2191;</button>
        <button
          class="dir-btn"
          @mouseenter="showTip($event, 'Передвинуть корабль вниз')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
          @click="emitManualMove(ship.id, 'Down')"
        >&#x2193;</button>
      </template>
    </div>
  </template>

  <!-- Cursed Boat Direction Choice -->
  <div
    v-if="cursedBoatSummons.some(s => s.waitingForDirectionChoice)"
    class="cursed-bar bs-bar"
  >
    <span class="action-label">Проклятый корабль — выберите направление:</span>
    <template v-for="s in cursedBoatSummons.filter(s => s.waitingForDirectionChoice)" :key="s.id">
      <button
        class="dir-btn"
        @mouseenter="showTip($event, 'Направление: вверх')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="emit('setCursedDirection', s.id, 'Up')"
      >&#x2191;</button>
      <button
        class="dir-btn"
        @mouseenter="showTip($event, 'Направление: вниз')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="emit('setCursedDirection', s.id, 'Down')"
      >&#x2193;</button>
      <button
        class="dir-btn"
        @mouseenter="showTip($event, 'Направление: влево')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="emit('setCursedDirection', s.id, 'Left')"
      >&#x2190;</button>
      <button
        class="dir-btn"
        @mouseenter="showTip($event, 'Направление: вправо')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="emit('setCursedDirection', s.id, 'Right')"
      >&#x2192;</button>
    </template>
  </div>

  <!-- Shot Result -->
  <div v-if="shotResult" class="shot-result" :class="shotResultClass">
    {{ shotResult.message }}
  </div>

  <!-- Tooltip -->
  <Teleport to="body">
    <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
      {{ tipText }}
    </div>
  </Teleport>
</template>

<style scoped>
/* ── Action bars (maneuver, cursed) ──────────────────────── */
.maneuver-bar,
.cursed-bar,
.pass-bar {
  margin-top: 0.5rem;
}

/* ── Labels ──────────────────────────────────────────────── */
.action-label {
  font-weight: 700;
  font-size: 0.8rem;
  color: var(--text-secondary);
  white-space: nowrap;
}

/* ── Direction buttons (compass style) ───────────────────── */
.dir-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border-radius: 50%;
  border: 1px solid var(--glass-border);
  background: rgba(255, 255, 255, 0.04);
  color: var(--text-secondary);
  font-size: 1rem;
  font-weight: 700;
  cursor: pointer;
  transition: color 0.2s, box-shadow 0.2s, transform 0.1s;
  line-height: 1;
  padding: 0;
}

.dir-btn:hover {
  color: var(--accent-gold);
  box-shadow: var(--glow-gold);
  border-color: color-mix(in srgb, var(--accent-gold) 50%, transparent);
}

.dir-btn:active {
  transform: scale(0.92);
}

/* ── Shot result banner ──────────────────────────────────── */
.shot-result {
  text-align: center;
  padding: 0.5rem 1rem;
  margin: 0.75rem 0;
  border-radius: 10px;
  border: 1px solid var(--glass-border);
  font-weight: 700;
  font-size: 0.85rem;
  animation: scroll-unroll 0.4s ease-out;
}

.shot-hit {
  background: color-mix(in srgb, var(--accent-red) 12%, transparent);
  border-color: color-mix(in srgb, var(--accent-red) 30%, transparent);
  color: var(--accent-red);
}

.shot-miss {
  background: rgba(255, 255, 255, 0.03);
  color: var(--text-muted);
}

.shot-scratch {
  background: color-mix(in srgb, var(--accent-orange) 12%, transparent);
  border-color: color-mix(in srgb, var(--accent-orange) 30%, transparent);
  color: var(--accent-orange);
}

.shot-dodge {
  background: color-mix(in srgb, var(--accent-green) 10%, transparent);
  border-color: color-mix(in srgb, var(--accent-green) 30%, transparent);
  color: var(--accent-green);
}

.shot-sunk {
  background: color-mix(in srgb, var(--accent-red) 18%, transparent) !important;
  border-color: color-mix(in srgb, var(--accent-red) 40%, transparent) !important;
  color: var(--accent-red) !important;
  font-size: 1rem;
  font-weight: 800;
  animation: bs-banner-appear 0.5s ease-out;
}

.shot-burn {
  background: color-mix(in srgb, var(--accent-orange) 16%, transparent) !important;
  border-color: color-mix(in srgb, var(--accent-orange) 40%, transparent) !important;
  color: var(--accent-orange) !important;
}

.shot-destroy {
  background: color-mix(in srgb, var(--accent-red) 16%, transparent) !important;
  border-color: color-mix(in srgb, var(--accent-red) 35%, transparent) !important;
  color: var(--accent-red) !important;
}

/* ── Animations ──────────────────────────────────────────── */
@keyframes scroll-unroll {
  from {
    opacity: 0;
    transform: scaleY(0.9);
    transform-origin: top center;
  }
  to {
    opacity: 1;
    transform: scaleY(1);
    transform-origin: top center;
  }
}
</style>
