<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch, type CSSProperties } from 'vue'
import { Crown, Sparkles } from 'lucide-vue-next'
import ScoreOdometer from 'src/components/ScoreOdometer.vue'
import { message } from 'src/platform/localization'
import type { RankedEloSettlement } from 'src/services/signalr'

const props = defineProps<{
  settlement: RankedEloSettlement
}>()

const reducedMotion = typeof window !== 'undefined'
  && window.matchMedia('(prefers-reduced-motion: reduce)').matches
const displayedRating = ref(reducedMotion
  ? props.settlement.ratingAfter
  : props.settlement.ratingBefore)
let ratingRevealTimer: ReturnType<typeof setTimeout> | null = null
let mounted = false

const resultTone = computed(() => props.settlement.delta > 0
  ? 'positive'
  : props.settlement.delta < 0
    ? 'negative'
    : 'neutral')
const isFirstPlace = computed(() => props.settlement.finalPlace === 1)
const particleCount = computed(() => isFirstPlace.value ? 56 : 30)

function signed(value: number): string {
  if (value > 0) return `+${value}`
  if (value < 0) return `−${Math.abs(value)}`
  return '±0'
}

function valueTone(value: number): string {
  if (value > 0) return 'positive'
  if (value < 0) return 'negative'
  return 'neutral'
}

function particleStyle(index: number): CSSProperties {
  return {
    '--elo-particle-x': `${(index * 47 + 13) % 101}%`,
    '--elo-particle-delay': `${((index * 83) % 900) / 1000}s`,
    '--elo-particle-duration': `${2.15 + (index % 6) * 0.19}s`,
    '--elo-particle-drift': `${-65 + (index * 29) % 131}px`,
    '--elo-particle-turn': `${360 + (index % 5) * 144}deg`,
    '--elo-particle-color': isFirstPlace.value
      ? `hsl(${38 + (index % 5) * 8} 94% ${58 + (index % 3) * 8}%)`
      : `hsl(${118 + (index % 5) * 9} 78% ${54 + (index % 3) * 8}%)`,
  } as CSSProperties
}

function rainStyle(index: number): CSSProperties {
  return {
    '--elo-rain-x': `${(index * 61 + 7) % 101}%`,
    '--elo-rain-delay': `${((index * 71) % 1200) / 1000}s`,
    '--elo-rain-duration': `${1.3 + (index % 5) * 0.22}s`,
  } as CSSProperties
}

onMounted(() => {
  mounted = true
  if (reducedMotion) return
  ratingRevealTimer = setTimeout(() => {
    displayedRating.value = props.settlement.ratingAfter
    ratingRevealTimer = null
  }, 520)
})

watch(() => props.settlement.ratingAfter, (ratingAfter) => {
  if (!mounted || ratingAfter === displayedRating.value) return
  if (ratingRevealTimer) {
    clearTimeout(ratingRevealTimer)
    ratingRevealTimer = null
  }
  displayedRating.value = ratingAfter
})

onUnmounted(() => {
  mounted = false
  if (ratingRevealTimer) clearTimeout(ratingRevealTimer)
})
</script>

<template>
  <Teleport to="body">
    <section
      class="ranked-elo-overlay"
      :class="[`is-${resultTone}`, { 'is-first-place': isFirstPlace }]"
      role="status"
      aria-live="polite"
      aria-atomic="true"
    >
      <div class="ranked-elo-backdrop" aria-hidden="true" />
      <div v-if="isFirstPlace" class="ranked-elo-rays" aria-hidden="true" />
      <div v-if="isFirstPlace" class="ranked-elo-shockwave" aria-hidden="true" />

      <div v-if="settlement.delta > 0" class="ranked-elo-particles" aria-hidden="true">
        <i
          v-for="index in particleCount"
          :key="index"
          :style="particleStyle(index)"
        />
      </div>
      <div v-else-if="settlement.delta < 0" class="ranked-elo-rain" aria-hidden="true">
        <i v-for="index in 24" :key="index" :style="rainStyle(index)" />
      </div>

      <div class="ranked-elo-card">
        <div class="ranked-elo-kicker">
          <Sparkles :size="15" aria-hidden="true" />
          ELO
          <Sparkles :size="15" aria-hidden="true" />
        </div>

        <div v-if="isFirstPlace" class="ranked-elo-crown" aria-hidden="true">
          <Crown :size="68" :stroke-width="1.55" />
        </div>

        <h2>
          {{ message(isFirstPlace ? 'kotgh.ranked.elo.firstPlaceTitle' : 'kotgh.ranked.elo.title') }}
        </h2>

        <div class="ranked-elo-rating" :aria-label="`ELO ${settlement.ratingAfter}`">
          <span>ELO</span>
          <ScoreOdometer :value="displayedRating" size="lg" />
        </div>

        <div class="ranked-elo-match-delta" :class="`is-${resultTone}`">
          <span>{{ message('kotgh.ranked.elo.matchDelta') }}</span>
          <strong>{{ signed(settlement.delta) }}</strong>
        </div>

        <div class="ranked-elo-breakdown">
          <div>
            <span>{{ message('kotgh.ranked.elo.placementDelta') }}</span>
            <strong :class="`is-${valueTone(settlement.placementDelta)}`">
              {{ signed(settlement.placementDelta) }}
            </strong>
          </div>
          <div v-if="settlement.shinigamiPenalty !== 0">
            <span>{{ message('kotgh.ranked.elo.shinigamiPenalty') }}</span>
            <strong class="is-negative">{{ signed(settlement.shinigamiPenalty) }}</strong>
          </div>
          <div v-if="settlement.blackjackRecovery !== 0">
            <span>{{ message('kotgh.ranked.elo.blackjackRecovery') }}</span>
            <strong class="is-positive">{{ signed(settlement.blackjackRecovery) }}</strong>
          </div>
        </div>
      </div>
    </section>
  </Teleport>
</template>

<style scoped>
.ranked-elo-overlay {
  --elo-tone: #aab1bf;
  position: fixed;
  z-index: 3600;
  inset: 0;
  display: grid;
  overflow: hidden;
  place-items: center;
  padding: 18px;
  pointer-events: auto;
  color: #f6f7fa;
  animation: ranked-elo-scene 4.5s ease both;
}

.ranked-elo-overlay.is-positive { --elo-tone: #58df88; }
.ranked-elo-overlay.is-negative { --elo-tone: #ff5f6f; }
.ranked-elo-overlay.is-first-place { --elo-tone: #ffd966; }

.ranked-elo-backdrop {
  position: absolute;
  inset: 0;
  background:
    radial-gradient(circle at 50% 44%, color-mix(in srgb, var(--elo-tone) 25%, transparent), transparent 38%),
    linear-gradient(180deg, rgba(5, 7, 11, 0.91), rgba(3, 4, 8, 0.96));
  backdrop-filter: blur(9px);
}

.is-negative .ranked-elo-backdrop {
  background:
    radial-gradient(circle at 50% 44%, rgba(151, 22, 40, 0.34), transparent 40%),
    linear-gradient(180deg, rgba(13, 4, 8, 0.94), rgba(5, 2, 5, 0.98));
}

.ranked-elo-card {
  position: relative;
  z-index: 3;
  display: flex;
  align-items: center;
  flex-direction: column;
  width: min(620px, 100%);
  padding: clamp(28px, 5vw, 48px);
  border: 1px solid color-mix(in srgb, var(--elo-tone) 58%, transparent);
  border-radius: 26px;
  text-align: center;
  background: linear-gradient(155deg, color-mix(in srgb, var(--elo-tone) 13%, #242832), #111319 72%);
  box-shadow:
    0 28px 100px rgba(0, 0, 0, 0.72),
    0 0 64px color-mix(in srgb, var(--elo-tone) 25%, transparent),
    inset 0 1px 0 rgba(255, 255, 255, 0.12);
  animation: ranked-elo-card-in 0.7s var(--ease-spring) both;
}

.is-first-place .ranked-elo-card {
  border-width: 2px;
  background:
    linear-gradient(155deg, rgba(112, 76, 15, 0.42), rgba(24, 19, 10, 0.98) 70%);
  box-shadow:
    0 30px 110px rgba(0, 0, 0, 0.78),
    0 0 96px rgba(255, 202, 73, 0.38),
    inset 0 1px 0 rgba(255, 247, 194, 0.28);
}

.ranked-elo-kicker {
  display: inline-flex;
  align-items: center;
  gap: 9px;
  color: var(--elo-tone);
  font: 900 11px/1 var(--font-mono);
  letter-spacing: 4px;
  text-shadow: 0 0 14px color-mix(in srgb, var(--elo-tone) 55%, transparent);
}

.ranked-elo-crown {
  display: grid;
  margin: 14px 0 -2px;
  place-items: center;
  color: #ffe38a;
  filter: drop-shadow(0 0 18px rgba(255, 199, 55, 0.72));
  animation: ranked-elo-crown 1.45s ease-in-out infinite alternate;
}

.ranked-elo-card h2 {
  margin: 15px 0 18px;
  color: #fff;
  font: 950 clamp(28px, 6vw, 50px)/1.02 var(--font-display, sans-serif);
  letter-spacing: -0.035em;
  text-wrap: balance;
  text-shadow: 0 0 26px color-mix(in srgb, var(--elo-tone) 38%, transparent);
}

.ranked-elo-rating {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 14px;
  min-width: min(360px, 100%);
  padding: 15px 24px;
  border: 1px solid color-mix(in srgb, var(--elo-tone) 42%, transparent);
  border-radius: 16px;
  color: #fff;
  background: rgba(0, 0, 0, 0.24);
  box-shadow: inset 0 0 30px color-mix(in srgb, var(--elo-tone) 8%, transparent);
}

.ranked-elo-rating > span {
  color: var(--elo-tone);
  font: 900 16px/1 var(--font-mono);
  letter-spacing: 3px;
}

.ranked-elo-rating :deep(.odo-lg) {
  height: 1.05em;
  font-size: clamp(42px, 10vw, 74px);
  text-shadow: 0 0 24px color-mix(in srgb, var(--elo-tone) 42%, transparent);
}

.ranked-elo-match-delta {
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 10px;
  margin-top: 16px;
  color: rgba(235, 238, 245, 0.68);
  font-size: 13px;
  font-weight: 800;
}

.ranked-elo-match-delta strong {
  color: var(--elo-tone);
  font: 950 30px/1 var(--font-mono);
  text-shadow: 0 0 18px color-mix(in srgb, var(--elo-tone) 48%, transparent);
}

.ranked-elo-match-delta.is-positive strong {
  color: #67e596;
  text-shadow: 0 0 18px rgba(103, 229, 150, 0.48);
}

.ranked-elo-match-delta.is-negative strong {
  color: #ff6978;
  text-shadow: 0 0 18px rgba(255, 105, 120, 0.48);
}

.ranked-elo-breakdown {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: 7px;
  width: 100%;
  margin-top: 20px;
}

.ranked-elo-breakdown > div {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 36px;
  padding: 8px 12px;
  border: 1px solid rgba(255, 255, 255, 0.09);
  border-radius: 10px;
  color: rgba(234, 237, 244, 0.62);
  background: rgba(255, 255, 255, 0.035);
  font-size: 11px;
  font-weight: 750;
}

.ranked-elo-breakdown strong {
  color: #b8bfca;
  font: 900 14px/1 var(--font-mono);
}

.ranked-elo-breakdown .is-positive { color: #67e596; }
.ranked-elo-breakdown .is-negative { color: #ff6978; }
.ranked-elo-breakdown .is-neutral { color: #b8bfca; }

.ranked-elo-rays {
  position: absolute;
  z-index: 1;
  top: 50%;
  left: 50%;
  width: min(112vw, 1000px);
  aspect-ratio: 1;
  background: repeating-conic-gradient(from 0deg, rgba(255, 220, 116, 0.25) 0 2deg, transparent 2deg 13deg);
  mask-image: radial-gradient(circle, #000 4%, transparent 67%);
  transform: translate(-50%, -50%);
  animation: ranked-elo-rays 18s linear infinite;
}

.ranked-elo-shockwave {
  position: absolute;
  z-index: 2;
  width: min(64vw, 520px);
  aspect-ratio: 1;
  border: 2px solid rgba(255, 224, 132, 0.75);
  border-radius: 50%;
  box-shadow: 0 0 40px rgba(255, 198, 65, 0.4);
  animation: ranked-elo-shockwave 1.75s ease-out 0.45s both;
}

.ranked-elo-particles,
.ranked-elo-rain {
  position: absolute;
  z-index: 2;
  inset: 0;
  overflow: hidden;
}

.ranked-elo-particles i {
  position: absolute;
  top: -18px;
  left: var(--elo-particle-x);
  width: 8px;
  height: 14px;
  border-radius: 2px;
  background: var(--elo-particle-color);
  box-shadow: 0 0 9px color-mix(in srgb, var(--elo-particle-color) 65%, transparent);
  animation: ranked-elo-confetti var(--elo-particle-duration) ease-in var(--elo-particle-delay) infinite;
}

.ranked-elo-rain i {
  position: absolute;
  top: -50px;
  left: var(--elo-rain-x);
  width: 2px;
  height: 42px;
  background: linear-gradient(transparent, rgba(255, 73, 94, 0.82));
  filter: drop-shadow(0 0 5px rgba(255, 57, 80, 0.6));
  animation: ranked-elo-rain var(--elo-rain-duration) linear var(--elo-rain-delay) infinite;
}

@keyframes ranked-elo-scene {
  0% { opacity: 0; }
  9%, 88% { opacity: 1; }
  100% { opacity: 0; }
}

@keyframes ranked-elo-card-in {
  from { opacity: 0; transform: translateY(28px) scale(0.78); filter: blur(8px); }
  to { opacity: 1; transform: translateY(0) scale(1); filter: blur(0); }
}

@keyframes ranked-elo-crown {
  from { transform: translateY(2px) scale(0.97); }
  to { transform: translateY(-4px) scale(1.04); }
}

@keyframes ranked-elo-rays {
  to { transform: translate(-50%, -50%) rotate(360deg); }
}

@keyframes ranked-elo-shockwave {
  from { opacity: 0.85; transform: scale(0.45); }
  to { opacity: 0; transform: scale(1.85); }
}

@keyframes ranked-elo-confetti {
  from { opacity: 0; transform: translate3d(0, -3vh, 0) rotate(0); }
  10% { opacity: 1; }
  to { opacity: 0; transform: translate3d(var(--elo-particle-drift), 108vh, 0) rotate(var(--elo-particle-turn)); }
}

@keyframes ranked-elo-rain {
  from { opacity: 0; transform: translateY(-8vh); }
  12% { opacity: 1; }
  to { opacity: 0; transform: translateY(112vh); }
}

@media (max-width: 560px) {
  .ranked-elo-card { padding: 27px 17px; border-radius: 21px; }
  .ranked-elo-rating { min-width: 0; padding: 13px 17px; }
  .ranked-elo-breakdown { flex-direction: column; }
  .ranked-elo-breakdown > div { justify-content: space-between; width: 100%; }
}

@media (prefers-reduced-motion: reduce) {
  .ranked-elo-overlay,
  .ranked-elo-card,
  .ranked-elo-crown,
  .ranked-elo-rays,
  .ranked-elo-shockwave,
  .ranked-elo-particles i,
  .ranked-elo-rain i {
    animation: none !important;
  }
  .ranked-elo-rays,
  .ranked-elo-shockwave,
  .ranked-elo-particles,
  .ranked-elo-rain { display: none; }
  .ranked-elo-rating :deep(.odo-digit) { transition: none !important; }
}
</style>
