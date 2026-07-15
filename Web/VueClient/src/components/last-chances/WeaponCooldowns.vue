<script setup lang="ts">
import { computed } from 'vue'
import { CircleDot, MousePointerClick, TimerReset } from 'lucide-vue-next'
import type { LastChancesGestureInputSnapshot } from '../../features/last-chances'
import type { LastChancesLocale } from './RunMapOverlay.vue'
import type { AttackHand } from './TouchControls.vue'

export type GestureKey = 'tap' | 'doubleTap' | 'doubleTapHold' | 'hold' | 'holdThenDoubleTap'

export type GestureCooldown = {
  key: GestureKey
  name: string
  remainingMs: number
  totalMs: number
  active?: boolean
}

export type WeaponCooldown = {
  hand: AttackHand
  name: string
  gestures: GestureCooldown[]
  input?: LastChancesGestureInputSnapshot
}

const props = defineProps<{
  locale: LastChancesLocale
  weapons: WeaponCooldown[]
}>()

const copy = {
  en: {
    title: 'Gesture memory',
    subtitle: 'One button, five intentions per hand',
    primary: 'Primary hand',
    secondary: 'Secondary hand',
    ready: 'Ready',
    cooling: 'Cooldown',
    empty: 'Waiting for the loadout',
    input: {
      idle: 'Ready for input',
      pressing: 'Holding · release or keep charging',
      doubleTapWindow: 'Tap again for double tap',
      secondPress: 'Second press · release or hold',
      holdFollowUpWindow: 'Tap once now for the hold follow-up',
      holdFollowUp: 'Hold follow-up registered',
    },
    gestures: {
      tap: 'Tap',
      doubleTap: 'Double tap',
      doubleTapHold: 'Double + hold',
      hold: 'Hold / release',
      holdThenDoubleTap: 'Hold + double tap',
    },
  },
  ru: {
    title: 'Память жестов',
    subtitle: 'Одна кнопка — пять намерений для каждой руки',
    primary: 'Основная рука',
    secondary: 'Вторая рука',
    ready: 'Готово',
    cooling: 'Откат',
    empty: 'Ожидание экипировки',
    input: {
      idle: 'Ожидание нажатия',
      pressing: 'Удержание · отпустите или продолжайте заряд',
      doubleTapWindow: 'Нажмите ещё раз для даблтапа',
      secondPress: 'Второе нажатие · отпустите или удерживайте',
      holdFollowUpWindow: 'Нажмите один раз для продолжения задержки',
      holdFollowUp: 'Продолжение задержки принято',
    },
    gestures: {
      tap: 'Нажатие',
      doubleTap: 'Двойное',
      doubleTapHold: 'Двойное + задержка',
      hold: 'Задержка / отпускание',
      holdThenDoubleTap: 'Задержка + двойное нажатие',
    },
  },
} as const

const t = computed(() => copy[props.locale])

function cooldownPercent(gesture: GestureCooldown): number {
  if (gesture.remainingMs <= 0 || gesture.totalMs <= 0) return 100
  return Math.max(0, Math.min(100, 100 - (gesture.remainingMs / gesture.totalMs) * 100))
}

function remainingLabel(gesture: GestureCooldown): string {
  if (gesture.remainingMs <= 0) return t.value.ready
  return `${(gesture.remainingMs / 1000).toFixed(gesture.remainingMs < 1000 ? 1 : 0)}s`
}
</script>

<template>
  <section class="lc-cooldowns" :aria-label="t.title">
    <header class="lc-cooldown-heading">
      <div class="lc-cooldown-mark"><MousePointerClick :size="16" aria-hidden="true" /></div>
      <div>
        <h2>{{ t.title }}</h2>
        <p>{{ t.subtitle }}</p>
      </div>
    </header>

    <div v-if="weapons.length" class="lc-weapon-list">
      <article
        v-for="weapon in weapons"
        :key="weapon.hand"
        class="lc-weapon"
        :class="`is-${weapon.hand}`"
      >
        <header>
          <span class="lc-hand-key">{{ weapon.hand === 'primary' ? 'L' : 'R' }}</span>
          <div>
            <small>{{ weapon.hand === 'primary' ? t.primary : t.secondary }}</small>
            <h3>{{ weapon.name }}</h3>
          </div>
          <CircleDot :size="15" aria-hidden="true" />
        </header>

        <div
          class="lc-input-feedback"
          :class="[`is-${weapon.input?.phase ?? 'idle'}`, { 'is-pressed': weapon.input?.pressed }]"
          role="status"
        >
          <span>{{ t.input[weapon.input?.phase ?? 'idle'] }}</span>
          <small v-if="weapon.input?.remainingMs">{{ Math.ceil(weapon.input.remainingMs) }} ms</small>
          <i aria-hidden="true"><b :style="{ width: `${(weapon.input?.progress ?? 0) * 100}%` }" /></i>
        </div>

        <ol class="lc-gesture-list">
          <li
            v-for="gesture in weapon.gestures"
            :key="gesture.key"
            :class="{ 'is-ready': gesture.remainingMs <= 0, 'is-active': gesture.active }"
          >
            <div class="lc-gesture-copy">
              <span class="lc-gesture-index">{{ weapon.gestures.indexOf(gesture) + 1 }}</span>
              <span class="lc-gesture-name">
                <small>{{ t.gestures[gesture.key] }}</small>
                <strong>{{ gesture.name }}</strong>
              </span>
              <span class="lc-gesture-time">
                <TimerReset v-if="gesture.remainingMs > 0" :size="11" aria-hidden="true" />
                {{ remainingLabel(gesture) }}
              </span>
            </div>
            <span class="lc-cooldown-track" aria-hidden="true">
              <i :style="{ width: `${cooldownPercent(gesture)}%` }" />
            </span>
            <span class="sr-only">
              {{ gesture.remainingMs <= 0 ? t.ready : `${t.cooling}: ${remainingLabel(gesture)}` }}
            </span>
          </li>
        </ol>
      </article>
    </div>

    <p v-else class="lc-cooldown-empty">{{ t.empty }}</p>
  </section>
</template>

<style scoped>
.lc-cooldowns {
  min-width: 0;
  display: grid;
  gap: 0.7rem;
}

.lc-cooldown-heading {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.lc-cooldown-mark {
  width: 2rem;
  height: 2rem;
  display: grid;
  place-items: center;
  border: 1px solid rgba(190, 158, 88, 0.28);
  border-radius: 50%;
  color: #c8a45e;
  background: rgba(181, 135, 50, 0.08);
}

.lc-cooldown-heading h2 {
  margin: 0;
  color: #dedbd2;
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

.lc-cooldown-heading p {
  margin: 0.08rem 0 0;
  color: #626765;
  font-size: 0.58rem;
}

.lc-weapon-list { display: grid; gap: 0.65rem; }

.lc-weapon {
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.07);
  border-radius: 0.65rem;
  background: rgba(9, 11, 12, 0.54);
  box-shadow: inset 0 1px rgba(255, 255, 255, 0.02);
}

.lc-weapon > header {
  display: grid;
  grid-template-columns: auto 1fr auto;
  align-items: center;
  gap: 0.55rem;
  padding: 0.55rem 0.65rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.055);
  color: #6d716f;
  background: linear-gradient(90deg, rgba(176, 137, 59, 0.08), transparent);
}

.lc-weapon.is-secondary > header { background: linear-gradient(90deg, rgba(135, 48, 55, 0.1), transparent); }
.lc-hand-key { width: 1.65rem; height: 1.65rem; display: grid; place-items: center; border: 1px solid rgba(205, 170, 94, 0.35); border-radius: 0.42rem; color: #d1b373; font: 800 0.68rem/1 var(--font-mono, monospace); }
.is-secondary .lc-hand-key { border-color: rgba(170, 68, 77, 0.45); color: #c77479; }
.lc-weapon header small { display: block; color: #666b69; font-size: 0.48rem; font-weight: 800; letter-spacing: 0.09em; text-transform: uppercase; }
.lc-weapon header h3 { max-width: 13rem; margin: 0.08rem 0 0; overflow: hidden; color: #dedbd2; font-size: 0.7rem; font-weight: 700; text-overflow: ellipsis; white-space: nowrap; }

.lc-input-feedback { position: relative; display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 0.35rem; padding: 0.35rem 0.6rem 0.42rem; overflow: hidden; border-bottom: 1px solid rgba(255, 255, 255, 0.04); color: #666b69; background: rgba(255, 255, 255, 0.015); font-size: 0.5rem; }
.lc-input-feedback span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.lc-input-feedback small { color: #aa9469; font: 700 0.47rem/1 var(--font-mono, monospace); }
.lc-input-feedback > i { position: absolute; inset: auto 0 0; height: 2px; background: rgba(255, 255, 255, 0.035); }
.lc-input-feedback > i b { display: block; height: 100%; background: #c7a45d; transition: width 0.06s linear; }
.is-secondary .lc-input-feedback > i b { background: #b35d63; }
.lc-input-feedback:not(.is-idle) { color: #ddd6c5; background: rgba(190, 153, 77, 0.075); }
.is-secondary .lc-input-feedback:not(.is-idle) { background: rgba(158, 57, 65, 0.085); }
.lc-input-feedback.is-pressed { box-shadow: inset 0 0 0.8rem rgba(215, 180, 104, 0.06); }

.lc-gesture-list { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
.lc-gesture-list li { position: relative; display: grid; padding: 0.4rem 0.55rem 0.34rem; border-bottom: 1px solid rgba(255, 255, 255, 0.035); opacity: 0.67; }
.lc-gesture-list li:last-child { border-bottom: 0; }
.lc-gesture-list li.is-ready { opacity: 1; }
.lc-gesture-list li.is-active { background: rgba(202, 170, 99, 0.08); }

.lc-gesture-copy { min-width: 0; display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 0.45rem; }
.lc-gesture-index { width: 1.18rem; height: 1.18rem; display: grid; place-items: center; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 50%; color: #626764; font: 700 0.47rem/1 var(--font-mono, monospace); }
.is-ready .lc-gesture-index { color: #c7a866; border-color: rgba(196, 158, 78, 0.3); }
.lc-gesture-name { min-width: 0; display: grid; }
.lc-gesture-name small { color: #5f6462; font-size: 0.46rem; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
.lc-gesture-name strong { overflow: hidden; color: #b6b7b1; font-size: 0.58rem; font-weight: 650; line-height: 1.2; text-overflow: ellipsis; white-space: nowrap; }
.is-ready .lc-gesture-name strong { color: #dfdcd3; }
.lc-gesture-time { display: inline-flex; align-items: center; gap: 0.2rem; color: #9b7778; font: 700 0.5rem/1 var(--font-mono, monospace); }
.is-ready .lc-gesture-time { color: #8c956f; }

.lc-cooldown-track { height: 2px; margin: 0.3rem 0 0 1.65rem; overflow: hidden; border-radius: 999px; background: rgba(255, 255, 255, 0.045); }
.lc-cooldown-track i { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, #6c4923, #c7a45d); box-shadow: 0 0 0.35rem rgba(205, 165, 81, 0.35); transition: width 0.12s linear; }
.is-secondary .lc-cooldown-track i { background: linear-gradient(90deg, #572229, #b35d63); box-shadow: 0 0 0.35rem rgba(178, 78, 86, 0.35); }
.lc-cooldown-empty { margin: 0; padding: 1rem; color: #686c6a; font-size: 0.65rem; text-align: center; }

.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

@media (max-width: 1100px) {
  .lc-weapon-list { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}

@media (max-width: 650px) {
  .lc-cooldown-heading { display: none; }
  .lc-weapon-list { gap: 0.35rem; }
  .lc-weapon > header { padding: 0.35rem 0.45rem; }
  .lc-hand-key { width: 1.35rem; height: 1.35rem; }
  .lc-weapon header small { display: none; }
  .lc-gesture-list li { padding: 0.27rem 0.38rem; }
  .lc-gesture-name small { display: none; }
  .lc-gesture-name strong { font-size: 0.5rem; }
  .lc-cooldown-track { margin-left: 1.5rem; }
}

@media (prefers-reduced-motion: reduce) {
  .lc-cooldown-track i,
  .lc-input-feedback > i b { transition: none; }
}
</style>
