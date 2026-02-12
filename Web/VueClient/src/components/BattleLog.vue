<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  logs: string
}>()

type BattleEntry = {
  type: 'battle' | 'event' | 'header'
  attacker?: string
  defender?: string
  winner?: string
  text?: string
}

const entries = computed<BattleEntry[]>(() => {
  if (!props.logs) return []
  const lines = props.logs.split('\n').filter(l => l.trim())
  const result: BattleEntry[] = []

  for (const raw of lines) {
    // Strip Discord custom emoji codes like <:war:123456>
    const line = raw.replace(/<:[^:]+:\d+>/g, '').trim()
    if (!line) continue

    // Round header like "**Раунд #9**:" or "__Раунд #2__:"
    const headerMatch = line.match(/\*\*(.+?)\*\*:?$/) || line.match(/__(.+?)__:?$/)
    if (headerMatch) {
      result.push({ type: 'header', text: headerMatch[1] })
      continue
    }

    // Battle line: "Player1 Player2 → Winner" (with optional emoji stripped)
    // The pattern is: "AttackerName DefenderName → WinnerName"
    const battleMatch = line.match(/^(.+?)\s+(.+?)\s*→\s*(.+)$/)
    if (battleMatch) {
      const winner = battleMatch[3].trim()
      // Check if it's a "no fight" result
      const isNoFight = winner.includes('не состоялся') || winner.includes('не состоялась')
      result.push({
        type: 'battle',
        attacker: battleMatch[1].trim(),
        defender: battleMatch[2].trim(),
        winner: isNoFight ? undefined : winner,
        text: isNoFight ? 'Бой не состоялся...' : undefined,
      })
      continue
    }

    // Other event text
    result.push({ type: 'event', text: line.replace(/\*\*/g, '').replace(/__/g, '').replace(/\*/g, '') })
  }

  return result
})
</script>

<template>
  <div class="battle-log">
    <div v-if="entries.length === 0" class="log-empty">
      No events yet.
    </div>

    <template v-for="(entry, idx) in entries" :key="idx">
      <!-- Round header -->
      <div v-if="entry.type === 'header'" class="bl-header">
        {{ entry.text }}
      </div>

      <!-- Battle card -->
      <div v-else-if="entry.type === 'battle'" class="bl-battle" :class="{ 'no-fight': !entry.winner }">
        <span
          class="bl-player attacker"
          :class="{ winner: entry.winner === entry.attacker }"
        >
          {{ entry.attacker }}
        </span>
        <img src="/art/emojis/war.png" alt="⚔" class="bl-sword" onerror="this.style.display='none'; this.nextElementSibling.style.display='inline'">
        <span class="bl-sword-fallback" style="display:none">⚔</span>
        <span
          class="bl-player defender"
          :class="{ winner: entry.winner === entry.defender }"
        >
          {{ entry.defender }}
        </span>
        <span v-if="entry.winner" class="bl-result">
          → <strong>{{ entry.winner }}</strong>
        </span>
        <span v-else-if="entry.text" class="bl-result no-fight-text">
          → <em>{{ entry.text }}</em>
        </span>
      </div>

      <!-- Other event -->
      <div v-else class="bl-event">
        {{ entry.text }}
      </div>
    </template>
  </div>
</template>

<style scoped>
.battle-log {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 260px;
  overflow-y: auto;
  padding: 6px;
  background: var(--bg-primary);
  border-radius: var(--radius);
}

.log-empty {
  color: var(--text-muted);
  font-style: italic;
  padding: 16px;
  text-align: center;
  font-size: 13px;
}

.bl-header {
  font-size: 13px;
  font-weight: 700;
  color: var(--accent-gold);
  padding: 6px 8px 2px;
  text-decoration: underline;
}

.bl-battle {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  border-radius: 6px;
  background: var(--bg-secondary);
  font-size: 12px;
}

.bl-battle.no-fight {
  opacity: 0.6;
}

.bl-player {
  font-weight: 600;
  color: var(--text-secondary);
}

.bl-player.winner {
  color: var(--accent-green);
}

.bl-sword {
  width: 16px;
  height: 16px;
  object-fit: contain;
  flex-shrink: 0;
  filter: brightness(0) invert(0.6);
}

.bl-result {
  margin-left: auto;
  font-size: 11px;
  color: var(--text-muted);
}

.bl-result strong {
  color: var(--accent-green);
}

.no-fight-text em {
  color: var(--accent-red);
}

.bl-event {
  padding: 4px 8px;
  font-size: 12px;
  color: var(--accent-orange);
  font-style: italic;
}
</style>
