<script setup lang="ts">
import { Banknote, Church, FerrisWheel, FlaskConical, Landmark, ShieldCheck, Wine } from 'lucide-vue-next'
import type { EmpiresDomesticEconomyView } from '../../features/empires-endgame/types'

defineProps<{
  cities: Array<{ id: string, name: string, disabledReason?: string }>
  activeCityId: string
  con: number
  view: EmpiresDomesticEconomyView
}>()

const emit = defineEmits<{
  selectCity: [cityId: string]
  takeLoan: [cityId: string]
  repayLoan: [loanId: string]
  persecution: [cityId: string]
  startInsurance: [cityId: string]
  fairAction: [cityId: string, actionId: string]
  fairExchange: [cityId: string]
  preach: [cityId: string]
  assignRelic: [cityId: string, slotIndex: number, giftId: string]
  clearRelic: [cityId: string, slotIndex: number]
  visitTavern: [cityId: string]
  startAlchemy: [cityId: string, recipeId: string]
}>()

function selectCity(event: Event) {
  emit('selectCity', (event.target as HTMLSelectElement).value)
}

function assignRelic(cityId: string, slotIndex: number, event: Event) {
  const giftId = (event.target as HTMLSelectElement).value
  if (giftId) emit('assignRelic', cityId, slotIndex, giftId)
}

function number(value: number) {
  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)
}

function loanBalance(loan: EmpiresDomesticEconomyView['bank']['loans'][number]) {
  return loan.installments
    .filter(installment => installment.status === 'pending')
    .reduce((total, installment) => total + installment.amount, 0)
}
</script>

<template>
  <section class="domestic-economy" data-testid="domestic-economy-panel" aria-labelledby="domestic-economy-title">
    <header class="economy-heading">
      <div>
        <span>Внутренняя экономика · кон {{ con }}</span>
        <h3 id="domestic-economy-title"><Landmark :size="22" /> {{ view.selectedCityName }}</h3>
      </div>
      <label>
        <span>Выбранный город</span>
        <select :value="activeCityId" data-testid="economy-city" @change="selectCity">
          <option
            v-for="city in cities"
            :key="city.id"
            :value="city.id"
            :disabled="Boolean(city.disabledReason)"
          >{{ city.name }}{{ city.disabledReason ? ` — ${city.disabledReason}` : '' }}</option>
        </select>
      </label>
    </header>

    <div class="economy-grid">
      <article class="economy-card bank-card">
        <header><Banknote :size="18" /><h4>Банк</h4></header>
        <p>
          Доход при выдаче: <b>{{ number(view.bank.quote.incomeAtOrigination) }}</b>.
          Кредит: <b>{{ number(view.bank.quote.principal) }}</b>;
          выплата: <b>{{ number(view.bank.quote.installmentAmount) }}</b> × {{ view.bank.quote.termCons }};
          процент: <b>{{ number(view.bank.quote.interest) }}</b>.
        </p>
        <button
          type="button"
          data-testid="economy-take-loan"
          :disabled="Boolean(view.bank.quote.blockedReason)"
          :title="view.bank.quote.blockedReason || undefined"
          @click="emit('takeLoan', view.cityId)"
        >Взять кредит</button>
        <small v-if="view.bank.quote.blockedReason" class="reason">{{ view.bank.quote.blockedReason }}</small>

        <div v-if="view.bank.loans.length" class="obligations" aria-label="Банковские обязательства">
          <strong>Обязательства</strong>
          <div v-for="loan in view.bank.loans" :key="loan.id" :data-testid="`loan-${loan.id}`">
            <span><b>{{ loan.id }}</b> · {{ loan.status }} · остаток {{ number(loanBalance(loan)) }}</span>
            <small>
              Следующий платёж:
              {{ loan.installments.find(item => item.status === 'pending')?.dueCon ?? 'закрыт' }}
              · дефолт {{ loan.defaultedAtCon ?? 'нет' }}
            </small>
            <button
              v-if="loan.status === 'active' || loan.status === 'defaulted'"
              type="button"
              @click="emit('repayLoan', loan.id)"
            >Погасить полностью</button>
          </div>
        </div>
        <button
          class="danger"
          type="button"
          data-testid="economy-persecution"
          :disabled="Boolean(view.bank.persecutionBlockedReason)"
          :title="view.bank.persecutionBlockedReason || undefined"
          @click="emit('persecution', view.cityId)"
        >Начать гонения</button>
        <small v-if="view.bank.persecutionActive">Новые кредиты закрыты навсегда.</small>
      </article>

      <article class="economy-card insurance-card">
        <header><ShieldCheck :size="18" /><h4>Страховой банк</h4></header>
        <template v-if="view.insurance.contract">
          <p data-testid="insurance-contract">
            <b>{{ view.insurance.contract.status }}</b> · спокойных конов {{ view.insurance.contract.calmTurns }} ·
            активация {{ view.insurance.contract.activatedAtCon ?? 'ожидается' }} ·
            срок {{ view.insurance.contract.expiresAfterCon ?? '—' }}.
          </p>
          <small>
            Выплата сейчас: {{ number(view.insurance.projectedPayoutGold) }} ·
            выплачено {{ number(view.insurance.contract.payoutGold) }} ·
            источник {{ view.insurance.contract.payoutIncidentId ?? 'нет' }}.
          </small>
        </template>
        <p v-else>Контракт активируется после трёх спокойных конов и покрывает эпидемию, метеор, набег, ядерный удар или осаду.</p>
        <button
          type="button"
          data-testid="economy-start-insurance"
          :disabled="Boolean(view.insurance.startBlockedReason)"
          :title="view.insurance.startBlockedReason || undefined"
          @click="emit('startInsurance', view.cityId)"
        >Выбрать контракт</button>
        <small v-if="view.insurance.startBlockedReason" class="reason">{{ view.insurance.startBlockedReason }}</small>
      </article>

      <article class="economy-card fair-card">
        <header><FerrisWheel :size="18" /><h4>Ярмарка</h4></header>
        <button
          type="button"
          data-testid="fair-resource-exchange"
          :disabled="Boolean(view.fair.exchangeBlockedReason)"
          :title="view.fair.exchangeBlockedReason || undefined"
          @click="emit('fairExchange', view.cityId)"
        >Обменять 1000 дерева по курсу Ярмарки</button>
        <button
          v-for="action in view.fair.actions"
          :key="action.id"
          type="button"
          :data-testid="`fair-${action.id}`"
          :disabled="Boolean(action.blockedReason)"
          :title="action.blockedReason || undefined"
          @click="emit('fairAction', view.cityId, action.id)"
        >
          <span>{{ action.name }} · {{ number(action.goldCost) }} золота</span>
          <small>повтор с {{ action.availableAtCon }} · эффект до {{ action.activeUntilCon ?? '—' }}</small>
          <em v-if="action.blockedReason">{{ action.blockedReason }}</em>
        </button>
        <p v-if="view.fair.baronUnlockedAtCon !== null">
          Циганский барон появился в коне {{ view.fair.baronUnlockedAtCon }}; его ветка остаётся явно отложена.
        </p>
      </article>

      <article class="economy-card temple-card">
        <header><Church :size="18" /><h4>Храм</h4></header>
        <button
          type="button"
          data-testid="temple-preach"
          :disabled="Boolean(view.temple.preachBlockedReason)"
          :title="view.temple.preachBlockedReason || undefined"
          @click="emit('preach', view.cityId)"
        >Проповедь · десятина {{ number(view.temple.projectedTitheGold) }}</button>
        <small v-if="view.temple.preachBlockedReason" class="reason">{{ view.temple.preachBlockedReason }}</small>
        <div class="relic-slots" aria-label="Слоты реликвий Храма">
          <strong>Реликвии</strong>
          <label v-for="slot in view.temple.slots" :key="slot.id" :data-testid="slot.id">
            <span>Слот {{ slot.index + 1 }} · {{ slot.active ? 'активен' : 'хранение' }}</span>
            <template v-if="slot.giftId">
              <b>{{ slot.giftName }}</b>
              <button type="button" @click="emit('clearRelic', view.cityId, slot.index)">Снять</button>
            </template>
            <select v-else :disabled="!slot.active || !view.temple.unassignedRelics.length" @change="assignRelic(view.cityId, slot.index, $event)">
              <option value="">Выберите реликвию</option>
              <option v-for="relic in view.temple.unassignedRelics" :key="relic.id" :value="relic.id">{{ relic.name }}</option>
            </select>
          </label>
          <small v-if="!view.temple.slots.length">Постройте Храм, чтобы получить слоты.</small>
        </div>
      </article>

      <article class="economy-card tavern-card">
        <header><Wine :size="18" /><h4>Таверна</h4></header>
        <p>
          Пассивный армейский hook: вместимость найма
          <b>+{{ number(view.tavern.recruitmentCapacityBonus) }}</b>, максимум Боевого духа
          <b>+{{ number(view.tavern.moraleMaximumBonus) }}</b>.
        </p>
        <p>
          Прохождение <b>№{{ view.tavern.runOrdinal }}</b> ·
          <template v-if="view.tavern.spawned">появилась в коне {{ view.tavern.spawnedAtCon }}</template>
          <template v-else>пока скрыта</template>.
          <template v-if="view.tavern.spiritsActive"> Спиртное действует сейчас.</template>
          <template v-else-if="view.tavern.spiritsReadyAtCon"> Спиртное: коны {{ view.tavern.spiritsReadyAtCon }}–{{ view.tavern.spiritsExpiresAfterCon }}.</template>
          <template v-if="view.tavern.lastVisitedCon"> Последнее посещение: кон {{ view.tavern.lastVisitedCon }}.</template>
        </p>
        <button
          type="button"
          data-testid="economy-visit-tavern"
          :disabled="Boolean(view.tavern.blockedReason)"
          :title="view.tavern.blockedReason || undefined"
          @click="emit('visitTavern', view.cityId)"
        >Посетить Таверну</button>
        <small v-if="view.tavern.blockedReason" class="reason">{{ view.tavern.blockedReason }}</small>
        <small v-for="capability in view.tavern.deferredCapabilities" :key="capability.id" class="deferred">
          {{ capability.id }}: {{ capability.reason }}
        </small>
      </article>

      <article class="economy-card alchemy-card" data-testid="economy-alchemy">
        <header><FlaskConical :size="18" /><h4>Алхимия</h4></header>
        <p>
          Лабораторные фигуры идут к конструкции с четырёх сторон. Ускорение растёт
          арифметически; превышение порога создаст эпидемию у этого города.
        </p>
        <button
          v-for="recipe in view.alchemy.recipes"
          :key="recipe.id"
          type="button"
          :data-testid="`alchemy-recipe-${recipe.id}`"
          :disabled="Boolean(recipe.blockedReason)"
          :title="recipe.blockedReason || undefined"
          @click="emit('startAlchemy', view.cityId, recipe.id)"
        >
          <span>{{ recipe.name }} · {{ recipe.mode === 'assembly' ? 'Сбор' : 'Разбор' }}</span>
          <small>{{ recipe.family }}</small>
          <em v-if="recipe.blockedReason">{{ recipe.blockedReason }}</em>
        </button>
        <small v-if="view.alchemy.blockedReason && !view.alchemy.recipes.length" class="reason">{{ view.alchemy.blockedReason }}</small>
        <small>Взрывов в летописи: {{ view.alchemy.explosionCount }}.</small>
        <small v-if="view.alchemy.pendingMutantAftermathCount > 0" class="reason" data-testid="alchemy-mutant-warning">
          Мутанты: {{ view.alchemy.pendingMutantAftermathCount }} очаг; последствия в коне {{ view.alchemy.nextMutantAftermathCon }}.
        </small>
        <small v-for="capability in view.alchemy.deferredCapabilities" :key="capability.id" class="deferred">
          {{ capability.id }}: {{ capability.reason }}
        </small>
      </article>
    </div>
  </section>
</template>

<style scoped>
.domestic-economy { display: grid; gap: 14px; padding: 18px; color: #f5e7ca; }
.economy-heading { display: flex; flex-wrap: wrap; align-items: end; justify-content: space-between; gap: 12px; padding: 15px; border: 1px solid rgba(215, 167, 77, .35); background: rgba(42, 27, 15, .88); }
.economy-heading span { color: #bda77c; font: 700 .62rem/1.3 var(--font-mono, monospace); text-transform: uppercase; letter-spacing: .12em; }
.economy-heading h3 { display: flex; align-items: center; gap: 8px; margin: 4px 0 0; font: 700 1.15rem/1.2 var(--font-display, serif); }
.economy-heading label { display: grid; gap: 5px; min-width: min(100%, 280px); }
.economy-heading select, .relic-slots select { min-height: 38px; border: 1px solid rgba(215, 167, 77, .4); background: #21170f; color: inherit; padding: 7px 10px; }
.economy-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 320px), 1fr)); gap: 12px; align-items: start; }
.economy-card { display: grid; gap: 10px; padding: 14px; border: 1px solid rgba(215, 167, 77, .25); background: linear-gradient(145deg, rgba(50, 32, 17, .96), rgba(27, 20, 15, .96)); }
.economy-card > header { display: flex; align-items: center; gap: 8px; color: #f5c66f; }
.economy-card h4, .economy-card p { margin: 0; }
.economy-card p { color: rgba(245, 231, 202, .78); font-size: .76rem; line-height: 1.55; }
.economy-card button { min-height: 38px; border: 1px solid rgba(219, 170, 76, .45); background: rgba(113, 67, 25, .55); color: inherit; padding: 7px 10px; cursor: pointer; text-align: left; }
.economy-card button:focus-visible, select:focus-visible { outline: 2px solid #ffcf70; outline-offset: 2px; }
.economy-card button:disabled { cursor: not-allowed; opacity: .48; }
.economy-card button.danger { border-color: rgba(201, 77, 61, .5); background: rgba(105, 31, 25, .45); }
.fair-card > button { display: grid; gap: 3px; }
.fair-card button small, .fair-card button em { font-size: .62rem; opacity: .75; }
.obligations, .relic-slots { display: grid; gap: 7px; padding: 10px; border: 1px solid rgba(215, 167, 77, .18); background: rgba(0, 0, 0, .16); }
.obligations > div, .relic-slots label { display: grid; gap: 5px; padding-top: 7px; border-top: 1px solid rgba(255, 255, 255, .08); }
.relic-slots label > button { width: max-content; min-height: 30px; }
.reason { color: #e5a38c; }
.deferred { color: #bfae91; font-style: italic; }
@media (max-width: 700px) { .domestic-economy { padding: 9px; } .economy-heading { align-items: stretch; } }
</style>
