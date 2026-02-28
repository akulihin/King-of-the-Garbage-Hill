<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import type { FightEntry, Player, Prediction, CharacterInfo } from 'src/services/signalr'
import {
  FightSoundPool,
  playDoomsDayFight,
  playDoomsDayWinLose,
  playDoomsDayDraw,
  playDoomsDayRndRoll,
  playDoomsDayScroll,
  playDoomsDayNoFights,
  playWinSpecialsForAll,
} from 'src/services/sound'

const props = withDefaults(defineProps<{
  fights: FightEntry[]
  letopis?: string
  gameStory?: string | null
  players?: Player[]
  myPlayerId?: string
  predictions?: Prediction[]
  isAdmin?: boolean
  characterCatalog?: CharacterInfo[]
}>(), {
  letopis: '',
  gameStory: null,
  players: () => [],
  myPlayerId: '',
  predictions: () => [],
  isAdmin: false,
  characterCatalog: () => [],
})

const emit = defineEmits<{
  (e: 'resist-flash', stats: string[]): void
  (e: 'justice-reset'): void
  (e: 'justice-up'): void
  (e: 'replay-ended'): void
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

// ── Needle & slam state (declared early for use in computed/watchers below) ──
const r3NeedlePos = ref(0)
const r3NeedleSettled = ref(false)
const slamPhase = ref<'idle' | 'rush' | 'impact' | 'resolved'>('idle')

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
    const vTier = Math.abs(v) <= 3 ? 0 : Math.abs(v) <= 6 ? 1 : 2
    list.push({
      label: 'Versatility',
      detail,
      value: v,
      highlight: hl(v),
      showValue: props.isAdmin,
      tier: props.isAdmin ? undefined : vTier,
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
      showValue: props.isAdmin,
      tier: props.isAdmin ? undefined : nTier,
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
      detail: props.isAdmin
        ? `${scaleHint.text} <span class="admin-extra">(${ourScale.toFixed(1)} vs ${theirScale.toFixed(1)})</span>`
        : scaleHint.text,
      value: v,
      showValue: props.isAdmin,
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
      showValue: props.isAdmin,
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
      showValue: props.isAdmin,
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
      showValue: props.isAdmin,
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
  if (fight.value?.usedRandomRoll) steps += 2 // R3 modifiers + roll animation
  return steps
})

// ── Weighing machine bar animation ──────────────────────────────────
// Switch: false = show real (unclamped) bar for all players, true = clamp factors for non-admins
const normalizeBar = false

// Target value (jumps per step)
const targetWeighingValue = computed(() => {
  if (!fight.value || isSpecialOutcome.value) return 0
  const factors = round1Factors.value
  const useRaw = !normalizeBar || props.isAdmin
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
watch(currentFightIdx, () => { barRandomNudge.value = Math.random() * 15 })

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
const showR1Factor = (idx: number) => {
  if (skippedToEnd.value || !isMyFight.value) return true
  return currentStep.value > idx
}

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

/** Show the animated roll bar (one step after R3 modifiers appear) */
const showR3Roll = computed(() => {
  if (!fight.value?.usedRandomRoll) return false
  if (skippedToEnd.value || !isMyFight.value) return true
  return currentStep.value > round1Factors.value.length + 3
})

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
    roundResults.value = []
    restart()
  }
}, { deep: true })

onUnmounted(() => {
  clearTimer()
  if (weighingAnimFrame) cancelAnimationFrame(weighingAnimFrame)
  if (needleAnimFrame) cancelAnimationFrame(needleAnimFrame)
  clearSlamTimers()
})

// ── Dooms Day sound system ───────────────────────────────────────────

const fightSoundPool = new FightSoundPool()
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
      playDoomsDayWinLose([r1result], false, false)
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
      playDoomsDayWinLose(roundResults.value, false, false)
    }
    return
  }

  if (hasR3) {
    // Step factorCount+3: R3 modifiers
    if (step === factorCount + 3) {
      playDoomsDayRndRoll()
      return
    }

    // Step factorCount+4: R3 roll bar + result sound
    if (step === factorCount + 4) {
      const s = sign.value
      const attackerWon = f.randomNumber <= f.randomForPoint
      const weWonR3 = s > 0 ? attackerWon : !attackerWon
      const r3result: 'w' | 'l' = weWonR3 ? 'w' : 'l'
      roundResults.value = [...roundResults.value, r3result]
      playDoomsDayWinLose(roundResults.value, false, false)
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

// ── Helpers ─────────────────────────────────────────────────────────
function outcomeLabel(f: FightEntry): string {
  switch (f.outcome) {
    case 'block': return 'БЛОК'
    case 'skip': return 'СКИП'
    case 'win': case 'loss': return `${f.winnerName} ПОБЕДИЛ`
    default: return ''
  }
}
function outcomeClass(f: FightEntry): string {
  if (f.outcome === 'block' || f.outcome === 'skip') return 'outcome-neutral'
  return f.outcome === 'win' ? 'outcome-attacker' : 'outcome-defender'
}
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
// Switch between numbers and blocks for justice slam display
// Set to true to show block pyramids, false to show raw numbers
const justiceUseBlocks = false

// Justice block layout: { front, back, top } — pyramid at justice 10 (secret easter egg)
function justiceBlockLayout(j: number): { front: number; back: number } {
  if (j >= 10) return { front: 3, back: 2, top: 1 }
  return { front: j <= 2 ? 1 : j <= 4 ? 2 : 3, back: 0, top: 0 }
}
const ourJusticeLayout = computed(() => justiceBlockLayout(ourJustice.value))
const enemyJusticeLayout = computed(() => justiceBlockLayout(enemyJustice.value))

// Our skill multiplier (for badge row)
const ourSkillMultiplier = computed(() => {
  if (!fight.value) return 1
  return isFlipped.value ? fight.value.skillMultiplierTarget : fight.value.skillMultiplierMe
})

// Justice: Number Slam animation
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
  // Numbers fly in from sides (CSS animation, 0-400ms)
  slamPhase.value = 'rush'
  // Impact shake at 400ms
  slamTimers.push(setTimeout(() => {
    slamPhase.value = 'impact'
  }, 400))
  // Resolved: winner scales up, loser shrinks at 900ms
  slamTimers.push(setTimeout(() => { slamPhase.value = 'resolved' }, 900))
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
  playDoomsDayWinLose(roundResults.value, true, isAbsolute, leftWon.value)
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
function fmtVal(v: number): string {
  return (v > 0 ? '+' : '') + v.toFixed(1)
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
/** Format a percentage delta with sign and 2 decimal places */
function fmtPct(v: number): string {
  return (v > 0 ? '+' : '') + v.toFixed(2) + '%'
}
/** CSS class for a R3 modifier value */
function r3ModClass(v: number): string {
  return v > 0 ? 'pct-good' : v < 0 ? 'pct-bad' : ''
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
  // Pad with phantom control points for start/end tangents
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
  // Ease-out time distribution: first segment gets more time (big sweep), later ones decelerate
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
    // Ease-out curve for overall deceleration
    const t = 1 - (1 - raw) * (1 - raw)

    let segIdx = segments - 1
    for (let i = 0; i < segments; i++) {
      if (t < breaks[i + 1]) { segIdx = i; break }
    }
    const segSpan = breaks[segIdx + 1] - breaks[segIdx]
    const localT = segSpan > 0 ? (t - breaks[segIdx]) / segSpan : 1

    // pts is offset by 1 due to phantom point at start
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
    // When chance < 0%, we hardcoded a fake threshold at 1-4%.
    // Ensure needle doesn't land inside the tiny fake zone.
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
  if (props.isAdmin) return false
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
  return '/art/avatars/guess.png'
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
              @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            <span class="fa-all-center" :class="{
              'center-neutral': f.outcome === 'block' || f.outcome === 'skip',
              'center-drop': f.drops > 0 && f.outcome !== 'block' && f.outcome !== 'skip',
              'center-arrow': f.outcome !== 'block' && f.outcome !== 'skip' && f.drops === 0
            }">{{ allFightCenterLabel(f) }}</span>
            <img :src="getDisplayAvatar(allFightRight(f).avatar, allFightRight(f).name)"
              class="fa-all-ava" :class="{ 'ava-winner': allFightRight(f).isWinner, 'ava-perfect': allFightRight(f).isWinner && perfectRoundPlayers.has(allFightRight(f).name) }"
              @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
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
        <button class="fa-btn" data-sfx-utility="true" @click="restart" title="Restart">⏮</button>
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

      <!-- Fight card -->
      <div v-if="fight" ref="fightCardRef" class="fa-card" :class="{ 'fa-shake': fightShake, 'fa-result-win': fightResult === 'win', 'fa-result-loss': fightResult === 'loss' }">
        <!-- Block/Skip -->
        <div v-if="isSpecialOutcome" class="fa-special">
          <div class="fa-bar-container">
            <div class="fa-id-left">
              <img :src="getDisplayAvatar(leftAvatar, leftName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
              <div class="fa-id-info">
                <span class="fa-id-name">{{ leftName }}</span>
              </div>
            </div>
            <div class="phase-tracker phase-tracker-inline">
              <div class="phase-role-pip" :class="attackedRight ? 'role-atk' : 'role-def'">
                <span class="phase-role-text">{{ attackedRight ? 'ATK' : 'DEF' }}</span>
              </div>
              <div class="phase-connector revealed"></div>
              <div class="phase-outcome-pip" :class="outcomeClass(fight)">
                <span class="phase-outcome-text">{{ outcomeLabel(fight) }}</span>
              </div>
            </div>
            <div class="fa-id-right">
              <div class="fa-id-info" style="text-align:right">
                <span class="fa-id-name">{{ rightName }}</span>
              </div>
              <img :src="getDisplayAvatar(rightAvatar, rightName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            </div>
          </div>
        </div>

        <!-- Normal fight -->
        <template v-else>
          <!-- Compact scale row: avatar+name | bar | avatar+name -->
          <div class="fa-bar-container">
            <div class="fa-id-left" :class="{ winner: leftWon, 'entrance-active': currentStep === 0 && !skippedToEnd }">
              <img :src="getDisplayAvatar(leftAvatar, leftName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
              <div class="fa-id-info">
                <span class="fa-id-name">{{ leftName }}</span>
                <span class="fa-id-char">{{ getDisplayCharName(leftCharName, leftName) }}</span>
              </div>
            </div>
            <!-- Phase tracker between avatars -->
            <div v-if="isMyFight" class="phase-tracker phase-tracker-inline">
              <div class="phase-role-pip" :class="attackedRight ? 'role-atk' : 'role-def'">
                <span class="phase-role-text">{{ attackedRight ? 'ATK' : 'DEF' }}</span>
              </div>
              <div class="phase-connector revealed"></div>
              <div class="phase-pip" :class="phaseClass(phase1Result, showR1Result)">
                <span v-if="!showR1Result" class="phase-icon phase-icon-pending">?</span>
                <span v-else-if="phase1Result > 0" class="phase-icon phase-icon-win">✓</span>
                <span v-else-if="phase1Result < 0" class="phase-icon phase-icon-lose">✗</span>
                <span v-else class="phase-icon phase-icon-draw">—</span>
              </div>
              <div class="phase-connector" :class="{ revealed: showR2 }"></div>
              <div class="phase-pip" :class="phaseClass(phase2Result, phase2Revealed)">
                <span v-if="!phase2Revealed" class="phase-icon phase-icon-pending">?</span>
                <span v-else-if="phase2Result > 0" class="phase-icon phase-icon-win">✓</span>
                <span v-else-if="phase2Result < 0" class="phase-icon phase-icon-lose">✗</span>
                <span v-else class="phase-icon phase-icon-draw">—</span>
              </div>
              <div class="phase-connector" :class="{ revealed: fight?.usedRandomRoll && showR3, broken: !fight?.usedRandomRoll }"></div>
              <div class="phase-pip" :class="[phaseClass(phase3Result, fight?.usedRandomRoll ? phase3Revealed : false), { 'phase-skipped': !fight?.usedRandomRoll }]">
                <template v-if="!fight?.usedRandomRoll">
                  <span class="phase-icon phase-icon-skip">—</span>
                </template>
                <template v-else>
                  <span v-if="!phase3Revealed" class="phase-icon phase-icon-pending">?</span>
                  <span v-else-if="phase3Result > 0" class="phase-icon phase-icon-win">✓</span>
                  <span v-else class="phase-icon phase-icon-lose">✗</span>
                </template>
              </div>
              <div class="phase-connector phase-connector-outcome" :class="{ revealed: showFinalResult }"></div>
              <div v-if="showFinalResult" class="phase-outcome-pip" :class="leftWon ? 'phase-ours' : 'phase-theirs'">
                <span class="phase-outcome-text">{{ leftWon ? 'Victory' : 'Defeat' }}</span>
              </div>
              <div v-else class="phase-outcome-pip phase-outcome-pending">
                <span class="phase-outcome-text">?</span>
              </div>
            </div>
            <!-- Fallback bar for enemy fights -->
            <div v-else class="fa-bar-track">
              <div class="fa-bar-fill" :style="{ width: barPosition + '%' }" :class="{ 'bar-attacker': animatedWeighingValue > 0, 'bar-defender': animatedWeighingValue < 0, 'bar-even': animatedWeighingValue === 0 }">
              </div>
            </div>
            <div class="fa-id-right" :class="{ winner: !leftWon && (fight.outcome === 'win' || fight.outcome === 'loss'), 'entrance-active': currentStep === 0 && !skippedToEnd }">
              <div class="fa-id-info" style="text-align:right">
                <span class="fa-id-name">{{ rightName }}</span>
                <span class="fa-id-char">{{ getDisplayCharName(rightCharName, rightName) }}</span>
              </div>
              <img :src="getDisplayAvatar(rightAvatar, rightName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            </div>
          </div>

          <!-- ═══ MY FIGHT: Full 3-round detail ═══ -->
          <template v-if="isMyFight">
            <!-- Weighing bar: visible from the start, animates as factors appear -->
            <div class="fa-bar-container fa-bar-compact">
              <div class="fa-bar-track">
                <div class="fa-bar-fill" :style="{ width: barPosition + '%' }" :class="{ 'bar-attacker': animatedWeighingValue > 0, 'bar-defender': animatedWeighingValue < 0, 'bar-even': animatedWeighingValue === 0, 'bar-particles-gold': barPosition > 50, 'bar-particles-red': barPosition < 50 }">
                </div>
              </div>
            </div>
            <div class="fa-factors">
              <div v-for="(fac, idx) in round1Factors" :key="'r1-'+idx"
                class="fa-factor" :class="[fac.highlight, { visible: showR1Factor(idx) }, fac.tier != null ? 'tier-' + fac.tier : '']">
                <span class="fa-factor-label">{{ fac.label }}</span>
                <span class="fa-factor-detail" v-html="fac.detail"></span>
                <span class="fa-factor-value" v-if="fac.value !== 0 && fac.showValue !== false">{{ fmtVal(fac.value) }}</span>
              </div>

              <!-- TooGood / TooStronk / Skill Multiplier badges -->
              <div v-if="(fight.isTooGoodMe || fight.isTooGoodEnemy) || (fight.isTooStronkMe || fight.isTooStronkEnemy) || ourSkillMultiplier > 1" class="fa-badge-row" :class="{ visible: showR1Result }">
                <span v-if="fight.isTooGoodMe || fight.isTooGoodEnemy" class="fa-badge badge-toogood">TOO GOOD: {{ (fight.isTooGoodMe ? !isFlipped : isFlipped) ? 'МЫ' : 'ВРАГ' }}</span>
                <span v-if="fight.isTooStronkMe || fight.isTooStronkEnemy" class="fa-badge badge-toostronk">TOO STRONK: {{ (fight.isTooStronkMe ? !isFlipped : isFlipped) ? 'МЫ' : 'ВРАГ' }}</span>
                <span v-if="ourSkillMultiplier > 1" class="fa-badge badge-skill">SKILL MULTIPLIER x{{ ourSkillMultiplier }}</span>
              </div>
            </div>

            <div v-if="showR2" class="fj-slam-wrap" :class="{ 'fj-slam-impact': slamPhase === 'impact' }">
              <template v-if="justiceUseBlocks">
                <!-- Block pyramid mode -->
                <div class="fj-slam-tower fj-slam-left" :class="{
                  winner: slamPhase === 'resolved' && ourJustice > enemyJustice,
                  loser: slamPhase === 'resolved' && ourJustice < enemyJustice,
                  tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
                }">
                  <div v-if="ourJusticeLayout.top" class="fj-slam-row fj-slam-top">
                    <div v-for="b in ourJusticeLayout.top" :key="'ot'+b" class="fj-block fj-block-ours"></div>
                  </div>
                  <div v-if="ourJusticeLayout.back" class="fj-slam-row fj-slam-back">
                    <div v-for="b in ourJusticeLayout.back" :key="'ob'+b" class="fj-block fj-block-ours"></div>
                  </div>
                  <div class="fj-slam-row">
                    <div v-for="b in ourJusticeLayout.front" :key="'of'+b" class="fj-block fj-block-ours"></div>
                  </div>
                </div>
                <div class="fj-slam-vs" :class="{ visible: slamPhase === 'impact' || slamPhase === 'resolved' }">
                  <span v-if="slamPhase === 'impact'" class="fj-slam-spark">⚖</span>
                  <span v-else>vs</span>
                </div>
                <div class="fj-slam-tower fj-slam-right" :class="{
                  winner: slamPhase === 'resolved' && enemyJustice > ourJustice,
                  loser: slamPhase === 'resolved' && enemyJustice < ourJustice,
                  tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
                }">
                  <div v-if="enemyJusticeLayout.top" class="fj-slam-row fj-slam-top">
                    <div v-for="b in enemyJusticeLayout.top" :key="'et'+b" class="fj-block fj-block-enemy"></div>
                  </div>
                  <div v-if="enemyJusticeLayout.back" class="fj-slam-row fj-slam-back">
                    <div v-for="b in enemyJusticeLayout.back" :key="'eb'+b" class="fj-block fj-block-enemy"></div>
                  </div>
                  <div class="fj-slam-row">
                    <div v-for="b in enemyJusticeLayout.front" :key="'ef'+b" class="fj-block fj-block-enemy"></div>
                  </div>
                </div>
              </template>
              <template v-else>
                <!-- Number mode -->
                <div class="fj-slam-num fj-slam-left" :class="{
                  winner: slamPhase === 'resolved' && ourJustice > enemyJustice,
                  loser: slamPhase === 'resolved' && ourJustice < enemyJustice,
                  tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
                  'justice-winner': slamPhase === 'resolved' && justiceWinner === 'left',
                  'justice-loser': slamPhase === 'resolved' && justiceWinner === 'right',
                  'justice-tied-pulse': slamPhase === 'resolved' && justiceWinner === 'tie',
                }">{{ ourJustice }}</div>
                <div class="fj-slam-vs" :class="{ visible: slamPhase === 'impact' || slamPhase === 'resolved' }">
                  <span v-if="slamPhase === 'impact'" class="fj-slam-spark">⚖</span>
                  <span v-else>vs</span>
                </div>
                <div class="fj-slam-num fj-slam-right" :class="{
                  winner: slamPhase === 'resolved' && enemyJustice > ourJustice,
                  loser: slamPhase === 'resolved' && enemyJustice < ourJustice,
                  tied: slamPhase === 'resolved' && ourJustice === enemyJustice,
                  'justice-winner': slamPhase === 'resolved' && justiceWinner === 'right',
                  'justice-loser': slamPhase === 'resolved' && justiceWinner === 'left',
                  'justice-tied-pulse': slamPhase === 'resolved' && justiceWinner === 'tie',
                }">{{ enemyJustice }}</div>
                <div v-if="slamPhase === 'impact' || slamPhase === 'resolved'" class="impact-cracks">
                  <div class="crack crack-1"></div>
                  <div class="crack crack-2"></div>
                  <div class="crack crack-3"></div>
                  <div class="crack crack-4"></div>
                </div>
              </template>
            </div>

            <template v-if="fight.usedRandomRoll">
              <div v-if="showR3" class="fa-r3-details">
                <!-- Our base win chance 
                <div class="fa-factor random visible">
                  <span class="fa-factor-label">Базовый шанс</span>
                  <span class="fa-factor-detail">{{ sign > 0 ? '50.00' : '50.00' }}%</span>
                </div>
                -->
                <!-- Modifier: TooGood -->
                <div v-if="fight.tooGoodRandomChange !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">TooGood</span>
                  <span class="fa-factor-detail" :class="r3ModClass(fight.tooGoodRandomChange * sign)">
                    {{ fmtPct(fight.tooGoodRandomChange * sign) }}
                  </span>
                </div>
                <!-- Modifier: TooStronk -->
                <div v-if="fight.tooStronkRandomChange !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">TooStronk</span>
                  <span class="fa-factor-detail" :class="r3ModClass(fight.tooStronkRandomChange * sign)">
                    {{ fmtPct(fight.tooStronkRandomChange * sign) }}
                  </span>
                </div>
                <!-- Modifier: Justice -->
                <div v-if="fight.justiceRandomChange !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">Justice</span>
                  <span class="fa-factor-detail" :class="r3ModClass(r3JusticePct)">
                    {{ fmtPct(r3JusticePct) }}
                  </span>
                </div>
                <!-- Modifier: NemesisMultiplier -->
                <div v-if="fight.nemesisRandomChange !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">Nemesis</span>
                  <span class="fa-factor-detail" :class="r3ModClass(r3NemesisPct)">
                    {{ fmtPct(r3NemesisPct) }}
                  </span>
                </div>

                <!-- Modifier: Skill Difference -->
                <div v-if="fight.skillDifferenceRandomModifier !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">Skill Difference</span>
                  <span class="fa-factor-detail" :class="r3ModClass(fight.skillDifferenceRandomModifier * sign)">
                    {{ fmtPct(fight.skillDifferenceRandomModifier * sign) }}
                  </span>
                </div>
                <!-- Our final win chance -->
                <!-- <div class="fa-factor random visible">
                  <span class="fa-factor-label">Шанс победы</span>
                  <span class="fa-factor-detail fa-chance-total" :class="r3OurChance >= 50 ? 'pct-good' : 'pct-bad'">
                    {{ r3OurChance.toFixed(2) }}%
                  </span>
                </div>  -->
                <!-- Roll result: animated bar -->
                <div v-if="showR3Roll" class="fa-roll-bar-wrap" :class="{ 'roll-overflow': r3Overflow }">
                  <div class="fa-roll-bar-track" :class="{ 'track-overflow': r3Overflow }">
                    <!-- Threshold marker at our win chance -->
                    <div class="fa-roll-threshold" :style="{ left: r3DisplayChance + '%' }">
                      <span class="fa-roll-threshold-label">{{ r3Overflow ? '>100' : r3DisplayChance.toFixed(0) }}%</span>
                    </div>
                    <!-- Win zone (0 to threshold) -->
                    <div class="fa-roll-zone-win"
                         :style="{ width: r3DisplayChance + '%' }"
                         :class="{ 'zone-overflow': r3Overflow }"></div>
                    <!-- Roll needle animates in -->
                    <div class="fa-roll-needle" :style="{ left: r3NeedlePos + '%' }" :class="[r3WeWon ? 'needle-win' : 'needle-lose', { 'needle-settled': r3NeedleSettled }]">
                      <span class="fa-roll-needle-val">{{ r3RollPct.toFixed(1) }}%</span>
                    </div>
                  </div>
                </div>
              </div>
            <!--<div v-if="showR3Roll" class="fa-phase-result" :class="phaseClass(phase3Result, true)">
              <span class="phase-result-icon">{{ phase3Result > 0 ? '✓' : '✗' }}</span>
              <span>{{ phase3Result > 0 ? 'Удача на нашей стороне' : 'Удача на стороне врага' }}</span>
            </div>-->
            </template>
          </template>

          <!-- ═══ ENEMY FIGHT: Minimal view ═══ -->
          <template v-else>
            <div v-if="showFinalResult" class="fa-enemy-summary">
              <span v-if="fight.isTooGoodMe || fight.isTooGoodEnemy" class="fa-badge badge-toogood">TOO GOOD</span>
              <span v-if="fight.isTooStronkMe || fight.isTooStronkEnemy" class="fa-badge badge-toostronk">TOO STRONK</span>
            </div>
          </template>

          <!-- ═══ Result badge (corner icon) ═══ -->
          <Transition name="result-badge">
            <span v-if="fightResult === 'win'" class="fa-result-badge fa-result-badge-win">✓</span>
            <span v-else-if="fightResult === 'loss'" class="fa-result-badge fa-result-badge-loss">✗</span>
          </Transition>

          <!-- ═══ Final result details (outcome shown in phase tracker above) ═══ -->
          <div v-if="showFinalResult" class="fa-result">
            <div v-if="isMyFight" class="fa-result-details">
              <!-- Skill gained: only show when WE are the attacker -->
              <span v-if="!isFlipped && fight.skillGainedFromTarget > 0" class="fa-detail-item fa-skill-gain">
                +{{ fight.skillGainedFromTarget }} Скилл (Мишень)
              </span>
              <span v-if="!isFlipped && fight.skillGainedFromClassAttacker > 0" class="fa-detail-item fa-skill-gain">
                +{{ fight.skillGainedFromClassAttacker }} Скилл (Класс)
              </span>
              <span v-if="isFlipped && fight.skillGainedFromClassDefender > 0" class="fa-detail-item fa-skill-gain">
                +{{ fight.skillGainedFromClassDefender }} Скилл (Класс)
              </span>
              <!-- Moral: show OUR actual moral change (win or loss) -->
              <span v-if="ourMoralChange !== 0" class="fa-detail-item fa-moral">
                {{ ourMoralChange > 0 ? '+' : '' }}{{ ourMoralChange }} Мораль
              </span>
              <!-- Justice: show when WE lost (loser gains justice) -->
              <span v-if="!leftWon && fight.justiceChange > 0" class="fa-detail-item fa-justice">
                +{{ fight.justiceChange }} Справедливость
              </span>
              <!-- Resist damage that happened to US (loser) -->
              <template v-if="!leftWon && fight.qualityDamageApplied">
                <span v-if="fight.resistIntelDamage > 0" class="fa-detail-item fa-resist"><span class="gi gi-int">INT</span> <span class="gi gi-def">DEF</span> -{{ fight.resistIntelDamage }}</span>
                <span v-if="fight.resistStrDamage > 0" class="fa-detail-item fa-resist"><span class="gi gi-str">STR</span> <span class="gi gi-def">DEF</span> -{{ fight.resistStrDamage }}</span>
                <span v-if="fight.resistPsycheDamage > 0" class="fa-detail-item fa-resist"><span class="gi gi-psy">PSY</span> <span class="gi gi-def">DEF</span> -{{ fight.resistPsycheDamage }}</span>
              </template>
              <!-- Intellectual / Emotional damage: show for BOTH sides with different colors -->
              <span v-if="!leftWon && fight.intellectualDamage" class="fa-detail-item fa-dmg-us">
                <span class="gi gi-int">INT</span> Intellectual Damage!
              </span>
              <span v-if="leftWon && fight.intellectualDamage" class="fa-detail-item fa-dmg-enemy">
                <span class="gi gi-int">INT</span> {{ rightName }}: Intellectual Damage!
              </span>
              <span v-if="!leftWon && fight.emotionalDamage" class="fa-detail-item fa-dmg-us">
                <span class="gi gi-psy">PSY</span> Emotional Damage!
              </span>
              <span v-if="leftWon && fight.emotionalDamage" class="fa-detail-item fa-dmg-enemy">
                <span class="gi gi-psy">PSY</span> {{ rightName }}: Emotional Damage!
              </span>
              <!-- Drop (us losing) -->
              <span v-if="!leftWon && fight.drops > 0" class="fa-detail-item fa-drop">
                 DROP x{{ fight.drops }}!
              </span>
              <!-- Drop (enemy losing) -->
              <span v-if="leftWon && fight.drops > 0" class="fa-detail-item fa-drop-enemy">
                {{ rightName }}: DROP x{{ fight.drops }}!
              </span>
            </div>
            <!-- Drops visible to ALL players -->
            <div v-else-if="fight.drops > 0" class="fa-result-details">
              <span class="fa-detail-item fa-drop">
                DROP {{ fight.droppedPlayerName }}! (-{{ fight.drops }})
              </span>
            </div>
          </div>
          <!-- Portal Gun swap indicator -->
          <div v-if="showFinalResult && isPortalSwap" class="portal-swap-overlay">
            <div class="portal-ring portal-ring-left"></div>
            <div class="portal-swap-text">PORTAL!</div>
            <div class="portal-ring portal-ring-right"></div>
          </div>
        </template>
      </div>

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

/* ── v-html icon badges (bypass scoped CSS) ── */
.fa-factor-detail :deep(.gi) { display: inline-block; font-size: 9px; font-weight: 800; padding: 1px 4px; border-radius: 3px; letter-spacing: 0.5px; vertical-align: middle; }
.fa-factor-detail :deep(.gi-int) { background: rgba(110, 170, 240, 0.12); color: var(--kh-c-secondary-info-200); }
.fa-factor-detail :deep(.gi-str) { background: rgba(239, 128, 128, 0.12); color: var(--kh-c-secondary-danger-200); }
.fa-factor-detail :deep(.gi-spd) { background: rgba(200, 185, 50, 0.12); color: var(--kh-c-text-highlight-dim); }
.fa-factor-detail :deep(.gi-psy) { background: rgba(232, 121, 249, 0.12); color: #e879f9; }
.fa-factor-detail :deep(.gi-ok) { color: var(--accent-green); font-weight: 800; }
.fa-factor-detail :deep(.gi-fail) { color: var(--accent-red); font-weight: 800; }
.fa-factor-detail :deep(.gi-tie) { color: var(--text-muted); }
.fa-factor-detail :deep(.dom-good) { color: var(--accent-green); font-weight: 900; text-transform: uppercase; font-size: 9px; letter-spacing: 0.5px; }
.fa-factor-detail :deep(.dom-bad) { color: var(--accent-red); font-weight: 900; text-transform: uppercase; font-size: 9px; letter-spacing: 0.5px; }

/* ── Controls ── */
.fa-controls { display: flex; align-items: center; gap: 4px; padding: 4px 0; }
.fa-btn { background: var(--bg-surface); border: 1px solid var(--border-subtle); border-radius: var(--radius); padding: 3px 10px; font-size: 13px; cursor: pointer; color: var(--text-primary); transition: all 0.15s; }
.fa-btn:hover { background: var(--accent-blue); color: white; border-color: var(--accent-blue); }
.fa-speed { display: flex; gap: 2px; margin-left: 6px; }
.fa-speed-btn { background: var(--bg-surface); border: 1px solid var(--border-subtle); border-radius: 4px; padding: 2px 8px; font-size: 10px; font-weight: 700; cursor: pointer; color: var(--text-muted); transition: all 0.15s; }
.fa-speed-btn.active { background: var(--kh-c-secondary-purple-500); color: var(--text-primary); border-color: var(--accent-purple); }

/* ── Card ── */
.fa-card { background: var(--bg-inset); border: 1px solid var(--border-subtle); border-radius: var(--radius); padding: 4px 6px; display: flex; flex-direction: column; gap: 3px; position: relative; overflow: hidden; }

/* ── Portraits ── */
/* ── Compact identity + scale row ── */
.fa-id-left, .fa-id-right { display: flex; align-items: center; gap: 4px; flex-shrink: 0; }
.fa-id-left.winner .fa-id-name, .fa-id-right.winner .fa-id-name { color: var(--accent-green); font-weight: 800; }
.fa-id-left.winner .fa-ava-sm, .fa-id-right.winner .fa-ava-sm { border-color: var(--accent-green); box-shadow: var(--glow-green); }
.fa-ava-sm { width: 28px; height: 28px; border-radius: 50%; object-fit: cover; border: 2px solid var(--border-subtle); flex-shrink: 0; }
.fa-id-info { display: flex; flex-direction: column; line-height: 1.2; }
.fa-id-name { font-size: 10px; font-weight: 700; color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80px; }
.fa-id-char { font-size: 8px; color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80px; }

/* ── Bar ── */
.fa-bar-container { display: flex; align-items: center; gap: 6px; padding: 2px 0; }
.fa-bar-compact { padding: 2px 0; }
.fa-bar-compact .fa-bar-track { height: 16px; }

/* ── Justice: Number Slam ── */
.fj-slam-wrap { display: flex; align-items: center; justify-content: center; gap: 0; padding: 1px 0; position: relative; }

/* Impact shake on the whole container */
.fj-slam-wrap.fj-slam-impact { animation: fj-slam-shake 0.4s ease-out; }
@keyframes fj-slam-shake {
  0%, 100% { transform: translateX(0); }
  15% { transform: translateX(-4px); }
  30% { transform: translateX(4px); }
  45% { transform: translateX(-3px); }
  60% { transform: translateX(2px); }
  75% { transform: translateX(-1px); }
}

/* Numbers (justiceUseBlocks = false) */
.fj-slam-num { font-size: 16px; font-weight: 900; font-family: var(--font-mono); color: var(--text-muted); min-width: 24px; text-align: center; transition: all 0.5s cubic-bezier(0.2, 0.8, 0.2, 1.2); }
.fj-slam-num.winner { font-size: 20px; color: var(--accent-purple); text-shadow: 0 0 8px rgba(139, 92, 246, 0.5); transform: scale(1.1); }
.fj-slam-num.winner.fj-slam-right { color: var(--accent-red); text-shadow: 0 0 8px rgba(239, 128, 128, 0.5); }
.fj-slam-num.loser { font-size: 11px; color: var(--text-dim); opacity: 0.35; transform: scale(0.7); }
.fj-slam-num.tied { color: var(--accent-orange); animation: fj-tied-tremble 0.3s ease-in-out infinite; }

/* Block towers (justiceUseBlocks = true) */
.fj-slam-tower { display: flex; flex-direction: column; align-items: center; gap: 1px; min-width: 24px; transition: all 0.5s cubic-bezier(0.2, 0.8, 0.2, 1.2); }
.fj-slam-row { display: flex; gap: 2px; }
/* Pyramid depth rows */
.fj-slam-top { opacity: 0.35; transform: scale(0.6); margin-bottom: -3px; }
.fj-slam-back { opacity: 0.5; transform: scale(0.8); margin-bottom: -2px; }
.fj-block { width: 16px; height: 10px; border-radius: 2px; border: 1.5px solid var(--border-subtle); background: var(--bg-secondary); transition: all 0.4s ease; }
.fj-block-ours { border-color: rgba(139, 92, 246, 0.4); background: rgba(139, 92, 246, 0.12); }
.fj-block-enemy { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.12); }

/* Fly-in from sides */
.fj-slam-left { animation: fj-fly-left 0.4s ease-out both; }
.fj-slam-right { animation: fj-fly-right 0.4s ease-out both; }

@keyframes fj-fly-left {
  0% { transform: translateX(-24px) scale(0.5); opacity: 0; }
  70% { transform: translateX(3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); }
}
@keyframes fj-fly-right {
  0% { transform: translateX(24px) scale(0.5); opacity: 0; }
  70% { transform: translateX(-3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); }
}

/* Resolved states — winner glows, loser crumbles */
.fj-slam-tower.winner .fj-block { transform: scale(1.15); }
.fj-slam-tower.winner .fj-block-ours { background: rgba(139, 92, 246, 0.35); border-color: rgba(139, 92, 246, 0.7); box-shadow: 0 0 8px rgba(139, 92, 246, 0.4); }
.fj-slam-tower.winner .fj-block-enemy { background: rgba(239, 128, 128, 0.35); border-color: rgba(239, 128, 128, 0.7); box-shadow: 0 0 8px rgba(239, 128, 128, 0.4); }
.fj-slam-tower.winner .fj-slam-top { opacity: 0.65; }
.fj-slam-tower.winner .fj-slam-back { opacity: 0.8; }
.fj-slam-tower.loser { transform: scale(0.6); opacity: 0.3; }
.fj-slam-tower.loser .fj-block { border-style: dashed; }
.fj-slam-tower.tied .fj-block { border-color: rgba(230, 148, 74, 0.6); background: rgba(230, 148, 74, 0.15); }
.fj-slam-tower.tied { animation: fj-tied-tremble 0.3s ease-in-out infinite; }

@keyframes fj-tied-tremble {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-1px); }
  75% { transform: translateX(1px); }
}

/* VS / spark in the center */
.fj-slam-vs { font-size: 9px; font-weight: 900; color: var(--text-dim); text-transform: uppercase; letter-spacing: 0.5px; min-width: 20px; text-align: center; opacity: 0; transition: opacity 0.3s; }
.fj-slam-vs.visible { opacity: 1; }
.fj-slam-spark { font-size: 14px; animation: fj-spark-pop 0.5s ease-out both; display: inline-block; }
@keyframes fj-spark-pop {
  0% { transform: scale(0.3); opacity: 0; }
  40% { transform: scale(1.8); opacity: 1; }
  100% { transform: scale(1); opacity: 0.7; }
}
.fa-bar-track { flex: 1; height: 20px; background: var(--bg-secondary); border-radius: 10px; overflow: hidden; position: relative; border: 1px solid var(--border-subtle); }
.fa-bar-fill { height: 100%; border-radius: 10px; transition: width 0.5s ease; display: flex; align-items: center; justify-content: flex-end; padding-right: 6px; min-width: 40px; }
.fa-bar-fill.bar-attacker { background: linear-gradient(90deg, var(--kh-c-secondary-success-500), var(--accent-green)); }
.fa-bar-fill.bar-defender { background: linear-gradient(90deg, var(--accent-red-dim), var(--accent-red)); }
.fa-bar-fill.bar-even { background: linear-gradient(90deg, rgba(230, 148, 74, 0.6), var(--accent-orange)); }
.fa-bar-value { font-size: 9px; font-weight: 800; color: white; text-shadow: 0 1px 2px rgba(0,0,0,0.5); font-family: var(--font-mono); }

/* ── Weighing bar particles (leading edge sparkles/embers) ─────── */
/* Particles render inside the bar fill area, animating upward via */
/* background-position shift to create floating dot effect.        */
.fa-bar-fill.bar-particles-gold,
.fa-bar-fill.bar-particles-red {
  position: relative;
}
.fa-bar-fill.bar-particles-gold::after,
.fa-bar-fill.bar-particles-red::after {
  content: '';
  position: absolute;
  right: 0;
  top: -2px;
  width: 8px;
  height: calc(100% + 4px);
  pointer-events: none;
  z-index: 2;
}
.fa-bar-fill.bar-particles-gold::after {
  background-image:
    radial-gradient(1.5px 1.5px at 2px 2px, rgba(240, 200, 80, 0.9) 50%, transparent 50%),
    radial-gradient(1px 1px at 5px 7px, rgba(240, 200, 80, 0.7) 50%, transparent 50%),
    radial-gradient(1.5px 1.5px at 1px 12px, rgba(255, 220, 100, 0.85) 50%, transparent 50%),
    radial-gradient(1px 1px at 6px 17px, rgba(240, 200, 80, 0.6) 50%, transparent 50%),
    radial-gradient(1px 1px at 3px 22px, rgba(255, 230, 120, 0.75) 50%, transparent 50%);
  background-size: 8px 24px;
  animation: bar-sparkle-up 1.2s linear infinite;
}
.fa-bar-fill.bar-particles-red::after {
  background-image:
    radial-gradient(1.5px 1.5px at 2px 3px, rgba(239, 128, 128, 0.9) 50%, transparent 50%),
    radial-gradient(1px 1px at 5px 8px, rgba(239, 128, 128, 0.7) 50%, transparent 50%),
    radial-gradient(1.5px 1.5px at 1px 13px, rgba(255, 100, 90, 0.85) 50%, transparent 50%),
    radial-gradient(1px 1px at 6px 18px, rgba(239, 128, 128, 0.6) 50%, transparent 50%),
    radial-gradient(1px 1px at 3px 23px, rgba(255, 110, 100, 0.75) 50%, transparent 50%);
  background-size: 8px 24px;
  animation: bar-sparkle-up 1.5s linear infinite;
}

@keyframes bar-sparkle-up {
  0% {
    background-position: 0 0;
    opacity: 0.7;
  }
  50% {
    opacity: 1;
  }
  100% {
    background-position: 0 -24px;
    opacity: 0.4;
  }
}

/* ── Phase tracker (3 pips) ── */
.phase-tracker { display: flex; align-items: center; justify-content: center; gap: 0; padding: 2px 0 1px; }
.phase-tracker-inline { flex: 1; min-width: 0; justify-content: center; }
.phase-role-pip { padding: 2px 5px; border-radius: 4px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); flex-shrink: 0; }
.phase-role-text { font-size: 8px; font-weight: 900; letter-spacing: 0.5px; }
.phase-role-pip.role-atk { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.08); }
.phase-role-pip.role-atk .phase-role-text { color: var(--accent-red); }
.phase-role-pip.role-def { border-color: rgba(100, 160, 255, 0.4); background: rgba(100, 160, 255, 0.08); }
.phase-role-pip.role-def .phase-role-text { color: var(--accent-blue); }
.phase-pip { display: flex; flex-direction: column; align-items: center; gap: 1px; padding: 3px 6px; border-radius: 6px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); transition: all 0.4s ease; min-width: 28px; }
.phase-connector-outcome { width: 12px; }
.phase-outcome-pip { padding: 2px 8px; border-radius: 4px; border: 1.5px solid var(--border-subtle); background: var(--bg-inset); transition: all 0.4s ease; animation: phase-pop 0.4s ease; }
.phase-outcome-pending { opacity: 0.3; }
.phase-outcome-pip.phase-ours, .phase-outcome-pip.outcome-attacker { border-color: rgba(63, 167, 61, 0.5); background: rgba(63, 167, 61, 0.1); }
.phase-outcome-pip.phase-theirs, .phase-outcome-pip.outcome-defender { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.08); }
.phase-outcome-pip.outcome-neutral { border-color: rgba(230, 148, 74, 0.4); background: rgba(230, 148, 74, 0.08); }
.phase-outcome-text { font-size: 8px; font-weight: 900; letter-spacing: 0.5px; text-transform: uppercase; white-space: nowrap; }
.phase-outcome-pip.phase-ours .phase-outcome-text, .phase-outcome-pip.outcome-attacker .phase-outcome-text { color: var(--accent-green); }
.phase-outcome-pip.phase-theirs .phase-outcome-text, .phase-outcome-pip.outcome-defender .phase-outcome-text { color: var(--accent-red); }
.phase-outcome-pip.outcome-neutral .phase-outcome-text { color: var(--accent-orange); }
.phase-outcome-pending .phase-outcome-text { color: var(--text-dim); }
.phase-icon { font-size: 13px; font-weight: 900; line-height: 1; transition: all 0.3s; }
.phase-icon-pending { color: var(--text-dim); opacity: 0.4; }
.phase-icon-win { color: var(--accent-green); animation: phase-icon-in 0.4s ease; }
.phase-icon-lose { color: var(--accent-red); animation: phase-icon-in 0.4s ease; }
.phase-icon-draw { color: var(--accent-orange); animation: phase-icon-in 0.4s ease; }
.phase-icon-skip { color: var(--text-dim); opacity: 0.3; }
.phase-label { font-size: 7px; font-weight: 700; color: var(--text-dim); text-transform: uppercase; letter-spacing: 0.3px; transition: color 0.4s; }
.phase-connector { width: 16px; height: 2px; background: var(--border-subtle); transition: background 0.4s; flex-shrink: 0; }
.phase-connector.revealed { background: var(--text-muted); }
.phase-connector.broken { background: none; border-top: 2px dashed var(--border-subtle); height: 0; }

.phase-pip.phase-ours { border-color: rgba(63, 167, 61, 0.5); background: rgba(63, 167, 61, 0.08); animation: phase-pop 0.4s ease; }
.phase-pip.phase-ours .phase-label { color: var(--accent-green); }
.phase-pip.phase-theirs { border-color: rgba(239, 128, 128, 0.4); background: rgba(239, 128, 128, 0.06); animation: phase-pop 0.4s ease; }
.phase-pip.phase-theirs .phase-label { color: var(--accent-red); }
.phase-pip.phase-draw { border-color: rgba(230, 148, 74, 0.4); background: rgba(230, 148, 74, 0.06); animation: phase-pop 0.4s ease; }
.phase-pip.phase-draw .phase-label { color: var(--accent-orange); }
.phase-pip.phase-skipped { opacity: 0.3; border-style: dashed; }

@keyframes phase-icon-in {
  0% { transform: scale(0); opacity: 0; }
  50% { transform: scale(1.3); }
  100% { transform: scale(1); opacity: 1; }
}

@keyframes phase-pop {
  0% { transform: scale(1); }
  50% { transform: scale(1.15); }
  100% { transform: scale(1); }
}

/* ── Phase result badge (replaces old round-result) ── */
.fa-phase-result { display: flex; align-items: center; justify-content: center; gap: 4px; font-size: 10px; font-weight: 700; padding: 2px 10px; border-radius: 4px; margin: 2px 0; animation: phase-result-in 0.3s ease; }
.phase-result-icon { font-size: 11px; font-weight: 900; }
.fa-phase-result.phase-ours { background: rgba(63, 167, 61, 0.1); color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.2); }
.fa-phase-result.phase-theirs { background: rgba(239, 128, 128, 0.08); color: var(--accent-red); border: 1px solid rgba(239, 128, 128, 0.15); }
.fa-phase-result.phase-draw { background: rgba(230, 148, 74, 0.08); color: var(--accent-orange); border: 1px solid rgba(230, 148, 74, 0.15); }

@keyframes phase-result-in {
  0% { opacity: 0; transform: translateY(-4px); }
  100% { opacity: 1; transform: translateY(0); }
}

/* ── Factors ── */
.fa-factors { display: flex; flex-direction: column; gap: 2px; }
.fa-factor { display: flex; align-items: center; gap: 6px; padding: 3px 8px; border-radius: 4px; font-size: 11px; background: var(--bg-secondary); opacity: 0; transform: translateY(4px); transition: opacity 0.3s, transform 0.3s; border: 1px solid transparent; }
.fa-factor.visible { opacity: 1; transform: translateY(0); }
.fa-factor.good { border-left: 2px solid var(--accent-green); }
.fa-factor.bad { border-left: 2px solid var(--accent-red); }
.fa-factor.neutral { border-left: 2px solid var(--accent-orange); }
.fa-factor.justice { border-left: 2px solid var(--accent-purple); }
.fa-factor.random { border-left: 2px solid var(--accent-blue); }
.fa-factor-label { font-weight: 700; color: var(--text-primary); min-width: 100px; font-size: 10px; }
.fa-factor-detail { color: var(--text-secondary); flex: 1; font-size: 10px; display: flex; align-items: center; gap: 3px; flex-wrap: wrap; }
.fa-factor-value { font-weight: 800; color: var(--accent-gold); margin-left: auto; font-family: var(--font-mono); font-size: 11px; flex-shrink: 0; }
.fa-factor-detail :deep(.admin-extra) { color: var(--text-dim); font-size: 9px; font-family: var(--font-mono); }

/* ── Tier intensity (non-admin factor rows) ── */
/* Border thickness scales with tier */
.fa-factor.good.tier-0 { border-left-width: 2px; }
.fa-factor.good.tier-1 { border-left-width: 3px; }
.fa-factor.good.tier-2 { border-left-width: 4px; background: rgba(63, 167, 61, 0.06); }
.fa-factor.bad.tier-0 { border-left-width: 2px; }
.fa-factor.bad.tier-1 { border-left-width: 3px; }
.fa-factor.bad.tier-2 { border-left-width: 4px; background: rgba(239, 128, 128, 0.06); }
/* Tier 0: muted text */
.fa-factor.tier-0 :deep(.gi-ok),
.fa-factor.tier-0 :deep(.gi-fail) { opacity: 0.65; }
/* Tier 1: normal (default styling) */
/* Tier 2: bold glow + pulse */
.fa-factor.tier-2 :deep(.gi-ok) { font-weight: 900; text-shadow: 0 0 6px rgba(63, 167, 61, 0.5); }
.fa-factor.tier-2 :deep(.gi-fail) { font-weight: 900; text-shadow: 0 0 6px rgba(239, 128, 128, 0.5); }
.fa-factor.tier-2.visible { animation: tier2-pulse 1.8s ease-in-out 1; }
@keyframes tier2-pulse {
  0%, 100% { filter: brightness(1); }
  40% { filter: brightness(1.25); }
}
.fa-factor-value.good-val { color: var(--accent-green); }
.fa-factor-value.bad-val { color: var(--accent-red); }
.neutral-val { color: var(--text-muted); }

/* ── Badges (TooGood / TooStronk) ── */
.fa-badge-row { display: flex; justify-content: center; gap: 6px; padding: 2px 0; opacity: 0; transition: opacity 0.3s; }
.fa-badge-row.visible { opacity: 1; }
.fa-badge { font-size: 9px; font-weight: 900; padding: 2px 10px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.5px; }
.badge-toogood { background: rgba(63, 167, 61, 0.1); color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.3); }
.badge-toostronk { background: rgba(180, 150, 255, 0.1); color: var(--accent-purple); border: 1px solid rgba(180, 150, 255, 0.3); }
.badge-skill { background: rgba(233, 219, 61, 0.1); color: var(--accent-gold); border: 1px solid rgba(233, 219, 61, 0.3); }

/* ── R3 details ── */
.fa-r3-details { display: flex; flex-direction: column; gap: 2px; }
.pct-good { color: var(--accent-green); font-weight: 700; }
.pct-bad { color: var(--accent-red); font-weight: 700; }
.fa-chance-total { font-size: 13px; font-weight: 800; }
/* ── Roll bar ── */
.fa-roll-bar-wrap { margin-top: 3px; padding-top: 3px; }
.fa-roll-verdict { font-weight: 900; font-size: 11px; }
.fa-roll-bar-track { position: relative; height: 22px; background: rgba(239, 128, 128, 0.12); border-radius: 6px; overflow: visible; border: 1px solid var(--border-subtle); }
.fa-roll-zone-win { position: absolute; left: 0; top: 0; height: 100%; background: rgba(63, 167, 61, 0.15); border-radius: 6px 0 0 6px; transition: width 0.6s ease; }
.fa-roll-threshold { position: absolute; top: -2px; bottom: -2px; width: 2px; background: var(--accent-gold); z-index: 2; transform: translateX(-1px); }
.fa-roll-threshold-label { position: absolute; top: -14px; left: 50%; transform: translateX(-50%); font-size: 8px; font-weight: 800; color: var(--accent-gold); white-space: nowrap; font-family: var(--font-mono); }
.fa-roll-needle { position: absolute; top: -2px; bottom: -2px; width: 3px; z-index: 3; transform: translateX(-1.5px); border-radius: 2px; transition: none; }
.fa-roll-needle.needle-win { background: var(--accent-green); box-shadow: 0 0 8px rgba(63, 167, 61, 0.6); }
.fa-roll-needle.needle-lose { background: var(--accent-red); box-shadow: 0 0 8px rgba(239, 128, 128, 0.6); }
.fa-roll-needle.needle-settled.needle-win { box-shadow: 0 0 14px rgba(63, 167, 61, 0.8), 0 0 28px rgba(63, 167, 61, 0.3); width: 4px; }
.fa-roll-needle.needle-settled.needle-lose { box-shadow: 0 0 14px rgba(239, 128, 128, 0.8), 0 0 28px rgba(239, 128, 128, 0.3); width: 4px; }
.fa-roll-needle-val { position: absolute; bottom: -14px; left: 50%; transform: translateX(-50%); font-size: 8px; font-weight: 800; white-space: nowrap; font-family: var(--font-mono); }
.needle-win .fa-roll-needle-val { color: var(--accent-green); }
.needle-lose .fa-roll-needle-val { color: var(--accent-red); }
/* Overflow: chance > 100% — zone fills track and bursts through right edge */
.fa-roll-bar-track.track-overflow {
  border-right: none;
  border-radius: 6px 0 0 6px;
}
.fa-roll-zone-win.zone-overflow {
  width: 100% !important;
  border-radius: 6px 0 0 6px;
  box-shadow: 4px 0 12px rgba(63, 167, 61, 0.4), 8px 0 24px rgba(63, 167, 61, 0.2);
}
.fa-roll-bar-wrap.roll-overflow { position: relative; }
.fa-roll-bar-wrap.roll-overflow::after {
  content: '';
  position: absolute;
  right: -8px;
  top: 3px;
  bottom: 0;
  width: 16px;
  background: linear-gradient(90deg, rgba(63, 167, 61, 0.3), transparent);
  border-radius: 0 6px 6px 0;
  pointer-events: none;
}
.fa-roll-bar-labels { display: flex; justify-content: space-between; font-size: 7px; color: var(--text-dim); margin-top: 14px; font-family: var(--font-mono); }

/* ── Enemy summary ── */
.fa-enemy-summary { display: flex; gap: 6px; justify-content: center; padding: 2px 0; }

/* ── Result ── */
.fa-result { display: flex; flex-direction: column; align-items: center; gap: 3px; padding: 3px 0 2px; }
.fa-outcome { font-size: 12px; font-weight: 900; text-transform: uppercase; padding: 4px 16px; border-radius: var(--radius); letter-spacing: 0.5px; }
.outcome-attacker { background: var(--kh-c-secondary-success-500); color: var(--text-primary); border: 1px solid var(--accent-green); }
.outcome-defender { background: var(--accent-red-dim); color: var(--text-primary); border: 1px solid var(--accent-red); }
.outcome-neutral { background: rgba(230, 148, 74, 0.3); color: var(--accent-orange); border: 1px solid var(--accent-orange); }

.fa-result-details { display: flex; flex-wrap: wrap; gap: 4px; justify-content: center; font-size: 10px; padding-top: 2px; }
.fa-detail-item { padding: 1px 8px; border-radius: 4px; background: var(--bg-secondary); border: 1px solid var(--border-subtle); }
.fa-skill-gain { color: var(--accent-green); font-weight: 700; }
.fa-moral { color: var(--accent-purple); }
.fa-justice { color: var(--accent-blue); }
.fa-resist { color: var(--accent-orange); }
/* Damage to US — alarming red pulse */
.fa-dmg-us { color: var(--accent-red); font-weight: 800; font-style: italic; animation: dmg-pulse 0.6s ease-in-out 2; }
/* Damage to ENEMY — muted, informational */
.fa-dmg-enemy { color: var(--text-muted); font-weight: 600; font-style: italic; }
@keyframes dmg-pulse { 0%,100% { opacity: 1; } 50% { opacity: 0.4; } }
.fa-drop { color: white; background: var(--accent-red-dim) !important; border-color: var(--accent-red) !important; font-weight: 800; animation: dmg-pulse 0.6s ease-in-out 2; }
.fa-drop-enemy { color: var(--text-muted); font-weight: 600; }

/* ── Special (block/skip) ── */
.fa-special { display: flex; justify-content: center; padding: 4px 0; }

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
/* ── All Fights list ── */
.fa-all-fights { flex: 1; overflow-y: auto; }
.fa-all-list { display: flex; flex-direction: column; gap: 3px; max-width: 480px; margin: 0 auto; }
.fa-all-row { display: grid; grid-template-columns: 1fr auto 1fr auto; align-items: center; gap: 4px; padding: 5px 8px; border-radius: var(--radius); background: var(--bg-inset); font-size: 12px; border: 1px solid transparent; transition: all 0.15s; }
.fa-all-row.my-attack { border-left: 2px solid var(--accent-gold-dim); }
.fa-all-row.clickable { cursor: pointer; }
.fa-all-row.clickable:hover { background: rgba(180, 150, 255, 0.12); border-color: rgba(180, 150, 255, 0.3); }
.fa-all-play { font-size: 10px; color: var(--text-dim); flex-shrink: 0; opacity: 0.4; transition: opacity 0.15s; }
.fa-all-row.clickable:hover .fa-all-play { opacity: 1; color: var(--accent-gold); }

/* Portal Gun swap overlay (fight replay) */
.portal-swap-overlay {
  display: flex; align-items: center; justify-content: center;
  gap: 8px; padding: 4px 0;
  animation: portal-fade-in 0.3s ease-out;
}
.portal-ring {
  width: 24px; height: 24px; border-radius: 50%;
  border: 3px solid transparent;
  border-top-color: #00ff88; border-right-color: #00cc66;
  animation: portal-spin 1s linear infinite;
  box-shadow: 0 0 10px rgba(0, 255, 136, 0.4), inset 0 0 6px rgba(0, 255, 136, 0.2);
}
.portal-ring-right {
  animation-direction: reverse;
  border-top-color: #ff8800; border-right-color: #cc6600;
  box-shadow: 0 0 10px rgba(255, 136, 0, 0.4), inset 0 0 6px rgba(255, 136, 0, 0.2);
}
@keyframes portal-spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
.portal-swap-text {
  font-size: 13px; font-weight: 900; color: #00ff88;
  text-transform: uppercase; letter-spacing: 2px;
  text-shadow: 0 0 12px rgba(0, 255, 136, 0.6);
  animation: portal-text-pulse 0.8s ease-in-out infinite alternate;
}
@keyframes portal-text-pulse {
  0% { opacity: 0.7; transform: scale(0.95); }
  100% { opacity: 1; transform: scale(1.05); }
}
@keyframes portal-fade-in {
  0% { opacity: 0; transform: scale(0.5); }
  60% { transform: scale(1.1); }
  100% { opacity: 1; transform: scale(1); }
}
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

/* ── Летопись ── */
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

/* Story popup styles moved to unscoped block below (Teleported to body) */

/* ── Fight result border glow ─────────────────────────────────────── */
.fa-card.fa-result-win {
  border-color: rgba(63, 167, 61, 0.5);
  box-shadow: 0 0 12px rgba(63, 167, 61, 0.25), 0 0 24px rgba(63, 167, 61, 0.1), inset 0 0 8px rgba(63, 167, 61, 0.05);
  transition: border-color 0.4s, box-shadow 0.4s;
}
.fa-card.fa-result-loss {
  border-color: rgba(239, 128, 128, 0.5);
  box-shadow: 0 0 12px rgba(239, 128, 128, 0.25), 0 0 24px rgba(239, 128, 128, 0.1), inset 0 0 8px rgba(239, 128, 128, 0.05);
  transition: border-color 0.4s, box-shadow 0.4s;
}

/* ── Result badge (corner icon) ──────────────────────────────────── */
.fa-result-badge {
  position: absolute;
  bottom: 8px;
  right: 10px;
  font-size: 18px;
  font-weight: 900;
  z-index: 10;
  pointer-events: none;
  line-height: 1;
}
.fa-result-badge-win {
  color: var(--accent-green);
  text-shadow: 0 0 8px rgba(63, 167, 61, 0.6);
}
.fa-result-badge-loss {
  color: var(--accent-red);
  text-shadow: 0 0 8px rgba(239, 128, 128, 0.6);
}

.result-badge-enter-active { transition: opacity 0.3s, transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1); }
.result-badge-leave-active { transition: opacity 0.4s; }
.result-badge-enter-from { opacity: 0; transform: scale(0.5); }
.result-badge-leave-to { opacity: 0; }

/* ── Screen shake on big stat differences ─────────────────────────── */
.fa-card.fa-shake {
  animation: fight-shake 0.5s ease-in-out;
}

@keyframes fight-shake {
  0%, 100% { transform: translateX(0); }
  10% { transform: translateX(-3px); }
  20% { transform: translateX(3px); }
  30% { transform: translateX(-2px); }
  40% { transform: translateX(2px); }
  50% { transform: translateX(-1px); }
  60% { transform: translateX(1px); }
}

/* ── Winning stat emphasis ─────────────────────────────────────────── */
.fa-id-left.winner .fa-id-char, .fa-id-right.winner .fa-id-char {
  color: var(--accent-green);
  text-shadow: 0 0 4px rgba(63, 167, 61, 0.3);
}

/* ── Phase 5a: Avatar clash intro ─────────────────────────────────── */
.fa-id-left .fa-ava-sm {
  animation: avatar-clash-left 0.5s cubic-bezier(0.34, 1.56, 0.64, 1);
}
.fa-id-right .fa-ava-sm {
  animation: avatar-clash-right 0.5s cubic-bezier(0.34, 1.56, 0.64, 1);
}

@keyframes avatar-clash-left {
  0% { transform: translateX(-20px) scale(0.8); opacity: 0.5; }
  50% { transform: translateX(3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}
@keyframes avatar-clash-right {
  0% { transform: translateX(20px) scale(0.8); opacity: 0.5; }
  50% { transform: translateX(-3px) scale(1.05); opacity: 1; }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}

/* ── Enhanced entrance animation (triggered on intro step) ────────── */
.fa-id-left.entrance-active .fa-ava-sm {
  animation: entrance-left 400ms cubic-bezier(0.34, 1.56, 0.64, 1) both;
  position: relative;
}
.fa-id-right.entrance-active .fa-ava-sm {
  animation: entrance-right 400ms cubic-bezier(0.34, 1.56, 0.64, 1) both;
  position: relative;
}

@keyframes entrance-left {
  0% { transform: translateX(-40px) scale(0.6); opacity: 0; }
  60% { transform: translateX(6px) scale(1.08); opacity: 1; }
  80% { transform: translateX(-2px) scale(0.98); }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}
@keyframes entrance-right {
  0% { transform: translateX(40px) scale(0.6); opacity: 0; }
  60% { transform: translateX(-6px) scale(1.08); opacity: 1; }
  80% { transform: translateX(2px) scale(0.98); }
  100% { transform: translateX(0) scale(1); opacity: 1; }
}

/* Impact particle flash on entrance */
.fa-id-left.entrance-active .fa-ava-sm::after,
.fa-id-right.entrance-active .fa-ava-sm::after {
  content: '';
  position: absolute;
  inset: -8px;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(240, 200, 80, 0.6) 0%, rgba(240, 200, 80, 0) 70%);
  opacity: 0;
  animation: entrance-impact-flash 500ms ease-out 300ms forwards;
  pointer-events: none;
  z-index: 1;
}

@keyframes entrance-impact-flash {
  0% { opacity: 0; transform: scale(0.4); }
  30% { opacity: 0.9; transform: scale(1.2); }
  100% { opacity: 0; transform: scale(1.8); }
}


/* ── Phase 5b: Factor reveal bounce ───────────────────────────────── */
.fa-factor.visible {
  animation: factor-slide-in 0.35s cubic-bezier(0.34, 1.56, 0.64, 1);
}

@keyframes factor-slide-in {
  0% { opacity: 0; transform: translateX(-15px); }
  60% { opacity: 1; transform: translateX(3px); }
  100% { opacity: 1; transform: translateX(0); }
}

/* Tier-2 factors get a glow burst */
.fa-factor.tier-2.visible::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  opacity: 0;
  animation: factor-glow-burst 0.8s ease-out 0.2s;
  pointer-events: none;
}
.fa-factor.tier-2.good.visible::before {
  background: radial-gradient(ellipse at 10% 50%, rgba(91, 168, 91, 0.2), transparent 70%);
}
.fa-factor.tier-2.bad.visible::before {
  background: radial-gradient(ellipse at 10% 50%, rgba(224, 85, 69, 0.2), transparent 70%);
}

@keyframes factor-glow-burst {
  0% { opacity: 0; }
  30% { opacity: 1; }
  100% { opacity: 0; }
}


/* ── Phase 5d: Justice slam zoom ──────────────────────────────────── */
.fj-slam-wrap.fj-slam-impact .fa-card {
  animation: justice-zoom 0.3s ease-out;
}

@keyframes justice-zoom {
  0% { transform: scale(1); }
  50% { transform: scale(1.04); }
  100% { transform: scale(1); }
}

/* Winner justice number ring */
.fj-slam-num.winner::after {
  content: '';
  position: absolute;
  inset: -4px;
  border-radius: 50%;
  border: 2px solid var(--accent-gold);
  opacity: 0;
  animation: justice-ring 0.6s ease-out 0.4s forwards;
  pointer-events: none;
}

@keyframes justice-ring {
  0% { transform: scale(0.5); opacity: 0.8; }
  100% { transform: scale(1.5); opacity: 0; }
}

/* ── Phase 5e: Roll bar needle trail ──────────────────────────────── */
.fa-roll-needle::before {
  content: '';
  position: absolute;
  top: 2px;
  bottom: 2px;
  left: -1px;
  width: 5px;
  border-radius: 3px;
  opacity: 0.3;
  filter: blur(2px);
  pointer-events: none;
  transition: none;
}
.fa-roll-needle.needle-win::before { background: var(--accent-green); }
.fa-roll-needle.needle-lose::before { background: var(--accent-red); }

/* Sound-visual beat (Phase 9a) — brief border flash on fight card */
.fa-card.fight-beat {
  animation: fight-beat-flash 0.15s ease-out;
}
@keyframes fight-beat-flash {
  0% { border-color: rgba(240, 200, 80, 0.6); }
  100% { border-color: var(--border-subtle); }
}

/* ── Impact cracks radiating from center ───────────────────────────── */
.impact-cracks {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  pointer-events: none;
  z-index: 5;
}
.crack {
  position: absolute;
  width: 2px;
  height: 0;
  background: linear-gradient(to bottom, rgba(255, 255, 255, 0.6), transparent);
  transform-origin: top center;
  animation: crack-grow 0.3s ease-out forwards;
}
.crack-1 { transform: rotate(-30deg); }
.crack-2 { transform: rotate(25deg); }
.crack-3 { transform: rotate(-60deg); }
.crack-4 { transform: rotate(50deg); }
@keyframes crack-grow {
  0% { height: 0; opacity: 1; }
  60% { height: 30px; opacity: 0.8; }
  100% { height: 40px; opacity: 0; }
}

/* ── Justice collision physics: winner/loser/tie ───────────────────── */
.justice-winner {
  transform: scale(1.3) !important;
  text-shadow: 0 0 20px rgba(240, 200, 80, 0.8);
  transition: all 0.3s cubic-bezier(0.2, 0.8, 0.2, 1.2);
}
.justice-loser {
  transform: scale(0.7) !important;
  opacity: 0.5;
  transition: all 0.3s ease-out;
}
.justice-tied-pulse {
  animation: justice-tie-pulse 0.4s ease-in-out 1;
}
@keyframes justice-tie-pulse {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.15); }
}
/* Loser recoil: push away from center */
.fj-slam-left.justice-loser {
  transform: scale(0.7) translateX(-8px) !important;
}
.fj-slam-right.justice-loser {
  transform: scale(0.7) translateX(8px) !important;
}
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
