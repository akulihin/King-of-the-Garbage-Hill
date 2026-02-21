<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import { signalrService } from 'src/services/signalr'

const store = useGameStore()
const router = useRouter()

const isCreatingGame = ref(false)

let pollInterval: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  store.refreshLobby()
  pollInterval = setInterval(() => {
    if (store.isConnected) store.refreshLobby()
  }, 3000)

  signalrService.onGameCreated = (data) => {
    isCreatingGame.value = false
    router.push(`/game/${data.gameId}`)
  }
  signalrService.onGameJoined = (data) => {
    router.push(`/game/${data.gameId}`)
  }
})

onUnmounted(() => {
  if (pollInterval) clearInterval(pollInterval)
  signalrService.onGameCreated = null
  signalrService.onGameJoined = null
})

async function createGame() {
  isCreatingGame.value = true
  await store.createWebGame()
}

async function handleJoinGame(gameId: number) {
  await store.joinWebGame(gameId)
}

function viewGame(gameId: number) {
  router.push(`/game/${gameId}`)
}

function spectateGame(gameId: number) {
  router.push(`/spectate/${gameId}`)
}
</script>

<template>
  <div class="lobby">
    <div class="lobby-header">
      <h1>Game Lobby</h1>
      <p class="subtitle">
        Create a new game, join an existing one, or spectate ongoing games.
      </p>
    </div>

    <!-- Active Games -->
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">
          Active Games
          <span v-if="store.lobbyState" class="badge">{{ store.lobbyState.activeGames }}</span>
        </h2>
        <button
          v-if="store.isAuthenticated"
          class="btn btn-primary btn-sm"
          :disabled="isCreatingGame"
          @click="createGame"
        >
          {{ isCreatingGame ? 'Creating...' : '+ New Game' }}
        </button>
      </div>

      <div v-if="!store.lobbyState || store.lobbyState.games.length === 0" class="empty-state">
        <p>No active games right now.</p>
        <p class="hint">
          Create a new game above or start one in Discord with <code>*st</code>!
        </p>
      </div>

      <div v-else class="games-grid">
        <div
          v-for="game in store.lobbyState.games"
          :key="game.gameId"
          class="game-card card"
        >
          <div class="game-card-header">
            <span class="game-id">Game #{{ game.gameId }}</span>
            <span class="game-mode" :class="game.gameMode.toLowerCase()">
              {{ game.gameMode }}
            </span>
          </div>

          <div class="game-card-stats">
            <div class="stat-row">
              <span class="stat-label">Round</span>
              <span class="stat-value">{{ game.roundNo }} / 10</span>
            </div>
            <div class="stat-row">
              <span class="stat-label">Players</span>
              <span class="stat-value">{{ game.humanCount }} / {{ game.playerCount }}</span>
            </div>
            <div v-if="game.botCount > 0" class="stat-row">
              <span class="stat-label">Bots</span>
              <span class="stat-value">{{ game.botCount }}</span>
            </div>
            <div class="stat-row">
              <span class="stat-label">Status</span>
              <span class="stat-value" :class="{ finished: game.isFinished }">
                {{ game.isFinished ? 'Finished' : 'In Progress' }}
              </span>
            </div>
          </div>

          <div class="game-card-actions">
            <button
              v-if="game.canJoin && store.isAuthenticated"
              class="btn btn-primary"
              @click="handleJoinGame(game.gameId)"
            >
              Join
            </button>
            <button
              v-else
              class="btn btn-primary"
              @click="viewGame(game.gameId)"
            >
              View
            </button>
            <button class="btn btn-ghost" @click="spectateGame(game.gameId)">
              Spectate
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- How to Play -->
    <div class="section">
      <h2 class="section-title">
        How to Play
      </h2>
      <div class="rules-grid">
        <div class="rule-card card">
          <div class="rule-icon"><span class="gi gi-xl gi-str">ATK</span></div>
          <h3>Attack</h3>
          <p>Choose a player on the leaderboard to attack. Win fights to earn points.</p>
        </div>
        <div class="rule-card card">
          <div class="rule-icon"><span class="gi gi-xl gi-def">DEF</span></div>
          <h3>Block</h3>
          <p>Skip your attack to defend. No fights this round, but no risk either.</p>
        </div>
        <div class="rule-card card">
          <div class="rule-icon"><span class="gi gi-xl gi-rnd">LVL</span></div>
          <h3>Level Up</h3>
          <p>Spend level-up points on Intelligence, Strength, Speed, or Psyche.</p>
        </div>
        <div class="rule-card card">
          <div class="rule-icon"><span class="gi gi-xl gi-psy">PSY</span></div>
          <h3>Predict</h3>
          <p>Guess which character each player is to earn bonus points.</p>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.lobby {
  max-width: 960px;
  margin: 0 auto;
}

.lobby-header {
  text-align: center;
  margin-bottom: 32px;
}

.lobby-header h1 {
  font-size: 28px;
  font-weight: 800;
  color: var(--accent-gold);
  letter-spacing: 1px;
  margin-bottom: 6px;
}

.subtitle {
  color: var(--text-muted);
  font-size: 13px;
}

.subtitle code {
  background: var(--bg-card);
  padding: 2px 6px;
  border-radius: 4px;
  color: var(--accent-gold);
  font-family: var(--font-mono);
  font-size: 12px;
}

.section {
  margin-bottom: 32px;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.section-title {
  font-size: 16px;
  font-weight: 800;
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--text-primary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin: 0;
}

.badge {
  background: var(--kh-c-secondary-info-500);
  color: var(--text-primary);
  padding: 2px 8px;
  border-radius: var(--radius);
  font-size: 11px;
  font-weight: 700;
  border: 1px solid var(--accent-blue);
}

.empty-state {
  text-align: center;
  padding: 40px;
  color: var(--text-muted);
  font-size: 13px;
}

.empty-state .hint {
  margin-top: 6px;
  font-size: 12px;
  color: var(--text-dim);
}

.games-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 12px;
}

.game-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border-subtle);
}

.game-id {
  font-weight: 800;
  font-size: 15px;
  color: var(--text-primary);
}

.game-mode {
  padding: 2px 10px;
  border-radius: var(--radius);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.game-mode.normal { background: var(--kh-c-secondary-info-500); color: var(--text-primary); border: 1px solid var(--accent-blue); }
.game-mode.aram { background: var(--kh-c-secondary-purple-500); color: var(--text-primary); border: 1px solid var(--accent-purple); }
.game-mode.team { background: var(--kh-c-secondary-success-500); color: var(--text-primary); border: 1px solid var(--accent-green); }

.game-card-stats {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 12px;
}

.stat-row {
  display: flex;
  justify-content: space-between;
}

.stat-label {
  color: var(--text-muted);
  font-size: 12px;
}

.stat-value {
  font-weight: 700;
  font-size: 12px;
  font-family: var(--font-mono);
}

.stat-value.finished {
  color: var(--accent-green);
}

.game-card-actions {
  display: flex;
  gap: 6px;
}

.game-card-actions .btn {
  flex: 1;
}

.rules-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 10px;
}

.rule-card {
  text-align: center;
  padding: 14px;
}

.rule-icon {
  font-size: 28px;
  margin-bottom: 8px;
}

.rule-card h3 {
  margin-bottom: 6px;
  color: var(--accent-gold);
  font-size: 13px;
  font-weight: 800;
}

.rule-card p {
  color: var(--text-secondary);
  font-size: 12px;
  line-height: 1.5;
}
</style>
