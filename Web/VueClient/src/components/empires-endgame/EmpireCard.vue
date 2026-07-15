<script setup lang="ts">
import { computed } from 'vue'
import { Clock3, Crown, Gem, RotateCcw, Sparkles } from 'lucide-vue-next'

const props = withDefaults(defineProps<{
  id?: string
  name?: string
  title?: string
  suit?: string
  rank?: string
  timeCost?: number
  value?: number
  description?: string
  image?: string
  inverted?: boolean
  upgrades?: number
  faceDown?: boolean
  disabled?: boolean
  selected?: boolean
  compact?: boolean
  trump?: boolean
  interactive?: boolean
  badge?: string
}>(), {
  id: '',
  name: '',
  title: '',
  suit: 'clubs',
  rank: '',
  timeCost: 0,
  value: 0,
  description: '',
  image: '',
  inverted: false,
  upgrades: 0,
  faceDown: false,
  disabled: false,
  selected: false,
  compact: false,
  trump: false,
  interactive: false,
  badge: '',
})

const emit = defineEmits<{ choose: [id: string] }>()

const suitSymbol = computed(() => ({
  diamonds: '♦',
  hearts: '♥',
  clubs: '♣',
  spades: '♠',
  joker: '✦',
}[props.suit] ?? props.suit))

const suitLabel = computed(() => ({
  diamonds: 'Экономика',
  hearts: 'Корона',
  clubs: 'Народ',
  spades: 'Прогресс',
  joker: 'Шут',
}[props.suit] ?? props.suit))

const artStyle = computed(() => props.image ? { backgroundImage: `url(${JSON.stringify(props.image).slice(1, -1)})` } : undefined)

function choose() {
  if (!props.disabled && props.interactive && !props.faceDown) emit('choose', props.id)
}
</script>

<template>
  <component
    :is="interactive ? 'button' : 'article'"
    class="empire-card"
    :class="[
      `suit-${suit}`,
      {
        inverted,
        compact,
        selected,
        disabled,
        interactive,
        trump,
        'face-down': faceDown,
      },
    ]"
    :type="interactive ? 'button' : undefined"
    :disabled="interactive ? disabled : undefined"
    :aria-label="faceDown ? 'Закрытая карта Бога' : `${rank} ${suitLabel}: ${name || title}`"
    :aria-pressed="interactive ? selected : undefined"
    @click="choose"
  >
    <template v-if="faceDown">
      <div class="card-back-pattern" aria-hidden="true">
        <span>✦</span>
        <i />
        <b>G</b>
      </div>
    </template>
    <template v-else>
      <div class="card-edge" aria-hidden="true" />
      <header class="card-meta">
        <div class="rank-suit">
          <strong>{{ rank }}</strong>
          <span>{{ suitSymbol }}</span>
        </div>
        <strong class="card-name">{{ name || title }}</strong>
        <div class="card-numbers">
          <span title="Времязатратность"><Clock3 :size="11" />{{ timeCost }}</span>
          <span title="Ценность"><Gem :size="11" />{{ value }}</span>
        </div>
      </header>

      <div class="card-art" :style="artStyle">
        <div v-if="!image" class="art-placeholder" aria-hidden="true">
          <span class="art-sigil">{{ suitSymbol }}</span>
          <Crown v-if="['K', 'Q', 'A'].includes(rank)" class="art-crown" :size="32" />
          <Sparkles v-else :size="26" />
        </div>
        <span v-if="trump" class="trump-seal">Козырь</span>
        <span v-if="inverted" class="inverted-seal"><RotateCcw :size="11" /> Перевёрнута</span>
        <span v-if="badge" class="custom-badge">{{ badge }}</span>
      </div>

      <div class="card-copy">
        <span class="suit-label">{{ suitLabel }}</span>
        <h3>{{ title || `${rank} ${suitSymbol}` }}</h3>
        <p>{{ description || 'Пассивная способность ещё не описана.' }}</p>
      </div>

      <footer class="card-footer">
        <div class="upgrade-pips" :title="`Улучшений: ${upgrades}`">
          <span v-for="n in Math.max(1, Math.min(5, upgrades + 1))" :key="n" :class="{ filled: n <= upgrades }" />
        </div>
        <small>{{ inverted ? 'вредит империи' : 'служит империи' }}</small>
      </footer>
    </template>
  </component>
</template>

<style scoped>
.empire-card {
  --suit: #ece1c4;
  --suit-deep: #877d66;
  position: relative;
  display: grid;
  width: 184px;
  min-width: 184px;
  height: 292px;
  grid-template-rows: auto 132px minmax(0, 1fr) auto;
  overflow: hidden;
  padding: 0;
  border: 1px solid color-mix(in srgb, var(--suit) 55%, #2f291d);
  border-radius: 12px;
  color: #271f16;
  background: linear-gradient(145deg, #f4ead2, #d7c7a7 65%, #b6a486);
  box-shadow: 0 9px 24px rgba(0, 0, 0, 0.35), inset 0 0 0 3px rgba(255, 252, 237, 0.3);
  font: inherit;
  text-align: left;
  transform-origin: center bottom;
  transition: transform 160ms ease, filter 160ms ease, box-shadow 160ms ease;
}
.suit-diamonds { --suit: #c6554f; --suit-deep: #743733; }
.suit-hearts { --suit: #ae3548; --suit-deep: #652332; }
.suit-clubs { --suit: #486b45; --suit-deep: #263e2b; }
.suit-spades { --suit: #3d4d5d; --suit-deep: #202a36; }
.suit-joker { --suit: #b89045; --suit-deep: #5c3e5f; }
.empire-card.interactive { cursor: pointer; }
.empire-card.interactive:hover:not(:disabled) { z-index: 3; filter: brightness(1.04); box-shadow: 0 14px 34px rgba(0,0,0,.45), 0 0 0 2px color-mix(in srgb, var(--suit) 72%, #fff); transform: translateY(-10px) rotate(.5deg); }
.empire-card.selected { box-shadow: 0 16px 36px rgba(0,0,0,.5), 0 0 0 3px #f1cc72; transform: translateY(-12px); }
.empire-card.disabled { filter: grayscale(.6) brightness(.7); }
.empire-card.trump { box-shadow: 0 9px 24px rgba(0,0,0,.35), 0 0 0 2px #d8b45d, 0 0 23px rgba(216,180,93,.28); }
.card-edge { position: absolute; z-index: 3; inset: 5px; border: 1px solid color-mix(in srgb, var(--suit) 42%, transparent); border-radius: 8px; pointer-events: none; }
.card-meta { position: relative; z-index: 1; display: flex; min-height: 48px; align-items: center; justify-content: space-between; padding: 7px 10px 5px; border-bottom: 1px solid rgba(64,49,31,.14); background: rgba(255,255,255,.14); }
.rank-suit { display: flex; align-items: baseline; gap: 4px; color: var(--suit-deep); }
.rank-suit strong { font: 800 1.34rem/1 Georgia, serif; }
.rank-suit span { font: 800 1.16rem/1 Georgia, serif; }
.card-name { min-width: 0; overflow: hidden; padding: 0 5px; color: #443927; font: 800 .58rem/1.1 Georgia, serif; text-align: center; text-overflow: ellipsis; white-space: nowrap; }
.card-numbers { display: flex; gap: 5px; }
.card-numbers span { display: inline-flex; min-width: 31px; align-items: center; justify-content: center; gap: 2px; padding: 3px 4px; border-radius: 5px; color: #504531; background: rgba(71,56,35,.08); font: 800 .58rem/1 var(--font-mono, monospace); }
.card-art { position: relative; overflow: hidden; border-bottom: 1px solid rgba(55,44,29,.18); background-color: color-mix(in srgb, var(--suit) 16%, #c7b892); background-position: center; background-size: cover; }
.card-art::after { content: ''; position: absolute; inset: 0; background: linear-gradient(180deg, transparent 52%, rgba(27,21,14,.32)); pointer-events: none; }
.art-placeholder { position: absolute; inset: 0; display: grid; place-content: center; place-items: center; gap: 4px; color: color-mix(in srgb, var(--suit-deep) 83%, #1c1914); background: radial-gradient(circle, color-mix(in srgb, var(--suit) 25%, transparent), transparent 58%), repeating-linear-gradient(42deg, transparent 0 12px, rgba(255,255,255,.055) 13px 14px); }
.art-sigil { position: absolute; opacity: .12; font: 800 7rem/1 Georgia, serif; }
.art-crown { margin-bottom: -5px; }
.trump-seal,.inverted-seal,.custom-badge { position: absolute; z-index: 2; display: inline-flex; align-items: center; gap: 3px; padding: 3px 5px; border-radius: 4px; color: #261d0f; background: #e1bd68; font: 900 .48rem/1 var(--font-mono, monospace); text-transform: uppercase; }
.trump-seal { top: 6px; right: 6px; }
.inverted-seal { right: 6px; bottom: 6px; color: #f5d8dc; background: #632b39; }
.custom-badge { top: 6px; left: 6px; color: #e8e0cf; background: rgba(28,27,23,.82); }
.card-copy { position: relative; z-index: 1; min-height: 0; padding: 8px 11px 4px; }
.suit-label { display: block; margin-bottom: 3px; color: var(--suit-deep); font: 900 .48rem/1 var(--font-mono, monospace); letter-spacing: .1em; text-transform: uppercase; }
.card-copy h3 { display: -webkit-box; margin: 0 0 4px; overflow: hidden; color: #2b2218; font: 800 .78rem/1.12 Georgia, serif; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.card-copy p { display: -webkit-box; margin: 0; overflow: hidden; color: rgba(43,34,24,.72); font-size: .57rem; line-height: 1.35; -webkit-box-orient: vertical; -webkit-line-clamp: 3; }
.card-footer { position: relative; z-index: 1; display: flex; align-items: center; justify-content: space-between; gap: 5px; padding: 5px 10px 8px; }
.card-footer small { overflow: hidden; color: rgba(43,34,24,.48); font: 700 .46rem/1 var(--font-mono, monospace); text-overflow: ellipsis; white-space: nowrap; }
.upgrade-pips { display: flex; gap: 2px; }
.upgrade-pips span { width: 5px; height: 5px; border: 1px solid var(--suit-deep); border-radius: 50%; opacity: .45; }
.upgrade-pips span.filled { background: var(--suit-deep); opacity: 1; }

.empire-card.inverted { color: #eee4df; border-color: color-mix(in srgb, var(--suit) 45%, #151015); background: linear-gradient(145deg, #2f2931, #17151b 62%, #0b0a0e); box-shadow: 0 10px 27px rgba(0,0,0,.56), inset 0 0 0 3px rgba(123,79,102,.15); }
.empire-card.inverted::before { content: ''; position: absolute; z-index: 0; inset: 0; opacity: .22; background: repeating-radial-gradient(ellipse at 50% 30%, transparent 0 13px, color-mix(in srgb, var(--suit) 32%, #53263f) 14px 15px); pointer-events: none; }
.inverted .card-edge { border-color: rgba(191,118,153,.28); }
.inverted .card-meta { border-color: rgba(225,183,201,.1); background: rgba(0,0,0,.18); }
.inverted .rank-suit { color: color-mix(in srgb, var(--suit) 64%, #efbfd4); }
.inverted .card-name { color: #eadce2; }
.inverted .card-numbers span { color: #d9c9d0; background: rgba(255,255,255,.05); }
.inverted .card-art { filter: grayscale(.28) contrast(1.18) brightness(.62); border-color: rgba(226,186,205,.12); }
.inverted .card-art::after { background: linear-gradient(180deg, rgba(35,10,27,.1), rgba(12,4,11,.64)); }
.inverted .card-copy h3 { color: #f1e3e8; }
.inverted .card-copy p { color: rgba(239,222,229,.65); }
.inverted .suit-label { color: color-mix(in srgb, var(--suit) 58%, #eabbd0); }
.inverted .card-footer small { color: rgba(239,222,229,.42); }

.empire-card.face-down { display: block; padding: 8px; border-color: #9c7b46; background: linear-gradient(145deg, #191d2b, #10121b); }
.card-back-pattern { position: relative; display: grid; width: 100%; height: 100%; place-items: center; overflow: hidden; border: 2px solid #a98449; border-radius: 8px; color: #d7bd82; background: repeating-linear-gradient(45deg, rgba(203,169,94,.08) 0 6px, transparent 7px 13px), radial-gradient(circle, #27314b, #121520 65%); box-shadow: inset 0 0 0 4px #171a28, inset 0 0 0 5px rgba(215,189,130,.35); }
.card-back-pattern::before,.card-back-pattern::after { content: ''; position: absolute; width: 126px; height: 126px; border: 1px solid rgba(215,189,130,.38); transform: rotate(45deg); }
.card-back-pattern::after { width: 82px; height: 82px; transform: rotate(45deg); }
.card-back-pattern span { position: absolute; z-index: 1; top: 26px; font-size: 1.4rem; }
.card-back-pattern i { z-index: 1; width: 54px; height: 76px; border: 2px solid #d1b16d; border-radius: 50% 50% 42% 42%; background: linear-gradient(145deg, #e8d9bd, #9c855e); box-shadow: 0 0 18px rgba(219,184,108,.24); }
.card-back-pattern b { position: absolute; z-index: 2; font: 800 1.2rem/1 Georgia, serif; }

.empire-card.compact { width: 116px; min-width: 116px; height: 184px; grid-template-rows: auto 78px minmax(0, 1fr) auto; border-radius: 8px; }
.compact .card-meta { min-height: 33px; padding: 4px 6px 3px; }
.compact .rank-suit strong { font-size: .92rem; }
.compact .rank-suit span { font-size: .82rem; }
.compact .card-name { display: none; }
.compact .card-numbers span { min-width: 22px; padding: 2px; font-size: .43rem; }
.compact .card-numbers svg { display: none; }
.compact .card-copy { padding: 5px 7px 2px; }
.compact .suit-label { display: none; }
.compact .card-copy h3 { margin-bottom: 2px; font-size: .57rem; -webkit-line-clamp: 1; }
.compact .card-copy p { font-size: .43rem; line-height: 1.2; -webkit-line-clamp: 2; }
.compact .card-footer { padding: 3px 6px 5px; }
.compact .card-footer small { display: none; }
.compact .trump-seal { font-size: .37rem; }
.compact .inverted-seal { left: 4px; right: auto; font-size: 0; }
.compact .inverted-seal svg { width: 9px; }

@media (max-width: 720px) {
  .empire-card:not(.compact) { width: 150px; min-width: 150px; height: 238px; grid-template-rows: auto 105px minmax(0, 1fr) auto; }
  .card-copy p { -webkit-line-clamp: 2; }
}
</style>
