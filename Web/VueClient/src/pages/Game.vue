<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, watch, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { useGameStore } from 'src/store/game'
import Leaderboard from 'src/components/Leaderboard.vue'
import DeathNote from 'src/components/DeathNote.vue'
import PlayerCard from 'src/components/PlayerCard.vue'
// ActionPanel removed — action buttons now live under PlayerCard in game-left
import SkillsPanel from 'src/components/SkillsPanel.vue'
import { formatPassiveDescription } from 'src/services/textFormatting'
import { translateText } from 'src/i18n'
import FightAnimation from 'src/components/FightAnimation.vue'
import MediaMessages from 'src/components/MediaMessages.vue'
import RoundTimer from 'src/components/RoundTimer.vue'
import Blackjack21 from 'src/components/Blackjack21.vue'
import AchievementPopup from 'src/components/AchievementPopup.vue'
import TerminalCommitOverlay from 'src/components/TerminalCommitOverlay.vue'
import HalfLife3Transition from 'src/components/HalfLife3Transition.vue'
import HalfLife3Release from 'src/components/HalfLife3Release.vue'
import DeepVeil from 'src/components/DeepVeil.vue'
import OmniManInvasion from 'src/components/OmniManInvasion.vue'
import OmniManUndergroundTrain from 'src/components/OmniManUndergroundTrain.vue'
import type { Player } from 'src/services/signalr'
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
  playPickleRickOnUse,
  playPickleRickOnWin,
  GiantBeansSoundPool,
  playGeraltGameWinTheme,
  playKiraGameWinTheme,
  playMonsterGameWinTheme,
  playRickGameWinTheme,
  playKiraGameStartTheme,
  pauseKiraGameStartTheme,
  resumeKiraGameStartTheme,
  stopKiraGameStartTheme,
  playGeraltGameStartTheme,
  pauseGeraltGameStartTheme,
  resumeGeraltGameStartTheme,
  stopGeraltGameStartTheme,
  playDoomGameStartTheme,
  stopDoomGameStartTheme,
  playDoomGameWinTheme,
  playErenGameWinTheme,
  playCharacterGameWinTheme,
  playErenTatake,
  playErenAttackTitan,
  playErenRumblingWarning,
  stopErenRumblingWarning,
  playGeraltQuestCompleted,
  playGeraltLevelUpAvailable,
  playGeraltOilLevelUp,
  playGeraltOilAttack,
  playGeraltRareLoot,
} from 'src/services/sound'

const props = defineProps<{ gameId: string }>()
const store = useGameStore()
const router = useRouter()

const gameIdNum = computed(() => Number(props.gameId))
let gameOverOverlayTimer: ReturnType<typeof setTimeout> | null = null
let finishPresentationFallbackTimer: ReturnType<typeof setTimeout> | null = null
let terminalCommitTimer: ReturnType<typeof setTimeout> | null = null
let halfLifeReleaseTimer: ReturnType<typeof setTimeout> | null = null
let deepVeilTimer: ReturnType<typeof setTimeout> | null = null
let omniManInvasionTimer: ReturnType<typeof setTimeout> | null = null
let omniManUndergroundTrainTimer: ReturnType<typeof setTimeout> | null = null

const terminalCommitVisible = ref(false)
const terminalCommitPoints = ref(0)
const halfLifeReleaseVisible = ref(false)
const deepVeilVisible = ref(false)
const omniManInvasionVisible = ref(false)
const omniManUndergroundTrainVisible = ref(false)
const omniManUndergroundTrainPhrase = ref('')

watch(() => store.gameState?.halfLifeReleaseSerial, (serial, previousSerial) => {
  if (serial == null || previousSerial == null || serial <= previousSerial) return
  halfLifeReleaseVisible.value = true
  if (halfLifeReleaseTimer) clearTimeout(halfLifeReleaseTimer)
  halfLifeReleaseTimer = setTimeout(() => {
    halfLifeReleaseVisible.value = false
    halfLifeReleaseTimer = null
  }, 5000)
})

watch(() => store.gameState?.abyssSerial, (serial, previousSerial) => {
  if (serial == null || previousSerial == null || serial <= previousSerial) return
  deepVeilVisible.value = true
  if (deepVeilTimer) clearTimeout(deepVeilTimer)
  deepVeilTimer = setTimeout(() => {
    deepVeilVisible.value = false
    deepVeilTimer = null
  }, 6000)
})

watch(() => store.gameState?.omniManInvasionSerial, (serial, previousSerial) => {
  if (serial == null || previousSerial == null || serial <= previousSerial) return
  omniManInvasionVisible.value = true
  if (omniManInvasionTimer) clearTimeout(omniManInvasionTimer)
  omniManInvasionTimer = setTimeout(() => {
    omniManInvasionVisible.value = false
    omniManInvasionTimer = null
  }, 6500)
})

watch(() => store.gameState?.omniManUndergroundTrainSerial, (serial, previousSerial) => {
  if (serial == null || previousSerial == null || serial <= previousSerial) return
  const phrase = store.gameState?.omniManUndergroundTrainPhrase
  if (!phrase) return
  omniManUndergroundTrainPhrase.value = phrase
  omniManUndergroundTrainVisible.value = true
  if (omniManUndergroundTrainTimer) clearTimeout(omniManUndergroundTrainTimer)
  omniManUndergroundTrainTimer = setTimeout(() => {
    omniManUndergroundTrainVisible.value = false
    omniManUndergroundTrainTimer = null
  }, 5400)
})

watch(() => store.myTerminalState?.commitSerial, (serial, previousSerial) => {
  if (!store.isTerminalMode || serial == null || previousSerial == null || serial <= previousSerial) return
  const committedPoints = store.myTerminalState?.lastCommitPoints ?? 0
  if (committedPoints <= 20) return
  terminalCommitPoints.value = committedPoints
  terminalCommitVisible.value = true
  if (terminalCommitTimer) clearTimeout(terminalCommitTimer)
  terminalCommitTimer = setTimeout(() => {
    terminalCommitVisible.value = false
    terminalCommitTimer = null
  }, 4200)
})

const roundMultiplier = computed(() => {
  const r = store.gameState?.roundNo ?? 0
  if (r <= 4) return 1
  if (r <= 9) return 2
  return 4
})

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

const justiceUpFlash = ref(false)
function onJusticeUp() {
  playJusticeUpSound()
  justiceUpFlash.value = true
  setTimeout(() => { justiceUpFlash.value = false }, 2000)
}

type JusticeDustParticle = {
  id: number
  x: number
  y: number
  dx: number
  dy: number
  delay: number
  size: number
}

const fightPanelRef = ref<HTMLElement | null>(null)
const justiceDustParticles = ref<JusticeDustParticle[]>([])
let justiceDustId = 0

function onJusticeTransfer() {
  void nextTick(() => {
    const source = fightPanelRef.value?.getBoundingClientRect()
    const target = document.querySelector<HTMLElement>('[data-justice-target]')?.getBoundingClientRect()
    if (!source || !target) return

    const burst = ++justiceDustId
    const startX = source.left + source.width * 0.18
    const startY = source.top + source.height * 0.55
    const endX = target.left + target.width * 0.5
    const endY = target.top + target.height * 0.5
    const particles = Array.from({ length: 11 }, (_, index) => ({
      id: burst * 100 + index,
      x: startX + (Math.random() - 0.5) * 18,
      y: startY + (Math.random() - 0.5) * 14,
      dx: endX - startX + (Math.random() - 0.5) * 12,
      dy: endY - startY + (Math.random() - 0.5) * 10,
      delay: index * 24 + Math.random() * 40,
      size: 2 + Math.random() * 2.5,
    }))
    justiceDustParticles.value.push(...particles)
    setTimeout(() => {
      justiceDustParticles.value = justiceDustParticles.value.filter(
        particle => particle.id < burst * 100 || particle.id >= burst * 100 + 100,
      )
    }, 1250)
  })
}

/** Fight replay ended — trigger score combo animation */
const fightReplayEnded = ref(false)
function onReplayEnded() {
  fightReplayEnded.value = true
}

function onAttack(place: number) {
  if (store.gameState?.isRoundTransitionPaused) return
  const roundNo = store.gameState?.roundNo ?? 0
  const charName = store.myPlayer?.character.name
  void playAttackSelection(charName, roundNo)
  if (roundNo >= 10) {
    playAnyMoveTurn10PlusLayer(charName ? isLateGameCharacter(charName) : false)
  }
  // Geralt: oil attack layer
  if (charName === 'Геральт' && store.myPlayer?.passiveAbilityStates?.geralt?.isOilApplied) {
    playGeraltOilAttack()
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
  stopKiraGameStartTheme()
  stopGeraltGameStartTheme()
  stopDoomGameStartTheme()
  stopErenRumblingWarning()
  clearPrevLogTimer()
  if (gameOverOverlayTimer) clearTimeout(gameOverOverlayTimer)
  if (finishPresentationFallbackTimer) clearTimeout(finishPresentationFallbackTimer)
  if (terminalCommitTimer) clearTimeout(terminalCommitTimer)
  if (halfLifeReleaseTimer) clearTimeout(halfLifeReleaseTimer)
  if (deepVeilTimer) clearTimeout(deepVeilTimer)
  if (omniManInvasionTimer) clearTimeout(omniManInvasionTimer)
  if (omniManUndergroundTrainTimer) clearTimeout(omniManUndergroundTrainTimer)
  if (store.isConnected && gameIdNum.value) {
    store.leaveGame(gameIdNum.value)
  }
})

// ── Character passive sound watchers ──────────────────────────────────

// Intro/start themes must not play during the 3-way pick — only after the player locks in.
// During the draft `myPlayer.character` reflects the currently *highlighted* option, so the
// character name alone is not a safe trigger.
const myCharacterConfirmed = computed(() => {
  const gs = store.gameState
  const st = store.myPlayer?.status
  if (!gs || !st) return false
  // Draft: not finalized while still showing this player's 3 options and not confirmed.
  if (gs.isDraftPickPhase && !st.isDraftPickConfirmed && gs.draftOptions) return false
  // ARAM: not finalized until the roll is confirmed.
  if (gs.isAramPickPhase && !st.isAramRollConfirmed) return false
  return true // no pick phase (normal mode) ⇒ finalized from the start
})

// Rick: game start theme — play once the pick is confirmed, stop on first action
const rickThemePlaying = ref(false)
watch([() => store.myPlayer?.character.name, myCharacterConfirmed] as const, ([name, confirmed]) => {
  if (name === 'Рик Санчез' && confirmed && !rickThemePlaying.value && (store.gameState?.roundNo ?? 0) <= 1) {
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

// Rick: portal gun — play the charge jingle once per gained charge; "use" sound when a charge is spent
const prevPortalCharges = ref<number | null>(null)
watch(() => store.myPortalGun, (pg) => {
  if (!pg || !pg.invented) {
    prevPortalCharges.value = null
    return
  }
  const prev = prevPortalCharges.value
  if (prev === null) {
    // First observation while invented (e.g. page reload) — announce only if already charged
    if (pg.charges > 0) playPortalGunCharged()
  } else if (pg.charges > prev) {
    playPortalGunCharged()
  } else if (pg.charges < prev) {
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

// Game-start themes stop authoritatively on finish. Winner themes are coordinated with the
// final fight presentation below so Eternal Tsukuyomi's fake fight is not talked over.
watch(() => store.gameState?.isFinished, (finished, prevFinished) => {
  if (finished && !prevFinished && store.gameState) {
    stopKiraGameStartTheme()
    stopGeraltGameStartTheme()
    stopDoomGameStartTheme()
  }
})

// Eren: owner-only event sounds, keyed by monotonic server serials.
watch(() => store.myPlayer?.passiveAbilityStates?.eren, (eren, previous) => {
  if (!eren || !previous) return
  for (let i = previous.tatakeSoundSerial; i < eren.tatakeSoundSerial; i++) playErenTatake()
  for (let i = previous.attackTitanSoundSerial; i < eren.attackTitanSoundSerial; i++) playErenAttackTitan()
}, { deep: true })

// Rumbling warning is public: the server flag does not depend on viewer-masked identities.
const rumblingAudioGameId = ref<number | null>(null)
watch(() => store.gameState, (state) => {
  if (state?.isRumblingWarningActive) {
    if (rumblingAudioGameId.value === state.gameId) return
    stopErenRumblingWarning()
    rumblingAudioGameId.value = state.gameId
    playErenRumblingWarning()
    return
  }

  if (rumblingAudioGameId.value !== null) {
    rumblingAudioGameId.value = null
    stopErenRumblingWarning()
  }
}, { immediate: true })

// DooM Guy: owner-only opening theme, started once the pick is confirmed, stopped after the first committed action.
const doomThemePlaying = ref(false)
watch([() => store.myPlayer?.character.name, myCharacterConfirmed] as const, ([name, confirmed]) => {
  if (name === 'DooM Guy' && confirmed && !doomThemePlaying.value && (store.gameState?.roundNo ?? 0) <= 1) {
    doomThemePlaying.value = true
    playDoomGameStartTheme()
  }
})
watch(() => store.myPlayer?.status.isReady, (ready) => {
  if (ready && doomThemePlaying.value) {
    doomThemePlaying.value = false
    stopDoomGameStartTheme()
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

// Kira: game start theme — plays entire game, pauses during fight animation
const kiraThemePlaying = ref(false)
const kiraResumeAfterReplay = ref(false)

// Start Kira theme once the pick is confirmed (no round restriction)
watch([() => store.myPlayer?.character.name, myCharacterConfirmed] as const, ([name, confirmed]) => {
  if (name === 'Кира' && confirmed && !kiraThemePlaying.value) {
    kiraThemePlaying.value = true
    playKiraGameStartTheme()
  }
})

// Pause when fight animation starts
watch(() => store.myPlayer?.status.previousRoundLogs, (logs) => {
  if (logs && logs.length > 0 && kiraThemePlaying.value) {
    pauseKiraGameStartTheme()
  }
})

// Resume when Kira writes in death note (or defer if fight still playing)
watch(() => store.myPlayer?.deathNote?.currentRoundTarget, (target, prev) => {
  if (!kiraThemePlaying.value) return
  const isEmpty = (t: string | undefined) =>
    !t || t === '00000000-0000-0000-0000-000000000000'
  if (!isEmpty(target) && isEmpty(prev)) {
    if (fightReplayEnded.value) {
      resumeKiraGameStartTheme()
    } else {
      kiraResumeAfterReplay.value = true
    }
  }
})

// Deferred resume: only if name was written during fight animation
watch(fightReplayEnded, (ended) => {
  if (ended && kiraThemePlaying.value && kiraResumeAfterReplay.value) {
    kiraResumeAfterReplay.value = false
    resumeKiraGameStartTheme()
  }
})
// Stop Kira theme on game finish
watch(() => store.gameState?.isFinished, (finished) => {
  if (finished && kiraThemePlaying.value) {
    kiraThemePlaying.value = false
    stopKiraGameStartTheme()
  }
})

// Geralt: game start theme (Req 3) — pausable loop on round 1, started once the pick is confirmed
const geraltThemePlaying = ref(false)
watch([() => store.myPlayer?.character.name, myCharacterConfirmed] as const, ([name, confirmed]) => {
  if (name === 'Геральт' && confirmed && !geraltThemePlaying.value && (store.gameState?.roundNo ?? 0) <= 1) {
    geraltThemePlaying.value = true
    playGeraltGameStartTheme()
  }
})
// Pause Geralt theme when fights arrive (replay starts)
watch(() => store.myPlayer?.status.previousRoundLogs, (logs) => {
  if (logs && logs.length > 0 && geraltThemePlaying.value) {
    pauseGeraltGameStartTheme()
  }
})
// Resume Geralt theme on replay ended
watch(fightReplayEnded, (ended) => {
  if (ended && geraltThemePlaying.value) {
    resumeGeraltGameStartTheme()
  }
})
// Stop Geralt theme after round 1 or on finish
watch(() => store.gameState?.roundNo, (roundNo) => {
  if (roundNo && roundNo > 1 && geraltThemePlaying.value) {
    geraltThemePlaying.value = false
    stopGeraltGameStartTheme()
  }
})

// Geralt: quest completed (Req 4)
watch(() => store.myPlayer?.passiveAbilityStates?.geralt?.questCompletedThisRound, (val, prev) => {
  if (val && !prev) playGeraltQuestCompleted()
})

// Geralt: level-up available (Req 5)
watch(() => store.myPlayer?.status.lvlUpPoints, (val, prev) => {
  if (store.myPlayer?.character.name === 'Геральт' && val && val > 0 && (prev === 0 || prev === undefined)) {
    playGeraltLevelUpAvailable()
  }
})
// Geralt: oil tier increase
const prevOilTiers = ref<number[]>([0, 0, 0, 0])
watch(() => store.myPlayer?.passiveAbilityStates?.geralt, (geralt) => {
  if (!geralt || store.myPlayer?.character.name !== 'Геральт') return
  const tiers = [geralt.drownersOilTier, geralt.werewolvesOilTier, geralt.vampiresOilTier, geralt.dragonsOilTier]
  for (let i = 0; i < 4; i++) {
    if (tiers[i] > prevOilTiers.value[i]) {
      playGeraltOilLevelUp()
      break
    }
  }
  prevOilTiers.value = tiers
}, { deep: true })

// Geralt: rare loot (Req 8)
watch(() => store.myPlayer?.passiveAbilityStates?.geralt?.rareLootFoundThisRound, (val, prev) => {
  if (val && !prev) playGeraltRareLoot()
})

function goToLobby() {
  router.push('/')
}

// ── Header status (moved from ActionPanel) ──────────────────────────
const me = computed(() => store.myPlayer)
const missingAvatarUrl = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'
const ownerAvatar = computed(() => me.value?.character.avatarCurrent || me.value?.character.avatar || missingAvatarUrl)
function gameoverAvatar(player: Player): string {
  return player.character.avatarCurrent || player.character.avatar || missingAvatarUrl
}
function handleOwnerAvatarError(event: Event): void {
  // The terminal's deliberately absent portrait is itself the presentation.
  // Ordinary missing images keep the established generic fallback.
  if (store.isTerminalMode) return
  const image = event.target as HTMLImageElement
  if (image.src !== missingAvatarUrl) image.src = missingAvatarUrl
}
const isMadara = computed(() => me.value?.character.name === 'Мадара')
// Булькает: no predictions at all (same passive gate as Discord's GetPredictMenu).
const hasBulkaet = computed(
  () => me.value?.character.passives?.some((p: { name: string }) => p.name === 'Булькает') ?? false,
)
const isMadaraRoundEight = computed(() => isMadara.value && store.gameState?.roundNo === 8)
const isGordon = computed(() => me.value?.character.name === 'Гордон Фримен')
const gordonState = computed(() => me.value?.passiveAbilityStates?.gordon ?? null)
const gordonHalfLife = computed(() => gordonState.value?.halfLife ?? null)
const transitionPaused = computed(() => store.gameState?.isRoundTransitionPaused ?? false)
const gordonActionPending = ref(false)

async function announceHalfLife3(): Promise<void> {
  if (gordonActionPending.value || transitionPaused.value || !gordonHalfLife.value?.canAnnounce) return
  gordonActionPending.value = true
  try {
    await store.announceHalfLife3()
  }
  finally {
    gordonActionPending.value = false
  }
}

async function wakeGordon(): Promise<void> {
  if (gordonActionPending.value || transitionPaused.value || !gordonState.value?.canWake) return
  gordonActionPending.value = true
  try {
    await store.wakeGordon()
  }
  finally {
    gordonActionPending.value = false
  }
}

async function resolveHalfLife3Decision(choice: 'freeze' | 'postpone' | 'release'): Promise<void> {
  const serial = gordonHalfLife.value?.decisionSerial
  if (gordonActionPending.value || serial == null || !gordonHalfLife.value?.pendingDecision) return
  gordonActionPending.value = true
  try {
    await store.resolveHalfLife3Decision(serial, choice)
  }
  finally {
    gordonActionPending.value = false
  }
}

// ── Avatar / identity (rendered in game-right) ────────────────────
const placeTier = computed(() => {
  const place = me.value?.status?.place ?? 3
  if (place <= 1) return 'place-1'
  if (place <= 2) return 'place-2'
  if (place <= 3) return 'place-3'
  if (place <= 5) return 'place-mid'
  return 'place-last'
})
const charTier = computed(() => me.value?.character.tier ?? 0)
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
const masteryPoints = computed(() => me.value?.characterMasteryPoints ?? 0)
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

// ── Customizable center column layout ────────────────────────────
type PanelId = 'leaderboard' | 'fight' | 'logs'

const allPermutations: PanelId[][] = [
  ['leaderboard', 'fight', 'logs'],
  ['leaderboard', 'logs', 'fight'],
  ['fight', 'leaderboard', 'logs'],
  ['fight', 'logs', 'leaderboard'],
  ['logs', 'leaderboard', 'fight'],
  ['logs', 'fight', 'leaderboard'],
]

function loadLayoutOrder(): PanelId[] {
  try {
    const raw = localStorage.getItem('kotgh_layout_order')
    if (!raw) return allPermutations[0]
    const arr = JSON.parse(raw) as string[]
    if (
      Array.isArray(arr) && arr.length === 3 &&
      arr.includes('leaderboard') && arr.includes('fight') && arr.includes('logs')
    ) return arr as PanelId[]
  } catch { /* ignore */ }
  return allPermutations[0]
}

const layoutOrder = ref<PanelId[]>(loadLayoutOrder())

function cycleLayoutOrder() {
  const key = JSON.stringify(layoutOrder.value)
  const idx = allPermutations.findIndex(p => JSON.stringify(p) === key)
  const next = allPermutations[(idx + 1) % allPermutations.length]
  layoutOrder.value = next
  localStorage.setItem('kotgh_layout_order', JSON.stringify(next))
}

const panelOrder = computed(() => {
  const o: Record<PanelId, number> = { leaderboard: 0, fight: 0, logs: 0 }
  layoutOrder.value.forEach((id, i) => { o[id] = i })
  return o
})

const layoutLabels: Record<PanelId, string> = { leaderboard: 'LB', fight: 'Fight', logs: 'Logs' }
const layoutOrderLabel = computed(() => layoutOrder.value.map(id => layoutLabels[id]).join(' | '))

const fightPanelFixed = ref(localStorage.getItem('kotgh_fight_panel_fixed') === 'true')

function toggleFightPanelSize() {
  fightPanelFixed.value = !fightPanelFixed.value
  localStorage.setItem('kotgh_fight_panel_fixed', String(fightPanelFixed.value))
}

const fightStyleOptions = ['v3', 'v2', 'v1'] as const
type FightStyle = typeof fightStyleOptions[number]
const STYLE_MIGRATION: Record<string, string> = { Classic: 'v1', Cards: 'v2', BigArt: 'v3' }
const rawStyle = localStorage.getItem('kotgh_fight_style') ?? ''
const migratedStyle = STYLE_MIGRATION[rawStyle] ?? rawStyle
const fightStyle = ref<FightStyle>(
  (fightStyleOptions as readonly string[]).includes(migratedStyle)
    ? migratedStyle as FightStyle
    : 'v3'
)
if (migratedStyle !== rawStyle) {
  localStorage.setItem('kotgh_fight_style', fightStyle.value)
}

function cycleFightStyle() {
  const idx = fightStyleOptions.indexOf(fightStyle.value)
  fightStyle.value = fightStyleOptions[(idx + 1) % fightStyleOptions.length]
  localStorage.setItem('kotgh_fight_style', fightStyle.value)
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
const finishPresentationPending = ref(false)
const gameOverPodium = computed(() => {
  if (!store.gameState?.isFinished) return []
  return [...store.gameState.players]
    .filter(p => !p.isDead)
    .sort((a, b) => a.status.place - b.status.place)
    .slice(0, 6)
})

function displayCharacterIntelligence(name: string, intelligence: number): number {
  if (name !== 'Dopa') return intelligence
  return intelligence >= 7
    ? 200 + (intelligence - 7) * 9 + Math.max(0, intelligence - 9)
    : 200 - (7 - intelligence)
}

function playFinishedWinnerThemes() {
  if (!store.gameState) return
  const eternalTsukuyomiVictory = store.myPlayer?.status.scoreBreakdown?.entries
    .some(entry => entry.source === 'Вечное Цукуеми') ?? false

  if (eternalTsukuyomiVictory && store.myPlayer) {
    playCharacterGameWinTheme(store.myPlayer.character.name)
    return
  }

  const winners = store.gameState.players.filter(player => player.status.place === 1 && !player.isDead)
  if (winners.some(player => player.character.name === 'Сайтама')) playSaitamaGameWinTheme()
  if (winners.some(player => player.character.name === 'Геральт')) playGeraltGameWinTheme()
  if (winners.some(player => player.character.name === 'Кира')) playKiraGameWinTheme()
  if (winners.some(player => player.character.name === 'Монстр без имени')) playMonsterGameWinTheme()
  if (winners.some(player => player.character.name === 'Рик Санчез')) playRickGameWinTheme()
  if (winners.some(player => player.character.name === 'DooM Guy')) playDoomGameWinTheme()
  if (winners.some(player => player.character.name === 'Эрен Йегер')) playErenGameWinTheme()
}

function revealFinishedGame() {
  if (!store.gameState?.isFinished) return
  finishPresentationPending.value = false
  if (finishPresentationFallbackTimer) {
    clearTimeout(finishPresentationFallbackTimer)
    finishPresentationFallbackTimer = null
  }

  playFinishedWinnerThemes()
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    showGameOverOverlay.value = false
    return
  }

  showGameOverOverlay.value = true
  if (gameOverOverlayTimer) clearTimeout(gameOverOverlayTimer)
  gameOverOverlayTimer = setTimeout(() => {
    showGameOverOverlay.value = false
    gameOverOverlayTimer = null
  }, 5000)
}

watch(() => store.gameState?.isFinished, (finished, prev) => {
  if (!finished || prev) return

  const hasFinalFight = (store.gameState?.fightLog?.length ?? 0) > 0
  const isEternalTsukuyomiIllusion = store.myPlayer?.status.scoreBreakdown?.entries
    .some(entry => entry.source === 'Вечное Цукуеми') ?? false
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  if (!isEternalTsukuyomiIllusion || !hasFinalFight || reducedMotion) {
    revealFinishedGame()
    return
  }

  fightReplayEnded.value = false
  finishPresentationPending.value = true
  finishPresentationFallbackTimer = setTimeout(revealFinishedGame, 15000)
})

watch(fightReplayEnded, (ended) => {
  if (ended && finishPresentationPending.value)
    revealFinishedGame()
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
  return convertDiscordEmoji(translateText(text))
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

const salldorumState = computed(() => {
  if (store.myPlayer?.character.name !== 'Salldorum') return null
  return store.myPlayer.passiveAbilityStates?.salldorum ?? null
})

const rewriteHistoryRounds = computed(() => {
  const game = store.gameState
  const state = salldorumState.value
  if (!game || !state || store.myPlayer?.isDead || game.isFinished || game.roundNo > 9 || state.historyRewritten) return []
  return Array.from({ length: Math.max(0, game.roundNo - 1) }, (_, index) => index + 1)
})

const rewriteHistoryLastChance = computed(() =>
  store.gameState?.roundNo === 9 && rewriteHistoryRounds.value.length > 0,
)

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
  raw = translateText(raw)
  
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
    const isHalfLifeWin = clean.includes('Hilfelife 3')
      && (clean.includes('Игра Тысячилетия') || clean.includes('Game of the Millennium'))
    let type: PrevLogColor = 'muted'
    let comboCount = 0

    if (isHalfLifeWin) {
      type = 'gold'
    } else if (clean.includes('Я - Учиха. Мадара.')) {
      type = 'red'
    } else if (isPhrase) {
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
      .replace('Я - Учиха. Мадара.', '<span class="madara-callout">Я - Учиха. Мадара.</span>')
      .replace(
        /<strong>(Hilfelife 3[^<]*(?:Игра Тысячилетия|Game of the Millennium)[^<]*)<\/strong>/g,
        '<strong class="halflife-win-callout">$1</strong>',
      )

    return { raw: clean, html, type, comboCount, isPhrase }
  })
}

const prevLogEntriesAll = computed(() => parsePrevLogs(store.myPlayer?.status.previousRoundLogs || ''))
const currentLogEntriesAll = computed(() => parsePrevLogs(mergeEvents() || ''))

// Split: "очков" entries go to PlayerCard, rest stay in log panels
const prevLogEntries = computed(() => prevLogEntriesAll.value.filter((e: PrevLogEntry) => e.type !== 'gold'))
const currentLogEntries = computed(() => currentLogEntriesAll.value.filter((e: PrevLogEntry) => e.type !== 'gold'))


/** Extract fight bonuses for myPlayer from fightLog (aggregated per type) */
const myFightBonuses = computed(() => {
  if (!fightReplayEnded.value) return []
  const log = store.gameState?.fightLog || []
  const myName = store.myPlayer?.discordUsername
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
    // Round transition: current shared log (right) moves into ordinary history (left).

    // 1. Left panel: fade out old content in place (no slide)
    prevPanelExiting.value = true
    prevLogVisibleCount.value = 0
    prevPanelSwiping.value = false

    // 2. Right panel: capture current items for exit-left animation
    const currentItems = currentLogEntries.value
    if (currentItems.length > 0) {
      exitingLogEntries.value = [...currentItems]
      currentPanelExiting.value = true

      // 3. After exit animation: show left panel content all at once
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
      // No right panel items to exit — show left panel immediately
      prevPanelExiting.value = false
      if (val) {
        prevPanelSwiping.value = true
        setTimeout(() => { prevPanelSwiping.value = false }, 500)
        prevLogVisibleCount.value = 999
      }
    }
  } else {
    // Mid-round update: just show the left history panel, no animation
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
  if (store.isTerminalMode) return 'rgba(0, 255, 65, 0.035)'
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

const rumblingKillCount = computed(() =>
  Math.min(4, Math.max(0, store.gameState?.rumblingKillCount ?? 0)),
)
const rumblingEmbers = Array.from({ length: 36 }, (_, index) => ({
  id: index,
  style: {
    left: `${(index * 37 + 11) % 100}%`,
    width: `${2 + (index % 4)}px`,
    height: `${5 + (index % 5) * 2}px`,
    animationDelay: `${-((index * 0.37) % 5).toFixed(2)}s`,
    animationDuration: `${(2.8 + (index % 7) * 0.31).toFixed(2)}s`,
  },
}))
</script>

<template>
  <div
    class="game-page"
    :class="{
      'is-terminal-game': store.isTerminalMode,
      [`rumbling-shake-${rumblingKillCount}`]: rumblingKillCount > 0,
    }"
    :style="charTint ? { background: charTint } : {}"
  >
    <Teleport to="body">
      <div
        v-if="rumblingKillCount > 0"
        class="rumbling-apocalypse"
        :class="`rumbling-fire-${rumblingKillCount}`"
        aria-hidden="true"
      >
        <div class="rumbling-smoke" />
        <div class="rumbling-fireline" />
        <i
          v-for="ember in rumblingEmbers"
          :key="ember.id"
          class="rumbling-ember"
          :style="ember.style"
        />
      </div>
    </Teleport>
    <TerminalCommitOverlay v-if="terminalCommitVisible" :points="terminalCommitPoints" />
    <HalfLife3Release v-if="halfLifeReleaseVisible" />
    <DeepVeil v-if="deepVeilVisible" />
    <OmniManInvasion v-if="omniManInvasionVisible" />
    <OmniManUndergroundTrain
      v-if="omniManUndergroundTrainVisible"
      :phrase="omniManUndergroundTrainPhrase"
    />
    <HalfLife3Transition
      v-if="transitionPaused"
      :is-gordon="isGordon"
      :half-life="gordonHalfLife"
      :transition-deadline-utc="store.gameState?.transitionDeadlineUtc"
      :is-submitting="gordonActionPending"
      @resolve="resolveHalfLife3Decision"
    />
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
              <img :src="gameoverAvatar(p)" class="gameover-avatar" :alt="p.character.name">
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

    <!-- Blocking binary pre-game prompt -->
    <div
      v-else-if="store.gameState.isDraftPickPhase && store.depthsCallPromptActive"
      class="draft-pick-overlay depths-call-overlay"
    >
      <div class="draft-pick-container depths-call-container">
        <h2 class="draft-pick-title">Откликнуться на зов глубин</h2>
        <div class="depths-call-actions">
          <button class="depths-answer yes" @click="store.depthsCallChoice(true)">Да</button>
          <button class="depths-answer no" @click="store.depthsCallChoice(false)">Нет</button>
        </div>
      </div>
    </div>

    <!-- Four-choice ritual layout -->
    <div
      v-else-if="store.gameState.isDraftPickPhase && store.gameState.draftOptions && store.gameState.draftPickHeading"
      class="draft-pick-overlay"
    >
      <div class="draft-pick-container">
        <h2 class="draft-pick-title">{{ store.gameState.draftPickHeading }}</h2>
        <div class="draft-ritual-layout">
          <article
            v-for="option in store.gameState.draftOptions"
            :key="option.name"
            class="draft-ritual-card"
          >
            <img :src="option.avatar" :alt="option.name" class="draft-ritual-avatar" />
            <h3>{{ option.name }}</h3>
            <div class="draft-ritual-stats">
              <span>🧠 {{ displayCharacterIntelligence(option.name, option.intelligence) }}</span>
              <span>💪 {{ option.strength }}</span>
              <span>⚡ {{ option.speed }}</span>
              <span>🧿 {{ option.psyche }}</span>
            </div>
            <div class="draft-ritual-passives">
              <span v-for="passive in option.passives" :key="passive.name">{{ passive.name }}</span>
            </div>
            <button class="draft-play-btn" @click="store.draftSelect(option.name)">Выбрать</button>
          </article>
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
                <span>🧠 {{ displayCharacterIntelligence(store.gameState.draftOptions[1].name, store.gameState.draftOptions[1].intelligence) }}</span>
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
                <span class="draft-stat" :title="store.gameState.draftOptions[0].name === 'Dopa' ? 'IQ' : 'Intelligence'">🧠 {{ displayCharacterIntelligence(store.gameState.draftOptions[0].name, store.gameState.draftOptions[0].intelligence) }}</span>
                <span class="draft-stat" title="Strength">💪 {{ store.gameState.draftOptions[0].strength }}</span>
                <span class="draft-stat" title="Speed">⚡ {{ store.gameState.draftOptions[0].speed }}</span>
                <span class="draft-stat" title="Psyche">🧿 {{ store.gameState.draftOptions[0].psyche }}</span>
              </div>
              <p class="draft-center-desc">{{ store.gameState.draftOptions[0].description }}</p>
              <div class="draft-center-passives">
                <div v-for="passive in store.gameState.draftOptions[0].passives" :key="passive.name" class="draft-passive">
                  <strong>{{ passive.name }}</strong>
                  <span v-if="passive.description">: <span v-html="formatPassiveDescription(passive.description)" /></span>
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
                <span>🧠 {{ displayCharacterIntelligence(store.gameState.draftOptions[2].name, store.gameState.draftOptions[2].intelligence) }}</span>
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
          :justice-up="justiceUpFlash"
          :score-breakdown="store.myPlayer?.status.scoreBreakdown ?? null"
          :score-anim-ready="fightReplayEnded"
          :fight-bonuses="myFightBonuses"
        />
        <!-- Action buttons -->
        <div
          v-if="store.myPlayer && !store.gameState.isFinished"
          class="game-actions"
          :class="{ 'can-act': store.isMyTurn && !transitionPaused, 'transition-paused': transitionPaused }"
        >
          <div v-if="store.mustSpendLevelUp" class="lvlup-gate-hint">
            ⚠ Остались очки прокачки — потрать их!
          </div>
          <div v-if="me?.adeptChoiceAvailable" class="act-group">
            <button
              class="act-btn cthulhu-adept"
              :disabled="transitionPaused"
              title="Открыть выбор адепта"
              @click="store.beginAdeptChoice()"
            >
              Выбрать адепта
            </button>
          </div>
          <div v-if="!me?.adeptChoiceAvailable && !isMadaraRoundEight" class="act-group">
            <button v-if="!isGordon" class="act-btn shield" :disabled="!store.isMyTurn || store.mustSpendLevelUp || transitionPaused" title="Block" @click="store.block()">
              <span class="gi gi-lg gi-def">DEF</span> Block
            </button>
            <button
              v-else-if="gordonHalfLife?.canAnnounce"
              class="act-btn half-life"
              :disabled="!store.isMyTurn || store.mustSpendLevelUp || transitionPaused || gordonActionPending"
              title="Анонсировать Halflife 3"
              @click="announceHalfLife3"
            >
              <span class="half-life-lambda">λ</span> Halflife 3
            </button>
            <button class="act-btn auto" :disabled="!store.isMyTurn || store.mustSpendLevelUp || transitionPaused" title="Auto Move" @click="store.autoMove()">
              <span class="gi gi-lg gi-auto">AUTO</span> Move
            </button>
            <button v-if="me?.status.isReady && !me?.status.isSkip" class="act-btn undo" :disabled="transitionPaused" title="Change Mind" @click="store.changeMind()">
              <span class="gi gi-lg gi-undo">UNDO</span> Change
            </button>
            <button v-if="!me?.status.confirmedSkip" :disabled="store.mustSpendLevelUp || transitionPaused" class="act-btn skip" title="Confirm Skip" @click="store.confirmSkip()">
              <span class="gi gi-lg gi-skip">SKIP</span>
            </button>
            <button
              v-if="gordonState?.canWake"
              class="act-btn gordon-wake"
              :disabled="transitionPaused || gordonActionPending"
              title="Просыпайтесь, мистер Фримен"
              @click="wakeGordon"
            >
              <span aria-hidden="true">⏰</span> Проснуться
            </button>
          </div>

          <div v-if="me?.darksciChoiceNeeded" class="act-group">
            <button class="act-btn darksci-stable" :disabled="transitionPaused" title="Стабильный: +20 Skill, +2 Moral" @click="store.darksciChoice(true)">
              Мне не везёт...
            </button>
            <button class="act-btn darksci-unstable" :disabled="transitionPaused" title="Нестабильный: удача решит" @click="store.darksciChoice(false)">
              Мне повезёт!
            </button>
          </div>

          <div v-if="me?.youngGlebAvailable" class="act-group">
            <button class="act-btn young-gleb" :disabled="transitionPaused" title="Трансформироваться в Молодого Глеба" @click="store.youngGleb()">
              Вспомнить Молодость
            </button>
          </div>

          <div v-if="me?.passiveAbilityStates?.doomGuy?.rollAvailable" class="act-group">
            <button class="act-btn doom-roll" :disabled="transitionPaused" title="Отключить Мораль и Предположения; получать случайные модули и +2 очка" @click="store.doomRoll()">
              Let's Roll!
            </button>
          </div>

          <div v-if="(store.gameState.roundNo ?? 0) >= 8 && !store.isKira && !isMadara && !hasBulkaet && !me?.status.confirmedPredict" class="act-group">
            <button class="act-btn predict-confirm" :disabled="transitionPaused" title="Confirm Predictions" @click="store.confirmPredict()">
              Confirm Prediction
            </button>
          </div>

          <div v-if="me?.passiveAbilityStates?.dopa?.needSecondAttack" class="act-group">
            <span class="dopa-second-hint">Выберите вторую цель (скрытая атака)</span>
          </div>
        </div>
      </div>

      <!-- Center: Header + Leaderboard + Actions + Logs -->
      <div class="game-center">
        <!-- Game header bar -->
        <div class="game-header">
          <div class="header-center">
            <span class="round-badge">
              Round {{ store.gameState.roundNo }} / 10
            </span>
            <span class="multiplier-badge" :class="{ 'mult-active': roundMultiplier > 1 }">
              Points multiplier: x{{ roundMultiplier }}
            </span>
            <span v-if="store.gameState.isFinished" class="finished-badge">
              Finished
            </span>
            <!-- Status chip (moved from ActionPanel) -->
            <span v-if="me && !store.gameState.isFinished" class="status-chip" :class="{ ready: me.status.isReady && !transitionPaused, waiting: !me.status.isReady || transitionPaused }">
              {{ transitionPaused ? '⏸ Round transition' : me.status.isReady ? '✓ Ready' : me.status.isSkip ? '⏭ Skip' : '⏳ Your turn' }}
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
            <!-- Layout order cycle -->
            <button class="btn btn-ghost btn-sm layout-btn"
              title="Cycle center column order"
              @click="cycleLayoutOrder()">
              {{ layoutOrderLabel }}
            </button>
            <!-- Fight panel size toggle -->
            <button class="btn btn-ghost btn-sm layout-btn"
              :class="{ active: fightPanelFixed }"
              title="Toggle fight panel sizing mode"
              @click="toggleFightPanelSize()">
              {{ fightPanelFixed ? 'Fixed' : 'Dynamic' }}
            </button>
            <!-- Fight animation style toggle -->
            <button class="btn btn-ghost btn-sm layout-btn"
              title="Toggle fight animation style"
              @click="cycleFightStyle()">
              {{ fightStyle }}
            </button>
            <RoundTimer v-if="!store.gameState.isFinished && !transitionPaused" />
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

        <!-- Leaderboard -->
        <div class="center-section" :style="{ order: panelOrder.leaderboard }">
          <Leaderboard
            :players="store.gameState.players"
            :my-player-id="store.myPlayer?.playerId"
            :can-attack="!store.gameState.isFinished && !transitionPaused && (store.isMyTurn || store.canFireGunDuringPickle) && !store.mustSpendLevelUp"
            :predictions="store.myPlayer?.predictions"
            :character-names="store.gameState.allCharacterNames || []"
            :character-catalog="store.gameState.allCharacters || []"
            :is-admin="store.isAdmin"
            :is-finished="store.gameState.isFinished"
            :round-no="store.gameState.roundNo"
            :confirmed-predict="store.myPlayer?.status.confirmedPredict"
            :fight-log="store.gameState.fightLog || []"
            :is-kira="store.isKira"
            :has-bulkaet="hasBulkaet"
            :death-note="store.myPlayer?.deathNote"
            :terminal-mode="store.isTerminalMode"
            :pink-ward-revealed-player-ids="store.gameState.pinkWardRevealedPlayerIds"
            @attack="onAttack"
            @predict="store.predict($event.playerId, $event.characterName)"
          />
        </div>

        <!-- Fight Panel + Blackjack -->
        <div class="center-section fight-section" :class="{ 'fight-section-fixed': fightPanelFixed || fightStyle !== 'v3' }" :style="{ order: panelOrder.fight }">
          <div ref="fightPanelRef" class="log-panel card fight-panel" :class="{ 'fight-panel-fixed': fightPanelFixed }" :data-style="fightStyle">
            <!-- Kira: Death Note above fight animation -->
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
            <!-- Fight animation (all players including Kira) -->
            <FightAnimation
              :fights="store.gameState.fightLog || []"
              :round-key="`${store.gameState.gameId}-${store.gameState.roundNo}-${store.gameState.isFinished ? 'finished' : 'live'}`"
              :letopis="letopis"
              :game-story="store.gameStory"
              :players="store.gameState.players"
              :my-player-id="store.myPlayer?.playerId"
              :predictions="store.myPlayer?.predictions"
              :is-admin="store.isAdmin && !store.isTerminalMode"
              :terminal-mode="store.isTerminalMode"
              :show-detailed-factors="store.isAdmin || store.isTerminalMode"
              :character-catalog="store.gameState.allCharacters || []"
              :fight-style="fightStyle"
              :rewrite-history-rounds="rewriteHistoryRounds"
              :rewrite-history-pending-round="store.rewritingHistoryRound"
              :rewrite-history-last-chance="rewriteHistoryLastChance"
              @resist-flash="onResistFlash"
              @justice-reset="onJusticeReset"
              @justice-transfer="onJusticeTransfer"
              @justice-up="onJusticeUp"
              @rewrite-history="store.rewriteHistory($event)"
              @replay-ended="onReplayEnded"
            />
          </div>

          <!-- Blackjack mini-game for players killed by Death Note -->
          <Blackjack21
            v-if="store.myPlayer?.isDead && store.myPlayer?.deathSource === 'Kira' && store.blackjackState"
            :game-id="store.gameState.gameId"
          />
        </div>

        <!-- Logs: events side-by-side -->
        <div class="center-section" :style="{ order: panelOrder.logs }">
          <div class="logs-row-top">

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

            <div class="log-panel card events-panel">

              <!-- Exiting items: current shared log slides left into the ordinary-log panel -->
              <div v-if="currentPanelExiting && exitingLogEntries.length" class="prev-logs slide-exit-left">
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

          </div>
        </div>


        <!-- "Back to Lobby" after game ends -->
        <div v-if="store.gameState.isFinished" class="finished-actions" style="order: 999">
          <button class="btn btn-primary btn-lg" @click="goToLobby">
            Back to Lobby
          </button>
        </div>

        <!-- Achievement unlock popup -->
        <AchievementPopup
          v-if="store.newlyUnlockedAchievements.length > 0 && store.gameState.isFinished && !finishPresentationPending && !showGameOverOverlay && !store.isLootBoxFlowActive"
          :achievements="store.newlyUnlockedAchievements"
          :is-saving="store.isAcknowledgingAchievements"
          :save-error="store.achievementAcknowledgeError"
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

        <Teleport to="body">
          <div class="justice-dust-layer" aria-hidden="true">
            <span
              v-for="particle in justiceDustParticles"
              :key="particle.id"
              class="justice-dust-particle"
              :style="{
                left: `${particle.x}px`,
                top: `${particle.y}px`,
                width: `${particle.size}px`,
                height: `${particle.size}px`,
                '--justice-dx': `${particle.dx}px`,
                '--justice-dy': `${particle.dy}px`,
                '--justice-delay': `${particle.delay}ms`,
              }"
            />
          </div>
        </Teleport>

        <!-- Character Phrase Media Messages (text, audio, images) -->
        <MediaMessages
          v-if="store.myPlayer?.status.mediaMessages?.length"
          :messages="store.myPlayer.status.mediaMessages"
        />

      </div>

      <!-- Right: Avatar + Identity + Skills / Passives -->
      <div class="game-right">
        <div v-if="me" class="gr-avatar-section">
          <div class="gr-avatar-wrap" :class="[placeTier]">
            <img
              :src="ownerAvatar"
              :alt="me.character.name"
              class="gr-avatar-img"
              @error="handleOwnerAvatarError"
            >
          </div>
          <div class="gr-identity">
            <div class="gr-name">
              {{ store.isTerminalMode ? `Name: ${me.character.name}` : me.character.name }}
              <span v-if="!store.isTerminalMode && charTier > 0" class="rarity-badge" :class="rarityClass">{{ rarityLabel }}</span>
            </div>
            <div v-if="!store.isTerminalMode && masteryLevel > 0" class="mastery-badge" :class="'mastery-' + masteryTier">
              <span class="mastery-level">{{ masteryLevel }}</span>
              <span class="mastery-label">{{ masteryTier }}</span>
            </div>
            <div class="gr-username">{{ me.discordUsername }}</div>
          </div>
        </div>
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
  height: calc(100vh - 44px - 2rem); /* viewport minus top-bar (44px) minus main-content padding (1rem * 2) */
  min-height: 0;
}

.rumbling-apocalypse {
  --rumbling-height: 20vh;
  --rumbling-opacity: 0.16;
  position: fixed;
  z-index: 250;
  inset: 0;
  overflow: hidden;
  pointer-events: none;
  opacity: var(--rumbling-opacity);
  mix-blend-mode: screen;
  animation: rumbling-heat-haze 2.8s ease-in-out infinite;
}

.rumbling-fire-2 { --rumbling-height: 40vh; --rumbling-opacity: 0.38; }
.rumbling-fire-3 { --rumbling-height: 72vh; --rumbling-opacity: 0.68; }
.rumbling-fire-4 { --rumbling-height: 118vh; --rumbling-opacity: 0.96; }

.rumbling-smoke {
  position: absolute;
  inset: 0;
  background:
    radial-gradient(ellipse at 12% 105%, rgba(255, 55, 0, 0.55), transparent 42%),
    radial-gradient(ellipse at 55% 115%, rgba(255, 125, 0, 0.48), transparent 50%),
    radial-gradient(ellipse at 92% 105%, rgba(185, 25, 0, 0.58), transparent 44%),
    linear-gradient(to top, rgba(38, 0, 0, 0.78), transparent 70%);
  animation: rumbling-smoke-breathe 4.8s ease-in-out infinite alternate;
}

.rumbling-fireline {
  position: absolute;
  left: -8vw;
  right: -8vw;
  bottom: -12vh;
  height: var(--rumbling-height);
  background:
    radial-gradient(ellipse at 7% 100%, #fff4a0 0 3%, #ff8a00 4% 12%, #ef2100 22%, transparent 40%),
    radial-gradient(ellipse at 23% 106%, #fff7b0 0 4%, #ff9d00 5% 14%, #d91a00 25%, transparent 43%),
    radial-gradient(ellipse at 42% 102%, #fff2a3 0 3%, #ff7400 4% 13%, #f02b00 23%, transparent 42%),
    radial-gradient(ellipse at 61% 108%, #fff7bb 0 4%, #ff9d00 5% 15%, #d81b00 26%, transparent 45%),
    radial-gradient(ellipse at 79% 101%, #fff1a0 0 3%, #ff7200 4% 12%, #ed2500 24%, transparent 42%),
    radial-gradient(ellipse at 96% 106%, #fff8bd 0 4%, #ff9700 5% 14%, #ce1700 26%, transparent 44%);
  background-size: 34% 100%, 31% 88%, 35% 96%, 31% 91%, 36% 100%, 32% 90%;
  background-repeat: repeat-x;
  filter: saturate(1.45) contrast(1.16) blur(0.4px);
  transform-origin: 50% 100%;
  animation: rumbling-flames 1.15s ease-in-out infinite alternate;
}

.rumbling-ember {
  position: absolute;
  bottom: -12px;
  display: block;
  border-radius: 50% 50% 35% 35%;
  background: #ffd36a;
  box-shadow: 0 0 7px 2px #ff5a00;
  animation: rumbling-ember-rise linear infinite;
}

.rumbling-shake-1 { animation: rumbling-shake-soft 2.2s steps(2, end) infinite; }
.rumbling-shake-2 { animation: rumbling-shake-medium 1.45s steps(2, end) infinite; }
.rumbling-shake-3 { animation: rumbling-shake-heavy 0.85s steps(2, end) infinite; }
.rumbling-shake-4 { animation: rumbling-shake-apocalypse 0.52s steps(2, end) infinite; }

@keyframes rumbling-heat-haze {
  0%, 100% { filter: brightness(0.92) blur(0); }
  50% { filter: brightness(1.12) blur(0.7px); }
}
@keyframes rumbling-smoke-breathe {
  from { transform: scale(1.02) translateY(2%); opacity: 0.72; }
  to { transform: scale(1.1) translateY(-3%); opacity: 1; }
}
@keyframes rumbling-flames {
  from { transform: scaleX(1.02) scaleY(0.92) skewX(-1deg); background-position: 0 0, 5% 3%, 11% 0, 17% 4%, 23% 1%, 29% 5%; }
  to { transform: scaleX(0.98) scaleY(1.08) skewX(1deg); background-position: 7% 4%, 12% 0, 18% 5%, 24% 0, 30% 4%, 36% 1%; }
}
@keyframes rumbling-ember-rise {
  0% { transform: translate3d(0, 0, 0) scale(0.5); opacity: 0; }
  12% { opacity: 1; }
  70% { opacity: 0.75; }
  100% { transform: translate3d(4vw, -105vh, 0) rotate(280deg) scale(0.05); opacity: 0; }
}
@keyframes rumbling-shake-soft {
  0%, 100% { transform: translate(0, 0); }
  50% { transform: translate(0.3px, -0.4px); }
}
@keyframes rumbling-shake-medium {
  0%, 100% { transform: translate(0, 0); }
  25% { transform: translate(-0.8px, 0.5px); }
  75% { transform: translate(0.9px, -0.6px); }
}
@keyframes rumbling-shake-heavy {
  0%, 100% { transform: translate(0, 0) rotate(0); }
  25% { transform: translate(-1.8px, 1px) rotate(-0.025deg); }
  50% { transform: translate(1.3px, -1.5px) rotate(0.02deg); }
  75% { transform: translate(1.8px, 1.2px) rotate(-0.015deg); }
}
@keyframes rumbling-shake-apocalypse {
  0%, 100% { transform: translate(0, 0) rotate(0); }
  20% { transform: translate(-3px, 2px) rotate(-0.05deg); }
  40% { transform: translate(2.5px, -3px) rotate(0.045deg); }
  60% { transform: translate(3px, 2px) rotate(-0.035deg); }
  80% { transform: translate(-2px, -2.5px) rotate(0.04deg); }
}

.game-page.is-terminal-game {
  position: relative;
  color: #9bffb3;
  font-family: var(--font-mono);
}
.game-page.is-terminal-game::before {
  content: 'SYS://SESSION_OVERRIDE  //  TRACE=OFF  //  WATCHDOG=BYPASSED';
  position: absolute;
  z-index: 20;
  top: -11px;
  left: 12px;
  padding: 0 6px;
  background: #000902;
  color: rgba(89, 255, 129, 0.42);
  font: 700 7px/1.2 var(--font-mono);
  letter-spacing: 0.1em;
  pointer-events: none;
}
.game-page.is-terminal-game :deep(.game-header),
.game-page.is-terminal-game :deep(.log-panel),
.game-page.is-terminal-game :deep(.leaderboard) {
  border-color: rgba(0, 255, 65, 0.28);
  background-color: rgba(0, 10, 3, 0.88);
}
.game-page.is-terminal-game .round-badge,
.game-page.is-terminal-game .multiplier-badge,
.game-page.is-terminal-game .status-chip {
  border-color: rgba(0, 255, 65, 0.34);
  background: rgba(0, 255, 65, 0.07);
  color: #74ff96;
  text-shadow: 0 0 6px rgba(0, 255, 65, 0.55);
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

.draft-ritual-layout {
  display: grid;
  grid-template-columns: repeat(4, minmax(180px, 1fr));
  gap: 18px;
  margin-top: 24px;
}

.draft-ritual-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  border: 1px solid rgba(25, 194, 184, 0.34);
  border-radius: 16px;
  background: linear-gradient(160deg, rgba(4, 28, 39, 0.96), rgba(2, 10, 20, 0.96));
  box-shadow: 0 0 22px rgba(25, 194, 184, 0.1), inset 0 0 22px rgba(62, 230, 200, 0.04);
}

.draft-ritual-card h3 {
  margin: 0;
  color: #a8d8e8;
}

.draft-ritual-avatar {
  width: 100%;
  aspect-ratio: 1;
  border-radius: 12px;
  object-fit: cover;
}

.draft-ritual-stats,
.draft-ritual-passives {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 7px 10px;
  color: #6fb3c9;
  font-size: 12px;
}

.draft-ritual-passives {
  min-height: 44px;
  align-content: flex-start;
  color: #3ee6c8;
}

.depths-call-container {
  max-width: 680px;
  padding: 54px 32px;
  border: 1px solid rgba(25, 194, 184, 0.38);
  border-radius: 24px;
  background: radial-gradient(circle at 50% 0%, rgba(25, 194, 184, 0.14), transparent 48%), #020a14;
  box-shadow: 0 0 60px rgba(25, 194, 184, 0.16);
}

.depths-call-actions {
  display: flex;
  justify-content: center;
  gap: 22px;
  margin-top: 34px;
}

.depths-answer {
  min-width: 150px;
  padding: 14px 26px;
  border: 1px solid rgba(62, 230, 200, 0.5);
  border-radius: 12px;
  color: #a8d8e8;
  background: rgba(4, 28, 39, 0.92);
  font: 800 18px/1 var(--font-display);
  cursor: pointer;
}

.depths-answer:hover {
  color: #020a14;
  background: #3ee6c8;
}

@media (max-width: 900px) {
  .draft-ritual-layout {
    grid-template-columns: repeat(2, minmax(160px, 1fr));
  }
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
  flex: 1;
  min-height: 0;
}

.game-center {
  display: flex;
  flex-direction: column;
  align-self: stretch;
  min-height: 0;
}

/* v1/v2: always fit content (no wasted space, no cutoff) */
.fight-panel[data-style="v1"],
.fight-panel[data-style="v2"] {
  flex: 0 0 auto;
  min-height: auto;
  max-height: none;
  overflow-y: visible;
}

/* v3 Fixed: fit-to-content instead of filling remaining space */
.fight-panel-fixed[data-style="v3"] {
  flex: 0 0 auto;
  min-height: auto;
  max-height: none;
  overflow-y: visible;
}

.layout-btn {
  font-size: 10px;
  white-space: nowrap;
}
.layout-btn.active {
  background: rgba(80, 150, 230, 0.15);
  color: var(--accent-blue);
}

@media (max-width: 1200px) {
  .game-layout {
    grid-template-columns: 1fr;
  }
  .game-right { order: -1; }
}

/* ── Action buttons (under PlayerCard in game-left) ────────────── */
.game-actions {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 6px 0;
}

.act-group {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
}

.act-btn {
  height: 28px;
  padding: 0 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  background: var(--bg-secondary);
  color: var(--text-primary);
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.2s var(--ease-in-out), border-color 0.2s var(--ease-in-out), box-shadow 0.2s var(--ease-in-out), opacity 0.2s var(--ease-in-out);
  white-space: nowrap;
  position: relative;
  overflow: hidden;
}

.act-btn.shield { border-left: 3px solid var(--accent-blue); }
.act-btn.auto { border-left: 3px solid var(--accent-green); }
.act-btn.undo { border-left: 3px solid var(--accent-orange); }
.act-btn.skip { border-left: 3px solid var(--text-dim); }
.act-btn.cthulhu-adept {
  width: 100%;
  min-height: 36px;
  border-color: rgba(35, 196, 188, 0.48);
  background: linear-gradient(135deg, rgba(4, 41, 56, 0.96), rgba(7, 91, 95, 0.72));
  color: #a7fff4;
  letter-spacing: 0.04em;
  box-shadow: inset 0 0 18px rgba(29, 188, 179, 0.12), 0 0 12px rgba(13, 105, 111, 0.2);
}
.act-btn.cthulhu-adept:hover:not(:disabled) {
  border-color: #52e4d6;
  box-shadow: inset 0 0 22px rgba(29, 188, 179, 0.18), 0 0 16px rgba(32, 190, 185, 0.32);
}
.act-btn.half-life {
  border-left: 3px solid #e78124;
  border-color: rgba(231, 129, 36, 0.42);
  background: linear-gradient(135deg, rgba(231, 129, 36, 0.12), var(--bg-secondary));
  color: #ffc06d;
}
.half-life-lambda {
  color: #ff9b3d;
  font: 900 16px/1 Arial, sans-serif;
  text-shadow: 0 0 7px rgba(231, 129, 36, 0.6);
}
.act-btn.gordon-wake {
  border-left: 3px solid #b8d8b2;
  border-color: rgba(184, 216, 178, 0.34);
  background: linear-gradient(135deg, rgba(124, 167, 116, 0.11), var(--bg-secondary));
  color: #d6ebd2;
}

.act-btn:active:not(:disabled)::after {
  content: '';
  position: absolute;
  inset: 0;
  background: radial-gradient(circle at center, rgba(255,255,255,0.15), transparent 70%);
  animation: act-btn-ripple 0.4s ease-out;
  pointer-events: none;
}
.act-btn.shield:active:not(:disabled)::after {
  background: radial-gradient(circle at center, rgba(110, 170, 240, 0.2), transparent 70%);
}
.act-btn.auto:active:not(:disabled)::after {
  background: radial-gradient(circle at center, rgba(63, 167, 61, 0.2), transparent 70%);
}
.act-btn.undo:active::after {
  background: radial-gradient(circle at center, rgba(230, 148, 74, 0.2), transparent 70%);
}

@keyframes act-btn-ripple {
  0% { transform: scale(0); opacity: 1; }
  100% { transform: scale(2.5); opacity: 0; }
}

.act-btn:hover:not(:disabled) {
  background: linear-gradient(180deg, var(--bg-card-hover), var(--bg-secondary));
  border-color: var(--accent-blue);
  transform: translateY(-1px);
  box-shadow: 0 3px 8px rgba(0, 0, 0, 0.3), 0 0 4px rgba(110, 170, 240, 0.1);
}

.act-btn:active:not(:disabled) {
  transform: translateY(0) scale(0.97);
}

.act-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
  filter: grayscale(0.5);
  border-left-color: var(--border-subtle);
}

.act-btn.shield:hover:not(:disabled) { border-color: var(--accent-blue); box-shadow: 0 3px 8px rgba(0,0,0,0.3), 0 0 8px rgba(110, 170, 240, 0.15); }
.act-btn.auto:hover:not(:disabled) { border-color: var(--accent-green); box-shadow: 0 3px 8px rgba(0,0,0,0.3), 0 0 8px rgba(63, 167, 61, 0.15); }
.act-btn.undo:hover { border-color: var(--accent-orange); box-shadow: 0 3px 8px rgba(0,0,0,0.3), 0 0 8px rgba(230, 148, 74, 0.15); }
.act-btn.skip:hover:not(:disabled) { border-color: var(--text-muted); }
.act-btn.half-life:hover:not(:disabled) { border-color: #e78124; box-shadow: 0 3px 8px rgba(0,0,0,0.3), 0 0 10px rgba(231, 129, 36, 0.22); }
.act-btn.gordon-wake:hover:not(:disabled) { border-color: #b8d8b2; box-shadow: 0 3px 8px rgba(0,0,0,0.3), 0 0 10px rgba(124, 167, 116, 0.18); }

.act-btn.predict-confirm {
  background: rgba(180, 150, 255, 0.06);
  border-color: rgba(180, 150, 255, 0.3);
  color: var(--accent-purple);
}
.act-btn.predict-confirm:hover:not(:disabled) { background: rgba(180, 150, 255, 0.12); }

.act-btn.darksci-stable {
  background: rgba(60, 120, 255, 0.08);
  border-color: rgba(60, 120, 255, 0.3);
  color: #6eaaff;
}
.act-btn.darksci-stable:hover { background: rgba(60, 120, 255, 0.15); }

.act-btn.darksci-unstable {
  background: rgba(255, 60, 60, 0.08);
  border-color: rgba(255, 60, 60, 0.3);
  color: #ff6e6e;
}
.act-btn.darksci-unstable:hover { background: rgba(255, 60, 60, 0.15); }

.act-btn.young-gleb {
  background: rgba(255, 180, 40, 0.08);
  border-color: rgba(255, 180, 40, 0.3);
  color: #ffb428;
}
.act-btn.young-gleb:hover { background: rgba(255, 180, 40, 0.15); }

.act-btn.dopa-stomp {
  background: rgba(255, 60, 60, 0.08);
  border-color: rgba(255, 60, 60, 0.3);
  color: #ff6e6e;
}
.act-btn.dopa-stomp:hover { background: rgba(255, 60, 60, 0.15); }

.act-btn.dopa-farm {
  background: rgba(60, 180, 60, 0.08);
  border-color: rgba(60, 180, 60, 0.3);
  color: #6ecc6e;
}
.act-btn.dopa-farm:hover { background: rgba(60, 180, 60, 0.15); }

.act-btn.dopa-domination {
  background: rgba(180, 60, 255, 0.08);
  border-color: rgba(180, 60, 255, 0.3);
  color: #b46eff;
}
.act-btn.dopa-domination:hover { background: rgba(180, 60, 255, 0.15); }

.act-btn.dopa-roam {
  background: rgba(74, 144, 217, 0.08);
  border-color: rgba(74, 144, 217, 0.3);
  color: #4a90d9;
}
.act-btn.dopa-roam:hover { background: rgba(74, 144, 217, 0.15); }

.dopa-second-hint {
  font-size: 11px;
  font-weight: 700;
  color: #4a90d9;
  animation: dopa-pulse 1.5s ease-in-out infinite;
}
@keyframes dopa-pulse {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}
.lvlup-gate-hint {
  font-size: 11px;
  font-weight: 800;
  text-align: center;
  padding: 4px 8px;
  margin-bottom: 4px;
  border-radius: 6px;
  color: #d98b1f;
  background: rgba(217, 139, 31, 0.12);
  border: 1px solid rgba(217, 139, 31, 0.35);
  animation: dopa-pulse 1.5s ease-in-out infinite;
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

.multiplier-badge {
  padding: 4px 12px;
  border-radius: var(--radius);
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.3px;
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-muted);
  border: 1px solid rgba(255, 255, 255, 0.08);
  transition: all 0.3s ease;
}
.multiplier-badge.mult-active {
  background: rgba(212, 160, 23, 0.12);
  color: var(--accent-gold);
  border-color: rgba(212, 160, 23, 0.3);
  box-shadow: 0 0 8px rgba(212, 160, 23, 0.1);
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

.justice-dust-layer {
  position: fixed;
  inset: 0;
  z-index: 2400;
  pointer-events: none;
  overflow: hidden;
}

.justice-dust-particle {
  position: fixed;
  display: block;
  border-radius: 50%;
  opacity: 0;
  background: rgba(164, 116, 255, 0.68);
  box-shadow: 0 0 5px rgba(139, 92, 246, 0.38);
  animation: justice-dust-flight 920ms cubic-bezier(0.3, 0.08, 0.3, 1) var(--justice-delay) forwards;
}

@keyframes justice-dust-flight {
  0% { opacity: 0; transform: translate(0, 0) scale(0.55); }
  18% { opacity: 0.5; }
  72% { opacity: 0.32; }
  100% { opacity: 0; transform: translate(var(--justice-dx), var(--justice-dy)) scale(0.15); }
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

/* v3 Dynamic: fill available space */
.fight-panel {
  padding: 5px 8px;
  min-height: 200px;
  flex: 1;
  overflow-y: auto;
  margin-bottom: 5px;
  display: flex;
  flex-direction: column;
}

.fight-section {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.fight-section-fixed {
  flex: 0 0 auto;
}

/* ── Avatar section in game-right ──────────────────────────────── */
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
.game-page.is-terminal-game .gr-avatar-wrap,
.game-page.is-terminal-game .gr-avatar-wrap.place-1,
.game-page.is-terminal-game .gr-avatar-wrap.place-2,
.game-page.is-terminal-game .gr-avatar-wrap.place-3,
.game-page.is-terminal-game .gr-avatar-wrap.place-mid,
.game-page.is-terminal-game .gr-avatar-wrap.place-last {
  border: 2px solid rgba(0, 255, 65, 0.62);
  background: #000;
  box-shadow: 0 0 16px rgba(0, 255, 65, 0.3), inset 0 0 24px rgba(0, 255, 65, 0.08);
  filter: none;
  animation: terminal-avatar-frame 3.8s steps(1, end) infinite;
}
.game-page.is-terminal-game .gr-avatar-wrap::after {
  content: '';
  position: absolute;
  z-index: 2;
  inset: 0;
  padding: 0;
  border-radius: 0;
  background:
    repeating-linear-gradient(0deg, transparent 0 3px, rgba(0, 255, 65, 0.08) 4px),
    linear-gradient(90deg, transparent 49%, rgba(0, 255, 65, 0.08) 50%, transparent 51%);
  mask: none;
  -webkit-mask: none;
  animation: none;
  pointer-events: none;
}
.game-page.is-terminal-game .gr-avatar-img {
  filter: grayscale(1) sepia(0.55) hue-rotate(72deg) contrast(1.28) brightness(0.78);
  animation: terminal-avatar-glitch 4.6s steps(1, end) infinite;
}
.game-page.is-terminal-game .gr-name {
  color: #8dffa8;
  font-family: var(--font-mono);
  letter-spacing: 0.035em;
  text-shadow: 2px 0 rgba(0, 255, 213, 0.3), -2px 0 rgba(0, 255, 65, 0.4), 0 0 9px #00ff41;
}
.game-page.is-terminal-game .gr-username {
  color: rgba(116, 255, 150, 0.46);
  font-family: var(--font-mono);
}
@keyframes terminal-avatar-frame {
  0%, 90%, 100% { transform: translate(0); }
  92% { transform: translate(-2px, 1px); }
  94% { transform: translate(2px, -1px); }
  96% { transform: translate(0); }
}
@keyframes terminal-avatar-glitch {
  0%, 86%, 100% { transform: scale(1.01); clip-path: none; }
  88% { transform: scale(1.025) translateX(-3px); clip-path: inset(12% 0 66% 0); }
  90% { transform: scale(1.025) translateX(3px); clip-path: inset(62% 0 13% 0); }
  92% { transform: scale(1.01); clip-path: none; }
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
/* Rarity badges */
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
/* Mastery badges */
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
.mastery-diamond { background: linear-gradient(135deg, rgba(185,242,255,0.3), rgba(255,255,255,0.15)); color: #e0f7ff; border: 1px solid rgba(185,242,255,0.5); text-shadow: 0 0 10px rgba(185,242,255,0.6); box-shadow: 0 0 16px rgba(185,242,255,0.15); animation: gr-mastery-shimmer 2s ease-in-out infinite; }
@keyframes gr-mastery-shimmer {
  0%, 100% { opacity: 1; box-shadow: 0 0 16px rgba(185,242,255,0.15); }
  50% { opacity: 0.85; box-shadow: 0 0 24px rgba(185,242,255,0.3); }
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
  overflow-x: hidden;
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

/* Left history items arrive from the current shared-log panel on the right. */
.prev-logs-panel .prev-log-item:not(.prev-log-visible) {
  transform: translateX(60px) scale(0.95);
}

/* History panel exit: fade out in place. */
.prev-log-item.prev-log-fade-exit {
  transform: translateX(0) !important;
}

/* Container-level slide animations */
@keyframes slide-from-left {
  from { transform: translateX(-15px); opacity: 0.7; }
  to { transform: translateX(0); opacity: 1; }
}

@keyframes slide-from-right-far {
  from { transform: translateX(60px); opacity: 0.2; }
  to { transform: translateX(0); opacity: 1; }
}

/* Exit animation: right-panel items slide left toward ordinary history. */
@keyframes slide-to-left {
  0% { transform: translateX(0); opacity: 1; }
  100% { transform: translateX(-60px); opacity: 0; }
}

.events-panel:not(.prev-logs-panel) .prev-logs.slide-enter {
  animation: slide-from-left 0.35s ease-out;
}

.prev-logs-panel .prev-logs.slide-enter {
  animation: slide-from-right-far 0.5s ease-out;
}

.slide-exit-left {
  animation: slide-to-left 0.35s cubic-bezier(0.4, 0, 0.6, 1) forwards;
  pointer-events: none;
}
.slide-exit-left .prev-log-item {
  transition: none;
}

.prev-log-text {
  flex: 1;
  color: var(--text-secondary);
  font-family: var(--font-mono);
}

.prev-log-text :deep(strong) { color: var(--accent-gold); }
.prev-log-text :deep(em) { color: var(--accent-blue); }
.prev-log-text :deep(u) { color: var(--accent-green); }
.prev-log-text :deep(.madara-callout) {
  color: #ff3535;
  font-weight: 900;
  text-shadow: 0 0 5px #ff1f1f, 0 0 14px rgba(255, 31, 31, 0.95), 0 0 28px rgba(255, 31, 31, 0.7);
  animation: madara-callout-pulse 0.9s ease-in-out infinite alternate;
}
.prev-log-text :deep(.halflife-win-callout) {
  color: #ffe56d;
  font-weight: 950;
  text-shadow: 0 0 5px #ffd84d, 0 0 15px rgba(255, 216, 77, 0.95), 0 0 30px rgba(255, 174, 45, 0.72);
  animation: halflife-win-callout-pulse 1.1s ease-in-out infinite alternate;
}
@keyframes madara-callout-pulse {
  from { filter: brightness(1); }
  to { filter: brightness(1.7); }
}
@keyframes halflife-win-callout-pulse {
  from { filter: brightness(1); }
  to { filter: brightness(1.55); }
}
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

/* ══════════════════════════════════════════════════════════════════
   MOBILE RESPONSIVE
   ══════════════════════════════════════════════════════════════════ */

/* Tablet: stack to single column */
@media (max-width: 1200px) {
  .game-header {
    flex-wrap: wrap;
    gap: 4px;
  }
  .header-right {
    flex-wrap: wrap;
    gap: 4px;
  }
}

/* Mobile: full responsive */
@media (max-width: 768px) {
  .game-page {
    height: calc(100vh - 40px - 1rem); /* top-bar 40px, padding 0.5rem*2 */
  }
  .game-layout {
    grid-template-columns: 1fr;
    gap: 6px;
  }
  .game-left { order: 1; }
  .game-center { order: 0; }
  .game-right { order: 2; }

  .game-header {
    flex-wrap: wrap;
    padding: 4px 0;
  }
  .header-center { flex-wrap: wrap; gap: 4px; }
  .header-right {
    width: 100%;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 4px;
  }

  .round-badge, .mode-badge, .status-chip {
    font-size: 9px;
    padding: 3px 8px;
  }

  /* Fight panel: more compact */
  .fight-panel { padding: 6px; }

  /* Log panels: stack vertically on mobile */
  .logs-row-top {
    flex-direction: column !important;
  }
  .events-panel {
    min-height: auto !important;
  }

  /* VFX messages: full width on mobile */
  .vfx-messages {
    right: 8px !important;
    left: 8px !important;
    max-width: none !important;
  }
  .vfx-msg {
    font-size: 11px !important;
    padding: 8px 12px !important;
  }
}

/* Small mobile */
@media (max-width: 480px) {
  .game-page {
    height: calc(100vh - 36px - 0.75rem); /* top-bar 36px, padding 0.375rem*2 */
  }
  .game-header {
    gap: 2px;
  }
  .vol-control {
    order: 10;
  }
  .round-announce-number {
    font-size: 48px;
  }
  .round-announce-label {
    font-size: 11px;
    letter-spacing: 4px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .rumbling-apocalypse,
  .rumbling-smoke,
  .rumbling-fireline,
  .rumbling-ember,
  .rumbling-shake-1,
  .rumbling-shake-2,
  .rumbling-shake-3,
  .rumbling-shake-4 {
    animation: none !important;
  }
  .rumbling-ember { display: none; }
  .game-page.is-terminal-game .gr-avatar-wrap,
  .game-page.is-terminal-game .gr-avatar-img {
    animation: none !important;
  }
  .gameover-title,
  .gameover-entry,
  .gameover-confetti,
  .confetti-piece {
    animation: none !important;
  }
  .gameover-entry { opacity: 1; }
  .gameover-confetti { display: none; }
}
</style>
