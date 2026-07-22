<script setup lang="ts">
import { computed, ref } from 'vue'
import { Coins, ScrollText, Sparkles, Swords, Users, Wine } from 'lucide-vue-next'
import { applyTavernCommand, createTavernReplayState, resolveTavern } from '../../features/empires-endgame/tavern/engine'
import type {
  EmpiresMysticCardDefinition,
  EmpiresTavernMinigameSession,
  TavernCommand,
  TavernResult,
} from '../../features/empires-endgame/types'

const props = defineProps<{
  session: EmpiresTavernMinigameSession
  mysticCards: EmpiresMysticCardDefinition[]
  qaMode?: boolean
}>()

const emit = defineEmits<{ resolved: [result: TavernResult] }>()
const section = ref<'tables' | 'bar'>('tables')
const replay = ref(createTavernReplayState())

const goldRemaining = computed(() => props.session.plan.goldAvailable - replay.value.goldSpent)

function command(value: Omit<TavernCommand, 'turn'>) {
  const next = { ...value, turn: replay.value.turn + 1 } as TavernCommand
  replay.value = applyTavernCommand(props.session.plan, replay.value, next)
}

function finish() {
  command({ kind: 'finish' })
  const result = resolveTavern(
    props.session.plan,
    props.session.seed,
    replay.value.commandLog,
  )
  if (!result.error) emit('resolved', result)
}
</script>

<template>
  <section class="tavern" data-testid="tavern-minigame" aria-labelledby="tavern-title">
    <header class="tavern-heading">
      <div>
        <span>Бездонное болото · кон {{ session.plan.con }}</span>
        <h2 id="tavern-title">Таверна «У List'a»</h2>
        <p>Хозяин наливает, но сам не пьёт. Советуют заказывать рыбу.</p>
      </div>
      <strong><Coins :size="16" /> {{ goldRemaining.toLocaleString('ru-RU') }} золота</strong>
    </header>

    <nav aria-label="Секции Таверны">
      <button type="button" :aria-pressed="section === 'tables'" :class="{ active: section === 'tables' }" @click="section = 'tables'">
        <Users :size="16" /> Столы и наёмники
      </button>
      <button type="button" :aria-pressed="section === 'bar'" :class="{ active: section === 'bar' }" @click="section = 'bar'">
        <Wine :size="16" /> Барная стойка
      </button>
    </nav>

    <div v-if="section === 'tables'" class="section-grid">
      <article class="panel">
        <header><Swords :size="19" /><h3>Найм наёмников</h3></header>
        <button
          v-for="offer in session.plan.mercenaryOffers"
          :key="offer.id"
          type="button"
          :data-testid="`tavern-hire-${offer.id}`"
          :disabled="Boolean(replay.hiredOfferId) || goldRemaining < offer.goldCost"
          @click="command({ kind: 'hire', offerId: offer.id })"
        >
          <b>{{ offer.name }} · {{ offer.count }} отр.</b>
          <span>{{ offer.goldCost.toLocaleString('ru-RU') }} золота</span>
        </button>
        <small v-if="replay.hiredOfferId">Договор записан в журнал посещения.</small>
      </article>

      <article class="panel">
        <header><Wine :size="19" /><h3>Дать спиртного всем</h3></header>
        <p>Отношение наёмников изменится через один кон и будет действовать два кона: предложений станет больше, а сильная рота может оказаться дешевле.</p>
        <button
          type="button"
          data-testid="tavern-buy-drinks"
          :disabled="replay.drinksPurchased || goldRemaining < session.plan.drinks.goldCost"
          @click="command({ kind: 'buy-drinks' })"
        >
          Заказать · {{ session.plan.drinks.goldCost.toLocaleString('ru-RU') }} золота
        </button>
        <small v-if="replay.drinksPurchased">Новые предложения: коны {{ session.plan.drinks.readyAtCon }}–{{ session.plan.drinks.expiresAfterCon }}.</small>
      </article>
    </div>

    <div v-else class="section-grid">
      <article class="panel">
        <header><ScrollText :size="19" /><h3>Слухи хозяина</h3></header>
        <p>Сведения хуже донесений Антона де Лоряна и не обходят заработанные правила памяти колоды.</p>
        <button
          type="button"
          data-testid="tavern-buy-rumor"
          :disabled="replay.rumorPurchased || goldRemaining < session.plan.rumor.goldCost"
          @click="command({ kind: 'buy-rumor' })"
        >
          Узнать слух · {{ session.plan.rumor.goldCost.toLocaleString('ru-RU') }} золота
        </button>
        <blockquote v-if="replay.rumorPurchased" role="status">
          {{ session.plan.rumor.text }}
          <b v-if="session.plan.rumor.deckHint">
            Позиция {{ session.plan.rumor.deckHint.position }}: {{ session.plan.rumor.deckHint.rank }} · {{ session.plan.rumor.deckHint.suit }}.
          </b>
        </blockquote>
      </article>

      <article class="panel regulars">
        <header><Sparkles :size="19" /><h3>Загадочная тройка</h3></header>
        <div v-for="card in mysticCards.filter(item => item.id !== 'mystic-queen-of-spades')" :key="card.id">
          <b>{{ card.name }}</b>
          <small>{{ card.normal.description }}</small>
        </div>
        <p>После посещения доступных мистиков можно пригласить в Совет карт.</p>
      </article>

      <article v-if="session.plan.maria.present" class="panel maria" data-testid="tavern-maria">
        <header><Sparkles :size="19" /><h3>{{ session.plan.maria.title }}</h3></header>
        <p>{{ session.plan.maria.description }}</p>
        <button
          type="button"
          data-testid="tavern-play-maria"
          :disabled="replay.mariaPlayed"
          @click="command({ kind: 'play-maria' })"
        >Сыграть двое на двое</button>
        <small v-if="replay.mariaPlayed" role="status">
          {{ replay.mariaVictory ? 'Победа: пороховое наследие сохранено.' : 'Мария выиграла эту партию.' }}
        </small>
      </article>
      <article v-else class="panel"><h3>Место Марии пустует</h3><p>Сегодня за обычными посетителями наблюдает только хозяин.</p></article>
    </div>

    <footer>
      <span v-if="replay.error" role="alert">{{ replay.error }}</span>
      <button type="button" data-testid="tavern-finish" @click="finish">Завершить посещение</button>
      <button v-if="qaMode" type="button" data-testid="tavern-qa-resolve" @click="finish">QA: быстро выйти</button>
    </footer>
  </section>
</template>

<style scoped>
.tavern { display:grid; gap:14px; max-width:1180px; margin:0 auto; padding:20px; border:1px solid rgba(205,158,76,.38); border-radius:18px; color:#f3e7cf; background:radial-gradient(circle at 20% 0,rgba(102,120,72,.18),transparent 35%),linear-gradient(145deg,#241b13,#111712); box-shadow:0 24px 70px rgba(0,0,0,.38); }
.tavern-heading { display:flex; align-items:start; justify-content:space-between; gap:18px; padding:16px; border:1px solid rgba(205,158,76,.2); background:rgba(0,0,0,.18); }
.tavern-heading span,.tavern small { color:#bba98b; font-size:.66rem; }.tavern-heading h2 { margin:5px 0; font:700 1.8rem/1 Georgia,serif; }.tavern-heading p,.panel p { margin:0; color:rgba(243,231,207,.72); line-height:1.55; }.tavern-heading > strong { display:flex; align-items:center; gap:6px; white-space:nowrap; color:#efc86f; }
nav { display:flex; gap:8px; } button { min-height:40px; padding:8px 12px; border:1px solid rgba(213,170,91,.34); color:inherit; background:rgba(88,57,28,.5); cursor:pointer; } button:disabled { cursor:not-allowed; opacity:.48; } button:focus-visible { outline:2px solid #ffd17a; outline-offset:2px; } nav button { display:flex; align-items:center; gap:6px; } nav button.active { border-color:#e0b25d; background:rgba(139,91,37,.72); }
.section-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(min(100%,310px),1fr)); gap:12px; }.panel { display:grid; align-content:start; gap:10px; padding:15px; border:1px solid rgba(205,158,76,.2); background:rgba(28,22,16,.88); }.panel header { display:flex; align-items:center; gap:8px; color:#f0c46f; }.panel h3 { margin:0; }.panel > button { display:flex; justify-content:space-between; gap:10px; text-align:left; }.regulars > div { display:grid; gap:3px; padding:8px; border-left:2px solid #8e7650; background:rgba(255,255,255,.025); }.maria { border-color:rgba(122,143,183,.5); background:linear-gradient(145deg,rgba(39,46,61,.92),rgba(24,19,24,.94)); } blockquote { display:grid; gap:6px; margin:0; padding:10px; border-left:2px solid #d9ab58; background:rgba(255,255,255,.04); } footer { display:flex; justify-content:flex-end; gap:8px; } footer span { margin-right:auto; color:#e39d87; }
@media (max-width:650px) { .tavern { padding:10px; }.tavern-heading { flex-direction:column; } nav { display:grid; grid-template-columns:1fr; } footer { align-items:stretch; flex-direction:column; } }
</style>
