<script setup lang="ts">
import { onMounted, onUnmounted, computed } from 'vue'
import { useBattleshipStore } from 'src/store/battleship'
import { signalrService } from 'src/services/signalr'
import 'src/components/battleship/battleship.css'
import BoardGrid from 'src/components/battleship/BoardGrid.vue'
import SummonTrailLegend from 'src/components/battleship/SummonTrailLegend.vue'
import BattleLogPanel from 'src/components/battleship/BattleLogPanel.vue'
import GameHeader from 'src/components/battleship/GameHeader.vue'
import { renderIcon } from 'src/components/battleship/battleship-icons'
import { useTip } from 'src/composables/useTip'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const props = defineProps<{ gameId: string }>()

const store = useBattleshipStore()

const p1 = computed(() => store.gameState?.player1)
const p2 = computed(() => store.gameState?.player2)
const phase = computed(() => store.phase)

const currentTurnName = computed(() => {
  if (!store.gameState?.currentTurnPlayerId) return ''
  return store.gameState.currentTurnPlayerId === p1.value?.discordId
    ? (p1.value?.username ?? '?')
    : (p2.value?.username ?? '?')
})

const phaseAccentClass = computed(() => {
  switch (phase.value) {
    case 'ArmySelection':
    case 'FleetBuilding': return 'bs-phase-fleet'
    case 'ShipPlacement': return 'bs-phase-placement'
    case 'Combat':
    case 'Boarding': return 'bs-phase-combat'
    case 'GameOver': return 'bs-phase-gameover'
    default: return 'bs-phase-lobby'
  }
})

function factionLabel(faction: string | undefined): string {
  if (faction === 'Alliance') return 'Альянс'
  if (faction === 'Empire') return 'Империя'
  return faction ?? ''
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
  <div class="bs-page bs-spectate" :class="phaseAccentClass">
    <GameHeader
      :game-id="gameId"
      :phase="phase"
      :turn-number="store.turnNumber"
      :shot-count="store.shotCount"
      mode="spectator"
      :current-turn-name="currentTurnName"
    />

    <!-- Boards -->
    <div v-if="store.gameState" class="boards-layout">
      <div class="board-section">
        <div class="board-nameplate">
          <span class="player-label">{{ p1?.username ?? 'Игрок 1' }}</span>
          <span v-if="p1" class="faction-label">{{ factionLabel(p1.faction) }}</span>
          <span v-if="p1" class="indicator-badges">
            <span v-if="p1.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="p1.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
          </span>
          <span v-if="p1" class="revealed-count bs-mono" @mouseenter="showTip($event, 'Разведано клеток из 100')" @mousemove="moveTip" @mouseleave="hideTip">{{ p1.revealedCellCount }}/100</span>
        </div>
        <BoardGrid :board="p1?.board ?? null" :ships="p1?.fleet" :cell-size="38" :animated-cells="store.myAnimatedCells" @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip" />
        <SummonTrailLegend :cells="p1?.board?.cells" />
        <div v-if="p1?.fleet" class="fleet-summary">
          <span v-for="ship in p1.fleet" :key="ship.id" class="fleet-chip" :class="{ 'chip-sunk': ship.isDestroyed }" @mouseenter="showTip($event, `${ship.name}${ship.isDestroyed ? ' — потоплен' : ''}`)" @mousemove="moveTip" @mouseleave="hideTip">
            {{ ship.name }} <span v-if="ship.isDestroyed" class="chip-x">X</span>
          </span>
        </div>
      </div>
      <div class="board-section">
        <div class="board-nameplate">
          <span class="player-label">{{ p2?.username ?? 'Игрок 2' }}</span>
          <span v-if="p2" class="faction-label">{{ factionLabel(p2.faction) }}</span>
          <span v-if="p2" class="indicator-badges">
            <span v-if="p2.stunShotExpiry >= store.shotCount" class="bs-badge bs-badge--stun" @mouseenter="showTip($event, 'Оглушён')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('stun', 12)"></span>
            <span v-if="p2.hasPenalty" class="bs-badge bs-badge--penalty" @mouseenter="showTip($event, 'Штраф')" @mousemove="moveTip" @mouseleave="hideTip" v-html="renderIcon('penalty', 12)"></span>
          </span>
          <span v-if="p2" class="revealed-count bs-mono" @mouseenter="showTip($event, 'Разведано клеток из 100')" @mousemove="moveTip" @mouseleave="hideTip">{{ p2.revealedCellCount }}/100</span>
        </div>
        <BoardGrid :board="p2?.board ?? null" :ships="p2?.fleet" :cell-size="38" :animated-cells="store.enemyAnimatedCells" @tip-show="showTip" @tip-move="moveTip" @tip-hide="hideTip" />
        <SummonTrailLegend :cells="p2?.board?.cells" />
        <div v-if="p2?.fleet" class="fleet-summary">
          <span v-for="ship in p2.fleet" :key="ship.id" class="fleet-chip" :class="{ 'chip-sunk': ship.isDestroyed }" @mouseenter="showTip($event, `${ship.name}${ship.isDestroyed ? ' — потоплен' : ''}`)" @mousemove="moveTip" @mouseleave="hideTip">
            {{ ship.name }} <span v-if="ship.isDestroyed" class="chip-x">X</span>
          </span>
        </div>
      </div>
    </div>

    <!-- Game Over Banner -->
    <div v-if="store.isFinished" class="gameover-banner bs-card">
      <div class="gameover-text">{{ store.gameState?.winnerId === p1?.discordId ? p1?.username : p2?.username }} победил!</div>
    </div>
    <div v-else-if="!store.gameState" class="loading">Загрузка...</div>

    <!-- Battle Log -->
    <BattleLogPanel :entries="store.gameLog" />

    <!-- Tooltip -->
    <Teleport to="body">
      <div v-if="tipVisible" class="pc-tooltip" :style="{ left: tipPos.x + 'px', top: tipPos.y + 'px' }">
        {{ tipText }}
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.bs-spectate {
  max-width: 1000px;
}

/* ── Boards Layout ── */
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

/* ── Nameplate ── */
.board-nameplate {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.375rem;
  padding: 3px 12px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--glass-border);
}
.player-label {
  font-size: 0.85rem;
  font-weight: 700;
  color: var(--text-primary);
}
.faction-label {
  padding: 1px 7px;
  border: 1px solid color-mix(in srgb, var(--accent-gold) 42%, transparent);
  border-radius: 999px;
  color: var(--accent-gold);
  background: color-mix(in srgb, var(--accent-gold) 9%, transparent);
  font-size: 0.62rem;
  font-weight: 800;
}

.indicator-badges {
  display: flex;
  gap: 0.25rem;
}

.revealed-count {
  font-size: 0.68rem;
  color: var(--text-dim);
}

/* ── Fleet summary chips ── */
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
  border-radius: 6px;
  background: color-mix(in srgb, var(--accent-green) 12%, transparent);
  color: var(--accent-green);
  font-weight: 600;
  border: 1px dashed color-mix(in srgb, var(--accent-green) 35%, transparent);
}
.fleet-chip.chip-sunk {
  background: color-mix(in srgb, var(--accent-red) 12%, transparent);
  color: var(--accent-red);
  border-color: color-mix(in srgb, var(--accent-red) 35%, transparent);
  opacity: 0.5;
  text-decoration: line-through;
}
.chip-x {
  font-weight: 800;
}

/* ── Game over banner ── */
.gameover-banner {
  --bs-accent: var(--accent-gold);
  text-align: center;
  padding: 2rem;
  margin: 1rem 0;
  box-shadow: 0 0 30px color-mix(in srgb, var(--accent-gold) 12%, transparent);
  animation: bs-banner-appear 0.5s ease-out;
}
.gameover-text {
  font-size: 1.8rem;
  font-weight: 900;
  color: var(--accent-gold);
}

/* ── Loading ── */
.loading {
  text-align: center;
  color: var(--text-muted);
  padding: 3rem;
}

/* ── Responsive ── */
@media (max-width: 768px) {
  .boards-layout { flex-direction: column; align-items: center; }
}
</style>
