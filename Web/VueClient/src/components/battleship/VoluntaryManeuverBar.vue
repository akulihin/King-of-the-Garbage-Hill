<script setup lang="ts">
import type { BattleshipVoluntaryManeuver } from 'src/services/signalr'
import { message } from 'src/platform/localization'
import BsIcon from './BsIcon.vue'

defineProps<{
  maneuvers: BattleshipVoluntaryManeuver[]
  selectedShipId: string | null
  disabled?: boolean
}>()

defineEmits<{
  (e: 'select', shipId: string): void
  (e: 'cancel'): void
}>()
</script>

<template>
  <section v-if="maneuvers.length" class="voluntary-maneuver-bar bs-card">
    <div class="voluntary-maneuver-copy">
      <span class="voluntary-maneuver-title">
        <BsIcon icon="waves" :size="14" />
        {{ message('battleship.maneuver.voluntaryTitle') }}
      </span>
      <small>
        {{ selectedShipId
          ? message('battleship.maneuver.destinationHint')
          : message('battleship.maneuver.selectHint') }}
      </small>
    </div>
    <div class="voluntary-maneuver-actions">
      <button
        v-for="maneuver in maneuvers"
        :key="maneuver.shipId"
        type="button"
        class="bs-btn bs-btn--sm maneuver-choice"
        :class="{ 'maneuver-choice--active': selectedShipId === maneuver.shipId }"
        :disabled="disabled || maneuver.options.length === 0"
        :aria-pressed="selectedShipId === maneuver.shipId"
        @click="$emit('select', maneuver.shipId)"
      >
        <BsIcon icon="ship" :size="13" />
        {{ maneuver.shipName }}
      </button>
      <button
        v-if="selectedShipId"
        type="button"
        class="bs-btn bs-btn--sm maneuver-cancel"
        @click="$emit('cancel')"
      >
        <BsIcon icon="x" :size="13" />
        {{ message('battleship.maneuver.cancel') }}
      </button>
    </div>
  </section>
</template>

<style scoped>
.voluntary-maneuver-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.7rem;
  margin: 0.45rem 0 0.65rem;
  padding: 0.55rem 0.7rem;
  border-color: color-mix(in srgb, var(--accent-green) 38%, var(--glass-border));
}
.voluntary-maneuver-copy {
  display: flex;
  flex-direction: column;
  gap: 0.12rem;
  min-width: 0;
}
.voluntary-maneuver-copy small {
  color: var(--text-dim);
  font-size: 0.64rem;
}
.voluntary-maneuver-title {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  color: var(--text-primary);
  font-size: 0.72rem;
  font-weight: 900;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.voluntary-maneuver-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.35rem;
  flex-wrap: wrap;
}
.maneuver-choice--active {
  color: #dcfce7;
  border-color: #4ade80;
  background: color-mix(in srgb, #22c55e 25%, var(--bg-card));
  box-shadow: 0 0 13px rgba(34, 197, 94, 0.3);
}
.maneuver-cancel {
  color: var(--text-muted);
}

@media (max-width: 720px) {
  .voluntary-maneuver-bar {
    align-items: stretch;
    flex-direction: column;
  }
  .voluntary-maneuver-actions {
    justify-content: flex-start;
  }
}
</style>
