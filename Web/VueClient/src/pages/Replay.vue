<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import type { FightEntry, Player } from 'src/services/signalr'
import { buildShiftedPlayer, getReplaySettlementLogs } from 'src/store/replay'
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
    .replace(/\|>Stat<\|/g, '')
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

// ── Avatar / identity (left = selected character, right = fighting character) ──
const placeTier = computed(() => {
  const place = myPlayer.value?.status?.place ?? 3
  if (place <= 1) return 'place-1'
  if (place <= 2) return 'place-2'
  if (place <= 3) return 'place-3'
  if (place <= 5) return 'place-mid'
  return 'place-last'
})
const charTier = computed(() => myPlayer.value?.character.tier ?? 0)
const rarityLabel = computed(() => {
  switch (charTier.value) {
    case 1: return 'Legendary'
    case 2: return 'Epic'
    case 3: return 'Rare'
    case 4: return 'Uncommon'
    case 5: case 6: return 'Common'
    default: return ''
  }
})
const rarityClass = computed(() => {
  switch (charTier.value) {
    case 1: return 'rarity-legendary'
    case 2: return 'rarity-epic'
    case 3: return 'rarity-rare'
    case 4: return 'rarity-uncommon'
    default: return 'rarity-common'
  }
})
const masteryPoints = computed(() => myPlayer.value?.characterMasteryPoints ?? 0)
const masteryLevel = computed(() => Math.floor(Math.sqrt(masteryPoints.value / 5)))
const masteryTier = computed(() => {
  const lvl = masteryLevel.value
  if (lvl >= 20) return 'diamond'
  if (lvl >= 15) return 'platinum'
  if (lvl >= 10) return 'gold'
  if (lvl >= 5) return 'silver'
  if (lvl >= 1) return 'bronze'
  return 'none'
})

const enemyPlaceTier = computed(() => {
  const place = enemyPlayer.value?.status?.place ?? 3
  if (place <= 1) return 'place-1'
  if (place <= 2) return 'place-2'
  if (place <= 3) return 'place-3'
  if (place <= 5) return 'place-mid'
  return 'place-last'
})

// ── Current fight tracking (for enemy PlayerCard on right side) ──
const currentFight = ref<FightEntry | null>(null)

function onCurrentFightUpdate(fight: FightEntry | null) {
  currentFight.value = fight
}

// ── Fight replay ended tracking (for point feed + fight bonuses) ──
const fightReplayEnded = ref(false)
function onReplayEnded() {
  fightReplayEnded.value = true
}

watch([() => replayStore.currentRound, () => replayStore.currentPlayerIndex], () => {
  fightReplayEnded.value = false
})

const myFightBonuses = computed(() => {
  if (!fightReplayEnded.value) return []
  const log = store.gameState?.fightLog || []
  const myName = myPlayer.value?.discordUsername
  if (!myName || !log.length) return []

  let totalSkill = 0
  let totalJustice = 0
  let totalMoral = 0

  for (const f of log) {
    const isAttacker = f.attackerName === myName
    const isDefender = f.defenderName === myName
    if (!isAttacker && !isDefender) continue

    if (isAttacker) {
      totalSkill += (f.skillGainedFromTarget || 0) + (f.skillGainedFromClassAttacker || 0)
      totalMoral += (f.attackerMoralChange || 0)
      if (f.outcome === 'loss') totalJustice += (f.justiceChange || 0)
    }
    if (isDefender) {
      totalSkill += (f.skillGainedFromClassDefender || 0)
      totalMoral += (f.defenderMoralChange || 0)
      if (f.outcome === 'win') totalJustice += (f.justiceChange || 0)
    }
  }

  const bonuses: { label: string; value: string; cssClass: string }[] = []
  if (totalSkill > 0) bonuses.push({ label: 'Skill', value: `+${totalSkill}`, cssClass: 'bonus-skill' })
  if (totalJustice > 0) bonuses.push({ label: 'Justice', value: `+${totalJustice}`, cssClass: 'bonus-justice' })
  if (totalMoral !== 0) bonuses.push({ label: 'Moral', value: `${totalMoral > 0 ? '+' : ''}${totalMoral}`, cssClass: totalMoral > 0 ? 'bonus-moral-up' : 'bonus-moral-down' })

  return bonuses
})

const enemyPlayer = computed<Player | null>(() => {
  const f = currentFight.value
  if (!f || !myPlayer.value) return null
  const round = replayStore.currentRoundData
  if (!round) return null
  const myName = myPlayer.value.discordUsername
  const enemyName = f.attackerName === myName ? f.defenderName
    : f.defenderName === myName ? f.attackerName
    : null
  if (!enemyName) return null
  // Get full unstripped player data from replay round
  const enemyRp = round.players.find(rp => rp.playerState.discordUsername === enemyName)
  if (!enemyRp) return null
  const preFightRp = replayStore.currentPreFightPlayers.find(rp => rp.playerId === enemyRp.playerId)
  return buildShiftedPlayer(
    enemyRp.playerState,
    preFightRp?.playerState,
    getReplaySettlementLogs(enemyRp, replayStore.includeLegacyFinalBuffer),
  )
})

// ── Score combo parsing (replicated from Game.vue) ───────────────

type PrevLogColor = 'purple' | 'gold' | 'green' | 'red' | 'blue' | 'orange' | 'muted'
interface PrevLogEntry {
  raw: string
  html: string
  type: PrevLogColor
  comboCount: number
  isPhrase: boolean
}

function cleanDiscord(text: string): string {
  return convertDiscordEmoji(text)
    .replace(/\|>Stat<\|/g, '')
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
    const isPhrase = line.includes('|>Phrase<|') && !line.includes('|>Stat<|')
    const clean = cleanDiscord(line)
    let type: PrevLogColor = 'muted'
    let comboCount = 0

    if (isPhrase) {
      type = 'purple'
    } else if (/[Сс]килла/i.test(clean) || /Справедливость/i.test(clean) || /Cкилла/i.test(clean) || /Морали/i.test(clean)) {
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

    return { raw: clean, html, type, comboCount, isPhrase }
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

const currentLogEntries = computed(() => currentLogEntriesAll.value.filter((e: PrevLogEntry) => e.type !== 'gold'))
const prevLogEntries = computed(() => prevLogEntriesAll.value.filter((e: PrevLogEntry) => e.type !== 'gold'))


// ── Round / Player navigation ──────────────────────────────────────

function prevRound() {
  replayStore.previousRound()
}
function nextRound() {
  replayStore.nextRound()
}
function selectPlayer(idx: number) {
  replayStore.setPlayer(idx)
}

function shareUrl() {
  const hash = replayStore.replayData?.replayHash ?? props.gameId
  const url = new URL(window.location.origin + `/replay/${hash}`)
  url.searchParams.set('round', String(replayStore.currentRound))
  url.searchParams.set('player', String(replayStore.currentPlayerIndex))
  if (replayStore.currentFightIndex > 0) {
    url.searchParams.set('fight', String(replayStore.currentFightIndex))
  }
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

// Update URL query params when round/player/fight changes
watch([() => replayStore.currentRound, () => replayStore.currentPlayerIndex, () => replayStore.currentFightIndex], ([round, player, fight]) => {
  const query: Record<string, string> = { round: String(round), player: String(player) }
  if (fight > 0) query.fight = String(fight)
  router.replace({ query })
})

// ── Lifecycle ───────────────────────────────────────────────────────

onMounted(async () => {
  await replayStore.loadReplay(props.gameId)
  // Apply URL params
  const roundParam = route.query.round
  const playerParam = route.query.player
  const fightParam = route.query.fight
  if (roundParam) replayStore.setRound(Number(roundParam))
  if (playerParam) replayStore.setPlayer(Number(playerParam))
  if (fightParam) replayStore.setFight(Number(fightParam))
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
      <!-- Left: Selected character avatar + Player info -->
      <div class="game-left">
        <div v-if="myPlayer" class="gr-avatar-section">
          <div class="gr-avatar-wrap" :class="[placeTier]">
            <img
              v-if="myPlayer.character.avatarCurrent"
              :src="myPlayer.character.avatarCurrent"
              :alt="myPlayer.character.name"
              class="gr-avatar-img"
            >
            <div v-else class="gr-avatar-fallback">
              {{ myPlayer.character.name.charAt(0) }}
            </div>
          </div>
          <div class="gr-identity">
            <div class="gr-name">
              {{ myPlayer.character.name }}
              <span v-if="charTier > 0" class="rarity-badge" :class="rarityClass">{{ rarityLabel }}</span>
            </div>
            <div v-if="masteryLevel > 0" class="mastery-badge" :class="'mastery-' + masteryTier">
              <span class="mastery-level">{{ masteryLevel }}</span>
              <span class="mastery-label">{{ masteryTier }}</span>
            </div>
            <div class="gr-username">{{ myPlayer.discordUsername }}</div>
          </div>
        </div>
        <PlayerCard
          v-if="myPlayer"
          :player="myPlayer"
          :is-me="true"
          :resist-flash="[]"
          :justice-reset="false"
          :score-breakdown="myPlayer?.status.scoreBreakdown ?? null"
          :score-anim-ready="fightReplayEnded"
          :fight-bonuses="myFightBonuses"
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
          <button class="btn btn-ghost btn-sm" :disabled="!replayStore.canPreviousRound" @click="prevRound">
            ←
          </button>
          <span class="round-badge">
            Round {{ replayStore.displayRound }} / {{ replayStore.totalRounds }}
          </span>
          <button class="btn btn-ghost btn-sm" :disabled="!replayStore.canNextRound" @click="nextRound">
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
            :is-admin="false"
            :show-detailed-factors="true"
            :character-catalog="store.gameState.allCharacters || []"
            :initial-fight-index="replayStore.currentFightIndex"
            fight-style="v1"
            @update:fight-index="replayStore.setFight"
            @update:current-fight="onCurrentFightUpdate"
            @replay-ended="onReplayEnded"
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
            <div v-if="currentLogEntries.length" class="prev-logs">
              <div v-for="(entry, idx) in currentLogEntries" :key="idx"
                class="prev-log-item prev-log-visible"
                :class="['prev-log-' + entry.type, { 'prev-log-phrase': entry.isPhrase }]">
                <span class="prev-log-text" v-html="entry.html"></span>
                <span v-if="entry.type === 'gold' && entry.comboCount > 0" class="prev-log-combo-badge">
                  x{{ entry.comboCount + 1 }} combo
                </span>
              </div>
            </div>
            <div v-else class="log-empty">No personal logs this round.</div>
          </div>
          <div class="log-panel card events-panel prev-logs-panel">
            <div v-if="prevLogEntries.length" class="prev-logs">
              <div v-for="(entry, idx) in prevLogEntries" :key="idx"
                class="prev-log-item prev-log-visible"
                :class="['prev-log-' + entry.type, { 'prev-log-phrase': entry.isPhrase }]">
                <span class="prev-log-text" v-html="entry.html"></span>
                <span v-if="entry.type === 'gold' && entry.comboCount > 0" class="prev-log-combo-badge">
                  x{{ entry.comboCount + 1 }} combo
                </span>
              </div>
            </div>
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

      <!-- Right: Fighting character avatar + Enemy PlayerCard or Skills fallback -->
      <div class="game-right">
        <div v-if="enemyPlayer" class="gr-avatar-section">
          <div class="gr-avatar-wrap" :class="[enemyPlaceTier]">
            <img
              v-if="enemyPlayer.character.avatarCurrent"
              :src="enemyPlayer.character.avatarCurrent"
              :alt="enemyPlayer.character.name"
              class="gr-avatar-img"
            >
            <div v-else class="gr-avatar-fallback">
              {{ enemyPlayer.character.name.charAt(0) }}
            </div>
          </div>
          <div class="gr-identity">
            <div class="gr-name">{{ enemyPlayer.character.name }}</div>
            <div class="gr-username">{{ enemyPlayer.discordUsername }}</div>
          </div>
        </div>
        <PlayerCard
          v-if="enemyPlayer"
          :player="enemyPlayer"
          :is-me="true"
          :resist-flash="[]"
          :justice-reset="false"
          :score-breakdown="null"
          :score-anim-ready="false"
        />
        <SkillsPanel v-else-if="myPlayer" :player="myPlayer" />
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
  grid-template-columns: 280px 1fr 280px;
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
  min-height: 0;
  overflow-y: visible;
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

/* ── Parsed Log Entries (matching Game.vue styling) ──────────────── */
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
  padding: 5px 10px;
  border-radius: 5px;
  font-size: 11px;
  line-height: 1.4;
  border-left: 3px solid transparent;
}

.prev-log-item.prev-log-visible {
  opacity: 1;
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

/* Phrase styling */
.prev-log-phrase {
  padding-left: 16px;
  font-style: italic;
  opacity: 0.85;
  border-left-style: dotted;
  font-size: 10.5px;
}

/* Combo badge */
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

/* ── Avatar section in game-left / game-right ─────────────────── */
.gr-avatar-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 8px 0 10px;
}
.gr-avatar-wrap {
  width: 220px;
  height: 220px;
  border-radius: var(--radius-lg);
  overflow: hidden;
  background: var(--bg-inset);
  border: 2px solid var(--border-subtle);
  transition: border-color 0.8s ease, box-shadow 0.8s ease, filter 0.8s ease;
  position: relative;
}
.gr-avatar-wrap.place-1 {
  border-width: 3px;
  border-color: rgba(240, 200, 80, 0.7);
  box-shadow: 0 0 16px rgba(240, 200, 80, 0.35), 0 0 40px rgba(240, 200, 80, 0.12), inset 0 0 12px rgba(240, 200, 80, 0.08);
  animation: gr-frame-pulse 3s ease-in-out infinite;
}
.gr-avatar-wrap.place-1::after {
  content: '';
  position: absolute;
  inset: -3px;
  border-radius: inherit;
  background: conic-gradient(from 0deg, rgba(240,200,80,0.3), rgba(255,160,60,0.2), rgba(240,200,80,0.3), rgba(255,220,120,0.2), rgba(240,200,80,0.3));
  mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
  mask-composite: exclude;
  -webkit-mask: linear-gradient(#fff 0 0) content-box, linear-gradient(#fff 0 0);
  -webkit-mask-composite: xor;
  padding: 3px;
  animation: gr-frame-rotate 4s linear infinite;
  pointer-events: none;
  z-index: 1;
}
@keyframes gr-frame-pulse {
  0%, 100% { box-shadow: 0 0 16px rgba(240,200,80,0.35), 0 0 40px rgba(240,200,80,0.12), inset 0 0 12px rgba(240,200,80,0.08); }
  50% { box-shadow: 0 0 22px rgba(240,200,80,0.5), 0 0 50px rgba(240,200,80,0.18), inset 0 0 16px rgba(240,200,80,0.12); }
}
@keyframes gr-frame-rotate {
  from { filter: hue-rotate(0deg); }
  to { filter: hue-rotate(360deg); }
}
.gr-avatar-wrap.place-2 {
  border-width: 3px;
  border-color: rgba(140, 200, 255, 0.5);
  box-shadow: 0 0 14px rgba(140, 200, 255, 0.2), 0 0 30px rgba(140, 200, 255, 0.08), inset 0 0 8px rgba(140, 200, 255, 0.06);
  animation: gr-frame-diamond 2.5s ease-in-out infinite;
}
@keyframes gr-frame-diamond {
  0%, 100% { box-shadow: 0 0 14px rgba(140,200,255,0.2), 0 0 30px rgba(140,200,255,0.08), inset 0 0 8px rgba(140,200,255,0.06); }
  50% { box-shadow: 0 0 18px rgba(140,200,255,0.3), 0 0 36px rgba(140,200,255,0.12), inset 0 0 10px rgba(140,200,255,0.08); }
}
.gr-avatar-wrap.place-3 {
  border-width: 2.5px;
  border-color: rgba(205, 160, 80, 0.5);
  box-shadow: 0 0 10px rgba(205, 160, 80, 0.18), 0 0 24px rgba(205, 160, 80, 0.06);
}
.gr-avatar-wrap.place-mid {
  border-color: rgba(160, 165, 180, 0.3);
  box-shadow: 0 0 6px rgba(160, 165, 180, 0.08);
}
.gr-avatar-wrap.place-last {
  border-color: rgba(120, 80, 80, 0.4);
  box-shadow: inset 0 0 16px rgba(100, 40, 40, 0.12);
  filter: saturate(0.65);
}
.gr-avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  animation: gr-avatar-breathe 4s ease-in-out infinite;
  transition: filter 0.5s ease;
}
.place-1 .gr-avatar-img,
.place-2 .gr-avatar-img {
  filter: contrast(1.05) brightness(1.05);
  animation-duration: 5s;
}
.place-last .gr-avatar-img {
  filter: saturate(0.7) brightness(0.9);
  animation-duration: 2.5s;
}
@keyframes gr-avatar-breathe {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.015); }
}
.gr-avatar-fallback {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 64px;
  font-weight: 800;
  color: var(--text-dim);
}
.gr-identity {
  text-align: center;
}
.gr-name {
  font-weight: 800;
  font-size: 14px;
  color: var(--accent-gold);
  letter-spacing: 0.3px;
  text-shadow: 0 0 10px rgba(240, 200, 80, 0.25);
}
.gr-username {
  font-size: 10px;
  color: var(--text-muted);
  font-family: var(--font-mono);
}
.rarity-badge {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.5px;
  text-transform: uppercase;
  padding: 1px 5px;
  border-radius: 3px;
  border: 1px solid;
  line-height: 1.4;
  text-shadow: none;
}
.rarity-legendary { color: #f0c850; border-color: rgba(240,200,80,0.4); background: rgba(240,200,80,0.1); box-shadow: 0 0 8px rgba(240,200,80,0.15); }
.rarity-epic { color: #c084fc; border-color: rgba(192,132,252,0.4); background: rgba(192,132,252,0.1); box-shadow: 0 0 8px rgba(192,132,252,0.15); }
.rarity-rare { color: #60a5fa; border-color: rgba(96,165,250,0.4); background: rgba(96,165,250,0.1); }
.rarity-uncommon { color: #4ade80; border-color: rgba(74,222,128,0.3); background: rgba(74,222,128,0.08); }
.rarity-common { color: var(--text-muted); border-color: rgba(255,255,255,0.1); background: rgba(255,255,255,0.03); }
.mastery-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 1px 8px;
  border-radius: 8px;
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.mastery-level { font-size: 11px; font-weight: 800; }
.mastery-bronze { background: linear-gradient(135deg, rgba(184,115,51,0.25), rgba(205,127,50,0.15)); color: #cd7f32; border: 1px solid rgba(205,127,50,0.35); text-shadow: 0 0 4px rgba(205,127,50,0.3); }
.mastery-silver { background: linear-gradient(135deg, rgba(192,192,192,0.25), rgba(169,169,169,0.15)); color: #c0c0c0; border: 1px solid rgba(192,192,192,0.35); text-shadow: 0 0 4px rgba(192,192,192,0.3); }
.mastery-gold { background: linear-gradient(135deg, rgba(255,215,0,0.25), rgba(255,193,37,0.15)); color: #ffd700; border: 1px solid rgba(255,215,0,0.4); text-shadow: 0 0 6px rgba(255,215,0,0.4); box-shadow: 0 0 8px rgba(255,215,0,0.1); }
.mastery-platinum { background: linear-gradient(135deg, rgba(180,220,255,0.25), rgba(200,230,255,0.15)); color: #b4dcff; border: 1px solid rgba(180,220,255,0.4); text-shadow: 0 0 8px rgba(180,220,255,0.5); box-shadow: 0 0 12px rgba(180,220,255,0.12); }

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
