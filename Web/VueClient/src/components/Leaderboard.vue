<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { Player, Prediction, CharacterInfo, FightEntry, DeathNote } from 'src/services/signalr'
import ScoreOdometer from 'src/components/ScoreOdometer.vue'

const props = defineProps<{
  players: Player[]
  myPlayerId?: string
  canAttack?: boolean
  predictions?: Prediction[]
  characterNames: string[]
  characterCatalog: CharacterInfo[]
  isAdmin?: boolean
  isFinished?: boolean
  roundNo?: number
  confirmedPredict?: boolean
  fightLog?: FightEntry[]
  isKira?: boolean
  deathNote?: DeathNote
  isBug?: boolean
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
    .filter((p: Player) => !p.isDead)
    .sort((a, b) => a.status.place - b.status.place),
)

// After round 8 + confirmed, predictions are locked. Kira doesn't predict.
const canPredict = computed(() => {
  if (props.isKira) return false
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


function isProtected(player: Player): boolean {
  if ((props.roundNo ?? 0) === 10 && player.character.passives.some(
    (p: { name: string }) => p.name === 'Стримснайпят и банят и банят и банят'
  )) return true
  return false
}

/** Map a 0-based leaderboard index to a hill tier class (hill-1 … hill-6) */
function hillTierClass(index: number, total: number): string {
  if (total <= 1) return 'hill-1'
  const tier = Math.round((index / (total - 1)) * 5) + 1
  return `hill-${tier}`
}

/** Whether this index is the last (ДНО) position */
function isLastPlace(index: number, total: number): boolean {
  return total > 1 && index === total - 1
}

const attackStampId = ref<string | null>(null)

function handleAttack(player: Player) {
  if (!props.canAttack) return
  emit('attack', player.status.place)
  // Attack stamp animation
  attackStampId.value = player.playerId
  setTimeout(() => { attackStampId.value = null }, 400)
}

// ── Score change flash animation ─────────────────────────────────────
const prevScores = ref<Record<string, number>>({})
const scoreFlash = ref<Record<string, 'up' | 'down'>>({})
let scoreFlashTimers: ReturnType<typeof setTimeout>[] = []

watch(() => props.players, (newPlayers) => {
  if (!newPlayers || !newPlayers.length) return
  const newFlashes: Record<string, 'up' | 'down'> = {}
  for (const p of newPlayers) {
    const prev = prevScores.value[p.playerId]
    if (prev !== undefined && p.status.score !== prev && p.status.score >= 0) {
      newFlashes[p.playerId] = p.status.score > prev ? 'up' : 'down'
    }
    prevScores.value[p.playerId] = p.status.score
  }
  if (Object.keys(newFlashes).length > 0) {
    scoreFlash.value = { ...scoreFlash.value, ...newFlashes }
    const t = setTimeout(() => {
      for (const id of Object.keys(newFlashes)) {
        delete scoreFlash.value[id]
      }
      scoreFlash.value = { ...scoreFlash.value }
    }, 1500)
    scoreFlashTimers.push(t)
  }
}, { deep: true })

// ── "On fire" streak tracking ───────────────────────────────────────
const streakCount = ref<Record<string, number>>({})

watch(() => props.players, (newPlayers) => {
  if (!newPlayers || !newPlayers.length) return
  for (const p of newPlayers) {
    const prev = prevScores.value[p.playerId]
    if (prev !== undefined && p.status.score > prev && p.status.score >= 0) {
      streakCount.value[p.playerId] = (streakCount.value[p.playerId] ?? 0) + 1
    } else if (prev !== undefined && p.status.score <= prev) {
      streakCount.value[p.playerId] = 0
    }
  }
  streakCount.value = { ...streakCount.value }
}, { deep: true })

function isOnFire(playerId: string): boolean {
  return (streakCount.value[playerId] ?? 0) >= 3
}

// ── Drop flash animation ────────────────────────────────────────────
const droppedPlayers = ref<Set<string>>(new Set())
let dropClearTimers: ReturnType<typeof setTimeout>[] = []

function getDropCount(discordUsername: string): number {
  if (!props.fightLog) return 0
  let total = 0
  for (const f of props.fightLog) {
    if (f.droppedPlayerName === discordUsername && f.drops > 0) {
      total += f.drops
    }
  }
  return total
}

function isDropped(player: Player): boolean {
  return droppedPlayers.value.has(player.discordUsername)
}

watch(() => props.fightLog, (newLog) => {
  if (!newLog || !newLog.length) return
  // Clear old timers
  dropClearTimers.forEach(t => clearTimeout(t))
  dropClearTimers = []
  const newDropped = new Set<string>()
  for (const f of newLog) {
    if (f.drops > 0 && f.droppedPlayerName) {
      newDropped.add(f.droppedPlayerName)
    }
  }
  droppedPlayers.value = newDropped
  // Auto-clear after 5 seconds
  if (newDropped.size > 0) {
    const t = setTimeout(() => { droppedPlayers.value = new Set() }, 5000)
    dropClearTimers.push(t)
  }
}, { deep: true })

// ── Tooltip system (matches PlayerCard pattern) ──────────────────────
const tipText = ref('')
const tipVisible = ref(false)
const tipPos = ref({ x: 0, y: 0 })
let tipTimer: ReturnType<typeof setTimeout> | null = null

function showTip(e: MouseEvent, text: string) {
  if (tipTimer) clearTimeout(tipTimer)
  tipText.value = text
  tipPos.value = { x: e.clientX, y: e.clientY }
  tipTimer = setTimeout(() => { tipVisible.value = true }, 120)
}
function moveTip(e: MouseEvent) {
  tipPos.value = { x: e.clientX, y: e.clientY }
}
function hideTip() {
  if (tipTimer) clearTimeout(tipTimer)
  tipVisible.value = false
}
</script>

<template>
  <div class="leaderboard card">
    <TransitionGroup name="hill" tag="div" class="lb-table">
      <div
        v-for="(player, index) in sorted"
        :key="player.playerId"
        class="lb-row"
        :class="{
          'is-me': player.playerId === myPlayerId,
          'is-bot': player.isBot,
          'is-ready': player.status.isReady,
          'can-click': canAttack,
          'is-protected': isProtected(player),
          'dropped': isDropped(player),
          'in-harm-range': player.isInMyHarmRange,
          'on-fire': isOnFire(player.playerId),
          'attack-stamp': attackStampId === player.playerId,
          [hillTierClass(index, sorted.length)]: true,
        }"
        @click="handleAttack(player)"
      >
        <!-- Place (with crown for 1st, ДНО for last) -->
        <div class="lb-place">
          <span v-if="index === 0" class="rank-crown">♛</span>
          <span
            v-if="player.customLeaderboardPrefix"
            class="lb-prefix"
            v-html="player.customLeaderboardPrefix"
          />
          <span class="place-number">{{ player.status.place }}</span>
          <span
            v-if="player.isInMyHarmRange"
            class="harm-range-dot"
            @mouseenter="showTip($event, 'In harm range')"
            @mousemove="moveTip"
            @mouseleave="hideTip"
          />
          <span v-if="isLastPlace(index, sorted.length)" class="dno-label">ДНО</span>
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
            <span v-if="isKira && deathNote && player.playerId === deathNote.lPlayerId" class="badge l-badge">L</span>
            <!-- Custom leaderboard annotations from passives -->
            <span
              v-if="player.customLeaderboardText"
              class="lb-custom"
              v-html="player.customLeaderboardText"
            />
            <!-- Exploit markers (visible only to Баг) -->
            <span v-if="isBug && player.isExploitable" class="badge vuln-badge">VULN</span>
            <span v-if="isBug && player.isExploitFixed" class="badge patched-badge">PATCHED</span>
          </div>
          <div class="lb-character" :class="{ 'masked-name': isMasked(player) && !getPrediction(player.playerId) }">
            {{ getDisplayCharName(player) }}
          </div>
        </div>

        <!-- Mini stats (editable for non-admin opponents) -->
        <div class="lb-stats">
          <template v-if="isMasked(player)">
            <span class="stat stat-intelligence" title="Intelligence"><span class="gi gi-int">INT</span>
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
            <span class="stat stat-strength" title="Strength"><span class="gi gi-str">STR</span>
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
            <span class="stat stat-speed" title="Speed"><span class="gi gi-spd">SPD</span>
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
            <span class="stat stat-psyche" title="Psyche"><span class="gi gi-psy">PSY</span>
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
            <span class="stat stat-intelligence" title="Intelligence"><span class="gi gi-int">INT</span>{{ player.character.intelligence }}</span>
            <span class="stat stat-strength" title="Strength"><span class="gi gi-str">STR</span>{{ player.character.strength }}</span>
            <span class="stat stat-speed" title="Speed"><span class="gi gi-spd">SPD</span>{{ player.character.speed }}</span>
            <span class="stat stat-psyche" title="Psyche"><span class="gi gi-psy">PSY</span>{{ player.character.psyche }}</span>
          </template>
        </div>

        <!-- Score bar (admin/replay only) -->
        <div v-if="isAdmin || isFinished" class="lb-score-area">
          <ScoreOdometer
            v-if="player.status.score >= 0"
            :value="player.status.score"
            size="sm"
            :flash-color="scoreFlash[player.playerId] === 'up' ? '#5ba85b' : scoreFlash[player.playerId] === 'down' ? '#e05545' : null"
            class="score-value"
          />
          <span v-else class="score-value score-hidden">?</span>
          <span v-if="isDropped(player)" class="drop-badge">
            -{{ getDropCount(player.discordUsername) }}
          </span>
        </div>

        <!-- Drop overlay animation -->
        <Transition name="drop-overlay">
          <div v-if="isDropped(player)" class="drop-overlay">
            <div class="drop-arrow">▼</div>
            <div class="drop-count">-{{ getDropCount(player.discordUsername) }}</div>
            <div class="drop-streak" />
          </div>
        </Transition>

        <!-- Predict button (o  pponents only, if allowed) -->
        <div
          v-if="player.playerId !== myPlayerId && canPredict"
          class="lb-predict"
          @click.stop
        >
          <button
            class="predict-btn"
            :class="{ 'has-prediction': !!getPrediction(player.playerId) }"
            :title="getPrediction(player.playerId) || 'Predict character'"
            data-sfx-predict="true"
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
    </TransitionGroup>

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
              data-sfx-predict="true"
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
                <span class="gi gi-int">INT</span>{{ charCatalogMap[name].intelligence }}
                <span class="gi gi-str">STR</span>{{ charCatalogMap[name].strength }}
                <span class="gi gi-spd">SPD</span>{{ charCatalogMap[name].speed }}
                <span class="gi gi-psy">PSY</span>{{ charCatalogMap[name].psyche }}
              </span>
            </button>
            <div v-if="filteredCharacters().length === 0" class="predict-empty">
              No characters found
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Tooltip — Teleported to body (reuses pc-tooltip from PlayerCard) -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.leaderboard.card { padding: 8px; }

.lb-table {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

/* ── Hill tier CSS custom properties ────────────────────────────────── */
.lb-row.hill-1 { --hill-tint: rgba(240,200,80, 0.06); --hill-border: rgba(240,200,80, 0.35); --hill-place: #f0c850; --hill-glow: 0 0 12px rgba(240,200,80, 0.08); }
.lb-row.hill-2 { --hill-tint: rgba(192,192,210, 0.08); --hill-border: rgba(192,192,210, 0.4); --hill-place: #c0c0d0; --hill-glow: 0 0 8px rgba(192,192,210, 0.08); }
.lb-row.hill-3 { --hill-tint: rgba(205,160,100, 0.07); --hill-border: rgba(205,160,100, 0.35); --hill-place: #cda064; --hill-glow: none; }
.lb-row.hill-4 { --hill-tint: rgba(80,95,130, 0.06);  --hill-border: rgba(80,95,130, 0.25);  --hill-place: #6a7a9a; --hill-glow: none; }
.lb-row.hill-5 { --hill-tint: rgba(50,65,75, 0.08);   --hill-border: rgba(50,65,75, 0.3);   --hill-place: #506570; --hill-glow: none; }
.lb-row.hill-6 { --hill-tint: rgba(160,45,45, 0.06);  --hill-border: rgba(160,45,45, 0.35);  --hill-place: #a03030; --hill-glow: inset 0 0 12px rgba(160,45,45, 0.08); }

.lb-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 7px 10px;
  border-radius: var(--radius);
  background: linear-gradient(90deg, var(--hill-tint, var(--bg-secondary)), var(--bg-secondary) 80%);
  transition: all 0.2s var(--ease-in-out);
  border: 1px solid transparent;
  border-left: 3px solid var(--hill-border, transparent);
  box-shadow: var(--hill-glow, none);
  position: relative;
  overflow: hidden;
}

/* 1D. Row hover shimmer */
.lb-row::before {
  content: '';
  position: absolute;
  inset: 0;
  background: radial-gradient(ellipse at 50% 50%, rgba(255,255,255,0.06), transparent 70%);
  opacity: 0;
  transition: opacity 0.3s;
  pointer-events: none;
  z-index: 0;
}
.lb-row:hover::before {
  opacity: 1;
}

.lb-row:hover {
  background: linear-gradient(90deg, var(--hill-tint, var(--bg-card-hover)), var(--bg-card-hover) 80%);
  transform: translateX(2px);
}

.lb-row.can-click { cursor: crosshair; }

.lb-row.can-click:hover {
  background: linear-gradient(90deg, rgba(239,128,128, 0.1), rgba(239,128,128, 0.03) 80%);
  border-color: rgba(239,128,128, 0.3);
  border-left-color: var(--accent-red);
  box-shadow: 0 0 12px rgba(239,128,128, 0.1), inset 0 0 8px rgba(239,128,128, 0.05);
  transform: translateX(3px);
}

/* ── "You" row highlight — gold glow layered on top of hill tier ──── */
.lb-row.is-me {
  border-top-color: rgba(240,200,80, 0.2);
  border-right-color: rgba(240,200,80, 0.2);
  border-bottom-color: rgba(240,200,80, 0.2);
  box-shadow: 0 0 12px rgba(240,200,80, 0.12), inset 0 0 12px rgba(240,200,80, 0.04), inset 0 1px 0 rgba(240,200,80, 0.08);
}

/* 1B. Gold shimmer sweep on "my" row */
.lb-row.is-me::before {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent 0%, rgba(240,200,80,0.06) 40%, rgba(240,200,80,0.1) 50%, rgba(240,200,80,0.06) 60%, transparent 100%);
  background-size: 200% 100%;
  animation: me-shimmer 4s ease-in-out infinite;
  pointer-events: none;
  z-index: 0;
}
@keyframes me-shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

.lb-row.is-me.can-click:hover {
  border-top-color: rgba(240,200,80, 0.2);
  border-right-color: rgba(240,200,80, 0.2);
  border-bottom-color: rgba(240,200,80, 0.2);
}

/* ── Podium effect — top 3 elevated ──────────────────────────────────── */
.lb-row.hill-2 {
  transform: translateY(-1px);
  z-index: 2;
}
.lb-row.hill-3 {
  z-index: 1;
}

/* ── Terrain flavor — hill elevation gradient for #2-3 ─────────────── */
.lb-row.hill-2:not(.is-protected)::after,
.lb-row.hill-3:not(.is-protected)::after {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, rgba(255,255,255,0.03) 0%, transparent 60%);
  pointer-events: none;
  border-radius: inherit;
  z-index: 0;
}

/* ── Terrain flavor — garbage texture for last place ───────────────── */
.lb-row.hill-6:not(.is-protected)::after {
  content: '';
  position: absolute;
  inset: 0;
  background-image:
    radial-gradient(1px 1px at 10% 20%, rgba(160,45,45,0.15) 50%, transparent 50%),
    radial-gradient(1px 1px at 30% 60%, rgba(160,45,45,0.12) 50%, transparent 50%),
    radial-gradient(1px 1px at 50% 40%, rgba(160,45,45,0.10) 50%, transparent 50%),
    radial-gradient(1px 1px at 70% 80%, rgba(160,45,45,0.13) 50%, transparent 50%),
    radial-gradient(1px 1px at 90% 30%, rgba(160,45,45,0.11) 50%, transparent 50%),
    radial-gradient(1px 1px at 20% 90%, rgba(160,45,45,0.14) 50%, transparent 50%),
    radial-gradient(1px 1px at 60% 10%, rgba(160,45,45,0.10) 50%, transparent 50%),
    radial-gradient(1px 1px at 80% 50%, rgba(160,45,45,0.12) 50%, transparent 50%);
  opacity: 0.5;
  pointer-events: none;
  border-radius: inherit;
  z-index: 0;
}

/* ── 1st place "Throne" — 3D lift + shimmer ──────────────────────────── */
.lb-row.hill-1 {
  animation: first-place-shimmer 3s ease-in-out infinite;
  transform: perspective(800px) translateZ(4px) translateY(-2px) scale(1.02);
  padding: 9px 12px;
  z-index: 3;
}
@keyframes first-place-shimmer {
  0%, 100% { box-shadow: 0 0 10px rgba(240,200,80, 0.07); }
  50% { box-shadow: 0 0 18px rgba(240,200,80, 0.16), 0 0 6px rgba(240,200,80, 0.07); }
}

/* Gold gradient sweep across 1st place row */
.lb-row.hill-1::after {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent 0%, rgba(240,200,80,0.02) 45%, rgba(240,200,80,0.05) 50%, rgba(240,200,80,0.02) 55%, transparent 100%);
  background-size: 300% 100%;
  animation: first-sweep 4s ease-in-out infinite;
  pointer-events: none;
  z-index: 0;
}
@keyframes first-sweep {
  0% { background-position: 300% 0; }
  100% { background-position: -300% 0; }
}

/* Coronation flash — when new player takes #1 */
.lb-row.hill-1.coronation {
  animation: coronation-burst 0.6s ease-out;
}
@keyframes coronation-burst {
  0% { transform: perspective(800px) translateZ(4px) translateY(-2px) scale(1.02); box-shadow: 0 0 10px rgba(240,200,80, 0.12); }
  30% { transform: perspective(800px) translateZ(8px) translateY(-2px) scale(1.05); box-shadow: 0 0 30px rgba(240,200,80, 0.3), 0 0 60px rgba(240,200,80, 0.12); }
  100% { transform: perspective(800px) translateZ(4px) translateY(-2px) scale(1.02); box-shadow: 0 0 10px rgba(240,200,80, 0.12); }
}

/* ── Last place "ДНО" danger zone ─────────────────────────────────── */
.lb-row.hill-6 {
  animation: last-place-pulse 2.5s ease-in-out infinite, sinkBob 3s ease-in-out infinite;
  filter: brightness(0.88);
  box-shadow: inset 0 0 20px rgba(200, 40, 40, 0.12);
  background-image:
    repeating-linear-gradient(
      45deg,
      transparent, transparent 8px,
      rgba(160, 45, 45, 0.04) 8px, rgba(160, 45, 45, 0.04) 16px
    );
}
@keyframes last-place-pulse {
  0%, 100% { border-left-color: rgba(160,45,45, 0.35); }
  50% { border-left-color: rgba(200,60,60, 0.55); box-shadow: inset 0 0 20px rgba(160,45,45, 0.15); }
}
@keyframes sinkBob {
  0%, 100% { transform: translateY(0px); }
  50% { transform: translateY(2px); }
}

/* ── "On fire" streak (3+ consecutive score gains) ────────────────── */
.lb-row.on-fire {
  animation: fire-border 1.5s ease-in-out infinite;
  position: relative;
}

@keyframes fire-border {
  0%, 100% { box-shadow: inset 0 0 8px rgba(255, 120, 30, 0.15); }
  50% { box-shadow: inset 0 0 16px rgba(255, 120, 30, 0.3), 0 0 10px rgba(255, 120, 30, 0.12); }
}

/* Ember particles on fire rows */
.lb-row.on-fire .lb-place::before,
.lb-row.on-fire .lb-place::after,
.lb-row.on-fire .lb-score-area::before {
  content: '';
  position: absolute;
  width: 3px;
  height: 3px;
  border-radius: 50%;
  background: rgba(255, 140, 40, 0.8);
  pointer-events: none;
  z-index: 5;
}
.lb-row.on-fire .lb-place::before {
  left: 6px;
  animation: ember-float 1.8s ease-out infinite;
}
.lb-row.on-fire .lb-place::after {
  left: 18px;
  animation: ember-float 2.2s ease-out infinite 0.4s;
}
.lb-row.on-fire .lb-score-area::before {
  right: 8px;
  animation: ember-float 2.5s ease-out infinite 0.8s;
}

@keyframes ember-float {
  0% { opacity: 0.8; transform: translateY(0) scale(1); }
  50% { opacity: 0.5; }
  100% { opacity: 0; transform: translateY(-18px) scale(0.3); }
}

.lb-row.in-harm-range {
  border-left: 3px solid var(--accent-red-dim);
}

.harm-range-dot {
  display: inline-block;
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--accent-red);
  margin-left: 2px;
  vertical-align: middle;
  box-shadow: 0 0 4px rgba(239, 128, 128, 0.5);
}

.lb-row.is-bot { opacity: 0.7; }

.lb-row.is-protected {
  opacity: 0.5;
  position: relative;
}
.lb-row.is-protected::after {
  content: '';
  position: absolute;
  inset: 0;
  background: repeating-linear-gradient(
    -45deg, transparent, transparent 4px,
    rgba(255, 60, 60, 0.06) 4px, rgba(255, 60, 60, 0.06) 8px
  );
  border-radius: inherit;
  pointer-events: none;
}

/* ── Place column — vertical flex with crown / number / ДНО ──────── */
.lb-place {
  width: 28px;
  display: flex;
  flex-direction: column;
  align-items: center;
  font-size: 14px;
  font-weight: 800;
  color: var(--hill-place, var(--text-muted));
  font-family: var(--font-mono);
  line-height: 1.1;
}

.place-number {
  color: inherit;
}

.lb-prefix {
  font-size: 13px;
  margin-right: 1px;
}

/* ── Rank crown (1st place) ─────────────────────────────────────────── */
.rank-crown {
  font-size: 14px;
  line-height: 1;
  color: #f0c850;
  text-shadow: 0 0 8px rgba(240,200,80, 0.7), 0 0 16px rgba(240,200,80, 0.35);
  animation: crownDrop 0.6s cubic-bezier(0.34, 1.56, 0.64, 1) both, crown-glow 2s ease-in-out infinite 0.6s, crown-float 3s ease-in-out infinite 0.6s;
}

@keyframes crownDrop {
  0% { transform: translateY(-20px); opacity: 0; }
  70% { transform: translateY(2px); opacity: 1; }
  100% { transform: translateY(0); opacity: 1; }
}

@keyframes crown-glow {
  0%, 100% { text-shadow: 0 0 8px rgba(240,200,80, 0.7), 0 0 16px rgba(240,200,80, 0.35); }
  50% { text-shadow: 0 0 12px rgba(240,200,80, 0.9), 0 0 24px rgba(240,200,80, 0.5); }
}

@keyframes crown-float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-1.5px); }
}

/* ── ДНО label (last place) ─────────────────────────────────────────── */
.dno-label {
  font-size: 7px;
  font-weight: 800;
  color: #a03030;
  letter-spacing: 0.8px;
  line-height: 1;
  text-shadow: 0 0 6px rgba(160,45,45, 0.4);
  animation: dno-pulse 1.8s ease-in-out infinite;
}
@keyframes dno-pulse {
  0%, 100% { color: #a03030; text-shadow: 0 0 6px rgba(160,45,45, 0.4); }
  50% { color: #d04040; text-shadow: 0 0 10px rgba(200,50,50, 0.7), 0 0 18px rgba(200,50,50, 0.3); }
}

.lb-avatar {
  width: 34px;
  height: 34px;
  flex-shrink: 0;
  position: relative;
}

.avatar-img {
  width: 34px;
  height: 34px;
  border-radius: var(--radius);
  object-fit: cover;
  border: 1.5px solid var(--border-subtle);
  transition: border-color 0.3s;
}

.lb-row.hill-1 .avatar-img {
  border-color: rgba(240,200,80, 0.4);
}
.lb-row.hill-6 .avatar-img {
  filter: saturate(0.65);
}

.avatar-placeholder {
  width: 34px;
  height: 34px;
  border-radius: var(--radius);
  background: var(--bg-inset);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 13px;
  color: var(--text-dim);
  border: 1px solid var(--border-subtle);
}

.ready-dot, .block-dot {
  position: absolute;
  bottom: -2px;
  right: -2px;
  width: 9px;
  height: 9px;
  border-radius: 50%;
  border: 2px solid var(--bg-secondary);
}

.ready-dot { background: var(--accent-green); box-shadow: var(--glow-green); }
.block-dot { background: var(--accent-blue); }

.lb-info { flex: 1; min-width: 0; }

.lb-name {
  display: flex;
  align-items: center;
  gap: 4px;
}

.player-name {
  font-weight: 700;
  font-size: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--text-primary);
}

.badge {
  padding: 1px 5px;
  border-radius: 3px;
  font-size: 8px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.bot-badge { background: var(--text-dim); color: var(--bg-primary); }
.l-badge { background: #c03030; color: #fff; }
.vuln-badge {
  background: rgba(0, 255, 65, 0.15);
  color: #00ff41;
  border: 1px solid rgba(0, 255, 65, 0.3);
  text-shadow: 0 0 4px rgba(0, 255, 65, 0.5);
  animation: vuln-pulse 2s ease-in-out infinite;
}
.patched-badge {
  background: rgba(0, 255, 65, 0.06);
  color: rgba(0, 255, 65, 0.4);
  border: 1px solid rgba(0, 255, 65, 0.15);
}
@keyframes vuln-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}

.lb-custom {
  font-size: 11px;
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
  width: 20px;
  height: 20px;
  vertical-align: middle;
  display: inline;
  margin: 0 2px;
}

.lb-character { font-size: 10px; color: var(--text-muted); }
.lb-character.masked-name { color: var(--text-dim); font-style: italic; }

.masked-avatar { opacity: 0.5; filter: grayscale(0.4); }

.lb-stats {
  display: flex;
  gap: 3px;
  font-size: 10px;
  font-family: var(--font-mono);
}

.stat { white-space: nowrap; display: flex; align-items: center; gap: 1px; }

.stat-guess-input {
  width: 26px;
  height: 18px;
  padding: 0 2px;
  border: 1px solid var(--border-subtle);
  border-radius: 3px;
  background: var(--bg-inset);
  color: var(--accent-orange);
  font-family: var(--font-mono);
  font-size: 10px;
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

.stat-guess-input::placeholder { color: var(--text-dim); font-style: italic; }

.stat-guess-input:focus {
  border-color: var(--accent-orange);
}

.score-hidden { color: var(--text-dim); font-style: italic; }

.score-value.score-up {
  animation: score-flash-up 1.5s ease-out;
}
.score-value.score-down {
  animation: score-flash-down 1.5s ease-out;
}

@keyframes score-flash-up {
  0% { color: var(--accent-green); transform: scale(1.4); text-shadow: 0 0 12px rgba(63, 167, 61, 0.5); }
  50% { text-shadow: 0 0 8px rgba(63, 167, 61, 0.3); }
  100% { color: var(--accent-gold); transform: scale(1); text-shadow: 0 0 6px rgba(240, 200, 80, 0.15); }
}
@keyframes score-flash-down {
  0% { color: var(--accent-red); transform: scale(1.4); text-shadow: 0 0 12px rgba(239, 128, 128, 0.5); }
  50% { text-shadow: 0 0 8px rgba(239, 128, 128, 0.3); }
  100% { color: var(--accent-gold); transform: scale(1); text-shadow: 0 0 6px rgba(240, 200, 80, 0.15); }
}

.lb-score-area {
  display: flex;
  align-items: center;
  gap: 5px;
  min-width: 80px;
  position: relative;
}

.score-value {
  font-size: 14px;
  font-weight: 800;
  min-width: 24px;
  text-align: right;
  color: var(--accent-gold);
  font-family: var(--font-mono);
  text-shadow: 0 0 6px rgba(240, 200, 80, 0.15);
  transition: color 0.3s, text-shadow 0.3s, transform 0.3s;
}

/* Predict inline button */
.lb-predict { flex-shrink: 0; }

.predict-btn {
  padding: 3px 8px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  background: var(--bg-inset);
  color: var(--text-muted);
  font-size: 11px;
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
  border-color: rgba(180, 150, 255, 0.3);
  background: rgba(180, 150, 255, 0.06);
}

/* Locked prediction (confirmed, read-only) */
.lb-predict-locked { flex-shrink: 0; }

.predict-locked-text {
  padding: 3px 8px;
  border-radius: var(--radius);
  background: rgba(180, 150, 255, 0.04);
  border: 1px solid rgba(180, 150, 255, 0.15);
  color: var(--accent-purple);
  font-size: 11px;
  opacity: 0.7;
}

/* ── TransitionGroup FLIP animation for position swaps — spring ─────── */
.hill-move {
  transition: transform 0.6s cubic-bezier(0.34, 1.56, 0.64, 1);
  animation: hill-move-pulse 0.6s cubic-bezier(0.34, 1.56, 0.64, 1);
}
@keyframes hill-move-pulse {
  0% { scale: 1; }
  50% { scale: 1.02; }
  100% { scale: 1; }
}
.hill-enter-active {
  transition: opacity 0.5s ease, transform 0.5s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.hill-leave-active {
  transition: opacity 0.25s ease, transform 0.25s ease;
  position: absolute;
  width: 100%;
}
.hill-enter-from {
  opacity: 0;
  transform: translateX(-20px) scale(0.96);
}
.hill-leave-to {
  opacity: 0;
  transform: translateX(20px) scale(0.96);
}

/* ── Attack stamp micro-interaction ──────────────────────────────── */
.lb-row.attack-stamp {
  animation: attack-stamp-anim 0.35s cubic-bezier(0.34, 1.56, 0.64, 1);
  border-color: rgba(224, 85, 69, 0.5) !important;
}

@keyframes attack-stamp-anim {
  0% { transform: scale(1) rotate(0deg); }
  30% { transform: scale(1.06) rotate(2deg); }
  60% { transform: scale(0.98) rotate(-1deg); }
  100% { transform: scale(1) rotate(0deg); }
}

/* ── Mobile responsive — larger touch targets ──────────────────── */
@media (max-width: 768px) {
  .lb-row {
    padding: 12px 10px;
    gap: 6px;
    min-height: 48px;
  }

  .lb-row.can-click {
    /* 44px minimum touch target */
    min-height: 48px;
  }

  .avatar-img {
    width: 38px;
    height: 38px;
  }
  .lb-avatar {
    width: 38px;
    height: 38px;
  }

  .player-name {
    font-size: 13px;
  }
  .lb-character {
    font-size: 11px;
  }

  /* Hide mini stats on small screens to save space */
  .lb-stats {
    display: none;
  }

  .predict-btn {
    min-width: 44px;
    min-height: 44px;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 6px 10px;
    font-size: 12px;
  }
}

@media (max-width: 480px) {
  .lb-place {
    width: 22px;
    font-size: 12px;
  }
  .rank-crown {
    font-size: 12px;
  }
  .lb-score-area {
    min-width: 50px;
  }
  .score-value {
    font-size: 12px;
  }
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
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
}

.predict-dropdown {
  width: 300px;
  max-height: 380px;
  background: var(--bg-card, #2d2b31);
  border: 1px solid var(--border-color, #3a3640);
  border-radius: var(--radius-lg, 10px);
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.7);
  overflow: hidden;
}

.predict-search {
  width: 100%;
  padding: 10px 14px;
  border: none;
  border-bottom: 1px solid var(--border-subtle, #2c2a33);
  background: transparent;
  color: var(--text-primary, #f3f3ea);
  font-size: 13px;
  outline: none;
}

.predict-list {
  max-height: 300px;
  overflow-y: auto;
}

.predict-option {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 7px 14px;
  border: none;
  background: transparent;
  color: var(--text-secondary, #eceff2);
  font-size: 13px;
  text-align: left;
  cursor: pointer;
  transition: background 0.1s;
}

.predict-option:hover {
  background: var(--bg-card-hover, #413f46);
  color: var(--text-primary, #f3f3ea);
}

.predict-option-avatar {
  width: 26px;
  height: 26px;
  border-radius: 4px;
  object-fit: cover;
  flex-shrink: 0;
  border: 1px solid var(--border-subtle, #2c2a33);
}

.predict-option-stats {
  margin-left: auto;
  font-size: 9px;
  font-family: var(--font-mono, monospace);
  color: var(--text-muted, #949ca4);
  white-space: nowrap;
}

.predict-empty {
  padding: 16px;
  text-align: center;
  color: var(--text-muted, #949ca4);
  font-size: 12px;
}

/* ── Drop animation ── */
.lb-row.dropped {
  animation: drop-shake 0.6s ease-out;
}
@keyframes drop-shake {
  0% { transform: translateY(0); }
  15% { transform: translateY(-3px); }
  30% { transform: translateY(6px); }
  45% { transform: translateY(-2px); }
  60% { transform: translateY(3px); }
  75% { transform: translateY(-1px); }
  100% { transform: translateY(0); }
}

/* Badge on the score area */
.drop-badge {
  position: absolute;
  right: -4px;
  top: -2px;
  font-size: 9px;
  font-weight: 900;
  color: white;
  background: var(--accent-red-dim, #a02d2d);
  padding: 0 5px;
  border-radius: 6px;
  line-height: 15px;
  animation: drop-badge-pop 0.5s ease-out;
  z-index: 2;
  border: 1px solid var(--accent-red, #ef8080);
}
@keyframes drop-badge-pop {
  0% { transform: scale(0); opacity: 0; }
  50% { transform: scale(1.3); }
  100% { transform: scale(1); opacity: 1; }
}

/* ── Drop overlay (full-row sweep) ── */
.drop-overlay {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 5;
  overflow: hidden;
  border-radius: inherit;
}

/* Red streak sweeps top-to-bottom */
.drop-streak {
  position: absolute;
  top: -100%;
  left: 0;
  right: 0;
  height: 100%;
  background: linear-gradient(
    180deg,
    transparent 0%,
    rgba(239, 128, 128, 0.25) 40%,
    rgba(200, 60, 60, 0.35) 50%,
    rgba(239, 128, 128, 0.25) 60%,
    transparent 100%
  );
  animation: drop-sweep 0.7s ease-in-out forwards;
}
@keyframes drop-sweep {
  0% { top: -100%; }
  100% { top: 100%; }
}

/* Downward arrow */
.drop-arrow {
  position: absolute;
  left: 50%;
  top: -16px;
  transform: translateX(-50%);
  font-size: 16px;
  color: var(--accent-red);
  text-shadow: 0 0 8px rgba(239, 128, 128, 0.8);
  animation: drop-arrow-fall 0.8s cubic-bezier(0.34, 1.56, 0.64, 1) forwards;
  z-index: 6;
}
@keyframes drop-arrow-fall {
  0% { top: -16px; opacity: 0; transform: translateX(-50%) scale(0.5); }
  30% { opacity: 1; transform: translateX(-50%) scale(1.2); }
  50% { top: calc(50% - 8px); transform: translateX(-50%) scale(1); }
  100% { top: calc(50% - 8px); opacity: 0; transform: translateX(-50%) scale(0.8) translateY(8px); }
}

/* Big drop count number */
.drop-count {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%) scale(0);
  font-size: 22px;
  font-weight: 900;
  color: var(--accent-red);
  text-shadow: 0 0 12px rgba(239, 128, 128, 0.6), 0 2px 4px rgba(0, 0, 0, 0.4);
  animation: drop-count-pop 1s cubic-bezier(0.34, 1.56, 0.64, 1) forwards;
  z-index: 6;
  font-family: var(--font-mono);
}
@keyframes drop-count-pop {
  0% { transform: translateY(-50%) scale(0); opacity: 0; }
  25% { transform: translateY(-50%) scale(1.3); opacity: 1; }
  40% { transform: translateY(-50%) scale(0.95); }
  55% { transform: translateY(-50%) scale(1.05); }
  70% { transform: translateY(-50%) scale(1); opacity: 1; }
  100% { transform: translateY(-50%) scale(1); opacity: 0; }
}

/* Transition for overlay enter/leave */
.drop-overlay-enter-active { transition: opacity 0.1s; }
.drop-overlay-leave-active { transition: opacity 0.5s 3s; }
.drop-overlay-enter-from { opacity: 0; }
.drop-overlay-leave-to { opacity: 0; }
</style>
