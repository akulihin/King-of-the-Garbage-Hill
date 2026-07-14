<script setup lang="ts">
import { computed } from 'vue'
import { Activity, Clock3, Shield, Swords, Trophy, X } from 'lucide-vue-next'
import { useFocusTrapDialog } from 'src/composables/useFocusTrapDialog'
import { currentLocale } from 'src/i18n'
import { DAMAGE_TYPES } from 'src/features/fight-calculator/types'
import type { CollisionStep, CollisionStepKind, CollisionSummary } from 'src/features/fight-calculator/types'

const props = defineProps<{ collision: CollisionSummary }>()
const emit = defineEmits<{ close: [] }>()
const { overlayRef, dialogRef, trapTabKey } = useFocusTrapDialog()

type StrikeStep = CollisionStep & { messages: string[] }

const openingStep = computed(() => props.collision.steps.find(step => step.kind === 'movement') ?? null)
const ongoingEffects = computed(() => props.collision.steps.filter(step => step.kind === 'bleed'))
const strikeSteps = computed<StrikeStep[]>(() => {
  const strikes: StrikeStep[] = []
  for (const step of props.collision.steps) {
    if (step.kind === 'block' || step.kind === 'damage') {
      strikes.push({ ...step, messages: [step.message] })
      continue
    }
    const latest = strikes.at(-1)
    if (latest && (step.kind === 'effect' || step.kind === 'weapon') && step.time === latest.time) {
      latest.messages.push(step.message)
      latest.snapshot = step.snapshot
    }
  }
  return strikes
})

function t(ru: string, en: string): string {
  return currentLocale.value === 'ru' ? ru : en
}

function phaseLabel(phase: CollisionSummary['phase']): string {
  if (phase === 'mirror') return t('Зеркальные слоты', 'Mirrored slots')
  if (phase === 'fallback') return t('Случайный свободный соперник', 'Random free opponent')
  return t('Бой выживших', 'Survivor fight')
}

function kindLabel(kind: CollisionStepKind): string {
  const labels: Record<CollisionStepKind, [string, string]> = {
    movement: ['Сближение', 'Movement'], attack: ['Атака', 'Attack'], block: ['Блок', 'Block'],
    damage: ['Пробитие', 'Penetration'], effect: ['Эффект', 'Effect'], weapon: ['Оружие', 'Weapon'],
    bleed: ['Кровь', 'Bleed'], result: ['Итог', 'Result'],
  }
  return t(labels[kind][0], labels[kind][1])
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    event.preventDefault()
    emit('close')
    return
  }
  trapTabKey(event)
}
</script>

<template>
  <Teleport to="body">
    <Transition name="fc-modal" appear>
      <div ref="overlayRef" class="details-overlay" @click.self="emit('close')">
        <section ref="dialogRef" class="details-dialog" role="dialog" aria-modal="true" aria-labelledby="collision-title" tabindex="-1" @keydown="onKeydown">
          <header class="details-header">
            <div>
              <span class="eyebrow">{{ phaseLabel(collision.phase) }} · #{{ collision.order }}</span>
              <h2 id="collision-title"><Swords :size="20" /> {{ collision.team1Name }} <span>vs</span> {{ collision.team2Name }}</h2>
            </div>
            <button class="icon-button" type="button" :aria-label="t('Закрыть', 'Close')" @click="emit('close')"><X :size="19" /></button>
          </header>

          <div class="collision-summary">
            <span><Clock3 :size="15" /> {{ collision.duration.toFixed(2) }} {{ t('сек', 'sec') }}</span>
            <strong><Trophy :size="16" /> {{ collision.winnerName ?? t('Нет победителя', 'No winner') }}</strong>
          </div>

          <p v-if="openingStep" class="opening-note">{{ openingStep.message }}</p>

          <ol class="steps-list">
            <li v-for="(step, strikeIndex) in strikeSteps" :key="step.index" class="step-card" :class="`step-card--${step.kind}`">
              <div class="step-rail"><span>{{ strikeIndex + 1 }}</span><small>{{ step.time.toFixed(2) }}s</small></div>
              <article>
                <header><span class="kind-pill">{{ kindLabel(step.kind) }}</span><strong>{{ step.actorName }}</strong><span v-if="step.weaponName" class="weapon-name">{{ step.weaponName }}</span></header>
                <p v-for="message in step.messages" :key="message">{{ message }}</p>
                <div v-if="step.technique" class="attack-equation"><Activity :size="14" /><b>{{ step.technique }}</b><span>{{ step.penetration }} ATK</span><span>vs</span><span>{{ step.resistance }} RES</span><strong v-if="step.damage">−{{ step.damage }} HP</strong></div>
                <details>
                  <summary><Shield :size="14" /> {{ t('Статы в момент этапа', 'Stats at this step') }}</summary>
                  <div class="snapshot-grid">
                    <div><h4>{{ step.actorName }}</h4><p>HP {{ step.snapshot.actorHp }} · {{ t('Усталость', 'Fatigue') }} {{ step.snapshot.actorFatigue }} · {{ t('Прочность', 'Durability') }} {{ step.snapshot.actorDurability ?? '∞' }}</p><div class="resist-row"><span v-for="type in DAMAGE_TYPES" :key="type">{{ type.slice(0, 3) }} {{ step.snapshot.actorResists[type] }}</span></div></div>
                    <div><h4>{{ step.targetName }}</h4><p>HP {{ step.snapshot.targetHp }} · {{ t('Усталость', 'Fatigue') }} {{ step.snapshot.targetFatigue }} · {{ t('Прочность', 'Durability') }} {{ step.snapshot.targetDurability ?? '∞' }}</p><div class="resist-row"><span v-for="type in DAMAGE_TYPES" :key="type">{{ type.slice(0, 3) }} {{ step.snapshot.targetResists[type] }}</span></div></div>
                  </div>
                </details>
              </article>
            </li>
          </ol>

          <div v-if="ongoingEffects.length" class="ongoing-effects">
            <h3>{{ t('Периодические эффекты', 'Ongoing effects') }}</h3>
            <p v-for="effect in ongoingEffects" :key="effect.index"><span>{{ effect.time.toFixed(2) }}s</span> {{ effect.message }}</p>
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.details-overlay { position: fixed; z-index: 4200; inset: 0; display: grid; place-items: center; padding: 14px; background: rgba(4, 4, 7, .86); backdrop-filter: blur(9px); }
.details-dialog { display: flex; flex-direction: column; width: min(980px, 100%); max-height: 95vh; overflow: hidden; border: 1px solid var(--border-color); border-radius: 18px; outline: none; background: linear-gradient(155deg, var(--bg-card), var(--bg-secondary)); box-shadow: 0 25px 90px rgba(0, 0, 0, .7); }
.details-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 16px 18px; border-bottom: 1px solid var(--border-subtle); }.eyebrow { color: var(--accent-gold); font: 800 .67rem/1 var(--font-mono); letter-spacing: .08em; text-transform: uppercase; } h2 { display: flex; align-items: center; gap: 7px; margin: 5px 0 0; font-size: 1.15rem; } h2 span { color: var(--text-dim); font-size: .75em; }
.icon-button { display: grid; width: 38px; height: 38px; place-items: center; border: 1px solid var(--border-color); border-radius: 10px; color: var(--text-muted); background: var(--bg-inset); cursor: pointer; }.collision-summary { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 10px 18px; border-bottom: 1px solid var(--border-subtle); background: var(--bg-inset); }.collision-summary span, .collision-summary strong { display: inline-flex; align-items: center; gap: 6px; }.collision-summary strong { color: var(--accent-gold); }
.opening-note { margin: 0; padding: 9px 18px; border-bottom: 1px solid var(--border-subtle); color: var(--text-muted); background: color-mix(in srgb, var(--bg-inset) 72%, transparent); font-size: .72rem; line-height: 1.4; }.steps-list { margin: 0; padding: 17px 18px 28px; overflow-y: auto; list-style: none; }.step-card { display: grid; grid-template-columns: 54px 1fr; gap: 11px; margin-bottom: 10px; }.step-rail { display: flex; flex-direction: column; align-items: center; gap: 3px; padding-top: 7px; color: var(--text-muted); }.step-rail span { display: grid; width: 30px; height: 30px; place-items: center; border: 1px solid var(--border-color); border-radius: 50%; color: var(--text-primary); background: var(--bg-inset); font-weight: 900; }.step-rail small { font: 700 .62rem/1 var(--font-mono); }
.step-card article { padding: 11px 13px; border: 1px solid var(--border-subtle); border-left: 3px solid var(--accent-blue); border-radius: 11px; background: color-mix(in srgb, var(--bg-inset) 72%, transparent); }.step-card--block article { border-left-color: var(--accent-gold); }.step-card--damage article, .step-card--bleed article { border-left-color: var(--accent-red); }.step-card--effect article { border-left-color: var(--accent-purple); }.step-card--result article { border-left-color: var(--accent-green); }
.step-card article > header { display: flex; align-items: center; gap: 7px; flex-wrap: wrap; }.kind-pill { padding: 3px 6px; border-radius: 5px; color: var(--text-muted); background: var(--bg-card); font: 800 .62rem/1 var(--font-mono); text-transform: uppercase; }.weapon-name { color: var(--text-muted); font-size: .7rem; }.step-card p { margin: 7px 0 0; color: var(--text-secondary); font-size: .79rem; line-height: 1.45; }.attack-equation { display: flex; align-items: center; gap: 7px; margin-top: 8px; padding: 7px 9px; border-radius: 7px; color: var(--text-muted); background: var(--bg-card); font: 700 .68rem/1 var(--font-mono); }.attack-equation strong { color: var(--accent-red); margin-left: auto; }
details { margin-top: 8px; } summary { display: inline-flex; align-items: center; gap: 5px; color: var(--text-muted); cursor: pointer; font-size: .7rem; font-weight: 750; }.snapshot-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 8px; margin-top: 8px; }.snapshot-grid > div { padding: 8px; border-radius: 7px; background: var(--bg-card); }.snapshot-grid h4 { margin: 0; font-size: .72rem; }.snapshot-grid p { margin: 4px 0 !important; font: 650 .65rem/1.4 var(--font-mono); }.resist-row { display: flex; gap: 4px; flex-wrap: wrap; }.resist-row span { padding: 2px 4px; border-radius: 4px; color: var(--text-muted); background: var(--bg-inset); font: 650 .58rem/1 var(--font-mono); }
.ongoing-effects { padding: 13px 18px 18px; border-top: 1px solid var(--border-subtle); background: var(--bg-inset); }.ongoing-effects h3 { margin: 0 0 7px; font-size: .78rem; }.ongoing-effects p { margin: 4px 0; color: var(--text-secondary); font-size: .7rem; }.ongoing-effects span { display: inline-block; min-width: 48px; color: var(--accent-red); font-family: var(--font-mono); font-weight: 800; }
button:focus-visible, summary:focus-visible { outline: 2px solid var(--accent-blue); outline-offset: 2px; }.fc-modal-enter-active, .fc-modal-leave-active { transition: opacity .18s ease; }.fc-modal-enter-from, .fc-modal-leave-to { opacity: 0; }
@media (max-width: 650px) { .details-overlay { padding: 5px; }.details-dialog { max-height: 98vh; border-radius: 11px; }.steps-list { padding: 12px 8px 20px; }.step-card { grid-template-columns: 38px 1fr; gap: 5px; }.snapshot-grid { grid-template-columns: 1fr; }.attack-equation { flex-wrap: wrap; } }
</style>
