<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useGameStore } from 'src/store/game'

const store = useGameStore()
const now = ref(Date.now())
let timerInterval: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  timerInterval = setInterval(() => {
    now.value = Date.now()
  }, 1000)
})

onUnmounted(() => {
  if (timerInterval) clearInterval(timerInterval)
})

const timeLeft = computed(() => {
  const seconds = Math.max(0, Math.floor(store.roundTimeLeft))
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${s.toString().padStart(2, '0')}`
})

const progress = computed(() => {
  if (!store.gameState) return 100
  const total = store.gameState.turnLengthInSecond
  const left = store.roundTimeLeft
  return Math.max(0, Math.min(100, (left / total) * 100))
})

const isUrgent = computed(() => store.roundTimeLeft < 30)
</script>

<template>
  <div class="round-timer" :class="{ urgent: isUrgent }">
    <div class="timer-bar">
      <div class="timer-fill" :style="{ width: `${progress}%` }" />
    </div>
    <span class="timer-text">{{ timeLeft }}</span>
  </div>
</template>

<style scoped>
.round-timer {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 120px;
}

.timer-bar {
  flex: 1;
  height: 6px;
  background: var(--bg-primary);
  border-radius: 3px;
  overflow: hidden;
}

.timer-fill {
  height: 100%;
  background: var(--accent-blue);
  border-radius: 3px;
  transition: width 1s linear;
}

.urgent .timer-fill {
  background: var(--accent-red);
  animation: pulse 1s ease-in-out infinite;
}

.timer-text {
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 16px;
  min-width: 50px;
  text-align: right;
}

.urgent .timer-text {
  color: var(--accent-red);
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}
</style>
