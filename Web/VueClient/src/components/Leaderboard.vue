<script setup lang="ts">
import { ref, computed } from 'vue'
import type { Player, Prediction, CharacterInfo } from 'src/services/signalr'

const props = defineProps<{
  players: Player[]
  myPlayerId?: string
  canAttack?: boolean
  predictions?: Prediction[]
  characterNames: string[]
  characterCatalog: CharacterInfo[]
  isAdmin?: boolean
  roundNo?: number
  confirmedPredict?: boolean
}>()

const emit = defineEmits<{
  attack: [place: number]
  predict: [payload: { playerId: string; characterName: string }]
}>()

// Which player's predict dropdown is open
const predictOpenFor = ref<string | null>(null)
const predictSearch = ref('')

// Client-side stat guesses for non-admin players (playerId → { stat: value })
const statGuesses = ref<Record<string, { intelligence: number; strength: number; speed: number; psyche: number }>>({})

const sorted = computed(() =>
  [...props.players]
    .filter((p: Player) => !p.kratosIsDead)
    .sort((a, b) => a.status.place - b.status.place),
)

const maxScore = computed(() => {
  const scores = props.players
    .filter((p: Player) => p.status.score >= 0) // exclude hidden scores
    .map((p: Player) => p.status.score)
  return Math.max(...scores, 1)
})

// After round 8 + confirmed, predictions are locked
const canPredict = computed(() => {
  if (!props.characterNames || props.characterNames.length === 0) return false
  if (props.confirmedPredict && (props.roundNo ?? 0) > 8) return false
  return true
})

// Build a lookup map from character name → catalog entry
const charCatalogMap = computed(() => {
  const map: Record<string, CharacterInfo> = {}
  for (const c of props.characterCatalog || []) {
    map[c.name] = c
  }
  return map
})

function getPrediction(playerId: string): string {
  if (!props.predictions) return ''
  const p = props.predictions.find((pr: Prediction) => pr.playerId === playerId)
  return p?.characterName ?? ''
}

/** For non-admin opponents: get the predicted character's catalog entry */
function getPredictedCharInfo(playerId: string): CharacterInfo | null {
  const predName = getPrediction(playerId)
  if (!predName) return null
  return charCatalogMap.value[predName] ?? null
}

/** Whether this player's data is masked (non-admin viewing an opponent) */
function isMasked(player: Player): boolean {
  if (props.isAdmin) return false
  if (player.playerId === props.myPlayerId) return false
  return player.character.name === '???'
}

/** Get display avatar for a player (predicted char avatar or guess.png) */
function getDisplayAvatar(player: Player): string {
  if (!isMasked(player)) return player.character.avatarCurrent || player.character.avatar
  const pred = getPredictedCharInfo(player.playerId)
  if (pred) return pred.avatar
  return '/art/avatars/guess.png'
}

/** Get display character name */
function getDisplayCharName(player: Player): string {
  if (!isMasked(player)) return player.character.name
  const pred = getPredictedCharInfo(player.playerId)
  if (pred) return pred.name + ' (?)'
  return '???'
}

/** Get display stat value for a specific stat */
function getDisplayStat(player: Player, stat: 'intelligence' | 'strength' | 'speed' | 'psyche'): string {
  if (!isMasked(player)) return String(player.character[stat])
  // Check client-side guesses first
  const guess = statGuesses.value[player.playerId]
  if (guess && guess[stat] !== -1) return String(guess[stat])
  // Fall back to predicted char's base stats
  const pred = getPredictedCharInfo(player.playerId)
  if (pred) return String(pred[stat])
  return '?'
}

/** Get display score */
function getDisplayScore(player: Player): string {
  if (player.status.score < 0) return '?'
  return String(player.status.score)
}

/** Get numeric score for bar width (hidden → 0) */
function getScoreForBar(player: Player): number {
  return player.status.score < 0 ? 0 : player.status.score
}

/** Get display tier */
function getDisplayTier(player: Player): number {
  if (!isMasked(player)) return player.character.tier
  const pred = getPredictedCharInfo(player.playerId)
  if (pred) return pred.tier
  return 0
}

/** Initialize stat guesses for a player from predicted character's base stats */
function initGuessesFromPrediction(playerId: string) {
  const pred = getPredictedCharInfo(playerId)
  if (pred) {
    statGuesses.value[playerId] = {
      intelligence: pred.intelligence,
      strength: pred.strength,
      speed: pred.speed,
      psyche: pred.psyche,
    }
  }
}

/** Handle stat guess edit */
function updateStatGuess(playerId: string, stat: 'intelligence' | 'strength' | 'speed' | 'psyche', value: string) {
  if (!statGuesses.value[playerId]) {
    statGuesses.value[playerId] = { intelligence: -1, strength: -1, speed: -1, psyche: -1 }
  }
  const num = parseInt(value, 10)
  statGuesses.value[playerId][stat] = isNaN(num) ? -1 : Math.max(0, Math.min(20, num))
}

function filteredCharacters(): string[] {
  const q = predictSearch.value.toLowerCase()
  if (!q) return props.characterNames
  return props.characterNames.filter((n: string) => n.toLowerCase().includes(q))
}

function selectPredict(playerId: string, charName: string) {
  emit('predict', { playerId, characterName: charName })
  predictOpenFor.value = null
  predictSearch.value = ''
  // Auto-populate stat guesses from predicted character's base stats
  setTimeout(() => initGuessesFromPrediction(playerId), 100)
}

function togglePredict(playerId: string) {
  if (predictOpenFor.value === playerId) {
    predictOpenFor.value = null
    predictSearch.value = ''
  } else {
    predictOpenFor.value = playerId
    predictSearch.value = ''
  }
}

function closePredict() {
  predictOpenFor.value = null
  predictSearch.value = ''
}

function handleAttack(player: Player) {
  if (!props.canAttack) return
  if (player.playerId === props.myPlayerId) return
  emit('attack', player.status.place)
}

function tierStars(tier: number): string {
  if (tier <= 0) return ''
  return '★'.repeat(Math.min(tier, 6))
}
</script>

<template>
  <div class="leaderboard card">
    <div class="lb-table">
      <div
        v-for="player in sorted"
        :key="player.playerId"
        class="lb-row"
        :class="{
          'is-me': player.playerId === myPlayerId,
          'is-bot': player.isBot,
          'is-ready': player.status.isReady,
          'can-click': canAttack && player.playerId !== myPlayerId,
        }"
        @click="handleAttack(player)"
      >
        <!-- Place (with optional prefix from passives like octopus tentacles) -->
        <div class="lb-place">
          <span
            v-if="player.customLeaderboardPrefix"
            class="lb-prefix"
            v-html="player.customLeaderboardPrefix"
          />{{ player.status.place }}
        </div>

        <!-- Avatar -->
        <div class="lb-avatar">
          <img
            :src="getDisplayAvatar(player)"
            :alt="getDisplayCharName(player)"
            class="avatar-img"
            :class="{ 'masked-avatar': isMasked(player) && !getPrediction(player.playerId) }"
          >
          <span v-if="player.status.isReady" class="ready-dot" title="Ready" />
          <span v-if="player.status.isBlock" class="block-dot" title="Blocking" />
        </div>

        <!-- Player info -->
        <div class="lb-info">
          <div class="lb-name">
            <span class="player-name">{{ player.discordUsername }}</span>
            <span v-if="player.isBot" class="badge bot-badge">BOT</span>
            <!-- Custom leaderboard annotations from passives -->
            <span
              v-if="player.customLeaderboardText"
              class="lb-custom"
              v-html="player.customLeaderboardText"
            />
          </div>
          <div class="lb-character" :class="{ 'masked-name': isMasked(player) && !getPrediction(player.playerId) }">
            {{ getDisplayCharName(player) }}
            <span v-if="getDisplayTier(player) > 0" class="tier-stars">{{ tierStars(getDisplayTier(player)) }}</span>
          </div>
        </div>

        <!-- Mini stats (editable for non-admin opponents) -->
        <div class="lb-stats">
          <template v-if="isMasked(player)">
            <span class="stat stat-intelligence" title="Intelligence">🧠
              <input
                type="number"
                class="stat-guess-input"
                :value="getDisplayStat(player, 'intelligence')"
                min="0" max="20"
                :placeholder="'?'"
                @change="updateStatGuess(player.playerId, 'intelligence', ($event.target as HTMLInputElement).value)"
                @click.stop
              >
            </span>
            <span class="stat stat-strength" title="Strength">💪
              <input
                type="number"
                class="stat-guess-input"
                :value="getDisplayStat(player, 'strength')"
                min="0" max="20"
                :placeholder="'?'"
                @change="updateStatGuess(player.playerId, 'strength', ($event.target as HTMLInputElement).value)"
                @click.stop
              >
            </span>
            <span class="stat stat-speed" title="Speed">⚡
              <input
                type="number"
                class="stat-guess-input"
                :value="getDisplayStat(player, 'speed')"
                min="0" max="20"
                :placeholder="'?'"
                @change="updateStatGuess(player.playerId, 'speed', ($event.target as HTMLInputElement).value)"
                @click.stop
              >
            </span>
            <span class="stat stat-psyche" title="Psyche">🧘
              <input
                type="number"
                class="stat-guess-input"
                :value="getDisplayStat(player, 'psyche')"
                min="0" max="20"
                :placeholder="'?'"
                @change="updateStatGuess(player.playerId, 'psyche', ($event.target as HTMLInputElement).value)"
                @click.stop
              >
            </span>
          </template>
          <template v-else>
            <span class="stat stat-intelligence" title="Intelligence">🧠{{ player.character.intelligence }}</span>
            <span class="stat stat-strength" title="Strength">💪{{ player.character.strength }}</span>
            <span class="stat stat-speed" title="Speed">⚡{{ player.character.speed }}</span>
            <span class="stat stat-psyche" title="Psyche">🧘{{ player.character.psyche }}</span>
          </template>
        </div>

        <!-- Score bar -->
        <div class="lb-score-area">
          <div class="score-bar-bg">
            <div
              class="score-bar-fill"
              :style="{ width: `${(getScoreForBar(player) / maxScore) * 100}%` }"
            />
          </div>
          <span class="score-value" :class="{ 'score-hidden': player.status.score < 0 }">
            {{ getDisplayScore(player) }}
          </span>
        </div>

        <!-- Predict button (opponents only, if allowed) -->
        <div
          v-if="player.playerId !== myPlayerId && canPredict"
          class="lb-predict"
          @click.stop
        >
          <button
            class="predict-btn"
            :class="{ 'has-prediction': !!getPrediction(player.playerId) }"
            :title="getPrediction(player.playerId) || 'Predict character'"
            @click="togglePredict(player.playerId)"
          >
            <template v-if="getPrediction(player.playerId)">
              {{ getPrediction(player.playerId) }}
            </template>
            <template v-else>
              ✏️
            </template>
          </button>
        </div>

        <!-- Show locked prediction (after confirmation) -->
        <div
          v-else-if="player.playerId !== myPlayerId && getPrediction(player.playerId)"
          class="lb-predict-locked"
        >
          <span class="predict-locked-text">{{ getPrediction(player.playerId) }}</span>
        </div>
      </div>
    </div>

    <!-- Predict dropdown — Teleported to body for z-index priority -->
    <Teleport to="body">
      <div v-if="predictOpenFor" class="predict-overlay" @click="closePredict">
        <div class="predict-dropdown" @click.stop>
          <input
            v-model="predictSearch"
            class="predict-search"
            placeholder="Search character..."
            autofocus
          >
          <div class="predict-list">
            <button
              v-for="name in filteredCharacters()"
              :key="name"
              class="predict-option"
              @click="selectPredict(predictOpenFor!, name)"
            >
              <img
                v-if="charCatalogMap[name]?.avatar"
                :src="charCatalogMap[name].avatar"
                class="predict-option-avatar"
                :alt="name"
              >
              <span>{{ name }}</span>
              <span v-if="charCatalogMap[name]" class="predict-option-stats">
                🧠{{ charCatalogMap[name].intelligence }}
                💪{{ charCatalogMap[name].strength }}
                ⚡{{ charCatalogMap[name].speed }}
                🧘{{ charCatalogMap[name].psyche }}
              </span>
            </button>
            <div v-if="filteredCharacters().length === 0" class="predict-empty">
              No characters found
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.lb-table {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.lb-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: var(--radius);
  background: var(--bg-secondary);
  transition: all 0.15s;
}

.lb-row:hover { background: var(--bg-card-hover); }

.lb-row.can-click { cursor: crosshair; }

.lb-row.can-click:hover {
  background: rgba(248, 113, 113, 0.1);
  border-left: 3px solid var(--accent-red);
  padding-left: 7px;
}

.lb-row.is-me {
  border-left: 3px solid var(--accent-gold);
  padding-left: 7px;
  background: rgba(255, 215, 0, 0.05);
}

.lb-row.is-me.can-click:hover {
  border-left-color: var(--accent-gold);
  background: rgba(255, 215, 0, 0.05);
  cursor: default;
}

.lb-row.is-bot { opacity: 0.75; }

.lb-place {
  width: 24px;
  text-align: center;
  font-size: 16px;
  font-weight: 800;
  color: var(--text-muted);
}

.lb-prefix {
  font-size: 14px;
  margin-right: 1px;
}

.lb-row:nth-child(1) .lb-place { color: var(--accent-gold); }
.lb-row:nth-child(2) .lb-place { color: #c0c0c0; }
.lb-row:nth-child(3) .lb-place { color: #cd7f32; }

.lb-avatar {
  width: 36px;
  height: 36px;
  flex-shrink: 0;
  position: relative;
}

.avatar-img {
  width: 36px;
  height: 36px;
  border-radius: 6px;
  object-fit: cover;
  border: 2px solid var(--border-color);
}

.avatar-placeholder {
  width: 36px;
  height: 36px;
  border-radius: 6px;
  background: var(--bg-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 14px;
  color: var(--text-muted);
  border: 2px solid var(--border-color);
}

.ready-dot, .block-dot {
  position: absolute;
  bottom: -2px;
  right: -2px;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  border: 2px solid var(--bg-secondary);
}

.ready-dot { background: var(--accent-green); }
.block-dot { background: var(--accent-blue); }

.lb-info { flex: 1; min-width: 0; }

.lb-name {
  display: flex;
  align-items: center;
  gap: 4px;
}

.player-name {
  font-weight: 600;
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.badge { padding: 1px 5px; border-radius: 3px; font-size: 9px; font-weight: 700; }
.bot-badge { background: var(--text-muted); color: var(--bg-primary); }

.lb-custom {
  font-size: 12px;
  color: var(--accent-orange);
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}
.lb-custom :deep(strong) { color: var(--accent-gold); font-weight: 700; }
.lb-custom :deep(em) { color: var(--accent-blue); }
.lb-custom :deep(u) { color: var(--accent-green); }
.lb-custom :deep(.lb-emoji) {
  width: 18px;
  height: 18px;
  vertical-align: middle;
  display: inline;
  margin: 0 1px;
}

.lb-character { font-size: 11px; color: var(--text-muted); }
.lb-character.masked-name { color: var(--text-muted); font-style: italic; opacity: 0.6; }
.tier-stars { color: var(--accent-gold); font-size: 9px; }

.masked-avatar { opacity: 0.6; filter: grayscale(0.3); }

.lb-stats {
  display: flex;
  gap: 4px;
  font-size: 11px;
  font-family: var(--font-mono);
}

.stat { white-space: nowrap; display: flex; align-items: center; gap: 1px; }

.stat-guess-input {
  width: 28px;
  height: 20px;
  padding: 0 2px;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background: var(--bg-primary);
  color: var(--accent-orange);
  font-family: var(--font-mono);
  font-size: 11px;
  font-weight: 700;
  text-align: center;
  outline: none;
  -moz-appearance: textfield;
}

.stat-guess-input::-webkit-outer-spin-button,
.stat-guess-input::-webkit-inner-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.stat-guess-input::placeholder { color: var(--text-muted); font-style: italic; }

.stat-guess-input:focus {
  border-color: var(--accent-orange);
  box-shadow: 0 0 0 1px rgba(251, 146, 60, 0.3);
}

.score-hidden { color: var(--text-muted); font-style: italic; }

.lb-score-area {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 80px;
}

.score-bar-bg {
  flex: 1;
  height: 6px;
  background: var(--bg-primary);
  border-radius: 3px;
  overflow: hidden;
}

.score-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--accent-green), var(--accent-gold));
  border-radius: 3px;
  transition: width 0.5s ease;
}

.score-value {
  font-size: 14px;
  font-weight: 800;
  min-width: 24px;
  text-align: right;
  color: var(--accent-gold);
  font-family: var(--font-mono);
}

/* Predict inline button */
.lb-predict { flex-shrink: 0; }

.predict-btn {
  padding: 4px 10px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-primary);
  color: var(--text-muted);
  font-size: 12px;
  cursor: pointer;
  white-space: nowrap;
  max-width: 100px;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: all 0.15s;
}

.predict-btn:hover {
  border-color: var(--accent-purple);
  color: var(--text-primary);
}

.predict-btn.has-prediction {
  color: var(--accent-purple);
  border-color: var(--accent-purple);
  background: rgba(167, 139, 250, 0.1);
}

/* Locked prediction (confirmed, read-only) */
.lb-predict-locked { flex-shrink: 0; }

.predict-locked-text {
  padding: 4px 10px;
  border-radius: 6px;
  background: rgba(167, 139, 250, 0.05);
  border: 1px solid rgba(167, 139, 250, 0.2);
  color: var(--accent-purple);
  font-size: 12px;
  opacity: 0.7;
}
</style>

<!-- Teleported dropdown styles — must be unscoped -->
<style>
.predict-overlay {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.5);
}

.predict-dropdown {
  width: 280px;
  max-height: 360px;
  background: var(--bg-card, #16213e);
  border: 1px solid var(--border-color, #2a2a4a);
  border-radius: 12px;
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.6);
  overflow: hidden;
}

.predict-search {
  width: 100%;
  padding: 12px 16px;
  border: none;
  border-bottom: 1px solid var(--border-color, #2a2a4a);
  background: transparent;
  color: var(--text-primary, #e0e0e0);
  font-size: 14px;
  outline: none;
}

.predict-list {
  max-height: 280px;
  overflow-y: auto;
}

.predict-option {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 8px 16px;
  border: none;
  background: transparent;
  color: var(--text-secondary, #a0a0b0);
  font-size: 14px;
  text-align: left;
  cursor: pointer;
  transition: background 0.1s;
}

.predict-option:hover {
  background: var(--bg-card-hover, #1a2744);
  color: var(--text-primary, #e0e0e0);
}

.predict-option-avatar {
  width: 28px;
  height: 28px;
  border-radius: 4px;
  object-fit: cover;
  flex-shrink: 0;
}

.predict-option-stats {
  margin-left: auto;
  font-size: 10px;
  font-family: var(--font-mono, monospace);
  color: var(--text-muted, #6b6b80);
  white-space: nowrap;
}

.predict-empty {
  padding: 16px;
  text-align: center;
  color: var(--text-muted, #6b6b80);
  font-size: 13px;
}
</style>
