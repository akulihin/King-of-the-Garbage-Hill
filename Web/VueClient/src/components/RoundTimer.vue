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
const isCritical = computed(() => store.roundTimeLeft < 15)
</script>

<template>
  <div class="round-timer" :class="{ urgent: isUrgent, critical: isCritical }">
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
  min-width: 110px;
}

.timer-bar {
  flex: 1;
  height: 6px;
  background: var(--bg-inset);
  border-radius: 3px;
  overflow: hidden;
  box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.3);
}

.timer-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--accent-blue), rgba(110, 170, 240, 0.7));
  border-radius: 3px;
  transition: width 1s linear;
  box-shadow: 0 0 6px rgba(110, 170, 240, 0.2);
  position: relative;
}

/* Shine effect on the fill bar */
.timer-fill::after {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 50%;
  background: linear-gradient(180deg, rgba(255,255,255,0.2), transparent);
  border-radius: 3px 3px 0 0;
}

/* 4C. Glowing leading edge */
.timer-fill::before {
  content: '';
  position: absolute;
  right: -1px;
  top: -2px;
  bottom: -2px;
  width: 8px;
  background: radial-gradient(ellipse at right center, rgba(110, 170, 240, 0.7), transparent 70%);
  border-radius: 0 3px 3px 0;
  filter: blur(2px);
  z-index: 1;
}

/* Red glow variant for urgent state */
.urgent .timer-fill::before {
  background: radial-gradient(ellipse at right center, rgba(239, 128, 128, 0.8), transparent 70%);
}
.critical .timer-fill::before {
  background: radial-gradient(ellipse at right center, rgba(239, 128, 128, 1), transparent 70%);
  width: 12px;
}

.urgent .timer-fill {
  background: linear-gradient(90deg, var(--accent-red), rgba(239, 128, 128, 0.7));
  box-shadow: 0 0 8px rgba(239, 128, 128, 0.3);
  animation: timer-pulse 1s ease-in-out infinite;
}

.critical .timer-fill {
  background: linear-gradient(90deg, var(--accent-red), rgba(239, 128, 128, 0.9));
  box-shadow: 0 0 12px rgba(239, 128, 128, 0.5);
  animation: timer-pulse 0.5s ease-in-out infinite;
}

.timer-text {
  font-family: var(--font-mono);
  font-weight: 800;
  font-size: 14px;
  min-width: 44px;
  text-align: right;
  color: var(--text-primary);
  transition: all 0.3s;
}

.urgent .timer-text {
  color: var(--accent-red);
  text-shadow: 0 0 8px rgba(239, 128, 128, 0.3);
}

.critical .timer-text {
  color: var(--accent-red);
  font-size: 17px;
  text-shadow: 0 0 12px rgba(239, 128, 128, 0.6), 0 0 24px rgba(239, 128, 128, 0.2);
  animation: timer-text-pulse 0.5s ease-in-out infinite;
}

@keyframes timer-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

@keyframes timer-text-pulse {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.1); }
}
</style>
