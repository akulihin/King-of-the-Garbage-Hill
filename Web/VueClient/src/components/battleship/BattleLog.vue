<script setup lang="ts">
withDefaults(defineProps<{
  entries: string[]
  maxHeight?: string
}>(), {
  maxHeight: '200px'
})

function logEntryClass(entry: string): string {
  if (entry.startsWith('[Мачта]')) return 'log-mast'
  if (entry.includes('потопил') || entry.includes('потоплен') || entry.includes('сгорел') || entry.includes('сгорел!')) return 'log-sunk'
  if (entry.includes('уничтожил палубу') || entry.includes('разрушил модуль')) return 'log-destroy'
  if (entry.includes('поцарапал')) return 'log-scratch'
  if (entry.includes('промахнулся') || entry.includes('Мимо')) return 'log-miss'
  if (entry.includes('увернул') || entry.includes('Юркая') || entry.includes('юркая')) return 'log-dodge'
  if (entry.includes('Зажигательный') || entry.includes('взорвал') || entry.includes('Брандер взорвался') || entry.includes('горит')) return 'log-burn'
  if (entry.includes('заморозил') || entry.includes('заморожен')) return 'log-freeze'
  if (entry.includes('опустошил') || entry.includes('Проклятый корабль')) return 'log-devastate'
  if (entry.includes('захватил')) return 'log-capture'
  if (entry.includes('протаранил') || entry.includes('врезался')) return 'log-ram'
  if (entry.includes('абордаж') || entry.includes('Абордаж')) return 'log-boarding'
  if (entry.includes('штраф') || entry.includes('пропускает') || entry.includes('оглушён')) return 'log-penalty'
  if (entry.includes('маневрирует')) return 'log-maneuver'
  return 'log-default'
}
</script>

<template>
  <div class="battle-log card-parchment" :style="{ maxHeight }">
    <div class="battle-log-header">
      <span class="header-icon">&#x2693;</span>
      Боевой журнал
    </div>
    <div class="battle-log-entries">
      <div
        v-for="(entry, i) in entries"
        :key="i"
        class="battle-log-entry"
        :class="logEntryClass(entry)"
      >
        {{ entry }}
      </div>
    </div>
  </div>
</template>

<style scoped>
.battle-log {
  overflow-y: auto;
  border-top: 3px solid var(--bs-wood-mid);
}

.battle-log-header {
  font-family: 'Pirata One', cursive;
  font-size: 1.1rem;
  color: var(--bs-gold);
  padding-bottom: 6px;
  margin-bottom: 4px;
  border-bottom: 1px solid var(--bs-parchment-dim);
  user-select: none;
}

.header-icon {
  margin-right: 6px;
  font-size: 1rem;
}

.battle-log-entries {
  display: flex;
  flex-direction: column-reverse;
  gap: 2px;
}

.battle-log-entry {
  font-size: 0.75rem;
  padding: 3px 8px;
  border-bottom: 1px solid var(--bs-wood-mid, rgba(74, 47, 26, 0.25));
  border-left: 3px solid var(--bs-parchment-dim);
  color: var(--bs-parchment-dim);
}

/* ── Entry type colors ─────────────────────────────────── */

.log-mast {
  border-left-color: var(--bs-gold);
  color: var(--bs-gold);
  font-style: italic;
}

.log-sunk {
  border-left-color: var(--bs-fire-red);
  color: var(--bs-fire-red);
  font-weight: 600;
}

.log-destroy {
  border-left-color: #ef8080;
  color: #ef8080;
}

.log-scratch {
  border-left-color: #ffa500;
  color: #ffa500;
}

.log-miss {
  border-left-color: var(--bs-parchment-dim);
  color: var(--bs-parchment-dim);
  opacity: 0.7;
}

.log-dodge {
  border-left-color: #00cc66;
  color: #00cc66;
}

.log-burn {
  border-left-color: var(--bs-fire-orange);
  color: var(--bs-fire-orange);
}

.log-freeze {
  border-left-color: var(--bs-ice-blue);
  color: var(--bs-ice-blue);
}

.log-devastate {
  border-left-color: var(--bs-cursed-purple);
  color: var(--bs-cursed-purple);
}

.log-capture {
  border-left-color: #b388ff;
  color: #b388ff;
}

.log-ram {
  border-left-color: var(--bs-gold);
  color: var(--bs-gold);
}

.log-boarding {
  border-left-color: var(--bs-fire-red);
  color: var(--bs-fire-red);
  font-weight: 600;
}

.log-penalty {
  border-left-color: #ff8888;
  color: #ff8888;
  font-style: italic;
}

.log-maneuver {
  border-left-color: var(--bs-ocean-blue);
  color: var(--bs-ocean-blue);
}

.log-default {
  border-left-color: var(--bs-parchment-dim);
  color: var(--bs-parchment-dim);
}
</style>
