<script setup lang="ts">
import { ref } from 'vue'
import type { Player } from 'src/services/signalr'

defineProps<{
  player: Player
}>()

const expandedSet = ref<Set<number>>(new Set())

function toggleSkill(idx: number) {
  if (expandedSet.value.has(idx)) {
    expandedSet.value.delete(idx)
  } else {
    expandedSet.value.add(idx)
  }
  // Trigger reactivity
  expandedSet.value = new Set(expandedSet.value)
}

function isExpanded(idx: number): boolean {
  return expandedSet.value.has(idx)
}
</script>

<template>
  <div class="skills-panel">
    <div
      v-for="(passive, idx) in player.character.passives"
      :key="idx"
      class="skill-card"
      :class="{ hidden: !passive.visible, expanded: isExpanded(idx) }"
      @click="toggleSkill(idx)"
    >
      <div class="skill-header">
        <div class="skill-header-left">
          <span class="skill-dot" :class="passive.visible ? 'dot-active' : 'dot-inactive'" />
          <span class="skill-name">{{ passive.name }}</span>
        </div>
        <div class="skill-header-right">
          <span v-if="!passive.visible" class="hidden-badge">Hidden</span>
          <span class="skill-chevron" :class="{ 'chevron-open': isExpanded(idx) }">▾</span>
        </div>
      </div>
      <Transition name="expand">
        <div v-if="isExpanded(idx)" class="skill-desc">
          {{ passive.description }}
        </div>
      </Transition>
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
  background: var(--glass-bg);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  border: 1px solid var(--glass-border);
  border-left: 3px solid var(--accent-purple);
  border-radius: var(--radius);
  padding: 9px 12px;
  transition: all 0.25s var(--ease-in-out);
  cursor: pointer;
  user-select: none;
  box-shadow: inset 0 1px 0 var(--glass-highlight);
}

.skill-card:hover {
  border-color: rgba(180, 150, 255, 0.25);
  border-left-color: var(--kh-c-secondary-purple-300);
  transform: translateX(2px);
  box-shadow: 0 0 12px rgba(180, 150, 255, 0.12), 0 2px 8px rgba(0, 0, 0, 0.15), inset 0 1px 0 var(--glass-highlight);
}

.skill-card.expanded {
  box-shadow: 0 0 14px rgba(180, 150, 255, 0.15), inset 0 0 10px rgba(180, 150, 255, 0.05), 0 2px 8px rgba(0, 0, 0, 0.12);
  border-left-color: var(--accent-purple);
  border-color: rgba(180, 150, 255, 0.2);
}

.skill-card.hidden {
  opacity: 0.45;
  border-style: dashed;
  border-left-style: solid;
  border-color: var(--border-subtle);
  border-left-color: var(--text-dim);
}

.skill-card.hidden:hover {
  opacity: 0.6;
}

.skill-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.skill-header-left {
  display: flex;
  align-items: center;
  gap: 6px;
}

.skill-header-right {
  display: flex;
  align-items: center;
  gap: 6px;
}

.skill-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  flex-shrink: 0;
  transition: box-shadow 0.3s;
}

.dot-active {
  background: var(--accent-green);
  box-shadow: 0 0 6px rgba(63, 167, 61, 0.5);
  animation: dot-glow 2s ease-in-out infinite;
}

@keyframes dot-glow {
  0%, 100% { box-shadow: 0 0 4px rgba(63, 167, 61, 0.4); }
  50% { box-shadow: 0 0 8px rgba(63, 167, 61, 0.7); }
}

.dot-inactive {
  background: var(--text-dim);
}

.skill-name {
  font-weight: 800;
  font-size: 12px;
  color: var(--accent-gold);
  letter-spacing: 0.2px;
  text-shadow: 0 0 6px rgba(240, 200, 80, 0.15);
}

.skill-chevron {
  font-size: 11px;
  color: var(--text-dim);
  transition: transform 0.3s var(--ease-spring), color 0.2s;
  line-height: 1;
}

.chevron-open {
  transform: rotate(180deg);
  color: var(--accent-purple);
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
  padding-top: 6px;
  padding-left: 12px;
}

/* Expand transition */
.expand-enter-active {
  transition: all 0.35s var(--ease-spring);
  overflow: hidden;
}
.expand-leave-active {
  transition: all 0.2s ease;
  overflow: hidden;
}
.expand-enter-from,
.expand-leave-to {
  opacity: 0;
  max-height: 0;
  padding-top: 0;
}
.expand-enter-to,
.expand-leave-from {
  opacity: 1;
  max-height: 200px;
}

.no-skills {
  color: var(--text-dim);
  font-style: italic;
  text-align: center;
  padding: 20px;
  font-size: 11px;
}
</style>
