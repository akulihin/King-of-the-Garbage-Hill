<script setup lang="ts">
import { ref, computed, watch, onUnmounted } from 'vue'
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

/** Active tab: 'fights' = replay, 'all' = compact results list, 'letopis' = full text log */
const activeTab = ref<'fights' | 'all' | 'letopis'>('fights')

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
  const myPlayer = props.players.find((p: Player) => p.playerId === props.myPlayerId)
  if (!myPlayer) return false
  return fight.value.attackerName === myPlayer.discordUsername ||
         fight.value.defenderName === myPlayer.discordUsername
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
  const list: Factor[] = []

  // 1. Contre
  if (f.contrWeighingDelta !== 0) {
    list.push({
      label: 'Контра',
      detail: f.isContrMe && f.isContrTarget
        ? 'Обоюдная контра (⚔️)'
        : f.isContrMe
          ? `Атакующий контрит (✅) (x${f.contrMultiplier})`
          : 'Защищающийся контрит (❌)',
      value: f.contrWeighingDelta,
      highlight: f.contrWeighingDelta > 0 ? 'good' : 'bad',
    })
  }

  // 2. Scale (stats + skill*multiplier)
  list.push({
    label: 'Статы',
    detail: `${f.scaleMe.toFixed(1)} vs ${f.scaleTarget.toFixed(1)}`,
    value: f.scaleWeighingDelta,
    highlight: f.scaleWeighingDelta > 0 ? 'good' : f.scaleWeighingDelta < 0 ? 'bad' : 'neutral',
  })

  // 3. WhoIsBetter
  if (f.whoIsBetterWeighingDelta !== 0) {
    list.push({
      label: 'Кто сильнее',
      detail: f.whoIsBetterWeighingDelta > 0 ? 'Атакующий сильнее' : 'Защищающийся сильнее',
      value: f.whoIsBetterWeighingDelta,
      highlight: f.whoIsBetterWeighingDelta > 0 ? 'good' : 'bad',
    })
  }

  // 4. Psyche
  if (f.psycheWeighingDelta !== 0) {
    list.push({
      label: 'Психика',
      detail: `${f.psycheDifference > 0 ? '✅' : '❌'}`,
      value: f.psycheWeighingDelta,
      highlight: f.psycheWeighingDelta > 0 ? 'good' : 'bad',
    })
  }

  // 5. Skill difference
  if (f.skillWeighingDelta !== 0) {
    list.push({
      label: 'Навык',
      detail: `Skill x${f.skillMultiplierMe} vs x${f.skillMultiplierTarget}`,
      value: f.skillWeighingDelta,
      highlight: f.skillWeighingDelta > 0 ? 'good' : 'bad',
    })
  }

  // 6. Justice in weighing
  if (f.justiceWeighingDelta !== 0) {
    list.push({
      label: 'Справедливость',
      detail: `${f.justiceMe} vs ${f.justiceTarget}`,
      value: f.justiceWeighingDelta,
      highlight: f.justiceWeighingDelta > 0 ? 'good' : 'bad',
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
const animatedWeighingValue = computed(() => {
  if (!fight.value || isSpecialOutcome.value) return 0
  if (skippedToEnd.value || !isMyFight.value) return fight.value.weighingMachine

  const factorCount = round1Factors.value.length
  let accumulated = 0
  const factorStepsShown = Math.min(currentStep.value, factorCount)
  for (let i = 0; i < factorStepsShown; i++) {
    accumulated += round1Factors.value[i].value
  }
  if (currentStep.value > factorCount) {
    accumulated = fight.value.weighingMachine
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
  if (fp !== lastAnimatedRound.value) { lastAnimatedRound.value = fp; restart() }
}, { deep: true })

onUnmounted(() => { clearTimer() })

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
      <button class="fa-tab" :class="{ active: activeTab === 'fights' }" @click="activeTab = 'fights'">Бои раунда</button>
      <button class="fa-tab" :class="{ active: activeTab === 'all' }" @click="activeTab = 'all'">Все бои</button>
      <button class="fa-tab" :class="{ active: activeTab === 'letopis' }" @click="activeTab = 'letopis'">Летопись</button>
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
      <!-- Controls -->
      <div class="fa-controls">
        <button class="fa-btn" @click="togglePlay">{{ isPlaying ? '⏸' : '▶' }}</button>
        <button class="fa-btn" @click="restart" title="Restart">⏮</button>
        <button class="fa-btn" @click="skipToEnd" title="Skip to end">⏭</button>
        <div class="fa-speed">
          <button v-for="s in [1, 2, 4]" :key="s" class="fa-speed-btn" :class="{ active: speed === s }" @click="setSpeed(s)">{{ s }}x</button>
        </div>
        <span class="fa-progress">{{ currentFightIdx + 1 }} / {{ myFights.length }}</span>
      </div>

      <!-- Fight card -->
      <div v-if="fight" class="fa-card">
        <!-- Portraits -->
        <div class="fa-portraits">
          <div class="fa-portrait" :class="{ winner: fight.outcome === 'win' }">
            <img :src="getDisplayAvatar(fight.attackerAvatar, fight.attackerName)" :alt="getDisplayCharName(fight.attackerCharName, fight.attackerName)" class="fa-avatar" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            <div class="fa-name">{{ fight.attackerName }}</div>
            <div class="fa-char">{{ getDisplayCharName(fight.attackerCharName, fight.attackerName) }}</div>
          </div>
          <div class="fa-vs">VS</div>
          <div class="fa-portrait" :class="{ winner: fight.outcome === 'loss' }">
            <img :src="getDisplayAvatar(fight.defenderAvatar, fight.defenderName)" :alt="getDisplayCharName(fight.defenderCharName, fight.defenderName)" class="fa-avatar" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            <div class="fa-name">{{ fight.defenderName }}</div>
            <div class="fa-char">{{ getDisplayCharName(fight.defenderCharName, fight.defenderName) }}</div>
          </div>
        </div>

        <!-- Block/Skip -->
        <div v-if="isSpecialOutcome" class="fa-special">
          <div class="fa-outcome" :class="outcomeClass(fight)">{{ outcomeLabel(fight) }}</div>
        </div>

        <!-- Normal fight -->
        <template v-else>
          <!-- Weighing Machine Bar -->
          <div class="fa-bar-container">
            <span class="fa-bar-label left">ATK</span>
            <div class="fa-bar-track">
              <div class="fa-bar-fill" :style="{ width: barPosition + '%' }" :class="{ 'bar-attacker': animatedWeighingValue > 0, 'bar-defender': animatedWeighingValue < 0, 'bar-even': animatedWeighingValue === 0 }">
                <span v-if="isMyFight" class="fa-bar-value">{{ fmtVal(animatedWeighingValue) }}</span>
              </div>
            </div>
            <span class="fa-bar-label right">DEF</span>
          </div>

          <!-- ═══ MY FIGHT: Full 3-round detail ═══ -->
          <template v-if="isMyFight">
            <!-- ── Round 1: Весы ── -->
            <div class="fa-round-header">Раунд 1 — Весы</div>
            <div class="fa-factors">
              <div v-for="(fac, idx) in round1Factors" :key="'r1-'+idx"
                class="fa-factor" :class="[fac.highlight, { visible: showR1Factor(idx) }]">
                <span class="fa-factor-label">{{ fac.label }}</span>
                <span class="fa-factor-detail">{{ fac.detail }}</span>
                <span class="fa-factor-value" v-if="fac.value !== 0">{{ fmtVal(fac.value) }}</span>
              </div>

              <!-- TooGood / TooStronk badges -->
              <div v-if="fight.isTooGoodMe || fight.isTooGoodEnemy" class="fa-badge-row" :class="{ visible: showR1Result }">
                <span class="fa-badge badge-toogood">TOO GOOD: {{ fight.isTooGoodMe ? 'ATK' : 'DEF' }}</span>
              </div>
              <div v-if="fight.isTooStronkMe || fight.isTooStronkEnemy" class="fa-badge-row" :class="{ visible: showR1Result }">
                <span class="fa-badge badge-toostronk">TOO STRONK: {{ fight.isTooStronkMe ? 'ATK' : 'DEF' }}</span>
              </div>
            </div>
            <!-- R1 result -->
            <div v-if="showR1Result" class="fa-round-result" :class="roundResultClass(fight.round1PointsWon)">
              {{ roundResultLabel(fight.round1PointsWon) }}
            </div>

            <!-- ── Round 2: Справедливость ── -->
            <div class="fa-round-header" :class="{ visible: showR2 }">Раунд 2 — Справедливость</div>
            <div v-if="showR2" class="fa-factor justice visible">
              <span class="fa-factor-label">Справедливость</span>
              <span class="fa-factor-detail">{{ fight.justiceMe }} vs {{ fight.justiceTarget }}</span>
              <span class="fa-factor-value" v-if="fight.pointsFromJustice !== 0">
                {{ fight.pointsFromJustice > 0 ? '+1 очко' : '-1 очко' }}
              </span>
              <span class="fa-factor-value neutral-val" v-else>0</span>
            </div>

            <!-- ── Round 3: Рандом (only if used) ── -->
            <template v-if="fight.usedRandomRoll">
              <div class="fa-round-header" :class="{ visible: showR3 }">Раунд 3 — Рандом</div>
              <div v-if="showR3" class="fa-r3-details">
                <!-- Random modifiers -->
                <div v-if="fight.tooGoodRandomChange !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">TooGood</span>
                  <span class="fa-factor-detail">Порог: {{ fmtVal(fight.tooGoodRandomChange) }}</span>
                </div>
                <div v-if="fight.tooStronkRandomChange !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">TooStronk</span>
                  <span class="fa-factor-detail">Порог: {{ fmtVal(fight.tooStronkRandomChange) }}</span>
                </div>
                <div v-if="fight.justiceRandomChange !== 0" class="fa-factor random visible">
                  <span class="fa-factor-label">Справедливость</span>
                  <span class="fa-factor-detail">Макс. рандом: {{ fmtVal(-fight.justiceRandomChange) }}</span>
                </div>
                <!-- Roll result -->
                <div class="fa-factor random visible">
                  <span class="fa-factor-label">🎲 Бросок</span>
                  <span class="fa-factor-detail">
                    {{ fight.randomNumber }} / {{ fight.maxRandomNumber.toFixed(0) }}
                    (порог: {{ fight.randomForPoint.toFixed(0) }})
                  </span>
                  <span class="fa-factor-value">
                    {{ fight.randomNumber <= fight.randomForPoint ? '+1' : '-1' }}
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
            <div class="fa-outcome" :class="outcomeClass(fight)">{{ outcomeLabel(fight) }}</div>
            <div v-if="isMyFight" class="fa-result-details">
              <!-- Moral: only when attacker won (outcome=win) -->
              <span v-if="fight.outcome === 'win' && fight.moralChange !== 0" class="fa-detail-item fa-moral">
                Мораль: {{ fight.outcome === 'win' ? 'DEF' : 'ATK' }} {{ fight.moralChange > 0 ? '+' : '' }}{{ fight.moralChange }}
              </span>
              <!-- Justice change: loser gains justice -->
              <span v-if="fight.justiceChange > 0" class="fa-detail-item fa-justice">
                Справедливость: {{ fight.outcome === 'win' ? 'DEF' : 'ATK' }} +{{ fight.justiceChange }} Справедливость
              </span>
              <!-- Quality damage details -->
              <template v-if="fight.qualityDamageApplied">
                <span v-if="fight.resistIntelDamage > 0" class="fa-detail-item fa-resist">🧠 -{{ fight.resistIntelDamage }}</span>
                <span v-if="fight.resistStrDamage > 0" class="fa-detail-item fa-resist">💪 -{{ fight.resistStrDamage }}</span>
                <span v-if="fight.resistPsycheDamage > 0" class="fa-detail-item fa-resist">🧘 -{{ fight.resistPsycheDamage }}</span>
                <!-- Intellectual damage: intel resist broke -->
                <span v-if="fight.intellectualDamage" class="fa-detail-item fa-intellectual">
                  Intellectual Damage!
                </span>
                <!-- Emotional damage: psyche resist broke -->
                <span v-if="fight.emotionalDamage" class="fa-detail-item fa-emotional">
                  Emotional Damage!
                </span>
              </template>
              <span v-if="fight.drops > 0" class="fa-detail-item fa-drop">
                DROP x{{ fight.drops }}!
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

      <!-- Fight thumbnails -->
      <div class="fa-thumbs">
        <button v-for="(f, idx) in myFights" :key="idx" class="fa-thumb"
          :class="{ active: idx === currentFightIdx, 'is-block': f.outcome === 'block', 'is-skip': f.outcome === 'skip' }"
          @click="currentFightIdx = idx; currentStep = totalSteps - 1; skippedToEnd = true; isPlaying = false; clearTimer()"
          :title="`${f.attackerName} vs ${f.defenderName}`">
          <span class="thumb-idx">{{ idx + 1 }}</span>
        </button>
      </div>
    </template>
    </template>
  </div>
</template>

<style scoped>
.fight-animation { display: flex; flex-direction: column; gap: 4px; padding: 4px; overflow-y: auto; }
.fa-empty { color: var(--text-muted); font-style: italic; padding: 12px; text-align: center; font-size: 13px; }

/* ── Controls ── */
.fa-controls { display: flex; align-items: center; gap: 4px; padding: 4px 0; }
.fa-btn { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 6px; padding: 3px 10px; font-size: 14px; cursor: pointer; color: var(--text-primary); transition: background 0.15s; }
.fa-btn:hover { background: var(--accent-blue); color: white; }
.fa-speed { display: flex; gap: 2px; margin-left: 6px; }
.fa-speed-btn { background: var(--bg-secondary); border: 1px solid var(--border); border-radius: 4px; padding: 2px 8px; font-size: 11px; cursor: pointer; color: var(--text-secondary); transition: all 0.15s; }
.fa-speed-btn.active { background: var(--accent-purple); color: white; border-color: var(--accent-purple); }
.fa-progress { margin-left: auto; font-size: 12px; color: var(--text-muted); font-weight: 600; }

/* ── Card ── */
.fa-card { background: var(--bg-primary); border-radius: var(--radius); padding: 6px 8px; display: flex; flex-direction: column; gap: 5px; }

/* ── Portraits ── */
.fa-portraits { display: flex; align-items: center; justify-content: center; gap: 12px; }
.fa-portrait { display: flex; flex-direction: column; align-items: center; gap: 3px; opacity: 0.7; transition: all 0.3s; }
.fa-portrait.winner { opacity: 1; transform: scale(1.05); }
.fa-portrait.winner .fa-name { color: var(--accent-green); font-weight: 700; }
.fa-avatar { width: 48px; height: 48px; border-radius: 50%; object-fit: cover; border: 2px solid var(--border); }
.fa-portrait.winner .fa-avatar { border-color: var(--accent-green); box-shadow: 0 0 8px rgba(72, 199, 142, 0.4); }
.fa-name { font-size: 12px; font-weight: 600; color: var(--text-primary); }
.fa-char { font-size: 10px; color: var(--text-muted); }
.fa-vs { font-size: 18px; font-weight: 900; color: var(--accent-red); text-shadow: 0 0 6px rgba(255, 82, 82, 0.3); }

/* ── Bar ── */
.fa-bar-container { display: flex; align-items: center; gap: 6px; padding: 4px 0; }
.fa-bar-label { font-size: 10px; font-weight: 700; color: var(--text-muted); width: 28px; text-align: center; flex-shrink: 0; }
.fa-bar-track { flex: 1; height: 22px; background: var(--bg-secondary); border-radius: 11px; overflow: hidden; position: relative; }
.fa-bar-fill { height: 100%; border-radius: 11px; transition: width 0.5s ease; display: flex; align-items: center; justify-content: flex-end; padding-right: 6px; min-width: 40px; }
.fa-bar-fill.bar-attacker { background: linear-gradient(90deg, var(--accent-green), #48c78e); }
.fa-bar-fill.bar-defender { background: linear-gradient(90deg, var(--accent-red), #ff5252); }
.fa-bar-fill.bar-even { background: linear-gradient(90deg, var(--accent-orange), #ffb347); }
.fa-bar-value { font-size: 10px; font-weight: 700; color: white; text-shadow: 0 1px 2px rgba(0,0,0,0.3); }

/* ── Round headers ── */
.fa-round-header { font-size: 11px; font-weight: 800; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; padding: 4px 0 1px; border-bottom: 1px solid var(--border); opacity: 0; transition: opacity 0.3s; }
.fa-round-header.visible, .fa-factors + .fa-round-header, .fa-round-header:first-of-type { opacity: 1; }
/* Always show the first round header */
.fa-card > .fa-round-header:first-of-type { opacity: 1; }
div.fa-round-header { opacity: 1; }

/* ── Round result badge ── */
.fa-round-result { text-align: center; font-size: 11px; font-weight: 800; padding: 2px 10px; border-radius: 4px; margin: 2px 0; }
.fa-round-result.round-win { background: rgba(72, 199, 142, 0.15); color: var(--accent-green); }
.fa-round-result.round-loss { background: rgba(255, 82, 82, 0.15); color: var(--accent-red); }
.fa-round-result.round-draw { background: rgba(251, 191, 36, 0.15); color: var(--accent-orange); }

/* ── Factors ── */
.fa-factors { display: flex; flex-direction: column; gap: 3px; }
.fa-factor { display: flex; align-items: center; gap: 6px; padding: 3px 8px; border-radius: 4px; font-size: 11px; background: var(--bg-secondary); opacity: 0; transform: translateY(4px); transition: opacity 0.3s, transform 0.3s; }
.fa-factor.visible { opacity: 1; transform: translateY(0); }
.fa-factor.good { border-left: 3px solid var(--accent-green); }
.fa-factor.bad { border-left: 3px solid var(--accent-red); }
.fa-factor.neutral { border-left: 3px solid var(--accent-orange); }
.fa-factor.justice { border-left: 3px solid var(--accent-purple); }
.fa-factor.random { border-left: 3px solid var(--accent-blue); }
.fa-factor-label { font-weight: 700; color: var(--text-primary); min-width: 100px; }
.fa-factor-detail { color: var(--text-secondary); flex: 1; }
.fa-factor-value { font-weight: 700; color: var(--accent-gold); margin-left: auto; }
.neutral-val { color: var(--text-muted); }

/* ── Badges (TooGood / TooStronk) ── */
.fa-badge-row { display: flex; justify-content: center; padding: 2px 0; opacity: 0; transition: opacity 0.3s; }
.fa-badge-row.visible { opacity: 1; }
.fa-badge { font-size: 10px; font-weight: 900; padding: 2px 10px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.5px; }
.badge-toogood { background: rgba(72, 199, 142, 0.2); color: var(--accent-green); border: 1px solid var(--accent-green); }
.badge-toostronk { background: rgba(167, 139, 250, 0.2); color: var(--accent-purple); border: 1px solid var(--accent-purple); }

/* ── R3 details ── */
.fa-r3-details { display: flex; flex-direction: column; gap: 3px; }

/* ── Enemy summary ── */
.fa-enemy-summary { display: flex; gap: 6px; justify-content: center; padding: 4px 0; }

/* ── Result ── */
.fa-result { display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 6px 0 4px; }
.fa-outcome { font-size: 14px; font-weight: 900; text-transform: uppercase; padding: 4px 16px; border-radius: 8px; }
.outcome-attacker { background: var(--accent-green); color: white; }
.outcome-defender { background: var(--accent-red); color: white; }
.outcome-neutral { background: var(--accent-orange); color: white; }

.fa-result-details { display: flex; flex-wrap: wrap; gap: 6px; justify-content: center; font-size: 11px; padding-top: 2px; }
.fa-detail-item { padding: 1px 8px; border-radius: 4px; background: var(--bg-secondary); }
.fa-moral { color: var(--accent-purple); }
.fa-justice { color: var(--accent-blue); }
.fa-resist { color: var(--accent-orange); }
.fa-intellectual { color: var(--accent-blue); font-weight: 700; font-style: italic; }
.fa-emotional { color: var(--accent-purple); font-weight: 700; font-style: italic; }
.fa-drop { color: white; background: var(--accent-red) !important; font-weight: 700; }

/* ── Special (block/skip) ── */
.fa-special { display: flex; justify-content: center; padding: 12px 0; }

/* ── Thumbnails ── */
.fa-thumbs { display: flex; gap: 3px; flex-wrap: wrap; padding-top: 4px; }
.fa-thumb { width: 26px; height: 26px; border-radius: 6px; border: 1px solid var(--border); background: var(--bg-secondary); cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 10px; font-weight: 700; color: var(--text-secondary); transition: all 0.15s; }
.fa-thumb:hover { border-color: var(--accent-blue); }
.fa-thumb.active { background: var(--accent-blue); color: white; border-color: var(--accent-blue); }
.fa-thumb.is-block { border-color: var(--accent-orange); }
.fa-thumb.is-skip { border-color: var(--accent-red); opacity: 0.6; }

/* ── Tabs ── */
.fa-tab-header { display: flex; gap: 2px; background: var(--bg-secondary); border-radius: 8px; padding: 2px; }
.fa-tab { flex: 1; padding: 5px 12px; border: none; border-radius: 6px; background: transparent; color: var(--text-muted); font-size: 12px; font-weight: 600; cursor: pointer; transition: all 0.15s; }
.fa-tab:hover { color: var(--text-primary); }
.fa-tab.active { background: var(--bg-card); color: var(--text-primary); box-shadow: 0 1px 3px rgba(0,0,0,0.2); }

/* ── All Fights compact list ── */
.fa-all-fights { flex: 1; overflow-y: auto; }
.fa-all-list { display: flex; flex-direction: column; gap: 2px; }
.fa-all-row { display: flex; align-items: center; gap: 6px; padding: 3px 6px; border-radius: 4px; background: var(--bg-primary); font-size: 11px; }
.fa-all-row.is-mine { background: rgba(99, 102, 241, 0.08); border-left: 2px solid var(--accent-purple); }
.fa-all-ava { width: 22px; height: 22px; border-radius: 50%; object-fit: cover; flex-shrink: 0; }
.fa-all-name { font-weight: 600; color: var(--text-primary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80px; }
.fa-all-vs { color: var(--text-muted); font-size: 10px; flex-shrink: 0; }
.fa-all-result { margin-left: auto; font-weight: 700; font-size: 10px; padding: 1px 6px; border-radius: 4px; white-space: nowrap; }
.fa-all-result.co-win { color: var(--accent-green); }
.fa-all-result.co-loss { color: var(--accent-red); }
.fa-all-result.co-neutral { color: var(--accent-orange); }
.fa-all-result.co-other { color: var(--text-secondary); }
.fa-all-drop { font-size: 9px; font-weight: 900; color: white; background: var(--accent-red); padding: 0 4px; border-radius: 3px; line-height: 14px; flex-shrink: 0; }

/* ── Летопись ── */
.fa-letopis { flex: 1; overflow-y: auto; padding: 4px; background: var(--bg-primary); border-radius: var(--radius); }
.fa-letopis-content { font-size: 12px; line-height: 1.6; color: var(--text-secondary); font-family: var(--font-mono); }
.fa-letopis-content :deep(strong) { color: var(--accent-gold); }
.fa-letopis-content :deep(em) { color: var(--accent-blue); }
.fa-letopis-content :deep(u) { color: var(--accent-green); }
.fa-letopis-content :deep(del) { color: var(--text-muted); text-decoration: line-through; }
</style>
