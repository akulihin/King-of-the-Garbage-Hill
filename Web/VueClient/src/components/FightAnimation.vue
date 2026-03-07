<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import type { FightEntry, ForOneFightMod, Player, Prediction, CharacterInfo } from 'src/services/signalr'
import FightArena from './fight/FightArena.vue'
import {
  FightSoundPool,
  playDoomsDayFight,
  playDoomsDayDraw,
  playDoomsDayRndRoll,
  playDoomsDayScroll,
  playDoomsDayNoFights,
  playWinSpecialsForAll,
  GeraltFightSoundPool,
  FolkPercussionPool,
  playDoomsDayCustom,
  doomsDayWinLosePath,
  getMemeLoseSoundPath,
  getGeraltVocalWinLayerPath,
  playClipsBatched,
  type SyncClip,
} from 'src/services/sound'

const props = withDefaults(defineProps<{
  fights: FightEntry[]
  letopis?: string
  gameStory?: string | null
  players?: Player[]
  myPlayerId?: string
  predictions?: Prediction[]
  isAdmin?: boolean
  showDetailedFactors?: boolean
  characterCatalog?: CharacterInfo[]
  initialFightIndex?: number
}>(), {
  letopis: '',
  gameStory: null,
  players: () => [],
  myPlayerId: '',
  predictions: () => [],
  isAdmin: false,
  showDetailedFactors: false,
  characterCatalog: () => [],
  initialFightIndex: undefined,
})

const showDetails = computed(() => props.showDetailedFactors || props.isAdmin)

const emit = defineEmits<{
  (e: 'resist-flash', stats: string[]): void
  (e: 'justice-reset'): void
  (e: 'justice-up'): void
  (e: 'replay-ended'): void
  (e: 'update:fightIndex', idx: number): void
  (e: 'update:currentFight', fight: FightEntry | null): void
}>()

/** Active tab: 'fights' = replay, 'all' = compact results list, 'letopis' = full text log, 'story' = AI story */
const activeTab = ref<'fights' | 'all' | 'letopis' | 'story'>('fights')
/** Tracks whether user manually switched tabs this round (prevents auto-transition) */
const userSwitchedTab = ref(false)
/** True when new fights arrived while user was on a non-fights tab */
const hasUnseenFights = ref(false)
/** Whether the story popup overlay is visible */
const showStoryPopup = ref(false)
/** Whether the story popup has been shown once already (don't auto-show again) */
const storyPopupShown = ref(false)

function setTab(tab: 'fights' | 'all' | 'letopis' | 'story') {
  activeTab.value = tab
  userSwitchedTab.value = true
  if (tab === 'fights') hasUnseenFights.value = false
}

function dismissStoryPopup() {
  showStoryPopup.value = false
}

// Auto-show story popup when story arrives; reset when story is cleared
watch(() => props.gameStory, (val) => {
  if (val && !storyPopupShown.value) {
    showStoryPopup.value = true
    storyPopupShown.value = true
  } else if (!val) {
    storyPopupShown.value = false
    showStoryPopup.value = false
  }
})

// ── Playback state ──────────────────────────────────────────────────
const currentFightIdx = ref(0)
const currentStep = ref(0)
const isPlaying = ref(true)
const speed = ref(1)
const skippedToEnd = ref(false)
const lastAnimatedRound = ref<string>('')
let timer: ReturnType<typeof setTimeout> | null = null

// ── Fight result glow + screen shake ──────────────────────────────
const fightResult = ref<'win' | 'loss' | null>(null)
const fightShake = ref(false)

// ── Slam & needle state (declared early for use in computed/watchers below) ──
const slamPhase = ref<'idle' | 'rush' | 'impact' | 'resolved'>('idle')
const r3NeedlePos = ref(0)
const r3NeedleSettled = ref(false)

// ── My username and character (for filtering) ──────────────────────────
const myPlayer = computed(() => props.players.find((pl: Player) => pl.playerId === props.myPlayerId) ?? null)
const myUsername = computed(() => myPlayer.value?.discordUsername ?? '')
const myCharacterName = computed(() => myPlayer.value?.character.name ?? '')

function isFightMine(f: FightEntry): boolean {
  if (props.isAdmin) return true
  if (!myUsername.value) return false
  return f.attackerName === myUsername.value || f.defenderName === myUsername.value
}

/** Find the index of a fight from the full list within myFights and jump to its replay */
function jumpToFightReplay(f: FightEntry) {
  if (!isFightMine(f)) return
  // Find which myFights index this fight corresponds to
  const idx = myFights.value.findIndex((mf: FightEntry) =>
    mf.attackerName === f.attackerName && mf.defenderName === f.defenderName
  )
  if (idx === -1) return
  clearTimer()
  isPlaying.value = false
  skippedToEnd.value = true
  currentFightIdx.value = idx
  // Switch tab after setting fight index so totalSteps computes correctly
  activeTab.value = 'fights'
  nextTick(() => { currentStep.value = totalSteps.value - 1 })
}

/** Fights shown in the replay — only own fights (admins see all) */
const myFights = computed(() => props.fights.filter(isFightMine))

const fight = computed<FightEntry | null>(() => {
  if (!myFights.value.length) return null
  if (currentFightIdx.value >= myFights.value.length) return null
  return myFights.value[currentFightIdx.value]
})

watch(fight, (f) => { emit('update:currentFight', f) }, { immediate: true })

const isSpecialOutcome = computed(() => {
  if (!fight.value) return false
  return fight.value.outcome === 'block' || fight.value.outcome === 'skip'
})

// ── Is this MY fight? ───────────────────────────────────────────────
const isMyFight = computed(() => {
  if (!fight.value) return false
  if (props.isAdmin) return true
  if (!myUsername.value) return false
  return fight.value.attackerName === myUsername.value ||
         fight.value.defenderName === myUsername.value
})

/** True when we are the defender — need to flip left/right so we're always on the left */
const isFlipped = computed(() => {
  if (!fight.value || !myUsername.value) return false
  // Admin viewing someone else's fight: don't flip, show as-is (attacker left)
  if (props.isAdmin && fight.value.attackerName !== myUsername.value && fight.value.defenderName !== myUsername.value) return false
  return fight.value.defenderName === myUsername.value
})

// ── Display accessors (flipped when we are defender) ────────────────
/** "Left" player = us, "Right" player = opponent */
const leftName = computed(() => isFlipped.value ? fight.value!.defenderName : fight.value!.attackerName)
const leftCharName = computed(() => isFlipped.value ? fight.value!.defenderCharName : fight.value!.attackerCharName)
const leftAvatar = computed(() => isFlipped.value ? fight.value!.defenderAvatar : fight.value!.attackerAvatar)
const rightName = computed(() => isFlipped.value ? fight.value!.attackerName : fight.value!.defenderName)
const rightCharName = computed(() => isFlipped.value ? fight.value!.attackerCharName : fight.value!.defenderCharName)
const rightAvatar = computed(() => isFlipped.value ? fight.value!.attackerAvatar : fight.value!.defenderAvatar)
/** Did the left (us) win? */
const leftWon = computed(() => {
  if (!fight.value) return false
  if (isFlipped.value) return fight.value.outcome === 'loss' // defender won
  return fight.value.outcome === 'win' // attacker won
})
/** Arrow direction: true = left attacked right (→), false = right attacked left (←) */
const attackedRight = computed(() => !isFlipped.value)
/** Sign multiplier: when flipped, negate all delta values so + = good for us */
const sign = computed(() => isFlipped.value ? -1 : 1)

/** Our actual moral change from this fight (using per-player snapshots) */
const ourMoralChange = computed(() => {
  const f = fight.value
  if (!f) return 0
  // We are left; if flipped we are the defender, otherwise the attacker
  return isFlipped.value ? f.defenderMoralChange : f.attackerMoralChange
})

// ── Round 1 factors (weighing machine deltas) ───────────────────────
type Factor = {
  label: string
  detail: string
  value: number
  highlight: 'good' | 'bad' | 'neutral'
  badge?: string
  showValue?: boolean
  tier?: number  // 0 = slight, 1 = medium, 2 = strong (non-admin only)
}

const round1Factors = computed<Factor[]>(() => {
  const f = fight.value
  if (!f || isSpecialOutcome.value) return []
  const s = sign.value // +1 or -1 depending on flip
  const list: Factor[] = []

  // Helper: after applying sign, positive = good for us
  const hl = (v: number): 'good' | 'bad' | 'neutral' => v > 0 ? 'good' : v < 0 ? 'bad' : 'neutral'


  // 1. Versatility — show whenever any stat differs, even if weighing bonus is 0
  if (f.versatilityIntel !== 0 || f.versatilityStr !== 0 || f.versatilitySpeed !== 0) {
    const v = f.versatilityWeighingDelta * s
    // Build stat-by-stat breakdown from our (left) perspective
    const wi = f.versatilityIntel * (isFlipped.value ? -1 : 1)
    const ws = f.versatilityStr * (isFlipped.value ? -1 : 1)
    const wsp = f.versatilitySpeed * (isFlipped.value ? -1 : 1)
    const ic = (val: number) => val > 0 ? '<span class="gi-ok">&#x2713;</span>' : val < 0 ? '<span class="gi-fail">&#x2717;</span>' : '<span class="gi-tie">&#x2015;</span>'
    const detail = `<span class="gi gi-int">INT</span>${ic(wi)} <span class="gi gi-str">STR</span>${ic(ws)} <span class="gi gi-spd">SPD</span>${ic(wsp)}`
    // Tier based on how many stats we won/lost (not the delta magnitude, since it's always ±5)
    const stats = [wi, ws, wsp]
    const wins = stats.filter(x => x > 0).length
    const losses = stats.filter(x => x < 0).length
    const vTier = v > 0 ? Math.max(0, wins - 1) : v < 0 ? Math.max(0, losses - 1) : 0
    list.push({
      label: 'Versatility',
      detail,
      value: v,
      highlight: hl(v),
      showValue: showDetails.value,
      tier: vTier,
    })
  }

  // 2. Nemesis
  if (f.nemesisWeighingDelta !== 0) {
    const v = f.nemesisWeighingDelta * s + f.nemesisMultiplierSkillDifference
    // Determine who has nemesis from our (left) perspective
    const weNemesis = isFlipped.value ? f.isNemesisTarget : f.isNemesisMe
    const theyNemesis = isFlipped.value ? f.isNemesisMe : f.isNemesisTarget
    const ourClass = isFlipped.value ? f.defenderClass : f.attackerClass
    const theirClass = isFlipped.value ? f.attackerClass : f.defenderClass
    const ci = (c: string) => c === 'Интеллект' ? '<span class="gi gi-int">INT</span>' : c === 'Сила' ? '<span class="gi gi-str">STR</span>' : c === 'Скорость' ? '<span class="gi gi-spd">SPD</span>' : '<span class="gi">?</span>'
    const nemesisVerb = (c: string) => c === 'Сила' ? 'Пресанул' : c === 'Интеллект' ? 'Обманул' : c === 'Скорость' ? 'Обогнал' : '???'
    let detail: string
    if (weNemesis && theyNemesis) {
      detail = `Neutral: ${ci(ourClass)}→${ci(theirClass)} / ${ci(theirClass)}→${ci(ourClass)}`
    } else if (weNemesis) {
      detail = `${ci(ourClass)} <span class="dom-good">${nemesisVerb(ourClass)}</span> ${ci(theirClass)}`
    } else {
      detail = `${ci(theirClass)} <span class="dom-bad">${nemesisVerb(theirClass)}</span> ${ci(ourClass)}`
    }
    const nTier = Math.abs(v) <= 3 ? 0 : Math.abs(v) <= 6 ? 1 : 2
    list.push({
      label: 'Nemesis',
      detail,
      value: v,
      highlight: hl(v),
      showValue: showDetails.value,
      tier: nTier,
    })
  }

  // 3. Scale (stats + skill*multiplier)
  {
    const v = f.scaleWeighingDelta * s
    const ourScale = isFlipped.value ? f.scaleTarget : f.scaleMe
    const theirScale = isFlipped.value ? f.scaleMe : f.scaleTarget
    const scaleHint = factorHint(v, { good: ['Slight edge', 'Stronger', 'Dominant'], bad: ['Slight gap', 'Weaker', 'Overpowered'], even: 'Even' })
    list.push({
      label: 'INT + STR + SPD',
      detail: showDetails.value
        ? `${scaleHint.text} <span class="admin-extra">(${ourScale.toFixed(1)} vs ${theirScale.toFixed(1)})</span>`
        : scaleHint.text,
      value: v,
      showValue: showDetails.value,
      highlight: hl(v),
      tier: scaleHint.tier,
    })
  }



  // 4. Psyche
  if (f.psycheWeighingDelta !== 0) {
    const v = f.psycheWeighingDelta * s
    const psyHint = factorHint(v, { good: ['Composed', 'Composed', 'Composed'], bad: ['Shaken', 'Shaken', 'Shaken'], even: 'Neutral' })
    list.push({
      label: 'PSY difference',
      detail: psyHint.text,
      value: v,
      highlight: hl(v),
      showValue: showDetails.value,
      tier: psyHint.tier,
    })
  }

  // 5. Skill difference
  if (f.skillWeighingDelta !== 0) {
    const v = f.skillWeighingDelta * s - f.nemesisMultiplierSkillDifference;
    const skillHint = factorHint(v, { good: ['Slight edge', 'Skilled', 'Mastery'], bad: ['Slight gap', 'Outskilled', 'Outclassed'], even: 'Even' })
    list.push({
      label: 'Skill',
      detail: skillHint.text,
      value: v,
      showValue: showDetails.value,
      highlight: hl(v),
      tier: skillHint.tier,
    })
  }

  // 6. Justice in weighing
  if (f.justiceWeighingDelta !== 0) {
    const v = f.justiceWeighingDelta * s
    const justiceHint = factorHint(v, { good: ['Slight favor', 'Favored', 'Destined'], bad: ['Slight disfavor', 'Unfavored', 'Cursed'], even: 'Balanced' })
    list.push({
      label: 'Justice',
      detail: justiceHint.text,
      value: v,
      highlight: hl(v),
      showValue: showDetails.value,
      tier: justiceHint.tier,
    })
  }

  return list
})

// ── Step counting for animation ─────────────────────────────────────
// For MY fights: intro(0) → R1 factors(1..N) → R1 result(N+1) → R2(N+2) → [R3(N+3)] → result(last)
// For ENEMY fights: intro(0) → result(1) (just 2 steps)
const totalSteps = computed(() => {
  if (isSpecialOutcome.value) return 2
  if (!isMyFight.value) return 2 // enemy: intro + result
  let steps = 1 + round1Factors.value.length + 1 + 1 + 1 // intro + R1factors + R1result + R2 + finalResult
  if (fight.value?.usedRandomRoll) steps += 1 // R3 (modifiers + roll together)
  return steps
})

// ── Weighing machine bar animation ──────────────────────────────────
// Switch: false = show real (unclamped) bar for all players, true = clamp factors for non-admins
const normalizeBar = false

// Target value (jumps per step)
const targetWeighingValue = computed(() => {
  if (!fight.value || isSpecialOutcome.value) return 0
  const factors = round1Factors.value
  const useRaw = !normalizeBar || showDetails.value
  const addFactor = (v: number) => useRaw ? v : clampFactor(v)

  if (skippedToEnd.value || !isMyFight.value) {
    if (useRaw) return fight.value.weighingMachine * sign.value
    return factors.reduce((sum, f) => sum + clampFactor(f.value), 0)
  }

  const factorCount = factors.length
  let accumulated = 0
  const factorStepsShown = Math.min(currentStep.value, factorCount)
  for (let i = 0; i < factorStepsShown; i++) {
    accumulated += addFactor(factors[i].value)
  }
  if (currentStep.value > factorCount) {
    accumulated = useRaw
      ? fight.value.weighingMachine * sign.value
      : factors.reduce((sum, f) => sum + clampFactor(f.value), 0)
  }
  return accumulated
})

// Smoothly animated weighing value
const animatedWeighingValue = ref(0)
let weighingAnimFrame: ReturnType<typeof requestAnimationFrame> | null = null

watch(targetWeighingValue, (target: number) => {
  if (weighingAnimFrame) cancelAnimationFrame(weighingAnimFrame)
  const start = animatedWeighingValue.value
  const diff = target - start
  if (Math.abs(diff) < 0.01) { animatedWeighingValue.value = target; return }
  const duration = 500 // ms
  const startTime = performance.now()
  function tick(now: number) {
    const elapsed = now - startTime
    const t = Math.min(elapsed / duration, 1)
    // ease-out cubic
    const ease = 1 - Math.pow(1 - t, 3)
    animatedWeighingValue.value = start + diff * ease
    if (t < 1) weighingAnimFrame = requestAnimationFrame(tick)
  }
  weighingAnimFrame = requestAnimationFrame(tick)
}, { immediate: true })

// Per-fight random nudge (0–15%) that pushes the bar further in the winning direction (only when normalized)
const barRandomNudge = ref(Math.random() * 15)
watch(currentFightIdx, () => {
  barRandomNudge.value = Math.random() * 15
  emit('update:fightIndex', currentFightIdx.value)
})

const barPosition = computed(() => {
  const val = animatedWeighingValue.value
  const clamped = Math.max(-50, Math.min(50, val))
  const base = 50 + (clamped / 50) * 50
  if (!normalizeBar) return base
  // Nudge away from center in the winning direction
  if (val > 0) return Math.min(100, base + barRandomNudge.value)
  if (val < 0) return Math.max(0, base - barRandomNudge.value)
  return base
})

// ── Visibility helpers ──────────────────────────────────────────────
const showR1Result = computed(() => {
  if (skippedToEnd.value || !isMyFight.value) return true
  return currentStep.value > round1Factors.value.length
})

const showR2 = computed(() => {
  if (skippedToEnd.value || !isMyFight.value) return true
  return currentStep.value > round1Factors.value.length + 1
})

const showR3 = computed(() => {
  if (!fight.value?.usedRandomRoll) return false
  if (skippedToEnd.value || !isMyFight.value) return true
  return currentStep.value > round1Factors.value.length + 2
})

/** Show the animated roll bar (same step as R3 modifiers) */
const showR3Roll = computed(() => showR3.value)

const showFinalResult = computed(() => {
  if (skippedToEnd.value || !isMyFight.value) return true
  return currentStep.value >= totalSteps.value - 1
})

const isPortalSwap = computed(() => fight.value?.portalGunSwap ?? false)

// ── Emit resist flash / justice reset to PlayerCard when result appears ──
watch(showFinalResult, (show: boolean) => {
  if (!show || !fight.value || !isMyFight.value) return
  const f = fight.value
  const weWon = isFlipped.value ? f.outcome === 'loss' : f.outcome === 'win'
  const weLost = !weWon && f.outcome !== 'block' && f.outcome !== 'skip'

  // Our pre-fight justice value
  const ourPreFightJustice = isFlipped.value ? f.justiceTarget : f.justiceMe

  // Justice resets when we win AND we had justice > 0
  if (weWon && ourPreFightJustice > 0) {
    emit('justice-reset')
  }

  // Fight result glow
  if (weWon) {
    fightResult.value = 'win'
  } else if (weLost) {
    fightResult.value = 'loss'
  }
  // Screen shake on big weighing differences
  if (Math.abs(f.totalWeighingDelta ?? 0) > 15) {
    fightShake.value = true
    setTimeout(() => { fightShake.value = false }, 500)
  }
  setTimeout(() => { fightResult.value = null }, 2000)

  // Resist flash when we lost
  if (weLost) {
    const flashStats: string[] = []
    if (f.intellectualDamage) flashStats.push('intelligence')
    if (f.emotionalDamage) flashStats.push('psyche')
    if (f.resistIntelDamage > 0) flashStats.push('intelligence')
    if (f.resistStrDamage > 0) flashStats.push('strength')
    if (f.resistPsycheDamage > 0) flashStats.push('psyche')
    if (flashStats.length > 0) {
      emit('resist-flash', [...new Set(flashStats)])
    }
    // Justice up: only if change > 0 AND we weren't already at max (5)
    if (f.justiceChange > 0 && ourPreFightJustice < 5) {
      emit('justice-up')
    }
  }
})

// ── Playback control ────────────────────────────────────────────────
function clearTimer() {
  if (timer) { clearTimeout(timer); timer = null }
}
function stepDelay(): number {
  // Enemy fights are quick — short delay just to show the card briefly
  if (!isMyFight.value) return 200 / speed.value
  return 800 / speed.value
}
function betweenFightDelay(): number {
  // Short pause between enemy fights, longer for own fights
  if (!isMyFight.value) return 400 / speed.value
  return 1500 / speed.value
}

function proceedToNextFight() {
  const delay = betweenFightDelay()
  setTimeout(() => {
    if (!isPlaying.value) return
    if (currentFightIdx.value < myFights.value.length - 1) {
      playDoomsDayScroll()
      roundResults.value = []
      folkPercussionPool.rollForFight()
      currentFightIdx.value++
      currentStep.value = 0
      scheduleNext()
    } else {
      isPlaying.value = false
      emit('replay-ended')
      if (!userSwitchedTab.value && activeTab.value === 'fights') {
        setTimeout(() => { activeTab.value = 'all' }, 800)
      }
    }
  }, delay)
}

function advanceStep() {
  if (!isPlaying.value || !fight.value) return
  if (currentStep.value < totalSteps.value - 1) {
    currentStep.value++
    scheduleNext()
  } else {
    proceedToNextFight()
  }
}

function scheduleNext() { clearTimer(); timer = setTimeout(advanceStep, stepDelay()) }
function togglePlay() {
  isPlaying.value = !isPlaying.value
  if (isPlaying.value) { skippedToEnd.value = false; scheduleNext() }
  else { clearTimer() }
}
function skipToEnd() {
  clearTimer(); isPlaying.value = false; skippedToEnd.value = true
  currentFightIdx.value = myFights.value.length - 1
  currentStep.value = totalSteps.value - 1
  emit('replay-ended')
}
function setSpeed(s: number) { speed.value = s }
function restart() {
  clearTimer(); currentFightIdx.value = 0; currentStep.value = 0
  skippedToEnd.value = false; isPlaying.value = true; scheduleNext()
}
function restartCurrentFight() {
  clearTimer()
  currentStep.value = 0
  fightResult.value = null
  fightShake.value = false
  r3NeedlePos.value = 0
  r3NeedleSettled.value = false
  slamPhase.value = 'idle'
  skippedToEnd.value = false
  isPlaying.value = true
  scheduleNext()
}

watch(() => props.fights, () => {
  if (!props.fights.length) return
  const fp = props.fights.map((f: FightEntry) => `${f.attackerName}-${f.defenderName}`).join('|')
  if (fp !== lastAnimatedRound.value) {
    // Mark unseen if user is on a different tab
    if (activeTab.value !== 'fights') {
      hasUnseenFights.value = true
    }
    lastAnimatedRound.value = fp
    userSwitchedTab.value = false
    activeTab.value = 'fights'
    fightSoundPool.reset()
    geraltFightPool.reset()
    folkPercussionPool.rollForFight()
    roundResults.value = []
    restart()
  }
}, { deep: true })

// ── One-time initial fight index (for replay deep links) ────────────
{
  let consumed = false
  watch([() => props.initialFightIndex, myFights], ([idx, fights]) => {
    if (consumed || idx == null || !fights.length) return
    consumed = true
    // Wait for the fights watcher's restart() to finish, then jump
    nextTick(() => {
      const clamped = Math.min(idx, fights.length - 1)
      clearTimer()
      isPlaying.value = false
      skippedToEnd.value = true
      currentFightIdx.value = clamped
      nextTick(() => { currentStep.value = totalSteps.value - 1 })
    })
  }, { immediate: true })
}

onUnmounted(() => {
  clearTimer()
  if (weighingAnimFrame) cancelAnimationFrame(weighingAnimFrame)
  if (needleAnimFrame) cancelAnimationFrame(needleAnimFrame)
  clearSlamTimers()
})

// ── Dooms Day sound system ───────────────────────────────────────────

const fightSoundPool = new FightSoundPool()
const geraltFightPool = new GeraltFightSoundPool()
const folkPercussionPool = new FolkPercussionPool()
const roundResults = ref<('w' | 'l')[]>([])

// Watch currentStep to trigger dooms_day sounds during MY fight animation
watch(currentStep, (step: number) => {
  if (!fight.value || !isMyFight.value || isSpecialOutcome.value) return
  if (skippedToEnd.value) return

  const f = fight.value
  const factorCount = round1Factors.value.length
  const hasR3 = f.usedRandomRoll

  // Steps 1..factorCount: R1 factor sounds
  if (step >= 1 && step <= factorCount) {
    const isLastFactor = step === factorCount
    // Geralt: 25% chance for special fight sounds (drowned pool only vs Утопцы enemies)
    if (myCharacterName.value === 'Геральт') {
      const opponentName = rightName.value
      const monsterTypes = myPlayer.value?.passiveAbilityStates?.geralt?.enemyMonsterTypes
      const isDrowned = monsterTypes?.[opponentName] === 'Утопцы'
      const geraltPath = isLastFactor ? geraltFightPool.tryNextFin() : geraltFightPool.tryNext(isDrowned)
      if (geraltPath) { playDoomsDayCustom(geraltPath); return }
    }
    playDoomsDayFight(fightSoundPool, isLastFactor)
    return
  }

  // Step factorCount+1: R1 result
  if (step === factorCount + 1) {
    const r1pts = f.round1PointsWon * sign.value
    if (r1pts === 0) {
      // R1 draw: play draw sound, letter = opposite of next non-draw result
      playDoomsDayDraw()
      const r2pts = f.pointsFromJustice * sign.value
      let nextResult: 'w' | 'l'
      if (r2pts !== 0) {
        nextResult = r2pts > 0 ? 'w' : 'l'
      } else if (hasR3) {
        const atkWon = f.randomNumber <= f.randomForPoint
        nextResult = (sign.value > 0 ? atkWon : !atkWon) ? 'w' : 'l'
      } else {
        nextResult = 'w'
      }
      roundResults.value = [nextResult === 'w' ? 'l' : 'w']
    } else {
      const r1result: 'w' | 'l' = r1pts > 0 ? 'w' : 'l'
      roundResults.value = [r1result]
      const isGeralt = myCharacterName.value === 'Геральт'
      const clips: SyncClip[] = [{ path: doomsDayWinLosePath([r1result], false, false), group: 'doomsDayWinLose' }]
      if (r1result === 'w') {
        const percPath = folkPercussionPool.getLayerPath()
        if (percPath) clips.push({ path: percPath, group: 'doomsDayLayers' })
        const vocalPath = getGeraltVocalWinLayerPath(roundResults.value, false, isGeralt)
        if (vocalPath) clips.push({ path: vocalPath, group: 'doomsDayLayers', gainMultiplier: 0.5 })
      } else {
        const memePath = getMemeLoseSoundPath(roundResults.value, false)
        if (memePath) clips.push({ path: memePath, group: 'doomsDayLayers' })
      }
      void playClipsBatched(clips)
    }
    return
  }

  // Step factorCount+2: R2 justice
  if (step === factorCount + 2) {
    const r2pts = f.pointsFromJustice * sign.value
    if (r2pts === 0) {
      // R2 draw: letter = opposite of previous (R1) result
      playDoomsDayDraw()
      const prev = roundResults.value[roundResults.value.length - 1]
      roundResults.value = [...roundResults.value, prev === 'w' ? 'l' : 'w']
    } else {
      const r2result: 'w' | 'l' = r2pts > 0 ? 'w' : 'l'
      roundResults.value = [...roundResults.value, r2result]
      const isGeralt = myCharacterName.value === 'Геральт'
      const clips: SyncClip[] = [{ path: doomsDayWinLosePath(roundResults.value, false, false), group: 'doomsDayWinLose' }]
      if (r2result === 'w') {
        const percPath = folkPercussionPool.getLayerPath()
        if (percPath) clips.push({ path: percPath, group: 'doomsDayLayers' })
        const vocalPath = getGeraltVocalWinLayerPath(roundResults.value, false, isGeralt)
        if (vocalPath) clips.push({ path: vocalPath, group: 'doomsDayLayers', gainMultiplier: 0.5 })
      } else {
        const memePath = getMemeLoseSoundPath(roundResults.value, false)
        if (memePath) clips.push({ path: memePath, group: 'doomsDayLayers' })
      }
      void playClipsBatched(clips)
    }
    return
  }

  if (hasR3) {
    // Step factorCount+3: R3 modifiers + roll bar together
    if (step === factorCount + 3) {
      playDoomsDayRndRoll()

      const s = sign.value
      const attackerWon = f.randomNumber <= f.randomForPoint
      const weWonR3 = s > 0 ? attackerWon : !attackerWon
      const r3result: 'w' | 'l' = weWonR3 ? 'w' : 'l'
      roundResults.value = [...roundResults.value, r3result]

      // R3 special sounds based on needle distance from threshold
      const thresholdPct = f.randomForPoint / f.maxRandomNumber * 100
      const needlePct = f.randomNumber / f.maxRandomNumber * 100
      const distance = Math.abs(needlePct - thresholdPct)
      const clips: SyncClip[] = [{ path: doomsDayWinLosePath(roundResults.value, false, false), group: 'doomsDayWinLose' }]
      if (weWonR3 && distance < 1) {
        clips.push({ path: 'dooms_day/round_3/round_3_win_less_1_percent.mp3', group: 'doomsDay' })
      } else if (distance < 5) {
        clips.push({ path: 'dooms_day/round_3/round_3_win_or_lose__less_5_percent.mp3', group: 'doomsDay' })
      }
      void playClipsBatched(clips)
      return
    }
  }

  // Final sound deferred to showFinalResult watcher (228ms delay)
})

// No-fights sound: fights exist but none are mine (play only once per round)
// Also: play character victory themes for ALL players when fight results arrive
let lastSeenFightsLength = 0
watch(() => props.fights.length, (cur) => {
  if (cur === 0) { lastSeenFightsLength = 0; return }
  if (cur === lastSeenFightsLength) return
  lastSeenFightsLength = cur
  // Play character victory themes (owner-only: only for the player playing that character)
  playWinSpecialsForAll(props.fights, myCharacterName.value)
  if (myFights.value.length === 0) {
    playDoomsDayNoFights()
  }
})

const fightCardRef = ref<HTMLElement | null>(null)

// ── Phase tracker: 3 phases of each fight ────────────────────────────
// Each phase result: 1 = we won, -1 = we lost, 0 = draw/not reached
const phase1Result = computed(() => {
  if (!fight.value || !isMyFight.value) return 0
  const pts = fight.value.round1PointsWon * sign.value
  return pts > 0 ? 1 : pts < 0 ? -1 : 0
})
const phase2Result = computed(() => {
  if (!fight.value || !isMyFight.value) return 0
  const pts = fight.value.pointsFromJustice * sign.value
  return pts > 0 ? 1 : pts < 0 ? -1 : 0
})

// Justice bar values (from our perspective)
const ourJustice = computed(() => {
  if (!fight.value) return 0
  return isFlipped.value ? fight.value.justiceTarget : fight.value.justiceMe
})
const enemyJustice = computed(() => {
  if (!fight.value) return 0
  return isFlipped.value ? fight.value.justiceMe : fight.value.justiceTarget
})
// Our skill multiplier (for badge row)
const ourSkillMultiplier = computed(() => {
  if (!fight.value) return 1
  return isFlipped.value ? fight.value.skillMultiplierTarget : fight.value.skillMultiplierMe
})

// ForOneFight mod badges — only show mods from our own passives
const ourForOneFightMods = computed(() => {
  if (!fight.value) return []
  return isFlipped.value
    ? (fight.value.defenderForOneFightMods ?? [])
    : (fight.value.attackerForOneFightMods ?? [])
})

function fofBadgeText(mod: ForOneFightMod): string {
  const delta = mod.newValue - mod.originalValue
  const statAbbrev: Record<string, string> = {
    Strength: 'Str', Speed: 'Spd', Intelligence: 'Int',
    Psyche: 'Psy', Skill: 'Skill', Justice: 'Justice'
  }
  const stat = statAbbrev[mod.stat] ?? mod.stat
  const sign = delta >= 0 ? '+' : ''
  const prefix = mod.isOnEnemy ? `${mod.source} (ВРАГ)` : mod.source
  if (mod.newValue === 0 && mod.originalValue !== 0) return `${prefix}: ${stat} = 0`
  return `${prefix}: ${sign}${delta} ${stat}`
}

function isFofBuff(mod: ForOneFightMod): boolean {
  const delta = mod.newValue - mod.originalValue
  return mod.isOnEnemy ? delta < 0 : delta >= 0
}

// ForOneFight mods that the ENEMY's passives applied to US
const enemyModsOnUs = computed(() => {
  if (!fight.value) return []
  // Opposite perspective's array, filtered for isOnEnemy=true
  // (from enemy's POV, "enemy" = us, so these are mods on our stats)
  const oppositeArray = isFlipped.value
    ? (fight.value.attackerForOneFightMods ?? [])
    : (fight.value.defenderForOneFightMods ?? [])
  return oppositeArray.filter(m => m.isOnEnemy)
})

function enemyFofBadgeText(mod: ForOneFightMod): string {
  const delta = mod.newValue - mod.originalValue
  const statAbbrev: Record<string, string> = {
    Strength: 'Str', Speed: 'Spd', Intelligence: 'Int',
    Psyche: 'Psy', Skill: 'Skill', Justice: 'Justice'
  }
  const stat = statAbbrev[mod.stat] ?? mod.stat
  const sign = delta >= 0 ? '+' : ''
  if (mod.newValue === 0 && mod.originalValue !== 0) return `${stat} = 0`
  return `${sign}${delta} ${stat}`
}

// Class change badge (when ForOneFight mods change our class)
const classChangeBadge = computed(() => {
  if (!fight.value) return ''
  const f = fight.value
  const origClass = isFlipped.value ? f.defenderOriginalClass : f.attackerOriginalClass
  const newClass = isFlipped.value ? f.defenderClass : f.attackerClass
  if (!origClass || origClass === newClass) return ''
  const classLabel: Record<string, string> = { 'Интеллект': 'Smart', 'Сила': 'Strong', 'Скорость': 'Fast' }
  return `${classLabel[origClass] ?? origClass} → ${classLabel[newClass] ?? newClass}`
})

// Justice: Slam animation
let slamTimers: ReturnType<typeof setTimeout>[] = []

const justiceWinner = computed(() => {
  if (ourJustice.value > enemyJustice.value) return 'left'
  if (enemyJustice.value > ourJustice.value) return 'right'
  return 'tie'
})

function clearSlamTimers() {
  slamTimers.forEach(t => clearTimeout(t))
  slamTimers = []
}

watch(showR2, (visible: boolean) => {
  clearSlamTimers()
  if (!visible) { slamPhase.value = 'idle'; return }
  if (skippedToEnd.value) { slamPhase.value = 'resolved'; return }
  slamPhase.value = 'rush'
  slamTimers.push(setTimeout(() => { slamPhase.value = 'impact' }, 200 / speed.value))
  slamTimers.push(setTimeout(() => { slamPhase.value = 'resolved' }, 450 / speed.value))
})
const phase3Result = computed(() => {
  if (!fight.value || !isMyFight.value || !fight.value.usedRandomRoll) return 0
  return r3WeWon.value ? 1 : -1
})

/** Delay phase-tracker pip reveals to sync with animations */
const phase2Revealed = computed(() => {
  if (!isMyFight.value || skippedToEnd.value) return showR2.value
  return slamPhase.value === 'resolved'
})
const phase3Revealed = computed(() => {
  if (!isMyFight.value || skippedToEnd.value) return showR3Roll.value
  return r3NeedleSettled.value
})

// ── Final result sound trigger ───────────────────────────────────────
watch(showFinalResult, (show) => {
  if (!show || skippedToEnd.value) return
  if (!fight.value || !isMyFight.value || isSpecialOutcome.value) return
  if (currentStep.value <= round1Factors.value.length + 2) return
  const isLastFight = currentFightIdx.value === myFights.value.length - 1
  const allSame = roundResults.value.length > 0 && roundResults.value.every(r => r === roundResults.value[0])
  const isAbsolute = isLastFight && allSame
  const isGeralt = myCharacterName.value === 'Геральт'
  const clips: SyncClip[] = [{ path: doomsDayWinLosePath(roundResults.value, true, isAbsolute, leftWon.value), group: 'doomsDayWinLose' }]
  if (leftWon.value) {
    const percPath = folkPercussionPool.getLayerPath()
    if (percPath) clips.push({ path: percPath, group: 'doomsDayLayers' })
    const vocalPath = getGeraltVocalWinLayerPath(roundResults.value, true, isGeralt)
    if (vocalPath) clips.push({ path: vocalPath, group: 'doomsDayLayers', gainMultiplier: 0.5 })
  }
  void playClipsBatched(clips)
})

function phaseClass(result: number, revealed: boolean): string {
  if (!revealed) return 'phase-pending'
  if (result > 0) return 'phase-ours'
  if (result < 0) return 'phase-theirs'
  return 'phase-draw'
}

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

function formatLetopis(text: string): string {
  return text
    .replace(/<:(\w+):\d+>/g, (_match, name: string) => {
      const src = discordEmojiMap[name]
      if (src === undefined) return `[${name}]`
      if (src === '') return ''
      return `<img class="lb-emoji" src="${src}" alt="${name}">`
    })
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/__(.*?)__/g, '<u>$1</u>')
    .replace(/\*(.*?)\*/g, '<em>$1</em>')
    .replace(/~~(.*?)~~/g, '<del>$1</del>')
    .replace(/\|>Stat<\|/g, '')
    .replace(/\|>Phrase<\|/g, '')
    .replace(/\n/g, '<br>')
}
/** Softcap a factor value for non-admin bar accumulation */
const FACTOR_CAP = 8
function clampFactor(v: number): number {
  return Math.sign(v) * Math.min(Math.abs(v), FACTOR_CAP)
}
/** Arrow + intensity word for non-admin factor rows; arrow count scales with tier */
function factorHint(v: number, tiers: { good: [string, string, string]; bad: [string, string, string]; even: string }, prefix?: string): { text: string; tier: number } {
  const abs = Math.abs(v)
  const tier = abs <= 3 ? 0 : abs <= 6 ? 1 : 2
  let word: string, cls: string, baseArrow: string
  if (v > 0) { word = tiers.good[tier]; cls = 'gi-ok'; baseArrow = '▲' }
  else if (v < 0) { word = tiers.bad[tier]; cls = 'gi-fail'; baseArrow = '▼' }
  else { word = tiers.even; cls = 'gi-tie'; baseArrow = '◆' }
  const arrows = baseArrow.repeat(tier + 1)
  const pre = prefix ? prefix + ' ' : ''
  return {
    text: `${pre}<span class="${cls}">${arrows} ${word}</span>`,
    tier,
  }
}
/** Round 3: Our win chance (percentage) */
const r3OurChance = computed(() => {
  const f = fight.value
  if (!f || f.maxRandomNumber === 0) return 50
  const s = sign.value
  const attackerChance = f.randomForPoint / f.maxRandomNumber * 100
  return s > 0 ? attackerChance : 100 - attackerChance
})

/** Displayed threshold — clamped for extreme values */
const r3DisplayChance = computed(() => {
  const raw = r3OurChance.value
  if (raw > 100) return 100  // Visual cap; overflow effect handles the rest
  if (raw < 0) return 1 + Math.abs(fight.value?.randomNumber ?? 0) % 4  // Deterministic 1-4%
  return raw
})

/** Whether our chance overflows (>100%) — triggers break-through effect */
const r3Overflow = computed(() => r3OurChance.value > 100)

/** Whether our chance is negative — triggers minimal-chance display */
const r3Underflow = computed(() => r3OurChance.value < 0)

/** Round 3: Pure Justice contribution to our chance (percentage points) */
const r3JusticePct = computed(() => {
  const f = fight.value
  if (!f || f.maxRandomNumber === 0 || f.justiceRandomChange === 0) return 0
  const s = sign.value
  // maxRandom with only justice applied (no nemesis effect)
  const maxRandomJusticeOnly = 100 - f.justiceRandomChange
  if (maxRandomJusticeOnly === 0) return 0
  const attackerDelta = f.randomForPoint * (100 / maxRandomJusticeOnly - 1)
  return attackerDelta * s
})

/** Round 3: NemesisMultiplier contribution to our chance (percentage points) */
const r3NemesisPct = computed(() => {
  //console.log('nemesisRandomChange', fight.value.nemesisRandomChange)
  const f = fight.value
  if (!f || f.maxRandomNumber === 0 || f.nemesisRandomChange === 0) return 0
  const s = sign.value
  // total attacker delta from both justice + nemesis
  const totalDelta = f.randomForPoint * (100 / f.maxRandomNumber - 1)
  // subtract the pure-justice part to isolate nemesis effect
  const maxRandomJusticeOnly = 100 - f.justiceRandomChange
  const justiceDelta = maxRandomJusticeOnly !== 0
    ? f.randomForPoint * (100 / maxRandomJusticeOnly - 1)
    : 0
  return (totalDelta - justiceDelta) * s
})

/** Round 3: Our roll as percentage of the range */
const r3RollPct = computed(() => {
  const f = fight.value
  if (!f || f.maxRandomNumber === 0) return 0
  const s = sign.value
  const rollPct = f.randomNumber / f.maxRandomNumber * 100
  // From our perspective: if we attack, low roll = good; if we defend, high roll = good
  // Normalize so that < ourChance% means we win
  return s > 0 ? rollPct : 100 - rollPct
})


/** Animated needle bounce animation */
let needleAnimFrame: ReturnType<typeof requestAnimationFrame> | null = null

function animateNeedleBounce(target: number) {
  if (needleAnimFrame) cancelAnimationFrame(needleAnimFrame)
  r3NeedleSettled.value = false

  const threshold = r3OurChance.value
  const weWin = target < threshold
  const distance = Math.abs(target - threshold)
  const wrongDir = weWin ? 1 : -1

  // Waypoints: bounces near threshold for tension
  const wps: number[] = [0]

  if (distance < 5) {
    const amp = 5 + Math.random() * 3
    wps.push(threshold + wrongDir * amp)
    wps.push(threshold - wrongDir * amp * 0.5)
    wps.push(threshold + wrongDir * amp * 0.2)
  } else if (distance < 15) {
    const amp = Math.min(distance + 3, 12)
    wps.push(threshold + wrongDir * amp * 0.6)
    wps.push(threshold - wrongDir * amp * 0.25)
  } else {
    const overshoot = 2 + Math.random() * 2
    wps.push(target + (weWin ? -1 : 1) * overshoot)
  }
  wps.push(target)

  for (let i = 0; i < wps.length; i++) {
    wps[i] = Math.max(0.5, Math.min(99.5, wps[i]))
  }

  const baseDuration = distance < 5 ? 700 : distance < 15 ? 650 : 500
  const duration = baseDuration / speed.value

  // Catmull-Rom spline for smooth continuous motion through waypoints
  const pts = [wps[0], ...wps, wps[wps.length - 1]]

  function catmullRom(p0: number, p1: number, p2: number, p3: number, t: number): number {
    return 0.5 * (
      (2 * p1) +
      (-p0 + p2) * t +
      (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
      (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t
    )
  }

  const segments = wps.length - 1
  const weights: number[] = []
  for (let i = 0; i < segments; i++) {
    weights.push(i === 0 ? 2.0 : 1.0 / (i + 1))
  }
  const totalWeight = weights.reduce((a, b) => a + b, 0)
  const breaks: number[] = [0]
  let acc = 0
  for (let i = 0; i < segments; i++) {
    acc += weights[i] / totalWeight
    breaks.push(acc)
  }

  const startTime = performance.now()

  function tick(now: number) {
    const raw = Math.min((now - startTime) / duration, 1)
    const t = 1 - (1 - raw) * (1 - raw)

    let segIdx = segments - 1
    for (let i = 0; i < segments; i++) {
      if (t < breaks[i + 1]) { segIdx = i; break }
    }
    const segSpan = breaks[segIdx + 1] - breaks[segIdx]
    const localT = segSpan > 0 ? (t - breaks[segIdx]) / segSpan : 1

    const pos = catmullRom(pts[segIdx], pts[segIdx + 1], pts[segIdx + 2], pts[segIdx + 3], localT)

    r3NeedlePos.value = Math.max(0, Math.min(100, pos))

    if (raw < 1) {
      needleAnimFrame = requestAnimationFrame(tick)
    } else {
      r3NeedlePos.value = target
      r3NeedleSettled.value = true
    }
  }

  needleAnimFrame = requestAnimationFrame(tick)
}

watch(showR3Roll, (show: boolean) => {
  if (show) {
    r3NeedlePos.value = 0
    r3NeedleSettled.value = false
    let target = r3RollPct.value
    if (r3Underflow.value && target <= r3DisplayChance.value) {
      target = r3DisplayChance.value + 1
    }
    nextTick(() => { setTimeout(() => animateNeedleBounce(target), 50) })
  } else {
    r3NeedlePos.value = 0
    r3NeedleSettled.value = false
    if (needleAnimFrame) { cancelAnimationFrame(needleAnimFrame); needleAnimFrame = null }
  }
})

/** Round 3: Did we win the roll? */
const r3WeWon = computed(() => {
  const f = fight.value
  if (!f) return false
  const s = sign.value
  const attackerWon = f.randomNumber <= f.randomForPoint
  return s > 0 ? attackerWon : !attackerWon
})

/** All-fights row: loser on left, winner on right */
function allFightLeft(f: FightEntry) {
  if (f.outcome === 'block' || f.outcome === 'skip') {
    return { name: f.defenderName, avatar: f.defenderAvatar, isWinner: false }
  }
  const loserIsAttacker = f.winnerName !== f.attackerName
  return {
    name: loserIsAttacker ? f.attackerName : f.defenderName,
    avatar: loserIsAttacker ? f.attackerAvatar : f.defenderAvatar,
    isWinner: false,
  }
}
function allFightRight(f: FightEntry) {
  if (f.outcome === 'block' || f.outcome === 'skip') {
    return { name: f.attackerName, avatar: f.attackerAvatar, isWinner: false }
  }
  const winnerIsAttacker = f.winnerName === f.attackerName
  return {
    name: winnerIsAttacker ? f.attackerName : f.defenderName,
    avatar: winnerIsAttacker ? f.attackerAvatar : f.defenderAvatar,
    isWinner: true,
  }
}
function allFightCenterLabel(f: FightEntry): string {
  if (f.outcome === 'block') return 'БЛОК'
  if (f.outcome === 'skip') return 'СКИП'
  if (f.drops > 0) return 'DROP'
  return '→'
}

const sortedFights = computed(() =>
  [...props.fights].sort((a, b) => {
    const aN = a.outcome === 'block' || a.outcome === 'skip'
    const bN = b.outcome === 'block' || b.outcome === 'skip'
    if (aN !== bN) return aN ? 1 : -1
    return (a.winnerName ?? '').localeCompare(b.winnerName ?? '')
  })
)

const perfectRoundPlayers = computed(() => {
  const wins = new Set<string>()
  const losses = new Set<string>()
  for (const f of props.fights) {
    if (f.outcome === 'block' || f.outcome === 'skip' || !f.winnerName) continue
    wins.add(f.winnerName)
    losses.add(f.winnerName === f.attackerName ? f.defenderName : f.attackerName)
  }
  const perfect = new Set<string>()
  for (const w of wins) { if (!losses.has(w)) perfect.add(w) }
  return perfect
})

function isMyAttack(f: FightEntry): boolean {
  return f.attackerName === myUsername.value
    && f.outcome !== 'block' && f.outcome !== 'skip'
}

// ── Avatar/name masking ─────────────────────────────────────────────
const charCatalogMap = computed(() => {
  const map: Record<string, CharacterInfo> = {}
  for (const c of props.characterCatalog) { map[c.name] = c }
  return map
})
function findPlayer(u: string): Player | null {
  return props.players.find((p: Player) => p.discordUsername === u) || null
}
function isPlayerMasked(u: string): boolean {
  if (showDetails.value) return false
  const p = findPlayer(u)
  if (!p) return false
  if (p.playerId === props.myPlayerId) return false
  return p.character.name === '???'
}
function getPredictionForPlayer(u: string): string {
  const p = findPlayer(u)
  if (!p || !props.predictions) return ''
  const pred = props.predictions.find((pr: Prediction) => pr.playerId === p.playerId)
  return pred?.characterName ?? ''
}
function getDisplayAvatar(orig: string, u: string): string {
  if (!isPlayerMasked(u)) return orig
  const predName = getPredictionForPlayer(u)
  if (predName && charCatalogMap.value[predName]) return charCatalogMap.value[predName].avatar
  return 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown_fixvalues.png'
}
function getDisplayCharName(orig: string, u: string): string {
  if (!isPlayerMasked(u)) return orig
  const predName = getPredictionForPlayer(u)
  if (predName) return predName + ' (?)'
  return '???'
}
</script>

<template>
  <div class="fight-animation">
    <!-- Tab header -->
    <div class="fa-tab-header">
      <button class="fa-tab" :class="{ active: activeTab === 'fights' }" data-sfx-fight-tab="true" @click="setTab('fights')">Бои раунда<span v-if="hasUnseenFights && activeTab !== 'fights'" class="fa-tab-dot"></span></button>
      <button class="fa-tab" :class="{ active: activeTab === 'all' }" data-sfx-fight-tab="true" @click="setTab('all')">Все бои</button>
      <button class="fa-tab" :class="{ active: activeTab === 'letopis' }" data-sfx-fight-tab="true" @click="setTab('letopis')">Летопись</button>
      <button v-if="gameStory" class="fa-tab fa-tab-story" :class="{ active: activeTab === 'story' }" data-sfx-fight-tab="true" @click="setTab('story')">История</button>
    </div>

    <!-- Летопись -->
    <div v-if="activeTab === 'letopis'" class="fa-letopis">
      <div v-if="letopis.trim()" class="fa-letopis-content" v-html="formatLetopis(letopis)" />
      <div v-else class="fa-empty">История пуста</div>
    </div>

    <!-- AI Story tab -->
    <div v-else-if="activeTab === 'story' && gameStory" class="fa-letopis">
      <div class="fa-story-content" v-html="gameStory"></div>
    </div>

    <!-- All Fights (compact results list) -->
    <div v-else-if="activeTab === 'all'" class="fa-all-fights">
      <div v-if="!fights.length" class="fa-empty">Бои еще не начались</div>
      <div v-else class="fa-all-list">
        <div v-for="(f, idx) in sortedFights" :key="idx"
          class="fa-all-row" :class="{ 'my-attack': isMyAttack(f), 'clickable': isFightMine(f) }"
          @click="jumpToFightReplay(f)">
          <!-- Left name (loser / defender for block-skip) -->
          <span class="fa-all-name fa-all-name-left" :class="{ 'name-winner': allFightLeft(f).isWinner }" :title="allFightLeft(f).name">
            <span v-if="allFightLeft(f).isWinner && perfectRoundPlayers.has(allFightLeft(f).name)" class="perfect-icon">✦</span>{{ allFightLeft(f).name }}
          </span>
          <!-- Center block: avatar | label | avatar -->
          <div class="fa-all-mid">
            <img :src="getDisplayAvatar(allFightLeft(f).avatar, allFightLeft(f).name)"
              class="fa-all-ava" :class="{ 'ava-winner': allFightLeft(f).isWinner, 'ava-perfect': allFightLeft(f).isWinner && perfectRoundPlayers.has(allFightLeft(f).name) }"
              @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
            <span class="fa-all-center" :class="{
              'center-neutral': f.outcome === 'block' || f.outcome === 'skip',
              'center-drop': f.drops > 0 && f.outcome !== 'block' && f.outcome !== 'skip',
              'center-arrow': f.outcome !== 'block' && f.outcome !== 'skip' && f.drops === 0
            }">{{ allFightCenterLabel(f) }}</span>
            <img :src="getDisplayAvatar(allFightRight(f).avatar, allFightRight(f).name)"
              class="fa-all-ava" :class="{ 'ava-winner': allFightRight(f).isWinner, 'ava-perfect': allFightRight(f).isWinner && perfectRoundPlayers.has(allFightRight(f).name) }"
              @error="(e: Event) => (e.target as HTMLImageElement).src = 'https://r2.ozvmusic.com/kotgh/art/avatars/unknown.png'">
          </div>
          <!-- Right name (winner / attacker for block-skip) -->
          <span class="fa-all-name fa-all-name-right" :class="{ 'name-winner': allFightRight(f).isWinner }" :title="allFightRight(f).name">
            {{ allFightRight(f).name }}<span v-if="allFightRight(f).isWinner && perfectRoundPlayers.has(allFightRight(f).name)" class="perfect-icon">✦</span>
          </span>
          <!-- Portal badge -->
          <span v-if="f.portalGunSwap" class="fa-portal-badge">PORTAL</span>
          <!-- Play button for own fights -->
          <span v-if="isFightMine(f)" class="fa-all-play" title="Смотреть бой">▶</span>
        </div>
      </div>
    </div>

    <!-- Fight animation (replay) -->
    <template v-else>
    <div v-if="!myFights.length" class="fa-empty">Нет ваших боев в этом раунде</div>
    <template v-else>
      <!-- Controls + fight thumbnails -->
      <div class="fa-controls">
        <button class="fa-btn" data-sfx-utility="true" @click="togglePlay">{{ isPlaying ? '⏸' : '▶' }}</button>
        <button class="fa-btn" data-sfx-utility="true" @click="restart" title="Restart all">⏮</button>
        <button class="fa-btn" data-sfx-utility="true" @click="restartCurrentFight" title="Restart current fight">🔄</button>
        <button class="fa-btn" data-sfx-utility="true" @click="skipToEnd" title="Skip to end">⏭</button>
        <div class="fa-speed">
          <button v-for="s in [1, 2, 4]" :key="s" class="fa-speed-btn" :class="{ active: speed === s }" data-sfx-utility="true" @click="setSpeed(s)">{{ s }}x</button>
        </div>
        <div class="fa-thumbs">
          <button v-for="(f, idx) in myFights" :key="idx" class="fa-thumb"
            :class="{ active: idx === currentFightIdx, 'is-block': f.outcome === 'block', 'is-skip': f.outcome === 'skip' }"
            data-sfx-utility="true"
            @click="currentFightIdx = idx; currentStep = totalSteps - 1; skippedToEnd = true; isPlaying = false; clearTimer()"
            :title="`${f.attackerName} vs ${f.defenderName}`">
            <span class="thumb-idx">{{ (idx as number) + 1 }}</span>
          </button>
        </div>
      </div>

      <!-- Fight Arena (Card Clash) -->
      <FightArena v-if="fight" ref="fightCardRef"
        :fight="fight"
        :is-my-fight="isMyFight"
        :is-flipped="isFlipped"
        :left-name="leftName"
        :left-char-name="leftCharName"
        :left-avatar="leftAvatar"
        :right-name="rightName"
        :right-char-name="rightCharName"
        :right-avatar="rightAvatar"
        :left-won="leftWon"
        :attacked-right="attackedRight"
        :sign="sign"
        :current-step="currentStep"
        :skipped-to-end="skippedToEnd"
        :show-r1-result="showR1Result"
        :show-r2="showR2"
        :show-r3="showR3"
        :show-final-result="showFinalResult"
        :show-details="showDetails"
        :phase1-result="phase1Result"
        :phase2-result="phase2Result"
        :phase3-result="phase3Result"
        :phase2-revealed="phase2Revealed"
        :phase3-revealed="phase3Revealed"
        :bar-position="barPosition"
        :animated-weighing-value="animatedWeighingValue"
        :our-justice="ourJustice"
        :enemy-justice="enemyJustice"
        :slam-phase="slamPhase"
        :justice-winner="justiceWinner"
        :r3-our-chance="r3OurChance"
        :r3-display-chance="r3DisplayChance"
        :r3-roll-pct="r3RollPct"
        :r3-we-won="r3WeWon"
        :r3-overflow="r3Overflow"
        :r3-underflow="r3Underflow"
        :r3-justice-pct="r3JusticePct"
        :r3-nemesis-pct="r3NemesisPct"
        :r3-needle-pos="r3NeedlePos"
        :r3-needle-settled="r3NeedleSettled"
        :round1-factors="round1Factors"
        :our-skill-multiplier="ourSkillMultiplier"
        :our-for-one-fight-mods="ourForOneFightMods"
        :enemy-mods-on-us="enemyModsOnUs"
        :class-change-badge="classChangeBadge"
        :our-moral-change="ourMoralChange"
        :fight-result="fightResult"
        :fight-shake="fightShake"
        :is-portal-swap="isPortalSwap"
        :get-display-avatar="getDisplayAvatar"
        :get-display-char-name="getDisplayCharName"
        :fof-badge-text="fofBadgeText"
        :is-fof-buff="isFofBuff"
        :enemy-fof-badge-text="enemyFofBadgeText"
        :phase-class="phaseClass"
      />

    </template>
    </template>

    <!-- Story popup overlay (teleported to body so it's not clipped by parent) -->
    <Teleport to="body">
      <div v-if="showStoryPopup && gameStory" class="fa-story-overlay" @click.self="dismissStoryPopup">
        <div class="fa-story-popup">
          <div class="fa-story-popup-header">
            <span class="fa-story-popup-title">История этой битвы</span>
            <button class="fa-story-popup-close" @click="dismissStoryPopup">&times;</button>
          </div>
          <div class="fa-story-popup-body" v-html="gameStory"></div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.fight-animation { display: flex; flex-direction: column; gap: 3px; padding: 3px; }
.fa-empty { color: var(--text-muted); font-style: italic; padding: 12px; text-align: center; font-size: 13px; }

/* ── Controls ── */
.fa-controls { display: flex; align-items: center; gap: 4px; padding: 4px 0; }
.fa-btn { background: var(--bg-surface); border: 1px solid var(--border-subtle); border-radius: var(--radius); padding: 3px 10px; font-size: 13px; cursor: pointer; color: var(--text-primary); transition: all 0.15s; }
.fa-btn:hover { background: var(--accent-blue); color: white; border-color: var(--accent-blue); }
.fa-speed { display: flex; gap: 2px; margin-left: 6px; }
.fa-speed-btn { background: var(--bg-surface); border: 1px solid var(--border-subtle); border-radius: 4px; padding: 2px 8px; font-size: 10px; font-weight: 700; cursor: pointer; color: var(--text-muted); transition: all 0.15s; }
.fa-speed-btn.active { background: var(--kh-c-secondary-purple-500); color: var(--text-primary); border-color: var(--accent-purple); }

/* ── Thumbnails ── */
.fa-thumbs { display: flex; gap: 2px; flex-wrap: wrap; margin-left: auto; }
.fa-thumb { width: 24px; height: 24px; border-radius: var(--radius); border: 1px solid var(--border-subtle); background: var(--bg-secondary); cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 9px; font-weight: 800; color: var(--text-muted); transition: all 0.15s; font-family: var(--font-mono); }
.fa-thumb:hover { border-color: var(--accent-blue); color: var(--text-primary); }
.fa-thumb.active { background: var(--kh-c-secondary-info-500); color: var(--text-primary); border-color: var(--accent-blue); }
.fa-thumb.is-block { border-color: rgba(230, 148, 74, 0.4); }
.fa-thumb.is-skip { border-color: rgba(239, 128, 128, 0.3); opacity: 0.5; }

/* ── Tabs ── */
.fa-tab-header { display: flex; gap: 2px; background: var(--bg-secondary); border-radius: var(--radius); padding: 2px; }
.fa-tab { flex: 1; padding: 4px 10px; border: none; border-radius: 4px; background: transparent; color: var(--text-muted); font-size: 11px; font-weight: 700; cursor: pointer; transition: all 0.15s; }
.fa-tab:hover { color: var(--text-primary); }
.fa-tab.active { background: var(--bg-card); color: var(--accent-gold); }
.fa-tab-dot { display: inline-block; width: 6px; height: 6px; border-radius: 50%; background: var(--accent-orange); margin-left: 4px; vertical-align: middle; animation: tab-dot-pulse 1.5s ease-in-out infinite; }
@keyframes tab-dot-pulse { 0%, 100% { opacity: 1; transform: scale(1); } 50% { opacity: 0.5; transform: scale(0.7); } }

/* ── All Fights list ── */
.fa-all-fights { flex: 1; overflow-y: auto; }
.fa-all-list { display: flex; flex-direction: column; gap: 3px; max-width: 480px; margin: 0 auto; }
.fa-all-row { display: grid; grid-template-columns: 1fr auto 1fr auto; align-items: center; gap: 4px; padding: 5px 8px; border-radius: var(--radius); background: var(--bg-inset); font-size: 12px; border: 1px solid transparent; transition: all 0.15s; }
.fa-all-row.my-attack { border-left: 2px solid var(--accent-gold-dim); }
.fa-all-row.clickable { cursor: pointer; }
.fa-all-row.clickable:hover { background: rgba(180, 150, 255, 0.12); border-color: rgba(180, 150, 255, 0.3); }
.fa-all-play { font-size: 10px; color: var(--text-dim); flex-shrink: 0; opacity: 0.4; transition: opacity 0.15s; }
.fa-all-row.clickable:hover .fa-all-play { opacity: 1; color: var(--accent-gold); }

/* Portal badge (All Fights compact list) */
.fa-portal-badge {
  font-size: 8px; font-weight: 900; color: #00ff88;
  background: rgba(0, 255, 136, 0.1);
  border: 1px solid rgba(0, 255, 136, 0.3);
  padding: 1px 4px; border-radius: 3px;
}
.fa-all-mid { display: flex; align-items: center; gap: 6px; justify-content: center; flex-shrink: 0; }
.fa-all-ava { width: 28px; height: 28px; border-radius: 50%; object-fit: cover; border: 2px solid var(--border-subtle); transition: all 0.3s; flex-shrink: 0; }
.fa-all-name { font-weight: 700; color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: 11px; min-width: 0; }
.fa-all-name-left { text-align: right; }
.fa-all-name-right { text-align: left; }
.fa-all-center { font-size: 10px; font-weight: 800; text-align: center; flex-shrink: 0; padding: 1px 0; border-radius: 3px; white-space: nowrap; width: 42px; display: inline-block; }
.fa-all-center.center-arrow { color: var(--text-dim); opacity: 0.5; }
.fa-all-center.center-neutral { color: var(--accent-orange); background: rgba(230, 148, 74, 0.1); border: 1px solid rgba(230, 148, 74, 0.2); }
.fa-all-center.center-drop { color: var(--accent-red); background: rgba(239, 128, 128, 0.1); border: 1px solid rgba(239, 128, 128, 0.2); }
.fa-all-name.name-winner { color: var(--accent-green); }
.fa-all-ava.ava-winner { border-color: var(--accent-green); }
.fa-all-ava.ava-perfect { box-shadow: 0 0 6px rgba(63, 167, 61, 0.4); }
.perfect-icon { color: var(--accent-green); font-size: 9px; margin-left: 2px; text-shadow: 0 0 4px rgba(63, 167, 61, 0.5); }

/* ── Letopis ── */
.fa-letopis { flex: 1; overflow-y: auto; padding: 4px; background: var(--bg-inset); border-radius: var(--radius); border: 1px solid var(--border-subtle); }
.fa-letopis-content { font-size: 11px; line-height: 1.6; color: var(--text-secondary); font-family: var(--font-mono); }
.fa-letopis-content :deep(strong) { color: var(--accent-gold); }
.fa-letopis-content :deep(em) { color: var(--accent-blue); }
.fa-letopis-content :deep(u) { color: var(--accent-green); }
.fa-letopis-content :deep(del) { color: var(--text-muted); text-decoration: line-through; }
.fa-letopis-content :deep(.lb-emoji) { width: 20px; height: 20px; vertical-align: middle; display: inline; margin: 0 2px; }

/* ── AI Story tab ── */
.fa-tab-story { color: var(--accent-gold) !important; }
.fa-story-content { font-size: 12px; line-height: 1.7; color: var(--text-secondary); padding: 8px; white-space: pre-line; }
.fa-story-content :deep(strong) { color: var(--accent-gold); font-weight: 800; }
.fa-story-content :deep(em) { color: var(--accent-blue); }
</style>

<!-- Unscoped styles for Teleported story popup -->
<style>
.fa-story-overlay {
  position: fixed; inset: 0; z-index: 200;
  background: rgba(0, 0, 0, 0.6);
  display: flex; align-items: center; justify-content: center;
  animation: story-overlay-in 0.3s ease-out;
}
@keyframes story-overlay-in { from { opacity: 0; } to { opacity: 1; } }

.fa-story-popup {
  background: var(--bg-card);
  border: 1px solid var(--accent-gold);
  border-radius: 8px;
  max-width: 560px;
  width: 90%;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 0 40px rgba(233, 219, 61, 0.15), var(--shadow-lg);
  animation: story-popup-in 0.4s ease-out;
}
@keyframes story-popup-in {
  from { opacity: 0; transform: translateY(20px) scale(0.95); }
  to { opacity: 1; transform: translateY(0) scale(1); }
}

.fa-story-popup-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-subtle);
}
.fa-story-popup-title {
  font-size: 14px; font-weight: 800; color: var(--accent-gold);
  text-transform: uppercase; letter-spacing: 0.5px;
}
.fa-story-popup-close {
  background: none; border: none; color: var(--text-muted);
  font-size: 22px; cursor: pointer; padding: 0 4px; line-height: 1;
  transition: color 0.15s;
}
.fa-story-popup-close:hover { color: var(--text-primary); }

.fa-story-popup-body {
  padding: 16px;
  font-size: 13px; line-height: 1.7;
  color: var(--text-secondary);
  white-space: pre-line;
}
.fa-story-popup-body strong { color: var(--accent-gold); font-weight: 800; }
.fa-story-popup-body em { color: var(--accent-blue); }
</style>
