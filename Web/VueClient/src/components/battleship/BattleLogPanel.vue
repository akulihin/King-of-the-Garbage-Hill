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
  <div class="battle-log bs-card" :style="{ maxHeight }">
    <div class="battle-log-header">
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
  padding: 10px 12px;
}

.battle-log-header {
  color: var(--text-muted);
  font-size: 0.62rem;
  font-weight: 900;
  letter-spacing: 1.5px;
  text-transform: uppercase;
  padding-bottom: 6px;
  margin-bottom: 4px;
  border-bottom: 1px solid var(--glass-border);
  user-select: none;
}

.battle-log-entries {
  display: flex;
  flex-direction: column-reverse;
  gap: 2px;
}

.battle-log-entry {
  --log-color: var(--text-muted);
  font-size: 0.72rem;
  padding: 3px 8px;
  border-bottom: 1px solid var(--glass-border);
  border-left: 3px solid var(--log-color);
  color: var(--log-color);
}

/* ── Entry type colors ─────────────────────────────────── */

.log-mast {
  --log-color: var(--accent-gold);
  font-style: italic;
}

.log-sunk {
  --log-color: var(--accent-red);
  font-weight: 600;
}

.log-destroy { --log-color: color-mix(in srgb, var(--accent-red) 70%, white); }

.log-scratch { --log-color: var(--accent-orange); }

.log-miss {
  --log-color: var(--text-dim);
  opacity: 0.8;
}

.log-dodge { --log-color: var(--accent-green); }

.log-burn { --log-color: var(--accent-orange); }

.log-freeze { --log-color: var(--accent-blue); }

.log-devastate { --log-color: var(--accent-purple); }

.log-capture { --log-color: color-mix(in srgb, var(--accent-purple) 75%, white); }

.log-ram { --log-color: var(--accent-gold); }

.log-boarding {
  --log-color: var(--accent-red);
  font-weight: 600;
}

.log-penalty {
  --log-color: color-mix(in srgb, var(--accent-red) 70%, white);
  font-style: italic;
}

.log-maneuver { --log-color: var(--accent-blue); }

.log-default { --log-color: var(--text-muted); }
</style>
