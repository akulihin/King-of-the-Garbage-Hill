<script setup lang="ts">
import { computed, ref } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import { message } from 'src/platform/localization'
import BsIcon from '../BsIcon.vue'

const store = useBattleshipStore()

const myPlayer = computed(() => store.myPlayer)
const enemyPlayer = computed(() => store.enemyPlayer)
const readyPending = ref(false)
const authoritativeBotVersion = computed(() => {
  const version = enemyPlayer.value?.botVersion ?? store.gameState?.botVersion
  return version === 2 || version === 3 ? version : 1
})
const enemyDisplayName = computed(() => {
  if (!enemyPlayer.value) return message('battleship.lobby.waitingForPlayer')
  if (enemyPlayer.value.isBot) {
    return message('battleship.lobby.botDisplayName', {
      version: authoritativeBotVersion.value,
    })
  }
  return enemyPlayer.value.username
})
const opponentIsReady = computed(() =>
  enemyPlayer.value?.isBot === true || enemyPlayer.value?.isReady === true,
)
const lobbyTitle = computed(() => {
  if (!enemyPlayer.value) return message('battleship.lobby.waitingTitle')
  if (myPlayer.value?.isReady) return message('battleship.lobby.waitingForOpponentReady')
  if (opponentIsReady.value) return message('battleship.lobby.opponentReadyTitle')
  return message('battleship.lobby.opponentJoinedTitle')
})
const readyButtonLabel = computed(() => {
  if (readyPending.value) return message('battleship.lobby.confirmingReady')
  if (myPlayer.value?.isReady) return message('battleship.lobby.readyWaitingAction')
  return message('battleship.lobby.startGame')
})
const readyDisabled = computed(() =>
  !enemyPlayer.value || myPlayer.value?.isReady === true || readyPending.value,
)

const emit = defineEmits<{
  leave: []
}>()

async function handleReady() {
  if (readyDisabled.value) return
  readyPending.value = true
  try {
    await store.confirmReady()
  }
  finally {
    readyPending.value = false
  }
}
</script>

<template>
  <div class="phase-content">
    <div class="bs-card centered-card">
      <span class="bs-kicker"><BsIcon icon="hourglass" :size="13" /> Лобби</span>
      <h3 class="card-title bs-title">{{ lobbyTitle }}</h3>
      <p class="card-subtitle">{{ myPlayer?.username ?? '' }} vs {{ enemyDisplayName }}</p>
      <button
        class="bs-btn bs-btn--primary bs-btn--lg"
        :disabled="readyDisabled"
        @click="handleReady"
      >
        {{ readyButtonLabel }}
      </button>
      <button class="bs-btn bs-btn--sm leave-btn" @click="emit('leave')">Выйти</button>
    </div>
  </div>
</template>

<style scoped>
.phase-content { margin-top: 0.5rem; }

.centered-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  text-align: center;
  padding: 2rem;
  max-width: 400px;
  margin: 2rem auto;
}
.card-title {
  margin: 0;
  font-size: 1.4rem;
}
.card-subtitle {
  color: var(--text-muted);
  margin: 0 0 0.75rem;
}
.leave-btn { margin-top: 0.25rem; opacity: 0.75; }
</style>
