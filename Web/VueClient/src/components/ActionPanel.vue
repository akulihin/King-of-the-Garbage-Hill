<script setup lang="ts">
import { computed } from 'vue'
import { useGameStore } from 'src/store/game'

const store = useGameStore()
const canAct = computed(() => store.isMyTurn)
const me = computed(() => store.myPlayer)
const game = computed(() => store.gameState)

const isReady = computed(() => me.value?.status.isReady && !me.value?.status.isSkip)
const hasPredictions = computed(() => (me.value?.predictions?.length ?? 0) > 0)

// "Predicted ✓" locked state only shows at round 8+
const isLockedPredict = computed(() => {
  if (!me.value || !game.value) return false
  return me.value.status.confirmedPredict && game.value.roundNo >= 8
})

const darksciChoiceNeeded = computed(() => me.value?.darksciChoiceNeeded ?? false)
const youngGlebAvailable = computed(() => me.value?.youngGlebAvailable ?? false)

</script>

<template>
  <div class="action-bar">
    <!-- Core actions -->
    <div class="action-group">
      <button
        class="act-btn shield"
        data-sfx-skip-default="true"
        :disabled="!canAct"
        title="Block"
        @click="store.block()"
      >
        <span class="gi gi-lg gi-def">DEF</span> Block
      </button>
      <button
        class="act-btn auto"
        :disabled="!canAct"
        title="Auto Move"
        @click="store.autoMove()"
      >
        <span class="gi gi-lg gi-auto">AUTO</span> Move
      </button>
      <button
        v-if="isReady"
        class="act-btn undo"
        title="Change Mind"
        @click="store.changeMind()"
      >
        <span class="gi gi-lg gi-undo">UNDO</span> Change
      </button>
      <button
        v-if="!me?.status.confirmedSkip"
        class="act-btn skip"
        title="Confirm Skip"
        @click="store.confirmSkip()"
      >
        <span class="gi gi-lg gi-skip">SKIP</span>
      </button>
    </div>

    <div v-if="(game?.roundNo ?? 0) >= 8 && !store.isKira" class="action-group">
      <button
        class="act-btn predict-confirm"
        :class="{ confirmed: isLockedPredict }"
        :disabled="isLockedPredict"
        title="Confirm Predictions"
        @click="store.confirmPredict()"
      >
        {{ isLockedPredict ? '🔮 Predicted ✓' : 'Confirm Prediction' }}
      </button>
    </div>

    <div v-if="darksciChoiceNeeded" class="action-group darksci-choice">
      <button
        class="act-btn darksci-stable"
        title="Стабильный: +20 Skill, +2 Moral"
        @click="store.darksciChoice(true)"
      >
        Мне не везёт...
      </button>
      <button
        class="act-btn darksci-unstable"
        title="Нестабильный: удача решит"
        @click="store.darksciChoice(false)"
      >
        Мне повезёт!
      </button>
    </div>

    <div v-if="youngGlebAvailable" class="action-group">
      <button
        class="act-btn young-gleb"
        title="Трансформироваться в Молодого Глеба"
        @click="store.youngGleb()"
      >
        Вспомнить Молодость
      </button>
    </div>

  </div>
</template>

<style scoped>
.action-bar {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-lg);
  flex-wrap: wrap;
  margin-bottom: 4px;
}

.action-group {
  display: flex;
  align-items: center;
  gap: 3px;
  padding-left: 6px;
  border-left: 1px solid var(--border-subtle);
  flex-wrap: wrap;
}

.act-btn {
  height: 28px;
  padding: 0 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  background: var(--bg-secondary);
  color: var(--text-primary);
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.15s;
  white-space: nowrap;
}

.act-btn:hover:not(:disabled) {
  background: var(--bg-card-hover);
  border-color: var(--accent-blue);
}

.act-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.act-btn.shield:hover:not(:disabled) { border-color: var(--accent-blue); }
.act-btn.auto:hover:not(:disabled) { border-color: var(--accent-green); }
.act-btn.undo:hover { border-color: var(--accent-orange); }
.act-btn.skip:hover:not(:disabled) { border-color: var(--text-muted); }

.act-btn.predict-confirm {
  background: rgba(180, 150, 255, 0.06);
  border-color: rgba(180, 150, 255, 0.3);
  color: var(--accent-purple);
}
.act-btn.predict-confirm:hover:not(:disabled) { background: rgba(180, 150, 255, 0.12); }
.act-btn.predict-confirm.confirmed {
  background: rgba(63, 167, 61, 0.06);
  border-color: rgba(63, 167, 61, 0.2);
  color: var(--accent-green);
  opacity: 0.7;
}

.act-btn.darksci-stable {
  background: rgba(60, 120, 255, 0.08);
  border-color: rgba(60, 120, 255, 0.3);
  color: #6eaaff;
}
.act-btn.darksci-stable:hover { background: rgba(60, 120, 255, 0.15); }

.act-btn.darksci-unstable {
  background: rgba(255, 60, 60, 0.08);
  border-color: rgba(255, 60, 60, 0.3);
  color: #ff6e6e;
}
.act-btn.darksci-unstable:hover { background: rgba(255, 60, 60, 0.15); }

.act-btn.young-gleb {
  background: rgba(255, 180, 40, 0.08);
  border-color: rgba(255, 180, 40, 0.3);
  color: #ffb428;
}
.act-btn.young-gleb:hover { background: rgba(255, 180, 40, 0.15); }

</style>
