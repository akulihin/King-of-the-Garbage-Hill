<script setup lang="ts">
import { computed, ref } from 'vue'
import type { Player } from 'src/services/signalr'
import { useGameStore } from 'src/store/game'

const props = withDefaults(defineProps<{
  player: Player
  isMe: boolean
  resistFlash?: string[]
  justiceReset?: boolean
}>(), {
  resistFlash: () => [],
  justiceReset: false,
})

const store = useGameStore()

const hasLvlUpPoints = computed(() => props.isMe && (props.player?.status.lvlUpPoints ?? 0) > 0)
const lvlUpPoints = computed(() => props.player?.status.lvlUpPoints ?? 0)

const moral = computed(() => {
  if (!props.player) return 0
  return Number.parseFloat(props.player.character.moralDisplay) || 0
})

const hasMoral = computed(() => props.isMe && moral.value >= 1)

const roundNo = computed(() => store.gameState?.roundNo ?? 0)
const isLastRound = computed(() => roundNo.value === 10)

const hasBulkaet = computed(() => {
  if (!props.player) return false
  return props.player.character.passives.some((p: { name: string }) => p.name === 'Булькает')
})

const isDeepList = computed(() => {
  if (!props.player) return false
  return props.player.character.name === 'DeepList'
})

/** Moral → Points exchange rate (matching backend GameUpdateMess.cs) */
const moralToPointsRate = computed(() => {
  if (isDeepList.value) return null
  const m = moral.value
  if (m >= 20) return { cost: 20, gain: 10 }
  if (m >= 13) return { cost: 13, gain: 5 }
  if (m >= 8)  return { cost: 8,  gain: 2 }
  if (m >= 5)  return { cost: 5,  gain: 1 }
  return null
})

const hasEvreiPassive = computed(() => {
  if (!props.player) return false
  return props.player.character.passives.some((p: { name: string }) => p.name === 'Еврей')
})

/** Moral → Skill exchange rate (matching backend GameUpdateMess.cs) */
const moralToSkillRate = computed(() => {
  if (hasBulkaet.value) return null
  const m = moral.value
  if (m >= 20) return { cost: 20, gain: 100 }
  if (m >= 13) return { cost: 13, gain: 50 }
  if (hasEvreiPassive.value && m >= 7) return { cost: 7, gain: 40 }
  if (m >= 8)  return { cost: 8,  gain: 30 }
  if (m >= 5)  return { cost: 5,  gain: 18 }
  if (m >= 3)  return { cost: 3,  gain: 10 }
  if (m >= 2)  return { cost: 2,  gain: 6 }
  if (m >= 1)  return { cost: 1,  gain: 2 }
  return null
})

/** Parse "ClassName || description" from classStatDisplayText */
const classLabel = computed(() => {
  const raw = props.player?.character.classStatDisplayText ?? ''
  if (!raw) return ''
  const parts = raw.split('||')
  return parts[0].trim()
})
const classTooltip = computed(() => {
  const raw = props.player?.character.classStatDisplayText ?? ''
  if (!raw) return ''
  const parts = raw.split('||')
  // Strip Discord markdown (*word*) from tooltip
  return parts.length > 1 ? parts[1].trim().replace(/\*/g, '') : ''
})

/** Translate skill target class to a badge label + class */
function skillTargetBadge(target: string): { label: string; cls: string } {
  switch (target) {
    case 'Интеллект': return { label: 'INT', cls: 'gi gi-lg gi-int' }
    case 'Сила': return { label: 'STR', cls: 'gi gi-lg gi-str' }
    case 'Скорость': return { label: 'SPD', cls: 'gi gi-lg gi-spd' }
    default: return { label: '?', cls: 'gi' }
  }
}

function skillTargetTooltip(target: string): string {
  switch (target) {
    case 'Интеллект': return 'Мишень: Intelligence. Attack INT-class enemies for bonus skill'
    case 'Сила': return 'Мишень: Strength. Attack STR-class enemies for bonus skill'
    case 'Скорость': return 'Мишень: Speed. Attack SPD-class enemies for bonus skill'
    default: return 'Мишень'
  }
}

// ── Tooltip system ──────────────────────────────────────────────────
const tipText = ref('')
const tipVisible = ref(false)
const tipPos = ref({ x: 0, y: 0 })
let tipTimer: ReturnType<typeof setTimeout> | null = null

function showTip(e: MouseEvent, text: string) {
  if (tipTimer) clearTimeout(tipTimer)
  tipText.value = text
  tipPos.value = { x: e.clientX, y: e.clientY }
  tipTimer = setTimeout(() => { tipVisible.value = true }, 120)
}
function moveTip(e: MouseEvent) {
  tipPos.value = { x: e.clientX, y: e.clientY }
}
function hideTip() {
  if (tipTimer) clearTimeout(tipTimer)
  tipVisible.value = false
}
</script>

<template>
  <div class="player-card" :class="{ 'is-me': isMe }">
    <!-- Large avatar -->
    <div class="pc-avatar-wrap">
      <img
        v-if="player.character.avatarCurrent"
        :src="player.character.avatarCurrent"
        :alt="player.character.name"
        class="pc-avatar-img"
      >
      <div v-else class="pc-avatar-fallback">
        {{ player.character.name.charAt(0) }}
      </div>
    </div>

    <!-- Name & character -->
    <div class="pc-identity">
      <div class="pc-name">{{ player.character.name }}</div>
      <div class="pc-username">{{ player.discordUsername }}</div>
    </div>

    <!-- Stats with bars + resist/quality -->
    <div class="pc-stats">
      <div v-if="hasLvlUpPoints" class="lvl-up-badge">
        +{{ lvlUpPoints }} очков
      </div>
      <!-- Intelligence -->
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('intelligence') }">
        <div class="stat-row">
          <span class="gi gi-lg gi-int">INT</span>
          <div class="stat-bar-bg">
            <div class="stat-bar intelligence" :style="{ width: `${player.character.intelligence * 10}%` }" />
          </div>
          <span class="stat-val stat-intelligence">{{ player.character.intelligence }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Intelligence" @click="store.levelUp(1)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.intelligenceResist }}</span>
          <span v-if="player.character.intelligenceBonusText" class="resist-bonus">{{ player.character.intelligenceBonusText }}</span>
        </div>
      </div>
      <!-- Strength -->
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('strength') }">
        <div class="stat-row">
          <span class="gi gi-lg gi-str">STR</span>
          <div class="stat-bar-bg">
            <div class="stat-bar strength" :style="{ width: `${player.character.strength * 10}%` }" />
          </div>
          <span class="stat-val stat-strength">{{ player.character.strength }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Strength" @click="store.levelUp(2)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.strengthResist }}</span>
          <span v-if="player.character.strengthBonusText" class="resist-bonus">{{ player.character.strengthBonusText }}</span>
        </div>
      </div>
      <!-- Speed -->
      <div class="stat-block">
        <div class="stat-row">
          <span class="gi gi-lg gi-spd">SPD</span>
          <div class="stat-bar-bg">
            <div class="stat-bar speed" :style="{ width: `${player.character.speed * 10}%` }" />
          </div>
          <span class="stat-val stat-speed">{{ player.character.speed }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Speed" @click="store.levelUp(3)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.speedResist }}</span>
          <span v-if="player.character.speedBonusText" class="resist-bonus">{{ player.character.speedBonusText }}</span>
        </div>
      </div>
    </div>

    <!-- Psyche (separated — different stat type) -->
    <div class="pc-psyche-box">
      <div class="stat-block" :class="{ 'resist-hit': resistFlash.includes('psyche') }">
        <div class="stat-row">
          <span class="gi gi-lg gi-psy">PSY</span>
          <div class="stat-bar-bg">
            <div class="stat-bar psyche" :style="{ width: `${player.character.psyche * 10}%` }" />
          </div>
          <span class="stat-val stat-psyche">{{ player.character.psyche }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Psyche" @click="store.levelUp(4)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge"><span class="gi gi-def">DEF</span> {{ player.character.psycheResist }}</span>
          <span v-if="player.character.psycheBonusText" class="resist-bonus">{{ player.character.psycheBonusText }}</span>
        </div>
      </div>
    </div>

    <!-- Class / Skill / Moral / Justice / Target -->
    <div class="pc-meta">
      <div class="meta-box" :class="{ 'justice-reset-flash': justiceReset }"
        @mouseenter="showTip($event, 'Justice allows you to win Round 2 and influences Round 1. You gain it when you\'re defeated and it is fully reset on victory')"
        @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Justice</span>
        <span class="meta-value stat-justice">{{ player.character.justice }}</span>
        <span v-if="justiceReset" class="justice-reset-label">RESET</span>
      </div>

      <div class="meta-box"
        @mouseenter="showTip($event, 'You can exchange moral for Skill or Points. Gain it by winning and lose it when you\'re defeated')"
        @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Moral</span>
        <span class="meta-value stat-moral">{{ player.character.moralDisplay }}</span>
      </div>

      <div class="meta-box"
        @mouseenter="showTip($event, 'Skill influences your fighting power. Gain it by attacking your TARGET')"
        @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Skill</span>
        <span class="meta-value stat-skill">{{ player.character.skillDisplay }}</span>
      </div>

      <div v-if="classLabel" class="meta-box"
        @mouseenter="showTip($event, classTooltip)" @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Class</span>
        <span class="meta-value stat-class">{{ classLabel }}</span>
      </div>

      <div v-if="isMe && player.character.skillTarget" class="meta-box"
        @mouseenter="showTip($event, skillTargetTooltip(player.character.skillTarget))" @mousemove="moveTip" @mouseleave="hideTip">
        <span class="meta-label">Target</span>
        <span class="meta-value"><span :class="skillTargetBadge(player.character.skillTarget).cls" style="font-size:14px">{{ skillTargetBadge(player.character.skillTarget).label }}</span></span>
      </div>
    </div>

    <!-- Moral exchange -->
    <div v-if="hasMoral" class="pc-moral-actions">
      <div v-if="isLastRound" class="moral-last-round">Последний шанс!</div>
      <!-- Булькает: both disabled -->
      <template v-if="hasBulkaet">
        <button class="moral-btn moral-btn-disabled" disabled>Ничего не понимает, но Булькает!</button>
      </template>
      <template v-else>
        <!-- Points button (DeepList can't use) -->
        <button v-if="isDeepList" class="moral-btn moral-btn-disabled" disabled>Только скилл</button>
        <button v-else-if="moralToPointsRate" class="moral-btn" @click="store.moralToPoints()"
          :title="`Обменять ${moralToPointsRate.cost} Морали на ${moralToPointsRate.gain} бонусных очков`">
          {{ moralToPointsRate.cost }} Moral → {{ moralToPointsRate.gain }} pts
        </button>
        <button v-else class="moral-btn moral-btn-disabled" disabled>Мало морали</button>
        <!-- Skill button -->
        <button v-if="moralToSkillRate" class="moral-btn" @click="store.moralToSkill()"
          :title="`Обменять ${moralToSkillRate.cost} Морали на ${moralToSkillRate.gain} Cкилла`">
          {{ moralToSkillRate.cost }} Moral → {{ moralToSkillRate.gain }} skill
        </button>
        <button v-else class="moral-btn moral-btn-disabled" disabled>Мало морали</button>
      </template>
    </div>

    <!-- Score -->
    <div class="pc-score-row">
      <span class="pc-score">{{ player.status.score }}</span>
      <span class="pc-score-label">pts</span>
      <span class="pc-place">#{{ player.status.place }}</span>
    </div>

    <!-- In-game tooltip (teleported to body for correct positioning) -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.player-card {
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.player-card.is-me {
  border-color: var(--accent-gold-dim);
  box-shadow: var(--glow-gold);
}

/* Avatar */
.pc-avatar-wrap {
  width: 100%;
  aspect-ratio: 1;
  border-radius: var(--radius);
  overflow: hidden;
  background: var(--bg-inset);
  border: 1px solid var(--border-subtle);
}

.pc-avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.pc-avatar-fallback {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 48px;
  font-weight: 800;
  color: var(--text-dim);
}

/* Identity */
.pc-identity {
  text-align: center;
}

.pc-name {
  font-weight: 800;
  font-size: 16px;
  color: var(--accent-gold);
  letter-spacing: 0.3px;
}

.pc-username {
  font-size: 11px;
  color: var(--text-muted);
  font-family: var(--font-mono);
}

/* Stats */
.pc-stats {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.stat-row {
  display: flex;
  align-items: center;
  gap: 5px;
}

.stat-bar-bg {
  flex: 1;
  height: 5px;
  background: var(--bg-inset);
  border-radius: 3px;
  overflow: hidden;
}

.stat-bar {
  height: 100%;
  border-radius: 3px;
  transition: width 0.5s ease;
}

.stat-bar.intelligence { background: linear-gradient(90deg, var(--kh-c-secondary-info-400), var(--kh-c-secondary-info-200)); }
.stat-bar.strength { background: linear-gradient(90deg, var(--kh-c-secondary-danger-400), var(--kh-c-secondary-danger-200)); }
.stat-bar.speed { background: linear-gradient(90deg, var(--kh-c-secondary-success-400), var(--kh-c-secondary-success-200)); }
.stat-bar.psyche { background: linear-gradient(90deg, var(--kh-c-secondary-purple-400), var(--kh-c-secondary-purple-200)); }

.stat-val {
  width: 20px;
  text-align: right;
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 12px;
  color: var(--text-primary);
}

.stat-block {
  display: flex;
  flex-direction: column;
  gap: 1px;
  border-radius: 4px;
  transition: background 0.3s, box-shadow 0.3s;
}

.stat-block.resist-hit {
  animation: resist-hit-flash 1.5s ease-out;
}

@keyframes resist-hit-flash {
  0% { background: rgba(239, 128, 128, 0.3); box-shadow: inset 0 0 12px rgba(239, 128, 128, 0.4); }
  30% { background: rgba(239, 128, 128, 0.15); box-shadow: inset 0 0 6px rgba(239, 128, 128, 0.2); }
  100% { background: transparent; box-shadow: none; }
}

.resist-row {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-left: 23px;
}

.resist-badge {
  font-size: 10px;
  font-weight: 600;
  color: var(--accent-blue);
}

.resist-bonus {
  font-size: 9px;
  color: var(--accent-gold-dim);
  font-weight: 700;
}

.lvl-up-badge {
  text-align: center;
  font-size: 10px;
  font-weight: 800;
  color: var(--accent-gold);
  padding: 3px 8px;
  background: rgba(233, 219, 61, 0.08);
  border: 1px solid rgba(233, 219, 61, 0.15);
  border-radius: var(--radius);
  margin-bottom: 2px;
  letter-spacing: 0.3px;
}

.lvl-btn {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: 1.5px solid var(--accent-green);
  background: rgba(63, 167, 61, 0.08);
  color: var(--accent-green);
  font-size: 13px;
  font-weight: 900;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  line-height: 1;
  transition: all 0.15s;
  flex-shrink: 0;
}
.lvl-btn:hover {
  background: var(--accent-green);
  color: var(--bg-primary);
  box-shadow: var(--glow-green);
}

/* Meta row */
.pc-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.meta-box {
  flex: 1 1 auto;
  min-width: 44px;
  text-align: center;
  padding: 5px 3px;
  background: var(--bg-inset);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  cursor: default;
  transition: border-color 0.15s;
}

.meta-box:hover {
  border-color: var(--border-color);
}

.meta-label {
  display: block;
  font-size: 8px;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 700;
}

.meta-value {
  font-size: 16px;
  font-weight: 800;
}

.stat-class { font-size: 12px; color: var(--accent-gold); }

/* Justice reset animation */
.justice-reset-flash {
  position: relative;
  animation: justice-reset 2s ease-out;
}

.justice-reset-label {
  position: absolute;
  top: -6px;
  right: -4px;
  font-size: 7px;
  font-weight: 900;
  color: #e879f9;
  background: rgba(232, 121, 249, 0.15);
  padding: 1px 4px;
  border-radius: 3px;
  letter-spacing: 0.5px;
  animation: justice-reset-label 2s ease-out forwards;
}

@keyframes justice-reset {
  0% { border-color: #e879f9; box-shadow: 0 0 12px rgba(232, 121, 249, 0.5); }
  30% { border-color: #e879f9; box-shadow: 0 0 8px rgba(232, 121, 249, 0.3); }
  100% { border-color: var(--border-subtle); box-shadow: none; }
}

@keyframes justice-reset-label {
  0% { opacity: 1; transform: translateY(0); }
  70% { opacity: 1; transform: translateY(0); }
  100% { opacity: 0; transform: translateY(-6px); }
}

/* Moral exchange */
.pc-moral-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  position: relative;
}

.moral-last-round {
  width: 100%;
  text-align: center;
  font-size: 10px;
  font-weight: 800;
  color: #ff4444;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  animation: lastChancePulse 1.2s ease-in-out infinite;
}
@keyframes lastChancePulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.moral-btn {
  flex: 1;
  padding: 5px 4px;
  border: 1px solid rgba(230, 148, 74, 0.3);
  border-radius: var(--radius);
  background: rgba(230, 148, 74, 0.06);
  color: var(--accent-orange);
  font-size: 10px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s;
  text-align: center;
  min-width: 0;
  white-space: nowrap;
}
.moral-btn:hover:not(:disabled) {
  background: var(--accent-orange);
  color: var(--bg-primary);
  border-color: var(--accent-orange);
}
.moral-btn-disabled {
  opacity: 0.45;
  cursor: not-allowed;
  border-color: rgba(100, 100, 100, 0.3);
  color: rgba(180, 180, 180, 0.7);
  background: rgba(60, 60, 60, 0.15);
}

/* Score */
.pc-score-row {
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 4px;
  padding-top: 4px;
  border-top: 1px solid var(--border-subtle);
}

.pc-score {
  font-size: 26px;
  font-weight: 900;
  color: var(--accent-gold);
  font-family: var(--font-mono);
}

.pc-score-label {
  font-size: 11px;
  color: var(--text-muted);
  font-weight: 600;
}

.pc-place {
  font-size: 13px;
  font-weight: 700;
  color: var(--text-muted);
  margin-left: 6px;
  font-family: var(--font-mono);
}

/* PSY separated box */
.pc-psyche-box {
  border: 1px solid rgba(232, 121, 249, 0.12);
  border-radius: var(--radius);
  padding: 4px 6px;
  background: rgba(232, 121, 249, 0.03);
}
</style>

<!-- Tooltip needs to be unscoped to work with Teleport to body -->
<style>
.pc-tooltip {
  position: fixed;
  z-index: 9999;
  pointer-events: none;
  transform: translate(12px, -100%);
  max-width: 240px;
  padding: 6px 10px;
  font-size: 11px;
  font-weight: 600;
  line-height: 1.4;
  color: var(--text-primary);
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 5px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5);
  animation: pc-tip-in 0.1s ease-out;
}
@keyframes pc-tip-in {
  from { opacity: 0; transform: translate(12px, -100%) scale(0.95); }
  to { opacity: 1; transform: translate(12px, -100%) scale(1); }
}
</style>
