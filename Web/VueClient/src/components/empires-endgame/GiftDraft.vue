<script setup lang="ts">
import { computed } from 'vue'
import {
  Check,
  Crown,
  Gift,
  Scale,
  Sparkles,
  Star,
} from 'lucide-vue-next'

interface GiftChoiceView {
  id: string
  name: string
  description: string
  kind: string
  rarity: string
  weight: number
  imageUrl?: string
  effects: string[]
  disabled?: boolean
}

const props = withDefaults(defineProps<{
  choices: GiftChoiceView[]
  title?: string
  description?: string
  selectedId?: string | null
  disabled?: boolean
}>(), {
  title: 'Выберите божественный дар',
  description: 'Бог Азарта признаёт вашу игру. Один дар перейдёт империи, остальные исчезнут.',
  selectedId: null,
  disabled: false,
})

const emit = defineEmits<{
  choose: [id: string]
}>()

const visibleChoices = computed(() => props.choices.slice(0, 3))
const totalWeight = computed(() => visibleChoices.value.reduce((sum, choice) => sum + Math.max(0, choice.weight), 0))

function weightShare(choice: GiftChoiceView) {
  if (totalWeight.value <= 0) return 0
  return Math.round(Math.max(0, choice.weight) / totalWeight.value * 100)
}

function rarityClass(rarity: string) {
  const normalized = rarity.toLocaleLowerCase('ru-RU')
  if (normalized.includes('легенд') || normalized.includes('legend')) return 'legendary'
  if (normalized.includes('эпич') || normalized.includes('epic')) return 'epic'
  if (normalized.includes('редк') || normalized.includes('rare')) return 'rare'
  if (normalized.includes('необыч') || normalized.includes('uncommon')) return 'uncommon'
  return 'common'
}

function choose(choice: GiftChoiceView) {
  if (props.disabled || choice.disabled) return
  emit('choose', choice.id)
}

function hideBrokenImage(event: Event) {
  ;(event.currentTarget as HTMLImageElement).hidden = true
}
</script>

<template>
  <section class="gift-draft" aria-labelledby="gift-draft-title">
    <header class="draft-heading">
      <span class="draft-seal"><Gift :size="23" aria-hidden="true" /></span>
      <div>
        <span>Награда за кон</span>
        <h2 id="gift-draft-title">{{ title }}</h2>
        <p>{{ description }}</p>
      </div>
      <div class="choice-count"><Sparkles :size="14" /> {{ visibleChoices.length }} дара</div>
    </header>

    <div class="gift-grid" role="group" aria-label="Доступные божественные дары">
      <button
        v-for="(choice, index) in visibleChoices"
        :key="choice.id"
        type="button"
        class="gift-card"
        :class="[
          `rarity-${rarityClass(choice.rarity)}`,
          { selected: choice.id === selectedId },
        ]"
        :disabled="disabled || choice.disabled"
        :aria-pressed="choice.id === selectedId"
        :aria-label="`Выбрать дар «${choice.name}». ${choice.description}`"
        @click="choose(choice)"
      >
        <span class="card-number">0{{ index + 1 }}</span>
        <span class="gift-visual">
          <img v-if="choice.imageUrl" :src="choice.imageUrl" alt="" @error="hideBrokenImage" />
          <span v-else class="visual-placeholder" aria-hidden="true">
            <Crown :size="42" />
          </span>
          <span class="rarity-ribbon"><Star :size="11" /> {{ choice.rarity }}</span>
          <span v-if="choice.id === selectedId" class="selected-mark"><Check :size="16" /> Выбрано</span>
        </span>

        <span class="gift-copy">
          <span class="gift-meta">
            <b>{{ choice.kind }}</b>
            <em title="Относительный вес в этом наборе">
              <Scale :size="12" /> вес {{ choice.weight }} · {{ weightShare(choice) }}%
            </em>
          </span>
          <strong>{{ choice.name }}</strong>
          <span class="gift-description">{{ choice.description }}</span>
        </span>

        <span class="effect-list">
          <span v-for="effect in choice.effects" :key="effect">
            <Sparkles :size="12" aria-hidden="true" />
            {{ effect }}
          </span>
          <span v-if="!choice.effects.length" class="empty-effect">Эффект скрыт волей Бога</span>
        </span>

        <span class="choose-label">
          {{ choice.disabled ? 'Недоступно' : choice.id === selectedId ? 'Дар назначен' : 'Принять дар' }}
        </span>
      </button>

      <div v-for="index in Math.max(0, 3 - visibleChoices.length)" :key="`empty-${index}`" class="gift-card empty-card">
        <Gift :size="28" />
        <strong>Дар ещё не проявился</strong>
      </div>
    </div>
  </section>
</template>

<style scoped>
.gift-draft {
  --gift-gold: #d0ad63;
  overflow: hidden;
  border: 1px solid rgba(225, 199, 145, 0.2);
  border-radius: 18px;
  color: #f0e6d2;
  background: radial-gradient(circle at 50% -10%, rgba(202, 166, 91, 0.12), transparent 42%), #10130f;
  box-shadow: 0 24px 70px rgba(0, 0, 0, 0.34);
}

.draft-heading {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 14px;
  padding: 18px 22px;
  border-bottom: 1px solid rgba(225, 199, 145, 0.15);
  background: linear-gradient(105deg, #252017, #192019 65%, #171913);
}
.draft-seal { display: grid; width: 46px; height: 46px; place-items: center; border: 1px solid rgba(224, 193, 126, 0.32); border-radius: 50%; color: #f0d58e; background: rgba(202, 166, 91, 0.09); box-shadow: inset 0 0 22px rgba(202, 166, 91, 0.11); }
.draft-heading > div > span { color: var(--gift-gold); font: 800 0.59rem/1 var(--font-mono, monospace); letter-spacing: 0.13em; text-transform: uppercase; }
.draft-heading h2 { margin: 5px 0 3px; color: #f8edd8; font: 700 clamp(1.25rem, 2.4vw, 1.75rem)/1.05 Georgia, serif; }
.draft-heading p { max-width: 660px; margin: 0; color: rgba(240, 230, 210, 0.55); font-size: 0.7rem; line-height: 1.4; }
.choice-count { display: inline-flex; align-items: center; gap: 5px; padding: 7px 9px; border: 1px solid rgba(224, 193, 126, 0.18); border-radius: 999px; color: #dcc488; background: rgba(202, 166, 91, 0.07); font: 800 0.57rem/1 var(--font-mono, monospace); text-transform: uppercase; }

.gift-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px; padding: 20px; }
.gift-card {
  --rarity: #858b7a;
  position: relative;
  display: grid;
  min-width: 0;
  grid-template-rows: 180px auto 1fr auto;
  overflow: hidden;
  padding: 0;
  border: 1px solid color-mix(in srgb, var(--rarity) 45%, #3d3d34);
  border-radius: 14px;
  color: #eee4cf;
  text-align: left;
  background: linear-gradient(155deg, rgba(35, 37, 29, 0.98), rgba(17, 20, 15, 0.99));
  box-shadow: 0 16px 36px rgba(0, 0, 0, 0.28);
  cursor: pointer;
  transition: border-color 150ms ease, box-shadow 150ms ease, transform 150ms ease;
}
.gift-card:hover:not(:disabled), .gift-card.selected { border-color: var(--rarity); box-shadow: 0 20px 42px rgba(0, 0, 0, 0.38), 0 0 0 1px color-mix(in srgb, var(--rarity) 38%, transparent); transform: translateY(-4px); }
.gift-card:focus-visible { outline: 2px solid #f4dda1; outline-offset: 3px; }
.gift-card:disabled { opacity: 0.52; cursor: not-allowed; filter: saturate(0.7); }
.rarity-uncommon { --rarity: #71a970; }
.rarity-rare { --rarity: #5d9fc0; }
.rarity-epic { --rarity: #a574bf; }
.rarity-legendary { --rarity: #d0a348; }
.card-number { position: absolute; z-index: 3; top: 10px; left: 11px; color: rgba(249, 237, 215, 0.7); font: 800 0.57rem/1 var(--font-mono, monospace); letter-spacing: 0.1em; text-shadow: 0 1px 6px rgba(0, 0, 0, 0.7); }
.gift-visual { position: relative; display: grid; min-height: 180px; place-items: center; overflow: hidden; background: radial-gradient(circle at 50% 35%, color-mix(in srgb, var(--rarity) 22%, transparent), transparent 52%), #171a14; }
.gift-visual::after { content: ''; position: absolute; inset: 0; background: linear-gradient(transparent 45%, rgba(16, 19, 15, 0.93)); pointer-events: none; }
.gift-visual > img { width: 100%; height: 100%; object-fit: cover; opacity: 0.78; }
.visual-placeholder { color: color-mix(in srgb, var(--rarity) 68%, #f1dfb5); filter: drop-shadow(0 0 16px color-mix(in srgb, var(--rarity) 35%, transparent)); }
.rarity-ribbon { position: absolute; z-index: 2; right: 9px; bottom: 8px; display: inline-flex; align-items: center; gap: 4px; padding: 5px 7px; border: 1px solid color-mix(in srgb, var(--rarity) 45%, transparent); border-radius: 999px; color: color-mix(in srgb, var(--rarity) 62%, white); background: rgba(10, 12, 9, 0.79); font: 800 0.52rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.selected-mark { position: absolute; z-index: 3; top: 9px; right: 9px; display: inline-flex; align-items: center; gap: 4px; padding: 5px 7px; border-radius: 999px; color: #d9f0cf; background: rgba(48, 102, 53, 0.86); font: 800 0.51rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.gift-copy { display: grid; gap: 7px; padding: 14px 14px 10px; }
.gift-meta { display: flex; align-items: center; justify-content: space-between; gap: 7px; }
.gift-meta b { color: var(--rarity); font: 800 0.55rem/1 var(--font-mono, monospace); letter-spacing: 0.09em; text-transform: uppercase; }
.gift-meta em { display: inline-flex; align-items: center; gap: 3px; color: rgba(240, 230, 210, 0.42); font: 700 0.5rem/1 var(--font-mono, monospace); font-style: normal; }
.gift-copy > strong { overflow-wrap: anywhere; color: #f5ead3; font: 700 1.18rem/1.05 Georgia, serif; }
.gift-description { color: rgba(240, 230, 210, 0.58); font-size: 0.67rem; line-height: 1.45; }
.effect-list { display: grid; align-content: start; gap: 5px; padding: 0 14px 13px; }
.effect-list > span { display: flex; align-items: flex-start; gap: 5px; padding: 7px 8px; border-left: 2px solid color-mix(in srgb, var(--rarity) 72%, #76766b); border-radius: 0 6px 6px 0; color: #d8ccb5; background: color-mix(in srgb, var(--rarity) 7%, transparent); font-size: 0.61rem; line-height: 1.35; }
.effect-list svg { flex: 0 0 auto; margin-top: 1px; color: var(--rarity); }
.effect-list .empty-effect { border-left-color: rgba(224, 201, 151, 0.16); color: rgba(240, 230, 210, 0.34); font-style: italic; }
.choose-label { display: grid; min-height: 40px; place-items: center; border-top: 1px solid color-mix(in srgb, var(--rarity) 24%, transparent); color: color-mix(in srgb, var(--rarity) 62%, white); background: color-mix(in srgb, var(--rarity) 8%, #12140f); font: 800 0.62rem/1 var(--font-mono, monospace); letter-spacing: 0.08em; text-transform: uppercase; }
.empty-card { min-height: 410px; place-content: center; justify-items: center; gap: 10px; border-style: dashed; color: rgba(240, 230, 210, 0.28); cursor: default; }
.empty-card strong { font-size: 0.67rem; font-weight: 600; }

@media (max-width: 860px) {
  .gift-grid { grid-template-columns: 1fr; }
  .gift-card { grid-template-columns: minmax(150px, 0.42fr) minmax(0, 1fr); grid-template-rows: auto 1fr auto; }
  .gift-visual { grid-row: 1 / 4; min-height: 250px; }
  .gift-copy { grid-column: 2; }
  .effect-list { grid-column: 2; }
  .choose-label { grid-column: 2; }
  .empty-card { display: none; }
}

@media (max-width: 560px) {
  .draft-heading { grid-template-columns: auto minmax(0, 1fr); padding: 15px; }
  .choice-count { display: none; }
  .gift-grid { gap: 11px; padding: 12px; }
  .gift-card { grid-template-columns: 1fr; grid-template-rows: 155px auto 1fr auto; }
  .gift-visual { grid-row: auto; min-height: 155px; }
  .gift-copy, .effect-list, .choose-label { grid-column: auto; }
}

@media (prefers-reduced-motion: reduce) {
  .gift-card { transition: none; }
  .gift-card:hover:not(:disabled), .gift-card.selected { transform: none; }
}
</style>
