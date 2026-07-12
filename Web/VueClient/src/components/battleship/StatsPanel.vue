<script setup lang="ts">
import { computed } from 'vue'
import { Check, Flame, Swords, Trophy } from 'lucide-vue-next'
import { useBattleshipStore } from 'src/store/battleship'
import { currentLocale } from 'src/i18n'

const store = useBattleshipStore()

const stats = computed(() => store.statsState)

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

const totalGames = computed(() => (stats.value?.wins ?? 0) + (stats.value?.losses ?? 0))

const winRate = computed(() => {
  if (!stats.value || totalGames.value === 0) return 0
  return Math.round((stats.value.wins / totalGames.value) * 100)
})

/** Conic ring fill: current streak against the personal best (full ring = new record). */
const streakRingDeg = computed(() => {
  if (!stats.value) return 0
  const best = Math.max(1, stats.value.bestDailyStreak)
  const ratio = Math.min(1, stats.value.currentDailyStreak / best)
  return Math.round(ratio * 360)
})
</script>

<template>
  <section v-if="stats" class="stats-panel bs-card" :aria-label="t('Your battleship record', 'Ваша статистика морского боя')">
    <div class="stats-main">
      <div class="streak-ring" :style="{ '--streak-progress': streakRingDeg + 'deg' }" :class="{ 'has-streak': stats.currentDailyStreak > 0 }">
        <div class="streak-ring-inner">
          <Flame :size="16" aria-hidden="true" />
          <strong class="bs-mono">{{ stats.currentDailyStreak }}</strong>
        </div>
      </div>
      <div class="stats-copy">
        <span class="bs-kicker"><Swords :size="12" aria-hidden="true" /> {{ t('Your record', 'Ваш рекорд') }}</span>
        <div class="record-line">
          <strong class="bs-mono">{{ stats.wins }}</strong>
          <span class="record-label">{{ t('wins', 'побед') }}</span>
          <span class="record-sep">/</span>
          <strong class="bs-mono">{{ stats.losses }}</strong>
          <span class="record-label">{{ t('losses', 'поражений') }}</span>
          <span v-if="totalGames > 0" class="bs-chip win-rate-chip bs-mono">{{ winRate }}%</span>
        </div>
        <small class="streak-note">{{ t(`Daily win streak: ${stats.currentDailyStreak} (best ${stats.bestDailyStreak})`, `Серия побед по дням: ${stats.currentDailyStreak} (рекорд ${stats.bestDailyStreak})`) }}</small>
      </div>
    </div>

    <div class="first-win" :class="stats.firstWinAvailable ? 'is-available' : 'is-claimed'">
      <template v-if="stats.firstWinAvailable">
        <span class="bs-chip bs-chip--green first-win-chip">
          <img :src="'/art/emojis/zbs.png'" alt="ZBS">
          <strong class="bs-mono">+{{ stats.firstWinZbs }}</strong>
          <span>{{ t('for the first win today', 'за первую победу сегодня') }}</span>
        </span>
      </template>
      <template v-else>
        <span class="bs-chip first-win-chip claimed">
          <Check :size="14" aria-hidden="true" />
          <span>{{ t('Daily bonus claimed', 'Дневной бонус получен') }}</span>
        </span>
      </template>
      <span class="zbs-balance bs-mono" :title="t('ZBS balance', 'Баланс ZBS')">
        <Trophy :size="12" aria-hidden="true" />
        {{ stats.zbsBalance }} ZBS
      </span>
    </div>
  </section>
</template>

<style scoped>
.stats-panel {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.stats-main {
  display: flex;
  align-items: center;
  gap: 14px;
}

/* Streak flame with conic progress ring (DailyQuestBoard pattern) */
.streak-ring {
  --streak-progress: 0deg;
  width: 58px;
  height: 58px;
  flex-shrink: 0;
  display: grid;
  place-items: center;
  border-radius: 50%;
  color: var(--text-dim);
  background:
    radial-gradient(circle at center, var(--bg-card) 58%, transparent 60%),
    conic-gradient(var(--accent-orange) var(--streak-progress), rgba(255, 255, 255, 0.07) 0deg);
}
.streak-ring.has-streak {
  color: var(--accent-orange);
  box-shadow: 0 0 20px color-mix(in srgb, var(--accent-orange) 12%, transparent);
}
.streak-ring-inner {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1px;
}
.streak-ring-inner strong {
  color: var(--text-primary);
  font-size: 14px;
  font-weight: 900;
  line-height: 1;
}

.stats-copy {
  display: flex;
  flex-direction: column;
  gap: 3px;
}
.record-line {
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 5px;
}
.record-line strong {
  color: var(--text-primary);
  font-size: 1.15rem;
  font-weight: 900;
  line-height: 1;
}
.record-label {
  color: var(--text-dim);
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.record-sep { color: var(--text-dim); }
.win-rate-chip {
  --bs-chip-color: var(--accent-gold);
  min-height: 20px;
  margin-left: 4px;
  font-size: 0.65rem;
}
.streak-note {
  color: var(--text-dim);
  font-size: 0.68rem;
}

.first-win {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
}
.first-win-chip {
  min-height: 30px;
  font-size: 0.72rem;
}
.first-win-chip img { width: 18px; height: 18px; object-fit: contain; }
.first-win-chip strong { font-size: 0.85rem; font-weight: 900; }
.first-win-chip.claimed { --bs-chip-color: var(--accent-green); opacity: 0.75; }
.is-available .first-win-chip {
  animation: bs-glow-pulse 2.2s ease-in-out infinite;
  --bs-accent: var(--accent-green);
}
.zbs-balance {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--text-muted);
  font-size: 0.72rem;
}
</style>
