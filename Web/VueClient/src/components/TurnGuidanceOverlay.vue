<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue'

type TurnInterference = 'none' | 'self' | 'enemy'

const props = defineProps<{
  interference: TurnInterference
  dimmed: boolean
  enemyEventKey: string | null
}>()
const emit = defineEmits<{
  enemyIntroActive: [active: boolean]
}>()

const enemyMessageVisible = ref(false)
let enemyMessageTimer: ReturnType<typeof setTimeout> | null = null

function clearEnemyMessageTimer() {
  if (!enemyMessageTimer) return
  clearTimeout(enemyMessageTimer)
  enemyMessageTimer = null
}

watch(() => props.enemyEventKey, (eventKey) => {
  clearEnemyMessageTimer()
  if (!eventKey) {
    enemyMessageVisible.value = false
    emit('enemyIntroActive', false)
    return
  }

  enemyMessageVisible.value = true
  emit('enemyIntroActive', true)
  enemyMessageTimer = setTimeout(() => {
    enemyMessageVisible.value = false
    enemyMessageTimer = null
    emit('enemyIntroActive', false)
  }, 5000)
}, { immediate: true })

onUnmounted(clearEnemyMessageTimer)
</script>

<template>
  <Teleport to="body">
    <div
      v-if="interference !== 'none'"
      class="turn-interference-vignette"
      :class="`turn-interference-vignette--${interference}`"
      aria-hidden="true"
    />
    <div v-if="dimmed && !enemyMessageVisible" class="turn-guidance-dim" aria-hidden="true" />
    <div
      v-if="enemyMessageVisible"
      class="enemy-interference-message"
      role="status"
      aria-live="assertive"
    >
      Вражеское воздействие...
    </div>
  </Teleport>
</template>

<style>
.turn-interference-vignette,
.turn-guidance-dim {
  position: fixed;
  inset: 0;
  pointer-events: none;
}

.turn-interference-vignette {
  z-index: 260;
}

.turn-interference-vignette--self {
  background:
    radial-gradient(
      ellipse at center,
      transparent 42%,
      rgba(255, 255, 255, 0.08) 68%,
      rgba(255, 255, 255, 0.32) 100%
    );
  box-shadow: inset 0 0 70px rgba(255, 255, 255, 0.28);
}

.turn-interference-vignette--enemy {
  background:
    radial-gradient(
      ellipse at center,
      transparent 36%,
      rgba(114, 52, 190, 0.13) 63%,
      rgba(106, 34, 184, 0.55) 100%
    );
  box-shadow:
    inset 0 0 90px rgba(104, 34, 180, 0.55),
    inset 0 0 180px rgba(73, 22, 128, 0.2);
}

.turn-guidance-dim {
  z-index: 261;
  background: rgba(3, 5, 10, 0.12);
}

.enemy-interference-message {
  position: fixed;
  z-index: 262;
  top: 17vh;
  left: 50%;
  max-width: min(92vw, 720px);
  padding: 10px 24px;
  color: #eadcff;
  font: 900 clamp(22px, 4.2vw, 48px)/1.08 var(--font-display, sans-serif);
  letter-spacing: 0.035em;
  text-align: center;
  text-shadow:
    0 0 9px rgba(210, 175, 255, 0.85),
    0 0 26px rgba(139, 76, 225, 0.72),
    0 4px 18px rgba(20, 4, 38, 0.82);
  pointer-events: none;
  transform-origin: 50% 80%;
  animation: enemy-interference-sway 2.15s ease-in-out infinite;
}

@keyframes enemy-interference-sway {
  0%, 100% { transform: translateX(-50%) translateY(0) rotate(-1deg); }
  25% { transform: translateX(calc(-50% - 5px)) translateY(-5px) rotate(0.75deg); }
  55% { transform: translateX(calc(-50% + 6px)) translateY(2px) rotate(-0.35deg); }
  78% { transform: translateX(calc(-50% + 2px)) translateY(-3px) rotate(0.9deg); }
}

@media (prefers-reduced-motion: reduce) {
  .enemy-interference-message {
    animation: none;
    transform: translateX(-50%);
  }
}
</style>
