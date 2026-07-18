<script setup lang="ts">
import { computed } from 'vue'
import { CircleDot, Gauge, Lock, MousePointerClick, TimerReset, Zap } from 'lucide-vue-next'
import type {
  LastChancesControlRoleSnapshot,
  LastChancesControlScheme,
  LastChancesGestureInputSnapshot,
  LastChancesHandActionCue,
  LastChancesSemanticControlCue,
  LastChancesWeaponStateSnapshot,
} from '../../features/last-chances'
import type { LastChancesLocale } from './RunMapOverlay.vue'
import type { AttackHand } from './TouchControls.vue'

export type GestureKey = 'tap' | 'doubleTap' | 'doubleTapHold' | 'hold' | 'holdThenDoubleTap'

export type GestureCooldown = {
  key: GestureKey
  physicalLabel?: string
  name: string
  description?: string
  remainingMs: number
  totalMs: number
  enabled: boolean
  ready: boolean
  color: string
  active?: boolean
  /** Still behind the move-unlock quest chain. */
  locked?: boolean
  contextDimmed?: boolean
  primed?: boolean
}

export type WeaponCooldown = {
  hand: AttackHand
  name: string
  gestures: GestureCooldown[]
  input?: LastChancesGestureInputSnapshot
  cue?: LastChancesHandActionCue
  controlCue?: LastChancesSemanticControlCue
  controlRole?: LastChancesControlRoleSnapshot
  state?: LastChancesWeaponStateSnapshot
  chargeMaxMs?: number
}

const props = defineProps<{
  locale: LastChancesLocale
  controlScheme: LastChancesControlScheme
  weapons: WeaponCooldown[]
}>()

const copy = {
  en: {
    title: 'Gesture memory',
    subtitle: 'One button, five intentions per hand',
    schemeTitles: {
      legacy: 'Gesture memory',
      mylorik: 'Control routes',
      dualsense: 'Trigger routes',
    },
    schemeSubtitles: {
      legacy: 'One button, five intentions per hand',
      mylorik: 'Immediate strikes and visible technique branches',
      dualsense: 'Instant bumpers and authored combo gates',
    },
    nextGate: 'Next gate',
    primary: 'Primary hand',
    secondary: 'Secondary hand',
    ready: 'Ready',
    cooling: 'Cooldown',
    unavailable: 'Unavailable',
    locked: 'Quest locked',
    empty: 'Waiting for the loadout',
    charge: 'Charge',
    recovery: 'Recovery',
    storedDot: 'Stored DOT',
    resource: 'Weapon resource',
    rhythm: {
      idle: 'Idle',
      early: 'Too early',
      good: 'On rhythm',
      late: 'Too late',
    },
    motionBonus: 'Aim-motion damage',
    unterhauReady: 'Unterhaw primed',
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
      holdThenDoubleTap: 'Hold + tap',
    },
  },
  ru: {
    title: 'Память жестов',
    subtitle: 'Одна кнопка — пять намерений для каждой руки',
    schemeTitles: {
      legacy: 'Память жестов',
      mylorik: 'Маршруты управления',
      dualsense: 'Маршруты триггеров',
    },
    schemeSubtitles: {
      legacy: 'Одна кнопка — пять намерений для каждой руки',
      mylorik: 'Мгновенные удары и видимые ветки техник',
      dualsense: 'Мгновенные бамперы и авторские комбо-гейты',
    },
    nextGate: 'Следующий гейт',
    primary: 'Основная рука',
    secondary: 'Вторая рука',
    ready: 'Готово',
    cooling: 'Откат',
    unavailable: 'Недоступно',
    locked: 'Закрыто квестом',
    empty: 'Ожидание экипировки',
    charge: 'Заряд',
    recovery: 'Восстановление',
    storedDot: 'Сохранённый DOT',
    resource: 'Ресурс оружия',
    rhythm: {
      idle: 'Ожидание',
      early: 'Слишком рано',
      good: 'В ритме',
      late: 'Слишком поздно',
    },
    motionBonus: 'Урон за движение прицелом',
    unterhauReady: 'Unterhaw заряжен',
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
      holdThenDoubleTap: 'Задержка + нажатие',
    },
  },
} as const

const t = computed(() => copy[props.locale])
const panelTitle = computed(() => t.value.schemeTitles[props.controlScheme])
const panelSubtitle = computed(() => t.value.schemeSubtitles[props.controlScheme])

function cooldownPercent(gesture: GestureCooldown): number {
  if (gesture.remainingMs <= 0 || gesture.totalMs <= 0) return 100
  return Math.max(0, Math.min(100, 100 - (gesture.remainingMs / gesture.totalMs) * 100))
}

function remainingLabel(weapon: WeaponCooldown, gesture: GestureCooldown): string {
  if (gesture.locked) return t.value.locked
  if (!gesture.enabled) return t.value.unavailable
  if (gesture.ready) return t.value.ready
  if (gesture.remainingMs > 0) {
    return `${(gesture.remainingMs / 1000).toFixed(gesture.remainingMs < 1000 ? 1 : 0)}s`
  }
  const recovery = recoveryMs(weapon)
  if (recovery > 0) return `${t.value.recovery} · ${Math.ceil(recovery)} ms`
  return t.value.unavailable
}

function recoveryMs(weapon: WeaponCooldown): number {
  return Math.max(weapon.cue?.recoveryMs ?? 0, weapon.state?.recoveryMs ?? 0)
}

function resourcePercent(state: LastChancesWeaponStateSnapshot): number {
  if (state.maxResource <= 0) return 0
  return Math.max(0, Math.min(100, state.resource / state.maxResource * 100))
}

function chargeSegments(weapon: WeaponCooldown) {
  const bands = weapon.cue?.chargeBands ?? []
  const maximum = Math.max(
    1,
    weapon.chargeMaxMs ?? 0,
    ...bands.map(band => band.minMs),
  )
  return bands.map((band, index) => {
    const next = bands[index + 1]?.minMs ?? maximum
    return {
      ...band,
      left: Math.max(0, Math.min(100, band.minMs / maximum * 100)),
      width: Math.max(1, (Math.max(band.minMs, next) - band.minMs) / maximum * 100),
      passed: (weapon.cue?.heldMs ?? 0) >= next,
    }
  })
}

function activeChargeLabel(weapon: WeaponCooldown): string {
  return weapon.cue?.chargeBands.find(band => band.active)?.label ?? t.value.charge
}

function inputFeedbackLabel(weapon: WeaponCooldown): string {
  if (props.controlScheme === 'legacy') {
    return weapon.cue?.phase === 'recovery'
      ? `${t.value.recovery} · ${Math.ceil(recoveryMs(weapon))} ms`
      : t.value.input[weapon.input?.phase ?? 'idle']
  }
  if (weapon.cue?.phase === 'recovery') {
    return `${t.value.recovery} · ${Math.ceil(recoveryMs(weapon))} ms`
  }
  if (weapon.controlCue?.label) return weapon.controlCue.label
  if (weapon.controlRole?.nextGate) {
    return `${t.value.nextGate}: ${weapon.controlRole.nextGate}`
  }
  return weapon.controlRole?.techniqueOrTrigger ?? t.value.ready
}

function gesturePrompt(gesture: GestureCooldown): string {
  return gesture.physicalLabel || t.value.gestures[gesture.key]
}
</script>

<template>
  <section class="lc-cooldowns" :aria-label="panelTitle">
    <header class="lc-cooldown-heading">
      <div class="lc-cooldown-mark"><MousePointerClick :size="16" aria-hidden="true" /></div>
      <div>
        <h2>{{ panelTitle }}</h2>
        <p>{{ panelSubtitle }}</p>
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
          <span class="lc-hand-key">
            {{ controlScheme === 'legacy'
              ? (weapon.hand === 'primary' ? 'L' : 'R')
              : (weapon.hand === 'primary' ? 'R' : 'L') }}
          </span>
          <div>
            <small>{{ weapon.hand === 'primary' ? t.primary : t.secondary }}</small>
            <h3>{{ weapon.name }}</h3>
            <span v-if="controlScheme !== 'legacy' && weapon.controlRole" class="lc-control-role-copy">
              {{ weapon.controlRole.instantMove }} · {{ weapon.controlRole.techniqueOrTrigger }}
            </span>
          </div>
          <CircleDot :size="15" aria-hidden="true" />
        </header>

        <div
          v-if="weapon.state || recoveryMs(weapon) > 0"
          class="lc-weapon-state"
          :style="{ '--resource-color': weapon.state?.resourceColor || '#8eb3ab' }"
        >
          <div v-if="weapon.state?.resourceKind" class="lc-resource">
            <span>
              <Gauge :size="11" aria-hidden="true" />
              {{ weapon.state.resourceLabel || t.resource }}
            </span>
            <strong>{{ Math.round(weapon.state.resource) }} / {{ Math.round(weapon.state.maxResource) }}</strong>
            <i aria-hidden="true"><b :style="{ width: `${resourcePercent(weapon.state)}%` }" /></i>
          </div>
          <div v-if="weapon.state?.storedDot" class="lc-state-chip is-dot">
            <Zap :size="10" aria-hidden="true" />{{ t.storedDot }} · {{ weapon.state.storedDot }}
          </div>
          <div
            v-if="weapon.state && (weapon.state.resourceKind === 'rhythm' || weapon.state.rhythm !== 'idle')"
            class="lc-state-chip"
            :class="`is-rhythm-${weapon.state.rhythm}`"
          >
            {{ t.rhythm[weapon.state.rhythm] }}
          </div>
          <div v-if="recoveryMs(weapon) > 0" class="lc-state-chip is-recovery">
            <TimerReset :size="10" aria-hidden="true" />
            {{ t.recovery }} · {{ Math.ceil(recoveryMs(weapon)) }} ms
          </div>
          <div v-if="weapon.state?.motionDamageBonus" class="lc-state-chip is-motion">
            {{ t.motionBonus }} · +{{ Math.round(weapon.state.motionDamageBonus * 100) }}%
          </div>
          <div v-if="weapon.state?.unterhauPrimed" class="lc-state-chip is-unterhau">
            <Zap :size="10" aria-hidden="true" />{{ t.unterhauReady }}
          </div>
        </div>

        <div
          class="lc-input-feedback"
          :class="[
            `is-${weapon.input?.phase ?? 'idle'}`,
            `is-cue-${weapon.cue?.phase ?? 'idle'}`,
            { 'is-pressed': weapon.input?.pressed },
          ]"
          :style="{ '--cue-color': weapon.cue?.color || '#c7a45d' }"
          role="status"
        >
          <span>{{ inputFeedbackLabel(weapon) }}</span>
          <small v-if="weapon.cue?.phase !== 'recovery' && weapon.cue?.heldMs">{{ Math.ceil(weapon.cue.heldMs) }} ms</small>
          <small v-else-if="weapon.cue?.phase !== 'recovery' && weapon.input?.remainingMs">{{ Math.ceil(weapon.input.remainingMs) }} ms</small>
          <i aria-hidden="true">
            <b :style="{ width: `${Math.max(weapon.input?.progress ?? 0, weapon.cue?.chargeProgress ?? 0) * 100}%` }" />
          </i>
        </div>

        <div
          v-if="weapon.cue?.chargeBands.length"
          class="lc-charge"
          :style="{ '--cue-color': weapon.cue.color }"
        >
          <div class="lc-charge-copy">
            <span>{{ activeChargeLabel(weapon) }}</span>
            <strong>{{ Math.ceil(weapon.cue.heldMs) }} ms</strong>
          </div>
          <div class="lc-charge-track" aria-hidden="true">
            <span
              v-for="segment in chargeSegments(weapon)"
              :key="segment.id"
              class="lc-charge-band"
              :class="{ 'is-active': segment.active, 'is-passed': segment.passed }"
              :title="`${segment.label}: ${segment.minMs} ms`"
              :style="{
                left: `${segment.left}%`,
                width: `${segment.width}%`,
                '--band-color': segment.color,
              }"
            />
            <i :style="{ width: `${weapon.cue.chargeProgress * 100}%` }" />
          </div>
          <div class="lc-charge-labels">
            <small
              v-for="band in weapon.cue.chargeBands"
              :key="band.id"
              :class="{ 'is-active': band.active }"
              :style="{ '--band-color': band.color }"
            >
              {{ band.label }} · {{ band.minMs }} ms
            </small>
          </div>
        </div>

        <ol class="lc-gesture-list">
          <li
            v-for="gesture in weapon.gestures"
            :key="gesture.key"
            :class="{
              'is-ready': gesture.enabled && gesture.ready,
              'is-active': gesture.enabled && gesture.active,
              'is-disabled': !gesture.enabled,
              'is-locked': gesture.locked,
              'is-context-dimmed': gesture.contextDimmed,
              'is-primed': gesture.primed,
              'is-blocked': gesture.enabled && !gesture.ready && gesture.remainingMs <= 0,
            }"
            :style="{ '--gesture-color': gesture.color }"
          >
            <div class="lc-gesture-copy">
              <span class="lc-gesture-index">{{ weapon.gestures.indexOf(gesture) + 1 }}</span>
              <span class="lc-gesture-name">
                <small>{{ gesturePrompt(gesture) }}</small>
                <strong>{{ gesture.name }}</strong>
                <em v-if="gesture.description">{{ gesture.description }}</em>
              </span>
              <span class="lc-gesture-time">
                <Lock v-if="gesture.locked" :size="11" aria-hidden="true" />
                <TimerReset v-else-if="gesture.remainingMs > 0 || (!gesture.ready && recoveryMs(weapon) > 0)" :size="11" aria-hidden="true" />
                {{ remainingLabel(weapon, gesture) }}
              </span>
            </div>
            <span class="lc-cooldown-track" aria-hidden="true">
              <i :style="{ width: `${gesture.enabled ? cooldownPercent(gesture) : 0}%` }" />
            </span>
            <span class="sr-only">
              {{ gesture.locked
                ? t.locked
                : !gesture.enabled
                  ? t.unavailable
                  : gesture.ready ? t.ready : remainingLabel(weapon, gesture) }}
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
.lc-control-role-copy { display: block; max-width: 13rem; margin-top: 0.12rem; overflow: hidden; color: #857993; font-size: 0.43rem; line-height: 1.2; text-overflow: ellipsis; white-space: nowrap; }

.lc-weapon-state { display: flex; flex-wrap: wrap; align-items: center; gap: 0.32rem; padding: 0.38rem 0.55rem; border-bottom: 1px solid rgba(255, 255, 255, 0.04); background: rgba(255, 255, 255, 0.012); }
.lc-resource { position: relative; min-width: 9rem; flex: 1 1 10rem; display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: 0.3rem; padding-bottom: 0.28rem; }
.lc-resource span { min-width: 0; display: inline-flex; align-items: center; gap: 0.25rem; overflow: hidden; color: #868b87; font-size: 0.49rem; font-weight: 700; text-overflow: ellipsis; white-space: nowrap; }
.lc-resource strong { color: var(--resource-color); font: 700 0.49rem/1 var(--font-mono, monospace); }
.lc-resource > i { position: absolute; inset: auto 0 0; height: 2px; overflow: hidden; border-radius: 999px; background: rgba(255, 255, 255, 0.04); }
.lc-resource > i b { display: block; height: 100%; border-radius: inherit; background: var(--resource-color); box-shadow: 0 0 0.35rem var(--resource-color); transition: width 0.1s linear; }
.lc-state-chip { display: inline-flex; align-items: center; gap: 0.2rem; padding: 0.2rem 0.34rem; border: 1px solid rgba(255, 255, 255, 0.07); border-radius: 999px; color: #898e8a; background: rgba(255, 255, 255, 0.025); font-size: 0.45rem; font-weight: 750; }
.lc-state-chip.is-dot { color: #a8c98b; border-color: rgba(135, 183, 111, 0.2); }
.lc-state-chip.is-recovery,
.lc-state-chip.is-rhythm-early,
.lc-state-chip.is-rhythm-late { color: #cf8585; border-color: rgba(193, 87, 94, 0.2); }
.lc-state-chip.is-rhythm-good { color: #9fd7b1; border-color: rgba(98, 190, 127, 0.25); }
.lc-state-chip.is-motion { color: #edcc7d; border-color: rgba(237, 204, 125, 0.24); }
.lc-state-chip.is-unterhau { color: #fff0a8; border-color: rgba(255, 224, 126, 0.4); box-shadow: 0 0 0.55rem rgba(255, 213, 96, 0.18); }

.lc-input-feedback { position: relative; display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 0.35rem; padding: 0.35rem 0.6rem 0.42rem; overflow: hidden; border-bottom: 1px solid rgba(255, 255, 255, 0.04); color: #666b69; background: rgba(255, 255, 255, 0.015); font-size: 0.5rem; }
.lc-input-feedback span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.lc-input-feedback small { color: #aa9469; font: 700 0.47rem/1 var(--font-mono, monospace); }
.lc-input-feedback > i { position: absolute; inset: auto 0 0; height: 2px; background: rgba(255, 255, 255, 0.035); }
.lc-input-feedback > i b { display: block; height: 100%; background: var(--cue-color); box-shadow: 0 0 0.35rem var(--cue-color); transition: width 0.06s linear; }
.lc-input-feedback:not(.is-idle) { color: #ddd6c5; background: rgba(190, 153, 77, 0.075); }
.is-secondary .lc-input-feedback:not(.is-idle) { background: rgba(158, 57, 65, 0.085); }
.lc-input-feedback.is-pressed { box-shadow: inset 0 0 0.8rem rgba(215, 180, 104, 0.06); }
.lc-input-feedback.is-cue-armed { color: var(--cue-color); box-shadow: inset 0 0 0.9rem color-mix(in srgb, var(--cue-color) 18%, transparent); }

.lc-charge { display: grid; gap: 0.3rem; padding: 0.42rem 0.55rem 0.5rem; border-bottom: 1px solid rgba(255, 255, 255, 0.04); background: rgba(255, 255, 255, 0.012); }
.lc-charge-copy { display: flex; justify-content: space-between; gap: 0.5rem; }
.lc-charge-copy span { overflow: hidden; color: var(--cue-color); font-size: 0.5rem; font-weight: 750; text-overflow: ellipsis; white-space: nowrap; }
.lc-charge-copy strong { color: #aaa49a; font: 700 0.47rem/1 var(--font-mono, monospace); }
.lc-charge-track { position: relative; height: 0.38rem; overflow: hidden; border-radius: 999px; background: rgba(255, 255, 255, 0.045); }
.lc-charge-track > i { position: absolute; z-index: 2; inset: 0 auto 0 0; border-right: 1px solid #fff; background: rgba(255, 255, 255, 0.16); box-shadow: 0 0 0.45rem var(--cue-color); transition: width 0.06s linear; }
.lc-charge-band { position: absolute; z-index: 1; inset-block: 0; border-left: 1px solid rgba(0, 0, 0, 0.55); background: color-mix(in srgb, var(--band-color) 34%, transparent); opacity: 0.5; }
.lc-charge-band.is-passed { opacity: 0.75; }
.lc-charge-band.is-active { background: var(--band-color); opacity: 0.9; box-shadow: 0 0 0.5rem var(--band-color); }
.lc-charge-labels { display: flex; flex-wrap: wrap; gap: 0.2rem 0.35rem; }
.lc-charge-labels small { color: #666b68; font-size: 0.42rem; }
.lc-charge-labels small::before { content: ''; display: inline-block; width: 0.32rem; height: 0.32rem; margin-right: 0.2rem; border-radius: 50%; background: var(--band-color); opacity: 0.55; }
.lc-charge-labels small.is-active { color: var(--band-color); }

.lc-gesture-list { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
.lc-gesture-list li { position: relative; display: grid; padding: 0.4rem 0.55rem 0.34rem; border-bottom: 1px solid rgba(255, 255, 255, 0.035); opacity: 0.67; }
.lc-gesture-list li:last-child { border-bottom: 0; }
.lc-gesture-list li.is-ready { opacity: 1; }
.lc-gesture-list li.is-blocked { opacity: 0.52; }
.lc-gesture-list li.is-active { background: color-mix(in srgb, var(--gesture-color) 11%, transparent); }
.lc-gesture-list li.is-disabled { opacity: 0.32; filter: grayscale(0.85); }
.lc-gesture-list li.is-locked { opacity: 0.38; filter: grayscale(0.9); }
.lc-gesture-list li.is-context-dimmed { opacity: 0.24; filter: grayscale(1) brightness(0.62); }
.lc-gesture-list li.is-primed { opacity: 1; filter: none; background: rgba(255, 220, 112, 0.11); box-shadow: inset 0 0 1rem rgba(255, 212, 85, 0.14); }
.lc-gesture-list li.is-locked .lc-gesture-time { color: #8a8478; }

.lc-gesture-copy { min-width: 0; display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 0.45rem; }
.lc-gesture-index { width: 1.18rem; height: 1.18rem; display: grid; place-items: center; border: 1px solid rgba(255, 255, 255, 0.09); border-radius: 50%; color: #626764; font: 700 0.47rem/1 var(--font-mono, monospace); }
.is-ready .lc-gesture-index { color: var(--gesture-color); border-color: color-mix(in srgb, var(--gesture-color) 45%, transparent); box-shadow: 0 0 0.35rem color-mix(in srgb, var(--gesture-color) 20%, transparent); }
.lc-gesture-name { min-width: 0; display: grid; }
.lc-gesture-name small { color: #5f6462; font-size: 0.46rem; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
.lc-gesture-name strong { overflow: hidden; color: #b6b7b1; font-size: 0.58rem; font-weight: 650; line-height: 1.2; text-overflow: ellipsis; white-space: nowrap; }
.lc-gesture-name em { margin-top: 0.12rem; overflow: hidden; color: #777d79; font-size: 0.45rem; font-style: normal; line-height: 1.25; text-overflow: ellipsis; white-space: nowrap; }
.is-ready .lc-gesture-name strong { color: #dfdcd3; }
.lc-gesture-time { display: inline-flex; align-items: center; gap: 0.2rem; color: #9b7778; font: 700 0.5rem/1 var(--font-mono, monospace); }
.is-ready .lc-gesture-time { color: var(--gesture-color); }

.lc-cooldown-track { height: 2px; margin: 0.3rem 0 0 1.65rem; overflow: hidden; border-radius: 999px; background: rgba(255, 255, 255, 0.045); }
.lc-cooldown-track i { display: block; height: 100%; border-radius: inherit; background: var(--gesture-color); box-shadow: 0 0 0.35rem color-mix(in srgb, var(--gesture-color) 45%, transparent); transition: width 0.12s linear; }
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
  .lc-resource > i b,
  .lc-charge-track > i,
  .lc-input-feedback > i b { transition: none; }
}
</style>
