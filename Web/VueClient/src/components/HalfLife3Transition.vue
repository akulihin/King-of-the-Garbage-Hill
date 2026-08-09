<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'
import { currentLocale } from 'src/i18n'
import { localizedText } from 'src/platform/localization'
import type { GordonHalfLifeState } from 'src/services/signalr'

const props = withDefaults(defineProps<{
  isGordon: boolean
  halfLife: GordonHalfLifeState | null
  transitionDeadlineUtc?: string
  isSubmitting?: boolean
}>(), {
  transitionDeadlineUtc: undefined,
  isSubmitting: false,
})

const emit = defineEmits<{
  resolve: [choice: 'freeze' | 'postpone' | 'release']
}>()

const { overlayRef, dialogRef, focusFirstControl, trapTabKey } = useFocusTrapDialog()
const secondsLeft = ref<number | null>(null)
let countdownTimer: ReturnType<typeof setInterval> | null = null

const isDecisionOwner = computed(() => props.isGordon && props.halfLife?.pendingDecision === true)
const deadlineUtc = computed(() => props.halfLife?.deadlineUtc || props.transitionDeadlineUtc)
const isExpired = computed(() => secondsLeft.value !== null && secondsLeft.value <= 0)
const actionsDisabled = computed(() => props.isSubmitting || isExpired.value)

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

function syncCountdown(): void {
  if (!deadlineUtc.value) {
    secondsLeft.value = null
    return
  }

  const deadline = Date.parse(deadlineUtc.value)
  if (Number.isNaN(deadline)) {
    secondsLeft.value = null
    return
  }

  secondsLeft.value = Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
}

function onDialogKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    return
  }
  trapTabKey(event)
}

function resolve(choice: 'freeze' | 'postpone' | 'release'): void {
  if (actionsDisabled.value) return
  emit('resolve', choice)
}

watch(deadlineUtc, syncCountdown, { immediate: true })
watch(isDecisionOwner, async (isOwner) => {
  if (!isOwner) return
  await nextTick()
  focusFirstControl()
})

onMounted(() => {
  syncCountdown()
  countdownTimer = setInterval(syncCountdown, 250)
})

onUnmounted(() => {
  if (countdownTimer) clearInterval(countdownTimer)
})
</script>

<template>
  <Teleport to="body">
    <div ref="overlayRef" class="hl3-overlay">
      <section
        ref="dialogRef"
        class="hl3-card"
        :class="{ 'hl3-card-waiting': !isDecisionOwner }"
        role="dialog"
        aria-modal="true"
        aria-labelledby="hl3-transition-title"
        :aria-describedby="isDecisionOwner ? 'hl3-transition-description' : 'hl3-waiting-description'"
        :aria-busy="isSubmitting"
        tabindex="-1"
        @keydown="onDialogKeydown"
      >
        <div class="hl3-scanlines" aria-hidden="true" />
        <div class="hl3-lambda" aria-hidden="true">λ</div>

        <template v-if="isDecisionOwner && halfLife">
          <div class="hl3-kicker">
            {{ t('GABE NEWELL — RELEASE REVIEW', 'ГЕЙБ НЬЮЭЛЛ — РЕШЕНИЕ О РЕЛИЗЕ') }}
          </div>
          <h2 id="hl3-transition-title">HALFLIFE 3</h2>
          <p id="hl3-transition-description" class="hl3-failure">
            {{ halfLife.decisionMessageText
              ? localizedText(halfLife.decisionMessageText)
              : halfLife.decisionMessage || 'Halflife 3: Недостаточно профита, нельзя выпускать игру.' }}
          </p>

          <div class="hl3-deadline" :class="{ expired: isExpired }" role="timer" aria-live="polite">
            <span class="hl3-deadline-dot" aria-hidden="true" />
            <template v-if="secondsLeft !== null">
              {{ isExpired
                ? t('Decision time expired', 'Время на решение истекло')
                : t(`${secondsLeft} seconds to decide`, `${secondsLeft} сек. на решение`) }}
            </template>
            <template v-else>{{ t('20-second decision window', '20 секунд на решение') }}</template>
          </div>

          <div class="hl3-profit" :aria-label="t('Release profit calculation', 'Расчёт профита релиза')">
            <div class="hl3-profit-cell">
              <span>{{ t('Round points', 'Очки за раунд') }}</span>
              <strong>{{ halfLife.rawPoints }}</strong>
            </div>
            <div class="hl3-profit-cell">
              <span>{{ t('Power calculation', 'Расчёт степени') }}</span>
              <strong>{{ halfLife.rawPoints }}^{{ halfLife.exponent }}</strong>
            </div>
            <div class="hl3-profit-cell hl3-profit-total">
              <span>{{ t('Final total', 'Финальная сумма') }}</span>
              <strong>{{ halfLife.finalPoints }}</strong>
            </div>
          </div>
          <p v-if="halfLife.superMultiplierDisabled" class="hl3-tolya-disabled">
            {{ t(
              'Tolya disabled pre-orders: ordinary points are awarded.',
              'Подсчет Толи отключил предзаказы: начисляются обычные очки.',
            ) }}
          </p>

          <div class="hl3-attempt">
            {{ t('Postponements', 'Переносов') }}: {{ halfLife.postponements }} / 3
          </div>

          <div class="hl3-actions">
            <button
              class="hl3-choice hl3-freeze"
              type="button"
              :disabled="actionsDisabled"
              @click="resolve(halfLife.decisionKind === 'release' ? 'release' : 'freeze')"
            >
              <span aria-hidden="true">{{ halfLife.decisionKind === 'release' ? '🚀' : '❄' }}</span>
              <strong>{{ halfLife.freezeLabel || t('Freeze the game', 'Заморозить игру') }}</strong>
            </button>
            <button
              class="hl3-choice hl3-postpone"
              type="button"
              :disabled="actionsDisabled"
              @click="resolve('postpone')"
            >
              <span aria-hidden="true">λ</span>
              <strong>{{ halfLife.postponeLabel || t('Postpone release', 'Перенести релиз') }}</strong>
            </button>
          </div>

          <div v-if="isSubmitting" class="hl3-submitting" role="status">
            <span class="hl3-spinner" aria-hidden="true" />
            {{ t('Sending decision…', 'Отправляем решение…') }}
          </div>
        </template>

        <template v-else>
          <div class="hl3-kicker">BLACK MESA // ROUND TRANSITION</div>
          <h2 id="hl3-transition-title">HALFLIFE 3</h2>
          <p id="hl3-waiting-description" class="hl3-waiting-copy">
            {{ t(
              'Gabe Newell is reviewing the release. Waiting for Gordon Freeman…',
              'Гейб Ньюэлл оценивает профит. Ожидаем решение Гордона Фримена…',
            ) }}
          </p>
          <div class="hl3-waiting-loader" aria-hidden="true">
            <span /><span /><span />
          </div>
          <div v-if="secondsLeft !== null" class="hl3-waiting-time" role="timer" aria-live="polite">
            {{ secondsLeft }}s
          </div>
        </template>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.hl3-overlay {
  position: fixed;
  z-index: 3400;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 18px;
  background:
    radial-gradient(circle at 50% 38%, rgba(113, 65, 19, 0.28), transparent 42%),
    rgba(4, 5, 5, 0.94);
  backdrop-filter: blur(10px);
}

.hl3-card {
  position: relative;
  isolation: isolate;
  width: min(620px, 100%);
  max-height: calc(100svh - 28px);
  overflow: hidden auto;
  padding: 30px;
  color: #e9e6dc;
  border: 1px solid rgba(241, 139, 37, 0.62);
  border-radius: 4px;
  outline: none;
  background:
    linear-gradient(145deg, rgba(37, 30, 22, 0.98), rgba(11, 13, 13, 0.99) 64%),
    #0b0d0d;
  box-shadow:
    0 32px 100px rgba(0, 0, 0, 0.78),
    0 0 48px rgba(238, 126, 28, 0.12),
    inset 0 1px 0 rgba(255, 255, 255, 0.06);
  animation: hl3-card-in 0.32s ease-out both;
}

.hl3-card::before {
  content: '';
  position: absolute;
  z-index: -1;
  top: 0;
  right: 0;
  left: 0;
  height: 6px;
  background: repeating-linear-gradient(135deg, #e78124 0 10px, #161817 10px 20px);
}

.hl3-scanlines {
  position: absolute;
  z-index: -1;
  inset: 0;
  opacity: 0.22;
  pointer-events: none;
  background: repeating-linear-gradient(180deg, transparent 0 3px, rgba(255, 255, 255, 0.018) 3px 4px);
}

.hl3-lambda {
  position: absolute;
  z-index: -1;
  top: -38px;
  right: 18px;
  color: rgba(238, 126, 28, 0.07);
  font: 900 210px/1 Arial, sans-serif;
  transform: rotate(-7deg);
  pointer-events: none;
}

.hl3-kicker {
  color: #e78124;
  font: 850 10px/1.3 var(--font-mono);
  letter-spacing: 0.18em;
}

.hl3-card h2 {
  margin: 7px 0 5px;
  color: #f2eee4;
  font: 950 clamp(30px, 7vw, 48px)/1 var(--font-mono);
  letter-spacing: 0.07em;
  text-shadow: 0 0 22px rgba(231, 129, 36, 0.24);
}

.hl3-failure,
.hl3-waiting-copy {
  margin: 12px 0 0;
  color: rgba(235, 232, 222, 0.76);
  font-size: 13px;
  line-height: 1.55;
  white-space: pre-wrap;
}

.hl3-deadline {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  margin-top: 14px;
  padding: 6px 9px;
  color: #ffc06d;
  border: 1px solid rgba(231, 129, 36, 0.24);
  border-radius: 3px;
  background: rgba(231, 129, 36, 0.08);
  font: 800 10px/1 var(--font-mono);
}

.hl3-deadline.expired { color: #f18c8c; border-color: rgba(241, 140, 140, 0.3); }
.hl3-deadline-dot { width: 7px; height: 7px; border-radius: 50%; background: currentColor; box-shadow: 0 0 8px currentColor; animation: hl3-deadline-pulse 1s ease-in-out infinite; }

.hl3-profit {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 7px;
  margin-top: 18px;
}

.hl3-profit-cell {
  min-width: 0;
  padding: 10px;
  border: 1px solid rgba(255, 255, 255, 0.07);
  border-radius: 3px;
  background: rgba(255, 255, 255, 0.025);
}

.hl3-profit-cell span {
  display: block;
  min-height: 25px;
  color: rgba(235, 232, 222, 0.46);
  font-size: 9px;
  font-weight: 750;
  line-height: 1.35;
  text-transform: uppercase;
}

.hl3-profit-cell strong { display: block; margin-top: 4px; color: #e9e6dc; font: 900 23px/1 var(--font-mono); }
.hl3-profit-total { border-color: rgba(231, 129, 36, 0.35); background: rgba(231, 129, 36, 0.08); }
.hl3-profit-total strong { color: #ffae53; text-shadow: 0 0 12px rgba(231, 129, 36, 0.35); }
.hl3-tolya-disabled {
  margin: 10px 0 0;
  padding: 8px 10px;
  color: #f1c477;
  border: 1px solid rgba(241, 196, 119, 0.24);
  border-radius: 3px;
  background: rgba(241, 196, 119, 0.07);
  font: 750 9px/1.45 var(--font-mono);
}

.hl3-attempt {
  margin-top: 10px;
  color: rgba(235, 232, 222, 0.48);
  font: 750 10px/1.3 var(--font-mono);
  text-align: right;
}

.hl3-actions {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 9px;
  margin-top: 18px;
}

.hl3-choice {
  display: grid;
  grid-template-columns: 29px minmax(0, 1fr);
  align-items: center;
  gap: 8px;
  min-height: 62px;
  padding: 10px 12px;
  color: #e9e6dc;
  border: 1px solid;
  border-radius: 4px;
  cursor: pointer;
  text-align: left;
  transition: transform 0.16s ease, border-color 0.16s ease, background 0.16s ease, box-shadow 0.16s ease;
}

.hl3-choice > span { display: grid; width: 29px; height: 29px; place-items: center; border-radius: 3px; font: 900 18px/1 var(--font-mono); }
.hl3-choice strong { overflow-wrap: anywhere; font-size: 11px; line-height: 1.35; }
.hl3-choice:focus-visible { outline: 2px solid #f2eee4; outline-offset: 3px; }
.hl3-choice:hover:not(:disabled) { transform: translateY(-2px); }
.hl3-choice:active:not(:disabled) { transform: translateY(0); }
.hl3-choice:disabled { opacity: 0.42; cursor: wait; }

.hl3-freeze { border-color: rgba(100, 186, 240, 0.36); background: rgba(54, 133, 183, 0.08); }
.hl3-freeze > span { color: #9edcff; background: rgba(75, 165, 218, 0.12); }
.hl3-freeze:hover:not(:disabled) { border-color: rgba(123, 205, 255, 0.68); box-shadow: 0 7px 20px rgba(43, 143, 204, 0.13); }
.hl3-postpone { border-color: rgba(231, 129, 36, 0.42); background: rgba(231, 129, 36, 0.09); }
.hl3-postpone > span { color: #ffab50; background: rgba(231, 129, 36, 0.13); }
.hl3-postpone:hover:not(:disabled) { border-color: rgba(255, 166, 78, 0.74); box-shadow: 0 7px 20px rgba(231, 129, 36, 0.14); }

.hl3-submitting {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 22px;
  margin-top: 10px;
  color: rgba(235, 232, 222, 0.58);
  font-size: 10px;
}

.hl3-spinner { width: 13px; height: 13px; border: 2px solid rgba(231, 129, 36, 0.2); border-top-color: #e78124; border-radius: 50%; animation: hl3-spin 0.7s linear infinite; }

.hl3-card-waiting { width: min(460px, 100%); text-align: center; }
.hl3-card-waiting .hl3-kicker { text-align: center; }
.hl3-waiting-copy { max-width: 360px; margin-inline: auto; }
.hl3-waiting-loader { display: flex; justify-content: center; gap: 7px; margin-top: 22px; }
.hl3-waiting-loader span { width: 8px; height: 8px; border-radius: 50%; background: #e78124; animation: hl3-wait 1.05s ease-in-out infinite; }
.hl3-waiting-loader span:nth-child(2) { animation-delay: 0.14s; }
.hl3-waiting-loader span:nth-child(3) { animation-delay: 0.28s; }
.hl3-waiting-time { margin-top: 11px; color: rgba(235, 232, 222, 0.42); font: 800 10px/1 var(--font-mono); }

@keyframes hl3-card-in { from { opacity: 0; transform: translateY(14px) scale(0.98); } to { opacity: 1; transform: translateY(0) scale(1); } }
@keyframes hl3-deadline-pulse { 50% { opacity: 0.35; } }
@keyframes hl3-spin { to { transform: rotate(360deg); } }
@keyframes hl3-wait { 0%, 100% { opacity: 0.25; transform: translateY(0); } 50% { opacity: 1; transform: translateY(-5px); } }

@media (max-width: 600px) {
  .hl3-overlay { align-items: end; padding: 7px; }
  .hl3-card { width: 100%; max-height: calc(100svh - 7px); padding: 25px 16px 18px; border-radius: 5px; }
  .hl3-profit { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .hl3-actions { grid-template-columns: 1fr; }
  .hl3-choice { min-height: 58px; }
}

@media (prefers-reduced-motion: reduce) {
  .hl3-card,
  .hl3-deadline-dot,
  .hl3-spinner,
  .hl3-waiting-loader span { animation: none; }
  .hl3-choice { transition: none; }
}
</style>
