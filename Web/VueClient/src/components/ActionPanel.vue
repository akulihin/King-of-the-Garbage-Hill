<script setup lang="ts">
import { computed } from 'vue'
import { useGameStore } from 'src/store/game'

const store = useGameStore()
const canAct = computed(() => store.isMyTurn)
const me = computed(() => store.myPlayer)
const game = computed(() => store.gameState)

const hasLvlUpPoints = computed(() => (me.value?.status.lvlUpPoints ?? 0) > 0)
const hasMoral = computed(() => {
  if (!me.value) return false
  const moral = Number.parseFloat(me.value.character.moralDisplay) || 0
  return moral >= 1
})
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
        🛡️ Block
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

    <!-- Level up -->
    <div v-if="hasLvlUpPoints" class="action-group lvl-group">
      <span class="lvl-label">LVL UP ({{ me?.status.lvlUpPoints }})</span>
      <button class="act-btn lvl" title="Intelligence" @click="store.levelUp(1)">🧠 INT</button>
      <button class="act-btn lvl" title="Strength" @click="store.levelUp(2)">💪 STR</button>
      <button class="act-btn lvl" title="Speed" @click="store.levelUp(3)">⚡ SPD</button>
      <button class="act-btn lvl" title="Psyche" @click="store.levelUp(4)">🧘 PSY</button>
    </div>

    <!-- Moral exchange -->
    <div v-if="hasMoral" class="action-group">
      <button class="act-btn moral" title="Moral → Points" @click="store.moralToPoints()">
        Moral → Points
      </button>
      <button class="act-btn moral" title="Moral → Skill" @click="store.moralToSkill()">
        Moral → Skill
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
  gap: 10px;
  padding: 10px 14px;
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  margin-top: 10px;
  flex-wrap: wrap;
}

.status-chip {
  padding: 6px 14px;
  border-radius: 12px;
  font-size: 13px;
  font-weight: 600;
  white-space: nowrap;
}

.status-chip.ready {
  background: rgba(74, 222, 128, 0.15);
  color: var(--accent-green);
}

.status-chip.waiting {
  background: rgba(251, 146, 60, 0.15);
  color: var(--accent-orange);
}

.action-group {
  display: flex;
  align-items: center;
  gap: 6px;
  padding-left: 10px;
  border-left: 1px solid var(--border-color);
}

.act-btn {
  height: 40px;
  padding: 0 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-secondary);
  color: var(--text-primary);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.15s;
  white-space: nowrap;
}

.act-btn:hover:not(:disabled) {
  background: var(--bg-card-hover);
  border-color: var(--accent-blue);
  transform: translateY(-1px);
}

.act-btn:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.act-btn.shield:hover:not(:disabled) { border-color: var(--accent-blue); }
.act-btn.auto:hover:not(:disabled) { border-color: var(--accent-green); }
.act-btn.undo:hover { border-color: var(--accent-orange); }
.act-btn.skip:hover:not(:disabled) { border-color: var(--text-muted); }

.act-btn.predict-confirm {
  background: rgba(167, 139, 250, 0.1);
  border-color: var(--accent-purple);
  color: var(--accent-purple);
}
.act-btn.predict-confirm:hover:not(:disabled) { background: rgba(167, 139, 250, 0.2); }
.act-btn.predict-confirm.confirmed {
  background: rgba(74, 222, 128, 0.1);
  border-color: var(--accent-green);
  color: var(--accent-green);
  opacity: 0.7;
}

.lvl-group {
  gap: 4px;
}

.lvl-label {
  font-size: 11px;
  font-weight: 700;
  color: var(--accent-gold);
  text-transform: uppercase;
  white-space: nowrap;
  margin-right: 4px;
}

.act-btn.lvl {
  height: 38px;
  padding: 0 10px;
  font-size: 12px;
}

.act-btn.moral {
  padding: 0 12px;
  font-size: 12px;
  font-weight: 700;
}

.act-btn.moral:hover:not(:disabled) { border-color: var(--accent-orange); }

.web-toggle {
  margin-left: auto;
}

.act-btn.web-mode {
  background: rgba(96, 165, 250, 0.08);
  border-color: var(--border-color);
  color: var(--text-muted);
  font-size: 12px;
}
.act-btn.web-mode:hover {
  border-color: var(--accent-blue);
  color: var(--accent-blue);
  background: rgba(96, 165, 250, 0.15);
}
.act-btn.web-mode.active {
  background: rgba(96, 165, 250, 0.15);
  border-color: var(--accent-blue);
  color: var(--accent-blue);
  font-weight: 700;
}
</style>
