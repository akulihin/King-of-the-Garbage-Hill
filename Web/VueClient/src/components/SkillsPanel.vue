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
  gap: 6px;
}

.skill-card {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  padding: 10px 12px;
  transition: border-color 0.2s;
}

.skill-card:hover {
  border-color: var(--kh-c-secondary-purple-400);
}

.skill-card.hidden {
  opacity: 0.5;
  border-style: dashed;
  border-color: var(--border-subtle);
}

.skill-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}

.skill-name {
  font-weight: 800;
  font-size: 12px;
  color: var(--accent-gold);
  letter-spacing: 0.2px;
}

.hidden-badge {
  font-size: 8px;
  padding: 1px 6px;
  border-radius: 3px;
  background: var(--text-dim);
  color: var(--bg-primary);
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.skill-desc {
  font-size: 11px;
  line-height: 1.5;
  color: var(--text-secondary);
}

.no-skills {
  color: var(--text-dim);
  font-style: italic;
  text-align: center;
  padding: 20px;
  font-size: 11px;
}
</style>
