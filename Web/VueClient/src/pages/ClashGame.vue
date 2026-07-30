<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useClashStore } from 'src/store/clash'
import ClashArmyBuilder from 'src/components/clash/ClashArmyBuilder.vue'
import ClashBetweenPhase from 'src/components/clash/ClashBetweenPhase.vue'
import ClashCombatPhase from 'src/components/clash/ClashCombatPhase.vue'
import ClashDeploymentPhase from 'src/components/clash/ClashDeploymentPhase.vue'
import {
  CLASH_FIELD_LIMITS,
  clashPhaseKind,
} from 'src/features/clash/types'
import 'src/components/clash/clash.css'

const props = defineProps<{ gameId: string }>()
const store = useClashStore()
const router = useRouter()
const configWidth = ref(CLASH_FIELD_LIMITS.defaultWidth)
const configLength = ref(CLASH_FIELD_LIMITS.defaultLength)

const state = computed(() => store.gameState)
const me = computed(() => store.myPlayer)
const opponent = computed(() => store.opponent)
const kind = computed(() => state.value ? clashPhaseKind(state.value) : null)
const handMinimum = computed(() => (state.value?.width ?? 0) * 3)
const handMaximum = computed(() => (state.value?.width ?? 0) * (state.value?.length ?? 0))
const selectedArmySize = computed(() =>
  me.value?.selectedArmyDefinitionIds.length ?? me.value?.armySize ?? 0)
const handIsValid = computed(() => {
  const count = selectedArmySize.value
  return count >= handMinimum.value && count <= handMaximum.value
})
const winnerName = computed(() => {
  if (!state.value?.winnerId) return ''
  if (state.value.host?.playerId === state.value.winnerId) return state.value.host.username
  if (state.value.guest?.playerId === state.value.winnerId) return state.value.guest.username
  return 'Победитель'
})
const didIWin = computed(() =>
  !!state.value?.winnerId && state.value.winnerId === state.value.myPlayerId)
const terminalReasonLabel = computed(() => {
  switch (state.value?.terminalReason) {
    case 'Breach': return 'Прорыв последней полосы'
    case 'DualBreach': return 'Двойной прорыв'
    case 'Elimination': return 'Армия уничтожена'
    case 'MutualElimination': return 'Взаимное уничтожение'
    case 'Forfeit': return 'Сдача'
    case 'Leave': return 'Соперник покинул игру'
    default: return 'Исход определён правилами клэша'
  }
})

watch(state, (next) => {
  if (!next) return
  configWidth.value = next.width
  configLength.value = next.length
}, { immediate: true })

function safely(task: Promise<void>) {
  void task.catch(() => undefined)
}

onMounted(async () => {
  store.initCallbacks()
  try {
    await store.joinGame(props.gameId)
    await Promise.all([
      store.requestState(props.gameId),
      store.requestCatalog(),
    ])
  }
  catch {
    // The page-level toast surfaces connection and membership errors.
  }
})

onUnmounted(() => {
  safely(store.leaveGame(props.gameId))
  store.cleanupCallbacks()
})

async function leaveToLobby() {
  await router.push('/clash')
}

async function leaveLobbyGame() {
  try {
    await store.leaveWebGame(props.gameId)
    await router.push('/clash')
  }
  catch {
    // The store surfaces the error.
  }
}

async function forfeit() {
  if (!window.confirm('Сдаться в этом клэше?')) return
  safely(store.forfeit())
}

async function saveConfiguration() {
  safely(store.setConfiguration(configWidth.value, configLength.value))
}

async function saveArmy(ids: string[]) {
  safely(store.setArmy(ids))
}
</script>

<template>
  <div class="clash-page clash-game">
    <header class="clash-game-header">
      <button type="button" class="clash-btn clash-btn--ghost" @click="leaveToLobby">← Лобби</button>
      <div class="clash-game-header__title">
        <span class="clash-eyebrow">Комната #{{ gameId }}</span>
        <strong v-if="state">Клэш {{ state.clashNumber }} · {{ state.width }}×{{ state.length }}</strong>
        <strong v-else>Подключение к полю…</strong>
      </div>
      <div v-if="state && kind !== 'lobby' && kind !== 'finished'" class="clash-game-header__players">
        <span :class="{ 'is-current': state.currentTurnPlayerId === state.host?.playerId }">
          {{ state.host?.username || 'Хост' }} <b>+{{ state.host?.morale ?? 0 }}</b>
        </span>
        <i>VS</i>
        <span :class="{ 'is-current': state.currentTurnPlayerId === state.guest?.playerId }">
          {{ state.guest?.username || 'Гость' }} <b>+{{ state.guest?.morale ?? 0 }}</b>
        </span>
      </div>
      <button
        v-if="state && kind !== 'lobby' && kind !== 'finished' && state.canForfeit"
        type="button"
        class="clash-btn clash-btn--danger"
        @click="forfeit"
      >
        Сдаться
      </button>
      <span v-else />
    </header>

    <div v-if="!state" class="clash-panel clash-loading">
      <span class="clash-loading__sigil">⚔</span>
      <strong>Разворачиваем поле боя</strong>
      <small>Получаем персональное состояние с сервера…</small>
    </div>

    <section v-else-if="kind === 'lobby' && me" class="clash-lobby-room">
      <header class="clash-phase__header">
        <div>
          <span class="clash-eyebrow">Военная комната</span>
          <h1>Подготовьте армию</h1>
          <p v-if="opponent">Оба игрока могут собирать руки одновременно.</p>
          <p v-else>Ожидаем приглашённого игрока. Руку можно собрать заранее.</p>
        </div>
        <div class="clash-lobby-room__players">
          <span :class="{ ready: state.host?.isReady }">
            {{ state.host?.username || 'Хост' }}
            <b>{{ state.host?.isReady ? 'Готов' : 'Собирает руку' }}</b>
          </span>
          <i>VS</i>
          <span :class="{ ready: state.guest?.isReady }">
            {{ state.guest?.username || 'Ожидание игрока' }}
            <b v-if="state.guest">{{ state.guest.isReady ? 'Готов' : 'Собирает руку' }}</b>
          </span>
        </div>
      </header>

      <section v-if="me.isHost" class="clash-panel clash-room-config">
        <header>
          <div>
            <span class="clash-eyebrow">Конфигурация хоста</span>
            <strong>Поле {{ configWidth }} × {{ configLength }}</strong>
          </div>
          <button
            type="button"
            class="clash-btn clash-btn--ghost"
            :disabled="!state.canConfigure || store.isBusy"
            @click="saveConfiguration"
          >
            Применить
          </button>
        </header>
        <label>
          Ширина
          <input v-model.number="configWidth" type="range" min="3" max="10" :disabled="!state.canConfigure" />
          <output>{{ configWidth }}</output>
        </label>
        <label>
          Длина
          <input v-model.number="configLength" type="range" min="3" max="5" :disabled="!state.canConfigure" />
          <output>{{ configLength }}</output>
        </label>
      </section>

      <ClashArmyBuilder
        :units="store.catalog?.units ?? []"
        :current-hand="me.hand"
        :selected-definition-ids="me.selectedArmyDefinitionIds"
        :width="state.width"
        :length="state.length"
          :disabled="!state.canSetArmy || store.isBusy"
        @confirm="saveArmy"
      />

      <footer class="clash-phase__actions clash-room-ready">
        <button type="button" class="clash-btn clash-btn--ghost" @click="leaveLobbyGame">
          Покинуть комнату
        </button>
        <span v-if="!handIsValid">В руке должно быть {{ handMinimum }}–{{ handMaximum }} юнитов.</span>
        <span v-else-if="me.isReady">Вы готовы. Ждём вторую сторону.</span>
        <span v-else>Подтвердите готовность, чтобы перейти к первой полосе.</span>
        <button
          type="button"
          class="clash-btn clash-btn--primary clash-btn--large"
          :disabled="!handIsValid || !state.canConfirmReady || store.isBusy"
          @click="store.confirmLobbyReady()"
        >
          {{ me.isReady ? 'Готово' : 'К расстановке' }}
        </button>
      </footer>
    </section>

    <ClashCombatPhase v-else-if="store.timelinePlaying || kind === 'combat'" />
    <ClashDeploymentPhase v-else-if="kind === 'deployment'" />
    <ClashBetweenPhase v-else-if="kind === 'reinforcement' || kind === 'actives'" />

    <section v-else-if="kind === 'finished'" class="clash-result">
      <span class="clash-result__sigil" aria-hidden="true">{{ state.isDraw ? '◇' : didIWin ? '♛' : '⚔' }}</span>
      <span class="clash-eyebrow">Битва завершена</span>
      <h1>{{ state.isDraw ? 'Ничья' : didIWin ? 'Победа' : 'Поражение' }}</h1>
      <p v-if="state.isDraw">
        {{ state.terminalReason === 'DualBreach'
          ? 'Обе стороны одновременно прорвались к последней полосе.'
          : 'Все юниты обеих армий погибли одновременно.' }}
      </p>
      <p v-else>{{ winnerName }} удержал поле.</p>
      <strong>{{ terminalReasonLabel }}</strong>
      <button type="button" class="clash-btn clash-btn--primary clash-btn--large" @click="leaveToLobby">
        Вернуться в лобби
      </button>
    </section>

    <section v-else class="clash-panel clash-loading">
      <strong>Фаза {{ state.phase }}</strong>
      <small>Ждём следующего серверного состояния…</small>
    </section>

    <Transition name="clash-toast">
      <div v-if="store.errorMessage" class="clash-toast" role="alert">{{ store.errorMessage }}</div>
    </Transition>
  </div>
</template>
