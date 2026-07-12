<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { Flame, Sparkles, X } from 'lucide-vue-next'
import { useBattleshipStore } from 'src/store/battleship'
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'
import { currentLocale } from 'src/i18n'
import { playAchievementUnlockSound, playBattleshipLose, playBattleshipWin } from 'src/services/sound'
import BsIcon from './BsIcon.vue'

const emit = defineEmits<{ dismiss: [] }>()

const store = useBattleshipStore()
const { overlayRef, dialogRef, trapTabKey } = useFocusTrapDialog()

const isWin = computed(() => store.isWinner)
const reward = computed(() => store.myEndReward)

const winnerName = computed(() => {
  if (!store.gameState?.winnerId) return ''
  if (store.gameState.player1?.discordId === store.gameState.winnerId) return store.gameState.player1?.username ?? ''
  return store.gameState.player2?.username ?? ''
})

const accuracy = computed(() => {
  if (store.myShotsFired === 0) return 0
  return Math.round((store.myShotsHit / store.myShotsFired) * 100)
})

const fleetAlive = computed(() => store.myFleet.filter(s => !s.isDestroyed).length)
const fleetTotal = computed(() => store.myFleet.length)

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

onMounted(() => {
  if (isWin.value) playBattleshipWin()
  else playBattleshipLose()
  if (reward.value?.firstWinAwarded) playAchievementUnlockSound('legendary')
})

function onDialogKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    emit('dismiss')
    return
  }
  trapTabKey(event)
}

function particleStyle(index: number): Record<string, string> {
  const angle = (index * 137.5) % 360
  const distance = 105 + (index % 5) * 24
  return {
    '--particle-angle': `${angle}deg`,
    '--particle-distance': `${distance}px`,
    '--particle-delay': `${(index % 6) * 45}ms`,
    '--particle-size': `${4 + (index % 3) * 2}px`,
  }
}
</script>

<template>
  <Teleport to="body">
    <Transition name="bs-celebration" appear>
      <div ref="overlayRef" class="celebration-overlay">
        <section
          ref="dialogRef"
          class="celebration-card"
          :class="isWin ? 'is-win' : 'is-loss'"
          role="dialog"
          aria-modal="true"
          aria-labelledby="bs-gameover-title"
          tabindex="-1"
          @keydown="onDialogKeydown"
        >
          <div class="celebration-aurora" aria-hidden="true" />
          <div v-if="isWin" class="celebration-rays" aria-hidden="true" />
          <div v-if="isWin" class="celebration-particles" aria-hidden="true">
            <i v-for="index in 24" :key="index" :style="particleStyle(index)" />
          </div>

          <button
            class="skip-button"
            type="button"
            :aria-label="t('Close and reveal the boards', 'Закрыть и показать поле')"
            @click="emit('dismiss')"
          >
            <X :size="18" aria-hidden="true" />
          </button>

          <div class="celebration-content" aria-live="polite">
            <div class="unlock-kicker">
              <Sparkles :size="15" aria-hidden="true" />
              {{ t('Battle over', 'Бой окончен') }}
              <Sparkles :size="15" aria-hidden="true" />
            </div>

            <div class="unlock-icon-stage" aria-hidden="true">
              <span class="icon-orbit icon-orbit-one" />
              <span class="icon-orbit icon-orbit-two" />
              <span class="unlock-icon">
                <BsIcon :icon="isWin ? 'trophy' : 'skull'" :size="54" :stroke-width="1.55" />
              </span>
            </div>

            <h2 id="bs-gameover-title">{{ isWin ? t('Victory!', 'Победа!') : t('Defeat', 'Поражение') }}</h2>
            <p class="winner-line">{{ t(`Winner: ${winnerName}`, `Победил: ${winnerName}`) }}</p>

            <!-- Match stats -->
            <div class="stats-grid">
              <div class="stat-tile">
                <small>{{ t('Turns', 'Ходы') }}</small>
                <strong class="bs-mono">{{ store.turnNumber }}</strong>
              </div>
              <div class="stat-tile">
                <small>{{ t('Shots', 'Выстрелы') }}</small>
                <strong class="bs-mono">{{ store.myShotsFired }}</strong>
              </div>
              <div class="stat-tile">
                <small>{{ t('Accuracy', 'Точность') }}</small>
                <strong class="bs-mono">{{ accuracy }}%</strong>
              </div>
              <div class="stat-tile">
                <small>{{ t('Ships sunk', 'Потоплено') }}</small>
                <strong class="bs-mono">{{ store.myShipsSunk }}</strong>
              </div>
              <div class="stat-tile">
                <small>{{ t('Fleet survived', 'Флот уцелел') }}</small>
                <strong class="bs-mono">{{ fleetAlive }}/{{ fleetTotal }}</strong>
              </div>
            </div>

            <!-- Meta rewards -->
            <div v-if="reward" class="reward-panel">
              <span class="reward-heading">{{ t('Career', 'Карьера') }}</span>
              <span class="bs-chip record-chip bs-mono">
                {{ t(`Record: ${reward.wins}W / ${reward.losses}L`, `Рекорд: ${reward.wins}П / ${reward.losses}П`) }}
              </span>
              <span v-if="reward.currentDailyStreak > 0" class="bs-chip bs-chip--orange streak-chip">
                <Flame :size="14" aria-hidden="true" />
                {{ t(`Daily streak: ${reward.currentDailyStreak}`, `Серия дней: ${reward.currentDailyStreak}`) }}
              </span>
              <span v-if="reward.firstWinAwarded" class="bs-chip bs-chip--green zbs-chip">
                <img :src="'/art/emojis/zbs.png'" alt="ZBS">
                <strong class="bs-mono">+{{ reward.zbsAwarded }}</strong>
                <span>{{ t('First win of the day', 'Первая победа дня') }}</span>
              </span>
            </div>

            <div class="celebration-actions">
              <button class="bs-btn reveal-btn" type="button" @click="emit('dismiss')">
                {{ t('Show the boards', 'Показать поле') }}
              </button>
              <RouterLink to="/battleship" class="bs-btn bs-btn--primary lobby-btn">
                {{ t('Back to lobby', 'Вернуться в лобби') }}
              </RouterLink>
            </div>
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.celebration-overlay {
  position: fixed;
  inset: 0;
  z-index: 3300;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  background:
    radial-gradient(circle at 50% 42%, rgba(35, 31, 46, 0.72), rgba(5, 5, 8, 0.94) 68%),
    rgba(0, 0, 0, 0.88);
  backdrop-filter: blur(10px);
}

.celebration-card {
  --rarity: #b5bcc4;
  position: relative;
  isolation: isolate;
  width: min(520px, 100%);
  max-height: calc(100svh - 32px);
  overflow: hidden auto;
  padding: 34px 34px 28px;
  color: var(--text-primary);
  border: 1px solid color-mix(in srgb, var(--rarity) 60%, transparent);
  border-radius: 22px;
  outline: none;
  background: linear-gradient(155deg, color-mix(in srgb, var(--rarity) 10%, #28252e), #18171d 68%);
  box-shadow:
    0 28px 90px rgba(0, 0, 0, 0.7),
    0 0 55px color-mix(in srgb, var(--rarity) 20%, transparent),
    inset 0 1px 0 rgba(255, 255, 255, 0.09);
  animation: celebration-card-in 0.7s var(--ease-spring) both;
}
.celebration-card.is-win { --rarity: #f3c85b; }
.celebration-card.is-loss { --rarity: #8b93a5; }

.celebration-aurora {
  position: absolute;
  z-index: -2;
  inset: -35% -20% 35%;
  background: radial-gradient(ellipse, color-mix(in srgb, var(--rarity) 34%, transparent), transparent 68%);
  filter: blur(18px);
  animation: aurora-breathe 2.8s ease-in-out infinite;
}

.celebration-rays {
  position: absolute;
  z-index: -1;
  top: -160px;
  left: 50%;
  width: 420px;
  height: 420px;
  transform: translateX(-50%);
  opacity: 0.16;
  background: repeating-conic-gradient(from 0deg, var(--rarity) 0deg 2deg, transparent 2deg 18deg);
  mask-image: radial-gradient(circle, black 5%, transparent 68%);
  animation: rays-turn 22s linear infinite;
}

.skip-button {
  position: absolute;
  z-index: 5;
  top: 12px;
  right: 12px;
  width: 38px;
  height: 38px;
  display: grid;
  place-items: center;
  color: rgba(255, 255, 255, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.2);
  cursor: pointer;
}
.skip-button:hover { color: white; border-color: rgba(255, 255, 255, 0.3); background: rgba(255, 255, 255, 0.05); }

.celebration-content { position: relative; z-index: 2; display: flex; align-items: center; flex-direction: column; text-align: center; }
.unlock-kicker { display: inline-flex; align-items: center; gap: 8px; color: var(--rarity); font-size: 11px; font-weight: 900; letter-spacing: 2.5px; text-transform: uppercase; animation: content-rise 0.45s ease 0.2s both; }
.unlock-icon-stage { position: relative; width: 148px; height: 148px; display: grid; place-items: center; margin: 14px 0 8px; }
.icon-orbit { position: absolute; inset: 8px; border: 1px solid color-mix(in srgb, var(--rarity) 36%, transparent); border-radius: 50%; animation: orbit-pulse 1.9s ease-in-out infinite; }
.icon-orbit-two { inset: 0; border-style: dashed; opacity: 0.5; animation-delay: -0.8s; animation-direction: reverse; }
.unlock-icon { position: relative; width: 96px; height: 96px; display: grid; place-items: center; color: var(--rarity); border: 2px solid color-mix(in srgb, var(--rarity) 70%, #fff); border-radius: 25px; background: linear-gradient(145deg, color-mix(in srgb, var(--rarity) 18%, #302d36), #1d1b22); box-shadow: 0 0 35px color-mix(in srgb, var(--rarity) 30%, transparent), inset 0 1px 0 rgba(255, 255, 255, 0.12); animation: icon-land 0.75s var(--ease-spring) 0.12s both; }

.celebration-content h2 { margin: 5px 0 3px; color: white; font-size: clamp(27px, 6vw, 36px); font-weight: 950; line-height: 1.12; text-shadow: 0 0 18px color-mix(in srgb, var(--rarity) 24%, transparent); animation: content-rise 0.45s ease 0.35s both; }
.winner-line { margin: 0; color: rgba(255, 255, 255, 0.55); font-size: 12px; animation: content-rise 0.45s ease 0.42s both; }

/* Stats grid */
.stats-grid {
  width: 100%;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.09);
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.16);
  margin-top: 16px;
  animation: content-rise 0.45s ease 0.5s both;
}
.stat-tile {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding: 10px 6px;
  border-right: 1px solid rgba(255, 255, 255, 0.07);
}
.stat-tile:last-child { border-right: 0; }
.stat-tile small {
  color: rgba(255, 255, 255, 0.4);
  font-size: 8px;
  font-weight: 800;
  letter-spacing: 0.4px;
  text-transform: uppercase;
}
.stat-tile strong {
  color: rgba(255, 255, 255, 0.9);
  font-size: 14px;
  font-weight: 900;
}

/* Reward panel */
.reward-panel {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 8px;
  width: 100%;
  margin-top: 14px;
  padding: 11px;
  border: 1px solid color-mix(in srgb, var(--rarity) 18%, rgba(255, 255, 255, 0.09));
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.18);
  animation: reward-arrive 0.55s var(--ease-spring) 0.62s both;
}
.reward-heading {
  width: 100%;
  color: rgba(255, 255, 255, 0.38);
  font-size: 8px;
  font-weight: 850;
  letter-spacing: 1.5px;
  text-transform: uppercase;
}
.record-chip { --bs-chip-color: #cfd6e4; font-size: 0.72rem; }
.streak-chip { font-size: 0.72rem; }
.zbs-chip {
  font-size: 0.72rem;
  min-height: 32px;
}
.zbs-chip img { width: 20px; height: 20px; object-fit: contain; }
.zbs-chip strong { font-size: 15px; font-weight: 900; }

.celebration-actions {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: 8px;
  width: 100%;
  margin-top: 17px;
  animation: content-rise 0.45s ease 0.72s both;
}
.celebration-actions .bs-btn { min-height: 42px; flex: 1; }
.reveal-btn {
  color: rgba(255, 255, 255, 0.75);
  border-color: rgba(255, 255, 255, 0.14);
}
.lobby-btn {
  --bs-accent: var(--rarity);
  color: #17151b;
}

.celebration-particles { position: absolute; z-index: 1; top: 105px; left: 50%; width: 1px; height: 1px; pointer-events: none; }
.celebration-particles i { --particle-angle: 0deg; --particle-distance: 120px; --particle-delay: 0ms; --particle-size: 5px; position: absolute; width: var(--particle-size); height: var(--particle-size); border-radius: 50%; background: var(--rarity); box-shadow: 0 0 8px var(--rarity); animation: particle-burst 1.1s ease-out var(--particle-delay) both; }

@keyframes celebration-card-in { from { opacity: 0; transform: scale(0.72) translateY(26px); } 65% { transform: scale(1.025) translateY(-3px); } to { opacity: 1; transform: scale(1) translateY(0); } }
@keyframes icon-land { from { opacity: 0; transform: scale(0.25) rotate(-18deg); } 65% { transform: scale(1.12) rotate(3deg); } to { opacity: 1; transform: scale(1) rotate(0); } }
@keyframes content-rise { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes reward-arrive { from { opacity: 0; transform: scale(0.82); } to { opacity: 1; transform: scale(1); } }
@keyframes orbit-pulse { 0%, 100% { transform: scale(0.92) rotate(0); opacity: 0.25; } 50% { transform: scale(1.06) rotate(12deg); opacity: 0.7; } }
@keyframes aurora-breathe { 0%, 100% { opacity: 0.5; transform: scale(0.9); } 50% { opacity: 0.9; transform: scale(1.08); } }
@keyframes rays-turn { to { transform: translateX(-50%) rotate(360deg); } }
@keyframes particle-burst { from { opacity: 1; transform: rotate(var(--particle-angle)) translateX(30px) scale(1); } to { opacity: 0; transform: rotate(var(--particle-angle)) translateX(var(--particle-distance)) scale(0); } }

.bs-celebration-enter-active { transition: opacity 0.3s ease; }
.bs-celebration-leave-active { transition: opacity 0.25s ease; }
.bs-celebration-enter-from,
.bs-celebration-leave-to { opacity: 0; }

@media (max-width: 560px) {
  .celebration-overlay { align-items: flex-end; padding: 8px; }
  .celebration-card { width: 100%; max-height: calc(100svh - 8px); padding: 28px 18px 20px; border-radius: 20px 20px 12px 12px; }
  .unlock-icon-stage { width: 125px; height: 125px; margin-top: 10px; }
  .unlock-icon { width: 84px; height: 84px; }
  .celebration-content h2 { font-size: 27px; }
  .stats-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
  .stat-tile { border-bottom: 1px solid rgba(255, 255, 255, 0.07); }
  .celebration-actions { align-items: stretch; flex-direction: column-reverse; }
  .celebration-actions .bs-btn { min-height: 46px; width: 100%; }
}

@media (prefers-reduced-motion: reduce) {
  .celebration-card,
  .celebration-aurora,
  .celebration-rays,
  .unlock-kicker,
  .unlock-icon,
  .winner-line,
  .celebration-content h2,
  .stats-grid,
  .reward-panel,
  .celebration-actions,
  .icon-orbit,
  .celebration-particles i { animation: none; }
  .celebration-particles { display: none; }
  .bs-celebration-enter-active,
  .bs-celebration-leave-active { transition: none; }
}
</style>
