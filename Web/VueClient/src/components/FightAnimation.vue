<script setup lang="ts">
import { ref, computed, watch, onUnmounted } from 'vue'
import type { FightEntry } from 'src/services/signalr'

const props = withDefaults(defineProps<{
  fights: FightEntry[]
  /** Full game history text (Летопись) shown as an alternative view */
  letopis?: string
}>(), {
  letopis: '',
})

/** Toggle between fight animation and Летопись text */
const showLetopis = ref(false)

// ── Playback state ──────────────────────────────────────────────────
const currentFightIdx = ref(0)
const currentStep = ref(0) // 0=intro, 1..N=factors, N+1=justice, N+2=random, N+3=result
const isPlaying = ref(true)
const speed = ref(1)
const skippedToEnd = ref(false)

// last round number we animated — used to detect new rounds and restart
const lastAnimatedRound = ref<string>('')

let timer: ReturnType<typeof setTimeout> | null = null

const fight = computed<FightEntry | null>(() => {
  if (!props.fights.length) return null
  if (currentFightIdx.value >= props.fights.length) return null
  return props.fights[currentFightIdx.value]
})

const isSpecialOutcome = computed(() => {
  if (!fight.value) return false
  return fight.value.outcome === 'block' || fight.value.outcome === 'skip'
})

// ── Factor list for step-by-step animation ──────────────────────────
type Factor = {
  label: string
  detail: string
  value: number // contribution to weighing machine
  highlight?: 'good' | 'bad' | 'neutral'
}

const factors = computed<Factor[]>(() => {
  const f = fight.value
  if (!f || isSpecialOutcome.value) return []

  const list: Factor[] = []

  // Scale (base stats)
  const scaleDiff = f.scaleMe - f.scaleTarget
  list.push({
    label: 'Статы (Scale)',
    detail: `${f.scaleMe.toFixed(1)} vs ${f.scaleTarget.toFixed(1)}`,
    value: scaleDiff,
    highlight: scaleDiff > 0 ? 'good' : scaleDiff < 0 ? 'bad' : 'neutral',
  })

  // Counter
  if (f.isContrMe || f.isContrTarget) {
    const contrVal = (f.isContrMe ? 2 : 0) - (f.isContrTarget ? 2 : 0)
    list.push({
      label: 'Контра',
      detail: f.isContrMe && f.isContrTarget
        ? 'Обоюдная контра'
        : f.isContrMe
          ? `Атакующий контрит (x${f.contrMultiplier})`
          : `Защищающийся контрит`,
      value: contrVal,
      highlight: contrVal > 0 ? 'good' : contrVal < 0 ? 'bad' : 'neutral',
    })
  }

  // Psyche difference (contributes via scale, shown as info)
  if (f.psycheDifference !== 0) {
    list.push({
      label: 'Психика',
      detail: `Разница: ${f.psycheDifference > 0 ? '+' : ''}${f.psycheDifference}`,
      value: f.psycheDifference,
      highlight: f.psycheDifference > 0 ? 'good' : 'bad',
    })
  }

  // TooGood / TooStronk
  if (f.isTooGoodMe) {
    list.push({ label: 'Too Good', detail: 'Атакующий TOO GOOD', value: 0, highlight: 'good' })
  }
  if (f.isTooGoodEnemy) {
    list.push({ label: 'Too Good', detail: 'Защищающийся TOO GOOD', value: 0, highlight: 'bad' })
  }
  if (f.isTooStronkMe) {
    list.push({ label: 'Too Stronk', detail: 'Атакующий TOO STRONK', value: 0, highlight: 'good' })
  }
  if (f.isTooStronkEnemy) {
    list.push({ label: 'Too Stronk', detail: 'Защищающийся TOO STRONK', value: 0, highlight: 'bad' })
  }

  // IsStatsBetter
  if (f.isStatsBetterMe) {
    list.push({ label: 'Сильнее по статам', detail: 'Весы в пользу атакующего', value: 0, highlight: 'good' })
  }
  if (f.isStatsBetterEnemy) {
    list.push({ label: 'Сильнее по статам', detail: 'Весы в пользу защищающегося', value: 0, highlight: 'bad' })
  }

  // Skill multiplier
  if (f.skillMultiplierMe > 1 || f.skillMultiplierTarget > 1) {
    list.push({
      label: 'Навык (Skill)',
      detail: `x${f.skillMultiplierMe} vs x${f.skillMultiplierTarget}`,
      value: 0,
      highlight: f.skillMultiplierMe > f.skillMultiplierTarget ? 'good' : 'bad',
    })
  }

  return list
})

const totalSteps = computed(() => {
  if (isSpecialOutcome.value) return 2 // intro + result
  // intro + each factor + justice + (random?) + result
  let steps = 1 + factors.value.length + 1 + 1 // intro, factors, justice, result
  if (fight.value?.usedRandomRoll) steps += 1
  return steps
})

// ── Weighing machine animation ──────────────────────────────────────
// Accumulate factors up to current step for the bar position
const animatedWeighingValue = computed(() => {
  if (!fight.value || isSpecialOutcome.value) return 0
  if (skippedToEnd.value) return fight.value.weighingMachine

  // Steps: 0=intro (0), 1..N=factors (cumulative), N+1=justice, N+2=random, N+3=result
  const factorCount = factors.value.length
  let accumulated = 0

  // Accumulate factor values up to current step
  const factorStepsShown = Math.min(currentStep.value, factorCount)
  for (let i = 0; i < factorStepsShown; i++) {
    accumulated += factors.value[i].value
  }

  // After all factors, show full weighing machine value
  if (currentStep.value > factorCount) {
    accumulated = fight.value.weighingMachine
  }

  return accumulated
})

// Normalize bar position: 0% = full attacker win, 100% = full defender win, 50% = even
const barPosition = computed(() => {
  const val = animatedWeighingValue.value
  // Clamp to [-50, 50] range and map to [0, 100]
  const clamped = Math.max(-50, Math.min(50, val))
  return 50 + (clamped / 50) * 50
})

// ── Visibility helpers ──────────────────────────────────────────────
const showFactor = (idx: number) => {
  if (skippedToEnd.value) return true
  return currentStep.value > idx // factor at index 0 is shown at step 1
}

const showJustice = computed(() => {
  if (skippedToEnd.value) return true
  return currentStep.value > factors.value.length
})

const showRandom = computed(() => {
  if (!fight.value?.usedRandomRoll) return false
  if (skippedToEnd.value) return true
  return currentStep.value > factors.value.length + 1
})

const showResult = computed(() => {
  if (skippedToEnd.value) return true
  return currentStep.value >= totalSteps.value - 1
})

// ── Playback control ────────────────────────────────────────────────
function clearTimer() {
  if (timer) {
    clearTimeout(timer)
    timer = null
  }
}

function stepDelay(): number {
  return 800 / speed.value
}

function advanceStep() {
  if (!isPlaying.value) return
  if (!fight.value) return

  if (currentStep.value < totalSteps.value - 1) {
    currentStep.value++
    scheduleNext()
  } else {
    // Fight done, move to next fight after a longer pause
    setTimeout(() => {
      if (!isPlaying.value) return
      if (currentFightIdx.value < props.fights.length - 1) {
        currentFightIdx.value++
        currentStep.value = 0
        scheduleNext()
      } else {
        isPlaying.value = false
      }
    }, 1500 / speed.value)
  }
}

function scheduleNext() {
  clearTimer()
  timer = setTimeout(advanceStep, stepDelay())
}

function togglePlay() {
  isPlaying.value = !isPlaying.value
  if (isPlaying.value) {
    skippedToEnd.value = false
    scheduleNext()
  } else {
    clearTimer()
  }
}

function skipToEnd() {
  clearTimer()
  isPlaying.value = false
  skippedToEnd.value = true
  currentFightIdx.value = props.fights.length - 1
  currentStep.value = totalSteps.value - 1
}

function setSpeed(s: number) {
  speed.value = s
}

function restart() {
  clearTimer()
  currentFightIdx.value = 0
  currentStep.value = 0
  skippedToEnd.value = false
  isPlaying.value = true
  scheduleNext()
}

// ── Watch for new fight data (new round) ────────────────────────────
watch(
  () => props.fights,
  (newFights: FightEntry[]) => {
    if (!newFights.length) return
    // Build a fingerprint to detect if this is genuinely new data
    const fingerprint = newFights.map(f => `${f.attackerName}-${f.defenderName}`).join('|')
    if (fingerprint !== lastAnimatedRound.value) {
      lastAnimatedRound.value = fingerprint
      restart()
    }
  },
  { deep: true }
)

onUnmounted(() => {
  clearTimer()
})

// ── Helpers ─────────────────────────────────────────────────────────
function outcomeLabel(f: FightEntry): string {
  switch (f.outcome) {
    case 'block': return 'БЛОК'
    case 'skip': return 'СКИП'
    case 'win': return `${f.winnerName} ПОБЕДИЛ`
    case 'loss': return `${f.winnerName} ПОБЕДИЛ`
    default: return ''
  }
}

function outcomeClass(f: FightEntry): string {
  if (f.outcome === 'block' || f.outcome === 'skip') return 'outcome-neutral'
  if (f.outcome === 'win') return 'outcome-attacker'
  return 'outcome-defender'
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
</script>

<template>
  <div class="fight-animation">
    <!-- Tab header: Бои раунда / Летопись -->
    <div class="fa-tab-header">
      <button
        class="fa-tab"
        :class="{ active: !showLetopis }"
        @click="showLetopis = false"
      >
        Бои раунда
      </button>
      <button
        class="fa-tab"
        :class="{ active: showLetopis }"
        @click="showLetopis = true"
      >
        Летопись
      </button>
    </div>

    <!-- Летопись (full text history) -->
    <div v-if="showLetopis" class="fa-letopis">
      <div v-if="letopis.trim()" class="fa-letopis-content" v-html="formatLetopis(letopis)" />
      <div v-else class="fa-empty">История пуста</div>
    </div>

    <!-- Fight animation view -->
    <template v-else>
    <!-- No fights -->
    <div v-if="!fights.length" class="fa-empty">
      Бои еще не начались
    </div>

    <template v-else>
      <!-- Controls -->
      <div class="fa-controls">
        <button class="fa-btn" @click="togglePlay">
          {{ isPlaying ? '⏸' : '▶' }}
        </button>
        <button class="fa-btn" @click="restart" title="Restart">⏮</button>
        <button class="fa-btn" @click="skipToEnd" title="Skip to end">⏭</button>
        <div class="fa-speed">
          <button
            v-for="s in [1, 2, 4]"
            :key="s"
            class="fa-speed-btn"
            :class="{ active: speed === s }"
            @click="setSpeed(s)"
          >
            {{ s }}x
          </button>
        </div>
        <span class="fa-progress">
          {{ currentFightIdx + 1 }} / {{ fights.length }}
        </span>
      </div>

      <!-- Fight card -->
      <div v-if="fight" class="fa-card">
        <!-- Portraits -->
        <div class="fa-portraits">
          <div class="fa-portrait" :class="{ winner: fight.outcome === 'win' }">
            <img :src="fight.attackerAvatar" :alt="fight.attackerCharName" class="fa-avatar" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            <div class="fa-name">{{ fight.attackerName }}</div>
            <div class="fa-char">{{ fight.attackerCharName }}</div>
          </div>

          <div class="fa-vs">VS</div>

          <div class="fa-portrait" :class="{ winner: fight.outcome === 'loss' }">
            <img :src="fight.defenderAvatar" :alt="fight.defenderCharName" class="fa-avatar" @error="(e: Event) => (e.target as HTMLImageElement).src = '/art/avatars/guess.png'">
            <div class="fa-name">{{ fight.defenderName }}</div>
            <div class="fa-char">{{ fight.defenderCharName }}</div>
          </div>
        </div>

        <!-- Block/Skip: simple result -->
        <div v-if="isSpecialOutcome" class="fa-special">
          <div class="fa-outcome" :class="outcomeClass(fight)">
            {{ outcomeLabel(fight) }}
          </div>
        </div>

        <!-- Normal fight: weighing machine + factors -->
        <template v-else>
          <!-- Weighing machine bar -->
          <div class="fa-bar-container">
            <span class="fa-bar-label left">ATK</span>
            <div class="fa-bar-track">
              <div
                class="fa-bar-fill"
                :style="{ width: barPosition + '%' }"
                :class="{
                  'bar-attacker': animatedWeighingValue > 0,
                  'bar-defender': animatedWeighingValue < 0,
                  'bar-even': animatedWeighingValue === 0,
                }"
              >
                <span class="fa-bar-value">
                  {{ animatedWeighingValue > 0 ? '+' : '' }}{{ animatedWeighingValue.toFixed(1) }}
                </span>
              </div>
            </div>
            <span class="fa-bar-label right">DEF</span>
          </div>

          <!-- Factors list -->
          <div class="fa-factors">
            <div
              v-for="(fac, idx) in factors"
              :key="idx"
              class="fa-factor"
              :class="[fac.highlight, { visible: showFactor(idx) }]"
            >
              <span class="fa-factor-label">{{ fac.label }}</span>
              <span class="fa-factor-detail">{{ fac.detail }}</span>
              <span class="fa-factor-value" v-if="fac.value !== 0">
                {{ fac.value > 0 ? '+' : '' }}{{ fac.value.toFixed(1) }}
              </span>
            </div>

            <!-- Justice (Step 2) -->
            <div class="fa-factor justice" :class="{ visible: showJustice }">
              <span class="fa-factor-label">Справедливость</span>
              <span class="fa-factor-detail">{{ fight.justiceMe }} vs {{ fight.justiceTarget }}</span>
              <span class="fa-factor-value" v-if="fight.pointsFromJustice !== 0">
                {{ fight.pointsFromJustice > 0 ? '+1 очко' : '-1 очко' }}
              </span>
              <span class="fa-factor-value neutral-val" v-else>0</span>
            </div>

            <!-- Random roll (Step 3, if used) -->
            <div v-if="fight.usedRandomRoll" class="fa-factor random" :class="{ visible: showRandom }">
              <span class="fa-factor-label">🎲 Случайность</span>
              <span class="fa-factor-detail">
                Бросок: {{ fight.randomNumber }} / {{ fight.maxRandomNumber.toFixed(0) }}
                (порог: {{ fight.randomForPoint.toFixed(0) }})
              </span>
              <span class="fa-factor-value">
                {{ fight.randomNumber <= fight.randomForPoint ? '+1' : '-1' }}
              </span>
            </div>
          </div>

          <!-- Result -->
          <div v-if="showResult" class="fa-result">
            <div class="fa-outcome" :class="outcomeClass(fight)">
              {{ outcomeLabel(fight) }}
            </div>
            <div class="fa-points">
              Очки: {{ fight.totalPointsWon }}
              <span v-if="fight.moralChange !== 0" class="fa-moral">
                | Мораль: {{ fight.moralChange > 0 ? '+' : '' }}{{ fight.moralChange }}
              </span>
            </div>
          </div>
        </template>
      </div>

      <!-- Mini fight list (clickable thumbnails) -->
      <div class="fa-thumbs">
        <button
          v-for="(f, idx) in fights"
          :key="idx"
          class="fa-thumb"
          :class="{
            active: idx === currentFightIdx,
            'is-block': f.outcome === 'block',
            'is-skip': f.outcome === 'skip',
          }"
          @click="currentFightIdx = idx; currentStep = totalSteps - 1; skippedToEnd = true; isPlaying = false; clearTimer()"
          :title="`${f.attackerName} vs ${f.defenderName}`"
        >
          <span class="thumb-idx">{{ idx + 1 }}</span>
        </button>
      </div>
    </template>
    </template>
  </div>
</template>

<style scoped>
.fight-animation {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 6px;
  overflow-y: auto;
  max-height: 400px;
}

.fa-empty {
  color: var(--text-muted);
  font-style: italic;
  padding: 24px;
  text-align: center;
  font-size: 13px;
}

/* ── Controls ────────────────────────────────────────────────────── */
.fa-controls {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 0;
}

.fa-btn {
  background: var(--bg-secondary);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 3px 10px;
  font-size: 14px;
  cursor: pointer;
  color: var(--text-primary);
  transition: background 0.15s;
}
.fa-btn:hover { background: var(--accent-blue); color: white; }

.fa-speed {
  display: flex;
  gap: 2px;
  margin-left: 6px;
}

.fa-speed-btn {
  background: var(--bg-secondary);
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 2px 8px;
  font-size: 11px;
  cursor: pointer;
  color: var(--text-secondary);
  transition: all 0.15s;
}
.fa-speed-btn.active {
  background: var(--accent-purple);
  color: white;
  border-color: var(--accent-purple);
}

.fa-progress {
  margin-left: auto;
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 600;
}

/* ── Fight card ──────────────────────────────────────────────────── */
.fa-card {
  background: var(--bg-primary);
  border-radius: var(--radius);
  padding: 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

/* ── Portraits ───────────────────────────────────────────────────── */
.fa-portraits {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
}

.fa-portrait {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 3px;
  opacity: 0.7;
  transition: all 0.3s;
}
.fa-portrait.winner {
  opacity: 1;
  transform: scale(1.05);
}
.fa-portrait.winner .fa-name {
  color: var(--accent-green);
  font-weight: 700;
}

.fa-avatar {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  object-fit: cover;
  border: 2px solid var(--border);
}
.fa-portrait.winner .fa-avatar {
  border-color: var(--accent-green);
  box-shadow: 0 0 8px rgba(72, 199, 142, 0.4);
}

.fa-name {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-primary);
}

.fa-char {
  font-size: 10px;
  color: var(--text-muted);
}

.fa-vs {
  font-size: 18px;
  font-weight: 900;
  color: var(--accent-red);
  text-shadow: 0 0 6px rgba(255, 82, 82, 0.3);
}

/* ── Weighing Machine Bar ────────────────────────────────────────── */
.fa-bar-container {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 0;
}

.fa-bar-label {
  font-size: 10px;
  font-weight: 700;
  color: var(--text-muted);
  width: 28px;
  text-align: center;
  flex-shrink: 0;
}

.fa-bar-track {
  flex: 1;
  height: 22px;
  background: var(--bg-secondary);
  border-radius: 11px;
  overflow: hidden;
  position: relative;
}

.fa-bar-fill {
  height: 100%;
  border-radius: 11px;
  transition: width 0.5s ease;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  padding-right: 6px;
  min-width: 40px;
}

.fa-bar-fill.bar-attacker {
  background: linear-gradient(90deg, var(--accent-green), #48c78e);
}
.fa-bar-fill.bar-defender {
  background: linear-gradient(90deg, var(--accent-red), #ff5252);
}
.fa-bar-fill.bar-even {
  background: linear-gradient(90deg, var(--accent-orange), #ffb347);
}

.fa-bar-value {
  font-size: 10px;
  font-weight: 700;
  color: white;
  text-shadow: 0 1px 2px rgba(0,0,0,0.3);
}

/* ── Factors ─────────────────────────────────────────────────────── */
.fa-factors {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.fa-factor {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 3px 8px;
  border-radius: 4px;
  font-size: 11px;
  background: var(--bg-secondary);
  opacity: 0;
  transform: translateY(4px);
  transition: opacity 0.3s, transform 0.3s;
}
.fa-factor.visible {
  opacity: 1;
  transform: translateY(0);
}

.fa-factor.good { border-left: 3px solid var(--accent-green); }
.fa-factor.bad { border-left: 3px solid var(--accent-red); }
.fa-factor.neutral { border-left: 3px solid var(--accent-orange); }

.fa-factor.justice { border-left: 3px solid var(--accent-purple); }
.fa-factor.random { border-left: 3px solid var(--accent-blue); }

.fa-factor-label {
  font-weight: 700;
  color: var(--text-primary);
  min-width: 100px;
}

.fa-factor-detail {
  color: var(--text-secondary);
  flex: 1;
}

.fa-factor-value {
  font-weight: 700;
  color: var(--accent-gold);
  margin-left: auto;
}
.neutral-val { color: var(--text-muted); }

/* ── Result ──────────────────────────────────────────────────────── */
.fa-result {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 8px 0 4px;
}

.fa-outcome {
  font-size: 14px;
  font-weight: 900;
  text-transform: uppercase;
  padding: 4px 16px;
  border-radius: 8px;
}
.outcome-attacker { background: var(--accent-green); color: white; }
.outcome-defender { background: var(--accent-red); color: white; }
.outcome-neutral { background: var(--accent-orange); color: white; }

.fa-points {
  font-size: 11px;
  color: var(--text-secondary);
}
.fa-moral {
  color: var(--accent-purple);
}

/* ── Special (block/skip) ────────────────────────────────────────── */
.fa-special {
  display: flex;
  justify-content: center;
  padding: 12px 0;
}

/* ── Thumbnails ──────────────────────────────────────────────────── */
.fa-thumbs {
  display: flex;
  gap: 3px;
  flex-wrap: wrap;
  padding-top: 4px;
}

.fa-thumb {
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--bg-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 10px;
  font-weight: 700;
  color: var(--text-secondary);
  transition: all 0.15s;
}
.fa-thumb:hover { border-color: var(--accent-blue); }
.fa-thumb.active {
  background: var(--accent-blue);
  color: white;
  border-color: var(--accent-blue);
}
.fa-thumb.is-block { border-color: var(--accent-orange); }
.fa-thumb.is-skip { border-color: var(--accent-red); opacity: 0.6; }

/* ── Tab header ──────────────────────────────────────────────────── */
.fa-tab-header {
  display: flex;
  gap: 2px;
  background: var(--bg-secondary);
  border-radius: 8px;
  padding: 2px;
}

.fa-tab {
  flex: 1;
  padding: 5px 12px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
}
.fa-tab:hover { color: var(--text-primary); }
.fa-tab.active {
  background: var(--bg-card);
  color: var(--text-primary);
  box-shadow: 0 1px 3px rgba(0,0,0,0.2);
}

/* ── Летопись ────────────────────────────────────────────────────── */
.fa-letopis {
  flex: 1;
  overflow-y: auto;
  padding: 6px;
  background: var(--bg-primary);
  border-radius: var(--radius);
  max-height: 420px;
}

.fa-letopis-content {
  font-size: 12px;
  line-height: 1.6;
  color: var(--text-secondary);
  font-family: var(--font-mono);
}

.fa-letopis-content :deep(strong) { color: var(--accent-gold); }
.fa-letopis-content :deep(em) { color: var(--accent-blue); }
.fa-letopis-content :deep(u) { color: var(--accent-green); }
.fa-letopis-content :deep(del) { color: var(--text-muted); text-decoration: line-through; }
</style>
