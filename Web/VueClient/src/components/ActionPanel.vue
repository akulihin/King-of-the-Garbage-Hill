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

const preferWeb = computed(() => game.value?.preferWeb ?? false)

function togglePreferWeb() {
  store.setPreferWeb(!preferWeb.value)
}
</script>

<template>
  <div class="action-bar">
    <!-- Status -->
    <div class="status-chip" :class="{ ready: me?.status.isReady, waiting: !me?.status.isReady }">
      {{ me?.status.isReady ? '✓ Ready' : me?.status.isSkip ? '⏭ Skip' : '⏳ Your turn' }}
    </div>

    <!-- Core actions -->
    <div class="action-group">
      <button
        class="act-btn shield"
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
        🤖 Auto
      </button>
      <button
        v-if="isReady"
        class="act-btn undo"
        title="Change Mind"
        @click="store.changeMind()"
      >
        ↩ Undo
      </button>
      <button
        v-if="!me?.status.confirmedSkip"
        class="act-btn skip"
        title="Confirm Skip"
        @click="store.confirmSkip()"
      >
        ⏭ Skip
      </button>
    </div>

    <div v-if="(game?.roundNo ?? 0) >= 8 && (hasPredictions || isLockedPredict)" class="action-group">
      <button
        class="act-btn predict-confirm"
        :class="{ confirmed: isLockedPredict }"
        :disabled="isLockedPredict"
        title="Confirm Predictions"
        @click="store.confirmPredict()"
      >
        {{ isLockedPredict ? '🔮 Predicted ✓' : '🔮 Confirm Predict' }}
      </button>
    </div>

    <!-- Web-only mode toggle -->
    <div class="action-group web-toggle">
      <button
        class="act-btn web-mode"
        :class="{ active: preferWeb }"
        title="When enabled, Discord messages are suppressed — play only via Web"
        @click="togglePreferWeb()"
      >
        {{ preferWeb ? '🌐 Web Only ✓' : '🌐 Web Only' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.action-bar {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 8px;
  background: var(--bg-card);
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  margin-top: 6px;
  flex-wrap: wrap;
}

.status-chip {
  padding: 3px 8px;
  border-radius: var(--radius);
  font-size: 10px;
  font-weight: 700;
  white-space: nowrap;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.status-chip.ready {
  background: rgba(63, 167, 61, 0.1);
  color: var(--accent-green);
  border: 1px solid rgba(63, 167, 61, 0.2);
}

.status-chip.waiting {
  background: rgba(230, 148, 74, 0.1);
  color: var(--accent-orange);
  border: 1px solid rgba(230, 148, 74, 0.2);
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

.web-toggle {
  margin-left: 0;
  border-left: none;
  padding-left: 0;
}

.act-btn.web-mode {
  background: rgba(80, 150, 230, 0.05);
  border-color: var(--border-subtle);
  color: var(--text-muted);
  font-size: 11px;
}
.act-btn.web-mode:hover {
  border-color: var(--accent-blue);
  color: var(--accent-blue);
}
.act-btn.web-mode.active {
  background: rgba(80, 150, 230, 0.1);
  border-color: var(--accent-blue);
  color: var(--accent-blue);
  font-weight: 800;
}
</style>
