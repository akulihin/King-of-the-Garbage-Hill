<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import Leaderboard from 'src/components/Leaderboard.vue'

const props = defineProps<{ gameId: string }>()
const store = useGameStore()
const router = useRouter()
const gameIdNum = computed(() => Number(props.gameId))

const commentary = computed(() => {
  if (!store.gameState) return 'Connecting...'
  if (store.gameState.isFinished) return 'Game Over!'
  return `Round ${store.gameState.roundNo} / 10 — ${store.gameState.players.length} players`
})

onMounted(async () => {
  if (store.isConnected) {
    await store.joinGame(gameIdNum.value)
  }
})

onUnmounted(() => {
  if (store.isConnected && gameIdNum.value) {
    store.leaveGame(gameIdNum.value)
  }
})
</script>

<template>
  <div class="spectate-page">
    <div class="spectate-header">
      <button class="btn btn-ghost btn-sm" @click="router.push('/')">
        ← Lobby
      </button>
      <h2>
        Spectating Game #{{ gameId }}
      </h2>
    </div>

    <div v-if="!store.gameState" class="loading">
      Connecting...
    </div>
    <div v-else>
      <div class="spectate-commentary">
        <span class="commentary-text">{{ commentary }}</span>
      </div>

      <div class="spectate-board card">
        <Leaderboard :players="store.gameState.players" />
      </div>

      <div v-if="store.gameState.globalLogs" class="spectate-log-card card">
        <div class="card-header">
          Game Log
        </div>
        <pre class="spectate-log">{{ store.gameState.globalLogs }}</pre>
      </div>
    </div>
  </div>
</template>

<style scoped>
.spectate-page {
  max-width: 800px;
  margin: 0 auto;
}

.spectate-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.spectate-header h2 {
  font-size: 15px;
  font-weight: 800;
  color: var(--text-primary);
}

.loading {
  text-align: center;
  padding: 60px;
  color: var(--text-muted);
  font-size: 13px;
}

/* Auto-Commentary Bar */
.spectate-commentary {
  padding: 8px 16px;
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius);
  margin-bottom: 12px;
  text-align: center;
}
.commentary-text {
  font-size: 12px;
  font-weight: 700;
  color: var(--accent-gold);
  letter-spacing: 0.5px;
  text-transform: uppercase;
}

/* Glassmorphism Board Wrapper */
.spectate-board {
  margin-bottom: 16px;
}

/* Glassmorphism Log Card */
.spectate-log-card {
  margin-top: 0;
}

.spectate-log {
  font-size: 11px;
  line-height: 1.6;
  color: var(--text-secondary);
  white-space: pre-wrap;
  word-break: break-word;
  font-family: var(--font-mono);
  background: var(--bg-inset);
  padding: 8px;
  border-radius: var(--radius);
  border: 1px solid var(--border-subtle);
}
</style>
