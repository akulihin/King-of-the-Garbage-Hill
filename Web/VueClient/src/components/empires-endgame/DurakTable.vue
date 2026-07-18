<script setup lang="ts">
import { computed } from 'vue'
import {
  ArrowDownToLine,
  Brain,
  CheckCheck,
  CircleDotDashed,
  Crown,
  Layers3,
  ShieldCheck,
  Sparkles,
} from 'lucide-vue-next'
import EmpireCard from './EmpireCard.vue'

export interface DurakCardView {
  id: string
  title: string
  suit: string
  rank: string
  timeCost: number
  value: number
  description: string
  image?: string
  inverted: boolean
  upgrades: number
  trump?: boolean
}

export interface DurakPairView {
  attack: DurakCardView
  defense?: DurakCardView | null
}

const props = withDefaults(defineProps<{
  playerHand: DurakCardView[]
  mysticCards?: DurakCardView[]
  queenPulseIds?: string[]
  godHandCount: number
  table: DurakPairView[]
  deckCount: number
  discardCount: number
  trumpSuit: string
  trumpCard?: DurakCardView | null
  attacker: 'player' | 'god'
  stage: string
  message?: string
  godLine?: string
  legalCardIds?: string[]
  canTake?: boolean
  canFinish?: boolean
  disabled?: boolean
  canInspectDeck?: boolean
  deckInspectionReason?: string | null
  remainingDeckInspections?: number | null
}>(), {
  trumpCard: null,
  mysticCards: () => [],
  queenPulseIds: () => [],
  message: '',
  godLine: '',
  legalCardIds: () => [],
  canTake: false,
  canFinish: false,
  disabled: false,
  canInspectDeck: false,
  deckInspectionReason: null,
  remainingDeckInspections: null,
})

const emit = defineEmits<{
  play: [cardId: string]
  take: []
  finish: []
  inspectDeck: []
}>()

const godCardsShown = computed(() => Math.min(9, Math.max(0, props.godHandCount)))
const legalIds = computed(() => new Set(props.legalCardIds))
const actionLabel = computed(() => {
  if (props.stage === 'taking') {
    return props.attacker === 'player'
      ? 'Бог забирает карты. Подкиньте совпадающее достоинство или завершите атаку.'
      : 'Вы забираете карты. Бог может подкинуть совпадающие достоинства.'
  }
  if (props.attacker === 'player') {
    return props.stage === 'throwIn' ? 'Подкиньте карту того же достоинства или завершите атаку.' : 'Ваш ход. Выберите карту для атаки.'
  }
  if (props.stage === 'defense') return 'Бог Азарта атакует. Побейте открытую карту или заберите всё.'
  return 'Бог обдумывает следующий ход.'
})

function symbolFor(suit: string) {
  return ({ clubs: '♣', diamonds: '♦', hearts: '♥', spades: '♠' } as Record<string, string>)[suit] ?? suit
}
</script>

<template>
  <section class="durak-table" :class="{ disabled }">
    <header class="god-row">
      <div class="god-portrait" aria-hidden="true">
        <span class="god-mask"><i /><b /></span>
        <Sparkles :size="18" />
      </div>
      <div class="god-copy">
        <span>Ваш противник</span>
        <h2>Бог Азарта</h2>
        <p>{{ attacker === 'god' ? 'Ведёт игру' : 'Ждёт вашего решения' }}</p>
        <blockquote v-if="godLine" data-testid="god-dialogue-line" role="status">{{ godLine }}</blockquote>
      </div>
      <div class="god-hand" :aria-label="`У Бога ${godHandCount} карт`">
        <EmpireCard
          v-for="n in godCardsShown"
          :key="n"
          face-down
          compact
          :style="{ '--fan-index': n, '--fan-total': godCardsShown }"
        />
        <strong v-if="godHandCount > godCardsShown">+{{ godHandCount - godCardsShown }}</strong>
      </div>
    </header>

    <div class="table-felt">
      <div class="deck-zone">
        <div class="deck-stack" :class="{ empty: deckCount === 0 }" :title="deckCount ? 'Добор происходит автоматически после завершения атаки' : 'Колода закончилась'">
          <EmpireCard v-if="deckCount" face-down compact />
          <span class="deck-count"><Layers3 :size="13" />{{ deckCount }}</span>
          <small v-if="deckCount" class="deck-help">автодобор</small>
        </div>
        <div class="trump-card">
          <EmpireCard
            v-if="trumpCard"
            v-bind="trumpCard"
            compact
            trump
          />
          <div v-else class="trump-token">
            <span>{{ symbolFor(trumpSuit) }}</span>
            <small>козырь</small>
          </div>
        </div>
        <span class="discard-count"><CheckCheck :size="13" /> Бито: {{ discardCount }}</span>
        <button
          data-testid="inspect-deck-memory"
          type="button"
          class="memory-button"
          :disabled="disabled || !canInspectDeck"
          :title="deckInspectionReason || 'Показать карты в порядке следующего добора'"
          @click="emit('inspectDeck')"
        >
          <Brain :size="14" /> Память колоды
          <small v-if="remainingDeckInspections !== null">{{ remainingDeckInspections }}</small>
        </button>
      </div>

      <div class="battle-zone" aria-label="Карты на столе">
        <article v-for="(pair, index) in table" :key="pair.attack.id" class="battle-pair">
          <span class="pair-index">{{ index + 1 }}</span>
          <EmpireCard v-bind="pair.attack" compact />
          <div class="defense-slot" :class="{ empty: !pair.defense }">
            <EmpireCard v-if="pair.defense" v-bind="pair.defense" compact />
            <template v-else>
              <ArrowDownToLine v-if="stage === 'taking'" :size="22" />
              <ShieldCheck v-else :size="22" />
              <span>{{ stage === 'taking' ? 'Забирает' : 'Нужно побить' }}</span>
            </template>
          </div>
        </article>

        <div v-if="!table.length" class="empty-table">
          <CircleDotDashed :size="33" />
          <strong>Стол свободен</strong>
          <span>{{ attacker === 'player' ? 'Ваше величество, ходите.' : 'Бог тянется к своей руке.' }}</span>
        </div>
      </div>

      <aside class="turn-panel">
        <span class="turn-eyebrow">{{ attacker === 'player' ? 'Ход императора' : 'Ход Бога' }}</span>
        <strong>{{ message || actionLabel }}</strong>
        <div class="turn-actions">
          <button v-if="canTake" data-testid="durak-take" type="button" class="take" :disabled="disabled" @click="emit('take')">
            <ArrowDownToLine :size="16" /> Взять домой
          </button>
          <button v-if="canFinish" data-testid="durak-finish" type="button" class="finish" :disabled="disabled" @click="emit('finish')">
            <CheckCheck :size="16" /> {{ ['throwIn', 'taking'].includes(stage) ? 'Хватит подкидывать' : 'Бито' }}
          </button>
        </div>
      </aside>
    </div>

    <footer class="player-zone">
      <div v-if="mysticCards.length" class="mystic-zone" aria-label="Упорядоченный мистический ряд" data-testid="mystic-zone">
        <div class="player-heading">
          <div class="player-seal"><Sparkles :size="18" /></div>
          <div><span>Мистический ряд</span><strong>{{ mysticCards.length }} карт без масти и ранга</strong></div>
        </div>
        <div class="player-hand">
          <EmpireCard
            v-for="card in mysticCards"
            :key="card.id"
            v-bind="card"
            :badge="queenPulseIds.includes(card.id) ? 'Перевёрнута Пиковой Дамой' : ''"
          />
        </div>
      </div>
      <div class="player-heading">
        <div class="player-seal"><Crown :size="18" /></div>
        <div>
          <span>Рука императора</span>
          <strong>{{ playerHand.length }} карт · −{{ playerHand.reduce((sum, card) => sum + card.timeCost, 0) }} дней к имперской фазе</strong>
        </div>
      </div>
      <div class="player-hand">
        <EmpireCard
          v-for="card in playerHand"
          :key="card.id"
          v-bind="card"
          interactive
          :disabled="disabled || !legalIds.has(card.id)"
          @choose="emit('play', $event)"
        />
      </div>
      <p v-if="!playerHand.length" class="hand-empty">Ваша рука пуста.</p>
    </footer>
  </section>
</template>

<style scoped>
.durak-table { overflow: hidden; border: 1px solid rgba(219,193,137,.22); border-radius: 20px; color: #f0e5ce; background: #11140f; box-shadow: 0 26px 80px rgba(0,0,0,.36); }
.durak-table.disabled { pointer-events: none; filter: saturate(.7); }
.god-row { display: grid; min-height: 128px; grid-template-columns: auto minmax(150px, 1fr) minmax(260px, auto); align-items: center; gap: 14px; padding: 14px 20px; border-bottom: 1px solid rgba(219,193,137,.12); background: radial-gradient(circle at 70% -50%, rgba(70,104,142,.25), transparent 45%), linear-gradient(100deg, #171a1c, #101722); }
.god-portrait { position: relative; display: grid; width: 78px; height: 90px; place-items: center; overflow: hidden; border: 1px solid rgba(221,196,139,.28); border-radius: 48% 48% 38% 38%; color: #d9bd78; background: repeating-linear-gradient(90deg, rgba(80,123,155,.11) 0 4px, transparent 5px 9px), #171d2b; box-shadow: inset 0 -20px 24px rgba(0,0,0,.3); }
.god-portrait > svg { position: absolute; right: 8px; bottom: 7px; opacity: .55; }
.god-mask { position: relative; display: block; width: 42px; height: 53px; border: 2px solid #e8d8b6; border-radius: 48% 48% 44% 44%; background: linear-gradient(145deg,#f3e8d1,#a99776); box-shadow: 0 0 22px rgba(231,206,148,.25); }
.god-mask::before,.god-mask::after { content: ''; position: absolute; top: 18px; width: 8px; height: 4px; border-radius: 50%; background: #30384c; }
.god-mask::before { left: 8px; transform: rotate(13deg); }
.god-mask::after { right: 8px; transform: rotate(-13deg); }
.god-mask i { position: absolute; bottom: 10px; left: 50%; width: 19px; height: 8px; border-bottom: 2px solid #594c3c; border-radius: 50%; transform: translateX(-50%); }
.god-mask b { position: absolute; top: -9px; left: 50%; width: 6px; height: 6px; border-radius: 50%; background: #d4b360; box-shadow: 0 0 12px #e3c376; transform: translateX(-50%); }
.god-copy > span { color: #879fbb; font: 800 .6rem/1 var(--font-mono,monospace); letter-spacing: .13em; text-transform: uppercase; }
.god-copy h2 { margin: 5px 0 2px; font: 700 1.7rem/1 Georgia,serif; }
.god-copy p { margin: 0; color: rgba(240,229,206,.48); font-size: .7rem; }
.god-copy blockquote { max-width:520px; margin:9px 0 0; padding:8px 10px; border-left:2px solid #d0b262; border-radius:0 6px 6px 0; color:#e9dab8; background:rgba(210,178,98,.08); font:italic .68rem/1.4 Georgia,serif; }
.god-hand { display: flex; min-width: 260px; height: 92px; align-items: center; justify-content: flex-end; padding-right: 35px; }
.god-hand :deep(.empire-card) { width: 58px; min-width: 58px; height: 88px; margin-right: -37px; border-radius: 6px; transform: rotate(calc((var(--fan-index) - (var(--fan-total) + 1) / 2) * 2.6deg)); transform-origin: center 130%; }
.god-hand :deep(.card-back-pattern) { border-width: 1px; }
.god-hand :deep(.card-back-pattern span),.god-hand :deep(.card-back-pattern i),.god-hand :deep(.card-back-pattern b) { transform: scale(.55); }
.god-hand > strong { align-self: flex-start; margin: 8px 0 0 42px; padding: 3px 5px; border-radius: 4px; color: #dbc386; background: #202638; font: 800 .6rem/1 var(--font-mono,monospace); }

.table-felt { position: relative; display: grid; min-height: 430px; grid-template-columns: 150px minmax(0,1fr) 210px; gap: 16px; padding: 24px; background: radial-gradient(ellipse at center, rgba(142,166,111,.12), transparent 46%), repeating-linear-gradient(32deg, rgba(255,255,255,.012) 0 1px, transparent 2px 9px), linear-gradient(145deg,#1c3b2f,#11271f); box-shadow: inset 0 16px 35px rgba(0,0,0,.26), inset 0 -18px 35px rgba(0,0,0,.24); }
.table-felt::before { content:''; position:absolute; inset:14px; border:1px solid rgba(211,190,145,.15); border-radius:40% / 10%; pointer-events:none; }
.deck-zone { position: relative; z-index:1; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:9px; }
.deck-stack { position:relative; height:185px; }
.deck-stack::before,.deck-stack::after { content:none; }
.deck-stack :deep(.empire-card) { z-index:2; }
.deck-stack.empty { height:30px; }
.deck-stack.empty::before,.deck-stack.empty::after { display:none; }
.deck-count { position:absolute; z-index:4; right:-10px; bottom:4px; display:inline-flex; align-items:center; gap:3px; padding:4px 6px; border-radius:5px; color:#251d12; background:#d4b363; font:900 .62rem/1 var(--font-mono,monospace); }
.deck-help { position:absolute; z-index:4; left:50%; bottom:-19px; color:rgba(240,229,206,.48); font:700 .48rem/1 var(--font-mono,monospace); letter-spacing:.06em; text-transform:uppercase; transform:translateX(-50%); white-space:nowrap; }
.trump-card { position:absolute; right:-22px; bottom:46px; z-index:0; transform:rotate(88deg) scale(.74); opacity:.82; }
.trump-token { display:grid; width:72px; height:72px; place-items:center; border:1px solid #d1b161; border-radius:50%; color:#e6ca84; background:rgba(11,24,19,.82); }
.trump-token span { font:700 1.8rem/1 Georgia,serif; }
.trump-token small { margin-top:-16px; font:800 .5rem/1 var(--font-mono,monospace); text-transform:uppercase; }
.discard-count { display:inline-flex; align-items:center; gap:4px; color:rgba(240,229,206,.48); font:700 .58rem/1 var(--font-mono,monospace); }
.mystic-zone { display:grid; gap:10px; margin-bottom:16px; padding-bottom:16px; border-bottom:1px solid rgba(170,133,202,.2); }
.memory-button { display:inline-flex; min-height:32px; align-items:center; gap:5px; padding:0 9px; border:1px solid rgba(209,183,116,.28); border-radius:6px; color:#dfc77f; background:rgba(11,23,18,.72); cursor:pointer; font:800 .54rem/1 var(--font-mono,monospace); }
.memory-button small { display:grid; min-width:17px; height:17px; place-items:center; border-radius:50%; color:#211b12; background:#d2b567; }
.memory-button:disabled { opacity:.42; cursor:not-allowed; }
.memory-button:focus-visible { outline:2px solid #eed381; outline-offset:3px; }

.battle-zone { position:relative; z-index:1; display:flex; min-height:330px; flex-wrap:wrap; align-content:center; justify-content:center; gap:10px 4px; }
.battle-pair { position:relative; width:146px; height:238px; }
.battle-pair > :deep(.empire-card:first-of-type) { position:absolute; top:18px; left:0; transform:rotate(-7deg); }
.defense-slot { position:absolute; top:44px; right:0; z-index:2; display:grid; width:116px; height:184px; place-items:center; border-radius:8px; transform:rotate(7deg); }
.defense-slot.empty { border:1px dashed rgba(233,219,188,.28); color:rgba(233,219,188,.4); background:rgba(0,0,0,.09); }
.defense-slot.empty span { font:700 .52rem/1 var(--font-mono,monospace); }
.pair-index { position:absolute; top:0; left:50%; z-index:5; display:grid; width:20px; height:20px; place-items:center; border-radius:50%; color:#1f2a20; background:#d4b76f; font:900 .55rem/1 var(--font-mono,monospace); transform:translateX(-50%); }
.empty-table { display:grid; place-content:center; place-items:center; gap:5px; color:rgba(234,224,202,.42); text-align:center; }
.empty-table strong { color:rgba(234,224,202,.62); font:700 1.05rem/1 Georgia,serif; }
.empty-table span { font-size:.65rem; }

.turn-panel { position:relative; z-index:1; align-self:center; padding:14px; border:1px solid rgba(219,193,137,.17); border-radius:11px; background:rgba(8,16,13,.58); backdrop-filter:blur(8px); }
.turn-eyebrow { display:block; margin-bottom:6px; color:#d2b56e; font:900 .56rem/1 var(--font-mono,monospace); letter-spacing:.1em; text-transform:uppercase; }
.turn-panel > strong { display:block; color:#eee4ce; font-size:.7rem; line-height:1.45; }
.turn-actions { display:grid; gap:6px; margin-top:12px; }
.turn-actions button { display:inline-flex; min-height:34px; align-items:center; justify-content:center; gap:6px; border-radius:6px; cursor:pointer; font-size:.62rem; font-weight:900; }
.turn-actions .take { border:1px solid rgba(192,112,100,.55); color:#f0c9c3; background:rgba(126,57,48,.28); }
.turn-actions .finish { border:1px solid #c7aa68; color:#231c11; background:#c7aa68; }

.player-zone { padding:18px 20px 24px; border-top:1px solid rgba(219,193,137,.14); background:linear-gradient(100deg,#211e17,#171b16); }
.player-heading { display:flex; align-items:center; gap:9px; margin-bottom:14px; }
.player-seal { display:grid; width:36px; height:36px; place-items:center; border:1px solid #c9a85c; border-radius:50%; color:#e0c474; background:rgba(205,172,91,.08); }
.player-heading span { display:block; color:#cdb16b; font:900 .57rem/1 var(--font-mono,monospace); letter-spacing:.11em; text-transform:uppercase; }
.player-heading strong { display:block; margin-top:4px; color:rgba(240,229,206,.66); font-size:.66rem; }
.player-hand { display:flex; align-items:flex-end; gap:10px; overflow-x:auto; padding:13px 8px 16px; scrollbar-color:rgba(205,177,108,.36) transparent; scrollbar-width:thin; }
.hand-empty { margin:24px; color:rgba(240,229,206,.45); text-align:center; }

@media (max-width:1000px) {
  .table-felt { grid-template-columns:125px minmax(0,1fr); }
  .turn-panel { grid-column:1 / -1; width:min(480px,100%); justify-self:center; }
}
@media (max-width:720px) {
  .god-row { grid-template-columns:auto 1fr; min-height:104px; padding:10px; }
  .god-portrait { width:58px; height:70px; }
  .god-hand { grid-column:1/-1; min-width:0; height:60px; justify-content:center; padding-right:30px; }
  .god-hand :deep(.empire-card) { width:42px; min-width:42px; height:62px; margin-right:-28px; }
  .table-felt { min-height:540px; grid-template-columns:1fr; padding:16px 10px; }
  .deck-zone { min-height:120px; flex-direction:row; justify-content:flex-start; padding-left:20px; }
  .deck-stack { height:112px; transform:scale(.6); transform-origin:left center; }
  .trump-card { right:-80px; bottom:7px; }
  .discard-count { margin-left:auto; }
  .battle-zone { min-height:240px; justify-content:flex-start; overflow-x:auto; flex-wrap:nowrap; }
  .battle-pair { min-width:138px; transform:scale(.88); transform-origin:center; }
  .player-zone { padding:14px 10px 18px; }
  .player-hand { gap:7px; }
}
</style>
