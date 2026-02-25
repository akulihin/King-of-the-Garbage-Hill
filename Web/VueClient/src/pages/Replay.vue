<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useGameStore } from 'src/store/game'
import { useReplayStore } from 'src/store/replay'
import Leaderboard from 'src/components/Leaderboard.vue'
import PlayerCard from 'src/components/PlayerCard.vue'
import SkillsPanel from 'src/components/SkillsPanel.vue'
import FightAnimation from 'src/components/FightAnimation.vue'
import MediaMessages from 'src/components/MediaMessages.vue'
import DeathNote from 'src/components/DeathNote.vue'

const props = defineProps<{ gameId: string }>()
const store = useGameStore()
const replayStore = useReplayStore()
const router = useRouter()
const route = useRoute()

const copied = ref(false)

/** Map Discord custom emoji names to local /art/emojis/ images. */
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

const letopis = computed(() => {
  const gs = store.gameState
  if (!gs) return ''
  return gs.fullChronicle || gs.allGlobalLogs || ''
})

const myPlayer = computed(() => {
  if (!store.gameState?.myPlayerId) return null
  return store.gameState.players.find(p => p.playerId === store.gameState!.myPlayerId) ?? null
})

const personalLogs = computed(() => {
  return myPlayer.value?.status.personalLogs || ''
})

const previousRoundLogs = computed(() => {
  return myPlayer.value?.status.previousRoundLogs || ''
})

const isViewingKira = computed(() => myPlayer.value?.isKira ?? false)

// ── Score combo parsing (replicated from Game.vue) ───────────────

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
    l => l.includes('Победа') && l.includes('Морали'),
    l => l.includes('скинули'),
    l => l.includes('обманул'),
    l => l.includes('Класс'),
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

    const html = clean
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/__(.*?)__/g, '<u>$1</u>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/~~(.*?)~~/g, '<del>$1</del>')

    return { raw: clean, html, type, comboCount }
  })
}

function filterFightLines(text: string): string {
  if (!text) return ''
  return text.split('\n').filter(line => !line.includes('⟶') && !line.includes('→') && !line.includes('Раунд #')).join('\n')
}

function mergeEvents(): string {
  const personal = myPlayer.value?.status.personalLogs || ''
  const global = filterFightLines(store.gameState?.globalLogs || '')
  const parts: string[] = []
  if (personal.trim()) parts.push(personal)
  if (global.trim()) parts.push(global)
  return parts.join('\n')
}

const currentLogEntriesAll = computed(() => parsePrevLogs(mergeEvents() || ''))
const prevLogEntriesAll = computed(() => parsePrevLogs(myPlayer.value?.status.previousRoundLogs || ''))

const scoreEntries = computed(() => [
  ...currentLogEntriesAll.value.filter((e: PrevLogEntry) => e.type === 'gold'),
  ...prevLogEntriesAll.value.filter((e: PrevLogEntry) => e.type === 'gold'),
])

// ── Round / Player navigation ──────────────────────────────────────

function prevRound() {
  replayStore.setRound(replayStore.currentRound - 1)
}
function nextRound() {
  replayStore.setRound(replayStore.currentRound + 1)
}
function selectPlayer(idx: number) {
  replayStore.setPlayer(idx)
}

function shareUrl() {
  const hash = replayStore.replayData?.replayHash ?? props.gameId
  const url = new URL(window.location.origin + `/replay/${hash}`)
  url.searchParams.set('round', String(replayStore.currentRound))
  url.searchParams.set('player', String(replayStore.currentPlayerIndex))
  navigator.clipboard.writeText(url.toString())
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}

function goToLobby() {
  router.push('/')
}

// ── Sync replay state → game store ─────────────────────────────────

watch(() => replayStore.computedGameState, (gs) => {
  if (gs) {
    store.gameState = gs
    // Also set story if available
    if (replayStore.replayData?.story) {
      store.gameStory = replayStore.replayData.story
    }
  }
}, { immediate: true })

// Update URL query params when round/player changes
watch([() => replayStore.currentRound, () => replayStore.currentPlayerIndex], ([round, player]) => {
  router.replace({
    query: { round: String(round), player: String(player) },
  })
})

// ── Lifecycle ───────────────────────────────────────────────────────

onMounted(async () => {
  await replayStore.loadReplay(props.gameId)
  // Apply URL params
  const roundParam = route.query.round
  const playerParam = route.query.player
  if (roundParam) replayStore.setRound(Number(roundParam))
  if (playerParam) replayStore.setPlayer(Number(playerParam))
})

onUnmounted(() => {
  replayStore.$reset()
  store.gameState = null
  store.gameStory = null
})
</script>

<template>
  <div class="replay-page">
    <!-- Loading -->
    <div v-if="replayStore.isLoading" class="loading">
      <p>Loading replay...</p>
    </div>

    <!-- Error -->
    <div v-else-if="replayStore.error" class="loading">
      <p>{{ replayStore.error }}</p>
      <button class="btn btn-primary" @click="goToLobby">Back to Lobby</button>
    </div>

    <!-- Replay loaded -->
    <div v-else-if="store.gameState && replayStore.replayData" class="game-layout">
      <!-- Left: Player info -->
      <div class="game-left">
        <PlayerCard
          v-if="myPlayer"
          :player="myPlayer"
          :is-me="true"
          :resist-flash="[]"
          :justice-reset="false"
          :score-entries="scoreEntries"
          :score-anim-ready="true"
        />
      </div>

      <!-- Center: Header + Navigation + Fight + Logs + Leaderboard -->
      <div class="game-center">
        <!-- Replay header -->
        <div class="game-header">
          <button class="btn btn-ghost btn-sm" @click="goToLobby">
            ← Lobby
          </button>
          <div class="header-center">
            <span class="replay-badge">REPLAY</span>
            <span class="mode-badge">{{ store.gameState.gameMode }}</span>
          </div>
          <div class="header-right">
            <button class="btn btn-ghost btn-sm" @click="shareUrl">
              {{ copied ? 'Copied!' : 'Share' }}
            </button>
          </div>
        </div>

        <!-- Round navigation -->
        <div class="round-nav">
          <button class="btn btn-ghost btn-sm" :disabled="replayStore.currentRound <= 1" @click="prevRound">
            ←
          </button>
          <span class="round-badge">
            Round {{ replayStore.currentRound }} / {{ replayStore.totalRounds }}
          </span>
          <button class="btn btn-ghost btn-sm" :disabled="replayStore.currentRound >= replayStore.totalRounds" @click="nextRound">
            →
          </button>
        </div>

        <!-- Player selector -->
        <div class="player-selector">
          <div
            v-for="(ps, idx) in replayStore.replayData.playerSummaries"
            :key="ps.playerId"
            class="player-avatar-btn"
            :class="{ active: idx === replayStore.currentPlayerIndex }"
            @click="selectPlayer(idx)"
          >
            <img :src="ps.characterAvatar" :alt="ps.characterName" class="player-avatar-img" />
            <span class="player-avatar-name">{{ ps.discordUsername }}</span>
            <span class="player-avatar-place">#{{ ps.finalPlace }}</span>
          </div>
        </div>

        <!-- Fight Panel / Death Note -->
        <div class="log-panel card fight-panel">
          <DeathNote
            v-if="isViewingKira && myPlayer?.deathNote"
            :death-note="myPlayer.deathNote"
            :players="store.gameState.players"
            :my-player-id="myPlayer.playerId"
            :character-names="store.gameState.allCharacterNames || []"
            :character-catalog="store.gameState.allCharacters || []"
            :is-finished="true"
            :moral="myPlayer.character.moralDisplay"
          />
          <FightAnimation
            v-else
            :fights="store.gameState.fightLog || []"
            :letopis="letopis"
            :game-story="store.gameStory"
            :players="store.gameState.players"
            :my-player-id="myPlayer?.playerId"
            :predictions="myPlayer?.predictions"
            :is-admin="true"
            :character-catalog="store.gameState.allCharacters || []"
          />
        </div>

        <!-- Media Messages -->
        <MediaMessages
          v-if="myPlayer?.status.mediaMessages?.length"
          :messages="myPlayer.status.mediaMessages"
        />

        <!-- Direct Messages -->
        <div
          v-if="myPlayer?.status.directMessages?.length"
          class="direct-messages"
        >
          <div
            v-for="(msg, idx) in myPlayer.status.directMessages"
            :key="idx"
            class="dm-item"
            v-html="formatLogs(msg)"
          />
        </div>

        <!-- Logs -->
        <div class="logs-row-top">
          <div class="log-panel card events-panel">
            <div v-if="personalLogs" class="log-content" v-html="formatLogs(personalLogs)"></div>
            <div v-else class="log-empty">No personal logs this round.</div>
          </div>
          <div class="log-panel card events-panel prev-logs-panel">
            <div v-if="previousRoundLogs" class="log-content" v-html="formatLogs(previousRoundLogs)"></div>
            <div v-else class="log-empty">No previous round logs.</div>
          </div>
        </div>

        <!-- Leaderboard -->
        <div class="lb-action-block">
          <Leaderboard
            :players="store.gameState.players"
            :my-player-id="myPlayer?.playerId"
            :can-attack="false"
            :predictions="myPlayer?.predictions"
            :character-names="store.gameState.allCharacterNames || []"
            :character-catalog="store.gameState.allCharacters || []"
            :is-admin="true"
            :round-no="store.gameState.roundNo"
            :confirmed-predict="true"
            :fight-log="store.gameState.fightLog || []"
            :is-kira="false"
            :death-note="undefined"
            :is-bug="false"
          />
        </div>

        <div class="finished-actions">
          <button class="btn btn-primary btn-lg" @click="goToLobby">
            Back to Lobby
          </button>
        </div>
      </div>

      <!-- Right: Skills / Passives -->
      <div class="game-right">
        <SkillsPanel v-if="myPlayer" :player="myPlayer" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.replay-page {
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

.game-layout {
  display: grid;
  grid-template-columns: 280px 1fr 260px;
  gap: 10px;
  align-items: start;
}

.game-left, .game-right {
  position: sticky;
  top: 10px;
}

.game-center {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
}

/* Header */
.game-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 10px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
}

.header-center {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 6px;
}

.replay-badge {
  background: var(--kh-c-secondary-purple-500, rgba(180, 100, 255, 0.2));
  color: var(--accent-purple, #b464ff);
  padding: 2px 10px;
  border-radius: var(--radius);
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 1px;
  border: 1px solid var(--accent-purple, #b464ff);
}

.mode-badge {
  padding: 2px 10px;
  border-radius: var(--radius);
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  background: var(--kh-c-secondary-info-500);
  color: var(--text-primary);
  border: 1px solid var(--accent-blue);
}

/* Round navigation */
.round-nav {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 6px;
}

.round-badge {
  font-size: 14px;
  font-weight: 800;
  color: var(--accent-gold);
  min-width: 120px;
  text-align: center;
}

/* Player selector */
.player-selector {
  display: flex;
  justify-content: center;
  gap: 8px;
  padding: 6px 0;
}

.player-avatar-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  cursor: pointer;
  padding: 4px;
  border-radius: 8px;
  border: 2px solid transparent;
  transition: border-color 0.2s, opacity 0.2s;
  opacity: 0.6;
}

.player-avatar-btn:hover {
  opacity: 0.9;
}

.player-avatar-btn.active {
  border-color: var(--accent-gold);
  opacity: 1;
}

.player-avatar-img {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  object-fit: cover;
}

.player-avatar-name {
  font-size: 10px;
  color: var(--text-secondary);
  margin-top: 2px;
  max-width: 60px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: center;
}

.player-avatar-place {
  font-size: 9px;
  font-weight: 700;
  color: var(--accent-gold);
  font-family: var(--font-mono);
}

/* Panels */
.log-panel {
  padding: 8px;
}

.fight-panel {
  min-height: 80px;
}

.logs-row-top {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.events-panel {
  max-height: 250px;
  overflow-y: auto;
  font-size: 12px;
  line-height: 1.6;
}

.log-content {
  color: var(--text-secondary);
}

.log-empty {
  color: var(--text-dim);
  font-size: 12px;
  text-align: center;
  padding: 12px;
}

.lb-action-block {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.finished-actions {
  text-align: center;
  padding: 12px 0;
}

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

/* Responsive */
@media (max-width: 1024px) {
  .game-layout {
    grid-template-columns: 1fr;
  }
  .game-left, .game-right {
    position: static;
  }
}
</style>
