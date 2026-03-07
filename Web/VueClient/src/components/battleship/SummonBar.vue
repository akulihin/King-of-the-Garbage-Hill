<script setup lang="ts">
import { ref, watch } from 'vue'
import type { BattleshipPlayerState, BattleshipPendingSummon } from 'src/services/signalr'
import { ICONS, renderIcon } from './battleship-icons'
import { useTip } from 'src/composables/useTip'

const { tipText, tipVisible, tipPos, showTip, moveTip, hideTip } = useTip()

const props = defineProps<{
  myPlayer: BattleshipPlayerState | null
  phase: string
  shotCount: number
  canDeploySummon: boolean
  hasBranderUpgrade: boolean
  summonDeployMode: { type: string; pendingId?: string; pendingCols?: number[] } | null
}>()

const emit = defineEmits<{
  enterDeploy: []
  enterPendingDeploy: [ps: BattleshipPendingSummon]
  cancelDeploy: []
  setSummonType: [type: string]
}>()

// ── Internal State ──────────────────────────────────────────────
const summonType = ref('Ram')

watch(summonType, (val) => {
  emit('setSummonType', val)
})

// ── Summon descriptions ─────────────────────────────────────────
const summonDescriptions: Record<string, string> = {
  Ram: 'Таран — скорость 2, урон 4, погибает при столкновении, разведка пути',
  Scout: 'Разведчик — скорость 1, отложенная разведка в Space, не открывает при заморозке',
  Brander: 'Брандер — скорость 1, проходит сквозь корабли, стреляйте в него для подрыва (ожог зоны)',
}

// ── Helpers ─────────────────────────────────────────────────────
const summonTypeNameRu: Record<string, string> = {
  Ram: 'Таран',
  Scout: 'Разведчик',
  Brander: 'Брандер',
  CursedBoat: 'Проклятый',
  PirateBoat: 'Пират',
}

const summonIconKey: Record<string, string> = {
  Ram: 'ram',
  Scout: 'scout',
  Brander: 'brander',
  CursedBoat: 'cursedBoat',
  PirateBoat: 'pirateBoat',
}

function nameRu(type: string): string {
  return summonTypeNameRu[type] ?? type
}

function iconFor(type: string): string {
  const key = summonIconKey[type]
  return key ? renderIcon(key, 16) : ''
}

function posLabel(row: number, col: number): string {
  return String.fromCharCode(65 + col) + (row + 1)
}
</script>

<template>
  <div class="summon-bar-root">
    <!-- 1. Summon Deployment Bar -->
    <div class="summon-bar card-wood">
      <span class="sb-label">Призыв ({{ myPlayer?.summonSlotsUsed ?? 0 }}/{{ myPlayer?.maxSummonSlots ?? 4 }}):</span>
      <select
        v-model="summonType"
        class="sb-select"
        @mouseenter="showTip($event, summonDescriptions[summonType] ?? '')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
      >
        <option value="Ram">Таран</option>
        <option value="Scout">Разведчик</option>
        <option v-if="hasBranderUpgrade" value="Brander">Брандер</option>
      </select>
      <button
        class="btn-pirate sb-deploy-btn"
        :disabled="!canDeploySummon"
        @mouseenter="showTip($event, canDeploySummon ? 'Выберите клетку на строке 1 вражеского поля' : '')"
        @mousemove="moveTip"
        @mouseleave="hideTip"
        @click="emit('enterDeploy')"
      >
        Разместить на карте
      </button>
      <span v-if="!canDeploySummon && myPlayer" class="sb-hint">
        <template v-if="myPlayer.summonSlotsUsed >= (myPlayer.maxSummonSlots ?? 4)">
          Все слоты заняты
        </template>
        <template v-else-if="myPlayer.summonCooldownRemaining > 0">
          Перезарядка: {{ myPlayer.summonCooldownRemaining }} выстр.
        </template>
        <template v-else>
          Нужно разведать {{ 5 * ((myPlayer?.summonSlotsUsed ?? 0) + 1) }} клеток
        </template>
      </span>
    </div>

    <!-- 2. Active Summons Status -->
    <div v-if="myPlayer?.summons?.filter(s => s.isAlive).length" class="summon-status-bar card-wood">
      <span class="sb-label">Активные призывы:</span>
      <span
        v-for="s in myPlayer!.summons.filter(ss => ss.isAlive)"
        :key="s.id"
        class="summon-chip"
        :class="'summon-' + s.type.toLowerCase()"
      >
        <span class="summon-chip-icon" v-html="iconFor(s.type)" />
        {{ nameRu(s.type) }}
        <span class="summon-pos">{{ posLabel(s.row, s.col) }}</span>
        <span
          v-if="s.waitingForTurnBack"
          class="summon-wait"
          @mouseenter="showTip($event, 'Ожидает разворота')"
          @mousemove="moveTip"
          @mouseleave="hideTip"
        >&#x21A9;</span>
      </span>
    </div>

    <!-- 3. Pending Summons -->
    <div v-if="myPlayer?.pendingSummons?.length" class="pending-bar card-wood">
      <span class="sb-label">Ожидающие призывы:</span>
      <div v-for="ps in myPlayer!.pendingSummons" :key="ps.id" class="pending-entry">
        <span class="pending-name">{{ ps.sourceShipName || ps.type }}</span>
        <span v-if="ps.isBoarding" class="boarding-badge">абордаж</span>
        <span v-if="ps.allowedColumns.length" class="sb-hint">
          (столбцы: {{ ps.allowedColumns.map(c => String.fromCharCode(65 + c)).join(', ') }})
        </span>
        <button class="btn-pirate sb-deploy-btn" @click="emit('enterPendingDeploy', ps)">
          Разместить на карте
        </button>
      </div>
    </div>

    <!-- 4. Deploy Mode Banner -->
    <div v-if="summonDeployMode" class="deploy-banner">
      <span class="deploy-banner-text">
        Выберите клетку на первой строчке вражеского поля для размещения {{ summonDeployMode.type }}
      </span>
      <span v-if="summonDeployMode.pendingCols" class="deploy-cols">
        (столбцы: {{ summonDeployMode.pendingCols.map(c => String.fromCharCode(65 + c)).join(', ') }})
      </span>
      <button class="sb-cancel-btn" @click="emit('cancelDeploy')">Отмена</button>
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
/* ── Summon Bar Root ────────────────────────────────────── */
.summon-bar-root {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

/* ── Shared bar layout (card-wood from theme) ──────────── */
.summon-bar,
.summon-status-bar,
.pending-bar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  flex-wrap: wrap;
}

.pending-bar {
  flex-direction: column;
  align-items: flex-start;
}

/* ── Labels ────────────────────────────────────────────── */
.sb-label {
  font-family: 'Crimson Text', 'Georgia', serif;
  font-weight: 700;
  font-size: 0.8rem;
  color: var(--bs-parchment);
  white-space: nowrap;
}

/* ── Select dropdown ───────────────────────────────────── */
.sb-select {
  padding: 4px 8px;
  border-radius: 4px;
  background: var(--bs-wood-dark);
  color: var(--bs-parchment);
  border: 1px solid var(--bs-wood-mid);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  cursor: pointer;
}

.sb-select:focus {
  outline: none;
  border-color: var(--bs-gold);
}

.sb-select option {
  background: var(--bs-wood-dark);
  color: var(--bs-parchment);
}

/* ── Deploy button (pirate style) ──────────────────────── */
.sb-deploy-btn {
  padding: 4px 14px;
  font-size: 0.8rem;
}

/* ── Hint text ─────────────────────────────────────────── */
.sb-hint {
  font-size: 0.7rem;
  color: var(--bs-parchment-dim);
}

/* ── Summon chips ──────────────────────────────────────── */
.summon-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 2px 8px;
  border-radius: 12px;
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.75rem;
  font-weight: 600;
  background: rgba(240, 200, 80, 0.12);
  color: var(--bs-gold);
}

.summon-chip-icon {
  display: inline-flex;
  align-items: center;
  line-height: 0;
}

.summon-ram {
  background: rgba(192, 57, 43, 0.15);
  color: var(--bs-fire-red);
  border: 1px solid rgba(192, 57, 43, 0.3);
}

.summon-scout {
  background: rgba(41, 128, 185, 0.15);
  color: var(--bs-ocean-blue);
  border: 1px solid rgba(41, 128, 185, 0.3);
}

.summon-brander {
  background: rgba(230, 126, 34, 0.15);
  color: var(--bs-fire-orange);
  border: 1px solid rgba(230, 126, 34, 0.3);
}

.summon-cursedboat {
  background: rgba(142, 68, 173, 0.15);
  color: var(--bs-cursed-purple);
  border: 1px solid rgba(142, 68, 173, 0.3);
}

.summon-pirateboat {
  background: rgba(212, 168, 71, 0.15);
  color: var(--bs-gold);
  border: 1px solid rgba(212, 168, 71, 0.3);
}

/* ── Position label in chip ────────────────────────────── */
.summon-pos {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 0.65rem;
  opacity: 0.7;
}

/* ── Waiting-for-turn-back indicator ───────────────────── */
.summon-wait {
  font-size: 0.85rem;
  animation: sb-pulse 1.2s ease-in-out infinite;
}

@keyframes sb-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

/* ── Pending entries ───────────────────────────────────── */
.pending-entry {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.pending-name {
  font-family: 'Crimson Text', 'Georgia', serif;
  font-weight: 600;
  color: var(--bs-parchment);
  font-size: 0.85rem;
}

/* ── Boarding badge ────────────────────────────────────── */
.boarding-badge {
  font-size: 0.6rem;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  padding: 1px 6px;
  border-radius: 10px;
  background: rgba(192, 57, 43, 0.18);
  color: var(--bs-fire-red);
  border: 1px solid rgba(192, 57, 43, 0.35);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-weight: 700;
}

/* ── Deploy Mode Banner ────────────────────────────────── */
.deploy-banner {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  background: rgba(41, 128, 185, 0.15);
  border: 1px solid rgba(41, 128, 185, 0.3);
}

.deploy-banner-text {
  font-family: 'Crimson Text', 'Georgia', serif;
  font-weight: 700;
  font-size: 0.85rem;
  color: var(--bs-ocean-light);
}

.deploy-cols {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 0.75rem;
  color: var(--bs-ocean-blue);
}

/* ── Cancel button (ghost style) ───────────────────────── */
.sb-cancel-btn {
  display: inline-flex;
  align-items: center;
  padding: 4px 12px;
  margin-left: 0.5rem;
  border: 1px solid rgba(176, 154, 120, 0.3);
  border-radius: 4px;
  background: transparent;
  color: var(--bs-parchment-dim);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  font-weight: 600;
  cursor: pointer;
  transition: color 0.15s, border-color 0.15s;
}

.sb-cancel-btn:hover {
  color: var(--bs-parchment);
  border-color: var(--bs-parchment-dim);
}

/* ── Tooltip (matches other battleship components) ─────── */
.pc-tooltip {
  position: fixed;
  z-index: 9999;
  padding: 6px 12px;
  border-radius: 4px;
  background: rgba(10, 22, 40, 0.95);
  color: var(--bs-parchment, #e8d5b0);
  font-family: 'Crimson Text', 'Georgia', serif;
  font-size: 0.8rem;
  pointer-events: none;
  white-space: pre-line;
  max-width: 320px;
  transform: translate(12px, -100%);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(232, 213, 176, 0.15);
}
</style>
