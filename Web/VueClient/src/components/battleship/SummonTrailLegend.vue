<script setup lang="ts">
import { computed } from 'vue'
import type {
  BattleshipCell,
  BattleshipSummonMarker,
} from 'src/services/signalr'
import { message } from 'src/platform/localization'
import { renderIcon } from './battleship-icons'
import {
  summonIconKey,
  summonMarkerClass,
} from './battleship-summon-presentation'

const props = defineProps<{
  cells?: BattleshipCell[]
}>()

type TrailLegendEntry = {
  key: string
  label: string
  icon: string
  markerClass: string
}

const ordinaryTrailNames: Record<string, string> = {
  Ram: 'Таран',
  Scout: 'Разведчик',
  Brander: 'Брандер',
  CursedBoat: 'Проклятая лодка',
  PirateBoat: 'Пиратская лодка',
}

function displayKey(marker: BattleshipSummonMarker): string {
  if (!marker.isBoardingShip) return `type:${marker.type}`
  return 'boarding'
}

const entries = computed<TrailLegendEntry[]>(() => {
  const byDisplay = new Map<string, TrailLegendEntry>()
  for (const cell of props.cells ?? []) {
    for (const marker of cell.summonTrails ?? []) {
      const key = displayKey(marker)
      if (byDisplay.has(key)) continue
      byDisplay.set(key, {
        key,
        label: marker.isBoardingShip
          ? message('battleship.boarding.genericShip')
          : (ordinaryTrailNames[marker.type] ?? marker.type),
        icon: summonIconKey(marker.type, marker.isBoardingShip),
        markerClass: summonMarkerClass(marker),
      })
    }
  }
  return [...byDisplay.values()]
})
</script>

<template>
  <div v-if="entries.length" class="range-legend">
    <span
      v-for="entry in entries"
      :key="entry.key"
      class="legend-item"
      :class="'legend-trail-' + entry.markerClass"
    >
      <span v-html="renderIcon(entry.icon, 12)"></span>
      След: {{ entry.label }}
    </span>
  </div>
</template>

<style scoped>
.range-legend {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.25rem;
  flex-wrap: wrap;
  justify-content: center;
}
.legend-item {
  --legend-color: var(--text-muted);
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 1px 6px;
  color: var(--legend-color);
  background: color-mix(in srgb, var(--legend-color) 11%, transparent);
  border: 1px solid color-mix(in srgb, var(--legend-color) 35%, transparent);
  border-radius: 6px;
  font-size: 0.6rem;
  font-weight: 600;
  white-space: nowrap;
}
.legend-trail-ram { --legend-color: var(--accent-red); }
.legend-trail-scout { --legend-color: var(--accent-blue); }
.legend-trail-brander { --legend-color: var(--accent-orange); }
.legend-trail-cursedboat { --legend-color: var(--accent-purple); }
.legend-trail-pirateboat { --legend-color: var(--accent-gold); }
.legend-trail-boarding { --legend-color: #86efac; }
</style>
