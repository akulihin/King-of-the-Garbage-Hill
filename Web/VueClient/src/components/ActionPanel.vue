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
const dopaChoiceNeeded = computed(() => me.value?.dopaChoiceNeeded ?? false)
const dopaSecondAttack = computed(() => me.value?.passiveAbilityStates?.dopa?.needSecondAttack ?? false)

</script>

<template>
  <div class="action-bar" :class="{ 'can-act': canAct }">
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

    <div v-if="dopaChoiceNeeded" class="action-group dopa-choice">
      <button class="act-btn dopa-stomp" title="Стомп: +9 Силы и 99 Скилла" @click="store.dopaChoice('Стомп')">Стомп</button>
      <button class="act-btn dopa-farm" title="Фарм: Взгляд в будущее x2" @click="store.dopaChoice('Фарм')">Фарм</button>
      <button class="act-btn dopa-domination" title="Доминация: +20 Skill/win, target -1 bonus" @click="store.dopaChoice('Доминация')">Доминация</button>
      <button class="act-btn dopa-roam" title="Роум: Steal from non-adjacent" @click="store.dopaChoice('Роум')">Роум</button>
    </div>

    <div v-if="dopaSecondAttack" class="action-group">
      <span class="dopa-second-hint">Выберите вторую цель (скрытая атака)</span>
    </div>

  </div>
</template>

<style scoped>
.action-bar {
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 6px 10px;
  background: var(--glass-bg);
  backdrop-filter: blur(10px);
  -webkit-backdrop-filter: blur(10px);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
  flex-wrap: wrap;
  margin-bottom: 4px;
  box-shadow: var(--shadow-glow), inset 0 1px 0 var(--glass-highlight);
  transition: border-color 0.3s, box-shadow 0.3s;
}

/* 3A. Gold border pulse when it's your turn */
.action-bar.can-act {
  animation: can-act-pulse 2.5s ease-in-out infinite;
}
@keyframes can-act-pulse {
  0%, 100% { border-color: rgba(240, 200, 80, 0.15); box-shadow: var(--shadow-glow), inset 0 1px 0 var(--glass-highlight); }
  50% { border-color: rgba(240, 200, 80, 0.4); box-shadow: var(--shadow-glow), inset 0 1px 0 var(--glass-highlight), 0 0 14px rgba(240, 200, 80, 0.12); }
}

.action-group {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-left: 8px;
  border-left: 1px solid var(--glass-border);
  flex-wrap: wrap;
}

.act-btn {
  height: 34px;
  padding: 0 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius);
  background: var(--bg-secondary);
  color: var(--text-primary);
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: all 0.2s var(--ease-in-out);
  white-space: nowrap;
  position: relative;
  overflow: hidden;
}

/* Colored left-border accents by button type */
.act-btn.shield { border-left: 3px solid var(--accent-blue); }
.act-btn.auto { border-left: 3px solid var(--accent-green); }
.act-btn.undo { border-left: 3px solid var(--accent-orange); }
.act-btn.skip { border-left: 3px solid var(--text-dim); }

/* 3C. Color-tinted ripples on click */
.act-btn:active:not(:disabled)::after {
  content: '';
  position: absolute;
  inset: 0;
  background: radial-gradient(circle at center, rgba(255,255,255,0.15), transparent 70%);
  animation: btn-ripple 0.4s ease-out;
  pointer-events: none;
}
.act-btn.shield:active:not(:disabled)::after {
  background: radial-gradient(circle at center, rgba(110, 170, 240, 0.2), transparent 70%);
}
.act-btn.auto:active:not(:disabled)::after {
  background: radial-gradient(circle at center, rgba(63, 167, 61, 0.2), transparent 70%);
}
.act-btn.undo:active::after {
  background: radial-gradient(circle at center, rgba(230, 148, 74, 0.2), transparent 70%);
}

@keyframes btn-ripple {
  0% { transform: scale(0); opacity: 1; }
  100% { transform: scale(2.5); opacity: 0; }
}

.act-btn:hover:not(:disabled) {
  background: linear-gradient(180deg, var(--bg-card-hover), var(--bg-secondary));
  border-color: var(--accent-blue);
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3), 0 0 6px rgba(110, 170, 240, 0.1);
}

.act-btn:active:not(:disabled) {
  transform: translateY(0) scale(0.97);
}

.act-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
  filter: grayscale(0.5);
  border-left-color: var(--border-subtle);
  transform: scale(0.98);
  transition: all 0.4s var(--ease-in-out);
}

/* 3B. Per-type hover glow shadows */
.act-btn.shield:hover:not(:disabled) { border-color: var(--accent-blue); box-shadow: 0 4px 12px rgba(0,0,0,0.3), 0 0 10px rgba(110, 170, 240, 0.15); }
.act-btn.auto:hover:not(:disabled) { border-color: var(--accent-green); box-shadow: 0 4px 12px rgba(0,0,0,0.3), 0 0 10px rgba(63, 167, 61, 0.15); }
.act-btn.undo:hover { border-color: var(--accent-orange); box-shadow: 0 4px 12px rgba(0,0,0,0.3), 0 0 10px rgba(230, 148, 74, 0.15); }
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

.act-btn.dopa-stomp {
  background: rgba(255, 60, 60, 0.08);
  border-color: rgba(255, 60, 60, 0.3);
  color: #ff6e6e;
}
.act-btn.dopa-stomp:hover { background: rgba(255, 60, 60, 0.15); }

.act-btn.dopa-farm {
  background: rgba(60, 180, 60, 0.08);
  border-color: rgba(60, 180, 60, 0.3);
  color: #6ecc6e;
}
.act-btn.dopa-farm:hover { background: rgba(60, 180, 60, 0.15); }

.act-btn.dopa-domination {
  background: rgba(180, 60, 255, 0.08);
  border-color: rgba(180, 60, 255, 0.3);
  color: #b46eff;
}
.act-btn.dopa-domination:hover { background: rgba(180, 60, 255, 0.15); }

.act-btn.dopa-roam {
  background: rgba(74, 144, 217, 0.08);
  border-color: rgba(74, 144, 217, 0.3);
  color: #4a90d9;
}
.act-btn.dopa-roam:hover { background: rgba(74, 144, 217, 0.15); }

.dopa-second-hint {
  font-size: 11px;
  font-weight: 700;
  color: #4a90d9;
  animation: dopa-pulse 1.5s ease-in-out infinite;
}
@keyframes dopa-pulse {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}

</style>
