<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useClashStore } from 'src/store/clash'
import { useGameStore } from 'src/store/game'
import {
  CLASH_FIELD_LIMITS,
  clampClashDimension,
} from 'src/features/clash/types'
import 'src/components/clash/clash.css'

const store = useClashStore()
const gameStore = useGameStore()
const router = useRouter()

const createOpen = ref(false)
const vsBot = ref(true)
const width = ref<number>(CLASH_FIELD_LIMITS.defaultWidth)
const length = ref<number>(CLASH_FIELD_LIMITS.defaultLength)
let pollTimer: ReturnType<typeof setInterval> | null = null

const games = computed(() => store.lobbyState?.games ?? [])
const resumableGameId = computed(() => store.myActiveGameId)
const limits = computed(() => store.catalog ?? {
  units: [],
  ...CLASH_FIELD_LIMITS,
  startingMorale: 0,
})

watch(() => store.catalog, (catalog) => {
  if (!catalog) return
  width.value = catalog.defaultWidth
  length.value = catalog.defaultLength
}, { immediate: true })

watch(() => store.navigationGameId, (gameId) => {
  if (!gameId) return
  store.consumeNavigationGameId()
  void router.push(`/clash/${gameId}`)
})

function safely(task: Promise<void>) {
  void task.catch(() => undefined)
}

onMounted(() => {
  store.initCallbacks()
  safely(store.refreshLobby())
  safely(store.requestCatalog())
  pollTimer = setInterval(() => {
    if (gameStore.isConnected && !store.isBusy) safely(store.refreshLobby())
  }, 3000)
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
  store.cleanupCallbacks()
})

function phaseLabel(phase: string) {
  const labels: Record<string, string> = {
    Lobby: 'Набор армий',
    InitialFrontPlacement: 'Скрытая расстановка',
    GuestSecondRowPlacement: 'Второй ряд',
    HostSecondRowPlacement: 'Второй ряд',
    GuestThirdRowPlacement: 'Третий ряд',
    HostThirdRowPlacement: 'Третий ряд',
    ResolvingClash: 'Идёт клэш',
    GuestReinforcement: 'Подкрепление',
    HostReinforcement: 'Подкрепление',
    ActiveExchange: 'Активки',
    Finished: 'Завершена',
  }
  return labels[phase] ?? phase
}

async function createGame() {
  width.value = clampClashDimension(width.value, limits.value.minWidth, limits.value.maxWidth)
  length.value = clampClashDimension(length.value, limits.value.minLength, limits.value.maxLength)
  try {
    const created = await store.createGame(vsBot.value, width.value, length.value)
    if (created) createOpen.value = false
  }
  catch {
    // The store surfaces the server error.
  }
}

async function joinGame(gameId: string) {
  try {
    await store.joinWebGame(gameId)
  }
  catch {
    // The store surfaces the server error.
  }
}

function resumeGame(gameId: string) {
  void router.push(`/clash/${gameId}`)
}
</script>

<template>
  <div class="clash-page clash-lobby">
    <section class="clash-hero">
      <div class="clash-hero__copy">
        <span class="clash-eyebrow">Empire's Endgame · тактическая дуэль</span>
        <h1>CLASH</h1>
        <p>
          Соберите армию, постройте три линии и прорвитесь к последнему ряду противника.
          Здесь каждый удар виден — и каждый шаг меняет фронт.
        </p>
        <div class="clash-hero__facts">
          <span>⚔ Одновременный бой</span>
          <span>➤ Скорость 1–9</span>
          <span>✦ Боевой дух 0–5</span>
        </div>
      </div>
      <button class="clash-btn clash-btn--primary clash-btn--hero" type="button" @click="createOpen = true">
        Создать лобби
      </button>
    </section>

    <section class="clash-panel clash-lobby__games">
      <header class="clash-panel__header">
        <div>
          <span class="clash-eyebrow">Военные комнаты</span>
          <h2>Доступные игры</h2>
        </div>
        <button
          type="button"
          class="clash-btn clash-btn--ghost"
          :disabled="store.isBusy"
          @click="safely(store.refreshLobby())"
        >
          Обновить
        </button>
      </header>

      <div v-if="games.length === 0" class="clash-empty clash-empty--large">
        <strong>На горизонте тихо</strong>
        <span>Создайте первую игру против бота или откройте комнату для игрока.</span>
      </div>
      <div v-else class="clash-game-list">
        <article v-for="game in games" :key="game.gameId" class="clash-game-card">
          <header>
            <span class="clash-game-card__id">#{{ game.gameId }}</span>
            <span class="clash-badge">{{ phaseLabel(game.phase) }}</span>
          </header>
          <div class="clash-game-card__versus">
            <strong>{{ game.hostName }}</strong>
            <span>VS</span>
            <strong :class="{ 'is-waiting': !game.guestName }">
              {{ game.guestName || (game.vsBot ? 'БОТ' : 'Ожидает игрока') }}
            </strong>
          </div>
          <footer>
            <span>{{ game.width }} × {{ game.length }}</span>
            <button
              v-if="resumableGameId === game.gameId"
              type="button"
              class="clash-btn clash-btn--accent"
              @click="resumeGame(game.gameId)"
            >
              Вернуться
            </button>
            <button
              v-else-if="game.canJoin"
              type="button"
              class="clash-btn clash-btn--accent"
              :disabled="store.isBusy"
              @click="joinGame(game.gameId)"
            >
              Вступить
            </button>
            <span v-else class="clash-game-card__closed">
              {{ game.phase === 'Finished' ? 'Завершено' : 'Комната занята' }}
            </span>
          </footer>
        </article>
      </div>
    </section>

    <Transition name="clash-modal">
      <div v-if="createOpen" class="clash-modal" role="dialog" aria-modal="true" aria-labelledby="clash-create-title" @click.self="createOpen = false">
        <form class="clash-modal__card" @submit.prevent="createGame">
          <header>
            <div>
              <span class="clash-eyebrow">Новая битва</span>
              <h2 id="clash-create-title">Создать лобби</h2>
            </div>
            <button type="button" class="clash-modal__close" aria-label="Закрыть" @click="createOpen = false">×</button>
          </header>

          <fieldset class="clash-opponent-picker">
            <legend>Противник</legend>
            <label :class="{ active: vsBot }">
              <input v-model="vsBot" type="radio" :value="true" />
              <span aria-hidden="true">◆</span>
              <strong>Бот</strong>
              <small>Начать сразу</small>
            </label>
            <label :class="{ active: !vsBot }">
              <input v-model="vsBot" type="radio" :value="false" />
              <span aria-hidden="true">⚔</span>
              <strong>Игрок</strong>
              <small>Открытое лобби</small>
            </label>
          </fieldset>

          <div class="clash-field-config">
            <label>
              <span>Ширина</span>
              <output>{{ width }}</output>
              <input
                v-model.number="width"
                type="range"
                :min="limits.minWidth"
                :max="limits.maxWidth"
              />
              <small>{{ limits.minWidth }}–{{ limits.maxWidth }} колонок</small>
            </label>
            <span class="clash-field-config__cross">×</span>
            <label>
              <span>Длина стороны</span>
              <output>{{ length }}</output>
              <input
                v-model.number="length"
                type="range"
                :min="limits.minLength"
                :max="limits.maxLength"
              />
              <small>{{ limits.minLength }}–{{ limits.maxLength }} рядов</small>
            </label>
          </div>

          <div class="clash-field-preview" :style="{ '--preview-width': width, '--preview-length': length }">
            <span v-for="cell in width * length" :key="cell" />
          </div>

          <footer>
            <button type="button" class="clash-btn clash-btn--ghost" @click="createOpen = false">Отмена</button>
            <button type="submit" class="clash-btn clash-btn--primary" :disabled="store.isCreating">
              {{ store.isCreating ? 'Создаём…' : 'Поднять знамя' }}
            </button>
          </footer>
        </form>
      </div>
    </Transition>

    <Transition name="clash-toast">
      <div v-if="store.errorMessage" class="clash-toast" role="alert">{{ store.errorMessage }}</div>
    </Transition>
  </div>
</template>
