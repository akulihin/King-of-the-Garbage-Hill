<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import { signalrService } from 'src/services/signalr'
import BoardGrid from 'src/components/battleship/BoardGrid.vue'
import BattleLog from 'src/components/battleship/BattleLog.vue'
import { renderIcon } from 'src/components/battleship/battleship-icons'
import { useTip } from 'src/composables/useTip'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const props = defineProps<{ gameId: string }>()

const store = useBattleshipStore()

const p1 = computed(() => store.gameState?.player1)
const p2 = computed(() => store.gameState?.player2)
const phase = computed(() => store.phase)

function phaseBadgeClass(p: string) {
  return 'phase-' + p.toLowerCase()
}

onMounted(async () => {
  store.initCallbacks()
  await signalrService.joinBattleshipGame(props.gameId)
})

onUnmounted(async () => {
  await signalrService.leaveBattleshipGame(props.gameId)
  store.cleanupCallbacks()
})
</script>

<template>
  <div class="bs-spectate bs-pirate">
    <!-- Header -->
    <div class="spectate-header">
      <div class="header-left">
        <RouterLink to="/battleship" class="btn-pirate btn-sm">Назад</RouterLink>
        <span class="game-tag">#{{ gameId }}</span>
        <span class="phase-badge" :class="phaseBadgeClass(phase)">{{ phase }}</span>
      </div>
      <div class="header-right">
        <span v-if="store.turnNumber > 0" class="turn-badge" @mouseenter="showTip($event, 'Номер текущего хода')" @mousemove="moveTip" @mouseleave="hideTip">Ход {{ store.turnNumber }}</span>
        <span v-if="store.shotCount > 0" class="turn-badge" @mouseenter="showTip($event, 'Общий счётчик выстрелов в матче')" @mousemove="moveTip" @mouseleave="hideTip">Выстрел {{ store.shotCount }}</span>
        <span v-if="store.gameState?.currentTurnPlayerId" class="turn-indicator" :class="{ 'is-p1': store.gameState.currentTurnPlayerId === p1?.discordId }">
          Ходит: {{ store.gameState.currentTurnPlayerId === p1?.discordId ? (p1?.username ?? '?') : (p2?.username ?? '?') }}
        </span>
      </div>
    </div>

    <!-- Boards -->
    <div v-if="store.gameState" class="boards-layout">
      <div class="board-section">
        <div class="board-nameplate">
          <span class="player-label bs-font-body">{{ p1?.username ?? 'Игрок 1' }}</span>
          <span v-if="p1" class="indicator-badges">
            <span v-if="p1.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="p1.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
          </span>
          <span v-if="p1" class="revealed-count bs-font-data" @mouseenter="showTip($event, 'Разведано клеток из 100')" @mousemove="moveTip" @mouseleave="hideTip">{{ p1.revealedCellCount }}/100</span>
        </div>
        <BoardGrid :board="p1?.board ?? null" :ships="p1?.fleet" :cell-size="38" :animated-cells="store.myAnimatedCells" @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip" />
        <div v-if="p1?.fleet" class="fleet-summary">
          <span v-for="ship in p1.fleet" :key="ship.id" class="fleet-chip" :class="{ 'chip-sunk': ship.isDestroyed }" @mouseenter="showTip($event, `${ship.name}${ship.isDestroyed ? ' — потоплен' : ''}`)" @mousemove="moveTip" @mouseleave="hideTip">
            {{ ship.name }} <span v-if="ship.isDestroyed" class="chip-x">X</span>
          </span>
        </div>
      </div>
      <div class="board-section">
        <div class="board-nameplate">
          <span class="player-label bs-font-body">{{ p2?.username ?? 'Игрок 2' }}</span>
          <span v-if="p2" class="indicator-badges">
            <span v-if="p2.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="p2.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
          </span>
          <span v-if="p2" class="revealed-count bs-font-data" @mouseenter="showTip($event, 'Разведано клеток из 100')" @mousemove="moveTip" @mouseleave="hideTip">{{ p2.revealedCellCount }}/100</span>
        </div>
        <BoardGrid :board="p2?.board ?? null" :ships="p2?.fleet" :cell-size="38" :animated-cells="store.enemyAnimatedCells" @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip" />
        <div v-if="p2?.fleet" class="fleet-summary">
          <span v-for="ship in p2.fleet" :key="ship.id" class="fleet-chip" :class="{ 'chip-sunk': ship.isDestroyed }" @mouseenter="showTip($event, `${ship.name}${ship.isDestroyed ? ' — потоплен' : ''}`)" @mousemove="moveTip" @mouseleave="hideTip">
            {{ ship.name }} <span v-if="ship.isDestroyed" class="chip-x">X</span>
          </span>
        </div>
      </div>
    </div>

    <!-- Game Over Banner -->
    <div v-if="store.isFinished" class="gameover-banner">
      <div class="gameover-text bs-font-title">{{ store.gameState?.winnerId === p1?.discordId ? p1?.username : p2?.username }} победил!</div>
    </div>
    <div v-else-if="!store.gameState" class="loading bs-font-body">Загрузка...</div>

    <!-- Battle Log -->
    <BattleLog :entries="store.gameLog" />

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
@import 'src/components/battleship/battleship-theme.css';

.bs-spectate {
  max-width: 1000px;
  margin: 0 auto;
  background: var(--bs-sea-deep, #0a1628);
  min-height: 100vh;
  padding: 0.5rem;
}

/* ══════════════════════════════════════════════════════════════════════════
   Header — Wood plank bar
   ══════════════════════════════════════════════════════════════════════════ */
.spectate-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding: 0.5rem 0.75rem;
  background: var(--bs-wood-dark, #2a1a0e);
  background-image: repeating-linear-gradient(88deg, transparent, transparent 8px, rgba(74, 47, 26, 0.3) 8px, rgba(74, 47, 26, 0.3) 9px);
  border: 2px solid var(--bs-wood-mid, #4a2f1a);
  border-radius: 4px;
}
.header-left, .header-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}
.game-tag {
  font-family: 'JetBrains Mono', monospace;
  color: var(--bs-parchment-dim, #b09a78);
  font-size: 0.8rem;
}

/* ── Phase badges ── */
.phase-badge {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 4px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-family: 'Crimson Text', serif;
}
.phase-lobby { background: rgba(41, 128, 185, 0.2); color: var(--bs-ocean-blue, #2980b9); }
.phase-armyselection,
.phase-fleetbuilding { background: rgba(212, 168, 71, 0.2); color: var(--bs-gold, #d4a847); }
.phase-shipplacement { background: rgba(39, 174, 96, 0.2); color: var(--bs-poison-green, #27ae60); }
.phase-combat,
.phase-boarding { background: rgba(192, 57, 43, 0.2); color: var(--bs-fire-red, #c0392b); }
.phase-gameover { background: rgba(176, 154, 120, 0.15); color: var(--bs-parchment-dim, #b09a78); }

/* ── Turn info ── */
.turn-badge {
  font-family: 'JetBrains Mono', monospace;
  font-size: 0.75rem;
  color: var(--bs-parchment-dim, #b09a78);
}
.turn-indicator {
  font-size: 0.8rem;
  font-weight: 700;
  padding: 2px 10px;
  border-radius: 4px;
  color: var(--bs-parchment-dim, #b09a78);
  background: var(--bs-wood-dark, #2a1a0e);
  border: 1px solid var(--bs-wood-mid, #4a2f1a);
  font-family: 'Crimson Text', serif;
  transition: all 0.15s;
}
.turn-indicator.is-p1 {
  color: var(--bs-gold-bright, #f0c850);
  background: rgba(212, 168, 71, 0.15);
  border-color: var(--bs-gold, #d4a847);
  box-shadow: 0 0 8px rgba(212, 168, 71, 0.3);
}

/* ══════════════════════════════════════════════════════════════════════════
   Boards Layout — Side-by-side
   ══════════════════════════════════════════════════════════════════════════ */
.boards-layout {
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

/* ── Brass nameplate ── */
.board-nameplate {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.375rem;
  padding: 3px 12px;
  border-radius: 3px;
  background: linear-gradient(
    180deg,
    rgba(212, 168, 71, 0.18) 0%,
    rgba(42, 26, 14, 0.6) 100%
  );
  border: 1px solid var(--bs-wood-mid, #4a2f1a);
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.25);
}
.player-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--bs-parchment, #e8d5b0);
}

/* ── Status indicator badges ── */
.indicator-badges {
  display: flex;
  gap: 0.25rem;
}

/* ── Revealed count ── */
.revealed-count {
  font-size: 0.7rem;
  color: var(--bs-parchment-dim, #b09a78);
}

/* ══════════════════════════════════════════════════════════════════════════
   Fleet Summary — Rope chips
   ══════════════════════════════════════════════════════════════════════════ */
.fleet-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 0.375rem;
  max-width: 400px;
}
.fleet-chip {
  font-size: 0.6rem;
  padding: 1px 6px;
  border-radius: 3px;
  background: rgba(39, 174, 96, 0.2);
  color: var(--bs-poison-green, #27ae60);
  font-weight: 600;
  border: 1px dashed rgba(39, 174, 96, 0.35);
  font-family: 'Crimson Text', serif;
}
.fleet-chip.chip-sunk {
  background: rgba(192, 57, 43, 0.2);
  color: var(--bs-fire-red, #c0392b);
  border-color: rgba(192, 57, 43, 0.35);
  opacity: 0.5;
  text-decoration: line-through;
}
.chip-x {
  font-weight: 800;
}

/* ══════════════════════════════════════════════════════════════════════════
   Game Over Banner — Gold themed
   ══════════════════════════════════════════════════════════════════════════ */
.gameover-banner {
  text-align: center;
  padding: 2rem;
  margin: 1rem 0;
  border-radius: 6px;
  background: rgba(212, 168, 71, 0.1);
  border: 2px solid var(--bs-gold, #d4a847);
  box-shadow: 0 0 30px rgba(212, 168, 71, 0.15);
  animation: banner-appear 0.5s ease-out;
}
.gameover-text {
  font-size: 2rem;
  font-weight: 800;
  color: var(--bs-gold, #d4a847);
}

@keyframes banner-appear {
  from { opacity: 0; transform: scale(0.9); }
  to { opacity: 1; transform: scale(1); }
}

/* ══════════════════════════════════════════════════════════════════════════
   Loading
   ══════════════════════════════════════════════════════════════════════════ */
.loading {
  text-align: center;
  color: var(--bs-parchment-dim, #b09a78);
  padding: 3rem;
}

/* ══════════════════════════════════════════════════════════════════════════
   Responsive
   ══════════════════════════════════════════════════════════════════════════ */
@media (max-width: 768px) {
  .boards-layout { flex-direction: column; align-items: center; }
  .spectate-header { flex-direction: column; gap: 0.375rem; }
}
</style>
