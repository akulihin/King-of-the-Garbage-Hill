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
import AchievementPopup from 'src/components/AchievementPopup.vue'
import {
  playAttackSelection,
  playAnyMoveTurn10PlusLayer,
  isLateGameCharacter,
  playJusticeResetSound,
  playJusticeUpSound,
  setSoundContext,
  getMasterVolume,
  setMasterVolume,
  playRickGameStartTheme,
  stopRickGameStartTheme,
  playPortalGunCharged,
  stopPortalGunCharged,
  playPortalGunUse,
  playKiraArrest,
  playSaitamaGameWinTheme,
  playLeCrispGameWinTheme,
  playPickleRickOnUse,
  playPickleRickOnWin,
  GiantBeansSoundPool,
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
  const roundNo = store.gameState?.roundNo ?? 0
  const charName = store.myPlayer?.character.name
  void playAttackSelection(charName, roundNo)
  if (roundNo >= 10) {
    playAnyMoveTurn10PlusLayer(charName ? isLateGameCharacter(charName) : false)
  }
  void store.attack(place)
}

// Redirect to the correct game URL if the server sends a different gameId
watch(() => store.gameState?.gameId, (actualGameId: number | undefined) => {
  if (actualGameId && actualGameId !== gameIdNum.value) {
    router.replace(`/game/${actualGameId}`)
  }
})

onMounted(async () => {
  setSoundContext('game')
  if (store.isConnected) {
    await store.joinGame(gameIdNum.value)
  }
})

onUnmounted(() => {
  setSoundContext('menu')
  stopRickGameStartTheme()
  stopPortalGunCharged()
  clearPrevLogTimer()
  if (store.isConnected && gameIdNum.value) {
    store.leaveGame(gameIdNum.value)
  }
})

// ── Character passive sound watchers ──────────────────────────────────

// Rick: game start theme — play when Rick joins, stop on first action
const rickThemePlaying = ref(false)
watch(() => store.myPlayer?.character.name, (name) => {
  if (name === 'Рик' && !rickThemePlaying.value && (store.gameState?.roundNo ?? 0) <= 1) {
    rickThemePlaying.value = true
    playRickGameStartTheme()
  }
})
watch(() => store.myPlayer?.status.isReady, (ready) => {
  if (ready && rickThemePlaying.value) {
    rickThemePlaying.value = false
    stopRickGameStartTheme()
  }
})

// Rick: portal gun — loop charged theme while charges > 0
const prevPortalCharges = ref<number | null>(null)
watch(() => store.myPortalGun, (pg) => {
  if (!pg || !pg.invented) {
    if (prevPortalCharges.value !== null && prevPortalCharges.value > 0) {
      stopPortalGunCharged()
    }
    prevPortalCharges.value = null
    return
  }
  const prev = prevPortalCharges.value
  if (pg.charges > 0 && (prev === null || prev === 0)) {
    playPortalGunCharged()
  } else if (pg.charges === 0 && prev !== null && prev > 0) {
    stopPortalGunCharged()
    playPortalGunUse()
  }
  prevPortalCharges.value = pg.charges
}, { deep: true })

// Kira: arrest sound
watch(() => store.myPlayer?.deathNote?.isArrested, (arrested, prevArrested) => {
  if (arrested && !prevArrested) {
    playKiraArrest()
  }
})

// Game finish: character win themes (play for all players)
watch(() => store.gameState?.isFinished, (finished, prevFinished) => {
  if (finished && !prevFinished && store.gameState) {
    // Saitama game win theme
    const hasSaitama = store.gameState.players.some(p => p.character.name === 'Сайтама')
    if (hasSaitama) playSaitamaGameWinTheme()

    // LeCrisp game win theme (plays for all players)
    const hasLeCrisp = store.gameState.players.some(p => p.character.name === 'LeCrisp')
    if (hasLeCrisp) playLeCrispGameWinTheme()

    // Rick portal gun charged theme on Rick game win (plays for everyone)
    const rickWinner = store.gameState.players.find(p => p.character.name === 'Рик')
    if (rickWinner && rickWinner.status.place === 1) {
      playPortalGunCharged()
    }
  }
})

// Rick: Pickle Rick — play sound when entering pickle form or winning while pickled
const prevPickleTurns = ref<number>(0)
watch(() => store.myPickleRick, (pickle, prevPickle) => {
  if (!pickle) {
    prevPickleTurns.value = 0
    return
  }
  const prev = prevPickle?.pickleTurnsRemaining ?? prevPickleTurns.value
  // Entered pickle form (turns went from 0 to >0)
  if (pickle.pickleTurnsRemaining > 0 && prev === 0) {
    playPickleRickOnUse()
  }
  // Won a fight while pickled (wasAttackedAsPickle flipped to true)
  if (pickle.pickleTurnsRemaining > 0 && pickle.wasAttackedAsPickle && !(prevPickle?.wasAttackedAsPickle)) {
    playPickleRickOnWin()
  }
  prevPickleTurns.value = pickle.pickleTurnsRemaining
}, { deep: true })

// Rick: Giant Beans — play spawn when ingredients appear, collect when beanStacks increase
const giantBeansPool = new GiantBeansSoundPool()
const prevBeanStacks = ref<number>(0)
const prevIngredientsActive = ref<boolean>(false)
watch(() => store.myGiantBeans, (beans, prevBeans) => {
  if (!beans) {
    prevBeanStacks.value = 0
    prevIngredientsActive.value = false
    return
  }
  const prevActive = prevBeans?.ingredientsActive ?? prevIngredientsActive.value
  const prevStacks = prevBeans?.beanStacks ?? prevBeanStacks.value
  // Ingredients spawned (ingredientsActive went from false to true, or target count increased)
  if (beans.ingredientsActive && !prevActive) {
    giantBeansPool.playSpawn()
  }
  // Bean collected (stacks increased)
  if (beans.beanStacks > prevStacks) {
    giantBeansPool.playCollect()
  }
  prevBeanStacks.value = beans.beanStacks
  prevIngredientsActive.value = beans.ingredientsActive
}, { deep: true })

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
    volume.value = 0.25
    setMasterVolume(0.25)
  }
}

const showFinishConfirm = ref(false)
function finishGame() {
  store.finishGame()
  showFinishConfirm.value = false
  router.push('/')
}

// ── Round start overlay ─────────────────────────────────────────────
const showRoundOverlay = ref(false)
const overlayRoundNo = ref(0)
const showLogin = ref(false) // for connection overlay check

watch(() => store.gameState?.roundNo, (newRound, oldRound) => {
  if (newRound && oldRound && newRound !== oldRound && newRound > 1) {
    overlayRoundNo.value = newRound
    showRoundOverlay.value = true
    setTimeout(() => { showRoundOverlay.value = false }, 2500)
  }
})

// ── Game Over cinematic sequence ──────────────────────────────────
const showGameOverOverlay = ref(false)
const gameOverPodium = computed(() => {
  if (!store.gameState?.isFinished) return []
  return [...store.gameState.players]
    .filter(p => !p.isDead)
    .sort((a, b) => a.status.place - b.status.place)
    .slice(0, 6)
})

watch(() => store.gameState?.isFinished, (finished, prev) => {
  if (finished && !prev) {
    showGameOverOverlay.value = true
    setTimeout(() => { showGameOverOverlay.value = false }, 5000)
  }
})

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
    .replace(/\|>Stat<\|/g, '')
    .replace(/\|>Phrase<\|/g, '')
    .replace(/\n/g, '<br>')
}

// ── VFX Message Popup ─────────────────────────────────────────────────
const vfxMessages = ref<{ id: number; text: string }[]>([])
let vfxId = 0

function pushVfxMessage(text: string) {
  const id = ++vfxId
  vfxMessages.value.push({ id, text })
  setTimeout(() => {
    vfxMessages.value = vfxMessages.value.filter(m => m.id !== id)
  }, 4000)
}

let lastSeenDirectMessageCount = 0
watch(() => store.myPlayer?.status.directMessages, (msgs) => {
  if (!msgs?.length) { lastSeenDirectMessageCount = 0; return }
  if (msgs.length > lastSeenDirectMessageCount) {
    for (let i = lastSeenDirectMessageCount; i < msgs.length; i++) {
      pushVfxMessage(msgs[i])
    }
  }
  lastSeenDirectMessageCount = msgs.length
}, { deep: true })

watch(() => store.errorMessage, (err) => {
  if (err) pushVfxMessage(err)
})

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

    return { raw: clean, html, type, comboCount, isPhrase }
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
const prevPanelSwiping = ref(false)
const prevPanelExiting = ref(false)
const currentPanelSwiping = ref(false)
const currentPanelExiting = ref(false)
const exitingLogEntries = ref<PrevLogEntry[]>([])
let prevLogTimer: ReturnType<typeof setInterval> | null = null
let prevLogSnapshot = ''
let currentLogTimer: ReturnType<typeof setInterval> | null = null
let currentLogSnapshot = ''
let lastAnimatedRound = -1
let lastMergeRound = -1
let currentLogShownCount = 0

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

  const roundNo = store.gameState?.roundNo ?? 0
  const isNewRound = roundNo !== lastAnimatedRound
  lastAnimatedRound = roundNo

  if (isNewRound) {
    // Round transition: animate left→right

    // 1. Right panel: fade out old content in place (no slide)
    prevPanelExiting.value = true
    prevLogVisibleCount.value = 0
    prevPanelSwiping.value = false

    // 2. Left panel: capture current items for exit-right animation
    const currentItems = currentLogEntries.value
    if (currentItems.length > 0) {
      exitingLogEntries.value = [...currentItems]
      currentPanelExiting.value = true

      // 3. After exit animation: show right panel content all at once
      setTimeout(() => {
        currentPanelExiting.value = false
        exitingLogEntries.value = []
        prevPanelExiting.value = false

        if (val) {
          prevPanelSwiping.value = true
          setTimeout(() => { prevPanelSwiping.value = false }, 500)
          prevLogVisibleCount.value = 999
        }
      }, 400)
    } else {
      // No left panel items to exit — show right panel immediately
      prevPanelExiting.value = false
      if (val) {
        prevPanelSwiping.value = true
        setTimeout(() => { prevPanelSwiping.value = false }, 500)
        prevLogVisibleCount.value = 999
      }
    }
  } else {
    // Mid-round update: just show right panel content, no animation
    prevLogVisibleCount.value = 999
  }
}, { immediate: true })

watch(() => mergeEvents(), (newVal: string | undefined) => {
  const val = newVal || ''
  if (val === currentLogSnapshot) return
  currentLogSnapshot = val
  clearCurrentLogTimer()

  // Detect round transition independently (don't rely on other watcher's timing)
  const roundNo = store.gameState?.roundNo ?? 0
  const isRoundTransition = roundNo !== lastMergeRound
  lastMergeRound = roundNo

  const count = currentLogEntries.value.length
  if (!val || count === 0) {
    currentLogVisibleCount.value = 999
    currentLogShownCount = 0
    return
  }

  if (isRoundTransition) {
    // New round: full animation — hide all, wait for exit→enter, then stagger from top
    const enterDelay = 580
    currentLogShownCount = 0
    currentPanelSwiping.value = false
    setTimeout(() => { currentPanelSwiping.value = true }, enterDelay)
    setTimeout(() => { currentPanelSwiping.value = false }, enterDelay + 500)
    currentLogVisibleCount.value = 0
    setTimeout(() => {
      let i = 0
      currentLogTimer = setInterval(() => {
        i++
        currentLogVisibleCount.value = i
        currentLogShownCount = i
        if (i >= count) clearCurrentLogTimer()
      }, 250)
    }, enterDelay)
  } else {
    // Mid-round: keep existing items visible, only animate new ones
    const from = currentLogShownCount
    currentLogVisibleCount.value = from
    if (count > from) {
      let i = from
      currentLogTimer = setInterval(() => {
        i++
        currentLogVisibleCount.value = i
        currentLogShownCount = i
        if (i >= count) clearCurrentLogTimer()
      }, 250)
    }
  }
}, { immediate: true })

const charTint = computed(() => {
  const name = store.myPlayer?.character.name
  if (!name) return ''
  const tints: Record<string, string> = {
    'Акула': 'rgba(100, 180, 240, 0.03)',
    'Дракон': 'rgba(240, 160, 50, 0.03)',
    'Кратос': 'rgba(200, 50, 50, 0.03)',
    'Сайтама': 'rgba(240, 220, 50, 0.03)',
    'Рик': 'rgba(100, 220, 180, 0.03)',
    'Глеб': 'rgba(180, 100, 220, 0.03)',
    'Стая Гоблинов': 'rgba(100, 180, 80, 0.03)',
    'Котики': 'rgba(240, 180, 140, 0.03)',
    'Кира': 'rgba(200, 50, 50, 0.04)',
  }
  return tints[name] || ''
})
</script>

<template>
  <div class="game-page" :style="charTint ? { background: charTint } : {}">
    <!-- Round announce cinematic overlay -->
    <Transition name="round-announce">
      <div v-if="showRoundOverlay" class="round-announce" :key="overlayRoundNo">
        <div class="round-announce-bg"></div>
        <div class="round-announce-content">
          <span class="round-announce-label">Round</span>
          <span class="round-announce-number">{{ overlayRoundNo }}</span>
          <span v-if="me" class="round-announce-status">
            {{ me.status.place <= 3 ? 'Top ' + me.status.place : 'Place ' + me.status.place }} — Score: {{ me.status.score >= 0 ? me.status.score : '?' }}
          </span>
        </div>
      </div>
    </Transition>

    <!-- Game Over cinematic sequence -->
    <Transition name="gameover">
      <div v-if="showGameOverOverlay" class="gameover-overlay">
        <div class="gameover-bg"></div>
        <div class="gameover-content">
          <div class="gameover-title">GAME OVER</div>
          <div class="gameover-podium">
            <div v-for="(p, idx) in gameOverPodium" :key="p.playerId"
              class="gameover-entry"
              :class="[`gameover-place-${idx + 1}`]"
              :style="{ animationDelay: `${(gameOverPodium.length - 1 - idx) * 0.3 + 0.5}s` }"
            >
              <span class="gameover-place-num">{{ idx + 1 }}</span>
              <img :src="p.character.avatarCurrent || p.character.avatar" class="gameover-avatar" :alt="p.character.name">
              <span class="gameover-name">{{ p.discordUsername }}</span>
              <span class="gameover-score">{{ p.status.score >= 0 ? p.status.score : '?' }}</span>
            </div>
          </div>
        </div>
        <div class="gameover-confetti">
          <span v-for="n in 30" :key="n" class="confetti-piece" :style="{ '--ci': n, '--cx': Math.random(), '--cdelay': Math.random() * 2 + 's' }"></span>
        </div>
      </div>
    </Transition>

    <!-- Connection lost overlay -->
    <Transition name="fade">
      <div v-if="!store.isConnected && !showLogin" class="connection-lost-overlay">
        <div class="connection-lost-card">
          <div class="connection-lost-spinner"></div>
          <span class="connection-lost-text">Reconnecting...</span>
        </div>
      </div>
    </Transition>

    <!-- Loading state (skeleton) -->
    <div v-if="!store.gameState" class="loading">
      <div class="skeleton-layout">
        <div class="skeleton-card skeleton-left">
          <div class="skeleton-avatar skeleton-pulse"></div>
          <div class="skeleton-line skeleton-pulse" style="width:60%"></div>
          <div class="skeleton-line skeleton-pulse" style="width:80%"></div>
          <div class="skeleton-line skeleton-pulse" style="width:70%"></div>
          <div class="skeleton-line skeleton-pulse" style="width:50%"></div>
        </div>
        <div class="skeleton-card skeleton-center">
          <div class="skeleton-line skeleton-pulse" style="width:40%"></div>
          <div class="skeleton-row skeleton-pulse" style="height:48px"></div>
          <div class="skeleton-row skeleton-pulse" style="height:48px"></div>
          <div class="skeleton-row skeleton-pulse" style="height:48px"></div>
          <div class="skeleton-row skeleton-pulse" style="height:48px"></div>
          <div class="skeleton-row skeleton-pulse" style="height:48px"></div>
          <div class="skeleton-row skeleton-pulse" style="height:48px"></div>
        </div>
        <div class="skeleton-card skeleton-right">
          <div class="skeleton-line skeleton-pulse" style="width:50%"></div>
          <div class="skeleton-line skeleton-pulse" style="width:70%"></div>
          <div class="skeleton-line skeleton-pulse" style="width:60%"></div>
        </div>
      </div>
    </div>

    <!-- Draft Pick Phase Overlay -->
    <div v-else-if="store.gameState.isDraftPickPhase && store.gameState.draftOptions" class="draft-pick-overlay">
      <div class="draft-pick-container">
        <div class="draft-pick-layout">
          <!-- Left side character (paid) -->
          <div v-if="store.gameState.draftOptions[1]" class="draft-side-panel">
            <div class="draft-side-card">
              <div class="draft-side-avatar">
                <img :src="store.gameState.draftOptions[1].avatar" :alt="store.gameState.draftOptions[1].name" />
              </div>
              <div class="draft-side-name">{{ store.gameState.draftOptions[1].name }}</div>
              <div class="draft-side-stats">
                <span>🧠 {{ store.gameState.draftOptions[1].intelligence }}</span>
                <span>💪 {{ store.gameState.draftOptions[1].strength }}</span>
                <span>⚡ {{ store.gameState.draftOptions[1].speed }}</span>
                <span>🧿 {{ store.gameState.draftOptions[1].psyche }}</span>
              </div>
            </div>
            <button class="draft-switch-btn" @click="store.draftSelect(store.gameState.draftOptions[1].name)">
              Switch
            </button>
            <div class="draft-cost-label">cost 5 ZBS points</div>
          </div>

          <!-- Center character (free) -->
          <div v-if="store.gameState.draftOptions[0]" class="draft-center-panel">
            <div class="draft-center-avatar">
              <img :src="store.gameState.draftOptions[0].avatar" :alt="store.gameState.draftOptions[0].name" />
            </div>
            <div class="draft-center-info">
              <h2 class="draft-center-name">{{ store.gameState.draftOptions[0].name }}</h2>
              <div class="draft-center-tier">Tier {{ store.gameState.draftOptions[0].tier }}</div>
              <div class="draft-center-stats">
                <span class="draft-stat" title="Intelligence">🧠 {{ store.gameState.draftOptions[0].intelligence }}</span>
                <span class="draft-stat" title="Strength">💪 {{ store.gameState.draftOptions[0].strength }}</span>
                <span class="draft-stat" title="Speed">⚡ {{ store.gameState.draftOptions[0].speed }}</span>
                <span class="draft-stat" title="Psyche">🧿 {{ store.gameState.draftOptions[0].psyche }}</span>
              </div>
              <p class="draft-center-desc">{{ store.gameState.draftOptions[0].description }}</p>
              <div class="draft-center-passives">
                <div v-for="passive in store.gameState.draftOptions[0].passives" :key="passive.name" class="draft-passive">
                  <strong>{{ passive.name }}</strong>
                  <span v-if="passive.description">: {{ passive.description }}</span>
                </div>
              </div>
            </div>
            <div class="draft-free-label">free character</div>
            <button class="draft-play-btn" @click="store.draftSelect(store.gameState.draftOptions[0].name)">
              PLAY
            </button>
          </div>

          <!-- Right side character (paid) -->
          <div v-if="store.gameState.draftOptions[2]" class="draft-side-panel">
            <div class="draft-side-card">
              <div class="draft-side-avatar">
                <img :src="store.gameState.draftOptions[2].avatar" :alt="store.gameState.draftOptions[2].name" />
              </div>
              <div class="draft-side-name">{{ store.gameState.draftOptions[2].name }}</div>
              <div class="draft-side-stats">
                <span>🧠 {{ store.gameState.draftOptions[2].intelligence }}</span>
                <span>💪 {{ store.gameState.draftOptions[2].strength }}</span>
                <span>⚡ {{ store.gameState.draftOptions[2].speed }}</span>
                <span>🧿 {{ store.gameState.draftOptions[2].psyche }}</span>
              </div>
            </div>
            <button class="draft-switch-btn" @click="store.draftSelect(store.gameState.draftOptions[2].name)">
              Switch
            </button>
            <div class="draft-cost-label">cost 5 ZBS points</div>
          </div>
        </div>
      </div>
    </div>

    <!-- Draft pick waiting (already confirmed, waiting for others) -->
    <div v-else-if="store.gameState.isDraftPickPhase && !store.gameState.draftOptions" class="draft-pick-overlay">
      <div class="draft-pick-container">
        <h2 class="draft-pick-title">Waiting for other players...</h2>
        <p class="draft-pick-subtitle">Your character has been selected. The game will start soon.</p>
      </div>
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
          <!-- Kira: Death Note replaces fight animation -->
          <DeathNote
            v-if="store.isKira && store.myPlayer?.deathNote && !store.gameState.isFinished"
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
          <!-- Normal players: fight animation -->
          <FightAnimation
            v-else
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
          v-if="store.myPlayer?.isDead && store.myPlayer?.deathSource === 'Kira' && store.blackjackState"
          :game-id="store.gameState.gameId"
        />

        <!-- Logs: events side-by-side -->
        <div class="logs-row-top">
          
          <div class="log-panel card events-panel">

            <!-- Exiting items: old logs sliding right toward prev panel -->
            <div v-if="currentPanelExiting && exitingLogEntries.length" class="prev-logs slide-exit-right">
              <div
                v-for="(entry, idx) in exitingLogEntries"
                :key="'exit-'+idx"
                class="prev-log-item prev-log-visible"
                :class="['prev-log-' + entry.type, { 'prev-log-phrase': entry.isPhrase }]"
              >
                <span class="prev-log-text" v-html="entry.html"></span>
              </div>
            </div>

            <!-- Normal current items -->
            <div v-else-if="currentLogEntries.length" class="prev-logs" :class="{ 'slide-enter': currentPanelSwiping }">
              <div
                v-for="(entry, idx) in currentLogEntries"
                :key="idx"
                class="prev-log-item"
                :class="[
                  'prev-log-' + entry.type,
                  { 'prev-log-visible': idx < currentLogVisibleCount },
                  { 'prev-log-combo': entry.type === 'gold' && entry.comboCount > 0 },
                  { 'prev-log-phrase': entry.isPhrase }
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

            <div v-if="prevLogEntries.length" class="prev-logs" :class="{ 'slide-enter': prevPanelSwiping }">
              <div
                v-for="(entry, idx) in prevLogEntries"
                :key="idx"
                class="prev-log-item"
                :class="[
                  'prev-log-' + entry.type,
                  { 'prev-log-visible': idx < prevLogVisibleCount },
                  { 'prev-log-fade-exit': prevPanelExiting },
                  { 'prev-log-combo': entry.type === 'gold' && entry.comboCount > 0 },
                  { 'prev-log-phrase': entry.isPhrase }
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
            :is-finished="store.gameState.isFinished"
            :round-no="store.gameState.roundNo"
            :confirmed-predict="store.myPlayer?.status.confirmedPredict"
            :fight-log="store.gameState.fightLog || []"
            :is-kira="store.isKira"
            :death-note="store.myPlayer?.deathNote"
            :is-bug="store.isBug"
            @attack="onAttack"
            @predict="store.predict($event.playerId, $event.characterName)"
          />
        </div>

        <!-- "Back to Lobby" after game ends -->
        <div v-if="store.gameState.isFinished" class="finished-actions">
          <button class="btn btn-primary btn-lg" @click="goToLobby">
            Back to Lobby
          </button>
        </div>

        <!-- Achievement unlock popup -->
        <AchievementPopup
          v-if="store.newlyUnlockedAchievements.length > 0 && store.gameState.isFinished"
          :achievements="store.newlyUnlockedAchievements"
          @dismiss="store.dismissAchievements()"
        />


        <!-- VFX Message Popup (direct messages + action errors) -->
        <Teleport to="body">
          <TransitionGroup name="vfx-msg" tag="div" class="vfx-messages">
            <div
              v-for="msg in vfxMessages"
              :key="msg.id"
              class="vfx-msg"
              @click="vfxMessages = vfxMessages.filter(m => m.id !== msg.id)"
              v-html="formatLogs(msg.text)"
            />
          </TransitionGroup>
        </Teleport>

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

/* ── Draft Pick Phase ─────────────────────────────────────────── */
.draft-pick-overlay {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 80vh;
  padding: 20px;
}
.draft-pick-container {
  text-align: center;
  max-width: 1200px;
  width: 100%;
}
.draft-pick-layout {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 24px;
}

/* ── Side panels (paid characters) ── */
.draft-side-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  width: 180px;
  flex-shrink: 0;
}
.draft-side-card {
  background: var(--bg-secondary);
  border: 2px solid var(--border);
  border-radius: var(--radius-lg, 12px);
  padding: 10px;
  width: 100%;
  text-align: center;
}
.draft-side-avatar {
  width: 100%;
  height: 140px;
  overflow: hidden;
  border-radius: var(--radius, 8px);
  margin-bottom: 8px;
}
.draft-side-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.draft-side-name {
  font-size: 14px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 4px;
}
.draft-side-stats {
  display: flex;
  gap: 6px;
  justify-content: center;
  font-size: 11px;
  color: var(--text-secondary);
}
.draft-switch-btn {
  background: transparent;
  border: 2px solid #4caf50;
  color: #4caf50;
  font-size: 18px;
  font-weight: 700;
  padding: 6px 24px;
  border-radius: 8px;
  cursor: pointer;
  transition: background 0.2s, color 0.2s;
}
.draft-switch-btn:hover {
  background: #4caf50;
  color: #fff;
}
.draft-cost-label {
  font-size: 12px;
  color: var(--text-muted);
}

/* ── Center panel (free character) ── */
.draft-center-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  max-width: 480px;
  flex: 1;
}
.draft-center-avatar {
  width: 320px;
  height: 260px;
  overflow: hidden;
  border-radius: var(--radius-lg, 12px);
  margin-bottom: 16px;
}
.draft-center-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.draft-center-info {
  text-align: left;
  width: 100%;
}
.draft-center-name {
  font-size: 24px;
  font-weight: 800;
  color: var(--text-primary);
  margin: 0 0 4px;
}
.draft-center-tier {
  font-size: 12px;
  color: var(--text-muted);
  margin-bottom: 8px;
}
.draft-center-stats {
  display: flex;
  gap: 12px;
  margin-bottom: 10px;
}
.draft-stat {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-secondary);
}
.draft-center-desc {
  font-size: 13px;
  color: var(--text-muted);
  margin-bottom: 10px;
  line-height: 1.4;
}
.draft-center-passives {
  font-size: 12px;
  color: var(--text-secondary);
  max-height: 160px;
  overflow-y: auto;
  margin-bottom: 12px;
}
.draft-passive {
  margin-bottom: 4px;
  line-height: 1.3;
}
.draft-passive strong {
  color: var(--accent-gold);
}
.draft-free-label {
  font-size: 14px;
  font-weight: 600;
  color: #4caf50;
  margin-bottom: 8px;
}
.draft-play-btn {
  background: #4caf50;
  border: none;
  color: #fff;
  font-size: 28px;
  font-weight: 800;
  padding: 12px 60px;
  border-radius: 12px;
  cursor: pointer;
  transition: background 0.2s, transform 0.2s;
}
.draft-play-btn:hover {
  background: #43a047;
  transform: scale(1.05);
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
  gap: 10px;
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
  background: rgba(80, 150, 230, 0.15);
  color: var(--accent-blue);
  padding: 4px 14px;
  border-radius: var(--radius);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.3px;
  border: 1px solid rgba(80, 150, 230, 0.3);
  box-shadow: 0 0 8px rgba(80, 150, 230, 0.1);
  backdrop-filter: blur(8px);
}

.mode-badge {
  background: rgba(180, 150, 255, 0.12);
  color: var(--accent-purple);
  padding: 4px 14px;
  border-radius: var(--radius);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  border: 1px solid rgba(180, 150, 255, 0.25);
  box-shadow: 0 0 8px rgba(180, 150, 255, 0.08);
}

.status-chip {
  padding: 4px 10px;
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
  border: 1px solid rgba(63, 167, 61, 0.25);
  box-shadow: 0 0 6px rgba(63, 167, 61, 0.15);
}
.status-chip.waiting {
  background: rgba(240, 200, 80, 0.08);
  color: var(--accent-gold);
  border: 1px solid rgba(240, 200, 80, 0.3);
  animation: turn-pulse 1.8s ease-in-out infinite;
}

@keyframes turn-pulse {
  0%, 100% { box-shadow: 0 0 4px rgba(240, 200, 80, 0.1); border-color: rgba(240, 200, 80, 0.3); }
  50% { box-shadow: 0 0 16px rgba(240, 200, 80, 0.4), 0 0 32px rgba(240, 200, 80, 0.1); border-color: rgba(240, 200, 80, 0.6); }
}

/* ── Round announce cinematic overlay ────────────────────────────── */
.round-announce {
  position: fixed;
  inset: 0;
  z-index: 200;
  pointer-events: none;
  display: flex;
  align-items: center;
  justify-content: center;
}

.round-announce-bg {
  position: absolute;
  inset: 0;
  background: linear-gradient(180deg, rgba(0,0,0,0.5) 0%, rgba(0,0,0,0.2) 40%, rgba(0,0,0,0.2) 60%, rgba(0,0,0,0.5) 100%);
}

.round-announce-content {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.round-announce-label {
  font-size: 14px;
  font-weight: 700;
  color: rgba(240, 200, 80, 0.6);
  text-transform: uppercase;
  letter-spacing: 8px;
  font-family: var(--font-mono);
  animation: announce-label-in 0.4s ease-out both;
}

.round-announce-number {
  font-size: 72px;
  font-weight: 900;
  color: var(--accent-gold);
  text-shadow:
    0 0 30px rgba(240, 200, 80, 0.8),
    0 0 60px rgba(240, 200, 80, 0.3),
    0 4px 20px rgba(0, 0, 0, 0.5);
  letter-spacing: 8px;
  font-family: var(--font-mono);
  line-height: 1;
  animation: announce-number-in 0.5s var(--ease-spring) both;
}

.round-announce-status {
  font-size: 12px;
  font-weight: 600;
  color: rgba(236, 239, 242, 0.6);
  font-family: var(--font-mono);
  letter-spacing: 1px;
  animation: announce-status-in 0.5s ease-out 0.3s both;
}

@keyframes announce-label-in {
  0% { opacity: 0; transform: translateY(10px); letter-spacing: 2px; }
  100% { opacity: 1; transform: translateY(0); letter-spacing: 8px; }
}
@keyframes announce-number-in {
  0% { opacity: 0; transform: scale(0.3); }
  60% { transform: scale(1.1); }
  100% { opacity: 1; transform: scale(1); }
}
@keyframes announce-status-in {
  0% { opacity: 0; transform: translateY(-8px); }
  100% { opacity: 1; transform: translateY(0); }
}

.round-announce-enter-active {
  animation: round-overlay-in 0.3s ease-out;
}
.round-announce-leave-active {
  animation: round-overlay-out 0.8s ease forwards;
}

@keyframes round-overlay-in {
  0% { opacity: 0; }
  100% { opacity: 1; }
}
@keyframes round-overlay-out {
  0% { opacity: 1; }
  100% { opacity: 0; }
}

/* ── Game Over cinematic ──────────────────────────────────────────── */
.gameover-overlay {
  position: fixed;
  inset: 0;
  z-index: 300;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;
}

.gameover-bg {
  position: absolute;
  inset: 0;
  background: radial-gradient(ellipse at center, rgba(0,0,0,0.6) 0%, rgba(0,0,0,0.8) 100%);
}

.gameover-content {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 24px;
}

.gameover-title {
  font-size: 48px;
  font-weight: 900;
  color: var(--accent-gold);
  text-shadow:
    0 0 40px rgba(240, 200, 80, 0.8),
    0 0 80px rgba(240, 200, 80, 0.4),
    0 4px 30px rgba(0, 0, 0, 0.6);
  letter-spacing: 12px;
  text-transform: uppercase;
  font-family: var(--font-mono);
  animation: gameover-title-in 0.8s var(--ease-spring) both;
}

@keyframes gameover-title-in {
  0% { opacity: 0; transform: scale(0.5) translateY(20px); }
  60% { transform: scale(1.08) translateY(-5px); }
  100% { opacity: 1; transform: scale(1) translateY(0); }
}

.gameover-podium {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 320px;
}

.gameover-entry {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 16px;
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius);
  backdrop-filter: blur(8px);
  opacity: 0;
  animation: gameover-entry-in 0.5s var(--ease-spring) both;
}

@keyframes gameover-entry-in {
  0% { opacity: 0; transform: translateX(-30px) scale(0.95); }
  100% { opacity: 1; transform: translateX(0) scale(1); }
}

.gameover-place-1 {
  border-color: rgba(240, 200, 80, 0.5);
  box-shadow: 0 0 20px rgba(240, 200, 80, 0.15);
  transform: scale(1.05);
}
.gameover-place-1 .gameover-place-num { color: var(--accent-gold); }
.gameover-place-2 .gameover-place-num { color: #c0c0d0; }
.gameover-place-3 .gameover-place-num { color: #cda064; }

.gameover-place-num {
  font-size: 20px;
  font-weight: 900;
  font-family: var(--font-mono);
  min-width: 28px;
  text-align: center;
  color: var(--text-muted);
}

.gameover-avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  object-fit: cover;
  border: 2px solid var(--glass-border);
}

.gameover-place-1 .gameover-avatar {
  width: 44px;
  height: 44px;
  border-color: rgba(240, 200, 80, 0.5);
  box-shadow: 0 0 12px rgba(240, 200, 80, 0.3);
}

.gameover-name {
  flex: 1;
  font-size: 13px;
  font-weight: 700;
  color: var(--text-primary);
}

.gameover-score {
  font-family: var(--font-mono);
  font-weight: 800;
  font-size: 14px;
  color: var(--accent-gold);
}

/* Confetti */
.gameover-confetti {
  position: absolute;
  inset: 0;
  overflow: hidden;
  pointer-events: none;
}

.confetti-piece {
  position: absolute;
  width: 8px;
  height: 8px;
  top: -10px;
  left: calc(var(--cx) * 100%);
  background: hsl(calc(var(--ci) * 47 + 10), 80%, 65%);
  border-radius: 2px;
  animation: confetti-fall 3s ease-in var(--cdelay) both;
}

@keyframes confetti-fall {
  0% { transform: translateY(0) rotate(0deg); opacity: 1; }
  100% { transform: translateY(100vh) rotate(720deg); opacity: 0; }
}

.gameover-enter-active { animation: round-overlay-in 0.5s ease-out; }
.gameover-leave-active { animation: round-overlay-out 1s ease forwards; }

/* ── Connection lost overlay ──────────────────────────────────────── */
.connection-lost-overlay {
  position: fixed;
  inset: 0;
  z-index: 500;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
}

.connection-lost-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  padding: 32px 48px;
  background: var(--glass-bg-heavy);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
}

.connection-lost-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid var(--border-subtle);
  border-top-color: var(--accent-gold);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.connection-lost-text {
  font-size: 14px;
  font-weight: 700;
  color: var(--text-secondary);
  letter-spacing: 0.5px;
}

/* ── Skeleton loading ──────────────────────────────────────────────── */
.skeleton-layout {
  display: grid;
  grid-template-columns: 250px 1fr 250px;
  gap: 10px;
  padding: 10px 0;
}

.skeleton-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 16px;
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
}

.skeleton-avatar {
  width: 100%;
  height: 180px;
  border-radius: var(--radius);
}

.skeleton-line {
  height: 14px;
  border-radius: 4px;
}

.skeleton-row {
  width: 100%;
  border-radius: var(--radius);
}

.skeleton-pulse {
  background: linear-gradient(90deg, var(--bg-surface) 25%, var(--bg-card-hover) 50%, var(--bg-surface) 75%);
  background-size: 200% 100%;
  animation: skeleton-shimmer 1.5s ease-in-out infinite;
}

@keyframes skeleton-shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}

@media (max-width: 1200px) {
  .skeleton-layout { grid-template-columns: 1fr; }
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

/* ── VFX Message Popup ────────────────────────────────────────────── */

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
  padding: 6px 10px;
  background: var(--glass-bg);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius);
  overflow: hidden;
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
.prev-logs-panel {
  max-height: 220px;
  background: var(--glass-bg);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius);
  overflow: hidden;
}

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
  opacity: 0;
  transform: translateX(-25px) scale(0.97);
  transition: opacity 0.35s ease, transform 0.35s var(--ease-spring);
}

.prev-log-item.prev-log-visible {
  opacity: 1;
  transform: translateX(0) scale(1);
  animation: log-fresh-reveal 1.5s ease-out;
}

/* 4E. Fresh-reveal inner glow — type-colored, fades out */
@keyframes log-fresh-reveal {
  0% { box-shadow: inset 0 0 8px rgba(180, 150, 255, 0.2); }
  100% { box-shadow: inset 0 0 0 transparent; }
}
.prev-log-gold.prev-log-visible { animation-name: log-fresh-gold; }
@keyframes log-fresh-gold {
  0% { box-shadow: inset 0 0 8px rgba(233, 219, 61, 0.2); }
  100% { box-shadow: inset 0 0 0 transparent; }
}
.prev-log-green.prev-log-visible { animation-name: log-fresh-green; }
@keyframes log-fresh-green {
  0% { box-shadow: inset 0 0 8px rgba(63, 167, 61, 0.2); }
  100% { box-shadow: inset 0 0 0 transparent; }
}
.prev-log-red.prev-log-visible { animation-name: log-fresh-red; }
@keyframes log-fresh-red {
  0% { box-shadow: inset 0 0 8px rgba(239, 128, 128, 0.2); }
  100% { box-shadow: inset 0 0 0 transparent; }
}
.prev-log-blue.prev-log-visible { animation-name: log-fresh-blue; }
@keyframes log-fresh-blue {
  0% { box-shadow: inset 0 0 8px rgba(100, 160, 255, 0.2); }
  100% { box-shadow: inset 0 0 0 transparent; }
}
.prev-log-orange.prev-log-visible { animation-name: log-fresh-orange; }
@keyframes log-fresh-orange {
  0% { box-shadow: inset 0 0 8px rgba(230, 148, 74, 0.2); }
  100% { box-shadow: inset 0 0 0 transparent; }
}

/* Right panel items slide from further left (content "arriving" from left panel) */
.prev-logs-panel .prev-log-item:not(.prev-log-visible) {
  transform: translateX(-60px) scale(0.95);
}

/* Right panel exit: fade out in place without sliding left */
.prev-log-item.prev-log-fade-exit {
  transform: translateX(0) !important;
}

/* Container-level slide animations */
@keyframes slide-from-left {
  from { transform: translateX(-15px); opacity: 0.7; }
  to { transform: translateX(0); opacity: 1; }
}

@keyframes slide-from-left-far {
  from { transform: translateX(-60px); opacity: 0.2; }
  to { transform: translateX(0); opacity: 1; }
}

/* Exit animation: left panel items slide right toward the prev panel */
@keyframes slide-to-right {
  0% { transform: translateX(0); opacity: 1; }
  100% { transform: translateX(60px); opacity: 0; }
}

.events-panel:not(.prev-logs-panel) .prev-logs.slide-enter {
  animation: slide-from-left 0.35s ease-out;
}

.prev-logs-panel .prev-logs.slide-enter {
  animation: slide-from-left-far 0.5s ease-out;
}

.slide-exit-right {
  animation: slide-to-right 0.35s ease-in forwards;
  pointer-events: none;
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

.prev-log-phrase {
  padding-left: 16px;
  font-style: italic;
  opacity: 0.85;
  border-left-style: dotted;
  font-size: 10.5px;
}
</style>

<!-- VFX popup styles — unscoped because Teleported to body -->
<style>
.vfx-messages {
  position: fixed;
  top: 12px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 900;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  pointer-events: none;
}

.vfx-msg {
  pointer-events: auto;
  cursor: pointer;
  padding: 10px 20px;
  background: var(--bg-card);
  border: 1px solid var(--accent-orange);
  border-radius: 10px;
  font-size: 13px;
  color: var(--text-primary);
  box-shadow: 0 0 16px rgba(255, 160, 50, 0.3), 0 4px 12px rgba(0,0,0,0.4);
  animation: vfxPop 0.4s cubic-bezier(0.34, 1.56, 0.64, 1);
  max-width: 400px;
  text-align: center;
}

.vfx-msg strong { color: var(--accent-gold); }
.vfx-msg em { color: var(--accent-blue); }
.vfx-msg u { color: var(--accent-green); }
.vfx-msg .lb-emoji {
  width: 20px;
  height: 20px;
  vertical-align: middle;
  display: inline;
  margin: 0 2px;
}

@keyframes vfxPop {
  from { transform: scale(0.7) translateY(-20px); opacity: 0; }
  60% { transform: scale(1.05) translateY(2px); }
  to { transform: scale(1) translateY(0); opacity: 1; }
}

.vfx-msg-enter-active { animation: vfxPop 0.4s cubic-bezier(0.34, 1.56, 0.64, 1); }
.vfx-msg-leave-active { transition: all 0.3s ease; }
.vfx-msg-leave-to { opacity: 0; transform: translateY(-10px) scale(0.9); }
</style>
