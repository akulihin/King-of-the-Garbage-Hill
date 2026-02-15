<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import Leaderboard from 'src/components/Leaderboard.vue'
import PlayerCard from 'src/components/PlayerCard.vue'
import ActionPanel from 'src/components/ActionPanel.vue'
import SkillsPanel from 'src/components/SkillsPanel.vue'
import FightAnimation from 'src/components/FightAnimation.vue'
import MediaMessages from 'src/components/MediaMessages.vue'
import RoundTimer from 'src/components/RoundTimer.vue'

const props = defineProps<{ gameId: string }>()
const store = useGameStore()
const router = useRouter()

const gameIdNum = computed(() => Number(props.gameId))

/** Stats currently flashing in PlayerCard due to resist damage */
const resistFlashStats = ref<string[]>([])
function onResistFlash(stats: string[]) {
  resistFlashStats.value = stats
  setTimeout(() => { resistFlashStats.value = [] }, 1500)
}

/** Justice reset flash in PlayerCard */
const justiceResetFlash = ref(false)
function onJusticeReset() {
  justiceResetFlash.value = true
  setTimeout(() => { justiceResetFlash.value = false }, 2000)
}

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

// ── Header status (moved from ActionPanel) ──────────────────────────
const me = computed(() => store.myPlayer)
const preferWeb = computed(() => store.gameState?.preferWeb ?? false)
function togglePreferWeb() { store.setPreferWeb(!preferWeb.value) }

const showFinishConfirm = ref(false)
function finishGame() {
  store.finishGame()
  showFinishConfirm.value = false
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

/** Filter out fight-result lines (containing ⟶ or →) from log text */
function filterFightLines(text: string): string {
  if (!text) return ''
  return text.split('\n').filter(line => !line.includes('⟶') && !line.includes('→') && !line.includes('Раунд #')).join('\n')
}

/** Merge personal logs + global events (minus fight results) */
const mergedEvents = computed(() => {
  const personal = store.myPlayer?.status.personalLogs || ''
  const global = filterFightLines(store.gameState?.globalLogs || '')
  const parts: string[] = []
  if (personal.trim()) parts.push(personal)
  if (global.trim()) parts.push(global)
  return parts.join('\n')
})

/**
 * "Летопись" — full game chronicle built from:
 *   - AllPersonalLogs (InGamePersonalLogsAll) — rounds split by "|||"
 *   - AllGlobalLogs (AllGameGlobalLogs)
 */
const letopis = computed(() => {
  const allGlobal = store.gameState?.allGlobalLogs || ''
  const allPersonal = store.myPlayer?.status.allPersonalLogs || ''

  const parts: string[] = []

  // Format personal logs: split by ||| into per-round sections
  if (allPersonal.trim()) {
    const rounds = allPersonal.split('|||').filter((r: string) => r.trim())
    rounds.forEach((roundText: string, idx: number) => {
      parts.push(`**Раунд #${idx + 1}**\n${roundText.trim()}`)
    })
  }

  // Append global logs at the end
  if (allGlobal.trim()) {
    parts.push(`**--- Глобальные события ---**\n${allGlobal}`)
  }

  return parts.join('\n\n')
})
</script>

<template>
  <div class="game-page">
    <!-- Loading state -->
    <div v-if="!store.gameState" class="loading">
      <p>Connecting to game...</p>
    </div>

    <!-- Active Game (or finished — same layout, just no actions) -->
    <div v-else class="game-layout">
      <!-- Left: Player info panel + action buttons -->
      <div class="game-left">
        <PlayerCard
          v-if="store.myPlayer"
          :player="store.myPlayer"
          :is-me="true"
          :resist-flash="resistFlashStats"
          :justice-reset="justiceResetFlash"
        />
        <!-- Action bar (only during active game) -->
        <ActionPanel v-if="store.myPlayer && !store.gameState.isFinished" />
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
            <!-- Status chip (moved from ActionPanel) -->
            <span v-if="me && !store.gameState.isFinished" class="status-chip" :class="{ ready: me.status.isReady, waiting: !me.status.isReady }">
              {{ me.status.isReady ? '✓ Ready' : me.status.isSkip ? '⏭ Skip' : '⏳ Your turn' }}
            </span>
          </div>
          <div class="header-right">
            <!-- Web-only toggle -->
            <button v-if="me && !store.gameState.isFinished"
              class="btn btn-ghost btn-sm web-mode-btn" :class="{ active: preferWeb }"
              title="When enabled, Discord messages are suppressed — play only via Web"
              @click="togglePreferWeb()">
              {{ preferWeb ? '🌐 Web ✓' : '🌐 Web' }}
            </button>
            <RoundTimer v-if="!store.gameState.isFinished" />
            <!-- Finish game -->
            <button v-if="me && !store.gameState.isFinished"
              class="btn btn-ghost btn-sm finish-btn"
              @click="showFinishConfirm = !showFinishConfirm">
              Finish
            </button>
            <div v-if="showFinishConfirm" class="finish-confirm">
              <span>Leave and be replaced by a bot?</span>
              <button class="btn btn-sm finish-confirm-yes" @click="finishGame()">Yes, leave</button>
              <button class="btn btn-ghost btn-sm" @click="showFinishConfirm = false">Cancel</button>
            </div>
          </div>
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
          :fight-log="store.gameState.fightLog || []"
          @attack="store.attack($event)"
          @predict="store.predict($event.playerId, $event.characterName)"
        />

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

        <!-- Logs: Row 1 = events side-by-side, Row 2 = full-width fight panel -->
        <div class="logs-row-top">
          <div class="log-panel card events-panel">
            <div class="card-header">События</div>
            <div
              v-if="mergedEvents.trim()"
              class="log-content"
              v-html="formatLogs(mergedEvents)"
            />
            <div v-else class="log-empty">Еще ничего не произошло. Наверное...</div>
          </div>
          <div class="log-panel card events-panel">
            <div class="card-header">События прошлого раунда</div>
            <div
              v-if="store.myPlayer?.status.previousRoundLogs"
              class="log-content"
              v-html="formatLogs(store.myPlayer.status.previousRoundLogs)"
            />
            <div v-else class="log-empty">В прошлом раунде ничего не произошло.</div>
          </div>
        </div>
        <div class="log-panel card fight-panel">
          <FightAnimation
            :fights="store.gameState.fightLog || []"
            :letopis="letopis"
            :players="store.gameState.players"
            :my-player-id="store.myPlayer?.playerId"
            :predictions="store.myPlayer?.predictions"
            :is-admin="store.isAdmin"
            :character-catalog="store.gameState.allCharacters || []"
            @resist-flash="onResistFlash"
            @justice-reset="onJusticeReset"
          />
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
  gap: 12px;
}

.loading {
  text-align: center;
  padding: 80px;
  color: var(--text-muted);
  font-size: 16px;
}

.finished-badge {
  background: var(--accent-gold);
  color: var(--bg-primary);
  padding: 3px 12px;
  border-radius: var(--radius);
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.finished-actions {
  display: flex;
  justify-content: center;
  padding: 16px 0;
}

/* ── 3-column layout ────────────────────────────────────────────── */
.game-layout {
  display: grid;
  grid-template-columns: 250px 1fr 250px;
  gap: 12px;
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
  gap: 10px;
  margin-bottom: 10px;
}

.header-center {
  display: flex;
  align-items: center;
  gap: 6px;
}

.round-badge {
  background: var(--kh-c-secondary-info-500);
  color: var(--text-primary);
  padding: 3px 12px;
  border-radius: var(--radius);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.3px;
  border: 1px solid var(--accent-blue);
}

.mode-badge {
  background: var(--kh-c-secondary-purple-500);
  color: var(--text-primary);
  padding: 3px 12px;
  border-radius: var(--radius);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  border: 1px solid var(--accent-purple);
}

.status-chip {
  padding: 3px 8px;
  border-radius: var(--radius);
  font-size: 10px;
  font-weight: 700;
  white-space: nowrap;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.status-chip.ready {
  background: rgba(63, 167, 61, 0.1);
  color: var(--accent-green);
  border: 1px solid rgba(63, 167, 61, 0.2);
}
.status-chip.waiting {
  background: rgba(230, 148, 74, 0.1);
  color: var(--accent-orange);
  border: 1px solid rgba(230, 148, 74, 0.2);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 6px;
  position: relative;
}

.web-mode-btn {
  font-size: 10px;
  color: var(--text-muted);
  border: 1px solid var(--border-subtle);
}
.web-mode-btn:hover { color: var(--accent-blue); border-color: var(--accent-blue); }
.web-mode-btn.active { color: var(--accent-blue); border-color: var(--accent-blue); font-weight: 800; }

.finish-btn {
  font-size: 10px;
  color: var(--accent-red);
  border: 1px solid rgba(239, 128, 128, 0.2);
}
.finish-btn:hover { background: rgba(239, 128, 128, 0.1); border-color: var(--accent-red); }

.finish-confirm {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 4px;
  background: var(--bg-card);
  border: 1px solid var(--accent-red);
  border-radius: var(--radius);
  padding: 8px 10px;
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: var(--text-secondary);
  white-space: nowrap;
  z-index: 100;
  box-shadow: var(--shadow-lg);
}
.finish-confirm-yes {
  background: var(--accent-red-dim);
  color: white;
  border: 1px solid var(--accent-red);
  font-weight: 700;
}
.finish-confirm-yes:hover { background: var(--accent-red); }

/* ── Direct Messages ──────────────────────────────────────────────── */
.direct-messages {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-top: 8px;
}

.dm-item {
  padding: 6px 12px;
  background: var(--bg-surface);
  border-left: 2px solid var(--accent-orange);
  border-radius: 0 var(--radius) var(--radius) 0;
  font-size: 12px;
  color: var(--text-primary);
  line-height: 1.5;
}

.dm-item :deep(strong) { color: var(--accent-gold); }
.dm-item :deep(em) { color: var(--accent-blue); }

/* ── Logs ────────────────────────────────────────────────────────── */
.logs-row-top {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px;
  margin-top: 6px;
}

@media (max-width: 800px) {
  .logs-row-top { grid-template-columns: 1fr; }
}

.log-panel {
  display: flex;
  flex-direction: column;
}

.events-panel {
  max-height: 150px;
  padding: 8px 10px;
}

.events-panel :deep(.card-header),
.events-panel .card-header {
  font-size: 11px;
  margin-bottom: 4px;
}

.fight-panel {
  margin-top: 6px;
  padding: 8px 10px;
}

.log-content {
  font-size: 11px;
  line-height: 1.4;
  color: var(--text-secondary);
  flex: 1;
  overflow-y: auto;
  padding: 4px 6px;
  background: var(--bg-inset);
  border-radius: var(--radius);
  font-family: var(--font-mono);
  border: 1px solid var(--border-subtle);
}

.log-content :deep(strong) { color: var(--accent-gold); }
.log-content :deep(em) { color: var(--accent-blue); }
.log-content :deep(u) { color: var(--accent-green); }
.log-content :deep(del) { color: var(--text-muted); text-decoration: line-through; }

.log-empty {
  color: var(--text-dim);
  font-style: italic;
  padding: 8px;
  text-align: center;
  font-size: 11px;
}
</style>
