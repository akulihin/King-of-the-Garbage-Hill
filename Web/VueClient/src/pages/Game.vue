<script setup lang="ts">
import { onMounted, onUnmounted, computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import Leaderboard from 'src/components/Leaderboard.vue'
import PlayerCard from 'src/components/PlayerCard.vue'
import ActionPanel from 'src/components/ActionPanel.vue'
import SkillsPanel from 'src/components/SkillsPanel.vue'
import BattleLog from 'src/components/BattleLog.vue'
import FightAnimation from 'src/components/FightAnimation.vue'
import MediaMessages from 'src/components/MediaMessages.vue'
import RoundTimer from 'src/components/RoundTimer.vue'

/** Toggle between current global events and all-game history in the global events panel */
const showAllGlobalLogs = ref(false)

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

function goToLobby() {
  router.push('/')
}

function formatLogs(text: string): string {
  return text
    .replace(/<:[^:]+:(\d+)>/g, '') // strip Discord custom emoji like <:war:123>
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/__(.*?)__/g, '<u>$1</u>')
    .replace(/\*(.*?)\*/g, '<em>$1</em>')
    .replace(/~~(.*?)~~/g, '<del>$1</del>')
    .replace(/\|>Phrase<\|/g, '')
    .replace(/\n/g, '<br>')
}
</script>

<template>
  <div class="game-page">
    <!-- Loading state -->
    <div v-if="!store.gameState" class="loading">
      <p>Connecting to game...</p>
    </div>

    <!-- Active Game (or finished — same layout, just no actions) -->
    <div v-else class="game-layout">
      <!-- Left: Player info panel -->
      <div class="game-left">
        <PlayerCard
          v-if="store.myPlayer"
          :player="store.myPlayer"
          :is-me="true"
        />
      </div>

      <!-- Center: Header + Leaderboard + Actions + Logs -->
      <div class="game-center">
        <!-- Game header bar -->
        <div class="game-header">
          <button class="btn btn-ghost btn-sm" @click="goToLobby">
            ← Lobby
          </button>
          <div class="header-center">
            <span class="round-badge">
              Round {{ store.gameState.roundNo }} / 10
            </span>
            <span class="mode-badge">
              {{ store.gameState.gameMode }}
            </span>
            <span v-if="store.gameState.isFinished" class="finished-badge">
              Finished
            </span>
          </div>
          <RoundTimer v-if="!store.gameState.isFinished" />
        </div>

        <!-- Leaderboard (click to attack only during active game) -->
        <Leaderboard
          :players="store.gameState.players"
          :my-player-id="store.myPlayer?.playerId"
          :can-attack="!store.gameState.isFinished && store.isMyTurn"
          :predictions="store.myPlayer?.predictions"
          :character-names="store.gameState.allCharacterNames || []"
          :character-catalog="store.gameState.allCharacters || []"
          :is-admin="store.isAdmin"
          :round-no="store.gameState.roundNo"
          :confirmed-predict="store.myPlayer?.status.confirmedPredict"
          @attack="store.attack($event)"
          @predict="store.predict($event.playerId, $event.characterName)"
        />

        <!-- Action bar (only during active game) -->
        <ActionPanel v-if="store.myPlayer && !store.gameState.isFinished" />

        <!-- "Back to Lobby" after game ends -->
        <div v-if="store.gameState.isFinished" class="finished-actions">
          <button class="btn btn-primary btn-lg" @click="goToLobby">
            Back to Lobby
          </button>
        </div>

        <!-- Direct Messages (ephemeral alerts) -->
        <div
          v-if="store.myPlayer?.status.directMessages?.length"
          class="direct-messages"
        >
          <div
            v-for="(msg, idx) in store.myPlayer.status.directMessages"
            :key="idx"
            class="dm-item"
            v-html="formatLogs(msg)"
          />
        </div>

        <!-- Character Phrase Media Messages (text, audio, images) -->
        <MediaMessages
          v-if="store.myPlayer?.status.mediaMessages?.length"
          :messages="store.myPlayer.status.mediaMessages"
        />

        <!-- Logs: top row (personal) + bottom row (global + fight animation) -->
        <div class="logs-grid">
          <!-- Row 1: personal logs -->
          <div class="log-panel card">
            <div class="card-header">События этого раунда</div>
            <div
              v-if="store.myPlayer?.status.personalLogs"
              class="log-content"
              v-html="formatLogs(store.myPlayer.status.personalLogs)"
            />
            <div v-else class="log-empty">Еще ничего не произошло. Наверное...</div>
          </div>
          <div class="log-panel card">
            <div class="card-header">События прошлого раунда</div>
            <div
              v-if="store.myPlayer?.status.previousRoundLogs"
              class="log-content"
              v-html="formatLogs(store.myPlayer.status.previousRoundLogs)"
            />
            <div v-else class="log-empty">В прошлом раунде ничего не произошло.</div>
          </div>

          <!-- Row 2: global events (with toggle) + fight animation -->
          <div class="log-panel card">
            <div class="card-header global-header">
              <span>Глобальные события</span>
              <button
                class="toggle-btn"
                @click="showAllGlobalLogs = !showAllGlobalLogs"
              >
                {{ showAllGlobalLogs ? '← Текущие' : 'Вся история →' }}
              </button>
            </div>
            <BattleLog
              v-if="!showAllGlobalLogs"
              :logs="store.gameState.globalLogs || ''"
              :hide-battles="true"
            />
            <BattleLog
              v-else
              :logs="store.gameState.allGlobalLogs || ''"
            />
          </div>
          <div class="log-panel card fight-panel">
            <div class="card-header">Бои раунда</div>
            <FightAnimation :fights="store.gameState.fightLog || []" />
          </div>
        </div>
      </div>

      <!-- Right: Skills / Passives -->
      <div class="game-right">
        <SkillsPanel v-if="store.myPlayer" :player="store.myPlayer" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.game-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.loading {
  text-align: center;
  padding: 80px;
  color: var(--text-muted);
  font-size: 18px;
}

.finished-badge {
  background: var(--accent-gold);
  color: var(--bg-primary);
  padding: 4px 14px;
  border-radius: 12px;
  font-size: 13px;
  font-weight: 700;
  text-transform: uppercase;
}

.finished-actions {
  display: flex;
  justify-content: center;
  padding: 16px 0;
}

/* ── 3-column layout ────────────────────────────────────────────── */
.game-layout {
  display: grid;
  grid-template-columns: 260px 1fr 260px;
  gap: 16px;
  align-items: start;
}

@media (max-width: 1200px) {
  .game-layout {
    grid-template-columns: 1fr;
  }
  .game-right { order: -1; }
}

/* ── Header ─────────────────────────────────────────────────────── */
.game-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.header-center {
  display: flex;
  align-items: center;
  gap: 8px;
}

.round-badge {
  background: var(--accent-blue);
  color: white;
  padding: 4px 14px;
  border-radius: 12px;
  font-size: 13px;
  font-weight: 600;
}

.mode-badge {
  background: var(--accent-purple);
  color: white;
  padding: 4px 14px;
  border-radius: 12px;
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
}

/* ── Direct Messages ──────────────────────────────────────────────── */
.direct-messages {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 10px;
}

.dm-item {
  padding: 8px 14px;
  background: var(--kh-c-neutrals-pale-300, var(--bg-card));
  border-left: 3px solid var(--accent-orange);
  border-radius: 0 var(--radius) var(--radius) 0;
  font-size: 13px;
  color: var(--text-primary);
  line-height: 1.5;
}

.dm-item :deep(strong) { color: var(--accent-gold); }
.dm-item :deep(em) { color: var(--accent-blue); }

/* ── Logs ────────────────────────────────────────────────────────── */
.logs-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-top: 12px;
}

@media (max-width: 800px) {
  .logs-grid { grid-template-columns: 1fr; }
}

.log-panel {
  max-height: 280px;
  display: flex;
  flex-direction: column;
}

.fight-panel {
  max-height: 420px;
}

.global-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.toggle-btn {
  background: var(--accent-blue);
  color: white;
  border: none;
  border-radius: 6px;
  padding: 2px 10px;
  font-size: 11px;
  cursor: pointer;
  font-weight: 600;
  transition: background 0.15s;
}
.toggle-btn:hover {
  background: var(--accent-purple);
}

.log-content {
  font-size: 12px;
  line-height: 1.6;
  color: var(--text-secondary);
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  background: var(--bg-primary);
  border-radius: var(--radius);
  font-family: var(--font-mono);
}

.log-content :deep(strong) { color: var(--accent-gold); }
.log-content :deep(em) { color: var(--accent-blue); }
.log-content :deep(u) { color: var(--accent-green); }
.log-content :deep(del) { color: var(--text-muted); text-decoration: line-through; }

.log-empty {
  color: var(--text-muted);
  font-style: italic;
  padding: 16px;
  text-align: center;
  font-size: 13px;
}
</style>
