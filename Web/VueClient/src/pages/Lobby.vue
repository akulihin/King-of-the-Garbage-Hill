<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import { Gift, PackageOpen, ShieldCheck, Sparkles, Trophy } from 'lucide-vue-next'
import { useGameStore } from 'src/store/game'
import { signalrService, type ReplayListEntry, type CharacterListEntry } from 'src/services/signalr'
import LootBox from 'src/components/LootBox.vue'
import { currentLocale } from 'src/i18n'

const API_BASE = import.meta.env.VITE_API_BASE || ''

const store = useGameStore()
const router = useRouter()

const isCreatingGame = ref(false)
const showCharacterPicker = ref(false)
const characterSearch = ref('')
const recentReplays = ref<ReplayListEntry[]>([])

const filteredCharacters = computed(() => {
  const q = characterSearch.value.toLowerCase()
  if (!q) return store.characterList
  return store.characterList.filter((c: CharacterListEntry) => c.name.toLowerCase().includes(q))
})

const quests = computed(() => store.questState?.quests ?? [])
const streakDays = computed(() => store.questState?.streakDays ?? 0)
const allDone = computed(() => store.questState?.allCompletedToday ?? false)
const zbsPoints = computed(() => store.questState?.zbsPoints ?? 0)
const pendingLootBoxes = computed(() => store.questState?.pendingLootBoxes ?? 0)
const lootBoxPity = computed(() => store.questState?.lootBoxPity ?? 0)
const guaranteedRareIn = computed(() => store.questState?.guaranteedRareIn ?? 0)
const lootBoxOdds = computed(() => store.questState?.lootBoxOdds ?? [])
const achievementProgress = computed(() => {
  const board = store.achievementBoard
  if (!board || board.totalAchievements <= 0) return 0
  return Math.round((board.totalUnlocked / board.totalAchievements) * 100)
})
const legendaryChance = computed(() => lootBoxOdds.value.find(entry => entry.rarity.toLocaleLowerCase() === 'legendary')?.chance ?? 0)
const pityThreshold = computed(() => Math.max(1, lootBoxPity.value + guaranteedRareIn.value))
const pityPercent = computed(() => Math.max(0, Math.min(100, (lootBoxPity.value / pityThreshold.value) * 100)))
const isOpeningLootBox = ref(false)
const showLootBoxOverlay = ref(false)
const isAcknowledgingLootBox = ref(false)
const lootBoxSaveError = ref<string | null>(null)

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

async function openLootBox() {
  if (isOpeningLootBox.value || pendingLootBoxes.value <= 0) return
  isOpeningLootBox.value = true
  lootBoxSaveError.value = null
  showLootBoxOverlay.value = true
  try {
    await store.openLootBox()
    window.setTimeout(() => {
      if (!store.lootBoxResult) {
        isOpeningLootBox.value = false
        showLootBoxOverlay.value = false
      }
    }, 1200)
  }
  catch {
    isOpeningLootBox.value = false
    showLootBoxOverlay.value = false
  }
}

async function continueLootBox(openingId: string) {
  if (isAcknowledgingLootBox.value) return
  isAcknowledgingLootBox.value = true
  lootBoxSaveError.value = null
  try {
    await store.acknowledgeLootBox(openingId)
    showLootBoxOverlay.value = false
    store.clearLootBoxResult(openingId)
  }
  catch (error) {
    lootBoxSaveError.value = error instanceof Error ? error.message : String(error)
  }
  finally {
    isAcknowledgingLootBox.value = false
  }
}

async function openAnotherLootBox(openingId: string) {
  if (isAcknowledgingLootBox.value) return
  isAcknowledgingLootBox.value = true
  lootBoxSaveError.value = null
  try {
    await store.acknowledgeLootBox(openingId)
  }
  catch (error) {
    lootBoxSaveError.value = error instanceof Error ? error.message : String(error)
    return
  }
  finally {
    isAcknowledgingLootBox.value = false
  }

  showLootBoxOverlay.value = false
  store.clearLootBoxResult(openingId)
  await nextTick()
  await openLootBox()
}

watch(() => store.lootBoxResult, (result) => {
  if (!result) return
  isOpeningLootBox.value = false
  showLootBoxOverlay.value = true
}, { immediate: true })

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
    store.requestAchievements()
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

async function openTestGamePicker() {
  if (store.characterList.length === 0) {
    await store.fetchCharacterList()
  }
  characterSearch.value = ''
  showCharacterPicker.value = true
}

async function selectTestCharacter(name: string) {
  showCharacterPicker.value = false
  isCreatingGame.value = true
  await store.createTestGame(name)
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

function viewAchievements() {
  router.push('/achievements')
}
</script>

<template>
  <div class="lobby">
    <div class="lobby-header">
      <div class="lobby-title-row">
        <img class="lobby-logo" src="https://r2.ozvmusic.com/kotgh/art/avatars/game_v2_big.png" alt="" />
        <h1>King of the Garbage Hill</h1>
      </div>
      <p class="subtitle">
        Create a new game, join an existing one, or spectate ongoing games.
      </p>
    </div>

    <!-- Rewards hub -->
    <section v-if="store.isAuthenticated" class="section rewards-section" aria-labelledby="rewards-title">
      <div class="rewards-heading">
        <div>
          <span class="rewards-kicker"><Sparkles :size="14" aria-hidden="true" /> {{ t('Progress & collection', 'Прогресс и коллекция') }}</span>
          <h2 id="rewards-title">{{ t('Rewards', 'Награды') }}</h2>
        </div>
        <span class="wallet-pill">
          <img :src="'/art/emojis/zbs.png'" alt="ZBS">
          <strong>{{ zbsPoints }}</strong>
          <span>ZBS</span>
        </span>
      </div>

      <div class="rewards-grid">
        <article class="reward-hub-card loot-hub-card" :class="{ 'has-boxes': pendingLootBoxes > 0 }">
          <div class="hub-card-glow" aria-hidden="true" />
          <header class="hub-card-header">
            <span class="hub-card-icon loot-hub-icon">
              <PackageOpen :size="29" :stroke-width="1.65" aria-hidden="true" />
              <strong>{{ pendingLootBoxes }}</strong>
            </span>
            <span class="hub-card-title">
              <small>{{ t('Reward inventory', 'Инвентарь наград') }}</small>
              <h3>{{ t('Loot boxes', 'Лутбоксы') }}</h3>
            </span>
          </header>

          <p v-if="pendingLootBoxes > 0">
            {{ t(
              `${pendingLootBoxes} ${pendingLootBoxes === 1 ? 'box is' : 'boxes are'} ready to open.`,
              `Готово к открытию: ${pendingLootBoxes}.`,
            ) }}
          </p>
          <p v-else>
            {{ t(
              'Finish in the top two while alive to bring a new box home.',
              'Финишируйте в топ-2 живым, чтобы получить новый лутбокс.',
            ) }}
          </p>

          <div class="hub-pity">
            <div class="hub-pity-label">
              <span><ShieldCheck :size="14" aria-hidden="true" /> {{ t('Rare+ guarantee', 'Гарантия редкой+') }}</span>
              <strong>{{ guaranteedRareIn > 0 ? t(`${guaranteedRareIn} boxes`, `${guaranteedRareIn} лутб.`) : t('Next box', 'Следующий') }}</strong>
            </div>
            <div class="hub-pity-track" role="progressbar" :aria-valuenow="Math.round(pityPercent)" aria-valuemin="0" aria-valuemax="100">
              <span :style="{ width: `${pityPercent}%` }" />
            </div>
            <div class="hub-odds-line">
              <span>{{ t(`Pity counter: ${lootBoxPity}`, `Счётчик удачи: ${lootBoxPity}`) }}</span>
              <span v-if="legendaryChance > 0">{{ t('Legendary', 'Легендарная') }} {{ legendaryChance }}%</span>
            </div>
          </div>

          <button
            class="btn hub-primary-action"
            :class="{ pulse: pendingLootBoxes > 0 }"
            :disabled="isOpeningLootBox || pendingLootBoxes <= 0"
            type="button"
            @click="openLootBox"
          >
            <Gift :size="17" aria-hidden="true" />
            {{ isOpeningLootBox ? t('Opening…', 'Открываем…') : pendingLootBoxes > 0 ? t('Open loot box', 'Открыть лутбокс') : t('No boxes yet', 'Лутбоксов пока нет') }}
          </button>
        </article>

        <article class="reward-hub-card achievements-hub-card">
          <div class="hub-card-glow" aria-hidden="true" />
          <header class="hub-card-header">
            <span class="hub-card-icon achievement-hub-icon">
              <Trophy :size="29" :stroke-width="1.65" aria-hidden="true" />
            </span>
            <span class="hub-card-title">
              <small>{{ t('Hall of Feats', 'Зал подвигов') }}</small>
              <h3>{{ t('Achievements', 'Достижения') }}</h3>
            </span>
            <span v-if="store.achievementBoard?.newlyUnlocked.length" class="new-achievement-badge">
              +{{ store.achievementBoard.newlyUnlocked.length }} {{ t('new', 'новых') }}
            </span>
          </header>

          <div v-if="store.achievementBoard" class="achievement-hub-progress">
            <span class="hub-progress-ring" :style="{ '--hub-progress': `${achievementProgress * 3.6}deg` }">
              <strong>{{ achievementProgress }}%</strong>
            </span>
            <span class="hub-progress-copy">
              <strong>{{ store.achievementBoard.totalUnlocked }} / {{ store.achievementBoard.totalAchievements }}</strong>
              <span>{{ t('feats completed', 'подвигов выполнено') }}</span>
            </span>
          </div>
          <div v-else class="achievement-hub-loading">
            <span />
            <span />
          </div>

          <div v-if="store.achievementBoard" class="hub-reward-totals">
            <span>
              <img :src="'/art/emojis/zbs.png'" alt="ZBS">
              <strong>{{ store.achievementBoard.earnedRewardZbs }}</strong>
              <small>/ {{ store.achievementBoard.totalRewardZbs }} ZBS</small>
            </span>
            <span>
              <Gift :size="17" aria-hidden="true" />
              <strong>{{ store.achievementBoard.earnedRewardLootBoxes }}</strong>
              <small>/ {{ store.achievementBoard.totalRewardLootBoxes }} {{ t('boxes', 'лутб.') }}</small>
            </span>
          </div>

          <button class="btn hub-secondary-action" type="button" @click="viewAchievements">
            <Trophy :size="16" aria-hidden="true" />
            {{ t('Explore achievements', 'Открыть достижения') }}
          </button>
        </article>
      </div>
    </section>

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
          :class="{ completed: quest.isCompleted, 'quest-complete': quest.current >= quest.target }"
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

    <!-- Loot Box opening and result overlay -->
    <LootBox
      v-if="showLootBoxOverlay"
      :result="store.lootBoxResult"
      :odds="lootBoxOdds"
      :zbs-balance="zbsPoints"
      :pending-loot-boxes="pendingLootBoxes"
      :loot-box-pity="lootBoxPity"
      :guaranteed-rare-in="guaranteedRareIn"
      :is-saving="isAcknowledgingLootBox"
      :save-error="lootBoxSaveError"
      @continue="continueLootBox"
      @open-another="openAnotherLootBox"
    />

    <!-- Active Games -->
    <div class="section">
      <div class="section-header">
        <h2 class="section-title">
          Active Games
          <span v-if="store.lobbyState" class="badge">{{ store.lobbyState.activeGames }}</span>
        </h2>
        <div class="header-actions">
          <button
            v-if="store.isAuthenticated"
            class="btn btn-primary btn-sm"
            :disabled="isCreatingGame"
            @click="createGame"
          >
            {{ isCreatingGame ? 'Creating...' : '+ New Game' }}
          </button>
          <button
            v-if="store.isLobbyAdmin && store.lastPlayedCharacter"
            class="btn btn-sm btn-last-play"
            :disabled="isCreatingGame"
            @click="selectTestCharacter(store.lastPlayedCharacter)"
          >
            Last Play {{ store.lastPlayedCharacter }}
          </button>
          <button
            v-if="store.isLobbyAdmin"
            class="btn btn-sm btn-test-game"
            :disabled="isCreatingGame"
            @click="openTestGamePicker"
          >
            Test New Game
          </button>
        </div>
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
          :class="{ 'almost-full': game.playerCount >= 5 }"
        >
          <span class="round-pip">R{{ game.roundNo }}</span>
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

          <!-- Character avatar preview fan -->
          <div v-if="game.characterAvatars?.length" class="game-card-avatars">
            <div
              v-for="(char, ci) in game.characterAvatars.slice(0, 6)"
              :key="ci"
              class="avatar-fan-item"
              :style="{ '--fan-i': ci, '--fan-total': Math.min(game.characterAvatars.length, 6) }"
              :title="char.name"
            >
              <img :src="char.avatar" :alt="char.name" class="avatar-fan-img" />
              <span v-if="char.tier" class="avatar-fan-tier">T{{ char.tier }}</span>
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

    <!-- Character Picker Modal (Admin Test Game) -->
    <div v-if="showCharacterPicker" class="modal-overlay" @click.self="showCharacterPicker = false">
      <div class="modal-content character-picker-modal">
        <div class="modal-header">
          <h2>Select Character</h2>
          <button class="btn btn-ghost btn-sm" @click="showCharacterPicker = false">X</button>
        </div>
        <input
          v-model="characterSearch"
          class="character-search"
          type="text"
          placeholder="Search characters..."
        />
        <div class="character-grid">
          <div
            v-for="char in filteredCharacters"
            :key="char.name"
            class="character-option card"
            @click="selectTestCharacter(char.name)"
          >
            <img :src="char.avatar" :alt="char.name" class="character-avatar" />
            <span class="character-name">{{ char.name }}</span>
            <span class="character-tier">T{{ char.tier }}</span>
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

.lobby-title-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.lobby-logo {
  height: 48px;
  width: auto;
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

/* Character avatar fan */
.game-card-avatars {
  display: flex;
  justify-content: center;
  padding: 4px 0 2px;
  position: relative;
  height: 36px;
}

.avatar-fan-item {
  position: relative;
  margin-left: -8px;
  transition: transform 0.2s var(--ease-spring, cubic-bezier(0.34, 1.56, 0.64, 1));
  z-index: calc(10 - var(--fan-i, 0));
}
.avatar-fan-item:first-child { margin-left: 0; }
.avatar-fan-item:hover { transform: translateY(-3px) scale(1.15); z-index: 20; }

.avatar-fan-img {
  width: 30px;
  height: 30px;
  border-radius: 50%;
  object-fit: cover;
  border: 2px solid var(--bg-card, #2d2b31);
  box-shadow: 0 1px 4px rgba(0,0,0,0.3);
}

.avatar-fan-tier {
  position: absolute;
  bottom: -2px;
  right: -2px;
  font-size: 7px;
  font-weight: 800;
  color: var(--accent-gold);
  background: var(--bg-primary);
  padding: 0 3px;
  border-radius: 3px;
  line-height: 12px;
  border: 1px solid var(--border-subtle);
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

/* Rewards hub */
.rewards-section { margin-bottom: 36px; }
.rewards-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; margin-bottom: 11px; }
.rewards-heading h2 { margin-top: 1px; color: var(--text-primary); font-size: 20px; font-weight: 900; letter-spacing: -0.3px; }
.rewards-kicker { display: inline-flex; align-items: center; gap: 6px; color: var(--accent-gold); font-size: 9px; font-weight: 850; letter-spacing: 1.5px; text-transform: uppercase; }
.wallet-pill { display: inline-flex; align-items: center; gap: 5px; min-height: 34px; padding: 5px 10px; color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.2); border-radius: 17px; background: rgba(63, 167, 61, 0.08); }
.wallet-pill img { width: 20px; height: 20px; object-fit: contain; }
.wallet-pill strong { font: 900 14px/1 var(--font-mono); }
.wallet-pill span { color: var(--text-muted); font-size: 8px; font-weight: 850; }
.rewards-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 11px; }
.reward-hub-card {
  position: relative;
  isolation: isolate;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 11px;
  overflow: hidden;
  padding: 17px;
  border: 1px solid var(--glass-border);
  border-radius: 14px;
  background: linear-gradient(145deg, rgba(255, 255, 255, 0.025), transparent 45%), var(--glass-bg);
  box-shadow: var(--shadow), inset 0 1px 0 var(--glass-highlight);
}
.reward-hub-card::after { content: ''; position: absolute; z-index: -2; inset: 0; pointer-events: none; background-image: radial-gradient(1px 1px at 20% 35%, rgba(255,255,255,.14), transparent), radial-gradient(1px 1px at 74% 22%, rgba(255,255,255,.12), transparent), radial-gradient(1px 1px at 88% 76%, rgba(255,255,255,.1), transparent); }
.hub-card-glow { position: absolute; z-index: -1; width: 210px; height: 210px; top: -125px; right: -85px; border-radius: 50%; filter: blur(35px); opacity: .28; }
.loot-hub-card .hub-card-glow { background: var(--accent-purple); }
.achievements-hub-card .hub-card-glow { background: var(--accent-gold); }
.loot-hub-card.has-boxes { border-color: rgba(180, 150, 255, 0.22); }
.hub-card-header { display: flex; align-items: center; gap: 11px; min-height: 48px; }
.hub-card-icon { position: relative; width: 47px; height: 47px; display: grid; place-items: center; flex: 0 0 47px; border-radius: 12px; }
.loot-hub-icon { color: var(--accent-purple); border: 1px solid rgba(180, 150, 255, 0.22); background: rgba(180, 150, 255, 0.1); }
.loot-hub-icon strong { position: absolute; right: -5px; bottom: -5px; min-width: 21px; height: 21px; display: grid; place-items: center; padding: 0 4px; color: #17151c; border: 2px solid var(--bg-card); border-radius: 11px; background: var(--accent-purple); font: 900 10px/1 var(--font-mono); }
.achievement-hub-icon { color: var(--accent-gold); border: 1px solid rgba(240, 200, 80, 0.22); background: rgba(240, 200, 80, 0.09); }
.hub-card-title { min-width: 0; flex: 1; }
.hub-card-title small { display: block; color: var(--text-dim); font-size: 8px; font-weight: 850; letter-spacing: 1px; text-transform: uppercase; }
.hub-card-title h3 { color: var(--text-primary); font-size: 16px; font-weight: 850; line-height: 1.2; }
.reward-hub-card > p { min-height: 35px; color: var(--text-muted); font-size: 10px; line-height: 1.55; }
.hub-pity { padding: 9px 10px; border: 1px solid var(--glass-border); border-radius: 10px; background: rgba(0, 0, 0, 0.13); }
.hub-pity-label { display: flex; align-items: center; justify-content: space-between; margin-bottom: 6px; }
.hub-pity-label span { display: inline-flex; align-items: center; gap: 5px; color: var(--text-muted); font-size: 9px; font-weight: 750; }
.hub-pity-label strong { color: var(--accent-purple); font: 800 9px/1 var(--font-mono); }
.hub-pity-track { height: 4px; overflow: hidden; border-radius: 3px; background: var(--bg-inset); }
.hub-pity-track span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, var(--accent-purple), var(--accent-gold)); transition: width .5s ease; }
.hub-odds-line { display: flex; justify-content: space-between; margin-top: 5px; color: var(--text-dim); font-size: 8px; }
.hub-primary-action,
.hub-secondary-action { min-height: 41px; width: 100%; margin-top: auto; }
.hub-primary-action { color: #17151c; background: linear-gradient(135deg, #d4b7ff, var(--accent-purple)); box-shadow: 0 7px 20px rgba(180, 150, 255, 0.12); }
.hub-primary-action:hover:not(:disabled) { filter: brightness(1.08); box-shadow: var(--glow-purple); }
.hub-primary-action:disabled { color: var(--text-dim); border: 1px solid var(--glass-border); background: rgba(255,255,255,.035); box-shadow: none; }
.hub-primary-action.pulse { animation: reward-button-pulse 2.1s ease-in-out infinite; }
.hub-secondary-action { color: var(--accent-gold); border: 1px solid rgba(240, 200, 80, 0.25); background: rgba(240, 200, 80, 0.08); }
.hub-secondary-action:hover { background: rgba(240, 200, 80, 0.14); box-shadow: var(--glow-gold); }
.new-achievement-badge { padding: 3px 7px; color: var(--accent-gold); border: 1px solid rgba(240, 200, 80, 0.24); border-radius: 8px; background: rgba(240, 200, 80, 0.09); font-size: 8px; font-weight: 850; white-space: nowrap; }
.achievement-hub-progress { display: flex; align-items: center; gap: 13px; padding: 3px 0; }
.hub-progress-ring { --hub-progress: 0deg; width: 66px; height: 66px; display: grid; place-items: center; flex: 0 0 66px; border-radius: 50%; background: radial-gradient(circle at center, var(--bg-card) 56%, transparent 58%), conic-gradient(var(--accent-gold) var(--hub-progress), rgba(255,255,255,.07) 0deg); box-shadow: 0 0 18px rgba(240,200,80,.08); }
.hub-progress-ring strong { color: var(--text-primary); font: 900 13px/1 var(--font-mono); }
.hub-progress-copy { display: flex; flex-direction: column; }
.hub-progress-copy strong { color: var(--text-primary); font: 900 20px/1.25 var(--font-mono); }
.hub-progress-copy span { color: var(--text-muted); font-size: 9px; }
.hub-reward-totals { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 6px; }
.hub-reward-totals > span { min-width: 0; display: flex; align-items: center; gap: 5px; padding: 7px 8px; border: 1px solid var(--glass-border); border-radius: 8px; background: rgba(0,0,0,.12); }
.hub-reward-totals img { width: 18px; height: 18px; object-fit: contain; }
.hub-reward-totals > span:first-child { color: var(--accent-green); }
.hub-reward-totals > span:last-child { color: var(--accent-purple); }
.hub-reward-totals strong { font: 850 12px/1 var(--font-mono); }
.hub-reward-totals small { overflow: hidden; color: var(--text-dim); font-size: 8px; text-overflow: ellipsis; white-space: nowrap; }
.achievement-hub-loading { display: flex; flex-direction: column; gap: 7px; padding: 8px 0; }
.achievement-hub-loading span { height: 18px; border-radius: 5px; background: linear-gradient(90deg, var(--bg-inset), var(--bg-card), var(--bg-inset)); background-size: 200% 100%; animation: hub-skeleton 1.4s linear infinite; }
.achievement-hub-loading span:last-child { width: 66%; }

@keyframes reward-button-pulse { 0%, 100% { box-shadow: 0 7px 20px rgba(180,150,255,.1); } 50% { box-shadow: 0 7px 26px rgba(180,150,255,.3), 0 0 0 3px rgba(180,150,255,.05); } }
@keyframes hub-skeleton { to { background-position: -200% 0; } }

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

/* Game Card Pulse Animation */
.game-card {
  position: relative;
  transition: all 0.3s;
  animation: game-card-idle 3s ease-in-out infinite;
}
@keyframes game-card-idle {
  0%, 100% { box-shadow: var(--shadow); }
  50% { box-shadow: var(--shadow), 0 0 8px rgba(100, 180, 240, 0.1); }
}

.game-card.almost-full {
  border-color: rgba(63, 167, 61, 0.3);
  animation: game-card-hot 2s ease-in-out infinite;
}
@keyframes game-card-hot {
  0%, 100% { box-shadow: var(--shadow); border-color: rgba(63, 167, 61, 0.3); }
  50% { box-shadow: var(--shadow), 0 0 12px rgba(63, 167, 61, 0.2); border-color: rgba(63, 167, 61, 0.5); }
}

/* Round Pip Badge */
.round-pip {
  position: absolute;
  top: 6px;
  right: 8px;
  font-size: 10px;
  font-weight: 800;
  color: var(--accent-blue);
  background: rgba(100, 180, 240, 0.12);
  padding: 2px 6px;
  border-radius: 4px;
  font-family: var(--font-mono);
}

/* Header Actions */
.header-actions {
  display: flex;
  gap: 6px;
  align-items: center;
}

/* Last Play Button */
.btn-last-play {
  background: rgba(233, 219, 61, 0.1);
  color: var(--accent-gold);
  border: 1px solid var(--accent-gold);
}
.btn-last-play:hover {
  background: rgba(233, 219, 61, 0.22);
}

/* Test Game Button */
.btn-test-game {
  background: rgba(180, 100, 255, 0.12);
  color: var(--accent-purple, #b464ff);
  border: 1px solid var(--accent-purple, #b464ff);
}
.btn-test-game:hover {
  background: rgba(180, 100, 255, 0.25);
}

/* Character Picker Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.character-picker-modal {
  background: var(--bg-card, #1e1e3a);
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  padding: 16px;
  width: 90%;
  max-width: 600px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.modal-header h2 {
  font-size: 16px;
  font-weight: 800;
  color: var(--accent-gold);
  margin: 0;
}

.character-search {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  background: var(--bg-elevated, #16162e);
  color: var(--text-primary);
  font-size: 13px;
  margin-bottom: 12px;
  box-sizing: border-box;
}
.character-search::placeholder {
  color: var(--text-muted);
}

.character-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 8px;
  overflow-y: auto;
  max-height: 50vh;
  padding-right: 4px;
}

.character-option {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  padding: 10px 6px;
  cursor: pointer;
  transition: border-color 0.2s, background 0.2s;
}
.character-option:hover {
  border-color: var(--accent-purple, #b464ff);
  background: rgba(180, 100, 255, 0.08);
}

.character-avatar {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  object-fit: cover;
}

.character-name {
  font-size: 11px;
  font-weight: 700;
  color: var(--text-primary);
  text-align: center;
  line-height: 1.2;
}

.character-tier {
  font-size: 10px;
  font-weight: 700;
  font-family: var(--font-mono);
  color: var(--accent-gold);
}

/* Quest Completion Celebration */
.quest-complete {
  animation: quest-celebrate 0.6s var(--ease-spring);
}
@keyframes quest-celebrate {
  0% { transform: scale(1); }
  30% { transform: scale(1.08); box-shadow: 0 0 20px rgba(240, 200, 80, 0.4); }
  100% { transform: scale(1); }
}

/* ── Mobile responsive ─────────────────────────────────────────── */
@media (max-width: 768px) {
  .rewards-grid {
    grid-template-columns: 1fr;
  }
  .hub-primary-action,
  .hub-secondary-action {
    min-height: 44px;
  }
  .games-grid {
    grid-template-columns: 1fr;
    gap: 8px;
  }
  .game-card-actions .btn {
    min-height: 44px;
    flex: 1;
  }
}

@media (max-width: 480px) {
  .rewards-heading {
    align-items: center;
  }
  .reward-hub-card {
    padding: 14px;
  }
  .quest-info,
  .section-header {
    align-items: flex-start;
    flex-direction: column;
    gap: 8px;
  }
  .games-grid {
    grid-template-columns: 1fr;
  }
}

@media (prefers-reduced-motion: reduce) {
  .hub-primary-action.pulse,
  .achievement-hub-loading span,
  .quest-complete {
    animation: none;
  }
  .hub-pity-track span {
    transition: none;
  }
}
</style>
