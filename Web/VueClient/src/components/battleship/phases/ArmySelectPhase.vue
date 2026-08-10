<script setup lang="ts">
import { useBattleshipStore } from 'src/store/battleship'
import { useTip } from 'src/composables/useTip'
import { message } from 'src/platform/localization'
import BsIcon from '../BsIcon.vue'

const store = useBattleshipStore()
const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()
</script>

<template>
  <div class="phase-content">
    <div class="bs-card centered-card">
      <span class="bs-kicker"><BsIcon icon="flag" :size="13" /> Фракция</span>
      <h3 class="card-title bs-title">Выберите армию</h3>
      <div class="army-options">
        <button class="bs-btn bs-btn--primary bs-btn--lg" @click="store.selectArmy('Empire')">Империя</button>
        <button
          class="bs-btn bs-btn--primary bs-btn--lg"
          @mouseenter="showTip($event, 'Бюджет 50 монет и уникальные корабли Альянса')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
          @click="store.selectArmy('Alliance')"
        >
          Альянс
        </button>
        <button
          class="bs-btn bs-btn--primary bs-btn--lg"
          @mouseenter="showTip($event, message('battleship.faction.captainFlint.tooltip'))"
          @mousemove="moveTip"
          @mouseleave="hideTip"
          @click="store.selectArmy('CaptainFlint')"
        >
          {{ message('battleship.faction.captainFlint') }}
        </button>
      </div>
    </div>

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
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
  max-width: 440px;
  margin: 2rem auto;
}
.card-title {
  margin: 0 0 0.5rem;
  font-size: 1.4rem;
}
.army-options {
  display: flex;
  gap: 1rem;
  justify-content: center;
  flex-wrap: wrap;
  margin-top: 0.5rem;
}
</style>
