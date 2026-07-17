<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ArrowLeftRight, Handshake, ShipWheel } from 'lucide-vue-next'
import type { EmpiresExternalDiplomacyView } from '../../features/empires-endgame/types'

const props = defineProps<{
  cities: Array<{ id: string, name: string, disabledReason?: string }>
  resources: Array<{ id: string, name: string }>
  activeCityId: string
  con: number
  view: EmpiresExternalDiplomacyView
}>()

const emit = defineEmits<{
  selectCity: [cityId: string]
  accept: [offerId: string, cityId: string]
  decline: [offerId: string]
  transfer: [fromCityId: string, toCityId: string, resourceId: string, amount: number]
}>()

const transferToCityId = ref('')
const transferResourceId = ref('')
const transferAmount = ref(100)

watch(() => props.activeCityId, () => {
  if (transferToCityId.value === props.activeCityId) transferToCityId.value = ''
})

const destinationCities = computed(() => props.cities.filter(city => city.id !== props.activeCityId))
const canTransfer = computed(() => Boolean(
  props.view.enabled
  && props.activeCityId && transferToCityId.value && transferResourceId.value && transferAmount.value > 0,
))

function effectLabel(effect: unknown) {
  return JSON.stringify(effect)
}
</script>

<template>
  <section class="external-panel" data-testid="external-diplomacy-panel">
    <header>
      <div>
        <p class="eyebrow"><Handshake :size="15" /> Внешняя дипломатия</p>
        <h2>Контракты и торговые связи</h2>
        <p>Предложения сохраняются в кампании: перезагрузка не меняет пул и не отменяет принятое решение.</p>
      </div>
      <label>
        Город сделки
        <select
          :value="activeCityId"
          data-testid="external-city"
          @change="emit('selectCity', ($event.target as HTMLSelectElement).value)"
        >
          <option v-for="city in cities" :key="city.id" :value="city.id" :disabled="Boolean(city.disabledReason)">
            {{ city.name }}{{ city.disabledReason ? ` · ${city.disabledReason}` : '' }}
          </option>
        </select>
      </label>
    </header>

    <p v-if="!view.enabled" class="blocked" data-testid="external-disabled">
      Внешняя дипломатия и перевозки отключены в этой конфигурации.
    </p>

    <div class="actor-row">
      <article v-for="actor in view.actors" :key="actor.id" :data-testid="`external-actor-${actor.id}`">
        <b>{{ actor.name }}</b>
        <span>{{ actor.relationship }}</span>
      </article>
    </div>

    <div class="offers">
      <article v-for="offer in view.offers" :key="offer.id" class="offer" :data-testid="`external-offer-${offer.definitionId}`">
        <div>
          <small>{{ offer.actorName }} · {{ offer.relationship }} · до кона {{ offer.expiresAfterCon }}</small>
          <h3>{{ offer.name }}</h3>
          <p>{{ offer.description }}</p>
          <p class="exact">
            {{ offer.quote.direction === 'import' ? 'Получить' : 'Передать' }}
            {{ offer.quote.resourceAmount.toLocaleString('ru-RU') }} {{ offer.quote.resourceId }} ·
            {{ offer.quote.direction === 'import' ? 'заплатить' : 'получить' }}
            {{ offer.quote.adjustedGold.toLocaleString('ru-RU') }} gold ·
            пошлина +{{ offer.quote.tariffGold.toLocaleString('ru-RU') }} gold ·
            наука +{{ offer.quote.knowledgeBonus.toLocaleString('ru-RU') }}
          </p>
          <p v-if="offer.quote.blockedReason" class="blocked" data-testid="external-offer-denial">
            {{ offer.quote.blockedReason }}
          </p>
          <p class="decline-effects">
            Отказ: {{ offer.declineEffects.length ? offer.declineEffects.map(effectLabel).join('; ') : 'без дополнительных последствий' }}
          </p>
        </div>
        <div class="offer-actions">
          <button
            :data-testid="`external-accept-${offer.definitionId}`"
            type="button"
            :disabled="Boolean(offer.quote.blockedReason)"
            @click="emit('accept', offer.id, activeCityId)"
          >Принять</button>
          <button
            :data-testid="`external-decline-${offer.definitionId}`"
            class="secondary"
            type="button"
            @click="emit('decline', offer.id)"
          >Отказаться</button>
        </div>
      </article>
      <p v-if="view.offers.length === 0" class="empty">Активных предложений нет. Следующая проверка произойдёт по сохранённому ритму.</p>
    </div>

    <article class="transfer-card">
      <div>
        <p class="eyebrow"><ArrowLeftRight :size="15" /> Перевозка между городами</p>
        <p>
          Стоимость: {{ view.transfer.effectiveTimeCostDays }} дн.
          <span v-if="view.transfer.compassActive">· Компас ускоряет базовые {{ view.transfer.baseTimeCostDays }} дн.</span>
        </p>
      </div>
      <select v-model="transferToCityId" data-testid="external-transfer-city">
        <option value="">Город назначения</option>
        <option v-for="city in destinationCities" :key="city.id" :value="city.id" :disabled="Boolean(city.disabledReason)">{{ city.name }}</option>
      </select>
      <select v-model="transferResourceId" data-testid="external-transfer-resource">
        <option value="">Ресурс</option>
        <option v-for="resource in resources" :key="resource.id" :value="resource.id">{{ resource.name }}</option>
      </select>
      <input v-model.number="transferAmount" data-testid="external-transfer-amount" type="number" min="1">
      <button
        data-testid="external-transfer-submit"
        type="button"
        :disabled="!canTransfer"
        @click="emit('transfer', activeCityId, transferToCityId, transferResourceId, transferAmount)"
      >Перевезти</button>
    </article>

    <section class="review">
      <p class="eyebrow"><ShipWheel :size="15" /> Проверенные отсутствующие здания</p>
      <p v-for="item in view.reviewedAbsentBuildings" :key="item.id" :data-testid="`external-review-${item.id}`">
        <b>{{ item.name }}</b> — {{ item.reason }}
      </p>
    </section>

    <details>
      <summary>История решений ({{ view.history.length }})</summary>
      <p v-for="record in view.history" :key="record.offerId">
        {{ record.resolvedAtCon }} кон · {{ record.definitionId }} · {{ record.resolution }}
        <span v-if="record.cityId">· {{ record.cityId }} · gold {{ record.goldDelta >= 0 ? '+' : '' }}{{ record.goldDelta }}</span>
      </p>
    </details>
  </section>
</template>

<style scoped>
.external-panel { display: grid; gap: 18px; padding: 18px; color: #efe6cb; }
header, .transfer-card { display: flex; gap: 16px; align-items: end; justify-content: space-between; flex-wrap: wrap; }
h2, h3, p { margin: 0; }
.eyebrow { display: flex; gap: 7px; align-items: center; color: #d8ad63; text-transform: uppercase; letter-spacing: .08em; font-size: .78rem; }
label { display: grid; gap: 5px; }
select, input, button { border: 1px solid #705638; border-radius: 7px; background: #17130f; color: inherit; padding: 8px 10px; }
button { cursor: pointer; background: #6f4a22; }
button.secondary { background: transparent; }
button:disabled { opacity: .45; cursor: not-allowed; }
.actor-row { display: flex; gap: 10px; flex-wrap: wrap; }
.actor-row article { display: flex; gap: 8px; padding: 8px 10px; border: 1px solid #4b3b2b; border-radius: 8px; }
.actor-row span, small { color: #bbaa8e; }
.offers { display: grid; gap: 10px; }
.offer, .transfer-card, .review { border: 1px solid #4b3b2b; border-radius: 10px; padding: 14px; background: #1c1712; }
.offer { display: flex; gap: 15px; justify-content: space-between; }
.offer-actions { display: flex; gap: 8px; align-items: center; }
.exact { margin-top: 8px; color: #d9c59f; }
.blocked { margin-top: 7px; color: #ee9b82; }
.decline-effects { margin-top: 7px; color: #bbaa8e; font-size: .86rem; }
.transfer-card { align-items: center; justify-content: flex-start; }
.review { display: grid; gap: 8px; }
.empty { color: #bbaa8e; }
details p { padding: 5px 0; color: #c9b99d; }
@media (max-width: 760px) { .offer { flex-direction: column; } }
</style>
