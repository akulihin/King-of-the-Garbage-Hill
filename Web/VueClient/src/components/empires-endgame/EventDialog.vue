<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import {
  AlertTriangle,
  CheckCircle2,
  ChevronRight,
  Coins,
  Crown,
  HelpCircle,
  Scale,
  ScrollText,
  Skull,
  Sparkles,
} from 'lucide-vue-next'

type OutcomeTone = 'positive' | 'negative' | 'neutral' | 'mixed' | 'warning'

interface EventChoiceView {
  id: string
  name: string
  description: string
  costs: string[]
  effects: string[]
  disabled?: boolean
  disabledReason?: string
}

interface EventOutcomeView {
  title: string
  description?: string
  effects: string[]
  tone?: OutcomeTone
}

const props = withDefaults(defineProps<{
  open?: boolean
  title: string
  description: string
  choices: EventChoiceView[]
  category?: string
  imageUrl?: string
  selectedId?: string | null
  outcome?: EventOutcomeView | null
  disabled?: boolean
}>(), {
  open: true,
  category: 'Имперское событие',
  imageUrl: '',
  selectedId: null,
  outcome: null,
  disabled: false,
})

const emit = defineEmits<{
  choose: [id: string]
}>()

const dialog = ref<HTMLElement | null>(null)
let previouslyFocused: HTMLElement | null = null

const outcomeTone = computed<OutcomeTone>(() => props.outcome?.tone ?? 'neutral')
const outcomeIcon = computed(() => {
  if (outcomeTone.value === 'positive') return CheckCircle2
  if (outcomeTone.value === 'negative') return Skull
  if (outcomeTone.value === 'mixed') return Scale
  if (outcomeTone.value === 'warning') return AlertTriangle
  return ScrollText
})

watch(() => props.open, async open => {
  if (open) {
    previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
    await nextTick()
    dialog.value?.focus()
    return
  }
  previouslyFocused?.focus()
  previouslyFocused = null
}, { immediate: true })

onBeforeUnmount(() => previouslyFocused?.focus())

function choose(choice: EventChoiceView) {
  if (props.disabled || props.outcome || choice.disabled) return
  emit('choose', choice.id)
}

function hideBrokenImage(event: Event) {
  ;(event.currentTarget as HTMLImageElement).hidden = true
}

function trapFocus(event: KeyboardEvent) {
  if (!dialog.value) return
  const focusable = Array.from(dialog.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )).filter(element => !element.hidden)
  if (!focusable.length) {
    event.preventDefault()
    dialog.value.focus()
    return
  }
  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}
</script>

<template>
  <Teleport to="body">
    <Transition name="event-dialog">
      <div v-if="open" class="event-backdrop">
        <section
          ref="dialog"
          class="event-dialog"
          :class="outcome ? `has-outcome tone-${outcomeTone}` : ''"
          role="dialog"
          aria-modal="true"
          aria-labelledby="event-dialog-title"
          aria-describedby="event-dialog-description"
          tabindex="-1"
          @keydown.tab="trapFocus"
        >
          <div class="event-hero" :class="{ 'has-image': imageUrl }">
            <img v-if="imageUrl" :src="imageUrl" alt="" @error="hideBrokenImage" />
            <div class="hero-shade" aria-hidden="true" />
            <span class="event-seal"><Crown :size="24" aria-hidden="true" /></span>
            <div class="event-title-block">
              <span>{{ category }}</span>
              <h2 id="event-dialog-title">{{ title }}</h2>
              <p id="event-dialog-description">{{ description }}</p>
            </div>
          </div>

          <div v-if="outcome" class="outcome-panel" role="status" aria-live="polite">
            <span class="outcome-icon"><component :is="outcomeIcon" :size="24" /></span>
            <div class="outcome-copy">
              <span>Исход решения</span>
              <h3>{{ outcome.title }}</h3>
              <p v-if="outcome.description">{{ outcome.description }}</p>
              <ul v-if="outcome.effects.length">
                <li v-for="effect in outcome.effects" :key="effect"><Sparkles :size="12" /> {{ effect }}</li>
              </ul>
            </div>
          </div>

          <div class="choice-section">
            <div class="choice-heading">
              <div>
                <span>{{ outcome ? 'Принятое решение' : 'Решение императора' }}</span>
                <strong>{{ outcome ? 'Последствия уже вступили в силу.' : 'Выберите один ответ. Решение нельзя отменить.' }}</strong>
              </div>
              <Scale :size="19" aria-hidden="true" />
            </div>

            <div class="event-choices" role="group" aria-label="Варианты решения события">
              <button
                v-for="(choice, index) in choices"
                :key="choice.id"
                type="button"
                class="event-choice"
                :class="{ selected: choice.id === selectedId, muted: Boolean(outcome) && choice.id !== selectedId }"
                :disabled="disabled || Boolean(outcome) || choice.disabled"
                :aria-pressed="choice.id === selectedId"
                @click="choose(choice)"
              >
                <span class="choice-index">{{ String.fromCharCode(65 + index) }}</span>
                <span class="choice-content">
                  <strong>{{ choice.name }}</strong>
                  <span class="choice-description">{{ choice.description }}</span>

                  <span v-if="choice.costs.length" class="string-group costs">
                    <b><Coins :size="12" /> Цена</b>
                    <span v-for="cost in choice.costs" :key="cost">{{ cost }}</span>
                  </span>
                  <span v-else class="string-group free-cost">
                    <b><Coins :size="12" /> Цена</b>
                    <span>Без прямых затрат</span>
                  </span>

                  <span v-if="choice.effects.length" class="string-group effects">
                    <b><Sparkles :size="12" /> Эффекты</b>
                    <span v-for="effect in choice.effects" :key="effect">{{ effect }}</span>
                  </span>
                </span>

                <span class="choice-action">
                  <template v-if="choice.disabled">
                    <HelpCircle :size="14" /> {{ choice.disabledReason || 'Недоступно' }}
                  </template>
                  <template v-else-if="choice.id === selectedId">
                    <CheckCircle2 :size="14" /> Выбрано
                  </template>
                  <template v-else>
                    Решить <ChevronRight :size="15" />
                  </template>
                </span>
              </button>
            </div>
          </div>

          <footer class="event-footer">
            <ScrollText :size="15" />
            <span>{{ outcome ? 'Событие завершено. Империя запомнит этот выбор.' : 'Событие приостанавливает течение имперских дней до вашего решения.' }}</span>
          </footer>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.event-backdrop {
  position: fixed;
  z-index: 1190;
  display: grid;
  inset: 0;
  place-items: center;
  overflow-y: auto;
  padding: 24px;
  background: rgba(4, 6, 4, 0.8);
  backdrop-filter: blur(9px);
}
.event-dialog {
  --tone: #c9aa67;
  width: min(900px, 100%);
  max-height: min(900px, calc(100vh - 48px));
  overflow: auto;
  border: 1px solid rgba(225, 199, 145, 0.23);
  border-radius: 18px;
  color: #eee5d1;
  background: linear-gradient(155deg, #1d2019, #10130f 72%);
  box-shadow: 0 34px 110px rgba(0, 0, 0, 0.66);
}
.event-dialog:focus { outline: none; }
.event-dialog:focus-visible { outline: 2px solid #e8ce8c; outline-offset: 3px; }
.event-dialog.tone-positive { --tone: #79b17a; }
.event-dialog.tone-negative { --tone: #c26e61; }
.event-dialog.tone-warning { --tone: #d1a250; }
.event-dialog.tone-mixed { --tone: #a985bd; }
.event-dialog.tone-neutral { --tone: #8c9b8a; }

.event-hero { position: relative; display: grid; min-height: 210px; grid-template-columns: auto minmax(0, 1fr); align-items: end; gap: 15px; overflow: hidden; padding: 27px; border-bottom: 1px solid rgba(225, 199, 145, 0.16); isolation: isolate; background: radial-gradient(circle at 70% 15%, rgba(202, 166, 91, 0.14), transparent 38%), linear-gradient(120deg, #2c251a, #192019 66%, #141812); }
.event-hero > img { position: absolute; z-index: -2; width: 100%; height: 100%; object-fit: cover; opacity: 0.58; }
.hero-shade { position: absolute; z-index: -1; inset: 0; background: linear-gradient(90deg, rgba(10, 12, 9, 0.93), rgba(10, 12, 9, 0.52) 60%, rgba(10, 12, 9, 0.76)), linear-gradient(transparent 30%, rgba(10, 12, 9, 0.88)); }
.event-seal { display: grid; width: 54px; height: 54px; place-items: center; border: 1px solid rgba(231, 204, 145, 0.35); border-radius: 50%; color: #efd58e; background: rgba(18, 20, 15, 0.7); box-shadow: inset 0 0 24px rgba(202, 166, 91, 0.12); }
.event-title-block > span { color: #d0ad63; font: 800 0.61rem/1 var(--font-mono, monospace); letter-spacing: 0.14em; text-transform: uppercase; }
.event-title-block h2 { margin: 6px 0 7px; color: #fbefd9; font: 700 clamp(1.6rem, 3.6vw, 2.55rem)/0.98 Georgia, serif; text-wrap: balance; }
.event-title-block p { max-width: 710px; margin: 0; color: rgba(240, 230, 210, 0.66); font-size: 0.75rem; line-height: 1.55; }

.outcome-panel { display: grid; grid-template-columns: auto minmax(0, 1fr); gap: 12px; margin: 18px 18px 0; padding: 14px; border: 1px solid color-mix(in srgb, var(--tone) 38%, transparent); border-radius: 11px; background: color-mix(in srgb, var(--tone) 8%, transparent); }
.outcome-icon { display: grid; width: 43px; height: 43px; place-items: center; border-radius: 9px; color: color-mix(in srgb, var(--tone) 70%, white); background: color-mix(in srgb, var(--tone) 13%, transparent); }
.outcome-copy > span { color: var(--tone); font: 800 0.56rem/1 var(--font-mono, monospace); letter-spacing: 0.1em; text-transform: uppercase; }
.outcome-copy h3 { margin: 5px 0 3px; color: #f1e6d1; font: 700 1.15rem/1 Georgia, serif; }
.outcome-copy p { margin: 0; color: rgba(240, 230, 210, 0.6); font-size: 0.67rem; line-height: 1.45; }
.outcome-copy ul { display: flex; flex-wrap: wrap; gap: 5px; margin: 9px 0 0; padding: 0; list-style: none; }
.outcome-copy li { display: inline-flex; align-items: center; gap: 4px; padding: 5px 7px; border-radius: 6px; color: color-mix(in srgb, var(--tone) 64%, white); background: color-mix(in srgb, var(--tone) 10%, transparent); font-size: 0.58rem; }

.choice-section { padding: 18px; }
.choice-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 10px; }
.choice-heading > div { display: grid; gap: 4px; }
.choice-heading span { color: #c9aa67; font: 800 0.58rem/1 var(--font-mono, monospace); letter-spacing: 0.1em; text-transform: uppercase; }
.choice-heading strong { color: rgba(240, 230, 210, 0.48); font-size: 0.63rem; font-weight: 500; }
.choice-heading > svg { color: rgba(214, 188, 132, 0.55); }
.event-choices { display: grid; gap: 8px; }
.event-choice { position: relative; display: grid; min-width: 0; grid-template-columns: 35px minmax(0, 1fr) auto; align-items: stretch; overflow: hidden; padding: 0; border: 1px solid rgba(225, 199, 145, 0.14); border-radius: 10px; color: #eee5d1; text-align: left; background: rgba(255, 255, 255, 0.025); cursor: pointer; transition: 140ms ease; }
.event-choice:hover:not(:disabled), .event-choice.selected { border-color: rgba(214, 181, 109, 0.55); background: rgba(202, 166, 91, 0.065); transform: translateX(3px); }
.event-choice:focus-visible { outline: 2px solid #e8ce8c; outline-offset: 2px; }
.event-choice:disabled { cursor: not-allowed; }
.event-choice.muted { opacity: 0.42; }
.event-choice.selected { opacity: 1; border-color: var(--tone); }
.choice-index { display: grid; place-items: center; border-right: 1px solid rgba(225, 199, 145, 0.12); color: #cdb371; background: rgba(8, 10, 8, 0.32); font: 800 0.7rem/1 var(--font-mono, monospace); }
.choice-content { display: grid; gap: 5px; min-width: 0; padding: 11px 13px; }
.choice-content > strong { color: #f2e7d1; font: 700 0.95rem/1.05 Georgia, serif; }
.choice-description { color: rgba(240, 230, 210, 0.55); font-size: 0.65rem; line-height: 1.4; }
.string-group { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; margin-top: 3px; }
.string-group b, .string-group > span { display: inline-flex; align-items: center; gap: 4px; padding: 4px 6px; border-radius: 5px; font-size: 0.54rem; }
.string-group b { color: rgba(240, 230, 210, 0.48); background: rgba(255, 255, 255, 0.03); font: 800 0.51rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.string-group.costs > span { color: #e0c481; background: rgba(202, 166, 91, 0.09); }
.string-group.effects > span { color: #9ac49a; background: rgba(85, 143, 88, 0.09); }
.string-group.free-cost > span { color: rgba(240, 230, 210, 0.38); background: rgba(255, 255, 255, 0.025); }
.choice-action { display: flex; min-width: 105px; align-items: center; justify-content: center; gap: 4px; padding: 9px 11px; border-left: 1px solid rgba(225, 199, 145, 0.11); color: #d8c180; background: rgba(8, 10, 8, 0.2); font: 800 0.55rem/1 var(--font-mono, monospace); text-align: center; text-transform: uppercase; }
.event-choice:disabled:not(.selected) .choice-action { color: #c4877c; }

.event-footer { display: flex; align-items: center; justify-content: center; gap: 7px; padding: 11px 16px; border-top: 1px solid rgba(225, 199, 145, 0.12); color: rgba(240, 230, 210, 0.4); background: rgba(7, 9, 7, 0.3); font-size: 0.61rem; }

.event-dialog-enter-active, .event-dialog-leave-active { transition: opacity 150ms ease; }
.event-dialog-enter-active .event-dialog, .event-dialog-leave-active .event-dialog { transition: opacity 150ms ease, transform 180ms ease; }
.event-dialog-enter-from, .event-dialog-leave-to { opacity: 0; }
.event-dialog-enter-from .event-dialog, .event-dialog-leave-to .event-dialog { opacity: 0; transform: translateY(12px) scale(0.985); }

@media (max-width: 650px) {
  .event-backdrop { align-items: end; padding: 0; }
  .event-dialog { width: 100%; max-height: 95vh; border-right: 0; border-bottom: 0; border-left: 0; border-radius: 16px 16px 0 0; }
  .event-hero { min-height: 180px; grid-template-columns: 1fr; padding: 19px; }
  .event-seal { width: 42px; height: 42px; }
  .choice-section { padding: 12px; }
  .event-choice { grid-template-columns: 31px minmax(0, 1fr); }
  .choice-action { grid-column: 2; min-height: 34px; border-top: 1px solid rgba(225, 199, 145, 0.1); border-left: 0; }
  .event-footer { text-align: center; }
}

@media (prefers-reduced-motion: reduce) {
  .event-choice, .event-dialog-enter-active, .event-dialog-leave-active,
  .event-dialog-enter-active .event-dialog, .event-dialog-leave-active .event-dialog { transition: none; }
  .event-choice:hover:not(:disabled), .event-choice.selected { transform: none; }
}
</style>
