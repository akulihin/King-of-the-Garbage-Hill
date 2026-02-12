<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'

const store = useGameStore()
const router = useRouter()

let pollInterval: ReturnType<typeof setInterval> | null = null

onMounted(() => {
  store.refreshLobby()
  pollInterval = setInterval(() => {
    if (store.isConnected) store.refreshLobby()
  }, 3000)
})

onUnmounted(() => {
  if (pollInterval) clearInterval(pollInterval)
})

function joinGame(gameId: number) {
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
        Start a game from Discord with <code>*st</code> and play it here, or spectate ongoing games.
      </p>
    </div>

    <!-- Active Games -->
    <div class="section">
      <h2 class="section-title">
        Active Games
        <span v-if="store.lobbyState" class="badge">{{ store.lobbyState.activeGames }}</span>
      </h2>

      <div v-if="!store.lobbyState || store.lobbyState.games.length === 0" class="empty-state">
        <p>No active games right now.</p>
        <p class="hint">
          Start a game in Discord with <code>*st</code> command!
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
            <div class="stat-row">
              <span class="stat-label">Status</span>
              <span class="stat-value" :class="{ finished: game.isFinished }">
                {{ game.isFinished ? 'Finished' : 'In Progress' }}
              </span>
            </div>
          </div>

          <div class="game-card-actions">
            <button class="btn btn-primary" @click="joinGame(game.gameId)">
              Join
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
          <div class="rule-icon">
            ⚔️
          </div>
          <h3>Attack</h3>
          <p>Choose a player on the leaderboard to attack. Win fights to earn points.</p>
        </div>
        <div class="rule-card card">
          <div class="rule-icon">
            🛡️
          </div>
          <h3>Block</h3>
          <p>Skip your attack to defend. No fights this round, but no risk either.</p>
        </div>
        <div class="rule-card card">
          <div class="rule-icon">
            📈
          </div>
          <h3>Level Up</h3>
          <p>Spend level-up points on Intelligence, Strength, Speed, or Psyche.</p>
        </div>
        <div class="rule-card card">
          <div class="rule-icon">
            🔮
          </div>
          <h3>Predict</h3>
          <p>Guess which character each player is to earn bonus points.</p>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.lobby {
  max-width: 1000px;
  margin: 0 auto;
}

.lobby-header {
  text-align: center;
  margin-bottom: 40px;
}

.lobby-header h1 {
  font-size: 36px;
  background: linear-gradient(135deg, var(--accent-gold), var(--accent-orange));
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  margin-bottom: 8px;
}

.subtitle {
  color: var(--text-secondary);
  font-size: 16px;
}

.subtitle code {
  background: var(--bg-card);
  padding: 2px 8px;
  border-radius: 4px;
  color: var(--accent-gold);
}

.section {
  margin-bottom: 40px;
}

.section-title {
  font-size: 20px;
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.badge {
  background: var(--accent-blue);
  color: white;
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 14px;
}

.empty-state {
  text-align: center;
  padding: 40px;
  color: var(--text-muted);
}

.empty-state .hint {
  margin-top: 8px;
  font-size: 14px;
}

.games-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.game-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.game-id {
  font-weight: 700;
  font-size: 18px;
}

.game-mode {
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
}

.game-mode.normal { background: var(--accent-blue); color: white; }
.game-mode.aram { background: var(--accent-purple); color: white; }
.game-mode.team { background: var(--accent-green); color: #1a1a2e; }

.game-card-stats {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 16px;
}

.stat-row {
  display: flex;
  justify-content: space-between;
}

.stat-label {
  color: var(--text-muted);
  font-size: 14px;
}

.stat-value {
  font-weight: 600;
  font-size: 14px;
}

.stat-value.finished {
  color: var(--accent-green);
}

.game-card-actions {
  display: flex;
  gap: 8px;
}

.game-card-actions .btn {
  flex: 1;
}

.rules-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 16px;
}

.rule-card {
  text-align: center;
}

.rule-icon {
  font-size: 36px;
  margin-bottom: 12px;
}

.rule-card h3 {
  margin-bottom: 8px;
  color: var(--accent-gold);
}

.rule-card p {
  color: var(--text-secondary);
  font-size: 14px;
  line-height: 1.5;
}
</style>
