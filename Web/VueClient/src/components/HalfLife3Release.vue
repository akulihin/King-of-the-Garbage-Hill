<script setup lang="ts">
import { currentLocale } from 'src/i18n'

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

const bursts = [
  { x: 12, y: 28, delay: 0.05, color: '#ff9b38' },
  { x: 31, y: 17, delay: 0.55, color: '#ffd86b' },
  { x: 51, y: 25, delay: 0.2, color: '#f47c32' },
  { x: 72, y: 14, delay: 0.8, color: '#fff0a8' },
  { x: 89, y: 31, delay: 0.35, color: '#ffb84d' },
  { x: 22, y: 63, delay: 1.05, color: '#f7d56b' },
  { x: 79, y: 61, delay: 1.2, color: '#ff8b32' },
]
</script>

<template>
  <Teleport to="body">
    <div class="hl3-release" role="status" aria-live="assertive">
      <div class="hl3-release-glow" aria-hidden="true" />
      <div class="hl3-fireworks" aria-hidden="true">
        <div
          v-for="(burst, burstIndex) in bursts"
          :key="burstIndex"
          class="hl3-firework"
          :style="{
            '--burst-x': `${burst.x}%`,
            '--burst-y': `${burst.y}%`,
            '--burst-delay': `${burst.delay}s`,
            '--burst-color': burst.color,
          }"
        >
          <i v-for="spark in 16" :key="spark" :style="{ '--spark': spark - 1 }" />
        </div>
      </div>
      <div class="hl3-release-copy">
        <span class="hl3-release-kicker">BLACK MESA // WORLD PREMIERE</span>
        <strong>{{ t('HL3 has been released!', 'Релиз HL3 состоялся!') }}</strong>
        <p>
          {{ t('The fans gave the game a warm welcome.', 'Фанаты приняли игру тепло.') }}<br>
          {{ t(
            'Now let’s see whether it can beat the competition…',
            'Посмотрим, обойдет ли она конкурентов...',
          ) }}
        </p>
      </div>
      <div class="hl3-release-lambda" aria-hidden="true">λ</div>
    </div>
  </Teleport>
</template>

<style scoped>
.hl3-release {
  position: fixed;
  z-index: 12000;
  inset: 0;
  display: grid;
  overflow: hidden;
  place-items: center;
  pointer-events: none;
  color: #fff7df;
  background:
    radial-gradient(circle at 50% 48%, rgba(242, 132, 34, 0.24), transparent 38%),
    linear-gradient(180deg, rgba(2, 4, 5, 0.9), rgba(8, 5, 2, 0.84));
  animation: hl3-release-scene 5s ease both;
}

.hl3-release-glow {
  position: absolute;
  inset: 20% 8%;
  background: radial-gradient(ellipse, rgba(255, 161, 65, 0.2), transparent 68%);
  filter: blur(24px);
  animation: hl3-release-glow 1.4s ease-in-out infinite alternate;
}

.hl3-release-copy {
  position: relative;
  z-index: 3;
  display: flex;
  align-items: center;
  flex-direction: column;
  width: min(860px, calc(100% - 36px));
  padding: 32px 24px;
  text-align: center;
  text-shadow: 0 4px 26px #000, 0 0 30px rgba(255, 145, 48, 0.5);
  animation: hl3-release-copy 0.75s cubic-bezier(0.2, 0.9, 0.2, 1.15) both;
}

.hl3-release-kicker {
  margin-bottom: 14px;
  color: #ffaf51;
  font: 800 clamp(10px, 1.4vw, 14px)/1.2 var(--font-mono);
  letter-spacing: clamp(3px, 0.8vw, 8px);
}

.hl3-release-copy strong {
  color: #fff3c9;
  font: 950 clamp(38px, 8vw, 88px)/0.98 var(--font-display, sans-serif);
  letter-spacing: -0.035em;
  text-wrap: balance;
}

.hl3-release-copy p {
  margin: 22px 0 0;
  color: rgba(255, 248, 226, 0.88);
  font: 700 clamp(15px, 2.3vw, 24px)/1.45 var(--font-body, sans-serif);
  text-wrap: balance;
}

.hl3-release-lambda {
  position: absolute;
  z-index: 1;
  color: rgba(247, 133, 31, 0.09);
  font: 950 min(74vw, 760px)/1 Arial, sans-serif;
  transform: rotate(-8deg);
  animation: hl3-release-lambda 5s ease-out both;
}

.hl3-fireworks,
.hl3-firework {
  position: absolute;
  inset: 0;
}

.hl3-firework {
  top: var(--burst-y);
  left: var(--burst-x);
  width: 1px;
  height: 1px;
}

.hl3-firework i {
  --angle: calc(var(--spark) * 22.5deg);
  position: absolute;
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: var(--burst-color);
  box-shadow: 0 0 9px var(--burst-color), 0 0 18px var(--burst-color);
  opacity: 0;
  animation: hl3-firework-spark 1.25s ease-out var(--burst-delay) 3 both;
}

@keyframes hl3-firework-spark {
  0% { opacity: 0; transform: rotate(var(--angle)) translateX(0) scale(0.3); }
  12% { opacity: 1; }
  100% { opacity: 0; transform: rotate(var(--angle)) translateX(clamp(54px, 9vw, 125px)) scale(0.05); }
}

@keyframes hl3-release-scene {
  0% { opacity: 0; }
  8%, 84% { opacity: 1; }
  100% { opacity: 0; }
}

@keyframes hl3-release-copy {
  from { opacity: 0; transform: scale(0.55) translateY(26px); filter: blur(10px); }
  to { opacity: 1; transform: scale(1) translateY(0); filter: blur(0); }
}

@keyframes hl3-release-glow {
  from { opacity: 0.55; transform: scale(0.9); }
  to { opacity: 1; transform: scale(1.08); }
}

@keyframes hl3-release-lambda {
  from { opacity: 0; transform: rotate(-8deg) scale(0.65); }
  20%, 100% { opacity: 1; transform: rotate(-8deg) scale(1); }
}

@media (max-width: 600px) {
  .hl3-release-copy { padding-inline: 14px; }
  .hl3-firework i { width: 4px; height: 4px; }
}

@media (prefers-reduced-motion: reduce) {
  .hl3-release,
  .hl3-release-copy,
  .hl3-release-glow,
  .hl3-release-lambda,
  .hl3-firework i {
    animation: none !important;
  }
  .hl3-fireworks { display: none; }
}
</style>
