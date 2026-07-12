<script setup lang="ts">
import { computed } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import BsIcon from '../BsIcon.vue'

const store = useBattleshipStore()

const myPlayer = computed(() => store.myPlayer)
const enemyPlayer = computed(() => store.enemyPlayer)

const emit = defineEmits<{
  leave: []
}>()

async function handleReady() {
  await store.confirmReady()
}
</script>

<template>
  <div class="phase-content">
    <div class="bs-card centered-card">
      <span class="bs-kicker"><BsIcon icon="hourglass" :size="13" /> Лобби</span>
      <h3 class="card-title bs-title">Ожидание противника...</h3>
      <p class="card-subtitle">{{ myPlayer?.username ?? '' }} vs {{ enemyPlayer?.username ?? 'Бот' }}</p>
      <button class="bs-btn bs-btn--primary bs-btn--lg" @click="handleReady">Начать игру</button>
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
