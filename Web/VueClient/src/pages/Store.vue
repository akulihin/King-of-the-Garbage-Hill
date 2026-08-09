<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  CircleAlert,
  Info,
  LoaderCircle,
  RotateCcw,
  Search,
  ShoppingBag,
  Sparkles,
  WalletCards,
} from 'lucide-vue-next'
import { currentLocale } from 'src/i18n'
import type { StoreCharacter } from 'src/services/signalr'
import { useGameStore } from 'src/store/game'

const store = useGameStore()
const searchQuery = ref('')
const selectedTier = ref<number | 'all'>('all')
const adjustedOnly = ref(false)
const confirmResetAll = ref(false)

const state = computed(() => store.storeState)
const characters = computed(() => state.value?.characters ?? [])
const availableTiers = computed(() =>
  [...new Set(characters.value.map(character => character.tier))]
    .filter(tier => tier >= 0)
    .sort((a, b) => a - b),
)
const filteredCharacters = computed(() => {
  const query = searchQuery.value.trim().toLocaleLowerCase()
  return characters.value
    .filter(character => selectedTier.value === 'all' || character.tier === selectedTier.value)
    .filter(character => !adjustedOnly.value || character.changes > 0)
    .filter(character => !query || character.name.toLocaleLowerCase().includes(query))
    .sort((a, b) => a.tier - b.tier || a.name.localeCompare(b.name, 'ru'))
})

onMounted(() => {
  if (store.isAuthenticated) void store.requestStore()
})

function t(english: string, russian: string): string {
  return currentLocale.value === 'ru' ? russian : english
}

function costFor(character: StoreCharacter, amount: number): number {
  return Math.abs(amount) === 10 ? character.costTen : character.costOne
}

function canAdjust(character: StoreCharacter, amount: number): boolean {
  if (!state.value || store.storeAction) return false
  const target = Math.round((character.multiplier + amount / 100) * 100) / 100
  return target >= state.value.minMultiplier
    && target <= state.value.maxMultiplier
    && state.value.zbsPoints >= costFor(character, amount)
}

function isCharacterBusy(character: StoreCharacter): boolean {
  return store.storeAction?.endsWith(`:${character.name}`) ?? false
}

function multiplierPercent(character: StoreCharacter): number {
  return Math.round(character.multiplier * 100)
}

function multiplierDelta(character: StoreCharacter): string {
  const delta = multiplierPercent(character) - 100
  return `${delta >= 0 ? '+' : ''}${delta}%`
}

function meterWidth(character: StoreCharacter): number {
  if (!state.value) return 0
  const range = state.value.maxMultiplier - state.value.minMultiplier
  if (range <= 0) return 0
  return Math.max(0, Math.min(100,
    ((character.multiplier - state.value.minMultiplier) / range) * 100,
  ))
}

async function adjust(character: StoreCharacter, amount: number): Promise<void> {
  confirmResetAll.value = false
  await store.adjustStoreCharacter(character.name, amount)
}

async function resetCharacter(character: StoreCharacter): Promise<void> {
  confirmResetAll.value = false
  await store.resetStoreCharacter(character.name)
}

async function resetAll(): Promise<void> {
  await store.resetStoreAllCharacters()
  confirmResetAll.value = false
}

function clearFilters(): void {
  searchQuery.value = ''
  selectedTier.value = 'all'
  adjustedOnly.value = false
}
</script>

<template>
  <div class="store-page">
    <RouterLink class="back-link" to="/games">
      <ArrowLeft :size="16" aria-hidden="true" />
      {{ t('Back to Lobby', 'Назад в лобби') }}
    </RouterLink>

    <section class="store-center" aria-labelledby="store-title">
      <header class="store-hero">
        <div class="merchant-aura" aria-hidden="true" />
        <div class="hero-icon" aria-hidden="true">
          <ShoppingBag :size="32" />
          <Sparkles class="hero-spark" :size="15" />
        </div>
        <div class="hero-copy">
          <div class="eyebrow">{{ t('The Merchant', 'Торговец') }}</div>
          <h1 id="store-title">{{ t('Character Store', 'Магазин персонажей') }}</h1>
          <p>
            {{ t(
              'Spend ZBS to tune the roll weight of characters you have already played.',
              'Тратьте ZBS, чтобы настроить вес выпадения уже сыгранных персонажей.',
            ) }}
          </p>
        </div>
        <div class="merchant-quote">
          <span>“WELCOME! Straaanger...”</span>
          <small>{{ t('Every adjustment changes the price of the next one.', 'Каждое изменение повышает цену следующего.') }}</small>
        </div>
      </header>

      <div v-if="state" class="store-stats" aria-label="Store account summary">
        <div class="overview-stat balance-stat">
          <span class="overview-icon"><img :src="'/art/emojis/zbs.png'" alt=""></span>
          <span class="overview-copy">
            <small>{{ t('Available balance', 'Доступный баланс') }}</small>
            <strong>{{ state.zbsPoints }}</strong>
            <span>ZBS Points</span>
          </span>
        </div>
        <div class="overview-stat">
          <span class="overview-icon"><WalletCards :size="21" aria-hidden="true" /></span>
          <span class="overview-copy">
            <small>{{ t('Refundable', 'Можно вернуть') }}</small>
            <strong>{{ state.totalInvestedZbs }}</strong>
            <span>{{ t('invested ZBS', 'вложено ZBS') }}</span>
          </span>
        </div>
        <div class="overview-stat">
          <span class="overview-icon"><ShoppingBag :size="21" aria-hidden="true" /></span>
          <span class="overview-copy">
            <small>{{ t('Discovered', 'Открыто') }}</small>
            <strong>{{ state.characters.length }}</strong>
            <span>{{ t('store characters', 'персонажей в магазине') }}</span>
          </span>
        </div>
      </div>

      <div v-if="state" class="store-explainer">
        <Info :size="18" aria-hidden="true" />
        <p>
          <strong>{{ t('This changes roll weight, not an exact probability.', 'Это вес выпадения, а не точная вероятность.') }}</strong>
          {{ t(
            `A 1-point change starts at ${state.basePrice} ZBS. Character roll weights can be changed only here. Pity, tier rules, and the other characters in a game still affect the final roll.`,
            `Изменение на 1 пункт начинается с ${state.basePrice} ZBS. Вес персонажей меняется только здесь. На итог всё ещё влияют pity, тир и остальные персонажи в игре.`,
          ) }}
        </p>
      </div>

      <div class="store-toolbar">
        <label class="search-field">
          <span class="sr-only">{{ t('Search characters', 'Поиск персонажей') }}</span>
          <Search :size="17" aria-hidden="true" />
          <input
            v-model="searchQuery"
            type="search"
            :placeholder="t('Find a character…', 'Найти персонажа…')"
          >
        </label>
        <div class="tier-tabs" role="group" :aria-label="t('Character tier', 'Тир персонажа')">
          <button type="button" :class="{ active: selectedTier === 'all' }" @click="selectedTier = 'all'">
            {{ t('All tiers', 'Все тиры') }}
          </button>
          <button
            v-for="tier in availableTiers"
            :key="tier"
            type="button"
            :class="{ active: selectedTier === tier }"
            @click="selectedTier = tier"
          >
            {{ tier === 0 ? 'PRO' : `T${tier}` }}
          </button>
        </div>
        <label class="adjusted-toggle">
          <input v-model="adjustedOnly" type="checkbox">
          <span>{{ t('Adjusted only', 'Только изменённые') }}</span>
        </label>
      </div>

      <div v-if="store.storeError" class="store-error" role="alert">
        <CircleAlert :size="20" aria-hidden="true" />
        <span>
          <strong>{{ t('The merchant could not finish that transaction', 'Торговец не смог завершить операцию') }}</strong>
          <small>{{ store.storeError }}</small>
        </span>
        <button v-if="!state" class="btn btn-primary" type="button" @click="store.requestStore()">
          {{ t('Try again', 'Повторить') }}
        </button>
      </div>

      <div v-if="store.isStoreLoading && !state" class="store-loading" role="status">
        <LoaderCircle :size="28" aria-hidden="true" />
        <strong>{{ t('The merchant is opening the shutters…', 'Торговец открывает ставни…') }}</strong>
        <span>{{ t('Counting ZBS and preparing your characters.', 'Считаем ZBS и готовим персонажей.') }}</span>
      </div>

      <div v-else-if="state && state.characters.length === 0" class="store-empty">
        <ShoppingBag :size="35" aria-hidden="true" />
        <strong>{{ t('The store is still closed', 'Магазин пока закрыт') }}</strong>
        <span>{{ t('Finish a game to discover your first character.', 'Завершите игру, чтобы открыть первого персонажа.') }}</span>
        <RouterLink class="btn btn-primary" to="/games">{{ t('Go to Lobby', 'В лобби') }}</RouterLink>
      </div>

      <div v-else-if="state && filteredCharacters.length === 0" class="store-empty">
        <Search :size="32" aria-hidden="true" />
        <strong>{{ t('No characters match these filters', 'По этим фильтрам ничего нет') }}</strong>
        <button class="btn btn-ghost" type="button" @click="clearFilters">
          {{ t('Clear filters', 'Сбросить фильтры') }}
        </button>
      </div>

      <div v-else-if="state" class="character-grid" role="list" :aria-busy="Boolean(store.storeAction)">
        <article
          v-for="character in filteredCharacters"
          :key="character.name"
          class="character-card"
          :class="[character.tier >= 0 ? `tier-${character.tier}` : '', { adjusted: character.changes > 0, busy: isCharacterBusy(character) }]"
          role="listitem"
        >
          <div class="card-accent" aria-hidden="true" />
          <header class="character-header">
            <div class="avatar-wrap">
              <img :src="character.avatar" :alt="character.name">
              <span v-if="character.tier >= 0" class="tier-badge">
                {{ character.tier === 0 ? 'PRO' : `T${character.tier}` }}
              </span>
            </div>
            <div class="character-heading">
              <span>{{ t('Roll weight', 'Вес выпадения') }}</span>
              <h2>{{ character.name }}</h2>
              <div class="weight-value" :class="{ positive: character.multiplier > 1, negative: character.multiplier < 1 }">
                <strong>{{ multiplierPercent(character) }}%</strong>
                <small>{{ multiplierDelta(character) }}</small>
              </div>
            </div>
          </header>

          <div class="weight-meter">
            <div class="meter-labels">
              <span>{{ Math.round(state.minMultiplier * 100) }}%</span>
              <span>{{ t('Base 100%', 'База 100%') }}</span>
              <span>{{ Math.round(state.maxMultiplier * 100) }}%</span>
            </div>
            <div class="meter-track" aria-hidden="true">
              <span class="meter-base" />
              <span class="meter-fill" :style="{ width: `${meterWidth(character)}%` }" />
              <i :style="{ left: `${meterWidth(character)}%` }" />
            </div>
          </div>

          <div class="price-summary">
            <span>
              <small>{{ t('Next step', 'Следующий шаг') }}</small>
              <strong><img :src="'/art/emojis/zbs.png'" alt="ZBS"> {{ character.costOne }}</strong>
            </span>
            <span>
              <small>{{ t('Ten steps', 'Десять шагов') }}</small>
              <strong><img :src="'/art/emojis/zbs.png'" alt="ZBS"> {{ character.costTen }}</strong>
            </span>
            <span>
              <small>{{ t('Invested', 'Вложено') }}</small>
              <strong>{{ character.refundZbs }}</strong>
            </span>
          </div>

          <div class="adjustment-actions">
            <button
              v-for="amount in [-10, -1, 1, 10]"
              :key="amount"
              class="adjust-button"
              :class="amount < 0 ? 'decrease' : 'increase'"
              type="button"
              :disabled="!canAdjust(character, amount)"
              :aria-label="t(
                `${amount > 0 ? 'Increase' : 'Decrease'} ${character.name} by ${Math.abs(amount)} percent for ${costFor(character, amount)} ZBS`,
                `${amount > 0 ? 'Повысить' : 'Понизить'} вес ${character.name} на ${Math.abs(amount)}% за ${costFor(character, amount)} ZBS`,
              )"
              @click="adjust(character, amount)"
            >
              <component :is="amount < 0 ? ArrowDown : ArrowUp" :size="14" aria-hidden="true" />
              <strong>{{ amount > 0 ? '+' : '' }}{{ amount }}%</strong>
              <small>{{ costFor(character, amount) }} ZBS</small>
            </button>
          </div>

          <footer class="character-footer">
            <span>
              {{ t(`${character.changes} purchased steps`, `Куплено шагов: ${character.changes}`) }}
            </span>
            <button
              class="reset-character"
              type="button"
              :disabled="character.changes === 0 || Boolean(store.storeAction)"
              @click="resetCharacter(character)"
            >
              <LoaderCircle v-if="store.storeAction === `reset:${character.name}`" :size="13" aria-hidden="true" />
              <RotateCcw v-else :size="13" aria-hidden="true" />
              {{ t(`Refund ${character.refundZbs}`, `Вернуть ${character.refundZbs}`) }}
            </button>
          </footer>

          <div v-if="isCharacterBusy(character)" class="card-busy" role="status">
            <LoaderCircle :size="24" aria-hidden="true" />
            <span>{{ t('Saving transaction…', 'Сохраняем операцию…') }}</span>
          </div>
        </article>
      </div>

      <section v-if="state && state.totalInvestedZbs > 0" class="refund-all" aria-labelledby="refund-all-title">
        <div>
          <span class="section-kicker">{{ t('Fresh start', 'Начать заново') }}</span>
          <h2 id="refund-all-title">{{ t('Reset every character', 'Сбросить всех персонажей') }}</h2>
          <p>{{ t(
            `Refund every purchased adjustment for ${state.totalInvestedZbs} ZBS.`,
            `Вернуть ${state.totalInvestedZbs} ZBS за все купленные изменения.`,
          ) }}</p>
        </div>
        <div v-if="!confirmResetAll" class="refund-actions">
          <button class="btn btn-ghost" type="button" :disabled="Boolean(store.storeAction)" @click="confirmResetAll = true">
            <RotateCcw :size="15" aria-hidden="true" /> {{ t('Reset all', 'Сбросить всё') }}
          </button>
        </div>
        <div v-else class="refund-confirm" role="group" :aria-label="t('Confirm full refund', 'Подтвердить полный возврат')">
          <strong>{{ t('Refund all changes?', 'Вернуть все изменения?') }}</strong>
          <button class="btn btn-primary" type="button" :disabled="Boolean(store.storeAction)" @click="resetAll">
            <LoaderCircle v-if="store.storeAction === 'reset:all'" :size="15" aria-hidden="true" />
            {{ t(`Yes, refund ${state.totalInvestedZbs} ZBS`, `Да, вернуть ${state.totalInvestedZbs} ZBS`) }}
          </button>
          <button class="btn btn-ghost" type="button" :disabled="Boolean(store.storeAction)" @click="confirmResetAll = false">
            {{ t('Cancel', 'Отмена') }}
          </button>
        </div>
      </section>
    </section>
  </div>
</template>

<style scoped>
.store-page { width: 100%; }
.back-link { display: inline-flex; align-items: center; gap: 6px; min-height: 36px; margin-bottom: 10px; padding: 4px 9px; color: var(--text-muted); border-radius: 7px; text-decoration: none; font-size: 11px; font-weight: 750; }
.back-link:hover { color: var(--text-primary); background: rgba(255, 255, 255, 0.04); }
.store-center { --tier-0: #ef7dff; --tier-1: #83d7a3; --tier-2: #73b8ff; --tier-3: #c18cff; --tier-4: #f1c35e; width: 100%; max-width: 1180px; margin: 0 auto; }
.store-hero { position: relative; min-height: 184px; display: grid; grid-template-columns: auto minmax(0, 1fr) minmax(190px, 300px); gap: 22px; align-items: center; overflow: hidden; padding: 28px 32px; border: 1px solid rgba(184, 135, 255, .2); border-radius: 20px; background: radial-gradient(circle at 85% 30%, rgba(168, 106, 255, .15), transparent 32%), linear-gradient(135deg, rgba(42, 31, 57, .94), rgba(21, 20, 26, .97)); box-shadow: 0 18px 55px rgba(0, 0, 0, .24), inset 0 1px 0 rgba(255, 255, 255, .06); }
.merchant-aura { position: absolute; width: 330px; height: 330px; top: -210px; right: 3%; border: 1px solid rgba(216, 186, 255, .12); border-radius: 50%; box-shadow: 0 0 90px rgba(157, 96, 255, .13); }
.hero-icon { position: relative; width: 74px; height: 74px; display: grid; place-items: center; color: #e0c8ff; border: 1px solid rgba(206, 169, 255, .34); border-radius: 21px; background: linear-gradient(145deg, rgba(197, 151, 255, .18), rgba(90, 55, 126, .12)); box-shadow: 0 0 32px rgba(168, 105, 255, .15); }
.hero-spark { position: absolute; top: 10px; right: 9px; color: var(--accent-gold); }
.hero-copy { position: relative; min-width: 0; }
.eyebrow, .section-kicker { color: #cba6ff; font-size: 9px; font-weight: 900; letter-spacing: 1.8px; text-transform: uppercase; }
.hero-copy h1 { margin: 5px 0 8px; color: var(--text-primary); font: 900 clamp(25px, 3vw, 39px)/1.05 var(--font-display); letter-spacing: -.7px; }
.hero-copy p { max-width: 610px; margin: 0; color: var(--text-muted); font-size: 11px; line-height: 1.65; }
.merchant-quote { position: relative; display: flex; flex-direction: column; gap: 7px; padding: 15px 17px; border-left: 2px solid rgba(216, 185, 255, .4); color: var(--text-secondary); }
.merchant-quote span { font: italic 800 15px/1.3 var(--font-display); }
.merchant-quote small { color: var(--text-dim); font-size: 9px; line-height: 1.5; }
.store-stats { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; margin: 12px 0; }
.overview-stat { min-width: 0; display: flex; align-items: center; gap: 12px; padding: 14px 16px; border: 1px solid var(--glass-border); border-radius: 14px; background: rgba(255, 255, 255, .025); }
.overview-icon { width: 40px; height: 40px; display: grid; flex: 0 0 auto; place-items: center; color: #c9a6f8; border: 1px solid rgba(190, 145, 249, .18); border-radius: 12px; background: rgba(174, 118, 241, .08); }
.overview-icon img { width: 30px; height: 30px; object-fit: contain; }
.overview-copy { min-width: 0; display: grid; grid-template-columns: auto 1fr; gap: 2px 8px; align-items: baseline; }
.overview-copy small { grid-column: 1 / -1; color: var(--text-dim); font-size: 8px; font-weight: 850; letter-spacing: .6px; text-transform: uppercase; }
.overview-copy strong { color: var(--text-primary); font: 900 21px/1 var(--font-mono); }
.overview-copy span { overflow: hidden; color: var(--text-muted); font-size: 9px; text-overflow: ellipsis; white-space: nowrap; }
.store-explainer { display: flex; gap: 11px; align-items: flex-start; margin-bottom: 12px; padding: 12px 15px; color: #c8a8ef; border: 1px solid rgba(174, 123, 238, .16); border-radius: 12px; background: rgba(151, 96, 217, .055); }
.store-explainer svg { flex: 0 0 auto; margin-top: 1px; }
.store-explainer p { margin: 0; color: var(--text-dim); font-size: 9px; line-height: 1.6; }
.store-explainer strong { display: block; margin-bottom: 2px; color: var(--text-secondary); font-size: 10px; }
.store-toolbar { display: grid; grid-template-columns: minmax(220px, 1fr) auto auto; gap: 10px; align-items: center; margin: 16px 0 13px; }
.search-field { min-height: 42px; display: flex; align-items: center; gap: 9px; padding: 0 12px; color: var(--text-dim); border: 1px solid var(--glass-border); border-radius: 11px; background: var(--bg-inset); }
.search-field:focus-within { color: #c9a6f8; border-color: rgba(189, 143, 248, .42); box-shadow: 0 0 0 3px rgba(174, 118, 241, .07); }
.search-field input { width: 100%; color: var(--text-primary); border: 0; outline: 0; background: transparent; font: 700 10px/1 var(--font-body); }
.tier-tabs { display: flex; gap: 3px; padding: 3px; border: 1px solid var(--glass-border); border-radius: 11px; background: var(--bg-inset); }
.tier-tabs button { min-height: 34px; padding: 0 11px; color: var(--text-dim); border: 0; border-radius: 8px; background: transparent; font-size: 9px; font-weight: 850; cursor: pointer; }
.tier-tabs button:hover { color: var(--text-secondary); }
.tier-tabs button.active { color: #e6d5fa; background: rgba(176, 126, 237, .16); box-shadow: inset 0 0 0 1px rgba(190, 150, 239, .14); }
.adjusted-toggle { min-height: 42px; display: flex; align-items: center; gap: 8px; padding: 0 12px; color: var(--text-muted); border: 1px solid var(--glass-border); border-radius: 11px; background: var(--bg-inset); font-size: 9px; font-weight: 800; cursor: pointer; }
.adjusted-toggle input { accent-color: #b87cf4; }
.character-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 12px; }
.character-card { --tier-color: var(--tier-1); position: relative; min-width: 0; overflow: hidden; border: 1px solid var(--glass-border); border-radius: 16px; background: linear-gradient(155deg, color-mix(in srgb, var(--tier-color) 4%, var(--bg-panel)), var(--bg-panel)); box-shadow: 0 10px 30px rgba(0, 0, 0, .15); }
.character-card.tier-0 { --tier-color: var(--tier-0); }
.character-card.tier-2 { --tier-color: var(--tier-2); }
.character-card.tier-3 { --tier-color: var(--tier-3); }
.character-card.tier-4 { --tier-color: var(--tier-4); }
.character-card.adjusted { border-color: color-mix(in srgb, var(--tier-color) 28%, var(--glass-border)); }
.card-accent { height: 3px; background: linear-gradient(90deg, transparent, var(--tier-color), transparent); opacity: .7; }
.character-header { display: flex; gap: 13px; align-items: center; padding: 14px 14px 11px; }
.avatar-wrap { position: relative; width: 68px; height: 68px; flex: 0 0 auto; }
.avatar-wrap img { width: 100%; height: 100%; object-fit: cover; border: 1px solid color-mix(in srgb, var(--tier-color) 42%, transparent); border-radius: 15px; background: var(--bg-inset); box-shadow: 0 6px 18px rgba(0, 0, 0, .22); }
.tier-badge { position: absolute; right: -4px; bottom: -4px; min-width: 25px; height: 20px; display: grid; place-items: center; color: #16151b; border: 2px solid var(--bg-panel); border-radius: 7px; background: var(--tier-color); font: 950 8px/1 var(--font-mono); }
.character-heading { min-width: 0; flex: 1; }
.character-heading > span { color: var(--text-dim); font-size: 8px; font-weight: 850; letter-spacing: .6px; text-transform: uppercase; }
.character-heading h2 { overflow: hidden; margin: 2px 0 7px; color: var(--text-primary); font: 850 15px/1.2 var(--font-display); text-overflow: ellipsis; white-space: nowrap; }
.weight-value { display: flex; align-items: baseline; gap: 7px; color: var(--text-secondary); }
.weight-value strong { font: 950 20px/1 var(--font-mono); }
.weight-value small { color: var(--text-dim); font: 850 9px/1 var(--font-mono); }
.weight-value.positive strong, .weight-value.positive small { color: var(--accent-green); }
.weight-value.negative strong, .weight-value.negative small { color: var(--accent-red); }
.weight-meter { padding: 0 14px 12px; }
.meter-labels { display: flex; justify-content: space-between; margin-bottom: 5px; color: var(--text-dim); font-size: 7px; font-weight: 750; }
.meter-track { position: relative; height: 6px; border-radius: 6px; background: var(--bg-inset); }
.meter-fill { position: absolute; inset: 0 auto 0 0; border-radius: inherit; background: linear-gradient(90deg, #e07878, var(--tier-color)); transition: width .35s ease; }
.meter-base { position: absolute; z-index: 2; top: -2px; bottom: -2px; left: 33.333%; width: 1px; background: rgba(255, 255, 255, .42); }
.meter-track i { position: absolute; z-index: 3; top: 50%; width: 10px; height: 10px; border: 2px solid var(--bg-panel); border-radius: 50%; background: var(--tier-color); box-shadow: 0 0 8px color-mix(in srgb, var(--tier-color) 45%, transparent); transform: translate(-50%, -50%); transition: left .35s ease; }
.price-summary { display: grid; grid-template-columns: repeat(3, 1fr); margin: 0 14px 12px; overflow: hidden; border: 1px solid var(--glass-border); border-radius: 10px; background: rgba(0, 0, 0, .12); }
.price-summary > span { min-width: 0; display: flex; flex-direction: column; gap: 3px; padding: 8px 7px; border-right: 1px solid var(--glass-border); text-align: center; }
.price-summary > span:last-child { border-right: 0; }
.price-summary small { color: var(--text-dim); font-size: 7px; font-weight: 800; text-transform: uppercase; }
.price-summary strong { display: flex; justify-content: center; align-items: center; gap: 3px; color: var(--text-secondary); font: 850 9px/1 var(--font-mono); }
.price-summary img { width: 14px; height: 14px; object-fit: contain; }
.adjustment-actions { display: grid; grid-template-columns: repeat(4, 1fr); gap: 5px; padding: 0 14px 13px; }
.adjust-button { min-width: 0; min-height: 54px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px; color: var(--text-muted); border: 1px solid var(--glass-border); border-radius: 9px; background: rgba(255, 255, 255, .025); cursor: pointer; }
.adjust-button svg { opacity: .7; }
.adjust-button strong { font: 850 10px/1 var(--font-mono); }
.adjust-button small { overflow: hidden; max-width: 100%; color: var(--text-dim); font-size: 7px; text-overflow: ellipsis; white-space: nowrap; }
.adjust-button.decrease:hover:not(:disabled) { color: #f1a1a1; border-color: rgba(235, 114, 114, .28); background: rgba(220, 83, 83, .08); }
.adjust-button.increase:hover:not(:disabled) { color: #9ce1af; border-color: rgba(92, 198, 122, .28); background: rgba(65, 174, 96, .08); }
.adjust-button:disabled { opacity: .34; cursor: not-allowed; }
.character-footer { min-height: 39px; display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 7px 14px; border-top: 1px solid var(--glass-border); background: rgba(0, 0, 0, .08); }
.character-footer > span { color: var(--text-dim); font-size: 8px; }
.loot-weight-bonus { color: var(--accent-purple); font-weight: 850; }
.reset-character { min-height: 27px; display: inline-flex; align-items: center; gap: 5px; padding: 0 8px; color: var(--text-muted); border: 1px solid transparent; border-radius: 7px; background: transparent; font-size: 8px; font-weight: 800; cursor: pointer; }
.reset-character:hover:not(:disabled) { color: #d6bbf5; border-color: rgba(190, 145, 242, .18); background: rgba(168, 112, 229, .07); }
.reset-character:disabled { opacity: .35; cursor: not-allowed; }
.card-busy { position: absolute; z-index: 5; inset: 3px 0 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 7px; color: #ddc5fa; background: rgba(20, 18, 24, .82); backdrop-filter: blur(4px); font-size: 9px; font-weight: 800; }
.card-busy svg, .store-loading svg, .refund-confirm svg { animation: store-spin .9s linear infinite; }
.store-error { display: flex; align-items: flex-start; gap: 10px; margin: 12px 0; padding: 12px 14px; color: var(--accent-red); border: 1px solid rgba(239, 128, 128, .24); border-radius: 12px; background: rgba(239, 128, 128, .07); }
.store-error > span { min-width: 0; display: flex; flex: 1; flex-direction: column; gap: 3px; }
.store-error strong { color: var(--text-secondary); font-size: 10px; }
.store-error small { overflow-wrap: anywhere; color: var(--text-dim); font-size: 8px; line-height: 1.5; }
.store-loading, .store-empty { min-height: 260px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; color: #c7a3f1; border: 1px dashed rgba(190, 145, 242, .2); border-radius: 16px; background: rgba(160, 105, 224, .035); text-align: center; }
.store-loading strong, .store-empty strong { color: var(--text-secondary); font-size: 12px; }
.store-loading span, .store-empty span { color: var(--text-dim); font-size: 9px; }
.store-empty .btn { margin-top: 5px; text-decoration: none; }
.refund-all { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: 20px; align-items: center; margin-top: 14px; padding: 20px 22px; border: 1px solid rgba(180, 132, 237, .18); border-radius: 15px; background: linear-gradient(120deg, rgba(155, 98, 219, .08), rgba(255, 255, 255, .02)); }
.refund-all h2 { margin: 3px 0 4px; color: var(--text-primary); font: 850 17px/1.2 var(--font-display); }
.refund-all p { margin: 0; color: var(--text-dim); font-size: 9px; }
.refund-actions .btn, .refund-confirm .btn { min-height: 39px; display: inline-flex; align-items: center; gap: 6px; }
.refund-confirm { display: flex; align-items: center; gap: 7px; }
.refund-confirm > strong { color: var(--text-secondary); font-size: 9px; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; }
@keyframes store-spin { to { transform: rotate(360deg); } }

@media (max-width: 940px) {
  .store-hero { grid-template-columns: auto 1fr; }
  .merchant-quote { grid-column: 1 / -1; margin-left: 96px; }
  .character-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .store-toolbar { grid-template-columns: 1fr auto; }
  .adjusted-toggle { grid-column: 1 / -1; justify-self: start; }
  .refund-all { grid-template-columns: 1fr; }
  .refund-confirm { flex-wrap: wrap; }
}

@media (max-width: 620px) {
  .back-link { min-height: 44px; }
  .store-hero { grid-template-columns: 1fr; padding: 23px 19px; }
  .hero-icon { width: 62px; height: 62px; }
  .merchant-quote { grid-column: auto; margin-left: 0; }
  .store-stats { grid-template-columns: 1fr; }
  .store-toolbar { grid-template-columns: 1fr; }
  .tier-tabs { overflow-x: auto; }
  .adjusted-toggle { grid-column: auto; }
  .character-grid { grid-template-columns: 1fr; }
  .adjust-button { min-height: 60px; }
  .refund-confirm { align-items: stretch; flex-direction: column; }
  .refund-confirm .btn { justify-content: center; width: 100%; }
}

@media (prefers-reduced-motion: reduce) {
  .meter-fill, .meter-track i { transition: none; }
  .card-busy svg, .store-loading svg, .refund-confirm svg { animation: none; }
}
</style>
