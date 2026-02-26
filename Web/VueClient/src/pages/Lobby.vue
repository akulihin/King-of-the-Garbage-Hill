<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import { signalrService, type ReplayListEntry } from 'src/services/signalr'
import AchievementBoard from 'src/components/AchievementBoard.vue'
import LootBox from 'src/components/LootBox.vue'

const API_BASE = import.meta.env.VITE_API_BASE || ''

const store = useGameStore()
const router = useRouter()

const isCreatingGame = ref(false)
const showAchievements = ref(false)
const recentReplays = ref<ReplayListEntry[]>([])

const quests = computed(() => store.questState?.quests ?? [])
const streakDays = computed(() => store.questState?.streakDays ?? 0)
const allDone = computed(() => store.questState?.allCompletedToday ?? false)
const zbsPoints = computed(() => store.questState?.zbsPoints ?? 0)
const pendingLootBoxes = computed(() => store.questState?.pendingLootBoxes ?? 0)
const isOpeningLootBox = ref(false)

async function openLootBox() {
  if (isOpeningLootBox.value || pendingLootBoxes.value <= 0) return
  isOpeningLootBox.value = true
  await store.openLootBox()
  isOpeningLootBox.value = false
}

function dismissLootBox() {
  store.dismissLootBox()
}

let pollInterval: ReturnType<typeof setInterval> | null = null

async function fetchReplays() {
  if (!store.discordId) return
  try {
    const resp = await fetch(`${API_BASE}/api/game/replays?limit=10`, {
      headers: { 'X-Discord-Id': store.discordId },
    })
    if (resp.ok) recentReplays.value = await resp.json()
  } catch { /* ignore */ }
}

onMounted(() => {
  store.refreshLobby()
  if (store.isAuthenticated) {
    store.requestQuests()
    fetchReplays()
  }
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

function viewReplay(hash: string) {
  router.push(`/replay/${hash}`)
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

    <!-- Daily Quests -->
    <div v-if="store.isAuthenticated && quests.length > 0" class="section">
      <div class="section-header">
        <h2 class="section-title">
          Daily Quests
          <span v-if="allDone" class="badge badge-done">Complete!</span>
        </h2>
        <div class="quest-meta">
          <span class="streak-badge" :class="{ active: streakDays > 0 }">
            Streak: {{ streakDays }}/7
          </span>
          <span class="zbs-badge">{{ zbsPoints }} ZBS</span>
        </div>
      </div>

      <div class="quests-grid">
        <div
          v-for="quest in quests"
          :key="quest.id"
          class="quest-card card"
          :class="{ completed: quest.isCompleted }"
        >
          <div class="quest-info">
            <span class="quest-desc">{{ quest.description }}</span>
            <span class="quest-reward">+{{ quest.zbsReward }} ZBS</span>
          </div>
          <div class="quest-progress">
            <div class="progress-bar">
              <div
                class="progress-fill"
                :style="{ width: `${Math.min(100, (quest.current / quest.target) * 100)}%` }"
              />
            </div>
            <span class="progress-text">{{ quest.current }} / {{ quest.target }}</span>
          </div>
        </div>
      </div>

      <div v-if="allDone" class="quest-bonus">
        All quests complete! +25 bonus ZBS
      </div>
      <div v-if="streakDays >= 7 && streakDays % 7 === 0" class="streak-bonus">
        7-day streak! +500 ZBS bonus!
      </div>
    </div>

    <!-- Loot Boxes -->
    <div v-if="store.isAuthenticated && pendingLootBoxes > 0" class="section lootbox-section">
      <button
        class="btn lootbox-btn"
        :class="{ pulse: pendingLootBoxes > 0 }"
        :disabled="isOpeningLootBox"
        @click="openLootBox"
      >
        <span class="lootbox-icon">&#x1F4E6;</span>
        Open Loot Box
        <span class="lootbox-count">{{ pendingLootBoxes }}</span>
      </button>
    </div>

    <!-- Loot Box Result Overlay -->
    <LootBox
      v-if="store.lootBoxResult"
      :result="store.lootBoxResult"
      @dismiss="dismissLootBox()"
    />

    <!-- Achievements Button -->
    <div v-if="store.isAuthenticated" class="section achievements-section">
      <button class="btn btn-ghost achievements-btn" @click="showAchievements = true">
        View Achievements
      </button>
    </div>

    <!-- Achievement Board Overlay -->
    <AchievementBoard v-if="showAchievements" @close="showAchievements = false" />

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

    <!-- Recent Replays (own games only) -->
    <div v-if="store.isAuthenticated && recentReplays.length > 0" class="section">
      <div class="section-header">
        <h2 class="section-title">
          Recent Replays
          <span class="badge">{{ recentReplays.length }}</span>
        </h2>
      </div>

      <div class="games-grid">
        <div
          v-for="replay in recentReplays"
          :key="replay.replayHash"
          class="game-card card replay-card"
          @click="viewReplay(replay.replayHash)"
        >
          <div class="game-card-header">
            <span class="game-id">Game {{ replay.replayHash }}</span>
            <span class="game-mode" :class="replay.gameMode.toLowerCase()">
              {{ replay.gameMode }}
            </span>
          </div>

          <div class="replay-players">
            <div
              v-for="p in replay.players"
              :key="p.discordUsername"
              class="replay-player"
            >
              <img :src="p.characterAvatar" :alt="p.characterName" class="replay-avatar" />
              <span class="replay-player-name">{{ p.discordUsername }}</span>
              <span class="replay-player-place">#{{ p.finalPlace }}</span>
            </div>
          </div>

          <div class="game-card-actions">
            <button class="btn btn-primary" @click.stop="viewReplay(replay.replayHash)">
              Watch Replay
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

/* Quest Widget */
.quest-meta {
  display: flex;
  align-items: center;
  gap: 8px;
}

.streak-badge {
  font-size: 11px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: var(--radius);
  background: var(--bg-card);
  color: var(--text-muted);
  border: 1px solid var(--border-subtle);
}

.streak-badge.active {
  background: var(--kh-c-secondary-warning-500, rgba(255, 170, 0, 0.15));
  color: var(--accent-gold);
  border-color: var(--accent-gold);
}

.zbs-badge {
  font-size: 11px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: var(--radius);
  background: var(--kh-c-secondary-success-500, rgba(0, 200, 100, 0.15));
  color: var(--accent-green);
  border: 1px solid var(--accent-green);
}

.badge-done {
  background: var(--kh-c-secondary-success-500, rgba(0, 200, 100, 0.15));
  color: var(--accent-green);
  border-color: var(--accent-green);
}

.quests-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.quest-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px 14px;
  transition: opacity 0.3s ease;
}

.quest-card.completed {
  opacity: 0.6;
}

.quest-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.quest-desc {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
}

.quest-card.completed .quest-desc {
  text-decoration: line-through;
  color: var(--text-muted);
}

.quest-reward {
  font-size: 11px;
  font-weight: 700;
  color: var(--accent-gold);
  font-family: var(--font-mono);
}

.quest-progress {
  display: flex;
  align-items: center;
  gap: 8px;
}

.progress-bar {
  flex: 1;
  height: 6px;
  background: var(--bg-elevated);
  border-radius: 3px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--accent-gold);
  border-radius: 3px;
  transition: width 0.4s ease;
}

.quest-card.completed .progress-fill {
  background: var(--accent-green);
}

.progress-text {
  font-size: 11px;
  font-weight: 700;
  font-family: var(--font-mono);
  color: var(--text-muted);
  min-width: 40px;
  text-align: right;
}

.quest-bonus, .streak-bonus {
  text-align: center;
  padding: 8px;
  margin-top: 8px;
  border-radius: var(--radius);
  font-size: 12px;
  font-weight: 700;
}

.quest-bonus {
  background: var(--kh-c-secondary-success-500, rgba(0, 200, 100, 0.15));
  color: var(--accent-green);
  border: 1px solid var(--accent-green);
}

.streak-bonus {
  background: var(--kh-c-secondary-warning-500, rgba(255, 170, 0, 0.15));
  color: var(--accent-gold);
  border: 1px solid var(--accent-gold);
  font-size: 14px;
}

/* Loot Box */
.lootbox-section {
  text-align: center;
}

.lootbox-btn {
  font-size: 14px;
  font-weight: 700;
  letter-spacing: 0.5px;
  padding: 10px 28px;
  border: 2px solid var(--accent-gold);
  color: var(--accent-gold);
  background: rgba(233, 219, 61, 0.08);
  border-radius: var(--radius);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  transition: background 0.2s, transform 0.2s;
}

.lootbox-btn:hover {
  background: rgba(233, 219, 61, 0.15);
  transform: scale(1.02);
}

.lootbox-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.lootbox-btn.pulse {
  animation: lootboxPulse 2s ease-in-out infinite;
}

@keyframes lootboxPulse {
  0%, 100% { box-shadow: 0 0 0 0 rgba(233, 219, 61, 0.3); }
  50% { box-shadow: 0 0 12px 4px rgba(233, 219, 61, 0.2); }
}

.lootbox-icon {
  font-size: 18px;
}

.lootbox-count {
  background: var(--accent-gold);
  color: var(--bg-base, #1a1a2e);
  font-size: 11px;
  font-weight: 800;
  padding: 1px 7px;
  border-radius: 10px;
  min-width: 20px;
  text-align: center;
}

/* Achievements */
.achievements-section {
  text-align: center;
}

.achievements-btn {
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  padding: 8px 24px;
  border: 1px solid var(--accent-gold);
  color: var(--accent-gold);
}
.achievements-btn:hover {
  background: rgba(233, 219, 61, 0.1);
}

/* Replay Cards */
.replay-card {
  cursor: pointer;
  transition: border-color 0.2s;
}
.replay-card:hover {
  border-color: var(--accent-purple, #b464ff);
}

.replay-players {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 10px;
}

.replay-player {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.replay-avatar {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  object-fit: cover;
}

.replay-player-name {
  flex: 1;
  color: var(--text-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.replay-player-place {
  font-weight: 700;
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--accent-gold);
}
</style>
