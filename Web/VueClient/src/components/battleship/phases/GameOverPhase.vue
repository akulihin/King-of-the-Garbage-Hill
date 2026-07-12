<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import { useTip } from 'src/composables/useTip'
import BoardGrid from '../BoardGrid.vue'
import BattleLogPanel from '../BattleLogPanel.vue'
import GameOverCelebration from '../GameOverCelebration.vue'
import BsIcon from '../BsIcon.vue'

const store = useBattleshipStore()
const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const isWin = computed(() => store.isWinner)
const myPlayer = computed(() => store.myPlayer)
const enemyPlayer = computed(() => store.enemyPlayer)
const myFleet = computed(() => store.myFleet)

const winnerName = computed(() => {
  if (!store.gameState?.winnerId) return ''
  if (store.gameState.player1?.discordId === store.gameState.winnerId) return store.gameState.player1?.username ?? ''
  return store.gameState.player2?.username ?? ''
})

// The celebration modal shows once per finished game; Escape/dismiss reveals the boards
const celebrationVisible = ref(true)
const celebratedGameId = ref<string | null>(null)

watch(() => store.gameId, (id) => {
  if (id !== celebratedGameId.value) celebrationVisible.value = true
}, { immediate: true })

function dismissCelebration() {
  celebrationVisible.value = false
  celebratedGameId.value = store.gameId
}
</script>

<template>
  <div class="phase-content">
    <GameOverCelebration v-if="celebrationVisible" @dismiss="dismissCelebration" />

    <div class="victory-banner bs-card" :class="{ 'is-win': isWin }">
      <div class="victory-icon"><BsIcon :icon="isWin ? 'trophy' : 'skull'" :size="44" /></div>
      <h2 class="victory-title" :class="isWin ? 'win-text' : 'lose-text'">
        {{ isWin ? 'Победа!' : 'Поражение' }}
      </h2>
      <p class="winner-name">{{ winnerName }}</p>
    </div>

    <div class="combat-layout gameover-boards">
      <div class="board-section gameover-enemy-board">
        <div class="board-label">
          <span class="player-label">{{ enemyPlayer?.username ?? 'Противник' }}</span>
        </div>
        <BoardGrid :board="store.enemyBoard" :is-enemy="true" :cell-size="38" :animated-cells="store.enemyAnimatedCells" @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip" />
      </div>
      <div class="board-section" :class="{ 'gameover-winner-board': isWin }">
        <div class="board-label">
          <span class="player-label">{{ myPlayer?.username ?? 'Вы' }}</span>
        </div>
        <BoardGrid :board="store.myBoard" :ships="myFleet" :cell-size="38" :animated-cells="store.myAnimatedCells" @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip" />
      </div>
    </div>

    <BattleLogPanel :entries="store.gameLog" max-height="300px" />

    <div class="gameover-action">
      <RouterLink to="/battleship" class="bs-btn bs-btn--primary bs-btn--lg">Вернуться в лобби</RouterLink>
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

.victory-banner {
  text-align: center;
  padding: 2rem;
  animation: bs-banner-appear 0.5s ease-out;
}
.victory-banner.is-win {
  --bs-accent: var(--accent-gold);
  box-shadow: 0 0 30px color-mix(in srgb, var(--accent-gold) 14%, transparent);
}
.victory-icon { margin-bottom: 0.5rem; color: var(--text-muted); }
.is-win .victory-icon { color: var(--accent-gold); }
.victory-title { margin: 0 0 0.25rem; font-size: 2.4rem; font-weight: 900; }
.win-text { color: var(--accent-gold); }
.lose-text { color: var(--accent-red); }
.winner-name { color: var(--text-muted); font-size: 1rem; margin: 0; }

.victory-banner.is-win .victory-title {
  animation: victory-bloom 1.5s ease-out;
}
@keyframes victory-bloom {
  0% { text-shadow: 0 0 0 transparent; transform: scale(0.8); }
  30% { text-shadow: 0 0 40px color-mix(in srgb, var(--accent-gold) 80%, transparent), 0 0 80px color-mix(in srgb, var(--accent-gold) 40%, transparent); transform: scale(1.05); }
  100% { text-shadow: 0 0 20px color-mix(in srgb, var(--accent-gold) 60%, transparent), 0 2px 8px rgba(0, 0, 0, 0.5); transform: scale(1); }
}

.combat-layout {
  display: flex;
  gap: 1.5rem;
  flex-wrap: wrap;
  justify-content: center;
}
.board-section {
  display: flex;
  flex-direction: column;
  align-items: center;
}
.board-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.375rem;
}
.player-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-primary);
}

.gameover-boards { margin: 1rem 0; }
.gameover-action { text-align: center; margin-top: 1rem; }
.gameover-enemy-board {
  filter: grayscale(0.7) brightness(0.7);
  transition: filter 1s ease;
}
.gameover-winner-board {
  animation: winner-glow 2s ease-in-out infinite alternate;
}
@keyframes winner-glow {
  0% { box-shadow: 0 0 10px color-mix(in srgb, var(--accent-gold) 20%, transparent); }
  100% { box-shadow: 0 0 25px color-mix(in srgb, var(--accent-gold) 50%, transparent); }
}

@media (max-width: 768px) {
  .combat-layout {
    flex-direction: column;
    align-items: center;
  }
}
</style>
