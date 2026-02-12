<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import Leaderboard from 'src/components/Leaderboard.vue'

const props = defineProps<{ gameId: string }>()
const store = useGameStore()
const router = useRouter()
const gameIdNum = computed(() => Number(props.gameId))

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
      <Leaderboard :players="store.gameState.players" />
      <div v-if="store.gameState.globalLogs" class="card" style="margin-top: 16px;">
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
  gap: 16px;
  margin-bottom: 24px;
}

.loading {
  text-align: center;
  padding: 80px;
  color: var(--text-muted);
}

.spectate-log {
  font-size: 13px;
  line-height: 1.6;
  color: var(--text-secondary);
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
