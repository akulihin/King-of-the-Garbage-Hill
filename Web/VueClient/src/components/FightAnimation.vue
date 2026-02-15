<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import type { FightEntry, Player, Prediction, CharacterInfo } from 'src/services/signalr'

const props = withDefaults(defineProps<{
  fights: FightEntry[]
  letopis?: string
  players?: Player[]
  myPlayerId?: string
  predictions?: Prediction[]
  isAdmin?: boolean
  characterCatalog?: CharacterInfo[]
}>(), {
  letopis: '',
  players: () => [],
  myPlayerId: '',
  predictions: () => [],
  isAdmin: false,
  characterCatalog: () => [],
})

const emit = defineEmits<{
  (e: 'resist-flash', stats: string[]): void
  (e: 'justice-reset'): void
}>()

/** Active tab: 'fights' = replay, 'all' = compact results list, 'letopis' = full text log */
const activeTab = ref<'fights' | 'all' | 'letopis'>('fights')
/** Tracks whether user manually switched tabs this round (prevents auto-transition) */
const userSwitchedTab = ref(false)
function setTab(tab: 'fights' | 'all' | 'letopis') {
  activeTab.value = tab
  userSwitchedTab.value = true
}

// ── Playback state ──────────────────────────────────────────────────
const currentFightIdx = ref(0)
const currentStep = ref(0)
const isPlaying = ref(true)
const speed = ref(1)
const skippedToEnd = ref(false)
const lastAnimatedRound = ref<string>('')
let timer: ReturnType<typeof setTimeout> | null = null

// ── My username (for filtering) ─────────────────────────────────────
const myUsername = computed(() => {
  const p = props.players.find((pl: Player) => pl.playerId === props.myPlayerId)
  return p?.discordUsername ?? ''
})

function isFightMine(f: FightEntry): boolean {
  if (props.isAdmin) return true
  if (!myUsername.value) return false
  return f.attackerName === myUsername.value || f.defenderName === myUsername.value
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
}

const round1Factors = computed<Factor[]>(() => {
  const f = fight.value
  if (!f || isSpecialOutcome.value) return []
  const s = sign.value // +1 or -1 depending on flip
  const list: Factor[] = []

  // Helper: after applying sign, positive = good for us
  const hl = (v: number): 'good' | 'bad' | 'neutral' => v > 0 ? 'good' : v < 0 ? 'bad' : 'neutral'


  // 1. WhoIsBetter
  if (f.whoIsBetterWeighingDelta !== 0) {
    const v = f.whoIsBetterWeighingDelta * s
    // Build stat-by-stat breakdown from our (left) perspective
    const wi = f.whoIsBetterIntel * (isFlipped.value ? -1 : 1)
    const ws = f.whoIsBetterStr * (isFlipped.value ? -1 : 1)
    const wsp = f.whoIsBetterSpeed * (isFlipped.value ? -1 : 1)
    const ic = (val: number) => val > 0 ? '<span class="gi-ok">&#x2713;</span>' : val < 0 ? '<span class="gi-fail">&#x2717;</span>' : '<span class="gi-tie">&#x2015;</span>'
    const detail = `<span class="gi gi-int">INT</span>${ic(wi)} <span class="gi gi-str">STR</span>${ic(ws)} <span class="gi gi-spd">SPD</span>${ic(wsp)}`
    list.push({
      label: 'Кто разностороннее',
      detail,
      value: v,
      highlight: hl(v),
    })
  }

  // 2. Contre
  if (f.contrWeighingDelta !== 0) {
    const v = f.contrWeighingDelta * s
    // Determine who contres from our (left) perspective
    const weContre = isFlipped.value ? f.isContrTarget : f.isContrMe
    const theyContre = isFlipped.value ? f.isContrMe : f.isContrTarget
    const ourClass = isFlipped.value ? f.defenderClass : f.attackerClass
    const theirClass = isFlipped.value ? f.attackerClass : f.defenderClass
    const ci = (c: string) => c === 'Интеллект' ? '<span class="gi gi-int">INT</span>' : c === 'Сила' ? '<span class="gi gi-str">STR</span>' : c === 'Скорость' ? '<span class="gi gi-spd">SPD</span>' : '<span class="gi">?</span>'
    let detail: string
    if (weContre && theyContre) {
      detail = `Обоюдная: ${ci(ourClass)}→${ci(theirClass)} / ${ci(theirClass)}→${ci(ourClass)}`
    } else if (weContre) {
      detail = `${ci(ourClass)} контрит ${ci(theirClass)} (x${f.contrMultiplier})`
    } else {
      detail = `${ci(theirClass)} контрит ${ci(ourClass)}`
    }
    list.push({
      label: 'Контра',
      detail,
      value: v,
      highlight: hl(v),
    })
  }

  // 3. Scale (stats + skill*multiplier)
  {
    const v = f.scaleWeighingDelta * s
    const ourScale = isFlipped.value ? f.scaleTarget : f.scaleMe
    const theirScale = isFlipped.value ? f.scaleMe : f.scaleTarget
    list.push({
      label: 'Статы',
      detail: `${ourScale.toFixed(1)} vs ${theirScale.toFixed(1)}`,
      value: v,
      highlight: hl(v),
    })
  }



  // 4. Psyche
  if (f.psycheWeighingDelta !== 0) {
    const v = f.psycheWeighingDelta * s
    list.push({
      label: 'Психика',
      detail: v > 0 ? '<span class="gi gi-psy">PSY</span> <span class="gi-ok">&#x2713;</span>' : '<span class="gi gi-psy">PSY</span> <span class="gi-fail">&#x2717;</span>',
      value: v,
      highlight: hl(v),
    })
  }

  // 5. Skill difference
  if (f.skillWeighingDelta !== 0) {
    const v = f.skillWeighingDelta * s
    const ourMult = isFlipped.value ? f.skillMultiplierTarget : f.skillMultiplierMe
    const theirMult = isFlipped.value ? f.skillMultiplierMe : f.skillMultiplierTarget
    list.push({
      label: 'Навык',
      //detail: `Skill x${ourMult} vs x${theirMult}`,
      detail: `x${ourMult} vs x${theirMult}`,
      value: v,
      highlight: hl(v),
    })
  }

  // 6. Justice in weighing
  if (f.justiceWeighingDelta !== 0) {
    const v = f.justiceWeighingDelta * s
    const ourJ = isFlipped.value ? f.justiceTarget : f.justiceMe
    const theirJ = isFlipped.value ? f.justiceMe : f.justiceTarget
    list.push({
      label: 'Справедливость',
      detail: `${ourJ} vs ${theirJ}`,
      value: v,
      highlight: hl(v),
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
  if (fight.value?.usedRandomRoll) steps += 1 // R3
  return steps
})

// ── Weighing machine bar animation ──────────────────────────────────
// Values are flipped so positive = good for left (us)
const animatedWeighingValue = computed(() => {
  if (!fight.value || isSpecialOutcome.value) return 0
  if (skippedToEnd.value || !isMyFight.value) return fight.value.weighingMachine * sign.value

  const factorCount = round1Factors.value.length
  let accumulated = 0
  const factorStepsShown = Math.min(currentStep.value, factorCount)
  for (let i = 0; i < factorStepsShown; i++) {
    accumulated += round1Factors.value[i].value // already sign-adjusted
  }
  if (currentStep.value > factorCount) {
    accumulated = fight.value.weighingMachine * sign.value
  }
  return accumulated
})

const barPosition = computed(() => {
  const val = animatedWeighingValue.value
  const clamped = Math.max(-50, Math.min(50, val))
  return 50 + (clamped / 50) * 50
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

const showFinalResult = computed(() => {
  if (skippedToEnd.value || !isMyFight.value) return true
  return currentStep.value >= totalSteps.value - 1
})

// ── Emit resist flash / justice reset to PlayerCard when result appears ──
watch(showFinalResult, (show: boolean) => {
  if (!show || !fight.value || !isMyFight.value) return
  const f = fight.value
  const weWon = isFlipped.value ? f.outcome === 'loss' : f.outcome === 'win'
  const weLost = !weWon && f.outcome !== 'block' && f.outcome !== 'skip'

  // Justice resets when we win
  if (weWon) {
    emit('justice-reset')
  }

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

function advanceStep() {
  if (!isPlaying.value || !fight.value) return
  if (currentStep.value < totalSteps.value - 1) {
    currentStep.value++
    scheduleNext()
  } else {
    const delay = betweenFightDelay()
    setTimeout(() => {
      if (!isPlaying.value) return
      if (currentFightIdx.value < myFights.value.length - 1) {
        currentFightIdx.value++
        currentStep.value = 0
        scheduleNext()
      } else {
        isPlaying.value = false
        // Auto-transition to 'all' tab after replay finishes (unless user already switched)
        if (!userSwitchedTab.value && activeTab.value === 'fights') {
          setTimeout(() => { activeTab.value = 'all' }, 800)
        }
      }
    }, delay)
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
    lastAnimatedRound.value = fp
    userSwitchedTab.value = false
    activeTab.value = 'fights'
    restart()
  }
}, { deep: true })

onUnmounted(() => { clearTimer() })

// ── Auto-scroll: keep the latest animated step visible in panel ──────
const fightCardRef = ref<HTMLElement | null>(null)
watch(currentStep, () => {
  nextTick(() => {
    if (!fightCardRef.value) return
    // Find the scrollable parent (.fight-panel)
    const panel = fightCardRef.value.closest('.fight-panel') || fightCardRef.value.parentElement
    if (panel) {
      panel.scrollTop = panel.scrollHeight
    }
  })
})

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
function roundResultLabel(pts: number): string {
  if (pts > 0) return '+1 очко'
  if (pts < 0) return '-1 очко'
  return 'Ничья'
}
function roundResultClass(pts: number): string {
  if (pts > 0) return 'round-win'
  if (pts < 0) return 'round-loss'
  return 'round-draw'
}
function formatLetopis(text: string): string {
  return text
    .replace(/<:[^:]+:(\d+)>/g, '')
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/__(.*?)__/g, '<u>$1</u>')
    .replace(/\*(.*?)\*/g, '<em>$1</em>')
    .replace(/~~(.*?)~~/g, '<del>$1</del>')
    .replace(/\|>Phrase<\|/g, '')
    .replace(/\n/g, '<br>')
}
function fmtVal(v: number): string {
  return (v > 0 ? '+' : '') + v.toFixed(1)
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

/** Round 3: Justice contribution to our chance (percentage points) */
const r3JusticePct = computed(() => {
  const f = fight.value
  if (!f || f.maxRandomNumber === 0 || f.justiceRandomChange === 0) return 0
  const s = sign.value
  // justiceRandomChange = 100 - maxRandom (positive = shrunk range = attacker benefits)
  // Attacker's chance delta: rfp * (100/maxRandom - 1)
  const attackerDelta = f.randomForPoint * (100 / f.maxRandomNumber - 1)
  return attackerDelta * s
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

/** Round 3: Margin — how close/far the roll was from the threshold.
 *  Positive = we got lucky (roll was within our zone), negative = we didn't. */
const r3Margin = computed(() => {
  return r3OurChance.value - r3RollPct.value
})

/** Round 3: Did we win the roll? */
const r3WeWon = computed(() => {
  const f = fight.value
  if (!f) return false
  const s = sign.value
  const attackerWon = f.randomNumber <= f.randomForPoint
  return s > 0 ? attackerWon : !attackerWon
})

function compactOutcome(f: FightEntry): string {
  if (f.outcome === 'block') return 'БЛОК'
  if (f.outcome === 'skip') return 'СКИП'
  return f.winnerName ?? ''
}
function compactOutcomeClass(f: FightEntry): string {
  if (f.outcome === 'block' || f.outcome === 'skip') return 'co-neutral'
  return isFightMine(f) ? (
    (f.outcome === 'win' && f.attackerName === myUsername.value) ||
    (f.outcome === 'loss' && f.defenderName === myUsername.value)
      ? 'co-win' : 'co-loss'
  ) : 'co-other'
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
      <button class="fa-tab" :class="{ active: activeTab === 'fights' }" @click="setTab('fights')">Бои раунда</button>
      <button class="fa-tab" :class="{ active: activeTab === 'all' }" @click="setTab('all')">Все бои</button>
      <button class="fa-tab" :class="{ active: activeTab === 'letopis' }" @click="setTab('letopis')">Летопись</button>
    </div>

    <!-- Летопись -->
    <div v-if="activeTab === 'letopis'" class="fa-letopis">
      <div v-if="letopis.trim()" class="fa-letopis-content" v-html="formatLetopis(letopis)" />
      <div v-else class="fa-empty">История пуста</div>
    </div>

    <!-- All Fights (compact results list) -->
    <div v-else-if="activeTab === 'all'" class="fa-all-fights">
      <div v-if="!fights.length" class="fa-empty">Бои еще не начались</div>
      <div v-else class="fa-all-list">
        <div v-for="(f, idx) in fights" :key="idx"
          class="fa-all-row" :class="{ 'is-mine': isFightMine(f) }">
          <img :src="getDisplayAvatar(f.attackerAvatar, f.attackerName)" class="fa-all-ava" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
          <span class="fa-all-name">{{ f.attackerName }}</span>
          <span class="fa-all-vs">vs</span>
          <span class="fa-all-name">{{ f.defenderName }}</span>
          <img :src="getDisplayAvatar(f.defenderAvatar, f.defenderName)" class="fa-all-ava" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
          <span class="fa-all-result" :class="compactOutcomeClass(f)">{{ compactOutcome(f) }}</span>
          <span v-if="f.drops > 0" class="fa-all-drop">DROP</span>
        </div>
      </div>
    </div>

    <!-- Fight animation (replay) -->
    <template v-else>
    <div v-if="!myFights.length" class="fa-empty">Нет ваших боев в этом раунде</div>
    <template v-else>
      <!-- Controls + fight thumbnails -->
      <div class="fa-controls">
        <button class="fa-btn" @click="togglePlay">{{ isPlaying ? '⏸' : '▶' }}</button>
        <button class="fa-btn" @click="restart" title="Restart">⏮</button>
        <button class="fa-btn" @click="skipToEnd" title="Skip to end">⏭</button>
        <div class="fa-speed">
          <button v-for="s in [1, 2, 4]" :key="s" class="fa-speed-btn" :class="{ active: speed === s }" @click="setSpeed(s)">{{ s }}x</button>
        </div>
        <div class="fa-thumbs">
          <button v-for="(f, idx) in myFights" :key="idx" class="fa-thumb"
            :class="{ active: idx === currentFightIdx, 'is-block': f.outcome === 'block', 'is-skip': f.outcome === 'skip' }"
            @click="currentFightIdx = idx; currentStep = totalSteps - 1; skippedToEnd = true; isPlaying = false; clearTimer()"
            :title="`${f.attackerName} vs ${f.defenderName}`">
            <span class="thumb-idx">{{ (idx as number) + 1 }}</span>
          </button>
        </div>
      </div>

      <!-- Fight card -->
      <div v-if="fight" ref="fightCardRef" class="fa-card">
        <!-- Block/Skip -->
        <div v-if="isSpecialOutcome" class="fa-special">
          <!-- Compact identity row for special outcomes -->
          <div class="fa-identity-row">
            <div class="fa-id-left" :class="{ winner: leftWon }">
              <img :src="getDisplayAvatar(leftAvatar, leftName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
              <span class="fa-id-name">{{ leftName }}</span>
            </div>
            <div class="fa-vs-arrow" :class="{ 'arrow-right': attackedRight, 'arrow-left': !attackedRight }">
              {{ attackedRight ? '→' : '←' }}
            </div>
            <div class="fa-id-right">
              <span class="fa-id-name">{{ rightName }}</span>
              <img :src="getDisplayAvatar(rightAvatar, rightName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            </div>
          </div>
          <div class="fa-outcome" :class="outcomeClass(fight)">{{ outcomeLabel(fight) }}</div>
        </div>

        <!-- Normal fight -->
        <template v-else>
          <!-- Compact scale row: avatar+name | bar | avatar+name -->
          <div class="fa-bar-container">
            <div class="fa-id-left" :class="{ winner: leftWon }">
              <img :src="getDisplayAvatar(leftAvatar, leftName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
              <div class="fa-id-info">
                <span class="fa-id-name">{{ leftName }}</span>
                <span class="fa-id-char">{{ getDisplayCharName(leftCharName, leftName) }}</span>
              </div>
              <div class="fa-vs-arrow" :class="{ 'arrow-right': attackedRight, 'arrow-left': !attackedRight }">
                {{ attackedRight ? '→' : '←' }}
              </div>
            </div>
            <div class="fa-bar-track">
              <div class="fa-bar-fill" :style="{ width: barPosition + '%' }" :class="{ 'bar-attacker': animatedWeighingValue > 0, 'bar-defender': animatedWeighingValue < 0, 'bar-even': animatedWeighingValue === 0 }">
                <span v-if="isMyFight" class="fa-bar-value">{{ fmtVal(animatedWeighingValue) }}</span>
              </div>
            </div>
            <div class="fa-id-right" :class="{ winner: !leftWon && (fight.outcome === 'win' || fight.outcome === 'loss') }">
              <div class="fa-id-info" style="text-align:right">
                <span class="fa-id-name">{{ rightName }}</span>
                <span class="fa-id-char">{{ getDisplayCharName(rightCharName, rightName) }}</span>
              </div>
              <img :src="getDisplayAvatar(rightAvatar, rightName)" class="fa-ava-sm" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            </div>
          </div>

          <!-- ═══ MY FIGHT: Full 3-round detail ═══ -->
          <template v-if="isMyFight">
            <!-- ── Round 1: Весы ── -->
            <div class="fa-round-header">Раунд 1 — Весы</div>
            <div class="fa-factors">
              <div v-for="(fac, idx) in round1Factors" :key="'r1-'+idx"
                class="fa-factor" :class="[fac.highlight, { visible: showR1Factor(idx) }]">
                <span class="fa-factor-label">{{ fac.label }}</span>
                <span class="fa-factor-detail" v-html="fac.detail"></span>
                <span class="fa-factor-value" v-if="fac.value !== 0">{{ fmtVal(fac.value) }}</span>
              </div>

              <!-- TooGood / TooStronk badges (flipped labels when we are defender) -->
              <div v-if="fight.isTooGoodMe || fight.isTooGoodEnemy" class="fa-badge-row" :class="{ visible: showR1Result }">
                <span class="fa-badge badge-toogood">TOO GOOD: {{ (fight.isTooGoodMe ? !isFlipped : isFlipped) ? 'МЫ' : 'ВРАГ' }}</span>
              </div>
              <div v-if="fight.isTooStronkMe || fight.isTooStronkEnemy" class="fa-badge-row" :class="{ visible: showR1Result }">
                <span class="fa-badge badge-toostronk">TOO STRONK: {{ (fight.isTooStronkMe ? !isFlipped : isFlipped) ? 'МЫ' : 'ВРАГ' }}</span>
              </div>
            </div>
            <!-- R1 result (flipped) -->
            <div v-if="showR1Result" class="fa-round-result" :class="roundResultClass(fight.round1PointsWon * sign)">
              {{ roundResultLabel(fight.round1PointsWon * sign) }}
            </div>

            <!-- ── Round 2: Справедливость ── -->
            <div class="fa-round-header" :class="{ visible: showR2 }">Раунд 2 — Справедливость</div>
            <div v-if="showR2" class="fa-factor justice visible">
              <span class="fa-factor-label">Справедливость</span>
              <span class="fa-factor-detail">{{ isFlipped ? fight.justiceTarget : fight.justiceMe }} vs {{ isFlipped ? fight.justiceMe : fight.justiceTarget }}</span>
              <span class="fa-factor-value" v-if="fight.pointsFromJustice !== 0">
                {{ fight.pointsFromJustice * sign > 0 ? '+1 очко' : '-1 очко' }}
              </span>
              <span class="fa-factor-value neutral-val" v-else>0</span>
            </div>

            <!-- ── Round 3: Рандом (only if used) ── -->
            <template v-if="fight.usedRandomRoll">
              <div class="fa-round-header" :class="{ visible: showR3 }">Раунд 3 — Рандом</div>
              <div v-if="showR3" class="fa-r3-details">
                <!-- Our base win chance -->
                <div class="fa-factor random visible">
                  <span class="fa-factor-label">Базовый шанс</span>
                  <span class="fa-factor-detail">{{ sign > 0 ? '50.00' : '50.00' }}%</span>
                </div>
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
                  <span class="fa-factor-label">Справедливость</span>
                  <span class="fa-factor-detail" :class="r3ModClass(r3JusticePct)">
                    {{ fmtPct(r3JusticePct) }}
                  </span>
                </div>
                <!-- Our final win chance -->
                <div class="fa-factor random visible">
                  <span class="fa-factor-label">Наш шанс</span>
                  <span class="fa-factor-detail fa-chance-total" :class="r3OurChance >= 50 ? 'pct-good' : 'pct-bad'">
                    {{ r3OurChance.toFixed(2) }}%
                  </span>
                </div>
                <!-- Roll result: show margin (how close/far from winning) -->
                <div class="fa-factor random visible fa-roll-result">
                  <span class="fa-factor-label"><span class="gi gi-rnd">RND</span> Бросок</span>
                  <span class="fa-factor-detail" :class="r3WeWon ? 'pct-good' : 'pct-bad'">
                    {{ fmtPct(r3Margin) }}
                  </span>
                  <span class="fa-factor-value" :class="r3WeWon ? 'good-val' : 'bad-val'">
                    {{ r3WeWon ? '+1' : '-1' }}
                  </span>
                </div>
              </div>
            </template>
          </template>

          <!-- ═══ ENEMY FIGHT: Minimal view ═══ -->
          <template v-else>
            <div v-if="showFinalResult" class="fa-enemy-summary">
              <span v-if="fight.isTooGoodMe || fight.isTooGoodEnemy" class="fa-badge badge-toogood">TOO GOOD</span>
              <span v-if="fight.isTooStronkMe || fight.isTooStronkEnemy" class="fa-badge badge-toostronk">TOO STRONK</span>
            </div>
          </template>

          <!-- ═══ Final result (both own & enemy) ═══ -->
          <div v-if="showFinalResult" class="fa-result">
            <div class="fa-outcome" :class="leftWon ? 'outcome-attacker' : (fight.outcome === 'block' || fight.outcome === 'skip') ? 'outcome-neutral' : 'outcome-defender'">
              {{ fight.outcome === 'block' ? 'БЛОК' : fight.outcome === 'skip' ? 'СКИП' : (leftWon ? 'ПОБЕДА' : 'ПОРАЖЕНИЕ') }}
            </div>
            <div v-if="isMyFight" class="fa-result-details">
              <!-- Skill gained: only show when WE are the attacker -->
              <span v-if="!isFlipped && fight.skillGainedFromTarget > 0" class="fa-detail-item fa-skill-gain">
                +{{ fight.skillGainedFromTarget }} Скилл (Мишень)
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
        </template>
      </div>

    </template>
    </template>
  </div>
</template>

<style scoped>
.fight-animation { display: flex; flex-direction: column; gap: 4px; padding: 4px; }
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

/* ── Controls ── */
.fa-controls { display: flex; align-items: center; gap: 4px; padding: 4px 0; }
.fa-btn { background: var(--bg-surface); border: 1px solid var(--border-subtle); border-radius: var(--radius); padding: 3px 10px; font-size: 13px; cursor: pointer; color: var(--text-primary); transition: all 0.15s; }
.fa-btn:hover { background: var(--accent-blue); color: white; border-color: var(--accent-blue); }
.fa-speed { display: flex; gap: 2px; margin-left: 6px; }
.fa-speed-btn { background: var(--bg-surface); border: 1px solid var(--border-subtle); border-radius: 4px; padding: 2px 8px; font-size: 10px; font-weight: 700; cursor: pointer; color: var(--text-muted); transition: all 0.15s; }
.fa-speed-btn.active { background: var(--kh-c-secondary-purple-500); color: var(--text-primary); border-color: var(--accent-purple); }

/* ── Card ── */
.fa-card { background: var(--bg-inset); border: 1px solid var(--border-subtle); border-radius: var(--radius); padding: 6px 8px; display: flex; flex-direction: column; gap: 5px; }

/* ── Portraits ── */
/* ── Compact identity + scale row ── */
.fa-id-left, .fa-id-right { display: flex; align-items: center; gap: 4px; flex-shrink: 0; }
.fa-id-left.winner .fa-id-name, .fa-id-right.winner .fa-id-name { color: var(--accent-green); font-weight: 800; }
.fa-id-left.winner .fa-ava-sm, .fa-id-right.winner .fa-ava-sm { border-color: var(--accent-green); box-shadow: var(--glow-green); }
.fa-ava-sm { width: 28px; height: 28px; border-radius: 50%; object-fit: cover; border: 2px solid var(--border-subtle); flex-shrink: 0; }
.fa-id-info { display: flex; flex-direction: column; line-height: 1.2; }
.fa-id-name { font-size: 10px; font-weight: 700; color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80px; }
.fa-id-char { font-size: 8px; color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80px; }
.fa-vs-arrow { font-size: 14px; font-weight: 900; flex-shrink: 0; }
.fa-vs-arrow.arrow-right { color: var(--accent-red); }
.fa-vs-arrow.arrow-left { color: var(--accent-blue); }
.fa-identity-row { display: flex; align-items: center; justify-content: center; gap: 10px; padding: 4px 0; }

/* ── Bar ── */
.fa-bar-container { display: flex; align-items: center; gap: 6px; padding: 4px 0; }
.fa-bar-track { flex: 1; height: 20px; background: var(--bg-secondary); border-radius: 10px; overflow: hidden; position: relative; border: 1px solid var(--border-subtle); }
.fa-bar-fill { height: 100%; border-radius: 10px; transition: width 0.5s ease; display: flex; align-items: center; justify-content: flex-end; padding-right: 6px; min-width: 40px; }
.fa-bar-fill.bar-attacker { background: linear-gradient(90deg, var(--kh-c-secondary-success-500), var(--accent-green)); }
.fa-bar-fill.bar-defender { background: linear-gradient(90deg, var(--accent-red-dim), var(--accent-red)); }
.fa-bar-fill.bar-even { background: linear-gradient(90deg, rgba(230, 148, 74, 0.6), var(--accent-orange)); }
.fa-bar-value { font-size: 9px; font-weight: 800; color: white; text-shadow: 0 1px 2px rgba(0,0,0,0.5); font-family: var(--font-mono); }

/* ── Round headers ── */
.fa-round-header { font-size: 10px; font-weight: 800; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; padding: 4px 0 1px; border-bottom: 1px solid var(--border-subtle); opacity: 0; transition: opacity 0.3s; }
.fa-round-header.visible, .fa-factors + .fa-round-header, .fa-round-header:first-of-type { opacity: 1; }
.fa-card > .fa-round-header:first-of-type { opacity: 1; }
div.fa-round-header { opacity: 1; }

/* ── Round result badge ── */
.fa-round-result { text-align: center; font-size: 10px; font-weight: 800; padding: 2px 10px; border-radius: 4px; margin: 2px 0; }
.fa-round-result.round-win { background: rgba(63, 167, 61, 0.1); color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.2); }
.fa-round-result.round-loss { background: rgba(239, 128, 128, 0.08); color: var(--accent-red); border: 1px solid rgba(239, 128, 128, 0.15); }
.fa-round-result.round-draw { background: rgba(230, 148, 74, 0.08); color: var(--accent-orange); border: 1px solid rgba(230, 148, 74, 0.15); }

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
.fa-factor-detail { color: var(--text-secondary); flex: 1; font-size: 10px; }
.fa-factor-value { font-weight: 800; color: var(--accent-gold); margin-left: auto; font-family: var(--font-mono); font-size: 11px; }
.fa-factor-value.good-val { color: var(--accent-green); }
.fa-factor-value.bad-val { color: var(--accent-red); }
.neutral-val { color: var(--text-muted); }

/* ── Badges (TooGood / TooStronk) ── */
.fa-badge-row { display: flex; justify-content: center; padding: 2px 0; opacity: 0; transition: opacity 0.3s; }
.fa-badge-row.visible { opacity: 1; }
.fa-badge { font-size: 9px; font-weight: 900; padding: 2px 10px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.5px; }
.badge-toogood { background: rgba(63, 167, 61, 0.1); color: var(--accent-green); border: 1px solid rgba(63, 167, 61, 0.3); }
.badge-toostronk { background: rgba(180, 150, 255, 0.1); color: var(--accent-purple); border: 1px solid rgba(180, 150, 255, 0.3); }

/* ── R3 details ── */
.fa-r3-details { display: flex; flex-direction: column; gap: 2px; }
.pct-good { color: var(--accent-green); font-weight: 700; }
.pct-bad { color: var(--accent-red); font-weight: 700; }
.fa-chance-total { font-size: 13px; font-weight: 800; }
.fa-roll-result { border-top: 1px solid var(--border-subtle); padding-top: 4px; margin-top: 2px; }

/* ── Enemy summary ── */
.fa-enemy-summary { display: flex; gap: 6px; justify-content: center; padding: 4px 0; }

/* ── Result ── */
.fa-result { display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 6px 0 4px; }
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
.fa-special { display: flex; justify-content: center; padding: 12px 0; }

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

/* ── All Fights list ── */
.fa-all-fights { flex: 1; overflow-y: auto; }
.fa-all-list { display: flex; flex-direction: column; gap: 3px; }
.fa-all-row { display: flex; align-items: center; gap: 8px; padding: 6px 10px; border-radius: var(--radius); background: var(--bg-inset); font-size: 13px; border: 1px solid transparent; }
.fa-all-row.is-mine { background: rgba(180, 150, 255, 0.05); border-color: rgba(180, 150, 255, 0.15); }
.fa-all-ava { width: 32px; height: 32px; border-radius: 50%; object-fit: cover; flex-shrink: 0; border: 1px solid var(--border-subtle); }
.fa-all-name { font-weight: 700; color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 100px; font-size: 12px; }
.fa-all-vs { color: var(--text-dim); font-size: 11px; flex-shrink: 0; font-weight: 700; }
.fa-all-result { margin-left: auto; font-weight: 800; font-size: 11px; padding: 2px 8px; border-radius: 4px; white-space: nowrap; }
.fa-all-result.co-win { color: var(--accent-green); background: rgba(63, 167, 61, 0.08); }
.fa-all-result.co-loss { color: var(--accent-red); background: rgba(239, 128, 128, 0.06); }
.fa-all-result.co-neutral { color: var(--accent-orange); background: rgba(230, 148, 74, 0.06); }
.fa-all-result.co-other { color: var(--text-muted); }
.fa-all-drop { font-size: 9px; font-weight: 900; color: white; background: var(--accent-red-dim); padding: 2px 6px; border-radius: 4px; line-height: 16px; flex-shrink: 0; border: 1px solid var(--accent-red); }

/* ── Летопись ── */
.fa-letopis { flex: 1; overflow-y: auto; padding: 4px; background: var(--bg-inset); border-radius: var(--radius); border: 1px solid var(--border-subtle); }
.fa-letopis-content { font-size: 11px; line-height: 1.6; color: var(--text-secondary); font-family: var(--font-mono); }
.fa-letopis-content :deep(strong) { color: var(--accent-gold); }
.fa-letopis-content :deep(em) { color: var(--accent-blue); }
.fa-letopis-content :deep(u) { color: var(--accent-green); }
.fa-letopis-content :deep(del) { color: var(--text-muted); text-decoration: line-through; }
</style>
