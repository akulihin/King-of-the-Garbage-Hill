<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { AchievementEntry } from 'src/services/signalr'

const props = defineProps<{
  achievements: AchievementEntry[]
}>()

const emit = defineEmits<{ dismiss: [] }>()

const currentIndex = ref(0)

const current = computed(() => props.achievements[currentIndex.value] ?? null)
const hasNext = computed(() => currentIndex.value < props.achievements.length - 1)
const total = computed(() => props.achievements.length)

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

function next() {
  if (hasNext.value) {
    currentIndex.value++
  } else {
    emit('dismiss')
  }
}

// Reset index when achievements change
watch(() => props.achievements, () => {
  currentIndex.value = 0
})
</script>

<template>
  <Transition name="popup">
    <div v-if="current" class="ach-popup-overlay" @click.self="emit('dismiss')">
      <div class="ach-popup" :key="current.id">
        <div class="ach-popup-glow" :style="{ background: rarityColor(current.rarity) }" />
        <div class="ach-popup-content">
          <div class="ach-popup-label">Achievement Unlocked!</div>
          <div class="ach-popup-icon" :style="{ borderColor: rarityColor(current.rarity) }">
            {{ current.icon }}
          </div>
          <div class="ach-popup-name" :style="{ color: rarityColor(current.rarity) }">
            {{ current.name }}
          </div>
          <div class="ach-popup-desc">{{ current.description }}</div>
          <div class="ach-popup-rarity" :style="{ color: rarityColor(current.rarity) }">
            {{ current.rarity.toUpperCase() }}
          </div>
          <div class="ach-popup-counter" v-if="total > 1">
            {{ currentIndex + 1 }} / {{ total }}
          </div>
          <button class="btn btn-primary ach-popup-btn" @click="next">
            {{ hasNext ? 'Next' : 'Close' }}
          </button>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.ach-popup-overlay {
  position: fixed;
  inset: 0;
  z-index: 950;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(3px);
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

.ach-popup {
  position: relative;
  width: 300px;
  padding: 28px 24px;
  border-radius: 16px;
  text-align: center;
  background: var(--bg-card, var(--kh-c-neutrals-sat-650));
  border: 2px solid var(--border-subtle);
  animation: popIn 0.5s cubic-bezier(0.34, 1.56, 0.64, 1);
  overflow: hidden;
}

@keyframes popIn {
  from { transform: scale(0.6) translateY(20px); opacity: 0; }
  to { transform: scale(1) translateY(0); opacity: 1; }
}

.ach-popup-glow {
  position: absolute;
  inset: -50px;
  border-radius: 50%;
  opacity: 0.1;
  filter: blur(50px);
  animation: glowPulse 2s ease-in-out infinite;
  pointer-events: none;
}

@keyframes glowPulse {
  0%, 100% { opacity: 0.08; transform: scale(0.95); }
  50% { opacity: 0.2; transform: scale(1.05); }
}

.ach-popup-content {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.ach-popup-label {
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 2px;
  color: var(--accent-gold);
  animation: slideDown 0.4s ease 0.1s both;
}

@keyframes slideDown {
  from { opacity: 0; transform: translateY(-10px); }
  to { opacity: 1; transform: translateY(0); }
}

.ach-popup-icon {
  width: 56px;
  height: 56px;
  border-radius: 12px;
  border: 3px solid;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  background: var(--bg-elevated, var(--kh-c-neutrals-sat-600));
  animation: iconBounce 0.6s ease 0.3s both;
}

@keyframes iconBounce {
  0% { transform: scale(0) rotate(-15deg); }
  60% { transform: scale(1.15) rotate(3deg); }
  100% { transform: scale(1) rotate(0deg); }
}

.ach-popup-name {
  font-size: 16px;
  font-weight: 800;
  animation: revealText 0.4s ease 0.5s both;
}

.ach-popup-desc {
  font-size: 12px;
  color: var(--text-muted);
  line-height: 1.4;
  max-width: 240px;
  animation: revealText 0.4s ease 0.6s both;
}

.ach-popup-rarity {
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 2px;
  animation: revealText 0.4s ease 0.7s both;
}

.ach-popup-counter {
  font-size: 11px;
  font-weight: 700;
  color: var(--text-dim);
  font-family: var(--font-mono);
  animation: revealText 0.3s ease 0.7s both;
}

.ach-popup-btn {
  margin-top: 4px;
  padding: 8px 28px;
  font-size: 13px;
  animation: revealText 0.4s ease 0.8s both;
}

@keyframes revealText {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

.popup-enter-active { animation: fadeIn 0.3s ease; }
.popup-leave-active { animation: fadeIn 0.3s ease reverse; }
</style>
