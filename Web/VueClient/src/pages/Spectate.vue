<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import Leaderboard from 'src/components/Leaderboard.vue'
import FightAnimation from 'src/components/FightAnimation.vue'
import BattleLog from 'src/components/BattleLog.vue'
import EventToast from 'src/components/EventToast.vue'

const props = defineProps<{ gameId: string }>()
const store = useGameStore()
const router = useRouter()
const gameIdNum = computed(() => Number(props.gameId))

const commentary = computed(() => {
  if (!store.gameState) return 'Connecting...'
  if (store.gameState.isFinished) return 'Game Over!'
  return `Round ${store.gameState.roundNo} / 10 — ${store.gameState.players.length} players`
})

const playerCount = computed(() => store.gameState?.players.length ?? 0)

/** Full chronicle for spectators */
const letopis = computed(() => {
  const chronicle = store.gameState?.fullChronicle
  if (chronicle) return chronicle
  return store.gameState?.allGlobalLogs || ''
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
        &larr; Lobby
      </button>
      <h2>
        Spectating Game #{{ gameId }}
      </h2>
      <span class="spectate-watching">
        <span class="watching-dot" />
        {{ playerCount }} players
      </span>
    </div>

    <div v-if="!store.gameState" class="loading">
      Connecting...
    </div>
    <div v-else class="spectate-layout">
      <div class="spectate-commentary">
        <span class="commentary-text">{{ commentary }}</span>
        <span v-if="store.gameState.isFinished" class="commentary-finished">FINISHED</span>
      </div>

      <!-- Fight Animation (read-only, auto-playing) -->
      <div v-if="store.gameState.fightLog?.length" class="spectate-fight card">
        <FightAnimation
          :fights="store.gameState.fightLog"
          :letopis="letopis"
          :game-story="store.gameStory"
          :players="store.gameState.players"
          :is-admin="true"
          :character-catalog="store.gameState.allCharacters || []"
        />
      </div>

      <!-- Leaderboard with all visual effects -->
      <div class="spectate-board card">
        <Leaderboard
          :players="store.gameState.players"
          :character-names="store.gameState.allCharacterNames || []"
          :character-catalog="store.gameState.allCharacters || []"
          :is-admin="true"
          :is-finished="store.gameState.isFinished"
          :round-no="store.gameState.roundNo"
          :fight-log="store.gameState.fightLog || []"
        />
      </div>

      <!-- Styled Battle Log instead of raw pre -->
      <div v-if="store.gameState.globalLogs" class="spectate-log-card card">
        <div class="card-header">
          Game Log
        </div>
        <BattleLog :logs="store.gameState.globalLogs" />
      </div>
    </div>

    <!-- Event Toasts for spectators too -->
    <EventToast
      v-if="store.gameState"
      :global-logs="store.gameState.globalLogs || ''"
      :players="store.gameState.players"
    />
  </div>
</template>

<style scoped>
.spectate-page {
  max-width: 900px;
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
  flex: 1;
}

.spectate-watching {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  padding: 4px 10px;
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: 20px;
  white-space: nowrap;
}

.watching-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--accent-red);
  box-shadow: 0 0 6px rgba(239, 128, 128, 0.5);
  animation: watching-pulse 2s ease-in-out infinite;
}

@keyframes watching-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.loading {
  text-align: center;
  padding: 60px;
  color: var(--text-muted);
  font-size: 13px;
}

.spectate-layout {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* Auto-Commentary Bar */
.spectate-commentary {
  padding: 8px 16px;
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius);
  text-align: center;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
}
.commentary-text {
  font-size: 12px;
  font-weight: 700;
  color: var(--accent-gold);
  letter-spacing: 0.5px;
  text-transform: uppercase;
}
.commentary-finished {
  font-size: 10px;
  font-weight: 800;
  color: var(--accent-green);
  padding: 2px 8px;
  background: rgba(63, 167, 61, 0.12);
  border-radius: 4px;
  letter-spacing: 0.5px;
}

/* Fight panel */
.spectate-fight {
  padding: 8px;
}

/* Glassmorphism Board Wrapper */
.spectate-board {
  /* inherits card styling */
}

/* Glassmorphism Log Card */
.spectate-log-card {
  /* inherits card styling */
}

/* Mobile responsive */
@media (max-width: 768px) {
  .spectate-page {
    max-width: 100%;
  }
  .spectate-header {
    flex-wrap: wrap;
    gap: 8px;
  }
  .spectate-header h2 {
    font-size: 13px;
  }
  .spectate-fight {
    padding: 4px;
  }
}
</style>
