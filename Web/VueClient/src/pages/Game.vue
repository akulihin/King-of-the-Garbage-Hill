<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import Leaderboard from 'src/components/Leaderboard.vue'
import DeathNote from 'src/components/DeathNote.vue'
import PlayerCard from 'src/components/PlayerCard.vue'
import ActionPanel from 'src/components/ActionPanel.vue'
import SkillsPanel from 'src/components/SkillsPanel.vue'
import FightAnimation from 'src/components/FightAnimation.vue'
import MediaMessages from 'src/components/MediaMessages.vue'
import RoundTimer from 'src/components/RoundTimer.vue'
import Blackjack21 from 'src/components/Blackjack21.vue'
import {
  playAttackSelection,
  playJusticeResetSound,
  playJusticeUpSound,
  setSoundContext,
  getMasterVolume,
  setMasterVolume,
} from 'src/services/sound'

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
  playJusticeResetSound()
  setTimeout(() => { justiceResetFlash.value = false }, 2000)
}

function onJusticeUp() {
  playJusticeUpSound()
}

/** Fight replay ended — trigger score combo animation */
const fightReplayEnded = ref(false)
function onReplayEnded() {
  fightReplayEnded.value = true
}

function onAttack(place: number) {
  void playAttackSelection(store.myPlayer?.character.name)
  void store.attack(place)
}

onMounted(async () => {
  setSoundContext('game')
  if (store.isConnected) {
    await store.joinGame(gameIdNum.value)
  }
})

onUnmounted(() => {
  setSoundContext('menu')
  clearPrevLogTimer()
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

// ── Volume control ──────────────────────────────────────────────────
const volume = ref(getMasterVolume())
const isMuted = computed(() => volume.value === 0)
function onVolumeInput(e: Event) {
  const val = parseFloat((e.target as HTMLInputElement).value)
  volume.value = val
  setMasterVolume(val)
}
function toggleMute() {
  if (volume.value > 0) {
    volume.value = 0
    setMasterVolume(0)
  } else {
    volume.value = 0.8
    setMasterVolume(0.8)
  }
}

const showFinishConfirm = ref(false)
function finishGame() {
  store.finishGame()
  showFinishConfirm.value = false
  router.push('/')
}

/** Map Discord custom emoji names to local /art/emojis/ images (mirrors C# EmojiMap). */
const discordEmojiMap: Record<string, string> = {
  weed: '/art/emojis/weed.png',
  bong: '/art/emojis/bone_1.png',
  WUF: '/art/emojis/wolf_mark.png',
  pet: '/art/emojis/collar.png',
  pepe_down: '/art/emojis/pepe.png',
  sparta: '/art/emojis/spartan_mark.png',
  Spartaneon: '/art/emojis/sparta.png',
  pantheon: '/art/emojis/spartan_mark.png',
  yasuo: '/art/emojis/shame_shame.png',
  broken_shield: '/art/emojis/broken_shield.png',
  yo_filled: '/art/emojis/gambit.png',
  Y_: '/art/emojis/vampyr_mark.png',
  bronze: '/art/emojis/bronze.png',
  plat: '/art/emojis/plat.png',
  393: '/art/emojis/mail_2.png',
  LoveLetter: '/art/emojis/mail_1.png',
  fr: '/art/emojis/friend.png',
  edu: '/art/emojis/learning.png',
  jaws: '/art/emojis/fin.png',
  luck: '/art/emojis/luck.png',
  war: '/art/emojis/war.png',
  volibir: '/art/emojis/voli.png',
  e_: '',
}

/** Convert Discord custom emoji codes to <img> tags (or remove if mapped to empty). */
function convertDiscordEmoji(text: string): string {
  return text.replace(/<:(\w+):\d+>/g, (_match, name: string) => {
    const src = discordEmojiMap[name]
    if (src === undefined) return `[${name}]`
    if (src === '') return ''
    return `<img class="lb-emoji" src="${src}" alt="${name}">`
  })
}

function formatLogs(text: string): string {
  return convertDiscordEmoji(text)
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
function mergeEvents(): string {
  const personal = store.myPlayer?.status.personalLogs || ''
  const global = filterFightLines(store.gameState?.globalLogs || '')
  const parts: string[] = []
  if (personal.trim()) parts.push(personal)
  if (global.trim()) parts.push(global)
  return parts.join('\n')
}
/**
 * "Летопись" — full game chronicle.
 * When the game is finished, uses the server-built FullChronicle (global events + ALL players' personal logs).
 * During gameplay, falls back to the requesting player's own logs + global events.
 */
const letopis = computed(() => {
  // Finished game: use server-built chronicle with all players' logs
  const chronicle = store.gameState?.fullChronicle
  if (chronicle) return chronicle

  // In-progress: show own personal logs + global events
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
    parts.push(`**--- Fight History ---**\n${allGlobal}`)
  }

  return parts.join('\n\n')
})

// ── Animated Previous Round Logs ─────────────────────────────────────
type PrevLogColor = 'purple' | 'gold' | 'green' | 'red' | 'blue' | 'orange' | 'muted'
interface PrevLogEntry {
  raw: string
  html: string
  type: PrevLogColor
  comboCount: number
}

function cleanDiscord(text: string): string {
  return convertDiscordEmoji(text)
    .replace(/\|>Phrase<\|/g, '')
}

function parsePrevLogs(raw: string): PrevLogEntry[] {
  if (!raw) return []
  if (raw.length < 3) return []
  
  // Lines to hide (already shown elsewhere in the UI)
  const hiddenPatterns: ((line: string) => boolean)[] = [
    l => l.includes('Мишень'),
    l => l.includes('Поражение') && l.includes('Морали'),
    l => l.includes('обогнал'),
    l => l.includes('Справедливость'),
    l => l.includes('вреда'),
    l => l.includes('TOO GOOD'),
    l => l.includes('TOO STRONK'),
    l => l.includes('скинули'),
    l => l.includes('напали'),
    l => l.includes('улучшили'),
    l => l.includes('Обмен'),
    l => l.includes('пресанул'),
    l => l.includes('Победа') &&  l.includes('Морали'),
    l => l.includes('скинули'),
    l => l.includes('обманул'),
    l => l.includes('Класс'),
    l => l.includes('обогнал'),
    l => l.includes('обогнал'),
    l => l.includes('обогнал'),
  ]

  const lines = raw.split('\n').filter((l: string) => l.trim() && !hiddenPatterns.some(fn => fn(l)) && l.length > 2)

  return lines.map((line: string) => {
    const clean = cleanDiscord(line)
    let type: PrevLogColor = 'muted'
    let comboCount = 0

    if (/[Сс]килла/i.test(clean) || /Справедливость/i.test(clean) || /Cкилла/i.test(clean) || /Морали/i.test(clean)) {
      type = 'green'  
    } else if (/очков/i.test(clean) && !clean.includes('отнял в общей сумме')) {
      type = 'gold'
      const parenMatch = clean.match(/\(([^)]+)\)/)
      if (parenMatch) {
        comboCount = (parenMatch[1].match(/\+/g) || []).length
      }
    } else if (/Поражение/i.test(clean) || /вреда/i.test(clean) || clean.includes('отнял в общей сумме')) {
      type = 'red'
    } else if (clean.includes(':')) {
      type = 'purple'
    }
    /*
purple
gold
green
red
blue
orange
muted	(Grey)
    */
    const html = clean
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/__(.*?)__/g, '<u>$1</u>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/~~(.*?)~~/g, '<del>$1</del>')

    return { raw: clean, html, type, comboCount }
  })
}

const prevLogEntriesAll = computed(() => parsePrevLogs(store.myPlayer?.status.previousRoundLogs || ''))
const currentLogEntriesAll = computed(() => parsePrevLogs(mergeEvents() || ''))

// Split: "очков" entries go to PlayerCard, rest stay in log panels
const prevLogEntries = computed(() => prevLogEntriesAll.value.filter((e: PrevLogEntry) => e.type !== 'gold'))
const currentLogEntries = computed(() => currentLogEntriesAll.value.filter((e: PrevLogEntry) => e.type !== 'gold'))

// All score entries (from both current + previous logs) for the PlayerCard combo display
const scoreEntries = computed(() => [
  ...currentLogEntriesAll.value.filter((e: PrevLogEntry) => e.type === 'gold'),
  ...prevLogEntriesAll.value.filter((e: PrevLogEntry) => e.type === 'gold'),
])

// Animation: reveal entries one by one
const prevLogVisibleCount = ref(999)
const currentLogVisibleCount = ref(999)
let prevLogTimer: ReturnType<typeof setInterval> | null = null
let prevLogSnapshot = ''
let currentLogTimer: ReturnType<typeof setInterval> | null = null
let currentLogSnapshot = ''

function clearPrevLogTimer() {
  if (prevLogTimer !== null) { clearInterval(prevLogTimer); prevLogTimer = null }
}
function clearCurrentLogTimer() {
  if (currentLogTimer !== null) { clearInterval(currentLogTimer); currentLogTimer = null }
}

watch(() => store.myPlayer?.status.previousRoundLogs, (newVal: string | undefined) => {
  const val = newVal || ''
  if (val === prevLogSnapshot) return
  prevLogSnapshot = val
  fightReplayEnded.value = false
  clearPrevLogTimer()
  if (!val) { prevLogVisibleCount.value = 999; return }
  const count = parsePrevLogs(val).length
  if (count === 0) { prevLogVisibleCount.value = 999; return }
  prevLogVisibleCount.value = 0
  // Defer animation start to next frame so Vue finishes its DOM patch first
  setTimeout(() => {
    let i = 0
    prevLogTimer = setInterval(() => {
      i++
      prevLogVisibleCount.value = i
      if (i >= count) clearPrevLogTimer()
    }, 250)
  }, 50)
}, { immediate: true })

watch(() => mergeEvents(), (newVal: string | undefined) => {
  const val = newVal || ''
  if (val === currentLogSnapshot) return
  currentLogSnapshot = val
  clearCurrentLogTimer()
  if (!val) { currentLogVisibleCount.value = 999; return }
  const count = parsePrevLogs(val).length
  if (count === 0) { currentLogVisibleCount.value = 999; return }
  currentLogVisibleCount.value = 0
  // Defer animation start to next frame so Vue finishes its DOM patch first
  setTimeout(() => {
    let i = 0
    currentLogTimer = setInterval(() => {
      i++
      currentLogVisibleCount.value = i
      if (i >= count) clearCurrentLogTimer()
    }, 250)
  }, 50)
}, { immediate: true })
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
          :score-entries="scoreEntries"
          :score-anim-ready="fightReplayEnded"
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
            <!-- Status chip (moved from ActionPanel) -->
            <span v-if="me && !store.gameState.isFinished" class="status-chip" :class="{ ready: me.status.isReady, waiting: !me.status.isReady }">
              {{ me.status.isReady ? '✓ Ready' : me.status.isSkip ? '⏭ Skip' : '⏳ Your turn' }}
            </span>
          </div>
          <div class="header-right">
            <!-- Volume control -->
            <div class="vol-control" data-sfx-skip-default="true">
              <button class="vol-mute-btn" data-sfx-skip-default="true" :title="isMuted ? 'Unmute' : 'Mute'" @click="toggleMute">
                <span v-if="isMuted" class="vol-icon vol-icon-off">&#x1F507;</span>
                <span v-else-if="volume < 0.4" class="vol-icon">&#x1F508;</span>
                <span v-else-if="volume < 0.75" class="vol-icon">&#x1F509;</span>
                <span v-else class="vol-icon">&#x1F50A;</span>
              </button>
              <input
                type="range"
                class="vol-slider"
                min="0" max="1" step="0.05"
                :value="volume"
                @input="onVolumeInput"
              >
            </div>
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

        <!-- Fight Panel (moved above leaderboard for prominence) -->
        <div class="log-panel card fight-panel">
          <FightAnimation
            :fights="store.gameState.fightLog || []"
            :letopis="letopis"
            :game-story="store.gameStory"
            :players="store.gameState.players"
            :my-player-id="store.myPlayer?.playerId"
            :predictions="store.myPlayer?.predictions"
            :is-admin="store.isAdmin"
            :character-catalog="store.gameState.allCharacters || []"
            @resist-flash="onResistFlash"
            @justice-reset="onJusticeReset"
            @justice-up="onJusticeUp"
            @replay-ended="onReplayEnded"
          />
        </div>

        <!-- Blackjack mini-game for players killed by Death Note -->
        <Blackjack21
          v-if="store.myPlayer?.kiraDeathNoteDead && store.blackjackState"
          :game-id="store.gameState.gameId"
        />

        <!-- Logs: events side-by-side -->
        <div class="logs-row-top">
          
          <div class="log-panel card events-panel">

            <div v-if="currentLogEntries.length" class="prev-logs">
              <div
                v-for="(entry, idx) in currentLogEntries"
                :key="idx"
                class="prev-log-item"
                :class="[
                  'prev-log-' + entry.type,
                  { 'prev-log-visible': idx < currentLogVisibleCount },
                  { 'prev-log-combo': entry.type === 'gold' && entry.comboCount > 0 }
                ]"
              >
                <span class="prev-log-text" v-html="entry.html"></span>
                <span v-if="entry.type === 'gold' && entry.comboCount > 0" class="prev-log-combo-badge">
                  x{{ entry.comboCount + 1 }} combo
                </span>
              </div>
            </div>

            <div v-else class="log-empty">Еще ничего не произошло. Наверное...</div>
          </div>

          <div class="log-panel card events-panel prev-logs-panel">

            <div v-if="prevLogEntries.length" class="prev-logs">
              <div
                v-for="(entry, idx) in prevLogEntries"
                :key="idx"
                class="prev-log-item"
                :class="[
                  'prev-log-' + entry.type,
                  { 'prev-log-visible': idx < prevLogVisibleCount },
                  { 'prev-log-combo': entry.type === 'gold' && entry.comboCount > 0 }
                ]"
              >
                <span class="prev-log-text" v-html="entry.html"></span>
                <span v-if="entry.type === 'gold' && entry.comboCount > 0" class="prev-log-combo-badge">
                  x{{ entry.comboCount + 1 }} combo
                </span>
              </div>
            </div>

            <div v-else class="log-empty">В прошлом раунде ничего не произошло.</div>
          </div>
          
        </div>

        <!-- Leaderboard + Action bar integrated -->
        <div class="lb-action-block">
          <ActionPanel v-if="store.myPlayer && !store.gameState.isFinished" />
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
            :is-kira="store.isKira"
            :death-note="store.myPlayer?.deathNote"
            :is-bug="store.isBug"
            @attack="onAttack"
            @predict="store.predict($event.playerId, $event.characterName)"
          />
          <DeathNote
            v-if="store.isKira && store.myPlayer?.deathNote"
            :death-note="store.myPlayer.deathNote"
            :players="store.gameState.players"
            :my-player-id="store.myPlayer.playerId"
            :character-names="store.gameState.allCharacterNames || []"
            :character-catalog="store.gameState.allCharacters || []"
            :is-finished="store.gameState.isFinished"
            :moral="store.myPlayer.character.moralDisplay"
            @write="store.deathNoteWrite($event.targetPlayerId, $event.characterName)"
            @shinigami-eyes="store.shinigamiEyes()"
          />
        </div>

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
  gap: 5px;
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
  padding: 6px 0;
}


/* ── 3-column layout ────────────────────────────────────────────── */
.game-layout {
  display: grid;
  grid-template-columns: 250px 1fr 250px;
  gap: 6px;
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
  gap: 6px;
  margin-bottom: 5px;
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
  animation: turn-pulse 2s ease-in-out infinite;
}

@keyframes turn-pulse {
  0%, 100% { box-shadow: none; }
  50% { box-shadow: 0 0 8px rgba(230, 148, 74, 0.4); }
}

.header-right {
  display: flex;
  align-items: center;
  gap: 6px;
  position: relative;
}

/* ── Volume control ────────────────────────────────────────────── */
.vol-control {
  display: flex;
  align-items: center;
  gap: 3px;
}
.vol-mute-btn {
  background: none;
  border: none;
  padding: 0 2px;
  cursor: pointer;
  line-height: 1;
}
.vol-icon {
  font-size: 14px;
  opacity: 0.7;
  transition: opacity 0.15s;
}
.vol-mute-btn:hover .vol-icon { opacity: 1; }
.vol-icon-off { opacity: 0.35; }
.vol-slider {
  width: 60px;
  height: 4px;
  -webkit-appearance: none;
  appearance: none;
  background: var(--bg-inset);
  border-radius: 2px;
  outline: none;
  cursor: pointer;
}
.vol-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--accent-gold);
  border: 1.5px solid var(--bg-card);
  cursor: pointer;
  transition: box-shadow 0.15s;
}
.vol-slider::-webkit-slider-thumb:hover {
  box-shadow: 0 0 6px rgba(233, 219, 61, 0.4);
}
.vol-slider::-moz-range-thumb {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--accent-gold);
  border: 1.5px solid var(--bg-card);
  cursor: pointer;
}
.vol-slider::-webkit-slider-runnable-track {
  height: 4px;
  border-radius: 2px;
}
.vol-slider::-moz-range-track {
  height: 4px;
  border-radius: 2px;
  background: var(--bg-inset);
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
  gap: 3px;
  margin-top: 4px;
}

.dm-item {
  padding: 4px 10px;
  background: var(--bg-surface);
  border-left: 2px solid var(--accent-orange);
  border-radius: 0 var(--radius) var(--radius) 0;
  font-size: 12px;
  color: var(--text-primary);
  line-height: 1.5;
}

.dm-item :deep(strong) { color: var(--accent-gold); }
.dm-item :deep(em) { color: var(--accent-blue); }
.dm-item :deep(.lb-emoji) {
  width: 20px;
  height: 20px;
  vertical-align: middle;
  display: inline;
  margin: 0 2px;
}

/* ── Leaderboard + Action bar block ─────────────────────────────── */
.lb-action-block {
  display: flex;
  flex-direction: column;
}

/* ── Logs ────────────────────────────────────────────────────────── */
.logs-row-top {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 5px;
  margin-top: 5px;
  margin-bottom: 6px;
}

@media (max-width: 800px) {
  .logs-row-top { grid-template-columns: 1fr; }
}

.log-panel {
  display: flex;
  flex-direction: column;
}

.events-panel {
  min-height: 80px;
  max-height: 150px;
  padding: 5px 8px;
}

.events-panel :deep(.card-header),
.events-panel .card-header {
  font-size: 11px;
  margin-bottom: 4px;
}

.fight-panel {
  padding: 5px 8px;
  min-height: 200px;
  max-height: 500px;
  overflow-y: auto;
  margin-bottom: 5px;
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

/* ── Animated Previous Round Logs ──────────────────────────────────── */
.prev-logs-panel { max-height: 220px; }

.prev-logs {
  display: flex;
  flex-direction: column;
  gap: 3px;
  overflow-y: auto;
  padding: 4px 2px;
}

.prev-log-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 11px;
  line-height: 1.4;
  border-left: 3px solid transparent;
  opacity: 0;
  transform: translateY(8px) scale(0.97);
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.prev-log-item.prev-log-visible {
  opacity: 1;
  transform: translateY(0) scale(1);
}

.prev-log-text {
  flex: 1;
  color: var(--text-secondary);
  font-family: var(--font-mono);
}

.prev-log-text :deep(strong) { color: var(--accent-gold); }
.prev-log-text :deep(em) { color: var(--accent-blue); }
.prev-log-text :deep(u) { color: var(--accent-green); }
.prev-log-text :deep(.lb-emoji) {
  width: 20px;
  height: 20px;
  vertical-align: middle;
  display: inline;
  margin: 0 2px;
}

/* Log colors */
.prev-log-purple {
  background: rgba(139, 92, 246, 0.06);
  border-left-color: rgba(139, 92, 246, 0.5);
}
.prev-log-gold {
  background: rgba(233, 219, 61, 0.06);
  border-left-color: rgba(233, 219, 61, 0.5);
}
.prev-log-green {
  background: rgba(63, 167, 61, 0.06);
  border-left-color: rgba(63, 167, 61, 0.5);
}
.prev-log-red {
  background: rgba(239, 128, 128, 0.06);
  border-left-color: rgba(239, 128, 128, 0.5);
}
.prev-log-blue {
  background: rgba(100, 160, 255, 0.06);
  border-left-color: rgba(100, 160, 255, 0.5);
}
.prev-log-orange {
  background: rgba(230, 148, 74, 0.06);
  border-left-color: rgba(230, 148, 74, 0.5);
}
.prev-log-muted {
  background: var(--bg-inset);
  border-left-color: var(--border-subtle);
}

/* Combo badge for score lines */
.prev-log-combo-badge {
  font-size: 9px;
  font-weight: 800;
  color: var(--accent-gold);
  background: rgba(233, 219, 61, 0.12);
  padding: 1px 6px;
  border-radius: 4px;
  border: 1px solid rgba(233, 219, 61, 0.25);
  white-space: nowrap;
  flex-shrink: 0;
  animation: combo-pop 0.4s ease;
}

.prev-log-combo .prev-log-text :deep(strong) {
  color: var(--accent-gold);
  text-shadow: 0 0 6px rgba(233, 219, 61, 0.3);
}

@keyframes combo-pop {
  0% { transform: scale(0.5); opacity: 0; }
  60% { transform: scale(1.15); }
  100% { transform: scale(1); opacity: 1; }
}
</style>
