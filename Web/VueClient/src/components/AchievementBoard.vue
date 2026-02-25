<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useGameStore } from 'src/store/game'
import type { AchievementEntry } from 'src/services/signalr'

const emit = defineEmits<{ close: [] }>()
const store = useGameStore()

const selectedCategory = ref<string>('all')
const showUnlockedOnly = ref(false)

onMounted(() => {
  store.requestAchievements()
})

const board = computed(() => store.achievementBoard)
const achievements = computed(() => board.value?.achievements ?? [])

const categories = computed(() => {
  const cats = new Set(achievements.value.map(a => a.category))
  return ['all', ...Array.from(cats)]
})

const filtered = computed(() => {
  let list = achievements.value
  if (selectedCategory.value !== 'all') {
    list = list.filter(a => a.category === selectedCategory.value)
  }
  if (showUnlockedOnly.value) {
    list = list.filter(a => a.isUnlocked)
  }
  return list
})

const progressPercent = computed(() => {
  if (!board.value) return 0
  return Math.round((board.value.totalUnlocked / board.value.totalAchievements) * 100)
})

function rarityColor(rarity: string): string {
  const colors: Record<string, string> = {
    common: '#9e9e9e',
    uncommon: '#4caf50',
    rare: '#2196f3',
    epic: '#9c27b0',
    legendary: '#ff9800',
  }
  return colors[rarity.toLowerCase()] ?? '#9e9e9e'
}

function categoryLabel(cat: string): string {
  if (cat === 'all') return 'All'
  return cat.charAt(0).toUpperCase() + cat.slice(1)
}

function displayName(a: AchievementEntry): string {
  if (a.isSecret && !a.isUnlocked) return '???'
  return a.name
}

function displayDescription(a: AchievementEntry): string {
  if (a.isSecret && !a.isUnlocked) return a.secretHint || 'Secret achievement'
  return a.description
}

function progressText(a: AchievementEntry): string {
  if (a.isUnlocked) return 'Unlocked'
  if (a.target <= 1) return ''
  return `${a.current} / ${a.target}`
}
</script>

<template>
  <div class="achievement-overlay" @click.self="emit('close')">
    <div class="achievement-board">
      <div class="board-header">
        <h2>Achievements</h2>
        <button class="btn-close" @click="emit('close')">&times;</button>
      </div>

      <!-- Progress bar -->
      <div v-if="board" class="board-progress">
        <div class="progress-info">
          <span>{{ board.totalUnlocked }} / {{ board.totalAchievements }}</span>
          <span class="progress-pct">{{ progressPercent }}%</span>
        </div>
        <div class="progress-bar-track">
          <div class="progress-bar-fill" :style="{ width: progressPercent + '%' }" />
        </div>
      </div>

      <!-- Category tabs -->
      <div class="category-tabs">
        <button
          v-for="cat in categories"
          :key="cat"
          class="tab"
          :class="{ active: selectedCategory === cat }"
          @click="selectedCategory = cat"
        >
          {{ categoryLabel(cat) }}
        </button>
        <label class="filter-toggle">
          <input v-model="showUnlockedOnly" type="checkbox">
          <span>Unlocked only</span>
        </label>
      </div>

      <!-- Achievement grid -->
      <div v-if="!board" class="loading">Loading...</div>
      <div v-else class="achievements-grid">
        <div
          v-for="a in filtered"
          :key="a.id"
          class="achievement-card"
          :class="{
            unlocked: a.isUnlocked,
            secret: a.isSecret && !a.isUnlocked,
            newly: board.newlyUnlocked.includes(a.id),
          }"
        >
          <div class="ach-icon" :style="{ borderColor: a.isUnlocked ? rarityColor(a.rarity) : 'var(--border-subtle)' }">
            <span v-if="a.isUnlocked" class="icon-text">{{ a.icon }}</span>
            <span v-else-if="a.isSecret" class="icon-lock">?</span>
            <span v-else class="icon-lock">{{ a.icon }}</span>
          </div>
          <div class="ach-info">
            <div class="ach-name" :style="{ color: a.isUnlocked ? rarityColor(a.rarity) : '' }">
              {{ displayName(a) }}
            </div>
            <div class="ach-desc">{{ displayDescription(a) }}</div>
            <div v-if="progressText(a)" class="ach-progress">
              <template v-if="a.isUnlocked">
                <span class="ach-unlocked-label">Unlocked</span>
              </template>
              <template v-else>
                <div class="ach-progress-bar">
                  <div class="ach-progress-fill" :style="{ width: `${(a.current / a.target) * 100}%` }" />
                </div>
                <span class="ach-progress-text">{{ a.current }}/{{ a.target }}</span>
              </template>
            </div>
          </div>
          <div v-if="a.isUnlocked" class="ach-rarity-dot" :style="{ background: rarityColor(a.rarity) }" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.achievement-overlay {
  position: fixed;
  inset: 0;
  z-index: 900;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.7);
  backdrop-filter: blur(4px);
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

.achievement-board {
  width: 90vw;
  max-width: 720px;
  max-height: 85vh;
  background: var(--bg-surface, var(--kh-c-neutrals-sat-700));
  border: 1px solid var(--border-subtle);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  animation: slideUp 0.3s ease;
}

@keyframes slideUp {
  from { transform: translateY(20px); opacity: 0; }
  to { transform: translateY(0); opacity: 1; }
}

.board-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-subtle);
}

.board-header h2 {
  font-size: 18px;
  font-weight: 800;
  color: var(--accent-gold);
  text-transform: uppercase;
  letter-spacing: 1px;
  margin: 0;
}

.btn-close {
  background: none;
  border: none;
  color: var(--text-muted);
  font-size: 24px;
  cursor: pointer;
  padding: 0 4px;
  line-height: 1;
}
.btn-close:hover { color: var(--text-primary); }

.board-progress {
  padding: 12px 20px;
  border-bottom: 1px solid var(--border-subtle);
}

.progress-info {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  font-weight: 700;
  color: var(--text-muted);
  margin-bottom: 6px;
}

.progress-pct {
  color: var(--accent-gold);
}

.progress-bar-track {
  height: 6px;
  background: var(--bg-elevated, var(--kh-c-neutrals-sat-600));
  border-radius: 3px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  background: var(--accent-gold);
  border-radius: 3px;
  transition: width 0.6s ease;
}

.category-tabs {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 10px 20px;
  border-bottom: 1px solid var(--border-subtle);
  overflow-x: auto;
  flex-wrap: wrap;
}

.tab {
  background: none;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius, 6px);
  color: var(--text-muted);
  font-size: 11px;
  font-weight: 700;
  padding: 4px 10px;
  cursor: pointer;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  white-space: nowrap;
}
.tab:hover { color: var(--text-primary); border-color: var(--text-muted); }
.tab.active {
  background: var(--kh-c-secondary-info-500, rgba(50, 100, 170, 1));
  border-color: var(--accent-blue, #4da6ff);
  color: var(--text-primary);
}

.filter-toggle {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  color: var(--text-muted);
  cursor: pointer;
  margin-left: auto;
  white-space: nowrap;
}
.filter-toggle input { cursor: pointer; }

.loading {
  padding: 40px;
  text-align: center;
  color: var(--text-muted);
}

.achievements-grid {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.achievement-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--bg-card, var(--kh-c-neutrals-sat-650));
  border: 1px solid var(--border-subtle);
  transition: all 0.2s ease;
  position: relative;
}

.achievement-card:not(.unlocked) {
  opacity: 0.55;
}

.achievement-card.unlocked {
  opacity: 1;
}

.achievement-card.newly {
  animation: newGlow 2s ease infinite;
}

@keyframes newGlow {
  0%, 100% { box-shadow: 0 0 0 0 rgba(233, 219, 61, 0); }
  50% { box-shadow: 0 0 12px 2px rgba(233, 219, 61, 0.25); }
}

.achievement-card.secret {
  border-style: dashed;
}

.ach-icon {
  width: 40px;
  height: 40px;
  min-width: 40px;
  border-radius: 8px;
  border: 2px solid var(--border-subtle);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  background: var(--bg-elevated, var(--kh-c-neutrals-sat-600));
}

.icon-lock {
  font-size: 14px;
  color: var(--text-dim, var(--kh-c-text-primary-800));
}

.ach-info {
  flex: 1;
  min-width: 0;
}

.ach-name {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 2px;
}

.ach-desc {
  font-size: 11px;
  color: var(--text-muted);
  line-height: 1.4;
}

.ach-progress {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 4px;
}

.ach-unlocked-label {
  font-size: 10px;
  font-weight: 700;
  color: var(--accent-green, #4caf50);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.ach-progress-bar {
  flex: 1;
  max-width: 120px;
  height: 4px;
  background: var(--bg-elevated, var(--kh-c-neutrals-sat-600));
  border-radius: 2px;
  overflow: hidden;
}

.ach-progress-fill {
  height: 100%;
  background: var(--accent-gold);
  border-radius: 2px;
  transition: width 0.4s ease;
}

.ach-progress-text {
  font-size: 10px;
  font-weight: 700;
  font-family: var(--font-mono);
  color: var(--text-dim);
}

.ach-rarity-dot {
  width: 8px;
  height: 8px;
  min-width: 8px;
  border-radius: 50%;
  margin-left: auto;
}
</style>
