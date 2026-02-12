<script setup lang="ts">
import type { Player } from 'src/services/signalr'

defineProps<{
  player: Player
}>()
</script>

<template>
  <div class="skills-panel">
    <div
      v-for="(passive, idx) in player.character.passives"
      :key="idx"
      class="skill-card"
      :class="{ hidden: !passive.visible }"
    >
      <div class="skill-header">
        <span class="skill-name">{{ passive.name }}</span>
        <span v-if="!passive.visible" class="hidden-badge">Hidden</span>
      </div>
      <div class="skill-desc">
        {{ passive.description }}
      </div>
    </div>

    <div v-if="player.character.passives.length === 0" class="no-skills">
      No passives available.
    </div>
  </div>
</template>

<style scoped>
.skills-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.skill-card {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  padding: 14px;
  transition: border-color 0.2s;
}

.skill-card:hover {
  border-color: var(--accent-purple);
}

.skill-card.hidden {
  opacity: 0.6;
  border-style: dashed;
}

.skill-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.skill-name {
  font-weight: 700;
  font-size: 14px;
  color: var(--accent-gold);
}

.hidden-badge {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: 4px;
  background: var(--text-muted);
  color: var(--bg-primary);
  font-weight: 600;
}

.skill-desc {
  font-size: 12px;
  line-height: 1.5;
  color: var(--text-secondary);
}

.no-skills {
  color: var(--text-muted);
  font-style: italic;
  text-align: center;
  padding: 24px;
  font-size: 13px;
}
</style>
