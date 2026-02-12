<script setup lang="ts">
import type { Player } from 'src/services/signalr'

defineProps<{
  player: Player
  isMe: boolean
}>()
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

    <!-- Stats with bars -->
    <div class="pc-stats">
      <div class="stat-row">
        <span class="stat-icon stat-intelligence">🧠</span>
        <span class="stat-label">Intelligence</span>
        <div class="stat-bar-bg">
          <div class="stat-bar intelligence" :style="{ width: `${player.character.intelligence * 10}%` }" />
        </div>
        <span class="stat-val">{{ player.character.intelligence }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-icon stat-strength">💪</span>
        <span class="stat-label">Strength</span>
        <div class="stat-bar-bg">
          <div class="stat-bar strength" :style="{ width: `${player.character.strength * 10}%` }" />
        </div>
        <span class="stat-val">{{ player.character.strength }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-icon stat-speed">⚡</span>
        <span class="stat-label">Speed</span>
        <div class="stat-bar-bg">
          <div class="stat-bar speed" :style="{ width: `${player.character.speed * 10}%` }" />
        </div>
        <span class="stat-val">{{ player.character.speed }}</span>
      </div>
      <div class="stat-row">
        <span class="stat-icon stat-psyche">🧘</span>
        <span class="stat-label">Psyche</span>
        <div class="stat-bar-bg">
          <div class="stat-bar psyche" :style="{ width: `${player.character.psyche * 10}%` }" />
        </div>
        <span class="stat-val">{{ player.character.psyche }}</span>
      </div>
    </div>

    <!-- Skill / Moral / Justice -->
    <div class="pc-meta">
      <div class="meta-box">
        <span class="meta-label">Skill</span>
        <span class="meta-value stat-skill">{{ player.character.skillDisplay }}</span>
      </div>
      <div class="meta-box">
        <span class="meta-label">Moral</span>
        <span class="meta-value stat-moral">{{ player.character.moralDisplay }}</span>
      </div>
      <div class="meta-box">
        <span class="meta-label">Justice</span>
        <span class="meta-value stat-justice">{{ player.character.justice }}</span>
      </div>
    </div>

    <!-- Class text -->
    <div v-if="player.character.classStatDisplayText" class="pc-class">
      {{ player.character.classStatDisplayText }}
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

/* Meta */
.pc-meta {
  display: flex;
  gap: 8px;
}

.meta-box {
  flex: 1;
  text-align: center;
  padding: 6px 4px;
  background: var(--bg-primary);
  border-radius: 6px;
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

/* Class */
.pc-class {
  font-size: 12px;
  color: var(--text-secondary);
  font-style: italic;
  text-align: center;
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
