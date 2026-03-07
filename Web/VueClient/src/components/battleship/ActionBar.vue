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

const props = defineProps<{
  maneuverableShips: ManeuverableShip[]
  cursedBoatSummons: CursedBoatSummon[]
  shotResult: { message: string } | null
  shotResultClass: Record<string, boolean>
  isMyTurn: boolean
  maneuveringDoubleUsed: boolean
}>()

const emit = defineEmits<{
  (e: 'manualMove', shipId: string, direction: string, distance: number): void
  (e: 'setCursedDirection', summonId: string, direction: string): void
}>()

const manualMoveDistance = ref(1)

function emitManualMove(shipId: string, direction: string) {
  emit('manualMove', shipId, direction, manualMoveDistance.value)
}
</script>

<template>
  <!-- Manual Move (Maneuvering Double) -->
  <template v-if="isMyTurn && !maneuveringDoubleUsed">
    <div
      v-for="ship in maneuverableShips"
      :key="ship.id"
      class="maneuver-bar card-wood"
    >
      <span class="action-label">Маневр: {{ ship.name }}</span>
      <select
        v-model.number="manualMoveDistance"
        class="maneuver-select"
        @mouseenter="showTip($event, 'Расстояние маневра в клетках')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
      >
        <option :value="1">1 клетка</option>
        <option :value="2">2 клетки</option>
      </select>
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
    class="cursed-bar card-wood"
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
.cursed-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  margin-top: 0.5rem;
  flex-wrap: wrap;
  border-radius: 6px;
}

/* ── Labels ──────────────────────────────────────────────── */
.action-label {
  font-family: 'Crimson Text', 'Georgia', serif;
  font-weight: 700;
  font-size: 0.85rem;
  color: var(--bs-parchment, #e8d5b0);
  white-space: nowrap;
}

/* ── Distance select ─────────────────────────────────────── */
.maneuver-select {
  padding: 4px 8px;
  border-radius: 4px;
  background: var(--bs-wood-dark, #2a1a0e);
  color: var(--bs-parchment, #e8d5b0);
  border: 1px solid var(--bs-wood-mid, #4a2f1a);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  cursor: pointer;
  outline: none;
}

.maneuver-select:focus {
  border-color: var(--bs-gold, #d4a847);
}

/* ── Direction buttons (compass-rose style) ──────────────── */
.dir-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  border: 2px solid var(--bs-wood-mid, #4a2f1a);
  background: var(--bs-wood-dark, #2a1a0e);
  color: var(--bs-parchment, #e8d5b0);
  font-size: 1rem;
  font-weight: 700;
  cursor: pointer;
  transition: color 0.2s, box-shadow 0.2s, transform 0.1s;
  line-height: 1;
  padding: 0;
}

.dir-btn:hover {
  color: var(--bs-gold-bright, #f0c850);
  box-shadow: 0 0 8px rgba(240, 200, 80, 0.35);
  border-color: var(--bs-gold, #d4a847);
}

.dir-btn:active {
  transform: scale(0.92);
  box-shadow: 0 0 4px rgba(240, 200, 80, 0.2);
}

/* ── Shot result banner ──────────────────────────────────── */
.shot-result {
  text-align: center;
  padding: 0.5rem 1rem;
  margin: 0.75rem 0;
  border-radius: 6px;
  font-family: 'Crimson Text', 'Georgia', serif;
  font-weight: 700;
  font-size: 0.9rem;
  animation: scroll-unroll 0.4s ease-out;
}

.shot-hit {
  background: rgba(192, 57, 43, 0.15);
  color: var(--bs-fire-red, #c0392b);
}

.shot-miss {
  background: rgba(148, 156, 164, 0.1);
  color: var(--bs-parchment-dim, #b09a78);
}

.shot-scratch {
  background: rgba(255, 165, 0, 0.15);
  color: #ffa500;
}

.shot-dodge {
  background: rgba(0, 255, 136, 0.12);
  color: #00cc66;
}

.shot-sunk {
  background: rgba(204, 0, 0, 0.2) !important;
  color: var(--bs-fire-red, #c0392b) !important;
  font-size: 1rem;
  font-weight: 800;
  animation: banner-appear 0.5s ease-out;
}

.shot-burn {
  background: rgba(255, 102, 0, 0.2) !important;
  color: var(--bs-fire-orange, #e67e22) !important;
}

.shot-destroy {
  background: rgba(192, 57, 43, 0.2) !important;
  color: var(--bs-fire-red, #c0392b) !important;
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

@keyframes banner-appear {
  from {
    opacity: 0;
    transform: scale(0.9);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}
</style>
