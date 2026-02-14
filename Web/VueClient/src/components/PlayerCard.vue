<script setup lang="ts">
import { computed } from 'vue'
import type { Player } from 'src/services/signalr'
import { useGameStore } from 'src/store/game'

const props = defineProps<{
  player: Player
  isMe: boolean
}>()

const store = useGameStore()

const hasLvlUpPoints = computed(() => props.isMe && (props.player?.status.lvlUpPoints ?? 0) > 0)
const lvlUpPoints = computed(() => props.player?.status.lvlUpPoints ?? 0)

const hasMoral = computed(() => {
  if (!props.isMe || !props.player) return false
  const moral = Number.parseFloat(props.player.character.moralDisplay) || 0
  return moral >= 1
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

/** Translate skill target class to a short display label */
function skillTargetLabel(target: string): string {
  switch (target) {
    case 'Интеллект': return '🧠'
    case 'Сила': return '💪'
    case 'Скорость': return '⚡'
    default: return target || '?'
  }
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
      <div class="stat-block">
        <div class="stat-row">
          <span class="stat-icon stat-intelligence">🧠</span>
          <span class="stat-label">Intelligence</span>
          <div class="stat-bar-bg">
            <div class="stat-bar intelligence" :style="{ width: `${player.character.intelligence * 10}%` }" />
          </div>
          <span class="stat-val">{{ player.character.intelligence }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Intelligence" @click="store.levelUp(1)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge">🛡{{ player.character.intelligenceResist }}</span>
          <span v-if="player.character.intelligenceBonusText" class="resist-bonus">{{ player.character.intelligenceBonusText }}</span>
        </div>
      </div>
      <!-- Strength -->
      <div class="stat-block">
        <div class="stat-row">
          <span class="stat-icon stat-strength">💪</span>
          <span class="stat-label">Strength</span>
          <div class="stat-bar-bg">
            <div class="stat-bar strength" :style="{ width: `${player.character.strength * 10}%` }" />
          </div>
          <span class="stat-val">{{ player.character.strength }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Strength" @click="store.levelUp(2)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge">🛡{{ player.character.strengthResist }}</span>
          <span v-if="player.character.strengthBonusText" class="resist-bonus">{{ player.character.strengthBonusText }}</span>
        </div>
      </div>
      <!-- Speed -->
      <div class="stat-block">
        <div class="stat-row">
          <span class="stat-icon stat-speed">⚡</span>
          <span class="stat-label">Speed</span>
          <div class="stat-bar-bg">
            <div class="stat-bar speed" :style="{ width: `${player.character.speed * 10}%` }" />
          </div>
          <span class="stat-val">{{ player.character.speed }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Speed" @click="store.levelUp(3)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge">🛡{{ player.character.speedResist }}</span>
          <span v-if="player.character.speedBonusText" class="resist-bonus">{{ player.character.speedBonusText }}</span>
        </div>
      </div>
      <!-- Psyche -->
      <div class="stat-block">
        <div class="stat-row">
          <span class="stat-icon stat-psyche">🧘</span>
          <span class="stat-label">Psyche</span>
          <div class="stat-bar-bg">
            <div class="stat-bar psyche" :style="{ width: `${player.character.psyche * 10}%` }" />
          </div>
          <span class="stat-val">{{ player.character.psyche }}</span>
          <button v-if="hasLvlUpPoints" class="lvl-btn" title="+1 Psyche" @click="store.levelUp(4)">+</button>
        </div>
        <div v-if="isMe" class="resist-row">
          <span class="resist-badge">🛡{{ player.character.psycheResist }}</span>
          <span v-if="player.character.psycheBonusText" class="resist-bonus">{{ player.character.psycheBonusText }}</span>
        </div>
      </div>
    </div>

    <!-- Class / Skill / Moral / Justice / Target -->
    <div class="pc-meta">
      <div class="meta-box">
        <span class="meta-label">Justice</span>
        <span class="meta-value stat-justice">{{ player.character.justice }}</span>
      </div>

      <div class="meta-box">
        <span class="meta-label">Moral</span>
        <span class="meta-value stat-moral">{{ player.character.moralDisplay }}</span>
      </div>

      <div class="meta-box">
        <span class="meta-label">Skill</span>
        <span class="meta-value stat-skill">{{ player.character.skillDisplay }}</span>
      </div>

      <div v-if="classLabel" class="meta-box" :title="classTooltip">
        <span class="meta-label">Class</span>
        <span class="meta-value stat-class">{{ classLabel }}</span>
      </div>
      
      <div v-if="isMe && player.character.skillTarget" class="meta-box" :title="'Мишень: ' + player.character.skillTarget">
        <span class="meta-label">Target</span>
        <span class="meta-value stat-target">{{ skillTargetLabel(player.character.skillTarget) }}</span>
      </div>
    </div>

    <!-- Moral exchange -->
    <div v-if="hasMoral" class="pc-moral-actions">
      <button class="moral-btn" title="Обменять мораль на очки" @click="store.moralToPoints()">
        Мораль → Очки
      </button>
      <button class="moral-btn" title="Обменять мораль на навык" @click="store.moralToSkill()">
        Мораль → Навык
      </button>
    </div>

    <!-- Score -->
    <div class="pc-score-row">
      <span class="pc-score">{{ player.status.score }}</span>
      <span class="pc-score-label">pts</span>
      <span class="pc-place">#{{ player.status.place }}</span>
    </div>
  </div>
</template>

<style scoped>
.player-card {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.player-card.is-me {
  border-color: var(--accent-gold);
  border-width: 2px;
}

/* Avatar */
.pc-avatar-wrap {
  width: 100%;
  aspect-ratio: 1;
  border-radius: var(--radius);
  overflow: hidden;
  background: var(--bg-primary);
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
  color: var(--text-muted);
}

/* Identity */
.pc-identity {
  text-align: center;
}

.pc-name {
  font-weight: 800;
  font-size: 18px;
  color: var(--accent-gold);
}

.pc-username {
  font-size: 12px;
  color: var(--text-muted);
}

/* Stats */
.pc-stats {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.stat-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

.stat-icon { font-size: 14px; width: 20px; }

.stat-label {
  width: 80px;
  font-size: 11px;
  color: var(--text-muted);
  font-weight: 600;
}

.stat-bar-bg {
  flex: 1;
  height: 6px;
  background: var(--bg-primary);
  border-radius: 3px;
  overflow: hidden;
}

.stat-bar {
  height: 100%;
  border-radius: 3px;
  transition: width 0.5s ease;
}

.stat-bar.intelligence { background: linear-gradient(90deg, #3b82f6, #60a5fa); }
.stat-bar.strength { background: linear-gradient(90deg, #dc2626, #f87171); }
.stat-bar.speed { background: linear-gradient(90deg, #16a34a, #4ade80); }
.stat-bar.psyche { background: linear-gradient(90deg, #7c3aed, #c084fc); }

.stat-val {
  width: 20px;
  text-align: right;
  font-family: var(--font-mono);
  font-weight: 700;
  font-size: 13px;
}

.stat-block {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.resist-row {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-left: 26px; /* align with stat label (icon width + gap) */
}

.resist-badge {
  font-size: 10px;
  font-weight: 600;
  color: var(--accent-blue);
}

.resist-bonus {
  font-size: 9px;
  color: var(--accent-gold);
  font-weight: 700;
}

.lvl-up-badge {
  text-align: center;
  font-size: 11px;
  font-weight: 700;
  color: var(--accent-gold);
  padding: 2px 8px;
  background: rgba(255, 193, 7, 0.1);
  border-radius: 6px;
  margin-bottom: 2px;
}

.lvl-btn {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  border: 1.5px solid var(--accent-green);
  background: rgba(74, 222, 128, 0.1);
  color: var(--accent-green);
  font-size: 14px;
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
  color: white;
  transform: scale(1.1);
  box-shadow: 0 0 8px rgba(74, 222, 128, 0.4);
}

/* Meta */
.pc-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.meta-box {
  flex: 1 1 auto;
  min-width: 48px;
  text-align: center;
  padding: 6px 4px;
  background: var(--bg-primary);
  border-radius: 6px;
  cursor: default;
}

.meta-label {
  display: block;
  font-size: 9px;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.meta-value {
  font-size: 18px;
  font-weight: 800;
}

.stat-class { font-size: 13px; color: var(--accent-gold); }
.stat-target { font-size: 22px; }

/* Moral exchange */
.pc-moral-actions {
  display: flex;
  gap: 6px;
}

.moral-btn {
  flex: 1;
  padding: 5px 4px;
  border: 1px solid var(--accent-orange);
  border-radius: 6px;
  background: rgba(251, 146, 60, 0.08);
  color: var(--accent-orange);
  font-size: 10px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s;
  text-align: center;
}
.moral-btn:hover {
  background: var(--accent-orange);
  color: white;
}

/* Score */
.pc-score-row {
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 4px;
}

.pc-score {
  font-size: 28px;
  font-weight: 900;
  color: var(--accent-gold);
}

.pc-score-label {
  font-size: 12px;
  color: var(--text-muted);
}

.pc-place {
  font-size: 14px;
  font-weight: 700;
  color: var(--text-secondary);
  margin-left: 8px;
}
</style>
